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
    /// A character clip as the engine plays it: one local transform per bone per frame, on one rig. No editor dependencies.
    /// A channel a track leaves out is the Havok default (0, identity, 1), not the bone's rest value; a bone with no track takes its rest
    /// local; the root (EXTRACT) is identity unless the clip stores it. With an identity root MALE stands +Y up, faces +Z, his left at -X.
    /// The game draws a character at marker * inverse(REFERENCE_ROOT); retail travelling clips (vaults, cover entries, ladders, cutscenes)
    /// keep the body over the capsule and put the inverse of the travel on REFERENCE_ROOT.
    /// </summary>
    public sealed class ClipFrames
    {
        public Skeleton Rig;
        public float FrameDuration = 1f / 30f;
        public List<Transform[]> Locals = new List<Transform[]>();

        public int Count { get { return Locals.Count; } }
        public int Bones { get { return Rig?.Bones.Count ?? 0; } }
        public float Duration { get { return Count > 1 ? (Count - 1) * FrameDuration : 0; } }

        /// <summary>A clip's frames <paramref name="first"/> to <paramref name="last"/> (inclusive; -1 = the last) on its own rig. Throws on a clip that can't be sampled.</summary>
        public static ClipFrames FromClip(Animation.ClipReference clip, Skeleton rig, int first = 0, int last = -1)
        {
            HavokPackfile.AnimationClip animation = clip?.Animation;
            if (animation == null || clip.Section?.Havok == null) throw new InvalidOperationException("'" + clip + "' has no animation data to sample.");
            if (rig == null) throw new InvalidOperationException("There is no rig to sample '" + clip + "' on.");
            if (animation.Additive) throw new InvalidOperationException("'" + clip.Name + "' is additive: it holds changes to lay over another pose, not a pose of its own.");
            if (last < 0 || last >= animation.FrameCount) last = animation.FrameCount - 1;
            first = Math.Max(0, Math.Min(first, last));

            ClipFrames frames = new ClipFrames { Rig = rig, FrameDuration = animation.FrameDuration > 0 ? animation.FrameDuration : 1f / 30f };
            for (int f = first; f <= last; f++)
            {
                Transform[] pose = RestLocals(rig);
                List<Transform> tracks = clip.Section.Havok.Sample(animation, f);
                if (tracks == null) throw new InvalidOperationException("Frame " + f + " of '" + clip.Name + "' did not decode.");
                for (int t = 0; t < tracks.Count && t < animation.TrackToBone.Count; t++)
                {
                    int bone = animation.TrackToBone[t];
                    if (bone < 0 || bone >= pose.Length) continue;
                    Transform s = tracks[t];
                    pose[bone] = Make(s.HasTranslation ? s.Translation : Vector3.Zero,
                                      s.HasRotation ? Quaternion.Normalize(s.Rotation) : Quaternion.Identity,
                                      s.HasScale ? s.Scale : Vector3.One);
                }
                frames.Locals.Add(pose);
            }
            return frames;
        }

        /// <summary>Frames read some other way (an import's poses): one list per frame, a transform per bone, the root as given.</summary>
        public static ClipFrames FromPoses(List<List<Transform>> poses, Skeleton rig, float frameDuration)
        {
            ClipFrames frames = new ClipFrames { Rig = rig, FrameDuration = frameDuration > 0 ? frameDuration : 1f / 30f };
            foreach (List<Transform> pose in poses)
            {
                Transform[] copy = new Transform[rig.Bones.Count];
                for (int b = 0; b < copy.Length; b++)
                    copy[b] = b < pose.Count ? Make(pose[b].Translation, Normal(pose[b].Rotation), pose[b].Scale) : RestLocal(rig, b);
                frames.Locals.Add(copy);
            }
            return frames;
        }

        /// <summary>What <see cref="Animation.AddClip(Animation.AnimationSet, string, string, string, List{short}, List{List{Transform}}, float, bool, string)"/> takes: track i drives bone i.</summary>
        public List<List<Transform>> ToPoses()
        {
            return Locals.Select(x => x.Select(t => Make(t.Translation, Normal(t.Rotation), t.Scale)).ToList()).ToList();
        }

        /// <summary>Bone transforms relative to the rig's root at one frame, the root as the frame stores it.</summary>
        public Matrix4x4[] Model(int frame)
        {
            return ModelOf(Locals[frame], Rig);
        }

        /// <summary>Where each bone is drawn in the space of the marker the clip plays at: model times the inverse of REFERENCE_ROOT's model.</summary>
        public Matrix4x4[] MarkerSpace(int frame)
        {
            Matrix4x4[] model = Model(frame);
            int reference = RigBones.ReferenceRoot(Rig);
            if (reference < 0) return model;
            Matrix4x4 inverse = Invert(model[reference]);
            for (int b = 0; b < model.Length; b++) model[b] = model[b] * inverse;
            return model;
        }

        /// <summary>The pose at a time in seconds, between frames: translations and scales lerped, rotations slerped. Held at the ends.</summary>
        public Transform[] At(float seconds)
        {
            if (Count == 0) return null;
            float at = FrameDuration > 0 ? seconds / FrameDuration : 0;
            if (at <= 0) return (Transform[])Locals[0].Clone();
            if (at >= Count - 1) return (Transform[])Locals[Count - 1].Clone();
            int a = (int)Math.Floor(at);
            float w = at - a;
            if (w < 1e-5f) return (Transform[])Locals[a].Clone();
            return Blend(Locals[a], Locals[a + 1], w);
        }

        #region helpers
        public static Transform Make(Vector3 t, Quaternion r, Vector3 s)
        {
            return new Transform { Translation = t, Rotation = r, Scale = s, HasTranslation = true, HasRotation = true, HasScale = true };
        }

        public static Transform Identity { get { return Make(Vector3.Zero, Quaternion.Identity, Vector3.One); } }

        public static Transform RestLocal(Skeleton rig, int bone)
        {
            return Make(rig.Bones[bone].Position, Normal(rig.Bones[bone].Rotation), rig.Bones[bone].ScaleXYZ);
        }

        /// <summary>The rest pose as the engine plays a clip that leaves every bone untracked: the rig's rest locals, the root at identity.</summary>
        public static Transform[] RestLocals(Skeleton rig)
        {
            Transform[] pose = new Transform[rig.Bones.Count];
            for (int b = 0; b < pose.Length; b++) pose[b] = rig.Bones[b].ParentIndex < 0 ? Identity : RestLocal(rig, b);
            return pose;
        }

        public static Matrix4x4 ToMatrix(Transform t)
        {
            return Matrix4x4.CreateScale(t.Scale) * Matrix4x4.CreateFromQuaternion(Normal(t.Rotation)) * Matrix4x4.CreateTranslation(t.Translation);
        }

        /// <summary>A matrix back to a transform; scales within a hair of one are snapped, so a bone that never scaled doesn't start to.</summary>
        public static Transform FromMatrix(Matrix4x4 m)
        {
            if (!Matrix4x4.Decompose(m, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
            {
                //A sheared matrix: keep its translation, and its rotation from the normalised axes
                translation = m.Translation;
                scale = Vector3.One;
                Matrix4x4 r = m;
                r.Translation = Vector3.Zero;
                rotation = Quaternion.CreateFromRotationMatrix(r);
            }
            if (Math.Abs(scale.X - 1) < 1e-4f && Math.Abs(scale.Y - 1) < 1e-4f && Math.Abs(scale.Z - 1) < 1e-4f) scale = Vector3.One;
            return Make(translation, Normal(rotation), scale);
        }

        public static Matrix4x4[] ModelOf(Transform[] locals, Skeleton rig)
        {
            Matrix4x4[] model = new Matrix4x4[locals.Length];
            for (int b = 0; b < locals.Length; b++)
            {
                int parent = rig.Bones[b].ParentIndex;
                Matrix4x4 local = ToMatrix(locals[b]);
                model[b] = parent >= 0 && parent < b ? local * model[parent] : local;
            }
            return model;
        }

        public static Transform[] Blend(Transform[] a, Transform[] b, float w)
        {
            Transform[] r = new Transform[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = Blend(a[i], b[i], w);
            return r;
        }

        public static Transform Blend(Transform a, Transform b, float w)
        {
            Quaternion qa = Normal(a.Rotation), qb = Normal(b.Rotation);
            if (Quaternion.Dot(qa, qb) < 0) qb = -qb;
            return Make(Vector3.Lerp(a.Translation, b.Translation, w), Quaternion.Normalize(Quaternion.Slerp(qa, qb, w)), Vector3.Lerp(a.Scale, b.Scale, w));
        }

        public static Quaternion Normal(Quaternion q)
        {
            return q.LengthSquared() > 1e-12f ? Quaternion.Normalize(q) : Quaternion.Identity;
        }

        public static Matrix4x4 Invert(Matrix4x4 m)
        {
            return Matrix4x4.Invert(m, out Matrix4x4 inverse) ? inverse : Matrix4x4.Identity;
        }

        /// <summary>Turn about +Y then move: a capsule's frame (it never pitches or rolls).</summary>
        public static Matrix4x4 Upright(float yawDegrees, Vector3 position)
        {
            return Matrix4x4.CreateRotationY((float)(yawDegrees * Math.PI / 180.0)) * Matrix4x4.CreateTranslation(position);
        }

        /// <summary>The heading of a frame's +Z in degrees about +Y (0 = +Z, 90 = +X).</summary>
        public static float YawOf(Matrix4x4 m)
        {
            Vector3 forward = Vector3.TransformNormal(Vector3.UnitZ, m);
            return (float)(Math.Atan2(forward.X, forward.Z) * 180.0 / Math.PI);
        }

        /// <summary>A blend of two upright frames: positions lerped, headings by the short way round.</summary>
        public static Matrix4x4 BlendUpright(Matrix4x4 a, Matrix4x4 b, float w)
        {
            float ya = YawOf(a), yb = YawOf(b);
            float d = yb - ya;
            while (d > 180) d -= 360;
            while (d < -180) d += 360;
            return Upright(ya + d * w, Vector3.Lerp(a.Translation, b.Translation, w));
        }
        #endregion
    }

    /// <summary>The bones the travel tools lean on, found by name on any character rig (MALE:HIPS, ALIEN:HIPS...).</summary>
    public static class RigBones
    {
        public static int Find(Skeleton rig, params string[] names)
        {
            if (rig == null) return -1;
            foreach (string name in names)
                for (int i = 0; i < rig.Bones.Count; i++)
                {
                    string bone = rig.Bones[i].Name ?? "";
                    int colon = bone.LastIndexOf(':');
                    if (string.Equals(colon < 0 ? bone : bone.Substring(colon + 1), name, StringComparison.OrdinalIgnoreCase)) return i;
                }
            return -1;
        }

        public static int ReferenceRoot(Skeleton rig) { return Find(rig, "REFERENCE_ROOT"); }

        public static int Root(Skeleton rig)
        {
            if (rig == null) return -1;
            for (int i = 0; i < rig.Bones.Count; i++) if (rig.Bones[i].ParentIndex < 0) return i;
            return -1;
        }

        /// <summary>The body: HIPS, else the bone the most others hang off (the pelvis on any rig).</summary>
        public static int Body(Skeleton rig)
        {
            if (rig == null) return -1;
            int hips = Find(rig, "HIPS", "PELVIS");
            if (hips >= 0) return hips;
            int reference = ReferenceRoot(rig), best = -1, most = 0;
            for (int i = 0; i < rig.Bones.Count; i++)
            {
                if (i == reference || rig.Bones[i].ParentIndex < 0) continue;
                int count = 0;
                for (int c = 0; c < rig.Bones.Count; c++)
                    for (int at = rig.Bones[c].ParentIndex; at >= 0; at = rig.Bones[at].ParentIndex)
                        if (at == i) { count++; break; }
                if (count > most) { most = count; best = i; }
            }
            return best;
        }

        public static int[] Feet(Skeleton rig)
        {
            int left = Find(rig, "LEFTFOOT", "L_ANKLE", "LEFTANKLE"), right = Find(rig, "RIGHTFOOT", "R_ANKLE", "RIGHTANKLE");
            return new[] { left, right }.Where(x => x >= 0).ToArray();
        }

        /// <summary>The bones hanging straight off the root: the body, the IK targets and REFERENCE_ROOT on a character rig.</summary>
        public static List<int> ChildrenOfRoot(Skeleton rig)
        {
            int root = Root(rig);
            List<int> children = new List<int>();
            for (int i = 0; i < rig.Bones.Count; i++) if (i != root && rig.Bones[i].ParentIndex == root) children.Add(i);
            return children;
        }
    }
}
