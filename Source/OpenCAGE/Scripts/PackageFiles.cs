using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The one place a package file is opened from, however it arrived: File &gt; Import Composites &gt;
    /// From Disk, a file dropped on the editor window or the viewport, or a double-click in Explorer
    /// (which reaches a running OpenCAGE through <see cref="PackageHandover"/>, or a fresh one
    /// through the -openfile= argument).
    /// </summary>
    /// <remarks>
    /// Files are queued rather than opened on the spot, because the moment they arrive is rarely a
    /// good one: the process may still be starting, a level may be loading (a package double-clicked
    /// with -level in the launch options meets exactly that), or a modal dialog may be up - and a
    /// Control.BeginInvoke callback runs happily during a modal loop, which would put an import window
    /// on top of a "save before closing?" prompt. A short timer drains the queue once the editor is
    /// idle: window enabled, no level mid-load.
    /// </remarks>
    public static class PackageFiles
    {
        /// <summary>OpenCAGE Composite Package: composites with everything they use.</summary>
        public const string CompositeExtension = ".ocp";
        /// <summary>OpenCAGE Mod Package: a set of changes to the game's files.</summary>
        public const string ModExtension = ".omp";

        /// <summary>A file the process was launched to open; queued once the editor window is up.</summary>
        public static string PendingOpen;

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        private static readonly List<string> _queue = new List<string>();
        private static Timer _drainTimer;
        private static FileSystemWatcher _stashWatcher;
        //The composite import window that is open, if one is: the next package waits for it to close
        private static Form _activeImport;

        public static bool IsPackage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            //Paths arrive from a pipe, a text file and a command line: none of them is trusted to be well formed
            string extension;
            try
            {
                if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    return false;
                extension = Path.GetExtension(path);
            }
            catch
            {
                return false;
            }
            return string.Equals(extension, CompositeExtension, StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ModExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Queue every package in the list. Non-packages are ignored. UI thread only.</summary>
        public static void OpenMany(IEnumerable<string> paths)
        {
            if (paths == null)
                return;
            foreach (string path in paths.Where(IsPackage))
                Enqueue(path);
            Drain();
        }

        /// <summary>
        /// Queue a package to open: the composite import window for an .ocp, the Mod Manager for an
        /// .omp. UI thread only. False when the path is not a package at all.
        /// </summary>
        public static bool Open(string path)
        {
            if (!IsPackage(path))
                return false;
            Enqueue(path);
            Drain();
            return true;
        }

        /// <summary>
        /// Once the editor window is up: queue the file this process was launched with and anything
        /// stashed for it, and start watching the stash for files handed over later.
        /// </summary>
        public static void OpenPending()
        {
            string path = PendingOpen;
            PendingOpen = null;
            if (IsPackage(path))
                Enqueue(path);
            //Watch first, then take: a stash written in between is then seen by the watcher. Only the
            //primary looks after the stash - a child instance has a different game directory.
            WatchStash();
            if (PrimaryInstanceLock.IsHeld)
                TakeStashIntoQueue();
            Drain();
        }

        /// <summary>
        /// An explicit File &gt; Import from Disk supersedes an import window already up: the user
        /// asked for this file now, not after the other one is dealt with.
        /// </summary>
        public static void CloseActiveImport()
        {
            Form active = _activeImport;
            _activeImport = null;
            if (active != null && !active.IsDisposed)
            {
                try { active.Close(); } catch { }
            }
        }

        private static void Enqueue(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            path = path.Trim();
            lock (_queue)
            {
                if (!_queue.Contains(path, StringComparer.OrdinalIgnoreCase))
                    _queue.Add(path);
            }
        }

        private static void TakeStashIntoQueue()
        {
            foreach (string path in PackageHandover.TakeStashed())
                if (IsPackage(path))
                    Enqueue(path);
        }

        /* Only the primary serves handovers, so only it watches the stash; a change on the watcher's
           thread just asks the UI thread to look */
        private static void WatchStash()
        {
            if (_stashWatcher != null || !PrimaryInstanceLock.IsHeld)
                return;
            try
            {
                string directory = Path.GetDirectoryName(PackageHandover.StashPath);
                string file = Path.GetFileName(PackageHandover.StashPath);
                _stashWatcher = new FileSystemWatcher(directory, file) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
                FileSystemEventHandler changed = (s, e) => ScheduleDrain();
                _stashWatcher.Changed += changed;
                _stashWatcher.Created += changed;
                _stashWatcher.Renamed += (s, e) => ScheduleDrain();
                _stashWatcher.EnableRaisingEvents = true;
            }
            catch
            {
                _stashWatcher = null;
            }
        }

        private static void ScheduleDrain()
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;
            try { editor.BeginInvoke(new Action(Drain)); } catch { }
        }

        /// <summary>Open what is queued if the editor can take it now; otherwise look again shortly.</summary>
        private static void Drain()
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed || editor.Disposing)
                return;

            if (PrimaryInstanceLock.IsHeld && PackageHandover.HasStash())
                TakeStashIntoQueue();

            List<string> ready;
            lock (_queue)
            {
                if (_queue.Count == 0)
                    return;
                if (!EditorCanOpen(editor))
                {
                    RetrySoon(editor);
                    return;
                }
                ready = new List<string>(_queue);
                _queue.Clear();
            }

            BringEditorForward(editor);

            //One at a time: the first opens now, the rest go back on the queue for when its window closes
            OpenNow(editor, ready[0]);
            if (ready.Count > 1)
            {
                lock (_queue)
                {
                    for (int i = ready.Count - 1; i >= 1; i--)
                        _queue.Insert(0, ready[i]);
                }
                RetrySoon(editor);
            }
        }

        /* Not while a modal dialog owns the thread (the native enabled state is what the modal loop
           changes; Control.Enabled does not follow it), not while a save is pumping messages from
           inside SaveLevel (the undo stack is blocked for exactly that), not while a level is part-way
           through loading - neither "open" nor "not open" - and not while an import window is already
           up: several packages dropped together open one after another, not stacked. */
        private static bool EditorCanOpen(CommandsEditor editor)
        {
            if (!editor.IsHandleCreated || !editor.Visible)
                return false;
            try
            {
                if (!IsWindowEnabled(editor.Handle))
                    return false;
            }
            catch { }
            if (editor.IsLevelLoadInProgress)
                return false;
            if (OpenCAGE.Undo.UndoStack.Current != null && OpenCAGE.Undo.UndoStack.Current.Blocked)
                return false;
            if (_activeImport != null && !_activeImport.IsDisposed && _activeImport.Visible)
                return false;
            return true;
        }

        private static void RetrySoon(CommandsEditor editor)
        {
            if (_drainTimer == null)
            {
                _drainTimer = new Timer() { Interval = 500 };
                _drainTimer.Tick += (s, e) =>
                {
                    _drainTimer.Stop();
                    Drain();
                };
            }
            if (!_drainTimer.Enabled)
                _drainTimer.Start();
        }

        private static void OpenNow(CommandsEditor editor, string path)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show("The file could not be found:\n\n" + path, "Cannot open package", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string extension = Path.GetExtension(path);
            if (string.Equals(extension, CompositeExtension, StringComparison.OrdinalIgnoreCase))
                OpenCompositePackage(path);
            else
                OpenModPackage(editor, path);
        }

        private static void OpenCompositePackage(string path)
        {
            //Only the header is read here: enough to show what the package holds before anything is unpacked
            CompositeArchive.Manifest manifest;
            try
            {
                manifest = CompositeArchive.ReadHeader(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("That file could not be read as a composite package:\n\n" + ex.Message, "Cannot import", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (manifest.Composites.Count == 0)
            {
                MessageBox.Show("This package holds no composites.", "Nothing to import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ImportCompositeArchive window = new ImportCompositeArchive(path, manifest);
            _activeImport = window;
            window.FormClosed += (s, e) =>
            {
                if (ReferenceEquals(_activeImport, window))
                    _activeImport = null;
                ScheduleDrain();
            };
            window.Show();
        }

        private static void OpenModPackage(CommandsEditor editor, string path)
        {
#if ENABLE_MOD_PACKAGES
            editor.OpenModManager(path);
#else
            MessageBox.Show("Mod packages aren't supported by this build of OpenCAGE.", "Cannot open package", MessageBoxButtons.OK, MessageBoxIcon.Information);
#endif
        }

        /* A double-click brings OpenCAGE to the front along with the dialog it opens. The sender has
           granted this process the foreground right (see PackageHandover); a file opened from within
           the editor is already in front. */
        private static void BringEditorForward(CommandsEditor editor)
        {
            try
            {
                if (editor.WindowState == FormWindowState.Minimized)
                    editor.WindowState = FormWindowState.Normal;
                editor.Activate();
            }
            catch { }
        }
    }
}
