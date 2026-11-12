using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.Hud
{
    /// <summary>An overlay movie a level keeps, and what it holds.</summary>
    public sealed class LevelOverlay
    {
        public string File;
        /// <summary>The overlay's name (the file name without .GFX).</summary>
        public string Name;
        /// <summary>The builder's design kept in it, or null (a movie made elsewhere).</summary>
        public HudDesign Design;
        public long Length;
    }

    /// <summary>A FlashScript in the level that loads one of the level's overlays, and whether its path is right.</summary>
    public sealed class OverlayLoader
    {
        public Composite Composite;
        public FunctionEntity Entity;
        public string EntityName, Filename, LayerName;
        /// <summary>The path to use (this level's own folder); differs from <see cref="Filename"/> after the level was copied or renamed.</summary>
        public string Expected;
        public bool PathRight => string.Equals(Filename, Expected, StringComparison.OrdinalIgnoreCase);
        /// <summary>The overlay's file exists in this level's folder.</summary>
        public bool FileThere;
    }

    /// <summary>
    /// A level's own HUD overlays: movies in DATA/ENV/&lt;level&gt;/UI/OVERLAYS/&lt;NAME&gt;.GFX (so a mod carries them as level files), each
    /// loaded by a FlashScript in the level's script. The game opens a movie that isn't in UI.PAK as a loose file (ScaleformFileOpener:
    /// packed files first, then loose), so nothing global is written. The layer is always ChallengeMapOverlay (see
    /// <see cref="HudDesign.LayerName"/>), so a level shows one overlay, and not beside a challenge map's own. Every movie is checked
    /// (<see cref="HudMovieCheck"/>) before it is written: one the game can't make a root object from crashes it on the first call.
    /// </summary>
    public static class HudOverlays
    {
        public const string FolderName = "OVERLAYS";

        /// <summary>The level's overlay folder: DATA/ENV/&lt;level&gt;/UI/OVERLAYS (beside its UI overrides, which never list it).</summary>
        public static string Folder(string pathToAI, string level) => Path.Combine(LevelUiOverrides.Folder(pathToAI, level), FolderName);

        public static string FileOf(string pathToAI, string level, string name) => Path.Combine(Folder(pathToAI, level), name.ToUpperInvariant() + ".GFX");

        /// <summary>The path a FlashScript gives the game for a level's overlay: relative to the game folder, forward slashes.</summary>
        public static string GamePath(string level, string name) =>
            "DATA/ENV/" + (level ?? "").Replace('\\', '/').Trim('/') + "/UI/" + FolderName + "/" + name.ToUpperInvariant() + ".GFX";

        /// <summary>The overlays the level keeps.</summary>
        public static List<LevelOverlay> Of(string pathToAI, string level)
        {
            List<LevelOverlay> found = new List<LevelOverlay>();
            string folder = Folder(pathToAI, level);
            if (string.IsNullOrWhiteSpace(level) || !Directory.Exists(folder))
                return found;
            foreach (string file in Directory.GetFiles(folder, "*.GFX").OrderBy(o => o, StringComparer.OrdinalIgnoreCase))
            {
                byte[] bytes = null;
                try { bytes = File.ReadAllBytes(file); } catch { }
                found.Add(new LevelOverlay()
                {
                    File = file,
                    Name = Path.GetFileNameWithoutExtension(file),
                    Design = bytes == null ? null : HudMovieBuilder.ReadDesign(bytes),
                    Length = bytes?.Length ?? 0,
                });
            }
            return found;
        }

        /// <summary>
        /// Build a design's movie, check it, and write it into the level's folder. Returns what went wrong (null when written);
        /// nothing is written when the check refuses the movie.
        /// </summary>
        public static string Write(string pathToAI, string level, HudDesign design, out MovieCheckResult check, IEnumerable<MovieCall> levelCalls = null)
        {
            check = null;
            if (string.IsNullOrWhiteSpace(level) || !Directory.Exists(Path.GetDirectoryName(LevelUiOverrides.Folder(pathToAI, level))))
                return "there is no level " + level;
            byte[] movie;
            try
            {
                movie = HudMovieBuilder.Build(design, out check);
            }
            catch (ArgumentException e)
            {
                return e.Message;
            }
            if (levelCalls != null)
                check = Recheck(movie, design, levelCalls);
            if (!check.Ok)
                return "the movie was refused, so nothing was written: " + string.Join(" ", check.Errors);
            return WriteFile(pathToAI, level, design.Name, movie);
        }

        /// <summary>
        /// Use a movie made elsewhere (Flash / Animate, or an exported overlay) as a level overlay, after checking it against the calls
        /// the level makes on its layer. Returns what went wrong (null when written).
        /// </summary>
        public static string Import(string pathToAI, string level, string name, byte[] movie, IEnumerable<MovieCall> levelCalls, out MovieCheckResult check)
        {
            check = HudMovieCheck.Check(movie, new[] { new MovieCall() { Method = HudMovieBuilder.InitMethod, Arguments = 1, From = "The game (as the layer loads)", Engine = true } }.Concat(levelCalls ?? Enumerable.Empty<MovieCall>()), LayerName(name));
            if (string.IsNullOrWhiteSpace(name) || name.Length > HudDesign.MaxNameLength || !name.All(c => char.IsLetterOrDigit(c) || c == '_') || !char.IsLetter(name[0]))
                return "an overlay's name is 1-" + HudDesign.MaxNameLength + " letters, digits or _, starting with a letter";
            if (!check.Ok)
                return "the movie was refused, so nothing was written: " + string.Join(" ", check.Errors);
            return WriteFile(pathToAI, level, name, movie);
        }

        private static string WriteFile(string pathToAI, string level, string name, byte[] movie)
        {
            try
            {
                string folder = Folder(pathToAI, level);
                Directory.CreateDirectory(folder);
                string file = FileOf(pathToAI, level, name);
                string temp = file + ".writing";
                File.WriteAllBytes(temp, movie);
                if (File.Exists(file)) File.Delete(file);
                File.Move(temp, file);
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        /// <summary>Take an overlay's movie out of the level's folder (its script is left: see <see cref="HudScript.Remove"/>).</summary>
        public static string Remove(string pathToAI, string level, string name)
        {
            try
            {
                string file = FileOf(pathToAI, level, name);
                if (File.Exists(file)) File.Delete(file);
                string folder = Folder(pathToAI, level);
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                    Directory.Delete(folder);
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        /// <summary>The layer an overlay is loaded on: the ChallengeMapOverlay layer for every overlay (see <see cref="HudDesign.LayerName"/>).</summary>
        public static string LayerName(string name) => HudMovieCheck.ChallengeMapLayer;

        /// <summary>A built overlay checked against its design and every call the level makes on its layer.</summary>
        public static MovieCheckResult Recheck(byte[] movie, HudDesign design, IEnumerable<MovieCall> levelCalls)
        {
            MovieCheckResult check = HudMovieBuilder.CheckOverlay(movie, design);
            MovieCheckResult level = HudMovieCheck.Check(movie, levelCalls, design.LayerName);
            foreach (string warning in level.Warnings)
                if (!check.Warnings.Contains(warning))
                    check.Warnings.Add(warning);
            return check;
        }

        #region The level's script
        private static string Str(Entity entity, string parameter) => (entity.GetParameter(parameter)?.content as cString)?.value;

        /// <summary>
        /// Every FlashInvoke in the level that calls a method on a layer, as the calls the movie on that layer must answer
        /// (the method, the arguments its invoke type passes, where it is).
        /// </summary>
        public static List<MovieCall> CallsOnLayer(Commands commands, string layerName)
        {
            List<MovieCall> calls = new List<MovieCall>();
            if (commands == null) return calls;
            foreach (Composite composite in commands.Entries.Where(o => o != null))
                foreach (FunctionEntity entity in composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.FlashInvoke))
                {
                    if (!string.Equals(Str(entity, "layer_name"), layerName, StringComparison.OrdinalIgnoreCase)) continue;
                    string method = Str(entity, "method");
                    if (string.IsNullOrEmpty(method)) continue;
                    calls.Add(new MovieCall()
                    {
                        Method = method,
                        Arguments = InvokeArguments((entity.GetParameter("invoke_type")?.content as cEnum)?.enumIndex ?? 0),
                        From = "FlashInvoke '" + commands.Utils.GetEntityName(composite, entity) + "' in " + composite.name,
                    });
                }
            return calls;
        }

        /// <summary>How many arguments a FlashInvoke's invoke type passes (FLASH_INVOKE_TYPE: NONE, INT, FLOAT, INT_INT, FLOAT_FLOAT, INT_FLOAT, FLOAT_INT, INT_INT_INT, FLOAT_FLOAT_FLOAT).</summary>
        public static int InvokeArguments(int invokeType)
        {
            switch (invokeType)
            {
                case 1: case 2: return 1;
                case 3: case 4: case 5: case 6: return 2;
                case 7: case 8: return 3;
                default: return 0;
            }
        }

        /// <summary>
        /// FlashScripts in the level on the game's ChallengeMapOverlay layer other than the ones in <paramref name="exceptIn"/> (an
        /// overlay's own composite): a level has one such layer, so a challenge map's own overlay or another overlay there is a clash.
        /// </summary>
        public static List<string> ChallengeLayerUsers(Commands commands, Composite exceptIn)
        {
            List<string> found = new List<string>();
            if (commands == null) return found;
            foreach (Composite composite in commands.Entries.Where(o => o != null && o != exceptIn))
                foreach (FunctionEntity entity in composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.FlashScript))
                    if (string.Equals(Str(entity, "layer_name"), HudMovieCheck.ChallengeMapLayer, StringComparison.Ordinal))
                        found.Add(composite.name + "\\" + commands.Utils.GetEntityName(composite, entity) + " (" + Str(entity, "filename") + ")");
            return found;
        }

        /// <summary>FlashScripts in the level that load a movie from a level's overlay folder, with the path this level should give.</summary>
        public static List<OverlayLoader> Loaders(Commands commands, string pathToAI, string level)
        {
            List<OverlayLoader> found = new List<OverlayLoader>();
            if (commands == null) return found;
            foreach (Composite composite in commands.Entries.Where(o => o != null))
                foreach (FunctionEntity entity in composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.FlashScript))
                {
                    string filename = (Str(entity, "filename") ?? "").Replace('\\', '/');
                    int at = filename.IndexOf("/UI/" + FolderName + "/", StringComparison.OrdinalIgnoreCase);
                    if (!filename.StartsWith("DATA/ENV/", StringComparison.OrdinalIgnoreCase) || at < 0) continue;
                    string name = Path.GetFileNameWithoutExtension(filename);
                    found.Add(new OverlayLoader()
                    {
                        Composite = composite,
                        Entity = entity,
                        EntityName = commands.Utils.GetEntityName(composite, entity),
                        Filename = filename,
                        LayerName = Str(entity, "layer_name"),
                        Expected = GamePath(level, name),
                        FileThere = File.Exists(FileOf(pathToAI, level, name)),
                    });
                }
            return found;
        }
        #endregion
    }
}
