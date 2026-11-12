using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.LevelDiff;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Recipes
{
    /// <summary>
    /// Turns what changed in a level (a <see cref="ScriptDelta"/> plus material values) into recipe steps, ordered so they replay one
    /// by one: composites made, new entities, changes to entities, links (which may join new entities), then removals.
    /// </summary>
    public static class RecipeBuilder
    {
        public sealed class Info
        {
            public string Name, Level;
            public ShortGuid Root = ShortGuid.Invalid;
            public IEnumerable<string> Steps;
            public IEnumerable<string> NotCarried;
            public DateTime Recorded = DateTime.Now;
        }

        public static Recipe Build(ScriptDelta delta, Info info, Table materialsBefore = null, Table materialsAfter = null)
        {
            Recipe recipe = new Recipe();
            recipe.Header["name"] = info.Name ?? "Recorded edits";
            recipe.Header["level"] = info.Level;
            recipe.Header["root"] = info.Root.IsInvalid ? null : CathodeJson.Id(info.Root);
            recipe.Header["recorded"] = info.Recorded.ToString("yyyy-MM-ddTHH:mm:ss");
            recipe.Header["steps"] = new JArray((info.Steps ?? Enumerable.Empty<string>()).ToArray());
            List<string> notCarried = new List<string>(info.NotCarried ?? Enumerable.Empty<string>());

            //1. Composites made, renamed, re-pinned
            foreach (CompositeSnap c in delta.AddedComposites)
            {
                JObject op = Op("add_composite", c, null);
                op["name"] = c.Name;
                if (c.Pins != null) op["pins"] = CathodeJson.Parse(c.Pins);
                if (c.Colours != null) op["colours"] = CathodeJson.Parse(c.Colours);
                recipe.Ops.Add(op);
            }
            foreach (CompositeDelta c in delta.ChangedComposites)
            {
                if (c.Renamed)
                {
                    JObject op = Op("rename_composite", c.After, null);
                    op["from"] = c.Before.Name;
                    op["to"] = c.After.Name;
                    recipe.Ops.Add(op);
                }
                if (c.PinsChanged) recipe.Ops.Add(BeforeAfter(Op("set_pins", c.After, null), c.Before.Pins, c.After.Pins));
                if (c.ColoursChanged) recipe.Ops.Add(BeforeAfter(Op("set_colours", c.After, null), c.Before.Colours, c.After.Colours));
            }

            //2. Every new entity, whole (its links come with the links below)
            foreach (CompositeSnap c in delta.AddedComposites)
                foreach (EntitySnap e in c.Entities.Values)
                    recipe.Ops.Add(AddEntity(c, e));
            foreach (CompositeDelta c in delta.ChangedComposites)
                foreach (EntitySnap e in c.Added)
                    recipe.Ops.Add(AddEntity(c.After, e));

            //3. Changes to entities that were there
            foreach (CompositeDelta c in delta.ChangedComposites)
                foreach (EntityDelta d in c.Changed.Where(o => o.Real))
                {
                    if (d.KindChanged) recipe.Ops.Add(BeforeAfter(Op("set_spec", c.After, d.After), d.Before.Spec, d.After.Spec));
                    foreach (ShortGuid p in d.ParamsChanged.Concat(d.ParamsAdded))
                    {
                        JObject op = Op("set_param", c.After, d.After);
                        op["param"] = CathodeJson.Name(p);
                        op["before"] = d.Before.Params.TryGetValue(p, out string was) ? CathodeJson.Parse(was) : JValue.CreateNull();
                        op["after"] = CathodeJson.Parse(d.After.Params[p]);
                        recipe.Ops.Add(op);
                    }
                    foreach (ShortGuid p in d.ParamsRemoved)
                    {
                        JObject op = Op("remove_param", c.After, d.After);
                        op["param"] = CathodeJson.Name(p);
                        op["before"] = CathodeJson.Parse(d.Before.Params[p]);
                        recipe.Ops.Add(op);
                    }
                    if (d.ResourcesChanged) recipe.Ops.Add(BeforeAfter(Op("set_resources", c.After, d.After), d.Before.Resources, d.After.Resources));
                    if (d.SpecialChanged)
                    {
                        //An animation can be big: the step holds the new one whole and only a hash of the old (what a conflict check needs)
                        JObject op = Op("set_special", c.After, d.After);
                        op["before_hash"] = Hash(d.Before.Special);
                        op["after"] = d.After.Special == null ? JValue.CreateNull() : CathodeJson.Parse(d.After.Special);
                        recipe.Ops.Add(op);
                    }
                    if (d.BrushChanged)
                    {
                        JObject op = Op("set_brush", c.After, d.After);
                        op["before"] = d.Before.Brush;
                        op["after"] = d.After.Brush;
                        recipe.Ops.Add(op);
                    }
                }

            //4. Links: of new entities, and of entities whose links changed (their whole list, in firing order)
            foreach (CompositeSnap c in delta.AddedComposites)
                foreach (EntitySnap e in c.Entities.Values.Where(o => o.Links.Count != 0))
                    recipe.Ops.Add(Links(c, e, null, e));
            foreach (CompositeDelta c in delta.ChangedComposites)
            {
                foreach (EntitySnap e in c.Added.Where(o => o.Links.Count != 0))
                    recipe.Ops.Add(Links(c.After, e, null, e));
                foreach (EntityDelta d in c.Changed.Where(o => o.LinksChanged))
                    recipe.Ops.Add(Links(c.After, d.After, d.Before, d.After));
            }

            //5. What went
            foreach (CompositeDelta c in delta.ChangedComposites)
                foreach (EntitySnap e in c.Removed)
                {
                    JObject op = Op("remove_entity", c.Before, e);
                    op["kind"] = e.Kind;
                    recipe.Ops.Add(op);
                }
            foreach (CompositeSnap c in delta.RemovedComposites)
            {
                JObject op = Op("remove_composite", c, null);
                op["name"] = c.Name;
                op["entities"] = c.Entities.Count;
                recipe.Ops.Add(op);
            }

            //6. Material values, by material
            if (materialsBefore != null && materialsAfter != null)
                Materials(recipe, materialsBefore, materialsAfter, notCarried);

            recipe.Header["not_carried"] = new JArray(notCarried.ToArray());
            recipe.Header["summary"] = recipe.Summary();
            return recipe;
        }

        private static JObject Op(string kind, CompositeSnap composite, EntitySnap entity)
        {
            JObject op = new JObject() { ["op"] = kind, ["composite"] = CathodeJson.Id(composite.Id), ["composite_name"] = composite.Name };
            if (entity != null)
            {
                op["entity"] = CathodeJson.Id(entity.Id);
                if (!string.IsNullOrEmpty(entity.Name)) op["entity_name"] = entity.Name;
            }
            return op;
        }

        private static JObject BeforeAfter(JObject op, string before, string after)
        {
            op["before"] = before == null ? JValue.CreateNull() : CathodeJson.Parse(before);
            op["after"] = after == null ? JValue.CreateNull() : CathodeJson.Parse(after);
            return op;
        }

        public static string Hash(string text)
        {
            if (text == null) return null;
            ulong h = 14695981039346656037UL;
            foreach (char ch in text) { h ^= ch; h *= 1099511628211UL; }
            return h.ToString("x16");
        }

        private static JObject AddEntity(CompositeSnap c, EntitySnap e)
        {
            JObject op = Op("add_entity", c, e);
            op["spec"] = CathodeJson.Parse(e.Spec);
            JObject parameters = new JObject();
            foreach (KeyValuePair<ShortGuid, string> p in e.Params) parameters[CathodeJson.Name(p.Key)] = CathodeJson.Parse(p.Value);
            op["params"] = parameters;
            if (e.Resources != null) op["resources"] = CathodeJson.Parse(e.Resources);
            if (e.Special != null) op["special"] = CathodeJson.Parse(e.Special);
            if (e.Brush != null) op["brush"] = e.Brush;
            return op;
        }

        /// <summary>A link's text ("param>ID.param") as {"from","to","pin","id"}.</summary>
        public static JObject LinkJson(string text, ShortGuid id)
        {
            int arrow = text.IndexOf('>');
            string target = text.Substring(arrow + 1, 11);
            return new JObject() { ["from"] = text.Substring(0, arrow), ["to"] = target, ["pin"] = text.Substring(arrow + 13), ["id"] = CathodeJson.Id(id) };
        }

        private static JArray LinkList(EntitySnap e)
        {
            JArray list = new JArray();
            if (e == null) return list;
            for (int i = 0; i < e.Links.Count; i++) list.Add(LinkJson(e.Links[i], i < e.LinkIds.Count ? e.LinkIds[i] : ShortGuid.Invalid));
            return list;
        }

        private static JObject Links(CompositeSnap c, EntitySnap e, EntitySnap before, EntitySnap after)
        {
            JObject op = Op("links", c, e);
            op["before"] = LinkList(before);
            op["after"] = LinkList(after);
            //What came and went, for a target whose links aren't exactly 'before' (merged rather than replaced)
            List<string> was = before?.Links ?? new List<string>();
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (string l in was) counts[l] = (counts.TryGetValue(l, out int n) ? n : 0) + 1;
            JArray add = new JArray(), remove = new JArray();
            for (int i = 0; i < after.Links.Count; i++)
            {
                string l = after.Links[i];
                if (counts.TryGetValue(l, out int n) && n > 0) counts[l] = n - 1;
                else add.Add(LinkJson(l, i < after.LinkIds.Count ? after.LinkIds[i] : ShortGuid.Invalid));
            }
            foreach (KeyValuePair<string, int> left in counts)
                for (int i = 0; i < left.Value; i++) remove.Add(LinkJson(left.Key, ShortGuid.Invalid));
            op["add"] = add;
            op["remove"] = remove;
            return op;
        }

        /// <summary>The material fields a recipe can set by name: texture slots, constants, priority, environment map slot, physical material.</summary>
        public static bool IsReplayableMaterialField(string field)
        {
            return field.StartsWith("texture slot ", StringComparison.Ordinal)
                || System.Text.RegularExpressions.Regex.IsMatch(field, "^(engine|vertex|pixel|hull|domain) constant [0-9]+$")
                || field == "priority" || field == "environment map slot" || field == "physical material";
        }

        private static void Materials(Recipe recipe, Table before, Table after, List<string> notCarried)
        {
            Dictionary<string, Table.Row> was = before.Rows.GroupBy(o => o.Key).ToDictionary(o => o.Key, o => o.First());
            //A material sharing its name is keyed by name + content hash, which its edit changed: the lone one of a name on each side is it
            Dictionary<string, Table.Row> paired = LevelComparer.PairByPlainName(before, after);
            foreach (Table.Row row in after.Rows)
            {
                if (!was.TryGetValue(row.Key, out Table.Row old) && !paired.TryGetValue(row.Key, out old))
                {
                    //A material made during the recording (an import, a duplicate): it isn't carried, only its use
                    if (!before.Rows.Any(o => o.Label == row.Label)) notCarried.Add("material " + row.Key + " (made while recording: port or import it into the target first)");
                    continue;
                }
                if (old.Line == row.Line) continue;
                Dictionary<string, string> a = old.Fields.GroupBy(o => o.Key).ToDictionary(o => o.Key, o => o.First().Value);
                foreach (KeyValuePair<string, string> f in row.Fields)
                {
                    a.TryGetValue(f.Key, out string v);
                    if (v == f.Value) continue;
                    if (!IsReplayableMaterialField(f.Key)) { notCarried.Add("material " + old.Key + " " + f.Key + " (" + v + " -> " + f.Value + ")"); continue; }
                    //Named as the target has it: as it was before the edit
                    recipe.Ops.Add(new JObject() { ["op"] = "set_material", ["material"] = old.Key, ["field"] = f.Key, ["before"] = v, ["after"] = f.Value });
                }
            }
        }
    }
}
