using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /* The order of a pin's links is part of what a script does: a relay fires its links in that order, and where several links set a
       value the last one wins. The flowgraph compiles a composite's links back from its pages page by page and node by node, so laying
       nodes out could reorder them with every link still there. Capture (before the compile) and Restore (after it) keep every stored
       link in its order, with its id; links the compile adds go after them. */
    public static class LinkOrder
    {
        /// <summary>Every entity's links as they were stored, in order.</summary>
        public sealed class Snapshot
        {
            internal readonly Dictionary<ShortGuid, List<EntityConnector>> Links = new Dictionary<ShortGuid, List<EntityConnector>>();
        }

        /// <summary>A pin's order changed through <see cref="Move"/>, <see cref="SetPinOrder"/> or an undo of one: the composite,
        /// and the entity whose pin it is.</summary>
        public static event Action<Composite, Entity> OrderChanged;

        internal static void RaiseOrderChanged(Composite composite, Entity owner) => OrderChanged?.Invoke(composite, owner);

        private static (ShortGuid, ShortGuid, ShortGuid) Key(EntityConnector link) => (link.thisParamID, link.linkedEntityID, link.linkedParamID);

        /// <summary>The composite's links as stored now: take it before the compile clears them.</summary>
        public static Snapshot Capture(Composite composite)
        {
            Snapshot snapshot = new Snapshot();
            if (composite == null)
                return snapshot;
            foreach (Entity entity in composite.GetEntities())
            {
                if (entity?.childLinks == null || entity.childLinks.Count == 0 || snapshot.Links.ContainsKey(entity.shortGUID))
                    continue;
                snapshot.Links.Add(entity.shortGUID, new List<EntityConnector>(entity.childLinks));
            }
            return snapshot;
        }

        /// <summary>
        /// After a compile: put each entity's links back in their stored order (and give them back their ids), the links the
        /// compile added after them. Only the order changes: the set of links is the compile's. Returns how many pins came out
        /// of the compile in another order and were put back.
        /// </summary>
        public static int Restore(Composite composite, Snapshot snapshot)
        {
            if (composite == null || snapshot == null)
                return 0;

            int pinsPutBack = 0;
            foreach (Entity entity in composite.GetEntities())
            {
                List<EntityConnector> compiled = entity?.childLinks;
                if (compiled == null || compiled.Count == 0 || !snapshot.Links.TryGetValue(entity.shortGUID, out List<EntityConnector> before))
                    continue;

                //Where each link stood: the k-th compiled copy of a link takes the place of the k-th stored one
                Dictionary<(ShortGuid, ShortGuid, ShortGuid), Queue<int>> stored = new Dictionary<(ShortGuid, ShortGuid, ShortGuid), Queue<int>>();
                for (int i = 0; i < before.Count; i++)
                {
                    if (!stored.TryGetValue(Key(before[i]), out Queue<int> places))
                        stored.Add(Key(before[i]), places = new Queue<int>());
                    places.Enqueue(i);
                }

                EntityConnector[] links = compiled.ToArray();
                long[] rank = new long[links.Length];
                for (int i = 0; i < links.Length; i++)
                {
                    if (stored.TryGetValue(Key(links[i]), out Queue<int> places) && places.Count != 0)
                    {
                        int was = places.Dequeue();
                        rank[i] = was;
                        links[i].ID = before[was].ID;
                    }
                    else
                        rank[i] = (long)before.Count + i; //new: after everything stored, in the order the pages gave it
                }

                //A pin the compile reordered: its links' places do not rise in the order the pages gave them
                Dictionary<ShortGuid, long> lastRank = new Dictionary<ShortGuid, long>();
                HashSet<ShortGuid> reordered = new HashSet<ShortGuid>();
                for (int i = 0; i < links.Length; i++)
                {
                    if (lastRank.TryGetValue(links[i].thisParamID, out long last) && rank[i] < last)
                        reordered.Add(links[i].thisParamID);
                    lastRank[links[i].thisParamID] = rank[i];
                }
                pinsPutBack += reordered.Count;

                int[] order = Enumerable.Range(0, links.Length).OrderBy(i => rank[i]).ToArray(); //stable
                compiled.Clear();
                foreach (int i in order)
                    compiled.Add(links[i]);
            }

            _lastPutBack[composite.shortGUID] = pinsPutBack;
            if (pinsPutBack != 0)
                Debug.Log("Link Order", (composite.name ?? "?") + ": the pages compiled " + pinsPutBack + " pin(s) with their links in another order - kept as stored");
            return pinsPutBack;
        }

        private static readonly Dictionary<ShortGuid, int> _lastPutBack = new Dictionary<ShortGuid, int>();

        /// <summary>How many pins the composite's last compile drew with their links in another order than they were stored in
        /// (each kept in its stored order). -1 when it has not been compiled.</summary>
        public static int LastPutBack(Composite composite)
        {
            return composite != null && _lastPutBack.TryGetValue(composite.shortGUID, out int count) ? count : -1;
        }

        /// <summary>The links out of one of an entity's pins, in the order they are stored: the order a relay fires them in.</summary>
        public static List<EntityConnector> PinLinks(Entity owner, ShortGuid pin)
        {
            return owner?.childLinks == null ? new List<EntityConnector>() : owner.childLinks.Where(o => o.thisParamID == pin).ToList();
        }

        /// <summary>The 0-based place of a link among its pin's stored links, or -1 when it is not stored (drawn on a page
        /// since the composite was last compiled). The k-th of identical links is the k-th place.</summary>
        public static int PlaceOf(Entity owner, EntityConnector link, int occurrence = 0)
        {
            List<EntityConnector> links = PinLinks(owner, link.thisParamID);
            for (int i = 0; i < links.Count; i++)
            {
                if (Key(links[i]) != Key(link))
                    continue;
                if (occurrence-- == 0)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Put a pin's stored links in the given order (links named by pin, target and target pin; the k-th of identical ones
        /// takes the k-th). The places the pin's links fill in the entity's list are kept; stored links the order does not
        /// name stay after the named ones, as they were. Returns true if anything moved.
        /// </summary>
        public static bool SetPinOrder(Entity owner, ShortGuid pin, IList<EntityConnector> order)
        {
            if (owner?.childLinks == null || order == null)
                return false;

            List<int> slots = new List<int>();
            for (int i = 0; i < owner.childLinks.Count; i++)
                if (owner.childLinks[i].thisParamID == pin)
                    slots.Add(i);

            List<EntityConnector> current = slots.Select(i => owner.childLinks[i]).ToList();
            bool[] taken = new bool[current.Count];
            List<EntityConnector> sequence = new List<EntityConnector>(current.Count);
            foreach (EntityConnector wanted in order)
            {
                for (int i = 0; i < current.Count; i++)
                {
                    if (taken[i] || Key(current[i]) != Key(wanted))
                        continue;
                    taken[i] = true;
                    sequence.Add(current[i]);
                    break;
                }
            }
            for (int i = 0; i < current.Count; i++)
                if (!taken[i])
                    sequence.Add(current[i]);

            bool changed = false;
            for (int k = 0; k < slots.Count; k++)
            {
                if (!changed && (Key(owner.childLinks[slots[k]]) != Key(sequence[k]) || owner.childLinks[slots[k]].ID != sequence[k].ID))
                    changed = true;
                owner.childLinks[slots[k]] = sequence[k];
            }
            return changed;
        }

        /// <summary>
        /// Move a stored link up (delta -1) or down (+1) among its pin's links, as one undo step, and mark the level changed.
        /// False when it is not stored or is already at that end.
        /// </summary>
        public static bool Move(Composite composite, Entity owner, EntityConnector link, int delta, int occurrence = 0)
        {
            int at = PlaceOf(owner, link, occurrence);
            if (composite == null || at < 0)
                return false;
            List<EntityConnector> before = PinLinks(owner, link.thisParamID);
            int to = Math.Max(0, Math.Min(before.Count - 1, at + delta));
            if (to == at)
                return false;

            List<EntityConnector> after = new List<EntityConnector>(before);
            EntityConnector moving = after[at];
            after.RemoveAt(at);
            after.Insert(to, moving);
            return Apply(composite, owner, link.thisParamID, after, "Move a link " + (delta < 0 ? "up" : "down") + " on " + UndoLabels.Entity(composite, owner));
        }

        /// <summary>Put a pin's stored links in a new order (see <see cref="SetPinOrder"/>) as one undo step, and mark the level
        /// changed. False when nothing moved.</summary>
        public static bool Apply(Composite composite, Entity owner, ShortGuid pin, IList<EntityConnector> order, string label)
        {
            List<EntityConnector> before = PinLinks(owner, pin);
            if (!SetPinOrder(owner, pin, order))
                return false;
            List<EntityConnector> after = PinLinks(owner, pin);
            UndoStack.Current.Record(new LinkOrderEdit(composite, owner, pin, before, after, label));
            DirtyTracker.MarkLevelDataModified();
            RaiseOrderChanged(composite, owner);
            return true;
        }
    }

    /// <summary>One pin's links put in another order (<see cref="LinkOrder.Apply"/>): undo puts the old order back.</summary>
    internal sealed class LinkOrderEdit : IEdit
    {
        private readonly ShortGuid _pin;
        private readonly List<EntityConnector> _before;
        private readonly List<EntityConnector> _after;

        public string Label { get; }
        public ShortGuid CompositeId { get; }
        public ShortGuid EntityId { get; }

        public LinkOrderEdit(Composite composite, Entity owner, ShortGuid pin, List<EntityConnector> before, List<EntityConnector> after, string label)
        {
            CompositeId = composite.shortGUID;
            EntityId = owner.shortGUID;
            _pin = pin;
            _before = new List<EntityConnector>(before);
            _after = new List<EntityConnector>(after);
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _after);
        public void Revert(UndoContext context) => Set(context, _before);

        private void Set(UndoContext context, List<EntityConnector> order)
        {
            Composite composite = context.Composite(CompositeId);
            Entity owner = composite?.GetEntityByID(EntityId);
            if (owner == null)
                return;
            LinkOrder.SetPinOrder(owner, _pin, order);
            LinkOrder.RaiseOrderChanged(composite, owner);
        }

        public bool TryMerge(IEdit next) => false;
    }
}
