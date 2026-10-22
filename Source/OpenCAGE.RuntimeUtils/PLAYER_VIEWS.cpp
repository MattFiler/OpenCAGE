#include "PLAYER_DEBUG.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#include <imgui.h>

/*
	The Player tab's hiding, under-foot and objectives sections, and the Input tab (see PLAYER_DEBUG.h). All are reads,
	on the render thread, but the objective popup, which runs on the entity thread as the level scripts' own objectives
	show theirs.

	- Hiding: whether the player is hiding (the logic flag the game sets as they hide, and since when), where the AI looks
	  for the hiding player (the point the hiding place's script sets, which the alien routes to when it pulls a player
	  out), each alien's "attack the hiding player" and "the player won the hiding struggle" flags, and the alien's
	  learning about hiding (whether it searches lockers and under things yet, and how often the player hid).
	- Under foot: what the player's physics controller found under the player's feet with its four downward rays - the
	  collision map row each hit (with its entity, composite instance and zone), the zone that puts the player in, the
	  sound material the footsteps use, the physical material and what it is (walkable, climbable...), where the player
	  is supported, and whether over a drop.
	- Objectives: the primary, secondary and exit objectives as the player's inventory holds them (text keys and the text
	  in the game's language), the motion tracker's route to its objective (the navigation mesh polygons the tracker's
	  path goes through, drawn through their centres), and a test of the objective popup.
	- Input: what the game thinks is pressed - every action of the player's (or the menus', or the game's debug)
	  input with its state, each analogue value, and whether the input and movement are let through - also as an overlay
	  for playing with it on (the window keeps the keyboard and mouse while open, so the game sees nothing then).
*/
namespace
{
	using namespace PLAYER_DEBUG;

	// A character's: position, the zone it stands in (and whether that zone is visible), the model and region it stands
	// on, how long it has been off the ground, what it is standing on (walkable, climbable...), the sound material and
	// physical material under its feet, whether it is in water, and its physics controller's index (-1: none)
	constexpr uint32_t kCharacterPosition = 0x70;
	constexpr uint32_t kZoneUnderFoot = 0x1B0;
	constexpr uint32_t kZoneVisible = 0x1B4;
	constexpr uint32_t kStoodOnModel = 0x1A8;
	constexpr uint32_t kStoodInRegion = 0x1AC;
	constexpr uint32_t kOffGroundTime = 0x1D0;
	constexpr uint32_t kMechanicUnderFoot = 0x1D8;
	constexpr uint32_t kSoundMaterial = 0x1E4;
	constexpr uint32_t kInWater = 0x1E8;
	constexpr uint32_t kPhysicalMaterial = 0x1EC;
	constexpr uint32_t kControllerHandle = 0x60;
	// A sound material: its index, then its name (80 characters)
	constexpr uint32_t kSoundMaterialName = 0x0C;

	// The physics controllers (one per character, its character first): the collision map rows the four rays under the
	// feet hit (-1: none), the zones those rows are in, where the character is supported, its last height on the ground,
	// the drop below its feet, whether the rays have answered, and whether it is over a drop
	constexpr uintptr_t kControllers = 0x01307A48;
	constexpr int kMaxControllers = 18;
	constexpr uint32_t kControllerSize = 0xB0;
	constexpr uint32_t kRayRows = 0x60;
	constexpr uint32_t kRayZones = 0x70;
	constexpr uint32_t kSupport = 0x80;
	constexpr uint32_t kLastHeight = 0x90;
	constexpr uint32_t kDropBelow = 0x94;
	constexpr uint32_t kRaysValid = 0xAB;
	constexpr uint32_t kOverDrop = 0xAC;

	// The level's collision map rows as loaded (a pointer to them, then how many there is room for), 0x30 bytes each:
	// flags, the entity, the composite instance, and the zone
	constexpr uintptr_t kCollisionRows = 0x01308838;
	constexpr uint32_t kRowSize = 0x30;
	constexpr uint32_t kRowFlags = 0x00;
	constexpr uint32_t kRowEntity = 0x0C;
	constexpr uint32_t kRowComposite = 0x10;
	constexpr uint32_t kRowZone = 0x1C;

	// The physical materials (a pointer to them, then how many), 0x340 bytes each, the name first
	constexpr uintptr_t kPhysicalMaterials = 0x0134A3E0;
	constexpr uint32_t kPhysicalMaterialSize = 0x340;

	// Hiding: the player's behaviour (inside the character) notes when they started hiding; the logic flags; the point
	// the AI looks for a hiding player from; and in the globals the alien's learning (a pointer): its abilities, how often
	// it searched for the player, how often the player hid from it and how often in a deep crouch
	constexpr uint32_t kHidingSince = 0x33E0 + 0xB4;
	constexpr int kFlagInVent = 0x03;
	constexpr int kFlagHiding = 0x0D;
	constexpr int kFlagAttackHiding = 0x0E;
	constexpr int kFlagWonHidingStruggle = 0x15;
	constexpr uintptr_t kHidingPosition = 0x012B18A8;
	constexpr uint32_t kGlobalsLearning = 0xA8;
	constexpr uint32_t kLearningAbilities = 0x04;
	constexpr uint32_t kLearningSearched = 0x0C;
	constexpr uint32_t kLearningHid = 0x10;
	constexpr uint32_t kLearningHidDeepCrouch = 0x14;
	constexpr uint32_t kAbilitySearchLockers = 0x80;
	constexpr uint32_t kAbilitySearchUnder = 0x100;
	constexpr int kAlienClass = 1;

	// Objectives, in the player's inventory: the primary (a title key of 128 characters, a description of 256, then
	// whether completed), 10 secondaries (0x118 each: title and description keys of 128, a position, a marker type, the
	// level, whether completed, whether it has a position), then the exit objective (as a secondary) and whether it is set
	constexpr uint32_t kObjectives = 0x2DB4;
	constexpr uint32_t kPrimaryCompleted = 0x180;
	constexpr uint32_t kPrimarySize = 0x184;
	constexpr uint32_t kSecondaries = 0x184;
	constexpr int kSecondaryCount = 10;
	constexpr uint32_t kSecondarySize = 0x118;
	constexpr uint32_t kSecondaryDescription = 0x80;
	constexpr uint32_t kSecondaryPosition = 0x100;
	constexpr uint32_t kSecondaryCompleted = 0x114;
	constexpr uint32_t kSecondaryHasPosition = 0x116;
	constexpr uint32_t kExitObjective = 0xC74;
	constexpr uint32_t kExitSet = 0xD90;

