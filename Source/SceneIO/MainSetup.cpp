#include "MainSetup.h"

/* Because we can't create this function as a callback in dxmain, here's a wrapper for it in global namespace */
namespace {
	MainSetup* g_pApp = nullptr;
}
LRESULT CALLBACK MainWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) 
{
	if (g_pApp) 
	{
		return g_pApp->MsgProc(hwnd, msg, wParam, lParam);
	}
	return DefWindowProc(hwnd, msg, wParam, lParam);
}

/* Initialise the default window configuration */
MainSetup::MainSetup(HINSTANCE hInstance)
{
	m_hAppInstance = hInstance;
	m_hAppWnd = NULL;
	m_clientWidth = 1280;
	m_clientHeight = 720; //todo: alt sizes
	m_appTitle = "OpenCAGE Cathode Editor";
	m_wndStyle = WS_OVERLAPPED | WS_CAPTION | WS_MINIMIZEBOX | WS_SYSMENU; //todo: do we want this style?

	g_pApp = this;

	m_pDevice = nullptr;
	m_pImmediateContext = nullptr;
	m_pRenderTargetView = nullptr;
	m_pSwapChain = nullptr;
}

/* Release our d3d resources on exit */
MainSetup::~MainSetup()
{
	if (m_pImmediateContext)
	{
		m_pImmediateContext->ClearState();
	}

	Memory::SafeRelease(m_pRenderTargetView);
	Memory::SafeRelease(m_pSwapChain);
	Memory::SafeRelease(m_pImmediateContext);
	Memory::SafeRelease(m_pDevice);

	imnodes::Shutdown();
	ImGui::DestroyContext();
}

/* Keep an eye on Windows messages, and call Update/Render if we aren't told to exit */
int MainSetup::Run()
{
	MSG msg = { 0 };
	static double gameTime = 0.0f;
	static double prevTime = 0.0f;
	static ULONGLONG timeStart = 0;
	while (WM_QUIT != msg.message) 
	{
		if (PeekMessage(&msg, NULL, NULL, NULL, PM_REMOVE)) 
		{
			TranslateMessage(&msg);
			DispatchMessage(&msg);
		}
		else
		{
			//Update our game time 
			if (m_driverType == D3D_DRIVER_TYPE_REFERENCE)
			{
				gameTime += (float)DirectX::XM_PI * 0.0125f;
			}
			else
			{
				ULONGLONG timeCur = GetTickCount64();
				if (timeStart == 0)
					timeStart = timeCur;
				gameTime = (timeCur - timeStart) / 1000.0f;
			}

			//Update our frame time
			double frameTime = 0.0f;
			if (prevTime != 0.0f) 
			{
				frameTime = gameTime - prevTime;
			}
			prevTime = gameTime;

			if (!Update(frameTime)) break;
			Render(frameTime);
		}
	}
	return static_cast<int>(msg.wParam);
}

/* Set appropriate properties and then initialise window */
bool MainSetup::Init()
{
	srand(time(NULL));
	return InitWindow() && InitDirectX() && InitImGUI();
}

