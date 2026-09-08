using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Enums;
using CathodeLib.ObjectExtensions;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Animation Mode: the window driving the Level Viewer from a playhead, and taking moves made out
    /// there back as keyframes. Also the live saving - every edit is written to the entity as it is
    /// made, rather than all at once when the window is closed.
    /// </summary>
    public partial class CAGEAnimationEditor : IAnimationModeHost
    {
        private Button animationModeBtn;
        private Button playBtn;
        private Label playheadLabel;
        private System.Windows.Forms.Timer playTimer;

        private float _playheadTime = 0f;
        private DateTime _playStartedAt;
        private float _playStartedFrom;

        /// <summary>Off until the window has finished building itself, so setup writes nothing back.</summary>
        private bool _liveCommits = false;

        #region UI

        private const int ANIM_MODE_BTN_W = 118;
        private const int PLAY_BTN_W = 62;

        private void SetupAnimationModeControls()
        {
            animationModeBtn = new Button()
            {
                Text = "Animation Mode",
                Size = new Size(ANIM_MODE_BTN_W, 26),
                UseVisualStyleBackColor = true,
            };
            animationModeBtn.Click += AnimationModeBtn_Click;
            Controls.Add(animationModeBtn);

            playBtn = new Button()
            {
                Text = "Play",
                Size = new Size(PLAY_BTN_W, 26),
                Visible = false,
                UseVisualStyleBackColor = true,
            };
            playBtn.Click += PlayBtn_Click;
            Controls.Add(playBtn);

            playheadLabel = new Label()
            {
                AutoSize = true,
                Visible = false,
                Text = "0.00s",
            };
            Controls.Add(playheadLabel);

            playTimer = new System.Windows.Forms.Timer() { Interval = 33 };
            playTimer.Tick += PlayTimer_Tick;

            ToolTip tip = new ToolTip();
            tip.SetToolTip(animationModeBtn,
                "Preview this animation in the Level Viewer, and make keyframes by moving things there.\n"
                + "Nothing is moved for real - leaving the mode puts everything back.");
            tip.SetToolTip(playBtn, "Play the animation through in the viewport.");
        }

        /* Laid out to the left of Close, so the footer reads snap / bezier ... animation mode / close */
        private void LayoutAnimationModeControls(int footerY, int rightEdge)
        {
            if (animationModeBtn == null) return;

            int x = rightEdge - ANIM_MODE_BTN_W;
            animationModeBtn.SetBounds(x, footerY, ANIM_MODE_BTN_W, 26);

            x -= 8 + PLAY_BTN_W;
            playBtn.SetBounds(x, footerY, PLAY_BTN_W, 26);

            x -= 8 + Math.Max(44, playheadLabel.Width);
            playheadLabel.Location = new Point(x, footerY + 6);
        }

        private void RefreshAnimationModeUi()
        {
            if (animationModeBtn == null) return;

            bool active = IsAnimationModeActive;
            animationModeBtn.Text = active ? "Exit Animation Mode" : "Animation Mode";
            animationModeBtn.Font = new Font(animationModeBtn.Font, active ? FontStyle.Bold : FontStyle.Regular);
            playBtn.Visible = active;
            playheadLabel.Visible = active;
            playBtn.Text = playTimer.Enabled ? "Stop" : "Play";
            playheadLabel.Text = _playheadTime.ToString("0.00") + "s";

            if (animCurveEditor != null)
            {
                animCurveEditor.ShowPlayhead = active;
                animCurveEditor.PlayheadTime = _playheadTime;
            }
            LayoutEditorControls();
        }

        private bool IsAnimationModeActive =>
            AnimationModeSession.Current != null && ReferenceEquals(AnimationModeSession.Current, _session);

        private AnimationModeSession _session;

        #endregion

        #region MODE

        private void AnimationModeBtn_Click(object sender, EventArgs e)
        {
            if (IsAnimationModeActive)
                ExitAnimationMode();
            else
                EnterAnimationMode();
        }

        private void EnterAnimationMode()
        {
            if (_entityDisplay?.Composite == null)
                return;

            if (!UnityConnection.Send.Connected)
            {
                MessageBox.Show(
                    "The Level Viewer isn't running, so there's nothing to preview the animation in.\n\n"
                    + "Open the viewport and try again.",
                    "Animation Mode",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            /* The hierarchy the user walked through to reach this composite is what says which
               placement of everything the animation drives is the one being previewed - the same entity
               in five instances of a composite is five different things to animate. */
            _session = AnimationModeSession.Begin(this, HierarchyRootComposite, HierarchyDrillPath());
            RefreshAnimationModeUi();
        }

        private void ExitAnimationMode()
        {
            StopPlayback();
            AnimationModeSession.EndFor(this);
            _session = null;
            RefreshAnimationModeUi();
        }

        private Composite HierarchyRootComposite =>
            _entityDisplay?.CompositeDisplay?.Path?.AllComposites.FirstOrDefault() ?? _entityDisplay?.Composite;

        private List<uint> HierarchyDrillPath()
        {
            List<uint> path = new List<uint>();
            DockPanels.CompositeDisplay display = _entityDisplay?.CompositeDisplay;
            if (display?.Path == null)
                return path;
            foreach (Entity step in display.Path.AllEntities)
                path.Add(step.shortGUID.AsUInt32);
            return path;
        }

        #endregion

        #region PLAYHEAD

        private void AnimCurve_PlayheadMoved(float time)
        {
            StopPlayback();
            SetPlayheadTime(time);
        }

        private void SetPlayheadTime(float time)
        {
            _playheadTime = time < 0f ? 0f : time;
            if (animCurveEditor != null)
                animCurveEditor.PlayheadTime = _playheadTime;
            if (playheadLabel != null)
                playheadLabel.Text = _playheadTime.ToString("0.00") + "s";
            if (IsAnimationModeActive)
                _session.SetTime(_playheadTime);
        }

        private void PlayBtn_Click(object sender, EventArgs e)
        {
            if (playTimer.Enabled)
            {
                StopPlayback();
                return;
            }

            //Starting from the end would show one frame and stop, so a play from there starts over
            if (_playheadTime >= anim_length - 0.0001f)
                SetPlayheadTime(0f);

            _playStartedAt = DateTime.UtcNow;
            _playStartedFrom = _playheadTime;
            playTimer.Start();
            playBtn.Text = "Stop";
        }

        private void StopPlayback()
        {
            if (playTimer == null || !playTimer.Enabled)
                return;
            playTimer.Stop();
            if (playBtn != null)
                playBtn.Text = "Play";
        }

        private void PlayTimer_Tick(object sender, EventArgs e)
        {
            if (!IsAnimationModeActive)
            {
                StopPlayback();
                return;
            }

            //Wall clock rather than a fixed step per tick, so the preview runs at the animation's speed
            //however far behind the timer falls
            float elapsed = (float)(DateTime.UtcNow - _playStartedAt).TotalSeconds;
            float time = _playStartedFrom + elapsed;
            if (time >= anim_length)
            {
                SetPlayheadTime(anim_length);
                StopPlayback();
                return;
            }
            SetPlayheadTime(time);
        }

        #endregion

        #region IAnimationModeHost

        CAGEAnimation IAnimationModeHost.Animation => animEntity;
        Composite IAnimationModeHost.AnimationComposite => _entityDisplay?.Composite;
        LevelContent IAnimationModeHost.AnimationContent => Content;
        float IAnimationModeHost.AnimationLength => anim_length;
        bool IAnimationModeHost.BezierInterpolation => bezierMode.Checked;

        void IAnimationModeHost.OnAnimationEditedExternally(string undoLabel, bool structureChanged)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ((IAnimationModeHost)this).OnAnimationEditedExternally(undoLabel, structureChanged)));
                return;
            }

            /* A keyframe made out in the world lands in the same undo step as the ones either side of
               it - a gizmo drag is one gesture, not one step per packet it sent. */
            CommitLive(undoLabel, mergeable: true);

            if (structureChanged)
            {
                anim_length = Math.Max(anim_length, CalculateAnimLength());
                //The rebuild below would otherwise save again, breaking the run of merged keyframes in two
                _suppressTimelineCommit = true;
                try
                {
                    RebuildTrackTree();
                    SetupAnimTimeline();
                }
                finally
                {
                    _suppressTimelineCommit = false;
                }
                AnimationModeSession.NotifyAnimationEdited();
            }
            else if (animCurveEditor != null)
            {
                animCurveEditor.Rebuild();
            }
        }

        #endregion

        #region LIVE SAVING

        /// <summary>
        /// Write the working copy back to the CAGEAnimation in the level, as one undo step.
        /// </summary>
        /// <remarks>
        /// The window edits a deep copy and this installs a fresh deep copy of it each time, so the
        /// lists the level is holding are never the ones still being edited here - which is what lets
        /// the undo step keep the previous ones as its "before" and have them stay that way.
        /// </remarks>
        private void CommitLive(string label, bool mergeable = false)
        {
            if (!_liveCommits || _committing)
                return;

            CAGEAnimation target = ResolveLevelEntity();
            Composite composite = _entityDisplay?.Composite;
            if (target == null || composite == null)
                return;

            _committing = true;
            try
            {
                animEntity.AddParameter("anim_length", new cFloat(anim_length));

                /* Fresh copies of the four lists rather than of the whole entity: a gizmo drag saves
                   once per packet it sends, and cloning the animation's links and resources with it
                   every time is work nothing here needs. */
                CageAnimationEdit.Lists before = CageAnimationEdit.Lists.Of(target);
                new CageAnimationEdit.Lists()
                {
                    Connections = animEntity.connections.Copy(),
                    EventTracks = animEntity.eventTracks.Copy(),
                    FloatTracks = animEntity.floatTracks.Copy(),
                    Parameters = animEntity.parameters.Copy(),
                }.ApplyTo(target);

                DirtyTracker.MarkLevelDataModified();
                UndoStack.Current.Record(new CageAnimationEdit(composite, target, before,
                    CageAnimationEdit.Lists.Of(target), label ?? DefaultCommitLabel(composite, target))
                {
                    Mergeable = mergeable,
                });

                //What the level animates has moved; the inspector works its purple rows out from this
                CageAnimationDrivers.Invalidate();
                Singleton.OnParameterModified?.Invoke();
            }
            finally
            {
                _committing = false;
            }
        }

        private bool _committing = false;

        /// <summary>
        /// A commit that also tells the inspector to repaint - for the edits that change WHICH
        /// parameters the animation drives, rather than just the values it drives them to.
        /// </summary>
        private void CommitLiveStructural(string label)
        {
            if (!_liveCommits || _suppressTimelineCommit)
                return;
            CommitLive(label);
            AnimationModeSession.NotifyAnimationEdited();
        }

        /// <summary>Set while a rebuild driven by a commit is running, so it does not save again.</summary>
        private bool _suppressTimelineCommit = false;

        /// <summary>
        /// An undo or redo replaced the animation's lists in the level. The working copy here is now the
        /// side that is out of date, so it is taken again from the entity - otherwise the next save
        /// would put the undone edit straight back.
        /// </summary>
        private void OnAnimationReplaced(CAGEAnimation animation)
        {
            if (IsDisposed || animation == null || animation.shortGUID != animEntity.shortGUID)
                return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnAnimationReplaced(animation)));
                return;
            }

            //Nothing here is an edit, so none of it is written back
            _suppressTimelineCommit = true;
            try
            {
                animEntity = animation.Copy();
                anim_length = CalculateAnimLength();
                cFloat storedLength = animEntity.GetParameter("anim_length")?.content as cFloat;
                if (storedLength != null && storedLength.value > anim_length)
                    anim_length = storedLength.value;

                RebuildTrackTree();
                RefreshEventTrackLists();
                SetupAnimTimeline();
            }
            finally
            {
                _suppressTimelineCommit = false;
            }

            if (IsAnimationModeActive)
                _session.Refresh();
        }

        private static string DefaultCommitLabel(Composite composite, CAGEAnimation animation)
        {
            return "Edit animation of " + UndoLabels.Entity(composite, animation);
        }

        /* Always resolved by ID: the inspector may have moved on to another entity while this window
           stayed open (following a T_GUID event link, say). */
        private CAGEAnimation ResolveLevelEntity()
        {
            CAGEAnimation entity = _entityDisplay?.Composite?.GetEntityByID(animEntity.shortGUID) as CAGEAnimation;
            return entity ?? _entityDisplay?.Entity as CAGEAnimation;
        }

        #endregion

        #region EVENT PIN REMOVAL

        /// <summary>
        /// The T_STRING pins these keyframes carry are about to go. Ask first if anything is still
        /// linked to one, because the link goes with it.
        /// </summary>
        /// <remarks>
        /// Asked here, at the removal, rather than at save: with edits written back as they are made
        /// there is no later moment at which the warning would still be something the user can act on.
        /// </remarks>
        private bool ConfirmRemovingStringEvents(List<CAGEAnimation.EventTrack.Keyframe> removing)
        {
            if (removing == null || removing.Count == 0)
                return true;

            CAGEAnimation original = _entityDisplay?.Entity as CAGEAnimation;
            Composite composite = _entityDisplay?.Composite;
            if (original == null || composite == null)
                return true;

            HashSet<ShortGuid> going = new HashSet<ShortGuid>();
            foreach (CAGEAnimation.EventTrack.Keyframe key in removing)
            {
                if (key == null || key.track_type != ANIM_TRACK_TYPE.T_STRING)
                    continue;
                going.Add(key.forward);
                going.Add(key.reverse);
            }
            if (going.Count == 0)
                return true;

            //A pin another keyframe still uses is not going anywhere
            HashSet<ShortGuid> staying = new HashSet<ShortGuid>();
            foreach (CAGEAnimation.EventTrack track in animEntity.eventTracks)
            {
                if (track?.keyframes == null) continue;
                foreach (CAGEAnimation.EventTrack.Keyframe key in track.keyframes)
                {
                    if (key == null || key.track_type != ANIM_TRACK_TYPE.T_STRING || removing.Contains(key))
                        continue;
                    staying.Add(key.forward);
                    staying.Add(key.reverse);
                }
            }

            List<string> connected = new List<string>();
            foreach (ShortGuid pin in going)
            {
                if (staying.Contains(pin)) continue;
                if (!IsStringEventPinConnected(original, composite, pin)) continue;

                string name = ShortGuidUtils.FindString(pin);
                connected.Add(string.IsNullOrEmpty(name) ? pin.ToByteString() : name);
            }

            if (connected.Count == 0)
                return true;

            connected.Sort(StringComparer.OrdinalIgnoreCase);
            return MessageBox.Show(
                "These string event pins still have connections in the flowgraph, and removing this "
                + "would take them away:\n\n"
                + string.Join("\n", connected)
                + "\n\nThose links will break. Continue anyway?",
                "Connected event pins will be removed",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        #endregion
    }
}
