#include "LIVE_ANIMATION.h"
#include "DevTools.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <atomic>
#include <clocale>
#include <cmath>
#include <cstdio>
#include <mutex>
#include <string>

namespace
{
	// The drive OpenCAGE asked for: written by the socket thread, read by the entity thread, under g_driveMutex
	std::mutex g_driveMutex;
	LIVE_ANIMATION::Drive g_drive;
	uint32_t g_generation = 0;         // bumped by every request, so the entity thread only drops the drive it looked at
	std::atomic<bool> g_wanted = false; // whether a drive is stored (read every entity frame without the lock)
	// Refusals are logged once, not for every request OpenCAGE sends while it is being refused
	std::string g_lastRefusal;

	// What the entity thread did, as of its last frame: written by it, read by the socket thread, under g_snapshotMutex
	std::mutex g_snapshotMutex;
	LIVE_ANIMATION::Snapshot g_snapshot;
	uint32_t g_frame = 0;               // bumped by every frame that wrote the snapshot
	std::atomic<bool> g_driving = false;

	constexpr const char* kOtherLevel = "The game is running a different level - save, and load this one in the game";

	enum class Level { None, Other, Same };

	// Plain reads of the entity manager (fine from the socket thread), guarded: it can go while a level unloads
	Level RunningLevel(uint32_t root)
	{
		__try
		{
			if (!LIVE_LINK::LevelRunning())
				return Level::None;
			return LIVE_LINK::RunningLevelIs(root) ? Level::Same : Level::Other;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return Level::None;
		}
	}

	// Called under g_driveMutex
	LIVE_LINK::Result RefuseLocked(const std::string& message)
	{
		if (message != g_lastRefusal)
			DevTools::Log("LiveLink: animation refused: %s", message.c_str());
		g_lastRefusal = message;
		return LIVE_LINK::Result{ false, message };
	}

	// Called under g_driveMutex: the drive is dropped (the entity thread gives back what it took for it on its next frame)
	void ReleaseLocked()
	{
		g_drive.mode = LIVE_ANIMATION::Release;
		g_generation++;
		g_wanted = false;
	}

	// The snapshot as ANIMATION_GET answers it: as of the last frame - but while another level than OpenCAGE's runs, that
	// is why anything it asked for waits
	std::string SnapshotText(uint32_t root)
	{
		LIVE_ANIMATION::Snapshot snapshot;
		uint32_t frame;
		{
			std::lock_guard<std::mutex> lock(g_snapshotMutex);
			snapshot = g_snapshot;
			frame = g_frame;
		}
		if (snapshot.state != LIVE_ANIMATION::State::Released && RunningLevel(root) == Level::Other)
		{
			snapshot.state = LIVE_ANIMATION::State::Waiting;
			snapshot.reason = kOtherLevel;
		}
		// Invariant whatever locale the process sets: OpenCAGE parses it with the invariant culture
		static const _locale_t invariant = _create_locale(LC_NUMERIC, "C");
		char numbers[256];
		_snprintf_s_l(numbers, sizeof(numbers), _TRUNCATE, "time=%.4f\nlength=%.4f\nsequence=%u\nframe=%u\ninstances=%u/%u\nwas_playing=%d", invariant,
			snapshot.time, snapshot.length, snapshot.sequence, frame, snapshot.applied, snapshot.found, snapshot.wasPlaying ? 1 : 0);
		return std::string("state=") + LIVE_ANIMATION::StateName(snapshot.state) + "\nreason=" + snapshot.reason + "\n" + numbers;
	}
}

const char* LIVE_ANIMATION::StateName(State state)
{
	switch (state)
	{
	case State::Released: return "released";
	case State::Waiting: return "waiting";
	case State::Held: return "held";
	case State::Playing: return "playing";
	case State::Ended: return "ended";
	case State::NotFound: return "not_found";
	case State::NotAnimation: return "not_animation";
	case State::Disabled: return "disabled";
	case State::NoData: return "no_data";
	case State::Cinematic: return "cinematic";
	}
	return "released";
}

LIVE_LINK::Result LIVE_ANIMATION::SetDrive(const Drive& drive, uint32_t pathCount)
{
	{
		std::lock_guard<std::mutex> lock(g_driveMutex);
		if (drive.mode == Release)
		{
			// Taken whatever is running: the entity thread gives the animation back on its next frame that may edit (or forgets
			// it, if its level has gone)
			ReleaseLocked();
			g_lastRefusal.clear();
		}
		else
		{
			if (drive.mode > Play)
				return RefuseLocked("Unknown animation mode " + std::to_string(drive.mode) + " (0 gives the animation back, 1 holds it, 2 plays it)");
			if (pathCount > kMaxPath)
				return RefuseLocked("The instance path is too long (" + std::to_string(pathCount) + " steps; at most " + std::to_string(kMaxPath) + " are taken)");
			if (!std::isfinite(drive.time) || !std::isfinite(drive.rate))
				return RefuseLocked("Malformed request (the time and rate must be finite numbers)");
			// A drive for another level from the connection holding one means OpenCAGE has moved on to that level: the one it
			// sent for this one is stale whether or not the new one can be taken
			if (g_drive.mode != Release && g_drive.connection == drive.connection && g_drive.root != drive.root)
				ReleaseLocked();
			// Not refused while no level runs, or while edits must wait: stored, and carried out once its level is played
			if (RunningLevel(drive.root) == Level::Other)
				return RefuseLocked(kOtherLevel);
			g_drive = drive;
			g_generation++;
			g_wanted = true;
			g_lastRefusal.clear();
		}
	}
	return LIVE_LINK::Result{ true, SnapshotText(drive.root) };
}

LIVE_LINK::Result LIVE_ANIMATION::Refuse(const std::string& message)
{
	std::lock_guard<std::mutex> lock(g_driveMutex);
	return RefuseLocked(message);
}

LIVE_LINK::Result LIVE_ANIMATION::GetState(uint32_t root)
{
	return LIVE_LINK::Result{ true, SnapshotText(root) };
}

bool LIVE_ANIMATION::Driving()
{
	return g_driving;
}

bool LIVE_ANIMATION::Wanted()
{
	return g_wanted;
}

uint32_t LIVE_ANIMATION::CurrentDrive(Drive& drive)
{
	std::lock_guard<std::mutex> lock(g_driveMutex);
	drive = g_drive;
	return g_generation;
}

void LIVE_ANIMATION::DropDrive(uint32_t generation)
{
	std::lock_guard<std::mutex> lock(g_driveMutex);
	if (g_generation == generation)
		ReleaseLocked();
}

void LIVE_ANIMATION::Publish(const Snapshot& snapshot)
{
	std::lock_guard<std::mutex> lock(g_snapshotMutex);
	g_snapshot = snapshot;
	g_frame++;
	g_driving = snapshot.taken;
}
