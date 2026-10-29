#pragma once

#include <string>

struct IDXGISwapChain;

namespace SCREENSHOT
{
	// Writes the swap chain's back buffer, as it stands (call it after the overlay has drawn, before the frame is
	// presented), to a 24-bit .bmp. Handles multisampled back buffers and the 8-bit RGBA/BGRA and 10-bit formats.
	bool Capture(IDXGISwapChain* swapChain, const std::string& path, std::string& error);
}
