using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE;
using OpenCAGE.Scripts;
using OpenCAGE.Undo;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp;

namespace OpenCAGE
{
    //See crash logs: loads of crashes here :(

    public partial class ShowCrossRefs : BaseWindow
    {
        public Action<Composite, Entity> OnEntitySelected;
        public Action<string, Entity> OnFlowgraphSelected;

        private CurrentDisplay _currentDisplay = CurrentDisplay.FLOWGRAPHS;
        private SynchronizedCollection<EntityRef>[] _entityRefs;

        private Entity _entity;
        private readonly Composite _entityComposite;
        private readonly EntitySearchScopeController _scopeController;

        public ShowCrossRefs(Entity entity, bool openOnAliases = false) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _openOnAliases = openOnAliases;
            _entity = entity;
            //The entity was picked in the open composite - only its name needs this, for the undo labels
            Composite open = Singleton.Editor?.CompositeDisplay?.Composite;
            _entityComposite = open != null && entity != null && open.GetEntityByID(entity.shortGUID) == entity ? open : null;
            InitializeComponent();

            bool hasID = entityList.Columns.ContainsKey("ID");
            bool showID = SettingsManager.GetBool(Settings.ShowShortGuids);
            if (showID && !hasID)
                entityList.Columns.Add(new ColumnHeader() { Name = "ID", Text = "ID", Width = 100 });
            else if (!showID && hasID)
                entityList.Columns.RemoveByKey("ID");

            entityList.MouseDown += List_MouseDown;
            flowgraphList.MouseDown += List_MouseDown;

            _scopeController = new EntitySearchScopeController(Settings.CrossRefSearchScope, GlobalEntitySearchScope.CurrentCompositeAndNested);
            _scopeController.BindSettingsButton(scopeSettingsBtn);
            _scopeController.AddScopeChangedHandler(OnScopeChanged);
            UndoStack.Current.Changed += OnUndoChanged;
            this.FormClosing += (s, e) =>
            {
                UndoStack.Current.Changed -= OnUndoChanged;
                _scopeController.RemoveScopeChangedHandler(OnScopeChanged);
                _scopeController.UnsubscribeSettings();
            };

            RebuildEntityRefs();
            UpdateTitle();

            UpdateUI(_openOnAliases ? CurrentDisplay.ALIASES : CurrentDisplay.FLOWGRAPHS);
        }
        private readonly bool _openOnAliases;

        private void UpdateTitle()
        {
            this.Text = "Find References In " + _scopeController.Scope.ToDisplayName();
        }

        private void RebuildEntityRefs()
        {
            HashSet<ShortGuid> scopedGuids = GlobalEntitySearchHelper.GetScopedCompositeGuids(Content, _scopeController.Scope);

            int displayTypes = Enum.GetValues(typeof(CurrentDisplay)).Length;
            _entityRefs = new SynchronizedCollection<EntityRef>[displayTypes];
            Parallel.For(0, displayTypes, (i) =>
            {
                _entityRefs[i] = GetEntityRefs((CurrentDisplay)i, scopedGuids);
            });

            showFlowgraphs.Text = "Flowgraphs (" + _entityRefs[(int)CurrentDisplay.FLOWGRAPHS].Count + ")";
            showLinkedProxies.Text = "Proxies (" + _entityRefs[(int)CurrentDisplay.PROXIES].Count + ")";
            showLinkedOverrides.Text = "Aliases (" + _entityRefs[(int)CurrentDisplay.ALIASES].Count + ")";
            showLinkedCageAnimations.Text = "CAGEAnimations (" + _entityRefs[(int)CurrentDisplay.CAGEANIMATIONS].Count + ")";
            showLinkedTriggerSequences.Text = "TriggerSequences (" + _entityRefs[(int)CurrentDisplay.TRIGGERSEQUENCES].Count + ")";
        }

        private void OnScopeChanged()
        {
            RebuildEntityRefs();
            UpdateTitle();
            UpdateUI(_currentDisplay);
        }

