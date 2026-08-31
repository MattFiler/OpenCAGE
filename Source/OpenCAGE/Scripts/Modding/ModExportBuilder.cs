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
        /// Include a file, together with its .META sidecar if it has one. Vanilla files are
        /// skipped silently - they carry nothing.
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
            if (!normalisedPath.EndsWith(SidecarSuffix, StringComparison.OrdinalIgnoreCase))
                Include(normalisedPath + SidecarSuffix);
        }

        /// <summary>The sidecar that ships with a file, or null if it has none on disk.</summary>
        public static string SidecarFor(string normalisedPath)
        {
            if (normalisedPath == null || normalisedPath.EndsWith(SidecarSuffix, StringComparison.OrdinalIgnoreCase))
                return null;
            return normalisedPath + SidecarSuffix;
        }

        public static bool IsSidecar(string normalisedPath)
        {
            return normalisedPath != null && normalisedPath.EndsWith(SidecarSuffix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The file a sidecar belongs to, or null if this isn't a sidecar.</summary>
        public static string SidecarParent(string normalisedPath)
        {
            return IsSidecar(normalisedPath)
                ? normalisedPath.Substring(0, normalisedPath.Length - SidecarSuffix.Length)
                : null;
        }

        private void Include(string normalisedPath)
        {
            byte[] hash = _cache.Hash(normalisedPath);
            if (hash == null || _manifest.IsVanilla(normalisedPath, hash))
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

        public ExportResult Write(string outputPath)
        {
            ExportResult result = new ExportResult();
            ModPackageWriter writer = new ModPackageWriter(Info);

            foreach (string path in _files.OrderBy(o => o))
            {
                string fullPath = ModToolkit.Denormalise(_gameRoot, path);
                byte[] content = File.ReadAllBytes(fullPath);
                string targetSha = ModToolkit.ToHex(ModToolkit.Sha256(content));

                byte[] vanilla = _installer == null ? null : _installer.VanillaBytes(path);
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

            foreach (KeyValuePair<string, List<BmlPatchOp>> config in _configs.OrderBy(o => o.Key))
            {
                byte[] payload = Encoding.UTF8.GetBytes(BmlPatch.Serialise(config.Value, config.Key));
                byte[] vanilla;
                _configVanilla.TryGetValue(config.Key, out vanilla);
                writer.Add(ModPackageEntry.KindBml, config.Key, payload,
                    vanilla == null ? null : ModToolkit.ToHex(ModToolkit.Sha256(vanilla)), null, payload.Length,
                    config.Value.Select(o => o.Claim).ToList());
                result.Entries.Add(new ExportEntrySummary() { Target = config.Key, Kind = ModPackageEntry.KindBml, PayloadSize = payload.Length, FileSize = payload.Length });
            }

            writer.Write(outputPath);
            result.PackageSize = new FileInfo(outputPath).Length;
            return result;
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
        public static List<BmlPatchOp> Diff(string gameRoot, string normalisedPath, byte[] vanillaBytes)
        {
            try
            {
                string fullPath = ModToolkit.Denormalise(gameRoot, normalisedPath);
                if (vanillaBytes == null || !File.Exists(fullPath))
                    return null;

                XmlDocument vanilla = new BML(vanillaBytes).Content;
                XmlDocument current = new BML(File.ReadAllBytes(fullPath)).Content;
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
