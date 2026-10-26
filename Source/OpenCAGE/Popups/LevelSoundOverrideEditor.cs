using OpenCAGE.Audio;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// What the open level's own sound package (WORLD/LEVEL_SOUND_OVERRIDE.PCK) holds: each soundbank and stream in it, and
    /// what in the base game it stands in for on this level. Opened from the Sound Editor. Sounds get into the package
    /// through the Sound Editor's Replace > For This Level Only; here whole files can be added (a soundbank built with
    /// Wwise, or a stream named for its id), exported, or taken out - a soundbank or stream taken out is read from
    /// DATA/SOUND again on this level. Built in code: a summary, the list and a row of buttons.
    /// </summary>
    public class LevelSoundOverrideEditor : BaseWindow
    {
        private readonly string _package;

        private readonly Label summary = new Label { Dock = DockStyle.Top, Height = 54, Padding = new Padding(6, 6, 6, 0) };
        private readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = true };
        private readonly FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
        private readonly Button closeBtn = new Button { Text = "Close", Width = 90, Height = 26 };
        private readonly Button removeBtn = new Button { Text = "Remove", Width = 90, Height = 26 };
        private readonly Button exportBtn = new Button { Text = "Export...", Width = 90, Height = 26 };
        private readonly Button addBtn = new Button { Text = "Add Files...", Width = 100, Height = 26 };
        private readonly ToolTip tips = new ToolTip();

        private sealed class Row
        {
            public bool IsBank;
            public WwiseFilePackageBuilder.Item Item;
            public string Name;
        }

        public LevelSoundOverrideEditor(string package)
        {
            _package = package;

            Text = "Level Sound Override";
            Icon = SharedFormIcon.Icon;
            ClientSize = new Size(760, 420);
            MinimumSize = new Size(520, 280);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/sound-events/#level-sound-override");

            list.Columns.Add("File", 230);
            list.Columns.Add("Kind", 80);
            list.Columns.Add("Language", 90);
            list.Columns.Add("Size", 80, HorizontalAlignment.Right);
            list.Columns.Add("On this level", 250);

            tips.SetToolTip(addBtn, "Put a soundbank (.bnk, built with the game's Wwise version) or a stream (.wem named for its id) in the package. One with the same id as the base game's is read instead of it on this level - except a permanent soundbank, which the game loads before any level.");
            tips.SetToolTip(exportBtn, "Save the selected soundbank or stream out of the package.");
            tips.SetToolTip(removeBtn, "Take the selected files out of the package: on this level the game reads the base game's instead (or nothing, for a file only this level has).");
            addBtn.Click += (s, e) => AddFiles();
            exportBtn.Click += (s, e) => Export();
            removeBtn.Click += (s, e) => RemoveSelected();
            closeBtn.Click += (s, e) => Close();
            list.SelectedIndexChanged += (s, e) => UpdateButtons();
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Delete) return;
                e.Handled = true;
                RemoveSelected();
            };

            //Right to left: Close at the far right, then what acts on the selection, then Add
            buttons.Controls.AddRange(new Control[] { closeBtn, removeBtn, exportBtn, addBtn });

            //Docked from the edges in: the summary and buttons first, then the list in what is left
            Controls.Add(list);
            Controls.Add(buttons);
            Controls.Add(summary);

            CancelButton = closeBtn;
            Load += (s, e) => Rebuild();
            FormClosed += (s, e) => tips.Dispose();
        }

        private void Rebuild()
        {
            list.BeginUpdate();
            list.Items.Clear();

            string relative = _package;
            if (!string.IsNullOrEmpty(Singleton.PathToAI) && _package.StartsWith(Path.GetFullPath(Singleton.PathToAI), StringComparison.OrdinalIgnoreCase))
                relative = _package.Substring(Path.GetFullPath(Singleton.PathToAI).Length).TrimStart('\\', '/');

            if (!File.Exists(_package))
            {
                summary.Text = "This level has no sound package yet (" + relative + ").\nReplace a sound with Replace > For This Level Only in the Sound Editor to start one, or add a soundbank or stream here.";
                list.EndUpdate();
                UpdateButtons();
                return;
            }

            WwiseFilePackageBuilder package;
            try
            {
                package = WwiseFilePackageBuilder.Load(_package);
            }
            catch (Exception e)
            {
                summary.Text = relative + " can't be read: " + e.Message;
                list.EndUpdate();
                UpdateButtons();
                return;
            }

            string soundDirectory = SoundPreviewLibrary.SoundDirectory;
            foreach (WwiseFilePackageBuilder.Item bank in package.Banks)
            {
                string baseFile = LevelSoundOverride.BaseBankFile(bank.Id, null);
                string name = LevelSoundOverride.BankName(bank, null);
                Add(new Row { IsBank = true, Item = bank, Name = name }, "Soundbank", package,
                    LevelSoundOverride.IsPermanent(bank.Id, soundDirectory) ? "Never read: a permanent soundbank, loaded before any level"
                    : baseFile != null ? "Read instead of DATA\\SOUND\\" + Path.GetFileName(baseFile)
                    : "Only this level has it");
            }
            foreach (WwiseFilePackageBuilder.Item stream in package.Streams)
            {
                bool shipped = soundDirectory != null && File.Exists(Path.Combine(soundDirectory, stream.Id + ".wem"));
                Add(new Row { IsBank = false, Item = stream, Name = stream.Id + ".wem" }, "Stream", package,
                    shipped ? "Read instead of DATA\\SOUND\\" + stream.Id + ".wem" : "Streamed audio this level supplies");
            }

            summary.Text = relative + "  -  " + LevelSoundOverride.Describe(new FileInfo(_package).Length) + "\n"
                + package.Banks.Count + " soundbank" + (package.Banks.Count == 1 ? "" : "s") + " and " + package.Streams.Count + " stream" + (package.Streams.Count == 1 ? "" : "s")
                + ". The game reads these instead of the base game's files of the same id, on this level only.";

            list.EndUpdate();
            UpdateButtons();
        }

        private void Add(Row row, string kind, WwiseFilePackageBuilder package, string onThisLevel)
        {
            string language;
            if (!package.Languages.TryGetValue(row.Item.LanguageId, out language))
                language = row.Item.LanguageId.ToString();

            ListViewItem item = new ListViewItem(new[] { row.Name, kind, language, LevelSoundOverride.Describe(row.Item.Length), onThisLevel });
            item.Tag = row;
            list.Items.Add(item);
        }

        private List<Row> Selected()
        {
            return list.SelectedItems.Cast<ListViewItem>().Select(o => (Row)o.Tag).ToList();
        }

        private void UpdateButtons()
        {
            int selected = list.SelectedItems.Count;
            removeBtn.Enabled = selected != 0;
            exportBtn.Enabled = selected == 1;
        }

        private void AddFiles()
        {
            string[] files;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Wwise Soundbanks and Streams|*.bnk;*.wem|Wwise Soundbank|*.bnk|Wwise Stream (named for its id)|*.wem";
                dialog.Title = "Add to the level's sound package";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                files = dialog.FileNames;
            }

            List<string> problems = new List<string>();
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                foreach (string file in files)
                {
                    LevelSoundOverride.Plan plan = LevelSoundOverride.Add(_package, file, true);
                    if (!plan.Ok)
                        problems.Add(Path.GetFileName(file) + ": " + plan.Problem);
                }
            }
            finally { Cursor.Current = Cursors.Default; }

            Changed();
            if (problems.Count != 0)
                MessageBox.Show(this, "Not everything could be added:\n\n" + string.Join("\n", problems), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void Export()
        {
            List<Row> selected = Selected();
            if (selected.Count != 1)
                return;

            Row row = selected[0];
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                string extension = row.IsBank ? ".bnk" : ".wem";
                dialog.Filter = row.IsBank ? "Wwise Soundbank|*.bnk" : "Wwise Stream|*.wem";
                dialog.FileName = row.IsBank ? Path.GetFileNameWithoutExtension(row.Name) + extension : row.Name;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    File.WriteAllBytes(dialog.FileName, row.Item.Read());
                }
                catch (Exception e)
                {
                    MessageBox.Show(this, "Could not save it:\n\n" + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void RemoveSelected()
        {
            List<Row> selected = Selected();
            if (selected.Count == 0)
                return;

            string what = selected.Count == 1 ? selected[0].Name : selected.Count + " files";
            if (MessageBox.Show(this, "Take " + what + " out of the level's sound package?\n\nOn this level the game will read the base game's "
                    + (selected.Count == 1 ? "file" : "files") + " of the same id instead - or nothing, for a file only this level has. "
                    + "The level's package is the only copy of anything changed in it.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            LevelSoundOverride.Plan plan;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                plan = LevelSoundOverride.Remove(_package, selected.Select(o => new KeyValuePair<bool, uint>(o.IsBank, o.Item.Id)), true);
            }
            finally { Cursor.Current = Cursors.Default; }

            Changed();
            if (!plan.Ok)
                MessageBox.Show(this, plan.Problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void Changed()
        {
            Rebuild();
            LevelSoundOverride.RaiseChanged();
        }
    }
}
