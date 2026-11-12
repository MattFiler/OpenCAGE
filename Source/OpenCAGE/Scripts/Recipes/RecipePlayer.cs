using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.LevelDiff;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenCAGE.Recipes
{
    public enum StepStatus
    {
        /// <summary>The target is as the recipe found it: the step applies.</summary>
        Ready,
        /// <summary>The target already has the step's result.</summary>
        Done,
        /// <summary>The target has something else there (someone changed it): skipped unless conflicts are overwritten.</summary>
        Conflict,
        /// <summary>What the step works on (a composite, an entity, a model) isn't in the target.</summary>
        Missing,
        /// <summary>It was tried and failed (Apply).</summary>
        Failed,
    }

    public sealed class PlannedStep
    {
        public int Index;
        public JObject Op;
        public StepStatus Status;
        public string Note;
        /// <summary>A links step whose target had other links too: the recorded ones are added and removed, the rest kept.</summary>
        public bool Merge;
        /// <summary>What Apply did with it.</summary>
        public string Result;

        public string Describe() => RecipeText.Describe(Op);
    }

    /// <summary>What a replay would do (the dry run), step by step, and how it gives ids to what it adds.</summary>
    public sealed class RecipePlan
    {
        public Recipe Recipe;
        public string Target;
        /// <summary>New composites, entities and links get new ids (another level, or the recorded ids are taken here).</summary>
        public bool Remap;
        public string ModeReason;
        public IdMap Map = new IdMap();
        public readonly List<PlannedStep> Steps = new List<PlannedStep>();

        public string Mode => Remap ? "remap" : "keep";
        public int Count(StepStatus status) => Steps.Count(o => o.Status == status);

        public string Summary()
        {
            return Steps.Count + " steps: " + Count(StepStatus.Ready) + " ready" + (Steps.Any(o => o.Merge) ? " (" + Steps.Count(o => o.Merge) + " merged with links already there)" : "") + ", " +
                Count(StepStatus.Done) + " already done, " + Count(StepStatus.Conflict) + " conflicts, " + Count(StepStatus.Missing) + " missing, " + Count(StepStatus.Failed) + " failed. Ids: " +
                (Remap ? "new ids for what it adds" : "the recorded ids") + " - " + ModeReason;
        }

        public JObject ToJson(int max = 200, bool all = false)
        {
            JObject o = new JObject()
            {
                ["recipe"] = Recipe.Name,
                ["recorded_on"] = Recipe.Level,
                ["target"] = Target,
                ["ids"] = Mode,
                ["ids_why"] = ModeReason,
                ["summary"] = Summary(),
                ["ready"] = Count(StepStatus.Ready),
                ["already_done"] = Count(StepStatus.Done),
                ["conflicts"] = Count(StepStatus.Conflict),
                ["missing"] = Count(StepStatus.Missing),
                ["failed"] = Count(StepStatus.Failed),
            };
            if (Recipe.NotCarried.Count != 0) o["not_carried"] = new JArray(Recipe.NotCarried.ToArray());
            JArray steps = new JArray();
            foreach (PlannedStep s in Steps.Where(x => all || x.Status != StepStatus.Ready || x.Merge || x.Result != null).Take(max))
            {
                JObject step = new JObject() { ["step"] = s.Index + 1, ["status"] = s.Status.ToString().ToLowerInvariant(), ["what"] = s.Describe() };
                if (s.Note != null) step["note"] = s.Note;
                if (s.Merge) step["merged"] = true;
                if (s.Result != null) step["result"] = s.Result;
                steps.Add(step);
            }
            o[all ? "steps" : "steps_not_simply_ready"] = steps;
            return o;
        }
    }

    /// <summary>
    /// Where a replay's changes go through. The editor's host makes them one undo step (McpScriptEdit); <see cref="PlainHost"/>
    /// changes the level directly (offline use).
    /// </summary>
    public interface IRecipeHost
    {
        void TouchEntries();
        void TouchContents(Composite composite);
        void TouchPins(Composite composite);
        void Touch(Composite composite, Entity entity);
        void Made(Composite composite, Entity entity);
        void Delete(Composite composite, Entity entity);
        Composite AddComposite(string name, ShortGuid id);
        void RemoveComposite(Composite composite);
        void Changed(Composite composite, bool links);
        /// <summary>State the host's undo doesn't hold: set(false) puts the change, set(true) what was there. The change is already made.</summary>
        void AfterEach(Action<bool> set);
        /// <summary>A material value changed (already made): set(false) puts the change, set(true) what was there.</summary>
        void MaterialChanged(Materials.Material material, string label, Action<bool> set);
    }

    /// <summary>Straight into the level, no undo: for use outside the editor.</summary>
    public sealed class PlainHost : IRecipeHost
    {
        private readonly Commands _commands;
        public PlainHost(Commands commands) { _commands = commands; }
        public void TouchEntries() { }
        public void TouchContents(Composite composite) { }
        public void TouchPins(Composite composite) { }
        public void Touch(Composite composite, Entity entity) { }
        public void Made(Composite composite, Entity entity) { }
        public void Delete(Composite composite, Entity entity)
        {
            foreach (Entity other in composite.GetEntities())
                other.childLinks.RemoveAll(o => o.linkedEntityID == entity.shortGUID);
            composite.RemoveEntity(entity);
        }
        public Composite AddComposite(string name, ShortGuid id)
        {
            Composite composite = new Composite(name) { shortGUID = id };
            _commands.Entries.Add(composite);
            return composite;
        }
        public void RemoveComposite(Composite composite) => _commands.Entries.Remove(composite);
        public void Changed(Composite composite, bool links) { }
        public void AfterEach(Action<bool> set) { }
        public void MaterialChanged(Materials.Material material, string label, Action<bool> set) { }
    }

    /// <summary>
    /// Replays a <see cref="Recipe"/> onto a level. <see cref="Plan"/> is the dry run: each step ready, already done, in conflict
    /// (the target holds neither the recorded before nor after) or missing what it works on, and whether new things keep their
    /// recorded ids or get new ones. <see cref="Apply"/> makes the ready steps through a host.
    /// </summary>
    public static class RecipePlayer
    {
        /// <param name="ids">auto (default), keep or remap.</param>
        public static RecipePlan Plan(Recipe recipe, Level target, string ids = "auto")
        {
            Commands commands = target.Commands;
            ResourceNames names = new ResourceNames(target);
            RecipePlan plan = new RecipePlan() { Recipe = recipe, Target = target.Name };
            ShortGuid root = ShortGuid.Invalid;
            try { root = commands.EntryPoints?[0]?.shortGUID ?? ShortGuid.Invalid; } catch { }

            //Ids: kept on a copy of the recorded level when none of them is taken there; else new ones
            List<JObject> adds = recipe.Ops.Where(o => (string)o["op"] == "add_composite" || (string)o["op"] == "add_entity").ToList();
            List<string> taken = new List<string>();
            foreach (JObject op in adds)
            {
                if ((string)op["op"] == "add_composite")
                {
                    Composite there = commands.GetComposite(CathodeJson.ToId(op["composite"]));
                    if (there != null && there.name != (string)op["name"]) taken.Add("composite " + (string)op["composite"] + " (here " + there.name + ")");
                }
                else
                {
                    Composite c = commands.GetComposite(CathodeJson.ToId(op["composite"]));
                    Entity there = c?.GetEntityByID(CathodeJson.ToId(op["entity"]));
                    if (there != null && EntitySnap.KindText(EntitySnap.SpecOf(there)) != EntitySnap.KindText((JObject)op["spec"]))
                        taken.Add("entity " + (string)op["entity"] + " in " + c.name);
                }
            }
            bool sameLevel = recipe.Root != null && !root.IsInvalid && recipe.Root == CathodeJson.Id(root);
            string mode = (ids ?? "auto").Trim().ToLowerInvariant();
            if (mode == "keep") { plan.Remap = false; plan.ModeReason = "asked to keep the recorded ids" + (taken.Count != 0 ? " (" + taken.Count + " are taken here: those steps conflict)" : ""); }
            else if (mode == "remap") { plan.Remap = true; plan.ModeReason = "asked for new ids"; }
            else if (adds.Count == 0) { plan.Remap = false; plan.ModeReason = "it adds nothing, so no ids are given"; }
            else if (!sameLevel) { plan.Remap = true; plan.ModeReason = "recorded on " + (recipe.Level ?? "another level") + ", and this is " + target.Name + " (another root composite): what it adds gets new ids, and every link and path to it follows"; }
            else if (taken.Count != 0) { plan.Remap = true; plan.ModeReason = taken.Count + " of the recorded ids are taken here by something else (" + string.Join(", ", taken.Take(3)) + (taken.Count > 3 ? ", ..." : "") + "): new ids, links and paths follow"; }
            else { plan.Remap = false; plan.ModeReason = "this is a copy of " + (recipe.Level ?? "the recorded level") + " (same root composite) and none of the " + adds.Count + " recorded ids is taken: it keeps them, so later recipes recorded after this one still find what it adds"; }

            if (plan.Remap)
                foreach (JObject op in adds)
                {
                    ShortGuid id = CathodeJson.ToId(op[(string)op["op"] == "add_composite" ? "composite" : "entity"]);
                    if (plan.Map.Contains(id)) continue;
                    ShortGuid fresh = (string)op["op"] == "add_composite" ? (Composite.DisplayModelId((string)op["name"]) ?? ShortGuidUtils.GenerateRandom()) : ShortGuidUtils.GenerateRandom();
                    plan.Map.Add(id, fresh);
                }
            else
                ShortGuidUtils.ReserveInUse(adds.Select(o => CathodeJson.ToId(o[(string)o["op"] == "add_composite" ? "composite" : "entity"])));

            Simulation sim = new Simulation(commands, plan.Map);
            for (int i = 0; i < recipe.Ops.Count; i++)
            {
                PlannedStep step = new PlannedStep() { Index = i, Op = recipe.Ops[i] };
                try { Check(step, sim, target, names, plan); }
                catch (RecipeException e) { step.Status = StepStatus.Missing; step.Note = e.Message; }
                catch (Exception e) { step.Status = StepStatus.Failed; step.Note = e.GetType().Name + ": " + e.Message; }
                plan.Steps.Add(step);
            }
            return plan;
        }

        /// <summary>The target as the plan goes through the steps: what composites and entities will be there.</summary>
        private sealed class Simulation
        {
            public readonly Commands Commands;
            public readonly IdMap Map;
            public readonly Dictionary<ShortGuid, string> AddedComposites = new Dictionary<ShortGuid, string>();
            public readonly HashSet<ShortGuid> RemovedComposites = new HashSet<ShortGuid>();
            public readonly HashSet<(ShortGuid, ShortGuid)> AddedEntities = new HashSet<(ShortGuid, ShortGuid)>();
            public readonly HashSet<(ShortGuid, ShortGuid)> RemovedEntities = new HashSet<(ShortGuid, ShortGuid)>();
            /// <summary>Recorded ids of composites / entities the recipe adds whose add won't happen (a conflict): steps on them are missing.</summary>
            public readonly HashSet<ShortGuid> BlockedComposites = new HashSet<ShortGuid>();
            public readonly HashSet<(ShortGuid, ShortGuid)> BlockedEntities = new HashSet<(ShortGuid, ShortGuid)>();
            public Simulation(Commands commands, IdMap map) { Commands = commands; Map = map; }

            /// <summary>The target's id for the step's composite: the (mapped) id, else the only composite of its name.</summary>
            public ShortGuid CompositeId(JObject op, out Composite composite, out string note)
            {
                note = null;
                composite = null;
                if (BlockedComposites.Contains(CathodeJson.ToId(op["composite"]))) { note = "the recipe's composite " + ((string)op["composite_name"] ?? (string)op["composite"]) + " was not added"; return ShortGuid.Invalid; }
                ShortGuid id = Map.Map(CathodeJson.ToId(op["composite"]));
                composite = RemovedComposites.Contains(id) ? null : Commands.GetComposite(id);
                if (composite != null || AddedComposites.ContainsKey(id)) return id;
                string name = (string)op["composite_name"] ?? (string)op["name"];
                if (name != null)
                {
                    List<Composite> named = Commands.Entries.Where(o => o != null && string.Equals(o.name, name, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (named.Count == 1 && !RemovedComposites.Contains(named[0].shortGUID)) { composite = named[0]; note = "found by name (another id here)"; return composite.shortGUID; }
                }
                return ShortGuid.Invalid;
            }

            public bool CompositeExists(ShortGuid id) => !id.IsInvalid && !RemovedComposites.Contains(id) && (AddedComposites.ContainsKey(id) || Commands.GetComposite(id) != null);

            public bool EntityExists(ShortGuid composite, Composite c, ShortGuid entity)
            {
                if (RemovedEntities.Contains((composite, entity))) return false;
                if (AddedEntities.Contains((composite, entity))) return true;
                return c?.GetEntityByID(entity) != null;
            }
        }

        private static void Check(PlannedStep step, Simulation sim, Level target, ResourceNames names, RecipePlan plan)
        {
            JObject op = step.Op;
            string kind = (string)op["op"];
            IdMap map = plan.Map;
            if (kind == "set_material") { CheckMaterial(step, target, names); return; }
            if (kind == "add_composite")
            {
                //Its own id only (no finding it by name): a composite of that name already here is a clash, not this one
                string name = (string)op["name"];
                ShortGuid recorded = CathodeJson.ToId(op["composite"]), mapped = map.Map(recorded);
                Composite there = sim.RemovedComposites.Contains(mapped) ? null : sim.Commands.GetComposite(mapped);
                if (there != null)
                {
                    if (string.Equals(there.name, name, StringComparison.OrdinalIgnoreCase)) { step.Status = StepStatus.Done; step.Note = "it is here already"; return; }
                    step.Status = StepStatus.Conflict; step.Note = "its id is " + there.name + " here"; sim.BlockedComposites.Add(recorded); return;
                }
                Composite same = sim.Commands.Entries.FirstOrDefault(o => o != null && string.Equals(o.name, name, StringComparison.OrdinalIgnoreCase) && !sim.RemovedComposites.Contains(o.shortGUID));
                if (same != null || sim.AddedComposites.Values.Any(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase)))
                {
                    step.Status = StepStatus.Conflict; step.Note = "a composite called " + name + " is here already (another id" + (plan.Remap ? "; replayed before?" : "") + ")"; sim.BlockedComposites.Add(recorded); return;
                }
                sim.AddedComposites[mapped] = name;
                step.Status = StepStatus.Ready;
                return;
            }

            ShortGuid cid = sim.CompositeId(op, out Composite composite, out string foundNote);
            step.Note = foundNote;
            switch (kind)
            {
                case "remove_composite":
                    if (!sim.CompositeExists(cid)) { step.Status = StepStatus.Done; return; }
                    sim.RemovedComposites.Add(cid);
                    step.Status = StepStatus.Ready;
                    return;
            }

            if (!sim.CompositeExists(cid)) { step.Status = StepStatus.Missing; step.Note = "the composite " + ((string)op["composite_name"] ?? (string)op["composite"]) + " is not in this level"; return; }
            bool added = sim.AddedComposites.ContainsKey(cid);
            switch (kind)
            {
                case "rename_composite":
                    {
                        string name = added ? sim.AddedComposites[cid] : composite.name;
                        step.Status = name == (string)op["to"] ? StepStatus.Done : name == (string)op["from"] ? StepStatus.Ready : StepStatus.Conflict;
                        if (step.Status == StepStatus.Conflict) step.Note = "it is called " + name + " here";
                        return;
                    }
                case "set_pins":
                case "set_colours":
                    {
                        string now = added ? null : kind == "set_pins" ? PinsText(sim.Commands, cid) : ColoursText(sim.Commands, cid);
                        Compare(step, now, op["before"], op["after"]);
                        return;
                    }
            }

            if (sim.BlockedEntities.Contains((cid, CathodeJson.ToId(op["entity"])))) { step.Status = StepStatus.Missing; step.Note = "the recipe's entity " + ((string)op["entity_name"] ?? (string)op["entity"]) + " was not added (its add conflicts)"; return; }
            ShortGuid eid = map.Map(CathodeJson.ToId(op["entity"]));
            Entity entity = composite?.GetEntityByID(eid);
            bool exists = sim.EntityExists(cid, composite, eid);
            if (kind == "add_entity")
            {
                if (exists)
                {
                    if (entity != null && EntitySnap.KindText(EntitySnap.SpecOf(entity)) == EntitySnap.KindText(RemapSpec((JObject)op["spec"], map)))
                    { step.Status = StepStatus.Done; step.Note = "it is here already"; return; }
                    step.Status = StepStatus.Conflict; step.Note = "its id is another entity here"; sim.BlockedEntities.Add((cid, CathodeJson.ToId(op["entity"]))); return;
                }
                //Every value must be makeable here (the models and materials it draws by name, the composite it places)
                JObject spec = (JObject)op["spec"];
                if ((string)spec["kind"] == "instance" && !sim.CompositeExists(map.Map(CathodeJson.ToId(spec["composite"]))))
                    throw new RecipeException("the composite it places (" + (string)spec["composite"] + ") is not in this level");
                foreach (JProperty p in ((JObject)op["params"]).Properties()) CathodeJson.ToValue(p.Value, names, map);
                if (op["resources"] != null) CathodeJson.ToReferences(op["resources"], names, map);
                sim.AddedEntities.Add((cid, eid));
                step.Status = StepStatus.Ready;
                return;
            }
            if (kind == "remove_entity")
            {
                if (!exists) { step.Status = StepStatus.Done; return; }
                if (entity != null && op["kind"] != null && EntitySnap.KindText(EntitySnap.SpecOf(entity)) != (string)op["kind"]) { step.Status = StepStatus.Conflict; step.Note = "it is something else here"; return; }
                sim.RemovedEntities.Add((cid, eid));
                step.Status = StepStatus.Ready;
                return;
            }
            if (!exists) { step.Status = StepStatus.Missing; step.Note = "the entity " + ((string)op["entity_name"] ?? (string)op["entity"]) + " is not in " + ((string)op["composite_name"] ?? "its composite") + " here"; return; }
            bool fresh = entity == null; //made by an earlier step of this recipe
            switch (kind)
            {
                case "set_param":
                    {
                        string now = fresh ? null : ParamText(entity, (string)op["param"], names);
                        if (op["after"] != null && op["after"].Type != JTokenType.Null) CathodeJson.ToValue(op["after"], names, map);
                        Compare(step, now, op["before"], RemapValue(op["after"], map), map);
                        return;
                    }
                case "remove_param":
                    {
                        string now = fresh ? null : ParamText(entity, (string)op["param"], names);
                        if (now == null) { step.Status = StepStatus.Done; return; }
                        step.Status = now == CathodeJson.Text(op["before"]) ? StepStatus.Ready : StepStatus.Conflict;
                        if (step.Status == StepStatus.Conflict) step.Note = "it is " + CathodeJson.Show(CathodeJson.Parse(now)) + " here";
                        return;
                    }
                case "set_spec":
                    Compare(step, fresh ? null : CathodeJson.Text(EntitySnap.SpecOf(entity)), op["before"], RemapSpec(op["after"] as JObject, map), map);
                    return;
                case "set_resources":
                    {
                        string now = fresh || !(entity is FunctionEntity fe) || fe.resources == null || fe.resources.Count == 0 ? null : CathodeJson.Text(CathodeJson.References(fe.resources, names));
                        if (op["after"] != null && op["after"].Type != JTokenType.Null) CathodeJson.ToReferences(op["after"], names, map);
                        Compare(step, now, op["before"], op["after"], map);
                        return;
                    }
                case "set_special":
                    {
                        string now = fresh ? null : SpecialText(entity);
                        string afterText = op["after"] == null || op["after"].Type == JTokenType.Null ? null : CathodeJson.Text(op["after"]);
                        if (now == afterText && !plan.Remap) step.Status = StepStatus.Done;
                        else if (RecipeBuilder.Hash(now) == (string)op["before_hash"]) step.Status = StepStatus.Ready;
                        else { step.Status = StepStatus.Conflict; step.Note = "its animation / sequence is not as the recipe found it"; }
                        return;
                    }
                case "set_brush":
                    {
                        string now = fresh ? null : BrushText(sim.Commands, cid, eid);
                        string before = (string)op["before"], after = (string)op["after"];
                        step.Status = now == after ? StepStatus.Done : now == before ? StepStatus.Ready : StepStatus.Conflict;
                        return;
                    }
                case "links":
                    {
                        List<string> now = fresh ? new List<string>() : entity.childLinks.Select(EntitySnap.LinkText).ToList();
                        List<string> before = LinkTexts(op["before"] as JArray, map), after = LinkTexts(op["after"] as JArray, map);
                        foreach (JToken l in (JArray)op["after"])
                        {
                            ShortGuid to = map.Map(CathodeJson.ToId(l["to"]));
                            if (!sim.EntityExists(cid, composite, to)) throw new RecipeException("a link goes to " + (string)l["to"] + ", which is not in " + ((string)op["composite_name"] ?? "the composite") + " here");
                        }
                        if (now.SequenceEqual(after)) step.Status = StepStatus.Done;
                        else if (now.SequenceEqual(before)) step.Status = StepStatus.Ready;
                        else { step.Status = StepStatus.Ready; step.Merge = true; step.Note = "it has other links here too: the recorded ones are added and removed, the rest kept"; }
                        return;
                    }
                default:
                    throw new RecipeException("unknown step '" + kind + "' (a newer OpenCAGE's?)");
            }
        }

        private static void Compare(PlannedStep step, string now, JToken before, JToken after, IdMap map = null)
        {
            string b = before == null || before.Type == JTokenType.Null ? null : CathodeJson.Text(before);
            string a = after == null || after.Type == JTokenType.Null ? null : CathodeJson.Text(after);
            if (now == a) step.Status = StepStatus.Done;
            else if (now == b) step.Status = StepStatus.Ready;
            else
            {
                step.Status = StepStatus.Conflict;
                step.Note = "it is " + (now == null ? "not set" : CathodeJson.Show(CathodeJson.Parse(now))) + " here (the recipe found " + (b == null ? "it not set" : CathodeJson.Show(before)) + ")";
            }
        }

        private static JToken RemapValue(JToken value, IdMap map)
        {
            //A resource value carries ids (its own, the entity's): compare against what the replay would write
            if (map == null || map.Count == 0 || !(value is JObject o) || o["resource"] == null) return value;
            JObject copy = (JObject)o.DeepClone();
            copy["resource"]["id"] = CathodeJson.Id(map.Map(CathodeJson.ToId(copy["resource"]["id"])));
            foreach (JToken r in (copy["resource"]["refs"] as JArray) ?? new JArray())
            {
                if (r is JObject ro && ro["id"] != null) ro["id"] = CathodeJson.Id(map.Map(CathodeJson.ToId(ro["id"])));
                if (r is JObject re && re["entity"] != null) re["entity"] = CathodeJson.Id(map.Map(CathodeJson.ToId(re["entity"])));
            }
            return copy;
        }

        private static JObject RemapSpec(JObject spec, IdMap map)
        {
            if (spec == null || map == null || map.Count == 0) return spec;
            JObject copy = (JObject)spec.DeepClone();
            if (copy["composite"] != null) copy["composite"] = CathodeJson.Id(map.Map(CathodeJson.ToId(copy["composite"])));
            if (copy["path"] is JArray path) copy["path"] = new JArray(path.Select(o => CathodeJson.Id(map.Map(CathodeJson.ToId(o)))));
            return copy;
        }

        private static List<string> LinkTexts(JArray links, IdMap map)
        {
            List<string> list = new List<string>();
            if (links == null) return list;
            foreach (JToken l in links)
                list.Add((string)l["from"] + ">" + CathodeJson.Id(map.Map(CathodeJson.ToId(l["to"]))) + "." + (string)l["pin"]);
            return list;
        }

        private static string ParamText(Entity entity, string name, ResourceNames names)
        {
            Parameter p = entity.GetParameter(CathodeJson.ToName(name));
            return p == null ? null : CathodeJson.Text(CathodeJson.Value(p.content, names));
        }

        private static string SpecialText(Entity entity)
        {
            switch (entity)
            {
                case CAGEAnimation a: return CathodeJson.Text(CathodeJson.Animation(a));
                case TriggerSequence t: return CathodeJson.Text(CathodeJson.Sequence(t.sequence, t.methods));
                case ProxyEntity p: return (p.sequence?.Count ?? 0) == 0 && (p.methods?.Count ?? 0) == 0 ? null : CathodeJson.Text(CathodeJson.Sequence(p.sequence, p.methods));
                default: return null;
            }
        }

        private static string PinsText(Commands commands, ShortGuid composite)
        {
            List<CompositePinInfoTable.PinInfo> pins = commands.Utils.GetAllCustomPinInfo(composite);
            return pins == null || pins.Count == 0 ? null : CathodeJson.Text(CompositeSnap.PinsJson(pins));
        }

        private static string ColoursText(Commands commands, ShortGuid composite)
        {
            HashSet<ShortGuid> colours = commands.Utils.GetColourParameters(composite);
            return colours == null || colours.Count == 0 ? null : CathodeJson.Text(new JArray(colours.Select(CathodeJson.Id).OrderBy(o => o, StringComparer.Ordinal)));
        }

        private static string BrushText(Commands commands, ShortGuid composite, ShortGuid entity)
        {
            try { return commands.Utils.GetBrush(composite, entity); } catch { return null; }
        }

        private static void CheckMaterial(PlannedStep step, Level target, ResourceNames names)
        {
            JObject op = step.Op;
            Materials.Material m = names.FindMaterial((string)op["material"]);
            if (m == null) { step.Status = StepStatus.Missing; step.Note = "no material " + (string)op["material"] + " here"; return; }
            string field = (string)op["field"];
            string now = LevelComparer.MaterialFields(m, names).FirstOrDefault(o => o.Key == field).Value;
            string before = (string)op["before"], after = (string)op["after"];
            step.Status = now == after ? StepStatus.Done : now == before ? StepStatus.Ready : StepStatus.Conflict;
            if (step.Status == StepStatus.Conflict) step.Note = "it is " + (now ?? "(none)") + " here";
            if (field.StartsWith("texture slot ") && after != "none" && FindTexture(target, after).tex == null)
            { step.Status = StepStatus.Missing; step.Note = "no texture " + after + " here (import it first)"; }
        }

        private static (Textures.TEX4 tex, TexturePtr.Source source) FindTexture(Level level, string name)
        {
            Textures.TEX4 tex = level.Textures?.Entries?.FirstOrDefault(o => o != null && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            if (tex != null) return (tex, TexturePtr.Source.LEVEL);
            tex = level.Global?.Textures?.Entries?.FirstOrDefault(o => o != null && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            return (tex, TexturePtr.Source.GLOBAL);
        }

        #region Applying
        /// <summary>
        /// Make the plan's ready steps (and its conflicts, with <paramref name="overwriteConflicts"/>) through <paramref name="host"/>.
        /// Each step's values are read before anything is changed, so a step that fails leaves nothing half made. Steps are left with
        /// their Result; returns how many were made.
        /// </summary>
        public static int Apply(RecipePlan plan, Level target, IRecipeHost host, bool overwriteConflicts = false)
        {
            ResourceNames names = new ResourceNames(target);
            int made = 0;
            foreach (PlannedStep step in plan.Steps)
            {
                bool go = step.Status == StepStatus.Ready || (overwriteConflicts && step.Status == StepStatus.Conflict);
                if (!go) { step.Result = step.Status == StepStatus.Done ? "already done" : "skipped (" + step.Status.ToString().ToLowerInvariant() + ")"; continue; }
                try
                {
                    Execute(step, plan, target, names, host);
                    step.Result = step.Merge ? "made (merged)" : "made";
                    made++;
                }
                catch (Exception e)
                {
                    step.Status = StepStatus.Failed;
                    step.Result = "failed: " + (e is RecipeException ? e.Message : e.GetType().Name + ": " + e.Message);
                }
            }
            return made;
        }

        private static Composite FindComposite(Commands commands, JObject op, IdMap map)
        {
            Composite c = commands.GetComposite(map.Map(CathodeJson.ToId(op["composite"])));
            if (c != null) return c;
            string name = (string)op["composite_name"] ?? (string)op["name"];
            List<Composite> named = name == null ? new List<Composite>() : commands.Entries.Where(o => o != null && string.Equals(o.name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (named.Count == 1) return named[0];
            throw new RecipeException("the composite " + (name ?? (string)op["composite"]) + " is not in this level");
        }

        private static void Execute(PlannedStep step, RecipePlan plan, Level target, ResourceNames names, IRecipeHost host)
        {
            JObject op = step.Op;
            IdMap map = plan.Map;
            Commands commands = target.Commands;
            string kind = (string)op["op"];
            if (kind == "set_material") { ExecuteMaterial(op, target, names, host); return; }
            if (kind == "add_composite")
            {
                ShortGuid id = map.Map(CathodeJson.ToId(op["composite"]));
                host.TouchEntries();
                Composite added = host.AddComposite((string)op["name"], id);
                if (op["pins"] != null || op["colours"] != null)
                {
                    host.TouchPins(added);
                    if (op["pins"] is JArray pins) commands.Utils.ReplaceCustomPinInfos(added.shortGUID, ToPins(pins, map));
                    if (op["colours"] is JArray colours) commands.Utils.ReplaceColourParameters(added.shortGUID, colours.Select(o => map.Map(CathodeJson.ToId(o))));
                }
                return;
            }
            Composite composite = FindComposite(commands, op, map);
            switch (kind)
            {
                case "remove_composite":
                    host.TouchEntries();
                    host.RemoveComposite(composite);
                    return;
                case "rename_composite":
                    {
                        string from = composite.name, to = (string)op["to"];
                        composite.name = to;
                        host.AfterEach(reverted => composite.name = reverted ? from : to);
                        host.Changed(composite, false);
                        return;
                    }
                case "set_pins":
                    host.TouchPins(composite);
                    commands.Utils.ReplaceCustomPinInfos(composite.shortGUID, op["after"] is JArray p ? ToPins(p, map) : new List<CompositePinInfoTable.PinInfo>());
                    host.Changed(composite, false);
                    return;
                case "set_colours":
                    host.TouchPins(composite);
                    commands.Utils.ReplaceColourParameters(composite.shortGUID, op["after"] is JArray cl ? cl.Select(o => map.Map(CathodeJson.ToId(o))) : Enumerable.Empty<ShortGuid>());
                    host.Changed(composite, false);
                    return;
                case "add_entity":
                    AddEntity(op, composite, commands, names, map, host);
                    return;
            }

            ShortGuid eid = map.Map(CathodeJson.ToId(op["entity"]));
            Entity entity = composite.GetEntityByID(eid) ?? throw new RecipeException("the entity " + ((string)op["entity_name"] ?? (string)op["entity"]) + " is not in " + composite.name);
            switch (kind)
            {
                case "remove_entity":
                    host.Delete(composite, entity);
                    return;
                case "set_param":
                    {
                        ShortGuid name = CathodeJson.ToName(op["param"]);
                        ParameterData value = CathodeJson.ToValue(op["after"], names, map);
                        host.Touch(composite, entity);
                        Parameter existing = entity.GetParameter(name);
                        ParameterVariant variant = existing?.variant ?? Variant(commands, composite, entity, name);
                        entity.AddParameter(name, value, variant, true, false);
                        return;
                    }
                case "remove_param":
                    host.Touch(composite, entity);
                    entity.RemoveParameter(CathodeJson.ToName(op["param"]));
                    return;
                case "set_spec":
                    SetSpec(entity, (JObject)op["after"], map, host, composite);
                    return;
                case "set_resources":
                    {
                        if (!(entity is FunctionEntity fe)) throw new RecipeException("only a function entity has resources of its own");
                        List<ResourceReference> after = op["after"] == null || op["after"].Type == JTokenType.Null ? new List<ResourceReference>() : CathodeJson.ToReferences(op["after"], names, map);
                        List<ResourceReference> before = fe.resources;
                        host.Touch(composite, entity);
                        fe.resources = after;
                        host.AfterEach(reverted => fe.resources = reverted ? before : after);
                        return;
                    }
                case "set_special":
                    SetSpecial(entity, op["after"], map, host, composite);
                    return;
                case "set_brush":
                    {
                        string before = BrushText(commands, composite.shortGUID, entity.shortGUID), after = (string)op["after"];
                        void Put(string definition)
                        {
                            if (definition == null) commands.Utils.RemoveBrush(composite.shortGUID, entity.shortGUID);
                            else commands.Utils.SetBrush(composite.shortGUID, entity.shortGUID, definition);
                        }
                        Put(after);
                        host.AfterEach(reverted => Put(reverted ? before : after));
                        host.Changed(composite, false);
                        return;
                    }
                case "links":
                    {
                        List<EntityConnector> links;
                        if (step.Merge)
                        {
                            links = new List<EntityConnector>(entity.childLinks);
                            foreach (JToken r in (JArray)op["remove"])
                            {
                                ShortGuid from = CathodeJson.ToName(r["from"]), to = map.Map(CathodeJson.ToId(r["to"])), pin = CathodeJson.ToName(r["pin"]);
                                int at = links.FindIndex(o => o.thisParamID == from && o.linkedEntityID == to && o.linkedParamID == pin);
                                if (at >= 0) links.RemoveAt(at);
                            }
                            foreach (JToken a in (JArray)op["add"]) links.Add(ToLink(a, plan));
                        }
                        else links = ((JArray)op["after"]).Select(o => ToLink(o, plan)).ToList();
                        host.Touch(composite, entity);
                        entity.childLinks = links;
                        host.Changed(composite, true);
                        return;
                    }
                default:
                    throw new RecipeException("unknown step '" + kind + "'");
            }
        }

        private static ParameterVariant Variant(Commands commands, Composite composite, Entity entity, ShortGuid name)
        {
            try { return commands.Utils.GetParameterMetadata(entity, name, composite).Item1 ?? ParameterVariant.PARAMETER; }
            catch { return ParameterVariant.PARAMETER; }
        }

        private static EntityConnector ToLink(JToken l, RecipePlan plan)
        {
            EntityConnector link = new EntityConnector(plan.Map.Map(CathodeJson.ToId(l["to"])), CathodeJson.ToName(l["from"]), CathodeJson.ToName(l["pin"]));
            //The recorded link id on a copy of the level (unless taken); a new one otherwise
            ShortGuid id = l["id"] == null ? ShortGuid.Invalid : CathodeJson.ToId(l["id"]);
            if (!plan.Remap && !id.IsInvalid) link.ID = id;
            return link;
        }

        private static List<CompositePinInfoTable.PinInfo> ToPins(JArray pins, IdMap map)
        {
            List<CompositePinInfoTable.PinInfo> list = new List<CompositePinInfoTable.PinInfo>();
            foreach (JToken p in pins)
                list.Add(new CompositePinInfoTable.PinInfo() { VariableGUID = map.Map(CathodeJson.ToId(p[0])), PinTypeGUID = CathodeJson.ToName(p[1]), PinEnumTypeGUID = CathodeJson.ToName(p[2]) });
            return list;
        }

        private static void AddEntity(JObject op, Composite composite, Commands commands, ResourceNames names, IdMap map, IRecipeHost host)
        {
            ShortGuid id = map.Map(CathodeJson.ToId(op["entity"]));
            JObject spec = (JObject)op["spec"];
            //Read every value first: a value that can't be made here leaves nothing half made
            List<(ShortGuid name, ParameterData value)> parameters = new List<(ShortGuid, ParameterData)>();
            foreach (JProperty p in ((JObject)op["params"]).Properties())
                parameters.Add((CathodeJson.ToName(p.Name), CathodeJson.ToValue(p.Value, names, map)));
            List<ResourceReference> resources = op["resources"] == null ? null : CathodeJson.ToReferences(op["resources"], names, map);

            Entity entity;
            switch ((string)spec["kind"])
            {
                case "function":
                    {
                        FunctionType type = (FunctionType)Enum.Parse(typeof(FunctionType), (string)spec["function"]);
                        entity = type == FunctionType.CAGEAnimation ? new CAGEAnimation(id) : type == FunctionType.TriggerSequence ? (Entity)new TriggerSequence(id) : new FunctionEntity(id, type);
                        break;
                    }
                case "instance":
                    {
                        ShortGuid placed = map.Map(CathodeJson.ToId(spec["composite"]));
                        if (commands.GetComposite(placed) == null) throw new RecipeException("the composite it places (" + (string)spec["composite"] + ") is not in this level");
                        entity = new FunctionEntity(id) { function = placed };
                        break;
                    }
                case "variable":
                    entity = new VariableEntity(id, CathodeJson.ToName(spec["name"]), (DataType)Enum.Parse(typeof(DataType), (string)spec["type"]));
                    break;
                case "alias":
                    entity = new AliasEntity(id, CathodeJson.ToPath(spec["path"], map));
                    break;
                case "proxy":
                    entity = new ProxyEntity(id, CathodeJson.ToPath(spec["path"], map), CathodeJson.ToName(spec["function"]));
                    break;
                default:
                    throw new RecipeException("can't make an entity of kind " + (string)spec["kind"]);
            }
            host.TouchContents(composite);
            foreach ((ShortGuid name, ParameterData value) in parameters)
                entity.AddParameter(name, value, Variant(commands, composite, entity, name), true, false);
            if (resources != null && entity is FunctionEntity function) function.resources = resources;
            if (op["special"] != null) ApplySpecial(entity, op["special"], map);
            switch (entity)
            {
                case FunctionEntity f: composite.AddFunction(f); break;
                case VariableEntity v: composite.AddVariable(v); break;
                case AliasEntity a: composite.AddAlias(a); break;
                case ProxyEntity x: composite.AddProxy(x); break;
            }
            if (op["brush"] != null)
            {
                string brush = (string)op["brush"];
                commands.Utils.SetBrush(composite.shortGUID, id, brush);
                host.AfterEach(reverted => { if (reverted) commands.Utils.RemoveBrush(composite.shortGUID, id); else commands.Utils.SetBrush(composite.shortGUID, id, brush); });
            }
            host.Made(composite, entity);
        }

        private static void ApplySpecial(Entity entity, JToken data, IdMap map)
        {
            switch (entity)
            {
                case CAGEAnimation a: CathodeJson.ApplyAnimation(a, data, map); break;
                case TriggerSequence t:
                    CathodeJson.ToSequence(data, map, out List<TriggerSequence.SequenceEntry> sequence, out List<TriggerSequence.MethodEntry> methods);
                    t.sequence = sequence; t.methods = methods;
                    break;
                case ProxyEntity p:
                    CathodeJson.ToSequence(data, map, out List<TriggerSequence.SequenceEntry> ps, out List<TriggerSequence.MethodEntry> pm);
                    p.sequence = ps; p.methods = pm;
                    break;
            }
        }

        private static void SetSpecial(Entity entity, JToken after, IdMap map, IRecipeHost host, Composite composite)
        {
            host.Touch(composite, entity);
            if (entity is CAGEAnimation animation)
            {
                //The transaction holds bindings and event tracks; the float tracks are kept here
                List<CAGEAnimation.FloatTrack> beforeFloats = animation.floatTracks;
                if (after == null || after.Type == JTokenType.Null) { animation.connections = new List<CAGEAnimation.Connection>(); animation.floatTracks = new List<CAGEAnimation.FloatTrack>(); animation.eventTracks = new List<CAGEAnimation.EventTrack>(); }
                else CathodeJson.ApplyAnimation(animation, after, map);
                List<CAGEAnimation.FloatTrack> afterFloats = animation.floatTracks;
                host.AfterEach(reverted => animation.floatTracks = reverted ? beforeFloats : afterFloats);
            }
            else if (after == null || after.Type == JTokenType.Null)
            {
                if (entity is TriggerSequence t) { t.sequence = new List<TriggerSequence.SequenceEntry>(); t.methods = new List<TriggerSequence.MethodEntry>(); }
                else if (entity is ProxyEntity p) { p.sequence = new List<TriggerSequence.SequenceEntry>(); p.methods = new List<TriggerSequence.MethodEntry>(); }
            }
            else ApplySpecial(entity, after, map);
            host.Changed(composite, false);
        }

        private static void SetSpec(Entity entity, JObject after, IdMap map, IRecipeHost host, Composite composite)
        {
            host.Touch(composite, entity);
            switch (entity)
            {
                case FunctionEntity f when (string)after["kind"] == "function":
                    f.function = new ShortGuid((uint)(FunctionType)Enum.Parse(typeof(FunctionType), (string)after["function"]));
                    break;
                case FunctionEntity f when (string)after["kind"] == "instance":
                    f.function = map.Map(CathodeJson.ToId(after["composite"]));
                    break;
                case AliasEntity a when (string)after["kind"] == "alias":
                    a.alias = new EntityPath(CathodeJson.ToPath(after["path"], map));
                    break;
                case ProxyEntity p when (string)after["kind"] == "proxy":
                    p.proxy = new EntityPath(CathodeJson.ToPath(after["path"], map));
                    p.function = CathodeJson.ToName(after["function"]);
                    break;
                case VariableEntity v when (string)after["kind"] == "variable":
                    {
                        ShortGuid beforeName = v.name, afterName = CathodeJson.ToName(after["name"]);
                        DataType beforeType = v.type, afterType = (DataType)Enum.Parse(typeof(DataType), (string)after["type"]);
                        v.name = afterName; v.type = afterType;
                        host.AfterEach(reverted => { v.name = reverted ? beforeName : afterName; v.type = reverted ? beforeType : afterType; });
                        break;
                    }
                default:
                    throw new RecipeException("it is a " + entity.variant + " here; the recipe makes it a " + (string)after["kind"]);
            }
            host.Changed(composite, true);
        }

        private static void ExecuteMaterial(JObject op, Level target, ResourceNames names, IRecipeHost host)
        {
            Materials.Material m = names.FindMaterial((string)op["material"]) ?? throw new RecipeException("no material " + (string)op["material"] + " here");
            string field = (string)op["field"], after = (string)op["after"];
            CultureInfo ic = CultureInfo.InvariantCulture;
            Action<bool> set;
            if (field.StartsWith("texture slot "))
            {
                int slot = int.Parse(field.Substring("texture slot ".Length), ic);
                if (slot >= m.TextureReferences.Count) throw new RecipeException("the material has no texture slot " + slot + " here");
                TexturePtr before = m.TextureReferences[slot];
                TexturePtr now;
                if (after == "none") now = null;
                else
                {
                    (Textures.TEX4 tex, TexturePtr.Source source) = FindTexture(target, after);
                    if (tex == null) throw new RecipeException("no texture " + after + " here (import it first)");
                    now = new TexturePtr() { Texture = tex, Location = source };
                }
                set = reverted => m.TextureReferences[slot] = reverted ? before : now;
            }
            else if (field.Contains(" constant "))
            {
                string[] parts = field.Split(' ');
                List<float> list = parts[0] == "engine" ? m.EngineConstants : parts[0] == "vertex" ? m.VertexShaderConstants : parts[0] == "pixel" ? m.PixelShaderConstants : parts[0] == "hull" ? m.HullShaderConstants : m.DomainShaderConstants;
                int index = int.Parse(parts[2], ic);
                if (list == null || index >= list.Count) throw new RecipeException("the material has no " + field + " here");
                float before = list[index], now = float.Parse(after, NumberStyles.Float, ic);
                set = reverted => list[index] = reverted ? before : now;
            }
            else
            {
                int now = int.Parse(after, ic);
                int before = field == "priority" ? m.Priority : field == "environment map slot" ? m.EnvironmentMapIndex : m.PhysicalMaterialIndex;
                set = reverted =>
                {
                    int v = reverted ? before : now;
                    if (field == "priority") m.Priority = v; else if (field == "environment map slot") m.EnvironmentMapIndex = v; else m.PhysicalMaterialIndex = v;
                };
            }
            set(false);
            host.MaterialChanged(m, "material " + (string)op["material"] + " " + field, set);
        }
        #endregion
    }
}
