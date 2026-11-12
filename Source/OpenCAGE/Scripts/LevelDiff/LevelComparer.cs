using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;

namespace OpenCAGE.LevelDiff
{
    /// <summary>One side of a comparison: a level folder on disk, or the editor's open level as it is now.</summary>
    public sealed class LevelSide
    {
        public string Label;
        /// <summary>The level's folder (...\DATA\ENV\PRODUCTION\NAME); null for nothing on disk.</summary>
        public string Folder;
        /// <summary>The open level, unsaved edits included: script, models, materials, textures and mappings only (the rest is made by a Save &amp; Build).</summary>
        public Level Live;
        /// <summary>Taken on the thread that owns <see cref="Live"/> before the compare runs elsewhere.</summary>
        public SideData LiveData;
        /// <summary>A folder to delete afterwards (a backup materialised for the compare).</summary>
        public string Temporary;

        public static LevelSide OnDisk(string folder, string label = null)
        {
            string full = Path.GetFullPath(folder).TrimEnd('\\', '/');
            if (full.Replace('/', '\\').IndexOf("\\DATA\\ENV\\", StringComparison.OrdinalIgnoreCase) < 0)
                throw new ArgumentException("A level folder must be inside a game's DATA\\ENV folder (" + full + " is not).");
            return new LevelSide() { Folder = full, Label = label ?? full };
        }

        public string LevelName
        {
            get
            {
                if (Folder == null) return Live?.Name;
                string f = Folder.Replace('/', '\\');
                int at = f.IndexOf("\\DATA\\ENV\\", StringComparison.OrdinalIgnoreCase);
                return f.Substring(at + "\\DATA\\ENV\\".Length).Replace('\\', '/').ToUpperInvariant();
            }
        }

        /// <summary>The game folder the level is in (the one holding DATA).</summary>
        public string Install
        {
            get
            {
                string f = (Folder ?? Live?.Filepath)?.Replace('/', '\\');
                if (f == null) return null;
                int at = f.IndexOf("\\DATA\\ENV\\", StringComparison.OrdinalIgnoreCase);
                return at < 0 ? null : f.Substring(0, at);
            }
        }
    }

    public sealed class CompareOptions
    {
        /// <summary>The categories to compare (<see cref="LevelComparer.Categories"/> keys); null for all.</summary>
        public HashSet<string> Only;
        /// <summary>The global textures and animations to load levels against; null loads the side's own install's.</summary>
        public Global Global;
        public Action<string> Progress;
        public CancellationToken Cancel;

        public bool Wants(string key) => Only == null || Only.Contains(key);
    }

    /// <summary>One file's records (models, collision rows ...) keyed for comparing: <see cref="LevelComparer"/> makes one per side and category.</summary>
    public sealed class Table
    {
        public sealed class Row
        {
            public string Key, Label;
            public List<KeyValuePair<string, string>> Fields;
            private string _line;
            public string Line => _line ?? (_line = string.Join("; ", Fields.Select(o => o.Key + "=" + o.Value)));
        }

        public readonly List<Row> Rows = new List<Row>();
        private readonly Dictionary<string, int> _keys = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>Whole-file facts compared as lines ("proxies: 1186").</summary>
        public readonly List<KeyValuePair<string, string>> Facts = new List<KeyValuePair<string, string>>();
        public bool Missing;

        public void Add(string key, string label, List<KeyValuePair<string, string>> fields)
        {
            //A key used twice (two records for one entity) gets "#n": the nth of them
            _keys.TryGetValue(key, out int n);
            _keys[key] = n + 1;
            Rows.Add(new Row() { Key = n == 0 ? key : key + " #" + n, Label = n == 0 ? label : label + " #" + n, Fields = fields });
        }

        public void Fact(string name, object value) => Facts.Add(new KeyValuePair<string, string>(name, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>Everything compared about one side, read from it once.</summary>
    public sealed class SideData
    {
        public string Label, LevelName, Install;
        public ScriptSnapshot Script;
        public readonly Dictionary<string, Table> Tables = new Dictionary<string, Table>();
        /// <summary>Relative path -> (size, md5); null when the side isn't on disk.</summary>
        public Dictionary<string, (long size, string hash)> Files;
        public Func<ShortGuid, ShortGuid, ShortGuid, string, bool> IsDefault;
        public bool Live;
        public readonly List<string> Notes = new List<string>();
    }

    /// <summary>
    /// Compare Levels: two copies of a level compared beyond the script (assets, placements, collision, physics, zones, navigation,
    /// sound, text, files byte for byte and, across installs, configs). Each category lists what was added, removed and changed.
    /// </summary>
    public static class LevelComparer
    {
        public static readonly (string Key, string Title)[] Categories =
        {
            ("script", "Script"),
            ("models", "Models"),
            ("materials", "Materials"),
            ("textures", "Textures"),
            ("mappings", "Material mappings"),
            ("placements", "Placements (movers)"),
            ("lights", "Lights"),
            ("collision", "Collision"),
            ("physics", "Physics"),
            ("zones", "Zones"),
            ("navigation", "Navigation"),
            ("sound", "Sound"),
            ("text", "Text"),
            ("other", "Other level data"),
            ("files", "Files"),
            ("configs", "Configs"),
        };

        /// <summary>The categories the open level (unsaved edits) can be compared in; the rest are made by a Save &amp; Build.</summary>
        public static readonly HashSet<string> LiveCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "script", "models", "materials", "textures", "mappings", "zones" };

        public static string Title(string key) => Categories.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase)).Title ?? key;

        #region Comparing
        public static LevelDiffReport Compare(LevelSide before, LevelSide after, CompareOptions options = null)
        {
            options = options ?? new CompareOptions();
            Stopwatch timer = Stopwatch.StartNew();
            LevelDiffReport report = new LevelDiffReport() { Before = before.Label, After = after.Label };
            SideData a = null, b = null;
            try
            {
                a = Read(before, options, "before");
                options.Cancel.ThrowIfCancellationRequested();
                b = Read(after, options, "after");
                options.Cancel.ThrowIfCancellationRequested();
                options.Progress?.Invoke("Comparing");
                Compare(a, b, options, report);
            }
            finally
            {
                foreach (LevelSide side in new[] { before, after })
                    if (side.Temporary != null)
                        try { Directory.Delete(side.Temporary, true); } catch { }
            }
            report.Notes.AddRange(a.Notes.Select(o => "before: " + o));
            report.Notes.AddRange(b.Notes.Select(o => "after: " + o));
            report.Took = timer.Elapsed;
            return report;
        }

