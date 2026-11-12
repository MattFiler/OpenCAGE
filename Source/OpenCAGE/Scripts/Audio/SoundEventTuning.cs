using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Reads and changes how one sound event plays inside a soundbank's bytes: volume, pitch, start delay, fade in, the fade and reach
    /// of its stop (its own sound, everything under the same actor-mixer, or everything on the same bus), looping, and how far away it
    /// can be heard. Only a level's own copy of a bank is changed (<see cref="LevelSoundOverride"/>), never DATA/SOUND's.
    ///
    /// Layouts are for the PC, Mac and Linux soundbanks (Wwise bank version 88):
    /// - an action is a type, its target, whether the target is a bus, a property list (u8 count, the ids, a u32 each - the delay is
    ///   0x0E and the fade time 0x0F, both in milliseconds), a list of randomised ranges (u8 count, the ids, a min and max each), then
    ///   what its type adds (five bytes for Play and Stop);
    /// - a sound or container opens with NodeBaseParams (see <see cref="Node"/>): its property list holds the volume (0, dB), pitch (2,
    ///   cents) and loop count (7: 0 is for ever, only on sounds), and its positioning block the attenuation when it sets its own 3D
    ///   positioning; one that doesn't inherits it from the container above;
    /// - an attenuation is a cone, which curve does what, the curves (distance, value, interpolation per point) and an RTPC list
    ///   (always empty in retail).
    /// Play targets are often shared by other events, and positioning usually comes from an actor-mixer many sounds share, so a reach
    /// change is made on a copy of the attenuation under its own id that only this event's sounds point at; the shared curve is never touched.
    ///
    /// Every change keeps the soundbank's other bytes as they were. The hierarchy comes after the audio in these banks, so an object
    /// that grows moves no audio.
    /// </summary>
    internal static class SoundEventTuning
    {
        public const uint SupportedVersion = 88;

        private const byte PropVolume = 0x00, PropPitch = 0x02, PropLoop = 0x07;
        private const byte ActionDelay = 0x0E, ActionFade = 0x0F;

        public enum StopReach
        {
            /// <summary>The sound it was made to stop.</summary>
            Own,

            /// <summary>Everything under the nearest actor-mixer above that sound.</summary>
            Mixer,

            /// <summary>Everything playing on that sound's output bus.</summary>
            Bus,
        }

        /// <summary>What to change. Anything left null stays as it is.</summary>
        public sealed class Settings
        {
            /// <summary>The event's sound's own volume, in dB (it adds to what the containers above it set).</summary>
            public float? VolumeDb;

            /// <summary>Added to the volume the event's sound has now, in dB (instead of <see cref="VolumeDb"/>).</summary>
            public float? VolumeChangeDb;

            /// <summary>The event's sound's own pitch, in cents (1200 is an octave up).</summary>
            public float? PitchCents;

            /// <summary>How long after it is played each of the event's actions happens, in ms. A randomised delay range is cleared.</summary>
            public int? DelayMs;

            /// <summary>Fade in, in ms, on the event's Play actions.</summary>
            public int? FadeInMs;

            /// <summary>Fade out, in ms, on the event's Stop actions (on a Play event: on its Stop event's - see LevelSoundOverride.Tune).</summary>
            public int? FadeOutMs;

            /// <summary>What the event's Stop actions stop.</summary>
            public StopReach? Stops;

            /// <summary>Loop count for every sound the event plays: 0 for ever, 1 once (no loop), n times.</summary>
            public int? Loop;

            /// <summary>How far away the event can be heard, in metres: its attenuation curves stretched to end there.</summary>
            public float? MaxDistance;

            public bool ChangesPlay
            {
                get { return VolumeDb.HasValue || VolumeChangeDb.HasValue || PitchCents.HasValue || DelayMs.HasValue || FadeInMs.HasValue || Loop.HasValue || MaxDistance.HasValue; }
            }

            public bool ChangesStop
            {
                get { return FadeOutMs.HasValue || Stops.HasValue; }
            }

            public bool Any
            {
                get { return ChangesPlay || ChangesStop; }
            }

            /// <summary>Only what a Stop event takes: the fade out and the reach.</summary>
            public Settings StopPart()
            {
                return new Settings { FadeOutMs = FadeOutMs, Stops = Stops };
            }

            /// <summary>Why these can't be set, or null.</summary>
            public string Check()
            {
                if (VolumeDb.HasValue && VolumeChangeDb.HasValue) return "Give a volume or a volume change, not both.";
                if (VolumeDb.HasValue && (VolumeDb.Value < -96 || VolumeDb.Value > 24 || float.IsNaN(VolumeDb.Value))) return "The volume goes from -96 to +24 dB.";
                if (VolumeChangeDb.HasValue && (Math.Abs(VolumeChangeDb.Value) > 96 || float.IsNaN(VolumeChangeDb.Value))) return "A volume change goes from -96 to +96 dB.";
                if (PitchCents.HasValue && (PitchCents.Value < -2400 || PitchCents.Value > 2400 || float.IsNaN(PitchCents.Value))) return "The pitch goes from -2400 to +2400 cents (two octaves).";
                if (DelayMs.HasValue && (DelayMs.Value < 0 || DelayMs.Value > 600000)) return "The delay goes from 0 to 600,000 ms.";
                if (FadeInMs.HasValue && (FadeInMs.Value < 0 || FadeInMs.Value > 600000)) return "The fade in goes from 0 to 600,000 ms.";
                if (FadeOutMs.HasValue && (FadeOutMs.Value < 0 || FadeOutMs.Value > 600000)) return "The fade out goes from 0 to 600,000 ms.";
                if (Loop.HasValue && (Loop.Value < 0 || Loop.Value > 32767)) return "The loop count goes from 0 (for ever) to 32,767 (1 plays once).";
                if (MaxDistance.HasValue && (MaxDistance.Value < 0.5f || MaxDistance.Value > 10000 || float.IsNaN(MaxDistance.Value))) return "The reach goes from 0.5 to 10,000 metres.";
                return null;
            }
        }

        #region WHAT AN EVENT DOES

        /// <summary>One of an event's actions, as a bank has it.</summary>
        public sealed class ActionState
        {
            public uint Id;
            public ushort Type;
            public uint Target;
            public bool TargetIsBus;
            public int? DelayMs;
            public int? FadeMs;

            /// <summary>A randomised delay range (ms) added to the delay, or null.</summary>
            public int[] DelayRange;

            /// <summary>Not in this bank (the event lists it but the bank doesn't carry it).</summary>
            public bool Missing;

            /// <summary>For a Stop action: the nearest actor-mixer above what it stops, and the bus that plays on (0 for none found).</summary>
            public uint Mixer, Bus;

            public bool IsPlay { get { return (Type >> 8) == 0x04 || (Type >> 8) == 0x05; } }
            public bool IsStop { get { return (Type >> 8) == 0x01; } }

            public string Kind
            {
                get
                {
                    switch (Type >> 8)
                    {
                        case 0x01: return "stop";
                        case 0x02: return "pause";
                        case 0x03: return "resume";
                        case 0x04: return "play";
                        case 0x05: return "play and continue";
                        case 0x06: return "mute";
                        case 0x07: return "unmute";
                        case 0x0A: return "set volume";
                        case 0x0B: return "reset volume";
                        case 0x12: return "set state";
                        case 0x13: return "set game parameter";
                        case 0x19: return "set switch";
                        default: return "action 0x" + Type.ToString("X4");
                    }
                }
            }

            /// <summary>Whether it acts on the object that played the event (else on every object).</summary>
            public bool OnOneObject { get { return (Type & 0xFF) == 0x03 || (Type & 0xFF) == 0x05 || (Type & 0xFF) == 0x09; } }
        }

        /// <summary>What a Play action plays, as a bank has it.</summary>
        public sealed class TargetState
        {
            public uint Id;
            public byte Type;
            public float VolumeDb;
            public float PitchCents;

            /// <summary>The sounds it plays (itself, for a sound) and the loop count of each: null for none (plays once), 0 for ever.</summary>
            public List<KeyValuePair<uint, int?>> Sounds = new List<KeyValuePair<uint, int?>>();

            /// <summary>Where its 3D positioning comes from: "own", "inherited" (from a container above), "2d", "path" or "none".</summary>
            public string Positioning = "none";

            /// <summary>The attenuations its sounds play with, and the farthest distance any of them reaches.</summary>
            public List<uint> Attenuations = new List<uint>();
            public float? MaxDistance;

            /// <summary>A kind of object these settings don't lay out (music): only the actions are read.</summary>
            public bool NotTunable;

            public string TypeName
            {
                get
                {
                    switch (Type)
                    {
                        case 2: return "sound";
                        case 5: return "random/sequence container";
                        case 6: return "switch container";
                        case 7: return "actor-mixer";
                        case 9: return "blend container";
                        case 10: return "music segment";
                        case 11: return "music track";
                        case 12: return "music switch";
                        case 13: return "music playlist";
                        default: return "object type " + Type;
                    }
                }
            }
        }

        /// <summary>One event as one bank has it.</summary>
        public sealed class EventState
        {
            public uint EventId;
            public List<ActionState> Actions = new List<ActionState>();
            public List<TargetState> Targets = new List<TargetState>();

            public IEnumerable<ActionState> Plays { get { return Actions.Where(o => !o.Missing && o.IsPlay); } }
            public IEnumerable<ActionState> Stops { get { return Actions.Where(o => !o.Missing && o.IsStop); } }

            /// <summary>How far away it can be heard (its farthest attenuation), or null when it plays in 2D or that can't be read.</summary>
            public float? MaxDistance
            {
                get
                {
                    List<float> d = Targets.Where(o => o.MaxDistance.HasValue).Select(o => o.MaxDistance.Value).ToList();
                    return d.Count == 0 ? (float?)null : d.Max();
                }
            }

            /// <summary>The loop count all its sounds share (null: plays once; -1 when they differ or there are none).</summary>
            public int? Loop
            {
                get
                {
                    List<int?> loops = Targets.SelectMany(o => o.Sounds).Select(o => o.Value).Distinct().ToList();
                    return loops.Count == 1 ? loops[0] : -1;
                }
            }
        }

        /// <summary>The event as this bank has it, or null when the bank doesn't carry the event.</summary>
        public static EventState Read(byte[] bank, uint eventId)
        {
            Bank b = Bank.Parse(bank);
            return b.Version == SupportedVersion ? ReadEvent(b, eventId) : null;
        }

        /// <summary>Every event a bank carries (or those asked for), read with one pass over the bank. Empty for a bank of another version.</summary>
        public static Dictionary<uint, EventState> ReadAll(byte[] bank, ICollection<uint> only = null)
        {
            Dictionary<uint, EventState> events = new Dictionary<uint, EventState>();
            Bank b = Bank.Parse(bank);
            if (b.Version != SupportedVersion)
                return events;
            foreach (Obj ev in b.Objects.Where(o => o.Type == (byte)WwiseObjectType.Event))
                if ((only == null || only.Contains(ev.Id)) && !events.ContainsKey(ev.Id))
                    events[ev.Id] = ReadEvent(b, ev.Id);
            return events;
        }

        /// <summary>Whether a bank carries an event (a quick look through its hierarchy, nothing copied).</summary>
        public static bool Carries(byte[] bank, uint eventId)
        {
            int pos = 0;
            while (pos + 8 <= bank.Length)
            {
                int size = BitConverter.ToInt32(bank, pos + 4);
                if (size < 0 || pos + 8 + (long)size > bank.Length)
                    return false;
                if (bank[pos] == 'H' && bank[pos + 1] == 'I' && bank[pos + 2] == 'R' && bank[pos + 3] == 'C')
                {
                    int count = BitConverter.ToInt32(bank, pos + 8), p = pos + 12, end = pos + 8 + size;
                    for (int i = 0; i < count && p + 9 <= end; i++)
                    {
                        if (bank[p] == (byte)WwiseObjectType.Event && BitConverter.ToUInt32(bank, p + 5) == eventId)
                            return true;
                        p += 5 + BitConverter.ToInt32(bank, p + 1);
                    }
                    return false;
                }
                pos += 8 + size;
            }
            return false;
        }

        /// <summary>The events a soundbank carries, by id.</summary>
        public static List<uint> EventsIn(byte[] bank)
        {
            return Bank.Parse(bank).Objects.Where(o => o.Type == (byte)WwiseObjectType.Event).Select(o => o.Id).ToList();
        }

        /// <summary>The bank version of a soundbank (88 PC/Mac/Linux, 134 iOS/Switch).</summary>
        public static uint VersionOf(byte[] bank)
        {
            return bank.Length >= 12 && Encoding.ASCII.GetString(bank, 0, 4) == "BKHD" ? BitConverter.ToUInt32(bank, 8) : 0;
        }

        private static EventState ReadEvent(Bank bank, uint eventId)
        {
            Obj ev = bank.Get(eventId, WwiseObjectType.Event);
            if (ev == null)
                return null;

            EventState state = new EventState { EventId = eventId };
            foreach (uint actionId in EventActions(ev))
            {
                Obj action = bank.Get(actionId, WwiseObjectType.Action);
                if (action == null)
                {
                    state.Actions.Add(new ActionState { Id = actionId, Missing = true });
                    continue;
                }

                ActionState a = ReadAction(action);
                state.Actions.Add(a);
                if (a.IsStop && !a.TargetIsBus)
                {
                    uint bus;
                    a.Mixer = MixerAbove(bank, a.Target, out bus);
                    a.Bus = bus;
                }
                else if (a.IsStop)
                    a.Bus = a.Target;

                if (!a.IsPlay || state.Targets.Any(o => o.Id == a.Target))
                    continue;
                Obj target = bank.Get(a.Target);
                if (target != null)
                    state.Targets.Add(ReadTarget(bank, target));
            }
            return state;
        }

        private static IEnumerable<uint> EventActions(Obj ev)
        {
            if (ev.Body.Length < 4)
                yield break;
            uint count = BitConverter.ToUInt32(ev.Body, 0);
            for (int i = 0; i < count && 4 + 4 * i + 4 <= ev.Body.Length; i++)
                yield return BitConverter.ToUInt32(ev.Body, 4 + 4 * i);
        }

        private static ActionState ReadAction(Obj action)
        {
            byte[] b = action.Body;
            ActionState a = new ActionState
            {
                Id = action.Id,
                Type = BitConverter.ToUInt16(b, 0),
                Target = BitConverter.ToUInt32(b, 2),
                TargetIsBus = b[6] != 0,
            };
            uint value;
            if (ActionProp(b, ActionDelay, out value)) a.DelayMs = (int)value;
            if (ActionProp(b, ActionFade, out value)) a.FadeMs = (int)value;
            int ranged = RangedAt(b);
            int n = b[ranged];
            for (int i = 0; i < n; i++)
                if (b[ranged + 1 + i] == ActionDelay)
                {
                    int at = ranged + 1 + n + 8 * i;
                    a.DelayRange = new[] { BitConverter.ToInt32(b, at), BitConverter.ToInt32(b, at + 4) };
                }
            return a;
        }

        private static TargetState ReadTarget(Bank bank, Obj target)
        {
            TargetState t = new TargetState { Id = target.Id, Type = target.Type };
            Node node = Node.Parse(target);
            if (node == null)
            {
                t.NotTunable = true;
                return t;
            }

            float f;
            if (node.Prop(target.Body, PropVolume, out f)) t.VolumeDb = f;
            if (node.Prop(target.Body, PropPitch, out f)) t.PitchCents = f;

            foreach (Obj sound in SoundsUnder(bank, target))
            {
                Node n = Node.Parse(sound);
                uint loop;
                t.Sounds.Add(new KeyValuePair<uint, int?>(sound.Id, n != null && n.PropBits(sound.Body, PropLoop, out loop) ? (int?)loop : null));
            }

            //Positioning: its own, else the nearest container above that sets one
            Obj owner = PositioningOwner(bank, target);
            if (owner == null)
                t.Positioning = "none";
            else
            {
                Node on = Node.Parse(owner);
                t.Positioning = on.Path ? "path" : on.Attenuation < 0 ? "2d" : owner == target ? "own" : "inherited";
                if (on.Attenuation >= 0)
                    t.Attenuations.Add(BitConverter.ToUInt32(owner.Body, on.Attenuation));
            }

            //Anything under it that sets its own plays with its own attenuation
            foreach (Obj below in Under(bank, target))
            {
                Node bn = Node.Parse(below);
                if (bn != null && bn.Attenuation >= 0)
                {
                    uint id = BitConverter.ToUInt32(below.Body, bn.Attenuation);
                    if (!t.Attenuations.Contains(id))
                        t.Attenuations.Add(id);
                }
            }

            foreach (uint id in t.Attenuations)
            {
                Obj att = bank.Get(id, WwiseObjectType.Attenuation);
                float max;
                if (att != null && AttenuationMax(att.Body, out max))
                    t.MaxDistance = t.MaxDistance.HasValue ? Math.Max(t.MaxDistance.Value, max) : max;
            }
            return t;
        }

        /// <summary>What changed between two readings of an event (base game's, then the level's), in words; empty when nothing did.</summary>
        public static List<string> Differences(EventState before, EventState after)
        {
            List<string> changes = new List<string>();
            if (before == null || after == null)
                return changes;

            foreach (TargetState a in after.Targets)
            {
                TargetState b = before.Targets.FirstOrDefault(o => o.Id == a.Id);
                if (b == null || a.NotTunable)
                    continue;
                if (b.VolumeDb != a.VolumeDb) changes.Add("volume " + Db(b.VolumeDb) + " -> " + Db(a.VolumeDb));
                if (b.PitchCents != a.PitchCents) changes.Add("pitch " + Num(b.PitchCents) + " -> " + Num(a.PitchCents) + " cents");
                List<int?> loopsBefore = b.Sounds.Select(o => o.Value).Distinct().ToList(), loopsAfter = a.Sounds.Select(o => o.Value).Distinct().ToList();
                if (!loopsBefore.SequenceEqual(loopsAfter)) changes.Add("loop " + LoopText(loopsBefore) + " -> " + LoopText(loopsAfter));
                if (b.MaxDistance != a.MaxDistance) changes.Add("reach " + Num(b.MaxDistance) + " -> " + Num(a.MaxDistance) + " m");
            }
            foreach (ActionState a in after.Actions.Where(o => !o.Missing))
            {
                ActionState b = before.Actions.FirstOrDefault(o => o.Id == a.Id && !o.Missing);
                if (b == null)
                    continue;
                if ((b.DelayMs ?? 0) != (a.DelayMs ?? 0) || !SameRange(b.DelayRange, a.DelayRange))
                    changes.Add(a.Kind + " delay " + Ms(b.DelayMs, b.DelayRange) + " -> " + Ms(a.DelayMs, a.DelayRange));
                if ((b.FadeMs ?? 0) != (a.FadeMs ?? 0))
                    changes.Add((a.IsStop ? "fade out " : a.IsPlay ? "fade in " : a.Kind + " fade ") + (b.FadeMs ?? 0) + " -> " + (a.FadeMs ?? 0) + " ms");
                if (b.Target != a.Target || b.TargetIsBus != a.TargetIsBus)
                    changes.Add(a.Kind + " " + (a.TargetIsBus ? "now stops everything on bus " + a.Target : a.Target == b.Mixer ? "now stops everything under actor-mixer " + a.Target : "target " + b.Target + " -> " + a.Target));
            }
            return changes;
        }

        private static bool SameRange(int[] a, int[] b)
        {
            bool za = a == null || (a[0] == 0 && a[1] == 0), zb = b == null || (b[0] == 0 && b[1] == 0);
            if (za || zb) return za == zb;
            return a[0] == b[0] && a[1] == b[1];
        }

        private static string Ms(int? delay, int[] range)
        {
            string text = (delay ?? 0) + " ms";
            if (range != null && (range[0] != 0 || range[1] != 0))
                text += " (" + (range[0] >= 0 ? "+" : "") + range[0] + ".." + (range[1] >= 0 ? "+" : "") + range[1] + " random)";
            return text;
        }

        private static string LoopText(List<int?> loops)
        {
            if (loops.Count != 1) return "mixed";
            return loops[0] == null || loops[0] == 1 ? "once" : loops[0] == 0 ? "for ever" : loops[0] + " times";
        }

        private static string Db(float v)
        {
            return (v > 0 ? "+" : "") + v.ToString("0.##", CultureInfo.InvariantCulture) + " dB";
        }

        private static string Num(float? v)
        {
            return v.HasValue ? v.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-";
        }

        #endregion

        #region CHANGING IT

        /// <summary>
        /// The bank with the event changed as <paramref name="settings"/> say. <paramref name="baseBank"/> is the base game's copy of the
        /// same bank: a Stop's original target and the reach to scale from come from it. <paramref name="taken"/> says whether an id is
        /// in use anywhere (a copied attenuation needs a free one). Changes skipped, and why, go in <paramref name="notes"/>. Null with
        /// <paramref name="problem"/> when the bank can't be changed.
        /// </summary>
        public static byte[] Apply(byte[] current, byte[] baseBank, uint eventId, Settings settings, Func<uint, bool> taken, List<string> notes, out string problem)
        {
            problem = null;
            Bank bank = Bank.Parse(current);
            Bank original = baseBank == null || ReferenceEquals(baseBank, current) ? bank : Bank.Parse(baseBank);
            if (bank.Version != SupportedVersion)
            {
                problem = "This soundbank is version " + bank.Version + "; sound events can be tuned in the PC, Mac and Linux soundbanks (version " + SupportedVersion + ").";
                return null;
            }
            if (!bank.HierarchyAfterAudio)
            {
                problem = "This soundbank keeps its audio after its hierarchy, which tuning would move.";
                return null;
            }

            Obj ev = bank.Get(eventId, WwiseObjectType.Event);
            if (ev == null)
            {
                problem = "This soundbank doesn't carry the event.";
                return null;
            }

            List<Obj> actions = EventActions(ev).Select(o => bank.Get(o, WwiseObjectType.Action)).Where(o => o != null).ToList();
            List<Obj> plays = actions.Where(o => ((BitConverter.ToUInt16(o.Body, 0) >> 8) == 0x04 || (BitConverter.ToUInt16(o.Body, 0) >> 8) == 0x05)).ToList();
            List<Obj> stops = actions.Where(o => (BitConverter.ToUInt16(o.Body, 0) >> 8) == 0x01).ToList();
            List<Obj> targets = plays.Select(o => bank.Get(BitConverter.ToUInt32(o.Body, 2))).Where(o => o != null).Distinct().ToList();
            List<Obj> tunable = targets.Where(o => Node.Parse(o) != null).ToList();
            if (settings.ChangesPlay && plays.Count != 0 && tunable.Count < targets.Count)
                notes.Add("A music object the event plays isn't laid out here: only its actions are changed.");

            //--- volume and pitch: the Play targets' own properties ---
            if (settings.VolumeDb.HasValue || settings.VolumeChangeDb.HasValue)
            {
                if (tunable.Count == 0)
                    notes.Add("Volume: the event plays nothing that can be turned up or down here.");
                foreach (Obj t in tunable)
                {
                    float now;
                    Node.Parse(t).Prop(t.Body, PropVolume, out now);
                    float value = settings.VolumeDb ?? now + settings.VolumeChangeDb.Value;
                    value = Math.Max(-96, Math.Min(24, value));
                    SetNodePropUnlessDefault(t, PropVolume, value);
                }
            }
            if (settings.PitchCents.HasValue)
            {
                if (tunable.Count == 0)
                    notes.Add("Pitch: the event plays nothing that can be pitched here.");
                foreach (Obj t in tunable)
                    SetNodePropUnlessDefault(t, PropPitch, settings.PitchCents.Value);
            }

            //--- delay: the event's Play actions (every action of an event that plays nothing); fade in: its Play actions ---
            if (settings.DelayMs.HasValue)
            {
                foreach (Obj a in plays.Count != 0 ? plays : actions)
                {
                    SetActionPropUnlessDefault(a, ActionDelay, (uint)settings.DelayMs.Value);
                    ClearRange(a, ActionDelay);
                }
            }
            if (settings.FadeInMs.HasValue)
            {
                if (plays.Count == 0)
                    notes.Add("Fade in: the event has no Play action.");
                foreach (Obj a in plays)
                    SetActionPropUnlessDefault(a, ActionFade, (uint)settings.FadeInMs.Value);
            }

            //--- fade out and reach: the event's Stop actions ---
            if (settings.FadeOutMs.HasValue || settings.Stops.HasValue)
            {
                if (stops.Count == 0)
                    notes.Add("Stop: the event has no Stop action.");
                foreach (Obj a in stops)
                {
                    if (settings.FadeOutMs.HasValue)
                        SetActionPropUnlessDefault(a, ActionFade, (uint)settings.FadeOutMs.Value);
                    if (settings.Stops.HasValue)
                        SetStopReach(bank, original, a, settings.Stops.Value, notes);
                }
            }

            //--- loop: every sound the event plays ---
            if (settings.Loop.HasValue)
            {
                int sounds = 0;
                foreach (Obj t in tunable)
                    foreach (Obj s in SoundsUnder(bank, t))
                    {
                        sounds++;
                        //Once is no loop count at all, as the game's own one-shot sounds have it
                        if (settings.Loop.Value == 1)
                            RemoveNodeProp(s, PropLoop);
                        else
                            SetNodeProp(s, PropLoop, (uint)settings.Loop.Value);
                    }
                if (sounds == 0)
                    notes.Add("Loop: the event plays no sound that can loop here.");
            }

            //--- reach: copies of the attenuations, only this event's sounds pointing at them ---
            if (settings.MaxDistance.HasValue)
                SetReach(bank, original, eventId, tunable.Select(o => o.Id).ToList(), settings.MaxDistance.Value, taken, notes);

            RemoveUnusedCopies(bank, original);
            return bank.Write();
        }

        /// <summary>
        /// Puts the event's objects (actions, what they play and everything under that) back as the base game's bank has them, keeping
        /// each sound's audio location so audio replaced in the same bank stays replaced. Unreferenced attenuation copies are removed.
        /// </summary>
        public static byte[] Revert(byte[] current, byte[] baseBank, uint eventId, out int restored)
        {
            restored = 0;
            Bank bank = Bank.Parse(current);
            Bank original = Bank.Parse(baseBank);
            if (bank.Version != SupportedVersion || !bank.HierarchyAfterAudio)
                return null;

            foreach (Obj o in EventObjects(original, eventId))
            {
                Obj now = bank.Get(o.Id, (WwiseObjectType)o.Type);
                if (now == null || now.Body.SequenceEqual(o.Body))
                    continue;

                byte[] body = (byte[])o.Body.Clone();
                if (o.Type == (byte)WwiseObjectType.Sound && SourceLength(body) == 25 && SourceLength(now.Body) == 25 && now.Body.Length >= 24)
                    Array.Copy(now.Body, 16, body, 16, 8); //where its audio is in this copy of the bank
                if (!now.Body.SequenceEqual(body))
                {
                    now.Body = body;
                    restored++;
                }
            }
            RemoveUnusedCopies(bank, original);
            return bank.Write();
        }

        /// <summary>The objects tuning an event can change, as a bank has them: its actions, what they play, everything under that.</summary>
        private static IEnumerable<Obj> EventObjects(Bank bank, uint eventId)
        {
            Obj ev = bank.Get(eventId, WwiseObjectType.Event);
            if (ev == null)
                yield break;
            HashSet<uint> seen = new HashSet<uint>();
            foreach (uint actionId in EventActions(ev))
            {
                Obj action = bank.Get(actionId, WwiseObjectType.Action);
                if (action == null || !seen.Add(action.Id))
                    continue;
                yield return action;
                int kind = BitConverter.ToUInt16(action.Body, 0) >> 8;
                if (kind != 0x04 && kind != 0x05)
                    continue;
                Obj target = bank.Get(BitConverter.ToUInt32(action.Body, 2));
                if (target == null || !seen.Add(target.Id))
                    continue;
                yield return target;
                foreach (Obj below in Under(bank, target))
                    if (seen.Add(below.Id))
                        yield return below;
            }
        }

        /// <summary>A Stop action made to stop its own sound, everything under that sound's actor-mixer, or everything on its bus.</summary>
        private static void SetStopReach(Bank bank, Bank original, Obj action, StopReach reach, List<string> notes)
        {
            //What it stopped as the game shipped it
            Obj shipped = original.Get(action.Id, WwiseObjectType.Action) ?? action;
            uint own = BitConverter.ToUInt32(shipped.Body, 2);
            bool ownIsBus = shipped.Body[6] != 0;

            uint target = own;
            bool isBus = ownIsBus;
            if (reach != StopReach.Own)
            {
                if (ownIsBus)
                {
                    if (reach == StopReach.Mixer)
                        notes.Add("Stop: it already stops a whole bus, which has no actor-mixer above it.");
                }
                else
                {
                    uint bus;
                    uint mixer = MixerAbove(original, own, out bus);
                    if (reach == StopReach.Mixer)
                    {
                        if (mixer == 0)
                            notes.Add("Stop: no actor-mixer above the sound it stops in this soundbank - left stopping its own sound.");
                        else
                            target = mixer;
                    }
                    else
                    {
                        if (bus == 0)
                            notes.Add("Stop: no output bus found for the sound it stops in this soundbank - left stopping its own sound.");
                        else
                        {
                            target = bus;
                            isBus = true;
                        }
                    }
                }
            }
            Put32(action.Body, 2, target);
            action.Body[6] = (byte)(isBus ? 1 : 0);
        }

        /// <summary>
        /// The event reaches <paramref name="metres"/>: each attenuation its sounds play with (as the base game's bank has them) is
        /// copied under an id of its own with every curve point's distance scaled, and only this event's Play targets (and anything under
        /// them that sets its own positioning) point at the copies. A target that inherited its positioning gets that block as its own.
        /// </summary>
        private static void SetReach(Bank bank, Bank original, uint eventId, List<uint> targetIds, float metres, Func<uint, bool> taken, List<string> notes)
        {
            //Start from the shipped positioning, so a second change is measured against the game's own reach, not the last one
            foreach (uint id in targetIds)
            {
                Obj shipped = original.Get(id);
                Obj now = bank.Get(id);
                if (shipped == null || now == null)
                    continue;
                RestorePositioning(now, shipped);
                foreach (Obj below in Under(original, shipped))
                {
                    Obj b = bank.Get(below.Id);
                    if (b != null)
                        RestorePositioning(b, below);
                }
            }

            //What each target plays with, and how far that reaches now
            Dictionary<uint, float> maxima = new Dictionary<uint, float>();
            List<KeyValuePair<Obj, Obj>> owners = new List<KeyValuePair<Obj, Obj>>();
            List<Obj> overriding = new List<Obj>();
            foreach (uint id in targetIds)
            {
                Obj t = bank.Get(id);
                if (t == null)
                    continue;
                Obj owner = PositioningOwner(bank, t);
                Node on = owner == null ? null : Node.Parse(owner);
                if (on == null || on.Path || on.Attenuation < 0)
                {
                    notes.Add("Reach: " + (on != null && on.Path ? "the event's sound moves along a path, which isn't changed here" : "the event's sound plays in 2D (no distance)") + ".");
                    continue;
                }
                owners.Add(new KeyValuePair<Obj, Obj>(t, owner));
                maxima[BitConverter.ToUInt32(owner.Body, on.Attenuation)] = 0;
                foreach (Obj below in Under(bank, t))
                {
                    Node bn = Node.Parse(below);
                    if (bn != null && bn.Attenuation >= 0)
                    {
                        overriding.Add(below);
                        maxima[BitConverter.ToUInt32(below.Body, bn.Attenuation)] = 0;
                    }
                }
            }
            if (maxima.Count == 0)
                return;

            foreach (uint id in maxima.Keys.ToList())
            {
                Obj att = bank.Get(id, WwiseObjectType.Attenuation);
                float max;
                if (att == null || !AttenuationMax(att.Body, out max) || max <= 0)
                {
                    notes.Add("Reach: attenuation " + id + " isn't in this soundbank, or can't be read - left as it is.");
                    maxima.Remove(id);
                    continue;
                }
                maxima[id] = max;
            }
            if (maxima.Count == 0)
                return;

            float factor = metres / maxima.Values.Max();
            Dictionary<uint, uint> copies = new Dictionary<uint, uint>();
            foreach (KeyValuePair<uint, float> att in maxima)
            {
                Obj source = bank.Get(att.Key, WwiseObjectType.Attenuation);
                uint copyId = CopyId(eventId, att.Key, bank, original, taken);
                byte[] body = (byte[])source.Body.Clone();
                ScaleAttenuation(body, factor);
                Obj copy = bank.Get(copyId, WwiseObjectType.Attenuation);
                if (copy != null)
                    copy.Body = body;
                else
                    bank.InsertAfter(source, new Obj { Type = (byte)WwiseObjectType.Attenuation, Id = copyId, Body = body });
                copies[att.Key] = copyId;
            }

            foreach (KeyValuePair<Obj, Obj> pair in owners)
            {
                Obj t = pair.Key, owner = pair.Value;
                Node on = Node.Parse(owner);
                uint att = BitConverter.ToUInt32(owner.Body, on.Attenuation);
                uint copy;
                if (!copies.TryGetValue(att, out copy))
                    continue;
                if (owner == t)
                    Put32(t.Body, on.Attenuation, copy);
                else
                {
                    //The inherited positioning becomes the target's own, pointing at the copy
                    byte[] block = Slice(owner.Body, on.Positioning, on.Aux);
                    Put32(block, on.Attenuation - on.Positioning, copy);
                    Node tn = Node.Parse(t);
                    t.Body = Splice(t.Body, tn.Positioning, tn.Aux, block);
                }
            }
            foreach (Obj below in overriding)
            {
                Node bn = Node.Parse(below);
                uint copy;
                if (bn != null && bn.Attenuation >= 0 && copies.TryGetValue(BitConverter.ToUInt32(below.Body, bn.Attenuation), out copy))
                    Put32(below.Body, bn.Attenuation, copy);
            }
        }

        /// <summary>An object's positioning block put back as the shipped object has it (the rest of the object as it is now).</summary>
        private static void RestorePositioning(Obj now, Obj shipped)
        {
            Node a = Node.Parse(now), b = Node.Parse(shipped);
            if (a == null || b == null || a.Aux < 0 || b.Aux < 0)
                return;
            byte[] block = Slice(shipped.Body, b.Positioning, b.Aux);
            if (!Slice(now.Body, a.Positioning, a.Aux).SequenceEqual(block))
                now.Body = Splice(now.Body, a.Positioning, a.Aux, block);
        }

        /// <summary>The id an event's copy of an attenuation goes under: the same every time (so changing the reach again reuses it), and in use nowhere else.</summary>
        private static uint CopyId(uint eventId, uint attenuation, Bank bank, Bank original, Func<uint, bool> taken)
        {
            for (int n = 0; ; n++)
            {
                uint id = CathodeLib.Utilities.SoundHashedString("OpenCAGE/tuned/" + eventId + "/" + attenuation + (n == 0 ? "" : "/" + n));
                if (id == 0 || original.Get(id) != null)
                    continue;
                Obj there = bank.Get(id);
                if (there != null && there.Type == (byte)WwiseObjectType.Attenuation)
                    return id; //made by an earlier change: reused
                if (there != null || (taken != null && taken(id)))
                    continue;
                return id;
            }
        }

        /// <summary>Attenuations added by tuning (none of the base game's bank's) that nothing in the bank points at any more.</summary>
        private static void RemoveUnusedCopies(Bank bank, Bank original)
        {
            List<Obj> added = bank.Objects.Where(o => o.Type == (byte)WwiseObjectType.Attenuation && original.Get(o.Id) == null).ToList();
            if (added.Count == 0)
                return;
            HashSet<uint> used = new HashSet<uint>();
            foreach (Obj o in bank.Objects)
            {
                Node n = Node.Parse(o);
                if (n != null && n.Attenuation >= 0)
                    used.Add(BitConverter.ToUInt32(o.Body, n.Attenuation));
            }
            foreach (Obj o in added.Where(o => !used.Contains(o.Id)))
                bank.Remove(o);
        }

        #endregion

        #region THE HIERARCHY

        private static readonly byte[] NodeTypes = { (byte)WwiseObjectType.Sound, (byte)WwiseObjectType.RandomSequenceContainer, (byte)WwiseObjectType.SwitchContainer, (byte)WwiseObjectType.ActorMixer, (byte)WwiseObjectType.BlendContainer };

        private static uint ParentOf(Obj o)
        {
            Node n = Node.Parse(o);
            return n == null ? 0 : BitConverter.ToUInt32(o.Body, n.Parent);
        }

        /// <summary>Everything below an object in this bank (found by the children's parent ids), nearest first.</summary>
        private static List<Obj> Under(Bank bank, Obj top)
        {
            List<Obj> found = new List<Obj>();
            HashSet<uint> seen = new HashSet<uint> { top.Id };
            Queue<uint> todo = new Queue<uint>();
            todo.Enqueue(top.Id);
            Dictionary<uint, List<Obj>> children = bank.Children();
            while (todo.Count != 0)
            {
                List<Obj> kids;
                if (!children.TryGetValue(todo.Dequeue(), out kids))
                    continue;
                foreach (Obj k in kids)
                    if (seen.Add(k.Id))
                    {
                        found.Add(k);
                        todo.Enqueue(k.Id);
                    }
            }
            return found;
        }

        /// <summary>The sounds an object plays: itself, for a sound, else the sounds below it.</summary>
        private static List<Obj> SoundsUnder(Bank bank, Obj top)
        {
            if (top.Type == (byte)WwiseObjectType.Sound)
                return new List<Obj> { top };
            return Under(bank, top).Where(o => o.Type == (byte)WwiseObjectType.Sound && Node.Parse(o) != null).ToList();
        }

        /// <summary>The object whose positioning block this one plays with: itself when it sets its own, else the nearest one above that does.</summary>
        private static Obj PositioningOwner(Bank bank, Obj o)
        {
            HashSet<uint> seen = new HashSet<uint>();
            while (o != null && seen.Add(o.Id))
            {
                Node n = Node.Parse(o);
                if (n == null)
                    return null;
                if (o.Body[n.Positioning] != 0)
                    return o;
                uint parent = BitConverter.ToUInt32(o.Body, n.Parent);
                o = parent == 0 ? null : bank.Get(parent);
            }
            return null;
        }

        /// <summary>The nearest actor-mixer above an object (0 for none in this bank), and the first output bus set on the way up.</summary>
        private static uint MixerAbove(Bank bank, uint id, out uint bus)
        {
            bus = 0;
            uint mixer = 0;
            Obj o = bank.Get(id);
            HashSet<uint> seen = new HashSet<uint>();
            bool first = true;
            while (o != null && seen.Add(o.Id))
            {
                Node n = Node.Parse(o);
                if (n == null)
                    break;
                if (!first && mixer == 0 && o.Type == (byte)WwiseObjectType.ActorMixer)
                    mixer = o.Id;
                if (bus == 0)
                    bus = BitConverter.ToUInt32(o.Body, n.Bus);
                first = false;
                uint parent = BitConverter.ToUInt32(o.Body, n.Parent);
                o = parent == 0 ? null : bank.Get(parent);
            }
            return mixer;
        }

        /// <summary>A v88 sound's source record: a source plugin 11 bytes, streamed audio 17, audio in the bank (or its prefetched opening) 25.</summary>
        private static int SourceLength(byte[] body)
        {
            if (body.Length < 16)
                return body.Length;
            uint plugin = BitConverter.ToUInt32(body, 0), stream = BitConverter.ToUInt32(body, 4);
            return (plugin & 0x0F) != 1 ? 11 : stream == (uint)WwiseStreamType.Streamed ? 17 : 25;
        }

        /// <summary>
        /// Where the parts of a v88 sound's or container's NodeBaseParams sit: the effects, the bus and parent ids, two priority bytes, the
        /// property list, the randomised ranges, then the positioning block (override; 2D and 3D flags; the panner byte with 2D; with 3D a
        /// u32 kind, the attenuation id, the spatialisation byte and, for game-defined positioning, one more) - the same reading the
        /// soundbank builder makes. A path-driven 3D block isn't laid out (<see cref="Path"/>), and stops the reading there.
        /// </summary>
        private sealed class Node
        {
            public int Bus, Parent, PropsAt, Ranged, Positioning, Aux = -1;

            /// <summary>The attenuation id's offset in the body, or -1 when the object doesn't set 3D positioning of its own.</summary>
            public int Attenuation = -1;
            public bool Path;

            public static Node Parse(Obj o)
            {
                if (o == null || Array.IndexOf(NodeTypes, o.Type) < 0)
                    return null;
                byte[] b = o.Body;
                try
                {
                    int p = o.Type == (byte)WwiseObjectType.Sound ? SourceLength(b) : 0;
                    Node n = new Node();
                    p += 1;
                    int fx = b[p++];
                    if (fx > 0) p += 1 + 7 * fx;
                    n.Bus = p;
                    n.Parent = p + 4;
                    p += 10;
                    n.PropsAt = p;
                    int count = b[p++];
                    p += count + 4 * count;
                    n.Ranged = p;
                    count = b[p++];
                    p += count + 8 * count;
                    n.Positioning = p;
                    if (b[p++] != 0)
                    {
                        int has2d = b[p++], has3d = b[p++];
                        if (has2d != 0) p++;
                        if (has3d != 0)
                        {
                            uint kind = BitConverter.ToUInt32(b, p);
                            if (kind != 1)
                            {
                                n.Path = true;
                                n.Aux = -1;
                                return p + 8 <= b.Length ? n : null;
                            }
                            n.Attenuation = p + 4;
                            p += 10;
                        }
                    }
                    n.Aux = p;
                    if (p + 3 > b.Length)
                        return null;
                    return n;
                }
                catch (IndexOutOfRangeException)
                {
                    return null;
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            public bool PropBits(byte[] b, byte id, out uint value)
            {
                value = 0;
                int n = b[PropsAt];
                for (int i = 0; i < n; i++)
                    if (b[PropsAt + 1 + i] == id)
                    {
                        value = BitConverter.ToUInt32(b, PropsAt + 1 + n + 4 * i);
                        return true;
                    }
                return false;
            }

            public bool Prop(byte[] b, byte id, out float value)
            {
                uint bits;
                bool has = PropBits(b, id, out bits);
                value = has ? BitConverter.ToSingle(BitConverter.GetBytes(bits), 0) : 0;
                return has;
            }
        }

        /// <summary>A property of a sound or container set, added to its list (in id order) when it hasn't one.</summary>
        private static void SetNodeProp(Obj o, byte id, uint value)
        {
            Node n = Node.Parse(o);
            if (n == null)
                return;
            o.Body = SetProp(o.Body, n.PropsAt, id, value);
        }

        /// <summary>A property of a sound or container taken out of its list, when it has it.</summary>
        private static void RemoveNodeProp(Obj o, byte id)
        {
            Node n = Node.Parse(o);
            if (n == null)
                return;
            byte[] b = o.Body;
            int at = n.PropsAt, count = b[at];
            int slot = -1;
            for (int i = 0; i < count; i++)
                if (b[at + 1 + i] == id) slot = i;
            if (slot < 0)
                return;
            int ids = at + 1, values = at + 1 + count;
            byte[] shrunk = new byte[b.Length - 5];
            Array.Copy(b, 0, shrunk, 0, ids + slot);
            Array.Copy(b, ids + slot + 1, shrunk, ids + slot, (count - slot - 1) + 4 * slot);
            Array.Copy(b, values + 4 * slot + 4, shrunk, values - 1 + 4 * slot, b.Length - (values + 4 * slot + 4));
            shrunk[at] = (byte)(count - 1);
            o.Body = shrunk;
        }

        /// <summary>A property set, unless it is being set to its default (0) on an object that doesn't list it - which already means that.</summary>
        private static void SetNodePropUnlessDefault(Obj o, byte id, float value)
        {
            Node n = Node.Parse(o);
            float now;
            if (n == null || (value == 0 && !n.Prop(o.Body, id, out now)))
                return;
            SetNodeProp(o, id, BitConverter.ToUInt32(BitConverter.GetBytes(value), 0));
        }

        private static void SetActionPropUnlessDefault(Obj a, byte id, uint value)
        {
            uint now;
            if (value == 0 && !ActionProp(a.Body, id, out now))
                return;
            SetActionProp(a, id, value);
        }

        /// <summary>A property of an action set, added to its list when it hasn't one.</summary>
        private static void SetActionProp(Obj a, byte id, uint value)
        {
            a.Body = SetProp(a.Body, 7, id, value);
        }

        /// <summary>A property list (u8 count, the ids, a u32 value each) at <paramref name="at"/> with one value set, or the property added.</summary>
        private static byte[] SetProp(byte[] b, int at, byte id, uint value)
        {
            int n = b[at];
            for (int i = 0; i < n; i++)
                if (b[at + 1 + i] == id)
                {
                    byte[] same = (byte[])b.Clone();
                    Put32(same, at + 1 + n + 4 * i, value);
                    return same;
                }
            if (n == 255)
                throw new InvalidDataException("An object has no room for another property.");

            //In id order, as the game's banks keep them (when they do)
            int slot = n;
            bool sorted = true;
            for (int i = 1; i < n; i++)
                if (b[at + 1 + i] < b[at + i]) sorted = false;
            if (sorted)
                for (int i = 0; i < n; i++)
                    if (b[at + 1 + i] > id) { slot = i; break; }

            byte[] grown = new byte[b.Length + 5];
            int ids = at + 1, values = at + 1 + n;
            //Up to the slot in the ids, the new id, the rest of the ids and the values before the slot, the new value, the rest
            Array.Copy(b, 0, grown, 0, ids + slot);
            grown[ids + slot] = id;
            Array.Copy(b, ids + slot, grown, ids + slot + 1, (n - slot) + 4 * slot);
            Put32(grown, values + 1 + 4 * slot, value);
            Array.Copy(b, values + 4 * slot, grown, values + 1 + 4 * slot + 4, b.Length - (values + 4 * slot));
            grown[at] = (byte)(n + 1);
            return grown;
        }

        private static int RangedAt(byte[] action)
        {
            int n = action[7];
            return 8 + n + 4 * n;
        }

        /// <summary>A randomised range on an action's property zeroed (min and max 0), where it has one.</summary>
        private static void ClearRange(Obj a, byte id)
        {
            int at = RangedAt(a.Body);
            int n = a.Body[at];
            for (int i = 0; i < n; i++)
                if (a.Body[at + 1 + i] == id)
                {
                    Put32(a.Body, at + 1 + n + 8 * i, 0);
                    Put32(a.Body, at + 1 + n + 8 * i + 4, 0);
                }
        }

        private static bool ActionProp(byte[] b, byte id, out uint value)
        {
            value = 0;
            int n = b[7];
            for (int i = 0; i < n; i++)
                if (b[8 + i] == id)
                {
                    value = BitConverter.ToUInt32(b, 8 + n + 4 * i);
                    return true;
                }
            return false;
        }

        /// <summary>
        /// The farthest distance an attenuation's curves reach (their last point's distance), when it lays out as the game's all do: a
        /// cone flag (and 16 bytes of cone with it), five curve indexes, the curves (a scaling byte, a u16 point count, 12 bytes a point:
        /// distance, value, interpolation), and an RTPC count of 0.
        /// </summary>
        private static bool AttenuationMax(byte[] b, out float max)
        {
            max = 0;
            List<int> xs = AttenuationPoints(b);
            if (xs == null)
                return false;
            foreach (int at in xs)
                max = Math.Max(max, BitConverter.ToSingle(b, at));
            return true;
        }

        private static List<int> AttenuationPoints(byte[] b)
        {
            try
            {
                int q = 0;
                bool cone = b[q++] != 0;
                if (cone) q += 16;
                q += 5;
                int curves = b[q++];
                List<int> xs = new List<int>();
                for (int c = 0; c < curves; c++)
                {
                    q++;
                    int points = BitConverter.ToUInt16(b, q);
                    q += 2;
                    for (int k = 0; k < points; k++)
                    {
                        xs.Add(q);
                        q += 12;
                    }
                }
                int rtpcs = BitConverter.ToUInt16(b, q);
                q += 2;
                return rtpcs == 0 && q == b.Length ? xs : null;
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

        private static void ScaleAttenuation(byte[] b, float factor)
        {
            foreach (int at in AttenuationPoints(b))
                Put32(b, at, BitConverter.ToUInt32(BitConverter.GetBytes(BitConverter.ToSingle(b, at) * factor), 0));
        }

        #endregion

        #region THE BANK

        private sealed class Obj
        {
            public byte Type;
            public uint Id;
            public byte[] Body;
        }

        /// <summary>A soundbank's chunks, its hierarchy as objects in their order, written back byte for byte when nothing changed.</summary>
        private sealed class Bank
        {
            private byte[] _bytes;
            private readonly List<KeyValuePair<string, KeyValuePair<int, int>>> _chunks = new List<KeyValuePair<string, KeyValuePair<int, int>>>();
            private int _tail = -1;
            public List<Obj> Objects = new List<Obj>();
            private Dictionary<uint, List<Obj>> _byId;
            private Dictionary<uint, List<Obj>> _children;
            public uint Version;
            public bool HierarchyAfterAudio = true;

            public static Bank Parse(byte[] bytes)
            {
                Bank bank = new Bank { _bytes = bytes };
                int pos = 0, hircAt = -1, dataAt = -1;
                while (pos + 8 <= bytes.Length)
                {
                    string tag = Encoding.ASCII.GetString(bytes, pos, 4);
                    int size = BitConverter.ToInt32(bytes, pos + 4);
                    if (size < 0 || pos + 8 + (long)size > bytes.Length)
                        break;
                    bank._chunks.Add(new KeyValuePair<string, KeyValuePair<int, int>>(tag, new KeyValuePair<int, int>(pos + 8, size)));
                    if (tag == "BKHD" && size >= 4)
                        bank.Version = BitConverter.ToUInt32(bytes, pos + 8);
                    if (tag == "DATA") dataAt = pos;
                    if (tag == "HIRC")
                    {
                        hircAt = pos;
                        int count = BitConverter.ToInt32(bytes, pos + 8), p = pos + 12, end = pos + 8 + size;
                        for (int i = 0; i < count && p + 9 <= end; i++)
                        {
                            int length = BitConverter.ToInt32(bytes, p + 1);
                            if (length < 4 || p + 5 + length > end)
                                throw new InvalidDataException("The soundbank's hierarchy doesn't read.");
                            Obj o = new Obj { Type = bytes[p], Id = BitConverter.ToUInt32(bytes, p + 5), Body = new byte[length - 4] };
                            Array.Copy(bytes, p + 9, o.Body, 0, o.Body.Length);
                            bank.Objects.Add(o);
                            p += 5 + length;
                        }
                    }
                    pos += 8 + size;
                }
                if (pos < bytes.Length)
                    bank._tail = pos;
                if (hircAt >= 0 && dataAt > hircAt)
                    bank.HierarchyAfterAudio = false;
                return bank;
            }

            public byte[] Write()
            {
                using (MemoryStream output = new MemoryStream(_bytes.Length + 1024))
                {
                    foreach (KeyValuePair<string, KeyValuePair<int, int>> chunk in _chunks)
                    {
                        output.Write(Encoding.ASCII.GetBytes(chunk.Key), 0, 4);
                        if (chunk.Key != "HIRC")
                        {
                            output.Write(BitConverter.GetBytes(chunk.Value.Value), 0, 4);
                            output.Write(_bytes, chunk.Value.Key, chunk.Value.Value);
                            continue;
                        }
                        int size = 4 + Objects.Sum(o => 9 + o.Body.Length);
                        output.Write(BitConverter.GetBytes(size), 0, 4);
                        output.Write(BitConverter.GetBytes(Objects.Count), 0, 4);
                        foreach (Obj o in Objects)
                        {
                            output.WriteByte(o.Type);
                            output.Write(BitConverter.GetBytes(o.Body.Length + 4), 0, 4);
                            output.Write(BitConverter.GetBytes(o.Id), 0, 4);
                            output.Write(o.Body, 0, o.Body.Length);
                        }
                    }
                    if (_tail >= 0)
                        output.Write(_bytes, _tail, _bytes.Length - _tail);
                    return output.ToArray();
                }
            }

            private Dictionary<uint, List<Obj>> ById()
            {
                if (_byId == null)
                {
                    _byId = new Dictionary<uint, List<Obj>>();
                    foreach (Obj o in Objects)
                    {
                        List<Obj> list;
                        if (!_byId.TryGetValue(o.Id, out list))
                            _byId.Add(o.Id, list = new List<Obj>());
                        list.Add(o);
                    }
                }
                return _byId;
            }

            /// <summary>The object with this id (of this type, when given), or null.</summary>
            public Obj Get(uint id, WwiseObjectType? type = null)
            {
                List<Obj> list;
                if (!ById().TryGetValue(id, out list))
                    return null;
                return type == null ? list[0] : list.FirstOrDefault(o => o.Type == (byte)type.Value);
            }

            /// <summary>Each object's children in this bank, by the parent id each child names.</summary>
            public Dictionary<uint, List<Obj>> Children()
            {
                if (_children == null)
                {
                    _children = new Dictionary<uint, List<Obj>>();
                    foreach (Obj o in Objects)
                    {
                        uint parent = ParentOf(o);
                        if (parent == 0)
                            continue;
                        List<Obj> list;
                        if (!_children.TryGetValue(parent, out list))
                            _children.Add(parent, list = new List<Obj>());
                        list.Add(o);
                    }
                }
                return _children;
            }

            public void InsertAfter(Obj existing, Obj added)
            {
                int at = Objects.IndexOf(existing);
                Objects.Insert(at < 0 ? 0 : at + 1, added);
                _byId = null;
            }

            public void Remove(Obj o)
            {
                Objects.Remove(o);
                _byId = null;
            }
        }

        private static void Put32(byte[] b, int at, uint value)
        {
            b[at] = (byte)value;
            b[at + 1] = (byte)(value >> 8);
            b[at + 2] = (byte)(value >> 16);
            b[at + 3] = (byte)(value >> 24);
        }

        private static byte[] Slice(byte[] b, int from, int to)
        {
            byte[] slice = new byte[to - from];
            Array.Copy(b, from, slice, 0, slice.Length);
            return slice;
        }

        /// <summary>The bytes with [from, to) swapped for <paramref name="insert"/>.</summary>
        private static byte[] Splice(byte[] b, int from, int to, byte[] insert)
        {
            byte[] result = new byte[b.Length - (to - from) + insert.Length];
            Array.Copy(b, 0, result, 0, from);
            Array.Copy(insert, 0, result, from, insert.Length);
            Array.Copy(b, to, result, from + insert.Length, b.Length - to);
            return result;
        }

        #endregion
    }
}
