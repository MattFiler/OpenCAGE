using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Records the game's audio (<see cref="GameAudioCapture"/>), one recording at a time: on the running game, or armed for the next
    /// game launched from OpenCAGE (every launch while Launch Game's "Record game audio" is ticked, or once via record_game_audio).
    /// A recording ends when stopped, when its time is up, or when the game closes.
    /// </summary>
    internal static class GameAudioRecorder
    {
        /// <summary>Launch Game's "Record game audio": every game launched from OpenCAGE is recorded until it closes. Off unless ticked.</summary>
        public const string SettingRecord = "RecordGameAudio";
        /// <summary>Where recordings go (empty: <see cref="DefaultFolder"/>).</summary>
        public const string SettingFolder = "GameAudioFolder";

        public static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OpenCAGE", "Game Audio");

        public static string Folder
        {
            get
            {
                string folder = SettingsManager.GetString(SettingFolder, "");
                return string.IsNullOrWhiteSpace(folder) ? DefaultFolder : folder;
            }
        }

        public static bool RecordLaunches => SettingsManager.GetBool(SettingRecord);

        /// <summary>A recording started or ended, or one was armed (raised on any thread).</summary>
        public static event Action Changed;

        public sealed class Request
        {
            public string Path;
            public string Level;
            public bool Quiet;
            public double MaxSeconds;
            public string By;
        }

        /// <summary>What a finished recording holds.</summary>
        public sealed class Summary
        {
            public string Path;
            public string Level;
            public string By;
            public int ProcessId;
            public DateTime? StartUtc;
            public double Seconds;
            public double FilledSeconds;
            /// <summary>Of those, silence filled next to sound: audio missing from the WAV.</summary>
            public double DroppedSeconds;
            public double PeakDb;
            public double RmsDb;
            public bool Quiet;
            public float Gain;
            public string EndedBecause;
            public string Problem;
            public GameAudioSessions.State Session;
            public List<GameAudioCapture.Mark> Marks = new List<GameAudioCapture.Mark>();

            /// <summary>Why the recording may not be what was expected (silent, muted, turned down), or null.</summary>
            public string Warning
            {
                get
                {
                    if (Session != null && Session.Sessions != 0 && Session.Muted && !Quiet)
                        return "Windows has the game muted (its own mute in the Volume mixer, which Windows keeps per program), so the recording is silence. Unmute Alien: Isolation in the Volume mixer.";
                    if (PeakDb <= -150)
                        return "The recording is silent: the game played nothing while it was recorded" + (Session == null || Session.Sessions == 0 ? " (it never opened its audio)." : ".");
                    if (Session != null && Session.Sessions != 0 && !Quiet && Session.Volume < 0.99f)
                        return "The game's own volume in Windows' Volume mixer is " + Math.Round(Session.Volume * 100) + "%, so the recording is that much quieter than the game plays.";
                    //Windows drops 10 ms periods of loopback audio when the PC is busy: timing stays right (silence is filled in) but
                    //that audio is lost
                    if (Seconds > 0 && DroppedSeconds > Math.Max(0.25, Seconds * 0.01))
                        return "Windows dropped " + Math.Round(DroppedSeconds, 1) + " s of the game's audio while the PC was busy (" + Math.Round(DroppedSeconds * 100 / Seconds, 1) + "% of the recording): those moments are silence in the WAV. Times in it are still right.";
                    return null;
                }
            }
        }

        private static readonly object _lock = new object();
        private static GameAudioCapture _current;
        private static Request _currentRequest;
        private static Summary _last;
        private static Request _armed;
        private static int _generation;

        /// <summary>The recording running now, or null.</summary>
        public static GameAudioCapture Current
        {
            get { lock (_lock) return _current != null && _current.Running ? _current : null; }
        }

        /// <summary>The last recording that has ended (since OpenCAGE started), or null.</summary>
        public static Summary Last
        {
            get { lock (_lock) return _last; }
        }

        /// <summary>The recording armed for the next launch from OpenCAGE (record_game_audio), or null.</summary>
        public static Request Armed
        {
            get { lock (_lock) return _armed; }
        }

        /// <summary>Records process <paramref name="pid"/> now. Throws <see cref="GameAudioException"/> when it can't.</summary>
        public static GameAudioCapture Start(int pid, Request request)
        {
            lock (_lock)
            {
                if (_current != null && _current.Running)
                    throw new GameAudioException("Already recording the game, to " + _current.Path + ". Stop that recording first.");
                string path = request.Path ?? NewPath(request.Level);
                GameAudioCapture capture = GameAudioCapture.Start(pid, path, new GameAudioCapture.Options() { Label = request.Level, Quiet = request.Quiet, MaxSeconds = request.MaxSeconds });
                _current = capture;
                _currentRequest = request;
                capture.Ended += OnEnded;
                //It may have ended already (the game closed straight away)
                if (!capture.Running && _last?.Path != capture.Path)
                    _last = Summarise(capture, request);
            }
            Raise();
            return Current;
        }

        /// <summary>Stops the running recording and returns what it holds (null when none was running).</summary>
        public static Summary Stop()
        {
            GameAudioCapture capture;
            lock (_lock) capture = _current;
            if (capture == null) return null;
            capture.Stop();
            lock (_lock)
            {
                if (_last == null || _last.Path != capture.Path)
                    _last = Summarise(capture, _currentRequest);
                return _last;
            }
        }

        /// <summary>Arms a recording for the next game launched from OpenCAGE (once); null takes it back.</summary>
        public static void Arm(Request request)
        {
            lock (_lock) _armed = request;
            Raise();
        }

        private static void OnEnded(GameAudioCapture capture)
        {
            lock (_lock)
            {
                Request request = _current == capture ? _currentRequest : null;
                _last = Summarise(capture, request);
                if (_current == capture) { _current = null; _currentRequest = null; }
            }
            Debug.Log("GameAudio", "recording ended (" + capture.EndedBecause + "): " + capture.Path);
            Raise();
        }

        public static Summary Summarise(GameAudioCapture capture, Request request)
        {
            return new Summary()
            {
                Path = capture.Path,
                Level = capture.Label,
                By = request?.By,
                ProcessId = capture.ProcessId,
                StartUtc = capture.StartUtc,
                Seconds = capture.Seconds,
                FilledSeconds = capture.FilledFrames / (double)GameAudioCapture.SampleRate,
                DroppedSeconds = capture.FilledDuringSoundFrames / (double)GameAudioCapture.SampleRate,
                PeakDb = capture.Peak <= 0 ? -200 : 20 * Math.Log10(capture.Peak),
                RmsDb = GameAudioCapture.ToDb(capture.TotalPower),
                Quiet = capture.Quiet,
                Gain = capture.Gain,
                EndedBecause = capture.EndedBecause ?? (capture.Running ? "recording" : null),
                Problem = capture.Problem,
                Session = capture.Session,
                Marks = capture.Marks,
            };
        }

        private static void Raise()
        {
            try { Changed?.Invoke(); }
            catch (Exception e) { Debug.Log("GameAudio", "a listener failed: " + e.Message); }
        }

        /// <summary>A new file in the recordings folder, named after the level and the time.</summary>
        public static string NewPath(string level)
        {
            string name = string.IsNullOrWhiteSpace(level) ? "game" : level;
            foreach (char bad in Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }))
                name = name.Replace(bad, '_');
            string path = Path.Combine(Folder, name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wav");
            for (int i = 2; File.Exists(path); i++)
                path = Path.Combine(Folder, name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + i + ".wav");
            return path;
        }

        /// <summary>The recordings in the folder, newest first.</summary>
        public static List<FileInfo> Recordings(int count)
        {
            try
            {
                DirectoryInfo folder = new DirectoryInfo(Folder);
                if (!folder.Exists) return new List<FileInfo>();
                return folder.GetFiles("*.wav").OrderByDescending(o => o.LastWriteTimeUtc).Take(count).ToList();
            }
            catch (Exception) { return new List<FileInfo>(); }
        }

        /// <summary>This install's running game: its process id and file, or (-1, null).</summary>
        public static (int Pid, string Exe) FindGame()
        {
            List<Process> games = EditorUtils.ThisInstallsGameProcesses();
            try
            {
                foreach (Process game in games)
                {
                    try
                    {
                        if (!game.HasExited) return (game.Id, EditorUtils.ProcessImagePath(game));
                    }
                    catch (Exception) { }
                }
                return (-1, null);
            }
            finally
            {
                foreach (Process game in games) game.Dispose();
            }
        }

        /// <summary>
        /// Called straight after a game is launched from OpenCAGE (Launch Game, the editor, launch_game, a mod launch);
        /// <paramref name="started"/> is the AI.exe started here, or null. Records the game until it closes when "Record game audio" is
        /// ticked or record_game_audio armed a recording, and restores any Windows volume an earlier quiet recording still owes it.
        /// </summary>
        public static void Launched(string pathToAI, string level, Process started = null)
        {
            Request request;
            lock (_lock)
            {
                request = _armed;
                _armed = null;
            }
            if (request == null && RecordLaunches)
                request = new Request() { Level = level, By = "Record game audio (Launch Game)" };
            else if (request != null && request.Level == null)
                request.Level = level;
            string exe = null;
            try { exe = Path.GetFullPath(Path.Combine(pathToAI, "AI.exe")); } catch (Exception) { }
            bool owed = exe != null && GameAudioSessions.Pending(exe) != null;
            if (request == null && !owed)
                return;
            if (request != null) Raise();

            int generation = Interlocked.Increment(ref _generation);
            int startedPid = -1;
            try { if (started != null) startedPid = started.Id; } catch (Exception) { }
            Task.Run(() =>
            {
                try
                {
                    Watch(generation, pathToAI, exe, startedPid, request, owed);
                }
                catch (Exception e)
                {
                    Debug.Log("GameAudio", "launch watch failed: " + e);
                }
            });
        }

        /* Waits for the game (Steam starts it itself; Epic's launcher restarts the one started here), then records it and/or restores
           an owed volume. A newer launch ends this watch. */
        private static void Watch(int generation, string pathToAI, string exe, int startedPid, Request request, bool owed)
        {
            Stopwatch waiting = Stopwatch.StartNew();
            TimeSpan patience = TimeSpan.FromMinutes(3);
            int lastPid = -1;
            while (Volatile.Read(ref _generation) == generation && waiting.Elapsed < patience)
            {
                int pid = -1;
                if (startedPid != -1 && startedPid != lastPid)
                {
                    try { using (Process p = Process.GetProcessById(startedPid)) if (!p.HasExited) pid = startedPid; } catch (Exception) { }
                }
                if (pid == -1 && string.Equals(Singleton.PathToAI, pathToAI, StringComparison.OrdinalIgnoreCase))
                {
                    (int found, string image) = FindGame();
                    if (found != lastPid) pid = found;
                }
                if (pid == -1)
                {
                    Thread.Sleep(250);
                    continue;
                }
                lastPid = pid;
                DateTime appeared = DateTime.UtcNow;

                //Restore an owed volume once the game has an audio session (a new quiet recording restores the original itself
                //when it ends)
                if (owed && (request == null || !request.Quiet))
                {
                    for (Stopwatch session = Stopwatch.StartNew(); session.Elapsed < TimeSpan.FromMinutes(2) && Volatile.Read(ref _generation) == generation; Thread.Sleep(250))
                    {
                        if (GameAudioSessions.RestorePending(exe, pid)) { Debug.Log("GameAudio", "put back the game's Windows volume a quiet recording took"); break; }
                        if (!Alive(pid)) break;
                    }
                }
                if (request == null)
                    return;

                GameAudioCapture capture;
                try
                {
                    capture = Start(pid, request);
                }
                catch (Exception e)
                {
                    Debug.Log("GameAudio", "could not record the launched game: " + e.Message);
                    lock (_lock) _last = new Summary() { Level = request.Level, By = request.By, Problem = e.Message, EndedBecause = "error: " + e.Message, ProcessId = pid };
                    Raise();
                    return;
                }
                if (capture == null) return;
                capture.WaitEnded(Timeout.Infinite);
                //Epic: the AI.exe started here closes within seconds and restarts through the launcher; drop that recording and
                //record the restarted game
                if (capture.EndedBecause == "the game closed" && DateTime.UtcNow - appeared < TimeSpan.FromSeconds(15) && capture.Peak <= 0)
                {
                    try { File.Delete(capture.Path); } catch (Exception) { }
                    waiting.Restart();
                    patience = TimeSpan.FromMinutes(1);
                    continue;
                }
                return;
            }
        }

        private static bool Alive(int pid)
        {
            try { using (Process p = Process.GetProcessById(pid)) return !p.HasExited; }
            catch (Exception) { return false; }
        }
    }
}
