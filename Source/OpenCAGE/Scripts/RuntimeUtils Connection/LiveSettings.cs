using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.RuntimeUtilsConnection
{
    /// <summary>
    /// The config editors' numbers sent to the running game as they are edited (LiveLink.SetSettings), so a change to the
    /// radiosity, hair and skin shading or global constants shows there at once, rather than when the game next loads a
    /// level and reads the file again. An editor attaches the files it edits while it is open, with a label kept saying
    /// whether its changes reach the game. A change is sent a moment after the last one (a spinner makes many), with every
    /// number of that file, and everything attached is sent again when Live Link connects. UI thread throughout.
    ///
    /// The AI config folders (alien setups, difficulty settings, view cones) are read by the game only when it starts: after
    /// an edit to one of their files is saved, the running game is asked to read the folder again (ReloadAfterEdit: the
    /// game's Debug Menu command ai.reload_configs, so the game must have the Debug Menu on), with the label saying how it went.
    /// </summary>
    internal static class LiveSettings
    {
        private const int DebounceMs = 120;

        private sealed class Attached
        {
            public Label Status;
            public Dictionary<LiveLink.SettingsFile, Func<List<KeyValuePair<string, float>>>> Files;
            /// <summary>Why the game didn't take the last numbers sent, or null.</summary>
            public string Problem;
            /// <summary>The game's runtime utils are from before settings could be changed live.</summary>
            public bool Outdated;
        }

        private static readonly List<Attached> _attached = new List<Attached>();
        private static readonly HashSet<LiveLink.SettingsFile> _changed = new HashSet<LiveLink.SettingsFile>();
        private static Timer _timer;
        private static bool _sending;
        private static bool _subscribed;

        /// <summary>
        /// Send an editor's numbers to the game while it is open: each file it edits, with what gives its numbers now (keys as
        /// the file spells them). The label says whether its changes reach the game.
        /// </summary>
        public static void Attach(Form form, Label status, params (LiveLink.SettingsFile file, Func<List<KeyValuePair<string, float>>> values)[] files)
        {
            Subscribe();
            Attached attached = new Attached() { Status = status, Files = files.ToDictionary(o => o.file, o => o.values) };
            _attached.Add(attached);
            form.FormClosed += (s, e) => _attached.Remove(attached);
            ShowStatus(attached);

            //A game already running may hold other numbers than the file's (it was edited without Live Link since the level loaded)
            if (LiveLink.Connected)
                Changed(files.Select(o => o.file).ToArray());
        }

        /// <summary>Numbers of these files were edited (and saved): send them to the game shortly.</summary>
        public static void Changed(params LiveLink.SettingsFile[] files)
        {
            if (!LiveLink.Connected)
                return;
            foreach (LiveLink.SettingsFile file in files)
                _changed.Add(file);
            if (_timer == null)
            {
                _timer = new Timer() { Interval = DebounceMs };
                _timer.Tick += async (s, e) =>
                {
                    _timer.Stop();
                    await SendChanged();
                };
            }
            _timer.Stop();
            _timer.Start();
        }

        /// <summary>Numbers from a file's spinners, keyed by the spinners' names (named as the file names its keys).</summary>
        public static List<KeyValuePair<string, float>> Values(params NumericUpDown[] controls)
        {
            return controls.Select(o => new KeyValuePair<string, float>(o.Name, (float)o.Value)).ToList();
        }

        private static async Task SendChanged()
        {
            //One at a time, in order: what changes meanwhile goes once the one in flight is answered
            if (_sending)
            {
                _timer.Start();
                return;
            }
            _sending = true;
            try
            {
                while (_changed.Count != 0 && LiveLink.Connected)
                {
                    LiveLink.SettingsFile file = _changed.First();
                    _changed.Remove(file);
                    Attached from = _attached.LastOrDefault(o => o.Files.ContainsKey(file));
                    if (from == null)
                        continue;
                    LiveLink.Reply reply = await LiveLink.SetSettings(file, from.Files[file]());
                    foreach (Attached attached in _attached.Where(o => o.Files.ContainsKey(file)))
                    {
                        attached.Outdated = LiveLink.IsUnknownRequest(reply);
                        attached.Problem = reply.Ok || attached.Outdated ? null : Problem(reply);
                        ShowStatus(attached);
                    }
                }
            }
            finally
            {
                _sending = false;
            }
        }

        /// <summary>The keys the game didn't take, from its answer (a line per key, ending live or inert when taken).</summary>
        private static string Problem(LiveLink.Reply reply)
        {
            List<string> refused = (reply.Message ?? "").Split('\n').Select(o => o.Trim())
                .Where(o => o.Length != 0 && !o.EndsWith(" live") && !o.EndsWith(" inert")).ToList();
            if (refused.Count == 0)
                return reply.Message;
            return "the game didn't take " + string.Join(", ", refused.Take(3)) + (refused.Count > 3 ? " and " + (refused.Count - 3) + " more" : "");
        }

        private static void ShowStatus(Attached attached)
        {
            if (attached.Status == null || attached.Status.IsDisposed)
                return;
            if (!LiveLink.Connected)
                attached.Status.Text = "Changes reach the game when it next loads a level, or as you make them while Live Link is connected.";
            else if (attached.Outdated)
                attached.Status.Text = "Live Link: the game's runtime utils are too old to take these live - launch it again from this OpenCAGE.";
            else if (attached.Problem != null)
                attached.Status.Text = "Live Link: " + attached.Problem + ".";
            else
                attached.Status.Text = "Live Link: changes show in the game as you make them.";
        }

        private static void Subscribe()
        {
            if (_subscribed)
                return;
            LiveLink.ConnectionChanged += OnConnectionChanged;
            _subscribed = true;
        }

        private static void OnConnectionChanged()
        {
            Form main = Singleton.Editor;
            if (main == null || main.IsDisposed)
                return;
            if (main.InvokeRequired)
            {
                main.BeginInvoke(new Action(OnConnectionChanged));
                return;
            }
            foreach (Attached attached in _attached)
            {
                attached.Problem = null;
                attached.Outdated = false;
                ShowStatus(attached);
            }
            //A game connected (or gone) since: how the last reload went no longer says anything about it
            foreach (AttachedReload attached in _reloadsAttached)
            {
                attached.Result = null;
                ShowReloadStatus(attached);
            }
            if (!LiveLink.Connected)
                _toReload.Clear();
            //A newly connected game read the files when its level loaded: send what the open editors hold, in case they differ
            if (LiveLink.Connected)
                Changed(_attached.SelectMany(o => o.Files.Keys).Distinct().ToArray());
        }

        /// <summary>The AI config folders the game reads only when it starts, which the running game can read again.</summary>
        public enum ReloadableConfig
        {
            /// <summary>DATA\ALIENCONFIGS</summary>
            AlienConfigs,
            /// <summary>DATA\DIFFICULTYSETTINGS</summary>
            DifficultySettings,
            /// <summary>DATA\VIEW_CONE_SETS</summary>
            Viewcones,
        }

        /// <summary>The game's Debug Menu command that reads them again (and has the characters take them up), answering a line per folder.</summary>
        public const string ReloadCommand = "ai.reload_configs";

        /// <summary>What the command calls a folder.</summary>
        public static string ReloadArgument(ReloadableConfig config)
        {
            switch (config)
            {
                case ReloadableConfig.AlienConfigs: return "alien";
                case ReloadableConfig.DifficultySettings: return "difficulty";
                default: return "viewcones";
            }
        }

        /// <summary>How the command's line about a folder starts.</summary>
        private static string ReloadLine(ReloadableConfig config)
        {
            switch (config)
            {
                case ReloadableConfig.AlienConfigs: return "alien setups:";
                case ReloadableConfig.DifficultySettings: return "difficulty:";
                default: return "view cones:";
            }
        }

        private const int ReloadDebounceMs = 400;

        private sealed class AttachedReload
        {
            public Label Status;
            public ReloadableConfig Config;
            /// <summary>How the last reload sent while it was open went, as the label shows it; null when none was.</summary>
            public string Result;
        }

        private static readonly List<AttachedReload> _reloadsAttached = new List<AttachedReload>();
        private static readonly HashSet<ReloadableConfig> _toReload = new HashSet<ReloadableConfig>();
        private static Timer _reloadTimer;
        private static bool _reloading;

        /// <summary>
        /// For an editor of one of the AI config folders, while it is open: the label says whether its changes reach the running
        /// game, and how the last one went (ReloadAfterEdit sends them).
        /// </summary>
        public static void AttachReload(Form form, Label status, ReloadableConfig config)
        {
            Subscribe();
            AttachedReload attached = new AttachedReload() { Status = status, Config = config };
            _reloadsAttached.Add(attached);
            form.FormClosed += (s, e) => _reloadsAttached.Remove(attached);
            ShowReloadStatus(attached);
        }

        /// <summary>
        /// An edit to one of a folder's files was saved: the running game reads the folder again shortly after (a spinner makes
        /// many edits, which go as one). Only for an edit: the saves an editor makes as it opens, or as another entry is picked,
        /// change nothing the game reads.
        /// </summary>
        public static void ReloadAfterEdit(ReloadableConfig config)
        {
            if (!LiveLink.Connected)
                return;
            _toReload.Add(config);
            if (_reloadTimer == null)
            {
                _reloadTimer = new Timer() { Interval = ReloadDebounceMs };
                _reloadTimer.Tick += async (s, e) =>
                {
                    _reloadTimer.Stop();
                    await SendReloads();
                };
            }
            _reloadTimer.Stop();
            _reloadTimer.Start();
        }

        private static async Task SendReloads()
        {
            //One at a time: what is edited meanwhile goes once the one in flight is answered
            if (_reloading)
            {
                _reloadTimer.Start();
                return;
            }
            _reloading = true;
            try
            {
                while (_toReload.Count != 0 && LiveLink.Connected)
                {
                    List<ReloadableConfig> configs = _toReload.ToList();
                    _toReload.Clear();
                    LiveLink.Reply reply = await LiveLink.DebugMenu(LiveLink.DebugMenuOp.RunCommand, ReloadCommand, argument: string.Join(" ", configs.Select(ReloadArgument)));
                    foreach (AttachedReload attached in _reloadsAttached.Where(o => configs.Contains(o.Config)))
                    {
                        attached.Result = ReloadResult(reply, attached.Config);
                        ShowReloadStatus(attached);
                    }
                }
            }
            finally
            {
                _reloading = false;
            }
        }

        /// <summary>What the game's answer says for a folder, for its label.</summary>
        private static string ReloadResult(LiveLink.Reply reply, ReloadableConfig config)
        {
            string message = (reply.Message ?? "").Trim();
            if (LiveLink.IsUnknownRequest(reply) || message.StartsWith("No debug menu command named"))
                return "Live Link: the game's runtime utils are too old to read these files again as you edit - launch it again from this OpenCAGE.";
            if (message.StartsWith("The debug menu is off"))
                return "Live Link: the game's Debug Menu is off, so changes reach the game when it next starts (launch it with the Debug Menu on to see them as you edit).";
            if (message.StartsWith("Timed out waiting for the game"))
                return "Live Link: the game reads the change once its AI runs again (it is paused, or no level is running).";
            if (!reply.Answered)
                return "Live Link: the game didn't answer" + (message.Length != 0 ? " (" + message + ")" : "") + ".";

            //A line per folder, and one for the characters that took them up (the setups and the difficulty)
            List<string> lines = message.Split('\n').Select(o => o.Trim()).Where(o => o.Length != 0).ToList();
            string line = lines.FirstOrDefault(o => o.StartsWith(ReloadLine(config)));
            if (line == null)
                return "Live Link: " + (reply.Ok ? "changes read again by the game." : "not read again: " + message);
            string text = line.Substring(ReloadLine(config).Length).Trim();
            if (!text.StartsWith("read again"))
                return "Live Link: " + text + ".";
            text = "changes read again by the game" + text.Substring("read again".Length);
            string characters = config == ReloadableConfig.Viewcones ? null : lines.FirstOrDefault(o => o.StartsWith("characters:"));
            return "Live Link: " + text + (characters != null ? "; " + characters : "") + ".";
        }

        private static void ShowReloadStatus(AttachedReload attached)
        {
            if (attached.Status == null || attached.Status.IsDisposed)
                return;
            if (!LiveLink.Connected)
                attached.Status.Text = "The game reads these files when it starts. While Live Link is connected (with the game's Debug Menu on), each change is read again by the running game.";
            else if (attached.Result != null)
                attached.Status.Text = attached.Result;
            else
                attached.Status.Text = "Live Link: each change is read again by the running game (with its Debug Menu on).";
        }
    }
}
