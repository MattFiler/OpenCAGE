using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>Scripted cameras: camera rigs and camera tracks for CAGEAnimations.</summary>
    /// <remarks>
    /// <para>The retail pattern for a free camera move (TECH_RND_HZDLAB's benchmark flythrough 'float_cam'): a CameraResource
    /// whose position a CAGEAnimation keys (all six component tracks, Bezier), switched to by a link into its activate_camera
    /// from the same trigger that starts the animation (Checkpoint.finished_loading there), and handed back to the player by
    /// deactivate_camera. The camera binding of an event track is something else: it is for CameraPlayAnimation events
    /// playing baked cutscene clips. Retail starts things when a zone has streamed in with ZoneLoaded.on_loaded (six in
    /// BSP_TORRENS), which is what a rig started at 'level_start' uses.</para>
    /// <para>Conventions, as every spatial tool states them: positions [x,y,z] metres, Y up; rotations [pitch, yaw, roll]
    /// degrees applied yaw then pitch then roll; a camera looks along its local +Z and a positive pitch looks down, so facing
    /// d is [-asin(d.y/|d|), atan2(d.x, d.z), 0] (<see cref="InstanceTransform.LookRotation"/>).</para>
    /// <para>A track is built in world space - a centripetal Catmull-Rom spline through the points (wrapped round for a
    /// loop), keys placed along it by arc length so the camera moves at a steady speed (or eases in and out), each key's
    /// rotation looking where it is told to - then each key is taken into the space the camera's position is written in,
    /// rotations unwrapped there, and given Hermite tangents (weight one: tan.X is the neighbouring segment's seconds, as
    /// exported retail keys carry), seamless across the join of a loop.</para>
    /// </remarks>
    internal static class McpCameraTools
    {
        private const string Conventions = "Positions [x, y, z] metres, Y up; rotations [pitch, yaw, roll] degrees (a camera looks along its +Z; positive pitch looks down).";

        private static JObject Prop(string type, string description) => new JObject() { ["type"] = type, ["description"] = description };

        //An entity path as every tool takes one (McpScript.PathSteps): an array of ids/names, one string split on '/', or a result's {ids} object
        private static JObject PathSchema() => new JObject()
        {
            ["anyOf"] = new JArray(
                new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } },
                new JObject() { ["type"] = "string" },
                new JObject()
                {
                    ["type"] = "object",
                    ["properties"] = new JObject() { ["ids"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } }, ["path"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } } },
                    ["additionalProperties"] = true,
                }),
        };

        /// <summary>The arguments a camera path takes, shared by both tools.</summary>
        private static McpSchema.Prop[] PathProps()
        {
            return new[]
            {
                McpSchema.Nested("region", "A room to fly round: a ring of points sampled off its collision as it is in the editor (sample_space loop), looking at the room's centre. " + McpRegion.Help, McpRegion.Schema),
                McpSchema.Nested("points", "Or the points to pass through, in order: [[x,y,z], ...] or [{position: [x,y,z], look_at?: [x,y,z], roll?: degrees}, ...] (look_at and roll blend from point to point).", new JObject() { ["type"] = "array" }),
                McpSchema.String("space", "What 'points' and 'look' points are in: world (default; find_places, sample_space and raycast give world points) or composite (the space the camera's position is written in).", options: new[] { "world", "composite" }),
                McpSchema.Integer("placement", "Which placement of the composite the move is for (0-based, get_placements order), when it is placed more than once."),
                McpSchema.Any("look", "Where the camera looks: 'centre' (default with a region: the room's centre at look_height), 'along_path' (default with points: the way it moves), a point [x,y,z], or 'points' (each point's own look_at)."),
                McpSchema.Number("roll", "Roll in degrees, added to every key (default 0)."),
                McpSchema.Number("duration", "Seconds for the whole path (default: its length at 'speed')."),
                McpSchema.Number("speed", "Metres a second, when no duration is given (default 1)."),
                McpSchema.Number("start_time", "When the move starts in the animation (s, default 0); before it the camera holds the first key."),
                McpSchema.Boolean("closed", "The path loops back to its first point, joining smoothly (default true for a region; for points, true when the last point is the first)."),
                McpSchema.String("ease", "none (steady speed; default for a loop), in_out (default for an open path: speeds up from rest and slows to a stop), in or out.", options: new[] { "none", "in_out", "in", "out" }),
                McpSchema.Number("key_spacing", "Metres between keys along the path (default 2; keys also fall on every point)."),
                McpSchema.Integer("count", "region: points round the room (default 10)."),
                McpSchema.Number("height", "region: camera height above the floor (m, default 1.6)."),
                McpSchema.Number("inset", "region: how far in from the walls (m, default 1)."),
                McpSchema.Number("look_height", "region: height above the floor of the centre looked at (m, default: height)."),
                McpSchema.Number("clearance", "Keep the path at least this far off the collision (m, default 0.3): points where the curve passes closer are pushed away from the surface (the result lists them). 0 keeps the points exactly."),
                McpSchema.String("collision", "The collision the path keeps clear of and is checked against (default camera). " + McpCollision.FilterHelp, options: McpCollision.Filters),
            };
        }

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "create_camera_animation",
                Title = "Create camera animation",
                Description = "Build a scripted camera move that plays in game, the way retail does it (TECH_RND_HZDLAB's flythrough): a CameraResource, a CAGEAnimation keying its position along a smooth path (round a room's 'region' sampled off its collision as it is in the editor, unsaved edits included, or through 'points'), " +
                    "a trigger that activates the camera and starts the animation ('start': level_start makes a ZoneLoaded whose on_loaded fires when the composite streams in; or an existing entity's output pin), and what happens at the end ('end': return hands the view back with deactivate_camera, loop starts it again, hold stays). " +
                    "One undo step. Returns the entities, the links, the track (length, speed, turn rate), a collision check of the path and check_cage_animation's verdict. Save with save_level to have it in game; preview_cage_animation view:'camera' shows the shot. " + Conventions,
                InputSchema = McpSchema.Object(new[]
                {
                    McpSchema.String("composite", "Where to make them (path or id). " + McpSchema.CompositeDefaultRoot + " Make them in the room's own composite to have them travel with it."),
                    McpSchema.String("name", "Name of the camera (default 'FlyCam'); the animation is <name>_Anim, the trigger <name>_Start."),
                    McpSchema.String("start", "level_start (default: a ZoneLoaded, firing when the composite streams in), none (wire it yourself: link to the camera's activate_camera and the animation's start), or 'Entity.pin' (an existing entity's output in the composite, e.g. 'PlayerTriggerBox_1.on_entered')."),
                    McpSchema.String("end", "return (default: animation_finished -> deactivate_camera, blending back over blend_out), loop (animation_finished -> start; use a closed path) or hold (the view stays on the camera).", options: new[] { "return", "loop", "hold" }),
                    McpSchema.Number("fov", "The camera's vertical field of view in degrees (default 45)."),
                    McpSchema.Number("blend_in", "Seconds to blend from the player's view into the camera (default 0: a cut)."),
                    McpSchema.Number("blend_out", "Seconds to blend back to the player at the end (default 1; end 'return' only)."),
                    McpSchema.Nested("existing_camera", "Use this CameraResource (path from the composite: ids/names, a string split on '/', or a result's {ids} object) instead of making one; its position is keyed.", PathSchema()),
                }.Concat(PathProps()).ToArray()),
                Destructive = false,
                Run = CreateCameraAnimation,
            };

            yield return new McpTool()
            {
                Name = "animate_camera_path",
                Title = "Animate camera path",
                Description = "Key a camera (or anything with a position) along a smooth path in an existing CAGEAnimation: round a room ('region', sampled off its collision as it is in the editor, unsaved edits included) or through 'points', looking at the room's centre, along the path or at points, at a steady speed or easing in and out, closed loops joining seamlessly. " +
                    "Rotations are built for you (unwrapped, so the camera never spins the long way round) and every key is converted into the space the camera's position is written in. Replaces the target's position keys (or only those in the move's time span with replace:false). " +
                    "One undo step. Returns the track (length, speed, turn rate), a collision check of the path and check_cage_animation's verdict; create_camera_animation builds the camera and its wiring too. " + Conventions,
                InputSchema = McpSchema.Object(new[]
                {
                    McpSchema.String("composite", "The composite the CAGEAnimation is in.", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true),
                    McpSchema.Nested("camera", "What to move: a path from the animation's composite (ids/names, or a result's {ids} object), e.g. ['FlyCam']. Default: the CameraResource the animation already moves, else the only CameraResource in its composite.", PathSchema()),
                    McpSchema.Boolean("from_root", "'camera' starts in the level's root composite."),
                    McpSchema.Boolean("replace", "Replace every position key of the target (default true); false keeps keys outside the move's time span."),
                    McpSchema.Number("length", "The animation's anim_length (default: kept, or the end of the move when that is later; with replace, one that ended at its last key (no hold after it) follows the new last key; a new animation takes the end of the move)."),
                }.Concat(PathProps()).ToArray()),
                Destructive = true,
                Run = AnimateCameraPath,
            };
        }

        #region Reading the path
        /// <summary>A camera path as asked for: control points and look targets in world space, and the timing.</summary>
        private sealed class PathSpec
        {
            public List<Vector3> Points = new List<Vector3>();
            public List<Vector3?> LookAts = new List<Vector3?>();
            public List<float?> Rolls = new List<float?>();
            /// <summary>centre, along_path, point or points.</summary>
            public string Look;
            public Vector3 LookPoint;
            public float Roll;
            public bool Closed;
            public double Duration;
            public float StartTime;
            public string Ease;
            public float KeySpacing;
            public string Collision;
            public bool Local;
            public float Clearance;
            /// <summary>Why a composite-space path could not be placed in the world (so it was not checked), or null.</summary>
            public string Unplaced;
            public JObject Room;
            public List<string> Notes = new List<string>();
        }

        private static float Finite(McpCall call, string name, double fallback, double min, double max, string what)
        {
            double value = call.Num(name, fallback);
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
                throw new McpError("'" + name + "' is " + what + ".");
            return (float)value;
        }

        /// <summary>The path arguments, a room sampled for its points when a region is given. Not on the UI thread.</summary>
        private static PathSpec ReadPath(McpCall call)
        {
            PathSpec spec = new PathSpec();
            bool region = call.Has("region"), points = call.Has("points");
            if (region == points)
                throw new McpError(region ? "Give 'region' or 'points', not both." : "Say where the camera goes: 'region' (a room to fly round, e.g. {\"name\": \"canteen\"} - find_places finds rooms) or 'points' ([[x,y,z], ...]).");
            string space = (call.Str("space") ?? "world").Trim().ToLowerInvariant();
            if (space == "local") space = "composite";
            if (space != "world" && space != "composite") throw new McpError("'space' is world or composite.");
            spec.Local = space == "composite";
            spec.Collision = McpCollision.ReadFilter(call);
            if (!call.Has("collision")) spec.Collision = "camera";
            spec.Clearance = Finite(call, "clearance", 0.3, 0, 5, "metres off the collision, 0 to 5");
            spec.Roll = Finite(call, "roll", 0, -3600, 3600, "degrees");
            spec.StartTime = Finite(call, "start_time", 0, 0, 1e6, "a time in seconds, 0 or more");
            spec.KeySpacing = Finite(call, "key_spacing", 2, 0.1, 1000, "metres between keys, at least 0.1");

            if (region)
            {
                if (spec.Local) throw new McpError("'space' is for 'points': a region's points come from the level in world space.");
                int count = call.Int("count", 10);
                if (count < 3 || count > 60) throw new McpError("'count' is 3 to 60 points round the room.");
                float height = Finite(call, "height", 1.6, 0.05, 20, "metres above the floor, more than 0");
                float inset = Finite(call, "inset", 1.0, 0, 20, "metres in from the walls, 0 or more");
                float lookHeight = Finite(call, "look_height", height, -20, 40, "metres above the floor");
                McpTool sampler = McpTools.Find("sample_space");
                if (sampler == null) throw new McpError("The room sampler (sample_space) is not available.");
                JObject args = new JObject() { ["region"] = call.Token("region").DeepClone(), ["pattern"] = "loop", ["count"] = count, ["height"] = height, ["inset"] = inset, ["look_at"] = "none", ["collision"] = spec.Collision == "camera" ? "solid" : spec.Collision };
                McpCall inner = new McpCall(sampler, args, call.Cancel, (text, done, total) => call.Progress(text, done, total));
                JObject room = sampler.Run(inner) as JObject ?? throw new McpError("The room could not be sampled.");
                foreach (string note in inner.Notes) spec.Notes.Add(note);
                if (room["warnings"] is JArray warnings) foreach (JToken warning in warnings) spec.Notes.Add("Room: " + (string)warning);
                foreach (JObject point in (room["points"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    spec.Points.Add(McpValues.ReadVector(point["position"], "points", null));
                    spec.LookAts.Add(null);
                    spec.Rolls.Add(null);
                }
                if (spec.Points.Count < 3) throw new McpError("Only " + spec.Points.Count + " free points were found round " + (string)room["region"] + ": too few for a path. Try a smaller 'inset' or another 'height'.");
                Vector3 centre = McpValues.ReadVector(room["centre"], "centre", null);
                float floor = (float)(room["floor_y"]?.Value<double>() ?? centre.Y - height);
                spec.LookPoint = new Vector3(centre.X, floor + lookHeight, centre.Z);
                spec.Look = "centre";
                spec.Closed = !call.Has("closed") || call.Bool("closed");
                spec.Room = new JObject()
                {
                    ["region"] = room["region"],
                    ["room_box"] = room["room"],
                    ["floor_y"] = room["floor_y"],
                    ["centre_looked_at"] = McpCollision.V(spec.LookPoint),
                    ["loop_length"] = room["loop_length"],
                };
                if (room["doors"] != null) spec.Room["doors"] = new JArray((room["doors"] as JArray).OfType<JObject>().Select(o => new JObject() { ["name"] = o["name"], ["position"] = o["position"] }));
            }
            else
            {
                JArray list = call.Array("points");
                if (list.Count < 2 || list.Count > 200) throw new McpError("'points' takes 2 to 200 points.");
                for (int i = 0; i < list.Count; i++)
                {
                    string where = "points[" + i + "]";
                    if (list[i] is JObject item)
                    {
                        foreach (JProperty property in item.Properties())
                            if (property.Name != "position" && property.Name != "look_at" && property.Name != "roll")
                                throw new McpError("'" + where + "' takes position, look_at and roll (not '" + property.Name + "').");
                        if (item["position"] == null) throw new McpError("'" + where + "' needs 'position'.");
                        spec.Points.Add(McpValues.ReadVector(item["position"], where + ".position", null));
                        spec.LookAts.Add(item["look_at"] != null ? McpValues.ReadVector(item["look_at"], where + ".look_at", null) : (Vector3?)null);
                        spec.Rolls.Add(item["roll"] != null ? (float)McpValues.ReadDouble(item["roll"], where + ".roll") : (float?)null);
                    }
                    else
                    {
                        spec.Points.Add(McpValues.ReadVector(list[i], where, null));
                        spec.LookAts.Add(null);
                        spec.Rolls.Add(null);
                    }
                }
                bool loopsBack = Vector3.Distance(spec.Points[0], spec.Points[spec.Points.Count - 1]) < 0.01f;
                spec.Closed = call.Has("closed") ? call.Bool("closed") : loopsBack;
                spec.Look = spec.LookAts.Any(o => o != null) ? "points" : "along_path";
            }
            //A loop given with its first point repeated at the end: the join is made by the loop itself
            if (spec.Closed && spec.Points.Count > 2 && Vector3.Distance(spec.Points[0], spec.Points[spec.Points.Count - 1]) < 0.01f)
            {
                spec.Points.RemoveAt(spec.Points.Count - 1);
                spec.LookAts.RemoveAt(spec.LookAts.Count - 1);
                spec.Rolls.RemoveAt(spec.Rolls.Count - 1);
            }
            if (spec.Closed && spec.Points.Count < 3) throw new McpError("A closed path needs at least 3 different points.");
            for (int i = 1; i < spec.Points.Count; i++)
                if (Vector3.Distance(spec.Points[i], spec.Points[i - 1]) < 0.01f)
                    throw new McpError("points " + (i - 1) + " and " + i + " are the same place: give each point once.");

            if (call.Has("look"))
            {
                JToken look = call.Token("look");
                if (look is JArray)
                {
                    spec.LookPoint = McpValues.ReadVector(look, "look", null);
                    spec.Look = "point";
                }
                else
                {
                    string mode = McpValues.ReadString(look).Trim().ToLowerInvariant();
                    if (mode == "center") mode = "centre";
                    if (mode == "path" || mode == "along" || mode == "next" || mode == "forward") mode = "along_path";
                    if (mode == "centre" && !region) throw new McpError("'look': 'centre' is the room's centre, for a 'region'; with points give a point [x,y,z] to look at.");
                    if (mode == "points" && !spec.LookAts.Any(o => o != null)) throw new McpError("'look': 'points' uses each point's look_at, and none was given.");
                    if (mode != "centre" && mode != "along_path" && mode != "points") throw new McpError("'look' is 'centre', 'along_path', 'points' or a point [x,y,z].");
                    spec.Look = mode;
                }
            }

            string ease = (call.Str("ease") ?? (spec.Closed ? "none" : "in_out")).Trim().ToLowerInvariant();
            if (ease == "inout" || ease == "in-out" || ease == "both") ease = "in_out";
            if (ease != "none" && ease != "in_out" && ease != "in" && ease != "out") throw new McpError("'ease' is none, in_out, in or out.");
            spec.Ease = ease;
            if (call.Has("duration") && call.Has("speed")) throw new McpError("Give 'duration' or 'speed', not both.");
            if (call.Has("duration")) spec.Duration = Finite(call, "duration", 0, 0.05, 1e5, "seconds, more than 0");
            return spec;
        }
        #endregion

        #region Building the track
        /// <summary>A track built from a path: keys per component in the camera's space, and what it does in the world.</summary>
        internal sealed class BuiltTrack
        {
            public Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> Keys = new Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>>();
            public cTransform FirstKey;
            public float Start, End, PathLength;
            public int KeyCount;
            public JObject Summary;
            /// <summary>World points along the finished curves, for the collision check.</summary>
            public List<KeyValuePair<float, Vector3>> Samples = new List<KeyValuePair<float, Vector3>>();
            public List<string> Warnings = new List<string>();
        }

        /// <summary>A centripetal Catmull-Rom spline through points, open or closed, with an arc-length table.</summary>
        private sealed class Spline
        {
            private readonly List<Vector3> _points;
            private readonly bool _closed;
            private const int Steps = 64;
            /// <summary>Cumulative length at each segment's start, and each segment's own table.</summary>
            public readonly List<float> SegmentStart = new List<float>();
            private readonly List<float[]> _tables = new List<float[]>();
            public float Length;
            public int Segments => _closed ? _points.Count : _points.Count - 1;

            public Spline(List<Vector3> points, bool closed)
            {
                _points = points;
                _closed = closed;
                for (int s = 0; s < Segments; s++)
                {
                    float[] table = new float[Steps + 1];
                    Vector3 previous = Point(s, 0f);
                    for (int i = 1; i <= Steps; i++)
                    {
                        Vector3 p = Point(s, i / (float)Steps);
                        table[i] = table[i - 1] + Vector3.Distance(previous, p);
                        previous = p;
                    }
                    SegmentStart.Add(Length);
                    _tables.Add(table);
                    Length += table[Steps];
                }
            }

            public float SegmentLength(int s) => _tables[s][Steps];

            private Vector3 Control(int i)
            {
                int n = _points.Count;
                if (_closed) return _points[((i % n) + n) % n];
                if (i < 0) return 2f * _points[0] - _points[1];
                if (i >= n) return 2f * _points[n - 1] - _points[n - 2];
                return _points[i];
            }

            /// <summary>The point a fraction u (0-1) through segment s.</summary>
            public Vector3 Point(int s, float u)
            {
                Vector3 p0 = Control(s - 1), p1 = Control(s), p2 = Control(s + 1), p3 = Control(s + 2);
                float t0 = 0f;
                float t1 = t0 + Knot(p0, p1);
                float t2 = t1 + Knot(p1, p2);
                float t3 = t2 + Knot(p2, p3);
                float t = t1 + (t2 - t1) * u;
                Vector3 a1 = Mix(p0, p1, t0, t1, t), a2 = Mix(p1, p2, t1, t2, t), a3 = Mix(p2, p3, t2, t3, t);
                Vector3 b1 = Mix(a1, a2, t0, t2, t), b2 = Mix(a2, a3, t1, t3, t);
                return Mix(b1, b2, t1, t2, t);
            }

            private static float Knot(Vector3 a, Vector3 b) => Math.Max(1e-3f, (float)Math.Sqrt(Vector3.Distance(a, b)));

            private static Vector3 Mix(Vector3 a, Vector3 b, float ta, float tb, float t) => tb - ta < 1e-6f ? a : ((tb - t) * a + (t - ta) * b) / (tb - ta);

            /// <summary>The unit direction of travel a fraction u through segment s.</summary>
            public Vector3 Direction(int s, float u)
            {
                const float h = 1e-3f;
                Vector3 d = Point(s, Math.Min(1f, u + h)) - Point(s, Math.Max(0f, u - h));
                return d.LengthSquared() < 1e-12f ? Vector3.UnitZ : Vector3.Normalize(d);
            }

            /// <summary>The fraction through segment s at which the curve has run <paramref name="distance"/> metres along it.</summary>
            public float FractionAt(int s, float distance)
            {
                float[] table = _tables[s];
                if (distance <= 0f) return 0f;
                if (distance >= table[Steps]) return 1f;
                int i = Array.BinarySearch(table, distance);
                if (i >= 0) return i / (float)Steps;
                i = ~i;
                float a = table[i - 1], b = table[i];
                return (i - 1 + (b - a < 1e-9f ? 0f : (distance - a) / (b - a))) / Steps;
            }
        }

        /// <summary>How far through the path (0-1) the camera is a fraction x through the time, and its speed there (in path lengths per duration).</summary>
        private static void Ease(string ease, double x, out double along, out double speed)
        {
            const double ramp = 0.25;
            double rin = ease == "in" || ease == "in_out" ? ramp : 0, rout = ease == "out" || ease == "in_out" ? ramp : 0;
            double vmax = 1.0 / (1.0 - rin / 2 - rout / 2);
            if (rin > 0 && x < rin) { along = vmax * x * x / (2 * rin); speed = vmax * x / rin; }
            else if (rout > 0 && x > 1 - rout) { along = 1 - vmax * (1 - x) * (1 - x) / (2 * rout); speed = vmax * (1 - x) / rout; }
            else { along = vmax * (rin / 2 + x - rin); speed = vmax; }
        }

        /// <summary>The time fraction (0-1) at which the camera has come <paramref name="along"/> (0-1) of the way.</summary>
        private static double TimeAt(string ease, double along)
        {
            if (along <= 0) return 0;
            if (along >= 1) return 1;
            double lo = 0, hi = 1;
            for (int i = 0; i < 50; i++)
            {
                double mid = (lo + hi) / 2;
                Ease(ease, mid, out double at, out double _);
                if (at < along) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }

        private sealed class Key
        {
            public float Time;
            public Vector3 Position;
            /// <summary>World velocity (m/s).</summary>
            public Vector3 Velocity;
            public Vector3 Rotation;
            public int Segment;
            public float Fraction;
            /// <summary>Metres along the whole path.</summary>
            public float Distance;
        }

        /// <summary>
        /// Build the track: keys along the spline at least every key_spacing metres (and on every point), timed by distance
        /// for a steady speed or eased, rotations from the look rule, then taken into <paramref name="frame"/>'s space (the
        /// space the camera's position is written in; null for world) with Hermite tangents. Pure maths: any thread.
        /// </summary>
        private static BuiltTrack Build(PathSpec spec, cTransform frame)
        {
            Spline spline = new Spline(spec.Points, spec.Closed);
            if (spline.Length < 0.05f) throw new McpError("The path is only " + Math.Round(spline.Length, 3) + " m long: give points further apart.");
            double duration = spec.Duration > 0 ? spec.Duration : spline.Length / 1.0;
            BuiltTrack built = new BuiltTrack() { Start = spec.StartTime, PathLength = spline.Length };
            built.End = (float)(spec.StartTime + duration);

            //Keys: every point, and between them often enough that no two are more than key_spacing apart along the curve
            List<Key> keys = new List<Key>();
            for (int s = 0; s < spline.Segments; s++)
            {
                float length = spline.SegmentLength(s);
                int parts = Math.Max(1, (int)Math.Ceiling(length / spec.KeySpacing - 1e-4));
                for (int j = 0; j < parts; j++)
                {
                    float fraction = spline.FractionAt(s, length * j / parts);
                    keys.Add(new Key() { Segment = s, Fraction = fraction, Position = spline.Point(s, fraction), Distance = spline.SegmentStart[s] + length * j / parts });
                }
            }
            //The end: the last point, or (a loop) back where it started
            int last = spline.Segments - 1;
            keys.Add(new Key() { Segment = last, Fraction = 1f, Position = spline.Point(last, 1f), Distance = spline.Length });
            if (keys.Count > 400) throw new McpError("That would be " + keys.Count + " keys: raise 'key_spacing' (metres between keys).");

            //Times by distance along the curve, and the velocity there
            for (int k = 0; k < keys.Count; k++)
            {
                Key key = keys[k];
                double along = k == keys.Count - 1 ? 1.0 : key.Distance / spline.Length;
                double x = TimeAt(spec.Ease, along);
                Ease(spec.Ease, x, out double _, out double speed);
                key.Time = (float)(spec.StartTime + x * duration);
                key.Velocity = spline.Direction(key.Segment, key.Fraction) * (float)(speed * spline.Length / duration);
            }
            if (spec.Closed) keys[keys.Count - 1].Velocity = keys[0].Velocity;

            //Rotations, unwrapped along the track
            List<Vector3> worldRotations = new List<Vector3>();
            foreach (Key key in keys)
            {
                Vector3 direction;
                switch (spec.Look)
                {
                    case "centre":
                    case "point":
                        direction = spec.LookPoint - key.Position;
                        break;
                    case "points":
                        direction = LookDirectionAt(spec, key, spline.Direction(key.Segment, key.Fraction));
                        break;
                    default:
                        direction = spline.Direction(key.Segment, key.Fraction);
                        break;
                }
                if (direction.LengthSquared() < 1e-6f) direction = spline.Direction(key.Segment, key.Fraction);
                Vector3 facing = InstanceTransform.LookRotation(direction);
                worldRotations.Add(new Vector3(facing.X, facing.Y, spec.Roll + RollAt(spec, key)));
            }
            InstanceTransform.UnwrapRotations(worldRotations);

            //Into the camera's own space
            List<Vector3> positions = new List<Vector3>(), velocities = new List<Vector3>(), rotations = new List<Vector3>();
            for (int k = 0; k < keys.Count; k++)
            {
                cTransform local = frame == null ? new cTransform(keys[k].Position, worldRotations[k]) : InstanceTransform.ToLocal(frame, new cTransform(keys[k].Position, worldRotations[k]));
                positions.Add(local.position);
                rotations.Add(local.rotation);
                velocities.Add(frame == null ? keys[k].Velocity : InstanceTransform.DirectionToLocal(frame, keys[k].Velocity));
            }
            InstanceTransform.UnwrapRotations(rotations);

            List<float> times = keys.Select(o => o.Time).ToList();
            string[] components = { "x", "y", "z", "Yaw", "Pitch", "Roll" };
            Func<int, int, float> value = (k, c) => c == 0 ? positions[k].X : c == 1 ? positions[k].Y : c == 2 ? positions[k].Z : c == 3 ? rotations[k].Y : c == 4 ? rotations[k].X : rotations[k].Z;
            for (int c = 0; c < components.Length; c++)
            {
                List<float> values = Enumerable.Range(0, keys.Count).Select(k => value(k, c)).ToList();
                List<float> slopes = c < 3
                    ? Enumerable.Range(0, keys.Count).Select(k => c == 0 ? velocities[k].X : c == 1 ? velocities[k].Y : velocities[k].Z).ToList()
                    : Slopes(times, values, spec.Closed, spec.Ease);
                built.Keys[components[c]] = Hermite(times, values, slopes, spec.Closed);
            }
            built.KeyCount = keys.Count;
            built.FirstKey = new cTransform(positions[0], rotations[0]);
            Measure(built, frame, spec);
            return built;
        }

        /// <summary>
        /// 'points' look: each end of the key's segment looks at its point's look_at, or along the path where it has none;
        /// the key blends the two directions by how far along the segment it is.
        /// </summary>
        private static Vector3 LookDirectionAt(PathSpec spec, Key key, Vector3 along)
        {
            int n = spec.Points.Count;
            Vector3? a = spec.LookAts[key.Segment % n], b = spec.LookAts[(key.Segment + 1) % n];
            Vector3 from = a != null && (a.Value - key.Position).LengthSquared() > 1e-6f ? Vector3.Normalize(a.Value - key.Position) : along;
            Vector3 to = b != null && (b.Value - key.Position).LengthSquared() > 1e-6f ? Vector3.Normalize(b.Value - key.Position) : along;
            Vector3 blended = Vector3.Lerp(from, to, key.Fraction);
            return blended.LengthSquared() < 1e-6f ? along : blended;
        }

        /// <summary>A point's roll (0 where it gives none), blended along the segment.</summary>
        private static float RollAt(PathSpec spec, Key key)
        {
            int n = spec.Points.Count;
            float a = spec.Rolls[key.Segment % n] ?? 0f, b = spec.Rolls[(key.Segment + 1) % n] ?? 0f;
            return a + (b - a) * key.Fraction;
        }

        /// <summary>
        /// Slopes for an angle track: the three-point estimate (exact for a parabola through uneven times) inside; at the ends of
        /// a loop the neighbours wrap round a lap (offset by however far the lap turns), so the join is seamless; at the ends of
        /// an open path flat when it eases there, else one-sided.
        /// </summary>
        private static List<float> Slopes(List<float> times, List<float> values, bool closed, string ease)
        {
            int n = times.Count;
            List<float> slopes = new List<float>();
            for (int k = 0; k < n; k++)
            {
                float tp, vp, tn, vn;
                if (k > 0) { tp = times[k - 1]; vp = values[k - 1]; }
                else if (closed && n > 2) { tp = times[n - 2] - (times[n - 1] - times[0]); vp = values[n - 2] - (values[n - 1] - values[0]); }
                else { tp = float.NaN; vp = 0; }
                if (k < n - 1) { tn = times[k + 1]; vn = values[k + 1]; }
                else if (closed && n > 2) { tn = times[1] + (times[n - 1] - times[0]); vn = values[1] + (values[n - 1] - values[0]); }
                else { tn = float.NaN; vn = 0; }
                float t = times[k], v = values[k];
                if (float.IsNaN(tp) && float.IsNaN(tn)) slopes.Add(0f);
                else if (float.IsNaN(tp)) slopes.Add(ease == "in" || ease == "in_out" ? 0f : (vn - v) / Math.Max(1e-4f, tn - t));
                else if (float.IsNaN(tn)) slopes.Add(ease == "out" || ease == "in_out" ? 0f : (v - vp) / Math.Max(1e-4f, t - tp));
                else
                {
                    float dp = Math.Max(1e-4f, t - tp), dn = Math.Max(1e-4f, tn - t);
                    slopes.Add(((vn - v) / dn * dp + (v - vp) / dp * dn) / (dp + dn));
                }
            }
            return slopes;
        }

        /// <summary>Keys with weight-one Hermite tangents: each side's tan.X is its neighbouring segment, tan.Y the slope times it.</summary>
        private static List<CAGEAnimation.FloatTrack.Keyframe> Hermite(List<float> times, List<float> values, List<float> slopes, bool closed)
        {
            int n = times.Count;
            List<CAGEAnimation.FloatTrack.Keyframe> keys = new List<CAGEAnimation.FloatTrack.Keyframe>();
            for (int k = 0; k < n; k++)
            {
                float before = k > 0 ? times[k] - times[k - 1] : closed && n > 2 ? times[n - 1] - times[n - 2] : (n > 1 ? times[1] - times[0] : 1f);
                float after = k < n - 1 ? times[k + 1] - times[k] : closed && n > 2 ? times[1] - times[0] : (n > 1 ? times[n - 1] - times[n - 2] : 1f);
                CAGEAnimation.FloatTrack.Keyframe key = CageAnimationCurves.NewKeyframe(times[k], values[k], CAGEAnimation.InterpolationMode.Bezier);
                key.tan_in = new Vector2(before, slopes[k] * before);
                key.tan_out = new Vector2(after, slopes[k] * after);
                keys.Add(key);
            }
            return keys;
        }

        /// <summary>Play the built curves back as the game does, and measure speed and turning in the world.</summary>
        private static void Measure(BuiltTrack built, cTransform frame, PathSpec spec)
        {
            Dictionary<string, CAGEAnimation.FloatTrack> tracks = built.Keys.ToDictionary(o => o.Key, o => new CAGEAnimation.FloatTrack() { keyframes = o.Value });
            float duration = built.End - built.Start;
            int steps = (int)Math.Min(2000, Math.Max(20, Math.Ceiling(duration / 0.05f)));
            float dt = duration / steps;
            Vector3 previous = Vector3.Zero, previousForward = Vector3.Zero;
            double maxSpeed = 0, minSpeed = double.MaxValue, maxTurn = 0, travelled = 0;
            float maxTurnAt = 0f;
            float sampleEvery = 0.25f, sinceSample = float.MaxValue;
            for (int i = 0; i <= steps; i++)
            {
                float t = built.Start + i * dt;
                Vector3 local = new Vector3(CageAnimationCurves.ValueAt(tracks["x"], t, true), CageAnimationCurves.ValueAt(tracks["y"], t, true), CageAnimationCurves.ValueAt(tracks["z"], t, true));
                Vector3 rotation = new Vector3(CageAnimationCurves.ValueAt(tracks["Pitch"], t, true), CageAnimationCurves.ValueAt(tracks["Yaw"], t, true), CageAnimationCurves.ValueAt(tracks["Roll"], t, true));
                cTransform world = frame == null ? new cTransform(local, rotation) : InstanceTransform.Compose(frame, new cTransform(local, rotation));
                Vector3 forward = InstanceTransform.Forward(world.rotation);
                if (i > 0)
                {
                    float step = Vector3.Distance(previous, world.position);
                    travelled += step;
                    double speed = step / dt;
                    maxSpeed = Math.Max(maxSpeed, speed);
                    if (i > 1 && i < steps) minSpeed = Math.Min(minSpeed, speed);
                    double turn = Math.Acos(Math.Max(-1.0, Math.Min(1.0, Vector3.Dot(previousForward, forward)))) * 180.0 / Math.PI / dt;
                    if (turn > maxTurn) { maxTurn = turn; maxTurnAt = t; }
                    sinceSample += step;
                }
                if (sinceSample >= sampleEvery || i == steps)
                {
                    built.Samples.Add(new KeyValuePair<float, Vector3>(t, world.position));
                    sinceSample = 0f;
                }
                previous = world.position;
                previousForward = forward;
            }
            if (minSpeed == double.MaxValue) minSpeed = 0;
            built.Summary = new JObject()
            {
                ["keys"] = built.KeyCount,
                ["start"] = Math.Round(built.Start, 3),
                ["end"] = Math.Round(built.End, 3),
                ["path_length_m"] = Math.Round(travelled, 2),
                ["speed_m_per_s"] = new JObject() { ["mean"] = Math.Round(travelled / Math.Max(1e-4, duration), 2), ["max"] = Math.Round(maxSpeed, 2), ["min"] = Math.Round(minSpeed, 2) },
                ["max_turn_deg_per_s"] = Math.Round(maxTurn, 1),
                ["closed"] = spec.Closed,
                ["ease"] = spec.Ease,
                ["look"] = spec.Look == "point" ? (JToken)McpCollision.V(spec.LookPoint) : spec.Look,
            };
            if (maxTurn > 90)
                built.Warnings.Add("The camera turns up to " + Math.Round(maxTurn) + " degrees a second (at " + Math.Round(maxTurnAt, 2) + " s): slow it down (duration/speed), or move the points so the view swings less.");
            if (spec.Ease == "none" && maxSpeed > 0 && minSpeed > 0 && maxSpeed / minSpeed > 1.6)
                built.Warnings.Add("Speed varies " + Math.Round(minSpeed, 2) + "-" + Math.Round(maxSpeed, 2) + " m/s along the path (tight corners): a smaller key_spacing evens it out.");
        }
        #endregion

        #region Keeping a path off the collision
        private static readonly Vector3[] Around =
        {
            Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ,
            Vector3.Normalize(new Vector3(1, 0, 1)), Vector3.Normalize(new Vector3(1, 0, -1)), Vector3.Normalize(new Vector3(-1, 0, 1)), Vector3.Normalize(new Vector3(-1, 0, -1)),
            Vector3.Normalize(new Vector3(1, 1, 0)), Vector3.Normalize(new Vector3(-1, 1, 0)), Vector3.Normalize(new Vector3(0, 1, 1)), Vector3.Normalize(new Vector3(0, 1, -1)),
        };

        /// <summary>The nearest surface round a point within <paramref name="reach"/>, by rays in 14 directions.</summary>
        private static bool Nearest(McpCollision.Soup soup, Vector3 point, float reach, out McpCollision.Hit nearest)
        {
            nearest = default(McpCollision.Hit);
            bool found = false;
            foreach (Vector3 direction in Around)
                if (soup.Cast(point, direction, reach, out McpCollision.Hit hit) && (!found || hit.Distance < nearest.Distance)) { nearest = hit; found = true; }
            return found;
        }

        /// <summary>
        /// Keep the curve off the collision: where the spline through the points passes closer than <paramref name="clearance"/>
        /// to a surface (by the level's collision as it is in the editor), the points either side are pushed away from it along
        /// the surface's normal, and the curve is measured again - up to four times. A point is never pushed through something.
        /// Returns the points moved. Not on the UI thread.
        /// </summary>
        private static JArray KeepClear(McpCall call, PathSpec spec, float clearance)
        {
            JArray moved = new JArray();
            if (clearance <= 0f || spec.Points.Count < 2) return moved;
            List<Vector3> original = new List<Vector3>(spec.Points);
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (Vector3 point in spec.Points) { min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
            McpCollision.Soup soup = McpCollision.SoupFor(call, McpRegion.Box(min - new Vector3(clearance + 3f), max + new Vector3(clearance + 3f), "round the camera path"), spec.Collision);
            int n = spec.Points.Count;
            for (int pass = 0; pass < 4; pass++)
            {
                Spline spline = new Spline(spec.Points, spec.Closed);
                Vector3[] push = new Vector3[n];
                bool any = false;
                for (int s = 0; s < spline.Segments; s++)
                {
                    int steps = Math.Max(4, (int)Math.Ceiling(spline.SegmentLength(s) / 0.25f));
                    for (int j = 0; j <= steps; j++)
                    {
                        float f = j / (float)steps;
                        Vector3 point = spline.Point(s, f);
                        if (!Nearest(soup, point, clearance + 0.05f, out McpCollision.Hit hit) || hit.Distance >= clearance) continue;
                        //The normal faces the ray's start, so along it is away from the surface
                        Vector3 away = hit.Normal * (clearance - hit.Distance);
                        //Shared between the segment's two points by how near each is; a little over, as the curve only follows its points part of the way
                        int a = s, b = spec.Closed ? (s + 1) % n : s + 1;
                        Vector3 toA = away * (1f - f) * 1.5f, toB = away * f * 1.5f;
                        if (toA.Length() > push[a].Length()) push[a] = toA;
                        if (toB.Length() > push[b].Length()) push[b] = toB;
                        any = true;
                    }
                }
                if (!any) break;
                for (int i = 0; i < n; i++)
                {
                    if (push[i].LengthSquared() < 1e-8f) continue;
                    Vector3 to = spec.Points[i] + push[i];
                    if (!soup.Clear(spec.Points[i], to, out McpCollision.Hit _, throughSeeThrough: false)) continue;
                    spec.Points[i] = to;
                }
            }
            for (int i = 0; i < n; i++)
            {
                float by = Vector3.Distance(original[i], spec.Points[i]);
                if (by > 0.01f) moved.Add(new JObject() { ["point"] = i, ["by_m"] = Math.Round(by, 2), ["to"] = McpCollision.V(spec.Points[i]) });
            }
            return moved;
        }
        #endregion

        #region Checking a path against collision
        /// <summary>
        /// Where a path through the world passes through collision (a straight line between successive points is blocked) or
        /// comes within 0.25 m of it, against the level as it stands in the editor. Not on the UI thread.
        /// </summary>
        internal static JObject CheckPath(McpCall call, List<KeyValuePair<float, Vector3>> path, string filter)
        {
            JObject result = new JObject() { ["collision"] = filter, ["points_checked"] = path.Count };
            if (path.Count < 2) return result;
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (KeyValuePair<float, Vector3> point in path) { min = Vector3.Min(min, point.Value); max = Vector3.Max(max, point.Value); }
            McpRegion box = McpRegion.Box(min - new Vector3(1.5f), max + new Vector3(1.5f), "round the camera path");
            McpCollision.Soup soup = McpCollision.SoupFor(call, box, filter);
            List<(float from, float to, Vector3 at, McpCollision.Record what)> blocked = new List<(float, float, Vector3, McpCollision.Record)>();
            for (int i = 1; i < path.Count; i++)
            {
                if (soup.Clear(path[i - 1].Value, path[i].Value, out McpCollision.Hit hit, throughSeeThrough: false)) continue;
                if (blocked.Count != 0 && Math.Abs(blocked[blocked.Count - 1].to - path[i - 1].Key) < 1e-4f && blocked[blocked.Count - 1].what == hit.Record)
                    blocked[blocked.Count - 1] = (blocked[blocked.Count - 1].from, path[i].Key, blocked[blocked.Count - 1].at, hit.Record);
                else
                    blocked.Add((path[i - 1].Key, path[i].Key, hit.Point, hit.Record));
            }
            List<(float time, float distance, Vector3 at, McpCollision.Record what)> close = new List<(float, float, Vector3, McpCollision.Record)>();
            Vector3[] around = { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ,
                Vector3.Normalize(new Vector3(1, 0, 1)), Vector3.Normalize(new Vector3(1, 0, -1)), Vector3.Normalize(new Vector3(-1, 0, 1)), Vector3.Normalize(new Vector3(-1, 0, -1)) };
            float nearestAll = float.MaxValue;
            foreach (KeyValuePair<float, Vector3> point in path)
            {
                float nearest = float.MaxValue;
                McpCollision.Hit nearestHit = default(McpCollision.Hit);
                foreach (Vector3 direction in around)
                    if (soup.Cast(point.Value, direction, 1f, out McpCollision.Hit hit) && hit.Distance < nearest) { nearest = hit.Distance; nearestHit = hit; }
                nearestAll = Math.Min(nearestAll, nearest);
                if (nearest < 0.25f && (close.Count == 0 || point.Key - close[close.Count - 1].time > 1f))
                    close.Add((point.Key, nearest, point.Value, nearestHit.Record));
            }
            JArray blockedRows = new JArray(), closeRows = new JArray();
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                foreach ((float from, float to, Vector3 at, McpCollision.Record what) in blocked.Take(10))
                    blockedRows.Add(new JObject() { ["from_t"] = Math.Round(from, 2), ["to_t"] = Math.Round(to, 2), ["at"] = McpCollision.V(at), ["hits"] = McpCollision.Describe(commands, what, brief: true) });
                foreach ((float time, float distance, Vector3 at, McpCollision.Record what) in close.Take(10))
                    closeRows.Add(new JObject() { ["t"] = Math.Round(time, 2), ["distance_m"] = Math.Round(distance, 2), ["at"] = McpCollision.V(at), ["near"] = McpCollision.Describe(commands, what, brief: true) });
            });
            result["clear"] = blocked.Count == 0;
            if (nearestAll < float.MaxValue) result["nearest_m"] = Math.Round(nearestAll, 2);
            if (blockedRows.Count != 0) result["passes_through"] = blockedRows;
            if (blocked.Count > 10) result["passes_through_total"] = blocked.Count;
            if (closeRows.Count != 0) result["close_to"] = closeRows;
            if (soup.Truncated) result["note"] = "The collision round the path was too much to load in full; some may be missing.";
            return result;
        }
        #endregion

        #region The tools
        /// <summary>The CameraResource an animation moves (its position), else the only one in its composite. UI thread.</summary>
        private static McpCageAnimationTools.Target DefaultCamera(Commands commands, Composite composite, CAGEAnimation animation)
        {
            foreach (CAGEAnimation.Connection connection in animation.connections)
            {
                if (connection?.connectedEntity?.path == null || connection.target_param != ShortGuidUtils.Generate("position")) continue;
                List<Tuple<Composite, Entity>> chain = commands.Utils.ResolveEntityPath(connection.connectedEntity, composite);
                (Composite c, Entity e) = commands.Utils.GetResolvedTarget(chain);
                if (McpCageAnimationTools.KindOf(e) != "CameraResource") continue;
                //The path as it is stored (from the composite, or from the root): the target it already reads back as, not re-read from the composite
                return new McpCageAnimationTools.Target()
                {
                    Path = new EntityPath((ShortGuid[])connection.connectedEntity.path.Clone()),
                    Composite = c,
                    Entity = e,
                    Chain = chain,
                    Name = McpScript.EntityName(commands, c, e),
                };
            }
            List<FunctionEntity> cameras = composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.CameraResource).ToList();
            if (cameras.Count == 1)
                return McpCageAnimationTools.ResolveTarget(commands, composite, new JArray(McpScript.Id(cameras[0].shortGUID)), false, "camera");
            throw new McpError("Say which camera to move with 'camera' (a path from " + composite.name + "): " + (cameras.Count == 0
                ? "the animation moves no CameraResource and there is none in " + composite.name + " (create_camera_animation makes one with its wiring)."
                : "there are " + cameras.Count + " CameraResources in " + composite.name + ": " + string.Join(", ", cameras.Take(10).Select(o => McpScript.EntityName(commands, composite, o))) + "."));
        }

        /// <summary>
        /// The world frame of the space the camera's position is written in, with points given in that space ('space':
        /// 'composite') brought into world space so the path is built, kept clear and checked there. A composite-space path
        /// whose composite has no single placement is built as given, unchecked (<see cref="PathSpec.Unplaced"/> says why). UI thread.
        /// </summary>
        private static cTransform FrameFor(PathSpec spec, Func<cTransform> frameOf)
        {
            cTransform frame;
            try
            {
                frame = frameOf();
            }
            catch (McpError e) when (spec.Local)
            {
                spec.Unplaced = "not checked against collision: " + e.Message;
                spec.Notes.Add("The path was keyed as given in the composite's space, but " + e.Message.TrimEnd('.') + ", so it was not kept clear of or checked against collision.");
                return null;
            }
            if (spec.Local)
            {
                for (int i = 0; i < spec.Points.Count; i++)
                {
                    spec.Points[i] = InstanceTransform.PointToWorld(frame, spec.Points[i]);
                    if (spec.LookAts[i] != null) spec.LookAts[i] = InstanceTransform.PointToWorld(frame, spec.LookAts[i].Value);
                }
                spec.LookPoint = InstanceTransform.PointToWorld(frame, spec.LookPoint);
            }
            return frame;
        }

        private static string SpaceNote(PathSpec spec, cTransform frame, string where)
        {
            if (frame == null) return "composite (keys as given, in " + where + ")";
            return (spec.Local ? "points given in " + where + "; " : "points given in world space; ") + "keys written in " + where;
        }

        private static object AnimateCameraPath(McpCall call)
        {
            PathSpec spec = ReadPath(call);
            int? placement = call.Has("placement") ? call.Int("placement") : (int?)null;
            if (call.Has("length") && call.Num("length") <= 0) throw new McpError("'length' is seconds, more than 0.");

            //Where the camera's position is written, in the world
            cTransform frame = null;
            JObject frameDescribed = null;
            string targetName = null;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                CAGEAnimation animation = FindAnimation(commands, composite, call.Str("entity", required: true));
                McpCageAnimationTools.Target target = call.Has("camera")
                    ? McpCageAnimationTools.ResolveTarget(commands, composite, call.Token("camera"), call.Bool("from_root"), "camera")
                    : DefaultCamera(commands, composite, animation);
                targetName = target.Name;
                frame = FrameFor(spec, () => McpCageAnimationTools.TargetFrame(commands, composite, target.Path, placement, "'placement'", out frameDescribed));
            });
            JArray moved = frame != null ? KeepClear(call, spec, spec.Clearance) : new JArray();
            BuiltTrack built = Build(spec, frame);
            JObject check = frame != null ? CheckPath(call, built.Samples, spec.Collision) : new JObject() { ["skipped"] = spec.Unplaced };

            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                CAGEAnimation animation = FindAnimation(commands, composite, call.Str("entity", required: true));
                McpCageAnimationTools.Target target = call.Has("camera")
                    ? McpCageAnimationTools.ResolveTarget(commands, composite, call.Token("camera"), call.Bool("from_root"), "camera")
                    : DefaultCamera(commands, composite, animation);
                McpCageAnimationTools.Working w = McpCageAnimationTools.BeginOn(commands, composite, animation);
                if (w.Mode != CAGEAnimation.InterpolationMode.Bezier)
                    spec.Notes.Add("The animation plays linear (its keys' interpolation), so the path is drawn straight between keys: animate_parameters interpolation 'bezier' makes it the smooth curve built here.");
                bool replace = call.Bool("replace", true);
                //anim_length that ended at the last key or event (no hold after it, as create_camera_animation leaves one) follows the new keys
                bool endedAtLastKey = Math.Abs(w.OriginalLength - McpCageAnimationTools.LatestOf(w)) < CageAnimationCurves.TimeEpsilon;
                string change = McpCageAnimationTools.WriteTransformKeys(w, target, built.Keys, replace);
                w.Changes.Add(change);
                //Otherwise it is kept (a hold at the end stays), or lengthened to the end of the move or the last other key or event
                float latest = Math.Max(built.End, McpCageAnimationTools.LatestOf(w));
                if (call.Has("length"))
                    w.Length = (float)call.Num("length");
                else if (!w.Fresh)
                    w.Length = replace && endedAtLastKey ? latest : Math.Max(w.OriginalLength, latest);
                bool changed = w.Commit("Camera path for " + target.Name);
                if (changed && !w.Fresh && Math.Abs(w.FinalLength - w.OriginalLength) > 0.001f)
                    w.Changes.Add("anim_length: " + Math.Round(w.OriginalLength, 3) + " -> " + Math.Round(w.FinalLength, 3) + " s" +
                        (w.FinalLength < w.OriginalLength && !call.Has("length") ? " (it ended at the last key, so it follows the new move; pass 'length' to keep it)" : ""));
                if (changed) McpScriptEdit.Show(composite, new List<Entity>() { animation });
                JObject result = new JObject()
                {
                    ["changed"] = changed,
                    ["animation"] = McpScript.EntityName(commands, composite, animation),
                    ["camera"] = target.Name,
                    ["space"] = SpaceNote(spec, frame, "the space " + target.Name + "'s position is written in"),
                    ["length"] = Math.Round(w.FinalLength, 3),
                    ["track"] = built.Summary,
                };
                if (frameDescribed != null) result["frame"] = frameDescribed;
                if (spec.Room != null) result["room"] = spec.Room;
                if (moved.Count != 0) result["points_moved"] = moved;
                result["path_check"] = check;
                if (built.Warnings.Count != 0) result["warnings"] = new JArray(built.Warnings);
                if (changed && w.Changes.Count != 0) result["changes"] = new JArray(w.Changes);
                result["will_it_play"] = Brief(McpCageAnimationTools.Check(commands, composite, animation));
                foreach (string note in spec.Notes.Concat(w.Notes)) call.Note(note);
                NoteMoved(call, moved, spec.Clearance);
                if (check["clear"]?.Type == JTokenType.Boolean && !(bool)check["clear"])
                    call.Note("The path passes through collision (path_check.passes_through): move those points, give the room a larger inset, or raise 'height'.");
                return result;
            });
        }

        private static void NoteMoved(McpCall call, JArray moved, float clearance)
        {
            if (moved.Count == 0) return;
            call.Note(moved.Count + " of the points " + (moved.Count == 1 ? "was" : "were") + " moved (up to " + moved.Max(o => (double)o["by_m"]) + " m) to keep the camera " + clearance + " m off the collision: points_moved lists them. Pass clearance: 0 to keep the points exactly as given.");
        }

        private static CAGEAnimation FindAnimation(Commands commands, Composite composite, string reference)
        {
            Entity entity = McpScript.FindEntity(commands, composite, reference);
            if (entity is CAGEAnimation animation) return animation;
            throw new McpError(McpScript.EntityName(commands, composite, entity) + " is a " + McpScript.TypeName(commands, composite, entity) + ", not a CAGEAnimation. find_cage_animations lists the level's CAGEAnimations; create_camera_animation makes a camera and its animation together.");
        }

        /// <summary>check_cage_animation's verdict, its problems and notes, without the rest.</summary>
        private static JObject Brief(JObject check)
        {
            JObject brief = new JObject() { ["verdict"] = check["verdict"] };
            if (check["problems"] is JArray problems && problems.Count != 0) brief["problems"] = problems;
            if (check["notes"] is JArray notes && notes.Count != 0) brief["notes"] = notes;
            if (check["started_by"] != null) brief["started_by"] = check["started_by"];
            if (check["cameras"] != null) brief["cameras"] = check["cameras"];
            if (check["placement"] != null) brief["placement"] = check["placement"];
            return brief;
        }

        private static object CreateCameraAnimation(McpCall call)
        {
            string name = (call.Str("name") ?? "FlyCam").Trim();
            if (name.Length == 0) throw new McpError("'name' is empty.");
            string start = (call.Str("start") ?? "level_start").Trim();
            string end = (call.Str("end") ?? "return").Trim().ToLowerInvariant();
            if (end != "return" && end != "loop" && end != "hold") throw new McpError("'end' is return, loop or hold.");
            float fov = Finite(call, "fov", 45, 1, 170, "degrees, 1-170");
            float blendIn = Finite(call, "blend_in", 0, 0, 60, "seconds, 0 or more");
            float blendOut = Finite(call, "blend_out", 1, 0, 60, "seconds, 0 or more");
            if (call.Has("blend_out") && end != "return") throw new McpError("'blend_out' is the blend back to the player at the end, for end 'return'.");
            PathSpec spec = ReadPath(call);
            if (end == "loop" && !spec.Closed)
                spec.Notes.Add("The path is open but the animation loops: the camera jumps back to the start each lap. A closed path (closed: true) joins smoothly.");
            int? placement = call.Has("placement") ? call.Int("placement") : (int?)null;

            //The composite's frame in the world: a camera made in it has its position written there
            cTransform frame = null;
            JObject frameDescribed = null;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : commands.EntryPoints[0];
                if (call.Has("existing_camera"))
                {
                    McpCageAnimationTools.Target camera = McpCageAnimationTools.ResolveTarget(commands, composite, call.Token("existing_camera"), false, "existing_camera");
                    if (McpCageAnimationTools.KindOf(camera.Entity) != "CameraResource")
                        throw new McpError("'existing_camera': " + camera.Name + " is a " + McpScript.TypeName(commands, camera.Composite, camera.Entity) + ", not a CameraResource.");
                    frame = FrameFor(spec, () => McpCageAnimationTools.TargetFrame(commands, composite, camera.Path, placement, "'placement'", out frameDescribed));
                }
                else
                    frame = FrameFor(spec, () => McpSpatialTools.FrameOf(commands, composite, placement, "'placement'", out frameDescribed));
            });
            JArray moved = frame != null ? KeepClear(call, spec, spec.Clearance) : new JArray();
            BuiltTrack built = Build(spec, frame);
            JObject check = frame != null ? CheckPath(call, built.Samples, spec.Collision) : new JObject() { ["skipped"] = spec.Unplaced };

            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : commands.EntryPoints[0];
                Entity startFrom = null;
                string startPin = null;
                if (!string.Equals(start, "level_start", StringComparison.OrdinalIgnoreCase) && !string.Equals(start, "none", StringComparison.OrdinalIgnoreCase))
                {
                    int dot = start.LastIndexOf('.');
                    if (dot <= 0 || dot == start.Length - 1)
                        throw new McpError("'start' is level_start, none, or 'Entity.pin' (an output of an entity in " + composite.name + ", e.g. 'PlayerTriggerBox_1.on_entered').");
                    startFrom = McpScript.FindEntity(commands, composite, start.Substring(0, dot));
                    startPin = start.Substring(dot + 1).Trim();
                    //Its outputs: its own pins, the relays its methods fire, and the pins its data makes (an animation's events)
                    HashSet<ShortGuid> pins = new HashSet<ShortGuid>(commands.Utils.GetAllParameters(startFrom, composite).Select(o => o.Item1));
                    foreach (ShortGuid relay in NodeUtils.GetMethodRelays(startFrom, composite, commands).Keys) pins.Add(relay);
                    foreach (ShortGuid pin in NodeUtils.GetDynamicPinParameters(startFrom, composite, commands)) pins.Add(pin);
                    if (!pins.Contains(McpScript.ParamId(startPin)))
                        throw McpError.NotFound("output of " + McpScript.EntityName(commands, composite, startFrom) + " (" + McpScript.TypeName(commands, composite, startFrom) + ")", startPin, pins.Select(McpScript.ParamName),
                            "describe_entity lists its pins; 'start' is 'Entity.pin', e.g. a trigger's relay such as 'triggered' or a PlayerTriggerBox's 'on_entered'.");
                }

                object mark = UndoStack.Current.Mark();
                FunctionEntity camera = null, animation = null, trigger = null;
                List<string> links = new List<string>();
                string label = "Create camera animation " + name;
                McpScriptEdit.Run(label, composite, edit =>
                {
                    if (call.Has("existing_camera"))
                    {
                        McpCageAnimationTools.Target existing = McpCageAnimationTools.ResolveTarget(commands, composite, call.Token("existing_camera"), false, "existing_camera");
                        if (existing.Composite != composite)
                            throw new McpError("'existing_camera' must be in " + composite.name + " itself, where its links are made (" + existing.Name + " is in " + existing.Composite.name + ").");
                        camera = (FunctionEntity)existing.Entity;
                    }
                    else
                    {
                        camera = edit.AddFunction(composite, FunctionType.CameraResource, name);
                        edit.SetParameter(composite, camera, "camera_name", new JValue(name));
                        //At rest it stands where the move starts
                        edit.SetParameter(composite, camera, "position", new JObject() { ["position"] = McpValues.Vector(built.FirstKey.position), ["rotation"] = McpValues.Vector(built.FirstKey.rotation) });
                    }
                    //A camera of the level's keeps its own settings unless they are given
                    bool made = !call.Has("existing_camera");
                    if (made || call.Has("fov")) edit.SetParameter(composite, camera, "fov", new JValue(fov));
                    if (made || call.Has("blend_in"))
                    {
                        edit.SetParameter(composite, camera, "enable_enter_transition", new JValue(blendIn > 0f));
                        if (blendIn > 0f) edit.SetParameter(composite, camera, "transition_duration", new JValue(blendIn));
                    }
                    if (end == "return" && (made || call.Has("blend_out")))
                    {
                        edit.SetParameter(composite, camera, "enable_exit_transition", new JValue(blendOut > 0f));
                        if (blendOut > 0f) edit.SetParameter(composite, camera, "exit_transition_duration", new JValue(blendOut));
                    }
                    animation = edit.AddFunction(composite, FunctionType.CAGEAnimation, name + "_Anim");

                    if (string.Equals(start, "level_start", StringComparison.OrdinalIgnoreCase))
                    {
                        trigger = edit.AddFunction(composite, FunctionType.ZoneLoaded, name + "_Start");
                        startFrom = trigger;
                        startPin = "on_loaded";
                    }
                    if (startFrom != null)
                    {
                        Link(edit, composite, startFrom, startPin, camera, "activate_camera", links, commands);
                        Link(edit, composite, startFrom, startPin, animation, "start", links, commands);
                    }
                    if (end == "return") Link(edit, composite, animation, "animation_finished", camera, "deactivate_camera", links, commands);
                    else if (end == "loop") Link(edit, composite, animation, "animation_finished", animation, "start", links, commands);
                    edit.Select.Add(camera);
                    edit.Select.Add(animation);
                });

                //The track, as a CAGEAnimation edit on the animation just made - folded into the same undo step
                CAGEAnimation anim = (CAGEAnimation)animation;
                McpCageAnimationTools.Working w = McpCageAnimationTools.BeginOn(commands, composite, anim);
                McpCageAnimationTools.Target target = McpCageAnimationTools.ResolveTarget(commands, composite, new JArray(McpScript.Id(camera.shortGUID)), false, "camera");
                w.Changes.Add(McpCageAnimationTools.WriteTransformKeys(w, target, built.Keys, true));
                w.Length = built.End;
                w.Commit(label);
                int steps = UndoStack.Current.StepsSince(mark);
                if (steps > 1 && !UndoStack.Current.Collapse(steps, "AI: " + label))
                    call.Note("The entities and the track are two steps on the undo history: undo both to take it back.");
                McpScriptEdit.Show(composite, new List<Entity>() { camera, animation });

                JObject result = new JObject()
                {
                    ["composite"] = composite.name,
                    ["camera"] = McpScript.Brief(commands, composite, camera),
                    ["animation"] = new JObject() { ["id"] = McpScript.Id(animation.shortGUID), ["name"] = McpScript.EntityName(commands, composite, animation), ["length"] = Math.Round(w.FinalLength, 3) },
                };
                if (trigger != null) result["trigger"] = new JObject() { ["id"] = McpScript.Id(trigger.shortGUID), ["name"] = McpScript.EntityName(commands, composite, trigger), ["type"] = "ZoneLoaded", ["fires"] = "on_loaded, when " + composite.name + " has streamed in (for the root composite: as the level loads)" };
                result["links"] = new JArray(links);
                result["space"] = SpaceNote(spec, frame, composite.name + "'s space");
                if (frameDescribed != null) result["frame"] = frameDescribed;
                result["track"] = built.Summary;
                if (spec.Room != null) result["room"] = spec.Room;
                if (moved.Count != 0) result["points_moved"] = moved;
                result["path_check"] = check;
                if (built.Warnings.Count != 0) result["warnings"] = new JArray(built.Warnings);
                result["will_it_play"] = Brief(McpCageAnimationTools.Check(commands, composite, anim));
                result["next"] = "save_level to have it in the game; preview_cage_animation {view: 'camera', times: [...]} shows the shot" + (start.Equals("none", StringComparison.OrdinalIgnoreCase) ? "; link a trigger to " + McpScript.EntityName(commands, composite, camera) + ".activate_camera and " + McpScript.EntityName(commands, composite, animation) + ".start to start it" : "") + ".";
                foreach (string note in spec.Notes.Concat(w.Notes)) call.Note(note);
                NoteMoved(call, moved, spec.Clearance);
                if (check["clear"]?.Type == JTokenType.Boolean && !(bool)check["clear"])
                    call.Note("The path passes through collision (path_check.passes_through): animate_camera_path with other points, a larger inset or another height re-keys it.");
                return result;
            });
        }

        private static void Link(McpScriptEdit edit, Composite composite, Entity from, string pin, Entity to, string toPin, List<string> links, Commands commands)
        {
            edit.AddLink(composite, from, pin, to, toPin);
            links.Add(McpScript.EntityName(commands, composite, from) + "." + pin + " -> " + McpScript.EntityName(commands, composite, to) + "." + toPin);
        }
        #endregion
    }
}