	// The motion tracker's route (a pointer in the player's behaviour): the objective's position, the marker's target,
	// the distance, whether the path was found, the path (navigation mesh polygon references) and its length, and
	// whether there is an objective
	constexpr uint32_t kTrackerRoute = 0x33E0 + 0x120;
	constexpr uint32_t kRouteObjective = 0x0C;
	constexpr uint32_t kRouteMarker = 0x24;
	constexpr uint32_t kRouteDistance = 0x40;
	constexpr uint32_t kRouteFound = 0x4C;
	constexpr uint32_t kRoutePath = 0x50;
	constexpr uint32_t kRouteLength = 0x450;
	constexpr uint32_t kRouteHasObjective = 0x455;
	constexpr int kMaxRoute = 256;

	// The navigation mesh (a pointer in the globals): its lock first, the mesh at +0x18; the mesh's tiles and how a
	// polygon reference splits into salt, tile and polygon; a tile's header (polygon count at +0x18), polygons and
	// vertices; a polygon's vertex indices and vertex count
	constexpr uint32_t kGlobalsNavMesh = 0x24;
	constexpr uint32_t kNavMeshMesh = 0x18;
	constexpr uint32_t kMeshMaxTiles = 0x30;
	constexpr uint32_t kMeshTiles = 0x44;
	constexpr uint32_t kMeshTileBits = 0x4C;
	constexpr uint32_t kMeshPolyBits = 0x50;
	constexpr uint32_t kTileSize = 0x40;
	constexpr uint32_t kTileHeader = 0x08;
	constexpr uint32_t kTilePolys = 0x0C;
	constexpr uint32_t kTileVerts = 0x14;
	constexpr uint32_t kHeaderPolyCount = 0x18;
	constexpr uint32_t kPolySize = 0x24;
	constexpr uint32_t kPolyVerts = 0x04;
	constexpr uint32_t kPolyVertCount = 0x1C;

	// The input system (a pointer): the player's, the menus' and the game's debug input; each has its actions (0x40
	// each: the state byte at +0x38, the name at +0x3C), its analogue values (0x44 each: the value at +0x38), how many of
	// each, and whether it is enabled. The player's also: movement let through, and locked by the interface
	constexpr uintptr_t kInputSystem = 0x01359B44;
	constexpr uint32_t kInputs[3] = { 0x48, 0x2998, 0x3A0C };
	constexpr uint32_t kInputButtons = 0x04;
	constexpr uint32_t kButtonSize = 0x40;
	constexpr uint32_t kButtonState = 0x38;
	constexpr uint32_t kButtonName = 0x3C;
	constexpr int kMaxButtons = 41;
	constexpr uint32_t kInputAnalogs = 0xA44;
	constexpr uint32_t kAnalogSize = 0x44;
	constexpr uint32_t kAnalogValue = 0x38;
	constexpr int kMaxAnalogs = 16;
	constexpr uint32_t kInputButtonCount = 0x1054;
	constexpr uint32_t kInputAnalogCount = 0x1058;
	constexpr uint32_t kInputEnabled = 0x105C;
	constexpr uint32_t kInputMovement = 0x294E;
	constexpr uint32_t kInputLockedByUi = 0x294F;
	// An action's state bits: pressed this frame, held, released this frame, toggled, enabled, and the button or key
	// itself held down (whether or not the game lets it through)
	constexpr uint8_t kPressed = 0x01, kHeld = 0x02, kReleased = 0x04, kToggled = 0x08, kEnabled = 0x10, kDown = 0x80;

	// The interface's layers (a pointer); the objective popups (the game's text lookup is PLAYER_DEBUG::Translated)
	constexpr uintptr_t kLayers = 0x0134A78C;
	typedef void*(__thiscall* t_get_layer)(void* layers, const char* name);
	const auto get_layer = reinterpret_cast<t_get_layer>(DEVTOOLS_RELATIVE_ADDRESS(0x00410420));
	typedef void(__thiscall* t_show_popup)(void* hub, const void* objective);
	const auto show_primary_popup = reinterpret_cast<t_show_popup>(DEVTOOLS_RELATIVE_ADDRESS(0x004271F0));
	const auto show_secondary_popup = reinterpret_cast<t_show_popup>(DEVTOOLS_RELATIVE_ADDRESS(0x00427290));

	std::atomic<bool> g_hidingOverlay = false;
	std::atomic<bool> g_underFootOverlay = false;
	std::atomic<bool> g_routeOverlay = false;
	std::atomic<bool> g_inputOverlay = false;

	bool ReadVector(const void* at, float out[3])
	{
		for (int i = 0; i < 3; i++)
			if (!Read(static_cast<const uint8_t*>(at) + i * 4, out[i]))
				return false;
		return std::isfinite(out[0]) && std::isfinite(out[1]) && std::isfinite(out[2]);
	}

	// ---- Hiding ----

	struct Hiding
	{
		bool read = false, hiding = false, inVent = false;
		float since = -1.0f;
		bool havePosition = false;
		float position[3] = {};
		bool haveLearning = false;
		uint32_t abilities = 0;
		int searched = 0, hid = 0, hidDeepCrouch = 0;
	};

