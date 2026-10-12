#include "DEBUG_MARKER.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <imgui.h>

#include <cstring>
#include <mutex>
#include <string>
#include <vector>

namespace
{
	struct Marker
	{
		void* owner;
		ULONGLONG seenAt;
		bool visible;
		ImVec2 origin; 
		bool axes;
		ImVec2 axisEnd[3];
		bool axisVisible[3];
		std::string label;
		ImU32 colour;
		float size;
	};

	std::mutex g_mutex;
	std::vector<Marker> g_markers;

	constexpr ULONGLONG kStaleMs = 250;   
	constexpr float kAxisLength = 0.6f;
	constexpr float kAxisThickness = 3.5f;
	constexpr int kEnvironmentTextOffset = 0x14;

	bool ViewProjection(DEBUG_MARKER::Matrix44& out)
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
		DEBUG_MARKER::matrix_multiply(&view, &out, &projection);
		return true;
	}

	bool Project(const DEBUG_MARKER::Matrix44& viewProjection, const DEBUG_MARKER::Vector3& world, ImVec2& screen)
	{
		DEBUG_MARKER::Vector4 point = { world.x, world.y, world.z, 1.0f };
		DEBUG_MARKER::Vector4 clip;
		DEBUG_MARKER::matrix_transform(&viewProjection, &clip, &point);
		if (clip.w <= 0.0f || clip.z < 0.0f)
			return false;
		screen.x = (clip.x / clip.w + 1.0f) * 0.5f;
		screen.y = 1.0f - (clip.y / clip.w + 1.0f) * 0.5f;
		return true;
	}

	DEBUG_MARKER::Vector3 Translation(const DEBUG_MARKER::Position& position)
	{
		return { position.m[3], position.m[7], position.m[11] };
	}

	bool TargetPosition(void* entityInterface, const void* entity, DEBUG_MARKER::Vector3& out)
	{
		void* target = nullptr;
		DEBUG_MARKER::get_target(entityInterface, &target, entity);

		bool found = false;
		if (target && *reinterpret_cast<void**>(static_cast<char*>(target) + DEBUG_MARKER::kEntityObjectOffset))
		{
			DEBUG_MARKER::Position position;
			DEBUG_MARKER::entity_position(&position, &target, *DEBUG_MARKER::default_position);
			if (memcmp(&position, DEBUG_MARKER::default_position, sizeof(position)) != 0)
			{
				out = Translation(position);
				found = true;
			}
		}
		DEBUG_MARKER::release_entity(&target);
		return found;
	}

	Marker& Upsert(void* owner)
	{
		for (Marker& marker : g_markers)
			if (marker.owner == owner)
				return marker;
		g_markers.push_back({ owner });
		return g_markers.back();
	}
}

bool DEBUG_MARKER::EnableEntities(bool environmentMarker)
{
	return !environmentMarker || DevTools::EnableEntity(0x00e57c48);
}

void DEBUG_MARKER::ClearAll()
{
	std::lock_guard<std::mutex> lock(g_mutex);
	g_markers.clear();
}

__declspec(noinline)
bool __fastcall DEBUG_MARKER::h_position_on_update(void* _this, void* /*_EDX*/, const void* entity, const void* trigger)
{
	Position position;
	get_world_pos(_this, &position, entity);

	Matrix44 viewProjection;
	if (ViewProjection(viewProjection))
	{
		const Vector3 origin = Translation(position);
		ImVec2 originScreen;
		const bool visible = Project(viewProjection, origin, originScreen);

		std::lock_guard<std::mutex> lock(g_mutex);
		Marker& marker = Upsert(_this);
		marker.seenAt = GetTickCount64();
		marker.visible = visible;
		marker.origin = originScreen;
		marker.axes = true;
		for (int axis = 0; axis < 3; axis++)
		{
			const Vector3 end = {
				origin.x + position.m[axis] * kAxisLength,
				origin.y + position.m[4 + axis] * kAxisLength,
				origin.z + position.m[8 + axis] * kAxisLength,
			};
			marker.axisVisible[axis] = Project(viewProjection, end, marker.axisEnd[axis]);
		}
	}

	return position_on_update(_this, entity, trigger);
}

__declspec(noinline)
bool __fastcall DEBUG_MARKER::h_environment_on_update(void* _this, void* /*_EDX*/, const void* entity, const void* /*trigger*/)
{
	Vector3 origin;
	if (!TargetPosition(_this, entity, origin))
	{
		Position position;
		get_world_pos(_this, &position, entity);
		origin = Translation(position);
	}
	Vector3 colour;
	get_colour(_this, &colour, entity);
	const float size = get_size(_this, entity);
	const char* text = static_cast<const char*>(_this) + kEnvironmentTextOffset;

	Matrix44 viewProjection;
	if (ViewProjection(viewProjection))
	{
		ImVec2 originScreen;
		const bool visible = Project(viewProjection, origin, originScreen);

		auto channel = [](float v) { return static_cast<int>(v < 0.0f ? 0.0f : v > 255.0f ? 255.0f : v); };

		std::lock_guard<std::mutex> lock(g_mutex);
		Marker& marker = Upsert(_this);
		marker.seenAt = GetTickCount64();
		marker.visible = visible;
		marker.origin = originScreen;
		marker.axes = false;
		marker.label = text;
		marker.colour = IM_COL32(channel(colour.x), channel(colour.y), channel(colour.z), 255);
		marker.size = size > 0.0f ? size : 20.0f;
	}

	return true;
}

void DEBUG_MARKER::DrawOverlay()
{
	std::lock_guard<std::mutex> lock(g_mutex);

	const ULONGLONG now = GetTickCount64();
	for (auto it = g_markers.begin(); it != g_markers.end();)
		it = (now - it->seenAt > kStaleMs) ? g_markers.erase(it) : it + 1;

	if (g_markers.empty())
		return;

	ImDrawList* drawList = ImGui::GetForegroundDrawList();
	ImFont* font = ImGui::GetFont();
	const ImVec2 screen = ImGui::GetIO().DisplaySize;
	auto toScreen = [&screen](const ImVec2& normalised) { return ImVec2(normalised.x * screen.x, normalised.y * screen.y); };
	static const ImU32 axisColours[3] = { IM_COL32(255, 40, 40, 255), IM_COL32(40, 255, 40, 255), IM_COL32(40, 120, 255, 255) };

	for (const Marker& marker : g_markers)
	{
		if (!marker.visible)
			continue;

		const ImVec2 origin = toScreen(marker.origin);
		if (marker.axes)
		{
			for (int axis = 0; axis < 3; axis++)
				if (marker.axisVisible[axis])
					drawList->AddLine(origin, toScreen(marker.axisEnd[axis]), axisColours[axis], kAxisThickness);
			drawList->AddCircleFilled(origin, kAxisThickness, IM_COL32_WHITE);
		}
		else
		{
			const ImVec2 textSize = font->CalcTextSizeA(marker.size, FLT_MAX, 0.0f, marker.label.c_str());
			const ImVec2 pos(origin.x - textSize.x * 0.5f, origin.y - textSize.y * 0.5f);
			drawList->AddText(font, marker.size, ImVec2(pos.x + 1.0f, pos.y + 1.0f), IM_COL32(0, 0, 0, 200), marker.label.c_str());
			drawList->AddText(font, marker.size, pos, marker.colour, marker.label.c_str());
		}
	}
}
