using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.ShaderTypes;
using CathodeLib;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Submesh = CATHODE.Models.CS2.Component.LOD.Submesh;

namespace OpenCAGE.Brushwork
{
    /// <summary>
    /// "Create collision to match": collision for a ModelReference made from its LOD 0 triangles through
    /// <see cref="BrushCollision.ApplyTriangles"/>, as brushes are. See-through submeshes are left out unless asked for, slivers
    /// dropped and corners welded; the physics material is the render material covering most of the surface. The entity's old
    /// collision is replaced (its proxies left for the collision compactor). One undo step.
    /// </summary>
    public static class CollisionMatch
    {
        public sealed class Options
        {
            /// <summary>solid, world or ballistic.</summary>
            public string Mode = "solid";
            /// <summary>Leave out glass, decals, blended and effect submeshes.</summary>
            public bool SkipSeeThrough = true;
            /// <summary>Triangles smaller than this (square metres) are left out.</summary>
            public float MinimumTriangleArea = 1e-5f;
            /// <summary>Corners closer than this (metres) are made one.</summary>
            public float Weld = 0.001f;
        }

        public sealed class Report
        {
            public int Submeshes, SkippedSubmeshes, Triangles, DroppedTriangles;
            public List<string> Skipped = new List<string>();
            public Materials.Material Material;
            public BrushCollision.Outcome Outcome;
            public List<string> Notes = new List<string>();
        }

        /// <summary>What a ModelReference draws: its renderable run (own resources or its 'resource' parameter), or null.</summary>
        public static List<RenderableElements.Element> RunOf(FunctionEntity entity) => entity?.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;

        /// <summary>Whether collision can be made to match this entity: a ModelReference drawing something, and not a pair's ballistic half.</summary>
        public static bool CanMatch(Level level, Composite composite, Entity entity)
        {
            if (!(entity is FunctionEntity function) || function.function != FunctionType.ModelReference) return false;
            List<RenderableElements.Element> run = RunOf(function);
            if (run == null || !run.Any(o => o?.Model != null)) return false;
            return CollisionPairs.FindOriginal(level?.Commands, composite, function) == null;
        }

        /// <summary>A material drawn see-through or as an effect: no collision of its own wanted.</summary>
        public static bool IsSeeThrough(Materials.Material material)
        {
            Shaders.Shader shader = material?.Shader;
            if (shader == null) return false;
            string family = shader.Ubershader.ToString();
            foreach (string word in new[] { "DECAL", "GLASS", "PARTICLE", "RIBBON", "FOG", "WATER", "REFRACTION", "EFFECT", "LIGHT", "VISOR", "LENS", "OVERLAY" })
                if (family.Contains(word)) return true;
            if ((shader.UbershaderRequirementFlags & (1L << (int)SHADER_REQUIREMENTS.FORCE_TO_ALPHA)) != 0) return true;
            foreach (string feature in new[] { "FORCE_TO_ALPHA", "USE_ALPHA_AS_BLENDFACTOR", "GLASS" })
            {
                int? bit = ShaderUtility.GetShaderFunctionalityIndex(shader.Ubershader, ShaderIndexType.FEATURES, feature);
                if (bit.HasValue && (shader.UbershaderFeatureFlags & (1L << bit.Value)) != 0) return true;
            }
            return false;
        }

