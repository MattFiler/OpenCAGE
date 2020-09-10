#pragma once

#include "SceneManager.h"

/* Entry point */
int WINAPI WinMain(__in HINSTANCE hInstance, __in_opt HINSTANCE hPrevInstance, __in LPSTR lpCmdLine, __in int nShowCmd) {
	SceneManager plantGen(hInstance);
	if (!plantGen.Init())
	{
		return 1;
	}

	return plantGen.Run();
}

