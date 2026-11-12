using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// The game's resident animation memory, and the Launch Game option that doubles it. A character spawns only once every RESIDENT clip
    /// section its set (and the set's parents) needs fits in the resident heap (ca_allocator 4, 22,532,096 bytes, shared between sets; see
    /// Actor::AnimationDataDb::acquire_resident_sections_for_set). A set that doesn't fit never loads and its characters silently never spawn.
    /// AI.exe's g_animHeapsCanExceedBudget doubles every ca_allocator budget (PatchManager.PatchAnimHeapBudget).
    /// </summary>
    public static class AnimationHeapBudget
    {
        /// <summary>The Launch Game option's setting (written to AI.exe when changed and at every launch).</summary>
        public const string SettingKey = "PatchAnimationHeapBudget";
        /// <summary>The option's launch_options name (and a mod's launch option name).</summary>
        public const string LaunchOptionName = "larger_animation_memory";
        public const string Label = "Allow larger animation memory";

        /// <summary>ca_allocator 4, the resident animation heap, as AI.exe's ca_allocator_initialisers table gives it (all three PC builds).</summary>
        public const long ResidentHeapBytes = 22532096;

        public static long Budget(bool larger) => larger ? ResidentHeapBytes * 2 : ResidentHeapBytes;

        /// <summary>The option as set (Launch Game writes AI.exe to match at every launch).</summary>
        public static bool Enabled => SettingsManager.GetBool(SettingKey);

        public static bool Supported(PatchManager.Platform platform) =>
            platform == PatchManager.Platform.STEAM || platform == PatchManager.Platform.GOG || platform == PatchManager.Platform.EPIC_GAMES_STORE;

        /// <summary>Set the option and write AI.exe to match. False when AI.exe could not be written (the game holding it open, say).</summary>
        public static bool Set(bool on)
        {
            SettingsManager.SetBool(SettingKey, on);
            return PatchManager.PatchAnimHeapBudget(Singleton.Platform, Singleton.PathToAI, on);
        }

        public static string Megabytes(long bytes) => (bytes / 1048576.0).ToString("0.0") + " MB";

        #region SIZES
        /// <summary>One animation set's resident clip sections (its own and its parents').</summary>
        public sealed class SetSize
        {
            public string Set;
            public bool Known;
            public int Sections;
            public long Bytes;
        }

        /// <summary>What a group of animation sets holds in the resident heap together: sections shared between them count once.</summary>
        public sealed class Estimate
        {
            public List<SetSize> Sets = new List<SetSize>();
            public long Bytes;
            public int Sections;
            public bool Fits => Bytes <= Budget(false);
            public bool FitsLarger => Bytes <= Budget(true);
            public IEnumerable<string> Unknown => Sets.Where(o => !o.Known).Select(o => o.Set);

            public JObject ToJson()
            {
                return new JObject()
                {
                    ["resident_bytes"] = Bytes,
                    ["resident_mb"] = Math.Round(Bytes / 1048576.0, 2),
                    ["sections"] = Sections,
                    ["heap_bytes"] = Budget(false),
                    ["heap_bytes_larger"] = Budget(true),
                    ["fits"] = Fits,
                    ["fits_larger"] = FitsLarger,
                    ["option_on"] = Enabled,
                    ["sets"] = new JArray(Sets.Select(o => new JObject() { ["set"] = o.Set, ["known"] = o.Known, ["sections"] = o.Sections, ["bytes"] = o.Bytes })),
                };
            }
        }

        private static readonly object _lock = new object();
        private static Animation _sizedFor;
        private static Dictionary<uint, long> _sizes;

        /* Each resident section's size by its hash: the 32-bit files the PC game loads (RESIDENT, not RESIDENT64) */
        private static Dictionary<uint, long> SectionSizes(Animation animations)
        {
            lock (_lock)
            {
                if (ReferenceEquals(_sizedFor, animations) && _sizes != null)
                    return _sizes;
                Dictionary<uint, long> sizes = new Dictionary<uint, long>();
                const string folder = @"DATA\ANIM_SYS\RESIDENT\";
                foreach (PAK2.File entry in animations?.PAK?.Entries ?? new List<PAK2.File>())
                {
                    string name = (entry?.Filename ?? "").Replace('/', '\\');
                    if (!name.StartsWith(folder, StringComparison.OrdinalIgnoreCase) || name.IndexOf('\\', folder.Length) >= 0) continue;
                    string stem = Path.GetFileNameWithoutExtension(name);
                    if (stem.StartsWith("ANIM_CLIP_DB_SEC_", StringComparison.OrdinalIgnoreCase) && uint.TryParse(stem.Substring(17), out uint hash))
                        sizes[hash] = entry.Content?.Length ?? 0;
                }
                _sizedFor = animations;
                _sizes = sizes;
                return sizes;
            }
        }

        /// <summary>Forget the sizes read (ANIMATION.PAK changed in memory: a set added, clips imported).</summary>
        public static void Invalidate()
        {
            lock (_lock) { _sizedFor = null; _sizes = null; }
        }

        /// <summary>The resident sections a set needs: its own dependencies and its parents' (MALE's parent is HUMAN). Null when the set isn't there.</summary>
        public static HashSet<uint> ResidentSections(Animation animations, string set)
        {
            if (animations?.ClipIndex == null || string.IsNullOrWhiteSpace(set)) return null;
            HashSet<uint> sections = null;
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string current = set.Trim();
            while (!string.IsNullOrEmpty(current) && current != "0" && visited.Add(current))
            {
                var entry = animations.ClipIndex.DependencyMap.FirstOrDefault(o => string.Equals(o.Name, current, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    if (sections == null) sections = new HashSet<uint>();
                    sections.UnionWith(animations.ClipIndex.GetDependencies(entry));
                }
                else if (sections == null)
                    return null;
                current = animations.ClipIndex.ParentDependencies.FirstOrDefault(o => string.Equals(o.Name, current, StringComparison.OrdinalIgnoreCase))?.Parent;
            }
            return sections;
        }

        /// <summary>The resident heap the given sets need together.</summary>
        public static Estimate ForSets(Animation animations, IEnumerable<string> sets)
        {
            Estimate estimate = new Estimate();
            Dictionary<uint, long> sizes = SectionSizes(animations);
            HashSet<uint> union = new HashSet<uint>();
            foreach (string set in (sets ?? Enumerable.Empty<string>()).Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                HashSet<uint> sections = ResidentSections(animations, set);
                SetSize size = new SetSize() { Set = set, Known = sections != null };
                if (sections != null)
                {
                    size.Sections = sections.Count;
                    size.Bytes = sections.Sum(o => sizes.TryGetValue(o, out long n) ? n : 0);
                    union.UnionWith(sections);
                }
                estimate.Sets.Add(size);
            }
            estimate.Sections = union.Count;
            estimate.Bytes = union.Sum(o => sizes.TryGetValue(o, out long n) ? n : 0);
            return estimate;
        }

        /// <summary>
        /// Whether a set fits beside others (the level's other characters'): a sentence when it would not without the larger memory
        /// (or not even with it), else null. For animation tools adding resident clips to a set, or making a new set.
        /// </summary>
        public static string Warning(Animation animations, string set, IEnumerable<string> alongside)
        {
            Estimate estimate = ForSets(animations, new[] { set }.Concat(alongside ?? Enumerable.Empty<string>()));
            if (estimate.Fits)
                return null;
            string need = Megabytes(estimate.Bytes) + " of resident animation memory (" + string.Join(", ", estimate.Sets.Where(o => o.Known).Select(o => o.Set)) + ")";
            if (!estimate.FitsLarger)
                return "Together these animation sets need " + need + ", more than even the larger animation memory holds (" + Megabytes(Budget(true)) + "): characters whose set doesn't fit never spawn. Move clips to streamed sections, or use fewer sets at once.";
            return "Together these animation sets need " + need + ", more than the game's " + Megabytes(Budget(false)) + ": characters whose set doesn't fit never spawn. Turn on Launch Game > " + Label + " (launch_options " + LaunchOptionName + "), and declare it in a mod that carries the set" + (Enabled ? " - it is on." : ".");
        }
        #endregion
    }

    /// <summary>
    /// Level Check: the animation sets of the level's Characters (their anim_set, as each sets it) need more resident animation
    /// memory than the game has, so some characters never spawn. Fix: the larger animation memory launch option.
    /// </summary>
    internal sealed class AnimationHeapRule : LevelCheckRule
    {
        public override string Id => "animation_memory";
        public override string Name => "Animation memory";
        public override string About => "Characters only spawn once their animation set's resident clips fit the game's 21.5 MB resident animation memory, shared by every set in use. Adds up the sets the level's Characters use.";
        public override IEnumerable<FunctionType> Walks => new[] { FunctionType.Character };

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            Animation animations = Singleton.Animations;
            if (animations?.ClipIndex == null) return;
            Dictionary<string, List<McpPlacements.Placement>> bySet = new Dictionary<string, List<McpPlacements.Placement>>(StringComparer.OrdinalIgnoreCase);
            foreach (McpPlacements.Placement placement in context.RealPlacements(FunctionType.Character))
            {
                //As describe_npc resolves it for this placement: an archetype instance's own pin, an alias, else the Character's
                string set = null;
                Composite start = context.Commands.EntryPoints.FirstOrDefault(o => o != null && placement.Chain.Count != 0 && o.GetEntityByID(placement.Chain[0].shortGUID) == placement.Chain[0]) ?? context.Root;
                try
                {
                    JToken value = McpCharacterTools.ResolvedSetting(context.Commands, start, placement.Chain, placement, "anim_set");
                    set = value != null && value.Type == JTokenType.String ? (string)value : null;
                }
                catch { }
                if (set == null)
                    set = LevelCheckContext.Text(placement.Chain[placement.Chain.Count - 1], "anim_set");
                if (string.IsNullOrWhiteSpace(set)) continue;
                if (!bySet.TryGetValue(set.Trim(), out List<McpPlacements.Placement> list)) bySet[set.Trim()] = list = new List<McpPlacements.Placement>();
                list.Add(placement);
            }
            if (bySet.Count == 0) return;

            AnimationHeapBudget.Estimate estimate = AnimationHeapBudget.ForSets(animations, bySet.Keys);
            context.Report.Figures["animation_memory"] = estimate.ToJson();
            if (estimate.Fits) return;
            bool on = AnimationHeapBudget.Enabled;
            if (on && estimate.FitsLarger) return;

            LevelCheckFinding finding = new LevelCheckFinding()
            {
                Key = on ? "over_larger" : "over",
                Severity = LevelCheckSeverity.Broken,
                Title = "The level's animation sets need " + AnimationHeapBudget.Megabytes(estimate.Bytes) + " of resident animation memory (the game has " + AnimationHeapBudget.Megabytes(AnimationHeapBudget.Budget(on)) + ")",
                Text = on || !estimate.FitsLarger
                    ? "Even the larger animation memory can't hold them: characters whose set doesn't fit never spawn (no error). Move clips into streamed sections, or use fewer sets."
                    : "Characters whose set doesn't fit never spawn (no error, no crash). Launch Game's " + AnimationHeapBudget.Label + " doubles it (an AI.exe patch, every level); a mod carrying these sets should declare it in its launch options.",
            };
            foreach (KeyValuePair<string, List<McpPlacements.Placement>> set in bySet.OrderByDescending(o => estimate.Sets.FirstOrDefault(s => string.Equals(s.Set, o.Key, StringComparison.OrdinalIgnoreCase))?.Bytes ?? 0))
            {
                AnimationHeapBudget.SetSize size = estimate.Sets.FirstOrDefault(o => string.Equals(o.Set, set.Key, StringComparison.OrdinalIgnoreCase));
                finding.Sources.Add(context.Source(set.Value[0], set.Key + (size?.Known == true ? ": " + AnimationHeapBudget.Megabytes(size.Bytes) : ": not in ANIMATION.PAK") + (set.Value.Count > 1 ? " (" + set.Value.Count + " Characters)" : "")));
            }
            if (!on && estimate.FitsLarger && AnimationHeapBudget.Supported(Singleton.Platform))
                finding.Fix = new LevelCheckFix()
                {
                    Label = "Turn on " + AnimationHeapBudget.Label,
                    Detail = "Launch Game's option, written to AI.exe now and at every launch: it doubles the game's animation memory, for every level, from the next start.",
                    Global = true,
                    Apply = fromTool =>
                    {
                        if (!AnimationHeapBudget.Set(true))
                            throw new InvalidOperationException("AI.exe could not be written (is the game running?). The option is on: it is written at the next launch.");
                        return AnimationHeapBudget.Label + " is on (AI.exe patched; it takes effect from the game's next start).";
                    },
                };
            found.Add(finding);
        }
    }
}
