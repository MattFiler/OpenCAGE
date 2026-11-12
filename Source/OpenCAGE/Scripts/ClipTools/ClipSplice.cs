using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Transform = CATHODE.HavokPackfile.SampledTransform;

namespace OpenCAGE.ClipTools
{
    /// <summary>
    /// The clip splicer: a new character clip assembled from frame ranges of shipped ones, each retimed and optionally held. Every frame
    /// is split into the clip's own upright capsule (inverse REFERENCE_ROOT) and the body relative to it; each segment's capsule is laid
    /// onto the previous one's where the crossfade starts, then the two are crossfaded. The result is in the retail travelling shape
    /// (<see cref="TravelAtMarker"/>). Planted feet are held through the joins (<see cref="FootLock"/>); hand locks are not done.
    /// </summary>
    public static class ClipSplice
    {
        public sealed class Segment
        {
            public Animation.ClipReference Clip;

            /// <summary>Frames of the clip to use, inclusive (-1 = its last).</summary>
            public int From = 0, To = -1;

            /// <summary>Playback speed: 0.5 plays it at half speed, so twice as long.</summary>
            public float Speed = 1;

            /// <summary>Seconds to crossfade from the previous segment (overlapping its end). Ignored on the first.</summary>
            public float Blend = 0.2f;

            /// <summary>Seconds to hold the segment's last frame before the next one.</summary>
            public float Hold = 0;

            /// <summary>Carry on from where the previous segment got to (the default), or keep the segment's own placement (pieces of one scene).</summary>
            public bool Continue = true;

            /// <summary>An extra turn (degrees about +Y; positive turns +Z towards +X) and offset (in the previous capsule's frame) at the join.</summary>
            public float Turn = 0;
            public Vector3 Offset = Vector3.Zero;

            /// <summary>
            /// Extra travel over the segment (m, in its own capsule's frame: +Z forward), spread evenly over it. Shipped traversal clips jump
            /// on the spot and leave the distance to the game's traversal code, so a splice that should cover ground says how far.
            /// </summary>
            public Vector3 Carry = Vector3.Zero;

            public override string ToString()
            {
                return (Clip?.Name ?? "?") + " [" + From + ".." + (To < 0 ? "end" : To.ToString()) + "]" + (Math.Abs(Speed - 1) > 1e-3f ? " x" + Speed.ToString("0.##") : "");
            }
        }

        public sealed class Options
        {
            /// <summary>Keep the travel (the default): REFERENCE_ROOT carries it, the capsule follows. Off plays the whole thing on the spot.</summary>
            public bool Travel = true;

            /// <summary>Start on the marker facing along it (the default); off keeps the first segment's own placement as the origin.</summary>
            public bool FromFirstFrame = true;

            /// <summary>Frames per second of the result, or 0 for the first segment's rate.</summary>
            public float FrameRate = 0;

            /// <summary>
            /// Hold a foot that is planted where a crossfade starts where it stands until it lifts (two-bone IK on the leg, the foot's own
            /// angle kept), easing off over 0.2 s - so a blend between two stances doesn't slide it.
            /// </summary>
            public bool LockFeet = true;
        }

        public sealed class Piece
        {
            public Segment Segment;
            public int FirstFrame, LastFrame;
            public float Start, End;
            public float Travel;
            public int BlendFrames;

            /// <summary>At the join into this piece: the pose pop back to back with no blend (m), and the worst frame-to-frame step after splicing.</summary>
            public float Pop, JoinStep;

            /// <summary>At the join into this piece: how far a foot planted where the crossfade starts has slid by its end (m).</summary>
            public float FootDrift;
        }

        public sealed class Result
        {
            public Skeleton Rig;
            public string RigName;
            public float FrameDuration;
            public List<List<Transform>> Poses;
            public List<Matrix4x4> Capsule = new List<Matrix4x4>();
            public List<Piece> Pieces = new List<Piece>();

            /// <summary>The largest jump any bone makes from one frame to the next where two segments meet (m, marker space): spliced, and laid end to end with no alignment or blend.</summary>
            public float WorstJoinStep, WorstNaiveStep;