        /// <summary>The triangles collision is made from, with what was left out, and the material covering most of them.</summary>
        public static CollisionProxyImporter.MeshSource BuildMesh(List<RenderableElements.Element> run, Options options, Report report, string name)
        {
            CollisionProxyImporter.MeshSource mesh = new CollisionProxyImporter.MeshSource() { Name = name ?? "" };
            Dictionary<(long, long, long), int> welded = new Dictionary<(long, long, long), int>();
            Dictionary<Materials.Material, double> area = new Dictionary<Materials.Material, double>();
            double weld = Math.Max(options.Weld, 1e-6);
            int Vertex(Vector3 p)
            {
                (long, long, long) key = ((long)Math.Round(p.X / weld), (long)Math.Round(p.Y / weld), (long)Math.Round(p.Z / weld));
                if (!welded.TryGetValue(key, out int at))
                {
                    at = mesh.Positions.Count;
                    mesh.Positions.Add(p);
                    welded[key] = at;
                }
                return at;
            }

            HashSet<Submesh> seen = new HashSet<Submesh>();
            foreach (RenderableElements.Element element in run ?? new List<RenderableElements.Element>())
            {
                if (element?.Model == null || !seen.Add(element.Model)) continue;
                report.Submeshes++;
                Materials.Material material = element.Material ?? element.Model.Material;
                if (options.SkipSeeThrough && IsSeeThrough(material))
                {
                    report.SkippedSubmeshes++;
                    report.Skipped.Add(material?.Name ?? "(no material)");
                    continue;
                }
                cMesh source = element.Model.ToMesh();
                if (source?.Vertices == null || source.Indices == null) continue;
                for (int i = 0; i + 2 < source.Indices.Count; i += 3)
                {
                    Vector3 a = source.Vertices[source.Indices[i]], b = source.Vertices[source.Indices[i + 1]], c = source.Vertices[source.Indices[i + 2]];
                    float triangle = Vector3.Cross(b - a, c - a).Length() * 0.5f;
                    if (!(triangle >= options.MinimumTriangleArea))
                    {
                        report.DroppedTriangles++;
                        continue;
                    }
                    int ia = Vertex(a), ib = Vertex(b), ic = Vertex(c);
                    if (ia == ib || ib == ic || ia == ic)
                    {
                        report.DroppedTriangles++;
                        continue;
                    }
                    mesh.Indices.Add(ia); mesh.Indices.Add(ib); mesh.Indices.Add(ic);
                    report.Triangles++;
                    if (material != null) area[material] = (area.TryGetValue(material, out double sum) ? sum : 0) + triangle;
                }
            }
            report.Material = area.Count == 0 ? null : area.OrderByDescending(o => o.Value).First().Key;
            return mesh;
        }

        #region Collision a model already has
        /// <summary>Collision that already belongs to the model an entity draws, to reuse rather than make.</summary>
        public sealed class Candidate
        {
            /// <summary>"placed": the same model placed elsewhere in the level with collision (its row is copied); "model": the proxy the model's own submeshes name.</summary>
            public string Kind;
            public HavokPackfile.StaticCompoundShape Proxy;
            public CollisionMaps.COLLISION_MAPPING Row;
            /// <summary>The placement it was found on ("placed").</summary>
            public Composite Composite;
            public FunctionEntity Entity;
            /// <summary>The source's collision is a WORLD + BALLISTIC pair.</summary>
            public bool Paired;
            public double Score;
            public string Description;
        }

        /// <summary>The model an entity draws (the CS2 holding its run's first submesh) and the component, or null.</summary>
        public static Models.CS2 DrawnModel(Level level, FunctionEntity entity, out Models.CS2.Component component)
        {
            component = null;
            Submesh first = RunOf(entity)?.FirstOrDefault(o => o?.Model != null)?.Model;
            if (first == null || level?.Models == null) return null;
            foreach (Models.CS2 model in level.Models.Entries)
            {
                if (model == null) continue;
                foreach (Models.CS2.Component c in model.Components)
                    if (c.LODs.Any(l => l.Submeshes.Contains(first)))
                    {
                        component = c;
                        return model;
                    }
            }
            return null;
        }

