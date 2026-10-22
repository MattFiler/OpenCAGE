#include "DEBUG_MENU.h"
#include "DEBUG_MARKER.h"
#include "LIVE_LINK.h"
#include "MOUSE_UNLOCK.h"
#include "Menu.h"
#include "Config.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cfloat>
#include <cmath>
#include <cstdio>
#include <chrono>
#include <condition_variable>
#include <cstring>
#include <memory>
#include <mutex>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// Where the game keeps its pointer to its globals; the byte after that pointer is set while its characters can be read
	// (the two sit together in every build)
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kCharactersValid = 0x04;
	// In the globals, the character manager (a pointer), and its lists of characters: a pointer to their pointers, then how
	// many - the players, those alive that aren't players, and the dead
	constexpr uint32_t kGlobalsCharacters = 0x08;
	constexpr uint32_t kPlayers = 0x80;
	constexpr uint32_t kAlive = 0xD0;
	constexpr uint32_t kDead = 0x120;
	constexpr uint32_t kMaxPerList = 18;

	// A character's fields
	constexpr uint32_t kCharacterName = 0x10;      // its debug name (64 characters)
	constexpr uint32_t kNameLength = 0x40;
	constexpr uint32_t kCharacterId = 0x50;        // its unique id
	constexpr uint32_t kCharacterSpawnedAs = 0x64; // what it was spawned as (a pointer): its class at +4
	constexpr uint32_t kSpawnedAsClass = 0x04;
	constexpr uint32_t kCharacterPosition = 0x70;  // x, y, z
	constexpr uint32_t kCharacterFacing = 0x174;   // yaw in radians

	// Each AddTab is a section; the sections of one name in one group make a tab (sections drawn lowest order first), and
	// the tabs of one group a row of tabs under that group's
	struct Section { std::string name; std::string group; int order; DEBUG_MENU::DrawTab draw; };
	struct Tab { std::string name; int order; std::vector<Section> sections; };
	struct Group { std::string name; int order; std::vector<Tab> tabs; };
	struct Overlay { int order; DEBUG_MENU::DrawOverlay draw; };

	// The groups along the top, in this order; any other after them, in the order of their lowest tab
	const char* const kGroupOrder[] = { "Overview", "Characters", "AI", "World", "Scripts", "Sound", "Rendering", "Performance", "Game", "Cinematic" };

	// Filled by static initializers, so made on first use
	std::vector<Section>& Sections() { static std::vector<Section> sections; return sections; }
	std::vector<Group>& Groups() { static std::vector<Group> groups; return groups; }
	std::vector<Overlay>& Overlays() { static std::vector<Overlay> overlays; return overlays; }

	// The named commands and values, and the views' hooks (made on first use the same way)
	struct NamedCommand { std::string name; std::string help; DEBUG_MENU::Command command; };
	struct NamedValue { std::string name; std::atomic<float>* value; float min; float max; };
	std::vector<NamedCommand>& Commands() { static std::vector<NamedCommand> commands; return commands; }
	std::vector<NamedValue>& Values() { static std::vector<NamedValue> values; return values; }
	std::vector<DEBUG_MENU::HookAttacher>& Hooks() { static std::vector<DEBUG_MENU::HookAttacher> hooks; return hooks; }
	bool g_hooksAttached = false;
	std::atomic<DEBUG_MENU::PickProvider> g_pickProvider = nullptr;

	void BuildGroups()
	{
		std::vector<Section> sections = Sections();
		std::stable_sort(sections.begin(), sections.end(), [](const Section& a, const Section& b) { return a.order < b.order; });
		std::vector<Group> groups;
		for (const Section& section : sections)
		{
			Tab* tab = nullptr;
			for (Group& group : groups)
				if (group.name == section.group)
					for (Tab& existing : group.tabs)
						if (existing.name == section.name)
							tab = &existing;
			if (!tab)
			{
				// A new tab, in its group (made when new: ordered by the list above, else after it by this, its lowest, tab)
				Group* group = nullptr;
				for (Group& existing : groups)
					if (existing.name == section.group)
						group = &existing;
				if (!group)
				{
					int order = 1000000 + section.order;
					for (int i = 0; i < static_cast<int>(sizeof(kGroupOrder) / sizeof(kGroupOrder[0])); i++)
						if (section.group == kGroupOrder[i])
							order = i;
					groups.push_back({ section.group, order, {} });
					group = &groups.back();
				}
				group->tabs.push_back({ section.name, section.order, {} });
				tab = &group->tabs.back();
			}
			tab->sections.push_back(section);
		}
		std::stable_sort(groups.begin(), groups.end(), [](const Group& a, const Group& b) { return a.order < b.order; });
		Groups().swap(groups);
	}

	std::atomic<bool> g_open = false;
	std::atomic<bool> g_typing = false;           // render thread writes: a text box in the window has the keyboard
	std::atomic<bool> g_mouseOverWindow = false;  // ... and the mouse is over the window (or dragging in it)
	bool g_cursorHeld = false;       // window thread: the cursor is held free while the window is open
	bool g_focusOnOpen = false;      // render thread: the window takes the keyboard when it opens
	HWND g_window = nullptr;

	// This frame's camera and characters (render thread only)
	DEBUG_MARKER::Matrix44 g_viewProjection;
	bool g_haveCamera = false;
	float g_cameraPosition[3] = {};
	float g_cameraForward[3] = {};
	// Where the window was last frame (render thread), for overlays that keep out of its way
	bool g_windowShown = false;
	ImVec2 g_windowMin, g_windowMax;
	ImVec2 g_display;
	// Where the next note along the top of the screen goes this frame (render thread)
	float g_bannerTop = 12.0f;
	std::vector<DEBUG_MENU::Character> g_characters;
	uint32_t g_selected = 0;

	// Work queued for the game's threads
	struct Queue
	{
		std::mutex mutex;
		std::vector<DEBUG_MENU::Work> work;
		std::atomic<bool> any = false;
	};
	Queue g_entityWork;
	Queue g_aiWork;
	Queue g_renderWork;

	void Enqueue(Queue& queue, DEBUG_MENU::Work work);

	// Work queued for a thread whose answer a command waits for
	DEBUG_MENU::CommandResult WaitForAnswer(Queue& queue, DEBUG_MENU::Answer work, int timeoutMs)
	{
		struct Waiting
		{
			std::mutex mutex;
			std::condition_variable done;
			bool finished = false;
			DEBUG_MENU::CommandResult result;
		};
		std::shared_ptr<Waiting> waiting = std::make_shared<Waiting>();
		Enqueue(queue, [waiting, work]()
		{
			DEBUG_MENU::CommandResult result = work();
			std::lock_guard<std::mutex> lock(waiting->mutex);
			waiting->result = std::move(result);
			waiting->finished = true;
			waiting->done.notify_all();
		});
		std::unique_lock<std::mutex> lock(waiting->mutex);
		if (!waiting->done.wait_for(lock, std::chrono::milliseconds(timeoutMs), [&]() { return waiting->finished; }))
			return { false, "Timed out waiting for the game (no level running, or paused?): it will still be done when the game comes round to it" };
		return waiting->result;
	}

	// The named switches, and what was asked from outside the window (a tab to bring to the front, a character to select
	// by name), and what the window shows, for Describe
	std::mutex g_outsideMutex;
	// (made on first use: a view may name its switches from a static initializer)
	std::vector<std::pair<std::string, std::atomic<bool>*>>& Switches() { static std::vector<std::pair<std::string, std::atomic<bool>*>> switches; return switches; }
	std::string g_wantedTab;
	std::string g_wantedCharacter;
	bool g_characterWanted = false;
	std::string g_currentGroup;
	std::string g_currentTab;
	std::string g_selectedText;

	// A tab asked for from outside, kept for a few frames while the window brings it to the front (render thread)
	std::string g_pendingTab;
	int g_pendingFrames = 0;

	std::string Lower(std::string text)
	{
		for (char& c : text)
			c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
		return text;
	}

	bool ContainsIgnoringCase(const std::string& text, const std::string& part)
	{
		return Lower(text).find(Lower(part)) != std::string::npos;
	}

	bool EqualsIgnoringCase(const std::string& a, const std::string& b)
	{
		return Lower(a) == Lower(b);
	}

	void Enqueue(Queue& queue, DEBUG_MENU::Work work)
	{
		std::lock_guard<std::mutex> lock(queue.mutex);
		queue.work.push_back(std::move(work));
		queue.any = true;
	}

	void RunQueued(Queue& queue)
	{
		if (!queue.any)
			return;
		std::vector<DEBUG_MENU::Work> work;
		{
			std::lock_guard<std::mutex> lock(queue.mutex);
			work.swap(queue.work);
			queue.any = false;
		}
		for (DEBUG_MENU::Work& item : work)
			item();
	}

	bool ReadCamera(DEBUG_MARKER::Matrix44& viewProjection, float position[3], float forward[3])
	{
		__try
		{
			void* cameraOwner = *reinterpret_cast<void**>(DEBUG_MARKER::engine + DEBUG_MARKER::kEngineCameraOffset);
			if (!cameraOwner)
				return false;
			const float* matrices = *reinterpret_cast<const float**>(static_cast<char*>(cameraOwner) + DEBUG_MARKER::kCameraMatricesOffset);
			if (!matrices)
				return false;
			DEBUG_MARKER::Matrix44 view, projection;
			memcpy(view.m, matrices, sizeof(view.m));
			memcpy(projection.m, matrices + 16, sizeof(projection.m));
			DEBUG_MARKER::matrix_multiply(&view, &viewProjection, &projection);
			// The camera's position from the view matrix (rows are its axes, the last its translation in view space)
			const float* v = view.m;
			for (int i = 0; i < 3; i++)
				position[i] = -(v[12] * v[i * 4 + 0] + v[13] * v[i * 4 + 1] + v[14] * v[i * 4 + 2]);
			// and the way it looks: its view axis (the third column of the rotation)
			for (int i = 0; i < 3; i++)
				forward[i] = v[i * 4 + 2];
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
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

	// One list of character pointers (up to max), guarded
	int ReadList(const uint8_t* manager, uint32_t offset, const uint8_t** out, int max)
	{
		__try
		{
			const uint8_t* const* list = *reinterpret_cast<const uint8_t* const* const*>(manager + offset);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + offset + 4);
			int found = 0;
			for (uint32_t i = 0; list && i < count && i < kMaxPerList && found < max; i++)
				if (list[i])
					out[found++] = list[i];
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	const uint8_t* CharacterManager()
	{
		__try
		{
			const uintptr_t globalsAt = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
			if (!globalsAt || !*reinterpret_cast<const uint8_t*>(globalsAt + kCharactersValid))
				return nullptr;
			const uint8_t* globals = *reinterpret_cast<const uint8_t* const*>(globalsAt);
			return globals ? *reinterpret_cast<const uint8_t* const*>(globals + kGlobalsCharacters) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	bool ReadCharacter(const uint8_t* object, DEBUG_MENU::Character& out)
	{
		char name[kNameLength + 1] = {};
		__try
		{
			memcpy(name, object + kCharacterName, kNameLength);
			out.id = *reinterpret_cast<const uint32_t*>(object + kCharacterId);
			memcpy(out.position, object + kCharacterPosition, sizeof(out.position));
			out.facing = *reinterpret_cast<const float*>(object + kCharacterFacing);
			const uint8_t* spawnedAs = *reinterpret_cast<const uint8_t* const*>(object + kCharacterSpawnedAs);
			out.characterClass = spawnedAs ? *reinterpret_cast<const int*>(spawnedAs + kSpawnedAsClass) : -1;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
		name[kNameLength] = '\0';
		out.name = name;
		out.object = object;
		return true;
	}

	void ReadCharacters()
	{
		g_characters.clear();
		if (!AnyLevel())
			return;
		const uint8_t* manager = CharacterManager();
		if (!manager)
			return;
		struct List { uint32_t offset; bool player; bool dead; };
		const List lists[] = { { kPlayers, true, false }, { kAlive, false, false }, { kDead, false, true } };
		for (const List& list : lists)
		{
			const uint8_t* objects[kMaxPerList];
			const int count = ReadList(manager, list.offset, objects, kMaxPerList);
			for (int i = 0; i < count; i++)
			{
				DEBUG_MENU::Character character;
				if (!ReadCharacter(objects[i], character))
					continue;
				character.player = list.player;
				character.dead = list.dead;
				// The player can be in the alive list too: listed once
				if (std::any_of(g_characters.begin(), g_characters.end(), [&](const DEBUG_MENU::Character& c) { return c.object == character.object; }))
					continue;
				g_characters.push_back(std::move(character));
			}
		}
	}

	void Open(bool open)
	{
		if (g_open.exchange(open) == open)
			return;
		if (open)
			g_focusOnOpen = true;
		// The window thread holds the cursor free (or lets it go) at its next message: one is sent now
		if (g_window)
			PostMessage(g_window, WM_NULL, 0, 0);
	}
}

namespace
{
	// Built-in commands, for testing the menu itself over the Live Link: which of the game's threads come round to queued
	// work, and this frame's characters
	const bool g_builtInCommands = []()
	{
		DEBUG_MENU::AddCommand("menu.ping_threads", "whether the entity, AI and render threads run queued work (each waited for up to a second)", [](const std::string&)
		{
			auto ping = [](DEBUG_MENU::CommandResult (*answer)(DEBUG_MENU::Answer, int)) { return answer([]() { return DEBUG_MENU::CommandResult{ true, "ok" }; }, 1000).ok ? "ok" : "timed out"; };
			DEBUG_MENU::CommandResult result;
			result.message = std::string("entity=") + ping(DEBUG_MENU::AnswerOnEntityThread) + "\nai=" + ping(DEBUG_MENU::AnswerOnAiThread) + "\nrender=" + ping(DEBUG_MENU::AnswerOnRenderThread);
			return result;
		});
		DEBUG_MENU::AddCommand("menu.characters", "this frame's characters: id, name, class, position, player/dead", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				DEBUG_MENU::CommandResult result;
				for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
				{
					char line[192];
					snprintf(line, sizeof(line), "%u %s (%s) %.2f,%.2f,%.2f%s%s\n", character.id, character.name.c_str(), DEBUG_MENU::ClassName(character.characterClass),
						character.position[0], character.position[1], character.position[2], character.player ? " player" : "", character.dead ? " dead" : "");
					result.message += line;
				}
				return result;
			});
		});
		return true;
	}();
}

bool DEBUG_MENU::AddTab(const char* name, int order, DrawTab draw, const char* group)
{
	// Static initializers only (one thread, before the first frame)
	Sections().push_back({ name, group ? group : "Debug", order, draw });
	BuildGroups();
	return true;
}

bool DEBUG_MENU::AddOverlay(int order, DrawOverlay draw)
{
	std::vector<Overlay>& overlays = Overlays();
	overlays.push_back({ order, draw });
	std::stable_sort(overlays.begin(), overlays.end(), [](const Overlay& a, const Overlay& b) { return a.order < b.order; });
	return true;
}

bool DEBUG_MENU::OnWindowMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam, LRESULT& result)
{
	g_window = window;
	if (message == WM_KEYUP && wParam == static_cast<WPARAM>(Config::Get().debugMenuKey))
		Open(!g_open);

	// The cursor held free while the window is open, and let go once it closes
	const bool open = g_open;
	if (open != g_cursorHeld)
	{
		g_cursorHeld = open;
		MOUSE_UNLOCK::Hold(open);
	}
	// The game window gaining or losing the focus, for the window's keys (any held down, Alt after Alt+Tab say, are let go
	// of when it loses it); the game has these too
	if (message == WM_SETFOCUS || message == WM_KILLFOCUS)
		Menu::QueueImGuiMessage(window, message, wParam, lParam);
	if (!open)
		return false;

	// While open, the keyboard and mouse are the window's: Escape closes it, the game's own input is kept from it
	if (message == WM_KEYDOWN && wParam == VK_ESCAPE)
	{
		Open(false);
		result = 0;
		return true;
	}
	if (message == WM_INPUT)
	{
		result = DefWindowProc(window, message, wParam, lParam);
		return true;
	}
	const bool keyboard = message >= WM_KEYFIRST && message <= WM_KEYLAST;
	const bool mouse = message >= WM_MOUSEFIRST && message <= WM_MOUSELAST;
	if (keyboard || mouse)
	{
		Menu::QueueImGuiMessage(window, message, wParam, lParam);
		// The game holds a key down from its press until its release, so it is given the release too (a key held as the
		// window opens would stay down in the game otherwise), bar Alt+Enter's, which switches it between windowed and full
		// screen
		if (message == WM_KEYUP || (message == WM_SYSKEYUP && wParam != VK_RETURN))
			return false;
		result = 0;
		return true;
	}
	return false;
}

void DEBUG_MENU::Draw()
{
	// The notes along the top of the screen start under the Unlock Mouse note while it shows
	ResetBanners((std::max)(12.0f, MOUSE_UNLOCK::NoteBottom() + 6.0f));

	ImGuiIO& io = ImGui::GetIO();
	g_display = io.DisplaySize;
	g_typing = g_open && io.WantTextInput;
	g_mouseOverWindow = g_open && io.WantCaptureMouse;
	g_haveCamera = ReadCamera(g_viewProjection, g_cameraPosition, g_cameraForward);
	ReadCharacters();

	// A character asked for by name from outside the window, and what is selected now, for Describe (before the queued
	// work, so a command that selects by name and then answers sees its character selected)
	std::string wantedTab;
	{
		std::lock_guard<std::mutex> lock(g_outsideMutex);
		if (g_characterWanted)
		{
			g_characterWanted = false;
			g_selected = 0;
			// By name, else by class (the game's own names can be odd: its alien is called "Alen")
			for (int pass = 0; pass < 2 && g_selected == 0 && !g_wantedCharacter.empty(); pass++)
				for (const Character& character : g_characters)
					if (ContainsIgnoringCase(pass == 0 ? character.name : std::string(ClassName(character.characterClass)), g_wantedCharacter))
					{
						g_selected = character.id;
						break;
					}
		}
		const Character* selected = Selected();
		g_selectedText = selected ? selected->name + " (" + std::to_string(selected->id) + ")" : "";
		wantedTab.swap(g_wantedTab);
	}
	RunQueued(g_renderWork);

	ImDrawList* drawList = ImGui::GetBackgroundDrawList();
	if (g_haveCamera)
		for (const Overlay& overlay : Overlays())
			overlay.draw(drawList);

	if (!g_open)
	{
		g_windowShown = false;
		return;
	}

	const float scale = Scale();
	ImGui::SetNextWindowPos(ImVec2(24.0f, 60.0f), ImGuiCond_Once);
	ImGui::SetNextWindowSize(ImVec2((std::min)(800.0f * scale, io.DisplaySize.x * 0.6f), io.DisplaySize.y * 0.8f), ImGuiCond_Once);
	if (g_focusOnOpen)
	{
		ImGui::SetNextWindowFocus();
		g_focusOnOpen = false;
	}
	bool open = true;
	char title[96];
	snprintf(title, sizeof(title), "Debug Menu (%s or Escape closes it)###DebugMenu", Config::KeyName(Config::Get().debugMenuKey));
	// The text scale for all of the window: its own text, and its child regions, tables, combos and tooltips
	ImGui::PushFont(nullptr, ImGui::GetStyle().FontSizeBase * scale);
	g_windowShown = ImGui::Begin(title, &open, ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoCollapse);
	g_windowMin = ImGui::GetWindowPos();
	g_windowMax = ImVec2(g_windowMin.x + ImGui::GetWindowSize().x, g_windowMin.y + ImGui::GetWindowSize().y);
	if (g_windowShown)
	{
		// A tab asked for from outside: one of that name (ignoring case), else one whose name holds it, else a group whose
		// name does. Asked of the group's row of tabs, then (once the group is in front, the frame after) of its own row
		if (!wantedTab.empty())
		{
			g_pendingTab = wantedTab;
			g_pendingFrames = 10;
		}
		const Group* wantedGroup = nullptr;
		const Tab* wantedTabItem = nullptr;
		if (g_pendingFrames > 0)
		{
			g_pendingFrames--;
			for (int pass = 0; pass < 2 && !wantedTabItem; pass++)
				for (const Group& group : Groups())
					for (const Tab& tab : group.tabs)
						if (!wantedTabItem && (pass == 0 ? EqualsIgnoringCase(tab.name, g_pendingTab) : ContainsIgnoringCase(tab.name, g_pendingTab)))
						{
							wantedTabItem = &tab;
							wantedGroup = &group;
						}
			for (const Group& group : Groups())
				if (!wantedGroup && ContainsIgnoringCase(group.name, g_pendingTab))
					wantedGroup = &group;
		}

		auto drawTab = [&](const Group& group, const Tab& tab)
		{
			{
				std::lock_guard<std::mutex> lock(g_outsideMutex);
				g_currentGroup = group.name;
				g_currentTab = tab.name;
			}
			if (&tab == wantedTabItem || (!wantedTabItem && &group == wantedGroup))
				g_pendingFrames = 0;
			for (size_t i = 0; i < tab.sections.size(); i++)
			{
				if (i > 0)
				{
					ImGui::Spacing();
					ImGui::Separator();
					ImGui::Spacing();
					ImGui::PushID(static_cast<int>(i));
				}
				tab.sections[i].draw();
				if (i > 0)
					ImGui::PopID();
			}
		};

		// The row of groups in amber, so it reads apart from the row of tabs under it
		auto groupColours = [](bool push)
		{
			const ImGuiCol cols[] = { ImGuiCol_Tab, ImGuiCol_TabHovered, ImGuiCol_TabSelected, ImGuiCol_TabSelectedOverline, ImGuiCol_TabDimmed, ImGuiCol_TabDimmedSelected, ImGuiCol_TabDimmedSelectedOverline };
			const ImU32 values[] = { IM_COL32(74, 60, 36, 230), IM_COL32(168, 122, 52, 255), IM_COL32(150, 106, 40, 255), IM_COL32(230, 170, 70, 255), IM_COL32(60, 50, 32, 230), IM_COL32(120, 88, 38, 255), IM_COL32(120, 88, 38, 255) };
			if (!push)
			{
				ImGui::PopStyleColor(static_cast<int>(sizeof(cols) / sizeof(cols[0])));
				return;
			}
			for (int i = 0; i < static_cast<int>(sizeof(cols) / sizeof(cols[0])); i++)
				ImGui::PushStyleColor(cols[i], values[i]);
		};

		groupColours(true);
		const bool groupsShown = !Groups().empty() && ImGui::BeginTabBar("groups");
		groupColours(false);
		if (Groups().empty())
			ImGui::TextUnformatted("Nothing to show.");
		else if (groupsShown)
		{
			for (const Group& group : Groups())
			{
				groupColours(true);
				const bool groupShown = ImGui::BeginTabItem(group.name.c_str(), nullptr, &group == wantedGroup ? ImGuiTabItemFlags_SetSelected : 0);
				groupColours(false);
				if (!groupShown)
					continue;
				// A group of one tab shows it without a row of its own
				if (group.tabs.size() == 1)
					drawTab(group, group.tabs.front());
				else if (ImGui::BeginTabBar(("tabs##" + group.name).c_str()))
				{
					for (const Tab& tab : group.tabs)
						if (ImGui::BeginTabItem(tab.name.c_str(), nullptr, &tab == wantedTabItem ? ImGuiTabItemFlags_SetSelected : 0))
						{
							drawTab(group, tab);
							ImGui::EndTabItem();
						}
					ImGui::EndTabBar();
				}
				ImGui::EndTabItem();
			}
			ImGui::EndTabBar();
		}
	}
	ImGui::End();
	ImGui::PopFont();
	if (!open)
		Open(false);
}

bool DEBUG_MENU::IsOpen()
{
	return g_open;
}

bool DEBUG_MENU::TypingInWindow()
{
	return g_typing;
}

bool DEBUG_MENU::MouseOverWindow()
{
	return g_mouseOverWindow;
}

float DEBUG_MENU::Scale()
{
	return ImGui::GetIO().DisplaySize.y >= 1000.0f ? 1.3f : 1.1f;
}

void DEBUG_MENU::ResetBanners(float top)
{
	g_bannerTop = top;
}

float DEBUG_MENU::BannerTop(float height)
{
	const float top = g_bannerTop;
	g_bannerTop += height + 6.0f;
	return top;
}

bool DEBUG_MENU::Project(const float world[3], ImVec2& screen)
{
	if (!g_haveCamera)
		return false;
	DEBUG_MARKER::Vector4 point = { world[0], world[1], world[2], 1.0f };
	DEBUG_MARKER::Vector4 clip;
	DEBUG_MARKER::matrix_transform(&g_viewProjection, &clip, &point);
	if (clip.w <= 0.0f || clip.z < 0.0f)
		return false;
	screen.x = (clip.x / clip.w + 1.0f) * 0.5f * g_display.x;
	screen.y = (1.0f - (clip.y / clip.w + 1.0f) * 0.5f) * g_display.y;
	return std::isfinite(screen.x) && std::isfinite(screen.y);
}

bool DEBUG_MENU::CameraPosition(float out[3])
{
	if (!g_haveCamera)
		return false;
	memcpy(out, g_cameraPosition, sizeof(g_cameraPosition));
	return true;
}

bool DEBUG_MENU::WindowRect(ImVec2& min, ImVec2& max)
{
	if (!g_windowShown)
		return false;
	min = g_windowMin;
	max = g_windowMax;
	return true;
}

bool DEBUG_MENU::CameraForward(float out[3])
{
	if (!g_haveCamera)
		return false;
	memcpy(out, g_cameraForward, sizeof(g_cameraForward));
	return true;
}

void DEBUG_MENU::WorldLine(ImDrawList* drawList, const float a[3], const float b[3], ImU32 colour, float thickness)
{
	if (!g_haveCamera)
		return;
	// Both ends in clip space; a line crossing the near plane is cut where it crosses, keeping the part in front
	DEBUG_MARKER::Vector4 pa = { a[0], a[1], a[2], 1.0f }, pb = { b[0], b[1], b[2], 1.0f };
	DEBUG_MARKER::Vector4 ca, cb;
	DEBUG_MARKER::matrix_transform(&g_viewProjection, &ca, &pa);
	DEBUG_MARKER::matrix_transform(&g_viewProjection, &cb, &pb);
	const bool aIn = ca.z >= 0.0f && ca.w > 0.0f, bIn = cb.z >= 0.0f && cb.w > 0.0f;
	if (!aIn && !bIn)
		return;
	if (!aIn || !bIn)
	{
		DEBUG_MARKER::Vector4& out = aIn ? cb : ca;
		const DEBUG_MARKER::Vector4& in = aIn ? ca : cb;
		const float t = in.z / (in.z - out.z);
		out.x = in.x + (out.x - in.x) * t;
		out.y = in.y + (out.y - in.y) * t;
		out.z = 0.0f;
		out.w = in.w + (out.w - in.w) * t;
		if (out.w <= 1e-5f)
			return;
	}
	const ImVec2 sa((ca.x / ca.w + 1.0f) * 0.5f * g_display.x, (1.0f - (ca.y / ca.w + 1.0f) * 0.5f) * g_display.y);
	const ImVec2 sb((cb.x / cb.w + 1.0f) * 0.5f * g_display.x, (1.0f - (cb.y / cb.w + 1.0f) * 0.5f) * g_display.y);
	if (std::isfinite(sa.x) && std::isfinite(sa.y) && std::isfinite(sb.x) && std::isfinite(sb.y))
		drawList->AddLine(sa, sb, colour, thickness);
}

void DEBUG_MENU::WorldCircle(ImDrawList* drawList, const float centre[3], float radius, ImU32 colour, int segments, float thickness)
{
	const float step = 6.2831853f / static_cast<float>(segments);
	float previous[3] = { centre[0] + radius, centre[1], centre[2] };
	for (int i = 1; i <= segments; i++)
	{
		const float next[3] = { centre[0] + radius * std::cos(step * i), centre[1], centre[2] + radius * std::sin(step * i) };
		WorldLine(drawList, previous, next, colour, thickness);
		memcpy(previous, next, sizeof(previous));
	}
}

void DEBUG_MENU::WorldText(ImDrawList* drawList, const float world[3], const char* text, ImU32 colour, float yOffsetPixels)
{
	ImVec2 at;
	if (!Project(world, at))
		return;
	// Only for a point on screen, and kept inside its edges (a label half off the screen can't be read)
	if (at.x < 0.0f || at.y < 0.0f || at.x > g_display.x || at.y > g_display.y)
		return;
	const float size = 15.0f * Scale();
	const ImVec2 extent = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, text);
	ImVec2 corner(at.x - extent.x * 0.5f, at.y - extent.y + yOffsetPixels);
	corner.x = (std::max)(4.0f, (std::min)(corner.x, g_display.x - extent.x - 4.0f));
	corner.y = (std::max)(2.0f, (std::min)(corner.y, g_display.y - extent.y - 2.0f));
	drawList->AddRectFilled(ImVec2(corner.x - 4.0f, corner.y - 2.0f), ImVec2(corner.x + extent.x + 4.0f, corner.y + extent.y + 2.0f), IM_COL32(0, 0, 0, 150), 3.0f);
	drawList->AddText(ImGui::GetFont(), size, corner, colour, text);
}

