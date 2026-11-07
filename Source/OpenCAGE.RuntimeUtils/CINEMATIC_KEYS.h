#pragma once

#include <string>

/*
	The Cinematic group's keys (CINEMATIC_KEYS.cpp): one table of what each key does, Cinematic Tools' keys by default, set
	again in game in Cinematic > Keys (click a key, press the new one) and kept in OpenCAGE.CinematicKeys.ini next to AI.exe
	(OpenCAGE never writes that file):

	  [Keys]
	  FreezeTime=DELETE      ; a key named as OpenCAGE.RuntimeUtils.ini's HotReloadKey (F1..F12, A..Z, 0..9, HOME, LEFT,
	  HideHud=HOME           ; CTRL, SHIFT, ADD, NUMPAD0..9...) or a Windows virtual-key code; NONE for no key
	  ...

	Some act any time a level is played (the game window in front, no text being typed in the Debug Menu's window): the
	free camera on and off, freeze time, hide the HUD, invisibility, freeze NPCs and a screenshot. The rest only while the
	free camera flies. A key that another helper has (the Debug Menu's own, Possess's, Unlock Mouse's, Hot Reload's) does
	nothing here: that helper keeps it.
*/
namespace CINEMATIC_KEYS
{
	enum Action
	{
		// Any time a level is played
		ToggleCamera, FreezeTime, HideHud, Invisibility, FreezeNpcs, Screenshot,
		// While the free camera flies
		MoveForward, MoveBackward, MoveLeft, MoveRight, MoveUp, MoveDown, MoveFaster,
		LookLeft, LookRight, LookUp, LookDown, RollLeft, RollRight, FovWider, FovNarrower,
		FocusFarther, FocusNearer, FocalLonger, FocalShorter, FNumberHigher, FNumberLower,
		TrackAddNode, TrackDeleteNode, TrackPlay, StepFrame, SpeedUp, SpeedDown,
		kActionCount
	};

	// The key that does it now: 0 when it has none, or when its key is another helper's. Any thread.
	int Key(Action action);
	// Its key held down now (false with none). Any thread.
	bool Down(Action action);
	// The key's name for text ("no key" for none). A copy: Config::KeyName's text for a letter is overwritten by its next call.
	std::string KeyText(Action action);

	// Whether a key is being set in the Keys tab: no key acts meanwhile. Any thread.
	bool Capturing();

	// Every action's key, a line each ("FreezeTime=DELETE"), with which act any time; for the Live Link's cinematic.keys.
	std::string Describe();
}
