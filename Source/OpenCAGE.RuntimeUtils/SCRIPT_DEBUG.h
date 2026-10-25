#pragma once

#include "DevTools.h"
#include <cstdint>
#include <string>
#include <vector>

/*
	What the debug menu's Scripts group (SCRIPT_ERRORS, SCRIPT_TRIGGERS, SCRIPT_PROGRESS, SCRIPT_EVENTS, SCRIPT_ANIMATIONS,
	SCRIPT_ENTITIES, SCRIPT_PROFILER) shares: how an entity of the level's scripts is told apart, and the patching of single
	calls in the game's code.

	How the game keeps a level's scripts, in short: every entity is an object (counted, like most of the game's objects:
	what the game passes around is a pointer to a small record holding the object and its count) with its state flags, the
	composite instance holding it, its id within that composite and its type - the id of its entity type, or of the
	composite it is an instance of. A composite instance is an entity too; the level's root instance is held by nothing.

	The game names an entity the same way everywhere it keeps one (collision, pickups, the saved variables, script errors):
	by its id and the id of the composite instance it sits in. An instance's id is worked out from the ids of the instance
	entities from it up to the root - its own id, combined in turn with each one above it - which is the same id OpenCAGE
	works out for that instance from the level's scripting, so OpenCAGE can find the entity from the two. The shipped
	game keeps no entity names (only zones keep theirs), but every composite keeps its name, so the composite an entity
	sits in can be named in game.

	Calls in the game's code are changed one at a time, at the call instruction (never at the function called, which may
	be shared by many unrelated calls): each call is checked to be a call that reaches what it should before its distance
	is changed, and put back when the debug menu's hooks detach - unless it no longer reaches what it was changed to, which
	means another tool has changed it since.
*/
namespace SCRIPT_DEBUG
{
	// An entity as OpenCAGE can find it (all ids as the game keeps them)
	struct EntityId
	{
		bool valid = false;
		uint32_t entity = 0;        // its id in its composite
		uint32_t instance = 0;      // the id of the composite instance it sits in (0: it is the level's root instance)
		uint32_t type = 0;          // the id of its entity type, or of the composite it is an instance of
		uint32_t composite = 0;     // the id of the composite it sits in (0: none, the level's root instance)
		uint32_t state = 0;         // its state flags (bit 2 started, bit 7 enabled, bit 18 updating, bit 28 initialised)
		char compositeName[96] = {};
		char instanceOf[96] = {};   // when it is a composite instance itself: the name of the composite it is an instance of
	};

	// Reads an entity (guarded: false when it cannot be read): from the game's counted record of it, or from a pointer to
	// one (what the game passes when it hands an entity to a function)
	bool ReadEntity(const void* record, EntityId& out);
	bool ReadEntityAt(const void* recordPointer, EntityId& out);
	// The entity object a counted record holds (null when none), guarded
	const uint8_t* EntityObject(const void* record);

	// Combines two ids the way the game does when it works out an instance's id
	uint32_t Combine(uint32_t a, uint32_t b);

	// An id the way OpenCAGE shows one ("01-23-45-67", its bytes in order); the plain number for command replies ("67452301")
	std::string Guid(uint32_t value);
	std::string Hex(uint32_t value);
	// The name of an entity type or of a method, for the commonest (SCRIPT_NAMES.cpp); null when not known
	const char* TypeName(uint32_t id);
	const char* MethodName(uint32_t id);
	// An entity's type as words: its type's name, else the composite it is an instance of, else the id
	std::string TypeText(const EntityId& id);
	// A method as words: its name, else its id
	std::string MethodText(uint32_t method);
	// A composite as words: the level's root composite (named by the path the level was built from, such as
	// "P:\CONTENT\BUILD\LEVELS\PRODUCTION\HAB_AIRPORT") as "the level root (HAB_AIRPORT)", any other by its name, else its id
	std::string CompositeText(uint32_t composite, const char* name);
	// The composite an entity sits in, as words the same way; empty for the level's root instance, which sits in none
	std::string WhereText(const EntityId& id);
	// "PlayerHasItem 01-23-45-67 in Composite\Name (instance 01-02-03-04)"
	std::string Describe(const EntityId& id);
	// The same without the instance, for a table's cell: "PlayerHasItem 01-23-45-67 in Composite\Name"
	std::string ShortText(const EntityId& id);
	// The same for a command reply: "entity=67452301 instance=04030201 type=EFCDAB89 type_name=PlayerHasItem composite=Composite\Name"
	std::string Fields(const EntityId& id);

	// The entity manager (null when there is none) and whether a level's scripts are up, guarded
	uint8_t* EntityManager();
	bool LevelRunning();
	// Whether the game is running again the call it held at a breakpoint (being let go), guarded
	bool RunningHeldCall();
	// The level's script clock, in seconds (0 with no level)
	double ScriptTime();
	// The running level's root composite id (0: none)
	uint32_t RunningRoot();

