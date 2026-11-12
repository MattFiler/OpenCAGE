using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using static CATHODE.CollisionMaps;

namespace OpenCAGE.Brushwork
{
    /// <summary>
    /// A brush's collision (the only place brushes touch collision), made from its own triangles as import_model's is. 'solid' is
    /// retail's pair through <see cref="CollisionPairs"/>: the entity's WORLD row on a STANDARD proxy plus a '&lt;name&gt;_Ballistic'
    /// sibling's BALLISTIC row on a BALLISTICS twin, so it is walked on, shot at, and gives footsteps and zones. New proxies are made
    /// only when the triangles change; replaced ones are noted on the brush as superseded. Proxies themselves cannot be undone.
    /// </summary>
    public static class BrushCollision
    {
        public static readonly string[] Modes = { "solid", "world", "ballistic", "none" };

        public static bool IsMode(string mode) => Modes.Contains((mode ?? "").Trim().ToLowerInvariant());

        public static CollisionFlags FlagsFor(string mode)
        {
            switch ((mode ?? "solid").Trim().ToLowerInvariant())
            {
                case "world": return CollisionFlags.WORLD;
                case "ballistic": return CollisionFlags.BALLISTIC;
                default: return CollisionFlags.WORLD | CollisionFlags.BALLISTIC;
            }
        }

        /// <summary>Proxies made this session, by what they were made from, so an undo-and-redo or a flip back to an earlier size reuses one.</summary>
        private static readonly Dictionary<string, (WeakReference level, HavokPackfile.StaticCompoundShape proxy)> _made = new Dictionary<string, (WeakReference, HavokPackfile.StaticCompoundShape)>();

        private static bool _hooked;

        /// <summary>Follow collision compaction (CathodeLib's CollisionCompactor), which renumbers proxies: once, at start.</summary>
        public static void Initialise()
        {
            if (_hooked) return;
            _hooked = true;
            CathodeLib.Havok.CollisionCompactor.Compacted += OnCompacted;
        }

        /* A level's proxies were pruned and renumbered: every brush row's proxy ordinals follow (a pruned one is dropped), and
           the session's made-proxy cache for that level is let go (its compounds may be gone). Synchronous: a save that compacts
           writes the BRUSHES table after this. */
        private static void OnCompacted(Level level, CathodeLib.Havok.CollisionCompactor.LevelCompaction report)
        {
            int[] remap = report?.Remap;
            if (remap == null || remap.Length == 0 || level?.Commands == null)
                return;
            int Map(int ordinal) => ordinal >= 0 && ordinal < remap.Length ? remap[ordinal] : ordinal;
            foreach (BrushTable.Brush row in level.Commands.Utils.GetBrushes())
            {
                BrushDefinition brush = BrushDefinition.FromJson(row.definition);
                if (brush == null) continue;
                brush.WorldProxy = Map(brush.WorldProxy);
                brush.BallisticProxy = Map(brush.BallisticProxy);
                brush.SupersededProxies = brush.SupersededProxies.Select(Map).Where(o => o >= 0).Distinct().ToList();
                level.Commands.Utils.SetBrush(row.composite, row.entity, brush.ToJson());
            }
            lock (_made)
                foreach (string key in _made.Where(o => ReferenceEquals(o.Value.level.Target, level) || o.Value.level.Target == null).Select(o => o.Key).ToList())
                    _made.Remove(key);
        }

