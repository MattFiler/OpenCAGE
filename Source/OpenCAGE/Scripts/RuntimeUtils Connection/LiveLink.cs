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
        }

        public sealed class Reply
        {
            public bool Ok;
            public string Message;
            /// <summary>For a push: the size of the image sent.</summary>
            public int Bytes;
            /// <summary>The game answered (a refusal is an answer too); false when it could not be asked, or did not answer in time.</summary>
            public bool Answered;
        }

        public static bool Connected => Send.Connected;

        /// <summary>The connection opened or closed. Raised on any thread.</summary>
        public static event Action ConnectionChanged;

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

                    TaskCompletionSource<Reply> request;
                    lock (_lock)
                    {
                        if (!_pending.TryGetValue(id, out request))
                            return;
                        _pending.Remove(id);
                    }
                    request.TrySetResult(new Reply() { Ok = ok, Message = message, Answered = true });
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
            if (!reply.Ok && IsLoadingRefusal(reply.Message))
                Queue(composite);
            return reply;
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
            Reply reply = await PushImage(RootOf(commands), composite.shortGUID, image, relocations);
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
