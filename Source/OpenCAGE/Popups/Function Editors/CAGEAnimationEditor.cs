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

        Dictionary<Keyframe, CAGEAnimation.FloatTrack.Keyframe> keyframeHandlesAnim;
        Dictionary<Keyframe, CAGEAnimation.EventTrack.Keyframe> keyframeHandlesEvent;

        Dictionary<Track, CAGEAnimation.FloatTrack> tracksAnim;
        Dictionary<Track, CAGEAnimation.EventTrack> tracksEvent;

        List<EntityPath> entityListToHierarchies;

        EntityInspector _entityDisplay;

        public CAGEAnimationEditor(EntityInspector entityDisplay) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _entityDisplay = entityDisplay;

            animEntity = ((CAGEAnimation)_entityDisplay.Entity).Copy();
            //File.WriteAllText("out.json", JsonConvert.SerializeObject(animEntity, Formatting.Indented));
            InitializeComponent();

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

            SetupAnimTimeline();
            SetupEventTimeline();

            this.BringToFront();
            this.Focus();
        }

        private void UpdateEntityList()
        {
            entityListToHierarchies = new List<EntityPath>();
            entityList.BeginUpdate();
            entityList.Items.Clear();
            List<CAGEAnimation.Connection> connections = animEntity.connections.FindAll(o => o.binding_type == ObjectType.ENTITY);
            foreach (CAGEAnimation.Connection connection in connections)
            {
                string connectionLink = Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity, _entityDisplay.Composite), SettingsManager.GetBool(Singleton.Settings.ShowShortGuids));
                if (!entityList.Items.Contains(connectionLink))
                {
                    entityList.Items.Add(connectionLink);
                    entityListToHierarchies.Add(connection.connectedEntity);
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
            keyframeHandlesAnim = new Dictionary<Keyframe, CAGEAnimation.FloatTrack.Keyframe>();
            tracksAnim = new Dictionary<Track, CAGEAnimation.FloatTrack>();

            activeAnimKeyframe = null;
            activeAnimHandle = null;
            animHost.Child = null;
            animKeyframeData.Visible = false;
            animTracks.Clear();

            //Filter down anims to the selected entity in the dropdown
            if (entityList.SelectedIndex == -1) return;
            List<CAGEAnimation.FloatTrack> filteredAnims = new List<CAGEAnimation.FloatTrack>();
            List<CAGEAnimation.Connection> filteredConnections = animEntity.connections.FindAll(o => o.connectedEntity == entityListToHierarchies[entityList.SelectedIndex]);
            for (int i = 0; i < filteredConnections.Count; i++)
            {
                CAGEAnimation.FloatTrack anim = animEntity.animations.FirstOrDefault(o => o.shortGUID == filteredConnections[i].target_track);
                if (anim != null) filteredAnims.Add(anim);
            }

            float anim_step = anim_length < 10.0f ? 1.0f : anim_length / 10.0f;

            Timeline animTimeline = new Timeline(animHost.Width, animHost.Height);
            animTimeline.OnNewKeyframe += OnKeyframeAddedToTrack_Anim;
            animTimeline.Setup(0, anim_length, anim_step, 150);
            for (int i = 0; i < filteredAnims.Count; i++)
            {
                CAGEAnimation.Connection connection = filteredConnections.FirstOrDefault(o => o.target_track == filteredAnims[i].shortGUID);
                for (int x = 0; x < filteredAnims[i].keyframes.Count; x++)
                {
                    CAGEAnimation.FloatTrack.Keyframe keyframeData = filteredAnims[i].keyframes[x];
                    string keyframeText = connection.target_sub_param.ToString() == "" ? connection.target_param.ToString() : connection.target_param.ToString() + " [" + connection.target_sub_param.ToString() + "]";
                    Keyframe keyframeUI = animTimeline.AddKeyframe(keyframeData.time, keyframeText);
                    keyframeUI.OnMoved += OnHandleMoved;
                    keyframeUI.HandleText = keyframeData.value.Y.ToString("0.00");
                    keyframeHandlesAnim.Add(keyframeUI, keyframeData);
                    if (!tracksAnim.ContainsKey(keyframeUI.Track))
                    {
                        tracksAnim.Add(keyframeUI.Track, filteredAnims[i]);
                        animTracks.Add(keyframeText);
                    }
                }
            }
            animHost.Child = animTimeline;
        }

        List<string> eventTracks = new List<string>();
        List<Entity> eventEntityIDs = new List<Entity>();
        private void SetupEventTimeline()
        {
            keyframeHandlesEvent = new Dictionary<Keyframe, CAGEAnimation.EventTrack.Keyframe>();
            tracksEvent = new Dictionary<Track, CAGEAnimation.EventTrack>();

            activeEventKeyframe = null;
            activeEventHandle = null;
            eventHost.Child = null;
            eventKeyframeData.Visible = false;
            eventTracks.Clear();
            eventEntityIDs.Clear();

            float anim_step = anim_length < 10.0f ? 1.0f : anim_length / 10.0f;

            Timeline eventTimeline = new Timeline(eventHost.Width, eventHost.Height);
            eventTimeline.OnNewKeyframe += OnKeyframeAddedToTrack_Event;
            eventTimeline.Setup(0, anim_length, anim_step, 150);
            for (int i = 0; i < animEntity.events.Count; i++)
            {
                //TODO: Frequently CHARACTER and MARKER objects both point to the same Event object - need to handle this better!
                CAGEAnimation.Connection connection = animEntity.connections.FirstOrDefault(o => o.target_track == animEntity.events[i].shortGUID);
                for (int x = 0; x < animEntity.events[i].keyframes.Count; x++)
                {
                    CAGEAnimation.EventTrack.Keyframe keyframeData = animEntity.events[i].keyframes[x];
                    string keyframeText = (connection == null) ? Content.Level.Commands.Utils.GetEntityName(_entityDisplay.Composite, animEntity) : Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity, _entityDisplay.Composite), SettingsManager.GetBool(Singleton.Settings.ShowShortGuids));
                    Keyframe keyframeUI = eventTimeline.AddKeyframe(keyframeData.time, keyframeText);
                    keyframeUI.OnMoved += OnHandleMoved;
                    keyframeHandlesEvent.Add(keyframeUI, keyframeData);
                    if (!tracksEvent.ContainsKey(keyframeUI.Track))
                    {
                        tracksEvent.Add(keyframeUI.Track, animEntity.events[i]);
                        eventTracks.Add(keyframeText);
                        eventEntityIDs.Add(connection == null ? animEntity : Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity, _entityDisplay.Composite)).Item2);
                    }
                }
            }
            eventHost.Child = eventTimeline;
        }

        private void OnKeyframeAddedToTrack_Anim(Keyframe key)
        {
            CAGEAnimation.FloatTrack e = tracksAnim[key.Track];
            CAGEAnimation.FloatTrack.Keyframe keyData = new CAGEAnimation.FloatTrack.Keyframe();
            keyData.time = key.Seconds;
            e.keyframes.Add(keyData);
            keyframeHandlesAnim.Add(key, keyData);
            key.OnMoved += OnHandleMoved;
        }
        private void OnKeyframeAddedToTrack_Event(Keyframe key)
        {
            CAGEAnimation.EventTrack e = tracksEvent[key.Track];
            CAGEAnimation.EventTrack.Keyframe keyData = new CAGEAnimation.EventTrack.Keyframe();
            keyData.time = key.Seconds;
            keyData.forward = ShortGuidUtils.Generate("");
            keyData.reverse = ShortGuidUtils.Generate("");
            e.keyframes.Add(keyData);
            keyframeHandlesEvent.Add(key, keyData);
            key.OnMoved += OnHandleMoved;
        }

        CAGEAnimation.FloatTrack.Keyframe activeAnimKeyframe = null;
        CAGEAnimation.EventTrack.Keyframe activeEventKeyframe = null;
        Keyframe activeAnimHandle = null;
        Keyframe activeEventHandle = null;
        private void OnHandleMoved(Keyframe handle, float time)
        {
            if (keyframeHandlesAnim.TryGetValue(handle, out CAGEAnimation.FloatTrack.Keyframe animKeyframe))
            {
                if (activeAnimHandle != null) activeAnimHandle.Highlight(false);
                handle.Highlight();
                activeAnimHandle = handle;

                activeAnimKeyframe = animKeyframe;
                activeAnimKeyframe.time = time;
                animKeyframeData.Visible = true;
                animKeyframeValue.Text = activeAnimKeyframe.value.Y.ToString(); //todo support X
                startVelX.Text = activeAnimKeyframe.tan_in.X.ToString();
                startVelY.Text = activeAnimKeyframe.tan_in.Y.ToString();
                endVelX.Text = activeAnimKeyframe.tan_out.X.ToString();
                endVelY.Text = activeAnimKeyframe.tan_out.Y.ToString();
            }
            else if (keyframeHandlesEvent.TryGetValue(handle, out CAGEAnimation.EventTrack.Keyframe eventKeyframe))
            {
                if (activeEventHandle != null) activeEventHandle.Highlight(false);
                handle.Highlight();
                activeEventHandle = handle;

                activeEventKeyframe = eventKeyframe;
                activeEventKeyframe.time = time;
                eventKeyframeData.Visible = true;
                eventParam1.Text = activeEventKeyframe.forward.ToString();
                eventParam2.Text = activeEventKeyframe.reverse.ToString();
            }
            else
            {
                //WARNING: Invalid logic!
            }
        }

        private void deleteAnimKeyframe_Click(object sender, EventArgs e)
        {
            tracksAnim[activeAnimHandle.Track].keyframes.Remove(activeAnimKeyframe);
            ((Timeline)animHost.Child).RemoveKeyframe(activeAnimHandle);
            activeAnimHandle = null;
            animKeyframeData.Visible = false;
        }
        private void deleteEventKeyframe_Click(object sender, EventArgs e)
        {
            tracksEvent[activeEventHandle.Track].keyframes.Remove(activeEventKeyframe);
            ((Timeline)eventHost.Child).RemoveKeyframe(activeEventHandle);
            activeEventHandle = null;
            eventKeyframeData.Visible = false;
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
            CAGEAnimation.FloatTrack.Keyframe keyEnd = new CAGEAnimation.FloatTrack.Keyframe();
            keyEnd.time = anim_length;
            keyEnd.value.Y = defaultKeyValue;
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
            SetupEventTimeline();
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

            SetupEventTimeline();
            this.BringToFront();
            this.Focus();
        }

        private void deleteEventTrack_Click(object sender, EventArgs e)
        {
            try
            {
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
            SetupEventTimeline();
        }

        private void SaveEntity_Click(object sender, EventArgs e)
        {
            animEntity.AddParameter("anim_length", new cFloat(anim_length));
            OnSaved?.Invoke(animEntity);
            this.Close();
        }

        private void animKeyframeValue_TextChanged(object sender, EventArgs e)
        {
            animKeyframeValue.Text = EditorUtils.ForceStringNumeric(animKeyframeValue.Text, true);
            activeAnimKeyframe.value.Y = Convert.ToSingle(animKeyframeValue.Text);
            activeAnimHandle.HandleText = activeAnimKeyframe.value.Y.ToString("0.00");
        }
        private void startVelX_TextChanged(object sender, EventArgs e)
        {
            startVelX.Text = EditorUtils.ForceStringNumeric(startVelX.Text, true);
            activeAnimKeyframe.tan_in.X = Convert.ToSingle(startVelX.Text);
        }
        private void startVelY_TextChanged(object sender, EventArgs e)
        {
            startVelY.Text = EditorUtils.ForceStringNumeric(startVelY.Text, true);
            activeAnimKeyframe.tan_in.Y = Convert.ToSingle(startVelY.Text);
        }
        private void endVelX_TextChanged(object sender, EventArgs e)
        {
            endVelX.Text = EditorUtils.ForceStringNumeric(endVelX.Text, true);
            activeAnimKeyframe.tan_out.X = Convert.ToSingle(endVelX.Text);
        }
        private void endVelY_TextChanged(object sender, EventArgs e)
        {
            endVelY.Text = EditorUtils.ForceStringNumeric(endVelY.Text, true);
            activeAnimKeyframe.tan_out.Y = Convert.ToSingle(endVelY.Text);
        }

        private void eventParam1_TextChanged(object sender, EventArgs e)
        {
            //Handle non-convertable param names
            if (activeEventKeyframe.forward.ToByteString() == eventParam1.Text) return;
            activeEventKeyframe.forward = ShortGuidUtils.Generate(eventParam1.Text);
        }
        private void eventParam2_TextChanged(object sender, EventArgs e)
        {
            //Handle non-convertable param names
            if (activeEventKeyframe.reverse.ToByteString() == eventParam2.Text) return;
            activeEventKeyframe.reverse = ShortGuidUtils.Generate(eventParam2.Text);
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
            SetupEventTimeline();
        }
    }
}
