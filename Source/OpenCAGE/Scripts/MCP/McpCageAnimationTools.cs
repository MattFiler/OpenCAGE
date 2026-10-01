using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using LiveLink = OpenCAGE.RuntimeUtilsConnection.LiveLink;
using LiveLinkAnimationDrive = OpenCAGE.RuntimeUtilsConnection.LiveLinkAnimationDrive;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// CAGEAnimation entities: reading their tracks, keys, events and bindings, and editing them the way the
    /// CAGEAnimation editor does - every call one <see cref="CageAnimationEdit"/> on the undo history, so an
    /// open editor window and the inspector follow it exactly as they follow an undo. Also what animates an
    /// entity, a posed preview in the viewport, and the ANIMATED_MODEL (environment animation) resource.
    /// </summary>
    internal static class McpCageAnimationTools
    {
        /// <summary>The sub-properties a transform is animated through, in the editor's order.</summary>
        private static readonly string[] Components = { "x", "y", "z", "Yaw", "Pitch", "Roll" };

        /// <summary>What an animation-entity (T_GUID) event may fire (CAGEAnimationEditor.GuidKeyframeFunctionTypes).</summary>
        private static readonly FunctionType[] EventEntityTypes = { FunctionType.CMD_PlayAnimation, FunctionType.CameraPlayAnimation, FunctionType.PlayEnvironmentAnimation };

        private const float Eps = CageAnimationCurves.TimeEpsilon;
        private static readonly ShortGuid AnimLength = ShortGuidUtils.Generate("anim_length");

        #region Schemas
        private static JObject Prop(string type, string description) => new JObject() { ["type"] = type, ["description"] = description };

        private static JObject PathProp(string description) => new JObject()
        {
            ["type"] = "array",
            ["items"] = new JObject() { ["type"] = "string" },
            ["description"] = description,
        };

        private static JObject Item(string description, JObject properties, params string[] required)
        {
            JObject item = new JObject() { ["type"] = "object", ["description"] = description, ["properties"] = properties };
            if (required.Length != 0) item["required"] = new JArray(required);
            return item;
        }

        private const string TargetText = "Entity path (ids/names) from the animation's composite, through instances, e.g. [\"Door_1\"] or [\"Room\", \"Door\"].";
        private const string FromRootText = "The path starts in the level's root composite instead (for things outside the animation's composite).";

        private static readonly JObject SetSpec = Item("One track (or a whole transform) to write.", new JObject()
        {
            ["target"] = PathProp(TargetText),
            ["from_root"] = Prop("boolean", FromRootText),
            ["track"] = Prop("string", "An existing float track id (from get_cage_animation), instead of target/parameter/component."),
            ["parameter"] = Prop("string", "The parameter to drive: 'position' (a transform) or a FLOAT one, e.g. 'intensity_multiplier'."),
            ["component"] = Prop("string", "For a transform: x, y, z, Yaw, Pitch or Roll. Leave out to key whole transforms."),
            ["keys"] = new JObject()
            {
                ["type"] = "array",
                ["description"] = "[time, value] pairs, or {time, value, tan_in?:[dt,dv], tan_out?:[dt,dv]}; a whole transform takes {time, position?:[x,y,z], rotation?:[x,y,z]} ({x?,y?,z?} keys only the axes given). A key at an existing time updates it.",
            },
            ["replace"] = Prop("boolean", "The keys replace every key the track has."),
            ["remove_times"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "Times (seconds) of keys to delete." },
            ["move"] = new JObject()
            {
                ["type"] = "array",
                ["items"] = Item("Move the key at 'from' to 'to' (seconds), keeping its value and tangents.", new JObject() { ["from"] = Prop("number", "Current time."), ["to"] = Prop("number", "New time.") }, "from", "to"),
                ["description"] = "Keys to move in time.",
            },
            ["tangents"] = Prop("string", "Tangents for keys written here without explicit ones: flat (default; eases in and out), smooth (through the neighbours) or straight."),
        });

        private static readonly JObject RemoveSpec = Item("Tracks to drop.", new JObject()
        {
            ["track"] = Prop("string", "A float track id."),
            ["target"] = PathProp(TargetText),
            ["from_root"] = Prop("boolean", FromRootText),
            ["parameter"] = Prop("string", "Only this parameter's tracks (default: every track on the target)."),
            ["component"] = Prop("string", "Only this transform component's track."),
        });

        private static readonly JObject BindingProps = new JObject()
        {
            ["marker"] = PathProp("Bind a PositionMarker or ModelReference (path as for targets); [] unbinds."),
            ["character"] = PathProp("Bind a Character or VariableThePlayer; [] unbinds."),
            ["camera"] = PathProp("Bind a CameraResource; [] unbinds."),
            ["from_root"] = Prop("boolean", "These paths start in the level's root composite."),
        };
        #endregion

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "get_cage_animation",
                Title = "Read CAGEAnimation",
                Description = "Read a CAGEAnimation entity: length, interpolation, float tracks (which entity, parameter and component each drives, its resting value, keys with tangents, optional samples), event tracks (string events and the output pins they make, animation-entity events, marker/character/camera bindings) and loose tracks. Track ids it gives are what animate_parameters and set_animation_events take.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the CAGEAnimation is in (path or id).", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true),
                    McpSchema.String("filter", "Only float tracks whose target or parameter contains this text."),
                    McpSchema.Integer("max_keys", "Keys listed per float track (default 40; the count is always given)."),
                    McpSchema.Integer("max_tracks", "Float tracks listed (default 150; the total is always given)."),
                    McpSchema.Number("sample_step", "Also sample each listed float track every this many seconds (at most 200 samples a track).")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands(forEditing: false);
                    CAGEAnimation animation = FindAnimation(commands, call, out Composite composite);
                    CompileIfShown(composite);
                    double step = call.Num("sample_step", 0);
                    if (double.IsNaN(step) || step < 0)
                        throw new McpError("'sample_step' must be a positive number of seconds.");
                    return Describe(commands, composite, animation, new DescribeOptions()
                    {
                        Filter = call.Str("filter"),
                        MaxKeys = Math.Max(0, call.Int("max_keys", 40)),
                        MaxTracks = Math.Max(0, call.Int("max_tracks", 150)),
                        SampleStep = (float)step,
                    });
                }),
            };

            yield return new McpTool()
            {
                Name = "find_cage_animations",
                Title = "Find CAGEAnimations",
                Description = "List the level's CAGEAnimation entities (all of them, or those in one composite), with their length and track counts. Given an entity instead: every CAGEAnimation in the level that drives its parameters, binds it as a marker/character/camera, or fires it as an animation event - an animated position is overridden while the animation plays.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Only animations in this composite; with 'entity', the composite that entity is in."),
                    McpSchema.String("entity", "Report what animates this entity (id or name) instead of listing."),
                    McpSchema.String("filter", "Listing only: text the animation's name or composite path contains."),
                    McpSchema.Integer("limit", "At most this many results (default 100; the total is always given).")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindAnimations,
            };

            yield return new McpTool()
            {
                Name = "animate_parameters",
                Title = "Animate parameters",
                Description = "Edit a CAGEAnimation's float tracks as its editor does: drive an entity's FLOAT or TRANSFORM parameter (a transform gets all six component tracks; a new track starts from the resting value at 0 unless 'replace'), add, update, replace, move or delete keys and tangents, remove tracks or targets, set length and interpolation. One undo step; saved with save_level. Returns the touched tracks' keys.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the CAGEAnimation is in.", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true),
                    McpSchema.Array("set", "Tracks to write.", SetSpec),
                    McpSchema.Array("remove", "Tracks to drop: {track}, or {target, from_root?, parameter?, component?} (no parameter drops every track on that target).", RemoveSpec),
                    McpSchema.Number("length", "Playable length in seconds (anim_length). Default: kept, growing to cover the last key."),
                    McpSchema.String("interpolation", "Applied to every key, as the editor's 'Bezier curves' box: linear or bezier.", options: new[] { "linear", "bezier" })),
                Destructive = true,
                Run = AnimateParameters,
            };

            yield return new McpTool()
            {
                Name = "set_animation_events",
                Title = "Set animation events",
                Description = "Edit a CAGEAnimation's event tracks as its editor does: string events (each gives the animation output pins <name> and reverse_<name> to link from), animation-entity events (CMD_PlayAnimation, CameraPlayAnimation, PlayEnvironmentAnimation), tracks, and entity tracks' marker/character/camera bindings. Refuses to drop linked pins unless allow_breaking_links. One undo step; saved with save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the CAGEAnimation is in.", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true),
                    McpSchema.Array("add_tracks", "New event tracks.", Item("A new track.", Merge(new JObject()
                    {
                        ["type"] = new JObject() { ["type"] = "string", ["enum"] = new JArray("string", "entity"), ["description"] = "string (named events) or entity (animation-entity events, which can be bound)." },
                        ["ref"] = Prop("string", "A name for it that other entries in this call can give as 'track'."),
                    }, BindingProps), "type")),
                    McpSchema.Strings("remove_tracks", "Event track ids to delete, with their events and bindings."),
                    McpSchema.Array("add", "Events to add.", Item("An event.", new JObject()
                    {
                        ["time"] = Prop("number", "Seconds."),
                        ["event"] = Prop("string", "A string event's name."),
                        ["entity"] = Prop("string", "Or: a CMD_PlayAnimation/CameraPlayAnimation/PlayEnvironmentAnimation in the animation's composite (id or name)."),
                        ["duration"] = Prop("number", "Seconds (default 0)."),
                        ["track"] = Prop("string", "Track id or ref. Default: the unbound string track / the only entity track (made if missing)."),
                    }, "time")),
                    McpSchema.Array("remove", "Events to delete; every field given must match.", Item("Which events.", new JObject()
                    {
                        ["event"] = Prop("string", "String event name."),
                        ["entity"] = Prop("string", "Animation entity (id or name)."),
                        ["time"] = Prop("number", "Seconds."),
                        ["track"] = Prop("string", "Track id or ref."),
                    })),
                    McpSchema.Array("move", "Events to move in time.", Item("Move the one event at 'time' to 'to'.", new JObject()
                    {
                        ["time"] = Prop("number", "Its time now."),
                        ["to"] = Prop("number", "Its new time."),
                        ["event"] = Prop("string", "Pick it by name."),
                        ["entity"] = Prop("string", "Or by animation entity."),
                        ["track"] = Prop("string", "Or by track."),
                    }, "time", "to")),
                    McpSchema.Array("rename", "Rename string events (their pins are renamed too).", Item("A rename.", new JObject()
                    {
                        ["from"] = Prop("string", "Current name."),
                        ["to"] = Prop("string", "New name."),
                        ["track"] = Prop("string", "Only on this track."),
                    }, "from", "to")),
                    McpSchema.Array("bindings", "Bind entity tracks to a marker, character or camera.", Item("Bindings for one track.", Merge(new JObject()
                    {
                        ["track"] = Prop("string", "Track id or ref."),
                    }, BindingProps), "track")),
                    McpSchema.Boolean("allow_breaking_links", "Go ahead even if removed or renamed event pins are linked (those links are left pointing at nothing).")),
                Destructive = true,
                Run = SetAnimationEvents,
            };

            yield return new McpTool()
            {
                Name = "preview_cage_animation",
                Title = "Preview CAGEAnimation",
                Description = "Pose a CAGEAnimation in the 3D viewport at a time, as the editor's Animation Mode does, and return a picture. Opens the animation's composite in the editor and waits (up to 5 min) for the viewport to finish loading it before posing; the viewport must be enabled. Nothing in the level changes and the pose is cleared afterwards (it is up for about a second: an inspector or viewport edit made meanwhile would become a keyframe of the preview's copy instead of applying, and the result says so). Only animated transforms (positions and rotations) are shown. in_game: the running game holds it at the time instead, as Animation Mode's In game does (Live Link; the game evaluates every track, as the level does - the game must run this level, as saved and pushed), waits until a game frame shows it, returns the game's frame, and gives the game its animation back.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the CAGEAnimation is in.", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true),
                    McpSchema.Number("time", "Seconds into the animation.", required: true),
                    McpSchema.Integer("max_width", "Scale the picture down to at most this many pixels wide (default 1024)."),
                    McpSchema.Boolean("in_game", "Hold it in the running game (Live Link) rather than pose it in the viewport."),
                    McpSchema.Strings("instance_path", "in_game: ids of the composite instance entities from the level's root down to the placement to hold. Default: the path the open composite was reached through from the root, else every placement."),
                    McpSchema.Boolean("screenshot", "in_game: return the game's frame (default true); false reports what the game shows instead.")),
                ReadOnly = true,
                Run = Preview,
            };

            yield return new McpTool()
            {
                Name = "set_animated_model",
                Title = "Set animated model",
                Description = "Choose the environment-animation entry an entity's ANIMATED_MODEL resource uses, as the resource editor does: an entry id, 'new' (an empty entry, as the editor adds; it stays in the level's table after undo) or 'none' (removes the reference). Without 'entry' it only reports the current entry and the level's entries. One undo step; saved with save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the entity is in.", required: true),
                    McpSchema.String("entity", "The entity (id or name), e.g. a ModelReference.", required: true),
                    McpSchema.String("entry", "An environment-animation entry id, 'new' or 'none'. Leave out to only report."),
                    McpSchema.Integer("limit", "Entries listed when reporting (default 50).")),
                Destructive = true,
                Run = SetAnimatedModel,
            };
        }

        #region Finding
        private static CAGEAnimation FindAnimation(Commands commands, McpCall call, out Composite composite)
        {
            composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
            Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
            if (entity is CAGEAnimation animation)
                return animation;
            string name = McpScript.EntityName(commands, composite, entity);
            if (entity is ProxyEntity proxy && proxy.function == FunctionType.CAGEAnimation)
                throw new McpError(name + " is a proxy of a CAGEAnimation: the tracks live on the CAGEAnimation it points at (describe_entity shows its path).");
            throw new McpError(name + " is a " + McpScript.TypeName(commands, composite, entity) + ", not a CAGEAnimation. find_cage_animations lists the level's CAGEAnimations.");
        }

        /// <summary>The live pages of the composite on screen hold links not yet in the data: compile them first.</summary>
        private static void CompileIfShown(Composite composite)
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display != null && !display.IsDisposed && display.Populated && display.Composite == composite && !UndoStack.Current.Blocked)
                display.SaveAllFlowgraphs();
        }

        /// <summary>An entity path given by a tool, resolved.</summary>
        private sealed class Target
        {
            public EntityPath Path;
            public Composite Composite;
            public Entity Entity;
            public List<Tuple<Composite, Entity>> Chain;
            public string Name;
        }

        private static List<string> ReadSteps(JToken token, string where)
        {
            if (token is JArray array && array.Count != 0)
                return array.Select(o => o.Type == JTokenType.String ? (string)o : o.ToString(Newtonsoft.Json.Formatting.None)).ToList();
            if (token != null && token.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)token))
                return ((string)token).Split(new[] { '/', '\\', '>' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).ToList();
            throw new McpError("'" + where + "' is an entity path: ids or names from the animation's composite through instances, e.g. [\"Door_1\"] or [\"Room\", \"Door\"].");
        }

        /// <summary>
        /// A path from the animation's composite (or the level root), checked to read back to the same entity
        /// the way the game reads stored paths (<see cref="CommandsUtils.ResolveEntityPath(EntityPath, Composite)"/>).
        /// </summary>
        private static Target ResolveTarget(Commands commands, Composite composite, JToken token, bool fromRoot, string where)
        {
            List<string> steps = ReadSteps(token, where);
            Composite start = fromRoot ? commands.EntryPoints[0] : composite;
            ShortGuid[] ids = McpScript.ResolvePath(commands, start, steps, out Composite targetComposite, out Entity target);
            EntityPath path = new EntityPath(ids);
            List<Tuple<Composite, Entity>> chain = commands.Utils.ResolveEntityPath(path, composite);
            (Composite readComposite, Entity readEntity) = commands.Utils.GetResolvedTarget(chain);
            if (readEntity != target || readComposite != targetComposite)
                throw new McpError("'" + where + "' reads back as something else from " + composite.name + (fromRoot ? "" : " (try from_root: true)") + ", so it cannot be stored.");
            return new Target()
            {
                Path = path,
                Composite = targetComposite,
                Entity = target,
                Chain = chain,
                Name = McpScript.EntityName(commands, targetComposite, target),
            };
        }

        /// <summary>Whether a stored path names the same thing: the same ids, or the same instance chain read back.</summary>
        private static bool SameTarget(Commands commands, Composite composite, EntityPath stored, Target wanted)
        {
            if (stored == null) return false;
            if (stored == wanted.Path) return true;
            List<Tuple<Composite, Entity>> chain = commands.Utils.ResolveEntityPath(stored, composite);
            if (chain == null || chain.Count == 0 || wanted.Chain == null || chain.Count != wanted.Chain.Count) return false;
            for (int i = 0; i < chain.Count; i++)
                if (chain[i].Item2 != wanted.Chain[i].Item2) return false;
            return true;
        }

        private static string PathKey(EntityPath path)
        {
            if (path?.path == null) return "";
            return string.Join("/", path.path.Select(o => o.AsUInt32.ToString("X8")));
        }
        #endregion

        #region Describing
        private static double R(float value) => Math.Round((double)value, 5);

        private static CAGEAnimation.InterpolationMode InterpolationOf(IEnumerable<CAGEAnimation.FloatTrack> tracks)
        {
            //As the editor decides it when it opens: the first key's mode, Bezier when there are no keys
            foreach (CAGEAnimation.FloatTrack track in tracks)
            {
                if (track?.keyframes == null || track.keyframes.Count == 0) continue;
                return track.keyframes[0].mode == CAGEAnimation.InterpolationMode.Bezier ? CAGEAnimation.InterpolationMode.Bezier : CAGEAnimation.InterpolationMode.Linear;
            }
            return CAGEAnimation.InterpolationMode.Bezier;
        }

        private static float Latest(IEnumerable<CAGEAnimation.FloatTrack> floats, IEnumerable<CAGEAnimation.EventTrack> events)
        {
            float latest = 0f;
            foreach (CAGEAnimation.FloatTrack track in floats)
                foreach (CAGEAnimation.FloatTrack.Keyframe key in track?.keyframes ?? new List<CAGEAnimation.FloatTrack.Keyframe>())
                    latest = Math.Max(latest, key.time);
            foreach (CAGEAnimation.EventTrack track in events)
                foreach (CAGEAnimation.EventTrack.Keyframe key in track?.keyframes ?? new List<CAGEAnimation.EventTrack.Keyframe>())
                    latest = Math.Max(latest, key.time);
            return latest;
        }

        private static float? StoredLength(CAGEAnimation animation) => (animation.GetParameter(AnimLength)?.content as cFloat)?.value;

        /// <summary>The length the editor works with: the latest key or event, or the stored anim_length if longer.</summary>
        private static float EffectiveLength(CAGEAnimation animation) => Math.Max(Latest(animation.floatTracks, animation.eventTracks), StoredLength(animation) ?? 0f);

        private static string ComponentName(ShortGuid sub)
        {
            if (sub.IsInvalid || sub == ShortGuidUtils.Generate("", false)) return "";
            foreach (string component in Components)
                if (ShortGuidUtils.Generate(component, false) == sub) return component;
            return McpScript.ParamName(sub);
        }

        private static string TrackTypeName(CAGEAnimation.EventTrack track)
        {
            //As the curve editor decides a lane's type (CurveEditor.GetEventTrackType)
            if (track.keyframes.Any(o => o.track_type == ANIM_TRACK_TYPE.T_GUID)) return "entity";
            if (track.keyframes.Count != 0) return "string";
            if (track.track_type == ANIM_TRACK_TYPE.T_GUID) return "entity";
            if (track.track_type == ANIM_TRACK_TYPE.T_MASTERING) return "mastering";
            return "string";
        }

        private static JObject DescribeTarget(Commands commands, Composite composite, EntityPath path)
        {
            List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveEntityPath(path, composite);
            JObject result = new JObject() { ["path"] = McpScript.DescribePath(commands, resolved, path?.path) };
            (Composite c, Entity e) = commands.Utils.GetResolvedTarget(resolved);
            if (e == null)
            {
                result["unresolved"] = true;
                return result;
            }
            if (!commands.Utils.CouldResolve(commands.Utils.ResolveAlias(path, composite)))
            {
                if (commands.Utils.CouldResolve(commands.Utils.ResolveHierarchy(path))) result["from_root"] = true;
                else result["reading"] = "proxy-style (starts with a composite id)";
            }
            result["composite"] = c.name;
            result["id"] = McpScript.Id(e.shortGUID);
            result["name"] = McpScript.EntityName(commands, c, e);
            result["type"] = McpScript.TypeName(commands, c, e);
            return result;
        }

        private static string TargetLabel(Commands commands, Composite composite, EntityPath path)
        {
            (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(path, composite));
            return e == null ? "(unresolved)" : McpScript.EntityName(commands, c, e);
        }

        /// <summary>What the target of a float track rests at when nothing plays (its parameter's value, or null).</summary>
        private static float? RestValue(Commands commands, Composite composite, CAGEAnimation.Connection connection)
        {
            try
            {
                (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(connection.connectedEntity, composite));
                if (e == null) return null;
                ParameterData content = e.GetParameter(connection.target_param)?.content;
                if (content == null)
                {
                    try { content = commands.Utils.CreateDefaultParameterData(e, c, connection.target_param); } catch { }
                }
                return ComponentValue(content, ComponentName(connection.target_sub_param));
            }
            catch
            {
                return null;
            }
        }

        private static float? ComponentValue(ParameterData content, string component)
        {
            switch (content)
            {
                case cFloat f: return f.value;
                case cTransform t:
                    switch (component)
                    {
                        case "x": return t.position.X;
                        case "y": return t.position.Y;
                        case "z": return t.position.Z;
                        //Cathode eulers: Y is yaw, X is pitch, Z is roll
                        case "Yaw": return t.rotation.Y;
                        case "Pitch": return t.rotation.X;
                        case "Roll": return t.rotation.Z;
                    }
                    return null;
            }
            return null;
        }

        private static int LinkCount(Composite composite, Entity animation, ShortGuid pin)
        {
            int count = animation.childLinks.Count(o => o.thisParamID == pin);
            foreach (Entity other in composite.GetEntities())
                count += other.childLinks.Count(o => o.linkedEntityID == animation.shortGUID && o.linkedParamID == pin);
            return count;
        }

        /// <summary>Every T_STRING pin the event tracks make, in order.</summary>
        private static List<ShortGuid> EventPins(IEnumerable<CAGEAnimation.EventTrack> tracks)
        {
            List<ShortGuid> pins = new List<ShortGuid>();
            HashSet<ShortGuid> seen = new HashSet<ShortGuid>();
            foreach (CAGEAnimation.EventTrack track in tracks)
            {
                foreach (CAGEAnimation.EventTrack.Keyframe key in track?.keyframes ?? new List<CAGEAnimation.EventTrack.Keyframe>())
                {
                    if (key == null || key.track_type != ANIM_TRACK_TYPE.T_STRING) continue;
                    if (seen.Add(key.forward)) pins.Add(key.forward);
                    if (seen.Add(key.reverse)) pins.Add(key.reverse);
                }
            }
            return pins;
        }

        private sealed class DescribeOptions
        {
            public string Filter;
            public int MaxKeys = 40;
            public int MaxTracks = 150;
            public float SampleStep;
            /// <summary>List keys only for these tracks (others get a count and their time span); null lists all.</summary>
            public HashSet<ShortGuid> KeysFor;
        }

        private static JObject Describe(Commands commands, Composite composite, CAGEAnimation animation, DescribeOptions options)
        {
            CAGEAnimation.InterpolationMode mode = InterpolationOf(animation.floatTracks);
            float length = EffectiveLength(animation);
            float? stored = StoredLength(animation);
            JObject result = new JObject()
            {
                ["composite"] = composite.name,
                ["id"] = McpScript.Id(animation.shortGUID),
                ["name"] = McpScript.EntityName(commands, composite, animation),
                ["length"] = R(length),
                ["length_parameter"] = stored == null ? JValue.CreateNull() : (JToken)R(stored.Value),
                ["interpolation"] = mode == CAGEAnimation.InterpolationMode.Bezier ? "bezier" : "linear",
                ["float_track_count"] = animation.floatTracks.Count,
                ["event_track_count"] = animation.eventTracks.Count,
            };

            Dictionary<ShortGuid, CAGEAnimation.FloatTrack> floats = new Dictionary<ShortGuid, CAGEAnimation.FloatTrack>();
            foreach (CAGEAnimation.FloatTrack track in animation.floatTracks)
                if (track != null && !floats.ContainsKey(track.shortGUID)) floats.Add(track.shortGUID, track);
            HashSet<ShortGuid> eventIds = new HashSet<ShortGuid>(animation.eventTracks.Where(o => o != null).Select(o => o.shortGUID));

            //Float tracks, grouped by the entity they drive
            Dictionary<string, JObject> targets = new Dictionary<string, JObject>();
            List<JObject> order = new List<JObject>();
            HashSet<ShortGuid> bound = new HashSet<ShortGuid>();
            int matching = 0, shown = 0;
            string filter = string.IsNullOrWhiteSpace(options.Filter) ? null : options.Filter.Trim();
            foreach (CAGEAnimation.Connection connection in animation.connections)
            {
                if (connection == null || !floats.TryGetValue(connection.target_track, out CAGEAnimation.FloatTrack track)) continue;
                bound.Add(track.shortGUID);
                string key = PathKey(connection.connectedEntity);
                if (!targets.TryGetValue(key, out JObject target))
                {
                    target = new JObject() { ["target"] = DescribeTarget(commands, composite, connection.connectedEntity), ["tracks"] = new JArray() };
                    targets.Add(key, target);
                    order.Add(target);
                }
                string parameter = McpScript.ParamName(connection.target_param);
                string component = ComponentName(connection.target_sub_param);
                if (filter != null && parameter.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 && target["target"].ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                matching++;
                if (shown >= options.MaxTracks) continue;
                shown++;
                ((JArray)target["tracks"]).Add(DescribeFloatTrack(commands, composite, connection, track, parameter, component, mode, length, options));
            }
            result["targets"] = new JArray(order.Where(o => ((JArray)o["tracks"]).Count != 0));
            if (matching > shown)
                result["tracks_not_listed"] = (matching - shown) + " of " + matching + " matching tracks (raise max_tracks, or narrow with filter)";

            //Event tracks
            JArray eventTracks = new JArray();
            foreach (CAGEAnimation.EventTrack track in animation.eventTracks)
            {
                if (track == null) continue;
                JObject described = new JObject() { ["track"] = McpScript.Id(track.shortGUID), ["type"] = TrackTypeName(track) };
                JObject bindings = new JObject();
                foreach (CAGEAnimation.Connection connection in animation.connections)
                {
                    if (connection == null || connection.target_track != track.shortGUID) continue;
                    string slot = connection.binding_type.ToString().ToLowerInvariant();
                    string name = slot;
                    for (int i = 2; bindings[name] != null; i++) name = slot + "_" + i;
                    bindings[name] = DescribeTarget(commands, composite, connection.connectedEntity);
                }
                if (bindings.Count != 0) described["bindings"] = bindings;
                JArray events = new JArray();
                foreach (CAGEAnimation.EventTrack.Keyframe key in track.keyframes.Where(o => o != null).OrderBy(o => o.time))
                    events.Add(DescribeEvent(commands, composite, key));
                described["events"] = events;
                eventTracks.Add(described);
            }
            result["event_tracks"] = eventTracks;

            List<ShortGuid> pins = EventPins(animation.eventTracks);
            if (pins.Count != 0)
            {
                result["event_pins"] = new JArray(pins.Select(o => new JObject() { ["pin"] = McpScript.ParamName(o), ["links"] = LinkCount(composite, animation, o) }));
                result["event_pins_note"] = "Each string event gives the animation output pins <name> and reverse_<name> (its counterpart for reverse playback); link from them with add_links (source = this CAGEAnimation; give the link allow_custom: true if add_links does not recognise the pin).";
            }

            //What nothing plays, or plays nothing
            JArray loose = new JArray(animation.floatTracks.Where(o => o != null && !bound.Contains(o.shortGUID)).Select(o => new JObject() { ["track"] = McpScript.Id(o.shortGUID), ["keys"] = o.keyframes.Count }));
            if (loose.Count != 0) result["unbound_float_tracks"] = loose;
            JArray dangling = new JArray();
            foreach (CAGEAnimation.Connection connection in animation.connections)
            {
                if (connection == null || floats.ContainsKey(connection.target_track) || eventIds.Contains(connection.target_track)) continue;
                dangling.Add(new JObject()
                {
                    ["missing_track"] = McpScript.Id(connection.target_track),
                    ["binding"] = connection.binding_type.ToString().ToLowerInvariant(),
                    ["parameter"] = McpScript.ParamName(connection.target_param),
                    ["target"] = DescribeTarget(commands, composite, connection.connectedEntity),
                });
            }
            if (dangling.Count != 0) result["connections_without_track"] = dangling;
            return result;
        }

        private static JObject DescribeFloatTrack(Commands commands, Composite composite, CAGEAnimation.Connection connection, CAGEAnimation.FloatTrack track, string parameter, string component, CAGEAnimation.InterpolationMode mode, float length, DescribeOptions options)
        {
            JObject described = new JObject() { ["track"] = McpScript.Id(track.shortGUID), ["parameter"] = parameter };
            if (component != "") described["component"] = component;
            described["type"] = connection.target_param_type.ToString();
            if (connection.binding_type != ObjectType.ENTITY) described["binding"] = connection.binding_type.ToString().ToLowerInvariant();
            float? rest = RestValue(commands, composite, connection);
            if (rest != null) described["rest"] = R(rest.Value);
            described["key_count"] = track.keyframes.Count;

            List<CAGEAnimation.FloatTrack.Keyframe> keys = track.keyframes.Where(o => o != null).OrderBy(o => o.time).ToList();
            if (options.KeysFor == null || options.KeysFor.Contains(track.shortGUID))
            {
                bool bezier = mode == CAGEAnimation.InterpolationMode.Bezier;
                JArray listed = new JArray();
                foreach (CAGEAnimation.FloatTrack.Keyframe key in keys.Take(options.MaxKeys))
                {
                    if (!bezier && key.mode == mode)
                    {
                        listed.Add(new JArray(R(key.time), R(key.value.Y)));
                        continue;
                    }
                    JObject k = new JObject() { ["time"] = R(key.time), ["value"] = R(key.value.Y) };
                    if (bezier)
                    {
                        k["tan_in"] = new JArray(R(key.tan_in.X), R(key.tan_in.Y));
                        k["tan_out"] = new JArray(R(key.tan_out.X), R(key.tan_out.Y));
                    }
                    if (key.mode != mode) k["mode"] = key.mode.ToString().ToLowerInvariant();
                    listed.Add(k);
                }
                described["keys"] = listed;
                if (keys.Count > options.MaxKeys) described["keys_listed"] = options.MaxKeys + " of " + keys.Count + " (raise max_keys)";
            }
            else if (keys.Count != 0)
            {
                described["from"] = R(keys[0].time);
                described["to"] = R(keys[keys.Count - 1].time);
            }

            if (options.SampleStep > 0 && keys.Count != 0)
            {
                float step = Math.Max(options.SampleStep, Math.Max(length, 0.0001f) / 200f);
                JArray samples = new JArray();
                bool bezier = mode == CAGEAnimation.InterpolationMode.Bezier;
                for (int i = 0; i <= 200; i++)
                {
                    float t = i * step;
                    if (t > length + Eps) break;
                    samples.Add(new JArray(R(t), R(CageAnimationCurves.ValueAt(track, t, bezier))));
                }
                described["samples"] = samples;
            }
            return described;
        }

        private static JObject DescribeEvent(Commands commands, Composite composite, CAGEAnimation.EventTrack.Keyframe key)
        {
            JObject described = new JObject() { ["time"] = R(key.time) };
            if (key.track_type == ANIM_TRACK_TYPE.T_GUID)
            {
                //Resolved only in the animation's own composite, as the editor does
                Entity entity = composite.GetEntityByID(key.forward);
                described["entity"] = entity == null
                    ? new JObject() { ["id"] = McpScript.Id(key.forward), ["missing"] = true }
                    : new JObject() { ["id"] = McpScript.Id(entity.shortGUID), ["name"] = McpScript.EntityName(commands, composite, entity), ["type"] = McpScript.TypeName(commands, composite, entity) };
            }
            else
            {
                string name = McpScript.ParamName(key.forward);
                described["event"] = name;
                if (key.reverse != ShortGuidUtils.Generate("reverse_" + name, false))
                    described["reverse_pin"] = McpScript.ParamName(key.reverse);
                if (key.track_type != ANIM_TRACK_TYPE.T_STRING)
                    described["key_type"] = key.track_type.ToString();
            }
            if (key.duration != 0f) described["duration"] = R(key.duration);
            return described;
        }
        #endregion

        #region What animates an entity
        /// <summary>
        /// Every CAGEAnimation in the level that drives a parameter of <paramref name="entity"/> (in
        /// <paramref name="composite"/>), binds it to an event track, or fires it as an animation event.
        /// Paths are resolved from each animation's own composite, as the game reads them.
        /// </summary>
        internal static JArray AnimatedBy(Commands commands, Composite composite, Entity entity, int limit = 100)
        {
            JArray found = new JArray();
            foreach (Composite other in commands.Entries)
            {
                if (other == null) continue;
                foreach (CAGEAnimation animation in other.functions.OfType<CAGEAnimation>())
                {
                    HashSet<ShortGuid> floatIds = new HashSet<ShortGuid>(animation.floatTracks.Where(o => o != null).Select(o => o.shortGUID));
                    List<string> drives = new List<string>();
                    List<string> bindings = new List<string>();
                    JArray paths = new JArray();
                    HashSet<string> pathKeys = new HashSet<string>();
                    foreach (CAGEAnimation.Connection connection in animation.connections)
                    {
                        if (connection?.connectedEntity?.path == null || connection.connectedEntity.GetPointedEntityID() != entity.shortGUID) continue;
                        (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(connection.connectedEntity, other));
                        if (e != entity || c != composite) continue;
                        if (floatIds.Contains(connection.target_track))
                        {
                            string component = ComponentName(connection.target_sub_param);
                            string label = McpScript.ParamName(connection.target_param) + (component == "" ? "" : "." + component);
                            if (!drives.Contains(label)) drives.Add(label);
                        }
                        else
                        {
                            string label = connection.binding_type.ToString().ToLowerInvariant();
                            if (!bindings.Contains(label)) bindings.Add(label);
                        }
                        if (pathKeys.Add(PathKey(connection.connectedEntity)))
                            paths.Add(McpScript.DescribePath(commands, commands.Utils.ResolveEntityPath(connection.connectedEntity, other), connection.connectedEntity.path));
                    }
                    int events = other == composite
                        ? animation.eventTracks.Where(o => o != null).Sum(o => o.keyframes.Count(k => k != null && k.track_type == ANIM_TRACK_TYPE.T_GUID && k.forward == entity.shortGUID))
                        : 0;
                    if (drives.Count == 0 && bindings.Count == 0 && events == 0) continue;
                    JObject item = new JObject()
                    {
                        ["composite"] = other.name,
                        ["animation"] = new JObject() { ["id"] = McpScript.Id(animation.shortGUID), ["name"] = McpScript.EntityName(commands, other, animation) },
                    };
                    if (other != composite) item["via"] = paths;
                    if (drives.Count != 0) item["drives"] = new JArray(drives);
                    if (bindings.Count != 0) item["bound_as"] = new JArray(bindings);
                    if (events != 0) item["fired_as_event"] = events;
                    found.Add(item);
                    if (found.Count >= limit) return found;
                }
            }
            return found;
        }

        private static object FindAnimations(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                int limit = Math.Max(1, call.Int("limit", 100));
                if (call.Has("entity"))
                {
                    if (!call.Has("composite"))
                        throw new McpError("Say which composite the entity is in ('composite').");
                    if (call.Has("filter"))
                        throw new McpError("'filter' is for listing; leave it out when asking about an entity.");
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite"));
                    Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity"));
                    JArray by = AnimatedBy(commands, composite, entity, limit);
                    JObject result = new JObject()
                    {
                        ["composite"] = composite.name,
                        ["entity"] = new JObject() { ["id"] = McpScript.Id(entity.shortGUID), ["name"] = McpScript.EntityName(commands, composite, entity), ["type"] = McpScript.TypeName(commands, composite, entity) },
                        ["animated_by"] = by,
                    };
                    if (by.Count >= limit) result["note"] = "Stopped at " + limit + " (raise limit).";
                    return result;
                }

                Composite scope = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : null;
                string filter = call.Str("filter");
                List<(Composite composite, CAGEAnimation animation)> all = new List<(Composite, CAGEAnimation)>();
                foreach (Composite composite in scope == null ? commands.Entries : new List<Composite>() { scope })
                {
                    if (composite == null) continue;
                    foreach (CAGEAnimation animation in composite.functions.OfType<CAGEAnimation>())
                    {
                        if (!string.IsNullOrWhiteSpace(filter)
                            && McpScript.EntityName(commands, composite, animation).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                            && (composite.name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        all.Add((composite, animation));
                    }
                }
                all = all.OrderBy(o => o.composite.name, StringComparer.OrdinalIgnoreCase).ThenBy(o => McpScript.EntityName(commands, o.composite, o.animation), StringComparer.OrdinalIgnoreCase).ToList();
                return new JObject()
                {
                    ["count"] = all.Count,
                    ["animations"] = new JArray(all.Take(limit).Select(o =>
                    {
                        HashSet<ShortGuid> floatIds = new HashSet<ShortGuid>(o.animation.floatTracks.Where(t => t != null).Select(t => t.shortGUID));
                        return new JObject()
                        {
                            ["composite"] = o.composite.name,
                            ["id"] = McpScript.Id(o.animation.shortGUID),
                            ["name"] = McpScript.EntityName(commands, o.composite, o.animation),
                            ["length"] = R(EffectiveLength(o.animation)),
                            ["float_tracks"] = o.animation.floatTracks.Count,
                            ["targets"] = o.animation.connections.Where(c => c != null && floatIds.Contains(c.target_track)).Select(c => PathKey(c.connectedEntity)).Distinct().Count(),
                            ["event_tracks"] = o.animation.eventTracks.Count,
                            ["events"] = o.animation.eventTracks.Where(t => t != null).Sum(t => t.keyframes.Count),
                        };
                    })),
                };
            });
        }
        #endregion

        #region The working copy
        /// <summary>
        /// A CAGEAnimation being changed by a tool. Its track lists are fresh deep copies, as the editor's
        /// CommitLive takes them, so nothing the undo step keeps as "before" is ever written to; they are
        /// installed together as one <see cref="CageAnimationEdit"/>, which is also what tells an open editor
        /// window and the inspector to take them.
        /// </summary>
        private sealed class Working
        {
            public readonly Commands Commands;
            public readonly Composite Composite;
            public readonly CAGEAnimation Animation;
            public readonly CageAnimationEdit.Lists Before;
            public readonly List<CAGEAnimation.Connection> Connections;
            public readonly List<CAGEAnimation.FloatTrack> FloatTracks;
            public readonly List<CAGEAnimation.EventTrack> EventTracks;
            public readonly float OriginalLength;
            public CAGEAnimation.InterpolationMode Mode;
            public float? Length;
            public bool Changed;
            public float FinalLength;
            public readonly List<string> Changes = new List<string>();
            /// <summary>Tracks this call made, to seed at their resting value over the whole length if nothing keys them.</summary>
            public readonly Dictionary<CAGEAnimation.FloatTrack, float> Created = new Dictionary<CAGEAnimation.FloatTrack, float>();
            public readonly HashSet<CAGEAnimation.FloatTrack> Touched = new HashSet<CAGEAnimation.FloatTrack>();
            /// <summary>Tracks whose keys were replaced outright: they hold exactly what was given.</summary>
            public readonly HashSet<CAGEAnimation.FloatTrack> Exact = new HashSet<CAGEAnimation.FloatTrack>();

            public Working(Commands commands, Composite composite, CAGEAnimation animation)
            {
                Commands = commands;
                Composite = composite;
                Animation = animation;
                Before = CageAnimationEdit.Lists.Of(animation);
                Connections = animation.connections.Copy() ?? new List<CAGEAnimation.Connection>();
                FloatTracks = animation.floatTracks.Copy() ?? new List<CAGEAnimation.FloatTrack>();
                EventTracks = animation.eventTracks.Copy() ?? new List<CAGEAnimation.EventTrack>();
                //Keys older editor builds made carry value.X = 1; the editor straightens them on open, and so does this
                foreach (CAGEAnimation.FloatTrack track in FloatTracks)
                    CageAnimationCurves.NormaliseKeyframes(track);
                OriginalLength = EffectiveLength(animation);
                Mode = InterpolationOf(FloatTracks);
            }

            public CAGEAnimation.FloatTrack FloatTrack(ShortGuid id) => FloatTracks.FirstOrDefault(o => o != null && o.shortGUID == id);

            /// <summary>Install the copies as one undo step. False when there was nothing to change.</summary>
            public bool Commit(string label)
            {
                float latest = Latest(FloatTracks, EventTracks);
                float length;
                if (Length.HasValue)
                {
                    if (latest > Length.Value + Eps)
                        throw new McpError("'length' (" + R(Length.Value) + " s) is shorter than the last key or event (" + R(latest) + " s). Move or remove those first, or leave 'length' out.");
                    length = Length.Value;
                }
                else
                    length = Math.Max(OriginalLength, latest);
                FinalLength = length;

                //A new track starts from the resting value, as the editor seeds one (AddNewConnectionSet): with nothing
                //keyed on it, a key at the start and one at the end; with keys, one at the start unless they have it
                foreach (KeyValuePair<CAGEAnimation.FloatTrack, float> made in Created)
                {
                    if (!FloatTracks.Contains(made.Key)) continue;
                    List<CAGEAnimation.FloatTrack.Keyframe> keys = made.Key.keyframes;
                    if (keys.Count == 0)
                    {
                        keys.Add(CageAnimationCurves.NewKeyframe(0f, made.Value, Mode));
                        if (length > Eps)
                            keys.Add(CageAnimationCurves.NewKeyframe(length, made.Value, Mode));
                        Changed = true;
                        continue;
                    }
                    if (Exact.Contains(made.Key) || keys.Any(o => o.time <= Eps)) continue;
                    CAGEAnimation.FloatTrack.Keyframe start = CageAnimationCurves.NewKeyframe(0f, made.Value, Mode);
                    keys.Insert(0, start);
                    SetTangents(made.Key, start, "flat", true, true);
                    Changed = true;
                }
                foreach (CAGEAnimation.FloatTrack track in Touched)
                    if (FloatTracks.Contains(track) && track.keyframes.Count == 0)
                        throw new McpError("That would leave float track " + McpScript.Id(track.shortGUID) + " with no keys. To stop animating it, drop it with 'remove' instead.");

                Parameter stored = Animation.GetParameter(AnimLength);
                bool lengthDiffers = !(stored?.content is cFloat current) || Math.Abs(current.value - length) > 1e-6f;
                if (!Changed && !(Length.HasValue && lengthDiffers))
                    return false;

                //anim_length as a new Parameter in a new list: the undo step keeps the old list and its objects
                List<Parameter> parameters = new List<Parameter>(Animation.parameters);
                Parameter written = new Parameter(AnimLength, new cFloat(length), stored?.variant ?? ParameterVariant.PARAMETER);
                int at = stored == null ? -1 : parameters.IndexOf(stored);
                if (at >= 0) parameters[at] = written;
                else parameters.Add(written);

                CageAnimationEdit.Lists after = new CageAnimationEdit.Lists()
                {
                    Connections = Connections,
                    EventTracks = EventTracks,
                    FloatTracks = FloatTracks,
                    Parameters = parameters,
                };
                UndoStack.Current.Apply(new CageAnimationEdit(Composite, Animation, Before, after, "AI: " + label));
                return true;
            }
        }

        private static Working Begin(McpCall call)
        {
            Commands commands = McpEditor.RequireCommands();
            McpEditor.RequireUndoIdle();
            CAGEAnimation animation = FindAnimation(commands, call, out Composite composite);
            return new Working(commands, composite, animation);
        }

        private static void CheckFields(JObject item, string where, params string[] allowed)
        {
            List<string> unknown = item.Properties().Select(o => o.Name).Where(o => !allowed.Contains(o)).ToList();
            if (unknown.Count != 0)
                throw new McpError("Unknown field" + (unknown.Count > 1 ? "s " : " ") + string.Join(", ", unknown.Select(o => "'" + o + "'")) + " in " + where + ". It takes: " + string.Join(", ", allowed) + ".");
        }

        private static JObject ItemAt(JToken token, string where)
        {
            if (token is JObject item) return item;
            throw new McpError(where + " must be an object.");
        }

        private static float ReadNumber(JToken token, string where)
        {
            double value = McpValues.ReadDouble(token, where);
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 1e7)
                throw new McpError("'" + where + "' must be a finite number.");
            return (float)value;
        }

        private static float ReadTime(JToken token, string where)
        {
            if (token == null || token.Type == JTokenType.Null)
                throw new McpError("'" + where + "' (a time in seconds) is missing.");
            float time = ReadNumber(token, where);
            if (time < 0f)
                throw new McpError("'" + where + "' cannot be negative (times run from 0).");
            return time;
        }

        private static bool ReadFlag(JToken token, string where) => token != null && token.Type != JTokenType.Null && McpValues.ReadBool(token, where);

        private static string Times(CAGEAnimation.FloatTrack track)
        {
            List<string> times = track.keyframes.OrderBy(o => o.time).Select(o => R(o.time).ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
            return times.Count == 0 ? "none" : string.Join(", ", times.Take(30)) + (times.Count > 30 ? ", ..." : "");
        }
        #endregion

        #region animate_parameters
        private struct KeySpec
        {
            public float Time;
            public float Value;
            public Vector2? TanIn;
            public Vector2? TanOut;
        }

        /// <summary>What one 'set' entry writes to: one track, or the component tracks of a whole transform.</summary>
        private sealed class Channel
        {
            public string Label;
            public CAGEAnimation.FloatTrack Track;
            public Dictionary<string, CAGEAnimation.FloatTrack> Transform;
            public Target Target;
            public ShortGuid Parameter;
            public Dictionary<string, float> Rest;
            public List<string> Made = new List<string>();
        }

        private static object AnimateParameters(McpCall call)
        {
            JArray sets = call.Array("set");
            JArray removes = call.Array("remove");
            if (sets.Count == 0 && removes.Count == 0 && !call.Has("length") && !call.Has("interpolation"))
                throw new McpError("Nothing to do: give 'set', 'remove', 'length' or 'interpolation'.");

            List<string> notes = new List<string>();
            return McpEditor.UI(() =>
            {
                Working w = Begin(call);
                if (call.Has("interpolation"))
                {
                    string wanted = call.Str("interpolation").Trim().ToLowerInvariant();
                    if (wanted != "linear" && wanted != "bezier")
                        throw new McpError("'interpolation' is linear or bezier.");
                    w.Mode = wanted == "bezier" ? CAGEAnimation.InterpolationMode.Bezier : CAGEAnimation.InterpolationMode.Linear;
                }
                if (call.Has("length"))
                {
                    float length = ReadNumber(call.Token("length"), "length");
                    if (length <= 0f)
                        throw new McpError("'length' must be more than 0 seconds.");
                    w.Length = length;
                }

                for (int i = 0; i < removes.Count; i++)
                    RemoveTracks(w, ItemAt(removes[i], "remove[" + i + "]"), "remove[" + i + "]");
                for (int i = 0; i < sets.Count; i++)
                    ApplySet(w, ItemAt(sets[i], "set[" + i + "]"), "set[" + i + "]", notes);

                //The 'Bezier curves' box: every key of every track takes the one mode
                if (call.Has("interpolation"))
                {
                    int switched = 0;
                    foreach (CAGEAnimation.FloatTrack track in w.FloatTracks)
                        foreach (CAGEAnimation.FloatTrack.Keyframe key in track.keyframes)
                            if (key.mode != w.Mode) { key.mode = w.Mode; switched++; }
                    if (switched != 0)
                    {
                        w.Changed = true;
                        w.Changes.Add("interpolation " + w.Mode.ToString().ToLowerInvariant() + " (" + switched + " keys)");
                    }
                }

                ClampTangents(w, notes);
                string label = "Animate " + McpScript.EntityName(w.Commands, w.Composite, w.Animation);
                bool changed = w.Commit(label);
                if (changed && w.Length.HasValue)
                    w.Changes.Add("length " + R(w.FinalLength) + " s");
                if (changed)
                    McpScriptEdit.Show(w.Composite, new List<Entity>() { w.Animation });

                JObject result = Describe(w.Commands, w.Composite, w.Animation, new DescribeOptions()
                {
                    MaxKeys = 60,
                    MaxTracks = 150,
                    KeysFor = new HashSet<ShortGuid>(w.Touched.Where(o => w.FloatTracks.Contains(o)).Select(o => o.shortGUID)),
                });
                JObject answer = new JObject() { ["changed"] = changed };
                if (changed && w.Changes.Count != 0) answer["changes"] = new JArray(w.Changes);
                foreach (JProperty property in result.Properties().ToList())
                    answer[property.Name] = property.Value;
                foreach (string note in notes) call.Note(note);
                if (!changed) call.Note("The animation already held all of that, so no undo step was made.");
                return answer;
            });
        }

        private static string CanonicalComponent(string text, string where)
        {
            string wanted = (text ?? "").Trim();
            string lower = wanted.ToLowerInvariant();
            switch (lower)
            {
                case "position.x": case "px": return "x";
                case "position.y": case "py": return "y";
                case "position.z": case "pz": return "z";
                case "rotation.x": return "Pitch";
                case "rotation.y": return "Yaw";
                case "rotation.z": return "Roll";
            }
            foreach (string component in Components)
                if (string.Equals(component, wanted, StringComparison.OrdinalIgnoreCase)) return component;
            throw new McpError("'" + where + "' is one of x, y, z, Yaw, Pitch, Roll (rotation x is Pitch, y is Yaw, z is Roll).");
        }

        /// <summary>The data type a parameter of an entity holds: its value's, else its type's declared one.</summary>
        private static DataType? ParameterType(Commands commands, Composite composite, Entity entity, ShortGuid parameter)
        {
            ParameterData content = entity.GetParameter(parameter)?.content;
            if (content != null) return content.dataType;
            try
            {
                (ParameterVariant? _, DataType? type, ShortGuid __) = commands.Utils.GetParameterMetadata(entity, parameter, composite);
                return type;
            }
            catch
            {
                return null;
            }
        }

        private static ParameterData RestingContent(Commands commands, Composite composite, Entity entity, ShortGuid parameter)
        {
            ParameterData content = entity.GetParameter(parameter)?.content;
            if (content != null) return content;
            try { return commands.Utils.CreateDefaultParameterData(entity, composite, parameter); } catch { return null; }
        }

        private static string AnimatableHint(Commands commands, Composite composite, Entity entity)
        {
            List<string> names = new List<string>();
            foreach (Parameter parameter in entity.parameters)
                if (parameter.content is cFloat || parameter.content is cTransform) names.Add(McpScript.ParamName(parameter.name));
            try
            {
                foreach ((ShortGuid name, ParameterVariant variant, DataType type) in commands.Utils.GetAllParameters(entity, composite))
                    if (type == DataType.FLOAT || type == DataType.TRANSFORM) names.Add(McpScript.ParamName(name));
            }
            catch { }
            names = names.Distinct().ToList();
            return names.Count == 0 ? " It has no FLOAT or TRANSFORM parameters to animate." : " Parameters it has that can be animated: " + string.Join(", ", names.Take(40)) + (names.Count > 40 ? ", ..." : "") + ".";
        }

        private static CAGEAnimation.FloatTrack FindTrack(Working w, Target target, ShortGuid parameter, string component)
        {
            foreach (CAGEAnimation.Connection connection in w.Connections)
            {
                if (connection == null || connection.target_param != parameter || !string.Equals(ComponentName(connection.target_sub_param), component, StringComparison.OrdinalIgnoreCase)) continue;
                CAGEAnimation.FloatTrack track = w.FloatTrack(connection.target_track);
                if (track == null) continue;
                if (SameTarget(w.Commands, w.Composite, connection.connectedEntity, target)) return track;
            }
            return null;
        }

        /// <summary>A new connection and float track, as the editor's AddNewConnectionSet makes them (the keys come later).</summary>
        private static CAGEAnimation.FloatTrack AddTrack(Working w, Target target, ShortGuid parameter, DataType type, string component, float rest)
        {
            CAGEAnimation.FloatTrack track = new CAGEAnimation.FloatTrack() { shortGUID = ShortGuidUtils.GenerateRandom() };
            w.FloatTracks.Add(track);
            CAGEAnimation.Connection connection = new CAGEAnimation.Connection()
            {
                binding_guid = ShortGuidUtils.GenerateRandom(),
                binding_type = ObjectType.ENTITY,
                target_param = parameter,
                target_param_type = type,
                target_sub_param = ShortGuidUtils.Generate(component),
                target_track = track.shortGUID,
            };
            connection.connectedEntity = new EntityPath((ShortGuid[])target.Path.path.Clone());
            w.Connections.Add(connection);
            w.Created[track] = rest;
            w.Changed = true;
            return track;
        }

        private static Channel ResolveChannel(Working w, JObject spec, string where)
        {
            if (spec["track"] != null)
            {
                if (spec["target"] != null || spec["parameter"] != null || spec["component"] != null || spec["from_root"] != null)
                    throw new McpError(where + ": give 'track', or 'target' and 'parameter' - not both.");
                string reference = (string)spec["track"];
                ShortGuid? id = McpScript.ParseId(reference);
                CAGEAnimation.FloatTrack track = id == null ? null : w.FloatTrack(id.Value);
                if (track == null)
                    throw new McpError(where + ": this animation has no float track '" + reference + "'" + (id != null && w.EventTracks.Any(o => o.shortGUID == id.Value) ? " (that is an event track: set_animation_events edits those)" : "") + ". get_cage_animation lists its tracks.");
                CAGEAnimation.Connection connection = w.Connections.FirstOrDefault(o => o != null && o.target_track == track.shortGUID);
                return new Channel() { Track = track, Label = connection == null ? "track " + McpScript.Id(track.shortGUID) : TargetLabel(w.Commands, w.Composite, connection.connectedEntity) + "." + McpScript.ParamName(connection.target_param) + (ComponentName(connection.target_sub_param) == "" ? "" : "." + ComponentName(connection.target_sub_param)) };
            }

            string parameterName = (string)spec["parameter"];
            if (spec["target"] == null || string.IsNullOrWhiteSpace(parameterName))
                throw new McpError(where + " needs 'target' and 'parameter' (or 'track' for an existing track).");
            Target target = ResolveTarget(w.Commands, w.Composite, spec["target"], ReadFlag(spec["from_root"], where + ".from_root"), where + ".target");
            if (target.Entity is AliasEntity)
                throw new McpError(where + ": " + target.Name + " is an alias. Point the path at the entity it overrides, through its instances.");
            ShortGuid parameter = McpScript.ParamId(parameterName);
            string parameterLabel = McpScript.ParamName(parameter);
            DataType? type = ParameterType(w.Commands, target.Composite, target.Entity, parameter);
            if (type == null)
                throw new McpError(where + ": " + target.Name + " (" + McpScript.TypeName(w.Commands, target.Composite, target.Entity) + ") has no parameter '" + parameterName + "'." + AnimatableHint(w.Commands, target.Composite, target.Entity));
            if (type != DataType.FLOAT && type != DataType.TRANSFORM)
                throw new McpError(where + ": '" + parameterLabel + "' on " + target.Name + " is a " + type + "; a CAGEAnimation can drive FLOAT and TRANSFORM parameters only." + AnimatableHint(w.Commands, target.Composite, target.Entity));

            ParameterData resting = RestingContent(w.Commands, target.Composite, target.Entity, parameter);
            Channel channel = new Channel() { Target = target, Parameter = parameter, Label = target.Name + "." + parameterLabel, Rest = new Dictionary<string, float>() };
            string component = spec["component"] == null || spec["component"].Type == JTokenType.Null ? null : (string)spec["component"];

            if (type == DataType.FLOAT)
            {
                if (!string.IsNullOrWhiteSpace(component))
                    throw new McpError(where + ": '" + parameterLabel + "' is a FLOAT, which has no components.");
                float rest = ComponentValue(resting, "") ?? 0f;
                channel.Track = FindTrack(w, target, parameter, "");
                if (channel.Track == null)
                {
                    channel.Track = AddTrack(w, target, parameter, DataType.FLOAT, "", rest);
                    channel.Made.Add(parameterLabel);
                }
                return channel;
            }

            //A transform is driven through six tracks; animating it for the first time adds all six, as the editor does
            Dictionary<string, CAGEAnimation.FloatTrack> existing = Components.ToDictionary(o => o, o => FindTrack(w, target, parameter, o));
            foreach (string c in Components)
                channel.Rest[c] = ComponentValue(resting, c) ?? 0f;
            bool none = existing.Values.All(o => o == null);
            if (none)
            {
                foreach (string c in Components)
                    existing[c] = AddTrack(w, target, parameter, DataType.TRANSFORM, c, channel.Rest[c]);
                channel.Made.Add(parameterLabel + " (x, y, z, Yaw, Pitch, Roll)");
            }
            if (!string.IsNullOrWhiteSpace(component))
            {
                string c = CanonicalComponent(component, where + ".component");
                channel.Label += "." + c;
                channel.Track = existing[c];
                if (channel.Track == null)
                {
                    channel.Track = AddTrack(w, target, parameter, DataType.TRANSFORM, c, channel.Rest[c]);
                    channel.Made.Add(parameterLabel + "." + c);
                }
                return channel;
            }
            channel.Transform = existing;
            return channel;
        }

        private static KeySpec ReadKey(JToken token, string where)
        {
            if (token is JArray pair)
            {
                if (pair.Count != 2)
                    throw new McpError(where + " is [time, value].");
                return new KeySpec() { Time = ReadTime(pair[0], where + "[0]"), Value = ReadNumber(pair[1], where + "[1]") };
            }
            if (token is JObject item)
            {
                if (item["position"] != null || item["rotation"] != null)
                    throw new McpError(where + ": position/rotation keys are for a whole transform; leave out 'component' to key one that way, or give {time, value}.");
                CheckFields(item, where, "time", "value", "tan_in", "tan_out");
                if (item["value"] == null)
                    throw new McpError(where + " needs a 'value'.");
                KeySpec key = new KeySpec() { Time = ReadTime(item["time"], where + ".time"), Value = ReadNumber(item["value"], where + ".value") };
                if (item["tan_in"] != null) key.TanIn = ReadTangent(item["tan_in"], where + ".tan_in");
                if (item["tan_out"] != null) key.TanOut = ReadTangent(item["tan_out"], where + ".tan_out");
                return key;
            }
            throw new McpError(where + " is [time, value] or {time, value, tan_in?, tan_out?}.");
        }

        private static Vector2 ReadTangent(JToken token, string where)
        {
            if (!(token is JArray pair) || pair.Count != 2)
                throw new McpError("'" + where + "' is [seconds, value]: the control point sits a third of it away from the key.");
            float dt = ReadNumber(pair[0], where + "[0]");
            float dv = ReadNumber(pair[1], where + "[1]");
            if (dt < 0f)
                throw new McpError("'" + where + "' cannot have negative seconds (the curve would run backwards in time).");
            return new Vector2(dt, dv);
        }

        /// <summary>A whole-transform key, split into the component values it gives.</summary>
        private static Dictionary<string, KeySpec> ReadTransformKey(JToken token, string where)
        {
            if (!(token is JObject item))
                throw new McpError(where + ": keys of a whole transform are {time, position?: [x,y,z], rotation?: [x,y,z]}; to key one component give 'component'.");
            CheckFields(item, where, "time", "position", "rotation");
            float time = ReadTime(item["time"], where + ".time");
            if (item["position"] == null && item["rotation"] == null)
                throw new McpError(where + " needs 'position' and/or 'rotation'.");
            Dictionary<string, KeySpec> keys = new Dictionary<string, KeySpec>();
            if (item["position"] != null)
                ReadTransformAxes(item["position"], where + ".position", time, keys, "x", "y", "z");
            if (item["rotation"] != null)
                ReadTransformAxes(item["rotation"], where + ".rotation", time, keys, "Pitch", "Yaw", "Roll");
            return keys;
        }

        /// <summary>
        /// [x, y, z], or {x?, y?, z?} keying only the axes it gives (the others are left alone, not keyed at 0), onto the
        /// component tracks <paramref name="components"/> names for x, y and z. Each value is checked as a scalar key's is.
        /// </summary>
        private static void ReadTransformAxes(JToken token, string where, float time, Dictionary<string, KeySpec> keys, params string[] components)
        {
            string[] axes = { "x", "y", "z" };
            if (token is JArray array)
            {
                if (array.Count != 3)
                    throw new McpError("'" + where + "' takes three numbers [x, y, z].");
                for (int i = 0; i < 3; i++)
                    keys[components[i]] = new KeySpec() { Time = time, Value = ReadNumber(array[i], where + "[" + i + "]") };
                return;
            }
            if (token is JObject obj)
            {
                CheckFields(obj, where, axes);
                int given = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (obj[axes[i]] == null) continue;
                    keys[components[i]] = new KeySpec() { Time = time, Value = ReadNumber(obj[axes[i]], where + "." + axes[i]) };
                    given++;
                }
                if (given == 0)
                    throw new McpError("'" + where + "' gives none of x, y or z.");
                return;
            }
            throw new McpError("'" + where + "' takes three numbers [x, y, z], or {x?, y?, z?} to key only some of them.");
        }

        private static void ApplySet(Working w, JObject spec, string where, List<string> notes)
        {
            CheckFields(spec, where, "target", "from_root", "track", "parameter", "component", "keys", "replace", "remove_times", "move", "tangents");
            bool replace = ReadFlag(spec["replace"], where + ".replace");
            JArray keyTokens = spec["keys"] == null || spec["keys"].Type == JTokenType.Null ? null : spec["keys"] as JArray ?? throw new McpError("'" + where + ".keys' is a list of keys.");
            List<float> removeTimes = new List<float>();
            if (spec["remove_times"] != null && spec["remove_times"].Type != JTokenType.Null)
            {
                if (!(spec["remove_times"] is JArray times)) throw new McpError("'" + where + ".remove_times' is a list of times.");
                for (int i = 0; i < times.Count; i++) removeTimes.Add(ReadTime(times[i], where + ".remove_times[" + i + "]"));
            }
            List<KeyValuePair<float, float>> moves = new List<KeyValuePair<float, float>>();
            if (spec["move"] != null && spec["move"].Type != JTokenType.Null)
            {
                if (!(spec["move"] is JArray list)) throw new McpError("'" + where + ".move' is a list of {from, to}.");
                for (int i = 0; i < list.Count; i++)
                {
                    JObject move = ItemAt(list[i], where + ".move[" + i + "]");
                    CheckFields(move, where + ".move[" + i + "]", "from", "to");
                    moves.Add(new KeyValuePair<float, float>(ReadTime(move["from"], where + ".move[" + i + "].from"), ReadTime(move["to"], where + ".move[" + i + "].to")));
                }
            }
            string tangents = spec["tangents"] == null || spec["tangents"].Type == JTokenType.Null ? null : ((string)spec["tangents"]).Trim().ToLowerInvariant();
            if (tangents != null && tangents != "flat" && tangents != "smooth" && tangents != "straight")
                throw new McpError("'" + where + ".tangents' is flat, smooth or straight.");
            if (replace && keyTokens == null)
                throw new McpError(where + ": 'replace' needs the 'keys' to replace them with.");
            if (replace && (removeTimes.Count != 0 || moves.Count != 0))
                throw new McpError(where + ": 'replace' rewrites every key; do not combine it with remove_times or move.");

            Channel channel = ResolveChannel(w, spec, where);
            List<string> summary = new List<string>();
            if (channel.Made.Count != 0) summary.Add("animated " + string.Join(", ", channel.Made));

            if (channel.Track != null)
            {
                List<KeySpec> keys = new List<KeySpec>();
                if (keyTokens != null)
                    for (int i = 0; i < keyTokens.Count; i++) keys.Add(ReadKey(keyTokens[i], where + ".keys[" + i + "]"));
                int done = EditTrack(w, channel.Track, channel.Label, keys, replace, removeTimes, moves, tangents, true, where);
                if (done != 0) summary.Add(Summary(keys.Count, removeTimes.Count, moves.Count, replace));
            }
            else
            {
                //A whole transform: each key's position and rotation go to their component tracks
                Dictionary<string, List<KeySpec>> perComponent = Components.ToDictionary(o => o, o => new List<KeySpec>());
                int keyCount = 0;
                if (keyTokens != null)
                {
                    for (int i = 0; i < keyTokens.Count; i++)
                    {
                        foreach (KeyValuePair<string, KeySpec> part in ReadTransformKey(keyTokens[i], where + ".keys[" + i + "]"))
                            perComponent[part.Key].Add(part.Value);
                        keyCount++;
                    }
                }
                foreach (string c in Components)
                {
                    if (channel.Transform[c] == null && perComponent[c].Count != 0)
                    {
                        channel.Transform[c] = AddTrack(w, channel.Target, channel.Parameter, DataType.TRANSFORM, c, channel.Rest[c]);
                        summary.Add("animated " + McpScript.ParamName(channel.Parameter) + "." + c);
                    }
                }
                //Removing or moving a key of a whole transform goes to whichever of its tracks have one there
                foreach (float time in removeTimes)
                    if (!Components.Any(c => channel.Transform[c] != null && CageAnimationCurves.FindKeyframe(channel.Transform[c], time) != null))
                        throw new McpError(where + ": " + channel.Label + " has no key at " + R(time) + " s.");
                foreach (KeyValuePair<float, float> move in moves)
                    if (!Components.Any(c => channel.Transform[c] != null && CageAnimationCurves.FindKeyframe(channel.Transform[c], move.Key) != null))
                        throw new McpError(where + ": " + channel.Label + " has no key at " + R(move.Key) + " s to move.");
                int done = 0;
                foreach (string c in Components)
                {
                    CAGEAnimation.FloatTrack track = channel.Transform[c];
                    if (track == null) continue;
                    bool rewrite = replace && perComponent[c].Count != 0;
                    done += EditTrack(w, track, channel.Label + "." + c, perComponent[c], rewrite, removeTimes, moves, tangents, false, where);
                }
                if (done != 0)
                    summary.Add(Summary(keyCount, removeTimes.Count, moves.Count, replace));
            }
            if (summary.Count != 0)
                w.Changes.Add(channel.Label + ": " + string.Join("; ", summary));
        }

        private static string Summary(int keys, int removed, int moved, bool replace)
        {
            List<string> parts = new List<string>();
            if (keys != 0) parts.Add((replace ? "replaced with " : "wrote ") + keys + (keys == 1 ? " key" : " keys"));
            if (removed != 0) parts.Add("removed " + removed);
            if (moved != 0) parts.Add("moved " + moved);
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Change one float track's keys: deletions, then moves, then the keys given (a key at an existing time
        /// updates it, as CageAnimationCurves.SetKeyframe does). Returns how many keys it changed.
        /// </summary>
        private static int EditTrack(Working w, CAGEAnimation.FloatTrack track, string label, List<KeySpec> keys, bool replace, List<float> removeTimes, List<KeyValuePair<float, float>> moves, string tangents, bool strict, string where)
        {
            w.Touched.Add(track);
            int changed = 0;
            for (int i = 0; i < keys.Count; i++)
                for (int j = i + 1; j < keys.Count; j++)
                    if (Math.Abs(keys[i].Time - keys[j].Time) <= Eps)
                        throw new McpError(where + ": two keys for " + label + " are both at " + R(keys[i].Time) + " s.");

            if (replace)
            {
                changed += track.keyframes.Count;
                track.keyframes = new List<CAGEAnimation.FloatTrack.Keyframe>();
                w.Exact.Add(track);
            }
            foreach (float time in removeTimes)
            {
                CAGEAnimation.FloatTrack.Keyframe key = CageAnimationCurves.FindKeyframe(track, time);
                if (key == null)
                {
                    if (strict) throw new McpError(where + ": " + label + " has no key at " + R(time) + " s (its keys are at " + Times(track) + ").");
                    continue;
                }
                track.keyframes.Remove(key);
                changed++;
            }
            foreach (KeyValuePair<float, float> move in moves)
            {
                CAGEAnimation.FloatTrack.Keyframe key = CageAnimationCurves.FindKeyframe(track, move.Key);
                if (key == null)
                {
                    if (strict) throw new McpError(where + ": " + label + " has no key at " + R(move.Key) + " s to move (its keys are at " + Times(track) + ").");
                    continue;
                }
                CAGEAnimation.FloatTrack.Keyframe there = CageAnimationCurves.FindKeyframe(track, move.Value);
                if (there != null && there != key)
                    throw new McpError(where + ": " + label + " already has a key at " + R(move.Value) + " s; remove it first (remove_times).");
                CageAnimationCurves.SetTime(key, move.Value);
                changed++;
            }

            Dictionary<CAGEAnimation.FloatTrack.Keyframe, (bool inside, bool outside)> automatic = new Dictionary<CAGEAnimation.FloatTrack.Keyframe, (bool, bool)>();
            foreach (KeySpec spec in keys)
            {
                CAGEAnimation.FloatTrack.Keyframe key = CageAnimationCurves.FindKeyframe(track, spec.Time);
                bool made = key == null;
                if (made)
                {
                    key = CageAnimationCurves.NewKeyframe(spec.Time, spec.Value, w.Mode);
                    track.keyframes.Add(key);
                    changed++;
                }
                else if (Math.Abs(key.value.Y - spec.Value) > 1e-6f)
                {
                    key.value.Y = spec.Value;
                    changed++;
                }
                if (spec.TanIn.HasValue) { key.tan_in = spec.TanIn.Value; changed++; }
                if (spec.TanOut.HasValue) { key.tan_out = spec.TanOut.Value; changed++; }
                //New keys get sized tangents; an updated key keeps its own unless a preset was asked for
                if (made || tangents != null)
                    automatic[key] = (!spec.TanIn.HasValue, !spec.TanOut.HasValue);
            }
            track.keyframes.Sort((a, b) => a.time.CompareTo(b.time));
            foreach (KeyValuePair<CAGEAnimation.FloatTrack.Keyframe, (bool inside, bool outside)> key in automatic)
                SetTangents(track, key.Key, tangents ?? "flat", key.Value.inside, key.Value.outside);
            if (changed != 0) w.Changed = true;
            return changed;
        }

        /// <summary>
        /// Tangents sized to the neighbouring segments (a tangent's seconds equal to the segment is weight one,
        /// as exported retail keys carry): flat eases in and out, smooth passes through at the slope between
        /// the neighbours, straight points at them (a straight Bezier segment).
        /// </summary>
        private static void SetTangents(CAGEAnimation.FloatTrack track, CAGEAnimation.FloatTrack.Keyframe key, string preset, bool setIn, bool setOut)
        {
            int index = track.keyframes.IndexOf(key);
            CAGEAnimation.FloatTrack.Keyframe previous = index > 0 ? track.keyframes[index - 1] : null;
            CAGEAnimation.FloatTrack.Keyframe next = index >= 0 && index < track.keyframes.Count - 1 ? track.keyframes[index + 1] : null;
            float before = previous != null ? key.time - previous.time : 0f;
            float after = next != null ? next.time - key.time : 0f;
            if (before <= 1e-6f) before = after > 1e-6f ? after : 1f;
            if (after <= 1e-6f) after = before > 1e-6f ? before : 1f;

            float slopeIn = 0f, slopeOut = 0f;
            switch (preset)
            {
                case "straight":
                    if (previous != null) slopeIn = (key.value.Y - previous.value.Y) / before;
                    if (next != null) slopeOut = (next.value.Y - key.value.Y) / after;
                    break;
                case "smooth":
                    if (previous != null && next != null && next.time - previous.time > 1e-6f)
                        slopeIn = slopeOut = (next.value.Y - previous.value.Y) / (next.time - previous.time);
                    break;
            }
            if (setIn) key.tan_in = new Vector2(before, slopeIn * before);
            if (setOut) key.tan_out = new Vector2(after, slopeOut * after);
        }

        /// <summary>
        /// A tangent reaching more than a whole segment past its neighbour makes the Bezier curve run backwards in
        /// time there. Keys added between existing ones shorten segments, so on the tracks this call touched such
        /// tangents are brought back to weight one, keeping their slope.
        /// </summary>
        private static void ClampTangents(Working w, List<string> notes)
        {
            int clamped = 0;
            foreach (CAGEAnimation.FloatTrack track in w.Touched)
            {
                if (!w.FloatTracks.Contains(track)) continue;
                List<CAGEAnimation.FloatTrack.Keyframe> keys = track.keyframes;
                for (int i = 0; i < keys.Count; i++)
                {
                    CAGEAnimation.FloatTrack.Keyframe key = keys[i];
                    if (i < keys.Count - 1)
                    {
                        float segment = keys[i + 1].time - key.time;
                        if (segment > 1e-6f && key.tan_out.X > 3f * segment)
                        {
                            key.tan_out = new Vector2(segment, key.tan_out.Y * segment / key.tan_out.X);
                            clamped++;
                        }
                    }
                    if (i > 0)
                    {
                        float segment = key.time - keys[i - 1].time;
                        if (segment > 1e-6f && key.tan_in.X > 3f * segment)
                        {
                            key.tan_in = new Vector2(segment, key.tan_in.Y * segment / key.tan_in.X);
                            clamped++;
                        }
                    }
                }
            }
            if (clamped != 0)
            {
                w.Changed = true;
                notes.Add(clamped + " tangent(s) reached past a neighbouring key (the curve would have run backwards in time) and were shortened to that segment, keeping their slope.");
            }
        }

        private static void RemoveTracks(Working w, JObject spec, string where)
        {
            CheckFields(spec, where, "track", "target", "from_root", "parameter", "component");
            List<CAGEAnimation.Connection> going;
            string label;
            if (spec["track"] != null)
            {
                if (spec["target"] != null || spec["parameter"] != null || spec["component"] != null || spec["from_root"] != null)
                    throw new McpError(where + ": give 'track', or 'target' (with parameter/component) - not both.");
                string reference = (string)spec["track"];
                ShortGuid? id = McpScript.ParseId(reference);
                CAGEAnimation.FloatTrack track = id == null ? null : w.FloatTrack(id.Value);
                if (track == null)
                    throw new McpError(where + ": this animation has no float track '" + reference + "'" + (id != null && w.EventTracks.Any(o => o.shortGUID == id.Value) ? " (that is an event track: set_animation_events removes those)" : "") + ".");
                //RemoveFloatTrack: the track and every connection to it
                w.FloatTracks.Remove(track);
                int connections = w.Connections.RemoveAll(o => o != null && o.target_track == track.shortGUID);
                w.Changed = true;
                w.Changes.Add("removed track " + McpScript.Id(track.shortGUID) + (connections == 0 ? " (it drove nothing)" : ""));
                return;
            }

            if (spec["target"] == null)
                throw new McpError(where + " needs 'track', or 'target' (with 'parameter' and 'component' to narrow it).");
            Target target = ResolveTarget(w.Commands, w.Composite, spec["target"], ReadFlag(spec["from_root"], where + ".from_root"), where + ".target");
            ShortGuid? parameter = spec["parameter"] == null || spec["parameter"].Type == JTokenType.Null ? (ShortGuid?)null : McpScript.ParamId((string)spec["parameter"]);
            string component = null;
            if (spec["component"] != null && spec["component"].Type != JTokenType.Null)
            {
                if (parameter == null) throw new McpError(where + ": 'component' needs 'parameter'.");
                component = CanonicalComponent((string)spec["component"], where + ".component");
            }
            going = w.Connections.Where(o => o != null && w.FloatTrack(o.target_track) != null
                && (parameter == null || o.target_param == parameter.Value)
                && (component == null || string.Equals(ComponentName(o.target_sub_param), component, StringComparison.OrdinalIgnoreCase))
                && SameTarget(w.Commands, w.Composite, o.connectedEntity, target)).ToList();
            if (going.Count == 0)
            {
                List<string> driven = w.Connections.Where(o => o != null && w.FloatTrack(o.target_track) != null && SameTarget(w.Commands, w.Composite, o.connectedEntity, target))
                    .Select(o => McpScript.ParamName(o.target_param) + (ComponentName(o.target_sub_param) == "" ? "" : "." + ComponentName(o.target_sub_param))).Distinct().ToList();
                throw new McpError(where + ": the animation drives " + (driven.Count == 0 ? "nothing on " + target.Name : "only " + string.Join(", ", driven) + " on " + target.Name) + ".");
            }
            label = target.Name + (parameter == null ? "" : "." + McpScript.ParamName(parameter.Value)) + (component == null ? "" : "." + component);

            //RemoveEntityFromAnimation / RemoveFloatTrack: the connections, and their tracks unless another connection still plays one
            HashSet<CAGEAnimation.Connection> goingSet = new HashSet<CAGEAnimation.Connection>(going);
            w.Connections.RemoveAll(o => goingSet.Contains(o));
            HashSet<ShortGuid> stillPlayed = new HashSet<ShortGuid>(w.Connections.Where(o => o != null).Select(o => o.target_track));
            int tracks = w.FloatTracks.RemoveAll(o => o != null && going.Any(c => c.target_track == o.shortGUID) && !stillPlayed.Contains(o.shortGUID));
            w.Changed = true;
            w.Changes.Add("stopped animating " + label + " (" + tracks + (tracks == 1 ? " track" : " tracks") + " removed)");
        }
        #endregion

        #region set_animation_events
        private static readonly string[] BindingSlots = { "marker", "character", "camera" };

        private static ObjectType SlotType(string slot)
        {
            switch (slot)
            {
                case "marker": return ObjectType.MARKER;
                case "character": return ObjectType.CHARACTER;
                default: return ObjectType.CAMERA;
            }
        }

        /// <summary>What each binding may point at (CAGEAnimationEditor.FunctionTypesForBinding).</summary>
        private static FunctionType[] SlotFunctions(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.MARKER: return new[] { FunctionType.PositionMarker, FunctionType.ModelReference };
                case ObjectType.CHARACTER: return new[] { FunctionType.Character, FunctionType.VariableThePlayer };
                default: return new[] { FunctionType.CameraResource };
            }
        }

        private static JObject Merge(JObject a, JObject b)
        {
            JObject merged = (JObject)a.DeepClone();
            foreach (JProperty property in b.Properties())
                merged[property.Name] = property.Value.DeepClone();
            return merged;
        }

        private sealed class EventWork
        {
            public Working W;
            public readonly Dictionary<string, CAGEAnimation.EventTrack> Refs = new Dictionary<string, CAGEAnimation.EventTrack>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Notes = new List<string>();
            public readonly HashSet<CAGEAnimation.EventTrack> Touched = new HashSet<CAGEAnimation.EventTrack>();
        }

        private static object SetAnimationEvents(McpCall call)
        {
            string[] parts = { "add_tracks", "remove_tracks", "add", "remove", "move", "rename", "bindings" };
            if (!parts.Any(call.Has))
                throw new McpError("Nothing to do: give add, remove, move, rename, add_tracks, remove_tracks or bindings.");

            return McpEditor.UI(() =>
            {
                Working w = Begin(call);
                CompileIfShown(w.Composite);
                EventWork e = new EventWork() { W = w };

                //Tracks first, so the rest of the call can name the new ones
                List<string> removeTracks = call.StrList("remove_tracks");
                foreach (string reference in removeTracks)
                {
                    CAGEAnimation.EventTrack track = FindEventTrack(e, reference, "remove_tracks");
                    w.EventTracks.Remove(track);
                    w.Connections.RemoveAll(o => o != null && o.target_track == track.shortGUID);
                    w.Changed = true;
                    w.Changes.Add("removed event track " + McpScript.Id(track.shortGUID) + " (" + track.keyframes.Count + " events)");
                }
                JArray addTracks = call.Array("add_tracks");
                for (int i = 0; i < addTracks.Count; i++)
                {
                    string where = "add_tracks[" + i + "]";
                    JObject spec = ItemAt(addTracks[i], where);
                    CheckFields(spec, where, "type", "ref", "marker", "character", "camera", "from_root");
                    string type = ((string)spec["type"] ?? "").Trim().ToLowerInvariant();
                    if (type != "string" && type != "entity")
                        throw new McpError("'" + where + ".type' is string or entity.");
                    CAGEAnimation.EventTrack track = NewEventTrack(w, type == "entity" ? ANIM_TRACK_TYPE.T_GUID : ANIM_TRACK_TYPE.T_STRING);
                    string reference = (string)spec["ref"];
                    if (!string.IsNullOrWhiteSpace(reference))
                    {
                        if (e.Refs.ContainsKey(reference.Trim())) throw new McpError("'" + where + ".ref' '" + reference + "' is used twice.");
                        e.Refs[reference.Trim()] = track;
                    }
                    w.Changes.Add("added " + type + " event track " + McpScript.Id(track.shortGUID) + (string.IsNullOrWhiteSpace(reference) ? "" : " ('" + reference.Trim() + "')"));
                    ApplyBindings(e, track, spec, where);
                }
                JArray bindings = call.Array("bindings");
                for (int i = 0; i < bindings.Count; i++)
                {
                    string where = "bindings[" + i + "]";
                    JObject spec = ItemAt(bindings[i], where);
                    CheckFields(spec, where, "track", "marker", "character", "camera", "from_root");
                    if (!BindingSlots.Any(o => spec[o] != null))
                        throw new McpError(where + " needs marker, character or camera.");
                    ApplyBindings(e, FindEventTrack(e, (string)spec["track"], where + ".track"), spec, where);
                }

                JArray removes = call.Array("remove");
                for (int i = 0; i < removes.Count; i++)
                    RemoveEvents(e, ItemAt(removes[i], "remove[" + i + "]"), "remove[" + i + "]");
                JArray renames = call.Array("rename");
                for (int i = 0; i < renames.Count; i++)
                    RenameEvents(e, ItemAt(renames[i], "rename[" + i + "]"), "rename[" + i + "]");
                JArray moves = call.Array("move");
                for (int i = 0; i < moves.Count; i++)
                    MoveEvent(e, ItemAt(moves[i], "move[" + i + "]"), "move[" + i + "]");
                JArray adds = call.Array("add");
                for (int i = 0; i < adds.Count; i++)
                    AddEvent(e, ItemAt(adds[i], "add[" + i + "]"), "add[" + i + "]");

                //Lanes are kept in time order, as the curve editor leaves them after a drag
                foreach (CAGEAnimation.EventTrack track in e.Touched)
                    track.keyframes = track.keyframes.OrderBy(o => o.time).ToList();

                //The editor's guard (ConfirmRemovingStringEvents): pins that go away take their links with them
                List<ShortGuid> before = EventPins(w.Animation.eventTracks);
                List<ShortGuid> after = EventPins(w.EventTracks);
                List<ShortGuid> gone = before.Where(o => !after.Contains(o)).ToList();
                JArray broken = new JArray();
                foreach (ShortGuid pin in gone)
                {
                    int links = LinkCount(w.Composite, w.Animation, pin);
                    if (links != 0) broken.Add(new JObject() { ["pin"] = McpScript.ParamName(pin), ["links"] = links });
                }
                if (broken.Count != 0 && !call.Bool("allow_breaking_links"))
                    throw new McpError("These event pins are still linked, and this would remove them: " + string.Join(", ", broken.Select(o => (string)o["pin"] + " (" + (int)o["links"] + ")")) + ". Those links would break. Re-link them first, or pass allow_breaking_links: true.");

                string label = "Edit events of " + McpScript.EntityName(w.Commands, w.Composite, w.Animation);
                bool changed = w.Commit(label);
                if (changed)
                    McpScriptEdit.Show(w.Composite, new List<Entity>() { w.Animation });

                JObject described = Describe(w.Commands, w.Composite, w.Animation, new DescribeOptions() { MaxKeys = 0, MaxTracks = 0 });
                JObject answer = new JObject() { ["changed"] = changed };
                if (changed && w.Changes.Count != 0) answer["changes"] = new JArray(w.Changes);
                if (e.Refs.Count != 0) answer["new_tracks"] = new JObject(e.Refs.Select(o => new JProperty(o.Key, McpScript.Id(o.Value.shortGUID))));
                List<ShortGuid> added = after.Where(o => !before.Contains(o)).ToList();
                if (added.Count != 0) answer["pins_added"] = new JArray(added.Select(McpScript.ParamName));
                if (gone.Count != 0) answer["pins_removed"] = new JArray(gone.Select(McpScript.ParamName));
                if (broken.Count != 0 && changed)
                {
                    answer["broken_links"] = broken;
                    call.Note("Links on the removed pins are still in the script, pointing at pins that no longer exist: remove_links (from this CAGEAnimation, param = the pin) clears them.");
                }
                foreach (string key in new[] { "composite", "id", "name", "length", "event_tracks", "event_pins", "connections_without_track" })
                    if (described[key] != null) answer[key] = described[key];
                foreach (string note in e.Notes) call.Note(note);
                if (!changed) call.Note("Nothing changed, so no undo step was made.");
                return answer;
            });
        }

        private static CAGEAnimation.EventTrack NewEventTrack(Working w, ANIM_TRACK_TYPE type)
        {
            //CreateEventTrack: a random id and the lane's type
            CAGEAnimation.EventTrack track = new CAGEAnimation.EventTrack() { shortGUID = ShortGuidUtils.GenerateRandom(), track_type = type };
            w.EventTracks.Add(track);
            w.Changed = true;
            return track;
        }

        private static CAGEAnimation.EventTrack FindEventTrack(EventWork e, string reference, string where)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new McpError("'" + where + "' names no track.");
            if (e.Refs.TryGetValue(reference.Trim(), out CAGEAnimation.EventTrack byRef))
                return byRef;
            ShortGuid? id = McpScript.ParseId(reference);
            CAGEAnimation.EventTrack track = id == null ? null : e.W.EventTracks.FirstOrDefault(o => o != null && o.shortGUID == id.Value);
            if (track != null) return track;
            bool isFloat = id != null && e.W.FloatTrack(id.Value) != null;
            throw new McpError("'" + where + "': this animation has no event track '" + reference + "'" + (isFloat ? " (that is a float track: animate_parameters edits those)" : "")
                + ". Its event tracks: " + (e.W.EventTracks.Count == 0 ? "none" : string.Join(", ", e.W.EventTracks.Select(o => McpScript.Id(o.shortGUID) + " (" + TrackTypeName(o) + ")"))) + ".");
        }

        private static bool IsBound(Working w, CAGEAnimation.EventTrack track) => w.Connections.Any(o => o != null && o.target_track == track.shortGUID);

        /// <summary>Where a string event goes when no track is named: the animation's own (unbound) string lane, made if missing (GetOrCreateSelfEventTrack).</summary>
        private static CAGEAnimation.EventTrack DefaultStringTrack(EventWork e)
        {
            CAGEAnimation.EventTrack track = e.W.EventTracks.FirstOrDefault(o => o != null && TrackTypeName(o) == "string" && !IsBound(e.W, o));
            if (track != null) return track;
            track = NewEventTrack(e.W, ANIM_TRACK_TYPE.T_STRING);
            e.W.Changes.Add("added string event track " + McpScript.Id(track.shortGUID));
            return track;
        }

        private static CAGEAnimation.EventTrack DefaultEntityTrack(EventWork e, string where)
        {
            List<CAGEAnimation.EventTrack> tracks = e.W.EventTracks.Where(o => o != null && TrackTypeName(o) == "entity").ToList();
            if (tracks.Count == 1) return tracks[0];
            if (tracks.Count > 1)
                throw new McpError(where + ": the animation has " + tracks.Count + " animation-entity tracks (" + string.Join(", ", tracks.Select(o => McpScript.Id(o.shortGUID))) + "); say which with 'track'.");
            CAGEAnimation.EventTrack track = NewEventTrack(e.W, ANIM_TRACK_TYPE.T_GUID);
            e.W.Changes.Add("added entity event track " + McpScript.Id(track.shortGUID));
            return track;
        }

        private static bool IsClear(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return true;
            if (value is JArray array) return array.Count == 0;
            if (value.Type == JTokenType.String)
            {
                string text = ((string)value).Trim();
                return text.Length == 0 || string.Equals(text, "none", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static void ApplyBindings(EventWork e, CAGEAnimation.EventTrack track, JObject spec, string where)
        {
            Working w = e.W;
            bool fromRoot = ReadFlag(spec["from_root"], where + ".from_root");
            foreach (string slot in BindingSlots)
            {
                if (spec.Property(slot) == null) continue;
                //Only an animation-entity lane has the binding slots (the editor shows them for T_GUID tracks)
                if (TrackTypeName(track) != "entity")
                    throw new McpError(where + ": only animation-entity tracks take marker/character/camera bindings; " + McpScript.Id(track.shortGUID) + " is a " + TrackTypeName(track) + " track.");
                ObjectType type = SlotType(slot);
                JToken value = spec[slot];
                if (IsClear(value))
                {
                    int removed = w.Connections.RemoveAll(o => o != null && o.target_track == track.shortGUID && o.binding_type == type);
                    if (removed != 0)
                    {
                        w.Changed = true;
                        w.Changes.Add("unbound the " + slot + " of track " + McpScript.Id(track.shortGUID));
                    }
                    else
                        e.Notes.Add("Track " + McpScript.Id(track.shortGUID) + " had no " + slot + " to unbind.");
                    continue;
                }
                Target target = ResolveTarget(w.Commands, w.Composite, value, fromRoot, where + "." + slot);
                FunctionType[] allowed = SlotFunctions(type);
                if (!(target.Entity is FunctionEntity function) || !function.function.IsFunctionType || !allowed.Contains(function.function.AsFunctionType))
                    throw new McpError(where + "." + slot + ": " + target.Name + " is a " + McpScript.TypeName(w.Commands, target.Composite, target.Entity) + "; a " + slot + " binding takes " + string.Join(" or ", allowed) + ".");
                //FindOrCreateBindingConnection: one connection per binding type on the track
                CAGEAnimation.Connection connection = w.Connections.FirstOrDefault(o => o != null && o.target_track == track.shortGUID && o.binding_type == type);
                if (connection == null)
                {
                    connection = new CAGEAnimation.Connection() { binding_guid = ShortGuidUtils.GenerateRandom(), target_track = track.shortGUID, binding_type = type };
                    w.Connections.Add(connection);
                }
                else if (connection.connectedEntity == target.Path)
                    continue;
                connection.connectedEntity = new EntityPath((ShortGuid[])target.Path.path.Clone());
                w.Changed = true;
                w.Changes.Add("bound the " + slot + " of track " + McpScript.Id(track.shortGUID) + " to " + target.Name);
            }
        }

        /// <summary>An entity event's entity: a play-animation function in the animation's own composite, where the game looks it up.</summary>
        private static FunctionEntity EventEntity(Working w, string reference, string where)
        {
            Entity entity = McpScript.FindEntity(w.Commands, w.Composite, reference);
            if (entity is FunctionEntity function && function.function.IsFunctionType && EventEntityTypes.Contains(function.function.AsFunctionType))
                return function;
            throw new McpError("'" + where + "': " + McpScript.EntityName(w.Commands, w.Composite, entity) + " is a " + McpScript.TypeName(w.Commands, w.Composite, entity) + "; an animation-entity event fires a CMD_PlayAnimation, CameraPlayAnimation or PlayEnvironmentAnimation in " + w.Composite.name + ".");
        }

        private static void AddEvent(EventWork e, JObject spec, string where)
        {
            Working w = e.W;
            CheckFields(spec, where, "time", "event", "entity", "duration", "track");
            float time = ReadTime(spec["time"], where + ".time");
            bool named = spec["event"] != null && spec["event"].Type != JTokenType.Null;
            bool entity = spec["entity"] != null && spec["entity"].Type != JTokenType.Null;
            if (named == entity)
                throw new McpError(where + " needs one of 'event' (a string event's name) or 'entity' (an animation entity).");
            float duration = 0f;
            if (spec["duration"] != null && spec["duration"].Type != JTokenType.Null)
            {
                duration = ReadNumber(spec["duration"], where + ".duration");
                if (duration < 0f) throw new McpError("'" + where + ".duration' cannot be negative.");
            }
            string trackRef = spec["track"] == null || spec["track"].Type == JTokenType.Null ? null : (string)spec["track"];

            CAGEAnimation.EventTrack track;
            CAGEAnimation.EventTrack.Keyframe key;
            string label;
            if (named)
            {
                string name = ((string)spec["event"]).Trim();
                if (name.Length == 0) throw new McpError("'" + where + ".event' is empty.");
                track = trackRef != null ? FindEventTrack(e, trackRef, where + ".track") : DefaultStringTrack(e);
                if (TrackTypeName(track) != "string")
                    throw new McpError(where + ": track " + McpScript.Id(track.shortGUID) + " is a " + TrackTypeName(track) + " track; string events go on a string track (leave 'track' out to use the animation's own).");
                ShortGuid forward = ShortGuidUtils.Generate(name, false);
                if (track.keyframes.Any(o => o.track_type == ANIM_TRACK_TYPE.T_STRING && o.forward == forward && Math.Abs(o.time - time) <= Eps))
                {
                    e.Notes.Add("'" + name + "' is already at " + R(time) + " s on track " + McpScript.Id(track.shortGUID) + ".");
                    return;
                }
                //The editor's EventTrack.Keyframe(time, name): forward = name, reverse = reverse_ + name
                key = new CAGEAnimation.EventTrack.Keyframe(time, name) { duration = duration };
                label = "'" + name + "'";
            }
            else
            {
                FunctionEntity function = EventEntity(w, (string)spec["entity"], where + ".entity");
                track = trackRef != null ? FindEventTrack(e, trackRef, where + ".track") : DefaultEntityTrack(e, where);
                if (TrackTypeName(track) != "entity")
                    throw new McpError(where + ": track " + McpScript.Id(track.shortGUID) + " is a " + TrackTypeName(track) + " track; animation-entity events go on an entity track (add_tracks with type entity makes one).");
                if (track.keyframes.Any(o => o.track_type == ANIM_TRACK_TYPE.T_GUID && o.forward == function.shortGUID && Math.Abs(o.time - time) <= Eps))
                {
                    e.Notes.Add(McpScript.EntityName(w.Commands, w.Composite, function) + " is already at " + R(time) + " s on track " + McpScript.Id(track.shortGUID) + ".");
                    return;
                }
                key = new CAGEAnimation.EventTrack.Keyframe(time, function) { duration = duration };
                label = McpScript.EntityName(w.Commands, w.Composite, function);
            }
            track.keyframes.Add(key);
            e.Touched.Add(track);
            w.Changed = true;
            w.Changes.Add("added " + label + " at " + R(time) + " s on track " + McpScript.Id(track.shortGUID));
        }

        /// <summary>The events a remove or move entry picks: every given field must match.</summary>
        private static List<(CAGEAnimation.EventTrack track, CAGEAnimation.EventTrack.Keyframe key)> MatchEvents(EventWork e, JObject spec, string where, string timeField)
        {
            Working w = e.W;
            JToken timeToken = spec[timeField];
            float? time = timeToken == null || timeToken.Type == JTokenType.Null ? (float?)null : ReadTime(timeToken, where + "." + timeField);
            ShortGuid? named = null, entity = null;
            if (spec["event"] != null && spec["event"].Type != JTokenType.Null)
            {
                string name = ((string)spec["event"]).Trim();
                named = McpScript.ParseId(name) ?? ShortGuidUtils.Generate(name, false);
            }
            if (spec["entity"] != null && spec["entity"].Type != JTokenType.Null)
            {
                string reference = (string)spec["entity"];
                Entity found = null;
                try { found = McpScript.FindEntity(w.Commands, w.Composite, reference); } catch (McpError) { }
                entity = found?.shortGUID ?? McpScript.ParseId(reference) ?? throw new McpError("'" + where + ".entity': no entity '" + reference + "' in " + w.Composite.name + ".");
            }
            if (named != null && entity != null)
                throw new McpError(where + ": give 'event' or 'entity', not both.");
            if (time == null && named == null && entity == null)
                throw new McpError(where + " needs 'event', 'entity' or '" + timeField + "' to say which events.");
            List<CAGEAnimation.EventTrack> tracks = spec["track"] != null && spec["track"].Type != JTokenType.Null
                ? new List<CAGEAnimation.EventTrack>() { FindEventTrack(e, (string)spec["track"], where + ".track") }
                : w.EventTracks.Where(o => o != null).ToList();

            List<(CAGEAnimation.EventTrack, CAGEAnimation.EventTrack.Keyframe)> matches = new List<(CAGEAnimation.EventTrack, CAGEAnimation.EventTrack.Keyframe)>();
            foreach (CAGEAnimation.EventTrack track in tracks)
            {
                foreach (CAGEAnimation.EventTrack.Keyframe key in track.keyframes)
                {
                    if (key == null) continue;
                    if (time != null && Math.Abs(key.time - time.Value) > Eps) continue;
                    if (named != null && (key.track_type == ANIM_TRACK_TYPE.T_GUID || key.forward != named.Value)) continue;
                    if (entity != null && (key.track_type != ANIM_TRACK_TYPE.T_GUID || key.forward != entity.Value)) continue;
                    matches.Add((track, key));
                }
            }
            return matches;
        }

        private static string EventLabel(Working w, CAGEAnimation.EventTrack.Keyframe key)
        {
            if (key.track_type == ANIM_TRACK_TYPE.T_GUID)
            {
                Entity entity = w.Composite.GetEntityByID(key.forward);
                return entity == null ? McpScript.Id(key.forward) : McpScript.EntityName(w.Commands, w.Composite, entity);
            }
            return "'" + McpScript.ParamName(key.forward) + "'";
        }

        private static void RemoveEvents(EventWork e, JObject spec, string where)
        {
            CheckFields(spec, where, "event", "entity", "time", "track");
            List<(CAGEAnimation.EventTrack track, CAGEAnimation.EventTrack.Keyframe key)> matches = MatchEvents(e, spec, where, "time");
            if (matches.Count == 0)
                throw new McpError(where + ": no event matches. get_cage_animation lists the animation's events.");
            foreach ((CAGEAnimation.EventTrack track, CAGEAnimation.EventTrack.Keyframe key) in matches)
            {
                track.keyframes.Remove(key);
                e.Touched.Add(track);
                e.W.Changes.Add("removed " + EventLabel(e.W, key) + " at " + R(key.time) + " s");
            }
            e.W.Changed = true;
        }

        private static void MoveEvent(EventWork e, JObject spec, string where)
        {
            CheckFields(spec, where, "time", "to", "event", "entity", "track");
            if (spec["time"] == null) throw new McpError(where + " needs 'time': when the event is now.");
            float to = ReadTime(spec["to"], where + ".to");
            List<(CAGEAnimation.EventTrack track, CAGEAnimation.EventTrack.Keyframe key)> matches = MatchEvents(e, spec, where, "time");
            if (matches.Count == 0)
                throw new McpError(where + ": no event is at that time" + (spec["event"] != null || spec["entity"] != null || spec["track"] != null ? " matching what was given" : "") + ".");
            if (matches.Count > 1)
                throw new McpError(where + ": " + matches.Count + " events are at that time (" + string.Join(", ", matches.Select(o => EventLabel(e.W, o.key))) + "); give 'event', 'entity' or 'track' to pick one.");
            float from = matches[0].key.time;
            matches[0].key.time = to;
            e.Touched.Add(matches[0].track);
            e.W.Changed = true;
            e.W.Changes.Add("moved " + EventLabel(e.W, matches[0].key) + " from " + R(from) + " s to " + R(to) + " s");
        }

        private static void RenameEvents(EventWork e, JObject spec, string where)
        {
            CheckFields(spec, where, "from", "to", "track");
            string from = ((string)spec["from"] ?? "").Trim();
            string to = ((string)spec["to"] ?? "").Trim();
            if (from.Length == 0 || to.Length == 0)
                throw new McpError(where + " needs 'from' and 'to' names.");
            ShortGuid old = McpScript.ParseId(from) ?? ShortGuidUtils.Generate(from, false);
            List<CAGEAnimation.EventTrack> tracks = spec["track"] != null && spec["track"].Type != JTokenType.Null
                ? new List<CAGEAnimation.EventTrack>() { FindEventTrack(e, (string)spec["track"], where + ".track") }
                : e.W.EventTracks.Where(o => o != null).ToList();
            int renamed = 0;
            foreach (CAGEAnimation.EventTrack track in tracks)
            {
                foreach (CAGEAnimation.EventTrack.Keyframe key in track.keyframes)
                {
                    if (key == null || key.track_type == ANIM_TRACK_TYPE.T_GUID || key.forward != old) continue;
                    //As the editor's Event name box: both pins regenerated from the new name
                    key.forward = ShortGuidUtils.Generate(to);
                    key.reverse = ShortGuidUtils.Generate("reverse_" + to);
                    key.track_type = ANIM_TRACK_TYPE.T_STRING;
                    e.Touched.Add(track);
                    renamed++;
                }
            }
            if (renamed == 0)
                throw new McpError(where + ": no string event is called '" + from + "'.");
            e.W.Changed = true;
            e.W.Changes.Add("renamed '" + from + "' to '" + to + "' (" + renamed + (renamed == 1 ? " event)" : " events)"));
        }
        #endregion

        #region preview_cage_animation
        /// <summary>
        /// Animation Mode's host for a preview a tool takes: a copy of the animation, so the session's
        /// recording (a gizmo drag while the picture is taken) can never reach the level's data.
        /// </summary>
        private sealed class PreviewHost : IAnimationModeHost
        {
            public CAGEAnimation Animation { get; set; }
            public Composite AnimationComposite { get; set; }
            public LevelContent AnimationContent { get; set; }
            public float AnimationLength { get; set; }
            public bool BezierInterpolation { get; set; }
            /// <summary>An edit made in the inspector or viewport while the pose was up: taken as a keyframe of the copy, so not applied. UI thread.</summary>
            public string EditTaken;
            public void OnAnimationEditedExternally(string undoLabel, bool structureChanged) { EditTaken = undoLabel ?? "an edit"; }
        }

        /// <summary>The viewer has what it was last sent on screen: connected, the level populated, no populate in flight. UI thread.</summary>
        private static bool ViewerSettled() => Send.Connected && ViewerResourceSync.ViewerReady && ViewerPopulateSync.ActivePopulateToken == 0;

        private static readonly TimeSpan ViewerReadyTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Wait, as capture_viewport does, until the viewer has taken the composite on screen, finished populating it and
        /// holds the level's models and materials as they are now. A pose has to go after that: a populate starts the
        /// viewer's scene afresh, dropping a pose sent before it, and nothing sends it again. <paramref name="populateEventsBefore"/>,
        /// when not -1, is <see cref="ViewerPopulateSync.PopulateEvents"/> from before a composite was opened.
        /// </summary>
        private static void AwaitViewerSettled(McpCall call, int populateEventsBefore)
        {
            if (!McpEditor.WaitFor(call, () => Send.Connected, TimeSpan.FromSeconds(60), "Waiting for the viewport"))
                throw new McpError("The viewport is not connected yet (it may still be starting, or loading the level).");
            //Give the viewer a moment to start on a composite just opened, so its old scene is not taken for the new one
            if (populateEventsBefore >= 0)
                McpEditor.WaitFor(call, () => ViewerPopulateSync.PopulateEvents != populateEventsBefore || !Send.Connected, TimeSpan.FromSeconds(3), "Waiting for the viewport to take the composite");
            AwaitViewerPopulated(call);

            //Resource edits go to the viewer on a coalescing timer: let any pending batch land first
            bool synced = false;
            McpEditor.UI(() => ViewerResourceSync.AfterNextSync(() => synced = true));
            if (!McpEditor.WaitFor(call, () => synced, TimeSpan.FromSeconds(30), "Sending changed models and materials to the viewport"))
                call.Note("Some model, material or texture changes may not have reached the viewport yet.");
        }

        private static void AwaitViewerPopulated(McpCall call)
        {
            if (!McpEditor.WaitFor(call, () => ViewerSettled() || !Send.Connected, ViewerReadyTimeout, "Waiting for the viewport to finish loading"))
                throw new McpError("The viewport is still loading after " + (int)ViewerReadyTimeout.TotalMinutes + " minutes. Try again later (get_viewport_state shows when it is ready).");
            if (!McpEditor.UI(() => ViewerSettled()))
                throw new McpError("The viewport disconnected while loading (it may have crashed): get_viewport_state with log_lines shows its output.");
        }

        /// <summary>
        /// A preview takes Animation Mode from an editor window - and, in the game, the game's animation drive from runtime_utils
        /// animate too (a viewport preview leaves the drive alone). UI thread.
        /// </summary>
        private static void RequireAnimationModeFree(bool inGame)
        {
            if (AnimationModeSession.Active)
                throw new McpError("Animation Mode is on in a CAGEAnimation editor window; a preview would take it over. Turn it off there first.");
            if (inGame && LiveLinkAnimationDrive.Active)
                throw new McpError("The game is driving an animation for runtime_utils animate; a preview would take the drive over. Give it back first: runtime_utils {action: 'animate', release: true}.");
        }

        private static object Preview(McpCall call)
        {
            float time = ReadTime(call.Token("time"), "time");
            if (ReadFlag(call.Token("in_game"), "in_game"))
                return PreviewInGame(call, time);
            if (call.Has("instance_path") || call.Has("screenshot"))
                throw new McpError("'instance_path' and 'screenshot' are for in_game.");
            PreviewHost host = null;
            int populateEvents = -1;
            string animationName = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                if (!Singleton.ViewportEnabled)
                    throw new McpError("The viewport is turned off (Options > Viewport > Enable Viewport, or OpenCAGE was started with -disable_viewport).");
                RequireAnimationModeFree(inGame: false);
                CAGEAnimation animation = FindAnimation(content.Level.Commands, call, out Composite composite);
                animationName = McpScript.EntityName(content.Level.Commands, composite, animation);
                CompositeDisplay display = Singleton.Editor.CompositeDisplay;
                if (display == null || display.IsDisposed || !display.Populated || display.Composite != composite)
                {
                    populateEvents = ViewerPopulateSync.PopulateEvents;
                    McpScriptEdit.Show(composite, null);
                }
                Singleton.Editor.CompositeDisplay?.ShowLevelViewerPanel(true);
                host = new PreviewHost()
                {
                    Animation = animation.Copy(),
                    AnimationComposite = composite,
                    AnimationContent = content,
                    AnimationLength = EffectiveLength(animation),
                    BezierInterpolation = InterpolationOf(animation.floatTracks) == CAGEAnimation.InterpolationMode.Bezier,
                };
            });

            //Posed only once the viewer has the composite, and Animation Mode held just for the moment of the picture:
            //while it is on, the user's own inspector and viewport edits become keyframes of the throwaway copy
            AwaitViewerSettled(call, populateEvents);

            try
            {
                AnimationModeSession session = null;
                McpEditor.UI(() =>
                {
                    if (AnimationModeSession.Active)
                        throw new McpError("Animation Mode was turned on in a CAGEAnimation editor window meanwhile; try again when it is off.");
                    CompositeDisplay display = Singleton.Editor.CompositeDisplay;
                    if (display == null || display.IsDisposed || display.Composite != host.AnimationComposite)
                        throw new McpError("The editor moved to another composite before the preview could be posed; try again.");
                    //The placement being previewed is the one the editor walked into, as Animation Mode takes it
                    Composite root = display.Path?.AllComposites.FirstOrDefault() ?? host.AnimationComposite;
                    List<uint> drill = display.Path?.AllEntities.Select(o => o.shortGUID.AsUInt32).ToList() ?? new List<uint>();
                    session = AnimationModeSession.Begin(host, root, drill);
                    session?.SetTime(time);
                });
                Thread.Sleep(600);

                //A populate started since then drops the pose with its scene: wait it out, and pose again just before the picture
                AwaitViewerPopulated(call);
                McpEditor.UI(() =>
                {
                    if (session == null || !ReferenceEquals(AnimationModeSession.Current, session))
                        throw new McpError("Animation Mode was turned on in a CAGEAnimation editor window meanwhile; try again when it is off.");
                    CompositeDisplay display = Singleton.Editor.CompositeDisplay;
                    if (display == null || display.IsDisposed || display.Composite != host.AnimationComposite)
                        throw new McpError("The editor moved to another composite before the picture was taken; try again.");
                    session.Refresh();
                });

                McpTool capture = McpTools.Find("capture_viewport");
                if (capture == null)
                    throw new McpError("The viewport capture tool is not available.");
                //The viewer was waited for above: capture's own wait would only hold the pose (and Animation Mode) up longer
                JObject args = new JObject() { ["wait"] = false };
                if (call.Has("max_width")) args["max_width"] = call.Int("max_width");
                McpCall inner = new McpCall(capture, args, call.Cancel, (text, done, total) => call.Progress(text, done, total));
                object picture = capture.Run(inner);
                foreach (string note in inner.Notes) call.Note(note);
                if (picture is McpImage image)
                {
                    image.Caption = animationName + " posed at " + R(time) + " s" + (time > host.AnimationLength + Eps ? " (past its end at " + R(host.AnimationLength) + " s, so held at the last keys)" : "") + ". " + image.Caption;
                    return image;
                }
                return picture;
            }
            finally
            {
                string taken = null;
                try { McpEditor.UI(() => { AnimationModeSession.EndFor(host); taken = host.EditTaken; }); } catch { }
                if (taken != null)
                    call.Note("While the pose was up, a change made in the editor (" + taken + ") went into the preview's throwaway copy of the animation as a keyframe, and was not applied: make it again.");
            }
        }

        /// <summary>
        /// preview_cage_animation in_game: Animation Mode with "In game", held just for the picture - the running game holds
        /// the animation at the time (evaluating every track itself), the tool waits until a game frame shows it, takes the
        /// game's frame, and the mode's end gives the game its animation back.
        /// </summary>
        private static object PreviewInGame(McpCall call, float time)
        {
            McpLevelTools.RequireLiveLink();
            PreviewHost host = null;
            Commands commands = null;
            Composite root = null;
            List<uint> drill = null;
            LiveLinkAnimationDrive.Target target = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                RequireAnimationModeFree(inGame: true);
                commands = content.Level.Commands;
                CAGEAnimation animation = FindAnimation(commands, call, out Composite composite);
                //The placement: as given, else the one the editor walked into (as call_method takes it), else every placement
                CompositeDisplay display = Singleton.Editor.CompositeDisplay;
                bool shown = display != null && !display.IsDisposed && display.Composite == composite;
                List<ShortGuid> path = null;
                if (call.Has("instance_path"))
                    path = call.StrList("instance_path").Select(o => McpScript.ParseId(o) ?? throw new McpError("'" + o + "' in 'instance_path' is not an entity id.")).ToList();
                else if (shown)
                    path = LiveLink.InstancePath(display, commands);
                //The viewer's half of the mode poses the same placement, if it shows the composite
                root = (shown ? display.Path?.AllComposites.FirstOrDefault() : null) ?? composite;
                drill = shown ? display.Path?.AllEntities.Select(o => o.shortGUID.AsUInt32).ToList() ?? new List<uint>() : new List<uint>();
                host = new PreviewHost()
                {
                    Animation = animation.Copy(),
                    AnimationComposite = composite,
                    AnimationContent = content,
                    AnimationLength = EffectiveLength(animation),
                    BezierInterpolation = InterpolationOf(animation.floatTracks) == CAGEAnimation.InterpolationMode.Bezier,
                };
                target = new LiveLinkAnimationDrive.Target()
                {
                    Root = LiveLink.RootOf(commands),
                    Composite = composite.shortGUID,
                    Entity = animation.shortGUID,
                    Path = path ?? new List<ShortGuid>(),
                    Label = McpScript.EntityName(commands, composite, animation),
                };
            });
            McpLevelTools.RequireRunningLevel(commands);

            try
            {
                uint sequence = 0;
                McpEditor.UI(() =>
                {
                    RequireAnimationModeFree(inGame: true);
                    AnimationModeSession session = AnimationModeSession.Begin(host, root, drill, target);
                    session?.SetTime(time);
                    sequence = LiveLinkAnimationDrive.Sequence;
                });
                LiveLink.GameAnimation game = McpLevelTools.AwaitGameAnimation(call, sequence, TimeSpan.FromSeconds(15));

                string caption = target.Label + " held in the game at " + R((float)game.Time) + " s"
                    + (time > game.Length ? " (asked for " + R(time) + " s: the game holds it just short of its end at " + R((float)game.Length) + " s)" : "")
                    + (game.Found > 1 ? ", in " + game.Applied + " of " + game.Found + " placements" : "")
                    + ".";
                if (call.Has("screenshot") && !ReadFlag(call.Token("screenshot"), "screenshot"))
                    return new JObject() { ["result"] = caption, ["game"] = McpLevelTools.DescribeGameAnimation(game) };

                //The game writes its frame to a file before it answers
                string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OpenCAGE_anim_" + Guid.NewGuid().ToString("N") + ".bmp");
                try
                {
                    LiveLink.Reply shot = LiveLink.Screenshot(file).Result;
                    if (!shot.Ok)
                        throw new McpError("The game held it, but could not take a screenshot: " + shot.Message);
                    int maxWidth = Math.Max(64, call.Int("max_width", 1024));
                    using (System.Drawing.Bitmap frame = new System.Drawing.Bitmap(file))
                    {
                        float scale = Math.Min(1f, (float)maxWidth / frame.Width);
                        using (System.Drawing.Bitmap picture = new System.Drawing.Bitmap(frame, Math.Max(1, (int)(frame.Width * scale)), Math.Max(1, (int)(frame.Height * scale))))
                        using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
                        {
                            picture.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                            return new McpImage()
                            {
                                Data = stream.ToArray(),
                                MimeType = "image/jpeg",
                                Caption = caption + " The game's frame, " + picture.Width + "x" + picture.Height + " pixels.",
                            };
                        }
                    }
                }
                finally
                {
                    try { System.IO.File.Delete(file); } catch { }
                }
            }
            finally
            {
                string taken = null;
                try { McpEditor.UI(() => { AnimationModeSession.EndFor(host); taken = host.EditTaken; }); } catch { }
                //Given back before the result, so a tool after this one finds the game's own animation
                try { McpEditor.WaitFor(null, () => !LiveLinkAnimationDrive.Releasing, TimeSpan.FromSeconds(3), ""); } catch { }
                if (taken != null)
                    call.Note("While the animation was held, a change made in the editor (" + taken + ") went into the preview's throwaway copy of the animation as a keyframe, and was not applied: make it again.");
            }
        }
        #endregion

        #region set_animated_model
        private static bool HasResourceParameter(Commands commands, FunctionType function)
        {
            //EntityInspector.FunctionHasResourceParameter: the type declares an internal 'resource'
            foreach ((ShortGuid name, ParameterVariant variant, DataType type) in commands.Utils.GetAllParameters(function))
                if (name == ShortGuids.resource && variant == ParameterVariant.INTERNAL && type == DataType.RESOURCE)
                    return true;
            return false;
        }

        private static JObject DescribeEnvironmentAnimation(EnvironmentAnimations.EnvironmentAnimation entry)
        {
            if (entry == null) return null;
            return new JObject()
            {
                ["id"] = entry.ID,
                ["skeleton"] = string.IsNullOrEmpty(entry.SkeletonName) ? null : entry.SkeletonName,
                ["bones"] = entry.BoneMappings?.Count ?? 0,
                ["meshes"] = entry.MeshMappings?.Count ?? 0,
            };
        }

        private static object SetAnimatedModel(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                bool writing = call.Has("entry");
                LevelContent content = McpEditor.RequireLevel(forEditing: writing);
                Level level = content.Level;
                Commands commands = level.Commands;
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                string name = McpScript.EntityName(commands, composite, entity);
                if (!(entity is FunctionEntity function) || !function.function.IsFunctionType)
                    throw new McpError(name + " is a " + McpScript.TypeName(commands, composite, entity) + "; only function entities carry resources.");
                FunctionType type = function.function.AsFunctionType;
                if (EntityInspector.FunctionResourcesAreGenerated(type))
                    throw new McpError(type + " entities have their resources generated from their parameters when the level is saved; they are not set by hand.");
                if (EntityInspector.FunctionIsMarkerResourceOnly(type))
                    throw new McpError(type + " entities carry only a marker resource, which is managed for them; they take no animated model.");
                bool parameterMode = HasResourceParameter(commands, type);
                if (!parameterMode && type != FunctionType.PhysicsSystem && (function.resources == null || function.resources.Count == 0))
                    throw new McpError(type + " entities do not take resources.");

                List<ResourceReference> current = parameterMode
                    ? (function.GetParameter(ShortGuids.resource)?.content as cResource)?.value ?? new List<ResourceReference>()
                    : function.resources ?? new List<ResourceReference>();
                ResourceReference animated = current.FirstOrDefault(o => o != null && o.resource_type == ResourceType.ANIMATED_MODEL);
                List<EnvironmentAnimations.EnvironmentAnimation> entries = level.EnvironmentAnimations?.Entries ?? new List<EnvironmentAnimations.EnvironmentAnimation>();
                JObject result = new JObject()
                {
                    ["composite"] = composite.name,
                    ["entity"] = new JObject() { ["id"] = McpScript.Id(entity.shortGUID), ["name"] = name, ["type"] = type.ToString() },
                };

                if (!writing)
                {
                    result["animated_model"] = animated == null ? JValue.CreateNull() : (JToken)DescribeEnvironmentAnimation(animated.AnimatedModel) ?? "(reference with no entry)";
                    int limit = Math.Max(1, call.Int("limit", 50));
                    result["entry_count"] = entries.Count;
                    result["entries"] = new JArray(entries.Where(o => o != null).OrderBy(o => o.ID).Take(limit).Select(DescribeEnvironmentAnimation));
                    return result;
                }

                McpEditor.RequireUndoIdle();
                string entry = call.Str("entry").Trim();
                bool remove = string.Equals(entry, "none", StringComparison.OrdinalIgnoreCase);
                bool makeNew = string.Equals(entry, "new", StringComparison.OrdinalIgnoreCase);
                EnvironmentAnimations.EnvironmentAnimation chosen = null;
                if (remove)
                {
                    if (animated == null)
                        throw new McpError(name + " has no ANIMATED_MODEL reference to remove.");
                }
                else if (makeNew)
                {
                    if (level.EnvironmentAnimations == null || entries.Count == 0)
                        throw new McpError("This level has no environment animations, so an ANIMATED_MODEL cannot be added (the editor refuses the same).");
                }
                else if (int.TryParse(entry, out int id))
                {
                    chosen = entries.FirstOrDefault(o => o != null && o.ID == id);
                    if (chosen == null)
                        throw new McpError("The level has no environment-animation entry " + id + ". Its entries: " + string.Join(", ", entries.Where(o => o != null).OrderBy(o => o.ID).Take(40).Select(o => o.ID + (string.IsNullOrEmpty(o.SkeletonName) ? "" : " (" + o.SkeletonName + ")"))) + (entries.Count > 40 ? ", ..." : "") + ".");
                    if (animated != null && ReferenceEquals(animated.AnimatedModel, chosen))
                    {
                        call.Note(name + " already uses entry " + id + "; nothing changed.");
                        result["animated_model"] = DescribeEnvironmentAnimation(chosen);
                        result["changed"] = false;
                        return result;
                    }
                }
                else
                    throw new McpError("'entry' is an environment-animation entry id, 'new' or 'none'.");

                if (makeNew)
                {
                    //As the resource editor's Add New Reference does: an empty entry with a fresh id, in the level's table
                    //Empty but complete: the save writes every one of these lists (and hashes the skeleton name), so none may be null
                    chosen = new EnvironmentAnimations.EnvironmentAnimation()
                    {
                        Matrix = System.Numerics.Matrix4x4.Identity,
                        SkeletonName = "",
                        BoneMappings = new List<ShortGuid>(),
                        MeshMappings = new List<ShortGuid>(),
                        InverseBindPoses = new List<System.Numerics.Matrix4x4>(),
                        HavokToCathodeMappings = new List<System.Numerics.Matrix4x4>(),
                        HelperMatrices = new List<EnvironmentAnimations.WeightedHelperData>(),
                    };
                    chosen.ID = level.EnvironmentAnimations.AllocateUniqueId();
                    level.EnvironmentAnimations.Entries.Add(chosen);
                    call.Note("Entry " + chosen.ID + " was added to the level's environment animations empty (no skeleton or mappings), as the editor adds one; undo takes the reference away but leaves the entry.");
                }

                string label = "AI: Edit resources of " + UndoLabels.Entity(composite, function);
                if (parameterMode)
                {
                    Parameter existing = function.GetParameter(ShortGuids.resource);
                    bool had = existing != null;
                    int index = had ? function.parameters.IndexOf(existing) : function.parameters.Count;
                    ParameterData beforeContent = ParameterValues.Clone(existing?.content);
                    ParameterVariant beforeVariant = existing?.variant ?? ParameterVariant.INTERNAL;
                    cResource after = existing?.content is cResource live ? (cResource)ParameterValues.Clone(live) : new cResource(function.shortGUID);
                    ChangeAnimatedModel(after.value, after.shortGUID, chosen, remove);
                    Parameter parameter = existing ?? new Parameter(ShortGuids.resource, new cResource(function.shortGUID), ParameterVariant.INTERNAL);
                    UndoStack.Current.Apply(new ResourceSessionEdit(composite, function, parameter, had, index, beforeContent, beforeVariant, after, label));
                }
                else
                {
                    List<ResourceReference> before = ResourceSessionEdit.CloneReferences(function.resources);
                    List<ResourceReference> after = ResourceSessionEdit.CloneReferences(function.resources);
                    ChangeAnimatedModel(after, function.shortGUID, chosen, remove);
                    UndoStack.Current.Apply(new ResourceSessionEdit(composite, function, before, after, label));
                }
                DirtyTracker.MarkLevelDataModified();
                McpScriptEdit.Show(composite, new List<Entity>() { function });

                result["changed"] = true;
                result["animated_model"] = remove ? JValue.CreateNull() : (JToken)DescribeEnvironmentAnimation(chosen);
                return result;
            });
        }

        /// <summary>Point the list's ANIMATED_MODEL reference at an entry (adding one if missing), or drop it. The list is a copy.</summary>
        private static void ChangeAnimatedModel(List<ResourceReference> references, ShortGuid resourceId, EnvironmentAnimations.EnvironmentAnimation entry, bool remove)
        {
            int at = references.FindIndex(o => o != null && o.resource_type == ResourceType.ANIMATED_MODEL);
            if (remove)
            {
                if (at >= 0) references.RemoveAt(at);
                return;
            }
            if (at >= 0)
                references[at].AnimatedModel = entry;
            else
                references.Add(new ResourceReference(ResourceType.ANIMATED_MODEL) { resource_id = resourceId, AnimatedModel = entry });
        }
        #endregion
    }
}
