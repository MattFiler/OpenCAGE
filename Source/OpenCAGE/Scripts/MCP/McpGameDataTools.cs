using CATHODE;
using CATHODE.Enums;
using Newtonsoft.Json.Linq;
using OpenCAGE.ConfigEditors;
using OpenCAGE.Popups;
using OpenCAGE.Popups.Configuration_Editors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// The game's own data files the configuration editors (View > Configuration Editors) and Revert Configs edit:
    /// config records, localised strings, which text databases a level loads, and putting configs back to vanilla.
    /// These are shared by every level and written straight away, so every writer has a dry_run and refuses while
    /// the game is running unless told to close it.
    /// </summary>
    internal static class McpGameDataTools
    {
        private static readonly string[] Kinds =
        {
            "alien_config", "ammo", "attributes", "difficulty", "viewcone_set", "global_constants",
            "radiosity", "hair_shading", "skin_shading", "interaction_glow",
            "inventory_item", "movie_playlist", "blueprint_recipe",
            "permanent_soundbanks", "physical_materials", "character_asset_set", "voice_mappings",
        };

        private static McpSchema.Prop DryRun() => McpSchema.Boolean("dry_run", "Check everything and report what would change, writing nothing.");
        private static McpSchema.Prop CloseGame() => McpSchema.Boolean("close_game", "If Alien: Isolation is running, close it first (as Revert Configs does). Without this the call is refused while it runs.");

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "get_config_record",
                Title = "Read game config record",
                Description = "Read a game config record as the configuration editors show it. alien_config, ammo, attributes (a character's attributes, senses, locomotion), difficulty and viewcone_set follow Template_Name: each effective field is listed with the template it comes from. Also settings files, the interaction glow, inventory items, playlists, recipes, lists, voice mappings. No name lists the records.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("kind", "What to read.", required: true, options: Kinds),
                    McpSchema.String("name", "The record (e.g. 'INTENSE', 'SECURITY_GUARD', 'pistol', 'ASSETSET_01', 'CharacterClass=CIVILIAN/Gender=MALE'). Leave out to list them."),
                    McpSchema.String("filter", "Only fields whose path contains this text."),
                    McpSchema.Integer("limit", "At most this many fields or records (default 400).")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() => GetRecord(call)),
            };

            yield return new McpTool()
            {
                Name = "set_config_record",
                Title = "Change game config record",
                Description = "Change fields of a game config record (kinds and paths as get_config_record shows them) by the editors' rules: an inherited field becomes an override in the record's own file (inherit[] drops one again); inventory items keep special slots and ammo targets in step. Written to the game's files at once, for every level; not undoable - try dry_run first, and reset_configs restores vanilla.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("kind", "What to change.", required: true, options: Kinds),
                    McpSchema.String("name", "The record (see get_config_record). Not needed for the single-file kinds."),
                    McpSchema.Map("values", "{path or setting: value}, e.g. {'AreaSweep/max_menaces': 3}. List kinds take whole lists (banks, clips, inputs, outputs, decals, voices...)."),
                    McpSchema.Strings("inherit", "Template-inheriting kinds: field paths whose override to remove from this record, so the template's value applies again."),
                    McpSchema.Boolean("create", "inventory_item: make a new item called name (item_type needed); voice_mappings: make the attribute set if missing."),
                    McpSchema.String("item_type", "inventory_item with create: the new item's type.", options: InventoryItemEditor.CreatableTypes),
                    DryRun(),
                    CloseGame()),
                Destructive = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() => SetRecord(call)),
            };

            yield return new McpTool()
            {
                Name = "list_strings",
                Title = "List localised strings",
                Description = "Search the game's localised text: the shared databases in DATA/TEXT, or a level's own (level). Gives id, database and text in one language; with id, gives that string in all nine languages. These ids are what STRING_OBJECTIVES / STRING_TERMINAL / STRING_UI parameters name. list_text_databases shows which databases a level loads.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "A level's own databases (as list_levels names it) instead of the shared DATA/TEXT ones."),
                    McpSchema.String("database", "Only this database (e.g. 'UI', 'T0001')."),
                    McpSchema.String("id", "One string id: return it in every language."),
                    McpSchema.String("filter", "Text the id or the text contains."),
                    McpSchema.String("language", "Language to list in (default ENGLISH).", options: LocalisationHandler.LanguageFolders),
                    McpSchema.Integer("limit", "At most this many strings (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() => ListStrings(call)),
            };

            yield return new McpTool()
            {
                Name = "set_strings",
                Title = "Set localised strings",
                Description = "Change or add strings in one text database (shared DATA/TEXT, or a level's own with level), as the Localisation editor does: text sets every language, texts sets named ones. New ids need create_missing. Written to the game's files at once (a shared database is used by every level); not undoable - previous values are returned. Loaded strings in the editor refresh.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("database", "The database (e.g. 'UI', or one made with set_text_databases).", required: true),
                    McpSchema.String("level", "The level whose own database this is (leave out for the shared DATA/TEXT ones)."),
                    McpSchema.Array("strings", "The strings to write.", McpSchema.Object(
                        McpSchema.String("id", "The string id (no [ or ]).", required: true),
                        McpSchema.String("text", "Text for every language."),
                        McpSchema.Map("texts", "{LANGUAGE: text} for particular languages (CZECH, ENGLISH, FRENCH, GERMAN, ITALIAN, POLISH, PORTUGUESE, RUSSIAN, SPANISH).")), required: true),
                    McpSchema.Boolean("create_missing", "Add ids (or languages) the database does not have yet."),
                    DryRun(),
                    CloseGame()),
                Destructive = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() => SetStrings(call)),
            };

            yield return new McpTool()
            {
                Name = "list_text_databases",
                Title = "List level text databases",
                Description = "Which text databases a level loads: the shared DATA/TEXT ones its block and the 'globals' block of LEVEL_TEXT_DATABASES.XML name, and its own TEXT folder's databases (listed in TEXT_DB_LIST.TXT or not). Also what DATA/TEXT offers. level 'globals' shows only the block every level gets.",
                InputSchema = McpSchema.Object(McpSchema.String("level", "The level (as list_levels names it) or 'globals'; default the open level.")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() => ListTextDatabases(call)),
            };

            yield return new McpTool()
            {
                Name = "set_text_databases",
                Title = "Set level text databases",
                Description = "Choose which text databases a level loads, as the Level Text DBs editor does: shared ones in its LEVEL_TEXT_DATABASES.XML block ('globals' = every level), its own in TEXT_DB_LIST.TXT, and create_local makes a new empty database of its own in all nine languages. Written at once, not undoable. Reload the level to see a changed list in the editor.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level (as list_levels names it) or 'globals'.", required: true),
                    McpSchema.Strings("add_shared", "Shared DATA/TEXT databases to load."),
                    McpSchema.Strings("remove_shared", "Shared databases to stop loading."),
                    McpSchema.Strings("add_local", "The level's own databases to list in TEXT_DB_LIST.TXT."),
                    McpSchema.Strings("remove_local", "The level's own databases to unlist (their files stay)."),
                    McpSchema.Strings("create_local", "New databases to create in the level's TEXT folder and list."),
                    McpSchema.Boolean("allow_shadowing", "Allow create_local to use a name DATA/TEXT already has (the level's copy then replaces the shared one for this level)."),
                    DryRun(),
                    CloseGame()),
                Destructive = true,
                Run = call => McpEditor.UI(() => SetTextDatabases(call)),
            };

            yield return new McpTool()
            {
                Name = "edit_config_elements",
                Title = "Edit config file elements",
                Description = "Structural edits to a game config file (BML/XML under DATA as read_config reads it, or CHR_INFO/CUSTOMCHARACTERVOICETYPEMAPPINGS.BIN): add, duplicate or remove elements, set or remove attributes, set text - e.g. input bindings, font slots, graphics presets. Keeps indentation; FONT_CONFIG edits reach every variant. All ops or none; written at once, not undoable (dry_run previews).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("file", "Path under DATA, e.g. 'INPUT.XML'.", required: true),
                    McpSchema.Array("ops", "Applied in order.", McpSchema.Object(
                        McpSchema.String("op", "What to do.", required: true, options: new[] { "set", "remove_attribute", "add", "duplicate", "remove" }),
                        McpSchema.String("xpath", "The node(s) it applies to (for add, where the new element goes).", required: true),
                        McpSchema.String("attribute", "set / remove_attribute: the attribute (set without it sets the text)."),
                        McpSchema.String("value", "set: the new value."),
                        McpSchema.String("element", "add: the new element's name."),
                        McpSchema.Map("attributes", "add / duplicate: attributes to give the new element."),
                        McpSchema.String("text", "add: the new element's text."),
                        McpSchema.String("position", "add: as the last child of the xpath node (default), or before/after it.", options: new[] { "child", "before", "after" }),
                        McpSchema.Integer("expect", "Refuse unless the xpath selects exactly this many nodes (needed when it selects more than one).")), required: true),
                    DryRun(),
                    CloseGame()),
                Destructive = true,
                Run = call => McpEditor.UI(() => EditElements(call)),
            };

            yield return new McpTool()
            {
                Name = "reset_configs",
                Title = "Reset configs to vanilla",
                Description = "Put the game's shipped config files back to vanilla from OpenCAGE's backups, as Revert Configs does, by group or file. With neither, lists the groups and which files differ from vanilla. Overwrites the files at once (every level) and closes open config editors (refused while one that only saves on its Save button is open); 'behaviour_trees' (or its file) also deletes and regenerates DATA/BEHAVIOR. Not undoable.",
                InputSchema = McpSchema.Object(
                    McpSchema.Strings("groups", "Groups to reset (leave out groups and files to list them)."),
                    McpSchema.Strings("files", "Single files to reset, as paths under DATA (e.g. 'GBL_ITEM.BML'). A group's file is reset as its group resets it (an optional one only if the install has it)."),
                    DryRun(),
                    CloseGame()),
                Destructive = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() => ResetConfigsTool(call)),
            };
        }

        #region Shared
        private static string DataFolder => Path.GetFullPath(Path.Combine(Singleton.PathToAI, "DATA"));

        /// <summary>A path under DATA ('/' separated) as a full path.</summary>
        private static string DataPath(string relative) => Path.Combine(DataFolder, relative.Replace('/', '\\').TrimStart('\\'));

        private static string Relative(string relative) => relative.Replace('\\', '/').Trim().TrimStart('/');

        /// <summary>The game reads these files; writing them under it is refused unless the caller lets us close it (the way Revert Configs does).</summary>
        private static void GuardGame(McpCall call, bool dryRun, List<string> extraProcesses = null)
        {
            List<string> running = new List<string>();
            foreach (string name in new[] { "AI" }.Concat(extraProcesses ?? new List<string>()))
            {
                System.Diagnostics.Process[] found = System.Diagnostics.Process.GetProcessesByName(name);
                if (found.Length != 0) running.Add(name + ".exe");
                foreach (System.Diagnostics.Process process in found) process.Dispose();
            }
            if (running.Count == 0)
                return;
            if (dryRun)
            {
                call.Note(string.Join(", ", running) + " is running: doing this for real needs close_game:true (or close it first).");
                return;
            }
            if (!call.Bool("close_game"))
                throw new McpError(string.Join(", ", running) + " is running and reads these files. Close it, or pass close_game:true to have OpenCAGE close it (progress in it is lost). Nothing was changed.");
            EditorUtils.CloseAI(extraProcesses);
            call.Note("Closed " + string.Join(", ", running) + " first.");
        }

        /// <summary>The configuration editor windows that hold their own copy of a file (under DATA) and save it back on their next change.</summary>
        private static Type[] EditorsFor(string relative)
        {
            string file = Relative(relative).ToUpperInvariant();
            if (file == "GBL_ITEM.BML") return new[] { typeof(HackingEditor), typeof(LoadMovieEditor), typeof(BlueprintEditor), typeof(InventoryItemEditor) };
            if (file.StartsWith("ALIENCONFIGS/")) return new[] { typeof(AlienConfigEditor) };
            if (file.StartsWith("DIFFICULTYSETTINGS/")) return new[] { typeof(DifficultyEditor) };
            if (file.StartsWith("CHR_INFO/ATTRIBUTES/")) return new[] { typeof(AttributesEditor), typeof(LocomotionEditor), typeof(SenseEditor) };
            if (file.StartsWith("WEAPON_INFO/AMMO/")) return new[] { typeof(AmmoEditor) };
            if (file.StartsWith("VIEW_CONE_SETS/")) return new[] { typeof(ViewconeEditor) };
            if (file == "GLOBALCONSTANTS.BML" || file == "UI/SELECTIONOVERLAYPARAMS.BIN") return new[] { typeof(GlobalConstantsEditor) };
            if (file == "RADIOSITY_SETTINGS.TXT") return new[] { typeof(RadiosityEditor) };
            if (file == "HAIR_SHADING_SETTINGS.TXT" || file == "SKIN_SHADING_SETTINGS.TXT") return new[] { typeof(HairAndSkinShadingEditor) };
            if (file == "LIST_OF_PERMANENT_SOUND_BANKS.TXT") return new[] { typeof(PermanentSoundbankEditor) };
            if (file == "MATERIAL_DATA/MATERIALS.BML") return new[] { typeof(PhysicalMaterialEditor) };
            if (file == "CHR_INFO/CUSTOMCHARACTERASSETDATA.BIN") return new[] { typeof(CharacterAssetEditor) };
            if (file == "CHR_INFO/CUSTOMCHARACTERVOICETYPEMAPPINGS.BIN") return new[] { typeof(VoiceMappingEditor) };
            if (file == "INPUT.XML") return new[] { typeof(InputsEditor) };
            if (file.StartsWith("FONT_CONFIG")) return new[] { typeof(FontConfigEditor) };
            if (file == "ENGINE_SETTINGS.XML") return new[] { typeof(GraphicsEditor) };
            if (file == "LEVEL_TEXT_DATABASES.XML") return new[] { typeof(LevelTextDBEditor) };
            return new Type[0];
        }

        //These only write on their Save button, so closing one would throw away edits made in it
        private static readonly Type[] SaveButtonEditors = { typeof(LoadMovieEditor), typeof(BlueprintEditor), typeof(CharacterAssetEditor), typeof(GraphicsEditor) };

        /// <summary>
        /// Everything checked, before writing these files (under DATA): false for a dry run, which only notes what
        /// would happen. The game must not be running (see <see cref="GuardGame"/>). An open configuration editor
        /// over a file keeps its own copy and saves it back on its next change, which would undo ours, so it is
        /// closed (they autosave, so nothing of theirs is lost) - except one that only writes on its Save button,
        /// which may hold unsaved edits and is left for the user to close.
        /// </summary>
        internal static bool BeginWrite(McpCall call, bool dryRun, IEnumerable<string> relativeFiles, List<string> extraProcesses = null)
        {
            HashSet<Type> types = new HashSet<Type>(relativeFiles.SelectMany(EditorsFor));
            List<Form> open = Application.OpenForms.Cast<Form>().Where(o => !o.IsDisposed && types.Contains(o.GetType())).ToList();
            foreach (Form form in open.Where(o => SaveButtonEditors.Contains(o.GetType())))
            {
                string message = "OpenCAGE's '" + form.Text + "' window is open and holds its own copy of this file, with any edits not yet saved there. Close it (saving it if it has changes worth keeping), then try again.";
                if (!dryRun) throw new McpError(message + " Nothing was changed.");
                call.Note(message);
            }
            GuardGame(call, dryRun, extraProcesses);
            foreach (Form form in open.Where(o => !SaveButtonEditors.Contains(o.GetType())))
            {
                if (dryRun)
                {
                    call.Note("OpenCAGE's '" + form.Text + "' window would be closed: it keeps its own copy of the file and would save it over this change.");
                    continue;
                }
                form.Close();
                call.Note("Closed OpenCAGE's '" + form.Text + "' window: it keeps its own copy of the file and would have saved it over this change.");
            }
            return !dryRun;
        }

        private static XmlDocument LoadBml(string relative, out BML bml)
        {
            string full = DataPath(relative);
            if (!File.Exists(full))
                throw new McpError("There is no DATA/" + relative + " in the install.");
            bml = new BML(full);
            XmlDocument document = bml.Loaded ? bml.Content : null;
            if (document?.DocumentElement == null)
                throw new McpError("Could not read DATA/" + relative + ".");
            return document;
        }

        private static void SaveBml(string relative, BML bml, XmlDocument document)
        {
            Modding.ModServices.CaptureBeforeWrite(DataPath(relative));
            //Content hands out a copy each time: the edited document has to be given back
            bml.Content = document;
            if (!bml.Save())
                throw new McpError("Could not write DATA/" + relative + " (is it read-only, or open in another program?).");
        }

        private static string TokenText(JToken token, string what)
        {
            if (token == null || token.Type == JTokenType.Null)
                throw new McpError(what + " needs a value.");
            switch (token.Type)
            {
                case JTokenType.String: return ((string)token).Trim();
                case JTokenType.Boolean: return (bool)token ? "true" : "false";
                case JTokenType.Integer:
                case JTokenType.Float: return token.ToString(Newtonsoft.Json.Formatting.None);
                default: throw new McpError(what + " must be a single value, not " + token.Type.ToString().ToLowerInvariant() + ".");
            }
        }

        private static bool IsNumber(string text) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        private static bool IsBoolText(string text) => string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase);

        private static bool ParseBool(string text, string what)
        {
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || text == "1") return true;
            if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) || text == "0") return false;
            throw new McpError(what + " must be true or false, not '" + text + "'.");
        }

        private static string Number(JToken token, string what)
        {
            string text = TokenText(token, what);
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value))
                throw new McpError(what + " must be a number, not '" + text + "'.");
            return text;
        }

        /// <summary>A value written into a field, checked against what the field holds now: numbers stay numbers, booleans keep the file's spelling, choices are one of the choices.</summary>
        private static string FieldValue(JToken token, string current, IList<string> choices, string what)
        {
            string text = TokenText(token, what);
            if (choices != null && choices.Count != 0)
            {
                string match = choices.FirstOrDefault(o => string.Equals(o, text, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    throw new McpError(what + " must be one of: " + string.Join(", ", choices.Take(60)) + (choices.Count > 60 ? ", ..." : "") + ".");
                return match;
            }
            if (current != null && IsBoolText(current))
            {
                bool value = ParseBool(text, what);
                //The files spell booleans both ways ("True" in the BMLs, "true" in GBL_ITEM): keep the file's
                string spelt = value ? "true" : "false";
                return char.IsUpper(current[0]) ? char.ToUpperInvariant(spelt[0]) + spelt.Substring(1) : spelt;
            }
            if (current != null && current.Length != 0 && IsNumber(current))
                return Number(token, what);
            return text;
        }

        private static List<string> StringList(JToken token, string what)
        {
            if (token == null || token.Type == JTokenType.Null)
                return new List<string>();
            if (token.Type == JTokenType.String)
                return new List<string>() { ((string)token).Trim() };
            if (!(token is JArray array))
                throw new McpError(what + " must be a list of names.");
            return array.Select(o => TokenText(o, what)).ToList();
        }

        private static JObject ValuesArgument(McpCall call, bool required = true)
        {
            JObject values = call.Object("values", required);
            if (required && values.Count == 0)
                throw new McpError("'values' is empty: say what to change.");
            return values ?? new JObject();
        }

        private static void RefuseArguments(McpCall call, string kind, params string[] names)
        {
            foreach (string name in names)
                if (call.Has(name))
                    throw new McpError("'" + name + "' does not apply to kind " + kind + ".");
        }

        private static string Suggest(IEnumerable<string> known, string wanted, int max = 8)
        {
            string leaf = wanted.Split('/').Last().TrimStart('@');
            List<string> close = known.Where(o => o.IndexOf(leaf, StringComparison.OrdinalIgnoreCase) >= 0 || leaf.IndexOf(o.Split('/').Last().TrimStart('@'), StringComparison.OrdinalIgnoreCase) >= 0).Take(max).ToList();
            return close.Count == 0 ? "" : " Did you mean: " + string.Join(", ", close) + "?";
        }

        /// <summary>The one match (found ignoring case), or null for none; more than one is refused, naming them, as picking one would be a guess.</summary>
        private static T OnlyMatch<T>(IEnumerable<T> matches, Func<T, string> name, string wanted, string hint) where T : class
        {
            List<T> found = matches.ToList();
            if (found.Count > 1)
                throw new McpError("'" + wanted + "' matches " + string.Join(", ", found.Select(o => "'" + name(o) + "'")) + ": " + hint);
            return found.FirstOrDefault();
        }

        private static string ResolveLevel(string level)
        {
            string wanted = (level ?? "").Replace('\\', '/').Trim().Trim('/');
            if (wanted.Length == 0)
                throw new McpError("Say which level.");
            List<string> levels = EditorUtils.GetEditableLevels();
            string match = levels.FirstOrDefault(o => string.Equals(o.Replace('\\', '/'), wanted, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match.Replace('\\', '/');
            string leaf = wanted.Split('/').Last();
            List<string> byLeaf = levels.Where(o => string.Equals(o.Replace('\\', '/').Split('/').Last(), leaf, StringComparison.OrdinalIgnoreCase)).ToList();
            if (byLeaf.Count == 1)
                return byLeaf[0].Replace('\\', '/');
            if (byLeaf.Count > 1)
                throw new McpError("'" + level + "' could be: " + string.Join(", ", byLeaf) + ". Give the full name.");
            throw new McpError("There is no level '" + level + "' (list_levels shows them).");
        }

        private static string OpenLevelName() => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name?.Replace('\\', '/');
        #endregion

        #region Records
        private static object GetRecord(McpCall call)
        {
            string kind = call.Str("kind", required: true);
            string name = call.Str("name");
            int limit = Math.Max(1, call.Int("limit", 400));
            XmlKind xmlKind = XmlKinds.FirstOrDefault(o => o.Key == kind);
            if (xmlKind != null)
                return GetXmlRecord(call, xmlKind, name, limit);
            if (name != null && (SettingsFiles.ContainsKey(kind) || kind == "interaction_glow" || kind == "permanent_soundbanks" || kind == "physical_materials"))
                throw new McpError(kind + " is one file: leave out 'name'.");
            switch (kind)
            {
                case "radiosity":
                case "hair_shading":
                case "skin_shading":
                    return GetSettingsFile(SettingsFiles[kind], call.Str("filter"));
                case "interaction_glow": return GetGlow();
                case "inventory_item": return GetInventoryItem(name, limit);
                case "movie_playlist": return GetPlaylist(name, limit);
                case "blueprint_recipe": return GetRecipe(name);
                case "permanent_soundbanks": return GetSoundbanks();
                case "physical_materials": return GetPhysicalMaterials(limit);
                case "character_asset_set": return GetAssetSet(name);
                case "voice_mappings": return GetVoiceMappings(name, limit);
            }
            throw new McpError("There is no kind '" + kind + "'. Kinds: " + string.Join(", ", Kinds) + ".");
        }

        private static object SetRecord(McpCall call)
        {
            string kind = call.Str("kind", required: true);
            string name = call.Str("name");
            bool dryRun = call.Bool("dry_run");
            if (kind != "inventory_item")
                RefuseArguments(call, kind, "item_type");
            if (kind != "inventory_item" && kind != "voice_mappings")
                RefuseArguments(call, kind, "create");
            XmlKind xmlKind = XmlKinds.FirstOrDefault(o => o.Key == kind);
            if (xmlKind != null)
                return SetXmlRecord(call, xmlKind, name, dryRun);
            RefuseArguments(call, kind, "inherit");
            switch (kind)
            {
                case "radiosity":
                case "hair_shading":
                case "skin_shading":
                    return SetSettingsFile(call, SettingsFiles[kind], dryRun);
                case "interaction_glow": return SetGlow(call, dryRun);
                case "inventory_item": return SetInventoryItem(call, name, dryRun);
                case "movie_playlist": return SetPlaylist(call, name, dryRun);
                case "blueprint_recipe": return SetRecipe(call, name, dryRun);
                case "permanent_soundbanks": return SetSoundbanks(call, dryRun);
                case "physical_materials": return SetPhysicalMaterials(call, dryRun);
                case "character_asset_set": return SetAssetSet(call, name, dryRun);
                case "voice_mappings": return SetVoiceMappings(call, name, dryRun);
            }
            throw new McpError("There is no kind '" + kind + "'. Kinds: " + string.Join(", ", Kinds) + ".");
        }
        #endregion

        #region Records: XML with Template_Name inheritance
        private sealed class XmlKind
        {
            public string Key;
            public string Folder;       //where each record's file is, under DATA
            public string ListFile;     //the file naming the records
            public string SingleFile;   //or: the one file that is the record
            public string Root;
        }

        private static readonly XmlKind[] XmlKinds =
        {
            new XmlKind() { Key = "alien_config", Folder = "ALIENCONFIGS", ListFile = "ALIENCONFIGS/ALIENCONFIGS.BML", Root = "AlienConfig" },
            new XmlKind() { Key = "ammo", Folder = "WEAPON_INFO/AMMO", ListFile = "WEAPON_INFO/AMMO/AMMOTYPES.BML", Root = "Ammo" },
            new XmlKind() { Key = "attributes", Folder = "CHR_INFO/ATTRIBUTES", ListFile = "CHR_INFO/ATTRIBUTES/ATTRIBUTES.BML", Root = "Attribute" },
            new XmlKind() { Key = "difficulty", Folder = "DIFFICULTYSETTINGS", ListFile = "DIFFICULTYSETTINGS/DIFFICULTYSETTINGS.BML", Root = "DifficultySetting" },
            new XmlKind() { Key = "viewcone_set", Folder = "VIEW_CONE_SETS", ListFile = "VIEW_CONE_SETS/VIEWCONESETS.BML", Root = "ViewconeSet" },
            new XmlKind() { Key = "global_constants", SingleFile = "GLOBALCONSTANTS.BML", Root = "GlobalConstants" },
        };

        //The viewcone sets the difficulty editor offers: the game skips any other in a difficulty's ViewconeSets
        private static readonly string[] DifficultyViewconeSets = { "VIEWCONESET_STANDARD", "VIEWCONESET_HUMAN", "VIEWCONESET_SLEEPING", "VIEWCONESET_ANDROID" };

        private sealed class ChainFile
        {
            public string Name;
            public string Relative;
            public BML Bml;
            public XmlDocument Document;
            public XmlElement Root;
        }

        private sealed class Field
        {
            public string Path;
            public string Value;
            public int From;
        }

        private static List<string> RecordNames(XmlKind kind)
        {
            if (kind.SingleFile != null)
                return new List<string>();
            XmlDocument list = LoadBml(kind.ListFile, out _);
            return list.DocumentElement.ChildNodes.OfType<XmlElement>().Select(o => o["Name"]?.InnerText).Where(o => !string.IsNullOrEmpty(o)).ToList();
        }

        /// <summary>The record's file and every template it inherits from, nearest first - the order the editors read values in.</summary>
        private static List<ChainFile> LoadChain(McpCall call, XmlKind kind, string name)
        {
            List<ChainFile> chain = new List<ChainFile>();
            if (kind.SingleFile != null)
            {
                XmlDocument single = LoadBml(kind.SingleFile, out BML singleBml);
                chain.Add(new ChainFile() { Name = Path.GetFileNameWithoutExtension(kind.SingleFile), Relative = kind.SingleFile, Bml = singleBml, Document = single, Root = single[kind.Root] ?? single.DocumentElement });
                return chain;
            }

            List<string> names = RecordNames(kind);
            string canonical = names.FirstOrDefault(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase));
            if (canonical == null)
                throw new McpError("There is no " + kind.Key + " '" + name + "'. There are: " + string.Join(", ", names) + ".");

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string next = canonical;
            while (!string.IsNullOrEmpty(next))
            {
                if (!seen.Add(next))
                {
                    call?.Note("The Template_Name chain of " + canonical + " loops back to " + next + "; it was cut there.");
                    break;
                }
                if (next.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || next.Contains(".."))
                    break;
                string relative = kind.Folder + "/" + next + ".BML";
                if (!File.Exists(DataPath(relative)))
                {
                    if (chain.Count == 0)
                        throw new McpError(kind.Key + " '" + canonical + "' is listed, but DATA/" + relative + " does not exist.");
                    call?.Note(chain.Last().Name + " names template " + next + ", but DATA/" + relative + " does not exist.");
                    break;
                }
                XmlDocument document = LoadBml(relative, out BML bml);
                XmlElement root = document[kind.Root] ?? document.DocumentElement;
                chain.Add(new ChainFile() { Name = next, Relative = relative, Bml = bml, Document = document, Root = root });
                next = root["Template_Name"]?.InnerText?.Trim();
            }
            return chain;
        }

        private static string Join(string prefix, string segment) => prefix.Length == 0 ? segment : prefix + "/" + segment;

        private static string StripIndexes(string path) => Regex.Replace(path, @"\[\d+\]", "");

        private sealed class Walked
        {
            public List<KeyValuePair<string, string>> Leaves = new List<KeyValuePair<string, string>>();
            public HashSet<string> Containers = new HashSet<string>();
            public HashSet<string> Elements = new HashSet<string>();   //paths without indexes
            public HashSet<string> Repeated = new HashSet<string>();   //elements that repeat under their parent
        }

        /// <summary>Every value a file holds, by path: element text, and attributes as .../@name. Repeated elements are numbered [n].</summary>
        private static void Walk(XmlElement element, string prefix, Walked into)
        {
            List<XmlElement> children = element.ChildNodes.OfType<XmlElement>().ToList();
            Dictionary<string, int> counts = children.GroupBy(o => o.Name).ToDictionary(o => o.Key, o => o.Count());
            Dictionary<string, int> seen = new Dictionary<string, int>();
            foreach (XmlElement child in children)
            {
                if (prefix.Length == 0 && child.Name == "Template_Name")
                    continue;
                seen.TryGetValue(child.Name, out int index);
                seen[child.Name] = ++index;
                string path = Join(prefix, counts[child.Name] > 1 ? child.Name + "[" + index + "]" : child.Name);
                string stripped = StripIndexes(path);
                into.Elements.Add(stripped);
                if (counts[child.Name] > 1) into.Repeated.Add(stripped);
                foreach (XmlAttribute attribute in child.Attributes)
                    into.Leaves.Add(new KeyValuePair<string, string>(path + "/@" + attribute.Name, attribute.Value));
                if (child.ChildNodes.OfType<XmlElement>().Any())
                {
                    into.Containers.Add(path);
                    Walk(child, path, into);
                }
                else if (child.Attributes.Count == 0 || child.InnerText.Length != 0)
                    into.Leaves.Add(new KeyValuePair<string, string>(path, child.InnerText));
            }
        }

        /// <summary>
        /// The record's effective values: each path from the nearest file in the chain that has it, which is how
        /// the editors resolve a field. A list (repeated elements) comes whole from the nearest file that has one.
        /// </summary>
        private static List<Field> Effective(List<ChainFile> chain)
        {
            List<Walked> walked = chain.Select(o => { Walked w = new Walked(); Walk(o.Root, "", w); return w; }).ToList();
            HashSet<string> containers = new HashSet<string>(walked.SelectMany(o => o.Containers));
            Dictionary<string, Field> byPath = new Dictionary<string, Field>();
            List<Field> fields = new List<Field>();
            HashSet<string> claimed = new HashSet<string>();
            HashSet<string> claimedRepeated = new HashSet<string>();
            for (int i = 0; i < chain.Count; i++)
            {
                foreach (KeyValuePair<string, string> leaf in walked[i].Leaves)
                {
                    if (containers.Contains(leaf.Key) || byPath.ContainsKey(leaf.Key))
                        continue;
                    if (i != 0 && InClaimedList(leaf.Key, walked[i], claimed, claimedRepeated))
                        continue;
                    Field field = new Field() { Path = leaf.Key, Value = leaf.Value, From = i };
                    byPath.Add(leaf.Key, field);
                    fields.Add(field);
                }
                claimed.UnionWith(walked[i].Elements);
                claimedRepeated.UnionWith(walked[i].Repeated);
            }
            return fields;
        }

        private static bool InClaimedList(string path, Walked file, HashSet<string> claimed, HashSet<string> claimedRepeated)
        {
            string[] segments = StripIndexes(path).Split('/');
            string prefix = "";
            foreach (string segment in segments)
            {
                if (segment.StartsWith("@")) break;
                prefix = Join(prefix, segment);
                if ((file.Repeated.Contains(prefix) || claimedRepeated.Contains(prefix)) && claimed.Contains(prefix))
                    return true;
            }
            return false;
        }

        /// <summary>The values a field may take, where the editors offer a fixed list.</summary>
        private static string[] Choices(XmlKind kind, string path, Dictionary<string, string[]> cache)
        {
            string leaf = path.Split('/').Last();
            string key = null;
            if (kind.Key == "attributes")
            {
                if (leaf == "Behavior_Tree") key = "Behavior_Tree";
                else if (leaf == "ATTACK_GROUP") key = "ATTACK_GROUP";
                else if (leaf == "TargetingSystem") key = "TargetingSystem";
                else if (leaf == "Character_Sound") key = "Character_Sound";
                else if (leaf.StartsWith("viewcone_set", StringComparison.OrdinalIgnoreCase)) key = "viewcone_set";
            }
            else if (kind.Key == "ammo" && (leaf == "@Damage_1" || leaf == "@Damage_2" || leaf == "@Damage_3"))
                key = "Damage";
            if (key == null)
                return null;
            if (cache.TryGetValue(key, out string[] cached))
                return cached;
            string[] choices;
            switch (key)
            {
                case "Behavior_Tree":
                    {
                        XmlDocument trees = File.Exists(DataPath("BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML")) ? LoadBml("BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML", out _) : null;
                        choices = trees?["DIR"]?.ChildNodes.OfType<XmlElement>().Where(o => o.Name == "File").Select(o => Path.GetFileNameWithoutExtension(o.GetAttribute("name"))).ToArray() ?? new string[0];
                        break;
                    }
                case "ATTACK_GROUP": choices = new[] { "AT_NONE", "AT_FHUGGER", "AT_ANDROID", "AT_HUMAN", "AT_ALIEN" }; break;
                case "TargetingSystem": choices = new[] { "TM_NONE", "TM_ALIEN", "TM_FACEHUGGER", "TM_ANDROID", "TM_HUMAN", "TM_PLAYER" }; break;
                case "Character_Sound": choices = new[] { "PLAYER1", "PLAYER2", "ALIEN", "FACEHUGGER", "ANDROID", "CIVILIAN", "SECURITY_GUARD" }; break;
                case "viewcone_set": choices = RecordNames(XmlKinds.First(o => o.Key == "viewcone_set")).ToArray(); break;
                case "Damage": choices = Enum.GetValues(typeof(DAMAGE_EFFECT_TYPE_FLAGS)).Cast<DAMAGE_EFFECT_TYPE_FLAGS>().Where(o => (int)o != -1).Select(o => o.ToString()).ToArray(); break;
                default: choices = null; break;
            }
            cache[key] = choices;
            return choices;
        }

        private static object GetXmlRecord(McpCall call, XmlKind kind, string name, int limit)
        {
            if (kind.SingleFile == null && string.IsNullOrWhiteSpace(name))
            {
                List<string> names = RecordNames(kind);
                return new JObject()
                {
                    ["kind"] = kind.Key,
                    ["count"] = names.Count,
                    ["records"] = new JArray(names.Take(limit).Select(o =>
                    {
                        JObject record = new JObject() { ["name"] = o };
                        string relative = kind.Folder + "/" + o + ".BML";
                        if (File.Exists(DataPath(relative)))
                        {
                            try
                            {
                                string template = LoadBml(relative, out _).DocumentElement?["Template_Name"]?.InnerText;
                                if (!string.IsNullOrEmpty(template)) record["template"] = template;
                            }
                            catch (McpError) { record["unreadable"] = true; }
                        }
                        else
                            record["missing_file"] = true;
                        return record;
                    })),
                };
            }
            if (kind.SingleFile != null && name != null)
                throw new McpError(kind.Key + " is one file: leave out 'name'.");

            List<ChainFile> chain = LoadChain(call, kind, name);
            List<Field> fields = Effective(chain);
            string filter = call.Str("filter");
            List<Field> shown = fields.Where(o => filter == null || o.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Dictionary<string, string[]> cache = new Dictionary<string, string[]>();
            JObject choices = new JObject();
            foreach (Field field in shown.Take(limit))
            {
                string[] options = Choices(kind, field.Path, cache);
                string leaf = field.Path.Split('/').Last();
                if (options != null && choices[leaf] == null)
                    choices[leaf] = new JArray(options);
            }
            JObject result = new JObject()
            {
                ["kind"] = kind.Key,
                ["name"] = chain[0].Name,
                ["file"] = chain[0].Relative,
                ["inherits"] = new JArray(chain.Skip(1).Select(o => o.Name)),
                ["count"] = shown.Count,
                ["fields"] = new JArray(shown.Take(limit).Select(o =>
                {
                    JObject entry = new JObject() { ["path"] = o.Path, ["value"] = o.Value };
                    if (o.From != 0) entry["from"] = chain[o.From].Name;
                    return entry;
                })),
            };
            if (choices.Count != 0) result["choices"] = choices;
            if (shown.Count > limit) call.Note("Showing " + limit + " of " + shown.Count + " fields: narrow with filter, or raise limit.");
            return result;
        }

        private static readonly Regex SimplePath = new Regex(@"^[A-Za-z_][\w.\-]*(/[A-Za-z_][\w.\-]*)*$");
        private static readonly Regex SimpleAttributePath = new Regex(@"^([A-Za-z_][\w.\-]*(/[A-Za-z_][\w.\-]*)*)/@([A-Za-z_][\w.\-]*)$");

        private static XmlNodeList Select(XmlElement root, string path)
        {
            try { return root.SelectNodes(path); }
            catch (Exception e) { throw new McpError("'" + path + "' is not a path this can follow: " + e.Message); }
        }

        private static object SetXmlRecord(McpCall call, XmlKind kind, string name, bool dryRun)
        {
            if (kind.SingleFile == null && string.IsNullOrWhiteSpace(name))
                throw new McpError("Say which " + kind.Key + " ('name'); get_config_record with no name lists them.");
            if (kind.SingleFile != null && name != null)
                throw new McpError(kind.Key + " is one file: leave out 'name'.");
            JObject values = call.Object("values") ?? new JObject();
            List<string> inherit = call.StrList("inherit");
            if (values.Count == 0 && inherit.Count == 0)
                throw new McpError("Nothing to change: give 'values' and/or 'inherit'.");

            List<ChainFile> chain = LoadChain(call, kind, name);
            ChainFile target = chain[0];
            List<Field> fields = Effective(chain);
            Dictionary<string, Field> byPath = fields.ToDictionary(o => o.Path);
            Dictionary<string, string[]> cache = new Dictionary<string, string[]>();
            JArray changed = new JArray();

            foreach (JProperty property in values.Properties())
            {
                string path = NormaliseFieldPath(property.Name, kind);
                if (path == "Template_Name")
                    throw new McpError("Template_Name is not changed here: it decides where every inherited value comes from.");
                byPath.TryGetValue(path, out Field current);
                string what = "'" + path + "'";
                string[] choices = Choices(kind, path, cache);
                XmlNodeList nodes = Select(target.Root, path);
                if (nodes.Count > 1)
                    throw new McpError(what + " matches " + nodes.Count + " nodes in " + target.Name + "; number the repeated element meant, as get_config_record lists it (e.g. range_damage[2]).");
                JObject entry = new JObject() { ["path"] = path };
                if (nodes.Count == 1)
                {
                    XmlNode node = nodes[0];
                    string previous;
                    if (node is XmlAttribute attribute)
                    {
                        previous = attribute.Value;
                        attribute.Value = FieldValue(property.Value, previous, choices, what);
                        entry["value"] = attribute.Value;
                    }
                    else if (node is XmlElement element)
                    {
                        if (element.ChildNodes.OfType<XmlElement>().Any())
                            throw new McpError(what + " is a group of fields, not a field.");
                        previous = element.InnerText;
                        element.InnerText = FieldValue(property.Value, previous, choices, what);
                        entry["value"] = element.InnerText;
                    }
                    else
                        throw new McpError(what + " does not name an element or attribute.");
                    entry["previous"] = previous;
                    changed.Add(entry);
                    continue;
                }

                //Not in the record's own file: it may be inherited, in which case the record gets an override
                if (SimplePath.IsMatch(path))
                {
                    string inheritedFrom = current != null ? chain[current.From].Name : null;
                    if (current == null && !KeyedSibling(kind, path, byPath.Keys))
                        throw new McpError(what + " is not a field of " + target.Name + " or anything it inherits." + Suggest(byPath.Keys, path));
                    XmlElement created = ConfigEditorUtils.EnsureChildElements(target.Root, path.Split('/'));
                    created.InnerText = FieldValue(property.Value, current?.Value, choices, what);
                    entry["value"] = created.InnerText;
                    entry["previous"] = current?.Value;
                    entry["override_of"] = inheritedFrom ?? "(none - a new entry beside its siblings)";
                    changed.Add(entry);
                    continue;
                }
                Match attributePath = SimpleAttributePath.Match(path);
                if (attributePath.Success && current != null)
                {
                    XmlNodeList owners = Select(target.Root, attributePath.Groups[1].Value);
                    if (owners.Count == 1 && owners[0] is XmlElement owner)
                    {
                        string attributeName = attributePath.Groups[3].Value;
                        owner.SetAttribute(attributeName, FieldValue(property.Value, current.Value, choices, what));
                        entry["value"] = owner.GetAttribute(attributeName);
                        entry["previous"] = current.Value;
                        entry["override_of"] = chain[current.From].Name;
                        changed.Add(entry);
                        continue;
                    }
                }
                if (current != null)
                    throw new McpError(what + " comes from " + chain[current.From].Name + ", as part of a list or an element " + target.Name + " does not have; it is not overridden one value at a time. Change it on " + chain[current.From].Name + " (every record inheriting it changes), or edit " + target.Relative + " with edit_config_elements.");
                throw new McpError(what + " is not a field of " + target.Name + " or anything it inherits." + Suggest(byPath.Keys, path));
            }

            JArray dropped = new JArray();
            foreach (string raw in inherit)
            {
                string path = NormaliseFieldPath(raw, kind);
                if (!SimplePath.IsMatch(path))
                    throw new McpError("inherit takes plain element paths like 'AreaSweep/max_menaces', not '" + raw + "'.");
                XmlNodeList nodes = Select(target.Root, path);
                if (nodes.Count != 1 || !(nodes[0] is XmlElement own) || own.ChildNodes.OfType<XmlElement>().Any())
                    throw new McpError("'" + path + "' is not a value " + target.Name + " sets itself, so there is no override to drop.");
                ChainFile template = chain.Skip(1).FirstOrDefault(o => Select(o.Root, path).Count != 0);
                if (template == null)
                    throw new McpError("Nothing " + target.Name + " inherits from has '" + path + "', so dropping it would lose the value, not inherit one.");
                string previous = own.InnerText;
                XmlNode parent = own.ParentNode;
                if (own.PreviousSibling != null && own.PreviousSibling.NodeType == XmlNodeType.Whitespace)
                    parent.RemoveChild(own.PreviousSibling);
                parent.RemoveChild(own);
                dropped.Add(new JObject() { ["path"] = path, ["previous"] = previous, ["now_from"] = template.Name, ["value"] = Select(template.Root, path)[0].InnerText });
            }

            JObject result = new JObject()
            {
                ["kind"] = kind.Key,
                ["name"] = target.Name,
                ["file"] = target.Relative,
                ["changed"] = changed,
            };
            if (dropped.Count != 0) result["inherited_again"] = dropped;
            result["dry_run"] = dryRun;
            if (!BeginWrite(call, dryRun, new[] { target.Relative }))
                return result;
            SaveBml(target.Relative, target.Bml, target.Document);
            result["written"] = "DATA/" + target.Relative;
            return result;
        }

        private static string NormaliseFieldPath(string raw, XmlKind kind)
        {
            string path = (raw ?? "").Trim().Replace('\\', '/').Trim('/');
            if (path.StartsWith(kind.Root + "/"))
                path = path.Substring(kind.Root.Length + 1);
            if (path.Length == 0)
                throw new McpError("A field path is empty.");
            return path;
        }

        /// <summary>
        /// Difficulty settings hold a block per character (NPC_Generic/&lt;character&gt;) and per viewcone set
        /// (ViewconeSets/&lt;set&gt;), and a block missing for one is written the way the others hold it - as the
        /// difficulty editor does - so a path is allowed when a sibling block has the same field.
        /// </summary>
        private static bool KeyedSibling(XmlKind kind, string path, IEnumerable<string> known)
        {
            if (kind.Key != "difficulty")
                return false;
            string[] segments = path.Split('/');
            if (segments.Length < 3)
                return false;
            IEnumerable<string> keys;
            if (segments[0] == "NPC_Generic") keys = RecordNames(XmlKinds.First(o => o.Key == "attributes"));
            else if (segments[0] == "ViewconeSets") keys = DifficultyViewconeSets;
            else return false;
            if (!keys.Contains(segments[1]))
                throw new McpError("'" + segments[1] + "' is not one of " + segments[0] + "'s blocks: " + string.Join(", ", keys) + ".");
            string rest = string.Join("/", segments.Skip(2));
            return known.Any(o =>
            {
                string[] other = o.Split('/');
                return other.Length == segments.Length && other[0] == segments[0] && string.Join("/", other.Skip(2)) == rest;
            });
        }
        #endregion

        #region Records: settings TXT files
        private sealed class SettingsFile
        {
            public string File;
            public string[] Keys;   //the ones the editor has controls for
        }

        private static readonly Dictionary<string, SettingsFile> SettingsFiles = new Dictionary<string, SettingsFile>()
        {
            { "radiosity", new SettingsFile() { File = "RADIOSITY_SETTINGS.TXT", Keys = new[] {
                "gRadiosityEmissiveSurfaceScale", "gRadiosityFirstBounceScale", "gRadiosityMultiBounceScale", "gRadiosityAlbedoOverbrightAmount",
                "gRadiosityAlbedoSaturationAmount", "gRadiositySpecularGlossScale", "gDeferredEmissiveSurfaceScale", "gDeferredEmissiveSurfaceExponent" } } },
            { "skin_shading", new SettingsFile() { File = "SKIN_SHADING_SETTINGS.TXT", Keys = new[] { "scattering_radius", "scattering_saturation" } } },
            { "hair_shading", new SettingsFile() { File = "HAIR_SHADING_SETTINGS.TXT", Keys = new[] {
                "alpha_threshold", "alpha_threshold_shadow", "primary_spec_level", "secondary_spec_level", "primary_spec_width", "secondary_spec_width",
                "spec_separation", "diffuse_level", "base_absorption", "absorption_rate", "ao_absorption", "scatter_dist_rate", "occlusion_rate",
                "occlusion_bias", "occlusion_ao_infl", "specular_occlusion", "specular_ao", "softening_length", "softening_normal_bias", "softening_distance_rate" } } },
        };

        private static List<KeyValuePair<string, string>> ReadSettings(List<string> lines)
        {
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>();
            foreach (string line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq > 0)
                    values.Add(new KeyValuePair<string, string>(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim()));
            }
            return values;
        }

        private static object GetSettingsFile(SettingsFile settings, string filter)
        {
            string full = DataPath(settings.File);
            if (!File.Exists(full))
                throw new McpError("There is no DATA/" + settings.File + " (reset_configs can restore it).");
            JObject values = new JObject();
            foreach (KeyValuePair<string, string> pair in ReadSettings(File.ReadAllLines(full).ToList()))
            {
                if (filter != null && pair.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                values[pair.Key] = double.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? (JToken)number : pair.Value;
            }
            JArray missing = new JArray(settings.Keys.Where(k => values[k] == null && (filter == null || k.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)));
            JObject result = new JObject() { ["file"] = settings.File, ["values"] = values };
            if (missing.Count != 0) result["not_in_file"] = missing;
            return result;
        }

        private static object SetSettingsFile(McpCall call, SettingsFile settings, bool dryRun)
        {
            if (call.Has("name")) throw new McpError("This kind is one file: leave out 'name'.");
            JObject values = ValuesArgument(call);
            string full = DataPath(settings.File);
            List<string> lines = File.Exists(full) ? File.ReadAllLines(full).ToList() : new List<string>();
            HashSet<string> known = new HashSet<string>(ReadSettings(lines).Select(o => o.Key).Concat(settings.Keys));
            JArray changed = new JArray();
            foreach (JProperty property in values.Properties())
            {
                string key = property.Name.Trim();
                if (key == "settings_file_version")
                    throw new McpError("settings_file_version is the file format's, not a setting.");
                if (!known.Contains(key))
                    throw new McpError("'" + key + "' is not a setting of " + settings.File + ". Settings: " + string.Join(", ", known.Where(o => o != "settings_file_version")) + ".");
                string value = Number(property.Value, "'" + key + "'");
                string line = key + "=" + value;
                string previous = null;
                bool found = false;
                //Edited in place, as the hair and skin editor does: unknown keys and the file's order survive
                for (int i = 0; i < lines.Count; i++)
                {
                    if (!HairAndSkinShadingEditor.IsKey(lines[i], key)) continue;
                    previous = lines[i].Substring(lines[i].IndexOf('=') + 1).Trim();
                    lines[i] = line;
                    found = true;
                    break;
                }
                if (!found)
                    lines.Add(line);
                changed.Add(new JObject() { ["setting"] = key, ["value"] = value, ["previous"] = previous });
            }
            JObject result = new JObject() { ["file"] = settings.File, ["changed"] = changed, ["dry_run"] = dryRun };
            if (!BeginWrite(call, dryRun, new[] { settings.File }))
                return result;
            Modding.ModServices.CaptureBeforeWrite(full);
            File.WriteAllLines(full, lines);
            result["written"] = "DATA/" + settings.File;
            return result;
        }
        #endregion

        #region Records: interaction glow
        private const string GlowFile = "UI/SELECTIONOVERLAYPARAMS.BIN";

        private static SelectionOverlayParams LoadGlow()
        {
            string full = DataPath(GlowFile);
            if (!File.Exists(full))
                throw new McpError("There is no DATA/" + GlowFile + " (reset_configs global_constants restores it).");
            SelectionOverlayParams glow = new SelectionOverlayParams(full);
            if (!glow.Loaded)
                throw new McpError("Could not read DATA/" + GlowFile + ".");
            return glow;
        }

        private static JObject DescribeGlow(SelectionOverlayParams glow)
        {
            return new JObject()
            {
                ["colour"] = new JArray(Math.Round(glow.Colour.X, 4), Math.Round(glow.Colour.Y, 4), Math.Round(glow.Colour.Z, 4), Math.Round(glow.Colour.W, 4)),
                ["rate"] = glow.Rate,
                ["power"] = glow.Power,
            };
        }

        private static object GetGlow()
        {
            JObject result = DescribeGlow(LoadGlow());
            result["file"] = GlowFile;
            result["about"] = "The glow on things the player can interact with: colour is [red, green, blue, alpha] from 0 to 1.";
            return result;
        }

        private static object SetGlow(McpCall call, bool dryRun)
        {
            if (call.Has("name")) throw new McpError("interaction_glow is one file: leave out 'name'.");
            JObject values = ValuesArgument(call);
            SelectionOverlayParams glow = LoadGlow();
            JObject previous = DescribeGlow(glow);
            foreach (JProperty property in values.Properties())
            {
                switch (property.Name)
                {
                    case "colour":
                    case "color":
                        {
                            if (!(property.Value is JArray colour) || (colour.Count != 3 && colour.Count != 4))
                                throw new McpError("colour is [red, green, blue] or [red, green, blue, alpha], each 0 to 1.");
                            float[] parts = colour.Select((o, i) => Unit(o, "colour[" + i + "]")).ToArray();
                            glow.Colour = new Vector4(parts[0], parts[1], parts[2], parts.Length == 4 ? parts[3] : glow.Colour.W);
                            break;
                        }
                    case "alpha": glow.Colour.W = Unit(property.Value, "alpha"); break;
                    case "rate": glow.Rate = float.Parse(Number(property.Value, "rate"), CultureInfo.InvariantCulture); break;
                    case "power": glow.Power = float.Parse(Number(property.Value, "power"), CultureInfo.InvariantCulture); break;
                    default: throw new McpError("'" + property.Name + "' is not part of the glow: colour, alpha, rate, power.");
                }
            }
            JObject result = new JObject() { ["file"] = GlowFile, ["previous"] = previous, ["value"] = DescribeGlow(glow), ["dry_run"] = dryRun };
            if (!BeginWrite(call, dryRun, new[] { GlowFile }))
                return result;
            Modding.ModServices.CaptureBeforeWrite(DataPath(GlowFile));
            if (!glow.Save())
                throw new McpError("Could not write DATA/" + GlowFile + ".");
            result["written"] = "DATA/" + GlowFile;
            return result;
        }

        private static float Unit(JToken token, string what)
        {
            float value = float.Parse(Number(token, what), CultureInfo.InvariantCulture);
            if (value < 0 || value > 1)
                throw new McpError(what + " must be from 0 to 1.");
            return value;
        }
        #endregion

        #region Records: GBL_ITEM (inventory items, playlists, recipes)
        private const string GblItem = "GBL_ITEM.BML";

        private static readonly string[] HeldAttributes = { "held_object_name", "thrown_object_name", "droppable_when_held", "drop_when_consume", "consume_when", "activated_by", "cancellable_duration_in_seconds" };
        private static readonly string[] ItemBaseAttributes = { "name", "localisation_tag", "keyframe", "vanish_when_collected", "default_quantity", "stack_limit", "display_quantity", "radial_menu_order_index", "crafting_resource", "composite", "special_slot" };
        private static readonly string[] ItemBoolAttributes = { "vanish_when_collected", "display_quantity", "crafting_resource", "droppable_when_held", "drop_when_consume" };
        private static readonly string[] ItemNumberAttributes = { "default_quantity", "stack_limit", "radial_menu_order_index", "cancellable_duration_in_seconds", "health_increase_percentage", "upgraded_health_increase_percentage", "x", "y", "width", "height" };
        private static readonly string[] ConsumeWhen = { "Never", "OnEquip", "OnActivate", "OnThrowOrPlace" };
        private static readonly string[] ActivatedBy = { "CannotBeActivated", "PressToActivate", "HoldToActivate" };

        private static string[] TypeAttributes(string type)
        {
            switch (type)
            {
                case "weapon": return new[] { "weapon_type" };
                case "ammo": return new[] { "target_weapon", "ammo_type" };
                case "medikit": return HeldAttributes.Concat(new[] { "health_increase_percentage", "upgraded_health_increase_percentage" }).ToArray();
                case "ied":
                case "light": return HeldAttributes;
                default: return new string[0];
            }
        }

        private static string[] EnumNames<T>() => Enum.GetValues(typeof(T)).Cast<object>().Where(o => Convert.ToInt32(o) != -1).Select(o => o.ToString()).ToArray();

        private static XmlElement Items(XmlDocument document) => document["item_database"]?["objects"] ?? throw new McpError("GBL_ITEM.BML has no item_database/objects.");

        private static XmlElement FindItem(XmlDocument document, string name)
        {
            List<XmlElement> items = Items(document).ChildNodes.OfType<XmlElement>().ToList();
            XmlElement item = items.FirstOrDefault(o => o.GetAttribute("name") == name)
                ?? OnlyMatch(items.Where(o => string.Equals(o.GetAttribute("name"), name, StringComparison.OrdinalIgnoreCase)), o => o.GetAttribute("name"), name, "give the exact name (they differ only in case).");
            if (item == null)
                throw new McpError("There is no inventory item '" + name + "'." + Suggest(items.Select(o => o.GetAttribute("name")), name));
            return item;
        }

        private static List<string> WeaponNames(XmlDocument document) => Items(document).ChildNodes.OfType<XmlElement>().Where(o => o.Name == "weapon").Select(o => o.GetAttribute("name")).ToList();

        private static object GetInventoryItem(string name, int limit)
        {
            XmlDocument document = LoadBml(GblItem, out _);
            if (string.IsNullOrWhiteSpace(name))
            {
                List<XmlElement> items = Items(document).ChildNodes.OfType<XmlElement>().ToList();
                return new JObject()
                {
                    ["count"] = items.Count,
                    ["items"] = new JArray(items.Take(limit).Select(o => new JObject() { ["name"] = o.GetAttribute("name"), ["type"] = o.Name })),
                    ["special_slots"] = new JArray(document["item_database"]?["special_slots"]?.ChildNodes.OfType<XmlElement>().Select(o => o.GetAttribute("name")) ?? Enumerable.Empty<string>()),
                    ["types"] = new JArray(InventoryItemEditor.CreatableTypes),
                };
            }
            XmlElement item = FindItem(document, name);
            JObject attributes = new JObject();
            foreach (XmlAttribute attribute in item.Attributes)
            {
                if (attribute.Name == "ammo_type" && int.TryParse(attribute.Value, out int ammo) && Enum.IsDefined(typeof(AMMO_TYPE), ammo))
                    attributes[attribute.Name] = ((AMMO_TYPE)ammo).ToString();
                else
                    attributes[attribute.Name] = attribute.Value;
            }
            JObject choices = new JObject();
            foreach (string attribute in TypeAttributes(item.Name))
            {
                string[] options = ItemChoices(attribute, document);
                if (options != null) choices[attribute] = new JArray(options);
            }
            return new JObject()
            {
                ["name"] = item.GetAttribute("name"),
                ["type"] = item.Name,
                ["attributes"] = attributes,
                ["editable"] = new JArray(ItemBaseAttributes.Concat(TypeAttributes(item.Name))),
                ["choices"] = choices,
                ["about"] = "Attributes as GBL_ITEM.BML holds them (vanish_when_collected is the file's value; the editor's checkbox shows its inverse). ammo_type is shown and set by name.",
            };
        }

        /// <summary>An attribute's value for a report: ammo_type is stored as a number, shown as its name.</summary>
        private static string ItemAttributeText(string attribute, string value)
        {
            if (attribute == "ammo_type" && int.TryParse(value, out int ammo) && Enum.IsDefined(typeof(AMMO_TYPE), ammo))
                return ((AMMO_TYPE)ammo).ToString();
            return value;
        }

        private static string[] ItemChoices(string attribute, XmlDocument document)
        {
            switch (attribute)
            {
                case "weapon_type": return EnumNames<WEAPON_TYPE>();
                case "ammo_type": return EnumNames<AMMO_TYPE>();
                case "target_weapon": return WeaponNames(document).ToArray();
                case "consume_when": return ConsumeWhen;
                case "activated_by": return ActivatedBy;
                default: return null;
            }
        }

        private static object SetInventoryItem(McpCall call, string name, bool dryRun)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new McpError("Say which item ('name'); get_config_record with no name lists them.");
            name = name.Trim();
            JObject values = call.Object("values") ?? new JObject();
            bool create = call.Bool("create");
            XmlDocument document = LoadBml(GblItem, out BML bml);
            XmlElement objects = Items(document);
            List<string> names = objects.ChildNodes.OfType<XmlElement>().Select(o => o.GetAttribute("name")).ToList();
            XmlElement item;
            JObject result = new JObject();
            if (create)
            {
                string type = call.Str("item_type");
                if (type == null || !InventoryItemEditor.CreatableTypes.Contains(type))
                    throw new McpError("A new item needs 'item_type': " + string.Join(", ", InventoryItemEditor.CreatableTypes) + ".");
                if (names.Any(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase)))
                    throw new McpError("There is already an item called '" + name + "'.");
                //As the editor's Add Item makes one, then its first save fills in every field it shows
                item = document.CreateElement(type);
                item.SetAttribute("name", name);
                item.SetAttribute("x", "0");
                item.SetAttribute("y", "1");
                item.SetAttribute("width", "1");
                item.SetAttribute("height", "1");
                objects.AppendChild(item);
                item.SetAttribute("localisation_tag", name);
                item.SetAttribute("keyframe", name);
                item.SetAttribute("vanish_when_collected", "false");
                item.SetAttribute("default_quantity", "1");
                item.SetAttribute("stack_limit", "1");
                item.SetAttribute("display_quantity", "false");
                item.SetAttribute("radial_menu_order_index", "0");
                item.SetAttribute("crafting_resource", "false");
                item.SetAttribute("composite", "Pickups\\" + name);
                item.SetAttribute("special_slot", "");
                switch (type)
                {
                    case "weapon":
                        item.SetAttribute("weapon_type", EnumNames<WEAPON_TYPE>().First());
                        break;
                    case "ammo":
                        item.SetAttribute("target_weapon", WeaponNames(document).FirstOrDefault() ?? "");
                        item.SetAttribute("ammo_type", Convert.ToInt32(Enum.Parse(typeof(AMMO_TYPE), EnumNames<AMMO_TYPE>().First())).ToString());
                        break;
                }
                if (TypeAttributes(type).Contains("activated_by"))
                {
                    item.SetAttribute("droppable_when_held", "false");
                    item.SetAttribute("drop_when_consume", "false");
                    item.SetAttribute("consume_when", ConsumeWhen[0]);
                    item.SetAttribute("activated_by", ActivatedBy[0]);
                    item.SetAttribute("cancellable_duration_in_seconds", "0");
                }
                if (type == "medikit")
                {
                    item.SetAttribute("health_increase_percentage", "0");
                    item.SetAttribute("upgraded_health_increase_percentage", "0");
                }
                result["created"] = type;
            }
            else
            {
                if (call.Has("item_type"))
                    throw new McpError("'item_type' is only for create:true (an item's type cannot change).");
                if (values.Count == 0)
                    throw new McpError("'values' is empty: say what to change.");
                item = FindItem(document, name);
            }

            string itemName = item.GetAttribute("name");
            HashSet<string> allowed = new HashSet<string>(ItemBaseAttributes.Concat(TypeAttributes(item.Name)).Concat(item.Attributes.Cast<XmlAttribute>().Select(o => o.Name)));
            JArray changed = new JArray();
            string renamedFrom = null;
            foreach (JProperty property in values.Properties())
            {
                string attribute = property.Name.Trim();
                string what = "'" + attribute + "'";
                if (!allowed.Contains(attribute))
                    throw new McpError(what + " is not an attribute a " + item.Name + " item has. It takes: " + string.Join(", ", allowed) + ".");
                string previous = item.HasAttribute(attribute) ? item.GetAttribute(attribute) : null;
                string value = TokenText(property.Value, what);
                if (ItemBoolAttributes.Contains(attribute))
                    value = ParseBool(value, what) ? "true" : "false";
                else if (ItemNumberAttributes.Contains(attribute))
                    value = Number(property.Value, what);
                else
                {
                    string[] choices = ItemChoices(attribute, document);
                    if (choices != null)
                    {
                        string match = choices.FirstOrDefault(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase));
                        if (match == null && attribute == "ammo_type" && int.TryParse(value, out int number) && Enum.IsDefined(typeof(AMMO_TYPE), number) && number != -1)
                            match = ((AMMO_TYPE)number).ToString();
                        if (match == null)
                            throw new McpError(what + " must be one of: " + string.Join(", ", choices) + ".");
                        value = match;
                    }
                }

                switch (attribute)
                {
                    case "name":
                        if (value.Length == 0)
                            throw new McpError("An item's name cannot be empty.");
                        if (value != itemName && names.Any(o => o == value || (string.Equals(o, value, StringComparison.OrdinalIgnoreCase) && o != itemName)))
                            throw new McpError("There is already an item called '" + value + "'.");
                        if (value != itemName)
                        {
                            //Ammo aimed at the item follows the rename, as in the editor
                            InventoryItemEditor.RenameAmmoTargets(document, itemName, value);
                            renamedFrom = itemName;
                            itemName = value;
                        }
                        item.SetAttribute("name", value);
                        break;
                    case "special_slot":
                        //The special_slots list follows: the old slot is renamed, or a new one added
                        InventoryItemEditor.ApplySpecialSlot(document, item, value);
                        item.SetAttribute("special_slot", value);
                        break;
                    case "composite":
                        {
                            string leaf = InventoryItemEditor.StripKnownPrefixes(value, "Required_Assets\\Pickups\\", "Pickups\\", "Required_Assets\\");
                            item.SetAttribute("composite", "Pickups\\" + (string.IsNullOrWhiteSpace(leaf) ? itemName : leaf));
                            break;
                        }
                    case "held_object_name":
                        {
                            string leaf = InventoryItemEditor.StripKnownPrefixes(value, "Required_Assets\\Archetypes\\Equipment\\", "Equipment\\", "Required_Assets\\Archetypes\\");
                            if (string.IsNullOrWhiteSpace(leaf)) item.RemoveAttribute(attribute);
                            else item.SetAttribute(attribute, "Equipment\\" + leaf);
                            break;
                        }
                    case "thrown_object_name":
                        {
                            string leaf = InventoryItemEditor.StripKnownPrefixes(value, "Required_Assets\\Thrown\\", "Thrown\\", "Required_Assets\\");
                            if (string.IsNullOrWhiteSpace(leaf)) item.RemoveAttribute(attribute);
                            else item.SetAttribute(attribute, "Thrown\\" + leaf);
                            break;
                        }
                    case "ammo_type":
                        item.SetAttribute(attribute, ((int)(AMMO_TYPE)Enum.Parse(typeof(AMMO_TYPE), value)).ToString());
                        break;
                    default:
                        item.SetAttribute(attribute, value);
                        break;
                }
                changed.Add(new JObject() { ["attribute"] = attribute, ["value"] = ItemAttributeText(attribute, item.HasAttribute(attribute) ? item.GetAttribute(attribute) : null), ["previous"] = ItemAttributeText(attribute, previous) });
            }
            if (renamedFrom != null)
                call.Note("Renamed from '" + renamedFrom + "': ammo whose target_weapon named it follows. Scripts or other configs naming the old name do not.");

            result["name"] = itemName;
            result["type"] = item.Name;
            result["changed"] = changed;
            result["dry_run"] = dryRun;
            if (!BeginWrite(call, dryRun, new[] { GblItem }))
                return result;
            SaveBml(GblItem, bml, document);
            result["written"] = "DATA/" + GblItem;
            return result;
        }

        private static XmlElement Playlists(XmlDocument document) => document["item_database"]?["movie_playlists"] ?? throw new McpError("GBL_ITEM.BML has no item_database/movie_playlists.");

        private static List<string> MovieFiles()
        {
            string folder = DataPath("UI/MOVIES");
            return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.*").Select(Path.GetFileName).OrderBy(o => o).ToList() : new List<string>();
        }

        private static XmlElement FindPlaylist(XmlDocument document, string name)
        {
            List<XmlElement> playlists = Playlists(document).ChildNodes.OfType<XmlElement>().ToList();
            XmlElement playlist = playlists.FirstOrDefault(o => o.GetAttribute("playlist_name") == name)
                ?? OnlyMatch(playlists.Where(o => string.Equals(o.GetAttribute("playlist_name"), name, StringComparison.OrdinalIgnoreCase)), o => o.GetAttribute("playlist_name"), name, "give the exact name (they differ only in case).");
            if (playlist == null)
                throw new McpError("There is no movie playlist '" + name + "'." + Suggest(playlists.Select(o => o.GetAttribute("playlist_name")), name));
            return playlist;
        }

        private static readonly string[] PlaylistFlags = { "terminate_on_load_completed", "allow_player_to_skip", "shuffle_playlist", "loop_playlist" };

        private static object GetPlaylist(string name, int limit)
        {
            XmlDocument document = LoadBml(GblItem, out _);
            if (string.IsNullOrWhiteSpace(name))
            {
                List<XmlElement> playlists = Playlists(document).ChildNodes.OfType<XmlElement>().ToList();
                return new JObject()
                {
                    ["count"] = playlists.Count,
                    ["playlists"] = new JArray(playlists.Take(limit).Select(o => new JObject() { ["name"] = o.GetAttribute("playlist_name"), ["clips"] = o.ChildNodes.OfType<XmlElement>().Count() })),
                    ["movies"] = new JArray(MovieFiles()),
                };
            }
            XmlElement playlist = FindPlaylist(document, name);
            JObject result = new JObject() { ["name"] = playlist.GetAttribute("playlist_name") };
            foreach (XmlAttribute attribute in playlist.Attributes)
                if (attribute.Name != "playlist_name")
                    result[attribute.Name] = IsBoolText(attribute.Value) ? (JToken)string.Equals(attribute.Value, "true", StringComparison.OrdinalIgnoreCase) : attribute.Value;
            result["clips"] = new JArray(playlist.ChildNodes.OfType<XmlElement>().Select(o => o.GetAttribute("clip_name")));
            result["movies"] = new JArray(MovieFiles());
            return result;
        }

        private static object SetPlaylist(McpCall call, string name, bool dryRun)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new McpError("Say which playlist ('name'); get_config_record with no name lists them.");
            JObject values = ValuesArgument(call);
            XmlDocument document = LoadBml(GblItem, out BML bml);
            XmlElement playlist = FindPlaylist(document, name);
            JObject previous = new JObject();
            JObject now = new JObject();
            foreach (JProperty property in values.Properties())
            {
                if (PlaylistFlags.Contains(property.Name))
                {
                    previous[property.Name] = playlist.GetAttribute(property.Name);
                    playlist.SetAttribute(property.Name, ParseBool(TokenText(property.Value, property.Name), "'" + property.Name + "'") ? "true" : "false");
                    now[property.Name] = playlist.GetAttribute(property.Name);
                }
                else if (property.Name == "clips")
                {
                    List<string> movies = MovieFiles();
                    List<string> existing = playlist.ChildNodes.OfType<XmlElement>().Select(o => o.GetAttribute("clip_name")).ToList();
                    List<string> clips = new List<string>();
                    foreach (string wanted in StringList(property.Value, "clips"))
                    {
                        string file = wanted.Replace('\\', '/');
                        if (file.StartsWith("Movies/", StringComparison.OrdinalIgnoreCase)) file = file.Substring(7);
                        string match = movies.FirstOrDefault(o => string.Equals(o, file, StringComparison.OrdinalIgnoreCase))
                            ?? OnlyMatch(movies.Where(o => string.Equals(Path.GetFileNameWithoutExtension(o), file, StringComparison.OrdinalIgnoreCase)), o => o, wanted, "give the file name with its extension.");
                        if (match == null)
                            throw new McpError("There is no movie '" + wanted + "' in DATA/UI/MOVIES." + Suggest(movies, file));
                        //Keep the spelling the playlists already use for the same file ("Movies/D_Exterior_17.usm")
                        string spelt = existing.FirstOrDefault(o => string.Equals(o, "Movies/" + match, StringComparison.OrdinalIgnoreCase))
                            ?? (string.Equals(file, match, StringComparison.OrdinalIgnoreCase) && Path.HasExtension(file) ? "Movies/" + file : "Movies/" + match);
                        clips.Add(spelt);
                    }
                    previous["clips"] = new JArray(existing);
                    //Rebuilt as the Loadscreen Movies editor saves it: one clip per entry, clip_index its position
                    foreach (XmlElement clip in playlist.ChildNodes.OfType<XmlElement>().ToList())
                        playlist.RemoveChild(clip);
                    for (int i = 0; i < clips.Count; i++)
                    {
                        XmlElement clip = document.CreateElement("clip");
                        clip.SetAttribute("clip_name", clips[i]);
                        clip.SetAttribute("clip_index", i.ToString());
                        playlist.AppendChild(clip);
                    }
                    now["clips"] = new JArray(clips);
                }
                else
                    throw new McpError("'" + property.Name + "' is not part of a playlist: clips, " + string.Join(", ", PlaylistFlags) + ".");
            }
            JObject result = new JObject() { ["name"] = playlist.GetAttribute("playlist_name"), ["previous"] = previous, ["value"] = now, ["dry_run"] = dryRun };
            if (!BeginWrite(call, dryRun, new[] { GblItem }))
                return result;
            SaveBml(GblItem, bml, document);
            result["written"] = "DATA/" + GblItem;
            return result;
        }

        //What the Blueprint Recipes editor offers: core resources in, craftable items out
        private static readonly string[] RecipeInputs = { "gasoline", "high_voltage_battery", "explosive", "pipe", "sharp_blade", "gel", "adhesive", "scrap" };
        private static readonly string[] RecipeOutputs = { "pipe_bomb", "molotov_cocktail", "emp_mine", "smoke_bomb", "flashbang", "noisemaker", "wide_area_chem_light", "explosive_mine", "incendiary_mine", "chem_light", "small_medikit" };

        private static XmlElement Recipes(XmlDocument document) => document["item_database"]?["recipes"] ?? throw new McpError("GBL_ITEM.BML has no item_database/recipes.");

        private static JArray RecipePart(XmlElement recipe, string part) =>
            new JArray((recipe[part]?.ChildNodes.OfType<XmlElement>() ?? Enumerable.Empty<XmlElement>()).Select(o => new JObject() { ["name"] = o.GetAttribute("name"), ["quantity"] = int.TryParse(o.GetAttribute("quantity"), out int q) ? (JToken)q : o.GetAttribute("quantity") }));

        private static XmlElement FindRecipe(XmlDocument document, string name)
        {
            List<XmlElement> recipes = Recipes(document).ChildNodes.OfType<XmlElement>().ToList();
            XmlElement recipe = recipes.FirstOrDefault(o => o.GetAttribute("name") == name)
                ?? OnlyMatch(recipes.Where(o => string.Equals(o.GetAttribute("name"), name, StringComparison.OrdinalIgnoreCase)), o => o.GetAttribute("name"), name, "give the exact name (they differ only in case).");
            if (recipe == null)
                throw new McpError("There is no blueprint recipe '" + name + "'. Recipes: " + string.Join(", ", recipes.Select(o => o.GetAttribute("name"))) + ".");
            return recipe;
        }

        private static object GetRecipe(string name)
        {
            XmlDocument document = LoadBml(GblItem, out _);
            if (string.IsNullOrWhiteSpace(name))
            {
                return new JObject()
                {
                    ["recipes"] = new JArray(Recipes(document).ChildNodes.OfType<XmlElement>().Select(o => o.GetAttribute("name"))),
                    ["input_resources"] = new JArray(RecipeInputs),
                    ["craftable_outputs"] = new JArray(RecipeOutputs),
                };
            }
            XmlElement recipe = FindRecipe(document, name);
            return new JObject()
            {
                ["name"] = recipe.GetAttribute("name"),
                ["inputs"] = RecipePart(recipe, "input"),
                ["outputs"] = RecipePart(recipe, "output"),
                ["input_resources"] = new JArray(RecipeInputs),
                ["craftable_outputs"] = new JArray(RecipeOutputs),
            };
        }

        private static object SetRecipe(McpCall call, string name, bool dryRun)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new McpError("Say which recipe ('name'); get_config_record with no name lists them.");
            JObject values = ValuesArgument(call);
            XmlDocument document = LoadBml(GblItem, out BML bml);
            XmlElement recipe = FindRecipe(document, name);
            HashSet<string> itemNames = new HashSet<string>(Items(document).ChildNodes.OfType<XmlElement>().Select(o => o.GetAttribute("name")), StringComparer.OrdinalIgnoreCase);
            JObject previous = new JObject();
            foreach (JProperty property in values.Properties())
            {
                string part = property.Name == "inputs" ? "input" : property.Name == "outputs" ? "output" : null;
                if (part == null)
                    throw new McpError("'" + property.Name + "' is not part of a recipe: inputs, outputs.");
                if (!(property.Value is JArray entries))
                    throw new McpError(property.Name + " must be a list of {name, quantity}.");
                string[] offered = part == "input" ? RecipeInputs : RecipeOutputs;
                List<XmlNode> made = new List<XmlNode>();
                foreach (JToken entry in entries)
                {
                    if (!(entry is JObject line) || line["name"] == null)
                        throw new McpError(property.Name + " must be a list of {name, quantity}.");
                    string item = TokenText(line["name"], property.Name + " name");
                    string known = offered.FirstOrDefault(o => string.Equals(o, item, StringComparison.OrdinalIgnoreCase));
                    if (known == null)
                    {
                        if (!itemNames.Contains(item))
                            throw new McpError("'" + item + "' is not an inventory item. The editor offers: " + string.Join(", ", offered) + ".");
                        call.Note("'" + item + "' is an item, but not one the Blueprint Recipes editor offers as " + (part == "input" ? "a core resource" : "a craftable output") + ".");
                        known = item;
                    }
                    string quantity = line["quantity"] == null ? "1" : TokenText(line["quantity"], item + " quantity");
                    if (!int.TryParse(quantity, out int count) || count < 1)
                        throw new McpError(item + "'s quantity must be a whole number of at least 1.");
                    XmlElement element = document.CreateElement("item");
                    element.SetAttribute("name", known);
                    element.SetAttribute("quantity", count.ToString());
                    made.Add(element);
                }
                previous[property.Name] = RecipePart(recipe, part);
                XmlElement holder = recipe[part];
                if (holder == null)
                {
                    holder = document.CreateElement(part);
                    recipe.AppendChild(holder);
                }
                holder.RemoveAll();
                foreach (XmlNode element in made)
                    holder.AppendChild(element);
            }
            JObject result = new JObject()
            {
                ["name"] = recipe.GetAttribute("name"),
                ["inputs"] = RecipePart(recipe, "input"),
                ["outputs"] = RecipePart(recipe, "output"),
                ["previous"] = previous,
                ["dry_run"] = dryRun,
            };
            if (!BeginWrite(call, dryRun, new[] { GblItem }))
                return result;
            SaveBml(GblItem, bml, document);
            result["written"] = "DATA/" + GblItem;
            return result;
        }
        #endregion

        #region Records: lists (soundbanks, physical materials)
        private const string SoundbankList = "LIST_OF_PERMANENT_SOUND_BANKS.TXT";

        private static List<string> SoundBanks()
        {
            string folder = DataPath("SOUND");
            return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.BNK").Select(Path.GetFileNameWithoutExtension).OrderBy(o => o).ToList() : new List<string>();
        }

        private static List<string> PermanentBanks()
        {
            string full = DataPath(SoundbankList);
            return File.Exists(full) ? File.ReadAllLines(full).Select(o => o.Trim()).Where(o => o.Length != 0).ToList() : new List<string>();
        }

        private static object GetSoundbanks()
        {
            List<string> available = SoundBanks();
            return new JObject()
            {
                ["file"] = SoundbankList,
                ["banks"] = new JArray(PermanentBanks()),
                ["available_count"] = available.Count,
                ["available"] = new JArray(available),
                ["about"] = "The sound banks the game keeps loaded everywhere; a level loads others itself (SoundLoadBank).",
            };
        }

        private static object SetSoundbanks(McpCall call, bool dryRun)
        {
            if (call.Has("name")) throw new McpError("permanent_soundbanks is one list: leave out 'name'.");
            JObject values = ValuesArgument(call);
            List<string> available = SoundBanks();
            List<string> before = PermanentBanks();
            List<string> banks = new List<string>(before);
            foreach (JProperty property in values.Properties())
                if (property.Name != "banks" && property.Name != "add" && property.Name != "remove")
                    throw new McpError("'" + property.Name + "' is not part of this list: banks (the whole list), add, remove.");
            if (values["banks"] != null && (values["add"] != null || values["remove"] != null))
                throw new McpError("Give either the whole list ('banks') or 'add'/'remove', not both.");

            Func<string, string> known = wanted =>
            {
                string existing = banks.Concat(before).FirstOrDefault(o => string.Equals(o, wanted, StringComparison.OrdinalIgnoreCase));
                if (existing != null) return existing;
                string file = available.FirstOrDefault(o => string.Equals(o, wanted, StringComparison.OrdinalIgnoreCase));
                if (file == null)
                    throw new McpError("There is no sound bank '" + wanted + "' in DATA/SOUND." + Suggest(available, wanted));
                return file;
            };
            if (values["banks"] != null)
                banks = StringList(values["banks"], "banks").Select(known).ToList();
            foreach (string remove in StringList(values["remove"], "remove"))
            {
                if (banks.RemoveAll(o => string.Equals(o, remove, StringComparison.OrdinalIgnoreCase)) == 0)
                    throw new McpError("'" + remove + "' is not in the permanent list.");
            }
            foreach (string add in StringList(values["add"], "add"))
            {
                string bank = known(add);
                if (banks.Any(o => string.Equals(o, bank, StringComparison.OrdinalIgnoreCase)))
                    call.Note(bank + " is already in the list.");
                else
                    banks.Add(bank);
            }
            //No bank twice
            banks = banks.GroupBy(o => o, StringComparer.OrdinalIgnoreCase).Select(o => o.First()).ToList();

            JObject result = new JObject() { ["file"] = SoundbankList, ["previous"] = new JArray(before), ["banks"] = new JArray(banks), ["dry_run"] = dryRun };
            if (!BeginWrite(call, dryRun, new[] { SoundbankList }))
                return result;
            Modding.ModServices.CaptureBeforeWrite(DataPath(SoundbankList));
            File.WriteAllLines(DataPath(SoundbankList), banks);
            result["written"] = "DATA/" + SoundbankList;
            return result;
        }

        private const string MaterialsFile = "MATERIAL_DATA/MATERIALS.BML";

        private static List<string> MaterialNames(XmlDocument document) =>
            (document["Materials"] ?? throw new McpError("MATERIALS.BML has no Materials list.")).ChildNodes.OfType<XmlElement>().Select(o => o.GetAttribute("name")).ToList();

        private static object GetPhysicalMaterials(int limit)
        {
            List<string> names = MaterialNames(LoadBml(MaterialsFile, out _));
            return new JObject()
            {
                ["file"] = MaterialsFile,
                ["count"] = names.Count,
                ["materials"] = new JArray(names.Take(limit).Select((o, i) => new JObject() { ["index"] = i, ["name"] = o })),
                ["about"] = "The engine looks physical materials up by index, so new ones can only be added at the end, and none removed.",
            };
        }

        private static object SetPhysicalMaterials(McpCall call, bool dryRun)
        {
            if (call.Has("name")) throw new McpError("physical_materials is one list: leave out 'name'.");
            JObject values = ValuesArgument(call);
            XmlDocument document = LoadBml(MaterialsFile, out BML bml);
            List<string> existing = MaterialNames(document);
            List<string> added = new List<string>();
            foreach (JProperty property in values.Properties())
            {
                if (property.Name == "add")
                    added.AddRange(StringList(property.Value, "add"));
                else if (property.Name == "materials")
                {
                    List<string> wanted = StringList(property.Value, "materials");
                    for (int i = 0; i < existing.Count; i++)
                        if (i >= wanted.Count || wanted[i] != existing[i])
                            throw new McpError("The list has to keep every existing material in its place (the engine looks them up by index): entry " + i + " is '" + existing[i] + "'. Only additions at the end are possible.");
                    added.AddRange(wanted.Skip(existing.Count));
                }
                else
                    throw new McpError("'" + property.Name + "' is not part of this list: add (new names), or materials (the whole list, existing ones unchanged).");
            }
            if (added.Count == 0)
                throw new McpError("No new material to add.");
            HashSet<string> taken = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            XmlElement list = document["Materials"];
            foreach (string name in added)
            {
                if (name.Length == 0 || name.Any(char.IsWhiteSpace))
                    throw new McpError("'" + name + "' is not a usable material name (no spaces).");
                if (!taken.Add(name))
                    throw new McpError("There is already a physical material called '" + name + "'.");
                //Appended as the editor writes its entries: the engine only uses the name
                XmlElement material = document.CreateElement("Material");
                material.SetAttribute("name", name);
                list.AppendChild(material);
            }
            JObject result = new JObject()
            {
                ["file"] = MaterialsFile,
                ["added"] = new JArray(added.Select((o, i) => new JObject() { ["index"] = existing.Count + i, ["name"] = o })),
                ["dry_run"] = dryRun,
            };
            if (!BeginWrite(call, dryRun, new[] { MaterialsFile }))
                return result;
            SaveBml(MaterialsFile, bml, document);
            result["written"] = "DATA/" + MaterialsFile;
            return result;
        }
        #endregion

        #region Records: character asset sets
        private const string AssetFile = "CHR_INFO/CUSTOMCHARACTERASSETDATA.BIN";

        private static CustomCharacterAssetData LoadAssets()
        {
            string full = DataPath(AssetFile);
            if (!File.Exists(full))
                throw new McpError("There is no DATA/" + AssetFile + " (reset_configs character_assets restores it).");
            CustomCharacterAssetData data = new CustomCharacterAssetData(full);
            if (!data.Loaded)
                throw new McpError("Could not read DATA/" + AssetFile + ".");
            return data;
        }

        private static CustomCharacterAssetData.AssetDefinition FindAssetSet(CustomCharacterAssetData data, string name)
        {
            CustomCharacterAssetData.AssetDefinition set = data.Entries.FirstOrDefault(o => string.Equals(o.AssetType.ToString(), name, StringComparison.OrdinalIgnoreCase));
            if (set == null)
                throw new McpError("There is no asset set '" + name + "'. Sets: " + string.Join(", ", data.Entries.Select(o => o.AssetType.ToString())) + ".");
            return set;
        }

        private static JObject DescribeAssetSet(CustomCharacterAssetData.AssetDefinition set)
        {
            JObject result = new JObject() { ["name"] = set.AssetType.ToString() };
            foreach (CustomCharacterAssetData.ColourType colour in Enum.GetValues(typeof(CustomCharacterAssetData.ColourType)))
            {
                List<Vector3> tints = set.Tints.TryGetValue(colour, out List<Vector3> list) ? list : new List<Vector3>();
                result[colour.ToString().ToLowerInvariant()] = new JArray(tints.Select(o => new JArray(Math.Round(o.X, 4), Math.Round(o.Y, 4), Math.Round(o.Z, 4))));
            }
            result["decals"] = new JArray(set.Decals);
            return result;
        }

        private static object GetAssetSet(string name)
        {
            CustomCharacterAssetData data = LoadAssets();
            if (string.IsNullOrWhiteSpace(name))
            {
                return new JObject()
                {
                    ["file"] = AssetFile,
                    ["sets"] = new JArray(data.Entries.Select(o => new JObject()
                    {
                        ["name"] = o.AssetType.ToString(),
                        ["tints"] = o.Tints.Values.Sum(t => t.Count),
                        ["decals"] = o.Decals.Count,
                    })),
                    ["about"] = "Custom characters' tint colour lists ([r, g, b] from 0 to 1) and decal textures, per asset set.",
                };
            }
            return DescribeAssetSet(FindAssetSet(data, name));
        }

        private static object SetAssetSet(McpCall call, string name, bool dryRun)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new McpError("Say which asset set ('name', e.g. ASSETSET_01).");
            JObject values = ValuesArgument(call);
            CustomCharacterAssetData data = LoadAssets();
            CustomCharacterAssetData.AssetDefinition set = FindAssetSet(data, name);
            JObject previous = DescribeAssetSet(set);
            CathodeLib.Level level = Singleton.Editor?.CompositeBrowser?.Content?.Level;
            foreach (JProperty property in values.Properties())
            {
                if (property.Name == "decals")
                {
                    List<string> decals = StringList(property.Value, "decals");
                    foreach (string decal in decals)
                    {
                        if (decal.Length == 0 || decal.Length > 259 || decal.Any(c => c > 127))
                            throw new McpError("Decal '" + decal + "' is not a usable texture name (plain ASCII, under 260 characters).");
                        if (level != null && !level.Textures.Entries.Any(o => string.Equals(o?.Name, decal, StringComparison.OrdinalIgnoreCase)))
                            call.Note("The open level has no texture '" + decal + "' (the editor picks decals from the open level's textures; list_textures shows them).");
                    }
                    if (level == null && decals.Count != 0)
                        call.Note("No level is open, so the decal names were not checked against any textures.");
                    set.Decals = decals;
                    continue;
                }
                CustomCharacterAssetData.ColourType colour;
                if (!Enum.TryParse(property.Name, true, out colour) || int.TryParse(property.Name, out _))
                    throw new McpError("'" + property.Name + "' is not part of an asset set: primary, secondary, tertiary (tint lists), decals.");
                if (!(property.Value is JArray tints))
                    throw new McpError(property.Name + " is a list of [r, g, b] colours from 0 to 1.");
                List<Vector3> list = new List<Vector3>();
                foreach (JToken tint in tints)
                {
                    if (!(tint is JArray rgb) || rgb.Count != 3)
                        throw new McpError(property.Name + " is a list of [r, g, b] colours from 0 to 1.");
                    list.Add(new Vector3(Unit(rgb[0], property.Name + " red"), Unit(rgb[1], property.Name + " green"), Unit(rgb[2], property.Name + " blue")));
                }
                set.Tints[colour] = list;
            }
            JObject result = new JObject() { ["file"] = AssetFile, ["previous"] = previous, ["value"] = DescribeAssetSet(set), ["dry_run"] = dryRun };
            if (!BeginWrite(call, dryRun, new[] { AssetFile }))
                return result;

            //CathodeLib writes this file without truncating it, so a shorter file would keep the old tail: write a
            //fresh copy and put that in place
            string full = DataPath(AssetFile);
            string temp = Path.Combine(Path.GetTempPath(), "OpenCAGE_" + Guid.NewGuid().ToString("N") + ".BIN");
            try
            {
                if (!data.Save(temp, false))
                    throw new McpError("Could not write DATA/" + AssetFile + ".");
                Modding.ModServices.CaptureBeforeWrite(full);
                File.Copy(temp, full, true);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
            result["written"] = "DATA/" + AssetFile;
            return result;
        }
        #endregion

        #region Records: voice mappings
        private const string VoiceFile = "CHR_INFO/CUSTOMCHARACTERVOICETYPEMAPPINGS.BIN";

        private static XmlDocument LoadVoices(out XmlElement root)
        {
            string full = DataPath(VoiceFile);
            if (!File.Exists(full))
                throw new McpError("There is no DATA/" + VoiceFile + " (reset_configs voice_mappings restores it).");
            XmlDocument document = new XmlDocument() { PreserveWhitespace = true };
            document.Load(full);
            root = document["VoiceTypeMappings"] ?? throw new McpError("DATA/" + VoiceFile + " has no VoiceTypeMappings.");
            return document;
        }

        private static string SetPath(XmlElement set, XmlElement root)
        {
            List<string> parts = new List<string>();
            for (XmlElement at = set; at != null && at != root; at = at.ParentNode as XmlElement)
                parts.Insert(0, at.Name + "=" + at.GetAttribute("type"));
            return string.Join("/", parts);
        }

        private static void DescribeSets(XmlElement parent, XmlElement root, JArray into)
        {
            foreach (XmlElement set in parent.ChildNodes.OfType<XmlElement>().Where(o => o.Name != "VoiceType"))
            {
                into.Add(DescribeSet(set, root));
                DescribeSets(set, root, into);
            }
        }

        private static JObject DescribeSet(XmlElement set, XmlElement root)
        {
            List<string> voices = set.ChildNodes.OfType<XmlElement>().Where(o => o.Name == "VoiceType").Select(o => o.GetAttribute("value")).ToList();
            JObject entry = new JObject() { ["set"] = SetPath(set, root), ["voices"] = new JArray(voices) };
            if (voices.Count == 0) entry["warning"] = "no voices of its own: a character matching it but not a deeper set gets no voice";
            return entry;
        }

        private static List<KeyValuePair<string, string>> ParseSetPath(string name)
        {
            List<KeyValuePair<string, string>> parts = new List<KeyValuePair<string, string>>();
            foreach (string segment in name.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = segment.IndexOf('=');
                string kind = eq < 0 ? "" : segment.Substring(0, eq).Trim();
                string type = eq < 0 ? "" : segment.Substring(eq + 1).Trim().ToUpperInvariant();
                string canonical = VoiceMappingEditor.AttributeKinds.Keys.FirstOrDefault(o => string.Equals(o, kind, StringComparison.OrdinalIgnoreCase));
                if (canonical == null || type.Length == 0)
                    throw new McpError("'" + segment + "' is not an attribute: use Kind=TYPE with kind one of " + string.Join(", ", VoiceMappingEditor.AttributeKinds.Keys) + ", e.g. 'CharacterClass=CIVILIAN/Gender=MALE'.");
                if (!VoiceMappingEditor.AttributeKinds[canonical].Contains(type))
                    throw new McpError(canonical + " is one of: " + string.Join(", ", VoiceMappingEditor.AttributeKinds[canonical]) + ".");
                parts.Add(new KeyValuePair<string, string>(canonical, type));
            }
            if (parts.Count == 0)
                throw new McpError("Name the attribute set, e.g. 'CharacterClass=CIVILIAN/Gender=MALE'.");
            return parts;
        }

        private static XmlElement FindSet(XmlElement root, List<KeyValuePair<string, string>> path, bool create, XmlDocument document)
        {
            XmlElement at = root;
            foreach (KeyValuePair<string, string> part in path)
            {
                XmlElement next = at.ChildNodes.OfType<XmlElement>().FirstOrDefault(o => o.Name == part.Key && string.Equals(o.GetAttribute("type"), part.Value, StringComparison.OrdinalIgnoreCase));
                if (next == null)
                {
                    if (!create) return null;
                    next = document.CreateElement(part.Key);
                    next.SetAttribute("type", part.Value);
                    ConfigEditorUtils.AppendIndented(at, next, "\r\n\t\t");
                }
                at = next;
            }
            return at;
        }

        private static object GetVoiceMappings(string name, int limit)
        {
            XmlDocument document = LoadVoices(out XmlElement root);
            JArray sets = new JArray();
            if (string.IsNullOrWhiteSpace(name))
                DescribeSets(root, root, sets);
            else
            {
                XmlElement set = FindSet(root, ParseSetPath(name), false, document) ?? throw new McpError("There is no attribute set '" + name + "' (leave out name to list them).");
                sets.Add(DescribeSet(set, root));
                DescribeSets(set, root, sets);
            }
            List<string> voiceTypes = VoiceMappingEditor.KnownVoiceTypes.Concat(root.SelectNodes(".//VoiceType").OfType<XmlElement>().Select(o => o.GetAttribute("value"))).Where(o => o.Length != 0).Distinct().ToList();
            JObject kinds = new JObject();
            foreach (KeyValuePair<string, string[]> kind in VoiceMappingEditor.AttributeKinds)
                kinds[kind.Key] = new JArray(kind.Value);
            return new JObject()
            {
                ["file"] = VoiceFile,
                ["count"] = sets.Count,
                ["sets"] = new JArray(sets.Take(limit)),
                ["attribute_kinds"] = kinds,
                ["voice_types"] = new JArray(voiceTypes),
                ["about"] = "The engine picks a voice from the deepest set matching the character. Sets nest in any order; name one as its path, e.g. 'CharacterClass=CIVILIAN/Gender=MALE'.",
            };
        }

        private static object SetVoiceMappings(McpCall call, string name, bool dryRun)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new McpError("Name the attribute set, e.g. 'CharacterClass=CIVILIAN/Gender=MALE' (get_config_record lists them).");
            JObject values = ValuesArgument(call);
            XmlDocument document = LoadVoices(out XmlElement root);
            List<KeyValuePair<string, string>> path = ParseSetPath(name);
            bool existed = FindSet(root, path, false, document) != null;
            JObject result = new JObject() { ["file"] = VoiceFile };
            foreach (JProperty property in values.Properties())
                if (property.Name != "voices" && property.Name != "remove")
                    throw new McpError("'" + property.Name + "' is not part of an attribute set: voices (its whole voice list), remove (true drops the set and everything in it).");
            if (values["remove"] != null && ParseBool(TokenText(values["remove"], "remove"), "remove"))
            {
                if (values["voices"] != null)
                    throw new McpError("Give voices or remove, not both.");
                XmlElement set = FindSet(root, path, false, document) ?? throw new McpError("There is no attribute set '" + name + "'.");
                result["removed"] = SetPath(set, root);
                if (set.PreviousSibling != null && set.PreviousSibling.NodeType == XmlNodeType.Whitespace)
                    set.ParentNode.RemoveChild(set.PreviousSibling);
                set.ParentNode.RemoveChild(set);
            }
            else if (values["voices"] != null)
            {
                if (!existed && !call.Bool("create"))
                    throw new McpError("There is no attribute set '" + name + "'; pass create:true to make it.");
                XmlElement set = FindSet(root, path, true, document);
                List<string> known = VoiceMappingEditor.KnownVoiceTypes.Concat(root.SelectNodes(".//VoiceType").OfType<XmlElement>().Select(o => o.GetAttribute("value"))).ToList();
                List<string> voices = StringList(values["voices"], "voices").Select(o => o.Trim().ToUpperInvariant()).Where(o => o.Length != 0).Distinct().ToList();
                foreach (string voice in voices.Where(o => !known.Contains(o)))
                    call.Note(voice + " is not a voice type the file's readme lists or it already uses.");
                result["previous"] = new JArray(set.ChildNodes.OfType<XmlElement>().Where(o => o.Name == "VoiceType").Select(o => o.GetAttribute("value")));
                foreach (XmlElement voice in set.ChildNodes.OfType<XmlElement>().Where(o => o.Name == "VoiceType").ToList())
                {
                    if (voice.PreviousSibling != null && voice.PreviousSibling.NodeType == XmlNodeType.Whitespace)
                        set.RemoveChild(voice.PreviousSibling);
                    set.RemoveChild(voice);
                }
                //Voices first, before any nested sets, as the file lays them out
                XmlNode firstSet = set.ChildNodes.OfType<XmlElement>().FirstOrDefault();
                foreach (string voice in voices)
                {
                    XmlElement element = document.CreateElement("VoiceType");
                    element.SetAttribute("value", voice);
                    if (firstSet == null)
                        ConfigEditorUtils.AppendIndented(set, element, "\r\n\t\t");
                    else
                    {
                        XmlNode indent = firstSet.PreviousSibling;
                        set.InsertBefore(element, firstSet);
                        if (indent != null && indent.NodeType == XmlNodeType.Whitespace)
                            set.InsertBefore(indent.CloneNode(true), firstSet);
                    }
                }
                result["set"] = SetPath(set, root);
                result["voices"] = new JArray(voices);
                if (!existed) result["created"] = true;
                if (voices.Count == 0) call.Note("The set now has no voices of its own: a character it matches (and no deeper set) gets no voice.");
            }
            else
                throw new McpError("Give voices (the set's whole voice list) or remove:true.");
            result["dry_run"] = dryRun;
            if (!BeginWrite(call, dryRun, new[] { VoiceFile }))
                return result;
            Modding.ModServices.CaptureBeforeWrite(DataPath(VoiceFile));
            document.Save(DataPath(VoiceFile));
            result["written"] = "DATA/" + VoiceFile;
            return result;
        }
        #endregion

        #region Strings
        private static string SharedTextFolder => Singleton.PathToAI + @"\DATA\TEXT";

        private static string LevelFolder(string levelPath) => Path.Combine(Singleton.PathToAI, "DATA", "ENV", levelPath.Replace('/', '\\'));

        private static string TextFolder(string level, out string levelPath)
        {
            levelPath = level == null ? null : ResolveLevel(level);
            return levelPath == null ? SharedTextFolder : Path.Combine(LevelFolder(levelPath), "TEXT");
        }

        /// <summary>Every database a TEXT folder holds, in any language.</summary>
        private static List<string> DatabasesIn(string textFolder)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> ordered = new List<string>();
            foreach (string language in LocalisationHandler.LanguageFolders)
            {
                string folder = Path.Combine(textFolder, language);
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.GetFiles(folder, "*.TXT", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!string.Equals(name, "TEXT_DB_LIST", StringComparison.OrdinalIgnoreCase) && names.Add(name))
                        ordered.Add(name);
                }
            }
            ordered.Sort(StringComparer.OrdinalIgnoreCase);
            return ordered;
        }

        private static string CanonicalDatabase(string textFolder, string database, string scope)
        {
            List<string> databases = DatabasesIn(textFolder);
            string match = databases.FirstOrDefault(o => string.Equals(o, database.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match == null)
                throw new McpError("There is no text database '" + database + "' in " + scope + "." + (databases.Count == 0 ? " It has none." : " It has: " + string.Join(", ", databases.Take(80)) + "."));
            return match;
        }

        private static LocalisationHandler.AYZ_Lang Language(string name)
        {
            if (name == null) return LocalisationHandler.AYZ_Lang.ENGLISH;
            if (Enum.TryParse(name.Trim(), true, out LocalisationHandler.AYZ_Lang language) && !int.TryParse(name, out _))
                return language;
            throw new McpError("'" + name + "' is not a language: " + string.Join(", ", LocalisationHandler.LanguageFolders) + ".");
        }

        //The text files mark a paragraph break with a blank line, which the reader turns into "\r\n\r\n"
        private static string ForDisplay(string text) => (text ?? "").Replace("\r\n", "\n");

        private static object ListStrings(McpCall call)
        {
            string folder = TextFolder(call.Str("level"), out string levelPath);
            string scope = levelPath == null ? "DATA/TEXT" : levelPath + "'s TEXT folder";
            string database = call.Has("database") ? CanonicalDatabase(folder, call.Str("database"), scope) : null;
            int limit = Math.Max(1, call.Int("limit", 100));
            string id = call.Str("id")?.Trim();

            if (id != null)
            {
                if (call.Has("filter") || call.Has("language") || call.Has("limit"))
                    throw new McpError("With 'id' the string comes back in every language: leave out filter, language and limit.");
                List<string> holding = new LocalisationHandler(folder, database).GetAllIDs(LocalisationHandler.AYZ_Lang.ENGLISH).Where(o => o.TextID == id).Select(o => o.MissionID).Distinct().ToList();
                if (holding.Count == 0)
                {
                    //Not in English: look in every language before saying it is not there
                    holding = LocalisationHandler.LanguageFolders.SelectMany((o, i) => new LocalisationHandler(folder, database).GetAllIDs((LocalisationHandler.AYZ_Lang)i)).Where(o => o.TextID == id).Select(o => o.MissionID).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }
                if (holding.Count == 0)
                    throw new McpError("No database in " + scope + (database != null ? " called " + database : "") + " has a string '" + id + "' (ids are case-sensitive; list_strings with filter searches).");
                JArray matches = new JArray();
                foreach (string db in holding)
                {
                    LocalisationHandler handler = new LocalisationHandler(folder, db);
                    JObject texts = new JObject();
                    for (int i = 0; i < LocalisationHandler.LanguageFolders.Length; i++)
                    {
                        LocalisedText? found = handler.GetAllStringsWithValues((LocalisationHandler.AYZ_Lang)i).Where(o => o.TextID == id).Cast<LocalisedText?>().FirstOrDefault();
                        texts[LocalisationHandler.LanguageFolders[i]] = found.HasValue ? ForDisplay(found.Value.TextValue) : null;
                    }
                    matches.Add(new JObject() { ["database"] = db, ["texts"] = texts });
                }
                return new JObject() { ["id"] = id, ["scope"] = scope, ["matches"] = matches };
            }

            LocalisationHandler.AYZ_Lang language = Language(call.Str("language"));
            List<LocalisedText> strings = new LocalisationHandler(folder, database).GetAllStringsWithValues(language);
            string filter = call.Str("filter");
            List<LocalisedText> shown = filter == null ? strings : strings.Where(o => o.TextID.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || (o.TextValue ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            JObject result = new JObject()
            {
                ["scope"] = scope,
                ["language"] = LocalisationHandler.LanguageFolders[(int)language],
                ["count"] = shown.Count,
                ["strings"] = new JArray(shown.Take(limit).Select(o => new JObject() { ["id"] = o.TextID, ["database"] = o.MissionID, ["text"] = ForDisplay(o.TextValue) })),
            };
            if (database == null)
                result["databases"] = new JArray(strings.GroupBy(o => o.MissionID).Select(o => new JObject() { ["name"] = o.Key, ["strings"] = o.Count() }));
            if (shown.Count > limit)
                call.Note("Showing " + limit + " of " + shown.Count + ": narrow with filter or database, or raise limit.");
            return result;
        }

        /// <summary>Text as the .TXT files can hold it: paragraphs split by a blank line, and nothing that would read as the end of the text or a new id.</summary>
        private static string StoredText(string text, string what, McpCall call)
        {
            string normalised = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            string[] paragraphs = Regex.Split(normalised, "\n+");
            if (paragraphs.Length > 1 && Regex.IsMatch(normalised, "(^|[^\n])\n([^\n]|$)"))
                call.Note(what + ": single line breaks became paragraph breaks - the text files only hold blank-line-separated paragraphs.");
            for (int i = 0; i < paragraphs.Length; i++)
            {
                if (i != 0 && paragraphs[i].StartsWith("["))
                    throw new McpError(what + ": a paragraph cannot start with '[' (the game would read it as a new string id).");
                if (i != paragraphs.Length - 1 && paragraphs[i].EndsWith("}"))
                    throw new McpError(what + ": a paragraph other than the last cannot end with '}' (the game would read it as the end of the text).");
            }
            return string.Join("\r\n\r\n", paragraphs);
        }

        private static bool FileHasId(string path, string id)
        {
            string marker = "[" + id + "]";
            return File.Exists(path) && File.ReadAllLines(path).Any(o => o == marker);
        }

        private static object SetStrings(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            bool createMissing = call.Bool("create_missing");
            string folder = TextFolder(call.Str("level"), out string levelPath);
            string scope = levelPath == null ? "DATA/TEXT" : levelPath + "'s TEXT folder";
            string database = CanonicalDatabase(folder, call.Str("database", required: true), scope);
            JArray strings = call.Array("strings", required: true);
            if (strings.Count == 0)
                throw new McpError("'strings' is empty.");
            string[] languages = LocalisationHandler.LanguageFolders;
            LocalisationHandler handler = new LocalisationHandler(folder, database);

            //What each language holds now, for previous values
            Dictionary<string, Dictionary<string, string>> current = new Dictionary<string, Dictionary<string, string>>();
            for (int i = 0; i < languages.Length; i++)
            {
                Dictionary<string, string> byId = new Dictionary<string, string>();
                foreach (LocalisedText text in handler.GetAllStringsWithValues((LocalisationHandler.AYZ_Lang)i))
                    if (!byId.ContainsKey(text.TextID)) byId.Add(text.TextID, text.TextValue);
                current[languages[i]] = byId;
            }

            List<Action> writes = new List<Action>();
            JArray report = new JArray();
            HashSet<string> seenIds = new HashSet<string>();
            foreach (JToken token in strings)
            {
                if (!(token is JObject entry))
                    throw new McpError("Each entry of 'strings' is {id, text or texts}.");
                foreach (JProperty property in entry.Properties())
                    if (property.Name != "id" && property.Name != "text" && property.Name != "texts")
                        throw new McpError("'" + property.Name + "' is not part of a string entry: id, text, texts.");
                string id = entry["id"]?.Type == JTokenType.String ? ((string)entry["id"]).Trim() : null;
                if (string.IsNullOrEmpty(id) || id.IndexOf('[') >= 0 || id.IndexOf(']') >= 0 || id.IndexOf('\n') >= 0)
                    throw new McpError("Each string needs an id, without [ ] or line breaks.");
                if (!seenIds.Add(id))
                    throw new McpError("'" + id + "' is given twice.");

                Dictionary<string, string> wanted = new Dictionary<string, string>();
                if (entry["text"] != null)
                {
                    string text = StoredText(TokenText(entry["text"], id + " text"), id, call);
                    foreach (string language in languages) wanted[language] = text;
                }
                if (entry["texts"] != null)
                {
                    if (!(entry["texts"] is JObject texts))
                        throw new McpError(id + ": texts is {LANGUAGE: text}.");
                    foreach (JProperty text in texts.Properties())
                    {
                        string language = languages.FirstOrDefault(o => string.Equals(o, text.Name, StringComparison.OrdinalIgnoreCase));
                        if (language == null)
                            throw new McpError(id + ": '" + text.Name + "' is not a language: " + string.Join(", ", languages) + ".");
                        wanted[language] = StoredText(TokenText(text.Value, id + " " + language), id + " (" + language + ")", call);
                    }
                }
                if (wanted.Count == 0)
                    throw new McpError(id + ": give text (every language) or texts ({LANGUAGE: text}).");

                List<string> have = languages.Where(o => FileHasId(Path.Combine(folder, o, database + ".TXT"), id)).ToList();
                JObject line = new JObject() { ["id"] = id };
                if (have.Count == 0)
                {
                    if (!createMissing)
                        throw new McpError(database + " has no string '" + id + "'. Pass create_missing:true to add it (nothing was changed).");
                    List<string> noFile = languages.Where(o => !File.Exists(Path.Combine(folder, o, database + ".TXT"))).ToList();
                    if (noFile.Count != 0)
                        throw new McpError(database + " has no file for " + string.Join(", ", noFile) + ", so a new string cannot go into every language.");
                    //A new string goes into every language, as the Add String dialog writes it: languages not given get the English (or any given) text
                    string fallback = wanted.TryGetValue("ENGLISH", out string english) ? english : wanted.Values.First();
                    string[] all = languages.Select(o => wanted.TryGetValue(o, out string text) ? text : fallback).ToArray();
                    List<string> filled = languages.Where(o => !wanted.ContainsKey(o)).ToList();
                    if (filled.Count != 0)
                        call.Note(id + ": " + string.Join(", ", filled) + " got the " + (wanted.ContainsKey("ENGLISH") ? "English" : "given") + " text (translate them with texts).");
                    line["created"] = true;
                    line["texts"] = new JObject(languages.Select((o, i) => new JProperty(o, ForDisplay(all[i]))));
                    writes.Add(() =>
                    {
                        if (!handler.TryAppendNewString(database, id, all, out string error))
                            throw new McpError(id + ": " + error);
                    });
                }
                else
                {
                    JObject previous = new JObject();
                    JObject now = new JObject();
                    JArray skipped = new JArray();
                    foreach (KeyValuePair<string, string> pair in wanted)
                    {
                        string path = Path.Combine(folder, pair.Key, database + ".TXT");
                        if (have.Contains(pair.Key))
                        {
                            previous[pair.Key] = current[pair.Key].TryGetValue(id, out string old) ? ForDisplay(old) : null;
                            now[pair.Key] = ForDisplay(pair.Value);
                            string language = pair.Key, value = pair.Value;
                            writes.Add(() => handler.UpdateLocalisedString(new LocalisedText(value, id, database, language)));
                        }
                        else if (createMissing && File.Exists(path))
                        {
                            now[pair.Key] = ForDisplay(pair.Value);
                            string value = pair.Value;
                            writes.Add(() => LocalisationHandler.AppendBlockToTextFile(path, "[" + id + "]", value));
                        }
                        else
                            skipped.Add(pair.Key);
                    }
                    if (skipped.Count != 0)
                        call.Note(id + " is not in " + string.Join(", ", skipped) + (createMissing ? " (no file for that language)" : "; pass create_missing:true to add it there") + ", so those were left alone.");
                    line["previous"] = previous;
                    line["texts"] = now;
                }
                report.Add(line);
            }

            JObject result = new JObject() { ["database"] = database, ["scope"] = scope, ["strings"] = report, ["dry_run"] = dryRun };
            if (levelPath != null && !ReadDbList(levelPath).Contains(database, StringComparer.OrdinalIgnoreCase))
                call.Note(database + " is not listed in " + levelPath + "'s TEXT_DB_LIST.TXT, so the game does not load it: set_text_databases add_local lists it.");
            if (!BeginWrite(call, dryRun, new string[0]))
                return result;
            foreach (Action write in writes)
                write();
            //The editor's string lists and the open level's strings are read from these files: bring them up to date
            handler.RefreshLoadedStrings(database);
            result["written"] = writes.Count;
            return result;
        }
        #endregion

        #region Text databases
        private const string LevelTextConfig = "LEVEL_TEXT_DATABASES.XML";
        private const string Globals = "globals";

        private static XmlDocument LoadLevelTextConfig()
        {
            string full = DataPath(LevelTextConfig);
            if (!File.Exists(full))
                throw new McpError("There is no DATA/" + LevelTextConfig + " (reset_configs level_text_databases restores it).");
            XmlDocument config = new XmlDocument() { PreserveWhitespace = true };
            config.Load(full);
            if (config["level_text_databases"] == null)
                throw new McpError("DATA/" + LevelTextConfig + " has no level_text_databases list.");
            return config;
        }

        private static List<string> ReadDbList(string levelPath)
        {
            string path = LevelTextDBEditor.LocalDbListPath(levelPath);
            return File.Exists(path) ? File.ReadAllLines(path).Select(o => o.Trim()).Where(o => o.Length != 0).ToList() : new List<string>();
        }

        //The string pickers only offer level databases named like the retail ones (see EnumStringListViewItems)
        private static bool InStringPickers(string database) =>
            database.StartsWith("DLC", StringComparison.OrdinalIgnoreCase) || database.StartsWith("T0", StringComparison.OrdinalIgnoreCase) || string.Equals(database, "UI", StringComparison.OrdinalIgnoreCase);

        private static JArray Named(IEnumerable<string> names, List<string> existing) =>
            new JArray(names.Select(o => existing.Contains(o, StringComparer.OrdinalIgnoreCase) ? (JToken)o : new JObject() { ["name"] = o, ["missing"] = true }));

        private static object ListTextDatabases(McpCall call)
        {
            XmlDocument config = LoadLevelTextConfig();
            List<string> shared = DatabasesIn(SharedTextFolder);
            string level = call.Str("level") ?? OpenLevelName();
            List<string> globals = LevelTextDBEditor.ReadXmlDbs(config, Globals);
            JObject result = new JObject() { ["globals"] = Named(globals, shared) };
            if (level == null || string.Equals(level.Trim(), Globals, StringComparison.OrdinalIgnoreCase))
            {
                result["level_blocks"] = new JArray(config["level_text_databases"].ChildNodes.OfType<XmlElement>().Where(o => o.Name == "level" && o.GetAttribute("name") != Globals).Select(o => o.GetAttribute("name")));
                result["shared_available"] = new JArray(shared);
                if (level == null) call.Note("No level is open: showing the globals block. Give level to see a level's databases.");
                return result;
            }

            string levelPath = ResolveLevel(level);
            string xmlName = levelPath.Split('/').Last();
            List<string> block = LevelTextDBEditor.ReadXmlDbs(config, xmlName);
            string localFolder = Path.Combine(LevelFolder(levelPath), "TEXT");
            List<string> local = Directory.Exists(localFolder) ? DatabasesIn(localFolder) : new List<string>();
            List<string> listed = ReadDbList(levelPath);

            //What the level loads, as Level.Load resolves it: shared ones named by its block or globals, then its listed own ones (which win)
            JArray loaded = new JArray();
            HashSet<string> localListed = new HashSet<string>(listed.Where(o => local.Contains(o, StringComparer.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase);
            foreach (string db in block.Concat(globals).Distinct(StringComparer.OrdinalIgnoreCase).Where(o => shared.Contains(o, StringComparer.OrdinalIgnoreCase) && !localListed.Contains(o)))
                loaded.Add(new JObject() { ["name"] = db, ["from"] = "shared" });
            foreach (string db in localListed)
                loaded.Add(new JObject() { ["name"] = db, ["from"] = "level" + (shared.Contains(db, StringComparer.OrdinalIgnoreCase) ? " (replaces the shared one)" : "") });

            result["level"] = levelPath;
            result["xml_block"] = xmlName;
            result["shared"] = Named(block, shared);
            result["local"] = new JArray(local.Select(o => new JObject()
            {
                ["name"] = o,
                ["listed"] = listed.Contains(o, StringComparer.OrdinalIgnoreCase),
                ["languages"] = LocalisationHandler.LanguageFolders.Count(l => File.Exists(Path.Combine(localFolder, l, o + ".TXT"))),
            }));
            JArray listedMissing = new JArray(listed.Where(o => !local.Contains(o, StringComparer.OrdinalIgnoreCase)));
            if (listedMissing.Count != 0) result["listed_but_missing"] = listedMissing;
            result["loaded"] = loaded;
            JArray hidden = new JArray(local.Where(o => !InStringPickers(o)));
            if (hidden.Count != 0) result["not_in_string_pickers"] = hidden;
            result["shared_available"] = new JArray(shared);
            return result;
        }

        private static object SetTextDatabases(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            string level = call.Str("level", required: true).Trim();
            bool isGlobals = string.Equals(level, Globals, StringComparison.OrdinalIgnoreCase);
            string levelPath = isGlobals ? null : ResolveLevel(level);
            string xmlName = isGlobals ? Globals : levelPath.Split('/').Last();
            List<string> addShared = call.StrList("add_shared"), removeShared = call.StrList("remove_shared");
            List<string> addLocal = call.StrList("add_local"), removeLocal = call.StrList("remove_local"), createLocal = call.StrList("create_local").Select(o => o.Trim()).ToList();
            if (isGlobals && (addLocal.Count != 0 || removeLocal.Count != 0 || createLocal.Count != 0))
                throw new McpError("The globals block has no level folder: add_local, remove_local and create_local need a level.");
            if (addShared.Count + removeShared.Count + addLocal.Count + removeLocal.Count + createLocal.Count == 0)
                throw new McpError("Nothing to change: give add_shared, remove_shared, add_local, remove_local or create_local.");

            XmlDocument config = LoadLevelTextConfig();
            List<string> shared = DatabasesIn(SharedTextFolder);
            JObject result = new JObject() { ["level"] = isGlobals ? Globals : levelPath };

            //Shared: the level's block of LEVEL_TEXT_DATABASES.XML
            List<string> before = LevelTextDBEditor.ReadXmlDbs(config, xmlName);
            List<string> after = new List<string>(before);
            foreach (string remove in removeShared)
                if (after.RemoveAll(o => string.Equals(o, remove.Trim(), StringComparison.OrdinalIgnoreCase)) == 0)
                    throw new McpError("'" + remove + "' is not in " + xmlName + "'s block. It has: " + (before.Count == 0 ? "nothing" : string.Join(", ", before)) + ".");
            foreach (string add in addShared)
            {
                string match = shared.FirstOrDefault(o => string.Equals(o, add.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    throw new McpError("DATA/TEXT has no database '" + add + "'." + Suggest(shared, add.Trim()));
                if (after.Any(o => string.Equals(o, match, StringComparison.OrdinalIgnoreCase)))
                    call.Note(match + " is already in " + xmlName + "'s block.");
                else
                    after.Add(match);
            }
            bool sharedChanged = !before.SequenceEqual(after);
            if (addShared.Count + removeShared.Count != 0)
                result["shared"] = new JObject() { ["previous"] = new JArray(before), ["now"] = new JArray(after) };

            //The level's own: TEXT/TEXT_DB_LIST.TXT, and new databases in its TEXT folder
            string localFolder = isGlobals ? null : Path.Combine(LevelFolder(levelPath), "TEXT");
            List<string> local = localFolder != null && Directory.Exists(localFolder) ? DatabasesIn(localFolder) : new List<string>();
            List<string> listedBefore = isGlobals ? new List<string>() : ReadDbList(levelPath);
            List<string> listed = new List<string>(listedBefore);
            for (int i = 0; i < createLocal.Count; i++)
            {
                string create = createLocal[i];
                if (create.Length == 0 || create.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || string.Equals(create, "TEXT_DB_LIST", StringComparison.OrdinalIgnoreCase))
                    throw new McpError("'" + create + "' is not a usable database name.");
                //Database names are file names, so two differing only in case are the same database
                if (createLocal.Take(i).Any(o => string.Equals(o, create, StringComparison.OrdinalIgnoreCase)))
                    throw new McpError("create_local names '" + create + "' more than once (names ignore case): give each new database once.");
                if (local.Contains(create, StringComparer.OrdinalIgnoreCase))
                    throw new McpError(levelPath + " already has a database called '" + create + "' (add_local lists it).");
                if (shared.Contains(create, StringComparer.OrdinalIgnoreCase) && !call.Bool("allow_shadowing"))
                    throw new McpError("DATA/TEXT already has a database called '" + create + "': one of that name in the level replaces it for this level. Pass allow_shadowing:true if that is meant, or choose another name.");
                if (!InStringPickers(create))
                    call.Note("'" + create + "' will not appear in the editor's STRING_OBJECTIVES / STRING_TERMINAL / STRING_UI pickers, which only offer level databases named DLC*, T0* or UI (a parameter can still name its ids).");
            }
            foreach (string remove in removeLocal)
                if (listed.RemoveAll(o => string.Equals(o, remove.Trim(), StringComparison.OrdinalIgnoreCase)) == 0)
                    throw new McpError("'" + remove + "' is not listed in " + levelPath + "'s TEXT_DB_LIST.TXT.");
            foreach (string add in addLocal.Concat(createLocal))
            {
                string match = local.Concat(createLocal).FirstOrDefault(o => string.Equals(o, add.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    throw new McpError(levelPath + "'s TEXT folder has no database '" + add + "' (create_local makes one)." + Suggest(local, add.Trim()));
                if (listed.Any(o => string.Equals(o, match, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!createLocal.Contains(add)) call.Note(match + " is already listed.");
                }
                else
                    listed.Add(match);
            }
            bool localChanged = !listedBefore.SequenceEqual(listed);
            if (!isGlobals && (addLocal.Count + removeLocal.Count + createLocal.Count != 0))
                result["local"] = new JObject() { ["previous"] = new JArray(listedBefore), ["now"] = new JArray(listed) };
            if (createLocal.Count != 0) result["created"] = new JArray(createLocal);
            result["dry_run"] = dryRun;
            if (!BeginWrite(call, dryRun, new[] { LevelTextConfig }))
                return result;

            //The new databases first: one that cannot be made then stops the call before either list is written
            List<string> created = new List<string>();
            foreach (string create in createLocal)
            {
                if (!LocalisationHandler.TryCreateDatabase(localFolder, create, out string error))
                    throw new McpError("Could not create '" + create + "': " + error + " Neither list was changed" +
                        (created.Count == 0 ? "." : "; " + string.Join(", ", created) + (created.Count == 1 ? " was" : " were") + " created before it but not listed (add_local lists " + (created.Count == 1 ? "it" : "them") + ")."));
                created.Add(create);
            }
            if (sharedChanged)
            {
                LevelTextDBEditor.SetXmlDbs(config, xmlName, after);
                Modding.ModServices.CaptureBeforeWrite(DataPath(LevelTextConfig));
                config.Save(DataPath(LevelTextConfig));
            }
            if (localChanged)
                LevelTextDBEditor.WriteLocalDbList(levelPath, listed);

            //A new database of the open level's is added to its loaded strings, as the editor does
            string open = OpenLevelName();
            if (!isGlobals && open != null && string.Equals(open, levelPath, StringComparison.OrdinalIgnoreCase))
            {
                foreach (string create in createLocal)
                    new LocalisationHandler(localFolder, create).RefreshLoadedStrings(create);
                if (sharedChanged || removeLocal.Count != 0)
                    call.Note("The open level's loaded strings still reflect the old list: load_level again to pick the change up (the game reads it at level load).");
            }
            result["written"] = true;
            return result;
        }
        #endregion

        #region Config elements
        private const string VoiceRoot = "VoiceTypeMappings";

        private static string ElementsPath(string relative)
        {
            string file = Relative(relative);
            string full = Path.GetFullPath(DataPath(file));
            if (!full.StartsWith(DataFolder + "\\", StringComparison.OrdinalIgnoreCase))
                throw new McpError("Configuration files have to be inside the install's DATA folder.");
            if (full.IndexOf("\\ENV\\", DataFolder.Length, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new McpError("Level files are not configuration files.");
            bool voices = string.Equals(file, VoiceFile, StringComparison.OrdinalIgnoreCase);
            if (!voices && !full.EndsWith(".BML", StringComparison.OrdinalIgnoreCase) && !full.EndsWith(".XML", StringComparison.OrdinalIgnoreCase))
                throw new McpError("Only .BML and .XML configuration files (and " + VoiceFile + ", which is XML) can be edited here; set_config_record covers the TXT and binary ones.");
            if (!File.Exists(full))
                throw new McpError("There is no DATA/" + file + " (list_config_files shows them).");
            return full;
        }

        /// <summary>
        /// A config file as XML to edit. A file whose root declares a default namespace (GBL_ITEM: xmlns=
        /// "http://www.w3schools.com") would match no plain-name XPath, so it is edited without it: defaultNamespace
        /// says what to put back when it is written (see <see cref="ForWriting"/>).
        /// </summary>
        internal static XmlDocument LoadForElements(string full, out BML bml, out string defaultNamespace)
        {
            bml = null;
            string text;
            bool whitespace;
            if (full.EndsWith(".BML", StringComparison.OrdinalIgnoreCase))
            {
                bml = new BML(full);
                XmlDocument content = bml.Loaded ? bml.Content : null;
                if (content?.DocumentElement == null)
                    throw new McpError("Could not read " + Path.GetFileName(full) + ".");
                defaultNamespace = content.DocumentElement.NamespaceURI.Length != 0 && content.DocumentElement.Prefix.Length == 0 ? content.DocumentElement.NamespaceURI : null;
                if (defaultNamespace == null)
                    return content;
                text = content.OuterXml;
                whitespace = false;
            }
            else
            {
                text = File.ReadAllText(full);
                //The Graphics editor's repair: a community copy of ENGINE_SETTINGS.XML carries a comment with the wrong dashes
                if (string.Equals(Path.GetFileName(full), "ENGINE_SETTINGS.XML", StringComparison.OrdinalIgnoreCase))
                    text = text.Replace("<!– resolution in pixels. –>", " ");
                whitespace = true;
                defaultNamespace = null;
            }
            XmlDocument document = new XmlDocument() { PreserveWhitespace = whitespace };
            try
            {
                document.LoadXml(text);
                if (defaultNamespace == null && document.DocumentElement.NamespaceURI.Length != 0 && document.DocumentElement.Prefix.Length == 0)
                    defaultNamespace = document.DocumentElement.NamespaceURI;
                if (defaultNamespace != null)
                {
                    //Only the root's own start tag loses its xmlns="..."
                    Match start = RootStart(text, document.DocumentElement.Name);
                    if (start == null || !Regex.IsMatch(start.Value, @"\sxmlns=""[^""]*"""))
                        throw new McpError(Path.GetFileName(full) + ": could not find its root's namespace declaration.");
                    string tag = Regex.Replace(start.Value, @"\s+xmlns=""[^""]*""", "");
                    document = new XmlDocument() { PreserveWhitespace = whitespace };
                    document.LoadXml(text.Substring(0, start.Index) + tag + text.Substring(start.Index + start.Length));
                }
            }
            catch (XmlException e) { throw new McpError(Path.GetFileName(full) + " is not valid XML: " + e.Message); }
            return document;
        }

        /// <summary>The edited document as it is written: with the default namespace it was loaded with put back.</summary>
        internal static XmlDocument ForWriting(XmlDocument document, string defaultNamespace, bool bml)
        {
            if (defaultNamespace == null || document.DocumentElement.HasAttribute("xmlns"))
                return document;
            if (bml)
            {
                //The BML writer takes names and attributes as they are, so the declaration goes back as the attribute it was
                XmlAttribute declaration = document.CreateAttribute("xmlns");
                declaration.Value = defaultNamespace;
                document.DocumentElement.Attributes.Prepend(declaration);
                return document;
            }
            string text = document.OuterXml;
            Match root = RootStart(text, document.DocumentElement.Name);
            if (root == null)
                throw new McpError("Could not put the namespace back on " + document.DocumentElement.Name + "; nothing was written.");
            text = text.Insert(root.Index + 1 + document.DocumentElement.Name.Length, " xmlns=\"" + SecurityElementEscape(defaultNamespace) + "\"");
            XmlDocument written = new XmlDocument() { PreserveWhitespace = document.PreserveWhitespace };
            written.LoadXml(text);
            return written;
        }

        private static string SecurityElementEscape(string text) => System.Security.SecurityElement.Escape(text);

        /// <summary>The root element's start tag in XML text: the first one of its name outside a comment.</summary>
        private static Match RootStart(string xml, string name)
        {
            for (Match start = Regex.Match(xml, "<" + Regex.Escape(name) + @"(?=[\s/>])[^>]*>"); start.Success; start = start.NextMatch())
            {
                string before = xml.Substring(0, start.Index);
                if (before.LastIndexOf("<!--", StringComparison.Ordinal) <= before.LastIndexOf("-->", StringComparison.Ordinal))
                    return start;
            }
            return null;
        }

        private static void InsertIndented(XmlNode anchor, XmlNode node, bool after)
        {
            XmlNode parent = anchor.ParentNode;
            XmlNode indent = anchor.PreviousSibling != null && anchor.PreviousSibling.NodeType == XmlNodeType.Whitespace ? anchor.PreviousSibling : null;
            if (after)
            {
                parent.InsertAfter(node, anchor);
                if (indent != null) parent.InsertBefore(indent.CloneNode(true), node);
            }
            else
            {
                parent.InsertBefore(node, anchor);
                if (indent != null) parent.InsertAfter(indent.CloneNode(true), node);
            }
        }

        private static string Preview(XmlNode node)
        {
            string xml = node.OuterXml;
            return xml.Length > 400 ? xml.Substring(0, 400) + "..." : xml;
        }

        private static object EditElements(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            string file = Relative(call.Str("file", required: true));
            string full = ElementsPath(file);
            JArray ops = call.Array("ops", required: true);
            if (ops.Count == 0)
                throw new McpError("'ops' is empty.");

            //Every spelling of the font config is written together, as the Font Config editor does
            List<string> targets = new List<string>() { full };
            bool fontConfig = FontConfigEditor.ConfigFiles.Any(o => string.Equals(o, Path.GetFileName(full), StringComparison.OrdinalIgnoreCase)) && Path.GetDirectoryName(full).Equals(DataFolder, StringComparison.OrdinalIgnoreCase);
            if (fontConfig)
            {
                targets = FontConfigEditor.ConfigFiles.Select(o => Path.Combine(DataFolder, o)).Where(File.Exists).ToList();
                full = targets[0];
                if (!string.Equals(Path.GetFileName(full), Path.GetFileName(DataPath(file)), StringComparison.OrdinalIgnoreCase))
                    call.Note("The font config is edited through " + Path.GetFileName(full) + " (the file the PC game reads), and every variant is written from it.");
            }
            bool voices = string.Equals(file, VoiceFile, StringComparison.OrdinalIgnoreCase);
            bool inputs = string.Equals(file, "INPUT.XML", StringComparison.OrdinalIgnoreCase);

            XmlDocument document = LoadForElements(full, out BML bml, out string defaultNamespace);
            if (defaultNamespace != null)
                call.Note(Path.GetFileName(full) + " declares a default namespace (" + defaultNamespace + "); it is set aside while editing so XPaths use plain element names, and written back unchanged.");
            JArray report = new JArray();
            for (int i = 0; i < ops.Count; i++)
            {
                if (!(ops[i] is JObject op))
                    throw new McpError("ops[" + i + "] is not an object.");
                string where = "ops[" + i + "]";
                string kind = op["op"]?.Type == JTokenType.String ? (string)op["op"] : null;
                string xpath = op["xpath"]?.Type == JTokenType.String ? (string)op["xpath"] : null;
                if (kind == null || xpath == null)
                    throw new McpError(where + " needs op and xpath.");
                HashSet<string> allowedKeys = new HashSet<string>() { "op", "xpath", "expect" };
                switch (kind)
                {
                    case "set": allowedKeys.UnionWith(new[] { "attribute", "value" }); break;
                    case "remove_attribute": allowedKeys.Add("attribute"); break;
                    case "add": allowedKeys.UnionWith(new[] { "element", "attributes", "text", "position" }); break;
                    case "duplicate": allowedKeys.Add("attributes"); break;
                    case "remove": break;
                    default: throw new McpError(where + ": '" + kind + "' is not an op: set, remove_attribute, add, duplicate, remove.");
                }
                foreach (JProperty property in op.Properties())
                    if (!allowedKeys.Contains(property.Name))
                        throw new McpError(where + ": '" + property.Name + "' does not apply to " + kind + " (it takes " + string.Join(", ", allowedKeys) + ").");

                XmlNodeList selected;
                try { selected = document.SelectNodes(xpath); }
                catch (Exception e) { throw new McpError(where + ": the XPath is not valid: " + e.Message); }
                List<XmlNode> nodes = selected.Cast<XmlNode>().ToList();
                if (op["expect"] != null)
                {
                    int expect = op["expect"].Value<int>();
                    if (nodes.Count != expect)
                        throw new McpError(where + ": the XPath selects " + nodes.Count + " nodes, not " + expect + ". Nothing was changed.");
                }
                else if (nodes.Count > 1)
                    throw new McpError(where + ": the XPath selects " + nodes.Count + " nodes; give expect:" + nodes.Count + " to change them all, or narrow it. Nothing was changed.");
                if (nodes.Count == 0)
                    throw new McpError(where + ": the XPath selects nothing in " + Path.GetFileName(full) + " (read_config shows the file). Nothing was changed.");

                JArray previews = new JArray();
                foreach (XmlNode node in nodes)
                {
                    switch (kind)
                    {
                        case "set":
                            {
                                string value = op["value"] == null ? throw new McpError(where + ": set needs a value.") : TokenText(op["value"], where + " value");
                                string attribute = op["attribute"]?.ToString();
                                if (attribute != null)
                                {
                                    if (!(node is XmlElement element)) throw new McpError(where + ": an attribute can only be set on an element.");
                                    element.SetAttribute(attribute, value);
                                    previews.Add(Preview(element));
                                }
                                else if (node is XmlElement element && element.ChildNodes.OfType<XmlElement>().Any())
                                    throw new McpError(where + ": that element holds other elements; setting its text would delete them.");
                                else
                                {
                                    if (node is XmlAttribute attr) attr.Value = value;
                                    else node.InnerText = value;
                                    previews.Add(Preview(node is XmlAttribute a ? a.OwnerElement : node));
                                }
                                break;
                            }
                        case "remove_attribute":
                            {
                                string attribute = op["attribute"]?.ToString() ?? throw new McpError(where + ": remove_attribute needs attribute.");
                                if (!(node is XmlElement element)) throw new McpError(where + ": attributes are removed from elements.");
                                if (!element.HasAttribute(attribute)) call.Note(where + ": " + element.Name + " had no " + attribute + ".");
                                element.RemoveAttribute(attribute);
                                previews.Add(Preview(element));
                                break;
                            }
                        case "add":
                            {
                                string name = op["element"]?.ToString();
                                if (string.IsNullOrWhiteSpace(name)) throw new McpError(where + ": add needs element (the new element's name).");
                                try { XmlConvert.VerifyName(name); } catch { throw new McpError(where + ": '" + name + "' is not a valid element name."); }
                                string position = op["position"]?.ToString() ?? "child";
                                if (voices && !VoiceMappingEditor.AttributeKinds.ContainsKey(name) && name != "VoiceType")
                                    throw new McpError(where + ": the voice mappings hold " + string.Join(", ", VoiceMappingEditor.AttributeKinds.Keys) + " and VoiceType elements only.");
                                XmlElement made = document.CreateElement(name);
                                SetAttributes(made, op["attributes"], where);
                                if (op["text"] != null) made.InnerText = TokenText(op["text"], where + " text");
                                if (position == "child")
                                {
                                    if (!(node is XmlElement || node is XmlDocument)) throw new McpError(where + ": elements are added inside an element.");
                                    ConfigEditorUtils.AppendIndented(node, made);
                                }
                                else if (position == "before" || position == "after")
                                {
                                    if (!(node is XmlElement) || node.ParentNode == null || node.ParentNode is XmlDocument) throw new McpError(where + ": before/after needs an element that has a parent element.");
                                    InsertIndented(node, made, position == "after");
                                }
                                else throw new McpError(where + ": position is child, before or after.");
                                previews.Add(Preview(made));
                                break;
                            }
                        case "duplicate":
                            {
                                if (!(node is XmlElement element) || node.ParentNode == null || node.ParentNode is XmlDocument) throw new McpError(where + ": only an element inside another element can be duplicated.");
                                XmlElement copy = (XmlElement)element.CloneNode(true);
                                SetAttributes(copy, op["attributes"], where);
                                InsertIndented(element, copy, true);
                                previews.Add(Preview(copy));
                                break;
                            }
                        case "remove":
                            {
                                if (node is XmlAttribute attribute)
                                {
                                    XmlElement owner = attribute.OwnerElement;
                                    owner.RemoveAttributeNode(attribute);
                                    previews.Add(Preview(owner));
                                    break;
                                }
                                if (node.ParentNode == null || node is XmlDocument || node == document.DocumentElement) throw new McpError(where + ": the root cannot be removed.");
                                if (inputs && node is XmlElement binding && binding.HasAttribute("action") &&
                                    node.ParentNode.ChildNodes.OfType<XmlElement>().Count(o => o.Name == binding.Name && o.GetAttribute("action") == binding.GetAttribute("action")) == 1)
                                    call.Note(where + ": that was the only " + binding.Name + " for '" + binding.GetAttribute("action") + "' on its device, so the action is now unbound there (setting id=\"\" instead keeps it listed but unbound, as the game ships unused actions).");
                                previews.Add("removed: " + Preview(node));
                                XmlNode parent = node.ParentNode;
                                //Take the indentation in front of it too, or the line it was on is left blank
                                if (node.PreviousSibling != null && node.PreviousSibling.NodeType == XmlNodeType.Whitespace)
                                    parent.RemoveChild(node.PreviousSibling);
                                parent.RemoveChild(node);
                                break;
                            }
                    }
                }
                report.Add(new JObject() { ["op"] = kind, ["xpath"] = xpath, ["matched"] = nodes.Count, ["result"] = new JArray(previews.Take(5)) });
            }

            if (string.Equals(file, "ENGINE_SETTINGS.XML", StringComparison.OrdinalIgnoreCase))
                call.Note("Graphics presets: each Quality's precedence decides its order in the menu; players may need to delete their saved settings file for removed presets (the Graphics editor warns the same).");
            List<string> relatives = targets.Select(o => o.Substring(DataFolder.Length + 1).Replace('\\', '/')).ToList();
            JObject result = new JObject() { ["file"] = file, ["ops"] = report, ["dry_run"] = dryRun };
            if (!BeginWrite(call, dryRun, relatives))
            {
                result["would_write"] = new JArray(relatives.Select(o => "DATA/" + o));
                return result;
            }
            foreach (string target in targets)
            {
                Modding.ModServices.CaptureBeforeWrite(target);
                if (target.EndsWith(".BML", StringComparison.OrdinalIgnoreCase))
                {
                    BML output = string.Equals(target, full, StringComparison.OrdinalIgnoreCase) && bml != null ? bml : new BML(target);
                    output.Content = ForWriting(document, defaultNamespace, true);
                    if (!output.Save())
                        throw new McpError("Could not write " + Path.GetFileName(target) + (targets.Count > 1 ? " (files before it in " + string.Join(", ", relatives) + " were written)" : "") + ".");
                }
                else
                    ForWriting(document, defaultNamespace, false).Save(target);
            }
            result["written"] = new JArray(relatives.Select(o => "DATA/" + o));
            return result;
        }

        private static void SetAttributes(XmlElement element, JToken attributes, string where)
        {
            if (attributes == null || attributes.Type == JTokenType.Null) return;
            if (!(attributes is JObject map)) throw new McpError(where + ": attributes is {name: value}.");
            foreach (JProperty property in map.Properties())
            {
                try { XmlConvert.VerifyName(property.Name); } catch { throw new McpError(where + ": '" + property.Name + "' is not a valid attribute name."); }
                element.SetAttribute(property.Name, TokenText(property.Value, where + " attribute " + property.Name));
            }
        }
        #endregion

        #region Reset
        private static object ResetConfigsTool(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            List<string> groups = call.StrList("groups");
            List<string> files = call.StrList("files").Select(Relative).ToList();
            PAK2 backups = ConfigReset.LoadBackups();

            Func<string, JToken> state = file =>
            {
                byte[] vanilla = ConfigReset.Vanilla(backups, file);
                string full = DataPath(file);
                if (vanilla == null) return "no vanilla copy";
                if (!File.Exists(full)) return "missing";
                byte[] now = File.ReadAllBytes(full);
                return now.Length == vanilla.Length && now.SequenceEqual(vanilla) ? "vanilla" : "modified";
            };

            if (groups.Count == 0 && files.Count == 0)
            {
                HashSet<string> grouped = new HashSet<string>(ConfigReset.Groups.SelectMany(o => o.Files.Concat(o.OptionalFiles)), StringComparer.OrdinalIgnoreCase);
                Func<IEnumerable<string>, JObject> summary = list =>
                {
                    List<string> all = list.ToList();
                    JObject entry = new JObject() { ["files"] = all.Count };
                    JObject differing = new JObject(all.Select(f => new JProperty(f, state(f))).Where(p => (string)p.Value != "vanilla"));
                    entry["not_vanilla"] = differing;
                    return entry;
                };
                return new JObject()
                {
                    ["groups"] = new JArray(ConfigReset.Groups.Select(g =>
                    {
                        JObject entry = summary(ConfigReset.FilesToReset(g));
                        entry.AddFirst(new JProperty("label", g.Label));
                        entry.AddFirst(new JProperty("group", g.Key));
                        return entry;
                    })),
                    ["other_backed_up_files"] = summary(backups.Entries.Where(o => !grouped.Contains(o.Filename)).Select(o => o.Filename)),
                    ["about"] = "not_vanilla lists files that differ from (or are missing against) OpenCAGE's vanilla copy. Reset with groups or files; dry_run lists every file first.",
                };
            }

            List<string> selected = new List<string>();
            bool behaviourTrees = false;
            foreach (string key in groups)
            {
                ConfigReset.Group group = ConfigReset.Groups.FirstOrDefault(o => string.Equals(o.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
                if (group == null)
                    throw new McpError("There is no config group '" + key + "'. Groups: " + string.Join(", ", ConfigReset.Groups.Select(o => o.Key)) + ".");
                if (group.Key == ConfigReset.BehaviourTrees) behaviourTrees = true;
                selected.AddRange(ConfigReset.FilesToReset(group));
            }
            foreach (string file in files)
            {
                PAK2.File entry = backups.Entries.FirstOrDefault(o => string.Equals(o.Filename, file, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                    throw new McpError("OpenCAGE's config backups have no vanilla copy of '" + file + "'." + Suggest(backups.Entries.Select(o => o.Filename), file));
                //A group's file is reset the way its group resets it
                ConfigReset.Group owner = ConfigReset.Groups.FirstOrDefault(o => o.Files.Concat(o.OptionalFiles).Contains(entry.Filename, StringComparer.OrdinalIgnoreCase));
                if (owner != null && owner.OptionalFiles.Contains(entry.Filename, StringComparer.OrdinalIgnoreCase) && !File.Exists(DataPath(entry.Filename)))
                    throw new McpError("This install has no DATA/" + entry.Filename + " (only some installs do), so resetting it would add a file the game never had here. The " + owner.Key + " group resets the files this install has.");
                if (owner?.Key == ConfigReset.BehaviourTrees) behaviourTrees = true;
                selected.Add(entry.Filename);
            }
            selected = selected.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            JObject before = new JObject(selected.Select(o => new JProperty(o, state(o))));
            JObject result = new JObject()
            {
                ["files"] = before,
                ["changes"] = selected.Count(o => (string)before[o] != "vanilla"),
                ["dry_run"] = dryRun,
            };
            if (behaviourTrees)
                call.Note("Resetting behaviour_trees (BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML) also deletes DATA/BEHAVIOR and writes every tree's .xml again from the vanilla list.");

            //As Revert Configs, every config editor window is closed (Singleton.OnResetConfigs), whatever file it holds - so one
            //that only writes on its Save button, and may hold unsaved edits, has to be closed by the user first
            List<Form> closing = (Singleton.OnResetConfigs?.GetInvocationList() ?? new Delegate[0]).Select(o => o.Target).OfType<Form>().Where(o => !o.IsDisposed).Distinct().ToList();
            foreach (Form form in closing.Where(o => SaveButtonEditors.Contains(o.GetType())))
            {
                string message = "OpenCAGE's '" + form.Text + "' window is open, with any edits not yet saved there, and resetting configs closes every configuration editor window. Close it (saving it if it has changes worth keeping), then try again.";
                if (!dryRun) throw new McpError(message + " Nothing was changed.");
                call.Note(message);
            }
            if (!BeginWrite(call, dryRun, new string[0], behaviourTrees ? new List<string>(ConfigReset.BehaviourTreeProcesses) : null))
            {
                foreach (Form form in closing.Where(o => !SaveButtonEditors.Contains(o.GetType())))
                    call.Note("OpenCAGE's '" + form.Text + "' window would be closed, so it cannot save its copy back over vanilla (its edits are already saved).");
                return result;
            }

            //Every config editor window closes first, so none saves its copy back over vanilla
            List<string> closed = closing.Select(o => o.Text).ToList();
            Singleton.OnResetConfigs?.Invoke();
            foreach (string title in closed)
                call.Note("Closed OpenCAGE's '" + title + "' window, so it cannot save its copy back over vanilla.");
            foreach (string file in selected)
                ConfigReset.ResetFile(backups, file);
            if (behaviourTrees)
                result["behaviour_trees_regenerated"] = ConfigReset.RegenerateBehaviourTrees();
            if (selected.Any(o => o.Equals("LEVEL_TEXT_DATABASES.XML", StringComparison.OrdinalIgnoreCase)))
                call.Note("Levels' own TEXT/TEXT_DB_LIST.TXT files were left alone (they are level data: restore_backup brings a level's back).");
            result["written"] = selected.Count;
            return result;
        }
        #endregion
    }
}
