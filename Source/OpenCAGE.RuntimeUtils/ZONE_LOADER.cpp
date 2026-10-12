#include "ZONE_LOADER.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <atomic>

namespace
{
	// A bit per source that wants every zone in (set when the DLL loads, on the camera task and from other tools' threads)
	std::atomic<uint32_t> g_sources = 0;
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
	match_current_zones_to_povs();

	if (!IsForced())
		return;

	char* zoneManager = static_cast<char*>(*zone_manager_instance);
	if (!zoneManager)
		return;
	char* zones = *reinterpret_cast<char**>(zoneManager + kZoneArrayOffset);
	if (!zones)
		return;

	const int count = *reinterpret_cast<int*>(zones + kArrayCountOffset);
	char** allocations = *reinterpret_cast<char***>(zones + kArrayDataOffset);
	if (!allocations)
		return;

	for (int i = 0; i < count; i++)
	{
		char* allocation = allocations[i];
		if (!allocation)
			continue;
		char* zone = *reinterpret_cast<char**>(allocation + kAllocationObjectOffset);
		if (!zone)
			continue;
		add_viewpoint(zoneManager, kPlayerViewer, *reinterpret_cast<unsigned int*>(zone + kZoneIdOffset));
	}
}

// For other tools injected into the game (Cinematic Tools toggles this with its free camera). It only switches their
// own request: LoadAllZones and the live link camera keep theirs.
extern "C" __declspec(dllexport) void OpenCAGE_SetForceZoneLoading(bool forced)
{
	ZONE_LOADER::SetForced(ZONE_LOADER::Source::External, forced);
}
