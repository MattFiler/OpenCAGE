#include "RENDER_COMMON.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <d3d11.h>

#include <imgui.h>
#include <backends/imgui_impl_dx11.h>

/*
	The debug menu's Rendering > Targets tab: the renderer's own render targets and textures as pictures - the G-buffer's
	targets and depth, linear depth, normals, velocity, ambient occlusion, the secondary (bounce) light, distortion, the
	shadow maps of this frame's shadow-casting lights and their cache, the bounce light's lightmaps - shown as thumbnails,
	one of them large.

	Each is one of the game's texture objects, kept in the G-buffer manager, the post-processing object, the shadow manager
	and the radiosity object (at fixed places in them, or as pointers). A texture object holds handles into the graphics
	device's pools: one into the texture pool (whose entries are the Direct3D resource, its shader resource view and its
	unordered access view, 12 bytes each) and, for a render target, one into the render target pool (the texture, its
	render target view, shader resource view and unordered access view, 16 bytes each). The shader resource view found
	there is drawn with the overlay's own image drawing (ImGui), at the frame's presentation, when the frame is finished
	and its targets still hold what it drew (and are no longer bound as targets: the overlay draws to the back buffer).

	A view is only drawn once it checks out: its virtual table must be the same as the overlay's own font texture's (a
	shader resource view made by the same device), and it must view a plain 2D texture (cube maps, volume textures and
	multisampled targets are listed but not drawn). Each view drawn is held (a reference) until the next frame, so the
	game letting go of it meanwhile can't free it under the overlay; it is found and held under the game's own lock on its
	pools, which the game holds as it lets go of a texture (on whichever thread does), so it can't go between the two.
	The pictures show the raw values: floating-point targets are clamped to 0-1, single-channel ones (depth, ambient
	occlusion) show in red only, and depth crowds close to 1 (almost solid red). They are drawn opaque - the alpha of
	most targets is data, not coverage (the G-buffer keeps the bounce light's lightmap coordinates in it) - by a blend
	state set from the overlay's draw list just for the pictures. A target with nothing to draw says why: not made (no
	texture in the pool), made without a view, a cube map...

	The G-buffer's five targets hold, in order, the glow (emissive), the normals, the surface colour (albedo), the shine
	(specular) and the specular normals, the first four with part of the lightmap coordinates in their alpha. The G-buffer
	manager also has room for signed normals and two linear depths, but the PC game never makes them (it reads the depth
	target itself), so they are not listed.

	Names: switch "render.targets.window" (the large view as a window of its own); commands "render.targets.list",
	"render.targets.show" (by name, or a part of one: a whole name first, then the first match that has something to draw).

	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	using namespace RENDER_COMMON;

	// The graphics device (a pointer), and its pools' shader resource views
	constexpr uintptr_t kGfxDevice = 0x017DF5CC;
	constexpr uint32_t kTexturePoolViews = 0x1BF240;      // + handle * 12
	constexpr uint32_t kTextureEntrySize = 12;
	constexpr uint32_t kTargetPoolViews = 0x1CD248;       // + handle * 16
	constexpr uint32_t kTargetEntrySize = 16;
	constexpr uint16_t kPoolSize = 0x1000;                // entries in each pool (handle 0 is none)
	// The game's lock on its graphics pools (a critical section), held as it makes and lets go of textures
	constexpr uintptr_t kGfxLock = 0x017DF5D4;
	// A texture object's handles: its current texture, its render target
	constexpr uint32_t kTextureCurrent = 0x5C;
	constexpr uint32_t kTextureRenderTarget = 0x5E;
	constexpr uint32_t kTextureHeader = 0x68;
	constexpr uint32_t kHeaderFileName = 0x28;
	constexpr uint32_t kTextureObjectSize = 0x74;
	// A render target object: its texture object
	constexpr uint32_t kTargetTexture = 0xC;

	// The shadow manager, and its textures
	constexpr uintptr_t kShadowManager = 0x012AEDB0;

	// Where a texture object is: an offset in one of the objects, or a pointer to it there
	enum class Owner { GBuffer, Post, Shadows, Radiosity };
	struct Source
	{
		const char* name;
		const char* group;
		Owner owner;
		uint32_t offset;
		bool pointer;  // a pointer to the texture object is at offset (else the object itself)
	};
	const Source kSources[] = {
		{ "G-buffer 0 (emissive)", "G-buffer", Owner::GBuffer, 0x14 + 0 * kTextureObjectSize, false },
		{ "G-buffer 1 (normals)", "G-buffer", Owner::GBuffer, 0x14 + 1 * kTextureObjectSize, false },
		{ "G-buffer 2 (albedo)", "G-buffer", Owner::GBuffer, 0x14 + 2 * kTextureObjectSize, false },
		{ "G-buffer 3 (specular)", "G-buffer", Owner::GBuffer, 0x14 + 3 * kTextureObjectSize, false },
		{ "G-buffer 4 (specular normals)", "G-buffer", Owner::GBuffer, 0x14 + 4 * kTextureObjectSize, false },
		{ "Depth", "G-buffer", Owner::GBuffer, 0x14 + 0x250, false },
		{ "Normals (copy)", "G-buffer", Owner::GBuffer, 0x8B0, false },
		{ "Specular normals (copy)", "G-buffer", Owner::GBuffer, 0x924, false },
		{ "Velocity", "G-buffer", Owner::GBuffer, 0x6D0, false },
		{ "Velocity (half)", "G-buffer", Owner::GBuffer, 0x748, false },
		{ "Ambient occlusion", "Lighting", Owner::GBuffer, 0xC64 + kTargetTexture, false },
		{ "Ambient occlusion (pass)", "Lighting", Owner::GBuffer, 0xCE8 + kTargetTexture, false },
		{ "Secondary light", "Lighting", Owner::GBuffer, 0x4E8, false },
		{ "Secondary light (copy)", "Lighting", Owner::GBuffer, 0x560, false },
		{ "Secondary light (half)", "Lighting", Owner::GBuffer, 0x5D8, false },
		{ "Lit scene with early transparency", "Lighting", Owner::GBuffer, 0xD88 + kTargetTexture, false },
		{ "Target", "Frame", Owner::GBuffer, 0x4E0, true },
		{ "Distortion", "Frame", Owner::GBuffer, 0x658, false },
		{ "Low-res particles", "Frame", Owner::GBuffer, 0x7C0, false },
		{ "Low-res depth", "Frame", Owner::GBuffer, 0x838, false },
		{ "Intermediate depth", "Frame", Owner::GBuffer, 0x46C, false },
		{ "Quarter size 1", "Frame", Owner::GBuffer, 0x998, false },
		{ "Quarter size 2", "Frame", Owner::GBuffer, 0xA10, false },
		{ "Frame 2x2", "Frame", Owner::Post, 0xC + kTargetTexture, false },
		{ "Motion blur / legacy DOF", "Frame", Owner::Post, 0x90 + kTargetTexture, false },
		{ "Current LUT", "Frame", Owner::Post, 0x5D4, true },
		{ "Target LUT", "Frame", Owner::Post, 0x5D8, true },
		{ "Shadow depth 0", "Shadows", Owner::Shadows, 0x18FC + 0 * 4, true },
		{ "Shadow depth 1", "Shadows", Owner::Shadows, 0x18FC + 1 * 4, true },
		{ "Shadow depth 2", "Shadows", Owner::Shadows, 0x18FC + 2 * 4, true },
		{ "Shadow depth 3", "Shadows", Owner::Shadows, 0x18FC + 3 * 4, true },
		{ "Shadow depth 4", "Shadows", Owner::Shadows, 0x18FC + 4 * 4, true },
		{ "Shadow filtered 0", "Shadows", Owner::Shadows, 0x1924 + 0 * 4, true },
		{ "Shadow filtered 1", "Shadows", Owner::Shadows, 0x1924 + 1 * 4, true },
		{ "Shadow filtered 2", "Shadows", Owner::Shadows, 0x1924 + 2 * 4, true },
		{ "Shadow filtered 3", "Shadows", Owner::Shadows, 0x1924 + 3 * 4, true },
		{ "Shadow filtered 4", "Shadows", Owner::Shadows, 0x1924 + 4 * 4, true },
		{ "Shadow cache 0", "Shadows", Owner::Shadows, 0x1878 + 0 * 0x20 + 4, true },
		{ "Shadow cache 1", "Shadows", Owner::Shadows, 0x1878 + 1 * 0x20 + 4, true },
		{ "Shadow cache 2", "Shadows", Owner::Shadows, 0x1878 + 2 * 0x20 + 4, true },
		{ "Shadow cache 3", "Shadows", Owner::Shadows, 0x1878 + 3 * 0x20 + 4, true },
		{ "Gobo (projected)", "Shadows", Owner::Shadows, 0x194C, true },
		{ "Torch gobo", "Shadows", Owner::Shadows, 0x1950, true },
		{ "Radiosity lightmap 0", "Radiosity", Owner::Radiosity, 0x4818 + 0 * kTextureObjectSize, false },
		{ "Radiosity lightmap 1", "Radiosity", Owner::Radiosity, 0x4818 + 1 * kTextureObjectSize, false },
		{ "Radiosity lightmap 2", "Radiosity", Owner::Radiosity, 0x4818 + 2 * kTextureObjectSize, false },
		{ "Radiosity lightmap 3", "Radiosity", Owner::Radiosity, 0x4818 + 3 * kTextureObjectSize, false },
		{ "Object probes (light)", "Radiosity", Owner::Radiosity, 0x8C + kTargetTexture, false },
		{ "Object probes (direction)", "Radiosity", Owner::Radiosity, 0x110 + kTargetTexture, false },
	};
	constexpr int kSourceCount = static_cast<int>(sizeof(kSources) / sizeof(kSources[0]));

	struct Target
	{
		ID3D11ShaderResourceView* view = nullptr; // held until the next frame
		bool drawable = false;                    // a plain 2D texture
		UINT width = 0, height = 0, arraySize = 0, mips = 0;
		DXGI_FORMAT format = DXGI_FORMAT_UNKNOWN;
		D3D11_SRV_DIMENSION dimension = D3D11_SRV_DIMENSION_UNKNOWN;
		std::string fileName;
		std::string why;                          // why there is nothing to draw
		const char* shortWhy = "";                // the same in a word or two, for its tile
		uint16_t targetHandle = 0, textureHandle = 0; // its handles in the device's pools (0: none)
	};

	std::vector<ID3D11ShaderResourceView*> g_held;   // views held for the frame being drawn
	std::atomic<bool> g_window = false;
	std::mutex g_selectedMutex;
	std::string g_selected = "G-buffer 0 (emissive)";
	float g_zoom = 1.0f;
	bool g_registered = false;

	uintptr_t OwnerAddress(Owner owner)
	{
		switch (owner)
		{
		case Owner::GBuffer: { const uintptr_t post = Game(kPostProcessing); return post ? post + kPostGBuffer : 0; }
		case Owner::Post: return Game(kPostProcessing);
		case Owner::Shadows: return Game(kShadowManager);
		default: { const uintptr_t engine = Engine(); return engine ? engine + kEngineRadiosity : 0; }
		}
	}

	// The overlay's own font texture's view, whose virtual table every view of this device shares
	void* KnownViewTable()
	{
		const ImTextureData* atlas = ImGui::GetIO().Fonts ? ImGui::GetIO().Fonts->TexData : nullptr;
		ID3D11ShaderResourceView* font = atlas ? reinterpret_cast<ID3D11ShaderResourceView*>(static_cast<uintptr_t>(atlas->TexID)) : nullptr;
		uintptr_t table = 0;
		return font && Read(reinterpret_cast<uintptr_t>(font), table) ? reinterpret_cast<void*>(table) : nullptr;
	}

	// The view a texture object has (its render target's, else its texture's): 0 when none. Its handles are given back
	// (0 for none) so an empty target can say which it has.
	uintptr_t ViewOf(uintptr_t texture, uintptr_t device, uint16_t& target, uint16_t& current)
	{
		target = 0;
		current = 0;
		if (!Read(texture + kTextureRenderTarget, target) || target >= kPoolSize)
			target = 0;
		if (!Read(texture + kTextureCurrent, current) || current >= kPoolSize)
			current = 0;
		if (target)
			if (const uintptr_t view = Pointer(device + kTargetPoolViews + target * kTargetEntrySize))
				return view;
		if (current)
			return Pointer(device + kTexturePoolViews + current * kTextureEntrySize);
		return 0;
	}

	// The view a texture object has, held (a reference of ours): found, checked and held under the game's lock on its
	// pools, so the game can't let go of it in between. Null, with why, when there is none this overlay can draw.
	ID3D11ShaderResourceView* HoldView(uintptr_t texture, uintptr_t device, Target& out)
	{
		CRITICAL_SECTION* lock = reinterpret_cast<CRITICAL_SECTION*>(Game(kGfxLock));
		void* known = KnownViewTable();
		if (!lock || !known)
		{
			out.why = "the overlay can't check the game's views on this game release";
			out.shortWhy = "not available";
			return nullptr;
		}
		ID3D11ShaderResourceView* srv = nullptr;
		EnterCriticalSection(lock);
		const uintptr_t view = ViewOf(texture, device, out.targetHandle, out.textureHandle);
		uintptr_t table = 0;
		if (view && Read(view, table) && reinterpret_cast<void*>(table) == known)
		{
			srv = reinterpret_cast<ID3D11ShaderResourceView*>(view);
			srv->AddRef();
		}
		LeaveCriticalSection(lock);
		if (srv)
			return srv;
		if (view)
		{
			out.why = "its view is not one the overlay can draw";
			out.shortWhy = "no drawable view";
		}
		else if (out.targetHandle || out.textureHandle)
		{
			out.why = "made, but without a view to draw it with";
			out.shortWhy = "no view";
		}
		else
		{
			out.why = "not made (nothing in the device's pools for it now)";
			out.shortWhy = "not made";
		}
		return nullptr;
	}

	// What a view says about itself and its texture
	struct ViewInfo
	{
		DXGI_FORMAT format = DXGI_FORMAT_UNKNOWN;
		D3D11_SRV_DIMENSION dimension = D3D11_SRV_DIMENSION_UNKNOWN;
		UINT width = 0, height = 0, arraySize = 0, mips = 0;
	};

	// Asks a view about itself, guarded (a view let go of meanwhile shows as unreadable rather than crashing)
	bool QueryView(ID3D11ShaderResourceView* srv, ViewInfo& out)
	{
		__try
		{
			D3D11_SHADER_RESOURCE_VIEW_DESC desc = {};
			srv->GetDesc(&desc);
			out.format = desc.Format;
			out.dimension = desc.ViewDimension;
			ID3D11Resource* resource = nullptr;
			srv->GetResource(&resource);
			if (resource)
			{
				D3D11_RESOURCE_DIMENSION kind = D3D11_RESOURCE_DIMENSION_UNKNOWN;
				resource->GetType(&kind);
				if (kind == D3D11_RESOURCE_DIMENSION_TEXTURE2D)
				{
					D3D11_TEXTURE2D_DESC textureDesc = {};
					static_cast<ID3D11Texture2D*>(resource)->GetDesc(&textureDesc);
					out.width = textureDesc.Width;
					out.height = textureDesc.Height;
					out.arraySize = textureDesc.ArraySize;
					out.mips = textureDesc.MipLevels;
				}
				else if (kind == D3D11_RESOURCE_DIMENSION_TEXTURE3D)
				{
					D3D11_TEXTURE3D_DESC textureDesc = {};
					static_cast<ID3D11Texture3D*>(resource)->GetDesc(&textureDesc);
					out.width = textureDesc.Width;
					out.height = textureDesc.Height;
					out.arraySize = textureDesc.Depth;
					out.mips = textureDesc.MipLevels;
				}
				resource->Release();
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void NewFrame();

	// Render thread: the target as it is now (its view held until the next frame when there is one)
	Target Find(const Source& source)
	{
		Target out;
		const uintptr_t owner = OwnerAddress(source.owner);
		const uintptr_t device = Pointer(Game(kGfxDevice));
		if (!owner || !device)
		{
			out.why = "not available (no level or renderer?)";
			out.shortWhy = "not available";
			return out;
		}
		const uintptr_t texture = source.pointer ? Pointer(owner + source.offset) : owner + source.offset;
		if (!texture)
		{
			out.why = "not made (none in use this frame)";
			out.shortWhy = "not made";
			return out;
		}
		const uintptr_t header = Pointer(texture + kTextureHeader);
		if (header)
			out.fileName = Text(Pointer(header + kHeaderFileName), 200);
		ID3D11ShaderResourceView* srv = HoldView(texture, device, out);
		if (!srv)
			return out;
		// Ours now until the next frame, drawn or not: let go at the start of the next frame's draw, wherever it was found
		// (the tab, the large window or a command), so none is kept once nothing looks
		if (g_held.empty())
			DEBUG_MENU::OnRenderThread([]() { NewFrame(); });
		g_held.push_back(srv);
		ViewInfo info;
		if (!QueryView(srv, info))
		{
			out.why = "its view can't be read";
			out.shortWhy = "not readable";
			return out;
		}
		out.format = info.format;
		out.dimension = info.dimension;
		out.width = info.width;
		out.height = info.height;
		out.arraySize = info.arraySize;
		out.mips = info.mips;
		out.drawable = info.dimension == D3D11_SRV_DIMENSION_TEXTURE2D;
		if (!out.drawable)
		{
			out.shortWhy = info.dimension == D3D11_SRV_DIMENSION_TEXTURECUBE ? "cube map" :
				info.dimension == D3D11_SRV_DIMENSION_TEXTURE3D ? "volume texture" :
				info.dimension == D3D11_SRV_DIMENSION_TEXTURE2DMS ? "multisampled" : "not plain 2D";
			out.why = std::string(info.dimension == D3D11_SRV_DIMENSION_TEXTURECUBE ? "a cube map" :
				info.dimension == D3D11_SRV_DIMENSION_TEXTURE3D ? "a volume texture" :
				info.dimension == D3D11_SRV_DIMENSION_TEXTURE2DMS ? "multisampled" : "not a plain 2D texture") + " (not drawn)";
			return out;
		}
		out.view = srv;
		return out;
	}

	// The views held let go
	void ReleaseHeld()
	{
		for (ID3D11ShaderResourceView* view : g_held)
			view->Release();
		g_held.clear();
	}

	// Once a frame, before the first view is found: the views held for the last frame let go (it has been drawn)
	int g_heldFrame = -1;
	void NewFrame()
	{
		const int frame = ImGui::GetFrameCount();
		if (frame == g_heldFrame)
			return;
		g_heldFrame = frame;
		ReleaseHeld();
	}

	ImTextureRef TextureOf(ID3D11ShaderResourceView* view)
	{
		return ImTextureRef(static_cast<ImTextureID>(reinterpret_cast<uintptr_t>(view)));
	}

	// The pictures are drawn opaque: a target's alpha is mostly data (the G-buffer keeps the lightmap coordinates in it),
	// and blended with it a picture would show the game through it. A callback in the overlay's draw list, run as the
	// overlay is drawn (render thread), sets a blend state with blending off that writes only the colour, just before a
	// picture; the overlay's own state is put back just after it.
	ID3D11BlendState* g_opaque = nullptr;
	ID3D11Device* g_opaqueDevice = nullptr;  // the device it was made on (made again for another)

	void SetOpaque(const ImDrawList* /*list*/, const ImDrawCmd* /*command*/)
	{
		ImGui_ImplDX11_RenderState* state = static_cast<ImGui_ImplDX11_RenderState*>(ImGui::GetPlatformIO().Renderer_RenderState);
		if (!state || !state->Device || !state->DeviceContext)
			return;
		if (state->Device != g_opaqueDevice)
		{
			// (One made on a device gone is not let go of: that device may be gone with it)
			g_opaque = nullptr;
			g_opaqueDevice = state->Device;
			D3D11_BLEND_DESC desc = {};
			desc.RenderTarget[0].BlendEnable = FALSE;
			desc.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_RED | D3D11_COLOR_WRITE_ENABLE_GREEN | D3D11_COLOR_WRITE_ENABLE_BLUE;
			if (FAILED(state->Device->CreateBlendState(&desc, &g_opaque)))
				g_opaque = nullptr;
		}
		if (g_opaque)
		{
			const float factor[4] = { 0.0f, 0.0f, 0.0f, 0.0f };
			state->DeviceContext->OMSetBlendState(g_opaque, factor, 0xFFFFFFFF);
		}
	}

	// Brackets a picture drawn in the current window: opaque from here to the end
	bool BeginOpaque()
	{
		const ImDrawCallback reset = ImGui::GetPlatformIO().DrawCallback_ResetRenderState;
		if (!reset)
			return false;
		ImGui::GetWindowDrawList()->AddCallback(SetOpaque, nullptr);
		return true;
	}
	void EndOpaque(bool begun)
	{
		if (begun)
			ImGui::GetWindowDrawList()->AddCallback(ImGui::GetPlatformIO().DrawCallback_ResetRenderState, nullptr);
	}

	// A picture as a button, laid out as the overlay's own picture buttons (a frame round the picture), with only the
	// picture opaque: the frame keeps the window's see-through button colours, so one under the mouse still stands out
	bool OpaqueImageButton(const char* id, ImTextureRef texture, const ImVec2& size)
	{
		const ImVec2 padding = ImGui::GetStyle().FramePadding;
		const bool clicked = ImGui::InvisibleButton(id, ImVec2(size.x + padding.x * 2.0f, size.y + padding.y * 2.0f));
		if (!ImGui::IsItemVisible())
			return clicked;
		const ImVec2 min = ImGui::GetItemRectMin(), max = ImGui::GetItemRectMax();
		const bool hovered = ImGui::IsItemHovered();
		ImDrawList* drawList = ImGui::GetWindowDrawList();
		drawList->AddRectFilled(min, max, ImGui::GetColorU32(hovered && ImGui::IsItemActive() ? ImGuiCol_ButtonActive : hovered ? ImGuiCol_ButtonHovered : ImGuiCol_Button), ImGui::GetStyle().FrameRounding);
		const bool opaque = BeginOpaque();
		drawList->AddImage(texture, ImVec2(min.x + padding.x, min.y + padding.y), ImVec2(max.x - padding.x, max.y - padding.y));
		EndOpaque(opaque);
		return clicked;
	}

	const char* FormatName(DXGI_FORMAT format)
	{
		switch (format)
		{
		case DXGI_FORMAT_R8G8B8A8_UNORM: return "RGBA8";
		case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB: return "RGBA8 sRGB";
		case DXGI_FORMAT_B8G8R8A8_UNORM: return "BGRA8";
		case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB: return "BGRA8 sRGB";
		case DXGI_FORMAT_R10G10B10A2_UNORM: return "RGB10A2";
		case DXGI_FORMAT_R11G11B10_FLOAT: return "R11G11B10F";
		case DXGI_FORMAT_R16G16B16A16_FLOAT: return "RGBA16F";
		case DXGI_FORMAT_R16G16B16A16_UNORM: return "RGBA16";
		case DXGI_FORMAT_R16G16_FLOAT: return "RG16F";
		case DXGI_FORMAT_R16G16_UNORM: return "RG16";
		case DXGI_FORMAT_R16_FLOAT: return "R16F";
		case DXGI_FORMAT_R32_FLOAT: return "R32F";
		case DXGI_FORMAT_R32G32_FLOAT: return "RG32F";
		case DXGI_FORMAT_R32G32B32A32_FLOAT: return "RGBA32F";
		case DXGI_FORMAT_R8_UNORM: return "R8";
		case DXGI_FORMAT_R8G8_UNORM: return "RG8";
		case DXGI_FORMAT_R24_UNORM_X8_TYPELESS: return "depth 24";
		case DXGI_FORMAT_R32_FLOAT_X8X24_TYPELESS: return "depth 32";
		case DXGI_FORMAT_BC1_UNORM: return "BC1";
		case DXGI_FORMAT_BC1_UNORM_SRGB: return "BC1 sRGB";
		case DXGI_FORMAT_BC3_UNORM: return "BC3";
		case DXGI_FORMAT_BC3_UNORM_SRGB: return "BC3 sRGB";
		case DXGI_FORMAT_BC5_UNORM: return "BC5";
		case DXGI_FORMAT_BC7_UNORM: return "BC7";
		default: return nullptr;
		}
	}

	std::string Describe(const Target& target)
	{
		char text[160];
		if (!target.width)
		{
			std::string out = target.why.empty() ? "nothing to draw" : target.why;
			// The handles it has, for a target made without a view
			if (target.targetHandle || target.textureHandle)
			{
				out += " (render target ";
				out += target.targetHandle ? std::to_string(target.targetHandle) : std::string("none");
				out += ", texture ";
				out += target.textureHandle ? std::to_string(target.textureHandle) : std::string("none");
				out += " in the device's pools)";
			}
			return out;
		}
		const char* format = FormatName(target.format);
		char formatNumber[16];
		snprintf(formatNumber, sizeof(formatNumber), "format %d", static_cast<int>(target.format));
		snprintf(text, sizeof(text), "%ux%u%s %s%s%s", target.width, target.height, target.arraySize > 1 ? (" x" + std::to_string(target.arraySize)).c_str() : "",
			format ? format : formatNumber, target.mips > 1 ? (", " + std::to_string(target.mips) + " mips").c_str() : "", target.why.empty() ? "" : (", " + target.why).c_str());
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

	std::string Selected()
	{
		std::lock_guard<std::mutex> lock(g_selectedMutex);
		return g_selected;
	}

	void Select(const std::string& name)
	{
		std::lock_guard<std::mutex> lock(g_selectedMutex);
		g_selected = name;
	}

	// The large view of the selected target, its name and size slider included, in width x maxHeight: at size 1 the whole
	// picture fits in what is left of that (as wide as it is, or as tall), with no scroll bars; made bigger than that, it
	// scrolls within that space
	void DrawLarge(float width, float maxHeight)
	{
		const std::string selected = Selected();
		for (const Source& source : kSources)
		{
			if (selected != source.name)
				continue;
			const float top = ImGui::GetCursorPosY();
			const Target target = Find(source);
			ImGui::TextWrapped("%s - %s%s%s", source.name, Describe(target).c_str(), target.fileName.empty() ? "" : ", ", target.fileName.c_str());
			if (!target.view || !target.width || !target.height)
				return;
			ImGui::SetNextItemWidth(ImGui::GetFontSize() * 10.0f);
			ImGui::SliderFloat("Size (1 = the whole picture)##targets", &g_zoom, 0.25f, 4.0f, "%.2fx");
			Tooltip("At 1 the whole picture fits in the space here; above 1 it is drawn bigger and scrolls.");
			const float box = (std::max)(maxHeight - (ImGui::GetCursorPosY() - top) - ImGui::GetStyle().ItemSpacing.y, ImGui::GetFontSize() * 6.0f);
			const float aspect = static_cast<float>(target.height) / static_cast<float>(target.width);
			const float fit = (std::min)(width, box / aspect);
			const ImVec2 shown(fit * g_zoom, fit * g_zoom * aspect);
			if (shown.x <= width + 0.5f && shown.y <= box + 0.5f)
			{
				const bool opaque = BeginOpaque();
				ImGui::Image(TextureOf(target.view), shown);
				EndOpaque(opaque);
				return;
			}
			// Bigger than the space: scrolled within it (no taller than the picture and its scroll bar)
			const float height = (std::min)(box, shown.y + (shown.x > width ? ImGui::GetStyle().ScrollbarSize : 0.0f) + 1.0f);
			if (ImGui::BeginChild("large##targets", ImVec2(width, height), ImGuiChildFlags_None, ImGuiWindowFlags_HorizontalScrollbar))
			{
				const bool opaque = BeginOpaque();
				ImGui::Image(TextureOf(target.view), shown);
				EndOpaque(opaque);
			}
			ImGui::EndChild();
			return;
		}
	}

	void DrawTab()
	{
		NewFrame();
		ImGui::TextWrapped("The renderer's own targets and textures as this frame left them: click one to see it large. The raw values are shown, without their alpha: floating-point targets are clamped to 0-1, and single-channel ones show in red only - depth crowds close to 1, so it shows as almost solid red. An empty tile says why it has nothing to draw; a black picture is a target with nothing above zero in its red, green and blue this frame (velocity while nothing moves, for one).");
		if (!Pointer(Game(kGfxDevice)) || !Game(kPostProcessing))
		{
			ColouredNote(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "Not available on this game release.");
			return;
		}
		bool window = g_window;
		if (ImGui::Checkbox("Large view in its own window##targets", &window))
			g_window = window;
		Tooltip("render.targets.window: the selected target in a window of its own, which stays open with the debug menu closed.");

		const float thumb = ImGui::GetFontSize() * 8.0f;
		if (!g_window)
		{
			// What is left of the window less room for the first row of targets (its heading, 16:9 tiles and names up to three
			// lines), so the targets to pick from stay in sight below it
			DrawLarge(ImGui::GetContentRegionAvail().x, ImGui::GetContentRegionAvail().y - thumb * 0.5625f - ImGui::GetFontSize() * 6.0f);
			ImGui::Separator();
		}

		const std::string selected = Selected();
		const char* group = nullptr;
		// A tile is its picture and the frame round it; one goes on the row only when all of it fits
		const float tile = thumb + ImGui::GetStyle().FramePadding.x * 2.0f;
		float x = 0.0f;  // where the row's next tile would start
		const float available = ImGui::GetContentRegionAvail().x;
		for (const Source& source : kSources)
		{
			if (!group || strcmp(group, source.group) != 0)
			{
				group = source.group;
				ImGui::SeparatorText(group);
				x = 0.0f;
			}
			const Target target = Find(source);
			if (x > 0.0f && x + tile <= available)
				ImGui::SameLine();
			else
				x = 0.0f;
			x += tile + ImGui::GetStyle().ItemSpacing.x;
			ImGui::BeginGroup();
			ImGui::PushID(source.name);
			const float aspect = target.width ? static_cast<float>(target.height) / static_cast<float>(target.width) : 0.5625f;
			const ImVec2 size(thumb, thumb * (std::min)(aspect, 1.0f));
			if (target.view)
			{
				if (OpaqueImageButton("##thumb", TextureOf(target.view), size))
					Select(source.name);
			}
			else
			{
				// Why there is nothing to draw, on the tile itself (in full in its tooltip)
				char label[64];
				snprintf(label, sizeof(label), "%s##thumb", target.shortWhy[0] ? target.shortWhy : "nothing");
				ImGui::BeginDisabled();
				ImGui::Button(label, ImVec2(size.x + ImGui::GetStyle().FramePadding.x * 2.0f, size.y + ImGui::GetStyle().FramePadding.y * 2.0f));
				ImGui::EndDisabled();
			}
			const std::string tip = std::string(source.name) + ": " + Describe(target) + (target.fileName.empty() ? "" : "\n" + target.fileName);
			Tooltip(tip.c_str());
			ImGui::PushTextWrapPos(ImGui::GetCursorPosX() + thumb);
			if (selected == source.name)
				ImGui::TextColored(ImVec4(1.0f, 0.85f, 0.3f, 1.0f), "%s", source.name);
			else
				ImGui::TextUnformatted(source.name);
			ImGui::PopTextWrapPos();
			ImGui::PopID();
			ImGui::EndGroup();
		}
	}

	// The large view's own window (render thread, every frame while switched on)
	void Overlay(ImDrawList* /*drawList*/)
	{
		if (!g_window)
			return;
		NewFrame();
		const float scale = DEBUG_MENU::Scale();
		ImGui::SetNextWindowSize(ImVec2(640.0f * scale, 420.0f * scale), ImGuiCond_FirstUseEver);
		// First opened beside the debug menu rather than over it
		ImVec2 menuMin, menuMax;
		if (DEBUG_MENU::WindowRect(menuMin, menuMax))
			ImGui::SetNextWindowPos(ImVec2((std::min)(menuMax.x + 8.0f, ImGui::GetIO().DisplaySize.x - 640.0f * scale), menuMin.y), ImGuiCond_FirstUseEver);
		bool open = true;
		if (ImGui::Begin("Render target###RenderTargetView", &open, ImGuiWindowFlags_NoSavedSettings))
		{
			ImGui::SetWindowFontScale(scale);
			DrawLarge(ImGui::GetContentRegionAvail().x, ImGui::GetContentRegionAvail().y);
		}
		ImGui::End();
		if (!open)
			g_window = false;
	}

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddSwitch("render.targets.window", &g_window);
		DEBUG_MENU::AddCommand("render.targets.list", "the render targets and textures the Targets tab shows: name, size and format (or why there is nothing), a line each", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				// The views this holds are let go at the next frame like the tab's (with the tab not drawn, nothing else would)
				NewFrame();
				std::string text;
				for (const Source& source : kSources)
				{
					const Target target = Find(source);
					text += std::string(source.name) + ": " + Describe(target) + (target.fileName.empty() ? "" : " (" + target.fileName + ")") + "\n";
				}
				return { true, text };
			});
		});
		DEBUG_MENU::AddCommand("render.targets.show", "shows a target large in the Targets tab: its whole name, else a part of one (of the targets it is part of, the first with something to draw this frame)", [](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			const auto lower = [](std::string text)
			{
				for (char& c : text)
					c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
				return text;
			};
			const std::string wanted = lower(argument);
			if (wanted.empty())
				return { false, "Name a target (render.targets.list lists them)" };
			// A whole name wins; else every target whose name it is part of, in the tab's order
			std::vector<int> matches;
			for (int i = 0; i < kSourceCount; i++)
			{
				const std::string name = lower(kSources[i].name);
				if (name == wanted)
				{
					matches.assign(1, i);
					break;
				}
				if (name.find(wanted) != std::string::npos)
					matches.push_back(i);
			}
			if (matches.empty())
				return { false, "No target named like that (render.targets.list lists them)" };
			// Of those, the first with something to draw (looked at on the render thread, where the views are held)
			return DEBUG_MENU::AnswerOnRenderThread([matches]() -> DEBUG_MENU::CommandResult
			{
				NewFrame();
				for (int index : matches)
					if (Find(kSources[index]).view)
					{
						Select(kSources[index].name);
						return { true, std::string("Showing ") + kSources[index].name };
					}
				const Source& first = kSources[matches[0]];
				Select(first.name);
				return { true, std::string("Showing ") + first.name + ", which has nothing to draw: " + Describe(Find(first)) };
			});
		});
	}

	void AttachHooks(bool attach)
	{
		// The views held are not let go of here: the game is closing, and Direct3D may be gone already
		if (!attach)
			g_held.clear();
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Targets", 75, DrawTab, "Rendering");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(1000, Overlay);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachHooks);
	const bool g_namesAdded = (Register(), true);
}
