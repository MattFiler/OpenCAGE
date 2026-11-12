using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Cinematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// simplify_keys and ride_along (here), build_cinematic / get_cinematic (McpCinematicTools.Shots.cs) and bake_pose_model
    /// (McpCinematicTools.Pose.cs). Yielded from McpCameraTools.Tools(); the CAGEAnimation tools are offered in the 'animation' toolset.
    /// </summary>
    internal static partial class McpCinematicTools
    {
        private static readonly ShortGuid PositionParam = ShortGuidUtils.Generate("position");

        /// <summary>Set while a window runs a tool, so its undo step reads as the user's own rather than "AI:".</summary>
        public static volatile bool FromWindow;

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "simplify_keys",
                Title = "Simplify keys",
                Description = "Thin a CAGEAnimation's float-track keys to the fewest of its own keys that play within a tolerance of how it plays now (checked 240 times a second as the game plays the curve, and at every old key): a key a frame becomes a key where the curve needs one. " +
                    "Kept keys keep their own tangents where both their neighbours are kept, so untouched stretches play exactly as before; cuts (steps between keys a fraction of a second apart) are kept. Positions in metres, angles in degrees, other values in their own units (default 0.1% of each track's range). " +
                    "One undo step; dry_run reports what it would do. Returns keys before and after per track and the worst difference.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the CAGEAnimation is in.", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true),
                    McpSchema.Nested("targets", "Only the tracks of these targets: a list of entity paths from the animation's composite (ids/names, e.g. [[\"Ship\"], [\"Room\", \"Door\"]]). Default every track.", new JObject() { ["type"] = "array" }),
                    McpSchema.Number("position_tolerance", "How far (m) a simplified x / y / z track may stray (default 0.002)."),
                    McpSchema.Number("rotation_tolerance", "How far (degrees) a simplified Yaw / Pitch / Roll track may stray (default 0.02)."),
                    McpSchema.Number("value_tolerance", "How far any other track may stray, in its own units (default 0.1% of each track's range)."),
                    McpSchema.Boolean("dry_run", "Report what would go without changing anything.")),
                Destructive = true,
                Toolset = "animation",
                Run = SimplifyKeys,
            };

            yield return new McpTool()
            {
                Name = "ride_along",
                Title = "Ride along",
                Description = "Make one entity ride along with another in the same CAGEAnimation: the rider (a model, a camera, an emitter, a posed character model) is keyed with the leader's motion composed with where the rider sits relative to it, so it moves, turns and banks with the leader frame for frame, on the same clock (an attachment link does not move a camera, and an animated body trails a keyed vehicle). " +
                    "offset 'keep' (default) keeps where the rider is now relative to the leader at attach_time; 'none' gives the rider exactly the leader's keys (a rider modelled in the leader's own space - copied key for key); or {position, rotation} in the leader's space. Without an offset the keys are copied exactly; otherwise sampled and fitted within the tolerances. " +
                    "One undo step. Keys on a target inside a composite instance apply in the animation's own composite space (proven in game), so a rider or leader inside instances is keyed there, as the editor's previews show it.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the CAGEAnimation is in.", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true),
                    McpSchema.Nested("rider", "The entity to carry: a path from the animation's composite (ids/names).", PathSchema(), required: true),
                    McpSchema.Nested("leader", "The entity it rides with, already keyed by this animation (its position): a path from the animation's composite.", PathSchema(), required: true),
                    McpSchema.Any("offset", "'keep' (default), 'none', or {position: [x,y,z], rotation: [pitch,yaw,roll]} in the leader's space."),
                    McpSchema.Number("attach_time", "offset 'keep': the moment (s) the rider's place relative to the leader is taken (default the leader's first key)."),
                    McpSchema.Number("position_tolerance", "When fitted: how far (m) the rider's keys may stray (default 0.002)."),
                    McpSchema.Number("rotation_tolerance", "When fitted: how far (degrees) they may stray (default 0.02)."),
                    McpSchema.Boolean("dry_run", "Report the keys it would write without writing them.")),
                Destructive = true,
                Toolset = "animation",
                Run = RideAlong,
            };

            foreach (McpTool tool in ShotTools()) yield return tool;
            foreach (McpTool tool in PoseTools()) yield return tool;
        }

        #region Shared
        //An entity path as every tool takes one (McpScript.PathSteps): an array of ids/names, one string split on '/', or a result's {ids} object
        internal static JObject PathSchema() => new JObject()
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

        internal static CAGEAnimation FindAnimation(Commands commands, Composite composite, string reference)
        {
            Entity entity = McpScript.FindEntity(commands, composite, reference);
            if (entity is CAGEAnimation animation) return animation;
            throw new McpError(McpScript.EntityName(commands, composite, entity) + " is a " + McpScript.TypeName(commands, composite, entity) + ", not a CAGEAnimation. find_cage_animations lists the level's CAGEAnimations.");
        }

        private static float Tolerance(McpCall call, string name, double fallback, double min, double max)
        {
            double value = call.Num(name, fallback);
            if (double.IsNaN(value) || value < min || value > max) throw McpError.Invalid("'" + name + "' is " + min + " to " + max + ".");
            return (float)value;
        }

        /// <summary>The transform component a connection's sub-parameter names (x ... Roll), or "" for none.</summary>
        internal static string ComponentOf(ShortGuid sub)
        {
            if (sub.IsInvalid) return "";
            foreach (string c in KeyFit.TransformComponents)
                if (ShortGuidUtils.Generate(c, false) == sub) return c;
            if (sub == ShortGuidUtils.Generate("", false)) return "";
            return McpScript.ParamName(sub);
        }

        private static string TargetName(Commands commands, Composite composite, EntityPath path)
        {
            try
            {
                (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(path, composite));
                if (e != null) return McpScript.EntityName(commands, c, e);
            }
            catch { }
            return "(a path that does not resolve)";
        }

        /// <summary>The frame of the composite a target's position is written in, relative to the animation's composite (null: the same).</summary>
        internal static cTransform ChainFrame(Commands commands, Composite composite, McpCageAnimationTools.Target target, string what)
        {
            List<Tuple<Composite, Entity>> chain = target.Chain;
            if (chain == null || chain.Count == 0) throw new McpError("'" + what + "' does not resolve from " + composite.name + ".");
            if (chain[0].Item1 != composite) throw new McpError("'" + what + "' must be a path from the animation's composite " + composite.name + " (not from the level's root).");
            List<Entity> through = chain.Take(chain.Count - 1).Select(o => o.Item2).ToList();
            if (through.Count == 0) return null;
            return McpRegion.Walker(commands).Evaluate(composite, through).World;
        }

        private static bool IsIdentity(cTransform t) => t == null || (t.position.LengthSquared() < 1e-10f && t.rotation.LengthSquared() < 1e-8f);

        private static readonly HashSet<FunctionType> Clocks = new HashSet<FunctionType>() { FunctionType.CAGEAnimation, FunctionType.RotateInTime, FunctionType.RotateAtSpeed };

        /// <summary>
        /// Level Check's nested_animation: a composite instance keyed by a CAGEAnimation freezes every animation inside it (CAGEAnimation,
        /// RotateInTime, RotateAtSpeed). A warning when the entity is such an instance, else null.
        /// </summary>
        internal static string NestedClockWarning(Commands commands, Composite composite, Entity entity, string name)
        {
            if (!(entity is FunctionEntity instance) || instance.function.IsFunctionType) return null;
            Composite inner = commands.GetComposite(instance.function);
            if (inner == null || !HoldsClock(commands, inner, new HashSet<Composite>())) return null;
            return name + " is an instance of " + EditorUtils.GetCompositeName(inner) + ", which holds animations of its own (a CAGEAnimation, RotateInTime or RotateAtSpeed): while this animation keys it, those freeze at time 0 (Level Check: nested_animation). Key its moving parts from this animation in its space, or move the inner animation out of it.";
        }

        private static bool HoldsClock(Commands commands, Composite composite, HashSet<Composite> seen)
        {
            if (composite == null || !seen.Add(composite)) return false;
            foreach (FunctionEntity function in composite.functions)
            {
                if (function.function.IsFunctionType)
                {
                    if (Clocks.Contains(function.function.AsFunctionType)) return true;
                    continue;
                }
                if (HoldsClock(commands, commands.GetComposite(function.function), seen)) return true;
            }
            return false;
        }

        /// <summary>Commit a working copy as one undo step, labelled as the user's own when a window asked for it.</summary>
        internal static bool Commit(McpCageAnimationTools.Working w, string label)
        {
            object mark = Undo.UndoStack.Current.Mark();
            bool changed = w.Commit(label);
            if (changed && FromWindow)
                Undo.UndoStack.Current.Collapse(Undo.UndoStack.Current.StepsSince(mark), label);
            return changed;
        }

        internal static JArray V(Vector3 v, int digits = 3) => new JArray(Math.Round(v.X, digits), Math.Round(v.Y, digits), Math.Round(v.Z, digits));
        #endregion

        #region simplify_keys
        private static object SimplifyKeys(McpCall call)
        {
            float position = Tolerance(call, "position_tolerance", KeyFit.DefaultPositionTolerance, 1e-5, 10);
            float angle = Tolerance(call, "rotation_tolerance", KeyFit.DefaultAngleTolerance, 1e-4, 45);
            float? value = call.Has("value_tolerance") ? Tolerance(call, "value_tolerance", 0, 1e-7, 1e6) : (float?)null;
            bool dry = call.Bool("dry_run");
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                CAGEAnimation animation = FindAnimation(commands, composite, call.Str("entity", required: true));
                McpCageAnimationTools.Working w = McpCageAnimationTools.BeginOn(commands, composite, animation);
                bool bezier = w.Mode == CAGEAnimation.InterpolationMode.Bezier;
                List<McpCageAnimationTools.Target> targets = null;
                if (call.Has("targets"))
                {
                    JArray list = call.Array("targets");
                    if (list.Count == 0) throw McpError.Invalid("'targets' is a list of entity paths.");
                    targets = list.Select((o, i) => McpCageAnimationTools.ResolveTarget(commands, composite, o, false, "targets[" + i + "]")).ToList();
                }
                JArray rows = new JArray();
                int before = 0, after = 0, tracks = 0;
                float worstPosition = 0f, worstAngle = 0f;
                foreach (CAGEAnimation.FloatTrack track in w.FloatTracks.ToList())
                {
                    if (track?.keyframes == null || track.keyframes.Count == 0) continue;
                    CAGEAnimation.Connection connection = w.Connections.FirstOrDefault(o => o != null && o.target_track == track.shortGUID);
                    if (connection == null) continue;
                    if (targets != null && !targets.Any(t => McpScriptEdit.SamePath(connection.connectedEntity?.path, t.Path.path))) continue;
                    string component = ComponentOf(connection.target_sub_param);
                    float tolerance;
                    if (KeyFit.TransformComponents.Contains(component))
                        tolerance = KeyFit.ToleranceFor(component, position, angle, 0f);
                    else
                    {
                        float min = track.keyframes.Min(o => o.value.Y), max = track.keyframes.Max(o => o.value.Y);
                        tolerance = value ?? Math.Max(1e-5f, (max - min) * 0.001f);
                    }
                    KeyFit.Simplified s = KeyFit.Simplify(track.keyframes, bezier, tolerance);
                    tracks++;
                    before += s.Before;
                    after += s.After;
                    if (component == "x" || component == "y" || component == "z") worstPosition = Math.Max(worstPosition, s.Worst);
                    else if (KeyFit.IsAngle(component)) worstAngle = Math.Max(worstAngle, s.Worst);
                    rows.Add(new JObject()
                    {
                        ["target"] = TargetName(commands, composite, connection.connectedEntity),
                        ["parameter"] = McpScript.ParamName(connection.target_param) + (component.Length != 0 ? "." + component : ""),
                        ["track"] = McpScript.Id(track.shortGUID),
                        ["keys_before"] = s.Before,
                        ["keys_after"] = s.After,
                        ["worst"] = Math.Round(s.Worst, 6),
                        ["tolerance"] = tolerance,
                    });
                    if (!dry && s.After < s.Before)
                    {
                        track.keyframes = s.Keys;
                        w.Touched.Add(track);
                        w.Changed = true;
                    }
                }
                if (tracks == 0) throw new McpError("No float tracks to simplify" + (targets != null ? " on those targets" : "") + " in " + McpScript.EntityName(commands, composite, animation) + ".");
                bool changed = !dry && w.Changed && Commit(w, "Simplify keys of " + McpScript.EntityName(commands, composite, animation));
                JObject result = new JObject()
                {
                    ["animation"] = McpScript.EntityName(commands, composite, animation),
                    ["changed"] = changed,
                    ["dry_run"] = dry,
                    ["tracks"] = tracks,
                    ["keys_before"] = before,
                    ["keys_after"] = after,
                    ["worst_position_m"] = Math.Round(worstPosition, 6),
                    ["worst_rotation_deg"] = Math.Round(worstAngle, 6),
                    ["interpolation"] = bezier ? "bezier" : "linear",
                    ["per_track"] = new JArray(rows.Take(150)),
                };
                if (rows.Count > 150) result["per_track_total"] = rows.Count;
                return result;
            });
        }
        #endregion

        #region ride_along
        private static object RideAlong(McpCall call)
        {
            float position = Tolerance(call, "position_tolerance", KeyFit.DefaultPositionTolerance, 1e-5, 10);
            float angle = Tolerance(call, "rotation_tolerance", KeyFit.DefaultAngleTolerance, 1e-4, 45);
            bool dry = call.Bool("dry_run");
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                CAGEAnimation animation = FindAnimation(commands, composite, call.Str("entity", required: true));
                McpCageAnimationTools.Working w = McpCageAnimationTools.BeginOn(commands, composite, animation);
                bool bezier = w.Mode == CAGEAnimation.InterpolationMode.Bezier;
                McpCageAnimationTools.Target leader = McpCageAnimationTools.ResolveTarget(commands, composite, call.Token("leader"), false, "leader");
                McpCageAnimationTools.Target rider = McpCageAnimationTools.ResolveTarget(commands, composite, call.Token("rider"), false, "rider");
                if (leader.Entity == rider.Entity && leader.Composite == rider.Composite) throw McpError.Invalid("The rider and the leader are the same entity.");

                //The leader's motion: its six position tracks (a missing one holds its resting value)
                Dictionary<string, CAGEAnimation.FloatTrack> leaderTracks = new Dictionary<string, CAGEAnimation.FloatTrack>();
                foreach (string c in KeyFit.TransformComponents)
                {
                    CAGEAnimation.FloatTrack track = McpCageAnimationTools.FindTrack(w, leader, PositionParam, c);
                    if (track != null && track.keyframes != null && track.keyframes.Count != 0) leaderTracks[c] = track;
                }
                if (leaderTracks.Count == 0)
                    throw new McpError(leader.Name + "'s position is not keyed by " + McpScript.EntityName(commands, composite, animation) + ": key it first (animate_parameters, animate_camera_path with fly), then make things ride along with it.");
                KeyPose leaderRest = Rest(commands, composite, leader);
                KeyPose riderRest = Rest(commands, composite, rider);
                float from = leaderTracks.Values.Min(o => o.keyframes.Min(k => k.time));
                float to = leaderTracks.Values.Max(o => o.keyframes.Max(k => k.time));
                float attach = call.Has("attach_time") ? (float)call.Num("attach_time") : from;

                //Spaces: every key of this animation - on a target however deep inside instances - applies in its own composite's
                //space (CageAnimationSpace), so the leader's and the rider's keys share one space and compose directly
                Func<float, cTransform> leaderInAnimation = t =>
                {
                    KeyPose p = KeyFit.PoseAt(leaderTracks, leaderRest, t, bezier);
                    return new cTransform(p.Position, p.Rotation);
                };

                //Where the rider is now, in the animation's space: its own keys at the attach time if it has any, else its resting place
                Dictionary<string, CAGEAnimation.FloatTrack> riderTracks = new Dictionary<string, CAGEAnimation.FloatTrack>();
                foreach (string c in KeyFit.TransformComponents)
                {
                    CAGEAnimation.FloatTrack track = McpCageAnimationTools.FindTrack(w, rider, PositionParam, c);
                    if (track != null && track.keyframes != null && track.keyframes.Count != 0) riderTracks[c] = track;
                }
                cTransform riderNow;
                if (riderTracks.Count != 0)
                {
                    KeyPose p = KeyFit.PoseAt(riderTracks, riderRest, attach, bezier);
                    riderNow = new cTransform(p.Position, p.Rotation);
                }
                else
                    riderNow = new cTransform(riderRest.Position, riderRest.Rotation);

                cTransform offset;
                string offsetMode;
                JToken offsetToken = call.Token("offset");
                if (offsetToken == null || (offsetToken.Type == JTokenType.String && string.Equals(((string)offsetToken).Trim(), "keep", StringComparison.OrdinalIgnoreCase)))
                {
                    offset = InstanceTransform.ToLocal(leaderInAnimation(attach), riderNow);
                    offsetMode = "keep (where it is relative to " + leader.Name + " at " + Math.Round(attach, 3) + " s)";
                }
                else if (offsetToken.Type == JTokenType.String && string.Equals(((string)offsetToken).Trim(), "none", StringComparison.OrdinalIgnoreCase))
                {
                    offset = new cTransform(Vector3.Zero, Vector3.Zero);
                    offsetMode = "none (the leader's own pose)";
                }
                else if (offsetToken is JObject o)
                {
                    offset = McpValues.ReadTransform(o, "offset", new cTransform(Vector3.Zero, Vector3.Zero));
                    offsetMode = "given";
                }
                else throw McpError.Invalid("'offset' is 'keep', 'none' or {position, rotation}.");
                if (Math.Abs(offset.position.X) < 1e-6f && Math.Abs(offset.position.Y) < 1e-6f && Math.Abs(offset.position.Z) < 1e-6f
                    && Math.Abs(offset.rotation.X) < 1e-5f && Math.Abs(offset.rotation.Y) < 1e-5f && Math.Abs(offset.rotation.Z) < 1e-5f)
                    offset = new cTransform(Vector3.Zero, Vector3.Zero);

                //The rider's keys: the leader's own when nothing lies between (no offset, the same key space), else sampled and fitted
                Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> keys;
                bool copied = IsIdentity(offset);
                KeyFit.Report report = new KeyFit.Report();
                if (copied)
                {
                    keys = new Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>>();
                    foreach (string c in KeyFit.TransformComponents)
                    {
                        if (leaderTracks.TryGetValue(c, out CAGEAnimation.FloatTrack track))
                            keys[c] = track.keyframes.OrderBy(k => k.time).Select(KeyFit.Clone).ToList();
                        else
                            keys[c] = new List<CAGEAnimation.FloatTrack.Keyframe>() { CageAnimationCurves.NewKeyframe(from, KeyFit.Component(leaderRest, c), w.Mode) };
                        report.Keys += keys[c].Count;
                    }
                }
                else
                {
                    List<(float, float)> steps = KeyFit.Steps(leaderTracks.Values);
                    List<PoseSpan> spans = KeyFit.SplitAtSteps(from, to, steps).Select(s => new PoseSpan()
                    {
                        From = s.Item1,
                        To = s.Item2,
                        Pose = t =>
                        {
                            cTransform key = InstanceTransform.Compose(leaderInAnimation(t), offset);
                            return new KeyPose(key.position, key.rotation);
                        },
                    }).ToList();
                    keys = KeyFit.FitPose(spans, position, angle, report);
                }

                List<string> warnings = new List<string>();
                string frozen = NestedClockWarning(commands, rider.Composite, rider.Entity, rider.Name);
                if (frozen != null) warnings.Add(frozen);
                bool changed = false;
                if (!dry)
                {
                    McpCageAnimationTools.WriteTransformKeys(w, rider, keys, true);
                    changed = Commit(w, rider.Name + " rides along with " + leader.Name);
                }
                JObject result = new JObject()
                {
                    ["animation"] = McpScript.EntityName(commands, composite, animation),
                    ["rider"] = rider.Name,
                    ["leader"] = leader.Name,
                    ["changed"] = changed,
                    ["dry_run"] = dry,
                    ["how"] = copied ? "copied: the leader's keys, key for key (exact)" : "fitted: the leader's motion composed with the offset, sampled 240 times a second and keyed within the tolerance",
                    ["offset"] = new JObject() { ["mode"] = offsetMode, ["position"] = V(offset.position), ["rotation"] = V(offset.rotation) },
                    ["span"] = new JArray(Math.Round(from, 3), Math.Round(to, 3)),
                    ["keys"] = new JObject(keys.Select(o => new JProperty(o.Key, o.Value.Count))),
                };
                if (!copied) result["fit"] = new JObject() { ["worst_position_mm"] = Math.Round(report.WorstPosition * 1000, 3), ["worst_rotation_deg"] = Math.Round(report.WorstAngle, 4) };
                if (warnings.Count != 0) result["warnings"] = new JArray(warnings);
                return result;
            });
        }

        /// <summary>Where a target's position rests, in its keys' space (the animation's composite for a target inside instances; zero when it has none).</summary>
        private static KeyPose Rest(Commands commands, Composite composite, McpCageAnimationTools.Target target)
        {
            ParameterData content = McpCageAnimationTools.RestingContent(commands, target.Composite, target.Entity, PositionParam);
            if (content is cTransform rest)
            {
                cTransform t = CageAnimationSpace.RestingKey(commands, composite, target.Path, PositionParam, rest);
                return new KeyPose(t.position, t.rotation);
            }
            return new KeyPose(Vector3.Zero, Vector3.Zero);
        }
        #endregion
    }
}
