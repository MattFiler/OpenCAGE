using CATHODE;
using CATHODE.Scripting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCAGE.Hud;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// The HUD Overlay Builder's tools: hud_overlay (a level's own HUD overlays - list, example, get, set, import, remove, fix_paths)
    /// and check_ui_movie (check any Scaleform movie against what the game calls on it). Registered from McpEditorTools.Tools().
    /// </summary>
    internal static class McpHudTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "hud_overlay",
                Title = "HUD overlay builder",
                Description = "A level's own HUD overlay (the HUD Overlay Builder): custom HUD elements - text with lines to pick from, numbers, timers (m:ss), bars, reticles, panels - placed on a 1280x720 design space (scaled to the screen), " +
                    "made into a Scaleform movie kept in the level's folder (DATA/ENV/<level>/UI/OVERLAYS/<NAME>.GFX - a mod carries it as a level file) and loaded by the level's own script straight from there: nothing global (UI.PAK, AI.exe) is written, and it works however the game is started. " +
                    "set builds the movie, CHECKS it (the file, every class, every method's code: a movie the game can't make its root object from would crash it on the first call, as a class with two members of one name does), writes it, and makes the composite HUD\\<Name> as one undo step: " +
                    "a FlashScript loading it on the game's ChallengeMapOverlay layer (the only FlashScript layer the game loads a movie on straight away; so one overlay per level, refused beside a challenge map's own overlay; hidden while paused), show / hide pins, a float input pin per value slot (link any number to it on the instance), Show/Hide <slot> pins when asked, built-in sources (player_health: CHR_GetHealth on the player; count_up: seconds from the level start, paused with the game), " +
                    "and the design's hide_ammo_counter / hide_health_bar (the game's own weapon HUD parts kept off every 0.5 s). Values reach the movie every 0.1 s as FlashInvoke hudSet(slot, value); hudShow(slot, on) shows or hides a slot (-1: all). " +
                    "An instance goes in the level's root the first time. Save the level afterwards (a plain save is enough). Remaking keeps the pins still wanted. " +
                    "example gives a design to start from (its JSON is what set takes); get returns a level overlay's design (it is kept inside the movie); import uses a movie made elsewhere (checked against the calls the level makes on its layer; refused movies are never written); " +
                    "remove takes the movie and the composite's contents out; fix_paths points the FlashScripts at this level's folder after the level was copied or renamed. list (default) shows the level's overlays, HUD composites and their loaders.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "list (default), example, get, set, import, remove or fix_paths.", options: new[] { "list", "example", "get", "set", "import", "remove", "fix_paths" }),
                    McpSchema.Any("design", "set: the design (an object, or its JSON as a string) - {name, show_at_start, hide_ammo_counter, hide_health_bar, challenge_map_layer (load it on the game's ChallengeMapOverlay layer instead of one of its own), slots: [{name, kind (Text, Number, Timer, Bar, Reticle, Box, Image), x, y, width, height, colour '#RRGGBB', alpha, visible, show_hide_pins, source (Pin, PlayerHealth, CountUp, None), initial, font ($JixellationBold, $JixellationReg, $Isolation, $JectRexBold, $NostromoBoldCond), size, align, shadow, lines, prefix, suffix, decimals, tenths, max (0: the largest value seen), direction (right/left/up/down), back_colour, back_alpha, low_colour, low_at, border_colour, border_width, style (cross/circle/dot/brackets), thickness, gap, image_file (Image: an absolute path to a PNG up to 1024x1024, read into the design)}]}. Field names as example returns them (PascalCase or snake_case both read)."),
                    McpSchema.String("name", "get / import / remove: the overlay's name (letters, digits, _)."),
                    McpSchema.String("file", "import: an absolute path to the .gfx / .swf to use."),
                    McpSchema.Boolean("make_script", "import: also make the HUD\\<name> composite with the FlashScript and show / hide pins (default true)."),
                    McpSchema.Boolean("dry_run", "set / import: build and check only - nothing is written."),
                    ShowProp()),
                Destructive = true,
                Run = Run,
            };

            yield return new McpTool()
            {
                Name = "check_ui_movie",
                Title = "Check a UI movie",
                Description = "Check a Scaleform movie (.gfx / .swf) the way the HUD Overlay Builder checks its own before the game is given one: the file and its tags, the ActionScript block's tables, every class's members (two with one name make the virtual machine refuse the class), every method's code (instructions, branches, stack and scope depths on every path, locals). " +
                    "A movie with faults gives the game no root object, and the game crashes on its first call to it. Then the calls the game will make on its root: 'Init' (any FlashScript layer), the level's FlashInvokes on 'layer' when a level is open, the ChallengeMapOverlay layer's own calls: a missing method does nothing (a warning, not a crash). " +
                    "Give 'file', or 'level' with 'overlay' (one of the level's overlays) or 'movie' (one of the game's UI movies: the level's own version, else the game's from UI.PAK). Reads only.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("file", "An absolute path to a .gfx / .swf."),
                    McpSchema.String("level", "The level (defaults to the open one)."),
                    McpSchema.String("overlay", "One of the level's HUD overlays, by name."),
                    McpSchema.String("movie", "One of the game's UI movies (RADIALMENU, WEAPONSTUFF...): the level's own version when it has one, else the game's."),
                    McpSchema.String("layer", "The layer the movie is loaded into (FlashScript layer_name), to check the level's FlashInvokes on it.")),
                ReadOnly = true,
                Idempotent = true,
                Run = Check,
            };
        }

        private static McpSchema.Prop ShowProp() => McpSchema.Boolean("show", "set / import: take the editor to the HUD composite afterwards (default false).");

        private static string OpenLevel()
        {
            string level = McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name);
            if (level == null)
                throw McpError.Invalid("No level is open: open the level the overlay is for.");
            return level;
        }

        /* A design from the call: an object or a JSON string, with PascalCase or snake_case names */
        internal static HudDesign ReadDesign(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                throw McpError.Invalid("'design' is required: hud_overlay {\"action\":\"example\"} gives one to start from.");
            JToken parsed = token.Type == JTokenType.String ? JToken.Parse((string)token) : token;
            try
            {
                return HudDesign.FromJson(Pascal(parsed).ToString(Formatting.None));
            }
            catch (Exception e) when (e is JsonException || e is FormatException)
            {
                throw McpError.Invalid("'design' can't be read: " + e.Message);
            }
        }

        private static JToken Pascal(JToken token)
        {
            if (token is JObject o)
            {
                JObject result = new JObject();
                foreach (JProperty p in o.Properties())
                {
                    string name = string.Concat(p.Name.Split('_').Where(s => s.Length != 0).Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));
                    result[name] = Pascal(p.Value);
                }
                return result;
            }
            if (token is JArray a)
                return new JArray(a.Select(Pascal));
            return token;
        }

        private static JObject Report(MovieCheckResult check)
        {
            JObject result = new JObject()
            {
                ["ok"] = check.Ok,
                ["summary"] = check.Summary(),
                ["root_class"] = check.DocumentClass,
                ["root_methods"] = new JArray(check.RootMethods.Where(o => !o.Name.StartsWith("oc", StringComparison.Ordinal)).Select(o => o.ToString())),
            };
            if (check.Errors.Count != 0) result["errors"] = new JArray(check.Errors.Take(30));
            if (check.Warnings.Count != 0) result["warnings"] = new JArray(check.Warnings.Take(30));
            return result;
        }

        private static object Run(McpCall call)
        {
            string action = (call.Str("action") ?? "list").Trim().ToLowerInvariant();
            string pathToAI = Singleton.PathToAI;
            switch (action)
            {
                case "example":
                    return new JObject() { ["design"] = JObject.Parse(HudDesign.Example(call.Str("name") ?? "Hud").ToJson()) };
                case "get":
                    {
                        string level = OpenLevelOr(call);
                        string name = call.Str("name", required: true);
                        LevelOverlay overlay = HudOverlays.Of(pathToAI, level).FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
                        if (overlay == null)
                            throw McpError.Invalid(level + " has no overlay called '" + name + "' (list shows them).");
                        if (overlay.Design == null)
                            throw McpError.Invalid(overlay.Name + " is a movie made elsewhere (imported): it holds no design.");
                        return new JObject() { ["level"] = level, ["file"] = overlay.File, ["design"] = JObject.Parse(overlay.Design.ToJson()) };
                    }
                case "set": return Set(call, pathToAI);
                case "import": return Import(call, pathToAI);
                case "remove": return Remove(call, pathToAI);
                case "fix_paths":
                    {
                        string level = OpenLevel();
                        Composite root = McpEditor.UI(() => McpEditor.RequireCommands().EntryPoints[0]);
                        int fixedCount = 0;
                        McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Point HUD overlays at " + level, root, edit => fixedCount = HudScript.FixPaths(edit, pathToAI, level), new McpScriptEdit.RunOptions() { Show = false });
                        foreach (string note in outcome.Notes) call.Note(note);
                        return new JObject() { ["level"] = level, ["fixed"] = fixedCount };
                    }
                default:
                    return List(call, pathToAI);
            }
        }

        private static string OpenLevelOr(McpCall call)
        {
            string given = call.Str("level");
            return given != null ? McpLevels.Resolve(given, call: call) : OpenLevel();
        }

        private static object List(McpCall call, string pathToAI)
        {
            string level = OpenLevel();
            JObject result = new JObject() { ["level"] = level, ["folder"] = HudOverlays.Folder(pathToAI, level) };
            JArray overlays = new JArray();
            List<OverlayLoader> loaders = McpEditor.UI(() => HudOverlays.Loaders(McpEditor.RequireCommands(false), pathToAI, level));
            foreach (LevelOverlay overlay in HudOverlays.Of(pathToAI, level))
            {
                byte[] bytes = File.ReadAllBytes(overlay.File);
                string layer = overlay.Design?.LayerName ?? HudOverlays.LayerName(overlay.Name);
                List<MovieCall> calls = McpEditor.UI(() => HudOverlays.CallsOnLayer(McpEditor.RequireCommands(false), layer));
                MovieCheckResult check = HudMovieCheck.Check(bytes, new[] { new MovieCall() { Method = HudMovieBuilder.InitMethod, Arguments = 1, From = "The game" } }.Concat(calls), layer);
                JObject row = new JObject()
                {
                    ["name"] = overlay.Name,
                    ["file"] = overlay.File,
                    ["size"] = overlay.Length,
                    ["made_here"] = overlay.Design != null,
                    ["layer"] = layer,
                    ["game_path"] = HudOverlays.GamePath(level, overlay.Name),
                    ["check"] = Report(check),
                    ["loaded_by"] = new JArray(loaders.Where(o => string.Equals(Path.GetFileNameWithoutExtension(o.Filename), overlay.Name, StringComparison.OrdinalIgnoreCase)).Select(o => o.Composite.name + "\\" + o.EntityName)),
                };
                if (overlay.Design != null)
                    row["slots"] = new JArray(overlay.Design.Slots.Select(o => o.Name + " (" + o.Kind + ")"));
                overlays.Add(row);
            }
            result["overlays"] = overlays;
            JArray loaderRows = new JArray(loaders.Select(o => new JObject()
            {
                ["flashscript"] = o.Composite.name + "\\" + o.EntityName,
                ["filename"] = o.Filename,
                ["layer"] = o.LayerName,
                ["path_right"] = o.PathRight,
                ["file_there"] = o.FileThere,
            }));
            result["loaders"] = loaderRows;
            if (loaders.Any(o => !o.PathRight))
                call.Note("A FlashScript points at another level's overlay folder (the level was copied or renamed?): hud_overlay fix_paths points it here.");
            return result;
        }

        private static object Set(McpCall call, string pathToAI)
        {
            string level = OpenLevel();
            HudDesign design = ReadDesign(call.Args["design"]);
            List<string> problems = design.Problems();
            if (problems.Count != 0)
                throw McpError.Invalid("The design can't be built: " + string.Join(" ", problems));
            string clash = McpEditor.UI(() => HudScript.Clash(McpEditor.RequireCommands(false), design.Name));
            if (clash != null)
                throw new McpError(McpErrorCodes.Refused, clash + " Nothing was written.");
            List<MovieCall> calls = McpEditor.UI(() => HudOverlays.CallsOnLayer(McpEditor.RequireCommands(false), design.LayerName));
            byte[] movie = HudMovieBuilder.Build(design, out MovieCheckResult check);
            check = HudOverlays.Recheck(movie, design, calls.Where(o => !o.From.Contains(" in " + HudScript.CompositePath(design.Name))));
            JObject result = new JObject()
            {
                ["level"] = level,
                ["name"] = design.Name,
                ["layer"] = design.LayerName,
                ["game_path"] = HudOverlays.GamePath(level, design.Name),
                ["file"] = HudOverlays.FileOf(pathToAI, level, design.Name),
                ["movie_bytes"] = movie.Length,
                ["check"] = Report(check),
            };
            if (!check.Ok)
                throw new McpError(McpErrorCodes.Refused, "The overlay's movie was refused by the check, so nothing was written: " + string.Join(" ", check.Errors));
            if (call.Bool("dry_run"))
            {
                result["dry_run"] = true;
                result["pins"] = new JArray(HudScript.Pins(design).Select(o => o.Key));
                return result;
            }

            string problem = HudOverlays.Write(pathToAI, level, design, out _);
            if (problem != null)
                throw new McpError(McpErrorCodes.Refused, "The overlay's movie was not written: " + problem);
            ApplyScript(call, design, level, result);
            return result;
        }

        private static void ApplyScript(McpCall call, HudDesign design, string level, JObject result)
        {
            Composite root = McpEditor.UI(() => McpEditor.RequireCommands().EntryPoints[0]);
            HudScript.Made made = null;
            int repointed = 0;
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("HUD overlay " + design.Name, root, edit => { made = HudScript.Apply(edit, design, level); repointed = HudScript.FixPaths(edit, Singleton.PathToAI, level); },
                new McpScriptEdit.RunOptions() { Show = call.Bool("show") });
            foreach (string note in outcome.Notes.Concat(made?.Notes ?? new List<string>())) call.Note(note);
            if (made != null)
            {
                result["composite"] = made.Composite.name;
                result["composite_made"] = made.CreatedComposite;
                result["instance"] = made.InstanceIn.name + "\\" + McpEditor.UI(() => McpEditor.RequireCommands(false).Utils.GetEntityName(made.InstanceIn, made.Instance));
                result["instance_placed"] = made.PlacedInstance;
                result["entities"] = made.Entities;
                result["pins"] = new JArray(HudScript.Pins(design).Select(o => o.Key));
                if (made.PinsAdded.Count != 0) result["pins_added"] = new JArray(made.PinsAdded);
                if (made.PinsRemoved.Count != 0) result["pins_removed"] = new JArray(made.PinsRemoved);
            }
            if (repointed != 0)
                call.Note(repointed + " FlashScript(s) that loaded an overlay from another level's folder (a copied level) now load this level's.");
            call.Note("Save the level for the game to run it (a plain save is enough; nothing here needs a build).");
        }

        private static object Import(McpCall call, string pathToAI)
        {
            string level = OpenLevel();
            string name = call.Str("name", required: true).Trim();
            string file = call.Str("file", required: true);
            if (!File.Exists(file))
                throw McpError.Invalid("There is no file " + file + ".");
            byte[] bytes = File.ReadAllBytes(file);
            string layer = HudOverlays.LayerName(name);
            List<MovieCall> calls = McpEditor.UI(() => HudOverlays.CallsOnLayer(McpEditor.RequireCommands(false), layer));
            JObject result = new JObject() { ["level"] = level, ["name"] = name, ["layer"] = layer, ["file"] = HudOverlays.FileOf(pathToAI, level, name) };
            //The movie first (a movie that would crash the game is the thing to hear about), then the level's one overlay layer
            MovieCheckResult first = HudMovieCheck.Check(bytes, new[] { new MovieCall() { Method = HudMovieBuilder.InitMethod, Arguments = 1, From = "The game" } }.Concat(calls), layer);
            if (call.Bool("dry_run"))
            {
                result["dry_run"] = true;
                result["check"] = Report(first);
                return result;
            }
            if (!first.Ok)
                throw new McpError(McpErrorCodes.Refused, "Not imported: the movie was refused, so nothing was written: " + string.Join(" ", first.Errors) + " (" + level + "'s overlay folder is unchanged).");
            string clash = call.Bool("make_script", true) ? McpEditor.UI(() => HudScript.Clash(McpEditor.RequireCommands(false), name)) : null;
            if (clash != null)
                throw new McpError(McpErrorCodes.Refused, clash + " Nothing was written.");
            string problem = HudOverlays.Import(pathToAI, level, name, bytes, calls, out MovieCheckResult check);
            result["check"] = Report(check);
            if (problem != null)
                throw new McpError(McpErrorCodes.Refused, "Not imported: " + problem + " (" + level + "'s overlay folder is unchanged).");
            if (call.Bool("make_script", true))
                ApplyScript(call, new HudDesign() { Name = name, ShowAtStart = true }, level, result);
            return result;
        }

        private static object Remove(McpCall call, string pathToAI)
        {
            string level = OpenLevel();
            string name = call.Str("name", required: true).Trim();
            Composite root = McpEditor.UI(() => McpEditor.RequireCommands().EntryPoints[0]);
            int instances = 0;
            McpScriptEdit.Run("Remove HUD overlay " + name, root, edit => instances = HudScript.Remove(edit, name), new McpScriptEdit.RunOptions() { Show = false });
            string problem = HudOverlays.Remove(pathToAI, level, name);
            if (problem != null)
                call.Note("The movie could not be removed: " + problem);
            return new JObject() { ["level"] = level, ["name"] = name, ["instances_removed"] = instances, ["movie_removed"] = problem == null };
        }

        private static object Check(McpCall call)
        {
            string pathToAI = Singleton.PathToAI;
            byte[] bytes;
            string what;
            string layer = call.Str("layer");
            if (call.Has("file"))
            {
                what = call.Str("file");
                if (!File.Exists(what)) throw McpError.Invalid("There is no file " + what + ".");
                bytes = File.ReadAllBytes(what);
            }
            else
            {
                string level = OpenLevelOr(call);
                if (call.Has("overlay"))
                {
                    what = HudOverlays.FileOf(pathToAI, level, call.Str("overlay"));
                    if (!File.Exists(what)) throw McpError.Invalid(level + " has no overlay called '" + call.Str("overlay") + "'.");
                    bytes = File.ReadAllBytes(what);
                    layer = layer ?? HudOverlays.LayerName(call.Str("overlay"));
                }
                else if (call.Has("movie"))
                {
                    LevelUiOverrides.Movie movie = LevelUiOverrides.Find(call.Str("movie"));
                    if (movie == null) throw McpError.Invalid("'" + call.Str("movie") + "' is not one of the game's UI movies (list_ui_overrides lists them).");
                    if (LevelUiOverrides.Of(pathToAI, level).TryGetValue(movie, out string own))
                    {
                        what = own;
                        bytes = File.ReadAllBytes(own);
                    }
                    else
                    {
                        what = "UI.PAK " + movie.OriginalEntry;
                        bytes = LevelUiOverrides.GameVersion(pathToAI, movie);
                        if (bytes == null) throw McpError.Invalid("UI.PAK has no " + movie.OriginalEntry + ".");
                    }
                }
                else
                    throw McpError.Invalid("Give 'file', or 'overlay' or 'movie' (with 'level').");
            }
            List<MovieCall> calls = new List<MovieCall>();
            if (layer != null)
            {
                calls.Add(new MovieCall() { Method = HudMovieBuilder.InitMethod, Arguments = 1, From = "The game (as the layer loads)", Engine = true });
                if (McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level) != null)
                    calls.AddRange(McpEditor.UI(() => HudOverlays.CallsOnLayer(McpEditor.RequireCommands(false), layer)));
            }
            MovieCheckResult check = HudMovieCheck.Check(bytes, calls, layer);
            JObject result = Report(check);
            result["movie"] = what;
            result["bytes"] = bytes.Length;
            result["overlay_made_by_opencage"] = check.OverlayDesign != null;
            return result;
        }
    }
}
