using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;

namespace OpenCAGE
{
    /// <summary>
    /// Who uses each record of a global configuration table, and which records nothing uses, so a "per-weapon" tuning doesn't silently
    /// change the base game. Ammo types (WEAPON_INFO\AMMO\AMMOTYPES.BML = the engine's fixed AMMO_TYPE enum: no record can be added, so a
    /// new ammo needs a spare one) and character attributes (CHR_INFO\ATTRIBUTES\ATTRIBUTES.BML, named by Character.attribute_set).
    /// A record is used by records built on it (Template_Name), GBL_ITEM's ammo items (ammo_type) and every level's script (an AMMO_TYPE
    /// value or an attribute_set name); a composite pin's default counts only where an instance relies on it. Level scans are cached
    /// until the script changes; the open level is read as it is now.
    /// </summary>
    public static class ConfigRecords
    {
        public enum Kind { Ammo, Attributes }

        public sealed class Record
        {
            public Kind Kind;
            public int Id;
            public string Name;
            /// <summary>Its file, relative to DATA.</summary>
            public string File;
            /// <summary>The record it is built on (Template_Name), or null.</summary>
            public string Template;
            /// <summary>Records built on it, however indirectly (they take every value they don't set themselves from it).</summary>
            public List<string> InheritedBy = new List<string>();
        }

        public sealed class Use
        {
            /// <summary>A level ("PRODUCTION/BSP_TORRENS"), or "GBL_ITEM" for an item, or "template" for a record built on it.</summary>
            public string Where;
            public string Composite;
            public string Entity;
            public string Type;
            public string Parameter;
            /// <summary>False for a composite pin's default that every instance of the composite overrides: it reaches nothing.</summary>
            public bool Effective = true;
            public string Note;

            public override string ToString() => Where + (Composite == null ? "" : ": " + Composite + " / " + Entity + " (" + Type + ")" + (Parameter == null ? "" : "." + Parameter)) + (Note == null ? "" : " - " + Note) + (Effective ? "" : " [not reached]");
        }

        public sealed class Report
        {
            public Kind Kind;
            public List<Record> Records = new List<Record>();
            public Dictionary<string, List<Use>> Uses = new Dictionary<string, List<Use>>(StringComparer.OrdinalIgnoreCase);
            public List<string> LevelsRead = new List<string>();
            public List<string> LevelsFailed = new List<string>();

            public List<Use> UsesOf(string record) => Uses.TryGetValue(record, out List<Use> list) ? list : new List<Use>();

            /// <summary>Nothing reaches it: no item, no script value that takes effect, and no record built on it is used.</summary>
            public bool IsSpare(Record record) => !UsesOf(record.Name).Any(o => o.Effective) && record.InheritedBy.All(o => !UsesOf(o).Any(u => u.Effective));

            public List<Record> Spare => Records.Where(IsSpare).ToList();
        }

        /// <summary>What the game's own code may use a record for without any data naming it, so "spare" is said with care.</summary>
        public static string EngineNote(Kind kind) => kind == Kind.Ammo
            ? "The game's code can still use a record no data names (PUSH, a shove, has no script use either), so check a spare record in game before you take it."
            : "The game's code can still use a record no data names (DEFAULTS is every record's base), so check a spare record in game before you take it.";

        private static string Table(Kind kind) => kind == Kind.Ammo ? "WEAPON_INFO\\AMMO\\AMMOTYPES.BML" : "CHR_INFO\\ATTRIBUTES\\ATTRIBUTES.BML";
        private static string Folder(Kind kind) => kind == Kind.Ammo ? "WEAPON_INFO\\AMMO\\" : "CHR_INFO\\ATTRIBUTES\\";
        private static string RootElement(Kind kind) => kind == Kind.Ammo ? "Ammo" : "Attribute";

        private static XmlDocument LoadBml(string path)
        {
            if (!System.IO.File.Exists(path)) return null;
            BML bml = new BML(path);
            return bml.Loaded ? bml.Content : null;
        }

