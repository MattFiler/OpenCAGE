#if ENABLE_MOD_PACKAGES
using CATHODE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace OpenCAGE.Modding
{
    public class ExportEntrySummary
    {
        public string Target;
        public string Kind;
        public long PayloadSize;
        public long FileSize;
    }

    public class ExportResult
    {
        public List<ExportEntrySummary> Entries = new List<ExportEntrySummary>();
        public List<string> Warnings = new List<string>();
        public long PackageSize;
    }

    /* Turns "what the user changed" into a package. Every included file that differs from vanilla
     * ships either whole or - when the baseline store holds the pristine bytes and the patch is
     * worth it - as a delta against them. Config files ship as the changed values themselves. */
    public class ModExportBuilder
    {
        private readonly string _gameRoot;
        private readonly VanillaManifest _manifest;
        private readonly HashCache _cache;
        private readonly ModInstaller _installer;

        public ModPackageInfo Info = new ModPackageInfo();

        private readonly List<string> _files = new List<string>();
        private readonly Dictionary<string, List<BmlPatchOp>> _configs = new Dictionary<string, List<BmlPatchOp>>();
        private readonly Dictionary<string, byte[]> _configVanilla = new Dictionary<string, byte[]>();

        public ModExportBuilder(string gameRoot, VanillaManifest manifest, HashCache cache, ModInstaller installer)
        {
            _gameRoot = gameRoot;
            _manifest = manifest;
            _cache = cache;
            _installer = installer;
            Info.Id = Guid.NewGuid().ToString("N");
            Info.CreatedUtc = DateTime.UtcNow;
            Info.HashSet = manifest.Platform;
        }

        /// <summary>
        /// The OpenCAGE sidecar suffix: the Commands custom tables, a level's radiosity ownership
        /// marker. See <see cref="CathodeLib.CustomTable"/> and <see cref="CATHODE.RadiosityRuntime"/>.
        /// </summary>
        private const string SidecarSuffix = ".META";

        /// <summary>
        /// Include a file, together with its sidecar: its .META if it has one (an animation PAK carries its tree layouts
        /// inside it). Vanilla files are skipped silently - they carry nothing.
        /// </summary>
        /// <remarks>
        /// The sidecar is the other half of the file it sits beside, not an optional extra, so it
        /// is not something a caller can forget or a user can untick: a package carrying
        /// COMMANDS.BIN without its tables, or a regenerated level without the marker saying the
        /// lighting is ours, installs something subtly wrong rather than something obviously
        /// missing. Enforced here rather than in the exporter UI so every caller gets it.
        /// </remarks>
        public void AddFile(string normalisedPath)
        {
            Include(normalisedPath);
            foreach (string sidecar in SidecarsFor(normalisedPath))
                Include(sidecar);
        }

        /// <summary>The sidecars that ship with a file (whether or not they're on disk): its .META.</summary>
        public static IEnumerable<string> SidecarsFor(string normalisedPath)
        {
            if (normalisedPath == null)
                yield break;
            if (!normalisedPath.EndsWith(SidecarSuffix, StringComparison.OrdinalIgnoreCase))
                yield return normalisedPath + SidecarSuffix;
        }

        public static bool IsSidecar(string normalisedPath)
        {
            return normalisedPath != null && normalisedPath.EndsWith(SidecarSuffix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The file a sidecar ships with, or none if this isn't a sidecar.</summary>
        public static IEnumerable<string> SidecarParents(string normalisedPath)
        {
            if (normalisedPath == null)
                return new string[0];
            if (normalisedPath.EndsWith(SidecarSuffix, StringComparison.OrdinalIgnoreCase))
                return new[] { normalisedPath.Substring(0, normalisedPath.Length - SidecarSuffix.Length) };
            return new string[0];
        }

        /// <summary>
        /// Whether a changed file goes in with another of the changed files rather than as a choice of its own: a
        /// sidecar whose parent is among them. A sidecar whose parent is unchanged has nothing to go in with.
        /// </summary>
        public static bool ShipsWithParent(string normalisedPath, ICollection<string> changed)
        {
            return IsSidecar(normalisedPath) && SidecarParents(normalisedPath).Any(changed.Contains);
        }

        private readonly List<string> _deleted = new List<string>();
        /* Files whose content isn't what's on disk: the user's own version of a file combined with installed mods there */
        private readonly Dictionary<string, byte[]> _content = new Dictionary<string, byte[]>();

        private void Include(string normalisedPath)
        {
            //Worked out again after every install - never carried
            if (ModToolkit.IsRegenerated(normalisedPath) || ModToolkit.IsExcluded(normalisedPath))
                return;
            //Under installed mods, the user's work is their own version, not the combined file
            if (_installer != null && _installer.TryGetOwnVersion(normalisedPath, out byte[] own))
            {
                if (own == null)
                {
                    if (_manifest.Contains(normalisedPath) && !_deleted.Contains(normalisedPath))
                        _deleted.Add(normalisedPath);
                }
                else if (!_manifest.IsVanilla(normalisedPath, ModToolkit.Sha256(own)))
                {
                    _content[normalisedPath] = own;
                    if (!_files.Contains(normalisedPath))
                        _files.Add(normalisedPath);
                }
                return;
            }
            byte[] hash = _cache.Hash(normalisedPath);
            if (hash == null)
            {
                //A shipped file that's gone: the mod deletes it
                if (_manifest.Contains(normalisedPath) && !_deleted.Contains(normalisedPath))
                    _deleted.Add(normalisedPath);
                return;
            }
            if (_manifest.IsVanilla(normalisedPath, hash))
                return;
            if (!_files.Contains(normalisedPath))
                _files.Add(normalisedPath);
        }

        public void AddFiles(IEnumerable<string> normalisedPaths)
        {
            foreach (string path in normalisedPaths)
                AddFile(path);
        }

        /// <summary>
        /// Include a config file as its changed values. Pass the ops the user selected (from
        /// DiffConfig) and the vanilla bytes they were diffed against.
        /// </summary>
        public void AddConfigPatch(string normalisedPath, List<BmlPatchOp> ops, byte[] vanillaBytes)
        {
            if (ops == null || ops.Count == 0)
                return;
            _configs[ModToolkit.Normalise(normalisedPath)] = ops;
            _configVanilla[ModToolkit.Normalise(normalisedPath)] = vanillaBytes;
        }

        /// <summary>A picture to show for the mod in the Mod Manager (PNG bytes), or null.</summary>
        public byte[] Preview;

        public ExportResult Write(string outputPath)
        {
            ExportResult result = new ExportResult();
            ModPackageWriter writer = new ModPackageWriter(Info);
            writer.SetPreview(Preview);
            if (string.IsNullOrWhiteSpace(Info.Summary))
                Info.Summary = Summarise();

            foreach (string path in _files.OrderBy(o => o))
            {
                byte[] content = _content.TryGetValue(path, out byte[] own) ? own : File.ReadAllBytes(ModToolkit.Denormalise(_gameRoot, path));
                AddContent(writer, result, path, content, _installer == null ? null : _installer.VanillaBytes(path));
            }

            //Configs ship as the finished file - vanilla with the chosen values changed - which the installer can
            //merge with any other mod's version of the same config
            foreach (KeyValuePair<string, List<BmlPatchOp>> config in _configs.OrderBy(o => o.Key))
            {
                _configVanilla.TryGetValue(config.Key, out byte[] vanilla);
                if (vanilla == null)
                    throw new Exception("No original copy of " + config.Key + " to build its changed values on.");
                XmlDocument document = new BML(vanilla).Content;
                BmlPatch.Apply(document, config.Value);
                AddContent(writer, result, config.Key, Merging.FileMerger.WriteBml(vanilla, document), vanilla);
            }

            foreach (string path in _deleted.OrderBy(o => o))
            {
                writer.AddDelete(path);
                result.Entries.Add(new ExportEntrySummary() { Target = path, Kind = ModPackageEntry.KindDelete });
            }

            writer.Write(outputPath);
            result.PackageSize = new FileInfo(outputPath).Length;
            return result;
        }

        /// <summary>
        /// In words, what the package changes: which levels, how many config values, which other files. Shown
        /// before installing; the author can write their own.
        /// </summary>
        public string Summarise()
        {
            List<string> parts = new List<string>();
            List<string> all = _files.Concat(_configs.Keys).Concat(_deleted).ToList();
            List<string> levels = all.Select(ModInstaller.LevelUnitOf).Where(o => o != null).Distinct().OrderBy(o => o).ToList();
            if (levels.Count != 0)
                parts.Add("Changes the level" + (levels.Count == 1 ? " " : "s ") + string.Join(", ", levels.Select(o => o.Replace("DLC/", ""))));
            //Settings by what they're for; behaviour trees by tree (the tree directory is one config file, but a tree is a tree)
            List<KeyValuePair<string, List<BmlPatchOp>>> settings = _configs.Where(o => !o.Key.StartsWith("DATA/BINARY_BEHAVIOR/")).ToList();
            int values = settings.Sum(o => o.Value.Count);
            if (values != 0)
                parts.Add("changes " + values + " setting" + (values == 1 ? "" : "s") + " for " + string.Join(", ", settings.Select(o => FriendlyConfigName(o.Key)).Distinct()));
            List<BmlPatchOp> treeOps = _configs.Where(o => o.Key.StartsWith("DATA/BINARY_BEHAVIOR/")).SelectMany(o => o.Value).ToList();
            if (treeOps.Count != 0)
            {
                int added = treeOps.Count(o => o.Kind == "add" && o.Path.IndexOf('/') < 0);
                int changed = treeOps.Where(o => !(o.Kind == "add" && o.Path.IndexOf('/') < 0)).Select(BmlPatch.TreeOf).Distinct().Count();
                List<string> trees = new List<string>();
                if (changed != 0) trees.Add("changes " + changed + " behaviour tree" + (changed == 1 ? "" : "s"));
                if (added != 0) trees.Add("adds " + added + " new " + (changed != 0 ? "one" : "behaviour tree") + (added == 1 ? "" : "s"));
                parts.Add(string.Join(" and ", trees));
            }
            List<string> others = all.Where(o => ModInstaller.LevelUnitOf(o) == null && !_configs.ContainsKey(o)).ToList();
            if (others.Any(o => o.StartsWith("DATA/BINARY_BEHAVIOR/"))) parts.Add("changes AI behaviour trees");
            if (others.Any(o => o.StartsWith("DATA/TEXT/"))) parts.Add("changes in-game text");
            if (others.Any(o => o.StartsWith("DATA/SOUND/"))) parts.Add("changes sounds");
            if (others.Any(o => o == "DATA/UI.PAK")) parts.Add("changes the user interface");
            if (others.Any(o => o.StartsWith("DATA/GLOBAL/"))) parts.Add("changes animations");
            int rest = others.Count(o => !o.StartsWith("DATA/BINARY_BEHAVIOR/") && !o.StartsWith("DATA/TEXT/") && !o.StartsWith("DATA/SOUND/") && o != "DATA/UI.PAK" && !o.StartsWith("DATA/GLOBAL/"));
            if (rest != 0) parts.Add("changes " + rest + " other game file" + (rest == 1 ? "" : "s"));
            if (parts.Count == 0) return "";
            string text = string.Join("; ", parts) + ".";
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>What a config file sets things for, in a player's words ("changes 2 settings for the alien").</summary>
        public static string FriendlyConfigName(string normalisedPath)
        {
            string p = normalisedPath.ToUpperInvariant();
            if (p.StartsWith("DATA/ALIENCONFIGS/")) return "the alien";
            if (p.StartsWith("DATA/BINARY_BEHAVIOR/")) return "the AI's behaviour trees";
            if (p.StartsWith("DATA/CHR_INFO/")) return "characters";
            if (p.StartsWith("DATA/DIFFICULTYSETTINGS/")) return "the difficulty levels";
            if (p.StartsWith("DATA/VIEW_CONE_SETS/")) return "what characters can see";
            if (p.StartsWith("DATA/WEAPON_INFO/")) return "weapons and ammo";
            if (p.StartsWith("DATA/AIM_IK_DATA/")) return "aiming";
            if (p.StartsWith("DATA/AWARDS/")) return "achievements";
            if (p == "DATA/GBL_ITEM.BML") return "items";
            if (p == "DATA/GLOBALCONSTANTS.BML") return "the whole game";
            if (p == "DATA/COVERANDTRAVERSALRULES.BML") return "cover and movement";
            if (p == "DATA/FONT_CONFIG.BML") return "fonts";
            if (p == "DATA/LEVEL_TEXT_DATABASES.BML") return "level text";
            if (p == "DATA/MATERIAL_DATA/MATERIALS.BML") return "surface materials";
            return p.Substring(p.LastIndexOf('/') + 1);
        }

        /* One finished file into the package: a patch from the vanilla bytes when that's smaller, else whole */
        private void AddContent(ModPackageWriter writer, ExportResult result, string path, byte[] content, byte[] vanilla)
        {
            string targetSha = ModToolkit.ToHex(ModToolkit.Sha256(content));
            string sourceAlso = null;
            if (vanilla != null)
            {
                /* A level texture pak that absorbed the global textures holds ~80MB of bytes
                 * that came straight out of the global pak - extend the delta source with the
                 * pristine global bytes so those become copies, not literals */
                string globalPak = GlobalTexturesPath(path);
                if (globalPak != null)
                {
                    if (_installer != null)
                        _installer.CaptureVanillaBaseline(globalPak);
                    byte[] globalVanilla = _installer == null ? null : _installer.VanillaBytes(globalPak);
                    if (globalVanilla != null)
                    {
                        vanilla = Concat(vanilla, globalVanilla);
                        sourceAlso = globalPak;
                    }
                }
            }

            byte[] delta = null;
            if (vanilla != null)
                delta = DeltaCodec.Encode(vanilla, content, (long)(content.Length * 0.6));

            if (delta != null)
            {
                writer.Add(ModPackageEntry.KindDelta, path, delta,
                    ModToolkit.ToHex(ModToolkit.Sha256(vanilla)), targetSha, content.Length, null, sourceAlso);
                result.Entries.Add(new ExportEntrySummary() { Target = path, Kind = ModPackageEntry.KindDelta, PayloadSize = delta.Length, FileSize = content.Length });
            }
            else
            {
                if (vanilla == null && _manifest.Contains(path))
                    result.Warnings.Add(path + " ships whole (" + PrettySize(content.Length) + ") - no pristine copy of it is stored to diff against.");
                writer.Add(ModPackageEntry.KindFile, path, content, null, targetSha, content.Length);
                result.Entries.Add(new ExportEntrySummary() { Target = path, Kind = ModPackageEntry.KindFile, PayloadSize = content.Length, FileSize = content.Length });
            }
        }

        public const string GlobalTexturesPak = "DATA/ENV/GLOBAL/WORLD/GLOBAL_TEXTURES.ALL.PAK";

        /// <summary>
        /// The global pak to extend a delta source with, for targets that absorb global textures -
        /// or null when the target isn't one of those.
        /// </summary>
        public static string GlobalTexturesPath(string normalisedTarget)
        {
            if (!normalisedTarget.StartsWith("DATA/ENV/PRODUCTION/"))
                return null;
            string fileName = normalisedTarget.Substring(normalisedTarget.LastIndexOf('/') + 1);
            return fileName.StartsWith("LEVEL_TEXTURES") ? GlobalTexturesPak : null;
        }

        public static byte[] Concat(byte[] a, byte[] b)
        {
            byte[] result = new byte[a.Length + b.Length];
            Array.Copy(a, result, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }

        public static string PrettySize(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.0") + " GB";
            if (bytes >= 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            if (bytes >= 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return bytes + " B";
        }
    }

    /* The value-level view of a changed config file, shared by the exporter UI and tests */
    public static class ConfigDiff
    {
        /// <summary>
        /// The ops that separate the current config from vanilla, or null when the vanilla bytes
        /// aren't obtainable or either side fails to parse.
        /// </summary>
        public static List<BmlPatchOp> Diff(string gameRoot, string normalisedPath, byte[] vanillaBytes, ModInstaller installer = null)
        {
            try
            {
                //Under installed mods, the user's changes are their own version, not the combined file
                byte[] currentBytes = null;
                if (installer == null || !installer.TryGetOwnVersion(normalisedPath, out currentBytes))
                {
                    string fullPath = ModToolkit.Denormalise(gameRoot, normalisedPath);
                    currentBytes = File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
                }
                if (vanillaBytes == null || currentBytes == null)
                    return null;

                XmlDocument vanilla = new BML(vanillaBytes).Content;
                XmlDocument current = new BML(currentBytes).Content;
                if (vanilla == null || current == null)
                    return null;
                return BmlPatch.Diff(vanilla, current);
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
