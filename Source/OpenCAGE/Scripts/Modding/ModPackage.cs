using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace OpenCAGE.Modding
{
    /* The distributable: a zip with a manifest and a payload per changed thing.
     *
     * Three payload kinds. "file" is the whole target file. "delta" is an ACDELTA1 patch that turns
     * the vanilla bytes into the target - it only applies on top of the exact source hash, which is
     * what keeps installs byte-faithful. "bml" is a config patch: the individual changed values,
     * applied to whatever the config currently holds so different config mods merge. */
    public class ModPackageEntry
    {
        public const string KindFile = "file";
        public const string KindDelta = "delta";
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
        [JsonProperty("format")] public int Format = 1;
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("description")] public string Description;
        [JsonProperty("author")] public string Author;
        [JsonProperty("version")] public string Version;
        [JsonProperty("createdUtc")] public DateTime CreatedUtc;
        [JsonProperty("opencage")] public string OpenCageVersion;
        [JsonProperty("platform")] public string Platform = "PC";
        [JsonProperty("hashSet")] public string HashSet = VanillaManifest.DefaultSet;
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
            if (info.Format != 1)
                throw new Exception("The mod package uses format " + info.Format + ", which this version of OpenCAGE doesn't know. Update OpenCAGE.");
            return new ModPackage() { Info = info, FilePath = filePath };
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

        public void Write(string filePath)
        {
            //A PAK2 of gzipped entries: CathodeLib's own container, one distributable file, read
            //back entirely in memory at install
            CATHODE.PAK2 pak = new CATHODE.PAK2(new byte[0]);
            pak.Entries.Clear();
            pak.Entries.Add(new CATHODE.PAK2.File()
            {
                Filename = "manifest.json",
                Content = ModToolkit.Gzip(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_info, Formatting.Indented))),
            });
            for (int i = 0; i < _info.Entries.Count; i++)
                pak.Entries.Add(new CATHODE.PAK2.File()
                {
                    Filename = _info.Entries[i].Payload,
                    Content = ModToolkit.Gzip(_payloads[i]),
                });

            if (File.Exists(filePath))
                File.Delete(filePath);
            if (!pak.Save(filePath))
                throw new Exception("Could not write the package to " + filePath + ".");
        }
    }
}
