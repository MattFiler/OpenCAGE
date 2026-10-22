#include "DEBUG_MENU.h"

#include <atomic>
#include <cmath>
#include <cstdio>
#include <string>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	The AI group's Tuning tab: five tuning numbers, which the game reads every
	time it needs them (each through a getter of its own that nothing else writes): how close a human or android must be to
	land a melee attack, how long an android keeps to a melee spot it chose, how long an android's grapple waits before it
	starts to hurt, how fast the player throws, and how much of an explosion's damage the player's allies take.

	Each is a named value ("ai.tuning.<name>"), read from the game the first time the menu runs (the game's own number, kept
	for Reset) and written back whenever it is changed, from the window or from OpenCAGE. Plain writes of the game's own
	globals on the render thread: a 32-bit store the AI threads read whole. Nothing is kept: the game starts with its own
	numbers again.

	Addresses are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	struct Tuning
	{
		const char* name;      // after "ai.tuning."
		const char* label;
		const char* tooltip;
		uintptr_t steam;
		bool integer;          // the game keeps it as a whole number
		float min, max;
		const char* format;
		std::atomic<float> value;
		float game;            // the game's own, read at the start
		float written;         // what is in the game now, as far as we know
		bool read;
	};

	Tuning g_tunings[] = {
		{ "android_melee_range", "Human and android melee range", "How close a human or android NPC must be to land a melee attack (metres; the game's own 2.5).",
			0x0123E974, false, 0.5f, 10.0f, "%.2f m" },
		{ "android_melee_context_timeout", "Android melee spot kept for", "How long an android keeps to the melee spot it chose round its target before choosing again (seconds; the game's own 60).",
			0x0123E978, false, 0.0f, 300.0f, "%.1f s" },
		{ "android_grapple_delay", "Android grapple: delay before it hurts", "How long an android's grapple holds the player before it starts doing damage (seconds; the game's own 1.5).",
			0x0123E97C, false, 0.0f, 10.0f, "%.2f s" },
		{ "player_throw_speed", "Player throw speed", "How fast the player's thrown items (flares, noisemakers, pipe bombs...) leave the hand (metres a second, a whole number; the game's own 20).",
			0x0123E964, true, 1.0f, 100.0f, "%.0f m/s" },
		{ "friendly_fire_modifier", "Explosion damage to the player's allies", "How much of an explosion's damage the player's allies take (a share of it: the game's own 0.5 is half).",
			0x0123E968, false, 0.0f, 2.0f, "x%.2f" },
	};
	constexpr int kTunings = sizeof(g_tunings) / sizeof(g_tunings[0]);

	bool ReadGame(const Tuning& tuning, float& out)
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(tuning.steam);
			if (!at)
				return false;
			out = tuning.integer ? static_cast<float>(*reinterpret_cast<const volatile int*>(at)) : *reinterpret_cast<const volatile float*>(at);
			return std::isfinite(out);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool WriteGame(const Tuning& tuning, float value)
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(tuning.steam);
			if (!at)
				return false;
			if (tuning.integer)
				*reinterpret_cast<volatile int*>(at) = static_cast<int>(std::lround(value));
			else
				*reinterpret_cast<volatile float*>(at) = value;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool Register();

	// Render thread, every frame (the window open or not): each number read once from the game, then written back when its
	// value changes
	void Apply(ImDrawList* /*drawList*/)
	{
		static const bool registered = Register();
		(void)registered;
		for (Tuning& tuning : g_tunings)
		{
			if (!tuning.read)
			{
				float game = 0.0f;
				if (!ReadGame(tuning, game))
					continue;
				tuning.game = game;
				tuning.written = game;
				tuning.value = game;
				tuning.read = true;
				continue;
			}
			float wanted = tuning.value;
			if (!std::isfinite(wanted))
				wanted = tuning.value = tuning.game;
			if (tuning.integer)
				wanted = static_cast<float>(std::lround(wanted));
			if (wanted != tuning.written && WriteGame(tuning, wanted))
				tuning.written = wanted;
		}
	}

	void DrawTab()
	{
		ImGui::TextWrapped("Tuning numbers the game reads every time it uses them. Changes last until the game is closed.");
		ImGui::Spacing();
		for (int i = 0; i < kTunings; i++)
		{
			Tuning& tuning = g_tunings[i];
			ImGui::PushID(i);
			if (!tuning.read)
			{
				ImGui::TextDisabled("%s: not available on this release of the game", tuning.label);
				ImGui::PopID();
				continue;
			}
			float value = tuning.value;
			ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
			if (ImGui::SliderFloat("##value", &value, tuning.min, tuning.max, tuning.format, ImGuiSliderFlags_AlwaysClamp))
				tuning.value = tuning.integer ? static_cast<float>(std::lround(value)) : value;
			ImGui::SameLine();
			ImGui::BeginDisabled(tuning.value == tuning.game);
			if (ImGui::SmallButton("Reset"))
				tuning.value = tuning.game;
			ImGui::EndDisabled();
			ImGui::SameLine();
			ImGui::TextUnformatted(tuning.label);
			if (ImGui::IsItemHovered())
				ImGui::SetTooltip("%s", tuning.tooltip);
			ImGui::PopID();
		}
	}

	bool Register()
	{
		for (Tuning& tuning : g_tunings)
			DEBUG_MENU::AddValue(std::string("ai.tuning.") + tuning.name, &tuning.value, tuning.min, tuning.max);
		DEBUG_MENU::AddCommand("ai.tuning.reset", "puts every ai.tuning value back to the game's own", [](const std::string&)
		{
			DEBUG_MENU::CommandResult result;
			for (Tuning& tuning : g_tunings)
				if (tuning.read)
				{
					tuning.value = tuning.game;
					char line[96];
					snprintf(line, sizeof(line), "%s=%g\n", tuning.name, tuning.game);
					result.message += line;
				}
			if (result.message.empty())
			{
				result.ok = false;
				result.message = "not read from the game yet";
			}
			return result;
		});
		return true;
	}

	const bool g_tab = DEBUG_MENU::AddTab("Tuning", 70, DrawTab, "AI");
	const bool g_overlay = DEBUG_MENU::AddOverlay(16, Apply);
}
