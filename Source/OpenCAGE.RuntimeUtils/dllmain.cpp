#include "DevTools.h"

#include "Config.h"
#include "Menu.h"

// Game-specific code classes.
#include "GAME_LEVEL_MANAGER.h"
#include "GameFlow.h"
#include "DEBUG_TEXT.h"
#include "DEBUG_MARKER.h"
#include "ZONE_LOADER.h"
#include "LIVE_LINK.h"
#include "LIVE_LINK_SERVER.h"
#include "LIVE_CAMERA.h"
#include "LIVE_TRACE.h"
#include "MOUSE_UNLOCK.h"
#include "FRAME_CAP.h"
#include "POSSESS.h"

// External includes.
#include <detours.h>
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>


typedef HRESULT(WINAPI* tD3D11CreateDeviceAndSwapChain)(
    void* pAdapter,
    D3D_DRIVER_TYPE      DriverType,
    HMODULE              Software,
    UINT                 Flags,
    const void* pFeatureLevels,
    UINT                 FeatureLevels,
    UINT                 SDKVersion,
    const DXGI_SWAP_CHAIN_DESC* pSwapChainDesc,
    IDXGISwapChain** ppSwapChain,
    ID3D11Device** ppDevice,
    void* pFeatureLevel,
    ID3D11DeviceContext** ppImmediateContext
);

// The original D3D11 device and swap chain creation, hooked so the overlay can find the game's swap chain.
tD3D11CreateDeviceAndSwapChain d3d11CreateDeviceAndSwapChain = nullptr;

typedef HRESULT(WINAPI* tD3D11Present)(
    IDXGISwapChain* swapChain,
    UINT            SyncInterval,
    UINT            Flags
);

// The original D3D11 frame presentation, hooked so the overlay draws on every frame.
tD3D11Present d3d11Present = nullptr;

typedef HRESULT(WINAPI* tDXGIResizeBuffers)(
    IDXGISwapChain* swapChain,
    UINT            BufferCount,
    UINT            Width,
    UINT            Height,
    DXGI_FORMAT     NewFormat,
    UINT            SwapChainFlags
);

// The original resize of the swap chain's back buffers (a change of resolution), hooked so the overlay lets go of them first.
tDXGIResizeBuffers dxgiResizeBuffers = nullptr;

// This will work for now, but I need to write a replacement that will restore the original call bytes, we just overwrite them.
void hookFunctionCall(int offset, void* replacementFunction)
{
	const SIZE_T patchSize = 5;
	DWORD oldProtect;
	char* patchLocation = reinterpret_cast<char*>(offset);

	// Change the memory page protection.
	VirtualProtect(patchLocation, patchSize, PAGE_EXECUTE_READWRITE, &oldProtect);
	//to fill out the last 4 bytes of instruction, we need the offset between
	//the payload function and the instruction immediately AFTER the call instruction

	//32 bit relative call opcode is E8, takes 1 32 bit operand for call offset
    uint8_t instruction[patchSize] = {0xE8, 0x0, 0x0, 0x0, 0x0};
	const uint32_t relativeAddress = reinterpret_cast<uint32_t>(replacementFunction) - (reinterpret_cast<uint32_t>(patchLocation) + sizeof(instruction));

	// Copy the remaining bytes of the instruction (after the opcode).
	memcpy_s(instruction + 1, patchSize - 1, &relativeAddress, patchSize - 1);

	// Install the hook.
	memcpy_s(patchLocation, patchSize, instruction, sizeof(instruction));

    // Restore original memory page protections.
	VirtualProtect(patchLocation, patchSize, oldProtect, &oldProtect);
}

HRESULT WINAPI hD3D11Present(
    IDXGISwapChain* swapChain,
    UINT        SyncInterval,
    UINT        Flags
) {
    if (Menu::UsePresentingSwapChain(swapChain))
        Menu::DrawMenu();

    // Live link screenshots and level loads, after the overlay so a screenshot shows it.
    if (Config::Get().liveLink)
        LIVE_LINK_SERVER::ProcessRenderRequests(swapChain);

    return d3d11Present(swapChain, SyncInterval, Flags);
}