        /// <summary>
        /// Collision that already belongs to what the entity draws, best first: the same model placed elsewhere in the level with collision
        /// (the same part of it ahead of another part), then the proxy the model's own submeshes name. Ranked within each by how
        /// closely the proxy's size matches the drawn part's bounds.
        /// </summary>
        public static List<Candidate> Candidates(Level level, Composite composite, FunctionEntity entity)
        {
            List<Candidate> found = new List<Candidate>();
            Models.CS2 model = DrawnModel(level, entity, out Models.CS2.Component component);
            HavokPackfile hkx = level?.Collision;
            if (model == null || level.Commands == null) return found;
            HashSet<Submesh> partSubmeshes = new HashSet<Submesh>(component?.LODs.SelectMany(l => l.Submeshes) ?? Enumerable.Empty<Submesh>());
            HashSet<Submesh> modelSubmeshes = new HashSet<Submesh>(model.Components.SelectMany(c => c.LODs).SelectMany(l => l.Submeshes));
            Vector3 size = PartSize(component);

            HashSet<HavokPackfile.StaticCompoundShape> seen = new HashSet<HavokPackfile.StaticCompoundShape>();
            foreach (Composite holder in level.Commands.Entries)
            {
                if (holder == null) continue;
                CollisionPairs.Names names = null;
                foreach (FunctionEntity other in holder.functions)
                {
                    if (other == entity || other.function != FunctionType.ModelReference) continue;
                    CollisionMaps.COLLISION_MAPPING row = CollisionPairs.RowOf(other);
                    if (row?.CollisionProxy == null || seen.Contains(row.CollisionProxy)) continue;
                    List<RenderableElements.Element> run = RunOf(other);
                    if (run == null || !run.Any(o => o?.Model != null && modelSubmeshes.Contains(o.Model))) continue;
                    names = names ?? new CollisionPairs.Names(level.Commands, holder);
                    if (CollisionPairs.FindOriginal(level.Commands, holder, other, names) != null) continue;
                    bool samePart = run.Any(o => o?.Model != null && partSubmeshes.Contains(o.Model));
                    seen.Add(row.CollisionProxy);
                    found.Add(new Candidate()
                    {
                        Kind = "placed",
                        Proxy = row.CollisionProxy,
                        Row = row,
                        Composite = holder,
                        Entity = other,
                        Paired = CollisionPairs.FindSibling(level.Commands, holder, other, names) != null || CollisionPairs.IsPair(row.Flags),
                        Score = (samePart ? 3 : 2) + SizeMatch(row.CollisionProxy, size),
                        Description = "the collision " + level.Commands.Utils.GetEntityName(holder, other) + " in " + holder.name + " has (proxy " + row.CollisionProxy.ProxyIndex + ")",
                    });
                }
            }
            //The proxy the model names for this part, as retail pairs them
            int named = component?.LODs.SelectMany(l => l.Submeshes).Select(o => o.CollisionProxyIndex).FirstOrDefault(o => o >= 0) ?? -1;
            HavokPackfile.StaticCompoundShape proxy = named >= 0 && hkx != null ? hkx.GetCompound(named) : null;
            /* Only when it is plausibly this part's: an imported or generated model's submeshes name proxy 0 by default, which is whatever
               the level's first compound is - its size has to agree with the part's, and it is never a world host */
            if (proxy != null && !seen.Contains(proxy) && !ReferenceEquals(proxy, hkx.WorldHostPrimary) && !ReferenceEquals(proxy, hkx.WorldHostSecondary) && SizeMatch(proxy, size) >= 0.6)
                found.Add(new Candidate() { Kind = "model", Proxy = proxy, Paired = true, Score = 1 + SizeMatch(proxy, size), Description = "the collision proxy the model names (" + named + ")" });
            return found.OrderByDescending(o => o.Score).ToList();
        }

        private static Vector3 PartSize(Models.CS2.Component component)
        {
            List<Submesh> submeshes = component?.LODs.FirstOrDefault()?.Submeshes;
            if (submeshes == null || submeshes.Count == 0) return Vector3.Zero;
            Vector3 min = submeshes[0].MinBounds, max = submeshes[0].MaxBounds;
            foreach (Submesh s in submeshes) { min = Vector3.Min(min, s.MinBounds); max = Vector3.Max(max, s.MaxBounds); }
            return max - min;
        }

        /* 0..1: how near the proxy's extent is to the drawn part's */
        private static double SizeMatch(HavokPackfile.StaticCompoundShape proxy, Vector3 size)
        {
            if (proxy == null || size == Vector3.Zero) return 0;
            Vector4 domain = proxy.DomainMax - proxy.DomainMin;
            Vector3 extent = new Vector3(domain.X, domain.Y, domain.Z);
            double a = extent.Length(), b = size.Length();
            if (a <= 0 || b <= 0) return 0;
            return Math.Min(a, b) / Math.Max(a, b);
        }

        /// <summary>Give the entity a candidate's collision: the same proxy and physics material, paired as the source is. One undo step.</summary>
        public static BrushCollision.Outcome UseCandidate(Level level, Composite composite, FunctionEntity entity, Candidate candidate, string label)
        {
            CollisionPairs.IWriter writer = CollisionPairs.Undoable(level, label);
            CollisionMaps.COLLISION_MAPPING current = CollisionPairs.RowOf(entity);
            BrushCollision.Outcome outcome = new BrushCollision.Outcome() { WorldBefore = current?.CollisionProxy?.ProxyIndex ?? -1 };
            CollisionMaps.CollisionFlags flags = candidate.Row != null ? CollisionPairs.WorldHalf(candidate.Row.Flags) : CollisionMaps.CollisionFlags.WORLD;
            if (!candidate.Paired && candidate.Row != null) flags = candidate.Row.Flags;
            using (UndoStack.Current.BeginGroup(label))
            {
                CollisionMaps.COLLISION_MAPPING row = new CollisionMaps.COLLISION_MAPPING()
                {
                    Flags = flags,
                    CollisionInstance = null,
                    ResourceGUID = current?.ResourceGUID ?? ShortGuid.Invalid,
                    Entity = new EntityHandle(),
                    Material = candidate.Row?.Material,
                    CollisionProxy = candidate.Proxy,
                    MaterialMapping = candidate.Row?.MaterialMapping,
                    ZoneID = ShortGuid.Invalid,
                };
                //The proxy keeps the collision type its own instances were made with: a WORLD row takes a STANDARD copy of a BALLISTICS proxy
                if (!CollisionPairs.Suits(row.CollisionProxy, row.Flags))
                    row.CollisionProxy = CollisionPairs.ProxyFor(level, row.CollisionProxy, CollisionPairs.FilterFor(row.Flags), CollisionPairs.UserData(level, row.Material, row.CollisionProxy), writer, out _);
                writer.SetCollision(composite, entity, row);
                outcome.WorldProxy = row.CollisionProxy?.ProxyIndex ?? -1;
                if (candidate.Paired)
                    outcome.BallisticProxy = CollisionPairs.SyncSibling(level, composite, entity, row, writer)?.Twin?.ProxyIndex ?? -1;
            }
            return outcome;
        }

