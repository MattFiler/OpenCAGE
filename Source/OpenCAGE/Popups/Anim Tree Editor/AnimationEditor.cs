using AlienPAK;
using CATHODE;
using CathodeLib;
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
    /// Browse everything in ANIMATION.PAK the way the game asks for it: an animation set, a context
    /// within it, and the clips that context can play. Clips can be previewed on a mesh and exported.
    /// </summary>
    public partial class AnimationEditor : BaseWindow
    {
        private CathodeLib.Animation _animations;
        private CathodeLib.Animation.AnimationContext _context;
        private AnimationPreview _preview;

        //rebuilding the tree on every keystroke over 400 sets is enough to feel it, so it waits
        private readonly Timer _searchDelay = new Timer { Interval = 250 };
        private bool _rebuilding;

        public AnimationEditor() : base()
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;

            clipList.Columns.Add("Animation", 300);
            clipList.Columns.Add("Length", 70, HorizontalAlignment.Right);
            clipList.Columns.Add("Frames", 60, HorizontalAlignment.Right);
            clipList.Columns.Add("Bones", 55, HorizontalAlignment.Right);
            clipList.Columns.Add("Skeleton", 140);

            setTree.AfterSelect += (s, e) => ShowSelectedContext();
            clipList.SelectedIndexChanged += (s, e) => ShowSelectedClip();
            clipList.DoubleClick += (s, e) => { if (previewBtn.Enabled) previewBtn.PerformClick(); };
            previewBtn.Click += PreviewBtn_Click;
            exportAllBtn.Click += ExportAllBtn_Click;

            _searchDelay.Tick += (s, e) => { _searchDelay.Stop(); RebuildTree(); };
            setSearchBox.TextChanged += (s, e) => { _searchDelay.Stop(); _searchDelay.Start(); };
            clipSearchBox.TextChanged += (s, e) => FillClips();

            splitMain.SplitterMoved += (s, e) => SettingsManager.SetInteger(Settings.AnimationEditorSplitter, splitMain.SplitterDistance);
            splitClips.SplitterMoved += (s, e) => SettingsManager.SetInteger(Settings.AnimationEditorClipSplitter, splitClips.SplitterDistance);

            Load += AnimationEditor_Load;
            FormClosed += (s, e) => _searchDelay.Dispose();
        }

        private void AnimationEditor_Load(object sender, EventArgs e)
        {
            RestoreSplitter(splitMain, Settings.AnimationEditorSplitter, 120, 600);
            RestoreSplitter(splitClips, Settings.AnimationEditorClipSplitter, 120, 900);

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

            if (_animations == null || !_animations.Loaded && _animations.Sets.Count == 0)
            {
                statusLabel.Text = "ANIMATION.PAK could not be read.";
                return;
            }

            RebuildTree();
            statusLabel.Text = _animations.Sets.Count + " animation sets, "
                + _animations.Sets.Sum(x => x.ClipCount).ToString("N0") + " clips"
                + (_animations.Failures.Count == 0 ? "" : "  (" + _animations.Failures.Count + " files could not be read)");
        }

        private void RestoreSplitter(SplitContainer container, string setting, int min, int max)
        {
            int saved = SettingsManager.GetInteger(setting, -1);
            if (saved >= min && saved <= max && saved < (container.Orientation == Orientation.Horizontal ? container.Height : container.Width) - min)
                container.SplitterDistance = saved;
        }

        #region TREE
        /* Sets, grouped by what they drive, with a node per context underneath. Search matches the set
         * name, the skeleton, and the names of the clips inside - so looking for "reload" finds the
         * sets that have one even though no set is called that. */
        private void RebuildTree()
        {
            if (_animations == null) return;

            string search = setSearchBox.Text.Trim();
            CathodeLib.Animation.AnimationContext previous = _context;

            _rebuilding = true;
            setTree.BeginUpdate();
            setTree.Nodes.Clear();

            TreeNode characters = new TreeNode("Characters");
            TreeNode environment = new TreeNode("Environment");
            TreeNode other = new TreeNode("Other");

            foreach (CathodeLib.Animation.AnimationSet set in _animations.Sets.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                List<CathodeLib.Animation.AnimationContext> contexts = MatchingContexts(set, search);
                if (contexts == null) continue;

                TreeNode node = new TreeNode(set.Name + "  (" + set.ClipCount.ToString("N0") + ")") { Tag = set };
                foreach (CathodeLib.Animation.AnimationContext context in contexts)
                {
                    if (context.Clips.Count == 0) continue;
                    node.Nodes.Add(new TreeNode(ContextName(context) + "  (" + context.Clips.Count + ")") { Tag = context });
                }

                (set.Kind == CathodeLib.Animation.AnimationKind.Character ? characters
                    : set.Kind == CathodeLib.Animation.AnimationKind.Environment ? environment
                    : other).Nodes.Add(node);
            }

            foreach (TreeNode group in new[] { characters, environment, other })
            {
                if (group.Nodes.Count == 0) continue;
                group.Text = group.Text + "  (" + group.Nodes.Count + ")";
                setTree.Nodes.Add(group);
            }

            //a search narrow enough to be readable is worth opening up
            if (search.Length != 0 && setTree.GetNodeCount(true) < 200) setTree.ExpandAll();
            else foreach (TreeNode group in setTree.Nodes) group.Expand();

            setTree.EndUpdate();
            _rebuilding = false;

            if (previous != null) Reselect(previous);
            if (setTree.SelectedNode == null) ShowSelectedContext();
        }

        /* Which of a set's contexts survive the search, or null if the set doesn't match at all */
        private List<CathodeLib.Animation.AnimationContext> MatchingContexts(CathodeLib.Animation.AnimationSet set, string search)
        {
            if (search.Length == 0) return set.Contexts;

            //a set that matches by name keeps all of its contexts, so you can still see everything in it
            if (set.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || set.Skeleton.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return set.Contexts;

            List<CathodeLib.Animation.AnimationContext> matched = set.Contexts
                .Where(x => x.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                         || x.Clips.Any(c => Matches(c, search)))
                .ToList();
            return matched.Count == 0 ? null : matched;
        }

        private static bool Matches(CathodeLib.Animation.ClipReference clip, string search)
        {
            return clip.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || clip.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ContextName(CathodeLib.Animation.AnimationContext context)
        {
            return context.Name.Trim().Length == 0 ? "(always available)" : context.Name;
        }

        private void Reselect(CathodeLib.Animation.AnimationContext context)
        {
            foreach (TreeNode group in setTree.Nodes)
                foreach (TreeNode set in group.Nodes)
                    foreach (TreeNode node in set.Nodes)
                        if (node.Tag == context) { setTree.SelectedNode = node; node.EnsureVisible(); return; }
        }
        #endregion

        #region CLIPS
        private void ShowSelectedContext()
        {
            if (_rebuilding) return;

            _context = setTree.SelectedNode?.Tag as CathodeLib.Animation.AnimationContext;
            if (_context == null && setTree.SelectedNode?.Tag is CathodeLib.Animation.AnimationSet set)
                _context = set.Contexts.FirstOrDefault(x => x.Clips.Count != 0);

            FillClips();
        }

        private void FillClips()
        {
            string search = clipSearchBox.Text.Trim();
            clipList.BeginUpdate();
            clipList.Items.Clear();

            if (_context != null)
            {
                foreach (CathodeLib.Animation.ClipReference clip in _context.Clips.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (search.Length != 0 && !Matches(clip, search)) continue;

                    ListViewItem item = new ListViewItem(clip.Name.Length == 0 ? Path.GetFileName(clip.Path) : clip.Name) { Tag = clip };
                    if (clip.Playable)
                    {
                        item.SubItems.Add(clip.Duration.ToString("0.00") + "s");
                        item.SubItems.Add(clip.Animation.FrameCount.ToString());
                        item.SubItems.Add(clip.Animation.TransformTrackCount.ToString());
                        item.SubItems.Add(clip.Animation.SkeletonName);
                    }
                    else
                    {
                        item.SubItems.Add("-");
                        item.SubItems.Add("-");
                        item.SubItems.Add("-");
                        item.SubItems.Add(clip.Section == null ? "not found" : "unreadable");
                        item.ForeColor = Color.Gray;
                    }
                    clipList.Items.Add(item);
                }
            }
            clipList.EndUpdate();

            contextLabel.Text = _context == null
                ? "Choose an animation set on the left."
                : _context.Set.Name + " → " + ContextName(_context)
                    + "  —  " + clipList.Items.Count + " of " + _context.Clips.Count + " shown"
                    + (_context.Set.Skeleton.Length == 0 ? "" : ", rigged to " + _context.Set.Skeleton);

            exportAllBtn.Enabled = clipList.Items.Count != 0;
            ShowSelectedClip();
        }

        private CathodeLib.Animation.ClipReference Selected()
        {
            return clipList.SelectedItems.Count == 0 ? null : clipList.SelectedItems[0].Tag as CathodeLib.Animation.ClipReference;
        }

        private void ShowSelectedClip()
        {
            CathodeLib.Animation.ClipReference clip = Selected();
            previewBtn.Enabled = clip != null && clip.Playable;
            detailBox.Text = Describe(clip);
        }

        /* Everything we know about a clip, laid out for reading rather than editing */
        private string Describe(CathodeLib.Animation.ClipReference clip)
        {
            if (clip == null) return "";

            List<string> lines = new List<string>();
            lines.Add("Name       " + clip.Name);
            lines.Add("Path       " + clip.Path);
            lines.Add("Section    " + (clip.Section == null ? "could not be resolved" : clip.Section.Filepath + "  [clip " + clip.Index + "]"));

            if (clip.Animation == null)
                lines.Add("           The animation itself could not be read out of this section.");
            else
            {
                HavokPackfile.AnimationClip animation = clip.Animation;
                lines.Add("");
                lines.Add("Skeleton   " + animation.SkeletonName + BoneCountSuffix(animation.SkeletonName));
                lines.Add("Length     " + animation.Duration.ToString("0.###") + "s over " + animation.FrameCount + " frames ("
                    + (animation.FrameDuration > 0 ? (1 / animation.FrameDuration).ToString("0.#") : "?") + " fps)");
                lines.Add("Tracks     " + animation.TransformTrackCount + " bones, " + animation.FloatTrackCount + " float");
                lines.Add("Storage    " + animation.Blocks.Count + " block(s) of up to " + animation.MaxFramesPerBlock
                    + " frames, " + animation.DataLength.ToString("N0") + " bytes compressed");
                if (animation.Additive)
                    lines.Add("Additive   This clip holds a difference to lay over another pose, not a pose of its own.");
            }

            AnimClipDBSec.MetadataSet metadata = clip.Metadata;
            if (metadata == null)
                lines.Add("\nThis clip has no metadata in its section.");
            else
            {
                lines.Add("");
                lines.Add("-- Properties of the clip --");
                foreach (AnimClipDBSec.MetadataArgument argument in metadata.Common.Arguments)
                    lines.Add("   " + argument.Name.PadRight(26) + Value(argument));

                for (int i = 0; i < metadata.Instances.Count; i++)
                {
                    AnimClipDBSec.MetadataBlock block = metadata.Instances[i];
                    if (block.Arguments.Count == 0 && block.Properties.Count == 0) continue;

                    lines.Add("");
                    lines.Add("-- Use " + (i + 1) + " of " + metadata.Instances.Count + " --");
                    foreach (AnimClipDBSec.MetadataArgument argument in block.Arguments)
                        lines.Add("   " + argument.Name.PadRight(26) + Value(argument));

                    foreach (AnimClipDBSec.MetadataProperty property in block.Properties)
                    {
                        lines.Add("   " + property.Name + " fires at:");
                        for (int t = 0; t < property.Times.Count; t++)
                            lines.Add("      " + property.Times[t].ToString("0.###") + "s"
                                + (t < property.Events.Count && property.Events[t].Name.Length != 0 ? "   → " + property.Events[t].Name : ""));
                    }
                }
            }
            return string.Join(Environment.NewLine, lines);
        }

        private string BoneCountSuffix(string skeleton)
        {
            List<Skeleton.Bone> bones = _animations?.GetSkeleton(skeleton)?.Bones;
            return bones == null ? "  (not in this PAK)" : "  (" + bones.Count + " bones)";
        }

        private static string Value(AnimClipDBSec.MetadataArgument argument)
        {
            object value = argument.Value;
            if (value is float number) return number.ToString("0.#####");
            return value == null ? "" : value.ToString();
        }
        #endregion

        #region ACTIONS
        private void PreviewBtn_Click(object sender, EventArgs e)
        {
            CathodeLib.Animation.ClipReference clip = Selected();
            if (clip == null || !clip.Playable) return;

            if (_preview != null && !_preview.IsDisposed)
            {
                _preview.Show(clip);
                _preview.BringToFront();
                return;
            }

            _preview = new AnimationPreview(_animations);
            _preview.FormClosed += (s, args) => _preview = null;
            _preview.Show();
            _preview.Show(clip);
        }

        /* Every clip currently listed, written out against a rig with no mesh attached. */
        private void ExportAllBtn_Click(object sender, EventArgs e)
        {
            if (_context == null || clipList.Items.Count == 0) return;

            List<CathodeLib.Animation.ClipReference> clips = clipList.Items.Cast<ListViewItem>()
                .Select(x => x.Tag as CathodeLib.Animation.ClipReference)
                .Where(x => x != null && x.Playable)
                .ToList();

            if (clips.Count == 0)
            {
                MessageBox.Show("None of the animations listed could be read, so there's nothing to export.",
                    "Export all", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Skeleton skeleton;
            using (AnimationSkeletonPicker picker = new AnimationSkeletonPicker(_animations, _context.Set, clips))
            {
                if (picker.ShowDialog(this) != DialogResult.OK || picker.Result == null) return;
                skeleton = picker.Result;
            }

            if (!AnimationExport.ConfirmLargeExport(this, clips.Count, skeleton.Bones.Count,
                    clips.Max(x => x.Animation.FrameCount)))
                return;

            string suggested = _context.Set.Name + (_context.Name.Trim().Length == 0 ? "" : "_" + _context.Name);
            string filename = AnimationExport.AskWhereToSave(this, suggested);
            if (filename == null) return;

            Cursor.Current = Cursors.WaitCursor;
            try
            {
                CathodeLibExtensions.ExportAnimations(skeleton, clips, filename);
                MessageBox.Show(clips.Count + " animation(s) exported against the '" + skeleton.Name + "' skeleton."
                    + (clips.Count == clipList.Items.Count ? "" : "\n\n" + (clipList.Items.Count - clips.Count) + " could not be read and were left out."),
                    "Export all", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor.Current = Cursors.Default; }
        }
        #endregion
    }
}
