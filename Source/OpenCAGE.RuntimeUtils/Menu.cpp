#include "Menu.h"

// Game-specific code classes.
#include "GAME_LEVEL_MANAGER.h"
#include "DEBUG_TEXT.h"
#include "DEBUG_MARKER.h"
#include "Config.h"
#include "LIVE_LINK_SERVER.h"
#include "LIVE_CAMERA.h"
#include "CUBEMAP_CAPTURE.h"
#include "CINEMATIC_CAMERA.h"
#include "CINEMATIC_CAPTURE.h"
#include "DEBUG_MENU.h"
#include "SCRIPT_DEBUG.h"
#include "MOUSE_UNLOCK.h"
#include "POSSESS.h"

// Engine-specific code classes.

#include <detours.h>
#include <algorithm>
#include <map>
#include <mutex>
#include <sstream>
#include <iomanip>
#include <cfloat>
#include <vector>

extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);

IDXGISwapChain* g_swapChain = nullptr;
ID3D11Device* g_device = nullptr;
ID3D11DeviceContext* g_context = nullptr;
ID3D11RenderTargetView* g_renderTargetView = nullptr;
HWND                    g_hWindow = nullptr;
WNDPROC                 g_originalWndProcHandler = nullptr;

bool g_menuInitialised = false;
bool g_showDemoWindow = false;
bool g_showMenu = false;

namespace
{
    // The window messages queued for ImGui since the last frame (QueueImGuiMessage), at most kMaxImGuiMessages of them
    struct ImGuiMessage
    {
        HWND window;
        UINT message;
        WPARAM wParam;
        LPARAM lParam;
    };
    std::mutex g_imguiMessagesMutex;
    std::vector<ImGuiMessage> g_imguiMessages;
    constexpr size_t kMaxImGuiMessages = 256;

    bool IsMouseMove(UINT message)
    {
        return message == WM_MOUSEMOVE || message == WM_NCMOUSEMOVE;
    }

    // The thread that presents, before its frame: ImGui is given the queued messages, in the order they came
    void FeedQueuedImGuiInput()
    {
        std::vector<ImGuiMessage> messages;
        {
            std::lock_guard<std::mutex> lock(g_imguiMessagesMutex);
            messages.swap(g_imguiMessages);
        }
        for (const ImGuiMessage& queued : messages)
            ImGui_ImplWin32_WndProcHandler(queued.window, queued.message, queued.wParam, queued.lParam);
    }
}

Menu::Menu()
{
	
}

// Forgets the overlay's swap chain, context, render target and window.
Menu::~Menu() {
    g_swapChain = nullptr;
    g_context = nullptr;
    g_renderTargetView = nullptr;
    g_hWindow = nullptr;
    g_originalWndProcHandler = nullptr;
}

// Our custom window handler to override A:I's own handler.
// This lets us detect keyboard inputs and pass mouse + keyboard input control to ImGui.
LRESULT CALLBACK Menu::WndProcHandler(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    const Config::Settings& config = Config::Get();
    if (uMsg == WM_KEYUP) {
        if (wParam == VK_DELETE) {
            // the Delete key once toggled the settings window; it is off for now
        }
        // Hot reload: restart the current level. Not while the debug menu is open: its text fields use the keys hot reload
        // can be set to (Insert, Delete, Home, End), and its Game tab can restart the level itself.
        if (config.hotReload && wParam == static_cast<WPARAM>(config.hotReloadKey) && GAME_LEVEL_MANAGER::m_instance != nullptr && !(config.debugMenu && DEBUG_MENU::IsOpen())) {
            LoadLevel(GAME_LEVEL_MANAGER::get_current_level(GAME_LEVEL_MANAGER::m_instance));
        }
    }

    // Unlock Mouse: its key, the cursor shown over the window while it's free, and the mouse kept from the game then.
    // A cubemap capture frees the cursor through it too, as does the debug menu while open, so it is in for those.
    const bool captures = config.liveLink && config.liveLinkCamera;
    if (config.unlockMouse || captures || config.debugMenu) {
        LRESULT result = 0;
        if (MOUSE_UNLOCK::OnWindowMessage(hWnd, uMsg, wParam, lParam, result))
            return result;
    }

    // Cubemap capture: the keyboard and mouse kept from the game while one runs (bar Escape, which stops it). When none
    // runs it hands them on to the debug menu's cinematic camera.
    if (captures) {
        LRESULT result = 0;
        if (CUBEMAP_CAPTURE::OnWindowMessage(hWnd, uMsg, wParam, lParam, result))
            return result;
    }
    // The debug menu's cinematic camera: the mouse turns it, and the game's keyboard and mouse are kept from the game while
    // it flies.
    else if (config.debugMenu) {
        LRESULT result = 0;
        if (CINEMATIC_CAMERA::OnWindowMessage(hWnd, uMsg, wParam, lParam, result))
            return result;
    }

    // The debug menu: its key, and the keyboard and mouse its window's while it is open.
    if (config.debugMenu) {
        LRESULT result = 0;
        if (DEBUG_MENU::OnWindowMessage(hWnd, uMsg, wParam, lParam, result))
            return result;
    }

    // Possess: its key, and the keyboard and mouse kept from the game while a character is possessed.
    if (config.possess) {
        LRESULT result = 0;
        if (POSSESS::OnWindowMessage(hWnd, uMsg, wParam, lParam, result))
            return result;
    }

    return CallWindowProc(g_originalWndProcHandler, hWnd, uMsg, wParam, lParam);
}

