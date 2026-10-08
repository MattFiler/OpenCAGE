using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib.ObjectExtensions;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Linq;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.Undo
{
    /// <summary>
    /// A composite or folder placeholder was created. Undo takes it out again (with the layout rows a
    /// real composite was given); redo puts it back.
    /// </summary>
    public sealed class CompositeAddEdit : IEdit
    {
        private readonly Composite _composite;
        private readonly bool _folder;
        private FlowgraphLayoutManager.CompositeLayoutState _layouts = null;

        public string Label { get; }
        public ShortGuid CompositeId => ShortGuid.Invalid;
        public ShortGuid EntityId => ShortGuid.Invalid;

        public CompositeAddEdit(Composite composite, bool folder, string label)
        {
            _composite = composite;
            _folder = folder;
            Label = label;
        }

        public void Apply(UndoContext context)
        {
            Commands commands = Require(context);
            if (!commands.Entries.Contains(_composite))
                commands.Entries.Add(_composite);
            if (!_folder)
            {
                if (_layouts != null)
                    FlowgraphLayoutManager.RestoreCompositeState(_layouts);
                Singleton.OnCompositeAdded?.Invoke(_composite);
            }
            DirtyTracker.MarkLevelDataModified();
            context.Ui?.CompositesChanged();
        }

        public void Revert(UndoContext context)
        {
            Commands commands = Require(context);
            context.Ui?.BeforeCompositesRemoved(new HashSet<ShortGuid>() { _composite.shortGUID });
            if (!_folder)
                _layouts = FlowgraphLayoutManager.RemoveCompositeState(_composite);
            commands.Entries.Remove(_composite);
            if (!_folder)
                Singleton.OnCompositeDeleted?.Invoke(_composite);
            DirtyTracker.MarkLevelDataModified();
            context.Ui?.CompositesChanged();
        }

        internal static Commands Require(UndoContext context)
        {
            if (context.Commands == null)
                throw new InvalidOperationException("No level is loaded");
            return context.Commands;
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>
    /// Composites removed from the level, with everything that referred to them: the instance entities
    /// in every other composite, the links into those, the aliases that could no longer resolve (and the
    /// links into them), and the TriggerSequence entries and CAGEAnimation connections that reached into
    /// them. The objects are kept, so undo puts back exactly what went.
    /// </summary>
    /// <remarks>
    /// Whatever the edit left pointing at what went, the purge would take the next time its composite was
    /// opened, and a save of a composite's pages writes only what they draw - either way beyond the reach of
    /// undo. The pages of every composite it reached are kept too (one opened while the composites are gone
    /// has its pages saved without them), and the composite on screen is shown again after each step, as a
    /// refactor does: pages drawn from before the step would be compiled back over the links at the next save.
    /// </remarks>
    public sealed class CompositeDeleteEdit : IEdit
    {
        private struct EntryRecord
        {
            public Composite Composite;
            public int Index;
        }
        private struct FunctionRecord
        {
            public ShortGuid Owner;
            public FunctionEntity Function;
        }
        private struct LinkRecord
        {
            public ShortGuid Owner;
            public ShortGuid Entity;
            public int Index;
            public EntityConnector Link;
        }
        private struct EntityRecord
        {
            public ShortGuid Owner;
            public Entity Entity;
        }
        private struct PathEntryRecord
        {
            public ShortGuid Owner;
            public ShortGuid Function;
            public int Index;
            public object Entry; //A TriggerSequence.SequenceEntry or a CAGEAnimation.Connection
        }

        private readonly List<Composite> _composites;
        private List<EntryRecord> _entries;
        private List<FunctionRecord> _removedFunctions;
        private List<LinkRecord> _prunedLinks;
        private List<EntityRecord> _removedAliases;
        private List<EntityRecord> _removedProxies;
        private List<PathEntryRecord> _removedPathEntries;
        private Dictionary<ShortGuid, List<FlowgraphMeta>> _ownerPages;

        public string Label { get; }
        public ShortGuid CompositeId => ShortGuid.Invalid;
        public ShortGuid EntityId => ShortGuid.Invalid;

        public CompositeDeleteEdit(List<Composite> composites, string label)
        {
            _composites = composites;
            Label = label;
        }

        public void Apply(UndoContext context)
        {
            Commands commands = CompositeAddEdit.Require(context);
            CommandsUtils utils = commands.Utils;
            HashSet<ShortGuid> deletedIds = new HashSet<ShortGuid>(_composites.Select(o => o.shortGUID));

            //Whatever the live pages on screen hold goes into the links and the page table first, as stepping to another
            //composite would: the removals below read the links, and a composite they reach is shown again from its saved
            //pages. Whichever composite it is, as which ones they reach is only known once they are done.
            ShownDisplay()?.SaveAllFlowgraphs();

            context.Ui?.BeforeCompositesRemoved(deletedIds);

            _removedFunctions = new List<FunctionRecord>();
            _prunedLinks = new List<LinkRecord>();
            _removedAliases = new List<EntityRecord>();
            _removedProxies = new List<EntityRecord>();
            _removedPathEntries = new List<PathEntryRecord>();

            //Remove the instances of the deleted composites
            foreach (Composite entry in commands.Entries)
            {
                List<FunctionEntity> keep = new List<FunctionEntity>();
                foreach (FunctionEntity function in entry.functions)
                {
                    if (deletedIds.Contains(function.function))
                    {
                        _removedFunctions.Add(new FunctionRecord() { Owner = entry.shortGUID, Function = function });
                        continue;
                    }
                    keep.Add(function);
                }

                if (keep.Count != entry.functions_dictionary.Count)
                {
                    entry.functions_dictionary.Clear();
                    foreach (FunctionEntity function in keep)
                        entry.functions_dictionary[function.shortGUID] = function;
                }
            }

            //Remove aliases that can no longer resolve. Proxies into the deleted composites stay, as dead
            //proxies with their links (see CommandsUtils.IsDeadProxy): they are shown red and re-pointed,
            //not lost. _removedProxies is kept for undo records made before that was so.
            foreach (Composite entry in commands.Entries)
            {
                List<AliasEntity> aliases = entry.aliases.Where(o => !utils.CouldResolve(utils.ResolveAlias(o, entry))).ToList();
                foreach (AliasEntity alias in aliases)
                {
                    _removedAliases.Add(new EntityRecord() { Owner = entry.shortGUID, Entity = alias });
                    entry.aliases_dictionary.Remove(alias.shortGUID);
                }
            }

            //Links into what went, from everything left beside it: functions, variables, aliases and proxies alike. The
            //links held by what went stay on it, and come back with it.
            Dictionary<ShortGuid, HashSet<ShortGuid>> removedIn = new Dictionary<ShortGuid, HashSet<ShortGuid>>();
            void NoteRemoved(ShortGuid owner, ShortGuid entity)
            {
                if (!removedIn.TryGetValue(owner, out HashSet<ShortGuid> ids))
                    removedIn.Add(owner, ids = new HashSet<ShortGuid>());
                ids.Add(entity);
            }
            foreach (FunctionRecord record in _removedFunctions)
                NoteRemoved(record.Owner, record.Function.shortGUID);
            foreach (EntityRecord record in _removedAliases)
                NoteRemoved(record.Owner, record.Entity.shortGUID);
            foreach (Composite entry in commands.Entries)
            {
                if (!removedIn.TryGetValue(entry.shortGUID, out HashSet<ShortGuid> removed))
                    continue;
                foreach (Entity entity in entry.GetEntities())
                {
                    List<EntityConnector> kept = new List<EntityConnector>(entity.childLinks.Count);
                    for (int i = 0; i < entity.childLinks.Count; i++)
                    {
                        EntityConnector link = entity.childLinks[i];
                        if (removed.Contains(link.linkedEntityID) && entry.GetEntityByID(link.linkedEntityID) == null)
                        {
                            _prunedLinks.Add(new LinkRecord() { Owner = entry.shortGUID, Entity = entity.shortGUID, Index = i, Link = link });
                            continue;
                        }
                        kept.Add(link);
                    }
                    if (kept.Count != entity.childLinks.Count)
                        entity.childLinks = kept;
                }
            }

            //TriggerSequence entries and CAGEAnimation connections that reached into what went
            HashSet<ShortGuid> removedIds = new HashSet<ShortGuid>(removedIn.Values.SelectMany(o => o));
            if (removedIds.Count != 0)
            {
                foreach (Composite entry in commands.Entries)
                {
                    foreach (FunctionEntity function in entry.functions)
                    {
                        if (function is TriggerSequence trigger)
                            RemoveUnresolvable(entry, function, trigger.sequence, o => o.connectedEntity, removedIds, utils);
                        else if (function is CAGEAnimation animation)
                            RemoveUnresolvable(entry, function, animation.connections, o => o.connectedEntity, removedIds, utils);
                    }
                }
            }

            //The pages of every composite reached, as they are now - one opened while the composites are gone has its
            //pages saved without what went, and undo puts these back
            HashSet<ShortGuid> ownerIds = OwnerIds();
            List<Composite> owners = commands.Entries.Where(o => o != null && ownerIds.Contains(o.shortGUID)).ToList();
            _ownerPages = new Dictionary<ShortGuid, List<FlowgraphMeta>>();
            foreach (Composite owner in owners)
                _ownerPages[owner.shortGUID] = FlowgraphLayoutManager.GetLayouts(owner).Select(o => o.Copy()).ToList();

            //Remove the composites. Every place is taken before any goes, so undo puts them back in order (taken as each went,
            //a later one's place was counted with the earlier ones already out, and it came back one too early).
            _entries = _composites.Select(o => new EntryRecord() { Composite = o, Index = commands.Entries.IndexOf(o) }).ToList();
            foreach (Composite composite in _composites)
                commands.Entries.Remove(composite);
            utils.PurgedComposites.purged.Clear(); //TODO: we should smartly remove from this list, rather than removing all

            context.Ui?.CompositesChanged();
            context.Content?.EditorUtils?.GenerateCompositeInstances(commands);

            //Before the composites are announced gone: the step up out of a deleted composite saves the pages it leaves
            ReloadIfShown(owners, utils);

            foreach (Composite composite in _composites)
                Singleton.OnCompositeDeleted?.Invoke(composite);
            List<Composite> modified = owners.Where(o => !deletedIds.Contains(o.shortGUID)).ToList();
            if (modified.Count != 0)
                Singleton.OnCompositesModified?.Invoke(modified);
        }

        public void Revert(UndoContext context)
        {
            Commands commands = CompositeAddEdit.Require(context);

            foreach (EntryRecord record in _entries.OrderBy(o => o.Index))
            {
                if (!commands.Entries.Contains(record.Composite))
                    commands.Entries.Insert(Math.Min(Math.Max(record.Index, 0), commands.Entries.Count), record.Composite);
            }

            foreach (FunctionRecord record in _removedFunctions)
            {
                Composite owner = commands.GetComposite(record.Owner);
                if (owner != null && !owner.functions_dictionary.ContainsKey(record.Function.shortGUID))
                    owner.functions_dictionary.Add(record.Function.shortGUID, record.Function);
            }
            foreach (LinkRecord record in _prunedLinks)
            {
                Entity entity = commands.GetComposite(record.Owner)?.GetEntityByID(record.Entity);
                if (entity != null && !entity.childLinks.Any(o => o.ID == record.Link.ID))
                    entity.childLinks.Insert(Math.Min(record.Index, entity.childLinks.Count), record.Link);
            }
            foreach (EntityRecord record in _removedAliases)
            {
                Composite owner = commands.GetComposite(record.Owner);
                if (owner != null && record.Entity is AliasEntity alias && !owner.aliases_dictionary.ContainsKey(alias.shortGUID))
                    owner.aliases_dictionary.Add(alias.shortGUID, alias);
            }
            foreach (EntityRecord record in _removedProxies)
            {
                Composite owner = commands.GetComposite(record.Owner);
                if (owner != null && record.Entity is ProxyEntity proxy && !owner.proxies_dictionary.ContainsKey(proxy.shortGUID))
                    owner.proxies_dictionary.Add(proxy.shortGUID, proxy);
            }
            foreach (PathEntryRecord record in _removedPathEntries)
            {
                Entity function = commands.GetComposite(record.Owner)?.GetEntityByID(record.Function);
                if (function is TriggerSequence trigger && record.Entry is TriggerSequence.SequenceEntry entry && !trigger.sequence.Contains(entry))
                    trigger.sequence.Insert(Math.Min(record.Index, trigger.sequence.Count), entry);
                else if (function is CAGEAnimation animation && record.Entry is CAGEAnimation.Connection connection && !animation.connections.Contains(connection))
                    animation.connections.Insert(Math.Min(record.Index, animation.connections.Count), connection);
            }

            //The pages of the composites reached, as they were. Judged again when next shown, as a verdict given while the
            //composites were gone was given on less; and purged again first, as the links they hold are the ones from before.
            List<Composite> owners = new List<Composite>();
            foreach (KeyValuePair<ShortGuid, List<FlowgraphMeta>> pages in _ownerPages)
            {
                Composite owner = commands.GetComposite(pages.Key);
                if (owner == null)
                    continue;
                FlowgraphLayoutManager.RemoveAllLayouts(owner);
                foreach (FlowgraphMeta page in pages.Value)
                    FlowgraphLayoutManager.AddLayout(page.Copy());
                FlowgraphLayoutManager.ClearCompatibilityInfo(owner);
                commands.Utils.PurgedComposites.purged.Remove(owner.shortGUID);
                owners.Add(owner);
            }

            context.Content?.EditorUtils?.GenerateCompositeInstances(commands);

            /* The viewer is told as a refactor tells it. The composites first, whole: their contents go now rather than
               after the next resource sync (nothing was imported), so an instance of one spawns with what it holds and the
               viewer can judge its size. Then their instances, back in the composites that placed them, each addressed to
               its own composite. The viewer still holds those - it drops the composite and frees its placements' nodes,
               not the instances - so they go and come back: announced with the composite alone, nothing was spawned, and
               every placement stayed missing from the scene on screen until the next populate. */
            HashSet<Composite> restoredComposites = new HashSet<Composite>(_entries.Select(o => o.Composite));
            using (UnityConnection.Send.SuppressAddedCompositeContents())
                foreach (EntryRecord record in _entries)
                    Singleton.OnCompositeAdded?.Invoke(record.Composite);
            foreach (EntryRecord record in _entries)
                if (record.Composite.GetEntities().Count != 0)
                    UnityConnection.Send.SendCompositeContents(record.Composite);

            HashSet<ShortGuid> respawnedIds = new HashSet<ShortGuid>();
            foreach (IGrouping<ShortGuid, FunctionRecord> group in _removedFunctions.GroupBy(o => o.Owner))
            {
                Composite owner = commands.GetComposite(group.Key);
                //A restored composite's own instances went with its contents
                if (owner == null || restoredComposites.Contains(owner))
                    continue;
                List<Entity> restored = group.Select(o => (Entity)o.Function).Where(o => owner.GetEntityByID(o.shortGUID) == o).ToList();
                UnityConnection.Send.SendEntitiesDeleted(owner, restored);
                UnityConnection.Send.SendCompositeContents(owner, restored);
                respawnedIds.UnionWith(restored.Select(o => o.shortGUID));
            }
            //Aliases and proxies into those placements lost hold of what they point at when it was freed: sent again now it is back
            if (respawnedIds.Count != 0)
            {
                foreach (Composite composite in commands.Entries)
                {
                    if (composite == null || restoredComposites.Contains(composite))
                        continue;
                    List<Entity> here = new List<Entity>();
                    foreach (AliasEntity alias in composite.aliases_dictionary.Values)
                        if (alias.alias?.path != null && alias.alias.path.Any(respawnedIds.Contains)) here.Add(alias);
                    foreach (ProxyEntity proxy in composite.proxies_dictionary.Values)
                        if (proxy.proxy?.path != null && proxy.proxy.path.Any(respawnedIds.Contains)) here.Add(proxy);
                    if (here.Count == 0)
                        continue;
                    UnityConnection.Send.SendEntitiesDeleted(composite, here);
                    UnityConnection.Send.SendCompositeContents(composite, here);
                }
            }
            UnityConnection.ViewerZoneSync.MarkDirty();
            context.Ui?.CompositesChanged();

            List<Composite> modified = owners.Union(restoredComposites).ToList();
            if (modified.Count != 0)
                Singleton.OnCompositesModified?.Invoke(modified);
            ReloadIfShown(owners, commands.Utils);
        }

        public bool TryMerge(IEdit next) => false;

        /* Every composite the delete took something out of */
        private HashSet<ShortGuid> OwnerIds()
        {
            HashSet<ShortGuid> ids = new HashSet<ShortGuid>(_removedFunctions.Select(o => o.Owner));
            ids.UnionWith(_prunedLinks.Select(o => o.Owner));
            ids.UnionWith(_removedAliases.Select(o => o.Owner));
            ids.UnionWith(_removedProxies.Select(o => o.Owner));
            ids.UnionWith(_removedPathEntries.Select(o => o.Owner));
            return ids;
        }

        /* Take out the entries whose path ran through something removed and no longer resolves - what the purge would
           take - keeping each with its place */
        private void RemoveUnresolvable<T>(Composite owner, FunctionEntity function, List<T> entries, Func<T, EntityPath> pathOf, HashSet<ShortGuid> removedIds, CommandsUtils utils)
        {
            List<T> kept = null;
            for (int i = 0; i < entries.Count; i++)
            {
                ShortGuid[] path = pathOf(entries[i])?.path;
                if (path == null || !path.Any(removedIds.Contains) || utils.CouldResolve(utils.ResolveEntityPath(path, owner)))
                {
                    kept?.Add(entries[i]);
                    continue;
                }
                if (kept == null)
                    kept = entries.GetRange(0, i);
                _removedPathEntries.Add(new PathEntryRecord() { Owner = owner.shortGUID, Function = function.shortGUID, Index = i, Entry = entries[i] });
            }
            if (kept == null)
                return;
            entries.Clear();
            entries.AddRange(kept);
        }

        private static CompositeDisplay ShownDisplay()
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            return display == null || display.IsDisposed || !display.Populated ? null : display;
        }

        /* The composite on screen, if the step changed it, shown again as it now is: its pages were drawn from what it
           held before, and the next save of them would compile that back over its links. Purged first, as opening it
           is, so its pages are judged against what the purge leaves. */
        private static void ReloadIfShown(List<Composite> owners, CommandsUtils utils)
        {
            CompositeDisplay display = ShownDisplay();
            if (display == null || !owners.Contains(display.Composite))
                return;
            if (utils != null && !utils.PurgedComposites.purged.Contains(display.Composite.shortGUID))
            {
                utils.PurgeDeadLinks(display.Composite);
                utils.PurgedComposites.purged.Add(display.Composite.shortGUID);
            }
            display.ClearEntitySelection();
            display.Reload(true);
        }
    }

    /// <summary>Composites renamed or moved between folders: one edit however many paths changed.</summary>
    public sealed class CompositeRenameEdit : IEdit
    {
        public struct Rename
        {
            public ShortGuid Composite;
            public string Before;
            public string After;
        }

        private readonly List<Rename> _renames;

        public string Label { get; }
        public ShortGuid CompositeId => ShortGuid.Invalid;
        public ShortGuid EntityId => ShortGuid.Invalid;

        public CompositeRenameEdit(List<Rename> renames, string label)
        {
            _renames = renames;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, true);
        public void Revert(UndoContext context) => Set(context, false);

        private void Set(UndoContext context, bool forward)
        {
            List<Composite> changed = new List<Composite>();
            foreach (Rename rename in _renames)
            {
                Composite composite = context.Composite(rename.Composite);
                if (composite == null)
                    continue;
                composite.name = forward ? rename.After : rename.Before;
                changed.Add(composite);
            }
            context.Ui?.CompositesRenamed(changed);
        }

        public bool TryMerge(IEdit next) => false;
    }
}
