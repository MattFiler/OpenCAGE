using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Enums;
using CathodeLib.ObjectExtensions;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using Newtonsoft.Json;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static CATHODE.Scripting.CAGEAnimation;

namespace OpenCAGE
{
    //NOTE: There's LOTS that needs changing here, for one, track types are not handled correctly leading to garbage data.
    //      I think the STRING track_type on keyframes is for events, and GUID is for CMD_PlayAnimation and PlayEnvironmentAnimation links. Should show that better.
    //      I should also auto add new pins to any nodes that exist when events are created here.

    public partial class CAGEAnimationEditor : BaseWindow
    {
        public Action<CAGEAnimation> OnSaved;

        float anim_length = 0;
        CAGEAnimation animEntity = null; //this is a unique instance we can write to

        List<EntityPath> entityListToHierarchies = new List<EntityPath>();
        List<string> eventTracks = new List<string>();
        List<Entity> eventEntityIDs = new List<Entity>();

        CurveEditor animCurveEditor;
        bool _suppressKeyframeEvents = false;
        CAGEAnimation.EventTrack.Keyframe activeGraphEventKeyframe = null;
        Entity activeGuidEventEntity = null;
        CAGEAnimation.FloatTrack.Keyframe activeAnimKeyframe = null;
        /// <summary>Track GUIDs the user has unchecked — new tracks default to visible.</summary>
        readonly HashSet<ShortGuid> _hiddenTrackIds = new HashSet<ShortGuid>();
        ImageList _trackTreeStateImages;
        List<CAGEAnimation.FloatTrack> _deletableAnimTracks = new List<CAGEAnimation.FloatTrack>();

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

            // okay so this is getting there. but a few things:
            // - I should show the event tracks on separate lines, rather than all on the one graph.
            // - event tracks can only ever be ONE TYPE (e.g. T_GUID or T_STRING, i need to enforce that)
            // - can event tracks of T_GUID type have more than one keyframe? or do they only ever trigger one anim?
            // - when clicking on an event track of T_GUID type, i need to show the associated connections (marker/character/etc)
            // - i also need to allow reassigning and selecting of the connected entities (from last bullet point)

            animEntity = ((CAGEAnimation)_entityDisplay.Entity).Copy();
            //File.WriteAllText("out.json", JsonConvert.SerializeObject(animEntity, Formatting.Indented));
            InitializeComponent();

            SetupSnapControls();
            SetupTrackTree();
            InitBezierModeFromData();

            anim_length = CalculateAnimLength();
            Parameter anim_length_param = animEntity.GetParameter("anim_length");
            if (anim_length_param != null && anim_length_param.content != null)
            {
                float animLengthParam = ((cFloat)anim_length_param.content).value;
                if (animLengthParam > anim_length) anim_length = animLengthParam;
            }

            //TODO: if we don't already have an event track that is not in connections (e.g. triggered on us) we should make one

            RebuildTrackTree();

            RefreshEventTrackLists();
            SetupAnimTimeline();

            this.Resize += CAGEAnimationEditor_Resize;
            LayoutEditorControls();

            this.BringToFront();
            this.Focus();
        }

        private const int SIDE_PANEL_WIDTH = 198;
        private const int LAYOUT_MARGIN = 7;
        private const int FOOTER_HEIGHT = 48;

        private void CAGEAnimationEditor_Resize(object sender, EventArgs e)
        {
            LayoutEditorControls();
            SyncHostedEditorSizes();
        }