void DEBUG_MENU::WorldPolygon(ImDrawList* drawList, const float (*points)[3], int count, ImU32 fill, ImU32 outline, float thickness)
{
	if (!g_haveCamera || count < 3)
		return;
	// Every point in clip space, the polygon cut where it crosses the near plane (keeping the part in front, still convex)
	std::vector<DEBUG_MARKER::Vector4> clip, cut;
	clip.reserve(count);
	for (int i = 0; i < count; i++)
	{
		DEBUG_MARKER::Vector4 point = { points[i][0], points[i][1], points[i][2], 1.0f }, out;
		DEBUG_MARKER::matrix_transform(&g_viewProjection, &out, &point);
		clip.push_back(out);
	}
	for (size_t i = 0; i < clip.size(); i++)
	{
		const DEBUG_MARKER::Vector4& a = clip[i];
		const DEBUG_MARKER::Vector4& b = clip[(i + 1) % clip.size()];
		const bool aIn = a.z >= 0.0f && a.w > 0.0f, bIn = b.z >= 0.0f && b.w > 0.0f;
		if (aIn)
			cut.push_back(a);
		if (aIn != bIn)
		{
			const float t = a.z / (a.z - b.z);
			const DEBUG_MARKER::Vector4 crossing = { a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, 0.0f, a.w + (b.w - a.w) * t };
			if (crossing.w > 1e-5f)
				cut.push_back(crossing);
		}
	}
	if (cut.size() < 3)
		return;
	std::vector<ImVec2> screen;
	screen.reserve(cut.size());
	for (const DEBUG_MARKER::Vector4& c : cut)
	{
		const ImVec2 at((c.x / c.w + 1.0f) * 0.5f * g_display.x, (1.0f - (c.y / c.w + 1.0f) * 0.5f) * g_display.y);
		if (!std::isfinite(at.x) || !std::isfinite(at.y))
			return;
		screen.push_back(at);
	}
	if (fill)
		drawList->AddConvexPolyFilled(screen.data(), static_cast<int>(screen.size()), fill);
	if (outline)
		drawList->AddPolyline(screen.data(), static_cast<int>(screen.size()), outline, thickness, ImDrawFlags_Closed);
}

