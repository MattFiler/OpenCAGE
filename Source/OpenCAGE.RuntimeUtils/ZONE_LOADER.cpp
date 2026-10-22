#include "ZONE_LOADER.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <atomic>

namespace
{
	// A bit per source that wants more zones in (set when the DLL loads, on the camera task and from other tools' threads)
	std::atomic<uint32_t> g_sources = 0;

	constexpr uint32_t kEveryZone = static_cast<uint32_t>(ZONE_LOADER::Source::Config);
	constexpr uint32_t kAroundCamera = static_cast<uint32_t>(ZONE_LOADER::Source::External)
		| static_cast<uint32_t>(ZONE_LOADER::Source::LiveCamera) | static_cast<uint32_t>(ZONE_LOADER::Source::Possess)
		| static_cast<uint32_t>(ZONE_LOADER::Source::Capture) | static_cast<uint32_t>(ZONE_LOADER::Source::Cinematic);

	char* MainView()
	{
		char* views = static_cast<char*>(*ZONE_LOADER::view_manager_instance);
		return views ? *reinterpret_cast<char**>(views + ZONE_LOADER::kMainViewOffset) : nullptr;
	}

	void RegisterEveryZone(char* zoneManager)
	{
		char* zones = *reinterpret_cast<char**>(zoneManager + ZONE_LOADER::kZoneArrayOffset);
		if (!zones)
			return;

		const int count = *reinterpret_cast<int*>(zones + ZONE_LOADER::kArrayCountOffset);
		char** allocations = *reinterpret_cast<char***>(zones + ZONE_LOADER::kArrayDataOffset);
		if (!allocations)
			return;

		for (int i = 0; i < count; i++)
		{
			char* allocation = allocations[i];
			if (!allocation)
				continue;
			char* zone = *reinterpret_cast<char**>(allocation + ZONE_LOADER::kAllocationObjectOffset);
			if (!zone)
				continue;
			ZONE_LOADER::add_viewpoint(zoneManager, ZONE_LOADER::kPlayerViewer, *reinterpret_cast<unsigned int*>(zone + ZONE_LOADER::kZoneIdOffset));
		}
	}
}

void ZONE_LOADER::SetForced(Source source, bool forced)
{
	if (forced)
		g_sources |= static_cast<uint32_t>(source);
	else
		g_sources &= ~static_cast<uint32_t>(source);
}

bool ZONE_LOADER::IsForced()
{
	return g_sources != 0;
}

__declspec(noinline)
void __cdecl ZONE_LOADER::h_match_current_zones_to_povs()
{
	const uint32_t sources = g_sources;

	// The main view is handed the zone of the character its camera follows (the player), wherever the camera itself
	// is, so only the player's surroundings stream in. While a camera is away from the player, clear that for this
	// frame's check - and the flag that would then leave the view with no zone at all (the player's camera has it) -
	// so the game finds the zone below the camera, as it does for its other views, and the zones around the camera
	// stream in. Both flags are put back afterwards. The zone it was handed is kept in as well, for the ground under
	// the player.
	unsigned int handedZone = kNoZone;
	uint8_t* zoneFlags = nullptr;
	uint8_t* cameraFlags = nullptr;
	uint8_t clearedZoneFlag = 0, clearedCameraFlag = 0;
	if (sources & kAroundCamera)
	{
		if (char* view = MainView())
		{
			zoneFlags = reinterpret_cast<uint8_t*>(view + kViewZoneFlagsOffset);
			cameraFlags = reinterpret_cast<uint8_t*>(view + kViewCameraFlagsOffset);
			clearedZoneFlag = *zoneFlags & kViewZoneHandedInFlag;
			clearedCameraFlag = *cameraFlags & kViewNoZoneOfItsOwnFlag;
			if (clearedZoneFlag)
				handedZone = *reinterpret_cast<unsigned int*>(view + kViewZoneOffset);
			*zoneFlags &= ~kViewZoneHandedInFlag;
			*cameraFlags &= ~kViewNoZoneOfItsOwnFlag;
		}
	}

	match_current_zones_to_povs();

	if (zoneFlags)
	{
		*zoneFlags |= clearedZoneFlag;
		*cameraFlags |= clearedCameraFlag;
	}

	if (!(sources & (kEveryZone | kAroundCamera)))
		return;
	char* zoneManager = static_cast<char*>(*zone_manager_instance);
	if (!zoneManager)
		return;
	if (handedZone != kNoZone)
		add_viewpoint(zoneManager, kPlayerViewer, handedZone);
	if (sources & kEveryZone)
		RegisterEveryZone(zoneManager);
}

// For other tools injected into the game, to switch on while their own camera is on. It only switches their own
// request: LoadAllZones and the Live Link camera keep theirs.
extern "C" __declspec(dllexport) void OpenCAGE_SetForceZoneLoading(bool forced)
{
	ZONE_LOADER::SetForced(ZONE_LOADER::Source::External, forced);
}
