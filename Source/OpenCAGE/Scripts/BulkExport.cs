using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// What the texture and model browsers' Export All buttons share: the format asked for up front,
    /// then everything written on a worker behind a progress bar.
    /// </summary>
    public static class BulkExport
    {
        /// <summary>How many failures the closing message names before it just counts the rest.</summary>
        private const int FailuresListed = 10;

        public class Result
        {
            /// <summary>How many were written.</summary>
            public int Exported;

            /// <summary>One line for each that wasn't: its name, and why.</summary>
            public readonly List<string> Failures = new List<string>();

            /// <summary>"Exported 340 models.", followed by what couldn't be, if anything.</summary>
            public string Describe(string singular, string plural)
            {
                string text = "Exported " + Exported.ToString("N0") + " " + (Exported == 1 ? singular : plural) + ".";
                if (Failures.Count == 0)
                    return text;

                text += "\n\n" + Failures.Count.ToString("N0") + " couldn't be exported:\n" + string.Join("\n", Failures.Take(FailuresListed));
                if (Failures.Count > FailuresListed)
                    text += "\n...and " + (Failures.Count - FailuresListed).ToString("N0") + " more.";
                return text;
            }
        }

        /// <summary>
        /// Tells things apart by identity. Models and textures compare by value, which would take a copy
        /// of a model for the model itself, and make every comparison a deep one.
        /// </summary>
        public sealed class ByReference<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ByReference<T> Instance = new ByReference<T>();
            public bool Equals(T x, T y) { return ReferenceEquals(x, y); }
            public int GetHashCode(T obj) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj); }
        }

        /// <summary>
        /// Ask which format to export everything as. Returns the index picked from <paramref name="formats"/>,
        /// or -1 if the export was cancelled.
        /// </summary>
        public static int AskFormat(IWin32Window owner, IList<string> formats, int selected, string note = null)
        {
            int width = note == null ? 256 : 336;
            using (Form f = new Form
            {
                Text = "Export all as",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                Icon = SharedFormIcon.Icon
            })
            {
                int y = 12;
                if (note != null)
                {
                    Label label = new Label { Text = note, Location = new Point(12, y), AutoSize = false };
                    label.Size = new Size(width, label.GetPreferredSize(new Size(width, 0)).Height);
                    f.Controls.Add(label);
                    y += label.Height + 8;
                }

                ComboBox cb = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(12, y),
                    Width = width
                };
                foreach (string format in formats)
                    cb.Items.Add(format);
                cb.SelectedIndex = selected >= 0 && selected < formats.Count ? selected : 0;
                y += cb.Height + 15;

                Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(width - 156, y), Width = 80 };
                Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(width - 68, y), Width = 80 };
                f.Controls.Add(cb);
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.ClientSize = new Size(width + 24, y + ok.Height + 12);
                Theming.ThemeManager.ApplyToForm(f);

                if (f.ShowDialog(owner) != DialogResult.OK)
                    return -1;
                return cb.SelectedIndex;
            }
        }

        /// <summary>
        /// Where something called <paramref name="name"/> is written under <paramref name="folder"/>: in the
        /// folders its name gives, with <paramref name="extension"/> in place of its own. Characters Windows
        /// won't take in a path become underscores, and a number is added if something earlier in the same
        /// export (recorded in <paramref name="taken"/>) already has the path. The folder is created.
        /// </summary>
        public static string PathFor(string folder, string name, string extension, HashSet<string> taken)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            List<string> parts = new List<string>();
            foreach (string part in (name ?? "").Split('/', '\\'))
            {
                //Windows drops trailing dots and spaces itself, which would put "." and ".." anywhere
                string clean = new string(part.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
                if (clean.Length != 0)
                    parts.Add(clean);
            }

            string file = parts.Count == 0 ? "unnamed" : parts[parts.Count - 1];
            if (parts.Count != 0)
                parts.RemoveAt(parts.Count - 1);
            string stem = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(stem))
                stem = file;

            string directory = parts.Count == 0 ? folder : Path.Combine(folder, Path.Combine(parts.ToArray()));
            string path = Path.Combine(directory, stem + extension);
            for (int i = 2; !taken.Add(path); i++)
                path = Path.Combine(directory, stem + "_" + i + extension);

            Directory.CreateDirectory(directory);
            return path;
        }

        /// <summary>
        /// Run <paramref name="export"/> on each item, on a worker while this thread pumps messages, with a
        /// progress bar counting through them. The same hold as a save: <paramref name="window"/> and the
        /// editor are disabled, the level picker closed and undo blocked until it's done, so nothing can
        /// change what is being written. An item that throws is recorded and the rest carry on.
        /// </summary>
        public static Result Run<T>(Form window, string title, IList<T> items, Func<T, string> name, Action<T> export)
        {
            Result result = new Result();

            CommandsEditor editor = Singleton.Editor;
            editor?.CloseLevelPicker();
            //A window opened as a dialog already has the editor disabled, and must leave it that way
            bool editorWasEnabled = editor != null && editor.Enabled;
            if (editor != null) editor.Enabled = false;
            window.Enabled = false;
            window.UseWaitCursor = true;
            OpenCAGE.Undo.UndoStack undo = OpenCAGE.Undo.UndoStack.Current;
            bool undoWasBlocked = undo != null && undo.Blocked;
            if (undo != null) undo.Blocked = true;

            ProgressUI progress = new ProgressUI();
            try
            {
                progress.ShowCounted(title, items.Count);

                int done = 0;
                Task work = Task.Run(() =>
                {
                    foreach (T item in items)
                    {
                        try
                        {
                            export(item);
                            result.Exported++;
                        }
                        catch (Exception ex)
                        {
                            result.Failures.Add(name(item) + ": " + ex.Message);
                        }
                        Interlocked.Increment(ref done);
                    }
                });
                while (!work.IsCompleted)
                {
                    progress.SetCount(Volatile.Read(ref done));
                    Application.DoEvents();
                    Thread.Sleep(16);
                }
                work.GetAwaiter().GetResult();
            }
            finally
            {
                progress.Close();
                progress.Dispose();
                if (undo != null) undo.Blocked = undoWasBlocked;
                if (editor != null && !editor.IsDisposed) editor.Enabled = editorWasEnabled;
                if (!window.IsDisposed)
                {
                    window.UseWaitCursor = false;
                    window.Enabled = true;
                }
            }
            return result;
        }
    }
}
