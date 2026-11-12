using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Cinematics
{
    /// <summary>What a flight along a camera or object path is asked to do.</summary>
    public sealed class FlightSpec
    {
        /// <summary>The route's points, in the space the poses come out in (metres).</summary>
        public List<Vector3> Points = new List<Vector3>();
        /// <summary>The route loops back to its first point.</summary>
        public bool Closed;
        /// <summary>"through" (a C2 cubic spline through every point, by chord length) or "bspline" (closed only: the points are the
        /// control points of a periodic cubic B-spline, which rounds every corner).</summary>
        public string Route = "through";
        /// <summary>Seconds for the whole route (0: from the speeds).</summary>
        public double Duration;
        /// <summary>Cruising speed (m/s) when no duration is given, and where no per-point speed is.</summary>
        public float Speed = 10f;
        /// <summary>Optional speed at each point (m/s; null where not given): the speed changes smoothly between them.</summary>
        public List<float?> Speeds;
        /// <summary>m/s gained for each metre below the route's mean height (and lost above it): dives fast, climbs slow.</summary>
        public float ClimbTrade;
        /// <summary>none, in (from rest), out (to a stop) or in_out.</summary>
        public string Ease = "none";
        /// <summary>Bank into turns (a coordinated turn: the craft's up along the lift it needs).</summary>
        public bool Bank = true;
        /// <summary>Scales the bank (1 = the physical coordinated turn).</summary>
        public float BankScale = 1f;
        /// <summary>Degrees the bank never exceeds.</summary>
        public float MaxBank = 60f;
        /// <summary>The nose follows climbs and dives (pitch along the path); false keeps it level.</summary>
        public bool PitchWithPath = true;
        /// <summary>Seconds a heavy craft's attitude lags the manoeuvre (a critically damped spring; 0 = none).</summary>
        public float AttitudeLag;
        /// <summary>Degrees added to every roll.</summary>
        public float Roll;
        /// <summary>When the flight starts (seconds into the animation).</summary>
        public float StartTime;
        /// <summary>Speeds never go below this (m/s), so a climb never stalls.</summary>
        public float MinSpeed = 0.5f;
    }

    /// <summary>
    /// A flight along a route: positions walked by arc length to a speed plan, and an attitude from the motion - nose along the path,
    /// banked into turns like an aircraft (lift along the acceleration plus gravity), optionally lagging through a critically damped
    /// spring. Pure maths: any thread.
    /// </summary>
    public sealed class FlightPath
    {
        private const float G = 9.81f;

        public FlightSpec Spec { get; private set; }
        public float Length { get; private set; }
        public float Duration { get; private set; }
        public float Start => Spec.StartTime;
        public float End => Spec.StartTime + Duration;

        //Report
        public float MaxBank { get; private set; }
        public float MaxBankAt { get; private set; }
        public float MaxLoad { get; private set; }
        public float MinSpeedSeen { get; private set; }
        public float MaxSpeedSeen { get; private set; }
        public float MaxTurnRate { get; private set; }
        public float MaxPitch { get; private set; }
        public List<string> Warnings { get; } = new List<string>();

        //The route: a parameter u over [0, U], and an arc-length table over it
        private Func<float, Vector3> _geometry;
        /// <summary>The route's first and second derivatives by its parameter (analytic, so tangents and curvature are smooth).</summary>
        private Func<float, Vector3> _d1, _d2;
        private float _uLength;
        private float[] _arcU, _arcS;
        //The speed plan: plan time at arc length (table over s), then eased into real time
        private float[] _planS, _planT;
        private float _planLength;
        private Func<float, float> _baseSpeed;
        private float _yRef;
        private bool _periodic;
        //The attitude, tabulated when it lags
        private Vector3[] _attitude;
        private float _attitudeFrom, _attitudeRate;

        private FlightPath() { }

        public static FlightPath Plan(FlightSpec spec)
        {
            if (spec.Points == null || spec.Points.Count < 2) throw new ArgumentException("A flight needs at least 2 points.");
            if (spec.Closed && spec.Points.Count < 3) throw new ArgumentException("A closed flight needs at least 3 points.");
            string route = (spec.Route ?? "through").ToLowerInvariant();
            if (route != "through" && route != "bspline") throw new ArgumentException("route is 'through' or 'bspline'.");
            if (route == "bspline" && !spec.Closed) throw new ArgumentException("route 'bspline' is for closed loops (its points are the loop's control points).");
            string ease = (spec.Ease ?? "none").ToLowerInvariant();
            if (ease != "none" && ease != "in" && ease != "out" && ease != "in_out") throw new ArgumentException("ease is none, in, out or in_out.");
            FlightPath f = new FlightPath() { Spec = spec };
            f._periodic = spec.Closed && ease == "none";

            if (route == "bspline")
            {
                Vector3[] c = spec.Points.ToArray();
                f._uLength = c.Length;
                f._geometry = u => BSpline(c, u, 0);
                f._d1 = u => BSpline(c, u, 1);
                f._d2 = u => BSpline(c, u, 2);
            }
            else if (spec.Closed)
                f.BuildPeriodic(spec.Points);
            else
                f.BuildOpen(spec.Points);
            f.BuildArcTable(route == "bspline" ? spec.Points.Count * 2000 : Math.Max(4000, spec.Points.Count * 600));
            f.BuildSpeedPlan(route);
            f.BuildAttitude();
            f.Measure();
            return f;
        }

        #region Route geometry
        /// <summary>A periodic uniform cubic B-spline (C2): the curve passes near, not through, its control points.</summary>
        private static Vector3 BSpline(Vector3[] c, float u, int derivative)
        {
            int n = c.Length;
            u = ((u % n) + n) % n;
            int i = (int)Math.Floor(u);
            if (i >= n) i = n - 1;
            float t = u - i;
            Vector3 p0 = c[(i - 1 + n) % n], p1 = c[i % n], p2 = c[(i + 1) % n], p3 = c[(i + 2) % n];
            float b0, b1, b2, b3;
            if (derivative == 1)
            {
                b0 = -(1 - t) * (1 - t) / 2f; b1 = (3 * t * t - 4 * t) / 2f; b2 = (-3 * t * t + 2 * t + 1) / 2f; b3 = t * t / 2f;
            }
            else if (derivative == 2)
            {
                b0 = 1 - t; b1 = 3 * t - 2; b2 = -3 * t + 1; b3 = t;
            }
            else
            {
                b0 = (1 - t) * (1 - t) * (1 - t) / 6f; b1 = (3 * t * t * t - 6 * t * t + 4) / 6f; b2 = (-3 * t * t * t + 3 * t * t + 3 * t + 1) / 6f; b3 = t * t * t / 6f;
            }
            return b0 * p0 + b1 * p1 + b2 * p2 + b3 * p3;
        }

        /// <summary>An open C2 cubic spline through the points by chord length, its ends leaving along the first and last chords.</summary>
        private void BuildOpen(List<Vector3> points)
        {
            int n = points.Count;
            float[] u = new float[n];
            for (int i = 1; i < n; i++) u[i] = u[i - 1] + Math.Max(1e-3f, Vector3.Distance(points[i], points[i - 1]));
            Vector3 v0 = Vector3.Normalize(points[1] - points[0]), v1 = Vector3.Normalize(points[n - 1] - points[n - 2]);
            Vector3[] p = points.ToArray();
            Vector3[] m = new Vector3[n];
            float[] a = new float[n], b = new float[n], cc = new float[n];
            Vector3[] d = new Vector3[n];
            float h0 = u[1] - u[0];
            b[0] = 2 * h0; cc[0] = h0; d[0] = 6 * ((p[1] - p[0]) / h0 - v0);
            for (int i = 1; i < n - 1; i++)
            {
                float hp = u[i] - u[i - 1], hn = u[i + 1] - u[i];
                a[i] = hp; b[i] = 2 * (hp + hn); cc[i] = hn;
                d[i] = 6 * ((p[i + 1] - p[i]) / hn - (p[i] - p[i - 1]) / hp);
            }
            float hl = u[n - 1] - u[n - 2];
            a[n - 1] = hl; b[n - 1] = 2 * hl; d[n - 1] = 6 * (v1 - (p[n - 1] - p[n - 2]) / hl);
            for (int i = 1; i < n; i++)
            {
                float w = a[i] / b[i - 1];
                b[i] -= w * cc[i - 1];
                d[i] -= w * d[i - 1];
            }
            m[n - 1] = d[n - 1] / b[n - 1];
            for (int i = n - 2; i >= 0; i--) m[i] = (d[i] - cc[i] * m[i + 1]) / b[i];
            _uLength = u[n - 1];
            _geometry = t => CubicAt(u, p, m, Math.Max(0f, Math.Min(u[n - 1], t)), 0);
            _d1 = t => CubicAt(u, p, m, Math.Max(0f, Math.Min(u[n - 1], t)), 1);
            _d2 = t => CubicAt(u, p, m, Math.Max(0f, Math.Min(u[n - 1], t)), 2);
        }

        /// <summary>A closed C2 cubic spline through the points by chord length (the periodic system solved directly).</summary>
        private void BuildPeriodic(List<Vector3> points)
        {
            int n = points.Count;
            float[] u = new float[n + 1];
            for (int i = 1; i <= n; i++) u[i] = u[i - 1] + Math.Max(1e-3f, Vector3.Distance(points[i % n], points[i - 1]));
            Vector3[] p = new Vector3[n + 1];
            for (int i = 0; i <= n; i++) p[i] = points[i % n];
            Func<int, float> h = i => u[((i % n) + n) % n + 1] - u[((i % n) + n) % n];
            //h(i-1) M(i-1) + 2(h(i-1) + h(i)) M(i) + h(i) M(i+1) = 6((p(i+1) - p(i)) / h(i) - (p(i) - p(i-1)) / h(i-1)), indices round the loop
            double[,] A = new double[n, n];
            double[][] rhs = { new double[n], new double[n], new double[n] };
            for (int i = 0; i < n; i++)
            {
                float hp = h(i - 1), hn = h(i);
                A[i, (i - 1 + n) % n] += hp;
                A[i, i] += 2 * (hp + hn);
                A[i, (i + 1) % n] += hn;
                Vector3 r = 6 * ((points[(i + 1) % n] - points[i]) / hn - (points[i] - points[(i - 1 + n) % n]) / hp);
                rhs[0][i] = r.X; rhs[1][i] = r.Y; rhs[2][i] = r.Z;
            }
            double[][] solved = SolveDense(A, rhs);
            Vector3[] m = new Vector3[n + 1];
            for (int i = 0; i <= n; i++) m[i] = new Vector3((float)solved[0][i % n], (float)solved[1][i % n], (float)solved[2][i % n]);
            _uLength = u[n];
            float total = u[n];
            Func<float, float> wrap = t => ((t % total) + total) % total;
            _geometry = t => CubicAt(u, p, m, wrap(t), 0);
            _d1 = t => CubicAt(u, p, m, wrap(t), 1);
            _d2 = t => CubicAt(u, p, m, wrap(t), 2);
        }

        private static double[][] SolveDense(double[,] A, double[][] rhs)
        {
            int n = A.GetLength(0), k = rhs.Length;
            double[,] a = (double[,])A.Clone();
            double[][] b = rhs.Select(o => (double[])o.Clone()).ToArray();
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < n; r++) if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col])) pivot = r;
                if (pivot != col)
                {
                    for (int c = 0; c < n; c++) { double t = a[col, c]; a[col, c] = a[pivot, c]; a[pivot, c] = t; }
                    foreach (double[] v in b) { double t = v[col]; v[col] = v[pivot]; v[pivot] = t; }
                }
                double diag = a[col, col];
                if (Math.Abs(diag) < 1e-12) throw new ArgumentException("The route's points do not make a curve (two in the same place?).");
                for (int r = col + 1; r < n; r++)
                {
                    double w = a[r, col] / diag;
                    if (w == 0) continue;
                    for (int c = col; c < n; c++) a[r, c] -= w * a[col, c];
                    foreach (double[] v in b) v[r] -= w * v[col];
                }
            }
            double[][] x = Enumerable.Range(0, k).Select(o => new double[n]).ToArray();
            for (int r = n - 1; r >= 0; r--)
                for (int j = 0; j < k; j++)
                {
                    double s = b[j][r];
                    for (int c = r + 1; c < n; c++) s -= a[r, c] * x[j][c];
                    x[j][r] = s / a[r, r];
                }
            return x;
        }

        /// <summary>A cubic spline with second derivatives m at knots u through points p.</summary>
        private static Vector3 CubicAt(float[] u, Vector3[] p, Vector3[] m, float t, int derivative)
        {
            int i = Array.BinarySearch(u, t);
            if (i < 0) i = ~i - 1;
            i = Math.Max(0, Math.Min(u.Length - 2, i));
            float h = u[i + 1] - u[i], x = t - u[i];
            Vector3 bb = (p[i + 1] - p[i]) / h - h * (2 * m[i] + m[i + 1]) / 6;
            if (derivative == 1) return bb + m[i] * x + (m[i + 1] - m[i]) / (2 * h) * x * x;
            if (derivative == 2) return m[i] + (m[i + 1] - m[i]) / h * x;
            return p[i] + bb * x + m[i] / 2 * x * x + (m[i + 1] - m[i]) / (6 * h) * x * x * x;
        }

        private void BuildArcTable(int samples)
        {
            _arcU = new float[samples + 1];
            _arcS = new float[samples + 1];
            Vector3 previous = _geometry(0f);
            for (int i = 1; i <= samples; i++)
            {
                float u = _uLength * i / samples;
                Vector3 p = _geometry(u);
                _arcU[i] = u;
                _arcS[i] = _arcS[i - 1] + Vector3.Distance(previous, p);
                previous = p;
            }
            Length = _arcS[samples];
            if (Length < 0.05f) throw new ArgumentException("The route is only " + Length + " m long.");
        }

        private float UAt(float s)
        {
            if (_periodic || Spec.Closed) s = ((s % Length) + Length) % Length;
            else s = Math.Max(0f, Math.Min(Length, s));
            int k = Array.BinarySearch(_arcS, s);
            if (k >= 0) return _arcU[k];
            k = ~k;
            if (k <= 0) return 0f;
            if (k >= _arcS.Length) return _uLength;
            float f = (s - _arcS[k - 1]) / Math.Max(1e-9f, _arcS[k] - _arcS[k - 1]);
            return _arcU[k - 1] + (_arcU[k] - _arcU[k - 1]) * f;
        }

        /// <summary>The point <paramref name="s"/> metres along the route.</summary>
        public Vector3 PointAt(float s) => _geometry(UAt(s));

        /// <summary>The route's direction <paramref name="s"/> metres along it (unit).</summary>
        public Vector3 TangentAt(float s)
        {
            Vector3 d = _d1(UAt(s));
            return d.LengthSquared() < 1e-12f ? Vector3.UnitZ : Vector3.Normalize(d);
        }

        /// <summary>The route's curvature vector (d tangent / d s, pointing into the turn, length 1 / radius), from the analytic derivatives.</summary>
        private Vector3 CurvatureAt(float s)
        {
            float u = UAt(s);
            Vector3 d1 = _d1(u), d2 = _d2(u);
            float speed2 = d1.LengthSquared();
            if (speed2 < 1e-12f) return Vector3.Zero;
            Vector3 t = d1 / (float)Math.Sqrt(speed2);
            return (d2 - Vector3.Dot(d2, t) * t) / speed2;
        }
        #endregion

        #region Speed plan
        private void BuildSpeedPlan(string route)
        {
            FlightSpec spec = Spec;
            int n = spec.Points.Count;
            //Each point's place along the route: by its nearest route sample (exact for 'through'; the nearest approach for a B-spline)
            float[] pointS = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (route == "through")
                {
                    float best = float.MaxValue;
                    //chord parameter of point i is where the spline passes through it: find it on the table
                    for (int k = 0; k < _arcU.Length; k += 1)
                    {
                        float d = Vector3.DistanceSquared(_geometry(_arcU[k]), spec.Points[i]);
                        if (d < best) { best = d; pointS[i] = _arcS[k]; }
                        if (k + 8 < _arcU.Length) k += 7;
                    }
                }
                else
                    pointS[i] = Length * i / n;
            }
            if (!spec.Closed) { pointS[0] = 0f; pointS[n - 1] = Length; }

            //Base speed along the route: per-point speeds blended smoothly (smoothstep between the points that give one), else the cruise speed
            List<(float s, float v)> given = new List<(float, float)>();
            if (spec.Speeds != null)
                for (int i = 0; i < n && i < spec.Speeds.Count; i++)
                    if (spec.Speeds[i].HasValue) given.Add((pointS[i], Math.Max(spec.MinSpeed, spec.Speeds[i].Value)));
            given = given.OrderBy(o => o.s).ToList();
            float yRef = 0f;
            const int M = 4000;
            for (int j = 0; j <= M; j++) yRef += PointAt(Length * j / M).Y;
            yRef /= M + 1;
            Func<float, float> baseSpeed = s =>
            {
                if (given.Count == 0) return spec.Speed;
                if (given.Count == 1) return given[0].v;
                if (spec.Closed)
                {
                    //round the loop: between the last given and the first given again
                    for (int k = 0; k < given.Count; k++)
                    {
                        (float sa, float va) = given[k];
                        (float sb, float vb) = given[(k + 1) % given.Count];
                        float span = k + 1 < given.Count ? sb - sa : sb + Length - sa;
                        float at = s - sa;
                        if (at < 0) at += Length;
                        if (at <= span) return va + (vb - va) * Smooth(span < 1e-4f ? 1f : at / span);
                    }
                    return given[0].v;
                }
                if (s <= given[0].s) return given[0].v;
                for (int k = 0; k + 1 < given.Count; k++)
                    if (s <= given[k + 1].s)
                    {
                        float span = given[k + 1].s - given[k].s;
                        return given[k].v + (given[k + 1].v - given[k].v) * Smooth(span < 1e-4f ? 1f : (s - given[k].s) / span);
                    }
                return given[given.Count - 1].v;
            };
            _baseSpeed = baseSpeed;
            _yRef = yRef;
            _planS = new float[M + 1];
            _planT = new float[M + 1];
            for (int j = 1; j <= M; j++)
            {
                float s0 = Length * (j - 1) / M, s1 = Length * j / M, mid = (s0 + s1) / 2;
                float v = baseSpeed(mid) + spec.ClimbTrade * (yRef - PointAt(mid).Y);
                v = Math.Max(spec.MinSpeed, v);
                _planS[j] = s1;
                _planT[j] = _planT[j - 1] + (s1 - s0) / v;
            }
            _planLength = _planT[M];
            Duration = spec.Duration > 0 ? (float)spec.Duration : _planLength;
            //From rest / to a stop spreads the same plan over the same time with ramps, so the middle runs faster
            if (spec.Duration <= 0 && (spec.Ease ?? "none") != "none")
            {
                Ease(spec.Ease, 0.5, out double _, out double peak);
                Duration = (float)(_planLength * peak);
            }
        }

        private static float Smooth(float x)
        {
            x = Math.Max(0f, Math.Min(1f, x));
            return x * x * (3f - 2f * x);
        }

        /// <summary>How far through (0-1) a fraction x of the time is, and the speed there in fractions per duration (the camera tools' ramps).</summary>
        private static void Ease(string ease, double x, out double along, out double speed)
        {
            const double ramp = 0.25;
            double rin = ease == "in" || ease == "in_out" ? ramp : 0, rout = ease == "out" || ease == "in_out" ? ramp : 0;
            double vmax = 1.0 / (1.0 - rin / 2 - rout / 2);
            //Smoothstep ramps (the speed itself eases in and out, so there is no jolt where a ramp starts or ends)
            if (rin > 0 && x < rin)
            {
                double y = x / rin;
                along = vmax * rin * (y * y * y - 0.5 * y * y * y * y);
                speed = vmax * (3 * y * y - 2 * y * y * y);
            }
            else if (rout > 0 && x > 1 - rout)
            {
                double y = (1 - x) / rout;
                along = 1 - vmax * rout * (y * y * y - 0.5 * y * y * y * y);
                speed = vmax * (3 * y * y - 2 * y * y * y);
            }
            else
            {
                along = vmax * (rin / 2 + x - rin);
                speed = vmax;
            }
        }

        /// <summary>Metres along the route at time <paramref name="t"/> (seconds into the animation).</summary>
        public float DistanceAt(float t)
        {
            float x = (t - Spec.StartTime) / Duration;
            if (_periodic)
            {
                //A loop with no ease runs on round itself, so the join is seamless and derivatives never see an end
                float laps = (float)Math.Floor(x);
                return laps * Length + PlanDistance((x - laps) * _planLength);
            }
            x = Math.Max(0f, Math.Min(1f, x));
            Ease(Spec.Ease ?? "none", x, out double along, out double _);
            return PlanDistance((float)(along * _planLength));
        }

        private float PlanDistance(float planTime)
        {
            if (planTime <= 0f) return 0f;
            if (planTime >= _planLength) return Length;
            int k = Array.BinarySearch(_planT, planTime);
            if (k >= 0) return _planS[k];
            k = ~k;
            float f = (planTime - _planT[k - 1]) / Math.Max(1e-9f, _planT[k] - _planT[k - 1]);
            return _planS[k - 1] + (_planS[k] - _planS[k - 1]) * f;
        }
        #endregion

        #region Attitude
        /// <summary>Where the craft is at time <paramref name="t"/>.</summary>
        public Vector3 PositionAt(float t) => PointAt(DistanceAt(t));

        /// <summary>Speed (m/s) along the route and its rate of change at t.</summary>
        private void SpeedAt(float t, out float speed, out float accel)
        {
            speed = Speed(t);
            const float h = 0.02f;
            float c = t - h, d = t + h;
            if (!_periodic) { c = Math.Max(Start, c); d = Math.Min(End, d); }
            accel = d - c < 1e-5f ? 0f : (Speed(d) - Speed(c)) / (d - c);
        }

        /// <summary>The planned speed (m/s) at a point along the route, before easing.</summary>
        private float PlanSpeed(float s)
        {
            if (Spec.Closed) s = ((s % Length) + Length) % Length;
            return Math.Max(Spec.MinSpeed, _baseSpeed(s) + Spec.ClimbTrade * (_yRef - PointAt(s).Y));
        }

        /// <summary>Speed (m/s) at time t: the plan's speed where it is, times how fast the eased clock runs through the plan.</summary>
        private float Speed(float t)
        {
            float x = (t - Spec.StartTime) / Duration;
            double rate;
            if (_periodic) rate = 1.0;
            else
            {
                if (x < 0f || x > 1f) return 0f;
                Ease(Spec.Ease ?? "none", x, out double _, out rate);
            }
            return PlanSpeed(DistanceAt(t)) * (float)(rate * _planLength / Duration);
        }

        /// <summary>The attitude the motion asks for at t, before any lag: [pitch, yaw, roll] degrees, plus the bank and load seen.</summary>
        private Vector3 TargetAttitude(float t, out float bankDegrees, out float load)
        {
            float s = DistanceAt(t);
            Vector3 fwd = TangentAt(s);
            SpeedAt(t, out float v, out float dv);
            Vector3 accel = dv * fwd + v * v * CurvatureAt(s);
            Vector3 up = Vector3.UnitY;
            Vector3 lift = accel + new Vector3(0, G, 0);
            load = lift.Length() / G;
            if (!Spec.PitchWithPath)
            {
                Vector3 flat = new Vector3(fwd.X, 0, fwd.Z);
                if (flat.LengthSquared() > 1e-8f) fwd = Vector3.Normalize(flat);
            }
            lift -= Vector3.Dot(lift, fwd) * fwd;
            Vector3 flatUp = up - Vector3.Dot(up, fwd) * fwd;
            flatUp = flatUp.LengthSquared() < 1e-8f ? Vector3.UnitZ : Vector3.Normalize(flatUp);
            float bank = 0f;
            if (Spec.Bank && lift.LengthSquared() > 1e-8f)
            {
                Vector3 liftN = Vector3.Normalize(lift);
                bank = (float)Math.Atan2(Vector3.Dot(Vector3.Cross(flatUp, liftN), fwd), Vector3.Dot(flatUp, liftN));
                float limit = Spec.MaxBank * (float)Math.PI / 180f;
                bank = Math.Max(-limit, Math.Min(limit, bank * Spec.BankScale));
            }
            Vector3 upB = Vector3.Transform(flatUp, Quaternion.CreateFromAxisAngle(fwd, bank));
            Vector3 right = Vector3.Normalize(Vector3.Cross(upB, fwd));
            upB = Vector3.Cross(fwd, right);
            Matrix4x4 m = new Matrix4x4(right.X, right.Y, right.Z, 0, upB.X, upB.Y, upB.Z, 0, fwd.X, fwd.Y, fwd.Z, 0, 0, 0, 0, 1);
            bankDegrees = bank * 180f / (float)Math.PI;
            Vector3 e = EulerOf(m);
            return new Vector3(e.X, e.Y, e.Z + Spec.Roll);
        }

        /// <summary>[pitch, yaw, roll] degrees of a rotation as the engine reads a transform (yaw = atan2(m31, m33), pitch = -asin(m32), roll = atan2(m12, m22)).</summary>
        public static Vector3 EulerOf(Matrix4x4 m)
        {
            const float r = 180f / (float)Math.PI;
            float yaw = (float)Math.Atan2(m.M31, m.M33) * r;
            float pitch = -(float)Math.Asin(Math.Max(-1f, Math.Min(1f, m.M32))) * r;
            float roll = (float)Math.Atan2(m.M12, m.M22) * r;
            return new Vector3(pitch, yaw, roll);
        }

        private static float Wrap(float a)
        {
            double d = a;
            while (d > 180) d -= 360;
            while (d < -180) d += 360;
            return (float)d;
        }

        private void BuildAttitude()
        {
            if (Spec.AttitudeLag <= 0f) return;
            //Critically damped: settles in about twice the lag
            float omega = 2f / Spec.AttitudeLag;
            _attitudeRate = 240f;
            //A loop runs a lap first so the spring is already in step as the real lap starts
            float lead = _periodic ? Duration : Math.Min(2f * Spec.AttitudeLag, 2f);
            _attitudeFrom = Start - lead;
            int n = (int)Math.Ceiling((End - _attitudeFrom) * _attitudeRate) + 2;
            _attitude = new Vector3[n];
            float dt = 1f / _attitudeRate;
            Vector3 x = TargetAttitude(_periodic ? _attitudeFrom : Start, out _, out _), vel = Vector3.Zero;
            for (int i = 0; i < n; i++)
            {
                float t = _attitudeFrom + i * dt;
                Vector3 target = TargetAttitude(_periodic ? t : Math.Max(Start, t), out _, out _);
                target = new Vector3(x.X + Wrap(target.X - x.X), x.Y + Wrap(target.Y - x.Y), x.Z + Wrap(target.Z - x.Z));
                Vector3 acc = omega * omega * (target - x) - 2 * omega * vel;
                vel += acc * dt;
                x += vel * dt;
                _attitude[i] = x;
            }
        }

        /// <summary>[pitch, yaw, roll] degrees at time t.</summary>
        public Vector3 AttitudeAt(float t)
        {
            if (_attitude == null) return TargetAttitude(t, out _, out _);
            //A loop: every lap is the steady lap tabulated
            if (_periodic)
            {
                float into = (t - Start) % Duration;
                if (into < 0) into += Duration;
                t = Start + into;
            }
            float f = (Math.Max(_attitudeFrom, Math.Min(End, t)) - _attitudeFrom) * _attitudeRate;
            int i = Math.Max(0, Math.Min(_attitude.Length - 2, (int)Math.Floor(f)));
            return Vector3.Lerp(_attitude[i], _attitude[i + 1], f - i);
        }

        /// <summary>The pose at time t (seconds into the animation).</summary>
        public KeyPose PoseAt(float t) => new KeyPose(PositionAt(t), AttitudeAt(t));

        private void Measure()
        {
            int steps = (int)Math.Min(20000, Math.Max(50, Math.Ceiling(Duration * 60)));
            MinSpeedSeen = float.MaxValue;
            Vector3 previousForward = Vector3.Zero;
            for (int i = 0; i <= steps; i++)
            {
                float t = Start + Duration * i / steps;
                TargetAttitude(t, out float bank, out float load);
                if (Math.Abs(bank) > Math.Abs(MaxBank)) { MaxBank = bank; MaxBankAt = t; }
                MaxLoad = Math.Max(MaxLoad, load);
                SpeedAt(t, out float v, out float _);
                if (i > 0 && i < steps) MinSpeedSeen = Math.Min(MinSpeedSeen, v);
                MaxSpeedSeen = Math.Max(MaxSpeedSeen, v);
                Vector3 att = AttitudeAt(t);
                MaxPitch = Math.Max(MaxPitch, Math.Abs(att.X));
                Matrix4x4 m = Matrix4x4.CreateFromYawPitchRoll(att.Y * (float)Math.PI / 180f, att.X * (float)Math.PI / 180f, att.Z * (float)Math.PI / 180f);
                Vector3 forward = new Vector3(m.M31, m.M32, m.M33);
                if (i > 0)
                {
                    float turn = (float)(Math.Acos(Math.Max(-1.0, Math.Min(1.0, Vector3.Dot(previousForward, forward)))) * 180.0 / Math.PI) / (Duration / steps);
                    MaxTurnRate = Math.Max(MaxTurnRate, turn);
                }
                previousForward = forward;
            }
            if (MinSpeedSeen == float.MaxValue) MinSpeedSeen = 0f;
            if (Math.Abs(MaxBank) >= Spec.MaxBank - 0.5f && Spec.Bank)
                Warnings.Add("The bank reaches its limit of " + Spec.MaxBank + " degrees (at " + Math.Round(MaxBankAt, 2) + " s): the turn there is too tight for the speed - slow down, widen the turn, or raise max_bank.");
            if (MaxPitch > 80f)
                Warnings.Add("The path climbs or dives almost straight (" + Math.Round(MaxPitch) + " degrees): the attitude flips near vertical.");
        }
        #endregion
    }
}
