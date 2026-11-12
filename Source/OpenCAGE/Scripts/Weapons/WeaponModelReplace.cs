using AlienPAK;
using Assimp;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;
using Submesh = CATHODE.Models.CS2.Component.LOD.Submesh;

namespace OpenCAGE.Weapons
{
    /// <summary>One thing a weapon composite draws: a ModelReference (or other renderable) and the bone that carries it.</summary>
    internal sealed class WeaponPart
    {
        public FunctionEntity Entity;
        public string Name = "";
        public ShortGuid ResourceId;
        /// <summary>The rig bone the level's animation entry hangs this part's renderable on, or -1 (it never moves).</summary>
        public int Bone = -1;
        public string BoneName;
        /// <summary>Takes the part's own model space to its composite's space at rest (the bind placement for a part on a bone).</summary>
        public Matrix4x4 Rest = Matrix4x4.Identity;
        /// <summary>The two ways a part can be placed: by its entity's position (always), and by its bone's bind pose (on a bone only).</summary>
        public Matrix4x4 EntityRest = Matrix4x4.Identity;
        public Matrix4x4? BindRest;
        public List<Submesh> Lod0 = new List<Submesh>();
        public int Triangles;
        public bool ShowOnReset = true;
        /// <summary>The clips of the rig's sets that move this part, and the most any of them moves it (metres).</summary>
        public List<string> MovedBy = new List<string>();
        public float Travel;
        /// <summary>Links that show or hide it from script (a shotgun's SHELL, shown while a shell is loaded).</summary>
        public List<string> Scripted = new List<string>();
        public string ModelName;
        public bool Animated => MovedBy.Count != 0;
    }

    /// <summary>One of a weapon's composites: its first-person one, its third-person one, or a prop drawing the same weapon (a pickup).</summary>
    internal sealed class WeaponComposite
    {
        public Composite Composite;
        /// <summary>first_person, third_person, pickup, dropped or prop.</summary>
        public string Role = "";
        public FunctionEntity AnimatedReference;
        public EnvironmentAnimations.EnvironmentAnimation Entry;
        public string RigName;
        public Skeleton Rig;
        public List<WeaponPart> Parts = new List<WeaponPart>();
        /// <summary>Entities placed by a position that are not drawn parts: markers, lights, effects, instances.</summary>
        public List<FunctionEntity> Positioned = new List<FunctionEntity>();
        public FunctionEntity Muzzle;
        public FunctionEntity PhysicsSystem;
        /// <summary>How this composite's space sits against weapon space (identity for the first-person composite).</summary>
        public WeaponGeometry.FrameFit Fit = new WeaponGeometry.FrameFit() { SameFrame = true };
        /// <summary>"bind" (the parts sit where their bones' bind poses put them) or "entity" (where their entities' positions do).</summary>
        public string PlacedBy = "bind";
        public int Placements;
        public bool Selected;
        public string Path => Composite?.name;
        public string Key;
        /// <summary>The weapon's body, the part a model's unnamed meshes go on (see <see cref="WeaponGeometry.MainPart"/>).</summary>
        public WeaponPart Main
        {
            get
            {
                List<WeaponPart> drawn = Parts.Where(o => o.Lod0.Count != 0).ToList();
                int i = WeaponGeometry.MainPart(Key, drawn.Select(o => o.Name).ToList(), drawn.Select(o => o.Bone).ToList(),
                    drawn.Select(o => o.Lod0.Sum(s => (s.MaxBounds - s.MinBounds).Length())).ToList(), drawn.Select(o => o.Triangles).ToList());
                return i < 0 ? null : drawn[i];
            }
        }
    }

    /// <summary>A character type whose weapon draws the third-person composite (through its weapon archetype).</summary>
    internal sealed class WeaponNpcType
    {
        public Composite Composite;
        public FunctionEntity Instance;
        public Composite Weapon;
        public FunctionEntity ThirdPersonInstance;
    }

    /// <summary>A weapon or held item as the wizard sees it.</summary>
    internal sealed class WeaponInfo
    {
        public string Key = "";
        public WeaponComposite FirstPerson;
        public WeaponComposite ThirdPerson;
        public List<WeaponComposite> Props = new List<WeaponComposite>();
        public List<WeaponNpcType> NpcTypes = new List<WeaponNpcType>();
        public List<string> Notes = new List<string>();
        public bool ClipsRead;
        public IEnumerable<WeaponComposite> All => new[] { FirstPerson, ThirdPerson }.Where(o => o != null).Concat(Props);
    }

