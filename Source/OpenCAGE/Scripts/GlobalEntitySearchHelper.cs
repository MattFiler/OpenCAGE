using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace OpenCAGE.Scripts
{
    public readonly struct SearchResultTag
    {
        public Entity Entity { get; }
        public Composite Composite { get; }

        public SearchResultTag(Entity entity, Composite composite)
        {
            Entity = entity;
            Composite = composite;
        }
    }

    public static class GlobalEntitySearchHelper
    {
        public static void SetupEntityListColumns(ListView entityList, bool showId)
        {
            bool hasId = entityList.Columns.ContainsKey("ID");
            if (showId && !hasId)
                entityList.Columns.Add(new ColumnHeader() { Name = "ID", Text = "ID", Width = 100 });
            else if (!showId && hasId)
                entityList.Columns.RemoveByKey("ID");
        }

        public static int SearchByName(LevelContent content, string name, ListView entityList, Dictionary<Entity, Composite> entityComposites)
        {
            return SearchByName(content, name, entityList, entityComposites, GlobalEntitySearchScopeSettings.Scope);
        }

        public static int SearchByName(LevelContent content, string name, ListView entityList, Dictionary<Entity, Composite> entityComposites, GlobalEntitySearchScope scope)
        {
            if (string.IsNullOrWhiteSpace(name))
                return 0;

            bool showId = SettingsManager.GetBool(Settings.ShowShortGuids);
            entityComposites.Clear();

            entityList.BeginUpdate();
            entityList.Items.Clear();
            entityList.Groups.Clear();

            string normalizedName = name.ToUpper().Replace(" ", "");
            foreach (Composite comp in GetCompositesInScope(content, scope))
            {
                List<Entity> ents = comp.GetEntities().FindAll(o =>
                    content.Level.Commands.Utils.GetEntityName(comp, o).ToUpper().Replace(" ", "").Contains(normalizedName)
                    || (showId && o.shortGUID.ToByteString().Replace("-", "").Contains(name.Replace("-", ""))));

                if (ents.Count == 0)
                    continue;

                entityList.Groups.Add(new ListViewGroup() { Header = comp.name });
                foreach (Entity ent in ents)
                {
                    ListViewItem item = (ListViewItem)content.GenerateListViewItem(ent, comp).Clone();
                    item.Tag = new SearchResultTag(ent, comp);
                    item.Group = entityList.Groups[entityList.Groups.Count - 1];
                    item.ImageIndex = EditorUtils.GetIndexesForListViewItem(ent, comp, content.Level.Commands).Item1;
                    entityList.Items.Add(item);
                    entityComposites.Add(ent, comp);
                }
            }

            entityList.EndUpdate();
            return entityList.Items.Count;
        }

        public static int SearchByFunction(LevelContent content, ShortGuid functionGuid, ListView entityList, Dictionary<Entity, Composite> entityComposites)
        {
            return SearchByFunction(content, functionGuid, entityList, entityComposites, GlobalEntitySearchScopeSettings.Scope);
        }

        public static int SearchByFunction(LevelContent content, ShortGuid functionGuid, ListView entityList, Dictionary<Entity, Composite> entityComposites, GlobalEntitySearchScope scope)
        {
            FunctionType? functionType = functionGuid.IsFunctionType ? functionGuid.AsFunctionType : (FunctionType?)null;
            return SearchByFunction(content, functionType, functionGuid, entityList, entityComposites, scope);
        }

        public static int SearchByFunction(LevelContent content, FunctionType functionType, ListView entityList, Dictionary<Entity, Composite> entityComposites)
        {
            return SearchByFunction(content, functionType, new ShortGuid((uint)functionType), entityList, entityComposites, GlobalEntitySearchScopeSettings.Scope);
        }

        private static int SearchByFunction(LevelContent content, FunctionType? functionType, ShortGuid functionGuid, ListView entityList, Dictionary<Entity, Composite> entityComposites, GlobalEntitySearchScope scope)
        {
            entityComposites.Clear();

            entityList.BeginUpdate();
            entityList.Items.Clear();
            entityList.Groups.Clear();

            foreach (Composite comp in GetCompositesInScope(content, scope))
            {
                List<FunctionEntity> funcs = comp.functions.FindAll(o =>
                    functionType.HasValue
                        ? o.function.IsFunctionType && o.function.AsFunctionType == functionType.Value
                        : o.function == functionGuid);
                if (funcs.Count == 0)
                    continue;

                entityList.Groups.Add(new ListViewGroup() { Header = comp.name });
                foreach (FunctionEntity ent in funcs)
                {
                    ListViewItem item = (ListViewItem)content.GenerateListViewItem(ent, comp).Clone();
                    item.Tag = new SearchResultTag(ent, comp);
                    item.Group = entityList.Groups[entityList.Groups.Count - 1];
                    item.ImageIndex = ent.function.IsFunctionType ? 1 : 2;
                    entityList.Items.Add(item);
                    entityComposites.Add(ent, comp);
                }
            }

            entityList.EndUpdate();
            return entityList.Items.Count;
        }

        public static bool RemoveDeletedEntityFromResults(
            Entity entity,
            ListView entityList,
            Dictionary<Entity, Composite> entityComposites)
        {
            if (entity == null || entityList == null || entityComposites == null)
                return false;

            ShortGuid entityId = entity.shortGUID;
            List<Entity> staleKeys = new List<Entity>();
            foreach (KeyValuePair<Entity, Composite> pair in entityComposites)
            {
                if (pair.Key?.shortGUID == entityId)
                    staleKeys.Add(pair.Key);
            }

            if (staleKeys.Count == 0)
                return false;

            entityList.BeginUpdate();
            try
            {
                for (int i = entityList.Items.Count - 1; i >= 0; i--)
                {
                    Entity listedEntity = GetEntityFromListTag(entityList.Items[i].Tag);
                    if (listedEntity != null && listedEntity.shortGUID == entityId)
                    {
                        entityList.Items.RemoveAt(i);
                    }
                }

                foreach (Entity staleKey in staleKeys)
                    entityComposites.Remove(staleKey);
            }
            finally
            {
                entityList.EndUpdate();
            }

            return true;
        }

        public static void JumpToSelectedEntity(ListView entityList, Dictionary<Entity, Composite> entityComposites)
        {
            JumpToSelectedEntity(entityList, entityComposites, GlobalEntitySearchScopeSettings.Scope);
        }

        public static void JumpToSelectedEntity(ListView entityList, Dictionary<Entity, Composite> entityComposites, GlobalEntitySearchScope scope)
        {
            if (entityList.SelectedItems.Count == 0)
                return;

            if (!TryResolveSelectedSearchResult(entityList.SelectedItems[0].Tag, entityComposites, out Entity selected, out Composite composite))
                return;

            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            Composite currentComposite = display?.Populated == true ? display.Composite : null;
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;

            if (scope == GlobalEntitySearchScope.CurrentComposite
                && currentComposite != null
                && display != null)
            {
                if (composite.shortGUID == currentComposite.shortGUID)
                {
                    display.LoadEntity(selected, true);
                    return;
                }

                Singleton.Editor?.CompositeBrowser?.LoadCompositeAndEntity(composite, selected);
                return;
            }

            if (scope == GlobalEntitySearchScope.CurrentCompositeAndNested
                && currentComposite != null
                && commands != null
                && display != null)
            {
                Composite entryComposite = GetHierarchyEntryComposite(display, currentComposite);

                if (!IsHierarchyEntryReachable(display, entryComposite))
                {
                    display = Singleton.Editor?.CompositeBrowser?.LoadComposite(entryComposite);
                    if (display == null)
                    {
                        Singleton.Editor?.CompositeBrowser?.LoadCompositeAndEntity(composite, selected);
                        return;
                    }
                }

                List<uint> pathGuids = new List<uint>();

                if (composite.shortGUID != entryComposite.shortGUID)
                {
                    if (!TryFindCompositeInstancePath(entryComposite, composite, commands, out List<Entity> instancePath))
                    {
                        Singleton.Editor?.CompositeBrowser?.LoadCompositeAndEntity(composite, selected);
                        return;
                    }

                    foreach (Entity stepEntity in instancePath)
                        pathGuids.Add(stepEntity.shortGUID.AsUInt32);
                }

                pathGuids.Add(selected.shortGUID.AsUInt32);

                if (display.ApplyViewerSelectionPath(
                    entryComposite,
                    pathGuids,
                    selectLeafEntity: true,
                    entity => GetChildCompositeForSearch(entity, commands)))
                {
                    return;
                }

                if (display.TrySelectLeafEntityInCurrentComposite(pathGuids))
                    return;
            }

            Singleton.Editor?.CompositeBrowser?.LoadCompositeAndEntity(composite, selected);
        }

        public static bool ShouldRefreshSearchForCompositeChange(GlobalEntitySearchScope scope)
        {
            return scope == GlobalEntitySearchScope.CurrentComposite
                || scope == GlobalEntitySearchScope.CurrentCompositeAndNested;
        }

        private static Entity GetEntityFromListTag(object tag)
        {
            if (tag is SearchResultTag searchResult)
                return searchResult.Entity;
            return tag as Entity;
        }

        private static bool TryResolveSelectedSearchResult(
            object tag,
            Dictionary<Entity, Composite> entityComposites,
            out Entity selected,
            out Composite composite)
        {
            selected = null;
            composite = null;

            if (tag is SearchResultTag searchResult)
            {
                selected = searchResult.Entity;
                composite = searchResult.Composite;
                return selected != null && composite != null;
            }

            selected = tag as Entity;
            if (!TryResolveEntityComposite(selected, entityComposites, out composite))
                return false;

            return true;
        }

        private static Composite GetHierarchyEntryComposite(CompositeDisplay display, Composite currentComposite)
        {
            if (display?.Path?.AllComposites != null && display.Path.AllComposites.Count > 0)
                return display.Path.AllComposites[0];
            return currentComposite;
        }

        private static bool IsHierarchyEntryReachable(CompositeDisplay display, Composite entryComposite)
        {
            if (display == null || entryComposite == null || !display.Populated)
                return false;

            if (display.Composite?.shortGUID == entryComposite.shortGUID)
                return true;

            if (display.Path?.AllComposites == null)
                return false;

            foreach (Composite composite in display.Path.AllComposites)
            {
                if (composite.shortGUID == entryComposite.shortGUID)
                    return true;
            }

            return false;
        }

        private static bool TryResolveEntityComposite(Entity selected, Dictionary<Entity, Composite> entityComposites, out Composite composite)
        {
            composite = null;
            if (selected == null || entityComposites == null)
                return false;

            if (entityComposites.TryGetValue(selected, out composite))
                return true;

            foreach (KeyValuePair<Entity, Composite> pair in entityComposites)
            {
                if (pair.Key?.shortGUID == selected.shortGUID)
                {
                    composite = pair.Value;
                    return true;
                }
            }

            return false;
        }

        private static Composite GetChildCompositeForSearch(Entity entity, Commands commands)
        {
            if (entity == null || entity.variant != EntityVariant.FUNCTION || commands == null)
                return null;

            FunctionEntity function = (FunctionEntity)entity;
            if (function.function.IsFunctionType)
                return null;

            return commands.GetComposite(function.function);
        }

        private static IEnumerable<Composite> GetCompositesInScope(LevelContent content, GlobalEntitySearchScope scope)
        {
            if (content?.Level?.Commands == null)
                yield break;

            Composite currentComposite = Singleton.Editor?.CompositeDisplay?.Populated == true
                ? Singleton.Editor.CompositeDisplay.Composite
                : null;

            if (scope == GlobalEntitySearchScope.AllComposites || currentComposite == null)
            {
                foreach (Composite comp in content.Level.Commands.Entries)
                    yield return comp;
                yield break;
            }

            if (scope == GlobalEntitySearchScope.CurrentComposite)
            {
                yield return currentComposite;
                yield break;
            }

            HashSet<ShortGuid> visited = new HashSet<ShortGuid>();
            foreach (Composite comp in CollectNestedComposites(currentComposite, content.Level.Commands, visited))
                yield return comp;
        }

        private static List<Composite> CollectNestedComposites(Composite root, Commands commands, HashSet<ShortGuid> visited)
        {
            List<Composite> composites = new List<Composite>();
            CollectNestedCompositesRecursive(root, commands, visited, composites);
            return composites;
        }

        private static void CollectNestedCompositesRecursive(Composite composite, Commands commands, HashSet<ShortGuid> visited, List<Composite> composites)
        {
            if (composite == null || !visited.Add(composite.shortGUID))
                return;

            composites.Add(composite);

            foreach (FunctionEntity function in composite.functions)
            {
                if (function.function.IsFunctionType)
                    continue;

                Composite child = commands.GetComposite(function.function);
                if (child != null)
                    CollectNestedCompositesRecursive(child, commands, visited, composites);
            }
        }

        private static bool TryFindCompositeInstancePath(Composite start, Composite target, Commands commands, out List<Entity> instancePath)
        {
            instancePath = null;
            if (start == null || target == null)
                return false;

            if (start.shortGUID == target.shortGUID)
            {
                instancePath = new List<Entity>();
                return true;
            }

            Queue<(Composite composite, List<Entity> path)> queue = new Queue<(Composite, List<Entity>)>();
            HashSet<ShortGuid> visited = new HashSet<ShortGuid>();
            queue.Enqueue((start, new List<Entity>()));
            visited.Add(start.shortGUID);

            while (queue.Count > 0)
            {
                (Composite composite, List<Entity> path) = queue.Dequeue();
                foreach (FunctionEntity function in composite.functions)
                {
                    if (function.function.IsFunctionType)
                        continue;

                    Composite child = commands.GetComposite(function.function);
                    if (child == null)
                        continue;

                    List<Entity> newPath = new List<Entity>(path) { function };
                    if (child.shortGUID == target.shortGUID)
                    {
                        instancePath = newPath;
                        return true;
                    }

                    if (visited.Add(child.shortGUID))
                        queue.Enqueue((child, newPath));
                }
            }

            return false;
        }
    }
}
