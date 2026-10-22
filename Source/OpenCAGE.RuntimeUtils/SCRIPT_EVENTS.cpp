#include "SCRIPT_DEBUG.h"
#include "DEBUG_MENU.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <detours.h>

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

#include <imgui.h>

/*
	Global events (debug menu: Scripts > Events): the messages the level's scripts broadcast, as they are sent, and
	who listens for them.

	Entities that are not linked can still talk: a message is added to the entity manager (a kind, an id - a name's id -
	and a little data), and once a frame it hands each one to every entity listening for that kind and id. The kinds:
	a global event (a Global Event entity's name and value, with the entity that started the chain that sent it; the torch
	control sends its own the same way), a global position (an Update Global Position entity's name and position), and a
	character damaged, killed or stunned. Monitor entities (Global Event Monitor, Global Position...) listen.

	Each message is noted as it is added (the game's one function that adds them is hooked, and passes it on): its kind,
	id, value or position, and - for a global event - the entity that started the chain. A message's id is the id of its
	name; the names are learned from where the game makes the ids of the names: the calls in the Global Event, Global Event
	Monitor, Update Global Position and Global Position entities that turn a name into its id are pointed at a function that
	makes the id as the game does and notes the name with it, so a name shows once one of those entities has used it.

	The listeners and the messages still waiting to be handed out are read on the entity thread, under the entity manager's
	own lock on them, while the tab is open.

	Addresses are from the Steam retail AI.exe.
*/
namespace
{
	// ---- The game's ----
	// The entity manager adds a message: its kind, its id, the size of its data, the data
	constexpr uintptr_t kAddMessage = 0x00525E50;
	typedef bool(__thiscall* t_add_message)(void* manager, int kind, uint32_t id, uint32_t size, const void* data);
	t_add_message g_addMessage = nullptr;

	// The string table turns a text's offset into the text's id (the id passed back through the first argument), and into
	// the text; where the scripts' named messages make their ids
	constexpr uintptr_t kHashFromOffset = 0x005321E0;
	constexpr uintptr_t kStringFromOffset = 0x00532160;
	typedef uint32_t* (__thiscall* t_hash_from_offset)(void* table, uint32_t* out, uint32_t offset);
	typedef const char* (__thiscall* t_string_from_offset)(void* table, uint32_t offset);
	constexpr uintptr_t kNameSites[] = {
		0x0013303A, // a Global Event sending its event
		0x00133479, // a Global Event Monitor starting to listen
		0x001334F6, // ... and when it is edited live
		0x0013379E, // an Update Global Position sending its position
		0x0013393F, // a Global Position starting
		0x00133990, // ... and when it is restored from a checkpoint
	};
	constexpr size_t kNameSiteCount = sizeof(kNameSites) / sizeof(kNameSites[0]);

	// The entity manager's messages waiting to be handed out (a counted array of counted records: the data, the kind, the
	// id) and its listeners (likewise: the listening entity, the kind, the id), and the lock it holds over them
	constexpr uint32_t kManagerMessages = 0x2C;
	constexpr uint32_t kManagerListeners = 0x30;
	constexpr uint32_t kRecordObject = 0x0C;
	constexpr uint32_t kArrayCount = 0x08;
	constexpr uint32_t kArrayData = 0x0C;
	constexpr uintptr_t kAccessLock = 0x0051E1B0;
	constexpr uintptr_t kAccessUnlock = 0x0051E1D0;
	typedef void(__thiscall* t_access)(void* access);

	enum Kind : int { DamagedHealth = 0x01, CharacterKilled = 0x02, CharacterStunned = 0x04, GlobalEvent = 0x10, GlobalPosition = 0x20 };

	const char* KindName(int kind)
	{
		switch (kind)
		{
		case DamagedHealth: return "damaged";
		case CharacterKilled: return "killed";
		case CharacterStunned: return "stunned";
		case GlobalEvent: return "global event";
		case GlobalPosition: return "global position";
		default: return "?";
		}
	}

	// Names the game makes in code rather than from a script's text (worked out ahead)
	struct KnownName { uint32_t id; const char* name; };
	const KnownName kKnownNames[] = {
		{ 0xE6D79574, "torch_control" },
	};

