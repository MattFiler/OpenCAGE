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
using LiveLink = OpenCAGE.RuntimeUtilsConnection.LiveLink;
using LiveLinkAnimationDrive = OpenCAGE.RuntimeUtilsConnection.LiveLinkAnimationDrive;

namespace OpenCAGE
{
    /// <summary>
    /// Animation Mode: the window driving the Level Viewer from a playhead, and taking moves made out
    /// there back as keyframes - and, with "In game" ticked, driving the animation in the running game
    /// too (live link). Also the live saving - every edit is written to the entity as it is made, rather
    /// than all at once when the window is closed.
    /// </summary>
    public partial class CAGEAnimationEditor : IAnimationModeHost
    {
        private Button animationModeBtn;
        private Button playBtn;
        private Label playheadLabel;
        private CheckBox loopCheck;
        private System.Windows.Forms.Timer playTimer;

        //The running game: "In game" sits by the mode button while the live link is connected; while the game drives the
        //animation, a row above the footer holds "Run events" and what the game is showing
        private CheckBox inGameCheck;
        private CheckBox runEventsCheck;
        private Label gameStatusLabel;
        private System.Windows.Forms.Timer gameEditTimer;
        private string _baseTitle;

        private float _playheadTime = 0f;
        private DateTime _playStartedAt;
        private float _playStartedFrom;
        //The play the game was asked for: the playhead follows the game's clock once the game shows it (0: none)
        private uint _gamePlaySequence = 0;
        //The placement the game drives, taken as the mode was entered (null: every placement)
        private List<ShortGuid> _gamePath;
        //After a key edit while the game drives the animation: "sending edit..." until the push is done, then "game updated" for a moment
        private string _gameEditNote = null;
        private DateTime _gameEditNoteUntil;

        /// <summary>Off until the window has finished building itself, so setup writes nothing back.</summary>
        private bool _liveCommits = false;

        #region UI

        private const int ANIM_MODE_BTN_W = 150;
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

            loopCheck = new CheckBox()
            {
                Text = "Loop",
                AutoSize = true,
                Visible = false,
                UseVisualStyleBackColor = true,
            };
            loopCheck.CheckedChanged += PlayOptions_CheckedChanged;
            Controls.Add(loopCheck);

            playTimer = new System.Windows.Forms.Timer() { Interval = 33 };
            playTimer.Tick += PlayTimer_Tick;

            inGameCheck = new CheckBox()
            {
                Text = "In game",
                AutoSize = true,
                Visible = LiveLink.Connected,
                Checked = SettingsManager.GetBool(Settings.LiveLinkAnimateInGame, true),
                UseVisualStyleBackColor = true,
            };
            inGameCheck.CheckedChanged += InGameCheck_CheckedChanged;
            Controls.Add(inGameCheck);

            runEventsCheck = new CheckBox()
            {
                Text = "Run events",
                AutoSize = true,
                Visible = false,
                UseVisualStyleBackColor = true,
            };
            runEventsCheck.CheckedChanged += PlayOptions_CheckedChanged;
            Controls.Add(runEventsCheck);

            gameStatusLabel = new Label()
            {
                AutoSize = false,
                AutoEllipsis = true,
                Visible = false,
            };
            Controls.Add(gameStatusLabel);

            //Clears a key edit's note once its push is done, or a moment after it landed
            gameEditTimer = new System.Windows.Forms.Timer() { Interval = 250 };
            gameEditTimer.Tick += GameEditTimer_Tick;

            _baseTitle = Text;

            ToolTip tip = new ToolTip();
            tip.SetToolTip(animationModeBtn,
                "Preview this animation in the Level Viewer, and make keyframes by moving things there.\n"
                + "Nothing is moved for real - leaving the mode puts everything back.");
            tip.SetToolTip(playBtn, "Play the animation through in the viewport.");
            tip.SetToolTip(loopCheck, "Play goes round again from the start, rather than stopping at the end.");
            tip.SetToolTip(inGameCheck,
                "Plays this animation in the running game too, while Animation Mode is on.\n"
                + "Leaving Animation Mode gives the game its animation back. Key edits reach the game as they do already.");
            tip.SetToolTip(runEventsCheck, "While it plays in the game, the animation's events reach the level's scripts.");

