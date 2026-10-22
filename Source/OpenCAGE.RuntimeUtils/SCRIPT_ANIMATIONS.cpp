#include "SCRIPT_DEBUG.h"
#include "DEBUG_MENU.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

#include <imgui.h>

/*
	CAGEAnimations (debug menu: Scripts > CAGEAnimations): the level's animation entities and where each is - which are
	playing, how far through, and which play a cutscene.

	A CAGEAnimation keeps the time it is at and the time it was last applied from; while it plays the game marks it as
	updated every frame and advances its time by the frame's time step (times its play speed), applying its tracks to the
	entities they move. One that plays a cutscene clip holds the clip. Its length is one of its settings, read through its
	interface the way Live Link reads it (with the entity manager's pause context pushed for it, as the game's own
	reads of an entity's settings from outside its update are).

	The animations are found through the level's composites (each composite holding one, in each of its running
	instances) on the entity thread, about four times a second while the tab is open, or when asked. Nothing is changed:
	OpenCAGE's own animation drive (Live Link's ANIMATION) holds and scrubs one.

	Addresses are from the Steam retail AI.exe.
*/
namespace
{
	// An animation entity's fields (its vtable checked first)
	constexpr uintptr_t kAnimationVTable = 0x00EA8BF8;
	constexpr uint32_t kRecordObject = 0x0C;
	constexpr uint32_t kEntityState = 0x04;
	constexpr uint32_t kAnimationInterface = 0x1C;   // its interface, which its length is read through
	constexpr uint32_t kAnimationPrevious = 0x28;    // the time it was last applied from (a double)
	constexpr uint32_t kAnimationCurrent = 0x30;     // the time it is at (a double)
	constexpr uint32_t kAnimationData = 0x38;        // its keyframe data (packed; 0xFFFFFFFF: none)
	constexpr uint32_t kAnimationClip = 0x44;        // the cutscene clip it plays (null but for cinematics)

	constexpr uint32_t kStateStarted = 1u << 2;
	constexpr uint32_t kStateEnabled = 1u << 7;
	constexpr uint32_t kStatePaused = 1u << 3;
	constexpr uint32_t kStateUpdating = 1u << 18;
	constexpr uint32_t kStateInitialised = 1u << 28;

	// The animation's length setting, read through its interface; the entity manager's pause context pushed and popped
	// around it for the entity
	constexpr uintptr_t kAnimationLength = 0x004EA500;
	typedef float(__thiscall* t_animation_length)(void* animationInterface, void** self);
	constexpr uintptr_t kPushPauseContext = 0x0052D310;
	constexpr uintptr_t kPopPauseContext = 0x0052F0E0;
	typedef void(__thiscall* t_pause_context)(void* manager, void** entity);

	struct Animation
	{
		SCRIPT_DEBUG::EntityId id;
		uint32_t state = 0;
		double current = 0.0, previous = 0.0;
		float length = -1.0f;          // -1: not read (only read for one that is started or playing)
		bool cinematic = false;
		bool hasData = false;
	};

	struct RawAnimation
	{
		uint32_t state;
		double current, previous;
		bool cinematic, hasData;
	};

