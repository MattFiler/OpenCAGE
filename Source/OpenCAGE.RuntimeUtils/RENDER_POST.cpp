#include "RENDER_COMMON.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <string>
#include <vector>

#include <imgui.h>

/*
	The debug menu's Rendering > Post tab: the post-processing as the game has it this frame - the exposure (how the
	picture adapts to the scene's brightness), the tone mapping, the colour grading (LUTs), and each effect's settings with
	the stack of settings the level's scripts push (each entry's priority and strength, the one on top winning) - and the
	fog; with the engine's own switches to leave an effect out, hold or set the exposure, and pick the tone mapping.

	All of it is in the post-processing object (a static object), read at the frame's presentation:
	- an "enabled" flag for each effect (film grain, motion blur, full-screen blur, vignette, bloom, distortion overlay,
	  fisheye, full-screen overlay, the LUT grading, depth of field, chromatic aberration, screen fade, low-resolution
	  frame blending), which the game only sets when a setting changes and reads every frame;
	- the settings each effect ends up with this frame, and the stacks they are worked out from;
	- the brightness histogram's statistics, the adaptation state and settings, the current and target LUTs;
	and, beside it:
	- the exposure the next frame uses, kept by the game in framebuffer units: the scene is scaled by 6 over it (the
	  "exposure scale"), worked out at the end of each frame's post-processing and read while the next frame is drawn;
	- which tone mapping operator the game uses (0 exponential, 1 linear, 2 Irawan, 3 Hable, 4 Hejl-Burgess-Dawson,
	  5 Haarm-Peter Duiker, 6 the default "day" one), which the level's tone mapping entities set when they change;
	- the G-buffer manager's ambient occlusion switches (on, and blurred);
	- the fog manager's stack of fog settings (the top one used) and its enabled flag.

	An override writes the game's value once a frame at the presentation (after the frame's own post-processing and before
	the next frame is drawn), remembering what the game had; while it holds, a value the game writes meanwhile becomes the
	one remembered. Released, the game's value is put back (unless the game has changed it since). These are the engine's
	switches, named "render.post.*" - the cinematic camera's own look settings for shots are separate.

	Names: switches "render.post.no_film_grain", "render.post.no_motion_blur", "render.post.no_fullscreen_blur",
	"render.post.no_vignette", "render.post.no_bloom", "render.post.no_distortion_overlay", "render.post.no_fisheye",
	"render.post.no_fullscreen_overlay", "render.post.no_lut", "render.post.no_dof", "render.post.no_chromatic_aberration",
	"render.post.no_screen_fade", "render.post.no_low_res_blend", "render.post.no_ao", "render.post.no_ao_blur",
	"render.post.no_fog", "render.post.exposure_lock"; values "render.post.exposure_scale" (0 = the game's; otherwise the
	scale held) and "render.post.tone_mapping" (-1 = the game's, 0-6 an operator); commands "render.post.status",
	"render.post.stacks", "render.post.reset".

	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	using namespace RENDER_COMMON;

	// The post-processing object's fields
	constexpr uint32_t kHistogram = 0x124;          // 7 floats: min, max, black point, median, white point, average, average log
	constexpr uint32_t kHistogramFiltered = 0x140;
	constexpr uint32_t kAdaptationState = 0x384;    // fast, slow, pigment
	constexpr uint32_t kAdaptationSettings = 0x390; // slow t0, fast t0, pigment t0, to cd/m2, max, min, mechanism, percentile, low, high
	constexpr uint32_t kLutSettings = 0x5BC;        // lut a, lut b (textures), a's share, b's share, blend
	constexpr uint32_t kCurrentLut = 0x5D4;
	constexpr uint32_t kTargetLut = 0x5D8;
	constexpr uint32_t kLutTransition = 0x5EC;
	constexpr uint32_t kLutContribution = 0x5F0;
	constexpr uint32_t kActiveLutEntities = 0x5D0;
	constexpr uint32_t kGammaCorrection = 0xAE0;
	constexpr uint32_t kPostAAMode = 0x1B3C;
	constexpr uint32_t kScreenFadeState = 0x1B44;
	constexpr uint32_t kScreenFadeValue = 0x1B48;
	// A texture's file header (a pointer), its file name in that (a pointer)
	constexpr uint32_t kTextureHeader = 0x68;
	constexpr uint32_t kHeaderFileName = 0x28;
	// The G-buffer manager's ambient occlusion: on, blurred
	constexpr uint32_t kGBufferAOEnabled = 0xD84;
	constexpr uint32_t kGBufferAOBlur = 0xD85;

	// The exposure the next frame is drawn with, and the tone mapping operator
	constexpr uintptr_t kAdaptationValue = 0x0124FECC;
	constexpr uintptr_t kToneMappingOperator = 0x01250980;
	constexpr float kExposureNumerator = 6.0f;
	const char* const kOperators[] = { "Exponential", "Linear", "Irawan", "Hable", "Hejl-Burgess-Dawson", "Haarm-Peter Duiker", "Day (default)" };
	constexpr int kOperatorCount = 7;

	// The fog manager (a static object): 8 settings of 0x28 (linear distance, max distance, linear density, exponential
	// density, near colour, far colour), the top's index, enabled
	constexpr uintptr_t kFogManager = 0x01358150;
	constexpr uint32_t kFogSettingSize = 0x28;
	constexpr uint32_t kFogTop = 0x140;
	constexpr uint32_t kFogEnabled = 0x144;
	constexpr uint32_t kFogStackSize = 8;

	// ---- The effects' settings and stacks ----

	struct Field { const char* name; uint32_t offset; };
	struct Effect
	{
		const char* name;
		uint32_t resolved;  // the settings this frame
		uint32_t stack;     // the stack (elements, count, capacity)
		uint32_t size;      // one entry's size
		std::vector<Field> fields;
	};
	// Each entry starts with its stack priority (u16), flags (u8) and strength (float at +4)
	const Effect kEffects[] = {
		{ "Colour", 0x190C, 0x1A84, 0x20, { { "brightness", 0x8 }, { "contrast", 0xC }, { "saturation", 0x10 }, { "red tint", 0x14 }, { "green tint", 0x18 }, { "blue tint", 0x1C } } },
		{ "Bloom", 0x192C, 0x1A90, 0x1C, { { "frame scale", 0x8 }, { "frame offset", 0xC }, { "bloom scale", 0x10 }, { "gather exponent", 0x14 }, { "gather scale", 0x18 } } },
		{ "Flares", 0x1948, 0x1A9C, 0x38, { { "offset 0", 0x8 }, { "intensity 0", 0x18 }, { "attenuation 0", 0x28 } } },
		{ "Motion blur", 0x1980, 0x1AA8, 0x28, { { "contribution", 0x8 }, { "object velocity scale", 0xC }, { "camera velocity scale", 0x10 }, { "blur range", 0x24 } } },
		{ "Film grain", 0x19A8, 0x1AB4, 0x38, { { "low", 0x8 }, { "mid", 0xC }, { "high", 0x10 }, { "adaptation", 0x20 }, { "noise scale", 0x34 } } },
		{ "Vignette", 0x19E0, 0x1AC0, 0x10, { { "factor", 0x8 }, { "chromatic aberration", 0xC } } },
		{ "Distortion", 0x19F0, 0x1ACC, 0x14, { { "radial factor", 0x8 }, { "radial constraint", 0xC }, { "radial scale", 0x10 } } },
		{ "Sharpness", 0x1A04, 0x1AD8, 0xC, { { "local contrast", 0x8 } } },
		{ "Lens dust", 0x1A10, 0x1AE4, 0x1C, { { "max reflected bloom", 0x8 }, { "max bloom", 0xC }, { "reflected scale", 0x10 }, { "bloom scale", 0x14 }, { "threshold", 0x18 } } },
		{ "Full-screen blur", 0x1A2C, 0x1AF0, 0xC, { { "contribution", 0x8 } } },
		{ "Depth of field", 0x1A38, 0x1AFC, 0x14, { { "focal length", 0x8 }, { "focal plane", 0xC }, { "f-number", 0x10 } } },
		{ "Low-res frame blend", 0x1A4C, 0x1B14, 0xC, { { "blend", 0x8 } } },
		{ "Chromatic aberration", 0x1A58, 0x1B20, 0xC, { { "scale", 0x8 } } },
		{ "Overlay", 0x1A64, 0x1B08, 0x1C, { { "threshold", 0x8 }, { "start", 0xC }, { "stop", 0x10 }, { "range", 0x14 }, { "alpha", 0x18 } } },
	};

	// ---- Overrides ----

	// A value of the game's held at ours while wanted (render thread)
	template<typename T> struct Override
	{
		bool active = false;
		T saved = T();     // the game's
		T written = T();   // ours, as last written
		// Holds the value at address to value while wanted; puts the game's back once not
		void Apply(uintptr_t address, bool wanted, T value)
		{
			if (!address)
				return;
			T now;
			if (!Read(address, now))
			{
				active = false;
				return;
			}
			if (wanted)
			{
				if (!active)
					saved = now;
				else if (now != written)
					saved = now; // the game changed it meanwhile
				active = true;
				written = value;
				if (now != value)
					Write(address, value);
			}
			else if (active)
			{
				active = false;
				if (now == written)
					Write(address, saved);
			}
		}
	};

	struct FlagSwitch
	{
		const char* name;      // after "render.post."
		const char* label;
		const char* tooltip;
		uint32_t offset;       // in the post-processing object, or the G-buffer manager when gbuffer, or the fog manager when fog
		int where;             // 0 post-processing, 1 G-buffer manager, 2 fog manager
		std::atomic<bool> wanted;
		Override<uint8_t> override;
	};
	FlagSwitch g_flags[] = {
		{ "no_film_grain", "No film grain", "The film grain (the noise over the picture) is left out.", 0x1B2C, 0, false, {} },
		{ "no_motion_blur", "No motion blur", "Motion blur is left out (and the velocity pass it needs).", 0x1B2D, 0, false, {} },
		{ "no_fullscreen_blur", "No full-screen blur", "The full-screen blur scripts use (for being hurt, say) is left out.", 0x1B2E, 0, false, {} },
		{ "no_vignette", "No vignette", "The darkening towards the picture's edges is left out.", 0x1B30, 0, false, {} },
		{ "no_bloom", "No bloom", "Bloom (the glow round bright things) is left out.", 0x1B31, 0, false, {} },
		{ "no_distortion_overlay", "No distortion overlay", "The full-screen distortion overlay scripts use is left out.", 0x1B33, 0, false, {} },
		{ "no_fisheye", "No fisheye", "The radial (fisheye) lens distortion is left out.", 0x1B34, 0, false, {} },
		{ "no_fullscreen_overlay", "No full-screen overlay", "The full-screen overlay texture scripts use is left out.", 0x1B35, 0, false, {} },
		{ "no_lut", "No colour grading (LUT)", "The colour grading lookup tables are left out: the picture as tone mapped, before grading.", 0x1B36, 0, false, {} },
		{ "no_dof", "No depth of field", "Depth of field (the blur of what is out of focus) is left out.", 0x1B37, 0, false, {} },
		{ "no_chromatic_aberration", "No chromatic aberration", "The colour fringes towards the picture's edges are left out.", 0x1B38, 0, false, {} },
		{ "no_screen_fade", "No screen fade", "Fades to and from black are left out: the picture shows through them.", 0x1B60, 0, false, {} },
		{ "no_low_res_blend", "No low-resolution frame blend", "The blend with a low-resolution earlier frame scripts use is left out.", 0x1B61, 0, false, {} },
		{ "no_ao", "No ambient occlusion", "Screen-space ambient occlusion is left out.", kGBufferAOEnabled, 1, false, {} },
		{ "no_ao_blur", "No ambient occlusion blur", "The ambient occlusion is used without its blur, so its noise shows.", kGBufferAOBlur, 1, false, {} },
		{ "no_fog", "No fog", "The level's fog is left out.", kFogEnabled, 2, false, {} },
	};

	std::atomic<bool> g_exposureLock = false;
	std::atomic<float> g_exposureScale = 0.0f;   // 0: the game's
	std::atomic<float> g_toneMapping = -1.0f;    // -1: the game's
	Override<float> g_exposureOverride;
	Override<uint32_t> g_toneOverride;
	float g_lockedAdaptation = 0.0f;
	bool g_wasLocked = false;
	bool g_registered = false;

	uintptr_t Post() { return Game(kPostProcessing); }

	uintptr_t FlagAddress(const FlagSwitch& flag)
	{
		switch (flag.where)
		{
		case 0: { const uintptr_t post = Post(); return post ? post + flag.offset : 0; }
		case 1: { const uintptr_t post = Post(); return post ? post + kPostGBuffer + flag.offset : 0; }
		default: { const uintptr_t fog = Game(kFogManager); return fog ? fog + flag.offset : 0; }
		}
	}

	float CurrentAdaptation()
	{
		float value = 0.0f;
		return Read(Game(kAdaptationValue), value) ? value : 0.0f;
	}

	// Render thread, once a frame: the overrides written (after the frame's own post-processing)
	void Apply()
	{
		for (FlagSwitch& flag : g_flags)
			flag.override.Apply(FlagAddress(flag), flag.wanted, static_cast<uint8_t>(0));

		const uintptr_t adaptation = Game(kAdaptationValue);
		const float scale = g_exposureScale;
		const bool lock = g_exposureLock;
		if (lock && !g_wasLocked)
			g_lockedAdaptation = CurrentAdaptation();
		g_wasLocked = lock;
		if (scale > 0.0f && std::isfinite(scale))
			g_exposureOverride.Apply(adaptation, true, kExposureNumerator / scale);
		else if (lock && g_lockedAdaptation > 0.0f)
			g_exposureOverride.Apply(adaptation, true, g_lockedAdaptation);
		else
			g_exposureOverride.Apply(adaptation, false, 0.0f);

		const int tone = static_cast<int>(std::lround(static_cast<float>(g_toneMapping)));
		g_toneOverride.Apply(Game(kToneMappingOperator), tone >= 0 && tone < kOperatorCount, static_cast<uint32_t>(tone < 0 ? 0 : tone));
	}

	void ResetAll()
	{
		for (FlagSwitch& flag : g_flags)
			flag.wanted = false;
		g_exposureLock = false;
		g_exposureScale = 0.0f;
		g_toneMapping = -1.0f;
	}

	// ---- Reading ----

	std::string TextureName(uintptr_t texture)
	{
		const uintptr_t header = texture ? Pointer(texture + kTextureHeader) : 0;
		const std::string name = header ? Text(Pointer(header + kHeaderFileName), 200) : std::string();
		return name.empty() ? (texture ? "(unnamed)" : "(none)") : name;
	}

	struct Stack { uintptr_t elements = 0; uint32_t count = 0; };

	Stack ReadStack(uintptr_t post, const Effect& effect)
	{
		Stack stack;
		stack.elements = Pointer(post + effect.stack);
		if (!Read(post + effect.stack + 4, stack.count) || stack.count > 64)
			stack.count = 0;
		return stack;
	}

	std::string EntryText(uintptr_t entry, const Effect& effect, bool withHeader)
	{
		std::string text;
		char part[96];
		if (withHeader)
		{
			uint16_t priority = 0;
			uint8_t flags = 0;
			float strength = 0.0f;
			Read(entry, priority);
			Read(entry + 2, flags);
			Read(entry + 4, strength);
			snprintf(part, sizeof(part), "priority %u, flags 0x%02X, strength %.3f: ", priority, flags, strength);
			text += part;
		}
		bool first = true;
		for (const Field& field : effect.fields)
		{
			float value = 0.0f;
			if (!Read(entry + field.offset, value))
				continue;
			snprintf(part, sizeof(part), "%s%s %.3f", first ? "" : ", ", field.name, value);
			text += part;
			first = false;
		}
		return text;
	}

	std::string StatusText()
	{
		const uintptr_t post = Post();
		if (!post)
			return "available=0";
		std::string text = "available=1";
		char line[256];
		for (const FlagSwitch& flag : g_flags)
		{
			uint8_t value = 0;
			const bool read = Read(FlagAddress(flag), value);
			snprintf(line, sizeof(line), "\n%s_game=%d override=%d", flag.name + 3, read ? (flag.override.active ? flag.override.saved : value) : -1, flag.wanted.load());
			text += line;
		}
		const float adaptation = CurrentAdaptation();
		uint32_t tone = 0;
		Read(Game(kToneMappingOperator), tone);
		float histogram[7] = {};
		Read(reinterpret_cast<const void*>(post + kHistogramFiltered), histogram, sizeof(histogram));
		snprintf(line, sizeof(line), "\nadaptation=%.5f\nexposure_scale=%.4f\nexposure_lock=%d\nexposure_override=%.4f\ntone_mapping=%u\ntone_mapping_override=%d",
			adaptation, kExposureNumerator / (std::max)(adaptation, 1e-5f), g_exposureLock.load(), g_exposureScale.load(), tone, static_cast<int>(std::lround(static_cast<float>(g_toneMapping))));
		text += line;
		snprintf(line, sizeof(line), "\nhistogram min=%.4f max=%.4f black=%.4f median=%.4f white=%.4f average=%.4f", histogram[0], histogram[1], histogram[2], histogram[3], histogram[4], histogram[5]);
		text += line;
		text += "\nlut_current=" + TextureName(Pointer(post + kCurrentLut)) + "\nlut_target=" + TextureName(Pointer(post + kTargetLut));
		const uintptr_t fog = Game(kFogManager);
		uint32_t top = 0;
		uint8_t fogOn = 0;
		if (fog && Read(fog + kFogTop, top) && Read(fog + kFogEnabled, fogOn) && top < kFogStackSize)
		{
			float setting[10] = {};
			Read(reinterpret_cast<const void*>(fog + top * kFogSettingSize), setting, sizeof(setting));
			snprintf(line, sizeof(line), "\nfog enabled=%d top=%u linear_distance=%.2f max_distance=%.2f linear_density=%.4f exp_density=%.4f near=%.3f,%.3f,%.3f far=%.3f,%.3f,%.3f",
				fogOn, top, setting[0], setting[1], setting[2], setting[3], setting[4], setting[5], setting[6], setting[7], setting[8], setting[9]);
			text += line;
		}
		for (const Effect& effect : kEffects)
			text += std::string("\n") + effect.name + ": " + EntryText(post + effect.resolved, effect, false);
		return text;
	}

	std::string StacksText()
	{
		const uintptr_t post = Post();
		if (!post)
			return "available=0";
		std::string text;
		for (const Effect& effect : kEffects)
		{
			const Stack stack = ReadStack(post, effect);
			text += std::string(effect.name) + ": " + std::to_string(stack.count) + " entries\n";
			for (uint32_t i = 0; stack.elements && i < stack.count; i++)
				text += "  " + std::to_string(i) + " " + EntryText(stack.elements + i * effect.size, effect, true) + "\n";
		}
		return text;
	}

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		for (FlagSwitch& flag : g_flags)
			DEBUG_MENU::AddSwitch(std::string("render.post.") + flag.name, &flag.wanted);
		DEBUG_MENU::AddSwitch("render.post.exposure_lock", &g_exposureLock);
		DEBUG_MENU::AddValue("render.post.exposure_scale", &g_exposureScale, 0.0f, 1000.0f);
		DEBUG_MENU::AddValue("render.post.tone_mapping", &g_toneMapping, -1.0f, static_cast<float>(kOperatorCount - 1));
		DEBUG_MENU::AddCommand("render.post.status", "the post-processing now: each effect on or off (and overridden), the exposure, tone mapping, histogram, LUTs, fog, each effect's settings", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult { return { true, StatusText() }; });
		});
		DEBUG_MENU::AddCommand("render.post.stacks", "each effect's stack of settings (priority, flags, strength, values), the top one winning", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult { return { true, StacksText() }; });
		});
		DEBUG_MENU::AddCommand("render.post.reset", "releases every post-processing override (the game's values come back)", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			ResetAll();
			return { true, "Every post-processing override released" };
		});
	}

	void Overlay(ImDrawList* /*drawList*/)
	{
		Register();
		Apply();
	}

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

	void DrawTab()
	{
		Register();
		const uintptr_t post = Post();
		ImGui::TextWrapped("The post-processing as the game has it this frame, and the engine's switches to leave an effect out, hold the exposure or pick the tone mapping. Each override is the game's own flag or value held by the debug menu; released, the game's comes back.");
		if (!post)
		{
			ColouredNote(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "Not available on this game release.");
			return;
		}
		int overridden = 0;
		for (const FlagSwitch& flag : g_flags)
			overridden += flag.wanted ? 1 : 0;
		overridden += g_exposureLock || g_exposureScale > 0.0f ? 1 : 0;
		overridden += g_toneMapping >= 0.0f ? 1 : 0;
		ImGui::BeginDisabled(overridden == 0);
		if (ImGui::Button("Reset all##post"))
			ResetAll();
		ImGui::EndDisabled();
		ImGui::SameLine();
		ImGui::TextDisabled("%d override%s", overridden, overridden == 1 ? "" : "s");

		// Exposure
		if (ImGui::CollapsingHeader("Exposure and tone mapping##post", ImGuiTreeNodeFlags_DefaultOpen))
		{
			const float adaptation = CurrentAdaptation();
			const float scale = kExposureNumerator / (std::max)(adaptation, 1e-5f);
			ImGui::Text("Adaptation %.5f (framebuffer units): exposure scale %.3f", adaptation, scale);
			float histogram[7] = {};
			Read(reinterpret_cast<const void*>(post + kHistogramFiltered), histogram, sizeof(histogram));
			Note("Brightness histogram: min %.3f, black %.3f, median %.3f, average %.3f, white %.3f, max %.3f", histogram[0], histogram[2], histogram[3], histogram[5], histogram[4], histogram[1]);
			float state[3] = {};
			float settings[6] = {};
			uint32_t mechanism = 0;
			Read(reinterpret_cast<const void*>(post + kAdaptationState), state, sizeof(state));
			Read(reinterpret_cast<const void*>(post + kAdaptationSettings), settings, sizeof(settings));
			Read(post + kAdaptationSettings + 0x18, mechanism);
			Note("Adapted: fast %.3f, slow %.3f, pigment %.3f; adapts between %.3f and %.3f by %s", state[0], state[1], state[2], settings[5], settings[4], mechanism == 0 ? "percentile" : "bracketed mean");
			bool lock = g_exposureLock;
			if (ImGui::Checkbox("Hold the exposure##post", &lock))
				g_exposureLock = lock;
			Tooltip("render.post.exposure_lock: the exposure is held where it is now, so the picture stops adapting to the scene's brightness.");
			ImGui::SameLine();
			float manual = g_exposureScale;
			ImGui::SetNextItemWidth(ImGui::GetFontSize() * 10.0f);
			if (ImGui::DragFloat("Exposure scale##post", &manual, 0.02f, 0.0f, 1000.0f, manual > 0.0f ? "%.3f" : "game's"))
				g_exposureScale = (std::max)(0.0f, manual);
			Tooltip("render.post.exposure_scale: the scene is scaled by this before tone mapping (0: the game's own, which adapts). The game's is 6 over the adaptation.");
			ImGui::SameLine();
			if (ImGui::SmallButton("Now##exposure"))
				g_exposureScale = scale;
			Tooltip("Hold the exposure scale at what it is now.");

			uint32_t tone = 0;
			Read(Game(kToneMappingOperator), tone);
			const int wantedTone = static_cast<int>(std::lround(static_cast<float>(g_toneMapping)));
			const char* gameTone = g_toneOverride.active ? (g_toneOverride.saved < kOperatorCount ? kOperators[g_toneOverride.saved] : "?") : (tone < kOperatorCount ? kOperators[tone] : "?");
			char preview[96];
			snprintf(preview, sizeof(preview), wantedTone < 0 ? "Game's: %s" : "%s", wantedTone < 0 ? gameTone : kOperators[(std::min)(wantedTone, kOperatorCount - 1)]);
			ImGui::SetNextItemWidth(ImGui::GetFontSize() * 14.0f);
			if (ImGui::BeginCombo("Tone mapping##post", preview))
			{
				if (ImGui::Selectable("Game's##tone", wantedTone < 0))
					g_toneMapping = -1.0f;
				for (int i = 0; i < kOperatorCount; i++)
				{
					char label[64];
					snprintf(label, sizeof(label), "%d %s##tone", i, kOperators[i]);
					if (ImGui::Selectable(label, wantedTone == i))
						g_toneMapping = static_cast<float>(i);
				}
				ImGui::EndCombo();
			}
			Tooltip("render.post.tone_mapping: the tone mapping operator (-1 the game's). The level's tone mapping entities choose the game's.");
		}

		// The switches
		if (ImGui::CollapsingHeader("Effects##post", ImGuiTreeNodeFlags_DefaultOpen))
		{
			ImGui::TextDisabled("Ticked: left out. The game's own state in brackets.");
			if (ImGui::BeginTable("postflags", 2))
			{
				for (FlagSwitch& flag : g_flags)
				{
					if (flag.where == 2)
						continue;
					ImGui::TableNextColumn();
					uint8_t game = 0;
					const bool read = Read(FlagAddress(flag), game);
					if (flag.override.active)
						game = flag.override.saved;
					bool wanted = flag.wanted;
					char label[96];
					snprintf(label, sizeof(label), "%s (%s)##%s", flag.label, read ? (game ? "on" : "off") : "?", flag.name);
					if (ImGui::Checkbox(label, &wanted))
						flag.wanted = wanted;
					const std::string tip = std::string("render.post.") + flag.name + ": " + flag.tooltip;
					Tooltip(tip.c_str());
				}
				ImGui::EndTable();
			}
		}

		// LUTs
		if (ImGui::CollapsingHeader("Colour grading (LUTs)##post"))
		{
			float transition = 0.0f, contribution = 0.0f, shares[3] = {};
			int entities = 0;
			Read(post + kLutTransition, transition);
			Read(post + kLutContribution, contribution);
			Read(post + kActiveLutEntities, entities);
			Read(reinterpret_cast<const void*>(post + kLutSettings + 8), shares, sizeof(shares));
			ImGui::Text("Current: %s", TextureName(Pointer(post + kCurrentLut)).c_str());
			ImGui::Text("Target: %s", TextureName(Pointer(post + kTargetLut)).c_str());
			Note("Transition %.3f, contribution %.3f, %d colour LUT entities active", transition, contribution, entities);
			ImGui::Text("A: %s (%.3f)", TextureName(Pointer(post + kLutSettings)).c_str(), shares[0]);
			ImGui::Text("B: %s (%.3f), blend %.3f", TextureName(Pointer(post + kLutSettings + 4)).c_str(), shares[1], shares[2]);
			float gamma = 0.0f;
			uint32_t aa = 0, fadeState = 0;
			float fade = 0.0f;
			Read(post + kGammaCorrection, gamma);
			Read(post + kPostAAMode, aa);
			Read(post + kScreenFadeState, fadeState);
			Read(post + kScreenFadeValue, fade);
			const char* const aaNames[] = { "none", "FXAA", "SMAA T1x", "SMAA T2x" };
			Note("Gamma correction %.3f, anti-aliasing %s, screen fade state %u value %.3f", gamma, aa < 4 ? aaNames[aa] : "?", fadeState, fade);
		}

		// Settings and stacks
		if (ImGui::CollapsingHeader("Settings and stacks##post"))
		{
			Note("Each effect's settings this frame, then the stack the level's scripts pushed (the top entry wins).");
			for (const Effect& effect : kEffects)
			{
				const Stack stack = ReadStack(post, effect);
				char label[96];
				snprintf(label, sizeof(label), "%s (%u in its stack)##stack", effect.name, stack.count);
				if (!ImGui::TreeNode(label))
					continue;
				ImGui::TextWrapped("Now: %s", EntryText(post + effect.resolved, effect, false).c_str());
				for (uint32_t i = 0; stack.elements && i < stack.count; i++)
					ImGui::TextWrapped("%u. %s", i, EntryText(stack.elements + i * effect.size, effect, true).c_str());
				ImGui::TreePop();
			}
		}

		// Fog
		if (ImGui::CollapsingHeader("Fog##post"))
		{
			const uintptr_t fog = Game(kFogManager);
			uint32_t top = 0;
			uint8_t on = 0;
			if (!fog || !Read(fog + kFogTop, top) || !Read(fog + kFogEnabled, on) || top >= kFogStackSize)
				ImGui::TextDisabled("The fog can't be read.");
			else
			{
				FlagSwitch& flag = g_flags[sizeof(g_flags) / sizeof(g_flags[0]) - 1];
				ImGui::Text("Fog %s (the game's), setting %u of its stack in use", (flag.override.active ? flag.override.saved : on) ? "on" : "off", top);
				bool wanted = flag.wanted;
				if (ImGui::Checkbox("No fog##post", &wanted))
					flag.wanted = wanted;
				Tooltip("render.post.no_fog: the level's fog is left out.");
				for (uint32_t i = 0; i <= top; i++)
				{
					float setting[10] = {};
					Read(reinterpret_cast<const void*>(fog + i * kFogSettingSize), setting, sizeof(setting));
					ImGui::TextWrapped("%u%s: linear from %.2f, max distance %.2f, linear density %.4f, exponential density %.4f, near colour %.3f %.3f %.3f, far colour %.3f %.3f %.3f",
						i, i == top ? " (in use)" : "", setting[0], setting[1], setting[2], setting[3], setting[4], setting[5], setting[6], setting[7], setting[8], setting[9]);
				}
			}
		}
	}

	void AttachHooks(bool attach)
	{
		if (attach)
			return;
		// The game's values put back as the game closes
		ResetAll();
		Apply();
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Post", 74, DrawTab, "Rendering");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(7, Overlay);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachHooks);
	const bool g_namesAdded = (Register(), true);
}
