using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Building;
using OpenCAGE.TextureTools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// add_sky_dome turns a panorama into a sky dome for the open level, and add_outdoor_lighting lights an outdoor area with a sun,
    /// a sky fill that keeps out of rooms, and sun bounce. The picture and planning work is in Scripts\Building (Panorama, OutdoorPlanner);
    /// this is the glue to the level. Registered from McpSpatialTools.Tools(); both name their toolset ("lights"). The Sky and Outdoor
    /// Lighting window runs these same tools.
    /// </summary>
    internal static class McpBuildingTools
    {
        public const string DomeComposite = "Sky\\SkyDome";
        public const string DomeModel = "SKY\\SKYDOME";
        public const string DomeMaterial = "SKY\\SKYDOME";
        public const string DomeInstance = "SkyDome";
        /// <summary>CA_ENVIRONMENT EMISSIVE + RADIOSITY_DYNAMIC: a shipped permutation that glows from its diffuse map and keeps out of the radiosity bake (a static emissive dome makes the bake throw).</summary>
        public const string DomeMask = "0x10800000000";
        /// <summary>The same + FORCE_TO_ALPHA, drawn at priority 39 (the see-through pass, after the lighting): unlit, outside the deferred lighting and the screen-space effects that read its depth.</summary>
        public const string DomeMaskUnlit = "0x10800000040";
        public const string LightingFolder = "Lighting\\Outdoor_";
        /// <summary>The camera's far plane (ClipPlanesController's default, and the MID preset cinematic cameras use), less a margin.</summary>
        public const double FarPlane = 1000, FarMargin = 10;

        /// <summary>Set by the Sky and Outdoor Lighting window round its own calls (on its worker thread): their undo steps are the user's, not "AI: ".</summary>
        [ThreadStatic] internal static bool FromWindow;
        private static string Prefix => FromWindow ? "" : "AI: ";

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "add_sky_dome",
                Title = "Add a sky dome from a panorama",
                Description = "Give the open level a sky of its own from a panorama picture: an equirectangular (2:1) picture, a cube map as a cross (4:3 or 3:4), a strip of six faces (6:1 / 1:6), a DDS cube map, or six face pictures. " +
                    "It becomes an inside-out dome model (a sphere seen from inside, UVs by direction so there is no seam) drawn with a glowing material (CA_ENVIRONMENT EMISSIVE + RADIOSITY_DYNAMIC, out of the radiosity bake, casting no shadows; by default in the see-through pass after the lighting, so nothing shades it), placed round the play area. " +
                    "The dome has to sit inside the camera's 1000 m far plane from every camera (the far side of a dome too big or off-centre draws black), so centre and radius default to the play area: radius = 990 - the farthest the camera gets from the centre, at most 900. " +
                    "It also checks the level for what hides a sky: distance fog (any FogSetting paints the dome a flat colour: fog 'remove' deletes them), cinematic cameras on the MACRO / CLOSE clip presets (10 / 100 m far planes), ClipPlanesControllers, and scenery beyond the dome (hidden behind it). " +
                    "And it reads the picture's sun (direction, colour) and sky / ground colours: add_outdoor_lighting with sun 'sky' uses them. Calling it again updates the dome in place (picture, size, place, brightness); remove: true takes it out. preview: true only reports. " +
                    "Reaches the game after save_level build=true. The picture's middle faces +Z; turning right moves right across it.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "The panorama picture's full path (PNG, JPEG, BMP, TIFF, TGA, HDR or DDS). Leave out on a second call to keep the picture and change only placement or brightness."),
                    McpSchema.Map("faces", "Instead of 'path': six face pictures {\"right\": path, \"left\": ..., \"up\": ..., \"down\": ..., \"front\": ..., \"back\": ...} (right = +X, up = +Y, front = +Z; each as seen from inside, side faces upright, Direct3D's cube convention)."),
                    McpSchema.String("layout", "auto (default: from the picture's shape), equirect, cross, vertical_cross, strip, vertical_strip.", options: new[] { "auto", "equirect", "cross", "vertical_cross", "strip", "vertical_strip" }),
                    McpSchema.Integer("resolution", "Width of the dome's picture in pixels (height half of it): 2048, 4096 (default, or the source's own if smaller) or 8192 (four times the texture memory)."),
                    McpSchema.Vector("centre", "World [x, y, z] of the dome's middle (default: the middle of the play area at eye height; the picture's horizon sits at this height)."),
                    McpSchema.Number("radius", "Dome radius in metres (default: as big as the far plane allows from every camera, at most 900)."),
                    McpSchema.Number("yaw", "Turn the dome this many degrees (positive turns the picture's middle from +Z towards +X)."),
                    McpSchema.Number("brightness", "How bright it glows: the material's EMISSIVE_MULT (default 0.3; a bright filmed sky wants less, a dim painted one more)."),
                    McpSchema.Colour("tint", "Multiply the picture by this colour, 0-255 (default [255,255,255]; baked into the picture - the game replaces a material's own emissive tint)."),
                    McpSchema.String("below_horizon", "keep (default) the picture as it is below the horizon, or mirror: repaint it as the sky reflected in calm water, for a sea that runs out to the dome.", options: new[] { "keep", "mirror" }),
                    McpSchema.Boolean("reflections", "Also draw it in planar reflections (water, puddles; default true)."),
                    McpSchema.String("draw", "unlit (default: the see-through pass, priority 39, after the lighting - nothing lights or shades it; in the lit pass, the game's screen-space effects read the far dome's coarse depth and draw rings and stripes over the sky) or lit (the opaque, lit pass, priority 70).", options: new[] { "lit", "unlit" }),
                    McpSchema.String("fog", "What to do with the level's distance fog (FogSetting), which paints over the dome: keep (default; it is reported) or remove (delete them).", options: new[] { "keep", "remove" }),
                    McpSchema.Boolean("preview", "Only read the picture and check the level: nothing changes. Writes a small copy of the dome's picture ('preview_picture')."),
                    McpSchema.Boolean("remove", "Take the dome out of the level (its placement; the model and picture stay for a later add).")),
                Destructive = true,
                Toolset = "lights",
                Run = AddSkyDome,
            };

            yield return new McpTool()
            {
                Name = "add_outdoor_lighting",
                Title = "Light an outdoor area (sun, sky, bounce)",
                Description = "Light an outdoor area in one step - Alien has no sun light, so this builds one from the game's own lights: " +
                    "the SUN as one shadowed spot far out along the sun's direction, its cone fitted to the area and full strength over all of it, so roofs shadow it and rooms stay dark (sun 'patches' instead: shadowless square spots over the sunlit floors, for an area too big for one shadow map or a busy shadow budget); " +
                    "the SKY as unshadowed fill lights over the most open floor of every 'fill_spacing' cell, each one's reach cut so at most 'leak' of it reaches a room it cannot see (lights in this game cast no shadows unless asked, so a fill beside a room lights it through its walls), set so open ground gets 'sky_intensity'; " +
                    "and sun BOUNCE off the sunlit ground folded into each fill's colour. Rooms and roofs are read from the level's collision (raycasts: sky view, walls round, sun visibility), so geometry with no collision casts no sky shadow. " +
                    "Everything goes in the composite 'Lighting\\Outdoor_<name>', placed in the level's root: calling again with the same name replaces it, remove: true takes it out; one undo step. preview: true only plans and reports (with a top-down map). " +
                    "Warns about the 2048-lights-in-view crash and about other shadowed lights the sun competes with (4 shadow maps a frame). sun 'sky' takes the sun's direction and colours from the level's sky dome (add_sky_dome). Light colours are sRGB 0-255 as add_light takes them. Reaches the game after save_level build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.Nested("region", "The outdoor area (default the whole level's collision). " + McpRegion.Help, McpRegion.Schema),
                    McpSchema.String("name", "Names the composite and lights (default 'Main'): Lighting\\Outdoor_<name>, Outdoor_<name>_Sun, _Sky_<n>, _SunPatch_<n>."),
                    McpSchema.Any("sun", "'sky' (the sky dome's sun, default when there is a dome with one), or {\"azimuth\": degrees from +Z towards +X, \"elevation\": degrees above the horizon}, or a direction [x, y, z] towards the sun."),
                    McpSchema.Colour("sun_colour", "0-255 (default: the dome's sun, else warm white [255, 244, 222])."),
                    McpSchema.Number("sun_intensity", "The sun's intensity_multiplier (default 6)."),
                    McpSchema.String("sun_mode", "auto (default: the spot, or patches when the area holds rooms and one shadow map over it would be coarser than 18 cm a texel - the sun then creeps into rooms along their walls), spot (one shadowed far spot: walls and props get sun too), patches (shadowless squares on the sunlit floors: floors only, no shadow maps) or none.", options: new[] { "auto", "spot", "patches", "none" }),
                    McpSchema.Number("sun_distance", "Metres from the area's middle to the sun spot (default: 3 x the area's radius, 150-600)."),
                    McpSchema.Number("sun_depth_bias", "Advanced: the sun's shadow depth_bias (the LightReference default is 0.05). Light creeping into rooms along their walls is too much bias; speckled 'acne' on lit faces too little."),
                    McpSchema.Integer("sun_slope_bias", "Advanced: the sun's slope_scale_depth_bias (LightReference default 1)."),
                    McpSchema.Colour("sky_colour", "0-255 (default: the dome's sky, else [170, 196, 255])."),
                    McpSchema.Number("sky_intensity", "The sky fill on open ground (default 1.2; 0 = no sky fill)."),
                    McpSchema.Number("fill_spacing", "One fill light per cell this wide, metres (default 12)."),
                    McpSchema.Number("leak", "At most this share of a fill light reaches a room it cannot see (default 0.1)."),
                    McpSchema.Number("max_reach", "A fill light's furthest reach, metres (default 30)."),
                    McpSchema.Number("bounce", "Sun bounce off sunlit ground into the fills (default 0.5; 0 = none)."),
                    McpSchema.Colour("ground_colour", "The ground's colour for the bounce, 0-255 (default: the dome's ground, else [255, 236, 214])."),
                    McpSchema.Number("ground_albedo", "How much light the ground throws back, 0-1 (default 0.2)."),
                    McpSchema.Number("patch_step", "sun patches: floor cell size in metres (default 1, or 2 over 200 x 200 m)."),
                    McpSchema.Boolean("reflections", "Also light planar reflections (water, puddles) with these lights (default true)."),
                    McpSchema.Boolean("preview", "Only plan: counts, warnings and a top-down map ('map'); nothing changes."),
                    McpSchema.Boolean("remove", "Take this name's outdoor lighting out of the level.")),
                Destructive = true,
                Toolset = "lights",
                Run = AddOutdoorLighting,
            };
        }

        #region Helpers
        private static JObject Nested(McpCall call, string tool, JObject args)
        {
            McpTool found = McpTools.Find(tool) ?? throw new McpError(McpErrorCodes.Failed, "The " + tool + " tool is missing from this build.");
            McpCall nested = new McpCall(found, args, call.Cancel, (text, d, total) => call.Progress(text, d, total));
            object result = found.Run(nested);
            foreach (string note in nested.Notes) call.Note(note);
            if (result == null) return new JObject();
            return result as JObject ?? (JToken.FromObject(result, McpJson.Serializer) as JObject) ?? new JObject();
        }

        private static Vector3 ReadColour(McpCall call, string name, Vector3 fallback)
        {
            if (!call.Has(name)) return fallback;
            Vector3 c = McpValues.ReadVector(call.Token(name), name, null);
            if (c.X < 0 || c.Y < 0 || c.Z < 0 || c.X > 255 || c.Y > 255 || c.Z > 255) throw McpError.Invalid("'" + name + "' is [r, g, b], each 0-255.");
            return c;
        }

        private static JArray Rounded(Vector3 v, int digits = 2) => new JArray(Math.Round(v.X, digits), Math.Round(v.Y, digits), Math.Round(v.Z, digits));

        private static string WorkFolder(string what)
        {
            string folder = Path.Combine(Path.GetTempPath(), "OpenCAGE", what, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>The level's colliders' world box (no render boxes: the dome's own would swallow everything). UI thread.</summary>
        private static bool CollisionBox(McpCall call, Commands commands, Level level, McpRegion region, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            if (region.Roots.Count == 0 && region.HasClip)
            {
                min = region.ClipMin.Value;
                max = region.ClipMax.Value;
                return true;
            }
            McpCollision.Gathered gathered = McpCollision.Gather(call, commands, level, region, "solid", render: false, volumes: false, clip: false);
            foreach (McpCollision.Placed placed in gathered.Colliders)
            {
                if (placed.Record.Ghosted) continue;
                McpCollision.PlacedBox(placed, out Vector3 a, out Vector3 b);
                min = Vector3.Min(min, a);
                max = Vector3.Max(max, b);
            }
            if (min.X > max.X) return false;
            if (region.Margin > 0) { min -= new Vector3(region.Margin); max += new Vector3(region.Margin); }
            return true;
        }

        private static IEnumerable<(Composite composite, FunctionEntity entity)> AllOfType(Commands commands, FunctionType type)
        {
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null) continue;
                foreach (FunctionEntity entity in composite.GetFunctionEntitiesOfType(type))
                    yield return (composite, entity);
            }
        }

        private static float? FloatParam(Entity entity, string name) => (entity.GetParameter(name)?.content as cFloat)?.value;
        private static bool? BoolParam(Entity entity, string name) => (entity.GetParameter(name)?.content as cBool)?.value;

        /// <summary>The dome this tool made: its composite, the ModelReference in it and the instance in the root (any may be null).</summary>
        private static (Composite composite, FunctionEntity model, FunctionEntity instance) FindDome(Commands commands)
        {
            Composite composite = commands.GetComposite(DomeComposite);
            if (composite == null) return (null, null, null);
            FunctionEntity model = composite.GetFunctionEntitiesOfType(FunctionType.ModelReference).FirstOrDefault();
            Composite root = commands.EntryPoints[0];
            FunctionEntity instance = root.functions.FirstOrDefault(o => o.function == composite.shortGUID);
            return (composite, model, instance);
        }

        private static float DomeRadiusOf(FunctionEntity model)
        {
            List<RenderableElements.Element> elements = model?.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;
            float r = 0;
            if (elements != null)
                foreach (RenderableElements.Element element in elements)
                {
                    if (element?.Model == null) continue;
                    r = Math.Max(r, Math.Max(element.Model.MaxBounds.Length(), element.Model.MinBounds.Length()) / (float)Math.Sqrt(2));
                    r = Math.Max(r, Math.Max(Math.Abs(element.Model.MaxBounds.Y), Math.Abs(element.Model.MinBounds.Y)));
                }
            return r;
        }

        private static string TextureOf(McpCall call, string material)
        {
            JObject described = Nested(call, "describe_material", new JObject() { ["material"] = material });
            foreach (JToken sampler in (described["samplers"] as JArray) ?? new JArray())
                if ((string)sampler["name"] == "DIFFUSE_MAP") return (string)sampler["texture"];
            return null;
        }
        #endregion

        #region add_sky_dome
        private static RgbImage LoadPicture(string path, out RgbImage[] cube)
        {
            cube = null;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".tif" || ext == ".tiff")
            {
                try { return Panorama.LoadBitmap(path); }
                catch (Exception e) { throw new McpError("Could not read " + Path.GetFileName(path) + ": " + e.Message); }
            }
            //Anything else through texconv, as the texture import does: uncompressed 32-bit, top mip only
            byte[] dds = TextureConverter.Convert(path, Textures.TextureFormat.A8R8G8B8, 1, out string problem);
            if (dds == null) throw new McpError("Could not read " + Path.GetFileName(path) + ": " + problem);
            RgbImage[] images = Panorama.FromBgraDds(dds, out bool isCube);
            if (images == null) throw new McpError("Could not read the pixels of " + Path.GetFileName(path) + " after converting it.");
            if (isCube) { cube = images; return null; }
            return images[0];
        }

        private static RgbImage BuildEquirect(McpCall call, int resolution, out JObject source)
        {
            source = new JObject();
            RgbImage[] faces = null;
            RgbImage picture = null;
            if (call.Has("faces"))
            {
                if (call.Has("path")) throw McpError.Invalid("Give 'path' or 'faces', not both.");
                JObject map = call.Token("faces") as JObject ?? throw McpError.Invalid("'faces' is {\"right\": path, \"left\": ..., \"up\": ..., \"down\": ..., \"front\": ..., \"back\": ...}.");
                faces = new RgbImage[6];
                for (int f = 0; f < 6; f++)
                {
                    string p = (string)map[Panorama.FaceNames[f]] ?? throw McpError.Invalid("'faces' needs all six: " + string.Join(", ", Panorama.FaceNames) + ".");
                    p = McpAssets.AbsolutePath(p, "faces." + Panorama.FaceNames[f]);
                    if (!File.Exists(p)) throw new McpError("There is no file at " + p + ".");
                    faces[f] = LoadPicture(p, out RgbImage[] cube) ?? throw McpError.Invalid(Panorama.FaceNames[f] + " is a cube map itself: give it as 'path'.");
                }
                source["kind"] = "six faces";
                source["face_size"] = faces[0].Width;
            }
            else
            {
                string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
                if (!File.Exists(path)) throw new McpError("There is no file at " + path + ".");
                picture = LoadPicture(path, out faces);
                source["file"] = path;
                if (faces != null)
                {
                    source["kind"] = "DDS cube map";
                    source["face_size"] = faces[0].Width;
                }
                else
                {
                    source["size"] = new JArray(picture.Width, picture.Height);
                    string layoutArg = (call.Str("layout") ?? "auto").Trim().ToLowerInvariant();
                    PanoramaLayout layout;
                    switch (layoutArg)
                    {
                        case "equirect": layout = PanoramaLayout.Equirect; break;
                        case "cross": layout = PanoramaLayout.HorizontalCross; break;
                        case "vertical_cross": layout = PanoramaLayout.VerticalCross; break;
                        case "strip": layout = PanoramaLayout.HorizontalStrip; break;
                        case "vertical_strip": layout = PanoramaLayout.VerticalStrip; break;
                        case "auto":
                            layout = Panorama.Detect(picture.Width, picture.Height) ?? throw McpError.Invalid("A " + picture.Width + " x " + picture.Height + " picture fits no panorama layout: equirectangular is 2:1, a cross 4:3 or 3:4, a strip 6:1 or 1:6. Give 'layout' to say which it is.");
                            break;
                        default: throw McpError.Invalid("'layout' is auto, equirect, cross, vertical_cross, strip or vertical_strip.");
                    }
                    source["kind"] = layout.ToString();
                    if (layout != PanoramaLayout.Equirect)
                    {
                        try { faces = Panorama.SplitFaces(picture, layout); }
                        catch (Exception e) { throw McpError.Invalid("The picture does not split as a " + layoutArg + ": " + e.Message); }
                        picture = null;
                    }
                }
            }
            int sourceWidth = picture != null ? picture.Width : faces[0].Width * 4;
            int width = Math.Min(resolution, sourceWidth);
            width = Math.Max(256, width / 8 * 8);
            using (McpEditorTools.Heartbeat(call, "Making the dome's picture"))
                return picture != null ? Panorama.ResizeEquirect(picture, width) : Panorama.FacesToEquirect(faces, width);
        }

        private static JObject Analysis(SkyAnalysis a, float yaw)
        {
            Vector3 local = Panorama.DirectionOfAngles(a.SunAzimuth, a.SunElevation);
            Vector3 world = InstanceTransform.DirectionToWorld(new cTransform(Vector3.Zero, new Vector3(0, yaw, 0)), local);
            Panorama.AnglesOf(world, out double az, out double el);
            return new JObject()
            {
                ["sun_found"] = a.SunFound,
                ["sun"] = new JObject()
                {
                    ["azimuth"] = Math.Round(az, 1),
                    ["elevation"] = Math.Round(el, 1),
                    ["direction"] = Rounded(world, 4),
                    ["colour"] = Rounded(a.SunColour, 0),
                    ["strength"] = Math.Round(a.SunStrength, 1),
                    ["about"] = a.SunFound ? "the brightest compact spot, " + Math.Round(a.SunStrength, 1) + " x the sky's median: in the world (the dome's yaw applied)" : "no clear sun (the brightest spot is only " + Math.Round(a.SunStrength, 1) + " x the sky's median): an overcast or night sky - give add_outdoor_lighting a sun yourself",
                },
                ["sky_colour"] = Rounded(a.SkyColour, 0),
                ["ground_colour"] = Rounded(a.GroundColour, 0),
                ["horizon_colour"] = Rounded(a.HorizonColour, 0),
                ["sky_luminance"] = Math.Round(a.SkyLuminance, 3),
                ["ground_luminance"] = Math.Round(a.GroundLuminance, 3),
            };
        }

        private static object AddSkyDome(McpCall call)
        {
            bool preview = call.Bool("preview"), remove = call.Bool("remove");
            if (remove) return RemoveDome(call);
            int resolution = call.Has("resolution") ? call.Int("resolution") : 4096;
            if (resolution < 512 || resolution > 8192) throw McpError.Invalid("'resolution' takes 512 to 8192 (pixels across).");
            float yaw = (float)call.Num("yaw", 0);
            double brightness = call.Num("brightness", 0.3);
            if (brightness < 0 || brightness > 100) throw McpError.Invalid("'brightness' takes 0 to 100.");
            Vector3 tint = ReadColour(call, "tint", new Vector3(255, 255, 255));
            string below = (call.Str("below_horizon") ?? "keep").Trim().ToLowerInvariant();
            if (below != "keep" && below != "mirror") throw McpError.Invalid("'below_horizon' is keep or mirror.");
            string fog = (call.Str("fog") ?? "keep").Trim().ToLowerInvariant();
            if (fog != "keep" && fog != "remove") throw McpError.Invalid("'fog' is keep or remove.");
            bool reflections = call.Bool("reflections", true);
            string draw = (call.Str("draw") ?? "unlit").Trim().ToLowerInvariant();
            if (draw != "lit" && draw != "unlit") throw McpError.Invalid("'draw' is lit or unlit.");
            bool newPicture = call.Has("path") || call.Has("faces");

            //---- what the level holds now: the dome, the play area, what hides a sky
            Commands commands = null;
            Level level = null;
            (Composite composite, FunctionEntity model, FunctionEntity instance) dome = (null, null, null);
            Vector3 playMin = Vector3.Zero, playMax = Vector3.Zero;
            bool hasPlay = false;
            List<(Composite c, FunctionEntity e)> fogs = null, cameras = null, clips = null;
            Vector3? floorAtCentre = null;
            List<(Vector3 min, Vector3 max)> scenery = new List<(Vector3, Vector3)>();
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: !preview);
                commands = content.Level.Commands;
                level = content.Level;
                dome = FindDome(commands);
                using (McpEditorTools.Heartbeat(call, "Measuring the play area"))
                    hasPlay = CollisionBox(call, commands, level, McpRegion.Resolve(call, commands, level, "level"), out playMin, out playMax);
                fogs = AllOfType(commands, FunctionType.FogSetting).ToList();
                cameras = AllOfType(commands, FunctionType.CameraPlayAnimation).Concat(AllOfType(commands, FunctionType.CameraResource))
                    .Where(o => o.entity.GetParameter("clipping_planes_preset")?.content is cEnum preset && (preset.enumIndex == 0 || preset.enumIndex == 1)).ToList();
                clips = AllOfType(commands, FunctionType.ClipPlanesController).ToList();
                //every model's world box but the dome's own: scenery further out than the dome is hidden behind it
                using (McpEditorTools.Heartbeat(call, "Measuring the scenery"))
                {
                    McpCollision.Gathered models = McpCollision.Gather(call, commands, level, McpRegion.Resolve(call, commands, level, "level"), null, render: true, volumes: false, clip: false);
                    foreach (McpCollision.RenderBox box in models.Render)
                    {
                        if (dome.instance != null && box.Chain.Contains(dome.instance)) continue;
                        McpCollision.WorldBox(box.Min, box.Max, box.World, out Vector3 a, out Vector3 b);
                        scenery.Add((a, b));
                    }
                }
            });
            if (!newPicture && dome.composite == null)
                throw McpError.Invalid("There is no sky dome in this level yet: give 'path' (or 'faces') for its picture.");

            //---- centre and radius
            Vector3 centre;
            JObject placement = new JObject();
            if (call.Has("centre")) centre = McpValues.ReadVector(call.Token("centre"), "centre", null);
            else if (!call.Has("centre") && dome.instance != null && !newPicture)
                centre = McpEditor.UI(() => InstanceTransform.TransformOf(dome.instance)?.position ?? Vector3.Zero);
            else if (hasPlay)
            {
                Vector3 mid = (playMin + playMax) * 0.5f;
                float floorY = playMin.Y;
                McpRegion near = McpRegion.Box(new Vector3(mid.X - 2, playMin.Y - 1, mid.Z - 2), new Vector3(mid.X + 2, playMax.Y + 1, mid.Z + 2));
                McpCollision.Soup soup = McpCollision.SoupFor(call, near, "walkable");
                //the lowest surface with 2 m of room over it (the slab under a floor, or a roof's underside, is not a floor)
                List<McpCollision.Hit> hits = soup.CastAll(new Vector3(mid.X, playMax.Y + 1, mid.Z), -Vector3.UnitY, playMax.Y - playMin.Y + 2, 16);
                List<float> heights = hits.Select(o => o.Point.Y).OrderByDescending(o => o).ToList();
                List<float> floors = heights.Where((h, i) => i == 0 || heights[i - 1] - h >= 2f).ToList();
                if (floors.Count != 0) floorY = floors.Min();
                else if (heights.Count != 0) floorY = heights.Min();
                floorAtCentre = new Vector3(mid.X, floorY, mid.Z);
                centre = new Vector3(mid.X, floorY + 1.7f, mid.Z);
                placement["centre_from"] = "the play area's middle (collision " + McpRegion.Format(playMin) + " to " + McpRegion.Format(playMax) + "), 1.7 m over its lowest floor";
            }
            else
            {
                centre = Vector3.Zero;
                placement["centre_from"] = "the origin (the level has no collision to find a play area in)";
            }
            double reach = 0;
            if (hasPlay)
                for (int i = 0; i < 8; i++)
                {
                    Vector3 q = new Vector3((i & 1) == 0 ? playMin.X : playMax.X, (i & 2) == 0 ? playMin.Y : playMax.Y + 2, (i & 4) == 0 ? playMin.Z : playMax.Z);
                    reach = Math.Max(reach, Vector3.Distance(q, centre));
                }
            double radius;
            if (call.Has("radius"))
            {
                radius = call.Num("radius");
                if (radius < 20 || radius > 5000) throw McpError.Invalid("'radius' takes 20 to 5000 m.");
            }
            else if (dome.model != null && !newPicture && !call.Has("centre"))
                radius = McpEditor.UI(() => DomeRadiusOf(dome.model));
            else
                radius = Math.Max(100, Math.Min(900, FarPlane - FarMargin - reach));

            //---- checks
            JObject checks = new JObject();
            List<string> warnings = new List<string>();
            double farSide = radius + reach;
            checks["far_plane"] = new JObject()
            {
                ["camera_far_plane"] = FarPlane,
                ["farthest_camera_from_centre"] = Math.Round(reach, 1),
                ["dome_far_side_from_there"] = Math.Round(farSide, 1),
                ["ok"] = farSide < FarPlane - 1,
            };
            if (farSide >= FarPlane - 1)
                warnings.Add("FAR PLANE: from the far edge of the play area (" + Math.Round(reach) + " m from the dome's middle) the dome's far side is " + Math.Round(farSide) + " m away, past the camera's " + FarPlane + " m far plane: it draws black there. Use a smaller radius (" + Math.Round(FarPlane - FarMargin - reach) + " m fits) or move the centre.");
            if (radius < 200)
                warnings.Add("The dome is small (" + Math.Round(radius) + " m): walking about, its picture visibly moves (parallax), and scenery further out is hidden behind it. A big play area leaves little room under the far plane; light a part of it, or keep the camera near the middle.");
            //scenery beyond the dome (its farthest point from the centre)
            int beyond = 0;
            double farthest = 0;
            foreach (var box in scenery)
            {
                double d = 0;
                for (int i = 0; i < 8; i++) d = Math.Max(d, Vector3.Distance(new Vector3((i & 1) == 0 ? box.min.X : box.max.X, (i & 2) == 0 ? box.min.Y : box.max.Y, (i & 4) == 0 ? box.min.Z : box.max.Z), centre));
                farthest = Math.Max(farthest, d);
                if (d > radius) beyond++;
            }
            checks["scenery"] = new JObject() { ["models"] = scenery.Count, ["farthest_from_centre"] = Math.Round(farthest, 1), ["reaching_past_the_dome"] = beyond };
            if (beyond != 0)
                warnings.Add("SCENERY: " + beyond + " model" + (beyond == 1 ? " reaches" : "s reach") + " further out than the dome (up to " + Math.Round(farthest) + " m from its middle, the dome is " + Math.Round(radius) + " m): what is past the dome is hidden behind it. A bigger dome needs the camera nearer its middle (the far plane), or bring the scenery in.");
            if (fogs.Count != 0)
            {
                checks["fog"] = new JArray(fogs.Select(o => o.c.name + " / " + McpScript.EntityName(commands, o.c, o.e)));
                if (fog == "keep")
                    warnings.Add("FOG: " + fogs.Count + " FogSetting entit" + (fogs.Count == 1 ? "y" : "ies") + " in the level. Any distance fog paints the dome one flat colour (even thin fog with a long max_distance): fog 'remove' deletes them.");
            }
            if (cameras.Count != 0)
            {
                checks["short_cameras"] = new JArray(cameras.Take(30).Select(o => o.c.name + " / " + McpScript.EntityName(commands, o.c, o.e)));
                warnings.Add("CAMERAS: " + cameras.Count + " cinematic camera" + (cameras.Count == 1 ? " uses" : "s use") + " the MACRO or CLOSE clip preset (far plane 10 / 100 m): the dome does not draw in their shots. MID (1000 m, the default) and WIDE (5000 m) do.");
            }
            if (clips.Count != 0)
            {
                checks["clip_planes_controllers"] = new JArray(clips.Select(o => o.c.name + " / " + McpScript.EntityName(commands, o.c, o.e) + ": far_plane " + (FloatParam(o.e, "far_plane") ?? 1000f) + (BoolParam(o.e, "update_far") == true ? " (update_far)" : "")));
                foreach (var c in clips)
                    if (BoolParam(c.e, "update_far") == true && (FloatParam(c.e, "far_plane") ?? 1000f) < farSide)
                        warnings.Add("A ClipPlanesController (" + McpScript.EntityName(commands, c.c, c.e) + ") sets the far plane to " + FloatParam(c.e, "far_plane") + " m: the dome is cut off while it is active.");
            }

            //---- the picture
            RgbImage picture = null;
            JObject source = null;
            SkyAnalysis analysis = null;
            string pictureFile = null;
            string folder = WorkFolder("SkyDome");
            if (newPicture)
            {
                picture = BuildEquirect(call, resolution, out source);
                Panorama.Tint(picture, tint);
                if (below == "mirror")
                {
                    //where a sea at the floor's height meets the dome, seen from its middle
                    double floorY = floorAtCentre?.Y ?? centre.Y - 1.7;
                    double rim = Math.Asin(Math.Max(-1, Math.Min(1, (floorY - centre.Y) / radius))) * 180 / Math.PI;
                    Panorama.MirrorBelowHorizon(picture, rim, new Vector3(8, 16, 22));
                }
                using (McpEditorTools.Heartbeat(call, "Reading the sky's sun and colours"))
                    analysis = Panorama.Analyse(picture);
                pictureFile = Path.Combine(folder, "skydome.png");
                Panorama.SavePng(picture, pictureFile);
            }
            string previewFile = null;
            if (picture != null)
            {
                previewFile = Path.Combine(folder, "skydome_preview.png");
                Panorama.SavePng(Panorama.ResizeEquirect(picture, 1024), previewFile);
            }

            JObject result = new JObject()
            {
                ["centre"] = Rounded(centre),
                ["radius"] = Math.Round(radius, 1),
                ["yaw"] = yaw,
                ["brightness"] = brightness,
            };
            if (placement.Count != 0) result["placement"] = placement;
            if (source != null) result["source"] = source;
            if (picture != null) result["picture"] = new JObject() { ["size"] = new JArray(picture.Width, picture.Height), ["file"] = pictureFile };
            if (previewFile != null) result["preview_picture"] = previewFile;
            if (analysis != null) result["sky"] = Analysis(analysis, yaw);
            result["checks"] = checks;
            if (warnings.Count != 0) result["warnings"] = new JArray(warnings);
            if (preview)
            {
                result["preview"] = true;
                result["existing_dome"] = dome.composite != null;
                return result;
            }

            //---- make or update it
            bool made = false;
            if (dome.composite == null || dome.model == null)
            {
                string gltf = Panorama.WriteDomeGltf(folder, "skydome", pictureFile, radius);
                JObject imported = Nested(call, "import_model", new JObject()
                {
                    ["path"] = gltf,
                    ["name"] = DomeModel,
                    ["generate"] = new JObject() { ["skydome"] = new JObject() { ["family"] = "CA_ENVIRONMENT", ["name"] = DomeMaterial, ["always_new"] = true } },
                    ["skeleton"] = "none",
                    ["create_composite"] = true,
                    ["composite_name"] = DomeComposite,
                    ["place_in"] = "root",
                    ["position"] = McpValues.Vector(centre),
                    ["rotation"] = new JArray(0, yaw, 0),
                    ["space"] = "world",
                    ["instance_name"] = DomeInstance,
                    ["collision"] = "none",
                });
                result["imported"] = new JObject() { ["model"] = imported["model"], ["composite"] = imported["composite"] };
                made = true;
                McpEditor.UI(() => dome = FindDome(McpEditor.RequireCommands()));
                if (dome.composite == null || dome.model == null)
                    throw new McpError(McpErrorCodes.Failed, "The dome was imported but its composite " + DomeComposite + " (with a ModelReference) is not in the level.");
            }
            else
            {
                if (newPicture)
                {
                    string texture = TextureOf(call, DomeMaterial);
                    if (texture == null) throw new McpError(McpErrorCodes.Failed, "The dome's material " + DomeMaterial + " has no DIFFUSE_MAP texture to put the new picture in.");
                    Nested(call, "import_texture", new JObject() { ["path"] = pictureFile, ["replace"] = texture });
                    result["texture_replaced"] = texture;
                }
                float old = McpEditor.UI(() => DomeRadiusOf(dome.model));
                if (old > 1 && Math.Abs(radius / old - 1) > 0.002)
                {
                    Nested(call, "edit_model", new JObject() { ["model"] = DomeModel, ["action"] = "rescale", ["factor"] = radius / old });
                    result["rescaled"] = new JObject() { ["from"] = Math.Round(old, 1), ["to"] = Math.Round(radius, 1) };
                }
            }

            //The material: glowing from its picture, out of the bake
            Nested(call, "edit_material", new JObject()
            {
                ["material"] = DomeMaterial,
                ["mask"] = draw == "unlit" ? DomeMaskUnlit : DomeMask,
                ["priority"] = draw == "unlit" ? 39 : 70,
                ["parameters"] = new JObject() { ["EMISSIVE_TINT"] = new JArray(1, 1, 1, 1), ["EMISSIVE_MULT"] = brightness, ["DIFFUSE_TINT"] = new JArray(1, 1, 1, 1) },
            });
            if (made)
            {
                try { Nested(call, "edit_model", new JObject() { ["model"] = DomeModel, ["action"] = "set_render_flags", ["flags_off"] = new JArray("IS_SHADOW_CASTING") }); }
                catch (McpError e) { call.Note("The dome's shadow-casting render flag was left as it was: " + e.Message); }
            }

            //The placement and the ModelReference's own settings, and the fog, in one undo step
            List<string> removedFog = new List<string>();
            Composite root = McpEditor.UI(() => McpEditor.RequireCommands().EntryPoints[0]);
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(made ? "Sky dome settings" : "Update sky dome", root, edit =>
            {
                (Composite composite, FunctionEntity model, FunctionEntity instance) d = FindDome(edit.Commands);
                FunctionEntity instance = d.instance ?? edit.AddInstance(root, d.composite, DomeInstance);
                edit.SetParameter(root, instance, "position", new JObject() { ["position"] = McpValues.Vector(centre), ["rotation"] = new JArray(0, yaw, 0) }, allowCustom: true);
                edit.SetParameter(d.composite, d.model, "cast_shadows", false, allowCustom: true);
                edit.SetParameter(d.composite, d.model, "cast_shadows_in_torch", false, allowCustom: true);
                edit.SetParameter(d.composite, d.model, "include_in_planar_reflections", reflections, allowCustom: true);
                edit.SetParameter(d.composite, d.model, "disable_size_culling", true, allowCustom: true);
                if (fog == "remove")
                    foreach (var f in AllOfType(edit.Commands, FunctionType.FogSetting).ToList())
                    {
                        removedFog.Add(f.composite.name + " / " + McpScript.EntityName(edit.Commands, f.composite, f.entity));
                        edit.Delete(f.composite, f.entity);
                    }
            }, new McpScriptEdit.RunOptions() { LabelPrefix = Prefix, Show = false, AlsoTouches = new[] { dome.composite } });
            foreach (string note in outcome.Notes) call.Note(note);
            if (removedFog.Count != 0) result["fog_removed"] = new JArray(removedFog);
            result[made ? "made" : "updated"] = new JObject()
            {
                ["composite"] = DomeComposite,
                ["model"] = DomeModel,
                ["material"] = DomeMaterial + (draw == "unlit" ? " (mask " + DomeMaskUnlit + ": EMISSIVE + RADIOSITY_DYNAMIC + FORCE_TO_ALPHA, priority 39: unlit)" : " (mask " + DomeMask + ": EMISSIVE + RADIOSITY_DYNAMIC, priority 70)"),
                ["instance"] = "root / " + DomeInstance,
            };
            call.Note("save_level with build=true puts the dome in the game. The picture's middle faces +Z (yaw turns it); 'brightness' is its glow (the game's auto exposure adapts to it).");
            return result;
        }

        private static object RemoveDome(McpCall call)
        {
            JObject result = new JObject();
            Composite root = McpEditor.UI(() => McpEditor.RequireCommands().EntryPoints[0]);
            (Composite composite, FunctionEntity model, FunctionEntity instance) dome = McpEditor.UI(() => FindDome(McpEditor.RequireCommands()));
            if (dome.instance == null) throw new McpError(McpErrorCodes.NotFound, "There is no sky dome placed in this level (no instance of " + DomeComposite + " in the root).");
            McpScriptEdit.Run("Remove sky dome", root, edit =>
            {
                foreach (FunctionEntity placed in root.functions.Where(o => o.function == dome.composite.shortGUID).ToList())
                    edit.Delete(root, placed);
            }, new McpScriptEdit.RunOptions() { LabelPrefix = Prefix, Show = false });
            result["removed"] = "root / " + DomeInstance;
            call.Note("The dome's model, material and picture stay in the level for a later add_sky_dome; save_level build=true takes it out of the game.");
            return result;
        }
        #endregion

        #region add_outdoor_lighting
        /// <summary>The level's dome as add_outdoor_lighting needs it: where it is, how big, and its picture read for the sun (null when there is none).</summary>
        private static (Vector3 centre, float radius, SkyAnalysis sky, float yaw)? ReadDome(McpCall call)
        {
            (Composite composite, FunctionEntity model, FunctionEntity instance) dome = McpEditor.UI(() => FindDome(McpEditor.RequireCommands(forEditing: false)));
            if (dome.instance == null || dome.model == null) return null;
            cTransform world = McpEditor.UI(() => InstanceTransform.TransformOf(dome.instance));
            float radius = McpEditor.UI(() => DomeRadiusOf(dome.model));
            SkyAnalysis sky = null;
            try
            {
                string texture = TextureOf(call, DomeMaterial);
                if (texture != null)
                {
                    string png = Path.Combine(WorkFolder("SkyDome"), "dome_read.png");
                    Nested(call, "export_textures", new JObject() { ["texture"] = texture, ["path"] = png });
                    if (File.Exists(png)) sky = Panorama.Analyse(Panorama.LoadBitmap(png));
                }
            }
            catch (McpError e) { call.Note("The dome's picture could not be read for its sun: " + e.Message); }
            return (world?.position ?? Vector3.Zero, radius, sky, world?.rotation.Y ?? 0);
        }

        private static object AddOutdoorLighting(McpCall call)
        {
            bool preview = call.Bool("preview"), remove = call.Bool("remove");
            string name = (call.Str("name") ?? "Main").Trim();
            if (name.Length == 0 || name.Any(c => !(char.IsLetterOrDigit(c) || c == '_'))) throw McpError.Invalid("'name' takes letters, digits and _ only.");
            string compositePath = LightingFolder + name, prefix = "Outdoor_" + name;
            if (remove) return RemoveOutdoor(call, compositePath, prefix);

            string sunMode = (call.Str("sun_mode") ?? "auto").Trim().ToLowerInvariant();
            if (sunMode != "auto" && sunMode != "spot" && sunMode != "patches" && sunMode != "none") throw McpError.Invalid("'sun_mode' is auto, spot, patches or none.");

            //---- the dome, for the sun and the colours and to keep the lights off it
            var dome = ReadDome(call);
            OutdoorSettings s = new OutdoorSettings() { Prefix = prefix, SunMode = sunMode };
            JObject result = new JObject();
            JToken sunArg = call.Token("sun");
            bool fromSky = sunArg == null ? dome?.sky?.SunFound == true : sunArg.Type == JTokenType.String && string.Equals((string)sunArg, "sky", StringComparison.OrdinalIgnoreCase);
            Vector3? skyWorldSun = null;
            if (dome?.sky != null)
                skyWorldSun = InstanceTransform.DirectionToWorld(new cTransform(Vector3.Zero, new Vector3(0, dome.Value.yaw, 0)), Panorama.DirectionOfAngles(dome.Value.sky.SunAzimuth, dome.Value.sky.SunElevation));
            if (fromSky)
            {
                if (dome == null) throw McpError.Invalid("sun 'sky' needs a sky dome in the level (add_sky_dome): give the sun's {azimuth, elevation} instead.");
                if (dome.Value.sky == null) throw new McpError("The sky dome's picture could not be read for its sun: give the sun's {azimuth, elevation}.");
                if (!dome.Value.sky.SunFound) throw new McpError("The sky dome's picture has no clear sun (an overcast or night sky): give the sun's {azimuth, elevation}.");
                s.SunDirection = skyWorldSun.Value;
                result["sun_from"] = "the sky dome's picture";
            }
            else if (sunArg == null)
            {
                result["sun_from"] = "the default (azimuth 150, elevation 40)";
            }
            else if (sunArg is JObject angles)
            {
                double az = (double?)angles["azimuth"] ?? throw McpError.Invalid("'sun' {azimuth, elevation} needs both.");
                double el = (double?)angles["elevation"] ?? throw McpError.Invalid("'sun' {azimuth, elevation} needs both.");
                if (el <= 0 || el > 90) throw McpError.Invalid("The sun's elevation takes above 0 to 90 degrees.");
                s.SunDirection = Panorama.DirectionOfAngles(az, el);
                result["sun_from"] = "azimuth " + az + ", elevation " + el;
            }
            else if (sunArg is JArray)
            {
                Vector3 d = McpValues.ReadVector(sunArg, "sun", null);
                if (d.LengthSquared() < 1e-9f || d.Y <= 0) throw McpError.Invalid("'sun' as a direction points TOWARDS the sun, up from the horizon (y above 0).");
                s.SunDirection = Vector3.Normalize(d);
                result["sun_from"] = "the direction given";
            }
            else throw McpError.Invalid("'sun' is 'sky', {azimuth, elevation} or a direction [x, y, z].");

            SkyAnalysis sky = dome?.sky;
            s.SunColour = ReadColour(call, "sun_colour", sky != null && sky.SunFound ? sky.SunColour : new Vector3(255, 244, 222));
            s.SkyColour = ReadColour(call, "sky_colour", sky != null ? sky.SkyColour : new Vector3(170, 196, 255));
            s.GroundColour = ReadColour(call, "ground_colour", sky != null ? sky.GroundColour : new Vector3(255, 236, 214));
            s.SunIntensity = (float)call.Num("sun_intensity", 6);
            s.SkyIntensity = (float)call.Num("sky_intensity", 1.2);
            s.FillSpacing = (float)call.Num("fill_spacing", 12);
            s.Leak = (float)call.Num("leak", 0.1);
            s.MaxReach = (float)call.Num("max_reach", 30);
            s.BounceStrength = (float)call.Num("bounce", 0.5);
            s.GroundAlbedo = (float)call.Num("ground_albedo", 0.2);
            s.PatchStep = (float)call.Num("patch_step", 0);
            s.SunDistance = (float)call.Num("sun_distance", 0);
            s.Reflections = call.Bool("reflections", true);
            if (call.Has("sun_depth_bias")) s.SunDepthBias = (float)call.Num("sun_depth_bias");
            if (call.Has("sun_slope_bias")) s.SunSlopeBias = call.Int("sun_slope_bias");
            if (s.SunIntensity < 0 || s.SunIntensity > 100) throw McpError.Invalid("'sun_intensity' takes 0 to 100.");
            if (s.SkyIntensity < 0 || s.SkyIntensity > 50) throw McpError.Invalid("'sky_intensity' takes 0 to 50.");
            if (s.FillSpacing < 4 || s.FillSpacing > 100) throw McpError.Invalid("'fill_spacing' takes 4 to 100 m.");
            if (s.Leak < 0.01 || s.Leak > 1) throw McpError.Invalid("'leak' takes 0.01 to 1.");
            if (s.MaxReach < 5 || s.MaxReach > 200) throw McpError.Invalid("'max_reach' takes 5 to 200 m.");
            if (s.PatchStep != 0 && (s.PatchStep < 0.5 || s.PatchStep > 8)) throw McpError.Invalid("'patch_step' takes 0.5 to 8 m.");
            if (dome != null) { s.DomeCentre = dome.Value.centre; s.DomeRadius = dome.Value.radius; }

            //---- the area, its collision, and the lights already there
            McpRegion region = null;
            Vector3 min = Vector3.Zero, max = Vector3.Zero;
            int existing = 0, casters = 0;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: !preview);
                Commands commands = content.Level.Commands;
                region = McpRegion.Resolve(call, commands, content.Level, call.Token("region") ?? "level");
                using (McpEditorTools.Heartbeat(call, "Measuring the area"))
                    if (!CollisionBox(call, commands, content.Level, region, out min, out max))
                        throw new McpError(McpErrorCodes.NotFound, "The area (" + region.Label + ") has no collision to light: floors, roofs and walls are read from collision.");
                Composite ours = commands.GetComposite(compositePath);
                Vector3 grownMin = min - new Vector3(30), grownMax = max + new Vector3(30);
                McpPlacements walker = McpRegion.Walker(commands);
                walker.Walk(commands.EntryPoints[0], step =>
                {
                    if (step.Composite == ours) return true;
                    if (!(step.Entity is FunctionEntity f) || f.function != FunctionType.LightReference) return true;
                    cTransform w = step.World;
                    if (w == null) return true;
                    Vector3 p = w.position;
                    if (p.X < grownMin.X || p.Y < grownMin.Y || p.Z < grownMin.Z || p.X > grownMax.X || p.Y > grownMax.Y || p.Z > grownMax.Z) return true;
                    existing++;
                    if (BoolParam(f, "cast_shadow") == true) casters++;
                    return true;
                }, null, call.Cancel);
            });
            if ((max.X - min.X) * (max.Z - min.Z) > 1500 * 1500) throw McpError.Invalid("The area is " + Math.Round(max.X - min.X) + " x " + Math.Round(max.Z - min.Z) + " m: too big to light in one go. Give a smaller 'region'.");
            s.Min = min;
            s.Max = max;
            s.ExistingLights = existing;
            s.ExistingShadowCasters = casters;

            //The collision round it: what shadows the sun (along it up to 250 m) and blocks the sky
            Vector3 towardSun = Vector3.Normalize(s.SunDirection);
            Vector3 sunReach = towardSun * 250f;
            Vector3 soupMin = Vector3.Min(min, min + sunReach) - new Vector3(s.MaxReach + 5), soupMax = Vector3.Max(max, max + sunReach) + new Vector3(s.MaxReach + 5);
            soupMax.Y = Math.Min(soupMax.Y, max.Y + 260);
            McpRegion around = McpRegion.Box(soupMin, soupMax, "the area and 250 m towards the sun");
            McpCollision.Soup soup;
            using (McpEditorTools.Heartbeat(call, "Reading the collision round the area"))
                soup = McpCollision.SoupFor(call, around, "solid");
            bool[] seeThrough = new bool[soup.TriangleCount];
            for (int t = 0; t < seeThrough.Length; t++) seeThrough[t] = soup.RecordOf(t)?.SeeThrough == true;
            TriangleScene scene = new TriangleScene(soup.Vertices, soup.Faces, seeThrough);
            OutdoorPlan plan;
            using (McpEditorTools.Heartbeat(call, "Planning the lights"))
                plan = OutdoorPlanner.Plan(scene, s, text => call.Progress(text));

            string mapFile = Path.Combine(WorkFolder("OutdoorLighting"), "outdoor_" + name + "_map.png");
            using (System.Drawing.Bitmap map = OutdoorPlanner.DrawMap(plan, s))
                map.Save(mapFile, System.Drawing.Imaging.ImageFormat.Png);

            result["area"] = new JObject() { ["region"] = region.Label, ["min"] = Rounded(min), ["max"] = Rounded(max), ["collision_triangles"] = soup.TriangleCount };
            Panorama.AnglesOf(s.SunDirection, out double sunAz, out double sunEl);
            result["sun"] = new JObject() { ["azimuth"] = Math.Round(sunAz, 1), ["elevation"] = Math.Round(sunEl, 1), ["colour"] = Rounded(s.SunColour, 0), ["intensity"] = s.SunIntensity, ["mode"] = sunMode == "auto" ? (plan.AutoPatches ? "auto: patches" : plan.Lights.Any(o => o.Role == "sun") ? "auto: spot" : "auto") : sunMode };
            result["sky"] = new JObject() { ["colour"] = Rounded(s.SkyColour, 0), ["intensity"] = s.SkyIntensity, ["ground_colour"] = Rounded(s.GroundColour, 0), ["bounce"] = s.BounceStrength, ["colours_from"] = sky != null && !call.Has("sky_colour") ? "the sky dome" : "given / default" };
            result["floors"] = new JObject()
            {
                ["points"] = plan.Floors.Count,
                ["step"] = plan.FloorStep,
                ["open"] = plan.OpenPoints,
                ["enclosed"] = plan.EnclosedPoints,
                ["rooms"] = plan.Rooms,
                ["room_points"] = plan.RoomPoints,
                ["sunlit"] = plan.SunlitPoints,
            };
            result["lights"] = new JObject()
            {
                ["sun"] = plan.Lights.Count(o => o.Role == "sun"),
                ["sun_patches"] = plan.Lights.Count(o => o.Role == "patch"),
                ["sky_fills"] = plan.Lights.Count(o => o.Role == "fill"),
                ["fills_cut_short_of_rooms"] = plan.FillsCut,
                ["fills_left_out"] = plan.FillsDropped,
                ["fills_aimed_away_from_rooms"] = plan.FillsAimed,
                ["fills_raised"] = plan.FillsBoosted,
                ["total"] = plan.Lights.Count,
                ["already_round_the_area"] = existing,
                ["shadow_casters_round_the_area"] = casters,
            };
            result["rooms_sky_fill"] = new JObject() { ["median"] = Math.Round(plan.RoomFillMedian, 3), ["worst"] = Math.Round(plan.RoomFillMax, 3), ["ground_beside_rooms"] = Math.Round(plan.RingFillMedian, 3), ["about"] = "the sky fill reaching room floors (and open ground within 8 m of a room), as a share of what open ground gets" };
            PlannedLight sunLight = plan.Lights.FirstOrDefault(o => o.Role == "sun");
            if (sunLight != null)
                result["sun_spot"] = new JObject() { ["position"] = Rounded(sunLight.Position), ["cone"] = new JArray(Math.Round(sunLight.Inner, 1), Math.Round(sunLight.Outer, 1)), ["start"] = Math.Round(sunLight.Start, 1), ["end"] = Math.Round(sunLight.End, 1), ["near_dist"] = Math.Round(sunLight.NearDist, 1), ["shadow_texel_cm"] = Math.Round(plan.SunShadowTexel * 100, 1) };
            if (plan.RoomLeakers.Count != 0) result["fills_reaching_rooms"] = new JArray(plan.RoomLeakers);
            if (plan.Notes.Count != 0) result["notes"] = new JArray(plan.Notes);
            if (plan.Warnings.Count != 0) result["warnings"] = new JArray(plan.Warnings);
            result["map"] = mapFile;
            result["planned_in_seconds"] = Math.Round(plan.Seconds, 2);
            if (preview)
            {
                result["preview"] = true;
                return result;
            }
            if (plan.Lights.Count == 0) throw new McpError("Nothing to add: " + string.Join(" ", plan.Warnings.DefaultIfEmpty("no lights planned")));

            //---- write it: the composite (emptied when it is there), its lights, its placement in the root; one undo step
            Composite root = McpEditor.UI(() => McpEditor.RequireCommands().EntryPoints[0]);
            int replaced = 0;
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Outdoor lighting (" + name + ")", root, edit =>
            {
                Composite ours = edit.Commands.GetComposite(compositePath);
                if (ours == null) ours = edit.AddComposite(compositePath);
                else
                    foreach (Entity old in ours.GetEntities().ToList())
                    {
                        edit.Delete(ours, old);
                        replaced++;
                    }
                edit.UsePage(ours, "Outdoor lighting");
                foreach (PlannedLight l in plan.Lights)
                {
                    FunctionEntity light = edit.AddFunction(ours, FunctionType.LightReference, l.Name);
                    Vector3 rotation = l.Type == "SPOT" ? (l.Square ? new Vector3(90, 0, 0) : InstanceTransform.LookRotation(l.Direction)) : Vector3.Zero;
                    edit.SetParameter(ours, light, "position", new JObject() { ["position"] = McpValues.Vector(l.Position), ["rotation"] = McpValues.Vector(rotation) }, allowCustom: true);
                    void Put(string parameter, JToken value) => edit.SetParameter(ours, light, parameter, value, allowCustom: true);
                    Put("type", l.Type);
                    Put("colour", McpValues.Vector(l.Colour));
                    Put("intensity_multiplier", Math.Round(l.Intensity, 4));
                    Put("start_attenuation", Math.Round(l.Start, 3));
                    Put("end_attenuation", Math.Round(l.End, 3));
                    Put("light_on_reset", true);
                    Put("is_specular", l.Role != "fill");
                    Put("radiosity_multiplier", 1.0);
                    Put("include_in_planar_reflections", s.Reflections);
                    if (l.Type == "SPOT")
                    {
                        Put("inner_cone_angle", Math.Round(l.Inner, 3));
                        Put("outer_cone_angle", Math.Round(l.Outer, 3));
                        Put("cast_shadow", l.Shadow);
                        if (l.Square) { Put("is_square_light", true); Put("aspect_ratio", 1.0); }
                        if (l.NearDist > 0) Put("near_dist", Math.Round(l.NearDist, 2));
                        if (l.ShadowPriority != 0) Put("shadow_priority", l.ShadowPriority);
                        if (l.DepthBias != null) Put("depth_bias", Math.Round(l.DepthBias.Value, 6));
                        if (l.SlopeBias != null) Put("slope_scale_depth_bias", l.SlopeBias.Value);
                    }
                }
                if (!root.functions.Any(o => o.function == ours.shortGUID))
                {
                    FunctionEntity instance = edit.AddInstance(root, ours, prefix);
                    edit.SetParameter(root, instance, "position", new JObject() { ["position"] = new JArray(0, 0, 0), ["rotation"] = new JArray(0, 0, 0) }, allowCustom: true);
                }
            }, new McpScriptEdit.RunOptions() { LabelPrefix = Prefix, Show = false });
            foreach (string note in outcome.Notes) call.Note(note);
            result["made"] = new JObject() { ["composite"] = compositePath, ["placed"] = "root / " + prefix, ["lights"] = plan.Lights.Count, ["replaced_entities"] = replaced };
            call.Note("save_level with build=true puts the lights in the game. In game, the Debug Menu's light.shadow_budget shows whether the sun got its shadow map (casters) and how many lights a view draws (drawn_other, crash past 2048).");
            return result;
        }

        private static object RemoveOutdoor(McpCall call, string compositePath, string prefix)
        {
            Composite root = McpEditor.UI(() => McpEditor.RequireCommands().EntryPoints[0]);
            Composite ours = McpEditor.UI(() => McpEditor.RequireCommands().GetComposite(compositePath));
            if (ours == null) throw new McpError(McpErrorCodes.NotFound, "There is no outdoor lighting called that (" + compositePath + ").");
            int removed = 0;
            McpScriptEdit.Run("Remove outdoor lighting", root, edit =>
            {
                foreach (FunctionEntity placed in root.functions.Where(o => o.function == ours.shortGUID).ToList()) { edit.Delete(root, placed); removed++; }
                foreach (Entity old in ours.GetEntities().ToList()) edit.Delete(ours, old);
            }, new McpScriptEdit.RunOptions() { LabelPrefix = Prefix, Show = false, AlsoTouches = new[] { ours } });
            call.Note("save_level with build=true takes the lights out of the game.");
            return new JObject() { ["removed"] = compositePath, ["placements_removed"] = removed };
        }
        #endregion
    }
}
