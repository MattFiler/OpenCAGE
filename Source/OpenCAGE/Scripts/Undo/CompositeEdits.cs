using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

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
    /// in every other composite, the links into those, and the aliases and proxies that could no longer
    /// resolve. The objects are kept, so undo puts back exactly what went.
    /// </summary>
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
            public ShortGuid Function;
            public int Index;
            public EntityConnector Link;
        }
        private struct EntityRecord
        {
            public ShortGuid Owner;
            public Entity Entity;
        }

        private readonly List<Composite> _composites;
        private List<EntryRecord> _entries;
        private List<FunctionRecord> _removedFunctions;
        private List<LinkRecord> _prunedLinks;
        private List<EntityRecord> _removedAliases;
        private List<EntityRecord> _removedProxies;

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
            HashSet<ShortGuid> deletedIds = new HashSet<ShortGuid>(_composites.Select(o => o.shortGUID));

            context.Ui?.BeforeCompositesRemoved(deletedIds);

            _removedFunctions = new List<FunctionRecord>();
            _prunedLinks = new List<LinkRecord>();
            _removedAliases = new List<EntityRecord>();
            _removedProxies = new List<EntityRecord>();

            //Remove any entities or links that reference the deleted composites
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

                    List<EntityConnector> kept = new List<EntityConnector>(function.childLinks.Count);
                    for (int i = 0; i < function.childLinks.Count; i++)
                    {
                        EntityConnector link = function.childLinks[i];
                        if (entry.GetEntityByID(link.linkedEntityID) is FunctionEntity linked && deletedIds.Contains(linked.function))
                        {
                            _prunedLinks.Add(new LinkRecord() { Owner = entry.shortGUID, Function = function.shortGUID, Index = i, Link = link });
                            continue;
                        }
                        kept.Add(link);
                    }
                    if (kept.Count != function.childLinks.Count)
                        function.childLinks = kept;
                    keep.Add(function);
                }

                if (keep.Count != entry.functions_dictionary.Count)
                {
                    entry.functions_dictionary.Clear();
                    foreach (FunctionEntity function in keep)
                        entry.functions_dictionary[function.shortGUID] = function;
                }
            }

            //Remove aliases and proxies that can no longer resolve
            CommandsUtils utils = commands.Utils;
            foreach (Composite entry in commands.Entries)
            {
                List<AliasEntity> aliases = entry.aliases.Where(o => !utils.CouldResolve(utils.ResolveAlias(o, entry))).ToList();
                List<ProxyEntity> proxies = entry.proxies.Where(o => !utils.CouldResolve(utils.ResolveProxy(o))).ToList();
                foreach (AliasEntity alias in aliases)
                {
                    _removedAliases.Add(new EntityRecord() { Owner = entry.shortGUID, Entity = alias });
                    entry.aliases_dictionary.Remove(alias.shortGUID);
                }
                foreach (ProxyEntity proxy in proxies)
                {
                    _removedProxies.Add(new EntityRecord() { Owner = entry.shortGUID, Entity = proxy });
                    entry.proxies_dictionary.Remove(proxy.shortGUID);
                }
            }

            //Remove the composites
            _entries = new List<EntryRecord>();
            foreach (Composite composite in _composites)
            {
                _entries.Add(new EntryRecord() { Composite = composite, Index = commands.Entries.IndexOf(composite) });
                commands.Entries.Remove(composite);
            }
            utils.PurgedComposites.purged.Clear(); //TODO: we should smartly remove from this list, rather than removing all

            context.Ui?.CompositesChanged();
            context.Content?.EditorUtils?.GenerateCompositeInstances(commands);

            foreach (Composite composite in _composites)
                Singleton.OnCompositeDeleted?.Invoke(composite);
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
                Entity function = commands.GetComposite(record.Owner)?.GetEntityByID(record.Function);
                if (function != null && !function.childLinks.Any(o => o.ID == record.Link.ID))
                    function.childLinks.Insert(Math.Min(record.Index, function.childLinks.Count), record.Link);
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

            context.Content?.EditorUtils?.GenerateCompositeInstances(commands);
            foreach (EntryRecord record in _entries)
                Singleton.OnCompositeAdded?.Invoke(record.Composite);
            context.Ui?.CompositesChanged();
        }

        public bool TryMerge(IEdit next) => false;
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
