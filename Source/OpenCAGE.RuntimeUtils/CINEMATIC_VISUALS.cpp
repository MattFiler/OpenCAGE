#include "CINEMATIC_VISUALS.h"
#include "CINEMATIC_CAMERA.h"
#include "CUBEMAP_CAPTURE.h"
#include "Config.h"
#include "DEBUG_MENU.h"

#include <detours.h>

#include <algorithm>
#include <atomic>
#include <cctype>
#include <clocale>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <limits>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Layouts and addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The post-processing object the per-frame update runs on keeps the settings the frame is drawn with: the colour stage
	(brightness, contrast, saturation, tint), the bloom stage (its frame scale and offset - Cinematic Tools' "brightness" and
	"gamma" - and the bloom itself), motion blur, film grain, vignette, lens distortion, sharpness, full-screen blur, the
	depth of field's focal parameters and chromatic aberration. The update sets each to the game's default, then blends in
	the level's active post-processing entities, every frame. The game's default depth of field has no focal length (no
	blur), a focus distance of 2.5 m and an f-number of 2.8.
*/
namespace
{
	// In the post-processing object: its settings for this frame (see above), the game's own depth of field switch (the
	// graphics settings'), and the day tone mapper (a pointer) with its settings at these offsets
	constexpr uint32_t kDepthOfFieldEnabled = 0x1B37;
	constexpr uint32_t kDofFocalLength = 0x1A40;
	constexpr uint32_t kDofFocus = 0x1A44;
	constexpr uint32_t kDofFNumber = 0x1A48;
	constexpr uint32_t kDayToneMapper = 0x380;

	enum class Where { PostProcessing, ToneMapper };

	struct Field
	{
		const char* name;     // after "cinematic." (its value) and "cinematic.override." (its switch)
		const char* label;
		const char* group;
		Where where;
		uint32_t offsets[3];  // where it is written (the same value to each; the first is the one read)
		int count;
		float min, max;
		const char* tooltip;
	};

	const Field kFields[] = {
		{ "brightness", "Brightness", "Colour", Where::PostProcessing, { 0x1914 }, 1, -1.0f, 1.0f, "The colour stage's brightness, added (the game's default 0)." },
		{ "contrast", "Contrast", "Colour", Where::PostProcessing, { 0x1918 }, 1, 0.0f, 4.0f, "Cinematic Tools' contrast (the game's default 1)." },
		{ "saturation", "Saturation", "Colour", Where::PostProcessing, { 0x191C }, 1, 0.0f, 4.0f, "Cinematic Tools' saturation (the game's default 1)." },
		{ "tint_red", "Tint red", "Colour", Where::PostProcessing, { 0x1920 }, 1, 0.0f, 4.0f, "Cinematic Tools' colour tint, red (the game's default 1)." },
		{ "tint_green", "Tint green", "Colour", Where::PostProcessing, { 0x1924 }, 1, 0.0f, 4.0f, "Cinematic Tools' colour tint, green." },
		{ "tint_blue", "Tint blue", "Colour", Where::PostProcessing, { 0x1928 }, 1, 0.0f, 4.0f, "Cinematic Tools' colour tint, blue." },
		{ "frame_scale", "Frame scale", "Exposure and bloom", Where::PostProcessing, { 0x1934 }, 1, 0.0f, 8.0f, "The frame's light multiplied by this before bloom: Cinematic Tools' \"brightness\" (the game's default 1). The game's fades to and from black go through it: none shows while it is overridden." },
		{ "frame_offset", "Frame offset", "Exposure and bloom", Where::PostProcessing, { 0x1938 }, 1, -1.0f, 1.0f, "Added to the frame's light before bloom: Cinematic Tools' \"gamma\" (the game's default 0). The game's fades go through it too: none shows while it is overridden." },
		{ "bloom_scale", "Bloom", "Exposure and bloom", Where::PostProcessing, { 0x193C }, 1, 0.0f, 8.0f, "How much bloom is added (the game's default 1)." },
		{ "bloom_exponent", "Bloom gather exponent", "Exposure and bloom", Where::PostProcessing, { 0x1940 }, 1, 0.0f, 16.0f, "How sharply bloom picks out the brightest parts (the game's default 1)." },
		{ "bloom_gather", "Bloom gather scale", "Exposure and bloom", Where::PostProcessing, { 0x1944 }, 1, 0.0f, 1.0f, "How much light goes into the bloom (the game's default 0.04)." },
		{ "motion_blur", "Motion blur", "Effects", Where::PostProcessing, { 0x1988 }, 1, 0.0f, 4.0f, "How much motion blur (0: none)." },
		{ "film_grain", "Film grain", "Effects", Where::PostProcessing, { 0x19B0, 0x19B4, 0x19B8 }, 3, 0.0f, 4.0f, "The film grain's strength in the dark, middle and bright parts, all three set to this (0: none)." },
		{ "vignette", "Vignette", "Effects", Where::PostProcessing, { 0x19E8 }, 1, 0.0f, 4.0f, "How dark the corners go (0: none)." },
		{ "vignette_aberration", "Vignette colour fringe", "Effects", Where::PostProcessing, { 0x19EC }, 1, 0.0f, 4.0f, "The colour fringing towards the corners (0: none)." },
		{ "lens_distortion", "Lens distortion", "Effects", Where::PostProcessing, { 0x19F8 }, 1, -2.0f, 2.0f, "The lens's barrel distortion (0: none)." },
		{ "sharpness", "Sharpness", "Effects", Where::PostProcessing, { 0x1A0C }, 1, 0.0f, 4.0f, "The local contrast sharpening (0: none)." },
		{ "full_screen_blur", "Full-screen blur", "Effects", Where::PostProcessing, { 0x1A34 }, 1, 0.0f, 1.0f, "Blurs the whole frame (0: none)." },
		{ "chromatic_aberration", "Chromatic aberration", "Effects", Where::PostProcessing, { 0x1A60 }, 1, 0.0f, 4.0f, "The colour fringing of the lens (0: none)." },
		{ "tone_black_point", "Black point", "Tone mapping", Where::ToneMapper, { 0x0C }, 1, 0.0f, 1.0f, "Cinematic Tools' tone mapping black point." },
		{ "tone_cross_over", "Cross-over point", "Tone mapping", Where::ToneMapper, { 0x10 }, 1, 0.0f, 16.0f, "Cinematic Tools' tone mapping cross-over point." },
		{ "tone_white_point", "White point", "Tone mapping", Where::ToneMapper, { 0x14 }, 1, 0.0f, 64.0f, "Cinematic Tools' tone mapping white point." },
		{ "tone_shoulder", "Shoulder strength", "Tone mapping", Where::ToneMapper, { 0x18 }, 1, 0.0f, 4.0f, "Cinematic Tools' tone mapping shoulder strength." },
		{ "tone_toe", "Toe strength", "Tone mapping", Where::ToneMapper, { 0x1C }, 1, 0.0f, 4.0f, "Cinematic Tools' tone mapping toe strength." },
		{ "tone_luminance", "Luminance scale", "Tone mapping", Where::ToneMapper, { 0x20 }, 1, 0.0f, 16.0f, "Cinematic Tools' tone mapping luminance scale." },
	};
	constexpr int kFieldCount = static_cast<int>(sizeof(kFields) / sizeof(kFields[0]));

