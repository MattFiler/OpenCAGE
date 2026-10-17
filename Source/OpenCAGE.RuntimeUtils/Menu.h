#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <d3d11.h>
#include <cstdio>
#include <string>

#include <imgui.h>
#include <backends/imgui_impl_win32.h>
#include <backends/imgui_impl_dx11.h>

class Menu
{
public:
	Menu();
	~Menu();

	static LRESULT CALLBACK WndProcHandler(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam);

	static void InitMenu(IDXGISwapChain* pSwapChain);
	static bool IsInitialised();

	// A frame presented on this swap chain: whether the overlay draws on it. It is set up on the first swap chain a frame
	// is presented with, and draws on that one only.
	static bool UsePresentingSwapChain(IDXGISwapChain* pSwapChain);

	// The swap chain is about to resize its back buffers: the overlay lets go of its view of them (made again on the new
	// ones at the next frame)
	static void ReleaseRenderTarget(IDXGISwapChain* pSwapChain);

	static IDXGISwapChain* GetSwapChain();
	static void DrawMenu();
	static void ShutdownMenu();
	static bool CreateRenderTarget();
	static HRESULT GetDeviceAndContextFromSwapChain(IDXGISwapChain* pSwapChain, ID3D11Device** ppDevice, ID3D11DeviceContext** ppContext);

	static bool LoadLevel(std::string name);
	static bool LoadLevel(int id);
};

