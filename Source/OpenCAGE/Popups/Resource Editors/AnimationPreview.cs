using AlienPAK;
using CATHODE;
using CathodeLib;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Plays one clip on a mesh of the user's choosing, with everything the metadata tags on it laid
    /// out underneath, and exports the two together.
    ///
    /// The mesh and rig are remembered per animation set: the game doesn't record which model goes
    /// with which set anywhere we can read, and re-picking a character every time you look at one
    /// of its animations gets old quickly.
    /// </summary>
    public partial class AnimationPreview : BaseWindow
    {
        private readonly CathodeLib.Animation _animations;
        private GUI_AnimationViewer _viewer;

        private CathodeLib.Animation.ClipReference _clip;
        private Models.CS2 _model;
        private Skeleton _skeleton;

        private Retargeter _retarget;

        private readonly Timer _playback = new Timer { Interval = 33 };
        private readonly Stopwatch _clock = new Stopwatch();
        private EditModel _modelPicker;
        private int _startFrame;

        private static readonly double[] Speeds = { 0.1, 0.25, 0.5, 1.0, 2.0 };

        public AnimationPreview(CathodeLib.Animation animations) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _animations = animations;
            InitializeComponent();
            Icon = SharedFormIcon.Icon;

            _viewer = new GUI_AnimationViewer();
            viewerHost.Child = _viewer;

            foreach (double speed in Speeds) speedBox.Items.Add(speed.ToString("0.##") + "x");
            speedBox.SelectedIndex = Array.IndexOf(Speeds, 1.0);

            loopCheck.Checked = SettingsManager.GetBool(Settings.AnimationLoop, true);
            bonesCheck.Checked = SettingsManager.GetBool(Settings.AnimationShowBones, true);
            showMeshCheck.Checked = SettingsManager.GetBool(Settings.AnimationShowMesh, true);
            meshCheck.Checked = SettingsManager.GetBool(Settings.AnimationShowTextures, true);
            rootMotionCheck.Checked = SettingsManager.GetBool(Settings.AnimationRootMotion, false);

            playBtn.Click += (s, e) => { if (_playback.Enabled) Stop(); else Play(); };
            modelBtn.Click += ModelBtn_Click;
            skeletonBtn.Click += SkeletonBtn_Click;
            exportBtn.Click += ExportBtn_Click;

            timeline.FrameChanged += Timeline_FrameChanged;
            timeline.MarkerSelected += Timeline_MarkerSelected;

            loopCheck.CheckedChanged += (s, e) => SettingsManager.SetBool(Settings.AnimationLoop, loopCheck.Checked);
            bonesCheck.CheckedChanged += (s, e) =>
            {
                SettingsManager.SetBool(Settings.AnimationShowBones, bonesCheck.Checked);
                _viewer.ShowBones = bonesCheck.Checked;
            };
            showMeshCheck.CheckedChanged += (s, e) =>
            {
                SettingsManager.SetBool(Settings.AnimationShowMesh, showMeshCheck.Checked);
                if (_viewer != null) _viewer.ShowMesh = showMeshCheck.Checked;
            };
            meshCheck.CheckedChanged += (s, e) =>
            {
                SettingsManager.SetBool(Settings.AnimationShowTextures, meshCheck.Checked);
                Rebind(false);
            };
            rootMotionCheck.CheckedChanged += (s, e) =>
            {
                SettingsManager.SetBool(Settings.AnimationRootMotion, rootMotionCheck.Checked);
                if (_viewer != null)
                    _viewer.RootMotion = rootMotionCheck.Checked
                        ? CathodeLib.Animation.RootMotion.Follow
                        : CathodeLib.Animation.RootMotion.Ignore;
            };
            partPanel.SizeChanged += (s, e) => LayoutPartFilters();
            showAllPartsBtn.Click += (s, e) => SetAllParts(true);
            hideAllPartsBtn.Click += (s, e) => SetAllParts(false);

            _playback.Tick += Playback_Tick;
            Load += (s, e) => SizeTimeline();
            FormClosed += AnimationPreview_FormClosed;
        }

        private void AnimationPreview_FormClosed(object sender, FormClosedEventArgs e)
        {
            _playback.Stop();
            _playback.Dispose();
            if (_modelPicker != null && !_modelPicker.IsDisposed) _modelPicker.Close();
            _viewer = null;
        }

        /// <summary>Show a clip, restoring whatever mesh and rig this set was last previewed with.</summary>
        public void Show(CathodeLib.Animation.ClipReference clip)
        {
            if (clip == null) return;
            Stop();

            bool changedSet = _clip?.Context?.Set != clip.Context?.Set;
            _clip = clip;
            clipLabel.Text = clip.Name + "   —   " + clip.Path;

            if (changedSet || _skeleton == null) RestoreChoices();

            timeline.SetClip(clip);
            SizeTimeline();
            ShowMarkerCount();

            Rebind(changedSet);
            SetFrame(0);
        }

        /* Give the timeline the room its lanes need, without eating the whole window */
        private void SizeTimeline()
        {
            if (!IsHandleCreated || splitViewer.Height <= 0) return;

            int wanted = timeline.PreferredHeight + markerLabel.Height;
            int allowed = Math.Max(splitViewer.Panel2MinSize, Math.Min(wanted, splitViewer.Height / 2));
            int distance = splitViewer.Height - allowed - splitViewer.SplitterWidth;
            if (distance >= splitViewer.Panel1MinSize) splitViewer.SplitterDistance = distance;
        }

        #region CHOICES
        private string SetName { get { return _clip?.Context?.Set?.Name ?? ""; } }

        /// <summary>
        /// Whether this clip plays on static geometry rather than on a skinned character. The two
        /// halves of the animation system want different meshes, so nearly every choice below forks
        /// on it.
        /// </summary>
        private bool IsEnvironment
        {
            get { return _clip?.Context?.Set?.Kind == CathodeLib.Animation.AnimationKind.Environment; }
        }

        /// <summary>
        /// Whether the rig being shown on drives set dressing - which is what decides whether it has
        /// to be turned a quarter turn to meet the mesh, a character's rig being Z up and its mesh Y
        /// up while a prop's rig already sits in the prop's space.
        ///
        /// The rig's own definition is the thing that says so, and every rig any set plays on has
        /// one; the set's classification is a stand-in for the case where it doesn't. The two agree
        /// on 398 of the 399 sets with a rig to show, and on the one they don't - FLAMETHROWER, on
        /// the WEAPONS_FLAME_THROWER rig - it is the rig that is right.
        /// </summary>
        private bool ShowingOnEnvironmentRig
        {
            get
            {
                if (_skeleton != null && _animations != null
                    && _animations.SkeletonDefs.TryGetValue(_skeleton.Name, out CathodeLib.Animation.SkeletonDef def))
                    return def.IsEnvironment;
                return IsEnvironment;
            }
        }

        /* Whatever was picked last time for this set, falling back to the rig the clip names */
        private void RestoreChoices()
        {
            /* The set's own rig leads, not the one the clip was authored on. A TAYLOR animation is
             * almost always authored on MALE, and showing it on MALE is showing it on a rig no mesh
             * in the game uses - retargeting is what makes the character's own rig the right answer. */
            string savedSkeleton = SettingsManager.GetString(Settings.AnimationPreviewSkeleton(SetName));
            _skeleton = FindSkeleton(savedSkeleton)
                     ?? FindSkeleton(SetName)
                     ?? FindSkeleton(_clip?.Animation?.SkeletonName)
                     ?? FindSkeleton(_clip?.Context?.Set?.Skeleton);

            string savedModel = SettingsManager.GetString(Settings.AnimationPreviewModel(SetName));
            _model = savedModel.Length == 0 ? null : FindModel(savedModel);

            /* An environment rig doesn't need picking for at all. Where the level animates something
             * with it, its record names exactly one mesh - 1358 records over the 21 shipped levels,
             * every one of them a single mesh - so there is nothing to choose between and no reason
             * to send anyone off to choose it.
             *
             * That one mesh wins over whatever was picked last time, unless what was picked is the
             * same mesh. An environment set is about one prop; a name saved against a different
             * level, or against a build that couldn't resolve this one, isn't a preference worth
             * keeping over the level's own answer. */
            PreferLevelModel();
        }

        /// <summary>
        /// Take the mesh the open level animates with the chosen rig, where it animates one at all.
        /// Does nothing for a character rig, which no level records a mesh for.
        /// </summary>
        private void PreferLevelModel()
        {
            List<Models.CS2> animated = LevelModelsFor(_skeleton);
            if (animated.Count != 0 && !animated.Any(x => ReferenceEquals(x, _model))) _model = animated[0];
        }

        /// <summary>The meshes the open level animates with this rig, the most driven first.</summary>
        private List<Models.CS2> LevelModelsFor(Skeleton rig)
        {
            if (rig == null || Content?.Level == null) return new List<Models.CS2>();
            return EnvironmentRigs.ModelsFor(Content.Level, rig.Name, rig);
        }

        private Skeleton FindSkeleton(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return _animations?.GetSkeleton(name)?.Skeleton
                ?? _animations?.Skeletons.FirstOrDefault(x => string.Equals((x.Skeleton ?? x.Skeleton64)?.Name, name, StringComparison.OrdinalIgnoreCase))?.Skeleton;
        }

        private Models.CS2 FindModel(string name)
        {
            return Content?.Level?.Models?.Entries?.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private void ModelBtn_Click(object sender, EventArgs e)
        {
            if (Content?.Level?.Models == null)
            {
                MessageBox.Show("Meshes come from the level that's open in the editor, and there isn't one yet.\n\n"
                    + "Open a level and the models in it will be offered here. The rig on its own can still be previewed without one.",
                    "No level loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_modelPicker != null && !_modelPicker.IsDisposed)
            {
                _modelPicker.BringToFront();
                return;
            }

            Skeleton rig = _skeleton;
            Func<Models.CS2, bool> fits = IsEnvironment ? EnvironmentFilter(rig) : CharacterFilter(rig);

            Cursor.Current = Cursors.WaitCursor;
            int offered;
            try { offered = Content.Level.Models.Entries.Count(fits); }
            finally { Cursor.Current = Cursors.Default; }

            if (offered == 0 && rig != null)
            {
                if (IsEnvironment)
                {
                    /* Nothing in this level names a part after one of the rig's bones. Usually that
                     * means the prop isn't in this level at all; sometimes the level's copy of the
                     * mesh was built without part names, which the preview can't work around. */
                    fits = x => Skeleton.RequiredBoneCount(x) == 0;
                    MessageBox.Show("Nothing in this level is animated by '" + rig.Name + "'.\n\n"
                        + "Environment animations move a static mesh a part at a time, matching each part to the bone of "
                        + "the same name, so the mesh has to be one this rig was built for - usually the prop's own model, "
                        + "in a level that has it.\n\n"
                        + "Every static mesh is listed anyway, but one this rig doesn't name won't move.",
                        "No mesh uses this rig", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    /* MALE, FEMALE and FEMALENPC are reference rigs that no mesh is skinned to - the
                     * game retargets off them onto a character's own rig, and so does this preview.
                     * Picking one of those as the rig is the wrong way round; pick the character. */
                    fits = x => Skeleton.RequiredBoneCount(x) > 0;
                    MessageBox.Show("No mesh in this level is skinned to '" + rig.Name + "'.\n\n"
                        + "'" + rig.Name + "' is likely a shared reference rig that clips are authored on rather than one any "
                        + "character wears. Choose the character's own rig instead and the clip will be retargeted onto it, "
                        + "the same way the game does it.\n\n"
                        + "Every skinned mesh is listed anyway.",
                        "No mesh uses this rig", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            _modelPicker = new EditModel(null, true, true, fits);
            _modelPicker.Text = rig == null
                ? (IsEnvironment ? "Choose a static mesh" : "Choose a skinned mesh")
                : (IsEnvironment ? "Choose a mesh animated by '" : "Choose a mesh skinned to '") + rig.Name + "'";
            _modelPicker.OnWholeModelSelected += ModelPicker_Selected;
            _modelPicker.FormClosed += (s, args) => _modelPicker = null;
            _modelPicker.Show();
        }

        private void ModelPicker_Selected(Models.CS2 model)
        {
            _model = model;
            SettingsManager.SetString(Settings.AnimationPreviewModel(SetName), _model?.Name ?? "");

            BringToFront();
            Focus();
            Rebind(true);
        }

        private void SkeletonBtn_Click(object sender, EventArgs e)
        {
            if (_clip == null) return;

            using (AnimationSkeletonPicker picker = new AnimationSkeletonPicker(_animations, _clip.Context?.Set, new[] { _clip }))
            {
                if (picker.ShowDialog(this) != DialogResult.OK || picker.Result == null) return;
                _skeleton = picker.Result;
            }

            SettingsManager.SetString(Settings.AnimationPreviewSkeleton(SetName), _skeleton.Name);

            /* A different environment rig is a different prop, so the mesh follows it rather than
             * leaving the last prop's geometry sitting under a rig that has nothing to do with it. */
            Models.CS2 was = _model;
            PreferLevelModel();
            if (!ReferenceEquals(was, _model))
                SettingsManager.SetString(Settings.AnimationPreviewModel(SetName), _model?.Name ?? "");

            Rebind(true);
        }

        /// <summary>Hand the current mesh, rig and clip to the viewer and report anything wrong with them.</summary>
        private void Rebind(bool resetCamera)
        {
            if (_viewer == null) return;

            _retarget = BuildRetargeter();

            modelLabel.Text = _model == null ? "None — showing the rig on its own" : _model.Name;
            skeletonLabel.Text = _skeleton == null ? "None" : _skeleton.Name + "  (" + _skeleton.Bones.Count + " bones)";
            if (_retarget != null)
                skeletonLabel.Text += "   —   retargeted from " + string.Join(" via ", _retarget.Route.Take(_retarget.Route.Count - 1));

            Cursor.Current = Cursors.WaitCursor;
            try
            {
                _viewer.ShowBones = bonesCheck.Checked;
                _viewer.ShowMesh = showMeshCheck.Checked;
                _viewer.EnvironmentRig = ShowingOnEnvironmentRig;
                _viewer.RootMotion = rootMotionCheck.Checked
                    ? CathodeLib.Animation.RootMotion.Follow
                    : CathodeLib.Animation.RootMotion.Ignore;
                _viewer.Retarget = _retarget;
                //there may be no level open at all - the rig on its own previews fine without one
                _viewer.SetModel(_model, _skeleton, meshCheck.Checked, Content?.Level);
                _viewer.SetClip(_clip);
                BuildPartFilters();

                //say how much of a prop the rig drives - the rest of it is scenery and stays put
                if (_viewer.Rigid && _viewer.TotalParts != 0)
                    modelLabel.Text = _model.Name + "   —   " + _viewer.DrivenParts + " of " + _viewer.TotalParts + " parts animated";
            }
            catch (Exception ex)
            {
                Warn("The preview could not be built: " + ex.Message);
                return;
            }
            finally { Cursor.Current = Cursors.Default; }

            int frames = _clip?.Animation?.FrameCount ?? 0;
            bool playable = frames > 0 && _skeleton != null;
            playBtn.Enabled = playable && frames > 1;
            exportBtn.Enabled = playable;

            Warn(BuildWarning());
            if (resetCamera) _viewer.ResetCamera();
            SetFrame(0);
        }

        /// <summary>
        /// How this clip gets from the rig it was authored on to the rig it's being shown on, or
        /// null when they're the same rig or the data doesn't join them up.
        /// </summary>
        private Retargeter BuildRetargeter()
        {
            string authored = _clip?.Animation?.SkeletonName;
            if (_skeleton == null || string.IsNullOrEmpty(authored)) return null;
            return Retargeter.Between(_animations, authored, _skeleton.Name);
        }

        /* Whole models only, skinned, and skinned to *this* rig. A character's GP variant is skinned
         * to a rig of its own with different bone numbering, so offering it here just produces a
         * mesh whose limbs follow the wrong bones. */
        private static Func<Models.CS2, bool> CharacterFilter(Skeleton rig)
        {
            return x => Skeleton.RequiredBoneCount(x) > 0 && (rig == null || Fits(rig, x));
        }

        /* Static meshes the rig can actually move: it has to name at least one of their parts. A
         * skinned mesh is excluded outright - it belongs to the character half of the system. */
        private Func<Models.CS2, bool> EnvironmentFilter(Skeleton rig)
        {
            return x => rig == null ? Skeleton.RequiredBoneCount(x) == 0 : EnvironmentRigs.Drives(rig, x, Content.Level);
        }

        /* How far each bone sits from the vertices weighted to it. The rig a mesh was actually skinned
         * to lands a few centimetres out; any other rig lands tens of centimetres out, because the
         * bone numbering doesn't correspond. Measured across the shipped characters: own rig 2.5 to
         * 7.8 cm, wrong rig 22.9 cm and up. */
        private const float FitLimit = 0.15f;

        /// <summary>Whether a mesh was skinned to this rig, judged by where its bones land.</summary>
        private static bool Fits(Skeleton rig, Models.CS2 model)
        {
            if (rig.Bones.Count < Skeleton.RequiredBoneCount(model)) return false;
            float fit = rig.ScoreFit(model);
            return fit >= 0 && fit <= FitLimit;
        }

        private string BadFitWarning()
        {
            if (_model == null || _skeleton == null) return null;

            /* Nothing here to score. A static mesh has no weights at all, and a mesh the level
             * animates as a prop is put together from the level's own record rather than skinned to
             * the rig - a few props do carry weights, and measuring how far those bones sit from
             * them answers a question nobody asked. Either way the only thing that could be wrong is
             * that the rig drives none of its parts, and the viewer has already said so. */
            if (Skeleton.RequiredBoneCount(_model) == 0 || _viewer.Rigid) return null;

            float fit = _skeleton.ScoreFit(_model);
            if (fit < 0 || fit <= FitLimit) return null;

            //name the rig it does belong to, since that's the question they'll ask next
            Skeleton better = _animations?.Skeletons.Select(x => x.Skeleton)
                .Where(x => x != null && x.Bones.Count >= Skeleton.RequiredBoneCount(_model))
                .Select(x => new { Rig = x, Fit = x.ScoreFit(_model) })
                .Where(x => x.Fit >= 0 && x.Fit <= FitLimit)
                .OrderBy(x => x.Fit).FirstOrDefault()?.Rig;

            return "'" + Path.GetFileName(Path.GetDirectoryName(_model.Name)) + "' isn't skinned to '" + _skeleton.Name
                 + "' - its bones sit " + (fit * 100).ToString("0") + " cm from the vertices weighted to them, so limbs will follow the wrong bones."
                 + (better == null ? " Pick a mesh that belongs to this rig." : " It belongs to '" + better.Name
                    + "'; this animation needs a mesh skinned to '" + _skeleton.Name + "'.");
        }

        /* The mismatches worth telling someone about before they conclude the tool is broken */
        private string BuildWarning()
        {
            if (_clip == null) return null;
            if (_skeleton == null) return "Pick a rig to play this animation on.";
            if (_viewer.Problem != null) return _viewer.Problem;

            string badFit = BadFitWarning();
            if (badFit != null) return badFit;

            /* An environment rig is a handful of markers with no shape of its own - often several of
             * them stacked on the same spot. It only reads as anything with the prop it moves. */
            if (_model == null && IsEnvironment)
                return "This animation moves static geometry. Choose a mesh to see it - the rig on its own is "
                     + "just a few markers and won't show you much.";

            string authored = _clip.Animation?.SkeletonName;
            if (!string.IsNullOrEmpty(authored) && !string.Equals(authored, _skeleton.Name, StringComparison.OrdinalIgnoreCase))
            {
                /* Nearly every clip is authored on a rig it never plays on, so this is the normal
                 * case rather than a problem - as long as the PAK says how to get from one to the
                 * other. It only needs saying at all when it can't. */
                if (_retarget == null)
                    return "This animation was authored on '" + authored + "' and nothing in the game's data says how to move it onto '"
                         + _skeleton.Name + "'. It's playing bone for bone, which will look wrong wherever the two rigs disagree.";
            }

            if (_clip.Additive)
                return "This is an additive animation - in game it lays on top of whatever else is playing. "
                     + "Here it's shown over the bind pose, so the movement is right but the starting pose isn't.";

            return LimbsLeftBehind();
        }

        /* Around one clip in seven never touches a given arm or leg: the game lays another animation
         * over the top for that limb, so the clip only carries the half it is responsible for. Left
         * to itself the limb sits in the rest pose, which looks exactly like the preview failing to
         * apply it - so say which limbs, before anyone concludes the tool has dropped them. */
        private string LimbsLeftBehind()
        {
            if (_clip?.Animation == null || _skeleton == null || IsEnvironment) return null;

            List<string> limbs = CathodeLib.Animation.LimbsLeftAtRest(_clip, _skeleton, _retarget);
            if (limbs.Count == 0) return null;

            return "This animation never moves the " + Join(limbs) + ", so " + (limbs.Count == 1 ? "it stays" : "they stay")
                 + " in the rest pose. That's how the clip was authored - in game another animation drives "
                 + (limbs.Count == 1 ? "it" : "them") + " at the same time.";
        }

        private static string Join(List<string> parts)
        {
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1)) + " or " + parts[parts.Count - 1];
        }

        private void Warn(string message)
        {
            warningLabel.Text = message ?? "";
            warningLabel.Visible = message != null;
            warningLabel.ForeColor = Color.FromArgb(200, 140, 40);
        }
        #endregion

        #region PART FILTERS
        /* A group per LOD with a checkbox per submesh, matching the model browser's render filters.
         * Characters ship with collision hulls and lower LODs bound to the same rig as the visible
         * mesh, sitting right on top of it - switching those off is the difference between seeing
         * the animation and not, so LOD 0 starts on and everything else starts off. */
        private void BuildPartFilters()
        {
            partPanel.SuspendLayout();
            foreach (Control control in partPanel.Controls.OfType<Control>().ToList()) control.Dispose();
            partPanel.Controls.Clear();

            IReadOnlyList<GUI_AnimationViewer.Part> parts = _viewer?.Parts;
            partGroup.Visible = parts != null && parts.Count != 0;
            if (partGroup.Visible)
            {
                /* Sized by hand rather than by AutoSize: a group box that auto-sizes around docked
                 * children collapses to nothing, and a flow panel that wraps turns the list into
                 * unreadable columns. */
                int width = PartRowWidth();
                foreach (IGrouping<int, GUI_AnimationViewer.Part> lod in parts.GroupBy(x => x.GroupOrder).OrderBy(x => x.Key))
                {
                    List<GUI_AnimationViewer.Part> inGroup = lod.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    GUI_AnimationViewer.Part first = inGroup[0];

                    GroupBox box = new GroupBox
                    {
                        Text = (first.Group.Length == 0 ? "Part " + first.GroupOrder : first.Group)
                             + (first.Lod == 0 ? "" : "  (LOD " + first.Lod + ")")
                             + (first.IsCollision ? "  - collision" : ""),
                        Width = width,
                        Height = GroupTop + (inGroup.Count * RowHeight) + 8,
                        Margin = new Padding(2, 2, 2, 6),
                    };

                    for (int i = 0; i < inGroup.Count; i++)
                    {
                        CheckBox check = new CheckBox
                        {
                            Text = inGroup[i].Name + "   (" + inGroup[i].VertexCount.ToString("N0") + ")",
                            Checked = inGroup[i].Visible,
                            AutoSize = false,
                            AutoEllipsis = true,
                            Tag = inGroup[i],
                            Location = new Point(8, GroupTop + (i * RowHeight)),
                            Size = new Size(Math.Max(60, width - 16), RowHeight - 2),
                            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                        };
                        check.CheckedChanged += (s, e) => _viewer?.SetPartVisible((GUI_AnimationViewer.Part)((CheckBox)s).Tag, ((CheckBox)s).Checked);
                        box.Controls.Add(check);
                    }
                    partPanel.Controls.Add(box);
                }
            }
            partPanel.ResumeLayout();
        }

        private const int GroupTop = 18;
        private const int RowHeight = 21;

        /* The width a group gets. Measured off the panel's outer width and always leaving room for
         * the scrollbar, so the answer doesn't change when the scrollbar appears - keying off
         * ClientSize would have the groups and the scrollbar chase each other. */
        private int PartRowWidth()
        {
            return Math.Max(120, partPanel.Width - SystemInformation.VerticalScrollBarWidth - 8);
        }

        private void LayoutPartFilters()
        {
            if (partPanel.Controls.Count == 0) return;

            int width = PartRowWidth();
            partPanel.SuspendLayout();
            foreach (Control box in partPanel.Controls) box.Width = width;
            partPanel.ResumeLayout();
        }

        private void SetAllParts(bool visible)
        {
            foreach (CheckBox check in AllPartChecks()) check.Checked = visible;
        }

        private IEnumerable<CheckBox> AllPartChecks()
        {
            foreach (Control group in partPanel.Controls)
                foreach (Control rows in group.Controls)
                    foreach (Control check in rows.Controls)
                        if (check is CheckBox box) yield return box;
        }
        #endregion

        #region TIMELINE
        private void ShowMarkerCount()
        {
            List<CathodeLib.Animation.ClipMarker> markers = _clip?.Markers;
            if (markers == null || markers.Count == 0)
            {
                markerCountLabel.Text = _clip?.Metadata == null ? "no metadata on this clip" : "no events tagged";
                markerLabel.Text = "Click a marker to see what it does.";
                return;
            }

            int audio = markers.Count(x => x.IsAudio);
            markerCountLabel.Text = markers.Count + " event(s) across " + timeline.LaneCount + " track(s)"
                + (audio == 0 ? "" : ", " + audio + " of them sounds");
            markerLabel.Text = "Click a marker to see what it does.";
        }

        private void Timeline_FrameChanged(object sender, EventArgs e)
        {
            //scrubbing takes over from playback rather than fighting it
            Stop();
            SetFrame(timeline.Frame);
        }

        private void Timeline_MarkerSelected(object sender, AnimationTimeline.Marker marker)
        {
            markerLabel.Text = marker == null
                ? "Click a marker to see what it does."
                : timeline.Describe(marker, false);
        }
        #endregion

        #region PLAYBACK
        private void Play()
        {
            if (!playBtn.Enabled) return;

            //restart from the top rather than sitting on the last frame doing nothing
            int frames = _clip?.Animation?.FrameCount ?? 0;
            if (timeline.Frame >= frames - 1) SetFrame(0);

            _startFrame = timeline.Frame;
            _clock.Restart();
            _playback.Start();
            playBtn.Text = "Pause";
        }

        private void Stop()
        {
            if (!_playback.Enabled) return;
            _playback.Stop();
            _clock.Stop();
            playBtn.Text = "Play";
        }

        /* Frames come from the clock rather than a counter, so a slow skinning pass drops frames
         * instead of playing the whole clip in slow motion. */
        private void Playback_Tick(object sender, EventArgs e)
        {
            HavokPackfile.AnimationClip animation = _clip?.Animation;
            if (animation == null || animation.FrameCount <= 1) { Stop(); return; }

            double speed = Speeds[Math.Max(0, Math.Min(Speeds.Length - 1, speedBox.SelectedIndex))];
            float frameDuration = animation.FrameDuration > 0 ? animation.FrameDuration : 1 / 30.0f;
            int frame = _startFrame + (int)(_clock.Elapsed.TotalSeconds * speed / frameDuration);

            if (frame > animation.FrameCount - 1)
            {
                if (!loopCheck.Checked) { SetFrame(animation.FrameCount - 1); Stop(); return; }
                _clock.Restart();
                _startFrame = 0;
                frame = 0;
            }
            SetFrame(frame);
        }

        private void SetFrame(int frame)
        {
            if (_viewer == null) return;

            HavokPackfile.AnimationClip animation = _clip?.Animation;
            int frames = animation?.FrameCount ?? 0;
            frame = Math.Max(0, Math.Min(Math.Max(0, frames - 1), frame));

            timeline.Frame = frame;
            _viewer.ShowFrame(frame);

            float frameDuration = animation != null && animation.FrameDuration > 0 ? animation.FrameDuration : 1 / 30.0f;
            frameLabel.Text = animation == null ? "-"
                : "frame " + (frame + 1) + " / " + frames + "   " + (frame * frameDuration).ToString("0.00") + "s";
        }
        #endregion

        #region EXPORT
        private void ExportBtn_Click(object sender, EventArgs e)
        {
            if (_clip == null || _skeleton == null) return;
            Stop();

            string filename = AnimationExport.AskWhereToSave(this,
                (_model != null ? Path.GetFileNameWithoutExtension(_model.Name) + "_" : "") + _clip.Name);
            if (filename == null) return;

            //export what's on screen, root motion and all, so the file matches the preview
            CathodeLib.Animation.RootMotion rootMotion = rootMotionCheck.Checked
                ? CathodeLib.Animation.RootMotion.Follow
                : CathodeLib.Animation.RootMotion.Ignore;

            Cursor.Current = Cursors.WaitCursor;
            try
            {
                if (_model != null)
                    _model.ExportMesh(filename, _skeleton, new[] { _clip }, rootMotion, Content?.Level);
                else
                    CathodeLibExtensions.ExportAnimations(_skeleton, new[] { _clip }, filename, rootMotion);

                MessageBox.Show("'" + _clip.Name + "' exported"
                    + (_model == null ? " against the '" + _skeleton.Name + "' skeleton." : " with '" + _model.Name + "' bound to '" + _skeleton.Name + "'.")
                    + (_model == null ? "" : "\n\nA '" + AlienPAK.ModelIO.SidecarExtension + "' file has been written alongside it, holding the parts of the model the mesh format can't store."),
                    "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