            /// <summary>The largest step any bone makes between two frames anywhere else (m): what the motion itself does.</summary>
            public float WorstOwnStep;

            /// <summary>The most a foot planted where a crossfade starts has moved across the floor by its end (m): a foot sliding in the blend.</summary>
            public float WorstFootDrift;

            /// <summary>With <see cref="Options.LockFeet"/>: how many foot locks were made (a foot held through one join).</summary>
            public int LockedFeet;

            public string Problem;
            public List<string> Notes = new List<string>();
            public bool Ok { get { return Poses != null && Problem == null; } }
            public int Frames { get { return Poses?.Count ?? 0; } }
            public float Duration { get { return Frames > 1 ? (Frames - 1) * FrameDuration : 0; } }
        }

        /// <summary>Build the spliced clip's frames. Nothing is added anywhere.</summary>
        public static Result Build(Animation animations, IList<Segment> segments, Options options)
        {
            options = options ?? new Options();
            Result result = new Result();
            if (segments == null || segments.Count == 0) { result.Problem = "There are no segments to splice."; return result; }
            for (int i = 0; i < segments.Count; i++)
            {
                Segment s = segments[i];
                if (s?.Clip == null || !s.Clip.Playable) { result.Problem = "Segment " + (i + 1) + " has no clip that plays."; return result; }
                if (s.Clip.Additive) { result.Problem = "Segment " + (i + 1) + " ('" + s.Clip.Name + "') is additive: it holds changes to lay over a pose, not a pose (additive clips belong on CHR_PlaySecondaryAnimation)."; return result; }
                if (s.Speed < 0.05f || s.Speed > 20) { result.Problem = "Segment " + (i + 1) + "'s speed " + s.Speed + " is outside 0.05 - 20."; return result; }
                if (s.Hold < 0 || s.Blend < 0) { result.Problem = "Segment " + (i + 1) + " has a negative blend or hold."; return result; }
            }
            result.RigName = segments[0].Clip.Skeleton;
            List<string> rigs = segments.Select(x => x.Clip.Skeleton).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (rigs.Count != 1)
            {
                result.Problem = "The segments are authored on different rigs (" + string.Join(", ", rigs) + "): a splice is built on one rig, so pick clips authored on the same one (the clip list shows each clip's rig).";
                return result;
            }
            result.Rig = animations?.GetSkeleton(result.RigName)?.Skeleton;
            if (result.Rig == null) { result.Problem = "The rig '" + result.RigName + "' is not in ANIMATION.PAK."; return result; }
            Skeleton rig = result.Rig;
            int root = RigBones.Root(rig), reference = RigBones.ReferenceRoot(rig);
            if (reference < 0 || RigBones.Body(rig) < 0) { result.Problem = result.RigName + " is not a character rig (no REFERENCE_ROOT or HIPS): only character clips can be spliced."; return result; }
            List<int> onRoot = RigBones.ChildrenOfRoot(rig);

            //every segment sampled at the output rate, in the travelling shape
            List<ClipFrames> sources = new List<ClipFrames>();
            foreach (Segment s in segments) sources.Add(ClipFrames.FromClip(s.Clip, rig, s.From, s.To));
            float dt = options.FrameRate > 0 ? 1f / options.FrameRate : sources[0].FrameDuration;
            result.FrameDuration = dt;

            List<Matrix4x4> carrier = new List<Matrix4x4>();
            List<Transform[]> relative = new List<Transform[]>();
            bool[] measured = BodyMask(rig);
            List<(int from, int to)> joins = new List<(int, int)>();
            for (int i = 0; i < segments.Count; i++)
            {
                Segment s = segments[i];
                ClipFrames src = sources[i];
                int count = Math.Max(1, (int)Math.Round(src.Duration / s.Speed / dt) + 1);
                ClipFrames timed = new ClipFrames { Rig = rig, FrameDuration = dt };
                for (int k = 0; k < count; k++) timed.Locals.Add(src.At(Math.Min(src.Duration, k * dt * s.Speed)));
                int hold = (int)Math.Round(s.Hold / dt);
                for (int k = 0; k < hold; k++) timed.Locals.Add(timed.Locals[timed.Count - 1]);

                List<Matrix4x4[]> marker = TravelAtMarker.Bodies(timed, root, reference, false);
                List<Matrix4x4> capsule = TravelAtMarker.OwnCapsules(timed, reference);
                if (s.Carry != Vector3.Zero && capsule.Count > 1)
                {
                    //spread over the segment's own frames (not a hold), along its first capsule's heading
                    int moving = Math.Max(1, count - 1);
                    Matrix4x4 heading = capsule[0];
                    heading.Translation = Vector3.Zero;
                    Vector3 total = Vector3.TransformNormal(s.Carry, heading);
                    for (int k = 0; k < capsule.Count; k++)
                    {
                        Vector3 add = total * Math.Min(1f, (float)k / moving);
                        Matrix4x4 c = capsule[k];
                        c.Translation += add;
                        capsule[k] = c;
                        Matrix4x4 shift = Matrix4x4.CreateTranslation(add);
                        for (int b = 0; b < marker[k].Length; b++) marker[k][b] = marker[k][b] * shift;
                    }
                }
                List<Transform[]> rel = new List<Transform[]>();
                for (int k = 0; k < timed.Count; k++)
                {
                    Matrix4x4 inverse = ClipFrames.Invert(capsule[k]);
                    Transform[] pose = (Transform[])timed.Locals[k].Clone();
                    pose[root] = ClipFrames.Identity;
                    pose[reference] = ClipFrames.Identity;
                    foreach (int child in onRoot)
                        if (child != reference) pose[child] = ClipFrames.FromMatrix(marker[k][child] * inverse);
                    rel.Add(pose);
                }

                Piece piece = new Piece { Segment = s };
                if (i == 0 || carrier.Count == 0)
                {
                    piece.FirstFrame = 0;
                    carrier.AddRange(capsule);
                    relative.AddRange(rel);
                }
                else
                {
                    int blend = Math.Min((int)Math.Round(s.Blend / dt), Math.Min(carrier.Count - 1, timed.Count - 1));
                    blend = Math.Max(0, blend);
                    piece.BlendFrames = blend;
                    int at = carrier.Count - 1 - blend;          //the join: the next segment's first frame lands here, then fades in
                    if (blend == 0) at = carrier.Count - 1;
                    Matrix4x4 align = Matrix4x4.Identity;
                    if (s.Continue)
                    {
                        Matrix4x4 target = ClipFrames.Upright(s.Turn, s.Offset) * carrier[at];
                        align = ClipFrames.Invert(capsule[0]) * target;
                    }
                    for (int k = 0; k < capsule.Count; k++) capsule[k] = capsule[k] * align;
                    //with foot locks, a foot planted on both sides of the join is put down on the same spot (the capsule nudged across the floor)
                    if (s.Continue && options.LockFeet)
                    {
                        Vector3 nudge = FootLock.PlantShift(rig, carrier, relative, at, rel[0], capsule[0]);
                        if (nudge != Vector3.Zero) for (int k = 0; k < capsule.Count; k++) capsule[k] = capsule[k] * Matrix4x4.CreateTranslation(nudge);
                    }
                    //the pop the two would make back to back with no blend: the last pose and the next first pose over the same capsule
                    piece.Pop = WorstStep(ClipFrames.ModelOf(relative[relative.Count - 1], rig), ClipFrames.ModelOf(rel[0], rig), measured);
                    result.WorstNaiveStep = Math.Max(result.WorstNaiveStep, piece.Pop);
                    joins.Add((at, at + blend + 1));
                    piece.FirstFrame = blend == 0 ? carrier.Count : at;
                    if (blend == 0)
                    {
                        //a hard cut, the first frame of this one taking the place of the last of the previous (they are the same moment)
                        carrier[carrier.Count - 1] = capsule[0];
                        relative[relative.Count - 1] = rel[0];
                        piece.FirstFrame = carrier.Count - 1;
                        for (int k = 1; k < capsule.Count; k++) { carrier.Add(capsule[k]); relative.Add(rel[k]); }
                    }
                    else
                    {
                        for (int j = 0; j <= blend; j++)
                        {
                            float w = Smooth((float)j / blend);
                            int o = at + j;
                            carrier[o] = ClipFrames.BlendUpright(carrier[o], capsule[j], w);
                            relative[o] = ClipFrames.Blend(relative[o], rel[j], w);
                        }
                        for (int k = blend + 1; k < capsule.Count; k++) { carrier.Add(capsule[k]); relative.Add(rel[k]); }
                    }
                }
                piece.LastFrame = carrier.Count - 1;
                piece.Start = piece.FirstFrame * dt;
                piece.End = piece.LastFrame * dt;
                result.Pieces.Add(piece);
            }

            //planted feet held where they stand through each crossfade (two-bone IK on the legs)
            if (options.LockFeet) result.LockedFeet = FootLock.Apply(rig, carrier, relative, joins, dt);

            //the whole thing measured from where it starts, or on the spot
            Matrix4x4 rebase = options.FromFirstFrame ? ClipFrames.Invert(carrier[0]) : Matrix4x4.Identity;
            List<List<Transform>> poses = new List<List<Transform>>(carrier.Count);
            List<Matrix4x4[]> drawn = new List<Matrix4x4[]>(carrier.Count);
            for (int f = 0; f < carrier.Count; f++)
            {
                Matrix4x4 capsule = options.Travel ? carrier[f] * rebase : Matrix4x4.Identity;
                List<Transform> pose = relative[f].Select(x => ClipFrames.Make(x.Translation, ClipFrames.Normal(x.Rotation), x.Scale)).ToList();
                pose[root] = ClipFrames.Identity;
                pose[reference] = ClipFrames.FromMatrix(ClipFrames.Invert(capsule));
                poses.Add(pose);
                result.Capsule.Add(capsule);
                drawn.Add(Draw(pose, rig, reference));
            }
            result.Poses = poses;
            foreach (Piece piece in result.Pieces)
            {
                float travel = 0;
                for (int f = piece.FirstFrame + 1; f <= piece.LastFrame; f++) travel += (result.Capsule[f].Translation - result.Capsule[f - 1].Translation).Length();
                piece.Travel = travel;
            }

            //how smooth the joins came out: the steps from one frame to the next across each join (its crossfade and a frame either side)
            HashSet<int> joinSet = new HashSet<int>();
            foreach ((int from, int to) in joins) for (int f = from; f <= to + 1; f++) joinSet.Add(f);
            for (int f = 1; f < drawn.Count; f++)
            {
                float step = WorstStep(drawn[f - 1], drawn[f], measured);
                if (joinSet.Contains(f)) result.WorstJoinStep = Math.Max(result.WorstJoinStep, step);
                else result.WorstOwnStep = Math.Max(result.WorstOwnStep, step);
            }
            for (int i = 1; i < result.Pieces.Count && i - 1 < joins.Count; i++)
                for (int f = Math.Max(1, joins[i - 1].from); f <= Math.Min(drawn.Count - 1, joins[i - 1].to + 1); f++)
                    result.Pieces[i].JoinStep = Math.Max(result.Pieces[i].JoinStep, WorstStep(drawn[f - 1], drawn[f], measured));
            //how far a foot planted where a crossfade starts has moved across the floor by its end
            for (int i = 1; i < result.Pieces.Count && i - 1 < joins.Count; i++)
            {
                result.Pieces[i].FootDrift = FootLock.Drift(rig, drawn, result.Capsule, joins[i - 1].from, joins[i - 1].to);
                result.WorstFootDrift = Math.Max(result.WorstFootDrift, result.Pieces[i].FootDrift);
            }
            if (joins.Count != 0 && result.WorstJoinStep > Math.Max(0.15f, result.WorstOwnStep * 2.5f))
                result.Notes.Add("A join still jumps " + (result.WorstJoinStep * 100).ToString("0") + " cm in one frame (the clips' own motion moves at most " + (result.WorstOwnStep * 100).ToString("0") + " cm): lengthen its blend, or pick frames where the poses are closer.");
            return result;
        }

