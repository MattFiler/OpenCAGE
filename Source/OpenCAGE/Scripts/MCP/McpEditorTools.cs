using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Backups;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.MCP
{
    /// <summary>The editor and its levels: what is open, opening, creating, saving, launching, undo, backups.</summary>
    internal static class McpEditorTools
    {
        //A campaign level with a viewport takes a minute or two to open; a Save & Build a few minutes
        private static readonly TimeSpan LoadTimeout = TimeSpan.FromMinutes(10);

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "get_editor_state",
                Title = "Get editor state",
                Description = "What OpenCAGE has open right now: the level (unsaved changes, compressed script), the composite on screen (and the instance path it was opened through), what is selected, whether the 3D viewport is connected, what undo/redo would do, the status bar and any running save or backup. Call this first.",
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() => State()),
            };

            yield return new McpTool()
            {
                Name = "list_levels",
                Title = "List levels",
                Description = "Every level OpenCAGE can open, from the game install. Campaign levels are named like 'PRODUCTION/SCI_ANDROIDLAB'; levels made with create_level have a plain name.",
                InputSchema = McpSchema.Object(McpSchema.String("filter", "Only levels whose name contains this.")),
                ReadOnly = true,
                Idempotent = true,
                Run = call =>
                {
                    string filter = call.Str("filter");
                    string open = McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name);
                    List<string> levels = EditorUtils.GetEditableLevels().Where(o => filter == null || o.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    return new JObject()
                    {
                        ["open_level"] = open,
                        ["levels"] = new JArray(levels.Select(o => new JObject() { ["name"] = o, ["custom"] = !o.Contains("/") })),
                    };
                },
            };

            yield return new McpTool()
            {
                Name = "load_level",
                Title = "Open level",
                Description = "Open a level in the editor (and its viewport), waiting until it is ready. Unsaved changes to the level open now are lost unless you save_level first; this refuses if there are any, unless discard_unsaved is true.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level, as list_levels names it (e.g. 'PRODUCTION/SCI_ANDROIDLAB').", required: true),
                    McpSchema.Boolean("discard_unsaved", "Open it even though the level open now has unsaved changes, losing them.")),
                Destructive = true,
                Run = LoadLevel,
            };

            yield return new McpTool()
            {
                Name = "create_level",
                Title = "Create level",
                Description = "Make a new level and open it (File > Create Level): GLOBAL, the pause menu, required assets and an empty root. 'imports' ports composites from other levels in as it is made. Written to DATA/ENV at once, and the game's level list (DATA/PACKAGES/MAIN.PKG) is updated to include it; not undoable. Refuses while the game runs unless close_game. Then place things with create_entities and save_level with build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "Letters, digits and underscores, up to 32 characters. It becomes the level's folder name (upper case).", required: true),
                    McpSchema.Array("imports", "Composites to port in while creating it, per source level, e.g. [{\"level\": \"PRODUCTION/SCI_ANDROIDLAB\", \"composites\": [\"<path or id>\"]}].",
                        McpSchema.Object(
                            McpSchema.String("level", "The source level, as list_levels names it.", required: true),
                            McpSchema.Strings("composites", "Composite paths or ids in that level.", required: true))),
                    McpSchema.Boolean("include_children", "With imports: also bring the composites they place (default true - they need them to work)."),
                    McpSchema.Boolean("overwrite_assets", "With imports: let imported models/textures/materials replace same-named ones (default false)."),
                    McpSchema.Boolean("discard_unsaved", "Open it even though the level open now has unsaved changes, losing them."),
                    McpSchema.Boolean("close_game", "If Alien: Isolation is running, close it first, as File > Create Level does (progress in it is lost). Without this the call is refused while it runs.")),
                Run = CreateLevel,
            };

            yield return new McpTool()
            {
                Name = "save_level",
                Title = "Save level",
                Description = "Write the open level to the game install. build=true is Save & Build: it also rebuilds what the game derives from the script (instancing, radiosity, navmesh, cover, sound...), needed before changes work in game; 'bakers' can skip slow ones. Takes seconds to minutes. Closes the game if running. Not undoable.",
                InputSchema = McpSchema.Object(
                    McpSchema.Boolean("build", "Save & Build (default false: a plain save)."),
                    McpSchema.Nested("bakers", "With build: which bakers run (default all). Skipping radiosity CLEARS the baked lighting - on a level still carrying its original lighting that loses it for good, as later bakes then regenerate it from scratch; skipped navmesh/cover/jobs/sound keep their old data.", BakersSchema)),
                Run = SaveLevel,
            };

            yield return new McpTool()
            {
                Name = "launch_game",
                Title = "Launch the game",
                Description = "Start Alien: Isolation straight into a level (Steam, Epic or GOG). Closes the game and Cinematic Tools first if running. Installs or removes the scripting-helpers ASI and starts Cinematic Tools as launch_options say. Saves nothing: save_level (build=true after script changes) first. Without a level it starts at the menu.",
                InputSchema = McpSchema.Object(McpSchema.String("level", "The level to start in, as list_levels names it. Defaults to the open level; 'menu' for the main menu.")),
                Destructive = true,
                Run = LaunchGame,
            };

            yield return new McpTool()
            {
                Name = "undo",
                Title = "Undo",
                Description = "Undo the last change(s) made in the editor, by the user or by these tools. Every change to the open level's script is one step: entities, parameters, links, trigger sequences, CAGEAnimations, entity resources, flowgraph pages and nodes, composites (create, rename, move, delete, de-instance, group). Asset and game-file changes (imports, models, materials, textures, ports, packages, configs, text, animations, sounds) are not: each tool's description says which it is.",
                InputSchema = McpSchema.Object(McpSchema.Integer("steps", "How many steps (default 1).")),
                Run = call => Step(call, undo: true),
            };

            yield return new McpTool()
            {
                Name = "redo",
                Title = "Redo",
                Description = "Redo what undo took back.",
                InputSchema = McpSchema.Object(McpSchema.Integer("steps", "How many steps (default 1).")),
                Run = call => Step(call, undo: false),
            };

            yield return new McpTool()
            {
                Name = "open_composite",
                Title = "Open composite",
                Description = "Show a composite in the editor (entity list, flowgraph pages, viewport), optionally selecting entities in it. With 'path' it is opened in place through instances, from the level root or from 'composite', keeping the breadcrumb and where it sits in the level (as Step Into Composite does).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite's path or id ('root' for the level's root). With 'path': where the path starts (default 'root')."),
                    PathProp("the composite the last one places is opened"),
                    McpSchema.Strings("select", "Entities in it to select, by id or name.")),
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    Commands commands = RequireCommandsForShowing();
                    Composite composite = ResolveShown(call, commands, out Composite entry, out List<Entity> instances);
                    List<Entity> select = call.StrList("select").Select(o => McpScript.FindEntity(commands, composite, o)).ToList();
                    ShowAtPath(commands, entry, instances, composite, select);
                    JObject result = new JObject() { ["opened"] = McpScript.CompositeSummary(commands, composite), ["selected"] = select.Count };
                    if (instances.Count != 0) result["path"] = DescribeInstancePath(commands, entry, instances);
                    return result;
                }),
            };

            yield return new McpTool()
            {
                Name = "select_entities",
                Title = "Select entities",
                Description = "Select entities in a composite (opening it), so the user sees them in the inspector and viewport. focus=true also moves the viewport camera to them. With 'path' the composite is opened in place through instances from the level root (or from 'composite'), so nested entities are framed where they sit in the level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite's path or id. With 'path': where the path starts (default 'root')."),
                    PathProp("the entities are in the composite the last one places"),
                    McpSchema.Strings("entities", "Entities to select, by id or name.", required: true),
                    McpSchema.Boolean("focus", "Move the viewport camera to the selection.")),
                Idempotent = true,
                Run = call =>
                {
                    JObject result = McpEditor.UI(() =>
                    {
                        Commands commands = RequireCommandsForShowing();
                        Composite composite = ResolveShown(call, commands, out Composite entry, out List<Entity> instances);
                        List<Entity> select = call.StrList("entities", required: true).Select(o => McpScript.FindEntity(commands, composite, o)).ToList();
                        ShowAtPath(commands, entry, instances, composite, select);
                        JObject selected = new JObject() { ["selected"] = select.Count, ["composite"] = McpScript.CompositeSummary(commands, composite) };
                        if (instances.Count != 0) selected["path"] = DescribeInstancePath(commands, entry, instances);
                        return selected;
                    });
                    if (call.Bool("focus"))
                    {
                        Thread.Sleep(500); //let the selection reach the viewer first
                        McpEditor.UI(() => Send.SendViewportAction(ViewportAction.FocusOnSelection));
                    }
                    return result;
                },
            };

            yield return new McpTool()
            {
                Name = "list_backups",
                Title = "List level backups",
                Description = "The backups OpenCAGE's backup manager holds for a level, newest first, each with how many files it changed from the one before (as Manage Backups shows). pending_changes also counts what differs on disk now from the newest backup.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level (defaults to the open one)."),
                    McpSchema.Boolean("pending_changes", "Also count files changed since the newest backup. Hashes the whole level folder: seconds to a minute on campaign levels."),
                    McpSchema.Integer("limit", "At most this many backups (default 50).")),
                ReadOnly = true,
                Idempotent = true,
                Run = call =>
                {
                    string level = LevelArgument(call);
                    int limit = Math.Max(1, call.Int("limit", 50));
                    AlienLevel backups = OpenBackups(level);
                    List<AlienLevel.AlienBackup> all = backups.Backups;
                    JArray listed = new JArray();
                    for (int i = all.Count - 1; i >= 0 && listed.Count < limit; i--)
                    {
                        AlienLevel.AlienBackup backup = all[i];
                        listed.Add(new JObject()
                        {
                            ["id"] = backup.ID,
                            ["name"] = backup.Name,
                            ["date"] = backup.Date,
                            ["files"] = backup.GUIDs?.Count ?? 0,
                            ["changed_files"] = i == 0 ? (backup.GUIDs?.Count ?? 0) : backups.CalculateDiff(all[i - 1], backup),
                        });
                    }
                    JObject result = new JObject() { ["level"] = level, ["total"] = all.Count, ["backups"] = listed };
                    if (call.Bool("pending_changes"))
                    {
                        using (Heartbeat(call, "Comparing " + level + " with its newest backup"))
                        {
                            try { result["pending_changes"] = backups.CalculateDiff(all.Count == 0 ? null : all[all.Count - 1]); }
                            catch (DirectoryNotFoundException) { throw new McpError(level + "'s folder is missing, so there is nothing on disk to compare."); }
                        }
                    }
                    return result;
                },
            };

            yield return new McpTool()
            {
                Name = "create_backup",
                Title = "Back up a level",
                Description = "Back up a level's files as they are on disk now (unsaved changes are not included - save_level first). all_levels backs up every editable level (Backup All Levels Now), which takes a long time. Written to DATA/MODTOOLS/BACKUPS at once.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "A name for the backup (required unless all_levels, which names them as the backup manager does)."),
                    McpSchema.String("level", "The level (defaults to the open one)."),
                    McpSchema.Boolean("all_levels", "Back up every editable level instead of one.")),
                Run = call => call.Bool("all_levels") ? BackupAllLevels(call) : BackupOneLevel(call),
            };

            yield return new McpTool()
            {
                Name = "restore_backup",
                Title = "Restore a level backup",
                Description = "Put a level's files back as a backup holds them, replacing what is on disk at once (closes the game; not undoable). If the level is open it is reopened, losing unsaved changes. A restore that fails part way puts the folder back as it was when it can, and says so.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("id", "The backup's id, from list_backups.", required: true),
                    McpSchema.String("level", "The level (defaults to the open one).")),
                Destructive = true,
                Run = call =>
                {
                    string level = LevelArgument(call);
                    long id = BackupId(call.Token("id") ?? throw new McpError("'id' is required."), "id");
                    AlienLevel backups = OpenBackups(level);
                    if (!backups.Backups.Any(o => o.ID == id))
                        throw new McpError("There is no backup " + id + " of " + level + " (list_backups shows them).");
                    //Nothing may save into the folder while it is deleted and rewritten: hold the guards SaveLevel respects
                    McpEditor.UI(() =>
                    {
                        if (Singleton.CurrentBackupState != Singleton.BackupState.NONE || UndoStack.Current.Blocked)
                            throw new McpError("A save or backup is running.");
                        //The loader reads a level's folder, which the restore deletes and rewrites
                        if (Singleton.Editor.IsLevelLoadInProgress || Singleton.Editor.IsLevelLoaderRunning)
                            throw new McpError("A level is loading. Wait for it (get_editor_state shows when it is ready), then try again.");
                        Singleton.CurrentBackupState = Singleton.BackupState.SINGLE_LEVEL;
                        Singleton.BackupLevel = level;
                        UndoStack.Current.Blocked = true;
                        Singleton.Editor.Enabled = false;
                    });
                    bool restored;
                    try
                    {
                        EditorUtils.CloseAI();
                        call.Progress("Restoring " + level);
                        //A level whose folder is gone altogether can still be put back: the restore first copies the folder aside
                        string folder = Path.Combine(Singleton.PathToAI, "DATA", "ENV", level);
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);
                        using (Heartbeat(call, "Restoring " + level))
                            restored = backups.RestoreBackup(id);
                    }
                    finally
                    {
                        McpEditor.UI(() =>
                        {
                            Singleton.CurrentBackupState = Singleton.BackupState.NONE;
                            Singleton.BackupLevel = "";
                            UndoStack.Current.Blocked = false;
                            Singleton.Editor.Enabled = true;
                        });
                    }
                    if (!restored)
                    {
                        switch (backups.LastRestoreFailure)
                        {
                            case AlienLevel.RestoreFailure.Untouched:
                                throw new McpError("Nothing was restored and " + level + "'s files were not changed: they could not be copied aside first (close anything holding them open, then try again).");
                            case AlienLevel.RestoreFailure.RolledBack:
                                throw new McpError("The restore failed part way (a file in " + level + " may be held open), and the level's folder was put back as it was. Close anything using its files and try again.");
                            case AlienLevel.RestoreFailure.CopyLeftBehind:
                                call.Note("The backup is restored, but the safety copy taken before it could not be removed: delete " + Path.GetFullPath(backups.SafetyCopyFolder) + " by hand.");
                                break;
                            default:
                                throw new McpError("The restore failed part way and " + level + "'s folder could not be put back: it may be incomplete. The files as they were before the restore are kept at " + Path.GetFullPath(backups.SafetyCopyFolder) + ". Try restoring a backup again, or verify the game files.");
                        }
                    }
                    bool isOpen = McpEditor.UI(() => string.Equals(Singleton.Editor.CompositeBrowser?.Content?.Level?.Name, level, StringComparison.OrdinalIgnoreCase));
                    if (isOpen)
                        OpenLevel(call, level);
                    return new JObject() { ["restored"] = level, ["backup"] = id, ["reopened"] = isOpen };
                },
            };

            yield return new McpTool()
            {
                Name = "delete_backups",
                Title = "Delete level backups",
                Description = "Permanently delete backups of a level (Manage Backups > Delete Checked Backups): their entries go, and stored file revisions no other backup uses are deleted from DATA/MODTOOLS/BACKUPS. Cannot be undone. list_backups gives the ids.",
                InputSchema = McpSchema.Object(
                    McpSchema.Array("ids", "The backups' ids, from list_backups.", new JObject() { ["type"] = "integer" }, required: true),
                    McpSchema.String("level", "The level (defaults to the open one).")),
                Destructive = true,
                Run = DeleteBackups,
            };
        }

        /// <summary>The 'path' argument open_composite and select_entities share.</summary>
        private static McpSchema.Prop PathProp(string end) =>
            McpSchema.Strings("path", "Instance entities (names or ids) to step through from 'composite' or the root; " + end + ".");

        //save_level's 'bakers' object
        private static readonly string[] BakerNames = { "navmesh", "cover", "radiosity", "job_positions", "alphalight", "sound" };
        private static readonly JObject BakersSchema = McpSchema.Object(
            McpSchema.Boolean("navmesh", "Rebuild the AI navigation mesh."),
            McpSchema.Boolean("cover", "Rebuild AI cover."),
            McpSchema.Boolean("radiosity", "Rebuild baked lighting. false CLEARS it: static geometry is unlit until a build with it."),
            McpSchema.Boolean("job_positions", "Rebuild AI job positions (spotting, crawl, assault)."),
            McpSchema.Boolean("alphalight", "Rebuild the alphalight atlas."),
            McpSchema.Boolean("sound", "Rebuild the sound node networks."));

        #region State
        private static JObject State()
        {
            CommandsEditor editor = McpEditor.Editor;
            LevelContent content = editor.CompositeBrowser?.Content;
            JObject state = new JObject()
            {
                ["opencage_version"] = Singleton.Version,
                ["game_path"] = Singleton.PathToAI,
                ["platform"] = Singleton.Platform.ToString(),
                ["loading"] = editor.IsLevelLoadInProgress,
                ["saving"] = UndoStack.Current.Blocked,
                ["viewport"] = new JObject() { ["enabled"] = Singleton.ViewportEnabled, ["connected"] = Send.Connected, ["ready"] = Send.Connected && ViewerResourceSync.ViewerReady && ViewerPopulateSync.ActivePopulateToken == 0 },
            };
            if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                state["backup_running"] = Singleton.CurrentBackupState == Singleton.BackupState.ALL_LEVELS ? "all levels" : Singleton.BackupLevel;
            //What the status bar says: loading/saving steps, undo messages, background work such as the shader database harvest
            string status = StatusText(editor);
            if (!string.IsNullOrWhiteSpace(status))
                state["status_text"] = status;

            if (content?.Level != null && content.IsLevelDataLoaded)
            {
                Commands commands = content.Level.Commands;
                state["level"] = new JObject()
                {
                    ["name"] = content.Level.Name,
                    ["unsaved_changes"] = DirtyTracker.IsDirty,
                    ["root_composite"] = commands.EntryPoints[0]?.name,
                    ["composites"] = commands.Entries.Count,
                    ["created_level"] = !content.Level.Name.Contains("/"),
                    //Options > Misc > Write Compressed: a compressed (Switch/mobile style) script is written back compressed
                    ["compressed"] = commands.Compressed,
                };

                CompositeDisplay display = editor.CompositeDisplay;
                if (display != null && display.Populated && display.Composite != null)
                {
                    JObject shown = McpScript.CompositeSummary(commands, display.Composite);
                    shown["script_view"] = display.SupportsFlowgraphs ? "pages" : "links";
                    shown["entities"] = display.Composite.GetEntities().Count;
                    //Opened in place through instances (Step Into Composite, or a 'path'): the breadcrumb from the entry
                    List<Composite> pathComposites = display.Path?.AllComposites;
                    List<Entity> pathEntities = display.Path?.AllEntities;
                    if (pathComposites != null && pathEntities != null && pathEntities.Count != 0 && pathComposites.Count != 0)
                        shown["instance_path"] = DescribeInstancePath(commands, pathComposites[0], pathEntities);
                    state["composite_on_screen"] = shown;

                    EntityInspector inspector = display.EntityDisplay;
                    List<Entity> selected = inspector?.MultiSelectedEntities?.Count > 1 ? inspector.MultiSelectedEntities.ToList() : inspector?.Entity != null ? new List<Entity>() { inspector.Entity } : new List<Entity>();
                    state["selection"] = new JArray(selected.Where(o => o != null).Take(50).Select(o => McpScript.Brief(commands, display.Composite, o)));
                }
            }
            else
                state["level"] = null;

            state["undo"] = new JObject()
            {
                ["can_undo"] = UndoStack.Current.CanUndo,
                ["undo"] = UndoStack.Current.UndoLabel,
                ["can_redo"] = UndoStack.Current.CanRedo,
                ["redo"] = UndoStack.Current.RedoLabel,
            };
            return state;
        }
        #endregion

        #region Levels
        private static object LoadLevel(McpCall call)
        {
            string level = NormaliseLevel(call.Str("level", required: true));
            if (EditorUtils.IsFrontend(level) || EditorUtils.IsFrontend("PRODUCTION/" + level))
                throw new McpError("FRONTEND is the game's menu level and cannot be edited. create_level makes a new level from it.");
            List<string> levels = EditorUtils.GetEditableLevels();
            string match = levels.FirstOrDefault(o => string.Equals(o, level, StringComparison.OrdinalIgnoreCase))
                ?? levels.FirstOrDefault(o => o.EndsWith("/" + level, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                throw new McpError("There is no level '" + level + "'. list_levels shows them.");
            CheckUnsaved(call);
            OpenLevel(call, match);
            return McpEditor.UI(() => State());
        }

        private static void CheckUnsaved(McpCall call)
        {
            bool dirty = McpEditor.UI(() => Singleton.Editor.CompositeBrowser?.Content?.Level != null && DirtyTracker.IsDirty);
            if (dirty && !call.Bool("discard_unsaved"))
                throw new McpError("The open level has unsaved changes. save_level first, or pass discard_unsaved: true to lose them.");
            McpEditor.UI(() =>
            {
                if (UndoStack.Current.Blocked) throw new McpError("OpenCAGE is saving the level.");
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE) throw new McpError("A level backup is running.");
                if (Singleton.Editor.IsLevelLoadInProgress) throw new McpError("A level is already loading.");
            });
        }

        /// <summary>Open a level and wait until it is ready to edit.</summary>
        private static void OpenLevel(McpCall call, string level)
        {
            McpEditor.UI(() =>
            {
                //Opening a level while it is open still reloads it: the "discard" path relies on that
                Singleton.Editor.CloseLevelPicker();
                Singleton.Editor.LoadLevel(level);
            });
            call.Progress("Opening " + level);
            Thread.Sleep(500);
            //Either it opens, or the loader gives up: its thread done, nothing loaded - and still so a moment later, since the
            //viewer finishing a populate of the previous level can clear the progress flag while the load runs
            bool Failed() => !Singleton.Editor.IsLevelLoadInProgress && !Singleton.Editor.IsLevelLoaderRunning && Singleton.Editor.CompositeBrowser?.Content?.IsLevelDataLoaded != true;
            Stopwatch failing = null;
            bool FailedForAWhile()
            {
                if (!Failed()) { failing = null; return false; }
                if (failing == null) failing = Stopwatch.StartNew();
                return failing.Elapsed > TimeSpan.FromSeconds(3);
            }
            bool ended = McpEditor.WaitFor(call, () => McpEditor.IsReady(level) || FailedForAWhile(), LoadTimeout, "Opening " + level);
            if (!ended || !McpEditor.UI(() => McpEditor.IsReady(level)))
            {
                bool failed = McpEditor.UI(() => Failed());
                if (failed)
                {
                    McpEditor.UI(() => Singleton.Editor.EnableButtons(true, ""));
                    throw new McpError(level + " failed to load. Its files may be damaged - verify the game files, or restore a backup.");
                }
                throw new McpError(level + " is taking a long time to open; it is still loading. Check get_editor_state in a moment.");
            }
            //Let the panels finish settling (tree population, first page) before anything reads them
            Thread.Sleep(500);
            McpEditor.UI(() => { });
        }

        private static object CreateLevel(McpCall call)
        {
            string name = call.Str("name", required: true).Trim();
            int maxLength = Math.Min(32, PatchManager.MaxLaunchMapNameLength);
            if (!Regex.IsMatch(name, "^[A-Za-z0-9_]+$"))
                throw new McpError("Level names can only contain letters, digits and underscores.");
            if (name.Length > maxLength)
                throw new McpError("Level names can be at most " + maxLength + " characters.");
            string levelId = name.ToUpperInvariant();
            string path = Singleton.PathToAI + "/DATA/ENV/" + levelId;
            if (Directory.Exists(path) || Level.GetLevels(Singleton.PathToAI).Contains(levelId))
                throw new McpError("A level called '" + levelId + "' already exists.");
            if (!Level.GetLevels(Singleton.PathToAI).Any(o => EditorUtils.IsFrontend(o)))
                throw new McpError("New levels are made from PRODUCTION/FRONTEND, which is missing from this install. Verify the game files.");
            //What to port in, checked against each source level's composite index before anything is written
            CompositeSelection imports = ReadImports(call);
            bool closeGame = call.Bool("close_game");
            CheckUnsaved(call);

            //It writes the whole level and the game's level list, and the game holds its files open: closed only when asked
            if (McpLevelTools.ProcessRunning("AI"))
            {
                if (!closeGame)
                    throw new McpError("Alien: Isolation is running, and a level can't be created while it is. Close it, or pass close_game: true to have OpenCAGE close it (progress in it is lost). Nothing was written.");
                EditorUtils.CloseAI();
                call.Note("The running game was closed first.");
            }
            bool listed = false;
            McpEditor.UI(() =>
            {
                UndoStack.Current.Blocked = true;
                Singleton.Editor.Enabled = false;
            });
            int ported;
            CompositeImporter.Result imported = null;
            try
            {
                call.Progress("Loading PRODUCTION/FRONTEND, the base for new levels");
                Level baseLevel = new Level(Singleton.PathToAI + "/DATA/ENV/" + EditorUtils.FrontendLevel, Singleton.Global, false);
                baseLevel.Load();
                call.ThrowIfCancelled();

                //From here the folder is being written (MakeNewLevelFrom saves the base level into it): a cancellation is
                //not taken, so the level is finished rather than left without its imports, pages and level-list entry
                call.Progress("Creating " + levelId);
                Level newLevel = Level.MakeNewLevelFrom(path, baseLevel);
                baseLevel = null;

                //Pages for everything the new level holds, as Create Level gives them: imported composites bring
                //their source level's pages, and what is still without one takes the bundled predefined pages
                CompositeFlowgraphTable layouts = (CompositeFlowgraphTable)CustomTable.ReadTable(newLevel.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS) ?? new CompositeFlowgraphTable();
                if (!imports.IsEmpty)
                {
                    //The importer loads each source level and ports from it behind its own progress windows, which are the UI thread's
                    call.Progress("Importing " + imports.Summary());
                    using (Heartbeat(call, "Importing " + imports.CompositeCount + " composite(s) into " + levelId))
                    {
                        imported = McpEditor.UI(() => CompositeImporter.Import(imports, newLevel, (composite, pages) =>
                        {
                            layouts.flowgraphs.RemoveAll(o => o.CompositeGUID == composite.shortGUID);
                            layouts.flowgraphs.AddRange(pages);
                        }));
                    }
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                }
                foreach (Composite composite in newLevel.Commands.Entries)
                {
                    if (layouts.flowgraphs.Any(o => o.CompositeGUID == composite.shortGUID)) continue;
                    layouts.flowgraphs.AddRange(McpEditor.UI(() => FlowgraphLayoutManager.GetLayoutsForPort(composite, null, EditorUtils.FrontendLevel)));
                }
                ported = newLevel.Commands.Entries.Count;
                call.Progress("Saving " + levelId);
                newLevel.Save();
                CustomTable.WriteTable(newLevel.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS, layouts);
                listed = PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI);
                newLevel = null;
            }
            catch (Exception e)
            {
                //Before MakeNewLevelFrom nothing is written; after it, whatever stopped it, the folder it left blocks the name
                if (!Directory.Exists(path) && (e is McpError || e is OperationCanceledException))
                    throw;
                string left = Directory.Exists(path) ? " A partly written folder was left at " + Path.GetFullPath(path) + ": it must be deleted before the name can be used again." : "";
                throw new McpError((e is OperationCanceledException ? "Creating " + levelId + " was cancelled." : e is McpError ? e.Message : "Creating " + levelId + " failed: " + e.Message) + left);
            }
            finally
            {
                McpEditor.UI(() =>
                {
                    UndoStack.Current.Blocked = false;
                    Singleton.Editor.Enabled = true;
                });
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            }

            if (!listed)
                call.Note("The game's level list (DATA/PACKAGES/MAIN.PKG) could not be updated to include " + levelId + "; launch_game updates it before starting the game.");
            //The level exists now whatever happens next: a refusal has to say so, or a retry would meet "already exists"
            try { OpenLevel(call, levelId); }
            catch (Exception e) when (e is McpError || e is OperationCanceledException)
            {
                throw new McpError(levelId + " was created (" + Path.GetFullPath(path) + ") but " + (e is OperationCanceledException
                    ? "the call was cancelled before it was opened: load_level opens it." : "opening it did not finish: " + e.Message));
            }
            JObject state = McpEditor.UI(() => State());
            JObject created = new JObject() { ["level"] = levelId, ["folder"] = Path.GetFullPath(path), ["composites"] = ported };
            if (imported != null)
            {
                JObject importedSummary = new JObject()
                {
                    ["from"] = new JArray(imports.Levels.Select(o => o.Level)),
                    ["ported_count"] = imported.Ported.Count,
                    ["ported"] = new JArray(imported.Ported.Take(100).Select(o => o.name)),
                    ["models"] = imported.Renderables,
                    ["collision"] = imported.CollisionMappings,
                    ["physics_systems"] = imported.PhysicsSystems,
                    ["animated_models"] = imported.AnimatedModels,
                };
                if (imported.DeadProxies.Any) importedSummary["dead_proxies"] = imported.DeadProxies.Describe(levelId);
                created["imported"] = importedSummary;
                call.Note("Imported composites are not placed anywhere yet: add instances in 'root' with create_entities, then save_level with build=true.");
            }
            else
                call.Note("The level has no geometry or player yet. Typical next steps: port_composites from a campaign level (e.g. the player spawn 'Archetypes\\Script\\Mission\\SpawnPositionSelect' with 'DisplayModel:RIPLEY_FP', and some geometry), create_entities in 'root' to place them, then save_level with build=true.");
            state["created"] = created;
            return state;
        }

        private static object SaveLevel(McpCall call)
        {
            bool build = call.Bool("build");
            LevelContent.BakeSelection bakers = ReadBakers(call);
            if (bakers != null && !build)
                throw new McpError("'bakers' chooses what a build rebuilds: pass build: true with it (a plain save runs no bakers).");
            string level = McpEditor.UI(() => McpEditor.RequireLevel().Level.Name);

            bool saved = false;
            Action onSaved = () => saved = true;
            Singleton.OnSaved += onSaved;
            Stopwatch timer = Stopwatch.StartNew();
            Exception failure = null;
            using (Heartbeat(call, build ? "Saving and building " + level : "Saving " + level))
            {
                try
                {
                    McpEditor.UI(() =>
                    {
                        LevelContent content = McpEditor.RequireLevel();
                        //The instanced save takes this as it starts (LevelContent.Save); cleared after in case the save never began
                        content.NextSaveBakers = bakers;
                        try
                        {
                            Singleton.Editor.SaveLevel(build, successMsg: false, allowLaunchGame: false);
                        }
                        catch (Exception e)
                        {
                            failure = e;
                            //What a failed save leaves behind: its progress window, status text and cursor
                            foreach (Form progress in Application.OpenForms.OfType<ProgressUI>().ToList())
                                try { progress.Close(); } catch { }
                            Singleton.Editor.EnableButtons(true, "");
                            Cursor.Current = Cursors.Default;
                        }
                        finally
                        {
                            content.NextSaveBakers = null;
                        }
                    });
                }
                finally
                {
                    Singleton.OnSaved -= onSaved;
                }
            }

            if (failure != null)
                throw new McpError("Saving " + level + " failed: " + failure.Message + ". Nothing may have been written, or only part of the level; the level is still open with its changes.");
            if (!saved)
                throw new McpError("OpenCAGE did not save " + level + " (a backup, or another save, may be running).");

            JObject result = new JObject() { ["saved"] = level, ["build"] = build, ["seconds"] = Math.Round(timer.Elapsed.TotalSeconds, 1) };
            if (build)
            {
                LevelContent.BakeSelection ran = bakers ?? new LevelContent.BakeSelection();
                result["bakers"] = new JObject()
                {
                    ["navmesh"] = ran.NavMesh,
                    ["cover"] = ran.Cover,
                    ["radiosity"] = ran.Radiosity,
                    ["job_positions"] = ran.JobPositions,
                    ["alphalight"] = ran.Alphalight,
                    ["sound"] = ran.SoundNetworks,
                };
                if (!ran.Radiosity)
                    call.Note("Radiosity was skipped, so the level's baked lighting files were CLEARED: static geometry renders without it until a build with radiosity.");
                List<string> stale = new List<string>();
                if (!ran.NavMesh) stale.Add("navmesh");
                if (!ran.Cover) stale.Add("cover");
                if (!ran.JobPositions) stale.Add("job positions");
                if (!ran.SoundNetworks) stale.Add("sound networks");
                if (!ran.Alphalight) stale.Add("alphalight");
                if (stale.Count != 0)
                    call.Note("Not rebuilt, so kept as they were on disk (they may no longer match moved or added geometry): " + string.Join(", ", stale) + ".");
            }
            McpEditor.UI(() =>
            {
                LevelContent content = Singleton.Editor.CompositeBrowser?.Content;
                if (content?.LastBakeWarnings?.Count > 0) result["build_warnings"] = new JArray(content.LastBakeWarnings);
                if (content?.LastWarnings?.Count > 0) result["warnings"] = new JArray(content.LastWarnings);
            });
            if (!build)
                call.Note("A plain save keeps the script and assets but does not rebuild the runtime data; after script changes, save with build=true before playing.");
            return result;
        }

        private static object LaunchGame(McpCall call)
        {
            PatchManager.Platform platform = Singleton.Platform;
            if (platform != PatchManager.Platform.STEAM && platform != PatchManager.Platform.EPIC_GAMES_STORE && platform != PatchManager.Platform.GOG)
                throw new McpError("OpenCAGE can only launch the game for Steam, Epic and GOG installs (this is " + platform + ").");
            string level = call.Has("level") ? NormaliseLevel(call.Str("level")) : McpEditor.UI(() => Singleton.Editor.CompositeBrowser?.Content?.Level?.Name);
            if (level == null || level == "MENU" || EditorUtils.IsFrontend(level) || EditorUtils.IsFrontend("PRODUCTION/" + level))
                level = EditorUtils.FrontendLevel;
            else
            {
                //The name goes into the game's executable: it has to be a level that exists, spelled as the game does
                List<string> levels = EditorUtils.GetEditableLevels();
                string wanted = level;
                level = levels.FirstOrDefault(o => string.Equals(o, wanted, StringComparison.OrdinalIgnoreCase))
                    ?? levels.FirstOrDefault(o => o.EndsWith("/" + wanted, StringComparison.OrdinalIgnoreCase))
                    ?? throw new McpError("There is no level '" + call.Str("level") + "' to launch into (list_levels shows them).");
            }
            if (level.Length > PatchManager.MaxLaunchMapNameLength)
                throw new McpError("'" + level + "' is too long to launch into (the most is " + PatchManager.MaxLaunchMapNameLength + " characters).");
            //Launching patches AI.exe and the level list packages, which a save (its release-build tail) and a level
            //creation or restore also write: never alongside one of those
            bool dirty = McpEditor.UI(() =>
            {
                if (UndoStack.Current.Blocked)
                    throw new McpError("OpenCAGE is saving or writing a level. Launch when it has finished (get_editor_state shows 'saving').");
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                    throw new McpError("A level backup is running. Launch when it has finished.");
                if (Singleton.Editor.IsLevelLoadInProgress)
                    throw new McpError("A level is still loading. Launch when it is open.");
                return Singleton.Editor.CompositeBrowser?.Content?.Level?.Name == level && DirtyTracker.IsDirty;
            });
            if (dirty)
                call.Note(level + " has unsaved changes in the editor, which the game will not see.");

            JObject result = new JObject();
            List<string> closed = McpLevelTools.CloseGame();
            if (closed.Count != 0)
            {
                result["closed"] = new JArray(closed);
                call.Note("The game was already running (" + string.Join(", ", closed) + ") and was closed first.");
            }
            PatchManager.PerformRecommendedPatches(platform, Singleton.PathToAI);
            //The scripting helpers (runtime utils ASI) go in or come out as launch_options ask, as the Launch Game window does
            string runtimeUtilsProblem = global::OpenCAGE.LaunchGame.ApplyRuntimeUtils();
            if (runtimeUtilsProblem != null)
                call.Note(runtimeUtilsProblem + " In-game scripting helpers (debug text, hot reload) will not work this run.");
            bool patched = PatchManager.PatchLaunchMode(platform, Singleton.PathToAI, level)
                         & PatchManager.PatchFileIntegrityCheck(platform, Singleton.PathToAI)
                         & PatchManager.PatchPopupMessage(platform, Singleton.PathToAI);
            if (!patched)
                call.Note("Some launch settings could not be written into AI.exe; the game may start at its menu.");
            PatchManager.UpdateLevelListInPackages(platform, Singleton.PathToAI);
            if (platform == PatchManager.Platform.STEAM)
                Process.Start("steam://rungameid/214490");
            else
                Process.Start(new ProcessStartInfo() { FileName = Singleton.PathToAI + "/AI.exe", WorkingDirectory = Singleton.PathToAI });

            result["launched"] = true;
            result["level"] = EditorUtils.IsFrontend(level) ? "menu" : level;
            result["scripting_helpers"] = global::OpenCAGE.LaunchGame.RuntimeUtilsNeeded() && runtimeUtilsProblem == null;
            if (SettingsManager.GetBool(Settings.CinematicTools))
            {
                if (!global::OpenCAGE.LaunchGame.CinematicToolsAvailable())
                {
                    result["cinematic_tools"] = "not available";
                    call.Note("Cinematic Tools are switched on in launch_options but only work with the Steam build and a complete OpenCAGE install; they were not started.");
                }
                else
                {
                    string problem = global::OpenCAGE.LaunchGame.StartCinematicTools(out string caption);
                    result["cinematic_tools"] = problem == null ? "started" : "failed";
                    if (problem != null)
                        call.Note(caption + ": " + problem.Replace("\n\n", " ").Replace("\n", " "));
                }
            }
            return result;
        }

        private static string NormaliseLevel(string level) => (level ?? "").Replace('\\', '/').Trim().Trim('/').ToUpperInvariant();

        /// <summary>The level a backup tool is about, named as load_level takes it (full name, or its last part).</summary>
        internal static string LevelArgument(McpCall call)
        {
            string level = call.Has("level") ? NormaliseLevel(call.Str("level")) : McpEditor.UI(() => Singleton.Editor.CompositeBrowser?.Content?.Level?.Name);
            if (string.IsNullOrEmpty(level))
                throw new McpError("Say which level (no level is open).");
            List<string> levels = EditorUtils.GetEditableLevels();
            string match = levels.FirstOrDefault(o => string.Equals(o, level, StringComparison.OrdinalIgnoreCase))
                ?? levels.FirstOrDefault(o => o.EndsWith("/" + level, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;

            //A level a failed restore left without its files is no longer listed, but its backups are still there.
            //Backups live under the full name; the old external tool kept campaign levels' under the short name alone.
            string env = Path.Combine(Singleton.PathToAI, "DATA", "ENV");
            string backups = Path.Combine(Singleton.PathToAI, "DATA", "MODTOOLS", "BACKUPS");
            string shortName = level.StartsWith("PRODUCTION/", StringComparison.OrdinalIgnoreCase) ? level.Substring("PRODUCTION/".Length) : level;
            bool HasBackups(string name) => File.Exists(Path.Combine(backups, name + ".BAK"));
            List<string> candidates = level.Contains("/") ? new List<string>() { level } : new List<string>() { "PRODUCTION/" + level, level };
            //One whose folder is still there (a restore recreates it, possibly empty)
            foreach (string candidate in candidates)
                if (Directory.Exists(Path.Combine(env, candidate)) && (HasBackups(candidate) || (candidate.StartsWith("PRODUCTION/") && HasBackups(shortName))))
                    return candidate;
            //Then by where the backups are: a legacy short-name archive belongs to the campaign level of that name
            foreach (string candidate in candidates)
                if (HasBackups(candidate) && candidate.Contains("/"))
                    return candidate;
            if (HasBackups(shortName))
                return "PRODUCTION/" + shortName;
            throw new McpError("There is no level '" + level + "' (list_levels shows them), and no backups under that name.");
        }

        /// <summary>A level's backup archive, read without writing anything.</summary>
        private static AlienLevel OpenBackups(string level)
        {
            try { return new AlienLevel(level); }
            catch (Exception e) { throw new McpError(level + "'s backup archive could not be read (" + e.Message + ")."); }
        }

        /// <summary>A backup id (Unix seconds) from a number or a numeric string.</summary>
        private static long BackupId(JToken token, string name)
        {
            if (token.Type == JTokenType.Integer)
                return (long)token;
            if (token.Type == JTokenType.Float && Math.Abs((double)token - Math.Round((double)token)) < 1e-9 && Math.Abs((double)token) < 9e15)
                return (long)Math.Round((double)token);
            if (token.Type == JTokenType.String && long.TryParse(((string)token).Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long parsed))
                return parsed;
            throw new McpError("'" + name + "' must be a backup id: the whole number list_backups gives (got " + token.ToString(Newtonsoft.Json.Formatting.None) + ").");
        }

        private static object BackupOneLevel(McpCall call)
        {
            string level = LevelArgument(call);
            string name = call.Str("name");
            if (string.IsNullOrWhiteSpace(name))
                throw new McpError("'name' is required: a name for the backup (or pass all_levels: true to back up every level).");
            if (!Directory.Exists(Path.Combine(Singleton.PathToAI, "DATA", "ENV", level)))
                throw new McpError(level + "'s folder is missing, so there is nothing to back up. restore_backup can put a backup back.");
            McpEditor.UI(() =>
            {
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                    throw new McpError("A backup is already running.");
                if (UndoStack.Current.Blocked)
                    throw new McpError("The level is being saved.");
                Singleton.CurrentBackupState = Singleton.BackupState.SINGLE_LEVEL;
                Singleton.BackupLevel = level;
            });
            AlienLevel.AlienBackup made;
            try
            {
                if (McpEditor.UI(() => string.Equals(Singleton.Editor.CompositeBrowser?.Content?.Level?.Name, level, StringComparison.OrdinalIgnoreCase) && DirtyTracker.IsDirty))
                    call.Note("The open level has unsaved changes, which are not in this backup.");
                call.Progress("Backing up " + level);
                using (Heartbeat(call, "Backing up " + level))
                {
                    AlienLevel archive = OpenBackups(level);
                    archive.CreateBackup(name);
                    made = archive.Backups.LastOrDefault();
                }
            }
            finally
            {
                McpEditor.UI(() => { Singleton.CurrentBackupState = Singleton.BackupState.NONE; Singleton.BackupLevel = ""; });
            }
            return new JObject() { ["level"] = level, ["backup"] = made == null ? null : new JObject() { ["id"] = made.ID, ["name"] = made.Name, ["date"] = made.Date, ["files"] = made.GUIDs?.Count ?? 0 } };
        }

        /// <summary>Backup All Levels Now: every editable level in turn, under the all-levels guard that holds off every save.</summary>
        private static object BackupAllLevels(McpCall call)
        {
            if (call.Has("level"))
                throw new McpError("Give 'level' or all_levels, not both.");
            string name = call.Str("name");
            List<string> levels = EditorUtils.GetEditableLevels();
            McpEditor.UI(() =>
            {
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                    throw new McpError("A backup is already running.");
                if (UndoStack.Current.Blocked)
                    throw new McpError("A level is being saved.");
                Singleton.CurrentBackupState = Singleton.BackupState.ALL_LEVELS;
                Singleton.BackupLevel = "";
            });
            JArray made = new JArray();
            JArray failed = new JArray();
            bool cancelled = false;
            try
            {
                if (McpEditor.UI(() => Singleton.Editor.CompositeBrowser?.Content?.Level != null && DirtyTracker.IsDirty))
                    call.Note("The open level has unsaved changes, which are not in its backup.");
                for (int i = 0; i < levels.Count; i++)
                {
                    if (call.Cancel.IsCancellationRequested) { cancelled = true; break; }
                    string level = levels[i];
                    call.Progress("Backing up " + level + " (" + (i + 1) + " of " + levels.Count + ")", i, levels.Count);
                    try
                    {
                        using (Heartbeat(call, "Backing up " + level + " (" + (i + 1) + " of " + levels.Count + ")"))
                        {
                            AlienLevel archive = new AlienLevel(level);
                            archive.CreateBackup(!string.IsNullOrWhiteSpace(name) ? name : archive.Backups.Count == 0 ? "First backup" : "Automated backup across all levels");
                            AlienLevel.AlienBackup backup = archive.Backups.LastOrDefault();
                            made.Add(new JObject() { ["level"] = level, ["id"] = backup?.ID, ["name"] = backup?.Name });
                        }
                    }
                    catch (Exception e)
                    {
                        failed.Add(new JObject() { ["level"] = level, ["error"] = e.Message });
                    }
                }
            }
            finally
            {
                McpEditor.UI(() => { Singleton.CurrentBackupState = Singleton.BackupState.NONE; Singleton.BackupLevel = ""; });
            }
            JObject result = new JObject() { ["levels"] = levels.Count, ["backed_up"] = made.Count, ["backups"] = made };
            if (failed.Count != 0) result["failed"] = failed;
            if (cancelled) call.Note("Cancelled after " + made.Count + " of " + levels.Count + " levels; those backups are kept.");
            return result;
        }

        private static object DeleteBackups(McpCall call)
        {
            string level = LevelArgument(call);
            JArray given = call.Array("ids", required: true);
            if (given.Count == 0)
                throw new McpError("'ids' is empty: give the ids of the backups to delete (list_backups shows them).");
            List<long> ids = given.Select(o => BackupId(o, "ids")).Distinct().ToList();
            AlienLevel archive = OpenBackups(level);
            List<long> missing = ids.Where(id => !archive.Backups.Any(o => o.ID == id)).ToList();
            if (missing.Count != 0)
                throw new McpError("No backup " + string.Join(", ", missing) + " of " + level + " (list_backups shows them). Nothing was deleted.");
            McpEditor.UI(() =>
            {
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                    throw new McpError("A backup is running. Try again when it has finished.");
                Singleton.CurrentBackupState = Singleton.BackupState.SINGLE_LEVEL;
                Singleton.BackupLevel = level;
            });
            JArray deleted = new JArray();
            try
            {
                foreach (long id in ids)
                {
                    AlienLevel.AlienBackup backup = archive.Backups.First(o => o.ID == id);
                    archive.DeleteBackup(id);
                    deleted.Add(new JObject() { ["id"] = id, ["name"] = backup.Name, ["date"] = backup.Date });
                }
            }
            finally
            {
                McpEditor.UI(() => { Singleton.CurrentBackupState = Singleton.BackupState.NONE; Singleton.BackupLevel = ""; });
            }
            return new JObject() { ["level"] = level, ["deleted"] = deleted, ["remaining"] = archive.Backups.Count };
        }

        /// <summary>
        /// Where create_level's imports come from, checked against each source level's composite index (no
        /// level load) before anything is written: a CompositeSelection as the Create Level window builds one.
        /// </summary>
        private static CompositeSelection ReadImports(McpCall call)
        {
            CompositeSelection selection = new CompositeSelection()
            {
                IncludeChildren = call.Bool("include_children", true),
                OverwriteAssets = call.Bool("overwrite_assets"),
                //The new level holds only what every level shares (GLOBAL, the pause menu, required assets): keep those
                OverwriteComposites = false,
            };
            JArray items = call.Array("imports");
            if (items.Count == 0)
            {
                if (call.Has("include_children") || call.Has("overwrite_assets"))
                    throw new McpError("include_children and overwrite_assets only apply with 'imports'.");
                return selection;
            }
            foreach (JToken item in items)
            {
                if (!(item is JObject spec))
                    throw new McpError("Each import is an object: {\"level\": \"<level>\", \"composites\": [\"<path or id>\", ...]}.");
                List<string> unknown = spec.Properties().Select(o => o.Name).Where(o => o != "level" && o != "composites").ToList();
                if (unknown.Count != 0)
                    throw new McpError("An import has unknown key(s) " + string.Join(", ", unknown) + "; it takes 'level' and 'composites'.");
                string level = McpPortingTools.Normalise(spec["level"]?.Type == JTokenType.String ? (string)spec["level"] : throw new McpError("Each import needs 'level' (a level name, as list_levels gives it)."));
                JToken composites = spec["composites"];
                List<string> references = composites is JArray array ? array.Select(o => o.Type == JTokenType.String ? (string)o : o.ToString(Newtonsoft.Json.Formatting.None)).ToList()
                    : composites?.Type == JTokenType.String ? new List<string>() { (string)composites } : null;
                if (references == null || references.Count == 0)
                    throw new McpError("The import from " + level + " needs 'composites': their paths or ids there (search_level finds them).");

                List<CompositeIndexEntry> index;
                try { index = CompositeIndexCache.Get(level); }
                catch (Exception e) { throw new McpError(level + "'s script could not be read (" + e.Message + ")."); }
                CompositeSelection.LevelPick pick = selection.GetOrAdd(level);
                foreach (string reference in references)
                {
                    CompositeIndexEntry entry = FindIndexed(index, level, reference);
                    if (entry.ID == McpPortingTools.GlobalId || entry.ID == McpPortingTools.PauseMenuId)
                        throw new McpError(entry.Name + " is GLOBAL or PAUSEMENU, which the new level has its own copy of. Import the composites it places instead.");
                    pick.Composites[entry.ID] = entry.Name;
                }
            }
            return selection;
        }

        /// <summary>A composite in another level's index, by id, full path, or a path ending that picks out one.</summary>
        private static CompositeIndexEntry FindIndexed(List<CompositeIndexEntry> index, string level, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new McpError("An import from " + level + " names an empty composite.");
            ShortGuid? id = McpScript.ParseId(reference);
            if (id != null)
            {
                List<CompositeIndexEntry> byId = index.Where(o => o.ID == id.Value).ToList();
                if (byId.Count != 0) return byId[0];
            }
            string wanted = McpScript.NormalisePath(reference);
            List<CompositeIndexEntry> exact = index.Where(o => string.Equals(McpScript.NormalisePath(o.Name), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1) return exact[0];
            if (exact.Count > 1)
                throw new McpError("More than one composite in " + level + " is called '" + wanted + "': use an id (" + string.Join(", ", exact.Select(o => McpScript.Id(o.ID))) + ").");
            List<CompositeIndexEntry> ending = index.Where(o => McpScript.NormalisePath(o.Name).EndsWith("\\" + wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            if (ending.Count == 1) return ending[0];
            if (ending.Count > 1)
                throw new McpError("'" + wanted + "' in " + level + " could be any of: " + string.Join("; ", ending.Take(12).Select(o => o.Name)) + ". Give the full path.");
            List<CompositeIndexEntry> near = index.Where(o => (o.Name ?? "").IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0).Take(10).ToList();
            throw new McpError("No composite '" + wanted + "' in " + level + "." + (near.Count != 0 ? " Similar: " + string.Join("; ", near.Select(o => o.Name)) + "." : " search_level finds them."));
        }

        /// <summary>save_level's 'bakers', or null when it is absent or asks for every baker.</summary>
        private static LevelContent.BakeSelection ReadBakers(McpCall call)
        {
            JObject spec = call.Object("bakers");
            if (spec == null)
                return null;
            List<string> unknown = spec.Properties().Select(o => o.Name).Where(o => !BakerNames.Contains(o)).ToList();
            if (unknown.Count != 0)
                throw new McpError("'bakers' has unknown key(s) " + string.Join(", ", unknown.Select(o => "'" + o + "'")) + "; it takes " + string.Join(", ", BakerNames) + ".");
            bool Read(string key)
            {
                JToken token = spec[key];
                if (token == null || token.Type == JTokenType.Null) return true;
                if (token.Type == JTokenType.Boolean) return (bool)token;
                throw new McpError("bakers." + key + " must be true or false.");
            }
            LevelContent.BakeSelection bakers = new LevelContent.BakeSelection()
            {
                NavMesh = Read("navmesh"),
                Cover = Read("cover"),
                Radiosity = Read("radiosity"),
                JobPositions = Read("job_positions"),
                Alphalight = Read("alphalight"),
                SoundNetworks = Read("sound"),
            };
            return bakers.All ? null : bakers;
        }
        #endregion

        #region Instance paths
        /// <summary>Opening a composite rewrites its links (compile, first-open purge): never while a save is writing them out.</summary>
        private static Commands RequireCommandsForShowing()
        {
            Commands commands = McpEditor.RequireCommands(forEditing: false);
            if (UndoStack.Current.Blocked)
                throw new McpError("OpenCAGE is saving the level. Try again when it has finished (get_editor_state shows 'saving').");
            return commands;
        }

        /// <summary>
        /// The composite 'composite' and 'path' name. Without a path it is 'composite' itself; with one, the
        /// composite the path's last instance places, reached from 'composite' (default the root).
        /// </summary>
        private static Composite ResolveShown(McpCall call, Commands commands, out Composite entry, out List<Entity> instances)
        {
            List<string> steps = call.StrList("path");
            instances = new List<Entity>();
            if (steps.Count == 0)
            {
                if (!call.Has("composite"))
                    throw new McpError("Say which composite: 'composite' (its path or id; 'root' for the level's root), or a 'path' of instances to step through from the root.");
                entry = McpScript.FindComposite(commands, call.Str("composite", required: true));
                return entry;
            }
            entry = McpScript.FindComposite(commands, call.Str("composite") ?? "root");
            return ResolveInstancePath(commands, entry, steps, instances);
        }

        /// <summary>Step through instance entities from a composite: each step names an instance in the composite the previous one places.</summary>
        internal static Composite ResolveInstancePath(Commands commands, Composite entry, IList<string> steps, List<Entity> instances)
        {
            Composite current = entry;
            foreach (string step in steps)
            {
                Entity entity = McpScript.FindEntity(commands, current, step);
                Composite next = McpScript.InstancedComposite(commands, entity);
                if (next == null)
                    throw new McpError("'" + step + "' in " + current.name + " is " + McpScript.TypeName(commands, current, entity) + ", not a composite instance, so the path cannot step into it. Every step of 'path' must be an instance; name the entities to select separately.");
                instances.Add(entity);
                current = next;
            }
            return current;
        }

        /// <summary>
        /// Show the composite at the end of an instance path, drilled into from its entry the way the viewport's
        /// Step Into Composite and the search's Jump To do (so the breadcrumb and the viewport keep its place in the
        /// level), and select entities there. No path: the composite on its own. UI thread.
        /// </summary>
        internal static void ShowAtPath(Commands commands, Composite entry, List<Entity> instances, Composite target, List<Entity> select)
        {
            if (instances.Count == 0)
            {
                McpScriptEdit.Show(target, select);
                return;
            }
            CommandsEditor editor = McpEditor.Editor;
            CompositeDisplay display = editor.CompositeDisplay;
            if (display == null || display.IsDisposed || !OnDisplayedPath(display, entry))
                display = editor.CompositeBrowser?.LoadComposite(entry) ?? display;
            if (display == null || display.IsDisposed)
                throw new McpError("The editor's composite view is not open.");

            List<uint> guids = instances.Select(o => o.shortGUID.AsUInt32).ToList();
            Func<Entity, Composite> child = entity => McpScript.InstancedComposite(commands, entity);
            List<Entity> chosen = select.Where(o => o != null).Distinct().Take(64).ToList();
            bool opened;
            if (chosen.Count == 1)
            {
                guids.Add(chosen[0].shortGUID.AsUInt32);
                opened = display.ApplyViewerSelectionPath(entry, guids, true, child);
            }
            else
            {
                opened = display.ApplyViewerSelectionPath(entry, guids, false, child);
                if (opened && chosen.Count > 1)
                    McpScriptEdit.Show(target, chosen);
            }
            if (!opened || display.Composite != target)
                throw new McpError("OpenCAGE could not open " + target.name + " through that path.");
        }

        /// <summary>Whether the display is showing the entry composite, or something drilled into from it (GlobalEntitySearchHelper's rule).</summary>
        private static bool OnDisplayedPath(CompositeDisplay display, Composite entry)
        {
            if (!display.Populated) return false;
            if (display.Composite?.shortGUID == entry.shortGUID) return true;
            return display.Path?.AllComposites?.Any(o => o.shortGUID == entry.shortGUID) == true;
        }

        private static JArray DescribeInstancePath(Commands commands, Composite entry, IList<Entity> instances)
        {
            JArray steps = new JArray() { entry.name };
            Composite current = entry;
            foreach (Entity instance in instances)
            {
                if (current == null) break;
                steps.Add(McpScript.EntityName(commands, current, instance) + " (" + McpScript.Id(instance.shortGUID) + ")");
                current = McpScript.InstancedComposite(commands, instance);
            }
            return steps;
        }

        /// <summary>The main window's status bar text.</summary>
        private static string StatusText(CommandsEditor editor)
        {
            foreach (StatusStrip strip in editor.Controls.OfType<StatusStrip>())
                foreach (ToolStripItem item in strip.Items)
                    if (!string.IsNullOrWhiteSpace(item.Text))
                        return item.Text;
            return null;
        }
        #endregion

        #region Undo
        private static object Step(McpCall call, bool undo)
        {
            int steps = Math.Max(1, call.Int("steps", 1));
            List<string> done = new List<string>();
            for (int i = 0; i < steps; i++)
            {
                string label = McpEditor.UI(() =>
                {
                    McpEditor.RequireLevel();
                    //While an edit of the editor's own is open or applying, CanUndo/CanRedo read false: say so, not "nothing to undo"
                    McpEditor.RequireUndoIdle();
                    UndoStack stack = UndoStack.Current;
                    if (undo ? !stack.CanUndo : !stack.CanRedo)
                        return null;
                    string next = undo ? stack.UndoLabel : stack.RedoLabel;
                    bool hadOther = undo ? stack.CanRedo : stack.CanUndo;
                    if (undo) stack.Undo(); else stack.Redo();
                    //A step that fails clears the whole history instead of moving across to the other side
                    if (!(undo ? stack.CanRedo : stack.CanUndo) || (undo ? stack.RedoLabel : stack.UndoLabel) != next)
                        throw new McpError("Could not " + (undo ? "undo" : "redo") + " '" + next + "': OpenCAGE cleared its undo history (the level had changed in a way that step could not follow).");
                    return next;
                });
                if (label == null) break;
                done.Add(label);
                McpEditor.UI(() => { }); //let what the step queued run
            }
            if (done.Count == 0)
                throw new McpError("There is nothing to " + (undo ? "undo" : "redo") + ".");
            return new JObject() { [undo ? "undone" : "redone"] = new JArray(done), ["state"] = McpEditor.UI(() => State())["undo"] };
        }
        #endregion

        /// <summary>Progress messages every few seconds while a long call runs on the UI thread.</summary>
        public static IDisposable Heartbeat(McpCall call, string what)
        {
            Stopwatch timer = Stopwatch.StartNew();
            System.Threading.Timer ticker = new System.Threading.Timer(_ => call.Progress(what + " (" + (int)timer.Elapsed.TotalSeconds + " s)", timer.Elapsed.TotalSeconds), null, 0, 5000);
            return new Disposer(() => ticker.Dispose());
        }

        private sealed class Disposer : IDisposable
        {
            private Action _action;
            public Disposer(Action action) { _action = action; }
            public void Dispose() { _action?.Invoke(); _action = null; }
        }
    }
}
