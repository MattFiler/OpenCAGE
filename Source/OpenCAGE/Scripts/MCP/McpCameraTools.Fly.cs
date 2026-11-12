using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using OpenCAGE.Cinematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// 'fly' on create_camera_animation / animate_camera_path: the move becomes a flight - walked to a speed plan, nose
    /// along the path, banked into turns the way an aircraft is, optionally lagging like a heavy airframe - keyed sparsely (keys only
    /// where the curve needs one, within a tolerance) rather than every key_spacing metres.
    /// </summary>
    internal static partial class McpCameraTools
    {
        private sealed partial class PathSpec
        {
            public FlightSpec Fly;
            public float FlyPositionTolerance = KeyFit.DefaultPositionTolerance;
            public float FlyAngleTolerance = KeyFit.DefaultAngleTolerance;
        }

        private const string FlyHelp = "Fly the path like an aircraft (anything with a position: a camera, a ship's composite instance, a model): the speed follows a plan (speed / duration, per-point speeds, faster in dives), the nose follows the path, and it banks into turns (a coordinated turn: up along the lift it needs, scaled and clamped), " +
            "optionally lagging the manoeuvre like a heavy airframe. Keys are fitted sparsely within a tolerance (keys only where the curve needs one) instead of one every key_spacing metres. 'look' must be along_path. {} flies with the defaults.";

        private static McpSchema.Prop FlyProp()
        {
            JObject properties = new JObject()
            {
                ["bank"] = new JObject() { ["type"] = "boolean", ["description"] = "Bank into turns (default true)." },
                ["max_bank"] = new JObject() { ["type"] = "number", ["description"] = "Degrees the bank never exceeds (default 60)." },
                ["bank_scale"] = new JObject() { ["type"] = "number", ["description"] = "Scale on the physical bank (default 1; 0.5 banks half as much)." },
                ["level"] = new JObject() { ["type"] = "boolean", ["description"] = "Keep the nose level through climbs and dives (default false: it pitches with the path)." },
                ["attitude_lag"] = new JObject() { ["type"] = "number", ["description"] = "Seconds the attitude lags the manoeuvre, like a heavy craft (default 0; ~0.45 suits a heavy aircraft)." },
                ["speeds"] = new JObject() { ["type"] = "array", ["description"] = "Speed (m/s) at each point, null where not set: the speed blends smoothly between them. Scaled to fit 'duration' when that is given." },
                ["climb_trade"] = new JObject() { ["type"] = "number", ["description"] = "m/s gained per metre below the path's mean height (and lost above it): dives fast, climbs slow (default 0; the Banshees used ~0.3)." },
                ["route"] = new JObject() { ["type"] = "string", ["enum"] = new JArray("through", "bspline"), ["description"] = "through (default: passes through every point) or bspline (closed loops: the points are control points the loop rounds, for big sweeping loops)." },
                ["position_tolerance"] = new JObject() { ["type"] = "number", ["description"] = "How far (m) the keys may stray from the flight (default 0.002)." },
                ["rotation_tolerance"] = new JObject() { ["type"] = "number", ["description"] = "How far (degrees) the keys' angles may stray (default 0.02)." },
            };
            return McpSchema.Nested("fly", FlyHelp, new JObject() { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false });
        }

        private static void ReadFly(McpCall call, PathSpec spec)
        {
            if (!call.Has("fly")) return;
            JToken token = call.Token("fly");
            if (token.Type == JTokenType.Boolean)
            {
                if (!(bool)token) return;
                token = new JObject();
            }
            if (!(token is JObject fly)) throw McpError.Invalid("'fly' is an object of flight settings ({} for the defaults) or true.");
            if (call.Has("look") && spec.Look != "along_path")
                throw McpError.Invalid("A flight faces along its path: leave 'look' out (or 'along_path') with 'fly'.");
            if (spec.Look != "along_path")
            {
                spec.Notes.Add("Flying: it faces along its path (a region's look at the centre does not apply).");
                spec.Look = "along_path";
            }
            if (call.Has("key_spacing")) spec.Notes.Add("'key_spacing' does not apply to a flight: its keys are fitted to the tolerance instead.");
            float Number(string name, float fallback, float min, float max)
            {
                if (fly[name] == null || fly[name].Type == JTokenType.Null) return fallback;
                double value = McpValues.ReadDouble(fly[name], "fly." + name);
                if (double.IsNaN(value) || value < min || value > max) throw McpError.Invalid("'fly." + name + "' is " + min + " to " + max + ".");
                return (float)value;
            }
            string[] known = { "bank", "max_bank", "bank_scale", "level", "attitude_lag", "speeds", "climb_trade", "route", "position_tolerance", "rotation_tolerance" };
            foreach (JProperty property in fly.Properties())
                if (!known.Contains(property.Name)) throw McpError.Invalid("'fly' takes " + string.Join(", ", known) + " (not '" + property.Name + "').");
            FlightSpec f = new FlightSpec()
            {
                Points = spec.Points,
                Closed = spec.Closed,
                Route = fly["route"] != null ? (string)fly["route"] : "through",
                Duration = spec.Duration,
                Speed = (float)call.Num("speed", 1),
                ClimbTrade = Number("climb_trade", 0f, -10f, 10f),
                Ease = spec.Ease,
                Bank = fly["bank"] == null || McpValues.ReadBool(fly["bank"], "fly.bank"),
                BankScale = Number("bank_scale", 1f, 0f, 5f),
                MaxBank = Number("max_bank", 60f, 0f, 89f),
                PitchWithPath = !(fly["level"] != null && McpValues.ReadBool(fly["level"], "fly.level")),
                AttitudeLag = Number("attitude_lag", 0f, 0f, 10f),
                Roll = spec.Roll,
                StartTime = spec.StartTime,
            };
            if (f.Speed <= 0) throw McpError.Invalid("'speed' is metres a second, more than 0.");
            if (fly["speeds"] is JArray speeds)
            {
                if (speeds.Count > spec.Points.Count + 1) throw McpError.Invalid("'fly.speeds' gives " + speeds.Count + " speeds for " + spec.Points.Count + " points.");
                f.Speeds = speeds.Select((o, i) => o == null || o.Type == JTokenType.Null ? (float?)null : (float)McpValues.ReadDouble(o, "fly.speeds[" + i + "]")).ToList();
                if (f.Speeds.Any(o => o != null && o.Value <= 0)) throw McpError.Invalid("'fly.speeds' are metres a second, more than 0.");
            }
            spec.FlyPositionTolerance = Number("position_tolerance", KeyFit.DefaultPositionTolerance, 0.0001f, 1f);
            spec.FlyAngleTolerance = Number("rotation_tolerance", KeyFit.DefaultAngleTolerance, 0.001f, 5f);
            spec.Fly = f;
        }

        /// <summary>The flight, its pose fitted to sparse keys in the target's space, and measured as the plain path is. Not on the UI thread.</summary>
        private static BuiltTrack BuildFly(PathSpec spec, cTransform frame)
        {
            FlightSpec f = spec.Fly;
            //The points as they stand now (KeepClear may have moved them since they were read)
            f.Points = spec.Points;
            f.Closed = spec.Closed;
            FlightPath flight;
            try
            {
                flight = FlightPath.Plan(f);
            }
            catch (ArgumentException e)
            {
                throw McpError.Invalid(e.Message);
            }
            bool loop = f.Closed && (f.Ease ?? "none") == "none";
            Func<float, KeyPose> pose = t =>
            {
                KeyPose world = flight.PoseAt(t);
                if (frame == null) return world;
                cTransform local = InstanceTransform.ToLocal(frame, new cTransform(world.Position, world.Rotation));
                return new KeyPose(local.position, local.rotation);
            };
            KeyFit.Report report = new KeyFit.Report();
            Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> keys = KeyFit.FitPose(new List<PoseSpan>() { new PoseSpan() { From = flight.Start, To = flight.End, Pose = pose, DefinedBeyond = loop } },
                spec.FlyPositionTolerance, spec.FlyAngleTolerance, report);
            BuiltTrack built = new BuiltTrack() { Start = flight.Start, End = flight.End, PathLength = flight.Length, Keys = keys };
            KeyPose first = pose(flight.Start);
            built.FirstKey = new cTransform(first.Position, first.Rotation);
            built.KeyCount = keys.Values.Max(o => o.Count);
            Measure(built, frame, spec);
            built.Summary["keys"] = new JObject(keys.Select(o => new JProperty(o.Key, o.Value.Count)));
            built.Summary["keys_total"] = report.Keys;
            built.Summary["flight"] = new JObject()
            {
                ["route"] = f.Route,
                ["length_m"] = Math.Round(flight.Length, 2),
                ["duration_s"] = Math.Round(flight.Duration, 3),
                ["speed_m_per_s"] = new JObject() { ["min"] = Math.Round(flight.MinSpeedSeen, 2), ["max"] = Math.Round(flight.MaxSpeedSeen, 2) },
                ["max_bank_deg"] = Math.Round(flight.MaxBank, 1),
                ["max_bank_at"] = Math.Round(flight.MaxBankAt, 2),
                ["max_load_g"] = Math.Round(flight.MaxLoad, 2),
                ["max_pitch_deg"] = Math.Round(flight.MaxPitch, 1),
                ["attitude_lag_s"] = f.AttitudeLag,
                ["fit"] = new JObject() { ["worst_position_mm"] = Math.Round(report.WorstPosition * 1000, 2), ["worst_rotation_deg"] = Math.Round(report.WorstAngle, 4), ["position_tolerance_m"] = spec.FlyPositionTolerance, ["rotation_tolerance_deg"] = spec.FlyAngleTolerance },
                ["loops_seamlessly"] = loop,
            };
            built.Warnings.AddRange(flight.Warnings);
            return built;
        }
    }
}
