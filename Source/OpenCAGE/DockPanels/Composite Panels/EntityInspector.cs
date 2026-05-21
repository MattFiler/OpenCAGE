using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Properties;
using OpenCAGE.UserControls;
using OpenCAGE;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using static OpenCAGE.SelectEnumString;
using static System.Windows.Forms.LinkLabel;

namespace OpenCAGE.DockPanels
{
    public partial class EntityInspector : DockContent
    {
        private CompositeDisplay _compositeDisplay;
        public CompositeDisplay CompositeDisplay => _compositeDisplay;

        private Entity _entity = null;
        private Composite _entityCompositePtr = null; //The composite that this entity points to, if it does.

        public bool Populated => _entity != null;

        public LevelContent Content => _compositeDisplay.Content;

        public Entity Entity => _entity;
        public Composite Composite => _compositeDisplay.Composite;

        private bool _displayingLinks = true;
        public bool DisplayingLinks => _displayingLinks;

        public EntityInspector(CompositeDisplay compositeDisplay)
        {
            _compositeDisplay = compositeDisplay;

            this.FormClosing += (s, e) => { DepopulateUI(); };
            this.FormClosed += EntityDisplay_FormClosed;

            InitializeComponent();

            Singleton.OnEntityAddPending += OnEntityAddPending;
            Singleton.OnEntityAdded += OnEntityAdded;
            Singleton.OnEntityRenamed += OnEntityRenamed;
            Singleton.OnCompositeRenamed += OnCompositeRenamed;

            Reload();

            this.DockStateChanged += EntityInspector_DockStateChanged;

            this.CloseButtonVisible = false;
        }

        private void EntityInspector_DockStateChanged(object sender, EventArgs e)
        {
            Debug.Log("Entity Inspector", "Dock state changed to " + DockState);
            if (DockState == DockState.Unknown || DockState == DockState.Hidden)
                return;

            if (DockState == _previousDockState) return;
            _previousDockState = DockState;

            SettingsManager.SetString(Singleton.Settings.EntityInspectorState, DockState.ToString());
        }

        private void OnEntityAddPending()
        {
            if (_prevTask != null && !_prevTask.IsCompleted && _prevTaskToken != null)
            {
                _prevTaskToken.Cancel();
            }
        }
        private void OnEntityAdded(Entity e)
        {
            if (_prevTask != null && !_prevTask.IsCompleted)
            {
                StartBackgroundEntityLoader();
            }
        }

        //TODO: this is not as efficient as it could be: really we should only reload if we know we're affected by the rename
        private void OnEntityRenamed(Entity entity, string name)
        {
            if (!Populated) return;
            Reload();
        }
        public void ApplyTransformFromExternal(cTransform transform)
        {
            if (!Populated || _entity == null || transform == null)
                return;

            bool updated = false;
            foreach (Control control in entity_params.Controls)
            {
                if (control is GUI_TransformDataType transformControl)
                {
                    transformControl.SetTransformValues(transform);
                    updated = true;
                }
            }

            if (!updated)
                _compositeDisplay.ReloadEntity(_entity);
        }

        private void OnCompositeRenamed(Composite composite, string name)
        {
            if (!Populated) return;
            Reload();
        }

        public void PopulateUI(Entity entity, bool displayLinks)
        {
            if (!this.Visible)
                this.Show();
            
            _entity = entity;
            _entityCompositePtr = _entity.variant == EntityVariant.FUNCTION ? Content.Level.Commands.GetComposite(((FunctionEntity)_entity).function) : null;

            switch (_entity.variant)
            {
                case EntityVariant.VARIABLE:
                    this.Icon = Resources.AnimatorController_Icon;
                    break;
                case EntityVariant.FUNCTION:
                    if (Content.Level.Commands.GetComposite(((FunctionEntity)_entity).function) == null)
                        this.Icon = Resources.d_ScriptableObject_Icon_braces_only;
                    else
                        this.Icon = Resources.d_PrefabVariant_Icon;
                    break;
                case EntityVariant.PROXY:
                    this.Icon = Resources.d_ScriptableObject_Icon;
                    break;
                case EntityVariant.ALIAS:
                    this.Icon = Resources.AreaEffector2D_Icon;
                    break;
            }

            Reload(displayLinks);

            this.Activate();
        }

        public void DepopulateUI()
        {
            this.Hide();
            EntityDisplay_FormClosed(null, null);
        }

        private void EntityDisplay_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.FormClosed -= EntityDisplay_FormClosed;
            Singleton.OnEntityAddPending -= OnEntityAddPending;
            Singleton.OnEntityAdded -= OnEntityAdded;
            Singleton.OnEntityRenamed -= OnEntityRenamed;
            Singleton.OnCompositeRenamed -= OnCompositeRenamed;

