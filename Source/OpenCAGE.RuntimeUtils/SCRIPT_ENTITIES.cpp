#include "SCRIPT_DEBUG.h"
#include "DEBUG_MENU.h"
#include "DEBUG_TEXT.h"
#include "DEBUG_MARKER.h"
#include "Config.h"
#include "Menu.h"
#include "SCREENSHOT.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

#include <imgui.h>

/*
	Debug script entities (debug menu: Scripts > Entities): the debugging entities that the shipped game still
	creates but whose methods do nothing - made to work again, the way DebugText is, each behind its own key in
	OpenCAGE.RuntimeUtils.ini (DebugGraph, DebugMenuToggle, DebugObjectMarker, DebugCaptureScreenShot: off unless it says 1,
	so a level that still holds one of them plays as the shipped game does; Launch Game's Debug entities writes all four).
	They run with or without the debug menu: the DLL attaches their hooks whenever one is on, and with the debug menu off
	draws their overlay itself (DEBUG_MENU::KeepUpWithoutWindow keeps the camera and the render thread's work going for it).
	The tab only shows what they are doing.

	- DebugGraph: plots the numbers linked to its Inputs over time while it is started (start / stop), in a panel over
	  the game - its duration (seconds kept), samples_per_second, scale (the top of the range; the bottom is 0, or minus it
	  when a value goes below 0) and auto_scale (fit the range to the values). Up to eight inputs: each entity linked to
	  Inputs gives its own value. Its start, stop and update entries of its class's
	  own table of methods are pointed here (the game shares the "does nothing" function they held with many others).
	- DebugMenuToggle: when triggered, sets the debug menu switch its debug_variable names (one of this menu's switch
	  names, e.g. characters.view_cones) to its value - or flips it when it has no value. An unknown name is noted in
	  Scripts > Errors. Its trigger entry is pointed here.
	- DebugObjectMarker: shows its marked_name over its marked_object, over the game, while its switch is on (the
	  markers are found through the level's composites, four times a second; nothing of the game's is changed).
	- DebugCaptureScreenShot: when triggered, saves the next frame (the game's picture, without this menu) as a .bmp named
	  after its capture_filename, in a "DebugCaptureScreenShot" folder next to AI.exe. Its field of view, clip planes and
	  wait for streaming are not followed. Its trigger entry is pointed here.

	The other debugging entities are not here: DebugText, DebugTextStacking and the two markers are RuntimeUtils' own
	already; the debug checkpoints are in Game > Tools; the corpse capture's exporter is gone from the game.

	Addresses are from the Steam retail AI.exe.
*/
namespace
{
	// ---- The game's ----
	// The classes' own tables of methods (their interfaces'), and the entries: a method called on the entity calls these
	constexpr uintptr_t kGraphMethods = 0x00E5B710;
	constexpr uintptr_t kToggleMethods = 0x00E58638;
	constexpr uintptr_t kScreenshotMethods = 0x00E77C28;
	constexpr uint32_t kOnUpdate = 0x200;
	constexpr uint32_t kOnStart = 0x28C;
	constexpr uint32_t kOnStop = 0x290;
	constexpr uint32_t kOnTrigger = 0x284;
	// The shared functions those entries hold in the shipped game: "do nothing, say no" and "do nothing, say yes"
	constexpr uintptr_t kAnswerNo = 0x004D21E0;
	constexpr uintptr_t kAnswerYes = 0x00004030;
	typedef bool(__thiscall* t_method)(void* methods, const void* entity, const void* trigger);

	// Starting and stopping an entity's updates every frame (on its state, at its interface minus 0x10)
	constexpr uintptr_t kStartUpdating = 0x00539DC0;
	constexpr uintptr_t kFinishUpdating = 0x00539EE0;
	typedef bool(__thiscall* t_updating)(void* state, const void* entity, const void* trigger, bool startOrStopToo);
	constexpr int kStateFromInterface = -0x10;

	// An entity's own value, as a number (an entity linked to a graph's Inputs gives this)
	constexpr uintptr_t kEntityAsNumber = 0x0001DB60;
	typedef float(__cdecl* t_entity_as_number)(const void* entity, float fallback);

	// A marker's marked object (a counted reference, released after)
	constexpr uintptr_t kMarkedObject = 0x004FB280;
	typedef void** (__thiscall* t_marked_object)(void* methods, void** out, const void* entity);
	constexpr uintptr_t kReleaseEntity = 0x00198230;
	typedef void(__thiscall* t_release_entity)(void** reference);
	constexpr uint32_t kVGetInterface = 0x70;      // an entity's interface (its methods' table's object)
	constexpr uint32_t kRecordObject = 0x0C;

