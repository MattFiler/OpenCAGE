#pragma once

#include "DevTools.h"
#include <cstdint>

namespace ZONE_LOADER
{
	inline void** zone_manager_instance = reinterpret_cast<void**>(DEVTOOLS_RELATIVE_ADDRESS(0x0134f860));

	typedef void(__thiscall* t_add_viewpoint)(void*, unsigned int, unsigned int);
	inline auto add_viewpoint = reinterpret_cast<t_add_viewpoint>(DEVTOOLS_RELATIVE_ADDRESS(0x005456c0));

	typedef void(__cdecl* t_match_current_zones_to_povs)();
	inline auto match_current_zones_to_povs = reinterpret_cast<t_match_current_zones_to_povs>(DEVTOOLS_RELATIVE_ADDRESS(0x0034c870));
	void __cdecl h_match_current_zones_to_povs();

	// The renderer's views of the level: the main camera's view is held here
	inline void** view_manager_instance = reinterpret_cast<void**>(DEVTOOLS_RELATIVE_ADDRESS(0x01366a00));

	constexpr uintptr_t kZoneArrayOffset = 0x18;
	constexpr uintptr_t kArrayCountOffset = 0x8;
	constexpr uintptr_t kArrayDataOffset = 0xc;
	constexpr uintptr_t kAllocationObjectOffset = 0xc;
	constexpr uintptr_t kZoneIdOffset = 0xc;
	constexpr unsigned int kPlayerViewer = 0;
	constexpr unsigned int kNoZone = 0xffffffff;

	constexpr uintptr_t kMainViewOffset = 0x8c;        // in the view manager: the main camera's view
	constexpr uintptr_t kViewZoneOffset = 0x330;       // in a view: the zone it is in (streamed in around it)
	constexpr uintptr_t kViewZoneFlagsOffset = 0x36c;  // in a view: a byte of flags about that zone
	// Set in that byte (again before every frame's zone check) while the camera takes its zone from the character it
	// follows, usually the player, rather than from where the camera is
	constexpr uint8_t kViewZoneHandedInFlag = 0x80;
	constexpr uintptr_t kViewCameraFlagsOffset = 0x36d; // in a view: a byte of flags taken from its camera
	// Set in that byte (again every frame) from a camera that never expires, as the player's does: a view with it that
	// is not handed a zone is given none, instead of the zone below the camera
	constexpr uint8_t kViewNoZoneOfItsOwnFlag = 0x01;

	// Who wants more than the player's surroundings streamed in. Each switches only its own request on and off, so
	// Cinematic Tools turning its camera off does not cancel LoadAllZones or the Live Link camera.
	// LoadAllZones streams in every zone of the level. The others are cameras away from the player: they get the zones
	// around the camera (as the player gets the zones around them), not every zone - all of a big level's zones at once
	// can be more than the game has memory for, and then no more of them finish loading.
	enum class Source : uint32_t
	{
		Config = 1 << 0,     // LoadAllZones in OpenCAGE.RuntimeUtils.ini
		External = 1 << 1,   // other injected tools, through this DLL's exported zone loading switch (Cinematic Tools, while its camera is on)
		LiveCamera = 1 << 2, // the Live Link camera sync, while the game renders from OpenCAGE's camera (LIVE_CAMERA.h)
		Possess = 1 << 3,    // the camera behind a possessed character (POSSESS.h)
	};

	void SetForced(Source source, bool forced);
	bool IsForced();
}