        public static void Compare(SideData a, SideData b, CompareOptions options, LevelDiffReport report)
        {
            foreach ((string key, string title) in Categories)
            {
                DiffCategory category;
                if (!options.Wants(key)) { category = new DiffCategory(key, title) { Skipped = "not asked for" }; report.Categories.Add(category); continue; }
                try
                {
                    category = CompareCategory(key, title, a, b);
                }
                catch (Exception e)
                {
                    category = new DiffCategory(key, title) { Error = e.GetType().Name + ": " + e.Message };
                }
                report.Categories.Add(category);
            }
        }

        private static DiffCategory CompareCategory(string key, string title, SideData a, SideData b)
        {
            if ((a.Live || b.Live) && !LiveCategories.Contains(key) && key != "files" && key != "configs")
                return new DiffCategory(key, title) { Skipped = "the open level's " + title.ToLowerInvariant() + " are made by a Save & Build: compare the saved level" };
            switch (key)
            {
                case "script":
                    if (a.Script == null || b.Script == null) return new DiffCategory(key, title) { Skipped = "no script on one side" };
                    ScriptDelta delta = ScriptDelta.Compare(a.Script, b.Script, (c, e, p, v, after) => (after ? b.IsDefault : a.IsDefault)?.Invoke(c, e, p, v) ?? false);
                    return ScriptReport.Build(delta);
                case "zones":
                    return Zones(a, b);
                case "files":
                    return FilesCategory(a, b);
                case "text":
                    a.Tables.TryGetValue(key, out Table xa);
                    b.Tables.TryGetValue(key, out Table xb);
                    if (xa == null || xb == null) return new DiffCategory(key, title) { Skipped = "not read" };
                    if ((xa.Rows.Count == 0) != (xb.Rows.Count == 0))
                        return new DiffCategory(key, title) { Skipped = (xa.Rows.Count == 0 ? "before" : "after") + " has no text loaded: its game folder has no DATA\\LEVEL_TEXT_DATABASES.XML or doesn't list the level (by its folder name)" };
                    return CompareTables(key, title, xa, xb);
                case "configs":
                    return Configs(a, b);
                default:
                    a.Tables.TryGetValue(key, out Table ta);
                    b.Tables.TryGetValue(key, out Table tb);
                    if (ta == null || tb == null) return new DiffCategory(key, title) { Skipped = "not read" };
                    return CompareTables(key, title, ta, tb);
            }
        }

        public static DiffCategory CompareTables(string key, string title, Table a, Table b, int maxDetails = 60)
        {
            DiffCategory category = new DiffCategory(key, title);
            if (a.Missing && b.Missing) { category.Summary = "on neither side"; return category; }
            if (a.Missing || b.Missing) category.Notes.Add((a.Missing ? "before" : "after") + " has none of this data (its file is missing or unreadable).");
            //Whole-file facts first
            Dictionary<string, string> factsA = a.Facts.GroupBy(o => o.Key).ToDictionary(o => o.Key, o => o.First().Value);
            foreach (KeyValuePair<string, string> fact in b.Facts)
            {
                factsA.TryGetValue(fact.Key, out string old);
                if (old != fact.Value) category.Item(DiffKind.Info, fact.Key + ": " + (old ?? "(none)") + " -> " + fact.Value);
            }
            Dictionary<string, Table.Row> rowsA = new Dictionary<string, Table.Row>(StringComparer.Ordinal);
            foreach (Table.Row r in a.Rows) rowsA[r.Key] = r;
            HashSet<string> keysB = new HashSet<string>(StringComparer.Ordinal);
            //Differences that are no difference in game, gathered under one line each rather than listed among the real ones
            DiffNode rounding = new DiffNode(DiffKind.Info, null), globalCopies = new DiffNode(DiffKind.Info, null);
            Dictionary<string, int> fieldCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            //A record keyed by name + content hash (a material sharing its name) gets another key when its content changes: pair the
            //lone unmatched one of a name on each side as the same record changed
            Dictionary<string, Table.Row> paired = PairByPlainName(a, b);
            foreach (Table.Row r in b.Rows)
            {
                keysB.Add(r.Key);
                if (paired.TryGetValue(r.Key, out Table.Row pairedOld)) { keysB.Add(pairedOld.Key); rowsA[r.Key] = pairedOld; }
                if (!rowsA.TryGetValue(r.Key, out Table.Row old))
                {
                    if (r.Fields.Any(f => f.Key == GlobalCopyField && f.Value == "yes")) { globalCopies.Add(DiffKind.Added, r.Label, key + ":" + r.Key); continue; }
                    DiffNode added = category.Item(DiffKind.Added, r.Label, key + ":" + r.Key);
                    foreach (KeyValuePair<string, string> f in r.Fields.Take(maxDetails)) added.Add(DiffKind.Info, f.Key + ": " + f.Value);
                    continue;
                }
                if (old.Line == r.Line) continue;
                if (OnlyRounding(old.Fields, r.Fields)) { rounding.Add(DiffKind.Changed, r.Label + ": " + string.Join("; ", Fields.Changes(old.Fields, r.Fields, 4)), key + ":" + r.Key); continue; }
                DiffNode changed = category.Item(DiffKind.Changed, r.Label, key + ":" + r.Key);
                List<string> changes = Fields.Changes(old.Fields, r.Fields, maxDetails);
                foreach (string c in changes) changed.Add(DiffKind.Changed, c);
                foreach (string field in ChangedFieldNames(old.Fields, r.Fields)) fieldCounts[field] = (fieldCounts.TryGetValue(field, out int n) ? n : 0) + 1;
            }
            foreach (Table.Row r in a.Rows)
                if (!keysB.Contains(r.Key))
                    category.Item(DiffKind.Removed, r.Label, key + ":" + r.Key);
            if (globalCopies.Children.Count != 0)
            {
                globalCopies.Text = globalCopies.Children.Count + " global textures copied into the level (a save copies in the global textures its materials use; the same pixels)";
                category.Items.Add(globalCopies);
            }
            if (rounding.Children.Count != 0)
            {
                rounding.Text = rounding.Children.Count + " differ only by float rounding (under 0.0001): no difference in game";
                category.Items.Add(rounding);
            }
            if (category.Changed > 20 && fieldCounts.Count != 0)
                category.Items.Insert(0, new DiffNode(DiffKind.Info, "fields changed: " + string.Join(", ", fieldCounts.OrderByDescending(o => o.Value).Take(12).Select(o => o.Key + " (" + o.Value + ")"))));
            return category;
        }