void Menu::QueueImGuiMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
{
    std::lock_guard<std::mutex> lock(g_imguiMessagesMutex);
    // A move after a move: only where the mouse went last matters
    if (IsMouseMove(message) && !g_imguiMessages.empty() && IsMouseMove(g_imguiMessages.back().message))
    {
        g_imguiMessages.back() = { window, message, wParam, lParam };
        return;
    }
    // Full (no frame presented for a while): the oldest move goes first, and the oldest of the rest only once no move is
    // left, so a key or button is never kept pressed while its release is lost
    if (g_imguiMessages.size() >= kMaxImGuiMessages)
    {
        auto oldest = std::find_if(g_imguiMessages.begin(), g_imguiMessages.end(), [](const ImGuiMessage& queued) { return IsMouseMove(queued.message); });
        g_imguiMessages.erase(oldest != g_imguiMessages.end() ? oldest : g_imguiMessages.begin());
    }
    g_imguiMessages.push_back({ window, message, wParam, lParam });
}

void Menu::InitMenu(IDXGISwapChain* pSwapChain)
{
    g_swapChain = pSwapChain;
}

IDXGISwapChain* Menu::GetSwapChain()
{
    return g_swapChain;
}

bool Menu::IsInitialised()
{
    return g_swapChain != nullptr;
}

bool Menu::UsePresentingSwapChain(IDXGISwapChain* pSwapChain)
{
    // The first swap chain made in the process needn't be the game's: the Epic Games Store build's online services make
    // one of their own as the game starts, and let it go, so the overlay is set up on the one frames are presented with
    if (!g_menuInitialised)
        g_swapChain = pSwapChain;
    return pSwapChain == g_swapChain;
}

// The overlay's font: ImGui's own (ProggyClean), with Consolas (else Lucida Console) from Windows merged in for what it
// lacks that text from the game can hold - Windows-1252's dashes, quotes, bullet, ellipsis, per mille, trademark and its
// few extra letters (the game's text is turned into UTF-8 before it is drawn). Glyphs go into the atlas as first drawn.
static void AddFonts(ImGuiIO& io)
{
    // ProggyClean as ImGui adds it by default, but with its size given, so the merged font can be given the same 1 px drop
    ImFontConfig base;
    base.SizePixels = 13.0f;
    base.PixelSnapH = true;
    io.Fonts->AddFontDefaultBitmap(&base);

    static const ImWchar kRanges[] = {
        0x0020, 0x00FF,                                                 // Basic Latin, Latin-1
        0x0152, 0x0153, 0x0160, 0x0161, 0x0178, 0x0178, 0x017D, 0x017E, // Windows-1252's other letters
        0x0192, 0x0192, 0x02C6, 0x02C6, 0x02DC, 0x02DC,
        0x2010, 0x2027, 0x2030, 0x203A,                                 // General Punctuation
        0x20AC, 0x20AC, 0x2122, 0x2122,                                 // euro, trademark
        0,
    };
    ImFontConfig merge;
    merge.MergeMode = true;
    merge.PixelSnapH = true;
    merge.GlyphOffset.y = 1.0f;
    merge.GlyphRanges = kRanges; // read up front only by a renderer without dynamic font textures
    merge.Flags |= ImFontFlags_NoLoadError;
    char windows[MAX_PATH] = {};
    const UINT length = GetWindowsDirectoryA(windows, MAX_PATH);
    if (length == 0 || length >= MAX_PATH)
        return;
    const char* const kFiles[] = { "consola.ttf", "lucon.ttf" };
    for (const char* file : kFiles)
    {
        const std::string path = std::string(windows) + "\\Fonts\\" + file;
        if (io.Fonts->AddFontFromFileTTF(path.c_str(), 13.0f, &merge))
            break;
    }
}