	// The parameters' ids (each the id of its name)
	constexpr uint32_t kInputs = 0x9EA527C0, kScale = 0x049A0005, kDuration = 0x96538F4D, kSamplesPerSecond = 0x5C4E572D, kAutoScale = 0xEAAB388C;
	constexpr uint32_t kDebugVariable = 0x92AD105F, kValue = 0x1C3CA08D;
	constexpr uint32_t kMarkedName = 0xDE90B782;
	constexpr uint32_t kCaptureFilename = 0x0760E643;

	constexpr int kMaxInputs = 8;
	constexpr int kMaxSamples = 2048;
	constexpr uint32_t kInvalidString = 0xFFFFFFFF;

	// ---- Switches (defaults from the ini) ----
	std::atomic<bool> g_graphOn = false, g_toggleOn = false, g_markerOn = false, g_screenshotOn = false;

	// ---- Shared ----
	// A string parameter's text as UTF-8 (the game keeps it as Windows-1252): drawn, and the screenshot name, which
	// SCREENSHOT reads as UTF-8
	std::string ResolveString(uint32_t offset)
	{
		if (offset == kInvalidString)
			return std::string();
		void* table = DEBUG_TEXT::string_table_instance ? *DEBUG_TEXT::string_table_instance : nullptr;
		return DevTools::GameTextToUtf8(table ? DEBUG_TEXT::string_from_offset(table, offset) : nullptr);
	}

