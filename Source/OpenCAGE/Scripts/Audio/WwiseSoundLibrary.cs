using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.Audio
{
    /// <summary>Why an event did or didn't produce anything to listen to.</summary>
    public enum WwiseEventOutcome
    {
        Resolved,

        /// <summary>No event by that name exists in any bank.</summary>
        NotInBanks,

        /// <summary>A dynamic dialogue event, whose line is chosen at runtime from its arguments.</summary>
        DialogueEvent,

        /// <summary>The event exists but carries no actions at all.</summary>
        NoActions,

        /// <summary>The event only stops sounds or changes settings; it never starts anything.</summary>
        NoPlayAction,

        /// <summary>It plays something, but that something isn't in any shipped bank.</summary>
        TargetMissing,

        /// <summary>It resolves to sounds, but their audio isn't shipped.</summary>
        MediaMissing,
    }

    /// <summary>Everything an event turned out to play, and an explanation when it plays nothing.</summary>
    public sealed class WwiseEventResolution
    {
        public List<WwiseSoundVariation> Variations = new List<WwiseSoundVariation>();
        public WwiseEventOutcome Outcome = WwiseEventOutcome.NotInBanks;

        /// <summary>The kinds of action the event does carry, when none of them is Play.</summary>
        public List<string> Actions = new List<string>();

        public bool HasAudio
        {
            get { return Variations.Count != 0; }
        }

        /// <summary>A sentence for the user explaining why there is nothing to hear.</summary>
        public string Explanation
        {
            get
            {
                switch (Outcome)
                {
                    case WwiseEventOutcome.Resolved:
                        return "";
                    case WwiseEventOutcome.DialogueEvent:
                        return "Dynamic dialogue - the line depends on who is speaking, so it can't be previewed.";
                    case WwiseEventOutcome.NoActions:
                        return "This event is empty - it carries no actions at all.";
                    case WwiseEventOutcome.NoPlayAction:
                        return Actions.Count == 0
                            ? "This event changes sound settings rather than playing anything."
                            : "This event doesn't start a sound - it only " + Describe(Actions) + ".";
                    case WwiseEventOutcome.TargetMissing:
                        return "The sound this event plays isn't in any of the game's soundbanks.";
                    case WwiseEventOutcome.MediaMissing:
                        return "This event's audio isn't shipped with the game.";
                    default:
                        return "This event isn't in any of the game's soundbanks.";
                }
            }
        }

        private static string Describe(List<string> actions)
        {
            if (actions.Count == 1)
                return actions[0];

            return string.Join(", ", actions.Take(actions.Count - 1).ToArray()) + " and " + actions[actions.Count - 1];
        }
    }

    /// <summary>One playable piece of audio that an event can end up producing.</summary>
    public sealed class WwiseSoundVariation
    {
        public uint SoundId;
        public uint SourceId;
        public WwiseMediaLocation Media;

        /// <summary>The containers walked through to reach this sound, outermost first.</summary>
        public string Path = "";

        /// <summary>The bank the sound object was declared in.</summary>
        public string Bank = "";

        public bool IsStreamed;

        public override string ToString()
        {
            return SourceId.ToString();
        }
    }

    /// <summary>
    /// The whole of the game's Wwise content, indexed so that a sound event name can be turned into the
    /// audio it plays.
    ///
    /// Everything here comes from the Wwise files themselves - the banks, the file packages and the loose
    /// streams. Which bank an event belongs to comes from CathodeLib's own parsers; the .txt and .xml
    /// files Wwise leaves next to the banks are build artefacts and are never read.
    ///
    /// Indexing is deliberately whole-game rather than per-bank. Events are duplicated across every bank
    /// that needs them and containers routinely reference objects declared elsewhere, so resolving inside
    /// a single bank leaves dead ends; parsing all of them costs a couple of seconds once, because the
    /// DATA chunks - which are 982MB of the 1GB on disk - are located rather than read.
    /// </summary>
    public sealed class WwiseSoundLibrary
    {
        private readonly Dictionary<uint, List<WwiseObject>> _objects = new Dictionary<uint, List<WwiseObject>>();
        private readonly Dictionary<uint, List<uint>> _children = new Dictionary<uint, List<uint>>();
        private readonly Dictionary<uint, WwiseMediaLocation> _media = new Dictionary<uint, WwiseMediaLocation>();

        public string SoundDirectory { get; private set; }
        public string Language { get; private set; }
        public string LevelOverride { get; private set; }

        /// <summary>Banks that failed to parse, for diagnostics. Never fatal.</summary>
        public List<string> Failures = new List<string>();

        public int BankCount { get; private set; }
        public int MediaCount { get { return _media.Count; } }
        public int ObjectCount { get; private set; }

        private WwiseSoundLibrary()
        {
        }

        #region BUILDING

        /// <summary>
        /// Index every bank, package and loose stream under a game's SOUND folder.
        /// </summary>
        /// <param name="soundDirectory">DATA/SOUND.</param>
        /// <param name="language">Which localised folder to take dialogue from, e.g. "English(US)".</param>
        /// <param name="levelOverridePck">
        /// A level's own level_sound_override.pck, if it has one. Indexed last so that it wins.
        /// </param>
        public static WwiseSoundLibrary Build(string soundDirectory, string language, string levelOverridePck, Action<string> progress = null)
        {
            WwiseSoundLibrary library = new WwiseSoundLibrary();
            library.SoundDirectory = soundDirectory;
            library.Language = language;
            library.LevelOverride = levelOverridePck;

            if (!Directory.Exists(soundDirectory))
                return library;

            //Loose streams first - they are the lowest priority, and anything in a bank or a level
            //override of the same id is meant to replace them
            if (progress != null) progress("Indexing streamed audio...");
            library.IndexLooseStreams(soundDirectory, language);

            if (progress != null) progress("Indexing sound packages...");
            foreach (string package in library.PackagesFor(soundDirectory, language))
                library.IndexPackage(package, false);

            if (progress != null) progress("Indexing soundbanks...");
            foreach (string bank in library.BanksFor(soundDirectory, language))
                library.IndexBank(bank);

            //The level's own package last, so its media replaces anything shipped globally
            if (!string.IsNullOrEmpty(levelOverridePck) && File.Exists(levelOverridePck))
            {
                if (progress != null) progress("Indexing level sound override...");
                library.IndexPackage(levelOverridePck, true);
            }

            library.BuildChildMap();
            return library;
        }

        /// <summary>
        /// Loose .wem files sitting in the SOUND folder, plus those in the chosen language's folder.
        /// Other languages are skipped so that one event doesn't resolve to seven copies of itself.
        /// </summary>
        private void IndexLooseStreams(string soundDirectory, string language)
        {
            AddLooseStreams(soundDirectory, "SOUND");

            foreach (string directory in Directory.GetDirectories(soundDirectory))
            {
                if (!IsChosenLanguage(Path.GetFileName(directory), language))
                    continue;

                AddLooseStreams(directory, Path.GetFileName(directory));
            }
        }

        private void AddLooseStreams(string directory, string origin)
        {
            foreach (string file in Directory.GetFiles(directory, "*.wem"))
            {
                uint id;
                if (!uint.TryParse(Path.GetFileNameWithoutExtension(file), out id))
                    continue;

                _media[id] = new WwiseMediaLocation
                {
                    File = file,
                    Offset = 0,
                    Length = (int)new FileInfo(file).Length,
                    Origin = origin,
                };
            }
        }

        private IEnumerable<string> PackagesFor(string soundDirectory, string language)
        {
            foreach (string file in Directory.GetFiles(soundDirectory, "*.pck", SearchOption.AllDirectories))
            {
                //The dialogue packages are named for their language; take only the one we want
                string name = Path.GetFileNameWithoutExtension(file);
                int split = name.LastIndexOf('_');
                string prefix = split > 0 ? name.Substring(0, split) : name;

                if (LooksLikeLanguage(prefix) && !IsChosenLanguage(prefix, language))
                    continue;

                yield return file;
            }
        }

        private IEnumerable<string> BanksFor(string soundDirectory, string language)
        {
            foreach (string file in Directory.GetFiles(soundDirectory, "*.bnk", SearchOption.AllDirectories))
            {
                string parent = Path.GetFileName(Path.GetDirectoryName(file));
                if (LooksLikeLanguage(parent) && !IsChosenLanguage(parent, language))
                    continue;

                yield return file;
            }
        }

        private static bool LooksLikeLanguage(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            //The localised folders are all "Language(Region)" or a bare language name; the only other
            //thing that sits beside them is the SOUND folder itself
            return name.IndexOf('(') != -1
                || name.Equals("German", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Russian", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Italian", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsChosenLanguage(string name, string language)
        {
            if (!LooksLikeLanguage(name))
                return false;

            return string.Equals(name, language, StringComparison.OrdinalIgnoreCase);
        }

        private void IndexBank(string path)
        {
            try
            {
                Ingest(WwiseSoundBank.Load(path), false);
            }
            catch (Exception e)
            {
                Failures.Add(Path.GetFileName(path) + ": " + e.Message);
            }
        }

        private void IndexPackage(string path, bool overrides)
        {
            WwiseFilePackage package;
            try
            {
                package = WwiseFilePackage.Load(path);
            }
            catch (Exception e)
            {
                Failures.Add(Path.GetFileName(path) + ": " + e.Message);
                return;
            }

            string origin = Path.GetFileNameWithoutExtension(path);

            foreach (WwiseFilePackage.Entry stream in package.Streams)
            {
                if (!overrides && _media.ContainsKey(stream.Id))
                    continue;

                _media[stream.Id] = new WwiseMediaLocation
                {
                    File = path,
                    Offset = stream.Offset,
                    Length = stream.Length,
                    Origin = origin,
                };
            }

            if (package.Banks.Count == 0)
                return;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    foreach (WwiseFilePackage.Entry entry in package.Banks)
                    {
                        try
                        {
                            Ingest(WwiseSoundBank.Load(stream, entry.Offset, entry.Length, path, origin), overrides);
                        }
                        catch (Exception e)
                        {
                            Failures.Add(origin + " bank " + entry.Id + ": " + e.Message);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Failures.Add(Path.GetFileName(path) + ": " + e.Message);
            }
        }

        private void Ingest(WwiseSoundBank bank, bool overrides)
        {
            BankCount++;

            foreach (KeyValuePair<uint, WwiseMediaLocation> media in bank.EmbeddedMedia)
            {
                if (!overrides && _media.ContainsKey(media.Key))
                    continue;

                _media[media.Key] = media.Value;
            }

            foreach (WwiseObject o in bank.Objects)
            {
                ObjectCount++;

                List<WwiseObject> existing;
                if (!_objects.TryGetValue(o.Id, out existing))
                {
                    existing = new List<WwiseObject>();
                    _objects.Add(o.Id, existing);
                }

                //An object is repeated in every bank that needs it, but the copies are NOT always
                //identical: measured across the game, 41% of sound ids and 16% of action ids differ
                //between banks - a sound hangs off a different container, an action points somewhere
                //else. Keeping only the first copy silently loses those, which is what left whole
                //events resolving to nothing. Identical copies are still collapsed, by content.
                long signature = Signature(o);
                if (existing.Any(e => Signature(e) == signature))
                    continue;

                existing.Add(o);
            }
        }

        /// <summary>
        /// Everything a container holds, found by inverting parent ids across the whole index.
        ///
        /// Only ids are stored, deduplicated: one child can be present several times over - once per
        /// variant of it that survived ingest - and walking the same child repeatedly turns a deep
        /// container into an exponential amount of work for no extra sounds.
        /// </summary>
        private void BuildChildMap()
        {
            foreach (List<WwiseObject> group in _objects.Values)
            {
                foreach (WwiseObject o in group)
                {
                    if (o.ParentId == 0)
                        continue;

                    List<uint> siblings;
                    if (!_children.TryGetValue(o.ParentId, out siblings))
                    {
                        siblings = new List<uint>();
                        _children.Add(o.ParentId, siblings);
                    }

                    if (!siblings.Contains(o.Id))
                        siblings.Add(o.Id);
                }
            }
        }

        /// <summary>
        /// Identifies what an object actually says, so that repeated copies of the same object collapse
        /// but genuinely different ones do not.
        /// </summary>
        private static long Signature(WwiseObject o)
        {
            long signature = ((long)o.Type << 56) ^ o.ParentId;

            WwiseSound sound = o as WwiseSound;
            if (sound != null)
                return signature ^ ((long)sound.SourceId << 16) ^ ((long)sound.StreamType << 48);

            WwiseAction action = o as WwiseAction;
            if (action != null)
                return signature ^ ((long)action.TargetId << 16) ^ ((long)action.ActionType << 40);

            WwiseEvent e = o as WwiseEvent;
            if (e != null)
            {
                foreach (uint id in e.ActionIds)
                    signature = signature * 31 + id;

                return signature;
            }

            WwiseMusicTrack track = o as WwiseMusicTrack;
            if (track != null)
            {
                foreach (uint id in track.SourceIds)
                    signature = signature * 31 + id;
            }

            return signature;
        }

        #endregion

        #region RESOLVING

        /// <summary>
        /// Work out everything a sound event can play.
        ///
        /// An event fires a list of actions; the Play ones each target either a sound or a container,
        /// and a container is walked down to the sounds underneath it. A random or switch container
        /// yields several variations, which is why this returns a list rather than one file.
        /// </summary>
        public List<WwiseSoundVariation> ResolveEvent(string eventName)
        {
            return Resolve(eventName).Variations;
        }

        /// <summary>
        /// Resolve an event, and say why if it yields nothing.
        ///
        /// Roughly a quarter of the game's sound events genuinely produce no audio - they stop other
        /// sounds, set a state or a volume, or are empty stubs - so the reason matters. Reporting all of
        /// them as "no audio" makes correct behaviour look like a failure.
        /// </summary>
        public WwiseEventResolution Resolve(string eventName)
        {
            WwiseEventResolution result = new WwiseEventResolution();
            if (string.IsNullOrEmpty(eventName))
                return result;

            uint eventId = CathodeLib.Utilities.SoundHashedString(eventName);

            List<WwiseObject> objects = Find(eventId).ToList();
            if (objects.Count == 0)
                return result;

            if (!objects.Any(o => o is WwiseEvent))
            {
                result.Outcome = objects.Any(o => o.Type == WwiseObjectType.DialogueEvent)
                    ? WwiseEventOutcome.DialogueEvent
                    : WwiseEventOutcome.NotInBanks;

                return result;
            }

            List<WwiseAction> actions = new List<WwiseAction>();
            foreach (WwiseEvent e in objects.OfType<WwiseEvent>())
            {
                foreach (uint actionId in e.ActionIds)
                    actions.AddRange(Find(actionId).OfType<WwiseAction>());
            }

            if (actions.Count == 0)
            {
                result.Outcome = WwiseEventOutcome.NoActions;
                return result;
            }

            List<WwiseAction> plays = actions.Where(a => a.IsPlay).ToList();
            if (plays.Count == 0)
            {
                result.Outcome = WwiseEventOutcome.NoPlayAction;
                foreach (string name in actions.Select(a => DescribeAction(a.ActionType)).Where(n => n != null).Distinct())
                    result.Actions.Add(name);

                return result;
            }

            HashSet<uint> seen = new HashSet<uint>();
            HashSet<uint> walked = new HashSet<uint>();
            foreach (WwiseAction action in plays)
                Collect(action.TargetId, "", result.Variations, seen, walked, 0);

            if (result.Variations.Count != 0)
                result.Outcome = WwiseEventOutcome.Resolved;
            else if (!plays.Any(p => _objects.ContainsKey(p.TargetId)))
                result.Outcome = WwiseEventOutcome.TargetMissing;
            else
                result.Outcome = WwiseEventOutcome.MediaMissing;

            return result;
        }

        /// <summary>Is there anything to preview for this event?</summary>
        public bool CanPreview(string eventName)
        {
            return Resolve(eventName).HasAudio;
        }

        /// <summary>
        /// What an action does, in words. The high byte of the action type is the operation; the low
        /// byte is only its scope, which doesn't matter here.
        /// </summary>
        private static string DescribeAction(ushort actionType)
        {
            switch (actionType >> 8)
            {
                case 0x01: return "stops other sounds";
                case 0x02: return "pauses other sounds";
                case 0x03: return "resumes other sounds";
                case 0x06: return "mutes other sounds";
                case 0x08:
                case 0x0A: return "changes a volume";
                case 0x0B: return "changes a bus volume";
                case 0x0C:
                case 0x0E: return "changes filtering";
                case 0x12: return "sets a state";
                case 0x13: return "sets a switch";
                case 0x1D: return "seeks within a sound";
                case 0x1E: return "sets a game parameter";
                default: return null;
            }
        }

        private IEnumerable<WwiseObject> Find(uint id)
        {
            List<WwiseObject> found;
            if (_objects.TryGetValue(id, out found))
                return found;

            return Enumerable.Empty<WwiseObject>();
        }

        private void Collect(uint id, string path, List<WwiseSoundVariation> results, HashSet<uint> seen, HashSet<uint> walked, int depth)
        {
            //Containers can reference each other in ways that loop; the depth cap is a backstop for
            //anything the walked set doesn't already catch
            if (depth > 12 || id == 0)
                return;

            bool descend = walked.Add(id);

            foreach (WwiseObject o in Find(id))
            {
                WwiseSound sound = o as WwiseSound;
                if (sound != null)
                {
                    AddSound(sound, path, results, seen);
                    continue;
                }

                WwiseMusicTrack track = o as WwiseMusicTrack;
                if (track != null)
                {
                    foreach (uint sourceId in track.SourceIds)
                        AddSource(sourceId, 0, Join(path, "Music"), o.Bank, results, seen);
                    continue;
                }

                if (!descend || !IsContainer(o.Type))
                    continue;

                List<uint> children;
                if (!_children.TryGetValue(id, out children))
                    continue;

                string next = Join(path, Describe(o.Type));
                for (int i = 0; i < children.Count; i++)
                    Collect(children[i], next, results, seen, walked, depth + 1);
            }
        }

        private void AddSound(WwiseSound sound, string path, List<WwiseSoundVariation> results, HashSet<uint> seen)
        {
            if (!sound.HasMedia)
                return; //A tone generator or silence - nothing to play

            AddSource(sound.SourceId, sound.Id, path, sound.Bank, results, seen);
        }

        private void AddSource(uint sourceId, uint soundId, string path, WwiseSoundBank bank, List<WwiseSoundVariation> results, HashSet<uint> seen)
        {
            if (sourceId == 0 || !seen.Add(sourceId))
                return;

            WwiseMediaLocation media;
            if (!_media.TryGetValue(sourceId, out media))
                return; //Media that isn't shipped - a placeholder, or content cut before release

            results.Add(new WwiseSoundVariation
            {
                SoundId = soundId,
                SourceId = sourceId,
                Media = media,
                Path = path,
                Bank = bank != null ? bank.Name : "",
                IsStreamed = !string.Equals(media.File, bank != null ? bank.FilePath : null, StringComparison.OrdinalIgnoreCase),
            });
        }

        private static bool IsContainer(WwiseObjectType type)
        {
            switch (type)
            {
                case WwiseObjectType.RandomSequenceContainer:
                case WwiseObjectType.SwitchContainer:
                case WwiseObjectType.ActorMixer:
                case WwiseObjectType.BlendContainer:
                case WwiseObjectType.MusicSegment:
                case WwiseObjectType.MusicSwitchContainer:
                case WwiseObjectType.MusicRandomSequenceContainer:
                    return true;
                default:
                    return false;
            }
        }

        private static string Describe(WwiseObjectType type)
        {
            switch (type)
            {
                case WwiseObjectType.RandomSequenceContainer: return "Random";
                case WwiseObjectType.SwitchContainer: return "Switch";
                case WwiseObjectType.BlendContainer: return "Blend";
                case WwiseObjectType.ActorMixer: return "Mixer";
                case WwiseObjectType.MusicSegment: return "Segment";
                case WwiseObjectType.MusicSwitchContainer: return "Music Switch";
                case WwiseObjectType.MusicRandomSequenceContainer: return "Music Playlist";
                default: return type.ToString();
            }
        }

        private static string Join(string path, string part)
        {
            return string.IsNullOrEmpty(path) ? part : path + " > " + part;
        }

        #endregion

        /// <summary>Pull the bytes for one piece of audio off disk.</summary>
        public static byte[] ReadMedia(WwiseMediaLocation media)
        {
            if (media == null)
                return null;

            using (FileStream stream = new FileStream(media.File, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                stream.Position = media.Offset;

                byte[] buffer = new byte[media.Length];
                int read = 0;
                while (read < buffer.Length)
                {
                    int got = stream.Read(buffer, read, buffer.Length - read);
                    if (got <= 0)
                        break;
                    read += got;
                }

                if (read == buffer.Length)
                    return buffer;

                Array.Resize(ref buffer, read);
                return buffer;
            }
        }
    }
}