        /// <summary>The table's records, with their templates and what is built on each.</summary>
        public static List<Record> Records(string gameRoot, Kind kind)
        {
            string data = Path.Combine(gameRoot, "DATA");
            XmlDocument table = LoadBml(Path.Combine(data, Table(kind)));
            List<Record> records = new List<Record>();
            if (table?.DocumentElement == null) return records;
            foreach (XmlElement row in table.DocumentElement.ChildNodes.OfType<XmlElement>())
            {
                string name = row["Name"]?.InnerText;
                if (string.IsNullOrWhiteSpace(name)) continue;
                int.TryParse(row["ID"]?.InnerText, out int id);
                Record record = new Record() { Kind = kind, Id = id, Name = name.Trim(), File = Folder(kind) + name.Trim() + ".BML" };
                XmlDocument file = LoadBml(Path.Combine(data, record.File));
                string template = file?.DocumentElement?["Template_Name"]?.InnerText;
                record.Template = string.IsNullOrWhiteSpace(template) ? null : template.Trim();
                records.Add(record);
            }
            foreach (Record record in records)
            {
                //Walk up from every other record: those reaching this one are built on it
                foreach (Record other in records)
                {
                    if (other == record) continue;
                    string up = other.Template;
                    for (int guard = 0; up != null && guard < 32; guard++)
                    {
                        if (string.Equals(up, record.Name, StringComparison.OrdinalIgnoreCase)) { record.InheritedBy.Add(other.Name); break; }
                        up = records.FirstOrDefault(o => string.Equals(o.Name, up, StringComparison.OrdinalIgnoreCase))?.Template;
                    }
                }
            }
            return records;
        }

        #region Scanning a level's script
        /// <summary>One level's uses of either table, as read from its script (kept on disk until the script changes).</summary>
        public sealed class LevelUses
        {
            public const int FormatVersion = 2;
            public int Format = FormatVersion;
            public long Written, Length;
            /// <summary>Ammo uses by AMMO_TYPE index, attribute uses by name (upper case).</summary>
            public List<RawUse> Uses = new List<RawUse>();
        }

        public sealed class RawUse
        {
            public Kind Kind;
            public string Value;
            public string Composite, Entity, Type, Parameter;
            /// <summary>A composite pin's default: how many instances of the composite there are, and how many set the pin themselves.</summary>
            public bool PinDefault;
            public int Instances, Overriding;
        }

        private static readonly ShortGuid AmmoEnum = ShortGuidUtils.Generate("AMMO_TYPE");
        private static readonly ShortGuid AttributeSet = ShortGuidUtils.Generate("attribute_set");
        private static readonly ShortGuid CharacterClassEnum = ShortGuidUtils.Generate("CHARACTER_CLASS");
        /// <summary>CHARACTER_CLASS values and the attribute set of that name (the game may pick a set by class as well as by attribute_set).</summary>
        private static readonly Dictionary<int, string> ClassSets = new Dictionary<int, string>()
        {
            { 0, "THE_PLAYER" }, { 1, "ALIEN" }, { 2, "ANDROID" }, { 3, "CIVILIAN" }, { 4, "SECURITY_GUARD" }, { 5, "FACEHUGGER" },
            { 6, "INNOCENT" }, { 7, "ANDROID_HEAVY" }, { 9, "MELEE_HUMAN" },
        };