            for (int i = 0; i < entity_params.Controls.Count; i++)
            {
                if (entity_params.Controls[i] is GUI_Link)
                {
                    GUI_Link link = (GUI_Link)entity_params.Controls[i];
                    link.GoToEntity -= _compositeDisplay.LoadEntityAndFocusNode;
                    link.OnLinkEdited -= OnLinkEdited;
                }
                entity_params.Controls[i].Dispose();
            }
            entity_params.Controls.Clear();

            _entity = null;
            _entityCompositePtr = null;

            imageList1.Images.Clear();
            imageList1.Dispose();

            if (add_parameter != null)
            {
                add_parameter.OnSaved -= Reload;
                add_parameter.Close();
            }
        }

        /* Reload this display */
        public void Reload() => Reload(_displayingLinks);
        public void Reload(bool displayLinks)
        {
#if DO_ENTITY_PERF_CHECK
            //TODO: The performance here is pretty poor. I should swap to using the PropertyGrid.
            Stopwatch timer = Stopwatch.StartNew();
            Debug.Log("Entity Inspector", "** RELOAD START **");
#endif

            if (this.IsDisposed || this.Disposing || entity_params == null || entity_params.IsDisposed || entity_params.Disposing)
            {
#if DO_ENTITY_PERF_CHECK
                timer.Stop();
#endif
                return;
            }

            _displayingLinks = displayLinks;
            ModifyParameters.Visible = !_displayingLinks;

            //UI defaults - TODO: just set this in the designer.
            entityInfoGroup.Text = "Selected Entity Info";
            entityParamGroup.Text = "Selected Entity Parameters";
            selected_entity_type_description.Text = "";
            selected_entity_name.Text = "";
            
            for (int i = entity_params.Controls.Count - 1; i >= 0; i--)
            {
                try
                {
                    Control ctrl = entity_params.Controls[i];
                    if (ctrl == null || ctrl.IsDisposed)
                        continue;

                    if (ctrl is ParameterUserControl)
                        ((ParameterUserControl)ctrl).OnDeleted -= OnDeleteParam;
                    else if (ctrl is GUI_Link)
                    {
                        GUI_Link link = (GUI_Link)ctrl;
                        link.GoToEntity -= _compositeDisplay.LoadEntityAndFocusNode;
                        link.OnLinkEdited -= OnLinkEdited;
                    }

                    ctrl.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.Log("Entity Inspector", $"Error disposing control: {ex.Message}");
                }
            }
            entity_params.Controls.Clear();
            jumpToComposite.Visible = false;
            editFunction.Enabled = false;
            editEntityResources.Enabled = false;
            editEntityMovers.Enabled = false;
            showOverridesAndProxies.Enabled = false;
            goToZone.Enabled = false;
            hierarchyDisplay.Visible = false;

            //NOTE: These visibility options should be mirrored in EntityListContextMenu_Opening in EntityList
            renameEntity.Enabled = _entity != null && _entity.variant != EntityVariant.ALIAS && _entity.variant != EntityVariant.VARIABLE; //TODO: we should support variable renaming, but doing that requires managing renaming all links/params (including node links)
            duplicateEntity.Enabled = _entity != null && _entity.variant != EntityVariant.ALIAS && _entity.variant != EntityVariant.VARIABLE; //This works, but why would you ever want to?
            deleteEntity.Enabled = _entity != null;

            ModifyParameters.Enabled = _entity != null;
            ModifyParameters_Link.Enabled = _entity != null;
            addLinkOut.Enabled = _entity != null;
            applyDefaultsToolStripMenuItem.Enabled = _entity != null;

            if (_entity == null)
            {
#if DO_ENTITY_PERF_CHECK
                timer.Stop();
#endif
                return;
            }

            Cursor.Current = Cursors.WaitCursor;
            StartBackgroundEntityLoader();
            List<Control> controls = new List<Control>();

            //populate info labels
            string entityVariantStr = "";
            switch (_entity.variant)
            {
                case EntityVariant.FUNCTION:
                    entityVariantStr = _entityCompositePtr != null ? "Composite Instance" : "Function";
                    break;
                case EntityVariant.VARIABLE:
                    //TODO: we should have a custom display for these. it's kinda weird to have parameters of parameters in this UI
                    entityVariantStr = "Composite Parameter";
                    break;
                case EntityVariant.PROXY:
                    entityVariantStr = "Proxy";
                    break;
                case EntityVariant.ALIAS:
                    entityVariantStr = "Alias";
                    break;
            }
            entityInfoGroup.Text = "Selected " + entityVariantStr + " Info";
            entityParamGroup.Text = "Selected " + entityVariantStr + " Parameters";

            //TODO: change this text contextually based on the linked editor - and hide the button when one isn't available.
            editFunction.Text = "Function";

            //TODO: we can correctly show the resources button now based on parameter info
            CompositePinInfoTable.PinInfo variableInfo = null;
            string description = "";
            switch (_entity.variant)
            {
                case EntityVariant.FUNCTION:
                    selected_entity_name.Text = Content.Level.Commands.Utils.GetEntityName(Composite.shortGUID, _entity.shortGUID);

                    //Composite Instance
                    if (_entityCompositePtr != null)
                    {
                        jumpToComposite.Visible = true;
                        editEntityResources.Enabled = false;
                        description = _entityCompositePtr.name;
                        //editFunction.Enabled = true;
                        //editFunction.Text = "Alias Overrides"; //TODO: show count?
                    }

                    //Function Entity
                    else
                    {
                        jumpToComposite.Visible = false;
                        editEntityResources.Enabled = (Content.Level.Models != null); //TODO: we can hide this button completely outside of this state

                        FunctionType function = ((FunctionEntity)_entity).function.AsFunctionType;
                        description = function.ToString();
                        editFunction.Enabled = function == FunctionType.CAGEAnimation || function == FunctionType.TriggerSequence || function == FunctionType.Character;
                    }
                    break;
                case EntityVariant.VARIABLE:
                    variableInfo = Content.Level.Commands.Utils.GetPinInfo(Composite, (VariableEntity)Entity);
                    if (variableInfo == null)
                        Debug.Log("Entity Inspector", "Warning: Could not get parameter pin info!");
                    description = (variableInfo != null ? ((CompositePinType)variableInfo.PinTypeGUID.AsUInt32).ToUIString() : ((VariableEntity)_entity).type.ToUIString());
                    selected_entity_name.Text = ShortGuidUtils.FindString(((VariableEntity)_entity).name);
                    break;
                case EntityVariant.PROXY:
                case EntityVariant.ALIAS:
                    hierarchyDisplay.Visible = true;
                    List<Tuple<Composite, Entity>> resolvedHierarchy = Content.Level.Commands.Utils.ResolveAliasOrProxy(_entity, Composite);
                    (Composite comp, Entity ent) = Content.Level.Commands.Utils.GetResolvedTarget(resolvedHierarchy);
                    hierarchyDisplay.Text = Content.Level.Commands.Utils.GetResolvedAsString(resolvedHierarchy, SettingsManager.GetBool(Singleton.Settings.ShowShortGuids));
                    jumpToComposite.Visible = true;
                    selected_entity_name.Text = (_entity.variant == EntityVariant.PROXY ? "Proxy to " : "Alias of ") + Content.Level.Commands.Utils.GetEntityName(comp, ent);
                    break;
                default:
                    selected_entity_name.Text = Content.Level.Commands.Utils.GetEntityName(Composite.shortGUID, _entity.shortGUID);
                    break;
            }
            selected_entity_type_description.Text = description;
            this.Text = selected_entity_name.Text;

            //show mvr editor button if this entity has a mvr link
            if (Content.Level.Movers != null && Content.Level.Movers.Entries.FirstOrDefault(o => o.Entity?.entity_id == this._entity.shortGUID) != null)
                editEntityMovers.Enabled = true;

#if DO_ENTITY_PERF_CHECK
            Debug.Log("Entity Inspector", $"METADATA UPDATE COMPLETED: {timer.Elapsed.TotalMilliseconds} ms");
#endif

            int current_ui_offset = 7;
            if (_displayingLinks)
            {
                //populate linked params IN
                List<Entity> ents = Composite.GetEntities();
                foreach (Entity ent in ents)
                {
                    foreach (EntityConnector link in ent.childLinks)
                    {
                        if (link.linkedEntityID != _entity.shortGUID) continue;
                        GUI_Link parameterGUI = new GUI_Link(this);
                        parameterGUI.PopulateUI(link, false, ent.shortGUID);
                        parameterGUI.TrackInstanceInfo(Composite.shortGUID, Entity.shortGUID, link.linkedParamID);
                        parameterGUI.HighlightAsModified(false); //For now, marking all links as "modified", given that they likely won't be default vals
                        parameterGUI.GoToEntity += _compositeDisplay.LoadEntityAndFocusNode;
                        parameterGUI.OnLinkEdited += OnLinkEdited;
                        parameterGUI.Location = new Point(15, current_ui_offset);
                        parameterGUI.Width = entity_params.Width - 30;
                        parameterGUI.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                        current_ui_offset += parameterGUI.Height + 6;
                        controls.Add(parameterGUI);
                    }
                }
            }

#if DO_ENTITY_PERF_CHECK
            Debug.Log("Entity Inspector", $"LINK IN CONTROLS COMPLETED: {timer.Elapsed.TotalMilliseconds} ms");
#endif

#if AUTO_POPULATE_PARAMS
            //make sure all defaults are applied to the entity so that we're showing everything
            //TODO: this should also factor in links in/out - if a link already exists then we shouldn't add it as a param (or it should add it and highlight it as such)
            if (!ParameterModificationTracker.IsDefaultsApplied(Composite.shortGUID, Entity.shortGUID))
            {
#if DEBUG
                int count_pre_add = _entity.parameters.Count;
#endif
                switch (_entity.variant)
                {
                    case EntityVariant.FUNCTION:
                        EntityUtils.ApplyDefaults((FunctionEntity)_entity, true, false);
                        break;
                    case EntityVariant.PROXY:
                        EntityUtils.ApplyDefaults((ProxyEntity)_entity, true, false);
                        break;
                }
                ParameterModificationTracker.SetDefaultsApplied(Composite.shortGUID, Entity.shortGUID);
#if DEBUG
                Debug.Log("Entity Inspector", "Applied " + (_entity.parameters.Count - count_pre_add) + " defaults to entity.");
#endif
#if DO_ENTITY_PERF_CHECK
                Debug.Log("Entity Inspector", $"DEFAULTS APPLIED: {timer.Elapsed.TotalMilliseconds} ms");
#endif
            }
#endif

            //figure out what parameters we should show - the input/output pin values are 'delay' values for the pins shown on the flowgraph, not actual parameters
            List<ShortGuid> visibleParams = new List<ShortGuid>();
            bool filterParams = CompositeDisplay.SupportsFlowgraphs;
#if DEBUG
            filterParams = false; //not filtering in debug mode, like how we always show links
#endif
            if (filterParams) 
            {
                List<(ShortGuid, ParameterVariant, DataType)> allParameters = Content.Level.Commands.Utils.GetAllParameters(Entity, Composite);
                foreach ((ShortGuid, ParameterVariant, DataType) parameter in allParameters)
                {
                    switch (parameter.Item2)
                    {
                        case ParameterVariant.INTERNAL: //NOTE: shouldn't really be showing internal, but until I handle some of the values better, I need to still (e.g. resources, spline points, etc)
                        case ParameterVariant.INPUT_PIN:
                        case ParameterVariant.PARAMETER:
                        case ParameterVariant.STATE_PARAMETER:
                            visibleParams.Add(parameter.Item1);
                            break;
                    }
                    //NOTE: This same switch case is found in ModifyParameters popup window, keep in sync!
                }
            }

            //populate parameters
            _entity.parameters = _entity.parameters.OrderBy(o => o.name.ToString()).ToList();
            for (int i = 0; i < _entity.parameters.Count; i++)
            {
                if (filterParams && !visibleParams.Contains(_entity.parameters[i].name))
                {
                    Debug.Log("Entity Inspector", "Skipping parameter: " + _entity.parameters[i].name);
                    continue;
                }

                //Use our metadata to update any wrongly typed cEnumStrings to get the nice UI
                if (_entity.parameters[i].content.dataType == DataType.STRING)
                {
                    ParameterData data = Content.Level.Commands.Utils.CreateDefaultParameterData(Entity, Composite, _entity.parameters[i].name);
                    if (data != null && data.dataType == DataType.ENUM_STRING)
                    {
                        ((cEnumString)data).value = ((cString)_entity.parameters[i].content).value;
                        _entity.parameters[i].content = data;
                    }
                }

                ParameterData this_param = _entity.parameters[i].content;
                ParameterUserControl parameterGUI = null;
                string paramName = _entity.parameters[i].name.ToString();

                //HACK: We handle composite material mappings as a special type!
                if (paramName == "mapping")
                {
                    if (this_param.dataType != DataType.RESOURCE)
                    {
                        _entity.parameters[i].content = new cResource(null, ShortGuid.Invalid);
                        this_param = _entity.parameters[i].content;
                    }

                    parameterGUI = new GUI_StringVariant_MappingSelect();
                    ((GUI_StringVariant_MappingSelect)parameterGUI).PopulateUI((cResource)this_param);
                }
                else
                {
                    switch (this_param.dataType)
                    {
                        case DataType.TRANSFORM:
                            parameterGUI = new GUI_TransformDataType();
                            ((GUI_TransformDataType)parameterGUI).PopulateUI(_entity, (cTransform)this_param, paramName);
                            break;
                        case DataType.INTEGER:
                            parameterGUI = new GUI_NumericDataType();
                            ((GUI_NumericDataType)parameterGUI).PopulateUI_Int((cInteger)this_param, paramName);
                            break;
                        case DataType.ENUM_STRING:
                            parameterGUI = new GUI_StringVariant_AssetDropdown();
                            ((GUI_StringVariant_AssetDropdown)parameterGUI).PopulateUI((cEnumString)this_param, paramName, false); //TODO: allow type selection?
                            break;
                        case DataType.STRING:
                            parameterGUI = new GUI_StringDataType();
                            ((GUI_StringDataType)parameterGUI).PopulateUI((cString)this_param, paramName);
                            break;
                        case DataType.BOOL:
                            parameterGUI = new GUI_BoolDataType();
                            ((GUI_BoolDataType)parameterGUI).PopulateUI((cBool)this_param, paramName);
                            break;
                        case DataType.FLOAT:
                            parameterGUI = new GUI_NumericDataType();
                            ((GUI_NumericDataType)parameterGUI).PopulateUI_Float((cFloat)this_param, paramName);
                            break;
                        case DataType.VECTOR:
                            //TODO: Should add a "colour" flag to handle this nicer.
                            switch (paramName)
                            {
                                case "AMBIENT_LIGHTING_COLOUR":
                                case "COLOUR_TINT_START":
                                case "COLOUR_TINT_MID":
                                case "COLOUR_TINT_END":
                                case "COLOUR_TINT":
                                case "COLOUR_TINT_OUTER":
                                case "DEPTH_INTERSECT_COLOUR_VALUE":
                                case "DEPTH_INTERSECT_INITIAL_COLOUR":
                                case "DEPTH_INTERSECT_MIDPOINT_COLOUR":
                                case "DEPTH_INTERSECT_END_COLOUR":
                                case "DEPTH_FOG_INITIAL_COLOUR":
                                case "DEPTH_FOG_MIDPOINT_COLOUR":
                                case "DEPTH_FOG_END_COLOUR":
                                case "ColourFactor":
                                case "lens_flare_colour":
                                case "light_shaft_colour":
                                case "initial_colour":
                                case "near_colour":
                                case "far_colour":
                                case "colour":
                                case "Colour":
                                    parameterGUI = new GUI_VectorVariant_Colour();
                                    ((GUI_VectorVariant_Colour)parameterGUI).PopulateUI((cVector3)this_param, paramName);
                                    break;
                                default:
                                    parameterGUI = new GUI_VectorDataType();
                                    ((GUI_VectorDataType)parameterGUI).PopulateUI((cVector3)this_param, paramName);
                                    break;
                            }
                            break;
                        case DataType.ENUM:
                            parameterGUI = new GUI_EnumDataType();
                            ParameterData defaultData = Content.Level.Commands.Utils.CreateDefaultParameterData(Entity, Composite, paramName);
                            ((GUI_EnumDataType)parameterGUI).PopulateUI((cEnum)this_param, paramName, defaultData == null || (defaultData.dataType == DataType.ENUM && ((cEnum)defaultData).enumID == ShortGuid.Invalid));
                            break;
                        case DataType.RESOURCE:
                            parameterGUI = new GUI_ResourceDataType();
                            ((GUI_ResourceDataType)parameterGUI).PopulateUI(this, (cResource)this_param, paramName);
                            break;
                        case DataType.SPLINE:
                            parameterGUI = new GUI_SplineDataType(_entity);
                            ((GUI_SplineDataType)parameterGUI).PopulateUI((cSpline)this_param, paramName);
                            break;
                    }
                }

                parameterGUI.Parameter = _entity.parameters[i];
                parameterGUI.OnDeleted += OnDeleteParam;
                parameterGUI.TrackInstanceInfo(Composite.shortGUID, Entity.shortGUID, _entity.parameters[i].name);
                parameterGUI.Location = new Point(15, current_ui_offset);
                parameterGUI.Width = entity_params.Width - 30;
                parameterGUI.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                current_ui_offset += parameterGUI.Height + 6;
                controls.Add(parameterGUI);

#if AUTO_POPULATE_PARAMS
                //Note: we always mark variable entity parameters as "modified", because they have no defaults - they're by definition variable.
                if (_entity.variant == EntityVariant.VARIABLE || ParameterModificationTracker.IsParameterModified(Composite.shortGUID, Entity.shortGUID, _entity.parameters[i].name))
                    parameterGUI.HighlightAsModified(false);
#endif
            }

#if DO_ENTITY_PERF_CHECK
            Debug.Log("Entity Inspector", $"PARAMETER CONTROLS COMPLETED: {timer.Elapsed.TotalMilliseconds} ms");
#endif

            if (_displayingLinks)
            {
                //populate linked params OUT
                for (int i = 0; i < _entity.childLinks.Count; i++)
                {
                    GUI_Link parameterGUI = new GUI_Link(this);
                    parameterGUI.PopulateUI(_entity.childLinks[i], true);
                    parameterGUI.TrackInstanceInfo(Composite.shortGUID, Entity.shortGUID, _entity.childLinks[i].thisParamID);
                    parameterGUI.HighlightAsModified(false); //For now, marking all links as "modified", given that they likely won't be default vals
                    parameterGUI.GoToEntity += _compositeDisplay.LoadEntityAndFocusNode;
                    parameterGUI.OnLinkEdited += OnLinkEdited;
                    parameterGUI.Location = new Point(15, current_ui_offset);
                    parameterGUI.Width = entity_params.Width - 30;
                    parameterGUI.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                    current_ui_offset += parameterGUI.Height + 6;
                    controls.Add(parameterGUI);
                }
            }

#if DO_ENTITY_PERF_CHECK
            Debug.Log("Entity Inspector", $"LINK OUT CONTROLS COMPLETED: {timer.Elapsed.TotalMilliseconds} ms");
#endif

            if (this.IsDisposed || this.Disposing || entity_params == null || entity_params.IsDisposed || entity_params.Disposing)
            {
                foreach (Control ctrl in controls)
                {
                    try
                    {
                        if (ctrl != null && !ctrl.IsDisposed)
                        {
                            if (ctrl is ParameterUserControl)
                                ((ParameterUserControl)ctrl).OnDeleted -= OnDeleteParam;
                            else if (ctrl is GUI_Link)
                            {
                                GUI_Link link = (GUI_Link)ctrl;
                                link.GoToEntity -= _compositeDisplay.LoadEntityAndFocusNode;
                                link.OnLinkEdited -= OnLinkEdited;
                            }
                            ctrl.Dispose();
                        }
                    }
                    catch { }
                }
#if DO_ENTITY_PERF_CHECK
                timer.Stop();
#endif
                return;
            }

            try
            {
                entity_params.SuspendLayout();
                entity_params.Controls.AddRange(controls.ToArray());
                entity_params.ResumeLayout();
            }
            catch (Exception ex)
            {
                Debug.Log("Entity Inspector", $"Error adding controls: {ex.Message}");
                foreach (Control ctrl in controls)
                {
                    try
                    {
                        if (ctrl != null && !ctrl.IsDisposed)
                        {
                            if (ctrl is ParameterUserControl)
                                ((ParameterUserControl)ctrl).OnDeleted -= OnDeleteParam;
                            else if (ctrl is GUI_Link)
                            {
                                GUI_Link link = (GUI_Link)ctrl;
                                link.GoToEntity -= _compositeDisplay.LoadEntityAndFocusNode;
                                link.OnLinkEdited -= OnLinkEdited;
                            }
                            ctrl.Dispose();
                        }
                    }
                    catch { }
                }
                throw; 
            }

#if DO_ENTITY_PERF_CHECK
            timer.Stop();
            Debug.Log("Entity Inspector", $"ADDED CONTROLS TO WINDOW: {timer.Elapsed.TotalMilliseconds} ms");
#endif

            Singleton.OnEntityReloaded?.Invoke(_entity);
            Cursor.Current = Cursors.Default;
        }

