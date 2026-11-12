using CATHODE;
using CathodeLib;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.ShaderTypes;
using Newtonsoft.Json.Linq;
using OpenCAGE.Audio;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenCAGE
{
    /// <summary>
    /// A planar reflection (SimpleWater, PLANAR_REFLECTIVE materials) is drawn again from a mirrored view with its own lighting pass,
    /// which only the lights flagged include_in_planar_reflections take part in: geometry drawn into it with no flagged light renders as
    /// black shapes.
    /// </summary>
    internal sealed class PlanarReflectionLightsRule : LevelCheckRule
    {
        private const string Flag = "include_in_planar_reflections";
        public override string Id => "reflection_lights";
        public override string Name => "Black planar reflections";
        public override string About => "A level with a planar reflection (SimpleWater, PLANAR_REFLECTIVE materials) and geometry drawn into it, but no light flagged include_in_planar_reflections: the reflection draws black shapes.";
        public override IEnumerable<FunctionType> Walks => new[] { FunctionType.LightReference, FunctionType.SimpleWater };

        private static bool FlaggedOnTheWay(McpPlacements.Placement placement) => placement.Chain.Any(o => LevelCheckContext.Bool(o, Flag) == true);

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            List<McpPlacements.Placement> water = context.RealPlacements(FunctionType.SimpleWater).ToList();
            //PLANAR_REFLECTIVE is CA_ENVIRONMENT feature 11; geometry drawn into the reflection carries INCLUDE_IN_REFLECTIVE (as last built)
            int reflectiveMovers = 0, drawnIntoIt = 0, lightMoversFlagged = 0;
            foreach (Movers.MOVER_DESCRIPTOR mover in context.Level.Movers?.Entries ?? new List<Movers.MOVER_DESCRIPTOR>())
            {
                bool flagged = (mover.CullFlags & Movers.CullFlag.INCLUDE_IN_REFLECTIVE) != 0;
                bool light = mover.GetRenderableType() == RenderableInstanceType.LIGHT;
                if (light) { if (flagged) lightMoversFlagged++; continue; }
                if (flagged) drawnIntoIt++;
                if (mover.RenderableElements.Any(o => o?.Material?.Shader != null && o.Material.Shader.Ubershader == SHADER_LIST.CA_ENVIRONMENT && (o.Material.Shader.UbershaderFeatureFlags & (1L << 11)) != 0))
                    reflectiveMovers++;
            }
            List<McpPlacements.Placement> lights = context.RealPlacements(FunctionType.LightReference).ToList();
            int lightsFlagged = lights.Count(FlaggedOnTheWay);
            context.Report.Figures["planar_reflections"] = new JObject()
            {
                ["simple_water"] = water.Count,
                ["planar_reflective_models"] = reflectiveMovers,
                ["models_drawn_in_reflections"] = drawnIntoIt,
                ["lights"] = lights.Count,
                ["lights_flagged"] = lightsFlagged,
            };
            if (water.Count == 0 && reflectiveMovers == 0) return;
            if (drawnIntoIt == 0 || lightsFlagged != 0 || lightMoversFlagged != 0 || lights.Count == 0) return;

            //The lights to flag: each light entity once (one in a composite placed many times is one edit)
            List<(Composite composite, Entity entity)> toFlag = lights.Select(o => (context.HolderOf(o), o.Chain[o.Chain.Count - 1])).Where(o => o.Item1 != null).Distinct().ToList();
            LevelCheckFinding finding = new LevelCheckFinding()
            {
                Severity = LevelCheckSeverity.Broken,
                Title = "The planar reflection has no lights: what it shows will be black",
                Text = "This level has a planar reflection (" + (water.Count != 0 ? water.Count + " SimpleWater" : "") + (water.Count != 0 && reflectiveMovers != 0 ? ", " : "") +
                       (reflectiveMovers != 0 ? reflectiveMovers + " PLANAR_REFLECTIVE model" + (reflectiveMovers == 1 ? "" : "s") : "") + ") and " + drawnIntoIt + " model" + (drawnIntoIt == 1 ? " is" : "s are") +
                       " drawn into it, but none of its " + lights.Count + " lights is flagged include_in_planar_reflections. The reflection is lit by its own pass, with only the flagged " +
                       "lights, so everything in it draws as a black silhouette. Flag the lights that light what it reflects (retail flags about half of a level's lights).",
            };
            foreach (McpPlacements.Placement placement in water.Take(3))
                finding.Sources.Add(context.Source(placement, "SimpleWater"));
            finding.Fix = new LevelCheckFix()
            {
                Label = "Flag all " + toFlag.Count + " lights for the reflection",
                Detail = "Sets include_in_planar_reflections on every light the level places (each light entity once). Every flagged light is also drawn in the reflection pass, " +
                         "so on a big level untick those far from the reflection afterwards.",
                Apply = fromTool => LevelCheckFix.Script("flag lights for planar reflections", toFlag[0].composite, fromTool, edit =>
                {
                    int set = 0;
                    foreach ((Composite composite, Entity entity) in toFlag)
                    {
                        if (!edit.Commands.Entries.Contains(composite) || composite.GetEntityByID(entity.shortGUID) != entity) continue;
                        edit.SetParameter(composite, entity, Flag, new JValue(true));
                        set++;
                    }
                    return "Flagged " + set + " light" + (set == 1 ? "" : "s") + " include_in_planar_reflections.";
                }),
            };
            found.Add(finding);
        }
    }

    /// <summary>
    /// The game's Havok world is 1024 m across, +-512 m round the origin (create_havok_world_from_snapshot): a trigger, a physics
    /// body or a character outside it has no collision, so it never fires, falls or stands.
    /// </summary>
    internal sealed class HavokWorldBoundsRule : LevelCheckRule
    {
        public const float Half = 512.0f;
        private static readonly FunctionType[] Types =
        {
            FunctionType.PlayerTriggerBox, FunctionType.PlayerUseTriggerBox, FunctionType.TriggerVolumeFilter, FunctionType.TriggerVolumeFilter_Monitored,
            FunctionType.ProximityTrigger, FunctionType.PhysicsSystem, FunctionType.Character,
        };
        public override string Id => "havok_world_bounds";
        public override string Name => "Outside the physics world";
        public override string About => "Triggers, physics bodies and characters more than 512 m from the origin on any axis are outside the game's Havok world: no collision, no trigger.";
        public override IEnumerable<FunctionType> Walks => Types;

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            List<(McpPlacements.Placement placement, FunctionType type, Vector3 at)> outside = new List<(McpPlacements.Placement, FunctionType, Vector3)>();
            foreach (FunctionType type in Types)
                foreach (McpPlacements.Placement placement in context.RealPlacements(type))
                {
                    if (placement.World == null || placement.PositionDefaulted) continue;
                    Vector3 at = placement.World.position;
                    if (Math.Abs(at.X) > Half || Math.Abs(at.Y) > Half || Math.Abs(at.Z) > Half)
                        outside.Add((placement, type, at));
                }
            if (outside.Count == 0) return;
            LevelCheckFinding finding = new LevelCheckFinding()
            {
                Severity = LevelCheckSeverity.Broken,
                Title = outside.Count + " trigger" + (outside.Count == 1 ? "" : "s") + ", physics bod" + (outside.Count == 1 ? "y or character is" : "ies or characters are") + " outside the physics world",
                Text = "The game builds its Havok world 1024 m across, from -512 to +512 m on each axis round the level's origin. Anything outside it has no collision: a trigger " +
                       "never fires, a physics body or a character falls or never stands. Move the level's playable space nearer the origin (move the root's contents), or these into it.",
            };
            foreach ((McpPlacements.Placement placement, FunctionType type, Vector3 at) in outside.Take(20))
                finding.Sources.Add(context.Source(placement, type + " at " + at.X.ToString("0") + ", " + at.Y.ToString("0") + ", " + at.Z.ToString("0")));
            found.Add(finding);
        }
    }

    /// <summary>
    /// NPCs with no SoundEnvironmentMarker in the level: the sound bake builds no sound network, so voices carry through walls at full
    /// volume from anywhere.
    /// </summary>
    internal sealed class SoundEnvironmentMarkerRule : LevelCheckRule
    {
        public override string Id => "sound_network";
        public override string Name => "NPC voices through walls";
        public override string About => "A level with NPCs but no SoundEnvironmentMarker: the sound bake builds no sound network, so NPC voices carry through walls from anywhere.";
        public override IEnumerable<FunctionType> Walks => new[] { FunctionType.Character, FunctionType.SoundEnvironmentMarker };

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            List<McpPlacements.Placement> npcs = context.RealPlacements(FunctionType.Character).Where(o => LevelCheckContext.Bool(o.Chain[o.Chain.Count - 1], "is_player") != true).ToList();
            int markers = context.RealPlacements(FunctionType.SoundEnvironmentMarker).Count();
            context.Report.Figures["sound_environment_markers"] = markers;
            if (npcs.Count == 0 || markers != 0) return;
            LevelCheckFinding finding = new LevelCheckFinding()
            {
                Severity = LevelCheckSeverity.Broken,
                Title = "NPCs but no SoundEnvironmentMarker: their voices carry through walls",
                Text = "The level places " + npcs.Count + " NPC" + (npcs.Count == 1 ? "" : "s") + " and no SoundEnvironmentMarker. The sound bake builds the sound network (how sound travels " +
                       "room to room) from those markers, so without any every voice and footstep is heard at full strength from anywhere. Place a SoundEnvironmentMarker in each room " +
                       "and corridor the NPCs use (retail: one per space, at head height), then Save & Build with the sound baker on.",
            };
            foreach (McpPlacements.Placement placement in npcs.Take(5))
                finding.Sources.Add(context.Source(placement));
            found.Add(finding);
        }
    }

    /// <summary>
    /// The banks a level loads itself (SoundLoadBank) share a pool of about 20.5 MB: past it the next bank silently refuses to load.
    /// Sizes are the banks' own: the level's package copy (LEVEL_SOUND_OVERRIDE.PCK) where it has one, else DATA\SOUND.
    /// </summary>
    internal sealed class SoundBankPoolRule : LevelCheckRule
    {
        public const long PoolBytes = (long)(20.5 * 1024 * 1024);
        public override string Id => "sound_bank_pool";
        public override string Name => "Sound bank memory";
        public override string About => "The banks a level's SoundLoadBanks load share about 20.5 MB; past it the next bank silently doesn't load (its sounds are silent).";

        /// <summary>Wwise's bank id: FNV-1 (32 bit) of the lower-case name.</summary>
        public static uint BankId(string name)
        {
            uint hash = 2166136261;
            foreach (byte b in System.Text.Encoding.ASCII.GetBytes((name ?? "").ToLowerInvariant()))
            {
                hash *= 16777619;
                hash ^= b;
            }
            return hash;
        }

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            string world = context.Level.Filepath + (context.Level.Patched ? "_PATCH" : "") + "/WORLD";
            //DATA\ENV\PRODUCTION\<level> -> DATA\SOUND
            string data = context.Level.Filepath.Replace('\\', '/');
            int envAt = data.ToUpperInvariant().IndexOf("/ENV/");
            string sound = envAt < 0 ? null : data.Substring(0, envAt) + "/SOUND";

            Dictionary<uint, long> package = new Dictionary<uint, long>();
            try
            {
                string pck = Directory.Exists(world) ? Directory.EnumerateFiles(world, "*.PCK").FirstOrDefault(o => string.Equals(Path.GetFileName(o), "LEVEL_SOUND_OVERRIDE.PCK", StringComparison.OrdinalIgnoreCase)) : null;
                if (pck != null)
                    foreach (WwiseFilePackage.Entry entry in WwiseFilePackage.Load(pck).Banks)
                        package[entry.Id] = entry.Length;
            }
            catch (Exception e) { Debug.Log("LevelCheck", "Could not read the level's sound package: " + e.Message); }

            Dictionary<string, (long bytes, bool onReset, bool packaged, McpSoundTools.BankLoader loader)> banks = new Dictionary<string, (long, bool, bool, McpSoundTools.BankLoader)>(StringComparer.OrdinalIgnoreCase);
            foreach (McpSoundTools.BankLoader loader in McpSoundTools.BankLoaders(context.Commands))
            {
                if (loader.TypedOnly || !context.Placed.Contains(loader.Composite) || string.IsNullOrEmpty(loader.Bank) || loader.Bank.StartsWith("(")) continue;
                long bytes = -1;
                bool packaged = package.TryGetValue(BankId(loader.Bank), out long inPackage);
                if (packaged) bytes = inPackage;
                else if (sound != null)
                {
                    string file = Path.Combine(sound, loader.Bank + ".BNK");
                    if (File.Exists(file)) bytes = new FileInfo(file).Length;
                }
                if (bytes < 0) continue;
                if (banks.TryGetValue(loader.Bank, out var had))
                    banks[loader.Bank] = (bytes, had.onReset || loader.OnReset, packaged, had.loader);
                else
                    banks[loader.Bank] = (bytes, loader.OnReset, packaged, loader);
            }
            long atStart = banks.Values.Where(o => o.onReset).Sum(o => o.bytes);
            long all = banks.Values.Sum(o => o.bytes);
            context.Report.Figures["sound_banks"] = new JObject()
            {
                ["loaded_at_start_mb"] = Math.Round(atStart / 1048576.0, 2),
                ["all_loaders_mb"] = Math.Round(all / 1048576.0, 2),
                ["pool_mb"] = 20.5,
                ["banks"] = new JObject(banks.OrderByDescending(o => o.Value.bytes).Select(o => new JProperty(o.Key, Math.Round(o.Value.bytes / 1048576.0, 2)))),
            };
            /* Loaded as the level starts and over the pool: certain. All the level's loaders together over it: can happen, unless the script
               unloads some first (retail's most, ENG_REACTORCORE, is 18 MB). The characters' own banks (voices, weapons) come on top of
               both, so the figure is a floor */
            if (all <= PoolBytes) return;
            bool certain = atStart > PoolBytes;
            LevelCheckFinding finding = new LevelCheckFinding()
            {
                Key = certain ? "at_start" : "can_reach",
                Severity = certain ? LevelCheckSeverity.Broken : LevelCheckSeverity.Warning,
                Title = certain ? "The level loads " + (atStart / 1048576.0).ToString("0.0") + " MB of sound banks as it starts (the pool holds about 20.5 MB)"
                                : "The level's SoundLoadBanks load up to " + (all / 1048576.0).ToString("0.0") + " MB of sound banks (the pool holds about 20.5 MB)",
                Text = "The banks a level loads share a pool of about 20.5 MB; once it is full the next bank silently doesn't load and all of its sounds are silent (the characters' " +
                       "own voice and weapon banks need room in it too). " + (certain ? "" : "If the script unloads some before loading others (unload_bank), dismiss this. ") +
                       "Load fewer banks at once (trigger_via_pin, and unload_bank when done), or make the level package's copies smaller: audio replaced into a bank " +
                       "(Level Sound Override) grows it, and long music belongs in a stream rather than inside a bank.",
            };
            foreach (KeyValuePair<string, (long bytes, bool onReset, bool packaged, McpSoundTools.BankLoader loader)> bank in banks.Where(o => !certain || o.Value.onReset).OrderByDescending(o => o.Value.bytes).Take(12))
                finding.Sources.Add(context.Source(bank.Value.loader.Composite, bank.Value.loader.Entity, bank.Key + " " + (bank.Value.bytes / 1048576.0).ToString("0.00") + " MB" + (bank.Value.packaged ? ", the level package's copy" : "")));
            found.Add(finding);
        }
    }

    /// <summary>
    /// Pins the game cannot find: it finds a composite's pin by a binary search on the pin's name id over its VARIABLES block as stored.
    /// Retail stores that block sorted; older CathodeLib wrote it in creation order (CommandsPAK now sorts it). A pin the search misses
    /// breaks: an output or target pin never fires its parent's links, an input reads no value. A load keeps file order, so replaying
    /// the search finds them; a save with the current CathodeLib fixes all of them.
    /// </summary>
    internal sealed class PinOrderRule : LevelCheckRule
    {
        public override string Id => "pin_order";
        public override string Name => "Pins the game cannot find";
        public override string About => "Composite pins stored in an order the game cannot search (saved by an older OpenCAGE): links through them to and from the composite's instances don't work. Saving the level fixes it.";

        /// <summary>The pins of a composite the game's lower_bound search (unsigned, by name id, over the stored order) misses.</summary>
        public static List<VariableEntity> PinsTheGameMisses(Composite composite)
        {
            List<VariableEntity> stored = composite.variables.ToList();
            List<VariableEntity> missed = new List<VariableEntity>();
            foreach (VariableEntity pin in stored)
            {
                uint key = pin.name.AsUInt32;
                int lo = 0, n = stored.Count;
                while (n > 0)
                {
                    int half = n / 2;
                    if (stored[lo + half].name.AsUInt32 < key) { lo += half + 1; n -= half + 1; }
                    else n = half;
                }
                if (lo >= stored.Count || stored[lo].name.AsUInt32 != key) missed.Add(pin);
            }
            return missed;
        }

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            int composites = 0, pins = 0;
            LevelCheckFinding finding = new LevelCheckFinding()
            {
                Severity = LevelCheckSeverity.Broken,
                Text = "The game finds a composite's pin by a binary search over its pins as stored, and these are stored out of order (an older OpenCAGE saved them in the order they " +
                       "were made), so links through them to and from the composite's instances don't work: an output pin never fires, an input reads no value. Saving the level fixes " +
                       "every composite (pins made since the level was opened show here until it is saved).",
            };
            foreach (Composite composite in context.Commands.Entries)
            {
                if (composite == null || !context.Placed.Contains(composite)) continue;
                List<VariableEntity> missed = PinsTheGameMisses(composite);
                if (missed.Count == 0) continue;
                composites++;
                pins += missed.Count;
                if (finding.Sources.Count < 30)
                    finding.Sources.Add(new LevelCheckSource()
                    {
                        Composite = composite,
                        Text = composite.name + ": " + missed.Count + " pin" + (missed.Count == 1 ? "" : "s") + " (" + string.Join(", ", missed.Take(6).Select(o => ShortGuidUtils.FindString(o.name))) + (missed.Count > 6 ? "..." : "") + ")",
                    });
            }
            context.Report.Figures["pins_out_of_order"] = pins;
            if (pins == 0) return;
            finding.Title = pins + " composite pin" + (pins == 1 ? "" : "s") + " in " + composites + " composite" + (composites == 1 ? "" : "s") + " can't be found by the game until the level is saved";
            found.Add(finding);
        }
    }

    /// <summary>
    /// The player's start (and NPCs) off the level's navmesh, as last built. With no NavMeshReachabilitySeedPoint the navmesh bake keeps
    /// only the largest connected walkable area and floors within 0.75 m of its height (NavMeshBaker CullUnseededIslands, as retail's
    /// levels without seeds need): a room whose ceiling slab is bigger than its floor keeps only the ceiling top, and the game starts
    /// the player on the roof.
    /// </summary>
    internal sealed class NavMeshCoverageRule : LevelCheckRule
    {
        public const string SpawnPoint = @"Archetypes\Script\Mission\SpawnPositionSelect";
        public override string Id => "navmesh_reach";
        public override string Name => "Off the navmesh";
        public override string About => "The player's start or NPCs off the level's navmesh (as last built): the game moves the player onto the nearest navmesh (a roof, say) and NPCs there can't walk. Without a NavMeshReachabilitySeedPoint the bake keeps only the largest walkable area.";
        public override IEnumerable<FunctionType> Walks => new[] { FunctionType.Character, FunctionType.NavMeshReachabilitySeedPoint };
        public override IEnumerable<string> WalksInstancesOf => new[] { SpawnPoint };

        private struct Poly { public float MinX, MaxX, MinZ, MaxZ, Y; }

        /// <summary>Whether a point stands on the navmesh: over a polygon (0.3 m slack) from 2 m below the point to 0.75 m above it.</summary>
        private static bool On(List<Poly> polys, Vector3 at)
        {
            foreach (Poly poly in polys)
            {
                if (at.X < poly.MinX - 0.3f || at.X > poly.MaxX + 0.3f || at.Z < poly.MinZ - 0.3f || at.Z > poly.MaxZ + 0.3f) continue;
                float dy = at.Y - poly.Y;
                if (dy >= -0.75f && dy <= 2.0f) return true;
            }
            return false;
        }

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            NavigationMesh nav = context.Level.StateResources?.FirstOrDefault()?.NavMesh;
            int seeds = context.RealPlacements(FunctionType.NavMeshReachabilitySeedPoint).Count();
            List<McpPlacements.Placement> starts = context.InstancePlacements(SpawnPoint).Where(o => o.Real && o.World != null).ToList();
            List<CharacterCapRule.Slot> npcs = CharacterCapRule.Slots(context).Where(o => !o.Player && o.Placement.World != null && !o.Placement.PositionDefaulted).ToList();
            if (nav?.Polygons == null || nav.Vertices == null || nav.Polygons.Length == 0)
            {
                context.Report.Figures["navmesh"] = new JObject() { ["polygons"] = 0, ["seed_points"] = seeds };
                return;
            }
            List<Poly> polys = new List<Poly>(nav.Polygons.Length);
            foreach (NavigationMesh.dtPoly p in nav.Polygons)
            {
                if (p.verts == null || p.vertCount == 0) continue;
                Poly poly = new Poly() { MinX = float.MaxValue, MaxX = float.MinValue, MinZ = float.MaxValue, MaxZ = float.MinValue };
                float y = 0;
                for (int i = 0; i < p.vertCount && i < p.verts.Length; i++)
                {
                    if (p.verts[i] >= nav.Vertices.Length) continue;
                    Vector3 v = nav.Vertices[p.verts[i]];
                    poly.MinX = Math.Min(poly.MinX, v.X); poly.MaxX = Math.Max(poly.MaxX, v.X);
                    poly.MinZ = Math.Min(poly.MinZ, v.Z); poly.MaxZ = Math.Max(poly.MaxZ, v.Z);
                    y += v.Y;
                }
                poly.Y = y / p.vertCount;
                polys.Add(poly);
            }
            List<McpPlacements.Placement> startsOff = starts.Where(o => !On(polys, o.World.position)).ToList();
            List<CharacterCapRule.Slot> npcsOff = npcs.Where(o => !On(polys, o.Placement.World.position)).ToList();
            context.Report.Figures["navmesh"] = new JObject()
            {
                ["polygons"] = polys.Count,
                ["seed_points"] = seeds,
                ["player_starts"] = starts.Count,
                ["player_starts_off"] = startsOff.Count,
                ["npcs"] = npcs.Count,
                ["npcs_off"] = npcsOff.Count,
            };
            if (startsOff.Count == 0) return;
            LevelCheckFinding finding = new LevelCheckFinding()
            {
                Key = "player_start",
                Severity = LevelCheckSeverity.Broken,
                Title = startsOff.Count + " of the level's " + starts.Count + " player start" + (starts.Count == 1 ? " is" : "s are") + " off its navmesh (as last built)",
                Text = "The game puts the player onto the navmesh nearest the start, which can be a ceiling or a roof, and NPCs can't walk where there is none. " +
                       (seeds == 0
                           ? "The level has no NavMeshReachabilitySeedPoint, so the navmesh bake kept only its largest walkable area (and floors within 0.75 m of that height) - a ceiling slab bigger than the floor under it wins. Add a NavMeshReachabilitySeedPoint on each floor the player and the NPCs use, then Save & Build with the navmesh baker on."
                           : "Check that the floor under it is walkable (its collision, slope and headroom) and that a NavMeshReachabilitySeedPoint can reach it, then Save & Build with the navmesh baker on."),
            };
            foreach (McpPlacements.Placement start in startsOff.Take(5))
                finding.Sources.Add(context.Source(start, "at " + start.World.position.X.ToString("0.0") + ", " + start.World.position.Y.ToString("0.0") + ", " + start.World.position.Z.ToString("0.0")));
            if (npcsOff.Count != 0)
                finding.Text += " " + npcsOff.Count + " of its " + npcs.Count + " NPCs are off the navmesh too.";
            found.Add(finding);
        }
    }
}