        /// <summary>Where every bone is drawn for a pose, in the marker's space (model times the inverse of REFERENCE_ROOT).</summary>
        public static Matrix4x4[] Draw(List<Transform> pose, Skeleton rig, int reference)
        {
            Matrix4x4[] model = ClipFrames.ModelOf(pose.ToArray(), rig);
            Matrix4x4 inverse = ClipFrames.Invert(model[reference]);
            for (int b = 0; b < model.Length; b++) model[b] = model[b] * inverse;
            return model;
        }

        private static float WorstStep(Matrix4x4[] a, Matrix4x4[] b, bool[] measured)
        {
            float worst = 0;
            for (int i = 0; i < a.Length; i++)
                if (measured[i]) worst = Math.Max(worst, (a[i].Translation - b[i].Translation).Length());
            return worst;
        }

        /// <summary>The body and everything hanging off it (not the root, REFERENCE_ROOT or the IK targets, which are placement, not limbs).</summary>
        public static bool[] BodyMask(Skeleton rig)
        {
            int body = RigBones.Body(rig);
            bool[] mask = new bool[rig.Bones.Count];
            for (int i = 0; i < mask.Length; i++)
                for (int at = i; at >= 0; at = rig.Bones[at].ParentIndex)
                    if (at == body) { mask[i] = true; break; }
            return mask;
        }

