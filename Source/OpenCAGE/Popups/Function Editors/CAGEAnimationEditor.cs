using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib.ObjectExtensions;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using Newtonsoft.Json;
using OpenCAGE;
using System;
using System.Collections.Generic;
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

        List<EntityPath> entityListToHierarchies;
        List<string> entityListDetails = new List<string>();
        List<string> eventTracks = new List<string>();
        List<Entity> eventEntityIDs = new List<Entity>();

        CurveEditor animCurveEditor;
        bool _suppressKeyframeEvents = false;
        CAGEAnimation.EventTrack.Keyframe activeGraphEventKeyframe = null;
        CAGEAnimation.FloatTrack.Keyframe activeAnimKeyframe = null;

        EntityInspector _entityDisplay;

        public CAGEAnimationEditor(EntityInspector entityDisplay) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _entityDisplay = entityDisplay;

            animEntity = ((CAGEAnimation)_entityDisplay.Entity).Copy();
            //File.WriteAllText("out.json", JsonConvert.SerializeObject(animEntity, Formatting.Indented));
            InitializeComponent();

            SetupKeyframeModeDropdown();
            SetupEntityListDisplay();

            anim_length = CalculateAnimLength();
            Parameter anim_length_param = animEntity.GetParameter("anim_length");
            if (anim_length_param != null && anim_length_param.content != null)
            {
                float animLengthParam = ((cFloat)anim_length_param.content).value;
                if (animLengthParam > anim_length) anim_length = animLengthParam;
            }
            animLength.Text = anim_length.ToString();

            //TODO: if we don't already have an event track that is not in connections (e.g. triggered on us) we should make one

            UpdateEntityList();

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

            groupBox1.SetBounds(LAYOUT_MARGIN, LAYOUT_MARGIN, availW, availH);

            int sideW = SIDE_PANEL_WIDTH;
            int hostW = Math.Max(200, availW - sideW - 16);
            // Room for: entity link, keyframe/event panel (~176), then 4 track buttons
            int animHostH = Math.Max(80, availH - 58);
            int sideX = availW - sideW - 10;

            entityList.SetBounds(6, 19, hostW, 21);
            animHost.SetBounds(6, 46, hostW, animHostH);
            addNewEntityRef.SetBounds(sideX, 17, sideW, 23);
            animKeyframeData.SetBounds(sideX, 47, sideW, 176);
            graphEventData.SetBounds(sideX, 47, sideW, 176);
            addAnimationTrack.SetBounds(sideX, availH - 108, sideW, 23);
            deleteAnimationTrack.SetBounds(sideX, availH - 83, sideW, 23);
            addEventTrack.SetBounds(sideX, availH - 58, sideW, 23);
            deleteEventTrack.SetBounds(sideX, availH - 33, sideW, 23);

            label10.Location = new System.Drawing.Point(4, ClientSize.Height - 43);
            animLength.Location = new System.Drawing.Point(7, ClientSize.Height - 29);
            editAnimLength.Location = new System.Drawing.Point(197, ClientSize.Height - 30);
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
            for (int i = 0; i < animEntity.events.Count; i++)
            {
                CAGEAnimation.Connection connection = animEntity.connections.FirstOrDefault(o => o.target_track == animEntity.events[i].shortGUID);
                string label = (connection == null)
                    ? Content.Level.Commands.Utils.GetEntityName(_entityDisplay.Composite, animEntity)
                    : Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity, _entityDisplay.Composite), SettingsManager.GetBool(Settings.ShowShortGuids));
                eventTracks.Add(label);
                eventEntityIDs.Add(connection == null
                    ? animEntity
                    : Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity, _entityDisplay.Composite)).Item2);
            }
        }

        private void SetupKeyframeModeDropdown()
        {
            animKeyframeMode.Items.Clear();
            animKeyframeMode.Items.Add(CAGEAnimation.InterpolationMode.Flat);
            animKeyframeMode.Items.Add(CAGEAnimation.InterpolationMode.Linear);
            animKeyframeMode.Items.Add(CAGEAnimation.InterpolationMode.Bezier);
        }

        private void SetupEntityListDisplay()
        {
            entityList.DrawMode = DrawMode.OwnerDrawFixed;
            entityList.ItemHeight = 18;
            entityList.DrawItem += entityList_DrawItem;
        }

        private void entityList_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0) return;

            string title = entityList.Items[e.Index].ToString();
            string detail = (e.Index < entityListDetails.Count) ? entityListDetails[e.Index] : "";

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (System.Drawing.SolidBrush chipBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0x1f, 0x77, 0xb4)))
                e.Graphics.FillRectangle(chipBrush, e.Bounds.Left + 4, e.Bounds.Top + 5, 9, 9);

            System.Drawing.Brush textBrush = selected ? System.Drawing.SystemBrushes.HighlightText : System.Drawing.SystemBrushes.ControlText;
            System.Drawing.Brush detailBrush = selected ? System.Drawing.SystemBrushes.HighlightText : System.Drawing.SystemBrushes.GrayText;

            using (System.Drawing.Font boldFont = new System.Drawing.Font(e.Font, System.Drawing.FontStyle.Bold))
            {
                float x = e.Bounds.Left + 18;
                e.Graphics.DrawString(title, boldFont, textBrush, x, e.Bounds.Top + 2);
                System.Drawing.SizeF titleSize = e.Graphics.MeasureString(title, boldFont);
                if (!string.IsNullOrEmpty(detail))
                    e.Graphics.DrawString("   " + detail, e.Font, detailBrush, x + titleSize.Width, e.Bounds.Top + 2);
            }
            e.DrawFocusRectangle();
        }

        private void UpdateEntityList()
        {
            entityListToHierarchies = new List<EntityPath>();
            entityListDetails = new List<string>();
            entityList.BeginUpdate();
            entityList.Items.Clear();
            List<CAGEAnimation.Connection> connections = animEntity.connections.FindAll(o => o.binding_type == ObjectType.ENTITY);
            foreach (CAGEAnimation.Connection connection in connections)
            {
                string connectionLink = Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity, _entityDisplay.Composite), SettingsManager.GetBool(Settings.ShowShortGuids));
                if (!entityList.Items.Contains(connectionLink))
                {
                    EntityPath thisEntity = connection.connectedEntity;
                    int paramCount = animEntity.connections.Count(o => o.connectedEntity == thisEntity && animEntity.animations.Any(a => a.shortGUID == o.target_track));
                    int eventCount = animEntity.connections.Count(o => o.connectedEntity == thisEntity && animEntity.events.Any(ev => ev.shortGUID == o.target_track));
                    string detail = paramCount + (paramCount == 1 ? " track" : " tracks");
                    if (eventCount > 0) detail += ", " + eventCount + (eventCount == 1 ? " event" : " events");

                    entityList.Items.Add(connectionLink);
                    entityListToHierarchies.Add(connection.connectedEntity);
                    entityListDetails.Add(detail);
                }
            }
            entityList.EndUpdate();
            entityList.SelectedIndex = (entityList.Items.Count < 1) ? -1 : 0;
        }

        public float CalculateAnimLength()
        {
            float animLength = 0.0f;
            for (int i = 0; i < animEntity.animations.Count; i++)
            {
                for (int x = 0; x < animEntity.animations[i].keyframes.Count; x++)
                {
                    if (animLength < animEntity.animations[i].keyframes[x].time)
                        animLength = animEntity.animations[i].keyframes[x].time;
                }
            }
            for (int i = 0; i < animEntity.events.Count; i++)
            {
                for (int x = 0; x < animEntity.events[i].keyframes.Count; x++)
                {
                    if (animLength < animEntity.events[i].keyframes[x].time)
                        animLength = animEntity.events[i].keyframes[x].time;
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

            CurveEditor editor = new CurveEditor(animHost.Width, animHost.Height);
            editor.Setup(0, anim_length);

            // Filter down anims to the selected entity in the dropdown
            if (entityList.SelectedIndex != -1)
            {
                List<CAGEAnimation.FloatTrack> filteredAnims = new List<CAGEAnimation.FloatTrack>();
                List<CAGEAnimation.Connection> filteredConnections = animEntity.connections.FindAll(o => o.connectedEntity == entityListToHierarchies[entityList.SelectedIndex]);
                for (int i = 0; i < filteredConnections.Count; i++)
                {
                    CAGEAnimation.FloatTrack anim = animEntity.animations.FirstOrDefault(o => o.shortGUID == filteredConnections[i].target_track);
                    if (anim != null) filteredAnims.Add(anim);
                }

                for (int i = 0; i < filteredAnims.Count; i++)
                {
                    CAGEAnimation.Connection connection = filteredConnections.FirstOrDefault(o => o.target_track == filteredAnims[i].shortGUID);
                    string keyframeText = connection.target_sub_param.ToString() == "" ? connection.target_param.ToString() : connection.target_param.ToString() + " [" + connection.target_sub_param.ToString() + "]";
                    editor.AddCurve(filteredAnims[i], keyframeText);
                    animTracks.Add(keyframeText);
                }
            }

            // Overlay all event markers as yellow vertical lines
            for (int i = 0; i < animEntity.events.Count; i++)
            {
                CAGEAnimation.Connection connection = animEntity.connections.FirstOrDefault(o => o.target_track == animEntity.events[i].shortGUID);
                string label = (connection == null)
                    ? Content.Level.Commands.Utils.GetEntityName(_entityDisplay.Composite, animEntity)
                    : Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity, _entityDisplay.Composite), SettingsManager.GetBool(Settings.ShowShortGuids));
                for (int x = 0; x < animEntity.events[i].keyframes.Count; x++)
                    editor.AddEvent(animEntity.events[i], animEntity.events[i].keyframes[x], label);
            }

            editor.KeyframeSelected += AnimCurve_KeyframeSelected;
            editor.SelectionCleared += AnimCurve_SelectionCleared;
            editor.EventSelected += AnimCurve_EventSelected;
            editor.EventSelectionCleared += AnimCurve_EventSelectionCleared;
            editor.EventAddRequested += AnimCurve_EventAddRequested;
            editor.Rebuild();
            animCurveEditor = editor;
            animHost.Child = editor;
        }

        private void AnimCurve_KeyframeSelected(CAGEAnimation.FloatTrack.Keyframe kf)
        {
            activeAnimKeyframe = kf;
            activeGraphEventKeyframe = null;
            animKeyframeData.Visible = true;
            graphEventData.Visible = false;

            _suppressKeyframeEvents = true;
            animKeyframeValue.Text = kf.value.Y.ToString();
            if (animKeyframeMode.Items.Contains(kf.mode))
                animKeyframeMode.SelectedItem = kf.mode;
            else
                animKeyframeMode.SelectedIndex = -1;
            _suppressKeyframeEvents = false;
        }
        private void AnimCurve_SelectionCleared()
        {
            activeAnimKeyframe = null;
            animKeyframeData.Visible = false;
        }
        private void AnimCurve_EventSelected(CAGEAnimation.EventTrack.Keyframe kf)
        {
            activeGraphEventKeyframe = kf;
            activeAnimKeyframe = null;
            graphEventData.Visible = true;
            animKeyframeData.Visible = false;

            _suppressKeyframeEvents = true;
            graphEventParam1.Text = kf.forward.ToString();
            graphEventParam2.Text = kf.reverse.ToString();
            _suppressKeyframeEvents = false;
        }
        private void AnimCurve_EventSelectionCleared()
        {
            activeGraphEventKeyframe = null;
            graphEventData.Visible = false;
        }
        private void AnimCurve_EventAddRequested(float time)
        {
            CAGEAnimation.EventTrack track = GetOrCreateSelfEventTrack();
            CAGEAnimation.EventTrack.Keyframe key = new CAGEAnimation.EventTrack.Keyframe();
            key.time = time;
            key.forward = ShortGuidUtils.Generate("");
            key.reverse = ShortGuidUtils.Generate("");
            track.keyframes.Add(key);

            RefreshEventTrackLists();
            SetupAnimTimeline();
            if (animCurveEditor != null)
                animCurveEditor.SelectEvent(key);
        }

        private CAGEAnimation.EventTrack GetOrCreateSelfEventTrack()
        {
            // Prefer an event track with no connection (events fired on the CAGEAnimation itself)
            for (int i = 0; i < animEntity.events.Count; i++)
            {
                if (!animEntity.connections.Any(c => c.target_track == animEntity.events[i].shortGUID))
                    return animEntity.events[i];
            }

            CAGEAnimation.EventTrack track = new CAGEAnimation.EventTrack();
            track.shortGUID = ShortGuidUtils.GenerateRandom();
            animEntity.events.Add(track);
            return track;
        }

        private void deleteGraphEvent_Click(object sender, EventArgs e)
        {
            if (animCurveEditor == null) return;
            animCurveEditor.RemoveSelectedEvent();
            activeGraphEventKeyframe = null;
            graphEventData.Visible = false;
            RefreshEventTrackLists();
        }
        private void graphEventParam1_TextChanged(object sender, EventArgs e)
        {
            if (_suppressKeyframeEvents || activeGraphEventKeyframe == null) return;
            if (activeGraphEventKeyframe.forward.ToByteString() == graphEventParam1.Text) return;
            activeGraphEventKeyframe.forward = ShortGuidUtils.Generate(graphEventParam1.Text);
            if (animCurveEditor != null) animCurveEditor.RefreshSelectedKeyframeVisual();
        }
        private void graphEventParam2_TextChanged(object sender, EventArgs e)
        {
            if (_suppressKeyframeEvents || activeGraphEventKeyframe == null) return;
            if (activeGraphEventKeyframe.reverse.ToByteString() == graphEventParam2.Text) return;
            activeGraphEventKeyframe.reverse = ShortGuidUtils.Generate(graphEventParam2.Text);
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
                    entityList.SelectedIndex = i;
                    this.BringToFront();
                    this.Focus();
                    return;
                }
            }

            //Creating a placeholder here that points to nothing so that the dropdown will pick it up - not ideal, but shouldn't affect anything
            CAGEAnimation.Connection newConnection = new CAGEAnimation.Connection();
            newConnection.connectedEntity.path = generatedHierarchy;
            newConnection.binding_type = ObjectType.ENTITY;
            animEntity.connections.Add(newConnection);

            UpdateEntityList();
            entityList.SelectedIndex = entityList.Items.Count - 1;
            this.BringToFront();
            this.Focus();
        }

        private void addAnimationTrack_Click(object sender, EventArgs e)
        {
            if (entityList.SelectedIndex == -1) return;
            try
            {
                CAGEAnimation_SelectParameter paramSelector = new CAGEAnimation_SelectParameter(Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(entityListToHierarchies[entityList.SelectedIndex], _entityDisplay.Composite)).Item2);
                paramSelector.OnParamSelected += OnParameterSelected;
                paramSelector.Show();
            }
            catch { }
        }
        private void OnParameterSelected(Parameter param)
        {
            //Make sure the same parameter isn't being added twice for the same entity
            if (animEntity.connections.FindAll(o => o.connectedEntity == entityListToHierarchies[entityList.SelectedIndex] && o.target_param == param.name).Count != 0)
            {
                MessageBox.Show("This parameter is already controlled by the CAGEAnimation!", "Parameter already selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.BringToFront();
                this.Focus();
                return;
            }

            CAGEAnimation.Connection connection = new CAGEAnimation.Connection();
            connection.connectedEntity.path = entityListToHierarchies[entityList.SelectedIndex].path;
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

            SetupAnimTimeline();
            this.BringToFront();
            this.Focus();
        }
        private void AddNewConnectionSet(CAGEAnimation.Connection conn, float defaultKeyValue, ShortGuid paramID, string subProp = "")
        {
            CAGEAnimation.FloatTrack.Keyframe keyStart = new CAGEAnimation.FloatTrack.Keyframe();
            keyStart.time = 0.0f;
            keyStart.value.Y = defaultKeyValue;
            keyStart.mode = CAGEAnimation.InterpolationMode.Linear;
            CAGEAnimation.FloatTrack.Keyframe keyEnd = new CAGEAnimation.FloatTrack.Keyframe();
            keyEnd.time = anim_length;
            keyEnd.value.Y = defaultKeyValue;
            keyEnd.mode = CAGEAnimation.InterpolationMode.Linear;
            CAGEAnimation.FloatTrack anim = new CAGEAnimation.FloatTrack();
            anim.shortGUID = ShortGuidUtils.GenerateRandom();
            anim.keyframes.Add(keyStart);
            anim.keyframes.Add(keyEnd);
            animEntity.animations.Add(anim);
            conn.binding_guid = ShortGuidUtils.GenerateRandom();
            conn.target_param = paramID;
            conn.target_sub_param = ShortGuidUtils.Generate(subProp);
            conn.target_track = anim.shortGUID;
            animEntity.connections.Add(conn);
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
            List<CAGEAnimation.Connection> trimmedConnections = new List<CAGEAnimation.Connection>();
            for (int i = 0; i < animEntity.connections.Count; i++)
            {
                if (animEntity.connections[i].target_track == animEntity.animations[index].shortGUID) continue;
                trimmedConnections.Add(animEntity.connections[i]);
            }
            animEntity.connections = trimmedConnections;
            animEntity.animations.RemoveAt(index);
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
            eventTrigger.keyframes.Add(new CAGEAnimation.EventTrack.Keyframe() { time = CalculateAnimLength(), forward = ShortGuidUtils.Generate(""), reverse = ShortGuidUtils.Generate("") }); 
            animEntity.events.Add(eventTrigger);

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
                if (animEntity.connections[i].target_track == animEntity.events[index].shortGUID) continue;
                trimmedConnections.Add(animEntity.connections[i]);
            }
            animEntity.connections = trimmedConnections;
            animEntity.events.RemoveAt(index);
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
        private void animKeyframeMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressKeyframeEvents || activeAnimKeyframe == null) return;
            if (animKeyframeMode.SelectedItem == null) return;
            activeAnimKeyframe.mode = (CAGEAnimation.InterpolationMode)animKeyframeMode.SelectedItem;
            if (animCurveEditor != null) animCurveEditor.RefreshSelectedKeyframeVisual();
        }

        private void entityList_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetupAnimTimeline();
        }

        private void animLength_TextChanged(object sender, EventArgs e)
        {
            animLength.Text = EditorUtils.ForceStringNumeric(animLength.Text, true);
        }
        private void editAnimLength_Click(object sender, EventArgs e)
        {
            animLength.Text = EditorUtils.ForceStringNumeric(animLength.Text, true);
            float newAnimLength = Convert.ToSingle(animLength.Text);

            //Validate no keyframes are below the new length
            float actualAnimLength = CalculateAnimLength();
            if (actualAnimLength > newAnimLength)
            {
                MessageBox.Show("There are keyframes that are placed beyond the new animation length.\nPlease move these keyframes within range before updating the length!", "Actual animation exceeds requested length!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                animLength.Text = anim_length.ToString();
                return;
            }

            anim_length = newAnimLength;
            SetupAnimTimeline();
        }
    }
}
