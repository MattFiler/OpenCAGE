using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.UserControls;
using OpenCAGE.Theming;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static CathodeLib.CompositePinInfoTable;

namespace OpenCAGE.Popups.UserControls
{
    public partial class CompositeEntityList : UserControl
    {
        public Entity SelectedEntity
        {
            get
            {
                if (composite_content.SelectedItems.Count == 0) return null;
                return (Entity)composite_content.SelectedItems[0].Tag;
            }
        }
        /// <summary>
        /// Opt-in multi-select support (only enabled by the composite display's entity list -
        /// other views of this control remain single-select).
        /// </summary>
        public bool AllowMultiSelect
        {
            get => composite_content.MultiSelect;
            set => composite_content.MultiSelect = value;
        }

        public List<Entity> SelectedEntities
        {
            get
            {
                List<Entity> toReturn = new List<Entity>();
                foreach (ListViewItem item in composite_content.SelectedItems)
                    if (item.Tag is Entity entity)
                        toReturn.Add(entity);
                return toReturn;
            }
        }

        /// <summary>
        /// Whether the list itself holds keyboard focus, as opposed to the search box beside it.
        /// </summary>
        /// <remarks>
        /// A shortcut owned by the window this control sits in is offered every key, wherever the user
        /// is typing - so anything acting on the selection has to check it is the list being typed at.
        /// Delete would otherwise remove the selected entity instead of a character from the search.
        /// </remarks>
        public bool ListHasFocus => composite_content != null && composite_content.Focused;

        public List<Entity> CheckedEntities
        {
            get
            {
                List<Entity> toReturn = new List<Entity>();
                if (composite_content.CheckedItems.Count == 0) return toReturn;

                foreach (ListViewItem item in composite_content.CheckedItems)
                    toReturn.Add((Entity)item.Tag);
                return toReturn;
            }
        }

        public Action<Entity> SelectedEntityChanged;
        public Action<List<Entity>> SelectedEntitiesChanged; //only raised when AllowMultiSelect and more than one entity is selected

        /// <summary>
        /// What Ctrl + middle click on a composite instance means for whoever is showing this list. The
        /// editor's entity list walks the editor into it; a picker walks its own path in instead. Left
        /// unset the shortcut does nothing - a list is in no position to assume where a step in belongs.
        /// </summary>
        public Action<Entity> StepIntoCompositeInstance;

        public Composite Composite => _composite;
        private Composite _composite;

        protected LevelContent Content => Singleton.Editor?.CompositeBrowser?.Content;

        private string _currentSearch = "";
        private DisplayOptions _displayOptions;

        /* Rows are looked up by entity id rather than by walking the list: every Items[i] is a message
           to the native control, so a scan of a big composite was thousands of them per selection. */
        private readonly Dictionary<ShortGuid, ListViewItem> _itemsById = new Dictionary<ShortGuid, ListViewItem>();
        private bool _suppressSelectionEvents;

        /* Each group's rows in the order they are drawn. Kept here by the sort, so the control never
           has to be asked for it - every Items[i] or Index is a message to the native window. */
        private List<ListViewItem>[] _orderedByGroup = new List<ListViewItem>[0];

        /* Column sorting. The groups keep their fixed order and rows are sorted within each group by
           the clicked column. Rather than handing the control a comparer (LVM_SORTITEMS calls back into
           managed code per comparison, and leaves each group's managed item order - which keyboard
           navigation relies on - out of step with what is drawn), rows are ordered here and inserted in
           that order, so the native order, the group order and the drawn order are the same thing.
           The choice is one for the whole app: every list follows it, and it is kept in settings. */
        public enum SortColumn { Name = 0, Type = 1, Id = 2 }
        private const string NameColumnTitle = "Name";
        private const string TypeColumnTitle = "Type";
        private const string IdColumnTitle = "ID";
        private static bool _sortLoaded;
        private static SortColumn _sortColumn = SortColumn.Name;
        private static bool _sortAscending = true;
        private bool _hasIdColumn;

        /* A re-sort of the control that a burst of additions only pays for once */
        private int _sortGeneration;
        private bool _sortPosted;

        public static SortColumn CurrentSortColumn { get { EnsureSortLoaded(); return _sortColumn; } }
        public static bool CurrentSortAscending { get { EnsureSortLoaded(); return _sortAscending; } }

        public CompositeEntityList()
        {
            InitializeComponent();
            ClearSearch();

            clearSearchBtn.BringToFront();

            this.Disposed += CompositeEntityList_Disposed;

            composite_content.MouseDown += Composite_content_MouseDown;
            composite_content.ItemDrag += Composite_content_ItemDrag;
            composite_content.KeyPress += Composite_content_KeyPress;
            _orderedByGroup = new List<ListViewItem>[composite_content.Groups.Count];
            for (int i = 0; i < _orderedByGroup.Length; i++)
                _orderedByGroup[i] = new List<ListViewItem>();
            ListViewGroupNavigation.Attach(composite_content, DisplayOrder);
            EnsureSortLoaded();
            composite_content.ColumnClick += composite_content_ColumnClick;
            UpdateSortGlyphs();

            Singleton.OnEntityRenamed += OnEntityRenamed;
            Singleton.OnCompositeRenamed += OnCompositeRenamed;
            Singleton.OnEntityDeleted += OnEntityDeleted;
        }