	// ---- What is kept ----
	struct Message
	{
		ULONGLONG tick = 0;
		double scriptTime = 0.0;
		int kind = 0;
		uint32_t id = 0;
		uint32_t size = 0;
		uint8_t data[48] = {};          // its first bytes (a global position's is a whole transform, 48)
		int value = 0;                  // a global event's value
		uint32_t originEntity = 0;      // a global event's: the entity that started the chain that sent it (its two ids)
		uint32_t originInstance = 0;
		float position[3] = {};         // a global position's
	};

	struct Total
	{
		int kind = 0;
		uint32_t id = 0;
		uint32_t count = 0;
		ULONGLONG last = 0;
		int lastValue = 0;
	};

	constexpr size_t kLogSize = 512;
	constexpr size_t kMaxTotals = 512;
	constexpr size_t kMaxNames = 1024;

	SRWLOCK g_lock = SRWLOCK_INIT;
	Message g_log[kLogSize];
	size_t g_logNext = 0, g_logCount = 0;
	std::vector<Total> g_totals;
	uint64_t g_total = 0;
	std::atomic<uint32_t> g_generation = 0;
	std::atomic<bool> g_record = true;

	// Names learned (id to name)
	SRWLOCK g_nameLock = SRWLOCK_INIT;
	std::unordered_map<uint32_t, std::string> g_names;

	std::string NameOf(uint32_t id)
	{
		for (const KnownName& known : kKnownNames)
			if (known.id == id)
				return known.name;
		AcquireSRWLockShared(&g_nameLock);
		auto it = g_names.find(id);
		std::string name = it != g_names.end() ? it->second : std::string();
		ReleaseSRWLockShared(&g_nameLock);
		return name;
	}

	std::string IdText(uint32_t id)
	{
		const std::string name = NameOf(id);
		return name.empty() ? SCRIPT_DEBUG::Guid(id) : "\"" + name + "\"";
	}

