#if ENABLE_MOD_PACKAGES
using CathodeLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace OpenCAGE.Modding
{
    /* The distributable: a manifest and a payload per changed file.
     *
     * Every entry says what the file holds with this mod installed on an unmodified game. "file" is the whole
     * file. "delta" is an ACDELTA1 patch from the vanilla bytes - it only applies on top of that exact source
     * hash, which keeps installs byte-faithful and packages small. "delete" is a file the mod removes. Combining
     * mods is the installer's business, not the package's: it merges these finished versions file by file.
     *
     * Format 1 packages also had "bml": a config's changed values, applied to the vanilla config to get the
     * finished file. Still read; no longer written. */
    public class ModPackageEntry
    {
        public const string KindFile = "file";
        public const string KindDelta = "delta";
        public const string KindDelete = "delete";
        public const string KindBml = "bml";

        [JsonProperty("target")] public string Target;
        [JsonProperty("kind")] public string Kind;
        [JsonProperty("payload")] public string Payload;
        [JsonProperty("sourceSha")] public string SourceShaHex; //delta: bytes the patch applies to; bml: the vanilla doc it was diffed against
        /* delta only, optional: a second file whose pristine bytes are appended to the delta
         * source. Lets a level texture pak that absorbed the global textures patch against
         * vanilla-level + vanilla-global instead of carrying 80MB the user already has. */
        [JsonProperty("sourceAlso")] public string SourceAlsoTarget;
        [JsonProperty("targetSha")] public string TargetShaHex; //file/delta: the exact result; bml: null (result depends on merge)
        [JsonProperty("targetSize")] public long TargetSize;
        [JsonProperty("claims")] public List<string> Claims = new List<string>();
    }

    public class ModPackageInfo
    {
        /* 3: a replacement mod ("installs": "replace") - an OpenCAGE before them would combine it with other mods. A
           combinable one is still written as 2, which every OpenCAGE with mods reads. */
        public const int CurrentFormat = 3;
        public const int CombinableFormat = 2;

        /// <summary>A combinable mod: combined with other mods (and the player's own changes) that change the same levels and files.</summary>
        public const string InstallsCombine = "combine";
        /// <summary>A replacement mod: installed exactly as it was made - whole levels and files, nothing rebuilt - so no other mod can change what it changes while it's on.</summary>
        public const string InstallsReplace = "replace";

        [JsonProperty("format")] public int Format = CurrentFormat;
        /* How it installs: InstallsCombine (packages from before there was a choice say nothing: that) or InstallsReplace */
        [JsonProperty("installs")] public string Installs = InstallsCombine;
        [JsonIgnore] public bool Replaces => Installs == InstallsReplace;
        /* What the mod changes, in words, written by the exporter: shown in the Mod Manager before installing */
        [JsonProperty("summary")] public string Summary;
        /* A picture for the Mod Manager (PNG payload name), or null */
        [JsonProperty("preview")] public string Preview;
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("description")] public string Description;
        [JsonProperty("author")] public string Author;
        [JsonProperty("version")] public string Version;
        [JsonProperty("createdUtc")] public DateTime CreatedUtc;
        [JsonProperty("opencage")] public string OpenCageVersion;
        // which build's vanilla bytes this was measured against - the deltas only apply on top of
        // those exact hashes. Serialized by name so the JSON does not depend on the enum's order.
        [JsonProperty("hashSet")] [JsonConverter(typeof(StringEnumConverter))] public PatchManager.Platform HashSet = PatchManager.Platform.STEAM;
        [JsonProperty("entries")] public List<ModPackageEntry> Entries = new List<ModPackageEntry>();

        [JsonIgnore]
        public List<string> Levels
        {
            get { return Entries.Select(o => ModToolkit.LevelOf(o.Target)).Where(o => o != null).Distinct().OrderBy(o => o).ToList(); }
        }
    }

    public class ModPackage
    {
        public ModPackageInfo Info { get; private set; }
        public string FilePath { get; private set; }

        private ModPackage() { }

        public static ModPackage Read(string filePath)
        {
            byte[] manifestJson = ReadArchiveEntry(filePath, "manifest.json");
            if (manifestJson == null)
                throw new Exception("Not a mod package: no manifest inside.");

            ModPackageInfo info = JsonConvert.DeserializeObject<ModPackageInfo>(System.Text.Encoding.UTF8.GetString(manifestJson));
            if (info == null || info.Entries == null || string.IsNullOrEmpty(info.Id))
                throw new Exception("The mod package's manifest is invalid.");
            if (info.Format < 1 || info.Format > ModPackageInfo.CurrentFormat)
                throw new Exception("The mod package uses format " + info.Format + ", which this version of OpenCAGE doesn't know. Update OpenCAGE.");
            //The id names the package's file in the library: it can't be a path
            if (!IsSafeName(info.Id))
                throw new Exception("The mod package's manifest is invalid.");
            //A mod changes the game's data and nothing else: a package naming any other file is damaged or unsafe, and isn't used at all
            foreach (ModPackageEntry entry in info.Entries)
            {
                if (entry == null)
                    throw new Exception("The mod package's manifest is invalid.");
                if (!IsSafeTarget(entry.Target) || (entry.SourceAlsoTarget != null && !IsSafeTarget(entry.SourceAlsoTarget)))
                    throw new Exception("The mod package names a file outside the game's DATA folder (" + (entry.Target ?? "nothing") + "), which no mod can change. It may be damaged or unsafe, so it can't be used.");
            }
            return new ModPackage() { Info = info, FilePath = filePath };
        }

        /// <summary>
        /// Whether a package's target is a file inside the game's DATA folder: relative, starting DATA/, with no empty, "." or
        /// ".." part (nor one Windows would read as one), no drive or stream colon, and no part Windows reads as a device.
        /// </summary>
        public static bool IsSafeTarget(string target)
        {
            if (string.IsNullOrEmpty(target))
                return false;
            string normalised = ModToolkit.Normalise(target);
            if (!normalised.StartsWith("DATA/", StringComparison.Ordinal) || normalised.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return false;
            foreach (string part in normalised.Split('/'))
                if (!IsSafeName(part))
                    return false;
            return true;
        }

        /* One name in a path: something other than dots and spaces, none of the characters a file name can't have (slashes, a
           colon), and not a device ("CON", "NUL.BML", "COM1") */
        private static bool IsSafeName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Trim(' ', '.').Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;
            string stem = name.Split('.')[0].Trim(' ').ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CLOCK$" || stem == "CONIN$" || stem == "CONOUT$")
                return false;
            //COM1-9 and LPT1-9, and the superscript 1, 2 and 3 Windows also takes for them
            if (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && (char.IsDigit(stem[3]) || stem[3] == (char)0xB9 || stem[3] == (char)0xB2 || stem[3] == (char)0xB3))
                return false;
            return true;
        }

        /// <summary>The preview picture's bytes, or null when the package has none.</summary>
        public byte[] ReadPreview()
        {
            if (string.IsNullOrEmpty(Info.Preview))
                return null;
            try { return ReadArchiveEntry(FilePath, Info.Preview); }
            catch { return null; }
        }

        public byte[] ReadPayload(ModPackageEntry entry)
        {
            byte[] payload = ReadArchiveEntry(FilePath, entry.Payload);
            if (payload == null)
                throw new Exception("The mod package is missing payload '" + entry.Payload + "'.");
            return payload;
        }

        /* One named entry out of a package, decompressed in memory. Packages are a PAK2 whose
         * entries are gzipped; the first packages ever built were zips, so those still read. */
        private static byte[] ReadArchiveEntry(string filePath, string entryName)
        {
            byte[] header = new byte[4];
            using (FileStream probe = File.OpenRead(filePath))
                probe.Read(header, 0, 4);

            if (header[0] == 'P' && header[1] == 'K')
            {
                using (ZipArchive zip = ZipFile.OpenRead(filePath))
                {
                    ZipArchiveEntry entry = zip.GetEntry(entryName);
                    if (entry == null)
                        return null;
                    using (MemoryStream result = new MemoryStream())
                    using (Stream stream = entry.Open())
                    {
                        stream.CopyTo(result);
                        return result.ToArray();
                    }
                }
            }

            CATHODE.PAK2 pak = new CATHODE.PAK2(File.ReadAllBytes(filePath));
            CATHODE.PAK2.File pakEntry = pak.Entries.FirstOrDefault(o => o.Filename == entryName);
            return pakEntry == null ? null : ModToolkit.Gunzip(pakEntry.Content);
        }
    }

    public class ModPackageWriter
    {
        private readonly ModPackageInfo _info;
        private readonly List<byte[]> _payloads = new List<byte[]>();

        public ModPackageWriter(ModPackageInfo info)
        {
            _info = info;
            _info.Entries.Clear();
        }

        public ModPackageEntry Add(string kind, string target, byte[] payload, string sourceShaHex, string targetShaHex, long targetSize, List<string> claims = null, string sourceAlsoTarget = null)
        {
            ModPackageEntry entry = new ModPackageEntry()
            {
                Kind = kind,
                Target = ModToolkit.Normalise(target),
                Payload = "payload/" + _payloads.Count.ToString("0000"),
                SourceShaHex = sourceShaHex,
                SourceAlsoTarget = sourceAlsoTarget,
                TargetShaHex = targetShaHex,
                TargetSize = targetSize,
            };
            entry.Claims = claims != null && claims.Count != 0 ? claims : new List<string>() { entry.Target };
            _info.Entries.Add(entry);
            _payloads.Add(payload);
            return entry;
        }

        /// <summary>A file the mod deletes.</summary>
        public ModPackageEntry AddDelete(string target)
        {
            ModPackageEntry entry = new ModPackageEntry()
            {
                Kind = ModPackageEntry.KindDelete,
                Target = ModToolkit.Normalise(target),
            };
            entry.Claims = new List<string>() { entry.Target };
            _info.Entries.Add(entry);
            _payloads.Add(null);
            return entry;
        }

        private byte[] _preview;

        /// <summary>A picture to show for the mod in the Mod Manager (PNG bytes).</summary>
        public void SetPreview(byte[] png)
        {
            _preview = png;
            _info.Preview = png == null ? null : "preview.png";
        }

        public void Write(string filePath)
        {
            //A PAK2 of gzipped entries: CathodeLib's own container, one distributable file, read
            //back entirely in memory at install
            CATHODE.PAK2 pak = new CATHODE.PAK2(new byte[0]);
            pak.Entries.Clear();
            //A combinable mod in the format every OpenCAGE with mods reads; a replacement one in the format only those that keep
            //it apart do, so an older one refuses it rather than combining it
            _info.Format = _info.Replaces ? ModPackageInfo.CurrentFormat : ModPackageInfo.CombinableFormat;
            pak.Entries.Add(new CATHODE.PAK2.File()
            {
                Filename = "manifest.json",
                Content = ModToolkit.Gzip(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_info, Formatting.Indented))),
            });
            if (_preview != null)
                pak.Entries.Add(new CATHODE.PAK2.File() { Filename = _info.Preview, Content = ModToolkit.Gzip(_preview) });
            for (int i = 0; i < _info.Entries.Count; i++)
            {
                if (_payloads[i] == null)
                    continue;
                pak.Entries.Add(new CATHODE.PAK2.File()
                {
                    Filename = _info.Entries[i].Payload,
                    Content = ModToolkit.Gzip(_payloads[i]),
                });
            }

            //A package is read back whole, and a PAK2's offsets are 32-bit: one that couldn't be read is never written (nor is
            //the old file deleted for it)
            long size = 16 + 3;
            foreach (CATHODE.PAK2.File entry in pak.Entries)
                size += entry.Filename.Length + 1 + 4 + entry.Content.Length + 3;
            if (size > MaxPackageSize)
                throw new Exception("This mod would be " + (size / (1024.0 * 1024 * 1024)).ToString("0.00") + " GB, and a mod file can be 2 GB at most. "
                    + "Leave some of it out (each new level goes in whole) and make it as two or more mods.");

            //Written beside the old file and swapped in once it's whole: a failed write leaves what was there
            string temp = filePath + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            try
            {
                if (!pak.Save(temp) || !File.Exists(temp) || new FileInfo(temp).Length > MaxPackageSize)
                    throw new Exception("Could not write the package to " + filePath + ".");
                if (File.Exists(filePath))
                    File.Delete(filePath);
                File.Move(temp, filePath);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        /* The biggest file reading a package can take whole (a byte array's limit) */
        private const long MaxPackageSize = 0x7FFFFFC7;
    }
}
#endif