	bool ReadStringOffset(void* methods, const void* entity, uint32_t guid, uint32_t& offset)
	{
		__try
		{
			return DEBUG_TEXT::find_parameter_string(methods, entity, &guid, &offset);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadString(void* methods, const void* entity, uint32_t guid, std::string& out)
	{
		uint32_t offset = kInvalidString;
		if (!ReadStringOffset(methods, entity, guid, offset))
			return false;
		out = ResolveString(offset);
		return true;
	}

	bool ReadFloat(void* methods, const void* entity, uint32_t guid, float& out)
	{
		__try
		{
			return DEBUG_TEXT::find_parameter_float(methods, entity, &guid, &out);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadBool(void* methods, const void* entity, uint32_t guid, bool& out)
	{
		__try
		{
			return DEBUG_TEXT::find_parameter_bool(methods, entity, &guid, &out);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- DebugGraph ----
	struct Graph
	{
		const void* methods = nullptr;  // tells graphs apart (never followed)
		std::string title;
		ULONGLONG seenAt = 0;           // the last update (a graph not updated for a second is gone)
		double lastSample = -1.0;
		float scale = 1.0f, duration = 10.0f, rate = 10.0f;
		bool autoScale = true;
		int inputs = 0;
		std::vector<float> samples[kMaxInputs];   // each input's, oldest first
	};

	std::mutex g_graphMutex;
	std::vector<Graph> g_graphs;

	// The values of the entities linked to Inputs (up to max): the game's parameter cache for it is read under its lock
	int ReadInputValues(void* methods, const void* entity, float* out, int max)
	{
		const t_entity_as_number asNumber = reinterpret_cast<t_entity_as_number>(DEVTOOLS_RELATIVE_ADDRESS(kEntityAsNumber));
		if (!asNumber)
			return 0;
		// Reading the parameter builds its cache entry when there is none yet
		float first = 0.0f;
		ReadFloat(methods, entity, kInputs, first);
		const void* linked[kMaxInputs] = {};
		int count = 0;
		alignas(16) unsigned char lock[16] = {};
		__try
		{
			DEBUG_TEXT::cache_lock_ctor(lock, entity);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
		__try
		{
			const uint32_t guid = kInputs;
			const uint8_t* entry = static_cast<const uint8_t*>(DEBUG_TEXT::find_entry(methods, entity, &guid));
			const uint8_t* link = entry ? *reinterpret_cast<const uint8_t* const*>(entry + 8) : nullptr;
			while (link && count < max && count < kMaxInputs)
			{
				linked[count++] = *reinterpret_cast<const void* const*>(link + 8);
				link = *reinterpret_cast<const uint8_t* const*>(link + 0x0C);
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
		__try
		{
			DEBUG_TEXT::cache_lock_dtor(lock);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
		int values = 0;
		for (int i = 0; i < count; i++)
		{
			const void* record = linked[i];
			if (!record)
				continue;
			__try
			{
				out[values++] = asNumber(&record, 0.0f);
			}
			__except (EXCEPTION_EXECUTE_HANDLER)
			{
			}
		}
		// A plain number set on Inputs itself, with nothing linked
		if (values == 0 && count == 0 && first != 0.0f)
			out[values++] = first;
		return values;
	}

	t_method g_graphStartWas = nullptr, g_graphStopWas = nullptr, g_graphUpdateWas = nullptr;
	t_method g_toggleWas = nullptr, g_screenshotWas = nullptr;

	bool Updating(uintptr_t steamFunction, void* methods, const void* entity, const void* trigger)
	{
		const t_updating call = reinterpret_cast<t_updating>(DEVTOOLS_RELATIVE_ADDRESS(steamFunction));
		if (!call)
			return false;
		__try
		{
			return call(static_cast<uint8_t*>(methods) + kStateFromInterface, entity, trigger, true);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool __fastcall OnGraphStart(void* methods, void* /*_EDX*/, const void* entity, const void* trigger)
	{
		if (!g_graphOn)
			return g_graphStartWas(methods, entity, trigger);
		SCRIPT_DEBUG::EntityId id;
		SCRIPT_DEBUG::ReadEntityAt(entity, id);
		{
			std::lock_guard<std::mutex> lock(g_graphMutex);
			auto it = std::find_if(g_graphs.begin(), g_graphs.end(), [&](const Graph& graph) { return graph.methods == methods; });
			if (it == g_graphs.end())
			{
				g_graphs.emplace_back();
				it = g_graphs.end() - 1;
			}
			*it = Graph();
			it->methods = methods;
			it->title = "DebugGraph " + SCRIPT_DEBUG::Guid(id.entity) + (id.compositeName[0] ? " in " + SCRIPT_DEBUG::CompositeText(id.composite, id.compositeName) : std::string());
			it->seenAt = GetTickCount64();
		}
		Updating(kStartUpdating, methods, entity, trigger);
		return true;
	}

	bool __fastcall OnGraphStop(void* methods, void* /*_EDX*/, const void* entity, const void* trigger)
	{
		if (!g_graphOn)
			return g_graphStopWas(methods, entity, trigger);
		{
			std::lock_guard<std::mutex> lock(g_graphMutex);
			g_graphs.erase(std::remove_if(g_graphs.begin(), g_graphs.end(), [&](const Graph& graph) { return graph.methods == methods; }), g_graphs.end());
		}
		Updating(kFinishUpdating, methods, entity, trigger);
		return true;
	}

	bool __fastcall OnGraphUpdate(void* methods, void* /*_EDX*/, const void* entity, const void* trigger)
	{
		if (!g_graphOn)
			return g_graphUpdateWas(methods, entity, trigger);
		float scale = 1.0f, duration = 10.0f, rate = 10.0f;
		bool autoScale = true;
		ReadFloat(methods, entity, kScale, scale);
		ReadFloat(methods, entity, kDuration, duration);
		ReadFloat(methods, entity, kSamplesPerSecond, rate);
		ReadBool(methods, entity, kAutoScale, autoScale);
		duration = std::isfinite(duration) && duration > 0.1f ? duration : 10.0f;
		rate = std::isfinite(rate) && rate > 0.1f ? (std::min)(rate, 120.0f) : 10.0f;
		const double now = SCRIPT_DEBUG::ScriptTime();

		// Whether a sample is due (the game's own reads are made outside the lock)
		bool sample = false;
		{
			std::lock_guard<std::mutex> lock(g_graphMutex);
			auto it = std::find_if(g_graphs.begin(), g_graphs.end(), [&](const Graph& graph) { return graph.methods == methods; });
			if (it == g_graphs.end())
			{
				// Started before the switch was on: shown from now
				SCRIPT_DEBUG::EntityId id;
				SCRIPT_DEBUG::ReadEntityAt(entity, id);
				g_graphs.emplace_back();
				it = g_graphs.end() - 1;
				it->methods = methods;
				it->title = "DebugGraph " + SCRIPT_DEBUG::Guid(id.entity) + (id.compositeName[0] ? " in " + SCRIPT_DEBUG::CompositeText(id.composite, id.compositeName) : std::string());
			}
			Graph& graph = *it;
			graph.seenAt = GetTickCount64();
			graph.scale = std::isfinite(scale) && scale != 0.0f ? scale : 1.0f;
			graph.duration = duration;
			graph.rate = rate;
			graph.autoScale = autoScale;
			sample = graph.lastSample < 0.0 || now < graph.lastSample || now - graph.lastSample >= 1.0 / rate;
			if (sample)
				graph.lastSample = now;
		}
		if (!sample)
			return true;

		float values[kMaxInputs];
		const int count = ReadInputValues(methods, entity, values, kMaxInputs);
		std::lock_guard<std::mutex> lock(g_graphMutex);
		auto it = std::find_if(g_graphs.begin(), g_graphs.end(), [&](const Graph& graph) { return graph.methods == methods; });
		if (it == g_graphs.end())
			return true;
		Graph& graph = *it;
		graph.inputs = (std::max)(graph.inputs, count);
		const size_t keep = static_cast<size_t>((std::min)(static_cast<float>(kMaxSamples), duration * rate));
		for (int i = 0; i < graph.inputs; i++)
		{
			std::vector<float>& samples = graph.samples[i];
			samples.push_back(i < count ? values[i] : (samples.empty() ? 0.0f : samples.back()));
			if (samples.size() > keep)
				samples.erase(samples.begin(), samples.begin() + (samples.size() - keep));
		}
		return true;
	}

	// ---- DebugMenuToggle ----
	bool __fastcall OnToggleTrigger(void* methods, void* /*_EDX*/, const void* entity, const void* trigger)
	{
		if (!g_toggleOn)
			return g_toggleWas(methods, entity, trigger);
		std::string name;
		ReadString(methods, entity, kDebugVariable, name);
		bool value = false;
		const bool hasValue = ReadBool(methods, entity, kValue, value);
		bool now = false;
		if (name.empty() || !DEBUG_MENU::GetSwitch(name, now))
		{
			SCRIPT_DEBUG::NoteScriptError(entity, ("DebugMenuToggle: no debug menu switch named \"" + name + "\" (it takes one of the debug menu's switch names, e.g. characters.view_cones)").c_str());
			return true;
		}
		const bool set = hasValue ? value : !now;
		DEBUG_MENU::SetSwitch(name, set);
		DevTools::Log("Debug menu: a DebugMenuToggle set %s to %d.", name.c_str(), set ? 1 : 0);
		return true;
	}

	// ---- DebugCaptureScreenShot ----
	std::atomic<int> g_screenshots = 0;
	std::mutex g_screenshotMutex;
	std::string g_lastScreenshot;

	std::string ScreenshotPath(std::string name)
	{
		char exe[MAX_PATH] = {};
		GetModuleFileNameA(nullptr, exe, MAX_PATH);
		std::string folder = exe;
		const size_t slash = folder.find_last_of("\\/");
		folder = (slash == std::string::npos ? std::string() : folder.substr(0, slash + 1)) + "DebugCaptureScreenShot";
		CreateDirectoryA(folder.c_str(), nullptr);
		for (char& c : name)
			if (c == '\\' || c == '/' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
				c = '_';
		if (name.empty())
		{
			SYSTEMTIME time;
			GetLocalTime(&time);
			char text[64];
			snprintf(text, sizeof(text), "screenshot_%04d%02d%02d_%02d%02d%02d", time.wYear, time.wMonth, time.wDay, time.wHour, time.wMinute, time.wSecond);
			name = text;
		}
		if (name.size() < 4 || _stricmp(name.c_str() + name.size() - 4, ".bmp") != 0)
			name += ".bmp";
		return folder + "\\" + name;
	}

	bool __fastcall OnScreenshotTrigger(void* methods, void* /*_EDX*/, const void* entity, const void* trigger)
	{
		if (!g_screenshotOn)
			return g_screenshotWas(methods, entity, trigger);
		std::string name;
		ReadString(methods, entity, kCaptureFilename, name);
		const std::string path = ScreenshotPath(name);
		// The next frame, at its presentation (before this menu draws on it)
		DEBUG_MENU::OnRenderThread([path]()
		{
			std::string error;
			IDXGISwapChain* swapChain = Menu::GetSwapChain();
			const bool ok = swapChain && SCREENSHOT::Capture(swapChain, path, error);
			DevTools::Log("Debug menu: DebugCaptureScreenShot %s %s%s%s", ok ? "saved" : "could not save", path.c_str(), error.empty() ? "" : ": ", error.c_str());
			if (ok)
			{
				g_screenshots++;
				std::lock_guard<std::mutex> lock(g_screenshotMutex);
				g_lastScreenshot = path;
			}
		});
		return true;
	}

	// ---- DebugObjectMarker ----
	struct Marker
	{
		SCRIPT_DEBUG::EntityId id;
		std::string name;
		bool hasObject = false;
		float position[3] = {};
		SCRIPT_DEBUG::EntityId object;
	};

	void* MethodsOf(const void* record)
	{
		__try
		{
			uint8_t* object = *reinterpret_cast<uint8_t* const*>(static_cast<const uint8_t*>(record) + kRecordObject);
			if (!object)
				return nullptr;
			typedef void* (__thiscall* t_get_interface)(void*);
			return reinterpret_cast<t_get_interface>((*reinterpret_cast<void***>(object))[kVGetInterface / 4])(object);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	void* MarkedObject(void* methods, const void* entity)
	{
		const t_marked_object marked = reinterpret_cast<t_marked_object>(DEVTOOLS_RELATIVE_ADDRESS(kMarkedObject));
		if (!marked)
			return nullptr;
		void* out = nullptr;
		__try
		{
			marked(methods, &out, entity);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
		return out;
	}

	bool ObjectPosition(void* record, float out[3])
	{
		if (!DEBUG_MARKER::entity_position || !DEBUG_MARKER::default_position)
			return false;
		__try
		{
			DEBUG_MARKER::Position position;
			DEBUG_MARKER::entity_position(&position, &record, *DEBUG_MARKER::default_position);
			if (memcmp(&position, DEBUG_MARKER::default_position, sizeof(position)) == 0)
				return false;
			out[0] = position.m[3];
			out[1] = position.m[7];
			out[2] = position.m[11];
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void Release(void* record)
	{
		const t_release_entity release = reinterpret_cast<t_release_entity>(DEVTOOLS_RELATIVE_ADDRESS(kReleaseEntity));
		if (!record || !release)
			return;
		__try
		{
			release(&record);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	// Entity thread: every marker in the level, and where its object is
	std::vector<Marker> ReadMarkers()
	{
		std::vector<Marker> markers;
		const uint32_t types[] = { SCRIPT_DEBUG::kTypeDebugObjectMarker };
		std::vector<SCRIPT_DEBUG::FoundEntity> found;
		if (!SCRIPT_DEBUG::FindEntities(types, 1, found, 512))
			return markers;
		for (const SCRIPT_DEBUG::FoundEntity& entity : found)
		{
			Marker marker;
			if (!SCRIPT_DEBUG::ReadEntity(entity.record, marker.id))
				continue;
			const void* record = entity.record;
			void* methods = MethodsOf(record);
			if (!methods)
				continue;
			ReadString(methods, &record, kMarkedName, marker.name);
			if (void* object = MarkedObject(methods, &record))
			{
				SCRIPT_DEBUG::ReadEntity(object, marker.object);
				marker.hasObject = ObjectPosition(object, marker.position);
				Release(object);
			}
			markers.push_back(marker);
		}
		return markers;
	}

	std::mutex g_markerMutex;
	std::vector<Marker> g_markers;
	std::atomic<bool> g_markersAsked = false;
	ULONGLONG g_markersAskedAt = 0;

	void AskMarkers()
	{
		if (g_markersAsked.exchange(true))
			return;
		DEBUG_MENU::OnEntityThread([]()
		{
			std::vector<Marker> markers = ReadMarkers();
			{
				std::lock_guard<std::mutex> lock(g_markerMutex);
				g_markers.swap(markers);
			}
			g_markersAsked = false;
		});
	}

	std::string MarkerText(const Marker& marker)
	{
		char position[96] = "no_object";
		if (marker.hasObject)
			snprintf(position, sizeof(position), "position=%.2f,%.2f,%.2f", marker.position[0], marker.position[1], marker.position[2]);
		return "name=\"" + marker.name + "\" " + position + " | marker " + SCRIPT_DEBUG::Fields(marker.id) + (marker.object.valid ? " | object " + SCRIPT_DEBUG::Fields(marker.object) : "");
	}

	// ---- Hooks ----
	SCRIPT_DEBUG::PatchedSlot g_slots[5];
	bool g_attached = false;
	std::string g_status = "the debug menu's hooks are not attached";

	void Attach(bool attach)
	{
		if (attach == g_attached)
			return;
		g_attached = attach;
		if (!attach)
		{
			for (SCRIPT_DEBUG::PatchedSlot& slot : g_slots)
				SCRIPT_DEBUG::RestoreSlot(slot);
			return;
		}
		// As the ini says (Launch Game's Debug entities writes all four). A DebugMenuToggle sets the debug menu's switches, so
		// it only does anything with the debug menu on.
		const Config::Settings& config = Config::Get();
		g_graphOn = config.debugGraph;
		g_toggleOn = config.debugMenuToggle && config.debugMenu;
		g_markerOn = config.debugObjectMarker;
		g_screenshotOn = config.debugCaptureScreenShot;
		struct Entry { uintptr_t table; uint32_t offset; uintptr_t was; const void* replacement; t_method* original; const char* what; };
		const Entry entries[] = {
			{ kGraphMethods, kOnStart, kAnswerNo, reinterpret_cast<const void*>(&OnGraphStart), &g_graphStartWas, "DebugGraph start" },
			{ kGraphMethods, kOnStop, kAnswerNo, reinterpret_cast<const void*>(&OnGraphStop), &g_graphStopWas, "DebugGraph stop" },
			{ kGraphMethods, kOnUpdate, kAnswerNo, reinterpret_cast<const void*>(&OnGraphUpdate), &g_graphUpdateWas, "DebugGraph update" },
			{ kToggleMethods, kOnTrigger, kAnswerYes, reinterpret_cast<const void*>(&OnToggleTrigger), &g_toggleWas, "DebugMenuToggle trigger" },
			{ kScreenshotMethods, kOnTrigger, kAnswerNo, reinterpret_cast<const void*>(&OnScreenshotTrigger), &g_screenshotWas, "DebugCaptureScreenShot trigger" },
		};
		g_status.clear();
		int patched = 0;
		for (size_t i = 0; i < sizeof(entries) / sizeof(entries[0]); i++)
		{
			*entries[i].original = reinterpret_cast<t_method>(DEVTOOLS_RELATIVE_ADDRESS(entries[i].was));
			const char* why = nullptr;
			if (*entries[i].original && SCRIPT_DEBUG::PatchSlot(entries[i].table, entries[i].offset, entries[i].was, entries[i].replacement, g_slots[i], why))
				patched++;
			else
				g_status += std::string(g_status.empty() ? "" : "; ") + entries[i].what + ": " + (why ? why : "not known in this build");
		}
		// A graph's three entries go together: one without the others is put back
		if (!g_slots[0].at || !g_slots[1].at || !g_slots[2].at)
			for (int i = 0; i < 3; i++)
				SCRIPT_DEBUG::RestoreSlot(g_slots[i]);
		DevTools::Log("Debug menu: debug script entities - %d of 5 methods followed%s%s.", patched, g_status.empty() ? "" : ": ", g_status.c_str());
	}

	bool GraphAvailable() { return g_slots[0].at && g_slots[1].at && g_slots[2].at; }
	bool ToggleAvailable() { return g_slots[3].at != nullptr; }
	bool ScreenshotAvailable() { return g_slots[4].at != nullptr; }

	// The hooks are attached by the DLL itself (SCRIPT_ENTITIES::AttachHooks), debug menu or not; the four are switched by
	// Launch Game's Debug entities, not from the menu
	bool Register()
	{
		DEBUG_MENU::AddCommand("scripts.list_debug_entities", "the debug script entities: which are followed in this build, the graphs being drawn, the object markers, the screenshots saved",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				DEBUG_MENU::CommandResult result;
				result.message = std::string("graph=") + (GraphAvailable() ? "available" : "not_available") + " toggle=" + (ToggleAvailable() ? "available" : "not_available") +
					" screenshot=" + (ScreenshotAvailable() ? "available" : "not_available") + (g_status.empty() ? "" : " (" + g_status + ")") + "\n";
				{
					std::lock_guard<std::mutex> lock(g_graphMutex);
					for (const Graph& graph : g_graphs)
					{
						result.message += "graph \"" + graph.title + "\" inputs=" + std::to_string(graph.inputs) + " samples=" + std::to_string(graph.samples[0].size());
						for (int i = 0; i < graph.inputs; i++)
							if (!graph.samples[i].empty())
								result.message += " input" + std::to_string(i) + "=" + std::to_string(graph.samples[i].back());
						result.message += "\n";
					}
				}
				const std::vector<Marker> markers = [] { std::lock_guard<std::mutex> lock(g_markerMutex); return g_markers; }();
				for (const Marker& marker : markers)
					result.message += "marker " + MarkerText(marker) + "\n";
				std::lock_guard<std::mutex> lock(g_screenshotMutex);
				result.message += "screenshots=" + std::to_string(g_screenshots.load()) + (g_lastScreenshot.empty() ? "" : " last=" + g_lastScreenshot);
				return result;
			});
		DEBUG_MENU::AddCommand("scripts.list_object_markers", "reads the level's DebugObjectMarker entities now: name, marked object and where it is",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				return DEBUG_MENU::AnswerOnEntityThread([]() -> DEBUG_MENU::CommandResult
				{
					if (!SCRIPT_DEBUG::LevelRunning())
						return { false, "no level running" };
					const std::vector<Marker> markers = ReadMarkers();
					DEBUG_MENU::CommandResult result;
					result.message = "markers=" + std::to_string(markers.size()) + "\n";
					for (const Marker& marker : markers)
						result.message += MarkerText(marker) + "\n";
					return result;
				});
			});
		return true;
	}

	const bool g_registered = Register();

	// ---- Drawing ----
	const ImU32 kInputColours[kMaxInputs] = { IM_COL32(255, 210, 90, 255), IM_COL32(110, 200, 255, 255), IM_COL32(140, 230, 120, 255), IM_COL32(255, 120, 120, 255),
		IM_COL32(220, 140, 255, 255), IM_COL32(255, 255, 255, 255), IM_COL32(255, 160, 60, 255), IM_COL32(120, 255, 220, 255) };

	void DrawGraphs(ImDrawList* drawList)
	{
		std::vector<Graph> graphs;
		{
			std::lock_guard<std::mutex> lock(g_graphMutex);
			const ULONGLONG now = GetTickCount64();
			// A graph not updated for a second is gone (stopped by the game's own logic, or its level unloaded)
			g_graphs.erase(std::remove_if(g_graphs.begin(), g_graphs.end(), [&](const Graph& graph) { return now - graph.seenAt > 1000; }), g_graphs.end());
			graphs = g_graphs;
		}
		if (graphs.empty())
			return;
		const float scale = DEBUG_MENU::Scale();
		const ImVec2 display = ImGui::GetIO().DisplaySize;
		const float width = 320.0f * scale, height = 110.0f * scale, gap = 8.0f * scale;
		float y = 112.0f * scale; // under the Live Link notes in the same corner
		for (const Graph& graph : graphs)
		{
			if (y + height > display.y)
				break;
			const ImVec2 topLeft(display.x - width - 16.0f * scale, y), bottomRight(display.x - 16.0f * scale, y + height);
			drawList->AddRectFilled(topLeft, bottomRight, IM_COL32(0, 0, 0, 160), 4.0f);
			drawList->AddRect(topLeft, bottomRight, IM_COL32(255, 255, 255, 60), 4.0f);
			float low = 0.0f, high = graph.scale;
			bool any = false;
			float dataLow = FLT_MAX, dataHigh = -FLT_MAX;
			for (int i = 0; i < graph.inputs; i++)
				for (float value : graph.samples[i])
				{
					dataLow = (std::min)(dataLow, value);
					dataHigh = (std::max)(dataHigh, value);
					any = true;
				}
			if (graph.autoScale && any)
			{
				low = dataLow;
				high = dataHigh;
				if (high - low < 1e-4f)
				{
					low -= 0.5f;
					high += 0.5f;
				}
			}
			else if (any && dataLow < 0.0f)
			{
				low = -std::fabs(graph.scale);
				high = std::fabs(graph.scale);
			}
			const float plotTop = topLeft.y + 18.0f * scale, plotBottom = bottomRight.y - 4.0f;
			const size_t capacity = static_cast<size_t>((std::min)(static_cast<float>(kMaxSamples), graph.duration * graph.rate));
			for (int i = 0; i < graph.inputs; i++)
			{
				const std::vector<float>& samples = graph.samples[i];
				if (samples.size() < 2)
					continue;
				std::vector<ImVec2> points;
				points.reserve(samples.size());
				const float step = (width - 8.0f) / static_cast<float>((std::max)(capacity, static_cast<size_t>(2)) - 1);
				const float start = bottomRight.x - 4.0f - step * static_cast<float>(samples.size() - 1);
				for (size_t s = 0; s < samples.size(); s++)
				{
					const float t = (std::min)(1.0f, (std::max)(0.0f, (samples[s] - low) / (high - low)));
					points.emplace_back(start + step * static_cast<float>(s), plotBottom - t * (plotBottom - plotTop));
				}
				drawList->AddPolyline(points.data(), static_cast<int>(points.size()), kInputColours[i], 0, 1.5f);
			}
			char text[160];
			snprintf(text, sizeof(text), "%s  [%.3g .. %.3g]", graph.title.c_str(), low, high);
			drawList->AddText(ImGui::GetFont(), 13.0f * scale, ImVec2(topLeft.x + 4.0f, topLeft.y + 2.0f), IM_COL32(230, 230, 230, 255), text);
			float x = topLeft.x + 4.0f;
			for (int i = 0; i < graph.inputs; i++)
				if (!graph.samples[i].empty())
				{
					snprintf(text, sizeof(text), "%.3g", graph.samples[i].back());
					drawList->AddText(ImGui::GetFont(), 12.0f * scale, ImVec2(x, plotTop), kInputColours[i], text);
					x += ImGui::GetFont()->CalcTextSizeA(12.0f * scale, FLT_MAX, 0.0f, text).x + 8.0f;
				}
			y += height + gap;
		}
	}

	void DrawMarkers(ImDrawList* drawList)
	{
		if (!g_markerOn)
			return;
		if (GetTickCount64() - g_markersAskedAt > 250)
		{
			g_markersAskedAt = GetTickCount64();
			AskMarkers();
		}
		std::vector<Marker> markers;
		{
			std::lock_guard<std::mutex> lock(g_markerMutex);
			markers = g_markers;
		}
		for (const Marker& marker : markers)
		{
			if (!marker.hasObject)
				continue;
			DEBUG_MENU::WorldCross(drawList, marker.position, 0.25f, IM_COL32(120, 255, 220, 255));
			DEBUG_MENU::WorldText(drawList, marker.position, marker.name.empty() ? "(marker)" : marker.name.c_str(), IM_COL32(120, 255, 220, 255), -10.0f);
		}
	}

	void DrawEntityOverlay(ImDrawList* drawList)
	{
		if (g_graphOn)
			DrawGraphs(drawList);
		DrawMarkers(drawList);
	}

	// ---- The tab ----
	void Status(const char* label, bool on, bool available, const char* help)
	{
		if (!available)
			ImGui::TextDisabled("%s: not available on this game release", label);
		else if (on)
			ImGui::TextColored(ImVec4(0.55f, 0.9f, 0.6f, 1.0f), "%s: on", label);
		else
			ImGui::TextDisabled("%s: off", label);
		ImGui::SameLine();
		ImGui::TextDisabled("(?)");
		if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
		{
			ImGui::BeginTooltip();
			ImGui::PushTextWrapPos(ImGui::GetFontSize() * 32.0f);
			ImGui::TextUnformatted(help);
			ImGui::PopTextWrapPos();
			ImGui::EndTooltip();
		}
	}

	void DrawTab()
	{
		ImGui::TextWrapped("The debugging script entities, which the game creates but leaves doing nothing - working again while OpenCAGE's Launch Game has Debug entities ticked (with or without this menu), so levels otherwise play as shipped.");
		Status("DebugGraph", g_graphOn, !g_attached || GraphAvailable(), "Plots the numbers linked to its Inputs while it is started, in a panel at the right of the screen: duration, samples_per_second, scale and auto_scale are followed.");
		Status("DebugMenuToggle", g_toggleOn, !g_attached || ToggleAvailable(), "When triggered, sets the debug menu switch its debug_variable names (one of this menu's switch names, e.g. characters.view_cones) to its value, or flips it when it has none. Only with this menu on.");
		Status("DebugObjectMarker", g_markerOn, true, "Shows its marked_name over its marked_object.");
		Status("DebugCaptureScreenShot", g_screenshotOn, !g_attached || ScreenshotAvailable(), "When triggered, saves the next frame as a .bmp named after its capture_filename, in a DebugCaptureScreenShot folder next to AI.exe (its field of view, clip planes and streaming wait are not followed).");

		{
			std::lock_guard<std::mutex> lock(g_graphMutex);
			ImGui::SeparatorText("Graphs");
			if (g_graphs.empty())
				ImGui::TextDisabled("None started.");
			for (const Graph& graph : g_graphs)
				ImGui::BulletText("%s: %d inputs, %.3g s at %.3g a second%s", graph.title.c_str(), graph.inputs, graph.duration, graph.rate, graph.autoScale ? ", auto scale" : "");
		}
		ImGui::SeparatorText("Object markers");
		if (!g_markerOn)
			ImGui::TextDisabled("Switched off.");
		else
		{
			std::vector<Marker> markers;
			{
				std::lock_guard<std::mutex> lock(g_markerMutex);
				markers = g_markers;
			}
			if (markers.empty())
				ImGui::TextDisabled("None in the level.");
			for (const Marker& marker : markers)
			{
				// Wrapped (an entity's composite path can be long)
				ImGui::Bullet();
				if (marker.hasObject)
					ImGui::TextWrapped("\"%s\" on %s at %.2f, %.2f, %.2f", marker.name.c_str(), SCRIPT_DEBUG::Describe(marker.object).c_str(), marker.position[0], marker.position[1], marker.position[2]);
				else
					ImGui::TextWrapped("\"%s\" (%s): no marked object", marker.name.c_str(), SCRIPT_DEBUG::Describe(marker.id).c_str());
			}
		}
		ImGui::SeparatorText("Screenshots");
		std::lock_guard<std::mutex> lock(g_screenshotMutex);
		ImGui::TextWrapped("%d saved%s%s", g_screenshots.load(), g_lastScreenshot.empty() ? "" : ", the last: ", g_lastScreenshot.c_str());
	}

	const bool g_added = DEBUG_MENU::AddTab("Entities", 150, DrawTab, "Scripts") && DEBUG_MENU::AddOverlay(97, DrawEntityOverlay);
}

void SCRIPT_ENTITIES::AttachHooks(bool attach)
{
	Attach(attach);
}

void SCRIPT_ENTITIES::DrawOverlay()
{
	DrawEntityOverlay(ImGui::GetBackgroundDrawList());
}
