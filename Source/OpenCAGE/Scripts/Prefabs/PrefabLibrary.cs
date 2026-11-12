using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.Prefabs
{
    /// <summary>
    /// A prefab: an OpenCAGE composite package (.ocp) in the Prefabs folder beside OpenCAGE, ready to place. Inserting one brings
    /// its composite (and what that composite places) into the level when the level does not have it yet, then places an
    /// instance of it. OpenCAGE ships gameplay prefabs built from retail patterns (<see cref="PrefabCatalog"/>);
    /// any package a user exports into the folder is listed too.
    /// </summary>
    internal sealed class Prefab
    {
        /// <summary>The package's full path.</summary>
        public string File;
        /// <summary>The package's file name without .ocp - what the catalog and place_prefab know it by.</summary>
        public string Key;
        /// <summary>What the package calls itself (else its file name).</summary>
        public string Name;
        public string Description;
        public string Category;
        /// <summary>One of the prefabs OpenCAGE ships.</summary>
        public bool BuiltIn => Catalog != null;
        /// <summary>The composite an instance places: the package's (first) exported composite.</summary>
        public uint RootGuid;
        public string RootName;
        public CompositeArchive.Manifest Manifest;
        /// <summary>What OpenCAGE knows about a prefab it ships; null for a user's own.</summary>
        public PrefabCatalog.Entry Catalog;

        public ShortGuid Root => new ShortGuid(RootGuid);

        public override string ToString() => Name;
    }

    /// <summary>The prefabs OpenCAGE ships, and what Insert Prefab can set on each one beyond its pins.</summary>
    internal static class PrefabCatalog
    {
        public sealed class Entry
        {
            public string Key;
            public string Category;
            public int Order;
            /// <summary>The NPC archetype instance inside the prefab that can be swapped for another archetype (a copy of the prefab is made for it).</summary>
            public string CharacterEntity;
            /// <summary>A box volume inside the prefab whose size can be set per placement (an alias override of its half_dimensions).</summary>
            public string BoxEntity;
            /// <summary>What to link once it is placed.</summary>
            public string Hint;
            /// <summary>
            /// The NPC archetype the package places in its character slot. It is left out of the package (retail characters are the
            /// game's own files, not OpenCAGE's): Insert Prefab brings it in from the first of <see cref="CharacterLevels"/> this install
            /// has when the level lacks it. Its id is the same in every retail level.
            /// </summary>
            public string CharacterArchetype, CharacterId;
            public string[] CharacterLevels;
        }

        public const string Characters = "Characters", Objectives = "Objectives", World = "World", Mine = "My prefabs";

        public static readonly Entry[] BuiltIn =
        {
            new Entry() { Key = "EnemySpawner", Category = Characters, Order = 0, CharacterEntity = "NPC",
                CharacterArchetype = @"Archetypes\NPCs\Human\Security\Grunt_NPC_Pistol", CharacterId = "03-72-21-29", CharacterLevels = new[] { "PRODUCTION/SCI_HUB", "PRODUCTION/SCI_ANDROIDLAB", "PRODUCTION/TECH_COMMS" },
                Hint = "It spawns by itself when the level starts. Link killed to a Kill Counter's add_kill; npc_reference is what a Hunter, a Safe Despawn or an Out-of-View Spawn Point's character links to." },
            new Entry() { Key = "Ally", Category = Characters, Order = 1, CharacterEntity = "Ally",
                CharacterArchetype = @"Archetypes\NPCs\Human\Civilian\Worker_NPC_Pistol", CharacterId = "43-9E-D5-30", CharacterLevels = new[] { "PRODUCTION/TECH_HUB", "PRODUCTION/SCI_ANDROIDLAB", "PRODUCTION/HAB_AIRPORT", "PRODUCTION/SCI_HUB" },
                Hint = "It spawns by itself when the level starts and follows the player. Place it on the navmesh near the player start." },
            new Entry() { Key = "Hunter", Category = Characters, Order = 2,
                Hint = "Link character to an NPC's npc_reference (an Enemy Spawner's too), and something to start - e.g. the spawner's finished_spawning." },
            new Entry() { Key = "SafeDespawn", Category = Characters, Order = 3,
                Hint = "Link character to the NPC's npc_reference and call despawn." },
            new Entry() { Key = "SpawnPoint", Category = Characters, Order = 4,
                Hint = "Link character to the NPC's npc_reference and call try (e.g. from an Enemy Spawner's finished_spawning); chain busy to the next point's try." },
            new Entry() { Key = "KillCounter", Category = Objectives, Order = 0,
                Hint = "Link each enemy's killed to add_kill, and something (a level start trigger) to start." },
            new Entry() { Key = "KillVolume", Category = World, Order = 0, BoxEntity = "Volume",
                Hint = "Its position is the floor of the box: put it where the player should die (water, a pit) and size it." },
        };

        public static readonly string[] CategoryOrder = { Characters, Objectives, World, Mine };

        public static Entry Find(string key) => BuiltIn.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The Prefabs folder: every package in it, read once and kept until the file changes.</summary>
    internal static class PrefabLibrary
    {
        public const string FolderName = "Prefabs";

        /// <summary>Where the prefabs live: beside OpenCAGE.</summary>
        public static string Folder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName);

        private sealed class Cached
        {
            public DateTime Written;
            public long Length;
            public Prefab Prefab;
        }
        private static readonly Dictionary<string, Cached> _cache = new Dictionary<string, Cached>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        /// <summary>Every prefab in the folder (and its subfolders), shipped ones first in catalog order. Packages that cannot be read go to <paramref name="problems"/>.</summary>
        public static List<Prefab> All(List<string> problems = null)
        {
            List<Prefab> found = new List<Prefab>();
            string folder = Folder;
            if (!Directory.Exists(folder))
                return found;
            string[] files;
            try { files = Directory.GetFiles(folder, "*" + PackageFiles.CompositeExtension, SearchOption.AllDirectories); }
            catch (Exception e) { problems?.Add("The prefab folder could not be read: " + e.Message); return found; }
            foreach (string file in files)
            {
                try { found.Add(Read(file)); }
                catch (Exception e) { problems?.Add(Path.GetFileName(file) + ": " + e.Message); }
            }
            return found
                .OrderBy(o => Array.IndexOf(PrefabCatalog.CategoryOrder, o.Category) is int i && i >= 0 ? i : PrefabCatalog.CategoryOrder.Length)
                .ThenBy(o => o.Catalog?.Order ?? int.MaxValue)
                .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>A prefab by its name, its key (file name without .ocp), or the full path of any .ocp. Null when there is none.</summary>
        public static Prefab Find(string what)
        {
            if (string.IsNullOrWhiteSpace(what))
                return null;
            what = what.Trim();
            if (what.EndsWith(PackageFiles.CompositeExtension, StringComparison.OrdinalIgnoreCase) && Path.IsPathRooted(what) && File.Exists(what))
                return Read(what);
            List<Prefab> all = All();
            string key = Path.GetFileNameWithoutExtension(what);
            return all.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(o => string.Equals(o.Name, what, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(o => string.Equals(Simplify(o.Name), Simplify(what), StringComparison.OrdinalIgnoreCase));
        }

        //"Kill Counter + Objective" is found as "kill_counter_objective" or "KillCounterObjective" too
        private static string Simplify(string text) => new string((text ?? "").Where(char.IsLetterOrDigit).ToArray());

        /// <summary>One package's header, as a prefab (cached until the file changes). Throws when it is not a package OpenCAGE can read.</summary>
        public static Prefab Read(string file)
        {
            FileInfo info = new FileInfo(file);
            lock (_lock)
            {
                if (_cache.TryGetValue(info.FullName, out Cached cached) && cached.Written == info.LastWriteTimeUtc && cached.Length == info.Length)
                    return cached.Prefab;
            }
            CompositeArchive.Manifest manifest = CompositeArchive.ReadHeader(info.FullName);
            CompositeArchive.CompositeEntry root = manifest.Composites.FirstOrDefault(o => o != null && o.IsRoot) ?? manifest.Composites.FirstOrDefault(o => o != null);
            if (root == null)
                throw new InvalidDataException("the package holds no composites");
            string key = Path.GetFileNameWithoutExtension(info.Name);
            PrefabCatalog.Entry entry = PrefabCatalog.Find(key);
            //A shipped prefab only counts as one while it is still the package OpenCAGE made (a user's file of the same name is theirs)
            if (entry != null && !IsShipped(info.FullName))
                entry = null;
            Prefab prefab = new Prefab()
            {
                File = info.FullName,
                Key = key,
                Name = manifest.DisplayName(info.FullName),
                Description = (manifest.Description ?? "").Trim(),
                Category = entry?.Category ?? PrefabCatalog.Mine,
                RootGuid = root.Guid,
                RootName = root.Name,
                Manifest = manifest,
                Catalog = entry,
            };
            lock (_lock)
                _cache[info.FullName] = new Cached() { Written = info.LastWriteTimeUtc, Length = info.Length, Prefab = prefab };
            return prefab;
        }

        //The shipped prefabs sit at the top of the folder; a subfolder is the user's
        private static bool IsShipped(string file) =>
            string.Equals(Path.GetFullPath(Path.GetDirectoryName(file)).TrimEnd('\\'), Path.GetFullPath(Folder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

        /// <summary>The prefab's description as paragraphs (the package keeps it as lines).</summary>
        public static string[] Paragraphs(Prefab prefab) =>
            (prefab?.Description ?? "").Replace("\r\n", "\n").Split('\n').Select(o => o.Trim()).Where(o => o.Length != 0).ToArray();
    }
}
