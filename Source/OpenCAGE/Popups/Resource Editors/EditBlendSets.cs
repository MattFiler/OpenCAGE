using CATHODE;
using CathodeLib;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Edit the parametric blend sets in ANIMATION.PAK - the things that turn "aim at 30 degrees up
    /// and 10 left" into a mix of the four clips that surround it.
    ///
    /// A blend set holds two halves. One is authored: the clips, which of them each blend point
    /// plays, how fast, and which parameters drive the whole thing. The other is a baked lookup the
    /// game reads at runtime, computed offline from where the blend points sit. Everything in the
    /// authored half can be changed here and the bake stays valid, because the bake only ever refers
    /// to blend points by number. Moving a point, or adding one, would need the bake redone - that
    /// isn't offered, and the window says so rather than letting someone produce a file that looks
    /// right here and plays wrong in game.
    /// </summary>
    public partial class EditBlendSets : BaseWindow
    {
        private CathodeLib.Animation _animations;
        private GlobalAnimClipDB.BlendSet _set;
        private EditAnimations _clipPicker;
        /* Depth rather than a flag: filling a list changes a selection, which fills the editor
         * under it, and a plain bool would be cleared by the inner call while the outer one was
         * still going - at which point setting a checkbox reads as the user setting it. */
        private int _filling;
        private bool _dirty;

        /// <summary>Raised when the user picks a blend set, with the name that references it.</summary>
        public Action<string> OnPicked;

        private readonly bool _picking;
        private readonly string _startOn;

        public EditBlendSets() : this(false, null) { }

        /// <summary>
        /// Open as a picker, handing the chosen set's name back through <see cref="OnPicked"/>.
        ///
        /// Nothing is filtered: measured across the shipped trees, the blend set a tree references never
        /// belongs to that tree's own animation set, so narrowing the list to the caller's set would
        /// show none of the ones it could actually want.
        /// </summary>
        public EditBlendSets(bool picking, string startingSet = null) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _picking = picking;
            _startOn = startingSet;

            InitializeComponent();
            Icon = SharedFormIcon.Icon;

            if (_picking)
            {
                Text = "Choose a blend set";
                pickBtn.Visible = true;
                pickBtn.Enabled = false;
                pickBtn.Click += PickBtn_Click;
                setTree.DoubleClick += (s, e) => { if (pickBtn.Enabled) pickBtn.PerformClick(); };
            }

            clipList.Columns.Add("#", 34, HorizontalAlignment.Right);
            clipList.Columns.Add("Clip", 300);
            clipList.Columns.Add("Length", 70, HorizontalAlignment.Right);
            clipList.Columns.Add("Mirrored", 70);
            clipList.Columns.Add("Used by", 90, HorizontalAlignment.Right);

            instanceList.Columns.Add("#", 34, HorizontalAlignment.Right);
            instanceList.Columns.Add("Position", 160);
            instanceList.Columns.Add("Plays", 300);
            instanceList.Columns.Add("Speed", 70, HorizontalAlignment.Right);

            userList.Columns.Add("Character", 220);
            userList.Columns.Add("Context", 220);
            userList.Columns.Add("Known as", 200);

            setTree.AfterSelect += (s, e) => ShowSelected();
            searchBox.TextChanged += (s, e) => BuildTree();

            clipList.SelectedIndexChanged += (s, e) => FillClipEditor();
            instanceList.SelectedIndexChanged += (s, e) => FillInstanceEditor();
            spaceView.InstanceSelected += SpaceView_InstanceSelected;

            clipNameBox.Validated += (s, e) => ApplyClip();
            clipDurationBox.Validated += (s, e) => ApplyClip();
            clipMirroredCheck.CheckedChanged += (s, e) => ApplyClip();
            pickClipBtn.Click += PickClipBtn_Click;

            instanceClipBox.SelectedIndexChanged += (s, e) => ApplyInstance();
            instanceSpeedBox.Validated += (s, e) => ApplyInstance();

            addUserBtn.Click += AddUserBtn_Click;
            removeUserBtn.Click += RemoveUserBtn_Click;

            headerLabel.Font = new Font(Font, FontStyle.Bold);
            saveBtn.Enabled = false;

            saveBtn.Click += SaveBtn_Click;
            FormClosing += EditBlendSets_FormClosing;
            FormClosed += (s, e) => { if (_clipPicker != null && !_clipPicker.IsDisposed) _clipPicker.Close(); };

            //parsing the PAK takes a couple of seconds, so let the window appear before it happens
            Load += EditBlendSets_Load;
        }

        private void EditBlendSets_Load(object sender, EventArgs e)
        {
            statusLabel.Text = "Loading ANIMATION.PAK...";
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                _animations = Singleton.Animations;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "ANIMATION.PAK could not be read.";
                MessageBox.Show(ex.Message, "Failed to load animations", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally { Cursor.Current = Cursors.Default; }

            if (_animations?.ClipIndex == null)
            {
                statusLabel.Text = "ANIMATION.PAK holds no blend sets.";
                return;
            }

            BuildTree();

            //open on the set the caller is already referencing, so the choice starts from what is set
            if (!string.IsNullOrEmpty(_startOn))
                Select(_animations.ClipIndex.BlendSets.FirstOrDefault(
                    x => string.Equals(x.Name, _startOn, StringComparison.OrdinalIgnoreCase)));

            ShowSelected();
        }

        #region TREE
        /* Grouped the way the names are built - anim set, then the context inside it, which is how
         * the game asks for them and how anyone looking for one will think of it. */
        private void BuildTree()
        {
            string search = searchBox.Text.Trim();
            GlobalAnimClipDB.BlendSet chosen = _set;

            setTree.BeginUpdate();
            setTree.Nodes.Clear();

            foreach (IGrouping<string, GlobalAnimClipDB.BlendSet> group in _animations.ClipIndex.BlendSets
                .Where(x => Matches(x, search))
                .GroupBy(x => x.AnimSet, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                TreeNode animSet = setTree.Nodes.Add(group.Key + "  (" + group.Count() + ")");
                foreach (IGrouping<string, GlobalAnimClipDB.BlendSet> context in group
                    .GroupBy(x => x.AnimSetContext, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    //"none" is how the data spells "the set itself", so hang those straight off it
                    TreeNode parent = string.Equals(context.Key, "none", StringComparison.OrdinalIgnoreCase)
                        ? animSet : animSet.Nodes.Add(context.Key);

                    foreach (GlobalAnimClipDB.BlendSet set in context.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                        parent.Nodes.Add(new TreeNode(set.Name) { Tag = set });
                }
                animSet.Expand();
            }

            setTree.EndUpdate();
            Select(chosen);
            statusLabel.Text = _animations.ClipIndex.BlendSets.Count + " blend set(s)" + (_dirty ? "   —   unsaved changes" : "");
        }

        private static bool Matches(GlobalAnimClipDB.BlendSet set, string search)
        {
            if (search.Length == 0) return true;
            return set.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || set.Clips.Any(x => (x.Name ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void Select(GlobalAnimClipDB.BlendSet set)
        {
            foreach (TreeNode node in AllNodes(setTree.Nodes))
            {
                if (!ReferenceEquals(node.Tag, set)) continue;
                setTree.SelectedNode = node;
                node.EnsureVisible();
                return;
            }
            setTree.SelectedNode = AllNodes(setTree.Nodes).FirstOrDefault(x => x.Tag is GlobalAnimClipDB.BlendSet);
        }

        private static IEnumerable<TreeNode> AllNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                yield return node;
                foreach (TreeNode child in AllNodes(node.Nodes)) yield return child;
            }
        }
        #endregion

        #region DISPLAY
        private void ShowSelected()
        {
            _set = setTree.SelectedNode?.Tag as GlobalAnimClipDB.BlendSet;
            if (_picking) pickBtn.Enabled = _set != null;
            _filling++;
            try
            {
                FillHeader();
                FillClips();
                FillInstances();
                FillUsers();
                spaceView.LabelFor = _set == null ? (Func<int, string>)null : InstanceLabel;
                spaceView.Set = _set;
            }
            finally { _filling--; }

            FillClipEditor();
            FillInstanceEditor();
        }

        private void FillHeader()
        {
            if (_set == null)
            {
                headerLabel.Text = "";
                noticeLabel.Text = "";
                return;
            }

            headerLabel.Text = _set.ToString() + "   —   " + _set.Dimensions + "D, "
                + _set.Clips.Count + " clip(s), " + _set.PlaySpeeds.Length + " blend point(s)";

            string axes = "driven by " + Describe(_set.BlendPropertyX)
                + (_set.Dimensions > 1 ? " and " + Describe(_set.BlendPropertyY) : "")
                + (_set.Dimensions > 2 ? " and " + Describe(_set.BlendPropertyZ) : "");

            noticeLabel.ForeColor = Color.FromArgb(160, 160, 170);
            noticeLabel.Text = axes + ".  The clips and speeds below can be changed. "
                + "Where the blend points sit is baked into a lookup the game reads, so those are shown but fixed."
                + (_set.Dimensions > 2 ? "  The picture shows the first slice of a 3D blend." : "");
        }

        private static string Describe(string property)
        {
            return string.IsNullOrEmpty(property) ? "(nothing)" : "'" + property + "'";
        }

        private void FillClips()
        {
            clipList.BeginUpdate();
            clipList.Items.Clear();
            if (_set != null)
            {
                for (int i = 0; i < _set.Clips.Count; i++)
                {
                    ListViewItem item = clipList.Items.Add(i.ToString());
                    item.SubItems.Add(_set.Clips[i].Name);
                    item.SubItems.Add(Duration(i).ToString("0.00"));
                    item.SubItems.Add(Mirrored(i) ? "yes" : "");
                    item.SubItems.Add(_set.InstanceToClip.Count(x => x == i).ToString());
                    item.Tag = i;
                }
                if (clipList.Items.Count != 0) clipList.Items[0].Selected = true;
            }
            clipList.EndUpdate();
        }

        private float Duration(int clip)
        {
            return _set != null && clip >= 0 && clip < _set.Durations.Length ? _set.Durations[clip] : 0;
        }

        private bool Mirrored(int clip)
        {
            return _set != null && clip >= 0 && clip < _set.Mirrored.Length && _set.Mirrored[clip];
        }

        private void FillInstances()
        {
            instanceList.BeginUpdate();
            instanceList.Items.Clear();
            instanceClipBox.Items.Clear();

            if (_set != null)
            {
                for (int i = 0; i < _set.Clips.Count; i++) instanceClipBox.Items.Add(i + "  " + _set.Clips[i].Name);

                for (int i = 0; i < _set.PlaySpeeds.Length; i++)
                {
                    ListViewItem item = instanceList.Items.Add(i.ToString());
                    item.SubItems.Add(Position(i));
                    item.SubItems.Add(ClipNameOf(i));
                    item.SubItems.Add(_set.PlaySpeeds[i].ToString("0.###"));
                    item.Tag = i;
                }
                if (instanceList.Items.Count != 0) instanceList.Items[0].Selected = true;
            }
            instanceList.EndUpdate();
        }

        private string Position(int instance)
        {
            if (_set == null) return "";
            List<string> values = new List<string>();
            for (int d = 0; d < _set.Dimensions; d++)
            {
                int at = (instance * _set.Dimensions) + d;
                values.Add(at < _set.InstanceProperties.Length ? _set.InstanceProperties[at].ToString("0.###") : "?");
            }
            return string.Join(", ", values);
        }

        private string ClipNameOf(int instance)
        {
            if (_set == null || instance < 0 || instance >= _set.InstanceToClip.Length) return "";
            int clip = _set.InstanceToClip[instance];
            return clip < _set.Clips.Count ? _set.Clips[clip].Name : "clip " + clip + " (missing)";
        }

        private string InstanceLabel(int instance)
        {
            string name = ClipNameOf(instance);
            return name.Length == 0 ? instance.ToString() : name;
        }

        /* Which characters and contexts can ask for this blend set. A blend set nothing lists is
         * dead weight - the game only reaches one through a character's own clip database. */
        private void FillUsers()
        {
            userList.BeginUpdate();
            userList.Items.Clear();

            if (_set != null)
            {
                string key = _set.ToString();
                foreach (AnimClipDB database in _animations.ClipDatabases)
                {
                    foreach (AnimClipDB.BlendSet reference in database.BlendSets)
                        if (string.Equals(reference.Filename, key, StringComparison.OrdinalIgnoreCase))
                            AddUserRow(database, null, reference);

                    foreach (AnimClipDB.Context context in database.Contexts)
                        foreach (AnimClipDB.BlendSet reference in context.BlendSets)
                            if (string.Equals(reference.Filename, key, StringComparison.OrdinalIgnoreCase))
                                AddUserRow(database, context, reference);
                }
            }
            userList.EndUpdate();
            removeUserBtn.Enabled = userList.Items.Count != 0;
        }

        private void AddUserRow(AnimClipDB database, AnimClipDB.Context context, AnimClipDB.BlendSet reference)
        {
            ListViewItem item = userList.Items.Add(database.Character);
            item.SubItems.Add(context == null ? "(the character itself)" : context.Name);
            item.SubItems.Add(reference.Name);
            item.Tag = new Reference { Database = database, Context = context, Entry = reference };
        }

        private class Reference
        {
            public AnimClipDB Database;
            public AnimClipDB.Context Context;
            public AnimClipDB.BlendSet Entry;
        }
        #endregion

        #region EDITING
        private int SelectedClip
        {
            get { return clipList.SelectedItems.Count == 0 ? -1 : (int)clipList.SelectedItems[0].Tag; }
        }

        private int SelectedInstance
        {
            get { return instanceList.SelectedItems.Count == 0 ? -1 : (int)instanceList.SelectedItems[0].Tag; }
        }

        private void FillClipEditor()
        {
            bool has = _set != null && SelectedClip >= 0;
            clipNameBox.Enabled = clipDurationBox.Enabled = clipMirroredCheck.Enabled = pickClipBtn.Enabled = has;

            _filling++;
            try
            {
                int clip = SelectedClip;
                clipNameBox.Text = has ? _set.Clips[clip].Name : "";
                clipDurationBox.Text = has ? Duration(clip).ToString("0.###") : "";
                clipMirroredCheck.Checked = has && Mirrored(clip);
            }
            finally { _filling--; }
        }

        private void FillInstanceEditor()
        {
            bool has = _set != null && SelectedInstance >= 0;
            instanceClipBox.Enabled = instanceSpeedBox.Enabled = has;

            _filling++;
            try
            {
                int instance = SelectedInstance;
                instanceClipBox.SelectedIndex = has && _set.InstanceToClip[instance] < instanceClipBox.Items.Count
                    ? _set.InstanceToClip[instance] : -1;
                instanceSpeedBox.Text = has ? _set.PlaySpeeds[instance].ToString("0.###") : "";
            }
            finally { _filling--; }

            if (has) spaceView.SelectedInstance = SelectedInstance;
        }

        private void ApplyClip()
        {
            if (_filling != 0 || _set == null) return;
            int clip = SelectedClip;
            if (clip < 0) return;

            bool changed = false;
            string name = clipNameBox.Text.Trim();
            if (name.Length != 0 && name != _set.Clips[clip].Name)
            {
                /* Only the hash of a name is stored, so a name the string table has never seen
                 * writes fine and reads back as a number. Register it and it stays readable. */
                _animations.AddName(name, true);
                _set.Clips[clip].Name = name;
                changed = true;
            }

            if (float.TryParse(clipDurationBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out float duration)
                && clip < _set.Durations.Length && Math.Abs(_set.Durations[clip] - duration) > 1e-6f)
            { _set.Durations[clip] = duration; changed = true; }

            if (clip < _set.Mirrored.Length && _set.Mirrored[clip] != clipMirroredCheck.Checked)
            { _set.Mirrored[clip] = clipMirroredCheck.Checked; changed = true; }

            if (!changed) return;
            MarkDirty();
            RefreshAfterEdit();
        }

        private void ApplyInstance()
        {
            if (_filling != 0 || _set == null) return;
            int instance = SelectedInstance;
            if (instance < 0) return;

            bool changed = false;
            if (instanceClipBox.SelectedIndex >= 0 && instanceClipBox.SelectedIndex < _set.Clips.Count
                && _set.InstanceToClip[instance] != (byte)instanceClipBox.SelectedIndex)
            { _set.InstanceToClip[instance] = (byte)instanceClipBox.SelectedIndex; changed = true; }

            if (float.TryParse(instanceSpeedBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out float speed)
                && Math.Abs(_set.PlaySpeeds[instance] - speed) > 1e-6f)
            { _set.PlaySpeeds[instance] = speed; changed = true; }

            if (!changed) return;
            MarkDirty();
            RefreshAfterEdit();
        }

        /* Redraw the lists without losing where the user was - every edit changes something another
         * list shows, and hunting for your row again after each keystroke would be miserable. */
        private void RefreshAfterEdit()
        {
            int clip = SelectedClip, instance = SelectedInstance;
            _filling++;
            try
            {
                FillClips();
                FillInstances();
                Reselect(clipList, clip);
                Reselect(instanceList, instance);
                spaceView.Set = _set;
                spaceView.SelectedInstance = instance;
            }
            finally { _filling--; }
        }

        private static void Reselect(ListView list, int tag)
        {
            foreach (ListViewItem item in list.Items)
            {
                if (!(item.Tag is int value) || value != tag) continue;
                item.Selected = true;
                item.EnsureVisible();
                return;
            }
        }

        /* Choosing the clip through the animation browser rather than typing it: a blend set names
         * clips the way its own character's database does, and those names are not guessable. */
        private void PickBtn_Click(object sender, EventArgs e)
        {
            //A parametric node references a blend set by its name - measured against the shipped trees
            if (string.IsNullOrEmpty(_set?.Name)) return;

            OnPicked?.Invoke(_set.Name);
            Close();
        }

        private void PickClipBtn_Click(object sender, EventArgs e)
        {
            if (_set == null || SelectedClip < 0) return;

            if (_clipPicker != null && !_clipPicker.IsDisposed) _clipPicker.Close();

            /* A blend set names its clips the way one of its characters does, not the way its anim
             * set is spelled, so open the browser on a character that actually uses it. */
            string startOn = userList.Items.Count != 0
                ? ((Reference)userList.Items[0].Tag).Database.Character
                : _set.AnimSet;

            _clipPicker = new EditAnimations(EditAnimations.PickMode.Animation, startOn, _set.Clips[SelectedClip].Name);
            _clipPicker.Text = "Choose a clip for '" + _set + "'";
            _clipPicker.OnPicked += name =>
            {
                if (string.IsNullOrEmpty(name)) return;
                clipNameBox.Text = name;
                ApplyClip();
                BringToFront();
            };
            _clipPicker.FormClosed += (s, args) => _clipPicker = null;
            _clipPicker.Show();
        }
        #endregion

        #region USERS
        private void AddUserBtn_Click(object sender, EventArgs e)
        {
            if (_set == null) return;

            using (BlendSetUserPicker picker = new BlendSetUserPicker(_animations, _set))
            {
                if (picker.ShowDialog(this) != DialogResult.OK || picker.Database == null) return;

                List<AnimClipDB.BlendSet> into = picker.Context == null ? picker.Database.BlendSets : picker.Context.BlendSets;
                string key = _set.ToString();
                if (into.Any(x => string.Equals(x.Filename, key, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("That already has this blend set.", "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                /* The name is how a tree node asks for the blend set, and the file name is what it
                 * resolves to. Both are only in the debug string table, so register them there or
                 * they come back as numbers. */
                _animations.AddName(_set.Name, true);
                _animations.AddName(key, true);
                into.Add(new AnimClipDB.BlendSet { Name = _set.Name, Filename = key });

                MarkDirty();
                FillUsers();
            }
        }

        private void RemoveUserBtn_Click(object sender, EventArgs e)
        {
            if (userList.SelectedItems.Count == 0) return;
            Reference reference = userList.SelectedItems[0].Tag as Reference;
            if (reference == null) return;

            List<AnimClipDB.BlendSet> from = reference.Context == null ? reference.Database.BlendSets : reference.Context.BlendSets;
            if (!from.Remove(reference.Entry)) return;

            MarkDirty();
            FillUsers();
        }
        #endregion

        #region SAVING
        private void MarkDirty()
        {
            _dirty = true;
            saveBtn.Enabled = true;
            statusLabel.Text = _animations.ClipIndex.BlendSets.Count + " blend set(s)   —   unsaved changes";
        }

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            if (!_dirty) return;

            //no confirmation: the button already said save, and the write is about a second and a half
            Cursor.Current = Cursors.WaitCursor;
            statusLabel.Text = "Writing ANIMATION.PAK...";
            statusLabel.Refresh();
            try
            {
                if (!_animations.Save())
                {
                    MessageBox.Show("ANIMATION.PAK could not be written.", "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                _dirty = false;
                saveBtn.Enabled = false;
                statusLabel.Text = _animations.ClipIndex.BlendSets.Count + " blend set(s)   —   saved";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor.Current = Cursors.Default; }
        }

        private void EditBlendSets_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_dirty || e.CloseReason != CloseReason.UserClosing) return;

            DialogResult answer = MessageBox.Show(
                "There are blend set changes that haven't been written to ANIMATION.PAK.\n\nSave them before closing?",
                "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (answer == DialogResult.Cancel) { e.Cancel = true; return; }
            if (answer == DialogResult.Yes) SaveBtn_Click(sender, EventArgs.Empty);
        }
        #endregion

        private void SpaceView_InstanceSelected(object sender, int instance)
        {
            if (instance < 0) return;
            tabs.SelectedTab = tabInstances;
            Reselect(instanceList, instance);
        }
    }
}