	// Each field's switch and value (named), and the level's own value as last read (for the tab)
	std::atomic<bool> g_override[kFieldCount] = {};
	std::atomic<float> g_value[kFieldCount] = {};
	std::atomic<float> g_level[kFieldCount] = {};
	std::atomic<bool> g_levelKnown[kFieldCount] = {};

	// The post-processing update's own (its thread only): whether each override was on at its last frame, the value it
	// last put in the override's value while the override was off (the value follows the level's then, so the tab shows
	// it and turning the override on starts from it), whether a value was set from outside since (kept for when it is
	// turned on), what it last wrote, and the level's own value (for the tone mapper, which the level only sets now and
	// then)
	bool g_wasOn[kFieldCount] = {};
	float g_seeded[kFieldCount];
	bool g_explicit[kFieldCount] = {};
	float g_written[kFieldCount];
	float g_levelOwn[kFieldCount];
	bool g_haveLevelOwn[kFieldCount] = {};
	// "Back to the level's values" asked for: values set while an override was off are let go of too
	std::atomic<bool> g_forgetValues = false;

	// The depth of field (one switch for the three), its values followed the same way
	std::atomic<bool> g_dof = false;
	std::atomic<float> g_dofFocal = 0.05f, g_dofFocus = 5.0f, g_dofFNumber = 2.8f;
	std::atomic<float> g_levelDof[3] = {};
	std::atomic<bool> g_gameDof = true;      // the game's own depth of field switch
	std::atomic<bool> g_postProcessSeen = false;
	std::atomic<ULONGLONG> g_postProcessAt = 0;
	bool g_dofWasOn = false;
	float g_dofSeeded[3] = { 0.05f, 5.0f, 2.8f };
	bool g_dofExplicit = false;

	// ---- Guarded reads and writes of the game ----

