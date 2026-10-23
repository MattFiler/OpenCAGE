#include "GAME_TIME_STEP.h"
#include "GAME_DEBUG.h"
#include "DEBUG_MENU.h"
#include "Config.h"
#include "LIVE_LINK.h"

#include <atomic>
#include <cmath>
#include <cstdio>
#include <string>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	Each frame the game's clock works out how far to move the game on: the real time that passed since the last frame
	(never less than 1/360 of a second, never more than a quarter of a second - a longer frame is taken as a hitch, and the
	clock picks up real time again from where the game's time has got to). Its fixed step flag has it hand out its stored
	step instead (the shipped game leaves it off; a launch option and the VideoCapture script entity, neither of which the
	shipped game uses, turned it on). That step is the frame's step before the game clock's time scale, which the game speed
	and the game's own slow-motion moments set, is applied: game time moves on by step x scale.

	The clock also sets the frame rate the game steps its physics world at from the step (its inverse), and with the flag
	on physics takes that rate as it is, without the time scale. So while the step is held here, the time scale is folded
	into the step and the clock's own scale is held off (its flag cleared each frame, after the game speed has set it):
	game time and physics both move on by speed/N. The scale itself is left where the speed put it, and its flag set again
	as the step goes off, so the game speed (or a slow-motion moment) carries on.

	While the step is on, the game's time drifts from real time (ahead when frames are drawn faster than N a second). On the
	way off, the clock's real-time offset is pushed far ahead, which its next frame takes as a hitch: it picks up real time
	from where the game's time is (one frame of 1/30 of a second, a few short ones as it settles - what it does after any
	long frame). Without that, a game that got ahead would creep at 1/360 of a second a frame until real time caught up.
	Physics, which only moves its rate 0.1 a frame towards the frames' own, is set straight to the frames being drawn.

	Every write here is a plain one, made on the render thread as the frame is presented (between the game's frames, as the
	game speed's is), every one guarded.
*/
namespace
{
	// The game's real-time clock: whether it runs on real time at all (otherwise on a fixed frame of its own), the fixed
	// step flag, its speed factor (a double, 1), its real-time offset (a double) and the fixed step (a float, 1/30 as
	// shipped)
	constexpr uintptr_t kRealClock = 0x012410E0;
	constexpr uint32_t kRealRunning = 0x00;
	constexpr uint32_t kRealFixed = 0x02;
	constexpr uint32_t kRealFactor = 0x08;
	constexpr uint32_t kRealOffset = 0x10;
	constexpr uint32_t kRealStep = 0x20;

	// The game clock: the time scale (a double), the byte that has the scale used, and the frames it has run
	constexpr uintptr_t kGameClock = 0x012410A8;
	constexpr uint32_t kClockScale = 0x20;
	constexpr uint32_t kClockScaled = 0x30;
	constexpr uint32_t kClockFrames = 0x34;

	// The physics timing: the rate the physics world is stepped at aims for (frames a second), the rate it is stepped at
	// this frame (which moves 0.1 a frame towards that aim, or is the clock's own rate while the fixed step flag is on), and
	// the last 60 frames' steps the aim is worked out from; never under 30 a second
	constexpr uintptr_t kPhysicsTiming = 0x01308848;
	constexpr uint32_t kPhysicsAim = 0x04;
	constexpr uint32_t kPhysicsRate = 0x08;
	constexpr uint32_t kPhysicsSteps = 0x10;
	constexpr uint32_t kPhysicsStepCount = 60;
	constexpr float kPhysicsMinRate = 30.0f;

	// The entity manager (a pointer to it): the running level's root composite id at +0x0C
	constexpr uintptr_t kEntityManager = 0x0134EF40;
	constexpr uint32_t kEntityRoot = 0x0C;

	// The rates allowed (a step of a quarter of a second is the longest the clock ever takes), the shortest step the speed
	// may fold it to, and how far ahead the real-time offset is pushed to have the clock pick up real time again
	constexpr float kMinFps = 4.0f;
	constexpr float kMaxFps = 1000.0f;
	constexpr double kMinStep = 0.0001;
	constexpr double kMaxStep = 0.25;
	constexpr double kResync = 1000000.0;

	uintptr_t Address(uintptr_t steam) { return DEVTOOLS_RELATIVE_ADDRESS(steam); }

	template <typename T>
	bool Read(uintptr_t at, T& value)
	{
		__try
		{
			value = *reinterpret_cast<const volatile T*>(at);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	template <typename T>
	bool Write(uintptr_t at, T value)
	{
		__try
		{
			*reinterpret_cast<volatile T*>(at) = value;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool LevelRunning()
	{
		__try
		{
			return LIVE_LINK::LevelRunning();
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The running level's root composite id (0: no level)
	uint32_t LevelRoot()
	{
		if (!LevelRunning())
			return 0;
		const uintptr_t at = Address(kEntityManager);
		uintptr_t entities = 0;
		uint32_t root = 0;
		return at && Read(at, entities) && entities && Read(entities + kEntityRoot, root) ? root : 0;
	}

	// ---- The step ----

	std::atomic<float> g_fps = 0.0f;            // "game.fixed_step_fps": N, 0 = off
	std::atomic<bool> g_applied = false;        // the step is held (render thread writes)
	std::atomic<float> g_frameStep = 0.0f;      // the step held, the speed folded in (0: off)
	std::atomic<float> g_drawnFps = 0.0f;       // frames drawn a second (render thread writes)
	std::atomic<uint32_t> g_savedFlag = 0;      // the clock's own flag and step, as they were when the step was taken
	std::atomic<float> g_savedStep = 0.0f;
	uint32_t g_framesAtStart = 0;               // render thread: the clock's frame count when the step was taken
	std::atomic<uint32_t> g_frames = 0;         // the frames run since
	uint32_t g_root = 0;                        // render thread: the level last seen
	std::string g_note;                         // render thread: why it last went off by itself

	// Takes the clock's fixed step over: keeps its flag and step, and has the clock's scale hold what is wanted of it (1
	// while its flag is off), as it is folded in from now on
	bool Take(uintptr_t real, uintptr_t clock)
	{
		uint8_t flag = 0, scaled = 0;
		float step = 0.0f;
		uint32_t frames = 0;
		if (!Read(real + kRealFixed, flag) || !Read(real + kRealStep, step) || !Read(clock + kClockScaled, scaled))
			return false;
		if (!scaled)
			Write<double>(clock + kClockScale, 1.0);
		Read(clock + kClockFrames, frames);
		g_savedFlag = flag;
		g_savedStep = step;
		g_framesAtStart = frames;
		g_frames = 0;
		g_applied = true;
		DevTools::Log("Fixed time step: on (the game's own flag was %u, its step %.6f)", static_cast<unsigned>(flag), step);
		return true;
	}

	// Every frame while it is on: the time scale folded into the step, and the step and flag written
	void Hold(uintptr_t real, uintptr_t clock, float fps)
	{
		uint8_t scaled = 0;
		double scale = 1.0;
		Read(clock + kClockScaled, scaled);
		Read(clock + kClockScale, scale);
		if (scaled)
			Write<uint8_t>(clock + kClockScaled, 0);
		if (!std::isfinite(scale) || scale <= 0.0)
			scale = 1.0;
		double step = scale / fps;
		step = step < kMinStep ? kMinStep : step > kMaxStep ? kMaxStep : step;
		Write<float>(real + kRealStep, static_cast<float>(step));
		Write<uint8_t>(real + kRealFixed, 1);
		g_frameStep = static_cast<float>(step);
		uint32_t frames = 0;
		if (Read(clock + kClockFrames, frames))
			g_frames = frames - g_framesAtStart;
	}

	// The physics timing set to the frames now being drawn (game time a frame: the time scale over the frames a second), as
	// it would have settled with the step off: otherwise it would climb or fall from the step's rate 0.1 a frame, and
	// physics would run fast or slow against the rest for seconds
	void SettlePhysics(double scale)
	{
		const uintptr_t timing = Address(kPhysicsTiming);
		const float drawn = g_drawnFps;
		if (!timing || !std::isfinite(drawn) || drawn < 1.0f || !std::isfinite(scale) || scale <= 0.0)
			return;
		const float step = static_cast<float>(scale / drawn);
		float rate = 1.0f / step;
		rate = rate < kPhysicsMinRate ? kPhysicsMinRate : rate;
		for (uint32_t i = 0; i < kPhysicsStepCount; i++)
			Write<float>(timing + kPhysicsSteps + i * 4, step);
		Write<float>(timing + kPhysicsAim, rate);
		Write<float>(timing + kPhysicsRate, rate);
	}

	// Puts the clock's own flag and step back, gives the time scale back to the clock, and has the clock pick up real time
	// again (when its own flag was off), physics with it
	void Restore(uintptr_t real, uintptr_t clock, const char* why)
	{
		const uint8_t flag = static_cast<uint8_t>(g_savedFlag.load());
		Write<uint8_t>(real + kRealFixed, flag);
		Write<float>(real + kRealStep, g_savedStep.load());
		double scale = 1.0;
		if (clock && Read(clock + kClockScale, scale) && scale != 1.0)
			Write<uint8_t>(clock + kClockScaled, 1);
		if (!flag)
		{
			double factor = 0.0, offset = 0.0;
			if (Read(real + kRealFactor, factor) && std::isfinite(factor) && factor > 0.0 && Read(real + kRealOffset, offset) && std::isfinite(offset))
				Write<double>(real + kRealOffset, offset + kResync);
			SettlePhysics(scale);
		}
		g_applied = false;
		g_frameStep = 0.0f;
		DevTools::Log("Fixed time step: off (%s) after %u frames", why, static_cast<unsigned>(g_frames.load()));
	}

	float Clamped(float fps)
	{
		return fps < kMinFps ? kMinFps : fps > kMaxFps ? kMaxFps : fps;
	}

	// ---- Named for OpenCAGE and testing ----

	std::string Report()
	{
		const uintptr_t real = Address(kRealClock);
		const uintptr_t clock = Address(kGameClock);
		if (!real || !clock)
			return "not available on this game release";
		uint8_t running = 0, flag = 0, scaled = 0;
		float step = 0.0f;
		double scale = 0.0;
		Read(real + kRealRunning, running);
		Read(real + kRealFixed, flag);
		Read(real + kRealStep, step);
		Read(clock + kClockScaled, scaled);
		Read(clock + kClockScale, scale);
		float speed = 1.0f;
		const bool haveSpeed = DEBUG_MENU::GetValue("game.speed", speed);
		char text[768];
		snprintf(text, sizeof(text),
			"fixed_step_fps=%g\napplied=%d\nframe_step=%.6f\nframes_stepped=%u\ndrawn_fps=%.1f\ngame_time_rate=%.3f\nclock_fixed_flag=%u\nclock_fixed_step=%.6f\n"
			"clock_scale=%.4f\nclock_scale_used=%u\ngame_speed=%s\nclock_on_real_time=%u\nsaved_flag=%u\nsaved_step=%.6f\ncap_fps=%d\n",
			g_fps.load(), g_applied ? 1 : 0, g_frameStep.load(), static_cast<unsigned>(g_frames.load()), g_drawnFps.load(),
			g_applied ? g_drawnFps.load() * g_frameStep.load() : 0.0f, static_cast<unsigned>(flag), step, scale, static_cast<unsigned>(scaled),
			haveSpeed ? std::to_string(speed).c_str() : "(none)", static_cast<unsigned>(running), static_cast<unsigned>(g_savedFlag.load()),
			g_savedStep.load(), Config::Get().capFps);
		return text;
	}

	void RegisterOnce()
	{
		static bool registered = false;
		if (registered)
			return;
		registered = true;
		DEBUG_MENU::AddValue("game.fixed_step_fps", &g_fps, 0.0f, kMaxFps);
		DEBUG_MENU::AddCommand("game.time_step", "the fixed time step: what game.fixed_step_fps asks for, the step held (the game speed folded in), frames stepped, frames drawn a second and the game's rate against real time, and the game clock's own flag, step and time scale", [](const std::string&)
		{
			return DEBUG_MENU::CommandResult{ true, Report() };
		});
	}

	// Render thread, every frame (the window open or not), after the game speed has been written: the step follows the
	// value; no level, or another level, turns it off
	void Apply(ImDrawList* /*drawList*/)
	{
		RegisterOnce();
		g_drawnFps = ImGui::GetIO().Framerate;
		const uintptr_t real = Address(kRealClock);
		const uintptr_t clock = Address(kGameClock);
		const uint32_t root = LevelRoot();
		const char* why = "switched off";
		if (root != g_root)
		{
			if (g_applied || g_fps > 0.0f)
				g_note = root ? "went off: the level changed" : "went off: the level unloaded";
			g_root = root;
			g_fps = 0.0f;
			why = root ? "the level changed" : "the level unloaded";
		}
		if (!root)
		{
			g_fps = 0.0f;
			why = "no level";
		}
		float fps = g_fps;
		if (!std::isfinite(fps) || fps < 0.0f)
		{
			fps = 0.0f;
			g_fps = 0.0f;
		}
		if (!real || !clock || !root || fps < 1.0f)
		{
			if (g_applied && real)
				Restore(real, clock, why);
			return;
		}
		if (!g_applied)
		{
			if (!Take(real, clock))
			{
				g_fps = 0.0f;
				g_note = "went off: the game's clock could not be read";
				return;
			}
			g_note.clear();
		}
		Hold(real, clock, Clamped(fps));
	}

	// As the runtime utils unload: the clock put back
	void AttachHooks(bool attach)
	{
		if (attach)
		{
			RegisterOnce();
			return;
		}
		const uintptr_t real = Address(kRealClock);
		if (g_applied && real)
			Restore(real, Address(kGameClock), "the runtime utils unloaded");
	}

	// ---- The window ----

	void Tooltip(const char* text)
	{
		if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
			return;
		ImGui::BeginTooltip();
		ImGui::PushTextWrapPos(ImGui::GetFontSize() * 30.0f);
		ImGui::TextUnformatted(text);
		ImGui::PopTextWrapPos();
		ImGui::EndTooltip();
	}

	// A line of prose in grey (or amber), wrapped at the window's right edge rather than running past it
	void Hint(const char* text, bool warning = false)
	{
		ImGui::PushTextWrapPos(0.0f);
		if (warning)
			ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "%s", text);
		else
			ImGui::TextDisabled("%s", text);
		ImGui::PopTextWrapPos();
	}

	const char* const kTooltip = "Every frame moves the game on by exactly 1/N of a second of game time, however long it takes to draw: for smooth "
		"captures recorded slower than real time (each video frame one step of the game) and for repeatable tests (the AI, "
		"physics and the scripts see the same steps every run). Sound runs in real time, so it drifts against the picture while "
		"this is on. Drawing faster than N frames a second runs the game faster than real time (Cap FPS or V-Sync at N keeps it "
		"to real time). The game speed still applies. A level change turns it off.";

	float g_custom = 50.0f; // render thread: the custom rate's box

	void SetFps(float fps)
	{
		g_fps = fps;
	}

	void DrawSection()
	{
		GAME_TIME_STEP::DrawControls();
	}

	const bool g_sectionAdded = DEBUG_MENU::AddTab("Tools", 81, DrawSection, "Game");
	// After the game speed's (DEBUG_ACTIONS, order 6), so the speed it wrote this frame is the one folded in
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(8, Apply);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachHooks);
}

void GAME_TIME_STEP::DrawControls()
{
	ImGui::SeparatorText("Fixed time step");
	const uintptr_t real = Address(kRealClock);
	if (!real || !Address(kGameClock))
	{
		ImGui::TextDisabled("Not available on this game release.");
		return;
	}
	const bool level = LevelRoot() != 0;
	const float asked = g_fps;
	const bool on = asked >= 1.0f;

	ImGui::BeginDisabled(!level);
	struct Preset { const char* label; float fps; };
	const Preset presets[] = { { "Off", 0.0f }, { "24", 24.0f }, { "30", 30.0f }, { "60", 60.0f } };
	for (size_t i = 0; i < sizeof(presets) / sizeof(presets[0]); i++)
	{
		if (i)
			ImGui::SameLine();
		const bool current = presets[i].fps == 0.0f ? !on : on && Clamped(asked) == presets[i].fps;
		if (current)
			ImGui::PushStyleColor(ImGuiCol_Button, ImGui::GetStyleColorVec4(ImGuiCol_ButtonActive));
		if (ImGui::Button(presets[i].label))
			SetFps(presets[i].fps);
		if (current)
			ImGui::PopStyleColor();
		Tooltip(presets[i].fps == 0.0f ? "The game's clock takes real time again." : kTooltip);
	}
	ImGui::SameLine();
	ImGui::SetNextItemWidth(90.0f * DEBUG_MENU::Scale());
	if (ImGui::InputFloat("##customfps", &g_custom, 0.0f, 0.0f, "%.3f", ImGuiInputTextFlags_EnterReturnsTrue))
		SetFps(Clamped(g_custom));
	Tooltip("Another rate, in steps a second (4 to 1000; 23.976, 29.97 and 59.94 work too). Enter or Use applies it.");
	ImGui::SameLine();
	if (ImGui::Button("Use"))
		SetFps(Clamped(g_custom));
	Tooltip(kTooltip);
	ImGui::EndDisabled();

	if (!level)
	{
		ImGui::TextDisabled("No level running.");
		if (!g_note.empty())
			Hint(("Last: " + g_note).c_str());
		return;
	}

	uint8_t running = 1;
	Read(real + kRealRunning, running);
	if (!g_applied)
	{
		Hint("Off: each frame moves the game on by the real time it took.");
		if (!g_note.empty())
			Hint(("Last: " + g_note).c_str());
		return;
	}

	const float fps = Clamped(asked);
	const float step = g_frameStep;
	char line[256];
	if (std::fabs(step * fps - 1.0f) < 0.0005f)
		snprintf(line, sizeof(line), "On: each frame moves the game on by 1/%g s (%.2f ms) of game time.", fps, step * 1000.0f);
	else
		snprintf(line, sizeof(line), "On: each frame moves the game on by %.2f ms of game time (1/%g s, the game speed of %.2fx folded in).", step * 1000.0f, fps, step * fps);
	ImGui::PushTextWrapPos(0.0f);
	ImGui::TextUnformatted(line);
	ImGui::PopTextWrapPos();
	Tooltip("The game speed (and any slow-motion moment of the game's own) is folded into the step, so physics keeps in step with the rest; the game clock's own time scale is held off meanwhile and handed back when this goes off.");

	if (GAME_DEBUG::Paused())
	{
		Hint("Paused: Step lets exactly one step through.");
	}
	else
	{
		const float drawn = g_drawnFps;
		const float rate = drawn * step;
		snprintf(line, sizeof(line), "Drawing %.0f frames a second: the game runs at %.2fx real time.", drawn, rate);
		Hint(line, rate < 0.95f || rate > 1.05f);
		Tooltip("Game time moves on by the step every frame, so it keeps to real time only when the frames are drawn at the step's rate. Cap FPS (in Launch Game's scripting helpers) or V-Sync at that rate keeps it there; a capture that draws slower runs the game slower, which is what makes its frames smooth.");
	}
	const int cap = Config::Get().capFps;
	if (cap > 0)
	{
		snprintf(line, sizeof(line), "Cap FPS is %d%s.", cap, std::fabs(static_cast<float>(cap) - fps) < 0.5f ? ", the step's rate: the game keeps to real time (times the game speed) while frames keep up" : "");
		Hint(line);
	}
	snprintf(line, sizeof(line), "%u frames stepped. Sound runs in real time and drifts against the picture while this is on.", static_cast<unsigned>(g_frames.load()));
	Hint(line);
	if (!running)
		Hint("The game's clock is not running on real time at the moment (its own fixed frame is in use), so this changes nothing until it is.", true);
	if (g_savedFlag)
		Hint("The game had a fixed step of its own on (a VideoCapture entity, say): it is put back when this goes off.");
}

bool GAME_TIME_STEP::FrameStep(float& seconds)
{
	const float step = g_frameStep;
	if (!g_applied || step <= 0.0f)
		return false;
	seconds = step;
	return true;
}
