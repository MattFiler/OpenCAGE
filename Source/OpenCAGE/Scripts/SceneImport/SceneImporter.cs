using AlienPAK;
using CATHODE;
using CATHODE.ShaderTypes;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Brushwork;
using OpenCAGE.MCP;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AssetKind = CathodeLib.AssetModificationInfoTable.AssetKind;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.SceneImport
{
    /// <summary>
    /// Carries out a <see cref="ScenePlan"/> in the open level, and describes one (the dry run), reusing MaterialGenerator, ModelIO.BuildCS2,
    /// ModelCompositeBuilder, CollisionMatch (WORLD + BALLISTIC pairs, see-through parts left out), make_physics_prop and McpScriptEdit.
    /// Everything on the script side is ONE undo step; models, materials and textures are not undoable (as import_model's are not) and stay
    /// as unused assets after an undo.
    /// </summary>
    internal static class SceneImporter
    {
        /// <summary>Set by the Import Scene window round its own calls (its worker thread): their undo step is the user's, not "AI: ".</summary>
        [ThreadStatic] internal static bool FromWindow;
        private static string Prefix => FromWindow ? "" : "AI: ";

        private static readonly CultureInfo Ic = CultureInfo.InvariantCulture;
        private static JArray V(Vector3 v, int digits = 3) => new JArray(Math.Round(v.X, digits), Math.Round(v.Y, digits), Math.Round(v.Z, digits));

        #region Describing (UI thread)
        /// <summary>What the plan will make, with the budgets it runs into. Reads the level, changes nothing.</summary>
        public static JObject Describe(ScenePlan plan, Level level)
        {
            SceneOptions options = plan.Options;
            int placements = plan.Placements.Count;
            int shared = plan.Models.Count(o => o.Placements.Count > 1);
            JObject summary = new JObject()
            {
                ["file"] = options.Path,
                ["scene"] = options.Name,
                ["file_unit"] = plan.UnitText,
                ["scale"] = options.Scale,
                ["nodes"] = plan.NodesVisited,
                ["placements"] = placements,
                ["models"] = plan.Models.Count,
                ["sharing"] = placements + " placement(s) drawn by " + plan.Models.Count + " model(s): " + shared + " model(s) placed more than once (" + plan.Models.Where(o => o.Placements.Count > 1).Sum(o => o.Placements.Count) + " placements share them)",
                ["meshes_in_file"] = plan.Scene.MeshCount,
                ["identical_meshes_merged"] = plan.MeshesMerged,
                ["triangles_placed"] = plan.Placements.Sum(o => o.Model.Triangles),
                ["triangles_in_models"] = plan.Models.Sum(o => o.Triangles),
                ["materials"] = plan.Materials.Count,
                ["groups"] = new JArray(plan.Groups),
                ["physics_models"] = plan.Models.Count(o => o.Physics),
                ["collision"] = options.Collision,
                ["lights"] = new JObject()
                {
                    ["omni"] = plan.Lights.Count(o => o.Type == "point"),
                    ["spot"] = plan.Lights.Count(o => o.Type == "spot"),
                    ["directional"] = plan.Lights.Count(o => o.Type == "directional"),
                },
            };
            if (plan.Excluded != 0) summary["excluded_nodes"] = plan.Excluded;

            JArray models = new JArray(plan.Models.Select(m =>
            {
                JObject item = new JObject()
                {
                    ["name"] = m.Name,
                    ["model"] = ModelAssetName(plan, m),
                    ["composite"] = plan.ModelsFolder + "\\" + m.Name,
                    ["meshes"] = new JArray(m.Meshes.Select(i => string.IsNullOrEmpty(plan.Scene.Meshes[i].Name) ? "mesh " + i : plan.Scene.Meshes[i].Name)),
                    ["placements"] = m.Placements.Count,
                    ["triangles"] = m.Triangles,
                    ["submeshes"] = m.Submeshes,
                    ["components"] = m.Components.Count,
                    ["baked"] = m.BakeText,
                    ["collision"] = m.Physics ? "physics prop (no static collision)" : options.Collision,
                };
                if (m.Physics) item["physics"] = true;
                if (m.SplitNotes.Count != 0) item["split"] = new JArray(m.SplitNotes);
                return item;
            }));

            JArray materials = new JArray(plan.Materials.Values.OrderBy(o => o.SourceIndex).Select(m => DescribeMaterial(m, null)));

            JArray lights = new JArray(plan.Lights.Select(DescribeLight));
            JArray groups = new JArray(plan.Groups.Select(g => new JObject()
            {
                ["composite"] = plan.GroupComposite(g),
                ["placed_in_root_as"] = options.Name + "_" + g,
                ["instances"] = plan.Placements.Count(o => o.Group == g),
            }));

            JObject result = new JObject()
            {
                ["summary"] = summary,
                ["models"] = models,
                ["materials"] = materials,
                ["groups"] = groups,
                ["lights"] = lights,
                ["budgets"] = Budgets(plan, level),
                ["placement_check"] = new JObject()
                {
                    ["placements"] = placements,
                    ["worst_mm"] = Math.Round(plan.WorstPlacementMillimetres, 3),
                    ["worst"] = plan.WorstPlacement,
                    ["how"] = "each placement's mesh box corners through the file's world matrix vs through the shared model's bake + the instance's transform",
                },
            };
            if (plan.LightFrameCheck != null && plan.LightFrameCheck.Count != 0) result["light_frame_check"] = plan.LightFrameCheck;
            List<string> warnings = new List<string>(plan.Warnings);
            warnings.AddRange(BudgetWarnings((JObject)result["budgets"]));
            foreach (SceneMaterial m in plan.Materials.Values.Where(o => o.Error != null))
                warnings.Add("Material '" + m.SourceName + "' cannot be generated (" + m.Error + "): its meshes use the fallback material.");
            if (level.Commands.Entries.Any(o => o != null && (o.name ?? "").StartsWith(plan.Folder + "\\", StringComparison.OrdinalIgnoreCase)))
                warnings.Add("NAME: the level already has composites under " + plan.Folder + " (a scene imported with this name): give the import another 'name'.");
            if (warnings.Count != 0) result["warnings"] = new JArray(warnings);
            if (plan.Notes.Count != 0) result["notes"] = new JArray(plan.Notes);
            return result;
        }

        private static string ModelAssetName(ScenePlan plan, SceneModel model) => plan.Folder + "\\" + model.Name + ".CS2";

        private static JObject DescribeMaterial(SceneMaterial m, Materials.Material made)
        {
            JObject item = new JObject() { ["source"] = m.SourceName, ["meshes"] = m.Meshes };
            MaterialGenerator.Plan p = m.Plan;
            if (p == null || p.Error != null)
            {
                item["error"] = m.Error ?? p?.Error ?? "not planned";
                item["uses"] = "the fallback material";
                return item;
            }
            item["name"] = made?.Name ?? (m.Reuses != null ? m.Reuses.Name + " (already in the level: reused)" : p.Name);
            item["family"] = p.Family.ToString();
            long mask = made?.Shader?.UbershaderFeatureFlags ?? p.Mask;
            item["mask"] = "0x" + mask.ToString("X");
            if (made == null) item["exact_mask"] = p.ExactMask;
            item["features"] = new JArray(p.Features);
            int priority = made?.Priority ?? MaterialGenerator.DefaultPriority(p.Family, mask);
            item["draw_pass"] = MaterialConsistency.DescribeDrawPass(priority);
            if (p.Opacity < 0.99f) item["see_through"] = "opacity " + Math.Round(p.Opacity, 3) + (p.Features.Contains("FORCE_TO_ALPHA") ? " (FORCE_TO_ALPHA, drawn in the see-through pass)" : " (the shipped permutations here lack FORCE_TO_ALPHA: drawn solid)");
            if (p.NeedsBothSides) item["double_sided"] = p.Features.Contains("DOUBLE_SIDED") ? "yes" : "wanted, but no shipped permutation here has it";
            if (p.CutOut != MaterialGenerator.CutOutSource.None) item["cut_out"] = p.CutOut.ToString();
            item["textures"] = new JArray(p.Textures.Select(t => new JObject()
            {
                ["sampler"] = t.Role.Sampler,
                ["source"] = t.SourceLabel,
                ["use"] = t.Existing != null ? "reuse " + t.Existing.Name : t.Usable ? "import as " + t.TextureName + " (" + FormatFor(t, p) + ")" : "skipped: " + t.Problem,
            }));
            if (p.Notes.Count != 0) item["notes"] = new JArray(p.Notes.Take(4));
            return item;
        }

        private static JObject DescribeLight(SceneLight l)
        {
            JObject item = new JObject()
            {
                ["name"] = l.Name,
                ["type"] = l.Type == "point" ? "OMNI" : l.Type == "spot" ? "SPOT" : "directional (not placed)",
                ["from"] = l.From,
            };
            if (l.Type != "directional")
            {
                item["position"] = V(l.Position);
                item["colour"] = V(l.Colour, 0);
                item["intensity"] = l.Intensity;
                item["range"] = l.Range + (l.SourceRange ? " (the file's)" : " (from its intensity)");
                if (l.Type == "spot")
                {
                    item["direction"] = V(l.Direction);
                    item["cone"] = new JArray(l.Inner, l.Outer);
                }
                item["cast_shadow"] = l.Shadow;
            }
            else
            {
                //towards the sun, as add_outdoor_lighting's 'sun' takes it
                item["sun"] = V(-l.Direction);
                item["colour"] = V(l.Colour, 0);
                item["add_outdoor_lighting"] = new JObject() { ["sun"] = V(-l.Direction), ["sun_colour"] = V(l.Colour, 0) };
            }
            return item;
        }
        #endregion

        #region Budgets
        private static JObject Budgets(ScenePlan plan, Level level)
        {
            JObject budgets = new JObject();

            //Texture memory: the level's streamed textures now (Level Check's estimate), plus what this import would add
            try
            {
                TextureBudget.Estimate estimate = TextureBudget.Of(level);
                Dictionary<string, double> added = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (SceneMaterial m in plan.Materials.Values)
                {
                    if (m.Plan == null || m.Plan.Error != null || m.Reuses != null) continue;
                    foreach (MaterialGenerator.PlannedTexture t in m.Plan.UsableTextures.Where(o => o.Existing == null))
                    {
                        string key = t.SourcePath ?? t.SourceLabel ?? t.TextureName;
                        if (key == null || added.ContainsKey(key)) continue;
                        added[key] = TextureBytes(t, m.Plan);
                    }
                }
                double newMB = added.Values.Sum() * TextureBudget.GameFactor / 1048576.0;
                double after = estimate.PeakMB + newMB;
                budgets["texture_memory"] = new JObject()
                {
                    ["heap_mb"] = estimate.HeapMB,
                    ["level_now_mb"] = Math.Round(estimate.PeakMB, 1),
                    ["new_textures"] = added.Count,
                    ["new_textures_mb"] = Math.Round(newMB, 1),
                    ["after_mb"] = Math.Round(after, 1),
                    ["ok"] = after <= estimate.HeapMB,
                    ["where"] = estimate.PeakWhere,
                };
            }
            catch (Exception e) { budgets["texture_memory"] = new JObject() { ["error"] = e.Message }; }

            //Elements per mover (the game draws the first 255 of a run) and triangles per submesh (21,845)
            int worstElements = plan.Models.Count == 0 ? 0 : plan.Models.Max(m => m.Components.Max(c => c.Sum(i => Pieces(plan.Scene.Meshes[i]))));
            budgets["elements_per_mover"] = new JObject()
            {
                ["limit"] = RenderableElements.MaxElementsPerInstance,
                ["largest"] = worstElements,
                ["ok"] = worstElements <= RenderableElements.MaxElementsPerInstance,
                ["models_cut_into_components"] = new JArray(plan.Models.Where(o => o.Components.Count > 1).Select(o => o.Name + ": " + o.Components.Count)),
            };
            budgets["triangles_per_submesh"] = new JObject()
            {
                ["limit"] = ModelIO.MaxTrianglesPerSubmesh,
                ["meshes_split"] = new JArray(plan.Models.SelectMany(o => o.SplitNotes).Distinct()),
            };

            //Lights: every placed light in a one-zone level can be in view; the game crashes on the 2049th unshadowed / 65th shadowed one in a view
            (int lights, int shadowed) now = CountLights(level.Commands);
            int adding = plan.Lights.Count(o => o.Type != "directional"), addingShadowed = plan.Lights.Count(o => o.Type != "directional" && o.Shadow);
            budgets["lights"] = new JObject()
            {
                ["placed_now"] = now.lights,
                ["adding"] = adding,
                ["after"] = now.lights + adding,
                ["in_view_crash_limit"] = 2048,
                ["shadowed_now"] = now.shadowed,
                ["shadowed_adding"] = addingShadowed,
                ["shadowed_after"] = now.shadowed + addingShadowed,
                ["shadowed_in_view_limit"] = 64,
                ["ok"] = now.lights + adding <= 2048 && now.shadowed + addingShadowed <= 64,
            };
            return budgets;
        }

        private static IEnumerable<string> BudgetWarnings(JObject budgets)
        {
            if (budgets["texture_memory"] is JObject t && t["ok"] != null && !(bool)t["ok"])
                yield return "TEXTURES: the level would need about " + t["after_mb"] + " MB of streamed textures at once, past the game's " + t["heap_mb"] + " MB heap (textures past it draw blocky): import with compact_textures (half the memory), zone the level, or raise the heap (set_texture_heap, global).";
            if (budgets["elements_per_mover"] is JObject e && e["ok"] != null && !(bool)e["ok"])
                yield return "ELEMENTS: a model part would draw more than 255 submeshes, and the game draws only the first 255 of one mover.";
            if (budgets["lights"] is JObject l && l["ok"] != null && !(bool)l["ok"])
                yield return "LIGHTS: the level would hold " + l["after"] + " lights (" + l["shadowed_after"] + " shadowed). The game crashes when more than 2048 unshadowed or 64 shadowed lights are drawn in one view: zone the level, or import with lights false.";
        }

        private static int Pieces(Assimp.Mesh mesh)
        {
            int triangles = mesh.Faces.Count(o => o.IndexCount == 3);
            return triangles > ModelIO.MaxTrianglesPerSubmesh ? (int)Math.Ceiling(triangles / 20000.0) : 1;
        }

        /// <summary>The level's placed lights (each LightReference times the times its composite is placed), and how many cast shadows.</summary>
        internal static (int lights, int shadowed) CountLights(Commands commands)
        {
            Dictionary<Composite, (int, int)> memo = new Dictionary<Composite, (int, int)>();
            HashSet<Composite> visiting = new HashSet<Composite>();
            (int, int) Count(Composite composite)
            {
                if (composite == null) return (0, 0);
                if (memo.TryGetValue(composite, out (int, int) known)) return known;
                if (!visiting.Add(composite)) return (0, 0);
                int lights = 0, shadowed = 0;
                foreach (FunctionEntity function in composite.functions)
                {
                    if (function.function.IsFunctionType)
                    {
                        if (function.function.AsFunctionType != FunctionType.LightReference) continue;
                        lights++;
                        if (function.GetParameter("cast_shadow")?.content is cBool shadow && shadow.value) shadowed++;
                        continue;
                    }
                    (int l, int s) inner = Count(commands.GetComposite(function.function));
                    lights += inner.l;
                    shadowed += inner.s;
                }
                visiting.Remove(composite);
                return memo[composite] = (lights, shadowed);
            }
            return commands?.EntryPoints == null || commands.EntryPoints.Length == 0 ? (0, 0) : Count(commands.EntryPoints[0]);
        }

        private static string FormatFor(MaterialGenerator.PlannedTexture t, MaterialGenerator.Plan p)
        {
            if (p.CompactTextures && t.Role.Format == Textures.TextureFormat.BC7 && t.Role.Sampler != "SEPARATE_ALPHA_MAP")
                return t.Role.Sampler == "DIFFUSE_MAP" && p.CutOut == MaterialGenerator.CutOutSource.BaseColourAlpha ? "DXT5" : "DXT1";
            return t.Format.ToString();
        }

        /// <summary>A planned texture's streamed size: its pixels at the format's bytes per pixel, plus a third for the mip chain.</summary>
        private static double TextureBytes(MaterialGenerator.PlannedTexture t, MaterialGenerator.Plan p)
        {
            int width = 1024, height = 1024;
            try
            {
                if (t.Embedded != null)
                {
                    if (t.Embedded.HasNonCompressedData) { width = t.Embedded.Width; height = t.Embedded.Height; }
                    else if (t.Embedded.HasCompressedData)
                        using (MemoryStream stream = new MemoryStream(t.Embedded.CompressedData))
                            ImageSize(stream, t.Embedded.CompressedFormatHint, ref width, ref height);
                }
                else if (t.SourcePath != null && File.Exists(t.SourcePath))
                    using (FileStream stream = File.OpenRead(t.SourcePath))
                        ImageSize(stream, Path.GetExtension(t.SourcePath), ref width, ref height);
            }
            catch { }
            string format = FormatFor(t, p);
            double perPixel = format == "DXT1" ? 0.5 : format == "A8R8G8B8" || format == "X8R8G8B8" ? 4 : 1;
            return width * (double)height * perPixel * 4.0 / 3.0;
        }

        private static void ImageSize(Stream stream, string kind, ref int width, ref int height)
        {
            byte[] header = new byte[20];
            int read = stream.Read(header, 0, header.Length);
            if (read >= 20 && header[0] == 'D' && header[1] == 'D' && header[2] == 'S' && header[3] == ' ')
            {
                height = BitConverter.ToInt32(header, 12);
                width = BitConverter.ToInt32(header, 16);
                return;
            }
            if (string.Equals((kind ?? "").TrimStart('.'), "tga", StringComparison.OrdinalIgnoreCase) && read >= 16)
            {
                width = BitConverter.ToUInt16(header, 12);
                height = BitConverter.ToUInt16(header, 14);
                return;
            }
            stream.Seek(0, SeekOrigin.Begin);
            using (System.Drawing.Image image = System.Drawing.Image.FromStream(stream, false, false))
            {
                width = image.Width;
                height = image.Height;
            }
        }
        #endregion

        #region Importing (the tool's thread)
        /// <summary>
        /// Import the planned scene into the open level: one undo step for everything on the script side. Call from a tool's thread
        /// (the steps run on the UI thread themselves). <paramref name="directional"/>: 'note' or 'outdoor_lighting'.
        /// </summary>
        public static JObject Import(McpCall call, ScenePlan plan, string directional)
        {
            SceneOptions options = plan.Options;
            string label = "Import scene " + options.Name;
            //read here, on the caller's thread (the window sets it there): the steps below run on the UI thread
            string prefix = Prefix;
            List<string> notes = new List<string>();
            JObject made = new JObject();
            object mark = null;
            int keepMax = 0;

            McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                McpEditor.RequireUndoIdle();
                if (level.Commands.Entries.Any(o => o != null && (o.name ?? "").StartsWith(plan.Folder + "\\", StringComparison.OrdinalIgnoreCase)))
                    throw new McpError(McpErrorCodes.Refused, "The level already has a scene called " + options.Name + " (composites under " + plan.Folder + "): give 'name' another value, or delete that scene's composites first.");
                mark = UndoStack.Current.Mark();
                keepMax = UndoStack.Current.MaxEdits;
                //every step of this import is kept until it is collapsed into one, however many there are
                UndoStack.Current.MaxEdits = Math.Max(keepMax, UndoStack.Current.UndoCount + 100000);
            });

            Exception failed = null;
            try
            {
                //---- materials, once per source material (not undoable: textures and shader entries go into the level)
                using (McpEditorTools.Heartbeat(call, "Generating " + plan.Materials.Count + " material(s)"))
                    McpEditor.UI(() => GenerateMaterials(plan, McpEditor.RequireLevel().Level, notes));

                //---- models (not undoable, as import_model's are not)
                using (McpEditorTools.Heartbeat(call, "Building " + plan.Models.Count + " model(s)"))
                    McpEditor.UI(() => BuildModels(plan, McpEditor.RequireLevel().Level, notes));

                //---- step 1: each model's composite, and its collision to match
                using (McpEditorTools.Heartbeat(call, "Making the models' composites and collision"))
                    McpEditor.UI(() => MakeComposites(plan, McpEditor.RequireLevel().Level, prefix + label, notes, made));

                //---- step 2: physics props, the way make_physics_prop builds them (spawned at load, keyed by name)
                JArray physics = new JArray();
                foreach (SceneModel model in plan.Models.Where(o => o.Physics && o.Composite != null))
                {
                    call.ThrowIfCancelled();
                    call.Progress("Making physics prop " + model.Name);
                    string entity = McpEditor.UI(() => model.Composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.ModelReference).Select(o => McpScript.Id(o.shortGUID)).FirstOrDefault());
                    int parts = McpEditor.UI(() => model.Composite.functions.Count(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.ModelReference));
                    if (entity == null || parts != 1)
                    {
                        notes.Add(model.Name + " is not one part (" + parts + " parts), so it stays a static model (make_physics_prop takes one ModelReference).");
                        McpEditor.UI(() => StaticInstead(plan, McpEditor.RequireLevel().Level, model, prefix + label, notes));
                        continue;
                    }
                    //Not a folder called PHYSICS: composites under REQUIRED_ASSETS, TEMPLATE or PHYSICS folders are never shown in the level
                    string propComposite = plan.Folder + "\\PhysicsProps\\" + model.Name;
                    try
                    {
                        JObject prop = McpEditorTools.Nested(call, "make_physics_prop", new JObject()
                        {
                            ["composite"] = model.CompositeName,
                            ["entity"] = entity,
                            ["name"] = propComposite,
                        });
                        physics.Add(new JObject() { ["model"] = model.Name, ["composite"] = propComposite, ["placements"] = model.Placements.Count, ["spawned"] = prop["spawned"] != null });
                    }
                    catch (McpError e)
                    {
                        notes.Add(model.Name + " could not be made a physics prop (" + e.Message + "); it stays a static model.");
                        McpEditor.UI(() => StaticInstead(plan, McpEditor.RequireLevel().Level, model, prefix + label, notes));
                    }
                }
                if (physics.Count != 0) made["physics_props"] = physics;

                //---- step 3: the group composites with their instances, and the lights
                using (McpEditorTools.Heartbeat(call, "Placing " + plan.Placements.Count + " instance(s) and " + plan.Lights.Count(o => o.Type != "directional") + " light(s)"))
                    PlaceEverything(call, plan, label, prefix, notes, made);

                //---- a sun, through add_outdoor_lighting, when asked
                SceneLight sun = plan.Lights.FirstOrDefault(o => o.Type == "directional");
                if (sun != null && directional == "outdoor_lighting")
                {
                    try
                    {
                        JObject outdoor = McpEditorTools.Nested(call, "add_outdoor_lighting", new JObject()
                        {
                            ["name"] = ScenePlan.EntityName(options.Name, "Scene"),
                            ["sun"] = V(-sun.Direction),
                            ["sun_colour"] = V(sun.Colour, 0),
                        });
                        made["outdoor_lighting"] = outdoor["made"] ?? outdoor["lights"] ?? new JValue("Lighting\\Outdoor_" + ScenePlan.EntityName(options.Name, "Scene"));
                    }
                    catch (McpError e) { notes.Add("The sun was not made (" + e.Message + ")."); }
                }
            }
            catch (Exception e)
            {
                failed = e;
            }
            finally
            {
                McpEditor.UI(() =>
                {
                    int steps = UndoStack.Current.StepsSince(mark);
                    if (steps > 1) UndoStack.Current.Collapse(steps, prefix + label);
                    UndoStack.Current.MaxEdits = keepMax;
                });
            }
            if (failed != null)
            {
                string why = failed is McpError mcp ? mcp.Message : failed.GetType().Name + ": " + failed.Message;
                throw new McpError(McpErrorCodes.Failed, "The scene import stopped part way: " + why + " What it made is one undo step ('" + prefix + label + "'): undo takes it out (models, materials and textures it built stay in the level, unused).");
            }

            JObject result = new JObject()
            {
                ["imported"] = options.Name,
                ["undo_step"] = prefix + label,
                ["made"] = made,
            };
            McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(false).Level;
                List<string> textureNotes = new List<string>();
                TextureBudget.NoteAfterImport(level, result, textureNotes);
                notes.AddRange(textureNotes);
                (int lights, int shadowed) count = CountLights(level.Commands);
                result["lights_in_level"] = new JObject() { ["placed"] = count.lights, ["shadowed"] = count.shadowed, ["in_view_crash_limit"] = 2048, ["shadowed_in_view_limit"] = 64 };
                Singleton.OnResourceModified?.Invoke();
                Singleton.Editor?.CompositeBrowser?.RefreshList();
            });
            foreach (string note in notes.Distinct().Take(40)) call.Note(note);
            call.Note("save_level with build=true puts the scene in the game. Undo takes the whole import out in one step; the models, materials and textures it built stay in the level (unused) - as after import_model. A big scene wants zones: auto_zone (a dry run first) splits the root's unzoned content, the scene's groups among it, into zones by where it sits.");
            return result;
        }

        private static void GenerateMaterials(ScenePlan plan, Level level, List<string> notes)
        {
            int materialsBefore = level.Materials?.Entries.Count ?? 0, texturesBefore = level.Textures?.Entries.Count ?? 0;
            foreach (SceneMaterial m in plan.Materials.Values.OrderBy(o => o.SourceIndex))
            {
                if (m.Plan == null || !m.Plan.CanGenerate) continue;
                string error = null;
                try { m.Made = MaterialGenerator.Generate(m.Plan, plan.Scene.Materials[m.SourceIndex], level, Singleton.PathToAI, out error); }
                catch (Exception e) { m.Made = null; error = e.Message; }
                if (m.Made == null)
                    notes.Add("Material '" + m.SourceName + "' could not be generated (" + (error ?? "no reason given") + "); its meshes use the fallback material.");
            }
            for (int i = materialsBefore; i < (level.Materials?.Entries.Count ?? 0); i++)
                EditHistory.AssetChanged(level, AssetKind.MATERIAL, level.Materials.Entries[i].Name);
            for (int i = texturesBefore; i < (level.Textures?.Entries.Count ?? 0); i++)
                EditHistory.AssetChanged(level, AssetKind.TEXTURE, level.Textures.Entries[i].Name);
            Singleton.OnResourceModified?.Invoke();
        }

        private static void BuildModels(ScenePlan plan, Level level, List<string> notes)
        {
            foreach (SceneModel model in plan.Models)
            {
                ModelIO.ImportPlan importPlan = new ModelIO.ImportPlan()
                {
                    Name = ModelAssetName(plan, model),
                    HasMetadata = false,
                    UnitScale = plan.UnitScale,
                    Scale = plan.Options.Scale,
                };
                foreach (List<int> component in model.Components)
                {
                    ModelIO.PlannedLOD lod = new ModelIO.PlannedLOD() { Name = model.Name };
                    foreach (int index in component)
                    {
                        Assimp.Mesh mesh = plan.Scene.Meshes[index];
                        plan.Materials.TryGetValue(mesh.MaterialIndex, out SceneMaterial material);
                        lod.Submeshes.Add(new ModelIO.PlannedSubmesh()
                        {
                            MeshIndex = index,
                            MeshName = string.IsNullOrEmpty(mesh.Name) ? model.Name : mesh.Name,
                            Transform = model.Bake,
                            Material = material?.Made,
                        });
                    }
                    ModelIO.PlannedComponent planned = new ModelIO.PlannedComponent();
                    planned.LODs.Add(lod);
                    importPlan.Components.Add(planned);
                }
                Models.CS2 cs2 = ModelIO.BuildCS2(plan.Scene, importPlan, wanted => level.Materials.Entries.FirstOrDefault(o => o.Name == wanted), Singleton.FallbackMaterial, out List<string> warnings);
                foreach (string warning in warnings.Where(o => !ModelIO.IsPutRight(o)).Take(5))
                    notes.Add(model.Name + ": " + warning);
                if (cs2.Components.Count == 0)
                {
                    notes.Add(model.Name + ": none of its meshes could be converted, so it is not placed.");
                    continue;
                }
                cs2.Name = AssetName.MakeUnique(importPlan.Name, level.Models.Entries.Select(o => o.Name));
                level.Models.Entries.Add(cs2);
                EditHistory.AssetChanged(level, AssetKind.MODEL, cs2.Name);
                model.Built = cs2;
                model.ModelAssetName = cs2.Name;
            }
            Singleton.OnResourceModified?.Invoke();
        }

        /// <summary>Step 1 (UI thread): a composite per model (ModelCompositeBuilder), and collision to match on each part that is not a physics prop.</summary>
        private static void MakeComposites(ScenePlan plan, Level level, string stepLabel, List<string> notes, JObject made)
        {
            JArray composites = new JArray();
            int collided = 0, skippedSeeThrough = 0, collisionTriangles = 0;
            List<Composite> added = new List<Composite>();
            using (UndoStack.Current.BeginGroup(stepLabel))
            {
                foreach (SceneModel model in plan.Models.Where(o => o.Built != null))
                {
                    string name = plan.ModelsFolder + "\\" + model.Name;
                    Singleton.OnCompositeAddPending?.Invoke();
                    ModelCompositeBuilder.Result result = ModelCompositeBuilder.Create(level, model.Built, name, null, null);
                    model.Composite = result.Composite;
                    model.CompositeName = result.Composite.name;
                    UndoStack.Current.Record(new CompositeAddEdit(result.Composite, false, "Add " + result.Composite.name));
                    Singleton.OnCompositeAdded?.Invoke(result.Composite);
                    added.Add(result.Composite);
                    notes.AddRange(result.Notes);
                    JObject item = new JObject() { ["composite"] = result.Composite.name, ["model"] = model.Built.Name, ["parts"] = result.ModelReferences, ["placements"] = model.Placements.Count };

                    if (!model.Physics && plan.Options.Collision != "none")
                    {
                        foreach (FunctionEntity part in result.Composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.ModelReference).ToList())
                        {
                            try
                            {
                                CollisionMatch.Report report = CollisionMatch.Apply(level, result.Composite, part, new CollisionMatch.Options() { Mode = plan.Options.Collision }, stepLabel);
                                collided++;
                                collisionTriangles += report.Triangles;
                                skippedSeeThrough += report.SkippedSubmeshes;
                                item["collision"] = plan.Options.Collision + ", " + report.Triangles + " triangles" + (report.Material != null ? ", physics material from " + report.Material.Name : "") + (report.SkippedSubmeshes != 0 ? ", " + report.SkippedSubmeshes + " see-through submesh(es) left out" : "");
                            }
                            catch (BrushException e)
                            {
                                item["collision"] = "none: " + e.Message;
                            }
                        }
                    }
                    composites.Add(item);
                }
            }
            EditHistory.CompositesChanged(level.Commands, added);
            Singleton.Editor?.CompositeBrowser?.RefreshList();
            made["models"] = composites;
            made["collision"] = new JObject() { ["parts_with_collision"] = collided, ["triangles"] = collisionTriangles, ["see_through_submeshes_left_out"] = skippedSeeThrough, ["mode"] = plan.Options.Collision };
            JArray materials = new JArray(plan.Materials.Values.OrderBy(o => o.SourceIndex).Select(m => DescribeMaterial(m, m.Made)));
            made["materials"] = materials;
        }

        /// <summary>A physics model that could not be made a prop: static after all, with the scene's collision so it is not walked through (UI thread).</summary>
        private static void StaticInstead(ScenePlan plan, Level level, SceneModel model, string stepLabel, List<string> notes)
        {
            if (plan.Options.Collision == "none" || model.Composite == null) return;
            using (UndoStack.Current.BeginGroup(stepLabel))
                foreach (FunctionEntity part in model.Composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.ModelReference && CollisionPairs.RowOf(o) == null).ToList())
                {
                    try { CollisionMatch.Apply(level, model.Composite, part, new CollisionMatch.Options() { Mode = plan.Options.Collision }, stepLabel); }
                    catch (BrushException e) { notes.Add(model.Name + ": no collision either (" + e.Message + ")."); }
                }
        }

        /// <summary>Step 3 (from the tool's thread): the group composites holding the instances, placed in the root at the scene's origin, and the lights.</summary>
        private static void PlaceEverything(McpCall call, ScenePlan plan, string label, string prefix, List<string> notes, JObject made)
        {
            SceneOptions options = plan.Options;
            Composite root = McpEditor.UI(() => McpEditor.RequireCommands().EntryPoints[0]);
            JArray groups = new JArray();
            JArray lights = new JArray();
            JObject offset = new JObject() { ["position"] = McpValues.Vector(options.Position), ["rotation"] = McpValues.Vector(options.Rotation) };
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(label, root, edit =>
            {
                foreach (string group in plan.Groups)
                {
                    List<ScenePlacement> placements = plan.Placements.Where(o => o.Group == group && o.Model.Composite != null && edit.Commands.Entries.Contains(o.Model.Composite)).ToList();
                    if (placements.Count == 0) continue;
                    Composite composite = edit.AddComposite(plan.GroupComposite(group));
                    foreach (ScenePlacement placement in placements)
                    {
                        FunctionEntity instance = edit.AddInstance(composite, placement.Model.Composite, placement.Name);
                        edit.SetParameter(composite, instance, "position", new JObject() { ["position"] = McpValues.Vector(placement.Position), ["rotation"] = McpValues.Vector(placement.Rotation) });
                    }
                    FunctionEntity placed = edit.AddInstance(root, composite, ScenePlan.EntityName(options.Name + "_" + group, "Scene"));
                    edit.SetParameter(root, placed, "position", offset);
                    groups.Add(new JObject() { ["composite"] = composite.name, ["instances"] = placements.Count, ["in_root_as"] = edit.Commands.Utils.GetEntityName(root, placed) });
                }

                List<SceneLight> placedLights = plan.Lights.Where(o => o.Type != "directional").ToList();
                if (placedLights.Count != 0)
                {
                    Composite composite = edit.AddComposite(plan.LightsComposite);
                    foreach (SceneLight light in placedLights)
                    {
                        FunctionEntity entity = edit.AddFunction(composite, FunctionType.LightReference, light.Name);
                        Vector3 rotation = light.Type == "spot" ? InstanceTransform.LookRotation(light.Direction) : Vector3.Zero;
                        void Put(string parameter, JToken value) => edit.SetParameter(composite, entity, parameter, value, allowCustom: true);
                        Put("position", new JObject() { ["position"] = McpValues.Vector(light.Position), ["rotation"] = McpValues.Vector(rotation) });
                        Put("type", light.Type == "spot" ? "SPOT" : "OMNI");
                        Put("colour", McpValues.Vector(light.Colour));
                        Put("intensity_multiplier", light.Intensity);
                        Put("end_attenuation", light.Range);
                        Put("start_attenuation", Math.Min(0.5, light.Range * 0.5));
                        Put("light_on_reset", true);
                        Put("is_specular", true);
                        Put("radiosity_multiplier", 1.0);
                        if (light.Type == "spot")
                        {
                            Put("inner_cone_angle", light.Inner);
                            Put("outer_cone_angle", light.Outer);
                        }
                        Put("cast_shadow", light.Shadow);
                        lights.Add(light.Name);
                    }
                    FunctionEntity placed = edit.AddInstance(root, composite, ScenePlan.EntityName(options.Name + "_Lights", "Lights"));
                    edit.SetParameter(root, placed, "position", offset);
                }
            }, new McpScriptEdit.RunOptions() { LabelPrefix = prefix, Show = false });
            foreach (string note in outcome.Notes) notes.Add(note);
            made["groups"] = groups;
            made["instances"] = plan.Placements.Count(o => o.Model.Composite != null);
            if (lights.Count != 0) made["lights"] = new JObject() { ["composite"] = plan.LightsComposite, ["count"] = lights.Count, ["names"] = lights };
        }
        #endregion
    }
}
