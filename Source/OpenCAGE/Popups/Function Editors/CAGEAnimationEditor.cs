using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Enums;
using CathodeLib.ObjectExtensions;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static CATHODE.Scripting.CAGEAnimation;

namespace OpenCAGE
{
    public partial class CAGEAnimationEditor : BaseWindow
    {
        public Action<CAGEAnimation> OnSaved;

        float anim_length = 0;
        CAGEAnimation animEntity = null; // unique writable instance for this editor session

        List<EntityPath> entityListToHierarchies = new List<EntityPath>();
        List<string> eventTracks = new List<string>();

        CurveEditor animCurveEditor;
        bool _suppressKeyframeEvents = false;
        CAGEAnimation.EventTrack.Keyframe activeGraphEventKeyframe = null;
        Entity activeGuidEventEntity = null;
        CAGEAnimation.FloatTrack.Keyframe activeAnimKeyframe = null;
        CAGEAnimation.EventTrack activeEventTrack = null;
        ObjectType _pendingBindingAssignType = ObjectType.ENTITY;

        // Event-track connections panel (built in code)
        GroupBox eventTrackPanel;
        Panel guidBindingsPanel;
        BindingSlotUi _markerSlot;
        BindingSlotUi _characterSlot;
        BindingSlotUi _cameraSlot;

        private static readonly Color EntityNameColor = Color.FromArgb(30, 100, 180);

        private class BindingSlotUi
        {
            public ObjectType BindingType;
            public GroupBox Group;
            public Label EntityLabel;
            public Button JumpBtn;
            public Button AssignBtn;
            public CAGEAnimation.Connection Connection;
        }

        /// <summary>Track GUIDs the user has unchecked — new tracks default to visible.</summary>
        readonly HashSet<ShortGuid> _hiddenTrackIds = new HashSet<ShortGuid>();
        ImageList _trackTreeStateImages;

        /// <summary>Preferred type for empty event tracks (keyed by track GUID).</summary>
        readonly Dictionary<ShortGuid, ANIM_TRACK_TYPE> _eventTrackPreferredTypes = new Dictionary<ShortGuid, ANIM_TRACK_TYPE>();

        EntityInspector _entityDisplay;

        private const int TRACK_TREE_WIDTH = 220;
        private const int STATE_UNCHECKED = 0;
        private const int STATE_CHECKED = 1;
        private const int STATE_MIXED = 2;

        private class EntityNodeTag
        {
            public EntityPath Path;
        }

        private class ParamNodeTag
        {
            public EntityPath Path;
            public CAGEAnimation.FloatTrack Track;
            public CAGEAnimation.Connection Connection;
            public string Label;
        }

        public CAGEAnimationEditor(EntityInspector entityDisplay) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _entityDisplay = entityDisplay;

            animEntity = ((CAGEAnimation)_entityDisplay.Entity).Copy();
            InitializeComponent();

            SetupSnapControls();
            SetupTrackTree();
            SetupEventTrackPanel();
            SetupGuidKeyframeReassignButton();
            InitBezierModeFromData();
            ApplyInspectorTooltips();

            anim_length = CalculateAnimLength();
            Parameter anim_length_param = animEntity.GetParameter("anim_length");
            if (anim_length_param != null && anim_length_param.content != null)
            {
                float animLengthParam = ((cFloat)anim_length_param.content).value;
                if (animLengthParam > anim_length) anim_length = animLengthParam;
            }

            RebuildTrackTree();

            RefreshEventTrackLists();
            SetupAnimTimeline();

            this.Resize += CAGEAnimationEditor_Resize;
            LayoutEditorControls();

            this.BringToFront();
            this.Focus();
        }

        private const int SIDE_PANEL_WIDTH = 210;
        private const int LAYOUT_MARGIN = 10;
        private const int FOOTER_HEIGHT = 42;
        private const int CONTENT_FOOTER_GAP = 6;
        private const int TRACKS_HEADER_H = 18;

        private EntityPath _pendingEntityLinkPath;

        private void ApplyInspectorTooltips()
        {
            ToolTip tip = new ToolTip();
            tip.SetToolTip(snapToSeconds, "Snap keyframe and event times while dragging.");
            tip.SetToolTip(snapInterval, "Time step used when snap is enabled.");
            tip.SetToolTip(bezierMode, "Show and edit curve handles. Off = linear segments.");
            tip.SetToolTip(trackTree, "Right-click to add entities, parameters, or keyframes.\nCheckboxes toggle curve visibility.");
            tip.SetToolTip(SaveEntity, "Write changes back to the CAGEAnimation entity.");
        }

        private void CAGEAnimationEditor_Resize(object sender, EventArgs e)
        {
            LayoutEditorControls();
            SyncHostedEditorSizes();
        }

        private void LayoutEditorControls()
        {
            int originX = LAYOUT_MARGIN;
            int originY = LAYOUT_MARGIN;
            int contentBottom = ClientSize.Height - FOOTER_HEIGHT - CONTENT_FOOTER_GAP;
            int availW = Math.Max(400, ClientSize.Width - LAYOUT_MARGIN * 2);
            int availH = Math.Max(200, contentBottom - originY);

            int sideW = SIDE_PANEL_WIDTH;
            int contentW = Math.Max(200, availW - sideW - 12);
            int treeW = Math.Min(TRACK_TREE_WIDTH, Math.Max(150, contentW / 4));
            int hostW = Math.Max(160, contentW - treeW - 8);
            int sideX = originX + availW - sideW;

            tracksHeader.Location = new Point(originX, originY);
            int treeTop = originY + TRACKS_HEADER_H + 2;
            int treeH = Math.Max(80, availH - TRACKS_HEADER_H - 2);
            trackTree.SetBounds(originX, treeTop, treeW, treeH);
            animHost.SetBounds(originX + treeW + 8, originY, hostW, availH);

            int sideH = Math.Max(80, availH);
            animKeyframeData.SetBounds(sideX, originY, sideW, Math.Min(108, sideH));
            graphEventData.SetBounds(sideX, originY, sideW, Math.Min(graphEventData.Height, sideH));
            if (eventTrackPanel != null)
                eventTrackPanel.SetBounds(sideX, originY, sideW, Math.Min(348, sideH));

            // Footer: snap cluster left, Save right
            int footerY = ClientSize.Height - FOOTER_HEIGHT + (FOOTER_HEIGHT - 21) / 2;
            int fx = LAYOUT_MARGIN;
            snapToSeconds.Location = new Point(fx, footerY);
            fx += snapToSeconds.Width + 8;
            labelSnap.Location = new Point(fx, footerY + 2);
            fx += labelSnap.Width + 4;
            snapInterval.Location = new Point(fx, footerY - 1);
            fx += snapInterval.Width + 16;
            bezierMode.Location = new Point(fx, footerY);

            int saveW = 120;
            int saveH = 28;
            SaveEntity.SetBounds(
                ClientSize.Width - saveW - LAYOUT_MARGIN,
                ClientSize.Height - FOOTER_HEIGHT + (FOOTER_HEIGHT - saveH) / 2,
                saveW,
                saveH);
        }

        private void SyncHostedEditorSizes()
        {
            if (animCurveEditor != null)
            {
                animCurveEditor.Width = Math.Max(1, animHost.Width);
                animCurveEditor.Height = Math.Max(1, animHost.Height);
            }
        }

