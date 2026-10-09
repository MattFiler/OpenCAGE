using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>Lights: finding the lights in a place and changing their real controls.</summary>
    /// <remarks>
    /// A retail light is a LightReference inside a fixture composite (AYZ\Lights\Small_Strip and the like), and its colour,
    /// strength and range are usually fed by links from the fixture's pins (lgt_col, lgt_spot_IntMult...), set per placement
    /// on the instance or by aliases. Writing the LightReference's own parameter then does nothing: instancing reads the link
    /// first. These tools follow each parameter to what really sets it (<see cref="McpValueSource"/>) and write there, for one
    /// placement through an alias in the root composite, as retail does. Flicker is the fixture's LightBehaviour archetype
    /// (its LightConstantBehaviour, a LIGHT_ANIM), reached the same way.
    /// </remarks>
    internal static class McpLightTools
    {
        private const string ColourHelp = "[r, g, b] 0-255 per channel, or '#RRGGBB'";

        //The level viewer makes no lights (it draws a selected light's range, nothing lit), so capture_viewport cannot show a light edit
        private const string SeeLighting = "The viewport draws no lighting (only a selected light's range), so check a light in game: after save_level build=true, " +
            "or live over Live Link with runtime_utils push (sends the changed values), then runtime_utils call_method 'refresh' on the LightReference (a light takes new values on refresh), then runtime_utils screenshot.";

        public static IEnumerable<McpTool> Tools()
        {
            JObject region = McpRegion.Schema;
            yield return new McpTool()
            {
                Name = "list_lights",
                Title = "List lights",
                Description = "The lights in a place (each LightReference placement, world space) with what they really show: type, colour (0-255 per channel, and hex), " +
                    "intensity, range [start, end] in metres, cone [inner, outer] in degrees (spot), strip length (strip), on at level start, cast_shadow, gobo, behaviour " +
                    "(UNIFORM, FLICKER, PULSATE... - the fixture's LightBehaviour), the world direction it shines (its +Z) and, for each, what controls it: the light's own " +
                    "parameter, a pin of its fixture composite (lgt_col...) or an alias. Values follow instancing's rules (a link beats a value; the outermost alias wins). " +
                    "A whole level has thousands: results are paged and grouped by fixture composite. set_lights changes them; add_light adds one. Lights reach the game at " +
                    "save_level build=true. " + SeeLighting,
                InputSchema = McpSchema.Object(
                    McpSchema.Nested("region", "Where to look (default: the whole level). " + McpRegion.Help, region),
                    McpSchema.String("filter", "Words the light's fixture composite or entity name must contain (e.g. 'strip')."),
                    McpSchema.String("type", "Only this light type.", options: new[] { "omni", "spot", "strip" }),
                    McpSchema.Boolean("include_fx", "Include effect lights (sparks, fire, explosions under FX_Library) and deleted or template placements (default false)."),
                    McpSchema.String("detail", "'brief' (default): values and a one-line control each; 'full': each control's holder, route and the path to set it on.", options: new[] { "brief", "full" }),
                    McpSchema.Limit(50, "lights"),
                    McpSchema.Offset("lights")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListLights,
            };

            JObject lightPath = new JObject() { ["description"] = "A light (or a fixture instance, meaning every light in it): its 'ids' (or names) from the root composite, as list_lights gives them; an array or one string split on '/'." };
            yield return new McpTool()
            {
                Name = "set_lights",
                Title = "Set lights",
                Description = "Change lights' real controls as one undo step: colour, intensity (or intensity_scale to dim/brighten by a factor), range, on at level start, " +
                    "cast_shadow and behaviour (FLICKER, PULSATE, ... through the fixture's LightBehaviour). Each value is written where the game reads it (a fixture pin " +
                    "such as lgt_col, or the light's own parameter); a fixture's emissive model follows a pin by itself, and one tinted to match the light is recoloured too. " +
                    "scope 'placement' (default) changes just these placements, through aliases in the root composite (reused when one is there); 'shared' edits the " +
                    "definition, changing every placement that does not override it. A value set at run time (a behaviour's output) cannot be written: those are skipped " +
                    "with the reason. Pick lights with 'lights' (paths from list_lights) or 'region' (+ filter/type). dry_run shows the writes. Reaches the game at save_level build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.Array("lights", "Lights to change: paths from the root (list_lights' 'ids').", lightPath),
                    McpSchema.Nested("region", "Or every light in a place. " + McpRegion.Help, region),
                    McpSchema.String("filter", "With region: words the fixture composite or light name must contain."),
                    McpSchema.String("type", "With region: only this light type.", options: new[] { "omni", "spot", "strip" }),
                    McpSchema.Boolean("include_fx", "With region: include effect lights (default false)."),
                    McpSchema.Any("colour", "New colour: " + ColourHelp + "."),
                    McpSchema.Number("intensity", "New intensity multiplier (1 is the fixture's usual strength; 0 is dark)."),
                    McpSchema.Number("intensity_scale", "Multiply the current intensity by this (0.5 halves it)."),
                    McpSchema.Number("range", "New end_attenuation in metres (where the light fades to nothing); start_attenuation is pulled in if it would pass it."),
                    McpSchema.Number("range_scale", "Multiply start and end attenuation by this."),
                    McpSchema.Boolean("on", "Lit at level start (light_on_reset): false turns it off, with its emissive fitting where a pin drives both."),
                    McpSchema.Boolean("cast_shadow", "Spot lights: cast shadows."),
                    McpSchema.String("behaviour", "LightBehaviour animation: UNIFORM (steady), PULSATE, OSCILLATE, FLICKER, FLUCTUATE, FLICKER_OFF, SPARKING, BLINK. Needs a fixture with a LightBehaviour.", options: Enum.GetNames(typeof(LIGHT_ANIM)).Where(o => !o.StartsWith("UNKNOWN")).ToArray()),
                    McpSchema.Number("behaviour_frequency", "With a behaviour: how fast it animates (lgt_behaviour_freq, 1 is normal)."),
                    McpSchema.String("scope", "'placement' (default): just these placements; 'shared': the fixture's definition (every placement not overriding it).", options: new[] { "placement", "shared" }),
                    McpSchema.Boolean("dry_run", "Report what would be written, change nothing.")),
                Run = SetLights,
            };

            yield return new McpTool()
            {
                Name = "add_light",
                Title = "Add light",
                Description = "Add a light at a point as one undo step: a bare LightReference ('omni', 'spot' or 'strip'), or an instance of a fixture composite " +
                    "(e.g. 'AYZ\\Lights\\Small_Strip', which brings its model, emissive fitting and LightBehaviour, so it can flicker). A spot shines along its +Z: give " +
                    "look_at (a point) or direction, or rotation. position is world space by default ('space': 'composite' for the composite's own); attach 'ceiling' " +
                    "puts it under the collision above the point (offset metres below). colour/intensity/range/cone/cast_shadow/behaviour are set where they are read " +
                    "(a fixture's pins on the instance). Lights reach the game at save_level build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Where to put it (path or id). " + McpSchema.CompositeDefaultRoot),
                    McpSchema.Position("position", "Where the light goes", required: true),
                    McpSchema.String("space", "'world' (default): position, look_at and direction are world space; 'composite': in the composite's own space.", options: new[] { "world", "composite" }),
                    McpSchema.Integer("placement", "With space 'world' in a composite placed more than once: which placement (0-based, as get_placements lists them)."),
                    McpSchema.String("preset", "A bare light: omni (default), spot or strip.", options: new[] { "omni", "spot", "strip" }),
                    McpSchema.String("fixture", "Instead of a preset: a light fixture composite to place (list_lights' groups name them; find_composites searches)."),
                    McpSchema.Position("look_at", "A point for its +Z to face"),
                    McpSchema.Vector("direction", "A direction for its +Z to face, e.g. [0, -1, 0] straight down."),
                    McpSchema.Rotation("rotation", "Its rotation instead"),
                    McpSchema.String("attach", "'ceiling': raise it to the collision above position, less offset (default 0.1 m).", options: new[] { "ceiling" }),
                    McpSchema.Number("offset", "With attach: metres below the ceiling (default 0.1)."),
                    McpSchema.Any("colour", "Colour: " + ColourHelp + " (default warm white)."),
                    McpSchema.Number("intensity", "Intensity multiplier (default 1)."),
                    McpSchema.Number("range", "end_attenuation in metres (default 6)."),
                    McpSchema.Array("cone", "Spot: [inner, outer] cone angles in degrees (default [30, 60]).", new JObject() { ["type"] = "number" }),
                    McpSchema.Boolean("cast_shadow", "Spot: cast shadows (default false)."),
                    McpSchema.String("behaviour", "With a fixture: its LightBehaviour animation (FLICKER...).", options: Enum.GetNames(typeof(LIGHT_ANIM)).Where(o => !o.StartsWith("UNKNOWN")).ToArray()),
                    McpSchema.String("name", "The new entity's name.")),
                Run = AddLight,
            };
        }

        #region Finding lights
        private static readonly ShortGuid LightConstantBehaviour = ShortGuidUtils.Generate("LightConstantBehaviour");
        private static readonly ShortGuid BehaviourFrequency = ShortGuidUtils.Generate("lgt_behaviour_freq");
        private static readonly ShortGuid EmissiveTint = ShortGuidUtils.Generate("emissive_tint");

        /// <summary>One placed LightReference.</summary>
        private sealed class Light
        {
            public List<Entity> Chain;
            public cTransform World;
            public McpValueSource Effective;
            public bool Real;
            public bool Fx;
            public FunctionEntity Entity => (FunctionEntity)Chain[Chain.Count - 1];
            public int Level => Chain.Count - 1;
            public Composite Composite => Effective.Comps[Level];
            public string Key => string.Join("/", Chain.Select(o => o.shortGUID.ToByteString()));
            private string _type;
            public string Type
            {
                get
                {
                    if (_type != null) return _type;
                    ParameterData data = Effective.Of(Level, Entity, ShortGuids.type).Value;
                    int index = (data as cEnum)?.enumIndex ?? 0;
                    _type = Enum.IsDefined(typeof(LIGHT_TYPE), index) ? ((LIGHT_TYPE)index).ToString() : index.ToString();
                    return _type;
                }
            }
        }

        /// <summary>Whether a composite holds a LightReference somewhere below it (cached per call).</summary>
        private sealed class LightReach
        {
            private readonly Commands _commands;
            private readonly Dictionary<Composite, bool> _has = new Dictionary<Composite, bool>();
            public LightReach(Commands commands) { _commands = commands; }

            public bool Has(Composite composite)
            {
                if (_has.TryGetValue(composite, out bool known)) return known;
                _has[composite] = false;
                bool found = false;
                foreach (FunctionEntity function in composite.functions)
                {
                    if (function.function.IsFunctionType) { if (function.function == FunctionType.LightReference) { found = true; break; } continue; }
                    Composite child = _commands.GetComposite(function.function);
                    if (child != null && Has(child)) { found = true; break; }
                }
                _has[composite] = found;
                return found;
            }
        }

        private static bool IsFx(Commands commands, IList<Entity> chain)
        {
            Composite current = commands.EntryPoints[0];
            foreach (Entity entity in chain)
            {
                string name = (current?.name ?? "").Replace('/', '\\');
                if (name.IndexOf("\\FX_Library\\", StringComparison.OrdinalIgnoreCase) >= 0 || name.StartsWith("Required_Assets\\", StringComparison.OrdinalIgnoreCase) || name.IndexOf("\\LightRig_", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                current = McpScript.InstancedComposite(commands, entity);
            }
            string last = (current?.name ?? "").Replace('/', '\\');
            return last.IndexOf("\\FX_Library\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Every LightReference placement in a region (all of the level for a box, sphere or 'level'). UI thread.</summary>
        private static List<Light> Gather(McpCall call, Commands commands, McpRegion region, bool includeFx, out int skippedFx, out bool truncated)
        {
            LightReach reach = new LightReach(commands);
            McpValueSource.AliasIndex aliases = new McpValueSource.AliasIndex();
            McpPlacements walker = McpRegion.Walker(commands);
            Composite root = commands.EntryPoints[0];
            List<Light> lights = new List<Light>();
            int fx = 0;
            bool Visit(McpPlacements.Step step)
            {
                if (!(step.Entity is FunctionEntity function) || function.function != FunctionType.LightReference) return true;
                cTransform world = step.World;
                if (world == null || !region.InClip(world.position)) return true;
                List<Entity> chain = new List<Entity>(step.Chain);
                bool isFx = IsFx(commands, chain);
                bool real = step.Real;
                if (!includeFx && (isFx || !real)) { fx++; return true; }
                lights.Add(new Light() { Chain = chain, World = world, Real = real, Fx = isFx, Effective = new McpValueSource(commands, root, chain, aliases) });
                return lights.Count < 20000;
            }
            truncated = false;
            if (region.Roots.Count == 0)
            {
                walker.Walk(root, Visit, reach.Has, call.Cancel);
                truncated = walker.Truncated || lights.Count >= 20000;
            }
            else
            {
                foreach (List<Entity> chain in region.Roots)
                {
                    walker.WalkUnder(root, chain, Visit, reach.Has, call.Cancel);
                    truncated |= walker.Truncated;
                }
                truncated |= lights.Count >= 20000;
            }
            skippedFx = fx;
            //One placement reached through two roots (a zone listing a room and a light in it) counts once
            return lights.GroupBy(o => o.Key).Select(o => o.First()).OrderBy(o => McpRegion.DescribeChain(commands, o.Chain), StringComparer.OrdinalIgnoreCase).ThenBy(o => o.Key).ToList();
        }

        private static List<Light> Filter(Commands commands, List<Light> lights, string filter, string type)
        {
            string[] words = (filter ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return lights.Where(o =>
            {
                if (type != null && !string.Equals(o.Type, type, StringComparison.OrdinalIgnoreCase)) return false;
                if (words.Length == 0) return true;
                string haystack = o.Composite.name + " " + McpScript.EntityName(commands, o.Composite, o.Entity) + " " + McpRegion.DescribeChain(commands, o.Chain);
                return words.All(w => haystack.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
            }).ToList();
        }

        private static string ReadType(McpCall call)
        {
            string type = call.Str("type");
            if (type == null) return null;
            type = type.Trim().ToUpperInvariant();
            if (type != "OMNI" && type != "SPOT" && type != "STRIP")
                throw McpError.Invalid("'type' is omni, spot or strip.");
            return type;
        }
        #endregion

        #region What controls a light
        /// <summary>One property of a light: its effective value and what sets it.</summary>
        private sealed class Control
        {
            public string Property;
            public ShortGuid Parameter;
            /// <summary>Where the value comes from (the light's parameter, a pin, an alias...).</summary>
            public McpValueSource.Source Source;
            /// <summary>For a value set at run time: the pin that sets its base (writable), when one could be found.</summary>
            public McpValueSource.Source Pin;
            public string RuntimeBy;
            public Light Light;

            /// <summary>What a write changes: the base pin for a run-time value, else the source.</summary>
            public McpValueSource.Source Writable => Source == null ? null : Source.Kind == "runtime" ? Pin : Source;
            public ParameterData Value => Writable?.Value ?? Source?.Value;
        }

        private static Control Resolve(Light light, string property, ShortGuid parameter, Entity on = null)
        {
            Entity entity = on ?? light.Entity;
            McpValueSource.Source source = light.Effective.Of(light.Level, entity, parameter);
            Control control = new Control() { Property = property, Parameter = parameter, Source = source, Light = light };
            if (source.Kind == "runtime")
            {
                control.RuntimeBy = McpScript.EntityName(light.Effective.Commands, source.RuntimeComposite, source.RuntimeEntity) + " (" + McpScript.TypeName(light.Effective.Commands, source.RuntimeComposite, source.RuntimeEntity) + ")";
                DataType? type = light.Effective.Commands.Utils.GetParameterMetadata(entity, parameter, light.Composite).Item2;
                McpValueSource.Source pin = light.Effective.Base(source, type);
                if (pin != null && pin.Kind != "runtime")
                    control.Pin = pin;
            }
            return control;
        }

        /// <summary>The fixture's LightBehaviour instance driving this light (refreshing it, or feeding its intensity), if it has one.</summary>
        private static FunctionEntity BehaviourOf(Commands commands, Composite composite, FunctionEntity light)
        {
            List<FunctionEntity> behaviours = composite.functions.Where(o => !o.function.IsFunctionType && commands.GetComposite(o.function) is Composite c && c.variables.Any(v => v.name == LightConstantBehaviour)).ToList();
            if (behaviours.Count == 0) return null;
            FunctionEntity linked = behaviours.FirstOrDefault(b => b.childLinks.Any(l => l.linkedEntityID == light.shortGUID));
            if (linked != null) return linked;
            //Reached from the light's intensity through the run-time entities between them (TriggerSelect and the like)
            HashSet<ShortGuid> seen = new HashSet<ShortGuid>();
            Queue<(Entity entity, int depth)> pending = new Queue<(Entity, int)>();
            foreach (EntityConnector link in light.childLinks.Where(o => o.thisParamID == ShortGuids.intensity_multiplier))
                if (composite.GetEntityByID(link.linkedEntityID) is Entity first && seen.Add(first.shortGUID)) pending.Enqueue((first, 0));
            while (pending.Count != 0)
            {
                (Entity entity, int depth) = pending.Dequeue();
                if (entity is FunctionEntity found && behaviours.Contains(found)) return found;
                if (depth >= 3 || !(entity is FunctionEntity function) || !function.function.IsFunctionType) continue;
                foreach (EntityConnector link in entity.childLinks)
                    if (composite.GetEntityByID(link.linkedEntityID) is Entity next && seen.Add(next.shortGUID)) pending.Enqueue((next, depth + 1));
            }
            List<FunctionEntity> wired = behaviours.Where(o => o.childLinks.Count != 0).ToList();
            return wired.Count == 1 ? wired[0] : null;
        }

        private static readonly (string property, string parameter)[] LightProperties =
        {
            ("colour", "colour"), ("intensity", "intensity_multiplier"), ("start_attenuation", "start_attenuation"), ("end_attenuation", "end_attenuation"),
            ("inner_cone", "inner_cone_angle"), ("outer_cone", "outer_cone_angle"), ("strip_length", "strip_length"), ("cast_shadow", "cast_shadow"),
            ("on", "light_on_reset"), ("gobo", "gobo_texture"),
        };

        private static Dictionary<string, Control> Controls(Light light)
        {
            Dictionary<string, Control> controls = new Dictionary<string, Control>();
            foreach ((string property, string parameter) in LightProperties)
                controls[property] = Resolve(light, property, ShortGuidUtils.Generate(parameter));
            FunctionEntity behaviour = BehaviourOf(light.Effective.Commands, light.Composite, light.Entity);
            if (behaviour != null)
            {
                controls["behaviour"] = Resolve(light, "behaviour", LightConstantBehaviour, behaviour);
                controls["behaviour_frequency"] = Resolve(light, "behaviour_frequency", BehaviourFrequency, behaviour);
                controls["behaviour_deleted"] = Resolve(light, "behaviour_deleted", ShortGuids.deleted, behaviour);
            }
            return controls;
        }

        private static JToken Json(Commands commands, ParameterData data) => data == null ? JValue.CreateNull() : McpValues.ToJson(data, commands);

        private static double Number(ParameterData data, double fallback) => data is cFloat f ? f.value : data is cInteger i ? i.value : fallback;

        /// <summary>A short line saying what sets a value, for brief listings.</summary>
        private static string Brief(Light light, Control control)
        {
            McpValueSource.Source source = control.Source;
            if (source == null) return null;
            string where = Where(light, control.Writable);
            if (source.Kind == "runtime")
                return "set at run time by " + control.RuntimeBy + (control.Pin != null ? "; its base is " + where : "");
            return where;
        }

        private static string Where(Light light, McpValueSource.Source source)
        {
            if (source == null) return "nothing writable";
            Commands commands = light.Effective.Commands;
            Composite composite = light.Effective.Comps[source.Level];
            string parameter = McpScript.ParamName(source.Parameter);
            string what;
            if (ReferenceEquals(source.Target, light.Entity))
                what = "the light's " + parameter;
            else if (McpScript.InstancedComposite(commands, source.Target) is Composite placed)
                what = "pin " + parameter + " of " + McpRegion.NameOf(commands, composite, source.Target) + " (" + McpScript.CompositeLeaf(placed) + ")";
            else if (source.Target is VariableEntity)
                what = "pin " + parameter + " of the root composite";
            else
                what = parameter + " of " + McpRegion.NameOf(commands, composite, source.Target);
            switch (source.Kind)
            {
                case "alias": return what + ", overridden by alias " + McpScript.EntityName(commands, source.HolderComposite, source.Holder) + " in " + source.HolderComposite.name;
                case "pin_default": return what + " (not set: the pin's default)";
                case "default": return what + " (not set: the default)";
            }
            return what;
        }

        private static JObject Full(Light light, Control control)
        {
            McpValueSource.Source source = control.Writable;
            JObject item = new JObject() { ["value"] = Json(light.Effective.Commands, control.Value) };
            if (control.Source?.Kind == "runtime")
            {
                item["runtime"] = control.RuntimeBy;
                if (control.Pin == null) { item["writable"] = false; return item; }
            }
            if (source == null) return item;
            item["set_by"] = source.Kind;
            item["where"] = Where(light, source);
            item["set_on"] = McpRegion.ChainJson(light.Effective.Commands, light.Chain.Take(source.Level).Concat(new[] { source.Target }).ToList())["path"];
            item["parameter"] = McpScript.ParamName(source.Parameter);
            if (source.Route.Count != 0) item["route"] = new JArray(source.Route);
            return item;
        }

        private static JObject Describe(Commands commands, Light light, bool full)
        {
            Dictionary<string, Control> controls = Controls(light);
            JObject chain = McpRegion.ChainJson(commands, light.Chain);
            JObject item = new JObject()
            {
                ["path"] = chain["path"],
                ["ids"] = chain["ids"],
                ["fixture"] = light.Level == 0 ? "(root)" : light.Composite.name,
                ["light"] = McpScript.EntityName(commands, light.Composite, light.Entity),
                ["type"] = light.Type,
                ["position"] = McpCollision.V(light.World.position),
            };
            if (light.Type != "OMNI") item["forward"] = McpCollision.V(InstanceTransform.Forward(light.World.rotation));
            ParameterData colour = controls["colour"].Value;
            if (colour is cVector3 rgb)
            {
                item["colour"] = McpValues.Vector(rgb.value);
                item["hex"] = Hex(rgb.value);
            }
            item["intensity"] = Math.Round(Number(controls["intensity"].Value, 1), 3);
            item["range"] = new JArray(Math.Round(Number(controls["start_attenuation"].Value, 0), 3), Math.Round(Number(controls["end_attenuation"].Value, 0), 3));
            if (light.Type == "SPOT") item["cone"] = new JArray(Math.Round(Number(controls["inner_cone"].Value, 0), 2), Math.Round(Number(controls["outer_cone"].Value, 0), 2));
            if (light.Type == "STRIP") item["strip_length"] = Math.Round(Number(controls["strip_length"].Value, 0), 3);
            item["on"] = (controls["on"].Value as cBool)?.value ?? true;
            if (light.Type == "SPOT") item["cast_shadow"] = (controls["cast_shadow"].Value as cBool)?.value ?? false;
            if (controls["gobo"].Value is cString gobo && !string.IsNullOrEmpty(gobo.value)) item["gobo"] = gobo.value;
            if (controls.TryGetValue("behaviour", out Control behaviour))
            {
                item["behaviour"] = Json(commands, behaviour.Value);
                if ((controls["behaviour_deleted"].Value as cBool)?.value == true) item["behaviour_disabled"] = true;
            }
            if (!light.Real) item["not_placed_by_game"] = "deleted or a template";
            if (light.Fx) item["fx"] = true;

            JObject described = new JObject();
            foreach (KeyValuePair<string, Control> entry in controls)
            {
                if (entry.Key == "behaviour_deleted" || entry.Key == "behaviour_frequency" && !full) continue;
                if ((entry.Key == "inner_cone" || entry.Key == "outer_cone" || entry.Key == "cast_shadow" || entry.Key == "gobo") && light.Type != "SPOT") continue;
                if (entry.Key == "strip_length" && light.Type != "STRIP") continue;
                if (entry.Key == "gobo" && !item.ContainsKey("gobo")) continue;
                described[entry.Key] = full ? (JToken)Full(light, entry.Value) : Brief(light, entry.Value);
            }
            item["controls"] = described;
            return item;
        }

        private static string Hex(Vector3 colour)
        {
            int Channel(float v) => Math.Max(0, Math.Min(255, (int)Math.Round(v)));
            return "#" + Channel(colour.X).ToString("X2") + Channel(colour.Y).ToString("X2") + Channel(colour.Z).ToString("X2");
        }
        #endregion

        #region list_lights
        private static object ListLights(McpCall call)
        {
            string type = ReadType(call);
            bool full = string.Equals(call.Str("detail"), "full", StringComparison.OrdinalIgnoreCase);
            using (McpEditorTools.Heartbeat(call, "Finding lights"))
                return McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: false);
                    Commands commands = content.Level.Commands;
                    McpRegion region = call.Has("region") ? McpRegion.Resolve(call, commands, content.Level, call.Token("region")) : new McpRegion() { Kind = "level", Label = "the whole level", Spec = new JObject() { ["level"] = true } };
                    foreach (string note in region.Notes) call.Note(note);
                    List<Light> all = Gather(call, commands, region, call.Bool("include_fx"), out int skipped, out bool truncated);
                    List<Light> lights = Filter(commands, all, call.Str("filter"), type);

                    JObject result = new JObject() { ["region"] = region.Label, ["space"] = "world" };
                    //Grouped by fixture: a level has thousands, mostly of a few fixtures
                    List<IGrouping<Composite, Light>> groups = lights.GroupBy(o => o.Composite).OrderByDescending(o => o.Count()).ThenBy(o => o.Key.name).ToList();
                    if (groups.Count > 1)
                        result["by_fixture"] = new JArray(groups.Take(40).Select(o => new JObject()
                        {
                            ["fixture"] = o.Key == commands.EntryPoints[0] ? "(root)" : o.Key.name,
                            ["lights"] = o.Count(),
                            ["type"] = o.First().Type,
                            ["placed_in_level"] = McpAssets.PlacementCount(commands, o.Key),
                        }));
                    McpPaging.Page(call, lights, result, "lights", o => Describe(commands, o, full), 50);
                    if (skipped != 0) result["skipped_fx_or_unplaced"] = skipped;
                    if (truncated) call.Note("The walk stopped early: use a smaller region.");
                    if (lights.Count == 0)
                        call.Note(all.Count != 0 ? all.Count + " lights are there, but none matched filter/type." : skipped != 0 ? "Only effect lights or deleted/template placements are there: include_fx shows them." : "There are no lights in " + region.Label + ".");
                    return result;
                });
        }
        #endregion

        #region set_lights
        /// <summary>One value to write, for one or more lights.</summary>
        private sealed class Write
        {
            public string Key;
            public Composite Composite;      //where the written entity is (or where the alias goes)
            public List<Entity> Path;        //from that composite to the entity, for an alias; null to write the entity itself
            public Entity Entity;
            public string Parameter;
            public JToken Value;
            public string Property;
            public string Where;
            public List<string> Lights = new List<string>();
        }

        private static Write PlanWrite(Light light, McpValueSource.Source source, string property, JToken value, bool shared)
        {
            Commands commands = light.Effective.Commands;
            Composite root = light.Effective.Comps[0];
            Write write = new Write() { Property = property, Value = value, Parameter = McpScript.ParamName(source.Parameter) };
            if (shared)
            {
                write.Composite = source.HolderComposite;
                write.Entity = source.Holder;
                write.Parameter = McpScript.ParamName(source.HolderParameter);
                write.Key = "shared|" + source.HolderComposite.shortGUID.ToByteString() + "|" + source.Holder.shortGUID.ToByteString() + "|" + write.Parameter;
                write.Where = write.Parameter + " of " + McpRegion.NameOf(commands, source.HolderComposite, source.Holder) + " in " + source.HolderComposite.name;
            }
            else if (source.Level == 0)
            {
                write.Composite = root;
                write.Entity = source.Target;
                write.Key = "root|" + source.Target.shortGUID.ToByteString() + "|" + write.Parameter;
                write.Where = write.Parameter + " of " + McpRegion.NameOf(commands, root, source.Target) + " in the root composite";
            }
            else
            {
                write.Composite = root;
                write.Path = light.Chain.Take(source.Level).Concat(new[] { source.Target }).ToList();
                write.Key = "alias|" + string.Join("/", write.Path.Select(o => o.shortGUID.ToByteString())) + "|" + write.Parameter;
                write.Where = write.Parameter + " through an alias in the root composite on " + McpRegion.DescribeChain(commands, write.Path);
            }
            return write;
        }

        private static Vector3 ReadColour(JToken token, string name)
        {
            if (token.Type == JTokenType.String)
            {
                string text = ((string)token).Trim().TrimStart('#');
                if (text.Length == 6 && int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                    return new Vector3((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
                throw McpError.Invalid("'" + name + "' is " + ColourHelp + ", not '" + token + "'.");
            }
            Vector3 colour = McpValues.ReadVector(token, name, null);
            if (colour.X < 0 || colour.Y < 0 || colour.Z < 0 || colour.X > 255 || colour.Y > 255 || colour.Z > 255)
                throw McpError.Invalid("'" + name + "' channels run 0-255.");
            //No light is lit with every channel at 1 or below ([1, 1, 1] is 0-1 white, nearly black on this scale); hex is exempt
            float brightest = Math.Max(colour.X, Math.Max(colour.Y, colour.Z));
            if (brightest > 0 && brightest <= 1)
                throw McpError.Invalid("'" + name + "' looks like 0-1 values; light colours run 0-255 per channel ([255, 255, 255] is white, [255, 0, 0] red). Multiply by 255, or give '#RRGGBB'.");
            return colour;
        }

        private static object SetLights(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            string scope = (call.Str("scope") ?? "placement").Trim().ToLowerInvariant();
            if (scope != "placement" && scope != "shared") throw McpError.Invalid("'scope' is 'placement' or 'shared'.");
            bool shared = scope == "shared";
            if (call.Has("intensity") && call.Has("intensity_scale")) throw McpError.Invalid("Give intensity or intensity_scale, not both.");
            if (call.Has("range") && call.Has("range_scale")) throw McpError.Invalid("Give range or range_scale, not both.");
            Vector3? colour = call.Has("colour") ? ReadColour(call.Token("colour"), "colour") : (Vector3?)null;
            double? intensity = call.Has("intensity") ? call.Num("intensity") : (double?)null;
            double? intensityScale = call.Has("intensity_scale") ? call.Num("intensity_scale") : (double?)null;
            double? range = call.Has("range") ? call.Num("range") : (double?)null;
            double? rangeScale = call.Has("range_scale") ? call.Num("range_scale") : (double?)null;
            if (intensity < 0 || intensityScale < 0) throw McpError.Invalid("Intensity cannot be negative (0 is dark).");
            if (range <= 0 || rangeScale <= 0) throw McpError.Invalid("Range has to be above 0 m.");
            string behaviour = null;
            if (call.Has("behaviour"))
            {
                behaviour = call.Str("behaviour").Trim().ToUpperInvariant();
                if (!Enum.TryParse(behaviour, out LIGHT_ANIM anim) || anim == LIGHT_ANIM.UNKNOWN_LIGHT_ANIM || !Enum.IsDefined(typeof(LIGHT_ANIM), anim))
                    throw McpError.Invalid("'behaviour' is one of " + string.Join(", ", Enum.GetNames(typeof(LIGHT_ANIM)).Where(o => !o.StartsWith("UNKNOWN"))) + ".");
            }
            if (call.Has("behaviour_frequency") && call.Num("behaviour_frequency") <= 0) throw McpError.Invalid("'behaviour_frequency' has to be above 0.");
            bool anything = colour != null || intensity != null || intensityScale != null || range != null || rangeScale != null || call.Has("on") || call.Has("cast_shadow") || behaviour != null || call.Has("behaviour_frequency");
            if (!anything) throw McpError.Invalid("Say what to change: colour, intensity / intensity_scale, range / range_scale, on, cast_shadow, behaviour or behaviour_frequency.");
            if (call.Has("lights") == call.Has("region")) throw McpError.Invalid("Give 'lights' (paths from list_lights) or 'region' (every light in a place), one of them.");
            string type = ReadType(call);

            List<Write> writes = new List<Write>();
            JArray skipped = new JArray();
            int lightCount = 0;
            Composite focus = null;
            string label = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel();
                Commands commands = content.Level.Commands;
                Composite root = commands.EntryPoints[0];
                focus = root;
                List<Light> lights;
                if (call.Has("lights"))
                    lights = LightsFromPaths(call, commands, call.Array("lights"));
                else
                {
                    McpRegion region = McpRegion.Resolve(call, commands, content.Level, call.Token("region"));
                    foreach (string note in region.Notes) call.Note(note);
                    lights = Filter(commands, Gather(call, commands, region, call.Bool("include_fx"), out int _, out bool _), call.Str("filter"), type);
                    if (lights.Count == 0) throw new McpError(McpErrorCodes.NotFound, "There are no lights in " + region.Label + (call.Has("filter") || type != null ? " matching filter/type" : "") + " (list_lights shows what is there).");
                }
                lightCount = lights.Count;
                Dictionary<string, Write> byKey = new Dictionary<string, Write>();
                void Add(Light light, McpValueSource.Source source, string property, JToken value)
                {
                    Write write = PlanWrite(light, source, property, value, shared);
                    string lightName = McpRegion.DescribeChain(commands, light.Chain);
                    if (byKey.TryGetValue(write.Key, out Write existing))
                    {
                        if (!JToken.DeepEquals(existing.Value, value))
                            call.Note(lightName + " and " + existing.Lights[0] + " share " + existing.Where + ", so they cannot take different " + property + " values: " + existing.Value.ToString(Newtonsoft.Json.Formatting.None) + " was kept.");
                        if (!existing.Lights.Contains(lightName)) existing.Lights.Add(lightName);
                        return;
                    }
                    write.Lights.Add(lightName);
                    byKey[write.Key] = write;
                    writes.Add(write);
                }
                void Skip(Light light, string property, string why) =>
                    skipped.Add(new JObject() { ["light"] = McpRegion.DescribeChain(commands, light.Chain), ["property"] = property, ["why"] = why });

                foreach (Light light in lights)
                {
                    Dictionary<string, Control> controls = Controls(light);
                    bool Writable(string property, out McpValueSource.Source source)
                    {
                        source = null;
                        if (!controls.TryGetValue(property, out Control control))
                        {
                            Skip(light, property, "its fixture (" + light.Composite.name + ") has no LightBehaviour to animate it: place a fixture that has one (list_lights shows which have a 'behaviour') or use add_light with such a fixture");
                            return false;
                        }
                        source = control.Writable;
                        if (source == null)
                        {
                            Skip(light, property, "it is set at run time by " + control.RuntimeBy + " and no pin sets its base");
                            return false;
                        }
                        if (shared && source.Holder == null)
                        {
                            Skip(light, property, "nothing holds a value to change for every placement");
                            return false;
                        }
                        return true;
                    }

                    if (colour != null && Writable("colour", out McpValueSource.Source colourSource))
                    {
                        Add(light, colourSource, "colour", McpValues.Vector(colour.Value));
                        //A fitting tinted to match the light, by value rather than through the same pin, is recoloured with it
                        if (ReferenceEquals(colourSource.Target, light.Entity) && colourSource.Value is cVector3 old)
                            foreach (FunctionEntity fitting in light.Composite.functions.Where(o => o.function == FunctionType.ModelReference))
                            {
                                McpValueSource.Source tint = light.Effective.Of(light.Level, fitting, EmissiveTint);
                                if (tint.Kind != "runtime" && tint.Value is cVector3 tinted && Vector3.Distance(tinted.value, old.value) < 0.5f && tint.Level == light.Level)
                                    Add(light, tint, "emissive_tint", McpValues.Vector(colour.Value));
                            }
                    }
                    if ((intensity != null || intensityScale != null) && Writable("intensity", out McpValueSource.Source intensitySource))
                    {
                        double now = Number(intensitySource.Value, 1);
                        Add(light, intensitySource, "intensity", Math.Round(intensity ?? now * intensityScale.Value, 4));
                    }
                    if (range != null || rangeScale != null)
                    {
                        if (Writable("end_attenuation", out McpValueSource.Source end))
                        {
                            double endNow = Number(end.Value, 0);
                            double endNew = range ?? endNow * rangeScale.Value;
                            Add(light, end, "end_attenuation", Math.Round(endNew, 4));
                            if (Writable("start_attenuation", out McpValueSource.Source start))
                            {
                                double startNow = Number(start.Value, 0);
                                double startNew = rangeScale != null ? startNow * rangeScale.Value : Math.Min(startNow, endNew * 0.9);
                                if (Math.Abs(startNew - startNow) > 1e-6) Add(light, start, "start_attenuation", Math.Round(startNew, 4));
                            }
                        }
                    }
                    if (call.Has("on") && Writable("on", out McpValueSource.Source on))
                        Add(light, on, "on", call.Bool("on"));
                    if (call.Has("cast_shadow"))
                    {
                        if (light.Type != "SPOT") Skip(light, "cast_shadow", "only spot lights cast shadows (this is " + light.Type + ")");
                        else if (Writable("cast_shadow", out McpValueSource.Source shadow)) Add(light, shadow, "cast_shadow", call.Bool("cast_shadow"));
                    }
                    if (behaviour != null && Writable("behaviour", out McpValueSource.Source anim))
                    {
                        Add(light, anim, "behaviour", behaviour);
                        //A disabled behaviour plays nothing: switch it back on
                        if (controls.TryGetValue("behaviour_deleted", out Control deleted) && (deleted.Value as cBool)?.value == true && deleted.Writable != null)
                            Add(light, deleted.Writable, "behaviour_deleted", false);
                    }
                    if (call.Has("behaviour_frequency") && Writable("behaviour_frequency", out McpValueSource.Source frequency))
                        Add(light, frequency, "behaviour_frequency", call.Num("behaviour_frequency"));
                }
                label = "Set " + lights.Count + " light" + (lights.Count == 1 ? "" : "s");
            });

            //A pin set on an instance further out than the light reaches every light that pin feeds below it, chosen or not
            Dictionary<string, int> under = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Dictionary<string, int> counts = new Dictionary<string, int>();
                McpPlacements walker = McpRegion.Walker(commands);
                LightReach reach = new LightReach(commands);
                foreach (Write write in writes.Where(o => o.Path != null && McpScript.InstancedComposite(commands, o.Path[o.Path.Count - 1]) != null).Take(20))
                {
                    string key = string.Join("/", write.Path.Select(o => o.shortGUID.ToByteString()));
                    if (counts.ContainsKey(key)) continue;
                    int lights = 0;
                    walker.WalkUnder(commands.EntryPoints[0], write.Path, step => { if (step.Entity is FunctionEntity f && f.function == FunctionType.LightReference && step.Real) lights++; return lights < 5000; }, reach.Has, call.Cancel);
                    counts[key] = lights;
                }
                return counts;
            });
            JObject result = new JObject()
            {
                ["lights"] = lightCount,
                ["scope"] = scope,
                ["writes"] = new JArray(writes.Take(25).Select(o =>
                {
                    JObject item = new JObject()
                    {
                        ["property"] = o.Property,
                        ["value"] = o.Value,
                        ["where"] = o.Where,
                        ["lights"] = o.Lights.Count,
                    };
                    string key = o.Path == null ? null : string.Join("/", o.Path.Select(p => p.shortGUID.ToByteString()));
                    if (key != null && under.TryGetValue(key, out int below) && below > o.Lights.Count)
                        item["lights_under_it"] = below;
                    return item;
                })),
            };
            if (writes.Any(o => o.Path != null && under.TryGetValue(string.Join("/", o.Path.Select(p => p.shortGUID.ToByteString())), out int below) && below > o.Lights.Count))
                call.Note("Some values are set on a fixture's pin at an instance holding more lights than the ones chosen (lights_under_it): those it feeds change too. A light whose pin is fed from further out cannot be changed alone without editing its fixture (scope 'shared', or duplicate_composite).");
            if (writes.Count > 25)
            {
                result["writes_total"] = writes.Count;
                result["writes_by_property"] = new JObject(writes.GroupBy(o => o.Property).Select(o => new JProperty(o.Key, o.Count())));
            }
            if (skipped.Count != 0)
            {
                result["skipped"] = new JArray(skipped.Take(30));
                if (skipped.Count > 30) result["skipped_total"] = skipped.Count;
            }
            if (writes.Count == 0)
            {
                if (dryRun) { result["dry_run"] = true; return result; }
                throw new McpError(McpErrorCodes.Refused, "Nothing could be written: " + string.Join("; ", skipped.Take(5).Select(o => o["light"] + " " + o["property"] + ": " + o["why"])) + ".");
            }
            if (dryRun)
            {
                result["dry_run"] = true;
                return result;
            }

            JArray made = new JArray();
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(label, focus, edit =>
            {
                Commands commands = edit.Commands;
                Dictionary<string, AliasEntity> aliases = new Dictionary<string, AliasEntity>();
                foreach (Write write in writes)
                {
                    Composite composite = write.Composite;
                    if (!commands.Entries.Contains(composite)) throw new McpError("A composite this change needs is no longer in the level.");
                    Entity entity = write.Entity;
                    if (write.Path != null)
                    {
                        string key = string.Join("/", write.Path.Select(o => o.shortGUID.ToByteString()));
                        if (!aliases.TryGetValue(key, out AliasEntity alias))
                        {
                            ShortGuid[] ids = write.Path.Select(o => o.shortGUID).Concat(new[] { ShortGuid.Invalid }).ToArray();
                            //One alias per path: two on the same path apply in no set order when the level is built
                            alias = edit.FindOrAddAlias(composite, ids, McpRegion.NameOf(commands, McpValueSource.CompositeOf(commands, composite, write.Path), write.Path[write.Path.Count - 1]) + "_Light", out bool created);
                            if (created)
                                made.Add(new JObject() { ["alias"] = McpScript.Id(alias.shortGUID), ["on"] = McpRegion.DescribeChain(commands, write.Path) });
                            aliases[key] = alias;
                        }
                        entity = alias;
                    }
                    edit.SetParameter(composite, entity, write.Parameter, write.Value, allowCustom: true);
                }
            }, show: false);
            if (made.Count != 0) result["aliases_made"] = made;
            result["undo"] = outcome.Changed ? "One undo step: 'AI: " + outcome.Label + "'." : "Nothing changed (the values were already set).";
            result["needs_build"] = true;
            call.Note("Light changes reach the game when the level is saved with build=true (save_level). " + SeeLighting);
            if (shared)
            {
                //Counting placements walks the script: on the UI thread, which is the one editing it
                List<string> notes = McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands(forEditing: false);
                    return writes.Where(o => o.Composite != focus).GroupBy(o => o.Composite).Select(o => o.First()).Take(5)
                        .Select(o => McpAssets.SharedEditNote(commands, o.Composite, "scope 'placement' changes one placement.")).ToList();
                });
                foreach (string note in notes)
                    call.Note(note);
            }
            return result;
        }

        /// <summary>Lights by path from the root: a LightReference, or an instance meaning every light in it. UI thread.</summary>
        private static List<Light> LightsFromPaths(McpCall call, Commands commands, JArray paths)
        {
            Composite root = commands.EntryPoints[0];
            McpValueSource.AliasIndex aliases = new McpValueSource.AliasIndex();
            List<Light> lights = new List<Light>();
            McpPlacements walker = McpRegion.Walker(commands);
            LightReach reach = new LightReach(commands);
            int i = 0;
            foreach (JToken token in paths)
            {
                JToken path = token is JObject obj ? (obj["ids"] ?? obj["path"]) : token;
                List<string> steps = ReadSteps(path, "lights[" + i + "]");
                List<Entity> chain = McpRegion.ChainFromNames(commands, steps);
                Entity last = chain[chain.Count - 1];
                if (last is FunctionEntity function && function.function == FunctionType.LightReference)
                {
                    cTransform world = walker.Evaluate(root, chain).World ?? new cTransform(Vector3.Zero, Vector3.Zero);
                    lights.Add(new Light() { Chain = chain, World = world, Real = true, Effective = new McpValueSource(commands, root, chain, aliases) });
                }
                else if (McpScript.InstancedComposite(commands, last) != null)
                {
                    int before = lights.Count;
                    walker.WalkUnder(root, chain, step =>
                    {
                        if (step.Entity is FunctionEntity f && f.function == FunctionType.LightReference && step.Real)
                            lights.Add(new Light() { Chain = new List<Entity>(step.Chain), World = step.World ?? new cTransform(Vector3.Zero, Vector3.Zero), Real = true, Effective = new McpValueSource(commands, root, step.Chain, aliases) });
                        return true;
                    }, reach.Has, call.Cancel);
                    if (lights.Count == before)
                        throw new McpError(McpErrorCodes.NotFound, McpRegion.DescribeChain(commands, chain) + " holds no lights.");
                }
                else
                    throw McpError.Invalid("lights[" + i + "] leads to " + McpRegion.DescribeChain(commands, chain) + ", a " + McpScript.TypeName(commands, McpValueSource.CompositeOf(commands, root, chain), last) + ", not a LightReference or a fixture instance.");
                i++;
            }
            return lights.GroupBy(o => o.Key).Select(o => o.First()).ToList();
        }

        /// <summary>The path from a composite down to its first LightReference (through instances), or null when it has none.</summary>
        private static List<Entity> FirstLightIn(Commands commands, Composite composite, HashSet<Composite> stack)
        {
            if (!stack.Add(composite)) return null;
            FunctionEntity own = composite.functions.FirstOrDefault(o => o.function == FunctionType.LightReference);
            if (own != null) return new List<Entity>() { own };
            foreach (FunctionEntity function in composite.functions)
            {
                Composite child = McpScript.InstancedComposite(commands, function);
                if (child == null) continue;
                List<Entity> below = FirstLightIn(commands, child, stack);
                if (below != null) { below.Insert(0, function); return below; }
            }
            return null;
        }

        private static List<string> ReadSteps(JToken token, string name)
        {
            if (token is JArray array && array.Count != 0 && array.All(o => o.Type == JTokenType.String || o.Type == JTokenType.Integer))
                return array.Select(o => McpValues.ReadString(o).Trim()).ToList();
            if (token != null && token.Type == JTokenType.String && ((string)token).Trim().Length != 0)
                return ((string)token).Split(new[] { '/', '>' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).ToList();
            throw McpError.Invalid("'" + name + "' is a path from the root: entity ids or names, as list_lights' 'ids' gives them.");
        }
        #endregion

        #region add_light
        private static object AddLight(McpCall call)
        {
            string preset = call.Str("preset")?.Trim().ToLowerInvariant();
            string fixtureName = call.Str("fixture");
            if (preset != null && fixtureName != null) throw McpError.Invalid("Give preset or fixture, not both.");
            if (preset == null && fixtureName == null) preset = "omni";
            if (preset != null && preset != "omni" && preset != "spot" && preset != "strip") throw McpError.Invalid("'preset' is omni, spot or strip.");
            string space = (call.Str("space") ?? "world").Trim().ToLowerInvariant();
            if (space != "world" && space != "composite") throw McpError.Invalid("'space' is 'world' or 'composite'.");
            int facing = (call.Has("look_at") ? 1 : 0) + (call.Has("direction") ? 1 : 0) + (call.Has("rotation") ? 1 : 0);
            if (facing > 1) throw McpError.Invalid("Give one of look_at, direction or rotation.");
            Vector3 position = McpValues.ReadVector(call.Token("position"), "position", null);
            Vector3? colour = call.Has("colour") ? ReadColour(call.Token("colour"), "colour") : (Vector3?)null;
            string behaviour = call.Has("behaviour") ? call.Str("behaviour").Trim().ToUpperInvariant() : null;
            if (behaviour != null && preset != null)
                throw McpError.Invalid("A bare LightReference has no LightBehaviour to animate it: give 'fixture' (a light composite that has one, e.g. AYZ\\Lights\\Small_Strip where the level holds it - list_lights shows each fixture's behaviour).");
            if (call.Has("range") && call.Num("range") <= 0) throw McpError.Invalid("'range' has to be above 0 m.");

            Composite composite = null;
            cTransform frame = null;
            JObject frameDescribed = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel();
                Commands commands = content.Level.Commands;
                composite = McpScript.FindComposite(commands, call.Str("composite") ?? "root");
                if (space == "world")
                    frame = McpSpatialTools.FrameOf(commands, composite, call.Has("placement") ? call.Int("placement") : (int?)null, "'placement'", out frameDescribed);
            });

            //Under the ceiling: the collision straight above the point
            JObject attached = null;
            if (string.Equals(call.Str("attach"), "ceiling", StringComparison.OrdinalIgnoreCase))
            {
                Vector3 worldPoint = space == "world" ? position : InstanceTransform.PointToWorld(McpEditor.UI(() => McpSpatialTools.FrameOf(McpEditor.RequireCommands(), composite, call.Has("placement") ? call.Int("placement") : (int?)null, "'placement'", out JObject _)), position);
                McpRegion near = new McpRegion() { Kind = "sphere", Label = "within 30 m of " + McpRegion.Format(worldPoint), Centre = worldPoint, Radius = 30f, ClipMin = worldPoint - new Vector3(30f), ClipMax = worldPoint + new Vector3(30f), Spec = new JObject() { ["near"] = McpValues.Vector(worldPoint), ["radius"] = 30 } };
                McpCollision.Soup soup = McpCollision.SoupFor(call, near, "solid");
                if (!soup.CeilingOver(worldPoint, out McpCollision.Hit hit, 30f))
                    throw new McpError(McpErrorCodes.NotFound, "There is no collision above " + McpRegion.Format(worldPoint) + " within 30 m to attach to. Give the height yourself (raycast or get_bounds find ceilings).");
                float offset = (float)call.Num("offset", 0.1);
                Vector3 under = hit.Point - new Vector3(0, offset, 0);
                attached = new JObject() { ["ceiling_y"] = McpCollision.R(hit.Point.Y), ["moved_from"] = McpCollision.V(worldPoint) };
                position = space == "world" ? under : InstanceTransform.PointToLocal(McpEditor.UI(() => McpSpatialTools.FrameOf(McpEditor.RequireCommands(), composite, call.Has("placement") ? call.Int("placement") : (int?)null, "'placement'", out JObject _)), under);
            }

            //The rotation in the space the position is given in (a spot with no facing points straight down), then both into the composite
            Vector3 rotation = Vector3.Zero;
            if (call.Has("rotation")) rotation = McpValues.ReadVector(call.Token("rotation"), "rotation", null);
            else if (call.Has("look_at")) rotation = InstanceTransform.LookAt(position, McpValues.ReadVector(call.Token("look_at"), "look_at", null));
            else if (call.Has("direction")) rotation = InstanceTransform.LookRotation(McpValues.ReadVector(call.Token("direction"), "direction", null));
            else if (preset == "spot") rotation = new Vector3(90, 0, 0);
            if (call.Has("look_at") && Vector3.Distance(position, McpValues.ReadVector(call.Token("look_at"), "look_at", null)) < 1e-4f)
                throw McpError.Invalid("'look_at' is the light's own position, so it gives no direction.");
            Vector3 worldPosition = position;
            cTransform placed = new cTransform(position, rotation);
            if (space == "world" && frame != null)
                placed = InstanceTransform.ToLocal(frame, placed);
            Vector3 cone = new Vector3(30, 60, 0);
            if (call.Has("cone"))
            {
                JArray angles = call.Array("cone");
                if (angles.Count != 2) throw McpError.Invalid("'cone' is [inner, outer] degrees.");
                cone = new Vector3((float)McpValues.ReadDouble(angles[0], "cone[0]"), (float)McpValues.ReadDouble(angles[1], "cone[1]"), 0);
                if (cone.X <= 0 || cone.Y <= 0 || cone.X > cone.Y || cone.Y > 179) throw McpError.Invalid("'cone' is [inner, outer] degrees with 0 < inner <= outer < 180.");
            }

            JObject made = null;
            JArray set = new JArray();
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Add light", composite, edit =>
            {
                Commands commands = edit.Commands;
                JObject transform = new JObject() { ["position"] = McpValues.Vector(placed.position), ["rotation"] = McpValues.Vector(placed.rotation) };
                if (preset != null)
                {
                    FunctionEntity light = edit.AddFunction(composite, FunctionType.LightReference, call.Str("name") ?? (preset == "omni" ? "Light_Omni" : preset == "spot" ? "Light_Spot" : "Light_Strip"));
                    edit.SetParameter(composite, light, "position", transform, allowCustom: true);
                    void Put(string parameter, JToken value)
                    {
                        edit.SetParameter(composite, light, parameter, value, allowCustom: true);
                        set.Add(parameter);
                    }
                    Put("type", preset.ToUpperInvariant());
                    Put("colour", McpValues.Vector(colour ?? new Vector3(255, 236, 214)));
                    Put("intensity_multiplier", call.Num("intensity", 1));
                    double end = call.Num("range", 6);
                    Put("end_attenuation", end);
                    Put("start_attenuation", Math.Min(0.5, end * 0.5));
                    Put("light_on_reset", true);
                    Put("is_specular", true);
                    Put("radiosity_multiplier", 1.0);
                    if (preset == "spot")
                    {
                        Put("inner_cone_angle", cone.X);
                        Put("outer_cone_angle", cone.Y);
                        Put("cast_shadow", call.Bool("cast_shadow"));
                    }
                    else if (preset == "strip")
                        Put("strip_length", 1.0);
                    made = McpScript.Brief(commands, composite, light);
                    return;
                }

                //A fixture: its pins on the new instance, found as list_lights finds a placed one's
                Composite fixture = McpScript.FindComposite(commands, fixtureName);
                List<Entity> inside = FirstLightIn(commands, fixture, new HashSet<Composite>());
                if (inside == null)
                    throw McpError.Invalid(fixture.name + " holds no LightReference, so it is not a light fixture. list_lights groups placed lights by fixture.");
                FunctionEntity instance = edit.AddInstance(composite, fixture, call.Str("name"));
                edit.SetParameter(composite, instance, "position", transform, allowCustom: true);
                made = McpScript.Brief(commands, composite, instance);
                List<Entity> lightChain = new List<Entity>() { instance };
                lightChain.AddRange(inside);
                Light fixtureLight = new Light() { Chain = lightChain, World = placed, Real = true, Effective = new McpValueSource(commands, composite, lightChain, new McpValueSource.AliasIndex()) };
                Dictionary<string, Control> controls = Controls(fixtureLight);
                Dictionary<string, AliasEntity> aliases = new Dictionary<string, AliasEntity>();
                void Set(string property, JToken value)
                {
                    if (!controls.TryGetValue(property, out Control control) || control.Writable == null)
                    {
                        call.Note(fixture.name + " has no writable " + property + (property.StartsWith("behaviour") ? " (no LightBehaviour)" : "") + "; it was left as the fixture sets it.");
                        return;
                    }
                    McpValueSource.Source source = control.Writable;
                    Entity target = source.Target;
                    if (source.Level == 0)
                        edit.SetParameter(composite, target, McpScript.ParamName(source.Parameter), value, allowCustom: true);
                    else
                    {
                        List<ShortGuid> path = fixtureLight.Chain.Take(source.Level).Concat(new[] { target }).Select(o => o.shortGUID).ToList();
                        string key = string.Join("/", path.Select(o => o.ToByteString()));
                        if (!aliases.TryGetValue(key, out AliasEntity alias))
                            aliases[key] = alias = edit.AddAlias(composite, path.Concat(new[] { ShortGuid.Invalid }).ToArray(), McpRegion.NameOf(commands, fixtureLight.Effective.Comps[source.Level], target) + "_Light");
                        edit.SetParameter(composite, alias, McpScript.ParamName(source.Parameter), value, allowCustom: true);
                    }
                    set.Add(property + " (" + Where(fixtureLight, source) + ")");
                }
                if (colour != null) Set("colour", McpValues.Vector(colour.Value));
                if (call.Has("intensity")) Set("intensity", call.Num("intensity"));
                if (call.Has("range")) Set("end_attenuation", call.Num("range"));
                if (call.Has("cast_shadow")) Set("cast_shadow", call.Bool("cast_shadow"));
                if (call.Has("cone"))
                {
                    Set("inner_cone", cone.X);
                    Set("outer_cone", cone.Y);
                }
                if (behaviour != null) Set("behaviour", behaviour);
                int count = fixture.functions.Count(o => o.function == FunctionType.LightReference);
                if (count > 1) call.Note(fixture.name + " holds " + count + " LightReferences; the values were set through the first one's controls (pins usually feed them all): list_lights shows each.");
            });
            bool inRoot = McpEditor.UI(() => composite == McpEditor.RequireCommands().EntryPoints[0]);
            JObject result = new JObject()
            {
                ["created"] = made,
                ["composite"] = composite.name,
                ["position"] = McpValues.Vector(placed.position),
                ["rotation"] = McpValues.Vector(placed.rotation),
                ["space"] = inRoot ? "world" : "composite",
            };
            if (space == "world" && !inRoot)
            {
                result["world_position"] = McpValues.Vector(worldPosition);
                result["frame"] = frameDescribed;
            }
            if (attached != null) result["attached"] = attached;
            if (set.Count != 0) result["set"] = set;
            result["needs_build"] = true;
            result["undo"] = "One undo step: 'AI: " + outcome.Label + "'.";
            call.Note("The light reaches the game when the level is saved with build=true; list_lights reads it back. " + SeeLighting);
            return result;
        }
        #endregion
    }

    /// <summary>
    /// The value a parameter of one placed entity really has, as instancing resolves it, and what to write to change it.
    /// </summary>
    /// <remarks>
    /// <para>A placement is a chain of entities from a start composite (usually the level's root): each an instance but the
    /// last. For a parameter: links into it win over values, the last link winning; links on the entity come first, then
    /// links that aliases add, innermost first (so the outermost alias's link wins). A link to a composite variable is followed
    /// to the value the placing instance passes in (its own links, then values), else the variable's default. With no link, an
    /// alias's value wins (the outermost one), then the entity's own value, then the type's default. A link from anything else
    /// is set at run time (a behaviour's output, a TriggerSelect...).</para>
    /// <para>To change it for one placement, write the <see cref="Source.Target"/>'s parameter through an alias in the start
    /// composite with the path down to it (or straight on it when it is in the start composite); to change it everywhere,
    /// write the <see cref="Source.Holder"/>.</para>
    /// </remarks>
    internal sealed class McpValueSource
    {
        public readonly Commands Commands;
        /// <summary>Comps[0] is the start; Chain[i] is in Comps[i]; Comps[i + 1] is what Chain[i] instances.</summary>
        public readonly List<Composite> Comps = new List<Composite>();
        public readonly List<Entity> Chain;
        private readonly AliasIndex _aliases;

        /// <summary>Aliases by the first id of their path, per composite (shared by the placements of one call).</summary>
        public sealed class AliasIndex
        {
            private readonly Dictionary<Composite, Dictionary<ShortGuid, List<AliasEntity>>> _byFirst = new Dictionary<Composite, Dictionary<ShortGuid, List<AliasEntity>>>();

            public List<AliasEntity> Starting(Composite composite, ShortGuid first)
            {
                if (!_byFirst.TryGetValue(composite, out Dictionary<ShortGuid, List<AliasEntity>> index))
                {
                    index = new Dictionary<ShortGuid, List<AliasEntity>>();
                    foreach (AliasEntity alias in composite.aliases)
                    {
                        ShortGuid[] path = alias.alias?.path;
                        if (path == null || path.Length == 0) continue;
                        if (!index.TryGetValue(path[0], out List<AliasEntity> list)) index[path[0]] = list = new List<AliasEntity>();
                        list.Add(alias);
                    }
                    _byFirst[composite] = index;
                }
                return index.TryGetValue(first, out List<AliasEntity> found) ? found : None;
            }

            private static readonly List<AliasEntity> None = new List<AliasEntity>();
        }

        /// <summary>Where a value comes from.</summary>
        public sealed class Source
        {
            /// <summary>value (set on the entity), alias (an alias overrides it), default (not set), pin_default (a pin no one sets), runtime (a link from an entity that sets it while the game runs).</summary>
            public string Kind;
            public ParameterData Value;
            /// <summary>The level in the chain of the entity to write for one placement, that entity, and its parameter.</summary>
            public int Level;
            public Entity Target;
            public ShortGuid Parameter;
            /// <summary>What holds the value now (the entity, an alias, a variable's default), where, and under which parameter: written to change it everywhere.</summary>
            public Entity Holder;
            public Composite HolderComposite;
            public ShortGuid HolderParameter;
            /// <summary>For runtime: the entity whose output feeds it, where it is, and its level.</summary>
            public Entity RuntimeEntity;
            public Composite RuntimeComposite;
            public int RuntimeLevel;
            public ShortGuid RuntimeParameter;
            public List<string> Route = new List<string>();
        }

        public McpValueSource(Commands commands, Composite start, IList<Entity> chain, AliasIndex aliases = null)
        {
            Commands = commands;
            Chain = new List<Entity>(chain);
            _aliases = aliases ?? new AliasIndex();
            Comps.Add(start);
            for (int i = 0; i + 1 < Chain.Count; i++)
            {
                Composite next = McpScript.InstancedComposite(commands, Chain[i]);
                if (next == null) throw new McpError(McpScript.Id(Chain[i].shortGUID) + " is not a composite instance, so the path cannot go through it.");
                Comps.Add(next);
            }
        }

        /// <summary>Whether an alias's stored path (ending in Invalid, or not) is these ids.</summary>
        public static bool SamePath(ShortGuid[] stored, IList<ShortGuid> ids)
        {
            if (stored == null) return false;
            int length = stored.Length > 0 && stored[stored.Length - 1] == ShortGuid.Invalid ? stored.Length - 1 : stored.Length;
            if (length != ids.Count) return false;
            for (int i = 0; i < length; i++)
                if (stored[i] != ids[i]) return false;
            return true;
        }

        /// <summary>The composite the last entity of a path from <paramref name="start"/> is in.</summary>
        public static Composite CompositeOf(Commands commands, Composite start, IList<Entity> path)
        {
            Composite current = start;
            for (int i = 0; i + 1 < path.Count && current != null; i++)
                current = McpScript.InstancedComposite(commands, path[i]);
            return current;
        }

        /// <summary>
        /// The value of <paramref name="parameter"/> on <paramref name="entity"/>, an entity of Comps[level] reached through
        /// Chain[0..level-1] (Chain[level] itself, or another entity of that composite).
        /// </summary>
        public Source Of(int level, Entity entity, ShortGuid parameter) => Of(level, entity, parameter, 0);

        private Source Of(int level, Entity entity, ShortGuid parameter, int depth)
        {
            if (depth > 40) return new Source() { Kind = "runtime", Level = level, Target = entity, Parameter = parameter, Route = { "gave up following links (a loop?)" } };
            Composite composite = Comps[level];
            List<(int level, Composite owner, EntityConnector link)> links = new List<(int, Composite, EntityConnector)>();
            foreach (EntityConnector link in entity.childLinks)
                if (link.thisParamID == parameter && composite.GetEntityByID(link.linkedEntityID) != null)
                    links.Add((level, composite, link));
            VariableEntity variable = entity as VariableEntity;
            if (variable != null && level > 0)
            {
                //A pin: the placing instance's links for it come after the variable's own (and so win)
                Entity instance = Chain[level - 1];
                foreach (EntityConnector link in instance.childLinks)
                    if (link.thisParamID == variable.name && Comps[level - 1].GetEntityByID(link.linkedEntityID) != null)
                        links.Add((level - 1, Comps[level - 1], link));
                links.AddRange(AliasLinks(level - 1, instance, variable.name));
            }
            else if (variable == null)
                links.AddRange(AliasLinks(level, entity, parameter));

            if (links.Count != 0)
            {
                (int at, Composite owner, EntityConnector link) = links[links.Count - 1];
                Entity source = owner.GetEntityByID(link.linkedEntityID);
                if (source is VariableEntity pin)
                {
                    Source found = Of(at, pin, pin.name, depth + 1);
                    found.Route.Insert(0, McpScript.ParamName(parameter) + " is linked to pin " + McpScript.ParamName(pin.name) + " of " + owner.name);
                    return found;
                }
                return new Source()
                {
                    Kind = "runtime",
                    Value = source.GetParameter(link.linkedParamID)?.content,
                    Level = level,
                    Target = entity,
                    Parameter = parameter,
                    RuntimeEntity = source,
                    RuntimeComposite = owner,
                    RuntimeLevel = at,
                    RuntimeParameter = link.linkedParamID,
                    Route = { McpScript.ParamName(parameter) + " is fed while the game runs by " + McpScript.EntityName(Commands, owner, source) + "." + McpScript.ParamName(link.linkedParamID) },
                };
            }

            if (variable != null)
            {
                if (level > 0)
                {
                    Source passed = Values(level - 1, Chain[level - 1], variable.name);
                    if (passed != null) return passed;
                    return new Source()
                    {
                        Kind = "pin_default",
                        Value = variable.GetParameter(variable.name)?.content,
                        Level = level - 1,
                        Target = Chain[level - 1],
                        Parameter = variable.name,
                        Holder = variable,
                        HolderComposite = composite,
                        HolderParameter = variable.name,
                    };
                }
                return new Source() { Kind = "pin_default", Value = variable.GetParameter(variable.name)?.content, Level = 0, Target = variable, Parameter = variable.name, Holder = variable, HolderComposite = composite, HolderParameter = variable.name };
            }

            Source own = Values(level, entity, parameter);
            if (own != null) return own;
            ParameterData standard = null;
            try { standard = Commands.Utils.CreateDefaultParameterData(entity, composite, parameter); } catch { }
            return new Source() { Kind = "default", Value = standard, Level = level, Target = entity, Parameter = parameter, Holder = entity, HolderComposite = composite, HolderParameter = parameter };
        }

        /// <summary>A value an alias sets (the outermost wins) or the entity's own; null when neither.</summary>
        private Source Values(int level, Entity entity, ShortGuid parameter)
        {
            for (int j = 0; j < level; j++)
                foreach (AliasEntity alias in Matching(j, level, entity))
                {
                    ParameterData value = alias.GetParameter(parameter)?.content;
                    if (value == null) continue;
                    return new Source() { Kind = "alias", Value = value, Level = level, Target = entity, Parameter = parameter, Holder = alias, HolderComposite = Comps[j], HolderParameter = parameter };
                }
            ParameterData own = entity.GetParameter(parameter)?.content;
            if (own == null) return null;
            return new Source() { Kind = "value", Value = own, Level = level, Target = entity, Parameter = parameter, Holder = entity, HolderComposite = Comps[level], HolderParameter = parameter };
        }

        /// <summary>Links aliases add to the parameter, innermost first (the outermost is last, and wins).</summary>
        private IEnumerable<(int, Composite, EntityConnector)> AliasLinks(int level, Entity entity, ShortGuid parameter)
        {
            for (int j = level - 1; j >= 0; j--)
                foreach (AliasEntity alias in Matching(j, level, entity))
                    foreach (EntityConnector link in alias.childLinks)
                        if (link.thisParamID == parameter && Comps[j].GetEntityByID(link.linkedEntityID) != null)
                            yield return (j, Comps[j], link);
        }

        /// <summary>The aliases in Comps[j] whose path is Chain[j..level-1] then the entity.</summary>
        private IEnumerable<AliasEntity> Matching(int j, int level, Entity entity)
        {
            ShortGuid first = j < level ? Chain[j].shortGUID : entity.shortGUID;
            foreach (AliasEntity alias in _aliases.Starting(Comps[j], first))
            {
                ShortGuid[] path = alias.alias.path;
                int length = path.Length > 0 && path[path.Length - 1] == ShortGuid.Invalid ? path.Length - 1 : path.Length;
                if (length != level - j + 1) continue;
                bool same = true;
                for (int i = 0; i < length - 1 && same; i++)
                    same = path[i] == Chain[j + i].shortGUID;
                if (same && path[length - 1] == entity.shortGUID)
                    yield return alias;
            }
        }

        /// <summary>
        /// For a value set at run time: what sets its base. From a function (a TriggerSelect, a FloatMultiply), the first pin
        /// of its composite of the same type its data links reach (through functions, not into instances); from an instance
        /// (a LightBehaviour), the same search inside the composite it places, from the output it reads, gives the instance's
        /// own pin to follow (IntensityMultiplier, say, which the fixture feeds from its lgt_*_IntMult pin). Null when none is found.
        /// </summary>
        public Source Base(Source runtime, DataType? type)
        {
            if (runtime?.RuntimeEntity == null) return null;
            Composite inner = McpScript.InstancedComposite(Commands, runtime.RuntimeEntity);
            if (inner == null)
            {
                VariableEntity pin = FirstVariable(runtime.RuntimeComposite, runtime.RuntimeEntity, type, null);
                return pin == null ? null : Of(runtime.RuntimeLevel, pin, pin.name);
            }
            VariableEntity output = inner.variables.FirstOrDefault(o => o.name == runtime.RuntimeParameter);
            if (output == null) return null;
            VariableEntity input = FirstVariable(inner, output, type, output);
            return input == null ? null : Of(runtime.RuntimeLevel, runtime.RuntimeEntity, input.name);
        }

        /// <summary>The first variable of a composite (of the type) reached by data links from an entity, through functions only.</summary>
        private static VariableEntity FirstVariable(Composite composite, Entity start, DataType? type, Entity skip)
        {
            HashSet<ShortGuid> seen = new HashSet<ShortGuid>() { start.shortGUID };
            Queue<(Entity entity, int depth)> pending = new Queue<(Entity, int)>();
            pending.Enqueue((start, 0));
            while (pending.Count != 0)
            {
                (Entity entity, int depth) = pending.Dequeue();
                foreach (EntityConnector link in entity.childLinks)
                {
                    Entity next = composite.GetEntityByID(link.linkedEntityID);
                    if (next == null || ReferenceEquals(next, skip) || !seen.Add(next.shortGUID)) continue;
                    if (next is VariableEntity variable)
                    {
                        if (type == null || variable.type == type) return variable;
                        continue;
                    }
                    if (depth < 4 && next is FunctionEntity function && function.function.IsFunctionType)
                        pending.Enqueue((next, depth + 1));
                }
            }
            return null;
        }
    }
}