        public const string GlobalCopyField = "copy of a global texture";

        /// <summary>The key without its content hash ("01 - DEFAULT {a1b2c3d4} #1" -> "01 - DEFAULT").</summary>
        public static string PlainKey(string key) => System.Text.RegularExpressions.Regex.Replace(key, @"( \{[0-9a-f]{8}\})?( #\d+)?$", "");

        /// <summary>After-side keys of rows whose key isn't on the before side, paired with the before-side row of the same plain name when each side has exactly one such.</summary>
        public static Dictionary<string, Table.Row> PairByPlainName(Table a, Table b)
        {
            HashSet<string> keysA = new HashSet<string>(a.Rows.Select(o => o.Key), StringComparer.Ordinal), keysB = new HashSet<string>(b.Rows.Select(o => o.Key), StringComparer.Ordinal);
            Dictionary<string, List<Table.Row>> onlyA = a.Rows.Where(o => !keysB.Contains(o.Key)).GroupBy(o => PlainKey(o.Key)).ToDictionary(o => o.Key, o => o.ToList());
            Dictionary<string, Table.Row> pairs = new Dictionary<string, Table.Row>(StringComparer.Ordinal);
            foreach (IGrouping<string, Table.Row> g in b.Rows.Where(o => !keysA.Contains(o.Key)).GroupBy(o => PlainKey(o.Key)))
                if (g.Count() == 1 && onlyA.TryGetValue(g.Key, out List<Table.Row> olds) && olds.Count == 1 && olds[0].Key != g.First().Key)
                    pairs[g.First().Key] = olds[0];
            return pairs;
        }

        private static readonly System.Text.RegularExpressions.Regex Number = new System.Text.RegularExpressions.Regex(@"-?\d+(\.\d+)?(E[-+]?\d+)?", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>Whether every field that differs differs only in numbers, each by under 0.0001 (a float written back after a round trip).</summary>
        public static bool OnlyRounding(List<KeyValuePair<string, string>> a, List<KeyValuePair<string, string>> b)
        {
            if (a.Count != b.Count) return false;
            bool any = false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Key != b[i].Key) return false;
                if (a[i].Value == b[i].Value) continue;
                any = true;
                if (Number.Replace(a[i].Value, "#") != Number.Replace(b[i].Value, "#")) return false;
                System.Text.RegularExpressions.MatchCollection na = Number.Matches(a[i].Value), nb = Number.Matches(b[i].Value);
                if (na.Count != nb.Count) return false;
                for (int n = 0; n < na.Count; n++)
                {
                    if (!double.TryParse(na[n].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x) ||
                        !double.TryParse(nb[n].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double y)) return false;
                    if (Math.Abs(x - y) > 1e-4 * Math.Max(1, Math.Abs(x))) return false;
                }
            }
            return any;
        }