        /// <summary>
        /// After a ModelReference was given another model (the Resources window): it has no collision, so offer what the model already
        /// has, best first - or, with none, collision made to match. UI thread.
        /// </summary>
        public static void OfferAfterModelChange(System.Windows.Forms.IWin32Window owner, Level level, Composite composite, FunctionEntity entity)
        {
            if (level == null || composite == null || entity == null || CollisionPairs.RowOf(entity) != null || !CanMatch(level, composite, entity)) return;
            if (BrushService.IsBrush(level, composite, entity)) return;
            string name = level.Commands.Utils.GetEntityName(composite, entity);
            Candidate best = Candidates(level, composite, entity).FirstOrDefault();
            System.Windows.Forms.DialogResult answer;
            if (best != null)
            {
                answer = System.Windows.Forms.MessageBox.Show(owner, "The model " + name + " now draws already has collision: " + best.Description + ".\n\nGive " + name + " the same collision?\n\n(No makes new collision to match it instead; Cancel leaves it without.)",
                    "Collision", System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Question);
                if (answer == System.Windows.Forms.DialogResult.Yes)
                {
                    UseCandidate(level, composite, entity, best, "Give " + name + " its model's collision");
                    return;
                }
                if (answer == System.Windows.Forms.DialogResult.Cancel) return;
            }
            else
            {
                answer = System.Windows.Forms.MessageBox.Show(owner, name + " has no collision, and the model it now draws has none anywhere in the level.\n\nCreate collision to match it (walked on and shot at)?",
                    "Collision", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Question);
                if (answer != System.Windows.Forms.DialogResult.Yes) return;
            }
            try { Apply(level, composite, entity, new Options(), "Create collision to match " + name); }
            catch (BrushException e) { System.Windows.Forms.MessageBox.Show(owner, e.Message, "Collision", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning); }
        }
        #endregion

        /// <summary>Give the entity collision made to match what it draws, replacing what it had. One undo step. Throws <see cref="BrushException"/> with the reason when it can't.</summary>
        public static Report Apply(Level level, Composite composite, FunctionEntity entity, Options options, string label)
        {
            if (level?.Commands == null || composite == null || composite.GetEntityByID(entity.shortGUID) != entity) throw new BrushException("That entity is not in the composite.");
            if (!CanMatch(level, composite, entity)) throw new BrushException("Collision to match is made for a ModelReference that draws a model (not a collision pair's ballistic half).");
            string mode = (options.Mode ?? "solid").Trim().ToLowerInvariant();
            if (mode == "none" || !BrushCollision.IsMode(mode)) throw new BrushException("The collision is solid, world or ballistic.");
            Report report = new Report();
            string name = level.Commands.Utils.GetEntityName(composite, entity);
            CollisionProxyImporter.MeshSource mesh = BuildMesh(RunOf(entity), options, report, name);
            if (mesh.TriangleCount == 0)
                throw new BrushException(name + " draws nothing solid to make collision from" + (report.SkippedSubmeshes != 0 ? " (" + report.SkippedSubmeshes + " see-through submesh(es) left out)." : "."));
            using (UndoStack.Current.BeginGroup(label ?? "Create collision to match " + name))
            {
                //A different source than any proxy the entity has: always made (or reused from this session's cache), never kept
                report.Outcome = BrushCollision.ApplyTriangles(level, composite, entity, mode, () => mesh, BrushGeometry.HashOf(mesh), null, report.Material, label ?? "Create collision to match " + name, report.Notes);
            }
            if (report.Outcome == null)
                throw new BrushException(report.Notes.FirstOrDefault() ?? "No collision could be made.");
            return report;
        }
    }
}