        private void Composite_content_MouseDown(object sender, MouseEventArgs e)
        {
            //A ListView doesn't select on right click, so a context menu would act on whatever was
            //selected beforehand. Select what was actually clicked (leaving a multi-selection alone).
            if (e.Button == MouseButtons.Right)
            {
                ListViewHitTestInfo rightHit = composite_content.HitTest(e.Location);
                if (rightHit.Item != null && !rightHit.Item.Selected)
                {
                    composite_content.SelectedIndices.Clear();
                    rightHit.Item.Selected = true;
                    rightHit.Item.Focused = true;
                }
                return;
            }

            if (e.Button != MouseButtons.Middle || (Control.ModifierKeys & Keys.Control) != Keys.Control)
                return;
            ListViewHitTestInfo hit = composite_content.HitTest(e.Location);
            if (hit.Item == null)
                return;

            composite_content.SelectedIndices.Clear();
            hit.Item.Selected = true;
            hit.Item.Focused = true;

            Entity entity = hit.Item.Tag as Entity;
            if (entity == null || StepIntoCompositeInstance == null)
                return;

            /* Stepping in reloads a list - this one, or the editor's - and doing that from inside the
               mouse message this control is still delivering tears the control down mid-click. Let the
               message finish first. */
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed)
                    return;

