using CATHODE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Transform = CATHODE.HavokPackfile.SampledTransform;

namespace OpenCAGE.ClipTools
{
    /// <summary>
    /// Puts a clip's travel where the game wants it. The game draws a character at marker * inverse(REFERENCE_ROOT) and its capsule (what
    /// animation LOD, culling and the AI measure from) sits there too, so a body that travels while REFERENCE_ROOT stays still leaves the
    /// capsule behind. The retail shape: the root (EXTRACT) at identity, a capsule frame following the body (hips over the ground, turned
    /// the way it faces), REFERENCE_ROOT carrying its inverse and the body stored relative to it. Where the body is drawn doesn't change.
    /// </summary>
    public static class TravelAtMarker
    {
        public sealed class Options
        {
            /// <summary>
            /// Measure the travel from the first frame (the default, for a clip placed where the character starts): the clip is moved - not
            /// turned - so its first capsule is on the marker, the file's forward along the marker's, and the capsule's heading counts from its
            /// first. Off keeps the file's own origin as the marker: for clips authored together in one scene.
            /// </summary>
            public bool FromFirstFrame = true;

            /// <summary>The capsule turns with the body (the retail shape: a cover entry's REFERENCE_ROOT turns 180 degrees with it).</summary>
            public bool FollowTurns = true;

            /// <summary>The capsule rises and falls with the feet (a jump, a fall, a ladder); off keeps it at the marker's height.</summary>
            public bool FollowHeight = true;

            /// <summary>Seconds either side to average the body's heading over: the hips twist with every step (+-10 degrees in a walk, more in a run).</summary>
            public float TurnSmoothing = 0.5f;

            /// <summary>
            /// Motion on the root (EXTRACT) is the body's travel: true for a file (whoever made it moved the character that way, though the
            /// game would cancel it), false for a clip already in the game (drawn exactly as the game draws it, the root cancelling out).
            /// </summary>
            public bool RootIsTravel = true;
        }

        public sealed class Result
        {
            public List<List<Transform>> Poses;

            /// <summary>Where the capsule is at each frame, in the marker's space (an upright frame: +Y up, its +Z the way it faces).</summary>
            public List<Matrix4x4> Capsule = new List<Matrix4x4>();

            /// <summary>How far the capsule travels along its path (m), where it ends up and how far it has turned (degrees, signed, unwrapped).</summary>
            public float PathLength;
            public Vector3 End;
            public float Turn;

            /// <summary>How far the body ever gets from its capsule across the ground (m), before and after: LOD and culling measure from the capsule.</summary>
            public float BodyFromCapsuleBefore, BodyFromCapsuleAfter;

            /// <summary>The lowest and highest the capsule goes (m) relative to the marker.</summary>
            public float Lowest, Highest;

            public string Problem;
            public List<string> Notes = new List<string>();
            public bool Ok { get { return Poses != null && Problem == null; } }

            public string Describe()
            {
                if (!Ok) return Problem;
                return "The capsule travels " + PathLength.ToString("0.00") + " m, ending " + Fmt(End) + " from the marker"
                    + (Math.Abs(Turn) >= 1 ? ", turned " + Turn.ToString("0") + " degrees" : "")
                    + (Highest - Lowest > 0.05f ? ", " + Lowest.ToString("0.00") + " to " + Highest.ToString("0.00") + " m up" : "")
                    + ". The body stays within " + BodyFromCapsuleAfter.ToString("0.00") + " m of its capsule (without this: up to " + BodyFromCapsuleBefore.ToString("0.00") + " m).";
            }
        }

        /// <summary>
        /// Rebuild a clip's frames in the travelling shape. <paramref name="poses"/> are local transforms per frame per bone, the root as
        /// the clip or file gives it (its travel, if any, is kept as the body's). Nothing else of the pose changes.
        /// </summary>
        public static Result Convert(List<List<Transform>> poses, Skeleton rig, float frameDuration, Options options)
        {
            options = options ?? new Options();
            Result result = new Result();
            if (poses == null || poses.Count == 0) { result.Problem = "There are no frames."; return result; }
            if (rig == null) { result.Problem = "There is no rig."; return result; }
            int root = RigBones.Root(rig), reference = RigBones.ReferenceRoot(rig), body = RigBones.Body(rig);
            if (reference < 0) { result.Problem = rig.Name + " has no REFERENCE_ROOT bone, so the game has no capsule frame to move: only character rigs travel this way."; return result; }
            if (rig.Bones[reference].ParentIndex != root) { result.Problem = rig.Name + "'s REFERENCE_ROOT does not hang off its root, which every character rig's does."; return result; }
            if (body < 0) { result.Problem = rig.Name + " has no body bone (HIPS) to follow."; return result; }

            ClipFrames frames = ClipFrames.FromPoses(poses, rig, frameDuration);
            List<Matrix4x4[]> marker = Bodies(frames, root, reference, options.RootIsTravel);
            List<Matrix4x4> capsule = Capsules(marker, rig, frames.FrameDuration, options);

            /* Measured from the first frame: the clip is moved (not turned) so its first capsule is on the marker and the file's forward stays
             * the marker's. Turning it to the hips' first heading would swing it off its line: a stance's hips are twisted from the way it goes. */
            if (options.FromFirstFrame)
            {
                Vector3 start = capsule[0].Translation;
                Matrix4x4 shift = Matrix4x4.CreateTranslation(-start);
                float yaw0 = ClipFrames.YawOf(capsule[0]);
                for (int f = 0; f < marker.Count; f++)
                {
                    for (int b = 0; b < marker[f].Length; b++) marker[f][b] = marker[f][b] * shift;
                    capsule[f] = ClipFrames.Upright(ClipFrames.YawOf(capsule[f]) - yaw0, capsule[f].Translation - start);
                }
            }

            result.Poses = Rebuild(frames, marker, capsule, root, reference);
            result.Capsule = capsule;
            Measure(result, marker, capsule, ClipSplice.BodyMask(rig));
            return result;
        }