            LiveLink.ConnectionChanged += OnLiveLinkConnectionChanged;
            LiveLink.Pushed += OnLiveLinkPushed;
            LiveLinkAnimationDrive.StateChanged += OnGameAnimationChanged;
        }

        private void UnsubscribeGameEvents()
        {
            LiveLink.ConnectionChanged -= OnLiveLinkConnectionChanged;
            LiveLink.Pushed -= OnLiveLinkPushed;
            LiveLinkAnimationDrive.StateChanged -= OnGameAnimationChanged;
            gameEditTimer?.Stop();
        }

        private const int GAME_ROW_H = 24;

        /// <summary>The game's row (Run events, what the game shows), between the graph and the footer while the game drives the animation.</summary>
        private int GameRowHeight => runEventsCheck != null && GameRowShown ? GAME_ROW_H : 0;

        /* Laid out to the left of Close, so the footer reads snap / bezier ... loop play / in game / animation mode / close */
        private void LayoutAnimationModeControls(int footerY, int rightEdge)
        {
            if (animationModeBtn == null) return;

            int x = rightEdge - ANIM_MODE_BTN_W;
            animationModeBtn.SetBounds(x, footerY, ANIM_MODE_BTN_W, 26);

            //Its slot only while it shows (Visible reads false until the window is shown, so it is not asked)
            if (LiveLink.Connected)
            {
                x -= 8 + inGameCheck.Width;
                inGameCheck.Location = new Point(x, footerY + 4);
            }

            x -= 8 + PLAY_BTN_W;
            playBtn.SetBounds(x, footerY, PLAY_BTN_W, 26);

            x -= 8 + loopCheck.Width;
            loopCheck.Location = new Point(x, footerY + 4);

            x -= 8 + Math.Max(44, playheadLabel.Width);
            playheadLabel.Location = new Point(x, footerY + 6);

            int rowY = ClientSize.Height - FOOTER_HEIGHT - GAME_ROW_H;
            runEventsCheck.Location = new Point(LAYOUT_MARGIN, rowY + 4);
            int statusX = runEventsCheck.Right + 16;
            gameStatusLabel.SetBounds(statusX, rowY + 6, Math.Max(40, ClientSize.Width - LAYOUT_MARGIN - statusX), 17);
        }

        private void RefreshAnimationModeUi()
        {
            if (animationModeBtn == null) return;

            bool active = IsAnimationModeActive;
            bool gameRow = GameRowShown;
            animationModeBtn.Text = active ? "Exit Animation Mode" : "Animation Mode";
            animationModeBtn.Font = new Font(animationModeBtn.Font, active ? FontStyle.Bold : FontStyle.Regular);
            playBtn.Visible = active;
            playheadLabel.Visible = active;
            loopCheck.Visible = active;
            //There while the game can be driven, so it can be ticked before the mode is entered as well as during it
            inGameCheck.Visible = LiveLink.Connected;
            runEventsCheck.Visible = gameRow;
            gameStatusLabel.Visible = gameRow;
            Text = _baseTitle + (gameRow ? " (driving the game)" : "");
            playBtn.Text = playTimer.Enabled ? "Stop" : "Play";
            playheadLabel.Text = _playheadTime.ToString("0.00") + "s";
            RefreshGameStatus();

            if (animCurveEditor != null)
            {
                animCurveEditor.ShowPlayhead = active;
                animCurveEditor.PlayheadTime = _playheadTime;
            }
            LayoutEditorControls();
            //The game's row coming or going resizes the graph
            SyncHostedEditorSizes();
        }