        private static float Smooth(float x)
        {
            x = Math.Max(0, Math.Min(1, x));
            return x * x * (3 - 2 * x);
        }
    }

    /// <summary>
    /// Foot locks for the splicer: a foot planted where a crossfade starts is held on that spot (two-bone IK on the thigh and shin, the
    /// foot keeping its own angle) until it lifts in the motion it fades into, then eased off over 0.2 s. Also measures foot slide.
    /// </summary>
    public static class FootLock
    {
        const float Low = 0.06f;          //a foot within this of its rest height above the capsule's floor is on the floor
        const float Still = 0.012f;       //and moving less than this a frame across it is planted
        const float Release = 0.2f;       //seconds to ease a lock off
        const float Longest = 0.6f;       //seconds a lock may run on past its crossfade, waiting for the foot to lift

        private sealed class Leg { public int Thigh, Shin, Foot; public float Rest; }

        private static List<Leg> Legs(Skeleton rig)
        {
            List<Leg> legs = new List<Leg>();
            Matrix4x4[] rest = ClipFrames.ModelOf(ClipFrames.RestLocals(rig), rig);
            foreach (string side in new[] { "LEFT", "RIGHT" })
            {
                Leg leg = new Leg { Thigh = RigBones.Find(rig, side + "UPLEG"), Shin = RigBones.Find(rig, side + "LEG"), Foot = RigBones.Find(rig, side + "FOOT") };
                if (leg.Thigh < 0 || leg.Shin < 0 || leg.Foot < 0) continue;
                if (rig.Bones[leg.Shin].ParentIndex != leg.Thigh || rig.Bones[leg.Foot].ParentIndex != leg.Shin) continue;
                leg.Rest = rest[leg.Foot].Translation.Y;
                legs.Add(leg);
            }
            return legs;
        }