        private void RefreshEventTrackLists()
        {
            eventTracks.Clear();
            for (int i = 0; i < animEntity.eventTracks.Count; i++)
            {
                CAGEAnimation.Connection connection = animEntity.connections.FirstOrDefault(o => o.target_track == animEntity.eventTracks[i].shortGUID);
                string label = (connection == null)
                    ? Content.Level.Commands.Utils.GetEntityName(_entityDisplay.Composite, animEntity)
                    : Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity, _entityDisplay.Composite), SettingsManager.GetBool(Settings.ShowShortGuids));
                eventTracks.Add(label);
            }
        }

        private class SnapIntervalOption
        {
            public string Label;
            public float Seconds;
            public override string ToString() { return Label; }
        }

        /// <summary>
        /// Bezier = curves via control points; Linear = straight lines between keyframes.
        /// </summary>
        private CAGEAnimation.InterpolationMode CurrentInterpolationMode
        {
            get { return bezierMode.Checked ? CAGEAnimation.InterpolationMode.Bezier : CAGEAnimation.InterpolationMode.Linear; }
        }

        private bool _suppressBezierModeEvents = false;

        private void InitBezierModeFromData()
        {
            // Default when no keyframes: bezier mode
            bool isBezier = true;
            foreach (CAGEAnimation.FloatTrack track in animEntity.floatTracks)
            {
                if (track.keyframes.Count == 0) continue;
                isBezier = track.keyframes[0].mode == CAGEAnimation.InterpolationMode.Bezier;
                break;
            }

            _suppressBezierModeEvents = true;
            bezierMode.Checked = isBezier;
            _suppressBezierModeEvents = false;
            ApplyInterpolationModeToAllKeys(CurrentInterpolationMode);
        }

        private void bezierMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressBezierModeEvents) return;
            ApplyInterpolationModeToAllKeys(CurrentInterpolationMode);
            if (animCurveEditor != null)
            {
                animCurveEditor.BezierMode = bezierMode.Checked;
                animCurveEditor.RefreshSelectedKeyframeVisual();
            }
        }

        private void ApplyInterpolationModeToAllKeys(CAGEAnimation.InterpolationMode mode)
        {
            foreach (CAGEAnimation.FloatTrack track in animEntity.floatTracks)
            {
                foreach (CAGEAnimation.FloatTrack.Keyframe key in track.keyframes)
                    key.mode = mode;
            }
        }

        private void SetupSnapControls()
        {
            snapInterval.Items.Clear();
            snapInterval.Items.Add(new SnapIntervalOption() { Label = "1 s", Seconds = 1f });
            snapInterval.Items.Add(new SnapIntervalOption() { Label = "1/2 s", Seconds = 0.5f });
            snapInterval.Items.Add(new SnapIntervalOption() { Label = "1/4 s", Seconds = 0.25f });
            snapInterval.Items.Add(new SnapIntervalOption() { Label = "1/8 s", Seconds = 0.125f });
            snapInterval.Items.Add(new SnapIntervalOption() { Label = "1/10 s", Seconds = 0.1f });
            snapInterval.Items.Add(new SnapIntervalOption() { Label = "1/16 s", Seconds = 0.0625f });
            snapInterval.SelectedIndex = 2; // 1/4 s default
            snapInterval.Enabled = snapToSeconds.Checked;
            labelSnap.Enabled = snapToSeconds.Checked;
        }

        private void ApplySnapSettingsToEditor()
        {
            if (animCurveEditor == null) return;
            animCurveEditor.SnapEnabled = snapToSeconds.Checked;
            SnapIntervalOption opt = snapInterval.SelectedItem as SnapIntervalOption;
            animCurveEditor.SnapInterval = opt != null ? opt.Seconds : 0.25f;
        }

        private void snapToSeconds_CheckedChanged(object sender, EventArgs e)
        {
            snapInterval.Enabled = snapToSeconds.Checked;
            labelSnap.Enabled = snapToSeconds.Checked;
            ApplySnapSettingsToEditor();
        }

        private void snapInterval_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplySnapSettingsToEditor();
        }

        private void SetupTrackTree()
        {
            _trackTreeStateImages = CreateCheckStateImageList();
            trackTree.StateImageList = _trackTreeStateImages;
            trackTree.CheckBoxes = false;
            trackTree.ShowLines = true;
            trackTree.ShowPlusMinus = true;
            trackTree.FullRowSelect = true;
            trackTree.NodeMouseClick += TrackTree_NodeMouseClick;

            ContextMenuStrip menu = new ContextMenuStrip();
            _trackTreeAddItem = new ToolStripMenuItem("Add Entity…");
            _trackTreeAddItem.Click += TrackTree_AddEntityLinkClicked;
            _trackTreeMenuSeparator = new ToolStripSeparator();
            _trackTreeAddParamItem = new ToolStripMenuItem("Add Parameter…");
            _trackTreeAddParamItem.Click += TrackTree_AddParameterClicked;
            _trackTreeAddKeyframeItem = new ToolStripMenuItem("Add Keyframe at Time…");
            _trackTreeAddKeyframeItem.Click += TrackTree_AddKeyframeClicked;
            _trackTreeRemoveItem = new ToolStripMenuItem("Remove");
            _trackTreeRemoveItem.Click += TrackTree_RemoveClicked;
            menu.Items.Add(_trackTreeAddItem);
            menu.Items.Add(_trackTreeMenuSeparator);
            menu.Items.Add(_trackTreeAddParamItem);
            menu.Items.Add(_trackTreeAddKeyframeItem);
            menu.Items.Add(_trackTreeRemoveItem);
            menu.Opening += TrackTreeContextMenu_Opening;
            trackTree.ContextMenuStrip = menu;
            trackTree.MouseDown += TrackTree_MouseDown;
        }

        private ToolStripMenuItem _trackTreeAddItem;
        private ToolStripMenuItem _trackTreeAddParamItem;
        private ToolStripMenuItem _trackTreeAddKeyframeItem;
        private ToolStripMenuItem _trackTreeRemoveItem;
        private ToolStripSeparator _trackTreeMenuSeparator;
        private TreeNode _trackTreeContextNode;

        private ImageList CreateCheckStateImageList()
        {
            ImageList list = new ImageList();
            list.ImageSize = new Size(16, 16);
            list.ColorDepth = ColorDepth.Depth32Bit;
            list.Images.Add(DrawCheckGlyph(CheckState.Unchecked));
            list.Images.Add(DrawCheckGlyph(CheckState.Checked));
            list.Images.Add(DrawCheckGlyph(CheckState.Indeterminate));
            return list;
        }

        private static Bitmap DrawCheckGlyph(CheckState state)
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                Rectangle box = new Rectangle(2, 2, 11, 11);
                g.FillRectangle(Brushes.White, box);
                g.DrawRectangle(Pens.Gray, box);
                if (state == CheckState.Checked)
                {
                    using (Pen p = new Pen(Color.FromArgb(0x1f, 0x77, 0xb4), 2))
                    {
                        g.DrawLines(p, new Point[] {
                            new Point(4, 7),
                            new Point(7, 10),
                            new Point(12, 4)
                        });
                    }
                }
                else if (state == CheckState.Indeterminate)
                {
                    using (Brush b = new SolidBrush(Color.FromArgb(0x1f, 0x77, 0xb4)))
                        g.FillRectangle(b, 4, 7, 8, 2);
                }
            }
            return bmp;
        }

        private void SetNodeCheckState(TreeNode node, int state)
        {
            if (node == null) return;
            node.StateImageIndex = state;
        }

        private int GetNodeCheckState(TreeNode node)
        {
            if (node == null) return STATE_UNCHECKED;
            // TreeView returns -1 until a state image is assigned/painted
            int idx = node.StateImageIndex;
            if (idx < STATE_UNCHECKED || idx > STATE_MIXED) return STATE_UNCHECKED;
            return idx;
        }

        private void TrackTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node == null) return;

            if (e.Button == MouseButtons.Right)
            {
                trackTree.SelectedNode = e.Node;
                _trackTreeContextNode = e.Node;
                return;
            }

            TreeViewHitTestInfo hit = trackTree.HitTest(e.Location);
            if (hit.Location == TreeViewHitTestLocations.StateImage)
            {
                if (e.Node.Tag is EntityNodeTag)
                {
                    int current = GetNodeCheckState(e.Node);
                    bool checkAll = current != STATE_CHECKED;
                    int next = checkAll ? STATE_CHECKED : STATE_UNCHECKED;
                    SetNodeCheckState(e.Node, next);
                    foreach (TreeNode child in e.Node.Nodes)
                    {
                        SetNodeCheckState(child, next);
                        ParamNodeTag param = child.Tag as ParamNodeTag;
                        if (param != null && param.Track != null)
                            SetTrackHidden(param.Track.shortGUID, !checkAll);
                    }
                }
                else if (e.Node.Tag is ParamNodeTag)
                {
                    ParamNodeTag param = (ParamNodeTag)e.Node.Tag;
                    bool currentlyChecked = GetNodeCheckState(e.Node) == STATE_CHECKED;
                    bool nextChecked = !currentlyChecked;
                    SetNodeCheckState(e.Node, nextChecked ? STATE_CHECKED : STATE_UNCHECKED);
                    if (param.Track != null)
                        SetTrackHidden(param.Track.shortGUID, !nextChecked);
                    UpdateParentCheckState(e.Node.Parent);
                }

                ApplyTrackVisibilityToEditor();
                return;
            }

            // Clicking the entity/parameter label frames those tracks in the graph
            FocusGraphOnTreeNode(e.Node);
        }

        private void TrackTree_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            // Set context node here — ContextMenu Opening can fire before NodeMouseClick
            TreeViewHitTestInfo hit = trackTree.HitTest(e.Location);
            _trackTreeContextNode = hit.Node;
            if (hit.Node != null)
                trackTree.SelectedNode = hit.Node;
        }

        private void TrackTreeContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            TreeNode node = _trackTreeContextNode;
            if (node == null)
            {
                // Fallback if Opening somehow precedes MouseDown hit-test
                Point client = trackTree.PointToClient(Control.MousePosition);
                node = trackTree.HitTest(client).Node;
            }

            bool onEntity = node != null && node.Tag is EntityNodeTag;
            bool onParam = node != null && node.Tag is ParamNodeTag;
            _trackTreeContextNode = (onEntity || onParam) ? node : null;

            // Always available
            _trackTreeAddItem.Visible = true;
            // Entity-contextual
            _trackTreeAddParamItem.Visible = onEntity;
            _trackTreeRemoveItem.Visible = onEntity || onParam;
            // Parameter-contextual
            _trackTreeAddKeyframeItem.Visible = onParam;

            _trackTreeMenuSeparator.Visible = onEntity || onParam;

            if (onEntity)
                _trackTreeRemoveItem.Text = "Remove Entity…";
            else if (onParam)
                _trackTreeRemoveItem.Text = "Remove Parameter…";
            else
                _trackTreeRemoveItem.Text = "Remove";
        }

        private void TrackTree_AddEntityLinkClicked(object sender, EventArgs e)
        {
            BeginAddEntityLink();
        }

        private void TrackTree_AddParameterClicked(object sender, EventArgs e)
        {
            TreeNode node = _trackTreeContextNode ?? trackTree.SelectedNode;
            EntityNodeTag entityTag = node?.Tag as EntityNodeTag;
            if (entityTag == null || entityTag.Path == null) return;

            _pendingEntityLinkPath = entityTag.Path;
            SelectEntityInTree(entityTag.Path);
            PromptParameterForPendingEntity();
        }

        private void TrackTree_AddKeyframeClicked(object sender, EventArgs e)
        {
            TreeNode node = _trackTreeContextNode ?? trackTree.SelectedNode;
            ParamNodeTag param = node?.Tag as ParamNodeTag;
            if (param == null || param.Track == null) return;

            float defaultTime = SuggestNewKeyframeTime(param.Track);
            CAGEAnimation_AddKeyframeAtTime dialog = new CAGEAnimation_AddKeyframeAtTime(defaultTime, anim_length);
            dialog.OnTimeEntered += (time) => AddKeyframeAtTime(param.Track, time);
            dialog.Show(this);
        }

        private float SuggestNewKeyframeTime(CAGEAnimation.FloatTrack track)
        {
            if (track == null || track.keyframes == null || track.keyframes.Count == 0)
                return 0f;
            if (track.keyframes.Count == 1)
            {
                float t0 = track.keyframes[0].time;
                if (Math.Abs(t0) < 0.0001f && anim_length > 0f)
                    return anim_length;
                return 0f;
            }
            return Math.Max(0f, anim_length * 0.5f);
        }

        private void AddKeyframeAtTime(CAGEAnimation.FloatTrack track, float time)
        {
            if (track == null) return;

            const float eps = 0.0001f;
            for (int i = 0; i < track.keyframes.Count; i++)
            {
                if (Math.Abs(track.keyframes[i].time - time) <= eps)
                {
                    MessageBox.Show("A keyframe already exists at that time.", "Duplicate keyframe", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            float value = 0f;
            if (track.keyframes.Count > 0)
            {
                // Prefer nearest existing key value (works for single-key tracks)
                CAGEAnimation.FloatTrack.Keyframe nearest = track.keyframes[0];
                float best = Math.Abs(nearest.time - time);
                for (int i = 1; i < track.keyframes.Count; i++)
                {
                    float d = Math.Abs(track.keyframes[i].time - time);
                    if (d < best)
                    {
                        best = d;
                        nearest = track.keyframes[i];
                    }
                }
                value = nearest.value.Y;
            }

            CAGEAnimation.FloatTrack.Keyframe key = new CAGEAnimation.FloatTrack.Keyframe();
            key.time = time;
            key.value.Y = value;
            key.mode = CurrentInterpolationMode;
            key.tan_in = new System.Numerics.Vector2(1f, 0f);
            key.tan_out = new System.Numerics.Vector2(1f, 0f);
            track.keyframes.Add(key);
            track.keyframes = track.keyframes.OrderBy(k => k.time).ToList();

            if (time > anim_length)
                anim_length = time;

            SetupAnimTimeline();
            this.BringToFront();
            this.Focus();
        }

        private void TrackTree_RemoveClicked(object sender, EventArgs e)
        {
            TreeNode node = _trackTreeContextNode ?? trackTree.SelectedNode;
            _trackTreeContextNode = null;
            if (node == null) return;

            if (node.Tag is ParamNodeTag param)
            {
                if (param.Track == null) return;
                string label = string.IsNullOrEmpty(param.Label) ? "this parameter" : param.Label;
                if (MessageBox.Show(
                        "Remove \"" + label + "\" from this animation?",
                        "Remove Parameter",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                RemoveFloatTrack(param.Track);
            }
            else if (node.Tag is EntityNodeTag entity)
            {
                string label = node.Text;
                if (MessageBox.Show(
                        "Remove all animated parameters for \"" + label + "\" from this animation?",
                        "Remove Entity",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                RemoveEntityFromAnimation(entity.Path);
            }
        }

        private void RemoveFloatTrack(CAGEAnimation.FloatTrack track)
        {
            if (track == null) return;

            List<CAGEAnimation.Connection> trimmedConnections = new List<CAGEAnimation.Connection>();
            for (int i = 0; i < animEntity.connections.Count; i++)
            {
                if (animEntity.connections[i].target_track == track.shortGUID) continue;
                trimmedConnections.Add(animEntity.connections[i]);
            }
            animEntity.connections = trimmedConnections;
            animEntity.floatTracks.Remove(track);
            _hiddenTrackIds.Remove(track.shortGUID);
            RebuildTrackTree();
            SetupAnimTimeline();
        }

        private void RemoveEntityFromAnimation(EntityPath path)
        {
            if (path == null) return;

            HashSet<ShortGuid> tracksToRemove = new HashSet<ShortGuid>();
            List<CAGEAnimation.Connection> trimmedConnections = new List<CAGEAnimation.Connection>();
            for (int i = 0; i < animEntity.connections.Count; i++)
            {
                CAGEAnimation.Connection conn = animEntity.connections[i];
                if (conn.binding_type == ObjectType.ENTITY && conn.connectedEntity == path)
                {
                    tracksToRemove.Add(conn.target_track);
                    continue;
                }
                trimmedConnections.Add(conn);
            }
            animEntity.connections = trimmedConnections;

            animEntity.floatTracks.RemoveAll(t => tracksToRemove.Contains(t.shortGUID));
            foreach (ShortGuid id in tracksToRemove)
                _hiddenTrackIds.Remove(id);

            RebuildTrackTree();
            SetupAnimTimeline();
        }

        private void FocusGraphOnTreeNode(TreeNode node)
        {
            if (animCurveEditor == null || node == null) return;

            List<CAGEAnimation.FloatTrack> tracks = new List<CAGEAnimation.FloatTrack>();
            if (node.Tag is ParamNodeTag param && param.Track != null)
            {
                tracks.Add(param.Track);
            }
            else if (node.Tag is EntityNodeTag)
            {
                foreach (TreeNode child in node.Nodes)
                {
                    ParamNodeTag childParam = child.Tag as ParamNodeTag;
                    if (childParam != null && childParam.Track != null)
                        tracks.Add(childParam.Track);
                }
            }

            if (tracks.Count > 0)
                animCurveEditor.FitToTracks(tracks);
        }

        private void SetTrackHidden(ShortGuid trackId, bool hidden)
        {
            if (hidden) _hiddenTrackIds.Add(trackId);
            else _hiddenTrackIds.Remove(trackId);
        }

        private bool IsTrackVisible(ShortGuid trackId)
        {
            return !_hiddenTrackIds.Contains(trackId);
        }

        private void UpdateParentCheckState(TreeNode parent)
        {
            if (parent == null || parent.Nodes.Count == 0) return;
            int checkedCount = 0;
            foreach (TreeNode child in parent.Nodes)
            {
                if (GetNodeCheckState(child) == STATE_CHECKED)
                    checkedCount++;
            }
            if (checkedCount == 0)
                SetNodeCheckState(parent, STATE_UNCHECKED);
            else if (checkedCount == parent.Nodes.Count)
                SetNodeCheckState(parent, STATE_CHECKED);
            else
                SetNodeCheckState(parent, STATE_MIXED);
        }

        private void RebuildTrackTree(EntityPath selectPath = null)
        {
            EntityPath preferSelect = selectPath ?? GetActiveEntityPath();
            ShortGuid? preferTrack = null;
            if (trackTree.SelectedNode != null && trackTree.SelectedNode.Tag is ParamNodeTag selParam && selParam.Track != null)
                preferTrack = selParam.Track.shortGUID;

            Dictionary<string, bool> expanded = new Dictionary<string, bool>();
            foreach (TreeNode n in trackTree.Nodes)
            {
                EntityNodeTag tag = n.Tag as EntityNodeTag;
                if (tag != null)
                    expanded[EntityPathKey(tag.Path)] = n.IsExpanded;
            }

            trackTree.BeginUpdate();
            trackTree.Nodes.Clear();
            entityListToHierarchies = new List<EntityPath>();

            List<CAGEAnimation.Connection> entityConnections = animEntity.connections.FindAll(o => o.binding_type == ObjectType.ENTITY);
            // Group by entity path string
            Dictionary<string, EntityPath> pathsByKey = new Dictionary<string, EntityPath>();
            Dictionary<string, List<CAGEAnimation.Connection>> connectionsByEntity = new Dictionary<string, List<CAGEAnimation.Connection>>();
            foreach (CAGEAnimation.Connection connection in entityConnections)
            {
                string key = EntityPathKey(connection.connectedEntity);
                if (!pathsByKey.ContainsKey(key))
                {
                    pathsByKey[key] = connection.connectedEntity;
                    connectionsByEntity[key] = new List<CAGEAnimation.Connection>();
                    entityListToHierarchies.Add(connection.connectedEntity);
                }
                connectionsByEntity[key].Add(connection);
            }

            TreeNode nodeToSelect = null;
            int paletteIndex = 0;
            foreach (var kvp in connectionsByEntity)
            {
                EntityPath path = pathsByKey[kvp.Key];
                string entityLabel = Content.Level.Commands.Utils.GetResolvedAsString(
                    Content.Level.Commands.Utils.ResolveAlias(path, _entityDisplay.Composite),
                    SettingsManager.GetBool(Settings.ShowShortGuids));

                TreeNode entityNode = new TreeNode(entityLabel);
                entityNode.Tag = new EntityNodeTag() { Path = path };

                int visibleParams = 0;
                int totalParams = 0;
                foreach (CAGEAnimation.Connection connection in kvp.Value)
                {
                    CAGEAnimation.FloatTrack track = animEntity.floatTracks.FirstOrDefault(o => o.shortGUID == connection.target_track);
                    if (track == null) continue;

                    string paramLabel = connection.target_sub_param.ToString() == ""
                        ? connection.target_param.ToString()
                        : connection.target_param.ToString() + " [" + connection.target_sub_param.ToString() + "]";

                    TreeNode paramNode = new TreeNode(paramLabel);
                    // Match CurveEditor.AddCurve palette order (entity → params)
                    System.Windows.Media.Color mc = CurveEditor.GetPaletteColor(paletteIndex++);
                    paramNode.ForeColor = System.Drawing.Color.FromArgb(mc.R, mc.G, mc.B);
                    paramNode.Tag = new ParamNodeTag()
                    {
                        Path = path,
                        Track = track,
                        Connection = connection,
                        Label = paramLabel
                    };
                    entityNode.Nodes.Add(paramNode);
                    bool visible = IsTrackVisible(track.shortGUID);
                    SetNodeCheckState(paramNode, visible ? STATE_CHECKED : STATE_UNCHECKED);
                    totalParams++;
                    if (visible) visibleParams++;

                    if (preferTrack.HasValue && track.shortGUID == preferTrack.Value)
                        nodeToSelect = paramNode;
                }

                trackTree.Nodes.Add(entityNode);

                if (totalParams == 0)
                    SetNodeCheckState(entityNode, STATE_UNCHECKED);
                else if (visibleParams == 0)
                    SetNodeCheckState(entityNode, STATE_UNCHECKED);
                else if (visibleParams == totalParams)
                    SetNodeCheckState(entityNode, STATE_CHECKED);
                else
                    SetNodeCheckState(entityNode, STATE_MIXED);

                bool wasExpanded;
                if (expanded.TryGetValue(kvp.Key, out wasExpanded))
                {
                    if (wasExpanded) entityNode.Expand();
                }
                else
                    entityNode.Expand();

                if (nodeToSelect == null && preferSelect != null && EntityPathKey(preferSelect) == kvp.Key)
                    nodeToSelect = entityNode;
            }

            trackTree.EndUpdate();

            if (nodeToSelect != null)
                trackTree.SelectedNode = nodeToSelect;
            else if (trackTree.Nodes.Count > 0)
                trackTree.SelectedNode = trackTree.Nodes[0];
        }

        private static string EntityPathKey(EntityPath path)
        {
            if (path == null || path.path == null) return "";
            return string.Join("/", path.path.Select(g => g.AsUInt32.ToString("X8")));
        }

        private EntityPath GetActiveEntityPath()
        {
            TreeNode node = trackTree.SelectedNode;
            if (node == null) return null;
            if (node.Tag is ParamNodeTag param) return param.Path;
            if (node.Tag is EntityNodeTag entity) return entity.Path;
            return null;
        }

        private void ApplyTrackVisibilityToEditor()
        {
            if (animCurveEditor == null) return;
            List<CAGEAnimation.FloatTrack> visible = new List<CAGEAnimation.FloatTrack>();
            foreach (TreeNode entityNode in trackTree.Nodes)
            {
                foreach (TreeNode paramNode in entityNode.Nodes)
                {
                    ParamNodeTag tag = paramNode.Tag as ParamNodeTag;
                    if (tag != null && tag.Track != null && IsTrackVisible(tag.Track.shortGUID))
                        visible.Add(tag.Track);
                }
            }
            animCurveEditor.SetCurvesVisible(visible);
        }

        private void SelectEntityInTree(EntityPath path)
        {
            string key = EntityPathKey(path);
            foreach (TreeNode node in trackTree.Nodes)
            {
                EntityNodeTag tag = node.Tag as EntityNodeTag;
                if (tag != null && EntityPathKey(tag.Path) == key)
                {
                    trackTree.SelectedNode = node;
                    node.EnsureVisible();
                    return;
                }
            }
        }

        public float CalculateAnimLength()
        {
            float animLength = 0.0f;
            for (int i = 0; i < animEntity.floatTracks.Count; i++)
            {
                for (int x = 0; x < animEntity.floatTracks[i].keyframes.Count; x++)
                {
                    if (animLength < animEntity.floatTracks[i].keyframes[x].time)
                        animLength = animEntity.floatTracks[i].keyframes[x].time;
                }
            }
            for (int i = 0; i < animEntity.eventTracks.Count; i++)
            {
                for (int x = 0; x < animEntity.eventTracks[i].keyframes.Count; x++)
                {
                    if (animLength < animEntity.eventTracks[i].keyframes[x].time)
                        animLength = animEntity.eventTracks[i].keyframes[x].time;
                }
            }
            return animLength;
        }

        private void SetupAnimTimeline()
        {
            activeAnimKeyframe = null;
            activeGraphEventKeyframe = null;
            activeEventTrack = null;
            animCurveEditor = null;
            animHost.Child = null;
            animKeyframeData.Visible = false;
            graphEventData.Visible = false;
            if (eventTrackPanel != null) eventTrackPanel.Visible = false;

            int hostW = Math.Max(1, animHost.Width);
            int hostH = Math.Max(1, animHost.Height);
            CurveEditor editor = new CurveEditor(hostW, hostH);
            editor.Setup(Math.Max(0.01f, anim_length));

            // Add all float tracks; visibility comes from the checked tree
            foreach (TreeNode entityNode in trackTree.Nodes)
            {
                string entityLabel = entityNode.Text;
                foreach (TreeNode paramNode in entityNode.Nodes)
                {
                    ParamNodeTag tag = paramNode.Tag as ParamNodeTag;
                    if (tag == null || tag.Track == null) continue;

                    // Use hidden-track set as source of truth — StateImageIndex can be -1 before the tree paints
                    bool visible = tag.Track != null && IsTrackVisible(tag.Track.shortGUID);
                    string curveName = entityLabel + " / " + tag.Label;
                    editor.AddCurve(tag.Track, curveName, visible);
                }
            }

            // Event track lanes + markers (yellow = T_STRING, blue = T_GUID)
            for (int i = 0; i < animEntity.eventTracks.Count; i++)
            {
                CAGEAnimation.EventTrack eventTrack = animEntity.eventTracks[i];
                // Register lane even if empty so the bar still appears
                editor.SetEventTrackLabel(eventTrack, "");
                ANIM_TRACK_TYPE preferred;
                if (_eventTrackPreferredTypes.TryGetValue(eventTrack.shortGUID, out preferred))
                    editor.SetEventTrackPreferredType(eventTrack, preferred);
                for (int x = 0; x < eventTrack.keyframes.Count; x++)
                {
                    CAGEAnimation.EventTrack.Keyframe key = eventTrack.keyframes[x];
                    editor.AddEvent(eventTrack, key, GetEventMarkerLabel(key));
                }
            }

            editor.KeyframeSelected += AnimCurve_KeyframeSelected;
            editor.SelectionCleared += AnimCurve_SelectionCleared;
            editor.EventSelected += AnimCurve_EventSelected;
            editor.EventSelectionCleared += AnimCurve_EventSelectionCleared;
            editor.EventTrackSelected += AnimCurve_EventTrackSelected;
            editor.EventTrackSelectionCleared += AnimCurve_EventTrackSelectionCleared;
            editor.EventAddRequested += AnimCurve_EventAddRequested;
            editor.EventTrackAddRequested += AnimCurve_EventTrackAddRequested;
            editor.EventTrackDeleteRequested += AnimCurve_EventTrackDeleteRequested;
            editor.AnimLengthChanged += AnimCurve_AnimLengthChanged;
            animCurveEditor = editor;
            ApplySnapSettingsToEditor();
            editor.BezierMode = bezierMode.Checked;
            editor.Rebuild();
            animHost.Child = editor;
        }

        private void AnimCurve_AnimLengthChanged(float length)
        {
            anim_length = length;
        }

        private void AnimCurve_KeyframeSelected(CAGEAnimation.FloatTrack.Keyframe kf)
        {
            activeAnimKeyframe = kf;
            activeGraphEventKeyframe = null;
            activeGuidEventEntity = null;
            activeEventTrack = null;
            animKeyframeData.Visible = true;
            graphEventData.Visible = false;
            if (eventTrackPanel != null) eventTrackPanel.Visible = false;

            _suppressKeyframeEvents = true;
            animKeyframeValue.Text = kf.value.Y.ToString();
            _suppressKeyframeEvents = false;
        }
        private void AnimCurve_SelectionCleared()
        {
            activeAnimKeyframe = null;
            animKeyframeData.Visible = false;
        }

        private string GetEventMarkerLabel(CAGEAnimation.EventTrack.Keyframe key)
        {
            if (key == null) return "event";

            if (key.track_type == ANIM_TRACK_TYPE.T_GUID)
            {
                Entity ent = ResolveGuidEventEntity(key.forward);
                if (ent != null && _entityDisplay?.Composite != null)
                    return Content.Level.Commands.Utils.GetEntityName(_entityDisplay.Composite, ent);
                return key.forward.ToByteString();
            }

            // T_STRING: show the forward event name (not reverse_)
            string name = key.forward.ToString();
            return string.IsNullOrWhiteSpace(name) ? "event" : name;
        }

        private Entity ResolveGuidEventEntity(ShortGuid id)
        {
            if (_entityDisplay?.Composite == null) return null;
            return _entityDisplay.Composite.GetEntityByID(id);
        }

        Button reassignGuidEntityBtn;

        private void SetupGuidKeyframeReassignButton()
        {
            reassignGuidEntityBtn = new Button();
            reassignGuidEntityBtn.Text = "Reassign…";
            reassignGuidEntityBtn.Location = new Point(10, 102);
            reassignGuidEntityBtn.Size = new Size(178, 26);
            reassignGuidEntityBtn.Visible = false;
            reassignGuidEntityBtn.Click += reassignGuidEntityBtn_Click;
            graphEventData.Controls.Add(reassignGuidEntityBtn);
        }

        private void ShowStringEventPanel(CAGEAnimation.EventTrack.Keyframe kf)
        {
            label13.Text = "Event name:";
            label13.Location = new Point(8, 18);
            graphEventParam1.Visible = true;
            graphEventParam1.Location = new Point(10, 34);
            graphEventParam1.Size = new Size(178, 20);
            graphGuidEntityName.Visible = false;
            openGuidEntityBtn.Visible = false;
            if (reassignGuidEntityBtn != null) reassignGuidEntityBtn.Visible = false;
            if (stringEventHint != null) stringEventHint.Visible = true;
            deleteGraphEvent.Text = "Delete Event";
            deleteGraphEvent.Location = new Point(10, 90);
            deleteGraphEvent.Size = new Size(178, 26);
            graphEventData.Text = "Event";
            graphEventData.Height = 128;

            _suppressKeyframeEvents = true;
            graphEventParam1.Text = kf.forward.ToString();
            _suppressKeyframeEvents = false;
        }

        private void ShowGuidEventPanel(CAGEAnimation.EventTrack.Keyframe kf)
        {
            label13.Text = "Animation entity:";
            label13.Location = new Point(8, 18);
            graphEventParam1.Visible = false;
            graphGuidEntityName.Visible = true;
            graphGuidEntityName.Location = new Point(10, 34);
            graphGuidEntityName.Size = new Size(178, 34);
            if (stringEventHint != null) stringEventHint.Visible = false;
            openGuidEntityBtn.Visible = true;
            openGuidEntityBtn.Location = new Point(10, 74);
            openGuidEntityBtn.Size = new Size(178, 26);
            if (reassignGuidEntityBtn != null)
            {
                reassignGuidEntityBtn.Visible = true;
                reassignGuidEntityBtn.Location = new Point(10, 104);
                reassignGuidEntityBtn.Size = new Size(178, 26);
            }
            deleteGraphEvent.Text = "Delete Keyframe";
            deleteGraphEvent.Location = new Point(10, 134);
            deleteGraphEvent.Size = new Size(178, 26);
            graphEventData.Text = "Animation Entity";
            graphEventData.Height = 172;

            activeGuidEventEntity = ResolveGuidEventEntity(kf.forward);
            if (activeGuidEventEntity != null && _entityDisplay?.Composite != null)
                graphGuidEntityName.Text = Content.EditorUtils.GenerateEntityName(activeGuidEventEntity, _entityDisplay.Composite);
            else
                graphGuidEntityName.Text = kf.forward.ToByteString();

            openGuidEntityBtn.Enabled = activeGuidEventEntity != null;
            if (reassignGuidEntityBtn != null) reassignGuidEntityBtn.Enabled = true;
        }

        private void AnimCurve_EventSelected(CAGEAnimation.EventTrack.Keyframe kf)
        {
            activeGraphEventKeyframe = kf;
            activeAnimKeyframe = null;
            graphEventData.Visible = true;
            animKeyframeData.Visible = false;
            if (eventTrackPanel != null) eventTrackPanel.Visible = false;

            if (kf.track_type == ANIM_TRACK_TYPE.T_GUID)
                ShowGuidEventPanel(kf);
            else
                ShowStringEventPanel(kf);
            LayoutEditorControls();
        }

        private void AnimCurve_EventSelectionCleared()
        {
            activeGraphEventKeyframe = null;
            activeGuidEventEntity = null;
            graphEventData.Visible = false;

            // Restore track bindings panel if a GUID track is still selected
            if (activeEventTrack != null
                && animCurveEditor != null
                && animCurveEditor.GetEventTrackType(activeEventTrack) == ANIM_TRACK_TYPE.T_GUID)
            {
                ShowEventTrackPanel(activeEventTrack);
            }
        }

        private void AnimCurve_EventTrackSelected(CAGEAnimation.EventTrack track)
        {
            activeEventTrack = track;
            // Lane bar click clears keyframe selection first; keyframe clicks keep the event panel instead
            if (activeGraphEventKeyframe != null)
            {
                // Still refresh binding slots in the background for GUID tracks
                if (animCurveEditor != null && animCurveEditor.GetEventTrackType(track) == ANIM_TRACK_TYPE.T_GUID)
                    RefreshBindingSlots(track);
                return;
            }

            activeAnimKeyframe = null;
            animKeyframeData.Visible = false;
            graphEventData.Visible = false;

            // STRING tracks have no entity bindings — don't open a side panel
            if (animCurveEditor == null || animCurveEditor.GetEventTrackType(track) != ANIM_TRACK_TYPE.T_GUID)
            {
                ClearBindingSlots();
                if (eventTrackPanel != null) eventTrackPanel.Visible = false;
                return;
            }

            ShowEventTrackPanel(track);
        }

        private void AnimCurve_EventTrackSelectionCleared()
        {
            activeEventTrack = null;
            ClearBindingSlots();
            if (eventTrackPanel != null) eventTrackPanel.Visible = false;
        }

        private void SetupEventTrackPanel()
        {
            eventTrackPanel = new GroupBox();
            eventTrackPanel.Name = "eventTrackPanel";
            eventTrackPanel.Text = "Animation Entity Track";
            eventTrackPanel.Visible = false;
            eventTrackPanel.TabStop = false;

            guidBindingsPanel = new Panel();
            guidBindingsPanel.Location = new Point(6, 18);
            guidBindingsPanel.Size = new Size(198, 320);
            guidBindingsPanel.Visible = true;

            const int slotH = 96;
            const int slotGap = 6;
            _markerSlot = CreateBindingSlot(ObjectType.MARKER, 0, slotH);
            _characterSlot = CreateBindingSlot(ObjectType.CHARACTER, slotH + slotGap, slotH);
            _cameraSlot = CreateBindingSlot(ObjectType.CAMERA, (slotH + slotGap) * 2, slotH);

            eventTrackPanel.Controls.Add(guidBindingsPanel);
            this.Controls.Add(eventTrackPanel);
            eventTrackPanel.BringToFront();
        }

        private BindingSlotUi CreateBindingSlot(ObjectType bindingType, int y, int height)
        {
            BindingSlotUi slot = new BindingSlotUi();
            slot.BindingType = bindingType;

            slot.Group = new GroupBox();
            slot.Group.Text = bindingType.ToString();
            slot.Group.Location = new Point(0, y);
            slot.Group.Size = new Size(192, height);
            slot.Group.TabStop = false;

            slot.EntityLabel = new Label();
            slot.EntityLabel.AutoSize = false;
            slot.EntityLabel.AutoEllipsis = true;
            slot.EntityLabel.Font = new Font(Font, FontStyle.Bold);
            slot.EntityLabel.Location = new Point(8, 18);
            slot.EntityLabel.Size = new Size(176, 30);
            slot.EntityLabel.Text = "(none)";
            slot.EntityLabel.ForeColor = SystemColors.GrayText;

            slot.JumpBtn = new Button();
            slot.JumpBtn.Text = "Jump";
            slot.JumpBtn.Location = new Point(8, 52);
            slot.JumpBtn.Size = new Size(84, 26);
            slot.JumpBtn.Enabled = false;
            slot.JumpBtn.Tag = slot;
            slot.JumpBtn.Click += BindingSlot_JumpClicked;

            slot.AssignBtn = new Button();
            slot.AssignBtn.Text = "Assign…";
            slot.AssignBtn.Location = new Point(98, 52);
            slot.AssignBtn.Size = new Size(84, 26);
            slot.AssignBtn.Tag = slot;
            slot.AssignBtn.Click += BindingSlot_AssignClicked;

            slot.Group.Controls.Add(slot.EntityLabel);
            slot.Group.Controls.Add(slot.JumpBtn);
            slot.Group.Controls.Add(slot.AssignBtn);
            guidBindingsPanel.Controls.Add(slot.Group);
            return slot;
        }

        private void ShowEventTrackPanel(CAGEAnimation.EventTrack track)
        {
            if (eventTrackPanel == null || track == null) return;

            if (animCurveEditor == null || animCurveEditor.GetEventTrackType(track) != ANIM_TRACK_TYPE.T_GUID)
            {
                ClearBindingSlots();
                eventTrackPanel.Visible = false;
                return;
            }

            eventTrackPanel.Text = "Animation Entity Track";
            eventTrackPanel.Visible = true;
            if (guidBindingsPanel != null) guidBindingsPanel.Visible = true;
            RefreshBindingSlots(track);
            LayoutEditorControls();
        }

        private static List<FunctionType> GuidKeyframeFunctionTypes = new List<FunctionType>()
        {
            FunctionType.CMD_PlayAnimation,
            FunctionType.CameraPlayAnimation,
            FunctionType.PlayEnvironmentAnimation,
        };

        private static List<FunctionType> FunctionTypesForBinding(ObjectType binding)
        {
            switch (binding)
            {
                case ObjectType.CHARACTER:
                    return new List<FunctionType>()
                    {
                        FunctionType.Character,
                        FunctionType.VariableThePlayer,
                    };
                case ObjectType.MARKER:
                    return new List<FunctionType>()
                    {
                        FunctionType.PositionMarker,
                        FunctionType.ModelReference,
                    };
                case ObjectType.CAMERA:
                    return new List<FunctionType>()
                    {
                        FunctionType.CameraResource,
                    };
                default:
                    return null;
            }
        }

        private void ClearBindingSlots()
        {
            ApplyConnectionToSlot(_markerSlot, null);
            ApplyConnectionToSlot(_characterSlot, null);
            ApplyConnectionToSlot(_cameraSlot, null);
        }

        private void RefreshBindingSlots(CAGEAnimation.EventTrack track)
        {
            if (track == null)
            {
                ClearBindingSlots();
                return;
            }

            List<CAGEAnimation.Connection> matches = animEntity.connections
                .Where(c => c != null && c.target_track == track.shortGUID)
                .ToList();

            ApplyConnectionToSlot(_markerSlot, matches.FirstOrDefault(c => c.binding_type == ObjectType.MARKER));
            ApplyConnectionToSlot(_characterSlot, matches.FirstOrDefault(c => c.binding_type == ObjectType.CHARACTER));
            ApplyConnectionToSlot(_cameraSlot, matches.FirstOrDefault(c => c.binding_type == ObjectType.CAMERA));
        }

        private void ApplyConnectionToSlot(BindingSlotUi slot, CAGEAnimation.Connection conn)
        {
            if (slot == null) return;
            slot.Connection = conn;

            if (conn == null)
            {
                slot.EntityLabel.Text = "(none)";
                slot.EntityLabel.ForeColor = System.Drawing.SystemColors.GrayText;
                slot.JumpBtn.Enabled = false;
                slot.AssignBtn.Text = "Assign…";
                return;
            }

            Entity entity = ResolveConnectionEntity(conn);
            if (entity != null && _entityDisplay?.Composite != null)
                slot.EntityLabel.Text = Content.EditorUtils.GenerateEntityName(entity, _entityDisplay.Composite);
            else
                slot.EntityLabel.Text = DescribeConnectionEntity(conn);

            slot.EntityLabel.ForeColor = EntityNameColor;
            slot.JumpBtn.Enabled = entity != null && _entityDisplay?.CompositeDisplay != null;
            slot.AssignBtn.Text = "Reassign…";
        }

        private string DescribeConnectionEntity(CAGEAnimation.Connection conn)
        {
            if (conn == null || conn.connectedEntity == null)
                return "(none)";
            try
            {
                return Content.Level.Commands.Utils.GetResolvedAsString(
                    Content.Level.Commands.Utils.ResolveAlias(conn.connectedEntity, _entityDisplay.Composite),
                    SettingsManager.GetBool(Settings.ShowShortGuids));
            }
            catch
            {
                return conn.connectedEntity.ToString();
            }
        }

        private Entity ResolveConnectionEntity(CAGEAnimation.Connection conn)
        {
            if (conn == null || conn.connectedEntity == null || _entityDisplay?.Composite == null)
                return null;
            try
            {
                return Content.Level.Commands.Utils.GetResolvedTarget(
                    Content.Level.Commands.Utils.ResolveAlias(conn.connectedEntity, _entityDisplay.Composite)).Item2;
            }
            catch
            {
                return null;
            }
        }

        private void BindingSlot_JumpClicked(object sender, EventArgs e)
        {
            BindingSlotUi slot = (sender as Button)?.Tag as BindingSlotUi;
            if (slot == null) return;
            Entity entity = ResolveConnectionEntity(slot.Connection);
            if (entity == null || _entityDisplay?.CompositeDisplay == null)
                return;
            _entityDisplay.CompositeDisplay.LoadEntity(entity, true);
            this.BringToFront();
            this.Focus();
        }

        private void BindingSlot_AssignClicked(object sender, EventArgs e)
        {
            BindingSlotUi slot = (sender as Button)?.Tag as BindingSlotUi;
            if (slot == null || activeEventTrack == null) return;

            _pendingBindingAssignType = slot.BindingType;
            SelectHierarchy hierarchyEditor = new SelectHierarchy(_entityDisplay.Composite, new CompositeEntityList.DisplayOptions()
            {
                DisplayAliases = false,
                DisplayFunctions = true,
                DisplayProxies = false,
                DisplayVariables = false,
                AllowedFunctionTypes = FunctionTypesForBinding(slot.BindingType),
            });
            hierarchyEditor.Text = (slot.Connection == null ? "Assign " : "Reassign ") + slot.BindingType;
            hierarchyEditor.Show(this);
            hierarchyEditor.OnHierarchyGenerated += BindingSlot_HierarchyGenerated;
        }

        private void BindingSlot_HierarchyGenerated(ShortGuid[] generatedHierarchy)
        {
            if (activeEventTrack == null) return;

            CAGEAnimation.Connection conn = FindOrCreateBindingConnection(activeEventTrack, _pendingBindingAssignType);
            conn.connectedEntity = new EntityPath(generatedHierarchy);
            conn.binding_type = _pendingBindingAssignType;
            conn.target_track = activeEventTrack.shortGUID;

            CAGEAnimation.EventTrack keepTrack = activeEventTrack;
            RefreshEventTrackLists();
            SetupAnimTimeline();
            if (keepTrack != null && animCurveEditor != null)
                animCurveEditor.SelectEventTrack(keepTrack);
            this.BringToFront();
            this.Focus();
        }

        private CAGEAnimation.Connection FindOrCreateBindingConnection(CAGEAnimation.EventTrack track, ObjectType bindingType)
        {
            CAGEAnimation.Connection existing = animEntity.connections.FirstOrDefault(c =>
                c != null && c.target_track == track.shortGUID && c.binding_type == bindingType);
            if (existing != null) return existing;

            CAGEAnimation.Connection created = new CAGEAnimation.Connection();
            created.binding_guid = ShortGuidUtils.GenerateRandom();
            created.target_track = track.shortGUID;
            created.binding_type = bindingType;
            animEntity.connections.Add(created);
            return created;
        }

        private float _pendingEventAddTime;
        private CAGEAnimation.EventTrack _pendingEventAddTrack;

        private void AnimCurve_EventTrackDeleteRequested(CAGEAnimation.EventTrack track)
        {
            if (track == null) return;
            CAGEAnimation.EventTrack trackRef = track;
            BeginInvoke(new Action(() =>
            {
                int index = animEntity.eventTracks.IndexOf(trackRef);
                if (index < 0) return;

                string label = index < eventTracks.Count ? eventTracks[index] : "this event track";
                if (MessageBox.Show(
                        "Delete event track \"" + label + "\" and all of its keyframes?",
                        "Delete Event Track",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                DeleteEventTrackAt(index);
            }));
        }

        private void AnimCurve_EventTrackAddRequested(ANIM_TRACK_TYPE type)
        {
            BeginInvoke(new Action(() => CreateEventTrack(type)));
        }

        private void CreateEventTrack(ANIM_TRACK_TYPE type)
        {
            CAGEAnimation.EventTrack track = new CAGEAnimation.EventTrack();
            track.shortGUID = ShortGuidUtils.GenerateRandom();
            animEntity.eventTracks.Add(track);
            _eventTrackPreferredTypes[track.shortGUID] = type;

            RefreshEventTrackLists();
            SetupAnimTimeline();
            if (animCurveEditor != null)
                animCurveEditor.SelectEventTrack(track);
            this.BringToFront();
            this.Focus();
        }

        private void AnimCurve_EventAddRequested(float time, CAGEAnimation.EventTrack preferredTrack)
        {
            // Defer dialogs so ElementHost isn't replaced mid mouse-down on the WPF curve editor
            float t = time;
            CAGEAnimation.EventTrack trackRef = preferredTrack;
            BeginInvoke(new Action(() =>
            {
                CAGEAnimation.EventTrack track = trackRef ?? GetOrCreateSelfEventTrack();
                ANIM_TRACK_TYPE type = animCurveEditor != null
                    ? animCurveEditor.GetEventTrackType(track)
                    : ANIM_TRACK_TYPE.T_STRING;
                _pendingEventAddTime = t;
                _pendingEventAddTrack = track;

                if (type == ANIM_TRACK_TYPE.T_GUID)
                    PromptGuidEventKeyframe(t, track);
                else
                    PromptStringEventKeyframe(t, track);
            }));
        }

        private void PromptStringEventKeyframe(float time, CAGEAnimation.EventTrack track)
        {
            CAGEAnimation_AddStringEvent dialog = new CAGEAnimation_AddStringEvent(time);
            dialog.OnEventNameEntered += (name) =>
            {
                if (_pendingEventAddTrack == null) return;
                CAGEAnimation.EventTrack.Keyframe key = new CAGEAnimation.EventTrack.Keyframe(time, name);
                _pendingEventAddTrack.keyframes.Add(key);
                FinishPendingEventAdd(key);
            };
            dialog.FormClosed += (s, e) => ClearPendingEventAdd();
            dialog.Show(this);
        }

        private void PromptGuidEventKeyframe(float time, CAGEAnimation.EventTrack track)
        {
            SelectHierarchy hierarchyEditor = new SelectHierarchy(_entityDisplay.Composite, new CompositeEntityList.DisplayOptions()
            {
                DisplayAliases = false,
                DisplayFunctions = true,
                DisplayProxies = false,
                DisplayVariables = false,
                AllowedFunctionTypes = GuidKeyframeFunctionTypes,
            });
            hierarchyEditor.Text = "Select Animation Entity";
            hierarchyEditor.OnFinalEntitySelected += GuidEvent_EntitySelected;
            hierarchyEditor.FormClosed += (s, e) =>
            {
                // Cleared after a successful add; also clear on cancel
                if (_pendingEventAddTrack != null)
                    ClearPendingEventAdd();
            };
            hierarchyEditor.Show(this);
        }

        private bool IsAllowedGuidKeyframeEntity(FunctionEntity function)
        {
            if (function == null) return false;
            for (int i = 0; i < GuidKeyframeFunctionTypes.Count; i++)
            {
                if (function.function == GuidKeyframeFunctionTypes[i])
                    return true;
            }
            return false;
        }

        private void GuidEvent_EntitySelected(Entity entity)
        {
            if (_pendingEventAddTrack == null) return;
            FunctionEntity function = entity as FunctionEntity;
            if (!IsAllowedGuidKeyframeEntity(function))
            {
                MessageBox.Show("Select a CMD_PlayAnimation, CameraPlayAnimation, or PlayEnvironmentAnimation entity.", "Invalid entity", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            float time = _pendingEventAddTime;
            CAGEAnimation.EventTrack.Keyframe key = new CAGEAnimation.EventTrack.Keyframe(time, function);
            _pendingEventAddTrack.keyframes.Add(key);
            FinishPendingEventAdd(key);
        }

        private void reassignGuidEntityBtn_Click(object sender, EventArgs e)
        {
            if (activeGraphEventKeyframe == null || activeGraphEventKeyframe.track_type != ANIM_TRACK_TYPE.T_GUID)
                return;

            SelectHierarchy hierarchyEditor = new SelectHierarchy(_entityDisplay.Composite, new CompositeEntityList.DisplayOptions()
            {
                DisplayAliases = false,
                DisplayFunctions = true,
                DisplayProxies = false,
                DisplayVariables = false,
                AllowedFunctionTypes = GuidKeyframeFunctionTypes,
            });
            hierarchyEditor.Text = "Reassign Animation Entity";
            hierarchyEditor.OnFinalEntitySelected += ReassignGuidKeyframe_EntitySelected;
            hierarchyEditor.Show(this);
        }

        private void ReassignGuidKeyframe_EntitySelected(Entity entity)
        {
            if (activeGraphEventKeyframe == null || activeGraphEventKeyframe.track_type != ANIM_TRACK_TYPE.T_GUID)
                return;

            FunctionEntity function = entity as FunctionEntity;
            if (!IsAllowedGuidKeyframeEntity(function))
            {
                MessageBox.Show("Select a CMD_PlayAnimation, CameraPlayAnimation, or PlayEnvironmentAnimation entity.", "Invalid entity", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            activeGraphEventKeyframe.forward = function.shortGUID;
            activeGraphEventKeyframe.track_type = ANIM_TRACK_TYPE.T_GUID;
            ShowGuidEventPanel(activeGraphEventKeyframe);
            if (animCurveEditor != null)
            {
                // Rebuild labels on markers
                CAGEAnimation.EventTrack.Keyframe keep = activeGraphEventKeyframe;
                SetupAnimTimeline();
                if (animCurveEditor != null)
                    animCurveEditor.SelectEvent(keep);
            }
            this.BringToFront();
            this.Focus();
        }

        private void FinishPendingEventAdd(CAGEAnimation.EventTrack.Keyframe key)
        {
            CAGEAnimation.EventTrack track = _pendingEventAddTrack;
            ClearPendingEventAdd();
            RefreshEventTrackLists();

            // Prefer an incremental update — full SetupAnimTimeline can render before layout and produce NaNs
            if (animCurveEditor != null && track != null && key != null)
            {
                animCurveEditor.AddEvent(track, key, GetEventMarkerLabel(key));
                animCurveEditor.NotifyEventsChanged();
                animCurveEditor.SelectEvent(key);
            }
            else
            {
                SetupAnimTimeline();
                if (animCurveEditor != null && key != null)
                    animCurveEditor.SelectEvent(key);
            }

            this.BringToFront();
            this.Focus();
        }

        private void ClearPendingEventAdd()
        {
            _pendingEventAddTrack = null;
        }

        private void openGuidEntityBtn_Click(object sender, EventArgs e)
        {
            if (activeGuidEventEntity == null || _entityDisplay?.CompositeDisplay == null)
                return;

            // LoadEntity updates the inspector without switching composite; anim editor stays open
            // (CAGEAnimationEditor does not close on NEW_ENTITY_SELECTION).
            _entityDisplay.CompositeDisplay.LoadEntity(activeGuidEventEntity, true);
            this.BringToFront();
            this.Focus();
        }

        private CAGEAnimation.EventTrack GetOrCreateSelfEventTrack()
        {
            // Prefer an event track with no connection (events fired on the CAGEAnimation itself)
            for (int i = 0; i < animEntity.eventTracks.Count; i++)
            {
                if (!animEntity.connections.Any(c => c.target_track == animEntity.eventTracks[i].shortGUID))
                    return animEntity.eventTracks[i];
            }

            CAGEAnimation.EventTrack track = new CAGEAnimation.EventTrack();
            track.shortGUID = ShortGuidUtils.GenerateRandom();
            animEntity.eventTracks.Add(track);
            return track;
        }

        private void deleteGraphEvent_Click(object sender, EventArgs e)
        {
            if (animCurveEditor == null) return;
            animCurveEditor.RemoveSelectedEvent();
            activeGraphEventKeyframe = null;
            activeGuidEventEntity = null;
            graphEventData.Visible = false;
            RefreshEventTrackLists();
        }
        private void graphEventParam1_TextChanged(object sender, EventArgs e)
        {
            if (_suppressKeyframeEvents || activeGraphEventKeyframe == null) return;
            if (activeGraphEventKeyframe.track_type == ANIM_TRACK_TYPE.T_GUID) return;

            string eventName = graphEventParam1.Text ?? "";
            // Match EventTrack.Keyframe(string) ctor: reverse_ + name
            activeGraphEventKeyframe.forward = ShortGuidUtils.Generate(eventName);
            activeGraphEventKeyframe.reverse = ShortGuidUtils.Generate("reverse_" + eventName);
            activeGraphEventKeyframe.track_type = ANIM_TRACK_TYPE.T_STRING;
            if (animCurveEditor != null) animCurveEditor.RefreshSelectedKeyframeVisual();
        }

        private void deleteAnimKeyframe_Click(object sender, EventArgs e)
        {
            if (animCurveEditor == null) return;
            animCurveEditor.RemoveSelectedKeyframe();
            activeAnimKeyframe = null;
            animKeyframeData.Visible = false;
        }

        private void BeginAddEntityLink()
        {
            SelectHierarchy hierarchyEditor = new SelectHierarchy(_entityDisplay.Composite, new CompositeEntityList.DisplayOptions()
            {
                DisplayAliases = false,
                DisplayFunctions = true,
                DisplayProxies = true,
                DisplayVariables = true,
            });
            hierarchyEditor.Show(this);
            hierarchyEditor.OnHierarchyGenerated += AddEntityLink_HierarchyGenerated;
        }

        private void AddEntityLink_HierarchyGenerated(ShortGuid[] generatedHierarchy)
        {
            EntityPath hierarchy = new EntityPath(generatedHierarchy);
            _pendingEntityLinkPath = hierarchy;

            // If this entity is already linked, select it in the tree for context
            for (int i = 0; i < entityListToHierarchies.Count; i++)
            {
                if (entityListToHierarchies[i] == hierarchy)
                {
                    SelectEntityInTree(hierarchy);
                    break;
                }
            }

            this.BringToFront();
            this.Focus();
            PromptParameterForPendingEntity();
        }

        private void PromptParameterForPendingEntity()
        {
            if (_pendingEntityLinkPath == null) return;
            try
            {
                Entity target = Content.Level.Commands.Utils.GetResolvedTarget(
                    Content.Level.Commands.Utils.ResolveAlias(_pendingEntityLinkPath, _entityDisplay.Composite)).Item2;
                CAGEAnimation_SelectParameter paramSelector = new CAGEAnimation_SelectParameter(target);
                paramSelector.OnParamSelected += OnParameterSelected;
                paramSelector.FormClosed += (s, ev) =>
                {
                    // Cleared after a successful link; also clear on cancel
                    if (_pendingEntityLinkPath != null)
                        _pendingEntityLinkPath = null;
                };
                paramSelector.Show(this);
            }
            catch
            {
                _pendingEntityLinkPath = null;
            }
        }

        private void OnParameterSelected(Parameter param)
        {
            EntityPath activePath = _pendingEntityLinkPath ?? GetActiveEntityPath();
            _pendingEntityLinkPath = null;
            if (activePath == null) return;

            //Make sure the same parameter isn't being added twice for the same entity
            if (animEntity.connections.FindAll(o => o.connectedEntity == activePath && o.target_param == param.name).Count != 0)
            {
                MessageBox.Show("This parameter is already controlled by the CAGEAnimation!", "Parameter already selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.BringToFront();
                this.Focus();
                return;
            }

            CAGEAnimation.Connection connection = new CAGEAnimation.Connection();
            connection.connectedEntity.path = activePath.path;
            connection.binding_type = ObjectType.ENTITY;
            connection.target_param_type = param.content.dataType;

            switch (param.content.dataType)
            {
                case DataType.FLOAT:
                    AddNewConnectionSet(connection.Copy(), ((cFloat)param.content).value, param.name);
                    break;
                case DataType.TRANSFORM:
                    string[] transformSubProperties = new string[6] { "x", "y", "z", "Yaw", "Pitch", "Roll" };
                    foreach (string subProp in transformSubProperties)
                    {
                        float val = 0;
                        switch (subProp)
                        {
                            case "x":
                                val = ((cTransform)param.content).position.X;
                                break;
                            case "y":
                                val = ((cTransform)param.content).position.Y;
                                break;
                            case "z":
                                val = ((cTransform)param.content).position.Z;
                                break;
                            case "Yaw":
                                val = ((cTransform)param.content).rotation.Y;
                                break;
                            case "Pitch":
                                val = ((cTransform)param.content).rotation.X;
                                break;
                            case "Roll":
                                val = ((cTransform)param.content).rotation.Z;
                                break;
                        }
                        AddNewConnectionSet(connection.Copy(), val, param.name, subProp);
                    }
                    break;
                //TODO: even though the base game doesn't use other datatypes in anims, we probably can!
                default:
                    MessageBox.Show("The datatype of the parameter you selected is not currently supported - please select either a FLOAT or TRANSFORM parameter to animate.", "Unsupported datatype", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
            }

            RebuildTrackTree(activePath);
            SetupAnimTimeline();
            this.BringToFront();
            this.Focus();
        }
        private void AddNewConnectionSet(CAGEAnimation.Connection conn, float defaultKeyValue, ShortGuid paramID, string subProp = "")
        {
            CAGEAnimation.FloatTrack.Keyframe keyStart = new CAGEAnimation.FloatTrack.Keyframe();
            keyStart.time = 0.0f;
            keyStart.value.Y = defaultKeyValue;
            keyStart.mode = CurrentInterpolationMode;
            keyStart.tan_in = new System.Numerics.Vector2(1f, 0f);
            keyStart.tan_out = new System.Numerics.Vector2(1f, 0f);
            CAGEAnimation.FloatTrack.Keyframe keyEnd = new CAGEAnimation.FloatTrack.Keyframe();
            keyEnd.time = anim_length;
            keyEnd.value.Y = defaultKeyValue;
            keyEnd.mode = CurrentInterpolationMode;
            keyEnd.tan_in = new System.Numerics.Vector2(1f, 0f);
            keyEnd.tan_out = new System.Numerics.Vector2(1f, 0f);
            CAGEAnimation.FloatTrack anim = new CAGEAnimation.FloatTrack();
            anim.shortGUID = ShortGuidUtils.GenerateRandom();
            anim.keyframes.Add(keyStart);
            anim.keyframes.Add(keyEnd);
            animEntity.floatTracks.Add(anim);
            conn.binding_guid = ShortGuidUtils.GenerateRandom();
            conn.target_param = paramID;
            conn.target_sub_param = ShortGuidUtils.Generate(subProp);
            conn.target_track = anim.shortGUID;
            animEntity.connections.Add(conn);
            _hiddenTrackIds.Remove(anim.shortGUID);
        }

        private void DeleteEventTrackAt(int index)
        {
            if (index < 0 || index >= animEntity.eventTracks.Count) return;

            ShortGuid trackId = animEntity.eventTracks[index].shortGUID;
            _eventTrackPreferredTypes.Remove(trackId);
            List<CAGEAnimation.Connection> trimmedConnections = new List<CAGEAnimation.Connection>();
            for (int i = 0; i < animEntity.connections.Count; i++)
            {
                if (animEntity.connections[i].target_track == trackId) continue;
                trimmedConnections.Add(animEntity.connections[i]);
            }
            animEntity.connections = trimmedConnections;
            animEntity.eventTracks.RemoveAt(index);
            activeEventTrack = null;
            activeGraphEventKeyframe = null;
            if (eventTrackPanel != null) eventTrackPanel.Visible = false;
            graphEventData.Visible = false;
            RefreshEventTrackLists();
            SetupAnimTimeline();
        }

        private void SaveEntity_Click(object sender, EventArgs e)
        {
            if (!ConfirmSaveWithRemovedStringEventPins())
                return;

            animEntity.AddParameter("anim_length", new cFloat(anim_length));
            OnSaved?.Invoke(animEntity);
            this.Close();
        }

        /// <summary>
        /// Warn if T_STRING event pins that still have flowgraph connections would disappear after save.
        /// </summary>
        private bool ConfirmSaveWithRemovedStringEventPins()
        {
            CAGEAnimation original = _entityDisplay?.Entity as CAGEAnimation;
            Composite composite = _entityDisplay?.Composite;
            if (original == null || composite == null)
                return true;

            HashSet<ShortGuid> oldPins = CollectStringEventPins(original);
            HashSet<ShortGuid> newPins = CollectStringEventPins(animEntity);
            List<string> connectedRemoved = new List<string>();

            foreach (ShortGuid pin in oldPins)
            {
                if (newPins.Contains(pin)) continue;
                if (!IsStringEventPinConnected(original, composite, pin)) continue;

                string name = ShortGuidUtils.FindString(pin);
                if (string.IsNullOrEmpty(name))
                    name = pin.ToByteString();
                connectedRemoved.Add(name);
            }

            if (connectedRemoved.Count == 0)
                return true;

            connectedRemoved.Sort(StringComparer.OrdinalIgnoreCase);
            string message =
                "These string event pins still have connections in the flowgraph, but will no longer exist after save:\n\n"
                + string.Join("\n", connectedRemoved)
                + "\n\nSaving will break those links. Continue anyway?";

            return MessageBox.Show(
                message,
                "Connected event pins will be removed",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private static HashSet<ShortGuid> CollectStringEventPins(CAGEAnimation anim)
        {
            HashSet<ShortGuid> pins = new HashSet<ShortGuid>();
            if (anim?.eventTracks == null) return pins;
            for (int t = 0; t < anim.eventTracks.Count; t++)
            {
                CAGEAnimation.EventTrack track = anim.eventTracks[t];
                if (track?.keyframes == null) continue;
                for (int k = 0; k < track.keyframes.Count; k++)
                {
                    CAGEAnimation.EventTrack.Keyframe key = track.keyframes[k];
                    if (key.track_type != ANIM_TRACK_TYPE.T_STRING) continue;
                    pins.Add(key.forward);
                    pins.Add(key.reverse);
                }
            }
            return pins;
        }

        private bool IsStringEventPinConnected(Entity entity, Composite composite, ShortGuid pinId)
        {
            if (entity == null) return false;

            if (entity.GetLinksOut(pinId).Count > 0)
                return true;
            if (composite != null && entity.GetLinksIn(pinId, composite).Count > 0)
                return true;

            // Also check live flowgraph UI (may have connections not yet compiled back to childLinks)
            List<Flowgraph> flowgraphs = _entityDisplay?.CompositeDisplay?.Flowgraphs;
            if (flowgraphs == null) return false;
            for (int i = 0; i < flowgraphs.Count; i++)
            {
                Flowgraph fg = flowgraphs[i];
                if (fg != null && fg.HasPinConnections(entity, pinId))
                    return true;
            }
            return false;
        }

        private void animKeyframeValue_TextChanged(object sender, EventArgs e)
        {
            if (_suppressKeyframeEvents) return;
            animKeyframeValue.Text = EditorUtils.ForceStringNumeric(animKeyframeValue.Text, true);
            if (activeAnimKeyframe == null) return;
            activeAnimKeyframe.value.Y = Convert.ToSingle(animKeyframeValue.Text);
            if (animCurveEditor != null) animCurveEditor.RefreshSelectedKeyframeVisual();
        }
    }
}
