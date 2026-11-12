using AlienPAK;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Rigging
{
    /// <summary>
    /// The level's half of a display rig (<see cref="DisplayRigBuilder"/>): a DisplayModel copied from one on the gameplay rig,
    /// its models and weighted hit collision resized (bullets test the mesh as the display model's skinning poses it) and its
    /// per-bone inverse binds moved onto the rig. The originals are left as they are.
    /// </summary>
    public static class DisplayRigFit
    {
        public class Result
        {
            public string Problem;
            public Composite Composite;
            public Models.CS2 Model;
            public float Factor;
            public List<string> Report = new List<string>();
            public bool Ok => Problem == null;
        }

        public static EnvironmentAnimations.EnvironmentAnimation AnimatedModelOf(Composite composite, out FunctionEntity reference)
        {
            reference = composite?.functions.FirstOrDefault(f => f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.EnvironmentModelReference);
            ResourceReference animated = (reference?.GetParameter(ShortGuids.resource)?.content as cResource)?.value.FirstOrDefault(r => r.resource_type == ResourceType.ANIMATED_MODEL);
            return animated?.AnimatedModel;
        }

        /// <summary>The models a composite's ModelReferences draw, in entity order, each once.</summary>
        public static List<Models.CS2> ModelsDrawn(Level level, Composite composite)
        {
            List<Models.CS2> models = new List<Models.CS2>();
            foreach (FunctionEntity part in composite.functions.Where(f => f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.ModelReference))
                foreach (ResourceReference r in (part.GetParameter(ShortGuids.resource)?.content as cResource)?.value ?? new List<ResourceReference>())
                    if (r.resource_type == ResourceType.RENDERABLE_INSTANCE)
                        foreach (RenderableElements.Element e in r.RenderableInstance ?? new List<RenderableElements.Element>())
                        {
                            Models.CS2 model = e?.Model == null ? null : level.Models.FindModel(e.Model);
                            if (model != null && !models.Contains(model)) models.Add(model);
                        }
            return models;
        }

        /// <summary>
        /// Makes <paramref name="newName"/> (a DisplayModel: composite) from <paramref name="source"/> on <paramref name="displayRig"/>,
        /// sized by <paramref name="factor"/> (the rig's size against the gameplay rig).
        /// </summary>
        public static Result Fit(Level level, Composite source, Skeleton gameplayRig, Skeleton displayRig, string newName, float factor)
        {
            Result r = new Result() { Factor = factor };
            EnvironmentAnimations.EnvironmentAnimation sourceEntry = AnimatedModelOf(source, out _);
            if (sourceEntry == null) { r.Problem = source?.name + " has no animated model (EnvironmentModelReference with an ANIMATED_MODEL), so it isn't a display model a rig can be fitted to."; return r; }
            if (sourceEntry.InverseBindPoses == null || sourceEntry.InverseBindPoses.Count != displayRig.Bones.Count)
            { r.Problem = source.name + " is on " + sourceEntry.SkeletonName + " (" + (sourceEntry.InverseBindPoses?.Count ?? 0) + " bones), not on " + gameplayRig.Name + " (" + displayRig.Bones.Count + " bones) the rig was made from."; return r; }
            string problem = ModelCompositeBuilder.Problem(newName, true, level.Commands);
            if (problem != null) { r.Problem = problem; return r; }

            //---- one model holding resized copies of every model the source draws (its hit-collision part included)
            List<Models.CS2> drawn = ModelsDrawn(level, source);
            if (drawn.Count == 0) { r.Problem = source.name + " draws no models."; return r; }
            string leaf = newName.Substring(ModelCompositeBuilder.DisplayModelPrefix.Length);
            string modelName = UniqueModelName(level, "CHARACTERS\\" + leaf + ".CS2");
            Models.CS2 model = new Models.CS2() { Name = modelName };
            Dictionary<Collisions.WeightedCollision, Collisions.WeightedCollision> collisions = new Dictionary<Collisions.WeightedCollision, Collisions.WeightedCollision>();
            int resizedCollisions = 0;
            foreach (Models.CS2 from in drawn)
                foreach (Models.CS2.Component component in from.Components)
                {
                    Models.CS2.Component made = new Models.CS2.Component();
                    foreach (Models.CS2.Component.LOD lod in component.LODs)
                    {
                        Models.CS2.Component.LOD copied = new Models.CS2.Component.LOD(lod.Name);
                        foreach (Models.CS2.Component.LOD.Submesh s in lod.Submeshes)
                        {
                            if (s == null) continue;
                            Models.CS2.Component.LOD.Submesh copy = new Models.CS2.Component.LOD.Submesh()
                            {
                                MinBounds = s.MinBounds, MaxBounds = s.MaxBounds, MinLODRange = s.MinLODRange, MaxLODRange = s.MaxLODRange,
                                RenderFlags = s.RenderFlags, Material = s.Material, CollisionProxyIndex = s.CollisionProxyIndex, MorphAnimSet = s.MorphAnimSet,
                                VertexFormatFull = s.VertexFormatFull, VertexFormatPartial = s.VertexFormatPartial, VertexScale = s.VertexScale,
                                VertexCount = s.VertexCount, IndexCount = s.IndexCount, Bones = new List<int>(s.Bones), Data = (byte[])s.Data.Clone(),
                            };
                            if (s.WeightedCollision != null)
                            {
                                if (!collisions.TryGetValue(s.WeightedCollision, out Collisions.WeightedCollision resized))
                                {
                                    collisions[s.WeightedCollision] = resized = ScaledCollision(s.WeightedCollision, factor);
                                    level.WeightedCollisions.Entries.Add(resized);
                                    resizedCollisions++;
                                }
                                copy.WeightedCollision = resized;
                            }
                            copied.Submeshes.Add(copy);
                        }
                        if (copied.Submeshes.Count != 0) made.LODs.Add(copied);
                    }
                    if (made.LODs.Count != 0) model.Components.Add(made);
                }
            //resized about the character's own origin (its feet), the culling reach of each skinned submesh with it (ModelIO.RescaleSubmeshes)
            List<Models.CS2.Component.LOD.Submesh> all = model.Components.SelectMany(c => c.LODs).SelectMany(l => l.Submeshes).ToList();
            List<float> reach = all.Select(s => s.Bones.Count != 0 && s.MinBounds.X >= 0 && s.MinBounds.X == s.MinBounds.Y && s.MinBounds.Y == s.MinBounds.Z ? s.MinBounds.X : -1).ToList();
            ModelIO.RescaleSubmeshes(all, factor);
            //The rig scales with the mesh, so each reach scales exactly
            for (int i = 0; i < all.Count; i++) if (reach[i] >= 0) all[i].MinBounds = new Vector3(reach[i] * factor);
            level.Models.Entries.Add(model);
            r.Model = model;
            r.Report.Add("Model " + modelName + ": " + drawn.Count + " model(s) of " + source.name + " copied at x" + factor.ToString("0.###") + " (" + all.Count + " submeshes" + (resizedCollisions != 0 ? ", " + resizedCollisions + " hit collision(s) resized with them" : ", no hit collision - bullets will pass through it") + ").");

            //---- the composite, its animated model copied from the source's and moved onto the rig
            ModelCompositeBuilder.Result made2 = ModelCompositeBuilder.Create(level, model, newName, gameplayRig, gameplayRig.Name);
            EnvironmentAnimations.EnvironmentAnimation entry = AnimatedModelOf(made2.Composite, out _);
            if (entry == null) { r.Problem = "The new composite has no animated model."; r.Composite = made2.Composite; return r; }
            entry.BoneMappings = new List<ShortGuid>(sourceEntry.BoneMappings);
            entry.InverseBindPoses = new List<Matrix4x4>(sourceEntry.InverseBindPoses);
            entry.HavokToCathodeMappings = new List<Matrix4x4>(sourceEntry.HavokToCathodeMappings);
            entry.HelperMatrices = sourceEntry.HelperMatrices?.Select(h => new EnvironmentAnimations.WeightedHelperData
            {
                HelperName = h.HelperName,
                BindPosition = h.BindPosition?.Select((v, k) => k < 3 ? v * factor : v).ToArray(),
                BindNormal = (float[])h.BindNormal?.Clone(),
                BindTangent = (float[])h.BindTangent?.Clone(),
                Indicies = (byte[])h.Indicies?.Clone(),
                Weights = (byte[])h.Weights?.Clone(),
            }).ToList() ?? new List<EnvironmentAnimations.WeightedHelperData>();
            int moved = BindToRig(entry, displayRig);
            entry.SkeletonName = displayRig.Name;
            r.Composite = made2.Composite;
            r.Report.Add(newName + ": animated model " + entry.ID + " on " + displayRig.Name + " (" + moved + " bone binds moved onto the rig, " + entry.HelperMatrices.Count + " helper point(s) x" + factor.ToString("0.###") + ").");
            return r;
        }

        /* Moves each bone's inverse bind (and Havok-to-mesh frame) so the bone sits as far from the root as the rig's does; bones the
         * rig keeps at gameplay size (the root, the IK markers) stay put. */
        private static int BindToRig(EnvironmentAnimations.EnvironmentAnimation entry, Skeleton rig)
        {
            List<Matrix4x4> bind = rig.GetBindPose();
            int moved = 0;
            for (int i = 0; i < entry.InverseBindPoses.Count && i < bind.Count; i++)
            {
                Matrix4x4.Invert(entry.InverseBindPoses[i], out Matrix4x4 bone);
                float now = bone.Translation.Length(), want = bind[i].Translation.Length();
                float f = now < 1e-5f ? 1f : want / now;
                if (Math.Abs(f - 1) > 1e-4f) moved++;
                Matrix4x4 m = entry.InverseBindPoses[i];
                m.M41 *= f; m.M42 *= f; m.M43 *= f;
                entry.InverseBindPoses[i] = m;
                if (i < entry.HavokToCathodeMappings.Count)
                {
                    Matrix4x4 h = entry.HavokToCathodeMappings[i];
                    h.M41 *= f; h.M42 *= f; h.M43 *= f;
                    entry.HavokToCathodeMappings[i] = h;
                }
            }
            return moved;
        }

        /* Hit collision at another size: positions are stored against a scale, so the scale resizes it exactly; each bone's inverse
         * bounds then takes a point of the resized body to where the original one was. */
        private static Collisions.WeightedCollision ScaledCollision(Collisions.WeightedCollision from, float factor)
        {
            Matrix4x4 grow = Matrix4x4.CreateScale(1f / factor);
            Collisions.WeightedCollision made = new Collisions.WeightedCollision { Scale = from.Scale * factor };
            foreach (Collisions.WeightedCollision.Bone b in from.Bones)
                made.Bones.Add(new Collisions.WeightedCollision.Bone
                {
                    BoneId = b.BoneId,
                    InverseModelBounds = grow * b.InverseModelBounds,
                    TriangleIndices = new List<ushort>(b.TriangleIndices),
                    VertexIndices = new List<ushort>(b.VertexIndices),
                });
            foreach (Collisions.WeightedCollision.Vertex v in from.Vertices)
                made.Vertices.Add(new Collisions.WeightedCollision.Vertex
                {
                    Position = v.Position,
                    BoneIndices = (byte[])v.BoneIndices.Clone(),
                    BoneWeights = (float[])v.BoneWeights.Clone(),
                    Armour = v.Armour,
                    Damage = v.Damage,
                    ImpactEffectMask = v.ImpactEffectMask,
                });
            return made;
        }

        private static string UniqueModelName(Level level, string wanted)
        {
            HashSet<string> taken = new HashSet<string>(level.Models.Entries.Where(o => o != null).Select(o => o.Name), StringComparer.OrdinalIgnoreCase);
            if (!taken.Contains(wanted)) return wanted;
            string stem = wanted.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase) ? wanted.Substring(0, wanted.Length - 4) : wanted;
            for (int i = 2; ; i++) if (!taken.Contains(stem + "_" + i + ".CS2")) return stem + "_" + i + ".CS2";
        }

        #region WHO WEARS IT
        /// <summary>
        /// Every placement of <paramref name="archetype"/> that passes its own value for a Character setting (reference_skeleton,
        /// display_model...); a placement's value wins over the composite's pin, so both need setting. Returns (composite, entity, value).
        /// </summary>
        public static List<(Composite composite, FunctionEntity entity, string was)> PlacementsPassing(Commands commands, Composite archetype, string parameter)
        {
            List<(Composite, FunctionEntity, string)> found = new List<(Composite, FunctionEntity, string)>();
            ShortGuid pin = ShortGuidUtils.Generate(parameter);
            foreach (Composite c in commands.Entries)
                foreach (FunctionEntity f in c.functions)
                    if (!f.function.IsFunctionType && f.function == archetype.shortGUID && f.GetParameter(pin)?.content is cString s)
                        found.Add((c, f, s.value));
            return found;
        }
        #endregion
    }
}
