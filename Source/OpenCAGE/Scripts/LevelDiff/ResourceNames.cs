using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenCAGE.LevelDiff
{
    /// <summary>
    /// Names a level's resources for <see cref="CathodeJson"/> and finds them again in another level. A material or model is its name,
    /// plus a short content hash when the name is shared ("01 - DEFAULT {a1b2c3d4}"), so renumbered tables don't read as changes. A
    /// collision proxy is a hash of its geometry and instances, not its number (compaction renumbers proxies).
    /// Build it on the thread that owns the level.
    /// </summary>
    public sealed class ResourceNames
    {
        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T o) => RuntimeHelpers.GetHashCode(o);
        }

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public Level Level { get; }
        private readonly Dictionary<Models.CS2.Component.LOD.Submesh, (string model, int c, int l, int s)> _submeshes = new Dictionary<Models.CS2.Component.LOD.Submesh, (string, int, int, int)>(new RefEq<Models.CS2.Component.LOD.Submesh>());
        private readonly Dictionary<Materials.Material, string> _materials = new Dictionary<Materials.Material, string>(new RefEq<Materials.Material>());
        private readonly Dictionary<Models.CS2, string> _models = new Dictionary<Models.CS2, string>(new RefEq<Models.CS2>());
        private readonly Dictionary<string, Models.CS2> _modelsById = new Dictionary<string, Models.CS2>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Models.CS2>> _modelsByName = new Dictionary<string, List<Models.CS2>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Materials.Material> _materialsById = new Dictionary<string, Materials.Material>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Materials.Material>> _materialsByName = new Dictionary<string, List<Materials.Material>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<HavokPackfile.StaticCompoundShape, string> _proxies;

        public ResourceNames(Level level)
        {
            Level = level;
            if (level == null) return;
            foreach (Materials.Material material in level.Materials?.Entries ?? new List<Materials.Material>())
            {
                if (material == null) continue;
                string name = material.Name ?? "";
                if (!_materialsByName.TryGetValue(name, out List<Materials.Material> same)) _materialsByName[name] = same = new List<Materials.Material>();
                same.Add(material);
            }
            foreach (KeyValuePair<string, List<Materials.Material>> group in _materialsByName)
                foreach (Materials.Material m in group.Value)
                {
                    if (_materials.ContainsKey(m)) continue;
                    string id = group.Value.Count == 1 ? group.Key : group.Key + " {" + Short(MaterialContent(m)) + "}";
                    _materials[m] = id;
                    if (!_materialsById.ContainsKey(id)) _materialsById[id] = m;
                }
            foreach (Models.CS2 model in level.Models?.Entries ?? new List<Models.CS2>())
            {
                if (model == null) continue;
                string name = model.Name ?? "";
                if (!_modelsByName.TryGetValue(name, out List<Models.CS2> same)) _modelsByName[name] = same = new List<Models.CS2>();
                same.Add(model);
            }
            foreach (KeyValuePair<string, List<Models.CS2>> group in _modelsByName)
                foreach (Models.CS2 model in group.Value)
                {
                    if (_models.ContainsKey(model)) continue;
                    string id = group.Value.Count == 1 ? group.Key : group.Key + " {" + Short(ModelContent(model)) + "}";
                    _models[model] = id;
                    if (!_modelsById.ContainsKey(id)) _modelsById[id] = model;
                    for (int c = 0; c < model.Components.Count; c++)
                        for (int l = 0; l < model.Components[c].LODs.Count; l++)
                            for (int s = 0; s < model.Components[c].LODs[l].Submeshes.Count; s++)
                            {
                                Models.CS2.Component.LOD.Submesh sub = model.Components[c].LODs[l].Submeshes[s];
                                if (sub != null && !_submeshes.ContainsKey(sub)) _submeshes[sub] = (id, c, l, s);
                            }
                }
        }

        #region Identities
        private static string Short(string text)
        {
            ulong h = 14695981039346656037UL;
            foreach (char ch in text) { h ^= ch; h *= 1099511628211UL; }
            return h.ToString("x16").Substring(0, 8);
        }

        private static string F(float f) => f.ToString("R", Invariant);

        /// <summary>What a material is, apart from its name and where its textures sit (a save copies global textures into the level).</summary>
        public static string MaterialContent(Materials.Material m)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(m.Shader == null ? "-" : m.Shader.Ubershader + "/" + m.Shader.Technique + "/" + m.Shader.PermutationHash.ToString("x8")).Append('|');
            foreach (TexturePtr t in m.TextureReferences) sb.Append(t?.Texture?.Name ?? "-").Append(',');
            foreach (List<float> list in new[] { m.EngineConstants, m.VertexShaderConstants, m.PixelShaderConstants, m.HullShaderConstants, m.DomainShaderConstants })
            {
                sb.Append('|');
                if (list != null) foreach (float f in list) sb.Append(F(f)).Append(',');
            }
            sb.Append('|').Append(m.OfflineLightFeatures == null ? "-" : Fields.Line(m.OfflineLightFeatures, null, 2));
            sb.Append('|').Append(m.PhysicalMaterialIndex).Append('|').Append(m.EnvironmentMapIndex).Append('|').Append(m.Priority);
            return sb.ToString();
        }

        private static string ModelContent(Models.CS2 model)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Models.CS2.Component c in model.Components)
                foreach (Models.CS2.Component.LOD l in c.LODs)
                    foreach (Models.CS2.Component.LOD.Submesh s in l.Submeshes)
                        if (s != null) sb.Append(s.VertexCount).Append('/').Append(s.IndexCount).Append('/').Append(Fields.Hash(s.Data)).Append(';');
            return sb.ToString();
        }

        public string MaterialName(Materials.Material material)
        {
            if (material == null) return null;
            return _materials.TryGetValue(material, out string name) ? name : material.Name;
        }

        public string ModelName(Models.CS2 model)
        {
            if (model == null) return null;
            return _models.TryGetValue(model, out string name) ? name : model.Name;
        }

        /// <summary>The material a name (as <see cref="MaterialName"/> gives it) is, here: that exact one, else the only one of the plain name.</summary>
        public Materials.Material FindMaterial(string name)
        {
            if (name == null) return null;
            if (_materialsById.TryGetValue(name, out Materials.Material m)) return m;
            string plain = Plain(name);
            if (_materialsByName.TryGetValue(plain, out List<Materials.Material> same) && same.Count == 1) return same[0];
            return null;
        }

        public Models.CS2 FindModel(string name)
        {
            if (name == null) return null;
            if (_modelsById.TryGetValue(name, out Models.CS2 m)) return m;
            string plain = Plain(name);
            if (_modelsByName.TryGetValue(plain, out List<Models.CS2> same) && same.Count == 1) return same[0];
            return null;
        }

        private static string Plain(string name)
        {
            int brace = name.LastIndexOf(" {", StringComparison.Ordinal);
            return brace > 0 && name.EndsWith("}") ? name.Substring(0, brace) : name;
        }

        /// <summary>A collision proxy by what it holds: "proxy " + a hash of its triangles, instances and materials (renumbering leaves it alone).</summary>
        public string ProxyName(HavokPackfile.StaticCompoundShape proxy)
        {
            if (proxy == null) return "none";
            if (_proxies == null) _proxies = new Dictionary<HavokPackfile.StaticCompoundShape, string>(new RefEq<HavokPackfile.StaticCompoundShape>());
            if (_proxies.TryGetValue(proxy, out string name)) return name;
            //Order doesn't count: a build or a compaction lists the same instances (and so the same triangles) in another order. Each
            //triangle (its corners rounded to 0.1 mm) is hashed and the hashes added up; the instances' descriptions are sorted
            ulong triangles = 0;
            int count = 0;
            string unreadable = "";
            try
            {
                HavokPackfile.PreviewMesh mesh = Level?.Collision?.BuildBakeMesh(proxy);
                if (mesh != null)
                    for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
                    {
                        ulong h = 14695981039346656037UL;
                        for (int k = 0; k < 3; k++)
                        {
                            Vector3 p = mesh.Positions[mesh.Indices[i + k]];
                            foreach (long v in new[] { (long)Math.Round(p.X * 10000), (long)Math.Round(p.Y * 10000), (long)Math.Round(p.Z * 10000) })
                            { h ^= (ulong)v; h *= 1099511628211UL; }
                        }
                        unchecked { triangles += h * 0x9E3779B97F4A7C15UL + (h >> 29); }
                        count++;
                    }
            }
            catch (Exception e) { unreadable = "unreadable " + e.GetType().Name; }
            List<string> instances = proxy.Instances.Select(o => o.FilterInfo + "/" + o.ChildFilterInfoMask + "/" + o.UserData + "/" + MaterialName(o.Material) + "/" + o.ShapeClassName).OrderBy(o => o, StringComparer.Ordinal).ToList();
            name = "proxy " + Short(triangles.ToString("x16") + "|" + count + "|" + unreadable + "|" + string.Join("|", instances)) + " (" + proxy.Instances.Count + " instances)";
            _proxies[proxy] = name;
            return name;
        }
        #endregion

        #region Elements
        public JObject Element(RenderableElements.Element e)
        {
            JObject o;
            if (e.Model != null && _submeshes.TryGetValue(e.Model, out (string model, int c, int l, int s) at))
                o = new JObject() { ["model"] = at.model, ["sub"] = new JArray(at.c, at.l, at.s) };
            else
                o = new JObject() { ["model"] = e.Model == null ? null : "?", ["sub"] = new JArray() };
            o["material"] = MaterialName(e.Material);
            return o;
        }

        public void Resolve(RenderableElements.Element e, JObject o)
        {
            string modelName = (string)o["model"];
            if (modelName != null)
            {
                Models.CS2 model = FindModel(modelName);
                JArray sub = o["sub"] as JArray;
                if (model == null || sub == null || sub.Count != 3)
                    throw new RecipeException("the model " + modelName + " is not in this level (port or import it first)");
                int c = (int)sub[0], l = (int)sub[1], s = (int)sub[2];
                if (c >= model.Components.Count || l >= model.Components[c].LODs.Count || s >= model.Components[c].LODs[l].Submeshes.Count)
                    throw new RecipeException("the model " + modelName + " in this level has no submesh " + c + "." + l + "." + s);
                e.Model = model.Components[c].LODs[l].Submeshes[s];
            }
            string materialName = (string)o["material"];
            if (materialName != null)
            {
                e.Material = FindMaterial(materialName);
                if (e.Material == null) throw new RecipeException("the material " + materialName + " is not in this level (port or import it first)");
            }
        }
        #endregion

        #region Collision, physics, animation
        public JObject Collision(CollisionMaps.COLLISION_MAPPING mapping)
        {
            JObject o = new JObject()
            {
                ["proxy"] = mapping.CollisionProxy == null ? "none" : ProxyName(mapping.CollisionProxy),
                ["instance"] = mapping.CollisionInstance == null ? "none" : Fields.Line(mapping.CollisionInstance, this, 1),
                ["flags"] = (int)mapping.Flags,
                ["entity"] = new JArray(CathodeJson.Id(mapping.Entity?.entity_id ?? ShortGuid.Invalid), CathodeJson.Id(mapping.Entity?.composite_instance_id ?? ShortGuid.Invalid)),
                ["resource"] = CathodeJson.Id(mapping.ResourceGUID),
                ["zone"] = CathodeJson.Id(mapping.ZoneID),
            };
            if (mapping.Material != null) o["material"] = MaterialName(mapping.Material);
            if (mapping.MaterialMapping != null) o["mapping"] = mapping.MaterialMapping.Name;
            return o;
        }

        /// <summary>The target's collision row for a described one: the row whose description is the same (a copy of the same level).</summary>
        public CollisionMaps.COLLISION_MAPPING FindCollision(JObject described, IdMap map)
        {
            JObject want = (JObject)described.DeepClone();
            if (map != null && want["entity"] is JArray entity && entity.Count == 2)
                want["entity"] = new JArray(CathodeJson.Id(map.Map(CathodeJson.ToId(entity[0]))), CathodeJson.Id(map.Map(CathodeJson.ToId(entity[1]))));
            string text = CathodeJson.Text(want);
            foreach (CollisionMaps.COLLISION_MAPPING row in Level?.CollisionMaps?.Entries ?? new List<CollisionMaps.COLLISION_MAPPING>())
                if (row != null && CathodeJson.Text(Collision(row)) == text)
                    return row;
            throw new RecipeException("its collision row (" + (string)described["proxy"] + ") is not in this level: collision comes with a model's import or a port, not a recipe");
        }

        public JToken PhysicsSystem(ResourceReference r)
        {
            if (r.PhysicsSystem?.Name != null) return new JArray(r.PhysicsSystemIndex, r.PhysicsSystem.Name);
            return new JValue(r.PhysicsSystemIndex);
        }

        public void SetPhysicsSystem(ResourceReference r, JToken described)
        {
            int index = described is JArray a ? (int)a[0] : (int)described;
            string name = described is JArray b && b.Count > 1 ? (string)b[1] : null;
            HavokPackfile.PhysicsSystem system = Level?.Physics?.PhysicsSystems?.FirstOrDefault(o => o != null && o.SystemIndex == index);
            if (name != null && (system == null || system.Name != name))
            {
                //Renumbered: the same system under another number
                system = Level?.Physics?.PhysicsSystems?.FirstOrDefault(o => o != null && o.Name == name);
                if (system == null) throw new RecipeException("its physics system " + name + " is not in this level: physics comes with an import, not a recipe");
                index = system.SystemIndex;
            }
            r.PhysicsSystem = system;
            r.PhysicsSystemIndex = index;
        }

        public JToken AnimatedModel(EnvironmentAnimations.EnvironmentAnimation animation) => new JArray(animation.ID, animation.SkeletonName ?? "");

        public EnvironmentAnimations.EnvironmentAnimation FindAnimatedModel(JToken described)
        {
            int id = described is JArray a ? (int)a[0] : (int)described;
            string skeleton = described is JArray b && b.Count > 1 ? (string)b[1] : null;
            EnvironmentAnimations.EnvironmentAnimation found = Level?.EnvironmentAnimations?.Entries?.FirstOrDefault(o => o != null && o.ID == id && (skeleton == null || (o.SkeletonName ?? "") == skeleton));
            if (found == null) throw new RecipeException("its animated model " + id + " (" + skeleton + ") is not in this level: environment animations come with an import, not a recipe");
            return found;
        }
        #endregion
    }
}
