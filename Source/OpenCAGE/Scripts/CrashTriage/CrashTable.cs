using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenCAGE.CrashTriage
{
    /// <summary>
    /// Known Alien: Isolation crashes, each with where in AI.exe it happens per build (Steam, GOG and Epic ship different
    /// executables). Crash triage (<see cref="CrashAnalysis"/>) matches a dump's faulting address against it.
    ///
    /// The table is plain text (<see cref="BuiltIn"/>); users can add their own in %LOCALAPPDATA%\OpenCAGE\known_crashes.txt in
    /// the same format (an entry with the same id replaces the built-in one).
    ///
    /// FORMAT. '#' starts a comment line. Before the first crash, one line per build of AI.exe:
    ///   build: &lt;key&gt; &lt;PE TimeDateStamp, hex&gt; &lt;SizeOfImage, hex&gt; &lt;name&gt;
    /// Then one block per crash, starting "[id]":
    ///   title: a few words, shown first
    ///   cause: what happened (more cause: lines add sentences)
    ///   fix:   what to do about it (more fix: lines add sentences)
    ///   docs:  a link with more (optional)
    ///   site:  &lt;build&gt; &lt;start&gt; &lt;length&gt; [exact crash offsets...] [weak] [needvia]
    ///          the crash is inside this range of AI.exe's code (all hex; offset = address minus where AI.exe was loaded). An
    ///          exact offset is the instruction a real crash of this kind stopped at. "weak": a crash there is only maybe this
    ///          one. "needvia": code many things call - it only counts with one of the vias on the stack.
    ///   via:   &lt;build&gt; &lt;start&gt; &lt;length&gt;
    ///          a caller seen on the crashing thread's stack when it is this crash (any one of them); a crash at the site
    ///          with none of them on the stack is only "possible"
    ///   module: &lt;file name&gt; [&lt;file name&gt;...]   the crash is inside one of these modules instead (any build; * wildcards)
    ///   most:  possible | likely   the most this crash is ever said to be (when its cause is only inferred)
    ///
    /// ADDING A CRASH: the triage of an unknown crash gives its build, the faulting offset and the AI.exe return addresses on the
    /// stack: the site is the code around the fault, the vias the code around the return addresses that set this crash apart.
    /// </summary>
    internal sealed class CrashTable
    {
        public sealed class Build
        {
            public string Key;
            public uint TimeDateStamp;
            public uint SizeOfImage;
            public string Name;
        }

        /// <summary>A range of AI.exe's code in one build, as offsets from where it was loaded.</summary>
        public sealed class Range
        {
            public string Build;
            public uint Start;
            public uint Length;
            public List<uint> Exact = new List<uint>();
            public bool Weak;
            public bool NeedVia;

            public bool Contains(uint offset) => offset >= Start && offset < Start + Length;
        }

        public sealed class Crash
        {
            public string Id;
            public string Title;
            public string Cause = "";
            public string Fix = "";
            public string Docs;
            public List<Range> Sites = new List<Range>();
            public List<Range> Vias = new List<Range>();
            public List<string> Modules = new List<string>();
            /// <summary>The most it is ever said to be: certain (no cap), likely or possible.</summary>
            public string Most = "certain";
            /// <summary>Where the entry was read from: "built-in" or the user file's path.</summary>
            public string Source;
        }

        public List<Build> Builds { get; } = new List<Build>();
        public List<Crash> Crashes { get; } = new List<Crash>();
        /// <summary>Lines that could not be read, as "source line N: why".</summary>
        public List<string> Problems { get; } = new List<string>();

        /// <summary>The user's own additions, read after the built-in table.</summary>
        public static string UserFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenCAGE", "known_crashes.txt");

        private static CrashTable _current;
        private static DateTime _userFileTime;
        private static readonly object _lock = new object();

        /// <summary>The built-in table plus the user's file (read again when it changes).</summary>
        public static CrashTable Current
        {
            get
            {
                lock (_lock)
                {
                    DateTime time = DateTime.MinValue;
                    try { if (File.Exists(UserFile)) time = File.GetLastWriteTimeUtc(UserFile); } catch (Exception) { }
                    if (_current == null || time != _userFileTime)
                    {
                        CrashTable table = new CrashTable();
                        table.Parse(BuiltIn, "built-in");
                        if (time != DateTime.MinValue)
                        {
                            try { table.Parse(File.ReadAllText(UserFile), UserFile); }
                            catch (Exception e) { table.Problems.Add(UserFile + ": " + e.Message); }
                        }
                        _current = table;
                        _userFileTime = time;
                    }
                    return _current;
                }
            }
        }

        /// <summary>The build a module is, by its PE stamp and size, or null when it is none the table knows.</summary>
        public Build BuildOf(uint timeDateStamp, uint sizeOfImage)
        {
            return Builds.FirstOrDefault(o => o.TimeDateStamp == timeDateStamp && o.SizeOfImage == sizeOfImage);
        }

        /// <summary>Read a table's text into this one: builds are added, crashes added or (same id) replaced.</summary>
        public void Parse(string text, string source)
        {
            Crash crash = null;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                try
                {
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        string id = line.Substring(1, line.Length - 2).Trim();
                        if (id.Length == 0) throw new FormatException("a crash needs an id");
                        Crashes.RemoveAll(o => o.Id == id);
                        crash = new Crash() { Id = id, Title = id, Source = source };
                        Crashes.Add(crash);
                        continue;
                    }
                    int colon = line.IndexOf(':');
                    if (colon <= 0) throw new FormatException("expected 'key: value' or '[id]'");
                    string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                    string value = line.Substring(colon + 1).Trim();
                    if (key == "build")
                    {
                        string[] parts = value.Split(new[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 3) throw new FormatException("build: <key> <stamp> <size> <name>");
                        Builds.RemoveAll(o => o.Key == parts[0]);
                        Builds.Add(new Build() { Key = parts[0], TimeDateStamp = Hex(parts[1]), SizeOfImage = Hex(parts[2]), Name = parts.Length > 3 ? parts[3] : parts[0] });
                        continue;
                    }
                    if (crash == null) throw new FormatException("'" + key + "' before the first [crash]");
                    switch (key)
                    {
                        case "title": crash.Title = value; break;
                        case "cause": crash.Cause = (crash.Cause + " " + value).Trim(); break;
                        case "fix": crash.Fix = (crash.Fix + " " + value).Trim(); break;
                        case "docs": crash.Docs = value; break;
                        case "site": crash.Sites.Add(ParseRange(value, true)); break;
                        case "via": crash.Vias.Add(ParseRange(value, false)); break;
                        case "module": crash.Modules.AddRange(value.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries)); break;
                        case "most":
                            if (value != "possible" && value != "likely" && value != "certain") throw new FormatException("most: possible, likely or certain");
                            crash.Most = value;
                            break;
                        default: throw new FormatException("unknown key '" + key + "'");
                    }
                }
                catch (Exception e) when (e is FormatException || e is OverflowException)
                {
                    Problems.Add(source + " line " + (i + 1) + ": " + e.Message);
                }
            }
        }

        private Range ParseRange(string value, bool site)
        {
            Range range = new Range();
            string[] parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) throw new FormatException((site ? "site" : "via") + ": <build> <start> <length> ...");
            range.Build = parts[0];
            range.Start = Hex(parts[1]);
            range.Length = Hex(parts[2]);
            for (int i = 3; i < parts.Length; i++)
            {
                if (site && parts[i] == "weak") range.Weak = true;
                else if (site && parts[i] == "needvia") range.NeedVia = true;
                else if (site) range.Exact.Add(Hex(parts[i]));
                else throw new FormatException("via takes no exact addresses");
            }
            foreach (uint exact in range.Exact)
                if (!range.Contains(exact)) throw new FormatException("exact address " + exact.ToString("X") + " is outside its range");
            return range;
        }

        private static uint Hex(string text)
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            return uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The known crashes. Steam = the Steam AI.exe; GOG = the 2022 Galaxy build; Epic = the 2020 EOS build.
        /// </summary>
        public const string BuiltIn = @"
