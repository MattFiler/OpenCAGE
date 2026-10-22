#pragma once

#include "DevTools.h"
#include <cstdint>

/*
	The Performance group's Timing tab (see PERF_STATS.h): CPU time per task and GPU time per render stage.

	The game runs each frame as a graph of tasks (the ones DATA\TASKS.TXT lists: the entity manager, the AI, Havok,
	building and sorting the render lists, the render stages...), on the main thread and the worker threads. Its task
	table holds each task's function, copied into the task's work item every frame, and the tasks' names. While timing
	is switched on, each task's function in the table is swapped for a small stub of our own that times it (and, for the
	render stages, marks the GPU's progress) and calls the game's function; switched off, the table is put back. The
	stubs stay in memory for good, so a work item made before the swap back still runs.

	GPU time: around each render stage a timestamp is written into the GPU's command stream on the game's own device
	context, inside the stage's own task, taking the game's ownership of its device for that one call (as the stages
	themselves do; the frame gives the device up while its render stages run, and has it again at the presentation). A
	stamp is left out while another thread has the device. Each frame's timestamps sit inside one "disjoint" query (from
	one presentation to the next), and are read three or so frames later, without ever waiting for the GPU. The whole
	frame's GPU time is from one presentation to the next (which includes the GPU waiting for the CPU, when the CPU is
	slower).

	A task's time includes any other task it ran while it waited for another to finish.

	Addresses and layouts are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace PERF_TIMING
{
	// Render thread, once a frame (from PERF_STATS's reading): brings the task table in line with the switches, takes
	// the last frame's CPU times, and closes the frame's GPU measurements and opens the next.
	void OnFrame();

	// Render thread: true while a timing switch is on, the task table is patched or GPU measurements are open or waiting
	// to be read (the reading once a frame carries on until all of it is put back)
	bool Busy();

	// GPU timing switched on (perf.gpu_timing) and available on this build, whether or not a frame is measured yet
	bool GpuTimingOn();

	// The whole frame's GPU time (presentation to presentation, averaged over the last measured frames); false while GPU
	// timing is off or nothing is measured yet
	bool GpuFrameMs(float& ms);
}
