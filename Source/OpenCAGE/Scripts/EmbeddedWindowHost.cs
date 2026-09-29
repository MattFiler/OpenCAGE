using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Hosts an external process main window inside a WinForms control using Win32 reparenting.
    /// </summary>
    public class EmbeddedWindowHost : Panel
    {
        private const int WM_SETFOCUS = 0x0007;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_ACTIVATE = 1;

        private IntPtr _embeddedWindow = IntPtr.Zero;
        private Process _process;
        private System.Windows.Forms.Timer _settleTimer;
        private int _settleTicks;

        public bool IsEmbedded => _embeddedWindow != IntPtr.Zero;

        /// <summary>The embedded window's thread answers within the time given (true when nothing is embedded).</summary>
        public bool IsEmbeddedWindowResponding(uint timeoutMs) => _embeddedWindow == IntPtr.Zero || NativeMethods.IsResponding(_embeddedWindow, timeoutMs);

        public EmbeddedWindowHost()
        {
            TabStop = true;
        }

        public event EventHandler EmbedFailed;

        /// <summary>
        /// Wait for the process to create its main window and reparent it into this control.
        ///
        /// The point of the tight polling is to catch the window before anyone sees it. Godot creates
        /// its window hidden and shows it a moment later - measured on the level viewer, the handle
        /// exists at around 280 ms and isn't shown until around 600 ms - so there is roughly a third
        /// of a second to take ownership of it during which it is not on screen. Waiting for it to be
        /// visible, or waiting on input idle first, misses that gap entirely and the window appears
        /// on the desktop before it lands in here.
        /// </summary>
        public bool TryEmbedProcess(Process process, int timeoutMs = 30000)
        {
            Detach();

            if (process == null || process.HasExited)
                return false;

            _process = process;

            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                    return false;

                if (IsHandleCreated && TryAttachMainWindow(process))
                    return true;

                Thread.Sleep(2);
            }

            EmbedFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        public void Detach()
        {
            StopSettling();

            IntPtr window;
            lock (_moveLock)
            {
                //A move still queued for this window must not show it again once it is no longer ours (see ApplyPendingMoves)
                window = _embeddedWindow;
                _embeddedWindow = IntPtr.Zero;
                _pendingMove = null;
            }

            if (window != IntPtr.Zero)
            {
                //Hidden before it leaves us so it never flashes on the desktop - but waiting on a viewer that is not
                //answering would freeze OpenCAGE, so then the hide is posted instead
                if (NativeMethods.IsResponding(window, 50))
                    NativeMethods.ShowWindow(window, NativeMethods.SW_HIDE);
                else
                    NativeMethods.ShowWindowAsync(window, NativeMethods.SW_HIDE);
                NativeMethods.SetParent(window, IntPtr.Zero);
                //The last thing queued for it: anything posted earlier (a show, a move) cannot bring it back
                NativeMethods.ShowWindowAsync(window, NativeMethods.SW_HIDE);
            }

            _process = null;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            ResizeEmbeddedWindow();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (_embeddedWindow == IntPtr.Zero)
                return;

            //Posted, not sent: see ResizeEmbeddedWindow
            NativeMethods.ShowWindowAsync(_embeddedWindow, Visible ? NativeMethods.SW_SHOW : NativeMethods.SW_HIDE);
            if (Visible)
                ResizeEmbeddedWindow();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _focusRetry?.Stop();
            Detach();
            base.OnHandleDestroyed(e);
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            if (!NativeMouseInput.IsAnyMouseButtonPressed)
                FocusEmbeddedWindow();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            FocusEmbeddedWindow(allowWhileMouseDown: true);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!NativeMouseInput.IsAnyMouseButtonPressed && IsHandleCreated)
                Focus();
        }

        protected override void WndProc(ref Message m)
        {
            if (_embeddedWindow != IntPtr.Zero)
            {
                switch (m.Msg)
                {
                    case WM_MOUSEACTIVATE:
                        FocusEmbeddedWindow(allowWhileMouseDown: true);
                        m.Result = (IntPtr)MA_ACTIVATE;
                        return;
                    case WM_SETFOCUS:
                        /* The viewer gave the focus to us itself (before a populate, or going idle): handing it straight
                           back would happen before the packet saying it is populating has been read here, so the viewer
                           would go into its populate holding the focus after all. Try again shortly instead. */
                        if (m.WParam == _embeddedWindow)
                            RetryFocusWhenResponding(allowWhileMouseDown: NativeMethods.GetCapture() == _embeddedWindow);
                        else
                            FocusEmbeddedWindow(allowWhileMouseDown: NativeMethods.GetCapture() == _embeddedWindow);
                        return;
                }
            }

            base.WndProc(ref m);
        }

        /// <summary>
        /// With the keyboard focus in the viewer's window, makes this host the active control of every container above it,
        /// without moving the focus. WinForms learns nothing when the focus goes into a window that is not one of its
        /// controls, so each container kept the control it had last - and removing one of those (closing a flowgraph
        /// page, the pane it leaves empty) made it select the next control and SetFocus it: out of the viewer's window,
        /// waiting on the viewer's thread. Selecting this host costs no focus move: the focus is already under it.
        /// </summary>
        public void AdoptViewerFocus()
        {
            if (_embeddedWindow == IntPtr.Zero || !IsHandleCreated)
                return;
            IntPtr focus = NativeMethods.GetFocus();
            if (focus != _embeddedWindow && !NativeMethods.IsChild(_embeddedWindow, focus))
                return;
            Select();
        }

        public void FocusEmbeddedWindow(bool allowWhileMouseDown = false)
        {
            if (_embeddedWindow == IntPtr.Zero || !IsHandleCreated)
                return;

            if (!allowWhileMouseDown && NativeMouseInput.IsAnyMouseButtonPressed)
                return;

            IntPtr focus = NativeMethods.GetFocus();
            if (focus == _embeddedWindow || NativeMethods.IsChild(_embeddedWindow, focus))
                return;

            if (NativeMethods.GetCapture() == _embeddedWindow)
                return;

            /* Attaching to the viewer's input queue waits on the viewer's thread, and a viewer in the
               middle of a populate, a resource sync or a scene rebuild does not answer for seconds - with
               this thread frozen the whole time. A viewer that cannot take the focus now gets it the next
               time something asks (the next click, the next WM_SETFOCUS). */
            if (UnityConnection.ViewerBusy.Likely || !NativeMethods.IsResponding(_embeddedWindow, 100))
            {
                RetryFocusWhenResponding(allowWhileMouseDown);
                return;
            }
            _focusRetry?.Stop();

            NativeMethods.GetWindowThreadProcessId(Handle, out uint hostThreadId);
            NativeMethods.GetWindowThreadProcessId(_embeddedWindow, out uint childThreadId);

            bool attached = false;
            if (hostThreadId != 0 && childThreadId != 0 && hostThreadId != childThreadId)
            {
                attached = NativeMethods.AttachThreadInput(hostThreadId, childThreadId, true);
            }

            try
            {
                NativeMethods.SetFocus(_embeddedWindow);
            }
            finally
            {
                if (attached)
                    NativeMethods.AttachThreadInput(hostThreadId, childThreadId, false);
            }
        }

        /* Focus stays on this host until the viewer answers; without this the keys the user types next
           go to the editor until they click the viewport again. Tried every quarter second for a while. */
        private System.Windows.Forms.Timer _focusRetry;
        private int _focusRetriesLeft;
        private void RetryFocusWhenResponding(bool allowWhileMouseDown)
        {
            if (_focusRetry == null)
            {
                _focusRetry = new System.Windows.Forms.Timer() { Interval = 250 };
                _focusRetry.Tick += (s, e) =>
                {
                    if (IsDisposed || !IsHandleCreated || _embeddedWindow == IntPtr.Zero || NativeMethods.GetFocus() != Handle)
                    {
                        _focusRetry.Stop();
                        return;
                    }
                    //A populate can run for a minute: wait it out (the focus is still ours to hand over), then count down
                    if (UnityConnection.ViewerBusy.Likely)
                        return;
                    if (--_focusRetriesLeft <= 0)
                    {
                        _focusRetry.Stop();
                        return;
                    }
                    FocusEmbeddedWindow(allowWhileMouseDown);
                };
            }
            _focusRetriesLeft = 40;
            _focusRetry.Start();
        }

        private bool TryAttachMainWindow(Process process)
        {
            IntPtr window = FindBestTopLevelWindow((uint)process.Id);
            if (window == IntPtr.Zero)
                return false;

            /* Hide it before touching anything else. Usually it hasn't been shown yet and this does
             * nothing, which is the whole point; if we were too slow it cuts the flash short. */
            NativeMethods.ShowWindow(window, NativeMethods.SW_HIDE);

            /* Deliberately not setting WS_VISIBLE here. The window is still parented to the desktop
             * at this point, so anything that makes it visible puts it on screen - it gets shown at
             * the end, once it is safely a child of this control. */
            int style = NativeMethods.GetWindowLong(window, NativeMethods.GWL_STYLE);
            style &= ~(NativeMethods.WS_POPUP | NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME
                | NativeMethods.WS_MINIMIZEBOX | NativeMethods.WS_MAXIMIZEBOX | NativeMethods.WS_SYSMENU
                | NativeMethods.WS_VISIBLE);
            style |= NativeMethods.WS_CHILD;
            NativeMethods.SetWindowLong(window, NativeMethods.GWL_STYLE, style);

            NativeMethods.SetParent(window, Handle);
            _embeddedWindow = window;

            //now it can be seen, in here
            ResizeEmbeddedWindow();
            FocusEmbeddedWindow();

            /* Godot still has its own startup to finish, and it ends with showing and positioning
             * the window it doesn't know we have taken. As a child those coordinates are relative to
             * this control, so they land somewhere arbitrary - put it back a few times while that
             * plays out. */
            StartSettling();
            return true;
        }

        /* Re-apply the embedded window's bounds for a couple of seconds after attaching, to undo any
         * moving the process does while it finishes starting up. */
        private void StartSettling()
        {
            StopSettling();

            _settleTicks = 0;
            _settleTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _settleTimer.Tick += (s, e) =>
            {
                if (_embeddedWindow == IntPtr.Zero || ++_settleTicks > 20)
                {
                    StopSettling();
                    return;
                }
                ResizeEmbeddedWindow();
            };
            _settleTimer.Start();
        }

        private void StopSettling()
        {
            if (_settleTimer == null)
                return;

            _settleTimer.Stop();
            _settleTimer.Dispose();
            _settleTimer = null;
        }

        public void RefreshEmbeddedBounds()
        {
            ResizeEmbeddedWindow();
        }

        /* The viewer's window belongs to another process's thread, so SetWindowPos on it waits until that thread takes the
           messages - and the viewer's main thread is busy for seconds at a time (a populate, a big spawn). The stress run
           caught OpenCAGE frozen for 90 s in here, showing the viewport panel or laying out the dock while the viewer was
           spawning. SWP_ASYNCWINDOWPOS alone does not help from this thread: it only posts when the caller's input queue
           is not attached to the window's, and parenting the viewer's window into ours attaches exactly those two. So
           the move is made from a pool thread (no windows, no attached queue), where the flag posts it to the viewer's
           thread and returns at once. Moves are coalesced: only the latest size matters. */
        private readonly object _moveLock = new object();
        private (IntPtr window, int width, int height)? _pendingMove;
        private bool _moverRunning;

        private void ResizeEmbeddedWindow()
        {
            if (_embeddedWindow == IntPtr.Zero || !IsHandleCreated)
                return;

            lock (_moveLock)
            {
                _pendingMove = (_embeddedWindow, Math.Max(0, ClientSize.Width), Math.Max(0, ClientSize.Height));
                if (_moverRunning)
                    return;
                _moverRunning = true;
            }
            ThreadPool.QueueUserWorkItem(_ => ApplyPendingMoves());
        }

        private void ApplyPendingMoves()
        {
            while (true)
            {
                lock (_moveLock)
                {
                    if (_pendingMove == null)
                    {
                        _moverRunning = false;
                        return;
                    }
                    (IntPtr window, int width, int height) move = _pendingMove.Value;
                    _pendingMove = null;
                    //Detached meanwhile: showing it now would put it on the desktop. Posting is immediate, so the lock is held
                    //across it - Detach cannot slip in between the check and the post.
                    if (move.window == IntPtr.Zero || move.window != _embeddedWindow)
                        continue;
                    try
                    {
                        NativeMethods.SetWindowPos(move.window, IntPtr.Zero, 0, 0, move.width, move.height,
                            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_ASYNCWINDOWPOS);
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// The process's main window, whether or not it has been shown yet.
        ///
        /// Visibility is deliberately not a requirement - catching the window while it is still
        /// hidden is what stops it appearing on the desktop first. That means the tiny helper windows
        /// a process makes on the way up have to be excluded instead, since they are no longer
        /// filtered out by being invisible: the level viewer alone puts up two 0x0 IME windows, a 0x0
        /// MSCTFIME UI window and a 1x1 "Temp Window" for Direct3D before its real one at 1168x687.
        /// </summary>
        private static IntPtr FindBestTopLevelWindow(uint processId)
        {
            const long smallestRealWindow = 64 * 64;

            IntPtr bestWindow = IntPtr.Zero;
            long bestArea = 0;
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                if (NativeMethods.GetParent(hWnd) != IntPtr.Zero)
                    return true;

                //a message-only window hangs off HWND_MESSAGE rather than the desktop
                if (NativeMethods.GetAncestor(hWnd, NativeMethods.GA_PARENT) != NativeMethods.GetDesktopWindow())
                    return true;

                //an owned window is a dialog or a tool window, never the main one
                if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
                    return true;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint windowProcessId);
                if (windowProcessId != processId)
                    return true;

                if (!NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect))
                    return true;

                long area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                if (area < smallestRealWindow || area <= bestArea)
                    return true;

                bestArea = area;
                bestWindow = hWnd;
                return true;
            }, IntPtr.Zero);

            return bestWindow;
        }

        private static class NativeMethods
        {
            public const int GWL_STYLE = -16;
            public const int WS_CHILD = 0x40000000;
            public const int WS_VISIBLE = 0x10000000;
            public const int WS_POPUP = unchecked((int)0x80000000);
            public const int WS_CAPTION = 0x00C00000;
            public const int WS_THICKFRAME = 0x00040000;
            public const int WS_MINIMIZEBOX = 0x00020000;
            public const int WS_MAXIMIZEBOX = 0x00010000;
            public const int WS_SYSMENU = 0x00080000;

            public const uint SWP_NOZORDER = 0x0004;
            public const uint SWP_NOACTIVATE = 0x0010;
            public const uint SWP_FRAMECHANGED = 0x0020;
            public const uint SWP_SHOWWINDOW = 0x0040;
            public const uint SWP_ASYNCWINDOWPOS = 0x4000;

            public const int SW_HIDE = 0;
            public const int SW_SHOW = 5;

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetWindowPos(
                IntPtr hWnd,
                IntPtr hWndInsertAfter,
                int x,
                int y,
                int cx,
                int cy,
                uint uFlags);

            [DllImport("user32.dll")]
            public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("user32.dll")]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern IntPtr GetParent(IntPtr hWnd);

            public const uint GA_PARENT = 1;
            public const uint GW_OWNER = 4;

            [DllImport("user32.dll")]
            public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

            [DllImport("user32.dll")]
            public static extern IntPtr GetWindow(IntPtr hWnd, uint command);

            [DllImport("user32.dll")]
            public static extern IntPtr GetDesktopWindow();

            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern IntPtr SetFocus(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

            [DllImport("user32.dll")]
            public static extern IntPtr GetFocus();

            [DllImport("user32.dll")]
            public static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeoutMs, out IntPtr result);

            private const uint WM_NULL = 0x0000;
            private const uint SMTO_ABORTIFHUNG = 0x0002;

            /// <summary>Whether the window's thread answers a no-op message within the time given.</summary>
            public static bool IsResponding(IntPtr window, uint timeoutMs)
            {
                if (window == IntPtr.Zero)
                    return false;
                IntPtr result;
                return SendMessageTimeout(window, WM_NULL, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, timeoutMs, out result) != IntPtr.Zero;
            }

            [DllImport("user32.dll")]
            public static extern IntPtr GetCapture();
        }
    }

    internal static class NativeMouseInput
    {
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int VK_MBUTTON = 0x04;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        public static bool IsAnyMouseButtonPressed =>
            IsKeyDown(VK_LBUTTON) || IsKeyDown(VK_RBUTTON) || IsKeyDown(VK_MBUTTON);

        private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }
}
