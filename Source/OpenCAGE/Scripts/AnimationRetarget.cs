using CATHODE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE
{
    /// <summary>
    /// Bring an animation across from a rig the game has never heard of - in practice an Unreal
    /// Engine mannequin, which is what most animation found online is built on.
    ///
    /// <see cref="AnimationImport"/> matches a file's nodes to a rig's bones by name, which needs the
    /// file to already be on that rig. This is the other case: the file is on a different skeleton
    /// entirely, with different names, different proportions and a different idea of which way is
    /// left. There is no <c>SKELE\MAPS</c> entry to lean on for a rig the game has never seen, so the
    /// corrective is worked out from the two rest poses instead.
    /// </summary>
    public static class AnimationRetarget
    {
        #region THE MAP
        /* Which of the file's joints stands for which of the rig's bones: the mannequin's names on
         * the left, CATHODE's on the right, one side of the body - the _l rows are mirrored
         * automatically.
         *
         * CATHODE names its bones the same way on every rig it ships, so this one table serves them
         * all: ALIEN, MALE and FEMALE all have HIPS, LEFTUPLEG, LEFTFOREARMROLL and the rest. What
         * differs between them is only which bones are present. A bone the rig has and the map
         * doesn't - the alien's tail, spikes and jaw, a human's armour bones - keeps its rest shape
         * and rides its parent, and a bone the map names and the rig lacks is skipped. Neither is an
         * error.
         *
         * The spine is handled separately, because the rigs disagree on how many bones it takes. */
        private static readonly string[,] Pairs = new string[,]
        {
            { "pelvis",             "HIPS" },
            { "neck_01",            "NECK" },
            { "head",               "HEAD" },

            { "clavicle_l",         "LEFTSHOULDER" },
            { "upperarm_l",         "LEFTARM" },
            { "upperarm_twist_01_l","LEFTARMROLL" },
            { "lowerarm_l",         "LEFTFOREARM" },
            { "lowerarm_twist_01_l","LEFTFOREARMROLL" },
            { "hand_l",             "LEFTHAND" },

            { "index_01_l",         "LEFTHANDINDEX1" },
            { "index_02_l",         "LEFTHANDINDEX2" },
            { "index_03_l",         "LEFTHANDINDEX3" },
            { "middle_01_l",        "LEFTHANDMIDDLE1" },
            { "middle_02_l",        "LEFTHANDMIDDLE2" },
            { "middle_03_l",        "LEFTHANDMIDDLE3" },
            { "ring_01_l",          "LEFTHANDRING1" },
            { "ring_02_l",          "LEFTHANDRING2" },
            { "ring_03_l",          "LEFTHANDRING3" },
            { "pinky_01_l",         "LEFTHANDPINKY1" },
            { "pinky_02_l",         "LEFTHANDPINKY2" },
            { "pinky_03_l",         "LEFTHANDPINKY3" },
            { "thumb_01_l",         "LEFTHANDTHUMB1" },
            { "thumb_02_l",         "LEFTHANDTHUMB2" },
            { "thumb_03_l",         "LEFTHANDTHUMB3" },

            { "thigh_l",            "LEFTUPLEG" },
            { "calf_l",             "LEFTLEG" },
            { "foot_l",             "LEFTFOOT" },
            { "ball_l",             "LEFTTOEBASE" },
        };

        /* The joints that have to be there for a file to be worth offering this for. Deliberately the
         * load-bearing ones rather than all of them: a rip often loses the twists and the fingers. */
        private static readonly string[] Landmarks =
        {
            "pelvis", "spine_01", "neck_01", "head",
            "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
            "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
            "thigh_l", "calf_l", "foot_l", "ball_l",
            "thigh_r", "calf_r", "foot_r", "ball_r",
        };
        #endregion

        #region WHETHER TO OFFER IT
        /// <summary>
        /// Whether a file looks like it was built on an Unreal mannequin. Judged on how many of the
        /// joints this can actually use are present, so a rig that merely borrows a name or two
        /// doesn't qualify.
        /// </summary>
        public static bool Looks(Assimp.Scene scene, out string what)
        {
            what = "";
            if (scene == null) return false;

            Rig rig = Read(scene, null);
            int found = Landmarks.Count(x => rig.Find(x) >= 0);
            if (found * 4 < Landmarks.Length * 3) return false;      //three quarters of them

            what = "This looks like an Unreal Engine mannequin rig - " + found + " of its "
                 + Landmarks.Length + " main joints are here, under names like '"
                 + string.Join("', '", Landmarks.Where(x => rig.Find(x) >= 0).Take(3).Select(x => Name(rig, x))) + "'.";
            return true;
        }

        /// <summary>
        /// Whether a rig can be retargeted onto. It needs the landmarks the change of basis is fitted
        /// from - which way is up, which side is left, and which way it faces. Every rig CATHODE
        /// ships for a character has them; a prop rig or a weapon does not, and shouldn't be offered.
        /// </summary>
        public static bool Supports(Skeleton rig)
        {
            if (rig == null || rig.Bones.Count == 0) return false;
            foreach (string bone in new[] { "HIPS", "NECK", "LEFTSHOULDER", "RIGHTSHOULDER",
                                            "LEFTFOOT", "RIGHTFOOT", "LEFTTOEBASE", "RIGHTTOEBASE" })
                if (Bone(rig, bone) < 0) return false;
            return true;
        }

        private static string Name(Rig rig, string joint)
        {
            int at = rig.Find(joint);
            return at < 0 ? joint : rig.Names[at];
        }
        #endregion

        #region THE RESULT
        public class Reading
        {
            public List<List<HavokPackfile.SampledTransform>> Poses;

            /// <summary>How many of the rig's bones the clip ends up driving.</summary>
            public int Driven;

            /// <summary>The change of basis that was fitted, and whether it turned out to be a mirror.</summary>
            public Matrix4x4 Frame = Matrix4x4.Identity;
            public bool Mirrored;

            /// <summary>How the two rigs' sizes compare, and how far apart their proportions are.</summary>
            public double Scale = 1, Spread;

            public List<string> Notes = new List<string>();
            public string Problem;

            public bool Ok { get { return Poses != null && Problem == null; } }
        }

        /// <summary>
        /// Retarget an animation onto a rig. <paramref name="frames"/> is the frame count to sample
        /// onto, which the caller has already decided along with the rate.
        /// </summary>
        public static Reading Build(Assimp.Scene scene, Assimp.Animation animation, Skeleton target, int frames, bool hands = false)
        {
            Reading reading = new Reading();
            if (!Supports(target)) { reading.Problem = target?.Name + " isn't a rig this can retarget onto."; return reading; }
            if (scene == null || animation == null || frames < 1) { reading.Problem = "There's no animation to retarget."; return reading; }

            Rig source = Read(scene, animation);
            if (source.Names.Count == 0) { reading.Problem = "That file has no skeleton in it."; return reading; }

            Sample(source, animation, frames);

            Dictionary<int, int> map = BuildMap(source, target, hands, out List<string> skipped);
            if (map.Count < 6)
            {
                reading.Problem = "That file's skeleton doesn't look like an Unreal mannequin - only "
                    + map.Count + " of its joints could be matched up.";
                return reading;
            }
            reading.Notes.AddRange(skipped);

            Matrix4x4[] rest = RestModel(target);
            int hips = Bone(target, "HIPS"), neck = Bone(target, "NECK");
            double rigSpan = (rest[neck].Translation - rest[hips].Translation).Length();
            double sourceSpan = (source.RestWorld[map[neck]].Translation - source.RestWorld[map[hips]].Translation).Length();
            reading.Scale = sourceSpan > 0 ? rigSpan / sourceSpan : 1;

            Matrix4x4 frame = FitFrame(source, target, rest, map);
            reading.Frame = frame;
            reading.Mirrored = Determinant(frame) < 0;
            reading.Spread = Residual(source, target, rest, map, reading.Scale, frame);
            reading.Driven = map.Count;

            reading.Poses = Retarget(source, target, map, rest, frame, reading.Scale);
            reading.Notes.Add(map.Count + " of " + target.Bones.Count + " bones are driven; the rest keep the shape they rest in.");
            reading.Notes.Add("The two rigs are built to different proportions - " + (reading.Spread * 100).ToString("0")
                + " cm apart on average - so " + target.Name + " keeps its own bone lengths and only the angles come across.");
            return reading;
        }
        #endregion

        #region THE SOURCE RIG
        /* The file's rig, flattened: every joint under the skeleton root, with its rest transform and
         * whatever the clip does to it. Nothing in here knows about CATHODE. */
        private class Rig
        {
            public List<string> Names = new List<string>();
            public List<int> Parents = new List<int>();
            public List<Matrix4x4> RestLocal = new List<Matrix4x4>();
            public Dictionary<string, int> Index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            /* Everything above the skeleton root - the exporter's own scene transform. Folded in, so
             * world orientations are the ones a viewer would see. */
            public Matrix4x4 Above = Matrix4x4.Identity;

            public Matrix4x4[] RestWorld;
            public List<Matrix4x4[]> World = new List<Matrix4x4[]>();

            /* Rotations are chained as quaternions rather than read back out of the composed world
             * matrix. A mannequin carries scale on its twist and fingertip joints, and once that has
             * been multiplied down the chain the matrix is sheared - decomposing it then gives a
             * rotation that is not merely approximate but can flip between frames. */
            public Quaternion[] RestSpin;
            public List<Quaternion[]> Spin = new List<Quaternion[]>();

            public int Find(string name)
            {
                return Index.TryGetValue(name, out int i) ? i : -1;
            }
        }

        /* Take the whole node tree. Which part of it is the skeleton is decided by the map rather
         * than by guessing at a root, so a mesh node in the middle of it costs nothing. */
        private static Rig Read(Assimp.Scene scene, Assimp.Animation animation)
        {
            Rig rig = new Rig();
            if (scene?.RootNode != null) Collect(scene.RootNode, -1, rig);
            if (animation == null) return rig;

            rig.RestWorld = Compose(rig, rig.RestLocal.ToArray());
            rig.RestSpin = Spin(rig, rig.RestLocal.ToArray());
            return rig;
        }

        /* Pose the source rig on a uniform grid of frames.
         *
         * Every channel is read at a TIME, never at a key index. Exporters drop keys that repeat what
         * came before, so one file can hold channels with 1, 183 and 196 keys for the same six
         * seconds - taking the nth key of each takes them from different moments, and the further
         * apart the counts the worse it gets. */
        private static void Sample(Rig rig, Assimp.Animation animation, int frames)
        {
            Dictionary<int, Assimp.NodeAnimationChannel> channels = new Dictionary<int, Assimp.NodeAnimationChannel>();
            foreach (Assimp.NodeAnimationChannel channel in animation.NodeAnimationChannels)
            {
                int joint = rig.Find(channel.NodeName);
                if (joint >= 0) channels[joint] = channel;
            }
            if (channels.Count == 0) return;

            double first = channels.Values.Where(x => x.RotationKeyCount > 0).Select(x => x.RotationKeys[0].Time).DefaultIfEmpty(0).Min();
            double last = channels.Values.Where(x => x.RotationKeyCount > 0)
                .Select(x => x.RotationKeys[x.RotationKeyCount - 1].Time).DefaultIfEmpty(0).Max();

            for (int frame = 0; frame < frames; frame++)
            {
                double at = frames > 1 ? first + (last - first) * frame / (frames - 1) : first;
                Matrix4x4[] local = new Matrix4x4[rig.Names.Count];
                for (int joint = 0; joint < local.Length; joint++)
                {
                    if (!channels.TryGetValue(joint, out Assimp.NodeAnimationChannel channel)) { local[joint] = rig.RestLocal[joint]; continue; }

                    Matrix4x4.Decompose(rig.RestLocal[joint], out Vector3 scale, out Quaternion rotation, out Vector3 translation);
                    translation = At(channel.PositionKeys, at, translation);
                    scale = At(channel.ScalingKeys, at, scale);
                    rotation = At(channel.RotationKeys, at, rotation);

                    local[joint] = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
                }
                rig.World.Add(Compose(rig, local));
                rig.Spin.Add(Spin(rig, local));
            }
        }

        private static void Collect(Assimp.Node node, int parent, Rig rig)
        {
            int index = rig.Names.Count;
            rig.Names.Add(node.Name);
            rig.Parents.Add(parent);
            rig.RestLocal.Add(ToNumerics(node.Transform));

            /* An exporter usually decorates a joint's name - "pelvis_SomeMeshName", "mixamorig:pelvis" -
             * so index it under what it is as well as under what it is called. */
            Remember(rig, node.Name, index);
            int colon = node.Name.LastIndexOf(':');
            if (colon >= 0) Remember(rig, node.Name.Substring(colon + 1), index);

            foreach (Assimp.Node child in node.Children) Collect(child, index, rig);
        }

        private static void Remember(Rig rig, string name, int index)
        {
            if (!rig.Index.ContainsKey(name)) rig.Index[name] = index;

            /* "pelvis_F_MED_NeonCat_Body_T_03.ao" is the pelvis. Only whole segments count, so
             * thigh_twist_01_l is never mistaken for thigh_l. */
            for (int at = name.IndexOf('_'); at > 0; at = name.IndexOf('_', at + 1))
            {
                string prefix = name.Substring(0, at);
                if (!rig.Index.ContainsKey(prefix)) rig.Index[prefix] = index;
            }
        }

        private static Matrix4x4[] Compose(Rig rig, Matrix4x4[] local)
        {
            Matrix4x4[] world = new Matrix4x4[local.Length];
            for (int i = 0; i < local.Length; i++)
                world[i] = local[i] * (rig.Parents[i] < 0 ? rig.Above : world[rig.Parents[i]]);
            return world;
        }

        /* The same chain in rotations only, so a joint's scale never reaches its children's orientation. */
        private static Quaternion[] Spin(Rig rig, Matrix4x4[] local)
        {
            Quaternion above = RotationOf(rig.Above);
            Quaternion[] spin = new Quaternion[local.Length];
            for (int i = 0; i < local.Length; i++)
            {
                Quaternion parent = rig.Parents[i] < 0 ? above : spin[rig.Parents[i]];
                spin[i] = Quaternion.Normalize(parent * RotationOf(local[i]));
            }
            return spin;
        }

        private static Vector3 At(List<Assimp.VectorKey> keys, double when, Vector3 fallback)
        {
            if (keys == null || keys.Count == 0) return fallback;
            if (keys.Count == 1 || when <= keys[0].Time) return ToNumerics(keys[0].Value);
            if (when >= keys[keys.Count - 1].Time) return ToNumerics(keys[keys.Count - 1].Value);

            int i = Before(keys.Count, x => keys[x].Time, when);
            double span = keys[i + 1].Time - keys[i].Time;
            return Vector3.Lerp(ToNumerics(keys[i].Value), ToNumerics(keys[i + 1].Value), span <= 0 ? 0 : (float)((when - keys[i].Time) / span));
        }

        private static Quaternion At(List<Assimp.QuaternionKey> keys, double when, Quaternion fallback)
        {
            if (keys == null || keys.Count == 0) return fallback;
            if (keys.Count == 1 || when <= keys[0].Time) return ToNumerics(keys[0].Value);
            if (when >= keys[keys.Count - 1].Time) return ToNumerics(keys[keys.Count - 1].Value);

            int i = Before(keys.Count, x => keys[x].Time, when);
            double span = keys[i + 1].Time - keys[i].Time;
            return Quaternion.Slerp(ToNumerics(keys[i].Value), ToNumerics(keys[i + 1].Value), span <= 0 ? 0 : (float)((when - keys[i].Time) / span));
        }

        private static int Before(int count, Func<int, double> time, double when)
        {
            int low = 0, high = count - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (time(middle) <= when) low = middle; else high = middle;
            }
            return low;
        }
        #endregion

        #region BUILDING THE MAP
        private static Dictionary<int, int> BuildMap(Rig source, Skeleton target, bool hands, out List<string> skipped)
        {
            skipped = new List<string>();
            Dictionary<int, int> map = new Dictionary<int, int>();       //target bone -> source joint
            for (int i = 0; i < Pairs.GetLength(0); i++)
            {
                /* Everything from the wrist out is left alone unless asked for, and so are the twist
                 * bones. In a downloaded clip they are routinely the noisiest thing in the file -
                 * wrists crossing 40 degrees in a single frame on a third of the clip, fingers worse
                 * still, while the upper arm swinging them does it a handful of times. That is noise
                 * in the rip rather than dancing, and it flickers. */
                if (!hands && (Pairs[i, 1].Contains("HAND") || Pairs[i, 1].Contains("ROLL"))) continue;

                Add(source, target, Pairs[i, 0], Pairs[i, 1], map, skipped);
                if (Pairs[i, 0].EndsWith("_l"))
                    Add(source, target, Pairs[i, 0].Substring(0, Pairs[i, 0].Length - 2) + "_r",
                        "RIGHT" + Pairs[i, 1].Substring(4), map, skipped);
            }
            MapSpine(source, target, map);
            return map;
        }

        /* The spines are different lengths - five bones on the mannequin, four on MALE and FEMALE,
         * three on ALIEN - so they are paired by how far along the chain each sits rather than by
         * name. Nothing is lost by the target having fewer: each bone takes its source's orientation
         * in MODEL space, which already carries every bend below it, so the bend the skipped ones
         * contributed still arrives at the top of the chain. */
        private static void MapSpine(Rig source, Skeleton target, Dictionary<int, int> map)
        {
            List<int> theirs = new List<int>();
            for (int i = 1; ; i++)
            {
                int joint = source.Find("spine_" + i.ToString("00"));
                if (joint < 0) break;
                theirs.Add(joint);
            }

            List<int> ours = new List<int>();
            for (int i = 0; ; i++)
            {
                int bone = Bone(target, i == 0 ? "SPINE" : "SPINE" + i);
                if (bone < 0) break;
                ours.Add(bone);
            }
            if (theirs.Count == 0 || ours.Count == 0) return;

            for (int i = 0; i < ours.Count; i++)
            {
                //the last of ours always takes the last of theirs, so the whole bend lands on it
                int at = ours.Count == 1 ? theirs.Count - 1
                    : (int)Math.Round((double)i * (theirs.Count - 1) / (ours.Count - 1));
                map[ours[i]] = theirs[Math.Min(at, theirs.Count - 1)];
            }
        }

        /* A pair is only worth mentioning when the FILE lacks the joint. A rig not having the bone is
         * ordinary - MALE has no tail, ALIEN no shoulder armour - and saying so every time would bury
         * the case that means something. */
        private static void Add(Rig source, Skeleton target, string from, string to, Dictionary<int, int> map, List<string> skipped)
        {
            int s = source.Find(from), t = Bone(target, to);
            if (s < 0) { skipped.Add("The file has no '" + from + "', so " + to + " isn't driven."); return; }
            if (t < 0) return;
            map[t] = s;
        }

        private static int Bone(Skeleton rig, string name)
        {
            return rig.Bones.FindIndex(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
                                         || x.Name.EndsWith(":" + name, StringComparison.OrdinalIgnoreCase));
        }
        #endregion

        #region THE CHANGE OF BASIS
        /// <summary>
        /// Where the rig rests as the engine composes it. The root bone is the engine's placement
        /// rather than the rig's, so it rests at identity however the rig file writes it - see
        /// <see cref="AnimationImport.RootHandling"/>.
        /// </summary>
        private static Matrix4x4[] RestModel(Skeleton rig)
        {
            Matrix4x4[] model = new Matrix4x4[rig.Bones.Count];
            for (int i = 0; i < rig.Bones.Count; i++)
            {
                Matrix4x4 local = i == 0 ? Matrix4x4.Identity : rig.Bones[i].LocalTransform;
                int parent = rig.Bones[i].ParentIndex;
                model[i] = parent >= 0 && parent < i ? local * model[parent] : local;
            }
            return model;
        }

        /* Fit the change of basis between the two rigs, allowing it to be a reflection.
         *
         * CATHODE's skeleton space is left-handed and a glTF's is not, so for these rigs the answer
         * IS a reflection - X to -X. That is not a difficulty to route around: conjugating a rotation
         * by a reflection gives a rotation, so the motion crosses intact. It only has to be allowed
         * for, which means searching all 48 signed axis permutations rather than the 24 rotations.
         * A quaternion cannot hold a reflection, and asked to stand in for one it becomes a 180
         * degree yaw instead - the same flip about the up axis, but front-to-back as well, which
         * plays the clip with the arms reaching backwards.
         *
         * Scored on three directions MEASURED from each rig's own anatomy. Never derive the third
         * with a cross product: that assumes the handedness this exists to discover. */
        private static Matrix4x4 FitFrame(Rig source, Skeleton target, Matrix4x4[] rest, Dictionary<int, int> map)
        {
            Vector3[] wants =
            {
                Vector3.Normalize(At(rest, target, "NECK") - At(rest, target, "HIPS")),                    //up
                Vector3.Normalize(At(rest, target, "LEFTSHOULDER") - At(rest, target, "RIGHTSHOULDER")),   //left
                Forward(At(rest, target, "LEFTTOEBASE") - At(rest, target, "LEFTFOOT"),
                        At(rest, target, "RIGHTTOEBASE") - At(rest, target, "RIGHTFOOT")),
            };
            Vector3[] haves =
            {
                Direction(source, map, target, "NECK", "HIPS"),
                Direction(source, map, target, "LEFTSHOULDER", "RIGHTSHOULDER"),
                Forward(Joint(source, "ball_l") - Joint(source, "foot_l"), Joint(source, "ball_r") - Joint(source, "foot_r")),
            };

            Matrix4x4 best = Matrix4x4.Identity;
            double bestError = double.MaxValue;
            foreach (Matrix4x4 candidate in SignedPermutations())
            {
                double sum = 0;
                for (int i = 0; i < wants.Length; i++) sum += (Vector3.Transform(haves[i], candidate) - wants[i]).LengthSquared();
                if (sum < bestError) { bestError = sum; best = candidate; }
            }
            return best;
        }

        /* Which way a body faces, from its toes: ankle-to-toe, averaged across both feet so the
         * outward splay cancels, and levelled so a digitigrade leg reads the same as a human one.
         * Every rig this handles has feet. Checked against the alien's tail, which is the one rig
         * where a second opinion exists: the two agree to 0 degrees. */
        private static Vector3 Forward(Vector3 left, Vector3 right)
        {
            return Flat(left + right);
        }

        private static Vector3 Flat(Vector3 v)
        {
            Vector3 flat = new Vector3(v.X, 0, v.Z);
            return flat.LengthSquared() < 1e-9f ? Vector3.UnitZ : Vector3.Normalize(flat);
        }

        /* The 48 ways to send each axis to another axis with either sign - 24 rotations and the 24
         * reflections. Written for row vectors, so Vector3.Transform(v, m) applies it. */
        private static IEnumerable<Matrix4x4> SignedPermutations()
        {
            int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
                               new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
            foreach (int[] order in orders)
                for (int signs = 0; signs < 8; signs++)
                {
                    float[,] cells = new float[3, 3];
                    for (int column = 0; column < 3; column++)
                        cells[order[column], column] = (signs & (1 << column)) != 0 ? -1 : 1;

                    yield return new Matrix4x4(
                        cells[0, 0], cells[0, 1], cells[0, 2], 0,
                        cells[1, 0], cells[1, 1], cells[1, 2], 0,
                        cells[2, 0], cells[2, 1], cells[2, 2], 0,
                        0, 0, 0, 1);
                }
        }

        private static double Determinant(Matrix4x4 m)
        {
            return m.M11 * (m.M22 * m.M33 - m.M23 * m.M32)
                 - m.M12 * (m.M21 * m.M33 - m.M23 * m.M31)
                 + m.M13 * (m.M21 * m.M32 - m.M22 * m.M31);
        }

        /* How far the two rigs' bones sit apart once carried across. This is a measure of how
         * differently they are BUILT, not of anything being wrong - the target keeps its own bone
         * offsets throughout, so nothing can stretch however far apart this comes out. */
        private static double Residual(Rig source, Skeleton target, Matrix4x4[] rest, Dictionary<int, int> map,
                                       double scale, Matrix4x4 frame)
        {
            Vector3 rigHips = At(rest, target, "HIPS");
            Vector3 sourceHips = source.RestWorld[map[Bone(target, "HIPS")]].Translation;

            double sum = 0;
            foreach (KeyValuePair<int, int> pair in map)
                sum += (Vector3.Transform((source.RestWorld[pair.Value].Translation - sourceHips) * (float)scale, frame)
                        - (rest[pair.Key].Translation - rigHips)).LengthSquared();
            return Math.Sqrt(sum / Math.Max(1, map.Count));
        }

        private static Vector3 At(Matrix4x4[] model, Skeleton rig, string bone)
        {
            int i = Bone(rig, bone);
            return i < 0 ? Vector3.Zero : model[i].Translation;
        }

        private static Vector3 Joint(Rig source, string name)
        {
            int i = source.Find(name);
            return i < 0 ? Vector3.Zero : source.RestWorld[i].Translation;
        }

        private static Vector3 Direction(Rig source, Dictionary<int, int> map, Skeleton target, string to, string from)
        {
            return Vector3.Normalize(source.RestWorld[map[Bone(target, to)]].Translation
                                   - source.RestWorld[map[Bone(target, from)]].Translation);
        }
        #endregion

        #region THE RETARGET
        /* Take each source bone's rotation away from its own rest, say it in the target's world, and
         * apply it to the target bone's rest. Translations stay the target's own, exactly as the
         * engine's own retargeter does for the rigs it ships mappings for, so no bone can change
         * length. The conjugation is done as matrices because the change of basis may be a mirror. */
        private static List<List<HavokPackfile.SampledTransform>> Retarget(Rig source, Skeleton target,
            Dictionary<int, int> map, Matrix4x4[] rest, Matrix4x4 frame, double scale)
        {
            Matrix4x4 inverse = Matrix4x4.Transpose(frame);            //orthogonal, so this is the inverse

            Quaternion[] restRotation = new Quaternion[target.Bones.Count];
            Matrix4x4[] restMatrix = new Matrix4x4[target.Bones.Count];
            for (int i = 0; i < target.Bones.Count; i++)
            {
                restRotation[i] = RotationOf(rest[i]);
                restMatrix[i] = Matrix4x4.CreateFromQuaternion(restRotation[i]);
            }

            int hips = Bone(target, "HIPS");
            Vector3 sourceHipsRest = source.RestWorld[map[hips]].Translation;

            bool[] isTail = target.Bones.Select(x => x.Name.ToUpperInvariant().Contains(":TAIL")).ToArray();
            int[] tail = Enumerable.Range(0, target.Bones.Count).Where(x => isTail[x]).ToArray();
            float restTail = tail.Length == 0 ? 0 : tail.Min(x => rest[x].Translation.Y);

            int[] feet = Feet(target);
            int[] sourceFeet = new[] { "ball_l", "ball_r", "foot_l", "foot_r" }
                .Select(x => source.Find(x)).Where(x => x >= 0).ToArray();
            float restFoot = feet.Length == 0 ? 0 : feet.Min(x => rest[x].Translation.Y);
            float sourceRestFoot = sourceFeet.Length == 0 ? 0
                : sourceFeet.Min(x => Stand(source.RestWorld[x].Translation, sourceHipsRest, frame, scale, rest[hips].Translation).Y);

            List<List<HavokPackfile.SampledTransform>> poses = new List<List<HavokPackfile.SampledTransform>>();
            for (int frameAt = 0; frameAt < source.World.Count; frameAt++)
            {
                Quaternion[] model = new Quaternion[target.Bones.Count];
                List<HavokPackfile.SampledTransform> pose = new List<HavokPackfile.SampledTransform>(target.Bones.Count);

                for (int bone = 0; bone < target.Bones.Count; bone++)
                {
                    int parent = target.Bones[bone].ParentIndex;
                    Quaternion parentModel = parent >= 0 && parent < bone ? model[parent] : Quaternion.Identity;

                    Quaternion local;
                    if (map.TryGetValue(bone, out int from))
                    {
                        Matrix4x4 was = Matrix4x4.CreateFromQuaternion(source.RestSpin[from]);
                        Matrix4x4 now = Matrix4x4.CreateFromQuaternion(source.Spin[frameAt][from]);
                        Matrix4x4 delta = inverse * (Matrix4x4.Transpose(was) * now) * frame;

                        model[bone] = Quaternion.Normalize(RotationOf(restMatrix[bone] * delta));
                        local = Quaternion.Normalize(Quaternion.Conjugate(parentModel) * model[bone]);
                    }
                    else if (isTail[bone])
                    {
                        /* A human rig has nothing to say about a tail, so any motion for one would be
                         * invention - but letting it ride the hips rigidly is not neutral either. The
                         * alien's is nearly four metres long and rests almost on the floor, so a few
                         * degrees of hip pitch sweeps it through the ground. Held where it rests, it
                         * lies where it would have been lying anyway. */
                        model[bone] = restRotation[bone];
                        local = Quaternion.Normalize(Quaternion.Conjugate(parentModel) * model[bone]);
                    }
                    else
                    {
                        //nothing drives it, so it holds its shape and rides whatever does
                        local = bone == 0 ? Quaternion.Identity : Quaternion.Normalize(target.Bones[bone].Rotation);
                        model[bone] = Quaternion.Normalize(parentModel * local);
                    }

                    Vector3 translation = bone == 0 ? Vector3.Zero : target.Bones[bone].Position;
                    if (bone == hips)
                    {
                        //sideways and forwards the hips follow the source's, scaled; height comes from the feet
                        Vector3 travel = source.World[frameAt][map[hips]].Translation - sourceHipsRest;
                        travel = Vector3.Transform(travel, frame) * (float)scale;
                        translation += new Vector3(travel.X, 0, travel.Z);
                    }

                    pose.Add(new HavokPackfile.SampledTransform
                    {
                        Translation = translation,
                        Rotation = local,
                        Scale = bone == 0 ? Vector3.One : target.Bones[bone].ScaleXYZ,
                    });
                }

                /* Copying the source's hip height does not keep the character standing on the floor:
                 * two legs built differently fold by different amounts for the same joint angles, and
                 * on the alien that drove it a third of a metre into the ground. Set the height from
                 * the feet instead - lift the hips until the lowest foot has risen and fallen by
                 * exactly as much as the source's did, each measured against its own rest. Everything
                 * below the hips moves with them, so one measurement per frame settles it. */
                if (feet.Length != 0 && sourceFeet.Length != 0)
                {
                    Matrix4x4[] posed = Model(target, pose);
                    float mine = feet.Min(x => posed[x].Translation.Y) - restFoot;
                    float theirs = sourceFeet.Min(x => Stand(source.World[frameAt][x].Translation,
                        sourceHipsRest, frame, scale, rest[hips].Translation).Y) - sourceRestFoot;

                    pose[hips].Translation = new Vector3(pose[hips].Translation.X,
                        pose[hips].Translation.Y + (theirs - mine), pose[hips].Translation.Z);
                }

                if (tail.Length != 0) LiftTail(target, pose, tail, restTail);
                poses.Add(pose);
            }
            return poses;
        }

        /* Even held still the tail is dragged down whenever the hips drop, because it rests almost on
         * the floor to begin with. Pitch it up at its base by however much it needs and no more, so
         * it arcs clear the way a real animal's would when it crouches. Rotating every one of its
         * bones by the same amount leaves each one's transform relative to the one before it exactly
         * as it was, so the tail keeps its own shape and only the base bends. */
        private static void LiftTail(Skeleton rig, List<HavokPackfile.SampledTransform> pose, int[] tail, float restLow)
        {
            const int Passes = 3;                                //the lowest bone moves as it lifts
            for (int pass = 0; pass < Passes; pass++)
            {
                Matrix4x4[] model = Model(rig, pose);
                int low = tail.OrderBy(x => model[x].Translation.Y).First();
                float under = restLow - model[low].Translation.Y;
                if (under <= 0.005f) return;

                Vector3 arm = model[low].Translation - model[tail[0]].Translation;
                Vector3 flat = new Vector3(arm.X, 0, arm.Z);
                if (arm.Length() < 0.01f || flat.Length() < 0.01f) return;

                Vector3 axis = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, flat));
                double angle = Math.Asin(Math.Min(0.9, under / arm.Length()));

                /* Take whichever way round actually raises it rather than reasoning about the sign.
                 * SampledTransform is a class, so the rotation has to be put back between the two
                 * attempts - copying the transform would only copy the reference. */
                Quaternion parentModel = RotationOf(model[rig.Bones[tail[0]].ParentIndex]);
                Quaternion was = pose[tail[0]].Rotation, bestRotation = was;
                float best = float.MinValue;
                foreach (double sign in new[] { 1.0, -1.0 })
                {
                    Quaternion pitch = Quaternion.CreateFromAxisAngle(axis, (float)(angle * sign));
                    pose[tail[0]].Rotation = Quaternion.Normalize(Quaternion.Conjugate(parentModel) * pitch * parentModel * was);

                    Matrix4x4[] tried = Model(rig, pose);
                    float lowest = tail.Min(x => tried[x].Translation.Y);
                    if (lowest > best) { best = lowest; bestRotation = pose[tail[0]].Rotation; }
                }
                pose[tail[0]].Rotation = bestRotation;
            }
        }

        /* The bones that reach the ground. The alien's foot is a digitigrade four-segment chain and a
         * human's is an ankle and a ball, so these are not the same shape - only both "the part that
         * stands on the floor". */
        private static int[] Feet(Skeleton rig)
        {
            return Enumerable.Range(0, rig.Bones.Count)
                .Where(x => rig.Bones[x].Name.ToUpperInvariant().Contains("TOE")
                         || rig.Bones[x].Name.ToUpperInvariant().Contains("PASTERN")).ToArray();
        }

        /* Put a point of the source rig where the retarget would put it: turned into the target's
         * world, scaled to its size, and hung off its hips. */
        private static Vector3 Stand(Vector3 point, Vector3 sourceHips, Matrix4x4 frame, double scale, Vector3 rigHips)
        {
            return Vector3.Transform(point - sourceHips, frame) * (float)scale + rigHips;
        }

        /// <summary>Compose a pose the way the runtime will: root at identity, everything else as stored.</summary>
        private static Matrix4x4[] Model(Skeleton rig, List<HavokPackfile.SampledTransform> pose)
        {
            Matrix4x4[] model = new Matrix4x4[rig.Bones.Count];
            for (int bone = 0; bone < rig.Bones.Count; bone++)
            {
                Matrix4x4 local = Matrix4x4.CreateScale(pose[bone].Scale)
                    * Matrix4x4.CreateFromQuaternion(pose[bone].Rotation)
                    * Matrix4x4.CreateTranslation(pose[bone].Translation);
                int parent = rig.Bones[bone].ParentIndex;
                model[bone] = parent >= 0 && parent < bone ? local * model[parent] : local;
            }
            return model;
        }
        #endregion

        #region HELPERS
        private static Quaternion RotationOf(Matrix4x4 m)
        {
            return Matrix4x4.Decompose(m, out Vector3 _, out Quaternion rotation, out Vector3 _)
                ? Quaternion.Normalize(rotation) : Quaternion.Identity;
        }

        //Assimp matrices are row-vector-on-the-right, System.Numerics are the transpose of that
        private static Matrix4x4 ToNumerics(Assimp.Matrix4x4 m)
        {
            return new Matrix4x4(m.A1, m.B1, m.C1, m.D1, m.A2, m.B2, m.C2, m.D2,
                                 m.A3, m.B3, m.C3, m.D3, m.A4, m.B4, m.C4, m.D4);
        }

        private static Vector3 ToNumerics(Assimp.Vector3D v) { return new Vector3(v.X, v.Y, v.Z); }
        private static Quaternion ToNumerics(Assimp.Quaternion q) { return new Quaternion(q.X, q.Y, q.Z, q.W); }
        #endregion
    }
}