void Menu::DrawMenu() {
    if (!g_menuInitialised) {
        if (FAILED(GetDeviceAndContextFromSwapChain(g_swapChain, &g_device, &g_context))) {
            DebugBreak();
            return;
        }

        IMGUI_CHECKVERSION();
        ImGui::CreateContext();
        ImGuiIO& io = ImGui::GetIO(); (void)io;
    	// Enable ImGUI support for keyboard and gamepad input.
        io.ConfigFlags |= ImGuiConfigFlags_NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags_NavEnableGamepad;

        // Get the swapchain description (we use this to get the handle to the game's window).
        DXGI_SWAP_CHAIN_DESC dxgiSwapChainDesc;
        if (FAILED(g_swapChain->GetDesc(&dxgiSwapChainDesc)))
        {
	        DebugBreak();
        	return;
        }

        ImGui::StyleColorsDark();
        AddFonts(io);
    	
        g_hWindow = dxgiSwapChainDesc.OutputWindow;
        // Replace the game's window handler with our own so we can intercept key press events in the game.
        g_originalWndProcHandler = reinterpret_cast<WNDPROC>(SetWindowLongPtr(g_hWindow, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(WndProcHandler)));

        ImGui_ImplWin32_Init(g_hWindow);
        ImGui_ImplDX11_Init(g_device, g_context);

        if (!CreateRenderTarget())
        {
            DebugBreak();
            return;
        }

        g_menuInitialised = true;
    }

    ImGui_ImplDX11_NewFrame();
    // ImGui's input, given to it here on the thread that presents: the window messages queued since the last frame, and
    // where the cursor is now (needed with no window of ours open too, when no mouse move is sent to it)
    FeedQueuedImGuiInput();
    {
        ImGuiIO& io = ImGui::GetIO();
        POINT cursorPos;
        if (GetCursorPos(&cursorPos) && ScreenToClient(g_hWindow, &cursorPos))
        {
            io.MousePos.x = static_cast<float>(cursorPos.x);
            io.MousePos.y = static_cast<float>(cursorPos.y);
        }
    }
    ImGui_ImplWin32_NewFrame();
    ImGui::NewFrame();

    // On-screen text from DebugText script entities.
    if (Config::AnyDebug())
    {
        DEBUG_TEXT::DrawOverlay();
        DEBUG_MARKER::DrawOverlay();
    }

    // The debug script entities (DebugGraph, DebugObjectMarker...) with the debug menu off: the camera and the render
    // thread's work kept going for them, and their overlay. With the debug menu on, it draws them as one of its overlays.
    if (Config::AnyScriptEntity() && !Config::Get().debugMenu)
    {
        DEBUG_MENU::KeepUpWithoutWindow();
        SCRIPT_ENTITIES::DrawOverlay();
    }

    // Unlock Mouse: a note when its key is pressed.
    if (Config::Get().unlockMouse)
        MOUSE_UNLOCK::DrawOverlay();

    // Possess: what is possessed and its keys.
    if (Config::Get().possess)
        POSSESS::DrawOverlay();

    // The debug menu: its overlays over the game, and its window while open.
    if (Config::Get().debugMenu)
        DEBUG_MENU::Draw();

    // Live Link: a quiet note in the top right while OpenCAGE is connected, with what it last did for a few seconds. Drawn
    // after the debug menu, so it can start under the notes along the top of the screen while any shows (a long one, such
    // as the scripts held at a breakpoint, runs right across). The "OpenCAGE Live Link" label can be hidden (OpenCAGE's
    // Options > Misc > Hide Live Link Label In-Game): the lines under it then start where it would be.
    if (Config::Get().liveLink && LIVE_LINK_SERVER::Connected())
    {
        ImDrawList* drawList = ImGui::GetForegroundDrawList();
        const float width = ImGui::GetIO().DisplaySize.x;
        const float size = 16.0f;
        const float top = Config::Get().debugMenu ? (std::max)(10.0f, DEBUG_MENU::BannersBottom()) : 10.0f;
        float line = top;
        if (LIVE_LINK_SERVER::ShowLabel())
        {
            const char* label = "OpenCAGE Live Link";
            const float labelWidth = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, label).x;
            drawList->AddText(ImGui::GetFont(), size, ImVec2(width - labelWidth - 12.0f, top), IM_COL32(120, 220, 140, 220), label);
            line += 20.0f;
        }
        // Camera sync: for as long as the game renders from OpenCAGE's viewport camera.
        if (LIVE_CAMERA::Applying())
        {
            const char* camera = "Camera: following OpenCAGE";
            const float cameraWidth = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, camera).x;
            drawList->AddText(ImGui::GetFont(), size, ImVec2(width - cameraWidth - 12.0f, line), IM_COL32(120, 220, 140, 220), camera);
            line += 20.0f;
        }
        // A cubemap capture: what it is waiting for, or which face it is on
        const std::string capture = CUBEMAP_CAPTURE::OverlayText();
        if (!capture.empty())
        {
            const float captureWidth = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, capture.c_str()).x;
            drawList->AddText(ImGui::GetFont(), size, ImVec2(width - captureWidth - 12.0f, line), IM_COL32(120, 220, 140, 220), capture.c_str());
            line += 20.0f;
            // ... and that the game's keys and mouse wait for it
            const char* held = "Keys and mouse ignored until it's done - Escape stops it";
            const float heldWidth = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, held).x;
            drawList->AddText(ImGui::GetFont(), size, ImVec2(width - heldWidth - 12.0f, line), IM_COL32(230, 230, 230, 170), held);
            line += 20.0f;
        }
        const std::string activity = LIVE_LINK_SERVER::LastActivity(4000);
        if (!activity.empty())
        {
            const float activityWidth = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, activity.c_str()).x;
            drawList->AddText(ImGui::GetFont(), size, ImVec2(width - activityWidth - 12.0f, line), IM_COL32(230, 230, 230, 220), activity.c_str());
        }
    }

    if (g_showMenu)
    {
        const ImVec2 center = ImGui::GetMainViewport()->GetCenter();
        ImGui::SetNextWindowPos(center, ImGuiCond_Appearing, ImVec2(0.5f, 0.5f));

        ImGui::Begin("Settings", nullptr, ImGuiWindowFlags_MenuBar | ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoSavedSettings);

        static char levelName[128] = "";
        ImGui::InputTextWithHint("Level name", "e.g. TECH_RnD_HzdLab", levelName, IM_ARRAYSIZE(levelName));
        if (ImGui::Button("Load to provided level")) LoadLevel(levelName);

        ImGui::End();
    }
    ImGui::EndFrame();

    ImGui::Render();
    // The game's own capture takes its files from the frame as it ends up: nothing of ours is drawn while one is under way
    if (CINEMATIC_CAPTURE::HideDrawing())
        return;
    // Let go of when the game resized its back buffers (a new resolution): made again on the new one
    if (!g_renderTargetView && !CreateRenderTarget())
        return;
    g_context->OMSetRenderTargets(1, &g_renderTargetView, nullptr);
    ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());
}

