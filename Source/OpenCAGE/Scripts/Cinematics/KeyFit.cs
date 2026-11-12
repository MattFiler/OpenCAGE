using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Cinematics
{
    /// <summary>A pose as an entity's transform is written: position in metres, rotation [pitch, yaw, roll] in degrees.</summary>
    public struct KeyPose
    {
        public Vector3 Position;
        public Vector3 Rotation;

        public KeyPose(Vector3 position, Vector3 rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public override string ToString() => Position + " " + Rotation;
    }

    /// <summary>One stretch of a pose over time. Spans follow each other; where one ends and the next starts there is a cut (a 0.2 ms step).</summary>
    public sealed class PoseSpan
    {
        public float From, To;
        public Func<float, KeyPose> Pose;
        /// <summary>The pose is defined, and smooth, past the span's ends (a loop): slopes at the ends are taken across them.</summary>
        public bool DefinedBeyond;
    }

    /// <summary>One stretch of a single value over time (cuts between spans, as <see cref="PoseSpan"/>).</summary>
    public sealed class ValueSpan
    {
        public float From, To;
        public Func<float, float> Value;
        public bool DefinedBeyond;
    }

    /// <summary>
    /// Making and trimming CAGEAnimation float-track keys. Fitting turns a pose or value function into sparse keys, adding one at a time
    /// where the played curve strays furthest from the function (checked at 240 Hz) until it is within tolerance. Keys carry weight-one
    /// tangents (segment, slope x segment): with those the weighted Bezier the game plays is the cubic Hermite through the keys' slopes,
    /// so the fit measures exactly what plays. Simplifying keeps the fewest of a track's own keys that stay within tolerance.
    /// </summary>
    public static class KeyFit
    {
        /// <summary>The component tracks a transform is animated through, in the editor's order (Yaw = rotation y, Pitch = x, Roll = z).</summary>
        public static readonly string[] TransformComponents = { "x", "y", "z", "Yaw", "Pitch", "Roll" };

        /// <summary>How often a function is checked against the keys fitted to it.</summary>
        public const float FitRate = 240f;

        /// <summary>The step at a cut: 0.2 ms (a 2 ms step can render a camera half way between two shots).</summary>
        public const float CutGap = 0.0002f;

        public const float DefaultPositionTolerance = 0.002f;
        public const float DefaultAngleTolerance = 0.02f;

        /// <summary>How a fit or a simplify went.</summary>
        public sealed class Report
        {
            public int Keys;
            public int KeysBefore;
            public int Tracks;
            public float WorstPosition;
            public float WorstAngle;
            public float WorstValue;
            public float WorstAt;
            public readonly Dictionary<string, int> KeysPerTrack = new Dictionary<string, int>();
        }

        #region Poses and components
        /// <summary>The value of transform component <paramref name="component"/> (x ... Roll) of a pose.</summary>
        public static float Component(KeyPose pose, string component)
        {
            switch (component)
            {
                case "x": return pose.Position.X;
                case "y": return pose.Position.Y;
                case "z": return pose.Position.Z;
                case "Yaw": return pose.Rotation.Y;
                case "Pitch": return pose.Rotation.X;
                case "Roll": return pose.Rotation.Z;
            }
            throw new ArgumentException("not a transform component: " + component);
        }

        public static bool IsAngle(string component) => component == "Yaw" || component == "Pitch" || component == "Roll";

        /// <summary>A pose from its six component values in <see cref="TransformComponents"/> order.</summary>
        public static KeyPose FromComponents(float[] v) => new KeyPose(new Vector3(v[0], v[1], v[2]), new Vector3(v[4], v[3], v[5]));

        private static float Wrap(float a)
        {
            double d = a;
            while (d > 180) d -= 360;
            while (d < -180) d += 360;
            return (float)d;
        }

        /// <summary>What a set of component tracks holds at a time: each track where there is one, the resting value elsewhere.</summary>
        public static KeyPose PoseAt(IDictionary<string, CAGEAnimation.FloatTrack> tracks, KeyPose rest, float time, bool bezier)
        {
            float[] v = new float[6];
            for (int c = 0; c < 6; c++)
            {
                string name = TransformComponents[c];
                v[c] = tracks != null && tracks.TryGetValue(name, out CAGEAnimation.FloatTrack track) && track?.keyframes != null && track.keyframes.Count != 0
                    ? CageAnimationCurves.ValueAt(track, time, bezier)
                    : Component(rest, name);
            }
            return FromComponents(v);
        }
        #endregion

        #region Fitting
        private sealed class Span
        {
            public float From, To;
            public Func<float, float[]> F;
            public bool DefinedBeyond;
        }

        /// <summary>
        /// Sparse keys for the six component tracks of a pose function, span by span (a cut between spans). Angles are unwrapped sample
        /// to sample and across cuts, so nothing ever turns the long way round.
        /// </summary>
        public static Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> FitPose(IList<PoseSpan> spans, float positionTolerance, float angleTolerance, Report report = null)
        {
            List<Span> list = spans.Select(s => new Span()
            {
                From = s.From,
                To = s.To,
                DefinedBeyond = s.DefinedBeyond,
                F = t =>
                {
                    KeyPose p = s.Pose(t);
                    return new float[] { p.Position.X, p.Position.Y, p.Position.Z, p.Rotation.Y, p.Rotation.X, p.Rotation.Z };
                },
            }).ToList();
            float[] tolerances = { positionTolerance, positionTolerance, positionTolerance, angleTolerance, angleTolerance, angleTolerance };
            bool[] angles = { false, false, false, true, true, true };
            List<CAGEAnimation.FloatTrack.Keyframe>[] keys = Fit(list, 6, tolerances, angles, out float[] worst, out float worstAt);
            Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> result = new Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>>();
            for (int c = 0; c < 6; c++) result[TransformComponents[c]] = keys[c];
            if (report != null)
            {
                report.Tracks += 6;
                for (int c = 0; c < 6; c++)
                {
                    report.Keys += keys[c].Count;
                    report.KeysPerTrack[TransformComponents[c]] = keys[c].Count;
                }
                report.WorstPosition = Math.Max(report.WorstPosition, Math.Max(worst[0], Math.Max(worst[1], worst[2])));
                report.WorstAngle = Math.Max(report.WorstAngle, Math.Max(worst[3], Math.Max(worst[4], worst[5])));
                report.WorstAt = worstAt;
            }
            return result;
        }

        /// <summary>Sparse keys for one value function, span by span (a cut between spans).</summary>
        public static List<CAGEAnimation.FloatTrack.Keyframe> FitValue(IList<ValueSpan> spans, float tolerance, Report report = null, bool angle = false)
        {
            List<Span> list = spans.Select(s => new Span() { From = s.From, To = s.To, DefinedBeyond = s.DefinedBeyond, F = t => new float[] { s.Value(t) } }).ToList();
            List<CAGEAnimation.FloatTrack.Keyframe>[] keys = Fit(list, 1, new[] { tolerance }, new[] { angle }, out float[] worst, out float worstAt);
            if (report != null)
            {
                report.Tracks++;
                report.Keys += keys[0].Count;
                report.WorstValue = Math.Max(report.WorstValue, worst[0]);
            }
            return keys[0];
        }

        private struct Chosen
        {
            public float Time, Value, Slope;
            public bool CutAfter;
        }

        private static List<CAGEAnimation.FloatTrack.Keyframe>[] Fit(List<Span> spans, int count, float[] tolerance, bool[] angle, out float[] worst, out float worstAt)
        {
            if (spans.Count == 0) throw new ArgumentException("nothing to fit");
            worst = new float[count];
            worstAt = 0f;
            List<Chosen>[] chosen = Enumerable.Range(0, count).Select(o => new List<Chosen>()).ToArray();
            float[] previous = Enumerable.Repeat(float.NaN, count).ToArray();
            for (int s = 0; s < spans.Count; s++)
            {
                Span span = spans[s];
                if (!(span.To >= span.From)) throw new ArgumentException("a span ends before it starts (" + span.From + " -> " + span.To + ")");
                int n = Math.Max(2, (int)Math.Ceiling((span.To - span.From) * FitRate) + 1);
                if (span.To - span.From < 1e-6f) n = 1;
                float[] ts = new float[n];
                float[][] vs = Enumerable.Range(0, count).Select(o => new float[n]).ToArray();
                for (int i = 0; i < n; i++)
                {
                    ts[i] = n == 1 ? span.From : (i == n - 1 ? span.To : span.From + (span.To - span.From) * i / (n - 1));
                    float[] v = span.F(ts[i]);
                    for (int c = 0; c < count; c++)
                    {
                        float value = v[c];
                        if (angle[c] && !float.IsNaN(previous[c])) value = previous[c] + Wrap(value - previous[c]);
                        vs[c][i] = value;
                        previous[c] = value;
                    }
                }
                bool cutAfter = s < spans.Count - 1;
                for (int c = 0; c < count; c++)
                {
                    if (n == 1)
                    {
                        chosen[c].Add(new Chosen() { Time = ts[0], Value = vs[c][0], Slope = 0f, CutAfter = cutAfter });
                        continue;
                    }
                    int comp = c;
                    Dictionary<int, float> slope = new Dictionary<int, float>();
                    float SlopeAt(int i)
                    {
                        if (slope.TryGetValue(i, out float known)) return known;
                        float found = Slope(span, comp, ts[i], vs[comp][i], angle[comp]);
                        slope[i] = found;
                        return found;
                    }
                    List<int> keys = new List<int>() { 0, n - 1 };
                    //Each segment's worst point, kept so an insertion re-measures only the two segments it splits
                    List<float> segWorst = new List<float>();
                    List<int> segAt = new List<int>();
                    void Measure(int k, out float e, out int at)
                    {
                        int a = keys[k], b = keys[k + 1];
                        float h = ts[b] - ts[a];
                        float ma = SlopeAt(a) * h, mb = SlopeAt(b) * h;
                        e = 0f; at = -1;
                        for (int i = a + 1; i < b; i++)
                        {
                            float d = Math.Abs(Hermite(vs[comp][a], ma, vs[comp][b], mb, (ts[i] - ts[a]) / h) - vs[comp][i]);
                            if (d > e) { e = d; at = i; }
                        }
                    }
                    Measure(0, out float e0, out int a0);
                    segWorst.Add(e0);
                    segAt.Add(a0);
                    while (true)
                    {
                        int k = -1;
                        //A margin under the tolerance: the check is every 1/240 s, and the curve can stray a little further between checks
                        float e = tolerance[c] * 0.85f;
                        for (int j = 0; j < segWorst.Count; j++)
                            if (segWorst[j] > e && segAt[j] >= 0) { e = segWorst[j]; k = j; }
                        if (k < 0) break;
                        int insert = segAt[k];
                        keys.Insert(k + 1, insert);
                        Measure(k, out float ea, out int aa);
                        Measure(k + 1, out float eb, out int ab);
                        segWorst[k] = ea; segAt[k] = aa;
                        segWorst.Insert(k + 1, eb); segAt.Insert(k + 1, ab);
                    }
                    for (int j = 0; j < segWorst.Count; j++)
                        if (segWorst[j] > worst[c]) { worst[c] = segWorst[j]; worstAt = segAt[j] >= 0 ? ts[segAt[j]] : worstAt; }
                    for (int j = 0; j < keys.Count; j++)
                        chosen[c].Add(new Chosen() { Time = ts[keys[j]], Value = vs[c][keys[j]], Slope = SlopeAt(keys[j]), CutAfter = j == keys.Count - 1 && cutAfter });
                }
            }
            List<CAGEAnimation.FloatTrack.Keyframe>[] result = new List<CAGEAnimation.FloatTrack.Keyframe>[count];
            for (int c = 0; c < count; c++)
                result[c] = Keyframes(chosen[c]);
            return result;
        }

        /// <summary>
        /// A component's slope at a time from the function itself: central difference inside the span, a three-point one-sided
        /// difference at its ends (the function need not be defined past them).
        /// </summary>
        private static float Slope(Span span, int c, float t, float v, bool angle)
        {
            const float h = 0.002f;
            float Value(float at)
            {
                float x = span.F(at)[c];
                return angle ? v + Wrap(x - v) : x;
            }
            float length = span.To - span.From;
            if (span.DefinedBeyond) return (Value(t + h) - Value(t - h)) / (2 * h);
            if (length < 4 * h)
            {
                if (length < 1e-6f) return 0f;
                return (Value(span.To) - Value(span.From)) / length;
            }
            if (t - h < span.From) return (-3f * v + 4f * Value(t + h) - Value(t + 2 * h)) / (2 * h);
            if (t + h > span.To) return (3f * v - 4f * Value(t - h) + Value(t - 2 * h)) / (2 * h);
            return (Value(t + h) - Value(t - h)) / (2 * h);
        }

        private static float Hermite(float p0, float m0h, float p1, float m1h, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return (2 * u3 - 3 * u2 + 1) * p0 + (u3 - 2 * u2 + u) * m0h + (-2 * u3 + 3 * u2) * p1 + (u3 - u2) * m1h;
        }

        /// <summary>
        /// Chosen points -> keyframes with weight-one tangents (segment, slope x segment). Across a cut the 0.2 ms segment is a straight
        /// step: its control points sit a tenth of the way in, level with their keys, and each side keeps its own slope outward.
        /// </summary>
        private static List<CAGEAnimation.FloatTrack.Keyframe> Keyframes(List<Chosen> points)
        {
            List<CAGEAnimation.FloatTrack.Keyframe> keys = new List<CAGEAnimation.FloatTrack.Keyframe>();
            for (int i = 0; i < points.Count; i++)
            {
                Chosen p = points[i];
                float hin = i > 0 ? p.Time - points[i - 1].Time : (points.Count > 1 ? points[1].Time - p.Time : 1f);
                float hout = i + 1 < points.Count ? points[i + 1].Time - p.Time : (points.Count > 1 ? p.Time - points[i - 1].Time : 1f);
                if (hin <= 0f) hin = 1e-4f;
                if (hout <= 0f) hout = 1e-4f;
                CAGEAnimation.FloatTrack.Keyframe key = CageAnimationCurves.NewKeyframe(p.Time, p.Value, CAGEAnimation.InterpolationMode.Bezier);
                key.tan_in = i > 0 && points[i - 1].CutAfter ? new Vector2(hin * 0.1f, 0f) : new Vector2(hin, p.Slope * hin);
                key.tan_out = p.CutAfter ? new Vector2(hout * 0.1f, 0f) : new Vector2(hout, p.Slope * hout);
                keys.Add(key);
            }
            return keys;
        }
        #endregion

        #region Simplifying
        /// <summary>One track simplified.</summary>
        public sealed class Simplified
        {
            public List<CAGEAnimation.FloatTrack.Keyframe> Keys;
            public int Before, After;
            public float Worst, WorstAt;
            /// <summary>Times where the tolerance could not be met (a guard: the track's own keys always meet it).</summary>
            public int Unmet;
        }

        /// <summary>
        /// The fewest of a track's own keys that play within <paramref name="tolerance"/> of it, as new objects. A kept key keeps its
        /// tangent on each side whose neighbour is kept, and otherwise gets its own slope over the longer segment.
        /// </summary>
        public static Simplified Simplify(IList<CAGEAnimation.FloatTrack.Keyframe> keyframes, bool bezier, float tolerance)
        {
            List<CAGEAnimation.FloatTrack.Keyframe> keys = keyframes.Where(o => o != null).OrderBy(o => o.time).ToList();
            int n = keys.Count;
            Simplified result = new Simplified() { Before = n };
            if (n <= 2)
            {
                result.Keys = keys.Select(Clone).ToList();
                result.After = result.Keys.Count;
                return result;
            }
            CAGEAnimation.FloatTrack original = new CAGEAnimation.FloatTrack() { keyframes = keys };

            //Samples: every old key's time, and 240 a second through each old segment, with what the old track plays there
            List<float> sampleTimes = new List<float>();
            for (int k = 0; k < n - 1; k++)
            {
                float a = keys[k].time, b = keys[k + 1].time;
                int m = Math.Max(1, (int)Math.Ceiling((b - a) * FitRate));
                for (int j = 0; j < m; j++) sampleTimes.Add(a + (b - a) * j / m);
            }
            sampleTimes.Add(keys[n - 1].time);
            float[] sampleValues = sampleTimes.Select(t => CageAnimationCurves.ValueAt(original, t, bezier)).ToArray();
            float[] times = sampleTimes.ToArray();

            //Each key's own slope either side, from its tangents (or the curve, where a tangent has no length)
            float[] slopeIn = new float[n], slopeOut = new float[n];
            for (int k = 0; k < n; k++)
            {
                CAGEAnimation.FloatTrack.Keyframe key = keys[k];
                if (!bezier)
                {
                    slopeIn[k] = k > 0 ? Divide(key.value.Y - keys[k - 1].value.Y, key.time - keys[k - 1].time) : 0f;
                    slopeOut[k] = k < n - 1 ? Divide(keys[k + 1].value.Y - key.value.Y, keys[k + 1].time - key.time) : 0f;
                    continue;
                }
                const float h = 0.001f;
                slopeIn[k] = key.tan_in.X > 1e-6f ? key.tan_in.Y / key.tan_in.X : (k > 0 ? (key.value.Y - CageAnimationCurves.ValueAt(original, key.time - h, true)) / h : 0f);
                slopeOut[k] = key.tan_out.X > 1e-6f ? key.tan_out.Y / key.tan_out.X : (k < n - 1 ? (CageAnimationCurves.ValueAt(original, key.time + h, true) - key.value.Y) / h : 0f);
            }

            List<int> kept = new List<int>() { 0, n - 1 };
            CAGEAnimation.FloatTrack.Keyframe Make(int at)
            {
                int i = kept[at];
                CAGEAnimation.FloatTrack.Keyframe key = Clone(keys[i]);
                if (bezier)
                {
                    if (at > 0 && kept[at - 1] != i - 1)
                    {
                        float h = key.time - keys[kept[at - 1]].time;
                        key.tan_in = new Vector2(h, slopeIn[i] * h);
                    }
                    if (at < kept.Count - 1 && kept[at + 1] != i + 1)
                    {
                        float h = keys[kept[at + 1]].time - key.time;
                        key.tan_out = new Vector2(h, slopeOut[i] * h);
                    }
                }
                return key;
            }
            List<CAGEAnimation.FloatTrack.Keyframe> made = new List<CAGEAnimation.FloatTrack.Keyframe>() { Make(0), Make(1) };
            List<float> segWorst = new List<float>();
            List<float> segAt = new List<float>();
            void Measure(int s, out float e, out float at)
            {
                CAGEAnimation.FloatTrack.Keyframe a = made[s], b = made[s + 1];
                e = 0f; at = a.time;
                int from = LowerBound(times, a.time), to = LowerBound(times, b.time + 1e-7f);
                for (int i = from; i < to && i < times.Length; i++)
                {
                    float d = Math.Abs(Segment(a, b, times[i], bezier) - sampleValues[i]);
                    if (d > e) { e = d; at = times[i]; }
                }
            }
            Measure(0, out float e0, out float t0);
            segWorst.Add(e0); segAt.Add(t0);
            while (true)
            {
                int s = -1;
                float e = tolerance;
                for (int j = 0; j < segWorst.Count; j++)
                    if (segWorst[j] > e && kept[j + 1] - kept[j] > 1) { e = segWorst[j]; s = j; }
                if (s < 0) break;
                //The old key nearest the worst point goes back in
                int best = -1;
                float bestDistance = float.MaxValue;
                for (int i = kept[s] + 1; i < kept[s + 1]; i++)
                {
                    float d = Math.Abs(keys[i].time - segAt[s]);
                    if (d < bestDistance) { bestDistance = d; best = i; }
                }
                kept.Insert(s + 1, best);
                made.Insert(s + 1, null);
                //The keys either side get their tangents over the new segments
                made[s] = Make(s);
                made[s + 1] = Make(s + 1);
                made[s + 2] = Make(s + 2);
                Measure(s, out float ea, out float aa);
                Measure(s + 1, out float eb, out float ab);
                segWorst[s] = ea; segAt[s] = aa;
                segWorst.Insert(s + 1, eb); segAt.Insert(s + 1, ab);
            }
            for (int j = 0; j < segWorst.Count; j++)
            {
                if (segWorst[j] > result.Worst) { result.Worst = segWorst[j]; result.WorstAt = segAt[j]; }
                if (segWorst[j] > tolerance) result.Unmet++;
            }
            result.Keys = made;
            result.After = made.Count;
            return result;
        }

        private static float Divide(float a, float b) => Math.Abs(b) < 1e-9f ? 0f : a / b;

        private static int LowerBound(float[] sorted, float value)
        {
            int lo = 0, hi = sorted.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (sorted[mid] < value - 1e-7f) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        /// <summary>One segment as <see cref="CageAnimationCurves.ValueAt"/> plays it (the same Newton solve for the Bezier's time).</summary>
        private static float Segment(CAGEAnimation.FloatTrack.Keyframe a, CAGEAnimation.FloatTrack.Keyframe b, float time, bool bezier)
        {
            if (time <= a.time) return a.value.Y;
            if (time >= b.time) return b.value.Y;
            float span = b.time - a.time;
            if (span <= 1e-8f) return a.value.Y;
            if (!bezier) return a.value.Y + (b.value.Y - a.value.Y) * ((time - a.time) / span);
            CageAnimationCurves.OutControl(a, out float c1t, out float c1v);
            CageAnimationCurves.InControl(b, out float c2t, out float c2v);
            float u = (time - a.time) / span;
            for (int iter = 0; iter < 8; iter++)
            {
                float x = Cubic(a.time, c1t, c2t, b.time, u);
                float dx = 3f * ((c1t - a.time) * (1f - u) * (1f - u) + 2f * (c2t - c1t) * (1f - u) * u + (b.time - c2t) * u * u);
                if (Math.Abs(dx) < 1e-8f) break;
                u -= (x - time) / dx;
                if (u < 0f) u = 0f;
                else if (u > 1f) u = 1f;
            }
            return Cubic(a.value.Y, c1v, c2v, b.value.Y, u);
        }

        private static float Cubic(float p0, float p1, float p2, float p3, float u)
        {
            float omu = 1f - u;
            return omu * omu * omu * p0 + 3f * omu * omu * u * p1 + 3f * omu * u * u * p2 + u * u * u * p3;
        }

        public static CAGEAnimation.FloatTrack.Keyframe Clone(CAGEAnimation.FloatTrack.Keyframe key)
        {
            return new CAGEAnimation.FloatTrack.Keyframe()
            {
                mode = key.mode,
                time = key.time,
                value = key.value,
                tan_in = key.tan_in,
                tan_out = key.tan_out,
            };
        }

        /// <summary>The tolerance a component track is simplified to: metres for x / y / z, degrees for Yaw / Pitch / Roll, else the value tolerance.</summary>
        public static float ToleranceFor(string component, float position, float angle, float value)
        {
            if (component == "x" || component == "y" || component == "z") return position;
            if (IsAngle(component)) return angle;
            return value;
        }
        #endregion

        #region Cuts in existing keys
        /// <summary>
        /// Where a set of tracks steps (two keys of one track closer than <paramref name="gap"/> seconds): the times a fit of anything
        /// riding these tracks must split its spans at, as [before, after] pairs.
        /// </summary>
        public static List<(float before, float after)> Steps(IEnumerable<CAGEAnimation.FloatTrack> tracks, float gap = 0.002f)
        {
            List<(float, float)> steps = new List<(float, float)>();
            foreach (CAGEAnimation.FloatTrack track in tracks)
            {
                if (track?.keyframes == null) continue;
                List<CAGEAnimation.FloatTrack.Keyframe> keys = track.keyframes.OrderBy(o => o.time).ToList();
                for (int i = 1; i < keys.Count; i++)
                {
                    float a = keys[i - 1].time, b = keys[i].time;
                    if (b - a < gap && b > a && !steps.Any(o => Math.Abs(o.Item1 - a) < 1e-5f))
                        steps.Add((a, b));
                }
            }
            return steps.OrderBy(o => o.Item1).ToList();
        }

        /// <summary>[from, to] split at steps into spans (each step's two sides become the end of one span and the start of the next).</summary>
        public static List<(float from, float to)> SplitAtSteps(float from, float to, List<(float before, float after)> steps)
        {
            List<(float, float)> spans = new List<(float, float)>();
            float start = from;
            foreach ((float before, float after) in steps)
            {
                if (before <= start || after >= to) continue;
                spans.Add((start, before));
                start = after;
            }
            spans.Add((start, to));
            return spans;
        }
        #endregion
    }
}
