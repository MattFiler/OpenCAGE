using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Cinematics;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// build_cinematic / get_cinematic: a shot list -> a cutscene. One CameraResource per shot (a fov fed by a float lags 1-2 frames,
    /// so a cut inside one camera shows the old fov); each cut brings the next camera up and lets the old one go 0.05 s later; depth of
    /// field through VariableFloats linked into a DepthOfFieldSettings (priority 500, intensity 1 = a blend weight, the lens makes the
    /// blur), its steps held 35 ms past each cut; motion blur off (priority 1000); started after the level has finished loading (a camera
    /// activated before then is lost); character clips locked to the timeline through keyed clocks into CMD_PlayAnimation.ExternalTime.
    /// </summary>
    internal static partial class McpCinematicTools
    {
        private const string PointHelp = "a point [x,y,z], or {entity: path} (an entity's resting position; path from the cinematic's composite)";

        private static IEnumerable<McpTool> ShotTools()
        {
            JObject point = new JObject() { ["description"] = PointHelp };
            JObject shot = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["name"] = new JObject() { ["type"] = "string", ["description"] = "A label for the shot (the report uses it)." },
                    ["duration"] = new JObject() { ["type"] = "number", ["description"] = "Seconds (at least 0.1)." },
                    ["move"] = new JObject() { ["type"] = "string", ["enum"] = new JArray("static", "dolly", "path", "orbit"), ["description"] = "static (default), dolly (position -> to; the default with 'to'), path (through position then 'path'; the default with 'path'), orbit (round 'orbit'; the default with 'orbit')." },
                    ["position"] = new JObject() { ["type"] = "array", ["description"] = "Where the camera starts [x,y,z] (not for orbit)." },
                    ["to"] = new JObject() { ["type"] = "array", ["description"] = "dolly: where it ends." },
                    ["path"] = new JObject() { ["type"] = "array", ["description"] = "path: the points it passes through after 'position' ([[x,y,z], ...])." },
                    ["bank"] = new JObject() { ["type"] = "boolean", ["description"] = "path: bank into turns like a flying camera, when it looks along the path." },
                    ["orbit"] = new JObject() { ["type"] = "object", ["description"] = "{centre: point, radius (m, 5), from (degrees, the camera's bearing from the centre: 0 = on its +Z side, 0), to (degrees, 90), height (m above the centre, 1.5)}." },
                    ["look_at"] = new JObject() { ["description"] = "What it looks at: " + PointHelp + ". Default along its move (orbit: the centre; static: 'rotation')." },
                    ["look_to"] = new JObject() { ["description"] = "Pan: the look point moves to this over the shot." },
                    ["rotation"] = new JObject() { ["type"] = "array", ["description"] = "A fixed [pitch, yaw, roll] when it looks at nothing." },
                    ["roll"] = new JObject() { ["type"] = "number", ["description"] = "Degrees of roll (Dutch angle)." },
                    ["ease"] = new JObject() { ["type"] = "string", ["enum"] = new JArray("none", "in", "out", "in_out"), ["description"] = "How moves, pans, zooms and focus pulls start and stop (default in_out)." },
                    ["shake"] = new JObject() { ["type"] = "number", ["description"] = "Handheld: degrees of slow wander (0 = locked off; 0.5-1.5 reads as handheld)." },
                    ["fov"] = new JObject() { ["type"] = "number", ["description"] = "Vertical field of view, degrees (default 45)." },
                    ["fov_to"] = new JObject() { ["type"] = "number", ["description"] = "Zoom: the fov at the shot's end (the camera's fov then reads a keyed float)." },
                    ["clip_planes"] = new JObject() { ["type"] = "string", ["enum"] = new JArray("MACRO", "CLOSE", "MID", "WIDE", "ULTRA"), ["description"] = "Near/far planes: MACRO .001/10, CLOSE .02/100, MID .05/1000 (default), WIDE .25/5000, ULTRA 1/10000 m." },
                    ["focus"] = new JObject() { ["description"] = "What is in focus: " + PointHelp + ", or a distance in metres. Default the look point." },
                    ["focus_to"] = new JObject() { ["description"] = "Rack focus: the focus moves to this point over the shot." },
                    ["fnum"] = new JObject() { ["type"] = "number", ["description"] = "Aperture (default 4; 2 is shallow, 8 deep)." },
                    ["focal_length"] = new JObject() { ["type"] = "number", ["description"] = "The depth-of-field lens in mm (default: the one that gives the shot's fov on a 24 mm-high frame; longer = more blur)." },
                    ["events"] = new JObject() { ["type"] = "array", ["description"] = "[{time: s into the shot, name}]: output pins on the timeline to link from." },
                },
                ["required"] = new JArray("duration"),
            };
            yield return new McpTool()
            {
                Name = "build_cinematic",
                Title = "Build cinematic",
                Description = "Build a multi-shot cutscene from a shot list, the way retail-style cutscenes have to be wired to play right in game: one CameraResource per shot keyed on one timeline CAGEAnimation (sparse keys), cuts that bring the next camera up and release the last 0.05 s later, " +
                    "depth of field through four VariableFloats linked into a DepthOfFieldSettings (priority 500; focus follows the focus point as the camera moves, rack focus, held 35 ms past each cut), motion blur off, the HUD hidden, started after the level has loaded (a camera activated earlier is lost), " +
                    "and character clips locked to the timeline (sync_clips: a keyed clock into each CMD_PlayAnimation's ExternalTime, so a clip never drifts from the camera). Everything is named <name>_*; <name>_Begin.trigger starts it, <name>_End.triggered fires when it is over (link gameplay from there). " +
                    "Re-running with the same name rebuilds it in place (links you made into _Begin / out of _End and out of your shot events stay). One undo step. dry_run plans it (times, keys) without building. " + McpSchema.PositionText + "; rotations [pitch, yaw, roll] degrees.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Where to build it. " + McpSchema.CompositeDefaultRoot),
                    McpSchema.String("name", "The cinematic's name, the prefix of everything it makes (default 'Cinematic')."),
                    McpSchema.Array("shots", "The shots, in order.", shot, required: true),
                    McpSchema.String("space", "What points are in: world (default) or composite (the composite's own space).", options: new[] { "world", "composite" }),
                    McpSchema.Integer("placement", "space world: which placement of the composite (when it is placed more than once)."),
                    McpSchema.String("start", "level_start (default: after the level has loaded and start_delay passed), none (link something to <name>_Begin.trigger yourself), or 'Entity.pin' (an output in the composite, e.g. 'PlayerTriggerBox_1.on_entered')."),
                    McpSchema.Number("start_delay", "level_start: seconds after the composite streams in (default 1; a camera activated while the level still loads is lost)."),
                    McpSchema.String("end", "return (default: hand the view back to the player), hold (stay on the last shot) or loop (play again).", options: new[] { "return", "hold", "loop" }),
                    McpSchema.Number("blend_out", "end return: seconds to blend back to the player (default 0: a cut)."),
                    McpSchema.Number("hold", "Seconds to hold the last shot's last frame before the end (default 0)."),
                    McpSchema.Boolean("hide_hud", "Hide the HUD while it plays (default true)."),
                    McpSchema.String("motion_blur", "off (default: a HighSpecMotionBlurSettings with no blur at priority 1000 while it plays) or keep.", options: new[] { "off", "keep" }),
                    McpSchema.Boolean("dof", "Depth of field (default true)."),
                    McpSchema.Array("cues", "Things to call at times: [{time: s into the cinematic, call: 'Entity.pin', name?: event name}].", new JObject() { ["type"] = "object" }),
                    McpSchema.Array("sync_clips", "Character clips locked to the timeline: [{entity: a CMD_PlayAnimation in the composite, at: s into the cinematic (clip time 0 there), clip_start?: s into the clip (0), rate?: 1, start?: 'Entity.pin' to call at 'at' (e.g. its TriggerBindCharacter's trigger)}].", new JObject() { ["type"] = "object" }),
                    McpSchema.Number("position_tolerance", "How far (m) camera keys may stray from the planned move (default 0.002)."),
                    McpSchema.Number("rotation_tolerance", "How far (degrees) they may stray (default 0.02)."),
                    McpSchema.Boolean("dry_run", "Plan it and report, building nothing.")),
                Destructive = true,
                Toolset = "cameras",
                Run = BuildCinematic,
            };

            yield return new McpTool()
            {
                Name = "get_cinematic",
                Title = "Read cinematic",
                Description = "Read back a cinematic build_cinematic made: its shot list (as last built), the entities it is made of, the timeline (length, cut and cue events and what they call), each camera's activate / release wiring and fov, and check_cage_animation's verdict on the timeline. Read-only.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Where it is. " + McpSchema.CompositeDefaultRoot),
                    McpSchema.String("name", "The cinematic's name (default 'Cinematic').")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "cameras",
                Run = GetCinematic,
            };
        }

        #region Reading the shot list
        private sealed class ClipSync
        {
            public Entity Command;
            public float At, ClipStart, Rate = 1f;
            public string Start;
        }

        private sealed class Cue
        {
            public float Time;
            public string Call;
            public string Name;
        }

        /// <summary>A point given as [x,y,z] or {entity: path}, in the shot list's space. UI thread.</summary>
        private static Vector3 ReadPoint(JToken token, string where, Func<JToken, string, Vector3> entityPoint)
        {
            if (token is JObject o && o["entity"] != null) return entityPoint(o["entity"], where + ".entity");
            if (token is JArray) return McpValues.ReadVector(token, where, null);
            throw McpError.Invalid("'" + where + "' is " + PointHelp + ".");
        }

        private static float ReadFloat(JToken token, string where, float min, float max)
        {
            double value = McpValues.ReadDouble(token, where);
            if (double.IsNaN(value) || value < min || value > max) throw McpError.Invalid("'" + where + "' is " + min + " to " + max + ".");
            return (float)value;
        }

        private static readonly string[] ShotFields = { "name", "duration", "move", "position", "to", "path", "bank", "orbit", "look_at", "look_to", "rotation", "roll", "ease", "shake", "fov", "fov_to", "clip_planes", "focus", "focus_to", "fnum", "focal_length", "events" };
        private static readonly string[] Presets = { "MACRO", "CLOSE", "MID", "WIDE", "ULTRA" };

        private static List<ShotSpec> ReadShots(JArray list, Func<JToken, string, Vector3> entityPoint)
        {
            if (list == null || list.Count == 0) throw McpError.Invalid("'shots' needs at least one shot.");
            if (list.Count > 60) throw McpError.Invalid("At most 60 shots.");
            List<ShotSpec> shots = new List<ShotSpec>();
            for (int i = 0; i < list.Count; i++)
            {
                string w = "shots[" + i + "]";
                if (!(list[i] is JObject s)) throw McpError.Invalid("'" + w + "' must be an object.");
                foreach (JProperty p in s.Properties())
                    if (!ShotFields.Contains(p.Name)) throw McpError.Invalid("'" + w + "' takes " + string.Join(", ", ShotFields) + " (not '" + p.Name + "').");
                ShotSpec shot = new ShotSpec() { Name = (string)s["name"] ?? ("Shot " + (i + 1)) };
                if (s["duration"] == null) throw McpError.Invalid("'" + w + ".duration' (seconds) is missing.");
                shot.Duration = ReadFloat(s["duration"], w + ".duration", 0.1f, 3600f);
                string move = ((string)s["move"] ?? (s["orbit"] != null ? "orbit" : s["path"] != null ? "path" : s["to"] != null ? "dolly" : "static")).Trim().ToLowerInvariant();
                if (move != "static" && move != "dolly" && move != "path" && move != "orbit") throw McpError.Invalid("'" + w + ".move' is static, dolly, path or orbit.");
                shot.Move = move;
                if (move == "orbit")
                {
                    if (!(s["orbit"] is JObject orbit)) throw McpError.Invalid("'" + w + ".orbit' {centre, radius, from, to, height} is needed for an orbit.");
                    if (orbit["centre"] == null) throw McpError.Invalid("'" + w + ".orbit.centre' is missing.");
                    shot.OrbitCentre = ReadPoint(orbit["centre"], w + ".orbit.centre", entityPoint);
                    shot.OrbitRadius = orbit["radius"] != null ? ReadFloat(orbit["radius"], w + ".orbit.radius", 0.05f, 10000f) : 5f;
                    shot.OrbitFrom = orbit["from"] != null ? ReadFloat(orbit["from"], w + ".orbit.from", -36000f, 36000f) : 0f;
                    shot.OrbitTo = orbit["to"] != null ? ReadFloat(orbit["to"], w + ".orbit.to", -36000f, 36000f) : 90f;
                    shot.OrbitHeight = orbit["height"] != null ? ReadFloat(orbit["height"], w + ".orbit.height", -1000f, 1000f) : 1.5f;
                    shot.Position = shot.OrbitCentre;
                }
                else
                {
                    if (s["position"] == null) throw McpError.Invalid("'" + w + ".position' (where the camera is) is missing.");
                    shot.Position = McpValues.ReadVector(s["position"], w + ".position", null);
                }
                if (move == "dolly")
                {
                    if (s["to"] == null) throw McpError.Invalid("'" + w + ".to' is needed for a dolly.");
                    shot.To = McpValues.ReadVector(s["to"], w + ".to", null);
                }
                if (move == "path")
                {
                    if (!(s["path"] is JArray path) || path.Count == 0) throw McpError.Invalid("'" + w + ".path' is the points the camera passes through after its position.");
                    shot.Path = new List<Vector3>() { shot.Position };
                    for (int k = 0; k < path.Count; k++) shot.Path.Add(McpValues.ReadVector(path[k], w + ".path[" + k + "]", null));
                    shot.Bank = s["bank"] != null && McpValues.ReadBool(s["bank"], w + ".bank");
                }
                if (s["look_at"] != null) shot.LookAt = ReadPoint(s["look_at"], w + ".look_at", entityPoint);
                if (s["look_to"] != null)
                {
                    if (shot.LookAt == null) throw McpError.Invalid("'" + w + ".look_to' pans from 'look_at': give that too.");
                    shot.LookTo = ReadPoint(s["look_to"], w + ".look_to", entityPoint);
                }
                if (s["rotation"] != null) shot.Rotation = McpValues.ReadVector(s["rotation"], w + ".rotation", null);
                if (s["roll"] != null) shot.Roll = ReadFloat(s["roll"], w + ".roll", -180f, 180f);
                if (s["ease"] != null)
                {
                    string ease = ((string)s["ease"]).Trim().ToLowerInvariant();
                    if (ease != "none" && ease != "in" && ease != "out" && ease != "in_out") throw McpError.Invalid("'" + w + ".ease' is none, in, out or in_out.");
                    shot.Ease = ease;
                }
                if (s["shake"] != null) shot.Shake = ReadFloat(s["shake"], w + ".shake", 0f, 10f);
                if (s["fov"] != null) shot.Fov = ReadFloat(s["fov"], w + ".fov", 1f, 170f);
                if (s["fov_to"] != null) shot.FovTo = ReadFloat(s["fov_to"], w + ".fov_to", 1f, 170f);
                if (s["clip_planes"] != null)
                {
                    string preset = ((string)s["clip_planes"]).Trim().ToUpperInvariant();
                    if (!Presets.Contains(preset)) throw McpError.Invalid("'" + w + ".clip_planes' is " + string.Join(", ", Presets) + ".");
                    shot.ClipPlanes = preset;
                }
                if (s["focus"] != null)
                {
                    if (s["focus"].Type == JTokenType.Float || s["focus"].Type == JTokenType.Integer) shot.FocusDistance = ReadFloat(s["focus"], w + ".focus", 0.1f, 100000f);
                    else shot.FocusAt = ReadPoint(s["focus"], w + ".focus", entityPoint);
                }
                if (s["focus_to"] != null)
                {
                    if (shot.FocusAt == null && shot.LookAt == null) throw McpError.Invalid("'" + w + ".focus_to' racks from 'focus' (a point): give that too.");
                    if (shot.FocusAt == null) shot.FocusAt = shot.LookAt;
                    shot.FocusTo = ReadPoint(s["focus_to"], w + ".focus_to", entityPoint);
                }
                if (s["fnum"] != null) shot.FNum = ReadFloat(s["fnum"], w + ".fnum", 0.7f, 64f);
                if (s["focal_length"] != null) shot.FocalLength = ReadFloat(s["focal_length"], w + ".focal_length", 1f, 2000f);
                if (s["events"] is JArray events)
                    for (int k = 0; k < events.Count; k++)
                    {
                        if (!(events[k] is JObject e) || e["time"] == null || string.IsNullOrWhiteSpace((string)e["name"])) throw McpError.Invalid("'" + w + ".events[" + k + "]' is {time, name}.");
                        float time = ReadFloat(e["time"], w + ".events[" + k + "].time", 0f, shot.Duration);
                        shot.Events.Add(new ShotEvent() { Time = time, Name = ((string)e["name"]).Trim() });
                    }
                shots.Add(shot);
            }
            return shots;
        }
        #endregion

        #region Building
        /// <summary>The roles a cinematic's entities play, by name suffix.</summary>
        private static string Cam(string name, int i) => name + "_Cam" + (i + 1).ToString("00");
        private static string Clock(string name, int i) => name + "_Clock" + (i + 1).ToString("00");

        private static readonly Dictionary<string, string> DofFloats = new Dictionary<string, string>()
        {
            ["focal_plane_m"] = "_DOF_Focus",
            ["focal_length_mm"] = "_DOF_Focal",
            ["fnum"] = "_DOF_FNum",
            ["intensity"] = "_DOF_Intensity",
        };

        private static string StoreFolder(string level) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenCAGE", "Cinematics", Safe(level ?? "LEVEL"));
        private static string Safe(string text) => new string((text ?? "").Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
        internal static string StoreFile(string level, string composite, string name) => Path.Combine(StoreFolder(level), Safe(composite) + "__" + Safe(name) + ".json");

        private static object BuildCinematic(McpCall call)
        {
            string name = (call.Str("name") ?? "Cinematic").Trim();
            if (name.Length == 0 || name.Length > 40 || name.Any(c => !(char.IsLetterOrDigit(c) || c == '_'))) throw McpError.Invalid("'name' is letters, digits and '_' (up to 40).");
            string space = (call.Str("space") ?? "world").Trim().ToLowerInvariant();
            if (space != "world" && space != "composite") throw McpError.Invalid("'space' is world or composite.");
            string start = (call.Str("start") ?? "level_start").Trim();
            float startDelay = call.Has("start_delay") ? Tolerance(call, "start_delay", 1, 0, 600) : 1f;
            string end = (call.Str("end") ?? "return").Trim().ToLowerInvariant();
            if (end != "return" && end != "hold" && end != "loop") throw McpError.Invalid("'end' is return, hold or loop.");
            float blendOut = call.Has("blend_out") ? Tolerance(call, "blend_out", 0, 0, 60) : 0f;
            float hold = call.Has("hold") ? Tolerance(call, "hold", 0, 0, 3600) : 0f;
            bool hideHud = call.Bool("hide_hud", true);
            string blur = (call.Str("motion_blur") ?? "off").Trim().ToLowerInvariant();
            if (blur != "off" && blur != "keep") throw McpError.Invalid("'motion_blur' is off or keep.");
            bool dof = call.Bool("dof", true);
            float posTol = Tolerance(call, "position_tolerance", KeyFit.DefaultPositionTolerance, 1e-5, 1);
            float angTol = Tolerance(call, "rotation_tolerance", KeyFit.DefaultAngleTolerance, 1e-4, 5);
            bool dry = call.Bool("dry_run");
            int? placement = call.Has("placement") ? call.Int("placement") : (int?)null;

            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel();
                Commands commands = content.Level.Commands;
                if (!dry) McpEditor.RequireUndoIdle();
                Composite composite = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : commands.EntryPoints[0];

                //The shot list's space -> the composite's (cameras are written in the composite's space)
                cTransform frame = null;
                JObject frameDescribed = null;
                if (space == "world") frame = McpSpatialTools.FrameOf(commands, composite, placement, "'placement'", out frameDescribed);
                Func<JToken, string, Vector3> entityPoint = (token, where) =>
                {
                    McpCageAnimationTools.Target target = McpCageAnimationTools.ResolveTarget(commands, composite, token, false, where);
                    cTransform rest = InstanceTransform.TransformOf(target.Entity) ?? new cTransform(Vector3.Zero, Vector3.Zero);
                    cTransform inComposite = InstanceTransform.Compose(ChainFrame(commands, composite, target, where), rest);
                    return frame == null ? inComposite.position : InstanceTransform.PointToWorld(frame, inComposite.position);
                };
                List<ShotSpec> shots = ReadShots(call.Array("shots", required: true), entityPoint);
                CinematicPlan plan;
                try { plan = new CinematicPlan(shots); }
                catch (ArgumentException e) { throw McpError.Invalid(e.Message); }
                Func<KeyPose, KeyPose> toKeySpace = frame == null || IsIdentity(frame) ? (Func<KeyPose, KeyPose>)null : p =>
                {
                    cTransform local = InstanceTransform.ToLocal(frame, new cTransform(p.Position, p.Rotation));
                    return new KeyPose(local.position, local.rotation);
                };

                //Keys
                KeyFit.Report cameraReport = new KeyFit.Report(), lensReport = new KeyFit.Report();
                List<Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>>> cameraKeys = new List<Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>>>();
                List<List<CAGEAnimation.FloatTrack.Keyframe>> fovKeys = new List<List<CAGEAnimation.FloatTrack.Keyframe>>();
                for (int i = 0; i < shots.Count; i++)
                {
                    cameraKeys.Add(plan.CameraKeys(i, toKeySpace, posTol, angTol, cameraReport));
                    fovKeys.Add(plan.FovKeys(i, lensReport));
                }
                Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> dofKeys = dof ? plan.DofKeys(lensReport) : null;
                float length = plan.Length + hold;

                //Cues and synced clips
                List<Cue> cues = new List<Cue>();
                foreach (JToken token in call.Array("cues"))
                {
                    if (!(token is JObject c) || c["time"] == null || c["call"] == null) throw McpError.Invalid("'cues' entries are {time, call: 'Entity.pin', name?}.");
                    cues.Add(new Cue() { Time = ReadFloat(c["time"], "cues.time", 0f, length), Call = ((string)c["call"]).Trim(), Name = (string)c["name"] });
                }
                List<ClipSync> clips = new List<ClipSync>();
                foreach (JToken token in call.Array("sync_clips"))
                {
                    if (!(token is JObject c) || c["entity"] == null || c["at"] == null) throw McpError.Invalid("'sync_clips' entries are {entity, at, clip_start?, rate?, start?}.");
                    Entity command = McpScript.FindEntity(commands, composite, (string)c["entity"]);
                    if (!(command is FunctionEntity f) || !f.function.IsFunctionType || f.function.AsFunctionType != FunctionType.CMD_PlayAnimation)
                        throw McpError.Invalid("'sync_clips.entity': " + McpScript.EntityName(commands, composite, command) + " is not a CMD_PlayAnimation.");
                    clips.Add(new ClipSync()
                    {
                        Command = command,
                        At = ReadFloat(c["at"], "sync_clips.at", 0f, length),
                        ClipStart = c["clip_start"] != null ? ReadFloat(c["clip_start"], "sync_clips.clip_start", 0f, 3600f) : 0f,
                        Rate = c["rate"] != null ? ReadFloat(c["rate"], "sync_clips.rate", 0.01f, 10f) : 1f,
                        Start = (string)c["start"],
                    });
                }

                //Every event the timeline fires, and what each calls (names checked unique)
                List<(float time, string name)> events = plan.CutEvents();
                for (int i = 0; i < shots.Count; i++)
                    foreach (ShotEvent e in shots[i].Events) events.Add((plan.Starts[i] + e.Time, e.Name));
                for (int i = 0; i < cues.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(cues[i].Name)) cues[i].Name = "cue_" + (i + 1).ToString("00");
                    events.Add((cues[i].Time, cues[i].Name));
                }
                for (int i = 0; i < clips.Count; i++)
                    if (!string.IsNullOrWhiteSpace(clips[i].Start)) events.Add((clips[i].At, "clip_" + (i + 1).ToString("00")));
                foreach (IGrouping<string, (float, string)> twice in events.GroupBy(o => o.Item2, StringComparer.OrdinalIgnoreCase).Where(o => o.Count() > 1))
                    throw McpError.Invalid("Two events are named '" + twice.Key + "': event names must differ (cut_NN, release_NN, clip_NN and cue_NN are the builder's).");

                JObject result = new JObject()
                {
                    ["name"] = name,
                    ["composite"] = composite.name,
                    ["length"] = Math.Round(length, 3),
                    ["shots"] = new JArray(shots.Select((s, i) => new JObject()
                    {
                        ["shot"] = i + 1,
                        ["name"] = s.Name,
                        ["camera"] = Cam(name, i),
                        ["from"] = Math.Round(plan.Starts[i], 3),
                        ["to"] = Math.Round(plan.Starts[i] + s.Duration, 3),
                        ["move"] = s.Move,
                        ["fov"] = s.FovTo != null ? (JToken)new JArray(s.Fov, s.FovTo.Value) : s.Fov,
                        ["focus_m"] = new JArray(Math.Round(plan.FocusDistanceAt(i, 0), 2), Math.Round(plan.FocusDistanceAt(i, s.Duration), 2)),
                        ["keys"] = cameraKeys[i].Values.Sum(o => o.Count),
                    })),
                    ["keys"] = new JObject() { ["camera"] = cameraReport.Keys, ["lens"] = lensReport.Keys, ["worst_position_mm"] = Math.Round(cameraReport.WorstPosition * 1000, 3), ["worst_rotation_deg"] = Math.Round(cameraReport.WorstAngle, 4) },
                    ["events"] = new JArray(events.OrderBy(o => o.Item1).Select(o => new JObject() { ["time"] = Math.Round(o.Item1, 4), ["name"] = o.Item2 })),
                };
                if (frameDescribed != null) result["frame"] = frameDescribed;
                if (dry)
                {
                    result["dry_run"] = true;
                    return result;
                }

                //Who calls what, checked before anything changes
                (Entity entity, string pin) Output(string text, string where)
                {
                    int dot = text.LastIndexOf('.');
                    if (dot <= 0 || dot == text.Length - 1) throw McpError.Invalid("'" + where + "' is 'Entity.pin' (an entity in " + composite.name + " and one of its pins).");
                    return (McpScript.FindEntity(commands, composite, text.Substring(0, dot)), text.Substring(dot + 1).Trim());
                }
                (Entity entity, string pin) startFrom = (null, null);
                if (!string.Equals(start, "level_start", StringComparison.OrdinalIgnoreCase) && !string.Equals(start, "none", StringComparison.OrdinalIgnoreCase))
                    startFrom = Output(start, "start");
                List<(Entity entity, string pin)> cueTargets = cues.Select((c, i) => Output(c.Call, "cues[" + i + "].call")).ToList();
                List<(Entity entity, string pin)> clipTargets = clips.Select((c, i) => string.IsNullOrWhiteSpace(c.Start) ? ((Entity)null, (string)null) : Output(c.Start, "sync_clips[" + i + "].start")).ToList();

                object mark = UndoStack.Current.Mark();
                string label = "Build cinematic " + name;
                Dictionary<string, Entity> made = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
                List<string> links = new List<string>();
                List<string> removed = new List<string>();
                CAGEAnimation timeline = null;

                //1. The entities (made, or re-used by name), their settings and every link but the timeline's event pins
                McpScriptEdit.Run(label, composite, edit =>
                {
                    List<Entity> existing = composite.GetEntities().Where(o => (McpScript.EntityName(commands, composite, o) ?? "").StartsWith(name + "_", StringComparison.OrdinalIgnoreCase)).ToList();
                    Dictionary<string, Entity> byName = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
                    foreach (Entity e in existing)
                    {
                        string n = McpScript.EntityName(commands, composite, e);
                        if (!byName.ContainsKey(n)) byName[n] = e;
                    }
                    Entity Need(string suffix, FunctionType type)
                    {
                        string full = name + suffix;
                        if (byName.TryGetValue(full, out Entity old))
                        {
                            if (old is FunctionEntity f && f.function.IsFunctionType && f.function.AsFunctionType == type) { made[full] = old; return old; }
                            removed.AddRange(edit.Delete(composite, old));
                            removed.Add(full + " (was another type)");
                        }
                        Entity created = edit.AddFunction(composite, type, full);
                        made[full] = created;
                        return created;
                    }
                    void Set(Entity e, string parameter, JToken value) => edit.SetParameter(composite, e, parameter, value);

                    timeline = (CAGEAnimation)Need("_Timeline", FunctionType.CAGEAnimation);
                    Set(timeline, "start_on_reset", false);
                    Set(timeline, "enable_on_reset", true);
                    Entity begin = Need("_Begin", FunctionType.TriggerSimple);
                    Entity finish = Need("_End", FunctionType.TriggerSimple);
                    List<Entity> cameras = new List<Entity>();
                    for (int i = 0; i < shots.Count; i++)
                    {
                        Entity cam = Need(Cam(name, i).Substring(name.Length), FunctionType.CameraResource);
                        cameras.Add(cam);
                        KeyPose first = PoseOfKeys(cameraKeys[i], plan.Starts[i]);
                        Set(cam, "camera_name", Cam(name, i));
                        Set(cam, "position", new JObject() { ["position"] = McpValues.Vector(first.Position), ["rotation"] = McpValues.Vector(first.Rotation) });
                        Set(cam, "fov", shots[i].Fov);
                        Set(cam, "clipping_planes_preset", shots[i].ClipPlanes);
                        Set(cam, "enable_enter_transition", false);
                        Set(cam, "transition_duration", 0.0);
                        bool blends = end == "return" && i == shots.Count - 1 && blendOut > 0f;
                        Set(cam, "enable_exit_transition", blends);
                        if (blends) Set(cam, "exit_transition_duration", blendOut);
                        Set(cam, "is_camera_transformation_local", false);
                        Set(cam, "reset_player_camera_on_exit", false);
                        Set(cam, "enable_on_reset", false);
                        Set(cam, "attach_on_reset", true);
                        Set(cam, "converge_to_player_camera", false);
                        if (fovKeys[i] != null)
                        {
                            Entity fov = Need(Cam(name, i).Substring(name.Length) + "_Fov", FunctionType.VariableFloat);
                            Set(fov, "initial_value", shots[i].Fov);
                        }
                    }
                    if (dof)
                    {
                        Entity d = Need("_DOF", FunctionType.DepthOfFieldSettings);
                        Set(d, "focal_plane_m", plan.FocusDistanceAt(0, 0));
                        Set(d, "focal_length_mm", plan.FocalLengthAt(0, 0));
                        Set(d, "fnum", shots[0].FNum);
                        Set(d, "blend_mode", "BLEND");
                        Set(d, "intensity", 1.0);
                        Set(d, "priority", 500);
                        Set(d, "start_on_reset", false);
                        foreach (KeyValuePair<string, string> f in DofFloats)
                        {
                            Entity v = Need(f.Value, FunctionType.VariableFloat);
                            float initial = f.Key == "focal_plane_m" ? plan.FocusDistanceAt(0, 0) : f.Key == "focal_length_mm" ? plan.FocalLengthAt(0, 0) : f.Key == "fnum" ? shots[0].FNum : 1f;
                            Set(v, "initial_value", initial);
                        }
                    }
                    if (blur == "off")
                    {
                        Entity b = Need("_NoBlur", FunctionType.HighSpecMotionBlurSettings);
                        Set(b, "contribution", 0.0);
                        Set(b, "priority", 1000);
                        Set(b, "blend_mode", "BLEND");
                        Set(b, "intensity", 1.0);
                        Set(b, "camera_velocity_scalar", 0.0);
                        Set(b, "object_velocity_scalar", 0.0);
                        Set(b, "start_on_reset", false);
                    }
                    if (hideHud) Need("_Hud", FunctionType.Force_UI_Visibility);
                    if (string.Equals(start, "level_start", StringComparison.OrdinalIgnoreCase))
                    {
                        Need("_Start", FunctionType.ZoneLoaded);
                        Entity delay = Need("_StartDelay", FunctionType.LogicDelay);
                        Set(delay, "delay", startDelay);
                    }
                    if (end == "loop" && shots.Count > 1)
                    {
                        Entity release = Need("_LoopRelease", FunctionType.LogicDelay);
                        Set(release, "delay", (double)CinematicPlan.ReleaseDelay);
                    }
                    for (int i = 0; i < clips.Count; i++)
                    {
                        Entity clock = Need(Clock(name, i).Substring(name.Length), FunctionType.VariableFloat);
                        Set(clock, "initial_value", clips[i].ClipStart);
                    }

                    //What the last build made that this one does not: gone (their tracks on the timeline are rebuilt below anyway)
                    foreach (KeyValuePair<string, Entity> old in byName)
                        if (!made.ContainsKey(old.Key) && IsBuilderName(name, old.Key) && composite.GetEntityByID(old.Value.shortGUID) == old.Value)
                        {
                            removed.AddRange(edit.Delete(composite, old.Value));
                            removed.Add(old.Key);
                        }

                    //Links: the builder's own are made afresh; links you made into _Begin and out of _End or your events are kept
                    HashSet<ShortGuid> mine = new HashSet<ShortGuid>(made.Values.Select(o => o.shortGUID));
                    foreach (Entity e in made.Values)
                        foreach (EntityConnector link in e.childLinks.ToList())
                            if (mine.Contains(link.linkedEntityID))
                                edit.RemoveLinks(composite, e, McpScript.ParamName(link.thisParamID), composite.GetEntityByID(link.linkedEntityID), McpScript.ParamName(link.linkedParamID));
                    //...and the timeline's cue / clip pins out to your entities, and every link into its clocks
                    foreach (EntityConnector link in timeline.childLinks.ToList())
                    {
                        string pin = McpScript.ParamName(link.thisParamID) ?? "";
                        if (pin.StartsWith("cue_") || pin.StartsWith("clip_") || cues.Any(c => string.Equals(c.Name, pin, StringComparison.OrdinalIgnoreCase)))
                            edit.RemoveLinks(composite, timeline, pin, composite.GetEntityByID(link.linkedEntityID), McpScript.ParamName(link.linkedParamID));
                    }
                    HashSet<ShortGuid> clocks = new HashSet<ShortGuid>(Enumerable.Range(0, clips.Count).Select(i => made[Clock(name, i)].shortGUID));
                    foreach (Entity e in composite.GetEntities().ToList())
                        foreach (EntityConnector link in e.childLinks.ToList())
                            if (clocks.Contains(link.linkedEntityID) && !mine.Contains(e.shortGUID))
                                edit.RemoveLinks(composite, e, McpScript.ParamName(link.thisParamID), composite.GetEntityByID(link.linkedEntityID), McpScript.ParamName(link.linkedParamID));

                    void Link(Entity from, string pin, Entity to, string toPin)
                    {
                        edit.AddLink(composite, from, pin, to, toPin);
                        links.Add(McpScript.EntityName(commands, composite, from) + "." + pin + " -> " + McpScript.EntityName(commands, composite, to) + "." + toPin);
                    }
                    if (made.TryGetValue(name + "_Start", out Entity zone))
                    {
                        Link(zone, "on_loaded", made[name + "_StartDelay"], "trigger");
                        Link(made[name + "_StartDelay"], "on_delay_finished", begin, "trigger");
                    }
                    else if (startFrom.entity != null)
                        Link(startFrom.entity, startFrom.pin, begin, "trigger");
                    Link(begin, "triggered", timeline, "start");
                    Link(begin, "triggered", cameras[0], "activate_camera");
                    if (dof) Link(begin, "triggered", made[name + "_DOF"], "start");
                    if (blur == "off") Link(begin, "triggered", made[name + "_NoBlur"], "start");
                    if (hideHud) Link(begin, "triggered", made[name + "_Hud"], "cutscene_visibility_start");
                    Link(timeline, "animation_finished", finish, "trigger");
                    if (end == "return")
                    {
                        Link(finish, "triggered", cameras[cameras.Count - 1], "deactivate_camera");
                        if (dof) Link(finish, "triggered", made[name + "_DOF"], "stop");
                        if (blur == "off") Link(finish, "triggered", made[name + "_NoBlur"], "stop");
                        if (hideHud) Link(finish, "triggered", made[name + "_Hud"], "cutscene_visibility_finish");
                    }
                    else if (end == "loop")
                    {
                        Link(finish, "triggered", timeline, "start");
                        if (shots.Count > 1)
                        {
                            Link(finish, "triggered", cameras[0], "activate_camera");
                            Link(finish, "triggered", made[name + "_LoopRelease"], "trigger");
                            Link(made[name + "_LoopRelease"], "on_delay_finished", cameras[cameras.Count - 1], "deactivate_camera");
                        }
                    }
                    for (int i = 0; i < shots.Count; i++)
                        if (fovKeys[i] != null) Link(cameras[i], "fov", made[Cam(name, i) + "_Fov"], "reference");
                    if (dof)
                        foreach (KeyValuePair<string, string> f in DofFloats)
                            Link(made[name + "_DOF"], f.Key, made[name + f.Value], "reference");
                    for (int i = 0; i < clips.Count; i++)
                        Link(clips[i].Command, "ExternalTime", made[Clock(name, i)], "reference");
                    edit.Select.Add(timeline);
                    edit.Select.Add(begin);
                }, show: false);

                //2. The timeline: every track and event the builder owns, afresh
                McpCageAnimationTools.Working w = McpCageAnimationTools.BeginOn(commands, composite, timeline);
                w.Connections.Clear();
                w.FloatTracks.Clear();
                w.EventTracks.Clear();
                w.Mode = CAGEAnimation.InterpolationMode.Bezier;
                for (int i = 0; i < shots.Count; i++)
                {
                    McpCageAnimationTools.Target camera = McpCageAnimationTools.ResolveTarget(commands, composite, new JArray(McpScript.Id(made[Cam(name, i)].shortGUID)), false, "camera");
                    McpCageAnimationTools.WriteTransformKeys(w, camera, cameraKeys[i], true);
                    if (fovKeys[i] != null) WriteFloat(w, commands, composite, made[Cam(name, i) + "_Fov"], fovKeys[i]);
                }
                if (dof)
                    foreach (KeyValuePair<string, string> f in DofFloats)
                        if (f.Key != "intensity") WriteFloat(w, commands, composite, made[name + f.Value], dofKeys[f.Key]);
                for (int i = 0; i < clips.Count; i++)
                {
                    ClipSync c = clips[i];
                    float span = Math.Max(0.01f, length - c.At);
                    CAGEAnimation.FloatTrack.Keyframe a = CageAnimationCurves.NewKeyframe(c.At, c.ClipStart, CAGEAnimation.InterpolationMode.Bezier);
                    CAGEAnimation.FloatTrack.Keyframe b = CageAnimationCurves.NewKeyframe(c.At + span, c.ClipStart + span * c.Rate, CAGEAnimation.InterpolationMode.Bezier);
                    a.tan_in = new Vector2(span, 0f);
                    a.tan_out = new Vector2(span, c.Rate * span);
                    b.tan_in = new Vector2(span, c.Rate * span);
                    b.tan_out = new Vector2(span, c.Rate * span);
                    WriteFloat(w, commands, composite, made[Clock(name, i)], new List<CAGEAnimation.FloatTrack.Keyframe>() { a, b });
                }
                CAGEAnimation.EventTrack track = new CAGEAnimation.EventTrack() { shortGUID = ShortGuidUtils.GenerateRandom(), track_type = ANIM_TRACK_TYPE.T_STRING };
                foreach ((float time, string ev) in events.OrderBy(o => o.Item1))
                    track.keyframes.Add(new CAGEAnimation.EventTrack.Keyframe(time, ev));
                if (track.keyframes.Count != 0) w.EventTracks.Add(track);
                w.Length = length;
                w.Changed = true;
                w.Commit(label);

                //3. The event pins, now they exist
                McpScriptEdit.Run(label, composite, edit =>
                {
                    void Link(Entity from, string pin, Entity to, string toPin)
                    {
                        edit.AddLink(composite, from, pin, to, toPin);
                        links.Add(McpScript.EntityName(commands, composite, from) + "." + pin + " -> " + McpScript.EntityName(commands, composite, to) + "." + toPin);
                    }
                    for (int i = 1; i < shots.Count; i++)
                    {
                        Link(timeline, CinematicPlan.CutName(i), made[Cam(name, i)], "activate_camera");
                        Link(timeline, CinematicPlan.ReleaseName(i), made[Cam(name, i - 1)], "deactivate_camera");
                    }
                    for (int i = 0; i < cues.Count; i++) Link(timeline, cues[i].Name, cueTargets[i].entity, cueTargets[i].pin);
                    for (int i = 0; i < clips.Count; i++)
                        if (clipTargets[i].entity != null) Link(timeline, "clip_" + (i + 1).ToString("00"), clipTargets[i].entity, clipTargets[i].pin);
                }, show: false);
                int steps = UndoStack.Current.StepsSince(mark);
                if (steps > 1 && !UndoStack.Current.Collapse(steps, (FromWindow ? "" : "AI: ") + label))
                    call.Note("The cinematic is " + steps + " steps on the undo history: undo them all to take it back.");
                McpScriptEdit.Show(composite, new List<Entity>() { timeline, made[name + "_Begin"] });

                //Kept for the next build and for the shot builder window
                try
                {
                    JObject stored = new JObject((JObject)call.Args.DeepClone());
                    stored.Remove("dry_run");
                    stored["composite"] = composite.name;
                    stored["name"] = name;
                    stored["_level"] = content.Level.Name;
                    stored["_built"] = DateTime.Now.ToString("s");
                    string file = StoreFile(content.Level.Name, composite.name, name);
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllText(file, stored.ToString());
                    result["shot_list_file"] = file;
                }
                catch (Exception e) { call.Note("The shot list could not be kept for next time: " + e.Message); }

                result["entities"] = new JArray(made.Keys.OrderBy(o => o));
                result["links"] = links.Count;
                if (removed.Count != 0) result["removed"] = new JArray(removed);
                JObject check = McpCageAnimationTools.Check(commands, composite, timeline);
                result["will_it_play"] = new JObject() { ["verdict"] = check["verdict"], ["problems"] = check["problems"] };
                result["start"] = made.ContainsKey(name + "_Start") ? name + "_Start.on_loaded, then " + startDelay + " s, then " + name + "_Begin.trigger"
                    : startFrom.entity != null ? start + " -> " + name + "_Begin.trigger" : "nothing yet: link something to " + name + "_Begin.trigger";
                result["next"] = "save_level to have it in the game. preview_cage_animation {composite: '" + composite.name + "', entity: '" + name + "_Timeline', view: 'camera', camera: ['" + Cam(name, 0) + "'], time: " + Math.Round(shots[0].Duration / 2, 2) + "} shows shot 1 (pick each shot's camera and a time inside it).";
                return result;
            });
        }

        private static bool IsBuilderName(string name, string full)
        {
            string rest = full.Substring(name.Length);
            if (rest == "_Timeline" || rest == "_Begin" || rest == "_End" || rest == "_DOF" || rest == "_NoBlur" || rest == "_Hud" || rest == "_Start" || rest == "_StartDelay" || rest == "_LoopRelease") return true;
            if (DofFloats.Values.Contains(rest)) return true;
            if (System.Text.RegularExpressions.Regex.IsMatch(rest, "^_Cam[0-9]{2}(_Fov)?$")) return true;
            if (System.Text.RegularExpressions.Regex.IsMatch(rest, "^_Clock[0-9]{2}$")) return true;
            return false;
        }

        private static KeyPose PoseOfKeys(Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> keys, float time)
        {
            Dictionary<string, CAGEAnimation.FloatTrack> tracks = keys.ToDictionary(o => o.Key, o => new CAGEAnimation.FloatTrack() { keyframes = o.Value });
            return KeyFit.PoseAt(tracks, new KeyPose(Vector3.Zero, Vector3.Zero), time, true);
        }

        /// <summary>A FLOAT track on an entity's initial_value (a VariableFloat), made if missing, holding exactly these keys.</summary>
        private static void WriteFloat(McpCageAnimationTools.Working w, Commands commands, Composite composite, Entity entity, List<CAGEAnimation.FloatTrack.Keyframe> keys)
        {
            McpCageAnimationTools.Target target = McpCageAnimationTools.ResolveTarget(commands, composite, new JArray(McpScript.Id(entity.shortGUID)), false, "float");
            ShortGuid parameter = ShortGuidUtils.Generate("initial_value");
            CAGEAnimation.FloatTrack track = McpCageAnimationTools.FindTrack(w, target, parameter, "")
                ?? McpCageAnimationTools.AddTrack(w, target, parameter, DataType.FLOAT, "", keys[0].value.Y);
            foreach (CAGEAnimation.FloatTrack.Keyframe key in keys) key.mode = w.Mode;
            track.keyframes = keys;
            w.Exact.Add(track);
            w.Touched.Add(track);
            w.Changed = true;
        }
        #endregion

        #region get_cinematic
        private static object GetCinematic(McpCall call)
        {
            string name = (call.Str("name") ?? "Cinematic").Trim();
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(false);
                Commands commands = content.Level.Commands;
                Composite composite = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : commands.EntryPoints[0];
                List<Entity> entities = composite.GetEntities().Where(o =>
                {
                    string n = McpScript.EntityName(commands, composite, o) ?? "";
                    return n.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase) && IsBuilderName(name, n);
                }).ToList();
                if (entities.Count == 0) throw new McpError("There is no cinematic '" + name + "' in " + composite.name + " (build_cinematic makes one).");
                JObject result = new JObject() { ["name"] = name, ["composite"] = composite.name };
                result["entities"] = new JArray(entities.Select(o => McpScript.EntityName(commands, composite, o) + " (" + McpScript.TypeName(commands, composite, o) + ")").OrderBy(o => o));
                CAGEAnimation timeline = entities.OfType<CAGEAnimation>().FirstOrDefault();
                if (timeline != null)
                {
                    result["length"] = Math.Round((timeline.GetParameter("anim_length")?.content as cFloat)?.value ?? 0f, 3);
                    JArray events = new JArray();
                    foreach (CAGEAnimation.EventTrack.Keyframe key in timeline.eventTracks.Where(o => o != null).SelectMany(o => o.keyframes).OrderBy(o => o.time))
                    {
                        string pin = McpScript.ParamName(key.forward);
                        List<string> calls = timeline.childLinks.Where(o => o.thisParamID == key.forward).Select(o => McpScript.EntityName(commands, composite, composite.GetEntityByID(o.linkedEntityID)) + "." + McpScript.ParamName(o.linkedParamID)).ToList();
                        events.Add(new JObject() { ["time"] = Math.Round(key.time, 4), ["event"] = pin, ["calls"] = new JArray(calls) });
                    }
                    result["events"] = events;
                    JObject check = McpCageAnimationTools.Check(commands, composite, timeline);
                    result["will_it_play"] = new JObject() { ["verdict"] = check["verdict"], ["problems"] = check["problems"], ["cameras"] = check["cameras"] };
                }
                string file = StoreFile(content.Level.Name, composite.name, name);
                if (File.Exists(file))
                {
                    try { result["shot_list"] = JObject.Parse(File.ReadAllText(file)); result["shot_list_file"] = file; }
                    catch (Exception e) { result["shot_list_note"] = "The kept shot list could not be read: " + e.Message; }
                }
                else result["shot_list_note"] = "No shot list is kept for it on this PC (it is kept when build_cinematic builds it).";
                return result;
            });
        }
        #endregion
    }
}
