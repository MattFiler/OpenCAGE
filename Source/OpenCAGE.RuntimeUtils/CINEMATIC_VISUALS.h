#pragma once

#include <cstdint>

/*
	The cinematic camera's look (the Cinematic group's Visuals and HUD tabs): the depth of field, the colour and the rest of
	the post-processing, the tone mapping, and the HUD.

	Post-processing: every frame the game works out its post-processing settings afresh from the level's post-processing
	entities (defaults, then each active entity blended in by priority), then draws the frame with them. Straight after
	that (the post-processing settings' per-frame update, hooked in CUBEMAP_CAPTURE.cpp), each setting overridden here is
	written over the level's value. The depth of field's focal length, focus distance and f-number (Cinematic Tools' "DoF
	strength", "focus distance" and "focus scale") go together, under one switch; each of the others - Cinematic Tools'
	brightness, contrast, saturation, gamma and colour tint, and the bloom, film grain, vignette, chromatic aberration, lens
	distortion, sharpness, motion blur and full-screen blur - has its own. While an override is off its value follows the
	level's (so the tab shows it), and turned on it starts from the level's value of that moment - unless a value was set
	while it was off (a command, a track, a profile), which is kept for when it is turned on; turned off (or "Back to the
	level's values", which also lets go of values set meanwhile), the level's own values are drawn again from the next
	frame. The level's values are read every frame (shown beside each override), never copied once - so an override
	can't be stuck on values from a moment the screen was fading, as Cinematic Tools' (which copied them on its first
	frame) could. The game's own depth of field switch (the graphics settings') is shown: with it off, the depth of field
	draws nothing.

	Tone mapping: the level's day tone mapping entity sets the tone mapper's black point, cross-over point, white point,
	shoulder and toe strength and luminance scale when it runs. Each overridden one is written every frame; turned off,
	the level's own value (the last it set) is written back.

	HUD: the game's flash interface is a list of layers (the in-game HUD, the weapon HUD, the motion tracker, subtitles,
	menus...). A layer is hidden by setting its flag that has its drawing (and updating) skipped while its resources are
	about to be let go of - the one Cinematic Tools set to hide the HUD, and cubemap captures set: nothing sets it while a
	level is played. "Hide the HUD" ("cinematic.hide_hud") hides the in-game HUD and the weapon HUD (as Cinematic Tools'
	key did), and in a challenge map the challenge mode's objectives, score and timer, a layer of their own made by the
	map's scripts (while "cinematic.hide_hud_challenge" is on, as it is by default); each layer of the list can also be
	hidden by itself ("cinematic.ui.<layer name>"). The flags are put back as they were when turned off, for the layers
	still there; menus are left alone unless ticked.
*/
namespace CINEMATIC_VISUALS
{
	// Post-processing settings' per-frame update (its hook, after the game's own): writes the overrides. postProcessing is
	// the post-processing object the update ran on.
	void OnPostProcess(uint8_t* postProcessing);

	// The depth of field the camera's keys and tracks change: focal length (m), focus distance (m), f-number; whether it is
	// overridden.
	struct Dof
	{
		float focalLength = 0.05f;
		float focus = 5.0f;
		float fnumber = 1.0f;
	};
	bool DofOn();
	Dof GetDof();
	void SetDof(const Dof& dof);

	// Hides the in-game HUD for as long as held (a screenshot), whatever "Hide the HUD" says. Counted; any thread.
	void HoldHudHidden(bool held);
	// Whether the HUD layers are hidden now (as of the last frame).
	bool HudHidden();
}
