using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Asks which rig to play or export a set's animations against. Unlike picking a skeleton for a
    /// model, there's no guessing involved: the clips name the skeleton they were authored on, so
    /// those sort to the top and everything else is there for retargeting experiments.
    /// </summary>
    public partial class AnimationSkeletonPicker : Form
    {
        /// <summary>The chosen skeleton, or null if the dialog was cancelled.</summary>
        public Skeleton Result { get; private set; }


        /// <summary>
        /// How the clips should be written out. Viewing and re-importing want different things from
        /// the same clip: a viewer wants bones the clip never mentions left at the rig's rest pose so
        /// the character still looks like itself, while the game puts a default there - and a rig
        /// rests its root a long way from identity, so an export that shows it is not an export that
        /// can come back in.
        /// </summary>
        public enum ExportMode
        {
            HeldInPlace,
            Travelling,
            AsTheGameStoresIt,
        }

        public ExportMode Mode
        {
            get { return (ExportMode)Math.Max(0, modeBox.SelectedIndex); }
        }

        public CathodeLib.Animation.RootMotion RootMotion
        {
            get
            {
                switch (Mode)
                {
                    case ExportMode.Travelling: return CathodeLib.Animation.RootMotion.Follow;
                    case ExportMode.AsTheGameStoresIt: return CathodeLib.Animation.RootMotion.Authored;
                    default: return CathodeLib.Animation.RootMotion.Ignore;
                }
            }
        }

        public CathodeLib.Animation.UntrackedChannels Untracked
        {
            get
            {
                return Mode == ExportMode.AsTheGameStoresIt
                    ? CathodeLib.Animation.UntrackedChannels.EngineDefaults
                    : CathodeLib.Animation.UntrackedChannels.RestPose;
            }
        }

        private void FillModes()
        {
            modeBox.Items.Add("For viewing - held on the spot");
            modeBox.Items.Add("For viewing - travelling as the clip carries it");
            modeBox.Items.Add("For editing and re-importing - exactly what the clip holds");
            modeBox.SelectedIndex = 0;
        }

        private readonly CathodeLib.Animation _animations;
        private readonly CathodeLib.Animation.AnimationSet _set;
        private readonly List<Candidate> _candidates = new List<Candidate>();

        private class Candidate
        {
            public string Name;
            public Skeleton Skeleton;

            /// <summary>How many of the clips being exported name this skeleton as their own.</summary>
            public int Authored;

            /// <summary>Whether it has enough bones for every track in those clips.</summary>
            public bool BigEnough;

            /// <summary>Whether it is the rig the mesh being shown with the clips is skinned to.</summary>
            public bool MeshRig;
        }

        private readonly Skeleton _meshRig;

        /// <param name="meshRig">
        /// The rig the mesh shown with these clips is skinned to, where the clips can be played on it. It
        /// goes first and is pre-selected: the rig the clips were authored on is usually a shared one few
        /// meshes are skinned to, and a mesh posed on a rig that numbers its bones differently is torn apart.
        /// </param>
        public AnimationSkeletonPicker(CathodeLib.Animation animations, CathodeLib.Animation.AnimationSet set,
                                       IEnumerable<CathodeLib.Animation.ClipReference> clips, Skeleton meshRig = null)
        {
            _animations = animations;
            _set = set;
            _meshRig = meshRig;
            InitializeComponent();
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
            Icon = SharedFormIcon.Icon;

            FillModes();

            skeletonList.Columns.Add("Skeleton", 240);
            skeletonList.Columns.Add("Bones", 60, HorizontalAlignment.Right);
            skeletonList.Columns.Add("Used by", 200);

            searchBox.TextChanged += (s, e) => Populate();
            skeletonList.SelectedIndexChanged += (s, e) => okBtn.Enabled = GetSelected() != null;
            skeletonList.DoubleClick += (s, e) => { if (GetSelected() != null) Accept(); };
            okBtn.Click += (s, e) => Accept();
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Score(clips?.ToList() ?? new List<CathodeLib.Animation.ClipReference>());
        }

        private void Score(List<CathodeLib.Animation.ClipReference> clips)
        {
            Dictionary<string, int> authored = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int mostTracks = 0;
            foreach (CathodeLib.Animation.ClipReference clip in clips)
            {
                if (clip.Animation == null) continue;
                authored.TryGetValue(clip.Animation.SkeletonName, out int seen);
                authored[clip.Animation.SkeletonName] = seen + 1;

                foreach (int bone in clip.Animation.TrackToBone)
                    if (bone + 1 > mostTracks) mostTracks = bone + 1;
            }

            foreach (CathodeLib.Animation.SkeletonAsset asset in _animations.Skeletons)
            {
                Skeleton skeleton = asset.Skeleton ?? asset.Skeleton64;
                if (skeleton == null) continue;

                authored.TryGetValue(skeleton.Name, out int count);
                _candidates.Add(new Candidate
                {
                    Name = skeleton.Name,
                    Skeleton = skeleton,
                    Authored = count,
                    BigEnough = skeleton.Bones.Count >= mostTracks,
                    MeshRig = ReferenceEquals(skeleton, _meshRig),
                });
            }

            //the mesh's own rig, then the rigs these clips were built on, then the set's own, then everything else by name
            _candidates.Sort((a, b) =>
            {
                if (a.MeshRig != b.MeshRig) return a.MeshRig ? -1 : 1;
                if (a.Authored != b.Authored) return b.Authored.CompareTo(a.Authored);
                bool setA = string.Equals(a.Name, _set?.Skeleton, StringComparison.OrdinalIgnoreCase);
                bool setB = string.Equals(b.Name, _set?.Skeleton, StringComparison.OrdinalIgnoreCase);
                if (setA != setB) return setA ? -1 : 1;
                if (a.BigEnough != b.BigEnough) return a.BigEnough ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            Candidate best = _candidates.FirstOrDefault();
            Candidate authoredBest = _candidates.Where(x => x.Authored != 0).OrderByDescending(x => x.Authored).FirstOrDefault();
            if (best != null && best.MeshRig)
                statusLabel.Text = "'" + best.Name + "' is the rig the mesh on show is skinned to, so it has been pre-selected"
                    + (authoredBest == null ? ". None of these animations name a skeleton this PAK holds, so they play on it bone for bone."
                       : authoredBest == best ? ", and these animations were authored against it."
                       : ": these animations were authored against '" + authoredBest.Name + "' and are retargeted onto it, the way the game plays them.")
                    + " On any other rig the mesh follows the wrong bones.";
            else
                statusLabel.Text = best == null || best.Authored == 0
                    ? "None of these animations name a skeleton this PAK holds, so pick one by name."
                    : "These animations were authored against '" + best.Name + "', which has been pre-selected. "
                        + "Picking another rig retargets them onto it, which may or may not look right.";

            Populate();
        }

        private void Populate()
        {
            string filter = searchBox.Text?.Trim() ?? "";
            skeletonList.BeginUpdate();
            skeletonList.Items.Clear();

            foreach (Candidate candidate in _candidates)
            {
                if (filter.Length != 0 && candidate.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                ListViewItem item = new ListViewItem(candidate.Name) { Tag = candidate };
                item.SubItems.Add(candidate.Skeleton.Bones.Count.ToString());
                string used = candidate.Authored != 0
                    ? candidate.Authored + " of the animations here"
                    : candidate.BigEnough ? "" : "too few bones";
                if (candidate.MeshRig) used = "the mesh on show" + (used.Length == 0 ? "" : ", " + used);
                item.SubItems.Add(used);
                if (candidate.Authored == 0 && !candidate.BigEnough && !candidate.MeshRig) item.ForeColor = Color.Gray;
                skeletonList.Items.Add(item);
            }

            if (skeletonList.Items.Count != 0)
            {
                skeletonList.Items[0].Selected = true;
                skeletonList.Items[0].EnsureVisible();
            }
            skeletonList.EndUpdate();
            okBtn.Enabled = GetSelected() != null;
        }

        private Candidate GetSelected()
        {
            return skeletonList.SelectedItems.Count == 0 ? null : skeletonList.SelectedItems[0].Tag as Candidate;
        }

        private void Accept()
        {
            Candidate candidate = GetSelected();
            if (candidate == null) return;

            //the mesh's own rig was only offered first because the clips can be played on it
            if (!candidate.BigEnough && !candidate.MeshRig &&
                MessageBox.Show("'" + candidate.Name + "' has fewer bones than these animations drive, so some tracks will have nowhere to go.\n\nUse it anyway?",
                    "Not enough bones", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            Result = candidate.Skeleton;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