namespace
{
	// A box's eight corners (bit 0 picks x, bit 1 y, bit 2 z) joined by its twelve edges
	void BoxEdges(ImDrawList* drawList, const float corners[8][3], ImU32 colour, float thickness)
	{
		for (int i = 0; i < 8; i++)
			for (int bit = 1; bit < 8; bit <<= 1)
				if (!(i & bit))
					DEBUG_MENU::WorldLine(drawList, corners[i], corners[i | bit], colour, thickness);
	}
}

void DEBUG_MENU::WorldBox(ImDrawList* drawList, const float min[3], const float max[3], ImU32 colour, float thickness)
{
	float corners[8][3];
	for (int i = 0; i < 8; i++)
		for (int axis = 0; axis < 3; axis++)
			corners[i][axis] = (i & (1 << axis)) ? max[axis] : min[axis];
	BoxEdges(drawList, corners, colour, thickness);
}

void DEBUG_MENU::WorldOrientedBox(ImDrawList* drawList, const float matrix[16], const float halfSize[3], ImU32 colour, float thickness)
{
	float corners[8][3];
	for (int i = 0; i < 8; i++)
		for (int axis = 0; axis < 3; axis++)
		{
			float value = matrix[12 + axis];
			for (int row = 0; row < 3; row++)
				value += ((i & (1 << row)) ? halfSize[row] : -halfSize[row]) * matrix[row * 4 + axis];
			corners[i][axis] = value;
		}
	BoxEdges(drawList, corners, colour, thickness);
}

