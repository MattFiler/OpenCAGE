#pragma once
#include <cstdint>

#define DEVTOOLS_DETOURS_ATTACH(original_func, detoured_func) (DetourAttach(&reinterpret_cast<PVOID&>(original_func), detoured_func))
#define DEVTOOLS_DETOURS_DETACH(original_func, detoured_func) (DetourDetach(&reinterpret_cast<PVOID&>(original_func), detoured_func))

// An address in the game, given where the Steam build has it (see GameAddresses.h for the other builds): 0 when this build
// has nothing known there
#define DEVTOOLS_RELATIVE_ADDRESS(offset) (DevTools::GameAddress(offset))

#define DEVTOOLS_DECLARE_CLASS_HOOK(return_type, original_func_name, hook_name, typedef_name, func_offset, ...) \
	return_type __fastcall hook_name(void* _this, void* _EDX, __VA_ARGS__); \
	typedef return_type(__thiscall* typedef_name)(void*, __VA_ARGS__); \
	inline auto original_func_name = reinterpret_cast<typedef_name>(DEVTOOLS_RELATIVE_ADDRESS(func_offset));

namespace DevTools
{
	class GameProcess
	{
	public:
		static uintptr_t GetBaseAddress();
	};

	// Which store's AI.exe the game is, told by its header (when it was built, and its size in memory). Every address the
	// code uses is the Steam build's, looked up for the GOG and Epic Games Store builds; a build that is none of these gets
	// no addresses, and nothing is hooked in it.
	enum class GameBuild { Unknown, Steam, GOG, Epic };
	GameBuild Build();
	const char* BuildName();

	// Where this build has what the Steam build has at steamOffset (an offset from the start of AI.exe in memory): 0 when
	// it has nothing known there
	uintptr_t BuildOffset(uintptr_t steamOffset);

	// The same as an address in the running game (0 when there is none)
	uintptr_t GameAddress(uintptr_t steamOffset);

	bool EnableEntity(uintptr_t slotOffset);

	// Appends a timestamped line to OpenCAGE.RuntimeUtils.log next to AI.exe (started afresh each run). Thread-safe.
	void Log(const char* format, ...);
}
