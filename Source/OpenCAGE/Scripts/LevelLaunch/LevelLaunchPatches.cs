using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE
{
    /// <summary>
    /// The AI.exe changes behind a level's launch settings: the streaming texture heap's size, whether characters collide with
    /// each other, which loading playlist plays, and the guard that keeps a spawn from crashing when every character slot is taken.
    /// Each region is only written over its exact original bytes or a form of its own, so another build's code is left alone.
    /// The binary relocates (it has a relocation table), so new code holds no absolute addresses of its own.
    /// </summary>
    public static class LevelLaunchPatches
    {
        /* Where each change goes in one build (file offsets) */
        private sealed class Sites
        {
            public int Heap;                    //the heap size read: 22 bytes
            public int PoolCheck;               //the free character slot check (11 bytes in: 18 bytes)
            public int PoolCave;                //45 bytes of padding for its new body
            public int Request;                 //where a loaded character takes its slot (its first 5 bytes)
            public int RequestCave;             //51 bytes of padding for the check in front of it
            public int Erase;                   //what drops a character's entry (a load given up on)
            public int CollideA, CollideB;      //where a character's collision layer is picked (12 bytes each)
            public int CollideCave;             //15 bytes of padding for the layer pick
            public int Slot;                    //a 26 byte string only a developer option uses: holds a playlist name
            public int Fallback;                //the playlist pushed when no playlist is registered (imm32)
            public uint FallbackOriginal;
            public int[] Table;                 //the level -> playlist table's playlist pushes (imm32), in order
            public uint[] TableOriginal;
        }

        private static Sites For(PatchManager.Platform platform)
        {
            switch (platform)
            {
                case PatchManager.Platform.STEAM:
                    return new Sites()
                    {
                        Heap = 0x5F6297, PoolCheck = 0x389B30, PoolCave = 0x567817, Request = 0x3D2F50, RequestCave = 0xBF411A, Erase = 0x3CFC60, CollideA = 0x3EBFB4, CollideB = 0x3FD552, CollideCave = 0x45BCD8,
                        Slot = 0xEFD698, Fallback = 0x43C130, FallbackOriginal = 0x12FAA5C,
                        Table = new[] { 0x409234, 0x40927A, 0x4092C4, 0x4092EE, 0x409318, 0x409342, 0x40936C, 0x409396, 0x4093C0, 0x4093EA, 0x409414, 0x40943E, 0x409468, 0x409492, 0x4094BC, 0x4094E6, 0x409510, 0x40953A, 0x409564, 0x40958E },
                        TableOriginal = new uint[] { 0x12FB12C, 0x12FB10C, 0x12FB0E8, 0x12FB0C4, 0x12FB0A8, 0x12FB078, 0x12FB048, 0x12FB01C, 0x12FAFEC, 0x12FAFC4, 0x12FAF98, 0x12FAF70, 0x12FAF4C, 0x12FAF28, 0x12FAEF8, 0x12FAECC, 0x12FAEAC, 0x12FAE84, 0x12FAE6C, 0x12FB12C },
                    };
                case PatchManager.Platform.GOG:
                    return new Sites()
                    {
                        Heap = 0x5F6407, PoolCheck = 0x389CE0, PoolCave = 0x4DE3B8, Request = 0x3D2F00, RequestCave = 0x2C43AC, Erase = 0x3CFC10, CollideA = 0x3EC0C4, CollideB = 0x3FD642, CollideCave = 0xD953C1,
                        Slot = 0xF15248, Fallback = 0x43C300, FallbackOriginal = 0x1311A14,
                        Table = new[] { 0x4093E4, 0x40942A, 0x409474, 0x40949E, 0x4094C8, 0x4094F2, 0x40951C, 0x409546, 0x409570, 0x40959A, 0x4095C4, 0x4095EE, 0x409618, 0x409642, 0x40966C, 0x409696, 0x4096C0, 0x4096EA, 0x409714, 0x40973E },
                        TableOriginal = new uint[] { 0x13120E4, 0x13120C4, 0x13120A0, 0x131207C, 0x1312060, 0x1312030, 0x1312000, 0x1311FD4, 0x1311FA4, 0x1311F7C, 0x1311F50, 0x1311F28, 0x1311F04, 0x1311EE0, 0x1311EB0, 0x1311E84, 0x1311E64, 0x1311E3C, 0x1311E24, 0x13120E4 },
                    };
                case PatchManager.Platform.EPIC_GAMES_STORE:
                    return new Sites()
                    {
                        Heap = 0x548757, PoolCheck = 0x39A8A0, PoolCave = 0xC0999A, Request = 0x3E3FD0, RequestCave = 0xD5F38C, Erase = 0x3E0C60, CollideA = 0x3FD1A4, CollideB = 0x40E752, CollideCave = 0xB6B315,
                        Slot = 0xF153F8, Fallback = 0x44D2E0, FallbackOriginal = 0x13121BC,
                        Table = new[] { 0x41A494, 0x41A4DA, 0x41A524, 0x41A54E, 0x41A578, 0x41A5A2, 0x41A5CC, 0x41A5F6, 0x41A620, 0x41A64A, 0x41A674, 0x41A69E, 0x41A6C8, 0x41A6F2, 0x41A71C, 0x41A746, 0x41A770, 0x41A79A, 0x41A7C4, 0x41A7EE },
                        TableOriginal = new uint[] { 0x131288C, 0x131286C, 0x1312848, 0x1312824, 0x1312808, 0x13127D8, 0x13127A8, 0x131277C, 0x131274C, 0x1312724, 0x13126F8, 0x13126D0, 0x13126AC, 0x1312688, 0x1312658, 0x131262C, 0x131260C, 0x13125E4, 0x13125CC, 0x131288C },
                    };
                default:
                    return null;
            }
        }

        /// <summary>Whether this build of the game can take launch settings.</summary>
        public static bool Supports(PatchManager.Platform platform) => For(platform) != null;

        #region THE TABLE
        /// <summary>One entry of the game's level -> loading playlist table: the level names it matches (by the last part of the path) and its playlist.</summary>
        public sealed class TableEntry
        {
            public string[] Levels;
            public string Playlist;
        }

        /// <summary>The game's own table, in its order; the last entry is what every other level gets.</summary>
        public static readonly IReadOnlyList<TableEntry> Table = new List<TableEntry>()
        {
            new TableEntry() { Levels = new[] { "BSP_Torrens" }, Playlist = "TORRENS_AmbientLoad" },
            new TableEntry() { Levels = new[] { "HAB_Airport" }, Playlist = "AIRPORT_AmbientLoad" },
            new TableEntry() { Levels = new[] { "Tech_Hub" }, Playlist = "TECH_HUB_AmbientLoad" },
            new TableEntry() { Levels = new[] { "Tech_Comms" }, Playlist = "TECH_COMMS_AmbientLoad" },
            new TableEntry() { Levels = new[] { "SCI_Hub" }, Playlist = "SCI_HUB_AmbientLoad" },
            new TableEntry() { Levels = new[] { "SCI_HospitalUpper" }, Playlist = "HOSPITAL_UPPER_AmbientLoad" },
            new TableEntry() { Levels = new[] { "SCI_HospitalLower" }, Playlist = "HOSPITAL_LOWER_AmbientLoad" },
            new TableEntry() { Levels = new[] { "SCI_AndroidLab" }, Playlist = "ANDROID_LABS_AmbientLoad" },
            new TableEntry() { Levels = new[] { "HAB_ShoppingCentre" }, Playlist = "SHOPPING_CENTRE_AmbientLoad" },
            new TableEntry() { Levels = new[] { "Tech_MuthrCore" }, Playlist = "APOLLO_CORE_AmbientLoad" },
            new TableEntry() { Levels = new[] { "ENG_ReactorCore" }, Playlist = "REACTOR_CORE_AmbientLoad" },
            new TableEntry() { Levels = new[] { "ENG_Alien_Nest" }, Playlist = "ALIEN_NEST_AmbientLoad" },
            new TableEntry() { Levels = new[] { "TECH_RnD" }, Playlist = "TECH_RND_AmbientLoad" },
            new TableEntry() { Levels = new[] { "TECH_RnD_HzdLab" }, Playlist = "HAZLAB_AmbientLoad" },
            new TableEntry() { Levels = new[] { "HAB_CorporatePent" }, Playlist = "CORPORATE_PENT_AmbientLoad" },
            new TableEntry() { Levels = new[] { "ENG_TowPlatform" }, Playlist = "TOWING_PLATFORM_AmbientLoad" },
            new TableEntry() { Levels = new[] { "Solace" }, Playlist = "ANESIDORA_AmbientLoad" },
            new TableEntry() { Levels = new[] { "BSP_LV426_Pt02" }, Playlist = "LV426_PT2_AmbientLoad" },
            new TableEntry() { Levels = new[] { "frontend", "Frontend" }, Playlist = "ReturnToFrontEndLevel" },
            new TableEntry() { Levels = new string[0], Playlist = "TORRENS_AmbientLoad" },
        };

        /// <summary>The table entry the game picks for a level (by the last part of its path; the last entry for any level it doesn't name).</summary>
        public static int TableIndexOf(string level)
        {
            string last = (level ?? "").Replace('\\', '/').TrimEnd('/');
            last = last.Substring(last.LastIndexOf('/') + 1);
            for (int i = 0; i < Table.Count - 1; i++)
                if (Table[i].Levels.Any(o => string.Equals(o, last, StringComparison.OrdinalIgnoreCase)))
                    return i;
            return Table.Count - 1;
        }

        /// <summary>The longest playlist name a level can give that isn't one of the table's own (they are kept in a 26 byte string).</summary>
        public const int MaxPlaylistName = 25;
        private const string SlotOriginalText = "example_transition_images";
        #endregion

        #region BYTES
        private static readonly byte[] HeapOriginal = { 0x8B, 0x78, 0x50, 0x8B, 0x86, 0x90, 0x20, 0x00, 0x00, 0xC1, 0xE7, 0x14, 0x85, 0xC0, 0x68, 0xCC, 0x00, 0x00, 0x00, 0x0F, 0x47, 0xF8 };

        /* The heap is at least `mb` megabytes: the setting's value is read as before, raised to mb when it is less
             mov edi,[eax+50h] / mov ecx,mb / cmp edi,ecx / cmovb edi,ecx / shl edi,14h / push 0CCh / nop */
        private static byte[] HeapPatched(int mb)
        {
            byte[] bytes = { 0x8B, 0x78, 0x50, 0xB9, 0, 0, 0, 0, 0x3B, 0xF9, 0x0F, 0x42, 0xF9, 0xC1, 0xE7, 0x14, 0x68, 0xCC, 0x00, 0x00, 0x00, 0x90 };
            BitConverter.GetBytes(mb).CopyTo(bytes, 4);
            return bytes;
        }

        private static int? HeapOf(byte[] current)
        {
            byte[] blank = HeapPatched(0);
            for (int i = 0; i < blank.Length; i++)
                if ((i < 4 || i > 7) && current[i] != blank[i])
                    return null;
            return BitConverter.ToInt32(current, 4);
        }

        /* The free slot check, counting characters still loading as taken (the game takes their slot only once they have loaded,
           so two spawns could both pass for the last slot). In place, after the free count is worked out into eax:
             push esi / mov edx,[ecx+4370h] (characters listed) / lea esi,[ecx+4378h] (first entry's slot) / jmp cave */
        private static readonly byte[] PoolOriginal = { 0x3B, 0x44, 0x24, 0x04, 0x1B, 0xC0, 0x40, 0xC2, 0x04, 0x00, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC };
        private const int PoolPatchAt = 11;
        private static byte[] PoolPatched(int fromVa, int caveVa)
        {
            byte[] bytes = { 0x56, 0x8B, 0x91, 0x70, 0x43, 0x00, 0x00, 0x8D, 0xB1, 0x78, 0x43, 0x00, 0x00, 0xE9, 0, 0, 0, 0 };
            BitConverter.GetBytes(caveVa - (fromVa + bytes.Length)).CopyTo(bytes, 14);
            return bytes;
        }

        /* The cave: each listed character whose entry (0x1D0 bytes each, from +0x230) has no character object yet (+0x160) is still
           loading and takes one from the free count; then free >= asked
             push edi / lea edx,[esi+edx*8] / L: cmp esi,edx / jae D / imul edi,[esi],1D0h / add esi,8 / cmp [ecx+edi+390h],0 / jne L /
             dec eax / jmp L / D: pop edi / pop esi / cmp eax,[esp+4] / setge al / movzx eax,al / ret 4 */
        private static readonly byte[] PoolCave =
        {
            0x57, 0x8D, 0x14, 0xD6, 0x3B, 0xF2, 0x73, 0x16, 0x69, 0x3E, 0xD0, 0x01, 0x00, 0x00, 0x83, 0xC6, 0x08, 0x83, 0xBC, 0x39, 0x90, 0x03, 0x00, 0x00, 0x00,
            0x75, 0xE9, 0x48, 0xEB, 0xE6, 0x5F, 0x5E, 0x3B, 0x44, 0x24, 0x04, 0x0F, 0x9D, 0xC0, 0x0F, 0xB6, 0xC0, 0xC2, 0x04, 0x00,
        };

        /* Where a loaded character takes its slot: loads are queued, so spawns checked in one frame can outrun the free count. With no
           slot free the load is dropped (as a cancelled one is) rather than taking from an empty pool. The pool's counters are read
           through the free slot check's own relocated operands, found relative to this code:
             call L / L: pop eax / mov edx,[eax+put] / mov edx,[edx] / mov eax,[eax+take] / sub edx,[eax] / jle refuse /
             push ecx,ebx,ebp,esi,edi / jmp back / refuse: mov eax,[esp+4] / push [eax] (its id) / lea ecx,[ecx-4] / call drop / ret 4 */
        private static readonly byte[] RequestOriginal = { 0x51, 0x53, 0x55, 0x56, 0x57 };
        private const int RequestCaveLength = 51;
        private static byte[] RequestPatched(int requestVa, int caveVa)
        {
            byte[] bytes = { 0xE9, 0, 0, 0, 0 };
            BitConverter.GetBytes(caveVa - (requestVa + 5)).CopyTo(bytes, 1);
            return bytes;
        }
        private static byte[] RequestCave(int caveVa, int checkVa, int requestVa, int eraseVa)
        {
            byte[] bytes =
            {
                0xE8, 0, 0, 0, 0, 0x58, 0x8B, 0x90, 0, 0, 0, 0, 0x8B, 0x12, 0x8B, 0x80, 0, 0, 0, 0, 0x2B, 0x10, 0x7E, 0x0A,
                0x51, 0x53, 0x55, 0x56, 0x57, 0xE9, 0, 0, 0, 0, 0x8B, 0x44, 0x24, 0x04, 0xFF, 0x30, 0x8D, 0x49, 0xFC, 0xE8, 0, 0, 0, 0, 0xC2, 0x04, 0x00,
            };
            int here = caveVa + 5;
            BitConverter.GetBytes(checkVa + 1 - here).CopyTo(bytes, 8);
            BitConverter.GetBytes(checkVa + 7 - here).CopyTo(bytes, 16);
            BitConverter.GetBytes(requestVa + 5 - (caveVa + 34)).CopyTo(bytes, 30);
            BitConverter.GetBytes(eraseVa - (caveVa + 48)).CopyTo(bytes, 44);
            return bytes;
        }

        /* A character's collision filter: alive, it is layer 5 (an NPC) or 25 (the player). Characters given one system group of their own
           are decided between each other by their subsystem ids alone (a matching "don't collide with" turns a pair off); everything
           else (the world, triggers, rays, ragdolls) still goes by the layer table */
        private static readonly byte[] CollideOriginal = { 0xF7, 0xD8, 0x1B, 0xC0, 0x83, 0xE0, 0x14, 0x83, 0xC0, 0x05, 0xEB, 0x05 };
        private const uint CharacterGroup = 0xFFFE;
        private static uint FilterInfo(uint layer, uint subsystem, uint dontCollideWith) => layer | (dontCollideWith << 10) | (subsystem << 5) | (CharacterGroup << 16);

        /* call cave / jmp past the dead layer / nop x5 */
        private static byte[] CollidePatched(int siteVa, int caveVa)
        {
            byte[] bytes = { 0xE8, 0, 0, 0, 0, 0xEB, 0x0A, 0x90, 0x90, 0x90, 0x90, 0x90 };
            BitConverter.GetBytes(caveVa - (siteVa + 5)).CopyTo(bytes, 1);
            return bytes;
        }

        /* eax: whether it is the player. test eax,eax / mov eax,npc / je +5 / mov eax,player / ret */
        private static byte[] CollideCave(uint npc, uint player)
        {
            byte[] bytes = { 0x85, 0xC0, 0xB8, 0, 0, 0, 0, 0x74, 0x05, 0xB8, 0, 0, 0, 0, 0xC3 };
            BitConverter.GetBytes(npc).CopyTo(bytes, 3);
            BitConverter.GetBytes(player).CopyTo(bytes, 10);
            return bytes;
        }

        private static (uint npc, uint player) CollideInfos(bool npcs, bool player)
        {
            if (!npcs && !player) return (FilterInfo(5, 1, 1), FilterInfo(25, 1, 1));
            if (!npcs) return (FilterInfo(5, 1, 1), 25);
            return (FilterInfo(5, 1, 2), FilterInfo(25, 2, 1));
        }

        private static (bool npcs, bool player)? CollideModeOf(byte[] cave)
        {
            if (cave.All(o => o == 0xCC)) return (true, true);
            uint npc = BitConverter.ToUInt32(cave, 3), player = BitConverter.ToUInt32(cave, 10);
            foreach ((bool npcs, bool player) mode in new[] { (false, false), (false, true), (true, false) })
            {
                (uint n, uint p) = CollideInfos(mode.npcs, mode.player);
                if (cave.SequenceEqual(CollideCave(n, p))) return mode;
            }
            return null;
        }
        #endregion

        #region READING AND WRITING
        /// <summary>What the game's binary is set up with now.</summary>
        public sealed class State
        {
            public bool Readable;
            public int? HeapMB;                 //null: the setting's own
            public bool NpcCollisions = true, PlayerCollisions = true;
            public bool SpawnGuard;
            public string Playlist;             //the playlist a launch put in, or null
            public bool AnyLevelSetting => HeapMB != null || !NpcCollisions || !PlayerCollisions || Playlist != null;
        }

        private sealed class Exe
        {
            public byte[] Bytes;
            public uint ImageBase;
            public List<(uint va, uint size, uint raw, uint rawSize)> Sections = new List<(uint, uint, uint, uint)>();
            public int ToVa(int fileOffset)
            {
                foreach ((uint va, uint size, uint raw, uint rawSize) in Sections)
                    if (fileOffset >= raw && fileOffset < raw + rawSize)
                        return (int)(ImageBase + va + (fileOffset - raw));
                throw new InvalidDataException("offset 0x" + fileOffset.ToString("X") + " is in no section");
            }
            public byte[] Read(int offset, int count)
            {
                byte[] bytes = new byte[count];
                Buffer.BlockCopy(Bytes, offset, bytes, 0, count);
                return bytes;
            }
        }

        private static Exe Load(string pathToAI)
        {
            Exe exe = new Exe() { Bytes = File.ReadAllBytes(Path.Combine(pathToAI, "AI.exe")) };
            int pe = BitConverter.ToInt32(exe.Bytes, 0x3C);
            int sections = BitConverter.ToUInt16(exe.Bytes, pe + 6), optional = BitConverter.ToUInt16(exe.Bytes, pe + 20);
            exe.ImageBase = BitConverter.ToUInt32(exe.Bytes, pe + 24 + 28);
            for (int i = 0; i < sections; i++)
            {
                int at = pe + 24 + optional + i * 40;
                exe.Sections.Add((BitConverter.ToUInt32(exe.Bytes, at + 12), BitConverter.ToUInt32(exe.Bytes, at + 8), BitConverter.ToUInt32(exe.Bytes, at + 20), BitConverter.ToUInt32(exe.Bytes, at + 16)));
            }
            return exe;
        }

        private static bool Same(byte[] a, byte[] b) => a.Length == b.Length && a.SequenceEqual(b);
        private static byte[] Fill(byte value, int count) => Enumerable.Repeat(value, count).ToArray();

        private static string SlotText(byte[] slot)
        {
            int end = Array.IndexOf(slot, (byte)0);
            if (end < 0) return null;
            string text = Encoding.ASCII.GetString(slot, 0, end);
            for (int i = end; i < slot.Length; i++) if (slot[i] != 0) return null;
            return text;
        }

        private static byte[] SlotBytes(string text)
        {
            byte[] bytes = new byte[SlotOriginalText.Length + 1];
            Encoding.ASCII.GetBytes(text).CopyTo(bytes, 0);
            return bytes;
        }

        /// <summary>What the game's binary holds now. Readable is false when it isn't one of the builds this knows, or a region is neither the game's nor ours.</summary>
        public static State Read(PatchManager.Platform platform, string pathToAI)
        {
            State state = new State();
            Sites sites = For(platform);
            if (sites == null) return state;
            try
            {
                Exe exe = Load(pathToAI);
                byte[] heap = exe.Read(sites.Heap, HeapOriginal.Length);
                if (!Same(heap, HeapOriginal))
                {
                    state.HeapMB = HeapOf(heap);
                    if (state.HeapMB == null) return state;
                }
                (bool npcs, bool player)? mode = CollideModeOf(exe.Read(sites.CollideCave, 15));
                if (mode == null) return state;
                state.NpcCollisions = mode.Value.npcs;
                state.PlayerCollisions = mode.Value.player;
                state.SpawnGuard = Same(exe.Read(sites.PoolCheck + PoolPatchAt, PoolOriginal.Length), PoolPatched(exe.ToVa(sites.PoolCheck + PoolPatchAt), exe.ToVa(sites.PoolCave))) &&
                                   Same(exe.Read(sites.Request, RequestOriginal.Length), RequestPatched(exe.ToVa(sites.Request), exe.ToVa(sites.RequestCave)));
                uint slotVa = (uint)exe.ToVa(sites.Slot);
                string slot = SlotText(exe.Read(sites.Slot, SlotOriginalText.Length + 1));
                uint fallback = BitConverter.ToUInt32(exe.Bytes, sites.Fallback);
                if (fallback != sites.FallbackOriginal)
                {
                    int table = Array.IndexOf(sites.TableOriginal, fallback);
                    state.Playlist = fallback == slotVa ? slot : table >= 0 ? Table[table].Playlist : null;
                }
                state.Readable = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("LevelLaunchPatches::Read - " + e.Message);
            }
            return state;
        }

        /// <summary>
        /// Write AI.exe for a launch into a level: its heap, collisions and loading playlist as its settings say (the game's own where
        /// they say nothing), and the spawn guard as asked. Returns what went wrong, or null.
        /// </summary>
        public static string Write(PatchManager.Platform platform, string pathToAI, string level, LevelLaunchSettings settings, bool spawnGuard)
        {
            Sites sites = For(platform);
            if (sites == null)
                return "this version of the game (" + platform + ") can't take launch settings";
            settings = settings ?? new LevelLaunchSettings();
            try
            {
                Exe exe = Load(pathToAI);
                List<KeyValuePair<int, byte[]>> writes = new List<KeyValuePair<int, byte[]>>();
                List<string> problems = new List<string>();

                //The heap: the game's code, or ours with the level's size
                byte[] heap = exe.Read(sites.Heap, HeapOriginal.Length);
                if (!Same(heap, HeapOriginal) && HeapOf(heap) == null)
                    problems.Add("the texture heap code is not as this build ships it");
                else
                    writes.Add(new KeyValuePair<int, byte[]>(sites.Heap, settings.TextureHeapMB == null ? HeapOriginal : HeapPatched(settings.TextureHeapMB.Value)));

                //Collisions: both sites call the cave, or neither does
                int caveVa = exe.ToVa(sites.CollideCave);
                byte[][] collide = { exe.Read(sites.CollideA, 12), exe.Read(sites.CollideB, 12) };
                int[] collideAt = { sites.CollideA, sites.CollideB };
                bool sitesKnown = true;
                for (int i = 0; i < 2; i++)
                    sitesKnown &= Same(collide[i], CollideOriginal) || Same(collide[i], CollidePatched(exe.ToVa(collideAt[i]), caveVa));
                if (!sitesKnown || CollideModeOf(exe.Read(sites.CollideCave, 15)) == null)
                    problems.Add("the character collision code is not as this build ships it");
                else
                {
                    bool npcs = settings.NpcCollisions != false, player = settings.PlayerNpcCollisions != false;
                    if (npcs && player)
                    {
                        //Back to the game's own: the sites first, then the padding
                        for (int i = 0; i < 2; i++) writes.Add(new KeyValuePair<int, byte[]>(collideAt[i], CollideOriginal));
                        writes.Add(new KeyValuePair<int, byte[]>(sites.CollideCave, Fill(0xCC, 15)));
                    }
                    else
                    {
                        (uint n, uint p) = CollideInfos(npcs, player);
                        writes.Add(new KeyValuePair<int, byte[]>(sites.CollideCave, CollideCave(n, p)));
                        for (int i = 0; i < 2; i++) writes.Add(new KeyValuePair<int, byte[]>(collideAt[i], CollidePatched(exe.ToVa(collideAt[i]), caveVa)));
                    }
                }

                //The spawn guard: each cave, then the jump to it
                int poolAt = sites.PoolCheck + PoolPatchAt;
                byte[] pool = exe.Read(poolAt, PoolOriginal.Length), poolCave = exe.Read(sites.PoolCave, PoolCave.Length);
                byte[] poolPatched = PoolPatched(exe.ToVa(poolAt), exe.ToVa(sites.PoolCave));
                byte[] request = exe.Read(sites.Request, RequestOriginal.Length), requestCave = exe.Read(sites.RequestCave, RequestCaveLength);
                byte[] requestPatched = RequestPatched(exe.ToVa(sites.Request), exe.ToVa(sites.RequestCave));
                byte[] requestCaveBytes = RequestCave(exe.ToVa(sites.RequestCave), exe.ToVa(sites.PoolCheck), exe.ToVa(sites.Request), exe.ToVa(sites.Erase));
                if (!(Same(pool, PoolOriginal) || Same(pool, poolPatched)) || !(poolCave.All(o => o == 0xCC) || Same(poolCave, PoolCave)) ||
                    !(Same(request, RequestOriginal) || Same(request, requestPatched)) || !(requestCave.All(o => o == 0xCC) || Same(requestCave, requestCaveBytes)))
                    problems.Add("the character slot code is not as this build ships it");
                else if (spawnGuard)
                {
                    writes.Add(new KeyValuePair<int, byte[]>(sites.PoolCave, PoolCave));
                    writes.Add(new KeyValuePair<int, byte[]>(poolAt, poolPatched));
                    writes.Add(new KeyValuePair<int, byte[]>(sites.RequestCave, requestCaveBytes));
                    writes.Add(new KeyValuePair<int, byte[]>(sites.Request, requestPatched));
                }
                else
                {
                    writes.Add(new KeyValuePair<int, byte[]>(poolAt, PoolOriginal));
                    writes.Add(new KeyValuePair<int, byte[]>(sites.PoolCave, Fill(0xCC, PoolCave.Length)));
                    writes.Add(new KeyValuePair<int, byte[]>(sites.Request, RequestOriginal));
                    writes.Add(new KeyValuePair<int, byte[]>(sites.RequestCave, Fill(0xCC, RequestCaveLength)));
                }

                //The loading playlist: the fallback push, the table's last entry (levels it doesn't name) and the level's own entry
                uint slotVa = (uint)exe.ToVa(sites.Slot);
                byte[] slot = exe.Read(sites.Slot, SlotOriginalText.Length + 1);
                bool playlistKnown = SlotText(slot) != null;
                uint fallback = BitConverter.ToUInt32(exe.Bytes, sites.Fallback);
                playlistKnown &= fallback == sites.FallbackOriginal || fallback == slotVa || sites.TableOriginal.Contains(fallback);
                for (int i = 0; i < sites.Table.Length; i++)
                {
                    uint now = BitConverter.ToUInt32(exe.Bytes, sites.Table[i]);
                    playlistKnown &= now == sites.TableOriginal[i] || now == slotVa || sites.TableOriginal.Contains(now);
                }
                if (!playlistKnown)
                    problems.Add("the loading playlist code is not as this build ships it");
                else
                {
                    string playlist = string.IsNullOrWhiteSpace(settings.LoadingPlaylist) ? null : settings.LoadingPlaylist.Trim();
                    uint target = 0;
                    if (playlist != null)
                    {
                        int known = Table.ToList().FindIndex(o => o.Playlist == playlist);
                        if (playlist.Length <= MaxPlaylistName)
                            target = slotVa;
                        else if (known >= 0)
                            target = sites.TableOriginal[known];
                        else
                        {
                            problems.Add("the loading playlist name " + playlist + " is longer than " + MaxPlaylistName + " characters, so the game's own plays");
                            playlist = null;
                        }
                    }
                    writes.Add(new KeyValuePair<int, byte[]>(sites.Slot, SlotBytes(playlist != null && target == slotVa ? playlist : SlotOriginalText)));
                    int own = TableIndexOf(level);
                    for (int i = 0; i < sites.Table.Length; i++)
                    {
                        bool redirect = playlist != null && (i == own || i == sites.Table.Length - 1);
                        writes.Add(new KeyValuePair<int, byte[]>(sites.Table[i], BitConverter.GetBytes(redirect ? target : sites.TableOriginal[i])));
                    }
                    writes.Add(new KeyValuePair<int, byte[]>(sites.Fallback, BitConverter.GetBytes(playlist != null ? target : sites.FallbackOriginal)));
                }

                WriteToBinary(pathToAI, exe, writes);
                return problems.Count == 0 ? null : string.Join("; ", problems);
            }
            catch (Exception e)
            {
                return "AI.exe could not be written (is the game open?): " + e.Message;
            }
        }

        /* Writes only what differs, in the order given (code a jump leads to goes in before the jump) */
        private static void WriteToBinary(string pathToAI, Exe exe, List<KeyValuePair<int, byte[]>> writes)
        {
            List<KeyValuePair<int, byte[]>> changed = writes.Where(o => !Same(exe.Read(o.Key, o.Value.Length), o.Value)).ToList();
            if (changed.Count == 0)
                return;
            using (FileStream stream = new FileStream(Path.Combine(pathToAI, "AI.exe"), FileMode.Open, FileAccess.Write, FileShare.None))
            {
                foreach (KeyValuePair<int, byte[]> write in changed)
                {
                    stream.Position = write.Key;
                    stream.Write(write.Value, 0, write.Value.Length);
                }
            }
        }
        #endregion
    }
}
