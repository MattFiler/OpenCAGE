using Newtonsoft.Json.Linq;
using OpenCAGE.Audio;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// record_game_audio (record what the game itself plays to a WAV: start, mark, stop, status) and analyze_audio (when the sounds
    /// in a WAV start and end, how loud, how they fade). The 'game' toolset; registered from McpTool's tool list.
    /// </summary>
    internal static class McpGameAudioTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "record_game_audio",
                Title = "Record game audio",
                Toolset = "game",
                Description = "Records what the game itself plays (this install's AI.exe and the processes it starts, through Windows' per-program loopback: never other " +
                    "programs, system sounds or a microphone) to a 48 kHz stereo float WAV, so a sound can be proved to play and timed from the audio (analyze_audio). " +
                    "action start: records the running game now; with no game running it is armed for the next launch from OpenCAGE (launch_game) and starts as the game " +
                    "appears. A recording ends at stop, at max_seconds, or when the game closes. mark: a labelled point at this moment (a WAV cue point; analyze_audio gives " +
                    "times after the last mark - mark just before sending what you want to time). stop: ends it and says what it holds (with a short analysis). status: " +
                    "what is recording, the game's Windows volume/mute (a game muted in the Volume mixer records silence), the last recording. quiet: turns the game's " +
                    "own volume in Windows down to 0.05% while recording, so it is not heard, and scales the recording back up; its volume and mute are put back when " +
                    "the recording ends (stop before closing the game). Needs Windows 11 or Windows 10 build 20348+.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "start, mark, stop, status (default) or cancel (takes back a recording armed for the next launch).", options: new[] { "start", "mark", "stop", "status", "cancel" }),
                    McpSchema.String("path", "start: the WAV to write (absolute, or relative to the recordings folder). Default: the recordings folder, named after the level and time."),
                    McpSchema.Boolean("overwrite", "start: replace a file already at 'path'."),
                    McpSchema.Boolean("quiet", "start: the game is not heard while it records (its Windows volume turned down, the recording scaled back up, the volume put back after)."),
                    McpSchema.Number("max_seconds", "start: stop by itself after this long (default 600; 0 = until stop or the game closes; at most 10800)."),
                    McpSchema.String("level", "start: what the recording is of, for its file name (default: the launched level, or the open level)."),
                    McpSchema.String("label", "mark: the mark's label (default 'mark N')."),
                    McpSchema.Boolean("analyze", "stop: include a short analysis of the recording (default true).")),
                Run = RecordGameAudio,
            };

            yield return new McpTool()
            {
                Name = "analyze_audio",
                Title = "Analyse audio",
                Toolset = "game",
                Description = "Measures a WAV (default: the last record_game_audio recording; any uncompressed WAV works): its loudness over time (dBFS), the sounds " +
                    "in it (start, end, peak, held level, fade in/out lengths), onsets (a sound starting out of quiet, or a rise of rise_db over the quarter second " +
                    "before: a sound starting over others), and where given tones are. Times are seconds from the WAV's start; a game audio recording also gives each as " +
                    "UTC and as seconds after the last mark before it (record_game_audio mark), so 'the music started 3.3 s after the play' reads straight off. " +
                    "Threshold: by default 12 dB over the recording's quiet level (at least -100 dBFS).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "The WAV (default: the last recording)."),
                    McpSchema.Number("from_s", "Only from here (seconds into the file)."),
                    McpSchema.Number("to_s", "Only up to here."),
                    McpSchema.Number("threshold_dbfs", "A sound is louder than this (default: picked from the recording)."),
                    McpSchema.Number("min_silence_ms", "Sounds closer than this are one (default 250)."),
                    McpSchema.Number("rise_db", "An onset over other sound is a rise of at least this (default 10)."),
                    McpSchema.Number("frame_ms", "Loudness window (default 10 ms; 1-100): the time resolution."),
                    McpSchema.Array("tones_hz", "Tones to find (Hz): where each makes up most of the sound.", new JObject() { ["type"] = "number" }),
                    McpSchema.Boolean("envelope", "Include the loudness curve (default true)."),
                    McpSchema.Number("envelope_step_s", "The loudness curve's step (default: at most 300 points)."),
                    McpSchema.Limit(100, "sounds")),
                ReadOnly = true,
                Idempotent = true,
                Run = AnalyzeAudio,
            };
        }

        private static object RecordGameAudio(McpCall call)
        {
            string action = (call.Str("action") ?? "status").Trim().ToLowerInvariant();
            switch (action)
            {
                case "start": return StartRecording(call);
                case "mark": return Mark(call);
                case "stop": return StopRecording(call);
                case "status": return Status();
                case "cancel":
                    {
                        bool armed = GameAudioRecorder.Armed != null;
                        GameAudioRecorder.Arm(null);
                        return new JObject() { ["cancelled"] = armed, ["recording"] = GameAudioRecorder.Current != null };
                    }
                default: throw new McpError(McpErrorCodes.InvalidArgument, "'action' is start, mark, stop, status or cancel.");
            }
        }

        private static object StartRecording(McpCall call)
        {
            if (GameAudioRecorder.Current is GameAudioCapture running)
                throw new McpError(McpErrorCodes.Conflict, "Already recording the game (" + running.Path + ", " + Math.Round(running.Seconds, 1) + " s). Stop it first (record_game_audio stop).");
            double maxSeconds = call.Has("max_seconds") ? call.Num("max_seconds") : 600;
            if (maxSeconds < 0 || maxSeconds > 10800) throw new McpError(McpErrorCodes.InvalidArgument, "max_seconds is 0 (no limit) to 10800.");
            string level = call.Str("level");
            string path = null;
            if (call.Has("path"))
            {
                path = call.Str("path").Trim();
                if (!Path.IsPathRooted(path)) path = Path.Combine(GameAudioRecorder.Folder, path);
                path = Path.GetFullPath(path);
                if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) path += ".wav";
                if (File.Exists(path) && !call.Bool("overwrite"))
                    throw new McpError(McpErrorCodes.Conflict, path + " already exists: pass overwrite: true to replace it, or another path.");
            }
            GameAudioRecorder.Request request = new GameAudioRecorder.Request() { Path = path, Level = level, Quiet = call.Bool("quiet"), MaxSeconds = maxSeconds, By = "record_game_audio" };

            (int pid, string exe) = GameAudioRecorder.FindGame();
            if (pid == -1)
            {
                //Armed: the next launch from OpenCAGE records (the level is the launched one unless named)
                GameAudioRecorder.Arm(request);
                return new JObject()
                {
                    ["armed"] = true,
                    ["note"] = "The game is not running: the next game launched from OpenCAGE (launch_game) is recorded from when it appears" + (maxSeconds > 0 ? ", for up to " + maxSeconds + " s" : "") + ".",
                    ["folder"] = path == null ? GameAudioRecorder.Folder : Path.GetDirectoryName(path),
                    ["quiet"] = request.Quiet,
                };
            }
            if (request.Level == null)
                request.Level = McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name) ?? "game";
            GameAudioCapture capture;
            try
            {
                capture = GameAudioRecorder.Start(pid, request);
            }
            catch (GameAudioException e)
            {
                throw new McpError(McpErrorCodes.Failed, "Could not record the game: " + e.Message);
            }
            JObject result = new JObject()
            {
                ["recording"] = capture != null,
                ["path"] = capture?.Path ?? GameAudioRecorder.Last?.Path,
                ["pid"] = pid,
                ["quiet"] = request.Quiet,
                ["max_seconds"] = maxSeconds,
            };
            if (capture?.StartUtc != null) result["start_utc"] = Utc(capture.StartUtc.Value);
            if (capture != null && capture.Session.Sessions != 0) result["game_volume"] = capture.Session.Describe();
            if (capture != null && capture.Session.Sessions != 0 && capture.Session.Muted && !request.Quiet)
                call.Note("Windows has the game muted (its Volume mixer mute, kept per program): this recording will be silence. Unmute it there, or record with quiet: true (which unmutes it while recording and puts the mute back after).");
            if (capture == null)
                call.Note("The game closed as the recording started.");
            return result;
        }

        private static object Mark(McpCall call)
        {
            GameAudioCapture capture = GameAudioRecorder.Current ?? throw new McpError(McpErrorCodes.Refused, "Nothing is recording (record_game_audio start first).");
            GameAudioCapture.Mark mark = capture.AddMark(call.Str("label"));
            JObject result = new JObject() { ["label"] = mark.Label, ["at_s"] = Math.Round(mark.Seconds, 3), ["utc"] = Utc(mark.Utc), ["path"] = capture.Path };
            if (!capture.StartUtc.HasValue) call.Note("No audio had arrived yet: the mark sits at the start of the recording.");
            return result;
        }

        private static object StopRecording(McpCall call)
        {
            GameAudioRecorder.Summary summary = GameAudioRecorder.Current != null ? GameAudioRecorder.Stop() : null;
            JObject result = new JObject() { ["stopped"] = summary != null };
            if (summary == null)
            {
                summary = GameAudioRecorder.Last;
                if (summary == null)
                {
                    bool armed = GameAudioRecorder.Armed != null;
                    GameAudioRecorder.Arm(null);
                    if (armed) result["note"] = "Nothing had started recording yet: the recording armed for the next launch is taken back.";
                    else throw new McpError(McpErrorCodes.Refused, "Nothing is recording, and nothing has been recorded since OpenCAGE started.");
                    return result;
                }
                result["note"] = "Nothing was recording: this is the last recording, which ended by itself (" + (summary.EndedBecause ?? "?") + ").";
            }
            result["recording"] = Describe(summary);
            if (summary.Warning != null) call.Note(summary.Warning);
            if (summary.Problem != null) call.Note(summary.Problem);
            if (call.Bool("analyze", true) && summary.Path != null && File.Exists(summary.Path) && summary.Seconds > 0)
            {
                try
                {
                    AudioAnalysis.Result analysis = AudioAnalysis.Analyze(summary.Path, new AudioAnalysis.Options(), call.Cancel);
                    JObject brief = analysis.ToJson(20, envelope: false);
                    foreach (string key in new[] { "path", "format", "duration_s", "start_utc", "marks" }) brief.Remove(key);
                    result["analysis"] = brief;
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    call.Note("The recording could not be analysed: " + e.Message);
                }
            }
            return result;
        }

        private static JObject Status()
        {
            JObject result = new JObject();
            GameAudioCapture capture = GameAudioRecorder.Current;
            result["recording"] = capture != null;
            if (capture != null)
            {
                result["path"] = capture.Path;
                result["pid"] = capture.ProcessId;
                result["seconds"] = Math.Round(capture.Seconds, 2);
                if (capture.StartUtc.HasValue) result["start_utc"] = Utc(capture.StartUtc.Value);
                result["level_now_dbfs"] = Math.Round(Math.Max(-200, capture.RecentDb), 1);
                result["peak_dbfs"] = capture.Peak <= 0 ? -200 : Math.Round(20 * Math.Log10(capture.Peak), 1);
                result["quiet"] = capture.Quiet;
                result["game_volume"] = capture.Session.Describe();
                result["marks"] = capture.Marks.Count;
            }
            if (GameAudioRecorder.Armed is GameAudioRecorder.Request armed)
                result["armed_for_next_launch"] = new JObject() { ["path"] = armed.Path, ["quiet"] = armed.Quiet, ["max_seconds"] = armed.MaxSeconds };
            (int pid, string exe) = GameAudioRecorder.FindGame();
            result["game_running"] = pid != -1;
            if (pid != -1 && capture == null)
                result["game_volume"] = GameAudioSessions.Read(pid).Describe();
            result["record_every_launch"] = GameAudioRecorder.RecordLaunches;
            result["folder"] = GameAudioRecorder.Folder;
            if (GameAudioRecorder.Last is GameAudioRecorder.Summary last) result["last"] = Describe(last);
            int build = GameAudioCapture.WindowsBuild();
            if (build > 0 && build < 20348) result["windows"] = "build " + build + ": recording one program needs Windows 11 (or Windows 10 build 20348+)";
            return result;
        }

        private static JObject Describe(GameAudioRecorder.Summary summary)
        {
            JObject json = new JObject()
            {
                ["path"] = summary.Path,
                ["level"] = summary.Level,
                ["seconds"] = Math.Round(summary.Seconds, 3),
            };
            if (summary.StartUtc.HasValue) json["start_utc"] = Utc(summary.StartUtc.Value);
            json["peak_dbfs"] = Math.Round(Math.Max(-200, summary.PeakDb), 1);
            json["rms_dbfs"] = Math.Round(Math.Max(-200, summary.RmsDb), 1);
            json["ended"] = summary.EndedBecause;
            if (summary.FilledSeconds > 0) json["silence_filled_s"] = Math.Round(summary.FilledSeconds, 3);
            if (summary.DroppedSeconds > 0) json["dropped_during_sound_s"] = Math.Round(summary.DroppedSeconds, 3);
            if (summary.Quiet) { json["quiet"] = true; json["gain"] = Math.Round(summary.Gain, 2); }
            if (summary.Session != null) json["game_volume"] = summary.Session.Describe();
            if (summary.Marks.Count != 0)
                json["marks"] = new JArray(summary.Marks.Select(o => new JObject() { ["label"] = o.Label, ["at_s"] = Math.Round(o.Seconds, 3), ["utc"] = Utc(o.Utc) }));
            if (summary.Warning != null) json["warning"] = summary.Warning;
            if (summary.Problem != null) json["problem"] = summary.Problem;
            return json;
        }

        private static object AnalyzeAudio(McpCall call)
        {
            string path = call.Str("path");
            if (string.IsNullOrWhiteSpace(path))
            {
                path = GameAudioRecorder.Current?.Path ?? GameAudioRecorder.Last?.Path ?? GameAudioRecorder.Recordings(1).FirstOrDefault()?.FullName;
                if (path == null) throw new McpError(McpErrorCodes.InvalidArgument, "Give 'path': nothing has been recorded (record_game_audio) and the recordings folder (" + GameAudioRecorder.Folder + ") has no WAVs.");
            }
            else if (!Path.IsPathRooted(path))
                path = Path.Combine(GameAudioRecorder.Folder, path);
            if (!File.Exists(path)) throw new McpError(McpErrorCodes.NotFound, "There is no file " + path + ".");

            AudioAnalysis.Options options = new AudioAnalysis.Options()
            {
                From = call.Num("from_s", 0),
                To = call.Num("to_s", 0),
                FrameMs = call.Num("frame_ms", 10),
                MinSilenceMs = call.Num("min_silence_ms", 250),
                RiseDb = call.Num("rise_db", 10),
            };
            if (call.Has("threshold_dbfs")) options.ThresholdDb = call.Num("threshold_dbfs");
            JArray tones = call.Array("tones_hz");
            if (tones != null) options.TonesHz = tones.Select(o => (double)o).ToArray();
            if (options.FrameMs < 1 || options.FrameMs > 100) throw new McpError(McpErrorCodes.InvalidArgument, "frame_ms is 1 to 100.");

            AudioAnalysis.Result result;
            try
            {
                using (McpEditorTools.Heartbeat(call, "Measuring " + Path.GetFileName(path)))
                    result = AudioAnalysis.Analyze(path, options, call.Cancel);
            }
            catch (Exception e) when (e is InvalidDataException || e is NotSupportedException || e is IOException || e is UnauthorizedAccessException)
            {
                throw new McpError(McpErrorCodes.Failed, "Could not read " + path + ": " + e.Message);
            }
            if (GameAudioRecorder.Current?.Path is string live && string.Equals(Path.GetFullPath(live), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                call.Note("That file is still being recorded: this measures it up to a second or two ago.");
            if (result.PeakDb <= -150) call.Note("The file is silent throughout.");
            int limit = Math.Max(1, Math.Min(2000, call.Int("limit", 100)));
            return result.ToJson(limit, call.Bool("envelope", true), call.Num("envelope_step_s", 0));
        }

        private static string Utc(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }
}
