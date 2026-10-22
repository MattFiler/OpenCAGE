#include "RENDER_COMMON.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <string>

#include <imgui.h>

/*
	The debug menu's Rendering > Debug Views tab: the game's own debug views of the bounce light (radiosity) and the
	G-buffer - the irradiance, albedo, normals, shininess, emissive, ambient occlusion, the radiosity's
	texture coordinates, blocked probes and slices, drawn over the whole screen in place of the lit scene.

	The game still has the shader for these views: it is loaded with the radiosity's other
	shaders (the same permutation, with its debug flag set) and kept at +0x60 in the radiosity object, but nothing picks it
	any more. The pass that applies the bounce light to the screen draws a few full-screen passes (one per kind of surface:
	the world, skin, characters, hair - or fewer when the level has no radiosity data); before each it writes zeros to the
	pixel shader constant that tells the debug shader which view to draw (register 1 of constant buffer 8), then sets the
	pass's own shader. With a view chosen, both calls of every pass are turned to ours (see RENDER_COMMON.h): ours writes
	the view's number to that constant and sets the debug shader in place of the pass's own. With no view chosen the calls
	are turned back.

	The view only takes the place of the bounce light's pass: the real-time lights, decals, transparency and
	post-processing still draw over it. The real-time lights and decals carry the surfaces' own colours, so with them on
	top a view of the lighting (the irradiance above all, which differs from the normal frame only in leaving the surfaces'
	colours and glow out of the bounce light) looks much like the normal frame in a room lit mostly by lamps. So while a
	view shows "alone" (the default) the Rendering tab's switches for the real-time lights and the decals are turned on
	with it, and the ones turned on here are turned off again when the view goes. Leaving the real-time lights out also darkens the player to the AI (the stealth meter
	samples them).

	Its value is "render.debug_view" (0 off, 1-15 the views) so OpenCAGE can choose one over the Live Link; the switch
	"render.debug_view_alone" (on by default) is whether a view shows alone.

	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	using namespace RENDER_COMMON;

	// The game's functions the passes call: the write of a pixel shader constant, and the setting of a shader
	constexpr uintptr_t kSetPixelConstant = 0x00621C90;
	constexpr uintptr_t kSetShader = 0x005FC560;
	// In the radiosity object: the debug view's shader (a pointer), and whether it has made what it draws with
	constexpr uint32_t kRadiosityDebugShader = 0x60;
	constexpr uint32_t kRadiosityLoaded = 0x04;
	constexpr uint32_t kRadiosityResources = 0x05;
	// The constant the debug shader reads its view from
	constexpr unsigned kConstantBuffer = 8;
	constexpr unsigned kConstantRegister = 1;

	// The passes' calls: the constant's write and the shader's setting, for the passes with radiosity data and without
	RENDER_COMMON::Call g_constantCalls[] = { { 0x005CB450 }, { 0x005CB4EB }, { 0x005CB57E }, { 0x005CB616 }, { 0x005CB6E1 }, { 0x005CB77B }, { 0x005CB80E } };
	RENDER_COMMON::Call g_shaderCalls[] = { { 0x005CB474 }, { 0x005CB507 }, { 0x005CB59F }, { 0x005CB637 }, { 0x005CB704 }, { 0x005CB797 }, { 0x005CB82F } };

	struct View
	{
		const char* label;
		const char* tooltip;
	};
	const View kViews[] = {
		{ "Off", "The game's own picture." },
		{ "Irradiance", "The bounce light reaching each surface, without the surfaces' own colours or glow (lamp-lit places stay dark: the real-time lights are left out while the view shows alone)." },
		{ "Albedo", "The surfaces' own colour (the G-buffer's albedo), unlit." },
		{ "Radiosity texture coordinates", "Where each surface reads its bounce light from in the radiosity's lightmaps: surfaces with none (dynamic, lit by the probes) and badly laid out ones stand out." },
		{ "Blocked probes", "The radiosity probes that are blocked (inside geometry), which make dark blotches." },
		{ "Direction", "The main direction the bounce light comes from at each surface." },
		{ "Shininess scale", "The scale applied to each surface's shininess." },
		{ "Normal", "The surfaces' normals (the G-buffer's)." },
		{ "Specular normal", "The normals used for the shine (the G-buffer's specular normals)." },
		{ "Shininess", "The surfaces' shininess (glossiness)." },
		{ "Specular levels", "How much each surface shines (its specular level)." },
		{ "Emissive", "The light surfaces give off themselves (screens, light panels)." },
		{ "Ambient occlusion", "The screen-space ambient occlusion." },
		{ "Irradiance map", "The irradiance as stored in the radiosity's lightmaps." },
		{ "Direction map", "The light direction as stored in the radiosity's lightmaps." },
		{ "Slices", "Which radiosity slice (part of the level's bounce-light data) each surface is in, each slice its own colour." },
	};
	constexpr int kViewCount = static_cast<int>(sizeof(kViews) / sizeof(kViews[0]));

	std::atomic<float> g_wanted = 0.0f;  // render.debug_view
	std::atomic<bool> g_alone = true;    // render.debug_view_alone
	std::atomic<int> g_active = 0;       // what the turned calls draw (0 while they are not turned)
	bool g_available = false;            // render thread: every call checked out
	bool g_checked = false;
	bool g_turned = false;
	bool g_registered = false;
	uintptr_t g_setPixelConstant = 0;    // the game's own functions (what the calls reached)
	uintptr_t g_setShader = 0;

	using SetPixelConstant = void(__cdecl*)(void* context, unsigned buffer, unsigned reg, const void* data, unsigned count);
	using SetShader = void(__thiscall*)(void* shaders, void* renderContext, void* shader, int keep, int force);

	// The debug shader, read from the radiosity object each time (it is made again when the radiosity is)
	void* DebugShader()
	{
		const uintptr_t engine = Engine();
		return engine ? reinterpret_cast<void*>(Pointer(engine + kEngineRadiosity + kRadiosityDebugShader)) : nullptr;
	}

	// The render task drawing the passes: the constant, with the view's number in it while one is chosen
	void __cdecl SetPixelConstantThunk(void* context, unsigned buffer, unsigned reg, const void* data, unsigned count)
	{
		const int view = g_active;
		if (view > 0 && buffer == kConstantBuffer && reg == kConstantRegister && count == 1)
		{
			const float value[4] = { static_cast<float>(view), 0.0f, 0.0f, 0.0f };
			reinterpret_cast<SetPixelConstant>(g_setPixelConstant)(context, buffer, reg, value, count);
			return;
		}
		reinterpret_cast<SetPixelConstant>(g_setPixelConstant)(context, buffer, reg, data, count);
	}

	// ... and the pass's shader, the debug shader in its place while a view is chosen
	void __fastcall SetShaderThunk(void* shaders, void* /*edx*/, void* renderContext, void* shader, int keep, int force)
	{
		if (g_active > 0)
			if (void* debug = DebugShader())
				shader = debug;
		reinterpret_cast<SetShader>(g_setShader)(shaders, renderContext, shader, keep, force);
	}

	void CheckCalls()
	{
		if (g_checked)
			return;
		g_checked = true;
		g_available = true;
		for (Call& call : g_constantCalls)
			g_available = Check(call, kSetPixelConstant, "render.debug_view") && g_available;
		for (Call& call : g_shaderCalls)
			g_available = Check(call, kSetShader, "render.debug_view") && g_available;
		if (g_available)
		{
			g_setPixelConstant = g_constantCalls[0].target;
			g_setShader = g_shaderCalls[0].target;
		}
	}

	// Turns every call to ours or back; on a call not as it was left, turns back what it can and gives up for the run
	bool TurnAll(bool on)
	{
		bool ok = true;
		for (Call& call : g_constantCalls)
			if (call.turned != on)
				ok = Turn(call, on ? reinterpret_cast<const void*>(&SetPixelConstantThunk) : nullptr) && ok;
		for (Call& call : g_shaderCalls)
			if (call.turned != on)
				ok = Turn(call, on ? reinterpret_cast<const void*>(&SetShaderThunk) : nullptr) && ok;
		if (!ok)
		{
			DevTools::Log("Debug menu: render.debug_view could not change the game's calls (not as they were left); it is off for this run.");
			g_active = 0;
			for (Call& call : g_constantCalls)
				if (call.turned)
					Turn(call, nullptr);
			for (Call& call : g_shaderCalls)
				if (call.turned)
					Turn(call, nullptr);
			g_available = false;
		}
		return ok;
	}

	int WantedView()
	{
		const float value = g_wanted;
		const int view = static_cast<int>(std::lround(value));
		return view < 0 ? 0 : view >= kViewCount ? kViewCount - 1 : view;
	}

	// What draws over a view and is left out while it shows alone: the Rendering tab's switches for the real-time lights
	// and the decals
	const char* const kLeftOut[] = { "render.no_deferred_lighting", "render.no_decals" };
	constexpr int kLeftOutCount = static_cast<int>(sizeof(kLeftOut) / sizeof(kLeftOut[0]));
	bool g_leaving = false;               // render thread: they are being left out
	bool g_turnedOn[kLeftOutCount] = {};  // render thread: the ones turned on here (so turned off again here)

	// Render thread: turns the switches on (those that are off) as a view starts showing alone, and the ones it turned on
	// off again as it stops
	void LeaveOut(bool wanted)
	{
		if (wanted == g_leaving)
			return;
		g_leaving = wanted;
		for (int i = 0; i < kLeftOutCount; i++)
		{
			if (wanted)
			{
				bool on = false;
				g_turnedOn[i] = DEBUG_MENU::GetSwitch(kLeftOut[i], on) && !on && DEBUG_MENU::SetSwitch(kLeftOut[i], true);
			}
			else if (g_turnedOn[i])
			{
				DEBUG_MENU::SetSwitch(kLeftOut[i], false);
				g_turnedOn[i] = false;
			}
		}
	}

	std::string StatusText();

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddValue("render.debug_view", &g_wanted, 0.0f, static_cast<float>(kViewCount - 1));
		DEBUG_MENU::AddSwitch("render.debug_view_alone", &g_alone);
		DEBUG_MENU::AddCommand("render.list_debug_views", "the radiosity / G-buffer debug views render.debug_view takes, a number and name a line", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			std::string text;
			for (int i = 0; i < kViewCount; i++)
				text += std::to_string(i) + " " + kViews[i].label + "\n";
			return { true, text };
		});
		DEBUG_MENU::AddCommand("render.debug_view_status", "the debug view chosen and drawn, and whether the game has what it needs", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult { return { true, StatusText() }; });
		});
	}

	// Render thread, every frame: brings the calls in line with the value chosen
	void ApplyView()
	{
		const int view = WantedView();
		if (view == 0)
		{
			g_active = 0;
			if (g_turned)
			{
				TurnAll(false);
				g_turned = false;
			}
			return;
		}
		CheckCalls();
		if (!g_available)
		{
			g_wanted = 0.0f;
			return;
		}
		if (!g_turned)
		{
			if (!TurnAll(true))
			{
				g_wanted = 0.0f;
				return;
			}
			g_turned = true;
		}
		g_active = view;
	}

	// ... and what draws over a view left out while one shows alone
	void Apply()
	{
		Register();
		ApplyView();
		LeaveOut(g_active > 0 && g_alone);
	}

	void ApplyOverlay(ImDrawList* /*drawList*/)
	{
		Apply();
	}

	struct RadiosityState
	{
		bool engine = false;
		bool loaded = false;
		bool resources = false;
		bool shader = false;
	};

	RadiosityState ReadState()
	{
		RadiosityState state;
		const uintptr_t engine = Engine();
		state.engine = engine != 0;
		if (!engine)
			return state;
		uint8_t flag = 0;
		state.loaded = Read(engine + kEngineRadiosity + kRadiosityLoaded, flag) && flag;
		state.resources = Read(engine + kEngineRadiosity + kRadiosityResources, flag) && flag;
		state.shader = DebugShader() != nullptr;
		return state;
	}

	std::string StatusText()
	{
		CheckCalls();
		const RadiosityState state = ReadState();
		const int view = WantedView();
		std::string text = "view=" + std::to_string(view) + " " + kViews[view].label;
		text += "\ndrawn=" + std::to_string(g_active.load());
		text += std::string("\navailable=") + (g_available ? "1" : "0");
		text += std::string("\ndebug_shader=") + (state.shader ? "loaded" : "missing");
		text += std::string("\nradiosity_data=") + (state.loaded ? "loaded" : "missing");
		text += std::string("\nradiosity_drawing=") + (state.resources ? "1" : "0");
		text += std::string("\nalone=") + (g_alone ? "1" : "0");
		text += "\nleft_out=";
		bool any = false;
		for (int i = 0; i < kLeftOutCount; i++)
		{
			bool on = false;
			if (DEBUG_MENU::GetSwitch(kLeftOut[i], on) && on)
			{
				text += std::string(any ? "," : "") + kLeftOut[i];
				any = true;
			}
		}
		if (!any)
			text += "none";
		return text;
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

	// A switch of the Rendering tab's, as a checkbox here
	void SwitchBox(const char* label, const char* name, const char* tooltip)
	{
		bool value = false;
		const bool exists = DEBUG_MENU::GetSwitch(name, value);
		ImGui::BeginDisabled(!exists);
		if (ImGui::Checkbox(label, &value))
			DEBUG_MENU::SetSwitch(name, value);
		ImGui::EndDisabled();
		Tooltip(tooltip);
	}

	void DrawTab()
	{
		Apply();
		CheckCalls();
		const RadiosityState state = ReadState();

		ImGui::TextWrapped("The game's own debug views of the bounce light (radiosity) and the G-buffer: each draws one thing over the whole screen in place of the lit scene. Transparency and post-processing still draw over it; the real-time lights and decals do too unless the view shows alone.");
		if (!g_available)
		{
			ColouredNote(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "Not available on this game release.");
			return;
		}
		if (!state.engine)
			return;
		if (!state.shader)
			ColouredNote(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "The debug shader is not loaded (no level running?): the views draw nothing until it is.");
		if (!state.resources)
			ColouredNote(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "The bounce light isn't being applied (no level, or no radiosity): the views show once it is.");
		else if (!state.loaded)
			ColouredNote(ImVec4(1.0f, 0.8f, 0.3f, 1.0f), "This level's radiosity data is not loaded: the G-buffer views still work, the radiosity ones show nothing useful.");

		int view = WantedView();
		ImGui::SetNextItemWidth(ImGui::GetFontSize() * 16.0f);
		if (ImGui::BeginCombo("Debug view##debugview", kViews[view].label))
		{
			for (int i = 0; i < kViewCount; i++)
			{
				char label[96];
				if (i == 0)
					snprintf(label, sizeof(label), "%s##%d", kViews[i].label, i);
				else
					snprintf(label, sizeof(label), "%d  %s##%d", i, kViews[i].label, i);
				if (ImGui::Selectable(label, i == view))
					g_wanted = static_cast<float>(i);
				Tooltip(kViews[i].tooltip);
			}
			ImGui::EndCombo();
		}
		Tooltip(kViews[view].tooltip);
		// (The list's own "Off" turns a view off too: this is the quick way back, shown only while a view is on)
		if (view != 0)
		{
			ImGui::SameLine();
			if (ImGui::Button("Back to the game's picture##debugview"))
				g_wanted = 0.0f;
		}

		// The views one click each
		const float width = ImGui::GetContentRegionAvail().x;
		const float button = ImGui::GetFontSize() * 9.5f;
		const int perRow = (std::max)(1, static_cast<int>(width / (button + ImGui::GetStyle().ItemSpacing.x)));
		for (int i = 1; i < kViewCount; i++)
		{
			if ((i - 1) % perRow != 0)
				ImGui::SameLine();
			const bool current = i == view;
			if (current)
				ImGui::PushStyleColor(ImGuiCol_Button, ImGui::GetStyleColorVec4(ImGuiCol_ButtonActive));
			char label[96];
			snprintf(label, sizeof(label), "%s##view%d", kViews[i].label, i);
			if (ImGui::Button(label, ImVec2(button, 0.0f)))
				g_wanted = current ? 0.0f : static_cast<float>(i);
			if (current)
				ImGui::PopStyleColor();
			Tooltip(kViews[i].tooltip);
		}

		ImGui::Spacing();
		// It only acts while a view is on, so it is greyed out with none (the switches below are then just the Rendering tab's)
		bool alone = g_alone;
		ImGui::BeginDisabled(view == 0);
		if (ImGui::Checkbox("Show the view alone##debugview", &alone))
			g_alone = alone;
		ImGui::EndDisabled();
		Tooltip("render.debug_view_alone: while a view is on, the real-time lights and decals are left out (the two switches below, turned on with the view and off again after it). Over a view they add the surfaces' own colours back, so a view of the lighting - the irradiance above all - would look much like the normal frame. Without the real-time lights the player also reads as darker to the AI.");
		if (view == 0)
			Note("\"Show the view alone\" only acts while a view is on: it then turns No real-time lights and No decals on (those that are off), and off again when the view goes. With no view on, the boxes below are just the Rendering tab's switches.");
		else if (g_alone)
			Note("Shown alone: No real-time lights and No decals are turned on with the view, and off again with it. The ticks below are what applies: untick one to draw it over the view anyway.");
		Note("Leave out what draws over the view (the Rendering tab's switches):");
		SwitchBox("No real-time lights##debugview", "render.no_deferred_lighting", "render.no_deferred_lighting: the real-time lights are not drawn, so they don't light the view.");
		ImGui::SameLine();
		SwitchBox("No decals##debugview", "render.no_decals", "render.no_decals: the decals laid over surfaces are not drawn over the view.");
		ImGui::SameLine();
		SwitchBox("No exposure scaling##debugview", "render.no_hdr_scaling", "render.no_hdr_scaling: the exposure no longer adapts, so the view's brightness holds still.");
	}

	void AttachHooks(bool attach)
	{
		// Nothing to attach: the calls are turned on the render thread when a view is chosen. Put back as the game closes.
		if (attach)
			return;
		g_active = 0;
		for (Call& call : g_constantCalls)
			if (call.turned)
				Turn(call, nullptr);
		for (Call& call : g_shaderCalls)
			if (call.turned)
				Turn(call, nullptr);
		g_turned = false;
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Debug Views", 71, DrawTab, "Rendering");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(6, ApplyOverlay);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachHooks);
	const bool g_namesAdded = (Register(), true);
}
