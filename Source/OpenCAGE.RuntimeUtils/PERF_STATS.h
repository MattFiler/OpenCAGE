#pragma once

#include "DevTools.h"
#include <cstdint>
#include <string>

/*
	The debug menu's Performance group (see DEBUG_MENU.h): how hard the game is working and how close it is to its limits.

	- Frame: the frame time (with a graph of the last few seconds, which can also be drawn over the game, under the Info
	  lines in the top left corner), the renderer's own running counts of draw calls,
	  primitives and buffer uploads taken a frame at a time, what the main view draws by material priority, every view
	  the renderer keeps, and how much of each fixed limit is in use - the GPU resource pools, the lights a frame can
	  draw, the shadow-casting lights a frame can have, and the particle systems, ribbons and particle buffers.
	- Timing: CPU time per task of the game's frame (the tasks DATA\TASKS.TXT lists) and GPU time per render stage
	  (see PERF_TIMING.h).
	- Memory: each of the game's memory heaps (how full, the most it has been), the memory each kind of script entity
	  takes, and the process: its memory, its 32-bit address space (how much is left and the largest free piece) and the
	  graphics card's memory.
	- Streaming: the texture and model streaming heaps, what waits to stream in and out, and the level's textures that
	  are in at full resolution against those that are not.

	Nothing is read or measured until one of these is used (a tab shown, an Info line or a switch turned on, a command
	run): from then on the frame's counts are read once a frame, as it is presented (a handful of reads), until a few
	seconds after the last use, and everything else only while its tab is shown or its command runs. The task timing
	patches only while switched on.

	Addresses and layouts are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace PERF_STATS
{
	// Keeps the reading once a frame going (any thread; a call while it is stopped queues it on the render thread). Called
	// each frame something is shown from it: the reading stops a few seconds after the last call, once the timing has
	// nothing patched or in flight.
	void EnsureRunning();

	// The renderer's counts for one frame (from one presentation to the next) - render thread
	struct FrameNumbers
	{
		bool have = false;              // two frames in a row were read
		uint32_t drawCalls = 0;
		uint32_t primitives = 0;        // triangles, lines or points drawn
		uint32_t bufferUpdates = 0;     // vertex, index and constant buffers written
		uint32_t bufferBytes = 0;
		uint32_t gpuResources = 0;      // textures, render targets and depth buffers held now
		float frameMs = 0.0f;           // presentation to presentation
	};
	// The last frame, and the average over about the last second
	const FrameNumbers& LastFrame();
	FrameNumbers Average();

	// The last frame times in milliseconds, oldest first (up to max of them); how many were written
	int FrameTimes(float* out, int max);

	// The process and the graphics card, read at most once a second while asked for (render thread); the address space is
	// walked on a thread of its own (a walk takes 10-20 ms), at most every five seconds, and its last walk shown from the
	// frame it finishes
	struct ProcessNumbers
	{
		bool have = false;
		uint64_t privateBytes = 0;      // memory the process has committed
		uint64_t workingSet = 0;        // of which in physical memory
		bool haveAddress = false;       // the address space walked at least once (the views show "reading..." until then)
		uint64_t addressTotal = 0;      // the process's address space (2 GB, or 4 GB when large-address aware)
		uint64_t addressFree = 0;
		uint64_t largestFree = 0;       // the largest free piece of it (the biggest allocation that can still succeed)
		uint32_t freePieces = 0;
		bool haveVideo = false;         // the graphics card's memory (Windows 10 and later)
		uint64_t videoUsed = 0;
		uint64_t videoBudget = 0;
	};
	const ProcessNumbers& Process();

	// Formatting for the views: "1.25 GB", "640 KB"... and "12.3 K", "1.2 M"
	std::string Bytes(uint64_t bytes);
	std::string Count(uint64_t count);

	// A greyed line of prose in a tab, wrapped to the window (printf-style)
	void Note(const char* format, ...);

	// The Performance tabs' tables, at the menu's text size, with a clear gap either side of each column divider; the
	// caller sets up the columns (names and other key columns fixed to their contents, numbers to the Width of their
	// widest value, a bar or the last column stretching). BeginTable's is as tall as its rows. BeginScrollingTable's also
	// scrolls: down past maxHeight (it is no taller than its rows need: rows of them, taller when they hold bars), and
	// sideways when the window is narrower than minWidth (the columns' widths added up, the stretching ones at their least)
	// instead of squashing them. A true return needs EndTable.
	bool BeginTable(const char* id, int columns);
	bool BeginScrollingTable(const char* id, int columns, float minWidth, int rows, bool bars, float maxHeight);
	void EndTable();
	// The width a column needs for this text (its header, or the widest value it shows such as "1023.9 MB"), and the
	// least to give a column of bars
	float Width(const char* text);
	float BarWidth();
	// A cell's text kept on one line: when wider than the cell, cut at the front ("...", so a path keeps its file name),
	// with the whole of it in a tooltip
	void CellText(const char* text);

	// The Info lines' panel in the top left corner, noted by the Info overlay each frame it draws (just before the graph
	// over the game, which goes under it): where its text starts and where the panel ends (render thread)
	void NoteInfoPanel(float left, float bottom);
}