	bool ReadAnimation(const uint8_t* record, uintptr_t vtable, RawAnimation& out)
	{
		__try
		{
			const uint8_t* object = *reinterpret_cast<const uint8_t* const*>(record + kRecordObject);
			if (!object || *reinterpret_cast<const uintptr_t*>(object) != vtable)
				return false;
			out.state = *reinterpret_cast<const uint32_t*>(object + kEntityState);
			out.current = *reinterpret_cast<const double*>(object + kAnimationCurrent);
			out.previous = *reinterpret_cast<const double*>(object + kAnimationPrevious);
			out.hasData = *reinterpret_cast<const uint32_t*>(object + kAnimationData) != 0xFFFFFFFF;
			out.cinematic = *reinterpret_cast<void* const*>(object + kAnimationClip) != nullptr;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool PauseContextGuarded(t_pause_context call, void* manager, void** entity)
	{
		__try
		{
			call(manager, entity);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	float LengthGuarded(t_animation_length length, void* record, void** entity)
	{
		__try
		{
			uint8_t* object = *reinterpret_cast<uint8_t**>(static_cast<uint8_t*>(record) + kRecordObject);
			return length(object + kAnimationInterface, entity);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1.0f;
		}
	}

	// The length, with the pause context pushed for the entity around the read (and always popped once pushed)
	float ReadLength(void* manager, void* record)
	{
		const t_animation_length length = reinterpret_cast<t_animation_length>(DEVTOOLS_RELATIVE_ADDRESS(kAnimationLength));
		const t_pause_context push = reinterpret_cast<t_pause_context>(DEVTOOLS_RELATIVE_ADDRESS(kPushPauseContext));
		const t_pause_context pop = reinterpret_cast<t_pause_context>(DEVTOOLS_RELATIVE_ADDRESS(kPopPauseContext));
		if (!length || !push || !pop || !manager)
			return -1.0f;
		void* entity = record;
		if (!PauseContextGuarded(push, manager, &entity))
			return -1.0f;
		const float value = LengthGuarded(length, record, &entity);
		PauseContextGuarded(pop, manager, &entity);
		return value;
	}

	struct Snapshot
	{
		bool ok = false;
		ULONGLONG tick = 0;
		int total = 0;
		std::vector<Animation> animations;   // playing first, then started, then the rest
	};

	// Entity thread
	void Read(Snapshot& out)
	{
		out = Snapshot();
		out.tick = GetTickCount64();
		const uint32_t types[] = { SCRIPT_DEBUG::kTypeCAGEAnimation };
		std::vector<SCRIPT_DEBUG::FoundEntity> found;
		if (!SCRIPT_DEBUG::FindEntities(types, 1, found, 8192))
			return;
		const uintptr_t vtable = DEVTOOLS_RELATIVE_ADDRESS(kAnimationVTable);
		void* manager = SCRIPT_DEBUG::EntityManager();
		for (const SCRIPT_DEBUG::FoundEntity& entity : found)
		{
			RawAnimation raw;
			if (!vtable || !ReadAnimation(static_cast<const uint8_t*>(entity.record), vtable, raw))
				continue;
			Animation animation;
			animation.state = raw.state;
			animation.current = raw.current;
			animation.previous = raw.previous;
			animation.cinematic = raw.cinematic;
			animation.hasData = raw.hasData;
			SCRIPT_DEBUG::ReadEntity(entity.record, animation.id);
			if ((raw.state & (kStateStarted | kStateUpdating)) && (raw.state & kStateInitialised))
				animation.length = ReadLength(manager, const_cast<void*>(entity.record));
			out.animations.push_back(animation);
		}
		out.total = static_cast<int>(out.animations.size());
		auto rank = [](const Animation& a) { return (a.state & kStateUpdating) ? 0 : (a.state & kStateStarted) ? 1 : 2; };
		std::stable_sort(out.animations.begin(), out.animations.end(), [&](const Animation& a, const Animation& b) { return rank(a) < rank(b); });
		out.ok = true;
	}

	std::mutex g_mutex;
	std::shared_ptr<Snapshot> g_snapshot = std::make_shared<Snapshot>();
	std::atomic<bool> g_asked = false;

	void Ask()
	{
		if (g_asked.exchange(true))
			return;
		DEBUG_MENU::OnEntityThread([]()
		{
			std::shared_ptr<Snapshot> snapshot = std::make_shared<Snapshot>();
			Read(*snapshot);
			{
				std::lock_guard<std::mutex> lock(g_mutex);
				g_snapshot = snapshot;
			}
			g_asked = false;
		});
	}

	std::shared_ptr<Snapshot> Latest()
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		return g_snapshot;
	}

	const char* StateText(const Animation& animation)
	{
		if (!(animation.state & kStateInitialised))
			return "not set up";
		if (animation.state & kStateUpdating)
			return (animation.state & kStatePaused) ? "playing (paused)" : "playing";
		if (animation.state & kStateStarted)
			return "started, held";
		return (animation.state & kStateEnabled) ? "stopped" : "disabled";
	}

	std::string AnimationText(const Animation& animation)
	{
		char text[160];
		snprintf(text, sizeof(text), "%s time=%.3f length=%s%s%s state=%08X | ", StateText(animation), animation.current,
			animation.length >= 0.0f ? std::to_string(animation.length).c_str() : "?", animation.cinematic ? " cinematic" : "", animation.hasData ? "" : " no_data", animation.state);
		return text + SCRIPT_DEBUG::Fields(animation.id);
	}

	bool Register()
	{
		DEBUG_MENU::AddCommand("scripts.list_animations", "the level's CAGEAnimations that are playing or started (argument \"all\": every one), with time and length",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				const bool all = argument == "all";
				return DEBUG_MENU::AnswerOnEntityThread([all]() -> DEBUG_MENU::CommandResult
				{
					Snapshot snapshot;
					Read(snapshot);
					if (!snapshot.ok)
						return { false, "no level running" };
					int playing = 0;
					for (const Animation& animation : snapshot.animations)
						playing += (animation.state & kStateUpdating) ? 1 : 0;
					DEBUG_MENU::CommandResult result;
					result.message = "animations=" + std::to_string(snapshot.total) + " playing=" + std::to_string(playing) + "\n";
					for (const Animation& animation : snapshot.animations)
						if (all || (animation.state & (kStateUpdating | kStateStarted)))
							result.message += AnimationText(animation) + "\n";
					return result;
				}, 4000);
			});
		return true;
	}

	const bool g_registered = Register();

	// ---- The tab ----
	char g_filter[96] = "";
	bool g_showAll = false;
	ULONGLONG g_lastAsked = 0;

	void DrawTab()
	{
		if (GetTickCount64() - g_lastAsked > 250)
		{
			g_lastAsked = GetTickCount64();
			Ask();
		}
		ImGui::TextWrapped("The level's CAGEAnimation entities: those playing (the game advancing their time each frame) first, then those started but held. OpenCAGE's animation mode holds and scrubs one.");
		const std::shared_ptr<Snapshot> snapshot = Latest();
		ImGui::Checkbox("Show every one (stopped too)", &g_showAll);
		ImGui::SameLine();
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##filter", "Filter", g_filter, sizeof(g_filter));
		ImGui::SameLine();
		if (ImGui::Button("Copy"))
		{
			std::string text;
			for (const Animation& animation : snapshot->animations)
				if ((g_showAll || (animation.state & (kStateUpdating | kStateStarted))) && SCRIPT_DEBUG::Matches(AnimationText(animation) + SCRIPT_DEBUG::Describe(animation.id), g_filter))
					text += AnimationText(animation) + "\n";
			ImGui::SetClipboardText(text.c_str());
		}
		if (!snapshot->ok)
		{
			ImGui::TextDisabled(snapshot->tick ? "No level running." : "Not read yet.");
			return;
		}
		int playing = 0, started = 0;
		for (const Animation& animation : snapshot->animations)
		{
			playing += (animation.state & kStateUpdating) ? 1 : 0;
			started += (!(animation.state & kStateUpdating) && (animation.state & kStateStarted)) ? 1 : 0;
		}
		ImGui::Text("%d in the level: %d playing, %d started and held", snapshot->total, playing, started);

		// The time bars as wide as the longest time they show, and no wider
		const float barWidth = ImGui::CalcTextSize("0000.00 / 0000.00 s").x + ImGui::GetStyle().FramePadding.x * 4.0f;
		if (!SCRIPT_DEBUG::BeginTable("animations", 4, 360.0f * DEBUG_MENU::Scale()))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("State");
		ImGui::TableSetupColumn("Time");
		ImGui::TableSetupColumn("Entity");
		ImGui::TableSetupColumn("Composite");
		ImGui::TableHeadersRow();
		int row = 0;
		for (const Animation& animation : snapshot->animations)
		{
			if (!g_showAll && !(animation.state & (kStateUpdating | kStateStarted)))
				continue;
			if (g_filter[0] && !SCRIPT_DEBUG::Matches(AnimationText(animation) + SCRIPT_DEBUG::Describe(animation.id), g_filter))
				continue;
			ImGui::PushID(row++);
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			const ImVec4 colour = (animation.state & kStateUpdating) ? ImVec4(0.5f, 0.9f, 0.5f, 1.0f) : (animation.state & kStateStarted) ? ImVec4(1.0f, 0.85f, 0.4f, 1.0f) : ImVec4(0.6f, 0.6f, 0.6f, 1.0f);
			// On the same line as the time bar's text, when the row has one (the cells after it are lowered to it)
			if (animation.length > 0.0f)
				ImGui::AlignTextToFramePadding();
			ImGui::TextColored(colour, "%s%s", StateText(animation), animation.cinematic ? " (cutscene)" : "");
			ImGui::TableNextColumn();
			if (animation.length > 0.0f)
			{
				char overlay[48];
				snprintf(overlay, sizeof(overlay), "%.2f / %.2f s", animation.current, animation.length);
				ImGui::ProgressBar(static_cast<float>((std::min)(1.0, (std::max)(0.0, animation.current / animation.length))), ImVec2(barWidth, 0.0f), overlay);
			}
			else
			{
				ImGui::Text("%.2f s%s", animation.current, animation.hasData ? "" : " (no keyframe data)");
			}
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(SCRIPT_DEBUG::Guid(animation.id.entity).c_str());
			if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
				ImGui::SetTooltip("%s\nstate flags %08X", SCRIPT_DEBUG::Fields(animation.id).c_str(), animation.state);
			ImGui::TableNextColumn();
			// On one line, the instance (which tells the copies of one composite apart) last: the table scrolls sideways for it
			const std::string where = SCRIPT_DEBUG::WhereText(animation.id);
			ImGui::Text("%s (instance %s)", where.empty() ? "the level root" : where.c_str(), SCRIPT_DEBUG::Guid(animation.id.instance).c_str());
			ImGui::PopID();
		}
		ImGui::EndTable();
	}

	const bool g_added = DEBUG_MENU::AddTab("CAGEAnimations", 140, DrawTab, "Scripts");
}
