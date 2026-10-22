#include "PERF_STATS.h"
#include "Config.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	The Memory tab (see PERF_STATS.h): the game's memory heaps, the memory each kind of script entity takes, and the
	process's memory and address space.

	The game splits its memory into named heaps of fixed sizes, each keeping how much of it is allocated, the most that
	ever was, and how many allocations and frees it has had: a heap that fills up fails the next allocation from it, even
	with memory to spare elsewhere. The game also keeps, for each kind of script entity, how many there are, their size
	and the memory they take. Both are read only while the tab is shown (or a command asks), at most once a second.

	A heap's largest free piece (how fragmented it is) is not kept: the game's own report of a heap works it out, by
	walking the heap's free blocks under the heap's lock. It is called only when asked (a button, or a command), and only
	for the two kinds of heap the shipped game has, told by their vtables.

	The process is 32-bit: everything the game allocates comes out of one address space of 2 or 4 GB, so the largest
	free piece of it is what the next big allocation (a level's data, a streaming heap) needs.

	The game's own UI memory overlay (perf.ui_memory_overlay): the game still has the code that writes how much memory its
	UI (the menus and the HUD) takes, checked against a byte each frame as it draws its UI, but nothing in it ever sets the
	byte. The switch sets it (render thread; the game only reads the byte) and puts it back to 0 as the hooks detach.
	UiMemoryOverlay=1 in OpenCAGE.RuntimeUtils.ini turns it on from the start.

	Addresses and layouts are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// The heap manager (a pointer): its heaps (255 slots, a heap let go leaves its slot empty and a new one takes the
	// first empty slot, so they can have gaps) and how many there are
	constexpr uintptr_t kHeapManager = 0x0134B888;
	constexpr uint32_t kHeaps = 0x000;
	constexpr uint32_t kHeapCount = 0x400;
	constexpr int kMaxHeaps = 255;
	// A heap: its size, its allocations and frees so far, its name (64 characters), the bytes allocated now and the most
	// ever allocated
	constexpr uint32_t kHeapSize = 0x04;
	constexpr uint32_t kHeapAllocations = 0x0C;
	constexpr uint32_t kHeapFrees = 0x10;
	constexpr uint32_t kHeapName = 0x1D;
	constexpr uint32_t kHeapNameLength = 0x40;
	constexpr uint32_t kHeapAllocated = 0x74;
	constexpr uint32_t kHeapHighest = 0x78;
	// The two kinds of heap: one keeping its blocks inside its own memory, one keeping them apart from it (the streaming
	// heaps); each's report of itself is the 13th function of its vtable (taking where to write the report)
	constexpr uintptr_t kEmbeddedHeapVtable = 0x00F02800;
	constexpr uintptr_t kExternalHeapVtable = 0x00F02888;
	constexpr uint32_t kReportSlot = 12;
	// The report: free blocks, allocated blocks, free bytes, allocated bytes, the largest and smallest free blocks, the
	// most ever allocated, and how fragmented (0: one free block; towards 1: many small ones)
	struct HeapReport
	{
		uint8_t id;
		const char* name;
		uint32_t size;
		uint32_t freeBlocks;
		uint32_t allocatedBlocks;
		uint32_t freeBytes;
		uint32_t allocatedBytes;
		uint32_t largestFree;
		uint32_t smallestFree;
		uint32_t highest;
		float fragmentation;
	};
	static_assert(sizeof(HeapReport) == 0x2C, "the game's heap report");
	using MakeReport = HeapReport* (__thiscall*)(const void* heap, HeapReport* out);

	// The script entities' memory, by kind: a list from the first (a pointer), each with its kind's name, the bytes used
	// and allocated, the size of one, how many there are, how many allocations, and the next
	constexpr uintptr_t kTrackers = 0x0134DF4C;
	constexpr uint32_t kTrackerName = 0x00;
	constexpr uint32_t kTrackerUsed = 0x04;
	constexpr uint32_t kTrackerAllocated = 0x08;
	constexpr uint32_t kTrackerItemSize = 0x0C;
	constexpr uint32_t kTrackerItems = 0x10;
	constexpr uint32_t kTrackerAllocations = 0x14;
	constexpr uint32_t kTrackerNext = 0x1C;
	constexpr int kMaxTrackers = 2048;

	// The UI memory overlay's byte (zero from the start; the game never sets it). While it is set the game writes the
	// memory its UI takes, in MB, on the right of the screen: white, yellow from 29.5 MB, and red from 30.5 MB with a line
	// under it saying the UI is out of its memory budget.
	constexpr uintptr_t kUiMemoryFlag = 0x0134A790;
	// The game's check of it: compare the byte (at the address after these two bytes) with a number (the byte after the
	// address) and skip the overlay when they are equal. The number is 0; an AI.exe that an older OpenCAGE's Launch Game
	// patched on disk has 1 there, showing the overlay while the byte is 0 - the switch works either way.
	constexpr uintptr_t kUiMemoryCheck = 0x0043A6B8;
	constexpr uint8_t kCompareByte[] = { 0x80, 0x3D };
	constexpr uint32_t kCompareNumber = 6;

	enum class HeapKind { Other, Embedded, External };

	struct Heap
	{
		const uint8_t* object;
		HeapKind kind;
		char name[kHeapNameLength + 1];
		uint32_t size, allocated, highest, allocations, frees;
		// From its report, when measured
		bool reported;
		uint32_t largestFree, freeBlocks;
		float fragmentation;
	};

	struct Tracker
	{
		char name[64];
		uint32_t used, allocated, itemSize, items, allocations;
	};

	Heap g_heaps[kMaxHeaps];
	int g_heapCount = 0;
	bool g_haveHeaps = false;
	ULONGLONG g_heapsRead = 0;
	ULONGLONG g_reportsMade = 0;     // when fragmentation was last measured (0: never)

	Tracker g_trackers[kMaxTrackers];
	int g_trackerCount = 0;
	bool g_haveTrackers = false;
	ULONGLONG g_trackersRead = 0;

	uintptr_t g_embeddedVtable = 0, g_externalVtable = 0;

	std::atomic<bool> g_uiMemoryOverlay{ false }; // perf.ui_memory_overlay: wanted
	bool g_uiMemoryChecked = false;
	uint8_t* g_uiMemoryByte = nullptr;            // the overlay's byte (nullptr: not on this game release)
	uint8_t g_uiMemoryHidden = 0;                 // what the byte holds to hide the overlay (the check's number)
	bool g_uiMemoryShown = false;                 // as last written

	// ---- Reading ----

	bool ReadHeapList(uintptr_t managerAt)
	{
		__try
		{
			const uint8_t* manager = *reinterpret_cast<const uint8_t* const*>(managerAt);
			if (!manager)
				return false;
			const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + kHeapCount);
			if (count > static_cast<uint32_t>(kMaxHeaps))
				return false;
			int found = 0;
			for (int i = 0; i < kMaxHeaps && found < static_cast<int>(count); i++)
			{
				const uint8_t* heap = *reinterpret_cast<const uint8_t* const*>(manager + kHeaps + i * 4);
				if (!heap)
					continue;
				Heap& entry = g_heaps[found];
				const bool sameHeap = found < g_heapCount && entry.object == heap;
				entry.object = heap;
				const uintptr_t vtable = *reinterpret_cast<const uintptr_t*>(heap);
				entry.kind = vtable && vtable == g_embeddedVtable ? HeapKind::Embedded : vtable && vtable == g_externalVtable ? HeapKind::External : HeapKind::Other;
				memcpy(entry.name, heap + kHeapName, kHeapNameLength);
				entry.name[kHeapNameLength] = '\0';
				entry.size = *reinterpret_cast<const uint32_t*>(heap + kHeapSize);
				entry.allocated = *reinterpret_cast<const uint32_t*>(heap + kHeapAllocated);
				entry.highest = *reinterpret_cast<const uint32_t*>(heap + kHeapHighest);
				entry.allocations = *reinterpret_cast<const uint32_t*>(heap + kHeapAllocations);
				entry.frees = *reinterpret_cast<const uint32_t*>(heap + kHeapFrees);
				if (!sameHeap)
					entry.reported = false;
				found++;
			}
			g_heapCount = found;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void ReadHeaps(bool now = false)
	{
		const ULONGLONG tick = GetTickCount64();
		if (!now && g_heapsRead && tick - g_heapsRead < 1000)
			return;
		g_heapsRead = tick;
		const uintptr_t managerAt = DEVTOOLS_RELATIVE_ADDRESS(kHeapManager);
		if (!g_embeddedVtable)
		{
			g_embeddedVtable = DEVTOOLS_RELATIVE_ADDRESS(kEmbeddedHeapVtable);
			g_externalVtable = DEVTOOLS_RELATIVE_ADDRESS(kExternalHeapVtable);
		}
		g_haveHeaps = managerAt && ReadHeapList(managerAt);
	}

	// One heap's report from the game, under the heap's own lock (only for the two kinds of heap known)
	bool Report(const Heap& heap, HeapReport& out)
	{
		if (heap.kind == HeapKind::Other)
			return false;
		__try
		{
			const uintptr_t vtable = *reinterpret_cast<const uintptr_t*>(heap.object);
			if (vtable != (heap.kind == HeapKind::Embedded ? g_embeddedVtable : g_externalVtable))
				return false;
			const MakeReport make = reinterpret_cast<MakeReport>(reinterpret_cast<const uintptr_t*>(vtable)[kReportSlot]);
			memset(&out, 0, sizeof(out));
			make(heap.object, &out);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Render thread: every known heap's largest free block and fragmentation
	void MeasureFragmentation()
	{
		ReadHeaps(true);
		for (int i = 0; i < g_heapCount; i++)
		{
			Heap& heap = g_heaps[i];
			HeapReport report;
			heap.reported = Report(heap, report);
			if (heap.reported)
			{
				heap.largestFree = report.largestFree;
				heap.freeBlocks = report.freeBlocks;
				heap.fragmentation = report.fragmentation;
			}
		}
		g_reportsMade = GetTickCount64();
	}

	bool ReadTrackerList(uintptr_t firstAt)
	{
		__try
		{
			const uint8_t* tracker = *reinterpret_cast<const uint8_t* const*>(firstAt);
			int found = 0;
			for (int guard = 0; tracker && guard < 8192 && found < kMaxTrackers; guard++)
			{
				const uint32_t items = *reinterpret_cast<const uint32_t*>(tracker + kTrackerItems);
				const uint32_t allocated = *reinterpret_cast<const uint32_t*>(tracker + kTrackerAllocated);
				if (items || allocated)
				{
					Tracker& entry = g_trackers[found++];
					const char* name = *reinterpret_cast<const char* const*>(tracker + kTrackerName);
					size_t length = 0;
					if (name)
						while (length < sizeof(entry.name) - 1 && name[length])
						{
							entry.name[length] = name[length];
							length++;
						}
					entry.name[length] = '\0';
					entry.used = *reinterpret_cast<const uint32_t*>(tracker + kTrackerUsed);
					entry.allocated = allocated;
					entry.itemSize = *reinterpret_cast<const uint32_t*>(tracker + kTrackerItemSize);
					entry.items = items;
					entry.allocations = *reinterpret_cast<const uint32_t*>(tracker + kTrackerAllocations);
				}
				tracker = *reinterpret_cast<const uint8_t* const*>(tracker + kTrackerNext);
			}
			g_trackerCount = found;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void ReadTrackers()
	{
		const ULONGLONG tick = GetTickCount64();
		if (g_trackersRead && tick - g_trackersRead < 1000)
			return;
		g_trackersRead = tick;
		const uintptr_t firstAt = DEVTOOLS_RELATIVE_ADDRESS(kTrackers);
		g_haveTrackers = firstAt && ReadTrackerList(firstAt);
		if (g_haveTrackers)
			std::sort(g_trackers, g_trackers + g_trackerCount, [](const Tracker& a, const Tracker& b) { return a.allocated > b.allocated; });
	}

	// ---- The UI memory overlay ----

	// The game's check of the overlay's byte as expected (comparing that byte with 0 or 1): the number, and the byte now
	bool ReadUiMemoryCheck(const uint8_t* check, uintptr_t flag, uint8_t& number, uint8_t& now)
	{
		__try
		{
			if (check[0] != kCompareByte[0] || check[1] != kCompareByte[1] || *reinterpret_cast<const uintptr_t*>(check + 2) != flag)
				return false;
			number = check[kCompareNumber];
			now = *reinterpret_cast<const uint8_t*>(flag);
			return number <= 1;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool WriteUiMemoryByte(uint8_t* at, uint8_t value)
	{
		__try
		{
			*reinterpret_cast<volatile uint8_t*>(at) = value;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Once: where the byte is, when the game's check of it is where it should be
	void CheckUiMemory()
	{
		if (g_uiMemoryChecked)
			return;
		g_uiMemoryChecked = true;
		const uintptr_t flag = DEVTOOLS_RELATIVE_ADDRESS(kUiMemoryFlag);
		const uintptr_t check = DEVTOOLS_RELATIVE_ADDRESS(kUiMemoryCheck);
		uint8_t number = 0, now = 0;
		if (!flag || !check || !ReadUiMemoryCheck(reinterpret_cast<const uint8_t*>(check), flag, number, now))
		{
			DevTools::Log("Debug menu: perf.ui_memory_overlay is not available on this game release.");
			return;
		}
		g_uiMemoryByte = reinterpret_cast<uint8_t*>(flag);
		g_uiMemoryHidden = number;
		g_uiMemoryShown = now != number;
		if (number)
			DevTools::Log("Debug menu: this AI.exe has the UI memory overlay patched on (by an older OpenCAGE's Launch Game); perf.ui_memory_overlay starts ticked.");
	}

	// Brings the byte in line with the switch (ticked in the tab or set from outside): at attach, then every frame on the
	// render thread. The game only reads the byte, as it draws its UI.
	void ApplyUiMemory()
	{
		CheckUiMemory();
		if (!g_uiMemoryByte)
		{
			g_uiMemoryOverlay = false;
			return;
		}
		const bool wanted = g_uiMemoryOverlay;
		if (wanted == g_uiMemoryShown)
			return;
		if (WriteUiMemoryByte(g_uiMemoryByte, wanted ? static_cast<uint8_t>(g_uiMemoryHidden ^ 1) : g_uiMemoryHidden))
		{
			g_uiMemoryShown = wanted;
			DevTools::Log("Debug menu: perf.ui_memory_overlay %s.", wanted ? "on" : "off");
		}
		else
			g_uiMemoryOverlay = g_uiMemoryShown;
	}

	void ApplyUiMemoryOverlay(ImDrawList* /*drawList*/)
	{
		ApplyUiMemory();
	}

	void AttachUiMemory(bool attach)
	{
		if (attach)
		{
			// On from the start with UiMemoryOverlay=1 (before the game first draws its UI), or when it shows already
			CheckUiMemory();
			g_uiMemoryOverlay = Config::IniInt("UiMemoryOverlay", 0) != 0 || g_uiMemoryShown;
			ApplyUiMemory();
			return;
		}
		// Put back to the game's own 0, and left alone from then on
		if (g_uiMemoryByte)
			WriteUiMemoryByte(g_uiMemoryByte, 0);
		g_uiMemoryByte = nullptr;
	}

	// ---- Text ----

	std::string Format(const char* format, ...)
	{
		char text[512];
		va_list args;
		va_start(args, format);
		vsnprintf(text, sizeof(text), format, args);
		va_end(args);
		return text;
	}

	float Percent(uint64_t used, uint64_t of)
	{
		return of ? 100.0f * static_cast<float>(static_cast<double>(used) / static_cast<double>(of)) : 0.0f;
	}

	const char* KindName(HeapKind kind)
	{
		return kind == HeapKind::Embedded ? "embedded" : kind == HeapKind::External ? "external" : "other";
	}

	// The heaps, fullest first
	std::vector<int> HeapsByFullest()
	{
		std::vector<int> order;
		for (int i = 0; i < g_heapCount; i++)
			order.push_back(i);
		std::stable_sort(order.begin(), order.end(), [](int a, int b) { return Percent(g_heaps[a].allocated, g_heaps[a].size) > Percent(g_heaps[b].allocated, g_heaps[b].size); });
		return order;
	}

	std::string ProcessText()
	{
		const PERF_STATS::ProcessNumbers& process = PERF_STATS::Process();
		std::string text;
		text += Format("private_bytes=%llu\nworking_set=%llu\n", process.privateBytes, process.workingSet);
		if (process.haveAddress)
			text += Format("address_space=%llu\naddress_free=%llu\naddress_used=%llu\nlargest_free=%llu\nfree_pieces=%u\n", process.addressTotal, process.addressFree,
				process.addressTotal - process.addressFree, process.largestFree, process.freePieces);
		else
			text += "address_space=reading... (ask again in a moment)\n";
		if (process.haveVideo)
			text += Format("video_used=%llu\nvideo_budget=%llu\n", process.videoUsed, process.videoBudget);
		else
			text += "video=unavailable\n";
		return text;
	}

	std::string HeapsText(bool reports)
	{
		if (reports)
			MeasureFragmentation();
		else
			ReadHeaps(true);
		if (!g_haveHeaps)
			return "the heaps could not be read (not on this build of the game?)";
		std::string text;
		for (int index : HeapsByFullest())
		{
			const Heap& heap = g_heaps[index];
			text += Format("%s used=%u size=%u percent=%.1f peak=%u blocks=%u kind=%s", heap.name[0] ? heap.name : "(no name)", heap.allocated, heap.size,
				Percent(heap.allocated, heap.size), heap.highest, heap.allocations - heap.frees, KindName(heap.kind));
			if (heap.reported)
				text += Format(" largest_free=%u free_blocks=%u fragmentation=%.3f", heap.largestFree, heap.freeBlocks, heap.fragmentation);
			text += "\n";
		}
		return text;
	}

	std::string TrackersText()
	{
		g_trackersRead = 0;
		ReadTrackers();
		if (!g_haveTrackers)
			return "the script entities' memory could not be read (not on this build of the game?)";
		std::string text;
		uint64_t allocated = 0;
		for (int i = 0; i < g_trackerCount; i++)
		{
			const Tracker& tracker = g_trackers[i];
			allocated += tracker.allocated;
			text += Format("%s items=%u item_size=%u used=%u allocated=%u\n", tracker.name[0] ? tracker.name : "(no name)", tracker.items, tracker.itemSize, tracker.used, tracker.allocated);
		}
		return Format("kinds=%d\nallocated=%llu\n", g_trackerCount, allocated) + text;
	}

	// ---- Drawing ----

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

	ImVec4 UseColour(float percent)
	{
		if (percent >= 95.0f)
			return ImVec4(1.0f, 0.35f, 0.3f, 1.0f);
		if (percent >= 85.0f)
			return ImVec4(1.0f, 0.75f, 0.3f, 1.0f);
		return ImGui::GetStyleColorVec4(ImGuiCol_Text);
	}

	void Bar(float percent)
	{
		char overlay[16];
		snprintf(overlay, sizeof(overlay), "%.0f%%", percent);
		ImGui::PushStyleColor(ImGuiCol_PlotHistogram, percent >= 95.0f ? ImVec4(0.85f, 0.25f, 0.2f, 1.0f) : percent >= 85.0f ? ImVec4(0.85f, 0.6f, 0.2f, 1.0f) : ImVec4(0.3f, 0.6f, 0.35f, 1.0f));
		ImGui::ProgressBar((std::min)(1.0f, percent / 100.0f), ImVec2(-FLT_MIN, 0.0f), overlay);
		ImGui::PopStyleColor();
	}

	void DrawProcess()
	{
		const PERF_STATS::ProcessNumbers& process = PERF_STATS::Process();
		if (!process.have)
			return;
		ImGui::TextWrapped("Memory committed: %s (%s of it in physical memory)", PERF_STATS::Bytes(process.privateBytes).c_str(), PERF_STATS::Bytes(process.workingSet).c_str());
		if (process.haveAddress)
		{
			const uint64_t used = process.addressTotal - process.addressFree;
			ImGui::TextWrapped("Address space: %s used of %s", PERF_STATS::Bytes(used).c_str(), PERF_STATS::Bytes(process.addressTotal).c_str());
			Tooltip("The game is a 32-bit program: all it allocates comes out of this one address space.");
			Bar(Percent(used, process.addressTotal));
			const bool low = process.largestFree < 64ull * 1024 * 1024;
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextColored(low ? ImVec4(1.0f, 0.5f, 0.3f, 1.0f) : ImGui::GetStyleColorVec4(ImGuiCol_Text), "Largest free piece: %s (%u free pieces)", PERF_STATS::Bytes(process.largestFree).c_str(), process.freePieces);
			ImGui::PopTextWrapPos();
			Tooltip("The biggest single allocation that can still succeed. When this gets small, loading a big level or streaming in more can fail even with memory free overall.");
		}
		else
			PERF_STATS::Note("Address space: reading... (the first reading takes a moment)");
		if (process.haveVideo)
		{
			ImGui::TextWrapped("Graphics card memory: %s used of a budget of %s", PERF_STATS::Bytes(process.videoUsed).c_str(), PERF_STATS::Bytes(process.videoBudget).c_str());
			Tooltip("The budget is what Windows gives the game of the card's own memory just now.");
		}
	}

	void DrawHeaps()
	{
		ReadHeaps();
		if (!g_haveHeaps)
		{
			PERF_STATS::Note("Not available on this build of the game.");
			return;
		}
		if (ImGui::Button("Measure fragmentation"))
			MeasureFragmentation();
		Tooltip("Asks each heap for its largest free block and how fragmented it is (perf.heap_fragmentation), for the table's last column. Each heap is walked under its own lock: a moment's pause.");
		if (g_reportsMade)
		{
			ImGui::SameLine();
			ImGui::TextDisabled("measured %llu s ago", (GetTickCount64() - g_reportsMade) / 1000);
		}
		// Each heap's name in full (kept in view while the table scrolls sideways), the sizes as wide as the widest a size
		// can be written, and the bars taking the rest
		float nameWidth = PERF_STATS::Width("Heap");
		for (int i = 0; i < g_heapCount; i++)
			nameWidth = (std::max)(nameWidth, PERF_STATS::Width(g_heaps[i].name[0] ? g_heaps[i].name : "(no name)"));
		const float bytesWidth = PERF_STATS::Width("1023.9 MB");
		const float largestWidth = (std::max)(bytesWidth, PERF_STATS::Width("Largest free"));
		const float minWidth = nameWidth + bytesWidth * 3.0f + PERF_STATS::BarWidth() + largestWidth;
		if (!PERF_STATS::BeginScrollingTable("heaps", 6, minWidth, g_heapCount, true, 300.0f * DEBUG_MENU::Scale()))
			return;
		const bool reports = g_reportsMade != 0;
		ImGui::TableSetupScrollFreeze(1, 1);
		ImGui::TableSetupColumn("Heap", ImGuiTableColumnFlags_WidthFixed | ImGuiTableColumnFlags_NoHide, nameWidth);
		ImGui::TableSetupColumn("Used", ImGuiTableColumnFlags_WidthFixed, bytesWidth);
		ImGui::TableSetupColumn("Size", ImGuiTableColumnFlags_WidthFixed, bytesWidth);
		ImGui::TableSetupColumn("Full", ImGuiTableColumnFlags_WidthStretch);
		ImGui::TableSetupColumn("Most", ImGuiTableColumnFlags_WidthFixed, bytesWidth);
		ImGui::TableSetupColumn("Largest free", ImGuiTableColumnFlags_WidthFixed, largestWidth);
		ImGui::TableHeadersRow();
		for (int index : HeapsByFullest())
		{
			const Heap& heap = g_heaps[index];
			const float percent = Percent(heap.allocated, heap.size);
			// The text before the bar level with the bar's
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::AlignTextToFramePadding();
			ImGui::TextUnformatted(heap.name[0] ? heap.name : "(no name)");
			if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
				ImGui::SetTooltip("%s heap; %u blocks allocated now (%u allocations, %u frees so far)", KindName(heap.kind), heap.allocations - heap.frees, heap.allocations, heap.frees);
			ImGui::TableNextColumn();
			ImGui::AlignTextToFramePadding();
			ImGui::TextColored(UseColour(percent), "%s", PERF_STATS::Bytes(heap.allocated).c_str());
			ImGui::TableNextColumn();
			ImGui::AlignTextToFramePadding();
			ImGui::TextUnformatted(PERF_STATS::Bytes(heap.size).c_str());
			ImGui::TableNextColumn();
			Bar(percent);
			ImGui::TableNextColumn();
			ImGui::TextColored(UseColour(Percent(heap.highest, heap.size)), "%s", PERF_STATS::Bytes(heap.highest).c_str());
			ImGui::TableNextColumn();
			if (reports && heap.reported)
			{
				ImGui::Text("%s", PERF_STATS::Bytes(heap.largestFree).c_str());
				if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
					ImGui::SetTooltip("%u free blocks; fragmentation %.2f (0: one free block; towards 1: many small ones)", heap.freeBlocks, heap.fragmentation);
			}
			else
			{
				ImGui::TextDisabled("-");
				Tooltip(heap.kind == HeapKind::Other ? "Not measured: the game can't report this kind of heap." : "Not measured yet: Measure fragmentation (above the table) fills this in.");
			}
		}
		PERF_STATS::EndTable();
	}

	char g_trackerFilter[64] = "";

	void DrawTrackers()
	{
		ReadTrackers();
		if (!g_haveTrackers)
		{
			PERF_STATS::Note("Not available on this build of the game.");
			return;
		}
		uint64_t allocated = 0, used = 0, items = 0;
		for (int i = 0; i < g_trackerCount; i++)
		{
			allocated += g_trackers[i].allocated;
			used += g_trackers[i].used;
			items += g_trackers[i].items;
		}
		ImGui::TextWrapped("%d kinds, %llu entities: %s used, %s allocated", g_trackerCount, items, PERF_STATS::Bytes(used).c_str(), PERF_STATS::Bytes(allocated).c_str());
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##trackerfilter", "Filter", g_trackerFilter, sizeof(g_trackerFilter));
		// The kinds the filter lets through; each kind's name in full, the numbers as wide as the widest they can be written
		std::string filter = g_trackerFilter;
		for (char& c : filter)
			c = static_cast<char>(toupper(static_cast<unsigned char>(c)));
		std::vector<int> shown;
		float kindWidth = PERF_STATS::Width("Kind");
		for (int i = 0; i < g_trackerCount; i++)
		{
			const Tracker& tracker = g_trackers[i];
			if (!filter.empty())
			{
				std::string name = tracker.name;
				for (char& c : name)
					c = static_cast<char>(toupper(static_cast<unsigned char>(c)));
				if (name.find(filter) == std::string::npos)
					continue;
			}
			shown.push_back(i);
			kindWidth = (std::max)(kindWidth, PERF_STATS::Width(tracker.name[0] ? tracker.name : "(no name)"));
		}
		const float countWidth = (std::max)(PERF_STATS::Width("Count"), PERF_STATS::Width("0000000"));
		const float bytesWidth = PERF_STATS::Width("1023.9 MB");
		const float sizeWidth = (std::max)(bytesWidth, PERF_STATS::Width("Size of one"));
		const float allocatedWidth = (std::max)(bytesWidth, PERF_STATS::Width("Allocated"));
		const float minWidth = kindWidth + countWidth + sizeWidth + bytesWidth + allocatedWidth;
		if (!PERF_STATS::BeginScrollingTable("trackers", 5, minWidth, static_cast<int>(shown.size()), false, 300.0f * DEBUG_MENU::Scale()))
			return;
		ImGui::TableSetupScrollFreeze(1, 1);
		ImGui::TableSetupColumn("Kind", ImGuiTableColumnFlags_WidthStretch | ImGuiTableColumnFlags_NoHide);
		ImGui::TableSetupColumn("Count", ImGuiTableColumnFlags_WidthFixed, countWidth);
		ImGui::TableSetupColumn("Size of one", ImGuiTableColumnFlags_WidthFixed, sizeWidth);
		ImGui::TableSetupColumn("Used", ImGuiTableColumnFlags_WidthFixed, bytesWidth);
		ImGui::TableSetupColumn("Allocated", ImGuiTableColumnFlags_WidthFixed, allocatedWidth);
		ImGui::TableHeadersRow();
		for (int i : shown)
		{
			const Tracker& tracker = g_trackers[i];
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(tracker.name[0] ? tracker.name : "(no name)");
			ImGui::TableNextColumn();
			ImGui::Text("%u", tracker.items);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(PERF_STATS::Bytes(tracker.itemSize).c_str());
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(PERF_STATS::Bytes(tracker.used).c_str());
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(PERF_STATS::Bytes(tracker.allocated).c_str());
		}
		PERF_STATS::EndTable();
	}

	void DrawUiMemory()
	{
		CheckUiMemory();
		bool shown = g_uiMemoryOverlay;
		ImGui::BeginDisabled(!g_uiMemoryByte);
		if (ImGui::Checkbox("The game's UI memory overlay", &shown))
			g_uiMemoryOverlay = shown;
		ImGui::EndDisabled();
		Tooltip(g_uiMemoryByte ? "The game's own overlay (perf.ui_memory_overlay): the memory its UI (the menus and the HUD) takes, in MB, written by the game on the right of the screen. White within budget, yellow from 29.5 MB, and red from 30.5 MB with a line under it saying the UI is out of its memory budget. UiMemoryOverlay=1 in OpenCAGE.RuntimeUtils.ini turns it on from the start."
			: "The game's own overlay of the memory its UI takes: not available on this game release.");
		if (!g_uiMemoryByte)
		{
			ImGui::SameLine();
			ImGui::TextDisabled("not available on this game release");
		}
	}

	void DrawMemoryTab()
	{
		PERF_STATS::EnsureRunning();
		DrawUiMemory();
		if (ImGui::CollapsingHeader("The process", ImGuiTreeNodeFlags_DefaultOpen))
			DrawProcess();
		if (ImGui::CollapsingHeader("Heaps", ImGuiTreeNodeFlags_DefaultOpen))
			DrawHeaps();
		if (ImGui::CollapsingHeader("Script entities' memory"))
			DrawTrackers();
	}

	DEBUG_MENU::CommandResult OnRender(std::string (*make)())
	{
		PERF_STATS::EnsureRunning();
		return DEBUG_MENU::AnswerOnRenderThread([make]() -> DEBUG_MENU::CommandResult { return { true, make() }; });
	}

	bool Register()
	{
		DEBUG_MENU::AddSwitch("perf.ui_memory_overlay", &g_uiMemoryOverlay);
		DEBUG_MENU::AddCommand("perf.memory", "the process's memory, address space (total, free, used, largest free piece; walked every few seconds, address_space=reading... until the first walk) and the graphics card's memory, key=value bytes",
			[](const std::string&) { return OnRender([]() { return ProcessText(); }); });
		DEBUG_MENU::AddCommand("perf.list_heaps", "the game's memory heaps, fullest first, one a line: NAME used= size= percent= peak= blocks= kind=",
			[](const std::string&) { return OnRender([]() { return HeapsText(false); }); });
		DEBUG_MENU::AddCommand("perf.heap_fragmentation", "measures each heap's largest free block (the game walks each under its lock), then lists the heaps as perf.list_heaps with largest_free= free_blocks= fragmentation=",
			[](const std::string&) { return OnRender([]() { return HeapsText(true); }); });
		DEBUG_MENU::AddCommand("perf.list_entity_memory", "the script entities' memory by kind, most allocated first: kinds=, allocated=, then NAME items= item_size= used= allocated=",
			[](const std::string&) { return OnRender([]() { return TrackersText(); }); });
		return true;
	}

	const bool g_registered = Register();
	const bool g_added = DEBUG_MENU::AddTab("Memory", 102, DrawMemoryTab, "Performance");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(7, ApplyUiMemoryOverlay);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachUiMemory);
}