        /// <summary>Lock the planted feet through each join; returns how many locks were made. <paramref name="relative"/> is edited in place.</summary>
        public static int Apply(Skeleton rig, List<Matrix4x4> carrier, List<HavokPackfile.SampledTransform[]> relative, List<(int from, int to)> joins, float dt)
        {
            List<Leg> legs = Legs(rig);
            int locks = 0;
            int release = Math.Max(1, (int)Math.Round(Release / dt)), longest = (int)Math.Round(Longest / dt);
            foreach ((int from, int to) in joins)
            {
                if (from < 1 || from >= carrier.Count) continue;
                foreach (Leg leg in legs)
                {
                    Vector3 at = World(rig, relative[from], carrier[from], leg.Foot), was = World(rig, relative[from - 1], carrier[from - 1], leg.Foot);
                    if (Height(at, carrier[from], leg) > Low || Flat(at - was) > Still) continue;
                    //held through the crossfade, and on until the foot lifts (or moves off) in what it fades into
                    int end = Math.Min(to, carrier.Count - 1);
                    while (end + 1 < carrier.Count && end + 1 <= to + longest)
                    {
                        Vector3 next = World(rig, relative[end + 1], carrier[end + 1], leg.Foot), now = World(rig, relative[end], carrier[end], leg.Foot);
                        if (Height(next, carrier[end + 1], leg) > Low || Flat(next - now) > Still) break;
                        end++;
                    }
                    for (int f = from; f <= Math.Min(carrier.Count - 1, end + release); f++)
                    {
                        float w = f <= end ? 1f : 1f - (float)(f - end) / (release + 1);
                        w = w * w * (3 - 2 * w);
                        Vector3 foot = World(rig, relative[f], carrier[f], leg.Foot);
                        Vector3 target = Vector3.Lerp(foot, at, w);
                        relative[f] = Solve(rig, relative[f], leg, Vector3.Transform(target, ClipFrames.Invert(carrier[f])));
                    }
                    locks++;
                }
            }
            return locks;
        }