HRESULT WINAPI hDXGIResizeBuffers(
    IDXGISwapChain* swapChain,
    UINT            BufferCount,
    UINT            Width,
    UINT            Height,
    DXGI_FORMAT     NewFormat,
    UINT            SwapChainFlags
) {
    Menu::ReleaseRenderTarget(swapChain);
    return dxgiResizeBuffers(swapChain, BufferCount, Width, Height, NewFormat, SwapChainFlags);
}

HRESULT WINAPI hD3D11CreateDeviceAndSwapChain(
    void* pAdapter,
    D3D_DRIVER_TYPE      DriverType,
    HMODULE              Software,
    UINT                 Flags,
    const void* pFeatureLevels,
    UINT                 FeatureLevels,
    UINT                 SDKVersion,
    const DXGI_SWAP_CHAIN_DESC* pSwapChainDesc,
    IDXGISwapChain** ppSwapChain,
    ID3D11Device** ppDevice,
    void* pFeatureLevel,
    ID3D11DeviceContext** ppImmediateContext
) {
    HRESULT res = d3d11CreateDeviceAndSwapChain(
        pAdapter,
        DriverType,
        Software,
        Flags,
        pFeatureLevels,
        FeatureLevels,
        SDKVersion,
        pSwapChainDesc,
        ppSwapChain,
        ppDevice,
        pFeatureLevel,
        ppImmediateContext
    );

    // If the overlay hasn't already been initialised, initialise it now.
    if (!Menu::IsInitialised())
    {
        Menu::InitMenu(*ppSwapChain);

        if (*ppSwapChain)
        {
            DetourTransactionBegin();
            DetourUpdateThread(GetCurrentThread());

            void** pVMTPresent = *reinterpret_cast<void***>(*ppSwapChain);

        	// Keep the original frame presentation, slot 8 of the swap chain's virtual table, and the resize of its back
            // buffers, slot 13.
            d3d11Present = static_cast<tD3D11Present>(pVMTPresent[8]);
            dxgiResizeBuffers = static_cast<tDXGIResizeBuffers>(pVMTPresent[13]);

            DEVTOOLS_DETOURS_ATTACH(d3d11Present, hD3D11Present);
            DEVTOOLS_DETOURS_ATTACH(dxgiResizeBuffers, hDXGIResizeBuffers);

            const auto result = DetourTransactionCommit();
		}
    }

    return res;
}