void DEBUG_MENU::WorldArrow(ImDrawList* drawList, const float from[3], const float to[3], ImU32 colour, float thickness)
{
	WorldLine(drawList, from, to, colour, thickness);
	float direction[3] = { to[0] - from[0], to[1] - from[1], to[2] - from[2] };
	const float length = std::sqrt(direction[0] * direction[0] + direction[1] * direction[1] + direction[2] * direction[2]);
	if (length < 1e-4f || !g_haveCamera)
		return;
	for (float& value : direction)
		value /= length;
	// The head: two strokes back from the tip, across the line as the camera sees it
	const float toCamera[3] = { g_cameraPosition[0] - to[0], g_cameraPosition[1] - to[1], g_cameraPosition[2] - to[2] };
	float side[3] = { direction[1] * toCamera[2] - direction[2] * toCamera[1], direction[2] * toCamera[0] - direction[0] * toCamera[2], direction[0] * toCamera[1] - direction[1] * toCamera[0] };
	float sideLength = std::sqrt(side[0] * side[0] + side[1] * side[1] + side[2] * side[2]);
	if (sideLength < 1e-5f)
	{
		// Looking straight along it: any way across
		side[0] = -direction[2];
		side[1] = 0.0f;
		side[2] = direction[0];
		sideLength = std::sqrt(side[0] * side[0] + side[2] * side[2]);
		if (sideLength < 1e-5f)
		{
			side[0] = 1.0f;
			sideLength = 1.0f;
		}
	}
	const float head = (std::min)(length * 0.25f, 0.4f);
	for (int sign = -1; sign <= 1; sign += 2)
	{
		float back[3];
		for (int i = 0; i < 3; i++)
			back[i] = to[i] - direction[i] * head + side[i] / sideLength * head * 0.5f * static_cast<float>(sign);
		WorldLine(drawList, to, back, colour, thickness);
	}
}

