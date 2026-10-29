#include "RENDER_SWITCHES.h"
#include "DEBUG_MENU.h"

#include <atomic>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds, which have a
	row for every call listed here). Each call is the one the switch's own code makes, found in the same place in the same
	code, in the order the code asks its switches.
*/
namespace
{
	// The two shared answer functions: "no" (clears the answer and returns) and "yes" (sets the answer to one and returns).
	// Both change nothing but the answer's low byte (and "no" the flags, which each caller sets afresh as it tests the
	// answer), so a call that reaches the other one leaves the caller's registers as it expects them.
	constexpr uintptr_t kAnswerNo = 0x002508C0;
	constexpr uintptr_t kAnswerYes = 0x00AA70F0;
	constexpr uint8_t kAnswerNoCode[] = { 0x32, 0xC0, 0xC3 };
	constexpr uint8_t kAnswerYesCode[] = { 0xB0, 0x01, 0xC3 };
	constexpr uint8_t kCallOpcode = 0xE8;

	enum Group { Passes, AntiAliasing, RadiosityLights, RadiosityDebug, Shadows, LevelOfDetail, AlphaAndPost, Culling, DebugDrawing, Output, Developer, kGroups };

	struct GroupText
	{
		const char* title;
		const char* note;
	};

	const GroupText kGroupTexts[kGroups] = {
		{ "Passes", "Each leaves a part of the frame out." },
		{ "Anti-aliasing", "These only change anything while the game's own anti-aliasing setting is the one named." },
		{ "Radiosity", "What feeds the bounce light (radiosity) - each left out here is on in the game." },
		{ "Radiosity debug", "Radiosity debug switches - each is off in the game." },
		{ "Shadows", "Shadow switches - each is off in the game." },
		{ "Level of detail", nullptr },
		{ "Alpha and post-processing", "Where transparent geometry is drawn, and post-processing switches." },
		{ "Culling and sorting", "What the game leaves out because it can't be seen, and the order it draws in - each switch is off in the game." },
		{ "Debug drawing", "Debug drawing the game still has." },
		{ "Output", "The final picture straight from the lit scene, without the tone mapping and grading. The first one ticked wins." },
		{ "Developer (experimental)", "Experimental tools that are off in the game. Save your game first." },
	};

	struct Switch
	{
		const char* name;            // after "render." in its dotted name
		const char* label;
		const char* tooltip;
		Group group;
		bool gameAnswer;             // what the game's calls answer: yes for a part of the frame that is on, no for a debug switch that is off
		std::vector<uint32_t> calls; // where its calls are (Steam build)
		uint8_t shaderAt = 0;        // for a switch whose code sets a shader of the game's: how far after its first call the
		                             // instruction's four-byte address of that shader's pointer is (0: none). The switch is
		                             // only turned on while that pointer is set (the shader loaded).
	};