        /// <summary>
        /// Bring <paramref name="entity"/>'s collision to what <paramref name="brush"/> asks for (its Collision mode, built from
        /// <paramref name="geometry"/>, with <paramref name="material"/> as the physics material). Updates the brush's proxy
        /// record. UI thread, inside the caller's undo group.
        /// </summary>
        public static void Apply(Level level, Composite composite, FunctionEntity entity, BrushDefinition brush, BrushGeometry geometry, Materials.Material material, string label, List<string> notes)
        {
            string mode = (brush.Collision ?? "solid").Trim().ToLowerInvariant();
            Outcome outcome = ApplyTriangles(level, composite, entity, mode, () => geometry.ToCollisionMesh(brush.Model ?? "brush"), mode == "none" ? null : geometry.TriangleHash(), brush.CollisionHash, material, label, notes);
            if (outcome == null)
                return;
            if (brush.WorldProxy != outcome.WorldProxy) Supersede(brush, brush.WorldProxy);
            if (brush.BallisticProxy != outcome.BallisticProxy) Supersede(brush, brush.BallisticProxy);
            brush.WorldProxy = outcome.WorldProxy;
            brush.BallisticProxy = outcome.BallisticProxy;
            brush.CollisionHash = outcome.Hash;
            brush.SupersededProxies.Remove(brush.WorldProxy);
            brush.SupersededProxies.Remove(brush.BallisticProxy);
        }

        /// <summary>What <see cref="ApplyTriangles"/> left the entity with.</summary>
        public sealed class Outcome
        {
            /// <summary>The proxies its WORLD (or only) row and its ballistic half's row use now (-1 none).</summary>
            public int WorldProxy = -1, BallisticProxy = -1;
            /// <summary>The proxies its rows used before, which nothing of it names any more (-1 none).</summary>
            public int WorldBefore = -1, BallisticBefore = -1;
            /// <summary>The triangles its collision was made from (null for none).</summary>
            public string Hash;
            /// <summary>A new proxy was made.</summary>
            public bool Made;
        }

        /// <summary>
        /// Give <paramref name="entity"/> collision of <paramref name="mode"/> (solid / world / ballistic / none) made from triangles in
        /// its own space - <paramref name="mesh"/>, asked for only when a proxy has to be made - with <paramref name="material"/> as
        /// the physics material. <paramref name="keepIfHash"/>: the hash the entity's current proxy was made from, so unchanged
        /// triangles keep it. Used by brushes and "collision to match". UI thread, inside the caller's undo group.
        /// Null when the level has no collision loaded.
        /// </summary>
        public static Outcome ApplyTriangles(Level level, Composite composite, FunctionEntity entity, string mode, Func<CollisionProxyImporter.MeshSource> mesh, string hash, string keepIfHash, Materials.Material material, string label, List<string> notes)
        {
            CollisionPairs.IWriter writer = CollisionPairs.Undoable(level, label);
            COLLISION_MAPPING current = CollisionPairs.RowOf(entity);
            mode = (mode ?? "solid").Trim().ToLowerInvariant();
            Outcome outcome = new Outcome() { WorldBefore = current?.CollisionProxy?.ProxyIndex ?? -1 };
            FunctionEntity halfBefore = CollisionPairs.FindSibling(level.Commands, composite, entity);
            outcome.BallisticBefore = CollisionPairs.RowOf(halfBefore)?.CollisionProxy?.ProxyIndex ?? -1;

            if (mode == "none")
            {
                if (current != null)
                {
                    CollisionPairs.RemoveSibling(level, composite, entity, writer);
                    RemoveRow(composite, entity, label);
                }
                return outcome;
            }

            if (level.Collision == null || !level.Collision.Loaded)
            {
                notes?.Add("The level has no collision file loaded, so there is no collision.");
                return null;
            }

            CollisionFlags flags = FlagsFor(mode);
            bool pair = CollisionPairs.IsPair(flags);
            CollisionFlags rowFlags = pair ? CollisionPairs.WorldHalf(flags) : flags;
            uint filter = CollisionPairs.FilterFor(rowFlags);
            uint userData = CollisionPairs.UserData(level, material);
            CollisionProxyImporter.MeshSource made = null;
            if (hash == null)
            {
                made = mesh();
                hash = BrushGeometry.HashOf(made);
            }
            outcome.Hash = hash;

            //The proxy the row needs: the one it has when nothing it is made from changed, else one made from the triangles
            HavokPackfile.StaticCompoundShape proxy = current?.CollisionProxy;
            bool keep = proxy != null && hash == keepIfHash && CollisionPairs.UniformFilter(proxy) == filter && CollisionPairs.UserDataOf(proxy) == userData
                && ReferenceEquals(level.Collision.GetCompound(proxy.ProxyIndex), proxy);
            if (!keep)
            {
                made = made ?? mesh();
                if (made == null || made.TriangleCount == 0)
                {
                    notes?.Add("There are no triangles to make collision from.");
                    return null;
                }
                proxy = MakeProxy(level, made, hash, userData, filter);
                outcome.Made = true;
            }

            if (current == null || current.Flags != rowFlags || !ReferenceEquals(current.CollisionProxy, proxy) || !ReferenceEquals(current.Material, material))
            {
                //A new row rather than one other entities (a pasted copy) may share
                COLLISION_MAPPING row = new COLLISION_MAPPING()
                {
                    Flags = rowFlags,
                    CollisionInstance = null,
                    ResourceGUID = current?.ResourceGUID ?? ShortGuid.Invalid,
                    Entity = new EntityHandle(),
                    Material = material,
                    CollisionProxy = proxy,
                    MaterialMapping = current?.MaterialMapping,
                    ZoneID = current?.ZoneID ?? ShortGuid.Invalid,
                };
                writer.SetCollision(composite, entity, row);
                current = row;
            }

            int ballistic = -1;
            if (pair)
            {
                CollisionPairs.Outcome half = CollisionPairs.SyncSibling(level, composite, entity, current, writer);
                ballistic = half?.Twin?.ProxyIndex ?? -1;
            }
            else
                CollisionPairs.RemoveSibling(level, composite, entity, writer);

            outcome.WorldProxy = proxy.ProxyIndex;
            outcome.BallisticProxy = ballistic;
            return outcome;
        }