/* Set appropriate properties and then initialise window */
bool MainSetup::InitWindow()
{
	//Setup window class
	WNDCLASSEX wcex;
	ZeroMemory(&wcex, sizeof(WNDCLASSEX));
	wcex.cbClsExtra = 0;
	wcex.cbWndExtra = 0;
	wcex.cbSize = sizeof(WNDCLASSEX);
	wcex.style = CS_HREDRAW | CS_VREDRAW;
	wcex.hInstance = m_hAppInstance;
	wcex.lpfnWndProc = MainWndProc; //The global namespace wrapper (above)
	wcex.hIcon = LoadIcon(NULL, IDI_APPLICATION); //todo: set the window icon here as imported
	wcex.hIconSm = LoadIcon(NULL, IDI_APPLICATION);
	wcex.hCursor = LoadCursor(NULL, IDC_ARROW); //todo: custom cursor?
	wcex.hbrBackground = (HBRUSH)GetStockObject(NULL_BRUSH);
	wcex.lpszMenuName = NULL;
	wcex.lpszClassName = "dxmain";

	if (!RegisterClassEx(&wcex))
	{
		Debug::Log("Failed to setup window class in dxmain!!");
		return false;
	}

	//Adjust window size for no menu bar
	RECT r = { 0, 0, m_clientWidth, m_clientHeight };
	AdjustWindowRect(&r, m_wndStyle, FALSE);

	//Get metrics for window
	UINT width = r.right - r.left;
	UINT height = r.bottom - r.top;
	UINT xPos = (GetSystemMetrics(SM_CXSCREEN) / 2) - (width / 2);
	UINT yPos = (GetSystemMetrics(SM_CYSCREEN) / 2) - (height / 2);

	//Create window
	m_hAppWnd = CreateWindow("dxmain", m_appTitle.c_str(), m_wndStyle, xPos, yPos, width, height, NULL, NULL, m_hAppInstance, NULL);

	if (!m_hAppWnd)
	{
		Debug::Log("Failed to create window in dxmain!!");
		return false;
	}
	Shared::m_hwnd = m_hAppWnd;

	//Show window
	ShowWindow(m_hAppWnd, SW_SHOW);
	return true;
}

