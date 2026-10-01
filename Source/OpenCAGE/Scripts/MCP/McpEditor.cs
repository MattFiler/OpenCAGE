using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Reaching the editor from a tool: its thread, the level it has open, and waiting for it.
    /// </summary>
    internal static class McpEditor
    {
        public static CommandsEditor Editor
        {
            get
            {
                CommandsEditor editor = Singleton.Editor;
                if (editor == null || editor.IsDisposed)
                    throw new McpError("The OpenCAGE window is not open.");
                return editor;
            }
        }

        //Whether this tool call has made its first step on the UI thread (the thread a tool runs on is its own for the call)
        [ThreadStatic] private static bool _begun;

        /// <summary>A tool call is starting on this thread: its first step on the UI thread checks for the user's dialogs.</summary>
        public static void BeginCall() => _begun = false;

        /// <summary>Run on the UI thread and wait for it; an exception comes back as itself.</summary>
        public static T UI<T>(Func<T> work)
        {
            CommandsEditor editor = Editor;
            //Only a call's first step refuses to start under a user's modal dialog: later steps (and the releases in its
            //finally blocks) must always run, or a guard it took would be left held
            bool first = !_begun;
            _begun = true;
            if (!editor.InvokeRequired)
                return Tracked(work, first);
            //...but not while the user is being asked something (closing OpenCAGE, say): run inside their box, a step holds up
            //their answer until it is done. It waits for the answer instead (after a Yes to closing, the window is gone)
            if (!first)
                while (Volatile.Read(ref McpDialogs.UserQuestions) > 0 && !editor.IsDisposed)
                    Thread.Sleep(50);
            Exception failure = null;
            T result = default(T);
            editor.Invoke(new Action(() =>
            {
                try { result = Tracked(work, first); }
                catch (Exception e) { failure = e; }
            }));
            if (failure is McpError || failure is OperationCanceledException)
                throw failure;
            if (failure != null)
                throw new McpUiException(failure);
            return result;
        }

        public static void UI(Action work) => UI<object>(() => { work(); return null; });

        /// <summary>Work a tool is doing on the UI thread: message boxes shown meanwhile are the tool's (see <see cref="McpDialogs"/>).</summary>
        private static T Tracked<T>(Func<T> work, bool firstStep)
        {
            McpDialogs.UiThreadId = McpDialogs.CurrentThreadId();
            //Invoked work also runs inside a modal loop the user has open: a call never starts underneath their dialog
            if (firstStep && Volatile.Read(ref McpDialogs.UiDepth) == 0)
                McpDialogs.ThrowIfUserModal();
            Interlocked.Increment(ref McpDialogs.UiDepth);
            try { return work(); }
            finally { Interlocked.Decrement(ref McpDialogs.UiDepth); }
        }

        /// <summary>
        /// The level that is open and ready (call on the UI thread): loaded, with its panels built, and
        /// not in the middle of a save or backup - anything else is an error the caller can act on.
        /// </summary>
        public static LevelContent RequireLevel(bool forEditing = true)
        {
            CommandsEditor editor = Editor;
            LevelContent content = editor.CompositeBrowser?.Content;
            if (editor.IsLevelLoadInProgress)
                throw new McpError("A level is still loading. Wait for it (get_editor_state shows when it is ready).");
            if (content == null || content.Level == null || !content.IsLevelDataLoaded || content.EditorUtils == null)
                throw new McpError("No level is open. Open one with load_level (list_levels shows them), or make one with create_level.");
            if (forEditing)
            {
                if (UndoStack.Current.Blocked)
                    throw new McpError("OpenCAGE is saving the level. Try again when it has finished.");
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                    throw new McpError("A level backup is running. Try again when it has finished.");
            }
            return content;
        }

        public static Commands RequireCommands(bool forEditing = true) => RequireLevel(forEditing).Level.Commands;

        /// <summary>
        /// Refuse while the editor is part-way through an edit of its own (a multi-entity parameter change leaves
        /// an undo group open until its queued follow-up runs): a step made now would be folded into it.
        /// </summary>
        public static void RequireUndoIdle()
        {
            UndoStack stack = UndoStack.Current;
            if (stack.IsApplying || stack.IsSuspended || stack.IsGrouping)
                throw new McpError("OpenCAGE is in the middle of an edit. Try again in a moment.");
        }

        /// <summary>Whether the level with this name is open with its panels built (UI thread).</summary>
        public static bool IsReady(string levelName)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed || editor.IsLevelLoadInProgress)
                return false;
            CompositeBrowser browser = editor.CompositeBrowser;
            LevelContent content = browser?.Content;
            return browser != null && !browser.IsDisposed && content?.Level != null && content.IsLevelDataLoaded && content.EditorUtils != null
                && (levelName == null || string.Equals(content.Level.Name, levelName, StringComparison.OrdinalIgnoreCase))
                && editor.CompositeDisplay != null && editor.CompositeDisplay.Populated;
        }

        /// <summary>Poll (on the UI thread) until the condition holds, reporting progress; false on timeout.</summary>
        public static bool WaitFor(McpCall call, Func<bool> condition, TimeSpan timeout, string waitingFor)
        {
            Stopwatch waited = Stopwatch.StartNew();
            int reported = -1;
            while (waited.Elapsed < timeout)
            {
                call?.ThrowIfCancelled();
                if (UI(condition))
                    return true;
                int seconds = (int)waited.Elapsed.TotalSeconds;
                if (call != null && seconds / 5 != reported)
                {
                    reported = seconds / 5;
                    call.Progress(waitingFor + " (" + seconds + " s)", seconds, timeout.TotalSeconds);
                }
                Thread.Sleep(200);
            }
            return false;
        }
    }

    /// <summary>An exception from the UI thread, carried back with its type and message.</summary>
    internal sealed class McpUiException : Exception
    {
        public McpUiException(Exception inner) : base(inner.GetType().Name + ": " + inner.Message, inner) { }
    }

    /// <summary>
    /// Message boxes OpenCAGE shows for a tool. A tool cannot click them, and a box left open would hold the
    /// tool up until someone does, so each is read, recorded for the tool's result, and answered with its
    /// most cautious button (Cancel, then No, then OK). Only the tool's own are touched: boxes from background
    /// threads (the level loader's), or from the UI thread while a tool's work is running on it. A box the
    /// user raises while the UI thread is theirs is left for them, and so is any box already open when the
    /// tool started; while the user has a modal window or box open, tools refuse to work on the UI thread.
    /// </summary>
    internal static class McpDialogs
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr dialog, int id);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int GetDlgCtrlID(IntPtr hWnd);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        public static uint CurrentThreadId() => GetCurrentThreadId();

        /// <summary>The UI thread's native id, and how deep tools' work on it is nested (from McpEditor.UI).</summary>
        public static volatile uint UiThreadId;
        public static int UiDepth;

        private const uint WM_COMMAND = 0x0111;
        private const uint WM_CLOSE = 0x0010;
        private const int IDOK = 1, IDCANCEL = 2, IDABORT = 3, IDRETRY = 4, IDIGNORE = 5, IDYES = 6, IDNO = 7;

        private static readonly object _lock = new object();
        private static int _watchers;
        private static System.Threading.Timer _timer;
        private static McpCall _call;
        private static readonly HashSet<IntPtr> _answered = new HashSet<IntPtr>();
        private static readonly HashSet<IntPtr> _foreign = new HashSet<IntPtr>();
        //Captions of the boxes AskUser has open
        private static readonly HashSet<string> _usersOwn = new HashSet<string>();

        public static IDisposable Watch(McpCall call)
        {
            lock (_lock)
            {
                _call = call;
                if (_watchers++ == 0)
                {
                    //Boxes already up belong to someone else (the user, or their own background work): never answered
                    _answered.Clear();
                    _foreign.Clear();
                    foreach (IntPtr existing in Dialogs(false))
                        _foreign.Add(existing);
                    _timer = new System.Threading.Timer(_ => Sweep(), null, 300, 300);
                }
            }
            return new Scope();
        }

        /// <summary>
        /// Refuse (UI thread) while the user has a modal window or message box open: work invoked now would run
        /// inside its modal loop, underneath it. Boxes this watchdog has answered, and are closing, do not count.
        /// </summary>
        public static void ThrowIfUserModal()
        {
            foreach (Form form in Application.OpenForms)
                if (form != null && !form.IsDisposed && form.Modal && form.Visible)
                    throw new McpError("OpenCAGE is showing the '" + form.Text + "' window. Close it, then try again.");
            uint ui = CurrentThreadId();
            foreach (IntPtr box in Dialogs(false))
            {
                lock (_lock)
                    if (_answered.Contains(box)) continue;
                if (GetWindowThreadProcessId(box, out _) != ui) continue;
                throw new McpError("OpenCAGE is showing a message (\"" + TextOf(box) + "\"). Answer it, then try again.");
            }
        }

        /// <summary>How many questions AskUser has open: a running tool's next step waits for the answer (McpEditor.UI).</summary>
        public static int UserQuestions;

        /// <summary>
        /// Ask the user something (UI thread) while a tool may be running - closing OpenCAGE part-way through one, say.
        /// A tool's next step waits for the answer, but a step can still land inside the box's modal loop (a call's first,
        /// which refuses), and would make it look like the tool's own box to the watchdog whenever a sweep lands on it: this
        /// one is the user's, and is never answered for a tool. The progress window of a load or save stays behind it.
        /// </summary>
        public static DialogResult AskUser(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1, IWin32Window owner = null)
        {
            lock (_lock) _usersOwn.Add(caption);
            Interlocked.Increment(ref UserQuestions);
            ProgressUI.QuestionsOpen++;
            RestackProgressWindows();
            try { return MessageBox.Show(owner, text, caption, buttons, icon, defaultButton); }
            finally
            {
                ProgressUI.QuestionsOpen--;
                RestackProgressWindows();
                Interlocked.Decrement(ref UserQuestions);
                lock (_lock) _usersOwn.Remove(caption);
            }
        }

        //A progress window keeps itself topmost, over the question: behind it while one is open, back on top after
        private static void RestackProgressWindows()
        {
            List<ProgressUI> progress = new List<ProgressUI>();
            foreach (Form form in Application.OpenForms)
                if (form is ProgressUI window)
                    progress.Add(window);
            foreach (ProgressUI window in progress)
                window.KeepOnTop();
        }

        /// <summary>Visible message boxes of this process; <paramref name="oursOnly"/>: only those a tool may answer now.</summary>
        private static List<IntPtr> Dialogs(bool oursOnly)
        {
            uint self = (uint)Process.GetCurrentProcess().Id;
            List<IntPtr> dialogs = new List<IntPtr>();
            bool uiIsOurs = Volatile.Read(ref UiDepth) > 0;
            EnumWindows((hWnd, _) =>
            {
                uint thread = GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == self && IsWindowVisible(hWnd) && ClassOf(hWnd) == "#32770" && (!oursOnly || thread != UiThreadId || uiIsOurs))
                    dialogs.Add(hWnd);
                return true;
            }, IntPtr.Zero);
            return dialogs;
        }

        private sealed class Scope : IDisposable
        {
            private bool _done;
            public void Dispose()
            {
                if (_done) return;
                _done = true;
                Sweep();
                lock (_lock)
                {
                    if (--_watchers == 0)
                    {
                        _timer?.Dispose();
                        _timer = null;
                        _call = null;
                    }
                }
            }
        }

        private static void Sweep()
        {
            foreach (IntPtr dialog in Dialogs(true))
            {
                lock (_lock)
                    if (_answered.Contains(dialog) || _foreign.Contains(dialog) || _call == null) continue;
                string title = TextOf(dialog);
                lock (_lock)
                {
                    if (_usersOwn.Contains(title)) { _foreign.Add(dialog); continue; }
                    if (_foreign.Contains(dialog) || _call == null || !_answered.Add(dialog)) continue;
                }
                List<string> texts = new List<string>();
                HashSet<int> buttons = new HashSet<int>();
                EnumChildWindows(dialog, (child, _) =>
                {
                    string cls = ClassOf(child);
                    if (cls == "Static")
                    {
                        string text = TextOf(child);
                        if (!string.IsNullOrWhiteSpace(text)) texts.Add(text.Trim());
                    }
                    else if (cls == "Button")
                        buttons.Add(GetDlgCtrlID(child));
                    return true;
                }, IntPtr.Zero);

                int answer = new[] { IDCANCEL, IDNO, IDOK, IDIGNORE, IDABORT }.FirstOrDefaultPresent(buttons);
                string answerName = answer == IDNO ? "No" : answer == IDCANCEL ? "Cancel" : answer == IDOK ? "OK" : answer == IDIGNORE ? "Ignore" : answer == IDABORT ? "Abort" : "closed";
                lock (_lock)
                    _call?.Dialogs.Add("\"" + title + "\": " + string.Join(" ", texts) + " [answered " + answerName + "]");
                //A box with only OK ignores a posted IDOK; closing it is how it takes OK
                if (answer != 0 && !(answer == IDOK && buttons.Count == 1))
                    PostMessage(dialog, WM_COMMAND, new IntPtr(answer), GetDlgItem(dialog, answer));
                else
                    PostMessage(dialog, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private static int FirstOrDefaultPresent(this int[] order, HashSet<int> present)
        {
            foreach (int id in order)
                if (present.Contains(id)) return id;
            return 0;
        }

        private static string ClassOf(IntPtr hWnd)
        {
            StringBuilder name = new StringBuilder(64);
            GetClassName(hWnd, name, name.Capacity);
            return name.ToString();
        }

        private static string TextOf(IntPtr hWnd)
        {
            int length = GetWindowTextLength(hWnd);
            if (length <= 0) return "";
            StringBuilder text = new StringBuilder(length + 1);
            GetWindowText(hWnd, text, text.Capacity);
            return text.ToString();
        }
    }
}
