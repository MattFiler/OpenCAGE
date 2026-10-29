using AlienPAK;
using Assimp;
using CATHODE;
using CATHODE.Animations;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Animation = CathodeLib.Animation;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE
{
    /// <summary>
    /// The skeleton and animation half of a model import: what rig the file is built on, whether the
    /// game already has it, and what can be done about it.
    ///
    /// A model file that carries a rig usually carries the animation authored on it too, and the two
    /// decisions are the same decision - a clip can only be imported once there is a skeleton for it
    /// to play on. So both are worked out here, together, and the dialog just presents them.
    /// </summary>
    public static class ModelImportRig
    {
        /// <summary>What the file has, and what the game can do with it.</summary>
        public class Situation
        {
            public bool Skinned;
            public int BoneCount;
            public string RootBoneName = "";

            /// <summary>The rig came out of OpenCAGE, so its bones already carry game indices.</summary>
            public bool OurOwnExport;

            /// <summary>The game rig that best fits the mesh, and how far off it is in metres.</summary>
            public Animation.SkeletonAsset BestFit;
            public float BestFitScore = -1;

            /// <summary>Animations in the file, by name.</summary>
            public List<string> Animations = new List<string>();

            /// <summary>The file is built on something shaped like a person, whatever it calls its bones.</summary>
            public bool LooksHumanoid;

            public bool HasAnimations { get { return Animations.Count != 0; } }


            /// <summary>A game rig this close to the mesh is the rig the mesh was made for.</summary>
            public bool FitsAGameRig { get { return BestFitScore >= 0 && BestFitScore <= FitThreshold; } }
        }

        /// <summary>
        /// How near a rig has to sit to a mesh's weighted vertices to be the rig it was built on.
        /// Measured across the shipped characters: a model's own rig scores 2.5-7.8 cm and the next
        /// best any other rig manages is 22.9 cm, so the gap is wide and anywhere inside it will do.
        /// </summary>
        public const float FitThreshold = 0.15f;

        /// <summary>What the import was told to do about the animation in the file.</summary>
        public class Outcome
        {
            public bool ImportAnimations;

            /// <summary>Set the clips go into.</summary>
            public string AnimationSetName = "";

            /// <summary>
            /// The rig to start the import window on, when the mesh is already bound to one the game
            /// has. Empty otherwise, and the window asks.
            /// </summary>
            public string PreferredRig = "";

            /// <summary>What actually happened, for the message at the end.</summary>
            public List<string> Report = new List<string>();
        }
        /// <summary>
        /// Work out what the file is offering. Cheap enough to call while the dialog is opening -
        /// the only real work is scoring the mesh against the game's rigs, and that only happens for
        /// a file that is skinned.
        /// </summary>
        public static Situation Examine(Scene scene, ModelIO.ImportPlan plan, Animation animations)
        {
            Situation situation = new Situation();
            if (scene == null) return situation;

            foreach (Assimp.Animation animation in scene.Animations)
                situation.Animations.Add(string.IsNullOrEmpty(animation.Name) ? "animation " + (situation.Animations.Count + 1) : animation.Name);

            HashSet<string> boneNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (Mesh mesh in scene.Meshes)
                foreach (Bone bone in mesh.Bones)
                    boneNames.Add(bone.Name ?? "");

            situation.Skinned = boneNames.Count != 0;
            situation.BoneCount = boneNames.Count;
            if (!situation.Skinned) return situation;

            situation.OurOwnExport = boneNames.All(x => ModelIO.TryParseBoneName(x, out int _));
            situation.RootBoneName = boneNames.OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault() ?? "";

            float scale = plan?.Scale > 0 ? plan.Scale : 1.0f;
            float unitScale = plan?.UnitScale > 0 ? plan.UnitScale : ModelIO.UnitScale;

            situation.LooksHumanoid = AnimationRetarget.Looks(scene, out string _);

            /* Which game rig, if any, this mesh is actually built on. Only worth asking of a file
             * whose bones carry game indices - a foreign rig's names match nothing, and scoring all
             * 377 of them to be told so is a second or two of nothing. */
            if (situation.OurOwnExport && animations != null)
                foreach (Animation.SkeletonAsset asset in animations.Skeletons)
                {
                    if (asset.Skeleton == null) continue;
                    float score = ScoreFit(scene, asset.Skeleton, scale, unitScale);
                    if (score < 0 || (situation.BestFitScore >= 0 && score >= situation.BestFitScore)) continue;
                    situation.BestFitScore = score;
                    situation.BestFit = asset;
                }
            return situation;
        }

        /// <summary>
        /// One line saying what the file is, for the top of the dialog's rig panel.
        /// </summary>
        public static string Describe(Situation situation)
        {
            if (situation == null || !situation.Skinned)
                return situation != null && situation.HasAnimations
                    ? "This file has " + Count(situation.Animations.Count, "animation") + " but no rig to play " + (situation.Animations.Count == 1 ? "it" : "them") + " on."
                    : "This model isn't skinned to a skeleton.";

            string rig = situation.FitsAGameRig
                ? "Skinned to " + situation.BestFit.Skeleton.Name + " (" + situation.BoneCount + " bones), a rig the game already has."
                : "Skinned to a rig the game doesn't have (" + situation.BoneCount + " bones, root '" + situation.RootBoneName + "').";

            if (!situation.HasAnimations) return rig;
            return rig + " It also carries " + Count(situation.Animations.Count, "animation") + ".";
        }


        /// <summary>
        /// How far a rig sits from the mesh it is being tested against: the mean distance between
        /// each bone and the weighted centre of the vertices bound to it, in metres. -1 when the two
        /// have too little in common to be answering about the same character at all.
        /// </summary>
        public static float ScoreFit(Scene scene, Skeleton rig, float scale = 1.0f, float unitScale = ModelIO.UnitScale)
        {
            if (scene == null || rig == null || rig.Bones.Count == 0) return -1;
            if (scale <= 0) scale = 1.0f;
            if (unitScale <= 0) unitScale = ModelIO.UnitScale;

            Dictionary<string, Vector3> weighted = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            Dictionary<string, float> totals = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (Mesh mesh in scene.Meshes)
                foreach (Bone bone in mesh.Bones)
                    foreach (VertexWeight weight in bone.VertexWeights)
                    {
                        if (weight.Weight <= 0.001f || weight.VertexID < 0 || weight.VertexID >= mesh.VertexCount) continue;

                        Assimp.Vector3D vertex = mesh.Vertices[weight.VertexID];
                        Vector3 position = new Vector3(vertex.X, vertex.Y, vertex.Z) * scale / unitScale;
                        weighted[bone.Name] = (weighted.TryGetValue(bone.Name, out Vector3 sum) ? sum : Vector3.Zero) + position * weight.Weight;
                        totals[bone.Name] = (totals.TryGetValue(bone.Name, out float total) ? total : 0) + weight.Weight;
                    }
            if (weighted.Count == 0) return -1;

            /* The scene's bones are matched to the rig's by name. Our own exports carry the game's
             * bone index in the name, which is what makes this answerable for a re-import; a foreign
             * file's names will not match a game rig, and it scoring nothing is the right answer. */
            List<Matrix4x4> pose = rig.GetBindPose();
            double total2 = 0;
            int matched = 0;
            foreach (KeyValuePair<string, Vector3> entry in weighted)
            {
                int bone = ModelIO.TryParseBoneName(entry.Key, out int index) ? index : rig.IndexOf(entry.Key);
                if (bone < 0 || bone >= pose.Count) continue;

                total2 += (entry.Value / totals[entry.Key] - pose[bone].Translation).Length();
                matched++;
            }

            //a rig that only a handful of bones line up with has not been identified, it has been guessed at
            return matched < Math.Min(4, weighted.Count) ? -1 : (float)(total2 / matched);
        }
        private static string Count(int number, string noun)
        {
            return number + " " + noun + (number == 1 ? "" : "s");
        }


    }
}
