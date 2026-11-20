using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Markup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>Composite markup (Scripts\Markup): the editor-only shapes the viewport draws at a composite's instances. 'viewport' toolset.</summary>
    internal static class McpMarkupTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "list_composite_markup",
                Title = "List composite markup",
                Description = "Composite markup: editor-only shapes the viewport draws at every instance of a composite to show what it is (where a job stands, how far from a wall a melee context goes), behind the 'Markup' scene filter. " +
                    "Never an entity, never in the game or a mod: OpenCAGE ships a built-in set for some archetypes, and a level can add pieces of its own to any composite (add_composite_markup). " +
                    "With composite: its built-in markup (read-only) and its own pieces, by index. Without: every composite of the level that has either, with how many times the level's scripts place it ('placed': an instance inside a composite that is itself placed many times counts once).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "One composite (path or id)."),
                    McpSchema.Limit(200, "composites"),
                    McpSchema.Offset("composites")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "viewport",
                Run = ListMarkup,
            };

            yield return new McpTool()
            {
                Name = "add_composite_markup",
                Title = "Add composite markup",
                Description = "Add a piece of markup to a composite of the open level (or replace one, with index): a shape drawn in the viewport at every instance of the composite, in its own space. Editor-only: no entity is made, " +
                    "the composite is not changed, the game never sees it; it is kept with the level (written by save_level). Shapes: " + string.Join(", ", CompositeMarkup.ShapeNames) + ". " +
                    "Box, Cylinder, Cone and Sphere stand on their origin (size = width, height, depth); Arrow lies flat from its origin along +Z (size = width, thickness, length); Ring lies flat round its origin (size = outer diameter, thickness, outer diameter); " +
                    "FrontLabel is the word FRONT standing on the floor, readable from -Z (size scales it); Mesh is a model file's triangles (mesh_file) or vertices/indices given here (size scales it). One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", McpSchema.CompositeRequired, required: true),
                    McpSchema.String("shape", "The shape.", required: true, options: CompositeMarkup.ShapeNames),
                    McpSchema.String("name", "A label for it (default the shape's name)."),
                    McpSchema.Position("position", "Where its origin goes, in the composite's space (default 0,0,0)."),
                    McpSchema.Rotation("rotation", "Its rotation."),
                    McpSchema.Vector("size", "[x, y, z] metres (or a scale, for FrontLabel and Mesh); default depends on the shape."),
                    McpSchema.Colour("colour", "Its colour (default the built-in markup's green)."),
                    McpSchema.String("mesh_file", "Mesh: a model file (FBX, OBJ, glTF...) to take the triangles from, as a model import reads it (in metres, Y up)."),
                    McpSchema.Array("vertices", "Mesh: [x, y, z, x, y, z, ...] in the piece's own space.", new JObject() { ["type"] = "number" }),
                    McpSchema.Array("indices", "Mesh: triangles as vertex index triples.", new JObject() { ["type"] = "integer" }),
                    McpSchema.Integer("index", "Replace the composite's piece at this index (from list_composite_markup) instead of adding one.")),
                Toolset = "viewport",
                Run = AddMarkup,
            };

            yield return new McpTool()
            {
                Name = "remove_composite_markup",
                Title = "Remove composite markup",
                Description = "Remove pieces of a composite's own markup (by index or name, or all of them). Built-in markup cannot be removed: switch the viewport's 'Markup' scene filter off to hide all markup. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", McpSchema.CompositeRequired, required: true),
                    McpSchema.Integer("index", "The piece's index (from list_composite_markup)."),
                    McpSchema.String("name", "Every piece with this name."),
                    McpSchema.Boolean("all", "Every piece of the composite's own.")),
                Destructive = true,
                Toolset = "viewport",
                Run = RemoveMarkup,
            };
        }

        private static object ListMarkup(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireLevel(forEditing: false).Level.Commands;
                if (call.Has("composite"))
                {
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite"));
                    return Describe(commands, composite, true);
                }

                Dictionary<ShortGuid, int> instances = InstanceCounts(commands);
                HashSet<ShortGuid> own = new HashSet<ShortGuid>(commands.Utils.GetCompositesWithMarkup());
                List<Composite> marked = commands.Entries.Where(o => o != null && (own.Contains(o.shortGUID) || CompositeMarkup.BuiltInFor(o) != null)).OrderBy(o => o.name, StringComparer.OrdinalIgnoreCase).ToList();
                JObject result = new JObject() { ["count"] = marked.Count, ["filter_on"] = UnityConnection.RenderFilters.IsSceneFilterEnabled(OpenCAGE.SceneFilterKind.Markup) };
                McpPaging.Page(call, marked, result, "composites", o =>
                {
                    JObject entry = Describe(commands, o, false);
                    entry["placed"] = instances.TryGetValue(o.shortGUID, out int n) ? n : 0;
                    return entry;
                }, 200);
                return result;
            });
        }

        private static object AddMarkup(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireLevel().Level.Commands;
                McpEditor.RequireUndoIdle();
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));

                string shapeText = call.Str("shape", required: true).Trim();
                if (!Enum.TryParse(shapeText, true, out CompositeMarkupTable.Shape shape) || !Enum.IsDefined(typeof(CompositeMarkupTable.Shape), shape))
                    throw McpError.Invalid("'shape' is one of " + string.Join(", ", CompositeMarkup.ShapeNames) + ".");

                List<CompositeMarkupTable.Markup> pieces = CompositeMarkup.Pieces(commands, composite);
                int index = call.Has("index") ? call.Int("index") : -1;
                if (call.Has("index") && (index < 0 || index >= pieces.Count))
                    throw McpError.Invalid(composite.name + " has " + pieces.Count + " piece(s) of its own; 'index' " + index + " is not one of them.");

                CompositeMarkupTable.Markup piece = CompositeMarkup.NewPiece(shape);
                if (call.Has("name"))
                {
                    piece.name = call.Str("name").Trim();
                    McpScriptEdit.RequireStorableText(piece.name, "name");
                }
                if (call.Has("position")) piece.position = CompositeMarkup.Floats(McpValues.ReadVector(call.Token("position"), "position", null));
                if (call.Has("rotation")) piece.rotation = CompositeMarkup.Floats(McpValues.ReadVector(call.Token("rotation"), "rotation", null));
                if (call.Has("size"))
                {
                    Vector3 size = McpValues.ReadVector(call.Token("size"), "size", null);
                    if (size.X <= 0 || size.Y < 0 || size.Z <= 0 || (size.Y == 0 && shape != CompositeMarkupTable.Shape.Mesh))
                        throw McpError.Invalid("'size' must be above zero on every axis.");
                    piece.size = CompositeMarkup.Floats(size);
                }
                if (call.Has("colour"))
                {
                    Vector3 colour = McpValues.ReadVector(call.Token("colour"), "colour", null);
                    piece.colour = new float[] { Clamp01(colour.X / 255f), Clamp01(colour.Y / 255f), Clamp01(colour.Z / 255f) };
                }

                if (shape == CompositeMarkupTable.Shape.Mesh)
                    ReadMesh(call, piece);
                else if (call.Has("mesh_file") || call.Has("vertices") || call.Has("indices"))
                    throw McpError.Invalid("mesh_file, vertices and indices are for shape Mesh.");

                if (index >= 0) pieces[index] = piece;
                else pieces.Add(piece);
                CompositeMarkup.SetPieces(commands, composite, pieces, "AI: " + (index >= 0 ? "Change" : "Add") + " Markup (" + piece.name + ")");

                JObject result = Describe(commands, composite, true);
                result["changed_index"] = index >= 0 ? index : pieces.Count - 1;
                return result;
            });
        }

        private static void ReadMesh(McpCall call, CompositeMarkupTable.Markup piece)
        {
            if (call.Has("mesh_file"))
            {
                if (call.Has("vertices") || call.Has("indices"))
                    throw McpError.Invalid("Give mesh_file, or vertices and indices, not both.");
                CollisionProxyImporter.MeshSource source;
                try { source = CollisionProxyImporter.FromModelFile(call.Str("mesh_file")); }
                catch (Exception e) { throw new McpError("Could not read " + call.Str("mesh_file") + ": " + e.Message); }
                SetMesh(piece, source.Positions, source.Indices);
                if (!call.Has("name")) piece.name = source.Name;
                return;
            }

            JArray vertices = call.Array("vertices"), indices = call.Array("indices");
            if (vertices == null || indices == null)
                throw McpError.Invalid("A Mesh needs mesh_file, or vertices and indices.");
            if (vertices.Count < 9 || vertices.Count % 3 != 0)
                throw McpError.Invalid("'vertices' is x, y, z after one another: a multiple of 3, at least 9.");
            if (indices.Count < 3 || indices.Count % 3 != 0)
                throw McpError.Invalid("'indices' is triangles: a multiple of 3.");
            List<Vector3> positions = new List<Vector3>();
            for (int i = 0; i < vertices.Count; i += 3)
                positions.Add(new Vector3((float)McpValues.ReadDouble(vertices[i], "vertices"), (float)McpValues.ReadDouble(vertices[i + 1], "vertices"), (float)McpValues.ReadDouble(vertices[i + 2], "vertices")));
            List<int> triangles = new List<int>();
            foreach (JToken token in indices)
            {
                int value = (int)McpValues.ReadDouble(token, "indices");
                if (value < 0 || value >= positions.Count) throw McpError.Invalid("Index " + value + " in 'indices' names no vertex (there are " + positions.Count + ").");
                triangles.Add(value);
            }
            SetMesh(piece, positions, triangles);
        }

        private static void SetMesh(CompositeMarkupTable.Markup piece, IList<Vector3> positions, IList<int> indices)
        {
            if (!CompositeMarkup.TrySetMesh(piece, positions, indices, out string problem))
                throw McpError.Invalid(problem);
        }

        private static object RemoveMarkup(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireLevel().Level.Commands;
                McpEditor.RequireUndoIdle();
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                List<CompositeMarkupTable.Markup> pieces = CompositeMarkup.Pieces(commands, composite);
                int before = pieces.Count;
                int given = (call.Has("index") ? 1 : 0) + (call.Has("name") ? 1 : 0) + (call.Bool("all") ? 1 : 0);
                if (given != 1)
                    throw McpError.Invalid("Give one of index, name or all.");
                if (call.Bool("all")) pieces.Clear();
                else if (call.Has("index"))
                {
                    int index = call.Int("index");
                    if (index < 0 || index >= pieces.Count)
                        throw McpError.Invalid(composite.name + " has " + pieces.Count + " piece(s) of its own; 'index' " + index + " is not one of them.");
                    pieces.RemoveAt(index);
                }
                else
                {
                    string name = call.Str("name");
                    pieces.RemoveAll(o => string.Equals(o.name, name, StringComparison.OrdinalIgnoreCase));
                }
                if (pieces.Count == before)
                    throw new McpError("Nothing to remove: " + composite.name + " has " + before + " piece(s) of its own" + (call.Has("name") ? ", none called '" + call.Str("name") + "'" : "") + ".");
                CompositeMarkup.SetPieces(commands, composite, pieces, "AI: Remove Markup (" + (before - pieces.Count) + ")");
                JObject result = Describe(commands, composite, true);
                result["removed"] = before - pieces.Count;
                return result;
            });
        }

        private static JObject Describe(Commands commands, Composite composite, bool full)
        {
            BuiltInMarkup.Entry builtIn = CompositeMarkup.BuiltInFor(composite);
            List<CompositeMarkupTable.Markup> pieces = CompositeMarkup.Pieces(commands, composite);
            JObject o = new JObject()
            {
                ["composite"] = composite.name,
                ["id"] = composite.shortGUID.ToByteString(),
            };
            if (builtIn != null)
            {
                MarkupMesh mesh = builtIn.Build();
                JObject b = new JObject() { ["description"] = builtIn.Description, ["triangles"] = mesh.TriangleCount };
                if (mesh.TryGetBounds(out Vector3 min, out Vector3 max))
                    b["bounds"] = new JObject() { ["min"] = McpValues.Vector(min), ["max"] = McpValues.Vector(max) };
                o["built_in"] = b;
            }
            else
                o["built_in"] = null;

            if (!full)
            {
                o["pieces"] = pieces.Count;
                return o;
            }
            JArray list = new JArray();
            for (int i = 0; i < pieces.Count; i++)
            {
                CompositeMarkupTable.Markup p = pieces[i];
                JObject piece = new JObject()
                {
                    ["index"] = i,
                    ["name"] = p.name,
                    ["shape"] = p.shape.ToString(),
                    ["position"] = McpValues.Vector(CompositeMarkup.Vec(p.position, Vector3.Zero)),
                    ["rotation"] = McpValues.Vector(CompositeMarkup.Vec(p.rotation, Vector3.Zero)),
                    ["size"] = McpValues.Vector(CompositeMarkup.Vec(p.size, Vector3.One)),
                    ["colour"] = new JArray(p.colour.Select(c => (int)Math.Round(Clamp01(c) * 255))),
                    ["triangles"] = CompositeMarkup.BuildPiece(p).TriangleCount,
                };
                list.Add(piece);
            }
            o["pieces"] = list;
            return o;
        }

        /// <summary>How many times each composite is instanced anywhere in the level's scripts.</summary>
        private static Dictionary<ShortGuid, int> InstanceCounts(Commands commands)
        {
            Dictionary<ShortGuid, int> counts = new Dictionary<ShortGuid, int>();
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null) continue;
                foreach (FunctionEntity function in composite.functions)
                {
                    if (function == null || function.function.IsFunctionType) continue;
                    counts[function.function] = (counts.TryGetValue(function.function, out int n) ? n : 0) + 1;
                }
            }
            return counts;
        }

        private static float Clamp01(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }
}
