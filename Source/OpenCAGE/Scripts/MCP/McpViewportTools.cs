using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
//Not the whole namespace: its Send (the game's connection) would clash with the viewport's
using LiveLinkCameraSync = OpenCAGE.RuntimeUtilsConnection.LiveLinkCameraSync;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Seeing and steering the 3D viewport: a picture of it, where its camera is and what lies under a point of it
    /// (asked of the viewer there and then: VIEWPORT_QUERY), putting the camera somewhere or looking through an entity,
    /// what it is set to show, placing and snapping entities with its raycasts, turning it on and off, and the
    /// composite previews it takes.
    /// </summary>
    /// <remarks>
    /// The viewport is a separate process (the Godot level viewer) spoken to by packets. Everything here goes
    /// through the packets and settings the editor's own toolbar, Options menu and context menu use, so the
    /// editor's controls stay in step with what a tool changed. Its view settings are editor settings,
    /// remembered across sessions; the only level data changed here is by place_in_viewport and snap_to_floor,
    /// which the editor records on its undo history exactly as a drop or Shift+End would (labelled 'AI: ...',
    /// as these tools' steps are). Camera placements the tools make (a field of view, an entity followed) last
    /// until the user moves the camera.
    /// </remarks>
    internal static class McpViewportTools
    {
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        private const uint PW_RENDERFULLCONTENT = 2;

        //A campaign level takes a minute or two to populate; a composite switch seconds
        private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(5);

        //Indexed by the enums' values (LevelViewerViewportDefinitions)
        private static readonly string[] HighlightModes = { "green", "wireframe", "wireframe_transparent", "none" };
        private static readonly string[] SelectionModes = { "regular", "deep", "advanced_deep" };
        private static readonly string[] GizmoModes = { "none", "translate_world", "rotate_local", "rotate_world", "translate_local" };
        //The Live Link Camera menu, in LiveLinkCameraSync.CameraMode order
        private static readonly string[] LiveLinkCameraModes = { "disabled", "viewport_to_game", "game_to_viewport" };

        private static readonly string[] Actions = { "focus", "snap_to_floor", "hide", "unhide_all", "deselect_all", "enable", "disable", "restart" };

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "capture_viewport",
                Title = "Look at the viewport",
                Description = "A picture of OpenCAGE's 3D view (the viewport's own area, as the user sees it), taken once the viewport has finished loading. 'composite' opens that composite first; 'focus' or 'path' select entities and move the camera to them; 'camera' puts the camera somewhere first (set_viewport_camera's arguments). x/y as fractions of this picture are what place_in_viewport and pick_in_viewport take. Refused while the level is saving. Changes no level data.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Open this composite first (path or id; 'root' for the whole level)."),
                    McpSchema.Strings("focus", "Entities in that composite to frame (ids or names). Selected to move the camera, then deselected so the highlight does not tint the picture."),
                    PathProp("Instead of focus: steps from 'composite' (default the root) down through instances to one nested entity to frame, e.g. ['CorridorLight_A', 'Light']."),
                    McpSchema.Map("camera", "Instead of focus/path: put the camera here first - an object of set_viewport_camera's arguments, e.g. {\"position\": [0, 2, -5], \"look_at\": [0, 1, 0], \"fov\": 60} or {\"look_through\": \"Cam\", \"composite\": \"...\"}. Positions are in the space of the composite on screen (after 'composite' opens it)."),
                    McpSchema.Boolean("keep_selection", "Leave the framed entities selected (and highlighted) in the picture."),
                    McpSchema.Boolean("wait", "Wait (up to 5 min) for the viewport to finish loading and take pending model/material changes first (default true)."),
                    McpSchema.Integer("max_width", "Scale the picture down to at most this many pixels wide (default 1024).")),
                ReadOnly = true,
                Run = Capture,
            };

            yield return new McpTool()
            {
                Name = "get_viewport_state",
                Title = "Get viewport state",
                Description = "The 3D viewport: whether it is on, running, connected and finished loading (ready), what it is showing, where its camera is now (position, rotation as an entity takes it, forward/up, vertical field of view; in world space when the level's root is on screen, and in the composite stepped into as well) with what is in the middle of the view ('looking_at': the surface, its entity and the instances it is placed through - the room), its view settings (overlays, render filters, highlight and gizmo modes, snaps, Live Link camera - what set_viewport_view changes), the level's states for the navmesh/cover overlay, the Live Link camera's state (with the game's camera, while the viewport follows it), and optionally the tail of its log.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("log_lines", "Also return this many of the viewport process's last output lines (at most 120), to diagnose a black or failed viewport."),
                    McpSchema.Boolean("list_filters", "Also list every render filter name set_viewport_view accepts.")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetState,
            };

            yield return new McpTool()
            {
                Name = "set_viewport_view",
                Title = "Set viewport view",
                Description = "Change what the 3D viewport shows, as its toolbar, Render Filters panel and Options > Viewport do: navmesh/cover overlays per state (root composite only; drawn from the last Save & Build), zone tint, render filters, display toggles, highlight/selection/gizmo/create modes, snaps. Editor settings, remembered across sessions: not level data, not undoable. Returns the view.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("navmesh_state", "Draw this state's generated navmesh (0 = default state; get_viewport_state lists states); -1 turns it off."),
                    McpSchema.Integer("cover_state", "Draw this state's generated cover; -1 turns it off."),
                    McpSchema.Boolean("show_zones", "Tint the level's geometry by zone (Highlight Zones)."),
                    McpSchema.String("live_link_camera", "The viewport's Live Link Camera menu: 'viewport_to_game' has the running game's camera follow the viewport's (the game streams in the zones around it; set_viewport_camera moves the viewport's), 'game_to_viewport' has the viewport's camera follow the game's (position, direction, field of view; the viewport cannot be moved meanwhile), 'disabled' neither. Needs Live Link connected to the game (runtime_utils), the game running this level and the viewport showing its root composite.", options: LiveLinkCameraModes),
                    McpSchema.Deprecated(McpSchema.Boolean("sync_game_camera", "Older name for live_link_camera: true is 'viewport_to_game', false is 'disabled'.")),
                    McpSchema.Map("render_filters", "Entity previews to show/hide: {FunctionType name: true|false}, e.g. {\"PlayerTriggerBox\": true}; key 'all' sets every one first."),
                    McpSchema.Map("scene_filters", "Scene geometry to show: {\"collision_meshes\": true|false, \"occlusion_meshes\": true|false}."),
                    McpSchema.Boolean("highlight_aliases", "Mark entities overridden by aliases."),
                    McpSchema.Boolean("highlight_proxies", "Mark entities reached by proxies."),
                    McpSchema.Boolean("focus_on_selected", "Move the camera to whatever gets selected (off also turns fix_camera_to_selected off)."),
                    McpSchema.Boolean("fix_camera_to_selected", "Keep the camera fixed on the selected entity (on also turns focus_on_selected on)."),
                    McpSchema.Boolean("show_camera_position", "Show the camera's position in the viewport."),
                    McpSchema.Boolean("wireframe", "Draw models as wireframe."),
                    McpSchema.Boolean("galaxy", "Draw the galaxy (sky) behind the level."),
                    McpSchema.Boolean("hide_nested_script_entities", "Hide script entity previews inside nested composites."),
                    McpSchema.String("highlight_mode", "How the selection is marked.", options: HighlightModes),
                    McpSchema.String("selection_mode", "How clicks in the viewport pick (deep modes pick inside instances).", options: SelectionModes),
                    McpSchema.String("gizmo_mode", "The transform gizmo on the selection.", options: GizmoModes),
                    McpSchema.String("create_mode", "Put the viewport in creation mode, where the user's clicks create this type (e.g. 'PlayerTriggerBox'; turns the gizmo off), or 'none'."),
                    McpSchema.Number("transform_snap", "Gizmo move snap in metres: 0 (off) or one of the transform snap increments."),
                    McpSchema.Number("rotation_snap", "Gizmo rotate snap in degrees: 0 (off) or one of the rotation snap increments."),
                    McpSchema.Boolean("vertex_snap", "Snap gizmo moves to mesh vertices."),
                    McpSchema.Array("transform_snap_increments", "Replace the transform snap values offered (positive metres; Off is always there).", new JObject() { ["type"] = "number" }),
                    McpSchema.Array("rotation_snap_increments", "Replace the rotation snap values offered (positive degrees; Off is always there).", new JObject() { ["type"] = "number" }),
                    McpSchema.Boolean("reset_snap_increments", "Put both snap value lists back to the defaults (before any lists given here).")),
                Idempotent = true,
                Run = SetView,
            };

            yield return new McpTool()
            {
                Name = "set_viewport_camera",
                Title = "Move the viewport camera",
                Description = "Put the 3D viewport's camera at 'position', looking along 'forward', at 'look_at' or by 'rotation', or look through an entity ('look_through': a CameraResource, or anything with a position - from where the viewport draws it now, an Animation Mode or preview_cage_animation pose included, along its +Z, at a CameraResource's own fov). Positions are in the level's world space (metres, Y up; runtime_utils game_status gives the game camera's, to copy) while the viewport shows the level's root composite, else in the space of the composite it built (the one opened, not one stepped into). 'fov' holds a field of view until the camera is next moved by hand. Returns where the camera ended up (as get_viewport_state's 'camera'). With live_link_camera 'viewport_to_game' (set_viewport_view) the game's camera follows; refused with 'game_to_viewport', where the viewport follows the game's camera. Changes no level data.",
                InputSchema = McpSchema.Object(
                    McpSchema.Vector("position", "[x, y, z] metres: where the camera goes (required unless look_through or path)."),
                    McpSchema.Vector("forward", "[x, y, z]: the direction to look along (any length but zero)."),
                    McpSchema.Vector("look_at", "Instead of forward: [x, y, z], a point to look at."),
                    McpSchema.Vector("rotation", "Instead of forward/look_at/up: [pitch, yaw, roll] degrees as an entity's rotation takes it (yaw, then pitch, then roll; [0, 0, 0] looks along +Z; positive pitch looks down) - what get_viewport_state's camera 'rotation' gives back."),
                    McpSchema.Vector("up", "With forward/look_at: [x, y, z], which way is up for the camera (default [0, 1, 0]); straightened to be square to the view."),
                    McpSchema.Number("fov", "Vertical field of view in degrees (1-170), held until the camera is next moved by hand; 0 puts the viewport's own back. With look_through it defaults to a CameraResource's own 'fov'."),
                    McpSchema.String("look_through", "Instead of position: an entity (id or name) in 'composite' to look through."),
                    PathProp("Instead of look_through: steps from 'composite' (default the composite on screen; a result's path object starts where its 'from' says, or at the root) down through instances to a nested entity to look through, e.g. ['Cinematic_A', 'Cam']."),
                    McpSchema.String("composite", "The composite look_through/path start in (path or id; default the composite on screen). If the viewport's scene places it more than once, the placement stepped into in the editor is used, else the first (the result says which); one the scene does not place is opened."),
                    McpSchema.Boolean("follow", "With look_through/path: keep looking through it as it moves (Animation Mode playing) until the camera is moved by hand or placed again. Default false.")),
                Idempotent = true,
                Run = SetCamera,
            };

            yield return new McpTool()
            {
                Name = "pick_in_viewport",
                Title = "What is at a viewport point",
                Description = "What the viewport shows at points of its picture, or meets along rays: for each, the nearest surface a click there would land on (models and entity icons/shapes as drawn now, unsaved edits included; not invisible collision) - the point, its normal and distance, the entity drawing it with the instances it is placed through from the composite on screen (outermost first: the room), and for a model its name, submesh and material with the material's textures. Points are fractions (0-1) of the capture_viewport picture, 0,0 top left (0.5, 0.5 is the middle); positions are in the same space as set_viewport_camera's. Also returns the camera they were seen from. With the viewport off, 'rays' alone are cast against the level's collision instead (as raycast does: world space; models without collision are not hit). Changes nothing.",
                InputSchema = McpSchema.Object(
                    McpSchema.Array("points", "[[x, y], ...]: points of the picture, each a fraction 0-1 across and down (pixel / picture width, pixel / picture height). Up to 64 points and rays together.", new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["minItems"] = 2, ["maxItems"] = 2 }),
                    McpSchema.Array("rays", "[{origin: [x, y, z], direction: [x, y, z]}, ...]: rays to cast through what the viewport draws, in the space positions use (e.g. straight down from a point: direction [0, -1, 0]).", new JObject()
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                        {
                            ["origin"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["minItems"] = 3, ["maxItems"] = 3 },
                            ["direction"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["minItems"] = 3, ["maxItems"] = 3 },
                        },
                        ["required"] = new JArray("origin", "direction"),
                    }),
                    McpSchema.Boolean("materials", "Describe the materials of models hit (shader and textures), once each (default true).")),
                ReadOnly = true,
                Idempotent = true,
                Run = Pick,
            };

            yield return new McpTool()
            {
                Name = "viewport_action",
                Title = "Viewport action",
                Description = "focus / snap_to_floor / hide act on entities ('composite' + 'entities', or 'path' for one nested entity), selected first. snap_to_floor drops them onto the RENDER geometry below as the viewport draws it - only the composite on screen, which is the entities' own composite unless 'path' steps in from the root (do that to land on the level's floor) - with the viewport on (one undo step); drop_to_floor does the same against the level's collision with the viewport off and unsaved changes. hide and unhide_all only change what the viewport draws. deselect_all clears the selection. enable / disable turn the viewport on or off (a remembered setting); restart relaunches it.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "What to do.", required: true, options: Actions),
                    McpSchema.String("composite", "focus / snap_to_floor / hide: the composite the entities are in (opened in the editor; 'root' for the level), or where 'path' starts (default the root)."),
                    McpSchema.Strings("entities", "Entities in 'composite' to act on (ids or names; up to 64)."),
                    PathProp("Instead of entities: steps from 'composite' (default the root) down through instances to one nested entity, e.g. ['CorridorLight_A', 'Light']."),
                    McpSchema.Boolean("save_first", "enable / restart: save the level first if it has unsaved changes (the viewport reads it from disk). Default false."),
                    McpSchema.Boolean("wait", "enable / restart: wait (up to 5 min) until the viewport has loaded the level (default true).")),
                Run = RunAction,
            };

            yield return new McpTool()
            {
                Name = "place_in_viewport",
                Title = "Place in viewport",
                Description = "Create a function entity or a composite instance where a point of the viewport lands on the level's geometry, as dropping it on the viewport does. x/y are fractions (0-1) of the capture_viewport picture, 0,0 top left. It goes in the composite on screen (or 'composite', opened first). One undo step. Fails if the point hits nothing.",
                InputSchema = McpSchema.Object(
                    McpSchema.Number("x", "Across the viewport, 0 (left) to 1 (right).", required: true),
                    McpSchema.Number("y", "Down the viewport, 0 (top) to 1 (bottom).", required: true),
                    McpSchema.String("function", "A function type to create (one with a position, e.g. 'Character', 'LightReference', 'PlayerTriggerBox')."),
                    McpSchema.String("instance_of", "Instead of function: the composite to place an instance of (path or id)."),
                    McpSchema.String("composite", "Where to create it (path or id; 'root' for the level). Default: the composite on screen.")),
                Run = Place,
            };

            yield return new McpTool()
            {
                Name = "get_composite_preview",
                Title = "Composite preview",
                Description = "A picture of what a composite looks like on its own: the preview the viewport took of it at the last save (or the one shipped with the game). fresh=true has the viewport take one now instead, rebuilding its scene for a moment (slow on big levels). Changes no level data.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.Boolean("fresh", "Take a new picture of the composite as it is now (needs the viewport; not stored). Default false.")),
                ReadOnly = true,
                Run = Preview,
            };
        }

        #region Viewer readiness
        /// <summary>The viewer has what it was last sent on screen: connected, the level populated, no populate in flight. UI thread.</summary>
        private static bool ViewerSettled() => Send.Connected && ViewerResourceSync.ViewerReady && ViewerPopulateSync.ActivePopulateToken == 0;

        /// <summary>Refuse unless the viewport is switched on. UI thread.</summary>
        private static LevelViewerPanel RequireViewportOn()
        {
            if (!Singleton.ViewportEnabled)
                throw new McpError("The viewport is turned off. viewport_action {action: 'enable'} turns it on (Options > Viewport > Enable Viewport).");
            return McpEditor.Editor.LevelViewerPanel;
        }

        /// <summary>A save rewrites the composite on screen's links: nothing may navigate the editor meanwhile. UI thread.</summary>
        private static void RequireNotSaving()
        {
            if (UndoStack.Current.Blocked)
                throw new McpError("OpenCAGE is saving the level. Try again when it has finished.");
        }

        /// <summary>
        /// Wait until the viewer is connected, has finished populating what it was last told to show, and holds
        /// the level's models, materials and textures as they are now. <paramref name="populateEventsBefore"/>,
        /// when not -1, is <see cref="ViewerPopulateSync.PopulateEvents"/> from before a composite switch: the
        /// viewer is given a moment to start on it, so its old scene is not taken for the new one. With
        /// <paramref name="relaunched"/> (a viewer just started) a populate since then is required outright:
        /// until the new process connects, the state left by the old one still reads as ready.
        /// </summary>
        private static void AwaitViewer(McpCall call, int populateEventsBefore = -1, bool relaunched = false)
        {
            if (!McpEditor.UI(() => { RequireViewportOn(); return ViewerAlive(); })
                && !McpEditor.WaitFor(call, ViewerAlive, TimeSpan.FromSeconds(5), "Waiting for the viewport to start"))
                throw new McpError("The viewport is not running. It opens with the next level load; viewport_action {action: 'restart'} starts it now.");

            if (!McpEditor.WaitFor(call, () => Send.Connected || !ViewerAlive(), TimeSpan.FromSeconds(90), "Waiting for the viewport to connect") || !McpEditor.UI(() => Send.Connected))
                throw new McpError("The viewport has not connected: it may have failed to start (get_viewport_state with log_lines shows its output; viewport_action {action: 'restart'} starts it again).");

            Func<bool> settled = ViewerSettled;
            if (relaunched)
                settled = () => ViewerPopulateSync.PopulateEvents != populateEventsBefore && ViewerSettled();
            else if (populateEventsBefore >= 0)
                McpEditor.WaitFor(call, () => ViewerPopulateSync.PopulateEvents != populateEventsBefore || !Send.Connected, TimeSpan.FromSeconds(3), "Waiting for the viewport to take the composite");

            //A viewer that is starting (relaunched) may not be connected yet; one that has gone is not coming back by itself
            Func<bool> alive = relaunched ? (Func<bool>)ViewerAlive : () => Send.Connected;
            if (!McpEditor.WaitFor(call, () => settled() || !alive(), ReadyTimeout, "Waiting for the viewport to finish loading"))
                throw new McpError("The viewport is still loading after " + (int)ReadyTimeout.TotalMinutes + " minutes. Try again later (get_viewport_state shows when it is ready).");
            if (!McpEditor.UI(() => settled()))
                throw new McpError("The viewport disconnected while loading (it may have crashed): get_viewport_state with log_lines shows its output.");

            //Resource edits go to the viewer on a coalescing timer: let any pending batch land first
            StrongBox<bool> synced = new StrongBox<bool>(false);
            McpEditor.UI(() => ViewerResourceSync.AfterNextSync(() => synced.Value = true));
            if (!McpEditor.WaitFor(call, () => synced.Value, TimeSpan.FromSeconds(30), "Sending changed models and materials to the viewport"))
                call.Note("Some model, material or texture changes may not have reached the viewport yet.");
        }

        /// <summary>The viewer is connected, or its process is running (starting up). UI thread.</summary>
        private static bool ViewerAlive()
        {
            LevelViewerPanel panel = Singleton.Editor?.LevelViewerPanel;
            return Send.Connected || (panel != null && !panel.IsDisposed && panel.IsRunning);
        }
        #endregion

        #region Targets
        /// <summary>What a tool acts on in the viewport: entities of one composite, or one entity reached down a path of instances.</summary>
        private sealed class Target
        {
            public Composite Entry;
            public Composite Owner;
            public List<Entity> Entities = new List<Entity>();
            /// <summary>The instance path from <see cref="Entry"/> to a nested entity (ids, the entity last), or null.</summary>
            public uint[] Path;
        }

        /// <summary>The 'path' arguments here: one shape everywhere (<see cref="McpScript.PathSteps"/>).</summary>
        private static McpSchema.Prop PathProp(string description) =>
            McpSchema.Any("path", description + " An array of ids/names, a string split on '/', or a path a result gives ({path, ids}, with 'from' when it does not start at the root) passed back as it is.");

        /// <summary>
        /// Where a 'path' starts: 'composite' when given, else the 'from' a result's path object carries, else the root (as
        /// results give paths from the root). Null when there is no path and no composite.
        /// </summary>
        private static string PathStart(McpCall call)
        {
            if (call.Has("composite")) return call.Str("composite");
            JToken path = call.Token("path");
            if (path == null) return null;
            return path is JObject given && given["from"]?.Type == JTokenType.String ? (string)given["from"] : "root";
        }

        /// <summary>The composite and the entities (from <paramref name="entitiesArgument"/>, or 'path') a call names. UI thread.</summary>
        private static Target ResolveTarget(McpCall call, Commands commands, string entitiesArgument)
        {
            bool hasEntities = call.Has(entitiesArgument), hasPath = call.Has("path");
            if (!call.Has("composite"))
            {
                if (hasEntities)
                    throw new McpError("Say which composite the '" + entitiesArgument + "' are in ('composite'; 'root' for the level).");
                if (!hasPath)
                    return null;
            }
            if (hasEntities && hasPath)
                throw new McpError("Give '" + entitiesArgument + "' or 'path', not both.");

            Composite composite = McpScript.FindComposite(commands, PathStart(call));
            Target target = new Target() { Entry = composite, Owner = composite };
            if (hasPath)
            {
                List<string> steps = McpScript.PathSteps(call.Token("path"));
                ShortGuid[] ids = McpScript.ResolvePath(commands, composite, steps, out Composite owner, out Entity entity);
                target.Entities.Add(entity);
                //More than one step (a leading 'root' is not one): stepped into from the entry
                if (ids.Count(o => o != ShortGuid.Invalid) > 1)
                {
                    target.Owner = owner;
                    target.Path = ids.Where(o => o != ShortGuid.Invalid).Select(o => o.AsUInt32).ToArray();
                }
            }
            else
            {
                foreach (string reference in call.StrList(entitiesArgument))
                {
                    Entity entity = McpScript.FindEntity(commands, composite, reference);
                    if (!target.Entities.Contains(entity))
                        target.Entities.Add(entity);
                }
                if (target.Entities.Count > 64)
                    throw new McpError("At most 64 entities at once (the editor selects no more).");
            }
            return target;
        }

        /// <summary>Show a composite on its own (not stepped into from another), as opening it in the browser does. UI thread. True if it switched.</summary>
        private static bool OpenTopLevel(Composite composite)
        {
            CommandsEditor editor = McpEditor.Editor;
            CompositeDisplay display = editor.CompositeDisplay;
            if (display == null || display.IsDisposed)
                throw new McpError("The editor's composite view is not open.");
            bool switched = false;
            if (!display.Populated || display.Composite != composite)
            {
                display = editor.CompositeBrowser?.LoadComposite(composite) ?? display;
                switched = true;
            }
            else if (display.Path.AllEntities.Count != 0)
            {
                //Stepped into from a parent: shown on its own again, which is what the viewer populates
                display.PopulateUI(composite);
                switched = true;
            }
            if (display == null || display.IsDisposed || display.Composite != composite)
                throw new McpError("OpenCAGE could not open " + composite.name + ".");
            return switched;
        }

        /// <summary>Open the target's composite and select what it names (or clear the selection). UI thread. True if the composite on screen changed.</summary>
        private static bool OpenAndSelect(Target target, Commands commands)
        {
            Func<Entity, Composite> child = o => McpScript.InstancedComposite(commands, o);
            CompositeDisplay display = McpEditor.Editor.CompositeDisplay;
            //Already stepped into something from this composite: the viewer has its scene, so the steps are walked
            //without opening it afresh (which would have the viewer build the whole scene again)
            bool steppedFromEntry = display != null && !display.IsDisposed && display.Populated
                && display.Path.AllEntities.Count != 0 && display.Path.AllComposites.FirstOrDefault() == target.Entry;
            bool switched = steppedFromEntry ? false : OpenTopLevel(target.Entry);
            display = McpEditor.Editor.CompositeDisplay;
            if (target.Path != null)
            {
                if (!display.ApplyViewerSelectionPath(target.Entry, target.Path, true, child))
                    throw new McpError("OpenCAGE could not step down to that entity.");
                return switched;
            }
            if (steppedFromEntry)
                display.ApplyViewerSelectionPath(target.Entry, new uint[0], false, child);
            if (target.Entities.Count == 1)
                display.LoadEntity(target.Entities[0], false);
            else if (target.Entities.Count > 1)
                display.ApplyMultiSelection(target.Entities);
            else
                display.ClearEntitySelection();
            return switched;
        }

        /// <summary>The inspector (and so the viewer) has the target's selection. UI thread.</summary>
        private static bool SelectionShown(Target target)
        {
            EntityInspector inspector = Singleton.Editor?.CompositeDisplay?.EntityDisplay;
            if (inspector == null || inspector.IsDisposed)
                return false;
            if (target.Entities.Count == 0)
                return !inspector.Populated;
            if (target.Entities.Count == 1)
                return !inspector.IsMultiEditing && inspector.Entity == target.Entities[0];
            List<Entity> multi = inspector.MultiSelectedEntities;
            return multi != null && multi.Count == target.Entities.Count && target.Entities.All(multi.Contains);
        }

        private static void AwaitSelection(McpCall call, Target target)
        {
            if (!McpEditor.WaitFor(call, () => SelectionShown(target), TimeSpan.FromSeconds(15), "Selecting"))
                throw new McpError("OpenCAGE did not select the entities (they may have been deleted, or the editor is busy).");
        }

        private static string Describe(Commands commands, Target target)
        {
            if (target.Entities.Count == 1)
                return McpScript.EntityName(commands, target.Owner, target.Entities[0]);
            return target.Entities.Count + " entities";
        }
        #endregion

        #region capture_viewport
        private static object Capture(McpCall call)
        {
            JObject cameraArgs = call.Object("camera");
            if (cameraArgs != null && (call.Has("focus") || call.Has("path")))
                throw new McpError("'camera' puts the camera somewhere itself: give it, or focus/path (which frame entities), not both.");
            McpCall cameraCall = cameraArgs != null ? NestedCall(call, "set_viewport_camera", cameraArgs, "camera") : null;

            Target target = null;
            int populateEvents = -1;
            McpEditor.UI(() =>
            {
                McpEditor.RequireLevel(forEditing: false);
                RequireNotSaving();
                RequireViewportOn();
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                target = ResolveTarget(call, commands, "focus");
                if (target != null)
                {
                    int before = ViewerPopulateSync.PopulateEvents;
                    if (OpenAndSelect(target, commands))
                        populateEvents = before;
                }
                Singleton.Editor.CompositeDisplay?.ShowLevelViewerPanel(true);
            });

            bool focused = target != null && target.Entities.Count != 0;
            if (target != null)
                AwaitSelection(call, target);

            if (call.Bool("wait", true))
                AwaitViewer(call, populateEvents);
            else if (!McpEditor.WaitFor(call, () => Send.Connected, TimeSpan.FromSeconds(60), "Waiting for the viewport"))
                throw new McpError("The viewport is not connected yet (it may still be starting, or loading the level).");

            Thread.Sleep(400);
            string placed = null;
            if (cameraCall != null)
            {
                JObject where = (JObject)SetCamera(cameraCall);
                foreach (string note in cameraCall.Notes) call.Note(note);
                placed = DescribePlacement(where);
                //Drawn from the new place before the picture is taken
                Thread.Sleep(300);
            }
            if (focused && LiveLinkCameraSync.CameraFollowsGame)
                call.Note(FollowingGameNote);
            if (focused)
            {
                McpEditor.UI(() => Send.SendViewportAction(ViewportAction.FocusOnSelection));
                Thread.Sleep(1500);
                //The selection highlight tints what it frames: drop it so the picture shows the real colours
                if (!call.Bool("keep_selection"))
                {
                    McpEditor.UI(() => Singleton.Editor.CompositeDisplay?.ClearEntitySelection());
                    Thread.Sleep(600);
                }
            }

            /* PrintWindow has the embedded viewer draw into the picture, which waits on the viewer's own thread: done while
               the viewer was busy (an edit just before had it spawning), it held the UI thread - OpenCAGE froze for as
               long. So wait here, off the UI thread, until the viewer answers. */
            if (!McpEditor.WaitFor(call, () => Singleton.Editor.LevelViewerPanel?.IsViewerResponding(100) ?? true, TimeSpan.FromSeconds(60), "Waiting for the viewport to be free to draw"))
                throw new McpError("The viewport is busy (it has not answered for a minute): try again when it has finished loading.");

            int maxWidth = Math.Max(64, call.Int("max_width", 1024));
            Size size = Size.Empty;
            byte[] image = McpEditor.UI(() =>
            {
                LevelViewerPanel panel = Singleton.Editor.LevelViewerPanel;
                if (panel == null || panel.IsDisposed || !panel.Visible)
                    throw new McpError("The viewport panel is not showing.");

                //Only the viewer's own window (the panel's toolbar is docked over its top edge): so a point's fraction of the picture is its fraction of the viewport
                if (!panel.TryGetViewportScreenPoint(0f, 0f, out Point topLeft) || !panel.TryGetViewportScreenPoint(1f, 1f, out Point bottomRight))
                    throw new McpError("The viewport is not running in its panel (it may have closed): viewport_action {action: 'restart'} starts it again.");
                Rectangle area = new Rectangle(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
                if (area.Width < 16 || area.Height < 16)
                    throw new McpError("The viewport panel is too small to picture.");

                //The whole window composed (the embedded viewer included, covered or not), then the viewer cut out of it.
                //The panel's own top-level window: docked in the editor, or floating in a window of its own.
                Control top = panel.TopLevelControl ?? Singleton.Editor;
                IntPtr window = top.Handle;
                if (IsIconic(window))
                    throw new McpError("The OpenCAGE window with the viewport is minimised: restore it and try again.");
                if (!GetWindowRect(window, out RECT bounds) || bounds.Right - bounds.Left < 16 || bounds.Bottom - bounds.Top < 16)
                    throw new McpError("Could not find where the viewport's window is.");
                using (Bitmap whole = new Bitmap(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, PixelFormat.Format32bppArgb))
                {
                    bool printed;
                    using (Graphics g = Graphics.FromImage(whole))
                    {
                        IntPtr hdc = g.GetHdc();
                        try { printed = PrintWindow(window, hdc, PW_RENDERFULLCONTENT); }
                        finally { g.ReleaseHdc(hdc); }
                    }
                    if (!printed)
                        throw new McpError("Windows would not draw the viewport's window into a picture.");
                    Rectangle crop = new Rectangle(area.X - bounds.Left, area.Y - bounds.Top, area.Width, area.Height);
                    crop.Intersect(new Rectangle(0, 0, whole.Width, whole.Height));
                    if (crop.Width < 16 || crop.Height < 16)
                        throw new McpError("The viewport panel is outside its window's visible area.");
                    float scale = Math.Min(1f, (float)maxWidth / crop.Width);
                    using (Bitmap picture = new Bitmap(Math.Max(1, (int)(crop.Width * scale)), Math.Max(1, (int)(crop.Height * scale)), PixelFormat.Format24bppRgb))
                    {
                        size = picture.Size;
                        using (Graphics g = Graphics.FromImage(picture))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.DrawImage(whole, new Rectangle(0, 0, picture.Width, picture.Height), crop, GraphicsUnit.Pixel);
                        }
                        using (MemoryStream stream = new MemoryStream())
                        {
                            ImageCodecInfo jpeg = ImageCodecInfo.GetImageEncoders().First(o => o.FormatID == ImageFormat.Jpeg.Guid);
                            EncoderParameters quality = new EncoderParameters(1);
                            quality.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
                            picture.Save(stream, jpeg, quality);
                            return stream.ToArray();
                        }
                    }
                }
            });

            string shown = McpEditor.UI(() => DescribeShown());
            return new McpImage()
            {
                Data = image,
                MimeType = "image/jpeg",
                Caption = "The viewport" + (shown != null ? ", showing " + shown : "") + (focused && !LiveLinkCameraSync.CameraFollowsGame ? ", framed on " + McpEditor.UI(() => Describe(McpEditor.RequireCommands(forEditing: false), target)) : "") + (placed != null ? ", " + placed : "") + ". " +
                    size.Width + "x" + size.Height + " pixels; place_in_viewport and pick_in_viewport take a point as x = px/" + size.Width + ", y = py/" + size.Height + ".",
            };
        }

        /// <summary>What the viewport is showing, with its overlays, in words. UI thread.</summary>
        private static string DescribeShown()
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display == null || !display.Populated || display.Composite == null)
                return null;
            List<CompositePath.CompAndEnt> path = display.Path.GetPathRich(display.Composite);
            List<string> overlays = new List<string>();
            if (ViewerStateInfoMode.NavMeshState != ViewerStateInfoMode.None) overlays.Add("navmesh of state " + ViewerStateInfoMode.NavMeshState);
            if (ViewerStateInfoMode.CoverState != ViewerStateInfoMode.None) overlays.Add("cover of state " + ViewerStateInfoMode.CoverState);
            if (SettingsManager.GetBool(Settings.ShowZones)) overlays.Add("zone tint");
            foreach (SceneFilterDefinition definition in RenderFilterDefinitions.SceneFilters)
                if (RenderFilters.IsSceneFilterEnabled(definition.Kind)) overlays.Add(definition.Label.ToLowerInvariant());
            string text = path.Count != 0 && path[0].Entity != null
                ? path[0].Composite.name + " (stepped into " + display.Composite.name + ")"
                : display.Composite.name;
            return text + (overlays.Count != 0 ? ", with " + string.Join(", ", overlays) : "");
        }
        #endregion

        #region get_viewport_state
        private static object GetState(McpCall call)
        {
            int logLines = Math.Max(0, Math.Min(120, call.Int("log_lines", 0)));
            bool listFilters = call.Bool("list_filters");
            JObject result = McpEditor.UI(() =>
            {
                CommandsEditor editor = McpEditor.Editor;
                LevelViewerPanel panel = editor.LevelViewerPanel;
                bool hasPanel = panel != null && !panel.IsDisposed;
                LevelContent content = editor.CompositeBrowser?.Content;
                bool levelOpen = content?.Level != null && content.IsLevelDataLoaded;

                JObject state = new JObject()
                {
                    ["enabled"] = Singleton.ViewportEnabled,
                    ["running"] = hasPanel && panel.IsRunning,
                    ["embedded"] = hasPanel && panel.IsEmbedded,
                    ["panel_visible"] = hasPanel && panel.Visible,
                    ["connected"] = Send.Connected,
                    ["level_populated"] = ViewerResourceSync.ViewerReady,
                    ["populating"] = ViewerPopulateSync.ActivePopulateToken != 0,
                    ["ready"] = ViewerSettled(),
                };
                if (!Singleton.ViewportEnabled)
                    state["note"] = "The viewport is off: viewport_action {action: 'enable'} turns it on.";

                CompositeDisplay display = editor.CompositeDisplay;
                if (levelOpen && display != null && !display.IsDisposed && display.Populated && display.Composite != null)
                {
                    Commands commands = content.Level.Commands;
                    JObject shown = McpScript.CompositeSummary(commands, display.Composite);
                    List<CompositePath.CompAndEnt> path = display.Path.GetPathRich(display.Composite);
                    if (path.Count != 0 && path[0].Entity != null)
                    {
                        //Stepped into through instances: the viewer shows the composite at the top of the path
                        shown["stepped_into_from"] = path[0].Composite.name;
                        shown["path"] = new JArray(path.Where(o => o.Entity != null).Select(o => McpScript.EntityName(commands, o.Composite, o.Entity)));
                    }
                    state["composite_on_screen"] = shown;
                    state["state_overlays_available"] = IsAtRoot(content, display);
                }
                if (levelOpen)
                    state["states"] = States(content);

                state["view"] = ViewState(listFilters);

                LiveLinkCameraSync.CameraMode mode = LiveLinkCameraSync.WantedMode;
                if (mode != LiveLinkCameraSync.CameraMode.Disabled)
                {
                    JObject sync = new JObject()
                    {
                        ["mode"] = LiveLinkCameraModes[(int)mode],
                        ["active"] = mode == LiveLinkCameraSync.CameraMode.ViewportToGame ? LiveLinkCameraSync.Enabled : LiveLinkCameraSync.CameraFollowsGame,
                        ["live_link_connected"] = global::OpenCAGE.RuntimeUtilsConnection.LiveLink.Connected,
                    };
                    if (LiveLinkCameraSync.LastStatus != null)
                        sync["status"] = LiveLinkCameraSync.LastStatus;
                    //The game's camera as it was last asked for, which the viewport's is put at
                    LiveLinkCameraSync.Pose game = LiveLinkCameraSync.LastGamePose;
                    if (mode == LiveLinkCameraSync.CameraMode.GameToViewport && game != null)
                        sync["game_camera"] = PoseState(game);
                    state["live_link_camera"] = sync;
                }

                if (logLines > 0)
                {
                    string[] tail = hasPanel ? panel.GetOutputTail() : new string[0];
                    state["log"] = new JArray(tail.Skip(Math.Max(0, tail.Length - logLines)));
                }
                return state;
            });

            /* Where the camera is now, asked of the viewer there and then (it is only streamed while the game's camera follows
               it), with what is in the middle of the view. Only once it has finished loading: a viewer mid-populate answers
               after the populate, and this is meant to be quick. */
            if ((bool)result["ready"])
            {
                try
                {
                    Packet answer = AskViewer(call, new List<float[]>() { new[] { 0.5f, 0.5f } }, null, TimeSpan.FromSeconds(5));
                    ViewportCamera camera = McpEditor.UI(() => ReadCamera(answer));
                    JObject cameraState = camera.ToJson();
                    McpEditor.UI(() => AddSteppedIntoSpace(cameraState, camera));
                    if (answer.viewport_pick_results != null && answer.viewport_pick_results.Count != 0)
                    {
                        JObject centre = McpEditor.UI(() => DescribePick(answer.viewport_pick_results[0], camera, null, null));
                        centre.Remove("point");
                        cameraState["looking_at"] = centre;
                    }
                    result["camera"] = cameraState;
                }
                catch (McpError e)
                {
                    result["camera_unavailable"] = e.Message;
                }
            }
            else if ((bool)result["connected"])
                result["camera_unavailable"] = "The viewport is still loading: its camera is reported once it is ready.";
            return result;
        }

        /// <summary>A camera pose the viewer reported (game world space; fov vertical, in degrees).</summary>
        private static JObject PoseState(LiveLinkCameraSync.Pose pose) => new JObject()
        {
            ["position"] = McpValues.Vector(pose.Position),
            ["forward"] = McpValues.Vector(pose.Forward),
            ["up"] = McpValues.Vector(pose.Up),
            ["fov"] = Math.Round(pose.Fov, 3),
            ["in_level_space"] = pose.InLevelSpace,
            ["received_ms_ago"] = (long)(DateTime.UtcNow - pose.Received).TotalMilliseconds,
        };

        /// <summary>The level's states, whose generated navmesh and cover the overlays draw. UI thread.</summary>
        private static JArray States(LevelContent content)
        {
            JArray result = new JArray();
            List<CathodeLib.Level.State> states = content?.Level?.StateResources;
            if (states == null)
                return result;
            for (int i = 0; i < states.Count; i++)
                result.Add(new JObject() { ["index"] = i, ["name"] = LevelViewerPanel.DescribeState(content, states[i], i) });
            return result;
        }

        /// <summary>Whether the composite on screen is the level's root (or stepped into from it): where Show State Info works. UI thread.</summary>
        private static bool IsAtRoot(LevelContent content, CompositeDisplay display)
        {
            if (content?.EditorUtils == null || display?.Composite == null)
                return false;
            Composite top = display.Path.AllComposites.FirstOrDefault() ?? display.Composite;
            return content.EditorUtils.GetCompositeType(top) == EditorUtils.CompositeType.IS_ROOT;
        }

        /// <summary>Every view setting set_viewport_view changes, as it names them. UI thread.</summary>
        private static JObject ViewState(bool listFilters)
        {
            Dictionary<uint, bool> filters = RenderFilters.LoadAll();
            JObject scene = new JObject();
            foreach (SceneFilterDefinition definition in RenderFilterDefinitions.SceneFilters)
                scene[Snake(definition.Kind.ToString())] = RenderFilters.IsSceneFilterEnabled(definition.Kind);

            JObject view = new JObject()
            {
                ["navmesh_state"] = ViewerStateInfoMode.NavMeshState,
                ["cover_state"] = ViewerStateInfoMode.CoverState,
                ["show_zones"] = SettingsManager.GetBool(Settings.ShowZones),
                ["live_link_camera"] = LiveLinkCameraModes[(int)LiveLinkCameraSync.WantedMode],
                ["render_filters_on"] = new JArray(RenderFilterDefinitions.All.Where(o => filters.TryGetValue(o.FunctionTypeUInt, out bool on) && on).Select(o => o.FunctionType.ToString()).OrderBy(o => o, StringComparer.OrdinalIgnoreCase)),
                ["scene_filters"] = scene,
                ["highlight_aliases"] = SettingsManager.GetBool(Settings.HighlightAliases),
                ["highlight_proxies"] = SettingsManager.GetBool(Settings.HighlightProxies),
                ["focus_on_selected"] = SettingsManager.GetBool(Settings.FocusOnSelected),
                ["fix_camera_to_selected"] = SettingsManager.GetBool(Settings.FixCameraToSelected),
                ["show_camera_position"] = SettingsManager.GetBool(Settings.ShowCameraPosition),
                ["wireframe"] = SettingsManager.GetBool(Settings.RenderWireframe),
                ["galaxy"] = SettingsManager.GetBool(Settings.RenderGalaxy),
                ["hide_nested_script_entities"] = SettingsManager.GetBool(Settings.HideNestedScriptEntities),
                ["highlight_mode"] = HighlightModes[(int)LevelViewerViewportDefinitions.NormalizeHighlightMode(SettingsManager.GetInteger(Settings.LevelViewerHighlightMode))],
                ["selection_mode"] = SelectionModes[(int)LevelViewerViewportDefinitions.NormalizeDeepSelectMode(SettingsManager.GetInteger(Settings.LevelViewerDeepSelectMode))],
                ["gizmo_mode"] = GizmoModes[(int)LevelViewerViewportDefinitions.NormalizeGizmoMode(SettingsManager.GetInteger(Settings.LevelViewerGizmoMode))],
                ["create_mode"] = ViewerCreateMode.IsActive ? ((FunctionType)ViewerCreateMode.ActiveFunctionType).ToString() : null,
                ["transform_snap"] = Math.Round(SettingsManager.GetFloat(Settings.TransformGridSnap), 4),
                ["rotation_snap"] = Math.Round(SettingsManager.GetFloat(Settings.RotationSnapDegrees), 4),
                ["vertex_snap"] = SettingsManager.GetBool(Settings.TransformVertexSnap),
                ["transform_snap_increments"] = new JArray(SnapIncrementSettings.GridValues.Where(o => o > 0f).Select(o => Math.Round(o, 4))),
                ["rotation_snap_increments"] = new JArray(SnapIncrementSettings.RotationValues.Where(o => o > 0f).Select(o => Math.Round(o, 4))),
            };
            if (listFilters)
                view["render_filters_available"] = new JArray(RenderFilterDefinitions.All.Select(o => o.FunctionType.ToString()).OrderBy(o => o, StringComparer.OrdinalIgnoreCase));
            return view;
        }

        /// <summary>"CollisionMeshes" as "collision_meshes".</summary>
        private static string Snake(string name)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i != 0) text.Append('_');
                text.Append(char.ToLowerInvariant(name[i]));
            }
            return text.ToString();
        }

        private static string Squash(string name) => (name ?? "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
        #endregion

        #region set_viewport_view
        private static readonly (string Argument, string Setting)[] Toggles =
        {
            ("highlight_aliases", Settings.HighlightAliases),
            ("highlight_proxies", Settings.HighlightProxies),
            ("show_camera_position", Settings.ShowCameraPosition),
            ("wireframe", Settings.RenderWireframe),
            ("galaxy", Settings.RenderGalaxy),
            ("hide_nested_script_entities", Settings.HideNestedScriptEntities),
            ("vertex_snap", Settings.TransformVertexSnap),
        };

        private static object SetView(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                CommandsEditor editor = McpEditor.Editor;
                List<string> changed = new List<string>();

                //Everything is read and checked first, so a bad argument changes nothing
                int? navmesh = call.Has("navmesh_state") ? call.Int("navmesh_state") : (int?)null;
                int? cover = call.Has("cover_state") ? call.Int("cover_state") : (int?)null;
                if ((navmesh.HasValue && navmesh.Value != ViewerStateInfoMode.None) || (cover.HasValue && cover.Value != ViewerStateInfoMode.None))
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: false);
                    int count = content.Level.StateResources?.Count ?? 0;
                    if (count == 0)
                        throw new McpError("This level has no states to draw navigation data for.");
                    foreach (int? index in new[] { navmesh, cover })
                        if (index.HasValue && index.Value != ViewerStateInfoMode.None && (index.Value < 0 || index.Value >= count))
                            throw new McpError("There is no state " + index.Value + ": this level has states 0 to " + (count - 1) + " (-1 turns an overlay off).");
                    if (!IsAtRoot(content, editor.CompositeDisplay))
                        throw new McpError("The navmesh and cover overlays draw over the level's root composite: open it first (open_composite 'root', or capture_viewport with composite 'root').");
                }

                Dictionary<uint, bool> filterChanges = ReadRenderFilters(call.Object("render_filters"));
                Dictionary<SceneFilterKind, bool> sceneChanges = ReadSceneFilters(call.Object("scene_filters"));

                List<(string argument, string setting, bool value)> toggles = Toggles.Where(o => call.Has(o.Argument)).Select(o => (o.Argument, o.Setting, call.Bool(o.Argument))).ToList();
                bool? zones = call.Has("show_zones") ? call.Bool("show_zones") : (bool?)null;
                //sync_game_camera is the older on/off for the first two of the menu's choices
                int? cameraMode = ReadMode(call, "live_link_camera", LiveLinkCameraModes);
                if (call.Has("sync_game_camera"))
                {
                    int alias = (int)(call.Bool("sync_game_camera") ? LiveLinkCameraSync.CameraMode.ViewportToGame : LiveLinkCameraSync.CameraMode.Disabled);
                    if (cameraMode.HasValue && cameraMode.Value != alias)
                        throw new McpError("sync_game_camera is the older name for live_link_camera ('viewport_to_game' or 'disabled'), and they disagree here: give live_link_camera alone.");
                    cameraMode = alias;
                }
                bool? focus = call.Has("focus_on_selected") ? call.Bool("focus_on_selected") : (bool?)null;
                bool? fix = call.Has("fix_camera_to_selected") ? call.Bool("fix_camera_to_selected") : (bool?)null;
                if (focus == false && fix == true)
                    throw new McpError("fix_camera_to_selected needs focus_on_selected: they cannot be false and true together.");

                int? highlight = ReadMode(call, "highlight_mode", HighlightModes);
                int? selection = ReadMode(call, "selection_mode", SelectionModes);
                int? gizmo = ReadMode(call, "gizmo_mode", GizmoModes);
                uint? create = null;
                if (call.Has("create_mode"))
                {
                    string wanted = call.Str("create_mode").Trim();
                    if (Squash(wanted) == "none" || wanted.Length == 0)
                        create = 0;
                    else
                    {
                        //The Create menu's types: the ones the viewport draws a preview for
                        foreach (RenderFilterDefinitions.Definition definition in RenderFilterDefinitions.All)
                            if (string.Equals(definition.FunctionType.ToString(), wanted, StringComparison.OrdinalIgnoreCase)) create = definition.FunctionTypeUInt;
                        if (create == null)
                            throw new McpError("create_mode must be 'none' or a type the viewport's Create menu offers (get_viewport_state with list_filters lists them; the same names). place_in_viewport creates any type with a position.");
                        if (gizmo.HasValue || selection.HasValue)
                            throw new McpError("create_mode turns the gizmo off and choosing a gizmo or selection mode leaves it: give one or the other.");
                    }
                }

                List<float> gridList = ReadIncrements(call, "transform_snap_increments");
                List<float> rotationList = ReadIncrements(call, "rotation_snap_increments");
                bool reset = call.Bool("reset_snap_increments");
                float? grid = call.Has("transform_snap") ? (float)call.Num("transform_snap") : (float?)null;
                float? rotation = call.Has("rotation_snap") ? (float)call.Num("rotation_snap") : (float?)null;
                //Checked against the lists as they will be once this call's lists are in
                if (grid.HasValue)
                    CheckSnap(grid.Value, gridList ?? (reset ? TransformSnapDefinitions.GridSnapValues.ToList() : SnapIncrementSettings.GridValues.ToList()), "transform_snap", "transform_snap_increments");
                if (rotation.HasValue)
                    CheckSnap(rotation.Value, rotationList ?? (reset ? TransformSnapDefinitions.RotationSnapValues.ToList() : SnapIncrementSettings.RotationValues.ToList()), "rotation_snap", "rotation_snap_increments");

                //Settings the Options menu and the viewport toolbar store, applied through the editor's own
                //settings-effects path (ApplySnapIncrementChange is its public door): menus ticked, toolbar
                //labelled, and one settings packet to the viewer for the lot
                List<string> keys = new List<string>();
                if (reset)
                {
                    SnapIncrementSettings.ResetToDefaults();
                    keys.Add(Settings.TransformSnapIncrements);
                    keys.Add(Settings.RotationSnapIncrements);
                    changed.Add("snap increments reset");
                }
                if (gridList != null)
                {
                    SnapIncrementSettings.SetGridValues(gridList);
                    keys.Add(Settings.TransformSnapIncrements);
                    changed.Add("transform_snap_increments");
                }
                if (rotationList != null)
                {
                    SnapIncrementSettings.SetRotationValues(rotationList);
                    keys.Add(Settings.RotationSnapIncrements);
                    changed.Add("rotation_snap_increments");
                }
                if (grid.HasValue)
                {
                    SettingsManager.SetFloat(Settings.TransformGridSnap, SnapIncrementSettings.NormalizeGrid(grid.Value));
                    keys.Add(Settings.TransformGridSnap);
                    changed.Add("transform_snap");
                }
                if (rotation.HasValue)
                {
                    SettingsManager.SetFloat(Settings.RotationSnapDegrees, SnapIncrementSettings.NormalizeRotation(rotation.Value));
                    keys.Add(Settings.RotationSnapDegrees);
                    changed.Add("rotation_snap");
                }
                foreach ((string argument, string setting, bool value) in toggles)
                {
                    SettingsManager.SetBool(setting, value);
                    keys.Add(setting);
                    changed.Add(argument);
                }
                //Tied together as the two Options items tie them
                if (focus.HasValue)
                {
                    SettingsManager.SetBool(Settings.FocusOnSelected, focus.Value);
                    if (!focus.Value) SettingsManager.SetBool(Settings.FixCameraToSelected, false);
                    changed.Add("focus_on_selected");
                }
                if (fix.HasValue)
                {
                    SettingsManager.SetBool(Settings.FixCameraToSelected, fix.Value);
                    if (fix.Value) SettingsManager.SetBool(Settings.FocusOnSelected, true);
                    changed.Add("fix_camera_to_selected");
                }
                if (focus.HasValue || fix.HasValue)
                {
                    keys.Add(Settings.FocusOnSelected);
                    keys.Add(Settings.FixCameraToSelected);
                }
                if (highlight.HasValue)
                {
                    SettingsManager.SetInteger(Settings.LevelViewerHighlightMode, highlight.Value);
                    keys.Add(Settings.LevelViewerHighlightMode);
                    changed.Add("highlight_mode");
                }
                if (selection.HasValue || gizmo.HasValue)
                {
                    //Choosing either leaves entity creation mode, as the toolbar does
                    editor.ExitViewerCreateMode();
                    if (selection.HasValue)
                    {
                        SettingsManager.SetInteger(Settings.LevelViewerDeepSelectMode, selection.Value);
                        keys.Add(Settings.LevelViewerDeepSelectMode);
                        changed.Add("selection_mode");
                    }
                    if (gizmo.HasValue)
                    {
                        SettingsManager.SetInteger(Settings.LevelViewerGizmoMode, gizmo.Value);
                        keys.Add(Settings.LevelViewerGizmoMode);
                        changed.Add("gizmo_mode");
                    }
                }
                if (keys.Count != 0)
                    editor.ApplySnapIncrementChange(keys.Distinct().ToArray());

                //The toolbar's Create menu: the type's filter on so what is made shows, the gizmo off, Measure off
                if (create.HasValue)
                {
                    if (create.Value == 0)
                        editor.ExitViewerCreateMode();
                    else
                    {
                        ViewerCreateMode.ActiveFunctionType = create.Value;
                        ViewerMeasureMode.Active = false;
                        SettingsManager.SetInteger(Settings.LevelViewerGizmoMode, (int)LevelViewerGizmoMode.None);
                        LevelViewerPanel toolbar = editor.LevelViewerPanel;
                        if (toolbar != null && !toolbar.IsDisposed)
                        {
                            toolbar.ApplyGizmoMode(LevelViewerGizmoMode.None);
                            toolbar.ApplyCreateMode(create.Value);
                            toolbar.ApplyMeasureMode(false);
                        }
                        if (!RenderFilters.IsEnabled(create.Value) && !filterChanges.ContainsKey(create.Value))
                            filterChanges[create.Value] = true;
                        Send.SendSettingsPacket();
                    }
                    changed.Add("create_mode");
                }

                //The Render Filters panel's path: the setting, the panel's ticks, and the filter packet
                if (filterChanges.Count != 0 || sceneChanges.Count != 0)
                {
                    foreach (KeyValuePair<uint, bool> filter in filterChanges)
                        RenderFilters.SetEnabled(filter.Key, filter.Value);
                    foreach (KeyValuePair<SceneFilterKind, bool> filter in sceneChanges)
                        RenderFilters.SetSceneFilterEnabled(filter.Key, filter.Value);
                    RenderFiltersPanel panel = editor.RenderFiltersPanel;
                    if (panel != null && !panel.IsDisposed)
                        panel.RefreshFilters();
                    if (Singleton.ViewportEnabled)
                        Send.SendRenderFilterPacket();
                    if (filterChanges.Count != 0) changed.Add("render_filters");
                    if (sceneChanges.Count != 0) changed.Add("scene_filters");
                }

                LevelViewerPanel viewer = editor.LevelViewerPanel;
                bool hasViewer = viewer != null && !viewer.IsDisposed;

                //The Highlight Zones button's path: the zone table first, then the settings packet
                if (zones.HasValue)
                {
                    SettingsManager.SetBool(Settings.ShowZones, zones.Value);
                    if (hasViewer) viewer.ApplyShowZones(zones.Value);
                    ViewerZoneSync.SendNow();
                    Send.SendSettingsPacket();
                    changed.Add("show_zones");
                }

                //The Live Link Camera menu's path: the setting, then the viewer told to stream its camera or to follow the
                //game's, and the game to follow or have its own back
                if (cameraMode.HasValue)
                {
                    LiveLinkCameraSync.CameraMode mode = LiveLinkCameraSync.NormaliseMode(cameraMode.Value);
                    SettingsManager.SetInteger(Settings.LiveLinkCameraMode, (int)mode);
                    if (hasViewer) viewer.ApplyLiveLinkCameraMode(mode);
                    LiveLinkCameraSync.Refresh();
                    changed.Add("live_link_camera");
                    if (mode != LiveLinkCameraSync.CameraMode.Disabled && !global::OpenCAGE.RuntimeUtilsConnection.LiveLink.Connected)
                        call.Note("Live Link is not connected to the game, so the cameras follow once it is (runtime_utils {action: 'connect'}, with the game running).");
                    else if (mode == LiveLinkCameraSync.CameraMode.GameToViewport)
                        call.Note("The viewport's camera follows the game's from now on, and cannot be moved meanwhile: get_viewport_state reports the game camera it was last put at, and live_link_camera's status whether it follows.");
                }

                //Show State Info: not a stored setting, and put back to none whenever another composite opens
                if (navmesh.HasValue || cover.HasValue)
                {
                    if (navmesh.HasValue) ViewerStateInfoMode.NavMeshState = navmesh.Value;
                    if (cover.HasValue) ViewerStateInfoMode.CoverState = cover.Value;
                    if (hasViewer) viewer.ApplyStateInfo();
                    Send.SendSettingsPacket();
                    changed.Add("state overlays");
                }

                if (!Singleton.ViewportEnabled && changed.Count != 0)
                    call.Note("The viewport is off, so nothing shows these until it is turned on (viewport_action {action: 'enable'}).");
                if ((navmesh.HasValue && navmesh.Value >= 0) || (cover.HasValue && cover.Value >= 0))
                    call.Note("The overlays draw the navigation data written by the last Save & Build (save_level with build=true); they clear when another composite is opened.");
                return new JObject() { ["changed"] = new JArray(changed), ["view"] = ViewState(false) };
            });
        }

        private static Dictionary<uint, bool> ReadRenderFilters(JObject filters)
        {
            Dictionary<uint, bool> result = new Dictionary<uint, bool>();
            if (filters == null)
                return result;
            //'all' first, whatever order the keys came in, so named filters are exceptions to it
            JProperty all = filters.Properties().FirstOrDefault(o => string.Equals(o.Name.Trim(), "all", StringComparison.OrdinalIgnoreCase));
            if (all != null)
            {
                bool on = ReadFlag(all);
                foreach (RenderFilterDefinitions.Definition definition in RenderFilterDefinitions.All)
                    result[definition.FunctionTypeUInt] = on;
            }
            foreach (JProperty property in filters.Properties())
            {
                if (string.Equals(property.Name, "all", StringComparison.OrdinalIgnoreCase)) continue;
                RenderFilterDefinitions.Definition? match = null;
                foreach (RenderFilterDefinitions.Definition definition in RenderFilterDefinitions.All)
                    if (string.Equals(definition.FunctionType.ToString(), property.Name.Trim(), StringComparison.OrdinalIgnoreCase)) { match = definition; break; }
                if (match == null)
                {
                    List<string> near = RenderFilterDefinitions.All.Select(o => o.FunctionType.ToString()).Where(o => o.IndexOf(property.Name.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).Take(10).ToList();
                    throw new McpError("'" + property.Name + "' is not a render filter." + (near.Count != 0 ? " Similar: " + string.Join(", ", near) + "." : " get_viewport_state with list_filters shows them all; collision and occlusion meshes are scene_filters."));
                }
                result[match.Value.FunctionTypeUInt] = ReadFlag(property);
            }
            return result;
        }

        private static Dictionary<SceneFilterKind, bool> ReadSceneFilters(JObject filters)
        {
            Dictionary<SceneFilterKind, bool> result = new Dictionary<SceneFilterKind, bool>();
            if (filters == null)
                return result;
            foreach (JProperty property in filters.Properties())
            {
                SceneFilterDefinition? match = null;
                foreach (SceneFilterDefinition definition in RenderFilterDefinitions.SceneFilters)
                    if (Squash(definition.Kind.ToString()) == Squash(property.Name) || Squash(definition.Label) == Squash(property.Name)) { match = definition; break; }
                if (match == null)
                    throw new McpError("'" + property.Name + "' is not a scene filter: they are " + string.Join(", ", RenderFilterDefinitions.SceneFilters.Select(o => Snake(o.Kind.ToString()))) + ".");
                result[match.Value.Kind] = ReadFlag(property);
            }
            return result;
        }

        private static bool ReadFlag(JProperty property)
        {
            JToken value = property.Value;
            if (value.Type == JTokenType.Boolean) return (bool)value;
            if (value.Type == JTokenType.String && bool.TryParse((string)value, out bool parsed)) return parsed;
            throw new McpError("'" + property.Name + "' must be true or false.");
        }

        private static int? ReadMode(McpCall call, string argument, string[] names)
        {
            if (!call.Has(argument))
                return null;
            string wanted = Squash(call.Str(argument));
            for (int i = 0; i < names.Length; i++)
                if (Squash(names[i]) == wanted) return i;
            throw new McpError("'" + argument + "' must be one of: " + string.Join(", ", names) + ".");
        }

        private static List<float> ReadIncrements(McpCall call, string argument)
        {
            if (!call.Has(argument))
                return null;
            List<float> values = new List<float>();
            foreach (JToken token in call.Array(argument))
            {
                double value;
                try { value = token.Value<double>(); }
                catch { throw new McpError("'" + argument + "' must be a list of numbers."); }
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                    throw new McpError("'" + argument + "' values must be positive numbers (Off, 0, is always offered).");
                if (value > 0) values.Add((float)value);
            }
            if (values.Count > 32)
                throw new McpError("'" + argument + "' can hold at most 32 values.");
            return values;
        }

        private static void CheckSnap(float value, List<float> offered, string argument, string listArgument)
        {
            if (value <= 0f || offered.Any(o => Math.Abs(o - value) < 0.0001f))
                return;
            throw new McpError("'" + argument + "' must be 0 (off) or one of the increments offered: " + string.Join(", ", offered.Where(o => o > 0f).Select(o => Math.Round(o, 4))) + ". Add others with '" + listArgument + "'.");
        }
        #endregion

        #region set_viewport_camera
        private static object SetCamera(McpCall call)
        {
            //Everything is read and checked first, so a bad argument waits for nothing and moves nothing
            bool through = call.Has("look_through") || call.Has("path");
            if (call.Has("look_through") && call.Has("path"))
                throw new McpError("Give 'look_through' (an entity in 'composite') or 'path' (steps from 'composite' down to a nested one), not both.");
            if (through && (call.Has("position") || call.Has("forward") || call.Has("look_at") || call.Has("rotation") || call.Has("up")))
                throw new McpError("Looking through an entity takes the camera's position and direction from it: drop position/forward/look_at/rotation/up, or drop look_through/path.");
            if (!through && (call.Has("composite") || call.Has("follow")))
                throw new McpError("'composite' and 'follow' go with look_through or path (an entity to look through).");
            float fov = ReadFov(call);
            //The viewer takes no camera but the game's while it follows it
            if (LiveLinkCameraSync.CameraFollowsGame)
                throw new McpError("The viewport follows the game camera (live_link_camera 'game_to_viewport'), so it cannot be put anywhere else: set_viewport_view live_link_camera 'disabled' (or 'viewport_to_game') first.");
            if (through)
                return LookThroughEntity(call, fov);

            if (!call.Has("position"))
                throw new McpError("'position' is required: [x, y, z], where the camera goes (or 'look_through', an entity to look through).");
            int ways = (call.Has("forward") ? 1 : 0) + (call.Has("look_at") ? 1 : 0) + (call.Has("rotation") ? 1 : 0);
            if (ways != 1)
                throw new McpError("Say which way the camera looks: 'forward' (a direction), 'look_at' (a point) or 'rotation' ([pitch, yaw, roll] degrees), one of them.");
            if (call.Has("rotation") && call.Has("up"))
                throw new McpError("'rotation' says which way is up itself (its roll): drop 'up'.");
            System.Numerics.Vector3 position = McpValues.ReadVector(call.Token("position"), "position", null);
            System.Numerics.Vector3 forward, up;
            if (call.Has("rotation"))
            {
                System.Numerics.Vector3 rotation = McpValues.ReadVector(call.Token("rotation"), "rotation", null);
                if (!Finite(rotation))
                    throw new McpError("'rotation' takes ordinary numbers of degrees (not NaN or infinity).");
                forward = ForwardOf(rotation);
                up = UpOf(rotation);
            }
            else
            {
                forward = call.Has("forward")
                    ? McpValues.ReadVector(call.Token("forward"), "forward", null)
                    : McpValues.ReadVector(call.Token("look_at"), "look_at", null) - position;
                up = call.Has("up") ? McpValues.ReadVector(call.Token("up"), "up", null) : System.Numerics.Vector3.UnitY;
            }
            if (!Finite(position) || !Finite(forward) || !Finite(up))
                throw new McpError("'position', 'forward', 'look_at' and 'up' take ordinary numbers (not NaN or infinity).");
            if (forward.Length() < 0.0001f)
                throw new McpError(call.Has("forward") ? "'forward' cannot be zero: it is the direction the camera looks." : "'look_at' is where the camera is: give a point away from 'position'.");
            forward = System.Numerics.Vector3.Normalize(forward);
            //Square to the view, as a camera's up is: only its part across the view says anything (which way the camera rolls)
            up -= forward * System.Numerics.Vector3.Dot(up, forward);
            if (up.Length() < 0.0001f)
                throw new McpError("'up' lies along the direction the camera looks, so it cannot say which way is up: give another (e.g. [0, 0, 1] when looking straight up or down).");
            up = System.Numerics.Vector3.Normalize(up);

            AwaitViewer(call);
            return PlaceAndReport(call, position, forward, up, float.IsNaN(fov) ? 0f : fov, null, false, new JObject(), out ViewportCamera _);
        }

        /// <summary>'fov' as the viewer takes it: NaN when not given, -1 for 0 (the viewport's own back), else 1-170 degrees (vertical).</summary>
        private static float ReadFov(McpCall call)
        {
            if (!call.Has("fov"))
                return float.NaN;
            double fov = call.Num("fov", double.NaN);
            if (double.IsNaN(fov) || double.IsInfinity(fov))
                throw new McpError("'fov' is the vertical field of view in degrees (1-170), or 0 for the viewport's own.");
            if (fov == 0)
                return -1f;
            if (fov < 1 || fov > 170)
                throw new McpError("'fov' must be from 1 to 170 degrees (the vertical field of view), or 0 for the viewport's own.");
            return (float)fov;
        }

        /// <summary>
        /// Send the camera somewhere (and, with <paramref name="lookThrough"/>, through an entity), then ask the viewer where it
        /// ended up - the answer comes after the placement, which runs first there - and report that, with what the game's
        /// camera made of it while it follows the viewport. Not on the UI thread; the viewer has been waited for.
        /// </summary>
        private static JObject PlaceAndReport(McpCall call, System.Numerics.Vector3 position, System.Numerics.Vector3 forward, System.Numerics.Vector3 up, float fov, List<uint> lookThrough, bool follow, JObject result, out ViewportCamera camera)
        {
            int posesBefore = 0;
            bool following = false;
            McpEditor.UI(() =>
            {
                RequireViewportOn();
                if (LiveLinkCameraSync.CameraFollowsGame)
                    throw new McpError("The viewport follows the game camera (live_link_camera 'game_to_viewport'), so it cannot be put anywhere else: set_viewport_view live_link_camera 'disabled' (or 'viewport_to_game') first.");
                posesBefore = LiveLinkCameraSync.PosesReceived;
                following = LiveLinkCameraSync.Enabled;
                Send.SendViewportSetCamera(position, forward, up, fov, lookThrough, follow);
                if (SettingsManager.GetBool(Settings.FixCameraToSelected))
                    call.Note("fix_camera_to_selected is on: the camera goes back to the selection when that moves (set_viewport_view turns it off).");
            });

            /* Where it ended up, which only the viewer knows (looking through an entity, it is where the viewer draws it). A level
               viewer from before it could be asked moved the camera all the same (to this side's working-out): that is reported. */
            try
            {
                Packet answer = AskViewer(call, null, null, PlacedTimeout);
                ViewportCamera placed = McpEditor.UI(() => ReadCamera(answer));
                camera = placed;
            }
            catch (McpError e)
            {
                camera = new ViewportCamera()
                {
                    Position = position,
                    Forward = forward,
                    Up = up,
                    Fov = Math.Max(0f, fov),
                    InLevelSpace = McpEditor.UI(() => IsAtRoot(McpEditor.Editor.CompositeBrowser?.Content, McpEditor.Editor.CompositeDisplay)),
                    Scene = McpEditor.UI(() => SceneComposite()),
                };
                result["camera_reported"] = false;
                call.Note("The camera was sent there, but the viewport did not say where it ended up (" + e.Message.TrimEnd('.') + "): 'camera' is what was sent.");
            }
            JObject cameraJson = camera.ToJson();
            ViewportCamera reported = camera;
            McpEditor.UI(() => AddSteppedIntoSpace(cameraJson, reported));
            result["camera"] = cameraJson;
            if (fov > 0f && Math.Abs(camera.Fov - fov) > 0.5f)
                call.Note("The viewport kept its own field of view (" + Math.Round(camera.Fov, 1) + " degrees, not " + Math.Round(fov, 1) + "): it is a level viewer from before set_viewport_camera could change it.");
            if (!camera.InLevelSpace)
                call.Note("The viewport is not showing the level's root composite, so positions are in the space of " + (camera.Scene != null ? camera.Scene.name : "the composite it shows") + (following ? ", and the game's camera does not follow it." : "."));
            string shown = McpEditor.UI(() => DescribeShown());
            if (shown != null)
                result["showing"] = shown;

            //The game's camera follows: the viewer streams where it moved to, and that is what the game is sent
            if (following)
            {
                McpEditor.WaitFor(call, () => LiveLinkCameraSync.PosesReceived != posesBefore, TimeSpan.FromSeconds(1), "Waiting for the viewport to report its camera");
                //Passed on to the game as it arrives: give the game's answer a moment to come back
                Thread.Sleep(300);
                McpEditor.UI(() =>
                {
                    if (LiveLinkCameraSync.LastStatus != null)
                        result["game_camera_status"] = LiveLinkCameraSync.LastStatus;
                });
            }
            return result;
        }

        //Placements of a composite counted when choosing one to look through
        private const int PlacementLimit = 20;

        /// <summary>An entity to look through, resolved to the viewport's scene. UI thread.</summary>
        private sealed class LookThroughTarget
        {
            public Composite Scene;           //the composite the viewer built its scene from
            public List<uint> Path;           //from Scene down to the entity, the entity last
            public Composite Owner;
            public Entity Entity;
            public cTransform Pose;           //this side's working-out of where it is, in Scene's space (for a viewer from before look-through)
            public float OwnFov = float.NaN;  //a CameraResource's 'fov'
            public int Placements = 1;        //how many placements of its composite the scene has
            public bool Opened;               //its composite was opened, as the scene had none of it
        }

        private static object LookThroughEntity(McpCall call, float fov)
        {
            bool follow = call.Bool("follow");
            int populateEvents = -1;
            LookThroughTarget target = McpEditor.UI(() =>
            {
                McpEditor.RequireLevel(forEditing: false);
                RequireNotSaving();
                RequireViewportOn();
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                int before = ViewerPopulateSync.PopulateEvents;
                LookThroughTarget resolved = ResolveLookThrough(call, commands);
                if (resolved.Opened)
                    populateEvents = before;
                return resolved;
            });
            AwaitViewer(call, populateEvents);

            JObject result = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                JObject entity = McpScript.Brief(commands, target.Owner, target.Entity);
                entity.Remove("position");
                entity.Remove("rotation");
                entity["composite"] = target.Owner.name;
                JObject through = new JObject()
                {
                    ["entity"] = entity,
                    ["scene"] = target.Scene.name,
                    ["path_ids"] = new JArray(target.Path.Select(o => McpScript.Id(new ShortGuid(o)))),
                    ["following"] = follow,
                };
                if (target.Placements > 1)
                    call.Note(target.Owner.name + " is placed " + (target.Placements >= PlacementLimit ? "at least " : "") + target.Placements + " times in the scene the viewport shows; it looked through the first (path_ids). Use 'composite' '" + target.Scene.name + "' with 'path' (the instance steps down to it; get_placements lists them) to choose another.");
                if (target.Opened)
                    call.Note("The viewport's scene had no placement of " + target.Owner.name + ", so it was opened on its own.");
                if (target.Entity.GetParameter("camera_transformation")?.content is cTransform offset && (offset.position != System.Numerics.Vector3.Zero || offset.rotation != System.Numerics.Vector3.Zero))
                    call.Note("Its camera_transformation (" + McpValues.Vector(offset.position) + ", " + McpValues.Vector(offset.rotation) + ") is not applied: the view is from its position.");
                return new JObject() { ["looking_through"] = through };
            });

            float send = !float.IsNaN(fov) ? fov : (!float.IsNaN(target.OwnFov) ? target.OwnFov : 0f);
            System.Numerics.Vector3 rotation = target.Pose?.rotation ?? System.Numerics.Vector3.Zero;
            System.Numerics.Vector3 position = target.Pose?.position ?? System.Numerics.Vector3.Zero;
            return PlaceAndReport(call, position, ForwardOf(rotation), UpOf(rotation), send, target.Path, follow, result, out ViewportCamera _);
        }

        /// <summary>
        /// The entity look_through/path name, and the instance path down to it from the composite the viewer built its scene
        /// from: the placement stepped into in the editor, else the first the scene has; a composite the scene does not place
        /// is opened. UI thread.
        /// </summary>
        private static LookThroughTarget ResolveLookThrough(McpCall call, Commands commands)
        {
            CompositeDisplay display = McpEditor.Editor.CompositeDisplay;
            bool shown = display != null && !display.IsDisposed && display.Populated && display.Composite != null;
            //A path a result gave starts where it says (its 'from', else the root)
            JToken pathToken = call.Token("path");
            bool resultPath = !call.Has("composite") && pathToken is JObject;
            Composite composite = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite"))
                : resultPath ? McpScript.FindComposite(commands, PathStart(call))
                : shown ? display.Composite
                : throw new McpError("No composite is on screen: say which one the entity is in with 'composite'.");

            LookThroughTarget target = new LookThroughTarget();
            List<Entity> steps = new List<Entity>();
            if (call.Has("path"))
            {
                ShortGuid[] ids = McpScript.ResolvePath(commands, composite, McpScript.PathSteps(pathToken), out Composite owner, out Entity entity);
                Composite walk = composite;
                foreach (ShortGuid id in ids.Where(o => o != ShortGuid.Invalid))
                {
                    Entity step = walk.GetEntityByID(id);
                    steps.Add(step);
                    walk = McpScript.InstancedComposite(commands, step) ?? walk;
                }
                target.Owner = owner;
                target.Entity = entity;
            }
            else
            {
                target.Entity = McpScript.FindEntity(commands, composite, call.Str("look_through"));
                target.Owner = composite;
                steps.Add(target.Entity);
            }
            if (target.Entity is AliasEntity || target.Entity is ProxyEntity || target.Entity is VariableEntity)
                throw new McpError("Look through the entity itself, not " + McpScript.Kind(target.Entity) + " '" + McpScript.EntityName(commands, target.Owner, target.Entity) + "' (an alias or proxy stands for an entity elsewhere: give the path to it).");

            //Where 'composite' sits in the scene the viewer has: the scene itself, the placement the editor stepped into, or a placement of it
            Composite scene = shown ? (display.Path?.AllComposites.FirstOrDefault() ?? display.Composite) : null;
            List<Entity> prefix = null;
            if (scene == composite)
                prefix = new List<Entity>();
            else if (shown && display.Composite == composite && display.Path.AllEntities.Count != 0)
                prefix = new List<Entity>(display.Path.AllEntities);
            else if (scene != null)
            {
                List<List<Entity>> placements = PlacementsIn(commands, scene, composite, PlacementLimit);
                if (placements.Count != 0)
                {
                    prefix = placements[0];
                    target.Placements = placements.Count;
                }
            }
            if (prefix == null)
            {
                //Not in the scene at all: shown on its own, as capture_viewport's 'composite' does
                OpenTopLevel(composite);
                Singleton.Editor.CompositeDisplay?.ShowLevelViewerPanel(false);
                scene = composite;
                prefix = new List<Entity>();
                target.Opened = true;
            }

            target.Scene = scene;
            target.Path = prefix.Concat(steps).Select(o => o.shortGUID.AsUInt32).ToList();
            target.Pose = PoseAlong(prefix.Concat(steps));
            if (target.Entity is FunctionEntity function && function.function.IsFunctionType && function.function.AsFunctionType == FunctionType.CameraResource)
            {
                //Unset, a CameraResource's fov is its type's default (describe_function_type)
                float own = target.Entity.GetParameter("fov")?.content is cFloat value ? value.value : 45f;
                if (own >= 1f && own <= 170f)
                    target.OwnFov = own;
            }
            return target;
        }

        /// <summary>The instance chains (outermost first) by which <paramref name="scene"/> places <paramref name="composite"/>: at most <paramref name="limit"/>.</summary>
        private static List<List<Entity>> PlacementsIn(Commands commands, Composite scene, Composite composite, int limit)
        {
            List<List<Entity>> found = new List<List<Entity>>();
            McpPlacements walk = new McpPlacements(commands);
            walk.Walk(scene, step =>
            {
                if (McpScript.InstancedComposite(commands, step.Entity) == composite)
                {
                    found.Add(new List<Entity>(step.Chain));
                    return found.Count < limit;
                }
                return true;
            });
            return found;
        }

        /// <summary>Where the last of <paramref name="chain"/> (instances, then the entity) is, composed down from the first's composite.</summary>
        private static cTransform PoseAlong(IEnumerable<Entity> chain)
        {
            cTransform pose = null;
            foreach (Entity entity in chain)
                pose = InstanceTransform.Compose(pose, InstanceTransform.TransformOf(entity) ?? new cTransform(System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero));
            return pose;
        }

        /// <summary>What a nested set_viewport_camera (capture_viewport's 'camera') did, in words.</summary>
        private static string DescribePlacement(JObject result)
        {
            JObject camera = result?["camera"] as JObject;
            if (camera == null)
                return null;
            string through = (string)result["looking_through"]?["entity"]?["name"];
            return (through != null ? "looking through " + through : "the camera placed") + " at " + camera["position"]?.ToString(Newtonsoft.Json.Formatting.None)
                + ", rotation " + camera["rotation"]?.ToString(Newtonsoft.Json.Formatting.None) + ", fov " + camera["fov"];
        }

        /// <summary>A call of another of these tools with <paramref name="args"/> (an argument of this one), its argument names checked.</summary>
        private static McpCall NestedCall(McpCall call, string toolName, JObject args, string argument)
        {
            McpTool tool = McpTools.Find(toolName);
            if (tool == null)
                throw new McpError(toolName + " is not available.");
            JObject properties = tool.InputSchema?["properties"] as JObject ?? new JObject();
            List<string> unknown = args.Properties().Select(o => o.Name).Where(o => properties[o] == null).ToList();
            if (unknown.Count != 0)
                throw new McpError("'" + argument + "' has " + string.Join(", ", unknown.Select(o => "'" + o + "'")) + ", which " + toolName + " does not take. It takes: " + string.Join(", ", properties.Properties().Select(o => o.Name)) + ".");
            return new McpCall(tool, args, call.Cancel, (text, done, total) => call.Progress(text, done, total));
        }

        private const string FollowingGameNote = "The viewport follows the game camera (live_link_camera 'game_to_viewport'), so it was not framed on the selection: set_viewport_view live_link_camera 'disabled' first to frame things.";

        private static bool Finite(System.Numerics.Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsNaN(v.Y) && !float.IsNaN(v.Z)
                && !float.IsInfinity(v.X) && !float.IsInfinity(v.Y) && !float.IsInfinity(v.Z);
        }
        #endregion

        #region Asking the viewport
        /// <summary>
        /// The viewport camera as the viewer reported it when asked: in the space of the composite it built its scene from (the
        /// level's world when that is the level's root), metres, Y up; its vertical field of view in degrees.
        /// </summary>
        internal sealed class ViewportCamera
        {
            public System.Numerics.Vector3 Position;
            public System.Numerics.Vector3 Forward;
            public System.Numerics.Vector3 Up;
            public float Fov;
            /// <summary>The scene is the level's root: the pose is in world space.</summary>
            public bool InLevelSpace;
            /// <summary>The composite the scene was built from (null if this side does not know it).</summary>
            public Composite Scene;
            public int Width;
            public int Height;

            /// <summary>The rotation an entity there would need to look the same way (degrees, as a position parameter's).</summary>
            public System.Numerics.Vector3 Rotation => RotationFacing(Forward, Up);

            public float HorizontalFov => Width > 0 && Height > 0
                ? (float)(2 * Math.Atan(Math.Tan(Fov * Math.PI / 360.0) * Width / Height) * 180.0 / Math.PI) : 0f;

            public string Space => InLevelSpace ? "world" : "composite " + (Scene != null ? Scene.name : "on screen") + " (its own origin)";

            public JObject ToJson()
            {
                JObject json = new JObject()
                {
                    ["position"] = McpValues.Vector(Position),
                    ["rotation"] = McpValues.Vector(Rotation),
                    ["forward"] = McpValues.Vector(Forward),
                    ["up"] = McpValues.Vector(Up),
                    ["space"] = Space,
                };
                //0 when unknown (what was sent to a viewer that did not say)
                if (Fov > 0f)
                    json["fov"] = Math.Round(Fov, 2);
                if (Width > 0 && Height > 0)
                {
                    json["fov_horizontal"] = Math.Round(HorizontalFov, 2);
                    json["viewport_px"] = new JArray(Width, Height);
                }
                return json;
            }
        }

        private static int _lastQuery;

        /// <summary>
        /// Ask the viewer where its camera is, and what lies under <paramref name="points"/> (0-1 fractions) or along
        /// <paramref name="rays"/> (scene space): its answer, or an error naming why not (not connected, busy past the timeout,
        /// a level viewer from before this, or nothing to answer from). Not on the UI thread.
        /// </summary>
        private static Packet AskViewer(McpCall call, List<float[]> points, List<ViewportPickRay> rays, TimeSpan timeout)
        {
            uint id = (uint)Interlocked.Increment(ref _lastQuery) | 0x40000000u;
            Packet answer = null;
            Action<Packet> onAnswer = packet =>
            {
                if (packet.viewport_query_id == id)
                    Volatile.Write(ref answer, packet);
            };
            Send.ViewportQueryAnswered += onAnswer;
            try
            {
                if (!McpEditor.UI(() => Send.SendViewportQuery(id, points, rays)))
                    throw new McpError("The viewport is not connected (get_viewport_state says whether it is on and running).");
                if (!McpEditor.WaitFor(call, () => Volatile.Read(ref answer) != null || !Send.Connected, timeout, "Asking the viewport"))
                    throw new McpError("The viewport did not answer within " + (int)timeout.TotalSeconds + " s: it may be busy building its scene (get_viewport_state says when it is ready), or it is a level viewer from before OpenCAGE could ask it (rebuild Dependencies/LevelViewer).");
                Packet answered = Volatile.Read(ref answer);
                if (answered == null)
                    throw new McpError("The viewport disconnected before it answered (it may have crashed): get_viewport_state with log_lines shows its output.");
                if (!string.IsNullOrEmpty(answered.viewport_query_error))
                    throw new McpError("The viewport could not answer: " + answered.viewport_query_error + ".");
                return answered;
            }
            finally
            {
                Send.ViewportQueryAnswered -= onAnswer;
            }
        }

        //A settled viewer answers within a frame or two (64 picks take well under a second); a query waits this long for one still
        //busy (an edit's spawn) before taking it for a viewer that cannot answer
        private static readonly TimeSpan CameraTimeout = TimeSpan.FromSeconds(15);
        //...and after a placement, which was only sent once the viewer had settled: no answer by then means it cannot answer
        private static readonly TimeSpan PlacedTimeout = TimeSpan.FromSeconds(8);

        /// <summary>The camera in a viewer's answer, with the scene's composite looked up. UI thread.</summary>
        private static ViewportCamera ReadCamera(Packet answer)
        {
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            return new ViewportCamera()
            {
                Position = answer.camera_position,
                Forward = answer.camera_forward,
                Up = answer.camera_up,
                Fov = answer.camera_fov,
                InLevelSpace = answer.camera_in_level_space,
                Scene = answer.camera_scene_composite != 0 && commands != null ? commands.GetComposite(new ShortGuid(answer.camera_scene_composite)) : null,
                Width = answer.viewport_width,
                Height = answer.viewport_height,
            };
        }

        /// <summary>
        /// While the editor is stepped into an instance (the viewport's scene is the composite the path starts from), the camera
        /// in the space of the composite on screen as well - where a position parameter of an entity there is - as
        /// 'in_composite_on_screen'. Composed down the stepped-into instances (alias overrides of their positions not applied).
        /// UI thread.
        /// </summary>
        private static void AddSteppedIntoSpace(JObject json, ViewportCamera camera)
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (json == null || camera == null || display == null || display.IsDisposed || !display.Populated || display.Composite == null)
                return;
            List<Entity> drill = display.Path?.AllEntities;
            if (drill == null || drill.Count == 0 || camera.Scene != (display.Path.AllComposites.FirstOrDefault() ?? display.Composite))
                return;
            cTransform placement = PoseAlong(drill);
            if (placement == null)
                return;
            System.Numerics.Quaternion inverse = System.Numerics.Quaternion.Inverse(InstanceTransform.ToQuaternion(placement.rotation));
            System.Numerics.Vector3 position = System.Numerics.Vector3.Transform(camera.Position - placement.position, inverse);
            System.Numerics.Vector3 forward = System.Numerics.Vector3.Transform(camera.Forward, inverse);
            System.Numerics.Vector3 up = System.Numerics.Vector3.Transform(camera.Up, inverse);
            json["in_composite_on_screen"] = new JObject()
            {
                ["composite"] = display.Composite.name,
                ["position"] = McpValues.Vector(position),
                ["rotation"] = McpValues.Vector(RotationFacing(forward, up)),
            };
        }

        /// <summary>The way an entity with this rotation (degrees, yaw then pitch then roll) faces: its +Z.</summary>
        internal static System.Numerics.Vector3 ForwardOf(System.Numerics.Vector3 rotationDegrees)
        {
            return System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ, InstanceTransform.ToQuaternion(rotationDegrees)));
        }

        /// <summary>Which way is up for an entity with this rotation: its +Y.</summary>
        internal static System.Numerics.Vector3 UpOf(System.Numerics.Vector3 rotationDegrees)
        {
            return System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitY, InstanceTransform.ToQuaternion(rotationDegrees)));
        }

        /// <summary>
        /// The rotation (degrees, as a position parameter takes it) that faces an entity along <paramref name="forward"/> with
        /// <paramref name="up"/> as its up (roll); an up along the view, or none, keeps it level.
        /// </summary>
        internal static System.Numerics.Vector3 RotationFacing(System.Numerics.Vector3 forward, System.Numerics.Vector3 up)
        {
            if (forward.LengthSquared() < 1e-12f)
                return System.Numerics.Vector3.Zero;
            System.Numerics.Vector3 f = System.Numerics.Vector3.Normalize(forward);
            System.Numerics.Vector3 u = up - f * System.Numerics.Vector3.Dot(up, f);
            if (u.LengthSquared() < 1e-8f)
            {
                //Straight up or down: level, with the world's up (or forward) standing in
                u = System.Numerics.Vector3.UnitY - f * f.Y;
                if (u.LengthSquared() < 1e-8f)
                    u = (f.Y > 0 ? -1 : 1) * System.Numerics.Vector3.UnitZ;
            }
            u = System.Numerics.Vector3.Normalize(u);
            System.Numerics.Vector3 right = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(u, f));
            //Rows are where +X, +Y and +Z go (System.Numerics multiplies row vectors)
            System.Numerics.Matrix4x4 basis = new System.Numerics.Matrix4x4(
                right.X, right.Y, right.Z, 0,
                u.X, u.Y, u.Z, 0,
                f.X, f.Y, f.Z, 0,
                0, 0, 0, 1);
            return InstanceTransform.ToEulerDegrees(System.Numerics.Quaternion.Normalize(System.Numerics.Quaternion.CreateFromRotationMatrix(basis)));
        }
        #endregion

        #region pick_in_viewport
        private const int MaxPicks = 64;

        private static object Pick(McpCall call)
        {
            List<float[]> points = new List<float[]>();
            List<ViewportPickRay> rays = new List<ViewportPickRay>();
            if (call.Has("points"))
            {
                int index = 0;
                foreach (JToken token in call.Array("points"))
                {
                    JArray pair = token as JArray;
                    double x, y;
                    try
                    {
                        if (pair == null || pair.Count != 2) throw new FormatException();
                        x = pair[0].Value<double>();
                        y = pair[1].Value<double>();
                    }
                    catch
                    {
                        throw new McpError("points[" + index + "] must be [x, y]: two fractions of the picture, 0-1 across and down.");
                    }
                    if (double.IsNaN(x) || double.IsNaN(y) || x < 0 || x > 1 || y < 0 || y > 1)
                        throw new McpError("points[" + index + "] is outside the picture: x and y are fractions 0-1 (pixel / picture width, pixel / picture height).");
                    points.Add(new[] { (float)x, (float)y });
                    index++;
                }
            }
            if (call.Has("rays"))
            {
                int index = 0;
                foreach (JToken token in call.Array("rays"))
                {
                    JObject ray = token as JObject;
                    if (ray == null || ray["origin"] == null || ray["direction"] == null)
                        throw new McpError("rays[" + index + "] must be {origin: [x, y, z], direction: [x, y, z]}.");
                    System.Numerics.Vector3 origin = McpValues.ReadVector(ray["origin"], "rays[" + index + "].origin", null);
                    System.Numerics.Vector3 direction = McpValues.ReadVector(ray["direction"], "rays[" + index + "].direction", null);
                    if (!Finite(origin) || !Finite(direction) || direction.Length() < 0.0001f)
                        throw new McpError("rays[" + index + "] needs ordinary numbers and a direction that is not zero.");
                    rays.Add(new ViewportPickRay() { origin = new[] { origin.X, origin.Y, origin.Z }, direction = new[] { direction.X, direction.Y, direction.Z } });
                    index++;
                }
            }
            if (points.Count + rays.Count == 0)
                throw new McpError("Say where to look: 'points' ([[x, y], ...] fractions of the capture_viewport picture; [[0.5, 0.5]] is the middle) or 'rays' ([{origin, direction}, ...]).");
            if (points.Count + rays.Count > MaxPicks)
                throw new McpError("At most " + MaxPicks + " points and rays together (" + (points.Count + rays.Count) + " given): split them over several calls.");
            bool materials = call.Bool("materials", true);

            bool viewportOn = McpEditor.UI(() =>
            {
                McpEditor.RequireLevel(forEditing: false);
                RequireNotSaving();
                return Singleton.ViewportEnabled;
            });
            if (!viewportOn)
            {
                //Nothing is drawn to look at: rays meet the level's collision instead (as raycast casts them); a picture's points need the viewport
                if (points.Count != 0)
                    throw new McpError(McpErrorCodes.Refused, "The viewport is turned off, so there is no picture to pick points of. viewport_action {action: 'enable'} turns it on. Without it, give 'rays' only (they are cast against the level's collision), or use raycast (rays, the floor under points, line of sight).");
                return PickByCollision(call, rays);
            }
            AwaitViewer(call);
            Packet answer = AskViewer(call, points, rays, CameraTimeout);

            return McpEditor.UI(() =>
            {
                ViewportCamera camera = ReadCamera(answer);
                List<ViewportPickResult> picks = answer.viewport_pick_results ?? new List<ViewportPickResult>();
                if (picks.Count != points.Count + rays.Count)
                    throw new McpError("The viewport answered " + picks.Count + " of " + (points.Count + rays.Count) + " points and rays: it may be a level viewer from before pick_in_viewport (rebuild Dependencies/LevelViewer).");
                Dictionary<string, JObject> described = materials ? new Dictionary<string, JObject>(StringComparer.Ordinal) : null;
                JArray results = new JArray();
                for (int i = 0; i < picks.Count; i++)
                {
                    JToken input = i < points.Count
                        ? (JToken)new JArray(Math.Round(points[i][0], 4), Math.Round(points[i][1], 4))
                        : new JObject() { ["origin"] = new JArray(rays[i - points.Count].origin.Select(o => Math.Round(o, 4))), ["direction"] = new JArray(rays[i - points.Count].direction.Select(o => Math.Round(o, 4))) };
                    results.Add(DescribePick(picks[i], camera, input, described));
                }
                JObject result = new JObject()
                {
                    ["space"] = camera.Space,
                    ["hits"] = picks.Count(o => o != null && o.hit),
                    ["results"] = results,
                };
                if (described != null && described.Count != 0)
                    result["materials"] = new JObject(described.Select(o => new JProperty(o.Key, o.Value)));
                result["camera"] = camera.ToJson();
                if (picks.Any(o => o != null && !o.hit))
                    call.Note("A point that hits nothing looks at the sky or past what is drawn (an entity filtered out, or beyond the composite in focus).");
                return result;
            });
        }

        /// <summary>
        /// pick_in_viewport's rays with the viewport off: cast against the level's collision as it is in the editor (raycast's
        /// soup, solid colliders), so the caller still learns what each ray meets - its point, normal, distance and the entity
        /// whose collision it is, with its placement path - rather than being refused. World space.
        /// </summary>
        private static object PickByCollision(McpCall call, List<ViewportPickRay> rays)
        {
            JArray asked = new JArray(rays.Select(o => new JObject()
            {
                ["from"] = new JArray(o.origin.Select(v => (double)v)),
                ["direction"] = new JArray(o.direction.Select(v => (double)v)),
            }));
            McpCall raycast = NestedCall(call, "raycast", new JObject() { ["rays"] = asked, ["detail"] = "full" }, "rays");
            object answer = McpTools.Find("raycast").Run(raycast);
            JObject result = answer as JObject ?? JObject.FromObject(answer, McpJson.Serializer);
            foreach (string note in raycast.Notes) call.Note(note);
            call.Note("The viewport is off, so the rays were cast against the level's collision (solid colliders, as raycast casts them) rather than what the viewport draws: invisible collision counts, and models without collision do not. viewport_action {action: 'enable'} turns the viewport on.");
            result["source"] = "collision (the viewport is off)";
            return result;
        }

        /// <summary>
        /// One pick for a reader: where it hit, the entity drawing it with the instances it is placed through from the scene
        /// (outermost first - the room), its model and material. <paramref name="materials"/>, when given, collects each
        /// material once (shader and textures). UI thread.
        /// </summary>
        private static JObject DescribePick(ViewportPickResult pick, ViewportCamera camera, JToken input, Dictionary<string, JObject> materials)
        {
            JObject row = new JObject();
            if (input != null)
                row[input is JArray ? "point" : "ray"] = input;
            row["hit"] = pick != null && pick.hit;
            if (pick == null || !pick.hit)
                return row;
            row["position"] = Floats(pick.position);
            row["normal"] = Floats(pick.normal);
            row["distance"] = Math.Round(pick.distance, 3);
            //What drew it: a model, an entity's stand-in shape or icon ("preview"), or an occlusion mesh
            if (!string.IsNullOrEmpty(pick.kind))
                row["surface"] = pick.kind;

            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            Commands commands = content?.Level?.Commands;
            int steps = Math.Min(pick.path_entities?.Count ?? 0, pick.path_composites?.Count ?? 0);
            if (commands != null && steps != 0)
            {
                JArray instances = new JArray();
                JArray ids = new JArray();
                for (int i = 0; i < steps; i++)
                {
                    Composite owner = commands.GetComposite(new ShortGuid(pick.path_composites[i]));
                    Entity entity = owner?.GetEntityByID(new ShortGuid(pick.path_entities[i]));
                    ids.Add(McpScript.Id(new ShortGuid(pick.path_entities[i])));
                    if (owner == null || entity == null)
                    {
                        //Gone since the viewer drew it (deleted a moment ago), or an override the viewer made of its own
                        if (i == steps - 1) row["entity"] = new JObject() { ["id"] = McpScript.Id(new ShortGuid(pick.path_entities[i])), ["note"] = "not in the level's script now" };
                        continue;
                    }
                    if (i == steps - 1)
                    {
                        JObject leaf = McpScript.Brief(commands, owner, entity);
                        leaf.Remove("position");
                        leaf.Remove("rotation");
                        leaf["composite"] = owner.name;
                        row["entity"] = leaf;
                    }
                    else
                        instances.Add(new JObject()
                        {
                            ["id"] = McpScript.Id(entity.shortGUID),
                            ["name"] = McpScript.EntityName(commands, owner, entity),
                            ["composite"] = McpScript.InstancedComposite(commands, entity)?.name,
                        });
                }
                if (instances.Count != 0)
                    row["instances"] = instances;
                row["path_ids"] = ids;
            }

            if (!string.IsNullOrEmpty(pick.model))
                row["model"] = pick.model + (string.IsNullOrEmpty(pick.lod) ? "" : " (" + pick.lod + ")");
            if (pick.submesh >= 0)
                row["submesh"] = pick.submesh;
            if (!string.IsNullOrEmpty(pick.material))
            {
                row["material"] = pick.material;
                if (materials != null && !materials.ContainsKey(pick.material))
                    materials[pick.material] = DescribeMaterialBriefly(content?.Level, pick.material, pick.material_index);
            }
            return row;
        }

        /// <summary>A material's index, shader and the textures its samplers use (describe_material has the rest). UI thread.</summary>
        private static JObject DescribeMaterialBriefly(CathodeLib.Level level, string name, int index)
        {
            List<Materials.Material> entries = level?.Materials?.Entries;
            if (entries == null)
                return new JObject() { ["note"] = "the level's materials are not loaded" };
            Materials.Material material = index >= 0 && index < entries.Count && entries[index] != null
                && (string.Equals(entries[index].Name, name, StringComparison.Ordinal) || string.Equals(level.Materials.GetMaterialName(entries[index]), name, StringComparison.Ordinal))
                ? entries[index]
                : entries.FirstOrDefault(o => o != null && string.Equals(o.Name, name, StringComparison.Ordinal));
            if (material == null)
                return new JObject() { ["note"] = "not among the level's materials now (list_materials)" };
            JObject full = McpMaterialTools.DescribeMaterial(level, material, false);
            JObject brief = new JObject() { ["index"] = full["index"], ["shader"] = full["shader"] };
            JObject textures = new JObject();
            foreach (JObject sampler in (full["samplers"] as JArray ?? new JArray()).OfType<JObject>())
                if (!string.IsNullOrEmpty((string)sampler["texture"]))
                    textures[(string)sampler["name"]] = sampler["texture"];
            brief["textures"] = textures;
            return brief;
        }

        private static JArray Floats(float[] values)
        {
            return values == null || values.Length < 3 ? null : McpValues.Vector(new System.Numerics.Vector3(values[0], values[1], values[2]));
        }
        #endregion

        #region For other tools
        /// <summary>
        /// The composite the viewport built its scene from - the one opened, not one stepped into from it - whose space the
        /// viewport's camera and picks are in (the level's world when it is the root). Null when none is on screen. UI thread.
        /// </summary>
        internal static Composite SceneComposite()
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display == null || display.IsDisposed || !display.Populated || display.Composite == null)
                return null;
            return display.Path?.AllComposites.FirstOrDefault() ?? display.Composite;
        }

        /// <summary>
        /// Where the viewport camera is now (any time: it need not be streamed for Live Link). Waits (up to 5 min) for the
        /// viewport to finish loading. Throws an McpError a tool can pass on when there is no viewport to ask. Not on the UI thread.
        /// </summary>
        internal static ViewportCamera QueryCamera(McpCall call)
        {
            McpEditor.UI(() => { RequireViewportOn(); });
            AwaitViewer(call);
            Packet answer = AskViewer(call, null, null, CameraTimeout);
            return McpEditor.UI(() => ReadCamera(answer));
        }

        /// <summary>
        /// Put the viewport camera at <paramref name="position"/> looking along <paramref name="forward"/> with <paramref name="up"/>
        /// as up, in the scene's space (see <see cref="SceneComposite"/>), and return where it ended up. <paramref name="fov"/>:
        /// above 0 a vertical field of view in degrees held until the camera is next moved by hand, below 0 the viewport's own
        /// back, 0 left as it is. Not on the UI thread.
        /// </summary>
        internal static ViewportCamera PlaceCamera(McpCall call, System.Numerics.Vector3 position, System.Numerics.Vector3 forward, System.Numerics.Vector3 up, float fov = 0f)
        {
            if (!Finite(position) || !Finite(forward) || !Finite(up) || forward.LengthSquared() < 1e-8f)
                throw new McpError("The camera needs a finite position and a direction to look along.");
            AwaitViewer(call);
            PlaceAndReport(call, position, System.Numerics.Vector3.Normalize(forward), up, fov, null, false, new JObject(), out ViewportCamera camera);
            return camera;
        }

        /// <summary>
        /// Look through the entity at <paramref name="instancePath"/> (entity ids from <see cref="SceneComposite"/> down through
        /// instances, the entity last - Animation Mode's root and drill, then the animated entity's own steps) from where the
        /// viewport draws it now, an Animation Mode pose included, along its +Z; return where the camera ended up.
        /// <paramref name="fov"/>: NaN takes a CameraResource's own 'fov' (else leaves the field of view), otherwise as for
        /// <see cref="PlaceCamera"/>. <paramref name="follow"/> keeps the camera on the entity as it moves until it is moved by
        /// hand. Not on the UI thread.
        /// </summary>
        internal static ViewportCamera LookThrough(McpCall call, IList<uint> instancePath, float fov = float.NaN, bool follow = false)
        {
            if (instancePath == null || instancePath.Count == 0)
                throw new McpError("Say which entity to look through: its instance path from the composite the viewport shows.");
            float ownFov = float.NaN;
            cTransform pose = McpEditor.UI(() =>
            {
                RequireViewportOn();
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                Composite scene = SceneComposite();
                if (scene == null)
                    throw new McpError("No composite is on screen to look through an entity of.");
                List<Entity> chain = new List<Entity>();
                Composite current = scene;
                for (int i = 0; i < instancePath.Count; i++)
                {
                    Entity step = current?.GetEntityByID(new ShortGuid(instancePath[i]));
                    if (step == null)
                        throw new McpError("Step " + i + " of the instance path (" + McpScript.Id(new ShortGuid(instancePath[i])) + ") is not in " + (current?.name ?? "an instance") + ": the path starts in " + scene.name + ", the composite the viewport shows.");
                    chain.Add(step);
                    current = McpScript.InstancedComposite(commands, step);
                }
                Entity leaf = chain[chain.Count - 1];
                if (leaf is FunctionEntity function && function.function.IsFunctionType && function.function.AsFunctionType == FunctionType.CameraResource)
                    ownFov = leaf.GetParameter("fov")?.content is cFloat value ? value.value : 45f;
                return PoseAlong(chain);
            });
            float send = !float.IsNaN(fov) ? fov : (!float.IsNaN(ownFov) && ownFov >= 1f && ownFov <= 170f ? ownFov : 0f);
            AwaitViewer(call);
            System.Numerics.Vector3 rotation = pose?.rotation ?? System.Numerics.Vector3.Zero;
            PlaceAndReport(call, pose?.position ?? System.Numerics.Vector3.Zero, ForwardOf(rotation), UpOf(rotation), send, new List<uint>(instancePath), follow, new JObject(), out ViewportCamera camera);
            return camera;
        }

        /// <summary>
        /// What the viewport shows at a point of its picture (fractions 0-1, 0,0 top left) - pick_in_viewport's result for it,
        /// material textures left out. Waits for the viewport to be ready. Not on the UI thread.
        /// </summary>
        internal static JObject PickPoint(McpCall call, double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y) || x < 0 || x > 1 || y < 0 || y > 1)
                throw new McpError("A viewport point is two fractions 0-1 (across, down).");
            McpEditor.UI(() => { RequireViewportOn(); });
            AwaitViewer(call);
            Packet answer = AskViewer(call, new List<float[]>() { new[] { (float)x, (float)y } }, null, CameraTimeout);
            return McpEditor.UI(() =>
            {
                ViewportCamera camera = ReadCamera(answer);
                JObject pick = answer.viewport_pick_results != null && answer.viewport_pick_results.Count != 0
                    ? DescribePick(answer.viewport_pick_results[0], camera, null, null)
                    : new JObject() { ["hit"] = false };
                pick["space"] = camera.Space;
                return pick;
            });
        }
        #endregion

        #region viewport_action
        private static object RunAction(McpCall call)
        {
            string action = (call.Str("action", required: true) ?? "").Trim().ToLowerInvariant();
            if (!Actions.Contains(action))
                throw new McpError("'action' must be one of: " + string.Join(", ", Actions) + ".");
            bool usesTarget = action == "focus" || action == "snap_to_floor" || action == "hide";
            if (!usesTarget && (call.Has("composite") || call.Has("entities") || call.Has("path")))
                throw new McpError(action + " takes no entities.");
            if (action != "enable" && action != "restart" && (call.Has("save_first") || call.Has("wait")))
                throw new McpError("'save_first' and 'wait' are for enable and restart.");

            switch (action)
            {
                case "focus":
                case "hide":
                    return FocusOrHide(call, action == "hide");
                case "snap_to_floor":
                    return SnapToFloor(call);
                case "unhide_all":
                    AwaitViewer(call);
                    McpEditor.UI(() => Send.SendViewportAction(ViewportAction.UnhideAll));
                    return new JObject() { ["unhidden"] = true };
                case "deselect_all":
                    McpEditor.UI(() =>
                    {
                        McpEditor.RequireLevel(forEditing: false);
                        CompositeDisplay display = McpEditor.Editor.CompositeDisplay;
                        if (display != null && !display.IsDisposed && display.Populated)
                            display.ClearEntitySelection();
                    });
                    return new JObject() { ["deselected"] = true };
                case "enable":
                    return Enable(call);
                case "disable":
                    return McpEditor.UI(() =>
                    {
                        CommandsEditor editor = McpEditor.Editor;
                        if (editor.IsLevelLoadInProgress)
                            throw new McpError("A level is loading. Try again when it has opened.");
                        RequireNotSaving();
                        if (!Singleton.ViewportEnabled)
                            return new JObject() { ["enabled"] = false, ["note"] = "The viewport was already off." };
                        editor.SetViewportEnabled(false, false);
                        return new JObject() { ["enabled"] = Singleton.ViewportEnabled };
                    });
                case "restart":
                    return Restart(call);
            }
            throw new McpError("Unknown action '" + action + "'.");
        }

        private static object FocusOrHide(McpCall call, bool hide)
        {
            Target target = null;
            int populateEvents = -1;
            string label = null;
            McpEditor.UI(() =>
            {
                McpEditor.RequireLevel(forEditing: false);
                RequireNotSaving();
                RequireViewportOn();
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                target = ResolveTarget(call, commands, "entities");
                if (target == null || target.Entities.Count == 0)
                    throw new McpError("Say what to " + (hide ? "hide" : "focus on") + ": 'composite' with 'entities', or 'path'.");
                label = Describe(commands, target);
                int before = ViewerPopulateSync.PopulateEvents;
                if (OpenAndSelect(target, commands))
                    populateEvents = before;
                Singleton.Editor.CompositeDisplay?.ShowLevelViewerPanel(false);
            });
            AwaitSelection(call, target);
            AwaitViewer(call, populateEvents);
            McpEditor.UI(() => Send.SendViewportAction(hide ? ViewportAction.Hide : ViewportAction.FocusOnSelection));
            Thread.Sleep(hide ? 300 : 1200);
            JObject result = new JObject() { [hide ? "hidden" : "focused"] = label, ["selected"] = true };
            if (hide)
                call.Note("Hidden in the viewport only (not level data) until unhide_all, or until the viewport rebuilds the scene.");
            else if (LiveLinkCameraSync.CameraFollowsGame)
            {
                //The viewer frames nothing while its camera is the game's
                result.Remove("focused");
                result["selected_not_framed"] = label;
                call.Note(FollowingGameNote);
            }
            return result;
        }

        private static object SnapToFloor(McpCall call)
        {
            Target target = null;
            int populateEvents = -1;
            Dictionary<Entity, cTransform> before = null;
            McpEditor.UI(() =>
            {
                McpEditor.RequireLevel(forEditing: true);
                McpEditor.RequireUndoIdle();
                RequireViewportOn();
                if (AnimationModeSession.Current != null)
                    throw new McpError("A CAGEAnimation editor is in Animation Mode, where a move becomes a keyframe: leave it first.");
                Commands commands = McpEditor.RequireCommands();
                target = ResolveTarget(call, commands, "entities");
                if (target == null || target.Entities.Count == 0)
                    throw new McpError("Say what to snap: 'composite' with 'entities', or 'path'.");
                //Snapping is refused by the viewer in creation mode
                if (Singleton.Editor.ExitViewerCreateMode())
                    call.Note("Left the viewport's entity creation mode.");
                before = Positions(target.Entities);
                int events = ViewerPopulateSync.PopulateEvents;
                if (OpenAndSelect(target, commands))
                    populateEvents = events;
                Singleton.Editor.CompositeDisplay?.ShowLevelViewerPanel(false);
            });
            AwaitSelection(call, target);
            AwaitViewer(call, populateEvents);

            //The answer is a gesture of parameter packets, recorded on the undo history as one step
            StrongBox<int> steps = new StrongBox<int>(0);
            System.Action counted = () => steps.Value++;
            string undoLabel = null;
            int recordsBefore = 0;
            McpEditor.UI(() =>
            {
                McpEditor.RequireUndoIdle();
                UndoStack.Current.Changed += counted;
                recordsBefore = UndoStack.Current.RecordCount;
            });
            try
            {
                McpEditor.UI(() => Send.SendViewportAction(ViewportAction.SnapToFloor));
                McpEditor.WaitFor(call, () => steps.Value != 0 || Moved(before).Count != 0, TimeSpan.FromSeconds(10), "Waiting for the viewport to snap");
                //The rest of the gesture's packets: wait until nothing has changed for a moment
                int seen = -1;
                for (int i = 0; i < 20; i++)
                {
                    Thread.Sleep(250);
                    int now = McpEditor.UI(() => steps.Value + Moved(before).Count * 1000);
                    if (now == seen) break;
                    seen = now;
                }
                undoLabel = McpEditor.UI(() => ClaimLatestStep(recordsBefore));
            }
            finally
            {
                McpEditor.UI(() => UndoStack.Current.Changed -= counted);
            }

            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                List<Entity> moved = Moved(before);
                if (moved.Count == 0 && steps.Value == 0)
                    throw new McpError("Nothing moved: the viewport found no floor below " + Describe(commands, target) + ", or cannot move it (only entities it draws with a position can be snapped).");
                JArray entities = new JArray();
                foreach (Entity entity in target.Entities)
                {
                    cTransform now = entity.GetParameter("position")?.content as cTransform;
                    JObject row = McpScript.Brief(commands, target.Owner, entity);
                    row["moved"] = moved.Contains(entity);
                    if (before[entity] != null) row["position_before"] = McpValues.Vector(before[entity].position);
                    if (now != null) row["position"] = McpValues.Vector(now.position);
                    entities.Add(row);
                }
                if (moved.Count == 0)
                    call.Note("The listed positions did not change: the viewport may have moved an override (an alias) instead. get_editor_state shows the undo step.");
                return new JObject()
                {
                    ["composite"] = target.Owner.name,
                    ["snapped"] = moved.Count,
                    ["entities"] = entities,
                    ["undo"] = undoLabel,
                };
            });
        }

        /// <summary>Each entity's position as it is now (a copy), or null where it has none.</summary>
        private static Dictionary<Entity, cTransform> Positions(IEnumerable<Entity> entities)
        {
            Dictionary<Entity, cTransform> positions = new Dictionary<Entity, cTransform>();
            foreach (Entity entity in entities)
            {
                cTransform transform = entity.GetParameter("position")?.content as cTransform;
                positions[entity] = transform == null ? null : new cTransform(transform.position, transform.rotation);
            }
            return positions;
        }

        private static List<Entity> Moved(Dictionary<Entity, cTransform> before)
        {
            List<Entity> moved = new List<Entity>();
            foreach (KeyValuePair<Entity, cTransform> entry in before)
            {
                cTransform now = entry.Key.GetParameter("position")?.content as cTransform;
                if (now == null) continue;
                if (entry.Value == null || (now.position - entry.Value.position).Length() > 0.00001f || (now.rotation - entry.Value.rotation).Length() > 0.00001f)
                    moved.Add(entry.Key);
            }
            return moved;
        }

        /// <summary>Save the level (a plain save) so the viewport, which reads from disk, sees it.</summary>
        private static void SaveFirst(McpCall call)
        {
            string level = McpEditor.UI(() => McpEditor.RequireLevel().Level.Name);
            Exception failure = null;
            using (McpEditorTools.Heartbeat(call, "Saving " + level))
            {
                McpEditor.UI(() =>
                {
                    try
                    {
                        Singleton.Editor.SaveLevel(false, successMsg: false, allowLaunchGame: false);
                    }
                    catch (Exception e)
                    {
                        failure = e;
                        foreach (Form progress in Application.OpenForms.OfType<ProgressUI>().ToList())
                            try { progress.Close(); } catch { }
                        Singleton.Editor.EnableButtons(true, "");
                        Cursor.Current = Cursors.Default;
                    }
                });
            }
            if (failure != null)
                throw new McpError("Saving " + level + " failed: " + failure.Message + ". Nothing else was done.");
            if (McpEditor.UI(() => DirtyTracker.IsDirty))
                throw new McpError("OpenCAGE did not save " + level + " (a backup may be running). Nothing else was done.");
            call.Note("Saved " + level + " first (a plain save, as save_level does).");
        }

        /// <summary>Whether the open level has changes the viewport, reading it from disk, would not see. UI thread.</summary>
        private static bool LevelDirty()
        {
            LevelContent content = McpEditor.Editor.CompositeBrowser?.Content;
            return content?.Level != null && content.IsLevelDataLoaded && DirtyTracker.IsDirty;
        }

        private static JObject ViewerStatus()
        {
            LevelViewerPanel panel = McpEditor.Editor.LevelViewerPanel;
            return new JObject()
            {
                ["enabled"] = Singleton.ViewportEnabled,
                ["running"] = panel != null && !panel.IsDisposed && panel.IsRunning,
                ["connected"] = Send.Connected,
                ["ready"] = ViewerSettled(),
            };
        }

        private static object Enable(McpCall call)
        {
            bool levelOpen = McpEditor.UI(() =>
            {
                CommandsEditor editor = McpEditor.Editor;
                if (editor.IsLevelLoadInProgress)
                    throw new McpError("A level is loading. Try again when it has opened.");
                RequireNotSaving();
                LevelContent content = editor.CompositeBrowser?.Content;
                return content?.Level != null && content.IsLevelDataLoaded;
            });
            if (McpEditor.UI(() => Singleton.ViewportEnabled))
            {
                JObject already = McpEditor.UI(() => ViewerStatus());
                already["note"] = "The viewport was already on." + (!(bool)already["running"] && levelOpen ? " It is not running: viewport_action {action: 'restart'} starts it." : "");
                return already;
            }
            if (!File.Exists(Singleton.ViewportExecutablePath))
                throw new McpError("The viewport's program is missing (" + Singleton.ViewportExecutablePath + "): reinstall OpenCAGE.");

            //The viewer reads the level from disk: with unsaved changes it opens at the next level load instead, as answering No does
            bool openNow = levelOpen;
            if (levelOpen && McpEditor.UI(() => LevelDirty()))
            {
                if (call.Bool("save_first"))
                    SaveFirst(call);
                else
                {
                    openNow = false;
                    call.Note("The level has unsaved changes the viewport could not show, so it opens when a level is next loaded. Pass save_first: true to save and open it now.");
                }
            }
            int populateEvents = McpEditor.UI(() => ViewerPopulateSync.PopulateEvents);
            McpEditor.UI(() => McpEditor.Editor.SetViewportEnabled(true, openNow));
            if (openNow && call.Bool("wait", true))
                AwaitViewer(call, populateEvents, relaunched: true);
            return McpEditor.UI(() => ViewerStatus());
        }

        private static object Restart(McpCall call)
        {
            McpEditor.UI(() =>
            {
                McpEditor.RequireLevel(forEditing: true);
                RequireViewportOn();
            });
            if (McpEditor.UI(() => LevelDirty()))
            {
                if (!call.Bool("save_first"))
                    throw new McpError("The viewport reads the level from disk, so it would not show the unsaved changes. Pass save_first: true to save first, or save_level.");
                SaveFirst(call);
            }
            int populateEvents = McpEditor.UI(() => ViewerPopulateSync.PopulateEvents);
            McpEditor.UI(() => McpEditor.Editor.RestartLevelViewer());
            if (call.Bool("wait", true))
                AwaitViewer(call, populateEvents, relaunched: true);
            return McpEditor.UI(() => ViewerStatus());
        }
        #endregion

        #region place_in_viewport
        private static object Place(McpCall call)
        {
            double x = call.Num("x", double.NaN), y = call.Num("y", double.NaN);
            if (!call.Has("x") || !call.Has("y"))
                throw new McpError("'x' and 'y' are required: fractions (0-1) of the viewport, from a capture_viewport picture.");
            if (double.IsNaN(x) || double.IsNaN(y) || x < 0 || x > 1 || y < 0 || y > 1)
                throw new McpError("'x' and 'y' are fractions of the viewport, from 0 to 1 (pixel / picture width, pixel / picture height).");
            if (call.Has("function") == call.Has("instance_of"))
                throw new McpError("Say what to place: 'function' (a function type) or 'instance_of' (a composite), one of them.");

            Composite into = null;
            FunctionType function = FunctionType.ModelReference;
            Composite instanceOf = null;
            int populateEvents = -1;
            McpEditor.UI(() =>
            {
                McpEditor.RequireLevel(forEditing: true);
                McpEditor.RequireUndoIdle();
                RequireViewportOn();
                Commands commands = McpEditor.RequireCommands();
                CompositeDisplay display = McpEditor.Editor.CompositeDisplay;
                if (call.Has("composite"))
                    into = McpScript.FindComposite(commands, call.Str("composite"));
                else if (display != null && !display.IsDisposed && display.Populated && display.Composite != null)
                    into = display.Composite;
                else
                    throw new McpError("No composite is on screen: say which one with 'composite' ('root' for the level).");

                if (call.Has("function"))
                {
                    string name = call.Str("function").Trim();
                    string match = Enum.GetNames(typeof(FunctionType)).FirstOrDefault(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                        throw new McpError("There is no function type '" + name + "' (list_function_types shows them).");
                    function = (FunctionType)Enum.Parse(typeof(FunctionType), match);
                    if (!ViewerFunctionDrop.HasPosition(function))
                        throw new McpError(function + " has no position, so it cannot be placed in the viewport: create_entities adds it.");
                    //What the editor would refuse with a message box
                    if ((function == FunctionType.PhysicsSystem || function == FunctionType.EnvironmentModelReference) && into.functions.Any(o => o.function == function))
                        throw new McpError(into.name + " already has a " + function + ", and a composite can only have one.");
                }
                else
                {
                    instanceOf = McpScript.FindComposite(commands, call.Str("instance_of"));
                    if (commands.Utils.WouldCreateCompositeInstanceCycle(into, instanceOf))
                        throw new McpError("An instance of " + instanceOf.name + " in " + into.name + " would contain itself (directly or through nested instances).");
                }

                int before = ViewerPopulateSync.PopulateEvents;
                if (OpenTopLevel(into))
                    populateEvents = before;
                Singleton.Editor.CompositeDisplay?.ShowLevelViewerPanel(false);
            });
            AwaitViewer(call, populateEvents);

            //The viewer raycasts the point and answers with ENTITY_CREATE_REQUEST, which the editor turns into the entity
            StrongBox<Entity> added = new StrongBox<Entity>(null);
            Composite target = into;
            FunctionType wantedFunction = function;
            Composite wantedComposite = instanceOf;
            Action<Entity> onAdded = entity =>
            {
                if (added.Value != null || !(entity is FunctionEntity made) || target.GetEntityByID(entity.shortGUID) != entity)
                    return;
                bool matches = wantedComposite != null
                    ? made.function == wantedComposite.shortGUID
                    : made.function.IsFunctionType && made.function.AsFunctionType == wantedFunction;
                if (matches)
                    added.Value = entity;
            };
            int recordsBefore = 0;
            McpEditor.UI(() =>
            {
                McpEditor.RequireUndoIdle();
                CompositeDisplay display = McpEditor.Editor.CompositeDisplay;
                if (display == null || display.Composite != target)
                    throw new McpError("The composite on screen changed while waiting for the viewport. Try again.");
                Singleton.OnEntityAdded += onAdded;
                recordsBefore = UndoStack.Current.RecordCount;
            });
            try
            {
                McpEditor.UI(() =>
                {
                    if (wantedComposite != null)
                        Send.SendCompositeDropPacket(wantedComposite, (float)x, (float)y);
                    else
                        Send.SendFunctionDropPacket(wantedFunction, (float)x, (float)y);
                });
                if (!McpEditor.WaitFor(call, () => added.Value != null, TimeSpan.FromSeconds(15), "Waiting for the viewport to place it"))
                    throw new McpError("Nothing was placed: the point (" + x.ToString("0.###") + ", " + y.ToString("0.###") + ") may not land on any geometry the viewport shows. capture_viewport, then pick a point on a visible surface. (Should it still appear later, undo removes it.)");
            }
            finally
            {
                McpEditor.UI(() => Singleton.OnEntityAdded -= onAdded);
            }

            McpEditor.UI(() => { }); //let the selection the create queued run
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                Entity entity = added.Value;
                JObject result = McpScript.Brief(commands, target, entity);
                result["composite"] = target.name;
                cTransform position = entity.GetParameter("position")?.content as cTransform;
                if (position != null)
                {
                    result["position"] = McpValues.Vector(position.position);
                    result["rotation"] = McpValues.Vector(position.rotation);
                }
                result["undo"] = ClaimLatestStep(recordsBefore);
                return result;
            });
        }

        /// <summary>
        /// The undo step the viewport made for a tool - a drop's create, a snap's gesture - is recorded as the viewer's packet
        /// arrives, outside the tool's own steps on the UI thread, so it is stamped as the user's and undo would refuse it. It is
        /// labelled 'AI: ...' here (still the one step) so undo takes it as these tools' - the latest step, when one was recorded
        /// since <paramref name="recordsBefore"/> (taken just before the request, so only an edit the user made in the second it
        /// took could be taken for it). Returns the latest step's label. UI thread.
        /// </summary>
        private static string ClaimLatestStep(int recordsBefore)
        {
            UndoStack stack = UndoStack.Current;
            if (stack.RecordCount != recordsBefore)
            {
                List<UndoStack.HistoryEntry> latest = stack.History(false, 1);
                string label = latest.Count != 0 ? latest[0].Label ?? "" : null;
                if (label != null && latest[0].Origin == null && !label.StartsWith("AI:", StringComparison.OrdinalIgnoreCase))
                    stack.Collapse(1, "AI: " + label);
            }
            return stack.UndoLabel;
        }
        #endregion

        #region get_composite_preview
        private static object Preview(McpCall call)
        {
            Composite composite = null;
            bool stale = false;
            byte[] png = null;
            string source = null;
            bool drawsNothing = false;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                stale = CompositePreviewManager.IsDirty(composite.shortGUID);
                CompositePreviewTable.Preview own = CompositePreviewManager.GetUserPreview(composite.shortGUID);
                drawsNothing = own != null && (own.png_gzip == null || own.png_gzip.Length == 0);
                if (CompositePreviewManager.TryGetPreviewPng(composite.shortGUID, out png))
                    source = own != null ? "taken by the viewport at a save of this level" : "shipped with the game";
            });

            if (call.Bool("fresh"))
                return FreshPreview(call, composite);

            if (png == null)
            {
                if (drawsNothing)
                    throw new McpError(composite.name + " draws nothing (its last preview came out empty)" + (stale ? ", though it has changed since: fresh: true takes a new one." : "."));
                throw new McpError("There is no preview of " + composite.name + " yet: the viewport takes them when the level is saved. fresh: true takes one now.");
            }
            return new McpImage()
            {
                Data = png,
                MimeType = "image/png",
                Caption = "Preview of " + composite.name + " (" + source + ")" + (stale ? "; the composite has changed since, so it may be out of date (fresh: true takes a new one)." : "."),
            };
        }

        private static object FreshPreview(McpCall call, Composite composite)
        {
            McpEditor.UI(() =>
            {
                McpEditor.RequireLevel(forEditing: false);
                RequireNotSaving();
            });
            AwaitViewer(call);

            string folder = Path.Combine(Path.GetTempPath(), "OpenCAGE", "mcp_previews", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            Action<Packet> onAnswer = null;
            try
            {
                uint id = composite.shortGUID.AsUInt32;
                string pngPath = Path.Combine(folder, id + ".png");
                string emptyPath = Path.ChangeExtension(pngPath, ".empty");
                //Our own request id: the editor's preview table only takes the answer to its own save-time request, so this one is left alone
                Random random = new Random();
                uint request = 0;
                void Ask()
                {
                    request = (uint)random.Next(1, int.MaxValue) | 0x80000000u;
                    McpEditor.UI(() =>
                    {
                        if (!McpEditor.RequireCommands(forEditing: false).Entries.Contains(composite))
                            throw new McpError("That composite is no longer in the level.");
                        Send.SendPreviewCaptureRequest(new List<uint>() { id }, folder, request);
                    });
                }

                /* The viewer answers every request, a refusal included ("a load is in flight" - the level or a populate still
                   coming in) - with no picture, straight away. Only the file used to be watched for, so a refused request
                   waited out the whole three minutes. A refusal is asked again once the viewer has settled. */
                Packet answer = null;
                onAnswer = packet =>
                {
                    if (packet.preview_request_id == Volatile.Read(ref request))
                        Volatile.Write(ref answer, packet);
                };
                CompositePreviewManager.CaptureAnswered += onAnswer;

                byte[] png = null;
                bool empty = false;
                DateTime until = DateTime.UtcNow + TimeSpan.FromMinutes(3);
                int reported = -1;
                int asked = 0;
                bool refused = false;
                DateTime started = DateTime.UtcNow;
                Ask();
                asked++;
                while (DateTime.UtcNow < until)
                {
                    call.ThrowIfCancelled();
                    if (File.Exists(emptyPath)) { empty = true; break; }
                    Packet answered = Volatile.Read(ref answer);
                    if (answered != null && !File.Exists(pngPath)
                        && (answered.preview_results == null || answered.preview_results.All(r => r == null || (CompositePreviewStatus)r.status == CompositePreviewStatus.Failed)))
                    {
                        if (asked >= 4)
                        {
                            refused = true;
                            break;
                        }
                        Volatile.Write(ref answer, null);
                        /* Longer each time: a level still being read shows OpenCAGE nothing busy until its populate starts,
                           so waiting for ViewerBusy alone used every ask up in seconds. (A capture that ran and failed
                           answers the same way; it is asked again too, at most three more times.) */
                        DateTime settleBy = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                        DateTime notBefore = DateTime.UtcNow + TimeSpan.FromSeconds(4 * asked);
                        while ((ViewerBusy.Likely || DateTime.UtcNow < notBefore) && DateTime.UtcNow < settleBy && DateTime.UtcNow < until)
                        {
                            call.ThrowIfCancelled();
                            Thread.Sleep(250);
                        }
                        Ask();
                        asked++;
                        continue;
                    }
                    if (File.Exists(pngPath))
                    {
                        try
                        {
                            using (FileStream stream = new FileStream(pngPath, FileMode.Open, FileAccess.Read, FileShare.None))
                            using (MemoryStream copy = new MemoryStream())
                            {
                                stream.CopyTo(copy);
                                if (copy.Length > 0) png = copy.ToArray();
                            }
                        }
                        catch (IOException) { } //still being written
                        if (png != null) break;
                    }
                    int seconds = (int)(DateTime.UtcNow - started).TotalSeconds;
                    if (seconds / 5 != reported)
                    {
                        reported = seconds / 5;
                        call.Progress("The viewport is taking a preview of " + composite.name + " (" + seconds + " s)", seconds, 180);
                    }
                    Thread.Sleep(250);
                }

                //The viewer builds the scene it had again afterwards; the editor sends its selection back after a capture, as its own do
                Thread.Sleep(1500);
                McpEditor.UI(() => Send.SendReSyncPacket());

                if (empty)
                    throw new McpError(composite.name + " draws nothing in the viewport, so there is no picture of it.");
                if (png == null && refused)
                    throw new McpError("The viewport answered " + asked + " requests for the preview without taking it (a load still in flight, or the capture failed: get_viewport_state with log_lines shows its log; lines tagged [Preview] say why).");
                if (png == null)
                    throw new McpError("The viewport did not take the preview (get_viewport_state with log_lines shows its log; lines tagged [Preview] say why).");
                return new McpImage()
                {
                    Data = png,
                    MimeType = "image/png",
                    Caption = "Preview of " + composite.name + ", taken now by the viewport (not stored; the level's own previews are retaken at save).",
                };
            }
            finally
            {
                if (onAnswer != null)
                    CompositePreviewManager.CaptureAnswered -= onAnswer;
                try { Directory.Delete(folder, true); } catch { }
            }
        }
        #endregion
    }

    /// <summary>A tool result that is a picture (with a line of text).</summary>
    internal sealed class McpImage
    {
        public byte[] Data;
        public string MimeType;
        public string Caption;
    }
}