        /// <summary>What the game is doing with the animation, beside Run events: the drive's status, and a key edit's progress.</summary>
        private void RefreshGameStatus()
        {
            if (gameStatusLabel == null || IsDisposed)
                return;
            string text = "";
            if (GameRowShown)
            {
                text = LiveLinkAnimationDrive.Status;
                //The game plays to its own copy's length: worth saying while that is not the one here (a length edit on its way)
                LiveLink.GameAnimation game = LiveLinkAnimationDrive.LastGame;
                bool shown = game != null && (game.State == "held" || game.State == "playing" || game.State == "ended");
                if (shown && !double.IsNaN(game.Length) && Math.Abs(game.Length - anim_length) > 0.001)
                    text += " (game length " + game.Length.ToString("0.00") + " s)";
                if (_gameEditNote != null)
                    text += " - " + _gameEditNote;
            }
            gameStatusLabel.Text = text;
        }

        private bool IsAnimationModeActive =>
            AnimationModeSession.Current != null && ReferenceEquals(AnimationModeSession.Current, _session);

        private AnimationModeSession _session;

        /// <summary>The mode is on and drives the animation in the game, which is connected.</summary>
        private bool GameRowShown => IsAnimationModeActive && _session.DrivesGame && LiveLink.Connected;

        #endregion

        #region GAME (live link)

