using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.LevelDiff
{
    /// <summary>
    /// What changed between two <see cref="ScriptSnapshot"/>s, entity by entity, for Compare Levels reports and recipe steps.
    /// Entities and composites are matched by id, so a rename is a change, not a remove + add.
    /// </summary>
    public sealed class ScriptDelta
    {
        public readonly List<CompositeSnap> AddedComposites = new List<CompositeSnap>();
        public readonly List<CompositeSnap> RemovedComposites = new List<CompositeSnap>();
        public readonly List<CompositeDelta> ChangedComposites = new List<CompositeDelta>();
        public ScriptSnapshot Before, After;

        public bool IsEmpty => AddedComposites.Count == 0 && RemovedComposites.Count == 0 && ChangedComposites.Count == 0;

        /// <summary>
        /// Says whether a parameter value is its type's default, so adding or removing it changes nothing in game. Null: every
        /// parameter counts. Arguments: composite id, entity id, parameter name, value text, and whether the entity is the after side's.
        /// </summary>
        public delegate bool DefaultCheck(ShortGuid composite, ShortGuid entity, ShortGuid parameter, string value, bool after);

        public static ScriptDelta Compare(ScriptSnapshot before, ScriptSnapshot after, DefaultCheck isDefault = null)
        {
            ScriptDelta delta = new ScriptDelta() { Before = before, After = after };
            foreach (ShortGuid id in after.Order)
            {
                CompositeSnap now = after.Composites[id];
                CompositeSnap was = before.Find(id);
                if (was == null) { delta.AddedComposites.Add(now); continue; }
                CompositeDelta changed = CompositeDelta.Compare(was, now, isDefault);
                if (changed != null) delta.ChangedComposites.Add(changed);
            }
            foreach (ShortGuid id in before.Order)
                if (!after.Composites.ContainsKey(id))
                    delta.RemovedComposites.Add(before.Composites[id]);
            return delta;
        }
    }

    public sealed class CompositeDelta
    {
        public CompositeSnap Before, After;
        public bool Renamed, PinsChanged, ColoursChanged;
        public readonly List<EntitySnap> Added = new List<EntitySnap>();
        public readonly List<EntitySnap> Removed = new List<EntitySnap>();
        public readonly List<EntityDelta> Changed = new List<EntityDelta>();

        public int Count => (Renamed ? 1 : 0) + (PinsChanged ? 1 : 0) + (ColoursChanged ? 1 : 0) + Added.Count + Removed.Count + Changed.Count;

        public static CompositeDelta Compare(CompositeSnap was, CompositeSnap now, ScriptDelta.DefaultCheck isDefault)
        {
            CompositeDelta d = new CompositeDelta() { Before = was, After = now };
            d.Renamed = was.Name != now.Name;
            d.PinsChanged = was.Pins != now.Pins;
            d.ColoursChanged = was.Colours != now.Colours;
            foreach (EntitySnap e in now.Entities.Values)
            {
                if (!was.Entities.TryGetValue(e.Id, out EntitySnap old)) { d.Added.Add(e); continue; }
                EntityDelta ed = EntityDelta.Compare(now.Id, old, e, isDefault);
                if (ed != null) d.Changed.Add(ed);
            }
            foreach (EntitySnap e in was.Entities.Values)
                if (!now.Entities.ContainsKey(e.Id))
                    d.Removed.Add(e);
            return d.Count == 0 ? null : d;
        }
    }

    public sealed class EntityDelta
    {
        public static readonly ShortGuid NameParameter = ShortGuidUtils.Generate("name");

        public EntitySnap Before, After;
        public bool KindChanged, Renamed, LinksReordered, ResourcesChanged, SpecialChanged, BrushChanged;
        public readonly List<ShortGuid> ParamsAdded = new List<ShortGuid>();
        public readonly List<ShortGuid> ParamsRemoved = new List<ShortGuid>();
        public readonly List<ShortGuid> ParamsChanged = new List<ShortGuid>();
        /// <summary>Parameters added or removed at their default value (no change in game): counted, not listed.</summary>
        public readonly List<ShortGuid> DefaultsAdded = new List<ShortGuid>();
        public readonly List<ShortGuid> DefaultsRemoved = new List<ShortGuid>();
        public readonly List<string> LinksAdded = new List<string>();
        public readonly List<string> LinksRemoved = new List<string>();

        public bool LinksChanged => LinksAdded.Count != 0 || LinksRemoved.Count != 0 || LinksReordered;
        /// <summary>Anything but defaults filled in.</summary>
        public bool Real => KindChanged || Renamed || LinksChanged || ResourcesChanged || SpecialChanged || BrushChanged || ParamsAdded.Count != 0 || ParamsRemoved.Count != 0 || ParamsChanged.Count != 0;

        public static EntityDelta Compare(ShortGuid composite, EntitySnap was, EntitySnap now, ScriptDelta.DefaultCheck isDefault)
        {
            EntityDelta d = new EntityDelta() { Before = was, After = now };
            d.KindChanged = was.Kind != now.Kind;
            d.Renamed = was.Name != now.Name;
            foreach (KeyValuePair<ShortGuid, string> p in now.Params)
            {
                if (!was.Params.TryGetValue(p.Key, out string old))
                {
                    if (EmptyResource(p.Value) || (isDefault != null && p.Key != NameParameter && isDefault(composite, now.Id, p.Key, p.Value, true))) d.DefaultsAdded.Add(p.Key);
                    else d.ParamsAdded.Add(p.Key);
                }
                else if (old != p.Value) d.ParamsChanged.Add(p.Key);
            }
            foreach (KeyValuePair<ShortGuid, string> p in was.Params)
            {
                if (now.Params.ContainsKey(p.Key)) continue;
                if (EmptyResource(p.Value) || (isDefault != null && p.Key != NameParameter && isDefault(composite, was.Id, p.Key, p.Value, false))) d.DefaultsRemoved.Add(p.Key);
                else d.ParamsRemoved.Add(p.Key);
            }
            if (!was.Links.SequenceEqual(now.Links))
            {
                //As multisets: what came and went; the same set in another order is a reorder (links fire in order)
                Dictionary<string, int> counts = new Dictionary<string, int>();
                foreach (string l in was.Links) counts[l] = (counts.TryGetValue(l, out int n) ? n : 0) + 1;
                foreach (string l in now.Links)
                {
                    if (counts.TryGetValue(l, out int n) && n > 0) counts[l] = n - 1;
                    else d.LinksAdded.Add(l);
                }
                foreach (KeyValuePair<string, int> left in counts)
                    for (int i = 0; i < left.Value; i++) d.LinksRemoved.Add(left.Key);
                d.LinksReordered = d.LinksAdded.Count == 0 && d.LinksRemoved.Count == 0;
            }
            d.ResourcesChanged = was.Resources != now.Resources;
            d.SpecialChanged = was.Special != now.Special;
            d.BrushChanged = was.Brush != now.Brush;
            //Only where its own resources sit moved: a Save & Build writes those (world space in retail, local after a build)
            if (d.ResourcesChanged && SameIgnoringPlacement(was.Resources, now.Resources))
            {
                d.ResourcesChanged = false;
                d.ResourcePlacementOnly = true;
            }
            if (!d.Real && !d.ResourcePlacementOnly && d.DefaultsAdded.Count == 0 && d.DefaultsRemoved.Count == 0) return null;
            return d;
        }

        /// <summary>Its own resources moved and nothing else changed about it (a Save &amp; Build does this): not a change anyone made.</summary>
        public bool ResourcePlacementOnly;

        /// <summary>A resource parameter holding no references (a build adds them to entities that had none): no change in game.</summary>
        private static bool EmptyResource(string value) => value != null && value.StartsWith("{\"resource\":", StringComparison.Ordinal) && value.EndsWith(",\"refs\":[]}}", StringComparison.Ordinal);

        private static bool SameIgnoringPlacement(string a, string b)
        {
            if (a == null || b == null) return false;
            Newtonsoft.Json.Linq.JArray ja = CathodeJson.Parse(a) as Newtonsoft.Json.Linq.JArray, jb = CathodeJson.Parse(b) as Newtonsoft.Json.Linq.JArray;
            if (ja == null || jb == null || ja.Count != jb.Count) return false;
            for (int i = 0; i < ja.Count; i++)
            {
                if (ja[i] is Newtonsoft.Json.Linq.JObject oa) { oa.Remove("position"); oa.Remove("rotation"); }
                if (jb[i] is Newtonsoft.Json.Linq.JObject ob) { ob.Remove("position"); ob.Remove("rotation"); }
            }
            return CathodeJson.Text(ja) == CathodeJson.Text(jb);
        }
    }
}
