#pragma once

#include "DevTools.h"
#include <cstdint>

/*
	Behaviour tree debugging, in the debug menu (see DEBUG_MENU.h): what each character's behaviour tree is doing, the
	trees' own debug text, and the trees' debug switches, none of which the game itself shows.

	Every character the game runs has a behaviour tree, made of nodes the behaviour tree editor names (selectors,
	sequences, conditions, branches, actions), that the game runs once a frame. While it runs, the game keeps the path of
	nodes from the tree's root to the action the character is carrying out (the "leading" node), each with how it is
	going (running, succeeded, failed, shutting down...), and, when a better branch wants to take over, a second path to
	that branch, which waits for the running action to shut down. It also keeps the outer branch the character is in (idle,
	search, attack...) and a count of every branch type on its path. None of that has a name in the shipped game: a node
	is only an object of its class. So:

	- When a level opens, the game loads every behaviour tree from its one packed tree file, file by file, a node at a
	  time. Three hooks follow that: the loading of all the trees (start and finish), the loading of one tree file (a
	  top-level tree, or a tree another tree refers to, which the game loads in place), and the reading of one node. The
	  first node read in a file is the file's root: the file's nodes are numbered from it in document order, as the
	  behaviour tree editor's XML has them (index 0 is the root), and every node the game makes is noted against its file
	  and number, its editor class, its Name and the attributes that say most about it (a branch's type, a switch's label
	  and default, a debug text). A node read through the editor's wrapper for a tree's root is read again, at once, as the
	  wrapped node: the first note made of a node is kept. The notes are built aside and handed over when the loading
	  finishes; the level closing drops them. If the loading was missed, the class each node object has still gives the
	  editor class (a fixed table), without the rest.
	- After each character's tree has run (the tree update hook, which Possess Characters also uses), both paths, the
	  outer branch and the branch counts are copied for the menu, and the branches that started or ended since the last run
	  are noted as events. So is the end of each leading action (succeeded or failed), from a hook on the game's own
	  handling of it.
	- The editor's DebugString action is for printing a text and its Assert action for reporting a message; the game
	  makes both succeed and do nothing else (their text is still loaded). The two classes' start entries
	  in their vtables are replaced: each records the text, against the character and the node, and then does what the
	  game did. Both run on every pass of the tree's search while they are on its path, so a line is only logged again
	  after a second without one from the same node and character (counted meanwhile).
	- The editor's DebugMenuLinkTest condition and DebugMenuLinkItem action are the trees' debug switches: each has a
	  label (In_Game_Menu_Text) and a default; the game reads only the default. Here each label is a tick box in the
	  debug menu: a ticked test goes the other way from its default, and an item sets its tick back to its default (so a
	  switch the tree acts on unticks itself). Both vtable entries are replaced to do that, with the labels noted at load;
	  the ticks are the menu's (or OpenCAGE's), for every character or only the one selected in the menu, and go back to
	  unticked when a level loads.

	The menu has three tabs: Behaviour Tree (the selected character's paths, branches, recent events and debug texts),
	Tree Switches and Tree Log (every debug text and assert logged), and labels over the characters when switched on.

	Threads: the trees load on the level loading thread, and run on the game's AI threads (and now and then on the entity
	thread, when a script asks for it); the menu draws on the render thread. What the hooks note is kept behind locks and
	copied out by the menu, which never reads the game's nodes itself: a node object is only read by the thread running
	its tree, while that tree is running - the class it names itself by (through its vtable), a branch node's type, and a
	text or switch node's own text and default as the game runs it - and a node record only if it is one of the pool's.
*/
namespace BT_DEBUG
{
	// Hooked: the loading of every behaviour tree, as a level opens.
	typedef void(__cdecl* t_load_all_trees)();
	inline auto load_all_trees = reinterpret_cast<t_load_all_trees>(DEVTOOLS_RELATIVE_ADDRESS(0x003344E0));
	void __cdecl h_load_all_trees();

	// Hooked: the loading of one tree file by name (a top-level tree, or one a tree refers to), giving its first node.
	typedef void*(__cdecl* t_load_tree_file)(const char* name);
	inline auto load_tree_file = reinterpret_cast<t_load_tree_file>(DEVTOOLS_RELATIVE_ADDRESS(0x0032D3E0));
	void* __cdecl h_load_tree_file(const char* name);

	// Hooked: the reading of one node from the packed tree file (which reads its children in turn), giving the node.
	typedef void*(__cdecl* t_read_tree_node)(const void* tag);
	inline auto read_tree_node = reinterpret_cast<t_read_tree_node>(DEVTOOLS_RELATIVE_ADDRESS(0x0032B950));
	void* __cdecl h_read_tree_node(const void* tag);

	// Hooked: the end of a character's leading action, which has just succeeded or failed.
	DEVTOOLS_DECLARE_CLASS_HOOK(void, leading_node_done, h_leading_node_done, t_leading_node_done, 0x002FDF60)

	// Attaches or detaches the hooks above (in dllmain's hook list, with the debug menu on), and puts the debug text,
	// assert and switch nodes' vtable entries in place, or back as they were.
	void AttachHooks(bool attach);

	// The tree update hook, once a character's tree has run (not when Possess Characters skipped it): its state is copied
	// for the menu. tree is the character's running tree.
	void AfterTreeUpdate(void* tree);

	// The level closing: everything noted about its trees and characters goes (the log stays).
	void OnLevelClose();
}
