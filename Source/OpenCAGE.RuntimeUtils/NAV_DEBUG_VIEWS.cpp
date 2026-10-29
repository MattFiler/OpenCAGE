#include "NAV_DEBUG.h"
#include "COVER_DEBUG.h"
#include "DEBUG_MENU.h"
#include "SOUND_DEBUG.h"
#include "TABLE_ORDER.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <unordered_map>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	The Navigation tab (in the World group) and the drawing over the game it switches on, from NAV_DEBUG's copy of the
	nav mesh, what its hooks noted (see NAV_DEBUG.h) and the traversals COVER_DEBUG reads. Every switch and number is a
	named one and every button and list has a command (all "nav."), so OpenCAGE can drive the tab and the main session
	can test it over the Live Link.

	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// A character's movement (a pointer), and in it: the polygon it is on, its navigation class, whether it is deep
	// crouching or backstage, whether where it is is known to be good and the last place that was
	constexpr uint32_t kCharacterLocomotion = 0x42A8;
	constexpr uint32_t kLocomotionPoly = 0x1C4;
	constexpr uint32_t kLocomotionClass = 0x1D8;
	constexpr uint32_t kLocomotionDeepCrouch = 0x1E2;
	constexpr uint32_t kLocomotionBackstage = 0x1E3;
	constexpr uint32_t kLocomotionKnownGood = 0x1E0;
	constexpr uint32_t kLocomotionLastGood = 0x174;

	enum ColourMode { kByMarkup, kByClasses, kByHeight, kByEnabled, kByBarrier, kByTraffic, kByReach, kByIsland, kColourModes };
	const char* const kColourModeNames[kColourModes] = {
		"Markup (normal, backstage, expensive)", "Classes let through", "Height (standing, crouch, deep crouch)", "Enabled",
		"Barriers (what each door lets through)", "Traffic (time since a character pathed through)", "Reachable from the flood's seed",
		"Islands (from the flood)" };

	// What the tab and the commands choose between
	struct ClassChoice { const char* name; const char* key; uint32_t navigationClass; int gameClass; };
	const ClassChoice kClasses[] = {
		{ "Player", "player", NAV_DEBUG::kClassPlayer, 0 },
		{ "Alien", "alien", NAV_DEBUG::kClassAlien, 1 },
		{ "Android", "android", NAV_DEBUG::kClassAndroid, 2 },
		{ "Human NPC", "human", NAV_DEBUG::kClassHuman, 3 },
		{ "Facehugger", "facehugger", NAV_DEBUG::kClassFacehugger, 5 },
	};
	constexpr int kClassCount = static_cast<int>(sizeof(kClasses) / sizeof(kClasses[0]));
	const char* const kPointNames[] = { "the player", "the crosshair (World > Inspect)", "the camera", "the selected character" };
	enum PointFrom { kFromPlayer, kFromPick, kFromCamera, kFromSelected, kPointFroms };

	// ---- Named switches and numbers ----
	std::atomic<bool> g_polys = false;           // nav.polys: the mesh's polygons near the camera
	std::atomic<bool> g_offMesh = false;         // nav.offmesh: off-mesh connections
	std::atomic<bool> g_barrierPolys = false;    // nav.barrier_polys: only barrier polygons, by what they let through
	std::atomic<bool> g_under = false;           // nav.under: the polygons under the player, the selected character and the crosshair
	std::atomic<bool> g_pathLog = false;         // nav.path_log: failed and partial path searches
	std::atomic<bool> g_traversals = false;      // nav.traversals: traversals (vaults, ladders, vents...)
	std::atomic<bool> g_labels = false;          // nav.labels: polygon numbers near the camera
	std::atomic<bool> g_aroundPlayer = false;    // nav.around_player: the drawing centred on the player, not the camera
	std::atomic<bool> g_failedOnly = true;       // nav.failed_only: the path log shows failed and partial searches only
	std::atomic<bool> g_testPath = true;         // nav.path_test_draw: the path tester's result drawn
	std::atomic<bool> g_floodAuto = true;        // nav.flood_auto: the flood run again when the mesh changes (doors)
	std::atomic<float> g_radius = 20.0f;         // nav.radius: how far from the centre to draw
	std::atomic<float> g_band = 3.0f;            // nav.height_band: how far above and below the centre
	std::atomic<float> g_mode = 0.0f;            // nav.colour_mode: see kColourModeNames
	std::atomic<float> g_maxPolys = 3000.0f;     // nav.max_polys
	std::atomic<float> g_fill = 0.35f;           // nav.fill: how solid the polygons are drawn

	// ---- What the tab holds (render thread) ----
	int g_highlight = -1;                        // a polygon chosen to show (the decode box, nav.highlight)
	uintptr_t g_highlightDetour = 0;             // the mesh it was chosen on
	char g_decodeText[24] = "";
	NAV_DEBUG::Flood g_flood;
	int g_floodClass = 1;
	bool g_floodBackstage = true, g_floodDeep = true;
	int g_floodFrom = kFromPlayer;
	bool g_floodCustom = false;                  // the filter of a logged search, not a class's
	NAV_DEBUG::Filter g_floodFilter;
	int g_testClass = 1;
	bool g_testBackstage = false, g_testDeep = false;
	int g_testFrom = kFromPlayer, g_testTo = kFromPick;
	int g_pathKind = -1;                         // the path log's kind shown (-1 all)
	uint32_t g_selectedPath = 0;
	int g_drawnPolys = 0;
	char g_offMeshFilter[32] = "";

	struct Logs
	{
		int frame = -1;
		std::vector<NAV_DEBUG::LoadRecord> loads;
		std::vector<NAV_DEBUG::BarrierRecord> barriers;
		std::vector<NAV_DEBUG::PathRecord> paths;
		NAV_DEBUG::PathCounts counts;
	} g_logs;

	// Chooses a polygon to show (-1: none), on the mesh loaded now
	void Choose(int poly)
	{
		g_highlight = poly;
		g_highlightDetour = NAV_DEBUG::Read().detour;
	}

	const Logs& CopyLogs()
	{
		const int frame = ImGui::GetFrameCount();
		if (g_logs.frame != frame)
		{
			g_logs.frame = frame;
			NAV_DEBUG::Hooked(g_logs.loads, g_logs.barriers, g_logs.paths, g_logs.counts);
		}
		return g_logs;
	}

	void SwitchBox(const char* label, std::atomic<bool>& on)
	{
		bool value = on;
		if (ImGui::Checkbox(label, &value))
			on = value;
	}

	void SliderValue(const char* label, std::atomic<float>& value, float min, float max, const char* format)
	{
		float now = value;
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat(label, &now, min, max, format))
			value = now;
	}

	std::string Format(const char* format, ...)
	{
		char text[2048];
		va_list args;
		va_start(args, format);
		vsnprintf(text, sizeof(text), format, args);
		va_end(args);
		return text;
	}

	float Distance(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dy * dy + dz * dz);
	}

	// How far a point is from a box seen from above (0 inside)
	float DistanceXZ(const float point[3], const float min[3], const float max[3])
	{
		const float dx = (std::max)((std::max)(min[0] - point[0], 0.0f), point[0] - max[0]);
		const float dz = (std::max)((std::max)(min[2] - point[2], 0.0f), point[2] - max[2]);
		return std::sqrt(dx * dx + dz * dz);
	}

	ImU32 Rgb(int r, int g, int b)
	{
		return IM_COL32(r, g, b, 255);
	}

	ImU32 WithAlpha(ImU32 colour, float alpha)
	{
		const int a = static_cast<int>((std::max)(0.0f, (std::min)(1.0f, alpha)) * 255.0f);
		return (colour & ~IM_COL32_A_MASK) | (static_cast<ImU32>(a) << IM_COL32_A_SHIFT);
	}

	ImU32 Hue(float hue)
	{
		float r, g, b;
		ImGui::ColorConvertHSVtoRGB(hue - std::floor(hue), 0.75f, 0.95f, r, g, b);
		return IM_COL32(static_cast<int>(r * 255), static_cast<int>(g * 255), static_cast<int>(b * 255), 255);
	}

	ImVec4 Colour4(ImU32 colour)
	{
		return ImGui::ColorConvertU32ToFloat4(colour);
	}

	// Notes and hints, greyed or coloured, wrapped to the window rather than running off its edge
	void TextDisabledWrapped(const char* format, ...)
	{
		va_list args;
		va_start(args, format);
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabledV(format, args);
		ImGui::PopTextWrapPos();
		va_end(args);
	}

	void TextColouredWrapped(const ImVec4& colour, const char* format, ...)
	{
		va_list args;
		va_start(args, format);
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextColoredV(colour, format, args);
		ImGui::PopTextWrapPos();
		va_end(args);
	}

	const DEBUG_MENU::Character* Player()
	{
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.player)
				return &character;
		return nullptr;
	}

	const DEBUG_MENU::Character* CharacterByObject(const void* object)
	{
		if (!object)
			return nullptr;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.object == object)
				return &character;
		return nullptr;
	}

	std::string CharacterLabel(const DEBUG_MENU::Character* character)
	{
		return character ? character->name + " (" + DEBUG_MENU::ClassName(character->characterClass) + ")" : std::string("?");
	}

	const DEBUG_MENU::Character* Nearest(const float point[3], float* distance = nullptr)
	{
		const DEBUG_MENU::Character* best = nullptr;
		float bestDistance = FLT_MAX;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		{
			if (character.dead)
				continue;
			const float d = Distance(point, character.position);
			if (d < bestDistance)
			{
				bestDistance = d;
				best = &character;
			}
		}
		if (distance)
			*distance = bestDistance;
		return best;
	}

	// A point to start or end at: the player, the crosshair, the camera or the selected character; false with why not
	bool PointFrom(int from, float out[3], std::string& why)
	{
		switch (from)
		{
		case kFromPlayer:
		{
			const DEBUG_MENU::Character* player = Player();
			if (!player)
			{
				why = "no player";
				return false;
			}
			memcpy(out, player->position, sizeof(float) * 3);
			return true;
		}
		case kFromPick:
		{
			DEBUG_MENU::PickResult pick;
			if (!DEBUG_MENU::Pick(pick))
			{
				why = "nothing under the crosshair: turn on the crosshair in World > Inspect, or the game is paused";
				return false;
			}
			memcpy(out, pick.position, sizeof(float) * 3);
			return true;
		}
		case kFromCamera:
			if (!DEBUG_MENU::CameraPosition(out))
			{
				why = "no camera";
				return false;
			}
			return true;
		case kFromSelected:
		{
			const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
			if (!selected)
			{
				why = "no character selected (Characters tab)";
				return false;
			}
			memcpy(out, selected->position, sizeof(float) * 3);
			return true;
		}
		default:
			why = "?";
			return false;
		}
	}

	// Where the drawing is centred: the camera, or the player
	bool OverlayCentre(float out[3])
	{
		if (g_aroundPlayer)
			if (const DEBUG_MENU::Character* player = Player())
			{
				memcpy(out, player->position, sizeof(float) * 3);
				return true;
			}
		return DEBUG_MENU::CameraPosition(out);
	}

	// A character's movement's view of the mesh
	struct ActorNav
	{
		bool have = false;
		uint32_t poly = 0;
		int navigationClass = -1;
		bool deepCrouch = false, backstage = false, knownGood = false;
		float lastGood[3] = {};
	};

	bool ReadActorNav(const void* character, ActorNav& out)
	{
		__try
		{
			if (!character)
				return false;
			const uint8_t* base = static_cast<const uint8_t*>(character);
			const uint8_t* actor = *reinterpret_cast<const uint8_t* const*>(base + kCharacterLocomotion);
			if (!actor)
				return false;
			out.poly = *reinterpret_cast<const uint32_t*>(actor + kLocomotionPoly);
			out.navigationClass = *reinterpret_cast<const int*>(actor + kLocomotionClass);
			out.deepCrouch = actor[kLocomotionDeepCrouch] != 0;
			out.backstage = actor[kLocomotionBackstage] != 0;
			out.knownGood = actor[kLocomotionKnownGood] != 0;
			memcpy(out.lastGood, actor + kLocomotionLastGood, sizeof(out.lastGood));
			out.have = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	const char* NavigationClassName(int navigationClass)
	{
		switch (navigationClass)
		{
		case 0: return "player";
		case 1: return "alien";
		case 2: return "android";
		case 3: return "human NPC";
		case 4: return "facehugger";
		default: return "?";
		}
	}

	// ---- Polygons ----

	void PolyPoints(const NAV_DEBUG::Mesh& mesh, const NAV_DEBUG::Poly& poly, float (*out)[3], float lift)
	{
		for (int v = 0; v < poly.vertCount; v++)
		{
			const int index = poly.verts[v];
			if (index * 3 + 2 >= static_cast<int>(mesh.verts.size()))
			{
				out[v][0] = out[v][1] = out[v][2] = 0.0f;
				continue;
			}
			out[v][0] = mesh.verts[index * 3];
			out[v][1] = mesh.verts[index * 3 + 1] + lift;
			out[v][2] = mesh.verts[index * 3 + 2];
		}
	}

	// The polygons a polygon links to (over the mesh's links)
	std::vector<int> Linked(const NAV_DEBUG::Mesh& mesh, int poly)
	{
		std::vector<int> out;
		if (poly < 0 || poly >= static_cast<int>(mesh.polys.size()))
			return out;
		uint32_t link = mesh.polys[poly].firstLink;
		for (int guard = 0; link != 0xFFFFFFFFu && link < mesh.links.size() && guard < 64; guard++)
		{
			const int other = NAV_DEBUG::PolyIndex(mesh.links[link].ref);
			if (other >= 0)
				out.push_back(other);
			link = mesh.links[link].next;
		}
		return out;
	}

	ImU32 ClassesColour(uint32_t classes)
	{
		classes &= NAV_DEBUG::kClassAll;
		if (classes == NAV_DEBUG::kClassAll)
			return Rgb(70, 210, 90);
		if (!classes)
			return Rgb(230, 60, 60);
		return Rgb((classes & NAV_DEBUG::kClassAlien) ? 230 : 70, (classes & NAV_DEBUG::kClassPlayer) ? 200 : 70, (classes & 0x1C) ? 230 : 70);
	}

	ImU32 BarrierColour(int area)
	{
		if (!area)
			return Rgb(120, 120, 120);
		const NAV_DEBUG::Barrier* barrier = NAV_DEBUG::BarrierByArea(area);
		if (!barrier)
			return Rgb(255, 0, 255);
		const uint32_t classes = barrier->classes & NAV_DEBUG::kClassAll;
		return classes == NAV_DEBUG::kClassAll ? Rgb(70, 210, 90) : !classes ? Rgb(230, 60, 60) : Rgb(255, 170, 40);
	}

	bool FloodCurrent(const NAV_DEBUG::Mesh& mesh)
	{
		return g_flood.done && g_flood.generation == mesh.generation && g_flood.reached.size() == mesh.polys.size();
	}

	ImU32 PolyColour(int mode, const NAV_DEBUG::Mesh& mesh, int index)
	{
		const uint32_t area = mesh.polys[index].area;
		switch (mode)
		{
		case kByMarkup:
		{
			const uint32_t markup = NAV_DEBUG::Markup(area);
			if (markup & NAV_DEBUG::kMarkupBackstage)
				return Rgb(180, 90, 255);
			if (markup & NAV_DEBUG::kMarkupExpensive)
				return Rgb(255, 150, 40);
			if (markup & NAV_DEBUG::kMarkupNormal)
				return Rgb(70, 150, 255);
			return Rgb(150, 150, 150);
		}
		case kByClasses:
			return ClassesColour(NAV_DEBUG::Admittance(area));
		case kByHeight:
		{
			const int height = NAV_DEBUG::HeightClass(area);
			return height == 0 ? Rgb(70, 210, 90) : height == 1 ? Rgb(240, 210, 60) : Rgb(230, 70, 60);
		}
		case kByEnabled:
			return NAV_DEBUG::Enabled(area) ? Rgb(70, 210, 90) : Rgb(230, 60, 60);
		case kByBarrier:
			return BarrierColour(NAV_DEBUG::AreaId(area));
		case kByTraffic:
		{
			uint16_t ticks = 0;
			uint8_t character = 0;
			if (!NAV_DEBUG::Freshness(index, ticks, character))
				return Rgb(150, 150, 150);
			if (ticks == 0xFFFF)
				return Rgb(60, 60, 70);
			// Red just now, through yellow and green to blue a minute or more ago
			const float t = (std::min)(static_cast<float>(ticks) / 30.0f / 60.0f, 1.0f);
			return Hue(t * 0.66f);
		}
		case kByReach:
			if (!FloodCurrent(mesh))
				return Rgb(150, 150, 150);
			return g_flood.reached[index] == 2 ? Rgb(70, 210, 90) : g_flood.reached[index] == 1 ? Rgb(230, 60, 60) : Rgb(80, 80, 80);
		case kByIsland:
		{
			if (!FloodCurrent(mesh) || index >= static_cast<int>(g_flood.island.size()))
				return Rgb(150, 150, 150);
			const int island = g_flood.island[index];
			if (island < 0)
				return Rgb(80, 80, 80);
			return island == 0 ? Rgb(70, 210, 90) : Hue(0.1f + static_cast<float>(island) * 0.618034f);
		}
		default:
			return Rgb(150, 150, 150);
		}
	}

	const char* LinkSideText(uint8_t side)
	{
		return side == 0xFF ? "inside the tile" : "on a tile edge";
	}

	ImU32 LinkTypeColour(int type)
	{
		switch (type)
		{
		case 0: return Rgb(230, 230, 230);
		case 1: return Rgb(60, 220, 230);
		case 2: return Rgb(250, 210, 60);
		case 3: return Rgb(160, 160, 160);
		case 4: return Rgb(190, 90, 255);
		default: return Rgb(255, 0, 255);
		}
	}

	ImU32 TraversalColour(int type)
	{
		return Hue(0.08f + static_cast<float>(type) * 0.11f);
	}

	// ---- Text for the tab and the commands ----

	std::string PolyText(const NAV_DEBUG::Mesh& mesh, int index)
	{
		if (index < 0 || index >= static_cast<int>(mesh.polys.size()))
			return "polygon=none\n";
		const NAV_DEBUG::Poly& poly = mesh.polys[index];
		const uint32_t area = poly.area;
		std::string text = Format("polygon=%d\nref=0x%08X\nflags=0x%08X\nkind=%s\n", index, NAV_DEBUG::PolyRef(index), area, NAV_DEBUG::IsOffMesh(area) ? "off-mesh connection" : "ground");
		const int id = NAV_DEBUG::AreaId(area);
		text += Format("area_id=%d\n", id);
		if (id)
		{
			const NAV_DEBUG::Barrier* barrier = NAV_DEBUG::BarrierByArea(id);
			text += barrier ? Format("barrier=door 0x%08X lets through %s\n", barrier->entity, NAV_DEBUG::ClassesText(barrier->classes).c_str())
				: std::string("barrier=NONE (no barrier row carries this area id)\n");
		}
		text += Format("enabled=%d\nadmits=%s\nheight=%s\nmarkup=%s\n", NAV_DEBUG::Enabled(area) ? 1 : 0, NAV_DEBUG::ClassesText(NAV_DEBUG::Admittance(area)).c_str(),
			NAV_DEBUG::HeightName(NAV_DEBUG::HeightClass(area)), NAV_DEBUG::MarkupText(NAV_DEBUG::Markup(area)).c_str());
		if (NAV_DEBUG::IsOffMesh(area))
			text += Format("link_type=%s\n", NAV_DEBUG::LinkTypeName(NAV_DEBUG::LinkType(area)));
		text += Format("centre=%.2f,%.2f,%.2f\nvertices=%d\n", poly.centre[0], poly.centre[1], poly.centre[2], poly.vertCount);
		std::string links;
		for (int other : Linked(mesh, index))
			links += (links.empty() ? "" : ",") + std::to_string(other);
		text += "links=" + (links.empty() ? std::string("(none)") : links) + "\n";
		uint16_t ticks = 0;
		uint8_t character = 0;
		if (NAV_DEBUG::Freshness(index, ticks, character))
			text += ticks == 0xFFFF ? std::string("last_pathed=never\n") : Format("last_pathed=%.1f s ago by character %u\n", ticks / 30.0f, character);
		return text;
	}

	std::string DecodeText(uint32_t ref)
	{
		const NAV_DEBUG::Decoded decoded = NAV_DEBUG::Decode(ref);
		std::string text = Format("ref=0x%08X\nsalt=%u\ntile=%u\npoly=%u\nvalid=%d\n", ref, decoded.salt, decoded.tile, decoded.poly, decoded.valid ? 1 : 0);
		if (!decoded.valid)
			text += "why=" + decoded.why + "\n";
		return text;
	}

	std::string StatusText(const NAV_DEBUG::Mesh& mesh)
	{
		std::string text = Format("level=%d\nhooks=%d\nnav_mesh_object=%d\nloaded=%d\n", mesh.level ? 1 : 0, NAV_DEBUG::HooksAttached() ? 1 : 0, mesh.haveObject ? 1 : 0, mesh.have ? 1 : 0);
		text += Format("state=%d\n", NAV_DEBUG::CurrentState());
		const Logs& logs = CopyLogs();
		if (!logs.loads.empty())
		{
			const NAV_DEBUG::LoadRecord& last = logs.loads.front();
			text += Format("last_load=state %u at %.1f s: %s, %d polygons, %d barriers put back\n", last.state, last.time, last.loaded ? "loaded" : "NO MESH", last.polys, last.barriers);
		}
		if (!mesh.have)
			return text;
		text += Format("change_counter=%u\npolygons=%d\nvertices=%d\nmax_links=%d\noff_mesh_connections=%d\noff_mesh_base=%d\n", mesh.changeCounter,
			mesh.polyCount, mesh.vertCount, mesh.maxLinkCount, mesh.offMeshConCount, mesh.offMeshBase);
		text += Format("detail_meshes=%d\ndetail_vertices=%d\ndetail_triangles=%d\nbv_nodes=%d\n", mesh.detailMeshCount, mesh.detailVertCount, mesh.detailTriCount, mesh.bvNodeCount);
		text += Format("walkable_height=%.3f\nwalkable_radius=%.3f\nwalkable_climb=%.3f\n", mesh.walkableHeight, mesh.walkableRadius, mesh.walkableClimb);
		text += Format("bmin=%.2f,%.2f,%.2f\nbmax=%.2f,%.2f,%.2f\nbv_quant=%.4f\n", mesh.bmin[0], mesh.bmin[1], mesh.bmin[2], mesh.bmax[0], mesh.bmax[1], mesh.bmax[2], mesh.bvQuantFactor);
		text += Format("magic=0x%08X\nversion=%d\ntile=%d,%d layer %d\nuser_id=%u\ndata_size=%d\n", mesh.magic, mesh.version, mesh.tileX, mesh.tileY, mesh.layer, mesh.userId, mesh.dataSize);
		text += Format("max_tiles=%d\ntile_index=%d\nsalt=%u\nsalt_bits=%u\ntile_bits=%u\npoly_bits=%u\n", mesh.maxTiles, mesh.tileIndex, mesh.tileSalt, mesh.saltBits, mesh.tileBits, mesh.polyBits);
		text += Format("barriers=%d\nlock_busy_frames=%d\ncopies=%d\n", static_cast<int>(NAV_DEBUG::Barriers().size()), mesh.lockMisses, mesh.copies);
		return text;
	}

	// ---- The door audit: nav barriers against the sound network's doorways, by id ----

	struct AuditRow
	{
		int nav = -1;                // in NAV_DEBUG::Barriers()
		int sound = -1;              // in SOUND_DEBUG::Barriers()
		uint32_t id = 0;
		const char* verdict = "";
		bool problem = false;
	};

	std::vector<AuditRow> DoorAudit()
	{
		SOUND_DEBUG::Read();
		const std::vector<NAV_DEBUG::Barrier>& nav = NAV_DEBUG::Barriers();
		const std::vector<SOUND_DEBUG::Barrier>& sound = SOUND_DEBUG::Barriers();
		std::vector<AuditRow> rows;
		std::unordered_map<uint32_t, int> soundById;
		for (size_t i = 0; i < sound.size(); i++)
			soundById.emplace(sound[i].instanceId, static_cast<int>(i));
		std::vector<bool> soundUsed(sound.size(), false);
		for (size_t i = 0; i < nav.size(); i++)
		{
			AuditRow row;
			row.nav = static_cast<int>(i);
			row.id = nav[i].entity;
			const auto found = soundById.find(nav[i].entity);
			if (found == soundById.end())
				row.verdict = "no sound doorway has this id";
			else
			{
				row.sound = found->second;
				soundUsed[found->second] = true;
				const float state = sound[found->second].state;
				const bool navOpen = (nav[i].classes & NAV_DEBUG::kClassAll) != 0;
				if (state > 0.01f && state < 0.99f)
					row.verdict = "door moving";
				else if (navOpen && state >= 0.99f)
				{
					row.verdict = "nav lets through, sound closed";
					row.problem = true;
				}
				else if (!navOpen && state <= 0.01f)
				{
					row.verdict = "nav shut, sound open";
					row.problem = true;
				}
				else
					row.verdict = navOpen ? "both open" : "both shut";
			}
			rows.push_back(row);
		}
		for (size_t i = 0; i < sound.size(); i++)
		{
			if (soundUsed[i])
				continue;
			AuditRow row;
			row.sound = static_cast<int>(i);
			row.id = sound[i].instanceId;
			row.verdict = "no nav barrier has this id";
			rows.push_back(row);
		}
		return rows;
	}

	std::string AuditLine(const AuditRow& row)
	{
		const std::vector<NAV_DEBUG::Barrier>& nav = NAV_DEBUG::Barriers();
		const std::vector<SOUND_DEBUG::Barrier>& sound = SOUND_DEBUG::Barriers();
		std::string line = Format("0x%08X ", row.id);
		if (row.nav >= 0)
			line += Format("nav(area %d, %s) ", nav[row.nav].area, NAV_DEBUG::ClassesText(nav[row.nav].classes).c_str());
		else
			line += "nav(-) ";
		if (row.sound >= 0)
			line += Format("sound(state %.2f) ", sound[row.sound].state);
		else
			line += "sound(-) ";
		return line + row.verdict;
	}

	// ---- The flood ----

	bool FloodSeed(int from, int& seed, std::string& why)
	{
		if (from == 3 && g_highlight >= 0)
		{
			seed = g_highlight;
			return true;
		}
		float point[3];
		if (!PointFrom(from == 3 ? kFromPlayer : from, point, why))
			return false;
		seed = NAV_DEBUG::PolyAt(point);
		if (seed < 0)
		{
			why = "no polygon under that point";
			return false;
		}
		return true;
	}

	NAV_DEBUG::Filter FloodFilter()
	{
		return g_floodCustom ? g_floodFilter : NAV_DEBUG::ClassFilter(kClasses[g_floodClass].navigationClass, g_floodBackstage, g_floodDeep);
	}

	std::string RunFlood()
	{
		const NAV_DEBUG::Mesh& mesh = NAV_DEBUG::Read();
		if (!mesh.have)
			return "no nav mesh";
		int seed = -1;
		std::string why;
		if (!FloodSeed(g_floodFrom, seed, why))
			seed = -1;
		NAV_DEBUG::RunFlood(g_flood, FloodFilter(), seed);
		return seed < 0 ? "flooded (no seed: " + why + ")" : "flooded";
	}

	std::string FloodText()
	{
		if (!g_flood.done)
			return "flood=none\n";
		const NAV_DEBUG::Filter& f = g_flood.filter;
		std::string text = Format("filter=classes %s, links 0x%X, height %s..%s, speed %d..%d, markup left out %s\nseed=%d\npassable=%d\nreached=%d\nislands=%d\n",
			NAV_DEBUG::ClassesText(f.include).c_str(), f.linkTypes, NAV_DEBUG::HeightName(f.heightMin), NAV_DEBUG::HeightName(f.heightMax), f.speedMin, f.speedMax,
			NAV_DEBUG::MarkupText(f.excludeMarkup).c_str(), g_flood.seed, g_flood.passableCount, g_flood.reachedCount, static_cast<int>(g_flood.islands.size()));
		for (size_t i = 0; i < g_flood.islands.size() && i < 64; i++)
		{
			const NAV_DEBUG::Flood::Island& island = g_flood.islands[i];
			text += Format("island %d: %d polygons, %.0f m2, centre %.1f,%.1f,%.1f%s\n", static_cast<int>(i), island.polys, island.area, island.centre[0], island.centre[1], island.centre[2],
				island.hasSeed ? " (the seed's)" : "");
		}
		return text;
	}

	// The flood kept current while it is shown (doors opening and closing change the mesh)
	void KeepFloodCurrent(const NAV_DEBUG::Mesh& mesh)
	{
		if (!g_flood.done || !g_floodAuto || !mesh.have)
			return;
		if (g_flood.generation != mesh.generation)
			RunFlood();
	}

	// ---- Path log text ----

	std::string PathLine(const NAV_DEBUG::PathRecord& record, float now)
	{
		float nearest = 0.0f;
		const DEBUG_MENU::Character* who = Nearest(record.originalStart, &nearest);
		return Format("#%u %.1fs ago %s %s: %.1f,%.1f,%.1f -> %.1f,%.1f,%.1f (%.1f m), %d polys, start %s, end %s, classes %s%s%s",
			record.serial, now - record.time, NAV_DEBUG::PathKindName(record.kind), NAV_DEBUG::StatusText(record.status).c_str(),
			record.originalStart[0], record.originalStart[1], record.originalStart[2], record.originalEnd[0], record.originalEnd[1], record.originalEnd[2],
			Distance(record.originalStart, record.originalEnd), record.length, NAV_DEBUG::ReasonName(record.startReason), NAV_DEBUG::ReasonName(record.endReason),
			NAV_DEBUG::ClassesText(record.filter.include).c_str(), who && nearest < 3.0f ? ", nearest " : "", who && nearest < 3.0f ? CharacterLabel(who).c_str() : "");
	}

	bool PathShown(const NAV_DEBUG::PathRecord& record)
	{
		if (g_pathKind >= 0 && record.kind != g_pathKind)
			return false;
		return !g_failedOnly || record.failed || record.partial;
	}

	// ---- Drawing over the game ----

	// Labels wait for a later overlay (after the cover's lines too), so no line drawn over the mesh crosses one
	struct QueuedLabel
	{
		float at[3];
		std::string text;
		ImU32 colour;
	};
	std::vector<QueuedLabel> g_queuedLabels;

	void QueueText(const float at[3], const char* text, ImU32 colour)
	{
		QueuedLabel label;
		memcpy(label.at, at, sizeof(label.at));
		label.text = text;
		label.colour = colour;
		g_queuedLabels.push_back(std::move(label));
	}

	void DrawQueuedLabels(ImDrawList* drawList)
	{
		for (const QueuedLabel& label : g_queuedLabels)
			DEBUG_MENU::WorldText(drawList, label.at, label.text.c_str(), label.colour);
		g_queuedLabels.clear();
	}

	void DrawPolys(ImDrawList* drawList, const NAV_DEBUG::Mesh& mesh, const float centre[3])
	{
		const float radius = g_radius, band = g_band;
		const int mode = (std::max)(0, (std::min)(static_cast<int>(g_mode), kColourModes - 1));
		const int max = static_cast<int>(g_maxPolys);
		const bool all = g_polys;
		if (all && (mode == kByReach || mode == kByIsland))
			KeepFloodCurrent(mesh);
		int drawn = 0, labels = 0;
		float points[6][3];
		for (int i = 0; i < static_cast<int>(mesh.polys.size()) && drawn < max; i++)
		{
			const NAV_DEBUG::Poly& poly = mesh.polys[i];
			if (NAV_DEBUG::IsOffMesh(poly.area) || poly.vertCount < 3)
				continue;
			if (!all && !NAV_DEBUG::AreaId(poly.area))
				continue;
			const float d = DistanceXZ(centre, poly.min, poly.max);
			if (d > radius || poly.max[1] < centre[1] - band || poly.min[1] > centre[1] + band)
				continue;
			drawn++;
			const ImU32 colour = all ? PolyColour(mode, mesh, i) : BarrierColour(NAV_DEBUG::AreaId(poly.area));
			const float fade = 1.0f - d / (std::max)(radius, 0.01f);
			PolyPoints(mesh, poly, points, 0.03f);
			DEBUG_MENU::WorldPolygon(drawList, points, poly.vertCount, WithAlpha(colour, g_fill * (0.25f + 0.75f * fade)), WithAlpha(colour, 0.25f + 0.5f * fade), 1.0f);
			if (g_labels && labels < 150 && Distance(centre, poly.centre) < 8.0f)
			{
				labels++;
				const float at[3] = { poly.centre[0], poly.centre[1] + 0.05f, poly.centre[2] };
				QueueText(at, std::to_string(i).c_str(), IM_COL32(255, 255, 255, 220));
			}
		}
		g_drawnPolys = drawn;
		// The barriers' labels
		if (!all || mode == kByBarrier)
			for (const NAV_DEBUG::Barrier& barrier : NAV_DEBUG::Barriers())
			{
				if (!barrier.polys || Distance(centre, barrier.centre) > (std::min)(radius, 25.0f))
					continue;
				const float at[3] = { barrier.centre[0], barrier.centre[1] + 0.4f, barrier.centre[2] };
				QueueText(at, Format("door 0x%08X: %s", barrier.entity, NAV_DEBUG::ClassesText(barrier.classes).c_str()).c_str(), BarrierColour(barrier.area));
			}
	}

	void DrawArc(ImDrawList* drawList, const float a[3], const float b[3], ImU32 colour, float thickness, bool arrow)
	{
		const float length = Distance(a, b);
		const float lift = (std::max)(0.4f, length * 0.2f);
		float previous[3] = { a[0], a[1], a[2] };
		const int steps = 10;
		for (int s = 1; s <= steps; s++)
		{
			const float t = static_cast<float>(s) / steps;
			const float next[3] = { a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t + 4.0f * lift * t * (1.0f - t), a[2] + (b[2] - a[2]) * t };
			if (s == steps && arrow)
				DEBUG_MENU::WorldArrow(drawList, previous, next, colour, thickness);
			else
				DEBUG_MENU::WorldLine(drawList, previous, next, colour, thickness);
			memcpy(previous, next, sizeof(previous));
		}
	}

	void DrawOffMesh(ImDrawList* drawList, const NAV_DEBUG::Mesh& mesh, const float centre[3])
	{
		const float radius = g_radius;
		int labels = 0;
		for (size_t i = 0; i < mesh.offMesh.size(); i++)
		{
			const NAV_DEBUG::OffMesh& connection = mesh.offMesh[i];
			if (Distance(centre, connection.start) > radius && Distance(centre, connection.end) > radius)
				continue;
			const uint32_t area = connection.poly >= 0 && connection.poly < static_cast<int>(mesh.polys.size()) ? mesh.polys[connection.poly].area : 0;
			const bool enabled = NAV_DEBUG::Enabled(area);
			const ImU32 colour = enabled ? LinkTypeColour(NAV_DEBUG::LinkType(area)) : Rgb(230, 60, 60);
			const bool both = (connection.flags & 1) != 0;
			DrawArc(drawList, connection.start, connection.end, WithAlpha(colour, enabled ? 0.95f : 0.6f), enabled ? 2.0f : 1.0f, !both);
			DEBUG_MENU::WorldCircle(drawList, connection.start, (std::max)(connection.radius, 0.1f), WithAlpha(colour, 0.8f), 16, 1.0f);
			DEBUG_MENU::WorldCircle(drawList, connection.end, (std::max)(connection.radius, 0.1f), WithAlpha(colour, both ? 0.8f : 0.4f), 16, 1.0f);
			if (labels < 40 && Distance(centre, connection.start) < 12.0f)
			{
				labels++;
				const float at[3] = { (connection.start[0] + connection.end[0]) * 0.5f, (connection.start[1] + connection.end[1]) * 0.5f + 0.6f, (connection.start[2] + connection.end[2]) * 0.5f };
				QueueText(at, Format("#%d %s%s%s, %s", static_cast<int>(i), NAV_DEBUG::LinkTypeName(NAV_DEBUG::LinkType(area)), both ? ", both ways" : "",
					enabled ? "" : ", OFF", NAV_DEBUG::ClassesText(NAV_DEBUG::Admittance(area)).c_str()).c_str(), colour);
			}
		}
	}

	void OutlinePoly(ImDrawList* drawList, const NAV_DEBUG::Mesh& mesh, int index, ImU32 colour, float thickness, const char* label, bool links)
	{
		if (index < 0 || index >= static_cast<int>(mesh.polys.size()))
			return;
		const NAV_DEBUG::Poly& poly = mesh.polys[index];
		float points[6][3];
		PolyPoints(mesh, poly, points, 0.06f);
		if (NAV_DEBUG::IsOffMesh(poly.area) || poly.vertCount < 3)
		{
			if (poly.vertCount >= 2)
				DEBUG_MENU::WorldLine(drawList, points[0], points[1], colour, thickness);
		}
		else
			DEBUG_MENU::WorldPolygon(drawList, points, poly.vertCount, WithAlpha(colour, 0.15f), colour, thickness);
		if (links)
			for (int other : Linked(mesh, index))
			{
				const float a[3] = { poly.centre[0], poly.centre[1] + 0.1f, poly.centre[2] };
				const NAV_DEBUG::Poly& target = mesh.polys[other];
				const float b[3] = { target.centre[0], target.centre[1] + 0.1f, target.centre[2] };
				DEBUG_MENU::WorldArrow(drawList, a, b, WithAlpha(colour, 0.6f), 1.0f);
			}
		if (label)
		{
			const float at[3] = { poly.centre[0], poly.centre[1] + 0.3f, poly.centre[2] };
			QueueText(at, Format("%s #%d", label, index).c_str(), colour);
		}
	}

	void DrawUnder(ImDrawList* drawList, const NAV_DEBUG::Mesh& mesh)
	{
		if (const DEBUG_MENU::Character* player = Player())
			OutlinePoly(drawList, mesh, NAV_DEBUG::PolyAt(player->position), IM_COL32(255, 255, 255, 255), 3.0f, "player", true);
		if (const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected())
		{
			ActorNav actor;
			if (!selected->player && ReadActorNav(selected->object, actor))
				OutlinePoly(drawList, mesh, NAV_DEBUG::PolyIndex(actor.poly), IM_COL32(60, 220, 255, 255), 3.0f, selected->name.c_str(), true);
		}
		DEBUG_MENU::PickResult pick;
		if (DEBUG_MENU::Pick(pick))
			OutlinePoly(drawList, mesh, NAV_DEBUG::PolyAt(pick.position), IM_COL32(255, 230, 60, 255), 3.0f, "crosshair", true);
	}

	void DrawPathLog(ImDrawList* drawList)
	{
		const Logs& logs = CopyLogs();
		const float now = NAV_DEBUG::Now();
		int drawn = 0;
		for (const NAV_DEBUG::PathRecord& record : logs.paths)
		{
			if (drawn >= 64 || now - record.time > 60.0f)
				break;
			// Noted later than now: from a level played before the game clock started again (its places mean nothing here)
			if (!PathShown(record) || record.time > now + 0.5f)
				continue;
			drawn++;
			const bool selected = record.serial == g_selectedPath;
			const ImU32 colour = record.failed && !record.partial ? Rgb(240, 60, 60) : record.partial ? Rgb(255, 170, 40) : Rgb(70, 210, 90);
			const float thickness = selected ? 3.0f : 1.5f;
			const float a[3] = { record.originalStart[0], record.originalStart[1] + 0.3f, record.originalStart[2] };
			const float b[3] = { record.originalEnd[0], record.originalEnd[1] + 0.3f, record.originalEnd[2] };
			if (record.partial)
			{
				const float reached[3] = { record.resultEndPos[0], record.resultEndPos[1] + 0.3f, record.resultEndPos[2] };
				DEBUG_MENU::WorldLine(drawList, a, reached, colour, thickness);
				DEBUG_MENU::WorldLine(drawList, reached, b, WithAlpha(colour, 0.4f), 1.0f);
			}
			else
				DEBUG_MENU::WorldLine(drawList, a, b, colour, thickness);
			DEBUG_MENU::WorldCross(drawList, b, 0.25f, colour, thickness);
			float camera[3];
			if (DEBUG_MENU::CameraPosition(camera) && (selected || Distance(camera, a) < 20.0f))
				QueueText(a, Format("%s %s", NAV_DEBUG::PathKindName(record.kind), record.partial ? "partial" : record.failed ? "failed" : "ok").c_str(), colour);
		}
	}

	void DrawTraversals(ImDrawList* drawList, const NAV_DEBUG::Mesh& mesh, const float centre[3])
	{
		const COVER_DEBUG::Cover& cover = COVER_DEBUG::ReadCover();
		int labels = 0;
		for (const COVER_DEBUG::Traversal& traversal : cover.traversals)
		{
			const float middle[3] = { (traversal.entrance[0][0] + traversal.exit[0][0]) * 0.5f, (traversal.entrance[0][1] + traversal.exit[0][1]) * 0.5f, (traversal.entrance[0][2] + traversal.exit[0][2]) * 0.5f };
			if (Distance(centre, middle) > g_radius)
				continue;
			const ImU32 colour = traversal.enabled ? TraversalColour(traversal.type) : Rgb(130, 130, 130);
			DEBUG_MENU::WorldLine(drawList, traversal.entrance[0], traversal.entrance[1], Rgb(70, 210, 90), 2.0f);
			DEBUG_MENU::WorldLine(drawList, traversal.exit[0], traversal.exit[1], Rgb(230, 60, 60), 2.0f);
			// Its path (points left at nothing are unused)
			const float* previous = nullptr;
			for (int p = 0; p < 16; p++)
			{
				const float* point = traversal.path[p];
				if (point[0] == 0.0f && point[1] == 0.0f && point[2] == 0.0f)
					continue;
				if (previous && Distance(previous, point) < 30.0f)
					DEBUG_MENU::WorldLine(drawList, previous, point, colour, 1.5f);
				previous = point;
			}
			if (labels < 30 && Distance(centre, middle) < 15.0f)
			{
				labels++;
				const float at[3] = { middle[0], middle[1] + 0.8f, middle[2] };
				QueueText(at, Format("%s #%d%s", COVER_DEBUG::TraversalTypeName(traversal.type), traversal.uid, traversal.enabled ? "" : " (off)").c_str(), colour);
			}
		}
		(void)mesh;
	}

	void DrawPathTest(ImDrawList* drawList)
	{
		const NAV_DEBUG::PathTest& test = NAV_DEBUG::UpdatePathTest();
		if (test.state == 0)
			return;
		DEBUG_MENU::WorldCross(drawList, test.start, 0.3f, IM_COL32(255, 255, 255, 255), 2.0f);
		DEBUG_MENU::WorldCross(drawList, test.end, 0.3f, IM_COL32(255, 255, 255, 255), 2.0f);
		if (test.state != 3)
			return;
		const ImU32 colour = test.status == 1 ? Rgb(80, 230, 120) : Rgb(240, 60, 60);
		const size_t count = test.points.size() / 3;
		for (size_t i = 1; i < count; i++)
		{
			const float a[3] = { test.points[(i - 1) * 3], test.points[(i - 1) * 3 + 1] + 0.15f, test.points[(i - 1) * 3 + 2] };
			const float b[3] = { test.points[i * 3], test.points[i * 3 + 1] + 0.15f, test.points[i * 3 + 2] };
			DEBUG_MENU::WorldLine(drawList, a, b, colour, 3.0f);
		}
		if (test.status != 1)
			DEBUG_MENU::WorldLine(drawList, test.start, test.end, WithAlpha(colour, 0.5f), 1.0f);
	}

	// A polygon chosen on another mesh (another level or state) means nothing on this one
	void ForgetOnNewMesh(const NAV_DEBUG::Mesh& mesh)
	{
		if (g_highlight >= 0 && g_highlightDetour != mesh.detour)
			g_highlight = -1;
	}

	void DrawOverlay(ImDrawList* drawList)
	{
		g_queuedLabels.clear();
		const bool test = g_testPath && NAV_DEBUG::PathTestActive();
		const bool any = g_polys || g_offMesh || g_barrierPolys || g_under || g_pathLog || g_traversals || g_highlight >= 0 || test;
		if (!any)
			return;
		if (test)
			DrawPathTest(drawList);
		const bool meshWanted = g_polys || g_offMesh || g_barrierPolys || g_under || g_highlight >= 0 || g_traversals;
		if (g_pathLog)
			DrawPathLog(drawList);
		if (!meshWanted)
			return;
		const NAV_DEBUG::Mesh& mesh = NAV_DEBUG::Read();
		ForgetOnNewMesh(mesh);
		float centre[3];
		if (!OverlayCentre(centre))
			return;
		if (mesh.have && (g_polys || g_barrierPolys))
			DrawPolys(drawList, mesh, centre);
		if (mesh.have && g_offMesh)
			DrawOffMesh(drawList, mesh, centre);
		if (g_traversals)
			DrawTraversals(drawList, mesh, centre);
		if (mesh.have && g_under)
			DrawUnder(drawList, mesh);
		if (mesh.have && g_highlight >= 0)
			OutlinePoly(drawList, mesh, g_highlight, IM_COL32(255, 60, 255, 255), 3.0f, "chosen", true);
	}

	// ---- The tab ----

	void Legend(int mode)
	{
		struct Item { ImU32 colour; const char* text; };
		std::vector<Item> items;
		switch (mode)
		{
		case kByMarkup: items = { { Rgb(70, 150, 255), "normal" }, { Rgb(180, 90, 255), "backstage (vents' sheet)" }, { Rgb(255, 150, 40), "expensive" }, { Rgb(150, 150, 150), "no markup" } }; break;
		case kByClasses: items = { { Rgb(70, 210, 90), "all" }, { Rgb(230, 60, 60), "none" }, { ClassesColour(NAV_DEBUG::kClassAlien), "alien only" }, { ClassesColour(NAV_DEBUG::kClassPlayer), "player only" }, { ClassesColour(0x1C), "NPCs only" } }; break;
		case kByHeight: items = { { Rgb(70, 210, 90), "standing" }, { Rgb(240, 210, 60), "crouch" }, { Rgb(230, 70, 60), "deep crouch" } }; break;
		case kByEnabled: items = { { Rgb(70, 210, 90), "enabled" }, { Rgb(230, 60, 60), "disabled" } }; break;
		case kByBarrier: items = { { Rgb(120, 120, 120), "no barrier" }, { Rgb(70, 210, 90), "door lets all through" }, { Rgb(230, 60, 60), "door lets none through" }, { Rgb(255, 170, 40), "some" }, { Rgb(255, 0, 255), "area id no barrier row has" } }; break;
		case kByTraffic: items = { { Hue(0.0f), "just now" }, { Hue(0.33f), "half a minute" }, { Hue(0.66f), "a minute or more" }, { Rgb(60, 60, 70), "never" } }; break;
		case kByReach: items = { { Rgb(70, 210, 90), "reached from the seed" }, { Rgb(230, 60, 60), "passable but not reached" }, { Rgb(80, 80, 80), "not passable for the filter" } }; break;
		case kByIsland: items = { { Rgb(70, 210, 90), "largest island" }, { Hue(0.1f + 0.618034f), "the others, a colour each" }, { Rgb(80, 80, 80), "not passable" } }; break;
		default: break;
		}
		// Swatch and words kept together, onto the next line when they would run off the window's edge
		const float swatch = 12.0f * DEBUG_MENU::Scale();
		const ImGuiStyle& style = ImGui::GetStyle();
		for (size_t i = 0; i < items.size(); i++)
		{
			if (i)
			{
				ImGui::SameLine();
				if (ImGui::GetContentRegionAvail().x < swatch + style.ItemSpacing.x + ImGui::CalcTextSize(items[i].text).x)
					ImGui::NewLine();
			}
			ImGui::ColorButton(Format("##legend%d", static_cast<int>(i)).c_str(), Colour4(items[i].colour), ImGuiColorEditFlags_NoTooltip | ImGuiColorEditFlags_NoBorder,
				ImVec2(swatch, swatch));
			ImGui::SameLine();
			ImGui::TextUnformatted(items[i].text);
		}
	}

	void DrawStatus(const NAV_DEBUG::Mesh& mesh, const Logs& logs)
	{
		if (!NAV_DEBUG::HooksAttached())
			TextDisabledWrapped("The navigation hooks are not attached: the state, door log and path log are not followed.");
		const int state = NAV_DEBUG::CurrentState();
		if (!mesh.haveObject)
		{
			ImGui::TextDisabled("The game has no nav mesh object (no level running).");
			return;
		}
		if (!mesh.have)
		{
			ImGui::TextColored(ImVec4(1.0f, 0.3f, 0.3f, 1.0f), "NO NAV MESH LOADED");
			ImGui::TextWrapped("The level runs without navigation: its state's nav_mesh file is missing, has the wrong version (the game wants 0x3B) or would not load. "
				"No character can path anywhere.");
			if (state >= 0)
				ImGui::Text("The last state loaded was state_%d.", state);
		}
		else
		{
			ImGui::TextWrapped("Nav mesh loaded%s: %d polygons, %d vertices, %d off-mesh connections, %d barriers.", state >= 0 ? Format(" (state_%d)", state).c_str() : "",
				mesh.polyCount, mesh.vertCount, mesh.offMeshConCount, static_cast<int>(NAV_DEBUG::Barriers().size()));
			ImGui::TextWrapped("Changed %u times since it loaded (doors, enabled links)%s.", mesh.changeCounter, mesh.lockMisses ? Format("; the game's lock was busy on %d frames", mesh.lockMisses).c_str() : "");
		}
		if (!logs.loads.empty())
		{
			const NAV_DEBUG::LoadRecord& last = logs.loads.front();
			ImGui::TextWrapped("Last load: state_%u at %.1f s - %s", last.state, last.time, last.loaded ? Format("%d polygons, %d barriers put back", last.polys, last.barriers).c_str() : "NO MESH");
			if (logs.loads.size() > 1 && ImGui::TreeNode("Earlier loads"))
			{
				for (size_t i = 1; i < logs.loads.size(); i++)
					ImGui::Text("state_%u at %.1f s - %s", logs.loads[i].state, logs.loads[i].time, logs.loads[i].loaded ? Format("%d polygons", logs.loads[i].polys).c_str() : "NO MESH");
				ImGui::TreePop();
			}
		}
		else if (NAV_DEBUG::HooksAttached())
			TextDisabledWrapped("No state loaded since the debug menu attached (the level's state is not known).");
		if (mesh.have && ImGui::TreeNode("Tile header and ids"))
		{
			ImGui::TextWrapped("Magic 0x%08X, version %d, tile %d,%d layer %d, user id %u, %d bytes", mesh.magic, mesh.version, mesh.tileX, mesh.tileY, mesh.layer, mesh.userId, mesh.dataSize);
			ImGui::TextWrapped("Max links %d, detail meshes %d, detail vertices %d, detail triangles %d, BV nodes %d, BV quantisation %.4f", mesh.maxLinkCount,
				mesh.detailMeshCount, mesh.detailVertCount, mesh.detailTriCount, mesh.bvNodeCount, mesh.bvQuantFactor);
			ImGui::Text("Walkable height %.3f, radius %.3f, climb %.3f", mesh.walkableHeight, mesh.walkableRadius, mesh.walkableClimb);
			ImGui::Text("Bounds %.1f, %.1f, %.1f to %.1f, %.1f, %.1f", mesh.bmin[0], mesh.bmin[1], mesh.bmin[2], mesh.bmax[0], mesh.bmax[1], mesh.bmax[2]);
			ImGui::TextWrapped("Off-mesh connections from polygon %d. Polygon ids: %u salt bits (salt %u), %u tile bits (tile %d of %d), %u polygon bits",
				mesh.offMeshBase, mesh.saltBits, mesh.tileSalt, mesh.tileBits, mesh.tileIndex, mesh.maxTiles, mesh.polyBits);
			ImGui::TreePop();
		}
	}

	void DrawOverlaySection()
	{
		SwitchBox("Polygons", g_polys);
		ImGui::SameLine();
		SwitchBox("Barrier polygons", g_barrierPolys);
		ImGui::SameLine();
		SwitchBox("Off-mesh connections##draw", g_offMesh);
		ImGui::SameLine();
		SwitchBox("Traversals##draw", g_traversals);
		SwitchBox("Under the player, selected and crosshair", g_under);
		ImGui::SameLine();
		SwitchBox("Polygon numbers", g_labels);
		ImGui::SameLine();
		SwitchBox("Around the player", g_aroundPlayer);
		int mode = (std::max)(0, (std::min)(static_cast<int>(g_mode), kColourModes - 1));
		ImGui::SetNextItemWidth(320.0f * DEBUG_MENU::Scale());
		if (ImGui::Combo("Colour by", &mode, kColourModeNames, kColourModes))
			g_mode = static_cast<float>(mode);
		Legend(mode);
		SliderValue("Distance", g_radius, 2.0f, 100.0f, "%.0f m");
		ImGui::SameLine();
		SliderValue("Height band", g_band, 0.5f, 50.0f, "%.1f m");
		SliderValue("Most polygons", g_maxPolys, 100.0f, 20000.0f, "%.0f");
		ImGui::SameLine();
		SliderValue("Fill", g_fill, 0.0f, 1.0f, "%.2f");
		if (g_polys || g_barrierPolys)
			TextDisabledWrapped("%d polygons drawn. Nothing hides them (no depth test): the height band keeps other floors out.", g_drawnPolys);
	}

	void DrawPolyDetails(const NAV_DEBUG::Mesh& mesh, int index)
	{
		if (index < 0 || index >= static_cast<int>(mesh.polys.size()))
		{
			ImGui::TextDisabled("No polygon.");
			return;
		}
		const NAV_DEBUG::Poly& poly = mesh.polys[index];
		const uint32_t area = poly.area;
		ImGui::Text("Polygon %d, id 0x%08X, flags 0x%08X - %s", index, NAV_DEBUG::PolyRef(index), area, NAV_DEBUG::IsOffMesh(area) ? "an off-mesh connection" : "ground");
		ImGui::TextWrapped("%s, admits %s, %s, markup %s", NAV_DEBUG::Enabled(area) ? "enabled" : "DISABLED", NAV_DEBUG::ClassesText(NAV_DEBUG::Admittance(area)).c_str(),
			NAV_DEBUG::HeightName(NAV_DEBUG::HeightClass(area)), NAV_DEBUG::MarkupText(NAV_DEBUG::Markup(area)).c_str());
		const int id = NAV_DEBUG::AreaId(area);
		if (id)
		{
			const NAV_DEBUG::Barrier* barrier = NAV_DEBUG::BarrierByArea(id);
			if (barrier)
				ImGui::TextWrapped("Area id %d: the barrier of door 0x%08X, which lets %s through", id, barrier->entity, NAV_DEBUG::ClassesText(barrier->classes).c_str());
			else
				TextColouredWrapped(ImVec4(1.0f, 0.3f, 1.0f, 1.0f), "Area id %d: no barrier row carries it (nothing will ever open or close it)", id);
		}
		if (NAV_DEBUG::IsOffMesh(area))
			ImGui::Text("Link type %s", NAV_DEBUG::LinkTypeName(NAV_DEBUG::LinkType(area)));
		ImGui::Text("Centre %.2f, %.2f, %.2f, %d vertices", poly.centre[0], poly.centre[1], poly.centre[2], poly.vertCount);
		std::string links;
		for (int other : Linked(mesh, index))
			links += (links.empty() ? "" : ", ") + std::to_string(other);
		ImGui::TextWrapped("Links to: %s", links.empty() ? "nothing (an island of one)" : links.c_str());
		uint16_t ticks = 0;
		uint8_t character = 0;
		if (NAV_DEBUG::Freshness(index, ticks, character))
		{
			if (ticks == 0xFFFF)
				ImGui::TextDisabled("No character has pathed through it.");
			else
				ImGui::Text("Character %u pathed through it %.1f s ago.", character, ticks / 30.0f);
		}
		ImGui::PushID(index);
		if (ImGui::SmallButton(g_highlight == index ? "Stop showing it" : "Show it"))
			Choose(g_highlight == index ? -1 : index);
		ImGui::SameLine();
		if (ImGui::SmallButton("Flood from it"))
		{
			Choose(index);
			g_floodFrom = 3;
			RunFlood();
		}
		ImGui::PopID();
	}

	void DrawUnderSection(const NAV_DEBUG::Mesh& mesh)
	{
		if (!mesh.have)
		{
			ImGui::TextDisabled("No nav mesh.");
			return;
		}
		SwitchBox("Draw them##under", g_under);
		if (const DEBUG_MENU::Character* player = Player())
		{
			float height = 0.0f;
			const int poly = NAV_DEBUG::PolyAt(player->position, &height);
			if (ImGui::TreeNodeEx("player", ImGuiTreeNodeFlags_DefaultOpen, "Under the player: %s", poly >= 0 ? Format("polygon %d (%.2f m below the feet)", poly, player->position[1] - height).c_str() : "NO POLYGON"))
			{
				DrawPolyDetails(mesh, poly);
				ImGui::TreePop();
			}
		}
		DEBUG_MENU::PickResult pick;
		if (DEBUG_MENU::Pick(pick))
		{
			const int poly = NAV_DEBUG::PolyAt(pick.position);
			if (ImGui::TreeNodeEx("pick", 0, "Under the crosshair: %s", poly >= 0 ? Format("polygon %d", poly).c_str() : "NO POLYGON"))
			{
				DrawPolyDetails(mesh, poly);
				ImGui::TreePop();
			}
		}
		else
			TextDisabledWrapped("Nothing under the crosshair: turn on the crosshair in World > Inspect for the polygon under it, or the game is paused.");

		// Each NPC: the polygon its movement says it is on, against the one under its feet
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_SizingFixedFit;
		if (ImGui::BeginTable("actors", 6, flags))
		{
			ImGui::TableSetupColumn("Character");
			ImGui::TableSetupColumn("Its movement's polygon");
			ImGui::TableSetupColumn("Under its feet");
			ImGui::TableSetupColumn("Class");
			ImGui::TableSetupColumn("Backstage");
			ImGui::TableSetupColumn("Deep crouch");
			ImGui::TableHeadersRow();
			for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			{
				if (character.player || character.dead)
					continue;
				ActorNav actor;
				if (!ReadActorNav(character.object, actor))
					continue;
				const int gamePoly = NAV_DEBUG::PolyIndex(actor.poly);
				const int ours = NAV_DEBUG::PolyAt(character.position);
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::PushID(static_cast<int>(character.id));
				if (ImGui::Selectable(CharacterLabel(&character).c_str(), DEBUG_MENU::SelectedId() == character.id, ImGuiSelectableFlags_SpanAllColumns))
					DEBUG_MENU::Select(character.id);
				ImGui::PopID();
				ImGui::TableNextColumn();
				if (gamePoly >= 0)
					ImGui::Text("%d (0x%08X)", gamePoly, actor.poly);
				else
					ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.4f, 1.0f), actor.poly ? "0x%08X (not this mesh's)" : "none", actor.poly);
				ImGui::TableNextColumn();
				if (ours < 0)
					ImGui::TextDisabled("none");
				else if (ours == gamePoly)
					ImGui::Text("%d (the same)", ours);
				else
					ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "%d (differs)", ours);
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(NavigationClassName(actor.navigationClass));
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(actor.backstage ? "yes" : "-");
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(actor.deepCrouch ? "yes" : "-");
			}
			ImGui::EndTable();
		}

		ImGui::SetNextItemWidth(140.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("Decode a polygon id", "0x00000000", g_decodeText, sizeof(g_decodeText), ImGuiInputTextFlags_CharsHexadecimal);
		if (g_decodeText[0])
		{
			const uint32_t ref = static_cast<uint32_t>(strtoul(g_decodeText, nullptr, 16));
			const NAV_DEBUG::Decoded decoded = NAV_DEBUG::Decode(ref);
			ImGui::TextWrapped("Salt %u, tile %u, polygon %u: %s", decoded.salt, decoded.tile, decoded.poly, decoded.valid ? "valid" : decoded.why.c_str());
			if (decoded.valid)
				DrawPolyDetails(mesh, static_cast<int>(decoded.poly));
		}
		if (g_highlight >= 0)
		{
			ImGui::Text("Shown: polygon %d", g_highlight);
			ImGui::SameLine();
			if (ImGui::SmallButton("Stop showing"))
				g_highlight = -1;
		}
	}

	void DrawBarrierSection(const NAV_DEBUG::Mesh& mesh, const Logs& logs)
	{
		const std::vector<NAV_DEBUG::Barrier>& barriers = NAV_DEBUG::Barriers();
		SwitchBox("Draw the barrier polygons", g_barrierPolys);
		if (barriers.empty())
		{
			ImGui::TextDisabled(mesh.haveObject ? "The level has no barrier rows." : "No level running.");
			return;
		}
		float centre[3] = {};
		const bool haveCentre = OverlayCentre(centre);
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
		if (ImGui::BeginTable("barriers", 8, flags, ImVec2(0.0f, 220.0f * DEBUG_MENU::Scale())))
		{
			ImGui::TableSetupScrollFreeze(1, 1);
			ImGui::TableSetupColumn("Area");
			ImGui::TableSetupColumn("Lets through");
			ImGui::TableSetupColumn("Door (sound doorway) id");
			ImGui::TableSetupColumn("Barrier entity / instance");
			ImGui::TableSetupColumn("Polygons");
			ImGui::TableSetupColumn("Distance");
			ImGui::TableSetupColumn("Collision row / zone");
			ImGui::TableSetupColumn("Changes");
			ImGui::TableHeadersRow();
			for (const NAV_DEBUG::Barrier& barrier : barriers)
			{
				int changes = 0;
				for (const NAV_DEBUG::BarrierRecord& record : logs.barriers)
					if (record.area == barrier.area)
						changes++;
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("%d", barrier.area);
				ImGui::TableNextColumn();
				ImGui::TextColored(Colour4(ClassesColour(barrier.classes)), "%s", NAV_DEBUG::ClassesText(barrier.classes).c_str());
				ImGui::TableNextColumn();
				ImGui::Text("0x%08X", barrier.entity);
				ImGui::TableNextColumn();
				ImGui::Text("0x%08X / 0x%08X", barrier.handleEntity, barrier.handleParent);
				ImGui::TableNextColumn();
				if (barrier.polys)
					ImGui::Text("%d (%d on)", barrier.polys, barrier.enabledPolys);
				else
					ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.4f, 1.0f), "none");
				ImGui::TableNextColumn();
				if (barrier.polys && haveCentre)
					ImGui::Text("%.1f m", Distance(centre, barrier.centre));
				else
					ImGui::TextDisabled("-");
				ImGui::TableNextColumn();
				if (barrier.haveCollision)
					ImGui::Text("%d / 0x%08X", barrier.collisionRow, barrier.zone);
				else
					ImGui::TextDisabled("-");
				ImGui::TableNextColumn();
				ImGui::Text("%d", changes);
			}
			ImGui::EndTable();
		}
		TextDisabledWrapped("A barrier with no polygons cuts nothing out of the nav mesh: the door can never stop anyone.");

		if (ImGui::TreeNode("Door audit (against the sound network's doorways)"))
		{
			const std::vector<AuditRow> rows = DoorAudit();
			int both = 0, navOnly = 0, soundOnly = 0, problems = 0;
			for (const AuditRow& row : rows)
			{
				both += row.nav >= 0 && row.sound >= 0;
				navOnly += row.nav >= 0 && row.sound < 0;
				soundOnly += row.nav < 0 && row.sound >= 0;
				problems += row.problem;
			}
			ImGui::TextWrapped("A door sets its nav barrier and its sound doorway by the same id. %d have both, %d a nav barrier only (hiding places, vents, scripted "
				"blockers...), %d a sound doorway only (it can never be opened by a door for hearing), %d disagree now.", both, navOnly, soundOnly, problems);
			if (SOUND_DEBUG::Barriers().empty())
				TextDisabledWrapped("The sound network has no doorways (or has not been read: open the Sound tab once).");
			const ImGuiTableFlags auditFlags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
			if (ImGui::BeginTable("audit", 4, auditFlags, ImVec2(0.0f, 200.0f * DEBUG_MENU::Scale())))
			{
				ImGui::TableSetupScrollFreeze(0, 1);
				ImGui::TableSetupColumn("Id");
				ImGui::TableSetupColumn("Nav barrier");
				ImGui::TableSetupColumn("Sound doorway");
				ImGui::TableSetupColumn("Verdict");
				ImGui::TableHeadersRow();
				const std::vector<SOUND_DEBUG::Barrier>& sound = SOUND_DEBUG::Barriers();
				for (const AuditRow& row : rows)
				{
					ImGui::TableNextRow();
					ImGui::TableNextColumn();
					ImGui::Text("0x%08X", row.id);
					ImGui::TableNextColumn();
					if (row.nav >= 0)
						ImGui::Text("area %d, %s", barriers[row.nav].area, NAV_DEBUG::ClassesText(barriers[row.nav].classes).c_str());
					else
						ImGui::TextDisabled("-");
					ImGui::TableNextColumn();
					if (row.sound >= 0)
						ImGui::Text("%s (%.2f), set %d times", sound[row.sound].state <= 0.01f ? "open" : sound[row.sound].state >= 0.99f ? "closed" : "moving", sound[row.sound].state, sound[row.sound].timesSet);
					else
						ImGui::TextDisabled("-");
					ImGui::TableNextColumn();
					if (row.problem)
						ImGui::TextColored(ImVec4(1.0f, 0.6f, 0.2f, 1.0f), "%s", row.verdict);
					else
						ImGui::TextUnformatted(row.verdict);
				}
				ImGui::EndTable();
			}
			ImGui::TreePop();
		}

		if (ImGui::TreeNode("Door log (barriers' classes written)"))
		{
			if (ImGui::SmallButton("Clear the logs"))
				NAV_DEBUG::ClearLogs();
			const float now = NAV_DEBUG::Now();
			if (logs.barriers.empty())
				TextDisabledWrapped("No door has changed its barrier since the level's state loaded.");
			for (const NAV_DEBUG::BarrierRecord& record : logs.barriers)
			{
				const NAV_DEBUG::Barrier* barrier = NAV_DEBUG::BarrierByArea(record.area);
				ImGui::TextWrapped("%.1f s ago: area %d (%s) %s -> %s%s", now - record.time, record.area, barrier ? Format("door 0x%08X", barrier->entity).c_str() : "no barrier row",
					record.before < 0 ? "not known" : NAV_DEBUG::ClassesText(static_cast<uint32_t>(record.before)).c_str(), NAV_DEBUG::ClassesText(record.after).c_str(),
					record.sets > 1 ? Format(" (x%d)", record.sets).c_str() : "");
			}
			ImGui::TreePop();
		}
	}

	void DrawOffMeshSection(const NAV_DEBUG::Mesh& mesh)
	{
		SwitchBox("Draw them##offmesh", g_offMesh);
		if (!mesh.have)
		{
			ImGui::TextDisabled("No nav mesh.");
			return;
		}
		int byType[5] = {}, disabled = 0, bothWays = 0;
		for (const NAV_DEBUG::OffMesh& connection : mesh.offMesh)
		{
			const uint32_t area = connection.poly >= 0 && connection.poly < static_cast<int>(mesh.polys.size()) ? mesh.polys[connection.poly].area : 0;
			const int type = NAV_DEBUG::LinkType(area);
			if (type >= 0 && type < 5)
				byType[type]++;
			disabled += !NAV_DEBUG::Enabled(area);
			bothWays += (connection.flags & 1) != 0;
		}
		ImGui::TextWrapped("%d connections: %d manual, %d teleport, %d traversal, %d wait, %d backstage; %d both ways, %d disabled.", static_cast<int>(mesh.offMesh.size()),
			byType[0], byType[1], byType[2], byType[3], byType[4], bothWays, disabled);
		float centre[3] = {};
		if (!OverlayCentre(centre))
			return;
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##offmeshfilter", "Filter (type, entity id)", g_offMeshFilter, sizeof(g_offMeshFilter));
		struct Row { int index; float distance; };
		std::vector<Row> rows;
		for (size_t i = 0; i < mesh.offMesh.size(); i++)
			rows.push_back({ static_cast<int>(i), (std::min)(Distance(centre, mesh.offMesh[i].start), Distance(centre, mesh.offMesh[i].end)) });
		// A connection by its polygon's id, and its place in the list
		TABLE_ORDER::Nearest("nav.offmesh", rows, [&mesh](const Row& row) { return static_cast<uint64_t>(mesh.offMesh[row.index].ref) << 32 | static_cast<uint32_t>(row.index); },
			[](const Row& row) { return row.distance; });
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
		if (!ImGui::BeginTable("offmesh", 9, flags, ImVec2(0.0f, 220.0f * DEBUG_MENU::Scale())))
			return;
		ImGui::TableSetupScrollFreeze(1, 1);
		ImGui::TableSetupColumn("#");
		ImGui::TableSetupColumn("Type");
		ImGui::TableSetupColumn("Admits");
		ImGui::TableSetupColumn("Ways / side");
		ImGui::TableSetupColumn("Length / radius");
		ImGui::TableSetupColumn("Distance");
		ImGui::TableSetupColumn("Traversal");
		ImGui::TableSetupColumn("Entity / instance");
		ImGui::TableSetupColumn("Cost / speeds");
		ImGui::TableHeadersRow();
		int shown = 0;
		for (const Row& row : rows)
		{
			if (shown >= 200)
				break;
			const NAV_DEBUG::OffMesh& connection = mesh.offMesh[row.index];
			const uint32_t area = connection.poly >= 0 && connection.poly < static_cast<int>(mesh.polys.size()) ? mesh.polys[connection.poly].area : 0;
			const std::string type = NAV_DEBUG::LinkTypeName(NAV_DEBUG::LinkType(area));
			const std::string entity = Format("%08X", connection.entity);
			if (g_offMeshFilter[0] && type.find(g_offMeshFilter) == std::string::npos && entity.find(g_offMeshFilter) == std::string::npos)
				continue;
			shown++;
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::PushID(row.index);
			if (ImGui::Selectable(std::to_string(row.index).c_str(), g_highlight == connection.poly, ImGuiSelectableFlags_SpanAllColumns))
				Choose(g_highlight == connection.poly ? -1 : connection.poly);
			ImGui::PopID();
			ImGui::TableNextColumn();
			ImGui::TextColored(Colour4(LinkTypeColour(NAV_DEBUG::LinkType(area))), "%s%s", type.c_str(), NAV_DEBUG::Enabled(area) ? "" : " (OFF)");
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(NAV_DEBUG::ClassesText(NAV_DEBUG::Admittance(area)).c_str());
			ImGui::TableNextColumn();
			ImGui::Text("%s / %s", (connection.flags & 1) ? "both" : "one", connection.side == 0xFF ? "inside" : std::to_string(connection.side).c_str());
			ImGui::TableNextColumn();
			ImGui::Text("%.1f m / %.2f", Distance(connection.start, connection.end), connection.radius);
			ImGui::TableNextColumn();
			ImGui::Text("%.1f m", row.distance);
			ImGui::TableNextColumn();
			if (connection.traversal)
			{
				const COVER_DEBUG::Traversal* traversal = COVER_DEBUG::TraversalByUid(static_cast<int>(connection.traversal));
				if (traversal)
					ImGui::Text("#%u %s", connection.traversal, COVER_DEBUG::TraversalTypeName(traversal->type));
				else
					ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.4f, 1.0f), "#%u (none such)", connection.traversal);
			}
			else
				ImGui::TextDisabled("-");
			ImGui::TableNextColumn();
			ImGui::Text("0x%08X / 0x%08X", connection.entity, connection.parent);
			ImGui::TableNextColumn();
			ImGui::Text("%.2f / %s..%s", connection.extraCost, NAV_DEBUG::SpeedName(connection.minSpeed), NAV_DEBUG::SpeedName(connection.maxSpeed));
		}
		ImGui::EndTable();
		TextDisabledWrapped("Nearest first. Click one to show its polygon (and links). Side \"inside\" (0xFF) is a connection within the tile.");
	}

	void DrawTraversalSection(const NAV_DEBUG::Mesh& mesh)
	{
		SwitchBox("Draw them##traversals", g_traversals);
		const COVER_DEBUG::Cover& cover = COVER_DEBUG::ReadCover();
		if (!cover.loaded)
		{
			ImGui::TextDisabled("The level's cover and traversal data is not loaded.");
			return;
		}
		// Which traversals an off-mesh connection names
		std::vector<int> connectionOf(cover.traversals.size() + 1, -1);
		for (size_t i = 0; i < mesh.offMesh.size(); i++)
			if (mesh.offMesh[i].traversal > 0 && mesh.offMesh[i].traversal <= cover.traversals.size())
				connectionOf[mesh.offMesh[i].traversal] = static_cast<int>(i);
		int byType[9] = {}, disabled = 0, unlinked = 0;
		for (const COVER_DEBUG::Traversal& traversal : cover.traversals)
		{
			if (traversal.type >= 0 && traversal.type < 9)
				byType[traversal.type]++;
			disabled += !traversal.enabled;
			unlinked += traversal.uid > 0 && traversal.uid < static_cast<int>(connectionOf.size()) && connectionOf[traversal.uid] < 0;
		}
		ImGui::TextWrapped("%d traversals: %d climb under, %d vent, %d ledge, %d sidling, %d ladder, %d pipe, %d balance beam, %d tight gap, %d wall; %d disabled, "
			"%d with no off-mesh connection naming them%s.", static_cast<int>(cover.traversals.size()), byType[0], byType[1], byType[2], byType[3], byType[4],
			byType[5], byType[6], byType[7], byType[8], disabled, unlinked, mesh.have ? "" : " (no nav mesh to check)");

		float centre[3] = {};
		if (OverlayCentre(centre))
		{
			struct Row { const COVER_DEBUG::Traversal* traversal; float distance; };
			std::vector<Row> rows;
			for (const COVER_DEBUG::Traversal& traversal : cover.traversals)
				rows.push_back({ &traversal, (std::min)(Distance(centre, traversal.entrance[0]), Distance(centre, traversal.exit[0])) });
			TABLE_ORDER::Nearest("nav.traversals", rows, [](const Row& row) { return static_cast<uint64_t>(static_cast<uint32_t>(row.traversal->uid)); }, [](const Row& row) { return row.distance; });
			const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
			if (ImGui::BeginTable("traversals", 7, flags, ImVec2(0.0f, 200.0f * DEBUG_MENU::Scale())))
			{
				ImGui::TableSetupScrollFreeze(1, 1);
				ImGui::TableSetupColumn("Uid");
				ImGui::TableSetupColumn("Type");
				ImGui::TableSetupColumn("Classes");
				ImGui::TableSetupColumn("Distance");
				ImGui::TableSetupColumn("Off-mesh connection");
				ImGui::TableSetupColumn("Entity / instance");
				ImGui::TableSetupColumn("Animation tree");
				ImGui::TableHeadersRow();
				for (size_t i = 0; i < rows.size() && i < 200; i++)
				{
					const COVER_DEBUG::Traversal& traversal = *rows[i].traversal;
					ImGui::TableNextRow();
					ImGui::TableNextColumn();
					ImGui::Text("%d", traversal.uid);
					ImGui::TableNextColumn();
					ImGui::TextColored(Colour4(TraversalColour(traversal.type)), "%s%s", COVER_DEBUG::TraversalTypeName(traversal.type), traversal.enabled ? "" : " (off)");
					ImGui::TableNextColumn();
					ImGui::TextUnformatted(NAV_DEBUG::ClassesText(traversal.classes).c_str());
					ImGui::TableNextColumn();
					ImGui::Text("%.1f m", rows[i].distance);
					ImGui::TableNextColumn();
					const int connection = traversal.uid > 0 && traversal.uid < static_cast<int>(connectionOf.size()) ? connectionOf[traversal.uid] : -1;
					if (connection >= 0)
						ImGui::Text("#%d", connection);
					else
						ImGui::TextColored(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "none");
					ImGui::TableNextColumn();
					ImGui::Text("0x%08X / 0x%08X", traversal.entity, traversal.parent);
					ImGui::TableNextColumn();
					ImGui::Text("0x%08X", traversal.animationTree);
				}
				ImGui::EndTable();
			}
		}

		// The player's tracker
		const DEBUG_MENU::Character* player = Player();
		COVER_DEBUG::Tracker tracker;
		if (player && COVER_DEBUG::ReadTracker(player->object, tracker))
		{
			ImGui::TextWrapped("The player: %s, %d traversals near%s%s", tracker.canTransition ? "can start one now" : "cannot start one now", static_cast<int>(tracker.nearby.size()),
				tracker.onTraversal ? Format(", on 0x%08X", tracker.onTraversal).c_str() : "", tracker.closest >= 0 ? Format(", closest slot %d", tracker.closest).c_str() : "");
			for (const COVER_DEBUG::Tracker::Near& nearby : tracker.nearby)
			{
				const COVER_DEBUG::Traversal* match = nullptr;
				for (const COVER_DEBUG::Traversal& traversal : cover.traversals)
					if (traversal.entity == nearby.entity)
						match = &traversal;
				ImGui::BulletText("%s0x%08X (ref %u) %.1f m%s", nearby.index == tracker.closest ? "closest: " : "", nearby.entity, nearby.ref, nearby.distance,
					match ? Format(" - %s #%d", COVER_DEBUG::TraversalTypeName(match->type), match->uid).c_str() : "");
			}
		}
	}

	void DrawPathLogSection(const Logs& logs)
	{
		SwitchBox("Draw them##paths", g_pathLog);
		ImGui::SameLine();
		SwitchBox("Failed and partial only", g_failedOnly);
		ImGui::SameLine();
		if (ImGui::SmallButton("Clear"))
			NAV_DEBUG::ClearLogs();
		if (!NAV_DEBUG::HooksAttached())
		{
			ImGui::TextDisabled("Not followed (the hooks are not attached).");
			return;
		}
		// Counts per kind
		if (ImGui::BeginTable("pathcounts", 4, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_SizingFixedFit))
		{
			ImGui::TableSetupColumn("Asked by");
			ImGui::TableSetupColumn("Searches");
			ImGui::TableSetupColumn("Failed");
			ImGui::TableSetupColumn("Partial");
			ImGui::TableHeadersRow();
			for (int kind = 0; kind < NAV_DEBUG::kPathKinds; kind++)
			{
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				if (ImGui::Selectable(NAV_DEBUG::PathKindName(kind), g_pathKind == kind, ImGuiSelectableFlags_SpanAllColumns))
					g_pathKind = g_pathKind == kind ? -1 : kind;
				ImGui::TableNextColumn();
				ImGui::Text("%d", logs.counts.total[kind]);
				ImGui::TableNextColumn();
				ImGui::Text("%d", logs.counts.failed[kind]);
				ImGui::TableNextColumn();
				ImGui::Text("%d", logs.counts.partial[kind]);
			}
			ImGui::EndTable();
		}
		ImGui::TextDisabled("Click a kind to show only it (%s).", g_pathKind < 0 ? "all shown" : NAV_DEBUG::PathKindName(g_pathKind));
		const float now = NAV_DEBUG::Now();
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
		if (ImGui::BeginTable("paths", 7, flags, ImVec2(0.0f, 220.0f * DEBUG_MENU::Scale())))
		{
			ImGui::TableSetupScrollFreeze(1, 1);
			ImGui::TableSetupColumn("Age");
			ImGui::TableSetupColumn("Asked by");
			ImGui::TableSetupColumn("Result");
			ImGui::TableSetupColumn("Distance");
			ImGui::TableSetupColumn("Polygons");
			ImGui::TableSetupColumn("Classes");
			ImGui::TableSetupColumn("Nearest character");
			ImGui::TableHeadersRow();
			int shown = 0;
			for (const NAV_DEBUG::PathRecord& record : logs.paths)
			{
				if (!PathShown(record) || shown >= 300)
					continue;
				shown++;
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::PushID(static_cast<int>(record.serial));
				if (ImGui::Selectable(Format("%.1f s", now - record.time).c_str(), g_selectedPath == record.serial, ImGuiSelectableFlags_SpanAllColumns))
					g_selectedPath = g_selectedPath == record.serial ? 0 : record.serial;
				ImGui::PopID();
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(NAV_DEBUG::PathKindName(record.kind));
				ImGui::TableNextColumn();
				const ImVec4 colour = record.partial ? ImVec4(1.0f, 0.67f, 0.16f, 1.0f) : record.failed ? ImVec4(1.0f, 0.3f, 0.3f, 1.0f) : ImVec4(0.4f, 0.9f, 0.5f, 1.0f);
				ImGui::TextColored(colour, "%s", record.partial ? "partial" : record.failed ? "failed" : "ok");
				ImGui::TableNextColumn();
				ImGui::Text("%.1f m", Distance(record.originalStart, record.originalEnd));
				ImGui::TableNextColumn();
				ImGui::Text("%d", record.length);
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(NAV_DEBUG::ClassesText(record.filter.include).c_str());
				ImGui::TableNextColumn();
				float nearest = 0.0f;
				const DEBUG_MENU::Character* who = Nearest(record.originalStart, &nearest);
				if (who && nearest < 3.0f)
					ImGui::TextUnformatted(CharacterLabel(who).c_str());
				else
					ImGui::TextDisabled("-");
			}
			ImGui::EndTable();
		}
		for (const NAV_DEBUG::PathRecord& record : logs.paths)
		{
			if (record.serial != g_selectedPath)
				continue;
			ImGui::TextWrapped("Search #%u (%s): %s", record.serial, NAV_DEBUG::PathKindName(record.kind), NAV_DEBUG::StatusText(record.status).c_str());
			ImGui::TextWrapped("Asked from %.2f, %.2f, %.2f to %.2f, %.2f, %.2f", record.originalStart[0], record.originalStart[1], record.originalStart[2],
				record.originalEnd[0], record.originalEnd[1], record.originalEnd[2]);
			ImGui::TextWrapped("Start %s (polygon %d), end %s (polygon %d)", NAV_DEBUG::ReasonName(record.startReason), NAV_DEBUG::PolyIndex(record.startRef),
				NAV_DEBUG::ReasonName(record.endReason), NAV_DEBUG::PolyIndex(record.endRef));
			ImGui::TextWrapped("Reached polygon %d at %.2f, %.2f, %.2f after %d polygons", NAV_DEBUG::PolyIndex(record.lastPoly), record.resultEndPos[0], record.resultEndPos[1],
				record.resultEndPos[2], record.length);
			const NAV_DEBUG::Filter& f = record.filter;
			ImGui::TextWrapped("Filter: classes %s, link types 0x%X, height %s..%s, speed %s..%s, markup left out %s", NAV_DEBUG::ClassesText(f.include).c_str(), f.linkTypes,
				NAV_DEBUG::HeightName(f.heightMin), NAV_DEBUG::HeightName(f.heightMax), NAV_DEBUG::SpeedName(f.speedMin), NAV_DEBUG::SpeedName(f.speedMax),
				NAV_DEBUG::MarkupText(f.excludeMarkup).c_str());
			if (ImGui::SmallButton("Flood with its filter, from its start"))
			{
				g_floodCustom = true;
				g_floodFilter = f;
				const int seed = NAV_DEBUG::PolyIndex(record.startRef) >= 0 ? NAV_DEBUG::PolyIndex(record.startRef) : NAV_DEBUG::PolyAt(record.originalStart);
				Choose(seed);
				g_floodFrom = 3;
				RunFlood();
				g_mode = static_cast<float>(kByReach);
			}
		}
	}

	void DrawFloodSection(const NAV_DEBUG::Mesh& mesh)
	{
		if (!mesh.have)
		{
			ImGui::TextDisabled("No nav mesh.");
			return;
		}
		const char* classNames[kClassCount];
		for (int i = 0; i < kClassCount; i++)
			classNames[i] = kClasses[i].name;
		bool custom = g_floodCustom;
		if (custom)
		{
			const NAV_DEBUG::Filter& f = g_floodFilter;
			ImGui::TextWrapped("Using a logged search's filter: classes %s, links 0x%X, height %s..%s, markup left out %s", NAV_DEBUG::ClassesText(f.include).c_str(),
				f.linkTypes, NAV_DEBUG::HeightName(f.heightMin), NAV_DEBUG::HeightName(f.heightMax), NAV_DEBUG::MarkupText(f.excludeMarkup).c_str());
			if (ImGui::SmallButton("Use a class instead"))
				g_floodCustom = false;
		}
		else
		{
			ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
			ImGui::Combo("Class", &g_floodClass, classNames, kClassCount);
			ImGui::SameLine();
			ImGui::Checkbox("Vents (backstage)", &g_floodBackstage);
			ImGui::SameLine();
			ImGui::Checkbox("Deep crouch", &g_floodDeep);
		}
		ImGui::SetNextItemWidth(260.0f * DEBUG_MENU::Scale());
		const char* seeds[] = { "From the player", "From the crosshair", "From the camera", "From the shown polygon" };
		ImGui::Combo("Seed", &g_floodFrom, seeds, 4);
		if (ImGui::Button("Flood"))
		{
			RunFlood();
			if (static_cast<int>(g_mode) != kByIsland)
				g_mode = static_cast<float>(kByReach);
			g_polys = true;
		}
		ImGui::SameLine();
		SwitchBox("Again when the mesh changes", g_floodAuto);
		if (!g_flood.done)
		{
			TextDisabledWrapped("The game's filter for the class (as its path queries make it: standing and crouch heights, vents left out) - tick Vents and Deep crouch "
				"for the alien's and the facehugger's way round. One-way off-mesh connections are followed one way only.");
			return;
		}
		if (!FloodCurrent(mesh))
			ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "The mesh has changed since this flood.");
		ImGui::TextWrapped("%d of %d passable polygons reached from polygon %d; %d islands.", g_flood.reachedCount, g_flood.passableCount, g_flood.seed, static_cast<int>(g_flood.islands.size()));
		if (ImGui::SmallButton("Colour by reached"))
		{
			g_mode = static_cast<float>(kByReach);
			g_polys = true;
		}
		ImGui::SameLine();
		if (ImGui::SmallButton("Colour by island"))
		{
			g_mode = static_cast<float>(kByIsland);
			g_polys = true;
		}
		float centre[3] = {};
		const bool haveCentre = OverlayCentre(centre);
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
		if (ImGui::BeginTable("islands", 5, flags, ImVec2(0.0f, 180.0f * DEBUG_MENU::Scale())))
		{
			ImGui::TableSetupScrollFreeze(0, 1);
			ImGui::TableSetupColumn("Island");
			ImGui::TableSetupColumn("Polygons");
			ImGui::TableSetupColumn("Area");
			ImGui::TableSetupColumn("Centre");
			ImGui::TableSetupColumn("Distance");
			ImGui::TableHeadersRow();
			for (size_t i = 0; i < g_flood.islands.size() && i < 500; i++)
			{
				const NAV_DEBUG::Flood::Island& island = g_flood.islands[i];
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::TextColored(Colour4(i == 0 ? Rgb(70, 210, 90) : Hue(0.1f + static_cast<float>(i) * 0.618034f)), "%d%s", static_cast<int>(i), island.hasSeed ? " (seed)" : "");
				ImGui::TableNextColumn();
				ImGui::Text("%d", island.polys);
				ImGui::TableNextColumn();
				ImGui::Text("%.0f m2", island.area);
				ImGui::TableNextColumn();
				ImGui::Text("%.1f, %.1f, %.1f", island.centre[0], island.centre[1], island.centre[2]);
				ImGui::TableNextColumn();
				if (haveCentre)
					ImGui::Text("%.0f m", Distance(centre, island.centre));
			}
			ImGui::EndTable();
		}
	}

	void DrawPathTestSection(const NAV_DEBUG::Mesh& mesh)
	{
		if (!NAV_DEBUG::path_query || !NAV_DEBUG::ask_path)
		{
			ImGui::TextDisabled("Not available on this game release.");
			return;
		}
		const char* classNames[kClassCount];
		for (int i = 0; i < kClassCount; i++)
			classNames[i] = kClasses[i].name;
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		ImGui::Combo("From", &g_testFrom, kPointNames, kPointFroms);
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		ImGui::Combo("To", &g_testTo, kPointNames, kPointFroms);
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		ImGui::Combo("As", &g_testClass, classNames, kClassCount);
		ImGui::SameLine();
		ImGui::Checkbox("Vents##test", &g_testBackstage);
		ImGui::SameLine();
		ImGui::Checkbox("Deep crouch##test", &g_testDeep);
		static std::string why;
		if (ImGui::Button("Find a path") && mesh.have)
		{
			float from[3], to[3];
			std::string whyFrom, whyTo;
			if (!PointFrom(g_testFrom, from, whyFrom))
				why = "From: " + whyFrom;
			else if (!PointFrom(g_testTo, to, whyTo))
				why = "To: " + whyTo;
			else
			{
				why.clear();
				NAV_DEBUG::StartPathTest(from, to, kClasses[g_testClass].gameClass, g_testBackstage, g_testDeep);
				g_testPath = true;
			}
		}
		ImGui::SameLine();
		SwitchBox("Draw it", g_testPath);
		ImGui::SameLine();
		if (ImGui::SmallButton("Clear##test"))
			NAV_DEBUG::ClearPathTest();
		if (!why.empty())
			TextColouredWrapped(ImVec4(1.0f, 0.5f, 0.3f, 1.0f), "%s", why.c_str());
		if (!mesh.have)
			ImGui::TextDisabled("No nav mesh.");
		const NAV_DEBUG::PathTest& test = NAV_DEBUG::UpdatePathTest();
		if (test.state == 0)
		{
			TextDisabledWrapped("Runs the game's own path search (its costs, its moving of the ends onto the mesh, its string pulling) on the AI thread.");
			return;
		}
		ImGui::TextWrapped("%s (%.1f, %.1f, %.1f to %.1f, %.1f, %.1f)", test.message.c_str(), test.start[0], test.start[1], test.start[2], test.end[0], test.end[1], test.end[2]);
		if (test.state == 3)
			ImGui::Text("%s: %d points, %.1f m long", test.status == 1 ? "Found" : "Not found", static_cast<int>(test.points.size() / 3), test.length);
	}

	void DrawTab()
	{
		const NAV_DEBUG::Mesh& mesh = NAV_DEBUG::Read();
		ForgetOnNewMesh(mesh);
		const Logs& logs = CopyLogs();
		if (!mesh.level)
		{
			ImGui::TextDisabled("No level running.");
			return;
		}
		if (ImGui::CollapsingHeader("Nav mesh", ImGuiTreeNodeFlags_DefaultOpen))
			DrawStatus(mesh, logs);
		if (ImGui::CollapsingHeader("Drawing", ImGuiTreeNodeFlags_DefaultOpen))
			DrawOverlaySection();
		if (ImGui::CollapsingHeader("Polygons under the player, characters and crosshair"))
			DrawUnderSection(mesh);
		if (ImGui::CollapsingHeader("Barriers and doors"))
			DrawBarrierSection(mesh, logs);
		if (ImGui::CollapsingHeader("Off-mesh connections"))
			DrawOffMeshSection(mesh);
		if (ImGui::CollapsingHeader("Traversals"))
			DrawTraversalSection(mesh);
		if (ImGui::CollapsingHeader("Path searches (the AI's, as they finish)"))
			DrawPathLogSection(logs);
		if (ImGui::CollapsingHeader("Who can get where (flood and islands)"))
			DrawFloodSection(mesh);
		if (ImGui::CollapsingHeader("Path tester (the game's own search)"))
			DrawPathTestSection(mesh);
	}

	// ---- Commands ----

	int ParseFloats(const std::string& text, float* out, int max)
	{
		int count = 0;
		const char* at = text.c_str();
		while (*at && count < max)
		{
			char* end = nullptr;
			const float value = strtof(at, &end);
			if (end == at)
			{
				at++;
				continue;
			}
			out[count++] = value;
			at = end;
		}
		return count;
	}

	int ClassByKey(const std::string& text)
	{
		for (int i = 0; i < kClassCount; i++)
			if (text.find(kClasses[i].key) != std::string::npos)
				return i;
		return -1;
	}

	DEBUG_MENU::CommandResult Ok(const std::string& message)
	{
		return { true, message };
	}

	DEBUG_MENU::CommandResult Error(const std::string& message)
	{
		return { false, message };
	}

	void NameControls()
	{
		DEBUG_MENU::AddSwitch("nav.polys", &g_polys);
		DEBUG_MENU::AddSwitch("nav.offmesh", &g_offMesh);
		DEBUG_MENU::AddSwitch("nav.barrier_polys", &g_barrierPolys);
		DEBUG_MENU::AddSwitch("nav.under", &g_under);
		DEBUG_MENU::AddSwitch("nav.path_log", &g_pathLog);
		DEBUG_MENU::AddSwitch("nav.traversals", &g_traversals);
		DEBUG_MENU::AddSwitch("nav.labels", &g_labels);
		DEBUG_MENU::AddSwitch("nav.around_player", &g_aroundPlayer);
		DEBUG_MENU::AddSwitch("nav.failed_only", &g_failedOnly);
		DEBUG_MENU::AddSwitch("nav.path_test_draw", &g_testPath);
		DEBUG_MENU::AddSwitch("nav.flood_auto", &g_floodAuto);
		DEBUG_MENU::AddValue("nav.radius", &g_radius, 2.0f, 100.0f);
		DEBUG_MENU::AddValue("nav.height_band", &g_band, 0.5f, 50.0f);
		DEBUG_MENU::AddValue("nav.colour_mode", &g_mode, 0.0f, static_cast<float>(kColourModes - 1));
		DEBUG_MENU::AddValue("nav.max_polys", &g_maxPolys, 100.0f, 20000.0f);
		DEBUG_MENU::AddValue("nav.fill", &g_fill, 0.0f, 1.0f);

		DEBUG_MENU::AddCommand("nav.status", "the nav mesh: loaded or not, its state, header, counters (key=value lines)", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() { return Ok(StatusText(NAV_DEBUG::Read())); });
		});
		DEBUG_MENU::AddCommand("nav.poly_at", "the polygon under a point \"x,y,z\" (empty: the player; \"pick\": the crosshair) with its flags decoded", [](const std::string& argument)
		{
			return DEBUG_MENU::AnswerOnRenderThread([argument]()
			{
				const NAV_DEBUG::Mesh& mesh = NAV_DEBUG::Read();
				if (!mesh.have)
					return Error("no nav mesh");
				float point[3];
				std::string why;
				if (argument.find("pick") != std::string::npos)
				{
					if (!PointFrom(kFromPick, point, why))
						return Error(why);
				}
				else if (ParseFloats(argument, point, 3) != 3 && !PointFrom(kFromPlayer, point, why))
					return Error(why);
				float height = 0.0f;
				const int poly = NAV_DEBUG::PolyAt(point, &height);
				return Ok(Format("point=%.2f,%.2f,%.2f\n", point[0], point[1], point[2]) + (poly >= 0 ? Format("height=%.3f\n", height) : std::string()) + PolyText(mesh, poly));
			});
		});
		DEBUG_MENU::AddCommand("nav.poly", "a polygon by its number, with its flags decoded", [](const std::string& argument)
		{
			const int index = atoi(argument.c_str());
			return DEBUG_MENU::AnswerOnRenderThread([index]() { return Ok(PolyText(NAV_DEBUG::Read(), index)); });
		});
		DEBUG_MENU::AddCommand("nav.decode", "a polygon id (hex) decoded: salt, tile, polygon, and the polygon when valid", [](const std::string& argument)
		{
			const uint32_t ref = static_cast<uint32_t>(strtoul(argument.c_str(), nullptr, 16));
			return DEBUG_MENU::AnswerOnRenderThread([ref]()
			{
				const NAV_DEBUG::Mesh& mesh = NAV_DEBUG::Read();
				std::string text = DecodeText(ref);
				const int poly = NAV_DEBUG::PolyIndex(ref);
				if (poly >= 0)
					text += PolyText(mesh, poly);
				return Ok(text);
			});
		});
		DEBUG_MENU::AddCommand("nav.highlight", "show a polygon by its number (-1: none)", [](const std::string& argument)
		{
			const int index = argument.empty() ? -1 : atoi(argument.c_str());
			return DEBUG_MENU::AnswerOnRenderThread([index]()
			{
				const NAV_DEBUG::Mesh& mesh = NAV_DEBUG::Read();
				Choose(index >= 0 && index < static_cast<int>(mesh.polys.size()) ? index : -1);
				return Ok(Format("highlight=%d", g_highlight));
			});
		});
		DEBUG_MENU::AddCommand("nav.list_actors", "each NPC: the polygon its movement is on, the one under its feet, its class, backstage and deep crouch", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				NAV_DEBUG::Read();
				std::string text;
				for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
				{
					if (character.dead)
						continue;
					const int ours = NAV_DEBUG::PolyAt(character.position);
					ActorNav actor;
					if (character.player || !ReadActorNav(character.object, actor))
					{
						text += Format("%s: under its feet %d\n", CharacterLabel(&character).c_str(), ours);
						continue;
					}
					text += Format("%s: movement 0x%08X (polygon %d), under its feet %d, class %s, backstage %d, deep crouch %d, known good %d\n", CharacterLabel(&character).c_str(),
						actor.poly, NAV_DEBUG::PolyIndex(actor.poly), ours, NavigationClassName(actor.navigationClass), actor.backstage ? 1 : 0, actor.deepCrouch ? 1 : 0, actor.knownGood ? 1 : 0);
				}
				return Ok(text.empty() ? std::string("no characters") : text);
			});
		});
		DEBUG_MENU::AddCommand("nav.list_barriers", "the barrier table: area, classes now, door id, barrier entity/instance, polygons, collision row, zone", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				NAV_DEBUG::Read();
				std::string text;
				for (const NAV_DEBUG::Barrier& barrier : NAV_DEBUG::Barriers())
					text += Format("area %d classes=%s door=0x%08X entity=0x%08X instance=0x%08X polys=%d enabled=%d row=%d zone=0x%08X\n", barrier.area,
						NAV_DEBUG::ClassesText(barrier.classes).c_str(), barrier.entity, barrier.handleEntity, barrier.handleParent, barrier.polys, barrier.enabledPolys,
						barrier.collisionRow, barrier.zone);
				return Ok(text.empty() ? std::string("no barriers") : text);
			});
		});
		DEBUG_MENU::AddCommand("nav.door_audit", "nav barriers against the sound network's doorways by id, with a verdict each", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				NAV_DEBUG::Read();
				std::string text;
				for (const AuditRow& row : DoorAudit())
					text += AuditLine(row) + "\n";
				return Ok(text.empty() ? std::string("no barriers or doorways") : text);
			});
		});
		DEBUG_MENU::AddCommand("nav.list_barrier_log", "the door log: barriers' classes written since the state loaded (newest first)", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				const Logs& logs = CopyLogs();
				const float now = NAV_DEBUG::Now();
				std::string text;
				for (const NAV_DEBUG::BarrierRecord& record : logs.barriers)
					text += Format("%.1fs ago area %d %s -> %s x%d\n", now - record.time, record.area, record.before < 0 ? "?" : NAV_DEBUG::ClassesText(static_cast<uint32_t>(record.before)).c_str(),
						NAV_DEBUG::ClassesText(record.after).c_str(), record.sets);
				return Ok(text.empty() ? std::string("no changes") : text);
			});
		});
		DEBUG_MENU::AddCommand("nav.list_loads", "the nav mesh states loaded since the debug menu attached (newest first)", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				const Logs& logs = CopyLogs();
				std::string text;
				for (const NAV_DEBUG::LoadRecord& record : logs.loads)
					text += Format("state_%u at %.1f s: %s, %d polygons, %d barriers put back\n", record.state, record.time, record.loaded ? "loaded" : "NO MESH", record.polys, record.barriers);
				return Ok(text.empty() ? std::string("no loads seen") : text);
			});
		});
		DEBUG_MENU::AddCommand("nav.list_paths", "the path log, newest first (\"failed\": failed and partial only; a kind name: only it)", [](const std::string& argument)
		{
			return DEBUG_MENU::AnswerOnRenderThread([argument]()
			{
				const Logs& logs = CopyLogs();
				const float now = NAV_DEBUG::Now();
				const bool failed = argument.find("failed") != std::string::npos;
				int kind = -1;
				for (int k = 0; k < NAV_DEBUG::kPathKinds; k++)
					if (argument.find(NAV_DEBUG::PathKindName(k)) != std::string::npos)
						kind = k;
				std::string text;
				for (int k = 0; k < NAV_DEBUG::kPathKinds; k++)
					text += Format("count %s: %d searches, %d failed, %d partial\n", NAV_DEBUG::PathKindName(k), logs.counts.total[k], logs.counts.failed[k], logs.counts.partial[k]);
				int lines = 0;
				for (const NAV_DEBUG::PathRecord& record : logs.paths)
				{
					if ((failed && !record.failed && !record.partial) || (kind >= 0 && record.kind != kind) || lines >= 100)
						continue;
					lines++;
					text += PathLine(record, now) + "\n";
				}
				return Ok(text);
			});
		});
		DEBUG_MENU::AddCommand("nav.clear_logs", "clear the door log and the path log", [](const std::string&)
		{
			NAV_DEBUG::ClearLogs();
			return Ok("cleared");
		});
		DEBUG_MENU::AddCommand("nav.list_offmesh", "off-mesh connections within a distance of the camera (default 20 m), nearest first", [](const std::string& argument)
		{
			float radius = 20.0f;
			ParseFloats(argument, &radius, 1);
			return DEBUG_MENU::AnswerOnRenderThread([radius]()
			{
				const NAV_DEBUG::Mesh& mesh = NAV_DEBUG::Read();
				float centre[3];
				if (!mesh.have || !OverlayCentre(centre))
					return Error("no nav mesh or camera");
				std::string text = Format("total=%d\n", static_cast<int>(mesh.offMesh.size()));
				int lines = 0;
				for (size_t i = 0; i < mesh.offMesh.size() && lines < 200; i++)
				{
					const NAV_DEBUG::OffMesh& c = mesh.offMesh[i];
					const float d = (std::min)(Distance(centre, c.start), Distance(centre, c.end));
					if (d > radius)
						continue;
					lines++;
					const uint32_t area = c.poly >= 0 && c.poly < static_cast<int>(mesh.polys.size()) ? mesh.polys[c.poly].area : 0;
					text += Format("#%d poly %d %s %s enabled=%d admits=%s side=0x%02X radius=%.2f length=%.1f distance=%.1f traversal=%u entity=0x%08X instance=0x%08X cost=%.2f speeds=%d..%d\n",
						static_cast<int>(i), c.poly, NAV_DEBUG::LinkTypeName(NAV_DEBUG::LinkType(area)), (c.flags & 1) ? "both-ways" : "one-way", NAV_DEBUG::Enabled(area) ? 1 : 0,
						NAV_DEBUG::ClassesText(NAV_DEBUG::Admittance(area)).c_str(), c.side, c.radius, Distance(c.start, c.end), d, c.traversal, c.entity, c.parent, c.extraCost, c.minSpeed, c.maxSpeed);
				}
				return Ok(text);
			});
		});
		DEBUG_MENU::AddCommand("nav.flood", "flood for a class from the player (or \"pick\", or \"poly=N\"): \"alien[,vents][,deep]\" (player, alien, android, human, facehugger); prints the islands", [](const std::string& argument)
		{
			return DEBUG_MENU::AnswerOnRenderThread([argument]()
			{
				const int cls = ClassByKey(argument);
				if (cls >= 0)
				{
					g_floodClass = cls;
					g_floodCustom = false;
				}
				g_floodBackstage = argument.find("vent") != std::string::npos || argument.find("backstage") != std::string::npos;
				g_floodDeep = argument.find("deep") != std::string::npos;
				g_floodFrom = argument.find("pick") != std::string::npos ? kFromPick : kFromPlayer;
				const size_t poly = argument.find("poly=");
				if (poly != std::string::npos)
				{
					Choose(atoi(argument.c_str() + poly + 5));
					g_floodFrom = 3;
				}
				const std::string result = RunFlood();
				return Ok(result + "\n" + FloodText());
			});
		});
		DEBUG_MENU::AddCommand("nav.flood_from_path", "flood with a logged path search's own filter, from its start (its number, as nav.list_paths shows it after #)", [](const std::string& argument)
		{
			const uint32_t serial = static_cast<uint32_t>(strtoul(argument.c_str(), nullptr, 10));
			return DEBUG_MENU::AnswerOnRenderThread([serial]()
			{
				NAV_DEBUG::Read();
				const Logs& logs = CopyLogs();
				for (const NAV_DEBUG::PathRecord& record : logs.paths)
				{
					if (record.serial != serial)
						continue;
					g_floodCustom = true;
					g_floodFilter = record.filter;
					const int start = NAV_DEBUG::PolyIndex(record.startRef);
					Choose(start >= 0 ? start : NAV_DEBUG::PolyAt(record.originalStart));
					g_floodFrom = 3;
					const std::string result = RunFlood();
					return Ok(result + "\n" + FloodText());
				}
				return Error("no logged search with that number");
			});
		});
		DEBUG_MENU::AddCommand("nav.list_islands", "the last flood's islands (largest first)", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() { return Ok(FloodText()); });
		});
		DEBUG_MENU::AddCommand("nav.path_test", "run the game's own path search: \"alien[,vents][,deep]\" from the player to the crosshair, or \"alien,x1,y1,z1,x2,y2,z2\"; read the answer with nav.path_test_result", [](const std::string& argument)
		{
			return DEBUG_MENU::AnswerOnRenderThread([argument]()
			{
				const NAV_DEBUG::Mesh& mesh = NAV_DEBUG::Read();
				if (!mesh.have)
					return Error("no nav mesh");
				const int cls = (std::max)(0, ClassByKey(argument));
				// Numbers after the first comma (the class name has none)
				const size_t comma = argument.find(',');
				float points[6];
				const int count = comma == std::string::npos ? 0 : ParseFloats(argument.substr(comma), points, 6);
				float from[3], to[3];
				std::string why;
				if (count == 6)
				{
					memcpy(from, points, sizeof(from));
					memcpy(to, points + 3, sizeof(to));
				}
				else if (!PointFrom(kFromPlayer, from, why) || !PointFrom(kFromPick, to, why))
					return Error(why);
				g_testClass = cls;
				g_testBackstage = argument.find("vent") != std::string::npos || argument.find("backstage") != std::string::npos;
				g_testDeep = argument.find("deep") != std::string::npos;
				NAV_DEBUG::StartPathTest(from, to, kClasses[cls].gameClass, g_testBackstage, g_testDeep);
				g_testPath = true;
				return Ok(Format("asked: %s from %.2f,%.2f,%.2f to %.2f,%.2f,%.2f", kClasses[cls].name, from[0], from[1], from[2], to[0], to[1], to[2]));
			});
		});
		DEBUG_MENU::AddCommand("nav.path_test_clear", "forget the path tester's result (letting the game's result go)", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				NAV_DEBUG::ClearPathTest();
				return Ok("cleared");
			});
		});
		DEBUG_MENU::AddCommand("nav.path_test_result", "the path tester's state and result (state 0 idle, 1 asked, 2 waiting, 3 done, 4 failed to ask)", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				const NAV_DEBUG::PathTest& test = NAV_DEBUG::UpdatePathTest();
				std::string text = Format("state=%d\nmessage=%s\nstatus=%d\nlength=%.2f\npoints=%d\n", test.state, test.message.c_str(), test.status, test.length, static_cast<int>(test.points.size() / 3));
				for (size_t i = 0; i + 2 < test.points.size() && i < 3 * 64; i += 3)
					text += Format("%.2f,%.2f,%.2f\n", test.points[i], test.points[i + 1], test.points[i + 2]);
				return Ok(text);
			});
		});
		DEBUG_MENU::AddCommand("nav.list_traversals", "traversals within a distance of the camera (default 20 m): uid, type, enabled, classes, off-mesh connection, entity", [](const std::string& argument)
		{
			float radius = 20.0f;
			ParseFloats(argument, &radius, 1);
			return DEBUG_MENU::AnswerOnRenderThread([radius]()
			{
				const NAV_DEBUG::Mesh& mesh = NAV_DEBUG::Read();
				const COVER_DEBUG::Cover& cover = COVER_DEBUG::ReadCover();
				float centre[3];
				if (!cover.loaded || !OverlayCentre(centre))
					return Error("no cover and traversal data, or no camera");
				std::string text = Format("total=%d\n", static_cast<int>(cover.traversals.size()));
				for (const COVER_DEBUG::Traversal& traversal : cover.traversals)
				{
					const float d = (std::min)(Distance(centre, traversal.entrance[0]), Distance(centre, traversal.exit[0]));
					if (d > radius)
						continue;
					int connection = -1;
					for (size_t i = 0; i < mesh.offMesh.size(); i++)
						if (mesh.offMesh[i].traversal == static_cast<uint32_t>(traversal.uid))
							connection = static_cast<int>(i);
					text += Format("#%d %s enabled=%d classes=%s distance=%.1f offmesh=%d entity=0x%08X instance=0x%08X\n", traversal.uid, COVER_DEBUG::TraversalTypeName(traversal.type),
						traversal.enabled ? 1 : 0, NAV_DEBUG::ClassesText(traversal.classes).c_str(), d, connection, traversal.entity, traversal.parent);
				}
				return Ok(text);
			});
		});
		DEBUG_MENU::AddCommand("nav.player_traversal", "the player's traversal tracker: can start one, the closest, those near, the one it is on", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				const DEBUG_MENU::Character* player = Player();
				COVER_DEBUG::Tracker tracker;
				if (!player || !COVER_DEBUG::ReadTracker(player->object, tracker))
					return Error("no player");
				std::string text = Format("can_transition=%d\nclosest=%d\non=0x%08X\n", tracker.canTransition ? 1 : 0, tracker.closest, tracker.onTraversal);
				for (const COVER_DEBUG::Tracker::Near& nearby : tracker.nearby)
					text += Format("%d: entity=0x%08X ref=%u distance=%.2f\n", nearby.index, nearby.entity, nearby.ref, nearby.distance);
				return Ok(text);
			});
		});
	}

	const bool g_named = (NameControls(), true);
	// The labels at 32: after the mesh (30) and the cover (31) are drawn
	const bool g_added = DEBUG_MENU::AddTab("Navigation", 120, DrawTab, "World") && DEBUG_MENU::AddOverlay(30, DrawOverlay) && DEBUG_MENU::AddOverlay(32, DrawQueuedLabels);
}
