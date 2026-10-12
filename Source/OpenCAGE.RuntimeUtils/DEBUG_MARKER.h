#pragma once

#include "DevTools.h"
#include <cstdint>

namespace DEBUG_MARKER
{
	struct Position { float m[12]; }; 
	struct alignas(16) Matrix44 { float m[16]; }; 
	struct alignas(16) Vector4 { float x, y, z, w; };
	struct Vector3 { float x, y, z; };

	inline uintptr_t engine = DEVTOOLS_RELATIVE_ADDRESS(0x013583a0);
	constexpr uintptr_t kEngineCameraOffset = 0xe660;
	constexpr uintptr_t kCameraMatricesOffset = 0x8;

	typedef Matrix44* (__thiscall* t_matrix_multiply)(const Matrix44*, Matrix44*, const Matrix44*);
	inline auto matrix_multiply = reinterpret_cast<t_matrix_multiply>(DEVTOOLS_RELATIVE_ADDRESS(0x00028220));
	typedef Vector4* (__thiscall* t_matrix_transform)(const Matrix44*, Vector4*, const Vector4*);
	inline auto matrix_transform = reinterpret_cast<t_matrix_transform>(DEVTOOLS_RELATIVE_ADDRESS(0x00028690));

	typedef Position* (__thiscall* t_get_world_pos)(void*, Position*, const void*);
	inline auto get_world_pos = reinterpret_cast<t_get_world_pos>(DEVTOOLS_RELATIVE_ADDRESS(0x004eb060));
	typedef Vector3* (__thiscall* t_get_colour)(void*, Vector3*, const void*);
	inline auto get_colour = reinterpret_cast<t_get_colour>(DEVTOOLS_RELATIVE_ADDRESS(0x004eb030));
	typedef float(__thiscall* t_get_size)(void*, const void*);
	inline auto get_size = reinterpret_cast<t_get_size>(DEVTOOLS_RELATIVE_ADDRESS(0x004eb000));

	typedef void** (__thiscall* t_get_target)(void*, void**, const void*);
	inline auto get_target = reinterpret_cast<t_get_target>(DEVTOOLS_RELATIVE_ADDRESS(0x004fb1e0));
	typedef void(__thiscall* t_release_entity)(void**);
	inline auto release_entity = reinterpret_cast<t_release_entity>(DEVTOOLS_RELATIVE_ADDRESS(0x00198230));
	constexpr uintptr_t kEntityObjectOffset = 0xc;

	typedef Position* (__cdecl* t_entity_position)(Position*, const void*, Position);
	inline auto entity_position = reinterpret_cast<t_entity_position>(DEVTOOLS_RELATIVE_ADDRESS(0x00004710));
	inline const Position* default_position = reinterpret_cast<const Position*>(DEVTOOLS_RELATIVE_ADDRESS(0x01243a78));

	DEVTOOLS_DECLARE_CLASS_HOOK(bool, position_on_update, h_position_on_update, t_position_on_update, 0x001a7d30, const void* entity, const void* trigger)
	DEVTOOLS_DECLARE_CLASS_HOOK(bool, environment_on_update, h_environment_on_update, t_environment_on_update, 0x001a77d0, const void* entity, const void* trigger)

	bool EnableEntities(bool environmentMarker);
	void ClearAll();
	void DrawOverlay();
}
