using AlienPAK;
using Assimp;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.ShaderTypes;
using CathodeLib;
using CathodeLib.Ubershaders;
using Newtonsoft.Json.Linq;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace OpenCAGE.MCP
{
    /// <summary>Bringing things in from files: models, composite packages; and the game's configuration files.</summary>
    internal static class McpImportTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            JObject generateOptions = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["family"] = new JObject() { ["type"] = "string", ["description"] = "Shader family, e.g. CA_ENVIRONMENT or CA_CHARACTER." },
                    ["name"] = new JObject() { ["type"] = "string", ["description"] = "The generated material's name." },
                    ["always_new"] = new JObject() { ["type"] = "boolean", ["description"] = "Build a new material even if an identical one is already in the level." },
                },
            };
            yield return new McpTool()
            {
                Name = "import_model",
                Title = "Import model",
                Description = "Import a 3D model file (FBX, GLB/glTF, OBJ, DAE) into the open level as the Model Editor's Import does: materials generated from the file's textures or chosen per mesh, meshes left out, and a composite placing it (a DisplayModel for a mesh skinned to a game skeleton). place_in (+ position, rotation) also places an instance of that composite, and collision gives its parts collision made from their own triangles - so walkable or solid geometry is one call; those two are one undo step, the import itself is not undoable. An unrigged prop is animated with a CAGEAnimation (animate_parameters on its instance); one rigidly skinned to a retail environment rig comes in animatable with skeleton:<rig>. preview=true only reports the plan. Written by save_level (with build=true for placed geometry).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "The model file's full path.", required: true),
                    McpSchema.Boolean("preview", "Report the meshes, source materials, generated-material plans and rig without importing anything."),
                    McpSchema.String("name", "The model's name in the level (backslash folders allowed), default the file's name."),
                    McpSchema.Number("scale", "Resize it on the way in (default 1)."),
                    McpSchema.Strings("exclude", "Meshes of the file to leave out (names or indexes, from preview)."),
                    McpSchema.Map("materials", "Mesh name or the file's material name -> a level material to draw it with: {\"Body\": \"MYMOD\\\\METAL\"}."),
                    McpSchema.String("material", "Use this level material for every mesh not named in 'materials', instead of generating."),
                    McpSchema.Nested("generate", "The file's material name -> how to generate it: {\"Metal\": {\"family\": \"CA_ENVIRONMENT\", \"name\": \"...\", \"always_new\": true}}.", new JObject() { ["type"] = "object", ["additionalProperties"] = generateOptions }),
                    McpSchema.String("skeleton", "'auto' (default: a mesh skinned to a game skeleton becomes a DisplayModel on it), 'none', or a skeleton name."),
                    McpSchema.Boolean("create_composite", "Make a composite that places it (default true)."),
                    McpSchema.String("composite_name", "That composite's path (default beside the model's name; 'DisplayModel:<name>' for a skinned one)."),
                    McpSchema.String("place_in", "Also place an instance of the new composite in this composite of the open level ('root' for the level itself)."),
                    McpSchema.Position("position", "With place_in: where, in place_in's space (or the world with space 'world')."),
                    McpSchema.Rotation("rotation", "With place_in: its rotation."),
                    McpSchema.String("space", "With place_in: 'composite' (default: position/rotation are in place_in's own space; the root's is world) or 'world' (converted for place_in's placement). " + McpSchema.SpaceText, options: new[] { "composite", "world" }),
                    McpSchema.Integer("placement", "With space 'world' and a place_in placed more than once: which placement (0-based, get_placements order)."),
                    McpSchema.String("instance_name", "With place_in: the instance's name (default numbered after the composite)."),
                    McpSchema.String("collision", "Give the new composite's parts collision made from their own triangles (as set_collision proxy 'from_model'): none (default), solid (WORLD and BALLISTIC: walked on, blocks bullets), world (blocks movement only) or ballistic (bullets only).", options: new[] { "none", "solid", "world", "ballistic" })),
                Run = ImportModel,
            };

            yield return new McpTool()
            {
                Name = "export_composite_package",
                Title = "Export composite package",
                Description = "Save composites of the open level (with everything they place and use: models, materials, textures, collision, script pages) as an OpenCAGE .ocp package file, which can be imported into any level. Written straight away; refuses to replace an existing file unless overwrite is true.",
                InputSchema = McpSchema.Object(
                    McpSchema.Strings("composites", "The composites (paths or ids).", required: true),
                    McpSchema.String("path", "Where to write the .ocp file (full path).", required: true),
                    McpSchema.String("name", "The package's name."),
                    McpSchema.String("description", "A description of it."),
                    McpSchema.Boolean("overwrite", "Replace a file already at the path (default false).")),
                Destructive = true,
                Run = ExportPackage,
            };

            yield return new McpTool()
            {
                Name = "inspect_composite_package",
                Title = "Inspect composite package",
                Description = "Read an OpenCAGE .ocp package's header without importing it: its name, description, source level, export date, OpenCAGE version, platform, composites (and which were exported directly), file count, compatibility warnings, and which of its composites the open level already has.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "The .ocp file's full path.", required: true),
                    McpSchema.Limit(200, "composites"),
                    McpSchema.Offset("composites")),
                ReadOnly = true,
                Idempotent = true,
                Run = InspectPackage,
            };

            yield return new McpTool()
            {
                Name = "import_composite_package",
                Title = "Import composite package",
                Description = "Import an OpenCAGE .ocp package's composites (and everything they use) into the open level: not undoable, written by save_level. Or, with 'levels', into those levels on disk: each is backed up first (backup_first), then loaded, imported into and saved (built too with build=true), written straight away; this closes this install's game. Place imported composites with create_entities. dry_run lists the composites and which the level already has (inspect_composite_package reads a package without a level). (.ocp is composites to bring into a level; a mod for the mod manager is an .omp - mods export.)",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "The .ocp file's full path.", required: true),
                    McpSchema.Boolean("overwrite_composites", "Replace composites the level already has with the same id."),
                    McpSchema.Boolean("overwrite_assets", "Replace models/textures/materials the level already has with the same name."),
                    McpSchema.Strings("levels", "Import into these levels on disk instead of the open one (list_levels names them). Not the level that is open."),
                    McpSchema.Boolean("build", "With 'levels': Save & Build each one after importing (much slower). Default false."),
                    McpSchema.Boolean("backup_first", "With 'levels': back up each level before writing it (default true); the result lists the backup ids."),
                    McpSchema.Boolean("dry_run", "Report the composites it would bring and which the level (or each of 'levels') already has, writing nothing.")),
                Destructive = true,
                Run = ImportPackage,
            };

            yield return new McpTool()
            {
                Name = "list_config_files",
                Title = "List configuration files",
                Description = "The game's configuration files OpenCAGE's configuration editors edit (BML and XML under the install's DATA folder): weapons, ammo, characters' attributes and senses, alien behaviour, difficulty, inventory, graphics, inputs and more. These are shared by every level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Text the path contains (e.g. 'WEAPON', 'ALIENCONFIGS')."),
                    McpSchema.Limit(500, "files"),
                    McpSchema.Offset("files")),
                ReadOnly = true,
                Idempotent = true,
                Run = call =>
                {
                    string data = DataFolder();
                    string filter = call.Str("filter");
                    List<string> files = Directory.EnumerateFiles(data, "*.*", SearchOption.AllDirectories)
                        .Where(o => o.EndsWith(".BML", StringComparison.OrdinalIgnoreCase) || o.EndsWith(".XML", StringComparison.OrdinalIgnoreCase))
                        .Select(o => o.Substring(data.Length).TrimStart('\\', '/').Replace('\\', '/'))
                        .Where(o => !o.StartsWith("ENV/", StringComparison.OrdinalIgnoreCase) && !o.StartsWith("MODTOOLS/", StringComparison.OrdinalIgnoreCase))
                        .Where(o => filter == null || o.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderBy(o => o).ToList();
                    JObject result = new JObject() { ["count"] = files.Count };
                    McpPaging.Page(call, files, result, "files", o => o, 500);
                    return result;
                },
            };

            yield return new McpTool()
            {
                Name = "read_config",
                Title = "Read configuration file",
                Description = "Read a game configuration file (BML or XML, path relative to the install's DATA folder, from list_config_files) as XML, optionally only the nodes an XPath selects.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("file", "Path under DATA, e.g. 'WEAPON_INFO/AMMO/AMMOTYPES.BML'.", required: true),
                    McpSchema.String("xpath", "Only return the nodes this XPath selects.")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    string file = ConfigPath(call.Str("file", required: true));
                    //A default namespace (GBL_ITEM has one) is set aside, so plain-name XPaths match
                    XmlDocument document = McpGameDataTools.LoadForElements(file, out _, out string defaultNamespace);
                    if (defaultNamespace != null)
                        call.Note("Its root declares xmlns=\"" + defaultNamespace + "\": it is shown and searched without it, so plain XPaths work.");
                    string xpath = call.Str("xpath");
                    if (xpath == null)
                        return new JObject() { ["file"] = call.Str("file"), ["xml"] = document.OuterXml.Length > 60000 ? document.OuterXml.Substring(0, 60000) + "... (cut short: use xpath to read part of it)" : Pretty(document) };
                    XmlNodeList nodes = SelectNodes(document, xpath);
                    return new JObject()
                    {
                        ["file"] = call.Str("file"),
                        ["matches"] = nodes.Count,
                        ["nodes"] = new JArray(nodes.Cast<XmlNode>().Take(100).Select(o => o.OuterXml.Length > 4000 ? o.OuterXml.Substring(0, 4000) + "..." : o.OuterXml)),
                    };
                }),
            };

            yield return new McpTool()
            {
                Name = "write_config",
                Title = "Write configuration file",
                Description = "Change a game configuration file (BML or XML under DATA): set the text or an attribute of every node an XPath selects (a node holding child elements has no text to set: it is refused). Written straight away, affects every level, and cannot be undone from OpenCAGE (the result lists the values replaced). Configuration editors open on the file are closed first; dry_run reports without writing.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("file", "Path under DATA, e.g. 'WEAPON_INFO/AMMO/AMMOTYPES.BML'.", required: true),
                    McpSchema.String("xpath", "The node(s) to change.", required: true),
                    McpSchema.String("attribute", "Set this attribute (leave out to set the node's text)."),
                    McpSchema.String("value", "The new value (\"\" empties it).", required: true),
                    McpSchema.Integer("expect", "Refuse unless the XPath selects exactly this many nodes (a guard against changing more than meant)."),
                    McpSchema.Boolean("dry_run", "Report what would change, without writing."),
                    McpSchema.Boolean("close_game", "If the game is running, close it first (otherwise the write is refused while it runs).")),
                Destructive = true,
                Run = call => McpEditor.UI(() =>
                {
                    string relative = call.Str("file", required: true);
                    string file = ConfigPath(relative);
                    bool dryRun = call.Bool("dry_run");
                    XmlDocument document = McpGameDataTools.LoadForElements(file, out BML bml, out string defaultNamespace);
                    XmlNodeList nodes = SelectNodes(document, call.Str("xpath", required: true));
                    if (nodes.Count == 0)
                        throw new McpError("The XPath selects nothing in " + relative + ".");
                    if (call.Has("expect") && nodes.Count != call.Int("expect"))
                        throw new McpError("The XPath selects " + nodes.Count + " nodes, not " + call.Int("expect") + "; nothing was changed.");
                    string attribute = call.Str("attribute");
                    //Present but empty is a value: it sets the text or attribute to nothing
                    if (!call.Has("value"))
                        throw new McpError("'value' is required.");
                    string value = call.Str("value");
                    JArray before = new JArray();
                    foreach (XmlNode node in nodes)
                    {
                        if (attribute != null)
                        {
                            if (!(node is XmlElement element))
                                throw new McpError("An attribute can only be set on an element.");
                            before.Add(element.HasAttribute(attribute) ? element.GetAttribute(attribute) : null);
                            element.SetAttribute(attribute, value);
                        }
                        else
                        {
                            //Setting the text of a node that holds elements deletes them all, and its text alone could not put them back
                            XmlNode child = node.ChildNodes.Cast<XmlNode>().FirstOrDefault(o => o.NodeType == XmlNodeType.Element);
                            if (child != null)
                                throw new McpError("<" + node.Name + "> holds child elements (<" + child.Name + ">, ...), which setting its text would delete. Select the one to change, or give 'attribute'. Nothing was changed.");
                            before.Add(node.InnerText);
                            node.InnerText = value;
                        }
                    }
                    //An editor window holding its own copy would save it over this; a running game holds the file. Editors are
                    //matched by the path as resolved, so "./X" or "A/../X" finds the one open on X
                    string resolved = file.Substring(DataFolder().Length + 1).Replace('\\', '/');
                    if (!McpGameDataTools.BeginWrite(call, dryRun, new[] { resolved }))
                        return new JObject() { ["file"] = relative, ["dry_run"] = true, ["would_change"] = nodes.Count, ["current_values"] = before };
                    document = McpGameDataTools.ForWriting(document, defaultNamespace, bml != null);
                    Modding.ModServices.CaptureBeforeWrite(file);
                    if (bml != null)
                    {
                        //Content hands out a copy each time: the edited document has to be given back
                        bml.Content = document;
                        if (!bml.Save())
                            throw new McpError("Could not write " + relative + " (is the game running?).");
                    }
                    else
                        document.Save(file);
                    McpGameDataTools.NoteBehaviourTreeLevels(call, new[] { file });
                    return new JObject() { ["file"] = relative, ["changed"] = nodes.Count, ["previous_values"] = before };
                }),
            };

            yield return new McpTool()
            {
                Name = "list_ui_files",
                Title = "List UI files",
                Description = "The files inside the game's DATA/UI.PAK (the HUD, menus and fonts shared by every level), with their sizes, as OpenCAGE's UI Editor lists them. edit_ui_pak exports, replaces, adds or deletes them.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Text the path contains."),
                    McpSchema.Limit(300, "files"),
                    McpSchema.Offset("files")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListUiFiles,
            };

            yield return new McpTool()
            {
                Name = "edit_ui_pak",
                Title = "Edit UI.PAK",
                Description = "Export files out of DATA/UI.PAK, or replace, add ('write') or delete one, as the UI Editor does. write/delete change the game's shared UI.PAK straight away (every level; not undoable) and refuse while the game runs unless close_game; dry_run only reports.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "What to do.", required: true, options: new[] { "export", "write", "delete" }),
                    McpSchema.String("path", "The file's path inside UI.PAK (list_ui_files)."),
                    McpSchema.String("filter", "export: every file whose path contains this ('*' for all), into 'destination' as a folder."),
                    McpSchema.String("destination", "export: full path of the file (one path) or folder (filter) to write."),
                    McpSchema.String("source_file", "write: full path of the file whose bytes go in."),
                    McpSchema.Boolean("create", "write: add the file if UI.PAK has no such path (default false: only replace)."),
                    McpSchema.Boolean("overwrite", "export: replace files already at the destination (default false)."),
                    McpSchema.Boolean("dry_run", "write/delete: check and report what would change without writing (default false)."),
                    McpSchema.Boolean("close_game", "write/delete: close the running game first, as the UI Editor does (default false: refuse).")),
                Destructive = true,
                Run = EditUiPak,
            };
        }

        #region Models
        private static object ImportModel(McpCall call)
        {
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
            if (!File.Exists(path))
                throw new McpError("There is no file at " + path + ".");
            float scale = (float)call.Num("scale", 1.0);
            if (scale <= 0 || float.IsNaN(scale) || float.IsInfinity(scale))
                throw new McpError("'scale' has to be above 0.");
            bool preview = call.Bool("preview");
            string skeletonArg = (call.Str("skeleton") ?? "auto").Trim();

            Scene scene;
            try
            {
                using (McpEditorTools.Heartbeat(call, "Reading " + Path.GetFileName(path)))
                using (AssimpContext importer = new AssimpContext())
                    scene = importer.ImportFile(path, ModelIO.ImportPostProcessSteps);
            }
            catch (Exception e)
            {
                throw new McpError(Path.GetFileName(path) + " could not be read: " + e.Message);
            }
            if (scene == null || scene.MeshCount == 0)
                throw new McpError("No meshes were found in " + Path.GetFileName(path) + " (they need to be under the scene root).");
            ModelIO.ModelMetadata metadata = ModelIO.TryLoadSidecar(path);
            ModelIO.ImportPlan plan = ModelIO.CreateImportPlan(scene, metadata, Path.GetFileNameWithoutExtension(path));
            plan.Scale = scale;
            //A file with no sidecar carries its own unit: glTF is in metres, FBX usually centimetres
            if (!plan.HasMetadata)
                plan.UnitScale = ModelIO.FormatUnitScale(path);
            List<ModelIO.PlannedSubmesh> planned = plan.AllSubmeshes().ToList();

            //Meshes left out, as unticking them in the import preview does
            foreach (string wanted in call.StrList("exclude"))
            {
                List<ModelIO.PlannedSubmesh> matched = MeshesNamed(scene, planned, wanted);
                if (matched.Count == 0)
                    throw new McpError("The file has no mesh '" + wanted + "'. It has: " + string.Join(", ", planned.Take(40).Select(o => o.MeshIndex + ": " + o.MeshName)) + ".");
                foreach (ModelIO.PlannedSubmesh submesh in matched) submesh.Include = false;
            }
            if (!planned.Any(o => o.Include))
                throw new McpError("Every mesh is excluded; leave at least one in.");
            string modelName = ImportedModelName(call, plan);

            //Placing and collision, checked before anything is imported
            string collision = (call.Str("collision") ?? "none").Trim().ToLowerInvariant();
            if (collision != "none" && collision != "solid" && collision != "world" && collision != "ballistic")
                throw McpError.Invalid("'collision' takes none, solid, world or ballistic.");
            bool place = call.Has("place_in");
            if (!place && (call.Has("position") || call.Has("rotation") || call.Has("space") || call.Has("placement") || call.Has("instance_name")))
                throw McpError.Invalid("position, rotation, space, placement and instance_name place the new composite: give place_in (e.g. 'root') with them.");
            if ((place || collision != "none") && !call.Bool("create_composite", true))
                throw McpError.Invalid("place_in and collision work on the composite the import makes: leave create_composite on.");
            string space = (call.Str("space") ?? "composite").Trim().ToLowerInvariant();
            if (space != "composite" && space != "world")
                throw McpError.Invalid("'space' takes composite or world.");
            if (place)
                McpEditor.UI(() => McpScript.FindComposite(McpEditor.RequireCommands(forEditing: false), call.Str("place_in")));

            string sourceName = Path.GetFileNameWithoutExtension(path);
            JObject imported;
            using (McpEditorTools.Heartbeat(call, (preview ? "Planning the import of " : "Importing ") + Path.GetFileName(path)))
            {
                imported = McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: !preview).Level;
                    List<string> notes = new List<string>();
                    string placement = ModelIO.PlacementWarning(scene, plan, path);
                    if (placement != null) notes.Add(placement);

                    //Per-mesh choices first: a mesh named in 'materials', then its file material named there, then 'material' for everything
                    Dictionary<ModelIO.PlannedSubmesh, Materials.Material> picked = new Dictionary<ModelIO.PlannedSubmesh, Materials.Material>();
                    JObject materialsMap = call.Object("materials");
                    if (materialsMap != null)
                    {
                        foreach (JProperty entry in materialsMap.Properties())
                        {
                            Materials.Material material = McpAssets.FindMaterial(level, McpValues.ReadString(entry.Value), "materials." + entry.Name);
                            List<ModelIO.PlannedSubmesh> byMesh = MeshesNamed(scene, planned, entry.Name);
                            List<ModelIO.PlannedSubmesh> bySource = planned.Where(o => string.Equals(SourceMaterialName(scene, o), entry.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                            if (byMesh.Count == 0 && bySource.Count == 0)
                                throw new McpError("'" + entry.Name + "' in materials is neither a mesh nor a material of the file (preview lists both).");
                            foreach (ModelIO.PlannedSubmesh submesh in bySource) if (!picked.ContainsKey(submesh)) picked[submesh] = material;
                            foreach (ModelIO.PlannedSubmesh submesh in byMesh) picked[submesh] = material;
                        }
                    }
                    Materials.Material forAll = call.Has("material") ? McpAssets.FindMaterial(level, call.Str("material")) : null;
                    foreach (ModelIO.PlannedSubmesh submesh in planned)
                    {
                        if (picked.ContainsKey(submesh)) continue;
                        if (forAll != null) { picked[submesh] = forAll; continue; }
                        //An export knows which material each submesh used: take it again if this level still has it
                        string named = submesh.Metadata?.Material;
                        Materials.Material existing = string.IsNullOrEmpty(named) ? null : level.Materials.Entries.FirstOrDefault(o => o != null && o.Name == named);
                        if (existing != null) picked[submesh] = existing;
                    }

                    //Everything else gets a material generated from the file's own material, one per source material
                    JObject generate = call.Object("generate");
                    Dictionary<int, JObject> overrides = new Dictionary<int, JObject>();
                    if (generate != null)
                    {
                        foreach (JProperty entry in generate.Properties())
                        {
                            int index = Enumerable.Range(0, scene.MaterialCount).FirstOrDefault(i => string.Equals(scene.Materials[i].Name, entry.Name, StringComparison.OrdinalIgnoreCase)) ;
                            if (index >= scene.MaterialCount || !string.Equals(scene.Materials[index].Name, entry.Name, StringComparison.OrdinalIgnoreCase))
                                throw new McpError("'" + entry.Name + "' in generate is not a material of the file. It has: " + string.Join(", ", scene.Materials.Select(o => "'" + o.Name + "'")) + ".");
                            if (!(entry.Value is JObject options))
                                throw new McpError("generate." + entry.Name + " has to be an object: {\"family\": ..., \"name\": ..., \"always_new\": ...}.");
                            foreach (JProperty option in options.Properties())
                                if (option.Name != "family" && option.Name != "name" && option.Name != "always_new")
                                    throw new McpError("generate." + entry.Name + "." + option.Name + " is not an option (family, name, always_new).");
                            overrides[index] = options;
                        }
                    }
                    List<SHADER_LIST> creatable = null;
                    Dictionary<int, MaterialGenerator.Plan> plans = new Dictionary<int, MaterialGenerator.Plan>();
                    foreach (ModelIO.PlannedSubmesh submesh in planned)
                    {
                        if (!submesh.Include || picked.ContainsKey(submesh)) continue;
                        int index = SourceMaterialIndex(scene, submesh);
                        if (index < 0 || plans.ContainsKey(index)) continue;
                        creatable = creatable ?? ShaderPermutationService.CreatableFamilies(level.Shaders, Singleton.PathToAI).Select(o => o.Family).ToList();
                        Assimp.Material source = scene.Materials[index];
                        Mesh mesh = scene.Meshes[submesh.MeshIndex];
                        SHADER_LIST family = MaterialGenerator.SuggestFamily(mesh, creatable);
                        string materialName = string.IsNullOrWhiteSpace(source.Name) || source.Name == "DefaultMaterial" ? sourceName : AssetName.Sanitise(sourceName + "\\" + source.Name);
                        bool alwaysNew = false;
                        if (overrides.TryGetValue(index, out JObject options))
                        {
                            if (options["family"] != null)
                            {
                                string familyText = ((string)options["family"] ?? "").Trim().ToUpperInvariant();
                                if (!familyText.StartsWith("CA_")) familyText = "CA_" + familyText;
                                if (!Enum.TryParse(familyText, out family) || !creatable.Contains(family))
                                    throw new McpError("generate." + source.Name + ".family has to be a family this level can build: " + string.Join(", ", creatable) + ".");
                            }
                            if (options["name"] != null)
                            {
                                materialName = AssetName.Normalise(((string)options["name"] ?? "").Trim());
                                string problem = AssetName.Problem(materialName);
                                if (problem != null) throw new McpError("generate." + source.Name + ".name: " + problem);
                            }
                            if (options["always_new"] != null) alwaysNew = McpValues.ReadBool(options["always_new"], "always_new");
                        }
                        MaterialGenerator.Plan materialPlan = MaterialGenerator.Describe(scene, source, mesh, path, plan.HasMetadata, level, family, materialName, Singleton.PathToAI);
                        materialPlan.AlwaysCreateNew = alwaysNew;
                        plans[index] = materialPlan;
                    }
                    foreach (int index in overrides.Keys.Where(o => !plans.ContainsKey(o)))
                        notes.Add("'" + scene.Materials[index].Name + "' in generate was not used: every mesh drawing with it has a material picked (or is excluded).");

                    //The rig: a mesh on one of the game's skeletons becomes a DisplayModel on it
                    ModelImportRig.Situation rig = ModelImportRig.Examine(scene, plan, Singleton.Animations);
                    Skeleton skeleton = null;
                    if (string.Equals(skeletonArg, "auto", StringComparison.OrdinalIgnoreCase))
                    {
                        if (rig.FitsAGameRig) skeleton = rig.BestFit?.Skeleton;
                    }
                    else if (!string.Equals(skeletonArg, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!rig.Skinned)
                            throw new McpError("The mesh is not skinned, so it cannot be a DisplayModel on a skeleton. Leave 'skeleton' out.");
                        if (!rig.OurOwnExport)
                            throw new McpError("The mesh's bones do not carry the game's bone indices (it was not bound to a game skeleton), so its skinning cannot be kept. Bind it to one of the game's skeletons first (export_model writes one to bind to).");
                        skeleton = Singleton.Global?.GetSkeleton(skeletonArg);
                        if (skeleton == null)
                            throw McpError.NotFound("skeleton", skeletonArg, Singleton.Animations?.Skeletons?.Select(o => o.ToString()), Singleton.AnimationsLoaded ? "list_skeletons lists them." : "(The animation data is not loaded yet.)");
                        float fit = ModelImportRig.ScoreFit(scene, skeleton, plan.Scale, plan.UnitScale);
                        if (fit < 0 || fit > ModelImportRig.FitThreshold)
                            notes.Add("The mesh does not sit on " + skeleton.Name + "'s bones (" + (fit < 0 ? "no match" : fit.ToString("0.00") + " m off") + "); it may deform wrongly.");
                    }
                    if (rig.Skinned && !rig.FitsAGameRig && skeleton == null)
                        notes.Add("This is skinned to a skeleton the game doesn't have, so it is imported unskinned: the mesh comes in, the bone weights don't.");
                    if (skeleton != null && (place || collision != "none"))
                        throw McpError.Invalid("This becomes a DisplayModel on " + skeleton.Name + ", which a Character wears (its display_model) rather than being placed or colliding: leave out place_in and collision, or pass skeleton: 'none' to import it as a static model.");

                    if (preview)
                        return DescribeImportPlan(level, scene, plan, planned, picked, plans, rig, skeleton, modelName, call, notes);

                    //Generation happens only now: it adds textures to the level and entries to the shader pool
                    Dictionary<int, Materials.Material> generated = new Dictionary<int, Materials.Material>();
                    foreach (KeyValuePair<int, MaterialGenerator.Plan> entry in plans)
                    {
                        string error = entry.Value.Error;
                        Materials.Material made = null;
                        if (entry.Value.CanGenerate)
                        {
                            try { made = MaterialGenerator.Generate(entry.Value, scene.Materials[entry.Key], level, Singleton.PathToAI, out error); }
                            catch (Exception e) { made = null; error = e.Message; }
                        }
                        if (made == null)
                            notes.Add("Material '" + entry.Value.Name + "' could not be generated (" + (error ?? "no reason given") + "); those meshes use the fallback material.");
                        generated[entry.Key] = made;
                    }
                    foreach (ModelIO.PlannedSubmesh submesh in planned)
                    {
                        if (picked.TryGetValue(submesh, out Materials.Material chosen)) { submesh.Material = chosen; continue; }
                        int index = SourceMaterialIndex(scene, submesh);
                        if (index >= 0 && generated.TryGetValue(index, out Materials.Material made) && made != null) submesh.Material = made;
                    }
                    if (generated.Values.Any(o => o != null))
                        Singleton.OnResourceModified?.Invoke();

                    Models.CS2 model = ModelIO.BuildCS2(scene, plan, wanted => level.Materials.Entries.FirstOrDefault(o => o.Name == wanted), Singleton.FallbackMaterial, out List<string> warnings);
                    //The rig note has already said a foreign rig loses its skinning; one per mesh says nothing more
                    if (rig.Skinned && !rig.FitsAGameRig && skeleton == null)
                        warnings.RemoveAll(ModelIO.IsDroppedSkinning);
                    if (model.Components.Count == 0)
                        throw new McpError("None of the meshes could be converted." + (warnings.Count != 0 ? " " + string.Join(" ", warnings.Take(10)) : ""));
                    notes.AddRange(warnings.Take(15));

                    model.Name = UniqueModelName(level, modelName);
                    if (model.Name != modelName) notes.Add("The level already had a model called " + modelName + ", so this one is " + model.Name + ".");
                    level.Models.Entries.Add(model);

                    JObject result = new JObject()
                    {
                        ["model"] = model.Name,
                        ["parts"] = model.Components.Count,
                        ["materials_generated"] = new JArray(generated.Values.Where(o => o != null).Select(o => o.Name).Distinct()),
                    };
                    if (picked.Count != 0)
                        result["materials_picked"] = new JArray(picked.Where(o => o.Key.Include).Select(o => level.Materials.GetMaterialName(o.Value)).Distinct());

                    if (call.Bool("create_composite", true))
                    {
                        bool displayModel = skeleton != null;
                        string compositeName = call.Has("composite_name") ? ModelCompositeBuilder.Normalise(call.Str("composite_name")) : ModelCompositeBuilder.DefaultName(model.Name, displayModel);
                        string problem = ModelCompositeBuilder.Problem(compositeName, displayModel, level.Commands);
                        if (problem != null)
                            notes.Add("No composite was made: " + problem);
                        else
                        {
                            Singleton.OnCompositeAddPending?.Invoke();
                            ModelCompositeBuilder.Result made = ModelCompositeBuilder.Create(level, model, compositeName, skeleton, skeleton?.Name);
                            Singleton.OnCompositeAdded?.Invoke(made.Composite);
                            Singleton.Editor.CompositeBrowser?.RefreshList();
                            result["composite"] = made.Composite.name;
                            if (displayModel) result["display_model_skeleton"] = skeleton.Name;
                            notes.AddRange(made.Notes);
                        }
                    }
                    else if (skeleton != null)
                        notes.Add("No DisplayModel composite was made (create_composite is false), so nothing binds the mesh to " + skeleton.Name + ".");

                    AddAnimationHint(result, rig, skeleton, path, notes);
                    Singleton.OnResourceModified?.Invoke();
                    foreach (string note in notes) call.Note(note);
                    return result;
                });
            }
            if (preview || (!place && collision == "none"))
                return imported;
            if (imported["composite"] == null)
            {
                call.Note("No composite was made, so nothing was placed or given collision.");
                return imported;
            }
            try { FinishImport(call, imported, place, collision, space); }
            catch (McpError e)
            {
                throw new McpError(e.Code, "The model was imported (" + imported["model"] + ", composite " + imported["composite"] + "; that cannot be undone), but " + (place ? "placing it" : "giving it collision") + " failed: " + e.Message);
            }
            return imported;
        }

        /// <summary>
        /// import_model's place_in and collision: each part's collision from its own triangles (set_collision proxy 'from_model'),
        /// and an instance of the composite where asked - made one undo step together.
        /// </summary>
        private static void FinishImport(McpCall call, JObject imported, bool place, string collision, string space)
        {
            string compositeName = (string)imported["composite"];
            object mark = McpEditor.UI(() => UndoStack.Current.Mark());
            int steps = 0;
            if (collision != "none")
            {
                List<string> parts = McpEditor.UI(() =>
                {
                    Composite owner = McpScript.FindComposite(McpEditor.RequireCommands(), compositeName);
                    return owner.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.ModelReference).Select(o => McpScript.Id(o.shortGUID)).ToList();
                });
                JArray flags = collision == "world" ? new JArray("WORLD") : collision == "ballistic" ? new JArray("BALLISTIC") : new JArray("WORLD", "BALLISTIC");
                JArray proxies = new JArray();
                int triangles = 0;
                foreach (string part in parts)
                {
                    call.ThrowIfCancelled();
                    JObject done = McpEditorTools.Nested(call, "set_collision", new JObject() { ["composite"] = compositeName, ["entity"] = part, ["proxy"] = "from_model", ["flags"] = flags });
                    steps++;
                    if (done["imported_proxy"] is JObject proxy)
                    {
                        proxies.Add(proxy["proxy"]);
                        triangles += (int?)proxy["triangles"] ?? 0;
                    }
                }
                imported["collision"] = new JObject() { ["parts"] = parts.Count, ["proxies"] = proxies, ["triangles"] = triangles, ["flags"] = flags };
            }
            if (place)
            {
                Composite into = null, made = null;
                cTransform local = null;
                JObject frame = null;
                McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands();
                    into = McpScript.FindComposite(commands, call.Str("place_in"));
                    made = McpScript.FindComposite(commands, compositeName);
                    System.Numerics.Vector3 position = call.Has("position") ? McpValues.ReadVector(call.Token("position"), "position", null) : System.Numerics.Vector3.Zero;
                    System.Numerics.Vector3 rotation = call.Has("rotation") ? McpValues.ReadVector(call.Token("rotation"), "rotation", null) : System.Numerics.Vector3.Zero;
                    local = new cTransform(position, rotation);
                    if (space == "world")
                        local = InstanceTransform.ToLocal(McpSpatialTools.FrameOf(commands, into, call.Has("placement") ? call.Int("placement") : (int?)null, "'placement'", out frame), local);
                });
                FunctionEntity instance = null;
                McpScriptEdit.Run("Place " + made.name, into, edit =>
                {
                    instance = edit.AddInstance(into, made, call.Str("instance_name"));
                    edit.SetParameter(into, instance, "position", new JObject() { ["position"] = McpValues.Vector(local.position), ["rotation"] = McpValues.Vector(local.rotation) });
                });
                steps++;
                JObject placed = new JObject()
                {
                    ["into"] = into.name,
                    ["instance"] = McpScript.Id(instance.shortGUID),
                    ["name"] = McpEditor.UI(() => McpScript.EntityName(McpEditor.RequireCommands(false), into, instance)),
                    ["position"] = McpValues.Vector(local.position),
                    ["rotation"] = McpValues.Vector(local.rotation),
                    ["space"] = McpEditor.UI(() => into == McpEditor.RequireCommands(false).EntryPoints[0]) ? "world" : "composite",
                };
                if (frame?["placement"] != null) placed["into_placement"] = frame["placement"];
                imported["placed"] = placed;
            }
            //Collision and placement come back as one step
            if (steps > 1)
                McpEditor.UI(() =>
                {
                    int since = UndoStack.Current.StepsSince(mark);
                    if (since > 1) UndoStack.Current.Collapse(since, "AI: " + (place ? "Place " : "Collide ") + compositeName + (place && collision != "none" ? " with collision" : ""));
                });
            call.Note("save_level with build=true makes the placed model and its collision part of the game's level data.");
        }

        /// <summary>The file's clips are ANIMATION.PAK's business, which import_animation writes: say what is there and what to pass it.</summary>
        private static void AddAnimationHint(JObject result, ModelImportRig.Situation rig, Skeleton skeleton, string path, List<string> notes)
        {
            if (!rig.HasAnimations) return;
            result["animations"] = new JArray(rig.Animations.Select((o, i) => new JObject() { ["clip_index"] = i, ["name"] = o }));
            string rigName = skeleton?.Name ?? (rig.FitsAGameRig ? rig.BestFit?.Skeleton?.Name : null);
            notes.Add("The file carries " + rig.Animations.Count + " animation(s), which are not part of the model: import_animation (path '" + path + "', set" + (rigName != null ? ", rig '" + rigName + "'" : "") + ") brings them in" + (rig.Animations.Count > 1 ? " - clip_index: 'all' imports every clip in one write, each under its own name, or clip_index picks one" : "") + ".");
        }

        private static JObject DescribeImportPlan(Level level, Scene scene, ModelIO.ImportPlan plan, List<ModelIO.PlannedSubmesh> planned,
            Dictionary<ModelIO.PlannedSubmesh, Materials.Material> picked, Dictionary<int, MaterialGenerator.Plan> plans,
            ModelImportRig.Situation rig, Skeleton skeleton, string modelName, McpCall call, List<string> notes)
        {
            JArray meshes = new JArray();
            for (int c = 0; c < plan.Components.Count; c++)
                for (int l = 0; l < plan.Components[c].LODs.Count; l++)
                    for (int s = 0; s < plan.Components[c].LODs[l].Submeshes.Count; s++)
                    {
                        ModelIO.PlannedSubmesh submesh = plan.Components[c].LODs[l].Submeshes[s];
                        JObject item = new JObject()
                        {
                            ["index"] = submesh.MeshIndex,
                            ["name"] = submesh.MeshName,
                            ["component"] = c,
                            ["lod"] = l,
                            ["vertices"] = submesh.MeshIndex < scene.MeshCount ? scene.Meshes[submesh.MeshIndex].VertexCount : 0,
                            ["source_material"] = SourceMaterialName(scene, submesh),
                            ["included"] = submesh.Include,
                        };
                        int index = SourceMaterialIndex(scene, submesh);
                        if (picked.TryGetValue(submesh, out Materials.Material material)) item["material"] = level.Materials.GetMaterialName(material);
                        else if (index >= 0 && plans.TryGetValue(index, out MaterialGenerator.Plan generated)) item["material"] = generated.CanGenerate ? "generated: " + generated.Name : "fallback (cannot generate)";
                        else item["material"] = "fallback";
                        meshes.Add(item);
                    }

            JArray generation = new JArray(plans.Select(o =>
            {
                MaterialGenerator.Plan p = o.Value;
                JObject item = new JObject()
                {
                    ["source_material"] = scene.Materials[o.Key].Name,
                    ["name"] = p.Name,
                    ["family"] = p.Family.ToString(),
                };
                if (p.Error != null) item["error"] = p.Error;
                else
                {
                    item["mask"] = "0x" + p.Mask.ToString("X");
                    item["exact_mask"] = p.ExactMask;
                    item["features"] = new JArray(p.Features);
                    item["textures"] = new JArray(p.Textures.Select(t => new JObject()
                    {
                        ["sampler"] = t.Role.Sampler,
                        ["source"] = t.SourceLabel,
                        ["use"] = t.Existing != null ? "reuse " + t.Existing.Name : t.Usable ? "import as " + t.TextureName : "skipped: " + t.Problem,
                    }));
                    Materials.Material shared = MaterialGenerator.WouldReuse(p, level);
                    if (shared != null) item["reuses_existing"] = shared.Name;
                    if (p.Notes.Count != 0) item["notes"] = new JArray(p.Notes);
                }
                return item;
            }));

            JObject result = new JObject()
            {
                ["preview"] = true,
                ["model"] = UniqueModelName(level, modelName),
                ["has_sidecar"] = plan.HasMetadata,
                ["structure"] = plan.Components.Count + " component(s), " + plan.Components.Sum(o => o.LODs.Count) + " LOD(s), " + planned.Count + " mesh(es)",
                ["meshes"] = meshes,
                ["file_materials"] = new JArray(scene.Materials.Select(o => o.Name)),
                ["generated_materials"] = generation,
                ["rig"] = new JObject()
                {
                    ["skinned"] = rig.Skinned,
                    ["summary"] = ModelImportRig.Describe(rig),
                    ["display_model_skeleton"] = skeleton?.Name,
                },
            };
            if (call.Bool("create_composite", true))
                result["composite"] = call.Has("composite_name") ? ModelCompositeBuilder.Normalise(call.Str("composite_name")) : ModelCompositeBuilder.DefaultName((string)result["model"], skeleton != null);
            AddAnimationHint(result, rig, skeleton, call.Str("path"), notes);
            foreach (string note in notes) call.Note(note);
            return result;
        }

        /// <summary>The meshes of the file a name (or index) picks out.</summary>
        private static List<ModelIO.PlannedSubmesh> MeshesNamed(Scene scene, List<ModelIO.PlannedSubmesh> planned, string wanted)
        {
            string name = (wanted ?? "").Trim();
            List<ModelIO.PlannedSubmesh> matched = planned.Where(o => string.Equals(o.MeshName, name, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Tag, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matched.Count == 0 && int.TryParse(name, out int index))
                matched = planned.Where(o => o.MeshIndex == index).ToList();
            return matched;
        }

        /// <summary>Which of the file's own materials a mesh draws with - what a generated material is built from.</summary>
        private static int SourceMaterialIndex(Scene scene, ModelIO.PlannedSubmesh submesh)
        {
            if (submesh == null || submesh.MeshIndex < 0 || submesh.MeshIndex >= scene.MeshCount) return -1;
            int index = scene.Meshes[submesh.MeshIndex].MaterialIndex;
            return index < 0 || index >= scene.MaterialCount ? -1 : index;
        }

        private static string SourceMaterialName(Scene scene, ModelIO.PlannedSubmesh submesh)
        {
            int index = SourceMaterialIndex(scene, submesh);
            return index < 0 ? null : scene.Materials[index].Name;
        }

        /// <summary>
        /// The name an imported model is stored under, tidied and checked as the import window's name field does
        /// (AssetName): 'name' (blank means not given), else the file's; with .CS2 on the end.
        /// </summary>
        private static string ImportedModelName(McpCall call, ModelIO.ImportPlan plan)
        {
            string given = call.Str("name");
            bool named = !string.IsNullOrWhiteSpace(given);
            string name = AssetName.Normalise(named ? given : plan.Name);
            if (name.Length != 0 && !name.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase)) name += ".CS2";
            string problem = AssetName.Problem(name);
            if (problem != null)
                throw new McpError((named ? "'name'" : "The file's model name '" + plan.Name + "'") + " cannot be used: " + problem + (named ? "" : " Give 'name'."));
            return name;
        }

        private static string UniqueModelName(Level level, string name)
        {
            if (!level.Models.Entries.Any(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)))
                return name;
            string extension = Path.GetExtension(name);
            string stem = name.Substring(0, name.Length - extension.Length);
            for (int i = 1; ; i++)
            {
                string candidate = stem + "_" + i + extension;
                if (!level.Models.Entries.Any(o => string.Equals(o.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                    return candidate;
            }
        }
        #endregion

        #region Packages
        private static object ExportPackage(McpCall call)
        {
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
            if (!path.EndsWith(PackageFiles.CompositeExtension, StringComparison.OrdinalIgnoreCase))
                path += PackageFiles.CompositeExtension;
            //The editor's save dialog would have asked before replacing a package; so does this
            if (File.Exists(path) && !call.Bool("overwrite"))
                throw new McpError("There is already a package at " + path + ". Pass overwrite: true to replace it, or pick another path.");
            using (McpEditorTools.Heartbeat(call, "Exporting a composite package"))
            {
                return McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel();
                    List<Composite> composites = call.StrList("composites", required: true).Select(o => McpScript.FindComposite(content.Level.Commands, o)).Distinct().ToList();
                    //The package takes the composites as their links stand: the live pages of the one on screen go in first
                    McpBrowseTools.CompileIfShown(Singleton.Editor.CompositeDisplay?.Composite);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    CompositeArchive.Manifest manifest = CompositeArchive.Export(content.Level, composites, path, new CompositeArchive.PackageInfo() { Name = call.Str("name"), Description = call.Str("description") });
                    GC.Collect();
                    return new JObject()
                    {
                        ["file"] = path,
                        ["composites"] = manifest.Composites.Count,
                        ["roots"] = new JArray(manifest.Composites.Where(o => o.IsRoot).Select(o => o.Name)),
                        ["bytes"] = new FileInfo(path).Length,
                    };
                });
            }
        }

        private static CompositeArchive.Manifest ReadPackage(string path)
        {
            if (!File.Exists(path))
                throw new McpError("There is no file at " + path + ".");
            try { return CompositeArchive.ReadHeader(path); }
            catch (Exception e) { throw new McpError(Path.GetFileName(path) + " is not a package OpenCAGE can read: " + e.Message); }
        }

        private static object InspectPackage(McpCall call)
        {
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
            CompositeArchive.Manifest manifest = ReadPackage(path);

            //The warnings the import window puts in its header
            List<string> warnings = new List<string>();
            PatchManager.Platform from = manifest.ParsedPlatform;
            PatchManager.Platform here = Singleton.Platform;
            if (from != here)
            {
                if (IsPcStorefront(from) && IsPcStorefront(here))
                    warnings.Add("Exported from a " + from + " install (this is " + here + ").");
                else
                    warnings.Add("Exported from a " + from + " install, which does not share level files with this " + here + " install - the import may not work.");
            }
            if (!string.IsNullOrEmpty(manifest.OpenCAGEVersion) && manifest.OpenCAGEVersion != Singleton.Version)
                warnings.Add("Exported by OpenCAGE " + manifest.OpenCAGEVersion + " (this is " + Singleton.Version + ").");

            HashSet<uint> here_ = McpEditor.UI(() =>
            {
                Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
                return commands == null ? null : new HashSet<uint>(commands.Entries.Where(o => o != null).Select(o => o.shortGUID.AsUInt32));
            });
            List<CompositeArchive.CompositeEntry> composites = manifest.Composites.Where(o => o != null).ToList();
            JObject result = new JObject()
            {
                ["file"] = path,
                ["package"] = manifest.DisplayName(path),
                ["description"] = string.IsNullOrWhiteSpace(manifest.Description) ? null : manifest.Description.Trim(),
                ["source_level"] = string.IsNullOrEmpty(manifest.SourceLevel) ? null : manifest.SourceLevel,
                ["exported_utc"] = manifest.ExportedUtc == default(DateTime) ? null : manifest.ExportedUtc.ToString("u"),
                ["opencage_version"] = string.IsNullOrEmpty(manifest.OpenCAGEVersion) ? null : manifest.OpenCAGEVersion + (string.IsNullOrEmpty(manifest.OpenCAGEBeta) ? "" : " (" + manifest.OpenCAGEBeta + ")"),
                ["platform"] = string.IsNullOrEmpty(manifest.Platform) ? null : manifest.Platform,
                ["format_version"] = manifest.FormatVersion,
                ["composite_count"] = composites.Count,
                ["roots"] = new JArray(composites.Where(o => o.IsRoot).Select(o => o.Name)),
            };
            McpPaging.Page(call, composites, result, "composites", o =>
            {
                JObject item = new JObject() { ["name"] = o.Name, ["id"] = McpScript.Id(new ShortGuid(o.Guid)) };
                if (o.IsRoot) item["root"] = true;
                if (here_ != null && here_.Contains(o.Guid)) item["already_in_open_level"] = true;
                return item;
            }, 200);
            result["files"] = manifest.Files.Count(o => o != null);
            result["bytes"] = manifest.Files.Where(o => o != null).Sum(o => o.Length);
            result["warnings"] = new JArray(warnings);
            if (here_ != null)
            {
                int already = composites.Count(o => here_.Contains(o.Guid));
                if (already != 0) call.Note(already + " of its composites are already in the open level: importing skips them unless overwrite_composites is true.");
            }
            return result;
        }

        private static bool IsPcStorefront(PatchManager.Platform platform)
        {
            switch (platform)
            {
                case PatchManager.Platform.STEAM:
                case PatchManager.Platform.EPIC_GAMES_STORE:
                case PatchManager.Platform.GOG:
                case PatchManager.Platform.WINDOWS_STORE:
                    return true;
                default:
                    return false;
            }
        }

        private static object ImportPackage(McpCall call)
        {
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
            CompositeArchive.Manifest manifest = ReadPackage(path);
            //What the import window imports: the composites the author ticked, which bring what they instance with them
            List<uint> ids = manifest.Composites.Where(o => o != null && o.IsRoot).Select(o => o.Guid).ToList();
            if (ids.Count == 0) ids = manifest.Composites.Where(o => o != null).Select(o => o.Guid).ToList();
            if (ids.Count == 0)
                throw new McpError("The package holds no composites.");
            bool overwrite = call.Bool("overwrite_composites");
            CompositeArchive.ImportOptions options = new CompositeArchive.ImportOptions() { OverwriteComposites = overwrite, OverwriteAssets = call.Bool("overwrite_assets") };

            if (call.Bool("dry_run"))
                return ImportPlan(call, path, manifest, overwrite);
            if (call.Has("levels"))
                return ImportPackageOnDisk(call, path, manifest, ids, options);
            if (call.Has("build") || call.Has("backup_first"))
                throw McpError.Invalid("'build' and 'backup_first' only apply with 'levels'. For the open level, import and then save_level (with build=true for placed geometry).");

            using (McpEditorTools.Heartbeat(call, "Importing " + manifest.DisplayName(path)))
            {
                return McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel();
                    McpEditor.RequireUndoIdle();
                    Level destination = content.Level;
                    //Where the user is, to put them back if the import closes the view (stepped down as they were, the same selection)
                    CompositePath.Place shown = Singleton.Editor.CompositeDisplay?.CapturePlace();
                    Dictionary<ShortGuid, Composite> existing = destination.Commands.Entries.Where(o => o != null).GroupBy(o => o.shortGUID).ToDictionary(o => o.Key, o => o.First());
                    List<Composite> ported = new List<Composite>();
                    List<string> replacedNames = new List<string>();
                    CompositeArchive.Result result;
                    bool putBack = false;
                    Send.BeginSceneBatch();
                    try
                    {
                        if (overwrite && manifest.Composites.Any(o => existing.ContainsKey(new ShortGuid(o.Guid))))
                            Singleton.Editor.CompositeBrowser.CloseAllChildTabs();
                        Singleton.OnCompositeAddPending?.Invoke();
                        result = CompositeArchive.Import(path, ids, options, destination, (copy, layouts) =>
                        {
                            ported.Add(copy);
                            if (existing.TryGetValue(copy.shortGUID, out Composite replaced) && !ReferenceEquals(replaced, copy))
                            {
                                Singleton.OnCompositeDeleted?.Invoke(replaced);
                                replacedNames.Add(replaced.name);
                            }
                            Singleton.OnCompositeAdded?.Invoke(copy);
                            //A package carries no previews. The composites it was made from are most likely edited copies, so they are
                            //taken afresh at the next save; what they place keeps the preview its ID has, as a port's does
                            if (manifest.Composites.Any(o => o.IsRoot && o.Guid == copy.shortGUID.AsUInt32))
                                CompositePreviewManager.MarkEdited(copy);
                            FlowgraphLayoutManager.ImportLayouts(copy, layouts);
                        });
                        //Put back inside the batch, as the import window does: the rebuild it asks the viewer for is then the batch's only
                        //one. Ended with nothing open, the batch had the viewer rebuild the old scene first.
                        if (Singleton.Editor.CompositeDisplay?.Composite == null && shown != null)
                        {
                            CompositeImporter.ReopenClosedPlace(shown);
                            putBack = true;
                        }
                    }
                    finally
                    {
                        Send.EndSceneBatch();
                    }
                    ViewerResourceSync.SyncImmediately();
                    //Earlier undo steps hold the composites that were just swapped for copies: they can no longer be undone
                    McpPortingTools.ForgetReplacedHistory(call, replacedNames);
                    if (!putBack)
                        Singleton.Editor.CompositeBrowser?.RefreshList();
                    JObject summary = new JObject()
                    {
                        ["package"] = manifest.DisplayName(path),
                        ["imported"] = new JArray(ported.Take(200).Select(o => McpScript.CompositeSummary(destination.Commands, o))),
                        ["imported_count"] = result.PortedCount,
                        ["roots"] = new JArray(manifest.Composites.Where(o => o.IsRoot).Select(o => o.Name)),
                        ["already_here"] = overwrite ? new JArray() : new JArray(manifest.Composites.Where(o => existing.ContainsKey(new ShortGuid(o.Guid))).Select(o => o.Name)),
                        ["models"] = result.Renderables,
                        ["collision"] = result.CollisionMappings,
                    };
                    if (result.DeadProxies.Any) summary["dead_proxies"] = result.DeadProxies.Describe("this level");
                    return summary;
                });
            }
        }

        /// <summary>
        /// The import window's "into levels on disk": each level is loaded, imported into and saved (or built) in turn on
        /// this worker, with the editor held off it meanwhile exactly as the window holds it off.
        /// </summary>
        private static object ImportPackageOnDisk(McpCall call, string path, CompositeArchive.Manifest manifest, List<uint> ids, CompositeArchive.ImportOptions options)
        {
            List<string> levels = PackageLevels(call);
            bool build = call.Bool("build");

            //A way back for each level first, before any is written
            JObject backups = new JObject();
            if (call.Bool("backup_first", true))
                foreach (string level in levels)
                {
                    call.ThrowIfCancelled();
                    global::OpenCAGE.Backups.AlienLevel.AlienBackup backup = McpEditorTools.MakeBackup(call, level, "Before importing " + manifest.DisplayName(path));
                    if (backup != null) backups[level] = backup.ID;
                }
            bool gameWasRunning = EditorUtils.ThisInstallsGameRunning();

            CommandsEditor editor = null;
            McpEditor.UI(() =>
            {
                editor = McpEditor.Editor;
                //A level being backed up or saved must not be rewritten underneath it (the window's own rule)
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                    throw new McpError("A level backup is running. Try again when it has finished.");
                if (UndoStack.Current.Blocked)
                    throw new McpError("OpenCAGE is saving a level. Try again when it has finished.");
                if (editor.IsLevelLoadInProgress)
                    throw new McpError("A level is loading. Try again when it has finished.");
                McpEditor.RequireUndoIdle();
                string open = editor.CompositeBrowser?.Content?.Level?.Name;
                string clash = open == null ? null : levels.FirstOrDefault(o => string.Equals(o, open, StringComparison.OrdinalIgnoreCase) || o.EndsWith("/" + open, StringComparison.OrdinalIgnoreCase) || open.EndsWith("/" + o, StringComparison.OrdinalIgnoreCase));
                if (clash != null)
                    throw new McpError(clash + " is open in the editor: import into it without 'levels' (then save_level), or open another level first.");
                editor.CloseLevelPicker();
                editor.Enabled = false;
                UndoStack.Current.Blocked = true;
            });

            List<CompositeArchive.LevelResult> results;
            try
            {
                using (McpEditorTools.Heartbeat(call, "Importing " + manifest.DisplayName(path) + " into " + levels.Count + " level(s)" + (build ? " and building" : "")))
                    results = CompositeArchive.ImportIntoLevelsOnDisk(path, ids, options, levels, build);
            }
            finally
            {
                McpEditor.UI(() =>
                {
                    UndoStack.Current.Blocked = false;
                    if (editor != null && !editor.IsDisposed) editor.Enabled = true;
                });
            }
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            CompositeArchive.LevelResult failed = results.FirstOrDefault(o => o.Error != null);
            List<CompositeArchive.LevelResult> done = results.Where(o => o.Result != null).ToList();
            JObject summary = new JObject()
            {
                ["package"] = manifest.DisplayName(path),
                ["built"] = build,
                ["levels"] = new JArray(done.Select(o =>
                {
                    JObject item = new JObject()
                    {
                        ["level"] = o.Level,
                        ["imported_count"] = o.Result.PortedCount,
                        ["models"] = o.Result.Renderables,
                        ["collision"] = o.Result.CollisionMappings,
                        ["physics_systems"] = o.Result.PhysicsSystems,
                    };
                    if (o.Result.DeadProxies.Any) item["dead_proxies"] = o.Result.DeadProxies.Describe(o.Level);
                    return item;
                })),
                ["roots"] = new JArray(manifest.Composites.Where(o => o != null && o.IsRoot).Select(o => o.Name)),
            };
            if (failed != null)
                throw new McpError(McpErrorCodes.Failed, "The import failed on " + failed.Level + ": " + failed.Error + ". " + (done.Count == 0 ? "No level was written." : "Written and saved: " + string.Join(", ", done.Select(o => o.Level)) + ".") + " " + failed.Level + " may be part-written on disk: " +
                    (backups[failed.Level] != null ? "restore_backup level '" + failed.Level + "' id " + backups[failed.Level] + " puts it back as it was before this call." : "restore it from a backup (list_backups), or verify the game files."));
            if (backups.Count != 0)
                summary["backups"] = backups;
            if (gameWasRunning && !EditorUtils.ThisInstallsGameRunning())
                summary["closed_game"] = true;
            call.Note("Written to disk. Open a level with load_level to place what arrived." + (backups.Count != 0 ? " Each level was backed up first: restore_backup with its id in 'backups' undoes it." : ""));
            return summary;
        }

        /// <summary>import_composite_package's 'levels', resolved and checked.</summary>
        private static List<string> PackageLevels(McpCall call)
        {
            List<string> levels = new List<string>();
            foreach (string raw in call.StrList("levels"))
            {
                string match = McpLevels.Resolve(raw, call: call, argument: "levels");
                if (!levels.Contains(match, StringComparer.OrdinalIgnoreCase)) levels.Add(match);
            }
            if (levels.Count == 0)
                throw McpError.Invalid("'levels' is empty: name the level(s) to import into.");
            return levels;
        }

        /// <summary>import_composite_package dry_run: the package's composites, and which the open level (or each of 'levels', from its script index) already has.</summary>
        private static JObject ImportPlan(McpCall call, string path, CompositeArchive.Manifest manifest, bool overwrite)
        {
            List<CompositeArchive.CompositeEntry> all = manifest.Composites.Where(o => o != null).ToList();
            JObject plan = new JObject()
            {
                ["dry_run"] = true,
                ["package"] = manifest.DisplayName(path),
                ["roots"] = new JArray(all.Where(o => o.IsRoot).Select(o => o.Name)),
                ["composites"] = all.Count,
            };
            JArray targets = new JArray();
            if (call.Has("levels"))
            {
                foreach (string level in PackageLevels(call))
                {
                    HashSet<ShortGuid> there = new HashSet<ShortGuid>(CompositeIndexCache.Get(level).Select(o => o.ID));
                    List<string> clash = all.Where(o => there.Contains(new ShortGuid(o.Guid))).Select(o => o.Name).ToList();
                    targets.Add(new JObject() { ["level"] = level, ["new"] = all.Count - clash.Count, [overwrite ? "would_replace" : "already_there_kept"] = new JArray(clash.Take(100)) });
                }
                if (EditorUtils.ThisInstallsGameRunning())
                    call.Note("Alien: Isolation is running: the real call closes it.");
            }
            else
            {
                HashSet<ShortGuid> here = McpEditor.UI(() => new HashSet<ShortGuid>(McpEditor.RequireCommands(forEditing: false).Entries.Where(o => o != null).Select(o => o.shortGUID)));
                List<string> clash = all.Where(o => here.Contains(new ShortGuid(o.Guid))).Select(o => o.Name).ToList();
                targets.Add(new JObject() { ["level"] = "the open level", ["new"] = all.Count - clash.Count, [overwrite ? "would_replace" : "already_here_kept"] = new JArray(clash.Take(100)) });
            }
            plan["into"] = targets;
            return plan;
        }
        #endregion

        #region UI.PAK
        private static string UiPakPath() => Path.GetFullPath(Path.Combine(Singleton.PathToAI, "DATA", "UI.PAK"));

        private static PAK2 ReadUiPak()
        {
            string file = UiPakPath();
            if (!File.Exists(file))
                throw new McpError("This install has no DATA/UI.PAK.");
            PAK2 archive = new PAK2(file);
            if (!archive.Loaded)
                throw new McpError("DATA/UI.PAK could not be read.");
            return archive;
        }

        private static string NormaliseEntry(string path) => (path ?? "").Trim().Replace('\\', '/').TrimStart('/');

        private static PAK2.File FindEntry(PAK2 archive, string path)
        {
            string wanted = NormaliseEntry(path);
            return archive.Entries.FirstOrDefault(o => string.Equals(NormaliseEntry(o.Filename), wanted, StringComparison.OrdinalIgnoreCase));
        }

        private static object ListUiFiles(McpCall call)
        {
            PAK2 archive = ReadUiPak();
            string filter = NormaliseEntry(call.Str("filter"));
            List<PAK2.File> files = archive.Entries.Where(o => o != null && (filter.Length == 0 || NormaliseEntry(o.Filename).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(o => o.Filename, StringComparer.OrdinalIgnoreCase).ToList();
            JObject result = new JObject() { ["count"] = files.Count };
            McpPaging.Page(call, files, result, "files", o =>
            {
                JObject file = new JObject() { ["path"] = NormaliseEntry(o.Filename), ["bytes"] = o.Content?.Length ?? 0 };
                string uiMod = McpLevelTools.UiModOf(o.Filename);
                if (uiMod != null) file["swapped_by_ui_mod"] = uiMod;
                return file;
            }, 300);
            return result;
        }

        private static object EditUiPak(McpCall call)
        {
            string action = call.Str("action", required: true).Trim().ToLowerInvariant();
            PAK2 archive = ReadUiPak();

            if (action == "export")
            {
                string destination = McpAssets.AbsolutePath(call.Str("destination", required: true), "destination");
                bool overwrite = call.Bool("overwrite");
                if (call.Has("path"))
                {
                    if (call.Has("filter")) throw new McpError("Give either 'path' (one file) or 'filter' (several), not both.");
                    PAK2.File entry = FindEntry(archive, call.Str("path"));
                    if (entry == null) throw new McpError("UI.PAK has no '" + call.Str("path") + "' (list_ui_files shows them).");
                    if (File.Exists(destination) && !overwrite) throw new McpError("There is already a file at " + destination + ". Pass overwrite: true to replace it.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.WriteAllBytes(destination, entry.Content ?? new byte[0]);
                    return new JObject() { ["exported"] = NormaliseEntry(entry.Filename), ["written"] = destination, ["bytes"] = entry.Content?.Length ?? 0 };
                }
                if (!call.Has("filter")) throw new McpError("export needs 'path' (one file to 'destination') or 'filter' ('*' for all, into 'destination' as a folder).");
                string filter = NormaliseEntry(call.Str("filter"));
                List<PAK2.File> files = archive.Entries.Where(o => o?.Content != null && (filter == "*" || filter.Length == 0 || NormaliseEntry(o.Filename).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                if (files.Count == 0) throw new McpError("No file in UI.PAK matches '" + filter + "'.");
                string root = Path.GetFullPath(destination);
                int written = 0, skipped = 0;
                foreach (PAK2.File entry in files)
                {
                    string target = Path.GetFullPath(Path.Combine(root, NormaliseEntry(entry.Filename).Replace('/', Path.DirectorySeparatorChar)));
                    if (!target.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                    if (File.Exists(target) && !overwrite) { skipped++; continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.WriteAllBytes(target, entry.Content);
                    written++;
                }
                return new JObject() { ["folder"] = root, ["matched"] = files.Count, ["written"] = written, ["skipped"] = skipped };
            }

            if (action != "write" && action != "delete")
                throw new McpError("Unknown action '" + action + "'.");
            string entryPath = NormaliseEntry(call.Str("path", required: true));
            if (entryPath.Length == 0 || entryPath.Contains(".."))
                throw new McpError("'path' has to be a file path inside UI.PAK, e.g. 'UI/HUD/...'.");
            PAK2.File existing = FindEntry(archive, entryPath);
            bool dryRun = call.Bool("dry_run");
            JObject result = new JObject() { ["action"] = action, ["path"] = entryPath, ["dry_run"] = dryRun };

            byte[] content = null;
            if (action == "write")
            {
                string source = McpAssets.AbsolutePath(call.Str("source_file", required: true), "source_file");
                if (!File.Exists(source)) throw new McpError("There is no file at " + source + ".");
                if (existing == null && !call.Bool("create"))
                    throw new McpError("UI.PAK has no '" + entryPath + "'. Pass create: true to add it as a new file, or check the path with list_ui_files.");
                content = File.ReadAllBytes(source);
                result[existing == null ? "adds" : "replaces"] = true;
                if (existing != null) result["bytes_before"] = existing.Content?.Length ?? 0;
                result["bytes"] = content.Length;
            }
            else
            {
                if (existing == null) throw new McpError("UI.PAK has no '" + entryPath + "' (list_ui_files shows them).");
                result["bytes_before"] = existing.Content?.Length ?? 0;
            }

            //A UI mod swaps this whole movie when switched (launch_options): its edit would go then
            string uiMod = McpLevelTools.UiModOf(entryPath);
            if (uiMod != null)
                call.Note(entryPath + " is the movie the " + uiMod + " UI mod swaps: switching launch_options " + uiMod + " (or reapply) replaces this edit with the stock or mod movie.");
            //The UI Editor keeps its own copy of the archive and writes it back on every change it makes
            bool editorOpen = McpEditor.UI(() => System.Windows.Forms.Application.OpenForms.OfType<EditPAK2>().Any(o => !o.IsDisposed));
            if (dryRun)
            {
                if (editorOpen) call.Note("OpenCAGE's UI Editor window is open: the real call is refused until it is closed.");
                if (EditorUtils.ThisInstallsGameRunning()) call.Note("The game is running: the real call needs close_game: true.");
                return result;
            }
            if (editorOpen)
                throw new McpError("OpenCAGE's UI Editor window is open: it would write its own copy of UI.PAK over this. Close it and try again.");
            if (EditorUtils.ThisInstallsGameRunning())
            {
                if (!call.Bool("close_game"))
                    throw new McpError(McpErrorCodes.Refused, "The game is running and holds UI.PAK open. Close it, or pass close_game: true to close it as the UI Editor does.");
                result["closed_game"] = new JArray(EditorUtils.CloseAI(null, thisInstallOnly: true));
            }

            if (action == "write")
            {
                if (existing != null) existing.Content = content;
                else archive.Entries.Add(new PAK2.File() { Filename = entryPath, Content = content });
            }
            else
                archive.Entries.RemoveAll(o => string.Equals(NormaliseEntry(o.Filename), entryPath, StringComparison.OrdinalIgnoreCase));

            //What the UI Editor's save does: give the mod tools their before-copy, then write the archive
            Modding.ModServices.CaptureBeforeWrite(archive.Filepath);
            if (!archive.Save())
                throw new McpError("UI.PAK could not be written (is the game still running?). Nothing was changed on disk.");
            result["written"] = true;
            return result;
        }
        #endregion

        #region Configuration files
        private static string DataFolder() => Path.GetFullPath(Path.Combine(Singleton.PathToAI, "DATA"));

        /// <summary>A path under DATA, refused if it leaves it or is not BML/XML.</summary>
        private static string ConfigPath(string relative)
        {
            string data = DataFolder();
            string full = Path.GetFullPath(Path.Combine(data, relative.Replace('/', '\\').TrimStart('\\')));
            if (!full.StartsWith(data + "\\", StringComparison.OrdinalIgnoreCase))
                throw new McpError("Configuration files have to be inside the install's DATA folder.");
            if (!full.EndsWith(".BML", StringComparison.OrdinalIgnoreCase) && !full.EndsWith(".XML", StringComparison.OrdinalIgnoreCase))
                throw new McpError("Only .BML and .XML configuration files can be read or written here.");
            if (full.IndexOf("\\ENV\\", data.Length, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new McpError("Level files are not configuration files.");
            if (!File.Exists(full))
                throw new McpError("There is no " + relative + " (list_config_files shows them).");
            return full;
        }

        private static XmlDocument ReadXml(string file, out BML bml)
        {
            bml = null;
            if (file.EndsWith(".BML", StringComparison.OrdinalIgnoreCase))
            {
                bml = new BML(file);
                XmlDocument content = bml.Content;
                if (content == null)
                    throw new McpError("Could not read " + Path.GetFileName(file) + ".");
                return content;
            }
            XmlDocument document = new XmlDocument() { PreserveWhitespace = true };
            document.Load(file);
            return document;
        }

        private static XmlNodeList SelectNodes(XmlDocument document, string xpath)
        {
            try { return document.SelectNodes(xpath); }
            catch (Exception e) { throw new McpError("That XPath is not valid: " + e.Message); }
        }

        private static string Pretty(XmlDocument document)
        {
            using (StringWriter text = new StringWriter())
            using (XmlTextWriter writer = new XmlTextWriter(text) { Formatting = System.Xml.Formatting.Indented })
            {
                document.WriteTo(writer);
                writer.Flush();
                return text.ToString();
            }
        }
        #endregion
    }
}
