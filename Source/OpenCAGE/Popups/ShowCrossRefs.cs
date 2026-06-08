using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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

        public ShowCrossRefs(Entity entity) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _entity = entity;
            InitializeComponent();

            bool hasID = entityList.Columns.ContainsKey("ID");
            bool showID = SettingsManager.GetBool(Singleton.Settings.ShowShortGuids);
            if (showID && !hasID)
                entityList.Columns.Add(new ColumnHeader() { Name = "ID", Text = "ID", Width = 100 });
            else if (!showID && hasID)
                entityList.Columns.RemoveByKey("ID");

            int displayTypes = Enum.GetValues(typeof(CurrentDisplay)).Length;
            _entityRefs = new SynchronizedCollection<EntityRef>[displayTypes];
            Parallel.For(0, displayTypes, (i) =>
            {
                _entityRefs[i] = GetEntityRefs((CurrentDisplay)i);
            });

            showFlowgraphs.Text = "Flowgraphs (" + _entityRefs[(int)CurrentDisplay.FLOWGRAPHS].Count + ")";
            showLinkedProxies.Text = "Proxies (" + _entityRefs[(int)CurrentDisplay.PROXIES].Count + ")";
            showLinkedOverrides.Text = "Aliases (" + _entityRefs[(int)CurrentDisplay.ALIASES].Count + ")";
            showLinkedCageAnimations.Text = "CAGEAnimations (" + _entityRefs[(int)CurrentDisplay.CAGEANIMATIONS].Count + ")";
            showLinkedTriggerSequences.Text = "TriggerSequences (" + _entityRefs[(int)CurrentDisplay.TRIGGERSEQUENCES].Count + ")";

            UpdateUI(CurrentDisplay.FLOWGRAPHS);
        }

        private void jumpToEntity_Click(object sender, EventArgs e)
        {
            if (_currentDisplay == CurrentDisplay.FLOWGRAPHS)
            {
                if (flowgraphList.SelectedItems.Count == 0) return;
                OnFlowgraphSelected?.Invoke(_entityRefs[(int)_currentDisplay][flowgraphList.SelectedItems[0].Index].flowgraph_name, _entityRefs[(int)_currentDisplay][flowgraphList.SelectedItems[0].Index].entity);
            }
            else
            {
                if (entityList.SelectedItems.Count == 0) return;
                OnEntitySelected?.Invoke(_entityRefs[(int)_currentDisplay][entityList.SelectedItems[0].Index].composite, _entityRefs[(int)_currentDisplay][entityList.SelectedItems[0].Index].entity);
            }
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
                    entityList.Items.Add(item);
                }
                entityList.EndUpdate();
            }

            Cursor.Current = Cursors.Default;
        }

        private SynchronizedCollection<EntityRef> GetEntityRefs(CurrentDisplay display)
        {
            bool showIDs = SettingsManager.GetBool(Singleton.Settings.ShowShortGuids);
            SynchronizedCollection<EntityRef> entityRefs = new SynchronizedCollection<EntityRef>();
            if (display == CurrentDisplay.FLOWGRAPHS)
            {
                foreach (Flowgraph flowgraph in Singleton.Editor.CompositeDisplay.Flowgraphs)
                {
                    foreach (STNode node in flowgraph.Nodegraph.Nodes)
                    {
                        if (node.Entity.shortGUID != _entity.shortGUID)
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
                                    if (Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(trigger.connectedEntity.path, comp)).Item2 == _entity)
                                        entityRefs.Add(new EntityRef() { composite = comp, entity = trig });
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
                                    if (Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity.path, comp)).Item2 == _entity)
                                        entityRefs.Add(new EntityRef() { composite = comp, entity = anim });
                                });
                            });
                            break;
                    }
                });
            }
            return entityRefs;
        }

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
