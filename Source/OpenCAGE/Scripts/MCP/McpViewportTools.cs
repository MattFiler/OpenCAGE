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

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Seeing and steering the 3D viewport: a picture of it, what it is set to show, placing and snapping
    /// entities with its raycasts, turning it on and off, and the composite previews it takes.
    /// </summary>
    /// <remarks>
    /// The viewport is a separate process (the Godot level viewer) spoken to by packets. Everything here goes
    /// through the packets and settings the editor's own toolbar, Options menu and context menu use, so the
    /// editor's controls stay in step with what a tool changed. Its view settings are editor settings,
    /// remembered across sessions; the only level data changed here is by place_in_viewport and snap_to_floor,
    /// which the editor records on its undo history exactly as a drop or Shift+End would.
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

        private static readonly string[] Actions = { "focus", "snap_to_floor", "hide", "unhide_all", "deselect_all", "enable", "disable", "restart" };

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "capture_viewport",
                Title = "Look at the viewport",
                Description = "A picture of OpenCAGE's 3D view (no toolbar), taken once the viewport has finished loading. 'composite' opens that composite first; 'focus' or 'path' select entities and move the camera to them. x/y as fractions of this picture are what place_in_viewport takes. Refused while the level is saving. Changes no level data.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Open this composite first (path or id; 'root' for the whole level)."),
                    McpSchema.Strings("focus", "Entities in that composite to frame (ids or names). Selected to move the camera, then deselected so the highlight does not tint the picture."),
                    McpSchema.Strings("path", "Instead of focus: steps from 'composite' down through instances to one nested entity to frame, e.g. ['CorridorLight_A', 'Light']."),
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
                Description = "The 3D viewport: whether it is on, running, connected and finished loading (ready), what it is showing, its view settings (overlays, render filters, highlight and gizmo modes, snaps - what set_viewport_view changes), the level's states for the navmesh/cover overlay, and optionally the tail of its log.",
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
                    McpSchema.Map("render_filters", "Entity previews to show/hide: {FunctionType name: true|false}, e.g. {\"TriggerBox\": true}; key 'all' sets every one first."),
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
                    McpSchema.String("create_mode", "Put the viewport in creation mode, where the user's clicks create this type (e.g. 'TriggerBox'; turns the gizmo off), or 'none'."),
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
                Name = "viewport_action",
                Title = "Viewport action",
                Description = "focus / snap_to_floor / hide act on entities ('composite' + 'entities', or 'path' for one nested entity), selected first: snap_to_floor drops them onto the geometry below (one undo step); hide and unhide_all only change what the viewport draws. deselect_all clears the selection. enable / disable turn the viewport on or off (a remembered setting); restart relaunches it.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "What to do.", required: true, options: Actions),
                    McpSchema.String("composite", "focus / snap_to_floor / hide: the composite the entities are in (opened in the editor; 'root' for the level)."),
                    McpSchema.Strings("entities", "Entities in 'composite' to act on (ids or names; up to 64)."),
                    McpSchema.Strings("path", "Instead of entities: steps from 'composite' down through instances to one nested entity, e.g. ['CorridorLight_A', 'Light']."),
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
                    McpSchema.String("function", "A function type to create (one with a position, e.g. 'Character', 'LightReference', 'TriggerBox')."),
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

        /// <summary>The composite and the entities (from <paramref name="entitiesArgument"/>, or 'path') a call names. UI thread.</summary>
        private static Target ResolveTarget(McpCall call, Commands commands, string entitiesArgument)
        {
            bool hasEntities = call.Has(entitiesArgument), hasPath = call.Has("path");
            if (!call.Has("composite"))
            {
                if (hasEntities || hasPath)
                    throw new McpError("Say which composite the " + (hasPath ? "path starts in" : "'" + entitiesArgument + "' are in") + " ('composite'; 'root' for the level).");
                return null;
            }
            if (hasEntities && hasPath)
                throw new McpError("Give '" + entitiesArgument + "' or 'path', not both.");

            Composite composite = McpScript.FindComposite(commands, call.Str("composite"));
            Target target = new Target() { Entry = composite, Owner = composite };
            if (hasPath)
            {
                List<string> steps = call.StrList("path");
                ShortGuid[] ids = McpScript.ResolvePath(commands, composite, steps, out Composite owner, out Entity entity);
                target.Entities.Add(entity);
                if (steps.Count > 1)
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

            int maxWidth = Math.Max(64, call.Int("max_width", 1024));
            Size size = Size.Empty;
            byte[] image = McpEditor.UI(() =>
            {
                LevelViewerPanel panel = Singleton.Editor.LevelViewerPanel;
                if (panel == null || panel.IsDisposed || !panel.Visible)
                    throw new McpError("The viewport panel is not showing.");

                //Only the viewer's own window, below the toolbar: so a point's fraction of the picture is its fraction of the viewport
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
                Caption = "The viewport" + (shown != null ? ", showing " + shown : "") + (focused ? ", framed on " + McpEditor.UI(() => Describe(McpEditor.RequireCommands(forEditing: false), target)) : "") + ". " +
                    size.Width + "x" + size.Height + " pixels; place_in_viewport takes a point as x = px/" + size.Width + ", y = py/" + size.Height + ".",
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
            return McpEditor.UI(() =>
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
                if (logLines > 0)
                {
                    string[] tail = hasPanel ? panel.GetOutputTail() : new string[0];
                    state["log"] = new JArray(tail.Skip(Math.Max(0, tail.Length - logLines)));
                }
                return state;
            });
        }

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

                //The toolbar's Create menu: the type's filter on so what is made shows, the gizmo off
                if (create.HasValue)
                {
                    if (create.Value == 0)
                        editor.ExitViewerCreateMode();
                    else
                    {
                        ViewerCreateMode.ActiveFunctionType = create.Value;
                        SettingsManager.SetInteger(Settings.LevelViewerGizmoMode, (int)LevelViewerGizmoMode.None);
                        LevelViewerPanel toolbar = editor.LevelViewerPanel;
                        if (toolbar != null && !toolbar.IsDisposed)
                        {
                            toolbar.ApplyGizmoMode(LevelViewerGizmoMode.None);
                            toolbar.ApplyCreateMode(create.Value);
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
            McpEditor.UI(() =>
            {
                McpEditor.RequireUndoIdle();
                UndoStack.Current.Changed += counted;
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
                undoLabel = McpEditor.UI(() => UndoStack.Current.UndoLabel);
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
            McpEditor.UI(() =>
            {
                McpEditor.RequireUndoIdle();
                CompositeDisplay display = McpEditor.Editor.CompositeDisplay;
                if (display == null || display.Composite != target)
                    throw new McpError("The composite on screen changed while waiting for the viewport. Try again.");
                Singleton.OnEntityAdded += onAdded;
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
                result["undo"] = UndoStack.Current.UndoLabel;
                return result;
            });
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
            try
            {
                uint id = composite.shortGUID.AsUInt32;
                string pngPath = Path.Combine(folder, id + ".png");
                string emptyPath = Path.ChangeExtension(pngPath, ".empty");
                //Our own request id: the editor's preview table only takes the answer to its own save-time request, so this one is left alone
                uint request = (uint)new Random().Next(1, int.MaxValue) | 0x80000000u;
                McpEditor.UI(() =>
                {
                    if (!McpEditor.RequireCommands(forEditing: false).Entries.Contains(composite))
                        throw new McpError("That composite is no longer in the level.");
                    Send.SendPreviewCaptureRequest(new List<uint>() { id }, folder, request);
                });

                byte[] png = null;
                bool empty = false;
                DateTime until = DateTime.UtcNow + TimeSpan.FromMinutes(3);
                int reported = -1;
                DateTime started = DateTime.UtcNow;
                while (DateTime.UtcNow < until)
                {
                    call.ThrowIfCancelled();
                    if (File.Exists(emptyPath)) { empty = true; break; }
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
