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

        public bool IsEmbedded => _embeddedWindow != IntPtr.Zero;

        public EmbeddedWindowHost()
        {
            TabStop = true;
        }

        public event EventHandler EmbedFailed;

        public bool TryEmbedProcess(Process process, int timeoutMs = 30000)
        {
            Detach();

            if (process == null || process.HasExited)
                return false;

            _process = process;

            try
            {
                process.WaitForInputIdle(5000);
            }
            catch
            {
            }

            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                    return false;

                if (IsHandleCreated && TryAttachMainWindow(process))
                    return true;

                Application.DoEvents();
                Thread.Sleep(100);
            }

            EmbedFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        public void Detach()
        {
            if (_embeddedWindow != IntPtr.Zero)
            {
                NativeMethods.SetParent(_embeddedWindow, IntPtr.Zero);
                _embeddedWindow = IntPtr.Zero;
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

            NativeMethods.ShowWindow(_embeddedWindow, Visible ? NativeMethods.SW_SHOW : NativeMethods.SW_HIDE);
            if (Visible)
                ResizeEmbeddedWindow();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
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
                        FocusEmbeddedWindow(allowWhileMouseDown: NativeMethods.GetCapture() == _embeddedWindow);
                        return;
                }
            }

            base.WndProc(ref m);
        }

        public void FocusEmbeddedWindow(bool allowWhileMouseDown = false)
        {
            if (_embeddedWindow == IntPtr.Zero || !IsHandleCreated)
                return;

            if (!allowWhileMouseDown && NativeMouseInput.IsAnyMouseButtonPressed)
                return;

            if (NativeMethods.GetFocus() == _embeddedWindow)
                return;

            if (NativeMethods.GetCapture() == _embeddedWindow)
                return;

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

        private bool TryAttachMainWindow(Process process)
        {
            IntPtr window = FindBestTopLevelWindow((uint)process.Id);
            if (window == IntPtr.Zero)
                return false;

            int style = NativeMethods.GetWindowLong(window, NativeMethods.GWL_STYLE);
            style &= ~(NativeMethods.WS_POPUP | NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME
                | NativeMethods.WS_MINIMIZEBOX | NativeMethods.WS_MAXIMIZEBOX | NativeMethods.WS_SYSMENU);
            style |= NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE;
            NativeMethods.SetWindowLong(window, NativeMethods.GWL_STYLE, style);

            NativeMethods.SetParent(window, Handle);
            _embeddedWindow = window;
            ResizeEmbeddedWindow();
            FocusEmbeddedWindow();
            return true;
        }

        private void ResizeEmbeddedWindow()
        {
            if (_embeddedWindow == IntPtr.Zero || !IsHandleCreated)
                return;

            NativeMethods.SetWindowPos(
                _embeddedWindow,
                IntPtr.Zero,
                0,
                0,
                Math.Max(0, ClientSize.Width),
                Math.Max(0, ClientSize.Height),
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW);
        }

        private static IntPtr FindBestTopLevelWindow(uint processId)
        {
            IntPtr bestWindow = IntPtr.Zero;
            long bestArea = 0;
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                if (!NativeMethods.IsWindowVisible(hWnd))
                    return true;

                if (NativeMethods.GetParent(hWnd) != IntPtr.Zero)
                    return true;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint windowProcessId);
                if (windowProcessId != processId)
                    return true;

                if (!NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect))
                    return true;

                long area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                if (area <= bestArea)
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

            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern IntPtr SetFocus(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

            [DllImport("user32.dll")]
            public static extern IntPtr GetFocus();

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