        /// <summary>
        /// Where to nudge the next segment across the floor so a foot planted at the join (frame <paramref name="at"/> of what is built so far) and
        /// planted at the next segment's first frame lands on the same spot: the average over such feet, at most 0.3 m; zero when none.
        /// </summary>
        public static Vector3 PlantShift(Skeleton rig, List<Matrix4x4> carrier, List<HavokPackfile.SampledTransform[]> relative, int at, HavokPackfile.SampledTransform[] next, Matrix4x4 nextCapsule)
        {
            if (at < 1 || at >= carrier.Count) return Vector3.Zero;
            Vector3 sum = Vector3.Zero;
            int count = 0;
            foreach (Leg leg in Legs(rig))
            {
                Vector3 was = World(rig, relative[at - 1], carrier[at - 1], leg.Foot), now = World(rig, relative[at], carrier[at], leg.Foot);
                if (Height(now, carrier[at], leg) > Low || Flat(now - was) > Still) continue;
                Vector3 then = World(rig, next, nextCapsule, leg.Foot);
                if (Height(then, nextCapsule, leg) > Low) continue;
                sum += new Vector3(now.X - then.X, 0, now.Z - then.Z);
                count++;
            }
            if (count == 0) return Vector3.Zero;
            Vector3 shift = sum / count;
            return shift.Length() > 0.3f ? Vector3.Zero : shift;
        }

        /// <summary>The most a foot planted at frame <paramref name="from"/> (on the floor, still) has moved across it by frame <paramref name="to"/> (m).</summary>
        public static float Drift(Skeleton rig, List<Matrix4x4[]> drawn, List<Matrix4x4> capsule, int from, int to)
        {
            if (from < 1 || from >= drawn.Count) return 0;
            to = Math.Min(to, drawn.Count - 1);
            float worst = 0;
            foreach (Leg leg in Legs(rig))
            {
                Vector3 was = drawn[from - 1][leg.Foot].Translation, now = drawn[from][leg.Foot].Translation;
                if (Height(now, capsule[from], leg) > Low || Flat(now - was) > Still) continue;
                worst = Math.Max(worst, Flat(drawn[to][leg.Foot].Translation - now));
            }
            return worst;
        }

        private static float Height(Vector3 foot, Matrix4x4 capsule, Leg leg) { return foot.Y - capsule.Translation.Y - leg.Rest; }
        private static float Flat(Vector3 v) { return new Vector2(v.X, v.Z).Length(); }

        private static Vector3 World(Skeleton rig, HavokPackfile.SampledTransform[] pose, Matrix4x4 capsule, int bone)
        {
            return Vector3.Transform(ClipFrames.ModelOf(pose, rig)[bone].Translation, capsule);
        }