	// Whether text holds part (both lower-cased; an empty part is in everything)
	bool Matches(const std::string& text, const char* part);

	// A table of the Scripts tabs (render thread): each cell on one line, so an id or a path is never cut short or broken
	// across lines; each column as wide as its widest cell, the table scrolling sideways when they are wider than the
	// window; a little room each side of a cell's text. Height in pixels. Ended with ImGui::EndTable when it returns true.
	bool BeginTable(const char* id, int columns, float height);

	// Entity thread: the running entities of the given types in the level (their counted records, not referenced: use them
	// within the frame), found through the level's composites - each composite whose scripting holds an entity of one of
	// those types, in each of its running instances - up to max. False with no level.
	struct FoundEntity
	{
		const void* record;
		uint32_t type;
	};
	bool FindEntities(const uint32_t* types, int typeCount, std::vector<FoundEntity>& out, size_t max = 4096);

	// Known entity type ids (as OpenCAGE has them)
	constexpr uint32_t kTypeCAGEAnimation = 0x790007BC;
	constexpr uint32_t kTypeDebugGraph = 0x62BB56F3;
	constexpr uint32_t kTypeDebugMenuToggle = 0xDEF6759B;
	constexpr uint32_t kTypeDebugObjectMarker = 0x28C341BA;
	constexpr uint32_t kTypeDebugCaptureScreenShot = 0x42F278CB;

	// One call instruction of the game's code changed to call something else
	struct PatchedCall
	{
		uint8_t* at = nullptr;     // the call instruction (0 when it was not patched)
		int32_t original = 0;      // its distance as the game had it
		int32_t patched = 0;       // its distance to the replacement
	};
	// Changes the call at steamSite (a Steam build address, looked up for the others: 0 there means not this build) to
	// call replacement, once it is checked to be a call that reaches what is at steamTarget now. False (and nothing
	// changed) otherwise; the reason in why.
	bool PatchCall(uintptr_t steamSite, uintptr_t steamTarget, const void* replacement, PatchedCall& out, const char*& why);
	// The same, checking the call reaches target (an address in the running game) instead
	bool PatchCallTo(uintptr_t steamSite, uintptr_t target, const void* replacement, PatchedCall& out, const char*& why);
	// Puts a call back as the game had it (only while it still calls the replacement); false when it could not
	bool RestoreCall(PatchedCall& call);

	// One entry of one of the game's virtual tables changed to point at something else (one class's own table, so only
	// that class is affected)
	struct PatchedSlot
	{
		uintptr_t* at = nullptr;   // the entry (null when it was not patched)
		uintptr_t original = 0;
		uintptr_t patched = 0;
	};
	// Changes the entry at byte offset in the table at steamTable (a Steam build address, looked up for the others) to
	// replacement, once it is checked to hold what is at steamExpected now. False (and nothing changed) otherwise.
	bool PatchSlot(uintptr_t steamTable, uint32_t offset, uintptr_t steamExpected, const void* replacement, PatchedSlot& out, const char*& why);
	// Puts an entry back (only while it still holds the replacement)
	bool RestoreSlot(PatchedSlot& slot);

	// Notes a problem with a script entity in the script errors list (SCRIPT_ERRORS, Scripts > Errors), as one of the
	// game's own errors is: the entity (a pointer to its counted record, as the game passes one) and the message
	void NoteScriptError(const void* entityRef, const char* message);

	// Every trigger the game is about to run (the entity manager's check, just before it calls a method through a link),
	// for the trigger counts (SCRIPT_TRIGGERS): called by SCRIPT_ERRORS's patched check, on the thread processing the
	// level's triggers. Returns at once while counting is off.
	void CountTrigger(const void* info);
}

// The debug script entities made to work again (SCRIPT_ENTITIES.cpp). They run on their own, debug menu or not, whenever
// one of their keys is on (Launch Game's Debug entities writes all four): their hooks are attached with the others, and with
// the debug menu off their overlay is drawn from the overlay's own frame (with it on, the debug menu draws it).
namespace SCRIPT_ENTITIES
{
	void AttachHooks(bool attach);

	// Render thread, inside the ImGui frame, while the debug menu is off
	void DrawOverlay();
}

// The game's script errors and failed checks (SCRIPT_ERRORS.cpp). Noted with the debug menu, or on their own with
// ScriptErrors=1 (Launch Game's Error log), for OpenCAGE's Error Log to read over Live Link (ERRORS_GET).
namespace SCRIPT_ERRORS
{
	void AttachHooks(bool attach);

	// Whether the game's errors are being noted (the hooks are attached and they could be patched)
	bool Following();

	// Live Link's ERRORS_GET, on the socket thread: every error noted after the sequence number given (0: all still kept),
	// oldest first, at most a batch at a time, as its payload (see LIVE_LINK_SERVER.h), with a summary for its message.
	// False (with why) while errors are not being noted.
	bool TakeSince(uint32_t after, std::string& message, std::vector<uint8_t>& payload);
}
