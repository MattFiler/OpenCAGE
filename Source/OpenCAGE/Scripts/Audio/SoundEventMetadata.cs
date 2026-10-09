using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// What the level's own data says about a sound event.
    ///
    /// This is the game's side of the story rather than Wwise's: SOUNDEVENTDATA lists which soundbank
    /// each event belongs to, and SOUNDBANKDATA turns the hashed bank id back into a name. Both are read
    /// through CathodeLib's parsers, which is where all of the editor's knowledge about the level's sound
    /// setup comes from - the banks themselves are only consulted for the audio.
    /// </summary>
    public static class SoundEventMetadata
    {
        private static readonly object _lock = new object();
        private static Dictionary<string, List<string>> _banksByEvent;
        private static Dictionary<string, SoundEventData.Soundbank.Event> _infoByEvent;
        private static SoundEventData _cachedFor;

        /// <summary>
        /// The soundbanks an event is declared in, by name. Empty when the level isn't loaded or the
        /// event isn't in its data.
        /// </summary>
        public static List<string> BanksFor(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return new List<string>();

            Dictionary<string, List<string>> lookup = Lookup();
            if (lookup == null)
                return new List<string>();

            List<string> banks;
            return lookup.TryGetValue(eventName, out banks) ? banks : new List<string>();
        }

        /// <summary>
        /// What the level's sound data says about one event beyond its banks: how far away it can be heard
        /// (max_attenuation, in metres) and its metadata string. Null when the level isn't loaded or the event
        /// isn't in its data. Names are matched ignoring case, as Wwise hashes them.
        /// </summary>
        public static SoundEventData.Soundbank.Event InfoFor(string eventName)
        {
            if (string.IsNullOrEmpty(eventName) || Lookup() == null)
                return null;

            lock (_lock)
            {
                SoundEventData.Soundbank.Event info;
                return _infoByEvent != null && _infoByEvent.TryGetValue(eventName, out info) ? info : null;
            }
        }

        /// <summary>Every event name the level's sound data declares (each once).</summary>
        public static List<string> AllEvents()
        {
            Dictionary<string, List<string>> lookup = Lookup();
            return lookup == null ? new List<string>() : lookup.Keys.ToList();
        }

        /// <summary>Drop the cache, so the next lookup reads the level again.</summary>
        public static void Invalidate()
        {
            lock (_lock)
            {
                _banksByEvent = null;
                _infoByEvent = null;
                _cachedFor = null;
            }
        }

        private static Dictionary<string, List<string>> Lookup()
        {
            LevelContent content = Singleton.Editor == null || Singleton.Editor.CompositeBrowser == null
                ? null
                : Singleton.Editor.CompositeBrowser.Content;

            if (content == null || content.Level == null)
                return null;

            SoundEventData events = content.Level.SoundEventData;
            SoundBankData banks = content.Level.SoundBankData;
            if (events == null || banks == null)
                return null;

            lock (_lock)
            {
                //The data is the same for every level, but the object isn't, so it is rebuilt on reload
                if (_banksByEvent != null && ReferenceEquals(_cachedFor, events))
                    return _banksByEvent;

                Dictionary<uint, string> namesById = new Dictionary<uint, string>();
                foreach (SoundBankData.SoundBank bank in banks.Entries)
                {
                    if (string.IsNullOrEmpty(bank.Name))
                        continue;

                    namesById[Utilities.SoundHashedString(bank.Name)] = bank.Name;
                }

                //Wwise hashes an event's name lowercased, so a script naming it in another case still plays it
                Dictionary<string, List<string>> lookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, SoundEventData.Soundbank.Event> info = new Dictionary<string, SoundEventData.Soundbank.Event>(StringComparer.OrdinalIgnoreCase);
                foreach (SoundEventData.Soundbank bank in events.Entries)
                {
                    string name;
                    if (!namesById.TryGetValue(bank.id, out name))
                        name = bank.id.ToString();

                    foreach (SoundEventData.Soundbank.Event e in bank.events)
                    {
                        List<string> found;
                        if (!lookup.TryGetValue(e.name, out found))
                        {
                            found = new List<string>();
                            lookup.Add(e.name, found);
                        }

                        if (!found.Contains(name))
                            found.Add(name);
                        if (!info.ContainsKey(e.name))
                            info.Add(e.name, e);
                    }
                }

                _banksByEvent = lookup;
                _infoByEvent = info;
                _cachedFor = events;
                return _banksByEvent;
            }
        }
    }
}