        /// <summary>Every use of either table in a level's script.</summary>
        public static LevelUses Scan(Commands commands, CancellationToken cancel = default(CancellationToken))
        {
            LevelUses found = new LevelUses();
            //Instances per composite, and per (composite, pin) how many set it themselves or feed it by a link
            Dictionary<ShortGuid, List<(Composite holder, FunctionEntity instance)>> instances = new Dictionary<ShortGuid, List<(Composite, FunctionEntity)>>();
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null) continue;
                foreach (FunctionEntity function in composite.functions)
                {
                    if (function.function.IsFunctionType) continue;
                    if (!instances.TryGetValue(function.function, out var list)) instances[function.function] = list = new List<(Composite, FunctionEntity)>();
                    list.Add((composite, function));
                }
            }
            foreach (Composite composite in commands.Entries)
            {
                cancel.ThrowIfCancellationRequested();
                if (composite == null) continue;
                foreach (Entity entity in composite.GetEntities())
                {
                    foreach (Parameter parameter in entity.parameters)
                    {
                        Kind kind;
                        string value;
                        if (parameter.content is cEnum enumValue && enumValue.enumID == AmmoEnum)
                        {
                            if (enumValue.enumIndex < 0) continue;
                            kind = Kind.Ammo;
                            value = enumValue.enumIndex.ToString();
                        }
                        else if (parameter.content is cString text && parameter.name == AttributeSet && !string.IsNullOrWhiteSpace(text.value))
                        {
                            kind = Kind.Attributes;
                            value = text.value.Trim().ToUpperInvariant();
                        }
                        else if (parameter.content is cEnum classValue && classValue.enumID == CharacterClassEnum && ClassSets.TryGetValue(classValue.enumIndex, out string set))
                        {
                            //A character's class names a set of the same name: counted as a use, so no set a class may pick is called spare
                            kind = Kind.Attributes;
                            value = set;
                        }
                        else
                            continue;
                        RawUse use = new RawUse()
                        {
                            Kind = kind,
                            Value = value,
                            Composite = composite.name,
                            Entity = commands.Utils.GetEntityName(composite, entity) ?? entity.shortGUID.ToByteString(),
                            Type = TypeOf(commands, entity),
                            Parameter = ShortGuidUtils.FindString(parameter.name),
                        };
                        //A composite's own pin: its value is the default its instances start from
                        if (entity is VariableEntity variable && variable.name == parameter.name)
                        {
                            use.PinDefault = true;
                            List<(Composite holder, FunctionEntity instance)> placed = instances.TryGetValue(composite.shortGUID, out var list) ? list : new List<(Composite, FunctionEntity)>();
                            use.Instances = placed.Count;
                            use.Overriding = placed.Count(o => o.instance.GetParameter(variable.name) != null || o.holder.GetEntities().Any(e => e.childLinks.Any(l => l.linkedEntityID == o.instance.shortGUID && l.linkedParamID == variable.name)));
                        }
                        found.Uses.Add(use);
                    }
                }
            }
            return found;
        }

        private static string TypeOf(Commands commands, Entity entity)
        {
            if (entity is FunctionEntity function)
                return function.function.IsFunctionType ? function.function.AsFunctionType.ToString() : "Instance of " + (commands.GetComposite(function.function)?.name ?? "?");
            if (entity is VariableEntity) return "pin of the composite";
            return entity.variant.ToString().ToLowerInvariant();
        }

        private static readonly Dictionary<string, LevelUses> _memory = new Dictionary<string, LevelUses>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        private static string CacheFile(string level) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenCAGE", "config_uses", "v" + LevelUses.FormatVersion, level.Replace('/', '_').Replace('\\', '_') + ".json");

        /// <summary>A level's uses from its saved script: from memory or the disk cache while the script is unchanged, else read now.</summary>
        public static LevelUses ForLevel(string gameRoot, string level, CancellationToken cancel = default(CancellationToken))
        {
            string path = Path.Combine(gameRoot, "DATA", "ENV", level.Replace('/', '\\'), "WORLD", "COMMANDS.PAK");
            FileInfo file = new FileInfo(path);
            if (!file.Exists) throw new FileNotFoundException("No script file", path);
            long written = file.LastWriteTimeUtc.Ticks, length = file.Length;
            string key = gameRoot + "|" + level;
            lock (_lock)
                if (_memory.TryGetValue(key, out LevelUses kept) && kept.Written == written && kept.Length == length) return kept;
            string cache = CacheFile(level);
            LevelUses uses = null;
            try
            {
                if (System.IO.File.Exists(cache))
                {
                    LevelUses stored = JsonConvert.DeserializeObject<LevelUses>(System.IO.File.ReadAllText(cache));
                    if (stored != null && stored.Format == LevelUses.FormatVersion && stored.Written == written && stored.Length == length) uses = stored;
                }
            }
            catch { }
            if (uses == null)
            {
                string none = Path.Combine(Path.GetTempPath(), "OpenCAGE_no_file_" + Guid.NewGuid().ToString("N"));
                Commands commands = new Commands(path, new EnvironmentAnimations(none, null), new CollisionMaps(none, null, null), new RenderableElements(none, null, null));
                if (!commands.Loaded) throw new InvalidDataException("The script at " + path + " could not be read.");
                uses = Scan(commands, cancel);
                uses.Written = written;
                uses.Length = length;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cache));
                    System.IO.File.WriteAllText(cache, JsonConvert.SerializeObject(uses));
                }
                catch { }
            }
            lock (_lock)
                _memory[key] = uses;
            return uses;
        }
        #endregion

        /// <summary>
        /// The report for one table: its records, and every use found in GBL_ITEM, the records built on each, and the levels named
        /// (their saved scripts; the open level as it is now when <paramref name="openCommands"/> is given for <paramref name="openLevel"/>).
        /// </summary>
        public static Report Find(string gameRoot, Kind kind, IEnumerable<string> levels, Commands openCommands = null, string openLevel = null,
            Action<string> progress = null, CancellationToken cancel = default(CancellationToken))
        {
            Report report = new Report() { Kind = kind, Records = Records(gameRoot, kind) };
            void Add(string record, Use use)
            {
                if (!report.Uses.TryGetValue(record, out List<Use> list)) report.Uses[record] = list = new List<Use>();
                list.Add(use);
            }

            foreach (Record record in report.Records)
                foreach (string child in record.InheritedBy)
                    Add(record.Name, new Use() { Where = "template", Entity = child, Type = "record", Note = child + " is built on it (Template_Name): it takes every value it doesn't set itself", Effective = false });

            if (kind == Kind.Ammo)
            {
                XmlDocument items = LoadBml(Path.Combine(gameRoot, "DATA", "GBL_ITEM.BML"));
                foreach (XmlElement ammo in items?.GetElementsByTagName("ammo").OfType<XmlElement>() ?? Enumerable.Empty<XmlElement>())
                {
                    if (!int.TryParse(ammo.GetAttribute("ammo_type"), out int id)) continue;
                    Record record = report.Records.FirstOrDefault(o => o.Id == id);
                    if (record != null)
                        Add(record.Name, new Use() { Where = "GBL_ITEM", Entity = ammo.GetAttribute("name"), Type = "ammo item", Note = "the pickup '" + ammo.GetAttribute("name") + "' for the " + ammo.GetAttribute("target_weapon") });
                }
            }

            foreach (string level in (levels ?? Enumerable.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancel.ThrowIfCancellationRequested();
                LevelUses uses;
                try
                {
                    if (openCommands != null && string.Equals(level, openLevel, StringComparison.OrdinalIgnoreCase))
                        uses = Scan(openCommands, cancel);
                    else
                    {
                        progress?.Invoke("Reading " + level);
                        uses = ForLevel(gameRoot, level, cancel);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    report.LevelsFailed.Add(level + ": " + e.Message);
                    continue;
                }
                report.LevelsRead.Add(level);
                AddLevel(report, level, uses);
            }
            return report;
        }

        /// <summary>A level's uses (already read) into a report.</summary>
        public static void AddLevel(Report report, string level, LevelUses uses)
        {
            Kind kind = report.Kind;
            foreach (RawUse raw in uses.Uses.Where(o => o.Kind == kind))
            {
                Record record = kind == Kind.Ammo
                    ? report.Records.FirstOrDefault(o => o.Id.ToString() == raw.Value)
                    : report.Records.FirstOrDefault(o => string.Equals(o.Name, raw.Value, StringComparison.OrdinalIgnoreCase));
                string name = record?.Name ?? (kind == Kind.Ammo ? "AMMO_TYPE " + raw.Value : raw.Value);
                Use use = new Use() { Where = level, Composite = raw.Composite, Entity = raw.Entity, Type = raw.Type, Parameter = raw.Parameter };
                if (kind == Kind.Attributes && raw.Parameter == "character_class")
                    use.Note = "its character class names this set (the game may pick the set by class)";
                if (raw.PinDefault)
                {
                    use.Effective = raw.Instances > raw.Overriding;
                    use.Note = "the default of the composite's pin: " + raw.Overriding + " of its " + raw.Instances + " instance" + (raw.Instances == 1 ? "" : "s") + " set their own";
                }
                if (!report.Uses.TryGetValue(name, out List<Use> list)) report.Uses[name] = list = new List<Use>();
                list.Add(use);
            }
        }

        /// <summary>A report on one level whose uses are already read (the open level, scanned live).</summary>
        public static Report FindIn(string gameRoot, Kind kind, string level, LevelUses uses)
        {
            Report report = new Report() { Kind = kind, Records = Records(gameRoot, kind) };
            AddLevel(report, level, uses);
            report.LevelsRead.Add(level);
            return report;
        }

        /// <summary>The kind a table file belongs to, or null.</summary>
        public static Kind? KindOf(string text)
        {
            string t = (text ?? "").Trim().ToLowerInvariant();
            if (t == "ammo" || t == "ammo_type" || t == "ammotypes" || t == "weapon_ammo") return Kind.Ammo;
            if (t == "attributes" || t == "attribute" || t == "attribute_set" || t == "character_attributes") return Kind.Attributes;
            return null;
        }
    }
}
