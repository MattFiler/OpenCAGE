using CATHODE;
using CathodeLib;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>How bad a Level Check finding is for whoever plays the level.</summary>
    public enum LevelCheckSeverity
    {
        /// <summary>The game crashes (or can).</summary>
        Crash = 0,
        /// <summary>Something in the level silently does not work in game.</summary>
        Broken = 1,
        /// <summary>Likely to go wrong; worth a look.</summary>
        Warning = 2,
    }

    /// <summary>Where a finding comes from: an entity in a composite, and how it is placed (when it is).</summary>
    public sealed class LevelCheckSource
    {
        public Composite Composite;
        public Entity Entity;
        /// <summary>From the level's root (or another entry point) down to the entity; null when it was not found placed.</summary>
        public List<Entity> Chain;
        /// <summary>What to show for it: "composite / entity" and any detail.</summary>
        public string Text;
    }

    /// <summary>A mechanical fix for a finding.</summary>
    public sealed class LevelCheckFix
    {
        /// <summary>The button's text ("Tick unique_material on 4 emitters").</summary>
        public string Label;
        /// <summary>What it changes, in a sentence or two (shown before it is applied, and by MCP).</summary>
        public string Detail;
        /// <summary>It changes a file outside the level, which every level (and every player of a mod carrying it) sees.</summary>
        public bool Global;
        /// <summary>
        /// Make the fix (the open level's script, as one undo step, or the global file). The argument says an MCP tool asked (its undo
        /// step is then "AI: ..."). Returns what it did; throws to refuse. The UI thread, or an MCP tool's thread.
        /// </summary>
        public Func<bool, string> Apply;

        /// <summary>
        /// A fix to the open level's script, made as one undo step through the MCP script editor (so the editor's pages, inspector and
        /// viewer follow it as they follow any edit). Returns what <paramref name="body"/> returns, or says nothing needed changing.
        /// </summary>
        internal static string Script(string label, Composite focus, bool fromTool, Func<McpScriptEdit, string> body)
        {
            string said = null;
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(label, focus, edit => { said = body(edit); },
                new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = fromTool ? "AI: Level Check: " : "Level Check: " });
            if (!outcome.Changed)
                return "Nothing needed changing (it may have been fixed already): run the Level Check again.";
            return (said ?? label) + " Save & Build to take it into the game (Undo puts it back).";
        }
    }

    /// <summary>One thing the Level Check found.</summary>
    public sealed class LevelCheckFinding
    {
        /// <summary>The check that found it ("character_cap", "sound_bank_typed" ...).</summary>
        public string Check;
        /// <summary>What in the level it is about, stable from one run to the next: a dismissal is kept against Check + Key.</summary>
        public string Key;
        public LevelCheckSeverity Severity;
        /// <summary>One line.</summary>
        public string Title;
        /// <summary>What happens in game, why, and what to do.</summary>
        public string Text;
        public List<LevelCheckSource> Sources = new List<LevelCheckSource>();
        /// <summary>Null when there is no mechanical fix.</summary>
        public LevelCheckFix Fix;
        /// <summary>The user dismissed it for this level.</summary>
        public bool Dismissed;

        public string Id => Check + ":" + Key;

        /// <summary>The warning as one paragraph, the way a Save &amp; Build lists it (and MCP save_level's level_warnings).</summary>
        public string AsWarning(int maxSources = 4)
        {
            string text = Title + ". " + Text;
            List<string> sources = Sources.Where(o => !string.IsNullOrEmpty(o.Text)).Select(o => o.Text).Distinct().ToList();
            if (sources.Count != 0)
                text += " [" + string.Join("; ", sources.Take(maxSources)) + (sources.Count > maxSources ? "; and " + (sources.Count - maxSources) + " more" : "") + "]";
            if (Fix != null)
                text += " (Level Check can fix this: " + Fix.Label + ".)";
            return text;
        }
    }

    /// <summary>What a run of the Level Check found, for one level.</summary>
    public sealed class LevelCheckReport
    {
        public string Level;
        public DateTime When = DateTime.Now;
        public List<LevelCheckFinding> Findings = new List<LevelCheckFinding>();
        /// <summary>Checks that threw, with why (their findings are lost, nothing else is).</summary>
        public Dictionary<string, string> Failed = new Dictionary<string, string>();
        /// <summary>Figures the checks worked out on the way (character slots, texture memory...), for the window and MCP.</summary>
        public JObject Figures = new JObject();
        /// <summary>The placement walk hit its budget: placed content past it was not looked at.</summary>
        public bool Truncated;
        public double Seconds;

        public IEnumerable<LevelCheckFinding> Active => Findings.Where(o => !o.Dismissed);
    }

    /// <summary>
    /// The Level Check: what in a level will crash the game or silently not work in it. A Save &amp; Build runs it as warnings
    /// (<see cref="BuildWarnings"/>) and the Level Check window or MCP check_level runs it at any time. Findings can be dismissed per
    /// level (<see cref="Dismissals"/>) and some carry a mechanical fix. The script is only read. Any thread (fixes: the UI thread or an
    /// MCP tool's).
    /// </summary>
    public static class LevelCheck
    {
        /// <summary>Every check, in the order findings are listed.</summary>
        internal static readonly List<LevelCheckRule> Rules = new List<LevelCheckRule>()
        {
            new AlienBackstageRule(),
            new CharacterCapRule(),
            new AnimationHeapRule(),
            new DisplayModelMissingRule(),
            new TextureBudgetRule(),
            new TextureSlotsRule(),
            new SoundBankPoolRule(),
            new SoundBankTypedRule(),
            new PlanarReflectionLightsRule(),
            new SameIdEmittersRule(),
            new SharedEmitterMaterialRule(),
            new PlayAnimationMarkerRule(),
            new ClipParksCapsuleRule(),
            new FakeSensePositionRule(),
            new RandomFloatDelayRule(),
            new DeadAnimationTracksRule(),
            new NestedAnimationRule(),
            new HavokWorldBoundsRule(),
            new SoundEnvironmentMarkerRule(),
            new NavMeshCoverageRule(),
            new PinOrderRule(),
            new PhysicsBodyKeysRule(),
            new WaterCullingRule(),
        };

        /// <summary>
        /// Let the authoring checks look at retail's own composites too (normally only those the user made or changed), for measuring
        /// what retail itself does. Off in the editor.
        /// </summary>
        public static bool LookAtRetail = false;

        /// <summary>The checks: id, name and what each looks for (the window's list, MCP's).</summary>
        public static IEnumerable<(string Id, string Name, string About)> Checks => Rules.Select(o => (o.Id, o.Name, o.About));

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, LevelCheckReport> _last = new Dictionary<string, LevelCheckReport>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised (any thread) when a run finishes, with its report.</summary>
        public static event Action<LevelCheckReport> Ran;

        /// <summary>The last report for a level (a build's or a check's), or null.</summary>
        public static LevelCheckReport Last(string level)
        {
            lock (_lock)
                return level != null && _last.TryGetValue(level, out LevelCheckReport report) ? report : null;
        }

        /// <summary>Run every check (or those named) on a loaded level. Never throws for a check that fails: see <see cref="LevelCheckReport.Failed"/>.</summary>
        public static LevelCheckReport Run(Level level, IEnumerable<string> only = null)
        {
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            LevelCheckReport report = new LevelCheckReport() { Level = level?.Name };
            if (level?.Commands?.Entries == null)
                return report;
            HashSet<string> wanted = only == null ? null : new HashSet<string>(only, StringComparer.OrdinalIgnoreCase);
            List<LevelCheckRule> rules = Rules.Where(o => wanted == null || wanted.Contains(o.Id)).ToList();

            LevelCheckContext context = new LevelCheckContext(level, report);
            try
            {
                context.Walk(rules.SelectMany(o => o.Walks).Distinct(), rules.SelectMany(o => o.WalksInstancesOf).Distinct());
            }
            catch (Exception e)
            {
                report.Failed["placements"] = e.Message;
                Debug.Log("LevelCheck", "Walking " + level.Name + "'s placements failed: " + e);
            }
            report.Truncated = context.Truncated;

            foreach (LevelCheckRule rule in rules)
            {
                List<LevelCheckFinding> found = new List<LevelCheckFinding>();
                try
                {
                    rule.Run(context, found);
                }
                catch (Exception e)
                {
                    report.Failed[rule.Id] = e.GetType().Name + ": " + e.Message;
                    Debug.Log("LevelCheck", "The " + rule.Id + " check failed on " + level.Name + ": " + e);
                    continue;
                }
                foreach (LevelCheckFinding finding in found)
                {
                    finding.Check = rule.Id;
                    if (string.IsNullOrEmpty(finding.Key)) finding.Key = "level";
                    finding.Dismissed = Dismissals.IsDismissed(level.Name, finding.Id);
                    report.Findings.Add(finding);
                }
            }
            report.Seconds = Math.Round(timer.Elapsed.TotalSeconds, 2);
            lock (_lock)
                _last[level.Name] = report;
            try { Ran?.Invoke(report); } catch (Exception e) { Debug.Log("LevelCheck", "A Level Check listener failed: " + e); }
            return report;
        }

        /// <summary>
        /// What a Save &amp; Build says about the level as it now is (LevelContent.CheckLevel): every finding not dismissed for it, as
        /// one warning each, crashes first. The report is kept (<see cref="Last"/>) for the Level Check window. Never throws.
        /// </summary>
        public static List<string> BuildWarnings(Level level)
        {
            try
            {
                LevelCheckReport report = Run(level);
                //The save before this wrote every composite's pins sorted (CommandsPAK), so pin_order's in-memory finding no longer holds
                report.Findings.RemoveAll(o => o.Check == "pin_order");
                return report.Active.OrderBy(o => o.Severity).Select(o => o.AsWarning()).ToList();
            }
            catch (Exception e)
            {
                Debug.Log("LevelCheck", "The Level Check failed on " + level?.Name + ": " + e);
                return new List<string>();
            }
        }

        /// <summary>
        /// Findings dismissed per level, kept on this PC beside OpenCAGE's settings ("OpenCAGE Level Check.json"), keyed by the
        /// game install and the level: never in the level's own files, so never packaged into a mod.
        /// </summary>
        public static class Dismissals
        {
            private static readonly object _fileLock = new object();
            private static JObject _data;
            private static string FilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenCAGE Level Check.json");

            private static string LevelKey(string level)
            {
                string install = "";
                try { install = Path.GetFullPath(Singleton.PathToAI ?? "").TrimEnd('\\', '/').ToUpperInvariant(); } catch { }
                return install + "|" + (level ?? "").Replace('\\', '/').ToUpperInvariant();
            }

            private static JObject Data()
            {
                if (_data != null) return _data;
                try { _data = File.Exists(FilePath) ? JObject.Parse(File.ReadAllText(FilePath)) : new JObject(); }
                catch (Exception e) { Debug.Log("LevelCheck", "Could not read " + FilePath + ": " + e.Message); _data = new JObject(); }
                return _data;
            }

            public static bool IsDismissed(string level, string findingId)
            {
                lock (_fileLock)
                    return (Data()[LevelKey(level)] as JArray)?.Any(o => string.Equals((string)o, findingId, StringComparison.Ordinal)) == true;
            }

            public static List<string> Of(string level)
            {
                lock (_fileLock)
                    return (Data()[LevelKey(level)] as JArray)?.Select(o => (string)o).ToList() ?? new List<string>();
            }

            /// <summary>Dismiss (or bring back) a finding for a level. Returns whether anything changed.</summary>
            public static bool Set(string level, string findingId, bool dismissed)
            {
                lock (_fileLock)
                {
                    JObject data = Data();
                    string key = LevelKey(level);
                    JArray list = data[key] as JArray ?? new JArray();
                    bool has = list.Any(o => string.Equals((string)o, findingId, StringComparison.Ordinal));
                    if (has == dismissed) return false;
                    if (dismissed) list.Add(findingId);
                    else foreach (JToken token in list.Where(o => string.Equals((string)o, findingId, StringComparison.Ordinal)).ToList()) token.Remove();
                    if (list.Count == 0) data.Remove(key); else data[key] = list;
                    try { File.WriteAllText(FilePath, data.ToString()); }
                    catch (Exception e) { Debug.Log("LevelCheck", "Could not write " + FilePath + ": " + e.Message); }
                }
                LevelCheckReport last = Last(level);
                if (last != null)
                    foreach (LevelCheckFinding finding in last.Findings.Where(o => o.Id == findingId))
                        finding.Dismissed = dismissed;
                return true;
            }
        }
    }

    /// <summary>One check of the Level Check.</summary>
    internal abstract class LevelCheckRule
    {
        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string About { get; }
        /// <summary>The entity types whose placements this check reads from the shared walk.</summary>
        public virtual IEnumerable<FunctionType> Walks => Enumerable.Empty<FunctionType>();
        /// <summary>The composites (by path) whose instances this check reads from the shared walk.</summary>
        public virtual IEnumerable<string> WalksInstancesOf => Enumerable.Empty<string>();
        public abstract void Run(LevelCheckContext context, List<LevelCheckFinding> found);
    }
}
