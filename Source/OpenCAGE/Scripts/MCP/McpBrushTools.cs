using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Brushwork;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>Hammer-style brushes (boxes and planes) made, changed and listed: see <see cref="BrushService"/>.</summary>
    internal static class McpBrushTools
    {
        private const string FacesText = "Per-face texturing: {\"<face>\": {\"material\": \"LEVEL\\\\MATERIAL\" (or null for the grey grid), \"scale\": [u, v] (1 = one repeat per 2 m), \"offset\": [u, v] (repeats), \"rotation\": degrees}}. " +
            "Box faces: +x, -x, +y (top), -y (bottom), +z, -z; plane faces: top, bottom; 'all' sets every face.";

        public static IEnumerable<McpTool> Tools()
        {
            JObject faceSchema = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["material"] = new JObject() { ["type"] = new JArray("string", "null"), ["description"] = "A level material's name; null for the brush default (the grey grid, OPENCAGE\\BRUSH_GRID)." },
                    ["scale"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "[u, v] texture scale: 1 = one repeat per 2 m, 2 = twice as large." },
                    ["offset"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "[u, v] shift in repeats." },
                    ["rotation"] = new JObject() { ["type"] = "number", ["description"] = "Turn of the texture on the face, degrees." },
                },
            };

            yield return new McpTool()
            {
                Name = "create_brush",
                Title = "Create brush",
                Description = "Make a Hammer-style brush - a box or a plane - in a composite of the open level: a ModelReference entity drawing a model OpenCAGE generates from it (grey grid texture by default, any level material per face), with solid collision made from its triangles (WORLD + BALLISTIC as retail pairs them: walked on, shot at, footsteps and zones). " +
                    "The brush is recorded in the level so edit_brush can resize and retexture it later. A box's origin is its bottom centre, a plane's its centre. Textures are world-aligned at 1 repeat per 2 m and move with the brush afterwards (texture lock). " +
                    "One undo step; written by save_level (build=true for the game).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("kind", "box (default) or plane.", options: new[] { "box", "plane" }),
                    McpSchema.String("composite", "The composite to put it in (path or id; default 'root', the level itself)."),
                    McpSchema.Position("position", "Where its origin goes, in the composite's space (default 0,0,0)."),
                    McpSchema.Rotation("rotation", "Its rotation."),
                    McpSchema.Vector("size", "[x, y, z] metres (box default [2, 2, 2]; plane default [4, 0, 4], y ignored)."),
                    McpSchema.Vector("min", "Instead of size: the box's corner nearest -infinity in its own space (with max)."),
                    McpSchema.Vector("max", "Instead of size: the far corner (with min)."),
                    McpSchema.String("material", "One level material for every face (default the grey grid)."),
                    McpSchema.Nested("faces", FacesText, new JObject() { ["type"] = "object", ["additionalProperties"] = faceSchema }),
                    McpSchema.Boolean("double_sided", "Plane: also drawn and collided with from below (default false)."),
                    McpSchema.String("collision", "solid (default: WORLD + BALLISTIC pair), world, ballistic or none.", options: BrushCollision.Modes),
                    McpSchema.Boolean("world_aligned", "Line the texture grid up with the world (default true), so neighbouring brushes' grids meet."),
                    McpSchema.String("name", "The entity's name (default Brush_Box_N / Brush_Plane_N).")),
                Run = CreateBrush,
            };

            yield return new McpTool()
            {
                Name = "edit_brush",
                Title = "Edit brush",
                Description = "Change a brush made by create_brush (or the editor): resize it (size keeping its bottom centre, min/max outright, or move_faces pushing single faces in or out along their normals as a viewport handle drag does), retexture faces, change its collision. " +
                    "Its model is regenerated in place (the same entity and model) and its collision rebuilt when its shape changes. A copy of a brush (pasted, duplicated) gets a model of its own the first time it is changed. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding it (path or id).", required: true),
                    McpSchema.String("entity", "The brush entity's id or name.", required: true),
                    McpSchema.Vector("size", "New [x, y, z] in metres, about its bottom centre (box) or centre (plane)."),
                    McpSchema.Vector("min", "New near corner, in its own space."),
                    McpSchema.Vector("max", "New far corner, in its own space."),
                    McpSchema.Map("move_faces", "Face -> metres to push it out along its normal (negative pulls it in): {\"+x\": 1.5, \"-y\": -0.5}. Box only."),
                    McpSchema.Number("snap", "With move_faces: round the moved faces onto this grid (metres, in the brush's space)."),
                    McpSchema.String("material", "Set every face to this level material ('grid' for the default)."),
                    McpSchema.Nested("faces", FacesText, new JObject() { ["type"] = "object", ["additionalProperties"] = faceSchema }),
                    McpSchema.Boolean("double_sided", "Plane: drawn from below too."),
                    McpSchema.String("collision", "solid, world, ballistic or none.", options: BrushCollision.Modes),
                    McpSchema.Boolean("align_to_world", "Line its texture grid up with the world again from where it now sits (after it was moved).")),
                Run = EditBrush,
            };

            yield return new McpTool()
            {
                Name = "edit_brush_mesh",
                Title = "Edit brush geometry",
                Description = "Edit a brush's geometry as Hammer's vertex, clip and extrude tools do. 'to_mesh' turns a box or plane into an editable mesh (its faces keep their texturing; it no longer has a box's resize handles or size); the other operations work on a mesh brush: " +
                    "'move_vertices' (vertices by index, by delta), 'scale' (factor, about pivot), 'clip' (cut with the plane through point with normal: the side the normal points to goes, or the other with keep 'front'; the cut is capped), 'extrude' (push a face out by distance, with side faces). " +
                    "Positions are in the brush's own space; the result lists its vertices and faces. The model and collision are rebuilt; one undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding it (path or id).", required: true),
                    McpSchema.String("entity", "The brush entity's id or name.", required: true),
                    McpSchema.String("operation", "What to do.", required: true, options: new[] { "to_mesh", "move_vertices", "scale", "clip", "extrude" }),
                    McpSchema.Array("vertices", "move_vertices: the vertex indices (from the result's 'vertices').", new JObject() { ["type"] = "integer" }),
                    McpSchema.Vector("delta", "move_vertices: [x, y, z] metres to move them by."),
                    McpSchema.Any("factor", "scale: one number, or [x, y, z]."),
                    McpSchema.Vector("pivot", "scale: the point it scales about (default the centre of its bounds)."),
                    McpSchema.Vector("point", "clip: a point on the plane."),
                    McpSchema.Vector("normal", "clip: the plane's normal - what is on that side is cut away (unless keep 'front')."),
                    McpSchema.String("keep", "clip: 'back' (default: keep what is behind the normal) or 'front'.", options: new[] { "back", "front" }),
                    McpSchema.Integer("face", "extrude: the face index (from the result's 'mesh_faces')."),
                    McpSchema.Number("distance", "extrude: metres out along the face's normal (negative pushes it in).")),
                Run = EditBrushMesh,
            };

            yield return new McpTool()
            {
                Name = "match_collision",
                Title = "Create collision to match",
                Description = "Give ModelReferences collision made to match what they draw: a new proxy from their renderable's triangles (LOD 0, in the model's space - the entity's placement places it), with the render material covering most of the surface as the physics material (footsteps, impacts). " +
                    "'solid' (default) is retail's WORLD + BALLISTIC pair (walked on, shot at, footstep sounds and zones; the BALLISTIC row on a '<name>_Ballistic' sibling), 'world' or 'ballistic' one row. See-through submeshes (glass, decals, blended or effect materials) are left out unless include_see_through; slivers are dropped and corners welded. " +
                    "Replaces whatever collision each had (its old proxies are left unreferenced, for collision compaction). One undo step per entity; reaches the game after save_level build=true. (A brush's own collision follows the brush: change it with edit_brush.)",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding them (path or id).", required: true),
                    McpSchema.String("entity", "One ModelReference (id or name)."),
                    McpSchema.Strings("entities", "Several (ids or names)."),
                    McpSchema.String("collision", "solid (default), world or ballistic.", options: new[] { "solid", "world", "ballistic" }),
                    McpSchema.Boolean("include_see_through", "Also make collision from glass, decal, blended and effect submeshes (default false)."),
                    McpSchema.Number("min_triangle_area", "Leave out triangles smaller than this, square metres (default 0.00001)."),
                    McpSchema.Number("weld", "Make corners closer than this one, metres (default 0.001)."),
                    McpSchema.String("source", "'match' (default): new collision from the triangles. 'existing': the collision the drawn model already has - the same model placed elsewhere with collision (its proxy, physics material and pairing), else the proxy the model's submeshes name - refused when there is none. 'auto': existing when there is one, else match. 'list': only report those candidates, best first.", options: new[] { "match", "existing", "auto", "list" })),
                Destructive = true,
                Run = MatchCollision,
            };

            yield return new McpTool()
            {
                Name = "make_physics_prop",
                Title = "Make physics prop",
                Description = "Turn a ModelReference into a physics prop the way retail's are built: it moves into a composite of its own ('PhysicsProps\\<name>' by default, an instance of it left where it was - group_into_composite), which gets a PhysicsSystem entity bound to a new one-body dynamic system made from the model's convex hull (import_physics_system: mass, friction, restitution). " +
                    "Its static collision (and ballistic half) is removed first: a body that moves cannot also be level geometry - and so is that of its collision-only partners in the composite (a retail prop's '<name>_COL'; remove_prop_collision false keeps those). Each step is its own undo step; the physics system itself cannot be undone. Reaches the game after save_level build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding it (path or id).", required: true),
                    McpSchema.String("entity", "The ModelReference (id or name).", required: true),
                    McpSchema.String("name", "The new composite's path (default PhysicsProps\\<entity name>)."),
                    McpSchema.Number("mass", "Kilograms, 0.01 to 10000 (default: from the hull's volume)."),
                    McpSchema.Number("friction", "0 to 10 (default 0.5)."),
                    McpSchema.Number("restitution", "Bounciness, 0 to 1 (default 0.4)."),
                    McpSchema.Boolean("remove_prop_collision", "Also take the static collision off the composite's collision-only entities that are this model's (named <entity>_COL..., or all of them when it is the composite's only drawn model), so no invisible block stays where the prop starts (default true)."),
                    McpSchema.Boolean("spawned", "Place it as retail places every dynamic prop: spawned at load by an AssetSpawner (a wrapper '<name>_Spawned' made once and reused, its bodies keyed by name - spawn_physics_prop), so it can be knocked about: it starts at rest where it is put and, once pushed or hit, falls, settles and can be pushed again. Placed directly, a dynamic body never moves, however hard it is pushed (default true).")),
                Destructive = true,
                Run = MakePhysicsProp,
            };

            yield return new McpTool()
            {
                Name = "list_brushes",
                Title = "List brushes",
                Description = "The brushes in the open level (made with create_brush or the editor's Create Brush): each one's entity, kind, size, faces' materials, model and collision proxies. Copies not changed yet are listed too ('copy_of').",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Only this composite (path or id)."),
                    McpSchema.Limit(100, "brushes"),
                    McpSchema.Offset("brushes")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListBrushes,
            };
        }

        private static object CreateBrush(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel();
                McpEditor.RequireUndoIdle();
                Level level = content.Level;
                Commands commands = level.Commands;
                Composite composite = McpScript.FindComposite(commands, call.Str("composite") ?? "root");

                string kindText = (call.Str("kind") ?? "box").Trim().ToLowerInvariant();
                if (kindText != "box" && kindText != "plane") throw McpError.Invalid("'kind' is box or plane.");
                bool plane = kindText == "plane";

                Vector3 position = call.Has("position") ? McpValues.ReadVector(call.Token("position"), "position", null) : Vector3.Zero;
                Vector3 rotation = call.Has("rotation") ? McpValues.ReadVector(call.Token("rotation"), "rotation", null) : Vector3.Zero;
                Vector3 min, max;
                if (call.Has("min") || call.Has("max"))
                {
                    if (!call.Has("min") || !call.Has("max") || call.Has("size")) throw McpError.Invalid("Give min and max together, or size.");
                    min = McpValues.ReadVector(call.Token("min"), "min", null);
                    max = McpValues.ReadVector(call.Token("max"), "max", null);
                }
                else
                {
                    Vector3 size = call.Has("size") ? McpValues.ReadVector(call.Token("size"), "size", null) : (plane ? new Vector3(4, 0, 4) : new Vector3(2, 2, 2));
                    min = new Vector3(-size.X / 2, plane ? 0 : 0, -size.Z / 2);
                    max = new Vector3(size.X / 2, plane ? 0 : size.Y, size.Z / 2);
                }

                string material = MaterialArg(level, call.Str("material"));
                BrushDefinition brush = plane ? BrushDefinition.NewPlane(min, max, material, call.Bool("double_sided")) : BrushDefinition.NewBox(min, max, material);
                ApplyFaces(level, brush, call.Object("faces"));
                if (call.Has("collision"))
                {
                    if (!BrushCollision.IsMode(call.Str("collision"))) throw McpError.Invalid("'collision' is solid, world, ballistic or none.");
                    brush.Collision = call.Str("collision").Trim().ToLowerInvariant();
                }
                if (call.Bool("world_aligned", true))
                    brush.UvOrigin = WorldOrigin(commands, composite, new cTransform(position, rotation));

                string name = call.Str("name");
                if (name != null) McpScriptEdit.RequireStorableText(name, "name");
                List<string> notes = new List<string>();
                FunctionEntity entity;
                try { entity = BrushService.Create(level, composite, brush, new cTransform(position, rotation), name, "AI: Create " + (name ?? "brush"), notes); }
                catch (BrushException e) { throw new McpError(e.Message); }
                foreach (string note in notes) call.Note(note);
                McpScriptEdit.Show(composite, new List<Entity>() { entity });
                JObject result = Describe(level, composite, entity, BrushService.Get(level, composite, entity), false);
                call.Note("save_level with build=true puts it in the game.");
                return result;
            });
        }

        private static object EditBrush(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel();
                McpEditor.RequireUndoIdle();
                Level level = content.Level;
                Commands commands = level.Commands;
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                Entity found = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                BrushDefinition before = BrushService.Get(level, composite, found);
                if (before == null || !(found is FunctionEntity entity))
                    throw new McpError(McpScript.EntityName(commands, composite, found) + " is not a brush (list_brushes lists them).");
                BrushDefinition brush = before.Clone();
                bool plane = brush.Kind == BrushKind.Plane;

                if (call.Has("size"))
                {
                    if (call.Has("min") || call.Has("max")) throw McpError.Invalid("Give size, or min/max, not both.");
                    Vector3 size = McpValues.ReadVector(call.Token("size"), "size", null);
                    Vector3 centre = (brush.Min + brush.Max) / 2;
                    brush.Min = new Vector3(centre.X - size.X / 2, brush.Min.Y, centre.Z - size.Z / 2);
                    brush.Max = new Vector3(centre.X + size.X / 2, plane ? brush.Min.Y : brush.Min.Y + size.Y, centre.Z + size.Z / 2);
                }
                if (call.Has("min")) brush.Min = McpValues.ReadVector(call.Token("min"), "min", brush.Min);
                if (call.Has("max")) brush.Max = McpValues.ReadVector(call.Token("max"), "max", brush.Max);
                if (plane) brush.Max = new Vector3(brush.Max.X, brush.Min.Y, brush.Max.Z);

                JObject moves = call.Object("move_faces");
                if (moves != null)
                {
                    if (brush.Kind != BrushKind.Box) throw McpError.Invalid("move_faces works on a box.");
                    float snap = (float)call.Num("snap", 0);
                    foreach (JProperty move in moves.Properties())
                    {
                        int face = Array.IndexOf(BrushDefinition.BoxFaceNames, move.Name.Trim().ToLowerInvariant());
                        if (face < 0) throw McpError.Invalid("'" + move.Name + "' in move_faces is not a face: +x, -x, +y, -y, +z, -z.");
                        float by = (float)McpValues.ReadDouble(move.Value, "move_faces." + move.Name);
                        BrushEditing.MoveFace(brush, face, by, snap);
                    }
                }

                if (call.Has("double_sided"))
                {
                    if (!plane) throw McpError.Invalid("double_sided is a plane's.");
                    brush.DoubleSided = call.Bool("double_sided");
                }
                if (call.Has("material"))
                {
                    string material = MaterialArg(level, call.Str("material"));
                    foreach (BrushFace face in brush.FaceTextures) face.Material = material;
                }
                ApplyFaces(level, brush, call.Object("faces"));
                if (call.Has("collision"))
                {
                    if (!BrushCollision.IsMode(call.Str("collision"))) throw McpError.Invalid("'collision' is solid, world, ballistic or none.");
                    brush.Collision = call.Str("collision").Trim().ToLowerInvariant();
                }
                if (call.Bool("align_to_world"))
                    brush.UvOrigin = WorldOrigin(commands, composite, entity.GetParameter(ShortGuids.position)?.content as cTransform ?? new cTransform());

                List<string> notes = new List<string>();
                try { BrushService.Apply(level, composite, entity, brush, "AI: Edit " + McpScript.EntityName(commands, composite, entity), notes); }
                catch (BrushException e) { throw new McpError(e.Message); }
                foreach (string note in notes) call.Note(note);
                return Describe(level, composite, entity, BrushService.Get(level, composite, entity), false);
            });
        }

        private static object EditBrushMesh(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel();
                McpEditor.RequireUndoIdle();
                Level level = content.Level;
                Commands commands = level.Commands;
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                Entity found = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                BrushDefinition before = BrushService.Get(level, composite, found);
                if (before == null || !(found is FunctionEntity entity))
                    throw new McpError(McpScript.EntityName(commands, composite, found) + " is not a brush (list_brushes lists them).");
                string operation = (call.Str("operation", required: true) ?? "").Trim().ToLowerInvariant();
                BrushDefinition brush = operation == "to_mesh" ? BrushEditing.ToMesh(before) : before.Clone();
                try
                {
                    switch (operation)
                    {
                        case "to_mesh":
                            if (before.Kind == BrushKind.Mesh) call.Note("It is a mesh already.");
                            break;
                        case "move_vertices":
                            {
                                JArray vertices = call.Token("vertices") as JArray ?? throw McpError.Invalid("Give 'vertices': indices from the result's 'vertices'.");
                                Vector3 delta = McpValues.ReadVector(call.Token("delta") ?? throw McpError.Invalid("Give 'delta'."), "delta", null);
                                BrushEditing.MoveVertices(brush, vertices.Select(o => (int)o), delta);
                                break;
                            }
                        case "scale":
                            {
                                JToken token = call.Token("factor") ?? throw McpError.Invalid("Give 'factor'.");
                                Vector3 factor = token is JArray ? McpValues.ReadVector(token, "factor", null) : new Vector3((float)McpValues.ReadDouble(token, "factor"));
                                Vector3 pivot = call.Has("pivot") ? McpValues.ReadVector(call.Token("pivot"), "pivot", null) : (brush.Min + brush.Max) / 2;
                                BrushEditing.Scale(brush, factor, pivot);
                                break;
                            }
                        case "clip":
                            {
                                Vector3 point = McpValues.ReadVector(call.Token("point") ?? throw McpError.Invalid("Give 'point'."), "point", null);
                                Vector3 normal = McpValues.ReadVector(call.Token("normal") ?? throw McpError.Invalid("Give 'normal'."), "normal", null);
                                BrushEditing.Clip(brush, point, normal, string.Equals(call.Str("keep"), "front", StringComparison.OrdinalIgnoreCase));
                                break;
                            }
                        case "extrude":
                            if (!call.Has("face") || !call.Has("distance")) throw McpError.Invalid("Give 'face' and 'distance'.");
                            BrushEditing.Extrude(brush, call.Int("face"), (float)call.Num("distance"));
                            break;
                        default:
                            throw McpError.Invalid("'operation' is to_mesh, move_vertices, scale, clip or extrude.");
                    }
                }
                catch (BrushException e) { throw new McpError(e.Message); }

                List<string> notes = new List<string>();
                try { BrushService.Apply(level, composite, entity, brush, "AI: " + operation.Replace('_', ' ') + " " + McpScript.EntityName(commands, composite, entity), notes); }
                catch (BrushException e) { throw new McpError(e.Message); }
                notes.AddRange(BrushEditing.ShapeWarnings(brush));
                foreach (string note in notes) call.Note(note);
                return Describe(level, composite, entity, BrushService.Get(level, composite, entity), false);
            });
        }

        private static object MatchCollision(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel();
                McpEditor.RequireUndoIdle();
                Level level = content.Level;
                Commands commands = level.Commands;
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                List<string> names = call.StrList("entities");
                if (call.Has("entity")) names.Insert(0, call.Str("entity"));
                if (names.Count == 0) throw McpError.Invalid("Give 'entity' or 'entities'.");
                CollisionMatch.Options options = new CollisionMatch.Options()
                {
                    Mode = (call.Str("collision") ?? "solid").Trim().ToLowerInvariant(),
                    SkipSeeThrough = !call.Bool("include_see_through"),
                    MinimumTriangleArea = (float)call.Num("min_triangle_area", 1e-5),
                    Weld = (float)call.Num("weld", 0.001),
                };
                if (options.Mode != "solid" && options.Mode != "world" && options.Mode != "ballistic") throw McpError.Invalid("'collision' is solid, world or ballistic.");
                List<FunctionEntity> targets = new List<FunctionEntity>();
                foreach (string name in names)
                {
                    Entity found = McpScript.FindEntity(commands, composite, name);
                    if (!CollisionMatch.CanMatch(level, composite, found))
                        throw McpError.Invalid(McpScript.EntityName(commands, composite, found) + " is not a ModelReference drawing a model (or it is a collision pair's ballistic half).");
                    targets.Add((FunctionEntity)found);
                }
                JArray results = new JArray();
                foreach (FunctionEntity entity in targets)
                {
                    string name = McpScript.EntityName(commands, composite, entity);
                    if (BrushService.IsBrush(level, composite, entity))
                        call.Note(name + " is a brush: its collision is rebuilt from the brush whenever the brush changes (edit_brush collision sets it).");
                    string source = (call.Str("source") ?? "match").Trim().ToLowerInvariant();
                    if (source != "match")
                    {
                        List<CollisionMatch.Candidate> candidates = CollisionMatch.Candidates(level, composite, entity);
                        if (source == "list")
                        {
                            results.Add(new JObject()
                            {
                                ["entity"] = McpScript.Brief(commands, composite, entity),
                                ["candidates"] = new JArray(candidates.Take(10).Select(o => new JObject() { ["kind"] = o.Kind, ["proxy"] = o.Proxy?.ProxyIndex ?? -1, ["paired"] = o.Paired, ["score"] = Math.Round(o.Score, 3), ["from"] = o.Description, ["physics_material"] = o.Row?.Material?.Name })),
                            });
                            continue;
                        }
                        if (candidates.Count != 0)
                        {
                            BrushCollision.Outcome used = CollisionMatch.UseCandidate(level, composite, entity, candidates[0], "AI: Give " + name + " its model's collision");
                            results.Add(new JObject()
                            {
                                ["entity"] = McpScript.Brief(commands, composite, entity),
                                ["from"] = candidates[0].Description,
                                ["proxies"] = new JObject() { ["world"] = used.WorldProxy, ["ballistic"] = used.BallisticProxy },
                            });
                            continue;
                        }
                        if (source == "existing")
                            throw new McpError(name + "'s model has no collision anywhere in the level: use source 'match' to make some.");
                    }
                    CollisionMatch.Report report;
                    try { report = CollisionMatch.Apply(level, composite, entity, options, "AI: Create collision to match " + name); }
                    catch (BrushException e) { throw new McpError(name + ": " + e.Message); }
                    foreach (string note in report.Notes) call.Note(note);
                    JObject result = new JObject()
                    {
                        ["entity"] = McpScript.Brief(commands, composite, entity),
                        ["triangles"] = report.Triangles,
                        ["submeshes"] = report.Submeshes,
                        ["physics_material"] = report.Material?.Name,
                        ["proxies"] = new JObject() { ["world"] = report.Outcome.WorldProxy, ["ballistic"] = report.Outcome.BallisticProxy },
                    };
                    if (report.DroppedTriangles != 0) result["triangles_dropped"] = report.DroppedTriangles;
                    if (report.SkippedSubmeshes != 0) result["see_through_left_out"] = new JArray(report.Skipped.Distinct());
                    if (report.Outcome.WorldBefore >= 0 && report.Outcome.WorldBefore != report.Outcome.WorldProxy)
                        result["replaced_proxies"] = new JArray(new[] { report.Outcome.WorldBefore, report.Outcome.BallisticBefore }.Where(o => o >= 0));
                    results.Add(result);
                }
                call.Note("save_level with build=true puts the collision in the game.");
                return new JObject() { ["composite"] = composite.name, ["matched"] = results };
            });
        }

        /* make_physics_prop: retail's shape for a physics prop (a composite holding the ModelReference and a PhysicsSystem entity bound to a
           one-body system), made from the existing tools, each a step of its own */
        private static object MakePhysicsProp(McpCall call)
        {
            string compositeArg = call.Str("composite", required: true), entityArg = call.Str("entity", required: true);
            string entityName = McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                Composite composite = McpScript.FindComposite(level.Commands, compositeArg);
                Entity found = McpScript.FindEntity(level.Commands, composite, entityArg);
                if (!CollisionMatch.CanMatch(level, composite, found))
                    throw McpError.Invalid(McpScript.EntityName(level.Commands, composite, found) + " is not a ModelReference drawing a model.");
                if (BrushService.IsBrush(level, composite, found))
                    throw McpError.Invalid("A brush is level geometry: it stays static (its collision follows the brush).");
                return McpScript.EntityName(level.Commands, composite, found);
            });
            JObject result = new JObject();
            //Static collision off: the body that moves is the physics system's
            bool hasCollision = McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                Composite composite = McpScript.FindComposite(level.Commands, compositeArg);
                return CollisionPairs.RowOf(McpScript.FindEntity(level.Commands, composite, entityArg) as FunctionEntity) != null;
            });
            if (hasCollision)
                result["static_collision_removed"] = McpEditorTools.Nested(call, "set_collision", new JObject() { ["composite"] = compositeArg, ["entity"] = entityArg, ["remove"] = true });

            //...and the prop's own collision-only entities (retail props carry a 'BARREL_COL' beside 'BARREL'): left in, they would hold
            //the place the prop starts from as an invisible static block it falls through or gets stuck in. Those named after the
            //model (<name>_COL...), or every one when this is the composite's only drawn model; never another model's
            if (call.Bool("remove_prop_collision", true))
            {
                List<string> others = McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel().Level;
                    Composite composite = McpScript.FindComposite(level.Commands, compositeArg);
                    FunctionEntity entity = McpScript.FindEntity(level.Commands, composite, entityArg) as FunctionEntity;
                    string own = level.Commands.Utils.GetEntityName(composite, entity);
                    bool Draws(FunctionEntity o) => (CollisionMatch.RunOf(o)?.Count ?? 0) != 0;
                    List<FunctionEntity> drawn = composite.functions.Where(o => o.function == FunctionType.ModelReference && Draws(o)).ToList();
                    bool onlyModel = drawn.Count == 1 && drawn[0] == entity;
                    return composite.functions
                        .Where(o => o != entity && o.function == FunctionType.ModelReference && !Draws(o) && CollisionPairs.RowOf(o) != null)
                        .Where(o =>
                        {
                            string name = level.Commands.Utils.GetEntityName(composite, o) ?? "";
                            if (string.Equals(name, own + "_Ballistic", StringComparison.OrdinalIgnoreCase)) return false;
                            return onlyModel || name.StartsWith(own + "_COL", StringComparison.OrdinalIgnoreCase);
                        })
                        .Select(o => McpScript.Id(o.shortGUID)).ToList();
                });
                JArray removed = new JArray();
                foreach (string other in others)
                    removed.Add(McpEditorTools.Nested(call, "set_collision", new JObject() { ["composite"] = compositeArg, ["entity"] = other, ["remove"] = true }));
                if (removed.Count != 0) result["prop_collision_removed"] = removed;
            }

            string path = call.Str("name") ?? ("PhysicsProps\\" + entityName);
            JObject grouped = McpEditorTools.Nested(call, "group_into_composite", new JObject() { ["composite"] = compositeArg, ["entities"] = new JArray(entityArg), ["name"] = path });
            result["grouped"] = grouped;
            string created = (string)grouped["composite"]?["path"] ?? (string)grouped["composite"]?["name"] ?? path;
            result["physics_system_entity"] = McpEditorTools.Nested(call, "create_entities", new JObject() { ["composite"] = created, ["entities"] = new JArray(new JObject() { ["function"] = "PhysicsSystem", ["name"] = "PhysicsSystem" }) });
            JObject import = new JObject() { ["composite"] = created };
            foreach (string key in new[] { "mass", "friction", "restitution" })
                if (call.Has(key)) import[key] = call.Token(key);
            result["physics_system"] = McpEditorTools.Nested(call, "import_physics_system", import);
            //Placed directly, a dynamic body stays asleep where it is put: spawned at load by an AssetSpawner instead, as every retail dynamic prop is
            string placedId = (string)grouped["instance"]?["id"];
            if (call.Bool("spawned", true) && placedId != null)
                result["spawned"] = PhysicsPropSpawner.MakeSpawned(call, compositeArg, placedId);
            call.Note("save_level with build=true puts the physics prop in the game.");
            return result;
        }

        private static object ListBrushes(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                Composite only = call.Has("composite") ? McpScript.FindComposite(level.Commands, call.Str("composite")) : null;
                List<(Composite composite, FunctionEntity entity, BrushDefinition brush)> brushes = BrushService.All(level).Where(o => only == null || o.composite == only).ToList();
                JObject result = new JObject() { ["count"] = brushes.Count };
                McpPaging.Page(call, brushes, result, "brushes", o => Describe(level, o.composite, o.entity, o.brush, true), 100);
                return result;
            });
        }

        #region Arguments
        /// <summary>A material argument: 'grid' / null / "" for the default, else a level material that has to exist.</summary>
        private static string MaterialArg(Level level, string text)
        {
            if (text == null) return null;
            string trimmed = text.Trim();
            if (trimmed.Length == 0 || string.Equals(trimmed, "grid", StringComparison.OrdinalIgnoreCase) || trimmed == BrushMaterials.GridName) return null;
            Materials.Material material = McpAssets.FindMaterial(level, trimmed, "material");
            return material.Name;
        }

        private static void ApplyFaces(Level level, BrushDefinition brush, JObject faces)
        {
            if (faces == null) return;
            string[] names = brush.FaceNames;
            List<BrushFace> textures = brush.FaceTextures;
            foreach (JProperty entry in faces.Properties())
            {
                string key = entry.Name.Trim().ToLowerInvariant();
                List<int> which = new List<int>();
                if (key == "all") which.AddRange(Enumerable.Range(0, textures.Count));
                else
                {
                    int at = Array.IndexOf(names, key);
                    if (at < 0 && key == "top" && brush.Kind == BrushKind.Box) at = 2;
                    if (at < 0 && key == "bottom" && brush.Kind == BrushKind.Box) at = 3;
                    if (at < 0) throw McpError.Invalid("'" + entry.Name + "' is not a face of a " + brush.Kind.ToString().ToLowerInvariant() + ": " + string.Join(", ", names) + " or all.");
                    which.Add(at);
                }
                if (!(entry.Value is JObject settings)) throw McpError.Invalid("faces." + entry.Name + " takes {material, scale, offset, rotation}.");
                foreach (JProperty setting in settings.Properties())
                    if (setting.Name != "material" && setting.Name != "scale" && setting.Name != "offset" && setting.Name != "rotation")
                        throw McpError.Invalid("faces." + entry.Name + "." + setting.Name + " is not a setting (material, scale, offset, rotation).");
                foreach (int index in which)
                {
                    BrushFace face = textures[index];
                    if (settings["material"] != null) face.Material = settings["material"].Type == JTokenType.Null ? null : MaterialArg(level, (string)settings["material"]);
                    if (settings["scale"] != null) face.Scale = ReadPair(settings["scale"], "scale");
                    if (settings["offset"] != null) face.Offset = ReadPair(settings["offset"], "offset");
                    if (settings["rotation"] != null) face.Rotation = (float)McpValues.ReadDouble(settings["rotation"], "rotation");
                }
            }
        }

        private static Vector2 ReadPair(JToken token, string name)
        {
            if (token is JArray array && array.Count == 2)
                return new Vector2((float)McpValues.ReadDouble(array[0], name), (float)McpValues.ReadDouble(array[1], name));
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                float both = (float)McpValues.ReadDouble(token, name);
                return new Vector2(both, both);
            }
            throw McpError.Invalid("'" + name + "' takes [u, v] (or one number for both).");
        }

        /// <summary>The world origin in the brush's own space, given where it is placed in the composite (and the composite's first placement in the world).</summary>
        private static Vector3 WorldOrigin(Commands commands, Composite composite, cTransform placement) => BrushService.WorldAlignedOrigin(commands, composite, placement);
        #endregion

        public static JObject Describe(Level level, Composite composite, FunctionEntity entity, BrushDefinition brush, bool brief)
        {
            Commands commands = level.Commands;
            JObject o = new JObject()
            {
                ["composite"] = composite.name,
                ["entity"] = McpScript.Brief(commands, composite, entity),
                ["kind"] = brush.Kind.ToString().ToLowerInvariant(),
                ["size"] = McpValues.Vector(brush.Size),
                ["min"] = McpValues.Vector(brush.Min),
                ["max"] = McpValues.Vector(brush.Max),
                ["model"] = brush.Model,
                ["collision"] = brush.Collision,
            };
            if (brush.Kind == BrushKind.Plane) o["double_sided"] = brush.DoubleSided;
            if (!BrushService.HasOwnRow(level, composite, entity)) o["copy_of"] = brush.Model;
            string[] names = brush.FaceNames;
            List<BrushFace> faces = brush.FaceTextures;
            JObject faceInfo = new JObject();
            for (int i = 0; i < faces.Count && i < names.Length; i++)
            {
                JObject face = new JObject() { ["material"] = faces[i].Material ?? BrushMaterials.GridName };
                if (!brief)
                {
                    if (faces[i].Scale != Vector2.One) face["scale"] = new JArray(faces[i].Scale.X, faces[i].Scale.Y);
                    if (faces[i].Offset != Vector2.Zero) face["offset"] = new JArray(faces[i].Offset.X, faces[i].Offset.Y);
                    if (faces[i].Rotation != 0) face["rotation"] = faces[i].Rotation;
                }
                faceInfo[names[i]] = face;
            }
            o["faces"] = faceInfo;
            if (brush.WorldProxy >= 0 || brush.BallisticProxy >= 0)
                o["proxies"] = new JObject() { ["world"] = brush.WorldProxy, ["ballistic"] = brush.BallisticProxy };
            if (brush.Kind == BrushKind.Mesh && brush.Mesh != null)
            {
                o["vertex_count"] = brush.Mesh.Vertices.Count;
                o["face_count"] = brush.Mesh.Faces.Count;
                if (!brief)
                {
                    o["vertices"] = new JArray(brush.Mesh.Vertices.Take(256).Select(McpValues.Vector));
                    o["mesh_faces"] = new JArray(brush.Mesh.Faces.Take(256).Select(f => new JArray(f.Vertices)));
                }
            }
            if (!brief)
            {
                o["uv_origin"] = McpValues.Vector(brush.UvOrigin);
                if (brush.SupersededProxies.Count != 0) o["superseded_proxies"] = new JArray(brush.SupersededProxies);
                Models.CS2 model = BrushService.FindModel(level, brush.Model);
                if (model != null) o["submeshes"] = BrushModelBuilder.Submeshes(model)?.Count ?? 0;
            }
            return o;
        }
    }
}