        private static IEnumerable<string> ChangedFieldNames(List<KeyValuePair<string, string>> a, List<KeyValuePair<string, string>> b)
        {
            Dictionary<string, string> av = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> f in a) av[f.Key] = f.Value;
            HashSet<string> names = new HashSet<string>();
            foreach (KeyValuePair<string, string> f in b)
                if (!av.TryGetValue(f.Key, out string v) || v != f.Value)
                    names.Add(System.Text.RegularExpressions.Regex.Replace(f.Key, @"\[\d+\]|\s\d+(\.\d+)*$", "[]"));
            return names;
        }
        #endregion

        #region Reading a side
        public static SideData Read(LevelSide side, CompareOptions options, string which)
        {
            if (side.Live != null)
            {
                if (side.LiveData == null) throw new InvalidOperationException("The open level's data must be read on the editor's thread first (LevelComparer.ReadLive).");
                SideData live = side.LiveData;
                if (options.Wants("files") && side.Live.Filepath != null)
                    live.Notes.Add("files: the open level's files are as last saved; unsaved edits are not in them.");
                return live;
            }
            options.Progress?.Invoke("Loading " + which + ": " + side.Label);
            Global global = options.Global ?? LoadGlobal(side.Install);
            Level level = new Level(side.Folder, global, false) { AbsorbGlobalTextures = false };
            level.Load();
            options.Cancel.ThrowIfCancellationRequested();
            options.Progress?.Invoke("Reading " + which);
            SideData data = Describe(level, options, false);
            data.Label = side.Label;
            data.Install = side.Install;
            if (options.Wants("files"))
            {
                options.Progress?.Invoke("Hashing the " + which + " level's files");
                data.Files = HashFiles(side.Folder, level.Name);
            }
            return data;
        }

        public static Global LoadGlobal(string install)
        {
            string path = Path.Combine(install ?? "", "DATA", "ENV", "GLOBAL");
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("There is no DATA\\ENV\\GLOBAL beside the level (" + path + ").");
            return new Global(path);
        }

        /// <summary>The open level's comparable data, read on the thread that owns it (the editor's UI thread).</summary>
        public static SideData ReadLive(Level level, CompareOptions options, string label)
        {
            SideData data = Describe(level, options, true);
            data.Label = label;
            data.Install = new LevelSide() { Live = level }.Install;
            return data;
        }

        public static SideData Describe(Level level, CompareOptions options, bool live)
        {
            SideData data = new SideData() { LevelName = level.Name, Live = live };
            ResourceNames names = new ResourceNames(level);
            if (options.Wants("script") || options.Wants("zones"))
            {
                data.Script = ScriptSnapshot.Take(level.Commands, names, level.Name);
                Commands commands = level.Commands;
                data.IsDefault = (c, e, p, v) => IsDefault(commands, names, c, e, p, v);
            }
            Run(data, "models", options, () => Models(level, names));
            Run(data, "materials", options, () => Materials(level, names));
            Run(data, "textures", options, () => Textures(level));
            Run(data, "mappings", options, () => Mappings(level));
            if (!live)
            {
                Dictionary<ShortGuid, string> entityNames = data.Script != null ? EntityNames(data.Script) : null;
                if (options.Wants("placements") || options.Wants("lights") || options.Wants("zones"))
                {
                    (Table placements, Table lights) = Movers(level, names, entityNames);
                    data.Tables["placements"] = placements;
                    data.Tables["lights"] = lights;
                }
                Run(data, "collision", options, () => Collision(level, names, entityNames));
                Run(data, "physics", options, () => Physics(level, names, entityNames));
                Run(data, "navigation", options, () => Navigation(level, names));
                Run(data, "sound", options, () => Sound(level, names));
                Run(data, "text", options, () => Text(level));
                Run(data, "other", options, () => Other(level, names));
            }
            return data;
        }

        private static void Run(SideData data, string key, CompareOptions options, Func<Table> read)
        {
            if (!options.Wants(key)) return;
            options.Cancel.ThrowIfCancellationRequested();
            try { data.Tables[key] = read(); }
            catch (Exception e) { data.Tables[key] = new Table() { Missing = true }; data.Notes.Add(key + " could not be read: " + e.Message); }
        }

        private static bool IsDefault(Commands commands, ResourceNames names, ShortGuid compositeId, ShortGuid entityId, ShortGuid parameter, string value)
        {
            try
            {
                Composite composite = commands.GetComposite(compositeId);
                Entity entity = composite?.GetEntityByID(entityId);
                if (entity == null) return false;
                ParameterData def = commands.Utils.CreateDefaultParameterData(entity, composite, parameter);
                if (def == null) return false;
                return CathodeJson.Text(CathodeJson.Value(def, names)) == value;
            }
            catch { return false; }
        }

        private static Dictionary<ShortGuid, string> EntityNames(ScriptSnapshot script)
        {
            Dictionary<ShortGuid, string> names = new Dictionary<ShortGuid, string>();
            foreach (CompositeSnap c in script.Composites.Values)
                foreach (EntitySnap e in c.Entities.Values)
                    if (!names.ContainsKey(e.Id)) names[e.Id] = (string.IsNullOrEmpty(e.Name) ? CathodeJson.Id(e.Id) : e.Name) + " in " + c.Name;
            return names;
        }

        private static string Who(EntityHandle handle, Dictionary<ShortGuid, string> entityNames)
        {
            if (handle == null) return "(no entity)";
            string name = entityNames != null && entityNames.TryGetValue(handle.entity_id, out string n) ? n : handle.entity_id.ToByteString();
            return name + " (instance " + handle.composite_instance_id.ToByteString() + ")";
        }
        private static string HandleKey(EntityHandle handle) => handle == null ? "none" : handle.entity_id.ToByteString() + "@" + handle.composite_instance_id.ToByteString();
        #endregion

        #region Tables
        private static Table Models(Level level, ResourceNames names)
        {
            Table t = new Table();
            if (level.Models == null) { t.Missing = true; return t; }
            Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Models.CS2 model in level.Models.Entries)
            {
                if (model == null) continue;
                List<KeyValuePair<string, string>> f = new List<KeyValuePair<string, string>>();
                int subs = 0, verts = 0, idx = 0;
                for (int c = 0; c < model.Components.Count; c++)
                    for (int l = 0; l < model.Components[c].LODs.Count; l++)
                        for (int s = 0; s < model.Components[c].LODs[l].Submeshes.Count; s++)
                        {
                            Models.CS2.Component.LOD.Submesh sub = model.Components[c].LODs[l].Submeshes[s];
                            if (sub == null) continue;
                            subs++; verts += sub.VertexCount; idx += sub.IndexCount;
                            f.Add(new KeyValuePair<string, string>("submesh " + c + "." + l + "." + s,
                                "material " + names.MaterialName(sub.Material) + ", " + sub.VertexCount + " verts, " + sub.IndexCount + " indices, bounds " + Fields.Vec(sub.MinBounds) + "-" + Fields.Vec(sub.MaxBounds) +
                                ", lod " + Fields.Num(sub.MinLODRange) + "-" + Fields.Num(sub.MaxLODRange) + ", flags " + sub.RenderFlags + ", collision " + ProxyOf(level, names, sub.CollisionProxyIndex) + ", data " + Fields.Hash(sub.Data)));
                        }
                f.Insert(0, new KeyValuePair<string, string>("size", model.Components.Count + " components, " + subs + " submeshes, " + verts + " verts, " + idx + " indices"));
                string name = names.ModelName(model);
                t.Add(name, "model " + name, f);
            }
            t.Fact("models", level.Models.Entries.Count);
            return t;
        }

        private static string ProxyOf(Level level, ResourceNames names, int index)
        {
            if (index < 0) return "none";
            try { return names.ProxyName(level.Collision?.GetCompound(index)); }
            catch { return "proxy #" + index + " (not in COLLISION.HKX)"; }
        }

        /// <summary>Every material, keyed by <see cref="ResourceNames.MaterialName"/>, with its <see cref="MaterialFields"/>.</summary>
        public static Table Materials(Level level, ResourceNames names)
        {
            Table t = new Table();
            if (level.Materials == null) { t.Missing = true; return t; }
            int global = 0;
            foreach (Materials.Material m in level.Materials.Entries)
            {
                if (m == null) continue;
                global += m.TextureReferences.Count(o => o?.Location == TexturePtr.Source.GLOBAL);
                string name = names.MaterialName(m);
                t.Add(name, "material " + name, MaterialFields(m, names));
            }
            t.Fact("materials", level.Materials.Entries.Count);
            t.Fact("texture slots reading the global textures", global);
            return t;
        }

        /// <summary>
        /// A material's values as fields: shader, texture slots (by texture name: a save copies the global textures a level uses into it,
        /// which moves no pixel), constants (written to read back exactly), light flags, physical material, environment map slot, priority.
        /// Recipes set these by field name (<see cref="Recipes.RecipeBuilder.IsReplayableMaterialField"/>).
        /// </summary>
        public static List<KeyValuePair<string, string>> MaterialFields(Materials.Material m, ResourceNames names)
        {
            List<KeyValuePair<string, string>> f = new List<KeyValuePair<string, string>>();
            f.Add(new KeyValuePair<string, string>("shader", m.Shader == null ? "none" : m.Shader.Ubershader + "/" + m.Shader.Technique + " " + m.Shader.PermutationHash.ToString("x8")));
            for (int i = 0; i < m.TextureReferences.Count; i++)
                f.Add(new KeyValuePair<string, string>("texture slot " + i, m.TextureReferences[i]?.Texture == null ? "none" : m.TextureReferences[i].Texture.Name));
            AddConstants(f, "engine constant", m.EngineConstants);
            AddConstants(f, "vertex constant", m.VertexShaderConstants);
            AddConstants(f, "pixel constant", m.PixelShaderConstants);
            AddConstants(f, "hull constant", m.HullShaderConstants);
            AddConstants(f, "domain constant", m.DomainShaderConstants);
            if (m.OfflineLightFeatures != null) f.AddRange(Fields.Of(m.OfflineLightFeatures, names, 2).Select(o => new KeyValuePair<string, string>("light " + o.Key, o.Value)));
            f.Add(new KeyValuePair<string, string>("physical material", m.PhysicalMaterialIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            f.Add(new KeyValuePair<string, string>("environment map slot", m.EnvironmentMapIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            f.Add(new KeyValuePair<string, string>("priority", m.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            return f;
        }

        private static void AddConstants(List<KeyValuePair<string, string>> f, string what, List<float> values)
        {
            if (values == null) return;
            f.Add(new KeyValuePair<string, string>(what + "s", values.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            for (int i = 0; i < values.Count; i++) f.Add(new KeyValuePair<string, string>(what + " " + i, values[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        }

        private static Table Textures(Level level)
        {
            Table t = new Table();
            if (level.Textures == null) { t.Missing = true; return t; }
            Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> globals = new HashSet<string>((level.Global?.Textures?.Entries ?? new List<Textures.TEX4>()).Where(o => o?.Name != null).Select(o => o.Name), StringComparer.OrdinalIgnoreCase);
            foreach (Textures.TEX4 tex in level.Textures.Entries)
            {
                if (tex == null) continue;
                List<KeyValuePair<string, string>> f = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>(GlobalCopyField, globals.Contains(tex.Name ?? "") ? "yes" : "no"),
                    new KeyValuePair<string, string>("format", tex.Format.ToString()),
                    new KeyValuePair<string, string>("state", tex.StateFlags.ToString()),
                    new KeyValuePair<string, string>("usage", tex.UsageFlags.ToString()),
                    new KeyValuePair<string, string>("persistent", Part(tex.TexturePersistent)),
                    new KeyValuePair<string, string>("streamed", Part(tex.TextureStreamed)),
                };
                string name = tex.Name ?? "";
                seen.TryGetValue(name, out int n);
                seen[name] = n + 1;
                t.Add(n == 0 ? name : name + "#" + n, "texture " + name, f);
            }
            t.Fact("textures", level.Textures.Entries.Count);
            return t;
        }
        private static string Part(Textures.TEX4.Texture p) => p == null || p.Content == null ? "none" : p.Width + "x" + p.Height + (p.Depth > 1 ? "x" + p.Depth : "") + " " + p.MipLevels + " mips, " + Fields.Hash(p.Content);

        private static Table Mappings(Level level)
        {
            Table t = new Table();
            if (level.MaterialMappings == null) { t.Missing = true; return t; }
            foreach (MaterialMappings.MaterialMapping m in level.MaterialMappings.Entries)
            {
                if (m == null) continue;
                List<KeyValuePair<string, string>> f = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("id", m.ID.ToByteString()) };
                for (int i = 0; i < m.Mappings.Count; i++) f.Add(new KeyValuePair<string, string>("map " + i, m.Mappings[i]?.from + " -> " + m.Mappings[i]?.to));
                t.Add(m.Name ?? m.ID.ToByteString(), "mapping " + m.Name, f);
            }
            return t;
        }

        private static (Table placements, Table lights) Movers(Level level, ResourceNames names, Dictionary<ShortGuid, string> entityNames)
        {
            Table placements = new Table(), lights = new Table();
            if (level.Movers == null) { placements.Missing = lights.Missing = true; return (placements, lights); }
            int lightCount = 0;
            foreach (Movers.MOVER_DESCRIPTOR m in level.Movers.Entries)
            {
                if (m == null) continue;
                bool light = m.RenderableElements != null && m.RenderableElements.Any(o => o?.Material?.OfflineLightFeatures != null);
                List<KeyValuePair<string, string>> f = Fields.Of(m, names, 4);
                //A mover with no entity (static geometry) is keyed by its resource, else by what it draws
                string key = m.Entity != null && !m.Entity.entity_id.IsInvalid ? HandleKey(m.Entity)
                    : m.Resource != null ? "resource " + m.Resource.resource_id.ToByteString() + "@" + m.Resource.composite_instance_id.ToByteString()
                    : "unowned " + string.Join(",", (m.RenderableElements ?? new List<RenderableElements.Element>()).Select(o => o == null ? "-" : CathodeJson.Text(names.Element(o))));
                string label = m.Entity != null && !m.Entity.entity_id.IsInvalid ? Who(m.Entity, entityNames) : key;
                if (light) { lights.Add(key, "light " + label, f); lightCount++; }
                else placements.Add(key, label, f);
            }
            placements.Fact("movers", level.Movers.Entries.Count);
            lights.Fact("lights", lightCount);
            return (placements, lights);
        }

        private static Table Collision(Level level, ResourceNames names, Dictionary<ShortGuid, string> entityNames)
        {
            Table t = new Table();
            if (level.CollisionMaps == null) { t.Missing = true; return t; }
            foreach (CollisionMaps.COLLISION_MAPPING row in level.CollisionMaps.Entries)
            {
                if (row == null) continue;
                t.Add(HandleKey(row.Entity) + " " + row.ResourceGUID.ToByteString(), "collision row of " + Who(row.Entity, entityNames), Fields.Of(row, names, 2));
            }
            t.Fact("COLLISION.MAP rows", level.CollisionMaps.Entries.Count);
            HavokPackfile hkx = level.Collision;
            t.Fact("collision proxies (COLLISION.HKX)", hkx == null ? "none" : hkx.StaticCompoundShapes.Count.ToString());
            t.Fact("weighted collision (COLLISION.BIN)", level.WeightedCollisions?.Entries?.Count ?? 0);
            return t;
        }

        private static Table Physics(Level level, ResourceNames names, Dictionary<ShortGuid, string> entityNames)
        {
            Table t = new Table();
            if (level.PhysicsMaps == null) { t.Missing = true; return t; }
            foreach (PhysicsMaps.DYNAMIC_PHYSICS_SYSTEM row in level.PhysicsMaps.Entries)
            {
                if (row == null) continue;
                t.Add(HandleKey(row.entity) + " " + row.composite_instance_id.ToByteString(), "physics of " + Who(row.entity, entityNames), Fields.Of(row, names, 2));
            }
            t.Fact("PHYSICS.MAP rows", level.PhysicsMaps.Entries.Count);
            HavokPackfile hkx = level.Physics;
            t.Fact("physics systems (PHYSICS.HKX)", hkx == null ? "none" : hkx.PhysicsSystems.Count + ": " + string.Join(", ", hkx.PhysicsSystems.Take(40).Select(o => o?.Name)));
            return t;
        }

        private static Table Navigation(Level level, ResourceNames names)
        {
            Table t = new Table();
            for (int i = 0; i < level.StateResources.Count; i++)
            {
                Level.State s = level.StateResources[i];
                string state = i == 0 ? "default state" : "state " + i;
                if (s.NavMesh != null)
                {
                    List<KeyValuePair<string, string>> f = Fields.Of(s.NavMesh.Header, names, 1);
                    f.Add(new KeyValuePair<string, string>("vertices", ListHash(s.NavMesh.Vertices)));
                    f.Add(new KeyValuePair<string, string>("polygons", ListHash(s.NavMesh.Polygons)));
                    f.Add(new KeyValuePair<string, string>("detail meshes", ListHash(s.NavMesh.DetailMeshes)));
                    f.Add(new KeyValuePair<string, string>("off-mesh connections", ListHash(s.NavMesh.OffMeshConnections)));
                    t.Add("navmesh " + i, "navmesh, " + state, f);
                }
                if (s.Cover != null) t.Add("cover " + i, "cover, " + state, new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("segments", ListHash(s.Cover.Entries, names)) });
                if (s.SpottingPositions != null) t.Add("spotting " + i, "spotting positions, " + state, new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("cells", ListHash(s.SpottingPositions.Cells, names)) });
                if (s.CrawlSpaceSpottingPositions != null) t.Add("crawl spotting " + i, "crawl-space spotting positions, " + state, new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("cells", ListHash(s.CrawlSpaceSpottingPositions.Cells, names)) });
                if (s.AssaultPositions != null) t.Add("assault " + i, "assault positions, " + state, new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("cells", ListHash(s.AssaultPositions.Cells, names)) });
            }
            t.Fact("states", level.StateResources.Count);
            return t;
        }

        private static string ListHash(IEnumerable items, ResourceNames names = null)
        {
            if (items == null) return "none";
            List<object> list = items.Cast<object>().ToList();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (object o in list) sb.Append(Fields.Line(o, names, 3, 1000)).Append('\n');
            return list.Count + " (" + Fields.Hash(System.Text.Encoding.UTF8.GetBytes(sb.ToString())) + ")";
        }

        private static Table Sound(Level level, ResourceNames names)
        {
            Table t = new Table();
            EntriesOf(t, "bank data", level.SoundBankData, names);
            EntriesOf(t, "event data", level.SoundEventData, names);
            EntriesOf(t, "environment data", level.SoundEnvironmentData, names);
            EntriesOf(t, "node network", level.SoundNodeNetwork, names);
            EntriesOf(t, "dialogue lookups", level.SoundDialogueLookups, names);
            return t;
        }

        /// <summary>Each entry of a file's Entries list as a row, keyed by its name when it has one, else its index.</summary>
        private static void EntriesOf(Table t, string what, object file, ResourceNames names)
        {
            if (file == null) { t.Fact(what, "none"); return; }
            FieldInfo entries = file.GetType().GetField("Entries", BindingFlags.Public | BindingFlags.Instance);
            IList list = entries?.GetValue(file) as IList;
            if (list == null) { t.Fact(what, "unreadable"); return; }
            t.Fact(what + " entries", list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                object entry = list[i];
                string key = KeyOf(entry) ?? "#" + i;
                t.Add(what + " " + key, what + " " + key, Fields.Of(entry, names, 4, 256));
            }
        }

        private static string KeyOf(object entry)
        {
            if (entry == null) return null;
            if (entry is string s) return s;
            foreach (string name in new[] { "name", "Name", "id", "ID", "bank_name", "BankName" })
            {
                FieldInfo f = entry.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                object v = f?.GetValue(entry);
                if (v != null) return v is ShortGuid g ? g.ToByteString() : v.ToString();
            }
            return null;
        }

        private static Table Text(Level level)
        {
            Table t = new Table();
            if (level.Strings == null) { t.Missing = true; return t; }
            foreach (KeyValuePair<string, Dictionary<string, TextDB>> lang in level.Strings.OrderBy(o => o.Key))
                foreach (KeyValuePair<string, TextDB> db in lang.Value.OrderBy(o => o.Key))
                    foreach (KeyValuePair<string, string> s in db.Value.Entries)
                        t.Add(lang.Key + "/" + db.Key + "/" + s.Key, lang.Key + " " + db.Key + " " + s.Key, new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("text", "\"" + s.Value + "\"") });
            return t;
        }

        private static Table Other(Level level, ResourceNames names)
        {
            Table t = new Table();
            EntriesOf(t, "environment animations", level.EnvironmentAnimations, names);
            EntriesOf(t, "behaviour trees", level.BehaviorTreeDB, names);
            EntriesOf(t, "galaxy items", level.GalaxyItems, names);
            EntriesOf(t, "resources", level.Resources, names);
            EntriesOf(t, "path barriers", level.PathBarrierResources, names);
            EntriesOf(t, "character accessory sets", level.AccessorySets, names);
            EntriesOf(t, "morph targets", level.MorphTargetDB, names);
            t.Fact("alpha light", level.AlphaLight?.ImageData == null ? "none" : Fields.Hash(level.AlphaLight.ImageData));
            t.Fact("occluder triangles", level.OccluderTriangleBVH == null ? "none" : ListHash(level.OccluderTriangleBVH.Triangles));
            t.Fact("renderable elements (REDS)", level.RenderableElements?.Entries?.Count ?? 0);
            return t;
        }
        #endregion

        #region Zones, files, configs
        private static readonly string[] ZoneTypes = { "Zone", "ZoneLink", "ZoneExclusionLink", "ZoneLoaded", "ZoneInterface", "ZoneLinkGate" };

        private static DiffCategory Zones(SideData a, SideData b)
        {
            DiffCategory category = new DiffCategory("zones", "Zones");
            if (a.Script == null || b.Script == null) { category.Skipped = "no script on one side"; return category; }
            HashSet<string> kinds = new HashSet<string>(ZoneTypes.Select(o => "function " + o), StringComparer.Ordinal);
            ScriptSnapshot Filter(ScriptSnapshot s)
            {
                ScriptSnapshot f = new ScriptSnapshot() { LevelName = s.LevelName, Root = s.Root };
                foreach (ShortGuid id in s.Order)
                {
                    CompositeSnap c = s.Composites[id];
                    CompositeSnap z = new CompositeSnap() { Id = c.Id, Name = c.Name };
                    foreach (EntitySnap e in c.Entities.Values) if (kinds.Contains(e.Kind)) z.Entities.Add(e.Id, e);
                    if (z.Entities.Count != 0) { f.Composites.Add(id, z); f.Order.Add(id); }
                }
                return f;
            }
            ScriptSnapshot za = Filter(a.Script), zb = Filter(b.Script);
            //A composite with no zones on one side is still the same composite: give it an empty entry there
            foreach (ShortGuid id in za.Order.Where(o => !zb.Composites.ContainsKey(o) && b.Script.Composites.ContainsKey(o)).ToList()) { zb.Composites[id] = new CompositeSnap() { Id = id, Name = b.Script.Composites[id].Name }; zb.Order.Add(id); }
            foreach (ShortGuid id in zb.Order.Where(o => !za.Composites.ContainsKey(o) && a.Script.Composites.ContainsKey(o)).ToList()) { za.Composites[id] = new CompositeSnap() { Id = id, Name = a.Script.Composites[id].Name }; za.Order.Add(id); }
            DiffCategory zones = ScriptReport.Build(ScriptDelta.Compare(za, zb));
            foreach (DiffNode item in zones.Items) category.Items.Add(item);
            category.Added = zones.Added; category.Removed = zones.Removed; category.Changed = zones.Changed;
            int zoneEntities = zb.Composites.Values.Sum(o => o.Entities.Count);
            //Placements whose zone changed
            if (a.Tables.TryGetValue("placements", out Table pa) && b.Tables.TryGetValue("placements", out Table pb))
            {
                Dictionary<string, string> zoneA = pa.Rows.ToDictionary(o => o.Key, o => ZoneOf(o));
                int moved = pb.Rows.Count(o => zoneA.TryGetValue(o.Key, out string z) && z != ZoneOf(o));
                if (moved != 0) category.Item(DiffKind.Info, moved + " placements are in another zone (Placements lists them: PrimaryZoneID / SecondaryZoneID)");
            }
            category.Summary = (category.HasDifferences ? category.Line() : "no differences") + " (" + zoneEntities + " zone entities after)";
            return category;
        }
        private static string ZoneOf(Table.Row r) => string.Join(",", r.Fields.Where(f => f.Key == "PrimaryZoneID" || f.Key == "SecondaryZoneID").Select(f => f.Value));

        /// <summary>The level folder's files: relative path -> (size, MD5). A Nostromo level's _PATCH folder comes in as "_PATCH\...".</summary>
        public static Dictionary<string, (long, string)> HashFiles(string folder, string levelName)
        {
            Dictionary<string, (long, string)> files = new Dictionary<string, (long, string)>(StringComparer.OrdinalIgnoreCase);
            void Walk(string root, string prefix)
            {
                if (!Directory.Exists(root)) return;
                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string rel = prefix + file.Substring(root.Length).TrimStart('\\', '/');
                    if (rel.StartsWith("DEV\\", StringComparison.OrdinalIgnoreCase) || rel.IndexOf("\\DEV\\", StringComparison.OrdinalIgnoreCase) >= 0) continue; //game-written logs
                    files[rel] = (new FileInfo(file).Length, null);
                }
            }
            Walk(folder, "");
            Walk(folder + "_PATCH", "_PATCH\\");
            return files;
        }

        private static string Md5(string path)
        {
            using (MD5 md5 = MD5.Create())
            using (FileStream s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20))
                return BitConverter.ToString(md5.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Which category a level file belongs to, for the files list.</summary>
        public static string CategoryOfFile(string relative)
        {
            string f = Path.GetFileName(relative).ToUpperInvariant();
            if (f.StartsWith("COMMANDS")) return "script";
            if (f.StartsWith("LEVEL_MODELS.PAK") || f.StartsWith("MORPH_TARGET")) return "models";
            if (f.StartsWith("LEVEL_MODELS.MTL") || f.StartsWith("LEVEL_MODELS.CST") || f.StartsWith("LEVEL_SHADERS")) return "materials";
            if (f.StartsWith("LEVEL_TEXTURES")) return "textures";
            if (f.StartsWith("MATERIAL_MAPPINGS")) return "mappings";
            if (f.StartsWith("MODELS.MVR") || f.StartsWith("REDS")) return "placements";
            if (f.StartsWith("LIGHTS")) return "lights";
            if (f.StartsWith("COLLISION") || f.StartsWith("RADIOSITY_COLLISION")) return "collision";
            if (f.StartsWith("PHYSICS")) return "physics";
            if (f.StartsWith("NAV_MESH") || f.StartsWith("COVER") || f.Contains("SPOTTING") || f.StartsWith("ASSAULT") || f.StartsWith("PATH_BARRIER") || f.StartsWith("TRAVERSAL")) return "navigation";
            if (f.StartsWith("SOUND") || f.StartsWith("SNDNODE")) return "sound";
            if (relative.ToUpperInvariant().Contains("\\TEXT\\") || relative.ToUpperInvariant().StartsWith("TEXT\\")) return "text";
            return "other";
        }

        private static DiffCategory FilesCategory(SideData a, SideData b)
        {
            DiffCategory category = new DiffCategory("files", "Files");
            if (a.Files == null || b.Files == null) { category.Skipped = "a side isn't on disk"; return category; }
            int same = 0;
            string folderA = a.Install == null ? null : Path.Combine(a.Install, "DATA", "ENV", a.LevelName.Replace('/', '\\'));
            string folderB = b.Install == null ? null : Path.Combine(b.Install, "DATA", "ENV", b.LevelName.Replace('/', '\\'));
            foreach (KeyValuePair<string, (long size, string hash)> f in b.Files.OrderBy(o => o.Key, StringComparer.OrdinalIgnoreCase))
            {
                string category1 = Title(CategoryOfFile(f.Key));
                if (!a.Files.TryGetValue(f.Key, out (long size, string hash) old)) { category.Item(DiffKind.Added, f.Key + " (" + f.Value.size + " bytes; " + category1 + ")", "file:" + f.Key); continue; }
                bool differs = old.size != f.Value.size;
                if (!differs && folderA != null && folderB != null)
                {
                    string pa = Real(folderA, f.Key), pb = Real(folderB, f.Key);
                    differs = Md5(pa) != Md5(pb);
                }
                if (!differs) { same++; continue; }
                category.Item(DiffKind.Changed, f.Key + " (" + old.size + " -> " + f.Value.size + " bytes; " + category1 + ")", "file:" + f.Key);
            }
            foreach (KeyValuePair<string, (long size, string hash)> f in a.Files)
                if (!b.Files.ContainsKey(f.Key)) category.Item(DiffKind.Removed, f.Key + " (" + f.Value.size + " bytes; " + Title(CategoryOfFile(f.Key)) + ")", "file:" + f.Key);
            category.Summary = (category.HasDifferences ? category.Line() + ", " : "") + same + " identical";
            return category;
        }
        private static string Real(string folder, string relative) => relative.StartsWith("_PATCH\\", StringComparison.OrdinalIgnoreCase) ? folder + "_PATCH\\" + relative.Substring(7) : Path.Combine(folder, relative);

        private static DiffCategory Configs(SideData a, SideData b)
        {
            DiffCategory category = new DiffCategory("configs", "Configs");
            if (a.Install == null || b.Install == null) { category.Skipped = "a side isn't in a game folder"; return category; }
            if (string.Equals(Path.GetFullPath(a.Install).TrimEnd('\\'), Path.GetFullPath(b.Install).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            { category.Skipped = "both levels are in the same game folder, so they share its configs"; return category; }
            string dataA = Path.Combine(a.Install, "DATA"), dataB = Path.Combine(b.Install, "DATA");
            if (!Directory.Exists(dataA) || !Directory.Exists(dataB)) { category.Skipped = "a game folder has no DATA"; return category; }
            List<string> files = Directory.GetFiles(dataB, "*.BML", SearchOption.AllDirectories).Select(o => o.Substring(dataB.Length + 1))
                .Concat(Directory.GetFiles(dataB, "*.XML", SearchOption.TopDirectoryOnly).Select(o => o.Substring(dataB.Length + 1)))
                .Where(o => !o.StartsWith("ENV\\", StringComparison.OrdinalIgnoreCase) && !o.StartsWith("MODTOOLS\\", StringComparison.OrdinalIgnoreCase))
                .OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
            int same = 0;
#if !ENABLE_MOD_PACKAGES
            category.Skipped = "this build has no config reader (ENABLE_MOD_PACKAGES)";
            return category;
#else
            foreach (string rel in files)
            {
                string pa = Path.Combine(dataA, rel), pb = Path.Combine(dataB, rel);
                string normalised = "DATA/" + rel.Replace('\\', '/').ToUpperInvariant();
                if (!File.Exists(pa)) { category.Item(DiffKind.Added, normalised, "config:" + normalised); continue; }
                byte[] ba = File.ReadAllBytes(pa), bb = File.ReadAllBytes(pb);
                if (ba.SequenceEqual(bb)) { same++; continue; }
                XmlDocument da = OpenCAGE.Modding.ConfigDiff.Read(normalised, ba), db = OpenCAGE.Modding.ConfigDiff.Read(normalised, bb);
                if (da == null || db == null) { category.Item(DiffKind.Changed, normalised + " (bytes differ; unreadable as a config)", "config:" + normalised); continue; }
                List<OpenCAGE.Modding.BmlPatchOp> ops = OpenCAGE.Modding.BmlPatch.Diff(da, db);
                if (ops.Count == 0) { same++; continue; }
                DiffNode node = category.Item(DiffKind.Changed, normalised + " (" + ops.Count + " values)", "config:" + normalised);
                foreach (OpenCAGE.Modding.BmlPatchOp op in ops.Take(80)) node.Add(DiffKind.Changed, OpenCAGE.Modding.BmlPatch.Describe(op, da));
                if (ops.Count > 80) node.Add(DiffKind.Info, "... and " + (ops.Count - 80) + " more");
            }
            category.Summary = (category.HasDifferences ? category.Line() + ", " : "") + same + " the same";
            return category;
#endif
        }
        #endregion
    }
}
