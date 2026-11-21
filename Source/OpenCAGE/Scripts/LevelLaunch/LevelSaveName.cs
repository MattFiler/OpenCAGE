using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.ConfigEditors;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The name a level's saves show on the load screen's save list. The game names a save by the string id the level's LevelInfo
    /// entity gives as the level starts (its save_level_name_id): the save keeps that id and the id's text as it read then ("Unknown
    /// Level" when the id named no string), and the list shows the id's text again wherever it is loaded, else the text kept. A level
    /// with no LevelInfo keeps the id of the level played before it in the same game. So the name is data the level carries: a
    /// LevelInfo entity naming an id of its own, and that id's string in a text database of the level's (TEXT\&lt;language&gt;\
    /// OC_SAVE_NAME.TXT, listed in its TEXT_DB_LIST.TXT), which every build of the game reads however it is started.
    /// </summary>
    public static class LevelSaveName
    {
        public const string Parameter = "save_level_name_id";
        public const string Database = "OC_SAVE_NAME";
        /// <summary>The LevelInfo entity added to the root composite of a level that has none.</summary>
        public const string EntityName = "Level_Save_Name";
        /// <summary>A save keeps its level's string id in 32 bytes.</summary>
        public const int MaxIdLength = 31;
        /// <summary>The list shows 91 characters of a save's name at most, its numbers included.</summary>
        public const int MaxTextLength = 80;
        private const string IdPrefix = "OC_SAVE_";

        /// <summary>
        /// The string id a level's save name gets: the level's own, as the list shows the loaded level's text for every save naming an
        /// id that level loads. At most 31 characters.
        /// </summary>
        public static string IdFor(string level)
        {
            string name = new string((level ?? "").Trim('/', '\\').ToUpperInvariant().Select(o => (o >= 'A' && o <= 'Z') || (o >= '0' && o <= '9') ? o : '_').ToArray());
            if (IdPrefix.Length + name.Length <= MaxIdLength)
                return IdPrefix + name;
            //A long name: its start and a hash of all of it
            using (SHA1 sha = SHA1.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(name))).Replace("-", "").Substring(0, 8);
                return IdPrefix + name.Substring(0, MaxIdLength - IdPrefix.Length - 9) + "_" + hash;
            }
        }

        /// <summary>Whether an id is one OpenCAGE gives a level's save name.</summary>
        public static bool IsOwnId(string id) => id != null && id.StartsWith(IdPrefix, StringComparison.Ordinal);

        /// <summary>Why a text can't be a save name, or null (an empty one is no save name).</summary>
        public static string Problem(string text)
        {
            text = text?.Trim() ?? "";
            if (text.Length > MaxTextLength)
                return "a save name is at most " + MaxTextLength + " characters";
            if (text.IndexOfAny(new[] { '{', '}', '\r', '\n' }) >= 0)
                return "a save name can't hold { } or line breaks";
            return null;
        }

        /// <summary>A save name as the list shows it: its first %u (or %d) is the save's number, any after it the mission's.</summary>
        public static string Shown(string text, uint save, uint mission)
        {
            StringBuilder shown = new StringBuilder();
            bool first = true;
            for (int i = 0; i < (text ?? "").Length; i++)
            {
                if (text[i] == '%' && i + 1 < text.Length && (text[i + 1] == 'u' || text[i + 1] == 'd'))
                {
                    shown.Append(first ? save : mission);
                    first = false;
                    i++;
                    continue;
                }
                shown.Append(text[i]);
            }
            return shown.ToString();
        }

        /// <summary>The level's own text folder: DATA\ENV\&lt;level&gt;\TEXT.</summary>
        public static string TextFolder(string pathToAI, string level) => Path.Combine(pathToAI ?? "", "DATA", "ENV", (level ?? "").Replace('/', '\\').Trim('\\'), "TEXT");

        private static string DbListPath(string pathToAI, string level) => Path.Combine(TextFolder(pathToAI, level), "TEXT_DB_LIST.TXT");
        private static string DatabasePath(string pathToAI, string level, string language) => Path.Combine(TextFolder(pathToAI, level), language, Database + ".TXT");

        private static List<string> DbList(string pathToAI, string level)
        {
            string path = DbListPath(pathToAI, level);
            return File.Exists(path) ? File.ReadAllLines(path).Select(o => o.Trim()).Where(o => o.Length != 0).ToList() : new List<string>();
        }

        /// <summary>The text the level's own save name database holds for an id (in English), or null.</summary>
        public static string OwnText(string pathToAI, string level, string id)
        {
            try
            {
                string path = DatabasePath(pathToAI, level, "ENGLISH");
                if (id == null || !File.Exists(path) || !DbList(pathToAI, level).Contains(Database, StringComparer.OrdinalIgnoreCase))
                    return null;
                return new TextDB(path).Entries.TryGetValue(id, out string text) ? text : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The level's own database holding just this string, in every language, and listed in its TEXT_DB_LIST.TXT (one written
        /// before is replaced). Returns what went wrong, or null.
        /// </summary>
        public static string WriteString(string pathToAI, string level, string id, string text)
        {
            try
            {
                byte[] bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("[" + id + "]\r\n{" + text.Trim() + "}\r\n")).ToArray();
                foreach (string language in LocalisationHandler.LanguageFolders)
                {
                    string path = DatabasePath(pathToAI, level, language);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    Modding.ModServices.CaptureBeforeWrite(path);
                    File.WriteAllBytes(path, bytes);
                }
                List<string> listed = DbList(pathToAI, level);
                if (!listed.Contains(Database, StringComparer.OrdinalIgnoreCase))
                {
                    listed.Add(Database);
                    Modding.ModServices.CaptureBeforeWrite(DbListPath(pathToAI, level));
                    File.WriteAllLines(DbListPath(pathToAI, level), listed);
                }
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        /// <summary>The level's own save name database gone (its files, its line in TEXT_DB_LIST.TXT). Returns what went wrong, or null.</summary>
        public static string RemoveString(string pathToAI, string level)
        {
            try
            {
                foreach (string language in LocalisationHandler.LanguageFolders)
                {
                    string path = DatabasePath(pathToAI, level, language);
                    if (!File.Exists(path)) continue;
                    Modding.ModServices.CaptureBeforeWrite(path);
                    File.Delete(path);
                    //A language folder only it was in goes with it
                    if (!Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(path)).Any())
                        Directory.Delete(Path.GetDirectoryName(path));
                }
                List<string> listed = DbList(pathToAI, level);
                if (listed.RemoveAll(o => string.Equals(o, Database, StringComparison.OrdinalIgnoreCase)) != 0)
                {
                    string list = DbListPath(pathToAI, level);
                    Modding.ModServices.CaptureBeforeWrite(list);
                    if (listed.Count == 0) File.Delete(list);
                    else File.WriteAllLines(list, listed);
                }
                string folder = TextFolder(pathToAI, level);
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                    Directory.Delete(folder);
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        #region THE OPEN LEVEL
        /// <summary>What names a level's saves now.</summary>
        public sealed class State
        {
            /// <summary>The id the level's LevelInfo entities name (null: none names one, so its saves show "Unknown Level").</summary>
            public string Id;
            /// <summary>The id's text (in English) as the level loads it; null when the id names no string the level loads.</summary>
            public string Text;
            /// <summary>The id is the level's own, in its save name database.</summary>
            public bool Own;
            /// <summary>The level is the one open, so its script was read (otherwise only its save name database was).</summary>
            public bool ScriptRead;
            /// <summary>Where its placed LevelInfo entities are: "composite / entity".</summary>
            public List<string> Entities = new List<string>();
            /// <summary>Its LevelInfo entities name different ids.</summary>
            public bool Mixed;
        }

        private static bool IsOpen(string level, out LevelContent content)
        {
            content = Singleton.Editor?.CompositeBrowser?.Content;
            return content?.Level != null && content.IsLevelDataLoaded && string.Equals(content.Level.Name.Replace('\\', '/').Trim('/'), (level ?? "").Replace('\\', '/').Trim('/'), StringComparison.OrdinalIgnoreCase);
        }

        /* The LevelInfo entities of the level that run: those in a composite placed in it */
        private static List<(Composite composite, FunctionEntity entity)> PlacedInfos(Commands commands)
        {
            List<(Composite, FunctionEntity)> infos = new List<(Composite, FunctionEntity)>();
            foreach (Composite composite in commands.Entries)
            {
                List<FunctionEntity> found = composite?.GetFunctionEntitiesOfType(FunctionType.LevelInfo);
                if (found == null || found.Count == 0 || McpScriptEdit.PlacementCount(commands, composite) == 0) continue;
                infos.AddRange(found.Select(o => (composite, o)));
            }
            return infos;
        }

        private static string IdOf(FunctionEntity entity) => (entity.GetParameter(ShortGuidUtils.Generate(Parameter))?.content as cString)?.value ?? "";

        /* A string id's English text as the open level loads it (its own and its shared databases, then every shared one), or null */
        private static string Resolve(LevelContent content, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (content.Level.Strings != null && content.Level.Strings.TryGetValue("ENGLISH", out Dictionary<string, TextDB> english))
                foreach (TextDB db in english.Values)
                    if (db.Entries.TryGetValue(id, out string text)) return text;
            foreach (TextDB db in Singleton.GlobalTextDBs.Values)
                if (db.Entries.TryGetValue(id, out string text)) return text;
            return null;
        }

        /// <summary>What names a level's saves: from its script when it is the level open, else from its save name database alone. UI thread.</summary>
        public static State Read(string pathToAI, string level)
        {
            State state = new State();
            if (!IsOpen(level, out LevelContent content))
            {
                string own = OwnText(pathToAI, level, IdFor(level));
                if (own != null)
                {
                    state.Id = IdFor(level);
                    state.Text = own;
                    state.Own = true;
                }
                return state;
            }
            state.ScriptRead = true;
            Commands commands = content.Level.Commands;
            List<(Composite composite, FunctionEntity entity)> infos = PlacedInfos(commands);
            List<string> ids = infos.Select(o => IdOf(o.entity)).Where(o => o.Length != 0).Distinct().ToList();
            state.Entities = infos.Select(o => o.composite.name + " / " + (McpScript.EntityName(commands, o.composite, o.entity) ?? o.entity.shortGUID.ToByteString())).ToList();
            state.Mixed = ids.Count > 1;
            state.Id = ids.FirstOrDefault();
            if (state.Id == null)
                return state;
            state.Own = state.Id == IdFor(level) && OwnText(pathToAI, level, state.Id) != null;
            state.Text = state.Own ? OwnText(pathToAI, level, state.Id) : Resolve(content, state.Id);
            return state;
        }

        /// <summary>
        /// Name the open level's saves (an empty text: take its save name away). The string goes into the level's own database first
        /// (written at once), then its LevelInfo entities name it - or one is added to its root composite - as one undo step, kept when
        /// the level is saved. Taking it away does the reverse. Returns what was done; throws to refuse. The UI thread, or a tool's.
        /// </summary>
        internal static List<string> Set(string pathToAI, string level, string text, bool fromTool)
        {
            text = text?.Trim() ?? "";
            string problem = Problem(text);
            if (problem != null)
                throw McpError.Invalid("Nothing was changed: " + problem + ".");
            string id = IdFor(level);
            List<string> done = new List<string>();
            Composite root = null;
            List<(Composite composite, FunctionEntity entity)> infos = null;
            McpEditor.UI(() =>
            {
                if (EditorUtils.IsFrontend(level))
                    throw new McpError(McpErrorCodes.Refused, "The main menu makes no saves, so it has no save name.");
                if (!IsOpen(level, out LevelContent content))
                    throw new McpError(McpErrorCodes.Refused, "Nothing was changed: open " + level + " first - its save name is a LevelInfo entity in its script.");
                if (Application.OpenForms.OfType<LevelTextDBEditor>().Any(o => !o.IsDisposed))
                    throw new McpError(McpErrorCodes.Refused, "Nothing was changed: the Level Text DBs window is open, and would save its own list of the level's text databases over this one. Close it first.");
                root = content.Level.Commands.EntryPoints[0];
                infos = PlacedInfos(content.Level.Commands);
            });
            if (root == null)
                throw new McpError(McpErrorCodes.Refused, "Nothing was changed: the level has no root composite.");

            //The string first: a LevelInfo naming an id with no string would name saves "Unknown Level"
            if (text.Length != 0)
            {
                string written = WriteString(pathToAI, level, id, text);
                if (written != null)
                    throw new McpError(McpErrorCodes.Failed, "Nothing was changed: the save name could not be written to the level's text (" + written + ").");
                done.Add("wrote " + id + " = \"" + text + "\" into the level's own text database " + Database + " (TEXT\\<language>\\" + Database + ".TXT, every language) and listed it in TEXT_DB_LIST.TXT");
                RefreshStrings(pathToAI, level);
            }

            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(text.Length != 0 ? "save name" : "remove the save name", root, edit =>
            {
                if (text.Length != 0)
                {
                    if (infos.Count == 0)
                    {
                        FunctionEntity entity = edit.AddFunction(root, FunctionType.LevelInfo, EntityName);
                        edit.SetParameter(root, entity, Parameter, new JValue(id));
                        done.Add("added LevelInfo '" + EntityName + "' to the root composite " + root.name + ", naming " + id);
                        return;
                    }
                    foreach ((Composite composite, FunctionEntity entity) in infos.Where(o => IdOf(o.entity) != id))
                    {
                        edit.SetParameter(composite, entity, Parameter, new JValue(id));
                        done.Add("LevelInfo " + composite.name + " / " + McpScript.EntityName(edit.Commands, composite, entity) + " now names " + id);
                    }
                    return;
                }
                foreach ((Composite composite, FunctionEntity entity) in infos.Where(o => IsOwnId(IdOf(o.entity))))
                {
                    string name = McpScript.EntityName(edit.Commands, composite, entity);
                    if (composite == root && name == EntityName)
                    {
                        edit.Delete(composite, entity);
                        done.Add("removed LevelInfo '" + EntityName + "' from the root composite");
                    }
                    else
                    {
                        edit.RemoveParameter(composite, entity, Parameter);
                        done.Add("LevelInfo " + composite.name + " / " + name + " names no save name now");
                    }
                }
            }, new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = fromTool ? "AI: " : "Level Launch Settings: ", AlsoTouches = infos.Select(o => o.composite).Distinct().ToList() });
            if (outcome.Changed)
                done.Add("save the level to keep the script change (Undo puts it back)");

            if (text.Length == 0)
            {
                if (LocalisationHandler.LanguageFolders.Any(o => File.Exists(DatabasePath(pathToAI, level, o))) || DbList(pathToAI, level).Contains(Database, StringComparer.OrdinalIgnoreCase))
                {
                    string removed = RemoveString(pathToAI, level);
                    if (removed != null)
                        throw new McpError(McpErrorCodes.Failed, "The script no longer names a save name, but the level's " + Database + " text database could not be removed (" + removed + ").");
                    done.Add("removed the level's own text database " + Database);
                    RefreshStrings(pathToAI, level);
                }
                //A name the level's script gives itself (its own string, or the game's) is left as it is
                foreach (string other in infos.Select(o => IdOf(o.entity)).Where(o => o.Length != 0 && !IsOwnId(o)).Distinct())
                    done.Add("the level's script still names " + other + " itself (its LevelInfo entities), which this leaves as it is");
            }
            return done;
        }

        /* The open level's loaded strings and the editor's string lists, brought up to date with the save name database */
        private static void RefreshStrings(string pathToAI, string level)
        {
            McpEditor.UI(() =>
            {
                try { new LocalisationHandler(TextFolder(pathToAI, level), Database).RefreshLoadedStrings(Database); }
                catch (Exception e) { Console.WriteLine("LevelSaveName::RefreshStrings - " + e.Message); }
            });
        }
        #endregion
    }
}