        /// <summary>
        /// Every bone's place in the marker's space. With <paramref name="rootIsTravel"/> off, exactly as the game draws it (model times the
        /// inverse of REFERENCE_ROOT's model, the root cancelling out). With it on, the root's motion since the first frame, which the game
        /// would cancel, is kept as the body's travel (not the first frame itself: an export's root can rest turned 180 degrees).
        /// </summary>
        public static List<Matrix4x4[]> Bodies(ClipFrames frames, int root, int reference, bool rootIsTravel)
        {
            List<Matrix4x4[]> bodies = new List<Matrix4x4[]>(frames.Count);
            Matrix4x4 firstRoot = ClipFrames.Invert(ClipFrames.ToMatrix(frames.Locals[0][root]));
            for (int f = 0; f < frames.Count; f++)
            {
                Matrix4x4[] model = frames.Model(f);
                Matrix4x4 rootNow = ClipFrames.ToMatrix(frames.Locals[f][root]);
                if (!rootIsTravel)
                {
                    Matrix4x4 inverse = ClipFrames.Invert(model[reference]);
                    for (int b = 0; b < model.Length; b++) model[b] = model[b] * inverse;
                    bodies.Add(model);
                    continue;
                }
                Matrix4x4 travel = rootNow * firstRoot;
                //model already carries rootNow; swap it for its motion since the first frame, then take REFERENCE_ROOT out
                Matrix4x4 strip = ClipFrames.Invert(rootNow) * travel * ClipFrames.Invert(ClipFrames.ToMatrix(frames.Locals[f][reference]));
                for (int b = 0; b < model.Length; b++) model[b] = model[b] * strip;
                bodies.Add(model);
            }
            return bodies;
        }

        /// <summary>
        /// The capsule's frame on every frame: over the body (HIPS) across the ground; at the feet's height, less their rest height above the
        /// floor; turned the way the body faces, averaged over <see cref="Options.TurnSmoothing"/> either side.
        /// </summary>
        public static List<Matrix4x4> Capsules(List<Matrix4x4[]> marker, Skeleton rig, float frameDuration, Options options)
        {
            int body = RigBones.Body(rig);
            int[] feet = RigBones.Feet(rig);
            Matrix4x4[] rest = ClipFrames.ModelOf(ClipFrames.RestLocals(rig), rig);
            float footRest = feet.Length == 0 ? rest[body].Translation.Y : feet.Min(x => rest[x].Translation.Y);
            float bodyRest = rest[body].Translation.Y;
            Matrix4x4 restTurn = rest[body];
            restTurn.Translation = Vector3.Zero;
            Matrix4x4 unturn = ClipFrames.Invert(restTurn);

            int n = marker.Count;
            float[] yaw = new float[n];
            float last = 0;
            for (int f = 0; f < n; f++)
            {
                Matrix4x4 turn = marker[f][body];
                turn.Translation = Vector3.Zero;
                //the body's forward, as it was at rest (+Z), carried round by how far the hips have turned from rest
                Vector3 forward = Vector3.TransformNormal(Vector3.UnitZ, unturn * turn);
                float y = new Vector2(forward.X, forward.Z).Length() > 0.2f ? (float)(Math.Atan2(forward.X, forward.Z) * 180.0 / Math.PI) : last;
                if (f > 0)
                {
                    while (y - yaw[f - 1] > 180) y -= 360;
                    while (y - yaw[f - 1] < -180) y += 360;
                }
                yaw[f] = last = y;
            }
            int window = Math.Max(0, (int)Math.Round(options.TurnSmoothing / Math.Max(1e-4f, frameDuration)));
            float[] smooth = new float[n];
            for (int f = 0; f < n; f++)
            {
                float sum = 0;
                int count = 0;
                for (int g = Math.Max(0, f - window); g <= Math.Min(n - 1, f + window); g++) { sum += yaw[g]; count++; }
                smooth[f] = options.FollowTurns ? sum / count : yaw[0];
            }

            List<Matrix4x4> capsules = new List<Matrix4x4>(n);
            for (int f = 0; f < n; f++)
            {
                Vector3 hips = marker[f][body].Translation;
                float height = 0;
                if (options.FollowHeight)
                    height = feet.Length == 0 ? hips.Y - bodyRest : feet.Min(x => marker[f][x].Translation.Y) - footRest;
                capsules.Add(ClipFrames.Upright(smooth[f], new Vector3(hips.X, height, hips.Z)));
            }
            return capsules;
        }