	const Switch kSwitches[] = {
		{ "no_solid", "No solid geometry", "The opaque geometry passes are skipped: walls, floors, props and characters vanish, leaving the sky, effects, glass and the HUD.", Passes, true,
			{ 0x00593EDC, 0x00593F4C, 0x00593FA3, 0x0059562A, 0x00595673, 0x00595713, 0x00595753 } },
		{ "no_decals", "No decals", "Decals - the stains, marks and signs laid over surfaces - are not drawn.", Passes, true,
			{ 0x005B3C43, 0x005B3DDB } },
		{ "no_deferred_lighting", "No deferred lighting", "The real-time lights (lamps, the torch...) are not gathered or sampled, leaving the bounce light and glowing surfaces. The stealth light meter samples these lights too, so the player reads as darker to the AI.", Passes, true,
			{ 0x0059006A, 0x005C6B88 } },
		{ "no_light_decals", "No light decals", "Light decals - light patterns projected onto surfaces - are not drawn.", Passes, true,
			{ 0x005958A3 } },
		{ "no_radiosity", "No radiosity", "The bounce light is not applied: indirect light goes, so areas lit only by light bouncing off other surfaces go dark. Transparent effects stop picking it up too.", Passes, true,
			{ 0x0057FA1C, 0x005800F9, 0x005C80AD, 0x005D5D4C, 0x005DF389, 0x005E037A } },
		{ "no_alpha", "No transparent geometry", "Transparent geometry - glass, smoke, particles, the sky's distant bodies - is not drawn, in any of the transparent passes.", Passes, true,
			{ 0x005956B3, 0x00595793, 0x005957E3, 0x005B4D70, 0x005B5343, 0x005B58D7 } },
		{ "no_low_res_alpha", "No low-resolution transparency", "The low-resolution transparent pass is skipped: smoke, steam and particles the game draws at reduced resolution vanish.", Passes, true,
			{ 0x005B5288 } },
		{ "no_alpha_lighting", "No lighting on transparency", "Transparent things (smoke, particles, glass) are no longer lit by the lights around them.", Passes, true,
			{ 0x005D4D1E } },
		{ "no_particle_cells", "No particle cell systems", "Particle cell systems - particles spread through the space around the camera, such as floating dust - are no longer updated.", Passes, true,
			{ 0x005DC2A1 } },
		{ "no_lens_flares", "No lens flares", "Lens flares are no longer tested for visibility or drawn.", Passes, true,
			{ 0x005BBAC3, 0x005D3953 } },
		{ "no_atmosphere", "No atmosphere", "The atmosphere pass (the light scattered by the air) is not drawn.", Passes, true,
			{ 0x005B53E1 } },
		{ "no_distortion", "No distortion", "Heat haze and the other screen distortion effects are not drawn.", Passes, true,
			{ 0x00595A73 } },
		{ "no_hair_softening", "No hair softening", "Hair is drawn without the pass that softens the edges of its strands.", Passes, true,
			{ 0x005B55B7, 0x005B5869 } },
		{ "no_skin_scattering", "No skin scattering", "Skin is drawn without the light that scatters under it, so faces look harder.", Passes, true,
			{ 0x00619D88 } },
		{ "no_character_morphs", "No character morphs", "Characters' morph animation (the shape changes of faces and bodies) is not applied.", Passes, true,
			{ 0x005C835B } },
		{ "no_face_wrinkles", "No face wrinkles", "Faces lose their animated wrinkles (the wrinkle maps blended in with expressions).", Passes, false,
			{ 0x0029544B } },
		{ "no_ui", "No HUD", "The game's HUD and menus are not drawn (this window still is).", Passes, true,
			{ 0x0043A7BB } },
		{ "no_selection_overlay", "No usable-object glow", "The glow drawn round the things the player can use is left out.", Passes, true,
			{ 0x0059429A } },

		{ "no_fxaa", "No FXAA", "FXAA is skipped (when the game's anti-aliasing is set to FXAA).", AntiAliasing, true,
			{ 0x0060022D, 0x00600317 } },
		{ "no_smaa", "No SMAA", "SMAA is skipped (when the game's anti-aliasing is set to SMAA or SMAA T2x).", AntiAliasing, true,
			{ 0x0060024A, 0x00600334 } },
		{ "no_smaa_temporal", "No temporal SMAA", "SMAA T2x runs as plain SMAA, without the part that blends with the frame before.", AntiAliasing, true,
			{ 0x00600461 } },

		{ "no_radiosity_static_lights", "No static lights in radiosity", "The bounce light leaves out the level's fixed lights.", RadiosityLights, true,
			{ 0x005D8DB6, 0x005D8E74 } },
		{ "no_radiosity_dynamic_lights", "No dynamic lights in radiosity", "The bounce light leaves out the lights that move or change.", RadiosityLights, true,
			{ 0x005D8FFB } },
		{ "no_radiosity_surface_lights", "No surface lights in radiosity", "The bounce light leaves out the light given off by glowing surfaces (screens, light panels).", RadiosityLights, true,
			{ 0x005D7E65 } },
		{ "no_radiosity_door_transfer", "No door transfer", "Bounce light no longer passes between rooms through their doors.", RadiosityLights, true,
			{ 0x005D8155 } },
		{ "no_radiosity_cubemap", "No radiosity cube maps", "The bounce light's indirect pass stops using its cube maps.", RadiosityLights, true,
			{ 0x005E3C58 } },

		{ "constant_ambient", "Constant ambient", "Adds a flat ambient light (0.2) on top of the bounce light, so nothing is fully dark and the lighting looks flatter.", RadiosityDebug, false,
			{ 0x005E02B3 } },
		{ "radiosity_unlimited_detail", "Unlimited radiosity detail", "The bounce light is worked out at full detail everywhere rather than at less far from the camera (slower).", RadiosityDebug, false,
			{ 0x005E5E54 } },
		{ "radiosity_no_frustum_priority", "No view priority", "The bounce light no longer updates what is in view first.", RadiosityDebug, false,
			{ 0x005E5E5D } },
		{ "radiosity_legacy_light_list", "Legacy light list", "The lights are handed to the bounce light the older way, from its old list of lights.", RadiosityDebug, false,
			{ 0x005D8DA9 } },
		{ "radiosity_no_spot_culling", "No spotlight culling", "Spotlights are handed to the bounce light without first leaving out those that can't reach it (slower).", RadiosityDebug, false,
			{ 0x005CBAC7 } },
		{ "radiosity_no_direct_compute", "No direct lighting compute", "The step that feeds the lights into the bounce light (a compute shader) is skipped, so the bounce light is no longer fed and fades.", RadiosityDebug, false,
			{ 0x005CC566, 0x005D7244 } },
		{ "radiosity_no_streaming", "No radiosity streaming", "The bounce light's data stops streaming in and out as the player moves through the level.", RadiosityDebug, false,
			{ 0x005E7F29 } },
		{ "radiosity_cubemap_pixel_shader", "Cube maps by pixel shader", "The radiosity cube maps are made with pixel shaders instead of compute shaders (should look the same).", RadiosityDebug, false,
			{ 0x005DF63C } },
		{ "radiosity_no_gun_cubemap", "No weapon cube maps", "Weapons stop using their own reflection cube maps.", RadiosityDebug, false,
			{ 0x00619C60 } },

		{ "no_shadow_cache", "No shadow cache", "Shadow maps are drawn afresh every frame instead of being kept from earlier frames (slower).", Shadows, false,
			{ 0x005F13AD } },
		{ "lowest_shadow_quality", "Shadow quality level 0", "Each shadow-casting light's shadow quality level is forced to 0 instead of the one the game picks for it.", Shadows, false,
			{ 0x00583434 } },
		{ "no_shadow_maps", "No shadow maps", "Lights are no longer taken as shadow casters, so the real-time lights cast no shadows.", Shadows, false,
			{ 0x00582C1C } },
		{ "no_shadow_frustum_filtering", "No shadow frustum filtering", "Shadow map filtering no longer skips what lies outside the light's view (slower).", Shadows, false,
			{ 0x005FB503, 0x005FBC8C } },
		{ "no_shadow_query_culling", "No shadow query culling", "Shadows are no longer left out by visibility queries (slower).", Shadows, false,
			{ 0x00578720, 0x00592972 } },
		{ "no_custom_env_maps", "No custom reflection maps", "Objects stop using their own reflection cube maps and fall back to the general one.", Shadows, false,
			{ 0x0058409C, 0x005841A5 } },

		{ "disable_lods", "Disable LODs", "Everything is drawn with its most detailed model at any distance, and the game's dynamic level-of-detail scaling is ignored (slower).", LevelOfDetail, false,
			{ 0x00288C94, 0x005EA77E, 0x005EA850, 0x005EA90E, 0x005EAA41, 0x005EAB10, 0x005EABCE, 0x005EAC96, 0x005EAD61, 0x005EAE30,
			  0x005EAEF6, 0x005EAFB6, 0x005EB081, 0x005EB146, 0x005EB206, 0x005EB2C6, 0x005EB386, 0x005EB446, 0x005EB506, 0x005EB5C6,
			  0x005EB686, 0x005EB746, 0x005EB806, 0x005EB8C6, 0x005EB986, 0x005EBA46, 0x005EBB06, 0x005EBBC6 } },

		{ "hide_early_alpha", "Hide early transparency", "The transparent geometry drawn before the rest of the transparent passes is hidden.", AlphaAndPost, false,
			{ 0x005B53F9 } },
		{ "all_alpha_low_res", "All transparency at low resolution", "All transparent geometry is drawn in the low-resolution pass.", AlphaAndPost, false,
			{ 0x005B549C } },
		{ "all_alpha_full_res", "All transparency at full resolution", "All transparent geometry is drawn at full resolution (nothing in the low-resolution pass).", AlphaAndPost, false,
			{ 0x005B54AD } },
		{ "hide_low_res_alpha", "Hide low-resolution transparency", "The low-resolution transparent pass is not run.", AlphaAndPost, false,
			{ 0x005B54BE } },
		{ "hide_alpha_before_low_res", "Hide transparency before the split", "The transparent geometry drawn ahead of the low- and full-resolution passes is hidden.", AlphaAndPost, false,
			{ 0x005B5549 } },
		{ "hide_main_alpha", "Hide full-resolution transparency", "The main full-resolution transparent pass is hidden.", AlphaAndPost, false,
			{ 0x005B58A0 } },
		{ "no_bilateral_upscale", "No edge-aware upscale", "The low-resolution transparent pass is scaled up without its edge-aware filter, so its edges look blocky.", AlphaAndPost, false,
			{ 0x005B55E9 } },
		{ "no_smooth_alpha", "No smooth alpha", "The smooth alpha pass (soft transparent geometry drawn on its own) is skipped.", AlphaAndPost, false,
			{ 0x005B0443 } },
		{ "no_hdr_scaling", "No exposure scaling", "The exposure (HDR) scaling is held off, so the picture no longer adapts its brightness to the scene.", AlphaAndPost, false,
			{ 0x005F1FF0 } },
		{ "blur_histogram", "Blur the brightness histogram", "The brightness histogram the auto exposure reads is blurred first, so the exposure shifts more smoothly.", AlphaAndPost, false,
			{ 0x00609BE3 } },
		{ "no_light_shafts", "No light shafts", "Light shafts (rays of light through the air) are not drawn.", AlphaAndPost, false,
			{ 0x005B4068, 0x005B4167, 0x005B5439 } },
		{ "light_shafts_unoccluded", "Light shafts ignore occlusion", "Light shafts are drawn at full strength, whatever blocks the light.", AlphaAndPost, false,
			{ 0x005B85BB, 0x005BDC96 } },
		{ "no_alpha_light_culling", "No light culling on transparency", "The lighting of transparent things (smoke, particles) no longer leaves out the lights that can't reach them (slower).", AlphaAndPost, false,
			{ 0x005809CC, 0x0057FBEB } },
		{ "no_alpha_light_camera_fit", "No camera fit for particle lighting", "Particles' lighting is worked out over their whole area instead of fitted to what the camera sees.", AlphaAndPost, false,
			{ 0x00589D32 } },
		{ "no_alpha_light_ambient", "No ambient in transparency lighting", "The lighting of transparent things no longer averages in the light around them (their ambient share is zero).", AlphaAndPost, false,
			{ 0x00580AD4 } },
		{ "no_force_alpha_to_full_res", "Keep transparency at low resolution", "Transparent things the game would move to the full-resolution pass (close to the camera, say) stay in the low-resolution pass.", AlphaAndPost, false,
			{ 0x005B4711, 0x005B4836 } },

		{ "no_occlusion_culling", "No occlusion culling", "Objects hidden behind others are no longer left out by the software depth test, so everything in view is drawn (slower). The quick test for an object that pops in or vanishes.", Culling, false,
			{ 0x005F09AA } },
		{ "no_occluder_rasterizing", "No occluder drawing", "The occluders (the big shapes that hide what is behind them) are no longer drawn into the software depth buffer, so the occlusion test has nothing to test against.", Culling, false,
			{ 0x005B3242 } },
		{ "update_all_particles", "Update particles out of view", "Particle emitters and ribbons keep updating when out of view, as if seen (particle culling off).", Culling, false,
			{ 0x005DC27D, 0x005D07D7 } },
		{ "depth_only_sort", "Sort by depth only", "What is drawn is sorted by distance alone instead of by material first: a performance comparison, the picture should look the same.", Culling, false,
			{ 0x0060B0BB, 0x0060CB3B, 0x00609CF1 } },

		{ "lens_flare_quads", "Lens flares as quads", "Lens flares are drawn as plain quads, showing each flare's extent.", DebugDrawing, false,
			{ 0x005D39AE } },
		{ "effect_overlay", "Surface effect overlay", "The game's full-screen surface effect material is drawn over the solid geometry (only where the level has that material).", DebugDrawing, false,
			{ 0x005B3EA8 } },

		{ "output_gamma22", "Output: gamma 2.2", "The lit scene shown with a plain gamma of 2.2, without the tone mapping, grading and other post-processing.", Output, false,
			{ 0x006100A3 }, 0xB },
		{ "output_gamma20", "Output: gamma 2.0", "The lit scene shown with a plain gamma of 2.0, without the tone mapping, grading and other post-processing.", Output, false,
			{ 0x006100C0 }, 0xA },
		{ "output_srgb", "Output: sRGB", "The lit scene shown as sRGB, without the tone mapping, grading and other post-processing.", Output, false,
			{ 0x006100D0 }, 0xB },
		{ "output_linear", "Output: linear", "The lit scene shown linear (no gamma correction), without the tone mapping, grading and other post-processing.", Output, false,
			{ 0x006100EC }, 0xB },

		{ "shader_hot_reload", "Shader hot reload", "Every 120 frames the game checks whether a shader pack it loaded has changed on disk, and if so reloads every shader (materials, lights, radiosity, lens flares). The game never turns this on: new materials or permutations that aren't in the loaded tables won't appear, and a reload can stall or crash the game - pause it and save first.", Developer, false,
			{ 0x00390527 } },
	};
	constexpr size_t kSwitchCount = sizeof(kSwitches) / sizeof(kSwitches[0]);