        private void LayoutEditorControls()
        {
            int availW = Math.Max(400, ClientSize.Width - LAYOUT_MARGIN * 2);
            int availH = Math.Max(280, ClientSize.Height - LAYOUT_MARGIN - FOOTER_HEIGHT);

            int sideW = SIDE_PANEL_WIDTH;
            int contentW = Math.Max(200, availW - sideW - 16);
            int treeW = Math.Min(TRACK_TREE_WIDTH, Math.Max(140, contentW / 4));
            int hostW = Math.Max(160, contentW - treeW - 6);
            int animHostH = Math.Max(80, availH - 28);
            int originX = LAYOUT_MARGIN;
            int originY = LAYOUT_MARGIN;
            int sideX = originX + availW - sideW - 10;

            trackTree.SetBounds(originX, originY, treeW, animHostH);
            animHost.SetBounds(originX + treeW + 6, originY, hostW, animHostH);
            addNewEntityRef.SetBounds(sideX, originY, sideW, 23);
            bezierMode.SetBounds(sideX, originY + 29, sideW, 17);
            animKeyframeData.SetBounds(sideX, originY + 55, sideW, 100);
            graphEventData.SetBounds(sideX, originY + 55, sideW, 200);
            addAnimationTrack.SetBounds(sideX, originY + availH - 108, sideW, 23);
            deleteAnimationTrack.SetBounds(sideX, originY + availH - 83, sideW, 23);
            addEventTrack.SetBounds(sideX, originY + availH - 58, sideW, 23);
            deleteEventTrack.SetBounds(sideX, originY + availH - 33, sideW, 23);

            snapToSeconds.Location = new System.Drawing.Point(7, ClientSize.Height - 27);
            labelSnap.Location = new System.Drawing.Point(121, ClientSize.Height - 43);
            snapInterval.Location = new System.Drawing.Point(124, ClientSize.Height - 29);
            SaveEntity.SetBounds(ClientSize.Width - 172, ClientSize.Height - 42, 167, 36);
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
            eventEntityIDs.Clear();
            for (int i = 0; i < animEntity.eventTracks.Count; i++)
            {
                CAGEAnimation.Connection connection = animEntity.connections.FirstOrDefault(o => o.target_track == animEntity.eventTracks[i].shortGUID);
                string label = (connection == null)
                    ? Content.Level.Commands.Utils.GetEntityName(_entityDisplay.Composite, animEntity)
                    : Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity, _entityDisplay.Composite), SettingsManager.GetBool(Settings.ShowShortGuids));
                eventTracks.Add(label);
                eventEntityIDs.Add(connection == null
                    ? animEntity
                    : Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity, _entityDisplay.Composite)).Item2);
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
        }

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

        List<string> animTracks = new List<string>();
        private void SetupAnimTimeline()
        {
            activeAnimKeyframe = null;
            activeGraphEventKeyframe = null;
            animCurveEditor = null;
            animHost.Child = null;
            animKeyframeData.Visible = false;
            graphEventData.Visible = false;
            animTracks.Clear();
            _deletableAnimTracks.Clear();

            CurveEditor editor = new CurveEditor(animHost.Width, animHost.Height);
            editor.Setup(anim_length);

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
                    animTracks.Add(curveName);
                    _deletableAnimTracks.Add(tag.Track);
                }
            }

            // Overlay all event markers (yellow = T_STRING, blue = T_GUID entity refs)
            for (int i = 0; i < animEntity.eventTracks.Count; i++)
            {
                CAGEAnimation.Connection connection = animEntity.connections.FirstOrDefault(o => o.target_track == animEntity.eventTracks[i].shortGUID);
                string trackLabel = (connection == null)
                    ? Content.Level.Commands.Utils.GetEntityName(_entityDisplay.Composite, animEntity)
                    : Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity, _entityDisplay.Composite), SettingsManager.GetBool(Settings.ShowShortGuids));
                for (int x = 0; x < animEntity.eventTracks[i].keyframes.Count; x++)
                {
                    CAGEAnimation.EventTrack.Keyframe key = animEntity.eventTracks[i].keyframes[x];
                    string markerLabel = GetEventMarkerLabel(key, trackLabel);
                    editor.AddEvent(animEntity.eventTracks[i], key, markerLabel);
                }
            }

            editor.KeyframeSelected += AnimCurve_KeyframeSelected;
            editor.SelectionCleared += AnimCurve_SelectionCleared;
            editor.EventSelected += AnimCurve_EventSelected;
            editor.EventSelectionCleared += AnimCurve_EventSelectionCleared;
            editor.EventAddRequested += AnimCurve_EventAddRequested;
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
            animKeyframeData.Visible = true;
            graphEventData.Visible = false;

            _suppressKeyframeEvents = true;
            animKeyframeValue.Text = kf.value.Y.ToString();
            _suppressKeyframeEvents = false;
        }
        private void AnimCurve_SelectionCleared()
        {
            activeAnimKeyframe = null;
            animKeyframeData.Visible = false;
        }
        private string GetEventMarkerLabel(CAGEAnimation.EventTrack.Keyframe key, string trackFallback)
        {
            if (key == null) return trackFallback ?? "event";

            if (key.track_type == ANIM_TRACK_TYPE.T_GUID)
            {
                Entity ent = ResolveGuidEventEntity(key.forward);
                if (ent != null && _entityDisplay?.Composite != null)
                    return Content.Level.Commands.Utils.GetEntityName(_entityDisplay.Composite, ent);
                return key.forward.ToByteString();
            }

            string name = key.forward.ToString();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
            return string.IsNullOrEmpty(trackFallback) ? "event" : trackFallback;
        }

        private Entity ResolveGuidEventEntity(ShortGuid id)
        {
            if (_entityDisplay?.Composite == null) return null;
            return _entityDisplay.Composite.GetEntityByID(id);
        }

        private string DescribeEntity(Entity entity)
        {
            if (entity == null) return "Entity not found in this composite.";

            switch (entity.variant)
            {
                case EntityVariant.FUNCTION:
                    Composite pointed = Content?.Level?.Commands?.GetComposite(((FunctionEntity)entity).function);
                    if (pointed != null)
                        return "Composite instance\r\n" + pointed.name + "\r\nID: " + entity.shortGUID.ToByteString();
                    return "Function: " + ((FunctionEntity)entity).function.AsFunctionType + "\r\nID: " + entity.shortGUID.ToByteString();
                case EntityVariant.VARIABLE:
                    return "Variable: " + ShortGuidUtils.FindString(((VariableEntity)entity).name) + "\r\nID: " + entity.shortGUID.ToByteString();
                case EntityVariant.PROXY:
                    return "Proxy\r\nID: " + entity.shortGUID.ToByteString();
                case EntityVariant.ALIAS:
                    return "Alias\r\nID: " + entity.shortGUID.ToByteString();
                default:
                    return entity.variant + "\r\nID: " + entity.shortGUID.ToByteString();
            }
        }

        private void ShowStringEventPanel(CAGEAnimation.EventTrack.Keyframe kf)
        {
            label13.Text = "Event to trigger:";
            label12.Visible = true;
            graphEventParam1.Visible = true;
            graphEventParam2.Visible = true;
            graphGuidEntityName.Visible = false;
            graphGuidEntityInfo.Visible = false;
            openGuidEntityBtn.Visible = false;
            deleteGraphEvent.Location = new System.Drawing.Point(6, 144);
            graphEventData.Text = "Selected Event";

            _suppressKeyframeEvents = true;
            graphEventParam1.Text = kf.forward.ToString();
            graphEventParam2.Text = kf.reverse.ToString();
            _suppressKeyframeEvents = false;
        }

        private void ShowGuidEventPanel(CAGEAnimation.EventTrack.Keyframe kf)
        {
            label13.Text = "Linked entity:";
            label12.Visible = false;
            graphEventParam1.Visible = false;
            graphEventParam2.Visible = false;
            graphGuidEntityName.Visible = true;
            graphGuidEntityInfo.Visible = true;
            openGuidEntityBtn.Visible = true;
            deleteGraphEvent.Location = new System.Drawing.Point(6, 168);
            graphEventData.Text = "Selected Entity Link";

            activeGuidEventEntity = ResolveGuidEventEntity(kf.forward);
            if (activeGuidEventEntity != null && _entityDisplay?.Composite != null)
                graphGuidEntityName.Text = Content.Level.Commands.Utils.GetEntityName(_entityDisplay.Composite, activeGuidEventEntity);
            else
                graphGuidEntityName.Text = kf.forward.ToByteString();

            graphGuidEntityInfo.Text = DescribeEntity(activeGuidEventEntity);
            openGuidEntityBtn.Enabled = activeGuidEventEntity != null;
        }

        private void AnimCurve_EventSelected(CAGEAnimation.EventTrack.Keyframe kf)
        {
            activeGraphEventKeyframe = kf;
            activeAnimKeyframe = null;
            graphEventData.Visible = true;
            animKeyframeData.Visible = false;

            if (kf.track_type == ANIM_TRACK_TYPE.T_GUID)
                ShowGuidEventPanel(kf);
            else
                ShowStringEventPanel(kf);
        }
        private void AnimCurve_EventSelectionCleared()
        {
            activeGraphEventKeyframe = null;
            activeGuidEventEntity = null;
            graphEventData.Visible = false;
        }
        private void AnimCurve_EventAddRequested(float time)
        {
            // Defer rebuild so ElementHost isn't replaced mid mouse-down on the WPF curve editor
            float t = time;
            BeginInvoke(new Action(() =>
            {
                CAGEAnimation.EventTrack track = GetOrCreateSelfEventTrack();
                CAGEAnimation.EventTrack.Keyframe key = new CAGEAnimation.EventTrack.Keyframe();
                key.time = t;
                key.forward = ShortGuidUtils.Generate("");
                key.reverse = ShortGuidUtils.Generate("");
                key.track_type = ANIM_TRACK_TYPE.T_STRING;
                track.keyframes.Add(key);

                RefreshEventTrackLists();
                SetupAnimTimeline();
                if (animCurveEditor != null)
                    animCurveEditor.SelectEvent(key);
            }));
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
            if (activeGraphEventKeyframe.forward.ToByteString() == graphEventParam1.Text) return;
            activeGraphEventKeyframe.forward = ShortGuidUtils.Generate(graphEventParam1.Text);
            activeGraphEventKeyframe.track_type = ANIM_TRACK_TYPE.T_STRING;
            if (animCurveEditor != null) animCurveEditor.RefreshSelectedKeyframeVisual();
        }
        private void graphEventParam2_TextChanged(object sender, EventArgs e)
        {
            if (_suppressKeyframeEvents || activeGraphEventKeyframe == null) return;
            if (activeGraphEventKeyframe.track_type == ANIM_TRACK_TYPE.T_GUID) return;
            if (activeGraphEventKeyframe.reverse.ToByteString() == graphEventParam2.Text) return;
            activeGraphEventKeyframe.reverse = ShortGuidUtils.Generate(graphEventParam2.Text);
            activeGraphEventKeyframe.track_type = ANIM_TRACK_TYPE.T_STRING;
        }

        private void deleteAnimKeyframe_Click(object sender, EventArgs e)
        {
            if (animCurveEditor == null) return;
            animCurveEditor.RemoveSelectedKeyframe();
            activeAnimKeyframe = null;
            animKeyframeData.Visible = false;
        }

        private void addNewEntityRef_Click(object sender, EventArgs e)
        {
            SelectHierarchy hierarchyEditor = new SelectHierarchy(_entityDisplay.Composite, new CompositeEntityList.DisplayOptions()
            {
                DisplayAliases = false,
                DisplayFunctions = true,
                DisplayProxies = true,
                DisplayVariables = true,
            });
            hierarchyEditor.Show();
            hierarchyEditor.OnHierarchyGenerated += HierarchyEditor_HierarchyGenerated;
        }
        private void HierarchyEditor_HierarchyGenerated(ShortGuid[] generatedHierarchy)
        {
            EntityPath hierarchy = new EntityPath(generatedHierarchy);

            //Prevent the same entity being added again (doesn't make sense as the list is unique)
            for (int i = 0; i < entityListToHierarchies.Count; i++)
            {
                if (entityListToHierarchies[i] == hierarchy)
                {
                    SelectEntityInTree(hierarchy);
                    this.BringToFront();
                    this.Focus();
                    return;
                }
            }

            //Creating a placeholder here that points to nothing so that the tree will pick it up - not ideal, but shouldn't affect anything
            CAGEAnimation.Connection newConnection = new CAGEAnimation.Connection();
            newConnection.connectedEntity.path = generatedHierarchy;
            newConnection.binding_type = ObjectType.ENTITY;
            animEntity.connections.Add(newConnection);

            RebuildTrackTree(hierarchy);
            SelectEntityInTree(hierarchy);
            this.BringToFront();
            this.Focus();
        }

        private void addAnimationTrack_Click(object sender, EventArgs e)
        {
            EntityPath activePath = GetActiveEntityPath();
            if (activePath == null)
            {
                MessageBox.Show("Select an entity in the track list first.", "No entity selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                CAGEAnimation_SelectParameter paramSelector = new CAGEAnimation_SelectParameter(Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(activePath, _entityDisplay.Composite)).Item2);
                paramSelector.OnParamSelected += OnParameterSelected;
                paramSelector.Show();
            }
            catch { }
        }
        private void OnParameterSelected(Parameter param)
        {
            EntityPath activePath = GetActiveEntityPath();
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

        private void deleteAnimationTrack_Click(object sender, EventArgs e)
        {
            try
            {
                CAGEAnimation_DeleteParam deleteParamWindow = new CAGEAnimation_DeleteParam(animTracks);
                deleteParamWindow.OnParamSelected += OnDeleteParamSelected;
                deleteParamWindow.Show();
            }
            catch { }
        }
        private void OnDeleteParamSelected(int index)
        {
            if (index < 0 || index >= _deletableAnimTracks.Count) return;
            CAGEAnimation.FloatTrack track = _deletableAnimTracks[index];
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

        private void addEventTrack_Click(object sender, EventArgs e)
        {
            SelectHierarchy hierarchyEditor = new SelectHierarchy(_entityDisplay.Composite, new CompositeEntityList.DisplayOptions()
            {
                DisplayAliases = false,
                DisplayFunctions = true,
                DisplayProxies = true,
                DisplayVariables = true,
            });
            hierarchyEditor.Show();
            hierarchyEditor.OnHierarchyGenerated += HierarchyEditor2_HierarchyGenerated;
        }
        private void HierarchyEditor2_HierarchyGenerated(ShortGuid[] generatedHierarchy)
        {
            EntityPath hierarchy = new EntityPath(generatedHierarchy);

            if (eventEntityIDs.Contains(Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(hierarchy, _entityDisplay.Composite)).Item2))
            {
                MessageBox.Show("This entity already has an event track added!", "Already added", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            //Add new connection
            ShortGuid connectionID;
            if (Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(hierarchy, _entityDisplay.Composite)).Item2 == animEntity)
            {
                connectionID = ShortGuidUtils.GenerateRandom();
            }
            else
            {
                CAGEAnimation.Connection connection = new CAGEAnimation.Connection();
                connection.binding_guid = ShortGuidUtils.GenerateRandom();
                connection.target_track = ShortGuidUtils.GenerateRandom();
                connection.binding_type = ObjectType.CHARACTER; //TODO: not only do we need to calculate this, we also need the pairs for CHARACTER/MARKER
                connection.connectedEntity = hierarchy;
                animEntity.connections.Add(connection);
                connectionID = connection.target_track;
            }

            //Add new event trigger
            CAGEAnimation.EventTrack eventTrigger = new CAGEAnimation.EventTrack();
            eventTrigger.shortGUID = connectionID;
            eventTrigger.keyframes.Add(new CAGEAnimation.EventTrack.Keyframe()
            {
                time = CalculateAnimLength(),
                forward = ShortGuidUtils.Generate(""),
                reverse = ShortGuidUtils.Generate(""),
                track_type = ANIM_TRACK_TYPE.T_STRING
            }); 
            animEntity.eventTracks.Add(eventTrigger);

            RefreshEventTrackLists();
            SetupAnimTimeline();
            this.BringToFront();
            this.Focus();
        }

        private void deleteEventTrack_Click(object sender, EventArgs e)
        {
            try
            {
                RefreshEventTrackLists();
                CAGEAnimation_DeleteEvent deleteEventWindow = new CAGEAnimation_DeleteEvent(eventTracks);
                deleteEventWindow.OnTrackSelected += OnDeleteEventSelected;
                deleteEventWindow.Show();
            }
            catch { }
        }
        private void OnDeleteEventSelected(int index)
        {
            List<CAGEAnimation.Connection> trimmedConnections = new List<CAGEAnimation.Connection>();
            for (int i = 0; i < animEntity.connections.Count; i++)
            {
                if (animEntity.connections[i].target_track == animEntity.eventTracks[index].shortGUID) continue;
                trimmedConnections.Add(animEntity.connections[i]);
            }
            animEntity.connections = trimmedConnections;
            animEntity.eventTracks.RemoveAt(index);
            RefreshEventTrackLists();
            SetupAnimTimeline();
        }

        private void SaveEntity_Click(object sender, EventArgs e)
        {
            animEntity.AddParameter("anim_length", new cFloat(anim_length));
            OnSaved?.Invoke(animEntity);
            this.Close();
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