        /// <summary>Two-bone IK in the capsule's frame: the foot to <paramref name="target"/>, the knee kept in its own bend plane, the foot's angle kept.</summary>
        private static HavokPackfile.SampledTransform[] Solve(Skeleton rig, HavokPackfile.SampledTransform[] pose, Leg leg, Vector3 target)
        {
            Matrix4x4[] model = ClipFrames.ModelOf(pose, rig);
            Vector3 hip = model[leg.Thigh].Translation, knee = model[leg.Shin].Translation, ankle = model[leg.Foot].Translation;
            float a = (knee - hip).Length(), b = (ankle - knee).Length();
            if (a < 1e-4f || b < 1e-4f) return pose;
            Vector3 toTarget = target - hip;
            float d = Math.Max(Math.Abs(a - b) + 1e-3f, Math.Min(a + b - 1e-3f, toTarget.Length()));
            if (toTarget.LengthSquared() < 1e-8f) return pose;
            Vector3 u = Vector3.Normalize(toTarget);
            //the knee bends the way it does now: its offset across the hip-to-target line
            Vector3 side = (knee - hip) - Vector3.Dot(knee - hip, u) * u;
            if (side.LengthSquared() < 1e-8f) side = Vector3.Cross(u, Vector3.UnitX);
            Vector3 v = Vector3.Normalize(side);
            float cos = Math.Max(-1f, Math.Min(1f, (a * a + d * d - b * b) / (2 * a * d)));
            Vector3 kneeNew = hip + u * (a * cos) + v * (a * (float)Math.Sqrt(Math.Max(0, 1 - cos * cos)));
            Vector3 ankleWant = hip + u * d;

            Matrix4x4 turnThigh = About(hip, Arc(knee - hip, kneeNew - hip));
            Matrix4x4 thigh = model[leg.Thigh] * turnThigh;
            Matrix4x4 shin = model[leg.Shin] * turnThigh;
            Vector3 ankleNow = Vector3.Transform(ankle, turnThigh);
            Matrix4x4 turnShin = About(kneeNew, Arc(ankleNow - kneeNew, ankleWant - kneeNew));
            shin = shin * turnShin;
            Matrix4x4 foot = model[leg.Foot];
            foot.Translation = ankleWant;

            HavokPackfile.SampledTransform[] result = (HavokPackfile.SampledTransform[])pose.Clone();
            int parent = rig.Bones[leg.Thigh].ParentIndex;
            result[leg.Thigh] = ClipFrames.FromMatrix(thigh * (parent >= 0 ? ClipFrames.Invert(model[parent]) : Matrix4x4.Identity));
            result[leg.Shin] = ClipFrames.FromMatrix(shin * ClipFrames.Invert(thigh));
            result[leg.Foot] = ClipFrames.FromMatrix(foot * ClipFrames.Invert(shin));
            return result;
        }

        private static Matrix4x4 About(Vector3 point, Quaternion turn)
        {
            return Matrix4x4.CreateTranslation(-point) * Matrix4x4.CreateFromQuaternion(turn) * Matrix4x4.CreateTranslation(point);
        }

        /// <summary>The shortest turn taking direction <paramref name="from"/> to <paramref name="to"/>.</summary>
        private static Quaternion Arc(Vector3 from, Vector3 to)
        {
            if (from.LengthSquared() < 1e-10f || to.LengthSquared() < 1e-10f) return Quaternion.Identity;
            from = Vector3.Normalize(from); to = Vector3.Normalize(to);
            float dot = Vector3.Dot(from, to);
            if (dot > 0.999999f) return Quaternion.Identity;
            if (dot < -0.999999f)
            {
                Vector3 axis = Vector3.Cross(from, Vector3.UnitX);
                if (axis.LengthSquared() < 1e-6f) axis = Vector3.Cross(from, Vector3.UnitY);
                return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), (float)Math.PI);
            }
            Vector3 c = Vector3.Cross(from, to);
            return Quaternion.Normalize(new Quaternion(c.X, c.Y, c.Z, 1 + dot));
        }
    }
}