    /// <summary>A mesh of the model file being put on the weapon.</summary>
    internal sealed class WeaponSourceMesh
    {
        public int Index;
        public string Name = "", Material = "";
        public int MaterialIndex = -1;
        /// <summary>Three corners per triangle, in weapon space as the file gives it (before the wizard's offset / rotation).</summary>
        public List<Vector3> Corners = new List<Vector3>();
        public bool Skinned;
        public bool Reference => Name.StartsWith("REF_", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class WeaponModelSource
    {
        public string Path = "";
        public float Scale = 1, UnitScale = 1;
        public List<WeaponSourceMesh> Meshes = new List<WeaponSourceMesh>();
        public List<string> Materials = new List<string>();
        public List<string> Notes = new List<string>();
        public IEnumerable<WeaponSourceMesh> Usable => Meshes.Where(o => !o.Reference && o.Corners.Count != 0);
    }

    /// <summary>What the wizard is asked to do (from replace_weapon_model's arguments or the window).</summary>
    internal sealed class WeaponOptions
    {
        public string Weapon;
        public string ModelPath;
        public float Scale = 1;
        public Vector3 Offset = Vector3.Zero, Rotation = Vector3.Zero;
        /// <summary>Part name -> "replace" with mesh names, "keep", "hide" or "auto" (the default for every part).</summary>
        public Dictionary<string, string> PartActions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> PartMeshes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        /// <summary>null: the targets found and accepted; else roles (first_person, third_person, pickup, dropped, prop) and/or composite paths.</summary>
        public List<string> Targets;
        public string Muzzle = "auto";
        public Vector3? MuzzlePoint;
        public float MuzzleRadius = 0.12f;
        public Dictionary<string, Vector3> Move = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
        public List<string> NpcTypes = new List<string>();
        public string NpcSuffix = "CUSTOM";
        public string Physics = "auto";
        public string Prefix;
        public bool Clearance = true;
    }

    internal sealed class WeaponPartPlan
    {
        public WeaponComposite Target;
        public WeaponPart Part;
        /// <summary>replace, keep or hide.</summary>
        public string Action = "keep";
        public string Reason = "";
        public List<WeaponSourceMesh> Meshes = new List<WeaponSourceMesh>();
        /// <summary>The composite path the change is made in (a third-person copy for chosen NPC types).</summary>
        public string CompositePath;
        public string ModelName;
        /// <summary>Takes a weapon-space point (after the user's offset / rotation) into this part's own model space.</summary>
        public Matrix4x4 WeaponToPart = Matrix4x4.Identity;
        public string ImportedModel;
        public string PreviousModel;
    }

    internal sealed class WeaponMarkerMove
    {
        public string CompositePath;
        public FunctionEntity Entity;
        public string Name;
        public Vector3 From, To;
        public string Why;
    }

    internal sealed class WeaponPlan
    {
        public WeaponInfo Info;
        public WeaponOptions Options;
        public WeaponModelSource Source;
        public Matrix4x4 UserTransform = Matrix4x4.Identity;
        public string Prefix = "";
        public List<WeaponComposite> Targets = new List<WeaponComposite>();
        public List<WeaponPartPlan> Parts = new List<WeaponPartPlan>();
        public List<WeaponMarkerMove> Markers = new List<WeaponMarkerMove>();
        public Vector3? OldMuzzle, NewMuzzle;
        public List<WeaponNpcType> NpcTypes = new List<WeaponNpcType>();
        public string ThirdPersonCopy, WeaponCopySuffix;
        public Dictionary<Composite, string> WeaponCopies = new Dictionary<Composite, string>();
        public List<WeaponComposite> HullTargets = new List<WeaponComposite>();
        public JArray Clearance = new JArray();
        public List<string> Notes = new List<string>();
        public bool HasFirstPerson => Parts.Any(o => o.Target.Role == "first_person" && o.Action == "replace");
    }

    /// <summary>
    /// The replace-weapon-model wizard: put a model of the user's own on a retail weapon or held item (first-person, third-person and
    /// pickup / dropped models in one go):
    /// <list type="bullet">
    /// <item>each part keeps its entity and RENDERABLE_INSTANCE resource id (set_renderable), so the bone the level's animation entry hangs
    /// on that id still carries the new geometry;</item>
    /// <item>the first-person model is imported with its own NO_CLIP material copies at draw pass 31 (an entity-level material override draws
    /// it invisible: a model's vertex streams are built for its own material), third-person and props with the plain ones;</item>
    /// <item>muzzle and effect markers move to the new barrel's end; a prop whose shape changes a lot gets its own physics hull;</item>
    /// <item>third-person changes can be kept to chosen NPC types (copies of the weapon archetype and its Base_TP);</item>
    /// <item>a clearance check says whether a kept animated part (the loaded shell) pokes through.</item>
    /// </list>
    /// The model file is authored in the weapon's first-person composite space ("weapon space"): <see cref="ExportReference"/> writes the
    /// retail weapon there to model against. Analysis runs on the UI thread; <see cref="Execute"/> on a tool's worker thread.
    /// </summary>
    internal static class WeaponModelReplace
    {
        public const string WeaponsFolder = "Required_Assets\\Weapons\\";
        /// <summary>The shipped CA_ENVIRONMENT permutation with NO_CLIP and normal mapping used for first-person materials.</summary>
        public const string NoClipMask = "0x200014180440400";
        public const int FirstPersonPriority = 31;
        public const string EnvironmentMap = "ayz\\env\\env_base_2.dds";
        private static readonly ShortGuid _show = ShortGuidUtils.Generate("show"), _hide = ShortGuidUtils.Generate("hide");

        #region Finding weapons
        /// <summary>The weapons and held items of a level: each first-person composite under Required_Assets\Weapons, by key.</summary>
        public static List<Tuple<string, Composite, Composite>> List(Commands commands)
        {
            List<Tuple<string, Composite, Composite>> found = new List<Tuple<string, Composite, Composite>>();
            List<Composite> all = commands.Entries.Where(o => o?.name != null && o.name.StartsWith(WeaponsFolder, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (IGrouping<string, Composite> folder in all.GroupBy(o => Segments(o.name)[2], StringComparer.OrdinalIgnoreCase).OrderBy(o => o.Key, StringComparer.OrdinalIgnoreCase))
            {
                List<Composite> firsts = folder.Where(o => Segments(o.name).Length == 4 && !Leaf(o).EndsWith("_TP", StringComparison.OrdinalIgnoreCase) && Draws(o)).ToList();
                foreach (Composite first in firsts.OrderBy(o => string.Equals(Leaf(o), "Base", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(o => o.name, StringComparer.OrdinalIgnoreCase))
                {
                    string key = firsts.Count == 1 || string.Equals(Leaf(first), "Base", StringComparison.OrdinalIgnoreCase) ? folder.Key : folder.Key + "\\" + Leaf(first);
                    Composite third = folder.FirstOrDefault(o => string.Equals(Leaf(o), Leaf(first) + "_TP", StringComparison.OrdinalIgnoreCase));
                    found.Add(Tuple.Create(key, first, third));
                }
            }
            return found;
        }

        /// <summary>A weapon by key ("Shotgun", "Motion_Tracker\Tracker_1979"), folder or first-person composite path.</summary>
        public static Tuple<string, Composite, Composite> Find(Commands commands, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) throw new McpError("Say which weapon: 'weapon' (describe_weapon with no weapon lists them).");
            string wanted = McpScript.NormalisePath(reference);
            List<Tuple<string, Composite, Composite>> weapons = List(commands);
            Tuple<string, Composite, Composite> hit = weapons.FirstOrDefault(o => string.Equals(o.Item1, wanted, StringComparison.OrdinalIgnoreCase))
                ?? weapons.FirstOrDefault(o => string.Equals(o.Item2.name, wanted, StringComparison.OrdinalIgnoreCase))
                ?? weapons.FirstOrDefault(o => o.Item3 != null && string.Equals(o.Item3.name, wanted, StringComparison.OrdinalIgnoreCase))
                ?? weapons.FirstOrDefault(o => string.Equals(Norm(o.Item1), Norm(wanted), StringComparison.OrdinalIgnoreCase));
            if (hit == null)
                throw new McpError(McpErrorCodes.NotFound, "No weapon or held item '" + reference + "' in this level. It has: " + string.Join(", ", weapons.Select(o => o.Item1)) + ".");
            return hit;
        }

        private static string[] Segments(string name) => (name ?? "").Split('\\');
        private static string Leaf(Composite composite) => Segments(composite.name).Last();
        /// <summary>Upper case, letters and digits only, a Blender-style ".001" suffix dropped: how names are compared.</summary>
        public static string Norm(string name) => WeaponGeometry.Norm(name);

        /// <summary>Whether a composite draws geometry with ModelReferences (a light's own renderable, a torch's spot cone, is not a part).</summary>
        private static bool Draws(Composite composite) => composite.functions.Any(o => IsPart(o));

        private static bool IsPart(FunctionEntity entity) => IsType(entity, FunctionType.ModelReference) && Renderables(entity).Any();

        private static IEnumerable<ResourceReference> Renderables(FunctionEntity entity)
        {
            List<ResourceReference> references = (entity.GetParameter(ShortGuids.resource)?.content as cResource)?.value;
            IEnumerable<ResourceReference> all = (references ?? new List<ResourceReference>()).Concat(entity.resources ?? new List<ResourceReference>());
            return all.Where(o => o != null && o.resource_type == ResourceType.RENDERABLE_INSTANCE && o.RenderableInstance != null && o.RenderableInstance.Any(e => e?.Model != null));
        }

        /// <summary>Everything about one weapon the wizard and describe_weapon show. UI thread.</summary>
        /// <param name="quick">The overview only (describe_weapon's list): no clips, scripts, fits or NPC types.</param>
        public static WeaponInfo Analyse(Level level, string reference, bool readClips = true, bool quick = false)
        {
            Commands commands = level.Commands;
            Tuple<string, Composite, Composite> found = Find(commands, reference);
            WeaponInfo info = new WeaponInfo() { Key = found.Item1 };
            Dictionary<ShortGuid, int> placements = Placements(commands);
            info.FirstPerson = Read(level, found.Item2, "first_person", placements);
            if (found.Item3 != null) info.ThirdPerson = Read(level, found.Item3, "third_person", placements);
            if (readClips && !quick) info.ClipsRead = ReadClips(info.FirstPerson, info.Notes);
            if (!quick) FindScripted(commands, info.FirstPerson);

            List<Vector3> reference3 = quick ? null : RestCorners(info.FirstPerson, false);
            if (info.ThirdPerson != null && !quick)
            {
                info.ThirdPerson.Fit = FitPlacement(reference3, info.ThirdPerson);
                info.ThirdPerson.Selected = info.ThirdPerson.Fit.Accepted;
                if (!info.ThirdPerson.Fit.SameFrame)
                    info.Notes.Add(info.ThirdPerson.Path + " does not share the first-person composite's frame; its model goes where the fit puts it (residual " + Mm(info.ThirdPerson.Fit.Residual) + ").");
            }

            //Props drawing the same weapon: the inventory display / pickup and the physics prop an NPC's death drops, by name, judged by fit
            string key = Norm(Segments(found.Item2.name)[2]);
            foreach (Composite composite in commands.Entries)
            {
                if (composite?.name == null) continue;
                bool inventory = composite.name.StartsWith("AYZ\\_Props_\\Inventory\\", StringComparison.OrdinalIgnoreCase);
                bool physics = composite.name.StartsWith("AYZ\\_Props_\\Physics\\", StringComparison.OrdinalIgnoreCase);
                if (!inventory && !physics) continue;
                string leaf = Norm(Leaf(composite));
                if (leaf.Length < 4 || !(leaf.StartsWith(key) || (key.StartsWith(leaf) && leaf.Length >= 4))) continue;
                if (!Draws(composite)) continue;
                WeaponComposite prop = Read(level, composite, physics ? "dropped" : "pickup", placements);
                if (!quick) prop.Fit = FitPlacement(reference3, prop);
                prop.Selected = quick || prop.Fit.Accepted;
                info.Props.Add(prop);
            }
            if (info.ThirdPerson != null && !quick) info.NpcTypes = FindNpcTypes(commands, info.ThirdPerson.Composite);
            return info;
        }

        /// <summary>
        /// How a composite lines up with weapon space, trying both ways its parts can be placed when they disagree (a Base_TP's rig binds its
        /// parts somewhere other than their entities put them): whichever lines its retail geometry up with the first person's is how the game
        /// shows it, and so where a new model has to go.
        /// </summary>
        private static WeaponGeometry.FrameFit FitPlacement(List<Vector3> reference, WeaponComposite weapon)
        {
            WeaponGeometry.FrameFit fit = WeaponGeometry.Fit(reference, RestCorners(weapon, false));
            if (!weapon.Parts.Any(o => o.BindRest != null && Apart(o.BindRest.Value, o.EntityRest))) return fit;
            List<Vector3> byEntity = weapon.Parts.SelectMany(p => p.Lod0.SelectMany(Corners).Select(v => Vector3.Transform(v, p.EntityRest))).ToList();
            WeaponGeometry.FrameFit alternative = WeaponGeometry.Fit(reference, byEntity);
            if (alternative.Residual >= fit.Residual * 0.8f) return fit;
            foreach (WeaponPart part in weapon.Parts) part.Rest = part.EntityRest;
            weapon.PlacedBy = "entity";
            return alternative;
        }

        private static bool Apart(Matrix4x4 a, Matrix4x4 b) => (a.Translation - b.Translation).Length() > 0.001f || WeaponGeometry.TurnAngle(a * Inverse(b)) > 0.01f;

        private static Dictionary<ShortGuid, int> Placements(Commands commands)
        {
            Dictionary<ShortGuid, int> placed = new Dictionary<ShortGuid, int>();
            foreach (Composite composite in commands.Entries)
                foreach (FunctionEntity function in composite.functions)
                    if (!function.function.IsFunctionType)
                        placed[function.function] = (placed.TryGetValue(function.function, out int n) ? n : 0) + 1;
            return placed;
        }

        private static WeaponComposite Read(Level level, Composite composite, string role, Dictionary<ShortGuid, int> placements)
        {
            Commands commands = level.Commands;
            WeaponComposite read = new WeaponComposite() { Composite = composite, Role = role, Placements = placements.TryGetValue(composite.shortGUID, out int n) ? n : 0, Key = WeaponKeyOf(composite) };
            foreach (FunctionEntity function in composite.functions)
            {
                if (!function.function.IsFunctionType) continue;
                FunctionType type = function.function.AsFunctionType;
                if (type == FunctionType.EnvironmentModelReference && read.Entry == null)
                {
                    EnvironmentAnimations.EnvironmentAnimation entry = McpGlobalAssetChecks.AnimatedModelOf(function);
                    if (entry != null) { read.AnimatedReference = function; read.Entry = entry; read.RigName = entry.SkeletonName; }
                }
                if (type == FunctionType.PhysicsSystem && read.PhysicsSystem == null) read.PhysicsSystem = function;
            }
            read.Rig = RigOf(read.RigName);
            foreach (FunctionEntity function in composite.functions)
            {
                List<ResourceReference> drawn = IsPart(function) ? Renderables(function).ToList() : new List<ResourceReference>();
                if (drawn.Count == 0)
                {
                    if (ReferenceEquals(function, read.AnimatedReference) || ReferenceEquals(function, read.PhysicsSystem)) continue;
                    if (function.GetParameter(ShortGuids.position)?.content is cTransform) read.Positioned.Add(function);
                    continue;
                }
                ResourceReference renderable = drawn[0];
                WeaponPart part = new WeaponPart()
                {
                    Entity = function,
                    Name = McpScript.EntityName(commands, composite, function) ?? McpScript.Id(function.shortGUID),
                    ResourceId = renderable.resource_id,
                };
                foreach (RenderableElements.Element element in renderable.RenderableInstance)
                    if (element?.Model != null && !part.Lod0.Any(o => ReferenceEquals(o, element.Model))) part.Lod0.Add(element.Model);
                part.Triangles = part.Lod0.Sum(o => o.IndexCount / 3);
                part.Bone = read.Entry?.BoneMappings == null ? -1 : read.Entry.BoneMappings.IndexOf(renderable.resource_id);
                part.Rest = part.EntityRest = InstanceTransform.ToMatrix(function.GetParameter(ShortGuids.position)?.content as cTransform);
                if (part.Bone >= 0 && part.Bone < read.Entry.InverseBindPoses.Count && Matrix4x4.Invert(read.Entry.InverseBindPoses[part.Bone], out Matrix4x4 bind))
                    part.Rest = (part.BindRest = bind).Value;
                if (part.Bone >= 0 && read.Rig != null && part.Bone < read.Rig.Bones.Count) part.BoneName = read.Rig.Bones[part.Bone].Name;
                if (function.GetParameter(ShortGuids.show_on_reset)?.content is cBool shown) part.ShowOnReset = shown.value;
                Models.CS2 model = ModelOf(level, part.Lod0[0]);
                part.ModelName = model?.Name;
                read.Parts.Add(part);
            }
            read.Muzzle = read.Positioned.FirstOrDefault(o => IsType(o, FunctionType.PositionMarker) && Norm(McpScript.EntityName(commands, composite, o)) == "MUZZLEPOSMARKER")
                ?? read.Positioned.FirstOrDefault(o => IsType(o, FunctionType.PositionMarker) && Norm(McpScript.EntityName(commands, composite, o)).StartsWith("MUZZLEPOSMARKER"));
            return read;
        }

        /// <summary>The weapon a composite draws, by its folder (Required_Assets\Weapons\<Weapon>\...) or its leaf less _Pickup / _Physics.</summary>
        private static string WeaponKeyOf(Composite composite)
        {
            string[] s = Segments(composite.name);
            if (composite.name.StartsWith(WeaponsFolder, StringComparison.OrdinalIgnoreCase) && s.Length >= 3) return s[2];
            string leaf = s.Last();
            foreach (string end in new[] { "_Pickup", "_Physics" }) if (leaf.EndsWith(end, StringComparison.OrdinalIgnoreCase)) return leaf.Substring(0, leaf.Length - end.Length);
            return leaf;
        }

        private static bool IsType(FunctionEntity entity, FunctionType type) => entity.function.IsFunctionType && entity.function.AsFunctionType == type;

        /// <summary>A rig's bones, through the skeleton index as the importer reads it, else any loaded rig of that name. Null when the animation data isn't loaded.</summary>
        private static Skeleton RigOf(string name)
        {
            if (string.IsNullOrEmpty(name) || !Singleton.AnimationsLoaded) return null;
            CathodeLib.Animation animations = Singleton.Animations;
            Skeleton skeleton = animations.GetSkeleton(name)?.Skeleton;
            if (skeleton != null && skeleton.Loaded && skeleton.Bones.Count != 0) return skeleton;
            return animations.Skeletons.Select(o => o.Skeleton ?? o.Skeleton64).FirstOrDefault(o => o != null && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static List<CathodeLib.Animation.ClipReference> ClipsFor(string rig)
        {
            if (string.IsNullOrEmpty(rig) || !Singleton.AnimationsLoaded) return new List<CathodeLib.Animation.ClipReference>();
            List<CathodeLib.Animation.ClipReference> clips = new List<CathodeLib.Animation.ClipReference>();
            foreach (CathodeLib.Animation.AnimationSet set in Singleton.Animations.Sets.Where(o => string.Equals(o.Skeleton, rig, StringComparison.OrdinalIgnoreCase)))
                foreach (CathodeLib.Animation.AnimationContext context in set.Contexts)
                    foreach (CathodeLib.Animation.ClipReference clip in context.Clips)
                        if (clip != null && !clips.Any(o => string.Equals(o.Name, clip.Name, StringComparison.OrdinalIgnoreCase) && o.Context?.Set == clip.Context?.Set)) clips.Add(clip);
            return clips;
        }

        private static string ClipLabel(CathodeLib.Animation.ClipReference clip) => string.IsNullOrEmpty(clip.Name) ? (clip.Label ?? clip.Path) : clip.Name;

        /// <summary>Which clips of the rig's sets move each part, sampled through the level's own part placement (EnvironmentRigs.Pose).</summary>
        private static bool ReadClips(WeaponComposite weapon, List<string> notes)
        {
            if (weapon?.Entry == null) return false;
            if (weapon.Rig == null)
            {
                notes.Add(Singleton.AnimationsLoaded ? "The rig " + weapon.RigName + " is not in ANIMATION.PAK, so which clips move which part is not shown." : "The game's animation data is not loaded yet, so which clips move which part is not shown (try again in a moment).");
                return false;
            }
            EnvironmentRigs.Prop prop = new EnvironmentRigs.Prop() { Entry = weapon.Entry, Composite = weapon.Composite };
            Matrix4x4[] rest = EnvironmentRigs.Pose(prop, weapon.Rig, null, 0);
            List<WeaponPart> moving = weapon.Parts.Where(o => o.Bone >= 0 && o.Bone < rest.Length).ToList();
            Dictionary<WeaponPart, Vector3[]> corners = moving.ToDictionary(o => o, o => BoxCorners(o.Lod0));
            foreach (CathodeLib.Animation.ClipReference clip in ClipsFor(weapon.RigName))
            {
                if (!clip.Playable) continue;
                foreach (int frame in Frames(clip.Animation.FrameCount, 24))
                {
                    Matrix4x4[] pose = EnvironmentRigs.Pose(prop, weapon.Rig, clip, frame);
                    foreach (WeaponPart part in moving)
                    {
                        if (part.Bone >= pose.Length) continue;
                        float moved = corners[part].Max(c => Vector3.Distance(Vector3.Transform(c, pose[part.Bone]), Vector3.Transform(c, rest[part.Bone])));
                        if (moved < 0.001f) continue;
                        part.Travel = Math.Max(part.Travel, moved);
                        string label = ClipLabel(clip);
                        if (!part.MovedBy.Contains(label)) part.MovedBy.Add(label);
                    }
                }
            }
            return true;
        }

        private static IEnumerable<int> Frames(int count, int most)
        {
            if (count <= 0) yield break;
            int step = Math.Max(1, (int)Math.Ceiling(count / (double)most));
            for (int f = 0; f < count; f += step) yield return f;
            if ((count - 1) % step != 0) yield return count - 1;
        }

        private static Vector3[] BoxCorners(List<Submesh> submeshes)
        {
            if (submeshes.Count == 0) return new[] { Vector3.Zero };
            Vector3 min = submeshes.Select(o => o.MinBounds).Aggregate(Vector3.Min), max = submeshes.Select(o => o.MaxBounds).Aggregate(Vector3.Max);
            return new[]
            {
                new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z),
                new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z),
            };
        }

        /// <summary>Links anywhere in the level that show or hide a part (in its own composite, or through an alias of it).</summary>
        private static void FindScripted(Commands commands, WeaponComposite weapon)
        {
            Dictionary<ShortGuid, WeaponPart> parts = new Dictionary<ShortGuid, WeaponPart>();
            foreach (WeaponPart part in weapon.Parts) parts[part.Entity.shortGUID] = part;
            foreach (Entity entity in weapon.Composite.GetEntities())
                foreach (EntityConnector link in entity.childLinks)
                    if ((link.linkedParamID == _show || link.linkedParamID == _hide) && parts.TryGetValue(link.linkedEntityID, out WeaponPart part))
                        part.Scripted.Add(McpScript.EntityName(commands, weapon.Composite, entity) + "." + McpScript.ParamName(link.thisParamID) + " -> " + McpScript.ParamName(link.linkedParamID));
            foreach (Composite composite in commands.Entries)
                foreach (AliasEntity alias in composite.aliases)
                {
                    ShortGuid[] path = alias.alias?.path;
                    if (path == null || path.Length < 2) continue;
                    ShortGuid last = path[path.Length - 1].IsInvalid ? path[path.Length - 2] : path[path.Length - 1];
                    if (!parts.TryGetValue(last, out WeaponPart part)) continue;
                    foreach (Entity entity in composite.GetEntities())
                        foreach (EntityConnector link in entity.childLinks)
                            if (link.linkedEntityID == alias.shortGUID && (link.linkedParamID == _show || link.linkedParamID == _hide))
                                part.Scripted.Add(Leaf(composite) + ": " + McpScript.EntityName(commands, composite, entity) + "." + McpScript.ParamName(link.thisParamID) + " -> " + McpScript.ParamName(link.linkedParamID));
                }
        }

        private static List<WeaponNpcType> FindNpcTypes(Commands commands, Composite thirdPerson)
        {
            List<WeaponNpcType> types = new List<WeaponNpcType>();
            //The weapon archetypes NPCs carry (NPC_Shotgun_Standard ...): other composites placing Base_TP (a wall weapon scanner's display) are not NPC types
            foreach (Composite weapon in commands.Entries.Where(o => o?.name != null && o.name.IndexOf("Archetypes\\Weapons\\", StringComparison.OrdinalIgnoreCase) >= 0))
                foreach (FunctionEntity tpInstance in weapon.functions.Where(o => o.function == thirdPerson.shortGUID))
                    foreach (Composite npc in commands.Entries)
                        foreach (FunctionEntity instance in npc.functions.Where(o => o.function == weapon.shortGUID))
                            types.Add(new WeaponNpcType() { Composite = npc, Instance = instance, Weapon = weapon, ThirdPersonInstance = tpInstance });
            return types;
        }
        #endregion

        #region Geometry of the retail parts
        private sealed class SameSubmesh : IEqualityComparer<Submesh>
        {
            public bool Equals(Submesh a, Submesh b) => ReferenceEquals(a, b);
            public int GetHashCode(Submesh s) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s);
        }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Level, Dictionary<Submesh, Models.CS2>> _owners = new System.Runtime.CompilerServices.ConditionalWeakTable<Level, Dictionary<Submesh, Models.CS2>>();

        /// <summary>The model a submesh belongs to, by reference (Models.FindModel compares every model by value: slow per part). New models fall back to FindModel.</summary>
        public static Models.CS2 ModelOf(Level level, Submesh submesh)
        {
            if (submesh == null) return null;
            Dictionary<Submesh, Models.CS2> owners = _owners.GetValue(level, l =>
            {
                Dictionary<Submesh, Models.CS2> map = new Dictionary<Submesh, Models.CS2>(new SameSubmesh());
                foreach (Models.CS2 model in l.Models.Entries)
                    foreach (Models.CS2.Component component in model.Components)
                        foreach (Models.CS2.Component.LOD lod in component.LODs)
                            foreach (Submesh s in lod.Submeshes)
                                if (s != null && !map.ContainsKey(s)) map[s] = model;
                return map;
            });
            if (owners.TryGetValue(submesh, out Models.CS2 found) && level.Models.Entries.Any(m => ReferenceEquals(m, found))) return found;
            found = level.Models.Entries.FirstOrDefault(m => m.Components.Any(c => c.LODs.Any(l => l.Submeshes.Any(s => ReferenceEquals(s, submesh)))));
            if (found != null) owners[submesh] = found;
            return found;
        }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Submesh, List<Vector3>> _corners = new System.Runtime.CompilerServices.ConditionalWeakTable<Submesh, List<Vector3>>();

        /// <summary>A submesh's triangles, three corners each, in its own model space.</summary>
        public static List<Vector3> Corners(Submesh submesh)
        {
            return _corners.GetValue(submesh, s =>
            {
                cMesh mesh = s.ToMesh();
                List<Vector3> corners = new List<Vector3>(mesh.Indices.Count);
                for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
                {
                    int a = mesh.Indices[i], b = mesh.Indices[i + 1], c = mesh.Indices[i + 2];
                    if (a >= mesh.Vertices.Count || b >= mesh.Vertices.Count || c >= mesh.Vertices.Count) continue;
                    corners.Add(mesh.Vertices[a]); corners.Add(mesh.Vertices[b]); corners.Add(mesh.Vertices[c]);
                }
                return corners;
            });
        }

        /// <summary>A part's triangles placed in its composite at rest.</summary>
        public static List<Vector3> RestCorners(WeaponPart part) => part.Lod0.SelectMany(Corners).Select(o => Vector3.Transform(o, part.Rest)).ToList();

        /// <summary>Everything a composite draws, at rest, in its own space (hidden parts too unless <paramref name="shownOnly"/>).</summary>
        public static List<Vector3> RestCorners(WeaponComposite weapon, bool shownOnly)
        {
            if (weapon == null) return new List<Vector3>();
            return weapon.Parts.Where(o => !shownOnly || o.ShowOnReset).SelectMany(RestCorners).ToList();
        }
        #endregion

        #region The user's model
        /// <summary>
        /// Read a model file the way import_model will (same post-processing, units, node transforms, sidecar), keeping each mesh's
        /// triangles in weapon space. Meshes named REF_* (the reference the wizard exports) are listed but never used.
        /// </summary>
        public static WeaponModelSource ReadModel(string path, float scale)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new McpError("There is no model file at '" + path + "'.");
            if (scale <= 0 || float.IsNaN(scale) || float.IsInfinity(scale)) throw new McpError("'scale' has to be above 0.");
            Scene scene;
            try
            {
                using (AssimpContext importer = new AssimpContext())
                    scene = importer.ImportFile(path, ModelIO.ImportPostProcessSteps);
            }
            catch (Exception e) { throw new McpError(System.IO.Path.GetFileName(path) + " could not be read: " + e.Message); }
            if (scene == null || scene.MeshCount == 0) throw new McpError("No meshes were found in " + System.IO.Path.GetFileName(path) + ".");
            ModelIO.ModelMetadata metadata = ModelIO.TryLoadSidecar(path);
            ModelIO.ImportPlan plan = ModelIO.CreateImportPlan(scene, metadata, System.IO.Path.GetFileNameWithoutExtension(path));
            plan.Scale = scale;
            if (!plan.HasMetadata) plan.UnitScale = ModelIO.FormatUnitScale(path);
            WeaponModelSource source = new WeaponModelSource() { Path = path, Scale = scale, UnitScale = plan.UnitScale };
            source.Materials = scene.Materials.Select(o => o.Name ?? "").ToList();
            foreach (ModelIO.PlannedSubmesh planned in plan.AllSubmeshes())
            {
                Mesh mesh = scene.Meshes[planned.MeshIndex];
                if (source.Meshes.Any(o => o.Index == planned.MeshIndex)) continue;
                Matrix4x4 transform = ModelIO.VertexTransform(mesh, planned.Transform, planned.Metadata, plan.UnitScale);
                WeaponSourceMesh read = new WeaponSourceMesh()
                {
                    Index = planned.MeshIndex,
                    Name = planned.MeshName ?? ("Mesh " + planned.MeshIndex),
                    MaterialIndex = mesh.MaterialIndex,
                    Material = mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.MaterialCount ? scene.Materials[mesh.MaterialIndex].Name ?? "" : "",
                    Skinned = mesh.HasBones,
                };
                float k = scale / plan.UnitScale;
                foreach (Face face in mesh.Faces)
                {
                    if (face.IndexCount != 3) continue;
                    foreach (int index in face.Indices)
                    {
                        Vector3D v = mesh.Vertices[index];
                        read.Corners.Add(Vector3.Transform(new Vector3(v.X, v.Y, v.Z), transform) * k);
                    }
                }
                if (read.Skinned) source.Notes.Add("Mesh '" + read.Name + "' is skinned; it comes in unskinned, where its vertices are (the bind pose).");
                source.Meshes.Add(read);
            }
            return source;
        }

        /// <summary>The offset and rotation the user gives, as a weapon-space matrix applied after the file's own placement.</summary>
        public static Matrix4x4 UserTransform(WeaponOptions options)
        {
            Matrix4x4 m = Matrix4x4.CreateFromQuaternion(InstanceTransform.ToQuaternion(options.Rotation));
            m.Translation = options.Offset;
            return m;
        }
        #endregion

        #region Planning
        /// <summary>Work out everything a replace would do (UI thread). Nothing changes.</summary>
        public static WeaponPlan Plan(Level level, WeaponOptions options)
        {
            Commands commands = level.Commands;
            WeaponInfo info = Analyse(level, options.Weapon);
            if (string.IsNullOrWhiteSpace(options.ModelPath)) return VisibilityPlan(info, options);
            WeaponModelSource source = ReadModel(options.ModelPath, options.Scale);
            WeaponPlan plan = new WeaponPlan() { Info = info, Options = options, Source = source, UserTransform = UserTransform(options) };
            plan.Notes.AddRange(source.Notes);
            plan.Notes.AddRange(info.Notes);
            plan.Prefix = string.IsNullOrWhiteSpace(options.Prefix) ? "WEAPONS\\" + Norm(Segments(info.FirstPerson.Path)[2]) : AssetPrefix(options.Prefix);
            List<WeaponSourceMesh> usable = source.Usable.ToList();
            if (usable.Count == 0)
                throw new McpError(System.IO.Path.GetFileName(source.Path) + " has no meshes to use" + (source.Meshes.Any(o => o.Reference) ? " (meshes named REF_* are the exported reference and are left out)" : "") + ".");

            //Which composites change
            plan.Targets = ChooseTargets(info, options, plan.Notes);
            if (plan.Targets.Count == 0) throw new McpError("Nothing to change: no target composite is chosen.");

            //NPC types: the third-person composite is changed in a copy only they use
            if (options.NpcTypes.Count != 0)
            {
                if (info.ThirdPerson == null) throw new McpError(info.Key + " has no third-person composite, so there is nothing to give NPC types.");
                foreach (string wanted in options.NpcTypes)
                {
                    List<WeaponNpcType> matched = info.NpcTypes.Where(o => string.Equals(o.Composite.name, McpScript.NormalisePath(wanted), StringComparison.OrdinalIgnoreCase) || string.Equals(Leaf(o.Composite), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matched.Count == 0)
                        throw new McpError(McpErrorCodes.NotFound, "'" + wanted + "' is not an NPC type carrying " + info.Key + ". They are: " + (info.NpcTypes.Count == 0 ? "(none in this level)" : string.Join(", ", info.NpcTypes.Select(o => o.Composite.name).Distinct())) + ".");
                    foreach (WeaponNpcType type in matched) if (!plan.NpcTypes.Contains(type)) plan.NpcTypes.Add(type);
                }
                string suffix = new string((options.NpcSuffix ?? "CUSTOM").ToUpperInvariant().Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                if (suffix.Length == 0) suffix = "CUSTOM";
                plan.WeaponCopySuffix = suffix;
                plan.ThirdPersonCopy = plan.Prefix + "\\" + Leaf(info.ThirdPerson.Composite) + "_" + suffix;
                foreach (Composite weapon in plan.NpcTypes.Select(o => o.Weapon).Distinct())
                    plan.WeaponCopies[weapon] = plan.Prefix + "\\" + Leaf(weapon) + "_" + suffix;
                if (!plan.Targets.Contains(info.ThirdPerson)) plan.Targets.Add(info.ThirdPerson);
            }

            foreach (WeaponComposite target in plan.Targets)
                PlanParts(plan, target, usable, options);
            if (!plan.Parts.Any(o => o.Action == "replace"))
                throw new McpError("No part is given a mesh of the file. Name a mesh after a part, or say which meshes go where with 'parts'.");

            PlanMuzzle(plan, level);
            PlanMoves(plan, commands);
            PlanPhysics(plan);
            if (options.Clearance) plan.Clearance = Clearance(plan);
            return plan;
        }

        private static string AssetPrefix(string prefix) => McpScript.NormalisePath(prefix).ToUpperInvariant();

        /// <summary>
        /// No model: only show or hide first-person parts (put back a retail part an earlier replace hid, say). One set_parameters, an undo step;
        /// nothing is imported.
        /// </summary>
        private static WeaponPlan VisibilityPlan(WeaponInfo info, WeaponOptions options)
        {
            WeaponPlan plan = new WeaponPlan() { Info = info, Options = options, Source = new WeaponModelSource(), Prefix = "" };
            plan.Targets.Add(info.FirstPerson);
            if (options.PartMeshes.Count != 0)
                throw new McpError("parts names meshes for " + string.Join(", ", options.PartMeshes.Keys) + ", but no model file is given: give 'path'.");
            List<KeyValuePair<string, string>> asked = options.PartActions.Where(o => o.Value == "show" || o.Value == "hide").ToList();
            if (asked.Count == 0)
                throw new McpError("Give 'path' (the model to put on " + info.Key + "), or without one, parts to show or hide: {\"TRIGGER\": \"show\"}.");
            foreach (KeyValuePair<string, string> entry in asked)
            {
                WeaponPart part = info.FirstPerson.Parts.FirstOrDefault(o => string.Equals(o.Name, entry.Key, StringComparison.OrdinalIgnoreCase))
                    ?? throw new McpError(McpErrorCodes.NotFound, "parts: " + info.FirstPerson.Path + " has no drawn part '" + entry.Key + "'. Its parts: " + string.Join(", ", info.FirstPerson.Parts.Select(o => o.Name)) + ".");
                plan.Parts.Add(new WeaponPartPlan() { Target = info.FirstPerson, Part = part, Action = entry.Value, Reason = "as asked (" + (part.ShowOnReset ? "shown" : "hidden") + " now)", CompositePath = info.FirstPerson.Path });
            }
            plan.Notes.Add("No model file: only the parts' visibility changes (show_on_reset), one undo step.");
            return plan;
        }

        private static List<WeaponComposite> ChooseTargets(WeaponInfo info, WeaponOptions options, List<string> notes)
        {
            if (options.Targets == null)
            {
                List<WeaponComposite> auto = new List<WeaponComposite>() { info.FirstPerson };
                if (info.ThirdPerson != null && info.ThirdPerson.Selected) auto.Add(info.ThirdPerson);
                else if (info.ThirdPerson != null) notes.Add(info.ThirdPerson.Path + " was left alone: its geometry does not line up with the first person's (" + Mm(info.ThirdPerson.Fit.Residual) + " apart). Name it in 'targets' to change it anyway.");
                auto.AddRange(info.Props.Where(o => o.Selected));
                foreach (WeaponComposite skipped in info.Props.Where(o => !o.Selected))
                    notes.Add(skipped.Path + " was left alone: its geometry does not match the weapon's (" + Mm(skipped.Fit.Residual) + " apart). Name it in 'targets' to change it anyway.");
                return auto;
            }
            List<WeaponComposite> chosen = new List<WeaponComposite>();
            foreach (string wanted in options.Targets)
            {
                string w = (wanted ?? "").Trim();
                IEnumerable<WeaponComposite> matched;
                if (string.Equals(w, "first_person", StringComparison.OrdinalIgnoreCase)) matched = new[] { info.FirstPerson };
                else if (string.Equals(w, "third_person", StringComparison.OrdinalIgnoreCase)) matched = info.ThirdPerson == null ? new WeaponComposite[0] : new[] { info.ThirdPerson };
                else if (string.Equals(w, "pickup", StringComparison.OrdinalIgnoreCase) || string.Equals(w, "dropped", StringComparison.OrdinalIgnoreCase)) matched = info.Props.Where(o => string.Equals(o.Role, w, StringComparison.OrdinalIgnoreCase) && o.Selected);
                else if (string.Equals(w, "props", StringComparison.OrdinalIgnoreCase)) matched = info.Props.Where(o => o.Selected);
                else matched = info.All.Where(o => string.Equals(o.Path, McpScript.NormalisePath(w), StringComparison.OrdinalIgnoreCase));
                List<WeaponComposite> list = matched.ToList();
                if (list.Count == 0)
                    throw new McpError(McpErrorCodes.NotFound, "'" + wanted + "' in targets matches nothing of " + info.Key + ". Give first_person, third_person, pickup, dropped, props, or one of: " + string.Join(", ", info.All.Select(o => o.Path)) + ".");
                foreach (WeaponComposite target in list) if (!chosen.Contains(target)) chosen.Add(target);
            }
            return chosen;
        }

        private static List<WeaponSourceMesh> Pick(List<WeaponSourceMesh> meshes, IEnumerable<string> names, string part)
        {
            List<WeaponSourceMesh> picked = new List<WeaponSourceMesh>();
            foreach (string name in names)
            {
                string wanted = (name ?? "").Trim();
                List<WeaponSourceMesh> hit = wanted == "*" ? meshes.ToList()
                    : meshes.Where(o => Norm(o.Name) == Norm(wanted) || Norm(o.Material) == Norm(wanted) || o.Index.ToString(CultureInfo.InvariantCulture) == wanted).ToList();
                if (hit.Count == 0)
                    throw new McpError(McpErrorCodes.NotFound, "parts." + part + ": the file has no mesh '" + wanted + "'. Its meshes: " + string.Join(", ", meshes.Select(o => o.Index + ": " + o.Name + (string.IsNullOrEmpty(o.Material) ? "" : " (" + o.Material + ")"))) + ".");
                foreach (WeaponSourceMesh mesh in hit) if (!picked.Contains(mesh)) picked.Add(mesh);
            }
            return picked;
        }

        /// <summary>
        /// What happens to each drawn part of one target. First person: a part named in 'parts' does as it says; a part a mesh is named after gets
        /// that mesh; whatever meshes are left go on the main part (most triangles); an unmapped part the rig moves or a script shows / hides is kept
        /// (it may be the shell a reload loads), any other is hidden. Third person and props: the whole model goes on the main part (or meshes named
        /// after a part go there), the rest are hidden.
        /// </summary>
        private static void PlanParts(WeaponPlan plan, WeaponComposite target, List<WeaponSourceMesh> meshes, WeaponOptions options)
        {
            bool first = target.Role == "first_person";
            string compositePath = target == plan.Info.ThirdPerson && plan.ThirdPersonCopy != null ? plan.ThirdPersonCopy : target.Path;
            List<WeaponPart> drawn = target.Parts.Where(o => o.Lod0.Count != 0).ToList();
            Dictionary<WeaponPart, WeaponPartPlan> plans = drawn.ToDictionary(o => o, o => new WeaponPartPlan() { Target = target, Part = o, CompositePath = compositePath });
            HashSet<WeaponSourceMesh> used = new HashSet<WeaponSourceMesh>();

            if (first)
                foreach (WeaponPart part in drawn)
                {
                    WeaponPartPlan p = plans[part];
                    string action = options.PartActions.TryGetValue(part.Name, out string a) ? a : "auto";
                    if (action == "keep" || action == "hide" || action == "show") { p.Action = action; p.Reason = "as asked"; continue; }
                    if (options.PartMeshes.TryGetValue(part.Name, out List<string> names))
                    {
                        p.Action = "replace"; p.Reason = "as asked";
                        p.Meshes = Pick(meshes, names, part.Name);
                        foreach (WeaponSourceMesh mesh in p.Meshes) used.Add(mesh);
                    }
                }
            foreach (string named in options.PartActions.Keys.Concat(options.PartMeshes.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
                if (first && !drawn.Any(o => string.Equals(o.Name, named, StringComparison.OrdinalIgnoreCase)))
                    throw new McpError(McpErrorCodes.NotFound, "parts: " + target.Path + " has no drawn part '" + named + "'. Its parts: " + string.Join(", ", drawn.Select(o => o.Name)) + ".");

            //Meshes named after a part go on it
            foreach (WeaponPart part in drawn)
            {
                WeaponPartPlan p = plans[part];
                if (p.Reason == "as asked") continue;
                List<WeaponSourceMesh> named = meshes.Where(o => !used.Contains(o) && (Norm(o.Name) == Norm(part.Name) || (first && Norm(o.Material) == Norm(part.Name)))).ToList();
                if (named.Count == 0) continue;
                p.Action = "replace"; p.Reason = "a mesh is named after it"; p.Meshes = named;
                foreach (WeaponSourceMesh mesh in named) used.Add(mesh);
            }
            //The rest go on the main part
            WeaponPart main = target.Main;
            List<WeaponSourceMesh> rest = meshes.Where(o => !used.Contains(o)).ToList();
            if (main != null && rest.Count != 0)
            {
                WeaponPartPlan p = plans[main];
                if (p.Reason != "as asked" || p.Action == "replace")
                {
                    if (p.Action != "replace") { p.Action = "replace"; p.Reason = "the main part (most triangles): every mesh not named after a part"; }
                    else p.Reason += "; also every mesh not named after a part";
                    p.Meshes.AddRange(rest);
                }
                else if (first)
                    plan.Notes.Add("These meshes go nowhere (the main part " + main.Name + " was told to " + p.Action + "): " + string.Join(", ", rest.Select(o => o.Name)) + ".");
            }
            foreach (WeaponPart part in drawn)
            {
                WeaponPartPlan p = plans[part];
                if (p.Action == "replace" || p.Reason == "as asked") continue;
                if (first && part.Scripted.Count != 0) { p.Action = "keep"; p.Reason = "shown / hidden by script"; }
                else if (first && LooseObject(part.Name)) { p.Action = "keep"; p.Reason = "a loose object the animation carries" + (part.Animated ? " (" + string.Join(", ", part.MovedBy.Take(4)) + ")" : ""); }
                else if (first && part.Animated) { p.Action = "hide"; p.Reason = "a moving detail of the retail shape (" + string.Join(", ", part.MovedBy.Take(4)) + "): name a mesh " + part.Name + " to put yours on its bone, or keep it"; }
                else { p.Action = "hide"; p.Reason = first ? "static and not given a mesh" : "the model goes on " + (main?.Name ?? "the main part"); }
            }
            foreach (WeaponPartPlan p in plans.Values)
            {
                if (p.Action != "replace") continue;
                p.WeaponToPart = target.Fit.WeaponToTarget * Inverse(p.Part.Rest);
                string role = first ? "FP" : target.Role == "third_person" ? (plan.ThirdPersonCopy != null ? "TP_" + plan.WeaponCopySuffix : "TP") : Norm(Leaf(target.Composite));
                p.ModelName = plan.Prefix + "\\" + role + "_" + Norm(p.Part.Name) + ".CS2";
                if (p.Part.ModelName != null && p.Part.ModelName.StartsWith(plan.Prefix + "\\", StringComparison.OrdinalIgnoreCase)) p.PreviousModel = p.Part.ModelName;
            }
            plan.Parts.AddRange(drawn.Select(o => plans[o]));
        }

        private static Matrix4x4 Inverse(Matrix4x4 m) { Matrix4x4.Invert(m, out Matrix4x4 inverse); return inverse; }

        /// <summary>
        /// A part that is a thing of its own rather than a piece of the gun: the shell a reload loads, a revolver's rounds, a magazine, the
        /// medipen's needle, the flare's cap. Kept by default (the animation needs it, whatever the gun looks like); other parts are the
        /// retail shape's and hidden unless a mesh is put on them.
        /// </summary>
        private static bool LooseObject(string name)
        {
            string n = Norm(name);
            return new[] { "SHELL", "BULLET", "ROUND", "CARTRIDGE", "MAGAZINE", "CLIP", "AMMO", "BOLT", "NEEDLE", "CAP", "BATTERY", "CANISTER" }.Any(o => n.StartsWith(o) || n.EndsWith(o));
        }

        /// <summary>The new model's triangles in weapon space (after the user's offset / rotation) for the given meshes.</summary>
        public static List<Vector3> Placed(WeaponPlan plan, IEnumerable<WeaponSourceMesh> meshes) => meshes.SelectMany(o => o.Corners).Select(o => Vector3.Transform(o, plan.UserTransform)).ToList();

        /// <summary>How far along the muzzle line (from the marker, in its +Z direction) a model reaches near that line, or null if no part of it is near.</summary>
        private static float? BarrelEnd(List<Vector3> corners, Vector3 origin, Vector3 forward)
        {
            foreach (float radius in new[] { 0.03f, 0.06f })
            {
                float best = float.MinValue;
                foreach (Vector3 v in corners)
                {
                    Vector3 d = v - origin;
                    float along = Vector3.Dot(d, forward);
                    if ((d - along * forward).Length() > radius) continue;
                    best = Math.Max(best, along);
                }
                if (best > float.MinValue) return best;
            }
            return null;
        }
        /// <summary>
        /// The muzzle: the first-person MUZZLE_POS_MARKER's line (its +Z) through the new model; the furthest vertex near the line is the new
        /// barrel's end. Everything placed within muzzle_radius of the old muzzle - in the first-person composite, the third-person one and
        /// the composites placing them (effects, lights, markers) - moves by the same amount.
        /// </summary>
        private static void PlanMuzzle(WeaponPlan plan, Level level)
        {
            WeaponComposite fp = plan.Info.FirstPerson;
            WeaponOptions options = plan.Options;
            if (string.Equals(options.Muzzle, "keep", StringComparison.OrdinalIgnoreCase)) return;
            if (fp.Muzzle == null)
            {
                if (options.MuzzlePoint != null) plan.Notes.Add(fp.Path + " has no MUZZLE_POS_MARKER; 'muzzle' was not used.");
                return;
            }
            cTransform marker = fp.Muzzle.GetParameter(ShortGuids.position)?.content as cTransform;
            if (marker == null) return;
            Vector3 old = marker.position;
            Vector3 forward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, InstanceTransform.ToQuaternion(marker.rotation)));
            Vector3 muzzle;
            if (options.MuzzlePoint != null) muzzle = options.MuzzlePoint.Value;
            else
            {
                //The barrel's end along the marker's line, measured the same way on the new model and on the retail one: the marker keeps
                //its own distance from the end (retail's sits 1-2 cm inside the shotgun's barrel lip), so the same gun moves nothing
                float? fresh = BarrelEnd(Placed(plan, plan.Parts.Where(o => o.Target == fp && o.Action == "replace").SelectMany(o => o.Meshes).Distinct()), old, forward);
                float? retail = BarrelEnd(fp.Parts.Where(o => o.ShowOnReset).SelectMany(RestCorners).ToList(), old, forward);
                if (fresh == null)
                {
                    plan.Notes.Add("No part of the new model lies along the muzzle line (within 6 cm of it), so the muzzle markers stay where they are. Give 'muzzle' [x, y, z] to put them somewhere.");
                    return;
                }
                muzzle = old + forward * (fresh.Value - (retail ?? 0));
            }            plan.OldMuzzle = old;
            plan.NewMuzzle = muzzle;
            Vector3 delta = muzzle - old;
            if (delta.Length() < 0.002f) { plan.Notes.Add("The new barrel ends where the old one did (within 2 mm): no marker moves."); return; }

            Commands commands = level.Commands;
            foreach (WeaponComposite target in new[] { fp, plan.Info.ThirdPerson }.Where(o => o != null && plan.Targets.Contains(o)))
            {
                string path = target == plan.Info.ThirdPerson && plan.ThirdPersonCopy != null ? plan.ThirdPersonCopy : target.Path;
                Vector3 centre = target == fp ? old : Vector3.Transform(old, target.Fit.WeaponToTarget);
                if (target != fp && target.Muzzle?.GetParameter(ShortGuids.position)?.content is cTransform own) centre = own.position;
                Vector3 shift = Vector3.TransformNormal(delta, target.Fit.WeaponToTarget);
                foreach (FunctionEntity entity in target.Positioned)
                {
                    cTransform t = (cTransform)entity.GetParameter(ShortGuids.position).content;
                    if (Vector3.Distance(t.position, centre) > options.MuzzleRadius) continue;
                    plan.Markers.Add(new WeaponMarkerMove() { CompositePath = path, Entity = entity, Name = McpScript.EntityName(commands, target.Composite, entity), From = t.position, To = t.position + shift, Why = "within " + Cm(options.MuzzleRadius) + " of the muzzle" });
                }
                //What the composites placing it put near the muzzle (a weapon archetype's flame and pilot light)
                foreach (Composite parent in commands.Entries)
                    foreach (FunctionEntity instance in parent.functions.Where(o => o.function == target.Composite.shortGUID))
                    {
                        //A third person kept to chosen NPC types: only their weapon archetypes' copies place the changed copy
                        if (target == plan.Info.ThirdPerson && plan.ThirdPersonCopy != null && !plan.WeaponCopies.ContainsKey(parent)) continue;
                        string parentPath = target == plan.Info.ThirdPerson && plan.WeaponCopies.TryGetValue(parent, out string copy) ? copy : parent.name;
                        Matrix4x4 placeInParent = InstanceTransform.ToMatrix(instance.GetParameter(ShortGuids.position)?.content as cTransform);
                        Matrix4x4 parentToTarget = Inverse(placeInParent);
                        foreach (FunctionEntity entity in parent.functions)
                        {
                            if (ReferenceEquals(entity, instance) || IsPart(entity)) continue;
                            if (!(entity.GetParameter(ShortGuids.position)?.content is cTransform t)) continue;
                            if (Vector3.Distance(Vector3.Transform(t.position, parentToTarget), centre) > options.MuzzleRadius) continue;
                            plan.Markers.Add(new WeaponMarkerMove() { CompositePath = parentPath, Entity = entity, Name = McpScript.EntityName(commands, parent, entity), From = t.position, To = t.position + Vector3.TransformNormal(shift, placeInParent), Why = "near the muzzle, placed by " + Leaf(parent) });
                        }
                    }
            }
        }

        /// <summary>'move': named entities of the first- and third-person composites by an offset in weapon space.</summary>
        private static void PlanMoves(WeaponPlan plan, Commands commands)
        {
            foreach (KeyValuePair<string, Vector3> move in plan.Options.Move)
            {
                bool any = false;
                foreach (WeaponComposite target in new[] { plan.Info.FirstPerson, plan.Info.ThirdPerson }.Where(o => o != null && plan.Targets.Contains(o)))
                {
                    string path = target == plan.Info.ThirdPerson && plan.ThirdPersonCopy != null ? plan.ThirdPersonCopy : target.Path;
                    foreach (FunctionEntity entity in target.Positioned)
                    {
                        string name = McpScript.EntityName(commands, target.Composite, entity);
                        if (Norm(name) != Norm(move.Key) && McpScript.Id(entity.shortGUID) != move.Key) continue;
                        cTransform t = (cTransform)entity.GetParameter(ShortGuids.position).content;
                        WeaponMarkerMove existing = plan.Markers.FirstOrDefault(o => ReferenceEquals(o.Entity, entity) && o.CompositePath == path);
                        Vector3 from = existing?.To ?? t.position;
                        if (existing != null) plan.Markers.Remove(existing);
                        plan.Markers.Add(new WeaponMarkerMove() { CompositePath = path, Entity = entity, Name = name, From = t.position, To = from + Vector3.TransformNormal(move.Value, target.Fit.WeaponToTarget), Why = "as asked (move)" });
                        any = true;
                    }
                }
                if (!any) throw new McpError(McpErrorCodes.NotFound, "move: no marker or other placed entity '" + move.Key + "' in " + plan.Info.FirstPerson.Path + (plan.Info.ThirdPerson != null ? " or " + plan.Info.ThirdPerson.Path : "") + ".");
            }
        }

        /// <summary>
        /// A target with its own physics body (a dropped weapon, a thrown item's Base_TP) whose new model differs a lot in shape from the retail
        /// one gets a hull of its own (a retail hull that doesn't fit the new shape can leave it rocking for ever). 'physics' auto / hull / keep.
        /// </summary>
        private static void PlanPhysics(WeaponPlan plan)
        {
            string mode = (plan.Options.Physics ?? "auto").Trim().ToLowerInvariant();
            if (mode == "keep") return;
            foreach (WeaponComposite target in plan.Targets.Where(o => o.PhysicsSystem != null && o.Role != "first_person"))
            {
                WeaponPartPlan main = plan.Parts.FirstOrDefault(o => o.Target == target && o.Part == target.Main && o.Action == "replace");
                if (main == null) continue;
                WeaponGeometry.Bounds(RestCorners(target.Main).Select(o => Vector3.Transform(o, target.Fit.TargetToWeapon)), out Vector3 rMin, out Vector3 rMax);
                WeaponGeometry.Bounds(Placed(plan, main.Meshes), out Vector3 nMin, out Vector3 nMax);
                Vector3 r = rMax - rMin, n = nMax - nMin;
                float worst = new[] { Ratio(r.X, n.X), Ratio(r.Y, n.Y), Ratio(r.Z, n.Z) }.Min();
                if (mode == "hull" || worst < 0.75f)
                {
                    plan.HullTargets.Add(target);
                    plan.Notes.Add(target.Path + " gets a physics hull of its own made from the new model (" + (mode == "hull" ? "as asked" : "its shape differs from the retail one's by up to " + Math.Round((1 - worst) * 100) + "%") + ").");
                }
            }
        }

        private static float Ratio(float a, float b) => Math.Min(a, b) / Math.Max(1e-5f, Math.Max(a, b));
        #endregion

        #region Clearance
        /// <summary>
        /// The clearance check: for every first-person part that is KEPT and that the rig moves (the shell a reload loads), sampled through
        /// every clip of the rig's sets, how far it rises out of the new model (above its top, +Z, or out of its sides, +-Y) while some of it
        /// is inside. The same measured on the retail model is given beside it as a baseline.
        /// </summary>
        public static JArray Clearance(WeaponPlan plan)
        {
            JArray rows = new JArray();
            WeaponComposite fp = plan.Info.FirstPerson;
            List<WeaponPartPlan> kept = plan.Parts.Where(o => o.Target == fp && o.Action == "keep").ToList();
            List<WeaponPartPlan> replaced = plan.Parts.Where(o => o.Target == fp && o.Action == "replace").ToList();
            if (kept.Count == 0 || replaced.Count == 0) return rows;
            EnvironmentRigs.Prop prop = fp.Entry == null || fp.Rig == null ? null : new EnvironmentRigs.Prop() { Entry = fp.Entry, Composite = fp.Composite };
            List<CathodeLib.Animation.ClipReference> clips = prop == null ? new List<CathodeLib.Animation.ClipReference>() : ClipsFor(fp.RigName).Where(o => o.Playable).ToList();

            //New geometry per replaced part, in its own model space; the retail geometry it replaces, likewise
            Dictionary<WeaponPartPlan, List<Vector3>> fresh = replaced.ToDictionary(o => o, o => Placed(plan, o.Meshes).Select(v => Vector3.Transform(v, Inverse(o.Part.Rest))).ToList());
            Dictionary<WeaponPartPlan, List<Vector3>> retail = replaced.ToDictionary(o => o, o => o.Part.Lod0.SelectMany(Corners).ToList());

            foreach (WeaponPartPlan k in kept)
            {
                List<Vector3> part = WeaponGeometry.Thin(k.Part.Lod0.SelectMany(Corners).Distinct().ToList(), 600);
                Worst now = new Worst(), before = new Worst();
                List<(string clip, int frame, float time, Matrix4x4[] pose)> samples = new List<(string, int, float, Matrix4x4[])>();
                if (prop != null) samples.Add(("rest", 0, 0, EnvironmentRigs.Pose(prop, fp.Rig, null, 0)));
                else samples.Add(("rest", 0, 0, null));
                foreach (CathodeLib.Animation.ClipReference clip in clips)
                {
                    if (k.Part.Animated && !k.Part.MovedBy.Contains(ClipLabel(clip)) && !replaced.Any(r => r.Part.MovedBy.Contains(ClipLabel(clip)))) continue;
                    int frames = clip.Animation.FrameCount;
                    foreach (int f in Frames(frames, 45))
                        samples.Add((ClipLabel(clip), f, frames <= 1 ? 0 : clip.Duration * f / (frames - 1), EnvironmentRigs.Pose(prop, fp.Rig, clip, f)));
                }
                Worst excess = new Worst();
                foreach ((string clip, int frame, float time, Matrix4x4[] pose) in samples)
                {
                    Func<WeaponPart, Matrix4x4> place = p => pose != null && p.Bone >= 0 && p.Bone < pose.Length ? pose[p.Bone] : p.Rest;
                    List<Vector3> shell = part.Select(v => Vector3.Transform(v, place(k.Part))).ToList();
                    Sample n = Measure(shell, replaced.SelectMany(r => fresh[r].Select(v => Vector3.Transform(v, place(r.Part)))).ToList());
                    Sample r0 = Measure(shell, replaced.SelectMany(r => retail[r].Select(v => Vector3.Transform(v, place(r.Part)))).ToList());
                    string at = clip + " " + time.ToString("0.00", CultureInfo.InvariantCulture) + " s";
                    now.Add(n, at);
                    before.Add(r0, at);
                    if (!n.Inside) continue;
                    //Frame by frame against what retail shows at the same moment: a shell half out of the retail gun as it goes in is fine
                    excess.Add(new Sample() { Inside = true, Above = n.Above - (r0.Inside ? Math.Max(0, r0.Above) : 0), Side = n.Side - (r0.Inside ? Math.Max(0, r0.Side) : 0) }, at);
                }
                JObject row = new JObject()
                {
                    ["part"] = k.Part.Name,
                    ["bone"] = k.Part.BoneName,
                    ["samples"] = samples.Count,
                    ["overlaps_new_model"] = now.Inside,
                };
                if (now.Inside)
                {
                    row["above_top_cm"] = Cm2(now.Above); row["above_at"] = now.AboveAt;
                    row["out_of_side_cm"] = Cm2(now.Side); row["side_at"] = now.SideAt;
                    row["retail_above_top_cm"] = Cm2(before.Above); row["retail_above_at"] = before.AboveAt;
                    row["retail_out_of_side_cm"] = Cm2(before.Side);
                    row["more_than_retail_above_cm"] = Cm2(excess.Above); row["more_than_retail_above_at"] = excess.AboveAt;
                    row["more_than_retail_side_cm"] = Cm2(excess.Side); row["more_than_retail_side_at"] = excess.SideAt;
                    if (excess.Above > 0.002f)
                        row["verdict"] = k.Part.Name + " rises " + Cm(excess.Above) + " further out of the top of the new model than of the retail one, at " + excess.AboveAt + ". Raise the model about " + Cm(excess.Above + 0.002f) + " (offset z), or hide the part.";
                    else if (excess.Side > 0.002f)
                        row["verdict"] = k.Part.Name + " shows " + Cm(excess.Side) + " further out of the side of the new model than of the retail one, at " + excess.SideAt + ". Widen the model there, or hide the part.";
                    else row["verdict"] = "clear: frame by frame it never shows more of itself through the new model than through the retail one.";
                }
                else row["verdict"] = "clear: it never overlaps the new model.";
                rows.Add(row);
            }
            return rows;
        }

        private struct Sample
        {
            public bool Inside;
            public float Above, Side;
        }

        private sealed class Worst
        {
            public bool Inside;
            public float Above = float.MinValue, Side = float.MinValue;
            public string AboveAt = "", SideAt = "";

            public void Add(Sample s, string at)
            {
                if (!s.Inside) return;
                Inside = true;
                if (s.Above > Above) { Above = s.Above; AboveAt = at; }
                if (s.Side > Side) { Side = s.Side; SideAt = at; }
            }
        }

        /// <summary>A part against a model at one moment: whether any of it is inside the model, and how far it rises above the model's top / out of its sides.</summary>
        private static Sample Measure(List<Vector3> part, List<Vector3> model)
        {
            Sample s = new Sample() { Above = float.MinValue, Side = float.MinValue };
            if (model.Count < 3) return s;
            WeaponGeometry.ColumnIndex up = new WeaponGeometry.ColumnIndex(model, 2), side = new WeaponGeometry.ColumnIndex(model, 1);
            int inside = 0;
            foreach (Vector3 v in part)
            {
                if (up.Query(v, out float low, out float high))
                {
                    if (v.Z >= low && v.Z <= high) inside++;
                    s.Above = Math.Max(s.Above, v.Z - high);
                }
                if (side.Query(v, out float left, out float right))
                    s.Side = Math.Max(s.Side, Math.Max(v.Y - right, left - v.Y));
            }
            s.Inside = inside != 0;
            return s;
        }
        #endregion

        #region Describing
        public static string Mm(float metres) => Math.Round(metres * 1000, 1).ToString(CultureInfo.InvariantCulture) + " mm";
        public static string Cm(float metres) => Math.Round(metres * 100, 1).ToString(CultureInfo.InvariantCulture) + " cm";
        private static JToken Cm2(float metres) => metres == float.MinValue ? (JToken)JValue.CreateNull() : Math.Round(metres * 100, 2);
        private static JArray V(Vector3 v) => new JArray(Math.Round(v.X, 4), Math.Round(v.Y, 4), Math.Round(v.Z, 4));

        public static JObject Describe(WeaponInfo info, Commands commands)
        {
            JObject result = new JObject()
            {
                ["weapon"] = info.Key,
                ["first_person"] = Describe(info.FirstPerson, commands, true),
            };
            if (info.ThirdPerson != null) result["third_person"] = Describe(info.ThirdPerson, commands, false);
            if (info.Props.Count != 0) result["props"] = new JArray(info.Props.Select(o => Describe(o, commands, false)));
            if (info.NpcTypes.Count != 0)
                result["npc_types"] = new JArray(info.NpcTypes.GroupBy(o => o.Composite).Select(g => new JObject() { ["npc_type"] = g.Key.name, ["weapon"] = g.First().Weapon.name, ["instances"] = g.Count() }));
            result["space"] = "weapon space = " + info.FirstPerson.Path + "'s composite space (metres; for the guns X runs along the barrel and Z is up). Author the new model there: export_weapon_reference writes the retail weapon in it to model against.";
            if (info.Notes.Count != 0) result["notes"] = new JArray(info.Notes);
            return result;
        }

        private static JObject Describe(WeaponComposite weapon, Commands commands, bool first)
        {
            JObject row = new JObject()
            {
                ["composite"] = weapon.Path,
                ["role"] = weapon.Role,
                ["placements"] = weapon.Placements,
            };
            if (weapon.RigName != null) row["rig"] = weapon.RigName;
            if (!first)
            {
                row["fit"] = new JObject()
                {
                    ["placed_by"] = weapon.PlacedBy,
                    ["same_frame"] = weapon.Fit.SameFrame,
                    ["residual_mm"] = Math.Round(weapon.Fit.Residual * 1000, 1),
                    ["accepted"] = weapon.Fit.Accepted,
                };
                if (!weapon.Fit.SameFrame) row["fit"]["turn_degrees"] = Math.Round(WeaponGeometry.TurnAngle(weapon.Fit.TargetToWeapon) * 180 / Math.PI, 1);
            }
            row["parts"] = new JArray(weapon.Parts.Select(p =>
            {
                JObject part = new JObject()
                {
                    ["entity"] = p.Name,
                    ["id"] = McpScript.Id(p.Entity.shortGUID),
                    ["resource_id"] = McpScript.Id(p.ResourceId),
                    ["triangles"] = p.Triangles,
                    ["model"] = p.ModelName,
                };
                if (p.Bone >= 0) { part["bone"] = p.Bone; if (p.BoneName != null) part["bone_name"] = p.BoneName; }
                else part["bone"] = null;
                if (first) { part["moved_by"] = new JArray(p.MovedBy); if (p.Travel > 0) part["travel_cm"] = Math.Round(p.Travel * 100, 1); }
                if (p.Scripted.Count != 0) part["scripted"] = new JArray(p.Scripted.Take(6));
                if (!p.ShowOnReset) part["shown"] = false;
                //Where it sits at rest, in weapon space (for lining a replacement up)
                WeaponGeometry.Bounds(RestCorners(p).Select(v => Vector3.Transform(v, weapon.Fit.TargetToWeapon)), out Vector3 min, out Vector3 max);
                part["bounds"] = new JObject() { ["min"] = V(min), ["max"] = V(max) };
                return part;
            }));
            JArray markers = new JArray();
            foreach (FunctionEntity entity in weapon.Positioned)
            {
                if (!IsType(entity, FunctionType.PositionMarker)) continue;
                cTransform t = (cTransform)entity.GetParameter(ShortGuids.position).content;
                markers.Add(new JObject() { ["entity"] = McpScript.EntityName(commands, weapon.Composite, entity), ["position"] = V(t.position) });
            }
            if (markers.Count != 0) row["markers"] = markers;
            if (weapon.PhysicsSystem != null) row["physics_system"] = McpScript.EntityName(commands, weapon.Composite, weapon.PhysicsSystem);
            return row;
        }

        /// <summary>The plan as dry_run (and the result) show it.</summary>
        public static JObject Describe(WeaponPlan plan, Commands commands)
        {
            JObject result = new JObject()
            {
                ["weapon"] = plan.Info.Key,
                ["model"] = plan.Source.Path,
                ["meshes"] = new JArray(plan.Source.Meshes.Select(o =>
                {
                    WeaponGeometry.Bounds(o.Corners.Select(v => Vector3.Transform(v, plan.UserTransform)), out Vector3 min, out Vector3 max);
                    JObject mesh = new JObject() { ["index"] = o.Index, ["name"] = o.Name, ["material"] = o.Material, ["triangles"] = o.Corners.Count / 3, ["min"] = V(min), ["max"] = V(max) };
                    if (o.Reference) mesh["left_out"] = "REF_ mesh (the exported reference)";
                    return mesh;
                })),
                ["prefix"] = plan.Prefix,
            };
            JArray targets = new JArray();
            foreach (WeaponComposite target in plan.Targets)
            {
                string path = plan.Parts.FirstOrDefault(o => o.Target == target)?.CompositePath ?? target.Path;
                JObject row = new JObject() { ["composite"] = path, ["role"] = target.Role, ["placements"] = target.Placements };
                if (path != target.Path) row["copy_of"] = target.Path;
                if (target.Role != "first_person") row["fit"] = target.Fit.SameFrame ? "same frame as first person" : "turned " + Math.Round(WeaponGeometry.TurnAngle(target.Fit.TargetToWeapon) * 180 / Math.PI) + " degrees, " + Mm(target.Fit.Residual) + " residual";
                row["parts"] = new JArray(plan.Parts.Where(o => o.Target == target).Select(p =>
                {
                    JObject part = new JObject() { ["entity"] = p.Part.Name, ["action"] = p.Action, ["reason"] = p.Reason };
                    if (p.Part.Bone >= 0) part["bone"] = p.Part.BoneName ?? p.Part.Bone.ToString(CultureInfo.InvariantCulture);
                    if (target.Role == "first_person" && p.Part.MovedBy.Count != 0) part["moved_by"] = new JArray(p.Part.MovedBy);
                    if (p.Action == "replace")
                    {
                        part["meshes"] = new JArray(p.Meshes.Select(m => m.Name));
                        part["model"] = p.ImportedModel ?? p.ModelName;
                        part["resource_id_kept"] = McpScript.Id(p.Part.ResourceId);
                    }
                    return part;
                }));
                if (plan.HullTargets.Contains(target)) row["physics"] = "a hull of its own from the new model";
                targets.Add(row);
            }
            result["targets"] = targets;
            JObject materials = new JObject()
            {
                ["plain"] = "generated from the file's materials (CA_ENVIRONMENT, draw pass 70) by the first import, named " + plan.Prefix + "\\<file material>",
            };
            if (plan.HasFirstPerson)
                materials["first_person"] = "a copy of each, <name>_FP, on the NO_CLIP permutation " + NoClipMask + " at draw pass " + FirstPersonPriority + " (flat specular, " + EnvironmentMap + ", white AO), the first-person models' OWN material";
            result["materials"] = materials;
            if (plan.OldMuzzle != null)
                result["muzzle"] = new JObject() { ["from"] = V(plan.OldMuzzle.Value), ["to"] = V(plan.NewMuzzle.Value), ["moved_cm"] = Math.Round((plan.NewMuzzle.Value - plan.OldMuzzle.Value).Length() * 100, 1) };
            if (plan.Markers.Count != 0)
                result["markers"] = new JArray(plan.Markers.Select(o => new JObject() { ["composite"] = o.CompositePath, ["entity"] = o.Name, ["from"] = V(o.From), ["to"] = V(o.To), ["why"] = o.Why }));
            if (plan.NpcTypes.Count != 0)
                result["npc_types"] = new JObject()
                {
                    ["only_for"] = new JArray(plan.NpcTypes.Select(o => o.Composite.name).Distinct()),
                    ["weapon_copies"] = new JArray(plan.WeaponCopies.Select(o => o.Key.name + " -> " + o.Value)),
                    ["third_person_copy"] = plan.Info.ThirdPerson.Path + " -> " + plan.ThirdPersonCopy,
                };
            if (plan.Clearance.Count != 0) result["clearance"] = plan.Clearance;
            result["steps"] = new JArray(Steps(plan).Select(o => o.Describe()));
            if (plan.Notes.Count != 0) result["notes"] = new JArray(plan.Notes.Distinct());
            return result;
        }
        #endregion

        #region Running it
        /// <summary>One thing the replace does, in order: a nested tool call, or a step of the wizard's own (an import with the part's transform, a material copy).</summary>
        internal sealed class Step
        {
            public string Kind;
            public string Tool;
            public JObject Args = new JObject();
            public WeaponPartPlan Part;
            public List<WeaponPartPlan> Parts;
            public bool First;
            public string Note;

            public JObject Describe()
            {
                JObject row = new JObject() { ["step"] = Kind };
                if (Tool != null) row["tool"] = Tool;
                if (Args.Count != 0) row["args"] = Args.DeepClone();
                if (Note != null) row["note"] = Note;
                return row;
            }
        }

        private static IEnumerable<Step> Steps(WeaponPlan plan)
        {
            //Copies for NPC types first: the third-person changes are made in the copy
            if (plan.NpcTypes.Count != 0)
            {
                foreach (KeyValuePair<Composite, string> copy in plan.WeaponCopies)
                    yield return new Step()
                    {
                        Kind = "copy weapon archetype for the NPC types",
                        Tool = "duplicate_composite",
                        Args = new JObject()
                        {
                            ["composite"] = copy.Key.name,
                            ["path"] = copy.Value,
                            ["use_for"] = new JArray(plan.NpcTypes.Where(o => o.Weapon == copy.Key).Select(o => new JObject() { ["composite"] = o.Composite.name, ["instance"] = McpScript.Id(o.Instance.shortGUID) })),
                        },
                    };
                yield return new Step()
                {
                    Kind = "copy the third-person composite for those copies",
                    Tool = "duplicate_composite",
                    Args = new JObject()
                    {
                        ["composite"] = plan.Info.ThirdPerson.Path,
                        ["path"] = plan.ThirdPersonCopy,
                        ["use_for"] = new JArray(plan.WeaponCopies.SelectMany(c => plan.NpcTypes.Where(o => o.Weapon == c.Key).Select(o => McpScript.Id(o.ThirdPersonInstance.shortGUID)).Distinct().Select(id => new JObject() { ["composite"] = c.Value, ["instance"] = id }))),
                    },
                };
            }
            List<WeaponPartPlan> imports = plan.Parts.Where(o => o.Action == "replace").ToList();
            WeaponPartPlan firstImport = imports.FirstOrDefault(o => o.Target.Role != "first_person");
            bool sourceImport = firstImport == null;
            if (plan.HasFirstPerson)
                yield return new Step() { Kind = "flat textures for first-person materials", Note = "import_texture " + FlatTextures.Select(o => o.Item1).Aggregate((a, b) => a + ", " + b) + " where the level lacks them" };
            if (imports.Count != 0)
            yield return new Step()
            {
                Kind = sourceImport ? "import the whole model in weapon space (makes the plain materials)" : "import " + firstImport.Part.Name + " of " + firstImport.CompositePath + " (makes the plain materials)",
                Tool = "import_model",
                Part = firstImport,
                First = true,
                Args = ImportArgs(plan, firstImport, true),
            };
            if (plan.HasFirstPerson)
                yield return new Step() { Kind = "first-person material copies", Note = "<plain>_FP: create_material copy_from <plain>; edit_material mask " + NoClipMask + ", DIFFUSE_MAP / NORMAL_MAP from <plain>, SPECULAR_MAP / AMBIENT_OCCLUSION_MAP flat, ENVIRONMENT_MAP " + EnvironmentMap + ", priority " + FirstPersonPriority };
            foreach (WeaponPartPlan p in imports.OrderBy(o => o.Target.Role == "first_person" ? 0 : 1))
                if (p != firstImport)
                    yield return new Step() { Kind = "import " + p.Part.Name + " of " + p.CompositePath, Tool = "import_model", Part = p, Args = ImportArgs(plan, p, false) };
            foreach (WeaponPartPlan p in imports)
                yield return new Step()
                {
                    Kind = "draw it with " + p.Part.Name + " (keeps resource id " + McpScript.Id(p.Part.ResourceId) + (p.Part.Bone >= 0 ? ", on bone " + (p.Part.BoneName ?? p.Part.Bone.ToString(CultureInfo.InvariantCulture)) : "") + ")",
                    Tool = "set_renderable",
                    Part = p,
                    Args = new JObject() { ["composite"] = p.CompositePath, ["entity"] = McpScript.Id(p.Part.Entity.shortGUID), ["model"] = p.ImportedModel ?? p.ModelName, ["part"] = 0 },
                };
            foreach (IGrouping<string, WeaponPartPlan> group in plan.Parts.Where(o => ((o.Action == "replace" || o.Action == "show") && !o.Part.ShowOnReset) || (o.Action == "hide" && o.Part.ShowOnReset)).GroupBy(o => o.CompositePath))
                yield return new Step()
                {
                    Kind = "show the replaced parts, hide the retail ones",
                    Tool = "set_parameters",
                    Parts = group.ToList(),
                    Args = new JObject() { ["composite"] = group.Key, ["changes"] = new JArray(group.Select(o => new JObject() { ["entity"] = McpScript.Id(o.Part.Entity.shortGUID), ["parameters"] = new JObject() { ["show_on_reset"] = o.Action == "replace" || o.Action == "show" } })) },
                };
            foreach (IGrouping<string, WeaponMarkerMove> group in plan.Markers.GroupBy(o => o.CompositePath))
                yield return new Step()
                {
                    Kind = "move markers and effects",
                    Tool = "set_parameters",
                    Args = new JObject() { ["composite"] = group.Key, ["changes"] = new JArray(group.Select(o => new JObject() { ["entity"] = McpScript.Id(o.Entity.shortGUID), ["parameters"] = new JObject() { ["position"] = new JObject() { ["position"] = V(o.To) } } })) },
                };
            foreach (WeaponComposite target in plan.HullTargets)
            {
                WeaponPartPlan main = plan.Parts.First(o => o.Target == target && o.Part == target.Main);
                string path = main.CompositePath;
                yield return new Step()
                {
                    Kind = "physics hull from the new model",
                    Tool = "import_physics_system",
                    Part = main,
                    Args = new JObject() { ["composite"] = path, ["model"] = main.ImportedModel ?? main.ModelName, ["system_name"] = (path.Replace('\\', '_') + "_HULL").ToUpperInvariant(), ["place_at_model"] = true, ["bind"] = true },
                };
            }
            foreach (WeaponPartPlan p in imports.Where(o => o.PreviousModel != null))
                yield return new Step() { Kind = "remove the model an earlier replace made, if nothing else draws it", Tool = "edit_model", Part = p, Args = new JObject() { ["model"] = p.PreviousModel, ["action"] = "delete" } };
        }

        private static JObject ImportArgs(WeaponPlan plan, WeaponPartPlan p, bool first)
        {
            JObject args = new JObject()
            {
                ["path"] = plan.Source.Path,
                ["name"] = p == null ? plan.Prefix + "\\SOURCE.CS2" : p.ModelName,
                ["create_composite"] = false,
                ["skeleton"] = "none",
            };
            if (plan.Source.Scale != 1) args["scale"] = plan.Source.Scale;
            if (first)
            {
                JObject generate = new JObject();
                foreach (string material in (p == null ? plan.Source.Usable : p.Meshes).Select(o => o.Material).Distinct())
                    if (!generate.ContainsKey(material ?? ""))
                        generate[material ?? ""] = new JObject() { ["family"] = "CA_ENVIRONMENT", ["name"] = PlainMaterialName(plan, material) };
                args["generate"] = generate;
            }
            else args["materials"] = "(each mesh's " + (p.Target.Role == "first_person" ? "first-person copy" : "plain material") + ", set when it runs)";
            args["meshes"] = new JArray((p == null ? plan.Source.Usable : p.Meshes).Select(o => o.Name));
            args["transform"] = p == null ? "none (weapon space)" : "weapon space -> " + p.Part.Name + "'s own space" + (p.Target.Role == "first_person" || p.Target.Fit.SameFrame ? "" : " via the fit");
            return args;
        }

        private static string PlainMaterialName(WeaponPlan plan, string material)
        {
            string leaf = new string(((material ?? "").Length == 0 || material == "DefaultMaterial" ? System.IO.Path.GetFileNameWithoutExtension(plan.Source.Path) : material).Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
            return plan.Prefix + "\\" + leaf.ToUpperInvariant();
        }

        private static readonly Tuple<string, Color, bool>[] FlatTextures =
        {
            Tuple.Create("WEAPONS\\WPN_FLAT_SPEC", Color.FromArgb(40, 90, 40), false),
            Tuple.Create("WEAPONS\\WPN_WHITE", Color.FromArgb(255, 255, 255), true),
            Tuple.Create("WEAPONS\\WPN_FLAT_NORMAL", Color.FromArgb(128, 128, 255), false),
        };

        /// <summary>Do it: every step through the tools that already make each change (worker thread). Not one undo step: imports cannot be undone.</summary>
        public static JObject Execute(McpCall call, WeaponPlan plan)
        {
            JArray done = new JArray();
            Dictionary<int, Materials.Material> plain = new Dictionary<int, Materials.Material>();
            Dictionary<Materials.Material, string> firstPerson = new Dictionary<Materials.Material, string>();
            List<Step> steps = Steps(plan).ToList();
            int index = 0;
            foreach (Step step in steps)
            {
                call.ThrowIfCancelled();
                call.Progress(step.Kind, ++index, steps.Count);
                JObject row = new JObject() { ["step"] = step.Kind };
                try
                {
                    switch (step.Tool)
                    {
                        case "import_model":
                            {
                                JObject imported = Import(call, plan, step, plain, firstPerson);
                                row["model"] = imported["model"];
                                if (imported["materials_generated"] is JArray made && made.Count != 0) row["materials_generated"] = made;
                                if (imported["materials_picked"] is JArray picked && picked.Count != 0) row["materials"] = picked;
                                break;
                            }
                        case null:
                            if (step.Kind.StartsWith("flat textures")) row["textures"] = EnsureFlatTextures(call);
                            else if (step.Kind.StartsWith("first-person material")) row["materials"] = MakeFirstPersonMaterials(call, plan, plain, firstPerson);
                            break;
                        case "set_renderable":
                            step.Args["model"] = step.Part.ImportedModel ?? step.Part.ModelName;
                            row["result"] = Brief(McpEditorTools.Nested(call, step.Tool, step.Args), "renderable", "placements_affected");
                            break;
                        case "import_physics_system":
                            step.Args["model"] = step.Part.ImportedModel ?? step.Part.ModelName;
                            CopyBodyValues(call, plan, step);
                            row["result"] = Brief(McpEditorTools.Nested(call, step.Tool, step.Args), "system_index", "system", "body", "mass", "bound");
                            break;
                        case "edit_model":
                            if (string.Equals(step.Args["model"]?.ToString(), step.Part.ImportedModel, StringComparison.OrdinalIgnoreCase)) { row["skipped"] = "it is the new model"; break; }
                            try { McpEditorTools.Nested(call, step.Tool, step.Args); row["removed"] = step.Args["model"]; }
                            catch (McpError e) { row["kept"] = step.Args["model"] + ": " + e.Message; }
                            break;
                        default:
                            row["result"] = Brief(McpEditorTools.Nested(call, step.Tool, step.Args), "path", "composite", "switched", "placements_affected", "changed");
                            break;
                    }
                    if (step.Tool != null) row["tool"] = step.Tool;
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    row["error"] = e.Message;
                    done.Add(row);
                    throw new McpError(McpErrorCodes.Failed, "Step " + index + " of " + steps.Count + " (" + step.Kind + ") failed: " + e.Message + " The steps before it were made (" + string.Join("; ", done.Take(done.Count - 1).Select(o => (string)o["step"])) + ").");
                }
                done.Add(row);
            }
            return new JObject() { ["steps"] = done, ["check"] = Check(call, plan) };
        }

        private static JToken Brief(JObject result, params string[] keys)
        {
            JObject brief = new JObject();
            foreach (string key in keys) if (result[key] != null) brief[key] = result[key];
            return brief.Count == 0 ? (JToken)"done" : brief;
        }

        /// <summary>
        /// One import: the file's meshes for this part, moved from weapon space into the part's own space on the way in (the planned submeshes'
        /// transforms, through import_model's prepare step), with the plain or first-person material each mesh draws with as its OWN material.
        /// </summary>
        private static JObject Import(McpCall call, WeaponPlan plan, Step step, Dictionary<int, Materials.Material> plain, Dictionary<Materials.Material, string> firstPerson)
        {
            WeaponPartPlan p = step.Part;
            HashSet<int> meshes = new HashSet<int>((p == null ? plan.Source.Usable : p.Meshes).Select(o => o.Index));
            Matrix4x4 move = plan.UserTransform * (p == null ? Matrix4x4.Identity : p.WeaponToPart);
            JObject args = new JObject()
            {
                ["path"] = plan.Source.Path,
                ["name"] = step.Args["name"],
                ["create_composite"] = false,
                ["skeleton"] = "none",
            };
            if (plan.Source.Scale != 1) args["scale"] = plan.Source.Scale;
            if (step.First) args["generate"] = step.Args["generate"];
            else
            {
                JObject materials = new JObject();
                Level level = McpEditor.UI(() => McpEditor.RequireLevel().Level);
                foreach (int mesh in meshes)
                {
                    if (!plain.TryGetValue(mesh, out Materials.Material material) || material == null) continue;
                    string name = p.Target.Role == "first_person" && firstPerson.TryGetValue(material, out string fp) ? fp : McpEditor.UI(() => McpAssets.MaterialRef(level, material));
                    materials["WPNMESH_" + mesh] = name;
                }
                if (materials.Count != 0) args["materials"] = materials;
            }
            List<ModelIO.PlannedSubmesh> planned = null;
            McpTool tool = McpTools.Find("import_model") ?? throw new McpError(McpErrorCodes.Failed, "The import_model tool is missing from this build.");
            McpCall nested = new McpCall(tool, args, call.Cancel, (text, d, total) => call.Progress(text, d, total));
            object result = McpImportTools.ImportModel(nested, (scene, importPlan) =>
            {
                planned = importPlan.AllSubmeshes().ToList();
                float s = importPlan.Scale, u = importPlan.UnitScale;
                Matrix4x4 k = Matrix4x4.CreateScale(s / u) * move * Matrix4x4.CreateScale(u / s);
                foreach (ModelIO.PlannedSubmesh submesh in planned)
                {
                    submesh.MeshName = "WPNMESH_" + submesh.MeshIndex;
                    submesh.Include = meshes.Contains(submesh.MeshIndex);
                    Mesh mesh = scene.Meshes[submesh.MeshIndex];
                    Matrix4x4 baseTransform = ModelIO.VertexTransform(mesh, submesh.Transform, submesh.Metadata, u);
                    if (mesh.HasBones) mesh.Bones.Clear();
                    if (submesh.Metadata != null) submesh.Metadata.Placement = null;
                    submesh.Transform = baseTransform * k;
                }
            });
            foreach (string note in nested.Notes) call.Note(note);
            JObject imported = result as JObject ?? JObject.FromObject(result);
            string model = (string)imported["model"];
            if (p != null) p.ImportedModel = model;
            if (step.First && planned != null)
                foreach (ModelIO.PlannedSubmesh submesh in planned.Where(o => o.Include))
                    if (!plain.ContainsKey(submesh.MeshIndex)) plain[submesh.MeshIndex] = submesh.Material;
            //Meshes the first import left out (a part's own) still need their plain material: the same file material made one
            if (step.First)
                foreach (WeaponSourceMesh mesh in plan.Source.Usable)
                    if (!plain.ContainsKey(mesh.Index))
                    {
                        WeaponSourceMesh sibling = plan.Source.Usable.FirstOrDefault(o => o.Material == mesh.Material && plain.ContainsKey(o.Index));
                        if (sibling != null) plain[mesh.Index] = plain[sibling.Index];
                    }
            return imported;
        }

        private static JArray EnsureFlatTextures(McpCall call)
        {
            JArray made = new JArray();
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OpenCAGE_Weapons");
            Directory.CreateDirectory(folder);
            foreach (Tuple<string, Color, bool> flat in FlatTextures)
            {
                bool exists = McpEditor.UI(() => { try { return McpAssets.FindTexture(McpEditor.RequireLevel().Level, flat.Item1, false, out bool _) != null; } catch (McpError) { return false; } });
                if (exists) continue;
                string file = System.IO.Path.Combine(folder, System.IO.Path.GetFileName(flat.Item1) + ".png");
                using (Bitmap bitmap = new Bitmap(4, 4))
                {
                    for (int x = 0; x < 4; x++) for (int y = 0; y < 4; y++) bitmap.SetPixel(x, y, flat.Item2);
                    bitmap.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                }
                JObject args = new JObject() { ["path"] = file, ["name"] = flat.Item1, ["srgb"] = flat.Item3 };
                if (flat.Item1.EndsWith("NORMAL")) args["format"] = "DXN";
                McpEditorTools.Nested(call, "import_texture", args);
                made.Add(flat.Item1);
            }
            return made;
        }

        /// <summary>
        /// The first-person copy of each plain material the first-person parts draw with (NO_CLIP, see <see cref="NoClipMask"/>). If the level
        /// can't reach that permutation, a copy of the weapon's own first-person material (NO_CLIP already) with the new diffuse and normal maps.
        /// </summary>
        private static JArray MakeFirstPersonMaterials(McpCall call, WeaponPlan plan, Dictionary<int, Materials.Material> plain, Dictionary<Materials.Material, string> firstPerson)
        {
            JArray rows = new JArray();
            List<Materials.Material> wanted = plan.Parts.Where(o => o.Target.Role == "first_person" && o.Action == "replace").SelectMany(o => o.Meshes)
                .Select(o => plain.TryGetValue(o.Index, out Materials.Material m) ? m : null).Where(o => o != null).Distinct().ToList();
            foreach (Materials.Material material in wanted)
            {
                Level level = McpEditor.UI(() => McpEditor.RequireLevel().Level);
                string source = McpEditor.UI(() => McpAssets.MaterialRef(level, material));
                string name = material.Name + "_FP";
                JObject described = McpEditor.UI(() => McpMaterialTools.DescribeMaterial(level, material, true));
                string diffuse = Sampler(described, "DIFFUSE_MAP"), normal = Sampler(described, "NORMAL_MAP");
                bool exists = McpEditor.UI(() => level.Materials.Entries.Any(o => o != null && o.Name == name));
                if (!exists) McpEditorTools.Nested(call, "create_material", new JObject() { ["name"] = name, ["copy_from"] = source });
                JObject textures = new JObject()
                {
                    ["SPECULAR_MAP"] = "WEAPONS\\WPN_FLAT_SPEC",
                    ["ENVIRONMENT_MAP"] = EnvironmentMap,
                    ["AMBIENT_OCCLUSION_MAP"] = "WEAPONS\\WPN_WHITE",
                    ["NORMAL_MAP"] = normal ?? "WEAPONS\\WPN_FLAT_NORMAL",
                };
                if (diffuse != null) textures["DIFFUSE_MAP"] = diffuse;
                JObject row = new JObject() { ["material"] = name, ["from"] = source };
                try
                {
                    McpEditorTools.Nested(call, "edit_material", new JObject() { ["material"] = name, ["mask"] = NoClipMask, ["textures"] = textures, ["priority"] = FirstPersonPriority });
                    row["mask"] = NoClipMask;
                }
                catch (McpError e)
                {
                    //The level can't reach the permutation: start again from the weapon's own first-person material, which is NO_CLIP already
                    Materials.Material retail = plan.Info.FirstPerson.Main?.Lod0.Select(o => o.Material).FirstOrDefault(o => o != null);
                    if (retail == null) throw;
                    string retailRef = McpEditor.UI(() => McpAssets.MaterialRef(level, retail));
                    string fallback = name + "_R";
                    bool fallbackExists = McpEditor.UI(() => level.Materials.Entries.Any(o => o != null && o.Name == fallback));
                    if (!fallbackExists) McpEditorTools.Nested(call, "create_material", new JObject() { ["name"] = fallback, ["copy_from"] = retailRef });
                    JObject retailDescribed = McpEditor.UI(() => McpMaterialTools.DescribeMaterial(level, level.Materials.Entries.First(o => o != null && o.Name == fallback), true));
                    JObject fallbackTextures = new JObject();
                    foreach (JObject sampler in (retailDescribed["samplers"] as JArray ?? new JArray()).OfType<JObject>())
                    {
                        string slot = (string)sampler["name"];
                        if (slot == "DIFFUSE_MAP" && diffuse != null) fallbackTextures[slot] = diffuse;
                        else if (slot == "NORMAL_MAP") fallbackTextures[slot] = normal ?? "WEAPONS\\WPN_FLAT_NORMAL";
                        else if (slot == "SPECULAR_MAP") fallbackTextures[slot] = "WEAPONS\\WPN_FLAT_SPEC";
                        else if (slot == "AMBIENT_OCCLUSION_MAP") fallbackTextures[slot] = "WEAPONS\\WPN_WHITE";
                    }
                    McpEditorTools.Nested(call, "edit_material", new JObject() { ["material"] = fallback, ["textures"] = fallbackTextures, ["priority"] = FirstPersonPriority });
                    row["note"] = "the NO_CLIP permutation " + NoClipMask + " is out of this level's reach (" + e.Message + "); made from the weapon's own first-person material instead";
                    row["material"] = name = fallback;
                }
                foreach (KeyValuePair<string, JToken> parameter in new JObject() { ["DIFFUSE_TINT"] = new JArray(1, 1, 1, 1), ["DIFFUSE_UV_MULT"] = 1, ["NORMAL_UV_MULT"] = 1, ["SPECULAR_UV_MULT"] = 1, ["SPECULAR_TINT"] = 0.25, ["SPECULAR_POWER"] = 0.5, ["ENVIRONMENT_MAP_MULT"] = 0.2, ["FRESNEL_INTENSITY"] = 1 })
                {
                    try { McpEditorTools.Nested(call, "edit_material", new JObject() { ["material"] = name, ["parameters"] = new JObject() { [parameter.Key] = parameter.Value } }); }
                    catch (McpError) { }
                }
                firstPerson[material] = name;
                rows.Add(row);
            }
            return rows;
        }

        private static string Sampler(JObject described, string slot)
        {
            foreach (JObject sampler in (described["samplers"] as JArray ?? new JArray()).OfType<JObject>())
                if ((string)sampler["name"] == slot && !string.IsNullOrEmpty((string)sampler["texture"])) return (string)sampler["texture"];
            return null;
        }

        /// <summary>A hull for a prop keeps the body values of the system it replaces (mass, friction, bounce), when they can be read.</summary>
        private static void CopyBodyValues(McpCall call, WeaponPlan plan, Step step)
        {
            try
            {
                //The system the prop's PhysicsSystem entity is bound to now (its system_index), with its bodies
                WeaponComposite target = step.Part?.Target;
                int index = McpEditor.UI(() => target?.PhysicsSystem?.GetParameter(ShortGuidUtils.Generate("system_index"))?.content is cInteger i ? i.value : -1);
                if (index < 0) return;
                JObject systems = McpEditorTools.Nested(call, "list_physics_systems", new JObject() { ["system"] = index, ["bodies"] = true });
                foreach (string key in new[] { "mass", "friction", "restitution" })
                {
                    JToken value = systems.SelectTokens("$.." + key).FirstOrDefault(o => o.Type == JTokenType.Float || o.Type == JTokenType.Integer);
                    if (value != null) step.Args[key] = value;
                }
            }
            catch (McpError) { }
        }
        /// <summary>After the run: each replaced part still has its resource id (so its bone still carries it) and draws the new model; first-person materials are NO_CLIP at 31.</summary>
        private static JObject Check(McpCall call, WeaponPlan plan)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                Commands commands = level.Commands;
                JArray parts = new JArray();
                bool allKept = true;
                foreach (WeaponPartPlan p in plan.Parts.Where(o => o.Action == "replace"))
                {
                    Composite composite = McpScript.FindComposite(commands, p.CompositePath);
                    FunctionEntity entity = composite.GetEntityByID(p.Part.Entity.shortGUID) as FunctionEntity;
                    ResourceReference renderable = entity == null ? null : Renderables(entity).FirstOrDefault();
                    bool kept = renderable != null && renderable.resource_id == p.Part.ResourceId;
                    allKept &= kept;
                    Submesh first = renderable?.RenderableInstance.FirstOrDefault(o => o?.Model != null)?.Model;
                    JObject row = new JObject()
                    {
                        ["composite"] = p.CompositePath,
                        ["entity"] = p.Part.Name,
                        ["resource_id_kept"] = kept,
                        ["draws"] = first == null ? null : ModelOf(level, first)?.Name,
                    };
                    Materials.Material material = renderable?.RenderableInstance.FirstOrDefault(o => o?.Material != null)?.Material;
                    if (material != null)
                    {
                        JObject described = McpMaterialTools.DescribeMaterial(level, material, false);
                        row["material"] = described["name"];
                        row["priority"] = described["priority"];
                        row["mask"] = described["mask"];
                    }
                    parts.Add(row);
                }
                return new JObject() { ["resource_ids_kept"] = allKept, ["parts"] = parts };
            });
        }
        #endregion

        #region The reference to model against
        /// <summary>A name as a node / mesh name: letters, digits and underscores (a part's own name, so a modeller sees RING_FRONT, not RINGFRONT).</summary>
        private static string Safe(string name) => new string((name ?? "").Select(c => char.IsLetterOrDigit(c) || c == '_' ? char.ToUpperInvariant(c) : '_').ToArray());

        /// <summary>
        /// Write the retail weapon in weapon space - one mesh per first-person part, REF_<part> (and REF_TP_<part> for third person), plus an empty
        /// node per marker - for lining a new model up against in a modelling program. Meshes named REF_* are left out when the wizard reads a file,
        /// so the reference can stay in it. glTF / GLB / FBX / OBJ / DAE by the extension (UI thread).
        /// </summary>
        public static JObject ExportReference(Level level, WeaponInfo info, string path, bool thirdPerson, bool markers)
        {
            ModelExport.ModelExporter.Format format = ModelExport.ModelExporter.For(path);
            Scene scene = new Scene();
            scene.RootNode = new Node("REF_" + Safe(info.Key));
            scene.Materials.Add(new Assimp.Material() { Name = "REF_FIRST_PERSON", ColorDiffuse = new Color4D(0.6f, 0.6f, 0.6f, 1) });
            scene.Materials.Add(new Assimp.Material() { Name = "REF_THIRD_PERSON", ColorDiffuse = new Color4D(0.3f, 0.45f, 0.8f, 1) });
            int meshes = 0;
            foreach (WeaponComposite weapon in new[] { info.FirstPerson, thirdPerson ? info.ThirdPerson : null }.Where(o => o != null))
                foreach (WeaponPart part in weapon.Parts.Where(o => o.Lod0.Count != 0))
                {
                    Matrix4x4 toWeapon = part.Rest * weapon.Fit.TargetToWeapon;
                    cMesh mesh = new cMesh();
                    foreach (Submesh submesh in part.Lod0)
                    {
                        List<Vector3> corners = Corners(submesh);
                        for (int i = 0; i + 2 < corners.Count && mesh.Vertices.Count < 65000; i += 3)
                        {
                            Vector3 a = Vector3.Transform(corners[i], toWeapon), b = Vector3.Transform(corners[i + 1], toWeapon), c = Vector3.Transform(corners[i + 2], toWeapon);
                            Vector3 n = Vector3.Cross(b - a, c - a);
                            n = n.LengthSquared() < 1e-20f ? Vector3.UnitY : Vector3.Normalize(n);
                            foreach (Vector3 v in new[] { a, b, c })
                            {
                                mesh.Indices.Add((ushort)mesh.Vertices.Count);
                                mesh.Vertices.Add(v);
                                mesh.Normals.Add(n);
                            }
                        }
                    }
                    if (mesh.Vertices.Count < 3) continue;
                    string name = "REF_" + (weapon == info.FirstPerson ? "" : "TP_") + Safe(part.Name);
                    Mesh converted = ModelIO.ToAssimpMesh(mesh, name, weapon == info.FirstPerson ? 0 : 1, format.FlipUVs, out int[] _, format.UnitScale);
                    scene.Meshes.Add(converted);
                    Node node = new Node(name, scene.RootNode);
                    node.MeshIndices.Add(scene.MeshCount - 1);
                    scene.RootNode.Children.Add(node);
                    meshes++;
                }
            JArray markerRows = new JArray();
            if (markers)
                foreach (FunctionEntity entity in info.FirstPerson.Positioned.Where(o => IsType(o, FunctionType.PositionMarker)))
                {
                    cTransform t = (cTransform)entity.GetParameter(ShortGuids.position).content;
                    string name = "REF_MARKER_" + Safe(McpScript.EntityName(level.Commands, info.FirstPerson.Composite, entity));
                    Node node = new Node(name, scene.RootNode) { Transform = Assimp.Matrix4x4.FromTranslation(new Vector3D(t.position.X * format.UnitScale, t.position.Y * format.UnitScale, -t.position.Z * format.UnitScale)) };
                    scene.RootNode.Children.Add(node);
                    markerRows.Add(name);
                }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
            ModelExport.ModelExporter.Write(scene, path);
            return new JObject()
            {
                ["path"] = path,
                ["format"] = format.Description,
                ["meshes"] = meshes,
                ["markers"] = markerRows,
                ["space"] = "weapon space (" + info.FirstPerson.Path + "'s composite space), in the format's own unit; meshes named REF_* are ignored by replace_weapon_model, so the reference can stay in the file",
            };
        }

        #endregion
    }
}