// Attaches or detaches every hook, according to which features are enabled in the config.
// Detours rejects detaching something that was never attached, so both directions must agree.
static void AttachHooks(bool attach)
{
    const Config::Settings& config = Config::Get();

    auto hook = [attach](auto& original, auto detoured)
    {
        if (attach) DEVTOOLS_DETOURS_ATTACH(original, detoured);
        else DEVTOOLS_DETOURS_DETACH(original, detoured);
    };

    // Rendering hooks: needed for the hot reload, unlock mouse and possess keys (window procedure) and the overlay.
    if ((config.hotReload || config.unlockMouse || Config::AnyDebug() || config.liveLink || config.possess) && d3d11CreateDeviceAndSwapChain)
    {
        hook(d3d11CreateDeviceAndSwapChain, hD3D11CreateDeviceAndSwapChain);
        if (!attach && d3d11Present)
            hook(d3d11Present, hD3D11Present);
        if (!attach && dxgiResizeBuffers)
            hook(dxgiResizeBuffers, hDXGIResizeBuffers);
    }

    // Level manager hooks: capture the level manager so the hot reload key can restart the current level, and the
    // live link can load a level (they only pass through otherwise).
    if (config.hotReload || config.liveLink)
    {
        hook(GAME_LEVEL_MANAGER::get_level_from_name, GAME_LEVEL_MANAGER::h_get_level_from_name);
        hook(GAME_LEVEL_MANAGER::queue_level, GAME_LEVEL_MANAGER::h_queue_level);
        hook(GAME_LEVEL_MANAGER::request_next_level, GAME_LEVEL_MANAGER::h_request_next_level);
    }

    // Live link: requests on entities are carried out once a frame, on the entity thread.
    if (config.liveLink)
        hook(LIVE_LINK::process, LIVE_LINK::h_process);

    // Live link camera sync: renders the game from OpenCAGE's viewport camera while OpenCAGE asks. Attached for the whole
    // run and gated by the pose instead: Cinematic Tools hooks the same function later (MinHook), and the two chain.
    // Possess puts the camera behind the possessed character through it too.
    if ((config.liveLink && config.liveLinkCamera) || config.possess)
        hook(LIVE_CAMERA::synchronize_with_engine, LIVE_CAMERA::h_synchronize_with_engine);

    // Possess: each character's behaviour update, to give the possessed one its orders from the keys in place of its
    // behaviour tree. They only pass through while nobody is possessed.
    if (config.possess)
    {
        hook(POSSESS::behaviour_update, POSSESS::h_behaviour_update);
        hook(POSSESS::tree_update, POSSESS::h_tree_update);
    }

    // Live link script activity: the game's firing of outputs and reading of parameters, noted for the composites OpenCAGE
    // watches while it asks. They only pass through (after checking a flag) otherwise.
    if (config.liveLink)
        LIVE_TRACE::AttachHooks(attach);

    // Unlock Mouse: the game's grab of the cursor, and its moving of it, while the cursor is free.
    if (config.unlockMouse)
    {
        hook(MOUSE_UNLOCK::grab_cursor, MOUSE_UNLOCK::h_grab_cursor);
        hook(MOUSE_UNLOCK::set_cursor_pos, MOUSE_UNLOCK::h_set_cursor_pos);
    }

    // Cap FPS: each frame waits its turn before the game reads its clock.
    if (config.capFps > 0)
        hook(FRAME_CAP::frame_start, FRAME_CAP::h_frame_start);

    // A hook on the start of gameplay (it only passes through).
    hook(GameFlow::start_gameplay, GameFlow::h_start_gameplay);

    // Zone streaming: always hooked, so Cinematic Tools (and the live link camera) can switch forced loading on and off
    // while running.
    hook(ZONE_LOADER::match_current_zones_to_povs, ZONE_LOADER::h_match_current_zones_to_povs);

    // DebugText / DebugTextStacking hooks. The level closing clears everything the debug entities drew, markers included.
    if (Config::AnyDebug())
        hook(DEBUG_TEXT::level_close, DEBUG_TEXT::h_level_close);

    if (config.debugText)
    {
        hook(DEBUG_TEXT::destructor, DEBUG_TEXT::h_destructor);
        hook(DEBUG_TEXT::on_start, DEBUG_TEXT::h_on_start);
        hook(DEBUG_TEXT::on_update, DEBUG_TEXT::h_on_update);
        hook(DEBUG_TEXT::on_clear_of_alignment, DEBUG_TEXT::h_on_clear_of_alignment);
        hook(DEBUG_TEXT::on_stop, DEBUG_TEXT::h_on_stop);
        hook(DEBUG_TEXT::on_clear_all, DEBUG_TEXT::h_on_clear_all);
    }

    if (config.debugTextStacking)
    {
        hook(DEBUG_TEXT::stacking_destructor, DEBUG_TEXT::h_stacking_destructor);
        hook(DEBUG_TEXT::stacking_on_start, DEBUG_TEXT::h_stacking_on_start);
        hook(DEBUG_TEXT::stacking_on_clear_all, DEBUG_TEXT::h_stacking_on_clear_all);
        hook(DEBUG_TEXT::stacking_on_clear_last, DEBUG_TEXT::h_stacking_on_clear_last);
    }

    if (config.debugEnvironmentMarker)
        hook(DEBUG_MARKER::environment_on_update, DEBUG_MARKER::h_environment_on_update);

    if (config.debugPositionMarker)
        hook(DEBUG_MARKER::position_on_update, DEBUG_MARKER::h_position_on_update);
}