build: steam 54E3199D 01F81000 Steam
build: gog   62952F34 01F9D000 GOG
build: epic  5FD25676 01F9F000 Epic Games Store

[character_cap]
title: The 19th character
cause: A 19th character came into play. The game holds 18 characters at once, the player included, and a dead body keeps its place until it is despawned.
cause: A spawn does not check that a place is free, so the 19th crashes the game.
fix: Keep the characters that can be alive or lying dead at once (the player, allies, aliens and NPCs) to 18 at most: despawn bodies before more spawn, spawn in waves, or lower the spawners' counts.
docs: https://opencage.co.uk/docs/launch-game/#crash-triage
site: steam 3D0470 3F0 3D0546
site: gog   3D0420 3F0 3D04F6
site: epic  3E1470 3F0 3E1546

[alien_no_backstage]
title: The alien has no vents to retreat into
cause: The alien tried to go backstage into the vents (it was flamed, sent away by the script, or finished an ambush), but the level has no PathfindingAlienBackstageNode, so there was nowhere to go.
fix: Give the alien vents to go backstage through: File > Alien Vents... (MCP add_alien_vents) finds spots under ceilings and places the game's own vent composites, 3 or more spread out - then Save & Build with the navmesh baker on, to bake the backstage navmesh. Save & Build warns about this.
docs: https://opencage.co.uk/docs/alien-vents/
site: steam 1EA270 B0 1EA27E
site: gog   1EA770 B0 1EA77E
site: epic  1FA800 B0 1FA80E

