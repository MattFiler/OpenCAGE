using AlienPAK;
using Assimp;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.Rigging
{
    /// <summary>
    /// The level's half of an imported animated prop (<see cref="PropRig"/>, <see cref="PropRigBuilder"/>): its parts as a model, and a
    /// composite with an EnvironmentModelReference whose ANIMATED_MODEL entry ties them to the rig, plus a PlayEnvironmentAnimation.
    /// Rigid parts are one component per bone, modelled about the bone and drawn by a ModelReference where it rests; BoneMappings[bone]
    /// is that ModelReference's RENDERABLE_INSTANCE resource id. Skinned parts are one model listed in MeshMappings. As in shipped
    /// props, inverse binds invert the rig's rest in the prop's space and the Havok-to-CATHODE frames are the identity.
    /// </summary>
    public static class PropImport
    {
        /// <summary>
        /// Rewrites the scene and its import plan for the model import: rigid parts about their bones, one component per bone named
        /// after it, no skinning; skinned parts in the prop's space with bones named ModelIO.BoneName.
        /// </summary>
        public static void ApplyToScene(PropRig rig, Scene scene, ModelIO.ImportPlan plan)
        {
            float unit = rig.UnitScale;
            bool rigid = rig.Parts.All(p => p.Weights == null);
            foreach (PropRig.Part part in rig.Parts)
            {
                Mesh mesh = scene.Meshes[part.MeshIndex];
                Vector3[] positions = rigid ? part.LocalPositions : part.Positions;
                Vector3[] normals = rigid ? part.LocalNormals : part.Normals;
                for (int i = 0; i < positions.Length && i < mesh.VertexCount; i++)
                {
                    mesh.Vertices[i] = RigMath.A(positions[i] * unit);
                    if (normals != null && mesh.Normals.Count == mesh.VertexCount) mesh.Normals[i] = RigMath.A(normals[i]);
                }
                //a part's tangents were in its node's space; the importer rebuilds a basis where the format wants one
                mesh.Tangents.Clear();
                mesh.BiTangents.Clear();
                mesh.Bones.Clear();
                if (rigid) continue;

                Dictionary<int, Bone> bones = new Dictionary<int, Bone>();
                Bone BoneFor(int joint)
                {
                    if (!bones.TryGetValue(joint, out Bone bone))
                    {
                        Matrix4x4 restFile = rig.Joints[joint].Rest;
                        restFile.Translation *= unit;
                        Matrix4x4.Invert(restFile, out Matrix4x4 offset);
                        bones[joint] = bone = new Bone() { Name = ModelIO.BoneName(joint, rig.Joints[joint].Name), OffsetMatrix = RigMath.ToAssimp(offset) };
                        mesh.Bones.Add(bone);
                    }
                    return bone;
                }
                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    if (part.Weights == null) { BoneFor(part.Joint).VertexWeights.Add(new VertexWeight(i, 1f)); continue; }
                    List<(int joint, float weight)> w = part.Weights[i].Count == 0 ? new List<(int, float)>() { (0, 1f) } : part.Weights[i];
                    float total = w.Sum(x => x.weight);
                    foreach (var g in w.GroupBy(x => x.joint))
                        BoneFor(g.Key).VertexWeights.Add(new VertexWeight(i, g.Sum(x => x.weight) / total));
                }
            }

            //One component per bone, its LOD named after the bone (how the composite finds each part's bone), untransformed: the part
            //is already about its bone, and its ModelReference puts it where the bone rests
            if (rigid && plan != null)
            {
                plan.Components.Clear();
                foreach (IGrouping<int, PropRig.Part> byJoint in rig.Parts.GroupBy(p => p.Joint).OrderBy(g => g.Key))
                {
                    ModelIO.PlannedLOD lod = new ModelIO.PlannedLOD() { Name = rig.Joints[byJoint.Key].Name };
                    foreach (PropRig.Part part in byJoint)
                        lod.Submeshes.Add(new ModelIO.PlannedSubmesh() { MeshIndex = part.MeshIndex, MeshName = scene.Meshes[part.MeshIndex].Name, Transform = Matrix4x4.Identity });
                    ModelIO.PlannedComponent component = new ModelIO.PlannedComponent();
                    component.LODs.Add(lod);
                    plan.Components.Add(component);
                }
            }
        }

        public class Placed
        {
            public Composite Composite;
            public FunctionEntity Reference;
            public FunctionEntity Player;
            public List<string> Report = new List<string>();
        }

        /* The helper name shipped props give their MAX_WORLDSPACE helper */
        private const uint MaxWorldspaceHelper = 3215093471;

        private static EnvironmentAnimations.WeightedHelperData Helper(uint name, int bone) => new EnvironmentAnimations.WeightedHelperData
        {
            HelperName = name,
            BindPosition = new float[] { 0, 0, 0 },
            BindNormal = new float[] { 0, 1, 0 },
            BindTangent = new float[] { 1, 0, 0 },
            Indicies = new byte[] { (byte)bone, 0, 0, 0 },
            Weights = new byte[] { 255, 0, 0, 0 },
        };

        /* The composite's functions with this one first and the rest in their order, each keyed by its own id */
        private static void PutFirst(Composite composite, FunctionEntity first)
        {
            if (composite == null || first == null || !composite.functions_dictionary.Values.Contains(first)) return;
            List<FunctionEntity> rest = composite.functions_dictionary.Values.Where(f => !ReferenceEquals(f, first)).ToList();
            composite.functions_dictionary.Clear();
            composite.functions_dictionary.Add(first.shortGUID, first);
            foreach (FunctionEntity f in rest) composite.functions_dictionary[f.shortGUID] = f;
        }

        /// <summary>
        /// The composite placing an imported prop: its parts, the EnvironmentModelReference with its animated model, and (when
        /// <paramref name="clip"/> is given) a PlayEnvironmentAnimation playing it from the level's start. The model must already be in the level.
        /// </summary>
        public static Placed MakeComposite(Level level, Models.CS2 model, string compositeName, Skeleton skeleton, PropRig rig, string setName, string clip, bool loop)
        {
            Placed placed = new Placed();
            bool rigid = rig.Parts.All(p => p.Weights == null);
            ModelCompositeBuilder.Result made = ModelCompositeBuilder.Create(level, model, compositeName, skeleton, setName);
            placed.Composite = made.Composite;
            EnvironmentAnimations.EnvironmentAnimation entry = DisplayRigFit.AnimatedModelOf(made.Composite, out FunctionEntity reference);
            /* The composite builder gives the EnvironmentModelReference the composite's own id (a DisplayModel's shape, which character
             * spawners look for); shipped props give it an id of its own */
            if (reference != null && reference.shortGUID == made.Composite.shortGUID)
            {
                reference.shortGUID = ShortGuidUtils.GenerateRandom();
                level.Commands.Utils.SetEntityName(made.Composite, reference, setName);
            }
            /* ...and list it first, before the parts it moves, as shipped animated props do, so its ANIMATED_MODEL row comes before its
             * parts' rows in RESOURCES.BIN. This also re-keys it under the id changed above. */
            PutFirst(made.Composite, reference);
            placed.Reference = reference;
            if (entry != null)
            {
                List<Matrix4x4> pose = skeleton.GetModelSpacePose();
                entry.InverseBindPoses = pose.Select(m => { Matrix4x4.Invert(m, out Matrix4x4 i); return i; }).ToList();
                entry.HavokToCathodeMappings = pose.Select(m => Matrix4x4.Identity).ToList();
                /* Two helpers, as shipped props carry: one on the root named after the prop (the anim hash of the composite's leaf name,
                 * upper case), and one on the MAX_WORLDSPACE bone (always 3215093471; its string is in neither string table). */
                entry.HelperMatrices = new List<EnvironmentAnimations.WeightedHelperData>();
                string leaf = (compositeName ?? setName).Split('\\', '/').Last().ToUpperInvariant();
                entry.HelperMatrices.Add(Helper(Utilities.AnimationHashedString(leaf), 0));
                int worldspace = skeleton.Bones.FindIndex(b => string.Equals(b.Name, "MAX_WORLDSPACE", StringComparison.OrdinalIgnoreCase) || (b.Name ?? "").EndsWith(":MAX_WORLDSPACE", StringComparison.OrdinalIgnoreCase));
                if (worldspace > 0) entry.HelperMatrices.Add(Helper(MaxWorldspaceHelper, worldspace));
                if (rigid)
                {
                    /* Each part's ModelReference goes where its bone rests and the bone names the part's resource; nothing is skinned,
                     * so the mesh list is empty (as CONTROLS_HIDING_DOOR's) */
                    List<FunctionEntity> parts = made.Composite.functions.Where(f => f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.ModelReference).ToList();
                    int carried = 0;
                    foreach (FunctionEntity part in parts)
                    {
                        cResource resource = part.GetParameter(ShortGuids.resource)?.content as cResource;
                        ResourceReference renderable = resource?.value.FirstOrDefault(r => r.resource_type == ResourceType.RENDERABLE_INSTANCE);
                        Models.CS2.Component.LOD.Submesh first = renderable?.RenderableInstance?.FirstOrDefault()?.Model;
                        //The component this part draws (by its first submesh object), and so its bone (by the component's LOD name)
                        Models.CS2.Component component = first == null ? null : model.Components.FirstOrDefault(o => o.LODs.Count != 0 && o.LODs[0].Submeshes.Any(s => ReferenceEquals(s, first)));
                        string bone = component?.LODs[0].Name ?? "";
                        int j = rig.Joints.FindIndex(o => string.Equals(o.Name, bone, StringComparison.OrdinalIgnoreCase));
                        if (j < 0 || renderable == null) continue;
                        /* BoneMappings = ShortGuid(part name), as retail's, and the same id on the parameter and each of its references:
                         * the game finds a bone's part through RESOURCES.BIN, whose row the save keys by the parameter's id. */
                        string partName = (part.GetParameter("name")?.content as cString)?.value;
                        if (string.IsNullOrEmpty(partName)) partName = level.Commands.Utils.GetEntityName(made.Composite, part);
                        ShortGuid id = string.IsNullOrEmpty(partName) ? resource.shortGUID : ShortGuidUtils.Generate(partName);
                        resource.shortGUID = id;
                        foreach (ResourceReference r in resource.value) r.resource_id = id;
                        entry.BoneMappings[j] = id;
                        /* The game only animates an environment part that has a COLLISION.MAP row, so every part gets a proxy-less
                         * mapping (FIXED | RESOURCE, ghosted and frozen, keyed by the resource id); the save makes each placement's row
                         * from it (FIXED | PREBUILT, frozen, no Havok instance), as retail does. */
                        if (!resource.value.Any(r => r.resource_type == ResourceType.COLLISION_MAPPING))
                        {
                            CollisionMaps.COLLISION_MAPPING row = new CollisionMaps.COLLISION_MAPPING()
                            {
                                Flags = CollisionMaps.CollisionFlags.FIXED | CollisionMaps.CollisionFlags.RESOURCE | CollisionMaps.CollisionFlags.GHOSTED |
                                        CollisionMaps.CollisionFlags.PRE_GHOSTED | CollisionMaps.CollisionFlags.FROZEN | CollisionMaps.CollisionFlags.PRE_FROZEN,
                                ResourceGUID = id,
                                Entity = new EntityHandle() { entity_id = part.shortGUID, composite_instance_id = ShortGuid.Invalid },
                                ZoneID = ShortGuid.Invalid,
                            };
                            resource.value.Insert(0, new ResourceReference(ResourceType.COLLISION_MAPPING) { resource_id = id, entityID = part.shortGUID, CollisionMapping = row });
                            level.CollisionMaps?.Entries.Add(row);
                        }
                        Matrix4x4.Decompose(rig.Joints[j].Rest, out Vector3 _, out Quaternion rotation, out Vector3 translation);
                        if (part.GetParameter("position")?.content is cTransform at)
                        {
                            at.position = translation;
                            at.rotation = InstanceTransform.ToEulerDegrees(rotation);
                        }
                        carried++;
                    }
                    entry.MeshMappings = new List<ShortGuid>();
                    placed.Report.Add("Animated model " + entry.ID + " on " + setName + ": " + carried + " part(s), each placed where its bone rests and named in the bone's mapping.");
                }
                else
                    placed.Report.Add("Animated model " + entry.ID + " on " + setName + ": the model skinned to " + entry.InverseBindPoses.Count + " bones, bound where the rig rests them in the prop's space.");
            }
            //A rigid prop's bones are named by the parts they carry, so the builder's note on guessed bone ids does not apply
            placed.Report.AddRange(rigid ? made.Notes.Select(n => n.Contains("bone ids hashed") ? "Animated model " + entry?.ID + " on skeleton " + setName + ", built from the rig." : n) : made.Notes);
            if (string.IsNullOrEmpty(clip) || reference == null) return placed;

            /* The player: PlayEnvironmentAnimation {AnimationSet, Animation} with no geometry link, inside the composite as shipped props
             * have it (it plays the composite's own animated model), from the level's start (play_on_reset). One undo step. */
            McpScriptEdit.Run("Play " + clip + " on " + setName, made.Composite, edit =>
            {
                FunctionEntity player = edit.AddFunction(made.Composite, FunctionType.PlayEnvironmentAnimation, "Play_" + clip);
                edit.SetParameter(made.Composite, player, "AnimationSet", setName);
                edit.SetParameter(made.Composite, player, "Animation", clip);
                edit.SetParameter(made.Composite, player, "play_on_reset", true);
                if (loop) edit.SetParameter(made.Composite, player, "loop", true);
                placed.Player = player;
            }, new McpScriptEdit.RunOptions() { Show = false });
            placed.Report.Add("PlayEnvironmentAnimation Play_" + clip + " plays " + setName + "/" + clip + " on it from the start of the level" + (loop ? ", round and round" : "") + ".");
            return placed;
        }
    }
}