        /* An undo or redo that leaves the window open (the composite on screen stays the same) may
           have put a reference back or taken one away: the lists follow. Not for the window's own
           removals, which rebuild once at the end. */
        private bool _removing = false;
        private void OnUndoChanged()
        {
            if (IsDisposed || _removing || !IsHandleCreated)
                return;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || _removing)
                    return;
                RebuildEntityRefs();
                UpdateUI(_currentDisplay);
            }));
        }

        /* The rows carry the references they stand for: with rows going and coming as references are
           removed, an index into the reference list is nothing to trust */
        private ListView VisibleList => _currentDisplay == CurrentDisplay.FLOWGRAPHS ? flowgraphList : entityList;
        private List<EntityRef> SelectedRefs()
        {
            List<EntityRef> refs = new List<EntityRef>();
            foreach (ListViewItem item in VisibleList.SelectedItems)
            {
                if (item.Tag is EntityRef entityRef)
                    refs.Add(entityRef);
            }
            return refs;
        }

        private void jumpToEntity_Click(object sender, EventArgs e)
        {
            List<EntityRef> selected = SelectedRefs();
            if (selected.Count == 0) return;
            EntityRef entityRef = selected[0];

            if (_currentDisplay == CurrentDisplay.FLOWGRAPHS)
                OnFlowgraphSelected?.Invoke(entityRef.flowgraph_name, entityRef.entity);
            else
                GlobalEntitySearchHelper.JumpToEntity(entityRef.entity, entityRef.composite, _scopeController.Scope);
            this.Close();
        }

        private void showFlowgraphs_Click(object sender, EventArgs e)
        {
            UpdateUI(CurrentDisplay.FLOWGRAPHS);
        }
        private void showLinkedProxies_Click(object sender, EventArgs e)
        {
            UpdateUI(CurrentDisplay.PROXIES);
        }
        private void showLinkedOverrides_Click(object sender, EventArgs e)
        {
            UpdateUI(CurrentDisplay.ALIASES);
        }
        private void showLinkedTriggerSequences_Click(object sender, EventArgs e)
        {
            UpdateUI(CurrentDisplay.TRIGGERSEQUENCES);
        }
        private void showLinkedCageAnimations_Click(object sender, EventArgs e)
        {
            UpdateUI(CurrentDisplay.CAGEANIMATIONS);
        }

        private void UpdateUI(CurrentDisplay display)
        {
            Cursor.Current = Cursors.WaitCursor;

            _currentDisplay = display;

            entityList.Visible = _currentDisplay != CurrentDisplay.FLOWGRAPHS;
            flowgraphList.Visible = _currentDisplay == CurrentDisplay.FLOWGRAPHS;

            showFlowgraphs.Enabled = display != CurrentDisplay.FLOWGRAPHS;
            showLinkedProxies.Enabled = display != CurrentDisplay.PROXIES;
            showLinkedOverrides.Enabled = display != CurrentDisplay.ALIASES;
            showLinkedTriggerSequences.Enabled = display != CurrentDisplay.TRIGGERSEQUENCES;
            showLinkedCageAnimations.Enabled = display != CurrentDisplay.CAGEANIMATIONS;

            SynchronizedCollection<EntityRef> entityRefs = _entityRefs[(int)display];
            label.Text = entityRefs.Count + " ";

            if (_currentDisplay == CurrentDisplay.FLOWGRAPHS)
            {
                label.Text += "flowgraph" + (entityRefs.Count > 1 ? "s" : "") + " within this composite " + (entityRefs.Count > 1 ? "have" : "has") + " nodes for this entity:"; //important note - only showing pages within the current composite (for now), not aliased pages or proxied pages

                flowgraphList.BeginUpdate();
                flowgraphList.Items.Clear();
                foreach (EntityRef entityRef in entityRefs)
                {
                    ListViewItem item = new ListViewItem(entityRef.flowgraph_name);
                    item.SubItems.Add(entityRef.flowgraph_node_count.ToString());
                    item.Tag = entityRef;
                    flowgraphList.Items.Add(item);
                }
                flowgraphList.EndUpdate();
            }
            else
            {
                switch (_currentDisplay)
                {
                    case CurrentDisplay.PROXIES:
                        label.Text += "Proxies";
                        break;
                    case CurrentDisplay.ALIASES:
                        label.Text += "Aliases";
                        break;
                    case CurrentDisplay.TRIGGERSEQUENCES:
                        label.Text += "TriggerSequences";
                        break;
                    case CurrentDisplay.CAGEANIMATIONS:
                        label.Text += "CAGEAnimations";
                        break;
                }
                label.Text += " pointing to this entity:";

                entityList.BeginUpdate();
                entityList.Items.Clear();
                entityList.Groups.Clear();
                Dictionary<Composite, ListViewGroup> compGroups = new Dictionary<Composite, ListViewGroup>();
                foreach (EntityRef entityRef in entityRefs)
                {
                    ListViewItem item = (ListViewItem)Content.GenerateListViewItem(entityRef.entity, entityRef.composite).Clone();
                    if (compGroups.TryGetValue(entityRef.composite, out ListViewGroup g))
                    {
                        item.Group = g;
                    }
                    else
                    {
                        ListViewGroup group = new ListViewGroup() { Header = entityRef.composite.name };
                        entityList.Groups.Add(group);
                        compGroups.Add(entityRef.composite, group);
                        item.Group = group;
                    }
                    item.ImageIndex = EditorUtils.GetIndexesForListViewItem(entityRef.entity, entityRef.composite, Content.Level.Commands).Item1;
                    item.Tag = entityRef;
                    entityList.Items.Add(item);
                }
                entityList.EndUpdate();
            }

            Cursor.Current = Cursors.Default;
        }

        private SynchronizedCollection<EntityRef> GetEntityRefs(CurrentDisplay display, HashSet<ShortGuid> scopedGuids)
        {
            bool showIDs = SettingsManager.GetBool(Settings.ShowShortGuids);
            SynchronizedCollection<EntityRef> entityRefs = new SynchronizedCollection<EntityRef>();
            //Nothing refers to no entity (crash 385: opened for a selection that had already gone)
            if (_entity == null || Content?.Level?.Commands == null)
                return entityRefs;
            if (display == CurrentDisplay.FLOWGRAPHS)
            {
                foreach (Flowgraph flowgraph in Singleton.Editor?.CompositeDisplay?.Flowgraphs ?? new List<Flowgraph>())
                {
                    if (flowgraph == null || flowgraph.IsDisposed)
                        continue;
                    foreach (STNode node in flowgraph.Nodegraph.Nodes)
                    {
                        if (node?.Entity == null || node.Entity.shortGUID != _entity.shortGUID)
                            continue;

                        EntityRef entRef = entityRefs.FirstOrDefault(o => o.flowgraph_name == flowgraph.FlowgraphName);
                        if (entRef == null)
                        {
                            entRef = new EntityRef();
                            entRef.flowgraph_name = flowgraph.FlowgraphName;
                            entRef.entity = _entity;
                            entityRefs.Add(entRef);
                        }
                        entRef.flowgraph_node_count++;
                    }
                }
            }
            else
            {
                Parallel.ForEach(Content.Level.Commands.Entries, (comp) =>
                {
                    if (scopedGuids != null && !scopedGuids.Contains(comp.shortGUID))
                        return;

                    switch (display)
                    {
                        case CurrentDisplay.PROXIES:
                            Parallel.ForEach(comp.proxies, (prox) =>
                            {
                                if (Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveProxy(prox)).Item2 == _entity)
                                    entityRefs.Add(new EntityRef() { composite = comp, entity = prox });
                            });
                            break;
                        case CurrentDisplay.ALIASES:
                            Parallel.ForEach(comp.aliases, (alias) =>
                            {
                                if (Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(alias, comp)).Item2 == _entity)
                                    entityRefs.Add(new EntityRef() { composite = comp, entity = alias });
                            });
                            break;
                        case CurrentDisplay.TRIGGERSEQUENCES:
                            List<FunctionEntity> triggerSequences = comp.functions_dictionary.Values.Where(o => o.function == FunctionType.TriggerSequence).ToList();
                            Parallel.ForEach(triggerSequences, (trigEnt) =>
                            {
                                TriggerSequence trig = (TriggerSequence)trigEnt;
                                Parallel.ForEach(trig.sequence, (trigger) =>
                                {
                                    if (PointsAtEntity(trigger?.connectedEntity, comp))
                                        entityRefs.Add(new EntityRef() { composite = comp, entity = trig });
                                });
                            });
                            //A proxy to a TriggerSequence can carry a sequence of its own (see ProxyEntity.sequence)
                            Parallel.ForEach(comp.proxies, (prox) =>
                            {
                                Parallel.ForEach(prox.sequence, (trigger) =>
                                {
                                    if (PointsAtEntity(trigger?.connectedEntity, comp))
                                        entityRefs.Add(new EntityRef() { composite = comp, entity = prox });
                                });
                            });
                            break;
                        case CurrentDisplay.CAGEANIMATIONS:
                            List<FunctionEntity> cageAnims = comp.functions_dictionary.Values.Where(o => o.function == FunctionType.CAGEAnimation).ToList();
                            Parallel.ForEach(cageAnims, (animEnt) =>
                            {
                                CAGEAnimation anim = (CAGEAnimation)animEnt;
                                Parallel.ForEach(anim.connections, (connection) =>
                                {
                                    if (PointsAtEntity(connection?.connectedEntity, comp))
                                        entityRefs.Add(new EntityRef() { composite = comp, entity = anim });
                                });
                            });
                            break;
                    }
                });
            }
            return entityRefs;
        }

        /* A trigger or animation entry names the entity by a path read from the composite it sits in -
           what is listed is what resolves to the entity, so that is also what removal takes out. An entry
           with no path names nothing (crash logs had the scan fall over on one). */
        private bool PointsAtEntity(EntityPath path, Composite composite)
        {
            if (path?.path == null || _entity == null)
                return false;
            return Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveEntityPath(path.path, composite)).Item2 == _entity;
        }

        #region REMOVAL

        /* Right-click on a row: jump to it, or take the reference away - the alias or proxy is deleted,
           a TriggerSequence or CAGEAnimation loses the entries that name the entity, a page loses the
           entity's nodes. Every kind is an undoable edit, the selection undoes as one step (one per
           composite for entity deletions), and the lists are rebuilt afterwards so what is shown is
           what is left. */
        private void referenceContextMenu_Opening(object sender, CancelEventArgs e)
        {
            List<EntityRef> selected = DistinctSelectedRefs();
            if (selected.Count == 0)
            {
                e.Cancel = true;
                return;
            }
            removeReferenceToolStripMenuItem.Text = RemovalLabel(selected.Count);
        }

        /* A right-click acts on the row under the cursor, unless that row is part of the selection */
        private void List_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || !(sender is ListView list))
                return;
            ListViewHitTestInfo hit = list.HitTest(e.Location);
            if (hit.Item != null && !hit.Item.Selected)
            {
                list.SelectedIndices.Clear();
                hit.Item.Selected = true;
                hit.Item.Focused = true;
            }
        }

        private void jumpToToolStripMenuItem_Click(object sender, EventArgs e)
        {
            jumpToEntity_Click(sender, e);
        }

        private void removeReferenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RemoveSelectedReferences();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (UndoKeys.TryHandle(keyData))
                return true;

            if (keyData == Keys.Delete && VisibleList.Focused)
            {
                RemoveSelectedReferences();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /* A TriggerSequence or CAGEAnimation is listed once per entry that names the entity, so a
           selection can hold the same one several times - it is removed from once */
        private List<EntityRef> DistinctSelectedRefs()
        {
            List<EntityRef> distinct = new List<EntityRef>();
            foreach (EntityRef entityRef in SelectedRefs())
            {
                bool seen = _currentDisplay == CurrentDisplay.FLOWGRAPHS
                    ? distinct.Any(o => o.flowgraph_name == entityRef.flowgraph_name)
                    : distinct.Any(o => o.entity == entityRef.entity);
                if (!seen)
                    distinct.Add(entityRef);
            }
            return distinct;
        }

        private string RemovalLabel(int count)
        {
            switch (_currentDisplay)
            {
                case CurrentDisplay.FLOWGRAPHS:
                    return count > 1 ? "Remove Nodes From " + count + " Flowgraphs" : "Remove Nodes From Flowgraph";
                case CurrentDisplay.PROXIES:
                    return count > 1 ? "Delete " + count + " Proxies" : "Delete Proxy";
                case CurrentDisplay.ALIASES:
                    return count > 1 ? "Delete " + count + " Aliases" : "Delete Alias";
                case CurrentDisplay.TRIGGERSEQUENCES:
                    return count > 1 ? "Remove From " + count + " TriggerSequences" : "Remove From TriggerSequence";
                case CurrentDisplay.CAGEANIMATIONS:
                    return count > 1 ? "Remove From " + count + " CAGEAnimations" : "Remove From CAGEAnimation";
            }
            return "Remove";
        }

        private string StepLabel(int count)
        {
            string entityName = UndoLabels.Entity(_entityComposite, _entity);
            switch (_currentDisplay)
            {
                case CurrentDisplay.FLOWGRAPHS:
                    return "Remove nodes for " + entityName + " from " + count + " flowgraphs";
                case CurrentDisplay.PROXIES:
                    return "Delete " + count + " proxies to " + entityName;
                case CurrentDisplay.ALIASES:
                    return "Delete " + count + " aliases to " + entityName;
                case CurrentDisplay.TRIGGERSEQUENCES:
                    return "Remove " + entityName + " from " + count + " trigger sequences";
                case CurrentDisplay.CAGEANIMATIONS:
                    return "Remove " + entityName + " from " + count + " animations";
            }
            return "Remove " + count + " references to " + entityName;
        }

        private string RemovalQuestion(int count)
        {
            string entityName = UndoLabels.Entity(_entityComposite, _entity);
            switch (_currentDisplay)
            {
                case CurrentDisplay.FLOWGRAPHS:
                    return "Are you sure you want to remove the nodes for " + entityName + " from " + (count > 1 ? "these " + count + " flowgraphs" : "this flowgraph") + "?";
                case CurrentDisplay.PROXIES:
                    return "Are you sure you want to delete " + (count > 1 ? "these " + count + " proxies" : "this proxy") + "?";
                case CurrentDisplay.ALIASES:
                    return "Are you sure you want to delete " + (count > 1 ? "these " + count + " aliases" : "this alias") + "?";
                case CurrentDisplay.TRIGGERSEQUENCES:
                    return "Are you sure you want to remove " + entityName + " from " + (count > 1 ? "these " + count + " trigger sequences" : "this trigger sequence") + "?";
                case CurrentDisplay.CAGEANIMATIONS:
                    return "Are you sure you want to remove " + entityName + " from " + (count > 1 ? "these " + count + " animations" : "this animation") + "?";
            }
            return "Are you sure?";
        }

        private void RemoveSelectedReferences()
        {
            List<EntityRef> selected = DistinctSelectedRefs();
            if (selected.Count == 0 || Content?.Level?.Commands == null)
                return;

            if (MessageBox.Show(RemovalQuestion(selected.Count), "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            /* One step to undo however many rows were picked - except that undo brings one composite
               on screen per step, and a deleted entity's nodes come back through its live pages, so
               entity deletions are one step per composite. A lone edit keeps its own label. */
            List<List<EntityRef>> steps = _currentDisplay == CurrentDisplay.PROXIES || _currentDisplay == CurrentDisplay.ALIASES
                ? selected.GroupBy(o => o.composite).Select(o => o.ToList()).ToList()
                : new List<List<EntityRef>>() { selected };

            /* An editor window open on a sequence is still holding the list objects the edit will
               replace, so it is closed first - which records what it had changed as a step of its
               own, before the removal's */
            if (_currentDisplay == CurrentDisplay.TRIGGERSEQUENCES)
            {
                foreach (TriggerSequenceEditor editor in System.Windows.Forms.Application.OpenForms.OfType<TriggerSequenceEditor>().ToList())
                {
                    if (selected.Any(o => o.entity == editor.Entity))
                        editor.Close();
                }
            }

            Cursor.Current = Cursors.WaitCursor;
            _removing = true;
            try
            {
                foreach (List<EntityRef> step in steps)
                {
                    using (UndoStack.Current.BeginGroup(step.Count > 1 ? StepLabel(step.Count) : null))
                    {
                        foreach (EntityRef entityRef in step)
                            RemoveReference(entityRef);
                    }
                }
            }
            finally
            {
                _removing = false;
                Cursor.Current = Cursors.Default;
            }

            //Deleting what the inspector shows closes the window along with it
            if (IsDisposed)
                return;
            RebuildEntityRefs();
            UpdateUI(_currentDisplay);
        }

        private void RemoveReference(EntityRef entityRef)
        {
            //A row's entity may have gone since the list was built (an undo of its creation, say)
            if (_currentDisplay != CurrentDisplay.FLOWGRAPHS && entityRef.composite?.GetEntityByID(entityRef.entity.shortGUID) != entityRef.entity)
                return;

            switch (_currentDisplay)
            {
                case CurrentDisplay.FLOWGRAPHS:
                    Singleton.Editor?.CompositeDisplay?.FindFlowgraph(entityRef.flowgraph_name)?.RemoveNodesForEntity(_entity);
                    break;
                case CurrentDisplay.PROXIES:
                case CurrentDisplay.ALIASES:
                    UndoStack.Current.Apply(new EntityDeleteEdit(entityRef.composite, entityRef.entity, "Delete " + UndoLabels.Entity(entityRef.composite, entityRef.entity)));
                    break;
                case CurrentDisplay.TRIGGERSEQUENCES:
                    RemoveFromTriggerSequence(entityRef.entity, entityRef.composite);
                    break;
                case CurrentDisplay.CAGEANIMATIONS:
                    if (entityRef.entity is CAGEAnimation animation)
                        RemoveFromCageAnimation(animation, entityRef.composite);
                    break;
            }
        }

        private void RemoveFromTriggerSequence(Entity entity, Composite composite)
        {
            List<TriggerSequence.SequenceEntry> sequence;
            List<TriggerSequence.MethodEntry> methods;
            switch (entity)
            {
                case TriggerSequence triggerSequence:
                    sequence = triggerSequence.sequence;
                    methods = triggerSequence.methods;
                    break;
                case ProxyEntity proxy:
                    sequence = proxy.sequence;
                    methods = proxy.methods;
                    break;
                default:
                    return;
            }

            List<TriggerSequence.SequenceEntry> kept = sequence.Where(o => !PointsAtEntity(o?.connectedEntity, composite)).ToList();
            if (kept.Count == sequence.Count)
                return;

            UndoStack.Current.Apply(new TriggerSequenceEdit(composite, entity,
                TriggerSequenceEdit.CloneSequence(sequence), TriggerSequenceEdit.CloneMethods(methods),
                TriggerSequenceEdit.CloneSequence(kept), TriggerSequenceEdit.CloneMethods(methods),
                "Remove " + UndoLabels.Entity(_entityComposite, _entity) + " from " + UndoLabels.Entity(composite, entity)));
        }

        private void RemoveFromCageAnimation(CAGEAnimation animation, Composite composite)
        {
            List<CAGEAnimation.Connection> kept = new List<CAGEAnimation.Connection>();
            HashSet<ShortGuid> orphanedTracks = new HashSet<ShortGuid>();
            foreach (CAGEAnimation.Connection connection in animation.connections)
            {
                if (PointsAtEntity(connection?.connectedEntity, composite))
                    orphanedTracks.Add(connection.target_track);
                else
                    kept.Add(connection);
            }
            if (kept.Count == animation.connections.Count)
                return;

            /* The keyframes the entity was driven by go with it - as removing it in the animation
               editor does - unless another connection still plays them. Event tracks stay, as they do
               when the entity itself is deleted: a binding's track can carry keyframes naming other
               entities, and without its binding it is simply unbound. */
            foreach (CAGEAnimation.Connection connection in kept)
            {
                if (connection != null)
                    orphanedTracks.Remove(connection.target_track);
            }

            CageAnimationEdit.Lists before = CageAnimationEdit.Lists.Of(animation);
            CageAnimationEdit.Lists after = new CageAnimationEdit.Lists()
            {
                Connections = kept,
                EventTracks = animation.eventTracks,
                FloatTracks = animation.floatTracks.Where(o => !orphanedTracks.Contains(o.shortGUID)).ToList(),
                Parameters = animation.parameters,
            };
            UndoStack.Current.Apply(new CageAnimationEdit(composite, animation, before, after,
                "Remove " + UndoLabels.Entity(_entityComposite, _entity) + " from " + UndoLabels.Entity(composite, animation)));
        }

        #endregion

        private enum CurrentDisplay
        {
            FLOWGRAPHS,
            PROXIES,
            ALIASES,
            TRIGGERSEQUENCES,
            CAGEANIMATIONS,
        }

        private class EntityRef
        {
            public Entity entity;
            public Composite composite;

            public string flowgraph_name;
            public int flowgraph_node_count;
        }
    }
}
