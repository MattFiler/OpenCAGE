#include "CINEMATIC_VISUALS.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"
#include "RENDER_COMMON.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cfloat>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The Cinematic group's Screenshots tab, its second part: the game's own screen capture. The game keeps one (a level's
	video capture script entity can start an image sequence with it, though no shipped level has one) and offers it
	nowhere; here it is asked for as that entity asks, on the entity thread.

	Asked for a capture, the game saves the next frames it draws, at the end of its 2D drawing - so with its HUD (unless
	hidden), and never with anything RuntimeUtils draws over the game: the frame after the request readies its buffers,
	the next is saved (a sequence: every frame from then on, until stopped), and the start of the level's next update
	finishes it. What it accepts is a table it fills in at start-up, read here; the PC release's is:
	- a still: standard (.tga), HDR (Radiance .hdr) or both, at Low, Medium, High or Ultra;
	- an environment map: standard, HDR or both, at Low only;
	- an image sequence: standard at Low only.
	Only those are offered.

	Low saves one frame. Medium, High and Ultra save 2x2, 4x4 or 8x8 frames, named "_tile_x_y": the game works out a
	sub-pixel offset for each (meant for a supersampled picture put together from them afterwards), but nothing in this
	release reads the offset, so the frames come out alike. For them the game also turns off the film grain, motion blur,
	bloom, depth of field, its anti-aliasing and the ambient occlusion's blur, and draws the ambient occlusion at full
	resolution: a clean picture. For every capture it turns off its exposure scaling (the frame is drawn at a fixed scale
	instead of the one the eye's adaptation sets), its frame-rate text and its profilers; while a still or an environment
	map is saved it holds the AI, the characters and the physics (a sequence lets them run). Finishing, it puts the
	frame-rate text and the profilers back as they were, but turns the rest back on to fixed values (on, anti-aliasing mode
	2, the ambient occlusion at half resolution), losing the graphics settings' own choices. So what they were is noted on
	the entity thread just before asking, and put back once the game has finished, where its finish changed them.

	HDR: the game copies the presented picture (8 or 10 bits a channel) into a half-float image; Direct3D 11 doesn't copy
	between those formats, so on PC the .hdr is expected to come out black (to be checked in game). "Both" is asked for
	here as an HDR capture then a standard one, as the game does it itself - but the game's own second request drops the
	file name, so it is made here, with the name kept.

	Environment map: this release has nothing that turns the camera to a cube's faces for it, so it saves the view as it
	is, like a Low still, numbered by a count of its own (Live Link's cube map capture takes the six faces).

	Files: the game writes them relative to its working directory (the game folder, the way every launcher starts it) and
	makes the folders in the name. Each is named so that it lands in the Screenshots folder the tab's first part uses,
	after the level and the time: a still "<level>_<time>_<n>.tga" (n the game's own count, "_tile_x_y" added at Medium
	and above), an environment map "<level>_<time>_envmap_<n>", a sequence a folder of its own, "<level>_<time>_sequence",
	of "frame_<i>.tga" from 0. Should the working directory not be the game folder, they land in a Screenshots folder there
	instead, and the result says so. The names are kept short: the game keeps 128 characters of a name.

	Image sequence: each frame's file is written as the frame is drawn, so the game runs slowly while it records. "Lock the
	time step while recording" sets the Game group's fixed time step ("game.fixed_step_fps") for the recording, so every
	frame is the same step of game time and the frames play back smoothly at that rate however slowly they were drawn; it
	is put back after (unless changed meanwhile). The game's stop is one flag its frame end reads, so it is set from the
	render thread, which works while the level's scripts are held too; a sequence is stopped when the level changes or
	unloads.

	"Hide the HUD for the shot" (the first part's switch) hides the HUD for these too: held hidden from a few frames before
	the request until the capture is finished (for a sequence, the whole recording).

	Names: switch "cinematic.game_sequence_lock_step"; values "cinematic.game_capture_quality" (0 Low, 1 Medium, 2 High,
	3 Ultra), "cinematic.game_capture_type" (0 standard, 1 HDR, 2 both), "cinematic.game_sequence_fps"; commands
	"cinematic.game_capture", "cinematic.game_sequence_start", "cinematic.game_sequence_stop",
	"cinematic.game_capture_status".
*/
namespace
{
	// The game's screen capture (a static object), what asks it for a capture, and what stops a sequence
	constexpr uintptr_t kCapture = 0x01358900;
	constexpr uintptr_t kRequest = 0x005ABE00;
	constexpr uintptr_t kStopSequence = 0x0059F8E0;
	// In it: its platform part (a pointer; that holds the table of accepted combinations - type, quality and mode, four
	// bytes each - as a pointer at +4 and a count at +8); the capture's type, quality, mode and state (0 ready, 1 asked
	// for, 2 under way, 3 saved and waiting for the level's update to finish it); the file it writes (as last named); a
	// sequence's frames saved and its stop flag
	constexpr uint32_t kPlatform = 0x00;
	constexpr uint32_t kPlatformTable = 0x04;
	constexpr uint32_t kPlatformTableCount = 0x08;
	constexpr uint32_t kType = 0x04;
	constexpr uint32_t kQuality = 0x08;
	constexpr uint32_t kMode = 0x0C;
	constexpr uint32_t kState = 0x10;
	constexpr uint32_t kFile = 0x43;
	constexpr uint32_t kFrames = 0x144;
	constexpr uint32_t kStopFlag = 0x148;
	constexpr uint32_t kCaptureSize = 0x14C;
	constexpr uint32_t kNameLength = 0x80;
	constexpr uint32_t kMaxTable = 64;
	constexpr int kStateReady = 0;
	const char* const kStateNames[] = { "ready", "asked for", "under way", "saved, finishing" };

	// The game's modes; its qualities (here by index: Low, Medium, High, Ultra; the game's numbers are the frames a side);
	// its types
	constexpr int kModeStill = 0, kModeEnvMap = 1, kModeSequence = 3;
	const int kQualities[] = { 1, 2, 4, 8 };
	constexpr int kQualityCount = 4;
	const char* const kQualityNames[] = { "low", "medium", "high", "ultra" };
	const char* const kQualityLabels[] = { "Low: one frame", "Medium: 4 frames, clean", "High: 16 frames, clean", "Ultra: 64 frames, clean" };
	constexpr int kTypeStandard = 0, kTypeHdr = 1, kTypeBoth = 2;
	constexpr int kTypeCount = 3;
	const char* const kTypeNames[] = { "standard", "hdr", "both" };
	const char* const kTypeLabels[] = { "Standard (.tga)", "HDR (.hdr)", "Both" };

	// The game's flow states a capture is started in (gameplay) and stopped at (unloading)
	constexpr int kGameplay = 4;
	constexpr int kUnloading = 7;

	// What the game turns off for a capture and back on to fixed values when it finishes, in the post-processing object
	// (and its G-buffer manager's ambient occlusion), with the value the finish leaves
	struct PostField
	{
		const char* name;
		uint32_t offset;
		uint32_t size;
		uint32_t finished;
	};
	const PostField kPostFields[] = {
		{ "ambient occlusion resolution", RENDER_COMMON::kPostGBuffer + 0xD74, 4, 0x3F000000 }, // 0.5 across
		{ "ambient occlusion resolution", RENDER_COMMON::kPostGBuffer + 0xD78, 4, 0x3F000000 }, // 0.5 down
		{ "ambient occlusion blur", RENDER_COMMON::kPostGBuffer + 0xD85, 1, 1 },
		{ "film grain", 0x1B2C, 1, 1 },
		{ "motion blur", 0x1B2D, 1, 1 },
		{ "bloom", 0x1B31, 1, 1 },
		{ "exposure scaling", 0x1B32, 1, 1 },
		{ "depth of field", 0x1B37, 1, 1 },
		{ "anti-aliasing", 0x1B3C, 4, 2 },
	};
	constexpr int kPostFieldCount = static_cast<int>(sizeof(kPostFields) / sizeof(kPostFields[0]));

	// Other views' names: the Game group's fixed time step (0 = off), and the first part's HUD switch
	const char* const kFixedStepValue = "game.fixed_step_fps";
	const char* const kHideHudSwitch = "cinematic.screenshot_hide_hud";

	typedef bool(__thiscall* t_request)(void* capture, int mode, int quality, int type, const char* name, bool onlyWhenTriggered, bool keepFrameCount);
	typedef void(__thiscall* t_stop)(void* capture);

	std::atomic<float> g_quality = 0.0f;     // cinematic.game_capture_quality: 0 Low .. 3 Ultra
	std::atomic<float> g_type = 0.0f;        // cinematic.game_capture_type: 0 standard, 1 HDR, 2 both
	std::atomic<bool> g_lockStep = true;     // cinematic.game_sequence_lock_step
	std::atomic<float> g_fps = 30.0f;        // cinematic.game_sequence_fps

	// ---- The game's side (every read and call guarded) ----

	bool Available()
	{
		return DEVTOOLS_RELATIVE_ADDRESS(kCapture) && DEVTOOLS_RELATIVE_ADDRESS(kRequest) && DEVTOOLS_RELATIVE_ADDRESS(kStopSequence) &&
			RENDER_COMMON::Game(RENDER_COMMON::kPostProcessing);
	}

	struct GameCapture
	{
		int type = 0;
		int quality = 0;
		int mode = 0;
		int state = 0;
		uint32_t frames = 0;
		bool stopAsked = false;
		char file[kNameLength + 1] = {};
	};

	bool ReadCapture(GameCapture& out)
	{
		const uintptr_t capture = DEVTOOLS_RELATIVE_ADDRESS(kCapture);
		uint8_t raw[kCaptureSize];
		if (!capture || !RENDER_COMMON::Read(reinterpret_cast<const void*>(capture), raw, sizeof(raw)))
			return false;
		memcpy(&out.type, raw + kType, 4);
		memcpy(&out.quality, raw + kQuality, 4);
		memcpy(&out.mode, raw + kMode, 4);
		memcpy(&out.state, raw + kState, 4);
		memcpy(&out.frames, raw + kFrames, 4);
		out.stopAsked = raw[kStopFlag] != 0;
		memcpy(out.file, raw + kFile, kNameLength);
		out.file[kNameLength] = '\0';
		return true;
	}

	const char* StateName(int state)
	{
		return state >= 0 && state < 4 ? kStateNames[state] : "unknown";
	}

	int QualityIndex(int quality)
	{
		for (int i = 0; i < kQualityCount; i++)
			if (kQualities[i] == quality)
				return i;
		return -1;
	}

	uint64_t Bit(int type, int qualityIndex, int mode)
	{
		return 1ull << ((type * kQualityCount + qualityIndex) * 4 + mode);
	}

	// The combinations the game accepts, from its own table: a bit each (Bit); false when it can't be read
	bool ReadAccepted(uint64_t& bits)
	{
		bits = 0;
		const uintptr_t capture = DEVTOOLS_RELATIVE_ADDRESS(kCapture);
		const uintptr_t platform = capture ? RENDER_COMMON::Pointer(capture + kPlatform) : 0;
		const uintptr_t table = platform ? RENDER_COMMON::Pointer(platform + kPlatformTable) : 0;
		uint32_t count = 0;
		if (!table || !RENDER_COMMON::Read(platform + kPlatformTableCount, count) || count == 0 || count > kMaxTable)
			return false;
		uint32_t entries[kMaxTable * 3];
		if (!RENDER_COMMON::Read(reinterpret_cast<const void*>(table), entries, count * 12))
			return false;
		for (uint32_t i = 0; i < count; i++)
		{
			const uint32_t type = entries[i * 3], quality = entries[i * 3 + 1], mode = entries[i * 3 + 2];
			const int qualityIndex = QualityIndex(static_cast<int>(quality));
			if (type < kTypeCount && qualityIndex >= 0 && mode < 4)
				bits |= Bit(static_cast<int>(type), qualityIndex, static_cast<int>(mode));
		}
		return true;
	}

	// Whether the game takes this: "both" is asked for here as HDR then standard, so all three must be in its table
	bool Accepted(uint64_t bits, int type, int qualityIndex, int mode)
	{
		if (type == kTypeBoth)
			return (bits & Bit(kTypeBoth, qualityIndex, mode)) && (bits & Bit(kTypeHdr, qualityIndex, mode)) && (bits & Bit(kTypeStandard, qualityIndex, mode));
		return (bits & Bit(type, qualityIndex, mode)) != 0;
	}

	// The game's request (entity thread) and stop (any thread: it sets one flag, only while a sequence is recorded)
	bool CallRequest(int mode, int quality, int type, const char* name, bool& accepted)
	{
		accepted = false;
		const uintptr_t capture = DEVTOOLS_RELATIVE_ADDRESS(kCapture);
		const uintptr_t request = DEVTOOLS_RELATIVE_ADDRESS(kRequest);
		if (!capture || !request)
			return false;
		__try
		{
			accepted = reinterpret_cast<t_request>(request)(reinterpret_cast<void*>(capture), mode, quality, type, name, false, false);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool CallStop()
	{
		const uintptr_t capture = DEVTOOLS_RELATIVE_ADDRESS(kCapture);
		const uintptr_t stop = DEVTOOLS_RELATIVE_ADDRESS(kStopSequence);
		if (!capture || !stop)
			return false;
		__try
		{
			reinterpret_cast<t_stop>(stop)(reinterpret_cast<void*>(capture));
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// What the game's finish sets to fixed values, as it was before the capture
	struct PostSnapshot
	{
		bool taken = false;
		uint32_t values[kPostFieldCount] = {};
	};

	bool ReadPostField(uintptr_t post, const PostField& field, uint32_t& value)
	{
		value = 0;
		return RENDER_COMMON::Read(reinterpret_cast<const void*>(post + field.offset), &value, field.size);
	}

	void TakePost(PostSnapshot& snapshot)
	{
		snapshot.taken = false;
		const uintptr_t post = RENDER_COMMON::Game(RENDER_COMMON::kPostProcessing);
		if (!post)
			return;
		for (int i = 0; i < kPostFieldCount; i++)
			if (!ReadPostField(post, kPostFields[i], snapshot.values[i]))
				return;
		snapshot.taken = true;
	}

	// Once the game has finished (render thread, before the next frame is drawn, as the Post tab writes these): each field
	// the finish left at its fixed value is put back as it was before. The names put back, "" for none.
	std::string RestorePost(const PostSnapshot& snapshot)
	{
		const uintptr_t post = RENDER_COMMON::Game(RENDER_COMMON::kPostProcessing);
		if (!snapshot.taken || !post)
			return std::string();
		std::string restored;
		for (int i = 0; i < kPostFieldCount; i++)
		{
			const PostField& field = kPostFields[i];
			uint32_t now = 0;
			if (!ReadPostField(post, field, now) || now != field.finished || snapshot.values[i] == now)
				continue;
			if (!RENDER_COMMON::Write(reinterpret_cast<void*>(post + field.offset), &snapshot.values[i], field.size))
				continue;
			if (restored.find(field.name) == std::string::npos)
				restored += (restored.empty() ? "" : ", ") + std::string(field.name);
		}
		return restored;
	}

	// ---- Names and folders ----

	std::string Utf8(const std::wstring& text)
	{
		if (text.empty())
			return std::string();
		const int length = WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, nullptr, 0, nullptr, nullptr);
		std::string out(static_cast<size_t>(length > 0 ? length : 1), '\0');
		WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, &out[0], length, nullptr, nullptr);
		out.resize(strlen(out.c_str()));
		return out;
	}

	std::wstring WithSlash(std::wstring folder)
	{
		if (!folder.empty() && folder.back() != L'\\' && folder.back() != L'/')
			folder += L'\\';
		return folder;
	}

	// The game folder (where AI.exe is) and the game's working directory, each with a trailing backslash
	std::wstring GameFolder()
	{
		wchar_t path[MAX_PATH] = {};
		GetModuleFileNameW(nullptr, path, MAX_PATH);
		std::wstring folder = path;
		const size_t slash = folder.find_last_of(L"\\/");
		return slash == std::wstring::npos ? std::wstring() : folder.substr(0, slash + 1);
	}

	std::wstring WorkingFolder()
	{
		wchar_t path[MAX_PATH] = {};
		const DWORD length = GetCurrentDirectoryW(MAX_PATH, path);
		return length > 0 && length < MAX_PATH ? WithSlash(path) : std::wstring();
	}

	// The level's name for a file name: its last part, letters, digits, '-' and '_' only, kept short (the game keeps 128
	// characters of a name, its own count and extension included)
	std::string LevelPart(const std::string& level)
	{
		std::string name = level;
		const size_t slash = name.find_last_of("\\/");
		if (slash != std::string::npos)
			name = name.substr(slash + 1);
		for (char& c : name)
			if (!isalnum(static_cast<unsigned char>(c)) && c != '_' && c != '-')
				c = '_';
		if (name.size() > 32)
			name.resize(32);
		return name.empty() ? std::string("level") : name;
	}

	// The time, as the first part names its shots
	std::string Stamp()
	{
		SYSTEMTIME time;
		GetLocalTime(&time);
		char stamp[64];
		sprintf_s(stamp, "_%04u-%02u-%02u_%02u-%02u-%02u-%03u", time.wYear, time.wMonth, time.wDay, time.wHour, time.wMinute, time.wSecond, time.wMilliseconds);
		return stamp;
	}

	struct FoundFile
	{
		std::wstring name;
		uint64_t size;
	};

	// The files in a folder whose names start with prefix, by name
	std::vector<FoundFile> ListFiles(const std::wstring& folder, const std::wstring& prefix)
	{
		std::vector<FoundFile> files;
		WIN32_FIND_DATAW data;
		const HANDLE find = FindFirstFileW((folder + prefix + L"*").c_str(), &data);
		if (find == INVALID_HANDLE_VALUE)
			return files;
		do
		{
			if (!(data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY))
				files.push_back({ data.cFileName, (static_cast<uint64_t>(data.nFileSizeHigh) << 32) | data.nFileSizeLow });
		} while (FindNextFileW(find, &data));
		FindClose(find);
		std::sort(files.begin(), files.end(), [](const FoundFile& a, const FoundFile& b) { return a.name < b.name; });
		return files;
	}

	std::string Size(uint64_t bytes)
	{
		char text[32];
		if (bytes >= 1024 * 1024)
			sprintf_s(text, "%.1f MB", static_cast<double>(bytes) / (1024.0 * 1024.0));
		else
			sprintf_s(text, "%llu KB", static_cast<unsigned long long>((bytes + 1023) / 1024));
		return text;
	}

	// ---- A capture asked for here (its state under the lock; the render thread moves it on, once a frame) ----

	enum class Kind { Still, EnvMap, Sequence };
	enum class Phase { Idle, Hiding, Queued, Asking, Running, Done };

	struct Job
	{
		uint32_t id = 0;
		Kind kind = Kind::Still;
		int qualityIndex = 0;
		int type = kTypeStandard;    // as asked for: standard, HDR or both
		int step = 0;                // both: 0 the HDR capture, 1 the standard one after it
		Phase phase = Phase::Idle;
		std::string name;            // what the game is given, relative to its working directory
		std::wstring folder;         // where the files land, with a trailing backslash
		std::wstring prefix;         // how their names start there
		std::string where;           // the folder as the result names it
		std::string level;           // the level it was started in
		bool holdingHud = false;
		int hudFrames = 0;
		ULONGLONG queuedAt = 0;
		ULONGLONG runningSince = 0;
		int answer = -1;             // the game's answer to the request: -1 none yet, 0 refused, 1 accepted
		bool callFailed = false;
		PostSnapshot post;
		// A sequence: the time step set for it, what it was before; a stop asked for, and when the game was told
		bool stepSet = false;
		float stepBefore = 0.0f;
		float stepValue = 0.0f;
		bool stopWanted = false;
		bool stopSent = false;
		ULONGLONG stopSentAt = 0;
		uint32_t frames = 0;
		std::string note;            // said with the result
		bool ok = false;
		std::string message;
	};

	// The capture asked for here and the last result, under the lock (held while DEBUG_MENU's own locks are taken, never
	// the other way round)
	std::mutex g_mutex;
	Job g_job;
	uint32_t g_nextId = 1;
	std::string g_last;              // the last result, for the tab

	bool Active(Phase phase)
	{
		return phase != Phase::Idle && phase != Phase::Done;
	}

	const char* KindName(Kind kind)
	{
		return kind == Kind::Still ? "still" : kind == Kind::EnvMap ? "environment map" : "image sequence";
	}

	int ModeOf(Kind kind)
	{
		return kind == Kind::Still ? kModeStill : kind == Kind::EnvMap ? kModeEnvMap : kModeSequence;
	}

	void Ask(uint32_t id);

	// Sends the request to the entity thread (the lock held)
	void QueueLocked()
	{
		g_job.phase = Phase::Queued;
		g_job.answer = -1;
		g_job.queuedAt = GetTickCount64();
		const uint32_t id = g_job.id;
		DEBUG_MENU::OnEntityThread([id]() { Ask(id); });
	}

	// Entity thread, as the game's own video capture entity asks: the post-processing noted (before the first request),
	// then the request
	void Ask(uint32_t id)
	{
		int mode = 0, quality = 1, type = kTypeStandard;
		char name[kNameLength] = {};
		bool snapshot = false;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			if (g_job.id != id || g_job.phase != Phase::Queued)
				return;
			g_job.phase = Phase::Asking;
			mode = ModeOf(g_job.kind);
			quality = kQualities[g_job.qualityIndex];
			type = g_job.type == kTypeBoth ? (g_job.step == 0 ? kTypeHdr : kTypeStandard) : g_job.type;
			strncpy_s(name, g_job.name.c_str(), _TRUNCATE);
			snapshot = !g_job.post.taken;
		}
		PostSnapshot post;
		if (snapshot)
			TakePost(post);
		bool accepted = false;
		const bool called = CallRequest(mode, quality, type, name, accepted);
		std::lock_guard<std::mutex> lock(g_mutex);
		if (g_job.id != id || g_job.phase != Phase::Asking)
			return;
		if (snapshot && accepted)
			g_job.post = post;
		g_job.callFailed = !called;
		g_job.answer = called && accepted ? 1 : 0;
	}

	// The time step set for a sequence put back (unless changed meanwhile; the lock held)
	void RestoreStepLocked()
	{
		if (!g_job.stepSet)
			return;
		g_job.stepSet = false;
		float now = 0.0f;
		if (DEBUG_MENU::GetValue(kFixedStepValue, now) && std::fabs(now - g_job.stepValue) < 0.001f)
			DEBUG_MENU::SetValue(kFixedStepValue, g_job.stepBefore);
	}

	// What the game saved, for the result (the lock held); false when it saved nothing
	bool FilesMessage(const Job& job, std::string& message)
	{
		const std::vector<FoundFile> files = ListFiles(job.folder, job.prefix);
		if (files.empty())
		{
			message = "the game saved no files in " + job.where + " (could it write there?)";
			return false;
		}
		if (job.kind == Kind::Sequence)
		{
			uint64_t total = 0;
			for (const FoundFile& file : files)
				total += file.size;
			message = "recorded " + std::to_string(files.size()) + " frames into " + job.where + " (frame_0.tga to frame_" + std::to_string(files.size() - 1) +
				".tga, " + Size(total) + ")";
			return true;
		}
		if (files.size() == 1)
		{
			message = "saved " + Utf8(files[0].name) + " (" + Size(files[0].size) + ") in " + job.where;
			return true;
		}
		message = "saved " + std::to_string(files.size()) + " files in " + job.where + ": ";
		if (files.size() <= 4)
		{
			for (size_t i = 0; i < files.size(); i++)
				message += (i ? ", " : "") + Utf8(files[i].name) + " (" + Size(files[i].size) + ")";
		}
		else
			message += Utf8(files.front().name) + " ... " + Utf8(files.back().name);
		return true;
	}

	// The capture is over (the lock held): what the finish changed put back (restorePost), the HUD and the time step let
	// go of, the result noted
	void FinishLocked(bool finished, const std::string& why, bool restorePost)
	{
		Job& job = g_job;
		const std::string restored = restorePost ? RestorePost(job.post) : std::string();
		if (job.holdingHud)
		{
			CINEMATIC_VISUALS::HoldHudHidden(false);
			job.holdingHud = false;
		}
		RestoreStepLocked();
		std::string message;
		job.ok = finished ? FilesMessage(job, message) : false;
		if (!finished)
			message = why;
		if (!job.note.empty())
			message += " (" + job.note + ")";
		if (!restored.empty())
			message += "; the game's finish changed " + restored + ": put back";
		job.message = message;
		job.phase = Phase::Done;
		g_last = std::string(KindName(job.kind)) + ": " + message;
		DevTools::Log("Debug menu: game capture: %s", g_last.c_str());
	}

	// Render thread: starts a capture; false with why not. lockFps > 0 locks a sequence's time step at it.
	bool Start(Kind kind, int qualityIndex, int type, float lockFps, std::string& error)
	{
		if (!Available())
		{
			error = "not available on this game release";
			return false;
		}
		const std::string level = LIVE_LINK::RunningLevelName();
		if (level.empty() || LIVE_LINK::GameState() != kGameplay)
		{
			error = "needs a level being played";
			return false;
		}
		GameCapture game;
		uint64_t accepted = 0;
		if (!ReadCapture(game) || !ReadAccepted(accepted))
		{
			error = "the game's capture can't be read";
			return false;
		}
		if (kind != Kind::Still)
			qualityIndex = 0;
		if (kind == Kind::Sequence)
			type = kTypeStandard;
		qualityIndex = (std::max)(0, (std::min)(kQualityCount - 1, qualityIndex));
		type = (std::max)(0, (std::min)(kTypeCount - 1, type));
		if (!Accepted(accepted, type, qualityIndex, ModeOf(kind)))
		{
			error = std::string("the game doesn't take a ") + KindName(kind) + " at " + kQualityNames[qualityIndex] + " quality, " + kTypeNames[type];
			return false;
		}
		float stepBefore = 0.0f;
		if (kind == Kind::Sequence && lockFps > 0.0f && !DEBUG_MENU::GetValue(kFixedStepValue, stepBefore))
		{
			error = "the fixed time step (game.fixed_step_fps) is not in this build";
			return false;
		}
		bool hide = false;
		DEBUG_MENU::GetSwitch(kHideHudSwitch, hide);

		std::lock_guard<std::mutex> lock(g_mutex);
		if (Active(g_job.phase))
		{
			error = "a capture is under way";
			return false;
		}
		if (game.state != kStateReady)
		{
			error = std::string("the game is capturing already (") + StateName(game.state) + ": a level's video capture entity?)";
			return false;
		}

		// The names: under the working directory's Screenshots folder (the game makes the folders in a name when missing;
		// the Screenshots folder is made here too, as the first part makes it)
		const std::wstring working = WorkingFolder();
		if (working.empty())
		{
			error = "the game's working directory can't be read";
			return false;
		}
		const std::string base = LevelPart(level) + Stamp();
		Job job;
		job.id = g_nextId++;
		job.kind = kind;
		job.qualityIndex = qualityIndex;
		job.type = type;
		job.level = level;
		job.folder = working + L"Screenshots\\";
		const bool gameFolder = _wcsicmp(working.c_str(), GameFolder().c_str()) == 0;
		CreateDirectoryW(job.folder.c_str(), nullptr);
		switch (kind)
		{
		case Kind::Still:
			job.name = "Screenshots\\" + base + "_";
			job.prefix = std::wstring(base.begin(), base.end()) + L"_";
			break;
		case Kind::EnvMap:
			job.name = "Screenshots\\" + base + "_envmap_";
			job.prefix = std::wstring(base.begin(), base.end()) + L"_envmap_";
			break;
		case Kind::Sequence:
			job.name = "Screenshots\\" + base + "_sequence\\frame";
			job.folder += std::wstring(base.begin(), base.end()) + L"_sequence\\";
			job.prefix = L"frame_";
			break;
		}
		job.where = gameFolder ? (kind == Kind::Sequence ? "the Screenshots folder's " + base + "_sequence" : std::string("the Screenshots folder")) : Utf8(job.folder);
		if (!gameFolder)
			job.note = "the game's working directory isn't the game folder";

		// A sequence's time step: set only when the Game group's is off, and put back after
		if (kind == Kind::Sequence && lockFps > 0.0f)
		{
			const float before = stepBefore;
			if (before <= 0.0f)
			{
				DEBUG_MENU::SetValue(kFixedStepValue, lockFps);
				float now = 0.0f;
				DEBUG_MENU::GetValue(kFixedStepValue, now);
				job.stepSet = true;
				job.stepBefore = before;
				job.stepValue = now;
			}
			else
			{
				char text[96];
				sprintf_s(text, "the fixed time step was on already, at %g a second: left as it was", before);
				job.note += (job.note.empty() ? "" : "; ") + std::string(text);
			}
		}

		g_job = job;
		if (hide)
		{
			CINEMATIC_VISUALS::HoldHudHidden(true);
			g_job.holdingHud = true;
			g_job.phase = Phase::Hiding;
		}
		else
			QueueLocked();
		return true;
	}

	// Any thread: stops the sequence being recorded (the game is told on the render thread, at its next frame); false with
	// why not
	bool AskStop(std::string& error)
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		if (g_job.kind != Kind::Sequence || !Active(g_job.phase))
		{
			error = "no image sequence is being recorded here";
			return false;
		}
		// Not asked for yet: let go of before it starts
		if (g_job.phase == Phase::Hiding || g_job.phase == Phase::Queued)
		{
			FinishLocked(false, "stopped before it started", false);
			return true;
		}
		g_job.stopWanted = true;
		return true;
	}

	// ---- The render thread, every frame: the capture moved on ----

	void Tick(ImDrawList* drawList)
	{
		bool recording = false;
		uint32_t frames = 0;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			Job& job = g_job;
			if (!Active(job.phase))
				return;
			const ULONGLONG now = GetTickCount64();
			GameCapture game;
			const bool read = ReadCapture(game);
			const std::string level = LIVE_LINK::RunningLevelName();
			const bool levelGone = level.empty() || level != job.level || LIVE_LINK::GameState() == kUnloading;

			switch (job.phase)
			{
			case Phase::Hiding:
				// The HUD's layers are hidden from the frame after they are asked to be: three frames' margin once they are
				if (levelGone)
				{
					FinishLocked(false, "not taken: the level changed", false);
					return;
				}
				job.hudFrames++;
				if ((CINEMATIC_VISUALS::HudHidden() && job.hudFrames >= 4) || job.hudFrames >= 30)
					QueueLocked();
				return;
			case Phase::Queued:
			case Phase::Asking:
				if (job.answer == 0)
				{
					FinishLocked(false, job.callFailed ? "the game's capture could not be called" : "the game turned it down (a capture of its own under way?)", true);
					return;
				}
				if (job.answer == 1)
				{
					job.phase = Phase::Running;
					job.runningSince = now;
					break;
				}
				// The level's scripts not running (the game's pause menu, a level loading): dropped, rather than taken later (after the
				// first of "both", what its finish changed is put back as at the end)
				if (job.phase == Phase::Queued && now - job.queuedAt > 5000)
					FinishLocked(false, "not taken: the level's scripts didn't run for 5 s (the game's own menu open, or a level loading?)", true);
				return;
			default:
				break;
			}

			// Under way
			if (job.kind == Kind::Sequence)
			{
				if (read && game.mode == kModeSequence)
					job.frames = game.frames;
				if (levelGone && !job.stopWanted)
				{
					job.stopWanted = true;
					job.note += (job.note.empty() ? "" : "; ") + std::string("stopped: the level changed");
				}
				if (job.stopWanted && !job.stopSent)
				{
					CallStop();
					job.stopSent = true;
					job.stopSentAt = now;
					RestoreStepLocked();
				}
				recording = !job.stopSent;
				frames = job.frames;
			}
			if (!read)
			{
				FinishLocked(false, "the game's capture can't be read any more", false);
				return;
			}
			if (game.state != kStateReady)
			{
				// Not finished: a still waits up to 30 s, a sequence 15 s once stopped (the game's finish comes with a level
				// update; what it changed is left to it then)
				if ((job.kind != Kind::Sequence && now - job.runningSince > 30000) || (job.stopSent && now - job.stopSentAt > 15000))
				{
					FinishLocked(false, std::string("the game hadn't finished it (") + StateName(game.state) + ")", false);
					return;
				}
			}
			else if (job.type == kTypeBoth && job.step == 0)
			{
				// The HDR one finished: the standard one next, under the same name
				job.step = 1;
				QueueLocked();
			}
			else
				FinishLocked(true, "", true);
		}

		// A note along the top while a sequence records (drawn after the game's frame, so never in it)
		if (!recording || !drawList)
			return;
		char text[160];
		sprintf_s(text, "Recording an image sequence: %u frames (Cinematic > Screenshots stops it)", frames);
		const float size = ImGui::GetFontSize() * DEBUG_MENU::Scale();
		const ImVec2 extent = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, text);
		const ImVec2 display = ImGui::GetIO().DisplaySize;
		const ImVec2 corner((display.x - extent.x) * 0.5f, DEBUG_MENU::BannerTop(extent.y + 8.0f) + 4.0f);
		drawList->AddRectFilled(ImVec2(corner.x - 8.0f, corner.y - 4.0f), ImVec2(corner.x + extent.x + 8.0f, corner.y + extent.y + 4.0f), IM_COL32(0, 0, 0, 170), 4.0f);
		drawList->AddText(ImGui::GetFont(), size, corner, IM_COL32(255, 90, 80, 255), text);
	}

	// ---- Named controls ----

	int CurrentQuality()
	{
		return (std::max)(0, (std::min)(kQualityCount - 1, static_cast<int>(std::lround(static_cast<float>(g_quality)))));
	}

	int CurrentType()
	{
		return (std::max)(0, (std::min)(kTypeCount - 1, static_cast<int>(std::lround(static_cast<float>(g_type)))));
	}

	std::vector<std::string> Words(const std::string& text)
	{
		std::vector<std::string> words;
		std::string word;
		for (char c : text)
		{
			if (isspace(static_cast<unsigned char>(c)) || c == ',')
			{
				if (!word.empty())
					words.push_back(word);
				word.clear();
			}
			else
				word += static_cast<char>(tolower(static_cast<unsigned char>(c)));
		}
		if (!word.empty())
			words.push_back(word);
		return words;
	}

	// Waits (a command's thread) for capture id to finish, up to timeoutMs
	DEBUG_MENU::CommandResult WaitFor(uint32_t id, int timeoutMs, const char* stillGoing)
	{
		for (int waited = 0; waited < timeoutMs; waited += 20)
		{
			{
				std::lock_guard<std::mutex> lock(g_mutex);
				if (g_job.id != id)
					return DEBUG_MENU::CommandResult{ false, "another capture replaced it" };
				if (g_job.phase == Phase::Done)
					return DEBUG_MENU::CommandResult{ g_job.ok, g_job.message };
			}
			Sleep(20);
		}
		return DEBUG_MENU::CommandResult{ true, stillGoing };
	}

	DEBUG_MENU::CommandResult StartFromCommand(Kind kind, int qualityIndex, int type, float lockFps)
	{
		// The level's name is the render thread's to read
		return DEBUG_MENU::AnswerOnRenderThread([kind, qualityIndex, type, lockFps]()
		{
			std::string error;
			if (!Start(kind, qualityIndex, type, lockFps, error))
				return DEBUG_MENU::CommandResult{ false, error };
			std::lock_guard<std::mutex> lock(g_mutex);
			return DEBUG_MENU::CommandResult{ true, std::to_string(g_job.id) };
		});
	}

	std::string Status()
	{
		std::string text = std::string("available=") + (Available() ? "1" : "0 (not on this game release)");
		GameCapture game;
		if (ReadCapture(game))
		{
			char line[256];
			sprintf_s(line, "\ngame_state=%d (%s)\ngame_mode=%d\ngame_quality=%d\ngame_type=%d\ngame_frames=%u\ngame_stop_flag=%d\ngame_file=%s",
				game.state, StateName(game.state), game.mode, game.quality, game.type, game.frames, game.stopAsked ? 1 : 0, game.file);
			text += line;
		}
		uint64_t accepted = 0;
		if (ReadAccepted(accepted))
		{
			text += "\naccepted=";
			bool first = true;
			const char* const modes[] = { "still", "envmap", "unit_test", "sequence" };
			for (int mode = 0; mode < 4; mode++)
				for (int type = 0; type < kTypeCount; type++)
					for (int quality = 0; quality < kQualityCount; quality++)
						if (accepted & Bit(type, quality, mode))
						{
							text += std::string(first ? "" : ",") + modes[mode] + "/" + kQualityNames[quality] + "/" + kTypeNames[type];
							first = false;
						}
		}
		float step = 0.0f;
		text += DEBUG_MENU::GetValue(kFixedStepValue, step) ? "\nfixed_step_fps=" + std::to_string(step) : std::string("\nfixed_step_fps=none (not in this build)");
		text += "\nworking_directory=" + Utf8(WorkingFolder()) + "\ngame_folder=" + Utf8(GameFolder());
		std::lock_guard<std::mutex> lock(g_mutex);
		const char* const phases[] = { "idle", "hiding the HUD", "queued for the entity thread", "asking", "under way", "done" };
		text += std::string("\njob_id=") + std::to_string(g_job.id) + "\njob_kind=" + KindName(g_job.kind) + "\njob_phase=" + phases[static_cast<int>(g_job.phase)];
		if (Active(g_job.phase))
			text += "\njob_name=" + g_job.name + "\njob_frames=" + std::to_string(g_job.frames) + (g_job.stepSet ? "\njob_time_step_set=" + std::to_string(g_job.stepValue) : std::string());
		text += "\nlast=" + g_last;
		return text;
	}

	void Name()
	{
		DEBUG_MENU::AddSwitch("cinematic.game_sequence_lock_step", &g_lockStep);
		DEBUG_MENU::AddValue("cinematic.game_capture_quality", &g_quality, 0.0f, 3.0f);
		DEBUG_MENU::AddValue("cinematic.game_capture_type", &g_type, 0.0f, 2.0f);
		DEBUG_MENU::AddValue("cinematic.game_sequence_fps", &g_fps, 1.0f, 240.0f);
		DEBUG_MENU::AddCommand("cinematic.game_capture", "<still|envmap> [low|medium|high|ultra] [standard|hdr|both] (the tab's choices when left out; an environment map is always low): the game's own capture into the Screenshots folder; waits for it (up to 30 s) and names the files", [](const std::string& argument)
		{
			Kind kind = Kind::Still;
			int quality = CurrentQuality(), type = CurrentType();
			for (const std::string& word : Words(argument))
			{
				bool known = false;
				if (word == "still" || word == "envmap" || word == "environment_map")
				{
					kind = word == "still" ? Kind::Still : Kind::EnvMap;
					known = true;
				}
				for (int i = 0; i < kQualityCount; i++)
					if (word == kQualityNames[i])
					{
						quality = i;
						known = true;
					}
				for (int i = 0; i < kTypeCount; i++)
					if (word == kTypeNames[i])
					{
						type = i;
						known = true;
					}
				if (!known)
					return DEBUG_MENU::CommandResult{ false, "unknown word \"" + word + "\": give still or envmap, low, medium, high or ultra, standard, hdr or both" };
			}
			const DEBUG_MENU::CommandResult started = StartFromCommand(kind, quality, type, 0.0f);
			if (!started.ok)
				return started;
			return WaitFor(static_cast<uint32_t>(strtoul(started.message.c_str(), nullptr, 10)), 30000, "still under way after 30 s: see cinematic.game_capture_status");
		});
		DEBUG_MENU::AddCommand("cinematic.game_sequence_start", "[fps] (0: leave the time step alone; nothing: the tab's lock and fps): records a file a frame into a folder in the Screenshots folder until cinematic.game_sequence_stop or a level change", [](const std::string& argument)
		{
			float lockFps = g_lockStep ? static_cast<float>(g_fps) : 0.0f;
			const std::vector<std::string> words = Words(argument);
			if (!words.empty())
			{
				char* end = nullptr;
				const float fps = strtof(words[0].c_str(), &end);
				if (end == words[0].c_str() || *end || !std::isfinite(fps) || fps < 0.0f || fps > 240.0f)
					return DEBUG_MENU::CommandResult{ false, "give the frames a second to lock the time step at (1-240), or 0 to leave it alone" };
				lockFps = fps;
			}
			const DEBUG_MENU::CommandResult started = StartFromCommand(Kind::Sequence, 0, kTypeStandard, lockFps);
			if (!started.ok)
				return started;
			const uint32_t id = static_cast<uint32_t>(strtoul(started.message.c_str(), nullptr, 10));
			// Until the game has taken it (or turned it down)
			for (int waited = 0; waited < 8000; waited += 20)
			{
				{
					std::lock_guard<std::mutex> lock(g_mutex);
					if (g_job.id != id)
						return DEBUG_MENU::CommandResult{ false, "another capture replaced it" };
					if (g_job.phase == Phase::Done)
						return DEBUG_MENU::CommandResult{ false, g_job.message };
					if (g_job.phase == Phase::Running)
						return DEBUG_MENU::CommandResult{ true, "recording into " + Utf8(g_job.folder) + (g_job.stepSet ? " with the time step locked at " + std::to_string(g_job.stepValue) + " a second" : std::string()) +
							(g_job.note.empty() ? std::string() : " (" + g_job.note + ")") };
				}
				Sleep(20);
			}
			return DEBUG_MENU::CommandResult{ true, "asked for: see cinematic.game_capture_status" };
		});
		DEBUG_MENU::AddCommand("cinematic.game_sequence_stop", "stops the image sequence recording; waits for the game to finish it (up to 15 s) and says how many frames it saved", [](const std::string&)
		{
			std::string error;
			if (!AskStop(error))
				return DEBUG_MENU::CommandResult{ false, error };
			uint32_t id = 0;
			{
				std::lock_guard<std::mutex> lock(g_mutex);
				id = g_job.id;
			}
			return WaitFor(id, 15000, "stop asked for, not finished yet: see cinematic.game_capture_status");
		});
		DEBUG_MENU::AddCommand("cinematic.game_capture_status", "the game's capture (its state, mode, quality, type, frames), the combinations it accepts, the time step, the folders, and the capture asked for here", [](const std::string&)
		{
			return DEBUG_MENU::CommandResult{ true, Status() };
		});
	}

	const bool g_named = (Name(), true);

	// ---- The tab's part ----

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

	void StartFromTab(Kind kind)
	{
		std::string error;
		if (!Start(kind, CurrentQuality(), CurrentType(), kind == Kind::Sequence && g_lockStep ? static_cast<float>(g_fps) : 0.0f, error))
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			g_last = std::string(KindName(kind)) + ": " + error;
		}
	}

	void DrawSection()
	{
		ImGui::SeparatorText("The game's own capture");
		if (!Available())
		{
			RENDER_COMMON::Note("Not available on this game release.");
			return;
		}
		RENDER_COMMON::Note("The game saves the next frame it draws itself, with its HUD unless hidden (never this window or anything else drawn over the game), as a .tga, a Radiance .hdr or both, in the Screenshots folder. Medium and above are a clean picture: no film grain, motion blur, bloom, depth of field or anti-aliasing.");

		uint64_t accepted = 0;
		const bool haveTable = ReadAccepted(accepted);
		GameCapture game;
		const bool readGame = ReadCapture(game);
		Phase phase;
		Kind kind;
		uint32_t frames;
		bool stopWanted;
		std::string last;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			phase = g_job.phase;
			kind = g_job.kind;
			frames = g_job.frames;
			stopWanted = g_job.stopWanted;
			last = g_last;
		}
		const bool busy = Active(phase);
		const std::string level = LIVE_LINK::RunningLevelName();
		const bool playing = !level.empty() && LIVE_LINK::GameState() == kGameplay;
		const char* why = !haveTable || !readGame ? "The game's capture can't be read yet." : !playing ? "Needs a level being played." :
			!busy && game.state != kStateReady ? "The game is capturing already (a level's video capture entity?)." : nullptr;

		// Only what the game takes: a kind with some quality for a still, the qualities for the kind chosen
		int type = CurrentType();
		int quality = CurrentQuality();
		auto typeTaken = [&](int t)
		{
			for (int q = 0; q < kQualityCount; q++)
				if (Accepted(accepted, t, q, kModeStill))
					return true;
			return false;
		};
		if (haveTable && !typeTaken(type))
			for (int t = 0; t < kTypeCount; t++)
				if (typeTaken(t))
				{
					type = t;
					g_type = static_cast<float>(t);
					break;
				}
		if (haveTable && !Accepted(accepted, type, quality, kModeStill))
			for (int q = 0; q < kQualityCount; q++)
				if (Accepted(accepted, type, q, kModeStill))
				{
					quality = q;
					g_quality = static_cast<float>(q);
					break;
				}

		const float width = 190.0f * DEBUG_MENU::Scale();
		ImGui::SetNextItemWidth(width);
		if (ImGui::BeginCombo("Quality", kQualityLabels[quality]))
		{
			for (int q = 0; q < kQualityCount; q++)
				if (!haveTable || Accepted(accepted, type, q, kModeStill))
					if (ImGui::Selectable(kQualityLabels[q], q == quality))
						g_quality = static_cast<float>(q);
			ImGui::EndCombo();
		}
		Tooltip("Low saves one frame, as drawn. Medium, High and Ultra save 4, 16 or 64 frames (\"_tile_x_y\") with the film grain, motion blur, bloom, depth of field, anti-aliasing and the ambient occlusion's blur off and the ambient occlusion at full resolution, the world held still meanwhile. The game works out a sub-pixel offset for each of those frames, meant for a supersampled picture put together from them, but this release never applies it, so they come out alike: Medium gives the clean picture, High and Ultra only take longer. Afterwards the graphics settings these turn off are put back as they were.");
		ImGui::SameLine();
		ImGui::SetNextItemWidth(width * 0.8f);
		if (ImGui::BeginCombo("Kind", kTypeLabels[type]))
		{
			for (int t = 0; t < kTypeCount; t++)
				if (!haveTable || typeTaken(t))
					if (ImGui::Selectable(kTypeLabels[t], t == type))
						g_type = static_cast<float>(t);
			ImGui::EndCombo();
		}
		Tooltip("Standard: a 32-bit .tga of the picture. HDR: a Radiance .hdr (the game turns its exposure scaling off for it). Both: the HDR one, then a standard one the frame after (numbered one on).");

		const bool stillTaken = haveTable && Accepted(accepted, type, quality, kModeStill);
		ImGui::BeginDisabled(busy || why || !stillTaken);
		if (ImGui::Button("Capture still"))
			StartFromTab(Kind::Still);
		ImGui::EndDisabled();
		Tooltip("The game's capture of the next frame it draws, into the Screenshots folder as <level>_<time>_<n>.tga (or .hdr), n the game's own count. While it is taken the game holds the AI, the characters and the physics.");
		ImGui::SameLine();
		const bool envTaken = haveTable && Accepted(accepted, type, 0, kModeEnvMap);
		ImGui::BeginDisabled(busy || why || !envTaken);
		if (ImGui::Button("Capture environment map"))
			StartFromTab(Kind::EnvMap);
		ImGui::EndDisabled();
		Tooltip("The game's environment map capture, always at Low: this release has nothing that turns the camera to a cube's faces for it, so it saves the view as it is, numbered by its own count (<level>_<time>_envmap_<n>). For the six faces of a cube map, use Live Link's cube map capture in OpenCAGE.");
		if (type != kTypeStandard)
			RENDER_COMMON::ColouredNote(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "HDR is expected to come out black on PC: the game copies the finished 8- or 10-bit picture into its half-float image, a copy Direct3D 11 doesn't make. Look at the .hdr before relying on it.");
		bool hide = false;
		if (DEBUG_MENU::GetSwitch(kHideHudSwitch, hide))
			ImGui::TextDisabled("Hide the HUD for the shot (above) is %s for these too.", hide ? "on" : "off");

		ImGui::SeparatorText("Image sequence");
		float step = 0.0f;
		const bool haveStep = DEBUG_MENU::GetValue(kFixedStepValue, step);
		ImGui::BeginDisabled(!haveStep || busy);
		bool lockStep = g_lockStep && haveStep;
		if (ImGui::Checkbox("Lock the time step while recording", &lockStep))
			g_lockStep = lockStep;
		ImGui::EndDisabled();
		Tooltip(haveStep ? "Sets the Game group's fixed time step (game.fixed_step_fps) for the recording, so every frame is the same step of game time and the frames play back smoothly at this rate however slowly the game draws them while it writes them. Put back when the recording stops. Left alone when the time step is on already. Sound keeps to real time, so it drifts from the picture." :
			"Needs the Game group's fixed time step (game.fixed_step_fps), which this build doesn't have.");
		ImGui::SameLine();
		ImGui::SetNextItemWidth(110.0f * DEBUG_MENU::Scale());
		float fps = g_fps;
		ImGui::BeginDisabled(!haveStep || !g_lockStep || busy);
		if (ImGui::SliderFloat("##gamesequencefps", &fps, 10.0f, 120.0f, "%.0f a second"))
			g_fps = (std::max)(1.0f, (std::min)(240.0f, std::round(fps)));
		ImGui::EndDisabled();
		if (haveStep && step > 0.0f && !busy)
			ImGui::TextDisabled("The fixed time step is on already (%g a second): the recording keeps to it.", step);

		const bool recording = busy && kind == Kind::Sequence;
		if (recording)
		{
			ImGui::BeginDisabled(stopWanted);
			if (ImGui::Button("Stop image sequence"))
			{
				std::string error;
				AskStop(error);
			}
			ImGui::EndDisabled();
			ImGui::SameLine();
			if (stopWanted)
				ImGui::TextDisabled("stopping: %u frames", frames);
			else if (phase == Phase::Running)
				ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.35f, 1.0f), "recording: %u frames", frames);
			else
				ImGui::TextDisabled("starting...");
		}
		else
		{
			const bool sequenceTaken = haveTable && Accepted(accepted, kTypeStandard, 0, kModeSequence);
			ImGui::BeginDisabled(busy || why || !sequenceTaken || (g_lockStep && !haveStep));
			if (ImGui::Button("Start image sequence"))
				StartFromTab(Kind::Sequence);
			ImGui::EndDisabled();
		}
		Tooltip("A standard .tga for every frame drawn, into a folder of its own in the Screenshots folder (<level>_<time>_sequence\\frame_0.tga on), until stopped here or the level changes. Each file is written as its frame is drawn, so the game runs slowly meanwhile; at 1920x1080 each frame is about 8 MB.");

		if (why)
			RENDER_COMMON::Note("%s", why);
		if (busy && kind != Kind::Sequence)
			ImGui::TextDisabled("Capturing...");
		if (!last.empty())
			RENDER_COMMON::Note("Last: %s", last.c_str());
	}

	const bool g_sectionAdded = DEBUG_MENU::AddTab("Screenshots", 96, DrawSection, "Cinematic");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(96, Tick);
}
