#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>

#include <imgui.h>

/*
	Measuring rings, a section of the World group's Overlays tab: up to four rings of chosen radius on the ground around
	the player, the selected character, the point under the crosshair or a point dropped where the player stands, for
	checking the ranges AI works to (how far a sense reaches, where a character starts to run). The characters inside
	the outer ring are joined to the centre by a line labelled with how far away they are. Drawn with
	world.overlay.rings; the radii are values (world.rings.radius_1 to _4, 0 for none) and so is the centre
	(world.rings.centre: 0 the player, 1 the selected character, 2 the crosshair point, 3 the dropped point).
*/
namespace
{
	std::atomic<bool> g_rings = false;
	std::atomic<bool> g_distances = true;
	std::atomic<float> g_radius1 = 2.0f, g_radius2 = 5.0f, g_radius3 = 10.0f, g_radius4 = 20.0f;
	std::atomic<float> g_centreMode = 0.0f;
	std::atomic<float>* const g_radii[4] = { &g_radius1, &g_radius2, &g_radius3, &g_radius4 };
	constexpr float kDefaultRadii[4] = { 2.0f, 5.0f, 10.0f, 20.0f };
	constexpr float kMaxRadius = 100.0f;

	enum Centre { CentrePlayer, CentreSelected, CentrePick, CentreDropped, kCentres };
	const char* const kCentreNames[kCentres] = { "The player", "The selected character", "The point under the crosshair", "The dropped point" };

	// The dropped point (render thread)
	float g_dropped[3] = {};
	bool g_haveDropped = false;

	int CentreMode()
	{
		const int mode = static_cast<int>(std::lround(g_centreMode.load()));
		return (std::max)(0, (std::min)(mode, kCentres - 1));
	}