        /* "In game" is remembered for next time; ticked or unticked while the mode is on, it starts or stops the game's drive */
        private void InGameCheck_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Settings.LiveLinkAnimateInGame, inGameCheck.Checked);
            if (!IsAnimationModeActive)
                return;
            if (inGameCheck.Checked)
                StartDrivingGame();
            else
            {
                _gamePlaySequence = 0;
                _session.DriveGame(null);
            }
            RefreshAnimationModeUi();
        }

        /* Loop and Run events change how the game plays it: a play under way goes on from the playhead with them */
        private void PlayOptions_CheckedChanged(object sender, EventArgs e)
        {
            if (playTimer == null || !playTimer.Enabled || !IsAnimationModeActive)
                return;
            _playStartedAt = DateTime.UtcNow;
            _playStartedFrom = _playheadTime;
            if (_session.DrivesGame)
                _gamePlaySequence = _session.PlayInGame(loopCheck.Checked, runEventsCheck.Checked);
        }

        /* The game takes the animation at the playhead - and plays it on, if the playhead is playing */
        private void StartDrivingGame()
        {
            if (!IsAnimationModeActive || !LiveLink.Connected)
                return;
            LiveLinkAnimationDrive.Target target = BuildGameTarget();
            if (target == null)
                return;
            _session.DriveGame(target);
            if (playTimer.Enabled)
            {
                _playStartedAt = DateTime.UtcNow;
                _playStartedFrom = _playheadTime;
                _gamePlaySequence = _session.PlayInGame(loopCheck.Checked, runEventsCheck.Checked);
            }
        }

        /* The animation as the running game knows it: its level's root, its composite, and the placement the mode was
           entered in (null: every placement) */
        private LiveLinkAnimationDrive.Target BuildGameTarget()
        {
            Commands commands = Content?.Level?.Commands;
            if (_animComposite == null || commands == null)
                return null;
            return new LiveLinkAnimationDrive.Target()
            {
                Root = LiveLink.RootOf(commands),
                Composite = _animComposite.shortGUID,
                Entity = animEntity.shortGUID,
                Path = _gamePath != null ? new List<ShortGuid>(_gamePath) : new List<ShortGuid>(),
                Label = commands.Utils.GetEntityName(_animComposite, animEntity),
            };
        }

        /* Raised on any thread: the connection came or went */
        private void OnLiveLinkConnectionChanged()
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(new Action(OnLiveLinkConnectionChangedUi));
            }
            catch (InvalidOperationException)
            {
                //Closing
            }
        }

        /* "In game" shows while the game is connected, and a mode entered before it connected takes the game up now. A
           game that disconnects has given the animation back itself; the drive stays wanted, so a reconnect picks it up -
           and a play under way goes on from the playhead, rather than from where it first started. */
        private void OnLiveLinkConnectionChangedUi()
        {
            if (IsDisposed)
                return;
            if (IsAnimationModeActive && LiveLink.Connected && inGameCheck.Checked && !_session.DrivesGame)
                StartDrivingGame();
            else if (IsAnimationModeActive && LiveLink.Connected && _session.DrivesGame && playTimer.Enabled)
                _gamePlaySequence = _session.PlayInGame(loopCheck.Checked, runEventsCheck.Checked);
            RefreshAnimationModeUi();
        }

        /* Raised on the drive's thread */
        private void OnGameAnimationChanged()
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(new Action(RefreshGameStatus));
            }
            catch (InvalidOperationException)
            {
                //Closing
            }
        }

        /* A key edit while the game drives the animation reaches it through the auto push: said until it has landed */
        private void NoteGameEdit()
        {
            if (!GameRowShown || !LiveLink.IsQueuedOrPushing(_animComposite))
                return;
            _gameEditNote = "sending edit...";
            _gameEditNoteUntil = DateTime.MaxValue;
            gameEditTimer.Start();
            RefreshGameStatus();
        }

        /* UI thread: the auto push (or a Resync) sent a composite. The game re-holds the animation by itself. */
        private void OnLiveLinkPushed(Composite composite, LiveLink.Reply reply)
        {
            if (IsDisposed || composite != _animComposite || !GameRowShown)
                return;
            _gameEditNote = reply.Ok ? "game updated" : "the game did not take the edit (see the status bar)";
            _gameEditNoteUntil = DateTime.UtcNow.AddSeconds(3);
            gameEditTimer.Start();
            RefreshGameStatus();
        }

        private void GameEditTimer_Tick(object sender, EventArgs e)
        {
            //"sending edit..." lasts while the edit waits or is on its way: one that leaves the game's copy as it was is
            //never sent, so never answered
            bool sending = _gameEditNoteUntil == DateTime.MaxValue;
            if (_gameEditNote != null && (sending ? LiveLink.IsQueuedOrPushing(_animComposite) : DateTime.UtcNow < _gameEditNoteUntil))
                return;
            _gameEditNote = null;
            gameEditTimer.Stop();
            RefreshGameStatus();
        }

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
            if (_animComposite == null)
                return;

            //The game shows the animation by itself, so either it or the viewer will do
            bool inGame = inGameCheck.Checked && LiveLink.Connected;
            if (!UnityConnection.Send.Connected && !inGame)
            {
                MessageBox.Show(
                    "The Level Viewer isn't running, so there's nothing to preview the animation in.\n\n"
                    + (LiveLink.Connected
                        ? "Open the viewport and try again, or tick \"In game\" to preview it in the running game."
                        : "Open the viewport and try again."),
                    "Animation Mode",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            /* The hierarchy the user walked through to reach this composite is what says which
               placement of everything the animation drives is the one being previewed - the same entity
               in five instances of a composite is five different things to animate. The game takes the
               same placement, by the rule the inspector's Trigger Method uses. */
            DockPanels.CompositeDisplay display = _entityDisplay?.CompositeDisplay;
            _gamePath = ShowsAnimationComposite ? LiveLink.InstancePath(display, Content?.Level?.Commands) : null;
            _session = AnimationModeSession.Begin(this, HierarchyRootComposite, HierarchyDrillPath(), inGame ? BuildGameTarget() : null);
            RefreshAnimationModeUi();
        }

        private void ExitAnimationMode()
        {
            StopPlayback();
            //Gives the game its animation back too
            AnimationModeSession.EndFor(this);
            _session = null;
            _gameEditNote = null;
            gameEditTimer?.Stop();
            RefreshAnimationModeUi();
        }

        /* The editor may have stepped into or out of a composite instance since the window opened: the
           hierarchy on show is then another composite's, and says nothing about this one's placement */
        private bool ShowsAnimationComposite =>
            _animComposite != null && _entityDisplay?.CompositeDisplay?.Composite == _animComposite;

        private Composite HierarchyRootComposite =>
            (ShowsAnimationComposite ? _entityDisplay.CompositeDisplay.Path?.AllComposites.FirstOrDefault() : null) ?? _animComposite;

        private List<uint> HierarchyDrillPath()
        {
            List<uint> path = new List<uint>();
            DockPanels.CompositeDisplay display = _entityDisplay?.CompositeDisplay;
            if (!ShowsAnimationComposite || display.Path == null)
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

        /// <param name="fromGame">The game's own time as it plays: moves the preview, and is not sent back to the game.</param>
        private void SetPlayheadTime(float time, bool fromGame = false)
        {
            _playheadTime = time < 0f ? 0f : time;
            if (animCurveEditor != null)
                animCurveEditor.PlayheadTime = _playheadTime;
            if (playheadLabel != null)
                playheadLabel.Text = _playheadTime.ToString("0.00") + "s";
            if (IsAnimationModeActive)
                _session.SetTime(_playheadTime, fromGame);
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
            //The game plays it on its own clock, and the playhead follows the game once it does
            _gamePlaySequence = IsAnimationModeActive ? _session.PlayInGame(loopCheck.Checked, runEventsCheck.Checked) : 0;
            playTimer.Start();
            playBtn.Text = "Stop";
        }

        private void StopPlayback()
        {
            if (playTimer == null || !playTimer.Enabled)
                return;
            playTimer.Stop();
            _gamePlaySequence = 0;
            if (playBtn != null)
                playBtn.Text = "Play";
            //The game stops where the playhead did
            if (IsAnimationModeActive)
                _session.HoldInGame();
        }

        private void PlayTimer_Tick(object sender, EventArgs e)
        {
            if (!IsAnimationModeActive)
            {
                StopPlayback();
                return;
            }

            //Once a game frame has played it, the playhead follows the game's clock rather than this one - which carries
            //on from the game's time should the game stop answering
            LiveLink.GameAnimation game = _gamePlaySequence != 0 ? LiveLinkAnimationDrive.LastGame : null;
            if (game != null && game.Shows(_gamePlaySequence) && !double.IsNaN(game.Time))
            {
                _playStartedAt = DateTime.UtcNow;
                _playStartedFrom = (float)game.Time;
                SetPlayheadTime((float)game.Time, fromGame: true);
                if (game.State == "ended")
                    StopPlayback();
                return;
            }

            //Wall clock rather than a fixed step per tick, so the preview runs at the animation's speed
            //however far behind the timer falls
            float elapsed = (float)(DateTime.UtcNow - _playStartedAt).TotalSeconds;
            float time = _playStartedFrom + elapsed;
            if (time >= anim_length)
            {
                if (!loopCheck.Checked || anim_length <= 0.0001f)
                {
                    SetPlayheadTime(anim_length);
                    StopPlayback();
                    return;
                }
                time %= anim_length;
            }
            SetPlayheadTime(time);
        }

        #endregion

        #region IAnimationModeHost

        CAGEAnimation IAnimationModeHost.Animation => animEntity;
        Composite IAnimationModeHost.AnimationComposite => _animComposite;
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
            Composite composite = _animComposite;
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
                //The live link's auto push follows the composite on show, which is another one while the editor has
                //stepped into an instance: named here, so the edit reaches the running game either way
                Singleton.OnCompositesModified?.Invoke(new List<Composite> { composite });
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
            NoteGameEdit();
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

        /* Always resolved by ID, in the composite the window was opened on: the inspector may have moved
           on to another entity (following a T_GUID event link, say), or into another composite, while this
           window stayed open - and whatever it shows then is not this animation. */
        private CAGEAnimation ResolveLevelEntity()
        {
            return _animComposite?.GetEntityByID(animEntity.shortGUID) as CAGEAnimation;
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

            CAGEAnimation original = ResolveLevelEntity();
            Composite composite = _animComposite;
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