        /// <summary>
        /// A clip's own capsule on every frame, in its marker's space: inverse(REFERENCE_ROOT), made upright (its heading and position). A
        /// shipped clip's capsule already follows its body, and clips made to follow one another (leap in, leap, leap out) meet on it exactly,
        /// so the splicer lines segments up on these rather than on the hips, whose heading swings with every step.
        /// </summary>
        public static List<Matrix4x4> OwnCapsules(ClipFrames frames, int reference)
        {
            List<Matrix4x4> capsules = new List<Matrix4x4>(frames.Count);
            for (int f = 0; f < frames.Count; f++)
            {
                Matrix4x4 capsule = ClipFrames.Invert(frames.Model(f)[reference]);
                capsules.Add(ClipFrames.Upright(ClipFrames.YawOf(capsule), capsule.Translation));
            }
            return capsules;
        }

        /// <summary>
        /// The frames in the travelling shape: the root at identity (left to the engine), REFERENCE_ROOT the capsule's inverse, every other bone
        /// on the root (the body, the IK targets) relative to the capsule; deeper bones keep their own locals.
        /// </summary>
        public static List<List<Transform>> Rebuild(ClipFrames frames, List<Matrix4x4[]> marker, List<Matrix4x4> capsule, int root, int reference)
        {
            List<int> onRoot = RigBones.ChildrenOfRoot(frames.Rig);
            List<List<Transform>> poses = new List<List<Transform>>(frames.Count);
            for (int f = 0; f < frames.Count; f++)
            {
                Matrix4x4 inverse = ClipFrames.Invert(capsule[f]);
                List<Transform> pose = frames.Locals[f].Select(x => ClipFrames.Make(x.Translation, ClipFrames.Normal(x.Rotation), x.Scale)).ToList();
                pose[root] = ClipFrames.Identity;
                pose[reference] = ClipFrames.FromMatrix(inverse);
                foreach (int child in onRoot)
                    if (child != reference) pose[child] = ClipFrames.FromMatrix(marker[f][child] * inverse);
                poses.Add(pose);
            }
            return poses;
        }

        private static void Measure(Result result, List<Matrix4x4[]> marker, List<Matrix4x4> capsule, bool[] body)
        {
            float length = 0, before = 0, after = 0, low = float.MaxValue, high = float.MinValue;
            Vector3 start = capsule[0].Translation;
            for (int f = 0; f < capsule.Count; f++)
            {
                Vector3 at = capsule[f].Translation;
                if (f > 0) length += (at - capsule[f - 1].Translation).Length();
                low = Math.Min(low, at.Y);
                high = Math.Max(high, at.Y);
                for (int b = 0; b < marker[f].Length; b++)
                {
                    if (!body[b]) continue;
                    Vector3 p = marker[f][b].Translation;
                    before = Math.Max(before, new Vector2(p.X - start.X, p.Z - start.Z).Length());
                    after = Math.Max(after, new Vector2(p.X - at.X, p.Z - at.Z).Length());
                }
            }
            result.PathLength = length;
            result.End = capsule[capsule.Count - 1].Translation;
            result.Turn = UnwrappedTurn(capsule);
            result.BodyFromCapsuleBefore = before;
            result.BodyFromCapsuleAfter = after;
            result.Lowest = low;
            result.Highest = high;
            if (before < 0.25f) result.Notes.Add("The body hardly travels (" + before.ToString("0.00") + " m at most from where it starts), so there is little for the capsule to follow.");
        }

        private static float UnwrappedTurn(List<Matrix4x4> capsule)
        {
            float total = 0;
            for (int f = 1; f < capsule.Count; f++)
            {
                float d = ClipFrames.YawOf(capsule[f]) - ClipFrames.YawOf(capsule[f - 1]);
                while (d > 180) d -= 360;
                while (d < -180) d += 360;
                total += d;
            }
            return total;
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.X.ToString("0.00") + ", " + v.Y.ToString("0.00") + ", " + v.Z.ToString("0.00") + ")";
        }
    }
}