	// One call as it is in the running game
	struct Call
	{
		uint8_t* at = nullptr;  // the call instruction
		int32_t own = 0;        // its own distance (to the game's answer)
		int32_t flipped = 0;    // the distance to the other answer
	};

	struct State
	{
		std::atomic<bool> wanted = false; // ticked: the game's answer is turned the other way
		bool applied = false;             // render thread: what the calls are now
		bool checked = false;             // render thread: the calls have been checked
		bool available = false;           // render thread: every call checked out
		std::vector<Call> calls;
	};
	State g_states[kSwitchCount];

	bool g_answersChecked = false;
	bool g_answersOk = false;
	uint8_t* g_answerNo = nullptr;
	uint8_t* g_answerYes = nullptr;
	bool g_registered = false;

	bool ReadBytes(const uint8_t* at, uint8_t* out, size_t count)
	{
		__try
		{
			memcpy(out, at, count);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The two answer functions hold exactly what they should in this build
	bool CheckAnswers()
	{
		if (g_answersChecked)
			return g_answersOk;
		g_answersChecked = true;
		g_answerNo = reinterpret_cast<uint8_t*>(DEVTOOLS_RELATIVE_ADDRESS(kAnswerNo));
		g_answerYes = reinterpret_cast<uint8_t*>(DEVTOOLS_RELATIVE_ADDRESS(kAnswerYes));
		uint8_t no[sizeof(kAnswerNoCode)], yes[sizeof(kAnswerYesCode)];
		g_answersOk = g_answerNo && g_answerYes &&
			ReadBytes(g_answerNo, no, sizeof(no)) && memcmp(no, kAnswerNoCode, sizeof(no)) == 0 &&
			ReadBytes(g_answerYes, yes, sizeof(yes)) && memcmp(yes, kAnswerYesCode, sizeof(yes)) == 0;
		if (!g_answersOk)
			DevTools::Log("Debug menu: the rendering switches' answer functions are not as expected in this build; the switches are off.");
		return g_answersOk;
	}

	int32_t DistanceTo(const uint8_t* call, const uint8_t* target)
	{
		return static_cast<int32_t>(reinterpret_cast<uintptr_t>(target) - (reinterpret_cast<uintptr_t>(call) + 5));
	}

	// Checks every call of a switch: a call instruction reaching the answer the game gives there
	void Check(size_t index)
	{
		const Switch& entry = kSwitches[index];
		State& state = g_states[index];
		state.checked = true;
		state.available = false;
		state.calls.clear();
		if (!CheckAnswers())
			return;
		const uint8_t* gameAnswer = entry.gameAnswer ? g_answerYes : g_answerNo;
		const uint8_t* otherAnswer = entry.gameAnswer ? g_answerNo : g_answerYes;
		for (uint32_t steam : entry.calls)
		{
			Call call;
			call.at = reinterpret_cast<uint8_t*>(DEVTOOLS_RELATIVE_ADDRESS(steam));
			uint8_t bytes[5];
			if (!call.at || !ReadBytes(call.at, bytes, sizeof(bytes)) || bytes[0] != kCallOpcode)
			{
				DevTools::Log("Debug menu: render.%s is not available - the call for 0x%08X is not where it should be.", entry.name, steam);
				state.calls.clear();
				return;
			}
			memcpy(&call.own, bytes + 1, sizeof(call.own));
			if (call.own != DistanceTo(call.at, gameAnswer))
			{
				DevTools::Log("Debug menu: render.%s is not available - the call for 0x%08X does not reach the expected answer.", entry.name, steam);
				state.calls.clear();
				return;
			}
			call.flipped = DistanceTo(call.at, otherAnswer);
			state.calls.push_back(call);
		}
		state.available = true;
	}

	// Writes a call's four distance bytes. Where they sit inside one 64-byte cache line (all but a few calls) they are
	// written as one locked exchange, which other processors see whole; across a line they are written plainly.
	bool WriteDistance(uint8_t* call, int32_t distance)
	{
		uint8_t* where = call + 1;
		DWORD oldProtect;
		if (!VirtualProtect(where, sizeof(distance), PAGE_EXECUTE_READWRITE, &oldProtect))
			return false;
		const uintptr_t address = reinterpret_cast<uintptr_t>(where);
		if ((address & 63) <= 64 - sizeof(distance))
			InterlockedExchange(reinterpret_cast<volatile LONG*>(where), distance);
		else
			memcpy(where, &distance, sizeof(distance));
		VirtualProtect(where, sizeof(distance), oldProtect, &oldProtect);
		FlushInstructionCache(GetCurrentProcess(), call, 5);
		return true;
	}

	// Turns a switch's calls to the other answer, or back to their own. Every call must still be as it was left (another
	// tool may have changed one since) before any is written.
	bool Turn(size_t index, bool flip)
	{
		State& state = g_states[index];
		for (const Call& call : state.calls)
		{
			uint8_t bytes[5];
			int32_t now;
			if (!ReadBytes(call.at, bytes, sizeof(bytes)) || bytes[0] != kCallOpcode)
				return false;
			memcpy(&now, bytes + 1, sizeof(now));
			if (now != (state.applied ? call.flipped : call.own))
				return false;
		}
		for (const Call& call : state.calls)
			if (!WriteDistance(call.at, flip ? call.flipped : call.own))
				return false;
		return true;
	}

	// A switch whose code sets one of the game's shaders: whether that shader is loaded (its pointer set)
	bool ShaderLoaded(size_t index)
	{
		const Switch& entry = kSwitches[index];
		if (!entry.shaderAt)
			return true;
		const State& state = g_states[index];
		if (state.calls.empty())
			return false;
		uintptr_t slot = 0, shader = 0;
		return ReadBytes(state.calls[0].at + entry.shaderAt, reinterpret_cast<uint8_t*>(&slot), sizeof(slot)) && slot &&
			ReadBytes(reinterpret_cast<const uint8_t*>(slot), reinterpret_cast<uint8_t*>(&shader), sizeof(shader)) && shader;
	}

	void RegisterSwitches()
	{
		if (g_registered)
			return;
		g_registered = true;
		for (size_t i = 0; i < kSwitchCount; i++)
			DEBUG_MENU::AddSwitch(std::string("render.") + kSwitches[i].name, &g_states[i].wanted);
	}

	// Render thread, every frame: brings the calls in line with the switches (ticked in the window or set from outside)
	void Apply()
	{
		RegisterSwitches();
		for (size_t i = 0; i < kSwitchCount; i++)
		{
			State& state = g_states[i];
			const bool wanted = state.wanted;
			if (wanted == state.applied)
				continue;
			if (!state.checked)
				Check(i);
			if (!state.available)
			{
				state.wanted = false;
				continue;
			}
			if (wanted && !ShaderLoaded(i))
			{
				DevTools::Log("Debug menu: render.%s is not turned on - the shader it uses is not loaded.", kSwitches[i].name);
				state.wanted = false;
				continue;
			}
			if (Turn(i, wanted))
			{
				state.applied = wanted;
				DevTools::Log("Debug menu: render.%s %s.", kSwitches[i].name, wanted ? "on" : "off");
			}
			else
			{
				DevTools::Log("Debug menu: render.%s could not be changed (its calls are not as they were left); it is off for this run.", kSwitches[i].name);
				state.available = false;
				state.wanted = state.applied;
			}
		}
	}

	void ApplyOverlay(ImDrawList* /*drawList*/)
	{
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
		Apply();

		// Every switch is checked when the tab is first shown, so the unavailable ones show as such
		for (size_t i = 0; i < kSwitchCount; i++)
			if (!g_states[i].checked)
				Check(i);

		ImGui::TextWrapped("The game's own rendering switches. Some only do part of what their name suggests, and a few only show in certain places or with certain settings. Hover over one for what it does.");
		int on = 0;
		for (const State& state : g_states)
			on += state.wanted ? 1 : 0;
		ImGui::BeginDisabled(on == 0);
		if (ImGui::Button("Reset all"))
			for (State& state : g_states)
				state.wanted = false;
		ImGui::EndDisabled();
		ImGui::SameLine();
		if (on == 1)
			ImGui::TextDisabled("1 switch changed");
		else
			ImGui::TextDisabled("%d switches changed", on);
		if (!CheckAnswers())
			ImGui::TextColored(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "Not available on this build of the game.");

		for (int group = 0; group < kGroups; group++)
		{
			if (!ImGui::CollapsingHeader(kGroupTexts[group].title, ImGuiTreeNodeFlags_DefaultOpen))
				continue;
			if (kGroupTexts[group].note)
			{
				// Wrapped at the window's edge rather than running off it
				ImGui::PushTextWrapPos(0.0f);
				ImGui::TextDisabled("%s", kGroupTexts[group].note);
				ImGui::PopTextWrapPos();
			}
			for (size_t i = 0; i < kSwitchCount; i++)
			{
				const Switch& entry = kSwitches[i];
				if (entry.group != group)
					continue;
				State& state = g_states[i];
				bool ticked = state.wanted;
				ImGui::BeginDisabled(!state.available);
				char label[96];
				snprintf(label, sizeof(label), "%s##%s", entry.label, entry.name);
				if (ImGui::Checkbox(label, &ticked))
					state.wanted = ticked;
				ImGui::EndDisabled();
				if (state.available)
				{
					Tooltip(entry.tooltip);
				}
				else
				{
					const std::string text = std::string(entry.tooltip) + "\n\nNot available on this build of the game.";
					Tooltip(text.c_str());
				}
			}
		}
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Rendering", 70, DrawTab, "Rendering");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(5, ApplyOverlay);
}

void RENDER_SWITCHES::Restore()
{
	for (size_t i = 0; i < kSwitchCount; i++)
	{
		State& state = g_states[i];
		if (state.applied && Turn(i, false))
			state.applied = false;
	}
}
