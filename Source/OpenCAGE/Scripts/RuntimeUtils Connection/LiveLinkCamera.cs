using CATHODE;
using CATHODE.Scripting;
using OpenCAGE.DockPanels;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.RuntimeUtilsConnection
{
    /// <summary>
    /// The Live Link Camera menu on the viewport's toolbar, which works while Live Link is connected:
    ///
    /// "Sync viewport camera to game" (ViewportToGame): the game's camera follows the viewport's. The viewer streams its
    /// camera (VIEWER_CAMERA_POSE, already in the game's world space) and each pose goes on to the game (LiveLink CAMERA),
    /// which renders from it and streams in the zones around it.
    ///
    /// "Sync game camera to viewport" (GameToViewport): the viewport's camera follows the game's. The viewer is told to
    /// follow (camera_follows_game), which takes its own camera controls away, and the game's camera is asked for about
    /// every frame (LiveLink CAMERA_GET) and put on the viewer's (VIEWPORT_SET_CAMERA) whenever it has moved.
    ///
    /// Poses arrive on the viewer socket's thread, up to about 30 a second, and are only worth anything while they are the
    /// latest: they must not queue behind the UI thread (a populate, a save) or behind each other. So only the latest is
    /// kept, and one pump on the thread pool sends it, one request in flight at a time - a pose that arrives meanwhile
    /// replaces the one waiting. Following the game is the same the other way round: one loop on the thread pool, one
    /// request in flight. Nothing here is sent from the UI thread.
    /// </summary>
    public static class LiveLinkCameraSync
    {
        /// <summary>The Live Link Camera menu's choices (the LiveLinkCameraMode setting).</summary>
        public enum CameraMode
        {
            /// <summary>No live link camera control: the game's camera and the viewport's go their own ways.</summary>
            Disabled = 0,
            /// <summary>"Sync viewport camera to game": the game's camera follows the viewport's.</summary>
            ViewportToGame = 1,
            /// <summary>"Sync game camera to viewport": the viewport's camera follows the game's, and cannot be moved by hand meanwhile.</summary>
            GameToViewport = 2,
        }

        /// <summary>A camera: the viewport's as the viewer reported it, or the game's as it answered - game world space, vertical field of view in degrees.</summary>
        public sealed class Pose
        {
            public Vector3 Position;
            public Vector3 Forward;
            public Vector3 Up;
            public float Fov;
            /// <summary>The viewer is showing the level's root, so the pose is in the level's space at all.</summary>
            public bool InLevelSpace;
            /// <summary>Which level (its root composite): 0 from a viewer from before this, taken to be the level open here.</summary>
            public uint Root;
            public DateTime Received;
            /// <summary>Not a pose: the viewer went away (see OnViewerGone).</summary>
            internal bool NoViewer;
            /// <summary>Which spell of following it arrived in: one from before the sync was last switched on is stale.</summary>
            internal int Session;

            public bool SameAs(Pose other)
            {
                return other != null && other.NoViewer == NoViewer && other.InLevelSpace == InLevelSpace && other.Root == Root
                    && other.Position == Position && other.Forward == Forward && other.Up == Up && other.Fov == Fov;
            }

            /// <summary>Where <paramref name="other"/> is, as near as the eye can tell (a camera that has not moved reports
            /// the same pose give or take the last digit).</summary>
            public bool CloseTo(Pose other)
            {
                return other != null && Vector3.DistanceSquared(other.Position, Position) <= PositionEpsilon * PositionEpsilon
                    && Vector3.DistanceSquared(other.Forward, Forward) <= DirectionEpsilon * DirectionEpsilon
                    && Vector3.DistanceSquared(other.Up, Up) <= DirectionEpsilon * DirectionEpsilon
                    && Math.Abs(other.Fov - Fov) <= FovEpsilon;
            }
        }

        private const float PositionEpsilon = 0.0005f;  //half a millimetre
        private const float DirectionEpsilon = 0.0001f; //along a unit vector: under a hundredth of a degree
        private const float FovEpsilon = 0.01f;         //degrees

        /// <summary>A LiveLinkCameraMode setting as a mode (anything unknown is Disabled).</summary>
        public static CameraMode NormaliseMode(int mode)
        {
            return mode == (int)CameraMode.ViewportToGame || mode == (int)CameraMode.GameToViewport ? (CameraMode)mode : CameraMode.Disabled;
        }

        /// <summary>The Live Link Camera menu's choice (remembered across sessions), whether or not Live Link is connected.</summary>
        public static CameraMode WantedMode => NormaliseMode(SettingsManager.GetInteger(Settings.LiveLinkCameraMode));

        /// <summary>
        /// ViewportToGame wanted, and the live link connected: the viewer streams its camera and the game is sent it. As last
        /// worked out by Refresh, so it can be read from any thread.
        /// </summary>
        public static bool Enabled => _enabled;

        /// <summary>Whether the viewer should send VIEWER_CAMERA_POSE (its stream_camera_pose setting).</summary>
        public static bool StreamPose => _enabled;

        /// <summary>
        /// GameToViewport wanted, and the live link connected: the viewer's camera follows the game's, and the viewer is told
        /// so (its camera_follows_game setting). Never true together with Enabled. As last worked out by Refresh.
        /// </summary>
        public static bool CameraFollowsGame => _followGame;

        /// <summary>The last pose the viewer sent, whether or not the game was sent it; null before the first.</summary>
        public static Pose LastPose => Volatile.Read(ref _received);

        /// <summary>The game's camera as it was last asked for while the viewport follows it; null before the first.</summary>
        public static Pose LastGamePose => Volatile.Read(ref _gamePose);

        /// <summary>How many poses the viewer has sent (a tool waits for the next).</summary>
        public static int PosesReceived => Volatile.Read(ref _posesReceived);

        /// <summary>The last line put in the status bar about the game's camera; null before the first.</summary>
        public static string LastStatus => Volatile.Read(ref _lastStatus);

        private static volatile bool _enabled;
        //The root composite of the level open here, sent with every pose so the game follows only on the level it belongs
        //to. Read on the UI thread (the level is only safe to read there) and kept for the pump; 0 while no level is loaded.
        private static volatile uint _root;
        //Bumped whenever Enabled or the root changes: the pump then starts over (what it sent was for the state before)
        private static int _generation;
        //Bumped whenever it is switched on: poses stamped with an older one were streamed before, and are stale now
        private static int _session;

        private static Pose _pending;   //the latest pose from the viewer, until the pump takes it
        private static Pose _received;  //the latest pose from the viewer, kept for LastPose
        private static int _posesReceived;
        private static string _lastStatus;
        private static int _pumping;    //1 while a pump runs: there is only ever one

        //The pump's own: only the one pump running touches these
        private static int _pumpGeneration = -1;
        private static Pose _latest;       //the latest pose the pump has taken
        private static Pose _sent;         //what the game was last sent and took
        private static Pose _refused;      //a pose the game turned away (or did not answer): asked about again after Reassert
        private static DateTime _refusedAt = DateTime.MinValue;
        private static DateTime _askedAt = DateTime.MinValue; //when the game was last sent a pose
        private static bool _following;    //the game may be rendering from a pose of ours: it is given its camera back when this stops
        private static bool _unsupported;  //the game's runtime utils are from before CAMERA
        private static string _reported;   //what the status bar was last told, so each change is said once

        //After the game turns a pose away, the next waits this long: the reason (another level running, no level) does not
        //change as the camera moves, and a request a frame is not worth making to hear it again
        private static readonly TimeSpan RefusalBackoff = TimeSpan.FromSeconds(1);

        //The game can change while the viewport stays still - it finishes loading the level, loads another and comes back,
        //reloads - and the viewer only sends a pose when its camera moves: so the latest pose, taken or turned away, is
        //sent again this often while the sync is on. It costs one small request, which the game answers at once.
        private static readonly TimeSpan Reassert = TimeSpan.FromSeconds(2);
        private static int _reassertScheduled; //1 while a Kick is waiting to do that

        //The viewport following the game's camera (GameToViewport). _followGame is the viewer's camera_follows_game; it
        //changes, and a camera packet is queued for the viewer, only under _followLock - so no pose can reach the viewer
        //after the settings packet that turned following off, or before the one that turned it on.
        private static readonly object _followLock = new object();
        private static volatile bool _followGame;
        //The viewport shows the level's root composite, as the editor has it on screen (the viewer shows what the editor
        //does): read on the UI thread, since a pose from the game means nothing to any other composite's space
        private static volatile bool _showsRoot;
        //Bumped whenever following starts, and when the viewer goes: the viewer must answer afresh before it is sent poses
        private static int _followSession;
        //The spell of following the viewer last answered in (its VIEWER_CAMERA_POSE when told to follow)
        private static int _helloSession;
        private static int _followPumping;  //1 while the follow loop runs: there is only ever one
        private static Pose _gamePose;      //the game's camera as last asked for, kept for LastGamePose
        private static string _followReported; //as _reported, for the follow loop

        //The game's camera is asked for about this often (a frame at 30 fps), one request in flight
        private static readonly TimeSpan FollowInterval = TimeSpan.FromMilliseconds(30);
        //A viewer that can follow answers being told to within a frame or two of having the level: this long means it cannot
        private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(3);

        /// <summary>Starts following level loads and the composite on screen. Call once, on the UI thread.</summary>
        public static void Initialise()
        {
            //A new level is a new root: poses from here on are for it (the game refuses them while it runs another)
            Singleton.OnLevelLoaded += content => RefreshOnUiThread();
            //The viewport follows the game only while it shows the level's root composite
            Singleton.OnCompositeSelected += composite => RefreshOnUiThread();
        }

        /// <summary>
        /// Work Enabled and CameraFollowsGame out again, after the Live Link Camera menu, Live Link connecting or
        /// disconnecting, a level load or another composite opening: the viewer is told whether to stream its camera or to
        /// follow the game's, the pump sends what the game should now have (a release, when the game's camera stops
        /// following while connected - a game that disconnects lets go of the camera by itself), and the follow loop starts
        /// or stops. UI thread.
        /// </summary>
        public static void Refresh()
        {
            CameraMode mode = WantedMode;
            bool connected = LiveLink.Connected;
            bool enabled = mode == CameraMode.ViewportToGame && connected;
            bool follow = mode == CameraMode.GameToViewport && connected;
            uint root = ReadRoot();
            _showsRoot = ReadShowsRoot(root);
            bool enabledChanged = enabled != _enabled;
            bool followChanged = follow != _followGame;
            if (!enabledChanged && !followChanged && root == _root)
                return;

            if (enabledChanged && enabled)
                Interlocked.Increment(ref _session);
            _root = root;
            _enabled = enabled;
            Interlocked.Increment(ref _generation);

            //The settings ride every packet too, so a viewer that connects later is told as well. Streaming switched on, the
            //viewer answers with where its camera is now, which is what the game is sent first; following switched on, it
            //answers the same way, which says it can follow.
            lock (_followLock)
            {
                if (followChanged && follow)
                    Interlocked.Increment(ref _followSession);
                _followGame = follow;
                if (enabledChanged || followChanged)
                    UnityConnection.Send.SendSettingsPacket();
            }
            if (follow)
                StartFollowing();
            Kick();
        }

        private static void RefreshOnUiThread()
        {
            //Without a handle yet (the level OpenCAGE was started with) there is no UI thread to go to, and InvokeRequired
            //would say there is no need: the editor refreshes this once it is shown (OnLiveLinkConnectionChanged)
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed || !editor.IsHandleCreated)
                return;
            if (!editor.InvokeRequired)
            {
                Refresh();
                return;
            }
            try
            {
                editor.BeginInvoke(new Action(Refresh));
            }
            catch
            {
                //Closing
            }
        }

        /// <summary>The root of the level open here, or 0 while none is loaded. UI thread.</summary>
        private static uint ReadRoot()
        {
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            return commands != null && commands.Loaded ? LiveLink.RootOf(commands) : 0;
        }

        /// <summary>
        /// Whether the composite on screen is the level's root, or one stepped into from it: then the viewer shows the root,
        /// in the level's (the game's) space. UI thread.
        /// </summary>
        private static bool ReadShowsRoot(uint root)
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (root == 0 || display == null || display.IsDisposed || display.Composite == null)
                return false;
            Composite top = display.Path?.AllComposites.FirstOrDefault() ?? display.Composite;
            return top.shortGUID.AsUInt32 == root;
        }

        /// <summary>A VIEWER_CAMERA_POSE from the viewer. Called on the viewer socket's thread, as it arrives.</summary>
        public static void OnViewerPose(UnityConnection.Packet packet)
        {
            Pose pose = new Pose()
            {
                Position = packet.camera_position,
                Forward = packet.camera_forward,
                Up = packet.camera_up,
                Fov = packet.camera_fov,
                InLevelSpace = packet.camera_in_level_space,
                Root = packet.camera_level_root,
                Received = DateTime.UtcNow,
                Session = Volatile.Read(ref _session),
            };
            Volatile.Write(ref _received, pose);
            Interlocked.Increment(ref _posesReceived);

            //While the viewport is to follow the game's camera, a pose is the viewer's answer to being told to: it can
            if (_followGame)
            {
                Volatile.Write(ref _helloSession, Volatile.Read(ref _followSession));
                return;
            }
            Interlocked.Exchange(ref _pending, pose);
            Kick();
        }

        /// <summary>
        /// The viewer disconnected (closed, restarted, turned off): with no viewport there is nothing to follow, and the
        /// toolbar that would turn this off is gone with it, so the game is given its camera back. Following starts again
        /// with the first pose from the next viewer - and the next viewer, told to follow the game's camera when it connects,
        /// must say it can before it is sent the game's. Any thread.
        /// </summary>
        public static void OnViewerGone()
        {
            Interlocked.Increment(ref _followSession);
            Interlocked.Exchange(ref _pending, new Pose() { NoViewer = true, Received = DateTime.UtcNow, Session = Volatile.Read(ref _session) });
            Kick();
        }

        private static void Kick()
        {
            if (Interlocked.CompareExchange(ref _pumping, 1, 0) != 0)
                return;
            Task.Run(() => Pump());
        }

        private static bool HasWork()
        {
            return Volatile.Read(ref _pending) != null || Volatile.Read(ref _generation) != _pumpGeneration;
        }

        private static async Task Pump()
        {
            try
            {
                while (true)
                {
                    if (await Step())
                        continue;

                    //Nothing left to do: stand down, then look once more - something that came in just before found the
                    //pump still running, so did not start another
                    Volatile.Write(ref _pumping, 0);
                    if (!HasWork() || Interlocked.CompareExchange(ref _pumping, 1, 0) != 0)
                        return;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Live Link", "The game camera sync failed: " + ex.Message);
                Volatile.Write(ref _pumping, 0);
            }
        }

        /// <summary>Send the game the one thing it should be sent next, if there is one. True when it did something (there may be more).</summary>
        private static async Task<bool> Step()
        {
            Pose taken = Interlocked.Exchange(ref _pending, null);
            if (taken != null)
                _latest = taken;

            int generation = Volatile.Read(ref _generation);
            if (generation != _pumpGeneration)
            {
                //What was sent, and what was turned away, was for the state before: the game is asked afresh
                _pumpGeneration = generation;
                _sent = null;
                _refused = null;
                _refusedAt = DateTime.MinValue;
                _unsupported = false;
                _reported = null;
            }

            bool connected = LiveLink.Connected;
            bool enabled = _enabled && connected;
            uint root = _root;
            //Streamed before it was last switched on: where the camera was then, not where it is
            if (_latest != null && _latest.Session != Volatile.Read(ref _session))
                _latest = null;
            Pose pose = _latest;

            //A pose from another level than the one open here (the viewer and OpenCAGE load a new level each in their own
            //time) is no pose for this one: it would put the camera wherever those coordinates land in this level
            bool otherLevel = pose != null && pose.Root != 0 && pose.Root != root;
            if (!enabled || pose == null || pose.NoViewer || !pose.InLevelSpace || root == 0 || otherLevel)
            {
                if (_following)
                {
                    //One release, and nothing more until there is a pose to follow again
                    _following = false;
                    _sent = null;
                    if (connected)
                    {
                        LiveLink.Reply reply = await LiveLink.ReleaseCamera(root);
                        Report(reply.Ok ? "Live Link: " + reply.Message : "Live Link: could not give the game its camera back - " + reply.Message);
                        return true;
                    }
                }
                if (enabled && pose != null && !pose.NoViewer && !pose.InLevelSpace)
                    Report("Live Link: the game camera follows the viewport only while it shows the level's root composite");
                return false;
            }

            if (_unsupported)
                return false;
            //Sent already, taken or turned away: asked about again once Reassert is up (see there), and in the meantime only
            //a new pose or a change of state sends anything
            if (pose == _refused || pose.SameAs(_sent))
            {
                TimeSpan due = _askedAt + Reassert - DateTime.UtcNow;
                if (due > TimeSpan.Zero)
                {
                    ScheduleReassert(due);
                    return false;
                }
            }

            //Turned away a moment ago: wait out the rest of that, then send whatever is latest by then
            TimeSpan wait = _refusedAt + RefusalBackoff - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait);
                return true;
            }

            _askedAt = DateTime.UtcNow;
            LiveLink.Reply sent = await LiveLink.SetCamera(root, pose.Position, pose.Forward, pose.Up, pose.Fov);
            if (LiveLink.IsUnknownRequest(sent))
            {
                _unsupported = true;
                Report("Live Link: the game's runtime utils are too old to follow the viewport camera - launch the game from OpenCAGE again to update them");
                return true;
            }

            //Not answered, it may have been taken all the same (a game that is slow for a moment): given back either way
            if (sent.Ok || !sent.Answered)
                _following = true;
            if (sent.Ok)
            {
                _sent = pose;
                _refused = null;
                _refusedAt = DateTime.MinValue;
                Report("Live Link: " + sent.Message);
            }
            else
            {
                _refused = pose;
                _refusedAt = DateTime.UtcNow;
                Report("Live Link: the game camera cannot follow the viewport - " + sent.Message);
            }
            return true;
        }

        /// <summary>
        /// Kick the pump once <paramref name="due"/> is up, so it sends the latest pose again (Reassert) - without a pump
        /// waiting on it, so a new pose in the meantime is sent at once. Only one is ever waiting; each pump that finds the
        /// pose already sent schedules the next, so none is lost. Pump thread.
        /// </summary>
        private static void ScheduleReassert(TimeSpan due)
        {
            if (Interlocked.Exchange(ref _reassertScheduled, 1) != 0)
                return;
            Task.Delay(due).ContinueWith(_ =>
            {
                Volatile.Write(ref _reassertScheduled, 0);
                Kick();
            });
        }

        #region FOLLOWING THE GAME
        private static void StartFollowing()
        {
            if (Interlocked.CompareExchange(ref _followPumping, 1, 0) != 0)
                return;
            Task.Run(() => Follow());
        }

        /// <summary>
        /// Put the game's camera on the viewport's for as long as CameraFollowsGame: once the viewer has said it can follow,
        /// the game is asked for its camera about every frame, one request in flight, and the viewer is sent it whenever it
        /// has moved (and every Reassert besides, in case the viewer has repopulated since). Thread pool.
        /// </summary>
        private static async Task Follow()
        {
            bool failed = false;
            try
            {
                int session = 0;
                DateTime helloWait = DateTime.UtcNow;
                bool toldTooOld = false;
                bool stopped = false;       //the game cannot answer at all (its runtime utils, its config): nothing more this spell
                Pose sent = null;           //what the viewer was last sent, while it still holds it
                DateTime sentAt = DateTime.MinValue;
                uint sentRoot = 0;

                while (_followGame)
                {
                    int current = Volatile.Read(ref _followSession);
                    if (current != session)
                    {
                        //Following afresh (switched on again, or another viewer): that viewer has to answer, and be sent the lot
                        session = current;
                        helloWait = DateTime.UtcNow;
                        toldTooOld = false;
                        stopped = false;
                        sent = null;
                        _followReported = null;
                    }

                    //Told to follow, the viewer answers with a pose. One from before this never does - and would take a camera
                    //packet for a resync - so it is sent nothing. It answers once it has a level, so the wait counts from then.
                    if (Volatile.Read(ref _helloSession) != session)
                    {
                        if (!UnityConnection.Send.Connected || !UnityConnection.ViewerResourceSync.ViewerReady)
                            helloWait = DateTime.UtcNow;
                        else if (!toldTooOld && DateTime.UtcNow - helloWait > HelloTimeout)
                        {
                            toldTooOld = true;
                            ReportFollow("Live Link: the viewport is too old to follow the game camera (update the level viewer)");
                        }
                        await Task.Delay(100);
                        continue;
                    }

                    //Nothing to put the camera in while the viewer loads a level, and nothing it means in another composite's space
                    uint root = _root;
                    if (stopped || root == 0 || !UnityConnection.ViewerResourceSync.ViewerReady || !_showsRoot)
                    {
                        if (!stopped && root != 0 && !_showsRoot)
                            ReportFollow("Live Link: the viewport follows the game camera only while it shows the level's root composite");
                        sent = null;
                        await Task.Delay(200);
                        continue;
                    }

                    DateTime asked = DateTime.UtcNow;
                    LiveLink.GameCamera camera = await LiveLink.GetCamera(root);
                    TimeSpan wait = FollowInterval - (DateTime.UtcNow - asked);
                    if (camera.Reply.Ok && camera.Valid)
                    {
                        Pose pose = new Pose()
                        {
                            Position = camera.Position,
                            Forward = camera.Forward,
                            Up = camera.Up,
                            Fov = camera.Fov,
                            InLevelSpace = true,
                            Root = root,
                            Received = DateTime.UtcNow,
                        };
                        Volatile.Write(ref _gamePose, pose);
                        //Moved, or not sent for a while (the viewer may have loaded, or repopulated the root, since)
                        if (!pose.CloseTo(sent) || root != sentRoot || DateTime.UtcNow - sentAt > Reassert)
                        {
                            if (SendToViewer(pose, session))
                            {
                                sent = pose;
                                sentAt = DateTime.UtcNow;
                                sentRoot = root;
                            }
                        }
                        ReportFollow("Live Link: the viewport follows the game camera");
                    }
                    else if (camera.Reply.Ok)
                    {
                        ReportFollow("Live Link: the viewport cannot follow the game camera - could not read it from \"" + camera.Reply.Message.Replace('\n', ' ') + "\"");
                        wait = RefusalBackoff;
                    }
                    else if (LiveLink.IsUnknownRequest(camera.Reply))
                    {
                        stopped = true;
                        ReportFollow("Live Link: the game's runtime utils are too old for the viewport to follow the game camera - launch the game from OpenCAGE again to update them");
                    }
                    else
                    {
                        //Another level running, no level, nothing drawn yet: said once, and asked about less often until it changes.
                        //Camera sync switched off in the game's config does not change while it runs.
                        if (camera.Reply.Message != null && camera.Reply.Message.Contains("LiveLinkCamera=0"))
                            stopped = true;
                        ReportFollow("Live Link: the viewport cannot follow the game camera - " + camera.Reply.Message);
                        sent = null;
                        wait = RefusalBackoff;
                    }
                    if (wait > TimeSpan.Zero)
                        await Task.Delay(wait);
                }
            }
            catch (Exception ex)
            {
                failed = true;
                Debug.Log("Live Link", "Following the game camera failed: " + ex.Message);
                ReportFollow("Live Link: following the game camera failed - " + ex.Message);
            }
            finally
            {
                Volatile.Write(ref _followPumping, 0);
            }
            //Switched on again just as this stopped: it found this one still running, so did not start another
            if (!failed && _followGame)
                StartFollowing();
        }

        /// <summary>Put the viewport's camera where the game's is, unless following has stopped (or started over) since. Follow loop.</summary>
        private static bool SendToViewer(Pose pose, int session)
        {
            lock (_followLock)
            {
                if (!_followGame || Volatile.Read(ref _followSession) != session)
                    return false;
                UnityConnection.Send.SendCameraFollowPose(pose.Position, pose.Forward, pose.Up, pose.Fov);
                return true;
            }
        }

        private static void ReportFollow(string text)
        {
            Report(text, ref _followReported);
        }
        #endregion

        /// <summary>Tell the status bar, when it is something other than what it was last told. Pump thread.</summary>
        private static void Report(string text)
        {
            Report(text, ref _reported);
        }

        private static void Report(string text, ref string reported)
        {
            if (text == reported)
                return;
            reported = text;
            Volatile.Write(ref _lastStatus, text);
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed || !editor.IsHandleCreated)
                return;
            try
            {
                editor.ShowLiveLinkActivity(text); //moves itself to the UI thread
            }
            catch
            {
                //Closing
            }
        }
    }
}
