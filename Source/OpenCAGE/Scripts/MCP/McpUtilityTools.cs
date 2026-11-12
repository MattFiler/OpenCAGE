using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Utility tools: add_rotation (a prop, light or instance turning continuously), key_physics_bodies (physics props that work
    /// spawned), find_spare_config_records (who uses each ammo type / attribute set, and which are spare).
    /// Registered from McpLightTools.Tools(); each names its own toolset.
    /// </summary>
    internal static class McpUtilityTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "add_rotation",
                Title = "Rotate an entity continuously",
                Description = "Turn a prop (ModelReference), a light (LightReference) or a composite instance about one of its OWN axes, at degrees per second, for as long as the level runs: fans, rotors, beacons, turning gobo lights - no keyframes. " +
                    "Retail's wiring (a looping RotateAtSpeed: start_pos / origin / Result linked to the entity's reference; a light also on_think -> refresh; started by the composite's ThinkOnce 'Spin_Start'), as one undo step. " +
                    "axis 'auto' picks the model's thinnest side (a fan disc), a light's Z (the way a spot shines: a gobo turns in place), else Y. Calling it again on the same entity changes its speed or axis; remove: true stops it. " +
                    "Refused where it could not move: inside a composite another CAGEAnimation keys an instance of (animations in a keyed instance stay at time 0). Without 'entity', lists the composite's turning entities. Reaches the game after save_level build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding it (path or id).", required: true),
                    McpSchema.String("entity", "The entity to turn (id or name). Leave out to list what turns in the composite."),
                    McpSchema.String("axis", "x, y or z (its own axes), or auto (default).", options: new[] { "auto", "x", "y", "z" }),
                    McpSchema.Number("degrees_per_second", "Speed; negative turns the other way (default 90: a turn every 4 s)."),
                    McpSchema.Boolean("remove", "Stop it turning (its RotateAtSpeed goes)."),
                    McpSchema.Boolean("dry_run", "Say what would be made, changing nothing.")),
                Destructive = true,
                Toolset = "lights",
                Run = AddRotation,
            };

            yield return new McpTool()
            {
                Name = "key_physics_bodies",
                Title = "Key physics props by body name",
                Description = "Physics props that work when an AssetSpawner spawns them: the game binds each body of a spawned physics system to its model and collision through the hash of the body's name, so the ModelReference it moves must have its resource keyed ShortGuid(body name), as retail's all are (OpenCAGE-made ones were keyed by entity id: 'Unable to create physics system'). " +
                    "Lists, for one composite or the whole level, every physics body with its ModelReference and state (keyed, not_keyed, draws_nothing, no_model) and which composites an AssetSpawner spawns; apply: true keys the not_keyed ones (one undo step). import_physics_system keys new props itself.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Only this composite (path or id). Default: every composite holding a PhysicsSystem."),
                    McpSchema.Boolean("apply", "Key the ModelReferences that aren't (default false: only report)."),
                    McpSchema.Boolean("include_keyed", "List keyed bodies too (default: only the rest, counted)."),
                    McpSchema.Limit(100, "bodies"),
                    McpSchema.Offset("bodies")),
                Destructive = true,
                Toolset = "assets",
                Run = KeyPhysicsBodies,
            };

            yield return new McpTool()
            {
                Name = "spawn_physics_prop",
                Title = "Make a physics prop spawned (retail's way)",
                Description = "A DYNAMIC physics prop placed directly in a level stays an inactive, sleeping body where it was put, whatever pushes it: retail places every one of its dynamic props through an AssetSpawner instead (AYZ\\_Props_\\Dynamic\\X: spawn_on_load, spawn_on_reset, allow_physics; its asset an instance of the prop marked is_template). " +
                    "This turns a placed instance of a physics prop (a composite holding a PhysicsSystem - make_physics_prop makes them) into that: its bodies keyed by name (key_physics_bodies), a wrapper composite '<prop path>_Spawned' made once per prop and reused, and the placement replaced by an instance of the wrapper with the same name and transform. One undo step; reaches the game after save_level build=true. key_physics_bodies lists placed props that aren't spawned (placed_not_spawned).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the placed instance (path or id).", required: true),
                    McpSchema.String("entity", "The placed instance of the physics prop (id or name).", required: true)),
                Destructive = true,
                Toolset = "assets",
                Run = call =>
                {
                    JObject done = PhysicsPropSpawner.MakeSpawned(call, call.Str("composite", required: true), call.Str("entity", required: true));
                    call.Note("save_level with build=true puts it in the game: it is spawned at load and falls, settles and can be pushed.");
                    return done;
                },
            };

            yield return new McpTool()
            {
                Name = "find_spare_config_records",
                Title = "Find spare config records (ammo, attributes)",
                Description = "Before a GLOBAL config edit: who uses each record of a game table, and which records nothing uses. kind 'ammo' = WEAPON_INFO\\AMMO\\AMMOTYPES.BML (the engine's fixed AMMO_TYPE enum: no new record can be added, so a new weapon's ammo takes a spare one); 'attributes' = CHR_INFO\\ATTRIBUTES (Character.attribute_set). " +
                    "Uses: records built on it (Template_Name: a change reaches them where they don't set the value), GBL_ITEM ammo items, and the scripts of the levels read (AMMO_TYPE values on any entity, attribute_set names; a composite pin's default counts only where an instance relies on it). " +
                    "Levels are read from disk once (all of them take a minute or two) and kept; the open level as it is now. The game's code can still use a record no data names: check a spare one in game. read_config / write_config change the files (globally).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("kind", "ammo or attributes.", required: true, options: new[] { "ammo", "attributes" }),
                    McpSchema.Any("levels", "'all' (default: every level on disk), 'retail' (the game's own), 'open', or a list of levels."),
                    McpSchema.String("record", "Only this record, with all its uses."),
                    McpSchema.Boolean("spare_only", "Only the spare records."),
                    McpSchema.Integer("max_uses", "At most this many uses listed per record (default 8).")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "gamedata",
                Run = FindSpareConfigRecords,
            };
        }

        #region add_rotation
        private static object AddRotation(McpCall call)
        {
            string compositeArg = call.Str("composite", required: true);
            string entityArg = call.Str("entity");
            if (entityArg == null)
            {
                return McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireLevel(forEditing: false).Level.Commands;
                    Composite composite = McpScript.FindComposite(commands, compositeArg);
                    JArray list = new JArray(Spinners.All(composite).Select(o => Describe(commands, o)));
                    return new JObject() { ["composite"] = composite.name, ["turning"] = list, ["count"] = list.Count };
                });
            }
            string axisText = (call.Str("axis") ?? "auto").Trim().ToLowerInvariant();
            if (axisText != "auto" && axisText != "x" && axisText != "y" && axisText != "z")
                throw McpError.Invalid("'axis' is x, y, z or auto.");
            double speed = call.Num("degrees_per_second", 90);
            if (Math.Abs(speed) > 36000) throw McpError.Invalid("'degrees_per_second' takes -36000 to 36000.");
            bool remove = call.Bool("remove");
            bool dryRun = call.Bool("dry_run");

            JObject result = new JObject();
            Composite focus = null;
            Entity target = null;
            Spinners.Axis axis = Spinners.Axis.Y;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireLevel().Level.Commands;
                focus = McpScript.FindComposite(commands, compositeArg);
                target = McpScript.FindEntity(commands, focus, entityArg);
                result["composite"] = focus.name;
                result["entity"] = McpScript.Brief(commands, focus, target);
                if (remove) return;
                if (!Spinners.CanSpin(target, out string why)) throw McpError.Invalid(why);
                Spinners.Axis suggested = Spinners.SuggestAxis(target, out string because);
                axis = axisText == "auto" ? suggested : axisText == "x" ? Spinners.Axis.X : axisText == "y" ? Spinners.Axis.Y : Spinners.Axis.Z;
                result["suggested_axis"] = new JObject() { ["axis"] = suggested.ToString().ToLowerInvariant(), ["why"] = because };
                string frozen = Spinners.WhyFrozen(commands, focus);
                if (frozen != null) throw new McpError(McpErrorCodes.Refused, frozen);
            });

            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(remove ? "Stop turning" : "Rotate continuously", focus, edit =>
            {
                if (remove)
                {
                    if (!Spinners.Remove(edit, focus, target))
                        throw new McpError("It isn't turning (no RotateAtSpeed in " + focus.name + " drives its reference).");
                    result["removed"] = true;
                    return;
                }
                Spinners.Spinner spinner = Spinners.Apply(edit, focus, target, axis, (float)speed);
                result["rotation"] = Describe(edit.Commands, spinner);
            }, new McpScriptEdit.RunOptions() { Show = false, DryRun = dryRun });
            if (dryRun) result["dry_run"] = true;
            foreach (string note in outcome.Notes) call.Note(note);
            if (!dryRun) call.Note("save_level with build=true puts it in the game.");
            return result;
        }

        private static JObject Describe(Commands commands, Spinners.Spinner spinner)
        {
            return new JObject()
            {
                ["target"] = McpScript.Brief(commands, spinner.Composite, spinner.Target),
                ["rotate_at_speed"] = McpScript.Brief(commands, spinner.Composite, spinner.RotateAtSpeed),
                ["axis"] = spinner.Axis.ToString().ToLowerInvariant(),
                ["degrees_per_second"] = spinner.DegreesPerSecond,
                ["period"] = Spinners.Period(spinner.DegreesPerSecond),
            };
        }
        #endregion

        #region key_physics_bodies
        private static object KeyPhysicsBodies(McpCall call)
        {
            bool apply = call.Bool("apply");
            bool includeKeyed = call.Bool("include_keyed");
            List<PhysicsBodyKeys.Body> bodies = null;
            HashSet<Composite> spawned = null;
            List<JObject> placedDynamic = null;
            Composite only = null;
            McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: apply).Level;
                if (level.Physics == null || !level.Physics.Loaded) throw new McpError("This level has no physics packfile loaded.");
                only = call.Has("composite") ? McpScript.FindComposite(level.Commands, call.Str("composite")) : null;
                bodies = only != null ? PhysicsBodyKeys.Inspect(level, only) : PhysicsBodyKeys.InspectLevel(level);
                spawned = new HashSet<Composite>(PhysicsBodyKeys.SpawnedAssets(level.Commands).Select(o => o.spawned));
                //Only the user's own (made, brought in or changed with OpenCAGE): retail places a few debris props directly, and leaves them so
                HashSet<ShortGuid> users = new HashSet<ShortGuid>((level.Commands.Utils?.AllModificationInfo ?? new List<CompositeModificationInfoTable.ModificationInfo>()).Where(o => o != null).Select(o => o.composite_id));
                placedDynamic = PhysicsPropSpawner.PlacedDynamic(level).Where(o => (users.Contains(o.holder.shortGUID) || users.Contains(o.prop.shortGUID)) && !PhysicsBodyKeys.IsRetailException(o.prop))
                    .Select(o => new JObject() { ["composite"] = o.holder.name, ["instance"] = McpScript.Brief(level.Commands, o.holder, o.instance), ["prop"] = o.prop.name }).ToList();
            });

            JObject result = new JObject();
            if (apply)
            {
                List<Composite> composites = bodies.Where(o => o.State == PhysicsBodyKeys.State.NotKeyed && !PhysicsBodyKeys.IsRetailException(o.Composite)).Select(o => o.Composite).Distinct().ToList();
                if (composites.Count != 0)
                {
                    int keyed = 0;
                    McpScriptEdit.Run("Key physics bodies by name (" + composites.Count + " composite" + (composites.Count == 1 ? "" : "s") + ")", composites[0], edit =>
                    {
                        foreach (Composite composite in composites)
                            keyed += PhysicsBodyKeys.Key(edit, bodies.Where(o => o.Composite == composite)).Count;
                    }, new McpScriptEdit.RunOptions() { Show = false, AlsoTouches = composites.Skip(1) });
                    result["keyed"] = keyed;
                    call.Note("save_level with build=true puts the keys in the game.");
                }
                else
                    result["keyed"] = 0;
                McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: false).Level;
                    bodies = only != null ? PhysicsBodyKeys.Inspect(level, only) : PhysicsBodyKeys.InspectLevel(level);
                });
            }

            if (placedDynamic.Count != 0)
            {
                result["placed_not_spawned"] = new JArray(placedDynamic.Take(50));
                call.Note("placed_not_spawned: dynamic physics props placed directly stay asleep where they are put - spawn_physics_prop makes each one spawned, as retail's are.");
            }
            result["bodies_total"] = bodies.Count;
            result["by_state"] = new JObject(bodies.GroupBy(o => o.State).Select(g => new JProperty(StateName(g.Key), g.Count())));
            List<PhysicsBodyKeys.Body> listed = bodies.Where(o => includeKeyed || o.State != PhysicsBodyKeys.State.Keyed).ToList();
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireLevel(forEditing: false).Level.Commands;
                McpPaging.Page(call, listed, result, "bodies", o => new JObject()
                {
                    ["composite"] = o.Composite.name,
                    ["system"] = o.SystemIndex,
                    ["body"] = o.BodyName,
                    ["state"] = StateName(o.State),
                    ["model_reference"] = o.Model == null ? null : McpScript.Brief(commands, o.Composite, o.Model),
                    ["spawned_by_asset_spawner"] = spawned.Contains(o.Composite),
                    ["retail_exception"] = PhysicsBodyKeys.IsRetailException(o.Composite) ? (JToken)true : null,
                    ["about"] = PhysicsBodyKeys.Describe(o),
                }, 100);
            });
            return result;
        }

        private static string StateName(PhysicsBodyKeys.State state)
        {
            switch (state)
            {
                case PhysicsBodyKeys.State.Keyed: return "keyed";
                case PhysicsBodyKeys.State.NotKeyed: return "not_keyed";
                case PhysicsBodyKeys.State.DrawsNothing: return "draws_nothing";
                default: return "no_model";
            }
        }
        #endregion

        #region find_spare_config_records
        private static string NormaliseLevel(string level) => (level ?? "").Replace('\\', '/').Trim().Trim('/').ToUpperInvariant();

        private static object FindSpareConfigRecords(McpCall call)
        {
            ConfigRecords.Kind kind = ConfigRecords.KindOf(call.Str("kind", required: true)) ?? throw McpError.Invalid("'kind' is ammo or attributes.");
            string root = Singleton.PathToAI;
            List<string> all = Level.GetLevels(root).Select(NormaliseLevel).Distinct().OrderBy(o => o).ToList();
            string openLevel = null;
            ConfigRecords.LevelUses openUses = null;
            McpEditor.UI(() =>
            {
                LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
                if (content?.Level?.Commands != null)
                {
                    openLevel = NormaliseLevel(content.Level.Name);
                    openUses = ConfigRecords.Scan(content.Level.Commands);
                }
            });

            List<string> levels;
            JToken levelsArg = call.Token("levels");
            if (levelsArg == null || (levelsArg.Type == JTokenType.String && string.Equals((string)levelsArg, "all", StringComparison.OrdinalIgnoreCase)))
                levels = all;
            else if (levelsArg.Type == JTokenType.String && string.Equals((string)levelsArg, "retail", StringComparison.OrdinalIgnoreCase))
                levels = all.Where(o => o.StartsWith("PRODUCTION/", StringComparison.OrdinalIgnoreCase)).ToList();
            else if (levelsArg.Type == JTokenType.String && string.Equals((string)levelsArg, "open", StringComparison.OrdinalIgnoreCase))
                levels = openLevel == null ? new List<string>() : new List<string>() { openLevel };
            else
            {
                levels = new List<string>();
                foreach (string text in levelsArg is JArray array ? array.Select(o => (string)o) : new[] { (string)levelsArg })
                {
                    string level = NormaliseLevel(text);
                    if (!all.Contains(level))
                    {
                        List<string> ending = all.Where(o => o.EndsWith("/" + level)).ToList();
                        if (ending.Count == 1) level = ending[0];
                        else throw McpError.NotFound("level", text, all, "list_levels lists them; or 'all', 'retail', 'open'.");
                    }
                    levels.Add(level);
                }
            }

            ConfigRecords.Report report = ConfigRecords.Find(root, kind, levels.Where(o => !string.Equals(o, openLevel, StringComparison.OrdinalIgnoreCase)), null, null, message => call.Progress(message), call.Cancel);
            if (openUses != null && levels.Contains(openLevel))
            {
                ConfigRecords.AddLevel(report, openLevel, openUses);
                report.LevelsRead.Add(openLevel);
            }

            string onlyRecord = call.Str("record");
            bool spareOnly = call.Bool("spare_only");
            int maxUses = Math.Max(0, Math.Min(200, call.Int("max_uses", 8)));
            JArray records = new JArray();
            foreach (ConfigRecords.Record record in report.Records)
            {
                if (onlyRecord != null && !string.Equals(record.Name, onlyRecord, StringComparison.OrdinalIgnoreCase)) continue;
                bool spare = report.IsSpare(record);
                if (spareOnly && !spare) continue;
                List<ConfigRecords.Use> uses = report.UsesOf(record.Name).Where(u => u.Where != "template").ToList();
                JObject o = new JObject()
                {
                    ["record"] = record.Name,
                    ["file"] = "DATA\\" + record.File,
                    ["spare"] = spare,
                    ["uses"] = uses.Count(u => u.Effective),
                };
                if (kind == ConfigRecords.Kind.Ammo) o["ammo_type"] = record.Id;
                if (record.Template != null) o["built_on"] = record.Template;
                if (record.InheritedBy.Count != 0) o["built_on_it"] = new JArray(record.InheritedBy);
                if (uses.Count != 0)
                {
                    o["levels"] = new JArray(uses.Where(u => u.Effective && u.Where != "GBL_ITEM").Select(u => u.Where).Distinct());
                    o["used_by"] = new JArray(uses.Take(onlyRecord != null ? 200 : maxUses).Select(u => u.ToString()));
                    if (uses.Count > (onlyRecord != null ? 200 : maxUses)) o["used_by_not_listed"] = uses.Count - (onlyRecord != null ? 200 : maxUses);
                    int unreached = uses.Count(u => !u.Effective);
                    if (unreached != 0) o["reaches_nothing"] = unreached + " (a composite pin default every instance overrides)";
                }
                records.Add(o);
            }
            if (onlyRecord != null && records.Count == 0)
                throw McpError.NotFound("record", onlyRecord, report.Records.Select(r => r.Name).ToList(), "The table's records.");
            JObject result = new JObject()
            {
                ["kind"] = kind == ConfigRecords.Kind.Ammo ? "ammo" : "attributes",
                ["table"] = kind == ConfigRecords.Kind.Ammo ? "DATA\\WEAPON_INFO\\AMMO\\AMMOTYPES.BML" : "DATA\\CHR_INFO\\ATTRIBUTES\\ATTRIBUTES.BML",
                ["global"] = "Every level of the game reads these records (and every mod's levels): a change to one changes the base game, and every record built on it.",
                ["spare"] = new JArray(report.Spare.Select(o => o.Name)),
                ["records"] = records,
                ["levels_read"] = report.LevelsRead.Count,
            };
            if (report.LevelsFailed.Count != 0) result["levels_failed"] = new JArray(report.LevelsFailed);
            call.Note(ConfigRecords.EngineNote(kind));
            return result;
        }
        #endregion
    }
}
