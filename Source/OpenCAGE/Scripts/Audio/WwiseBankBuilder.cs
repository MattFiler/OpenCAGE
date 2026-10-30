using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Builds a soundbank of the user's own, for a level's sound package: sound events new to the game, each playing audio made
    /// from a .wav, and sounding like a sound of the game's the user picks - its actor-mixer, output bus, 3D positioning and
    /// attenuation, reverb sends and the rest of what the sound designers set up are borrowed from it. No Wwise involved.
    ///
    /// The bank is laid out as the game's own (Wwise bank version 88, measured over all of them - see the HircStudy harnesses):
    /// a header, the audio's directory and data, the object hierarchy, and the bank's own name. Each new event is three objects:
    /// the event (its id the hash of its name, as Wwise and the game both make it), one Play action, and a sound - a copy of the
    /// template's sound object with the new audio as its source. What the template inherited from a container above it (a
    /// random container's positioning, say) is copied onto the sound itself, and the sound hangs off the template's nearest
    /// actor-mixer instead, a copy of which (and of every mixer above it, and the attenuation curves they use) goes in the bank
    /// as the game's banks carry them - each mixer listing only the children in this bank, and children ahead of their parents.
    /// A bank built here can be added to later: its objects are read back and the new events added beside them.
    ///
    /// The iOS and Switch builds are a later Wwise (2019.1, bank version 134), and a bank for one of them is laid out as theirs
    /// are: the same chunks and the same three objects per event, in that generation's object layout - measured over every
    /// sound and actor-mixer those two builds ship (131,291 and 10,351), each of which ends exactly where the layout below says.
    /// What moved is noted where it is read or written; the biggest is that an attenuation is no longer part of the positioning
    /// block but a property beside the volume and pitch, set on the same object as the positioning it goes with.
    ///
    /// The game finds a sound event a script names in its level's sound event table (WORLD/SOUNDEVENTDATA.DAT), not in the
    /// bank - so the events made here also go in that table (see <see cref="LevelSoundOverride"/>), and a SoundLoadBank loads
    /// the bank by name like any other: the game registers a bank it has no entry for as it's first asked for.
    /// </summary>
    internal static class WwiseBankBuilder
    {
        /// <summary>The bank version written when the game's own can't be read: the PC's.</summary>
        public const uint Version = 88;
        private const uint VorbisPlugin = 0x00040001;

        /// <summary>One new event: what it's called, its audio (a .wem, Wwise Vorbis) and the sound of the game's it sounds like.</summary>
        public sealed class NewEvent
        {
            public string Name;
            public byte[] Wem;
            public uint TemplateSourceId;

            /// <summary>Repeat the sound until it's stopped (the .wav's own loop region, if it has one, is what repeats).</summary>
            public bool Loop;
        }

        /// <summary>An event in a bank built here, as the level's sound event table lists it.</summary>
        public sealed class EventInfo
        {
            public string Name;
            public uint EventId;
            public uint SoundId;
            public uint SourceId;

            /// <summary>How far away it can be heard, in metres - the template's events' figure.</summary>
            public float MaxAttenuation = 1024;

            /// <summary>The sound of the game's it was made to sound like, described.</summary>
            public string Template;
        }

        public sealed class Result
        {
            public byte[] Bank;
            public uint BankId;

            /// <summary>The Wwise bank version it was laid out for - the game's own.</summary>
            public uint Version;

            public List<EventInfo> Events = new List<EventInfo>();
            public List<string> Notes = new List<string>();
        }

        /// <summary>One object of a bank's hierarchy, as it's stored.</summary>
        private sealed class HircObject
        {
            public byte Type;
            public uint Id;
            public byte[] Body;
        }

        /// <summary>
        /// What differs between the two bank generations the game ships, for everything here that reads or writes objects.
        /// </summary>
        private sealed class Format
        {
            public uint Version;

            /// <summary>Version 134 (Wwise 2019.1: iOS, Switch) rather than 88 (PC, Mac, Linux).</summary>
            public bool Modern;

            public static Format For(uint version)
            {
                if (version != 88 && version != 134)
                    throw new InvalidDataException("This game's soundbanks are version " + version + ", which a soundbank can't be made for here (only 88, the PC's, Mac's and Linux's, and 134, the iOS and Switch builds').");
                return new Format { Version = version, Modern = version >= 134 };
            }

            /// <summary>The loop count property: 7 in version 88; 0x3A in 134, where 7 is the priority.</summary>
            public byte LoopProperty
            {
                get { return Modern ? (byte)0x3A : (byte)7; }
            }

            /// <summary>Version 134's attenuation property: the attenuation object's id, on the object whose positioning it goes with.</summary>
            public const byte AttenuationProperty = 0x46;

            /// <summary>Whether a property adds to the one below it: volume, pitch and low-pass in both; high-pass too in 134.</summary>
            public bool Additive(byte property)
            {
                return property == 0 || property == 2 || property == 3 || (Modern && property == 4);
            }

            /// <summary>
            /// The kinds of object a sound's or mixer's settings point at that the bank carries along: attenuations, effects and
            /// auxiliary busses. Version 134 numbers them differently - effect share sets 16, custom effects 17, aux busses 18 -
            /// where 88 had effects 18 and aux busses 19 (measured: every reference 134's objects make lands on one of these).
            /// </summary>
            public bool Carried(byte type)
            {
                if (type == (byte)WwiseObjectType.Attenuation)
                    return true;
                return Modern ? type == 16 || type == 17 || type == 18 : type == (byte)WwiseObjectType.Effect || type == (byte)WwiseObjectType.AuxBus;
            }

            /// <summary>
            /// How long a sound's source record is. Version 88: a plugin source 11 bytes, streamed audio 17, audio in a bank 25.
            /// Version 134: 14 bytes flat, a source plugin (type 2) adding a u32-length parameter block.
            /// </summary>
            public int SourceLength(byte[] body)
            {
                uint plugin = BitConverter.ToUInt32(body, 0);
                if (Modern)
                {
                    if ((plugin & 0x0F) == 2 && body.Length >= 18)
                    {
                        uint parameters = BitConverter.ToUInt32(body, 14);
                        if (parameters < 0x1000)
                            return 18 + (int)parameters;
                    }
                    return 14;
                }

                uint stream = BitConverter.ToUInt32(body, 4);
                return (plugin & 0x0F) != 1 ? 11 : stream == (uint)WwiseStreamType.Streamed ? 17 : 25;
            }

            /// <summary>A sound's source id: 8 bytes in in version 88, 5 in 134 (whose stream type is a single byte).</summary>
            public uint SourceId(byte[] body)
            {
                return BitConverter.ToUInt32(body, Modern ? 5 : 8);
            }

            /// <summary>An event's actions: a u32 count in version 88, a u8 count in 134.</summary>
            public IEnumerable<uint> Actions(HircObject ev)
            {
                int header = Modern ? 1 : 4;
                if (ev.Body.Length < header)
                    yield break;
                uint count = Modern ? ev.Body[0] : BitConverter.ToUInt32(ev.Body, 0);
                for (int i = 0; i < count && header + 4 * i + 4 <= ev.Body.Length; i++)
                    yield return BitConverter.ToUInt32(ev.Body, header + 4 * i);
            }

            /// <summary>An event playing one action.</summary>
            public byte[] Event(uint actionId)
            {
                byte[] ev = new byte[Modern ? 5 : 8];
                if (Modern)
                    ev[0] = 1;
                else
                    Put32(ev, 0, 1);
                Put32(ev, Modern ? 1 : 4, actionId);
                return ev;
            }

            /// <summary>Whether this object sets the positioning it (and what's under it) plays with, rather than inheriting it.</summary>
            public bool OverridesPositioning(byte[] body, Node n)
            {
                return Modern ? (body[n.Positioning] & 1) != 0 : body[n.Positioning] != 0;
            }

            /// <summary>Whether this object sets its own auxiliary sends (game-defined or user-defined), or lists aux busses of its own.</summary>
            public bool SetsAux(byte[] body, Node n)
            {
                return Modern ? (body[n.Aux] & 0x0D) != 0 : body[n.Aux] != 0 || body[n.Aux + 2] != 0;
            }
        }

        #region BUILDING

        /// <summary>
        /// A soundbank named <paramref name="bankName"/> holding <paramref name="add"/> - and, given the bytes of one built here
        /// before (<paramref name="existing"/>), its events but those in <paramref name="drop"/>. <paramref name="names"/> gives
        /// the existing events' names by id (the level's sound event table has them; the bank has only their hashes). It is laid
        /// out for the game's own bank version (<paramref name="version"/>, or the game's when 0).
        /// </summary>
        public static Result Build(string bankName, IList<NewEvent> add, WwiseSoundLibrary library, byte[] existing, IDictionary<uint, string> names, ICollection<uint> drop, uint version = 0)
        {
            uint bankId = CathodeLib.Utilities.SoundHashedString(bankName);

            //Laid out as the bank already is, else as the game's own are
            if (existing != null && existing.Length >= 16 && Encoding.ASCII.GetString(existing, 0, 4) == "BKHD")
                version = BitConverter.ToUInt32(existing, 8);
            if (version == 0)
                version = SoundPreviewLibrary.BankVersion;
            if (version == 0)
                version = Version;
            Format format = Format.For(version);

            Result result = new Result { BankId = bankId, Version = version };

            Dictionary<uint, HircObject> objects = new Dictionary<uint, HircObject>();
            Dictionary<uint, byte[]> media = new Dictionary<uint, byte[]>();
            List<uint> events = new List<uint>();
            byte[] header = existing != null ? HeaderAfterLanguage(existing) : null;

            //--- what the bank already holds ---
            if (existing != null)
            {
                if (BitConverter.ToUInt32(existing, 12) != bankId)
                    throw new InvalidDataException("That soundbank isn't called " + bankName + ".");

                foreach (HircObject o in ReadHirc(existing))
                {
                    objects[o.Id] = o;
                    if (o.Type == (byte)WwiseObjectType.Event && (drop == null || !drop.Contains(o.Id)))
                        events.Add(o.Id);
                }
                foreach (KeyValuePair<uint, KeyValuePair<long, int>> m in WwiseMediaInjector.MediaIn(existing))
                {
                    byte[] bytes = new byte[m.Value.Value];
                    Array.Copy(existing, m.Value.Key, bytes, 0, bytes.Length);
                    media[m.Key] = bytes;
                }
            }

            //--- the new events ---
            HashSet<uint> taken = new HashSet<uint>(objects.Keys.Concat(media.Keys));
            HashSet<uint> ownEvents = new HashSet<uint>(objects.Values.Where(o => o.Type == (byte)WwiseObjectType.Event).Select(o => o.Id));
            Dictionary<uint, string> allNames = names == null ? new Dictionary<uint, string>() : new Dictionary<uint, string>(names);
            Dictionary<string, Dictionary<uint, HircObject>> templateBanks = new Dictionary<string, Dictionary<uint, HircObject>>(StringComparer.OrdinalIgnoreCase);
            foreach (NewEvent e in add)
            {
                if (string.IsNullOrWhiteSpace(e.Name))
                    throw new InvalidDataException("Every event needs a name.");
                uint eventId = CathodeLib.Utilities.SoundHashedString(e.Name);
                if (events.Contains(eventId))
                    throw new InvalidDataException("The soundbank already has an event called " + e.Name + ".");
                if (library != null && library.HasEvent(e.Name) && !ownEvents.Contains(eventId))
                    throw new InvalidDataException("The game already has an event called " + e.Name + ": give the new one a name of its own.");

                //An event of this bank's taken out and made again: its old objects go
                if (objects.ContainsKey(eventId))
                    objects.Remove(eventId);

                Template template = FindTemplate(library, e.TemplateSourceId, templateBanks, format);
                if (header == null)
                    header = template.Header;
                uint soundId = NewId(bankName + "/" + e.Name + "/sound", library, taken);
                uint sourceId = NewId(bankName + "/" + e.Name + "/audio", library, taken);
                uint actionId = NewId(bankName + "/" + e.Name + "/play", library, taken);

                //The sound: the template's, with the new audio, hung off the template's actor-mixer
                objects[soundId] = new HircObject { Type = (byte)WwiseObjectType.Sound, Id = soundId, Body = SoundBody(template, sourceId, bankId, e.Loop, format) };
                media[sourceId] = e.Wem;

                //The mixers above it and the curves and effects they use, as the template's bank has them
                foreach (HircObject o in template.Carry)
                    if (!objects.ContainsKey(o.Id))
                        objects[o.Id] = o;

                //Play it: the action names the bank its target is in, as the game's own do. The same fourteen bytes in both
                //generations (type, target, not a bus, no properties, no ranged ones, the fade curve, the bank)
                byte[] play = new byte[14];
                Put16(play, 0, 0x0403);
                Put32(play, 2, soundId);
                play[9] = 4; //fade curve: linear, as every Play action the game ships
                Put32(play, 10, bankId);
                objects[actionId] = new HircObject { Type = (byte)WwiseObjectType.Action, Id = actionId, Body = play };

                objects[eventId] = new HircObject { Type = (byte)WwiseObjectType.Event, Id = eventId, Body = format.Event(actionId) };
                events.Add(eventId);
                allNames[eventId] = e.Name;

                result.Notes.AddRange(template.Notes.Select(n => e.Name + ": " + n));
                result.Events.Add(new EventInfo { Name = e.Name, EventId = eventId, SoundId = soundId, SourceId = sourceId, MaxAttenuation = template.MaxAttenuation, Template = template.Description });
            }

            //--- only what the events still reach, then laid out ---
            Dictionary<uint, HircObject> kept = Reachable(objects, events, format);
            foreach (uint id in events)
            {
                if (result.Events.Any(o => o.EventId == id))
                    continue;
                string name;
                EventInfo info = new EventInfo { Name = allNames.TryGetValue(id, out name) ? name : null, EventId = id };
                HircObject action = format.Actions(objects[id]).Select(a => kept.ContainsKey(a) ? kept[a] : null).FirstOrDefault(a => a != null);
                if (action != null)
                {
                    info.SoundId = BitConverter.ToUInt32(action.Body, 2);
                    HircObject sound;
                    if (kept.TryGetValue(info.SoundId, out sound) && sound.Type == (byte)WwiseObjectType.Sound)
                        info.SourceId = format.SourceId(sound.Body);
                }
                result.Events.Add(info);
            }

            HashSet<uint> sources = new HashSet<uint>(kept.Values.Where(o => o.Type == (byte)WwiseObjectType.Sound).Select(o => format.SourceId(o.Body)));
            result.Bank = Write(bankName, bankId, kept, media.Where(m => sources.Contains(m.Key)).ToDictionary(m => m.Key, m => m.Value), format, header);
            return result;
        }

        /// <summary>
        /// The events and the objects they reach: their actions, the sounds those play, the mixers above the sounds, and the
        /// attenuations and effects any of them use. Each mixer's child list is made again from the children kept.
        /// </summary>
        private static Dictionary<uint, HircObject> Reachable(Dictionary<uint, HircObject> objects, List<uint> events, Format format)
        {
            Dictionary<uint, HircObject> kept = new Dictionary<uint, HircObject>();
            Queue<uint> todo = new Queue<uint>(events);
            while (todo.Count != 0)
            {
                uint id = todo.Dequeue();
                HircObject o;
                if (kept.ContainsKey(id) || !objects.TryGetValue(id, out o))
                    continue;
                kept[id] = o;

                switch ((WwiseObjectType)o.Type)
                {
                    case WwiseObjectType.Event:
                        foreach (uint a in format.Actions(o)) todo.Enqueue(a);
                        break;
                    case WwiseObjectType.Action:
                        todo.Enqueue(BitConverter.ToUInt32(o.Body, 2));
                        break;
                    case WwiseObjectType.Sound:
                    case WwiseObjectType.ActorMixer:
                        Node node = Node.Parse(o.Body, o.Type == (byte)WwiseObjectType.Sound ? format.SourceLength(o.Body) : 0, o.Type, format);
                        if (node == null)
                            break;
                        uint parent = BitConverter.ToUInt32(o.Body, node.Parent);
                        if (parent != 0) todo.Enqueue(parent);
                        foreach (uint r in node.References(o.Body, format)) todo.Enqueue(r);
                        break;
                }
            }

            //Each mixer lists the children this bank holds, sorted, as the game's banks do
            foreach (HircObject mixer in kept.Values.Where(o => o.Type == (byte)WwiseObjectType.ActorMixer).ToList())
            {
                Node node = Node.Parse(mixer.Body, 0, mixer.Type, format);
                if (node == null)
                    continue;
                List<uint> children = kept.Values.Where(o => (o.Type == (byte)WwiseObjectType.Sound || o.Type == (byte)WwiseObjectType.ActorMixer) && o != mixer && ParentOf(o, format) == mixer.Id).Select(o => o.Id).OrderBy(o => o).ToList();
                byte[] body = new byte[node.Children + 4 + 4 * children.Count];
                Array.Copy(mixer.Body, body, node.Children);
                Put32(body, node.Children, (uint)children.Count);
                for (int i = 0; i < children.Count; i++)
                    Put32(body, node.Children + 4 + 4 * i, children[i]);
                kept[mixer.Id] = new HircObject { Type = mixer.Type, Id = mixer.Id, Body = body };
            }
            return kept;
        }

        private static uint ParentOf(HircObject o, Format format)
        {
            Node node = Node.Parse(o.Body, o.Type == (byte)WwiseObjectType.Sound ? format.SourceLength(o.Body) : 0, o.Type, format);
            return node == null ? 0 : BitConverter.ToUInt32(o.Body, node.Parent);
        }

        /// <summary>A hashed id for something new, moved along until nothing in the game or this bank has it.</summary>
        private static uint NewId(string seed, WwiseSoundLibrary library, HashSet<uint> taken)
        {
            for (int n = 0; ; n++)
            {
                uint id = CathodeLib.Utilities.SoundHashedString(n == 0 ? seed : seed + "/" + n);
                if (id == 0 || taken.Contains(id) || (library != null && library.HasId(id)))
                    continue;
                taken.Add(id);
                return id;
            }
        }

        #endregion

        #region THE TEMPLATE

        /// <summary>A sound of the game's to borrow from, and what of its hierarchy goes along with it.</summary>
        private sealed class Template
        {
            public HircObject Sound;
            public Node SoundNode;

            /// <summary>The actor-mixer the new sound hangs off (0 for none), the bus it plays to, and the blocks it inherited.</summary>
            public uint Mixer;
            public uint Bus;
            public byte[] Positioning;
            public byte[] Aux;
            public Dictionary<byte, float> AddedProps = new Dictionary<byte, float>();

            /// <summary>
            /// Version 134: the positioning came from a container above the sound, and with it this attenuation (0 for none) -
            /// which that generation keeps in a property of the object the positioning is set on.
            /// </summary>
            public bool PositioningInherited;
            public uint Attenuation;

            /// <summary>Objects that go in the bank with it: the mixers above it, and the attenuations and effects used.</summary>
            public List<HircObject> Carry = new List<HircObject>();

            /// <summary>The header fields of the template's bank that follow its language, which every bank of the game shares.</summary>
            public byte[] Header;

            public float MaxAttenuation = 1024;
            public string Description;
            public List<string> Notes = new List<string>();
        }

        private static Template FindTemplate(WwiseSoundLibrary library, uint sourceId, Dictionary<string, Dictionary<uint, HircObject>> cache, Format format)
        {
            if (library == null)
                throw new InvalidOperationException("The game's soundbanks aren't indexed.");

            //Each bank's copy of the sound object can differ (a different parent): take one whose bank holds it parseably,
            //preferring the game's own soundbank files over a level's package
            List<WwiseSound> copies = library.SoundsWithSource(sourceId).Where(o => o.HasMedia && o.Bank != null && o.Bank.Version == format.Version)
                .OrderBy(o => string.Equals(Path.GetExtension(o.Bank.FilePath), ".bnk", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
            if (copies.Count == 0)
                throw new InvalidDataException("No soundbank the game ships plays that sound, so there's nothing to borrow from it - pick another.");

            string problem = null;
            Dictionary<string, byte[]> headers = new Dictionary<string, byte[]>();
            foreach (WwiseSound copy in copies)
            {
                Dictionary<uint, HircObject> bank;
                string key = copy.Bank.FilePath + "@" + copy.Bank.BaseOffset;
                if (!cache.TryGetValue(key, out bank))
                {
                    byte[] bytes = BankBytes(copy.Bank);
                    bank = bytes == null ? new Dictionary<uint, HircObject>() : ReadHirc(bytes).GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First());
                    if (bytes != null)
                        headers[key] = HeaderAfterLanguage(bytes);
                    cache[key] = bank;
                }

                HircObject sound;
                if (!bank.TryGetValue(copy.Id, out sound))
                    continue;

                Template template = Borrow(sound, bank, format, out problem);
                if (template == null)
                    continue;

                byte[] header;
                template.Header = headers.TryGetValue(key, out header) ? header : null;

                //How far away it can be heard: what the level's sound data says for an event that plays it
                foreach (SoundNames.Use use in SoundNames.Playing(library, sourceId))
                {
                    CATHODE.SoundEventData.Soundbank.Event info = SoundEventMetadata.InfoFor(use.Event);
                    if (info != null)
                    {
                        template.MaxAttenuation = info.max_attenuation;
                        break;
                    }
                }
                template.Description = (SoundNames.Describe(library, sourceId) ?? "sound " + sourceId) + " in " + copy.Bank.Name;
                return template;
            }

            throw new InvalidDataException("That sound's object in the game's soundbanks can't be borrowed from" + (problem != null ? " (" + problem + ")" : "") + " - pick another.");
        }

        /// <summary>The template's sound and what it needs; null with a reason when its hierarchy can't be read.</summary>
        private static Template Borrow(HircObject sound, Dictionary<uint, HircObject> bank, Format format, out string problem)
        {
            problem = null;
            int source = format.SourceLength(sound.Body);
            if (BitConverter.ToUInt32(sound.Body, 0) != VorbisPlugin)
            {
                problem = "it isn't Vorbis audio";
                return null;
            }

            Node node = Node.Parse(sound.Body, source, sound.Type, format);
            if (node == null)
            {
                problem = "its sound object doesn't lay out as expected";
                return null;
            }

            Template template = new Template { Sound = sound, SoundNode = node };

            //Up the hierarchy: the containers between the sound and its nearest actor-mixer are skipped (the new sound isn't one
            //of their takes), so what they set that the sound inherited is copied onto it; from the mixer up, it inherits as before
            List<KeyValuePair<HircObject, Node>> chain = new List<KeyValuePair<HircObject, Node>> { new KeyValuePair<HircObject, Node>(sound, node) };
            uint parent = BitConverter.ToUInt32(sound.Body, node.Parent);
            HashSet<uint> seen = new HashSet<uint>();
            while (parent != 0 && seen.Add(parent))
            {
                HircObject o;
                if (!bank.TryGetValue(parent, out o))
                    break;
                Node n = Node.Parse(o.Body, 0, o.Type, format);
                if (n == null)
                    break;
                chain.Add(new KeyValuePair<HircObject, Node>(o, n));
                parent = BitConverter.ToUInt32(o.Body, n.Parent);
            }

            int mixerAt = chain.Count > 1 ? chain.FindIndex(1, o => o.Key.Type == (byte)WwiseObjectType.ActorMixer && o.Value.Exact) : -1;
            template.Mixer = mixerAt > 0 ? chain[mixerAt].Key.Id : 0;
            if (mixerAt < 0 && BitConverter.ToUInt32(sound.Body, node.Parent) != 0)
                template.Notes.Add("the actor-mixer above the sound it's like couldn't be read, so the new sound stands on its own: its bus, positioning and sends are copied, but the mixer's volume and game-parameter curves don't apply");

            //What the sound plays through: the first bus, positioning and sends set on the way up
            foreach (KeyValuePair<HircObject, Node> link in chain)
            {
                byte[] body = link.Key.Body;
                Node n = link.Value;
                if (template.Bus == 0 && BitConverter.ToUInt32(body, n.Bus) != 0)
                    template.Bus = BitConverter.ToUInt32(body, n.Bus);
                if (template.Positioning == null && format.OverridesPositioning(body, n))
                {
                    template.Positioning = Slice(body, n.Positioning, n.Aux);
                    if (format.Modern && link.Key != sound)
                    {
                        //The attenuation goes with the positioning: a property of the object it was set on (measured - every
                        //attenuation property in the iOS and Switch builds sits on an object that overrides its positioning)
                        template.PositioningInherited = true;
                        foreach (KeyValuePair<byte, uint> prop in n.Props(body))
                            if (prop.Key == Format.AttenuationProperty)
                                template.Attenuation = prop.Value;
                    }
                }
                if (template.Aux == null && format.SetsAux(body, n))
                {
                    template.Aux = Slice(body, n.Aux, n.Advanced);

                    //Version 134: aux busses listed by a container at the top of a hierarchy apply without the override flag,
                    //but on a sound under a mixer they only apply with it
                    if (format.Modern && link.Key != sound && (template.Aux[0] & 0x08) != 0)
                        template.Aux[0] |= 0x04;
                }
            }
            if (template.Positioning != null && !format.OverridesPositioning(template.Positioning, new Node()))
                template.Positioning = null;

            //Volume, pitch and filtering set on the skipped containers add to the sound's own
            int stop = mixerAt > 0 ? mixerAt : chain.Count;
            for (int i = 1; i < stop; i++)
            {
                byte[] body = chain[i].Key.Body;
                Node n = chain[i].Value;
                foreach (KeyValuePair<byte, uint> prop in n.Props(body))
                {
                    if (!format.Additive(prop.Key))
                        continue;
                    float value = BitConverter.ToSingle(BitConverter.GetBytes(prop.Value), 0);
                    float sum;
                    template.AddedProps.TryGetValue(prop.Key, out sum);
                    template.AddedProps[prop.Key] = sum + value;
                }
            }

            //Carried along: the mixers from the nearest up, and every attenuation and effect the sound or they use
            HashSet<uint> carried = new HashSet<uint>();
            if (mixerAt > 0)
            {
                for (int i = mixerAt; i < chain.Count; i++)
                {
                    if (chain[i].Key.Type != (byte)WwiseObjectType.ActorMixer || !chain[i].Value.Exact)
                        break; //above a mixer that isn't one or can't be laid out again, the chain stops: it inherits from what's loaded
                    template.Carry.Add(chain[i].Key);
                    carried.Add(chain[i].Key.Id);
                }
            }
            List<uint> references = node.References(sound.Body, format).ToList();
            if (template.Positioning != null && !format.Modern)
                references.AddRange(PositioningReferences(template.Positioning));
            if (template.Attenuation != 0)
                references.Add(template.Attenuation);
            if (template.Aux != null && format.Modern && (template.Aux[0] & 0x08) != 0)
                for (int i = 0; i < 4; i++)
                    references.Add(BitConverter.ToUInt32(template.Aux, 1 + 4 * i));
            foreach (HircObject mixer in template.Carry.ToList())
                references.AddRange(Node.Parse(mixer.Body, 0, mixer.Type, format).References(mixer.Body, format));
            foreach (uint id in references.Distinct())
            {
                HircObject o;
                if (id != 0 && !carried.Contains(id) && bank.TryGetValue(id, out o) && format.Carried(o.Type))
                {
                    template.Carry.Insert(0, o);
                    carried.Add(id);
                }
            }
            return template;
        }

        /// <summary>The new sound's object: the template's, with the new audio, the template's mixer, bus, positioning and sends.</summary>
        private static byte[] SoundBody(Template template, uint sourceId, uint bankId, bool loop, Format format)
        {
            byte[] body = template.Sound.Body;
            Node n = template.SoundNode;
            using (MemoryStream output = new MemoryStream(body.Length + 32))
            using (BinaryWriter writer = new BinaryWriter(output))
            {
                //Source: Vorbis audio in this bank's data - where and how long is filled in once the bank is laid out
                writer.Write(VorbisPlugin);
                if (format.Modern)
                {
                    //Version 134: a one-byte stream type (0, in the bank), the source id, its size in memory, a flag byte
                    //(0 for every in-bank sound the iOS and Switch builds ship)
                    writer.Write((byte)0);
                    writer.Write(sourceId);
                    writer.Write(0u);
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write((uint)WwiseStreamType.Embedded);
                    writer.Write(sourceId);
                    writer.Write(bankId);
                    writer.Write(0u);
                    writer.Write(0u);
                    writer.Write((byte)0);
                }

                //Effects as the sound's own, then the bus and the mixer
                writer.Write(body, n.Start, n.Bus - n.Start);
                writer.Write(template.Bus);
                writer.Write(template.Mixer);
                writer.Write(body, n.Parent + 4, n.PropsAt - (n.Parent + 4)); //priority override and distance factor

                //Properties: the sound's own and what the skipped containers added (volume, pitch, filtering), the loop as asked
                Dictionary<byte, uint> props = new Dictionary<byte, uint>();
                foreach (KeyValuePair<byte, uint> prop in n.Props(body))
                    props[prop.Key] = prop.Value;
                foreach (KeyValuePair<byte, float> added in template.AddedProps)
                {
                    float own = props.ContainsKey(added.Key) ? BitConverter.ToSingle(BitConverter.GetBytes(props[added.Key]), 0) : 0;
                    props[added.Key] = BitConverter.ToUInt32(BitConverter.GetBytes(own + added.Value), 0);
                }
                if (template.PositioningInherited)
                {
                    //The positioning comes from a container above, and the attenuation set beside it with it
                    props.Remove(Format.AttenuationProperty);
                    if (template.Attenuation != 0)
                        props[Format.AttenuationProperty] = template.Attenuation;
                }
                props.Remove(format.LoopProperty);
                if (loop)
                    props[format.LoopProperty] = 0; //loop count: 0 is for ever
                List<byte> ids = props.Keys.OrderBy(o => o).ToList();
                writer.Write((byte)ids.Count);
                foreach (byte id in ids) writer.Write(id);
                foreach (byte id in ids) writer.Write(props[id]);

                //No randomised ranges: the .wav plays as it is
                writer.Write((byte)0);

                writer.Write(template.Positioning ?? Slice(body, n.Positioning, n.Aux));
                writer.Write(template.Aux ?? Slice(body, n.Aux, n.Advanced));
                writer.Write(body, n.Advanced, body.Length - n.Advanced); //advanced settings, states, game-parameter curves, the rest
                return output.ToArray();
            }
        }

        /// <summary>Version 88: the attenuation a positioning block names.</summary>
        private static IEnumerable<uint> PositioningReferences(byte[] positioning)
        {
            //override, 2D, 3D, [panner], [3D type u32, attenuation u32, ...]
            if (positioning.Length >= 3 && positioning[0] != 0 && positioning[2] != 0)
            {
                int at = 3 + (positioning[1] != 0 ? 1 : 0);
                if (at + 8 <= positioning.Length)
                    yield return BitConverter.ToUInt32(positioning, at + 4);
            }
        }

        #endregion

        #region LAYOUT

        /// <summary>
        /// Where the parts of a sound's or a container's NodeBaseParams sit in its body: effects, bus, parent, priority,
        /// properties, ranged modifiers, positioning, sends, advanced settings, states, game-parameter curves - and for an
        /// actor-mixer its child list.
        ///
        /// Version 88 measured over every object the PC game ships (40,048 sounds and 4,243 actor-mixers end exactly where this
        /// says), with a u32 (the motion bus, 0) after the curves. Version 134 measured over the iOS and Switch builds (131,291
        /// sounds, 10,351 actor-mixers, all exact): one byte (attachment override) between the effects and the bus, the two
        /// priority bytes folded into one bit field, positioning as a bit field with a 3D byte and an optional automation path,
        /// sends as a bit field (bit 3: four aux busses follow), six bytes of advanced settings, state properties (three bytes
        /// each) ahead of the state groups with their counts as variable-length integers, curves with a type, accumulation and
        /// variable-length parameter id - and no motion bus.
        /// </summary>
        private sealed class Node
        {
            public int Start, Bus, Parent, PropsAt, Ranged, Positioning, Aux, Advanced, States, Rtpc, Tail, Children;

            /// <summary>Whether the object ends exactly where its last part does - always checkable for sounds and mixers.</summary>
            public bool Exact;

            public static Node Parse(byte[] b, int start, byte type, Format format)
            {
                try
                {
                    bool modern = format.Modern;
                    Node n = new Node { Start = start };
                    int p = start + 1;
                    int fx = b[p++];
                    if (fx > 0) p += 1 + 7 * fx;
                    if (modern) p += 1; //override attachment params
                    n.Bus = p;
                    n.Parent = p + 4;
                    p += 8 + (modern ? 1 : 2);
                    n.PropsAt = p;
                    int count = b[p++]; p += count + 4 * count;
                    n.Ranged = p;
                    count = b[p++]; p += count + 8 * count;
                    n.Positioning = p;
                    if (modern)
                    {
                        //Override (bit 0), listener-relative routing - 3D - (bit 1), the 3D position type in bits 5-6
                        byte bits = b[p++];
                        if ((bits & 1) != 0 && (bits & 2) != 0)
                        {
                            p++; //3D bits
                            if (((bits >> 5) & 3) != 0)
                            {
                                //An automation path: its mode and transition time, vertices, then play-list items and their ranges
                                p += 1 + 4;
                                int vertices = BitConverter.ToInt32(b, p); p += 4 + 16 * vertices;
                                int items = BitConverter.ToInt32(b, p); p += 4 + 8 * items + 12 * items;
                            }
                        }
                    }
                    else if (b[p++] != 0)
                    {
                        int has2d = b[p++], has3d = b[p++];
                        if (has2d != 0) p++;
                        if (has3d != 0)
                        {
                            if (BitConverter.ToUInt32(b, p) != 1)
                                return null; //a path-driven 3D sound: not laid out here
                            p += 10;
                        }
                    }
                    n.Aux = p;
                    if (modern)
                        p += 1 + ((b[p] & 0x08) != 0 ? 16 : 0);
                    else
                        p += 3 + (b[p + 2] != 0 ? 16 : 0);
                    n.Advanced = p;
                    p += modern ? 6 : 14;
                    n.States = p;
                    if (modern)
                    {
                        int stateProps = Var(b, ref p);
                        for (int i = 0; i < stateProps; i++) { Var(b, ref p); p += 2; }
                        int groups = Var(b, ref p);
                        if (groups > 64)
                            return null;
                        for (int g = 0; g < groups; g++) { p += 5; int states = Var(b, ref p); p += 8 * states; }
                    }
                    else
                    {
                        uint groups = BitConverter.ToUInt32(b, p); p += 4;
                        if (groups > 64)
                            return null;
                        for (int g = 0; g < groups; g++) { p += 5; int states = BitConverter.ToUInt16(b, p); p += 2 + 8 * states; }
                    }
                    n.Rtpc = p;
                    int curves = BitConverter.ToUInt16(b, p); p += 2;
                    for (int c = 0; c < curves; c++)
                    {
                        if (modern) { p += 6; Var(b, ref p); p += 5; }
                        else p += 13;
                        int points = BitConverter.ToUInt16(b, p); p += 2 + 12 * points;
                    }
                    n.Tail = p;
                    n.Children = modern ? p : p + 4;
                    if (type == (byte)WwiseObjectType.Sound)
                        n.Exact = n.Children == b.Length;
                    else if (type == (byte)WwiseObjectType.ActorMixer)
                        n.Exact = n.Children + 4 <= b.Length && n.Children + 4 + 4 * (long)BitConverter.ToUInt32(b, n.Children) == b.Length;
                    else
                        n.Exact = p <= b.Length;
                    if (n.Tail > b.Length || ((type == (byte)WwiseObjectType.Sound || type == (byte)WwiseObjectType.ActorMixer) && !n.Exact))
                        return null;
                    return n;
                }
                catch (ArgumentException)
                {
                    return null;
                }
                catch (IndexOutOfRangeException)
                {
                    return null;
                }
            }

            /// <summary>A variable-length integer (seven bits a byte, the top bit saying another follows), as version 134 counts with.</summary>
            private static int Var(byte[] b, ref int p)
            {
                int value = 0, shift = 0;
                while (true)
                {
                    byte next = b[p++];
                    value |= (next & 0x7F) << shift;
                    if ((next & 0x80) == 0 || shift > 21)
                        return value;
                    shift += 7;
                }
            }

            public IEnumerable<KeyValuePair<byte, uint>> Props(byte[] b)
            {
                int count = b[PropsAt];
                for (int i = 0; i < count; i++)
                    yield return new KeyValuePair<byte, uint>(b[PropsAt + 1 + i], BitConverter.ToUInt32(b, PropsAt + 1 + count + 4 * i));
            }

            /// <summary>The attenuation and effects this object uses, by id.</summary>
            public IEnumerable<uint> References(byte[] b, Format format)
            {
                int fx = b[Start + 1];
                for (int i = 0; i < fx; i++)
                    yield return BitConverter.ToUInt32(b, Start + 3 + 7 * i + 1);
                if (format.Modern)
                {
                    foreach (KeyValuePair<byte, uint> prop in Props(b))
                        if (prop.Key == Format.AttenuationProperty)
                            yield return prop.Value;
                    if ((b[Aux] & 0x08) != 0)
                        for (int i = 0; i < 4; i++)
                            yield return BitConverter.ToUInt32(b, Aux + 1 + 4 * i);
                    yield break;
                }
                foreach (uint id in PositioningReferences(Slice(b, Positioning, Aux)))
                    yield return id;
                if (b[Aux + 2] != 0)
                    for (int i = 0; i < 4; i++)
                        yield return BitConverter.ToUInt32(b, Aux + 3 + 4 * i);
            }
        }

        #endregion

        #region READING AND WRITING

        private static List<HircObject> ReadHirc(byte[] bank)
        {
            List<HircObject> objects = new List<HircObject>();
            int position = 0;
            while (position + 8 <= bank.Length)
            {
                string tag = Encoding.ASCII.GetString(bank, position, 4);
                int size = BitConverter.ToInt32(bank, position + 4);
                if (size < 0 || position + 8 + size > bank.Length)
                    break;

                if (tag == "HIRC")
                {
                    int count = BitConverter.ToInt32(bank, position + 8), p = position + 12, end = position + 8 + size;
                    for (int i = 0; i < count && p + 9 <= end; i++)
                    {
                        int length = BitConverter.ToInt32(bank, p + 1);
                        HircObject o = new HircObject { Type = bank[p], Id = BitConverter.ToUInt32(bank, p + 5), Body = new byte[length - 4] };
                        Array.Copy(bank, p + 9, o.Body, 0, o.Body.Length);
                        objects.Add(o);
                        p += 5 + length;
                    }
                }
                position += 8 + size;
            }
            return objects;
        }

        /// <summary>
        /// The header fields after a bank's version, id and language - the same in every bank one build ships: on PC a flag (1)
        /// and the project id (0x2B), on iOS an alignment field (0) and the project id (0x2BF). Null when it has none.
        /// </summary>
        private static byte[] HeaderAfterLanguage(byte[] bank)
        {
            if (bank.Length < 28 || Encoding.ASCII.GetString(bank, 0, 4) != "BKHD" || BitConverter.ToInt32(bank, 4) < 20)
                return null;
            byte[] fields = new byte[8];
            Array.Copy(bank, 20, fields, 0, 8);
            return fields;
        }

        /// <summary>A bank's bytes, from its own file or the package it's packed in.</summary>
        private static byte[] BankBytes(WwiseSoundBank bank)
        {
            if (string.IsNullOrEmpty(bank.FilePath) || !File.Exists(bank.FilePath))
                return null;
            if (string.Equals(Path.GetExtension(bank.FilePath), ".bnk", StringComparison.OrdinalIgnoreCase))
                return File.ReadAllBytes(bank.FilePath);

            WwiseFilePackage package = WwiseFilePackage.Load(bank.FilePath);
            WwiseFilePackage.Entry entry = package.Banks.FirstOrDefault(o => o.Offset == bank.BaseOffset);
            if (entry == null)
                return null;
            byte[] bytes = new byte[entry.Length];
            using (FileStream stream = new FileStream(bank.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                stream.Position = entry.Offset;
                int read = 0;
                while (read < bytes.Length)
                {
                    int got = stream.Read(bytes, read, bytes.Length - read);
                    if (got <= 0) break;
                    read += got;
                }
            }
            return bytes;
        }

        /// <summary>
        /// The bank written out: BKHD, DIDX, DATA, HIRC, STID - the order the game's banks have. Objects go children first, as
        /// they do there (and actions ahead of the events that list them), and each sound's source record points at where its
        /// audio ended up. The header is padded, as every bank of the game's is, so the audio starts on a sixteen byte boundary.
        /// </summary>
        private static byte[] Write(string bankName, uint bankId, Dictionary<uint, HircObject> objects, Dictionary<uint, byte[]> media, Format format, byte[] headerFields)
        {
            //--- the audio ---
            const int Alignment = 16;
            List<uint> sources = media.Keys.OrderBy(o => o).ToList();
            byte[] didx = new byte[12 * sources.Count];
            Dictionary<uint, KeyValuePair<uint, uint>> placed = new Dictionary<uint, KeyValuePair<uint, uint>>();
            byte[] data;
            using (MemoryStream stream = new MemoryStream())
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    while (stream.Position % Alignment != 0) stream.WriteByte(0);
                    uint offset = (uint)stream.Position;
                    byte[] bytes = media[sources[i]];
                    stream.Write(bytes, 0, bytes.Length);
                    Put32(didx, 12 * i, sources[i]);
                    Put32(didx, 12 * i + 4, offset);
                    Put32(didx, 12 * i + 8, (uint)bytes.Length);
                    placed[sources[i]] = new KeyValuePair<uint, uint>(offset, (uint)bytes.Length);
                }
                data = stream.ToArray();
            }

            //The header's fields take 20 bytes; the game's banks pad it so that the data chunk's body (behind the header, the
            //directory and the data chunk's own tag and size) lands on a sixteen byte boundary - 20, 24, 28 or 32 bytes, measured
            //over every bank with audio on PC and iOS - and a bank without audio has 24
            int headerLength = 24;
            if (sources.Count != 0)
                headerLength = 20 + (int)((Alignment - (8 + 20 + 8 + didx.Length + 8) % Alignment) % Alignment);
            long dataBody = (8 + headerLength) + (sources.Count == 0 ? 0 : (8 + didx.Length) + 8);

            //--- the hierarchy, children first ---
            Func<HircObject, int> rank = o =>
            {
                if (o.Type == (byte)WwiseObjectType.Attenuation || format.Carried(o.Type))
                    return 0;
                switch ((WwiseObjectType)o.Type)
                {
                    case WwiseObjectType.Sound: return 1;
                    case WwiseObjectType.ActorMixer: return 2;
                    case WwiseObjectType.Action: return 4;
                    case WwiseObjectType.Event: return 5;
                    default: return 3;
                }
            };
            Dictionary<uint, int> depth = new Dictionary<uint, int>();
            Func<HircObject, int> height = null;
            height = o =>
            {
                //A mixer goes after every mixer below it
                int h;
                if (depth.TryGetValue(o.Id, out h)) return h;
                depth[o.Id] = 0;
                h = 0;
                foreach (HircObject child in objects.Values.Where(c => c.Type == (byte)WwiseObjectType.ActorMixer && c != o && ParentOf(c, format) == o.Id))
                    h = Math.Max(h, height(child) + 1);
                depth[o.Id] = h;
                return h;
            };
            List<HircObject> ordered = objects.Values.OrderBy(rank).ThenBy(o => o.Type == (byte)WwiseObjectType.ActorMixer ? height(o) : 0).ThenBy(o => o.Id).ToList();

            byte[] hirc;
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((uint)ordered.Count);
                foreach (HircObject o in ordered)
                {
                    byte[] body = o.Body;
                    KeyValuePair<uint, uint> at;
                    if (o.Type == (byte)WwiseObjectType.Sound && (BitConverter.ToUInt32(body, 0) & 0x0F) == 1 && placed.TryGetValue(format.SourceId(body), out at))
                    {
                        if (format.Modern && body[4] == 0)
                        {
                            //Version 134 keeps only the size in memory - the directory says where the audio is
                            body = (byte[])body.Clone();
                            Put32(body, 9, at.Value);
                        }
                        else if (!format.Modern && format.SourceLength(body) == 25 && BitConverter.ToUInt32(body, 4) == (uint)WwiseStreamType.Embedded)
                        {
                            body = (byte[])body.Clone();
                            Put32(body, 16, (uint)(dataBody + at.Key));
                            Put32(body, 20, at.Value);
                        }
                    }
                    writer.Write(o.Type);
                    writer.Write((uint)(body.Length + 4));
                    writer.Write(o.Id);
                    writer.Write(body);
                }
                hirc = stream.ToArray();
            }

            //--- the bank's own name ---
            byte[] name = Encoding.ASCII.GetBytes(bankName);
            if (name.Length > 255)
                throw new InvalidDataException("That soundbank name is too long.");
            byte[] stid = new byte[8 + 4 + 1 + name.Length];
            Put32(stid, 0, 1);
            Put32(stid, 4, 1);
            Put32(stid, 8, bankId);
            stid[12] = (byte)name.Length;
            Array.Copy(name, 0, stid, 13, name.Length);

            //--- the header: version, id, language (none: sound effects), and the rest as the game's banks have them ---
            //Sound effects are language 0 in version 88; version 134 names every language by its name's hash, "sfx" too
            byte[] header = new byte[headerLength];
            Put32(header, 0, format.Version);
            Put32(header, 4, bankId);
            Put32(header, 8, format.Modern ? CathodeLib.Utilities.SoundHashedString("sfx") : 0);
            if (headerFields != null)
                Array.Copy(headerFields, 0, header, 12, 8);
            else if (format.Modern)
                Put32(header, 16, 0x2BF);
            else
            {
                Put32(header, 12, 1);
                Put32(header, 16, 0x2B);
            }

            using (MemoryStream output = new MemoryStream())
            {
                Chunk(output, "BKHD", header);
                if (sources.Count != 0)
                {
                    Chunk(output, "DIDX", didx);
                    Chunk(output, "DATA", data);
                }
                Chunk(output, "HIRC", hirc);
                Chunk(output, "STID", stid);
                return output.ToArray();
            }
        }

        private static void Chunk(Stream output, string tag, byte[] body)
        {
            output.Write(Encoding.ASCII.GetBytes(tag), 0, 4);
            output.Write(BitConverter.GetBytes(body.Length), 0, 4);
            output.Write(body, 0, body.Length);
        }

        private static byte[] Slice(byte[] b, int from, int to)
        {
            byte[] slice = new byte[to - from];
            Array.Copy(b, from, slice, 0, slice.Length);
            return slice;
        }

        private static void Put16(byte[] target, int offset, ushort value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
        }

        private static void Put32(byte[] target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        #endregion

        /// <summary>The events a bank built here holds, by id - read from its hierarchy (their names are in the level's table).</summary>
        public static List<uint> EventsIn(byte[] bank)
        {
            return ReadHirc(bank).Where(o => o.Type == (byte)WwiseObjectType.Event).Select(o => o.Id).ToList();
        }
    }
}