void DEBUG_MENU::WorldCross(ImDrawList* drawList, const float at[3], float size, ImU32 colour, float thickness)
{
	for (int axis = 0; axis < 3; axis++)
	{
		float a[3] = { at[0], at[1], at[2] }, b[3] = { at[0], at[1], at[2] };
		a[axis] -= size;
		b[axis] += size;
		WorldLine(drawList, a, b, colour, thickness);
	}
}

const std::vector<DEBUG_MENU::Character>& DEBUG_MENU::Characters()
{
	return g_characters;
}

const char* DEBUG_MENU::ClassName(int characterClass)
{
	switch (characterClass)
	{
	case 0: return "Player";
	case 1: return "Alien";
	case 2: return "Android";
	case 3: return "Human";
	case 4: return "Security";
	case 5: return "Facehugger";
	case 6: return "Innocent";
	case 7: return "Heavy android";
	case 8: return "Tracker";
	case 9: return "Human (melee)";
	default: return "?";
	}
}

uint32_t DEBUG_MENU::SelectedId()
{
	return g_selected;
}

void DEBUG_MENU::Select(uint32_t id)
{
	g_selected = id;
}

const DEBUG_MENU::Character* DEBUG_MENU::Selected()
{
	for (const Character& character : g_characters)
		if (character.id == g_selected)
			return &character;
	return nullptr;
}