[empty_cover_grid]
title: A human NPC in combat with no cover data
cause: A human NPC went into combat in a level whose cover data (the COVER file of the level's state) holds an empty 0 x 0 grid, and the cover search read outside it.
fix: Save & Build the level in this version of OpenCAGE, which always writes a valid cover grid, even for a level with no cover.
docs: https://opencage.co.uk/docs/launch-game/#crash-triage
site: steam 3378D0 270 337A85
site: gog   337C30 270 337DE5
site: epic  348260 270 348415
via: steam 342040 340
via: gog   342520 340
via: epic  352B10 340
via: steam 375220 30
via: gog   375520 30
via: epic  385E40 30

[despawn_mid_cover]
title: An NPC despawned while taking cover
cause: An NPC was despawned (or deleted) while it was taking cover: as the character was destroyed, its take-cover request was removed from a path that had already gone.
fix: Stop the NPC's AI before despawning it: CMD_Idle on it with override_all_ai, then DespawnCharacter about a second later (0.8 s works).
docs: https://opencage.co.uk/docs/launch-game/#crash-triage
site: steam 58390 290
site: gog   587D0 290
site: epic  68300 290
via: steam 282560 D0
via: gog   282940 D0
via: epic  292EE0 D0
via: steam 286C80 80
via: gog   287110 80
via: epic  297580 80
via: steam 399CE0 100
via: gog   39A030 100
via: epic  3AAA40 100
via: steam 3BFC90 F0
via: gog   3BFD00 F0
via: epic  3D0C40 F0

[simplewater_no_renderable]
title: A SimpleWater with no renderable
cause: A SimpleWater entity has no renderable (model) resource, so the game crashed setting up its collision as the level loaded.
cause: Older OpenCAGE versions could write a level's resource list short, which moved the water's model away from it.
fix: Save & Build the level in this version of OpenCAGE. If it still crashes, give each SimpleWater entity its model (renderable) again, or delete it and add it back.
docs: https://opencage.co.uk/docs/launch-game/#crash-triage
site: steam DC380 2D0 DC5D6
site: gog   DC6B0 2D0 DC906
site: epic  EC740 2D0 EC996
via: steam DC920 160
via: gog   DCBE0 160
via: epic  ECD00 160

[hud_movie]
title: A HUD movie the game can't drive
cause: A replaced UI movie (a HUD .GFX from a UI mod or from the level's own UI overrides) lacks something the game calls in it when it swaps the HUD's images, so the game crashed as the level opened.
fix: Put the game's own movie back to confirm it (switch the UI tweak off, or take the level's UI override out), then fix the movie so it keeps the objects and ActionScript functions of the original it replaces.
docs: https://opencage.co.uk/docs/launch-game/#crash-triage
site: steam 41C2B0 140 41C3A4
site: gog   41C460 140 41C554
site: epic  42D2E0 140 42D3D4
via: steam 42E0B0 E0
via: gog   42E350 E0
via: epic  43F240 E0

[display_model_no_animated_model]
title: A display model with no animated model
cause: A character uses a display model whose animated model (its skeleton entry) the game could not find, so looking up the model's EYETRACKER point failed.
cause: Display models made by hand, or by older OpenCAGE versions, can lack it.
fix: Import the display model again with this OpenCAGE (with its skeleton set), which writes the composite, model reference and animated model the way the game expects - or point the character back at a retail display model.
docs: https://opencage.co.uk/docs/launch-game/#crash-triage
site: steam 2B4260 100
site: gog   2B46D0 100
site: epic  2C4C70 100
via: steam 2B4E70 F0
via: gog   2B52E0 F0
via: epic  2C5880 F0
via: steam 2B4F60 290
via: gog   2B53D0 290
via: epic  2C5970 290

[display_rig_no_bone_groups]
title: A rig with no bone groups
cause: A character is drawn on a rig whose bone groups the game cannot find: a display rig, or another rig added to ANIMATION.PAK, filed in SKELE\DB.BIN under a source path that does not lead to DATA\REFERENCESKELETONS\<name>_BONE_GROUPS.TXT.
cause: The game reads a rig's bone groups from that path (P:\\CONTENT\BUILD\REFERENCESKELETONS\<name>, as every shipped rig is filed), so the character's animation layers found none and the game crashed as it spawned.
fix: Make the rig again with this version of OpenCAGE's Create display rig (Animation Editor, right-click a set; MCP create_display_rig) under the same name: it files the rig as shipped rigs are, with copies of the gameplay rig's bone groups and mirror table. For a rig added another way, give it a SKELE\DB.BIN row with source path P:\\CONTENT\BUILD\REFERENCESKELETONS\<name> and a DATA\REFERENCESKELETONS\<name>_BONE_GROUPS.TXT.
docs: https://opencage.co.uk/docs/animation-editor/#display-rigs
most: likely
site: steam 2916E0 120 291751
site: gog   291BC0 120 291C31
site: epic  2A1FB0 120 2A2021
via: steam 2A25A0 6F0
via: gog   2A29E0 6F0
via: epic  2B2EE0 6F0
via: steam 2A4F10 130
via: gog   2A5350 130
via: epic  2B5850 130

[light_cap]
title: Over 2048 lights drawn at once
cause: More than 2048 lights without shadows were drawn in one view. The game's list of them holds 2048 and has no check, so the 2049th corrupted it.
fix: Draw fewer lights at once: split the level into zones so rooms out of sight are not drawn, add occluders (models with a CA_OCCLUSION_CULLING material) so hidden lights are culled, and merge or remove small lights.
docs: https://opencage.co.uk/docs/launch-game/#crash-triage
site: steam 5781F0 180 578342
site: gog   578130 180 578282
site: epic  4C9FA0 180 4CA0F2
site: steam 582300 750 weak
site: gog   582300 750 weak
site: epic  4D41E0 750 weak

# The alpha-light pass that lights particles met a light with no instance, seen alongside light_cap crashes.
# Inferred, not proven, so never more than possible.
[light_cap_alpha]
title: Over 2048 lights drawn at once (seen lighting particles)
cause: The pass that lights particles and other see-through things met a light the renderer had dropped - most likely because more than 2048 lights without shadows were in view, which the game can't hold.
fix: Cut the lights in that area: split the level into zones so rooms out of sight are not drawn, add occluders (models with a CA_OCCLUSION_CULLING material) so hidden lights are culled, and merge or remove small lights.
docs: https://opencage.co.uk/docs/launch-game/#crash-triage
most: possible
site: steam 5B1FB0 2F0 5B1FB9 needvia
site: gog   5B21B0 2F0 5B21B9 needvia
site: epic  504150 2F0 504159 needvia
via: steam 5C6200 830
via: gog   5C63C0 830
via: epic  518360 830
via: steam 57FBA0 220
via: gog   57FBA0 220
via: epic  4D1A80 220

[material_texture_gap]
title: A material with an empty texture slot
cause: A material has an empty texture slot before a used one. Older OpenCAGE versions wrote it as a missing texture, which the game reads anyway when it draws the model.
fix: Save & Build the level in this version of OpenCAGE, which fills the gap, or give the material a texture in that slot again in the Material Editor.
docs: https://opencage.co.uk/docs/launch-game/#crash-triage
site: steam 5A51B0 230 5A51DF
site: gog   5A5360 230 5A538F
site: epic  4F7340 230 4F736F

[runtime_utils]
title: A crash inside OpenCAGE's runtime utils
cause: The game crashed inside OpenCAGE's runtime utils, the add-on that serves Live Link, the Debug Menu, hot reload and the other Launch Game helpers.
fix: Launch with Live Link, the Debug Menu and the other helpers off to see whether the game runs without them, and report the crash to OpenCAGE with the dump.
docs: https://opencage.co.uk/docs/launch-game/#crash-triage
module: OpenCAGE.RuntimeUtils.asi

[graphics_driver]
title: A crash inside the graphics driver
cause: The game crashed inside the graphics driver. That can be the driver itself, or something the level gave it to draw that it could not handle, such as a broken imported model, texture or material.
fix: Try other levels: if only this one crashes, look at what was imported or changed in it last. Otherwise update or reinstall the graphics driver.
docs: https://opencage.co.uk/docs/launch-game/#crash-triage
most: possible
module: nvwgf2um.dll nvd3dum.dll atidxx32.dll aticfx32.dll amdxx32.dll amdxc32.dll igd10iumd32.dll igd12umd32.dll igdumdim32.dll
";
    }
}
