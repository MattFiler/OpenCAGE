using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Readable names for the game's audio.
    ///
    /// A stream, or a piece of audio inside a soundbank, is filed under a number - its source id, which is also the name of
    /// a loose .wem - and the number tells nobody anything. The sound events that play it are what the scripts, the Sound
    /// Editor and the game's own data call it, so that is the name used. Worked out by resolving every event the level's
    /// sound data declares (about 7,000 of them, in under a tenth of a second) once per sound index, and kept until the index
    /// or the level changes. Audio no event of the level's plays - dialogue picked at runtime, a soundbank of the user's
    /// own - keeps its number.
    /// </summary>
    internal static class SoundNames
    {
        /// <summary>An event that plays a piece of audio, and which of the event's takes it is.</summary>
        public sealed class Use
        {
            public string Event;

            /// <summary>From 1, in the order the Sound Editor lists the event's takes.</summary>
            public int Take;
            public int Takes;

            public override string ToString()
            {
                return Takes > 1 ? Event + " (take " + Take + " of " + Takes + ")" : Event;
            }
        }

        private static readonly object _lock = new object();
        private static WwiseSoundLibrary _builtFor;
        private static int _builtForEvents;
        private static Dictionary<uint, List<Use>> _bySource;

        /// <summary>Every piece of audio the level's events play, with the events that play it (sorted by event name).</summary>
        public static Dictionary<uint, List<Use>> All(WwiseSoundLibrary library)
        {
            List<string> events = SoundEventMetadata.AllEvents();
            lock (_lock)
            {
                if (_bySource != null && ReferenceEquals(_builtFor, library) && _builtForEvents == events.Count)
                    return _bySource;
            }

            Dictionary<uint, List<Use>> bySource = new Dictionary<uint, List<Use>>();
            foreach (string name in events.OrderBy(o => o, StringComparer.OrdinalIgnoreCase))
            {
                List<WwiseSoundVariation> takes;
                try
                {
                    takes = library.Resolve(name).Variations;
                }
                catch
                {
                    continue;
                }

                for (int i = 0; i < takes.Count; i++)
                {
                    List<Use> uses;
                    if (!bySource.TryGetValue(takes[i].SourceId, out uses))
                        bySource.Add(takes[i].SourceId, uses = new List<Use>());
                    uses.Add(new Use { Event = name, Take = i + 1, Takes = takes.Count });
                }
            }

            lock (_lock)
            {
                _bySource = bySource;
                _builtFor = library;
                _builtForEvents = events.Count;
            }
            return bySource;
        }

        /// <summary>The events that play a piece of audio; empty when none of the level's does.</summary>
        public static IList<Use> Playing(WwiseSoundLibrary library, uint sourceId)
        {
            List<Use> uses;
            return library != null && All(library).TryGetValue(sourceId, out uses) ? uses : (IList<Use>)new Use[0];
        }

        /// <summary>
        /// A piece of audio by the event that plays it - "Play_Pistol_Fire (take 3 of 19)", and "+2 more" when other events
        /// play it too - or null when none of the level's events does.
        /// </summary>
        public static string Describe(WwiseSoundLibrary library, uint sourceId)
        {
            IList<Use> uses = Playing(library, sourceId);
            if (uses.Count == 0)
                return null;

            return uses.Count == 1 ? uses[0].ToString() : uses[0] + " +" + (uses.Count - 1) + " more";
        }

        /// <summary>
        /// A soundbank by name, from its id: as the level's sound data spells it (M04_WelcomeToSevastopol), else the name the
        /// bank gives itself, else its file in DATA/SOUND - and only when none of those knows it, the number.
        /// </summary>
        public static string BankName(uint bankId, byte[] bank)
        {
            string name = SoundEventMetadata.BankName(bankId);
            if (name != null)
                return name;

            if (bank != null)
            {
                try
                {
                    if (WwiseSoundBank.Load(new MemoryStream(bank), 0, bank.Length, "", "").BankNames.TryGetValue(bankId, out name) && !string.IsNullOrEmpty(name))
                        return name;
                }
                catch
                {
                }
            }

            string file = LevelSoundOverride.BaseBankFile(bankId, null);
            return file != null ? Path.GetFileNameWithoutExtension(file) : "Soundbank " + bankId;
        }
    }
}
