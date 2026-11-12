using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// A physics prop placed the way retail places every DYNAMIC prop: spawned at load by an AssetSpawner (retail's placed physics systems
    /// are keyframed: doors, cameras, panels). Each is an AYZ\_Props_\Dynamic\X composite holding an AssetSpawner (spawn_on_load,
    /// spawn_on_reset, allow_physics, allow_forced_despawn, persist_on_callback) whose 'asset' is an instance of AYZ\_Props_\Physics\X_Template;
    /// a dynamic prop placed directly stays an inactive, sleeping body. The wrapper made here is that shape: '&lt;prop path&gt;_Spawned' holding
    /// '&lt;leaf&gt;_Template_1' (is_template) and 'AssetSpawner_1'. The bodies are keyed by name first (<see cref="PhysicsBodyKeys"/>).
    /// </summary>
    public static class PhysicsPropSpawner
    {
        public const string Suffix = "_Spawned";
        private static readonly ShortGuid AssetPin = ShortGuidUtils.Generate("asset");

        /// <summary>Whether a composite holds a PhysicsSystem entity (a physics prop).</summary>
        public static bool IsPhysicsProp(Composite composite) =>
            composite != null && composite.functions.Any(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.PhysicsSystem);

        /// <summary>Whether a composite is a spawner wrapper of a physics prop (it holds an AssetSpawner whose asset is an instance of one).</summary>
        public static bool IsWrapper(Commands commands, Composite composite)
        {
            if (composite == null) return false;
            foreach (FunctionEntity spawner in composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.AssetSpawner))
                foreach (EntityConnector link in spawner.childLinks.Where(o => o.thisParamID == AssetPin))
                    if (composite.GetEntityByID(link.linkedEntityID) is FunctionEntity instance && !instance.function.IsFunctionType && IsPhysicsProp(commands.GetComposite(instance.function)))
                        return true;
            return false;
        }

        /// <summary>
        /// One of the game's effect libraries or required assets (AYZ\FX_Library\..., Required_Assets\...), which the game spawns itself:
        /// the explosion debris a ported retail water brings in (Required_Assets\IED_VFX\HE\HE_Debris) is placed directly by retail.
        /// </summary>
        public static bool IsGameEffect(Composite composite)
        {
            string name = (composite?.name ?? "").Replace('/', '\\');
            return name.StartsWith("Required_Assets\\", StringComparison.OrdinalIgnoreCase) || name.IndexOf("\\FX_Library\\", StringComparison.OrdinalIgnoreCase) >= 0 || name.StartsWith("FX_Library\\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The placed instances of physics props with at least one dynamic body that are not spawned: placed directly, not as the asset of
        /// an AssetSpawner, with the composite holding each. Keyframed systems (doors) are placed directly by retail and are left out, and so
        /// is the game's own effect debris (<see cref="IsGameEffect"/>): ported with a retail effect, it is placed directly there too.
        /// </summary>
        public static List<(Composite holder, FunctionEntity instance, Composite prop)> PlacedDynamic(Level level)
        {
            List<(Composite, FunctionEntity, Composite)> found = new List<(Composite, FunctionEntity, Composite)>();
            Commands commands = level?.Commands;
            HavokPackfile physics = level?.Physics;
            if (commands == null || physics == null || !physics.Loaded) return found;
            Dictionary<Composite, bool> dynamic = new Dictionary<Composite, bool>();
            bool IsDynamic(Composite prop)
            {
                if (dynamic.TryGetValue(prop, out bool known)) return known;
                bool any = false;
                foreach (FunctionEntity system in prop.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.PhysicsSystem))
                {
                    int index = PhysicsBodyKeys.SystemIndexOf(system);
                    HavokPackfile.PhysicsSystem bound = index < 0 ? null : physics.GetPhysicsSystem(index);
                    if (bound != null && (physics.GetRigidBodies(bound) ?? new List<HavokPackfile.RigidBodyInfo>()).Any(o => o.MassInv > 0f)) any = true;
                }
                return dynamic[prop] = any;
            }
            foreach (Composite holder in commands.Entries)
            {
                if (holder == null || IsGameEffect(holder)) continue;
                HashSet<ShortGuid> spawnedHere = new HashSet<ShortGuid>(holder.functions
                    .Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.AssetSpawner)
                    .SelectMany(o => o.childLinks.Where(l => l.thisParamID == AssetPin).Select(l => l.linkedEntityID)));
                foreach (FunctionEntity instance in holder.functions.Where(o => !o.function.IsFunctionType))
                {
                    if (spawnedHere.Contains(instance.shortGUID)) continue;
                    Composite prop = commands.GetComposite(instance.function);
                    if (prop == null || IsGameEffect(prop) || !IsPhysicsProp(prop) || !IsDynamic(prop)) continue;
                    found.Add((holder, instance, prop));
                }
            }
            return found;
        }

        /// <summary>
        /// Inside an MCP script edit: make the placed instance of a physics prop a spawned one (see the class). Returns what was done, for
        /// a result: the wrapper, whether it was made or reused, the instance now placed, and the bodies keyed.
        /// </summary>
        internal static JObject Wrap(McpScriptEdit edit, Level level, Composite holder, FunctionEntity placed)
        {
            Commands commands = edit.Commands;
            Composite prop = placed.function.IsFunctionType ? null : commands.GetComposite(placed.function);
            if (!IsPhysicsProp(prop))
                throw new McpError(McpScript.EntityName(commands, holder, placed) + " is not an instance of a physics prop (a composite holding a PhysicsSystem).");
            JObject result = new JObject() { ["prop"] = prop.name };

            //Its bodies keyed by name first: spawned, a body finds its model through that key
            List<PhysicsBodyKeys.Body> keyed = PhysicsBodyKeys.Key(edit, PhysicsBodyKeys.Inspect(level, prop));
            if (keyed.Count != 0) result["bodies_keyed"] = new JArray(keyed.Select(o => o.BodyName));

            //The wrapper: made once per prop, reused by every placement
            string wrapperPath = prop.name + Suffix;
            Composite wrapper = commands.Entries.FirstOrDefault(o => string.Equals(o.name, wrapperPath, StringComparison.OrdinalIgnoreCase));
            if (wrapper != null && !IsWrapper(commands, wrapper))
                throw new McpError(wrapperPath + " already exists and is not a spawner of " + prop.name + ".");
            if (wrapper == null)
            {
                wrapper = edit.AddComposite(wrapperPath);
                string leaf = prop.name.Substring(prop.name.LastIndexOf('\\') + 1);
                FunctionEntity template = edit.AddInstance(wrapper, prop, leaf + "_Template_1");
                edit.SetParameter(wrapper, template, "is_template", new JValue(true), allowCustom: true);
                FunctionEntity spawner = edit.AddFunction(wrapper, FunctionType.AssetSpawner, "AssetSpawner_1");
                foreach (string flag in new[] { "spawn_on_load", "spawn_on_reset", "allow_physics", "allow_forced_despawn", "persist_on_callback" })
                    edit.SetParameter(wrapper, spawner, flag, new JValue(true), allowCustom: true);
                edit.AddLink(wrapper, spawner, "asset", template, "reference");
                result["wrapper_made"] = true;
            }
            result["wrapper"] = wrapper.name;

            //The placement becomes the wrapper's, same name and transform
            string name = commands.Utils.GetEntityName(holder, placed);
            cTransform transform = placed.GetParameter("position")?.content as cTransform;
            edit.Delete(holder, placed);
            FunctionEntity replacement = edit.AddInstance(holder, wrapper, name);
            if (transform != null)
            {
                replacement.AddParameter("position", (ParameterData)transform.Clone());
                edit.Changed(holder);
            }
            result["placed"] = McpScript.Brief(commands, holder, replacement);
            return result;
        }

        /// <summary>The collision retail gives a dynamic prop's ModelReference: its ballistic proxy, ghosted and frozen until the body takes it.</summary>
        public static readonly string[] PropCollisionFlags = { "BALLISTIC", "FIXED", "RESOURCE", "GHOSTED", "PRE_GHOSTED", "FROZEN", "PRE_FROZEN" };

        /// <summary>
        /// Retail's dynamic-prop collision for one ModelReference: set_collision proxy from_model with <see cref="PropCollisionFlags"/>, run
        /// inside an MCP call (<paramref name="call"/>) or, from the editor (a Level Check fix), on its own.
        /// </summary>
        internal static JObject SetPropCollision(McpCall call, string composite, string entity)
        {
            JObject arguments = new JObject() { ["composite"] = composite, ["entity"] = entity, ["proxy"] = "from_model", ["flags"] = new JArray(PropCollisionFlags) };
            if (call != null)
                return McpEditorTools.Nested(call, "set_collision", arguments);
            McpTool tool = McpTools.Find("set_collision") ?? throw new McpError(McpErrorCodes.Failed, "The set_collision tool is missing from this build.");
            object result = tool.Run(new McpCall(tool, arguments, System.Threading.CancellationToken.None, null));
            return result as JObject ?? new JObject();
        }

        /// <summary>
        /// Give every body of a physics prop whose ModelReference has no collision of its own retail's (<see cref="SetPropCollision"/>),
        /// then key the bodies by name (one undo step). Returns what was given.
        /// </summary>
        internal static JArray GiveCollisionAndKey(McpCall call, string propPath, string labelPrefix = "AI: ")
        {
            Composite prop = null;
            Level level = null;
            List<string> need = new List<string>();
            McpEditor.UI(() =>
            {
                level = McpEditor.RequireLevel().Level;
                prop = McpScript.FindComposite(level.Commands, propPath);
                need = PhysicsBodyKeys.Inspect(level, prop).Where(o => o.Model != null && !o.HasCollision && o.State != PhysicsBodyKeys.State.DrawsNothing).Select(o => McpScript.Id(o.Model.shortGUID)).Distinct().ToList();
            });
            JArray given = new JArray();
            foreach (string entity in need)
                given.Add(SetPropCollision(call, prop.name, entity));
            PhysicsBodyKeys.KeyComposite(level, prop, "Key physics bodies of " + prop.name + " by name", labelPrefix);
            return given;
        }

        /// <summary>
        /// Make a placed physics prop spawned (an MCP tool's thread): each body's ModelReference with no collision of its own first gets
        /// retail's dynamic-prop collision (without it a spawned body logs "has no matching collision" and is never made), then the wrapper
        /// as one undo step (<see cref="Wrap"/>). <paramref name="labelPrefix"/>: "AI: " from a tool.
        /// </summary>
        internal static JObject MakeSpawned(McpCall call, string compositeArg, string entityArg, string labelPrefix = "AI: ")
        {
            Composite holder = null;
            FunctionEntity placed = null;
            Level level = null;
            string label = null;
            List<(string composite, string entity)> needCollision = new List<(string, string)>();
            McpEditor.UI(() =>
            {
                level = McpEditor.RequireLevel().Level;
                holder = McpScript.FindComposite(level.Commands, compositeArg);
                placed = McpScript.FindEntity(level.Commands, holder, entityArg) as FunctionEntity;
                if (placed == null) throw McpError.Invalid(entityArg + " is not an instance in " + holder.name + ".");
                Composite prop = placed.function.IsFunctionType ? null : level.Commands.GetComposite(placed.function);
                if (!IsPhysicsProp(prop))
                    throw McpError.Invalid(McpScript.EntityName(level.Commands, holder, placed) + " is not an instance of a physics prop (a composite holding a PhysicsSystem).");
                foreach (PhysicsBodyKeys.Body body in PhysicsBodyKeys.Inspect(level, prop).Where(o => o.Model != null && !o.HasCollision && o.State != PhysicsBodyKeys.State.DrawsNothing))
                    needCollision.Add((prop.name, McpScript.Id(body.Model.shortGUID)));
                label = "Spawn physics prop " + McpScript.EntityName(level.Commands, holder, placed);
            });
            JArray collision = new JArray();
            foreach ((string composite, string entity) in needCollision.Distinct())
                collision.Add(SetPropCollision(call, composite, entity));
            JObject result = null;
            McpScriptEdit.Run(label, holder,
                edit => result = Wrap(edit, level, holder, placed), new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = labelPrefix });
            if (collision.Count != 0) result["collision_given"] = collision;
            return result;
        }
    }
}