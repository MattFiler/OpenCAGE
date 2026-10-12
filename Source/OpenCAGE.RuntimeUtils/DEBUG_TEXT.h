#pragma once

#include "DevTools.h"
#include <cstdint>

namespace DEBUG_TEXT
{
	typedef bool(__thiscall* t_find_parameter_string)(void*, const void*, const void*, uint32_t*);
	inline auto find_parameter_string = reinterpret_cast<t_find_parameter_string>(DEVTOOLS_RELATIVE_ADDRESS(0x001991a0)); 
	typedef bool(__thiscall* t_find_parameter_int)(void*, const void*, const void*, int*);
	inline auto find_parameter_int = reinterpret_cast<t_find_parameter_int>(DEVTOOLS_RELATIVE_ADDRESS(0x00396570));         
	typedef bool(__thiscall* t_find_parameter_float)(void*, const void*, const void*, float*);
	inline auto find_parameter_float = reinterpret_cast<t_find_parameter_float>(DEVTOOLS_RELATIVE_ADDRESS(0x004d6510));   
	typedef bool(__thiscall* t_find_parameter_bool)(void*, const void*, const void*, bool*);
	inline auto find_parameter_bool = reinterpret_cast<t_find_parameter_bool>(DEVTOOLS_RELATIVE_ADDRESS(0x00230590));     
	typedef bool(__thiscall* t_find_parameter_vector)(void*, const void*, const void*, float*);                            
	inline auto find_parameter_vector = reinterpret_cast<t_find_parameter_vector>(DEVTOOLS_RELATIVE_ADDRESS(0x004d63c0)); 
	struct Enum { uint32_t type; int index; };                                                                            
	typedef bool(__thiscall* t_find_parameter_enum)(void*, const void*, const void*, Enum*);
	inline auto find_parameter_enum = reinterpret_cast<t_find_parameter_enum>(DEVTOOLS_RELATIVE_ADDRESS(0x004d6620));     

	typedef const void* (__thiscall* t_find_entry)(void*, const void*, const void*);
	inline auto find_entry = reinterpret_cast<t_find_entry>(DEVTOOLS_RELATIVE_ADDRESS(0x004bda00));

	typedef void(__thiscall* t_cache_lock_ctor)(void*, const void*);
	inline auto cache_lock_ctor = reinterpret_cast<t_cache_lock_ctor>(DEVTOOLS_RELATIVE_ADDRESS(0x004bca90));
	typedef void(__thiscall* t_cache_lock_dtor)(void*);
	inline auto cache_lock_dtor = reinterpret_cast<t_cache_lock_dtor>(DEVTOOLS_RELATIVE_ADDRESS(0x004bcbb0));

	typedef const char* (__thiscall* t_string_from_offset)(void*, uint32_t);
	inline auto string_from_offset = reinterpret_cast<t_string_from_offset>(DEVTOOLS_RELATIVE_ADDRESS(0x00532160));

	inline void** string_table_instance = reinterpret_cast<void**>(DEVTOOLS_RELATIVE_ADDRESS(0x0134ef78));

	typedef int(__thiscall* t_get_alignment)(void*, const void*);
	inline auto get_alignment = reinterpret_cast<t_get_alignment>(DEVTOOLS_RELATIVE_ADDRESS(0x004eae70));

	typedef float(__thiscall* t_get_duration)(void*, const void*);
	inline auto get_duration = reinterpret_cast<t_get_duration>(DEVTOOLS_RELATIVE_ADDRESS(0x004eaea0));

	typedef bool(__thiscall* t_start_updating)(void*, const void*, const void*, bool);
	inline auto start_updating = reinterpret_cast<t_start_updating>(DEVTOOLS_RELATIVE_ADDRESS(0x00539dc0));
	constexpr int kEntityStateOffset = -0x10;

	DEVTOOLS_DECLARE_CLASS_HOOK(bool, on_start, h_on_start, t_on_start, 0x001a7250, const void* entity, const void* trigger)
	DEVTOOLS_DECLARE_CLASS_HOOK(bool, on_update, h_on_update, t_on_update, 0x001a7150, const void* entity, const void* trigger)
	DEVTOOLS_DECLARE_CLASS_HOOK(bool, on_stop, h_on_stop, t_on_stop, 0x001a72e0, const void* entity, const void* trigger)
	DEVTOOLS_DECLARE_CLASS_HOOK(bool, on_clear_all, h_on_clear_all, t_on_clear_all, 0x001a7370, const void* entity, const void* trigger)
	DEVTOOLS_DECLARE_CLASS_HOOK(bool, on_clear_of_alignment, h_on_clear_of_alignment, t_on_clear_of_alignment, 0x001a73a0, const void* entity, const void* trigger)

	DEVTOOLS_DECLARE_CLASS_HOOK(bool, stacking_on_start, h_stacking_on_start, t_stacking_on_start, 0x001a6e40, const void* entity, const void* trigger)
	DEVTOOLS_DECLARE_CLASS_HOOK(bool, stacking_on_clear_all, h_stacking_on_clear_all, t_stacking_on_clear_all, 0x001a6f00, const void* entity, const void* trigger)
	DEVTOOLS_DECLARE_CLASS_HOOK(bool, stacking_on_clear_last, h_stacking_on_clear_last, t_stacking_on_clear_last, 0x001a6f30, const void* entity, const void* trigger)

	DEVTOOLS_DECLARE_CLASS_HOOK(void, level_close, h_level_close, t_level_close, 0x003d9810)

	DEVTOOLS_DECLARE_CLASS_HOOK(void, destructor, h_destructor, t_destructor, 0x001a74b0)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, stacking_destructor, h_stacking_destructor, t_stacking_destructor, 0x001a6f60)

	bool EnableEntities(bool text, bool stacking);
	void ClearAll();
	void DrawOverlay();
}