	bool ReadFloat(const uint8_t* at, float& value)
	{
		__try
		{
			value = *reinterpret_cast<const volatile float*>(at);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool WriteFloat(uint8_t* at, float value)
	{
		__try
		{
			*reinterpret_cast<volatile float*>(at) = value;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadByte(const uint8_t* at, uint8_t& value)
	{
		__try
		{
			value = *reinterpret_cast<const volatile uint8_t*>(at);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	uint8_t* ReadPointer(const uint8_t* at)
	{
		__try
		{
			return *reinterpret_cast<uint8_t* const volatile*>(at);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	bool Same(float a, float b)
	{
		return a == b || (std::isnan(a) && std::isnan(b));
	}

	// While an override is off its value follows the level's: the level's goes in only while the value still holds what
	// was last put in, so a value set from outside in between is never written over. False when one was set.
	bool Follow(std::atomic<float>& value, float& seeded, float level)
	{
		float expected = seeded;
		if (!value.compare_exchange_strong(expected, level))
			return false;
		seeded = level;
		return true;
	}

	void InitialiseState()
	{
		static bool done = false;
		if (done)
			return;
		done = true;
		for (int i = 0; i < kFieldCount; i++)
		{
			g_written[i] = std::numeric_limits<float>::quiet_NaN();
			g_levelOwn[i] = 0.0f;
		}
	}
}

void CINEMATIC_VISUALS::OnPostProcess(uint8_t* postProcessing)
{
	// Part of the debug menu: nothing without it (the hook is in for cubemap captures too)
	if (!Config::Get().debugMenu)
		return;
	InitialiseState();
	g_postProcessSeen = true;
	g_postProcessAt = GetTickCount64();
	uint8_t enabled = 1;
	if (ReadByte(postProcessing + kDepthOfFieldEnabled, enabled))
		g_gameDof = enabled != 0;
	uint8_t* toneMapper = ReadPointer(postProcessing + kDayToneMapper);
	const bool forget = g_forgetValues.exchange(false);

	for (int i = 0; i < kFieldCount; i++)
	{
		const Field& field = kFields[i];
		// "Back to the level's values": the value follows the level's again from here (even when it can't be read this
		// frame, so the request isn't lost)
		if (forget)
		{
			g_explicit[i] = false;
			g_seeded[i] = g_value[i];
		}
		uint8_t* base = field.where == Where::PostProcessing ? postProcessing : toneMapper;
		if (!base)
			continue;
		float current;
		if (!ReadFloat(base + field.offsets[0], current))
			continue;
		const bool on = g_override[i];
		// The level's own value: the post-processing's is worked out afresh every frame before this; the tone mapper's
		// stays as the level last set it, so a value other than the one written here is the level's
		if (field.where == Where::PostProcessing || !g_wasOn[i] || !Same(current, g_written[i]))
		{
			g_levelOwn[i] = current;
			g_haveLevelOwn[i] = true;
		}
		g_level[i] = g_levelOwn[i];
		g_levelKnown[i] = true;

		// Turned off: the value follows the level's again from here
		if (!on && g_wasOn[i])
		{
			g_explicit[i] = false;
			g_seeded[i] = g_value[i];
		}
		// While it is off, and as it is turned on, the value is the level's of this frame - unless a value was set from
		// outside meanwhile, which is kept for when it is turned on
		const float level = (std::max)(field.min, (std::min)(field.max, g_levelOwn[i]));
		if ((!on || !g_wasOn[i]) && !g_explicit[i] && !Follow(g_value[i], g_seeded[i], level))
			g_explicit[i] = true;
		if (on)
		{
			const float value = g_value[i];
			for (int o = 0; o < field.count; o++)
				WriteFloat(base + field.offsets[o], value);
			g_written[i] = value;
		}
		else if (g_wasOn[i])
		{
			// Turned off: the tone mapper gets the level's own value back (the post-processing works its out next frame)
			if (field.where == Where::ToneMapper && g_haveLevelOwn[i] && Same(current, g_written[i]))
				for (int o = 0; o < field.count; o++)
					WriteFloat(base + field.offsets[o], g_levelOwn[i]);
		}
		g_wasOn[i] = on;
	}

	// The depth of field, its three together
	std::atomic<float>* const dof[3] = { &g_dofFocal, &g_dofFocus, &g_dofFNumber };
	if (forget)
	{
		g_dofExplicit = false;
		for (int i = 0; i < 3; i++)
			g_dofSeeded[i] = dof[i]->load();
	}
	float level[3] = {};
	const uint32_t dofOffsets[3] = { kDofFocalLength, kDofFocus, kDofFNumber };
	bool read = true;
	for (int i = 0; i < 3; i++)
		read &= ReadFloat(postProcessing + dofOffsets[i], level[i]);
	if (!read)
		return;
	for (int i = 0; i < 3; i++)
		g_levelDof[i] = level[i];
	const bool dofOn = g_dof;
	if (!dofOn && g_dofWasOn)
	{
		g_dofExplicit = false;
		for (int i = 0; i < 3; i++)
			g_dofSeeded[i] = dof[i]->load();
	}
	if ((!dofOn || !g_dofWasOn) && !g_dofExplicit)
	{
		// Off, and as it is turned on: the level's focus (a level with no depth of field has no focal length: a 50 mm
		// lens then), the three together - one set from outside (a track, a profile, a command) keeps all three
		const float seed[3] = { level[0] > 0.0f ? level[0] : 0.05f, level[1] > 0.0f ? level[1] : 5.0f, level[2] > 0.0f ? level[2] : 2.8f };
		bool followed = true;
		for (int i = 0; i < 3; i++)
			followed &= Same(dof[i]->load(), g_dofSeeded[i]);
		for (int i = 0; i < 3 && followed; i++)
			followed &= Follow(*dof[i], g_dofSeeded[i], seed[i]);
		if (!followed)
			g_dofExplicit = true;
	}
	if (dofOn)
	{
		WriteFloat(postProcessing + kDofFocalLength, g_dofFocal);
		WriteFloat(postProcessing + kDofFocus, g_dofFocus);
		WriteFloat(postProcessing + kDofFNumber, g_dofFNumber);
	}
	g_dofWasOn = dofOn;
}

bool CINEMATIC_VISUALS::DofOn()
{
	return g_dof;
}

CINEMATIC_VISUALS::Dof CINEMATIC_VISUALS::GetDof()
{
	Dof dof;
	dof.focalLength = g_dofFocal;
	dof.focus = g_dofFocus;
	dof.fnumber = g_dofFNumber;
	return dof;
}

void CINEMATIC_VISUALS::SetDof(const Dof& dof)
{
	if (std::isfinite(dof.focalLength))
		g_dofFocal = (std::max)(0.0f, (std::min)(1.0f, dof.focalLength));
	if (std::isfinite(dof.focus))
		g_dofFocus = (std::max)(0.01f, (std::min)(1000.0f, dof.focus));
	if (std::isfinite(dof.fnumber))
		g_dofFNumber = (std::max)(0.01f, (std::min)(64.0f, dof.fnumber));
}

namespace
{
	// ---- The HUD: the game's flash interface layers ----

	// The layer manager (a pointer to it): a lock at +0x4 (taken while its list is walked), its list of layers (a pointer
	// to their pointers) and how many. A layer's name (a pointer to text), and the flag that has the layer skipped (not
	// drawn or updated) while its resources are about to be let go of - set for as long as a layer is to be hidden.
	constexpr uintptr_t kLayerManager = 0x0134A78C;
	constexpr uint32_t kManagerLock = 0x04;
	constexpr uint32_t kManagerLayers = 0x38;
	constexpr uint32_t kManagerLayerCount = 0x3C;
	constexpr uint32_t kLayerName = 0x34;
	constexpr uint32_t kLayerSkipped = 0x29;
	constexpr int kMaxLayers = 96;
	constexpr int kNameLength = 48;

	// What "Hide the HUD" hides: the in-game HUD and the weapon HUD, as Cinematic Tools' key did, and in a challenge map the
	// challenge mode's overlay (its objectives, score and timer: a layer of its own, made by the map's scripts) while
	// cinematic.hide_hud_challenge is on
	struct HudLayer
	{
		const char* name;
		const char* what;    // for the HUD tab
		bool challenge;      // only while cinematic.hide_hud_challenge is on
	};
	const HudLayer kHudLayers[] = {
		{ "ingameui", "the in-game HUD", false },
		{ "weaponstuff", "the weapon HUD", false },
		{ "ChallengeMapOverlay", "the challenge mode's objectives, score and timer", true },
	};

	struct LayerSeen
	{
		uint8_t* layer;
		char name[kNameLength];
		uint8_t skipped;
	};

	struct LayerWrite
	{
		uint8_t* layer;
		uint8_t value;
	};

	bool TryLock(CRITICAL_SECTION* lock)
	{
		__try
		{
			return TryEnterCriticalSection(lock) != FALSE;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void Unlock(CRITICAL_SECTION* lock)
	{
		__try
		{
			LeaveCriticalSection(lock);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	// Under the layer manager's lock: its layers (pointer, name, flag), and then the writes asked for, each only to a
	// layer still in its list. The count read, or -1 when it can't be.
	int WalkLayers(uint8_t* manager, LayerSeen* seen, int max, const LayerWrite* writes, int writeCount)
	{
		__try
		{
			uint8_t* const* layers = *reinterpret_cast<uint8_t* const* const*>(manager + kManagerLayers);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + kManagerLayerCount);
			if (!layers || count > 4096)
				return -1;
			for (int w = 0; w < writeCount; w++)
				for (uint32_t i = 0; i < count; i++)
					if (layers[i] == writes[w].layer)
					{
						writes[w].layer[kLayerSkipped] = writes[w].value;
						break;
					}
			int found = 0;
			for (uint32_t i = 0; i < count && found < max; i++)
			{
				uint8_t* layer = layers[i];
				if (!layer)
					continue;
				LayerSeen& entry = seen[found++];
				entry.layer = layer;
				entry.skipped = layer[kLayerSkipped];
				const char* name = *reinterpret_cast<const char* const*>(layer + kLayerName);
				int c = 0;
				for (; name && c + 1 < kNameLength && name[c]; c++)
					entry.name[c] = name[c];
				entry.name[c] = '\0';
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	int Layers(LayerSeen* seen, int max, const LayerWrite* writes, int writeCount)
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kLayerManager);
		uint8_t* manager = at ? ReadPointer(reinterpret_cast<const uint8_t*>(at)) : nullptr;
		if (!manager)
			return -1;
		CRITICAL_SECTION* lock = reinterpret_cast<CRITICAL_SECTION*>(manager + kManagerLock);
		if (!TryLock(lock))
			return -1;
		const int found = WalkLayers(manager, seen, max, writes, writeCount);
		Unlock(lock);
		return found;
	}

	std::atomic<bool> g_hideHud = false;     // cinematic.hide_hud
	std::atomic<bool> g_hideHudChallenge = true; // cinematic.hide_hud_challenge
	std::atomic<int> g_hudHolds = 0;         // screenshots holding the HUD hidden
	std::atomic<bool> g_hudHidden = false;   // as of the last frame
	std::atomic<ULONGLONG> g_layersWantedAt = 0; // when the HUD tab or a command last wanted the list of layers
	ULONGLONG g_layersScannedAt = 0;         // when the layers were last read (render thread)

	// A switch for each layer seen, by name ("cinematic.ui.<name>"), from a pool (named once, kept across levels)
	struct LayerSwitch
	{
		std::string name;
		std::atomic<bool> hide = false;
	};
	LayerSwitch g_layerSwitches[kMaxLayers];
	int g_layerSwitchCount = 0;              // render thread
	std::mutex g_layerMutex;                 // the names, and what the tab shows
	struct LayerShown
	{
		std::string name;
		bool hidden;    // skipped now
		bool ours;      // hidden by this
	};
	std::vector<LayerShown> g_layersShown;

	// The layers hidden here (render thread only), to give back
	struct Hidden
	{
		uint8_t* layer;
		std::string name;
	};
	std::vector<Hidden> g_hiddenLayers;

	std::string SwitchName(const char* layerName)
	{
		std::string name = "cinematic.ui.";
		for (const char* c = layerName; *c; c++)
			name += isalnum(static_cast<unsigned char>(*c)) ? static_cast<char>(tolower(static_cast<unsigned char>(*c))) : '_';
		return name;
	}

	LayerSwitch* SwitchFor(const char* layerName)
	{
		for (int i = 0; i < g_layerSwitchCount; i++)
			if (g_layerSwitches[i].name == layerName)
				return &g_layerSwitches[i];
		if (g_layerSwitchCount >= kMaxLayers || !layerName[0])
			return nullptr;
		LayerSwitch& entry = g_layerSwitches[g_layerSwitchCount++];
		{
			std::lock_guard<std::mutex> lock(g_layerMutex);
			entry.name = layerName;
		}
		DEBUG_MENU::AddSwitch(SwitchName(layerName), &entry.hide);
		return &entry;
	}

	const HudLayer* FindHudLayer(const char* name)
	{
		for (const HudLayer& hud : kHudLayers)
			if (strcmp(name, hud.name) == 0)
				return &hud;
		return nullptr;
	}

	// Whether "Hide the HUD" hides the layer (as things are set now)
	bool IsHudLayer(const char* name)
	{
		const HudLayer* hud = FindHudLayer(name);
		return hud && (!hud->challenge || g_hideHudChallenge);
	}

	// Render thread, every frame: the layers' flags brought in line with what is wanted
	void ApplyLayers()
	{
		// With nothing hidden here or asked to be, and nothing showing the list, the layers are read twice a second rather
		// than every frame (a new layer still gets its switch soon after it appears)
		const ULONGLONG now = GetTickCount64();
		bool anySwitch = false;
		for (int i = 0; i < g_layerSwitchCount && !anySwitch; i++)
			anySwitch = g_layerSwitches[i].hide;
		const bool idle = !g_hideHud && g_hudHolds == 0 && g_hiddenLayers.empty() && !anySwitch && now - g_layersWantedAt.load() > 500;
		if (idle && now - g_layersScannedAt < 500)
		{
			g_hudHidden = false;
			return;
		}
		g_layersScannedAt = now;
		static LayerSeen seen[kMaxLayers];
		const int count = Layers(seen, kMaxLayers, nullptr, 0);
		if (count < 0)
			return;
		const bool hud = g_hideHud || g_hudHolds > 0;
		std::vector<LayerWrite> writes;
		std::vector<Hidden> kept;
		std::vector<LayerShown> shown;
		bool hudHidden = hud;
		for (int i = 0; i < count; i++)
		{
			const LayerSeen& layer = seen[i];
			LayerSwitch* own = SwitchFor(layer.name);
			const bool wanted = (hud && IsHudLayer(layer.name)) || (own && own->hide);
			auto ours = std::find_if(g_hiddenLayers.begin(), g_hiddenLayers.end(), [&](const Hidden& h) { return h.layer == layer.layer; });
			const bool isOurs = ours != g_hiddenLayers.end() && ours->name == layer.name;
			bool hiddenNow = layer.skipped != 0;
			if (wanted)
			{
				if (isOurs)
				{
					// Kept hidden (the game clears the flag when it loads a layer's resources again)
					if (!layer.skipped)
						writes.push_back({ layer.layer, 1 });
					kept.push_back({ layer.layer, layer.name });
					hiddenNow = true;
				}
				else if (!layer.skipped)
				{
					writes.push_back({ layer.layer, 1 });
					kept.push_back({ layer.layer, layer.name });
					hiddenNow = true;
				}
				// (already skipped by the game or a cubemap capture: left to them)
			}
			else if (isOurs && layer.skipped)
			{
				// Never shown again while a cubemap capture runs: kept as ours, and the capture gives it the flag it had
				// when it ends, so it is shown here after that
				if (CUBEMAP_CAPTURE::Capturing())
					kept.push_back({ layer.layer, layer.name });
				else
				{
					writes.push_back({ layer.layer, 0 });
					hiddenNow = false;
				}
			}
			if (hud && IsHudLayer(layer.name) && !hiddenNow)
				hudHidden = false;
			shown.push_back({ layer.name, hiddenNow, wanted && hiddenNow });
		}
		if (!writes.empty())
			Layers(seen, kMaxLayers, writes.data(), static_cast<int>(writes.size()));
		// Those no longer in the list are gone (a level ending): forgotten
		g_hiddenLayers.swap(kept);
		g_hudHidden = hud && hudHidden;
		std::lock_guard<std::mutex> lock(g_layerMutex);
		g_layersShown.swap(shown);
	}

	void ApplyOverlay(ImDrawList* /*drawList*/)
	{
		ApplyLayers();
	}
}

void CINEMATIC_VISUALS::HoldHudHidden(bool held)
{
	if (held)
		g_hudHolds++;
	else if (g_hudHolds > 0)
		g_hudHolds--;
}

bool CINEMATIC_VISUALS::HudHidden()
{
	return g_hudHidden;
}

namespace
{
	// ---- Names, hooks and the tabs ----

	std::string Number(float value, int decimals = 4)
	{
		static const _locale_t invariant = _create_locale(LC_NUMERIC, "C");
		char text[48];
		_snprintf_s_l(text, sizeof(text), _TRUNCATE, "%.*f", invariant, decimals, value);
		return text;
	}

	void ResetAll()
	{
		for (int i = 0; i < kFieldCount; i++)
			g_override[i] = false;
		g_dof = false;
		g_forgetValues = true;
	}

	void Name()
	{
		for (int i = 0; i < kFieldCount; i++)
		{
			const Field& field = kFields[i];
			// (the level's value from the first frame drawn)
			g_value[i] = field.min > 0.0f ? field.min : field.max < 0.0f ? field.max : 0.0f;
			g_seeded[i] = g_value[i];
			DEBUG_MENU::AddSwitch(std::string("cinematic.override.") + field.name, &g_override[i]);
			DEBUG_MENU::AddValue(std::string("cinematic.") + field.name, &g_value[i], field.min, field.max);
		}
		DEBUG_MENU::AddSwitch("cinematic.dof", &g_dof);
		DEBUG_MENU::AddValue("cinematic.dof_focal_length", &g_dofFocal, 0.0f, 1.0f);
		DEBUG_MENU::AddValue("cinematic.dof_focus", &g_dofFocus, 0.01f, 1000.0f);
		DEBUG_MENU::AddValue("cinematic.dof_fnumber", &g_dofFNumber, 0.01f, 64.0f);
		DEBUG_MENU::AddSwitch("cinematic.hide_hud", &g_hideHud);
		DEBUG_MENU::AddSwitch("cinematic.hide_hud_challenge", &g_hideHudChallenge);

		DEBUG_MENU::AddCommand("cinematic.visuals_reset", "turns every cinematic look override off (the level's own values again), the depth of field's too, and lets go of values set while one was off",[](const std::string&)
		{
			ResetAll();
			return DEBUG_MENU::CommandResult{ true, "the level's own look" };
		});
		DEBUG_MENU::AddCommand("cinematic.list_visuals", "each look setting: name, overridden, value, the level's own value", [](const std::string&)
		{
			std::string text;
			const bool seen = g_postProcessSeen && GetTickCount64() - g_postProcessAt < 2000;
			text += std::string("post_processing_seen=") + (seen ? "1" : "0") + "\ngame_depth_of_field=" + (g_gameDof ? "1" : "0") + "\n";
			text += std::string("dof on=") + (g_dof ? "1" : "0") + " focal_length=" + Number(g_dofFocal) + " focus=" + Number(g_dofFocus) + " fnumber=" + Number(g_dofFNumber) +
				" level=" + Number(g_levelDof[0]) + "," + Number(g_levelDof[1]) + "," + Number(g_levelDof[2]) + "\n";
			for (int i = 0; i < kFieldCount; i++)
				text += std::string(kFields[i].name) + " on=" + (g_override[i] ? "1" : "0") + " value=" + Number(g_value[i]) + " level=" +
					(g_levelKnown[i] ? Number(g_level[i]) : std::string("?")) + "\n";
			return DEBUG_MENU::CommandResult{ true, text };
		});
		DEBUG_MENU::AddCommand("cinematic.list_ui_layers", "the game's interface layers: name, hidden now, hidden by the cinematic switches, part of Hide the HUD", [](const std::string&)
		{
			g_layersWantedAt = GetTickCount64();
			std::lock_guard<std::mutex> lock(g_layerMutex);
			std::string text;
			for (const LayerShown& layer : g_layersShown)
				text += layer.name + " hidden=" + (layer.hidden ? "1" : "0") + " ours=" + (layer.ours ? "1" : "0") + " hud=" + (IsHudLayer(layer.name.c_str()) ? "1" : "0") +
					" switch=" + SwitchName(layer.name.c_str()) + "\n";
			return DEBUG_MENU::CommandResult{ true, text.empty() ? std::string("no layers read yet (no level running?)") : text };
		});
	}

	const bool g_named = (Name(), true);

	// The post-processing update's hook is attached by the DLL while Live Link's camera sync is on (cubemap captures
	// need it); with the debug menu on and camera sync off, here
	void AttachHooks(bool attach)
	{
		const Config::Settings& config = Config::Get();
		if ((config.liveLink && config.liveLinkCamera) || !CUBEMAP_CAPTURE::post_process_update)
			return;
		if (attach)
			DEVTOOLS_DETOURS_ATTACH(CUBEMAP_CAPTURE::post_process_update, CUBEMAP_CAPTURE::h_post_process_update);
		else
			DEVTOOLS_DETOURS_DETACH(CUBEMAP_CAPTURE::post_process_update, CUBEMAP_CAPTURE::h_post_process_update);
	}
	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);

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

	bool Checkbox(const char* label, std::atomic<bool>& value)
	{
		bool v = value;
		if (ImGui::Checkbox(label, &v))
		{
			value = v;
			return true;
		}
		return false;
	}

	void DragValue(const char* label, std::atomic<float>& value, float speed, float min, float max, const char* format)
	{
		float v = value;
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		if (ImGui::DragFloat(label, &v, speed, min, max, format, ImGuiSliderFlags_AlwaysClamp))
			value = v;
	}

	void DrawVisuals()
	{
		const bool seen = g_postProcessSeen && GetTickCount64() - g_postProcessAt < 2000;
		if (!seen)
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "The game's post-processing hasn't been seen this second (no level drawn?): nothing here applies until it is.");
			ImGui::PopTextWrapPos();
		}
		if (ImGui::Button("Back to the level's values"))
			ResetAll();
		Tooltip("Every override off: the level's own look from the next frame (Cinematic Tools' \"Reset values\"). Values set while an override was off are let go of too.");

		ImGui::SeparatorText("Depth of field");
		Checkbox("Override the level's focus", g_dof);
		Tooltip("The depth of field's focus set here instead of by the level. While this is off the values below follow the level's (a 50 mm lens when the level has none), so turned on it starts from them; values set while it is off (a command, a track, a profile) are kept for when it is turned on. The camera's numeric pad keys and a track's \"lock depth of field\" change it.");
		if (!g_gameDof)
		{
			ImGui::SameLine();
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "the game's depth of field is off (graphics settings): nothing shows");
			ImGui::PopTextWrapPos();
		}
		ImGui::BeginDisabled(!g_dof);
		DragValue("Focal length (m)", g_dofFocal, 0.001f, 0.0f, 1.0f, "%.3f");
		Tooltip("Cinematic Tools' \"DoF strength\": the longer the lens, the stronger the blur (0: none). Numeric pad 8/9.");
		DragValue("Focus distance (m)", g_dofFocus, 0.05f, 0.01f, 1000.0f, "%.2f");
		Tooltip("Where it is sharp. Numeric pad 6/5.");
		DragValue("F-number", g_dofFNumber, 0.05f, 0.01f, 64.0f, "%.2f");
		Tooltip("Cinematic Tools' \"focus scale\": the lower, the shallower the sharp part. Numeric pad 3/2.");
		ImGui::EndDisabled();
		ImGui::PushTextWrapPos(0.0f);
		if (g_levelDof[0].load() > 0.0f)
			ImGui::TextDisabled("The level's: focal length %.3f m, focus %.2f m, f-number %.2f", g_levelDof[0].load(), g_levelDof[1].load(), g_levelDof[2].load());
		else
			ImGui::TextDisabled("The level's: no focal length (no blur; the override starts from a 50 mm lens), focus %.2f m, f-number %.2f", g_levelDof[1].load(), g_levelDof[2].load());
		ImGui::PopTextWrapPos();

		const char* group = nullptr;
		for (int i = 0; i < kFieldCount; i++)
		{
			const Field& field = kFields[i];
			if (!group || strcmp(group, field.group) != 0)
			{
				group = field.group;
				ImGui::SeparatorText(group);
			}
			ImGui::PushID(i);
			Checkbox("##on", g_override[i]);
			Tooltip("Override the level's value. While this is off the value follows the level's, so turned on it starts from it; a value set while it is off (cinematic.<setting>) is kept for when it is turned on.");
			ImGui::SameLine();
			ImGui::BeginDisabled(!g_override[i]);
			const float range = field.max - field.min;
			DragValue(field.label, g_value[i], range / 400.0f, field.min, field.max, "%.3f");
			ImGui::EndDisabled();
			Tooltip(field.tooltip);
			ImGui::SameLine();
			// A frame scale of 0 is a black frame: the game's fades to black go through it (see its tooltip). Nothing here
			// writes it while its override is off, so it is the game's own
			if (g_levelKnown[i] && strcmp(field.name, "frame_scale") == 0 && g_level[i].load() <= 0.001f)
				ImGui::TextDisabled("(level %.3f: faded to black by the game)", g_level[i].load());
			else if (g_levelKnown[i])
				ImGui::TextDisabled("(level %.3f)", g_level[i].load());
			else
				ImGui::TextDisabled("(level not read yet)");
			ImGui::PopID();
		}
	}

	void DrawHud()
	{
		bool hide = g_hideHud;
		if (ImGui::Checkbox("Hide the HUD", &hide))
			g_hideHud = hide;
		Tooltip("Hides the in-game HUD and the weapon HUD (Cinematic Tools' Hide HUD), and in a challenge map its objectives, score and timer (with the box under it ticked). Home toggles it while the camera flies. Other layers below can be hidden one by one.");
		if (g_hudHidden)
		{
			ImGui::SameLine();
			ImGui::TextDisabled("(hidden)");
		}
		// A sub-option of hiding the HUD (here, and for a screenshot with the Screenshots tab's box ticked), so it is under
		// it and not greyed out: it still sets what a screenshot hides while Hide the HUD is off
		ImGui::Indent();
		bool challenge = g_hideHudChallenge;
		if (ImGui::Checkbox("Also hide the challenge mode's objectives when the HUD is hidden", &challenge))
			g_hideHudChallenge = challenge;
		Tooltip("In a challenge map its objectives, score and timer are a layer of their own (ChallengeMapOverlay): ticked, hiding the HUD (here or for a screenshot) hides it too. It does nothing by itself. While hidden it isn't updated either; it catches up when shown again.");
		ImGui::Unindent();
		ImGui::TextDisabled("The game's interface layers (tick to hide one):");
		g_layersWantedAt = GetTickCount64();
		std::vector<LayerShown> layers;
		{
			std::lock_guard<std::mutex> lock(g_layerMutex);
			layers = g_layersShown;
		}
		if (layers.empty())
			ImGui::TextDisabled("None read yet (no level running?).");
		for (size_t i = 0; i < layers.size(); i++)
		{
			const LayerShown& layer = layers[i];
			bool value = false;
			const std::string name = SwitchName(layer.name.c_str());
			DEBUG_MENU::GetSwitch(name, value);
			// By its place in the list too: two layers can have the same name
			ImGui::PushID(static_cast<int>(i));
			if (ImGui::Checkbox((layer.name + "##ui").c_str(), &value))
				DEBUG_MENU::SetSwitch(name, value);
			ImGui::PopID();
			ImGui::SameLine();
			const HudLayer* hud = FindHudLayer(layer.name.c_str());
			if (layer.ours)
				ImGui::TextDisabled("hidden here");
			else if (layer.hidden)
				ImGui::TextDisabled("not drawn (the game's own)");
			else if (hud)
				ImGui::TextDisabled("%s%s", hud->what, IsHudLayer(layer.name.c_str()) ? " - Hide the HUD hides it" : "");
			else
				ImGui::NewLine();
		}
		ImGui::TextDisabled("Menus hidden here stay hidden when they open: leave the pause menu alone.");
	}

	const bool g_visualsAdded = DEBUG_MENU::AddTab("Visuals", 92, DrawVisuals, "Cinematic");
	const bool g_hudAdded = DEBUG_MENU::AddTab("HUD", 93, DrawHud, "Cinematic");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(92, ApplyOverlay);
}