	Hiding ReadHiding(const uint8_t* player)
	{
		Hiding hiding;
		if (!player)
			return hiding;
		hiding.read = true;
		hiding.hiding = LogicFlag(player, kFlagHiding);
		hiding.inVent = LogicFlag(player, kFlagInVent);
		if (hiding.hiding)
			Read(player + kHidingSince, hiding.since);
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kHidingPosition);
		hiding.havePosition = at && ReadVector(reinterpret_cast<const void*>(at), hiding.position) &&
			(hiding.position[0] != 0.0f || hiding.position[1] != 0.0f || hiding.position[2] != 0.0f);
		const uint8_t* learning = PointerAt(Globals(), kGlobalsLearning);
		hiding.haveLearning = learning && Read(learning + kLearningAbilities, hiding.abilities) && Read(learning + kLearningSearched, hiding.searched) &&
			Read(learning + kLearningHid, hiding.hid) && Read(learning + kLearningHidDeepCrouch, hiding.hidDeepCrouch);
		return hiding;
	}

	void DrawHiding()
	{
		ImGui::SeparatorText("Hiding");
		const uint8_t* player = Player();
		const Hiding hiding = ReadHiding(player);
		if (!hiding.read)
		{
			ImGui::TextDisabled("No player (no level running).");
			return;
		}
		if (hiding.hiding)
		{
			const double now = GameTime();
			if (hiding.since > 0.0f && now >= hiding.since)
				ImGui::Text("Hiding, for %.1f s%s", now - hiding.since, hiding.inVent ? " (in a vent)" : "");
			else
				ImGui::Text("Hiding%s", hiding.inVent ? " (in a vent)" : "");
		}
		else
			ImGui::Text("Not hiding%s", hiding.inVent ? " (in a vent)" : "");
		if (hiding.havePosition)
		{
			float at[3] = {};
			ReadVector(player + kCharacterPosition, at);
			const float dx = hiding.position[0] - at[0], dy = hiding.position[1] - at[1], dz = hiding.position[2] - at[2];
			ImGui::TextWrapped("Where the AI looks for a hiding player: %.2f, %.2f, %.2f (%.1f m away)", hiding.position[0], hiding.position[1], hiding.position[2], std::sqrt(dx * dx + dy * dy + dz * dz));
			Tooltip("Set by the script of the hiding place the player last used (a locker, under a desk...): the alien routes to it when it comes to pull the player out.");
		}
		else
			ImGui::TextDisabled("No hiding place used yet in this level.");
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		{
			if (character.characterClass != kAlienClass || character.dead)
				continue;
			const bool attack = LogicFlag(character.object, kFlagAttackHiding);
			const bool won = LogicFlag(character.object, kFlagWonHidingStruggle);
			ImGui::Text("%s: %s%s", character.name.c_str(), attack ? "going for the hiding player" : "not going for the hiding player", won ? "; the player won the struggle" : "");
		}
		if (hiding.haveLearning)
		{
			ImGui::TextWrapped("The alien has learnt: %s, %s", (hiding.abilities & kAbilitySearchLockers) ? "searches lockers" : "doesn't search lockers yet",
				(hiding.abilities & kAbilitySearchUnder) ? "searches under things" : "doesn't search under things yet");
			WrappedDisabled("The player hid %d times (%d in a deep crouch); the alien searched %d times", hiding.hid, hiding.hidDeepCrouch, hiding.searched);
			Tooltip("The alien's learning: its abilities unlock from these counts (sooner on harder difficulties). The game keeps them in memory only, never in a save (Characters > Alien).");
		}
		bool overlay = g_hidingOverlay;
		if (ImGui::Checkbox("Draw where the AI looks for the hiding player", &overlay))
			g_hidingOverlay = overlay;
	}

	// ---- Under foot ----

	struct UnderFoot
	{
		bool read = false, haveController = false, raysValid = false, overDrop = false, inWater = false, zoneVisible = false;
		uint32_t zone = 0, region = 0;
		int model = -1, mechanic = 0, physicalMaterial = -1;
		std::string soundMaterial, physicalMaterialName;
		float offGround = 0.0f, lastHeight = 0.0f, dropBelow = 0.0f;
		float support[3] = {};
		int rows[4] = { -1, -1, -1, -1 };
		uint32_t rayZones[4] = {};
		uint32_t rowFlags[4] = {}, rowEntity[4] = {}, rowComposite[4] = {}, rowZone[4] = {};
	};

	const char* MechanicName(int mechanic)
	{
		switch (mechanic)
		{
		case 0: return "nothing";
		case 1: return "pressurised";
		case 2: return "walkable";
		case 3: return "climbable";
		default: return "?";
		}
	}

	UnderFoot ReadUnderFoot(const uint8_t* player)
	{
		UnderFoot foot;
		if (!player)
			return foot;
		foot.read = Read(player + kZoneUnderFoot, foot.zone);
		Read(player + kZoneVisible, foot.zoneVisible);
		Read(player + kStoodOnModel, foot.model);
		Read(player + kStoodInRegion, foot.region);
		Read(player + kOffGroundTime, foot.offGround);
		Read(player + kMechanicUnderFoot, foot.mechanic);
		Read(player + kInWater, foot.inWater);
		Read(player + kPhysicalMaterial, foot.physicalMaterial);
		const uint8_t* sound = PointerAt(player, kSoundMaterial);
		if (sound)
			foot.soundMaterial = Text(sound + kSoundMaterialName, 79);
		const uintptr_t materials = DEVTOOLS_RELATIVE_ADDRESS(kPhysicalMaterials);
		const uint8_t* materialList = materials ? PointerAt(reinterpret_cast<const void*>(materials), 0) : nullptr;
		uint32_t materialCount = 0;
		if (materialList && foot.physicalMaterial >= 0 && Read(reinterpret_cast<const uint8_t*>(materials) + 4, materialCount) &&
			static_cast<uint32_t>(foot.physicalMaterial) < materialCount)
			foot.physicalMaterialName = Text(materialList + foot.physicalMaterial * kPhysicalMaterialSize, 49);

		int handle = -1;
		const uintptr_t controllers = DEVTOOLS_RELATIVE_ADDRESS(kControllers);
		if (!controllers || !Read(player + kControllerHandle, handle) || handle < 0 || handle >= kMaxControllers)
			return foot;
		const uint8_t* controller = reinterpret_cast<const uint8_t*>(controllers) + handle * kControllerSize;
		if (PointerAt(controller, 0) != player)
			return foot;
		foot.haveController = true;
		Read(controller + kRaysValid, foot.raysValid);
		Read(controller + kOverDrop, foot.overDrop);
		Read(controller + kLastHeight, foot.lastHeight);
		Read(controller + kDropBelow, foot.dropBelow);
		ReadVector(controller + kSupport, foot.support);
		const uintptr_t rowsAt = DEVTOOLS_RELATIVE_ADDRESS(kCollisionRows);
		const uint8_t* rows = rowsAt ? PointerAt(reinterpret_cast<const void*>(rowsAt), 0) : nullptr;
		uint32_t rowCount = 0;
		if (rowsAt)
			Read(reinterpret_cast<const uint8_t*>(rowsAt) + 4, rowCount);
		for (int i = 0; i < 4; i++)
		{
			Read(controller + kRayRows + i * 4, foot.rows[i]);
			Read(controller + kRayZones + i * 4, foot.rayZones[i]);
			if (!rows || foot.rows[i] < 0 || static_cast<uint32_t>(foot.rows[i]) >= rowCount)
				continue;
			const uint8_t* row = rows + foot.rows[i] * kRowSize;
			Read(row + kRowFlags, foot.rowFlags[i]);
			Read(row + kRowEntity, foot.rowEntity[i]);
			Read(row + kRowComposite, foot.rowComposite[i]);
			Read(row + kRowZone, foot.rowZone[i]);
		}
		return foot;
	}

	std::string ZoneText(uint32_t zone)
	{
		char text[32];
		if (zone == 0xFFFFFFFF)
			return "none";
		snprintf(text, sizeof(text), "%08X", zone);
		return text;
	}

	void DrawUnderFoot()
	{
		ImGui::SeparatorText("Under foot");
		const uint8_t* player = Player();
		const UnderFoot foot = ReadUnderFoot(player);
		if (!foot.read)
		{
			ImGui::TextDisabled("No player (no level running).");
			return;
		}
		ImGui::Text("Footsteps: %s", foot.soundMaterial.empty() ? "(no sound material)" : foot.soundMaterial.c_str());
		Tooltip("The sound material the footsteps use: the physics controller sets it from the render material on the collision under the player's feet (its physical material index, mapped to a sound material).");
		if (foot.physicalMaterial >= 0)
			ImGui::Text("Physical material %d%s%s, %s", foot.physicalMaterial, foot.physicalMaterialName.empty() ? "" : ": ", foot.physicalMaterialName.c_str(), MechanicName(foot.mechanic));
		ImGui::Text("Zone under foot: %s (%s)%s", ZoneText(foot.zone).c_str(), foot.zoneVisible ? "visible" : "not visible", foot.inWater ? "; in water" : "");
		Tooltip("The zone the collision under the player's feet puts them in: the game shows that zone (the feet are a second viewpoint for zone streaming, next to the camera).");
		if (foot.offGround > 0.0f)
			ImGui::Text("Off the ground for %.2f s", foot.offGround);
		if (!foot.haveController)
		{
			ImGui::TextDisabled("No physics controller.");
			return;
		}
		ImGui::TextWrapped("Supported at %.2f, %.2f, %.2f; last height on the ground %.2f; drop below the feet %.2f m%s", foot.support[0], foot.support[1], foot.support[2],
			foot.lastHeight, foot.dropBelow, foot.overDrop ? " - over a drop" : "");
		if (!foot.raysValid)
			ImGui::TextDisabled("The rays under the feet haven't answered yet.");
		if (ImGui::BeginTable("##rays", 5, ImGuiTableFlags_RowBg | ImGuiTableFlags_SizingStretchProp))
		{
			ImGui::TableSetupColumn("Ray");
			ImGui::TableSetupColumn("Collision row");
			ImGui::TableSetupColumn("Entity");
			ImGui::TableSetupColumn("Composite instance");
			ImGui::TableSetupColumn("Zone");
			ImGui::TableHeadersRow();
			for (int i = 0; i < 4; i++)
			{
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("%d", i + 1);
				ImGui::TableNextColumn();
				if (foot.rows[i] < 0)
				{
					ImGui::TextDisabled("no hit (or the global zone)");
					continue;
				}
				ImGui::Text("%d (flags %08X)", foot.rows[i], foot.rowFlags[i]);
				// An id of 0: the row names no entity or composite instance
				ImGui::TableNextColumn();
				if (foot.rowEntity[i])
					ImGui::Text("%08X", foot.rowEntity[i]);
				else
					ImGui::TextDisabled("none");
				ImGui::TableNextColumn();
				if (foot.rowComposite[i])
					ImGui::Text("%08X", foot.rowComposite[i]);
				else
					ImGui::TextDisabled("none");
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(ZoneText(foot.rayZones[i]).c_str());
			}
			ImGui::EndTable();
		}
		Tooltip("Each ray's row of the level's collision map (COLLISION.MAP): the entity and composite instance it belongs to, and its zone. A hit in the level's always-loaded zone counts as no hit for the zone.");
		bool overlay = g_underFootOverlay;
		if (ImGui::Checkbox("Draw the support point and the surface", &overlay))
			g_underFootOverlay = overlay;
	}

	// ---- Objectives ----

	struct Objective
	{
		bool set = false, completed = false, hasPosition = false;
		std::string title, description;
		float position[3] = {};
	};

	Objective ReadObjective(const uint8_t* at, bool primary)
	{
		Objective objective;
		objective.title = Text(at, 127);
		objective.description = Text(at + 0x80, primary ? 255 : 127);
		objective.set = !objective.title.empty();
		Read(at + (primary ? kPrimaryCompleted : kSecondaryCompleted), objective.completed);
		if (!primary)
		{
			Read(at + kSecondaryHasPosition, objective.hasPosition);
			if (objective.hasPosition)
				ReadVector(at + kSecondaryPosition, objective.position);
		}
		return objective;
	}

	struct Route
	{
		bool read = false, hasObjective = false, found = false;
		float objective[3] = {}, marker[3] = {};
		float distance = 0.0f;
		uint32_t length = 0;
		uint32_t path[kMaxRoute] = {};
	};

	Route ReadRoute(const uint8_t* player)
	{
		Route route;
		const uint8_t* data = PointerAt(player, kTrackerRoute);
		if (!data)
			return route;
		route.read = true;
		Read(data + kRouteHasObjective, route.hasObjective);
		Read(data + kRouteFound, route.found);
		ReadVector(data + kRouteObjective, route.objective);
		ReadVector(data + kRouteMarker, route.marker);
		Read(data + kRouteDistance, route.distance);
		Read(data + kRouteLength, route.length);
		route.length = (std::min)(route.length, static_cast<uint32_t>(kMaxRoute));
		for (uint32_t i = 0; i < route.length; i++)
			Read(data + kRoutePath + i * 4, route.path[i]);
		return route;
	}

	// The centre of each polygon on a route, read under the navigation mesh's own lock (skipped when it is busy); how many
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

	int PolygonCentres(const uint8_t* mesh, const uint32_t* path, int count, float (*out)[3])
	{
		int found = 0;
		__try
		{
			const uint8_t* tiles = *reinterpret_cast<uint8_t* const*>(mesh + kMeshTiles);
			const int maxTiles = *reinterpret_cast<const int*>(mesh + kMeshMaxTiles);
			const uint32_t tileBits = *reinterpret_cast<const uint32_t*>(mesh + kMeshTileBits);
			const uint32_t polyBits = *reinterpret_cast<const uint32_t*>(mesh + kMeshPolyBits);
			if (!tiles || tileBits > 31 || polyBits > 31)
				return 0;
			for (int i = 0; i < count; i++)
			{
				const uint32_t tileIndex = (path[i] >> polyBits) & ((1u << tileBits) - 1);
				const uint32_t polyIndex = path[i] & ((1u << polyBits) - 1);
				if (!path[i] || static_cast<int>(tileIndex) >= maxTiles)
					continue;
				const uint8_t* tile = tiles + tileIndex * kTileSize;
				const uint8_t* header = *reinterpret_cast<uint8_t* const*>(tile + kTileHeader);
				const uint8_t* polys = *reinterpret_cast<uint8_t* const*>(tile + kTilePolys);
				const float* verts = *reinterpret_cast<float* const*>(tile + kTileVerts);
				if (!header || !polys || !verts || polyIndex >= *reinterpret_cast<const uint32_t*>(header + kHeaderPolyCount))
					continue;
				const uint8_t* poly = polys + polyIndex * kPolySize;
				const int vertCount = (std::min)(6, static_cast<int>(poly[kPolyVertCount]));
				if (vertCount <= 0)
					continue;
				float centre[3] = {};
				for (int v = 0; v < vertCount; v++)
				{
					const uint16_t index = reinterpret_cast<const uint16_t*>(poly + kPolyVerts)[v];
					for (int k = 0; k < 3; k++)
						centre[k] += verts[index * 3 + k];
				}
				for (int k = 0; k < 3; k++)
					out[found][k] = centre[k] / vertCount;
				found++;
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
		return found;
	}

	int RouteCentres(const Route& route, float (*out)[3])
	{
		uint8_t* navMesh = PointerAt(Globals(), kGlobalsNavMesh);
		if (!navMesh || !route.length)
			return 0;
		CRITICAL_SECTION* lock = reinterpret_cast<CRITICAL_SECTION*>(navMesh);
		if (!TryLock(lock))
			return 0;
		const uint8_t* mesh = PointerAt(navMesh, kNavMeshMesh);
		const int count = mesh ? PolygonCentres(mesh, route.path, static_cast<int>(route.length), out) : 0;
		LeaveCriticalSection(lock);
		return count;
	}

	// Entity thread: the popup the game shows as an objective is set or completed, through its own interface layer
	bool ShowPopup(const void* objective, bool primary)
	{
		const uintptr_t layersAt = DEVTOOLS_RELATIVE_ADDRESS(kLayers);
		__try
		{
			void* layers = layersAt ? *reinterpret_cast<void**>(layersAt) : nullptr;
			void* hub = layers ? get_layer(layers, "hub") : nullptr;
			if (!hub)
				return false;
			(primary ? show_primary_popup : show_secondary_popup)(hub, objective);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool PopupAvailable()
	{
		return get_layer && show_primary_popup && show_secondary_popup && DEVTOOLS_RELATIVE_ADDRESS(kLayers);
	}

	// Entity thread: the popup for the current primary objective, a secondary one (index), or one with this text
	DEBUG_MENU::CommandResult Popup(int secondary, const std::string& text)
	{
		if (!PopupAvailable())
			return { false, "not available on this game release" };
		const uint8_t* inventory = Inventory();
		if (!inventory)
			return { false, "no player inventory (no level running)" };
		bool shown = false;
		std::string what;
		if (!text.empty())
		{
			// A primary objective of its own (the title is looked up as a text key, as the game's are). The popup puts its
			// text through the interface's formatter, which reads an argument for each "[0]"-style placeholder in it (or in
			// what the key translates to) while holding the game's string lock, and is given none: such text is refused
			uint8_t objective[kPrimarySize] = {};
			strncpy_s(reinterpret_cast<char*>(objective), 128, text.c_str(), _TRUNCATE);
			const std::string title = reinterpret_cast<const char*>(objective);
			const std::string translated = Translated(title);
			if ((translated.empty() ? title : translated).find("[0") != std::string::npos)
				return { false, "the popup can't show text with a [0] placeholder (the game would read values that aren't there)" };
			shown = ShowPopup(objective, true);
			what = "\"" + text + "\"";
		}
		else if (secondary >= 0)
		{
			if (secondary >= kSecondaryCount)
				return { false, "secondary objectives are 0 to 9" };
			const uint8_t* at = inventory + kObjectives + kSecondaries + secondary * kSecondarySize;
			if (!ReadObjective(at, false).set)
				return { false, "secondary objective " + std::to_string(secondary) + " isn't set" };
			shown = ShowPopup(at, false);
			what = "secondary objective " + std::to_string(secondary);
		}
		else
		{
			const uint8_t* at = inventory + kObjectives;
			if (!ReadObjective(at, true).set)
				return { false, "no primary objective is set" };
			shown = ShowPopup(at, true);
			what = "the primary objective";
		}
		const std::string result = shown ? "showed the popup for " + what : "the popup could not be shown (no interface)";
		SetStatus(StatusArea::Objectives, result);
		return { shown, result };
	}

	char g_popupText[128] = {};

	void DrawObjectives()
	{
		ImGui::SeparatorText("Objectives");
		const uint8_t* player = Player();
		const uint8_t* inventory = Inventory();
		if (!player || !inventory)
		{
			ImGui::TextDisabled("No player (no level running).");
			return;
		}
		const bool canPopup = PopupAvailable();
		// An objective's title, with its popup button on the same line (popup: -1 the primary's, a secondary's index, or
		// below -1 for none) so the button stays in view when the description under it wraps
		auto line = [canPopup](const char* what, const Objective& objective, int popup)
		{
			const std::string title = Translated(objective.title);
			ImGui::Text("%s: %s%s", what, title.empty() ? objective.title.c_str() : title.c_str(), objective.completed ? " (completed)" : "");
			if (popup >= -1)
			{
				ImGui::SameLine();
				ImGui::PushID(popup);
				ImGui::BeginDisabled(!canPopup);
				if (ImGui::SmallButton("Popup"))
					DEBUG_MENU::OnEntityThread([popup]() { Popup(popup, std::string()); });
				ImGui::EndDisabled();
				ImGui::PopID();
			}
			if (!title.empty())
			{
				const std::string description = Translated(objective.description);
				ImGui::Indent();
				WrappedDisabled("%s / %s%s%s", objective.title.c_str(), objective.description.c_str(), description.empty() ? "" : ": ", description.c_str());
				ImGui::Unindent();
			}
		};
		const Objective primary = ReadObjective(inventory + kObjectives, true);
		if (primary.set)
			line("Primary", primary, -1);
		else
			ImGui::TextDisabled("No primary objective.");
		for (int i = 0; i < kSecondaryCount; i++)
		{
			const Objective objective = ReadObjective(inventory + kObjectives + kSecondaries + i * kSecondarySize, false);
			if (!objective.set)
				continue;
			char what[32];
			snprintf(what, sizeof(what), "Secondary %d", i);
			line(what, objective, i);
			if (objective.hasPosition)
			{
				ImGui::Indent();
				ImGui::TextDisabled("at %.2f, %.2f, %.2f", objective.position[0], objective.position[1], objective.position[2]);
				ImGui::Unindent();
			}
		}
		bool exitSet = false;
		Read(inventory + kObjectives + kExitSet, exitSet);
		if (exitSet)
			line("Exit", ReadObjective(inventory + kObjectives + kExitObjective, false), -2);

		const Route route = ReadRoute(player);
		if (route.read && route.hasObjective)
		{
			ImGui::TextWrapped("Motion tracker: objective at %.1f, %.1f, %.1f, %.1f m by its route%s", route.objective[0], route.objective[1], route.objective[2], route.distance,
				route.found ? "" : " (no route found)");
			ImGui::Indent();
			WrappedDisabled("marker at %.1f, %.1f, %.1f; route through %u navigation mesh polygons", route.marker[0], route.marker[1], route.marker[2], route.length);
			ImGui::Unindent();
		}
		else
			ImGui::TextDisabled("The motion tracker has no objective.");
		bool overlay = g_routeOverlay;
		if (ImGui::Checkbox("Draw the motion tracker's route", &overlay))
			g_routeOverlay = overlay;
		Tooltip("The route the motion tracker's objective arrow follows: through the centres of the navigation mesh polygons its path finds, to the marker.");

		ImGui::SetNextItemWidth(260.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##popup", "text or text key", g_popupText, sizeof(g_popupText));
		ImGui::SameLine();
		ImGui::BeginDisabled(!canPopup || !g_popupText[0]);
		if (ImGui::Button("Show the objective popup"))
		{
			const std::string text = g_popupText;
			DEBUG_MENU::OnEntityThread([text]() { Popup(-1, text); });
		}
		ImGui::EndDisabled();
		Tooltip("Shows the game's \"objective updated\" popup with this as its title (a text key, as the level scripts' objectives use, shows in the game's language). The objectives themselves are left as they are.");
	}

	// ---- Input ----

	struct Action
	{
		std::string name;
		uint8_t state = 0;
	};

	struct InputView
	{
		bool read = false, enabled = false, movement = false, lockedByUi = false;
		std::vector<Action> actions;
		float analogs[kMaxAnalogs] = {};
		int analogCount = 0;
	};

	InputView ReadInput(int which)
	{
		InputView view;
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kInputSystem);
		const uint8_t* system = at ? PointerAt(reinterpret_cast<const void*>(at), 0) : nullptr;
		if (!system || which < 0 || which > 2)
			return view;
		const uint8_t* input = system + kInputs[which];
		uint32_t buttons = 0, analogs = 0;
		if (!Read(input + kInputButtonCount, buttons) || !Read(input + kInputAnalogCount, analogs))
			return view;
		view.read = true;
		Read(input + kInputEnabled, view.enabled);
		if (which == 0)
		{
			Read(input + kInputMovement, view.movement);
			Read(input + kInputLockedByUi, view.lockedByUi);
		}
		for (uint32_t i = 0; i < buttons && i < kMaxButtons; i++)
		{
			const uint8_t* button = input + kInputButtons + i * kButtonSize;
			Action action;
			Read(button + kButtonState, action.state);
			action.name = TextAt(button, kButtonName, 63);
			if (action.name.empty())
				action.name = "action " + std::to_string(i);
			view.actions.push_back(action);
		}
		view.analogCount = static_cast<int>((std::min)(analogs, static_cast<uint32_t>(kMaxAnalogs)));
		for (int i = 0; i < view.analogCount; i++)
			Read(input + kInputAnalogs + i * kAnalogSize + kAnalogValue, view.analogs[i]);
		return view;
	}

	std::string StateText(uint8_t state)
	{
		std::string text;
		if (state & kPressed) text += "pressed ";
		if (state & kHeld) text += "held ";
		if (state & kReleased) text += "released ";
		if (state & kToggled) text += "toggled ";
		if ((state & kDown) && !(state & kHeld)) text += "down but not let through ";
		if (!(state & kEnabled)) text += "(disabled)";
		return text;
	}

	int g_inputWhich = 0;
	bool g_inputAll = false;
	const char* const kInputNames[3] = { "Player", "Menus", "Debug" };

	void DrawInputTab()
	{
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		ImGui::Combo("Input", &g_inputWhich, kInputNames, 3);
		ImGui::SameLine();
		ImGui::Checkbox("Every action", &g_inputAll);
		const InputView view = ReadInput(g_inputWhich);
		if (!view.read)
		{
			ImGui::TextDisabled("No input system.");
			return;
		}
		ImGui::Text("Input %s%s%s", view.enabled ? "enabled" : "disabled", g_inputWhich == 0 ? (view.movement ? ", movement let through" : ", movement held back") : "",
			g_inputWhich == 0 && view.lockedByUi ? ", locked by the interface" : "");
		WrappedDisabled("While this window is open it has the keyboard and mouse, so the game sees nothing from them: use the overlay, or a pad.");
		if (ImGui::BeginTable("##actions", 2, ImGuiTableFlags_RowBg | ImGuiTableFlags_SizingStretchProp))
		{
			for (const Action& action : view.actions)
			{
				const bool active = (action.state & (kPressed | kHeld | kReleased | kDown)) != 0;
				if (!g_inputAll && !active)
					continue;
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				if (active)
					ImGui::TextColored(ImVec4(0.4f, 1.0f, 0.5f, 1.0f), "%s", action.name.c_str());
				else
					ImGui::TextUnformatted(action.name.c_str());
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(StateText(action.state).c_str());
			}
			ImGui::EndTable();
		}
		int analogsShown = 0;
		for (int i = 0; i < view.analogCount; i++)
			if (g_inputAll || std::fabs(view.analogs[i]) > 0.001f)
			{
				ImGui::Text("Analogue %d: %.3f", i, view.analogs[i]);
				analogsShown++;
			}
		// (said, so a tab with nothing moving doesn't look as if it reads nothing)
		if (view.analogCount > 0 && analogsShown == 0)
			ImGui::TextDisabled("%d analogue input(s), all at rest (Every action lists them).", view.analogCount);
		bool overlay = g_inputOverlay;
		if (ImGui::Checkbox("Show what is pressed over the game", &overlay))
			g_inputOverlay = overlay;
	}

	// ---- Overlays ----

	void DrawOverlay(ImDrawList* drawList)
	{
		if (!g_hidingOverlay && !g_underFootOverlay && !g_routeOverlay && !g_inputOverlay)
			return;
		const uint8_t* player = Player();
		float feet[3] = {};
		const bool havePlayer = player && ReadVector(player + kCharacterPosition, feet);

		if (g_hidingOverlay && havePlayer)
		{
			const Hiding hiding = ReadHiding(player);
			if (hiding.havePosition)
			{
				const ImU32 colour = hiding.hiding ? IM_COL32(255, 120, 60, 255) : IM_COL32(170, 170, 170, 200);
				DEBUG_MENU::WorldCross(drawList, hiding.position, 0.3f, colour, 2.0f);
				DEBUG_MENU::WorldLine(drawList, feet, hiding.position, colour, 1.0f);
				DEBUG_MENU::WorldText(drawList, hiding.position, "where the AI looks for the hiding player", colour, -14.0f);
			}
			if (hiding.hiding)
				DEBUG_MENU::WorldCircle(drawList, feet, 0.6f, IM_COL32(255, 120, 60, 255));
		}

		if (g_underFootOverlay && havePlayer)
		{
			const UnderFoot foot = ReadUnderFoot(player);
			if (foot.haveController)
			{
				const ImU32 colour = foot.overDrop ? IM_COL32(255, 80, 60, 255) : IM_COL32(90, 220, 255, 255);
				DEBUG_MENU::WorldCross(drawList, foot.support, 0.2f, colour, 2.0f);
				std::string label = foot.soundMaterial.empty() ? std::string("(no sound material)") : foot.soundMaterial;
				if (!foot.physicalMaterialName.empty())
					label += " / " + foot.physicalMaterialName;
				label += "  zone " + ZoneText(foot.zone);
				if (foot.overDrop)
					label += "  over a drop";
				DEBUG_MENU::WorldText(drawList, foot.support, label.c_str(), colour, 14.0f);
			}
		}

		if (g_routeOverlay && havePlayer)
		{
			static Route route;
			route = ReadRoute(player);
			if (route.read && route.hasObjective)
			{
				static float centres[kMaxRoute][3];
				const int count = RouteCentres(route, centres);
				const ImU32 colour = route.found ? IM_COL32(120, 255, 120, 230) : IM_COL32(255, 200, 60, 230);
				const float* last = feet;
				for (int i = 0; i < count; i++)
				{
					DEBUG_MENU::WorldLine(drawList, last, centres[i], colour, 2.0f);
					last = centres[i];
				}
				DEBUG_MENU::WorldLine(drawList, last, route.marker, colour, 2.0f);
				DEBUG_MENU::WorldCross(drawList, route.marker, 0.4f, colour, 2.0f);
				DEBUG_MENU::WorldText(drawList, route.marker, "motion tracker marker", colour, -14.0f);
				DEBUG_MENU::WorldCross(drawList, route.objective, 0.4f, IM_COL32(255, 255, 255, 200), 1.5f);
			}
		}

		if (g_inputOverlay)
		{
			const InputView view = ReadInput(0);
			if (view.read)
			{
				std::string text = std::string("Input") + (view.enabled ? "" : " (disabled)") + (view.movement ? "" : " (movement held back)") + (view.lockedByUi ? " (locked by the interface)" : "");
				for (const Action& action : view.actions)
					if (action.state & (kPressed | kHeld | kReleased | kDown))
						text += "\n" + action.name + "  " + StateText(action.state);
				for (int i = 0; i < view.analogCount; i++)
					if (std::fabs(view.analogs[i]) > 0.001f)
					{
						char line[48];
						snprintf(line, sizeof(line), "\nanalogue %d  %.2f", i, view.analogs[i]);
						text += line;
					}
				const ImVec2 size = ImGui::CalcTextSize(text.c_str());
				const ImVec2 display = ImGui::GetIO().DisplaySize;
				const ImVec2 at(display.x - size.x - 20.0f, display.y * 0.35f);
				drawList->AddRectFilled(ImVec2(at.x - 6.0f, at.y - 4.0f), ImVec2(at.x + size.x + 6.0f, at.y + size.y + 4.0f), IM_COL32(0, 0, 0, 160), 4.0f);
				drawList->AddText(at, IM_COL32(230, 230, 230, 255), text.c_str());
			}
		}
	}

	// ---- Named switches and commands ----

	bool g_registered = false;

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddSwitch("player.hiding_overlay", &g_hidingOverlay);
		DEBUG_MENU::AddSwitch("player.underfoot_overlay", &g_underFootOverlay);
		DEBUG_MENU::AddSwitch("player.objective_route", &g_routeOverlay);
		DEBUG_MENU::AddSwitch("player.input_overlay", &g_inputOverlay);

		DEBUG_MENU::AddCommand("player.hiding", "hiding: whether the player hides, where the AI looks, the aliens' flags, the alien's learning", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				const uint8_t* player = Player();
				const Hiding hiding = ReadHiding(player);
				if (!hiding.read)
					return { false, "no player" };
				char text[512];
				snprintf(text, sizeof(text), "hiding=%d\nin_vent=%d\nsince=%.2f\nhiding_position=%s\nabilities=0x%X\nplayer_hid=%d\nhid_deep_crouch=%d\nalien_searched=%d",
					hiding.hiding ? 1 : 0, hiding.inVent ? 1 : 0, hiding.since, hiding.havePosition ? Vector(hiding.position).c_str() : "none",
					hiding.abilities, hiding.hid, hiding.hidDeepCrouch, hiding.searched);
				std::string result = text;
				for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
					if (character.characterClass == kAlienClass && !character.dead)
						result += "\nalien=" + character.name + " attack_hiding=" + (LogicFlag(character.object, kFlagAttackHiding) ? "1" : "0") +
							" player_won_struggle=" + (LogicFlag(character.object, kFlagWonHidingStruggle) ? "1" : "0");
				return { true, result };
			});
		});
		DEBUG_MENU::AddCommand("player.underfoot", "what is under the player's feet: sound and physical material, zone, the four rays' collision rows", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				const UnderFoot foot = ReadUnderFoot(Player());
				if (!foot.read)
					return { false, "no player" };
				char text[512];
				snprintf(text, sizeof(text), "sound_material=%s\nphysical_material=%d %s\nmechanic=%s\nzone=%s\nzone_visible=%d\nin_water=%d\nsupport=%s\nover_drop=%d\ndrop_below=%.2f\nrays_valid=%d",
					foot.soundMaterial.c_str(), foot.physicalMaterial, foot.physicalMaterialName.c_str(), MechanicName(foot.mechanic), ZoneText(foot.zone).c_str(),
					foot.zoneVisible ? 1 : 0, foot.inWater ? 1 : 0, Vector(foot.support).c_str(), foot.overDrop ? 1 : 0, foot.dropBelow, foot.raysValid ? 1 : 0);
				std::string result = text;
				for (int i = 0; i < 4; i++)
				{
					char line[160];
					snprintf(line, sizeof(line), "\nray%d=row %d entity %08X composite %08X zone %s", i + 1, foot.rows[i], foot.rowEntity[i], foot.rowComposite[i], ZoneText(foot.rayZones[i]).c_str());
					result += line;
				}
				return { true, result };
			});
		});
		DEBUG_MENU::AddCommand("player.list_objectives", "the objectives: primary, secondary 0-9, exit (key | text | completed), and the motion tracker's route", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				const uint8_t* player = Player();
				const uint8_t* inventory = Inventory();
				if (!player || !inventory)
					return { false, "no player" };
				std::string text;
				auto add = [&text](const std::string& what, const Objective& objective)
				{
					if (objective.set)
						text += what + "=" + objective.title + " | " + Translated(objective.title) + " | " + (objective.completed ? "1" : "0") + "\n";
				};
				add("primary", ReadObjective(inventory + kObjectives, true));
				for (int i = 0; i < kSecondaryCount; i++)
					add("secondary" + std::to_string(i), ReadObjective(inventory + kObjectives + kSecondaries + i * kSecondarySize, false));
				bool exitSet = false;
				if (Read(inventory + kObjectives + kExitSet, exitSet) && exitSet)
					add("exit", ReadObjective(inventory + kObjectives + kExitObjective, false));
				const Route route = ReadRoute(player);
				if (route.read && route.hasObjective)
				{
					char line[200];
					snprintf(line, sizeof(line), "tracker_objective=%s\ntracker_marker=%s\ntracker_distance=%.2f\ntracker_route_found=%d\ntracker_route_polygons=%u\n",
						Vector(route.objective).c_str(), Vector(route.marker).c_str(), route.distance, route.found ? 1 : 0, route.length);
					text += line;
				}
				return { true, text.empty() ? "(no objectives)" : text };
			});
		});
		DEBUG_MENU::AddCommand("player.objective_popup", "show the objective popup: \"\" for the primary, \"secondary N\", or any text (key)", [](const std::string& argument)
		{
			int secondary = -1;
			std::string text = argument;
			if (argument.rfind("secondary ", 0) == 0)
			{
				secondary = atoi(argument.c_str() + 10);
				text.clear();
			}
			return DEBUG_MENU::AnswerOnEntityThread([secondary, text]() { return Popup(secondary, text); });
		});
		DEBUG_MENU::AddCommand("player.list_input", "what the game thinks is pressed: \"[player|menus|debug] [all]\" - action state, analogue values", [](const std::string& argument)
		{
			const int which = argument.find("menu") != std::string::npos ? 1 : argument.find("debug") != std::string::npos ? 2 : 0;
			const bool all = argument.find("all") != std::string::npos;
			return DEBUG_MENU::AnswerOnRenderThread([which, all]() -> DEBUG_MENU::CommandResult
			{
				const InputView view = ReadInput(which);
				if (!view.read)
					return { false, "no input system" };
				std::string text = std::string("enabled=") + (view.enabled ? "1" : "0");
				if (which == 0)
					text += std::string("\nmovement=") + (view.movement ? "1" : "0") + "\nlocked_by_ui=" + (view.lockedByUi ? "1" : "0");
				for (const Action& action : view.actions)
					if (all || (action.state & (kPressed | kHeld | kReleased | kDown)))
					{
						char state[8];
						snprintf(state, sizeof(state), "%02X", action.state);
						text += "\n" + action.name + "=" + state + " " + StateText(action.state);
					}
				for (int i = 0; i < view.analogCount; i++)
					if (all || std::fabs(view.analogs[i]) > 0.001f)
					{
						char line[48];
						snprintf(line, sizeof(line), "\nanalogue%d=%.3f", i, view.analogs[i]);
						text += line;
					}
				return { true, text };
			});
		});
	}

	void Overlay(ImDrawList* drawList)
	{
		Register();
		DrawOverlay(drawList);
	}

	void DrawHidingSection() { Register(); DrawHiding(); }
	void DrawUnderFootSection() { Register(); DrawUnderFoot(); }
	void DrawObjectivesSection() { Register(); DrawObjectives(); DrawStatus(StatusArea::Objectives); }
	void DrawInput() { Register(); DrawInputTab(); }

	const bool g_added = DEBUG_MENU::AddTab("Player", 31, DrawHidingSection, "Characters") && DEBUG_MENU::AddTab("Player", 32, DrawUnderFootSection, "Characters") &&
		DEBUG_MENU::AddTab("Player", 33, DrawObjectivesSection, "Characters") && DEBUG_MENU::AddTab("Input", 37, DrawInput, "Characters") &&
		DEBUG_MENU::AddOverlay(45, Overlay);
}
