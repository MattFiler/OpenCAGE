using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Which DATA/SOUND soundbanks carry each hierarchy object (events, actions, sounds, containers), and which events play each sound
    /// or container. Unlike <see cref="WwiseSoundLibrary"/>, which keeps one copy of a shared object, this lists every bank: an event in
    /// several banks must be changed in each one a level can load, or whichever loads first wins. Built from the hierarchy chunks only,
    /// once per game folder, and rebuilt when its banks change.
    /// </summary>
    internal sealed class SoundBankIndex
    {
        public sealed class BankFile
        {
            public string File;
            public uint Id;
            public uint Version;
            public long Length;

            /// <summary>Loaded as the game starts and kept loaded (Init, UI, DATA/LIST_OF_PERMANENT_SOUND_BANKS.TXT): no level can stand in for it.</summary>
            public bool Permanent;

            public string Name { get { return Path.GetFileNameWithoutExtension(File); } }
        }

        public readonly List<BankFile> Banks = new List<BankFile>();
        private readonly Dictionary<uint, List<int>> _objects = new Dictionary<uint, List<int>>();
        private readonly Dictionary<uint, List<uint>> _playedBy = new Dictionary<uint, List<uint>>();

        private static readonly object _lock = new object();
        private static SoundBankIndex _cached;
        private static string _cachedFor;

        /// <summary>The index of a game's SOUND folder, built on first use and again whenever its soundbanks change.</summary>
        public static SoundBankIndex For(string soundDirectory)
        {
            if (string.IsNullOrEmpty(soundDirectory) || !Directory.Exists(soundDirectory))
                return new SoundBankIndex();

            string key = Fingerprint(soundDirectory);
            lock (_lock)
            {
                if (_cached != null && _cachedFor == key)
                    return _cached;
            }
            SoundBankIndex index = Build(soundDirectory);
            lock (_lock)
            {
                _cached = index;
                _cachedFor = key;
            }
            return index;
        }

        private static string Fingerprint(string soundDirectory)
        {
            long length = 0, newest = 0;
            int count = 0;
            foreach (FileInfo file in new DirectoryInfo(soundDirectory).EnumerateFiles("*.bnk"))
            {
                count++;
                length += file.Length;
                newest = Math.Max(newest, file.LastWriteTimeUtc.Ticks);
            }
            return Path.GetFullPath(soundDirectory).ToUpperInvariant() + "|" + count + "|" + length + "|" + newest;
        }

        private static SoundBankIndex Build(string soundDirectory)
        {
            SoundBankIndex index = new SoundBankIndex();
            foreach (string file in Directory.EnumerateFiles(soundDirectory, "*.bnk").OrderBy(o => o, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    index.Read(file, soundDirectory);
                }
                catch (Exception e)
                {
                    Debug.Log("Sound", "The soundbank index skipped " + Path.GetFileName(file) + ": " + e.Message);
                }
            }
            return index;
        }

        /// <summary>One bank: its header, then its hierarchy chunk only.</summary>
        private void Read(string file, string soundDirectory)
        {
            using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                BankFile bank = new BankFile { File = file, Length = stream.Length };
                long position = 0;
                byte[] hirc = null;
                while (position + 8 <= stream.Length)
                {
                    stream.Position = position;
                    string tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    uint size = reader.ReadUInt32();
                    if (position + 8 + size > stream.Length)
                        break;
                    if (tag == "BKHD" && size >= 8)
                    {
                        bank.Version = reader.ReadUInt32();
                        bank.Id = reader.ReadUInt32();
                    }
                    else if (tag == "HIRC")
                        hirc = reader.ReadBytes((int)size);
                    position += 8 + size;
                }
                if (bank.Id == 0)
                    return;

                bank.Permanent = LevelSoundOverride.IsPermanent(bank.Id, soundDirectory);
                int at = Banks.Count;
                Banks.Add(bank);
                if (hirc == null || hirc.Length < 4)
                    return;

                //Every object's id; for the events, what their Play actions play
                Dictionary<uint, uint> plays = new Dictionary<uint, uint>();
                List<KeyValuePair<uint, int>> events = new List<KeyValuePair<uint, int>>();
                int count = BitConverter.ToInt32(hirc, 0), p = 4;
                bool modern = bank.Version >= 134;
                for (int i = 0; i < count && p + 9 <= hirc.Length; i++)
                {
                    byte type = hirc[p];
                    int length = BitConverter.ToInt32(hirc, p + 1);
                    uint id = BitConverter.ToUInt32(hirc, p + 5);
                    if (length < 4 || p + 5 + length > hirc.Length)
                        break;
                    List<int> where;
                    if (!_objects.TryGetValue(id, out where))
                        _objects.Add(id, where = new List<int>());
                    if (where.Count == 0 || where[where.Count - 1] != at)
                        where.Add(at);

                    int body = p + 9;
                    if (type == (byte)WwiseObjectType.Action && length >= 10)
                    {
                        int kind = BitConverter.ToUInt16(hirc, body) >> 8;
                        if (kind == 0x04 || kind == 0x05)
                            plays[id] = BitConverter.ToUInt32(hirc, body + 2);
                    }
                    else if (type == (byte)WwiseObjectType.Event)
                        events.Add(new KeyValuePair<uint, int>(id, body));
                    p += 5 + length;
                }

                foreach (KeyValuePair<uint, int> ev in events)
                {
                    int body = ev.Value;
                    uint actions = modern ? hirc[body] : BitConverter.ToUInt32(hirc, body);
                    int first = body + (modern ? 1 : 4);
                    for (int i = 0; i < actions && first + 4 * i + 4 <= hirc.Length; i++)
                    {
                        uint target;
                        if (!plays.TryGetValue(BitConverter.ToUInt32(hirc, first + 4 * i), out target))
                            continue;
                        List<uint> by;
                        if (!_playedBy.TryGetValue(target, out by))
                            _playedBy.Add(target, by = new List<uint>());
                        if (!by.Contains(ev.Key))
                            by.Add(ev.Key);
                    }
                }
            }
        }

        /// <summary>The banks carrying an object with this id (an event, an action, a sound...).</summary>
        public IEnumerable<BankFile> BanksWith(uint id)
        {
            List<int> where;
            return _objects.TryGetValue(id, out where) ? where.Select(o => Banks[o]) : Enumerable.Empty<BankFile>();
        }

        /// <summary>Whether any of the game's banks has an object with this id.</summary>
        public bool Has(uint id)
        {
            return _objects.ContainsKey(id);
        }

        /// <summary>The events (ids) whose Play actions play this sound or container.</summary>
        public IList<uint> EventsPlaying(uint id)
        {
            List<uint> by;
            return _playedBy.TryGetValue(id, out by) ? by : (IList<uint>)new uint[0];
        }
    }
}