/* Set appropriate properties and then initialise d3d to the window */
bool MainSetup::InitDirectX()
{
	//Request d3d debugging if in debug
	UINT createDeviceFlags = 0;
#if _DEBUG
	createDeviceFlags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

	//Supported driver types (in order of priority)
	D3D_DRIVER_TYPE driverTypes[] =
	{
		D3D_DRIVER_TYPE_HARDWARE,
		D3D_DRIVER_TYPE_WARP,
		D3D_DRIVER_TYPE_REFERENCE
	};
	UINT numDriverTypes = ARRAYSIZE(driverTypes);

	//Supported d3d feature levels
	D3D_FEATURE_LEVEL featureLevels[] =
	{
		D3D_FEATURE_LEVEL_11_0,
		D3D_FEATURE_LEVEL_10_1,
		D3D_FEATURE_LEVEL_10_0,
		D3D_FEATURE_LEVEL_9_3
	};
	UINT numFeatureLevels = ARRAYSIZE(featureLevels);

	//Create & further setup swapchain class
	DXGI_SWAP_CHAIN_DESC swapDesc;
	ZeroMemory(&swapDesc, sizeof(DXGI_SWAP_CHAIN_DESC));

	//Buffering
	swapDesc.BufferCount = 1; //Double buffered

	//Buffer size and format
	swapDesc.BufferDesc.Width = m_clientWidth;
	swapDesc.BufferDesc.Height = m_clientHeight;
	swapDesc.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM; //32-bit unsigned normal

	//Target FPS
	swapDesc.BufferDesc.RefreshRate.Numerator = 60;
	swapDesc.BufferDesc.RefreshRate.Denominator = 1;

	//Output to our window
	swapDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
	swapDesc.OutputWindow = m_hAppWnd;
	swapDesc.Windowed = true; //Start as windowed
	swapDesc.Flags = DXGI_SWAP_CHAIN_FLAG_ALLOW_MODE_SWITCH; //Allow alt+enter fullscreen (doesn't resize buffer)

	//Operation
	swapDesc.SwapEffect = DXGI_SWAP_EFFECT_DISCARD; //Most efficient

	//Multisampling
	swapDesc.SampleDesc.Count = 1;
	swapDesc.SampleDesc.Quality = 0;

	//Try create the d3d device and swap chain
	HRESULT result;
	for (int i = 0; i < numDriverTypes; i++) 
	{
		result = D3D11CreateDeviceAndSwapChain(NULL, driverTypes[i], NULL, createDeviceFlags, featureLevels, numFeatureLevels, D3D11_SDK_VERSION, &swapDesc, &m_pSwapChain, &m_pDevice, &m_featureLevel, &m_pImmediateContext);
		if (SUCCEEDED(result)) 
		{
			m_driverType = driverTypes[i];
			break;
		}
	}

	if (FAILED(result)) 
	{
		Debug::Log("Failed to create device and/or swap chain!!");
		return false;
	}

	//Create the render target view
	ID3D11Texture2D* m_pBackBufferTex = NULL;
	HR(m_pSwapChain->GetBuffer(NULL, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&m_pBackBufferTex)));
	HR(m_pDevice->CreateRenderTargetView(m_pBackBufferTex, nullptr, &m_pRenderTargetView));
	Memory::SafeRelease(m_pBackBufferTex);

	//Create depth stencil texture
	D3D11_TEXTURE2D_DESC descDepth;
	ZeroMemory(&descDepth, sizeof(descDepth));
	descDepth.Width = m_clientWidth;
	descDepth.Height = m_clientHeight;
	descDepth.MipLevels = 1;
	descDepth.ArraySize = 1;
	descDepth.Format = DXGI_FORMAT_D24_UNORM_S8_UINT;
	descDepth.SampleDesc.Count = 1;
	descDepth.SampleDesc.Quality = 0;
	descDepth.Usage = D3D11_USAGE_DEFAULT;
	descDepth.BindFlags = D3D11_BIND_DEPTH_STENCIL;
	descDepth.CPUAccessFlags = 0;
	descDepth.MiscFlags = 0;
	HR(m_pDevice->CreateTexture2D(&descDepth, nullptr, &g_pDepthStencil));

	//Create the depth stencil view
	D3D11_DEPTH_STENCIL_VIEW_DESC descDSV;
	ZeroMemory(&descDSV, sizeof(descDSV));
	descDSV.Format = descDepth.Format;
	descDSV.ViewDimension = D3D11_DSV_DIMENSION_TEXTURE2D;
	descDSV.Texture2D.MipSlice = 0;
	HR(m_pDevice->CreateDepthStencilView(g_pDepthStencil, &descDSV, &g_pDepthStencilView));

	//Bind the render target view
	m_pImmediateContext->OMSetRenderTargets(1, &m_pRenderTargetView, g_pDepthStencilView); 

	//Set rasterizer state
	D3D11_RASTERIZER_DESC wfdesc;
	ZeroMemory(&wfdesc, sizeof(D3D11_RASTERIZER_DESC));
	wfdesc.FillMode = D3D11_FILL_SOLID;
	wfdesc.CullMode = D3D11_CULL_BACK;
	HR(m_pDevice->CreateRasterizerState(&wfdesc, &WireFrame));
	m_pImmediateContext->RSSetState(WireFrame);

	//Create the viewport
	m_viewport.Width = static_cast<float>(m_clientWidth);
	m_viewport.Height = static_cast<float>(m_clientHeight);
	m_viewport.TopLeftX = 0;
	m_viewport.TopLeftY = 0;
	m_viewport.MinDepth = 0.0f;
	m_viewport.MaxDepth = 1.0f;

	//Bind the viewport
	m_pImmediateContext->RSSetViewports(1, &m_viewport);

	//Share out render size (todo: will need to be done on update for resizing)
	Shared::m_renderWidth = m_clientWidth;
	Shared::m_renderHeight = m_clientHeight;
	
	//Share out the device and device context
	Shared::m_pDevice = m_pDevice;
	Shared::m_pImmediateContext = m_pImmediateContext;

	//Set default ambient lighting
	Shared::ambientLightColour = XMFLOAT4(1.0f, 1.0f, 1.0f, 0.0f);

	//Set topology for rendering
	m_pImmediateContext->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

	//Initialize the view matrix 
	DirectX::XMVECTOR Eye = DirectX::XMVectorSet(0.0f, 1.0f, -5.0f, 0.0f);
	DirectX::XMVECTOR At = DirectX::XMVectorSet(0.0f, 1.0f, 0.0f, 0.0f);
	DirectX::XMVECTOR Up = DirectX::XMVectorSet(0.0f, 1.0f, 0.0f, 0.0f);
	Shared::mView = DirectX::XMMatrixLookAtLH(Eye, At, Up);

	//Initialize the projection matrix 
	Shared::mProjection = DirectX::XMMatrixPerspectiveFovLH(Shared::cameraFOV, Shared::m_renderWidth / (FLOAT)Shared::m_renderHeight, Shared::cameraNear, Shared::cameraFar);
	
	return true;
}

