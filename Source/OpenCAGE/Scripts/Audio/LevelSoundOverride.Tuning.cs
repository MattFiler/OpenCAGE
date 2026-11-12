using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Per-level tuning of a sound event (volume, pitch, start delay, fades, stop reach, looping, max distance), made in the level's
    /// own copies of the soundbanks that carry it (<see cref="SoundEventTuning"/> edits the bytes).
    ///
    /// Every DATA/SOUND bank carrying the event (except other levels' DynamicLoad banks) is copied into the package the first time;
    /// a level's own soundbank (New Soundbank) is changed in place. Changes are read back by comparing copies with DATA/SOUND, and a
    /// copy that matches the base game again leaves the package. Events in permanent soundbanks can't be tuned per level: those load
    /// before any level's package, and Wwise keeps the first copy of an object it loads.
    /// </summary>
    internal static partial class LevelSoundOverride
    {
        /// <summary>The result of tuning, untuning or inspecting an event on a level.</summary>
        public sealed class Tuning
        {
            public Plan Plan;
            public string Event;

            /// <summary>The Stop event whose Stop actions took the fade out and reach (a Play event's own Stop_ event), or null.</summary>
            public string StopEvent;

            /// <summary>The event as the base game ships it, as the level played it before, and as it plays now (from its first soundbank).</summary>
            public SoundEventTuning.EventState Shipped, Before, After;
            public SoundEventTuning.EventState StopShipped, StopAfter;

            /// <summary>The soundbanks carrying the event on this level (the package's copies, or DATA/SOUND's).</summary>
            public List<string> Banks = new List<string>();

            /// <summary>Whether the level's package changes it now (after this call).</summary>
            public bool Tuned;

            /// <summary>Other events that play what this one plays: volume, pitch, loop and reach change for them too.</summary>
            public List<string> AlsoPlay = new List<string>();

            /// <summary>Everything the level now plays differently from the base game, in words.</summary>
            public List<string> Changes
            {
                get
                {
                    List<string> changes = SoundEventTuning.Differences(Shipped, After);
                    if (StopEvent != null)
                        changes.AddRange(SoundEventTuning.Differences(StopShipped, StopAfter).Select(o => StopEvent + ": " + o));
                    return changes;
                }
            }
        }

        /// <summary>One event the level's package plays differently from the base game.</summary>
        public sealed class TunedEvent
        {
            public string Bank;
            public uint BankId;
            public uint EventId;

            /// <summary>Its name, where the level's sound data names it; else null.</summary>
            public string Event;
            public List<string> Changes = new List<string>();

            public string Name { get { return Event ?? "Event " + EventId; } }
        }

        /// <summary>A level's copy of a soundbank carrying the event, and the base game's copy it was made from.</summary>
        private sealed class BankCopy
        {
            public WwiseFilePackageBuilder.Item Item;
            public string Name;
            public byte[] Base;
            public bool HasBase;
            public bool CopiedIn;
        }

        #region LOOKING

        /// <summary>How an event plays on the level now - its package's copy where it has one - without changing anything.</summary>
        public static Tuning Inspect(string package, string eventName)
        {
            Tuning result = new Tuning { Event = eventName, Plan = new Plan { Package = package } };
            Plan plan = result.Plan;
            try
            {
                SoundBankIndex index = SoundBankIndex.For(SoundPreviewLibrary.SoundDirectory);
                if (Refuse(eventName, index, plan))
                    return result;
                WwiseFilePackageBuilder builder = File.Exists(package) ? WwiseFilePackageBuilder.Load(package) : WwiseFilePackageBuilder.Create(SoundPreviewLibrary.NarrowPackageNames);
                uint eventId = CathodeLib.Utilities.SoundHashedString(eventName);
                List<BankCopy> copies = CopiesOf(eventId, package, builder, index, false, plan);
                if (copies.Count == 0)
                {
                    plan.Problem = NoBankProblem(eventName, eventId, index, package);
                    return result;
                }
                BankCopy first = copies[0];
                result.Banks = copies.Select(o => o.Name + (o.Item != null ? " (the level's copy)" : "")).ToList();
                result.Shipped = SoundEventTuning.Read(first.Base, eventId);
                result.Before = result.After = SoundEventTuning.Read(first.Item != null ? first.Item.Read() : first.Base, eventId);

                //Its Stop event, when it has no Stop action of its own
                string stopName = StopEventFor(eventName, result.After, index);
                if (stopName != null)
                {
                    uint stopId = CathodeLib.Utilities.SoundHashedString(stopName);
                    List<BankCopy> stops = CopiesOf(stopId, package, builder, index, false, plan);
                    if (stops.Count != 0)
                    {
                        result.StopEvent = stopName;
                        result.StopShipped = SoundEventTuning.Read(stops[0].Base, stopId);
                        result.StopAfter = SoundEventTuning.Read(stops[0].Item != null ? stops[0].Item.Read() : stops[0].Base, stopId);
                    }
                }
                result.Tuned = result.Changes.Count != 0;
                result.AlsoPlay = AlsoPlaying(eventId, result.After, index);
                plan.Notes.AddRange(SharingNotes(eventName, result.After, copies, index, package));
            }
            catch (Exception e)
            {
                plan.Problem = "The event couldn't be read: " + e.Message;
            }
            return result;
        }

        /// <summary>Every event the level's package plays differently from the base game, by comparing each bank copy with DATA/SOUND's.</summary>
        public static List<TunedEvent> TunedEvents(string package)
        {
            List<TunedEvent> found = new List<TunedEvent>();
            if (!File.Exists(package))
                return found;
            Dictionary<uint, string> names = EventNames();
            WwiseFilePackageBuilder builder = WwiseFilePackageBuilder.Load(package);
            foreach (WwiseFilePackageBuilder.Item bank in builder.Banks)
            {
                string baseFile = BaseBankFile(bank.Id, (WwiseSoundLibrary)null);
                if (baseFile == null)
                    continue;
                byte[] now = bank.Read();
                if (SoundEventTuning.VersionOf(now) != SoundEventTuning.SupportedVersion)
                    continue;
                byte[] shipped = File.ReadAllBytes(baseFile);
                if (shipped.Length == now.Length && shipped.SequenceEqual(now))
                    continue;
                Dictionary<uint, SoundEventTuning.EventState> before = SoundEventTuning.ReadAll(shipped);
                Dictionary<uint, SoundEventTuning.EventState> after = SoundEventTuning.ReadAll(now, before.Keys);
                foreach (KeyValuePair<uint, SoundEventTuning.EventState> ev in after)
                {
                    List<string> changes = SoundEventTuning.Differences(before[ev.Key], ev.Value);
                    if (changes.Count == 0)
                        continue;
                    string name;
                    found.Add(new TunedEvent
                    {
                        Bank = Path.GetFileName(baseFile),
                        BankId = bank.Id,
                        EventId = ev.Key,
                        Event = names.TryGetValue(ev.Key, out name) ? name : null,
                        Changes = changes,
                    });
                }
            }
            return found;
        }

        #endregion

        #region TUNING

        /// <summary>
        /// Changes how an event plays on the level, in its sound package. A fade out or stop reach for a Play event with no Stop action
        /// goes on its Stop event (Play_X's Stop_X). With <paramref name="write"/> false nothing is written; the result says what would change.
        /// </summary>
        public static Tuning Tune(string package, string eventName, SoundEventTuning.Settings settings, bool write)
        {
            Tuning result = new Tuning { Event = eventName, Plan = new Plan { Package = package } };
            Plan plan = result.Plan;
            string invalid = settings == null ? "Nothing to change." : settings.Check();
            if (invalid == null && !settings.Any)
                invalid = "Nothing to change: give a volume, pitch, delay, fade, stop reach, loop or reach.";
            if (invalid != null)
            {
                plan.Problem = invalid;
                return result;
            }

            WwiseFilePackageBuilder builder;
            SoundBankIndex index;
            uint eventId = CathodeLib.Utilities.SoundHashedString(eventName);
            List<BankCopy> copies;
            try
            {
                index = SoundBankIndex.For(SoundPreviewLibrary.SoundDirectory);
                if (Refuse(eventName, index, plan))
                    return result;
                builder = Open(package, plan);
                copies = CopiesOf(eventId, package, builder, index, true, plan);
            }
            catch (Exception e)
            {
                plan.Problem = "The level's sound package can't be read: " + e.Message;
                return result;
            }
            if (copies.Count == 0)
            {
                plan.Problem = NoBankProblem(eventName, eventId, index, package);
                return result;
            }

            //A Play event with no Stop action takes its fade out and stop reach on its Stop event
            SoundEventTuning.EventState first = SoundEventTuning.Read(copies[0].Item.Read(), eventId);
            SoundEventTuning.Settings forEvent = settings;
            string stopName = null;
            if (settings.ChangesStop && first != null && !first.Stops.Any())
            {
                stopName = StopEventFor(eventName, first, index);
                if (stopName == null)
                {
                    plan.Problem = eventName + " has no Stop action, and there is no Stop_ event for it: a fade out or stop reach goes on the event a Sound's stop_event plays.";
                    return result;
                }
                forEvent = new SoundEventTuning.Settings { VolumeDb = settings.VolumeDb, VolumeChangeDb = settings.VolumeChangeDb, PitchCents = settings.PitchCents, DelayMs = settings.DelayMs, FadeInMs = settings.FadeInMs, Loop = settings.Loop, MaxDistance = settings.MaxDistance };
            }

            try
            {
                Func<uint, bool> taken = id => index.Has(id);
                result.Banks = copies.Select(o => o.Name).ToList();
                result.Shipped = SoundEventTuning.Read(copies[0].Base, eventId);
                result.Before = first;
                if (forEvent.Any && !TuneCopies(eventId, copies, forEvent, taken, plan))
                    return result;
                result.After = SoundEventTuning.Read(copies[0].Item.Read(), eventId);

                if (stopName != null)
                {
                    uint stopId = CathodeLib.Utilities.SoundHashedString(stopName);
                    if (Refuse(stopName, index, plan))
                        return result;
                    List<BankCopy> stops = CopiesOf(stopId, package, builder, index, true, plan);
                    if (stops.Count == 0)
                    {
                        plan.Problem = NoBankProblem(stopName, stopId, index, package);
                        return result;
                    }
                    if (!TuneCopies(stopId, stops, settings.StopPart(), taken, plan))
                        return result;
                    result.StopEvent = stopName;
                    result.StopShipped = SoundEventTuning.Read(stops[0].Base, stopId);
                    result.StopAfter = SoundEventTuning.Read(stops[0].Item.Read(), stopId);
                    foreach (BankCopy copy in stops.Where(o => !copies.Any(c => c.Item == o.Item)))
                        copies.Add(copy);
                }
                else if (result.StopEvent == null)
                {
                    string companion = StopEventFor(eventName, result.After, index);
                    if (companion != null)
                    {
                        //Shown with the event, as it is now
                        uint stopId = CathodeLib.Utilities.SoundHashedString(companion);
                        List<BankCopy> stops = CopiesOf(stopId, package, builder, index, false, plan);
                        if (stops.Count != 0)
                        {
                            result.StopEvent = companion;
                            result.StopShipped = SoundEventTuning.Read(stops[0].Base, stopId);
                            result.StopAfter = SoundEventTuning.Read(stops[0].Item != null ? stops[0].Item.Read() : stops[0].Base, stopId);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                plan.Problem = "The event couldn't be tuned: " + e.Message;
                return result;
            }

            //Copies that are the base game's again leave the package
            foreach (BankCopy copy in copies.Where(o => o.HasBase))
            {
                byte[] now = copy.Item.Read();
                if (now.Length == copy.Base.Length && now.SequenceEqual(copy.Base))
                {
                    builder.Banks.Remove(copy.Item);
                    plan.BanksCopied.Remove(copy.Name);
                    plan.BanksChanged.Remove(copy.Name);
                    if (!copy.CopiedIn)
                        plan.BanksRemoved.Add(copy.Name);
                }
            }

            result.Tuned = result.Changes.Count != 0;
            result.AlsoPlay = AlsoPlaying(eventId, result.After, index);
            if (result.AlsoPlay.Count != 0 && (settings.VolumeDb.HasValue || settings.VolumeChangeDb.HasValue || settings.PitchCents.HasValue || settings.Loop.HasValue || settings.MaxDistance.HasValue))
                plan.Notes.Add("What it plays is played by " + result.AlsoPlay.Count + " other event" + (result.AlsoPlay.Count == 1 ? "" : "s") + " too (" + string.Join(", ", result.AlsoPlay.Take(4)) + (result.AlsoPlay.Count > 4 ? "..." : "")
                    + "): on this level the volume, pitch, loop and reach change for " + (result.AlsoPlay.Count == 1 ? "it" : "them") + " as well. Delays and fades are the event's own.");
            plan.Notes.AddRange(SharingNotes(eventName, result.After, copies, index, package));
            Done(result, builder, write);
            if (settings.MaxDistance.HasValue && result.Before != null && result.After != null && result.Before.MaxDistance.HasValue && result.After.MaxDistance.HasValue)
                MoveTableReach(result, package, result.Before.MaxDistance.Value, result.After.MaxDistance.Value, write);
            return result;
        }

        /// <summary>
        /// Keeps the open level's sound event table (WORLD\SOUNDEVENTDATA.DAT) in step with a changed reach: entries whose max_attenuation
        /// equals the old reach get the new one; other values (often 1024) are left alone. The game only reads it to place static ambience
        /// on the ambience grid (SOUND_DATABASE::get_event_max_radius, called only from SOUND_AMBIENCE_GRID::add_sound_to_grid). Written
        /// immediately, as the level's own soundbanks write their events there too.
        /// </summary>
        private static void MoveTableReach(Tuning result, string package, float from, float to, bool write)
        {
            if (Math.Abs(from - to) < 0.001f || !result.Plan.Ok)
                return;
            try
            {
                LevelContent content = Singleton.Editor == null || Singleton.Editor.CompositeBrowser == null ? null : Singleton.Editor.CompositeBrowser.Content;
                if (content == null || content.Level == null || content.Level.SoundEventData == null || !SameFile(Path.GetFullPath(package), PathForOpenLevel() ?? ""))
                    return;
                CATHODE.SoundEventData table = content.Level.SoundEventData;
                List<CATHODE.SoundEventData.Soundbank.Event> entries = table.Entries.SelectMany(o => o.events).Where(o => string.Equals(o.name, result.Event, StringComparison.OrdinalIgnoreCase) && Math.Abs(o.max_attenuation - from) < 0.01f).ToList();
                if (entries.Count == 0)
                    return;
                result.Plan.Notes.Add("The level's sound event table gives " + result.Event + " a reach of " + to.ToString("0.##") + " m too (it had " + from.ToString("0.##") + " m): static ambience is placed by it.");
                if (!write)
                    return;
                foreach (CATHODE.SoundEventData.Soundbank.Event entry in entries)
                    entry.max_attenuation = to;
                Modding.ModServices.CaptureBeforeWrite(table.Filepath);
                table.Save();
                SoundEventMetadata.Invalidate();
            }
            catch (Exception e)
            {
                result.Plan.Notes.Add("The level's sound event table couldn't be given the new reach: " + e.Message);
            }
        }

        /// <summary>
        /// Restores the event (and its Stop event, if tuned) to the base game's version on this level; a bank copy that then matches the
        /// base game leaves the package.
        /// </summary>
        public static Tuning Untune(string package, string eventName, bool write)
        {
            Tuning result = new Tuning { Event = eventName, Plan = new Plan { Package = package } };
            Plan plan = result.Plan;
            if (!File.Exists(package))
            {
                plan.Problem = "This level has no sound package.";
                return result;
            }

            WwiseFilePackageBuilder builder;
            SoundBankIndex index;
            try
            {
                index = SoundBankIndex.For(SoundPreviewLibrary.SoundDirectory);
                builder = Open(package, plan);
            }
            catch (Exception e)
            {
                plan.Problem = "The level's sound package can't be read: " + e.Message;
                return result;
            }

            List<string> names = new List<string> { eventName };
            uint eventId = CathodeLib.Utilities.SoundHashedString(eventName);
            string stopName = StopEventFor(eventName, null, index);
            if (stopName != null)
                names.Add(stopName);

            int restored = 0;
            float? tunedReach = null, shippedReach = null;
            try
            {
                foreach (string name in names)
                {
                    uint id = CathodeLib.Utilities.SoundHashedString(name);
                    foreach (BankCopy copy in CopiesOf(id, package, builder, index, false, plan).Where(o => o.Item != null && o.HasBase))
                    {
                        if (id == eventId && tunedReach == null)
                        {
                            SoundEventTuning.EventState now = SoundEventTuning.Read(copy.Item.Read(), id), shipped = SoundEventTuning.Read(copy.Base, id);
                            tunedReach = now == null ? null : now.MaxDistance;
                            shippedReach = shipped == null ? null : shipped.MaxDistance;
                        }
                        int count;
                        byte[] back = SoundEventTuning.Revert(copy.Item.Read(), copy.Base, id, out count);
                        if (back == null || count == 0)
                            continue;
                        restored += count;
                        if (back.Length == copy.Base.Length && back.SequenceEqual(copy.Base))
                        {
                            builder.Banks.Remove(copy.Item);
                            if (!plan.BanksRemoved.Contains(copy.Name)) plan.BanksRemoved.Add(copy.Name);
                            plan.BanksChanged.Remove(copy.Name);
                        }
                        else
                        {
                            copy.Item.Data = back;
                            if (!plan.BanksChanged.Contains(copy.Name)) plan.BanksChanged.Add(copy.Name);
                        }
                        if (name == stopName)
                            result.StopEvent = stopName;
                    }
                }
            }
            catch (Exception e)
            {
                plan.Problem = "The event couldn't be put back: " + e.Message;
                return result;
            }
            if (restored == 0)
            {
                plan.Problem = "This level's sound package doesn't change " + eventName + (stopName != null ? " or " + stopName : "") + ".";
                return result;
            }
            plan.Notes.Insert(0, eventName + (result.StopEvent != null ? " and " + result.StopEvent + " play as the base game ships them" : " plays as the base game ships it") + " again on this level (" + restored + " object" + (restored == 1 ? "" : "s") + " put back).");
            Done(result, builder, write);
            if (tunedReach.HasValue && shippedReach.HasValue)
                MoveTableReach(result, package, tunedReach.Value, shippedReach.Value, write);
            return result;
        }

        private static Tuning Done(Tuning result, WwiseFilePackageBuilder builder, bool write)
        {
            Plan plan = result.Plan;
            Finish(builder, plan, write);
            //Finish words its "copied in" note for a replace: reword it for tuning
            for (int i = 0; i < plan.Notes.Count; i++)
                if (plan.Notes[i].StartsWith("Copied into the level's package with the sound changed inside", StringComparison.Ordinal))
                    plan.Notes[i] = plan.Notes[i].Replace("with the sound changed inside", "with the event tuned inside");
            return result;
        }

        /// <summary>Tune each copy; false (with the plan's problem set) when one can't be.</summary>
        private static bool TuneCopies(uint eventId, List<BankCopy> copies, SoundEventTuning.Settings settings, Func<uint, bool> taken, Plan plan)
        {
            List<string> notes = new List<string>();
            foreach (BankCopy copy in copies)
            {
                string problem;
                byte[] tuned = SoundEventTuning.Apply(copy.Item.Read(), copy.Base, eventId, settings, taken, notes, out problem);
                if (tuned == null)
                {
                    plan.Problem = copy.Name + ": " + problem;
                    return false;
                }
                copy.Item.Data = tuned;
                //A copy made in this same call (for the event, then changed again for its Stop event) is reported once, as copied in
                List<string> list = copy.CopiedIn ? plan.BanksCopied : plan.BanksChanged;
                if (!list.Contains(copy.Name) && !plan.BanksCopied.Contains(copy.Name))
                    list.Add(copy.Name);
            }
            foreach (string note in notes.Distinct())
                plan.Notes.Add(note);
            return true;
        }

        #endregion

        #region SHARED

        /// <summary>
        /// The level's copies of the soundbanks carrying an event: the package's own (a soundbank of the level's own is its own base), then
        /// each one in DATA/SOUND the level can load - copied in when <paramref name="copyIn"/>, else listed as the base game's (no item).
        /// </summary>
        private static List<BankCopy> CopiesOf(uint eventId, string package, WwiseFilePackageBuilder builder, SoundBankIndex index, bool copyIn, Plan plan)
        {
            List<BankCopy> copies = new List<BankCopy>();
            foreach (WwiseFilePackageBuilder.Item item in builder.Banks.Where(o => o.LanguageId == builder.SfxLanguage).ToList())
            {
                byte[] bytes = item.Read();
                if (!SoundEventTuning.Carries(bytes, eventId))
                    continue;
                string baseFile = BaseBankFile(item.Id, (WwiseSoundLibrary)null);
                copies.Add(new BankCopy
                {
                    Item = item,
                    Name = baseFile != null ? Path.GetFileName(baseFile) : SoundNames.BankName(item.Id, bytes),
                    Base = baseFile != null ? File.ReadAllBytes(baseFile) : bytes,
                    HasBase = baseFile != null,
                });
            }

            foreach (SoundBankIndex.BankFile bank in index.BanksWith(eventId))
            {
                if (bank.Permanent || IsOtherLevelsDynamicBank(bank.File, package) || copies.Any(o => o.Item != null && o.Item.Id == bank.Id))
                    continue;
                byte[] bytes = File.ReadAllBytes(bank.File);
                BankCopy copy = new BankCopy { Name = Path.GetFileName(bank.File), Base = bytes, HasBase = true };
                if (copyIn)
                {
                    copy.Item = builder.Set(builder.Banks, bank.Id, builder.SfxLanguage, bytes);
                    copy.CopiedIn = true;
                }
                copies.Add(copy);
            }
            return copies;
        }

        /// <summary>Whether the event can't be tuned per level at all (the plan says why).</summary>
        private static bool Refuse(string eventName, SoundBankIndex index, Plan plan)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                plan.Problem = "Name the sound event.";
                return true;
            }
            if (SoundPreviewLibrary.Modern)
            {
                plan.Problem = "Sound events can be tuned in the PC, Mac and Linux soundbanks (Wwise bank version 88); this game's are version " + SoundPreviewLibrary.BankVersion + ".";
                return true;
            }
            uint eventId = CathodeLib.Utilities.SoundHashedString(eventName);
            SoundBankIndex.BankFile permanent = index.BanksWith(eventId).FirstOrDefault(o => o.Permanent);
            if (permanent != null)
            {
                plan.Problem = eventName + " is in " + Path.GetFileName(permanent.File) + ", one of the soundbanks the game loads as it starts and keeps loaded (Init, UI and those in "
                    + "DATA\\LIST_OF_PERMANENT_SOUND_BANKS.TXT). That copy is in memory before any level's sound package is read, and the game keeps the first copy it loads - "
                    + "so this event can only be changed for every level.";
                plan.HeldPermanently = true;
                return true;
            }
            return false;
        }

        private static string NoBankProblem(string eventName, uint eventId, SoundBankIndex index, string package)
        {
            List<SoundBankIndex.BankFile> elsewhere = index.BanksWith(eventId).ToList();
            if (elsewhere.Count != 0)
                return eventName + " is only in other levels' DynamicLoad soundbanks (" + string.Join(", ", elsewhere.Take(3).Select(o => o.Name)) + (elsewhere.Count > 3 ? "..." : "") + "), which this level never loads.";
            return "No soundbank carries a sound event called " + eventName + " (a soundbank of the level's own is searched too).";
        }

        /// <summary>
        /// The Stop event for a Play event that has no Stop action of its own: Stop_X for Play_X (the game's own naming), when a soundbank
        /// carries one. Null otherwise.
        /// </summary>
        private static string StopEventFor(string eventName, SoundEventTuning.EventState state, SoundBankIndex index)
        {
            if (state != null && state.Stops.Any())
                return null;
            if (!eventName.StartsWith("Play_", StringComparison.OrdinalIgnoreCase))
                return null;
            string stop = "Stop_" + eventName.Substring(5);
            return index.Has(CathodeLib.Utilities.SoundHashedString(stop)) ? stop : null;
        }

        /// <summary>The other events (by name, where the level's sound data names them) whose Play actions play what this one plays.</summary>
        private static List<string> AlsoPlaying(uint eventId, SoundEventTuning.EventState state, SoundBankIndex index)
        {
            List<string> others = new List<string>();
            if (state == null)
                return others;
            Dictionary<uint, string> names = null;
            foreach (SoundEventTuning.TargetState target in state.Targets)
                foreach (uint other in index.EventsPlaying(target.Id))
                {
                    if (other == eventId)
                        continue;
                    if (names == null)
                        names = EventNames();
                    string name;
                    name = names.TryGetValue(other, out name) ? name : "event " + other;
                    if (!others.Contains(name))
                        others.Add(name);
                }
            return others;
        }

        /// <summary>
        /// Notes where what an event plays is also held in banks the level's copies don't cover: a permanent soundbank (whose copy the
        /// game keeps, so volume, pitch, loop and reach don't apply), or a bank not carrying the event (whose copy wins if loaded first).
        /// </summary>
        private static List<string> SharingNotes(string eventName, SoundEventTuning.EventState state, List<BankCopy> copies, SoundBankIndex index, string package)
        {
            List<string> notes = new List<string>();
            if (state == null)
                return notes;
            HashSet<string> tuned = new HashSet<string>(copies.Select(o => o.Name), StringComparer.OrdinalIgnoreCase);
            HashSet<string> permanent = new HashSet<string>(StringComparer.OrdinalIgnoreCase), other = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SoundEventTuning.TargetState target in state.Targets)
                foreach (SoundBankIndex.BankFile bank in index.BanksWith(target.Id))
                {
                    if (bank.Permanent)
                        permanent.Add(Path.GetFileName(bank.File));
                    else if (!tuned.Contains(Path.GetFileName(bank.File)) && !IsOtherLevelsDynamicBank(bank.File, package))
                        other.Add(Path.GetFileName(bank.File));
                }
            if (permanent.Count != 0)
                notes.Add("What " + eventName + " plays is also in " + string.Join(", ", permanent) + " (permanent: loaded before the level, and the game keeps the first copy) - so its volume, pitch, loop and reach play as shipped everywhere; its delays, fades and stops are the event's own and do change.");
            if (other.Count != 0)
                notes.Add("What " + eventName + " plays is also in " + string.Join(", ", other.Take(4)) + (other.Count > 4 ? "..." : "") + ", which doesn't carry the event: if the level loads " + (other.Count == 1 ? "that soundbank" : "one of those") + " before this event's, its copy's volume, pitch, loop and reach are the ones heard.");
            return notes;
        }

        /// <summary>Every sound event the open level's sound data names, by id.</summary>
        private static Dictionary<uint, string> EventNames()
        {
            Dictionary<uint, string> names = new Dictionary<uint, string>();
            foreach (string name in SoundEventMetadata.AllEvents())
                names[CathodeLib.Utilities.SoundHashedString(name)] = name;
            return names;
        }

        /// <summary>Notes for replaced audio that several events play: on this level all of them play the new audio.</summary>
        private static List<string> SharedAudioNotes(WwiseSoundLibrary library, IEnumerable<uint> sourceIds)
        {
            List<string> notes = new List<string>();
            try
            {
                foreach (uint id in sourceIds)
                {
                    List<string> events = SoundNames.Playing(library, id).Select(o => o.Event).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    if (events.Count > 1)
                        notes.Add("Several events play this audio (source " + id + "): " + string.Join(", ", events.Take(6)) + (events.Count > 6 ? " and " + (events.Count - 6) + " more" : "")
                            + ". They are one piece of audio, so on this level all of them play the new sound.");
                }
            }
            catch (Exception e)
            {
                Debug.Log("Sound", "Couldn't list the events sharing replaced audio: " + e.Message);
            }
            return notes;
        }

        #endregion
    }
}
