using CATHODE;
using CathodeLib;
using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace OpenCAGE
{
    /// <summary>
    /// The game's texture streaming has two limits: TextureBudget (Level Check) watches the heap's BYTES; this watches its COUNT.
    ///
    /// The game streams each drawn texture's full-size part into one heap (CA::CTMemory::ExternalHeap "Texture_Strm"), made with 1024 block
    /// handles (AI.exe TEXTURE_STREAMING::one_time_initialise: ExternalHeap::initialise(..., block count 0x400, ...)). Every texture streamed
    /// in holds one handle, and so does every free gap between them, so only about 1,020 textures are ever in at full size at once, however
    /// much of the heap is free. A request that finds no handle waits; in a level with one zone nothing is ever let go, so those textures
    /// draw from their small resident copies, blurry, for the whole level. Retail levels stay under the limit by zoning.
    ///
    /// Two fixes that need no exe patch:
    /// - KEEP LOADED: a texture with no streamed part and its full chain in the persistent part never streams and needs no handle (retail
    ///   does this for effect flipbooks, lens textures and gobos, with the same header sizes in both parts). It costs memory for the whole
    ///   level, so it suits small textures and the few that must always be sharp (a sky).
    /// - FEWER TEXTURES: the same image imported under several names streams once per name. Merging identical textures (materials pointed at
    ///   one copy) frees the others' handles.
    /// The GPU texture pool (4,096 handles, perf.limits pool.Textures) is the next limit: a streamed-in texture takes two GPU textures (its
    /// resident and streamed copies), a kept-loaded one takes one, so patching the handle count alone is not enough.
    /// </summary>
    public static partial class TextureStreaming
    {
        /// <summary>The block handles the game's texture streaming heap is made with (AI.exe, all three PC builds).</summary>
        public const int StreamerBlocks = 1024;
        /// <summary>What a level should keep under at once: handles are also taken by the gaps between streamed textures and by the
        /// global textures the player, weapons and effects draw.</summary>
        public const int SafeStreamedAtOnce = 1000;
        /// <summary>The GPU texture pool (perf.limits pool.Textures).</summary>
        public const int GpuTexturePool = 4096;
        /// <summary>The longest edge "keep small textures loaded" takes by default.</summary>
        public const int DefaultKeepLoadedEdge = 256;
        /// <summary>The resident copy's longest edge when a texture streams again (most retail resident copies stop at 128 px).</summary>
        public const int ResidentEdge = 128;

        public static bool HasPart(Textures.TEX4.Texture part) => part?.Content != null && part.Content.Length != 0;

        /// <summary>Whether the game streams this texture (it has a streamed part): each one streamed in takes a handle.</summary>
        public static bool Streams(Textures.TEX4 texture) => texture != null && HasPart(texture.TextureStreamed);

        /// <summary>Kept loaded: no streamed part, the image in the persistent part (never streams, never blurry).</summary>
        public static bool IsKeptLoaded(Textures.TEX4 texture) => texture != null && !HasPart(texture.TextureStreamed) && HasPart(texture.TexturePersistent);

        /// <summary>The full-size image's longest edge.</summary>
        public static int Edge(Textures.TEX4 texture)
        {
            Textures.TEX4.Texture part = HasPart(texture?.TextureStreamed) ? texture.TextureStreamed : texture?.TexturePersistent;
            return part == null ? 0 : Math.Max(part.Width, part.Height);
        }

        /// <summary>Why a texture can't be kept loaded, or null when it can.</summary>
        public static string CannotKeepLoaded(Textures.TEX4 texture)
        {
            if (texture == null) return "no texture";
            if (IsKeptLoaded(texture)) return "it is already kept loaded";
            if (!HasPart(texture.TextureStreamed)) return "it has no image";
            //Retail streams every cubemap; a cube in the persistent slot is untested
            if (texture.StateFlags.HasFlag(Textures.TextureStateFlag.CUBE)) return "it is a cubemap (the game streams every cubemap)";
            return null;
        }

        /// <summary>Bytes the level holds for good once this texture is kept loaded, beyond its resident copy today (its streamed part
        /// replaces its persistent one). The game counts each texture's memory as its stored bytes, the heap's 4 KB rounding aside.</summary>
        public static long KeepLoadedCost(Textures.TEX4 texture)
        {
            if (!Streams(texture)) return 0;
            return texture.TextureStreamed.Content.LongLength - (HasPart(texture.TexturePersistent) ? texture.TexturePersistent.Content.LongLength : 0);
        }

        /// <summary>
        /// Keep a texture loaded: its streamed part (the full chain) becomes its persistent part, and the streamed slot keeps only the header
        /// sizes, as every shipped persistent-only texture's does (P and S sizes and mip counts equal, no streamed bytes). False (unchanged)
        /// when it can't (<see cref="CannotKeepLoaded"/>).
        /// </summary>
        public static bool KeepLoaded(Textures.TEX4 texture)
        {
            if (CannotKeepLoaded(texture) != null) return false;
            Textures.TEX4.Texture full = texture.TextureStreamed;
            texture.TexturePersistent = new Textures.TEX4.Texture() { Width = full.Width, Height = full.Height, Depth = full.Depth, MipLevels = full.MipLevels, Content = (byte[])full.Content.Clone() };
            texture.TextureStreamed = HeaderOnly(full);
            return true;
        }

        /// <summary>A part with the sizes and no bytes (the streamed slot of a kept-loaded texture).</summary>
        public static Textures.TEX4.Texture HeaderOnly(Textures.TEX4.Texture part) => new Textures.TEX4.Texture() { Width = part?.Width ?? 0, Height = part?.Height ?? 0, Depth = part?.Depth ?? 0, MipLevels = part?.MipLevels ?? 0 };

        /// <summary>The two parts of a texture as they are now, to put back (undo).</summary>
        public static (Textures.TEX4.Texture streamed, Textures.TEX4.Texture persistent) Parts(Textures.TEX4 texture) => (texture.TextureStreamed, texture.TexturePersistent);

        /// <summary>
        /// Textures to keep loaded: those that stream, aren't cubemaps, and match - by name (exact, path-normalised), by a regex on the name,
        /// and/or with a full-size longest edge at most <paramref name="maxEdge"/> (0: any size). With no names, no pattern and no edge,
        /// nothing matches. Sorted by name.
        /// </summary>
        public static List<Textures.TEX4> Select(IEnumerable<Textures.TEX4> textures, ICollection<string> names, string pattern, int maxEdge, bool keepLoaded)
        {
            HashSet<string> wanted = names == null || names.Count == 0 ? null : new HashSet<string>(names.Select(Normalise), StringComparer.Ordinal);
            System.Text.RegularExpressions.Regex regex = string.IsNullOrEmpty(pattern) ? null : new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (wanted == null && regex == null && maxEdge <= 0) return new List<Textures.TEX4>();
            return textures.Where(o => o != null)
                .Where(o => keepLoaded ? CannotKeepLoaded(o) == null : IsKeptLoaded(o))
                .Where(o => wanted == null || wanted.Contains(Normalise(o.Name)))
                .Where(o => regex == null || regex.IsMatch(o.Name ?? ""))
                .Where(o => maxEdge <= 0 || Edge(o) <= maxEdge)
                .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string Normalise(string name) => (name ?? "").Replace('/', '\\').ToUpperInvariant();

        #region Identical textures
        /// <summary>Textures with identical images: the one kept and the copies whose material slots move to it.</summary>
        public sealed class DuplicateGroup
        {
            public Textures.TEX4 Keep;
            public List<Textures.TEX4> Copies = new List<Textures.TEX4>();
            /// <summary>Material texture slots pointing at a copy (they move to <see cref="Keep"/>).</summary>
            public int Slots;
            public long StreamedBytes => Streams(Keep) ? Keep.TextureStreamed.Content.LongLength : 0;
        }

        /// <summary>
        /// Groups of the level's textures with the same image: format, state and usage flags, both parts' sizes, mips and bytes all equal.
        /// Left out: cubemaps (environment maps name theirs by path), Flash textures (a movie may draw into one by name), and textures no
        /// material uses (they never stream). The texture kept is the one the most material slots use, then the first in the level.
        /// </summary>
        public static List<DuplicateGroup> FindDuplicates(Level level)
        {
            List<Textures.TEX4> all = level?.Textures?.Entries?.Where(o => o != null).ToList() ?? new List<Textures.TEX4>();
            Dictionary<Textures.TEX4, int> slots = SlotUse(level);
            Dictionary<string, List<Textures.TEX4>> byImage = new Dictionary<string, List<Textures.TEX4>>(StringComparer.Ordinal);
            using (SHA1 sha = SHA1.Create())
            {
                foreach (Textures.TEX4 texture in all)
                {
                    if (texture.StateFlags.HasFlag(Textures.TextureStateFlag.CUBE) || texture.StateFlags.HasFlag(Textures.TextureStateFlag.FLASH_TEXTURE)) continue;
                    if (!slots.ContainsKey(texture)) continue;
                    if (!HasPart(texture.TextureStreamed) && !HasPart(texture.TexturePersistent)) continue;
                    string key = ImageKey(sha, texture);
                    if (!byImage.TryGetValue(key, out List<Textures.TEX4> same)) byImage[key] = same = new List<Textures.TEX4>();
                    same.Add(texture);
                }
            }
            List<DuplicateGroup> groups = new List<DuplicateGroup>();
            foreach (List<Textures.TEX4> same in byImage.Values)
            {
                if (same.Count < 2) continue;
                Textures.TEX4 keep = same.OrderByDescending(o => slots[o]).ThenBy(o => all.IndexOf(o)).First();
                DuplicateGroup group = new DuplicateGroup() { Keep = keep, Copies = same.Where(o => !ReferenceEquals(o, keep)).ToList() };
                group.Slots = group.Copies.Sum(o => slots[o]);
                groups.Add(group);
            }
            return groups.OrderByDescending(o => o.Copies.Count).ThenBy(o => o.Keep.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string ImageKey(SHA1 sha, Textures.TEX4 texture)
        {
            Textures.TEX4.Texture s = texture.TextureStreamed, p = texture.TexturePersistent;
            string head = (int)texture.Format + "|" + (int)texture.StateFlags + "|" + (int)texture.UsageFlags + "|" +
                          (s?.Width ?? 0) + "x" + (s?.Height ?? 0) + "x" + (s?.Depth ?? 0) + "m" + (s?.MipLevels ?? 0) + "|" +
                          (p?.Width ?? 0) + "x" + (p?.Height ?? 0) + "x" + (p?.Depth ?? 0) + "m" + (p?.MipLevels ?? 0) + "|";
            byte[] hs = HasPart(s) ? sha.ComputeHash(s.Content) : new byte[0];
            byte[] hp = HasPart(p) ? sha.ComputeHash(p.Content) : new byte[0];
            return head + Convert.ToBase64String(hs) + "|" + Convert.ToBase64String(hp) + "|" + (s?.Content?.Length ?? 0) + "|" + (p?.Content?.Length ?? 0);
        }

        /// <summary>How many material texture slots use each level texture.</summary>
        public static Dictionary<Textures.TEX4, int> SlotUse(Level level)
        {
            Dictionary<Textures.TEX4, int> use = new Dictionary<Textures.TEX4, int>(ReferenceComparer.Instance);
            HashSet<Textures.TEX4> levelTextures = new HashSet<Textures.TEX4>(level?.Textures?.Entries?.Where(o => o != null) ?? Enumerable.Empty<Textures.TEX4>(), ReferenceComparer.Instance);
            foreach (Materials.Material material in level?.Materials?.Entries ?? new List<Materials.Material>())
                foreach (TexturePtr pointer in material?.TextureReferences ?? new List<TexturePtr>())
                    if (pointer?.Texture != null && pointer.Location == TexturePtr.Source.LEVEL && levelTextures.Contains(pointer.Texture))
                        use[pointer.Texture] = (use.TryGetValue(pointer.Texture, out int n) ? n : 0) + 1;
            return use;
        }

        /// <summary>
        /// Point every material slot that uses a copy at the group's kept texture. Returns each slot moved with the texture it had (to put back).
        /// The copies stay in the level, unused: a texture no material uses is never streamed, and nothing else that might name one
        /// (an environment map, a Flash movie) is touched.
        /// </summary>
        public static List<(TexturePtr slot, Textures.TEX4 was)> MergeDuplicates(Level level, IEnumerable<DuplicateGroup> groups)
        {
            Dictionary<Textures.TEX4, Textures.TEX4> to = new Dictionary<Textures.TEX4, Textures.TEX4>(ReferenceComparer.Instance);
            foreach (DuplicateGroup group in groups)
                foreach (Textures.TEX4 copy in group.Copies)
                    to[copy] = group.Keep;
            List<(TexturePtr, Textures.TEX4)> moved = new List<(TexturePtr, Textures.TEX4)>();
            foreach (Materials.Material material in level?.Materials?.Entries ?? new List<Materials.Material>())
                foreach (TexturePtr pointer in material?.TextureReferences ?? new List<TexturePtr>())
                    if (pointer?.Texture != null && pointer.Location == TexturePtr.Source.LEVEL && to.TryGetValue(pointer.Texture, out Textures.TEX4 keep))
                    {
                        moved.Add((pointer, pointer.Texture));
                        pointer.Texture = keep;
                    }
            return moved;
        }

        /// <summary>TEX4 compares by content; the tables here go by object.</summary>
        private sealed class ReferenceComparer : IEqualityComparer<Textures.TEX4>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(Textures.TEX4 x, Textures.TEX4 y) => ReferenceEquals(x, y);
            public int GetHashCode(Textures.TEX4 o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }
        #endregion

        #region How many stream at once
        /// <summary>The level's streamed textures by count, the way the game's handles see them.</summary>
        public sealed class SlotEstimate
        {
            public int LevelTextures, Streamed, KeptLoaded, Cubemaps;
            /// <summary>Streamed textures drawn everywhere (unzoned or global-zone movers), and those no placed model draws (characters, weapons,
            /// effects: they stream only while drawn).</summary>
            public int Always, Unplaced, Unused;
            public int Peak;
            public string PeakWhere;
            /// <summary>Per zone, busiest first: its own streamed textures with what is drawn everywhere, and with its linked neighbours' too.</summary>
            public List<(string zone, int textures, int withNeighbours)> Zones = new List<(string, int, int)>();
            /// <summary>Streamed textures that are 256 px or smaller, and the bytes keeping them loaded would hold for good.</summary>
            public int Small;
            public long SmallBytes;
            public int Duplicates;
            public bool OneZone;
        }

        private static bool AlwaysLoadedZone(ShortGuid zone) => zone == ShortGuid.Invalid || zone == new ShortGuid("01-00-00-00");

        /// <summary>
        /// Count the streamed textures that can be in at once. A level with one zone lets nothing go: every streamed texture something can draw
        /// counts. A zoned level: the always-drawn set plus a zone and - given <paramref name="neighbours"/> (the zones a ZoneLink joins it to:
        /// the game streams those in too while the link is open) - its neighbours', at the busiest zone. Read only; any thread.
        /// </summary>
        public static SlotEstimate Estimate(Level level, Func<ShortGuid, string> zoneName = null, bool withDuplicates = false, Func<ShortGuid, IEnumerable<ShortGuid>> neighbours = null)
        {
            SlotEstimate estimate = new SlotEstimate();
            List<Textures.TEX4> all = level?.Textures?.Entries?.Where(o => o != null).ToList() ?? new List<Textures.TEX4>();
            estimate.LevelTextures = all.Count;
            estimate.Streamed = all.Count(Streams);
            estimate.KeptLoaded = all.Count(IsKeptLoaded);
            estimate.Cubemaps = all.Count(o => Streams(o) && o.StateFlags.HasFlag(Textures.TextureStateFlag.CUBE));
            List<Textures.TEX4> small = all.Where(o => CannotKeepLoaded(o) == null && Edge(o) <= DefaultKeepLoadedEdge).ToList();
            estimate.Small = small.Count;
            estimate.SmallBytes = small.Sum(KeepLoadedCost);
            HashSet<Textures.TEX4> streamed = new HashSet<Textures.TEX4>(all.Where(Streams), ReferenceComparer.Instance);

            Dictionary<Textures.TEX4, HashSet<ShortGuid>> zonesOf = new Dictionary<Textures.TEX4, HashSet<ShortGuid>>(ReferenceComparer.Instance);
            foreach (Movers.MOVER_DESCRIPTOR mover in level?.Movers?.Entries ?? new List<Movers.MOVER_DESCRIPTOR>())
                foreach (RenderableElements.Element element in mover.RenderableElements)
                    foreach (TexturePtr pointer in element?.Material?.TextureReferences ?? new List<TexturePtr>())
                    {
                        Textures.TEX4 texture = pointer?.Texture;
                        if (texture == null || !streamed.Contains(texture)) continue;
                        if (!zonesOf.TryGetValue(texture, out HashSet<ShortGuid> zones)) zonesOf[texture] = zones = new HashSet<ShortGuid>();
                        zones.Add(mover.PrimaryZoneID);
                        if (mover.SecondaryZoneID != ShortGuid.Invalid) zones.Add(mover.SecondaryZoneID);
                    }
            HashSet<Textures.TEX4> drawable = new HashSet<Textures.TEX4>(ReferenceComparer.Instance);
            foreach (RenderableElements.Element element in level?.RenderableElements?.Entries ?? new List<RenderableElements.Element>())
                foreach (TexturePtr pointer in element?.Material?.TextureReferences ?? new List<TexturePtr>())
                    if (pointer?.Texture != null && streamed.Contains(pointer.Texture)) drawable.Add(pointer.Texture);
            HashSet<Textures.TEX4> always = new HashSet<Textures.TEX4>(streamed.Where(o => zonesOf.TryGetValue(o, out HashSet<ShortGuid> z) && z.Any(AlwaysLoadedZone)), ReferenceComparer.Instance);
            HashSet<Textures.TEX4> unplaced = new HashSet<Textures.TEX4>(streamed.Where(o => !zonesOf.ContainsKey(o) && drawable.Contains(o)), ReferenceComparer.Instance);
            estimate.Always = always.Count;
            estimate.Unplaced = unplaced.Count;
            estimate.Unused = streamed.Count(o => !zonesOf.ContainsKey(o) && !drawable.Contains(o));
            Dictionary<ShortGuid, HashSet<Textures.TEX4>> byZone = new Dictionary<ShortGuid, HashSet<Textures.TEX4>>();
            foreach (KeyValuePair<Textures.TEX4, HashSet<ShortGuid>> entry in zonesOf)
            {
                if (always.Contains(entry.Key)) continue;
                foreach (ShortGuid zone in entry.Value)
                {
                    if (!byZone.TryGetValue(zone, out HashSet<Textures.TEX4> set)) byZone[zone] = set = new HashSet<Textures.TEX4>(ReferenceComparer.Instance);
                    set.Add(entry.Key);
                }
            }
            List<(string zone, int textures, int withNeighbours)> zoneRows = new List<(string, int, int)>();
            foreach (KeyValuePair<ShortGuid, HashSet<Textures.TEX4>> zone in byZone)
            {
                HashSet<Textures.TEX4> near = new HashSet<Textures.TEX4>(zone.Value, ReferenceComparer.Instance);
                foreach (ShortGuid other in neighbours?.Invoke(zone.Key) ?? Enumerable.Empty<ShortGuid>())
                    if (byZone.TryGetValue(other, out HashSet<Textures.TEX4> theirs)) near.UnionWith(theirs);
                zoneRows.Add((zoneName?.Invoke(zone.Key) ?? zone.Key.ToByteString(), estimate.Always + zone.Value.Count, estimate.Always + near.Count));
            }
            estimate.Zones = zoneRows.OrderByDescending(o => o.withNeighbours).ThenByDescending(o => o.textures).ToList();
            estimate.OneZone = byZone.Count == 0;
            if (!estimate.OneZone)
            {
                //A zoned level lets a zone's textures go as it unloads; what no placed model draws streams only while drawn
                estimate.Peak = estimate.Zones[0].withNeighbours;
                estimate.PeakWhere = "in zone " + estimate.Zones[0].zone + (neighbours != null ? " with the zones linked to it" : "") + " (and what is drawn everywhere)";
            }
            else
            {
                estimate.Peak = estimate.Always + estimate.Unplaced;
                estimate.PeakWhere = "everywhere (the level has one zone, so nothing is let go: every streamed texture anything can draw counts)";
            }
            if (withDuplicates) estimate.Duplicates = FindDuplicates(level).Sum(o => o.Copies.Count(Streams));
            return estimate;
        }
        #endregion
    }
}