BOOL APIENTRY DllMain( HMODULE /*hModule*/,
                       DWORD  ul_reason_for_call,
                       LPVOID /*lpReserved*/
                     )
{
    if (DetourIsHelperProcess())
    {
        return TRUE;
    }

    if (ul_reason_for_call == DLL_PROCESS_ATTACH)
    {
        // Which store's AI.exe this is: every address the hooks use is the Steam build's, looked up for the GOG and Epic
        // Games Store builds. Another build (an update, another store) would get the wrong ones, so it gets nothing.
        DevTools::Log("AI.exe build: %s", DevTools::BuildName());
        if (DevTools::Build() == DevTools::GameBuild::Unknown)
        {
            MessageBox(NULL, L"Warning - this AI.exe is not a build the OpenCAGE runtime utils know (they support the Steam, GOG and Epic Games Store releases of Alien: Isolation), so they are switched off for this run.", L"AlienIsolation.DevTools", MB_ICONWARNING);
            return TRUE;
        }

        const Config::Settings& config = Config::Get();
        ZONE_LOADER::SetForced(ZONE_LOADER::Source::Config, config.loadAllZones);
        FRAME_CAP::SetLimit(config.capFps);

        // Re-enable the DebugText / DebugTextStacking script entities, which retail builds disable.
        if (Config::AnyDebug())
        {
            if (!DEBUG_TEXT::EnableEntities(config.debugText, config.debugTextStacking) || !DEBUG_MARKER::EnableEntities(config.debugEnvironmentMarker))
            {
                MessageBox(NULL, L"Warning - could not enable the DebugText entities: unexpected AI.exe build?", L"AlienIsolation.DevTools", MB_ICONWARNING);
            }
        }

        DetourRestoreAfterWith();
        DetourTransactionBegin();
        DetourUpdateThread(GetCurrentThread());

    	// Overlay hooks / initialisation code, adapted from Alias Isolation.
        if (config.hotReload || config.unlockMouse || Config::AnyDebug() || config.liveLink || config.possess)
        {
            const HMODULE hModule = GetModuleHandle(L"d3d11");

            if (hModule)
            {
                d3d11CreateDeviceAndSwapChain = reinterpret_cast<tD3D11CreateDeviceAndSwapChain>(GetProcAddress(hModule, "D3D11CreateDeviceAndSwapChain"));

                if (!d3d11CreateDeviceAndSwapChain)
                {
                    MessageBox(NULL, L"Fatal Error - GetProcAddress(\"D3D11CreateDeviceAndSwapChain\") failed!", L"AlienIsolation.DevTools", MB_ICONERROR);
                }
            }
            else
            {
                MessageBox(NULL, L"Fatal Error - GetModuleHandle(\"d3d11\") failed: MODULE_NOT_FOUND!", L"AlienIsolation.DevTools", MB_ICONERROR);
            }
        }

        AttachHooks(true);

        if (config.liveLink)
            LIVE_LINK_SERVER::Start(static_cast<uint16_t>(config.liveLinkPort));

        const long result = DetourTransactionCommit();
        if (result != NO_ERROR)
        {
            switch (result)
            {
            case ERROR_INVALID_BLOCK:
                MessageBox(NULL, L"Fatal Error - The function referenced is too small to be detoured", L"AlienIsolation.DevTools", MB_ICONERROR);
                break;
            case ERROR_INVALID_HANDLE:
                MessageBox(NULL, L"Fatal Error - The ppPointer parameter is null or points to a null pointer", L"AlienIsolation.DevTools", MB_ICONERROR);
                break;
            case ERROR_INVALID_OPERATION:
                MessageBox(NULL, L"Fatal Error - No pending transaction exists", L"AlienIsolation.DevTools", MB_ICONERROR);
                break;
            case ERROR_NOT_ENOUGH_MEMORY:
                MessageBox(NULL, L"Fatal Error - Not enough memory exists to complete the operation", L"AlienIsolation.DevTools", MB_ICONERROR);
                break;
            case ERROR_INVALID_PARAMETER:
                MessageBox(NULL, L"Fatal Error - An invalid parameter has been passed", L"AlienIsolation.DevTools", MB_ICONERROR);
                break;
            default:
                MessageBox(NULL, L"Fatal Error - Unknown Detours error", L"AlienIsolation.DevTools", MB_ICONERROR);
                break;
            }
        }
    }
    else if (ul_reason_for_call == DLL_PROCESS_DETACH)
    {
        // Nothing was hooked in a build these don't know
        if (DevTools::Build() == DevTools::GameBuild::Unknown)
            return TRUE;

        if (Config::Get().liveLink)
            LIVE_LINK_SERVER::Stop();

        DetourTransactionBegin();
        DetourUpdateThread(GetCurrentThread());

        AttachHooks(false);

        DetourTransactionCommit();
    }

    return TRUE;
}