        private void OnDeleteParam(Parameter param)
        {
            Singleton.OnEntityParameterModified?.Invoke(_entity, param, true);
            if (param?.content != null && param.name == ShortGuidUtils.Generate("position") && param.content.dataType == DataType.TRANSFORM)
                Singleton.OnEntityMoved?.Invoke(null, _entity);
            Singleton.OnParameterModified?.Invoke();
            _entity.parameters.Remove(param);
            _compositeDisplay.ReloadEntity(_entity);
        }

        private void OnLinkEdited(Entity orig, Entity linked)
        {
            Singleton.OnParameterModified?.Invoke();
            _compositeDisplay.ReloadEntity(orig);
            _compositeDisplay.ReloadEntity(linked);
        }

        private CancellationTokenSource _prevTaskToken = null;
        private Task _prevTask = null;
        private void StartBackgroundEntityLoader()
        {
            if (_prevTaskToken != null)
                _prevTaskToken.Cancel();

            _prevTaskToken = new CancellationTokenSource();
            _prevTask = Task.Run(() => BackgroundEntityLoader(_entity, this, _prevTaskToken.Token), _prevTaskToken.Token);
        }
        private void BackgroundEntityLoader(Entity ent, EntityInspector mainInst, CancellationToken ct)
        {
            bool isPointedTo = false;
            Composite zoneComp = null;
            FunctionEntity zoneEnt = null;
            Parallel.For(0, 2, (i) =>
            {
                switch (i)
                {
                    case 0:
                        isPointedTo = mainInst.CompositeDisplay.AnyFlowgraphsContainEntity(ent);
                        if (!isPointedTo)
                            isPointedTo = mainInst.Content.EditorUtils.IsEntityReferencedExternally(ent, ct);
                        break;
                    case 1:
                        mainInst.Content.EditorUtils.TryFindZoneForEntity(ent, mainInst.Composite, out zoneComp, out zoneEnt, ct);
                        break;
                }
            });
            mainInst.ThreadedEntityUIUpdate(ent, isPointedTo, zoneComp, zoneEnt);
        }
        private Composite zoneCompositeForSelectedEntity = null;
        private FunctionEntity zoneEntityForSelectedEntity = null;
        public void ThreadedEntityUIUpdate(Entity ent, bool isPointedTo, Composite zoneComp, FunctionEntity zoneEnt)
        {
            //TODO: we have an issue here where this can be called after the entitydisplay object has been disposed

            try
            {
                showOverridesAndProxies.Invoke(new Action(() => { showOverridesAndProxies.Enabled = isPointedTo; }));
                zoneCompositeForSelectedEntity = zoneComp;
                zoneEntityForSelectedEntity = zoneEnt;
                string zoneText = "Zone";
                if (zoneEnt != null)
                {
                    Parameter name = zoneEnt.GetParameter("name");
                    if (name != null) zoneText += " (" + ((cString)name.content).value + ")";
                }
                goToZone.Invoke(new Action(() => { goToZone.Enabled = zoneEnt != null; goToZone.Text = zoneText; }));
            }
            catch { }
        }