	const DEBUG_MENU::Character* Player()
	{
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.player)
				return &character;
		return nullptr;
	}

	// Where the rings are centred (render thread), and the character there (to leave out of the distances), if any
	bool CentreOf(float out[3], uint32_t& characterId, std::string& why)
	{
		characterId = 0;
		switch (CentreMode())
		{
		case CentrePlayer:
			if (const DEBUG_MENU::Character* player = Player())
			{
				memcpy(out, player->position, sizeof(player->position));
				characterId = player->id;
				return true;
			}
			why = "No player.";
			return false;
		case CentreSelected:
			if (const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected())
			{
				memcpy(out, selected->position, sizeof(selected->position));
				characterId = selected->id;
				return true;
			}
			why = "No character selected (choose one in Characters).";
			return false;
		case CentrePick:
		{
			DEBUG_MENU::PickResult pick;
			if (DEBUG_MENU::Pick(pick))
			{
				memcpy(out, pick.position, sizeof(pick.position));
				return true;
			}
			why = "Nothing under the crosshair (turn on the crosshair in World > Inspect, or the game is paused).";
			return false;
		}
		default:
			if (g_haveDropped)
			{
				memcpy(out, g_dropped, sizeof(g_dropped));
				return true;
			}
			why = "No point dropped yet.";
			return false;
		}
	}

	bool Drop()
	{
		const DEBUG_MENU::Character* player = Player();
		if (player)
		{
			memcpy(g_dropped, player->position, sizeof(g_dropped));
			g_haveDropped = true;
			return true;
		}
		float camera[3];
		if (!DEBUG_MENU::CameraPosition(camera))
			return false;
		memcpy(g_dropped, camera, sizeof(g_dropped));
		g_haveDropped = true;
		return true;
	}

	ImU32 RingColour(int index)
	{
		static const ImU32 kColours[4] = { IM_COL32(120, 255, 140, 230), IM_COL32(230, 240, 110, 230), IM_COL32(255, 170, 80, 230), IM_COL32(255, 90, 90, 230) };
		return kColours[index & 3];
	}

	float Horizontal(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dz * dz);
	}

	float OuterRadius()
	{
		float outer = 0.0f;
		for (std::atomic<float>* radius : g_radii)
			outer = (std::max)(outer, radius->load());
		return outer;
	}

	void DrawRings(ImDrawList* drawList)
	{
		if (!g_rings)
			return;
		float centre[3];
		uint32_t centreId = 0;
		std::string why;
		if (!CentreOf(centre, centreId, why))
			return;
		DEBUG_MENU::WorldCross(drawList, centre, 0.2f, IM_COL32(255, 255, 255, 220), 2.0f);
		for (int i = 0; i < 4; i++)
		{
			const float radius = g_radii[i]->load();
			if (radius <= 0.0f)
				continue;
			const int segments = (std::max)(32, (std::min)(160, static_cast<int>(radius * 8.0f)));
			DEBUG_MENU::WorldCircle(drawList, centre, radius, RingColour(i), segments, 2.0f);
			char label[32];
			snprintf(label, sizeof(label), "%g m", radius);
			const float at[3] = { centre[0] + radius, centre[1], centre[2] };
			DEBUG_MENU::WorldText(drawList, at, label, RingColour(i) | IM_COL32_A_MASK);
		}
		if (!g_distances)
			return;
		const float outer = OuterRadius();
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		{
			if (character.id == centreId || character.dead)
				continue;
			const float across = Horizontal(centre, character.position);
			if (across > outer)
				continue;
			const float rise = character.position[1] - centre[1];
			// Coloured as the smallest ring it is inside
			ImU32 colour = IM_COL32(255, 255, 255, 220);
			float smallest = FLT_MAX;
			for (int i = 0; i < 4; i++)
			{
				const float radius = g_radii[i]->load();
				if (radius > 0.0f && across <= radius && radius < smallest)
				{
					smallest = radius;
					colour = RingColour(i);
				}
			}
			DEBUG_MENU::WorldLine(drawList, centre, character.position, colour, 1.5f);
			char label[96];
			if (std::fabs(rise) > 0.5f)
				snprintf(label, sizeof(label), "%s %.1f m (%+.1f m up)", character.name.c_str(), across, rise);
			else
				snprintf(label, sizeof(label), "%s %.1f m", character.name.c_str(), across);
			const float mid[3] = { character.position[0], character.position[1] + 0.3f, character.position[2] };
			DEBUG_MENU::WorldText(drawList, mid, label, colour | IM_COL32_A_MASK);
		}
	}

	void DrawSection()
	{
		ImGui::SeparatorText("Measuring rings");
		bool on = g_rings;
		if (ImGui::Checkbox("Draw rings on the ground", &on))
			g_rings = on;
		ImGui::SameLine();
		bool distances = g_distances;
		if (ImGui::Checkbox("Distances to the characters inside", &distances))
			g_distances = distances;
		int mode = CentreMode();
		ImGui::SetNextItemWidth(260.0f * DEBUG_MENU::Scale());
		if (ImGui::Combo("Around", &mode, kCentreNames, kCentres))
			g_centreMode = static_cast<float>(mode);
		ImGui::SameLine();
		if (ImGui::Button("Drop a point here"))
		{
			Drop();
			g_centreMode = static_cast<float>(CentreDropped);
		}
		for (int i = 0; i < 4; i++)
		{
			float radius = g_radii[i]->load();
			char label[32];
			snprintf(label, sizeof(label), "Ring %d", i + 1);
			ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
			ImGui::PushStyleColor(ImGuiCol_Text, RingColour(i) | IM_COL32_A_MASK);
			if (ImGui::SliderFloat(label, &radius, 0.0f, kMaxRadius, radius > 0.0f ? "%.1f m" : "off", ImGuiSliderFlags_AlwaysClamp))
				g_radii[i]->store(radius);
			ImGui::PopStyleColor();
			if (i % 2 == 0)
				ImGui::SameLine();
		}
		if (ImGui::Button("Default radii"))
			for (int i = 0; i < 4; i++)
				g_radii[i]->store(kDefaultRadii[i]);
		float centre[3];
		uint32_t centreId = 0;
		std::string why;
		ImGui::PushTextWrapPos(0.0f);
		if (!CentreOf(centre, centreId, why))
			ImGui::TextDisabled("%s", why.c_str());
		else
			ImGui::TextDisabled("Centred at %.2f, %.2f, %.2f. Distances are along the ground (the rise is shown when over half a metre).", centre[0], centre[1], centre[2]);
		ImGui::PopTextWrapPos();
	}

	DEBUG_MENU::CommandResult Report()
	{
		float centre[3];
		uint32_t centreId = 0;
		std::string why;
		if (!CentreOf(centre, centreId, why))
			return { false, why };
		char text[256];
		snprintf(text, sizeof(text), "centre_mode=%d\ncentre=%.3f,%.3f,%.3f\nradii=%g,%g,%g,%g\n", CentreMode(), centre[0], centre[1], centre[2], g_radius1.load(), g_radius2.load(),
			g_radius3.load(), g_radius4.load());
		std::string out = text;
		const float outer = OuterRadius();
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		{
			if (character.id == centreId || character.dead)
				continue;
			const float across = Horizontal(centre, character.position);
			if (across > outer)
				continue;
			snprintf(text, sizeof(text), "character=%s id=%u across=%.2f rise=%.2f\n", character.name.c_str(), character.id, across, character.position[1] - centre[1]);
			out += text;
		}
		return { true, out };
	}

	bool RegisterControls()
	{
		DEBUG_MENU::AddSwitch("world.overlay.rings", &g_rings);
		DEBUG_MENU::AddSwitch("world.rings.distances", &g_distances);
		DEBUG_MENU::AddValue("world.rings.radius_1", &g_radius1, 0.0f, kMaxRadius);
		DEBUG_MENU::AddValue("world.rings.radius_2", &g_radius2, 0.0f, kMaxRadius);
		DEBUG_MENU::AddValue("world.rings.radius_3", &g_radius3, 0.0f, kMaxRadius);
		DEBUG_MENU::AddValue("world.rings.radius_4", &g_radius4, 0.0f, kMaxRadius);
		DEBUG_MENU::AddValue("world.rings.centre", &g_centreMode, 0.0f, static_cast<float>(kCentres - 1));
		DEBUG_MENU::AddCommand("world.rings.drop", "drop the rings' point where the player stands (or the camera is), or at \"x,y,z\", and centre the rings on it",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			float at[3];
			const bool given = !argument.empty() && sscanf_s(argument.c_str(), "%f,%f,%f", &at[0], &at[1], &at[2]) == 3;
			if (!argument.empty() && !given)
				return { false, "Give the point as x,y,z (or nothing for where the player stands)" };
			return DEBUG_MENU::AnswerOnRenderThread([given, at]() -> DEBUG_MENU::CommandResult
			{
				if (given)
				{
					memcpy(g_dropped, at, sizeof(g_dropped));
					g_haveDropped = true;
				}
				else if (!Drop())
					return { false, "No player or camera to drop the point at" };
				g_centreMode = static_cast<float>(CentreDropped);
				char text[96];
				snprintf(text, sizeof(text), "Dropped at %.3f,%.3f,%.3f", g_dropped[0], g_dropped[1], g_dropped[2]);
				return { true, text };
			});
		});
		DEBUG_MENU::AddCommand("world.rings.reset", "the rings' radii back to 2, 5, 10 and 20 m (the Default radii button)",
			[](const std::string&) -> DEBUG_MENU::CommandResult
		{
			for (int i = 0; i < 4; i++)
				g_radii[i]->store(kDefaultRadii[i]);
			return { true, "Radii 2, 5, 10, 20 m" };
		});
		DEBUG_MENU::AddCommand("world.rings.report", "the rings' centre and radii, and each character inside the outer ring with its distance",
			[](const std::string&) { return DEBUG_MENU::AnswerOnRenderThread([]() { return Report(); }); });
		return true;
	}

	const bool g_controls = RegisterControls();
	const bool g_added = DEBUG_MENU::AddTab("Overlays", 116, DrawSection, "World") && DEBUG_MENU::AddOverlay(63, DrawRings);
}
