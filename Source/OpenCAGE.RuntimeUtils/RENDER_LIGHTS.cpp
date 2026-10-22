#include "RENDER_COMMON.h"
#include "Config.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <iterator>
#include <mutex>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include <imgui.h>

/*
	The debug menu's Rendering > Lights tab: the real-time lights - which are drawn this frame, which cast shadows and
	which wanted to but missed the shadow budget, which feed the bounce light (radiosity) - with their settings, an
	overlay of where they are and how far they reach, and a way to mute a light, play one alone (solo), or leave out
	whole kinds of light, to see what each one does to a scene.

	What is read, at the frame's presentation (render thread), when the frame's lights have all been gathered:
	- the deferred-light renderer's two lists of the lights it draws this frame (up to 64 that may cast shadows, and up to
	  2048 that don't), each entry the light, its material, and whether it casts a shadow this frame;
	- the shadow manager: how many shadow-casting lights a frame may have (4, or 16 in the game's capture mode), the
	  shadow maps' resolution and filtering, the lights that got a shadow map this frame and the ones that wanted one but
	  missed out on the budget;
	- the light manager's lists of every light of the level (the fixed ones, by their mover index, and the moving ones),
	  for the "all lights" list;
	- each light: its matrix (where it is; a spotlight shines along its third axis), colour, the distances its light
	  starts and stops at, the cosines of its cone's outer and inner half-angles, its type and features (shadow, gobo...).

	Four calls the game makes once for each light are turned to ours (see RENDER_COMMON.h), from the first time this tab,
	its overlay or one of its commands is used until the game closes: the handing of a light to the deferred-light
	renderer to draw, to the shadow manager as a shadow candidate, to the lens flares, and to the bounce light. Ours note
	the light (for the shadow candidates and what feeds the bounce light, which are not kept anywhere readable - only
	while the tab or its overlay shows, or for a few seconds after one of its commands) and leave out the lights that are
	muted, not the one played solo, or of a kind left out - so a muted light is not drawn, casts no shadow, has no flare
	and no longer adds to the bounce light. The game passes each light on at once otherwise.

	Names: switches "light.overlay", "light.labels", "light.hide_spot", "light.hide_point", "light.hide_strip",
	"light.only_shadowed", "light.only_gobo"; value "light.overlay_distance"; commands "light.list", "light.shadow_budget",
	"light.mute", "light.unmute", "light.solo", "light.select". A light is named by its id (the hex number "light.list"
	gives, the game's own address for it while it is loaded) or as "mover:N" (its mover index in the level).

	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	using namespace RENDER_COMMON;

	// ---- The game's layouts ----

	// The deferred-light renderer: its lights that may cast shadows (entries of 0x14: the light, its material, its shadow
	// view, whether it casts a shadow this frame) and how many, then the rest and how many
	constexpr uint32_t kShadowingLights = 0x4;
	constexpr uint32_t kShadowingCount = 0x504;
	constexpr uint32_t kOtherLights = 0x508;
	constexpr uint32_t kOtherCount = 0xA508;
	constexpr uint32_t kMaxShadowing = 0x40;
	constexpr uint32_t kMaxOther = 0x800;
	constexpr uint32_t kEntrySize = 0x14;
	constexpr uint32_t kEntryMaterial = 0x4;
	constexpr uint32_t kEntryCastsShadow = 0xC;

	// A light: its matrix (three columns of four: x, y, z axes with the position as each column's last), its GPU constants
	// and its parameters
	constexpr uint32_t kLightColour = 0x40;          // r, g, b
	constexpr uint32_t kLightAttenuationBegin = 0x54;
	constexpr uint32_t kLightNear = 0x58;
	constexpr uint32_t kLightOuterCos = 0x64;        // the cosine of the cone's outer half-angle
	constexpr uint32_t kLightInnerCos = 0x68;
	constexpr uint32_t kLightAreaRadius = 0x6C;
	constexpr uint32_t kLightAttenuationEnd = 0x74;  // how far its light reaches
	constexpr uint32_t kLightSpecularity = 0x78;
	constexpr uint32_t kLightVolumeDensity = 0x8C;
	constexpr uint32_t kLightType = 0xA3;            // 0 ambient, 1 strip, 2 point, 3 spot, 4 directional
	constexpr uint32_t kLightShadowPriority = 0xA4;  // a signed offset
	constexpr uint32_t kLightFeatures = 0xA6;        // bits, see kFeatureNames
	constexpr uint32_t kLightRadiosityFraction = 0xA2; // 0-255
	constexpr uint32_t kLightFadeType = 0xA9;

	// A material: its textures (12 of 8 bytes, a pointer in the low four)
	constexpr uint32_t kMaterialTextures = 0x20;
	constexpr int kMaterialTextureCount = 12;
	// A texture's file header (a pointer), and in that the file name (a pointer)
	constexpr uint32_t kTextureHeader = 0x68;
	constexpr uint32_t kHeaderFileName = 0x28;

	// The shadow manager (a static object): the most shadow-casting lights a frame, the shadow maps' resolution, filter
	// quality and type, the lights given a shadow map this frame (a pointer to entries of 0x28: view, light, quality,
	// cache slot), the shadow candidates' data (0x200 entries of 0xC: priority, quality, light), and the lights that
	// wanted a shadow map and missed out (up to 16) and how many
	constexpr uintptr_t kShadowManager = 0x012AEDB0;
	constexpr uint32_t kShadowMaxPerFrame = 0x8;
	constexpr uint32_t kShadowResolution = 0xC;
	constexpr uint32_t kShadowFilterQuality = 0x10;
	constexpr uint32_t kShadowFilterType = 0x14;
	constexpr uint32_t kShadowCasters = 0x24;
	constexpr uint32_t kCasterSize = 0x28;
	constexpr uint32_t kCasterLight = 0x4;
	constexpr uint32_t kCasterQuality = 0x8;
	constexpr uint32_t kCasterCacheSlot = 0xC;
	constexpr uint32_t kShadowCandidates = 0x34;
	constexpr uint32_t kCandidateSize = 0xC;
	constexpr uint32_t kMaxCandidates = 0x200;
	constexpr uint32_t kShadowMissed = 0x1834;
	constexpr uint32_t kShadowMissedCount = 0x1874;
	constexpr uint32_t kMaxMissed = 16;

	// The light manager: its fixed lights (an array of mover indices, and how many) and its moving lights (an array of
	// light pointers, and how many)
	constexpr uint32_t kManagerStaticIndices = 0x18;
	constexpr uint32_t kManagerStaticCount = 0x1C;
	constexpr uint32_t kManagerDynamic = 0x28;
	constexpr uint32_t kManagerDynamicCount = 0x2C;

	// The calls turned to ours, and the game's functions they reach
	constexpr uintptr_t kAddRenderable = 0x005781F0;
	constexpr uintptr_t kAddCandidate = 0x00582BF0;
	constexpr uintptr_t kAddFlare = 0x005C5160;
	constexpr uintptr_t kFeedRadiosity = 0x005CC550;
	Call g_addRenderableCall = { 0x00588333 };
	Call g_addCandidateCall = { 0x0058830A };
	Call g_addFlareCall = { 0x0058831A };
	Call g_feedRadiosityCalls[] = { { 0x005D8E56 }, { 0x005D8FEB }, { 0x005D90A2 } };

	const char* const kTypeNames[] = { "ambient", "strip", "point", "spot", "directional" };
	struct Feature { uint16_t bit; const char* letter; const char* name; };
	const Feature kFeatureNames[] = {
		{ 0x0004, "S", "shadow" }, { 0x0008, "G", "gobo" }, { 0x0002, "Sp", "specular" }, { 0x2000, "V", "volume" },
		{ 0x0020, "F", "lens flare" }, { 0x0010, "A", "animated" }, { 0x0100, "Ar", "area" }, { 0x0200, "Sq", "square" },
		{ 0x0400, "T", "torch" }, { 0x0001, "Sd", "soft diffuse" }, { 0x0040, "Nc", "no clip" }, { 0x0080, "Db", "diffuse bias" },
		{ 0x0800, "Pa", "physical attenuation" }, { 0x1000, "Gm", "gobo by distance mip" }, { 0x4000, "Na", "not on transparency" },
		{ 0x8000, "Gf", "gobo flipped" },
	};
	constexpr uint16_t kFeatureShadow = 0x0004;
	constexpr uint16_t kFeatureGobo = 0x0008;

	// ---- What the turned calls see and leave out (render tasks' threads) ----

	// Lights noted by the turned calls, a ring each, read back at the presentation
	struct Ring
	{
		static constexpr uint32_t kSize = 2048;
		std::atomic<uint32_t> written = 0;
		std::atomic<uintptr_t> items[kSize] = {};
		uint32_t read = 0; // render thread
	};
	Ring g_candidateRing;
	Ring g_radiosityRing;
	// Whether the turned calls note the lights: only while the tab, the overlay or a command lately wants what they noted
	// (set once a frame, render thread)
	std::atomic<bool> g_noting = false;
	// When a command last asked about the lights (any thread)
	std::atomic<ULONGLONG> g_commandAt = 0;

	void Note(Ring& ring, uintptr_t light)
	{
		const uint32_t at = ring.written.fetch_add(1, std::memory_order_relaxed);
		ring.items[at % Ring::kSize].store(light, std::memory_order_relaxed);
	}

	// The lights muted (up to kMaxMuted; 0 = a free slot), the one played solo (0: none), and the kinds left out
	constexpr int kMaxMuted = 64;
	std::atomic<uintptr_t> g_muted[kMaxMuted] = {};
	std::atomic<uintptr_t> g_solo = 0;
	std::atomic<bool> g_hideSpot = false, g_hidePoint = false, g_hideStrip = false, g_onlyShadowed = false, g_onlyGobo = false;
	std::atomic<bool> g_anyHidden = false; // any of the above, worked out again whenever one changes (render thread)
	std::mutex g_mutedMutex;               // for changing the muted list (not for reading it)

	bool ReadKind(uintptr_t light, uint8_t& type, uint16_t& features)
	{
		return Read(light + kLightType, type) && Read(light + kLightFeatures, features);
	}

	bool Hidden(uintptr_t light)
	{
		if (!g_anyHidden.load(std::memory_order_relaxed) || !light)
			return false;
		const uintptr_t solo = g_solo.load(std::memory_order_relaxed);
		if (solo)
			return light != solo;
		for (const std::atomic<uintptr_t>& muted : g_muted)
			if (muted.load(std::memory_order_relaxed) == light)
				return true;
		if (g_hideSpot || g_hidePoint || g_hideStrip || g_onlyShadowed || g_onlyGobo)
		{
			uint8_t type = 0;
			uint16_t features = 0;
			if (!ReadKind(light, type, features))
				return false;
			if ((type == 3 && g_hideSpot) || (type == 2 && g_hidePoint) || (type == 1 && g_hideStrip))
				return true;
			if (g_onlyShadowed && !(features & kFeatureShadow))
				return true;
			if (g_onlyGobo && !(features & kFeatureGobo))
				return true;
		}
		return false;
	}

	uintptr_t g_addRenderable = 0, g_addCandidate = 0, g_addFlare = 0, g_feedRadiosity = 0;
	using LightCall = void(__thiscall*)(void* self, uintptr_t light);
	using AddRenderable = void(__thiscall*)(void* self, uintptr_t light, uintptr_t material, int castShadows);

	void __fastcall AddRenderableThunk(void* self, void* /*edx*/, uintptr_t light, uintptr_t material, int castShadows)
	{
		if (!Hidden(light))
			reinterpret_cast<AddRenderable>(g_addRenderable)(self, light, material, castShadows);
	}

	void __fastcall AddCandidateThunk(void* self, void* /*edx*/, uintptr_t light)
	{
		if (Hidden(light))
			return;
		if (g_noting.load(std::memory_order_relaxed))
			Note(g_candidateRing, light);
		reinterpret_cast<LightCall>(g_addCandidate)(self, light);
	}

	void __fastcall AddFlareThunk(void* self, void* /*edx*/, uintptr_t light)
	{
		if (!Hidden(light))
			reinterpret_cast<LightCall>(g_addFlare)(self, light);
	}

	void __fastcall FeedRadiosityThunk(void* self, void* /*edx*/, uintptr_t light)
	{
		if (Hidden(light))
			return;
		if (g_noting.load(std::memory_order_relaxed))
			Note(g_radiosityRing, light);
		reinterpret_cast<LightCall>(g_feedRadiosity)(self, light);
	}

	// ---- The calls (render thread) ----

	bool g_callsChecked = false;
	bool g_callsAvailable = false;
	bool g_callsTurned = false;
	std::atomic<bool> g_callsWanted = false; // set by the tab, the overlay or a command: turn them from the next frame

	void CheckCalls()
	{
		if (g_callsChecked)
			return;
		g_callsChecked = true;
		bool ok = Check(g_addRenderableCall, kAddRenderable, "the Lights tab");
		ok = Check(g_addCandidateCall, kAddCandidate, "the Lights tab") && ok;
		ok = Check(g_addFlareCall, kAddFlare, "the Lights tab") && ok;
		for (Call& call : g_feedRadiosityCalls)
			ok = Check(call, kFeedRadiosity, "the Lights tab") && ok;
		g_callsAvailable = ok;
		if (ok)
		{
			g_addRenderable = g_addRenderableCall.target;
			g_addCandidate = g_addCandidateCall.target;
			g_addFlare = g_addFlareCall.target;
			g_feedRadiosity = g_feedRadiosityCalls[0].target;
		}
	}

	void TurnBack()
	{
		Call* calls[] = { &g_addRenderableCall, &g_addCandidateCall, &g_addFlareCall, &g_feedRadiosityCalls[0], &g_feedRadiosityCalls[1], &g_feedRadiosityCalls[2] };
		for (Call* call : calls)
			if (call->turned)
				Turn(*call, nullptr);
		g_callsTurned = false;
	}

	void TurnCalls()
	{
		if (g_callsTurned || !g_callsWanted)
			return;
		CheckCalls();
		if (!g_callsAvailable)
			return;
		bool ok = Turn(g_addRenderableCall, reinterpret_cast<const void*>(&AddRenderableThunk));
		ok = ok && Turn(g_addCandidateCall, reinterpret_cast<const void*>(&AddCandidateThunk));
		ok = ok && Turn(g_addFlareCall, reinterpret_cast<const void*>(&AddFlareThunk));
		for (Call& call : g_feedRadiosityCalls)
			ok = ok && Turn(call, reinterpret_cast<const void*>(&FeedRadiosityThunk));
		if (!ok)
		{
			DevTools::Log("Debug menu: the Lights tab could not change the game's calls (not as they were left); muting and its lists of shadow candidates and bounce-light lights are off for this run.");
			TurnBack();
			g_callsAvailable = false;
			return;
		}
		g_callsTurned = true;
	}

	// ---- Reading the lights (render thread) ----

	struct Light
	{
		uintptr_t object = 0;
		int mover = -1;
		uint8_t type = 0;
		uint16_t features = 0;
		float position[3] = {};
		float direction[3] = {};   // a spotlight's
		float colour[3] = {};
		float begin = 0.0f, end = 0.0f, nearDistance = 0.0f;
		float outerCos = 0.0f, innerCos = 0.0f, areaRadius = 0.0f, specularity = 0.0f, density = 0.0f;
		int shadowPriorityOffset = 0;
		int radiosityFraction = 0;
		int fadeType = 0;
		uintptr_t material = 0;
		bool drawn = false;        // in the deferred-light renderer's lists this frame
		bool castsShadow = false;  // drawn with a shadow this frame
		bool caster = false;       // given a shadow map
		int casterQuality = -1, casterSlot = -1;
		bool missed = false;       // wanted a shadow map, missed the budget
		bool candidate = false;    // a shadow candidate this frame
		float candidatePriority = 0.0f;
		int candidateQuality = -1;
		bool fed = false;          // fed to the bounce light lately
		bool muted = false;
		float distance = 0.0f;
	};

	// A light's fields, read in one go (the matrix to the end of its parameters)
	constexpr uint32_t kLightReadSize = 0xB0;

	template<typename T> T Field(const uint8_t* data, uint32_t offset)
	{
		T value;
		memcpy(&value, data + offset, sizeof(T));
		return value;
	}

	bool ReadLight(uintptr_t object, Light& out)
	{
		uint8_t data[kLightReadSize];
		if (!Read(reinterpret_cast<const void*>(object), data, sizeof(data)))
			return false;
		float matrix[12];
		memcpy(matrix, data, sizeof(matrix));
		out.object = object;
		out.position[0] = matrix[3];
		out.position[1] = matrix[7];
		out.position[2] = matrix[11];
		out.direction[0] = matrix[2];
		out.direction[1] = matrix[6];
		out.direction[2] = matrix[10];
		memcpy(out.colour, data + kLightColour, sizeof(out.colour));
		out.begin = Field<float>(data, kLightAttenuationBegin);
		out.end = Field<float>(data, kLightAttenuationEnd);
		out.nearDistance = Field<float>(data, kLightNear);
		out.outerCos = Field<float>(data, kLightOuterCos);
		out.innerCos = Field<float>(data, kLightInnerCos);
		out.areaRadius = Field<float>(data, kLightAreaRadius);
		out.specularity = Field<float>(data, kLightSpecularity);
		out.density = Field<float>(data, kLightVolumeDensity);
		out.type = Field<uint8_t>(data, kLightType);
		out.features = Field<uint16_t>(data, kLightFeatures);
		out.shadowPriorityOffset = Field<int8_t>(data, kLightShadowPriority);
		out.radiosityFraction = Field<uint8_t>(data, kLightRadiosityFraction);
		out.fadeType = Field<uint8_t>(data, kLightFadeType);
		return true;
	}

	struct ShadowBudget
	{
		bool have = false;
		uint32_t maxPerFrame = 0, resolution = 0, filterQuality = 0, filterType = 0;
		uint32_t casters = 0, missed = 0, candidates = 0;
	};

	struct Snapshot
	{
		bool have = false;            // a level's lights were read
		bool all = false;             // every light of the level, not just those drawn
		std::vector<Light> lights;
		uint32_t shadowingDrawn = 0, otherDrawn = 0, fed = 0;
		ShadowBudget budget;
	};

	// The mover index of each light (from the instance database: every instance of the level by mover index), made again
	// every couple of seconds while wanted
	std::unordered_map<uintptr_t, int> g_movers;
	ULONGLONG g_moversAt = 0;
	uint32_t g_moverCount = 0;

	void UpdateMovers()
	{
		const ULONGLONG now = GetTickCount64();
		if (g_moversAt && now - g_moversAt < 2000)
			return;
		g_moversAt = now;
		g_movers.clear();
		const uintptr_t engine = Engine();
		uint32_t count = 0;
		const uintptr_t array = engine ? Pointer(engine + kEngineInstances + 8) : 0;
		if (!array || !Read(engine + kEngineInstances, count) || count == 0 || count > 200000)
			return;
		std::vector<uintptr_t> instances(count);
		if (!Read(reinterpret_cast<const void*>(array), instances.data(), count * sizeof(uintptr_t)))
			return;
		g_moverCount = count;
		g_movers.reserve(count);
		for (uint32_t i = 0; i < count; i++)
			if (instances[i])
				g_movers.emplace(instances[i], static_cast<int>(i));
	}

	uintptr_t MoverInstance(int mover)
	{
		const uintptr_t engine = Engine();
		uint32_t count = 0;
		if (!engine || mover < 0 || !Read(engine + kEngineInstances, count) || static_cast<uint32_t>(mover) >= count)
			return 0;
		const uintptr_t array = Pointer(engine + kEngineInstances + 8);
		return array ? Pointer(array + mover * sizeof(uintptr_t)) : 0;
	}

	// Lights fed to the bounce light, by when last (a frame count of ours), and the shadow candidates of the last frame
	std::unordered_map<uintptr_t, uint32_t> g_fedAt;
	std::vector<uintptr_t> g_lastCandidates;
	uint32_t g_frame = 0;
	constexpr uint32_t kFedFrames = 30;

	void Drain(Ring& ring, std::vector<uintptr_t>& out)
	{
		const uint32_t written = ring.written.load(std::memory_order_acquire);
		uint32_t from = ring.read;
		if (written - from > Ring::kSize)
			from = written - Ring::kSize;
		for (uint32_t i = from; i != written; i++)
			out.push_back(ring.items[i % Ring::kSize].load(std::memory_order_relaxed));
		ring.read = written;
	}

	// Once a frame (render thread): what the turned calls noted since the last frame
	void DrainRings()
	{
		g_frame++;
		std::vector<uintptr_t> noted;
		Drain(g_radiosityRing, noted);
		for (uintptr_t light : noted)
			g_fedAt[light] = g_frame;
		if (g_fedAt.size() > 4096)
			for (auto it = g_fedAt.begin(); it != g_fedAt.end();)
				it = g_frame - it->second > kFedFrames ? g_fedAt.erase(it) : std::next(it);
		std::vector<uintptr_t> candidates;
		Drain(g_candidateRing, candidates);
		if (!candidates.empty())
			g_lastCandidates.swap(candidates);
	}

	bool IsMuted(uintptr_t light)
	{
		for (const std::atomic<uintptr_t>& muted : g_muted)
			if (muted.load() == light)
				return true;
		return false;
	}

	void UpdateAnyHidden()
	{
		bool any = g_solo != 0 || g_hideSpot || g_hidePoint || g_hideStrip || g_onlyShadowed || g_onlyGobo;
		for (const std::atomic<uintptr_t>& muted : g_muted)
			any = any || muted.load() != 0;
		g_anyHidden = any;
	}

	bool SetMuted(uintptr_t light, bool mute)
	{
		std::lock_guard<std::mutex> lock(g_mutedMutex);
		bool done = false;
		for (std::atomic<uintptr_t>& muted : g_muted)
		{
			if (muted.load() == light)
			{
				if (!mute)
					muted = 0;
				done = true;
			}
		}
		if (mute && !done)
			for (std::atomic<uintptr_t>& muted : g_muted)
				if (muted.load() == 0)
				{
					muted = light;
					done = true;
					break;
				}
		UpdateAnyHidden();
		return done;
	}

	void UnmuteAll()
	{
		std::lock_guard<std::mutex> lock(g_mutedMutex);
		for (std::atomic<uintptr_t>& muted : g_muted)
			muted = 0;
		g_solo = 0;
		UpdateAnyHidden();
	}

	void ReadBudget(ShadowBudget& budget, std::unordered_map<uintptr_t, Light*>& byObject, std::vector<Light>& lights, bool addMissing)
	{
		const uintptr_t manager = Game(kShadowManager);
		if (!manager)
			return;
		budget.have = Read(manager + kShadowMaxPerFrame, budget.maxPerFrame) && Read(manager + kShadowResolution, budget.resolution) &&
			Read(manager + kShadowFilterQuality, budget.filterQuality) && Read(manager + kShadowFilterType, budget.filterType);
		if (!budget.have || budget.maxPerFrame > 64)
		{
			budget.have = false;
			return;
		}
		auto find = [&](uintptr_t object) -> Light*
		{
			auto it = byObject.find(object);
			if (it != byObject.end())
				return it->second;
			if (!addMissing)
				return nullptr;
			Light light;
			if (!ReadLight(object, light))
				return nullptr;
			lights.push_back(light);
			// Pointers into the vector move as it grows: the map is made again
			byObject.clear();
			for (Light& l : lights)
				byObject[l.object] = &l;
			return byObject[object];
		};
		const uintptr_t casters = Pointer(manager + kShadowCasters);
		for (uint32_t i = 0; casters && i < budget.maxPerFrame; i++)
		{
			const uintptr_t entry = casters + i * kCasterSize;
			const uintptr_t object = Pointer(entry + kCasterLight);
			if (!object)
				continue;
			budget.casters++;
			if (Light* light = find(object))
			{
				light->caster = true;
				uint32_t quality = 0, slot = 0;
				if (Read(entry + kCasterQuality, quality))
					light->casterQuality = static_cast<int>(quality);
				if (Read(entry + kCasterCacheSlot, slot))
					light->casterSlot = static_cast<int>(slot);
			}
		}
		uint32_t missed = 0;
		if (Read(manager + kShadowMissedCount, missed) && missed <= kMaxMissed)
		{
			budget.missed = missed;
			for (uint32_t i = 0; i < missed; i++)
				if (Light* light = find(Pointer(manager + kShadowMissed + i * sizeof(uintptr_t))))
					light->missed = true;
		}
		// The candidates the turned call noted last frame, with the priority and quality the game gave each (the shadow
		// manager fills its candidate entries from the first, in the order it is handed them). Every light drawn is handed
		// to it, and it only takes those with the shadow feature and some light (the rest it fades out and leaves)
		budget.candidates = 0;
		const uint32_t search = (std::min)(kMaxCandidates, static_cast<uint32_t>(g_lastCandidates.size()) + 8);
		for (uintptr_t object : g_lastCandidates)
		{
			Light* light = find(object);
			if (!light || !(light->features & kFeatureShadow) || light->colour[0] + light->colour[1] + light->colour[2] <= 0.0f)
				continue;
			budget.candidates++;
			light->candidate = true;
			for (uint32_t i = 0; i < search; i++)
			{
				const uintptr_t entry = manager + kShadowCandidates + i * kCandidateSize;
				if (Pointer(entry + 8) == object)
				{
					uint32_t quality = 0;
					Read(entry, light->candidatePriority);
					if (Read(entry + 4, quality))
						light->candidateQuality = static_cast<int>(quality);
					break;
				}
			}
		}
	}

	// The lights, read now
	void ReadSnapshot(Snapshot& out, bool all)
	{
		out = Snapshot();
		out.all = all;
		const uintptr_t engine = Engine();
		if (!engine)
			return;
		const uintptr_t renderer = Pointer(engine + kEngineDeferredLights);
		if (!renderer)
			return;
		out.have = true;
		UpdateMovers();

		std::unordered_map<uintptr_t, Light*> byObject;
		std::vector<Light>& lights = out.lights;
		lights.reserve(256);
		struct Drawn { uintptr_t object, material; bool shadow; };
		std::vector<Drawn> drawn;
		uint32_t shadowing = 0, other = 0;
		Read(renderer + kShadowingCount, shadowing);
		Read(renderer + kOtherCount, other);
		shadowing = (std::min)(shadowing, kMaxShadowing);
		other = (std::min)(other, kMaxOther);
		out.shadowingDrawn = shadowing;
		out.otherDrawn = other;
		for (int list = 0; list < 2; list++)
		{
			const uint32_t count = list == 0 ? shadowing : other;
			const uintptr_t base = renderer + (list == 0 ? kShadowingLights : kOtherLights);
			for (uint32_t i = 0; i < count; i++)
			{
				const uintptr_t entry = base + i * kEntrySize;
				uint8_t casts = 0;
				const uintptr_t object = Pointer(entry);
				if (object)
					drawn.push_back({ object, Pointer(entry + kEntryMaterial), Read(entry + kEntryCastsShadow, casts) && casts });
			}
		}
		std::unordered_set<uintptr_t> seen;
		auto add = [&](uintptr_t object) -> Light*
		{
			if (!object || !seen.insert(object).second)
				return nullptr;
			Light light;
			if (!ReadLight(object, light))
				return nullptr;
			lights.push_back(light);
			return &lights.back();
		};
		for (const Drawn& d : drawn)
			if (Light* light = add(d.object))
				light->material = d.material;
		if (all)
		{
			const uintptr_t manager = engine + kEngineLightManager;
			uint32_t count = 0;
			const uintptr_t indices = Pointer(manager + kManagerStaticIndices);
			if (indices && Read(manager + kManagerStaticCount, count) && count < 100000)
				for (uint32_t i = 0; i < count; i++)
				{
					uint32_t mover = 0;
					if (Read(indices + i * 4, mover))
						add(MoverInstance(static_cast<int>(mover)));
				}
			const uintptr_t dynamic = Pointer(manager + kManagerDynamic);
			if (dynamic && Read(manager + kManagerDynamicCount, count) && count < 10000)
				for (uint32_t i = 0; i < count; i++)
					add(Pointer(dynamic + i * 4));
		}
		for (Light& light : lights)
			byObject[light.object] = &light;
		for (const Drawn& d : drawn)
			if (Light* light = byObject[d.object])
			{
				light->drawn = true;
				light->castsShadow = light->castsShadow || d.shadow;
				if (!light->material)
					light->material = d.material;
			}
		ReadBudget(out.budget, byObject, lights, true);

		float camera[3] = {};
		const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
		for (Light& light : lights)
		{
			auto mover = g_movers.find(light.object);
			light.mover = mover != g_movers.end() ? mover->second : -1;
			auto fed = g_fedAt.find(light.object);
			light.fed = fed != g_fedAt.end() && g_frame - fed->second <= kFedFrames;
			out.fed += light.fed ? 1 : 0;
			light.muted = IsMuted(light.object);
			if (haveCamera)
			{
				const float dx = light.position[0] - camera[0], dy = light.position[1] - camera[1], dz = light.position[2] - camera[2];
				light.distance = std::sqrt(dx * dx + dy * dy + dz * dz);
			}
		}
		std::stable_sort(lights.begin(), lights.end(), [](const Light& a, const Light& b) { return a.distance < b.distance; });
	}

	// ---- Names and text ----

	std::string Hex(uintptr_t value)
	{
		char text[16];
		snprintf(text, sizeof(text), "%08X", static_cast<unsigned>(value));
		return text;
	}

	const char* TypeName(uint8_t type)
	{
		return type < sizeof(kTypeNames) / sizeof(kTypeNames[0]) ? kTypeNames[type] : "?";
	}

	std::string FeatureLetters(uint16_t features)
	{
		std::string text;
		for (const Feature& feature : kFeatureNames)
			if (features & feature.bit)
				text += std::string(text.empty() ? "" : " ") + feature.letter;
		return text;
	}

	std::string FeatureWords(uint16_t features)
	{
		std::string text;
		for (const Feature& feature : kFeatureNames)
			if (features & feature.bit)
				text += std::string(text.empty() ? "" : ", ") + feature.name;
		return text.empty() ? "none" : text;
	}

	float ConeDegrees(float cosine)
	{
		cosine = (std::max)(-1.0f, (std::min)(1.0f, cosine));
		return std::acos(cosine) * 57.29578f;
	}

	// The material's texture file names (the gobo's among them), comma separated
	std::string TextureNames(uintptr_t material)
	{
		std::string text;
		for (int i = 0; material && i < kMaterialTextureCount; i++)
		{
			const uintptr_t texture = Pointer(material + kMaterialTextures + i * 8);
			const uintptr_t header = texture ? Pointer(texture + kTextureHeader) : 0;
			const std::string name = header ? Text(Pointer(header + kHeaderFileName), 160) : std::string();
			if (!name.empty())
				text += (text.empty() ? "" : ", ") + name;
		}
		return text;
	}

	std::string LightLine(const Light& light)
	{
		char text[512];
		snprintf(text, sizeof(text), "id=%s mover=%d type=%s pos=%.2f,%.2f,%.2f range=%.2f begin=%.2f colour=%.3f,%.3f,%.3f cone=%.1f/%.1f features=%s drawn=%d shadow=%d caster=%d missed=%d candidate=%d fed=%d muted=%d dist=%.1f",
			Hex(light.object).c_str(), light.mover, TypeName(light.type), light.position[0], light.position[1], light.position[2], light.end, light.begin,
			light.colour[0], light.colour[1], light.colour[2], light.type == 3 ? ConeDegrees(light.innerCos) * 2.0f : 0.0f, light.type == 3 ? ConeDegrees(light.outerCos) * 2.0f : 0.0f,
			FeatureLetters(light.features).c_str(), light.drawn, light.castsShadow, light.caster, light.missed, light.candidate, light.fed, light.muted, light.distance);
		return text;
	}

	const char* FilterTypeName(uint32_t type)
	{
		switch (type)
		{
		case 0: return "PCF";
		case 1: return "contact hardening";
		case 2: return "ESM";
		default: return "?";
		}
	}

	std::string BudgetText(const Snapshot& snapshot)
	{
		const ShadowBudget& budget = snapshot.budget;
		char text[512];
		snprintf(text, sizeof(text), "available=%d\nmax_per_frame=%u\nresolution=%u\nfilter_quality=%u\nfilter_type=%u\ncasters=%u\nmissed=%u\ncandidates=%u\ndrawn_shadowing=%u/%u\ndrawn_other=%u/%u\nfed_to_radiosity=%u\nextra_shadow_lights=%d",
			budget.have, budget.maxPerFrame, budget.resolution, budget.filterQuality, budget.filterType, budget.casters, budget.missed, budget.candidates,
			snapshot.shadowingDrawn, kMaxShadowing, snapshot.otherDrawn, kMaxOther, snapshot.fed, budget.maxPerFrame > 4);
		std::string out = text;
		for (const Light& light : snapshot.lights)
			if (light.caster)
				out += "\ncaster=" + Hex(light.object) + " quality=" + std::to_string(light.casterQuality) + " slot=" + std::to_string(light.casterSlot);
		for (const Light& light : snapshot.lights)
			if (light.missed)
				out += "\nmissed=" + Hex(light.object);
		return out;
	}

	// A light named by its id (hex) or "mover:N": 0 when none
	uintptr_t ParseLight(const std::string& argument)
	{
		std::string text = argument;
		while (!text.empty() && isspace(static_cast<unsigned char>(text.back())))
			text.pop_back();
		while (!text.empty() && isspace(static_cast<unsigned char>(text.front())))
			text.erase(text.begin());
		if (text.rfind("mover:", 0) == 0)
			return MoverInstance(atoi(text.c_str() + 6));
		if (text.rfind("0x", 0) == 0 || text.rfind("0X", 0) == 0)
			text = text.substr(2);
		if (text.empty())
			return 0;
		char* end = nullptr;
		const unsigned long value = strtoul(text.c_str(), &end, 16);
		return end && *end == '\0' ? static_cast<uintptr_t>(value) : 0;
	}

	// ---- State of the view (render thread unless said) ----

	std::atomic<bool> g_overlay = false, g_labels = false;
	std::atomic<float> g_overlayDistance = 30.0f;
	std::atomic<uintptr_t> g_selected = 0;
	bool g_showAll = false;
	bool g_registered = false;
	Snapshot g_snapshot;         // the tab's and overlay's, read once a frame while wanted
	ULONGLONG g_tabShownAt = 0;

	void Register();

	void ApplyFilters()
	{
		UpdateAnyHidden();
	}

	// Once a frame from the overlay: the calls turned once wanted, what they noted read, the lights read while shown
	void Frame(ImDrawList* drawList);

	void SwitchBox(const char* label, std::atomic<bool>& value, const char* tooltip)
	{
		bool on = value;
		if (ImGui::Checkbox(label, &on))
		{
			value = on;
			ApplyFilters();
			g_callsWanted = true;
		}
		if (tooltip && ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
		{
			ImGui::BeginTooltip();
			ImGui::PushTextWrapPos(ImGui::GetFontSize() * 28.0f);
			ImGui::TextUnformatted(tooltip);
			ImGui::PopTextWrapPos();
			ImGui::EndTooltip();
		}
	}

	ImU32 LightColour(const Light& light, float alpha)
	{
		float r = light.colour[0], g = light.colour[1], b = light.colour[2];
		const float peak = (std::max)(r, (std::max)(g, b));
		if (peak > 1e-4f)
		{
			r /= peak;
			g /= peak;
			b /= peak;
		}
		else
			r = g = b = 0.5f;
		// Kept light enough to see over a dark scene
		r = 0.35f + 0.65f * r;
		g = 0.35f + 0.65f * g;
		b = 0.35f + 0.65f * b;
		return ImGui::ColorConvertFloat4ToU32(ImVec4(r, g, b, alpha));
	}

	// A circle about an axis (unit), in the plane through centre square to it
	void AxisCircle(ImDrawList* drawList, const float centre[3], const float axis[3], float radius, ImU32 colour, float thickness)
	{
		// Two directions square to the axis
		float u[3] = { 0.0f, 0.0f, 0.0f };
		if (std::fabs(axis[1]) < 0.9f)
			u[1] = 1.0f;
		else
			u[0] = 1.0f;
		float a[3] = { axis[1] * u[2] - axis[2] * u[1], axis[2] * u[0] - axis[0] * u[2], axis[0] * u[1] - axis[1] * u[0] };
		const float la = std::sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);
		if (la < 1e-5f)
			return;
		for (float& v : a)
			v /= la;
		const float b[3] = { axis[1] * a[2] - axis[2] * a[1], axis[2] * a[0] - axis[0] * a[2], axis[0] * a[1] - axis[1] * a[0] };
		constexpr int kSegments = 24;
		float previous[3];
		for (int i = 0; i <= kSegments; i++)
		{
			const float t = 6.2831853f * i / kSegments;
			const float c = std::cos(t) * radius, s = std::sin(t) * radius;
			const float point[3] = { centre[0] + a[0] * c + b[0] * s, centre[1] + a[1] * c + b[1] * s, centre[2] + a[2] * c + b[2] * s };
			if (i > 0)
				DEBUG_MENU::WorldLine(drawList, previous, point, colour, thickness);
			memcpy(previous, point, sizeof(previous));
		}
	}

	void DrawLight(ImDrawList* drawList, const Light& light, float fade, bool selected, int index)
	{
		const bool hidden = light.muted || (g_solo != 0 && g_solo != light.object);
		const ImU32 colour = hidden ? IM_COL32(120, 120, 120, static_cast<int>(160 * fade)) : LightColour(light, 0.9f * fade);
		const float thickness = selected ? 3.0f : light.caster ? 2.2f : 1.3f;
		DEBUG_MENU::WorldCross(drawList, light.position, 0.15f, colour, thickness);
		if (light.type == 3)
		{
			// The cone: from the light along its axis to where its light ends, its outer edge drawn at the end
			float axis[3] = { light.direction[0], light.direction[1], light.direction[2] };
			const float length = std::sqrt(axis[0] * axis[0] + axis[1] * axis[1] + axis[2] * axis[2]);
			if (length > 1e-5f)
			{
				for (float& v : axis)
					v /= length;
				const float reach = (std::min)(light.end, 50.0f);
				const float half = std::acos((std::max)(-1.0f, (std::min)(1.0f, light.outerCos)));
				const float radius = reach * std::tan((std::min)(half, 1.45f));
				const float end[3] = { light.position[0] + axis[0] * reach, light.position[1] + axis[1] * reach, light.position[2] + axis[2] * reach };
				DEBUG_MENU::WorldLine(drawList, light.position, end, colour, 1.0f);
				AxisCircle(drawList, end, axis, radius, colour, thickness);
				// Four lines from the light to the rim
				float u[3] = { 0.0f, 0.0f, 0.0f };
				if (std::fabs(axis[1]) < 0.9f)
					u[1] = 1.0f;
				else
					u[0] = 1.0f;
				float a[3] = { axis[1] * u[2] - axis[2] * u[1], axis[2] * u[0] - axis[0] * u[2], axis[0] * u[1] - axis[1] * u[0] };
				const float la = std::sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);
				for (float& v : a)
					v /= la;
				const float b[3] = { axis[1] * a[2] - axis[2] * a[1], axis[2] * a[0] - axis[0] * a[2], axis[0] * a[1] - axis[1] * a[0] };
				for (int i = 0; i < 4; i++)
				{
					const float* side = i < 2 ? a : b;
					const float sign = (i % 2) ? -1.0f : 1.0f;
					const float rim[3] = { end[0] + side[0] * radius * sign, end[1] + side[1] * radius * sign, end[2] + side[2] * radius * sign };
					DEBUG_MENU::WorldLine(drawList, light.position, rim, colour, 1.0f);
				}
			}
		}
		else
		{
			// How far it reaches: three circles of a sphere
			const float reach = (std::min)(light.end, 50.0f);
			const float axes[3][3] = { { 1.0f, 0.0f, 0.0f }, { 0.0f, 1.0f, 0.0f }, { 0.0f, 0.0f, 1.0f } };
			for (const auto& axis : axes)
				AxisCircle(drawList, light.position, axis, reach, colour, selected ? 2.0f : 1.0f);
		}
		if (g_labels || selected)
		{
			char text[128];
			snprintf(text, sizeof(text), "#%d %s%s%s%s%s", index, TypeName(light.type), light.mover >= 0 ? (" mover " + std::to_string(light.mover)).c_str() : "",
				light.caster ? " SHADOW" : light.missed ? " missed shadow" : "", light.fed ? " radiosity" : "", hidden ? " MUTED" : "");
			ScreenLabel(drawList, light.position, text, colour, -12.0f);
		}
	}

	void Frame(ImDrawList* drawList)
	{
		Register();
		// The switches may have been set from outside the window
		UpdateAnyHidden();
		if (g_anyHidden)
			g_callsWanted = true;
		TurnCalls();
		const bool shown = g_tabShownAt && GetTickCount64() - g_tabShownAt < 500;
		// The lights are noted, and what was noted read, only while something shows it or a command lately asked (muting
		// and the rest don't need it); otherwise what was noted is dropped
		const bool wanted = g_overlay || shown || GetTickCount64() - g_commandAt.load() < 3000;
		g_noting.store(wanted, std::memory_order_relaxed);
		if (wanted)
			DrainRings();
		else
		{
			g_candidateRing.read = g_candidateRing.written.load();
			g_radiosityRing.read = g_radiosityRing.written.load();
			if (!g_fedAt.empty())
				g_fedAt.clear();
			g_lastCandidates.clear();
		}
		if (!g_overlay && !shown)
		{
			if (!g_snapshot.lights.empty())
				g_snapshot = Snapshot();
			return;
		}
		g_callsWanted = true;
		ReadSnapshot(g_snapshot, g_showAll);
		if (!g_overlay || !drawList)
			return;
		const float maxDistance = g_overlayDistance;
		const uintptr_t selected = g_selected;
		ImVec2 windowMin, windowMax;
		const bool window = DEBUG_MENU::WindowRect(windowMin, windowMax);
		int index = 0;
		for (const Light& light : g_snapshot.lights)
		{
			index++;
			if (light.distance > maxDistance && light.object != selected)
				continue;
			// Faded with distance, and kept off the window
			ImVec2 at;
			if (window && DEBUG_MENU::Project(light.position, at) && at.x >= windowMin.x && at.x <= windowMax.x && at.y >= windowMin.y && at.y <= windowMax.y)
				continue;
			const float fade = light.object == selected ? 1.0f : (std::max)(0.25f, 1.0f - light.distance / (maxDistance * 1.25f));
			DrawLight(drawList, light, fade, light.object == selected, index);
		}
	}

	void Overlay(ImDrawList* drawList)
	{
		Frame(drawList);
	}

	void Tooltip(const char* text)
	{
		if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
			return;
		ImGui::BeginTooltip();
		ImGui::PushTextWrapPos(ImGui::GetFontSize() * 28.0f);
		ImGui::TextUnformatted(text);
		ImGui::PopTextWrapPos();
		ImGui::EndTooltip();
	}

	void DrawDetails(const Light& light)
	{
		ImGui::Text("Light %s  %s%s", Hex(light.object).c_str(), TypeName(light.type), light.mover >= 0 ? (", mover index " + std::to_string(light.mover)).c_str() : " (moving or spawned: no mover index)");
		ImGui::Text("Position %.2f, %.2f, %.2f   %.1f m from the camera", light.position[0], light.position[1], light.position[2], light.distance);
		ImGui::ColorButton("##lightcolour", ImVec4((std::min)(1.0f, light.colour[0]), (std::min)(1.0f, light.colour[1]), (std::min)(1.0f, light.colour[2]), 1.0f), ImGuiColorEditFlags_NoTooltip);
		ImGui::SameLine();
		ImGui::Text("Colour (with intensity) %.3f, %.3f, %.3f", light.colour[0], light.colour[1], light.colour[2]);
		ImGui::Text("Light from %.2f to %.2f m (near %.2f), specularity %.2f, area radius %.2f", light.begin, light.end, light.nearDistance, light.specularity, light.areaRadius);
		if (light.type == 3)
			ImGui::Text("Cone %.1f degrees inner, %.1f outer; direction %.2f, %.2f, %.2f", ConeDegrees(light.innerCos) * 2.0f, ConeDegrees(light.outerCos) * 2.0f, light.direction[0], light.direction[1], light.direction[2]);
		ImGui::TextWrapped("Features: %s", FeatureWords(light.features).c_str());
		ImGui::Text("Shadow priority offset %d, bounce-light share %.0f%%, fade type %d, volume density %.3f", light.shadowPriorityOffset, light.radiosityFraction / 2.55f, light.fadeType, light.density);
		const std::string textures = TextureNames(light.material);
		ImGui::TextWrapped("Material textures: %s", textures.empty() ? "(none read)" : textures.c_str());
		std::string state;
		state += light.drawn ? "drawn this frame" : "not drawn this frame";
		if (light.castsShadow)
			state += ", drawn with a shadow";
		if (light.caster)
			state += ", has shadow map (quality " + std::to_string(light.casterQuality) + ", cache slot " + std::to_string(light.casterSlot) + ")";
		if (light.missed)
			state += ", WANTED A SHADOW MAP BUT MISSED THE BUDGET";
		if (light.candidate)
		{
			char candidate[96];
			snprintf(candidate, sizeof(candidate), ", shadow candidate (priority %.3f, quality %d)", light.candidatePriority, light.candidateQuality);
			state += candidate;
		}
		state += light.fed ? ", feeds the bounce light" : ", not feeding the bounce light";
		ImGui::TextWrapped("State: %s.", state.c_str());
		const bool muted = light.muted;
		if (ImGui::Button(muted ? "Unmute##light" : "Mute##light"))
		{
			g_callsWanted = true;
			SetMuted(light.object, !muted);
		}
		ImGui::SameLine();
		const bool solo = g_solo == light.object;
		if (ImGui::Button(solo ? "End solo##light" : "Solo##light"))
		{
			g_callsWanted = true;
			std::lock_guard<std::mutex> lock(g_mutedMutex);
			g_solo = solo ? 0 : light.object;
			UpdateAnyHidden();
		}
		Tooltip("Solo: only this light is drawn, casts shadows and feeds the bounce light; every other light is left out.");
		ImGui::SameLine();
		if (ImGui::Button("Copy id##light"))
			ImGui::SetClipboardText(Hex(light.object).c_str());
	}

	void DrawTab()
	{
		g_tabShownAt = GetTickCount64();
		g_callsWanted = true;
		Register();
		const Snapshot& snapshot = g_snapshot;

		ImGui::TextWrapped("The real-time lights: which are drawn this frame, which cast shadows (and which wanted to but missed the budget), which feed the bounce light. Mute or solo a light to see what it does, or leave out whole kinds.");
		if (!g_callsAvailable && g_callsChecked)
			ColouredNote(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "Muting, the shadow candidates and the bounce-light list are not available on this game release.");
		if (!snapshot.have)
		{
			ImGui::TextDisabled("No level running.");
			return;
		}

		// The budget
		const ShadowBudget& budget = snapshot.budget;
		ImGui::TextWrapped("Drawn: %u that may cast shadows (of %u), %u others (of %u); %u feeding the bounce light", snapshot.shadowingDrawn, kMaxShadowing, snapshot.otherDrawn, kMaxOther, snapshot.fed);
		if (budget.have)
		{
			ImGui::TextWrapped("Shadow maps: %u of %u this frame, %u candidates, %u missed out", budget.casters, budget.maxPerFrame, budget.candidates, budget.missed);
			RENDER_COMMON::Note("Shadow map resolution %u pixels, filter %s (quality %u). %s", budget.resolution, FilterTypeName(budget.filterType), budget.filterQuality,
				budget.maxPerFrame > 4 ? "16 shadow-casting lights a frame (the game's capture mode) is on." : "ExtraShadowLights=1 in OpenCAGE.RuntimeUtils.ini raises the budget to 16 at the next start.");
			if (budget.missed > 0)
				ColouredNote(ImVec4(1.0f, 0.8f, 0.3f, 1.0f), "%u light(s) wanted a shadow map this frame and missed out on the budget (marked below).", budget.missed);
		}

		// Overlay and filters
		SwitchBox("Overlay##lights", g_overlay, "light.overlay: each light over the game - a cross where it is, its reach (a sphere, or a spotlight's cone), its colour; thicker for those with a shadow map, grey when muted.");
		ImGui::SameLine();
		SwitchBox("Labels##lights", g_labels, "light.labels: each light's number in the list, type, mover index and state over it.");
		ImGui::SameLine();
		float distance = g_overlayDistance;
		ImGui::SetNextItemWidth(ImGui::GetFontSize() * 8.0f);
		if (ImGui::SliderFloat("Within (m)##lights", &distance, 5.0f, 200.0f, "%.0f"))
			g_overlayDistance = distance;
		ImGui::TextDisabled("Leave out:");
		ImGui::SameLine();
		SwitchBox("Spotlights##lights", g_hideSpot, "light.hide_spot: spotlights are not drawn, cast no shadows and don't feed the bounce light.");
		ImGui::SameLine();
		SwitchBox("Point lights##lights", g_hidePoint, "light.hide_point: point (omni) lights are left out.");
		ImGui::SameLine();
		SwitchBox("Strip lights##lights", g_hideStrip, "light.hide_strip: strip lights are left out.");
		SwitchBox("Only lights with shadows##lights", g_onlyShadowed, "light.only_shadowed: every light without the shadow feature is left out.");
		ImGui::SameLine();
		SwitchBox("Only lights with gobos##lights", g_onlyGobo, "light.only_gobo: every light without a gobo (a projected pattern) is left out.");
		int mutedCount = 0;
		for (const std::atomic<uintptr_t>& muted : g_muted)
			mutedCount += muted.load() ? 1 : 0;
		ImGui::BeginDisabled(mutedCount == 0 && g_solo == 0);
		if (ImGui::Button("Unmute all##lights"))
			UnmuteAll();
		ImGui::EndDisabled();
		ImGui::SameLine();
		ImGui::TextDisabled("%d muted%s", mutedCount, g_solo != 0 ? ", one played solo" : "");
		ImGui::SameLine();
		ImGui::Checkbox("Every light of the level##lights", &g_showAll);
		Tooltip("List every light the level has (its fixed lights and the moving ones), not just those drawn this frame.");

		// The selected light
		const uintptr_t selectedObject = g_selected;
		for (const Light& light : snapshot.lights)
			if (light.object == selectedObject)
			{
				ImGui::Separator();
				DrawDetails(light);
				break;
			}

		ImGui::Separator();
		// The letters the Features column uses, above the list so it is in sight with it: each letter and its name kept
		// together on a line, wrapping at the window's edge as prose does
		std::vector<std::string> legend = { "Features:" };
		for (size_t i = 0; i < std::size(kFeatureNames); i++)
			legend.push_back(std::string(kFeatureNames[i].letter) + " " + kFeatureNames[i].name + (i + 1 == std::size(kFeatureNames) ? "." : ","));
		for (const char* word : { "Hover", "a", "light's", "features", "for", "them", "in", "words." })
			legend.push_back(word);
		{
			const float right = ImGui::GetCursorScreenPos().x + ImGui::GetContentRegionAvail().x;
			const float space = ImGui::CalcTextSize(" ").x;
			ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing, ImVec2(ImGui::GetStyle().ItemSpacing.x, 0.0f));
			for (size_t i = 0; i < legend.size(); i++)
			{
				// The last piece keeps the usual spacing under it
				if (i + 1 == legend.size())
					ImGui::PopStyleVar();
				if (i > 0 && ImGui::GetItemRectMax().x + space + ImGui::CalcTextSize(legend[i].c_str()).x <= right)
					ImGui::SameLine(0.0f, space);
				ImGui::TextDisabled("%s", legend[i].c_str());
			}
		}
		// Scrolls sideways (the # column kept in sight) once the window is too narrow for the list's words, rather than
		// squeezing them; wider, the two columns of words share what is left, so the list fills the window
		const float minimumWidth = ImGui::GetFontSize() * 56.0f;
		const bool scrollX = ImGui::GetContentRegionAvail().x < minimumWidth;
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_Resizable |
			(scrollX ? ImGuiTableFlags_ScrollX : ImGuiTableFlags_None);
		const float height = (std::max)(ImGui::GetTextLineHeightWithSpacing() * 6.0f, ImGui::GetContentRegionAvail().y);
		if (ImGui::BeginTable("lights", 9, flags, ImVec2(0.0f, height), scrollX ? minimumWidth : 0.0f))
		{
			ImGui::TableSetupScrollFreeze(1, 1);
			ImGui::TableSetupColumn("#");
			ImGui::TableSetupColumn("Type");
			ImGui::TableSetupColumn("Mover");
			ImGui::TableSetupColumn("Colour");
			ImGui::TableSetupColumn("Reach (m)");
			// The two columns of words share what is left of the width, so the list fills the window
			ImGui::TableSetupColumn("Features", ImGuiTableColumnFlags_WidthStretch, 1.0f);
			ImGui::TableSetupColumn("State", ImGuiTableColumnFlags_WidthStretch, 2.0f);
			ImGui::TableSetupColumn("Distance (m)");
			ImGui::TableSetupColumn("Mute");
			ImGui::TableHeadersRow();
			int index = 0;
			ImGuiListClipper clipper;
			clipper.Begin(static_cast<int>(snapshot.lights.size()));
			while (clipper.Step())
				for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
				{
					const Light& light = snapshot.lights[row];
					index = row + 1;
					ImGui::TableNextRow();
					ImGui::PushID(static_cast<int>(light.object));
					ImGui::TableNextColumn();
					char number[16];
					snprintf(number, sizeof(number), "%d", index);
					if (ImGui::Selectable(number, light.object == selectedObject, ImGuiSelectableFlags_SpanAllColumns | ImGuiSelectableFlags_AllowOverlap))
						g_selected = light.object == selectedObject ? 0 : light.object;
					ImGui::TableNextColumn();
					ImGui::TextUnformatted(TypeName(light.type));
					ImGui::TableNextColumn();
					if (light.mover >= 0)
						ImGui::Text("%d", light.mover);
					else
						ImGui::TextDisabled("-");
					ImGui::TableNextColumn();
					ImGui::ColorButton("##c", ImVec4((std::min)(1.0f, light.colour[0]), (std::min)(1.0f, light.colour[1]), (std::min)(1.0f, light.colour[2]), 1.0f), ImGuiColorEditFlags_NoTooltip, ImVec2(ImGui::GetFontSize(), ImGui::GetFontSize()));
					ImGui::SameLine();
					ImGui::Text("%.2f", (std::max)(light.colour[0], (std::max)(light.colour[1], light.colour[2])));
					ImGui::TableNextColumn();
					ImGui::Text("%.1f", light.end);
					ImGui::TableNextColumn();
					const std::string letters = FeatureLetters(light.features);
					if (letters.empty())
						ImGui::TextDisabled("none");
					else
						ImGui::TextUnformatted(letters.c_str());
					if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
						ImGui::SetTooltip("%s", FeatureWords(light.features).c_str());
					ImGui::TableNextColumn();
					// What it does this frame, in words
					std::string state = light.drawn ? "drawn" : "not drawn";
					if (light.caster)
						state += ", SHADOW MAP";
					else if (light.missed)
						state += ", missed a shadow map";
					else if (light.castsShadow)
						state += ", drawn with a shadow";
					if (light.candidate && !light.caster)
						state += ", shadow candidate";
					if (light.fed)
						state += ", feeds the bounce light";
					if (light.missed)
						ImGui::TextColored(ImVec4(1.0f, 0.8f, 0.3f, 1.0f), "%s", state.c_str());
					else
						ImGui::TextUnformatted(state.c_str());
					ImGui::TableNextColumn();
					ImGui::Text("%.1f", light.distance);
					ImGui::TableNextColumn();
					bool muted = light.muted;
					if (ImGui::Checkbox("##mute", &muted))
						SetMuted(light.object, muted);
					ImGui::PopID();
				}
			ImGui::EndTable();
		}
	}

	DEBUG_MENU::CommandResult ListCommand(const std::string& argument)
	{
		const bool all = argument.find("all") != std::string::npos;
		g_callsWanted = true;
		g_commandAt = GetTickCount64();
		return DEBUG_MENU::AnswerOnRenderThread([all]() -> DEBUG_MENU::CommandResult
		{
			Snapshot snapshot;
			ReadSnapshot(snapshot, all);
			if (!snapshot.have)
				return { false, "No level running" };
			std::string text;
			for (const Light& light : snapshot.lights)
				text += LightLine(light) + "\n";
			return { true, text.empty() ? std::string("(no lights)") : text };
		});
	}

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddSwitch("light.overlay", &g_overlay);
		DEBUG_MENU::AddSwitch("light.labels", &g_labels);
		DEBUG_MENU::AddSwitch("light.hide_spot", &g_hideSpot);
		DEBUG_MENU::AddSwitch("light.hide_point", &g_hidePoint);
		DEBUG_MENU::AddSwitch("light.hide_strip", &g_hideStrip);
		DEBUG_MENU::AddSwitch("light.only_shadowed", &g_onlyShadowed);
		DEBUG_MENU::AddSwitch("light.only_gobo", &g_onlyGobo);
		DEBUG_MENU::AddValue("light.overlay_distance", &g_overlayDistance, 5.0f, 200.0f);
		DEBUG_MENU::AddCommand("light.list", "the lights drawn this frame (argument \"all\": every light of the level), nearest first, a line each", ListCommand);
		DEBUG_MENU::AddCommand("light.shadow_budget", "the shadow budget: the most shadow-casting lights a frame, this frame's casters, candidates and those that missed out", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			g_callsWanted = true;
			g_commandAt = GetTickCount64();
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				Snapshot snapshot;
				ReadSnapshot(snapshot, false);
				if (!snapshot.have)
					return { false, "No level running" };
				return { true, BudgetText(snapshot) };
			});
		});
		DEBUG_MENU::AddCommand("light.mute", "mutes a light (its id from light.list, or mover:N): not drawn, no shadow, no flare, no bounce light", [](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			const uintptr_t light = ParseLight(argument);
			if (!light)
				return { false, "Name a light: its id from light.list, or mover:N" };
			g_callsWanted = true;
			g_commandAt = GetTickCount64();
			return SetMuted(light, true) ? DEBUG_MENU::CommandResult{ true, "Muted " + Hex(light) } : DEBUG_MENU::CommandResult{ false, "Too many lights muted already (64)" };
		});
		DEBUG_MENU::AddCommand("light.unmute", "unmutes a light (id or mover:N), or every light and the solo with \"all\"", [](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			if (argument.find("all") != std::string::npos)
			{
				UnmuteAll();
				return { true, "Every light unmuted, solo ended" };
			}
			const uintptr_t light = ParseLight(argument);
			if (!light)
				return { false, "Name a light: its id from light.list, or mover:N, or all" };
			SetMuted(light, false);
			return { true, "Unmuted " + Hex(light) };
		});
		DEBUG_MENU::AddCommand("light.solo", "plays one light alone (id or mover:N): every other light left out; \"none\" ends it", [](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			g_callsWanted = true;
			g_commandAt = GetTickCount64();
			const uintptr_t light = argument.find("none") != std::string::npos ? 0 : ParseLight(argument);
			if (!light && argument.find("none") == std::string::npos)
				return { false, "Name a light: its id from light.list, or mover:N, or none" };
			std::lock_guard<std::mutex> lock(g_mutedMutex);
			g_solo = light;
			UpdateAnyHidden();
			return { true, light ? "Solo " + Hex(light) : std::string("Solo ended") };
		});
		DEBUG_MENU::AddCommand("light.select", "selects a light in the Lights tab and the overlay (id or mover:N; \"none\" clears it)", [](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			const uintptr_t light = argument.find("none") != std::string::npos ? 0 : ParseLight(argument);
			g_selected = light;
			g_commandAt = GetTickCount64();
			return { true, light ? "Selected " + Hex(light) : std::string("Nothing selected") };
		});
	}

	// The 16 shadow-casting lights a frame of the game's capture mode, when OpenCAGE.RuntimeUtils.ini asks for it: the
	// game sizes its shadow lists by this flag once, as the renderer starts (after the DLL has loaded), and reads it every
	// frame after, so it is set as the hooks attach and never changed while the game runs
	constexpr uintptr_t kExtraShadowLights = 0x012F0CA7;

	void AttachHooks(bool attach)
	{
		if (attach)
		{
			if (Config::IniInt("ExtraShadowLights", 0) != 0)
			{
				const uintptr_t flag = Game(kExtraShadowLights);
				const uint8_t on = 1;
				// Only before the shadow manager is made (its budget still zero): set after, the per-frame code would
				// use lists sized for 4 as if they held 16
				uint32_t budget = 1;
				const uintptr_t manager = Game(kShadowManager);
				if (!manager || !Read(manager + kShadowMaxPerFrame, budget) || budget != 0)
					DevTools::Log("Debug menu: ExtraShadowLights=1 is ignored - the game's shadows are already set up (the DLL was loaded late).");
				else if (flag && Write(flag, on))
					DevTools::Log("Debug menu: 16 shadow-casting lights a frame (ExtraShadowLights=1).");
				else
					DevTools::Log("Debug menu: ExtraShadowLights=1 is not available on this game release.");
			}
			return;
		}
		// The calls put back as the game closes
		TurnBack();
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Lights", 72, DrawTab, "Rendering");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(40, Overlay);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachHooks);
	const bool g_namesAdded = (Register(), true);
}

int RENDER_COMMON::LightsLeftOut()
{
	int count = 0;
	for (const std::atomic<uintptr_t>& muted : g_muted)
		count += muted.load() != 0 ? 1 : 0;
	return count + (g_solo != 0 ? 1 : 0);
}
