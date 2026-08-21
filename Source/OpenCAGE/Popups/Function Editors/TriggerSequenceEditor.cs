using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    //TODO: I should add pins to all TriggerSequence nodes for any new methods that are added here.

    public partial class TriggerSequenceEditor : BaseWindow
    {
        //The entity being edited: either a TriggerSequence, or a ProxyEntity pointing at one
        //(proxies carry their own trigger sequence data) - we edit its lists directly.
        Entity _entity = null;
        List<TriggerSequence.SequenceEntry> _sequence = null;
        List<TriggerSequence.MethodEntry> _methods = null;

        EntityInspector _entityDisplay;
        private bool _suppressDelayWrite = false;
        private readonly string _openSnapshot;

        public TriggerSequenceEditor(EntityInspector entityDisplay) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            _entityDisplay = entityDisplay;
            _entity = _entityDisplay.Entity;
            switch (_entity)
            {
                case TriggerSequence triggerSequence:
                    _sequence = triggerSequence.sequence;
                    _methods = triggerSequence.methods;
                    break;
                case ProxyEntity proxyEntity:
                    _sequence = proxyEntity.sequence;
                    _methods = proxyEntity.methods;
                    break;
                default:
                    throw new ArgumentException("TriggerSequenceEditor requires a TriggerSequence, or a proxy to one.");
            }

            //The trigger data is edited in place from a lot of handlers, so instead of marking the level as
            //modified in each one, compare against this snapshot when the window closes
            _openSnapshot = DirtyTracker.Snapshot(new object[] { _sequence, _methods });
            this.FormClosing += (s, e) => DirtyTracker.MarkIfChanged(_openSnapshot, new object[] { _sequence, _methods });

            entityTriggerDelay.Text = "0.0";
            this.Text = "TriggerSequence Editor: " + Content.Level.Commands.Utils.GetEntityName(_entityDisplay.Composite, _entity);
            selectedEntityDetails.Visible = false;
            selectedTriggerDetails.Visible = false;

            entity_list.AllowDrop = true;
            entity_list.ItemDrag += entity_list_ItemDrag;
            entity_list.DragEnter += entity_list_DragEnter;
            entity_list.DragOver += entity_list_DragOver;
            entity_list.DragDrop += entity_list_DragDrop;
            entity_list.DragLeave += entity_list_DragLeave;

            ReloadEntityList();
            ReloadTriggerList();
        }

        /* The list can be filtered, so a row's position is not its position in the sequence - every row
           carries its sequence index instead. */
        private static int SequenceIndexOf(ListViewItem item)
        {
            return item?.Tag is int index ? index : -1;
        }
        private int SelectedSequenceIndex => entity_list.SelectedItems.Count == 0 ? -1 : SequenceIndexOf(entity_list.SelectedItems[0]);
        private bool IsFiltered => _entitySearch != "";
        private string _entitySearch = "";

        private void ReloadEntityList(int indexToSelect = -1)
        {
            entity_list.BeginUpdate();
            entity_list.Items.Clear();
            for (int i = 0; i < _sequence.Count; i++)
            {
                string name = Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveAlias(_sequence[i].connectedEntity.path, _entityDisplay.Composite), SettingsManager.GetBool(Settings.ShowShortGuids));
                if (IsFiltered && !name.ToUpper().Replace(" ", "").Contains(_entitySearch))
                    continue;

                ListViewItem item = new ListViewItem();
                item.Text = name;
                item.SubItems.Add(_sequence[i].timing + "s");
                item.Tag = i;
                entity_list.Items.Add(item);
            }
            entity_list.EndUpdate();

            if (indexToSelect == -1)
                return;

            foreach (ListViewItem item in entity_list.Items)
            {
                if (SequenceIndexOf(item) != indexToSelect)
                    continue;

                item.Selected = true;
                entity_list.EnsureVisible(item.Index);
                break;
            }
        }

        private void entitySearchBox_TextChanged(object sender, EventArgs e)
        {
            string newSearch = entitySearchBox.Text.ToUpper().Replace(" ", "");
            if (newSearch == _entitySearch)
                return;

            int selected = SelectedSequenceIndex;
            _entitySearch = newSearch;
            ReloadEntityList(selected);
            LoadSelectedEntity();
        }

        private void clearEntitySearchBtn_Click(object sender, EventArgs e)
        {
            entitySearchBox.Text = "";
        }
        private void ReloadTriggerList()
        {
            trigger_list.BeginUpdate();
            trigger_list.Items.Clear();
            for (int i = 0; i < _methods.Count; i++)
            {
                trigger_list.Items.Add(ShortGuidUtils.FindString(_methods[i].method) + " -> " + ShortGuidUtils.FindString(_methods[i].finished));
            }
            trigger_list.EndUpdate();
        }

        private void triggerDelay_TextChanged(object sender, EventArgs e)
        {
            if (_suppressDelayWrite)
                return;

            entityTriggerDelay.Text = EditorUtils.ForceStringNumeric(entityTriggerDelay.Text, true);

            if (entity_list.SelectedItems.Count == 0)
                return;
            if (string.IsNullOrWhiteSpace(entityTriggerDelay.Text) || entityTriggerDelay.Text == "." || entityTriggerDelay.Text == "-" || entityTriggerDelay.Text == "-.")
                return;

            float delay = Convert.ToSingle(entityTriggerDelay.Text);
            string delayLabel = entityTriggerDelay.Text + "s";
            foreach (ListViewItem item in entity_list.SelectedItems)
            {
                _sequence[SequenceIndexOf(item)].timing = delay;
                item.SubItems[1].Text = delayLabel;
            }
        }

        private void entity_list_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSelectedEntity();
        }

        private void LoadSelectedEntity()
        {
            int selectedCount = entity_list.SelectedItems.Count;
            bool singleSelection = selectedCount == 1;
            //Reordering is only meaningful when the whole sequence is on screen
            moveUp.Enabled = singleSelection && !IsFiltered;
            moveDown.Enabled = singleSelection && !IsFiltered;
            selectEntToPointTo.Enabled = singleSelection;
            button3.Enabled = singleSelection;

            if (selectedCount == 0)
            {
                _suppressDelayWrite = true;
                entityHierarchy.Text = "";
                entityTriggerDelay.Text = "0.0";
                _suppressDelayWrite = false;
                selectedEntityDetails.Visible = false;
                return;
            }

            selectedEntityDetails.Visible = true;

            _suppressDelayWrite = true;
            if (singleSelection)
            {
                int index = SelectedSequenceIndex;
                entityHierarchy.Text = Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveAlias(_sequence[index].connectedEntity.path, _entityDisplay.Composite), SettingsManager.GetBool(Settings.ShowShortGuids));
                entityTriggerDelay.Text = _sequence[index].timing.ToString();
            }
            else
            {
                entityHierarchy.Text = selectedCount + " entities selected";

                float? sharedDelay = null;
                bool delaysMatch = true;
                foreach (ListViewItem item in entity_list.SelectedItems)
                {
                    float delay = _sequence[SequenceIndexOf(item)].timing;
                    if (sharedDelay == null)
                        sharedDelay = delay;
                    else if (sharedDelay.Value != delay)
                    {
                        delaysMatch = false;
                        break;
                    }
                }
                entityTriggerDelay.Text = delaysMatch && sharedDelay.HasValue ? sharedDelay.Value.ToString() : "";
            }
            _suppressDelayWrite = false;
        }

        private void LoadSelectedTriggers()
        {
            if (trigger_list.SelectedIndex == -1)
            {
                triggerStartParam.Text = "";
                selectedTriggerDetails.Visible = false;
                return;
            }

            triggerStartParam.Text = ShortGuidUtils.FindString(_methods[trigger_list.SelectedIndex].method);
            selectedTriggerDetails.Visible = true;
        }

        private void selectEntToPointTo_Click(object sender, EventArgs e)
        {
            SelectHierarchy hierarchyEditor = new SelectHierarchy(_entityDisplay.Composite, new CompositeEntityList.DisplayOptions()
            {
                DisplayAliases = false,
                DisplayFunctions = true,
                DisplayProxies = false,
                DisplayVariables = false,
            });
            hierarchyEditor.Show(this);
            hierarchyEditor.OnHierarchyGenerated += HierarchyEditor_HierarchyGenerated;
        }
        private void HierarchyEditor_HierarchyGenerated(ShortGuid[] generatedHierarchy)
        {
            if (entity_list.SelectedItems.Count == 0) return;
            int index = SelectedSequenceIndex;
            _sequence[index].connectedEntity.path = generatedHierarchy;
            ReloadEntityList(index);
            LoadSelectedEntity();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < _sequence.Count; i++)
            {
                if (_sequence[i].connectedEntity.path.Length == 0 || _sequence[i].connectedEntity.path.Length == 1)
                {
                    MessageBox.Show("One or more triggers does not point to a node!", "Trigger setup incorrectly!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            this.Close();
        }

        private void addNewEntity_Click(object sender, EventArgs e)
        {
            SelectHierarchy hierarchyEditor = new SelectHierarchy(_entityDisplay.Composite, new CompositeEntityList.DisplayOptions()
            {
                DisplayAliases = false,
                DisplayFunctions = true,
                DisplayProxies = false,
                DisplayVariables = false,
                ShowCheckboxes = true,
            });
            hierarchyEditor.Show(this);
            hierarchyEditor.OnHierarchiesGenerated += addNewEntities_HierarchiesGenerated;
        }
        private void addNewEntities_HierarchiesGenerated(List<ShortGuid[]> hierarchies)
        {
            if (hierarchies == null || hierarchies.Count == 0)
                return;

            int insertIndex = (entity_list.SelectedItems.Count == 0) ? _sequence.Count : SelectedSequenceIndex + 1;
            for (int i = 0; i < hierarchies.Count; i++)
            {
                TriggerSequence.SequenceEntry trigger = new TriggerSequence.SequenceEntry();
                trigger.connectedEntity.path = hierarchies[i];
                _sequence.Insert(insertIndex + i, trigger);
            }

            int selectIndex = insertIndex + hierarchies.Count - 1;
            ReloadEntityList(selectIndex);
            LoadSelectedEntity();
        }

        private void deleteSelectedEntity_Click(object sender, EventArgs e)
        {
            if (entity_list.SelectedItems.Count == 0) 
                return;
            _sequence.RemoveAt(SelectedSequenceIndex);
            ReloadEntityList();
            LoadSelectedEntity();
        }

        private void addNewParamTrigger_Click(object sender, EventArgs e)
        {
            TriggerSequence.MethodEntry trigger = new TriggerSequence.MethodEntry(triggerStartParam.Text);

            int insertIndex = (trigger_list.SelectedIndex == -1) ? _methods.Count : trigger_list.SelectedIndex + 1;
            _methods.Insert(insertIndex, trigger);

            ReloadTriggerList();
            trigger_list.SelectedIndex = insertIndex;
            LoadSelectedTriggers();
        }
        private void deleteParamTrigger_Click(object sender, EventArgs e)
        {
            if (trigger_list.SelectedIndex == -1) return;
            _methods.RemoveAt(trigger_list.SelectedIndex);
            ReloadTriggerList();
            LoadSelectedTriggers();
        }

        private void saveTrigger_Click(object sender, EventArgs e)
        {
            if (trigger_list.SelectedIndex == -1) return;
            int index = trigger_list.SelectedIndex;
            _methods[index] = new TriggerSequence.MethodEntry(triggerStartParam.Text);
            LoadSelectedTriggers();
            ReloadTriggerList();
            trigger_list.SelectedIndex = index;
        }

        private void trigger_list_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSelectedTriggers();
        }

        private void moveUp_Click(object sender, EventArgs e)
        {
            if (entity_list.SelectedItems.Count == 0) return;
            int index = SelectedSequenceIndex;
            if (index == 0) return;

            TriggerSequence.SequenceEntry toMoveDown = _sequence[index - 1];
            TriggerSequence.SequenceEntry toMoveUp = _sequence[index];

            _sequence[index - 1] = toMoveUp;
            _sequence[index] = toMoveDown;

            ReloadEntityList(index - 1);
        }

        private void moveDown_Click(object sender, EventArgs e)
        {
            if (entity_list.SelectedItems.Count == 0) return;
            int index = SelectedSequenceIndex;
            if (index == _sequence.Count - 1) return;

            TriggerSequence.SequenceEntry toMoveUp = _sequence[index + 1];
            TriggerSequence.SequenceEntry toMoveDown = _sequence[index];

            _sequence[index + 1] = toMoveDown;
            _sequence[index] = toMoveUp;

            ReloadEntityList(index + 1);
        }

        private void entity_list_ItemDrag(object sender, ItemDragEventArgs e)
        {
            //Reordering a filtered list would be ambiguous: the rows either side may not be the
            //sequence entries either side
            if (IsFiltered)
                return;

            if (e.Item is ListViewItem item)
                DoDragDrop(item, DragDropEffects.Move);
        }

        private void entity_list_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(ListViewItem)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void entity_list_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            e.Effect = DragDropEffects.Move;
            Point clientPoint = entity_list.PointToClient(new Point(e.X, e.Y));
            ListViewItem hoverItem = entity_list.GetItemAt(clientPoint.X, clientPoint.Y);

            if (hoverItem == null)
            {
                if (entity_list.Items.Count == 0)
                {
                    entity_list.InsertionMark.Index = -1;
                    return;
                }

                entity_list.InsertionMark.Index = entity_list.Items.Count - 1;
                entity_list.InsertionMark.AppearsAfterItem = true;
                return;
            }

            Rectangle bounds = hoverItem.GetBounds(ItemBoundsPortion.Entire);
            entity_list.InsertionMark.AppearsAfterItem = clientPoint.Y > bounds.Top + (bounds.Height / 2);
            entity_list.InsertionMark.Index = hoverItem.Index;
        }

        private void entity_list_DragLeave(object sender, EventArgs e)
        {
            entity_list.InsertionMark.Index = -1;
        }

        private void entity_list_DragDrop(object sender, DragEventArgs e)
        {
            if (!(e.Data.GetData(typeof(ListViewItem)) is ListViewItem dragItem))
                return;

            int fromIndex = SequenceIndexOf(dragItem);
            int markIndex = entity_list.InsertionMark.Index;
            bool appearsAfter = entity_list.InsertionMark.AppearsAfterItem;
            entity_list.InsertionMark.Index = -1;

            if (fromIndex < 0 || markIndex < 0 || markIndex >= entity_list.Items.Count)
                return;

            int toIndex = SequenceIndexOf(entity_list.Items[markIndex]);
            if (toIndex < 0)
                return;

            if (appearsAfter)
                toIndex++;
            if (toIndex > fromIndex)
                toIndex--;
            if (fromIndex == toIndex)
                return;

            TriggerSequence.SequenceEntry entry = _sequence[fromIndex];
            _sequence.RemoveAt(fromIndex);
            _sequence.Insert(toIndex, entry);
            ReloadEntityList(toIndex);
            LoadSelectedEntity();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (entity_list.CheckedItems.Count == 0)
                return;

            if (MessageBox.Show("You are about to remove " + entity_list.CheckedItems.Count + " triggers. Are you sure?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) 
                return;

            List<int> invalidIndexes = new List<int>();
            foreach (ListViewItem item in entity_list.CheckedItems)
                invalidIndexes.Add(SequenceIndexOf(item));

            List<TriggerSequence.SequenceEntry> filteredEnts = new List<TriggerSequence.SequenceEntry>();
            for (int i = 0; i < _sequence.Count; i++)
            {
                if (invalidIndexes.Contains(i))
                    continue;
                filteredEnts.Add(_sequence[i]);
            }
            //Mutate in place - _sequence is the entity's own list, reassigning would orphan it
            _sequence.Clear();
            _sequence.AddRange(filteredEnts);

            ReloadEntityList();
            LoadSelectedEntity();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (entity_list.SelectedItems.Count == 0) 
                return;

            if (MessageBox.Show("Going to this entity will close the TriggerSequence editor.\nAre you sure you want to continue?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            (Composite comp, Entity ent) = Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(_sequence[SelectedSequenceIndex].connectedEntity.path, _entityDisplay.Composite));
            if (comp == null || ent == null)
            {
                MessageBox.Show("Failed to resolve entity! Can not load to it.", "Entity pointer corrupted!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _entityDisplay.CompositeDisplay.CompositeBrowser.LoadCompositeAndEntity(comp, ent);
        }
    }
}