bool DEBUG_MENU::CharacterPicker(const char* label)
{
	if (g_characters.empty())
	{
		ImGui::TextDisabled("No characters (no level running)");
		return false;
	}
	const Character* selected = Selected();
	char preview[96];
	if (selected)
		snprintf(preview, sizeof(preview), "%s (%s)", selected->name.c_str(), ClassName(selected->characterClass));
	else
		snprintf(preview, sizeof(preview), "(none)");
	if (ImGui::BeginCombo(label, preview))
	{
		for (const Character& character : g_characters)
		{
			char text[96];
			snprintf(text, sizeof(text), "%s (%s)%s##%u", character.name.c_str(), ClassName(character.characterClass), character.dead ? " - dead" : "", character.id);
			if (ImGui::Selectable(text, character.id == g_selected))
				g_selected = character.id;
		}
		ImGui::EndCombo();
	}
	return true;
}

void DEBUG_MENU::OnEntityThread(Work work)
{
	Enqueue(g_entityWork, std::move(work));
}

void DEBUG_MENU::OnAiThread(Work work)
{
	Enqueue(g_aiWork, std::move(work));
}

void DEBUG_MENU::RunEntityThreadWork()
{
	RunQueued(g_entityWork);
}

void DEBUG_MENU::RunAiThreadWork()
{
	RunQueued(g_aiWork);
}

