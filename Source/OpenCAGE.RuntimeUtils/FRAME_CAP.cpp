#include "FRAME_CAP.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#ifndef CREATE_WAITABLE_TIMER_HIGH_RESOLUTION
#define CREATE_WAITABLE_TIMER_HIGH_RESOLUTION 0x00000002
#endif

namespace
{
	LONGLONG g_frequency = 0;	// performance counter ticks a second
	LONGLONG g_interval = 0;	// ticks between frames at the cap; 0 = no cap
	LONGLONG g_next = 0;		// the soonest the next frame may start
	LONGLONG g_spin = 0;		// the last stretch of a wait, spun rather than slept
	HANDLE g_timer = nullptr;	// a high-resolution waitable timer (Windows 10 1803 on), or null for Sleep

	LONGLONG Now()
	{
		LARGE_INTEGER now;
		QueryPerformanceCounter(&now);
		return now.QuadPart;
	}

	// Waits until 'deadline' (performance counter ticks): sleeping while more than the spin stretch is left, then spinning.
	void WaitUntil(LONGLONG deadline)
	{
		for (;;)
		{
			const LONGLONG remaining = deadline - Now();
			if (remaining <= 0)
				return;

			if (remaining <= g_spin)
			{
				YieldProcessor();
				continue;
			}

			if (g_timer)
			{
				// Relative due time, in 100 ns units
				LARGE_INTEGER due;
				due.QuadPart = -((remaining - g_spin) * 10000000LL / g_frequency);
				if (due.QuadPart < 0 && SetWaitableTimer(g_timer, &due, 0, nullptr, nullptr, FALSE))
				{
					WaitForSingleObject(g_timer, INFINITE);
					continue;
				}
			}
			Sleep(1);
		}
	}
}

void FRAME_CAP::SetLimit(int framesPerSecond)
{
	g_interval = 0;
	g_next = 0;
	if (framesPerSecond <= 0)
		return;

	LARGE_INTEGER frequency;
	QueryPerformanceFrequency(&frequency);
	g_frequency = frequency.QuadPart;
	g_interval = g_frequency / framesPerSecond;

	if (!g_timer)
		g_timer = CreateWaitableTimerExW(nullptr, nullptr, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS);

	// A high-resolution timer wakes within a fraction of a millisecond; Sleep(1) can take a couple more
	g_spin = g_frequency * (g_timer ? 1 : 3) / 2000;

	DevTools::Log("Cap FPS: at most %d frames a second (%s).", framesPerSecond, g_timer ? "high-resolution timer" : "Sleep, no high-resolution timer");
}

__declspec(noinline)
void __cdecl FRAME_CAP::h_frame_start()
{
	if (g_interval > 0)
	{
		const LONGLONG now = Now();
		if (g_next != 0 && now < g_next)
		{
			// Early: wait for this frame's turn, and keep to the cadence from there
			WaitUntil(g_next);
		}
		else if (g_next == 0 || now - g_next > g_interval)
		{
			// The first frame, or one that ran a whole frame long: count from now rather than rush to catch up
			g_next = now;
		}
		g_next += g_interval;
	}

	frame_start();
}
