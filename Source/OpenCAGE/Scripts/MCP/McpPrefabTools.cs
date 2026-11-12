using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using OpenCAGE.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>The prefab library (Insert Prefab): list the prefabs, place one. 'script' toolset.</summary>
    internal static class McpPrefabTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "list_prefabs",
                Title = "List prefabs",
                Toolset = "script",
                Description = "The prefab library (Create Entity > Insert Prefab): ready-made gameplay script built from retail patterns, each an OpenCAGE package (.ocp) in the Prefabs folder beside OpenCAGE - " +
                    "kill volume, safe despawn, ally that doesn't fail the level, respawning enemy spawner with loot, kill counter + objective, out-of-view spawn point, hunter - and any package the user exported there. " +
                    "Gives each one's name, what it does, its pins and the engine trap it avoids, whether the open level has it already, what can be set when placing it (character, box size), and the NPC archetypes the open level has. place_prefab places one.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("prefab", "Only this prefab (its name or key), in full.")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListPrefabs,
            };

            yield return new McpTool()
            {
                Name = "place_prefab",
                Title = "Place prefab",
                Toolset = "script",
                Description = "Place a prefab from the library (list_prefabs) in a composite, as Create Entity > Insert Prefab does: when the level does not have the prefab's composite yet, its package is imported first (not undoable; written by save_level), then an instance is made as one undo step, " +
                    "with its position, input pin values ('pins', e.g. {\"respawn_delay\": 20, \"goal\": 5}), and for a prefab with a box (Kill Volume) the box's size for this placement only ('box_half_dimensions', an alias override as retail uses). " +
                    "For a prefab with a character (Enemy Spawner, Ally), 'character' swaps its NPC for another NPC archetype the level has (a spawn_npc kind such as 'android', or an archetype path): a copy of the prefab is made for that archetype once (its own undo step) and reused. " +
                    "Then link its pins with add_links (e.g. an Enemy Spawner's killed -> a Kill Counter's add_kill; a Hunter's character -> the spawner's npc_reference). save_level with build=true makes it playable. dry_run reports what would happen.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("prefab", "The prefab: its name or key (list_prefabs), or the full path of any .ocp.", required: true),
                    McpSchema.String("composite", "The composite to place it in (path or id; 'root' for the level's root; default the root)."),
                    McpSchema.Position("position", "Where, relative to the composite (for the root, world space). Default [0, 0, 0]"),
                    McpSchema.Rotation("rotation", "Its rotation, relative to the composite. Default [0, 0, 0]"),
                    McpSchema.String("name", "The instance's name (default: the prefab composite's name, numbered)."),
                    McpSchema.Map("pins", "Values for its input pins, by pin name (list_prefabs lists them)."),
                    McpSchema.String("character", "For a prefab with a character: the NPC archetype to use - a spawn_npc kind ('android', 'security', 'human', 'riot', ...) or an archetype path the level has."),
                    McpSchema.Vector("box_half_dimensions", "For a prefab with a box: this placement's half sizes [x, y, z] in metres (the box stands on its position: it reaches up 2 x y)."),
                    McpSchema.String("page", "The flowgraph page its node goes on (default: the page last shown)."),
                    McpSchema.Boolean("bring_character", "When the level lacks the NPC archetype a character prefab is packaged with (packages leave the game's own characters out), bring it in from a level of this install that has it - as port_composites would, not undoable, it loads that level - before placing (default true; false refuses instead)."),
                    McpSchema.Boolean("show", "Take the editor to the new instance and select it (default true)."),
                    McpSchema.Boolean("dry_run", "Say what would be imported and made, changing nothing.")),
                Destructive = true,
                Run = PlacePrefab,
            };
        }

        private static object ListPrefabs(McpCall call)
        {
            List<string> problems = new List<string>();
            List<Prefab> prefabs = PrefabLibrary.All(problems);
            if (call.Has("prefab"))
            {
                Prefab one = PrefabLibrary.Find(call.Str("prefab")) ?? throw McpError.NotFound("prefab", call.Str("prefab"), prefabs.Select(o => o.Name), "list_prefabs with no 'prefab' lists them.");
                prefabs = new List<Prefab>() { one };
            }
            return McpEditor.UI(() =>
            {
                Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
                JObject result = new JObject()
                {
                    ["folder"] = PrefabLibrary.Folder,
                    ["prefabs"] = new JArray(prefabs.Select(o => Describe(o, commands, call.Has("prefab")))),
                };
                if (commands != null)
                    result["npc_archetypes_in_level"] = new JArray(PrefabPlacer.NpcArchetypes(commands).Select(o => o.name));
                if (problems.Count != 0)
                    result["unreadable"] = new JArray(problems);
                if (prefabs.Count == 0)
                    call.Note("No prefabs found in " + PrefabLibrary.Folder + ".");
                return result;
            });
        }

        private static JObject Describe(Prefab prefab, Commands commands, bool full)
        {
            string[] paragraphs = PrefabLibrary.Paragraphs(prefab);
            JObject item = new JObject()
            {
                ["name"] = prefab.Name,
                ["key"] = prefab.Key,
                ["category"] = prefab.Category,
                ["built_in"] = prefab.BuiltIn,
                ["composite"] = prefab.RootName,
                ["description"] = full ? string.Join("\n", paragraphs) : (paragraphs.FirstOrDefault() ?? ""),
            };
            if (prefab.Catalog?.CharacterEntity != null) item["character_slot"] = prefab.Catalog.CharacterEntity;
            if (prefab.Catalog?.BoxEntity != null) item["box_slot"] = prefab.Catalog.BoxEntity;
            if (prefab.Catalog?.Hint != null) item["after_placing"] = prefab.Catalog.Hint;
            Composite inLevel = commands == null ? null : PrefabPlacer.InLevel(prefab, commands);
            if (commands != null)
                item["in_open_level"] = inLevel != null;
            if (inLevel != null)
            {
                item["pins"] = new JObject(PrefabPlacer.Pins(commands, inLevel).Select(o => new JProperty(o.Name, o.Kind)));
                Composite packaged = PrefabPlacer.PackagedCharacter(prefab, commands);
                if (packaged != null) item["character"] = packaged.name;
            }
            else if (full)
                item["pins"] = "listed once the prefab is in the level (place_prefab imports it); the description names them";
            return item;
        }

        private static object PlacePrefab(McpCall call)
        {
            Prefab prefab = PrefabLibrary.Find(call.Str("prefab", required: true));
            if (prefab == null)
                throw McpError.NotFound("prefab", call.Str("prefab"), PrefabLibrary.All().Select(o => o.Name), "list_prefabs lists the library.");
            Vector3 position = call.Has("position") ? McpValues.ReadVector(call.Token("position"), "position", null) : Vector3.Zero;
            Vector3 rotation = call.Has("rotation") ? McpValues.ReadVector(call.Token("rotation"), "rotation", null) : Vector3.Zero;
            Vector3? box = call.Has("box_half_dimensions") ? McpValues.ReadVector(call.Token("box_half_dimensions"), "box_half_dimensions", null) : (Vector3?)null;
            if (box != null && prefab.Catalog?.BoxEntity == null)
                throw McpError.Invalid(prefab.Name + " has no box to size.");
            if (call.Has("character") && prefab.Catalog?.CharacterEntity == null)
                throw McpError.Invalid(prefab.Name + " has no character to change.");

            using (McpEditorTools.Heartbeat(call, "Placing " + prefab.Name))
            {
                return McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel();
                    McpEditor.RequireUndoIdle();
                    Commands commands = content.Level.Commands;
                    Composite into = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : commands.EntryPoints[0];
                    Composite character = null;
                    if (call.Has("character"))
                        character = FindArchetype(commands, call.Str("character"));

                    if (call.Bool("dry_run"))
                    {
                        Composite have = PrefabPlacer.InLevel(prefab, commands);
                        JObject plan = new JObject()
                        {
                            ["dry_run"] = true,
                            ["prefab"] = prefab.Name,
                            ["would_import"] = have == null ? (JToken)prefab.RootName : false,
                            ["into"] = into.name,
                        };
                        if (PrefabPlacer.MissingCharacter(prefab, commands) is string missing) plan["would_bring_character"] = missing + " from " + (PrefabPlacer.CharacterSource(prefab) ?? "(no level of this install has it)");
                        if (character != null)
                        {
                            string variant = PrefabPlacer.VariantName(have?.name ?? prefab.RootName, character);
                            plan["character"] = character.name;
                            plan["copy"] = variant + (commands.GetComposite(variant) != null ? " (already made: reused)" : " (would be made)");
                        }
                        return plan;
                    }

                    PrefabPlaced placed = PrefabPlacer.Place(content, new PrefabPlacement()
                    {
                        Prefab = prefab,
                        Into = into,
                        Position = position,
                        Rotation = rotation,
                        Name = call.Str("name"),
                        Character = character,
                        BoxHalfDimensions = box,
                        Pins = call.Has("pins") ? call.Object("pins") : null,
                        Page = call.Str("page"),
                        LabelPrefix = "AI: ",
                        Show = !call.Has("show") || call.Bool("show"),
                        BringCharacter = !call.Has("bring_character") || call.Bool("bring_character"),
                    });
                    foreach (string note in placed.Notes) call.Note(note);
                    JObject result = new JObject()
                    {
                        ["prefab"] = prefab.Name,
                        ["composite"] = into.name,
                        ["instance"] = McpScript.Brief(commands, into, placed.Instance),
                        ["places"] = placed.Composite.name,
                        ["imported_composites"] = placed.Imported,
                        ["pins"] = new JObject(PrefabPlacer.Pins(commands, placed.Composite).Select(o => new JProperty(o.Name, o.Kind))),
                    };
                    if (placed.CharacterImported != 0) result["character_brought_in"] = placed.CharacterImported;
                    if (placed.VariantMade != null) result["copy_made"] = placed.VariantMade.name;
                    if (placed.BoxAlias != null) result["box_override"] = McpScript.Brief(commands, into, placed.BoxAlias);
                    if (prefab.Catalog?.Hint != null) result["next"] = prefab.Catalog.Hint;
                    return result;
                });
            }
        }

        /// <summary>An NPC archetype the level has, by spawn_npc kind, path, id or name.</summary>
        private static Composite FindArchetype(Commands commands, string wanted)
        {
            List<Composite> present = PrefabPlacer.NpcArchetypes(commands);
            string kindPath = KindPath(wanted);
            Composite found = present.FirstOrDefault(o => kindPath != null && string.Equals(McpScript.NormalisePath(o.name), McpScript.NormalisePath(kindPath), StringComparison.OrdinalIgnoreCase))
                ?? present.FirstOrDefault(o => string.Equals(McpScript.NormalisePath(o.name), McpScript.NormalisePath(wanted), StringComparison.OrdinalIgnoreCase))
                ?? present.FirstOrDefault(o => string.Equals(McpScript.CompositeLeaf(o), wanted.Trim(), StringComparison.OrdinalIgnoreCase));
            if (found != null)
                return found;
            throw McpError.NotFound("NPC archetype", wanted, present.Select(o => o.name),
                "This level's NPC archetypes: " + (present.Count == 0 ? "none" : string.Join(", ", present.Select(o => o.name))) + ". port_composites brings one in from a level that has it (spawn_npc's error names one).");
        }

        //spawn_npc's kinds, for the retail archetypes they name
        private static string KindPath(string kind)
        {
            switch ((kind ?? "").Trim().ToLowerInvariant())
            {
                case "android": return @"Archetypes\NPCs\Android\Android_NPC";
                case "android_heavy": return @"Archetypes\NPCs\Android\Android_Heavy_NPC";
                case "human": return @"Archetypes\NPCs\Human\Civilian\Worker_NPC_Pistol";
                case "human_shotgun": return @"Archetypes\NPCs\Human\Civilian\Worker_NPC_Shotgun";
                case "human_unarmed": return @"Archetypes\NPCs\Human\Civilian\Worker_NPC";
                case "security": return @"Archetypes\NPCs\Human\Security\Grunt_NPC_Pistol";
                case "security_shotgun": return @"Archetypes\NPCs\Human\Security\Grunt_NPC_Shotgun";
                case "riot": return @"Archetypes\NPCs\Human\Riot\Riot_NPC_Pistol";
                case "riot_shotgun": return @"Archetypes\NPCs\Human\Riot\Riot_NPC_Shotgun";
                case "alien": return @"Archetypes\NPCs\Alien\Xenomorph_NPC";
                case "facehugger": return @"Archetypes\NPCs\Alien\Facehugger";
                default: return null;
            }
        }
    }
}
