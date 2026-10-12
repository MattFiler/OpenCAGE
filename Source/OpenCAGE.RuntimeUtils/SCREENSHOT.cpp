#include "SCREENSHOT.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <d3d11.h>

#include <cstdio>
#include <cstring>
#include <vector>

namespace
{
	template<typename T> struct Com
	{
		T* ptr = nullptr;
		~Com() { if (ptr) ptr->Release(); }
		T** operator&() { return &ptr; }
		T* operator->() { return ptr; }
	};

	bool WriteBmp(const std::string& path, UINT width, UINT height, const std::vector<uint8_t>& bgr)
	{
		// The path comes over the live link as UTF-8; the narrow CRT would read it in the ANSI code page
		const int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, path.c_str(), -1, nullptr, 0);
		if (length <= 0)
			return false;
		std::wstring widePath(static_cast<size_t>(length), L'\0');
		MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, path.c_str(), -1, &widePath[0], length);
		FILE* file = nullptr;
		if (_wfopen_s(&file, widePath.c_str(), L"wb") != 0 || !file)
			return false;
		const UINT rowSize = (width * 3 + 3) & ~3u;
		BITMAPFILEHEADER fileHeader = {};
		BITMAPINFOHEADER infoHeader = {};
		fileHeader.bfType = 0x4D42;
		fileHeader.bfOffBits = sizeof(fileHeader) + sizeof(infoHeader);
		fileHeader.bfSize = fileHeader.bfOffBits + rowSize * height;
		infoHeader.biSize = sizeof(infoHeader);
		infoHeader.biWidth = static_cast<LONG>(width);
		infoHeader.biHeight = -static_cast<LONG>(height); // top-down
		infoHeader.biPlanes = 1;
		infoHeader.biBitCount = 24;
		infoHeader.biCompression = BI_RGB;
		fwrite(&fileHeader, sizeof(fileHeader), 1, file);
		fwrite(&infoHeader, sizeof(infoHeader), 1, file);
		const uint8_t padding[3] = {};
		for (UINT y = 0; y < height; y++)
		{
			fwrite(bgr.data() + static_cast<size_t>(y) * width * 3, 1, width * 3, file);
			fwrite(padding, 1, rowSize - width * 3, file);
		}
		fclose(file);
		return true;
	}
}

bool SCREENSHOT::Capture(IDXGISwapChain* swapChain, const std::string& path, std::string& error)
{
	if (!swapChain)
	{
		error = "No swap chain yet";
		return false;
	}
	Com<ID3D11Texture2D> backBuffer;
	if (FAILED(swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&backBuffer))))
	{
		error = "Could not get the back buffer";
		return false;
	}
	Com<ID3D11Device> device;
	backBuffer->GetDevice(&device);
	Com<ID3D11DeviceContext> context;
	device->GetImmediateContext(&context);

	D3D11_TEXTURE2D_DESC desc;
	backBuffer->GetDesc(&desc);
	const DXGI_FORMAT format = desc.Format;

	// A multisampled back buffer is resolved first
	Com<ID3D11Texture2D> resolved;
	ID3D11Texture2D* source = backBuffer.ptr;
	if (desc.SampleDesc.Count > 1)
	{
		D3D11_TEXTURE2D_DESC resolveDesc = desc;
		resolveDesc.SampleDesc.Count = 1;
		resolveDesc.SampleDesc.Quality = 0;
		resolveDesc.BindFlags = 0;
		resolveDesc.MiscFlags = 0;
		if (FAILED(device->CreateTexture2D(&resolveDesc, nullptr, &resolved)))
		{
			error = "Could not create a resolve texture";
			return false;
		}
		context->ResolveSubresource(resolved.ptr, 0, backBuffer.ptr, 0, format);
		source = resolved.ptr;
	}

	D3D11_TEXTURE2D_DESC stagingDesc = desc;
	stagingDesc.SampleDesc.Count = 1;
	stagingDesc.SampleDesc.Quality = 0;
	stagingDesc.BindFlags = 0;
	stagingDesc.MiscFlags = 0;
	stagingDesc.Usage = D3D11_USAGE_STAGING;
	stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
	stagingDesc.MipLevels = 1;
	stagingDesc.ArraySize = 1;
	Com<ID3D11Texture2D> staging;
	if (FAILED(device->CreateTexture2D(&stagingDesc, nullptr, &staging)))
	{
		error = "Could not create a staging texture";
		return false;
	}
	context->CopyResource(staging.ptr, source);

	D3D11_MAPPED_SUBRESOURCE mapped;
	if (FAILED(context->Map(staging.ptr, 0, D3D11_MAP_READ, 0, &mapped)))
	{
		error = "Could not read the back buffer";
		return false;
	}

	std::vector<uint8_t> bgr(static_cast<size_t>(desc.Width) * desc.Height * 3);
	bool supported = true;
	for (UINT y = 0; y < desc.Height && supported; y++)
	{
		const uint8_t* row = static_cast<const uint8_t*>(mapped.pData) + static_cast<size_t>(y) * mapped.RowPitch;
		uint8_t* out = bgr.data() + static_cast<size_t>(y) * desc.Width * 3;
		for (UINT x = 0; x < desc.Width; x++)
		{
			uint8_t r, g, b;
			switch (format)
			{
			case DXGI_FORMAT_R8G8B8A8_UNORM:
			case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
			case DXGI_FORMAT_R8G8B8A8_TYPELESS:
				r = row[x * 4]; g = row[x * 4 + 1]; b = row[x * 4 + 2];
				break;
			case DXGI_FORMAT_B8G8R8A8_UNORM:
			case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:
			case DXGI_FORMAT_B8G8R8A8_TYPELESS:
			case DXGI_FORMAT_B8G8R8X8_UNORM:
				b = row[x * 4]; g = row[x * 4 + 1]; r = row[x * 4 + 2];
				break;
			case DXGI_FORMAT_R10G10B10A2_UNORM:
			{
				uint32_t value;
				memcpy(&value, row + x * 4, 4);
				r = static_cast<uint8_t>((value & 0x3FF) >> 2);
				g = static_cast<uint8_t>(((value >> 10) & 0x3FF) >> 2);
				b = static_cast<uint8_t>(((value >> 20) & 0x3FF) >> 2);
				break;
			}
			default:
				supported = false;
				r = g = b = 0;
				break;
			}
			out[x * 3] = b;
			out[x * 3 + 1] = g;
			out[x * 3 + 2] = r;
		}
	}
	context->Unmap(staging.ptr, 0);

	if (!supported)
	{
		error = "Unsupported back buffer format " + std::to_string(static_cast<int>(format));
		return false;
	}
	if (!WriteBmp(path, desc.Width, desc.Height, bgr))
	{
		error = "Could not write " + path;
		return false;
	}
	return true;
}
