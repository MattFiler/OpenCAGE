using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Enums;
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

        //Multi-selection state: when more than one entity is selected, the grid edits them all at once
        private List<Entity> _multiEntities = null;
        public bool IsMultiEditing => _multiEntities != null && _multiEntities.Count > 1;

        //Parameter grid UI (replaces the old stacked parameter UserControls)
        private SplitContainer _paramSplit;
        private ParameterGridPanel _gridPanel;

        public bool Populated => _entity != null || IsMultiEditing;

        public LevelContent Content => _compositeDisplay?.Content;

        public Entity Entity => _entity;
        public Composite Composite => _compositeDisplay?.Composite;

        private bool _displayingLinks = true;
        public bool DisplayingLinks => _displayingLinks;

        public EntityInspector()
        {
            this.FormClosing += (s, e) => { DepopulateUI(); };
            this.FormClosed += EntityDisplay_FormClosed;

            InitializeComponent();

            //Restructure the parameter area: parameter grid on top, the old scrolling panel below (links only now).
            //The height is managed by LayoutParamArea so the area can reclaim the link bar's space when it's hidden.
            _paramSplit = new SplitContainer()
            {
                Orientation = Orientation.Horizontal,
                Bounds = entity_params.Bounds,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Panel1MinSize = 80,
                Panel2MinSize = 60
            };
            entityParamGroup.Controls.Remove(entity_params);
            _paramSplit.Panel2.Controls.Add(entity_params);
            entity_params.Dock = DockStyle.Fill;
            _gridPanel = new ParameterGridPanel() { Dock = DockStyle.Fill };
            _paramSplit.Panel1.Controls.Add(_gridPanel);
            entityParamGroup.Controls.Add(_paramSplit);
            try { _paramSplit.SplitterDistance = (int)(_paramSplit.Height * 0.6f); } catch { }
            _paramSplit.Panel2Collapsed = true;
            entityParamGroup.Resize += (s, e) => LayoutParamArea();

            Singleton.OnEntityAddPending += OnEntityAddPending;
            Singleton.OnEntityAdded += OnEntityAdded;
            Singleton.OnEntityRenamed += OnEntityRenamed;
            Singleton.OnCompositeRenamed += OnCompositeRenamed;

            Reload();

            this.CloseButton = false;
            this.CloseButtonVisible = false;
            this.AllowEndUserDocking = false;
        }

        public void AttachCompositeDisplay(CompositeDisplay compositeDisplay)
        {
            _compositeDisplay = compositeDisplay;
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

        private bool IsAffectedByEntityRename(Entity renamedEntity)
        {
            if (_entity == null || renamedEntity == null)
                return false;

            if (_entity.shortGUID == renamedEntity.shortGUID)
                return true;

            switch (_entity.variant)
            {
                case EntityVariant.ALIAS:
                    return ((AliasEntity)_entity).alias.path.Contains(renamedEntity.shortGUID);
                case EntityVariant.PROXY:
                    return ((ProxyEntity)_entity).proxy.path.Contains(renamedEntity.shortGUID);
                default:
                    return false;
            }
        }

        private bool IsAffectedByCompositeRename(Composite composite)
        {
            if (_entity == null || composite == null)
                return false;

            if (_entity.variant == EntityVariant.FUNCTION
                && ((FunctionEntity)_entity).function == composite.shortGUID)
                return true;

            switch (_entity.variant)
            {
                case EntityVariant.ALIAS:
                {
                    (Composite comp, Entity ent) = Content.Level.Commands.Utils.GetResolvedTarget(
                        Content.Level.Commands.Utils.ResolveAlias((AliasEntity)_entity, Composite));
                    return comp?.shortGUID == composite.shortGUID;
                }
                case EntityVariant.PROXY:
                {
                    (Composite comp, Entity ent) = Content.Level.Commands.Utils.GetResolvedTarget(
                        Content.Level.Commands.Utils.ResolveProxy((ProxyEntity)_entity));
                    return comp?.shortGUID == composite.shortGUID;
                }
                default:
                    return false;
            }
        }

        private void OnEntityRenamed(Entity entity, string name)
        {
            if (!Populated || !IsAffectedByEntityRename(entity))
                return;

            Reload();
        }
        /* Recompute the "fed by flowgraph" parameter highlights (called live as connections change) */
        public void RefreshParameterHighlights()
        {
            if (!Populated || IsMultiEditing)
                return;
            _gridPanel?.RefreshStatuses();
        }

        public void ApplyTransformFromExternal(ShortGuid paramName, cTransform transform)
        {
            if (!Populated || transform == null)
                return;

            //The grid reads values live from the entity data, so a refresh picks up the new transform
            _gridPanel?.RefreshValues();
        }

        private void OnCompositeRenamed(Composite composite, string name)
        {
            if (!Populated || !IsAffectedByCompositeRename(composite))
                return;

            Reload();
        }

        /* Populate the inspector with multiple selected entities (multi-edit mode) */
        public void PopulateUI(List<Entity> entities, bool displayLinks)
        {
            if (entities == null || entities.Count == 0)
            {
                ClearSelectedEntity();
                return;
            }

            List<Entity> distinct = new List<Entity>();
            foreach (Entity entity in entities)
            {
                if (entity == null) continue;
                if (distinct.FirstOrDefault(o => o.shortGUID == entity.shortGUID) == null)
                    distinct.Add(entity);
            }
            if (distinct.Count == 0)
            {
                ClearSelectedEntity();
                return;
            }
            if (distinct.Count == 1)
            {
                PopulateUI(distinct[0], displayLinks);
                return;
            }

            if (IsDisposed || Disposing)
                return;

            if (!Visible || DockState == DockState.Hidden || DockState == DockState.Float)
            {
                if (Singleton.Editor?.DockPanel != null)
                    Show(Singleton.Editor.DockPanel, DockState.DockRight);
                else
                    Show();
            }

            _entity = null;
            _entityCompositePtr = null;
            _multiEntities = distinct;
            this.Icon = Resources.d_ScriptableObject_Icon_braces_only;

            Reload(false);

            //Viewer-originated selection/paste must not steal Win32 focus from the embedded viewer
            Control list = Singleton.Editor?.CompositeDisplay?.EntityListPanel;
            if (!ViewerSelectionSync.IsApplyingViewerSelection
                && (list == null || !list.ContainsFocus))
                this.Activate();
        }

        public void PopulateUI(Entity entity, bool displayLinks)
        {
            if (entity == null)
            {
                ClearSelectedEntity();
                return;
            }

            bool wasMultiEditing = IsMultiEditing;
            _multiEntities = null;
            if (!wasMultiEditing && Populated && _entity != null && _entity.shortGUID == entity.shortGUID)
                return;

            if (IsDisposed || Disposing)
                return;

            if (!Visible || DockState == DockState.Hidden || DockState == DockState.Float)
            {
                if (Singleton.Editor?.DockPanel != null)
                    Show(Singleton.Editor.DockPanel, DockState.DockRight);
                else
                    Show();
            }
            
            _entity = entity;
            _entityCompositePtr = _entity.variant == EntityVariant.FUNCTION && Content?.Level?.Commands != null
                ? Content.Level.Commands.GetComposite(((FunctionEntity)_entity).function)
                : null;

            switch (_entity.variant)
            {
                case EntityVariant.VARIABLE:
                    this.Icon = Resources.AnimatorController_Icon;
                    break;
                case EntityVariant.FUNCTION:
                    if (Content?.Level?.Commands == null || Content.Level.Commands.GetComposite(((FunctionEntity)_entity).function) == null)
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

            //Viewer-originated selection/paste must not steal Win32 focus from the embedded viewer
            Control list = Singleton.Editor?.CompositeDisplay?.EntityListPanel;
            if (!ViewerSelectionSync.IsApplyingViewerSelection
                && (list == null || !list.ContainsFocus))
                this.Activate();
        }

        public void DepopulateUI()
        {
            this.Hide();
            EntityDisplay_FormClosed(null, null);
        }

        public void ClearSelectedEntity()
        {
            if (_entity == null && !IsMultiEditing)
            {
                Reload(_displayingLinks);
                return;
            }

            _entity = null;
            _entityCompositePtr = null;
            _multiEntities = null;
            Reload(_displayingLinks);
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
            _gridPanel?.ClearEntities();

            _entity = null;
            _entityCompositePtr = null;
            _multiEntities = null;

            imageList1.Images.Clear();
            imageList1.Dispose();
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

            //UI defaults - TODO: just set this in the designer.
            this.Text = "Entity Inspector";
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
            showOverridesAndProxies.Enabled = false;
            goToZone.Enabled = false;
            hierarchyDisplay.Visible = false;

            //NOTE: These visibility options should be mirrored in EntityListContextMenu_Opening in EntityList
            renameEntity.Enabled = _entity != null && _entity.variant != EntityVariant.ALIAS && _entity.variant != EntityVariant.VARIABLE; //TODO: we should support variable renaming, but doing that requires managing renaming all links/params (including node links)
            duplicateEntity.Enabled = _entity != null && _entity.variant != EntityVariant.ALIAS && _entity.variant != EntityVariant.VARIABLE; //This works, but why would you ever want to?
            deleteEntity.Enabled = _entity != null;

            //Links (and the Create Link bar) are only for composites without flowgraph support -
            //in flowgraph mode links are made by connecting pins on the graph instead
            addLinkOut.Enabled = _entity != null;
            tableLayoutPanel2.Visible = _displayingLinks;
            LayoutParamArea();

            if (_entity == null)
            {
                if (IsMultiEditing)
                {
                    ReloadMulti();
                }
                else
                {
                    _gridPanel.ClearEntities();
                    _paramSplit.Panel2Collapsed = true;
                }
#if DO_ENTITY_PERF_CHECK
                timer.Stop();
#endif
                return;
            }

            // Strip unused FLOAT 0 delay params that mirror T_STRING event pins (forward + reverse_)
            if (_entity is CAGEAnimation cageAnimForCleanup)
                CleanupCageAnimationZeroDelayStringParams(cageAnimForCleanup);

            if (Content?.Level?.Commands?.Utils == null || Composite == null)
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
                        description = _entityCompositePtr.name;
                        //editFunction.Enabled = true;
                        //editFunction.Text = "Alias Overrides"; //TODO: show count?
                    }

                    //Function Entity
                    else
                    {
                        jumpToComposite.Visible = false;

                        FunctionType function = ((FunctionEntity)_entity).function.AsFunctionType;
                        description = function.ToString();
                        editFunction.Enabled = function == FunctionType.CAGEAnimation || function == FunctionType.TriggerSequence || function == FunctionType.Character;

                        bool supportsResources = EntitySupportsResources((FunctionEntity)_entity, function);
                        editEntityResources.Enabled = supportsResources && Content.Level.Models != null;
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
                    hierarchyDisplay.Text = Content.Level.Commands.Utils.GetResolvedAsString(resolvedHierarchy, SettingsManager.GetBool(Settings.ShowShortGuids));
                    toolTip1.SetToolTip(hierarchyDisplay, hierarchyDisplay.Text);
                    jumpToComposite.Visible = true;
                    if (comp == null || ent == null)
                    {
                        selected_entity_name.Text = (_entity.variant == EntityVariant.PROXY ? "Proxy" : "Alias") + " (unresolved target)";
                        description = "Target composite/entity could not be resolved";
                    }
                    else
                    {
                        selected_entity_name.Text = (_entity.variant == EntityVariant.PROXY ? "Proxy to " : "Alias of ") + Content.Level.Commands.Utils.GetEntityName(comp, ent);

                        //Proxies to TriggerSequences carry their own trigger data - allow editing it
                        if (_entity.variant == EntityVariant.PROXY
                            && ent is FunctionEntity proxyTargetFunction
                            && proxyTargetFunction.function.IsFunctionType
                            && proxyTargetFunction.function.AsFunctionType == FunctionType.TriggerSequence)
                        {
                            editFunction.Enabled = true;
                        }
                    }
                    break;
                default:
                    selected_entity_name.Text = Content.Level.Commands.Utils.GetEntityName(Composite.shortGUID, _entity.shortGUID);
                    break;
            }
            selected_entity_type_description.Text = description;
            this.Text = selected_entity_name.Text;

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
            ApplyDefaultsForInspection(_entity);
#if DO_ENTITY_PERF_CHECK
            Debug.Log("Entity Inspector", $"DEFAULTS APPLIED: {timer.Elapsed.TotalMilliseconds} ms");
#endif
#endif

            //populate parameters via the grid (visibility filtering, enum-string fixups and special
            //types are handled by EntityParameterProxy)
            _gridPanel.ShowEntities(this, new List<Entity>() { _entity }, Composite, Content, FilterPinParameters());
            _paramSplit.Panel2Collapsed = !_displayingLinks;

#if DO_ENTITY_PERF_CHECK
            Debug.Log("Entity Inspector", $"PARAMETER GRID COMPLETED: {timer.Elapsed.TotalMilliseconds} ms");
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

        /* Size the parameter area to the space above the Create Link bar (or the full group when it's hidden) */
        private void LayoutParamArea()
        {
            if (_paramSplit == null || _paramSplit.IsDisposed || entityParamGroup == null)
                return;

            int bottom = tableLayoutPanel2.Visible
                ? tableLayoutPanel2.Top - 6
                : entityParamGroup.ClientSize.Height - 8;
            int height = bottom - _paramSplit.Top;
            if (height < _paramSplit.Panel1MinSize)
                height = _paramSplit.Panel1MinSize;

            if (_paramSplit.Height != height)
                _paramSplit.Height = height;
        }

        /* Should pin-delay/output params be hidden from the parameter list? */
        private bool FilterPinParameters()
        {
            return CompositeDisplay.SupportsFlowgraphs;
        }

#if AUTO_POPULATE_PARAMS
        /* Apply all default parameters to the entity (once) so the grid shows everything available */
        private void ApplyDefaultsForInspection(Entity entity)
        {
            EnsureDefaultsApplied(entity, Composite, Content);
        }

        /* Static variant so the parameter grid can also apply defaults (e.g. to an alias's resolved target) */
        public static void EnsureDefaultsApplied(Entity entity, Composite composite, LevelContent content)
        {
            if (entity == null || composite == null || content?.Level?.Commands?.Utils == null)
                return;
            if (entity.variant != EntityVariant.FUNCTION && entity.variant != EntityVariant.PROXY)
                return;
            if (ParameterModificationTracker.IsDefaultsApplied(composite.shortGUID, entity.shortGUID))
                return;

            //NOTE: INPUT_PIN excluded - pin delay values are edited via flowgraph pins, not shown as parameters
            bool hasDeleteMe = entity.GetParameter("delete_me") != null;
            content.Level.Commands.Utils.AddAllDefaultParameters(entity, composite, false, ParameterVariant.STATE_PARAMETER | ParameterVariant.PARAMETER);
            if (!hasDeleteMe) entity.RemoveParameter("delete_me");
            ParameterModificationTracker.SetDefaultsApplied(composite.shortGUID, entity.shortGUID);
        }
#endif

        /* Populate the inspector for a multi-selection: tabs per entity type, per-entity buttons disabled */
        private void ReloadMulti()
        {
            this.Icon = Resources.d_ScriptableObject_Icon_braces_only;

            int count = _multiEntities.Count;
            entityInfoGroup.Text = "Multi-Selection Info";
            entityParamGroup.Text = "Multi-Selection Parameters";
            selected_entity_name.Text = count + " entities selected";
            selected_entity_type_description.Text = SummariseMultiSelectionTypes();
            this.Text = "Entity Inspector (" + count + ")";

            if (Content?.Level?.Commands?.Utils == null || Composite == null)
                return;

            Cursor.Current = Cursors.WaitCursor;
#if AUTO_POPULATE_PARAMS
            foreach (Entity entity in _multiEntities)
                ApplyDefaultsForInspection(entity);
#endif
            _gridPanel.ShowEntities(this, _multiEntities, Composite, Content, FilterPinParameters());
            _paramSplit.Panel2Collapsed = true;
            Cursor.Current = Cursors.Default;
        }

        private string SummariseMultiSelectionTypes()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            List<string> order = new List<string>();
            foreach (Entity entity in _multiEntities)
            {
                string label;
                switch (entity.variant)
                {
                    case EntityVariant.FUNCTION:
                        FunctionEntity function = (FunctionEntity)entity;
                        if (function.function.IsFunctionType)
                            label = function.function.AsFunctionType.ToString();
                        else
                        {
                            Composite pointedComposite = Content?.Level?.Commands?.GetComposite(function.function);
                            label = pointedComposite != null ? pointedComposite.name : "Composite Instance";
                        }
                        break;
                    case EntityVariant.VARIABLE:
                        label = "Variable";
                        break;
                    case EntityVariant.PROXY:
                        label = "Proxy";
                        break;
                    case EntityVariant.ALIAS:
                        label = "Alias";
                        break;
                    default:
                        label = "Entity";
                        break;
                }
                if (!counts.ContainsKey(label))
                {
                    counts.Add(label, 0);
                    order.Add(label);
                }
                counts[label]++;
            }

            StringBuilder summary = new StringBuilder();
            foreach (string label in order)
            {
                if (summary.Length != 0)
                    summary.Append(", ");
                summary.Append(counts[label]).Append("x ").Append(label);
            }
            return summary.ToString();
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
            createLinkToolStripMenuItem.Enabled = _entity != null;
            createLinkToolStripMenuItem.Visible = DisplayingLinks;
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

        ShowCrossRefs _crossRefsDialog = null;
        private void showOverridesAndProxies_Click(object sender, EventArgs e)
        {
            if (_crossRefsDialog != null)
                _crossRefsDialog.Close();

            _crossRefsDialog = new ShowCrossRefs(Entity);
            _crossRefsDialog.Show();
            _crossRefsDialog.OnEntitySelected += _compositeDisplay.CompositeBrowser.LoadCompositeAndEntity;
            _crossRefsDialog.OnFlowgraphSelected += _compositeDisplay.SelectEntityOnFlowgraph;
        }

        AddOrEditResource _resourceDialog = null;
        private void editEntityResources_Click(object sender, EventArgs e)
        {
            if (_resourceDialog != null)
                _resourceDialog.Close();

            if (!(_entity is FunctionEntity functionEntity))
                return;

            FunctionType function = functionEntity.function.AsFunctionType;
            if (FunctionHasResourceParameter(function))
            {
                cResource resourceParam = EnsureResourceParameter(functionEntity);
                _resourceDialog = new AddOrEditResource(this, resourceParam, "resource");
            }
            else
            {
                _resourceDialog = new AddOrEditResource(this);
            }
            _resourceDialog.Show();
        }

        /// <summary>
        /// True when this function declares an internal <c>resource</c> parameter (CathodeEntities),
        /// uses <see cref="FunctionEntity.resources"/> (e.g. PhysicsSystem), or already has entity-level resources.
        /// </summary>
        bool EntitySupportsResources(FunctionEntity entity, FunctionType function)
        {
            // Marker-only resource types are auto-managed on load/defaults/instancing — nothing to edit.
            if (FunctionIsMarkerResourceOnly(function))
                return false;

            if (FunctionHasResourceParameter(function))
                return true;
            if (FunctionUsesEntityResourceList(function))
                return true;
            if (entity.resources == null)
                return false;
            for (int i = 0; i < entity.resources.Count; i++)
            {
                if (entity.resources[i] != null && !IsMarkerOnlyResourceType(entity.resources[i].resource_type))
                    return true;
            }
            return false;
        }

        bool FunctionHasResourceParameter(FunctionType function)
        {
            if (Content?.Level?.Commands?.Utils == null)
                return false;

            List<(ShortGuid, ParameterVariant, DataType)> parameters = Content.Level.Commands.Utils.GetAllParameters(function);
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].Item1 == ShortGuids.resource
                    && parameters[i].Item2 == ParameterVariant.INTERNAL
                    && parameters[i].Item3 == DataType.RESOURCE)
                    return true;
            }
            return false;
        }

        static bool FunctionUsesEntityResourceList(FunctionType function)
        {
            // PhysicsSystem stores DYNAMIC_PHYSICS_SYSTEM on FunctionEntity.resources (not a resource param).
            return function == FunctionType.PhysicsSystem;
        }

        /// <summary>
        /// Function types whose only Commands resource is a marker with no editable payload.
        /// </summary>
        static bool FunctionIsMarkerResourceOnly(FunctionType function)
        {
            switch (function)
            {
                case FunctionType.ExclusiveMaster:
                case FunctionType.NavMeshBarrier:
                case FunctionType.TRAV_1ShotClimbUnder:
                case FunctionType.TRAV_1ShotFloorVentEntrance:
                case FunctionType.TRAV_1ShotFloorVentExit:
                case FunctionType.TRAV_1ShotLeap:
                case FunctionType.TRAV_1ShotSpline:
                case FunctionType.TRAV_1ShotVentEntrance:
                case FunctionType.TRAV_1ShotVentExit:
                case FunctionType.TRAV_ContinuousBalanceBeam:
                case FunctionType.TRAV_ContinuousCinematicSidle:
                case FunctionType.TRAV_ContinuousClimbingWall:
                case FunctionType.TRAV_ContinuousLadder:
                case FunctionType.TRAV_ContinuousLedge:
                case FunctionType.TRAV_ContinuousPipe:
                case FunctionType.TRAV_ContinuousTightGap:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Marker resource types written for Commands/RESOURCES round-trip but with no editable payload.
        /// </summary>
        internal static bool IsMarkerOnlyResourceType(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.TRAVERSAL_SEGMENT:
                case ResourceType.NAV_MESH_BARRIER_RESOURCE:
                case ResourceType.EXCLUSIVE_MASTER_STATE_RESOURCE:
                    return true;
                default:
                    return false;
            }
        }

        static cResource EnsureResourceParameter(FunctionEntity entity)
        {
            Parameter param = entity.GetParameter(ShortGuids.resource);
            if (param == null)
            {
                param = new Parameter(ShortGuids.resource, new cResource(entity.shortGUID), ParameterVariant.INTERNAL);
                entity.parameters.Add(param);
            }
            else if (!(param.content is cResource))
            {
                param.content = new cResource(entity.shortGUID);
                param.variant = ParameterVariant.INTERNAL;
            }
            return (cResource)param.content;
        }

        private void goToZone_Click(object sender, EventArgs e)
        {
            CompositeDisplay display = _compositeDisplay;
            if (Composite != zoneCompositeForSelectedEntity)
                display = _compositeDisplay.CompositeBrowser.LoadComposite(zoneCompositeForSelectedEntity);

            display.LoadEntity(zoneEntityForSelectedEntity, true);
        }

        ShowCompositeInstanceOverrides _instanceOverridesDialog = null;
        CAGEAnimationEditor _cageAnimDialog = null;
        TriggerSequenceEditor _triggerSeqDialog = null;
        CharacterEditor _charEditorDialog = null;
        private void editFunction_Click(object sender, EventArgs e)
        {
            //Proxy to a TriggerSequence: edit the trigger data carried on the proxy itself
            if (Entity.variant == EntityVariant.PROXY)
            {
                if (_triggerSeqDialog != null)
                    _triggerSeqDialog.Close();
                _triggerSeqDialog = new TriggerSequenceEditor(this);
                _triggerSeqDialog.Show();
                return;
            }

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
            // Always write back to the original CAGEAnimation by ID — the inspector may have
            // navigated to a different entity (e.g. via a T_GUID event link) while the editor stayed open.
            CAGEAnimation entity = Composite?.GetEntityByID(newEntity.shortGUID) as CAGEAnimation;
            if (entity == null)
                entity = Entity as CAGEAnimation;
            if (entity == null)
                return;

            entity.connections = newEntity.connections;
            entity.eventTracks = newEntity.eventTracks;
            entity.floatTracks = newEntity.floatTracks;
            entity.parameters = newEntity.parameters;
            Reload();
        }

        private void jumpToComposite_Click(object sender, EventArgs e)
        {
            _compositeDisplay.StepIntoEntity(Entity);
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

        /// <summary>
        /// Remove FLOAT parameters at 0.0 that exist only as unused pin-delay slots for T_STRING
        /// event names (forward and reverse_). Non-zero delays are kept.
        /// </summary>
        private static void CleanupCageAnimationZeroDelayStringParams(CAGEAnimation anim)
        {
            if (anim?.eventTracks == null || anim.parameters == null) return;

            HashSet<ShortGuid> stringEventPins = new HashSet<ShortGuid>();
            for (int t = 0; t < anim.eventTracks.Count; t++)
            {
                CAGEAnimation.EventTrack track = anim.eventTracks[t];
                if (track?.keyframes == null) continue;
                for (int k = 0; k < track.keyframes.Count; k++)
                {
                    CAGEAnimation.EventTrack.Keyframe key = track.keyframes[k];
                    if (key.track_type != ANIM_TRACK_TYPE.T_STRING) continue;
                    stringEventPins.Add(key.forward);
                    stringEventPins.Add(key.reverse);
                }
            }
            if (stringEventPins.Count == 0) return;

            List<Parameter> toRemove = new List<Parameter>();
            for (int i = 0; i < anim.parameters.Count; i++)
            {
                Parameter param = anim.parameters[i];
                if (param == null || !stringEventPins.Contains(param.name)) continue;
                cFloat asFloat = param.content as cFloat;
                if (asFloat == null) continue;
                if (Math.Abs(asFloat.value) > 0.00001f) continue;
                toRemove.Add(param);
            }

            for (int i = 0; i < toRemove.Count; i++)
                anim.RemoveParameter(toRemove[i]);
        }
    }
}
