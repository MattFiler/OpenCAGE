#include "SOUND_DEBUG.h"
#include "DEBUG_MENU.h"
#include "TABLE_ORDER.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>
#include <unordered_map>
#include <vector>

#include <imgui.h>

/*
	The Sound tab and the drawing over the game it switches on, from SOUND_DEBUG's snapshot and what its hooks noted (see
	SOUND_DEBUG.h). Every switch is a named one (DEBUG_MENU::AddSwitch), so OpenCAGE can set them too.
*/
namespace
{
	std::atomic<bool> g_networkOverlay = false, g_playingOverlay = false, g_stimuliOverlay = false, g_routeOverlay = false, g_speechOverlay = false;
	std::atomic<bool> g_allLinks = false;   // the network drawn with every link, not only each node's shortest

	void NameSwitches()
	{
		static bool named = false;
		if (named)
			return;
		named = true;
		DEBUG_MENU::AddSwitch("sound.network", &g_networkOverlay);
		DEBUG_MENU::AddSwitch("sound.network_all_links", &g_allLinks);
		DEBUG_MENU::AddSwitch("sound.playing", &g_playingOverlay);
		DEBUG_MENU::AddSwitch("sound.stimuli", &g_stimuliOverlay);
		DEBUG_MENU::AddSwitch("sound.route", &g_routeOverlay);
		DEBUG_MENU::AddSwitch("sound.speech", &g_speechOverlay);
	}

	bool AnyOverlay()
	{
		return g_networkOverlay || g_playingOverlay || g_stimuliOverlay || g_routeOverlay || g_speechOverlay;
	}

	// What the tab chose: a playing sound, a character's hearing of a stimulus, the test route
	uint32_t g_selectedPlaying = 0;
	uint32_t g_selectedStimulus = 0;
	uint32_t g_selectedHeard = 0;
	bool g_testRoute = false;
	float g_testLoudness = 0.5f;
	char g_playingFilter[64] = "";
	char g_logFilter[64] = "";
	bool g_logPaused = false;

	// The hooks' logs, copied once a frame while wanted
	struct Logs
	{
		int frame = -1;
		std::vector<SOUND_DEBUG::PlayRecord> plays;
		std::vector<SOUND_DEBUG::HeardRecord> heard;
		std::vector<SOUND_DEBUG::Stimulus> stimuli;
		std::vector<SOUND_DEBUG::DoorRecord> doors;
		std::vector<SOUND_DEBUG::DialogueRecord> dialogue;
		std::vector<SOUND_DEBUG::Subtitle> subtitles;
		SOUND_DEBUG::Subtitle subtitle;
	} g_logs;
	std::vector<SOUND_DEBUG::PlayRecord> g_pausedPlays;

	const Logs& CopyLogs()
	{
		const int frame = ImGui::GetFrameCount();
		if (g_logs.frame != frame)
		{
			g_logs.frame = frame;
			SOUND_DEBUG::Hooked(g_logs.plays, g_logs.heard, g_logs.stimuli, g_logs.doors, g_logs.dialogue, g_logs.subtitles, g_logs.subtitle);
		}
		return g_logs;
	}