        private static void Supersede(BrushDefinition brush, int proxy)
        {
            if (proxy >= 0 && !brush.SupersededProxies.Contains(proxy))
                brush.SupersededProxies.Add(proxy);
        }

        private static HavokPackfile.StaticCompoundShape MakeProxy(Level level, CollisionProxyImporter.MeshSource mesh, string hash, uint userData, uint filter)
        {
            string key = hash + "|" + userData + "|" + filter;
            lock (_made)
            {
                if (_made.TryGetValue(key, out (WeakReference level, HavokPackfile.StaticCompoundShape proxy) known) && ReferenceEquals(known.level.Target, level)
                    && ReferenceEquals(level.Collision.GetCompound(known.proxy.ProxyIndex), known.proxy))
                    return known.proxy;
            }
            HavokPackfile.StaticCompoundShape created = CollisionProxyImporter.Import(level, mesh, userData, filter);
            EditHistory.AssetChanged(level, AssetModificationInfoTable.AssetKind.COLLISION, created.ProxyIndex.ToString());
            Singleton.OnResourceModified?.Invoke();
            lock (_made)
                _made[key] = (new WeakReference(level), created);
            return created;
        }

        /// <summary>Take the collision reference off the entity's resource (one undo step of the caller's group).</summary>
        private static void RemoveRow(Composite composite, FunctionEntity entity, string label)
        {
            if (entity.resources != null && entity.resources.Any(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING))
            {
                List<ResourceReference> before = ResourceSessionEdit.CloneReferences(entity.resources);
                List<ResourceReference> after = ResourceSessionEdit.CloneReferences(entity.resources);
                after.RemoveAll(o => o.resource_type == ResourceType.COLLISION_MAPPING);
                UndoStack.Current.Apply(new ResourceSessionEdit(composite, entity, before, after, label));
            }
            Parameter existing = entity.GetParameter(ShortGuids.resource);
            if (existing?.content is cResource resource && resource.value.Any(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING))
            {
                cResource after = (cResource)ParameterValues.Clone(resource);
                after.value.RemoveAll(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING);
                UndoStack.Current.Apply(new ResourceSessionEdit(composite, entity, existing, true, entity.parameters.IndexOf(existing), ParameterValues.Clone(resource), existing.variant, after, label));
            }
        }
    }
}
