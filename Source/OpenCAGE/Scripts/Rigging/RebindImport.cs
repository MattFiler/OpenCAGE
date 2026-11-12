using AlienPAK;
using Assimp;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Animation = CathodeLib.Animation;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.Rigging
{
    /// <summary>
    /// The editor's side of rebinding a model onto a game rig (<see cref="SkinRebind"/>): rig choice, planning and writing it into
    /// the imported scene, a preview picture, mapping files, and the hit collision a character needs to be shot.
    /// </summary>
    public static class RebindImport
    {
        /// <summary>The game's character rigs a model can be rebound onto, the shared reference rigs first.</summary>
        public static List<Skeleton> CharacterRigs(Animation animations)
        {
            if (animations == null) return new List<Skeleton>();
            string[] first = { "MALE", "FEMALE", "FEMALENPC", "ALIEN", "FEMALEFP", "FACEHUGGER", "ANDROID" };
            return animations.Skeletons.Select(o => o.Skeleton ?? o.Skeleton64).Where(RigRoles.CanTarget)
                .GroupBy(o => o.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .OrderBy(o => { int i = Array.IndexOf(first, o.Name.ToUpperInvariant()); return i < 0 ? 100 : i; })
                .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Read the scene's skeleton and work the rebind out, changing nothing.</summary>
        public static SkinRebind.Result Plan(Scene scene, float unitScale, Skeleton rig, SkinRebind.Options options)
        {
            SourceRig source = SourceRig.Read(scene, unitScale);
            if (source == null) return new SkinRebind.Result() { Problem = "Nothing in this file is skinned to a skeleton, so there is nothing to rebind.", Target = rig };
            return SkinRebind.Build(source, rig, options);
        }

        /// <summary>
        /// Writes a rebind into its scene, which then reads as a mesh OpenCAGE exported on the rig (bones named by
        /// ModelIO.BoneName, vertices in its bind pose).
        /// </summary>
        public static void Apply(SkinRebind.Result result, Scene scene)
        {
            if (result == null || !result.Ok) throw new InvalidOperationException(result?.Problem ?? "Nothing to apply.");
            SkinRebind.WriteBack(result, scene, b => ModelIO.BoneName(b, RigRoles.Short(result.Target.Bones[b].Name)));
        }

        /// <summary>A short clip of the rig's own to show the result moving: a walk if it has one.</summary>
        public static Animation.ClipReference ShowcaseClip(Animation animations, Skeleton rig)
        {
            if (animations == null || rig == null) return null;
            try
            {
                List<Animation.ClipReference> clips = animations.GetClips(rig.Name);
                foreach (string word in new[] { "walk_forward", "walk", "run", "idle" })
                {
                    Animation.ClipReference clip = clips.Where(c => (c.Name ?? "").IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 && c.Duration > 0.3f).OrderBy(c => c.Name.Length).FirstOrDefault();
                    if (clip != null) return clip;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// The result drawn front and side at the rig's bind pose, and again posed by <paramref name="clip"/> (when
        /// given), with the rig's bones in white over it.
        /// </summary>
        public static Bitmap Picture(SkinRebind.Result result, Scene scene, Animation.ClipReference clip, int width = 960, int height = 480)
        {
            if (result == null || !result.Ok) return null;
            Skeleton rig = result.Target;
            List<Matrix4x4> bind = result.TargetBind;
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (SkinRebind.MeshOut m in result.Meshes) foreach (Vector3 p in m.Positions) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            foreach (Matrix4x4 b in bind) { min = Vector3.Min(min, b.Translation); max = Vector3.Max(max, b.Translation); }
            Vector3 centre = (min + max) * 0.5f;
            int panels = clip != null ? 4 : 2;
            int w = width / panels;
            float extent = Math.Max(max.Y - min.Y, Math.Max(max.X - min.X, max.Z - min.Z));
            float ppm = Math.Min(height * 0.92f / Math.Max(0.1f, max.Y - min.Y), w * 0.92f / Math.Max(0.1f, Math.Max(max.X - min.X, max.Z - min.Z)));

            List<Matrix4x4> skin = null, posed = null;
            if (clip != null)
            {
                try
                {
                    int frame = Math.Max(0, (int)Math.Round(clip.Duration * 30) / 3);
                    skin = Animation.SampleSkinningPose(clip, rig, frame, Animation.RootMotion.Ignore);
                    posed = Animation.SampleModelPose(clip, rig, frame, Animation.RootMotion.Ignore);
                }
                catch { skin = null; }
                if (skin == null) panels = 2;
            }

            RigPicture picture = new RigPicture(width, height);
            List<RigPicture.View> views = new List<RigPicture.View>()
            {
                RigPicture.View.Character(true, centre, ppm, 0, 0, w, height, "at " + rig.Name + "'s bind pose"),
                RigPicture.View.Character(false, centre, ppm, w, 0, w, height, ""),
            };
            if (skin != null)
            {
                views.Add(RigPicture.View.Character(true, centre, ppm, 2 * w, 0, w, height, "posed by " + clip.Name));
                views.Add(RigPicture.View.Character(false, centre, ppm, 3 * w, 0, w, height, ""));
                /* A clip sampled on the spot can face the other way to the bind pose (its root's turn is the engine's): look at the
                 * posed figure from its own front, judged by where its toes point against the bind pose's */
                int lf = RigRoles.Bone(rig, "LEFTFOOT"), lt = RigRoles.Bone(rig, "LEFTTOEBASE");
                if (lf >= 0 && lt >= 0 && posed != null)
                {
                    Vector3 restFwd = bind[lt].Translation - bind[lf].Translation, posedFwd = posed[lt].Translation - posed[lf].Translation;
                    if (new Vector3(restFwd.X, 0, restFwd.Z).LengthSquared() > 1e-6f && Vector3.Dot(new Vector3(restFwd.X, 0, restFwd.Z), new Vector3(posedFwd.X, 0, posedFwd.Z)) < 0)
                        for (int v = 2; v < 4; v++) { views[v].Right = -views[v].Right; views[v].Toward = -views[v].Toward; }
                }
            }
            for (int v = 0; v < views.Count; v++)
            {
                bool moving = v >= 2;
                foreach (SkinRebind.MeshOut m in result.Meshes)
                {
                    Mesh mesh = scene.Meshes[m.MeshIndex];
                    Vector3[] pos = new Vector3[m.Positions.Length];
                    for (int i = 0; i < pos.Length; i++)
                    {
                        if (!moving) { pos[i] = m.Positions[i]; continue; }
                        Vector3 p = Vector3.Zero;
                        for (int k = 0; k < m.Bones[i].Length; k++) p += Vector3.Transform(m.Positions[i], skin[m.Bones[i][k]]) * m.Weights[i][k];
                        pos[i] = p;
                    }
                    foreach (Face f in mesh.Faces)
                        if (f.IndexCount == 3 && f.Indices[0] < pos.Length && f.Indices[1] < pos.Length && f.Indices[2] < pos.Length)
                            picture.Triangle(views[v], pos[f.Indices[0]], pos[f.Indices[1]], pos[f.Indices[2]], RigPicture.BoneColour(m.Bones[f.Indices[0]][0]));
                }
                List<Matrix4x4> pose = moving ? posed : bind;
                for (int b = 0; b < rig.Bones.Count; b++)
                {
                    int parent = rig.Bones[b].ParentIndex;
                    if (parent > 0) picture.Line(views[v], pose[parent].Translation, pose[b].Translation, Color.White, 1);
                }
            }
            return picture.ToBitmap(views);
        }

        #region MAPPING FILES
        /// <summary>A mapping as saved: role -> the file's joint name ("" leaves the role out).</summary>
        public static string ToJson(IDictionary<string, string> map)
        {
            return JsonConvert.SerializeObject(new SortedDictionary<string, string>(map.ToDictionary(o => o.Key, o => o.Value), StringComparer.OrdinalIgnoreCase), Formatting.Indented);
        }

        public static Dictionary<string, string> FromJson(string json)
        {
            Dictionary<string, string> map = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            return new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        }
        #endregion

        #region HIT COLLISION
        /* Retail display models have a COL_ part: a skinned mesh carrying weighted collision (hit zones with per-vertex damage and
         * armour), listed among the animated model's meshes. Without one bullets pass through the character, so a rebound mesh
         * borrows a part of this level that carries weighted collision on the same rig. */
        public class CollisionSource
        {
            public Composite Composite;
            public FunctionEntity Part;
            public string Description;
            public string Material;
            public float Fit;
        }

        /// <summary>The hit-collision parts of this level that are skinned to <paramref name="rig"/> (or to a rig with its bones), best first.</summary>
        public static List<CollisionSource> HitCollisionParts(Level level, Skeleton rig)
        {
            List<CollisionSource> found = new List<CollisionSource>();
            if (level?.Commands == null || rig == null) return found;
            foreach (Composite composite in level.Commands.Entries)
            {
                if (composite == null) continue;
                foreach (FunctionEntity part in composite.functions)
                {
                    if (!part.function.IsFunctionType || part.function.AsFunctionType != FunctionType.ModelReference) continue;
                    List<ResourceReference> refs = (part.GetParameter(ShortGuids.resource)?.content as cResource)?.value;
                    if (refs == null) continue;
                    foreach (ResourceReference r in refs.Where(o => o.resource_type == ResourceType.RENDERABLE_INSTANCE))
                    {
                        RenderableElements.Element element = r.RenderableInstance?.FirstOrDefault(e => e?.Model?.WeightedCollision != null);
                        if (element == null) continue;
                        Models.CS2 model = level.Models.FindModel(element.Model);
                        if (model == null) continue;
                        float fit = rig.ScoreFit(model);
                        if (fit < 0 || fit > Skeleton.FitLimit) continue;
                        found.Add(new CollisionSource() { Composite = composite, Part = part, Material = element.Material?.Name ?? "", Fit = fit, Description = composite.name + " / " + (model.Name ?? "?") + " (" + (element.Material?.Name ?? "") + ", " + (fit * 100).ToString("0.0") + " cm off " + rig.Name + ")" });
                    }
                }
            }
            //Collision composites and display models first, then a material of the rig's kind (an android's COL_ part sprays
            //android fluid), then the closest fit
            return found.OrderBy(o => o.Composite.name.IndexOf("COLLISION", StringComparison.OrdinalIgnoreCase) >= 0 || o.Composite.name.StartsWith(ModelCompositeBuilder.DisplayModelPrefix) ? 0 : 1)
                        .ThenBy(o => SameKind(o.Material, rig.Name) ? 0 : 1)
                        .ThenBy(o => o.Fit)
                        .ThenBy(o => o.Description, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Whether a collision material suits a rig: ANDROID and ALIEN ones for those rigs, the rest for everyone else.</summary>
        private static bool SameKind(string material, string rig)
        {
            foreach (string kind in new[] { "ANDROID", "ALIEN", "FACEHUGGER" })
            {
                bool rigIs = (rig ?? "").IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0;
                bool materialIs = (material ?? "").IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0;
                if (rigIs || materialIs) return rigIs == materialIs;
            }
            return true;
        }

        /// <summary>
        /// Lists every part of a display model (COL_ included) in its animated model's meshes, as retail does: the game skins and
        /// hit-tests only the meshes listed there. Returns how many were added.
        /// </summary>
        public static int ListPartsInAnimatedModel(Composite displayModel)
        {
            FunctionEntity env = displayModel?.functions.FirstOrDefault(f => f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.EnvironmentModelReference);
            ResourceReference animated = (env?.GetParameter(ShortGuids.resource)?.content as cResource)?.value.FirstOrDefault(r => r.resource_type == ResourceType.ANIMATED_MODEL);
            EnvironmentAnimations.EnvironmentAnimation entry = animated?.AnimatedModel;
            if (entry == null) return 0;
            int added = 0;
            foreach (FunctionEntity part in displayModel.functions.Where(f => f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.ModelReference))
                foreach (ResourceReference r in (part.GetParameter(ShortGuids.resource)?.content as cResource)?.value ?? new List<ResourceReference>())
                    if (r.resource_type == ResourceType.RENDERABLE_INSTANCE && !entry.MeshMappings.Contains(r.resource_id))
                    {
                        entry.MeshMappings.Add(r.resource_id);
                        added++;
                    }
            return added;
        }
        #endregion
    }
}