        private void contextMenuStrip2_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            toolStripMenuItem1.Enabled = _entity != null;
            createLinkToolStripMenuItem.Enabled = _entity != null;
            createLinkToolStripMenuItem.Visible = DisplayingLinks;
        }

        ModifyParameters add_parameter;
        private void ModifyParameters_Click(object sender, EventArgs e)
        {
            if (add_parameter != null)
            {
                add_parameter.OnSaved -= Reload;
                add_parameter.Close();
            }
            
            add_parameter = new ModifyParameters(this);
            add_parameter.Show();
            add_parameter.OnSaved += Reload;
        }
        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ModifyParameters_Click(null, null);
        }

        /* Add a new link out */
        AddOrEditLink _linkDialog = null;
        private void addLinkOut_Click(object sender, EventArgs e)
        {
            if (_linkDialog != null)
                _linkDialog.Close();

            _linkDialog = new AddOrEditLink(this);
            _linkDialog.Show();
            _linkDialog.OnSaved += Reload;
        }
        private void createLinkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            addLinkOut_Click(null, null);
        }

        EditMVR _mvrDialog = null;
        private void editEntityMovers_Click(object sender, EventArgs e)
        {
            if (_mvrDialog != null)
                _mvrDialog.Close();

            _mvrDialog = new EditMVR(this);
            _mvrDialog.Show();
        }

        ShowCrossRefs _crossRefsDialog = null;
        private void showOverridesAndProxies_Click(object sender, EventArgs e)
        {
            if (_crossRefsDialog != null)
                _crossRefsDialog.Close();

            _crossRefsDialog = new ShowCrossRefs(Entity);
            _crossRefsDialog.Show();
            _crossRefsDialog.OnEntitySelected += _compositeDisplay.CommandsDisplay.LoadCompositeAndEntity;
            _crossRefsDialog.OnFlowgraphSelected += _compositeDisplay.SelectEntityOnFlowgraph;
        }

        AddOrEditResource _resourceDialog = null;
        private void editEntityResources_Click(object sender, EventArgs e)
        {
            if (_resourceDialog != null)
                _resourceDialog.Close();

            _resourceDialog = new AddOrEditResource(this);
            _resourceDialog.Show();
        }

        private void goToZone_Click(object sender, EventArgs e)
        {
            CompositeDisplay display = _compositeDisplay;
            if (Composite != zoneCompositeForSelectedEntity)
                display = _compositeDisplay.CommandsDisplay.LoadComposite(zoneCompositeForSelectedEntity);

            display.LoadEntity(zoneEntityForSelectedEntity, true);
        }

        ShowCompositeInstanceOverrides _instanceOverridesDialog = null;
        CAGEAnimationEditor _cageAnimDialog = null;
        TriggerSequenceEditor _triggerSeqDialog = null;
        CharacterEditor _charEditorDialog = null;
        private void editFunction_Click(object sender, EventArgs e)
        {
            if (Entity.variant != EntityVariant.FUNCTION) return;
            if (_entityCompositePtr != null)
            {
                //Composite Instance
                if (_instanceOverridesDialog != null)
                    _instanceOverridesDialog.Close();
                _instanceOverridesDialog = new ShowCompositeInstanceOverrides(this);
                _instanceOverridesDialog.Show();
            }
            else
            {
                //Function Entity
                switch (((FunctionEntity)Entity).function.AsFunctionType)
                {
                    case FunctionType.CAGEAnimation:
                        Singleton.OnCAGEAnimationEditorOpened?.Invoke();
                        if (_cageAnimDialog != null)
                            _cageAnimDialog.Close();
                        _cageAnimDialog = new CAGEAnimationEditor(this);
                        _cageAnimDialog.Show();
                        _cageAnimDialog.OnSaved += CAGEAnimationEditor_OnSaved;
                        break;
                    case FunctionType.TriggerSequence:
                        if (_triggerSeqDialog != null)
                            _triggerSeqDialog.Close();
                        _triggerSeqDialog = new TriggerSequenceEditor(this);
                        _triggerSeqDialog.Show();
                        break;
                    case FunctionType.Character:
                        //TODO: I think this is only valid for entities with "custom_character_type" set - but working that out requires a complex parse of connected entities. So ignoring for now.
                        if (_charEditorDialog != null)
                            _charEditorDialog.Close();
                        _charEditorDialog = new CharacterEditor(this);
                        _charEditorDialog.Show();
                        break;
                }
            }
        }
        private void CAGEAnimationEditor_OnSaved(CAGEAnimation newEntity)
        {
            CAGEAnimation entity = (CAGEAnimation)Entity;
            entity.connections = newEntity.connections;
            entity.events = newEntity.events;
            entity.animations = newEntity.animations;
            entity.parameters = newEntity.parameters;
            Reload();
        }

        private void jumpToComposite_Click(object sender, EventArgs e)
        {
            switch (Entity.variant)
            {
                case EntityVariant.PROXY:
                    //Proxies forward directly to the entity they point to, breaking us out of the hierarchy.
                    (Composite composite, Entity entity) = Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveProxy((ProxyEntity)_entity));
                    if (MessageBox.Show("Jumping to a proxy will break you out of your composite.\nAre you sure?", "About to follow proxy...", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        _compositeDisplay.CommandsDisplay.LoadCompositeAndEntity(composite, entity);
                    break;
                case EntityVariant.FUNCTION:
                    //Composite instances take us a step down the hierarchy.
                    _compositeDisplay.LoadChild(Content.Level.Commands.GetComposite(selected_entity_type_description.Text), Entity);
                    return;
                case EntityVariant.ALIAS:
                    //Aliases take us (potentially) multiple steps down the hierarchy.
                    ShortGuid[] aliasPath = ((AliasEntity)Entity).alias.path;
                    for (int i = 0; i < aliasPath.Length - 2; i++)
                        _compositeDisplay.LoadChild(Content.Level.Commands.GetComposite(((FunctionEntity)Composite.GetEntityByID(aliasPath[i])).function), Composite.GetEntityByID(aliasPath[i]));
                    _compositeDisplay.LoadEntity(Composite.GetEntityByID(aliasPath[aliasPath.Length - 2]), true);
                    return;
            }

        }

        private void deleteEntity_Click(object sender, EventArgs e)
        {
            _compositeDisplay.DeleteEntity(Entity);
        }

        private void duplicateEntity_Click(object sender, EventArgs e)
        {
            _compositeDisplay.DuplicateEntity(Entity);
        }

        RenameEntity _renameDialog = null;
        private void renameEntity_Click(object sender, EventArgs e)
        {
            if (_renameDialog != null)
                _renameDialog.Close();

            _renameDialog = new RenameEntity(this.Entity, this.Composite);
            _renameDialog.Show();
        }

        /* Context menu close entity */
        private void closeAll_Click(object sender, EventArgs e)
        {
            _compositeDisplay.CloseAllChildTabs();
        }
        private void closeSelected_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void closeAllBut_Click(object sender, EventArgs e)
        {
            _compositeDisplay.CloseAllChildTabsExcept(Entity);
        }

        /* Apply defaults */
        private void addUnsetParametersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Add only the parameters not already set
            bool hasDeleteMe = Entity.GetParameter("delete_me") != null;
            Content.Level.Commands.Utils.AddAllDefaultParameters(Entity, Composite, false);
            if (!hasDeleteMe) Entity.RemoveParameter("delete_me");
            _compositeDisplay.ReloadEntity(Entity);

            foreach (Parameter param in Entity.parameters)
            {
                if (param?.content != null && param.name == ShortGuidUtils.Generate("position") && param.content.dataType == DataType.TRANSFORM)
                    Singleton.OnEntityMoved?.Invoke((cTransform)param.content, _entity);
                if (param?.content != null && param.name == ShortGuidUtils.Generate("resource") && param.content.dataType == DataType.RESOURCE)
                    Singleton.OnResourceModified?.Invoke();
            }
            Singleton.OnParameterModified?.Invoke();
        }
        private void applyAllDefaultsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Add all defaults, overwriting the ones already set
            Entity.parameters.Clear();
            Content.Level.Commands.Utils.AddAllDefaultParameters(Entity, Composite);
            Entity.RemoveParameter("delete_me");
            _compositeDisplay.ReloadEntity(Entity);

            foreach (Parameter param in Entity.parameters)
            {
                if (param?.content != null && param.name == ShortGuidUtils.Generate("position") && param.content.dataType == DataType.TRANSFORM)
                    Singleton.OnEntityMoved?.Invoke((cTransform)param.content, _entity);
                if (param?.content != null && param.name == ShortGuidUtils.Generate("resource") && param.content.dataType == DataType.RESOURCE)
                    Singleton.OnResourceModified?.Invoke();
            }
            Singleton.OnParameterModified?.Invoke();
        }
    }
}
