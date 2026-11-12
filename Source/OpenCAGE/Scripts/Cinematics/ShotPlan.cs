using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Cinematics
{
    /// <summary>A named moment inside a shot (seconds from the shot's start): an output pin on the cinematic's timeline.</summary>
    public sealed class ShotEvent
    {
        public float Time;
        public string Name;
    }

    /// <summary>
    /// One shot of a cinematic. Points are in the space the cinematic is built in (world, or its composite's space);
    /// the camera of a shot is its own CameraResource.
    /// </summary>
    public sealed class ShotSpec
    {
        public string Name;
        public float Duration = 3f;
        /// <summary>static (held at position), dolly (position -> to), path (through points), orbit (round a centre).</summary>
        public string Move = "static";
        public Vector3 Position;
        public Vector3? To;
        public List<Vector3> Path;
        public Vector3 OrbitCentre;
        public float OrbitRadius = 5f;
        /// <summary>Orbit angles: the camera's bearing from the centre in degrees (0 = on the centre's +Z side), from and to.</summary>
        public float OrbitFrom, OrbitTo = 90f;
        /// <summary>Orbit: the camera's height above the centre.</summary>
        public float OrbitHeight = 1.5f;
        /// <summary>A path move banks into its turns (as a flight).</summary>
        public bool Bank;
        /// <summary>Where the camera looks (null: along the move, or at the orbit's centre, or 'Rotation').</summary>
        public Vector3? LookAt;
        /// <summary>Pan: the look point moves from LookAt to this over the shot.</summary>
        public Vector3? LookTo;
        /// <summary>A fixed rotation [pitch, yaw, roll] when nothing is looked at.</summary>
        public Vector3? Rotation;
        public float Roll;
        /// <summary>none, in, out or in_out: how a move (dolly, path, orbit, pan, zoom, rack focus) starts and stops.</summary>
        public string Ease = "in_out";
        /// <summary>Handheld: degrees of slow wander added to the view (0 = locked off).</summary>
        public float Shake;
        public float Fov = 45f;
        /// <summary>Zoom: the fov at the shot's end (null: no zoom).</summary>
        public float? FovTo;
        /// <summary>MACRO, CLOSE, MID, WIDE or ULTRA (near/far planes .001/10, .02/100, .05/1000, .25/5000, 1/10000 m).</summary>
        public string ClipPlanes = "MID";
        /// <summary>The point in focus (null: the look point; then the focus distance).</summary>
        public Vector3? FocusAt;
        /// <summary>Rack focus: the focus moves from FocusAt to this over the shot.</summary>
        public Vector3? FocusTo;
        /// <summary>A fixed focus distance (m), when no point is given.</summary>
        public float? FocusDistance;
        public float FNum = 4f;
        /// <summary>The depth-of-field lens (mm); null: the focal length that gives the shot's fov on a 24 mm-high frame.</summary>
        public float? FocalLength;
        public List<ShotEvent> Events = new List<ShotEvent>();
    }

    /// <summary>The camera, lens and focus of each shot over time, and the cinematic's timeline. Pure maths: any thread.</summary>
    public sealed class CinematicPlan
    {
        /// <summary>After a cut, how long the old camera stays active before it is released (activate new, then deactivate old).</summary>
        public const float ReleaseDelay = 0.05f;
        /// <summary>Depth-of-field values change this long after an event cut: a camera an event brings up shows 1-2 frames late.</summary>
        public const float DofLag = 0.035f;

        public readonly List<ShotSpec> Shots;
        public readonly float[] Starts;
        public readonly float Length;
        private readonly FlightPath[] _paths;

        public CinematicPlan(List<ShotSpec> shots)
        {
            if (shots == null || shots.Count == 0) throw new ArgumentException("A cinematic needs at least one shot.");
            Shots = shots;
            Starts = new float[shots.Count];
            _paths = new FlightPath[shots.Count];
            float t = 0f;
            for (int i = 0; i < shots.Count; i++)
            {
                ShotSpec s = shots[i];
                if (!(s.Duration >= 0.1f)) throw new ArgumentException("shot " + (i + 1) + ": duration is at least 0.1 s.");
                Starts[i] = t;
                t += s.Duration;
                if (s.Move == "path")
                {
                    if (s.Path == null || s.Path.Count < 2) throw new ArgumentException("shot " + (i + 1) + ": a path move needs at least 2 points.");
                    _paths[i] = FlightPath.Plan(new FlightSpec()
                    {
                        Points = s.Path,
                        Duration = s.Duration,
                        Ease = s.Ease,
                        Bank = s.Bank,
                        MaxBank = 35f,
                        Speed = 1f,
                    });
                }
            }
            Length = t;
        }

        public int ShotAt(float time)
        {
            for (int i = Shots.Count - 1; i >= 0; i--)
                if (time >= Starts[i] - 1e-5f) return i;
            return 0;
        }

        #region Easing
        /// <summary>0-1 through a move a fraction x through its shot.</summary>
        public static float Eased(string ease, float x)
        {
            x = Math.Max(0f, Math.Min(1f, x));
            switch ((ease ?? "in_out").ToLowerInvariant())
            {
                case "none": return x;
                case "in": return EaseIn(x);
                case "out": return 1f - EaseIn(1f - x);
                default: return x * x * x * (x * (6f * x - 15f) + 10f);
            }
        }

        /// <summary>From rest into a steady speed (a smootherstep's first half stretched over the move, joined to a line).</summary>
        private static float EaseIn(float x)
        {
            //Speed rises along a smoothstep over the first half, then holds: v(x) = vmax * smooth(x / 0.5) ... integrated and normalised
            const float r = 0.5f;
            float vmax = 1f / (1f - r / 2f);
            if (x < r)
            {
                float y = x / r;
                return vmax * r * (y * y * y - 0.5f * y * y * y * y);
            }
            return vmax * (r / 2f + x - r);
        }
        #endregion

        #region One shot
        /// <summary>Where shot <paramref name="i"/>'s camera is <paramref name="local"/> seconds into the shot (clamped to it).</summary>
        public KeyPose CameraPose(int i, float local)
        {
            ShotSpec s = Shots[i];
            float x = s.Duration <= 0 ? 0f : Math.Max(0f, Math.Min(1f, local / s.Duration));
            float e = Eased(s.Ease, x);
            Vector3 position;
            Vector3? along = null;
            Vector3? flightAttitude = null;
            switch (s.Move)
            {
                case "dolly":
                    {
                        Vector3 to = s.To ?? s.Position;
                        position = Vector3.Lerp(s.Position, to, e);
                        if ((to - s.Position).LengthSquared() > 1e-8f) along = to - s.Position;
                        break;
                    }
                case "path":
                    {
                        FlightPath path = _paths[i];
                        float t = Math.Max(0f, Math.Min(s.Duration, local));
                        position = path.PositionAt(t);
                        along = path.TangentAt(path.DistanceAt(t));
                        if (s.Bank) flightAttitude = path.AttitudeAt(t);
                        break;
                    }
                case "orbit":
                    {
                        float angle = (s.OrbitFrom + (s.OrbitTo - s.OrbitFrom) * e) * (float)Math.PI / 180f;
                        position = s.OrbitCentre + new Vector3((float)Math.Sin(angle) * s.OrbitRadius, s.OrbitHeight, (float)Math.Cos(angle) * s.OrbitRadius);
                        break;
                    }
                default:
                    position = s.Position;
                    break;
            }

            Vector3 rotation;
            Vector3? look = LookPoint(s, e);
            if (look != null && (look.Value - position).LengthSquared() > 1e-6f)
                rotation = InstanceTransform.LookRotation(look.Value - position);
            else if (s.Move == "orbit")
                rotation = InstanceTransform.LookRotation(s.OrbitCentre - position);
            else if (flightAttitude != null)
                rotation = flightAttitude.Value;
            else if (s.Rotation != null)
                rotation = s.Rotation.Value;
            else if (along != null)
                rotation = InstanceTransform.LookRotation(along.Value);
            else
                rotation = Vector3.Zero;
            rotation = new Vector3(rotation.X, rotation.Y, rotation.Z + s.Roll);
            if (s.Shake > 0f) rotation += Shake(i, local) * s.Shake;
            return new KeyPose(position, rotation);
        }

        /// <summary>The point a shot looks at, a fraction e (eased) through it; null when it looks at nothing.</summary>
        private static Vector3? LookPoint(ShotSpec s, float e)
        {
            if (s.LookAt == null) return null;
            if (s.LookTo == null) return s.LookAt;
            return Vector3.Lerp(s.LookAt.Value, s.LookTo.Value, e);
        }

        /// <summary>Handheld wander: three slow incommensurate sines per axis (degrees per unit of shake), different for every shot.</summary>
        private static Vector3 Shake(int shot, float t)
        {
            double k = shot * 1.618;
            float pitch = (float)(0.55 * Math.Sin(t * 1.7 + k) + 0.3 * Math.Sin(t * 3.1 + 2 * k) + 0.15 * Math.Sin(t * 6.3 + 0.5));
            float yaw = (float)(0.6 * Math.Sin(t * 1.3 + 1.1 + k) + 0.28 * Math.Sin(t * 2.9 + 0.7 * k) + 0.12 * Math.Sin(t * 5.7 + 1.9));
            float roll = (float)(0.35 * Math.Sin(t * 0.9 + 0.4 + k) + 0.15 * Math.Sin(t * 2.3 + k));
            return new Vector3(pitch, yaw, roll);
        }

        public float FovAt(int i, float local)
        {
            ShotSpec s = Shots[i];
            if (s.FovTo == null) return s.Fov;
            float x = s.Duration <= 0 ? 0f : local / s.Duration;
            return s.Fov + (s.FovTo.Value - s.Fov) * Eased(s.Ease, x);
        }

        public bool Zooms(int i) => Shots[i].FovTo != null && Math.Abs(Shots[i].FovTo.Value - Shots[i].Fov) > 1e-3f;

        /// <summary>The focal length (mm) that gives a vertical fov on a 24 mm-high frame (a 35 mm camera's).</summary>
        public static float FocalLengthFor(float fovDegrees)
        {
            double half = Math.Max(0.5, Math.Min(170, fovDegrees)) * Math.PI / 360.0;
            return (float)(12.0 / Math.Tan(half));
        }

        /// <summary>Distance from the camera to what is in focus, <paramref name="local"/> seconds into shot i.</summary>
        public float FocusDistanceAt(int i, float local)
        {
            ShotSpec s = Shots[i];
            float x = s.Duration <= 0 ? 0f : local / s.Duration;
            float e = Eased(s.Ease, x);
            Vector3 camera = CameraPose(i, local).Position;
            Vector3? point = s.FocusAt;
            if (point != null && s.FocusTo != null) point = Vector3.Lerp(s.FocusAt.Value, s.FocusTo.Value, e);
            if (point == null && s.FocusDistance == null) point = LookPoint(s, e) ?? (s.Move == "orbit" ? s.OrbitCentre : (Vector3?)null);
            if (point != null) return Math.Max(0.1f, Vector3.Distance(camera, point.Value));
            return Math.Max(0.1f, s.FocusDistance ?? 10f);
        }

        public float FocalLengthAt(int i, float local) => Shots[i].FocalLength ?? FocalLengthFor(FovAt(i, local));
        #endregion

        #region Keys
        /// <summary>
        /// Shot i's camera keys, over its own shot only (before it the camera holds its first pose, after it its last - so when an event
        /// brings it up a frame or two late, that frame is already right). <paramref name="toKeySpace"/> takes a pose from the space the
        /// shot list is in into the space the camera's keys are written in (null: the same).
        /// </summary>
        public Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> CameraKeys(int i, Func<KeyPose, KeyPose> toKeySpace, float positionTolerance, float angleTolerance, KeyFit.Report report)
        {
            float start = Starts[i], end = Starts[i] + Shots[i].Duration;
            bool moves = Shots[i].Move != "static" || Shots[i].LookTo != null || Shots[i].Shake > 0f;
            Func<float, KeyPose> pose = t =>
            {
                KeyPose p = CameraPose(i, t - start);
                return toKeySpace == null ? p : toKeySpace(p);
            };
            if (!moves)
            {
                //A held shot: one key per track
                KeyPose p = pose(start);
                Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> held = new Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>>();
                foreach (string c in KeyFit.TransformComponents)
                {
                    CAGEAnimation.FloatTrack.Keyframe key = CageAnimationCurves.NewKeyframe(start, KeyFit.Component(p, c), CAGEAnimation.InterpolationMode.Bezier);
                    key.tan_in = new Vector2(1f, 0f);
                    key.tan_out = new Vector2(1f, 0f);
                    held[c] = new List<CAGEAnimation.FloatTrack.Keyframe>() { key };
                }
                if (report != null) { report.Keys += 6; report.Tracks += 6; }
                return held;
            }
            return KeyFit.FitPose(new List<PoseSpan>() { new PoseSpan() { From = start, To = end, Pose = pose } }, positionTolerance, angleTolerance, report);
        }

        /// <summary>Shot i's fov over its shot (for the VariableFloat its camera reads), or null when it does not zoom.</summary>
        public List<CAGEAnimation.FloatTrack.Keyframe> FovKeys(int i, KeyFit.Report report)
        {
            if (!Zooms(i)) return null;
            float start = Starts[i];
            return KeyFit.FitValue(new List<ValueSpan>() { new ValueSpan() { From = start, To = start + Shots[i].Duration, Value = t => FovAt(i, t - start) } }, 0.01f, report);
        }

        /// <summary>
        /// The depth-of-field values over the whole cinematic: focal_plane_m, focal_length_mm, fnum and intensity (1: a blend weight, the
        /// lens makes the blur). Each shot's values start <see cref="DofLag"/> after its cut (the old shot's are held into the new shot's
        /// first frame or two rather than shown on the old shot's last), stepping in 0.2 ms.
        /// </summary>
        public Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> DofKeys(KeyFit.Report report)
        {
            List<ValueSpan> focus = new List<ValueSpan>(), focal = new List<ValueSpan>(), fnum = new List<ValueSpan>();
            for (int i = 0; i < Shots.Count; i++)
            {
                int shot = i;
                float from = i == 0 ? 0f : Starts[i] + DofLag;
                float to = i == Shots.Count - 1 ? Length : Starts[i + 1] + DofLag - KeyFit.CutGap;
                float start = Starts[i];
                Func<float, float> local = t => Math.Max(0f, Math.Min(Shots[shot].Duration, t - start));
                focus.Add(new ValueSpan() { From = from, To = to, Value = t => FocusDistanceAt(shot, local(t)) });
                focal.Add(new ValueSpan() { From = from, To = to, Value = t => FocalLengthAt(shot, local(t)) });
                fnum.Add(new ValueSpan() { From = from, To = to, Value = t => Shots[shot].FNum });
            }
            Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> keys = new Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>>()
            {
                ["focal_plane_m"] = KeyFit.FitValue(focus, 0.01f, report),
                ["focal_length_mm"] = KeyFit.FitValue(focal, 0.05f, report),
                ["fnum"] = KeyFit.FitValue(fnum, 0.01f, report),
            };
            CAGEAnimation.FloatTrack.Keyframe one = CageAnimationCurves.NewKeyframe(0f, 1f, CAGEAnimation.InterpolationMode.Bezier);
            keys["intensity"] = new List<CAGEAnimation.FloatTrack.Keyframe>() { one };
            return keys;
        }

        /// <summary>The cut events: cut_NN at each later shot's start (brings its camera up) and release_NN 0.05 s later (lets the one before go).</summary>
        public List<(float time, string name)> CutEvents()
        {
            List<(float, string)> events = new List<(float, string)>();
            for (int i = 1; i < Shots.Count; i++)
            {
                events.Add((Starts[i], CutName(i)));
                events.Add((Starts[i] + ReleaseDelay, ReleaseName(i)));
            }
            return events;
        }

        public static string CutName(int shot) => "cut_" + (shot + 1).ToString("00");
        public static string ReleaseName(int shot) => "release_" + shot.ToString("00");
        #endregion
    }
}
