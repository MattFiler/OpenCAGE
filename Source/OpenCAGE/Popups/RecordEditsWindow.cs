using OpenCAGE.LevelDiff;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using OpenCAGE.Recipes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// File > Record Edits: records undo steps into a recipe, and replays a recipe (dry run first, then Apply as one undo step).
    /// Recording carries on with the window closed.
    /// </summary>
    public class RecordEditsWindow : BaseWindow
    {
        private static RecordEditsWindow _open;

        private readonly Label _state, _planSummary;
        private readonly Button _record, _stop, _discard, _openRecipe, _apply, _applyAll;
        private readonly ListBox _steps;
        private readonly ListView _plan;
        private readonly ComboBox _ids;
        private Recipe _recipe;
        private string _recipePath;
        private RecipePlan _current;
        private readonly Action _onChanged;

        public RecordEditsWindow() : base(WindowClosesOn.NONE)
        {
            Text = "Record Edits";
            Icon = SharedFormIcon.Icon;
            Size = new Size(980, 700);
            MinimumSize = new Size(700, 480);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/record-edits/");

            SplitContainer split = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 250 };

            //Recording
            GroupBox recording = new GroupBox() { Text = "Record", Dock = DockStyle.Fill, Padding = new Padding(8) };
            Panel row = new Panel() { Dock = DockStyle.Top, Height = 34 };
            _record = new Button() { Text = "Record", AutoSize = true, Location = new Point(0, 4) };
            _record.Click += (s, e) => StartRecording();
            _stop = new Button() { Text = "Stop && Save Recipe...", AutoSize = true, Location = new Point(90, 4) };
            _stop.Click += (s, e) => StopRecording();
            _discard = new Button() { Text = "Discard", AutoSize = true, Location = new Point(250, 4) };
            _discard.Click += (s, e) => { RecipeRecorder.Discard(); RefreshState(); };
            _state = new Label() { AutoSize = false, Location = new Point(340, 9), Size = new Size(580, 20), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
            row.Controls.Add(_record); row.Controls.Add(_stop); row.Controls.Add(_discard); row.Controls.Add(_state);
            _steps = new ListBox() { Dock = DockStyle.Fill, IntegralHeight = false };
            Label hint = new Label() { Dock = DockStyle.Bottom, Height = 34, ForeColor = SystemColors.GrayText, Text = "Every edit counts - the editor's windows, the viewport, AI tools - and what you undo while recording is left out. The recipe holds the net change, with what each thing was before, so a replay can tell when the target differs." };
            recording.Controls.Add(_steps);
            recording.Controls.Add(hint);
            recording.Controls.Add(row);
            split.Panel1.Controls.Add(recording);
            split.Panel1.Padding = new Padding(8, 8, 8, 4);

            //Replaying
            GroupBox replay = new GroupBox() { Text = "Replay", Dock = DockStyle.Fill, Padding = new Padding(8) };
            Panel replayRow = new Panel() { Dock = DockStyle.Top, Height = 34 };
            _openRecipe = new Button() { Text = "Open Recipe...", AutoSize = true, Location = new Point(0, 4) };
            _openRecipe.Click += (s, e) => OpenRecipe();
            Label idsLabel = new Label() { Text = "Ids:", AutoSize = true, Location = new Point(120, 9) };
            _ids = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(150, 5), Width = 230 };
            _ids.Items.AddRange(new object[] { "auto (keep on a copy, else new)", "keep the recorded ids", "new ids (remap)" });
            _ids.SelectedIndex = 0;
            _ids.SelectedIndexChanged += (s, e) => Replan();
            _apply = new Button() { Text = "Apply", AutoSize = true, Location = new Point(400, 4), Enabled = false };
            _apply.Click += (s, e) => Apply(false);
            _applyAll = new Button() { Text = "Apply, Overwriting Conflicts", AutoSize = true, Location = new Point(490, 4), Enabled = false };
            _applyAll.Click += (s, e) => Apply(true);
            replayRow.Controls.Add(_openRecipe); replayRow.Controls.Add(idsLabel); replayRow.Controls.Add(_ids); replayRow.Controls.Add(_apply); replayRow.Controls.Add(_applyAll);
            _planSummary = new Label() { Dock = DockStyle.Top, Height = 40, Text = "Open a recipe to see what it would do here (nothing changes until Apply)." };
            _plan = new ListView() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false };
            _plan.Columns.Add("#", 44);
            _plan.Columns.Add("Status", 100);
            _plan.Columns.Add("Step", 520);
            _plan.Columns.Add("Note", 260);
            replay.Controls.Add(_plan);
            replay.Controls.Add(_planSummary);
            replay.Controls.Add(replayRow);
            split.Panel2.Controls.Add(replay);
            split.Panel2.Padding = new Padding(8, 4, 8, 8);

            Controls.Add(split);
            //The splitter can only be placed once the window has its size
            Load += (s, e) => { try { split.SplitterDistance = Math.Max(160, ClientSize.Height * 2 / 5); } catch { } };
            _onChanged = () => { if (!IsDisposed) BeginInvoke(new Action(RefreshState)); };
            RecipeRecorder.Changed += _onChanged;
            FormClosed += (s, e) => RecipeRecorder.Changed -= _onChanged;
            Theming.ThemeManager.ApplyCurrent(this);
            RefreshState();
        }

        public static RecordEditsWindow Open()
        {
            if (_open == null || _open.IsDisposed)
            {
                _open = new RecordEditsWindow();
                _open.FormClosed += (s, e) => _open = null;
                if (Singleton.Editor != null) _open.Show(Singleton.Editor); else _open.Show();
            }
            else
            {
                if (_open.WindowState == FormWindowState.Minimized) _open.WindowState = FormWindowState.Normal;
                _open.Activate();
            }
            return _open;
        }

        #region Recording
        private void RefreshState()
        {
            bool open = LevelSides.OpenLevel?.Commands != null;
            bool recording = RecipeRecorder.Recording;
            _record.Enabled = open && !recording;
            _stop.Enabled = recording || RecipeRecorder.Orphan != null;
            _discard.Enabled = recording || RecipeRecorder.Orphan != null;
            if (recording)
            {
                int n = RecipeRecorder.StepCount;
                _state.Text = "Recording " + RecipeRecorder.LevelName + " since " + RecipeRecorder.Started.ToString("HH:mm") + " - " + (n < 0 ? "the undo history was cleared (the recipe is still complete)" : n + (n == 1 ? " undo step" : " undo steps") + " so far");
                _steps.BeginUpdate();
                _steps.Items.Clear();
                foreach (string label in RecipeRecorder.StepLabels()) _steps.Items.Add(label);
                _steps.EndUpdate();
            }
            else if (RecipeRecorder.Orphan != null) _state.Text = "Recording stopped when its level closed: Stop & Save Recipe keeps it.";
            else _state.Text = open ? "Not recording." : "Open a level to record.";
            _apply.Enabled = _applyAll.Enabled = _current != null && open;
        }

        private void StartRecording()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                RecipeRecorder.Start(LevelSides.OpenLevel);
                _steps.Items.Clear();
            }
            catch (Exception e) { MessageBox.Show(this, "Could not start recording: " + e.Message, "Record Edits", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { Cursor = Cursors.Default; }
            RefreshState();
        }

        private void StopRecording()
        {
            Recipe recipe;
            try
            {
                Cursor = Cursors.WaitCursor;
                recipe = RecipeRecorder.Recording ? RecipeRecorder.Stop("Recorded edits") : RecipeRecorder.TakeOrphan();
            }
            catch (Exception e) { MessageBox.Show(this, "Could not stop recording: " + e.Message, "Record Edits", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            finally { Cursor = Cursors.Default; RefreshState(); }
            if (recipe == null) return;
            if (recipe.Ops.Count == 0) { MessageBox.Show(this, "Nothing changed while recording, so there is no recipe to save.", "Record Edits", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string path = McpLevelDiffTools.DefaultRecipePath(recipe.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (SaveFileDialog dialog = new SaveFileDialog() { Filter = "OpenCAGE recipe (*" + Recipe.Extension + ")|*" + Recipe.Extension, InitialDirectory = Path.GetDirectoryName(path), FileName = Path.GetFileName(path) })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    if (MessageBox.Show(this, "Throw the recording away?", "Record Edits", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) { recipe.Save(path); _state.Text = "Saved " + path; }
                    return;
                }
                recipe.Name = Path.GetFileNameWithoutExtension(dialog.FileName);
                recipe.Save(dialog.FileName);
                _state.Text = "Saved " + dialog.FileName + ": " + recipe.Summary() + (recipe.NotCarried.Count != 0 ? " (" + recipe.NotCarried.Count + " things not carried: see the file's header)" : "");
            }
        }
        #endregion

        #region Replaying
        private void OpenRecipe()
        {
            using (OpenFileDialog dialog = new OpenFileDialog() { Filter = "OpenCAGE recipe (*" + Recipe.Extension + ")|*" + Recipe.Extension + "|All files|*.*", InitialDirectory = Directory.Exists(McpLevelDiffTools.RecipeFolder) ? McpLevelDiffTools.RecipeFolder : null })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _recipe = Recipe.Read(dialog.FileName);
                    _recipePath = dialog.FileName;
                }
                catch (Exception e) { MessageBox.Show(this, "That is not a recipe this OpenCAGE reads: " + e.Message, "Replay", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            }
            Replan();
        }

        private string IdsMode => _ids.SelectedIndex == 1 ? "keep" : _ids.SelectedIndex == 2 ? "remap" : "auto";

        private void Replan()
        {
            _current = null;
            _plan.Items.Clear();
            if (_recipe == null) { RefreshState(); return; }
            try
            {
                Cursor = Cursors.WaitCursor;
                _current = RecipeReplay.Plan(_recipe, IdsMode);
            }
            catch (Exception e) { _planSummary.Text = "Can't plan it: " + e.Message; RefreshState(); return; }
            finally { Cursor = Cursors.Default; }
            ShowPlan();
            RefreshState();
        }

        private void ShowPlan()
        {
            _planSummary.Text = Path.GetFileName(_recipePath) + " (recorded on " + (_recipe.Level ?? "?") + ", " + _recipe.Summary() + ")\r\nHere: " + _current.Summary() +
                (_recipe.NotCarried.Count != 0 ? " Not carried: " + string.Join("; ", _recipe.NotCarried.Take(3)) + (_recipe.NotCarried.Count > 3 ? "..." : "") : "");
            _plan.BeginUpdate();
            _plan.Items.Clear();
            foreach (PlannedStep step in _current.Steps)
            {
                ListViewItem item = new ListViewItem(new[] { (step.Index + 1).ToString(), step.Merge ? "ready (merge)" : step.Status.ToString().ToLowerInvariant(), step.Describe(), step.Result ?? step.Note ?? "" });
                if (step.Status == StepStatus.Conflict) item.ForeColor = Color.DarkOrange;
                else if (step.Status == StepStatus.Missing || step.Status == StepStatus.Failed) item.ForeColor = Color.Firebrick;
                else if (step.Status == StepStatus.Done) item.ForeColor = SystemColors.GrayText;
                _plan.Items.Add(item);
            }
            _plan.EndUpdate();
        }

        private void Apply(bool overwrite)
        {
            if (_current == null) return;
            if (overwrite && _current.Count(StepStatus.Conflict) != 0 &&
                MessageBox.Show(this, _current.Count(StepStatus.Conflict) + " steps would write over what is there now. Go ahead?", "Replay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                Cursor = Cursors.WaitCursor;
                RecipeReplay.Outcome outcome = RecipeReplay.Apply(_current, overwrite);
                ShowPlan();
                _planSummary.Text = "Applied: " + outcome.Made + " steps made" + (outcome.Changed ? " as one undo step ('" + outcome.Label + "'). Save (or Save & Build, when it placed or moved models, lights or collision) to keep it." : ".") + "\r\n" + _current.Summary();
                _current = null;
            }
            catch (Exception e) { MessageBox.Show(this, "The replay failed: " + e.Message, "Replay", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { Cursor = Cursors.Default; RefreshState(); }
        }
        #endregion
    }
}
