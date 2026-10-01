using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAGE.RuntimeUtilsConnection
{
    /// <summary>
    /// Live link: script edits made here reach the game while it runs the level, through the runtime utils ASI
    /// (Source/Dependencies/RuntimeUtils, LIVE_LINK*.cpp) over the runtime utils connection.
    ///
    /// A composite is sent whole, as the game holds it in memory (Commands.WriteLiveLinkImage); the game works out what
    /// changed against what it is running and brings every running instance of the composite in line - entities that
    /// went are shut down and removed, new ones are created and initialised, and entities whose parameters changed are
    /// reinitialised the way CA's own live link did (EntityState::live_edit). New composites, and new models or other
    /// resources, need the level saved and reloaded.
    /// </summary>
    public static class LiveLink
    {
        public const uint Magic = 0x4C4C434F; //"OCLL"
        public const ushort Version = 1;

        public enum Command : ushort
        {
            STATUS = 1,
            CALL_METHOD = 2,
            APPLY_COMPOSITE = 3,
            LOAD_LEVEL = 4,
            SCREENSHOT = 5,
            DESCRIBE = 6,
            CAMERA = 7,
            CAMERA_GET = 8,
            ANIMATION = 9,
            ANIMATION_GET = 10,
            TRACE = 11,
            TRACE_GET = 12,
        }

        public sealed class Reply
        {
            public bool Ok;
            public string Message;
            /// <summary>For a push: the size of the image sent.</summary>
            public int Bytes;
            /// <summary>The game answered (a refusal is an answer too); false when it could not be asked, or did not answer in time.</summary>
            public bool Answered;
            /// <summary>What the game sent after the message (TRACE_GET's records); null when it sent nothing more.</summary>
            public byte[] Payload;
        }

        public static bool Connected => Send.Connected;

        /// <summary>The connection opened or closed. Raised on any thread.</summary>
        public static event Action ConnectionChanged;

        /// <summary>A composite's scripting was sent to the game and it answered (the auto push, or PushNow). Raised on the UI thread.</summary>
        public static event Action<Composite, Reply> Pushed;

        /// <summary>A line about what the live link just did, for the status bar. Raised on the UI thread.</summary>
        public static event Action<string> Activity;

        private static readonly object _lock = new object();
        private static readonly Dictionary<uint, TaskCompletionSource<Reply>> _pending = new Dictionary<uint, TaskCompletionSource<Reply>>();
        private static uint _nextId = 1;

        #region CONNECTION
        internal static void NotifyConnectionChanged()
        {
            //A new connection may be to a game that has reloaded the level since: send everything afresh
            ForgetSent();
            ConnectionChanged?.Invoke();
        }

        internal static void FailPending(string why)
        {
            List<TaskCompletionSource<Reply>> pending;
            lock (_lock)
            {
                pending = _pending.Values.ToList();
                _pending.Clear();
            }
            foreach (TaskCompletionSource<Reply> request in pending)
                request.TrySetResult(new Reply() { Ok = false, Message = why });
        }

        internal static void HandleReply(byte[] data)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
                {
                    if (reader.ReadUInt32() != Magic || reader.ReadUInt16() != Version)
                        return;
                    reader.ReadUInt16(); //command | 0x8000
                    uint id = reader.ReadUInt32();
                    bool ok = reader.ReadByte() != 0;
                    string message = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
                    //Some replies carry more after the message (TRACE_GET's records)
                    long rest = data.Length - reader.BaseStream.Position;
                    byte[] payload = rest > 0 ? reader.ReadBytes((int)rest) : null;

                    TaskCompletionSource<Reply> request;
                    lock (_lock)
                    {
                        if (!_pending.TryGetValue(id, out request))
                            return;
                        _pending.Remove(id);
                    }
                    request.TrySetResult(new Reply() { Ok = ok, Message = message, Answered = true, Payload = payload });
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Live Link", "Could not read a reply: " + ex.Message);
            }
        }

        private static async Task<Reply> Request(Command command, Action<BinaryWriter> payload, double timeoutSeconds = 30)
        {
            if (!Connected)
                return new Reply() { Ok = false, Message = "Not connected to the game" };

            TaskCompletionSource<Reply> request = new TaskCompletionSource<Reply>(TaskCreationOptions.RunContinuationsAsynchronously);
            uint id;
            lock (_lock)
            {
                id = _nextId++;
                _pending[id] = request;
            }

            byte[] message;
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(Magic);
                    writer.Write(Version);
                    writer.Write((ushort)command);
                    writer.Write(id);
                    payload(writer);
                }
                message = stream.ToArray();
            }
            if (!Send.SendBinary(message))
            {
                lock (_lock) _pending.Remove(id);
                return new Reply() { Ok = false, Message = "Could not send to the game" };
            }

            //Requests on entities are carried out once a frame; a paused or loading game can take a while (a camera pose
            //waits far less: the next one is already on its way)
            Task finished = await Task.WhenAny(request.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            if (finished != request.Task)
            {
                lock (_lock) _pending.Remove(id);
                return new Reply() { Ok = false, Message = "The game did not answer (is it paused on a loading screen, or at a breakpoint?)" };
            }
            return request.Task.Result;
        }

        private static void WriteString(BinaryWriter writer, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        #endregion

        #region REQUESTS
        /// <summary>Whether the game is running a level, and which (its root composite).</summary>
        public static async Task<GameStatus> Status()
        {
            Reply reply = await Request(Command.STATUS, w => { });
            GameStatus status = new GameStatus() { Reply = reply };
            if (!reply.Ok)
                return status;
            foreach (string line in reply.Message.Split('\n'))
            {
                int equals = line.IndexOf('=');
                if (equals < 0) continue;
                string key = line.Substring(0, equals);
                string value = line.Substring(equals + 1);
                switch (key)
                {
                    case "running": status.LevelRunning = value == "1"; break;
                    case "root": status.RootComposite = new ShortGuid(value); break;
                    case "root_name": status.RootCompositeName = value; break;
                    case "camera": status.CameraPosition = Triple(value); break;
                    case "camera_forward": status.CameraForward = Triple(value); break;
                    case "camera_up": status.CameraUp = Triple(value); break;
                    case "camera_sync": status.CameraSync = value == "1"; break;
                    case "playing": status.Playing = value == "1"; break;
                    case "loading_reason": status.LoadingReason = value; break;
                    case "animation": status.Animation = value == "1"; break;
                    case "trace": status.Trace = value == "1"; break;
                }
            }
            return status;
        }

        private static System.Numerics.Vector3? Triple(string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length != 3) return null;
            float[] v = new float[3];
            for (int i = 0; i < 3; i++)
                if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v[i])) return null;
            return new System.Numerics.Vector3(v[0], v[1], v[2]);
        }

        public sealed class GameStatus
        {
            public Reply Reply;
            public bool LevelRunning;
            public ShortGuid RootComposite = ShortGuid.Invalid;
            public string RootCompositeName = "";
            /// <summary>Where the game camera is and which way it looks (world space), when the game could read it.</summary>
            public System.Numerics.Vector3? CameraPosition;
            public System.Numerics.Vector3? CameraForward;
            public System.Numerics.Vector3? CameraUp;
            /// <summary>Whether the game is rendering from the camera OpenCAGE sends (the viewport's: LiveLinkCameraSync).</summary>
            public bool CameraSync;
            /// <summary>Whether the game takes edits and calls now (a level being played, not still loading).</summary>
            public bool Playing;
            /// <summary>While the level is still loading: why edits and calls are being held.</summary>
            public string LoadingReason;
            /// <summary>Whether the game has a CAGEAnimation taken for OpenCAGE (LiveLinkAnimationDrive): held, played, or waiting to be given back.</summary>
            public bool Animation;
            /// <summary>Whether the game is gathering script activity for this connection (TRACE; LiveLinkTrace).</summary>
            public bool Trace;

            /// <summary>Whether the game is running the level these commands are from.</summary>
            public bool IsRunning(Commands commands)
            {
                Composite root = commands?.EntryPoints?[0];
                return LevelRunning && root != null && root.shortGUID == RootComposite;
            }
        }

        /// <summary>
        /// The root composite of the level these commands are: sent with edits and calls, so the game refuses them (on its
        /// entity thread, where the answer cannot go stale) when it is running another level. 0 when there is none.
        /// </summary>
        public static uint RootOf(Commands commands)
        {
            Composite root = commands?.EntryPoints?[0];
            return root == null ? 0 : root.shortGUID.AsUInt32;
        }

        /// <summary>
        /// The composite on screen holds its links in its flowgraph pages until they are compiled: compile them before its
        /// scripting is read (as the MCP tools do). UI thread.
        /// </summary>
        public static void CompileIfShown(Composite composite)
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display != null && !display.IsDisposed && display.Populated && display.Composite == composite && !Undo.UndoStack.Current.Blocked)
                display.SaveAllFlowgraphs();
        }

        /// <summary>Replace a composite's scripting in the running game with what it is here. UI thread.</summary>
        public static Task<Reply> PushComposite(Commands commands, Composite composite)
        {
            byte[] image;
            List<int> relocations;
            try
            {
                CompileIfShown(composite);
                image = commands.WriteLiveLinkImage(composite, out relocations);
            }
            catch (Exception ex)
            {
                return Task.FromResult(new Reply() { Ok = false, Message = "Could not write " + composite.name + " for the game: " + ex.Message });
            }
            return PushImage(RootOf(commands), composite.shortGUID, image, relocations);
        }

        /// <summary>Send a composite already written with Commands.WriteLiveLinkImage (which must run where the level is safe to read).</summary>
        public static Task<Reply> PushImage(uint root, ShortGuid composite, byte[] image, List<int> relocations)
        {
            return Request(Command.APPLY_COMPOSITE, w =>
            {
                w.Write(root);
                w.Write(composite.AsUInt32);
                w.Write(image.Length);
                w.Write(image);
                w.Write(relocations.Count);
                foreach (int relocation in relocations)
                    w.Write(relocation);
            });
        }

        /// <summary>
        /// Call a method (e.g. "start") on an entity in the running game: in the one instance at the given path of
        /// composite instance entities from the level's root, or in every instance of the composite when there is no path.
        /// </summary>
        public static Task<Reply> CallMethod(uint root, Composite composite, Entity entity, ShortGuid method, IList<ShortGuid> instancePath)
        {
            return Request(Command.CALL_METHOD, w =>
            {
                w.Write(root);
                w.Write(composite.shortGUID.AsUInt32);
                w.Write(entity.shortGUID.AsUInt32);
                w.Write(method.AsUInt32);
                w.Write(instancePath?.Count ?? 0);
                if (instancePath != null)
                    foreach (ShortGuid step in instancePath)
                        w.Write(step.AsUInt32);
            });
        }

        /// <summary>The composite's running instances and their entities, as the game sees them.</summary>
        public static Task<Reply> Describe(Composite composite)
        {
            return Request(Command.DESCRIBE, w => w.Write(composite.shortGUID.AsUInt32));
        }

        /// <summary>Write the game's current frame to a .bmp.</summary>
        public static Task<Reply> Screenshot(string path)
        {
            return Request(Command.SCREENSHOT, w => WriteString(w, path));
        }

        /// <summary>Ask the game to load a level (e.g. "PRODUCTION/BSP_TORRENS"). Needs the hot reload helper on.</summary>
        public static Task<Reply> LoadLevel(string level)
        {
            return Request(Command.LOAD_LEVEL, w => WriteString(w, level.Replace('/', '\\')));
        }

        /// <summary>How long a camera request waits for its answer. Poses come many a second, and a later one replaces a pose
        /// that is still waiting to go, so one that is not answered quickly is not worth waiting on.</summary>
        public const double CameraTimeoutSeconds = 1.0;

        /// <summary>
        /// Have the game render from this camera, in its world space (CATHODE's axes: Y up; forward and up unit vectors; fov
        /// vertical, in degrees, 0 or less to keep the game's own), streaming in the zones it needs around it - while it is
        /// running the level of this root (0: any level). LiveLinkCameraSync sends these from the viewport's camera.
        /// </summary>
        public static Task<Reply> SetCamera(uint root, System.Numerics.Vector3 position, System.Numerics.Vector3 forward, System.Numerics.Vector3 up, float fov)
        {
            return Request(Command.CAMERA, w =>
            {
                w.Write(root);
                w.Write((byte)1);
                WriteVector(w, position);
                WriteVector(w, forward);
                WriteVector(w, up);
                w.Write(fov);
            }, CameraTimeoutSeconds);
        }

        /// <summary>Give the game its own camera back after SetCamera. The game also does this by itself when OpenCAGE disconnects.</summary>
        public static Task<Reply> ReleaseCamera(uint root)
        {
            return Request(Command.CAMERA, w =>
            {
                w.Write(root);
                w.Write((byte)0);
            }, CameraTimeoutSeconds);
        }

        /// <summary>How long a request for the game's camera waits. The game answers it on its socket thread, from what it
        /// kept of its last frame, so it answers at once or not at all - and the next is asked for a frame later.</summary>
        public const double CameraGetTimeoutSeconds = 0.5;

        /// <summary>
        /// The game's own camera as of the last frame it drew (never a pose OpenCAGE sent it), while it is running the level
        /// of this root (0: any level). LiveLinkCameraSync asks for it every frame while the viewport follows the game's camera.
        /// </summary>
        public static async Task<GameCamera> GetCamera(uint root)
        {
            Reply reply = await Request(Command.CAMERA_GET, w => w.Write(root), CameraGetTimeoutSeconds);
            GameCamera camera = new GameCamera() { Reply = reply };
            if (!reply.Ok)
                return camera;
            System.Numerics.Vector3? position = null, forward = null, up = null;
            float? fov = null;
            foreach (string line in reply.Message.Split('\n'))
            {
                int equals = line.IndexOf('=');
                if (equals < 0) continue;
                string value = line.Substring(equals + 1).Trim();
                switch (line.Substring(0, equals).Trim())
                {
                    case "position": position = Triple(value); break;
                    case "forward": forward = Triple(value); break;
                    case "up": up = Triple(value); break;
                    case "fov":
                        if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float degrees))
                            fov = degrees;
                        break;
                    case "frame":
                        if (long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long frame))
                            camera.Frame = frame;
                        break;
                }
            }
            if (position.HasValue && forward.HasValue && up.HasValue && fov.HasValue)
            {
                camera.Valid = true;
                camera.Position = position.Value;
                camera.Forward = forward.Value;
                camera.Up = up.Value;
                camera.Fov = fov.Value;
            }
            return camera;
        }

        public sealed class GameCamera
        {
            public Reply Reply;
            /// <summary>The game answered with a whole pose (a reply it could not be read from is not one).</summary>
            public bool Valid;
            /// <summary>World space (CATHODE's axes: Y up); forward and up are unit vectors.</summary>
            public System.Numerics.Vector3 Position;
            public System.Numerics.Vector3 Forward;
            public System.Numerics.Vector3 Up;
            /// <summary>Vertical, in degrees.</summary>
            public float Fov;
            /// <summary>The game's count of the frames it kept its camera from: the same number is the same pose.</summary>
            public long Frame;
        }

        /// <summary>What the game is to do with the CAGEAnimation it drives for OpenCAGE (ANIMATION's mode).</summary>
        public enum AnimationRequest : byte
        {
            /// <summary>Give it back, as it was before it was taken.</summary>
            Release = 0,
            /// <summary>Hold it at a time, running none of its events.</summary>
            Hold = 1,
            /// <summary>Play it from a time, on the game's own clock.</summary>
            Play = 2,
        }

        /// <summary>How the game plays an animation it drives (ANIMATION's flags; a hold ignores them).</summary>
        [Flags]
        public enum AnimationFlags : byte
        {
            None = 0,
            /// <summary>Wrap to 0 rather than stopping just short of the end.</summary>
            Loop = 1,
            /// <summary>Run the event tracks its frames pass over, as the game's own playback does.</summary>
            Events = 2,
        }

        /// <summary>
        /// Have the game hold (or play) a CAGEAnimation at a time, evaluated by the game itself, while it is running the level
        /// of this root (0: any level): in the one placement of its composite at the given path of composite instance entity
        /// ids from the root, or in every placement when there is no path. One animation at a time: one for another target
        /// gives the last back first. The sequence comes back in GetAnimation once a frame has applied the request (and a
        /// play starts again from its time only when the sequence changes). LiveLinkAnimationDrive sends these.
        /// </summary>
        public static async Task<GameAnimation> SetAnimation(uint root, ShortGuid composite, ShortGuid entity, IList<ShortGuid> path, AnimationRequest mode, float time, float rate, AnimationFlags flags, uint sequence)
        {
            if (mode == AnimationRequest.Release)
                return await ReleaseAnimation(root);
            //The game turns away a time or rate that is not a number, even on a hold (where the rate means nothing)
            if (float.IsNaN(time) || float.IsInfinity(time)) time = 0f;
            if (float.IsNaN(rate) || float.IsInfinity(rate)) rate = 1f;
            Reply reply = await Request(Command.ANIMATION, w =>
            {
                w.Write(root);
                w.Write((byte)mode);
                w.Write(composite.AsUInt32);
                w.Write(entity.AsUInt32);
                w.Write(path?.Count ?? 0);
                if (path != null)
                    foreach (ShortGuid step in path)
                        w.Write(step.AsUInt32);
                w.Write(time);
                w.Write(rate);
                w.Write((byte)flags);
                w.Write(sequence);
            }, CameraTimeoutSeconds);
            return ParseAnimation(reply);
        }

        /// <summary>Give the game back the animation SetAnimation took. The game also does this by itself when OpenCAGE disconnects.</summary>
        public static async Task<GameAnimation> ReleaseAnimation(uint root)
        {
            Reply reply = await Request(Command.ANIMATION, w =>
            {
                w.Write(root);
                w.Write((byte)AnimationRequest.Release);
            }, CameraTimeoutSeconds);
            return ParseAnimation(reply);
        }

        /// <summary>
        /// What the game did with the animation it drives for OpenCAGE, as of its last entity frame, while it is running the
        /// level of this root (0: any level). Answered at once on the game's socket thread, like CAMERA_GET.
        /// </summary>
        public static async Task<GameAnimation> GetAnimation(uint root)
        {
            Reply reply = await Request(Command.ANIMATION_GET, w => w.Write(root), CameraGetTimeoutSeconds);
            return ParseAnimation(reply);
        }

        //"state=..\nreason=..\ntime=s\nlength=s\nsequence=N\nframe=N\ninstances=applied/found\nwas_playing=0|1", 4 decimals
        //whatever the game's locale. A reason may hold '=': each line is split at its first.
        private static GameAnimation ParseAnimation(Reply reply)
        {
            GameAnimation animation = new GameAnimation() { Reply = reply };
            if (!reply.Ok || reply.Message == null)
                return animation;
            System.Globalization.CultureInfo invariant = System.Globalization.CultureInfo.InvariantCulture;
            foreach (string line in reply.Message.Split('\n'))
            {
                int equals = line.IndexOf('=');
                if (equals <= 0) continue;
                string value = line.Substring(equals + 1).Trim();
                switch (line.Substring(0, equals).Trim())
                {
                    case "state": animation.State = value; break;
                    case "reason": animation.Reason = value; break;
                    case "time":
                        if (double.TryParse(value, System.Globalization.NumberStyles.Float, invariant, out double time)) animation.Time = time;
                        break;
                    case "length":
                        if (double.TryParse(value, System.Globalization.NumberStyles.Float, invariant, out double length)) animation.Length = length;
                        break;
                    case "sequence":
                        if (uint.TryParse(value, System.Globalization.NumberStyles.Integer, invariant, out uint sequence)) animation.Sequence = sequence;
                        break;
                    case "frame":
                        if (long.TryParse(value, System.Globalization.NumberStyles.Integer, invariant, out long frame)) animation.Frame = frame;
                        break;
                    case "instances":
                        string[] parts = value.Split('/');
                        if (parts.Length == 2
                            && int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, invariant, out int applied)
                            && int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, invariant, out int found))
                        {
                            animation.Applied = applied;
                            animation.Found = found;
                        }
                        break;
                    case "was_playing": animation.WasPlaying = value == "1"; break;
                }
            }
            return animation;
        }

        public sealed class GameAnimation
        {
            public Reply Reply;
            /// <summary>released, waiting, held, playing, ended, not_found, not_animation, disabled, no_data or cinematic; empty when the reply was not one.</summary>
            public string State = "";
            /// <summary>Why, in the game's words (what it waits for, why it cannot drive it).</summary>
            public string Reason = "";
            /// <summary>Seconds into the animation, as the game last applied (or was asked for).</summary>
            public double Time = double.NaN;
            /// <summary>The animation's length in the running game (its anim_length: 10 when unset).</summary>
            public double Length = double.NaN;
            /// <summary>The last request a game frame applied - the one whose time the game shows (0 while nothing is taken). A newer request that waits leaves it as it was.</summary>
            public uint Sequence;
            /// <summary>The game's count of the entity frames that wrote this: the same number is the same answer.</summary>
            public long Frame = -1;
            /// <summary>Placements of the animation the game applied the time to, of those it found.</summary>
            public int Applied = -1;
            public int Found = -1;
            /// <summary>The game was playing the animation itself when it was taken.</summary>
            public bool WasPlaying;

            /// <summary>The game answered with a snapshot (a refusal, or a reply it could not be read from, is not one).</summary>
            public bool Valid => State.Length != 0;

            /// <summary>
            /// Whether a game frame has applied this request: its sequence, in a state that shows it. A frame that only waits,
            /// or could not find the animation, does not count - the sequence names the last request applied, and stays
            /// through a later wait.
            /// </summary>
            public bool Shows(uint sequence)
            {
                return sequence != 0 && Sequence == sequence && (State == "held" || State == "playing" || State == "ended");
            }
        }

        /// <summary>How long a trace request waits. The game answers TRACE and TRACE_GET on its socket thread, at once.</summary>
        public const double TraceTimeoutSeconds = 2.0;

        /// <summary>The most watches one TRACE may carry, and the longest path a watch may have (the game turns more away).</summary>
        public const int MaxTraceWatches = 512;
        public const int MaxTracePath = 64;
        /// <summary>The most watches a game whose runtime utils predate the larger limit takes: it answers more with "Malformed request".</summary>
        public const int OldMaxTraceWatches = 32;

        //A watch's path count meaning "every instance of the composite"
        private const uint TraceAnyInstance = 0xFFFFFFFF;

        /// <summary>A composite instance whose script activity the game is to gather (TRACE).</summary>
        public sealed class TraceWatch
        {
            public ShortGuid Composite;
            /// <summary>Composite instance entity ids from the level's root down to the watched instance (empty: the root itself); null for every instance of the composite.</summary>
            public List<ShortGuid> Path;

            /// <summary>The watch as text, to tell whether a set of watches changed.</summary>
            public string Key => Composite.AsUInt32.ToString("X8") + (Path == null ? "*" : ":" + string.Join(",", Path.Select(o => o.AsUInt32.ToString("X8"))));
        }

        /// <summary>
        /// Have the game gather the script activity in these composite instances (the outputs their entities fire, the
        /// parameters they read and the values they send through data links, the methods called through links) for GetTrace
        /// to take, while it is running the level of this root (0: any
        /// level). It replaces what was traced before, and drops anything not yet taken. No watches stops it; so does a
        /// disconnect. LiveLinkTrace sends these from the flowgraphs on show.
        /// </summary>
        public static Task<Reply> SetTrace(uint root, IList<TraceWatch> watches)
        {
            if (watches != null && watches.Count > MaxTraceWatches)
                return Task.FromResult(new Reply() { Ok = false, Message = "Too many composites to trace at once (at most " + MaxTraceWatches + ")" });
            if (watches != null && watches.Any(o => o.Path != null && o.Path.Count > MaxTracePath))
                return Task.FromResult(new Reply() { Ok = false, Message = "An instance too deep to trace (at most " + MaxTracePath + " steps from the root)" });
            return Request(Command.TRACE, w =>
            {
                w.Write(root);
                if (watches == null || watches.Count == 0)
                {
                    w.Write((byte)0);
                    return;
                }
                w.Write((byte)1);
                w.Write(watches.Count);
                foreach (TraceWatch watch in watches)
                {
                    w.Write(watch.Composite.AsUInt32);
                    if (watch.Path == null)
                    {
                        w.Write(TraceAnyInstance);
                        continue;
                    }
                    w.Write(watch.Path.Count);
                    foreach (ShortGuid step in watch.Path)
                        w.Write(step.AsUInt32);
                }
            }, TraceTimeoutSeconds);
        }

        /// <summary>
        /// Take the script activity the game gathered since the last take (it starts afresh), while it is running the level
        /// of this root (0: any level). Answered at once on the game's socket thread. LiveLinkTrace asks for it several times
        /// a second while flowgraphs show activity.
        /// </summary>
        public static async Task<TraceBatch> GetTrace(uint root)
        {
            Reply reply = await Request(Command.TRACE_GET, w => w.Write(root), TraceTimeoutSeconds);
            return ParseTrace(reply);
        }

        /// <summary>What happened, in a TraceRecord.</summary>
        public enum TraceKind : byte
        {
            /// <summary>An entity fired one of its outputs (following every link out of it).</summary>
            Fired = 1,
            /// <summary>An entity read one of its parameters through a data link.</summary>
            Read = 2,
            /// <summary>An entity sent a value out of one of its parameters through a data link (or looked up what the parameter is linked to).</summary>
            Wrote = 3,
            /// <summary>An entity's method was called through a link: the entity is the one called, the source the caller and the output it fired.</summary>
            Called = 4,
        }

        /// <summary>
        /// One thing the game saw happen since the last take, however many times. An entity is named by the composite holding
        /// it, its id there, and the path of composite instance entity ids from the level's root to the instance holding it.
        /// </summary>
        public sealed class TraceRecord
        {
            public TraceKind Kind;
            /// <summary>How many times it happened since the last take (the game stops counting at uint.MaxValue).</summary>
            public uint Count;
            /// <summary>How long before the game answered it last happened, in milliseconds.</summary>
            public uint AgeMs;
            /// <summary>The composite holding the entity (its owner instance's); 0 when it has no owner.</summary>
            public uint Composite;
            public uint Entity;
            /// <summary>Fired: the output that fired; Read: the parameter read; Wrote: the parameter the value left through; Called: the method called.</summary>
            public uint Pin;
            /// <summary>When the entity is itself a composite instance, the composite it is an instance of; else 0.</summary>
            public uint Self;
            /// <summary>
            /// Read, Wrote and Called: the link's other end the same way - the entity read from, the entity the value went to,
            /// or the caller and the output it fired (all 0 for a call with no caller: OpenCAGE's own "call in game").
            /// </summary>
            public uint SourceComposite;
            public uint SourceEntity;
            public uint SourcePin;
            public uint SourceSelf;
            /// <summary>Instance entity ids from the root to the instance holding the entity (empty: the root).</summary>
            public uint[] Path = new uint[0];
            /// <summary>Read, Wrote and Called: the same for the other end.</summary>
            public uint[] SourcePath = new uint[0];

            /// <summary>The record names the link's other end (every kind but Fired).</summary>
            public bool HasSource => Kind != TraceKind.Fired;
        }

        /// <summary>What TRACE_GET handed over: the script activity the game gathered since the last take.</summary>
        public sealed class TraceBatch
        {
            public Reply Reply;
            /// <summary>The game answered with records that could be read (none, while it traces nothing, is still a batch).</summary>
            public bool Valid;
            /// <summary>The game's count of takes since it started (for logging).</summary>
            public uint Batch;
            /// <summary>Distinct activities the game could not keep since the last take, as its store was full.</summary>
            public uint Dropped;
            public List<TraceRecord> Records = new List<TraceRecord>();

            /// <summary>The game's runtime utils are from before TRACE_GET.</summary>
            public bool Unsupported => IsUnknownRequest(Reply);
        }

        //TRACE_GET's payload format, as this reads it
        private const uint TraceFormat = 1;

        /// <summary>
        /// Read TRACE_GET's answer: u32 format, u32 batch, u32 dropped, u32 record count, then per record u8 kind, u8 path
        /// count, u8 source path count, u8 0, u32 count, u32 age ms, u32 composite, u32 entity, u32 pin, u32 self, (kinds 2-4:
        /// u32 source composite, source entity, source pin, source self), u32 path[], (kinds 2-4: u32 source path[]). A
        /// payload that cannot be read whole gives no records at all (Valid false).
        /// </summary>
        public static TraceBatch ParseTrace(Reply reply)
        {
            TraceBatch batch = new TraceBatch() { Reply = reply };
            if (reply == null || !reply.Ok || reply.Payload == null)
                return batch;
            byte[] payload = reply.Payload;
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(payload, false)))
                {
                    if (payload.Length < 16 || reader.ReadUInt32() != TraceFormat)
                        return batch;
                    batch.Batch = reader.ReadUInt32();
                    batch.Dropped = reader.ReadUInt32();
                    uint count = reader.ReadUInt32();
                    //Every record is at least 28 bytes: a count past that is not to be believed (or allocated for)
                    if (count > (payload.Length - 16) / 28)
                        return batch;
                    List<TraceRecord> records = new List<TraceRecord>((int)count);
                    for (uint i = 0; i < count; i++)
                    {
                        TraceRecord record = new TraceRecord();
                        byte kind = reader.ReadByte();
                        int pathCount = reader.ReadByte();
                        int sourcePathCount = reader.ReadByte();
                        reader.ReadByte();
                        //An unknown kind cannot be stepped over (its size is unknown), nor can a source path on a kind 1
                        if (kind < (byte)TraceKind.Fired || kind > (byte)TraceKind.Called || (kind == (byte)TraceKind.Fired && sourcePathCount != 0))
                            return batch;
                        record.Kind = (TraceKind)kind;
                        record.Count = reader.ReadUInt32();
                        record.AgeMs = reader.ReadUInt32();
                        record.Composite = reader.ReadUInt32();
                        record.Entity = reader.ReadUInt32();
                        record.Pin = reader.ReadUInt32();
                        record.Self = reader.ReadUInt32();
                        //Kinds 2-4 all name the link's other end, laid out the same way
                        if (record.HasSource)
                        {
                            record.SourceComposite = reader.ReadUInt32();
                            record.SourceEntity = reader.ReadUInt32();
                            record.SourcePin = reader.ReadUInt32();
                            record.SourceSelf = reader.ReadUInt32();
                        }
                        record.Path = ReadIds(reader, pathCount);
                        if (record.HasSource)
                            record.SourcePath = ReadIds(reader, sourcePathCount);
                        records.Add(record);
                    }
                    batch.Records = records;
                    batch.Valid = true;
                }
            }
            catch (EndOfStreamException)
            {
                //Cut short: none of it is trusted
                batch.Records = new List<TraceRecord>();
                batch.Valid = false;
            }
            return batch;
        }

        private static uint[] ReadIds(BinaryReader reader, int count)
        {
            uint[] ids = new uint[count];
            for (int i = 0; i < count; i++)
                ids[i] = reader.ReadUInt32();
            return ids;
        }

        /// <summary>Whether a reply is from a game whose runtime utils are from before a command (they answer "Unknown request N").</summary>
        public static bool IsUnknownRequest(Reply reply)
        {
            return reply != null && !reply.Ok && reply.Message != null && reply.Message.StartsWith("Unknown request");
        }

        private static void WriteVector(BinaryWriter writer, System.Numerics.Vector3 vector)
        {
            writer.Write(vector.X);
            writer.Write(vector.Y);
            writer.Write(vector.Z);
        }

        /// <summary>
        /// The instance of the displayed composite that the user navigated to, as composite instance entity ids from the
        /// level's root - or null when the display was not reached from the root (then calls go to every instance).
        /// </summary>
        public static List<ShortGuid> InstancePath(CompositeDisplay display, Commands commands)
        {
            Composite root = commands?.EntryPoints?[0];
            if (display?.Composite == null || root == null)
                return null;
            List<Composite> composites = display.Path.AllComposites;
            if (composites.Count == 0)
                return display.Composite == root ? new List<ShortGuid>() : null;
            if (composites[0] != root)
                return null;
            return display.Path.GetPath();
        }

        /// <summary>The methods an entity can be sent: its method pins, inherited ones included (ToString gives the name).</summary>
        public static List<ShortGuid> Methods(Commands commands, Entity entity, Composite composite)
        {
            List<ShortGuid> methods = new List<ShortGuid>();
            if (commands == null || entity == null)
                return methods;
            foreach ((ShortGuid id, ParameterVariant variant, DataType type) in commands.Utils.GetAllParameters(entity, composite))
                if (variant == ParameterVariant.METHOD_PIN && !methods.Contains(id))
                    methods.Add(id);
            return methods;
        }
        #endregion

        #region AUTOMATIC PUSHING
        //Script edits always go to the game as they are made while it is connected (there is no option to hold them: the
        //composite's Resync button sends one again when the game has fallen out of step)

        private static readonly HashSet<Composite> _queued = new HashSet<Composite>();
        private static System.Windows.Forms.Timer _timer;
        private static bool _flushing = false;
        //The composite being sent now, for IsQueuedOrPushing (taken off the queue while it goes)
        private static Composite _pushing;

        /// <summary>Starts following edits. Call once, on the UI thread.</summary>
        public static void Initialise()
        {
            if (_timer != null)
                return;
            //Edits come in bursts (a drag, a paste, an undo of several steps): send once they settle
            _timer = new System.Windows.Forms.Timer() { Interval = 350 };
            _timer.Tick += (s, e) => { _timer.Stop(); Flush(); };

            //Every edit ends in DirtyTracker, including the flowgraph's link edits that raise nothing else
            DirtyTracker.OnDirty += () => QueueDisplayed();
            //Edits that name their entity may be to a composite other than the one on show (the MCP tools, undo)
            Singleton.OnEntityAdded += entity => QueueEntity(entity);
            Singleton.OnEntityDeleted += entity => QueueEntity(entity);
            Singleton.OnEntityMoved += (transform, entity) => QueueEntity(entity);
            Singleton.OnEntityParameterModified += (entity, parameter, removed) => QueueEntity(entity);
            Singleton.OnEntityDeletePending += (entity, composite) => Queue(composite);
            //Refactors and MCP edits (and their undo/redo) say which composites they changed
            Singleton.OnCompositesModified += composites => { foreach (Composite composite in composites) Queue(composite); };
            //A level load or save starts from what is on disk: nothing queued before it still applies
            Singleton.OnLevelLoaded += content => { lock (_queued) _queued.Clear(); ForgetSent(); };
        }

        /// <summary>
        /// Send a composite now (the composite's Resync button, MCP push), after any push still in flight, and remember what
        /// was sent (so the auto push knows what the game has). Turned away because the level is still starting, it is sent
        /// again by the auto push. UI thread.
        /// </summary>
        public static async Task<Reply> PushNow(Commands commands, Composite composite)
        {
            await WaitForPushes();
            _flushing = true;
            Reply reply;
            try
            {
                reply = await PushChecked(commands, composite, false);
            }
            finally
            {
                _flushing = false;
            }
            Pushed?.Invoke(composite, reply);
            if (!reply.Ok && IsLoadingRefusal(reply.Message))
                Queue(composite);
            return reply;
        }

        /// <summary>
        /// Whether edits to a composite are waiting to go to the game, or on their way: queued by the auto push, or being
        /// sent now. UI thread.
        /// </summary>
        public static bool IsQueuedOrPushing(Composite composite)
        {
            if (composite == null)
                return false;
            if (_pushing == composite)
                return true;
            lock (_queued) return _queued.Contains(composite);
        }

        /// <summary>
        /// Call a method once the edits made before it have reached the game: a push still in flight goes first (the game
        /// holds them while a level starts), then everything the auto push has queued - so a call to an entity just added
        /// reaches it, and a call cannot overtake an edit. UI thread.
        /// </summary>
        public static async Task<Reply> CallAfterEdits(Commands commands, Composite composite, Entity entity, ShortGuid method, IList<ShortGuid> instancePath)
        {
            await WaitForPushes();
            _flushing = true;
            List<(Composite composite, Reply reply)> failed;
            try
            {
                failed = await PushQueued(commands);
            }
            finally
            {
                _flushing = false;
            }
            //Its composite held back for the level to start: the call would be too, and could arrive before the edits
            foreach ((Composite pushed, Reply reply) in failed)
                if (pushed == composite && IsLoadingRefusal(reply.Message))
                    return new Reply() { Ok = false, Message = "Not called, as the edits made before it have not reached the game yet (they are sent again by themselves) - " + reply.Message };
            return await CallMethod(RootOf(commands), composite, entity, method, instancePath);
        }

        //Pushes go one at a time, in order: this waits for one in flight. Everything that sets _flushing runs on the UI
        //thread, so the first to resume takes it before another can.
        private static async Task WaitForPushes()
        {
            while (_flushing)
                await Task.Delay(50);
        }

        private static void QueueDisplayed()
        {
            Queue(Singleton.Editor?.CompositeDisplay?.Composite);
        }

        private static void QueueEntity(Entity entity)
        {
            if (entity == null || !Connected)
                return;
            Commands commands = Singleton.Editor?.CompositeDisplay?.Content?.Level?.Commands;
            Composite displayed = Singleton.Editor?.CompositeDisplay?.Composite;
            if (displayed != null && displayed.GetEntityByID(entity.shortGUID) == entity)
            {
                Queue(displayed);
                return;
            }
            //Deleted entities are no longer in their composite; the pending-delete event has queued it already
            Composite owner = commands?.Entries.FirstOrDefault(o => o.GetEntityByID(entity.shortGUID) == entity);
            Queue(owner);
        }

        private static void Queue(Composite composite)
        {
            if (composite == null || !Connected || _timer == null)
                return;
            lock (_queued) _queued.Add(composite);
            //The timer is a UI one: restart it there
            if (Singleton.Editor != null && Singleton.Editor.InvokeRequired)
                Singleton.Editor.BeginInvoke(new Action(() => { _timer.Stop(); _timer.Start(); }));
            else
            {
                _timer.Stop();
                _timer.Start();
            }
        }

        private static async void Flush()
        {
            if (_flushing)
            {
                //A push is still in flight: try again once it is done
                _timer.Start();
                return;
            }
            Commands commands = Singleton.Editor?.CompositeDisplay?.Content?.Level?.Commands;
            _flushing = true;
            List<(Composite composite, Reply reply)> failed;
            try
            {
                failed = await PushQueued(commands);
            }
            finally
            {
                _flushing = false;
            }
            if (failed.Any(o => IsLoadingRefusal(o.reply.Message)))
            {
                _timer.Stop();
                await Task.Delay(3000);
                _timer.Start();
            }
        }

        /// <summary>
        /// Push everything queued, in order; composites the game turned away because its level is still starting go back
        /// in the queue. Returns the pushes that failed. The caller holds _flushing.
        /// </summary>
        private static async Task<List<(Composite composite, Reply reply)>> PushQueued(Commands commands)
        {
            List<(Composite, Reply)> failed = new List<(Composite, Reply)>();
            List<Composite> composites;
            lock (_queued)
            {
                composites = _queued.ToList();
                _queued.Clear();
            }
            if (composites.Count == 0 || commands == null || !Connected)
                return failed;
            foreach (Composite composite in composites)
            {
                if (!commands.Entries.Contains(composite))
                    continue; //deleted since
                Reply reply = await PushChecked(commands, composite, true);
                if (reply == null)
                    continue;
                Activity?.Invoke(reply.Message);
                Pushed?.Invoke(composite, reply);
                if (reply.Ok)
                    continue;
                failed.Add((composite, reply));
                //The game holds edits while its level starts, and turns them away if that takes long: keep it for later
                if (IsLoadingRefusal(reply.Message))
                    Queue(composite);
            }
            return failed;
        }

        /// <summary>Whether the game turned a request away only because its level is still loading (worth sending again).</summary>
        public static bool IsLoadingRefusal(string message)
        {
            //The game's EditsMustWait reasons: a level starting (loading, intro, opening cutscene), the pause menu, a debugger
            return message != null && (message.Contains("still starting") || message.Contains("is paused") || message.Contains("scripts are stopped") || message.Contains("still loading"));
        }

        //What was last sent of each composite (a hash of its image), so edits that do not touch its scripting - a
        //material, a model - send nothing. Forgotten when the connection or the level changes.
        private static readonly Dictionary<Composite, string> _lastSent = new Dictionary<Composite, string>();

        /// <summary>
        /// Push, if the game is running this level: sent to another level the edit would land in the wrong place. With
        /// onlyIfChanged, a composite whose image is what was last sent is skipped (null).
        /// </summary>
        private static async Task<Reply> PushChecked(Commands commands, Composite composite, bool onlyIfChanged)
        {
            string name = EditorUtils.GetCompositeName(composite);
            byte[] image;
            List<int> relocations;
            try
            {
                CompileIfShown(composite); //links made or broken on the flowgraph are in its pages until compiled
                image = commands.WriteLiveLinkImage(composite, out relocations);
            }
            catch (Exception ex)
            {
                return new Reply() { Ok = false, Message = "Live link: could not write " + name + " for the game: " + ex.Message };
            }
            string hash;
            using (System.Security.Cryptography.SHA1 sha = System.Security.Cryptography.SHA1.Create())
                hash = Convert.ToBase64String(sha.ComputeHash(image));
            lock (_lastSent)
            {
                if (onlyIfChanged && _lastSent.TryGetValue(composite, out string sent) && sent == hash)
                    return null;
            }

            //The game checks it is running this level (the root sent with it), so an edit cannot land in another one
            Reply reply;
            _pushing = composite;
            try
            {
                reply = await PushImage(RootOf(commands), composite.shortGUID, image, relocations);
            }
            finally
            {
                _pushing = null;
            }
            if (reply.Ok)
                lock (_lastSent) _lastSent[composite] = hash;
            return new Reply() { Ok = reply.Ok, Message = "Live link: " + name + " - " + reply.Message, Bytes = image.Length };
        }

        private static void ForgetSent()
        {
            lock (_lastSent) _lastSent.Clear();
        }
        #endregion
    }
}
