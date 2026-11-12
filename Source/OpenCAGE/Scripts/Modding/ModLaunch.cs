#if ENABLE_MOD_PACKAGES
using CathodeLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OpenCAGE.Modding
{
    /// <summary>
    /// What a mod's package says about launching it (ModPackageInfo.Launch): the level the game starts in, and launch options for
    /// that launch. Written by Create a Mod; read by Launch Mod.
    /// </summary>
    public class ModLaunchInfo
    {
        /* The level, as DATA/ENV names it ("PRODUCTION/MYLEVEL", "MYLEVEL"); "menu" (or none): the game's main menu */
        [JsonProperty("level")] public string Level;
        /* Launch options for this launch, by launch_options name (ModLaunch.Options): left out, the player's own */
        [JsonProperty("options", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, bool> Options;
    }

    /// <summary>
    /// Launch Mod: starts the game for an installed mod (ticked, applied, files unchanged) into its start level, with its levels' UI
    /// movies (LevelUiOverrides) and its launch options, as Launch Game does for a level. What a launch writes is reset to the player's
    /// Launch Game settings at the next launch, and the level's UI movies are removed once the game closes.
    /// </summary>
    public static class ModLaunch
    {
        /// <summary>The main menu, as a mod's launch level.</summary>
        public const string Menu = "menu";

        public sealed class Option
        {
            public string Name, Label;
            public Func<PatchManager.Platform, string, bool, bool> Patch;
        }

        /// <summary>The launch options a mod can ask for: Launch Game's AI.exe patches that change how a level plays.</summary>
        public static readonly Option[] Options =
        {
            new Option() { Name = "no_ui", Label = "No HUD", Patch = PatchManager.PatchNoUIFlag },
            new Option() { Name = "patch_current_gen_optimisations", Label = "Current-gen script fixes", Patch = PatchManager.DisableCurrentGenOptimisations },
            new Option() { Name = "render_constant_ambient", Label = "Constant ambient lighting", Patch = PatchManager.PatchRenderConstantAmbientFlag },
            new Option() { Name = AnimationHeapBudget.LaunchOptionName, Label = AnimationHeapBudget.Label, Patch = PatchManager.PatchAnimHeapBudget },
        };

        /// <summary>
        /// The level a mod starts in: its package's choice, else its only level; "menu" for the main menu. Null when the player has
        /// to pick: choices are the mod's levels then.
        /// </summary>
        public static string StartLevel(ModPackageInfo info, out List<string> choices)
        {
            choices = info?.Levels.Where(o => !string.Equals(o, "GLOBAL", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<string>();
            string declared = info?.Launch?.Level;
            if (!string.IsNullOrWhiteSpace(declared))
                return IsMenu(declared) ? Menu : declared.Replace('\\', '/').Trim('/');
            if (choices.Count == 1)
                return choices[0];
            if (choices.Count == 0)
                return Menu;
            return null;
        }

        public static bool IsMenu(string level) => level == null || string.Equals(level.Trim(), Menu, StringComparison.OrdinalIgnoreCase) || EditorUtils.IsFrontend(level);

        /// <summary>
        /// Why a mod can't be launched now (not installed, not applied yet, its files changed since), or null. Uses the Mod Manager's
        /// check of the game files (quick once a check has read them).
        /// </summary>
        public static string NotReady(ModState.InstalledMod mod)
        {
            if (mod == null)
                return "there is no such mod in the Mod Manager";
            if (!mod.Enabled || mod.Applied.Count == 0)
                return "'" + mod.Name + "' isn't installed: tick it in the Mod Manager and click Apply first";
            try
            {
                List<string> stale = ModServices.Installer.PathsNeedingRepair().Where(mod.Applied.ContainsKey).ToList();
                if (stale.Count != 0)
                    return "'" + mod.Name + "' has " + stale.Count + " file" + (stale.Count == 1 ? "" : "s") + " that changed since it was installed (" + stale[0] + (stale.Count > 1 ? ", ..." : "") + "): Repair it in the Mod Manager first";
            }
            catch { }
            return null;
        }

        public sealed class Result
        {
            public string Level;                                        //what the game starts in (PRODUCTION/FRONTEND for the menu)
            public List<LevelUiOverrides.Movie> LevelUi = new List<LevelUiOverrides.Movie>();
            public Dictionary<string, bool> Options = new Dictionary<string, bool>();
            public List<string> Closed = new List<string>();            //the game, running before, closed first
            public List<string> Problems = new List<string>();          //what couldn't be set up (the game still starts)
            public Process Started;                                     //GOG and Epic: the AI.exe started (Steam starts the game itself)
        }

        /// <summary>
        /// Starts the game into a level (or the menu) for a mod: closes the game, writes the player's Launch Game settings and then the
        /// mod's launch options, puts the level's UI movies in, and starts the game. Throws when it can't be launched at all (the level
        /// isn't there, or this version of the game can't be launched from OpenCAGE).
        /// </summary>
        public static Result Launch(string level, IDictionary<string, bool> options)
        {
            PatchManager.Platform platform = Singleton.Platform;
            if (platform != PatchManager.Platform.STEAM && platform != PatchManager.Platform.EPIC_GAMES_STORE && platform != PatchManager.Platform.GOG)
                throw new InvalidOperationException("OpenCAGE can only launch the Steam, Epic and GOG versions of the game (this is " + platform + ").");
            string pathToAI = Singleton.PathToAI;
            Result result = new Result() { Level = IsMenu(level) ? EditorUtils.FrontendLevel : level.Replace('\\', '/').Trim('/') };
            bool menu = EditorUtils.IsFrontend(result.Level);
            if (!menu && !Directory.Exists(Path.Combine(pathToAI, "DATA", "ENV", result.Level.Replace('/', Path.DirectorySeparatorChar))))
                throw new InvalidOperationException("The level " + result.Level + " isn't in the game (is the mod installed?).");
            if (result.Level.Length > PatchManager.MaxLaunchMapNameLength)
                throw new InvalidOperationException("The level name " + result.Level + " is too long to launch into (at most " + PatchManager.MaxLaunchMapNameLength + " characters).");

            result.Closed = LaunchGame.CloseGameAndOldInjector();
            //As every launch: the player's Launch Game settings, and the title screen skipped into a level
            List<string> failures = MCP.McpLevelTools.ApplyPatchSettings(platform, !menu);
            //The mod's own launch options, over the player's for this launch (the next launch writes the player's again)
            foreach (KeyValuePair<string, bool> asked in options ?? new Dictionary<string, bool>())
            {
                Option option = Options.FirstOrDefault(o => string.Equals(o.Name, asked.Key, StringComparison.OrdinalIgnoreCase));
                if (option == null)
                {
                    result.Problems.Add("the mod asks for a launch option OpenCAGE doesn't have (" + asked.Key + "): left as it is");
                    continue;
                }
                if (option.Patch(platform, pathToAI, asked.Value))
                    result.Options[option.Name] = asked.Value;
                else
                    failures.Add(option.Name);
            }
            if (failures.Count != 0)
                result.Problems.Add("AI.exe could not be patched for: " + string.Join(", ", failures) + " (is it read-only, or still held open?)");
            string runtimeUtils = LaunchGame.ApplyRuntimeUtils(out _);
            if (runtimeUtils != null)
                result.Problems.Add(runtimeUtils);
            if (!PatchManager.PatchLaunchMode(platform, pathToAI, result.Level))
                result.Problems.Add("AI.exe could not be patched to start in " + result.Level);
            PatchManager.UpdateLevelListInPackages(platform, pathToAI);
            //The level's own UI movies in, an earlier launch's out, the UI tweaks as the player has them
            string ui = LevelUiOverrides.ApplyForLaunch(pathToAI, result.Level, out result.LevelUi);
            if (ui != null)
                result.Problems.Add(ui);

            if (platform == PatchManager.Platform.STEAM)
                Process.Start("steam://rungameid/214490");
            else
                result.Started = Process.Start(new ProcessStartInfo() { FileName = Path.Combine(pathToAI, "AI.exe"), WorkingDirectory = pathToAI });
            //...which come out again once this game has closed
            LevelUiOverrides.PutBackWhenGameCloses(pathToAI);
            //Crash triage watches the game until it closes and names the cause if it crashes
            CrashTriage.CrashWatch.Launched(pathToAI, result.Level, result.Started);
            return result;
        }
    }
}
#endif