	float Distance(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dy * dy + dz * dz);
	}

	// A sentence of help, greyed and wrapped to the window
	void Hint(const char* format, ...)
	{
		va_list arguments;
		va_start(arguments, format);
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabledV(format, arguments);
		ImGui::PopTextWrapPos();
		va_end(arguments);
	}

	// The height of a table that scrolls (sideways too) for so many rows of a line each under its header, with room for its
	// sideways scrollbar, and no taller than most: a short list leaves no empty box under it
	float TableHeight(size_t rows, float most)
	{
		const ImGuiStyle& style = ImGui::GetStyle();
		const float row = ImGui::GetFontSize() + style.CellPadding.y * 2.0f;
		return (std::min)(most, row * static_cast<float>(rows + 1) + style.ScrollbarSize + style.CellPadding.y * 2.0f + 2.0f);
	}

	// 1 up to full, falling to 0 at none
	float Fade(float distance, float full, float none)
	{
		return distance <= full ? 1.0f : distance >= none ? 0.0f : (none - distance) / (none - full);
	}

	bool ContainsIgnoringCase(const std::string& text, const char* part)
	{
		if (!part || !part[0])
			return true;
		auto lower = [](std::string s) { for (char& c : s) c = static_cast<char>(tolower(static_cast<unsigned char>(c))); return s; };
		return lower(text).find(lower(part)) != std::string::npos;
	}

	std::string IdName(uint32_t id)
	{
		const std::string name = SOUND_DEBUG::Name(id);
		if (!name.empty())
			return name;
		char text[16];
		snprintf(text, sizeof(text), "#%08X", id);
		return text;
	}

	std::string EventLabel(uint32_t id)
	{
		if (!id)
			return "(none)";
		const std::string name = SOUND_DEBUG::EventName(id);
		return name.empty() ? IdName(id) : name;
	}

	std::string CharacterLabel(const DEBUG_MENU::Character* character)
	{
		return character ? character->name + " (" + DEBUG_MENU::ClassName(character->characterClass) + ")" : std::string("?");
	}

	std::string NetworkName(int network)
	{
		const std::vector<SOUND_DEBUG::Network>& networks = SOUND_DEBUG::Networks();
		if (network < 0 || network >= static_cast<int>(networks.size()))
			return "-";
		return networks[network].name;
	}

	int NodeNetwork(int node)
	{
		const std::vector<SOUND_DEBUG::Node>& nodes = SOUND_DEBUG::Nodes();
		return node >= 0 && node < static_cast<int>(nodes.size()) ? nodes[node].network : -1;
	}

	const SOUND_DEBUG::SoundObject* ObjectById(const SOUND_DEBUG::Snapshot& s, uint32_t id)
	{
		for (const SOUND_DEBUG::SoundObject& object : s.objects)
			if (object.id == id)
				return &object;
		return nullptr;
	}

	std::string OwnerLabel(const SOUND_DEBUG::SoundObject* object)
	{
		if (!object)
			return "(gone)";
		return object->owner.empty() ? std::string("(unnamed object)") : object->owner;
	}

	ImU32 NetworkColour(int network, int alpha)
	{
		// A colour for each network
		return IM_COL32((66 * network) % 255, (network + 55) % 255, (34 * network) % 255, alpha);
	}

	ImU32 DoorwayColour(float state, int alpha = 255)
	{
		if (state <= 0.0f)
			return IM_COL32(60, 220, 90, alpha);
		if (state >= 1.0f)
			return IM_COL32(235, 60, 50, alpha);
		return IM_COL32(240, 170, 40, alpha);
	}

	void Square(ImDrawList* drawList, const float world[3], float half, ImU32 colour, bool filled)
	{
		ImVec2 at;
		if (!DEBUG_MENU::Project(world, at))
			return;
		if (filled)
			drawList->AddRectFilled(ImVec2(at.x - half, at.y - half), ImVec2(at.x + half, at.y + half), colour);
		else
			drawList->AddRect(ImVec2(at.x - half, at.y - half), ImVec2(at.x + half, at.y + half), colour, 0.0f, 0, 1.5f);
	}

	void Dot(ImDrawList* drawList, const float world[3], float radius, ImU32 colour)
	{
		ImVec2 at;
		if (DEBUG_MENU::Project(world, at))
			drawList->AddCircleFilled(at, radius, colour);
	}

	void Raised(const float in[3], float up, float out[3])
	{
		out[0] = in[0];
		out[1] = in[1] + up;
		out[2] = in[2];
	}

	// ---- What a character heard, worked out again (the game's own steps) ----

	struct Recomputed
	{
		bool viaNetwork = false;      // the route through the network was used
		bool unheard = false;         // no way through: the game treats it as not heard
		float straight = 0.0f, distance = 0.0f, intensity = 0.0f;
		int sourceNode = -1;
		SOUND_DEBUG::Route route;
	};

	Recomputed Recompute(const SOUND_DEBUG::HeardRecord& heard)
	{
		Recomputed out;
		out.straight = Distance(heard.position, heard.listenerPosition);
		float distance = out.straight;
		if (!heard.network)
			distance = out.straight * heard.scale;
		else
		{
			float raised[3];
			Raised(heard.position, 0.5f, raised);
			out.sourceNode = heard.sender ? heard.senderNode : SOUND_DEBUG::NearestNode(raised);
			if (heard.listenerNode >= 0 && out.sourceNode >= 0)
			{
				out.viaNetwork = true;
				out.route = SOUND_DEBUG::ShortestRoute(out.sourceNode, heard.listenerNode);
				if (out.route.found && out.route.distance >= 0.0f && out.route.occlusion <= 0.99f)
					distance = (std::max)(out.straight, out.route.distance);
				else
					out.unheard = true;
			}
		}
		out.distance = distance;
		if (out.unheard || heard.maxHearing <= 0.0f)
			out.intensity = 0.0f;
		else
			out.intensity = (std::max)(0.0f, (1.0f - distance / heard.maxHearing) - (1.0f - heard.loudness));
		return out;
	}

	// The test route: from the player (as a stimulus they make) to the selected character's hearing
	struct TestRoute
	{
		bool have = false;
		const DEBUG_MENU::Character* player = nullptr;
		const DEBUG_MENU::Character* listener = nullptr;
		SOUND_DEBUG::Route route;
		float straight = 0.0f;
	};

	TestRoute MakeTestRoute()
	{
		TestRoute test;
		if (!g_testRoute)
			return test;
		for (const DEBUG_MENU::Character& c : DEBUG_MENU::Characters())
			if (c.player)
				test.player = &c;
		test.listener = DEBUG_MENU::Selected();
		if (!test.player || !test.listener || test.listener == test.player)
			return test;
		const int from = SOUND_DEBUG::CharacterNode(test.player->object);
		const int to = SOUND_DEBUG::CharacterNode(test.listener->object);
		test.straight = Distance(test.player->position, test.listener->position);
		if (from >= 0 && to >= 0)
			test.route = SOUND_DEBUG::ShortestRoute(from, to);
		test.have = true;
		return test;
	}

	// ---- Overlays ----

	void DrawRoute(ImDrawList* drawList, const SOUND_DEBUG::Route& route, const float* from, const float* to, ImU32 colour, const char* label)
	{
		const std::vector<SOUND_DEBUG::Node>& nodes = SOUND_DEBUG::Nodes();
		const std::vector<SOUND_DEBUG::Barrier>& barriers = SOUND_DEBUG::Barriers();
		std::vector<const float*> points;
		if (from)
			points.push_back(from);
		for (int n : route.points)
			if (n >= 0 && n < static_cast<int>(nodes.size()))
				points.push_back(nodes[n].position);
		if (to)
			points.push_back(to);
		for (size_t i = 1; i < points.size(); i++)
			DEBUG_MENU::WorldLine(drawList, points[i - 1], points[i], colour, 3.0f);
		for (int b : route.barriers)
			if (b >= 0 && b < static_cast<int>(barriers.size()))
				Square(drawList, barriers[b].position, 6.0f * DEBUG_MENU::Scale(), DoorwayColour(barriers[b].state), true);
		if (label && to)
		{
			float above[3];
			Raised(to, 0.6f, above);
			SOUND_DEBUG::Label(drawList, above, label, colour);
		}
	}

	const char* DoorwayStateName(float state)
	{
		return state <= 0.0f ? "open" : state >= 1.0f ? "closed" : "part open";
	}

	void DrawNetwork(ImDrawList* drawList, const SOUND_DEBUG::Snapshot& s)
	{
		const std::vector<SOUND_DEBUG::Node>& nodes = SOUND_DEBUG::Nodes();
		const std::vector<SOUND_DEBUG::Network>& networks = SOUND_DEBUG::Networks();
		const std::vector<SOUND_DEBUG::Barrier>& barriers = SOUND_DEBUG::Barriers();
		float camera[3];
		if (!DEBUG_MENU::CameraPosition(camera))
			return;
		// Nodes and doorways within 30 m of the camera. A network links most of its nodes to each other, so every link makes
		// a dense web: unless all are asked for, each node shows only its three shortest links (the room's shape), and links
		// show within 20 m, fading out from 8 m. Doorways within 20 m are named.
		constexpr float kRange = 30.0f, kLinkRange = 20.0f, kLinkFade = 8.0f, kDoorwayLabelRange = 20.0f;
		constexpr int kNearestLinks = 3;
		const float scale = DEBUG_MENU::Scale();
		static std::vector<float> away;     // each node's distance from the camera
		away.resize(nodes.size());
		std::vector<bool> networkShown(networks.size(), false);
		for (size_t i = 0; i < nodes.size(); i++)
			away[i] = Distance(nodes[i].position, camera);

		struct Edge { int a, b, obstruction; };
		static std::vector<Edge> edges;
		edges.clear();
		if (g_allLinks)
		{
			for (size_t i = 0; i < nodes.size(); i++)
				if (away[i] <= kLinkRange)
					for (const SOUND_DEBUG::Node::Link& link : nodes[i].links)
						if (link.other < nodes.size() && away[link.other] <= kLinkRange)
							edges.push_back({ static_cast<int>(i), static_cast<int>(link.other), link.obstruction });
		}
		else
		{
			// Each node's shortest links (by the game's distance in metres), from both ends as the game stores each pair once
			struct Nearest { int other[kNearestLinks]; int metres[kNearestLinks]; int obstruction[kNearestLinks]; int count; };
			static std::vector<int> slot;   // a node's place in closest (-1 out of range)
			static std::vector<Nearest> closest;
			slot.assign(nodes.size(), -1);
			closest.clear();
			for (size_t i = 0; i < nodes.size(); i++)
				if (away[i] <= kLinkRange)
				{
					slot[i] = static_cast<int>(closest.size());
					closest.push_back(Nearest{ {}, {}, {}, 0 });
				}
			auto offer = [](Nearest& n, int other, int metres, int obstruction)
			{
				int at = n.count;
				while (at > 0 && n.metres[at - 1] > metres)
					at--;
				if (at >= kNearestLinks)
					return;
				for (int k = n.count < kNearestLinks ? n.count : kNearestLinks - 1; k > at; k--)
				{
					n.other[k] = n.other[k - 1];
					n.metres[k] = n.metres[k - 1];
					n.obstruction[k] = n.obstruction[k - 1];
				}
				n.other[at] = other;
				n.metres[at] = metres;
				n.obstruction[at] = obstruction;
				if (n.count < kNearestLinks)
					n.count++;
			};
			for (size_t i = 0; i < nodes.size(); i++)
			{
				if (slot[i] < 0)
					continue;
				for (const SOUND_DEBUG::Node::Link& link : nodes[i].links)
					if (link.other < nodes.size() && slot[link.other] >= 0)
					{
						offer(closest[slot[i]], link.other, link.distance, link.obstruction);
						offer(closest[slot[link.other]], static_cast<int>(i), link.distance, link.obstruction);
					}
			}
			for (size_t i = 0; i < nodes.size(); i++)
				if (slot[i] >= 0)
				{
					const Nearest& n = closest[slot[i]];
					for (int k = 0; k < n.count; k++)
						edges.push_back({ (std::min)(static_cast<int>(i), n.other[k]), (std::max)(static_cast<int>(i), n.other[k]), n.obstruction[k] });
				}
			std::sort(edges.begin(), edges.end(), [](const Edge& x, const Edge& y) { return x.a != y.a ? x.a < y.a : x.b < y.b; });
			edges.erase(std::unique(edges.begin(), edges.end(), [](const Edge& x, const Edge& y) { return x.a == y.a && x.b == y.b; }), edges.end());
		}
		// Links first, then the nodes over them: brighter in the listener's network (less so the more obstructed), faint in others
		for (const Edge& edge : edges)
		{
			const bool listening = nodes[edge.a].network >= 0 && nodes[edge.a].network == s.listenerNetwork;
			const float base = listening ? static_cast<float>((std::max)(50, 160 - edge.obstruction / 2)) : 70.0f;
			const int alpha = static_cast<int>(base * Fade((std::max)(away[edge.a], away[edge.b]), kLinkFade, kLinkRange));
			if (alpha >= 4)
				DEBUG_MENU::WorldLine(drawList, nodes[edge.a].position, nodes[edge.b].position, IM_COL32(170, 170, 190, alpha), 1.0f);
		}
		for (size_t i = 0; i < nodes.size(); i++)
		{
			if (away[i] > kRange)
				continue;
			const int network = nodes[i].network;
			const bool listening = network >= 0 && network == s.listenerNetwork;
			Dot(drawList, nodes[i].position, (listening ? 4.0f : 2.5f) * scale, NetworkColour((std::max)(network, 0), listening ? 255 : 150));
			if (network >= 0)
				networkShown[network] = true;
		}
		for (int l = 0; l < 2; l++)
		{
			const int n = s.listenerNodes[l];
			ImVec2 at;
			if (n >= 0 && n < static_cast<int>(nodes.size()) && DEBUG_MENU::Project(nodes[n].position, at))
				drawList->AddCircle(at, (l == 0 ? 9.0f : 7.0f) * scale, IM_COL32(255, 255, 255, 220), 0, 1.5f);
		}
		// Doorways: a square in the state's colour with a white outline (nodes are round), and near ones named with their
		// number (as the tab's Doorways list and sound.list_doorways number them) and state
		for (size_t b = 0; b < barriers.size(); b++)
		{
			const SOUND_DEBUG::Barrier& barrier = barriers[b];
			const float distance = Distance(barrier.position, camera);
			if (distance > kRange)
				continue;
			const bool linked = s.listenerNetwork >= 0 && (barrier.networks[0] == s.listenerNetwork || barrier.networks[1] == s.listenerNetwork);
			const float half = (linked ? 6.0f : 4.5f) * scale;
			Square(drawList, barrier.position, half, DoorwayColour(barrier.state), true);
			Square(drawList, barrier.position, half + 1.5f, IM_COL32(255, 255, 255, 160), false);
			if (static_cast<int>(b) == s.activeBarrier)
				Square(drawList, barrier.position, 9.0f * scale, IM_COL32(255, 255, 255, 220), false);
			if (distance > kDoorwayLabelRange)
				continue;
			char text[64];
			if (barrier.state > 0.0f && barrier.state < 1.0f)
			{
				// Rounded, but never to 0% or 100% while it is part open
				const int closed = (std::min)(99, (std::max)(1, static_cast<int>(barrier.state * 100.0f + 0.5f)));
				snprintf(text, sizeof(text), "doorway %d: %d%% closed", static_cast<int>(b), closed);
			}
			else
				snprintf(text, sizeof(text), "doorway %d: %s%s", static_cast<int>(b), DoorwayStateName(barrier.state), barrier.timesSet ? "" : " (never set)");
			const int alpha = static_cast<int>(255.0f * (0.35f + 0.65f * Fade(distance, 10.0f, kDoorwayLabelRange)));
			SOUND_DEBUG::Label(drawList, barrier.position, text, DoorwayColour(barrier.state, alpha), -10.0f * scale);
		}
		for (size_t n = 0; n < networks.size(); n++)
		{
			if (!networkShown[n])
				continue;
			const bool listening = static_cast<int>(n) == s.listenerNetwork;
			const bool sealed = networks[n].barriers.empty();
			int alpha = listening ? 255 : 170;
			if (sealed)
				alpha = static_cast<int>(130 + 120 * (0.5f + 0.5f * std::sin(static_cast<float>(ImGui::GetTime()) * 4.0f)));
			std::string label = networks[n].name;
			if (sealed)
				label += " (no doorways)";
			SOUND_DEBUG::Label(drawList, networks[n].centre, label.c_str(), NetworkColour(static_cast<int>(n), alpha) | IM_COL32(60, 60, 60, 0));
		}
	}

	void DrawPlaying(ImDrawList* drawList, const SOUND_DEBUG::Snapshot& s)
	{
		float camera[3];
		if (!DEBUG_MENU::CameraPosition(camera))
			return;
		const float scale = DEBUG_MENU::Scale();
		// One label per sound object, with what it plays: each event once (with how many when it plays more than once), at
		// most two named
		std::vector<std::vector<std::pair<uint32_t, int>>> events(s.objects.size());
		std::vector<bool> selected(s.objects.size(), false);
		for (const SOUND_DEBUG::Playing& playing : s.playing)
		{
			if (playing.object < 0)
				continue;
			std::vector<std::pair<uint32_t, int>>& list = events[playing.object];
			const auto found = std::find_if(list.begin(), list.end(), [&](const std::pair<uint32_t, int>& e) { return e.first == playing.eventId; });
			if (found != list.end())
				found->second++;
			else
				list.push_back({ playing.eventId, 1 });
			if (playing.playingId == g_selectedPlaying)
				selected[playing.object] = true;
		}
		std::vector<std::string> labels(s.objects.size());
		for (size_t i = 0; i < events.size(); i++)
		{
			std::string& label = labels[i];
			for (size_t e = 0; e < events[i].size() && e < 2; e++)
			{
				label += (e ? ", " : "") + EventLabel(events[i][e].first);
				if (events[i][e].second > 1)
					label += " x" + std::to_string(events[i][e].second);
			}
			if (events[i].size() > 2)
				label += ", +" + std::to_string(events[i].size() - 2) + " more";
		}
		for (size_t i = 0; i < s.objects.size(); i++)
		{
			const SOUND_DEBUG::SoundObject& object = s.objects[i];
			if (labels[i].empty() || (object.status & 4) || Distance(object.position, camera) > 60.0f)
				continue;
			const bool occluded = (object.status & 0x10) != 0, obstructed = (object.status & 0x20) != 0;
			const int alpha = occluded ? 110 : 255;
			const ImU32 colour = selected[i] ? IM_COL32(255, 255, 120, 255) : IM_COL32(120, 210, 255, alpha);
			ImVec2 at;
			if (DEBUG_MENU::Project(object.position, at))
			{
				drawList->AddCircleFilled(at, 4.0f * scale, colour);
				if (obstructed)
					drawList->AddCircle(at, 7.0f * scale, IM_COL32(240, 170, 40, alpha), 0, 1.5f);
			}
			char text[256];
			snprintf(text, sizeof(text), "%s%s%s, %.1f m%s", labels[i].c_str(), object.owner.empty() ? "" : " - ", object.owner.c_str(),
				Distance(object.position, s.listener), occluded ? ", occluded" : "");
			SOUND_DEBUG::Label(drawList, object.position, text, colour, -14.0f * scale);
		}
		// The selected sound: how far it can be heard, and the doorways the game routes it through to the listener
		for (const SOUND_DEBUG::Playing& playing : s.playing)
		{
			if (playing.playingId != g_selectedPlaying || playing.object < 0)
				continue;
			const SOUND_DEBUG::SoundObject& object = s.objects[playing.object];
			const float radius = SOUND_DEBUG::EventMaxDistance(playing.eventId);
			if (radius > 0.0f)
				DEBUG_MENU::WorldCircle(drawList, object.position, radius, IM_COL32(255, 255, 120, 140), 48, 1.0f);
			const int from = s.listenerNetwork, to = NodeNetwork(object.node);
			const std::vector<SOUND_DEBUG::Network>& networks = SOUND_DEBUG::Networks();
			const std::vector<SOUND_DEBUG::Barrier>& barriers = SOUND_DEBUG::Barriers();
			if (from >= 0 && to >= 0 && from != to)
			{
				const int low = (std::min)(from, to), high = (std::max)(from, to);
				for (const SOUND_DEBUG::Network::Path& path : networks[low].paths)
				{
					if (path.to != high)
						continue;
					std::vector<const float*> points;
					points.push_back(s.listener);
					std::vector<int> order = path.barriers;
					if (from != low)
						std::reverse(order.begin(), order.end());
					for (int b : order)
						if (b >= 0 && b < static_cast<int>(barriers.size()))
							points.push_back(barriers[b].position);
					points.push_back(object.position);
					for (size_t p = 1; p < points.size(); p++)
						DEBUG_MENU::WorldLine(drawList, points[p - 1], points[p], IM_COL32(255, 255, 120, 220), 2.0f);
					for (int b : order)
						if (b >= 0 && b < static_cast<int>(barriers.size()))
							Square(drawList, barriers[b].position, 6.0f * scale, DoorwayColour(barriers[b].state), true);
				}
			}
		}
	}

	void DrawStimuli(ImDrawList* drawList, const SOUND_DEBUG::Snapshot& s, const Logs& logs)
	{
		const float scale = DEBUG_MENU::Scale();
		for (const SOUND_DEBUG::Stimulus& stimulus : logs.stimuli)
		{
			const float age = s.now - stimulus.time;
			if (age < 0.0f || age > 3.0f)
				continue;
			const int alpha = static_cast<int>(255 * (1.0f - age / 3.0f));
			const ImU32 colour = IM_COL32(255, 140, 220, alpha);
			DEBUG_MENU::WorldCircle(drawList, stimulus.position, 0.5f + stimulus.loudness * 4.5f, colour, 32, 2.0f);
			char text[96];
			snprintf(text, sizeof(text), "%s %.2f", SOUND_DEBUG::StimulusName(stimulus.type), stimulus.loudness);
			SOUND_DEBUG::Label(drawList, stimulus.position, text, colour, -12.0f * scale);
			for (const SOUND_DEBUG::HeardRecord& heard : logs.heard)
			{
				if (heard.stimulus != stimulus.serial || heard.result <= 0.0f)
					continue;
				float to[3];
				Raised(heard.listenerPosition, 1.5f, to);
				DEBUG_MENU::WorldLine(drawList, stimulus.position, to, colour, 1.0f + heard.result * 4.0f);
				const float middle[3] = { (stimulus.position[0] + to[0]) * 0.5f, (stimulus.position[1] + to[1]) * 0.5f, (stimulus.position[2] + to[2]) * 0.5f };
				snprintf(text, sizeof(text), "heard %.2f", heard.result);
				SOUND_DEBUG::Label(drawList, middle, text, colour);
			}
		}
	}

	void DrawRoutes(ImDrawList* drawList, const Logs& logs)
	{
		for (const SOUND_DEBUG::HeardRecord& heard : logs.heard)
		{
			if (heard.serial != g_selectedHeard)
				continue;
			const Recomputed again = Recompute(heard);
			char text[192];
			if (again.viaNetwork && !again.unheard)
				snprintf(text, sizeof(text), "occlusion %.2f, obstruction %.0f, distance %.1f m, direct %.1f m", again.route.occlusion, again.route.obstruction,
					again.route.distance, again.straight);
			else
				snprintf(text, sizeof(text), again.unheard ? "no way through: not heard" : "direct %.1f m", again.straight);
			float to[3];
			Raised(heard.listenerPosition, 1.5f, to);
			DrawRoute(drawList, again.route, heard.position, to, IM_COL32(255, 220, 60, 230), text);
		}
		const TestRoute test = MakeTestRoute();
		if (test.have)
		{
			char text[192];
			if (test.route.found)
				snprintf(text, sizeof(text), "occlusion %.2f, obstruction %.0f, distance %.1f m, direct %.1f m", test.route.occlusion, test.route.obstruction,
					test.route.distance, test.straight);
			else
				snprintf(text, sizeof(text), "no way through (direct %.1f m)", test.straight);
			float from[3], to[3];
			Raised(test.player->position, 0.5f, from);
			Raised(test.listener->position, 1.5f, to);
			DrawRoute(drawList, test.route, from, to, IM_COL32(120, 255, 200, 230), text);
		}
	}

	void DrawSpeech(ImDrawList* drawList, const SOUND_DEBUG::Snapshot& s, const Logs& logs)
	{
		const float scale = DEBUG_MENU::Scale();
		for (const SOUND_DEBUG::Speech& line : s.speech)
		{
			if (line.status != 4)
				continue;
			const DEBUG_MENU::Character* speaker = SOUND_DEBUG::CharacterByObject(line.character);
			if (!speaker)
				speaker = SOUND_DEBUG::CharacterBySoundObject(line.objectId);
			if (!speaker)
				continue;
			float above[3];
			Raised(speaker->position, 2.2f, above);
			std::string text = EventLabel(line.eventId);
			if (!logs.subtitle.key.empty() && logs.subtitle.originId == line.objectId)
				text += "\n\"" + (logs.subtitle.text.empty() ? logs.subtitle.key : logs.subtitle.text) + "\"";
			SOUND_DEBUG::Label(drawList, above, text.c_str(), IM_COL32(255, 235, 170, 255), -8.0f * scale);
		}
	}

	void DrawOverlay(ImDrawList* drawList)
	{
		NameSwitches();
		if (!AnyOverlay())
			return;
		const SOUND_DEBUG::Snapshot& s = SOUND_DEBUG::Read();
		if (!s.have)
			return;
		const Logs& logs = CopyLogs();
		if (g_networkOverlay && s.haveNetwork)
			DrawNetwork(drawList, s);
		if (g_playingOverlay)
			DrawPlaying(drawList, s);
		if (g_stimuliOverlay)
			DrawStimuli(drawList, s, logs);
		if (g_routeOverlay && s.haveNetwork)
			DrawRoutes(drawList, logs);
		if (g_speechOverlay)
			DrawSpeech(drawList, s, logs);
	}

	// ---- The tab ----

	void SwitchBox(const char* label, std::atomic<bool>& on)
	{
		bool value = on;
		if (ImGui::Checkbox(label, &value))
			on = value;
	}

	const char* AlienStateName(int state)
	{
		switch (state)
		{
		case 0: return "not in the level";
		case 1: return "in the level";
		case 2: return "backstage (in the vents)";
		default: return "?";
		}
	}

	void DrawOverview(const SOUND_DEBUG::Snapshot& s)
	{
		const std::vector<SOUND_DEBUG::Network>& networks = SOUND_DEBUG::Networks();
		ImGui::Text("Sound %s%s; game time %.1f s", s.enabled ? "on" : "off", s.paused ? ", paused" : "", s.now);
		// Where the listener is held (the Tools section below can hold it still or at the player instead of the camera; with no
		// player, "at the player" falls back to frozen when that is on too, else the camera)
		bool frozen = false, atPlayer = false, havePlayer = false;
		DEBUG_MENU::GetSwitch("sound.listener_frozen", frozen);
		DEBUG_MENU::GetSwitch("sound.listener_at_player", atPlayer);
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.player)
				havePlayer = true;
		ImGui::TextWrapped("Listener: %.2f, %.2f, %.2f (%s; occlusion is heard from %s)", s.listener[0], s.listener[1], s.listener[2],
			atPlayer && havePlayer ? "at the player" : frozen ? "frozen" : "the camera", s.listenerIsPlayerHead ? "the player's head" : "the camera");
		if (s.haveNetwork && s.listenerNetwork >= 0 && s.listenerNetwork < static_cast<int>(networks.size()))
		{
			const SOUND_DEBUG::Network& network = networks[s.listenerNetwork];
			const std::vector<uint32_t>& reverbs = SOUND_DEBUG::Reverbs();
			const std::string reverb = network.reverb >= 0 && network.reverb < static_cast<int>(reverbs.size()) ? IdName(reverbs[network.reverb]) : std::string("none");
			ImGui::TextWrapped("Space: %s (network %d, nodes %d and %d)", network.name.c_str(), s.listenerNetwork, s.listenerNodes[0], s.listenerNodes[1]);
			ImGui::TextWrapped("Room type %s, reverb %s", IdName(s.roomType).c_str(), reverb.c_str());
			// The blend is only set near a doorway (below 0 until then: the ambience plays as it is)
			if (!network.enterEvent)
				ImGui::Text("No ambience in this space");
			else if (network.ambienceBlend >= 0.0f)
				ImGui::TextWrapped("Ambience %s: %s, doorway blend %.2f (of 1)", EventLabel(network.enterEvent).c_str(), network.ambienceActive ? "playing" : "not playing",
					network.ambienceBlend);
			else
				ImGui::TextWrapped("Ambience %s: %s", EventLabel(network.enterEvent).c_str(), network.ambienceActive ? "playing" : "not playing");
			if (s.activeBarrier >= 0)
				ImGui::Text("Crossfading through doorway %d", s.activeBarrier);
		}
		else if (!s.haveNetwork)
			ImGui::TextDisabled("No sound network read for this level.");
		else
			Hint("The listener is in no network (more than 10 m from every node).");
		bool occlusion = s.occlusion, reverb = s.reverb;
		ImGui::BeginDisabled(!s.haveNetwork);
		if (ImGui::Checkbox("Occlusion", &occlusion))
			SOUND_DEBUG::SetOcclusion(occlusion);
		ImGui::SameLine();
		if (ImGui::Checkbox("Reverb", &reverb))
			SOUND_DEBUG::SetReverb(reverb);
		ImGui::EndDisabled();
		ImGui::SameLine();
		Hint("(the game's own debug switches: what blocks sound through closed doorways, and the rooms' reverb)");
		if (s.alienDistance >= 0.0f)
			ImGui::TextWrapped("Alien: %s, %.1f m from the player through the network", AlienStateName(s.alienState), s.alienDistance);
		else
			ImGui::Text("Alien: %s", AlienStateName(s.alienState));
		ImGui::Text("Threat level %.2f, stealth level %.2f", s.threat, s.stealth);
		int queued = 0, speaking = 0;
		for (const SOUND_DEBUG::Speech& line : s.speech)
			(line.status == 4 ? speaking : queued)++;
		ImGui::TextWrapped("%d sound objects, %d sounds playing, %d lines speaking and %d waiting", static_cast<int>(s.objects.size()),
			static_cast<int>(s.playing.size()), speaking, queued);
		if (s.lockMisses)
			Hint("The game's sound lock was busy on %d frames (the last copy shown).", s.lockMisses);
	}

	void DrawPlayingTable(const SOUND_DEBUG::Snapshot& s)
	{
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##playingfilter", "Filter (event or owner)", g_playingFilter, sizeof(g_playingFilter));
		ImGui::SameLine();
		SwitchBox("Draw them", g_playingOverlay);
		struct Row { const SOUND_DEBUG::Playing* playing; const SOUND_DEBUG::SoundObject* object; std::string event, owner; float distance; };
		std::vector<Row> rows;
		for (const SOUND_DEBUG::Playing& playing : s.playing)
		{
			Row row{ &playing, playing.object >= 0 ? &s.objects[playing.object] : nullptr, EventLabel(playing.eventId), "", FLT_MAX };
			row.owner = playing.objectId == 0xFFFFFFFF ? std::string("Global") : OwnerLabel(row.object);
			if (row.object && !(row.object->status & 4))
				row.distance = Distance(row.object->position, s.listener);
			if (!ContainsIgnoringCase(row.event, g_playingFilter) && !ContainsIgnoringCase(row.owner, g_playingFilter))
				continue;
			rows.push_back(std::move(row));
		}
		TABLE_ORDER::Nearest("sound.playing", rows, [](const Row& row) { return static_cast<uint64_t>(row.playing->playingId); }, [](const Row& row) { return row.distance; });
		if (rows.empty())
		{
			ImGui::TextDisabled("Nothing playing%s.", g_playingFilter[0] ? " that matches" : "");
			return;
		}
		// Scrolls sideways rather than squeezing the last columns when the names are long: the event, how far away and the room
		// first, so they show without scrolling, and the event kept in view while the rest scroll
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY |
			ImGuiTableFlags_SizingFixedFit;
		if (!ImGui::BeginTable("playing", 7, flags, ImVec2(0.0f, TableHeight(rows.size(), 260.0f * DEBUG_MENU::Scale()))))
			return;
		ImGui::TableSetupScrollFreeze(1, 1);
		ImGui::TableSetupColumn("Event", ImGuiTableColumnFlags_NoHide);
		ImGui::TableSetupColumn("Distance");
		ImGui::TableSetupColumn("Room");
		ImGui::TableSetupColumn("Owner");
		ImGui::TableSetupColumn("Heard to");
		ImGui::TableSetupColumn("Voices");
		ImGui::TableSetupColumn("Age");
		ImGui::TableHeadersRow();
		for (const Row& row : rows)
		{
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::PushID(static_cast<int>(row.playing->playingId));
			const bool selected = row.playing->playingId == g_selectedPlaying;
			if (ImGui::Selectable(row.event.c_str(), selected, ImGuiSelectableFlags_SpanAllColumns))
				g_selectedPlaying = selected ? 0 : row.playing->playingId;
			ImGui::PopID();
			// In the order of the columns above: distance, room, owner, heard to, voices, age
			ImGui::TableNextColumn();
			if (!row.object)
				ImGui::TextDisabled("-");
			else if (row.object->status & 4)
				ImGui::TextDisabled("not in the world");
			else
				ImGui::Text("%.1f m%s%s", row.distance, (row.object->status & 0x10) ? ", occluded" : "", (row.object->status & 0x20) ? ", obstructed" : "");
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(row.object ? NetworkName(NodeNetwork(row.object->node)).c_str() : "-");
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(row.owner.c_str());
			ImGui::TableNextColumn();
			const float radius = SOUND_DEBUG::EventMaxDistance(row.playing->eventId);
			if (radius > 0.0f)
				ImGui::Text("%.0f m", radius);
			else
				ImGui::TextDisabled("-");
			ImGui::TableNextColumn();
			ImGui::Text("%d", row.playing->voices);
			ImGui::TableNextColumn();
			if (row.playing->startedAt >= 0.0f)
				ImGui::Text("%.1f s", s.now - row.playing->startedAt);
			else
				ImGui::TextDisabled("-");
		}
		ImGui::EndTable();
		Hint("Click one to draw how far it is heard and the doorways the game takes it through to the listener (with Draw them on).");
	}

	void DrawEventLog(const SOUND_DEBUG::Snapshot& s, const Logs& logs)
	{
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##logfilter", "Filter (event, owner or argument)", g_logFilter, sizeof(g_logFilter));
		ImGui::SameLine();
		if (ImGui::Checkbox("Pause", &g_logPaused) && g_logPaused)
			g_pausedPlays = logs.plays;
		ImGui::SameLine();
		if (ImGui::Button("Clear the logs"))
		{
			SOUND_DEBUG::ClearLogs();
			g_pausedPlays.clear();
		}
		const std::vector<SOUND_DEBUG::PlayRecord>& plays = g_logPaused ? g_pausedPlays : logs.plays;
		if (plays.empty())
		{
			ImGui::TextDisabled("Nothing played yet.");
			return;
		}
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY |
			ImGuiTableFlags_SizingFixedFit;
		if (!ImGui::BeginTable("eventlog", 5, flags, ImVec2(0.0f, 260.0f * DEBUG_MENU::Scale())))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Age");
		ImGui::TableSetupColumn("Event");
		ImGui::TableSetupColumn("Played on");
		ImGui::TableSetupColumn("Where");
		ImGui::TableSetupColumn("Arguments");
		ImGui::TableHeadersRow();
		std::unordered_map<uint32_t, const SOUND_DEBUG::SoundObject*> objects;
		for (const SOUND_DEBUG::SoundObject& object : s.objects)
			objects[object.id] = &object;
		for (const SOUND_DEBUG::PlayRecord& play : plays)
		{
			const std::string event = EventLabel(play.eventId);
			std::string owner;
			if (play.scriptEntity)
			{
				char text[64];
				snprintf(text, sizeof(text), "script entity 0x%08X", play.scriptEntity);
				owner = text;
			}
			else if (!play.object)
				owner = "short-lived object";
			else
			{
				const auto object = objects.find(play.objectId);
				owner = OwnerLabel(object == objects.end() ? nullptr : object->second);
			}
			if (!ContainsIgnoringCase(event, g_logFilter) && !ContainsIgnoringCase(owner, g_logFilter) && !ContainsIgnoringCase(play.arguments, g_logFilter))
				continue;
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("%.1f s", s.now - play.time);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(event.c_str());
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(owner.c_str());
			if (play.scriptEntity && ImGui::IsItemHovered())
				ImGui::SetTooltip("A script Sound entity, id 0x%08X in the composite instance with id 0x%08X (as OpenCAGE shows them).", play.scriptEntity, play.scriptInstance);
			ImGui::TableNextColumn();
			if (play.havePosition)
				ImGui::Text("%.1f m away", Distance(play.position, s.listener));
			else
				ImGui::TextDisabled("-");
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(play.arguments.c_str());
		}
		ImGui::EndTable();
	}

	void DrawSpeechSection(const SOUND_DEBUG::Snapshot& s, const Logs& logs)
	{
		SwitchBox("Draw what each speaking character says", g_speechOverlay);
		if (!logs.subtitle.key.empty())
		{
			const DEBUG_MENU::Character* speaker = SOUND_DEBUG::CharacterBySoundObject(logs.subtitle.originId);
			ImGui::TextWrapped("Subtitle: \"%s\" (%s, key %s)", logs.subtitle.text.empty() ? "(no text)" : logs.subtitle.text.c_str(),
				speaker ? CharacterLabel(speaker).c_str() : "speaker not a character", logs.subtitle.key.c_str());
		}
		else
			ImGui::TextDisabled("No subtitle on screen.");
		if (s.speech.empty())
			ImGui::TextDisabled("No speech queued or playing.");
		else if (ImGui::BeginTable("speech", 6, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY |
			ImGuiTableFlags_SizingFixedFit, ImVec2(0.0f, TableHeight(s.speech.size(), 200.0f * DEBUG_MENU::Scale()))))
		{
			ImGui::TableSetupScrollFreeze(0, 1);
			ImGui::TableSetupColumn("Status");
			ImGui::TableSetupColumn("Priority");
			ImGui::TableSetupColumn("Line");
			ImGui::TableSetupColumn("Speaker");
			ImGui::TableSetupColumn("Gives up in");
			ImGui::TableSetupColumn("Arguments");
			ImGui::TableHeadersRow();
			for (const SOUND_DEBUG::Speech& line : s.speech)
			{
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("%s%s%s", SOUND_DEBUG::SpeechStatusName(line.status), line.inSlot ? " (in a slot)" : "",
					line.subStatus == 1 ? ", radio squawk" : line.subStatus == 2 ? ", closing squawk" : "");
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(SOUND_DEBUG::SpeechTypeName(line.type));
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(EventLabel(line.eventId).c_str());
				ImGui::TableNextColumn();
				const DEBUG_MENU::Character* speaker = SOUND_DEBUG::CharacterByObject(line.character);
				ImGui::TextUnformatted(speaker ? CharacterLabel(speaker).c_str() : OwnerLabel(ObjectById(s, line.objectId)).c_str());
				ImGui::TableNextColumn();
				if (line.status < 4 && line.timeout > s.now)
					ImGui::Text("%.1f s", line.timeout - s.now);
				else
					ImGui::TextDisabled("-");
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(line.arguments.c_str());
			}
			ImGui::EndTable();
		}
		if (ImGui::TreeNode("Subtitles shown"))
		{
			if (logs.subtitles.empty())
				ImGui::TextDisabled("None yet.");
			for (const SOUND_DEBUG::Subtitle& subtitle : logs.subtitles)
				ImGui::TextWrapped("%.1f s ago: \"%s\" (%s)", s.now - subtitle.time, subtitle.text.empty() ? subtitle.key.c_str() : subtitle.text.c_str(), subtitle.key.c_str());
			ImGui::TreePop();
		}
		if (ImGui::TreeNode("NPC dynamic dialogue"))
		{
			Hint("What the squads asked their characters to say (they may not have been allowed to).");
			if (logs.dialogue.empty())
				ImGui::TextDisabled("None yet.");
			for (const SOUND_DEBUG::DialogueRecord& dialogue : logs.dialogue)
				ImGui::Text("%.1f s ago: %s - %s (%s)", s.now - dialogue.time, CharacterLabel(SOUND_DEBUG::CharacterByObject(dialogue.character)).c_str(),
					SOUND_DEBUG::DialogueEventName(dialogue.dialogueEvent), SOUND_DEBUG::DialogueStateName(dialogue.dialogueState));
			ImGui::TreePop();
		}
	}

	void DrawHearing(const SOUND_DEBUG::Snapshot& s, const Logs& logs)
	{
		SwitchBox("Draw the stimuli and who heard them", g_stimuliOverlay);
		ImGui::SameLine();
		SwitchBox("Draw the selected route", g_routeOverlay);
		Hint("A character hears a stimulus at (1 - distance / its hearing distance) - (1 - loudness), nothing below 0; the distance is along the shortest route through the network's open doorways.");
		if (logs.stimuli.empty())
			Hint("No stimuli yet: footsteps, gunfire and impacts appear here.");
		else if (ImGui::BeginTable("stimuli", 5, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY |
			ImGuiTableFlags_SizingFixedFit, ImVec2(0.0f, 180.0f * DEBUG_MENU::Scale())))
		{
			ImGui::TableSetupScrollFreeze(0, 1);
			ImGui::TableSetupColumn("Age");
			ImGui::TableSetupColumn("Stimulus");
			ImGui::TableSetupColumn("Loudness");
			ImGui::TableSetupColumn("Made by");
			ImGui::TableSetupColumn("Heard by");
			ImGui::TableHeadersRow();
			for (const SOUND_DEBUG::Stimulus& stimulus : logs.stimuli)
			{
				int heard = 0, checked = 0;
				for (const SOUND_DEBUG::HeardRecord& record : logs.heard)
					if (record.stimulus == stimulus.serial)
					{
						checked++;
						heard += record.result > 0.0f ? 1 : 0;
					}
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::PushID(static_cast<int>(stimulus.serial));
				char age[32];
				snprintf(age, sizeof(age), "%.1f s", s.now - stimulus.time);
				const bool selected = stimulus.serial == g_selectedStimulus;
				if (ImGui::Selectable(age, selected, ImGuiSelectableFlags_SpanAllColumns))
					g_selectedStimulus = selected ? 0 : stimulus.serial;
				ImGui::PopID();
				ImGui::TableNextColumn();
				ImGui::Text("%s%s", SOUND_DEBUG::StimulusName(stimulus.type), stimulus.motionTracker ? " (motion tracker)" : "");
				ImGui::TableNextColumn();
				ImGui::Text("%.2f", stimulus.loudness);
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(CharacterLabel(SOUND_DEBUG::CharacterByHandle(stimulus.senderEntity, stimulus.senderInstance)).c_str());
				ImGui::TableNextColumn();
				ImGui::Text("%d of %d", heard, checked);
			}
			ImGui::EndTable();
		}

		// Who worked out how loud the selected stimulus was, and the same worked out again here
		if (g_selectedStimulus)
		{
			ImGui::SeparatorText("Who checked it");
			size_t checked = 0;
			for (const SOUND_DEBUG::HeardRecord& record : logs.heard)
				checked += record.stimulus == g_selectedStimulus ? 1 : 0;
			const bool any = checked > 0;
			if (any && ImGui::BeginTable("heard", 6, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY |
				ImGuiTableFlags_SizingFixedFit, ImVec2(0.0f, TableHeight(checked, 200.0f * DEBUG_MENU::Scale()))))
			{
				ImGui::TableSetupScrollFreeze(0, 1);
				ImGui::TableSetupColumn("Character");
				ImGui::TableSetupColumn("Heard");
				ImGui::TableSetupColumn("Distance");
				ImGui::TableSetupColumn("Occlusion");
				ImGui::TableSetupColumn("Doorways");
				ImGui::TableSetupColumn("Worked out again");
				ImGui::TableHeadersRow();
				for (const SOUND_DEBUG::HeardRecord& record : logs.heard)
				{
					if (record.stimulus != g_selectedStimulus)
						continue;
					const Recomputed again = Recompute(record);
					ImGui::TableNextRow();
					ImGui::TableNextColumn();
					ImGui::PushID(static_cast<int>(record.serial));
					const bool selected = record.serial == g_selectedHeard;
					if (ImGui::Selectable(CharacterLabel(SOUND_DEBUG::CharacterByObject(record.listener)).c_str(), selected, ImGuiSelectableFlags_SpanAllColumns))
						g_selectedHeard = selected ? 0 : record.serial;
					ImGui::PopID();
					ImGui::TableNextColumn();
					ImGui::Text("%.3f%s", record.result, record.result > 0.0f ? "" : " (not heard)");
					ImGui::TableNextColumn();
					if (again.unheard)
						ImGui::TextDisabled("no way through");
					else
						ImGui::Text("%.1f m%s", again.distance, again.viaNetwork ? "" : " (direct)");
					ImGui::TableNextColumn();
					if (again.viaNetwork && again.route.found)
						ImGui::Text("%.2f", again.route.occlusion);
					else
						ImGui::TextDisabled("-");
					ImGui::TableNextColumn();
					ImGui::Text("%d", static_cast<int>(again.route.barriers.size()));
					ImGui::TableNextColumn();
					const bool matches = std::fabs(again.intensity - record.result) < 0.001f;
					ImGui::TextColored(matches ? ImVec4(0.5f, 0.9f, 0.5f, 1.0f) : ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "%.3f (%s)", again.intensity, matches ? "the same" : "differs");
					if (!matches && ImGui::IsItemHovered())
						ImGui::SetTooltip("Worked out again now, with the doorways as they are now: a door that moved since gives another answer.");
				}
				ImGui::EndTable();
			}
			if (!any)
				Hint("Nobody checked it (no character near enough, or none that listens for it).");
		}

		ImGui::SeparatorText("Test route");
		ImGui::Checkbox("From the player to the selected character", &g_testRoute);
		ImGui::SameLine();
		DEBUG_MENU::CharacterPicker("##testlistener");
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		ImGui::SliderFloat("Loudness", &g_testLoudness, 0.0f, 1.0f, "%.2f");
		const TestRoute test = MakeTestRoute();
		if (g_testRoute && !test.have)
			ImGui::TextDisabled("Select a character other than the player.");
		else if (test.have)
		{
			float hearing = 0.0f;
			for (const SOUND_DEBUG::HeardRecord& record : logs.heard)
				if (record.listener == test.listener->object)
				{
					hearing = record.maxHearing;
					break;
				}
			if (!test.route.found)
				ImGui::TextWrapped("No way through the network: the player would not be heard (direct %.1f m).", test.straight);
			else
			{
				const float distance = (std::max)(test.straight, test.route.distance);
				ImGui::TextWrapped("Route %.1f m (direct %.1f m), occlusion %.2f, obstruction %.0f, %d doorways", test.route.distance, test.straight,
					test.route.occlusion, test.route.obstruction, static_cast<int>(test.route.barriers.size()));
				if (test.route.occlusion > 0.99f)
					ImGui::Text("Too occluded: not heard.");
				else if (hearing > 0.0f)
					ImGui::TextWrapped("A sound of loudness %.2f would be heard at %.3f (hearing distance %.1f m).", g_testLoudness,
						(std::max)(0.0f, (1.0f - distance / hearing) - (1.0f - g_testLoudness)), hearing);
				else
					Hint("Its hearing distance is known once it has checked a stimulus.");
			}
		}
	}

	void DrawNetworkSection(const SOUND_DEBUG::Snapshot& s, const Logs& logs)
	{
		const std::vector<SOUND_DEBUG::Node>& nodes = SOUND_DEBUG::Nodes();
		const std::vector<SOUND_DEBUG::Network>& networks = SOUND_DEBUG::Networks();
		const std::vector<SOUND_DEBUG::Barrier>& barriers = SOUND_DEBUG::Barriers();
		SwitchBox("Draw the network near the camera", g_networkOverlay);
		ImGui::SameLine();
		SwitchBox("Every link", g_allLinks);
		if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
			ImGui::SetTooltip("Off: each node's three shortest links only (a room links most of its nodes to each other, so every link is a dense web).");
		Hint("Nodes coloured by network (the listener's two nearest circled), links fading with obstruction and distance, doorways as squares named "
			"with their number: green open, amber part open, red closed.");
		if (!s.haveNetwork)
		{
			ImGui::TextDisabled("No sound network read for this level.");
			return;
		}
		const std::string file = SOUND_DEBUG::NetworkFile();
		ImGui::Text("%d nodes, %d networks, %d doorways%s", static_cast<int>(nodes.size()), static_cast<int>(networks.size()), static_cast<int>(barriers.size()),
			s.networkEnabled ? "" : " (the network is off)");
		if (!file.empty())
			Hint("From %s", file.c_str());
		int neverSet = 0;
		for (const SOUND_DEBUG::Barrier& barrier : barriers)
			neverSet += barrier.timesSet == 0 && barrier.state >= 1.0f ? 1 : 0;
		if (neverSet)
			ImGui::TextWrapped("%d doorways are closed and nothing has set them since the network loaded. A doorway is closed until a door or Sound Barrier entity with its id opens it, so one whose id matches none stays closed for sound and the AI's hearing.",
				neverSet);

		if (ImGui::TreeNode("Networks"))
		{
			const std::vector<uint32_t>& reverbs = SOUND_DEBUG::Reverbs();
			if (ImGui::BeginTable("networks", 6, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY |
				ImGuiTableFlags_SizingFixedFit, ImVec2(0.0f, 220.0f * DEBUG_MENU::Scale())))
			{
				ImGui::TableSetupScrollFreeze(0, 1);
				ImGui::TableSetupColumn("Network");
				ImGui::TableSetupColumn("Nodes");
				ImGui::TableSetupColumn("Doorways");
				ImGui::TableSetupColumn("Room type");
				ImGui::TableSetupColumn("Reverb");
				ImGui::TableSetupColumn("Ambience");
				ImGui::TableHeadersRow();
				for (size_t n = 0; n < networks.size(); n++)
				{
					const SOUND_DEBUG::Network& network = networks[n];
					ImGui::TableNextRow();
					ImGui::TableNextColumn();
					if (static_cast<int>(n) == s.listenerNetwork)
						ImGui::TextColored(ImVec4(1.0f, 1.0f, 0.6f, 1.0f), "%d %s (listener)", static_cast<int>(n), network.name.c_str());
					else
						ImGui::Text("%d %s", static_cast<int>(n), network.name.c_str());
					ImGui::TableNextColumn();
					ImGui::Text("%d", static_cast<int>(network.nodes.size()));
					ImGui::TableNextColumn();
					ImGui::Text("%d", static_cast<int>(network.barriers.size()));
					ImGui::TableNextColumn();
					ImGui::TextUnformatted(network.barriers.empty() ? "-" : IdName(network.roomType).c_str());
					ImGui::TableNextColumn();
					ImGui::TextUnformatted(network.reverb >= 0 && network.reverb < static_cast<int>(reverbs.size()) ? IdName(reverbs[network.reverb]).c_str() : "-");
					ImGui::TableNextColumn();
					if (network.enterEvent)
						ImGui::Text("%s%s", EventLabel(network.enterEvent).c_str(), network.ambienceActive ? " (playing)" : "");
					else
						ImGui::TextDisabled("-");
				}
				ImGui::EndTable();
			}
			ImGui::TreePop();
		}
		if (ImGui::TreeNode("Doorways"))
		{
			float camera[3] = {};
			DEBUG_MENU::CameraPosition(camera);
			if (ImGui::BeginTable("doorways", 5, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY |
				ImGuiTableFlags_SizingFixedFit, ImVec2(0.0f, 220.0f * DEBUG_MENU::Scale())))
			{
				ImGui::TableSetupScrollFreeze(0, 1);
				ImGui::TableSetupColumn("Doorway");
				ImGui::TableSetupColumn("Id");
				ImGui::TableSetupColumn("State");
				ImGui::TableSetupColumn("Between");
				ImGui::TableSetupColumn("Distance");
				ImGui::TableHeadersRow();
				for (size_t b = 0; b < barriers.size(); b++)
				{
					const SOUND_DEBUG::Barrier& barrier = barriers[b];
					ImGui::TableNextRow();
					ImGui::TableNextColumn();
					ImGui::Text("%d", static_cast<int>(b));
					ImGui::TableNextColumn();
					ImGui::Text("0x%08X", barrier.instanceId);
					ImGui::TableNextColumn();
					const ImU32 colour = DoorwayColour(barrier.state);
					ImGui::TextColored(ImGui::ColorConvertU32ToFloat4(colour), "%s%s", DoorwayStateName(barrier.state), barrier.timesSet ? "" : " (never set)");
					ImGui::TableNextColumn();
					ImGui::Text("%s - %s", NetworkName(barrier.networks[0]).c_str(), NetworkName(barrier.networks[1]).c_str());
					ImGui::TableNextColumn();
					ImGui::Text("%.1f m", Distance(barrier.position, camera));
				}
				ImGui::EndTable();
			}
			ImGui::TreePop();
		}
		if (ImGui::TreeNode("Door log"))
		{
			Hint("Every doorway state set by a door or Sound Barrier entity since the network loaded, newest first.");
			if (logs.doors.empty())
				ImGui::TextDisabled("None yet.");
			for (const SOUND_DEBUG::DoorRecord& door : logs.doors)
			{
				if (door.barrier < 0)
					ImGui::TextColored(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "%.1f s ago: id 0x%08X set to %.2f - no doorway has this id", s.now - door.time, door.instanceId, door.after);
				else if (door.sets > 1)
					ImGui::Text("%.1f s ago: doorway %d (id 0x%08X) %.2f -> %.2f over %d sets", s.now - door.time, door.barrier, door.instanceId, door.before, door.after, door.sets);
				else
					ImGui::Text("%.1f s ago: doorway %d (id 0x%08X) %.2f -> %.2f", s.now - door.time, door.barrier, door.instanceId, door.before, door.after);
			}
			ImGui::TreePop();
		}
	}

	void DrawStatesAndMusic(const SOUND_DEBUG::Snapshot& s)
	{
		ImGui::TextDisabled("The last states the game set (newest last).");
		if (s.stateCount == 0)
			ImGui::TextDisabled("None set yet.");
		for (int i = 0; i < s.stateCount; i++)
			ImGui::Text("%s = %s", IdName(s.stateHistory[i][0]).c_str(), IdName(s.stateHistory[i][1]).c_str());
		ImGui::Spacing();
		if (!s.haveMusic)
		{
			ImGui::TextDisabled("No music controller.");
			return;
		}
		ImGui::Text("Music %s: %s", s.musicEnabled ? "on" : "off", EventLabel(s.musicEvent).c_str());
		ImGui::Text("Driven by %s: %.2f, heading for %.2f", SOUND_DEBUG::MusicModeName(s.musicMode), s.musicCurrent, s.musicTarget);
		ImGui::Text("Last request %s, %d waiting", EventLabel(s.musicRequestEvent).c_str(), s.musicQueue);
	}

	void DrawBanks(const SOUND_DEBUG::Snapshot& s)
	{
		if (s.banks.empty())
		{
			ImGui::TextDisabled("No sound banks listed.");
			return;
		}
		if (!ImGui::BeginTable("banks", 3, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit,
			ImVec2(0.0f, 200.0f * DEBUG_MENU::Scale())))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Bank");
		ImGui::TableSetupColumn("Used by");
		ImGui::TableSetupColumn("State");
		ImGui::TableHeadersRow();
		for (const SOUND_DEBUG::Snapshot::Bank& bank : s.banks)
		{
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(bank.name.c_str());
			ImGui::TableNextColumn();
			ImGui::Text("%d", bank.references);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(bank.loaded ? "loaded" : bank.loading ? "loading" : "not loaded");
		}
		ImGui::EndTable();
	}

	void DrawTab()
	{
		NameSwitches();
		if (!SOUND_DEBUG::HooksAttached())
		{
			ImGui::TextDisabled("Not available on this build of the game.");
			return;
		}
		const SOUND_DEBUG::Snapshot& s = SOUND_DEBUG::Read();
		if (!s.have)
		{
			ImGui::TextDisabled("No level running.");
			return;
		}
		const Logs& logs = CopyLogs();
		if (ImGui::CollapsingHeader("Overview", ImGuiTreeNodeFlags_DefaultOpen))
			DrawOverview(s);
		if (ImGui::CollapsingHeader("Playing now", ImGuiTreeNodeFlags_DefaultOpen))
			DrawPlayingTable(s);
		if (ImGui::CollapsingHeader("Event log"))
			DrawEventLog(s, logs);
		if (ImGui::CollapsingHeader("Speech and dialogue"))
			DrawSpeechSection(s, logs);
		if (ImGui::CollapsingHeader("What the AI hears"))
			DrawHearing(s, logs);
		if (ImGui::CollapsingHeader("Sound network"))
			DrawNetworkSection(s, logs);
		if (ImGui::CollapsingHeader("States and music"))
			DrawStatesAndMusic(s);
		if (ImGui::CollapsingHeader("Sound banks"))
			DrawBanks(s);
	}

	const bool g_added = DEBUG_MENU::AddTab("Sound", 45, DrawTab, "Sound") && DEBUG_MENU::AddOverlay(40, DrawOverlay);
}
