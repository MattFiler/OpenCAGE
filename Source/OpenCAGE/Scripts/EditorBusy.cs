using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The editor held still for a long job on the open level - an import into it, an export from it - as Save &amp; Build holds
    /// it for a build: every window disabled (as a modal window disables the others), the level picker closed, undo blocked,
    /// what the viewport sends that would change the level held until the end, and no save, level load or package import
    /// started meanwhile. The windows go on painting and Windows never calls the editor not responding: work that touches
    /// nothing open (unpacking a package, loading or saving another level) runs on a worker through <see cref="Run"/> while
    /// messages are pumped, and work that reads or writes the open level stays on the UI thread and calls <see cref="Pump"/>
    /// as it goes. UI thread only.
    /// </summary>
    internal sealed class EditorBusy : IDisposable
    {
        private static EditorBusy _current;

        /// <summary>A job is holding the editor (see <see cref="Begin"/>).</summary>
        public static bool Active => _current != null;

        private const int PumpIntervalMs = 50;

        private readonly bool _outermost;
        private readonly List<Form> _disabled = new List<Form>();
        private bool _undoWasBlocked, _viewerWasHeld;
        private readonly Stopwatch _sincePump = Stopwatch.StartNew();
        private bool _pumping, _disposed;

        private EditorBusy(bool outermost)
        {
            _outermost = outermost;
        }

        /// <summary>
        /// Hold the editor until the result is disposed. Inside a job already holding it, this changes nothing; whatever was
        /// held before (a save's holds, say) is left held at the end.
        /// </summary>
        public static EditorBusy Begin()
        {
            if (_current != null)
                return new EditorBusy(false);

            EditorBusy busy = new EditorBusy(true);
            CommandsEditor editor = Singleton.Editor;
            //Left open beside the job, it could load another level over the one being worked on
            editor?.CloseLevelPicker();
            if (OpenCAGE.Undo.UndoStack.Current != null)
            {
                busy._undoWasBlocked = OpenCAGE.Undo.UndoStack.Current.Blocked;
                OpenCAGE.Undo.UndoStack.Current.Blocked = true;
            }
            busy._viewerWasHeld = ViewerInboundDispatcher.IsHeldForSave;
            ViewerInboundDispatcher.HoldForSave();
            if (editor != null && !editor.IsDisposed)
                editor.DisableWindowsForSave(busy._disabled);
            _current = busy;
            Cursor.Current = Cursors.WaitCursor;
            return busy;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (!_outermost)
                return;

            _current = null;
            if (OpenCAGE.Undo.UndoStack.Current != null)
                OpenCAGE.Undo.UndoStack.Current.Blocked = _undoWasBlocked;
            if (!_viewerWasHeld)
                ViewerInboundDispatcher.ReleaseAfterSave();
            CommandsEditor.EnableWindowsAfterSave(_disabled);
            Cursor.Current = Cursors.Default;
        }

        private static bool OnUiThread
        {
            get
            {
                CommandsEditor editor = Singleton.Editor;
                return editor != null && !editor.IsDisposed && editor.IsHandleCreated && !editor.InvokeRequired;
            }
        }

        /// <summary>
        /// Run work that touches nothing open in the editor on a worker, pumping messages until it is done, as Save &amp; Build
        /// does: everything paints and runs as usual, and what could change the open level is held off by <see cref="Begin"/>.
        /// Exceptions come back here as thrown. With no job holding the editor, or off the UI thread, the work just runs here.
        /// </summary>
        public static T Run<T>(Func<T> work)
        {
            if (_current == null || !OnUiThread)
                return work();

            Task<T> task = Task.Run(work);
            //Not Task.Wait: waiting on a task that hasn't started yet runs it right here, on this thread
            while (!task.IsCompleted)
            {
                Application.DoEvents();
                Thread.Sleep(16);
            }
            _current?._sincePump.Restart();
            return task.GetAwaiter().GetResult();
        }

        public static void Run(Action work)
        {
            Run(() => { work(); return 0; });
        }

        /// <summary>
        /// For work on the UI thread that reads or writes the open level, between its steps (a porter's progress ticks, say):
        /// repaint what needs it, keep the progress bars moving and answer Windows, which calls a window that hasn't looked at
        /// its messages for five seconds not responding. Nothing else runs - no timers, no queued work - since anything that ran
        /// now could find the level half-written, and input that arrived is thrown away: dispatched, a click on a title bar or
        /// a menu runs a loop of its own that runs everything. At most every 50 ms; nothing with no job holding the editor.
        /// </summary>
        public static void Pump()
        {
            EditorBusy busy = _current;
            if (busy == null || busy._pumping || busy._sincePump.ElapsedMilliseconds < PumpIntervalMs || !OnUiThread)
                return;

            busy._pumping = true;
            try
            {
                Discard(WM_NCMOUSEMOVE, WM_NCXBUTTONDBLCLK);
                Discard(WM_KEYFIRST, WM_KEYLAST);
                Discard(WM_GESTURE, WM_GESTURE);
                Discard(WM_MOUSEFIRST, WM_MOUSELAST);
                Discard(WM_TOUCH, WM_POINTERLAST);
                Discard(WM_NCMOUSEHOVER, WM_MOUSELEAVE);

                //Made on demand while a window has something to repaint, and gone once it has: the cap is for a paint that fails
                MSG msg;
                for (int i = 0; i < 256 && PeekMessage(out msg, IntPtr.Zero, WM_PAINT, WM_PAINT, PM_REMOVE); i++)
                    DispatchMessage(ref msg);

                //The progress windows' bars animate on timers of their own
                foreach (ProgressUI form in Application.OpenForms.OfType<ProgressUI>().ToList())
                {
                    if (!form.IsDisposed && form.IsHandleCreated)
                        DispatchTimers(form);
                }

                //What tells Windows the editor is still responding: a look at the whole queue, which a filtered look is not.
                //Nothing is taken off it.
                PeekMessage(out msg, IntPtr.Zero, 0, 0, PM_NOREMOVE);
            }
            finally
            {
                busy._pumping = false;
                busy._sincePump.Restart();
            }
        }

        private static void DispatchTimers(Control control)
        {
            MSG msg;
            for (int i = 0; i < 16 && PeekMessage(out msg, control.Handle, WM_TIMER, WM_TIMER, PM_REMOVE); i++)
                DispatchMessage(ref msg);
            foreach (Control child in control.Controls)
            {
                if (child.IsHandleCreated)
                    DispatchTimers(child);
            }
        }

        private static void Discard(uint first, uint last)
        {
            MSG msg;
            for (int i = 0; i < 4096 && PeekMessage(out msg, IntPtr.Zero, first, last, PM_REMOVE); i++) { }
        }

        private const uint PM_NOREMOVE = 0x0000, PM_REMOVE = 0x0001;
        private const uint WM_PAINT = 0x000F, WM_TIMER = 0x0113, WM_GESTURE = 0x0119;
        private const uint WM_NCMOUSEMOVE = 0x00A0, WM_NCXBUTTONDBLCLK = 0x00AD;
        private const uint WM_KEYFIRST = 0x0100, WM_KEYLAST = 0x0109;
        private const uint WM_MOUSEFIRST = 0x0200, WM_MOUSELAST = 0x020E;
        private const uint WM_TOUCH = 0x0240, WM_POINTERLAST = 0x0257;
        private const uint WM_NCMOUSEHOVER = 0x02A0, WM_MOUSELEAVE = 0x02A3;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint filterMin, uint filterMax, uint remove);
        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG msg);
    }
}
