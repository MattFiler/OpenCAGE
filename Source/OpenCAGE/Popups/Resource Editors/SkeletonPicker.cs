using CATHODE;
using CathodeLib;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AlienPAK
{
    /// <summary>
    /// Asks which skeleton to write into an exported model. The list is scored against the model's
    /// own skin weights, so the rig it was actually authored against sorts to the top.
    /// </summary>
    public partial class SkeletonPicker : Form
    {
        /// <summary>The skeleton to export with, or null for none.</summary>
        public Skeleton Result { get; private set; }

        private readonly Models.CS2 _model;
        private readonly int _requiredBones;
        private readonly List<Candidate> _candidates = new List<Candidate>();

        private class Candidate
        {
            public SkeletonDB.SkeletonEntry Entry;
            public Skeleton Skeleton;
            public float Fit = -1;
            public bool FitsBoneCount;
        }

        public SkeletonPicker(Models.CS2 model)
        {
            _model = model;
            _requiredBones = Skeleton.RequiredBoneCount(model);
            InitializeComponent();
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
            Icon = SharedFormIcon.Icon;
            Text = "Choose a skeleton";

            skeletonList.Columns.Add("Skeleton", 260);
            skeletonList.Columns.Add("Bones", 60, HorizontalAlignment.Right);
            skeletonList.Columns.Add("Fit", 110, HorizontalAlignment.Right);

            searchBox.TextChanged += (s, e) => Populate();
            skeletonList.SelectedIndexChanged += (s, e) => UpdateButtons();
            skeletonList.DoubleClick += (s, e) => { if (GetSelected() != null) Accept(); };
            okBtn.Click += (s, e) => Accept();
            noneBtn.Click += (s, e) => { Result = null; DialogResult = DialogResult.OK; Close(); };
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Load += (s, e) => Score();
        }

        /* Load every skeleton once and rank them by how close their bones sit to the vertices
         * weighted to them. It's a few hundred small files, so this is quick enough to do up front. */
        private void Score()
        {
            SkeletonDB db = Singleton.Global?.Skeletons;
            if (db == null)
            {
                statusLabel.Text = "The animation data hasn't been loaded, so no skeletons are available.";
                skeletonList.Enabled = false;
                return;
            }

            Cursor.Current = Cursors.WaitCursor;
            try
            {
                foreach (SkeletonDB.SkeletonEntry entry in db.Skeletons)
                {
                    Skeleton skeleton = Singleton.Global.GetSkeleton(entry);
                    if (skeleton == null) continue;

                    _candidates.Add(new Candidate
                    {
                        Entry = entry,
                        Skeleton = skeleton,
                        FitsBoneCount = skeleton.Bones.Count >= _requiredBones,
                        Fit = skeleton.ScoreFit(_model),
                    });
                }
            }
            finally { Cursor.Current = Cursors.Default; }

            //Best fit first, then anything big enough, then the rest
            _candidates.Sort((a, b) =>
            {
                if (a.FitsBoneCount != b.FitsBoneCount) return a.FitsBoneCount ? -1 : 1;
                bool scoredA = a.Fit >= 0, scoredB = b.Fit >= 0;
                if (scoredA != scoredB) return scoredA ? -1 : 1;
                if (scoredA && a.Fit != b.Fit) return a.Fit.CompareTo(b.Fit);
                return string.Compare(a.Entry.Name, b.Entry.Name, StringComparison.OrdinalIgnoreCase);
            });

            Populate();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_requiredBones == 0)
            {
                statusLabel.Text = "This model isn't skinned, so a skeleton is optional - picking one just writes the rig alongside the mesh.";
                return;
            }

            Candidate best = _candidates.FirstOrDefault(x => x.Fit >= 0);
            string need = "This model uses " + _requiredBones + " bone slot(s), so it needs a skeleton with at least that many bones. ";
            statusLabel.Text = best == null
                ? need + "Nothing could be scored against its skin weights, so pick by name."
                : need + "'" + best.Entry.Name + "' fits its skin weights best (" + best.Fit.ToString("0.00") + " m average), and has been pre-selected.";
        }

        private void Populate()
        {
            string filter = searchBox.Text?.Trim() ?? "";
            skeletonList.BeginUpdate();
            skeletonList.Items.Clear();

            foreach (Candidate candidate in _candidates)
            {
                if (filter.Length != 0 && candidate.Entry.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                ListViewItem item = new ListViewItem(candidate.Entry.Name) { Tag = candidate };
                item.SubItems.Add(candidate.Skeleton.Bones.Count.ToString());
                item.SubItems.Add(!candidate.FitsBoneCount ? "too small"
                    : candidate.Fit < 0 ? "-" : candidate.Fit.ToString("0.00") + " m");
                if (!candidate.FitsBoneCount) item.ForeColor = Color.Gray;
                skeletonList.Items.Add(item);
            }

            if (skeletonList.Items.Count != 0)
            {
                skeletonList.Items[0].Selected = true;
                skeletonList.Items[0].EnsureVisible();
            }
            skeletonList.EndUpdate();
            UpdateButtons();
        }

        private Candidate GetSelected()
        {
            return skeletonList.SelectedItems.Count == 0 ? null : skeletonList.SelectedItems[0].Tag as Candidate;
        }

        private void UpdateButtons()
        {
            okBtn.Enabled = GetSelected() != null;
        }

        private void Accept()
        {
            Candidate candidate = GetSelected();
            if (candidate == null) return;

            if (!candidate.FitsBoneCount)
            {
                string message = "'" + candidate.Entry.Name + "' only has " + candidate.Skeleton.Bones.Count + " bones, but this model is skinned to "
                    + _requiredBones + ". Some of the mesh won't have a bone to attach to.\n\nExport with it anyway?";
                if (MessageBox.Show(message, "Not enough bones", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            Result = candidate.Skeleton;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