void DEBUG_MENU::AddSwitch(const std::string& name, std::atomic<bool>* value)
{
	std::lock_guard<std::mutex> lock(g_outsideMutex);
	for (auto& existing : Switches())
		if (existing.first == name)
		{
			existing.second = value;
			return;
		}
	Switches().emplace_back(name, value);
}

bool DEBUG_MENU::SetSwitch(const std::string& name, bool value)
{
	std::lock_guard<std::mutex> lock(g_outsideMutex);
	for (auto& existing : Switches())
		if (existing.first == name)
		{
			*existing.second = value;
			return true;
		}
	return false;
}

bool DEBUG_MENU::GetSwitch(const std::string& name, bool& value)
{
	std::lock_guard<std::mutex> lock(g_outsideMutex);
	for (const auto& existing : Switches())
		if (existing.first == name)
		{
			value = existing.second->load();
			return true;
		}
	return false;
}

bool DEBUG_MENU::GetValue(const std::string& name, float& value)
{
	std::lock_guard<std::mutex> lock(g_outsideMutex);
	for (const NamedValue& existing : Values())
		if (existing.name == name)
		{
			value = existing.value->load();
			return true;
		}
	return false;
}

void DEBUG_MENU::AddValue(const std::string& name, std::atomic<float>* value, float min, float max)
{
	std::lock_guard<std::mutex> lock(g_outsideMutex);
	for (NamedValue& existing : Values())
		if (existing.name == name)
		{
			existing = { name, value, min, max };
			return;
		}
	Values().push_back({ name, value, min, max });
}