/* Initialise ImGUI */
bool MainSetup::InitImGUI()
{
	IMGUI_CHECKVERSION();
	ImGui::CreateContext();

	ImGuiIO& io = ImGui::GetIO();
	io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
	io.Fonts->AddFontFromFileTTF("data\\ui_resources\\Ruda-Bold.ttf", 14);
	io.Fonts->AddFontFromFileTTF("data\\ui_resources\\Ruda-Bold.ttf", 12);
	io.Fonts->AddFontFromFileTTF("data\\ui_resources\\Ruda-Bold.ttf", 16);
	io.Fonts->AddFontFromFileTTF("data\\ui_resources\\Ruda-Bold.ttf", 20);

	InputHandler::Setup();

	ImGui_ImplWin32_Init(m_hAppWnd);
	ImGui_ImplDX11_Init(m_pDevice, m_pImmediateContext);

	//https://www.unknowncheats.me/forum/c-and-c-/189635-imgui-style-settings.html
	ImGuiStyle* style = &ImGui::GetStyle();
	style->WindowPadding = ImVec2(0, 0);
	style->WindowRounding = 0.0f;
	style->FramePadding = ImVec2(5, 5);
	style->FrameRounding = 4.0f;
	style->ItemSpacing = ImVec2(12, 8);
	style->ItemInnerSpacing = ImVec2(8, 6);
	style->IndentSpacing = 25.0f;
	style->ScrollbarSize = 15.0f;
	style->ScrollbarRounding = 9.0f;
	style->GrabMinSize = 5.0f;
	style->GrabRounding = 3.0f;
	style->Colors[ImGuiCol_Text] = ImVec4(0.80f, 0.80f, 0.83f, 1.00f);
	style->Colors[ImGuiCol_TextDisabled] = ImVec4(0.24f, 0.23f, 0.29f, 1.00f);
	style->Colors[ImGuiCol_WindowBg] = ImVec4(0.06f, 0.05f, 0.07f, 1.00f);
	//style->Colors[ImGuiCol_ChildWindowBg] = ImVec4(0.07f, 0.07f, 0.09f, 1.00f);
	style->Colors[ImGuiCol_PopupBg] = ImVec4(0.07f, 0.07f, 0.09f, 1.00f);
	style->Colors[ImGuiCol_Border] = ImVec4(0.33f, 0.33f, 0.33f, 0.88f);
	style->Colors[ImGuiCol_BorderShadow] = ImVec4(0.22f, 0.22f, 0.22f, 0.00f);
	style->Colors[ImGuiCol_FrameBg] = ImVec4(0.10f, 0.09f, 0.12f, 1.00f);
	style->Colors[ImGuiCol_FrameBgHovered] = ImVec4(0.24f, 0.23f, 0.29f, 1.00f);
	style->Colors[ImGuiCol_FrameBgActive] = ImVec4(0.56f, 0.56f, 0.58f, 1.00f);
	style->Colors[ImGuiCol_TitleBg] = ImVec4(0.10f, 0.09f, 0.12f, 1.00f);
	style->Colors[ImGuiCol_TitleBgCollapsed] = ImVec4(1.00f, 0.98f, 0.95f, 0.75f);
	style->Colors[ImGuiCol_TitleBgActive] = ImVec4(0.07f, 0.07f, 0.09f, 1.00f);
	style->Colors[ImGuiCol_MenuBarBg] = ImVec4(0.10f, 0.09f, 0.12f, 1.00f);
	style->Colors[ImGuiCol_ScrollbarBg] = ImVec4(0.10f, 0.09f, 0.12f, 1.00f);
	style->Colors[ImGuiCol_ScrollbarGrab] = ImVec4(0.80f, 0.80f, 0.83f, 0.31f);
	style->Colors[ImGuiCol_ScrollbarGrabHovered] = ImVec4(0.56f, 0.56f, 0.58f, 1.00f);
	style->Colors[ImGuiCol_ScrollbarGrabActive] = ImVec4(0.06f, 0.05f, 0.07f, 1.00f);
	//style->Colors[ImGuiCol_ComboBg] = ImVec4(0.19f, 0.18f, 0.21f, 1.00f);
	style->Colors[ImGuiCol_CheckMark] = ImVec4(0.80f, 0.80f, 0.83f, 0.31f);
	style->Colors[ImGuiCol_SliderGrab] = ImVec4(0.80f, 0.80f, 0.83f, 0.31f);
	style->Colors[ImGuiCol_SliderGrabActive] = ImVec4(0.06f, 0.05f, 0.07f, 1.00f);
	style->Colors[ImGuiCol_Button] = ImVec4(0.10f, 0.09f, 0.12f, 1.00f);
	style->Colors[ImGuiCol_ButtonHovered] = ImVec4(0.24f, 0.23f, 0.29f, 1.00f);
	style->Colors[ImGuiCol_ButtonActive] = ImVec4(0.56f, 0.56f, 0.58f, 1.00f);
	style->Colors[ImGuiCol_Header] = ImVec4(0.3f, 0.3f, 0.3f, 1.00f);
	style->Colors[ImGuiCol_HeaderHovered] = ImVec4(0.33f, 0.33f, 0.33f, 1.00f);
	style->Colors[ImGuiCol_HeaderActive] = ImVec4(0.06f, 0.05f, 0.07f, 1.00f);
	//style->Colors[ImGuiCol_Column] = ImVec4(0.56f, 0.56f, 0.58f, 1.00f);
	//style->Colors[ImGuiCol_ColumnHovered] = ImVec4(0.24f, 0.23f, 0.29f, 1.00f);
	//style->Colors[ImGuiCol_ColumnActive] = ImVec4(0.56f, 0.56f, 0.58f, 1.00f);
	style->Colors[ImGuiCol_ResizeGrip] = ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
	style->Colors[ImGuiCol_ResizeGripHovered] = ImVec4(0.56f, 0.56f, 0.58f, 1.00f);
	style->Colors[ImGuiCol_ResizeGripActive] = ImVec4(0.06f, 0.05f, 0.07f, 1.00f);
	//style->Colors[ImGuiCol_CloseButton] = ImVec4(0.40f, 0.39f, 0.38f, 0.16f);
	//style->Colors[ImGuiCol_CloseButtonHovered] = ImVec4(0.40f, 0.39f, 0.38f, 0.39f);
	//style->Colors[ImGuiCol_CloseButtonActive] = ImVec4(0.40f, 0.39f, 0.38f, 1.00f);
	style->Colors[ImGuiCol_PlotLines] = ImVec4(0.40f, 0.39f, 0.38f, 0.63f);
	style->Colors[ImGuiCol_PlotLinesHovered] = ImVec4(0.25f, 1.00f, 0.00f, 1.00f);
	style->Colors[ImGuiCol_PlotHistogram] = ImVec4(0.40f, 0.39f, 0.38f, 0.63f);
	style->Colors[ImGuiCol_PlotHistogramHovered] = ImVec4(0.25f, 1.00f, 0.00f, 1.00f);
	style->Colors[ImGuiCol_TextSelectedBg] = ImVec4(0.25f, 1.00f, 0.00f, 0.43f);
	style->Colors[ImGuiCol_ModalWindowDarkening] = ImVec4(1.00f, 0.98f, 0.95f, 0.73f);

	imnodes::Initialize();
	imnodes::SetNodeGridSpacePos(1, ImVec2(200.0f, 200.0f));

	return true;
}

/* Handle windows messages */
extern LRESULT ImGui_ImplWin32_WndProcHandler(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
LRESULT MainSetup::MsgProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
	if (ImGui_ImplWin32_WndProcHandler(hwnd, msg, wParam, lParam))
		return true;

	switch (msg) 
	{
		case WM_DESTROY:
			PostQuitMessage(0);
			return 0;
		case WM_KEYDOWN:
			//InputHandler::SetKeyState((WindowsKey)((unsigned long)wParam), true);
			break;
		case WM_KEYUP:
			//InputHandler::SetKeyState((WindowsKey)((unsigned long)wParam), false);
			break;
	}
	return DefWindowProc(hwnd, msg, wParam, lParam);
}