	bool CopyData(const void* data, uint32_t size, uint8_t* out, size_t outSize)
	{
		__try
		{
			memcpy(out, data, (std::min)(static_cast<size_t>(size), outSize));
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void Note(int kind, uint32_t id, uint32_t size, const void* data)
	{
		Message message;
		message.tick = GetTickCount64();
		message.scriptTime = SCRIPT_DEBUG::ScriptTime();
		message.kind = kind;
		message.id = id;
		message.size = size;
		if (data && size && CopyData(data, size, message.data, sizeof(message.data)))
		{
			if (kind == GlobalEvent && size >= 12)
			{
				memcpy(&message.value, message.data, 4);
				memcpy(&message.originEntity, message.data + 4, 4);
				memcpy(&message.originInstance, message.data + 8, 4);
			}
			else if (kind == GlobalPosition && size >= 48)
			{
				// A transform as the game keeps one (twelve numbers, three rows of four): the position is the last of each row
				memcpy(&message.position[0], message.data + 0x0C, 4);
				memcpy(&message.position[1], message.data + 0x1C, 4);
				memcpy(&message.position[2], message.data + 0x2C, 4);
			}
		}
		AcquireSRWLockExclusive(&g_lock);
		g_log[g_logNext] = message;
		g_logNext = (g_logNext + 1) % kLogSize;
		g_logCount = (std::min)(g_logCount + 1, kLogSize);
		g_total++;
		Total* total = nullptr;
		for (Total& existing : g_totals)
			if (existing.kind == kind && existing.id == id)
				total = &existing;
		if (!total && g_totals.size() < kMaxTotals)
		{
			g_totals.push_back(Total());
			total = &g_totals.back();
			total->kind = kind;
			total->id = id;
		}
		if (total)
		{
			total->count++;
			total->last = message.tick;
			total->lastValue = message.value;
		}
		g_generation++;
		ReleaseSRWLockExclusive(&g_lock);
	}

	bool __fastcall h_add_message(void* manager, void* /*_EDX*/, int kind, uint32_t id, uint32_t size, const void* data)
	{
		if (g_record)
			Note(kind, id, size, data);
		return g_addMessage(manager, kind, id, size, data);
	}

	// A name turned into its id at one of the patched calls: makes the id as the game does, and notes the name
	const char* NameGuarded(void* table, uint32_t offset, t_string_from_offset text)
	{
		__try
		{
			return text(table, offset);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	t_hash_from_offset g_hashFromOffset = nullptr;
	t_string_from_offset g_stringFromOffset = nullptr;

	uint32_t* __fastcall OnNameToId(void* table, void* /*_EDX*/, uint32_t* out, uint32_t offset)
	{
		uint32_t* result = g_hashFromOffset(table, out, offset);
		if (offset != 0xFFFFFFFF && out && g_stringFromOffset)
		{
			const uint32_t id = *out;
			const char* name = NameGuarded(table, offset, g_stringFromOffset);
			char copy[128];
			size_t i = 0;
			for (; name && i + 1 < sizeof(copy) && name[i]; i++)
				copy[i] = name[i];
			copy[i] = '\0';
			if (id && copy[0])
			{
				AcquireSRWLockExclusive(&g_nameLock);
				if (g_names.size() < kMaxNames)
					g_names.emplace(id, copy);
				ReleaseSRWLockExclusive(&g_nameLock);
			}
		}
		return result;
	}

	// ---- Hooks ----
	SCRIPT_DEBUG::PatchedCall g_namePatches[kNameSiteCount];
	int g_namesPatched = 0;
	bool g_detoured = false;

	void Attach(bool attach)
	{
		if (attach)
		{
			g_addMessage = reinterpret_cast<t_add_message>(DEVTOOLS_RELATIVE_ADDRESS(kAddMessage));
			if (g_addMessage)
			{
				g_detoured = DEVTOOLS_DETOURS_ATTACH(g_addMessage, h_add_message) == NO_ERROR;
				if (!g_detoured)
					g_addMessage = reinterpret_cast<t_add_message>(DEVTOOLS_RELATIVE_ADDRESS(kAddMessage));
			}
			g_hashFromOffset = reinterpret_cast<t_hash_from_offset>(DEVTOOLS_RELATIVE_ADDRESS(kHashFromOffset));
			g_stringFromOffset = reinterpret_cast<t_string_from_offset>(DEVTOOLS_RELATIVE_ADDRESS(kStringFromOffset));
			if (g_hashFromOffset && g_stringFromOffset)
				for (size_t i = 0; i < kNameSiteCount; i++)
				{
					const char* why = nullptr;
					if (SCRIPT_DEBUG::PatchCall(kNameSites[i], kHashFromOffset, reinterpret_cast<const void*>(&OnNameToId), g_namePatches[i], why))
						g_namesPatched++;
				}
			DevTools::Log("Debug menu: global events %s; %d of %d name calls followed.", g_detoured ? "followed" : "not followed", g_namesPatched, static_cast<int>(kNameSiteCount));
		}
		else
		{
			if (g_detoured)
				DEVTOOLS_DETOURS_DETACH(g_addMessage, h_add_message);
			g_detoured = false;
			for (SCRIPT_DEBUG::PatchedCall& call : g_namePatches)
				SCRIPT_DEBUG::RestoreCall(call);
		}
	}

	const bool g_hooks = DEBUG_MENU::AddHooks(Attach);

	// ---- Listeners and waiting messages (entity thread, under the manager's lock) ----
	struct Listener
	{
		int kind = 0;
		uint32_t id = 0;
		SCRIPT_DEBUG::EntityId owner;
	};
	struct Waiting
	{
		int kind = 0;
		uint32_t id = 0;
	};
	struct ListenSnapshot
	{
		bool ok = false;
		std::vector<Listener> listeners;
		std::vector<Waiting> waiting;
	};

	struct RawListener { const void* owner; int kind; uint32_t id; };

	// Reads one of the manager's arrays of counted records, each record's object's first three words (guarded)
	int ReadRecords(const uint8_t* manager, uint32_t offset, RawListener* out, int max)
	{
		__try
		{
			const uint8_t* array = *reinterpret_cast<const uint8_t* const*>(manager + offset);
			if (!array)
				return 0;
			const uint32_t count = *reinterpret_cast<const uint32_t*>(array + kArrayCount);
			const uint8_t* const* data = *reinterpret_cast<const uint8_t* const* const*>(array + kArrayData);
			int found = 0;
			for (uint32_t i = 0; data && i < count && found < max; i++)
			{
				const uint8_t* record = data[i];
				const uint8_t* object = record ? *reinterpret_cast<const uint8_t* const*>(record + kRecordObject) : nullptr;
				if (!object)
					continue;
				out[found].owner = *reinterpret_cast<const void* const*>(object);
				out[found].kind = *reinterpret_cast<const int*>(object + 4);
				out[found].id = *reinterpret_cast<const uint32_t*>(object + 8);
				found++;
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	void ReadListeners(ListenSnapshot& out)
	{
		out = ListenSnapshot();
		if (!SCRIPT_DEBUG::LevelRunning())
			return;
		const uint8_t* manager = SCRIPT_DEBUG::EntityManager();
		const t_access lock = reinterpret_cast<t_access>(DEVTOOLS_RELATIVE_ADDRESS(kAccessLock));
		const t_access unlock = reinterpret_cast<t_access>(DEVTOOLS_RELATIVE_ADDRESS(kAccessUnlock));
		if (!manager || !lock || !unlock)
			return;
		constexpr int kMax = 2048;
		std::vector<RawListener> listeners(kMax), messages(kMax);
		uint8_t access[8] = {};
		lock(access);
		const int listenerCount = ReadRecords(manager, kManagerListeners, listeners.data(), kMax);
		const int messageCount = ReadRecords(manager, kManagerMessages, messages.data(), kMax);
		// The listeners' entities are read under the lock too (they cannot go while it is held)
		for (int i = 0; i < listenerCount; i++)
		{
			Listener listener;
			listener.kind = listeners[i].kind;
			listener.id = listeners[i].id;
			SCRIPT_DEBUG::ReadEntity(listeners[i].owner, listener.owner);
			out.listeners.push_back(listener);
		}
		unlock(access);
		for (int i = 0; i < messageCount; i++)
			out.waiting.push_back({ messages[i].kind, messages[i].id });
		std::sort(out.listeners.begin(), out.listeners.end(), [](const Listener& a, const Listener& b) { return a.kind != b.kind ? a.kind < b.kind : a.id < b.id; });
		out.ok = true;
	}

	std::mutex g_listenMutex;
	std::shared_ptr<ListenSnapshot> g_listen = std::make_shared<ListenSnapshot>();
	std::atomic<bool> g_listenAsked = false;

	void AskListeners()
	{
		if (g_listenAsked.exchange(true))
			return;
		DEBUG_MENU::OnEntityThread([]()
		{
			std::shared_ptr<ListenSnapshot> snapshot = std::make_shared<ListenSnapshot>();
			ReadListeners(*snapshot);
			{
				std::lock_guard<std::mutex> lock(g_listenMutex);
				g_listen = snapshot;
			}
			g_listenAsked = false;
		});
	}

	std::shared_ptr<ListenSnapshot> LatestListeners()
	{
		std::lock_guard<std::mutex> lock(g_listenMutex);
		return g_listen;
	}

	// ---- Text ----
	std::string MessageText(const Message& message)
	{
		char text[256];
		std::string line = std::string(KindName(message.kind)) + " " + IdText(message.id);
		if (message.kind == GlobalEvent)
		{
			snprintf(text, sizeof(text), " value=%d origin_entity=%08X origin_instance=%08X", message.value, message.originEntity, message.originInstance);
			line += text;
		}
		else if (message.kind == GlobalPosition)
		{
			snprintf(text, sizeof(text), " position=%.2f,%.2f,%.2f", message.position[0], message.position[1], message.position[2]);
			line += text;
		}
		else
		{
			line += " data=";
			for (uint32_t i = 0; i < message.size && i < sizeof(message.data); i++)
			{
				snprintf(text, sizeof(text), "%02X", message.data[i]);
				line += text;
			}
		}
		snprintf(text, sizeof(text), " id=%08X script_time=%.2f", message.id, message.scriptTime);
		return line + text;
	}

	std::vector<Message> TakeLog()
	{
		std::vector<Message> out;
		AcquireSRWLockShared(&g_lock);
		for (size_t i = 0; i < g_logCount; i++)
			out.push_back(g_log[(g_logNext + kLogSize - 1 - i) % kLogSize]);
		ReleaseSRWLockShared(&g_lock);
		return out;
	}

	std::vector<Total> TakeTotals(uint64_t& total)
	{
		AcquireSRWLockShared(&g_lock);
		std::vector<Total> out = g_totals;
		total = g_total;
		ReleaseSRWLockShared(&g_lock);
		std::sort(out.begin(), out.end(), [](const Total& a, const Total& b) { return a.last > b.last; });
		return out;
	}

	void Clear()
	{
		AcquireSRWLockExclusive(&g_lock);
		g_logNext = g_logCount = 0;
		g_totals.clear();
		g_total = 0;
		g_generation++;
		ReleaseSRWLockExclusive(&g_lock);
	}

	bool Register()
	{
		DEBUG_MENU::AddSwitch("scripts.global_events", &g_record);
		DEBUG_MENU::AddCommand("scripts.list_global_events", "the latest broadcast messages (global events and positions, damage, kills), newest first; argument: a filter",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				if (!g_detoured)
					return { false, "not available on this game release (or the debug menu's hooks are not attached)" };
				DEBUG_MENU::CommandResult result;
				for (const Message& message : TakeLog())
				{
					const std::string text = MessageText(message);
					if (SCRIPT_DEBUG::Matches(text, argument.c_str()))
						result.message += text + "\n";
				}
				if (result.message.empty())
					result.message = "(none)";
				return result;
			});
		DEBUG_MENU::AddCommand("scripts.list_global_event_counts", "each kind and id of broadcast message with how many were sent",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				uint64_t total = 0;
				DEBUG_MENU::CommandResult result;
				for (const Total& entry : TakeTotals(total))
					result.message += std::to_string(entry.count) + "x " + KindName(entry.kind) + " " + IdText(entry.id) + " id=" + SCRIPT_DEBUG::Hex(entry.id) +
						(entry.kind == GlobalEvent ? " last_value=" + std::to_string(entry.lastValue) : "") + "\n";
				return { true, "total=" + std::to_string(total) + "\n" + result.message };
			});
		DEBUG_MENU::AddCommand("scripts.list_listeners", "the entities listening for broadcast messages, and the messages waiting to be handed out",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				return DEBUG_MENU::AnswerOnEntityThread([argument]() -> DEBUG_MENU::CommandResult
				{
					ListenSnapshot snapshot;
					ReadListeners(snapshot);
					if (!snapshot.ok)
						return { false, "no level running" };
					DEBUG_MENU::CommandResult result;
					result.message = "listeners=" + std::to_string(snapshot.listeners.size()) + " waiting=" + std::to_string(snapshot.waiting.size()) + "\n";
					for (const Listener& listener : snapshot.listeners)
					{
						const std::string text = std::string(KindName(listener.kind)) + " " + IdText(listener.id) + " id=" + SCRIPT_DEBUG::Hex(listener.id) + " | " + SCRIPT_DEBUG::Fields(listener.owner);
						if (SCRIPT_DEBUG::Matches(text, argument.c_str()))
							result.message += text + "\n";
					}
					for (const Waiting& waiting : snapshot.waiting)
						result.message += std::string("waiting: ") + KindName(waiting.kind) + " " + IdText(waiting.id) + "\n";
					return result;
				});
			});
		DEBUG_MENU::AddCommand("scripts.clear_global_events", "forgets the broadcast messages noted so far",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				Clear();
				return { true, "cleared" };
			});
		return true;
	}

	const bool g_registered = Register();

	// ---- The tab ----
	char g_filter[96] = "";
	bool g_showCharacterMessages = true;
	ULONGLONG g_lastAsked = 0;

	void DrawTab()
	{
		ImGui::TextWrapped("Messages the level's scripts broadcast to entities that listen for them rather than being linked: Global Event entities' events, Update Global Position's positions, and characters being damaged, killed or stunned. A name shows once an entity that uses it has run.");
		if (!g_detoured)
		{
			ImGui::TextColored(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "Not available on this game release (or the debug menu's hooks are not attached).");
			return;
		}
		bool record = g_record;
		if (ImGui::Checkbox("Keep messages", &record))
			g_record = record;
		ImGui::SameLine();
		ImGui::Checkbox("Show character messages", &g_showCharacterMessages);
		ImGui::SameLine();
		if (ImGui::Button("Clear"))
			Clear();
		const std::vector<Message> log = TakeLog();
		ImGui::SameLine();
		if (ImGui::Button("Copy"))
		{
			std::string text;
			for (const Message& message : log)
				if (SCRIPT_DEBUG::Matches(MessageText(message), g_filter))
					text += MessageText(message) + "\n";
			ImGui::SetClipboardText(text.c_str());
		}
		ImGui::SameLine();
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##filter", "Filter", g_filter, sizeof(g_filter));

		ImGui::SeparatorText("Sent");
		if (SCRIPT_DEBUG::BeginTable("messages", 5, 200.0f * DEBUG_MENU::Scale()))
		{
			ImGui::TableSetupScrollFreeze(0, 1);
			ImGui::TableSetupColumn("Script clock");
			ImGui::TableSetupColumn("Kind");
			ImGui::TableSetupColumn("Name");
			ImGui::TableSetupColumn("Value");
			ImGui::TableSetupColumn("Started by");
			ImGui::TableHeadersRow();
			int row = 0;
			for (const Message& message : log)
			{
				if (!g_showCharacterMessages && message.kind != GlobalEvent && message.kind != GlobalPosition)
					continue;
				if (g_filter[0] && !SCRIPT_DEBUG::Matches(MessageText(message), g_filter))
					continue;
				ImGui::PushID(row++);
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("%.2f", message.scriptTime);
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(KindName(message.kind));
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(IdText(message.id).c_str());
				if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
					ImGui::SetTooltip("Id %s (0x%08X)", SCRIPT_DEBUG::Guid(message.id).c_str(), message.id);
				ImGui::TableNextColumn();
				if (message.kind == GlobalEvent)
				{
					ImGui::Text("%d", message.value);
					ImGui::TableNextColumn();
					if (!message.originEntity)
						ImGui::TextDisabled("-");
					else
					{
						ImGui::TextDisabled("%s", SCRIPT_DEBUG::Guid(message.originEntity).c_str());
						if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
							ImGui::SetTooltip("The entity that started the chain that sent it: %s (0x%08X) in composite instance %s (0x%08X)", SCRIPT_DEBUG::Guid(message.originEntity).c_str(),
								message.originEntity, SCRIPT_DEBUG::Guid(message.originInstance).c_str(), message.originInstance);
					}
				}
				else if (message.kind == GlobalPosition)
				{
					ImGui::Text("%.2f, %.2f, %.2f", message.position[0], message.position[1], message.position[2]);
				}
				else
				{
					std::string bytes;
					char hex[4];
					for (uint32_t i = 0; i < message.size && i < sizeof(message.data); i++)
					{
						snprintf(hex, sizeof(hex), "%02X", message.data[i]);
						bytes += hex;
					}
					ImGui::TextDisabled("%u bytes %s", message.size, bytes.c_str());
				}
				ImGui::PopID();
			}
			ImGui::EndTable();
		}

		uint64_t total = 0;
		const std::vector<Total> totals = TakeTotals(total);
		ImGui::SeparatorText("By name");
		ImGui::Text("%llu sent", static_cast<unsigned long long>(total));
		for (const Total& entry : totals)
			ImGui::BulletText("%s %s: %u%s", KindName(entry.kind), IdText(entry.id).c_str(), entry.count,
				entry.kind == GlobalEvent ? (" (last value " + std::to_string(entry.lastValue) + ")").c_str() : "");

		ImGui::SeparatorText("Listening");
		if (GetTickCount64() - g_lastAsked > 1000)
		{
			g_lastAsked = GetTickCount64();
			AskListeners();
		}
		const std::shared_ptr<ListenSnapshot> listen = LatestListeners();
		if (!listen->ok)
		{
			ImGui::TextDisabled("No level running.");
			return;
		}
		ImGui::Text("%d listening, %d messages waiting to be handed out", static_cast<int>(listen->listeners.size()), static_cast<int>(listen->waiting.size()));
		if (SCRIPT_DEBUG::BeginTable("listeners", 3, 180.0f * DEBUG_MENU::Scale()))
		{
			ImGui::TableSetupScrollFreeze(0, 1);
			ImGui::TableSetupColumn("Kind");
			ImGui::TableSetupColumn("Name");
			ImGui::TableSetupColumn("Listener");
			ImGui::TableHeadersRow();
			int row = 0;
			for (const Listener& listener : listen->listeners)
			{
				const std::string text = std::string(KindName(listener.kind)) + " " + IdText(listener.id) + " " + SCRIPT_DEBUG::Describe(listener.owner);
				if (!SCRIPT_DEBUG::Matches(text, g_filter))
					continue;
				ImGui::PushID(row++);
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(KindName(listener.kind));
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(IdText(listener.id).c_str());
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(SCRIPT_DEBUG::Describe(listener.owner).c_str());
				if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
					ImGui::SetTooltip("%s", SCRIPT_DEBUG::Fields(listener.owner).c_str());
				ImGui::PopID();
			}
			ImGui::EndTable();
		}
	}

	const bool g_added = DEBUG_MENU::AddTab("Events", 130, DrawTab, "Scripts");
}