bool DEBUG_MENU::SetValue(const std::string& name, float value)
{
	if (!std::isfinite(value))
		return false;
	std::lock_guard<std::mutex> lock(g_outsideMutex);
	for (NamedValue& existing : Values())
		if (existing.name == name)
		{
			*existing.value = (std::max)(existing.min, (std::min)(existing.max, value));
			return true;
		}
	return false;
}

void DEBUG_MENU::AddCommand(const std::string& name, const char* help, Command command)
{
	std::lock_guard<std::mutex> lock(g_outsideMutex);
	for (NamedCommand& existing : Commands())
		if (existing.name == name)
		{
			existing.help = help ? help : "";
			existing.command = std::move(command);
			return;
		}
	Commands().push_back({ name, help ? help : "", std::move(command) });
}

bool DEBUG_MENU::RunCommand(const std::string& name, const std::string& argument, CommandResult& result)
{
	Command command;
	{
		std::lock_guard<std::mutex> lock(g_outsideMutex);
		if (name == "help")
		{
			result = CommandResult();
			for (const NamedCommand& existing : Commands())
				result.message += existing.name + (existing.help.empty() ? "" : " - " + existing.help) + "\n";
			return true;
		}
		for (const NamedCommand& existing : Commands())
			if (existing.name == name)
				command = existing.command;
	}
	// Run outside the lock: a command may set switches or describe the window itself
	if (!command)
		return false;
	result = command(argument);
	return true;
}

DEBUG_MENU::CommandResult DEBUG_MENU::AnswerOnEntityThread(Answer work, int timeoutMs)
{
	return WaitForAnswer(g_entityWork, std::move(work), timeoutMs);
}

DEBUG_MENU::CommandResult DEBUG_MENU::AnswerOnAiThread(Answer work, int timeoutMs)
{
	return WaitForAnswer(g_aiWork, std::move(work), timeoutMs);
}

DEBUG_MENU::CommandResult DEBUG_MENU::AnswerOnRenderThread(Answer work, int timeoutMs)
{
	return WaitForAnswer(g_renderWork, std::move(work), timeoutMs);
}

void DEBUG_MENU::OnRenderThread(Work work)
{
	Enqueue(g_renderWork, std::move(work));
}

void DEBUG_MENU::SetPickProvider(PickProvider provider)
{
	g_pickProvider = provider;
}

bool DEBUG_MENU::Pick(PickResult& out)
{
	out = PickResult();
	const PickProvider provider = g_pickProvider;
	return provider && provider(out) && out.hit;
}

bool DEBUG_MENU::AddHooks(HookAttacher attacher)
{
	// Static initializers only, before the DLL attaches its hooks
	Hooks().push_back(attacher);
	return true;
}

void DEBUG_MENU::AttachHooks(bool attach)
{
	// Once each way, in step (Detours refuses to detach what was never attached), the last attached detached first
	if (attach == g_hooksAttached)
		return;
	g_hooksAttached = attach;
	std::vector<HookAttacher>& hooks = Hooks();
	if (attach)
		for (HookAttacher attacher : hooks)
			attacher(true);
	else
		for (auto it = hooks.rbegin(); it != hooks.rend(); ++it)
			(*it)(false);
}

void DEBUG_MENU::Show(bool open)
{
	Open(open);
}

void DEBUG_MENU::ShowTab(const std::string& name)
{
	std::lock_guard<std::mutex> lock(g_outsideMutex);
	g_wantedTab = name;
}

void DEBUG_MENU::SelectByName(const std::string& name)
{
	std::lock_guard<std::mutex> lock(g_outsideMutex);
	g_wantedCharacter = name;
	g_characterWanted = true;
}

std::string DEBUG_MENU::Describe()
{
	std::lock_guard<std::mutex> lock(g_outsideMutex);
	std::string text = std::string("open=") + (g_open ? "1" : "0") + "\ngroup=" + g_currentGroup + "\ntab=" + g_currentTab + "\nselected=" + g_selectedText;
	std::string tabs, groups, commands, values;
	for (const Group& group : Groups())
	{
		groups += (groups.empty() ? "" : ",") + group.name;
		for (const Tab& tab : group.tabs)
			tabs += (tabs.empty() ? "" : ",") + tab.name;
	}
	for (const NamedCommand& command : Commands())
		commands += (commands.empty() ? "" : ",") + command.name;
	for (const NamedValue& value : Values())
	{
		char number[32];
		snprintf(number, sizeof(number), "%g", value.value->load());
		values += (values.empty() ? "" : ",") + value.name + ":" + number;
	}
	text += "\ntabs=" + tabs + "\ngroups=" + groups + "\ncommands=" + commands + "\nvalues=" + values;
	for (const auto& entry : Switches())
		text += "\n" + entry.first + "=" + (entry.second->load() ? "1" : "0");
	return text;
}
