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
                Description = "What OpenCAGE has open right now: the level (unsaved changes; build: whether it needs a Save & Build before playing, and why; when this session last saved and built it), the composite on screen (and the instance path it was opened through, as {from, path, ids} that 'path' arguments take back), what is selected, whether the 3D viewport is connected, what undo/redo would do, the status bar, any running save or backup, and the game: whether this install's Alien: Isolation is running (a save, restore or level write closes it), whether Live Link is connected and which level the game runs. Answered even while another call runs. Call this first.",
                ReadOnly = true,
                Idempotent = true,
                Concurrent = true,
                Run = call =>
                {
                    //Process and Live Link checks here, off the UI thread
                    JObject game = GameState(TimeSpan.FromSeconds(1.5));
                    JObject state = McpEditor.UI(() => State());
                    state["game"] = game;
                    return state;
                },
            };

            yield return new McpTool()
            {
                Name = "list_levels",
                Title = "List levels",
                Description = "Every level OpenCAGE can open, from the game install. Campaign levels are named like 'PRODUCTION/SCI_ANDROIDLAB'; levels made with create_level or duplicate_level have a plain name (custom: true). Tools take a level's full name, or its last part when only one level has it.",
                InputSchema = McpSchema.Object(McpSchema.String("filter", "Only levels whose name contains this (near names are suggested when none does).")),
                ReadOnly = true,
                Idempotent = true,
                Concurrent = true,
                Run = call =>
                {
                    string filter = call.Str("filter");
                    string open = McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name);
                    List<string> all = EditorUtils.GetEditableLevels().OrderBy(o => o.Contains("/") ? 1 : 0).ThenBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
                    List<string> levels = all.Where(o => filter == null || o.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    JObject result = new JObject()
                    {
                        ["open_level"] = open,
                        ["levels"] = new JArray(levels.Select(o => new JObject() { ["name"] = o, ["custom"] = !o.Contains("/") })),
                    };
                    if (levels.Count == 0 && filter != null)
                    {
                        List<string> near = McpLevels.Near(all, filter);
                        call.Note("No level's name contains '" + filter + "'." + (near.Count != 0 ? " Did you mean " + McpNames.Quote(near) + "?" : ""));
                    }
                    return result;
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
                Description = "Make a new level from scratch and open it (File > Create Level): GLOBAL, the pause menu, required assets and an empty root. 'imports' ports composites from other levels in as it is made, with the DisplayModel composites their Characters use (as port_composites brings them). Written to DATA/ENV at once, and the game's level list (DATA/PACKAGES/MAIN.PKG) is updated to include it; not undoable (delete_level removes it). Refuses a name another level already uses, as its whole name or last part (a level called SCI_HUB would share PRODUCTION/SCI_HUB's text databases and backups). Refuses while the game runs unless close_game. To start from a copy of an existing level (its lighting, navmesh and sounds kept), use duplicate_level. Then place things with create_entities and save_level with build=true. dry_run checks the name and imports and lists what would be ported.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "Letters, digits and underscores, up to 32 characters. It becomes the level's folder name (upper case).", required: true),
                    McpSchema.Array("imports", "Composites to port in while creating it, per source level, e.g. [{\"level\": \"PRODUCTION/SCI_ANDROIDLAB\", \"composites\": [\"<path or id>\"]}].",
                        McpSchema.Object(
                            McpSchema.String("level", "The source level, as list_levels names it.", required: true),
                            McpSchema.Strings("composites", "Composite paths or ids in that level.", required: true))),
                    McpSchema.Boolean("include_children", "With imports: also bring the composites they place (default true - they need them to work)."),
                    McpSchema.Boolean("include_display_models", "With imports: also bring the DisplayModel composites their Characters wear, and for a player every suit GLOBAL names (default true)."),
                    McpSchema.Boolean("overwrite_assets", "With imports: let imported models/textures/materials replace same-named ones (default false)."),
                    McpSchema.Boolean("discard_unsaved", "Open it even though the level open now has unsaved changes, losing them."),
                    McpSchema.Boolean("close_game", "If Alien: Isolation is running, close it first, as File > Create Level does (progress in it is lost). Without this the call is refused while it runs."),
                    McpSchema.Boolean("dry_run", "Check the name and imports and report what would be ported, writing nothing.")),
                Destructive = true,
                Run = CreateLevel,
            };

            yield return new McpTool()
            {
                Name = "duplicate_level",
                Title = "Duplicate level",
                Description = "Copy a whole level, campaign or custom, as a new custom level and open it: every file of DATA/ENV/<source> (a Nostromo level's _PATCH/WORLD merged in, as the game runs it), so its lighting, navmesh, cover, sound and zones come with it, ready to play. Its LEVEL_TEXT_DATABASES.XML block (subtitles, objectives) is copied under the new name, and the game's level list is updated. The root composite keeps the source's name. Campaign levels are hundreds of MB to a few GB: dry_run reports the size. Written at once, not undoable (delete_level removes it). Refuses names another level uses (as create_level), and while the game runs unless close_game.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("source", "The level to copy, as list_levels names it.", required: true),
                    McpSchema.String("name", "The new level's name: letters, digits and underscores, up to 32 characters (its folder under DATA/ENV).", required: true),
                    McpSchema.Boolean("open", "Open the copy in the editor afterwards (default true)."),
                    McpSchema.Boolean("discard_unsaved", "With open: open it even though the level open now has unsaved changes, losing them."),
                    McpSchema.Boolean("close_game", "If this install's game is running, close it first (progress in it is lost). Without this the call is refused while it runs."),
                    McpSchema.Boolean("dry_run", "Check everything and report what would be copied (files, size), writing nothing.")),
                Destructive = true,
                Run = DuplicateLevel,
            };

            yield return new McpTool()
            {
                Name = "delete_level",
                Title = "Delete level",
                Description = "Delete a custom level (one made with create_level or duplicate_level, never a campaign level under PRODUCTION): a backup of it is taken first (restore_backup with its id brings it back; its backups are kept), then its folder under DATA/ENV goes, its LEVEL_TEXT_DATABASES.XML block is dropped and the game's level list is updated. Refused while it is the open level (load_level another first), and while the game runs unless close_game. Not undoable; dry_run reports what would go.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The custom level, as list_levels names it.", required: true),
                    McpSchema.Boolean("backup_first", "Back it up before deleting (default true)."),
                    McpSchema.Boolean("close_game", "If this install's game is running, close it first. Without this the call is refused while it runs."),
                    McpSchema.Boolean("dry_run", "Report what would be deleted, deleting nothing.")),
                Destructive = true,
                Run = DeleteLevel,
            };

            yield return new McpTool()
            {
                Name = "diff_level",
                Title = "What changed in a level",
                Description = "Compare the open level's script (with its unsaved edits) against: 'saved' (default, what is on disk now - 'what have I changed since the last save'), 'backup' (a backup's script, backup_id from list_backups), or 'level' (another level's script on disk, e.g. the level a copy came from). Lists composites added, removed and renamed, and per composite the entities added, removed and renamed, parameters changed (old -> new), and links added and removed. Script only: models, materials and textures are not compared. Read-only.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("against", "saved (default), backup, or level.", options: new[] { "saved", "backup", "level" }),
                    McpSchema.Integer("backup_id", "against backup: the backup's id (list_backups)."),
                    McpSchema.String("level", "against level: the other level, as list_levels names it."),
                    McpSchema.Strings("composites", "Only these composites (paths or ids in the open level)."),
                    McpSchema.Boolean("details", "List each change (default true); false gives counts per composite only."),
                    McpSchema.Limit(100, "composites with changes"),
                    McpSchema.Offset("composites with changes")),
                ReadOnly = true,
                Idempotent = true,
                Run = DiffLevel,
            };

            yield return new McpTool()
            {
                Name = "save_level",
                Title = "Save level",
                Description = "Write the open level to the game install. A plain save writes the script and assets: enough for logic changes (links, triggers, parameters of logic entities, CAGEAnimations of cameras). build=true is Save & Build: it also regenerates what the game derives from the script - the movers that draw models and lights, collision and physics, zone membership, navmesh, cover, job positions, sound networks, radiosity - needed after placing, moving or deleting models, lights, collision, zones, navigation or sound entities, or composites holding them. build='auto' builds only when get_editor_state's level.build says it is needed (and says why). 'bakers' can skip slow ones. Takes seconds (plain) to minutes (build). Closes the game if it runs, as the editor's Save does (closed_game says so; Live Link drops). Not undoable.",
                InputSchema = McpSchema.Object(
                    McpSchema.Any("build", "true: Save & Build; false (default): a plain save; 'auto': build only if the level needs it (get_editor_state level.build)."),
                    McpSchema.Nested("bakers", "With build: which bakers run (default all). Skipping radiosity CLEARS the baked lighting - on a level still carrying its original lighting that loses it for good, as later bakes then regenerate it from scratch; skipped navmesh/cover/jobs/sound keep their old data.", BakersSchema)),
                Destructive = true,
                Run = SaveLevel,
            };

            yield return new McpTool()
            {
                Name = "launch_game",
                Title = "Launch the game",
                Description = "Start Alien: Isolation straight into a level (Steam, Epic or GOG). Closes this install's game and Cinematic Tools first if running, and writes the launch_options AI.exe patches as set. The game loads what is on disk: save (or save_level first) - a level needing a Save & Build is refused unless allow_stale_build (get_editor_state level.build says why). Without skip_frontend the level's scripts start but the game waits at PRESS ANY BUTTON; skip_frontend (default: the launch_options setting) starts it playing. wait_for 'level' or 'playing' waits until the game reports it through Live Link (launch_options live_link on), 'process' until AI.exe runs. Installs or removes the scripting-helpers ASI and starts Cinematic Tools as launch_options say. Without a level it starts at the menu.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level to start in, as list_levels names it. Defaults to the open level; 'menu' for the main menu."),
                    McpSchema.String("save", "Save the open level first when it is the one launched: none (default), save, build (Save & Build), or auto (build if it needs one, else save if it has unsaved changes).", options: new[] { "none", "save", "build", "auto" }),
                    McpSchema.Boolean("skip_frontend", "This launch only: true skips the frontend so the level starts playing; false waits at PRESS ANY BUTTON. Default: the launch_options skip_frontend setting. The next launch writes the setting back."),
                    McpSchema.String("wait_for", "none (default: return once started), process (AI.exe is running), level (the level has loaded) or playing (its scripts are running). level and playing need Live Link.", options: new[] { "none", "process", "level", "playing" }),
                    McpSchema.Integer("timeout_seconds", "With wait_for: how long to wait (default 180, at most 600)."),
                    McpSchema.Boolean("allow_stale_build", "Launch even though the level has changes only a Save & Build gives the game (it runs the older data)."),
                    McpSchema.Boolean("dry_run", "Report what would be launched and written (level, AI.exe patches, the build check), launching and writing nothing.")),
                Destructive = true,
                Run = LaunchGame,
            };

            yield return new McpTool()
            {
                Name = "undo",
                Title = "Undo",
                Description = "Undo the last change(s) made in the editor, by the user or by these tools. Each tool's change to the open level's script is one step (remove_references makes one per composite it edits, plus one): entities, parameters, links, trigger sequences, CAGEAnimations, entity resources, flowgraph pages and nodes, composites (create, rename, move, delete, de-instance, group). So are edits to the open level's materials, material mappings and textures, and some model edits; imports, ports, packages and files every level shares (configs, text, ANIMATION.PAK, sounds, UI), saves and backups are not: each tool's description says which it is, and get_undo_history lists the assistant's changes that left no step. undo_group makes several calls one step.",
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
                    List<Entity> chosen = null;
                    JObject result = McpEditor.UI(() =>
                    {
                        Commands commands = RequireCommandsForShowing();
                        Composite composite = ResolveShown(call, commands, out Composite entry, out List<Entity> instances);
                        List<Entity> select = call.StrList("entities", required: true).Select(o => McpScript.FindEntity(commands, composite, o)).ToList();
                        ShowAtPath(commands, entry, instances, composite, select);
                        chosen = select.Where(o => o != null).Distinct().ToList();
                        JObject selected = new JObject() { ["selected"] = select.Count, ["composite"] = McpScript.CompositeSummary(commands, composite) };
                        if (instances.Count != 0) selected["path"] = DescribeInstancePath(commands, entry, instances);
                        return selected;
                    });
                    if (call.Bool("focus"))
                    {
                        //The inspector takes the selection first, and the viewer follows it: focus once it has it
                        if (!McpEditor.WaitFor(call, () => SelectionShown(chosen), TimeSpan.FromSeconds(15), "Selecting"))
                            call.Note("The selection had not reached the inspector after 15 s, so the viewport camera may not have moved to it.");
                        Thread.Sleep(150);
                        McpEditor.UI(() => Send.SendViewportAction(ViewportAction.FocusOnSelection));
                    }
                    return result;
                },
            };

            yield return new McpTool()
            {
                Name = "list_backups",
                Title = "List level backups",
                Description = "The backups OpenCAGE's backup manager holds for a level, newest first: id (Unix time in seconds, which restore_backup and delete_backups take), name, date_utc (ISO 8601), age_hours, and how many files it changed from the one before (as Manage Backups shows); changed:true lists those files. pending_changes also counts what differs on disk now from the newest backup.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level (defaults to the open one)."),
                    McpSchema.Boolean("pending_changes", "Also count files changed since the newest backup. Hashes the whole level folder: seconds to a minute on campaign levels."),
                    McpSchema.Boolean("changed", "List the level-relative files each backup changed from the one before (up to 50 each)."),
                    McpSchema.Limit(50, "backups"),
                    McpSchema.Offset("backups")),
                ReadOnly = true,
                Idempotent = true,
                Concurrent = true,
                Run = ListBackups,
            };

            yield return new McpTool()
            {
                Name = "create_backup",
                Title = "Back up a level",
                Description = "Back up a level's files as they are on disk now (unsaved changes are not included - save_level first). all_levels backs up every editable level (Backup All Levels Now), which takes a long time. Written to DATA/MODTOOLS/BACKUPS at once; an open Manage Backups window on the level is refreshed.",
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
                Description = "Put a level's files back as a backup holds them, replacing what is on disk at once (closes this install's game; not undoable). By default the level's current files are backed up first (backup_current_first), so a wrong pick can be restored back; 'files' restores only some (e.g. ['WORLD/COMMANDS.PAK'] for just the script, or 'WORLD/*'); dry_run lists the files that would change, appear and disappear. If the level is open it is reopened: refused while it has unsaved changes unless discard_unsaved. A restore that fails part way puts the files back as they were when it can, and says so.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("id", "The backup's id, from list_backups.", required: true),
                    McpSchema.String("level", "The level (defaults to the open one)."),
                    McpSchema.Strings("files", "Only these level-relative files (either slash; '*' matches within a folder name, '**' across folders): each is written as the backup holds it, or deleted if the backup has no such file. Default: the whole level folder."),
                    McpSchema.Boolean("backup_current_first", "Back up the level as it is now before restoring (default true); the result gives its id."),
                    McpSchema.Boolean("discard_unsaved", "The level is open with unsaved changes: restore anyway, losing them."),
                    McpSchema.Boolean("dry_run", "Report which files would change, appear and disappear (hashes the level folder), writing nothing.")),
                Destructive = true,
                Run = call =>
                {
                    string level = LevelArgument(call);
                    long id = BackupId(call.Token("id") ?? throw new McpError("'id' is required."), "id");
                    AlienLevel backups = OpenBackups(level);
                    if (!backups.Backups.Any(o => o.ID == id))
                        throw McpError.NotFound("backup of " + level + " with id", id.ToString(), backups.Backups.Select(o => o.ID.ToString()), "list_backups shows them, newest first.");
                    AlienLevel.AlienBackup chosen = backups.Backups.First(o => o.ID == id);
                    Dictionary<string, string> stored = backups.PathsOf(chosen, out List<string> ambiguous);
                    List<string> only = call.Has("files") ? MatchFiles(call.StrList("files"), stored.Keys.Concat(CurrentFiles(level)), "files") : null;
                    bool isOpen = McpEditor.UI(() => string.Equals(Singleton.Editor.CompositeBrowser?.Content?.Level?.Name, level, StringComparison.OrdinalIgnoreCase));
                    bool openDirty = isOpen && McpEditor.UI(() => DirtyTracker.IsDirty);

                    if (call.Bool("dry_run"))
                    {
                        JObject plan = new JObject() { ["dry_run"] = true, ["level"] = level, ["backup"] = DescribeBackup(chosen) };
                        Dictionary<string, string> now;
                        using (Heartbeat(call, "Comparing " + level + " with backup " + id))
                        {
                            try { now = backups.CurrentPaths(); }
                            catch (DirectoryNotFoundException) { now = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }
                        }
                        Func<string, bool> inScope = name => only == null || only.Contains(name, StringComparer.OrdinalIgnoreCase);
                        List<string> changes = stored.Where(o => inScope(o.Key) && now.TryGetValue(o.Key, out string hash) && hash != o.Value).Select(o => Slashes(o.Key)).OrderBy(o => o).ToList();
                        List<string> appear = stored.Where(o => inScope(o.Key) && !now.ContainsKey(o.Key)).Select(o => Slashes(o.Key)).OrderBy(o => o).ToList();
                        List<string> disappear = now.Keys.Where(o => inScope(o) && !stored.ContainsKey(o)).Select(Slashes).OrderBy(o => o).ToList();
                        plan["would_change"] = ListOf(changes, 200);
                        plan["would_add"] = ListOf(appear, 200);
                        plan["would_delete"] = ListOf(disappear, 200);
                        plan["unchanged"] = stored.Count(o => inScope(o.Key) && now.TryGetValue(o.Key, out string hash) && hash == o.Value);
                        if (ambiguous.Count != 0)
                            plan["ambiguous"] = ListOf(ambiguous.Select(Slashes).ToList(), 50);
                        if (openDirty && !call.Bool("discard_unsaved"))
                            call.Note(level + " is open with unsaved changes: the real call needs discard_unsaved: true (or save_level first).");
                        if (LevelBackupWindowBusy(level))
                            call.Note("A Manage Backups window is working on " + level + ": the real call waits for it to finish.");
                        return plan;
                    }
                    if (openDirty && !call.Bool("discard_unsaved"))
                        throw new McpError(McpErrorCodes.Refused, level + " is open with unsaved changes, which the restore would lose (the level is reopened from the restored files). save_level first, or pass discard_unsaved: true. Nothing was restored.");
                    if (LevelBackupWindowBusy(level))
                        throw McpError.Busy("A Manage Backups window is backing up or restoring " + level + ". Try again when it has finished.");

                    //A safety net first: the level as it is now, restorable if this was the wrong backup
                    JObject before = null;
                    if (call.Bool("backup_current_first", true) && Directory.Exists(Path.Combine(Singleton.PathToAI, "DATA", "ENV", level)))
                    {
                        AlienLevel.AlienBackup safety = MakeBackup(call, level, "Before restoring '" + chosen.Name + "'");
                        before = DescribeBackup(safety);
                        backups = OpenBackups(level);
                    }
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
                    bool recreated = false;
                    List<string> closedGame;
                    try
                    {
                        closedGame = EditorUtils.CloseAI(null, thisInstallOnly: true);
                        call.Progress("Restoring " + level);
                        //A level whose folder is gone altogether can still be put back: the restore first copies the folder aside
                        string folder = Path.Combine(Singleton.PathToAI, "DATA", "ENV", level);
                        if (!Directory.Exists(folder))
                        {
                            Directory.CreateDirectory(folder);
                            recreated = true;
                        }
                        using (Heartbeat(call, "Restoring " + level))
                            restored = backups.RestoreBackup(id, only);
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
                    RefreshBackupWindows(level);
                    bool reopen = McpEditor.UI(() => string.Equals(Singleton.Editor.CompositeBrowser?.Content?.Level?.Name, level, StringComparison.OrdinalIgnoreCase));
                    if (reopen)
                    {
                        OpenLevel(call, level);
                        //Some files from one state and the rest from another: what the build derived may not match the script
                        if (only != null && only.Count < stored.Count)
                            McpEditor.UI(() => BuildTracker.MarkUnbuilt("only some of its files were restored from backup '" + chosen.Name + "': the rest of its data may not match its script"));
                    }
                    JObject result = new JObject() { ["restored"] = level, ["backup"] = DescribeBackup(chosen), ["reopened"] = reopen };
                    //Deleted with delete_level: what it took from outside the folder goes back too
                    if (recreated)
                        RestoreDeletedLevelEntries(call, level, backups, result);
                    if (only != null)
                        result["files"] = ListOf(only.Select(Slashes).ToList(), 200);
                    if (before != null)
                    {
                        result["backup_of_previous_state"] = before;
                        call.Note("The level as it was before is backup " + (long)before["id"] + ": restore_backup with that id puts it back.");
                    }
                    if (ambiguous.Count != 0 && (only == null || only.Any(o => ambiguous.Contains(o, StringComparer.OrdinalIgnoreCase))))
                        call.Note("This backup predates per-file records, and " + ambiguous.Count + " file(s) matched more than one stored revision; the newest match was used: " + string.Join(", ", ambiguous.Take(8).Select(Slashes)) + ".");
                    if (closedGame.Count != 0)
                        result["closed_game"] = new JArray(closedGame);
                    if (openDirty)
                        call.Note("The unsaved changes " + level + " had were discarded.");
                    return result;
                },
            };

            yield return new McpTool()
            {
                Name = "delete_backups",
                Title = "Delete level backups",
                Description = "Permanently delete backups of a level (Manage Backups > Delete Checked Backups): their entries go, and stored file revisions no other backup uses are deleted from DATA/MODTOOLS/BACKUPS. Cannot be undone. list_backups gives the ids; an open Manage Backups window on the level is refreshed.",
                InputSchema = McpSchema.Object(
                    McpSchema.Array("ids", "The backups' ids, from list_backups.", new JObject() { ["type"] = "integer" }, required: true),
                    McpSchema.String("level", "The level (defaults to the open one).")),
                Destructive = true,
                Run = DeleteBackups,
            };
        }

        /// <summary>The 'path' argument open_composite and select_entities share.</summary>
        private static McpSchema.Prop PathProp(string end) =>
            McpSchema.Any("path", "Instance entities (names or ids) to step through from 'composite' or the root; " + end + ". An array, a string split on '/', or a path a result gives ({path, ids}, with 'from' when it does not start at the root) passed back as it is.");

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
                    ["build"] = BuildState(),
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

        /// <summary>Whether the open level needs a Save & Build before the game has its changes, and why. UI thread.</summary>
        internal static JObject BuildState()
        {
            List<string> reasons = BuildTracker.Reasons(12, out int total);
            JObject build = new JObject() { ["needs_build"] = total != 0 };
            if (total != 0)
            {
                build["reasons"] = new JArray(reasons);
                if (total > reasons.Count) build["more_reasons"] = total - reasons.Count;
            }
            if (BuildTracker.LastSavedUtc != null) build["last_saved_utc"] = BuildTracker.LastSavedUtc.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");
            if (BuildTracker.LastBuiltUtc != null) build["last_built_utc"] = BuildTracker.LastBuiltUtc.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");
            if (BuildTracker.LastBuildSkipped.Count != 0) build["last_build_skipped"] = new JArray(BuildTracker.LastBuildSkipped);
            return build;
        }

        /// <summary>
        /// This install's game as get_editor_state reports it: running (by its AI.exe path), other AI.exe processes, and
        /// through Live Link (when connected) the level it runs. Off the UI thread: process scans and the Live Link round trip.
        /// </summary>
        internal static JObject GameState(TimeSpan timeout)
        {
            JObject game = new JObject() { ["running"] = EditorUtils.ThisInstallsGameRunning() };
            List<string> others = EditorUtils.GameProcesses(false);
            if (others.Count != 0)
                game["other_installs_running"] = new JArray(others);
            bool connected = global::OpenCAGE.RuntimeUtilsConnection.Send.Connected;
            game["live_link_connected"] = connected;
            if (!connected)
                return game;
            try
            {
                System.Threading.Tasks.Task<global::OpenCAGE.RuntimeUtilsConnection.LiveLink.GameStatus> asked = global::OpenCAGE.RuntimeUtilsConnection.LiveLink.Status();
                if (!asked.Wait(timeout))
                {
                    game["live_link_status"] = "no answer within " + timeout.TotalSeconds + " s";
                    return game;
                }
                global::OpenCAGE.RuntimeUtilsConnection.LiveLink.GameStatus status = asked.Result;
                if (!status.Reply.Ok)
                {
                    game["live_link_status"] = status.Reply.Message;
                    return game;
                }
                game["level_running"] = status.LevelRunning;
                game["playing"] = status.Playing;
                if (!string.IsNullOrEmpty(status.RootCompositeName)) game["running_root_composite"] = status.RootCompositeName;
                Commands open = McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands);
                if (open != null && status.LevelRunning)
                    game["running_level_open_here"] = status.IsRunning(open);
            }
            catch (Exception e)
            {
                game["live_link_status"] = e.InnerException?.Message ?? e.Message;
            }
            return game;
        }

        /// <summary>The inspector has exactly these entities selected (none: nothing). UI thread.</summary>
        private static bool SelectionShown(List<Entity> chosen)
        {
            EntityInspector inspector = Singleton.Editor?.CompositeDisplay?.EntityDisplay;
            if (inspector == null || inspector.IsDisposed || chosen == null)
                return false;
            if (chosen.Count == 0)
                return !inspector.Populated;
            if (chosen.Count == 1)
                return !inspector.IsMultiEditing && inspector.Entity == chosen[0];
            List<Entity> multi = inspector.MultiSelectedEntities;
            return multi != null && multi.Count == chosen.Count && chosen.All(multi.Contains);
        }
        #endregion

        #region Levels
        private static object LoadLevel(McpCall call)
        {
            string match = McpLevels.Resolve(call.Str("level", required: true), call: call);
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
            //Whatever comes of it, the level the client knew is closed: the one this leaves open is the one it knows
            call.OpenedLevel = true;
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
            string clash = McpLevels.Clash(levelId);
            if (clash != null)
                throw new McpError(McpErrorCodes.Conflict, clash);
            if (!Level.GetLevels(Singleton.PathToAI).Any(o => EditorUtils.IsFrontend(o)))
                throw new McpError("New levels are made from PRODUCTION/FRONTEND, which is missing from this install. Verify the game files.");
            //What to port in, checked against each source level's composite index before anything is written
            CompositeSelection imports = ReadImports(call);
            bool displayModels = call.Bool("include_display_models", true);
            if (call.Has("include_display_models") && imports.IsEmpty)
                throw McpError.Invalid("include_display_models only applies with 'imports'.");
            bool closeGame = call.Bool("close_game");

            if (call.Bool("dry_run"))
            {
                JObject plan = new JObject() { ["dry_run"] = true, ["level"] = levelId, ["folder"] = Path.GetFullPath(path) };
                if (!imports.IsEmpty)
                {
                    JArray from = new JArray();
                    foreach (CompositeSelection.LevelPick pick in imports.Levels)
                    {
                        List<CompositeIndexEntry> index = CompositeIndexCache.Get(pick.Level);
                        List<CompositeIndexEntry> closure = McpPortingTools.IndexClosure(index, pick.Composites.Keys, imports.IncludeChildren);
                        from.Add(new JObject()
                        {
                            ["level"] = pick.Level,
                            ["composites"] = new JArray(pick.Composites.Values),
                            ["with_what_they_place"] = closure.Count,
                        });
                    }
                    plan["imports"] = from;
                    if (displayModels)
                        call.Note("Display models the imported Characters wear are found when each source level loads; they are not listed here.");
                }
                if (EditorUtils.ThisInstallsGameRunning() && !closeGame)
                    call.Note("Alien: Isolation is running: the real call needs close_game: true.");
                if (McpEditor.UI(() => Singleton.Editor.CompositeBrowser?.Content?.Level != null && DirtyTracker.IsDirty) && !call.Bool("discard_unsaved"))
                    call.Note("The open level has unsaved changes: the real call needs discard_unsaved: true (or save_level first).");
                return plan;
            }
            CheckUnsaved(call);

            //It writes the whole level and the game's level list, and the game holds its files open: closed only when asked
            if (EditorUtils.ThisInstallsGameRunning())
            {
                if (!closeGame)
                    throw new McpError(McpErrorCodes.Refused, "Alien: Isolation is running, and a level can't be created while it is. Close it, or pass close_game: true to have OpenCAGE close it (progress in it is lost). Nothing was written.");
                EditorUtils.CloseAI(null, thisInstallOnly: true);
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
                //Which parameters were set by hand, and which entities had their defaults applied: the inspector's marks, which
                //port_composites carries for the open level and which travel here from each source's own tables
                CompositeParameterModificationTable modifications = (CustomTable.ReadTable(newLevel.Commands.Filepath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION) as CompositeParameterModificationTable)
                    ?? ParameterModificationTracker.GenerateModificationTable(newLevel.Commands);
                EntityAppliedDefaultsTable defaults = (CustomTable.ReadTable(newLevel.Commands.Filepath, CustomTableType.ENTITY_APPLIED_DEFAULTS) as EntityAppliedDefaultsTable) ?? new EntityAppliedDefaultsTable();
                Dictionary<Level, KeyValuePair<CompositeParameterModificationTable, EntityAppliedDefaultsTable>> sourceTables = new Dictionary<Level, KeyValuePair<CompositeParameterModificationTable, EntityAppliedDefaultsTable>>();
                if (!imports.IsEmpty)
                {
                    //The importer loads each source level and ports from it behind its own progress windows, which are the UI thread's
                    call.Progress("Importing " + imports.Summary());
                    using (Heartbeat(call, "Importing " + imports.CompositeCount + " composite(s) into " + levelId))
                    {
                        Level made = newLevel;
                        imported = McpEditor.UI(() => CompositeImporter.Import(imports, made, (composite, pages) =>
                        {
                            layouts.flowgraphs.RemoveAll(o => o.CompositeGUID == composite.shortGUID);
                            layouts.flowgraphs.AddRange(pages);
                        },
                        //Characters name their display model rather than placing it: those come too, as port_composites brings them
                        displayModels ? (Func<Level, CompositeSelection.LevelPick, IEnumerable<Composite>>)((source, pick) =>
                            McpPortingTools.DisplayModelsFor(source.Commands, pick.Composites.Keys.Select(o => source.Commands.GetComposite(o)).Where(o => o != null).ToList(),
                                imports.IncludeChildren, new HashSet<ShortGuid>(made.Commands.Entries.Where(o => o != null).Select(o => o.shortGUID)), false)) : null,
                        (source, original, copy) =>
                        {
                            if (!sourceTables.TryGetValue(source, out KeyValuePair<CompositeParameterModificationTable, EntityAppliedDefaultsTable> tables))
                            {
                                tables = new KeyValuePair<CompositeParameterModificationTable, EntityAppliedDefaultsTable>(
                                    CustomTable.ReadTable(source.Commands.Filepath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION) as CompositeParameterModificationTable,
                                    CustomTable.ReadTable(source.Commands.Filepath, CustomTableType.ENTITY_APPLIED_DEFAULTS) as EntityAppliedDefaultsTable);
                                sourceTables[source] = tables;
                            }
                            ParameterModificationTracker.CopyCompositeRows(copy.shortGUID, tables.Key, tables.Value, modifications, defaults);
                        }));
                    }
                    sourceTables.Clear();
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
                if (imported != null)
                {
                    CustomTable.WriteTable(newLevel.Commands.Filepath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION, modifications);
                    CustomTable.WriteTable(newLevel.Commands.Filepath, CustomTableType.ENTITY_APPLIED_DEFAULTS, defaults);
                }
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
            //A level made from scratch has never been built: the game cannot run it until it is
            McpEditor.UI(() => BuildTracker.MarkUnbuilt("a new level: it has not been through a Save & Build yet"));
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
                if (imported.Extra.Count != 0) importedSummary["display_models_added"] = new JArray(imported.Extra);
                if (imported.DeadProxies.Any) importedSummary["dead_proxies"] = imported.DeadProxies.Describe(levelId);
                created["imported"] = importedSummary;
                call.Note("Imported composites are not placed anywhere yet: add instances in 'root' with create_entities, then save_level with build=true.");
            }
            else
                call.Note("The level has no geometry or player yet. Typical next steps: port_composites from a campaign level (e.g. the player spawn 'Archetypes\\Script\\Mission\\SpawnPositionSelect' with 'DisplayModel:RIPLEY_FP', and some geometry), create_entities in 'root' to place them, then save_level with build=true.");
            state["created"] = created;
            return state;
        }

        /// <summary>A new level's name, as create_level and duplicate_level take it: its folder name (upper case), checked.</summary>
        private static string NewLevelName(McpCall call, string argument)
        {
            string name = call.Str(argument, required: true).Trim();
            int maxLength = Math.Min(32, PatchManager.MaxLaunchMapNameLength);
            if (!Regex.IsMatch(name, "^[A-Za-z0-9_]+$"))
                throw McpError.Invalid("Level names can only contain letters, digits and underscores ('" + name + "').");
            if (name.Length > maxLength)
                throw McpError.Invalid("Level names can be at most " + maxLength + " characters.");
            string levelId = name.ToUpperInvariant();
            //The game crashes building the physics world of any BSPNOSTROMO_* folder that is not one of the real two
            if (levelId.StartsWith("BSPNOSTROMO", StringComparison.Ordinal))
                throw McpError.Invalid("Level names starting BSPNOSTROMO crash the game (it treats them as the Nostromo DLC). Choose another name, e.g. NOSTROMO_COPY.");
            string clash = McpLevels.Clash(levelId);
            if (clash != null)
                throw new McpError(McpErrorCodes.Conflict, clash);
            return levelId;
        }

        private static object DuplicateLevel(McpCall call)
        {
            string source = McpLevels.Resolve(call.Str("source", required: true), call: call, argument: "source");
            string levelId = NewLevelName(call, "name");
            bool dryRun = call.Bool("dry_run");
            bool open = call.Bool("open", true);
            string from = Path.Combine(Singleton.PathToAI, "DATA", "ENV", source.Replace('/', '\\'));
            string to = Path.Combine(Singleton.PathToAI, "DATA", "ENV", levelId);

            //What the game runs: the level folder, with a Nostromo level's WORLD taken from its _PATCH companion
            Dictionary<string, string> files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.GetFiles(from, "*.*", SearchOption.AllDirectories))
                files[file.Substring(from.Length + 1)] = file;
            string patchWorld = from + "_PATCH\\WORLD";
            int patched = 0;
            if (source.StartsWith("PRODUCTION/DLC/BSPNOSTROMO_", StringComparison.OrdinalIgnoreCase) && Directory.Exists(patchWorld))
                foreach (string file in Directory.GetFiles(patchWorld, "*.*", SearchOption.AllDirectories))
                {
                    files["WORLD\\" + file.Substring(patchWorld.Length + 1)] = file;
                    patched++;
                }
            long bytes = files.Values.Sum(o => new FileInfo(o).Length);
            List<string> textDatabases = McpGameDataTools.TextDatabaseBlock(McpLevels.Leaf(source));
            bool sourceDirty = McpEditor.UI(() => string.Equals(Singleton.Editor.CompositeBrowser?.Content?.Level?.Name, source, StringComparison.OrdinalIgnoreCase) && DirtyTracker.IsDirty);
            if (sourceDirty)
                call.Note(source + " is open with unsaved changes: the copy is made from its files on disk, without them (save_level first to include them).");

            JObject result = new JObject()
            {
                ["source"] = source,
                ["level"] = levelId,
                ["folder"] = Path.GetFullPath(to),
                ["files"] = files.Count,
                ["size_mb"] = Math.Round(bytes / 1048576.0, 1),
                ["text_databases"] = new JArray(textDatabases),
            };
            if (patched != 0) result["nostromo_patch_files"] = patched;
            if (textDatabases.Count == 0)
                call.Note(source + " has no LEVEL_TEXT_DATABASES.XML block of its own (it loads only the globals block).");
            if (dryRun)
            {
                result["dry_run"] = true;
                if (EditorUtils.ThisInstallsGameRunning() && !call.Bool("close_game"))
                    call.Note("Alien: Isolation is running: the real call needs close_game: true.");
                return result;
            }
            if (open)
                CheckUnsaved(call);
            if (EditorUtils.ThisInstallsGameRunning())
            {
                if (!call.Bool("close_game"))
                    throw new McpError(McpErrorCodes.Refused, "Alien: Isolation is running and writes into its level list while it runs. Close it, or pass close_game: true (progress in it is lost). Nothing was copied.");
                result["closed_game"] = new JArray(EditorUtils.CloseAI(null, thisInstallOnly: true));
            }
            McpEditor.UI(() =>
            {
                if (UndoStack.Current.Blocked) throw McpError.Busy("OpenCAGE is saving a level. Try again when it has finished.");
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE) throw McpError.Busy("A level backup is running. Try again when it has finished.");
            });

            Stopwatch timer = Stopwatch.StartNew();
            long copied = 0;
            try
            {
                foreach (KeyValuePair<string, string> file in files)
                {
                    call.ThrowIfCancelled();
                    string target = Path.Combine(to, file.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file.Value, target, false);
                    copied += new FileInfo(file.Value).Length;
                    call.Progress("Copying " + source + " to " + levelId + " (" + (copied / 1048576) + " of " + (bytes / 1048576) + " MB)", copied, bytes);
                }
            }
            catch (Exception e)
            {
                //A part copy would block the name and look like a level: take it away again
                try { if (Directory.Exists(to)) Directory.Delete(to, true); } catch { }
                if (e is OperationCanceledException)
                    throw new McpError(McpErrorCodes.Cancelled, "Cancelled: the part-made copy was removed.");
                throw new McpError(McpErrorCodes.Failed, "Copying " + source + " failed (" + e.Message + "); the part-made copy was removed" + (Directory.Exists(to) ? " as far as it could be: delete " + Path.GetFullPath(to) + " by hand" : "") + ".");
            }
            result["seconds"] = Math.Round(timer.Elapsed.TotalSeconds, 1);

            //Its subtitles and objectives: the source's block of LEVEL_TEXT_DATABASES.XML, under the new name
            if (textDatabases.Count != 0)
            {
                try { McpGameDataTools.SetTextDatabaseBlock(call, levelId, textDatabases); }
                catch (McpError e) { call.Note("The copy is made, but its text databases could not be listed in LEVEL_TEXT_DATABASES.XML (" + e.Message + "): set_text_databases level '" + levelId + "' add_shared " + string.Join(", ", textDatabases) + " does it."); }
            }
            if (!PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI))
                call.Note("The game's level list (DATA/PACKAGES/MAIN.PKG) could not be updated; launch_game updates it before starting the game.");
            call.Note("The copy keeps " + source + "'s root composite and every id, so a SwitchLevel that names " + source + " still goes there, not here.");

            if (open)
            {
                try { OpenLevel(call, levelId); }
                catch (Exception e) when (e is McpError || e is OperationCanceledException)
                {
                    throw new McpError(levelId + " was made (" + Path.GetFullPath(to) + ") but " + (e is OperationCanceledException ? "the call was cancelled before it was opened: load_level opens it." : "opening it did not finish: " + e.Message));
                }
                result["opened"] = true;
            }
            return result;
        }

        private static object DeleteLevel(McpCall call)
        {
            string level = McpLevels.Resolve(call.Str("level", required: true), call: call);
            if (level.Contains("/"))
                throw new McpError(McpErrorCodes.Refused, level + " is a campaign or DLC level: only custom levels (made with create_level or duplicate_level) can be deleted. restore_backup puts a campaign level back as a backup holds it.");
            string folder = Path.Combine(Singleton.PathToAI, "DATA", "ENV", level);
            string open = McpEditor.UI(() => Singleton.Editor.CompositeBrowser?.Content?.Level?.Name);
            if (string.Equals(open, level, StringComparison.OrdinalIgnoreCase))
                throw new McpError(McpErrorCodes.Refused, level + " is the level open in the editor: load_level another level first.");
            string[] files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
            long bytes = files.Sum(o => new FileInfo(o).Length);
            //A block keyed by the same last part as another level (made before names were checked) is theirs too: kept
            bool shared = Level.GetLevels(Singleton.PathToAI).Any(o => !string.Equals(o, level, StringComparison.OrdinalIgnoreCase) && McpLevels.Leaf(o) == level);
            List<string> textDatabases = shared ? new List<string>() : McpGameDataTools.TextDatabaseBlock(level);
            bool backupFirst = call.Bool("backup_first", true);
            JObject result = new JObject()
            {
                ["level"] = level,
                ["folder"] = Path.GetFullPath(folder),
                ["files"] = files.Length,
                ["size_mb"] = Math.Round(bytes / 1048576.0, 1),
            };
            if (textDatabases.Count != 0) result["text_databases_block"] = new JArray(textDatabases);
            if (shared) call.Note("Another level's name ends with " + level + ", and LEVEL_TEXT_DATABASES.XML keys blocks by that last part: its block is left in place.");
            if (call.Bool("dry_run"))
            {
                result["dry_run"] = true;
                result["would_back_up_first"] = backupFirst;
                if (EditorUtils.ThisInstallsGameRunning() && !call.Bool("close_game"))
                    call.Note("Alien: Isolation is running: the real call needs close_game: true.");
                return result;
            }
            if (EditorUtils.ThisInstallsGameRunning())
            {
                if (!call.Bool("close_game"))
                    throw new McpError(McpErrorCodes.Refused, "Alien: Isolation is running and may hold " + level + "'s files open. Close it, or pass close_game: true. Nothing was deleted.");
                result["closed_game"] = new JArray(EditorUtils.CloseAI(null, thisInstallOnly: true));
            }
            if (backupFirst)
            {
                //Its block of LEVEL_TEXT_DATABASES.XML is outside its folder, so no backup holds it: the name keeps it for restore_backup
                AlienLevel.AlienBackup backup = MakeBackup(call, level, DeletedLevelBackupName + (textDatabases.Count != 0 ? TextDatabasesInName + string.Join(", ", textDatabases) : ""));
                result["backup"] = DescribeBackup(backup);
            }
            McpEditor.UI(() =>
            {
                if (UndoStack.Current.Blocked) throw McpError.Busy("OpenCAGE is saving a level. Try again when it has finished.");
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE) throw McpError.Busy("A level backup is running. Try again when it has finished.");
                if (string.Equals(Singleton.Editor.CompositeBrowser?.Content?.Level?.Name, level, StringComparison.OrdinalIgnoreCase) || Singleton.Editor.IsLevelLoadInProgress)
                    throw McpError.Busy(level + " is being opened in the editor. Nothing was deleted.");
            });
            if (string.Equals(McpLevelCache.Held, level, StringComparison.OrdinalIgnoreCase))
                McpLevelCache.Release();
            try { Directory.Delete(folder, true); }
            catch (Exception e)
            {
                throw new McpError(McpErrorCodes.Failed, "Deleting " + folder + " failed part way (" + e.Message + "): close anything holding its files and call again" + (backupFirst ? " (the backup taken first can put it back)." : "."));
            }
            result["deleted"] = true;
            if (textDatabases.Count != 0)
            {
                try { McpGameDataTools.SetTextDatabaseBlock(call, level, new List<string>()); }
                catch (McpError e) { call.Note("Its LEVEL_TEXT_DATABASES.XML block could not be removed (" + e.Message + ")."); }
            }
            PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI);
            if (backupFirst)
                call.Note("Its backups are kept: restore_backup level '" + level + "' id " + (long)result["backup"]["id"] + " brings it back" +
                    (textDatabases.Count != 0 ? ", with its LEVEL_TEXT_DATABASES.XML block (" + string.Join(", ", textDatabases) + ")" : "") + ", and lists it in the game's level list again.");
            else if (textDatabases.Count != 0)
                call.Note("Its LEVEL_TEXT_DATABASES.XML block (" + string.Join(", ", textDatabases) + ") is gone with it: no backup was taken, so a copy put back by hand also needs set_text_databases level '" + level + "' add_shared " + string.Join(", ", textDatabases) + ".");
            return result;
        }

        //The name delete_level gives the backup it takes first; its LEVEL_TEXT_DATABASES.XML block, if it had one, follows it
        private const string DeletedLevelBackupName = "Before deleting the level";
        private const string TextDatabasesInName = "; text databases: ";

        /// <summary>
        /// A level whose folder a restore made again (deleted with delete_level): listed in the game's level list again, and its
        /// block of LEVEL_TEXT_DATABASES.XML put back as the newest of its delete_level backups recorded it. Notes what it did.
        /// </summary>
        private static void RestoreDeletedLevelEntries(McpCall call, string level, AlienLevel backups, JObject result)
        {
            if (!PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI))
                call.Note("The game's level list (DATA/PACKAGES/MAIN.PKG) could not be updated to include " + level + "; launch_game updates it before starting the game.");
            if (level.Contains("/") || McpGameDataTools.TextDatabaseBlock(level).Count != 0)
                return;
            AlienLevel.AlienBackup deletion = backups.Backups.Where(o => o.Name != null && o.Name.StartsWith(DeletedLevelBackupName + TextDatabasesInName, StringComparison.Ordinal))
                .OrderByDescending(o => o.ID).FirstOrDefault();
            if (deletion == null)
                return;
            List<string> databases = deletion.Name.Substring((DeletedLevelBackupName + TextDatabasesInName).Length)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).Where(o => o.Length != 0).ToList();
            if (databases.Count == 0)
                return;
            try
            {
                McpGameDataTools.SetTextDatabaseBlock(call, level, databases);
                result["text_databases_block"] = new JArray(databases);
            }
            catch (McpError e)
            {
                call.Note("Its LEVEL_TEXT_DATABASES.XML block could not be put back (" + e.Message + "): set_text_databases level '" + level + "' add_shared " + string.Join(", ", databases) + " does it.");
            }
        }

        private static object DiffLevel(McpCall call)
        {
            string against = (call.Str("against") ?? "saved").Trim().ToLowerInvariant();
            if (against != "saved" && against != "backup" && against != "level")
                throw McpError.Invalid("'against' takes saved, backup or level.");
            if (call.Has("backup_id") && against != "backup")
                throw McpError.Invalid("backup_id goes with against: 'backup'.");
            if (call.Has("level") && against != "level")
                throw McpError.Invalid("'level' goes with against: 'level' (the open level is always one side).");
            bool details = call.Bool("details", true);

            Commands mine = null;
            string levelName = null, scriptPath = null, levelFolder = null;
            List<Composite> only = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                mine = content.Level.Commands;
                levelName = content.Level.Name;
                scriptPath = Path.GetFullPath(content.Level.CommandsFilepath);
                levelFolder = Path.GetFullPath(content.Level.Filepath).TrimEnd('\\', '/');
                //The composite on screen keeps its links in its live pages until they are compiled
                Composite shown = Singleton.Editor.CompositeDisplay?.Composite;
                if (shown != null) McpBrowseTools.CompileIfShown(shown);
                if (call.Has("composites"))
                    only = call.StrList("composites").Select(o => McpScript.FindComposite(mine, o)).Distinct().ToList();
            });

            string label, otherPath, temp = null;
            switch (against)
            {
                case "saved":
                    otherPath = scriptPath;
                    label = levelName + " as saved";
                    break;
                case "level":
                    string other = McpLevels.Resolve(call.Str("level", required: true), call: call);
                    otherPath = new Level(Singleton.PathToAI + "/DATA/ENV/" + other, Singleton.Global, false).CommandsFilepath;
                    label = other + " as saved";
                    break;
                default:
                    long id = BackupId(call.Token("backup_id") ?? throw McpError.Invalid("against 'backup' needs backup_id (list_backups gives them)."), "backup_id");
                    AlienLevel archive = OpenBackups(levelName);
                    AlienLevel.AlienBackup backup = archive.Backups.FirstOrDefault(o => o.ID == id)
                        ?? throw McpError.NotFound("backup of " + levelName + " with id", id.ToString(), archive.Backups.Select(o => o.ID.ToString()), "list_backups shows them.");
                    Dictionary<string, string> paths = archive.PathsOf(backup, out _);
                    string relative = scriptPath.StartsWith(levelFolder + "\\", StringComparison.OrdinalIgnoreCase) ? scriptPath.Substring(levelFolder.Length + 1) : null;
                    string hash = relative == null ? null : paths.FirstOrDefault(o => string.Equals(o.Key.Replace('/', '\\'), relative, StringComparison.OrdinalIgnoreCase)).Value;
                    if (hash == null)
                        throw new McpError(McpErrorCodes.NotFound, "Backup " + id + " holds no " + (relative ?? Path.GetFileName(scriptPath)) + " (" + (relative == null ? "this level's script lives outside its folder - a _PATCH companion - which backups do not hold" : "it was taken before the level had one") + ").");
                    temp = Path.Combine(Path.GetTempPath(), "OpenCAGE_diff_" + Guid.NewGuid().ToString("N") + Path.GetExtension(scriptPath));
                    File.WriteAllBytes(temp, archive.ReadRevision(hash));
                    otherPath = temp;
                    label = "backup " + id + " ('" + backup.Name + "', " + AlienLevel.DateUtc(backup).ToString("yyyy-MM-ddTHH:mm:ssZ") + ")";
                    break;
            }
            Commands theirs;
            try
            {
                if (!File.Exists(otherPath))
                    throw new McpError(McpErrorCodes.NotFound, "There is no script at " + otherPath + ".");
                using (Heartbeat(call, "Reading the script of " + label))
                    theirs = McpPortingTools.ReadScript(otherPath);
            }
            finally
            {
                if (temp != null) try { File.Delete(temp); } catch { }
            }

            JObject result = McpEditor.UI(() => CompareScripts(call, mine, theirs, only, details));
            result["level"] = levelName;
            result["against"] = label;
            result["note"] = "'before' is " + label + "; 'now' is the open level, unsaved edits included. Script only: models, materials and textures are not compared.";
            return result;
        }

        /// <summary>What changed from <paramref name="before"/> to <paramref name="now"/>, per composite. UI thread (now is the open level).</summary>
        private static JObject CompareScripts(McpCall call, Commands now, Commands before, List<Composite> only, bool details)
        {
            Dictionary<ShortGuid, Composite> was = before.Entries.Where(o => o != null).GroupBy(o => o.shortGUID).ToDictionary(o => o.Key, o => o.First());
            Dictionary<ShortGuid, Composite> isNow = now.Entries.Where(o => o != null).GroupBy(o => o.shortGUID).ToDictionary(o => o.Key, o => o.First());
            List<Composite> added = isNow.Values.Where(o => !was.ContainsKey(o.shortGUID)).OrderBy(o => o.name).ToList();
            List<Composite> removed = was.Values.Where(o => !isNow.ContainsKey(o.shortGUID)).OrderBy(o => o.name).ToList();
            List<JObject> renamed = isNow.Values.Where(o => was.TryGetValue(o.shortGUID, out Composite old) && old.name != o.name)
                .Select(o => new JObject() { ["id"] = McpScript.Id(o.shortGUID), ["from"] = was[o.shortGUID].name, ["to"] = o.name }).ToList();

            List<JObject> changed = new List<JObject>();
            int entityChanges = 0;
            foreach (Composite composite in isNow.Values.OrderBy(o => o.name, StringComparer.OrdinalIgnoreCase))
            {
                if (only != null && !only.Contains(composite)) continue;
                if (!was.TryGetValue(composite.shortGUID, out Composite old)) continue;
                JObject diff = CompareComposite(now, composite, before, old, details, out int count);
                if (count == 0) continue;
                entityChanges += count;
                changed.Add(diff);
            }

            JObject result = new JObject()
            {
                ["composites_added"] = ListOf(added.Select(o => o.name).ToList(), 100),
                ["composites_removed"] = ListOf(removed.Select(o => o.name).ToList(), 100),
                ["changes"] = entityChanges,
            };
            if (renamed.Count != 0) result["composites_renamed"] = new JArray(renamed.Take(100));
            McpPaging.Page(call, changed, result, "composites_changed", o => o, 100);
            if (added.Count == 0 && removed.Count == 0 && renamed.Count == 0 && changed.Count == 0)
                call.Note("No differences in the script" + (only != null ? " of those composites" : "") + ".");
            return result;
        }

        private static JObject CompareComposite(Commands nowCommands, Composite now, Commands beforeCommands, Composite before, bool details, out int count)
        {
            const int max = 40;
            Dictionary<ShortGuid, Entity> was = before.GetEntities().GroupBy(o => o.shortGUID).ToDictionary(o => o.Key, o => o.First());
            Dictionary<ShortGuid, Entity> isNow = now.GetEntities().GroupBy(o => o.shortGUID).ToDictionary(o => o.Key, o => o.First());
            JArray added = new JArray(), removed = new JArray(), parameters = new JArray(), linksAdded = new JArray(), linksRemoved = new JArray(), other = new JArray();
            int addedCount = 0, removedCount = 0, parameterCount = 0, linksAddedCount = 0, linksRemovedCount = 0, otherCount = 0;
            string Name(Commands commands, Composite composite, Entity entity) => McpScript.EntityName(commands, composite, entity);
            string Link(Commands commands, Composite composite, Entity owner, EntityConnector link)
            {
                Entity target = composite.GetEntityByID(link.linkedEntityID);
                return Name(commands, composite, owner) + "." + McpScript.ParamName(link.thisParamID) + " -> " + (target != null ? Name(commands, composite, target) : McpScript.Id(link.linkedEntityID)) + "." + McpScript.ParamName(link.linkedParamID);
            }

            foreach (Entity entity in isNow.Values)
            {
                if (was.ContainsKey(entity.shortGUID)) continue;
                if (++addedCount <= max) added.Add(McpScript.Id(entity.shortGUID) + " " + Name(nowCommands, now, entity) + " (" + McpScript.TypeName(nowCommands, now, entity) + ")");
            }
            foreach (Entity entity in was.Values)
            {
                if (isNow.ContainsKey(entity.shortGUID)) continue;
                if (++removedCount <= max) removed.Add(McpScript.Id(entity.shortGUID) + " " + Name(beforeCommands, before, entity) + " (" + McpScript.TypeName(beforeCommands, before, entity) + ")");
            }
            foreach (Entity entity in isNow.Values)
            {
                if (!was.TryGetValue(entity.shortGUID, out Entity old)) continue;
                string label = Name(nowCommands, now, entity);
                //What it is
                if (entity is FunctionEntity f && old is FunctionEntity g && f.function != g.function && ++otherCount <= max)
                    other.Add(label + ": type " + McpScript.TypeName(beforeCommands, before, old) + " -> " + McpScript.TypeName(nowCommands, now, entity));
                if (entity is AliasEntity alias && old is AliasEntity oldAlias && !(alias.alias?.path ?? new ShortGuid[0]).SequenceEqual(oldAlias.alias?.path ?? new ShortGuid[0]) && ++otherCount <= max)
                    other.Add(label + ": the alias points elsewhere");
                if (entity is CAGEAnimation animation && old is CAGEAnimation oldAnimation && AnimationHash(animation) != AnimationHash(oldAnimation) && ++otherCount <= max)
                    other.Add(label + ": its tracks or keys changed (get_cage_animation shows them)");
                //Parameters
                Dictionary<ShortGuid, Parameter> nowParameters = entity.parameters.Where(o => o != null).GroupBy(o => o.name).ToDictionary(o => o.Key, o => o.First());
                Dictionary<ShortGuid, Parameter> oldParameters = old.parameters.Where(o => o != null).GroupBy(o => o.name).ToDictionary(o => o.Key, o => o.First());
                foreach (ShortGuid name in nowParameters.Keys.Union(oldParameters.Keys))
                {
                    nowParameters.TryGetValue(name, out Parameter a);
                    oldParameters.TryGetValue(name, out Parameter b);
                    if (a?.content == null && b?.content == null) continue;
                    if (a?.content != null && b?.content != null && a.content == b.content) continue;
                    //Some kinds compare by reference (a spline's points): compare what they hold
                    JToken beforeValue = b?.content == null ? null : SafeJson(b.content, beforeCommands);
                    JToken nowValue = a?.content == null ? null : SafeJson(a.content, nowCommands);
                    if (beforeValue != null && nowValue != null && JToken.DeepEquals(beforeValue, nowValue)) continue;
                    if (++parameterCount > max || !details) continue;
                    JObject change = new JObject() { ["entity"] = label, ["id"] = McpScript.Id(entity.shortGUID), ["parameter"] = McpScript.ParamName(name) };
                    change["before"] = beforeValue;
                    change["now"] = nowValue;
                    parameters.Add(change);
                }
                //Links out of it
                HashSet<Tuple<uint, uint, uint>> nowLinks = new HashSet<Tuple<uint, uint, uint>>(entity.childLinks.Select(LinkKey));
                HashSet<Tuple<uint, uint, uint>> oldLinks = new HashSet<Tuple<uint, uint, uint>>(old.childLinks.Select(LinkKey));
                foreach (EntityConnector link in entity.childLinks)
                    if (!oldLinks.Contains(LinkKey(link)) && ++linksAddedCount <= max)
                        linksAdded.Add(Link(nowCommands, now, entity, link));
                foreach (EntityConnector link in old.childLinks)
                    if (!nowLinks.Contains(LinkKey(link)) && ++linksRemovedCount <= max)
                        linksRemoved.Add(Link(beforeCommands, before, old, link));
            }
            //Links of entities that came or went count with them
            foreach (Entity entity in isNow.Values.Where(o => !was.ContainsKey(o.shortGUID)))
                linksAddedCount += entity.childLinks.Count;
            foreach (Entity entity in was.Values.Where(o => !isNow.ContainsKey(o.shortGUID)))
                linksRemovedCount += entity.childLinks.Count;

            count = addedCount + removedCount + parameterCount + linksAddedCount + linksRemovedCount + otherCount;
            JObject diff = new JObject() { ["composite"] = now.name, ["id"] = McpScript.Id(now.shortGUID) };
            void Put(string key, JArray list, int total)
            {
                if (total == 0) return;
                if (!details) { diff[key] = total; return; }
                if (total > list.Count) list.Add("... and " + (total - list.Count) + " more");
                diff[key] = list;
            }
            Put("entities_added", added, addedCount);
            Put("entities_removed", removed, removedCount);
            Put("parameters_changed", parameters, parameterCount);
            Put("links_added", linksAdded, linksAddedCount);
            Put("links_removed", linksRemoved, linksRemovedCount);
            Put("other", other, otherCount);
            return diff;
        }

        private static Tuple<uint, uint, uint> LinkKey(EntityConnector link) => Tuple.Create(link.thisParamID.AsUInt32, link.linkedEntityID.AsUInt32, link.linkedParamID.AsUInt32);

        private static JToken SafeJson(ParameterData data, Commands commands)
        {
            try { return McpValues.ToJson(data, commands); }
            catch (Exception) { return data.dataType.ToString(); }
        }

        private static long AnimationHash(CAGEAnimation animation)
        {
            long hash = 17;
            foreach (CAGEAnimation.Connection connection in animation.connections)
                hash = unchecked(hash * 31 + connection.target_track.GetHashCode() + connection.target_param.GetHashCode() * 7 + connection.target_sub_param.GetHashCode() * 13);
            foreach (CAGEAnimation.FloatTrack track in animation.floatTracks)
                foreach (CAGEAnimation.FloatTrack.Keyframe key in track.keyframes)
                    hash = unchecked(hash * 31 + key.time.GetHashCode() * 3 + key.value.GetHashCode() + (int)key.mode);
            hash = unchecked(hash * 31 + animation.eventTracks.Count);
            return hash;
        }

        private static object SaveLevel(McpCall call)
        {
            //build: true, false or 'auto' (a build only when the level needs one)
            JToken buildArgument = call.Token("build");
            bool auto = buildArgument?.Type == JTokenType.String && string.Equals(((string)buildArgument).Trim(), "auto", StringComparison.OrdinalIgnoreCase);
            bool build = !auto && call.Bool("build");
            LevelContent.BakeSelection bakers = ReadBakers(call);
            JObject needed = null;
            string level = McpEditor.UI(() =>
            {
                string name = McpEditor.RequireLevel().Level.Name;
                needed = BuildState();
                return name;
            });
            if (auto)
                build = (bool)needed["needs_build"];
            if (bakers != null && !build)
                throw McpError.Invalid(auto ? "build 'auto' found nothing that needs a build, so 'bakers' does not apply: pass build: true to build anyway." : "'bakers' chooses what a build rebuilds: pass build: true with it (a plain save runs no bakers).");
            bool gameWasRunning = EditorUtils.ThisInstallsGameRunning();

            bool saved = false;
            Action onSaved = () => saved = true;
            Singleton.OnSaved += onSaved;
            Stopwatch timer = Stopwatch.StartNew();
            Exception failure = null;
            using (SaveHeartbeat(call, build ? "Saving and building " + level : "Saving " + level))
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
                            //A build pumps the message loop while it writes, and the user can work meanwhile: the steps they record
                            //and the windows they open are theirs (the save itself records nothing, and asks only in message boxes)
                            using (build ? McpDialogs.UserInput() : null)
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
            if (auto)
            {
                result["build_was_needed"] = needed["needs_build"];
                if (needed["reasons"] != null) result["build_reasons"] = needed["reasons"];
            }
            //The editor's save closes the game (it holds the level's files open)
            if (gameWasRunning && !EditorUtils.ThisInstallsGameRunning())
            {
                result["closed_game"] = true;
                call.Note("Alien: Isolation was running and was closed for the save (Live Link disconnects with it): launch_game starts it again.");
            }
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
            {
                JObject after = McpEditor.UI(() => BuildState());
                result["needs_build"] = after["needs_build"];
                if ((bool)after["needs_build"])
                {
                    result["build_reasons"] = after["reasons"];
                    call.Note("Saved without a build, but the level has changes the game only takes after a Save & Build (build_reasons): save_level with build=true before playing.");
                }
            }
            return result;
        }

        /// <summary>A heartbeat for a save that also says how far the level's files are through (the save's own ticks).</summary>
        private static IDisposable SaveHeartbeat(McpCall call, string what)
        {
            Level level = null;
            try { level = McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level); } catch (McpError) { }
            int ticks = 0;
            Action tick = () => Interlocked.Increment(ref ticks);
            if (level != null) level.OnSaveTick += tick;
            Stopwatch timer = Stopwatch.StartNew();
            //Never through the UI thread: the save holds it
            System.Threading.Timer ticker = new System.Threading.Timer(_ =>
            {
                //The level's file writes, out of the Level.NumberOfTicks a save makes (a build's bakers run before them). The progress
                //value stays the elapsed seconds, which only ever rises, as clients require
                int done = Volatile.Read(ref ticks);
                call.Progress(what + (done != 0 ? " (" + Math.Min(done, Level.NumberOfTicks) + " of " + Level.NumberOfTicks + " files written)" : "") + " (" + (int)timer.Elapsed.TotalSeconds + " s)", timer.Elapsed.TotalSeconds);
            }, null, 0, 5000);
            return new Disposer(() =>
            {
                ticker.Dispose();
                if (level != null) level.OnSaveTick -= tick;
            });
        }

        private static object LaunchGame(McpCall call)
        {
            PatchManager.Platform platform = Singleton.Platform;
            if (platform != PatchManager.Platform.STEAM && platform != PatchManager.Platform.EPIC_GAMES_STORE && platform != PatchManager.Platform.GOG)
                throw new McpError("OpenCAGE can only launch the game for Steam, Epic and GOG installs (this is " + platform + ").");
            string given = call.Str("level");
            string level = given != null ? NormaliseLevel(given) : McpEditor.UI(() => Singleton.Editor.CompositeBrowser?.Content?.Level?.Name);
            if (level == null || level == "MENU" || EditorUtils.IsFrontend(level) || EditorUtils.IsFrontend("PRODUCTION/" + level))
                level = EditorUtils.FrontendLevel;
            else
                //The name goes into the game's executable: it has to be a level that exists, spelled as the game does
                level = McpLevels.Resolve(level, call: call);
            if (level.Length > PatchManager.MaxLaunchMapNameLength)
                throw McpError.Invalid("'" + level + "' is too long to launch into (the most is " + PatchManager.MaxLaunchMapNameLength + " characters).");
            bool menu = EditorUtils.IsFrontend(level);

            string save = (call.Str("save") ?? "none").Trim().ToLowerInvariant();
            if (save != "none" && save != "save" && save != "build" && save != "auto")
                throw McpError.Invalid("'save' takes none, save, build or auto.");
            string waitFor = (call.Str("wait_for") ?? "none").Trim().ToLowerInvariant();
            if (waitFor != "none" && waitFor != "process" && waitFor != "level" && waitFor != "playing")
                throw McpError.Invalid("'wait_for' takes none, process, level or playing.");
            int timeoutSeconds = Math.Max(5, Math.Min(600, call.Int("timeout_seconds", 180)));
            if (call.Has("timeout_seconds") && waitFor == "none")
                call.Note("timeout_seconds only applies with wait_for.");
            bool? skipFrontend = call.Has("skip_frontend") ? call.Bool("skip_frontend") : (bool?)null;
            bool dryRun = call.Bool("dry_run");
            bool liveLinkWait = waitFor == "level" || waitFor == "playing";
            if (liveLinkWait && !(SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink) && global::OpenCAGE.LaunchGame.ScriptingHelpersAvailable()))
                throw new McpError(McpErrorCodes.Refused, "wait_for '" + waitFor + "' needs Live Link, which the game serves only when launched with launch_options live_link: true (Steam build). Turn it on with launch_options, or wait_for 'process'. Nothing was launched.");

            //Launching patches AI.exe and the level list packages, which a save (its release-build tail) and a level
            //creation or restore also write: never alongside one of those
            string open = null;
            bool dirty = McpEditor.UI(() =>
            {
                if (UndoStack.Current.Blocked)
                    throw McpError.Busy("OpenCAGE is saving or writing a level. Launch when it has finished (get_editor_state shows 'saving').");
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                    throw McpError.Busy("A level backup is running. Launch when it has finished.");
                if (Singleton.Editor.IsLevelLoadInProgress)
                    throw McpError.Busy("A level is still loading. Launch when it is open.");
                open = Singleton.Editor.CompositeBrowser?.Content?.IsLevelDataLoaded == true ? Singleton.Editor.CompositeBrowser.Content.Level.Name : null;
                return open == level && DirtyTracker.IsDirty;
            });
            bool launchesOpen = open != null && open == level;
            if (save != "none" && !launchesOpen)
                call.Note("'save' only saves the open level (" + (open ?? "none is open") + "), which is not the one launched: nothing was saved.");

            JObject result = new JObject();
            //Saved first when asked (it closes a running game itself), so the build check below sees the result
            if (launchesOpen && save != "none" && !dryRun)
            {
                JObject buildNow = McpEditor.UI(() => BuildState());
                bool needsBuild = (bool)buildNow["needs_build"];
                bool doBuild = save == "build" || (save == "auto" && needsBuild);
                if (doBuild || save == "save" || dirty)
                {
                    JObject saved = Nested(call, "save_level", new JObject() { ["build"] = doBuild });
                    result["saved"] = saved;
                    dirty = false;
                }
            }
            else if (dirty)
                call.Note(level + " has unsaved changes in the editor, which the game will not see (save: 'save' or 'auto' saves first).");

            //A level whose script changed in ways only a build passes on would run older data
            if (launchesOpen)
            {
                List<string> reasons = McpEditor.UI(() => BuildTracker.Reasons(8, out _));
                bool tracked = McpEditor.UI(() => BuildTracker.NeedsBuild && !(BuildTracker.SavedWithoutBuildOnDisk && reasons.Count == 1));
                if (tracked && !call.Bool("allow_stale_build"))
                {
                    if (!dryRun)
                        throw new McpError(McpErrorCodes.Refused, level + " has changes the game only takes after a Save & Build: " + string.Join("; ", reasons) + ". save_level with build=true first (or launch_game save: 'build'), or pass allow_stale_build: true to play the older data. Nothing was launched" + (result["saved"] != null ? " (the level was saved, without a build)." : "."));
                    call.Note("A real launch would be refused: the level needs a Save & Build (" + string.Join("; ", reasons) + ").");
                }
                else if (reasons.Count != 0)
                {
                    result["build_reasons"] = new JArray(reasons);
                    call.Note("The game may run data older than the script (" + reasons[0] + "): Save & Build if something is missing in game.");
                }
            }
            else if (!menu && SavedWithoutBuildOnDisk(level))
                call.Note(level + " was last saved without a build after its last Save & Build: the game may run data older than its script.");

            if (dryRun)
            {
                result["dry_run"] = true;
                result["level"] = menu ? "menu" : level;
                result["skip_frontend"] = skipFrontend ?? SettingsManager.GetBool(Settings.SkipFrontend);
                result["game_running"] = EditorUtils.ThisInstallsGameRunning();
                result["patches"] = McpLevelTools.PatchSettings(skipFrontend);
                result["wait_for"] = waitFor;
                if (save != "none") result["would_save"] = launchesOpen ? save : "nothing (the launched level is not open)";
                return result;
            }

            List<string> closed = McpLevelTools.CloseGame();
            if (closed.Count != 0)
            {
                result["closed_game"] = new JArray(closed);
                call.Note("The game was already running (" + string.Join(", ", closed) + ") and was closed first.");
            }
            //The launch_options patches as set (a verified AI.exe loses them), with skip_frontend as asked for this launch
            List<string> patchFailures = McpLevelTools.ApplyPatchSettings(platform, skipFrontend);
            //The scripting helpers (runtime utils ASI) go in or come out as launch_options ask, as the Launch Game window does
            string runtimeUtilsProblem = global::OpenCAGE.LaunchGame.ApplyRuntimeUtils();
            if (runtimeUtilsProblem != null)
                call.Note(runtimeUtilsProblem + " In-game scripting helpers (debug text, hot reload, Live Link) will not work this run.");
            //The level to start in, the integrity check and the popup are what a launch needs; the rest are options
            bool launchPatched = PatchManager.PatchLaunchMode(platform, Singleton.PathToAI, level) && !patchFailures.Contains("file integrity check") && !patchFailures.Contains("popup message");
            if (!launchPatched)
                call.Note("AI.exe could not be patched to start in " + level + " (is it read-only, or still held open?): the game may start at its menu, or refuse edited files.");
            List<string> optional = patchFailures.Where(o => o != "file integrity check" && o != "popup message").ToList();
            if (optional.Count != 0)
                call.Note("These launch_options AI.exe patches could not be written this time, so the game runs without them as set: " + string.Join(", ", optional) + ".");
            PatchManager.UpdateLevelListInPackages(platform, Singleton.PathToAI);
            //Live Link connects by itself once the game is up when Connect to Game is on: waiting through it needs that
            if (liveLinkWait && !SettingsManager.GetBool(Settings.RuntimeUtilsOpt) && platform == PatchManager.Platform.STEAM)
            {
                McpEditor.UI(() =>
                {
                    SettingsManager.SetBool(Settings.RuntimeUtilsOpt, true);
                    McpEditor.Editor.ApplySnapIncrementChange(new[] { Settings.RuntimeUtilsOpt });
                });
                call.Note("Connect to Game (the Live Link button) was turned on so OpenCAGE connects once the game is up.");
            }
            Stopwatch launched = Stopwatch.StartNew();
            if (platform == PatchManager.Platform.STEAM)
                Process.Start("steam://rungameid/214490");
            else
                Process.Start(new ProcessStartInfo() { FileName = Singleton.PathToAI + "/AI.exe", WorkingDirectory = Singleton.PathToAI });

            result["launched"] = true;
            result["level"] = menu ? "menu" : level;
            bool skipped = skipFrontend ?? SettingsManager.GetBool(Settings.SkipFrontend);
            result["skip_frontend"] = skipped;
            if (!skipped && !menu)
                call.Note("The frontend is not skipped: the level's scripts start, but the game waits at PRESS ANY BUTTON until someone presses one (skip_frontend: true starts it playing).");
            if (waitFor != "none")
                result["wait"] = WaitForGame(call, waitFor, TimeSpan.FromSeconds(timeoutSeconds), launched, launchesOpen);
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

        /// <summary>Run another tool as part of this call (its notes become this call's), returning its result.</summary>
        internal static JObject Nested(McpCall call, string tool, JObject arguments)
        {
            McpTool found = McpTools.Find(tool) ?? throw new McpError(McpErrorCodes.Failed, "The " + tool + " tool is missing from this build.");
            McpCall nested = new McpCall(found, arguments, call.Cancel, (text, done, total) => call.Progress(text, done, total));
            object result = found.Run(nested);
            foreach (string note in nested.Notes) call.Note(note);
            return result as JObject ?? (result == null ? new JObject() : JObject.FromObject(result));
        }

        /// <summary>
        /// A level on disk saved without a build after its last Save & Build: its script (written with the COMMANDS.BIN an
        /// OpenCAGE save adds; retail ships the PAK alone) is newer than the radiosity collision mapping, which only a build writes.
        /// </summary>
        internal static bool SavedWithoutBuildOnDisk(string level)
        {
            try
            {
                string script = new Level(Singleton.PathToAI + "/DATA/ENV/" + level, Singleton.Global, false).CommandsFilepath;
                string world = Path.GetDirectoryName(script);
                string radiosity = Path.Combine(world, "RADIOSITY_COLLISION_MAPPING.BIN");
                return File.Exists(script) && File.Exists(Path.Combine(world, "COMMANDS.BIN")) && File.Exists(radiosity)
                    && File.GetLastWriteTimeUtc(script) > File.GetLastWriteTimeUtc(radiosity).AddSeconds(60);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>launch_game's wait_for: poll the process, then (Live Link) the level and its scripts, until reached or the timeout.</summary>
        private static JObject WaitForGame(McpCall call, string waitFor, TimeSpan timeout, Stopwatch launched, bool launchesOpen)
        {
            JObject wait = new JObject() { ["for"] = waitFor };
            string reached = "nothing";
            Commands open = launchesOpen ? McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands) : null;
            int lastSecond = -1;
            while (launched.Elapsed < timeout)
            {
                call.ThrowIfCancelled();
                int second = (int)launched.Elapsed.TotalSeconds;
                if (EditorUtils.ThisInstallsGameRunning())
                {
                    if (reached == "nothing") reached = "process";
                    if (waitFor == "process") break;
                    bool connected = global::OpenCAGE.RuntimeUtilsConnection.Send.Connected || global::OpenCAGE.RuntimeUtilsConnection.Send.Start();
                    wait["live_link_connected"] = connected;
                    if (connected)
                    {
                        try
                        {
                            System.Threading.Tasks.Task<global::OpenCAGE.RuntimeUtilsConnection.LiveLink.GameStatus> asked = global::OpenCAGE.RuntimeUtilsConnection.LiveLink.Status();
                            if (asked.Wait(TimeSpan.FromSeconds(3)) && asked.Result.Reply.Ok)
                            {
                                global::OpenCAGE.RuntimeUtilsConnection.LiveLink.GameStatus status = asked.Result;
                                wait["level_running"] = status.LevelRunning;
                                wait["playing"] = status.Playing;
                                if (!string.IsNullOrEmpty(status.RootCompositeName)) wait["running_root_composite"] = status.RootCompositeName;
                                if (open != null && status.LevelRunning) wait["running_level_open_here"] = status.IsRunning(open);
                                if (!string.IsNullOrEmpty(status.LoadingReason)) wait["loading"] = status.LoadingReason;
                                if (status.LevelRunning && reached == "process") reached = "level";
                                if (status.LevelRunning && status.Playing) reached = "playing";
                                if (reached == waitFor || reached == "playing") break;
                            }
                        }
                        catch (Exception) { }
                    }
                }
                if (second != lastSecond && second % 5 == 0)
                {
                    lastSecond = second;
                    call.Progress("Waiting for the game: " + reached + " so far (" + second + " s)", second, timeout.TotalSeconds);
                }
                Thread.Sleep(1000);
            }
            wait["reached"] = reached;
            wait["seconds"] = Math.Round(launched.Elapsed.TotalSeconds, 1);
            bool done = reached == waitFor || (waitFor == "level" && reached == "playing");
            wait["done"] = done;
            if (!done)
                call.Note("The game had reached '" + reached + "' after " + (int)timeout.TotalSeconds + " s, not '" + waitFor + "'" +
                    (reached == "nothing" ? " (AI.exe never started: is Steam running and signed in?)" : reached == "process" ? " (Live Link did not answer yet, or the level is still loading)" : " (its scripts are paused: the frontend, a loading screen or the pause menu)") +
                    ". get_editor_state's game block and runtime_utils game_status show how it gets on.");
            return wait;
        }

        private static string NormaliseLevel(string level) => (level ?? "").Replace('\\', '/').Trim().Trim('/').ToUpperInvariant();

        /// <summary>The level a backup tool is about, named as load_level takes it (full name, or its last part).</summary>
        internal static string LevelArgument(McpCall call)
        {
            string level = call.Has("level") ? NormaliseLevel(call.Str("level")) : McpEditor.UI(() => Singleton.Editor.CompositeBrowser?.Content?.Level?.Name);
            if (string.IsNullOrEmpty(level))
                throw McpError.Invalid("Say which level ('level'; no level is open).");
            McpError unknown;
            try { return McpLevels.Resolve(level, call: call); }
            catch (McpError e) when (e.Code == McpErrorCodes.NotFound) { unknown = e; }

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
            //A custom level deleted with delete_level keeps its backups under its own name
            if (!level.Contains("/") && HasBackups(level) && !Directory.Exists(Path.Combine(env, "PRODUCTION", level)))
                return level;
            if (HasBackups(shortName))
                return "PRODUCTION/" + shortName;
            throw new McpError(McpErrorCodes.NotFound, unknown.Message + " No backups are kept under that name either.");
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
            if (McpEditor.UI(() => string.Equals(Singleton.Editor.CompositeBrowser?.Content?.Level?.Name, level, StringComparison.OrdinalIgnoreCase) && DirtyTracker.IsDirty))
                call.Note("The open level has unsaved changes, which are not in this backup.");
            AlienLevel.AlienBackup made = MakeBackup(call, level, name);
            return new JObject() { ["level"] = level, ["backup"] = made == null ? null : DescribeBackup(made) };
        }

        /// <summary>Back up one level under the single-level guard every save respects, and refresh a Manage Backups window showing it.</summary>
        internal static AlienLevel.AlienBackup MakeBackup(McpCall call, string level, string name)
        {
            if (LevelBackupWindowBusy(level))
                throw McpError.Busy("A Manage Backups window is backing up or restoring " + level + ". Try again when it has finished.");
            McpEditor.UI(() =>
            {
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                    throw McpError.Busy("A backup is already running.");
                if (UndoStack.Current.Blocked)
                    throw McpError.Busy("The level is being saved.");
                Singleton.CurrentBackupState = Singleton.BackupState.SINGLE_LEVEL;
                Singleton.BackupLevel = level;
            });
            AlienLevel.AlienBackup made;
            try
            {
                call.Progress("Backing up " + level);
                using (Heartbeat(call, "Backing up " + level))
                {
                    AlienLevel archive = OpenBackups(level);
                    archive.CreateBackup(name);
                    made = archive.Newest;
                }
            }
            finally
            {
                McpEditor.UI(() => { Singleton.CurrentBackupState = Singleton.BackupState.NONE; Singleton.BackupLevel = ""; });
            }
            RefreshBackupWindows(level);
            return made;
        }

        internal static JObject DescribeBackup(AlienLevel.AlienBackup backup)
        {
            DateTime utc = AlienLevel.DateUtc(backup);
            return new JObject()
            {
                ["id"] = backup.ID,
                ["name"] = backup.Name,
                ["date_utc"] = utc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["age_hours"] = Math.Round((DateTime.UtcNow - utc).TotalHours, 1),
                ["files"] = backup.GUIDs?.Count ?? 0,
            };
        }

        /// <summary>Whether a Manage Backups window is in the middle of its own backup or restore of the level.</summary>
        private static bool LevelBackupWindowBusy(string level) => McpEditor.UI(() => LevelBackupManager.OpenOn(level).Any(o => o.IsBusy));

        /// <summary>A Manage Backups window keeps its own copy of the archive's list: show it what was just written.</summary>
        private static void RefreshBackupWindows(string level)
        {
            try { McpEditor.UI(() => { foreach (LevelBackupManager window in LevelBackupManager.OpenOn(level)) window.ReloadArchive(); }); }
            catch (McpError) { }
        }

        private static object ListBackups(McpCall call)
        {
            string level = LevelArgument(call);
            AlienLevel backups = OpenBackups(level);
            List<AlienLevel.AlienBackup> all = backups.Backups;
            List<int> newestFirst = Enumerable.Range(0, all.Count).Reverse().ToList();
            bool changed = call.Bool("changed");
            JObject result = new JObject() { ["level"] = level };
            McpPaging.Page(call, newestFirst, result, "backups", i =>
            {
                AlienLevel.AlienBackup backup = all[i];
                JObject entry = DescribeBackup(backup);
                entry["changed_files"] = i == 0 ? (backup.GUIDs?.Count ?? 0) : backups.CalculateDiff(all[i - 1], backup);
                if (changed)
                {
                    Dictionary<string, string> after = backups.PathsOf(backup, out _);
                    Dictionary<string, string> before = i == 0 ? new Dictionary<string, string>() : backups.PathsOf(all[i - 1], out _);
                    entry["changed"] = ListOf(AlienLevel.Differences(before, after).Select(Slashes).OrderBy(o => o).ToList(), 50);
                }
                return entry;
            }, 50);
            if (call.Bool("pending_changes"))
            {
                using (Heartbeat(call, "Comparing " + level + " with its newest backup"))
                {
                    try { result["pending_changes"] = backups.CalculateDiff(all.Count == 0 ? null : all[all.Count - 1]); }
                    catch (DirectoryNotFoundException) { throw new McpError(level + "'s folder is missing, so there is nothing on disk to compare."); }
                }
            }
            if (all.Count == 0)
                call.Note(level + " has no backups yet: create_backup makes one.");
            return result;
        }

        /// <summary>The level-relative paths ('\' separated) a 'files' list picks out of the candidates: exact paths, or '*' / '**' patterns.</summary>
        private static List<string> MatchFiles(List<string> patterns, IEnumerable<string> candidates, string argument)
        {
            List<string> known = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            List<string> chosen = new List<string>();
            foreach (string given in patterns)
            {
                string pattern = (given ?? "").Trim().Replace('/', '\\').Trim('\\');
                if (pattern.Length == 0)
                    throw McpError.Invalid("'" + argument + "' has an empty path.");
                string regex = "^" + Regex.Escape(pattern).Replace("\\*\\*", "\u0001").Replace("\\*", "[^\\\\]*").Replace("\u0001", ".*").Replace("\\?", ".") + "$";
                List<string> hits = known.Where(o => Regex.IsMatch(o, regex, RegexOptions.IgnoreCase)).ToList();
                if (hits.Count == 0)
                    throw McpError.NotFound("file in the level or the backup", given, known.Select(Slashes), "Paths are relative to the level folder, e.g. 'WORLD/COMMANDS.PAK' or 'WORLD/*'.");
                chosen.AddRange(hits.Where(o => !chosen.Contains(o, StringComparer.OrdinalIgnoreCase)));
            }
            return chosen;
        }

        /// <summary>A level's files on disk, level-relative ('\' separated).</summary>
        private static IEnumerable<string> CurrentFiles(string level)
        {
            string folder = Path.Combine(Singleton.PathToAI, "DATA", "ENV", level.Replace('/', '\\'));
            if (!Directory.Exists(folder)) return Enumerable.Empty<string>();
            string root = Path.GetFullPath(folder).TrimEnd('\\') + "\\";
            return Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories).Select(o => Path.GetFullPath(o).Substring(root.Length));
        }

        private static string Slashes(string path) => (path ?? "").Replace('\\', '/');

        /// <summary>A list as JSON, cut to <paramref name="max"/> with a last entry saying how many more there are.</summary>
        private static JArray ListOf(List<string> items, int max)
        {
            JArray list = new JArray(items.Take(max));
            if (items.Count > max) list.Add("... and " + (items.Count - max) + " more");
            return list;
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
            foreach (JToken backup in made)
                RefreshBackupWindows((string)backup["level"]);
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
                throw new McpError(McpErrorCodes.NotFound, "No backup " + string.Join(", ", missing) + " of " + level + " (list_backups shows them; its ids are " + string.Join(", ", archive.Backups.Select(o => o.ID).Reverse().Take(10)) + (archive.Backups.Count > 10 ? ", ..." : "") + "). Nothing was deleted.");
            if (LevelBackupWindowBusy(level))
                throw McpError.Busy("A Manage Backups window is backing up or restoring " + level + ". Try again when it has finished.");
            McpEditor.UI(() =>
            {
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                    throw McpError.Busy("A backup is running. Try again when it has finished.");
                Singleton.CurrentBackupState = Singleton.BackupState.SINGLE_LEVEL;
                Singleton.BackupLevel = level;
            });
            JArray deleted = new JArray();
            try
            {
                foreach (long id in ids)
                {
                    AlienLevel.AlienBackup backup = archive.Backups.First(o => o.ID == id);
                    JObject described = DescribeBackup(backup);
                    archive.DeleteBackup(id);
                    deleted.Add(described);
                }
            }
            finally
            {
                McpEditor.UI(() => { Singleton.CurrentBackupState = Singleton.BackupState.NONE; Singleton.BackupLevel = ""; });
            }
            RefreshBackupWindows(level);
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
            //Any shape a path is given in: an array, 'A/B', or a result's own {from, path, ids}
            JToken path = call.Token("path");
            List<string> steps = path == null || (path is JArray empty && empty.Count == 0) ? new List<string>() : McpScript.PathSteps(path);
            instances = new List<Entity>();
            if (steps.Count == 0)
            {
                if (!call.Has("composite"))
                    throw new McpError("Say which composite: 'composite' (its path or id; 'root' for the level's root), or a 'path' of instances to step through from the root.");
                entry = McpScript.FindComposite(commands, call.Str("composite", required: true));
                return entry;
            }
            //A path a result gave says where it starts ('from'); 'composite' still decides when given
            string from = call.Str("composite") ?? (path is JObject given && given["from"]?.Type == JTokenType.String ? (string)given["from"] : null) ?? "root";
            entry = McpScript.FindComposite(commands, from);
            return ResolveInstancePath(commands, entry, steps, instances);
        }

        /// <summary>
        /// The steps without a first one naming where they start: 'root' when they start at the root (as a path written from
        /// the root may begin), or the entry composite's own path (as instance paths were once given) - unless an entity there
        /// really has that name.
        /// </summary>
        private static IList<string> WithoutStart(Commands commands, Composite entry, IList<string> steps)
        {
            if (steps.Count < 2) return steps;
            string first = (steps[0] ?? "").Trim();
            bool namesStart = (entry == commands.EntryPoints[0] && string.Equals(first, "root", StringComparison.OrdinalIgnoreCase))
                || string.Equals(McpScript.NormalisePath(first), McpScript.NormalisePath(entry.name), StringComparison.OrdinalIgnoreCase);
            if (!namesStart || entry.GetEntities().Any(o => string.Equals(McpScript.EntityName(commands, entry, o), first, StringComparison.OrdinalIgnoreCase)))
                return steps;
            return steps.Skip(1).ToList();
        }

        /// <summary>Step through instance entities from a composite: each step names an instance in the composite the previous one places.</summary>
        internal static Composite ResolveInstancePath(Commands commands, Composite entry, IList<string> steps, List<Entity> instances)
        {
            steps = WithoutStart(commands, entry, steps);
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
            if (display == null || display.IsDisposed || !display.Populated)
                display = editor.CompositeBrowser?.LoadComposite(entry) ?? display;
            if (display == null || display.IsDisposed)
                throw new McpError("The editor's composite view is not open.");

            List<uint> guids = instances.Select(o => o.shortGUID.AsUInt32).ToList();
            Func<Entity, Composite> child = entity => McpScript.InstancedComposite(commands, entity);
            List<Entity> chosen = select.Where(o => o != null).Distinct().Take(64).ToList();
            //One step back for the user, however many moves it takes: the path is walked from the scene's root, so the entry is
            //made the root first (the viewer builds that scene) unless it is the root already
            bool opened = display.NavigateAsOneStep(() =>
            {
                display.OpenAsSceneRoot(entry);
                if (chosen.Count == 1)
                {
                    guids.Add(chosen[0].shortGUID.AsUInt32);
                    return display.ApplyViewerSelectionPath(entry, guids, true, child);
                }
                bool walked = display.ApplyViewerSelectionPath(entry, guids, false, child);
                if (walked && chosen.Count > 1)
                    McpScriptEdit.Show(target, chosen);
                return walked;
            });
            if (!opened || display.Composite != target)
                throw new McpError("OpenCAGE could not open " + target.name + " through that path.");
        }


        /// <summary>An instance path as every result gives one (<see cref="McpRegion.ChainJson(Commands, IList{Entity}, Composite)"/>): {path, ids}, with 'from' when it does not start at the root - which 'path' arguments take back as it is.</summary>
        private static JObject DescribeInstancePath(Commands commands, Composite entry, IList<Entity> instances) => McpRegion.ChainJson(commands, instances, entry);

        /// <summary>The main window's status bar text (not the item saying an assistant is at work, which is the caller).</summary>
        private static string StatusText(CommandsEditor editor)
        {
            foreach (StatusStrip strip in editor.Controls.OfType<StatusStrip>())
                foreach (ToolStripItem item in strip.Items)
                    if (item != editor.AiAssistantStatusItem && !string.IsNullOrWhiteSpace(item.Text))
                        return item.Text;
            return null;
        }
        #endregion

        #region Undo
        /// <summary>
        /// Undo or redo 'steps' steps, one UI step each. <paramref name="check"/> (UI thread) is asked before each, with how many
        /// went before it: the user can make a step of their own between two, so whose step is next is read as it is taken. An
        /// error it returns refuses the call before the first step, and stops it after a later one (saying so in the result).
        /// </summary>
        internal static object Step(McpCall call, bool undo, Func<int, McpError> check = null)
        {
            int steps = Math.Max(1, call.Int("steps", 1));
            List<string> done = new List<string>();
            McpError stopped = null;
            for (int i = 0; i < steps; i++)
            {
                int index = i;
                string label = McpEditor.UI(() =>
                {
                    McpEditor.RequireLevel();
                    //While an edit of the editor's own is open or applying, CanUndo/CanRedo read false: say so, not "nothing to undo"
                    McpEditor.RequireUndoIdle();
                    UndoStack stack = UndoStack.Current;
                    if (undo ? !stack.CanUndo : !stack.CanRedo)
                        return null;
                    stopped = check?.Invoke(index);
                    if (stopped != null)
                    {
                        if (index == 0) throw stopped;
                        return null;
                    }
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
            JObject result = new JObject() { [undo ? "undone" : "redone"] = new JArray(done) };
            if (stopped != null)
                result["stopped"] = "Stopped after " + done.Count + " of " + steps + ": " + stopped.Message;
            result["state"] = McpEditor.UI(() => State())["undo"];
            return result;
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

    /// <summary>
    /// Level names as every tool takes them: the full name list_levels gives ('PRODUCTION/SCI_HUB', 'PRODUCTION/DLC/CHALLENGEMAP1',
    /// 'MYLEVEL'), in any case and with either slash, or its last part when only one level ends with it ('SCI_HUB'). A name
    /// two levels share, or one no level has, is refused with the candidates.
    /// </summary>
    internal static class McpLevels
    {
        public static string Tidy(string level) => (level ?? "").Replace('\\', '/').Trim().Trim('/').ToUpperInvariant();

        public static string Leaf(string level)
        {
            string tidy = Tidy(level);
            int at = tidy.LastIndexOf('/');
            return at < 0 ? tidy : tidy.Substring(at + 1);
        }

        /// <summary>
        /// A level of this install. <paramref name="allowFrontend"/>: PRODUCTION/FRONTEND (the menu, which cannot be
        /// edited) counts too. <paramref name="call"/>, when given, gets a note when the name is also another level's last part.
        /// </summary>
        public static string Resolve(string given, bool allowFrontend = false, McpCall call = null, string argument = "level")
        {
            string wanted = Tidy(given);
            if (wanted.Length == 0)
                throw McpError.Invalid("'" + argument + "' is empty: give a level name (list_levels shows them).");
            if (!allowFrontend && (EditorUtils.IsFrontend(wanted) || EditorUtils.IsFrontend("PRODUCTION/" + wanted)))
                throw new McpError(McpErrorCodes.Refused, "FRONTEND is the game's menu level and cannot be edited. create_level makes a new level from it.");
            List<string> levels = (allowFrontend ? Level.GetLevels(Singleton.PathToAI) : EditorUtils.GetEditableLevels()).Select(Tidy).ToList();
            string exact = levels.FirstOrDefault(o => o == wanted);
            //'SCI_HUB', or 'DLC/CHALLENGEMAP1': the end of a full name
            List<string> byLeaf = levels.Where(o => o != wanted && o.EndsWith("/" + wanted, StringComparison.Ordinal)).ToList();
            if (exact != null)
            {
                if (byLeaf.Count != 0)
                    call?.Note("'" + wanted + "' is the custom level " + exact + "; " + string.Join(" and ", byLeaf) + (byLeaf.Count == 1 ? " has" : " have") + " the same last part - give the full name to mean " + (byLeaf.Count == 1 ? "it" : "one of them") + ".");
                return exact;
            }
            if (byLeaf.Count == 1)
                return byLeaf[0];
            if (byLeaf.Count > 1)
                throw McpError.Ambiguous("levels", given, byLeaf.Select(o => new JObject() { ["name"] = o }), "Give the full name.");
            //A part of a name: say which levels it could be
            List<string> containing = levels.Where(o => o.Contains(wanted)).ToList();
            if (containing.Count != 0 && containing.Count <= 12)
                throw new McpError(McpErrorCodes.NotFound, "There is no level '" + given + "'. Levels whose name contains it: " + McpNames.Quote(containing) + ".") { Candidates = new JArray(containing) };
            List<string> near = Near(levels, wanted);
            throw new McpError(McpErrorCodes.NotFound, "There is no level '" + given + "'." + (near.Count != 0 ? " Did you mean " + McpNames.Quote(near) + "?" : "") + " list_levels shows them.") { Candidates = near.Count != 0 ? new JArray(near) : null };
        }

        /// <summary>
        /// Level names near a mistyped one: by the whole name, by its last part, or with a typo inside the last part
        /// ('HOSPTAL' is two letters from part of 'SCI_HOSPITALUPPER').
        /// </summary>
        public static List<string> Near(List<string> levels, string query)
        {
            string wanted = Tidy(query);
            List<string> near = McpNames.Similar(levels, wanted, 6);
            if (near.Count != 0) return near;
            List<string> leaves = McpNames.Similar(levels.Select(Leaf), Leaf(wanted), 6);
            near = levels.Where(o => leaves.Contains(Leaf(o))).Take(6).ToList();
            if (near.Count != 0) return near;
            string part = Leaf(wanted);
            int allowed = Math.Max(1, part.Length / 4);
            return levels.Where(o =>
            {
                string leaf = Leaf(o);
                for (int length = Math.Max(1, part.Length - 1); length <= part.Length + 1; length++)
                    for (int start = 0; start + length <= leaf.Length; start++)
                        if (McpNames.Distance(leaf.Substring(start, length), part, allowed) <= allowed)
                            return true;
                return false;
            }).Take(6).ToList();
        }

        /// <summary>Whether this name (a new level's folder) would clash with a level, its last part, or a backup archive under that name.</summary>
        /// <param name="forTools">The message names the tool that restores backups (false: the editor's Create Level window, which does not).</param>
        public static string Clash(string newName, bool forTools = true)
        {
            string wanted = Tidy(newName);
            List<string> levels = Level.GetLevels(Singleton.PathToAI).Select(Tidy).ToList();
            string same = levels.FirstOrDefault(o => o == wanted || Leaf(o) == wanted);
            if (same != null)
                return same == wanted ? "A level called '" + wanted + "' already exists." : "'" + wanted + "' is the last part of " + same + "'s name: a level called that would share its LEVEL_TEXT_DATABASES.XML block and old-style backups, and plain '" + wanted + "' would mean either. Choose another name.";
            if (Directory.Exists(Path.Combine(Singleton.PathToAI, "DATA", "ENV", wanted)))
                return "DATA/ENV/" + wanted + " already exists (a folder that is not a complete level - a failed create or copy?). Delete or rename it, or choose another name.";
            if (File.Exists(Path.Combine(Singleton.PathToAI, "DATA", "MODTOOLS", "BACKUPS", wanted + ".BAK")))
                return "There are backups under the name '" + wanted + "' (a level that was deleted, or a campaign level's old-style backups): a new level of that name would take them over. Choose another name" + (forTools ? ", or restore_backup level '" + wanted + "'." : ".");
            return null;
        }
    }
}
