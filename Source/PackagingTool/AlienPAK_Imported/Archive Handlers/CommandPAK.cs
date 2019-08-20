using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    /*
     *
     * Commands PAK handler.
     * This is a compiled script archive, and more work needs to be done to understand what each "block" contains.
     * Once this is known, more reliable exporting can happen, and maybe eventually importing also.
     * Each block seems to be common info (e.g. strings) from every script.
     * We can currently export the compiled scripts with this, but they're not much use.
     * It seems to build up to a custom binary format which represents the CATHODE node system.
     * 
    */
    class CommandPAK : AnyPAK
    {
        List<EntryCommandsPAK> CommandsEntries = new List<EntryCommandsPAK>();

        /* Initialise the CommandsPAK class with the intended location (existing or not) */
        public CommandPAK(string PathToPAK)
        {
            FilePathPAK = PathToPAK;
        }

        /* Load the contents of an existing CommandsPAK */
        public override PAKReturnType Load()
        {
            if (!File.Exists(FilePathPAK))
            {
                return PAKReturnType.FAIL_TRIED_TO_LOAD_VIRTUAL_ARCHIVE;
            }

            try
            {
                //Open PAK
                BinaryReader ArchiveFile = new BinaryReader(File.OpenRead(FilePathPAK));
                List<byte[]> CommandsHeaderMagics = new List<byte[]>();
                ExtraBinaryUtils BinaryUtils = new ExtraBinaryUtils();

                /* **************************** */
                /* *********  Header  ********* */

                ArchiveFile.BaseStream.Position = 28; //Skip header

                /* **************************** */
                /* ********* "Blocks" ********* */
                CommandsHeaderMagics.Add(new byte[] { 0xDA, 0x6B, 0xD7, 0x02 });
                CommandsHeaderMagics.Add(new byte[] { 0x84, 0x11, 0xCD, 0x38 });
                CommandsHeaderMagics.Add(new byte[] { 0x5E, 0x8E, 0x8E, 0x5A });
                CommandsHeaderMagics.Add(new byte[] { 0xBF, 0xA7, 0x62, 0x8C });
                CommandsHeaderMagics.Add(new byte[] { 0xF6, 0xAF, 0x08, 0x93 });
                CommandsHeaderMagics.Add(new byte[] { 0xF0, 0x0B, 0x76, 0x96 });
                CommandsHeaderMagics.Add(new byte[] { 0x38, 0x43, 0xFF, 0xBF });
                CommandsHeaderMagics.Add(new byte[] { 0x87, 0xC1, 0x25, 0xE7 });
                CommandsHeaderMagics.Add(new byte[] { 0xDC, 0x72, 0x74, 0xFD });

                //Block one
                ParseGenericCommandsPakBlock(ArchiveFile, CommandsHeaderMagics[0], CommandsHeaderMagics[1]);

                //String block
                List<string> ScriptStringDump = new List<string>();
                string ThisStringEntry = "";
                bool DidJustSubmit = false;
                for (int i = 0; i < 99999999; i++)
                {
                    //Read a block of four
                    byte[] ThisSegment = ArchiveFile.ReadBytes(4);

                    //Check for header magics, and act accordingly
                    if (ThisSegment.SequenceEqual(CommandsHeaderMagics[1]) || ThisSegment.SequenceEqual(CommandsHeaderMagics[2]))
                    {
                        //Only submit if the string has content
                        ScriptStringDump.Add(ThisStringEntry);
                        ThisStringEntry = "";

                        //If we're still in the current list of strings, continue - else break
                        if (ThisSegment.SequenceEqual(CommandsHeaderMagics[2]))
                        {
                            ArchiveFile.BaseStream.Position -= 4;
                            break;
                        }
                        DidJustSubmit = true;
                        continue;
                    }

                    //We get eight bytes of unknown for each string - skip them for now 
                    if (DidJustSubmit)
                    {
                        ArchiveFile.BaseStream.Position += 4;
                        DidJustSubmit = false;
                        continue;
                    }

                    //We're still inside the current string, add the chars on if they're not null
                    for (int x = 0; x < 4; x++)
                    {
                        if (ThisSegment[x] != 0x00)
                        {
                            ThisStringEntry += (char)ThisSegment[x];
                        }
                    }
                }

                //Blocks two through seven
                for (int i = 2; i < 8; i++)
                {
                    ParseGenericCommandsPakBlock(ArchiveFile, CommandsHeaderMagics[i], CommandsHeaderMagics[i + 1]);
                }

                //Block eight
                List<List<byte>> AllBlockEntries = new List<List<byte>>(); //All entries
                List<byte> ThisBlockEntry = new List<byte>(); //A single entry
                for (int i = 0; i < 99999999; i++)
                {
                    //Read a segment of four bytes
                    byte[] ThisSegment = ArchiveFile.ReadBytes(4);

                    //Each block here is 8 bytes known - so if the header matches, read the following four into our list
                    if (ThisSegment.SequenceEqual(CommandsHeaderMagics[8]))
                    {
                        byte[] ThisSegmentCont = ArchiveFile.ReadBytes(4);
                        for (int x = 0; x < 4; x++)
                        {
                            ThisBlockEntry.Add(ThisSegmentCont[x]);
                        }
                        AllBlockEntries.Add(ThisBlockEntry);
                        ThisBlockEntry.Clear();
                        continue;
                    }

                    //We didn't match the header, so we must be at the scripts
                    ArchiveFile.BaseStream.Position -= 4;
                    break;
                }
            
                /* ***************************** */
                /* ********* "Scripts" ********* */
                CommandsHeaderMagics.Add(new byte[] { 0x07, 0x00, 0x00, 0x00 }); //Not really a header, but used as such here.
                CommandsHeaderMagics.Add(new byte[] { 0x0E, 0x00, 0x00, 0x00 }); //Not really a header, but used as such here.

                //Parse each script entry
                EntryCommandsPAK NewScriptEntry;
                for (int i = 0; i < 99999999; i++)
                {
                    byte[] ThisSegment = ArchiveFile.ReadBytes(4);

                    //We reached the garbage block
                    if (ThisSegment.SequenceEqual(CommandsHeaderMagics[9]))
                    {
                        ArchiveFile.BaseStream.Position -= 4;
                        break;
                    }

                    //We didn't reach the garbage block - make a new entry and assign the bytes (ID)
                    NewScriptEntry = new EntryCommandsPAK();
                    NewScriptEntry.ScriptID = ThisSegment;

                    //Read-in script string
                    bool should_stop = false;
                    for (int x = 0; x < 99999999; x++)
                    {
                        byte[] ThisSegmentCont = ArchiveFile.ReadBytes(4);
                        for (int y = 0; y < 4; y++)
                        {
                            if (ThisSegmentCont[y] == 0x00)
                            {
                                should_stop = true;
                                break;
                            }
                            NewScriptEntry.ScriptName += (char)ThisSegmentCont[y];
                        }
                        if (should_stop) { break; }
                    }

                    //Get the script's magic (used to denote the start/end of the script)
                    NewScriptEntry.ScriptMarker = ArchiveFile.ReadBytes(4);
                    for (int x = 0; x < 4; x++) { NewScriptEntry.ScriptContent.Add(NewScriptEntry.ScriptMarker[x]); }

                    //Capture the script until we hit the end magic
                    bool TriggeredReset = false;
                ScriptLoopStart:
                    for (int x = 0; x < 99999999; x++)
                    {
                        byte[] ThisSegmentCont = ArchiveFile.ReadBytes(4);

                        //We've reached the end
                        if (!TriggeredReset && ThisSegmentCont.SequenceEqual(NewScriptEntry.ScriptMarker))
                        {
                            break;
                        }
                        TriggeredReset = false;

                        //We haven't reached the end, keep reading the script
                        for (int y = 0; y < 4; y++)
                        {
                            NewScriptEntry.ScriptContent.Add(ThisSegmentCont[y]);
                        }
                    }

                    //Parse the numbers at the bottom (?!?)
                    for (int x = 0; x < 24; x++)
                    {
                        NewScriptEntry.ScriptTrailingInts[x] = ArchiveFile.ReadInt32();
                    }

                    //Verify we should've exited when we did (this is a bug from the odd formatting of the PAK's script entry/exit points)
                    int SanityDiff = NewScriptEntry.ScriptTrailingInts[2] - NewScriptEntry.ScriptTrailingInts[0];
                    if (!(SanityDiff < (NewScriptEntry.ScriptTrailingInts[2].ToString().Length * 10000) && SanityDiff >= 0)) // This is a magic number that seems to work: a better solution is required really.
                    {
                        ArchiveFile.BaseStream.Position -= 100;
                        TriggeredReset = true;
                        goto ScriptLoopStart;
                    }

                    //Append the magic to the end
                    for (int x = 0; x < 4; x++) { NewScriptEntry.ScriptContent.Add(NewScriptEntry.ScriptMarker[x]); }

                    //Add to list
                    CommandsEntries.Add(NewScriptEntry);
                }
                
                /* ***************************** */
                /* ********** Garbage ********** */

                //Count up the "garbage" at the end - these numbers might actually be IDs for something
                try
                {
                    for (int i = 0; i < 999999999; i++)
                    {
                        int GarbageNumber = ArchiveFile.ReadInt32(); //do something with this
                    }
                }
                catch { }
                
                //Done!
                ArchiveFile.Close();
                return PAKReturnType.SUCCESS;
            }
            catch (IOException) { return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE; }
            catch (Exception) { return PAKReturnType.FAIL_UNKNOWN; }
        }

        /* Parse a generic CommandsPAK block of data */
        private List<List<byte>> ParseGenericCommandsPakBlock(BinaryReader ArchiveFile, byte[] ThisMagic, byte[] NextMagic)
        {
            List<List<byte>> AllBlockEntries = new List<List<byte>>(); //All entries
            List<byte> ThisBlockEntry = new List<byte>(); //A single entry

            for (int i = 0; i < 99999999; i++)
            {
                //Read a segment of four bytes
                byte[] ThisSegment = ArchiveFile.ReadBytes(4);

                //We're at the start of a new entry - submit the previous one
                if (ThisSegment.SequenceEqual(ThisMagic) || ThisSegment.SequenceEqual(NextMagic))
                {
                    //Only submit if the entry has content
                    if (ThisBlockEntry.Count > 0)
                    {
                        AllBlockEntries.Add(ThisBlockEntry);
                    }
                    ThisBlockEntry.Clear();

                    //If we're still in the current block, continue - else break
                    if (ThisSegment.SequenceEqual(NextMagic))
                    {
                        ArchiveFile.BaseStream.Position -= 4;
                        break;
                    }
                    continue;
                }

                //We're still inside the current entry, add it on!
                for (int x = 0; x < 4; x++)
                {
                    ThisBlockEntry.Add(ThisSegment[x]);
                }
            }

            return AllBlockEntries;
        }

        /* Return a list of filenames for files in the CommandsPAK archive */
        public override List<string> GetFileNames()
        {
            List<string> FileNameList = new List<string>();
            foreach (EntryCommandsPAK ScriptEntry in CommandsEntries)
            {
                FileNameList.Add(ScriptEntry.ScriptName);
            }
            return FileNameList;
        }

        /* Get the file size of a script entry (compiled size, not actual) */
        public override int GetFilesize(string FileName)
        {
            return CommandsEntries[GetFileIndex(FileName)].ScriptContent.Count;
        }

        /* Find the a script entry object by name */
        protected override int GetFileIndex(string FileName)
        {
            for (int i = 0; i < CommandsEntries.Count; i++)
            {
                if (CommandsEntries[i].ScriptName == FileName || CommandsEntries[i].ScriptName == FileName.Replace('/', '\\'))
                {
                    return i;
                }
            }
            throw new Exception("Could not find the requested file in CommandPAK!");
        }
    }
}
