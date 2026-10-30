using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// A level's own sound package: WORLD/LEVEL_SOUND_OVERRIDE.PCK.
    ///
    /// The game mounts it as the level loads, ahead of the shipped audio, so a soundbank or a stream in it with the id of
    /// one in DATA/SOUND is read from here instead - for that level alone. The challenge maps carry changed copies of base
    /// banks (Android), banks of their own and replacement streams this way; the soundbank on disk named for a challenge
    /// map is an empty stub its package stands in for. Any level can have one: the game makes the path from the level's
    /// folder rather than from a list.
    ///
    /// Replacing a sound for one level puts the new audio here and leaves DATA/SOUND alone. Streamed audio goes in as a
    /// stream of the same id. Audio held inside a soundbank needs the whole bank, so the bank is copied in from the base
    /// game - once: a bank already in the package is changed where it is - with the audio swapped inside it. Voiced
    /// audio is left out: the game looks it up under the voice language, and a package carrying any has to list it
    /// for every language.
    /// </summary>
    internal static class LevelSoundOverride
    {
        public const string FileName = "LEVEL_SOUND_OVERRIDE.PCK";

        /// <summary>Raised on the UI thread after a level's package has been written, made or deleted.</summary>
        public static event Action Changed;

        /// <summary>The open level's package, whether or not it exists yet. Null when no level is open.</summary>
        public static string PathForOpenLevel()
        {
            try
            {
                LevelContent content = Singleton.Editor == null || Singleton.Editor.CompositeBrowser == null
                    ? null
                    : Singleton.Editor.CompositeBrowser.Content;

                if (content == null || content.Level == null || string.IsNullOrEmpty(content.Level.Filepath))
                    return null;

                string world = Path.GetFullPath(content.Level.Filepath + (content.Level.Patched ? "_PATCH" : "") + "/WORLD");
                return Path.Combine(world, NameIn(world));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The package's file name in a level's WORLD folder: the one already there, whatever its case - else in the case the
        /// level's own files are in. The Mac, Linux and iOS builds ship every file in lower case and run on file systems that
        /// can tell the difference, so a package made for one of them has to be called level_sound_override.pck to be found.
        /// </summary>
        private static string NameIn(string world)
        {
            try
            {
                if (Directory.Exists(world))
                {
                    string existing = Directory.EnumerateFiles(world, FileName).Select(Path.GetFileName).FirstOrDefault(o => string.Equals(o, FileName, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                        return existing;

                    //The level's files say which case it uses: the WORLD folder itself, as it is on disk
                    DirectoryInfo folder = new DirectoryInfo(world);
                    string actual = folder.Parent == null ? null : folder.Parent.EnumerateDirectories(folder.Name).Select(o => o.Name).FirstOrDefault();
                    if (actual != null && actual == actual.ToLowerInvariant())
                        return FileName.ToLowerInvariant();
                }
            }
            catch
            {
            }
            return FileName;
        }

        public static void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null)
                handler();
        }

        /// <summary>What a change to a level's package does - or would do, for a dry run - in words for the user.</summary>
        public sealed class Plan
        {
            public string Package;
            public bool Existed;
            public bool Deleted;
            public long SizeBefore;
            public long SizeAfter;

            /// <summary>Banks from DATA/SOUND copied into the package so the audio could be changed inside them.</summary>
            public List<string> BanksCopied = new List<string>();

            /// <summary>Banks already in the package that were changed.</summary>
            public List<string> BanksChanged = new List<string>();

            /// <summary>Banks taken back out, because they had become the same as the base game's again.</summary>
            public List<string> BanksRemoved = new List<string>();

            public int StreamsSet;
            public int StreamsRemoved;

            public List<string> Notes = new List<string>();
            public string Problem;

            /// <summary>The problem is that the audio is in a permanent soundbank, which no level can stand in for.</summary>
            public bool HeldPermanently;

            public bool Ok
            {
                get { return Problem == null; }
            }

            public bool ChangesAnything
            {
                get { return BanksCopied.Count + BanksChanged.Count + BanksRemoved.Count + StreamsSet + StreamsRemoved != 0; }
            }
        }

        #region REPLACING

        /// <summary>
        /// Put new audio for each source id into the level's package, making the package if there isn't one. With
        /// <paramref name="write"/> false nothing is written and the plan says what would be.
        /// </summary>
        public static Plan Replace(string package, WwiseSoundLibrary library, IDictionary<uint, byte[]> replacements, bool write)
        {
            Plan plan = new Plan { Package = package };
            WwiseFilePackageBuilder builder;
            try
            {
                builder = Open(package, plan);
            }
            catch (Exception e)
            {
                plan.Problem = "The level's sound package can't be read: " + e.Message;
                return plan;
            }

            Dictionary<string, byte[]> baseBanks = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            HashSet<WwiseFilePackageBuilder.Item> changedBanks = new HashSet<WwiseFilePackageBuilder.Item>();
            List<string> voiced = new List<string>();
            HashSet<string> otherLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<uint, byte[]> replacement in replacements)
            {
                uint id = replacement.Key;
                byte[] wem = replacement.Value;

                //What this audio has been written into so far: the package's own copy and the base game's copy of a bank are
                //often the same bank in the package, and are reached once each
                HashSet<WwiseFilePackageBuilder.Item> reached = new HashSet<WwiseFilePackageBuilder.Item>();

                string permanent = PermanentBankHolding(library, id);
                if (permanent != null)
                {
                    plan.Problem = "This sound is held in " + permanent + ", one of the soundbanks the game loads as it starts and keeps loaded "
                        + "(Init, UI and those in DATA\\LIST_OF_PERMANENT_SOUND_BANKS.TXT). That copy is in memory before any level's sound package is read, and the game "
                        + "plays it even where a level's own soundbank has the sound too - so this sound can only be replaced for every level.";
                    plan.HeldPermanently = true;
                    return plan;
                }

                try
                {
                    foreach (WwiseMediaLocation copy in library.AllCopies(id))
                    {
                        string file = Path.GetFullPath(copy.File);

                        //Already in the package: a stream of its own, or audio inside one of the package's banks
                        if (SameFile(file, package))
                        {
                            WwiseFilePackageBuilder.Item stream = builder.Streams.FirstOrDefault(o => o.SourceFile != null && o.SourceOffset == copy.Offset);
                            if (stream != null)
                            {
                                if (reached.Add(stream))
                                {
                                    stream.Data = wem;
                                    plan.StreamsSet++;
                                }
                                continue;
                            }

                            WwiseFilePackageBuilder.Item bank = builder.Banks.FirstOrDefault(o => o.SourceFile != null && copy.Offset >= o.SourceOffset && copy.Offset < o.SourceOffset + o.SourceLength);
                            if (bank != null && !reached.Contains(bank) && SwapInBank(bank, id, wem))
                            {
                                reached.Add(bank);
                                if (changedBanks.Add(bank))
                                    plan.BanksChanged.Add(BankName(bank, library));
                            }
                            continue;
                        }

                        if (!IsSharedSoundEffect(file, library))
                        {
                            voiced.Add(Path.GetFileName(file));
                            continue;
                        }

                        if (IsOtherLevelsDynamicBank(file, package))
                        {
                            otherLevels.Add(Path.GetFileName(file));
                            continue;
                        }

                        switch (Path.GetExtension(file).ToLowerInvariant())
                        {
                            case ".wem":
                            {
                                //Streamed: the package's stream of the same id is opened instead of the loose file
                                WwiseFilePackageBuilder.Item stream = builder.Set(builder.Streams, id, builder.SfxLanguage, wem);
                                if (reached.Add(stream))
                                    plan.StreamsSet++;
                                break;
                            }

                            case ".bnk":
                            {
                                byte[] baseBank;
                                if (!baseBanks.TryGetValue(file, out baseBank))
                                {
                                    baseBank = File.ReadAllBytes(file);
                                    baseBanks.Add(file, baseBank);
                                }

                                uint bankId = BankId(baseBank);
                                List<WwiseFilePackageBuilder.Item> inPackage = builder.Banks.Where(o => o.Id == bankId).ToList();
                                if (inPackage.Count != 0)
                                {
                                    //The level loads its own copy of this bank instead. If that copy carries the audio it is
                                    //changed there (unless it already has been); if it doesn't, the base game's copy never
                                    //plays on this level and there is nothing to do
                                    foreach (WwiseFilePackageBuilder.Item bank in inPackage)
                                    {
                                        if (reached.Contains(bank) || !SwapInBank(bank, id, wem))
                                            continue;

                                        reached.Add(bank);
                                        if (changedBanks.Add(bank))
                                            plan.BanksChanged.Add(BankName(bank, library));
                                    }
                                    break;
                                }

                                WwiseFilePackageBuilder.Item copied = builder.Set(builder.Banks, bankId, builder.SfxLanguage, baseBank);
                                SwapInBank(copied, id, wem);
                                reached.Add(copied);
                                changedBanks.Add(copied);
                                plan.BanksCopied.Add(Path.GetFileName(file) + " (" + Describe(baseBank.Length) + ")");
                                break;
                            }

                            default:
                                //A stream in another package: the dialogue, which is voiced
                                voiced.Add(Path.GetFileName(file));
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    plan.Problem = "Source " + id + " could not be put in the level's sound package: " + e.Message;
                    return plan;
                }

                if (reached.Count == 0)
                {
                    plan.Problem = voiced.Count != 0
                        ? "This is voiced audio (" + string.Join(", ", voiced.Distinct().Take(3)) + "), which the game looks up by voice language - it can only be replaced for every level."
                        : otherLevels.Count != 0
                        ? "This sound is only in other levels' DynamicLoad soundbanks (" + string.Join(", ", otherLevels.Take(3)) + (otherLevels.Count > 3 ? "..." : "") + "), which this level never loads."
                        : "Source " + id + " isn't in any soundbank or stream the game loads, so there is nothing to replace on this level.";
                    return plan;
                }
            }

            if (voiced.Count != 0)
                plan.Notes.Add("Left as it is, because it's voiced audio the game looks up by voice language: " + string.Join(", ", voiced.Distinct()) + ".");
            if (otherLevels.Count != 0)
                plan.Notes.Add("Left out: " + otherLevels.Count + " other levels' DynamicLoad soundbank" + (otherLevels.Count == 1 ? "" : "s")
                    + " holding this sound too (" + string.Join(", ", otherLevels.Take(3)) + (otherLevels.Count > 3 ? "..." : "") + ") - a level only ever loads its own.");

            return Finish(builder, plan, write);
        }

        /// <summary>Put new audio in a bank in the package, if the bank holds that audio. False when it doesn't.</summary>
        private static bool SwapInBank(WwiseFilePackageBuilder.Item bank, uint sourceId, byte[] wem)
        {
            byte[] bytes = bank.Read();
            if (!WwiseMediaInjector.MediaIn(bytes).Any(o => o.Key == sourceId))
                return false;

            bank.Data = WwiseMediaInjector.ReplaceInBank(bytes, sourceId, wem);
            return true;
        }

        #endregion

        #region REVERTING

        /// <summary>
        /// Let the level play the base game's audio for each source id again: streams of them come out of the package,
        /// and banks in it get the base game's audio back - and come out too once they are the same as the base game's
        /// bank. Audio only this level has (nothing in DATA/SOUND to go back to) stays as it is.
        /// </summary>
        public static Plan Revert(string package, WwiseSoundLibrary library, IEnumerable<uint> sourceIds, bool write)
        {
            Plan plan = new Plan { Package = package };
            if (!File.Exists(package))
            {
                plan.Problem = "This level has no sound package.";
                return plan;
            }

            WwiseFilePackageBuilder builder;
            try
            {
                builder = Open(package, plan);
            }
            catch (Exception e)
            {
                plan.Problem = "The level's sound package can't be read: " + e.Message;
                return plan;
            }

            Dictionary<WwiseFilePackageBuilder.Item, string> changedBanks = new Dictionary<WwiseFilePackageBuilder.Item, string>();
            Dictionary<string, byte[]> baseBanks = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            List<uint> levelOnly = new List<uint>();

            try
            {
                foreach (uint id in sourceIds.Distinct())
                {
                    //The base game's audio for a bank comes from the base game's copy of that same bank where it can - a
                    //prefetched sound holds only its opening there, not the whole stream - and from any other copy if not
                    WwiseMediaLocation anyOriginal = library.AllCopies(id).FirstOrDefault(o => !SameFile(Path.GetFullPath(o.File), package) && IsSharedSoundEffect(Path.GetFullPath(o.File), library));

                    plan.StreamsRemoved += builder.Streams.RemoveAll(o => o.Id == id);

                    foreach (WwiseFilePackageBuilder.Item bank in builder.Banks)
                    {
                        byte[] bytes = bank.Read();
                        if (!WwiseMediaInjector.MediaIn(bytes).Any(o => o.Key == id))
                            continue;

                        byte[] original = null;
                        string baseFile = BaseBankFile(bank.Id, library);
                        if (baseFile != null)
                        {
                            byte[] baseBank;
                            if (!baseBanks.TryGetValue(baseFile, out baseBank))
                            {
                                baseBank = File.ReadAllBytes(baseFile);
                                baseBanks.Add(baseFile, baseBank);
                            }

                            foreach (KeyValuePair<uint, KeyValuePair<long, int>> media in WwiseMediaInjector.MediaIn(baseBank))
                            {
                                if (media.Key != id)
                                    continue;

                                original = new byte[media.Value.Value];
                                Array.Copy(baseBank, media.Value.Key, original, 0, original.Length);
                                break;
                            }
                        }
                        if (original == null && anyOriginal != null)
                            original = WwiseSoundLibrary.ReadMedia(anyOriginal);

                        if (original == null)
                        {
                            if (!levelOnly.Contains(id))
                                levelOnly.Add(id);
                            continue;
                        }

                        bank.Data = WwiseMediaInjector.ReplaceInBank(bytes, id, original);
                        if (!changedBanks.ContainsKey(bank))
                            changedBanks.Add(bank, BankName(bank, library));
                    }
                }

                //A bank that is the base game's again byte for byte has no reason to be in the package
                foreach (KeyValuePair<WwiseFilePackageBuilder.Item, string> bank in changedBanks)
                {
                    string baseFile = BaseBankFile(bank.Key.Id, library);
                    byte[] baseBank;
                    if (baseFile != null && !baseBanks.TryGetValue(baseFile, out baseBank))
                        baseBanks[baseFile] = baseBank = File.ReadAllBytes(baseFile);
                    if (baseFile != null && bank.Key.Data.SequenceEqual(baseBanks[baseFile]))
                    {
                        builder.Banks.Remove(bank.Key);
                        plan.BanksRemoved.Add(bank.Value);
                    }
                    else
                        plan.BanksChanged.Add(bank.Value);
                }
            }
            catch (Exception e)
            {
                plan.Problem = "The level's sound package could not be changed: " + e.Message;
                return plan;
            }

            if (levelOnly.Count != 0)
                plan.Notes.Add("Kept: " + string.Join(", ", levelOnly) + " - the base game has no audio of its own to go back to, only this level's.");
            if (!plan.ChangesAnything && plan.Problem == null)
                plan.Problem = levelOnly.Count != 0
                    ? "This sound only exists in this level's sound package - the base game has no audio for it to go back to."
                    : "This level's sound package doesn't change this sound.";
            if (!plan.Ok)
                return plan;

            return Finish(builder, plan, write);
        }

        #endregion

        #region WHOLE FILES

        /// <summary>
        /// Put a soundbank (.bnk) or a stream (.wem, named for its id) into the package as it is, in place of anything with
        /// the same id - a bank built with the Wwise authoring tool, or audio exported from another level's package.
        /// </summary>
        public static Plan Add(string package, string file, bool write)
        {
            Plan plan = new Plan { Package = package };
            WwiseFilePackageBuilder builder;
            try
            {
                builder = Open(package, plan);
                byte[] bytes = File.ReadAllBytes(file);

                if (string.Equals(Path.GetExtension(file), ".bnk", StringComparison.OrdinalIgnoreCase))
                {
                    if (bytes.Length < 16 || Encoding.ASCII.GetString(bytes, 0, 4) != "BKHD")
                    {
                        plan.Problem = Path.GetFileName(file) + " isn't a Wwise soundbank.";
                        return plan;
                    }

                    uint version = BitConverter.ToUInt32(bytes, 8);
                    uint expected = ExpectedBankVersion();
                    if (expected != 0 && version != expected)
                    {
                        plan.Problem = Path.GetFileName(file) + " is a version " + version + " soundbank; this game reads version " + expected + ".";
                        return plan;
                    }

                    uint id = BankId(bytes);
                    if (IsPermanent(id, SoundPreviewLibrary.SoundDirectory))
                    {
                        plan.Problem = Path.GetFileName(file) + " is one of the soundbanks the game loads as it starts and keeps loaded "
                            + "(Init, UI and those in DATA\\LIST_OF_PERMANENT_SOUND_BANKS.TXT), so a level's copy of it would never be read.";
                        return plan;
                    }

                    bool replacing = builder.Banks.Any(o => o.Id == id && o.LanguageId == builder.SfxLanguage);
                    builder.Set(builder.Banks, id, builder.SfxLanguage, bytes);
                    (replacing ? plan.BanksChanged : plan.BanksCopied).Add(Path.GetFileName(file));
                }
                else
                {
                    uint id;
                    if (!uint.TryParse(Path.GetFileNameWithoutExtension(file), out id))
                    {
                        plan.Problem = "A stream is found by its id, so the .wem has to be named for it - e.g. 123456789.wem.";
                        return plan;
                    }
                    if (bytes.Length < 12 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF")
                    {
                        plan.Problem = Path.GetFileName(file) + " isn't a Wwise audio file.";
                        return plan;
                    }

                    builder.Set(builder.Streams, id, builder.SfxLanguage, bytes);
                    plan.StreamsSet++;
                }
            }
            catch (Exception e)
            {
                plan.Problem = e.Message;
                return plan;
            }

            return Finish(builder, plan, write);
        }

        /// <summary>
        /// Put audio into the package as a stream with this id, in place of any stream of that id - audio made here from a .wav
        /// for a soundbank of the user's own to stream, or a stream of the base game's replaced whole.
        /// </summary>
        public static Plan AddStream(string package, uint id, byte[] wem, bool write)
        {
            Plan plan = new Plan { Package = package };
            WwiseFilePackageBuilder builder;
            try
            {
                if (wem == null || wem.Length < 12 || Encoding.ASCII.GetString(wem, 0, 4) != "RIFF")
                {
                    plan.Problem = "That isn't Wwise audio.";
                    return plan;
                }

                builder = Open(package, plan);
                builder.Set(builder.Streams, id, builder.SfxLanguage, wem);
                plan.StreamsSet++;
            }
            catch (Exception e)
            {
                plan.Problem = e.Message;
                return plan;
            }

            return Finish(builder, plan, write);
        }

        /// <summary>Take files out of the package by table and id (every language of each), deleting it if nothing is left.</summary>
        public static Plan Remove(string package, IEnumerable<KeyValuePair<bool, uint>> files, bool write)
        {
            Plan plan = new Plan { Package = package };
            WwiseFilePackageBuilder builder;
            try
            {
                builder = Open(package, plan);
                foreach (KeyValuePair<bool, uint> file in files)
                {
                    if (file.Key)
                    {
                        int removed = builder.Banks.RemoveAll(o => o.Id == file.Value);
                        for (int i = 0; i < removed; i++)
                            plan.BanksRemoved.Add(file.Value.ToString());
                    }
                    else
                        plan.StreamsRemoved += builder.Streams.RemoveAll(o => o.Id == file.Value);
                }
            }
            catch (Exception e)
            {
                plan.Problem = e.Message;
                return plan;
            }

            Plan finished = Finish(builder, plan, write);

            //A soundbank of the level's own (none in DATA/SOUND) takes its events out of the level's sound event table with it
            if (write && finished.Ok)
            {
                List<uint> ownBanks = files.Where(o => o.Key && BaseBankFile(o.Value, (WwiseSoundLibrary)null) == null).Select(o => o.Value).ToList();
                if (ownBanks.Count != 0)
                {
                    string note = SetLevelEvents(package, ownBanks.ToDictionary(o => o, o => (IList<WwiseBankBuilder.EventInfo>)new WwiseBankBuilder.EventInfo[0]), null);
                    if (note != null)
                        finished.Notes.Add(note);
                }
            }
            return finished;
        }

        #endregion

        #region SOUNDBANKS OF THE LEVEL'S OWN

        /// <summary>
        /// Put a soundbank built here (<see cref="WwiseBankBuilder"/>) in the package, in place of any of the same name, and list its
        /// events in the level's sound event table - which is where the game finds an event a script names (SOUND_ENTITY::on_load
        /// looks the name up there and plays nothing without it). The table and the level's soundbank list are written straight
        /// away, as the package is. Only for the open level's package: the tables are the open level's.
        /// </summary>
        public static Plan SaveBank(string package, string bankName, WwiseBankBuilder.Result built, bool write)
        {
            Plan plan = new Plan { Package = package };
            WwiseFilePackageBuilder builder;
            try
            {
                if (!SameFile(Path.GetFullPath(package), PathForOpenLevel() ?? ""))
                {
                    plan.Problem = "A soundbank of the level's own can only be made for the open level: its events go in the level's sound event table.";
                    return plan;
                }
                if (IsPermanent(built.BankId, SoundPreviewLibrary.SoundDirectory) || BaseBankFile(built.BankId, (WwiseSoundLibrary)null) != null)
                {
                    plan.Problem = "The game already has a soundbank called " + bankName + ": give the new one a name of its own.";
                    return plan;
                }

                builder = Open(package, plan);
                bool replacing = builder.Banks.Any(o => o.Id == built.BankId);
                builder.Set(builder.Banks, built.BankId, builder.SfxLanguage, built.Bank);
                (replacing ? plan.BanksChanged : plan.BanksCopied).Add(bankName);
            }
            catch (Exception e)
            {
                plan.Problem = e.Message;
                return plan;
            }

            Finish(builder, plan, write);
            plan.Notes.RemoveAll(o => o.StartsWith("Copied into the level's package", StringComparison.Ordinal));
            plan.Notes.Insert(0, "The soundbank " + bankName + " (" + Describe(built.Bank.Length) + ") goes in the level's sound package, with " + built.Events.Count + " event" + (built.Events.Count == 1 ? "" : "s")
                + ". Its events go in the level's sound event table (WORLD\\SOUNDEVENTDATA.DAT) - scripts can play them once a SoundLoadBank loads the bank.");
            if (!write || !plan.Ok)
                return plan;

            string note = SetLevelEvents(package, new Dictionary<uint, IList<WwiseBankBuilder.EventInfo>> { { built.BankId, built.Events } }, bankName);
            if (note != null)
            {
                plan.Problem = note;
                return plan;
            }
            return plan;
        }

        /// <summary>
        /// The open level's sound event table (and its soundbank list) made to list exactly these events for these banks - none,
        /// to take a bank's out - then written. Null when it went through, else what went wrong. Everything that lists sound events
        /// (the pickers, the Sound Editor, its Soundbanks column) is told to read them again.
        /// </summary>
        private static string SetLevelEvents(string package, IDictionary<uint, IList<WwiseBankBuilder.EventInfo>> banks, string addBankName)
        {
            try
            {
                LevelContent content = Singleton.Editor == null || Singleton.Editor.CompositeBrowser == null ? null : Singleton.Editor.CompositeBrowser.Content;
                if (content == null || content.Level == null || content.Level.SoundEventData == null || !SameFile(Path.GetFullPath(package), PathForOpenLevel() ?? ""))
                    return "The level's sound event table wasn't changed: the level isn't open.";

                CATHODE.SoundEventData events = content.Level.SoundEventData;
                CATHODE.SoundBankData bankList = content.Level.SoundBankData;
                foreach (KeyValuePair<uint, IList<WwiseBankBuilder.EventInfo>> bank in banks)
                {
                    events.Entries.RemoveAll(o => o.id == bank.Key);
                    List<WwiseBankBuilder.EventInfo> named = bank.Value.Where(o => !string.IsNullOrEmpty(o.Name)).ToList();
                    if (named.Count != 0)
                    {
                        CATHODE.SoundEventData.Soundbank entry = new CATHODE.SoundEventData.Soundbank { id = bank.Key };
                        foreach (WwiseBankBuilder.EventInfo e in named)
                        {
                            entry.events.Add(new CATHODE.SoundEventData.Soundbank.Event { name = e.Name, metadata = "", max_attenuation = e.MaxAttenuation });
                            CATHODE.Scripting.ShortGuidUtils.Generate(e.Name);
                        }
                        events.Entries.Add(entry);
                    }
                    if (bankList != null)
                    {
                        bankList.Entries.RemoveAll(o => !string.IsNullOrEmpty(o.Name) && CathodeLib.Utilities.SoundHashedString(o.Name) == bank.Key);
                        if (named.Count != 0 && addBankName != null)
                            bankList.Entries.Add(new CATHODE.SoundBankData.SoundBank { Name = addBankName, Localised = false });
                    }
                }

                Modding.ModServices.CaptureBeforeWrite(events.Filepath);
                events.Save();
                if (bankList != null)
                {
                    Modding.ModServices.CaptureBeforeWrite(bankList.Filepath);
                    bankList.Save();
                }
            }
            catch (Exception e)
            {
                return "The level's sound event table could not be written: " + e.Message;
            }
            finally
            {
                SoundEventMetadata.Invalidate();
                EnumStringListViewItems.RefreshSoundEntries();
            }
            return null;
        }

        /// <summary>
        /// The events of a soundbank of the level's own, by id, with their names - as the open level's sound event table lists
        /// them (a bank holds only their hashes).
        /// </summary>
        public static Dictionary<uint, string> LevelEventNames(uint bankId)
        {
            Dictionary<uint, string> names = new Dictionary<uint, string>();
            LevelContent content = Singleton.Editor == null || Singleton.Editor.CompositeBrowser == null ? null : Singleton.Editor.CompositeBrowser.Content;
            if (content == null || content.Level == null || content.Level.SoundEventData == null)
                return names;
            foreach (CATHODE.SoundEventData.Soundbank bank in content.Level.SoundEventData.Entries.Where(o => o.id == bankId))
                foreach (CATHODE.SoundEventData.Soundbank.Event e in bank.events)
                    names[CathodeLib.Utilities.SoundHashedString(e.name)] = e.name;
            return names;
        }

        #endregion

        #region SHARED

        private static WwiseFilePackageBuilder Open(string package, Plan plan)
        {
            plan.Existed = File.Exists(package);
            WwiseFilePackageBuilder builder = plan.Existed ? WwiseFilePackageBuilder.Load(package) : WwiseFilePackageBuilder.Create(SoundPreviewLibrary.NarrowPackageNames);
            plan.SizeBefore = plan.Existed ? new FileInfo(package).Length : 0;
            return builder;
        }

        /// <summary>Write the package (or delete it, once there is nothing left in it), and say what it came to.</summary>
        private static Plan Finish(WwiseFilePackageBuilder builder, Plan plan, bool write)
        {
            plan.Deleted = builder.IsEmpty;
            plan.SizeAfter = builder.IsEmpty ? 0 : builder.Size;

            if (!plan.Existed && !builder.IsEmpty)
                plan.Notes.Add("The level has no sound package yet, so one is made: WORLD\\" + FileName + ".");
            if (plan.BanksCopied.Count != 0)
                plan.Notes.Add("Copied into the level's package with the sound changed inside: " + string.Join(", ", plan.BanksCopied) + ". On this level the game loads "
                    + (plan.BanksCopied.Count == 1 ? "that copy" : "those copies") + " instead of DATA\\SOUND's.");
            if (plan.BanksChanged.Count != 0)
                plan.Notes.Add("Changed where they are in the level's package: " + string.Join(", ", plan.BanksChanged.Distinct()) + ".");
            if (plan.BanksRemoved.Count != 0)
                plan.Notes.Add("Taken out of the level's package: " + string.Join(", ", plan.BanksRemoved.Distinct()) + ".");
            if (plan.StreamsSet != 0)
                plan.Notes.Add(plan.StreamsSet == 1 ? "The streamed copy goes in as a stream of its own." : plan.StreamsSet + " streamed copies go in as streams of their own.");
            if (plan.StreamsRemoved != 0)
                plan.Notes.Add((plan.StreamsRemoved == 1 ? "A stream is" : plan.StreamsRemoved + " streams are") + " taken out of the level's package.");
            if (plan.Deleted && plan.Existed)
                plan.Notes.Add("Nothing is left in the level's package, so it is deleted.");
            else if (!plan.Deleted)
                plan.Notes.Add("The package comes to " + Describe(plan.SizeAfter) + (plan.Existed ? " (it was " + Describe(plan.SizeBefore) + ")" : "") + ".");

            if (!write)
                return plan;

            Directory.CreateDirectory(Path.GetDirectoryName(plan.Package));
            Modding.ModServices.CaptureBeforeWrite(plan.Package);
            try
            {
                if (builder.IsEmpty)
                {
                    if (File.Exists(plan.Package))
                        File.Delete(plan.Package);
                }
                else
                    builder.Save(plan.Package);
            }
            catch (IOException e)
            {
                plan.Problem = "The level's sound package could not be written (" + e.Message + "). If the game is running this level, close it first - it holds the package open.";
            }
            catch (UnauthorizedAccessException e)
            {
                plan.Problem = "The level's sound package could not be written: " + e.Message;
            }

            //Its audio has moved, so the index is built again on the next ask
            SoundPreviewLibrary.Invalidate();
            return plan;
        }

        /// <summary>
        /// Whether a soundbank is another level's DynamicLoad bank. As it opens a level the game loads one DynamicLoad soundbank,
        /// the one named for the level's folder (DynamicLoad_TECH_HUB, DynamicLoad_ChallengeMap1), so every other is never read
        /// there - the same sound is in dozens of them, and copying them all would only make the package bigger.
        /// </summary>
        private static bool IsOtherLevelsDynamicBank(string file, string package)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (!name.StartsWith("DynamicLoad_", StringComparison.OrdinalIgnoreCase))
                return false;

            //<level>[_PATCH]\WORLD\LEVEL_SOUND_OVERRIDE.PCK
            DirectoryInfo world = new FileInfo(package).Directory;
            if (world == null || world.Parent == null)
                return false;

            string level = world.Parent.Name;
            if (level.EndsWith("_PATCH", StringComparison.OrdinalIgnoreCase))
                level = level.Substring(0, level.Length - "_PATCH".Length);

            return !string.Equals(name, "DynamicLoad_" + level, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Audio every level shares and nobody voices: a loose bank or stream straight in the SOUND folder.</summary>
        private static bool IsSharedSoundEffect(string file, WwiseSoundLibrary library)
        {
            try
            {
                return SameFile(Path.GetFullPath(Path.GetDirectoryName(file)), Path.GetFullPath(library.SoundDirectory))
                    && !string.Equals(Path.GetExtension(file), ".pck", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The permanent soundbank holding a copy of this audio (its file name), or null when none does. The game loads those banks
        /// as it starts and keeps them for as long as it runs, so they are in memory before any level's package is mounted - and
        /// where the same audio is in two loaded banks, the game plays the copy it loaded first.
        /// </summary>
        public static string PermanentBankHolding(WwiseSoundLibrary library, uint sourceId)
        {
            HashSet<uint> permanent = PermanentBanks(library.SoundDirectory);
            if (permanent.Count == 0)
                return null;

            foreach (WwiseMediaLocation copy in library.AllCopies(sourceId))
            {
                string file = Path.GetFullPath(copy.File);
                if (!string.Equals(Path.GetExtension(file), ".bnk", StringComparison.OrdinalIgnoreCase) || !IsSharedSoundEffect(file, library))
                    continue;

                try
                {
                    byte[] header = new byte[16];
                    using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        if (stream.Read(header, 0, 16) == 16 && permanent.Contains(BankId(header)))
                            return Path.GetFileName(file);
                }
                catch
                {
                }
            }

            return null;
        }

        /// <summary>Whether a soundbank is one the game loads as it starts and keeps loaded, by its id.</summary>
        public static bool IsPermanent(uint bankId, string soundDirectory)
        {
            return PermanentBanks(soundDirectory).Contains(bankId);
        }

        /// <summary>The permanent soundbanks' files in DATA/SOUND (full paths), for telling at a glance which audio no level can replace.</summary>
        public static HashSet<string> PermanentBankFiles(WwiseSoundLibrary library)
        {
            HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (uint id in PermanentBanks(library.SoundDirectory))
            {
                string file = BaseBankFile(id, library);
                if (file != null)
                    files.Add(Path.GetFullPath(file));
            }
            return files;
        }

        /// <summary>
        /// The ids of the soundbanks the game loads as it starts and keeps (a bank's id is its name hashed, as Wwise hashes
        /// every name): Init and UI, which it loads by name as it opens, then those named in DATA/LIST_OF_PERMANENT_SOUND_BANKS.TXT.
        /// The list is read every time: the Permanent Soundbanks editor changes it. Measured in game: with a level's package
        /// holding changed copies of UI.BNK and WEAPONS.BNK, the base game's copies were the ones in memory.
        /// </summary>
        private static HashSet<uint> PermanentBanks(string soundDirectory)
        {
            HashSet<uint> ids = new HashSet<uint>
            {
                CathodeLib.Utilities.SoundHashedString("Init"),
                CathodeLib.Utilities.SoundHashedString("UI"),
            };
            try
            {
                if (string.IsNullOrEmpty(soundDirectory))
                    return ids;

                string list = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(soundDirectory).TrimEnd('\\', '/')), "LIST_OF_PERMANENT_SOUND_BANKS.TXT");
                if (File.Exists(list))
                    foreach (string line in File.ReadAllLines(list))
                        if (!string.IsNullOrWhiteSpace(line))
                            ids.Add(CathodeLib.Utilities.SoundHashedString(line.Trim()));
            }
            catch
            {
            }

            return ids;
        }

        /// <summary>A soundbank's id, from its header - what the game looks it up in a package by.</summary>
        public static uint BankId(byte[] bank)
        {
            if (bank.Length < 16 || Encoding.ASCII.GetString(bank, 0, 4) != "BKHD")
                throw new InvalidDataException("That isn't a Wwise soundbank.");

            return BitConverter.ToUInt32(bank, 12);
        }

        /// <summary>The version of the game's own soundbanks - 88 on PC, Mac and Linux, 134 on iOS and Switch - or 0 if none could be read.</summary>
        private static uint ExpectedBankVersion()
        {
            return SoundPreviewLibrary.BankVersion;
        }

        private static readonly object _baseBanksLock = new object();
        private static Dictionary<uint, string> _baseBanks;
        private static string _baseBanksFor;

        /// <summary>The base game's soundbank file with this id, or null when DATA/SOUND has none.</summary>
        public static string BaseBankFile(uint id, WwiseSoundLibrary library)
        {
            string directory = library == null ? SoundPreviewLibrary.SoundDirectory : library.SoundDirectory;
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return null;

            lock (_baseBanksLock)
                return BaseBankFile(id, directory);
        }

        private static string BaseBankFile(uint id, string directory)
        {
            //Read once per game folder: the ids are in each bank's first sixteen bytes, so this is quick, but it is asked a lot
            if (_baseBanks == null || !string.Equals(_baseBanksFor, directory, StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<uint, string> banks = new Dictionary<uint, string>();
                byte[] header = new byte[16];
                foreach (string file in Directory.EnumerateFiles(directory, "*.bnk"))
                {
                    try
                    {
                        using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                            if (stream.Read(header, 0, 16) == 16 && Encoding.ASCII.GetString(header, 0, 4) == "BKHD")
                                banks[BitConverter.ToUInt32(header, 12)] = file;
                    }
                    catch
                    {
                    }
                }

                _baseBanks = banks;
                _baseBanksFor = directory;
            }

            string found;
            return _baseBanks.TryGetValue(id, out found) ? found : null;
        }

        /// <summary>A bank in the package by name: the base game's file it stands in for, else the name the bank gives itself.</summary>
        public static string BankName(WwiseFilePackageBuilder.Item bank, WwiseSoundLibrary library)
        {
            string baseFile = BaseBankFile(bank.Id, library);
            if (baseFile != null)
                return Path.GetFileName(baseFile);

            try
            {
                string name;
                if (WwiseSoundBank.Load(new MemoryStream(bank.Read()), 0, bank.Length, "", "").BankNames.TryGetValue(bank.Id, out name))
                    return name;
            }
            catch
            {
            }

            return "soundbank " + bank.Id;
        }

        private static bool SameFile(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public static string Describe(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            if (bytes >= 1024)
                return (bytes / 1024.0).ToString("0") + " KB";
            return bytes + " bytes";
        }

        #endregion
    }
}
