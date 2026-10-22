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
            if (!_subscribed)
            {
                LiveLink.ConnectionChanged += OnConnectionChanged;
                _subscribed = true;
            }
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
            //A newly connected game read the files when its level loaded: send what the open editors hold, in case they differ
            if (LiveLink.Connected)
                Changed(_attached.SelectMany(o => o.Files.Keys).Distinct().ToArray());
        }
    }
}