bool Menu::CreateRenderTarget()
{
    ID3D11Texture2D* pBackBuffer = nullptr;
    if (FAILED(g_swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<LPVOID*>(&pBackBuffer))) || !pBackBuffer)
        return false;
    const bool made = SUCCEEDED(g_device->CreateRenderTargetView(pBackBuffer, nullptr, &g_renderTargetView));
    pBackBuffer->Release();
    return made;
}

void Menu::ReleaseRenderTarget(IDXGISwapChain* pSwapChain)
{
    // The swap chain can only resize its back buffers once nothing else holds them: the overlay's view of the first one
    // goes first (a change of resolution failed, leaving the picture the wrong size, while it was held)
    if (pSwapChain != g_swapChain || !g_renderTargetView)
        return;
    g_renderTargetView->Release();
    g_renderTargetView = nullptr;
}

bool Menu::LoadLevel(std::string name) {
    std::string levelToLoad = "Production\\" + name;
    const int level = GAME_LEVEL_MANAGER::get_level_from_name(GAME_LEVEL_MANAGER::m_instance, const_cast<char*>(levelToLoad.c_str()));
    return LoadLevel(level);
}
bool Menu::LoadLevel(int id) {
    if (id == 0) return false;
    GAME_LEVEL_MANAGER::queue_level(GAME_LEVEL_MANAGER::m_instance, id);
    GAME_LEVEL_MANAGER::request_next_level(GAME_LEVEL_MANAGER::m_instance, false);
    return true;
}

void Menu::ShutdownMenu() {
    ImGui_ImplDX11_Shutdown();
    ImGui_ImplWin32_Shutdown();
    ImGui::DestroyContext();
}

// Code taken (and changed a little) from Niemand's wonderful article on hooking DX11 and using ImGUI.
// https://niemand.com.ar/2019/01/01/how-to-hook-directx-11-imgui/
HRESULT Menu::GetDeviceAndContextFromSwapChain(IDXGISwapChain* pSwapChain, ID3D11Device** ppDevice, ID3D11DeviceContext** ppContext) {
    const HRESULT ret = pSwapChain->GetDevice(__uuidof(ID3D11Device), reinterpret_cast<PVOID*>(ppDevice));

    if (SUCCEEDED(ret)) {
        (*ppDevice)->GetImmediateContext(ppContext);
    }

    return ret;
}