                StepIntoCompositeInstance?.Invoke(entity);
            }));
        }

        private void Composite_content_KeyPress(object sender, KeyPressEventArgs e)
        {
            // ListView prefix-jump steals typing; send it to the search box instead.
            if (char.IsControl(e.KeyChar) && e.KeyChar != '\b')
                return;

            e.Handled = true;

            if (e.KeyChar == '\b')
            {
                if (_currentSearch.Length > 0)
                    entity_search_box.Text = _currentSearch.Substring(0, _currentSearch.Length - 1);
            }
            else
            {
                entity_search_box.Text = _currentSearch + e.KeyChar;
            }

            entity_search_box.Focus();
            entity_search_box.SelectionStart = entity_search_box.Text.Length;
            entity_search_box.SelectionLength = 0;
        }

        private void Composite_content_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (!(e.Item is ListViewItem item) || !(item.Tag is Entity entity))
                return;

            DataObject data = new DataObject();
            data.SetData(EntityList.EntityDragFormat, entity.shortGUID.AsUInt32);

            DoDragDrop(data, DragDropEffects.Copy);
        }

        private void CompositeEntityList_Disposed(object sender, EventArgs e)
        {
            this.Disposed -= CompositeEntityList_Disposed;

            Singleton.OnEntityRenamed -= OnEntityRenamed;
            Singleton.OnCompositeRenamed -= OnCompositeRenamed;
            Singleton.OnEntityDeleted -= OnEntityDeleted;

            composite_content.ItemDrag -= Composite_content_ItemDrag;
            composite_content.KeyPress -= Composite_content_KeyPress;
            composite_content.ColumnClick -= composite_content_ColumnClick;
            ClearGroupsAndItems();
        }

        private void OnEntityDeleted(Entity entity)
        {
            if (_composite == null || entity == null)
                return;

            if (_composite.GetEntityByID(entity.shortGUID) != null)
                return;

            RemoveEntity(entity.shortGUID);
        }

        private void OnEntityRenamed(Entity entity, string name)
        {
            if (_composite == null || entity == null || Content == null)
                return;

            ShortGuid renamedId = entity.shortGUID;
            bool keepRenamedEntitySelected = SelectedEntity?.shortGUID == renamedId;
            ListViewItem topItem = composite_content.TopItem;
            int topIndex = topItem?.Index ?? 0;

            composite_content.BeginUpdate();
            try
            {
                if (_composite.GetEntityByID(renamedId) != null)
                    UpdateEntityInList(entity);

                foreach (ProxyEntity proxy in _composite.proxies)
                {
                    if (proxy.proxy.path.Contains(renamedId))
                        UpdateEntityInList(proxy);
                }

                foreach (AliasEntity alias in _composite.aliases)
                {
                    if (alias.alias.path.Contains(renamedId))
                        UpdateEntityInList(alias);
                }
            }
            finally
            {
                composite_content.EndUpdate();
                ThemeListView.Refresh(composite_content);
            }

            if (keepRenamedEntitySelected)
                SelectEntity(entity);
            else if (topItem != null && topIndex < composite_content.Items.Count)
                composite_content.TopItem = composite_content.Items[topIndex];
        }

        private void OnCompositeRenamed(Composite composite, string name)
        {
            if (_composite == null || composite == null || Content == null)
                return;

            ListViewItem topItem = composite_content.TopItem;
            int topIndex = topItem?.Index ?? 0;

            composite_content.BeginUpdate();
            try
            {
                foreach (FunctionEntity function in _composite.functions)
                {
                    if (function.function == composite.shortGUID)
                        UpdateEntityInList(function);
                }
            }
            finally
            {
                composite_content.EndUpdate();
                ThemeListView.Refresh(composite_content);
            }

            if (topItem != null && topIndex < composite_content.Items.Count)
                composite_content.TopItem = composite_content.Items[topIndex];
        }

        /* This UserControl differs from BaseUserControl because we don't instantiate at runtime - so make sure to call setup in code to pass this construction info before you use it. */
        public void Setup(Composite composite, DisplayOptions displayOptions = null, bool doReload = true)
        {
            _composite = composite;
            SetDisplayOptions(displayOptions, doReload);
        }

        /* Update the display options to handle filtering out certain entity types */
        public void SetDisplayOptions(DisplayOptions displayOptions, bool doReload = true)
        {
            _displayOptions = displayOptions == null ? new DisplayOptions() : displayOptions;
            composite_content.CheckBoxes = _displayOptions.ShowCheckboxes;

            if (doReload)
                ReloadComposite();
        }

        public bool ContainsEntity(ShortGuid entityId)
        {
            return FindItem(entityId) != null;
        }

        /* The listed row for an entity, or null if it isn't listed */
        private ListViewItem FindItem(ShortGuid entityId)
        {
            return _itemsById.TryGetValue(entityId, out ListViewItem item) && item.ListView == composite_content ? item : null;
        }

        private int FindEntityIndex(ShortGuid entityId)
        {
            ListViewItem item = FindItem(entityId);
            return item == null ? -1 : item.Index;
        }

        /* Select an entity in the list, if it's there */
        public void SelectEntity(Entity entity)
        {
            if (entity == null)
                return;

            int selectedIndex = SelectEntityInList(entity);

            if (selectedIndex == -1)
            {
                clearSearchBtn_Click(null, null);
                selectedIndex = SelectEntityInList(entity);
            }

            if (selectedIndex != -1)
            {
                composite_content.EnsureVisible(selectedIndex);
            }
        }

        private int SelectEntityInList(Entity entity)
        {
            if (entity == null)
                return -1;

            ListViewItem item = FindItem(entity.shortGUID);
            if (item == null)
                return -1;

            //With multi-select enabled, a programmatic select replaces the selection rather than adding to it
            if (composite_content.MultiSelect
                && (composite_content.SelectedItems.Count > 1 || (composite_content.SelectedItems.Count == 1 && composite_content.SelectedItems[0] != item)))
                composite_content.SelectedIndices.Clear();

            item.Selected = true;
            return item.Index;
        }

        /* Select several entities at once, without a selection event per row - the caller is applying
           a selection that already exists somewhere else (the viewport), so it drives the rest itself.
           Entities the list isn't showing (filtered out by a search) are skipped. */
        public void SelectEntities(List<Entity> entities)
        {
            if (entities == null || entities.Count == 0)
                return;

            _suppressSelectionEvents = true;
            composite_content.BeginUpdate();
            try
            {
                composite_content.SelectedIndices.Clear();

                int first = -1;
                foreach (Entity entity in entities)
                {
                    ListViewItem item = entity == null ? null : FindItem(entity.shortGUID);
                    if (item == null)
                        continue;

                    item.Selected = true;
                    if (first == -1)
                        first = item.Index;
                }

                if (first != -1)
                    composite_content.EnsureVisible(first);
            }
            finally
            {
                composite_content.EndUpdate();
                _suppressSelectionEvents = false;
            }
        }

        public void ClearSelection()
        {
            composite_content.SelectedItems.Clear();
        }

        /* Refresh a single list row from the cached ListViewItem for an entity. */
        public bool UpdateEntityInList(Entity entity)
        {
            if (_composite == null || entity == null || Content == null)
                return false;

            ListViewItem existing = FindItem(entity.shortGUID);
            if (existing == null)
                return false;

            //Regenerate rather than reading the cache - the cached row still holds the old values
            ListViewItem cached = Content.GenerateListViewItem(entity, _composite, LevelContent.CacheMethod.IGNORE_AND_OVERWRITE_CACHE);

            existing.Text = cached.Text;
            while (existing.SubItems.Count > cached.SubItems.Count)
                existing.SubItems.RemoveAt(existing.SubItems.Count - 1);

            for (int i = 1; i < cached.SubItems.Count; i++)
            {
                if (i < existing.SubItems.Count)
                    existing.SubItems[i].Text = cached.SubItems[i].Text;
                else
                    existing.SubItems.Add(cached.SubItems[i].Text);
            }

            RepositionItem(existing);
            return true;
        }

        /* Reload the active composite's entities */
        public void ReloadComposite(bool clearSearch = false)
        {
            if (clearSearch)
                ClearSearch();

            LoadComposite(_composite);
        }

        /* Load a new composite into the entity list */
        public void LoadComposite(Composite composite, bool clearSearch = false)
        {
            bool compositeChanged = composite == null
                ? _composite != null
                : _composite == null || _composite.shortGUID != composite.shortGUID;

            _composite = composite;

            if (clearSearch || compositeChanged)
                ClearSearch();

            DoSearch();
        }

        public string SearchText => _currentSearch;

        /* Apply search text after navigation (does not clear on composite change) */
        public void ApplySearch(string text)
        {
            text = text ?? "";
            if (entity_search_box.Text == text)
            {
                if (_currentSearch != text)
                    _currentSearch = text;
                clearSearchBtn.Visible = _currentSearch != "";
                DoSearch();
                return;
            }

            entity_search_box.Text = text;
        }

        /* Add a new entity to the list, in the place the sort puts it */
        public void AddNewEntity(Entity entity, bool skipSanityChecks = false)
        {
            if (entity == null)
                return;
            if (!skipSanityChecks && !IsDisplayable(entity))
                return;
            if (FindItem(entity.shortGUID) != null)
                return;

            InsertSorted(CreateRow(entity));
        }

        private bool IsDisplayable(Entity entity)
        {
            switch (entity.variant)
            {
                case EntityVariant.ALIAS:
                    if (!_displayOptions.DisplayAliases) return false;
                    break;
                case EntityVariant.PROXY:
                    if (!_displayOptions.DisplayProxies) return false;
                    break;
                case EntityVariant.FUNCTION:
                    if (!_displayOptions.DisplayFunctions) return false;
                    break;
                case EntityVariant.VARIABLE:
                    if (!_displayOptions.DisplayVariables) return false;
                    break;
                default:
                    return false;
            }
            return PassesFunctionTypeFilter(entity);
        }

        private bool PassesFunctionTypeFilter(Entity entity)
        {
            if (_displayOptions == null || _displayOptions.AllowedFunctionTypes == null || _displayOptions.AllowedFunctionTypes.Count == 0)
                return true;
            if (entity == null || entity.variant != EntityVariant.FUNCTION)
                return true;

            FunctionEntity fe = (FunctionEntity)entity;
            // Keep composite instances so the hierarchy picker can navigate into them
            if (!fe.function.IsFunctionType)
                return true;

            for (int i = 0; i < _displayOptions.AllowedFunctionTypes.Count; i++)
            {
                if (fe.function == _displayOptions.AllowedFunctionTypes[i])
                    return true;
            }
            return false;
        }

        public bool RemoveEntity(Entity entity)
        {
            if (entity == null)
                return false;

            return RemoveEntity(entity.shortGUID);
        }

        public bool RemoveEntity(ShortGuid entityId)
        {
            if (_composite == null)
                return false;

            ListViewItem matchedItem = FindItem(entityId);
            if (matchedItem == null)
                return false;

            Content?.RemoveCachedEntity(matchedItem.Tag as Entity, _composite);

            bool wasSelected = matchedItem.Selected;
            int groupIndex = matchedItem.Group == null ? -1 : composite_content.Groups.IndexOf(matchedItem.Group);
            composite_content.Items.Remove(matchedItem);
            _itemsById.Remove(entityId);
            if (groupIndex >= 0 && groupIndex < _orderedByGroup.Length)
                _orderedByGroup[groupIndex].Remove(matchedItem);
            //The row colours after the gap are put right by the theme's own watch on the list
            if (wasSelected)
                SelectedEntityChanged?.Invoke(SelectedEntity);

            return true;
        }

        /* Focus the entity list */
        public void FocusOnList()
        {
            composite_content.Focus();
        }

        private void PopulateEntities(List<Entity> entities)
        {
            bool hasID = composite_content.Columns.ContainsKey("ID");
            bool showID = SettingsManager.GetBool(Settings.ShowShortGuids);
            if (showID && !hasID)
                composite_content.Columns.Add(new ColumnHeader() { Name = "ID", Text = IdColumnTitle, Width = 100 });
            else if (!showID && hasID)
                composite_content.Columns.RemoveByKey("ID");
            _hasIdColumn = showID;
            UpdateSortGlyphs();

            //Every row is made first so the lot can be ordered before anything reaches the control
            List<Row> rows = new List<Row>(entities.Count);
            for (int i = 0; i < entities.Count; i++)
            {
                if (IsDisplayable(entities[i]))
                    rows.Add(CreateRow(entities[i]));
            }
            rows.Sort(CompareRows);

            composite_content.BeginUpdate();
            composite_content.SuspendLayout();
            ClearGroupsAndItems();
            AddRows(rows);
            //Rows added to an empty list are drawn in the order they went in, so no sort is needed here -
            //and any that a recent addition asked for is stale, since this order is the one that counts
            _sortGeneration++;
            composite_content.EndUpdate();
            composite_content.ResumeLayout();
            ThemeListView.RowsColoured(composite_content);
        }

        /* Items.Clear takes each row out of its group as it goes, so the groups are empty by the time
           they are cleared here and that costs nothing. Emptying them first instead was measured at 90 ms
           on 4,500 rows: each row leaving a group is a native item update. */
        private void ClearGroupsAndItems()
        {
            composite_content.Items.Clear();
            for (int g = 0; g < composite_content.Groups.Count; g++)
                composite_content.Groups[g].Items.Clear();
            for (int g = 0; g < _orderedByGroup.Length; g++)
                _orderedByGroup[g].Clear();
            _itemsById.Clear();
        }

        /* ---- Sorting ---- */

        /* A row on its way in: the item, plus what the sort needs so nothing is re-read from the control */
        private struct Row
        {
            public ListViewItem Item;
            public ShortGuid Id;
            public int Group;
            public string Key;
            public string Name;
            public string Type;
            public string IdText;
        }

        private static void EnsureSortLoaded()
        {
            if (_sortLoaded)
                return;
            _sortLoaded = true;

            int column = SettingsManager.GetInteger(Settings.EntityListSortColumn, (int)SortColumn.Name);
            _sortColumn = Enum.IsDefined(typeof(SortColumn), column) ? (SortColumn)column : SortColumn.Name;
            _sortAscending = SettingsManager.GetBool(Settings.EntityListSortAscending, true);
        }

        /* Sorting by the ID column while it is hidden would order the list by something unseen. Read per
           comparison, so it answers from the flag the last population set rather than scanning columns. */
        private SortColumn EffectiveSortColumn
        {
            get
            {
                if (_sortColumn == SortColumn.Id && !_hasIdColumn)
                    return SortColumn.Name;
                return _sortColumn;
            }
        }

        private static string SortKey(ListViewItem item, SortColumn column)
        {
            switch (column)
            {
                case SortColumn.Type:
                    return item.SubItems.Count > 1 ? item.SubItems[1].Text : "";
                case SortColumn.Id:
                    //The id is the last sub item whether or not its column is showing
                    return item.SubItems.Count > 2 ? item.SubItems[item.SubItems.Count - 1].Text : "";
                //NOTE: an id is compared as the string it shows (see CompareRows), not through the
                //natural compare - it is four hex bytes, and reading its digit runs as decimal numbers
                //puts 10-.. after 9A-.. and 05-.. after 5A-.., which is an order nobody can follow.
                default:
                    return item.Text;
            }
        }

        private Row CreateRow(Entity entity)
        {
            (int imageIndex, int groupIndex) = EditorUtils.GetIndexesForListViewItem(entity, _composite, Content.Level.Commands);
            ListViewItem item = (ListViewItem)Content.GenerateListViewItem(entity, _composite).Clone();
            item.ImageIndex = imageIndex;
            return new Row
            {
                Item = item,
                Id = entity.shortGUID,
                Group = groupIndex,
                Key = SortKey(item, EffectiveSortColumn),
                Name = item.Text,
                Type = SortKey(item, SortColumn.Type),
                IdText = SortKey(item, SortColumn.Id),
            };
        }

        private Row RowOf(ListViewItem item, int groupIndex)
        {
            return new Row
            {
                Item = item,
                Id = item.Tag is Entity entity ? entity.shortGUID : ShortGuid.Invalid,
                Group = groupIndex,
                Key = SortKey(item, EffectiveSortColumn),
                Name = item.Text,
                Type = SortKey(item, SortColumn.Type),
                IdText = SortKey(item, SortColumn.Id),
            };
        }

        /* Group first (the fixed group order), then the sorted column; rows that tie on it - most of a
           Type sort, and the aliases that share a name - fall back to name, then type, then the id as it
           is shown, so the order is the same from one population to the next.

           Names are compared naturally (light_2 before light_10); ids are compared as plain strings,
           since they are hex and their digits are not decimal numbers. */
        private int CompareRows(Row a, Row b)
        {
            if (a.Group != b.Group)
                return a.Group < b.Group ? -1 : 1;

            int order = EffectiveSortColumn == SortColumn.Id
                ? string.CompareOrdinal(a.Key, b.Key)
                : NaturalCompare(a.Key, b.Key);
            if (order != 0)
                return _sortAscending ? order : -order;

            order = NaturalCompare(a.Name, b.Name);
            if (order != 0)
                return order;

            order = NaturalCompare(a.Type, b.Type);
            if (order != 0)
                return order;

            return string.CompareOrdinal(a.IdText, b.IdText);
        }

        /* Case-insensitive, with runs of digits compared by value, so light_2 sits before light_10 */
        private static int NaturalCompare(string a, string b)
        {
            if (ReferenceEquals(a, b))
                return 0;
            if (a == null)
                return -1;
            if (b == null)
                return 1;

            int i = 0;
            int j = 0;
            while (i < a.Length && j < b.Length)
            {
                char ca = a[i];
                char cb = b[j];
                if (ca >= '0' && ca <= '9' && cb >= '0' && cb <= '9')
                {
                    int startA = i;
                    int startB = j;
                    while (i < a.Length && a[i] == '0') i++;
                    while (j < b.Length && b[j] == '0') j++;
                    int digitsA = i;
                    int digitsB = j;
                    while (i < a.Length && a[i] >= '0' && a[i] <= '9') i++;
                    while (j < b.Length && b[j] >= '0' && b[j] <= '9') j++;

                    int lengthA = i - digitsA;
                    int lengthB = j - digitsB;
                    if (lengthA != lengthB)
                        return lengthA < lengthB ? -1 : 1;
                    for (int k = 0; k < lengthA; k++)
                    {
                        if (a[digitsA + k] != b[digitsB + k])
                            return a[digitsA + k] < b[digitsB + k] ? -1 : 1;
                    }

                    //Same value: the one written with fewer leading zeros first
                    int zerosA = digitsA - startA;
                    int zerosB = digitsB - startB;
                    if (zerosA != zerosB)
                        return zerosA < zerosB ? -1 : 1;
                    continue;
                }

                char ua = char.ToUpperInvariant(ca);
                char ub = char.ToUpperInvariant(cb);
                if (ua != ub)
                    return ua < ub ? -1 : 1;
                i++;
                j++;
            }

            int remainingA = a.Length - i;
            int remainingB = b.Length - j;
            if (remainingA != remainingB)
                return remainingA < remainingB ? -1 : 1;
            return 0;
        }

        /* Put ordered rows on the control. Group membership goes on in bulk per group: the per-item
           setter checks the group for a duplicate first, which is a scan of everything added so far. */
        private void AddRows(List<Row> rows)
        {
            ListViewItem[] items = new ListViewItem[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                ListViewItem item = rows[i].Item;
                items[i] = item;
                if (ThemeListView.RowColours(i, out Color back, out Color fore))
                {
                    item.BackColor = back;
                    item.ForeColor = fore;
                }
                _itemsById[rows[i].Id] = item;
                _orderedByGroup[rows[i].Group].Add(item);
            }

            int start = 0;
            while (start < rows.Count)
            {
                int end = start;
                while (end < rows.Count && rows[end].Group == rows[start].Group)
                    end++;

                ListViewItem[] groupItems = new ListViewItem[end - start];
                Array.Copy(items, start, groupItems, 0, groupItems.Length);
                composite_content.Groups[rows[start].Group].Items.AddRange(groupItems);
                start = end;
            }

            composite_content.Items.AddRange(items);
        }

        /* Every row in drawn order, for keyboard navigation across the group boundaries */
        private List<ListViewItem> DisplayOrder()
        {
            List<ListViewItem> rows = new List<ListViewItem>(_itemsById.Count);
            for (int g = 0; g < _orderedByGroup.Length; g++)
                rows.AddRange(_orderedByGroup[g]);
            return rows;
        }

        /* One row into its sorted place: binary search over the group's rows, then insert at the native
           index of the row it lands in front of, so what is drawn and the group's own order agree */
        private void InsertSorted(Row row)
        {
            ListViewGroup group = composite_content.Groups[row.Group];
            List<ListViewItem> live = _orderedByGroup[row.Group];

            int low = 0;
            int high = live.Count;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (CompareRows(RowOf(live[mid], row.Group), row) <= 0)
                    low = mid + 1;
                else
                    high = mid;
            }

            /* Where the row goes on the control is not a matter of the index it is given: a grouped
               ListView paints a row that arrives after the group was filled at the END of that group,
               whatever its item index (measured - see lvbench). The order lives here, and the control is
               told about it by sorting, which is also the only native way to place a row within a group. */
            row.Item.Group = group;
            live.Insert(low, row.Item);
            composite_content.Items.Add(row.Item);
            _itemsById[row.Id] = row.Item;
            QueueSortToControl();
        }

        /* Sort the control to the order kept here. One re-sort covers however many rows were added or
           renamed since, so a paste of fifty entities pays for it once rather than fifty times. */
        private void QueueSortToControl()
        {
            if (_sortPosted)
                return;

            if (IsDisposed || !IsHandleCreated)
            {
                //Nothing to post to: put the control in order now (or at the next population, if empty)
                ApplyOrderToControl();
                return;
            }

            int generation = ++_sortGeneration;
            _sortPosted = true;
            try
            {
                BeginInvoke(new MethodInvoker(() =>
                {
                    _sortPosted = false;
                    //A repopulate or a column sort got there first, and left the control in order
                    if (generation != _sortGeneration || IsDisposed)
                        return;
                    ApplyOrderToControl();
                }));
            }
            catch (Exception)
            {
                _sortPosted = false;
                ApplyOrderToControl();
            }
        }

        /* Put the control's rows in the order held in _orderedByGroup, and colour them for it.
           LVM_SORTITEMS moves the rows in place, so the selection and the focus survive it. */
        private void ApplyOrderToControl()
        {
            if (composite_content.IsDisposed || composite_content.Items.Count == 0)
                return;

            Dictionary<ListViewItem, int> rank = new Dictionary<ListViewItem, int>(composite_content.Items.Count);
            int position = 0;
            for (int g = 0; g < _orderedByGroup.Length; g++)
            {
                foreach (ListViewItem item in _orderedByGroup[g])
                {
                    rank[item] = position;
                    if (ThemeListView.RowColours(position, out Color back, out Color fore))
                    {
                        item.BackColor = back;
                        item.ForeColor = fore;
                    }
                    position++;
                }
            }

            _suppressSelectionEvents = true;
            composite_content.BeginUpdate();
            try
            {
                //Setting a sorter sorts; it is taken off again so nothing else is sorted behind our back
                composite_content.ListViewItemSorter = new RankComparer(rank);
                composite_content.ListViewItemSorter = null;
            }
            finally
            {
                composite_content.EndUpdate();
                _suppressSelectionEvents = false;
            }

            _sortGeneration++;
            ThemeListView.RowsColoured(composite_content);
        }

        /* A row whose text changed may no longer sit where the sort put it: move it if not */
        private void RepositionItem(ListViewItem item)
        {
            if (item == null || item.ListView != composite_content || item.Group == null)
                return;

            int groupIndex = composite_content.Groups.IndexOf(item.Group);
            if (groupIndex < 0)
                return;

            Row row = RowOf(item, groupIndex);
            List<ListViewItem> live = _orderedByGroup[groupIndex];
            int at = live.IndexOf(item);
            if (at < 0)
                return;

            bool inPlace = (at == 0 || CompareRows(RowOf(live[at - 1], groupIndex), row) <= 0)
                && (at == live.Count - 1 || CompareRows(row, RowOf(live[at + 1], groupIndex)) <= 0);
            if (inPlace)
                return;

            //Only the order kept here changes; the control is sorted to match, which leaves the row
            //itself (and so its selection and focus) alone
            live.RemoveAt(at);
            int destination = 0;
            int end = live.Count;
            while (destination < end)
            {
                int mid = (destination + end) / 2;
                if (CompareRows(RowOf(live[mid], groupIndex), row) <= 0)
                    destination = mid + 1;
                else
                    end = mid;
            }
            live.Insert(destination, item);
            QueueSortToControl();
        }

        private SortColumn ColumnAt(int index)
        {
            if (index < 0 || index >= composite_content.Columns.Count)
                return SortColumn.Name;

            ColumnHeader header = composite_content.Columns[index];
            if (header == EntityType)
                return SortColumn.Type;
            if (header.Name == "ID")
                return SortColumn.Id;
            return SortColumn.Name;
        }

        private void composite_content_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            SortColumn column = ColumnAt(e.Column);
            if (column == _sortColumn)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = column;
                _sortAscending = true;
            }

            SettingsManager.SetInteger(Settings.EntityListSortColumn, (int)_sortColumn);
            SettingsManager.SetBool(Settings.EntityListSortAscending, _sortAscending);
            ResortItems();
        }

        /* The column headers carry the sort: an arrow on the sorted one */
        private void UpdateSortGlyphs()
        {
            string glyph = _sortAscending ? " \u25B2" : " \u25BC";
            SortColumn column = EffectiveSortColumn;
            EntityName.Text = NameColumnTitle + (column == SortColumn.Name ? glyph : "");
            EntityType.Text = TypeColumnTitle + (column == SortColumn.Type ? glyph : "");
            ColumnHeader id = composite_content.Columns["ID"];
            if (id != null)
                id.Text = IdColumnTitle + (column == SortColumn.Id ? glyph : "");
        }

        /* Re-order what is already listed without rebuilding it. The rows are sorted here, and the
           control is then asked to sort itself to the same order: LVM_SORTITEMS moves rows in place, so
           the selection, the focus and the row colours survive, and it costs a comparison callback per
           pair rather than a delete and an insert per row - a tenth of the time on a big composite. */
        private void ResortItems()
        {
            UpdateSortGlyphs();
            if (composite_content.Items.Count == 0)
                return;

            List<Row> rows = new List<Row>(_itemsById.Count);
            for (int g = 0; g < _orderedByGroup.Length; g++)
            {
                foreach (ListViewItem item in _orderedByGroup[g])
                    rows.Add(RowOf(item, g));
            }
            if (rows.Count != composite_content.Items.Count)
            {
                //The order kept here has lost step with the control: build the list again
                DoSearch();
                return;
            }
            rows.Sort(CompareRows);

            for (int g = 0; g < _orderedByGroup.Length; g++)
                _orderedByGroup[g].Clear();
            for (int i = 0; i < rows.Count; i++)
                _orderedByGroup[rows[i].Group].Add(rows[i].Item);
            ApplyOrderToControl();

            ListViewItem show = composite_content.FocusedItem;
            if (show == null && composite_content.SelectedItems.Count > 0)
                show = composite_content.SelectedItems[0];
            if (show != null)
                show.EnsureVisible();
            else
                composite_content.EnsureVisible(0);
        }

        /* Puts the control's rows in the order the rows list already has */
        private sealed class RankComparer : System.Collections.IComparer
        {
            private readonly Dictionary<ListViewItem, int> _rank;

            public RankComparer(Dictionary<ListViewItem, int> rank)
            {
                _rank = rank;
            }

            public int Compare(object x, object y)
            {
                int a = x is ListViewItem itemX && _rank.TryGetValue(itemX, out int rankX) ? rankX : int.MaxValue;
                int b = y is ListViewItem itemY && _rank.TryGetValue(itemY, out int rankY) ? rankY : int.MaxValue;
                return a.CompareTo(b);
            }
        }

        private void composite_content_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressSelectionEvents)
                return;

            if (composite_content.MultiSelect && composite_content.SelectedItems.Count > 1)
            {
                SelectedEntitiesChanged?.Invoke(SelectedEntities);
                return;
            }
            SelectedEntityChanged?.Invoke(SelectedEntity);
        }

        private void entity_search_box_TextChanged(object sender, EventArgs e)
        {
            if (entity_search_box.Text == _currentSearch) return;
            _currentSearch = entity_search_box.Text;

            clearSearchBtn.Visible = _currentSearch != "";

            int caret = entity_search_box.SelectionStart;
            DoSearch();

            // PopulateEntities can move focus; keep typing in the search box.
            if (!entity_search_box.Focused)
                entity_search_box.Focus();
            entity_search_box.SelectionStart = Math.Min(caret, entity_search_box.Text.Length);
            entity_search_box.SelectionLength = 0;
        }

        private void clearSearchBtn_Click(object sender, EventArgs e)
        {
            if (entity_search_box.Text == "" && _currentSearch == "")
                return;

            ClearSearch();
            DoSearch();
        }

        private void DoSearch()
        {
            List<Entity> allEntities = GetDisplayableEntities();
            List<Entity> filteredEntities = new List<Entity>();

            //NOTE: we look at current search, NOT the text in the textbox - we want to respect the user's button click when reloading
            if (_currentSearch == "")
            {
                filteredEntities = allEntities;
            }
            else
            {
                for (int i = 0; i < allEntities.Count; i++)
                {
                    ListViewItem item = Content.GenerateListViewItem(allEntities[i], _composite);
                    for (int x = 0; x < item.SubItems.Count; x++)
                    {
                        if (!item.SubItems[x].Text.ToUpper().Replace(" ", "").Contains(_currentSearch.ToUpper().Replace(" ", "")))
                            continue;

                        //If entity IDs column is hidden, we should ignore it in the search
                        if (x == item.SubItems.Count - 1 && !SettingsManager.GetBool(Settings.ShowShortGuids))
                            continue;

                        filteredEntities.Add(allEntities[i]);
                        break;
                    }
                }
            }

            PopulateEntities(filteredEntities);
        }

        private void ClearSearch()
        {
            _currentSearch = "";
            entity_search_box.Text = "";
            clearSearchBtn.Visible = false;
        }

        private List<Entity> GetDisplayableEntities()
        {
            List<Entity> entities = new List<Entity>();
            if (_displayOptions.DisplayAliases)
                entities.AddRange(_composite.aliases);
            if (_displayOptions.DisplayProxies)
                entities.AddRange(_composite.proxies);
            if (_displayOptions.DisplayFunctions)
                entities.AddRange(_composite.functions);
            if (_displayOptions.DisplayVariables)
                entities.AddRange(_composite.variables);
            return entities;
        }

        public class DisplayOptions
        {
            public bool ShowCheckboxes = false;

            public bool DisplayAliases = true;
            public bool DisplayProxies = true;
            public bool DisplayFunctions = true;
            public bool DisplayVariables = true;

            /// <summary>
            /// When set, only FunctionEntities of these types are listed.
            /// Composite instances (non-function-type) are still shown so you can navigate into them.
            /// </summary>
            public List<FunctionType> AllowedFunctionTypes = null;
        }
    }
}
