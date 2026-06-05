using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.UserControls;
using DarkModeForms;
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

        public Composite Composite => _composite;
        private Composite _composite;

        protected LevelContent Content => Singleton.Editor?.CommandsDisplay?.Content;

        private string _currentSearch = "";
        private DisplayOptions _displayOptions;

        public CompositeEntityList()
        {
            InitializeComponent();
            ClearSearch();

            clearSearchBtn.BringToFront();

            this.Disposed += CompositeEntityList_Disposed;

            Singleton.OnEntityRenamed += OnEntityRenamed;
            Singleton.OnCompositeRenamed += OnCompositeRenamed;
        }

        private void CompositeEntityList_Disposed(object sender, EventArgs e)
        {
            this.Disposed -= CompositeEntityList_Disposed;

            Singleton.OnEntityRenamed -= OnEntityRenamed;
            Singleton.OnCompositeRenamed -= OnCompositeRenamed;

            composite_content.Items.Clear();
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
                DarkModeCS.TryRefreshThemedListView(composite_content);
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
                DarkModeCS.TryRefreshThemedListView(composite_content);
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
            return FindEntityIndex(entityId) != -1;
        }

        private int FindEntityIndex(ShortGuid entityId)
        {
            for (int i = 0; i < composite_content.Items.Count; i++)
            {
                if (composite_content.Items[i].Tag is Entity listedEntity
                    && listedEntity.shortGUID == entityId)
                    return i;
            }

            return -1;
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

            for (int i = 0; i < composite_content.Items.Count; i++)
            {
                if (composite_content.Items[i].Tag is Entity listedEntity
                    && listedEntity.shortGUID == entity.shortGUID)
                {
                    composite_content.Items[i].Selected = true;
                    return i;
                }
            }

            return -1;
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

            int index = FindEntityIndex(entity.shortGUID);
            if (index == -1)
                return false;

            ListViewItem existing = composite_content.Items[index];
            ListViewItem cached = Content.GenerateListViewItem(entity, _composite);

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
            _composite = composite;

            if (clearSearch) 
                ClearSearch();

            //By calling a search again, we won't necessarily show ALL entities when loading, but we'll respect the user's search, which is better
            DoSearch();
        }

        /* Add a new entity to the list */
        public void AddNewEntity(Entity entity, bool skipSanityChecks = false)
        {
            if (!skipSanityChecks)
            {
                if (entity.variant == EntityVariant.ALIAS && !_displayOptions.DisplayAliases)
                    return;
                if (entity.variant == EntityVariant.PROXY && !_displayOptions.DisplayProxies)
                    return;
                if (entity.variant == EntityVariant.FUNCTION && !_displayOptions.DisplayFunctions)
                    return;
                if (entity.variant == EntityVariant.VARIABLE && !_displayOptions.DisplayVariables)
                    return;
            }

            (int imageIndex, int groupIndex) = EditorUtils.GetIndexesForListViewItem(entity, _composite, Content.Level.Commands);
            ListViewItem item = (ListViewItem)Content.GenerateListViewItem(entity, _composite).Clone();
            item.ImageIndex = imageIndex;
            item.Group = composite_content.Groups[groupIndex];
            composite_content.Items.Add(item);
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

            ListViewItem matchedItem = null;
            Entity removedEntity = null;
            for (int i = 0; i < composite_content.Items.Count; i++)
            {
                if (composite_content.Items[i].Tag is Entity entity && entity.shortGUID == entityId)
                {
                    matchedItem = composite_content.Items[i];
                    removedEntity = entity;
                    break;
                }
            }

            if (matchedItem == null)
                return false;

            Content?.RemoveCachedEntity(removedEntity, _composite);

            bool wasSelected = composite_content.SelectedItems.Contains(matchedItem);
            composite_content.Items.Remove(matchedItem);
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
            bool showID = SettingsManager.GetBool(Singleton.Settings.ShowShortGuids);
            if (showID && !hasID)
                composite_content.Columns.Add(new ColumnHeader() { Name = "ID", Text = "ID", Width = 100 });
            else if (!showID && hasID)
                composite_content.Columns.RemoveByKey("ID");

            composite_content.BeginUpdate();
            composite_content.SuspendLayout();
            composite_content.Items.Clear();

            List<Entity> ents = entities.FindAll(entity =>
                (entity.variant == EntityVariant.ALIAS && _displayOptions.DisplayAliases) ||
                (entity.variant == EntityVariant.PROXY && _displayOptions.DisplayProxies) ||
                (entity.variant == EntityVariant.FUNCTION && _displayOptions.DisplayFunctions) ||
                (entity.variant == EntityVariant.VARIABLE && _displayOptions.DisplayVariables)
            );
            for (int i = 0; i < ents.Count; i++)
                AddNewEntity(ents[i], true);

            //composite_content.SetGroupState(ListViewGroupState.Collapsible);
            composite_content.EndUpdate();
            composite_content.ResumeLayout();
            DarkModeCS.TryRefreshThemedListView(composite_content);
        }

        private void composite_content_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedEntityChanged?.Invoke(SelectedEntity);
        }

        private void entity_search_box_TextChanged(object sender, EventArgs e)
        {
            if (entity_search_box.Text == _currentSearch) return;
            _currentSearch = entity_search_box.Text;

            clearSearchBtn.Visible = _currentSearch != "";

            DoSearch();
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
                        if (x == item.SubItems.Count - 1 && !SettingsManager.GetBool(Singleton.Settings.ShowShortGuids))
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
            public bool ShowApplyDefaults = false;

            public bool DisplayAliases = true;
            public bool DisplayProxies = true;
            public bool DisplayFunctions = true;
            public bool DisplayVariables = true;
        }
    }
}
