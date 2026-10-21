#include "CUBEMAP_CAPTURE.h"
#include "LIVE_LINK_SERVER.h"
#include "MOUSE_UNLOCK.h"
#include "ZONE_LOADER.h"
#include "Config.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <d3d11.h>

#include <DirectXMath.h>
#include <algorithm>
#include <atomic>
#include <clocale>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <unordered_map>

/*
	Layouts and addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// The cube faces in the order a cube texture keeps them, each as the camera looks for it: forward, and up. With the
	// game's camera (right = up x forward, left-handed, as Direct3D's own cube lookups are) these give each face picture
	// the right way round and up, so it goes into the texture as it is.
	struct Face { const char* name; float forward[3]; float up[3]; };
	constexpr Face kFaces[6] = {
		{ "+X", { 1.0f, 0.0f, 0.0f }, { 0.0f, 1.0f, 0.0f } },
		{ "-X", { -1.0f, 0.0f, 0.0f }, { 0.0f, 1.0f, 0.0f } },
		{ "+Y", { 0.0f, 1.0f, 0.0f }, { 0.0f, 0.0f, -1.0f } },
		{ "-Y", { 0.0f, -1.0f, 0.0f }, { 0.0f, 0.0f, 1.0f } },
		{ "+Z", { 0.0f, 0.0f, 1.0f }, { 0.0f, 1.0f, 0.0f } },
		{ "-Z", { 0.0f, 0.0f, -1.0f }, { 0.0f, 1.0f, 0.0f } },
	};
	constexpr float kFaceFov = 90.0f; // vertical, so the square as tall as the frame spans 90 degrees both ways

	// Streaming: the first face waits at least this long, then until the zones loaded around it have stayed the same for
	// kStreamStableMs. A zone counts as loaded if it was seen loaded in the last kZoneSeenMs: with the camera away from the
	// player a zone's loaded flag drops for a frame or two several times a second (seen: 20-50 ms at a time).
	constexpr ULONGLONG kStreamMinMs = 1500;
	constexpr ULONGLONG kStreamStableMs = 2000;
	constexpr ULONGLONG kZoneSeenMs = 400;
	// No camera frame or no frame presented for this long ends the capture: the game has stopped drawing
	constexpr ULONGLONG kStalledMs = 5000;
	// After the six faces are due, the most a capture can take on top of its streaming wait
	constexpr ULONGLONG kFacesMs = 60000;
	// ... and a tenth of a second more for each frame a face is held (the game running at 10 frames a second, say)
	constexpr ULONGLONG kSettleFrameMs = 100;
	// The answer's header: seven u32s (see CAPTURE_CUBEMAP in LIVE_LINK_SERVER.h)
	constexpr size_t kHeaderBytes = 7 * sizeof(uint32_t);

	// The game's globals for the checks below
	constexpr uintptr_t kEntityManagerInstance = 0x0134ef40; // where the game keeps its pointer to the entity manager
	constexpr uint32_t kManagerTransport = 0xBC;             // the level scripts' transport: 1 paused, 2 pause menu, 5 running
	constexpr uint32_t kTransportPauseMenu = 2;

	// The game's flash user interface (the HUD and the like): a pointer to its manager, which keeps its pieces in an
	// array; each piece has a flag that keeps it hidden whatever it would show, and a name
	constexpr uintptr_t kUiManagerInstance = 0x0134a78c;
	constexpr uint32_t kUiPieces = 0x38;      // the array of pieces (pointers)
	constexpr uint32_t kUiPieceCount = 0x3C;
	constexpr uint32_t kUiPieceHidden = 0x29; // set: the piece is not drawn
	constexpr uint32_t kMaxUiPieces = 256;

	// The post-processing settings, at this offset in what their per-frame update is called on, and the fields turned off
	constexpr uint32_t kPostProcessSettings = 0x1918;
	constexpr uint32_t kFilmGrainScales[] = { 0x98, 0x9C, 0xA0 };
	constexpr uint32_t kVignetteStrength = 0xD0;
	constexpr uint32_t kChromaticAberration = 0xD4;
	constexpr uint32_t kLensDistortion = 0xE0;
	constexpr uint32_t kDepthOfFieldStrength = 0x128;

	// The zone manager's zones (see ZONE_LOADER.h), and a zone's state: its first bit is set while it is loaded
	constexpr uint32_t kZoneState = 0x08;
	constexpr uint32_t kZoneLoaded = 1u << 0;
	constexpr int kMaxZones = 1024;

	enum class Phase { Idle, Streaming, Settling };

	struct Job
	{
		CUBEMAP_CAPTURE::Request request;
		CUBEMAP_CAPTURE::Done done;
		Phase phase = Phase::Idle;
		int face = 0;                  // the face being held (Settling), 0 while streaming
		float rotations[6][4] = {};
		ULONGLONG startedAt = 0;
		ULONGLONG streamedAt = 0;      // when streaming ended (0: still streaming)
		bool streamTimedOut = false;
		bool started = false;          // the render thread has set the job up (hidden the HUD...)
		std::unordered_map<uint32_t, ULONGLONG> zoneSeen; // zone id -> when it was last seen loaded
		size_t loadedCount = 0;
		ULONGLONG loadedSince = 0;     // when loadedCount last changed
		uint32_t presentsOnFace = 0;   // frames presented since the camera first rendered the held face
		std::vector<uint8_t> faces;    // the answer's header (filled in at the end), then the pictures taken so far, RGBA, face after face
		uint32_t frameWidth = 0, frameHeight = 0;
		std::vector<std::pair<uint8_t*, uint8_t>> hiddenUi; // each UI piece hidden, and its flag as it was
	};

	// The job, between the socket thread (Start), the camera hook (CameraPose) and the frame presentation (OnPresent)
	std::mutex g_mutex;
	Job g_job;
	std::atomic<bool> g_active = false;
	std::atomic<bool> g_postEffectsOff = false;
	// The face the camera hook last rendered from (-1: none yet), and when; and when a frame was last presented
	std::atomic<int> g_cameraFace = -1;
	std::atomic<ULONGLONG> g_cameraAt = 0;
	std::atomic<ULONGLONG> g_presentAt = 0;
	// For the overlay
	std::mutex g_textMutex;
	std::string g_overlay;

	void SetOverlay(const std::string& text)
	{
		std::lock_guard<std::mutex> lock(g_textMutex);
		g_overlay = text;
	}

	uint8_t* Address(uintptr_t steamOffset) { return reinterpret_cast<uint8_t*>(DEVTOOLS_RELATIVE_ADDRESS(steamOffset)); }

	// Plain reads of the game's state, guarded: it can go while a level unloads. -1: not readable.
	int ScriptTransport()
	{
		__try
		{
			const uint8_t* manager = *reinterpret_cast<uint8_t**>(Address(kEntityManagerInstance));
			return manager ? static_cast<int>(*reinterpret_cast<const uint32_t*>(manager + kManagerTransport)) : -1;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	bool AnyLevel()
	{
		__try
		{
			return LIVE_LINK::LevelRunning();
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool LevelIs(uint32_t root)
	{
		__try
		{
			return LIVE_LINK::LevelRunning() && LIVE_LINK::RunningLevelIs(root);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The ids of the zones loaded now, into ids (at most max); the count, or -1 if they can't be read
	int LoadedZones(uint32_t* ids, int max)
	{
		__try
		{
			const uint8_t* zoneManager = static_cast<const uint8_t*>(*ZONE_LOADER::zone_manager_instance);
			if (!zoneManager)
				return -1;
			const uint8_t* zones = *reinterpret_cast<uint8_t* const*>(zoneManager + ZONE_LOADER::kZoneArrayOffset);
			if (!zones)
				return -1;
			const int count = *reinterpret_cast<const int*>(zones + ZONE_LOADER::kArrayCountOffset);
			uint8_t* const* allocations = *reinterpret_cast<uint8_t** const*>(zones + ZONE_LOADER::kArrayDataOffset);
			if (!allocations || count < 0)
				return -1;
			int found = 0;
			for (int i = 0; i < count && found < max; i++)
			{
				const uint8_t* allocation = allocations[i];
				if (!allocation)
					continue;
				const uint8_t* zone = *reinterpret_cast<uint8_t* const*>(allocation + ZONE_LOADER::kAllocationObjectOffset);
				if (zone && (*reinterpret_cast<const uint32_t*>(zone + kZoneState) & kZoneLoaded))
					ids[found++] = *reinterpret_cast<const uint32_t*>(zone + ZONE_LOADER::kZoneIdOffset);
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	// The UI pieces' hide flags (pointers into the pieces, and their values now) into flags/values (at most max); the count
	int UiHideFlags(uint8_t** flags, uint8_t* values, int max)
	{
		__try
		{
			const uint8_t* manager = *reinterpret_cast<uint8_t* const*>(Address(kUiManagerInstance));
			if (!manager)
				return 0;
			uint8_t* const* pieces = *reinterpret_cast<uint8_t* const* const*>(manager + kUiPieces);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + kUiPieceCount);
			if (!pieces)
				return 0;
			int found = 0;
			for (uint32_t i = 0; i < count && found < max; i++)
			{
				if (!pieces[i])
					continue;
				flags[found] = pieces[i] + kUiPieceHidden;
				values[found] = *flags[found];
				found++;
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	void WriteByte(uint8_t* where, uint8_t value)
	{
		__try
		{
			*where = value;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	// The camera's quaternion for a face: the rows the camera hand-over makes of it are right, up, forward (as LIVE_CAMERA's
	// own conversion from axes), right = up x forward
	void FaceRotation(const Face& face, float rotation[4])
	{
		using namespace DirectX;
		const XMVECTOR f = XMVectorSet(face.forward[0], face.forward[1], face.forward[2], 0.0f);
		const XMVECTOR u = XMVectorSet(face.up[0], face.up[1], face.up[2], 0.0f);
		const XMVECTOR r = XMVector3Cross(u, f);
		const XMMATRIX axes(r, u, f, XMVectorSet(0.0f, 0.0f, 0.0f, 1.0f));
		XMFLOAT4 quaternion;
		XMStoreFloat4(&quaternion, XMQuaternionNormalize(XMQuaternionRotationMatrix(axes)));
		rotation[0] = quaternion.x;
		rotation[1] = quaternion.y;
		rotation[2] = quaternion.z;
		rotation[3] = quaternion.w;
	}

	template<typename T> struct Com
	{
		T* ptr = nullptr;
		~Com() { if (ptr) ptr->Release(); }
		T** operator&() { return &ptr; }
		T* operator->() { return ptr; }
	};

	// sRGB-encoded 8-bit to linear, and back: the face is averaged in linear light, so a small bright light shrinks
	// into the face's pixels with the energy it had rather than darker
	float g_toLinear[256];
	void BuildToLinear()
	{
		static bool built = false;
		if (built)
			return;
		for (int i = 0; i < 256; i++)
		{
			const float c = i / 255.0f;
			g_toLinear[i] = c <= 0.04045f ? c / 12.92f : std::pow((c + 0.055f) / 1.055f, 2.4f);
		}
		built = true;
	}
	uint8_t ToEncoded(float linear)
	{
		linear = linear < 0.0f ? 0.0f : linear > 1.0f ? 1.0f : linear;
		const float c = linear <= 0.0031308f ? linear * 12.92f : 1.055f * std::pow(linear, 1.0f / 2.4f) - 0.055f;
		return static_cast<uint8_t>(std::lround(c * 255.0f));
	}

	// Which source pixels (and how much of each) make up each of count output pixels along an axis: the output covers
	// [start, start + span) of the source evenly
	struct Tap { int index; float weight; };
	std::vector<std::vector<Tap>> Taps(double start, double span, uint32_t count, int sourceSize)
	{
		std::vector<std::vector<Tap>> taps(count);
		const double step = span / count;
		for (uint32_t o = 0; o < count; o++)
		{
			const double a = start + o * step, b = a + step;
			for (int s = static_cast<int>(std::floor(a)); s < static_cast<int>(std::ceil(b)); s++)
			{
				const double overlap = (std::min)(b, s + 1.0) - (std::max)(a, static_cast<double>(s));
				if (overlap > 0.0)
					taps[o].push_back({ (std::max)(0, (std::min)(s, sourceSize - 1)), static_cast<float>(overlap / step) });
			}
		}
		return taps;
	}

	// Takes the presented frame's middle square, scaled to size x size RGBA, onto the end of out
	bool TakeFace(IDXGISwapChain* swapChain, uint32_t size, std::vector<uint8_t>& out, uint32_t& width, uint32_t& height, std::string& error)
	{
		Com<ID3D11Texture2D> backBuffer;
		if (FAILED(swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&backBuffer))))
		{
			error = "Could not get the frame";
			return false;
		}
		Com<ID3D11Device> device;
		backBuffer->GetDevice(&device);
		Com<ID3D11DeviceContext> context;
		device->GetImmediateContext(&context);
		D3D11_TEXTURE2D_DESC desc;
		backBuffer->GetDesc(&desc);
		width = desc.Width;
		height = desc.Height;
		if (desc.Width < desc.Height)
		{
			error = "The game's window is taller than it is wide";
			return false;
		}

		// A multisampled frame is resolved first
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
				error = "Could not resolve the frame";
				return false;
			}
			context->ResolveSubresource(resolved.ptr, 0, backBuffer.ptr, 0, desc.Format);
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
			error = "Could not read the frame";
			return false;
		}
		context->CopyResource(staging.ptr, source);
		D3D11_MAPPED_SUBRESOURCE mapped;
		if (FAILED(context->Map(staging.ptr, 0, D3D11_MAP_READ, 0, &mapped)))
		{
			error = "Could not read the frame";
			return false;
		}

		// The pixel layouts a frame can have (as screenshots take them)
		int layout = -1; // 0 RGBA8, 1 BGRA8, 2 RGB10A2
		switch (desc.Format)
		{
		case DXGI_FORMAT_R8G8B8A8_UNORM:
		case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
		case DXGI_FORMAT_R8G8B8A8_TYPELESS:
			layout = 0;
			break;
		case DXGI_FORMAT_B8G8R8A8_UNORM:
		case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:
		case DXGI_FORMAT_B8G8R8A8_TYPELESS:
		case DXGI_FORMAT_B8G8R8X8_UNORM:
			layout = 1;
			break;
		case DXGI_FORMAT_R10G10B10A2_UNORM:
			layout = 2;
			break;
		default:
			break;
		}
		if (layout < 0)
		{
			context->Unmap(staging.ptr, 0);
			error = "Unsupported frame format " + std::to_string(static_cast<int>(desc.Format));
			return false;
		}

		// The middle square, as tall as the frame
		BuildToLinear();
		const double side = desc.Height;
		const std::vector<std::vector<Tap>> xTaps = Taps((desc.Width - side) / 2.0, side, size, static_cast<int>(desc.Width));
		const std::vector<std::vector<Tap>> yTaps = Taps(0.0, side, size, static_cast<int>(desc.Height));
		const size_t at = out.size();
		out.resize(at + static_cast<size_t>(size) * size * 4);
		uint8_t* face = out.data() + at;
		for (uint32_t oy = 0; oy < size; oy++)
		{
			for (uint32_t ox = 0; ox < size; ox++)
			{
				float sum[3] = {};
				for (const Tap& ty : yTaps[oy])
				{
					const uint8_t* row = static_cast<const uint8_t*>(mapped.pData) + static_cast<size_t>(ty.index) * mapped.RowPitch;
					for (const Tap& tx : xTaps[ox])
					{
						const float w = ty.weight * tx.weight;
						const uint8_t* pixel = row + static_cast<size_t>(tx.index) * 4;
						float r, g, b;
						if (layout == 2)
						{
							uint32_t value;
							memcpy(&value, pixel, 4);
							r = g_toLinear[(value & 0x3FF) >> 2];
							g = g_toLinear[((value >> 10) & 0x3FF) >> 2];
							b = g_toLinear[((value >> 20) & 0x3FF) >> 2];
						}
						else
						{
							r = g_toLinear[pixel[layout == 0 ? 0 : 2]];
							g = g_toLinear[pixel[1]];
							b = g_toLinear[pixel[layout == 0 ? 2 : 0]];
						}
						sum[0] += r * w;
						sum[1] += g * w;
						sum[2] += b * w;
					}
				}
				uint8_t* o = face + (static_cast<size_t>(oy) * size + ox) * 4;
				o[0] = ToEncoded(sum[0]);
				o[1] = ToEncoded(sum[1]);
				o[2] = ToEncoded(sum[2]);
				o[3] = 255;
			}
		}
		context->Unmap(staging.ptr, 0);
		return true;
	}

	int ReadByte(const uint8_t* where)
	{
		__try
		{
			return *where;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	// Called under g_mutex: undoes what the job changed in the game (render thread, or the camera thread when frames stopped)
	void RestoreGame(Job& job)
	{
		// The interface pieces hidden are given their flags back - only those still in the interface manager's list (a
		// piece can be let go of meanwhile, a level ending say, and its memory reused) and still hidden as the capture left
		// them (the game may have hidden or shown it itself since)
		if (!job.hiddenUi.empty())
		{
			uint8_t* flags[kMaxUiPieces];
			uint8_t values[kMaxUiPieces];
			const int count = UiHideFlags(flags, values, kMaxUiPieces);
			for (auto& hidden : job.hiddenUi)
			{
				bool present = false;
				for (int i = 0; i < count && !present; i++)
					present = flags[i] == hidden.first;
				if (present && ReadByte(hidden.first) == 1)
					WriteByte(hidden.first, hidden.second);
			}
		}
		job.hiddenUi.clear();
		g_postEffectsOff = false;
		ZONE_LOADER::SetForced(ZONE_LOADER::Source::Capture, false);
		MOUSE_UNLOCK::Hold(false);
	}

	// Called under g_mutex: ends the job, answering it (render thread)
	void Finish(bool ok, const std::string& message, std::vector<uint8_t>&& payload)
	{
		RestoreGame(g_job);
		CUBEMAP_CAPTURE::Done done = std::move(g_job.done);
		g_job = Job();
		g_active = false;
		g_cameraFace = -1;
		SetOverlay("");
		DevTools::Log("LiveLink: cubemap capture %s: %s", ok ? "done" : "ended", message.c_str());
		LIVE_LINK_SERVER::ShowActivity(ok ? "Cubemap captured" : "Cubemap capture ended: " + message);
		// Answering copies the faces once more: if the game is that short of memory, it is answered without them
		if (done)
		{
			try
			{
				done(ok, message, std::move(payload));
			}
			catch (const std::bad_alloc&)
			{
				try { done(false, "The game ran short of memory sending the faces", {}); }
				catch (const std::bad_alloc&) {}
			}
		}
	}

	std::string Number(float value)
	{
		static const _locale_t invariant = _create_locale(LC_NUMERIC, "C");
		char text[32];
		_snprintf_s_l(text, sizeof(text), _TRUNCATE, "%.3f", invariant, value);
		return text;
	}
}

LIVE_LINK::Result CUBEMAP_CAPTURE::Start(const Request& request, Done done)
{
	if (!Config::Get().liveLinkCamera)
		return LIVE_LINK::Result{ false, "Camera sync is off in the game (LiveLinkCamera=0 in OpenCAGE.RuntimeUtils.ini), which captures need" };
	if (!LevelIs(request.root))
		return LIVE_LINK::Result{ false, AnyLevel() ? "The game is running a different level - save, and load this one in the game" : "No level is running" };
	if (ScriptTransport() == static_cast<int>(kTransportPauseMenu))
		return LIVE_LINK::Result{ false, "The game is paused - close the pause menu (nothing streams in while it is open)" };
	for (float value : request.position)
		if (!std::isfinite(value))
			return LIVE_LINK::Result{ false, "The capture position is not a number" };

	// The answer is built in one buffer as the faces are taken: the header, then each face (allocated up front, so a
	// 32-bit game short of memory turns the capture away now rather than failing part way)
	std::vector<uint8_t> buffer;
	try
	{
		buffer.reserve(kHeaderBytes + static_cast<size_t>(request.size) * request.size * 4 * 6);
		buffer.resize(kHeaderBytes);
	}
	catch (const std::bad_alloc&)
	{
		return LIVE_LINK::Result{ false, "The game hasn't the memory for faces this size - capture a smaller cubemap" };
	}

	std::lock_guard<std::mutex> lock(g_mutex);
	if (g_active)
		return LIVE_LINK::Result{ false, "A cubemap is already being captured" };
	g_job = Job();
	g_job.faces = std::move(buffer);
	g_job.request = request;
	g_job.done = std::move(done);
	g_job.phase = Phase::Streaming;
	g_job.startedAt = GetTickCount64();
	g_job.loadedSince = g_job.startedAt;
	for (int i = 0; i < 6; i++)
		FaceRotation(kFaces[i], g_job.rotations[i]);

	g_cameraFace = -1;
	g_cameraAt = 0;
	g_presentAt = 0;
	ZONE_LOADER::SetForced(ZONE_LOADER::Source::Capture, true);
	MOUSE_UNLOCK::Hold(true);
	g_active = true;
	const std::string where = Number(request.position[0]) + ", " + Number(request.position[1]) + ", " + Number(request.position[2]);
	DevTools::Log("LiveLink: cubemap capture at %s, %u px faces", where.c_str(), request.size);
	SetOverlay("Capturing a cubemap: streaming in the zones around it");
	return LIVE_LINK::Result{ true, "Capturing at " + where };
}

bool CUBEMAP_CAPTURE::CameraPose(float rotation[4], float position[3], float& fov)
{
	if (!g_active)
		return false;
	std::lock_guard<std::mutex> lock(g_mutex);
	if (!g_active)
		return false;
	// Frames stopped being presented (the window minimised, say): the capture cannot finish, and must not keep the camera
	const ULONGLONG now = GetTickCount64();
	const ULONGLONG presentAt = g_presentAt;
	if (now - (presentAt ? presentAt : g_job.startedAt) > kStalledMs)
	{
		Finish(false, "The game stopped presenting frames", {});
		return false;
	}
	const int face = g_job.phase == Phase::Settling ? g_job.face : 0;
	memcpy(rotation, g_job.rotations[face], sizeof(float) * 4);
	memcpy(position, g_job.request.position, sizeof(float) * 3);
	fov = kFaceFov;
	g_cameraFace = face;
	g_cameraAt = now;
	return true;
}

void CUBEMAP_CAPTURE::OnPresent(IDXGISwapChain* swapChain)
{
	if (!g_active)
		return;
	std::lock_guard<std::mutex> lock(g_mutex);
	if (!g_active)
		return;
	Job& job = g_job;
	const ULONGLONG now = GetTickCount64();
	g_presentAt = now;

	// Set up once the job is in the render thread's hands: the UI is hidden from here on, and the post-processing turned off
	if (!job.started)
	{
		job.started = true;
		if (!(job.request.flags & kKeepHud))
		{
			uint8_t* flags[kMaxUiPieces];
			uint8_t values[kMaxUiPieces];
			const int count = UiHideFlags(flags, values, kMaxUiPieces);
			for (int i = 0; i < count; i++)
			{
				job.hiddenUi.emplace_back(flags[i], values[i]);
				WriteByte(flags[i], 1);
			}
		}
		g_postEffectsOff = !(job.request.flags & kKeepPostEffects);
	}

	// Over early: OpenCAGE gone, another level, the pause menu, or nothing drawn from the capture's camera for a while
	if (!LIVE_LINK_SERVER::Connected() || LIVE_LINK_SERVER::CurrentConnection() != job.request.connection)
	{
		Finish(false, "OpenCAGE disconnected", {});
		return;
	}
	if (!LevelIs(job.request.root))
	{
		Finish(false, "The level changed", {});
		return;
	}
	if (ScriptTransport() == static_cast<int>(kTransportPauseMenu))
	{
		Finish(false, "The game was paused", {});
		return;
	}
	const ULONGLONG cameraAt = g_cameraAt;
	if (now - (cameraAt ? cameraAt : job.startedAt) > kStalledMs)
	{
		Finish(false, "The game stopped drawing from the capture's camera", {});
		return;
	}
	if (now - job.startedAt > job.request.streamTimeoutMs + kFacesMs + static_cast<ULONGLONG>(job.request.settleFrames) * 6 * kSettleFrameMs)
	{
		Finish(false, "The capture took too long", {});
		return;
	}

	if (job.phase == Phase::Streaming)
	{
		// What is loaded now, remembered for a little while (see kZoneSeenMs)
		uint32_t ids[kMaxZones];
		const int found = LoadedZones(ids, kMaxZones);
		for (int i = 0; i < found; i++)
			job.zoneSeen[ids[i]] = now;
		size_t loaded = 0;
		for (const auto& seen : job.zoneSeen)
			if (now - seen.second <= kZoneSeenMs)
				loaded++;
		if (loaded != job.loadedCount)
		{
			job.loadedCount = loaded;
			job.loadedSince = now;
		}
		const ULONGLONG waited = now - job.startedAt;
		const bool settled = g_cameraFace == 0 && waited >= kStreamMinMs && now - job.loadedSince >= kStreamStableMs;
		const bool timedOut = waited >= job.request.streamTimeoutMs;
		if (!settled && !timedOut)
		{
			char text[128];
			sprintf_s(text, "Capturing a cubemap: streaming in the zones around it (%u loaded, %.1f s)", static_cast<unsigned>(loaded), waited / 1000.0);
			SetOverlay(text);
			return;
		}
		job.streamTimedOut = !settled;
		job.streamedAt = now;
		job.phase = Phase::Settling;
		job.face = 0;
		job.presentsOnFace = 0;
		DevTools::Log("LiveLink: cubemap capture: %s after %.1f s (%u zones loaded)", settled ? "zones settled" : "zones still streaming", waited / 1000.0, static_cast<unsigned>(loaded));
	}

	// A face is held until the camera has rendered from it for settleFrames frames, then its picture is taken
	if (g_cameraFace != job.face)
		return;
	if (++job.presentsOnFace < job.request.settleFrames)
	{
		char text[96];
		sprintf_s(text, "Capturing a cubemap: face %s (%d of 6)", kFaces[job.face].name, job.face + 1);
		SetOverlay(text);
		return;
	}
	std::string error;
	uint32_t width = 0, height = 0;
	bool taken = false;
	try
	{
		taken = TakeFace(swapChain, job.request.size, job.faces, width, height, error);
	}
	catch (const std::bad_alloc&)
	{
		error = "The game ran short of memory taking a face";
	}
	if (!taken)
	{
		Finish(false, error, {});
		return;
	}
	if (job.face > 0 && (width != job.frameWidth || height != job.frameHeight))
	{
		Finish(false, "The game's resolution changed during the capture", {});
		return;
	}
	job.frameWidth = width;
	job.frameHeight = height;
	if (++job.face < 6)
	{
		job.presentsOnFace = 0;
		return;
	}

	// All six: the answer's payload (see CAPTURE_CUBEMAP in LIVE_LINK_SERVER.h)
	const uint32_t flags = (job.streamTimedOut ? 1u : 0u) | (job.hiddenUi.empty() ? 0u : 2u) | (g_postEffectsOff ? 4u : 0u);
	const uint32_t streamMs = static_cast<uint32_t>(job.streamedAt - job.startedAt);
	const uint32_t header[] = { 1, job.request.size, 6, flags, width, height, streamMs };
	static_assert(sizeof(header) == kHeaderBytes, "the answer's header");
	memcpy(job.faces.data(), header, sizeof(header));
	char message[256];
	sprintf_s(message, "Captured a %u px cubemap from a %ux%u frame%s", job.request.size, width, height,
		job.streamTimedOut ? " (zones were still streaming in when the wait ran out)" : "");
	std::vector<uint8_t> payload = std::move(job.faces);
	Finish(true, message, std::move(payload));
}

std::string CUBEMAP_CAPTURE::OverlayText()
{
	if (!g_active)
		return {};
	std::lock_guard<std::mutex> lock(g_textMutex);
	return g_overlay;
}

bool CUBEMAP_CAPTURE::OnWindowMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam, LRESULT& result)
{
	// The game reads its keyboard and mouse as raw input, and only while its window has the focus. Escape is let through:
	// the pause menu it opens ends the capture.
	if (!g_active || message != WM_INPUT || GetFocus() != window)
		return false;
	RAWINPUT input = {};
	UINT size = sizeof(input);
	if (GetRawInputData(reinterpret_cast<HRAWINPUT>(lParam), RID_INPUT, &input, &size, sizeof(RAWINPUTHEADER)) == static_cast<UINT>(-1))
		return false;
	if (input.header.dwType == RIM_TYPEKEYBOARD && input.data.keyboard.VKey == VK_ESCAPE)
		return false;
	if (input.header.dwType != RIM_TYPEKEYBOARD && input.header.dwType != RIM_TYPEMOUSE)
		return false;
	result = DefWindowProc(window, message, wParam, lParam);
	return true;
}

namespace
{
	void TurnOffPostEffects(uint8_t* settings)
	{
		__try
		{
			for (uint32_t offset : kFilmGrainScales)
				*reinterpret_cast<float*>(settings + offset) = 0.0f;
			*reinterpret_cast<float*>(settings + kVignetteStrength) = 0.0f;
			*reinterpret_cast<float*>(settings + kChromaticAberration) = 0.0f;
			*reinterpret_cast<float*>(settings + kLensDistortion) = 0.0f;
			*reinterpret_cast<float*>(settings + kDepthOfFieldStrength) = 0.0f;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}
}

__declspec(noinline)
int __fastcall CUBEMAP_CAPTURE::h_post_process_update(void* _this, void* /*_EDX*/)
{
	const int result = post_process_update(_this);
	if (g_postEffectsOff && _this)
		TurnOffPostEffects(static_cast<uint8_t*>(_this) + kPostProcessSettings);
	return result;
}
