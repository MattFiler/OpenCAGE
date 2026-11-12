using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.LevelDiff
{
    /// <summary>A <see cref="ScriptDelta"/> as the "Script" category of a report: composite > entity > what changed (before -> now).</summary>
    public static class ScriptReport
    {
        public const int MaxEntitiesListed = 400;

        public static DiffCategory Build(ScriptDelta delta)
        {
            DiffCategory category = new DiffCategory("script", "Script");
            Func<ShortGuid, string> compositeName = id => delta.After.Find(id)?.Name ?? delta.Before.Find(id)?.Name ?? CathodeJson.Id(id);
            int entityChanges = 0, defaults = 0, defaultEntities = 0, placementOnly = 0, rounding = 0;

            foreach (CompositeSnap added in delta.AddedComposites)
            {
                DiffNode node = category.Item(DiffKind.Added, "composite " + added.Name + " (" + added.Entities.Count + " entities)", "composite:" + CathodeJson.Id(added.Id));
                foreach (EntitySnap e in added.Entities.Values.Take(MaxEntitiesListed))
                    node.Add(DiffKind.Added, Label(e, compositeName), EntityRef(added.Id, e.Id));
                if (added.Entities.Count > MaxEntitiesListed) node.Add(DiffKind.Info, "... and " + (added.Entities.Count - MaxEntitiesListed) + " more entities");
            }
            foreach (CompositeSnap removed in delta.RemovedComposites)
                category.Item(DiffKind.Removed, "composite " + removed.Name + " (" + removed.Entities.Count + " entities)", "composite:" + CathodeJson.Id(removed.Id));

            foreach (CompositeDelta c in delta.ChangedComposites.OrderBy(o => o.After.Name, StringComparer.OrdinalIgnoreCase))
            {
                List<EntityDelta> real = c.Changed.Where(o => o.Real && !RoundingOnly(o)).ToList();
                rounding += c.Changed.Count(o => o.Real && RoundingOnly(o));
                int filled = c.Changed.Sum(o => o.DefaultsAdded.Count + o.DefaultsRemoved.Count);
                defaults += filled;
                defaultEntities += c.Changed.Count(o => o.DefaultsAdded.Count + o.DefaultsRemoved.Count != 0);
                placementOnly += c.Changed.Count(o => o.ResourcePlacementOnly && !o.Real);
                if (!c.Renamed && !c.PinsChanged && !c.ColoursChanged && c.Added.Count == 0 && c.Removed.Count == 0 && real.Count == 0)
                    continue;

                List<string> parts = new List<string>();
                if (c.Renamed) parts.Add("renamed from " + c.Before.Name);
                if (c.Added.Count != 0) parts.Add(c.Added.Count + " entities added");
                if (c.Removed.Count != 0) parts.Add(c.Removed.Count + " removed");
                if (real.Count != 0) parts.Add(real.Count + " changed");
                if (c.PinsChanged) parts.Add("pin types changed");
                if (c.ColoursChanged) parts.Add("colour pins changed");
                DiffNode node = category.Item(DiffKind.Changed, c.After.Name + ": " + string.Join(", ", parts), "composite:" + CathodeJson.Id(c.After.Id));
                entityChanges += c.Added.Count + c.Removed.Count + real.Count;

                if (c.Renamed) node.Add(DiffKind.Changed, "name: " + c.Before.Name + " -> " + c.After.Name);
                foreach (EntitySnap e in c.Added.Take(MaxEntitiesListed))
                {
                    DiffNode added = node.Add(DiffKind.Added, Label(e, compositeName), EntityRef(c.After.Id, e.Id));
                    foreach (KeyValuePair<ShortGuid, string> p in e.Params.Where(o => o.Key != EntityDelta.NameParameter).OrderBy(o => CathodeJson.Name(o.Key), StringComparer.OrdinalIgnoreCase))
                        added.Add(DiffKind.Info, CathodeJson.Name(p.Key) + ": " + CathodeJson.Show(CathodeJson.Parse(p.Value)));
                    foreach (string link in e.Links)
                        added.Add(DiffKind.Info, "link " + LinkLabel(link, c.After));
                }
                foreach (EntitySnap e in c.Removed.Take(MaxEntitiesListed))
                    node.Add(DiffKind.Removed, Label(e, compositeName), EntityRef(c.After.Id, e.Id));
                foreach (EntityDelta d in real.Take(MaxEntitiesListed))
                    Entity(node, c, d, compositeName);
                int listed = Math.Min(c.Added.Count, MaxEntitiesListed) + Math.Min(c.Removed.Count, MaxEntitiesListed) + Math.Min(real.Count, MaxEntitiesListed);
                if (listed < c.Added.Count + c.Removed.Count + real.Count) node.Add(DiffKind.Info, "... and " + (c.Added.Count + c.Removed.Count + real.Count - listed) + " more entities");
                if (c.PinsChanged) node.Add(DiffKind.Changed, "pin types: " + Short(c.Before.Pins) + " -> " + Short(c.After.Pins));
                if (c.ColoursChanged) node.Add(DiffKind.Changed, "colour pins: " + Short(c.Before.Colours) + " -> " + Short(c.After.Colours));
            }

            List<string> summary = new List<string>();
            if (delta.AddedComposites.Count != 0) summary.Add(delta.AddedComposites.Count + " composites added");
            if (delta.RemovedComposites.Count != 0) summary.Add(delta.RemovedComposites.Count + " removed");
            int changedComposites = category.Changed;
            if (changedComposites != 0) summary.Add(changedComposites + " changed (" + entityChanges + " entity changes)");
            category.Summary = summary.Count == 0 ? "no differences" : string.Join(", ", summary);
            if (defaults != 0)
                category.Notes.Add(defaults + " parameters on " + defaultEntities + " entities were only filled in (or left out) at their default value, or empty - the editor fills defaults in when an entity is looked at, a build adds empty resource parameters; no change in game, not listed.");
            if (rounding != 0)
                category.Notes.Add(rounding + " entities differ only by float rounding in their values (under 0.0001, as a load and save writes them back): no change in game, not listed.");
            if (placementOnly != 0)
                category.Notes.Add(placementOnly + " entities' own resources (collision barriers, light renderables ...) only sit elsewhere: a Save & Build writes where those go (retail has them in world space, a build in the entity's own); not listed.");
            return category;
        }

        /// <summary>Its only changes are values that differ by float rounding (a value written back after a load and save).</summary>
        public static bool RoundingOnly(EntityDelta d)
        {
            if (d.KindChanged || d.Renamed || d.LinksChanged || d.ResourcesChanged || d.SpecialChanged || d.BrushChanged || d.ParamsAdded.Count != 0 || d.ParamsRemoved.Count != 0 || d.ParamsChanged.Count == 0)
                return false;
            foreach (ShortGuid p in d.ParamsChanged)
                if (!LevelComparer.OnlyRounding(new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("v", d.Before.Params[p]) }, new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("v", d.After.Params[p]) }))
                    return false;
            return true;
        }

        public static string EntityRef(ShortGuid composite, ShortGuid entity) => "entity:" + CathodeJson.Id(composite) + "/" + CathodeJson.Id(entity);

        public static string Label(EntitySnap e, Func<ShortGuid, string> compositeName)
        {
            string what = e.Describe(compositeName);
            string name = string.IsNullOrEmpty(e.Name) ? CathodeJson.Id(e.Id) : e.Name;
            return name + " (" + what + ") [" + CathodeJson.Id(e.Id) + "]";
        }

        private static void Entity(DiffNode parent, CompositeDelta c, EntityDelta d, Func<ShortGuid, string> compositeName)
        {
            DiffNode node = parent.Add(DiffKind.Changed, Label(d.After, compositeName), EntityRef(c.After.Id, d.After.Id));
            if (d.KindChanged) node.Add(DiffKind.Changed, "type: " + d.Before.Describe(compositeName) + " -> " + d.After.Describe(compositeName));
            if (d.Renamed) node.Add(DiffKind.Changed, "name: " + (d.Before.Name ?? "(none)") + " -> " + (d.After.Name ?? "(none)"));
            foreach (ShortGuid p in d.ParamsChanged.Where(o => o != EntityDelta.NameParameter || !d.Renamed).OrderBy(o => CathodeJson.Name(o), StringComparer.OrdinalIgnoreCase))
            {
                string a = CathodeJson.Show(CathodeJson.Parse(d.Before.Params[p])), b = CathodeJson.Show(CathodeJson.Parse(d.After.Params[p]));
                if (a == b) { node.Add(DiffKind.Changed, CathodeJson.Name(p) + ": " + string.Join("; ", CathodeJson.Differences(CathodeJson.Parse(d.Before.Params[p]), CathodeJson.Parse(d.After.Params[p])))); continue; }
                node.Add(DiffKind.Changed, CathodeJson.Name(p) + ": " + a + " -> " + b);
            }
            foreach (ShortGuid p in d.ParamsAdded.OrderBy(o => CathodeJson.Name(o), StringComparer.OrdinalIgnoreCase))
                node.Add(DiffKind.Added, CathodeJson.Name(p) + ": " + CathodeJson.Show(CathodeJson.Parse(d.After.Params[p])));
            foreach (ShortGuid p in d.ParamsRemoved.OrderBy(o => CathodeJson.Name(o), StringComparer.OrdinalIgnoreCase))
                node.Add(DiffKind.Removed, CathodeJson.Name(p) + " (was " + CathodeJson.Show(CathodeJson.Parse(d.Before.Params[p])) + ")");
            foreach (string l in d.LinksAdded) node.Add(DiffKind.Added, "link " + LinkLabel(l, c.After));
            foreach (string l in d.LinksRemoved) node.Add(DiffKind.Removed, "link " + LinkLabel(l, c.Before));
            if (d.LinksReordered) node.Add(DiffKind.Changed, "link order (they fire in order): " + string.Join(", ", d.After.Links.Select(o => LinkLabel(o, c.After))));
            if (d.ResourcesChanged)
            {
                string a = ShowRefs(d.Before.Resources), b = ShowRefs(d.After.Resources);
                //Read the same: say what differs underneath (a collision row, an id)
                if (a == b) node.Add(DiffKind.Changed, "resources: " + string.Join("; ", CathodeJson.Differences(CathodeJson.Parse(d.Before.Resources), CathodeJson.Parse(d.After.Resources))));
                else node.Add(DiffKind.Changed, "resources: " + a + " -> " + b);
            }
            if (d.SpecialChanged) node.Add(DiffKind.Changed, SpecialLine(d.Before.Special, d.After.Special));
            if (d.BrushChanged) node.Add(DiffKind.Changed, "brush shape changed");
        }

        /// <summary>"relay -> Door_1.open" from "relay>ID.open", naming the target from the composite.</summary>
        public static string LinkLabel(string link, CompositeSnap composite)
        {
            int arrow = link.IndexOf('>');
            int dot = link.IndexOf('.', arrow + 1);
            if (arrow < 0 || dot < 0) return link;
            string from = link.Substring(0, arrow), target = link.Substring(arrow + 1, dot - arrow - 1), pin = link.Substring(dot + 1);
            string name = target;
            try
            {
                if (composite != null && composite.Entities.TryGetValue(new ShortGuid(target), out EntitySnap e) && !string.IsNullOrEmpty(e.Name)) name = e.Name;
            }
            catch { }
            return from + " -> " + name + "." + pin;
        }

        private static string ShowRefs(string text) => text == null ? "none" : CathodeJson.ShowReferences(CathodeJson.Parse(text) as JArray);
        private static string Short(string text, int max = 160) => text == null ? "none" : text.Length > max ? text.Substring(0, max) + "..." : text;

        private static string SpecialLine(string before, string after)
        {
            JObject a = before == null ? null : CathodeJson.Parse(before) as JObject;
            JObject b = after == null ? null : CathodeJson.Parse(after) as JObject;
            JObject any = b ?? a;
            //Where the two differ, when the counts alone don't say
            string where = a != null && b != null ? " (" + string.Join("; ", CathodeJson.Differences(a, b, 3)) + ")" : "";
            if (any != null && any["sequence"] != null)
                return "trigger sequence: " + Count(a, "sequence") + " entries / " + Count(a, "methods") + " events -> " + Count(b, "sequence") + " entries / " + Count(b, "methods") + " events" + where;
            return "animation: " + Count(a, "connections") + " bindings, " + Count(a, "float_tracks") + " tracks (" + Keys(a) + " keys), " + Count(a, "event_tracks") + " event tracks -> " +
                Count(b, "connections") + " bindings, " + Count(b, "float_tracks") + " tracks (" + Keys(b) + " keys), " + Count(b, "event_tracks") + " event tracks" + where;
        }
        private static int Count(JObject o, string key) => (o?[key] as JArray)?.Count ?? 0;
        private static int Keys(JObject o) => (o?["float_tracks"] as JArray)?.Sum(t => (t["keys"] as JArray)?.Count ?? 0) ?? 0;
    }
}
