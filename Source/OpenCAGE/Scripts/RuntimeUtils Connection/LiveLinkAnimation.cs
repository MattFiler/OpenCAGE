using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.RuntimeUtilsConnection
{
    /// <summary>
    /// Animation Mode in the running game ("In game" in the CAGEAnimation editor): the game holds one CAGEAnimation at the
    /// editor's playhead, or plays it on its own clock, evaluating it itself (LiveLink ANIMATION) - so every track it binds
    /// moves as it would in the level - and gives it back as it was when the mode ends, or when OpenCAGE disconnects.
    /// Nothing in the level's data changes: key edits reach the game through the auto push as they always do, and the game
    /// keeps holding the animation through them by itself.
    ///
    /// A scrub moves the playhead with every mouse move, and only the latest time is worth anything: so, as with
    /// LiveLinkCameraSync's poses, only the latest wanted state is kept, and one pump on the thread pool sends it, one
    /// request in flight at a time. Each change gets the next sequence number, which the game gives back (ANIMATION_GET)
    /// once a frame has applied it. While the drive is on, the same pump asks the game what it shows - about every frame
    /// while it plays, so the editor's playhead can follow the game's clock - and it asks nothing while the drive is off.
    /// Nothing here is sent from the UI thread.
    /// </summary>
    public static class LiveLinkAnimationDrive
    {
        /// <summary>The CAGEAnimation the game is to drive: in one placement of its composite, or in all of them.</summary>
        public sealed class Target
        {
            /// <summary>The root composite of its level (LiveLink.RootOf): the game drives it only while running that level.</summary>
            public uint Root;
            public ShortGuid Composite;
            /// <summary>The CAGEAnimation entity, in Composite.</summary>
            public ShortGuid Entity;
            /// <summary>Composite instance entity ids from the level's root down to the placement of Composite; empty for every placement.</summary>
            public List<ShortGuid> Path = new List<ShortGuid>();
            /// <summary>Its name, for the status bar.</summary>
            public string Label = "";
        }

        /// <summary>What the game should be doing. Swapped in whole, and never changed once made.</summary>
        private sealed class Wanted
        {
            /// <summary>null: nothing (whatever the game has is given back).</summary>
            public Target Target;
            public LiveLink.AnimationRequest Mode;
            public float Time;
            public float Rate = 1f;
            public LiveLink.AnimationFlags Flags;
            public uint Sequence;
        }

        /// <summary>A request the game took, and the frame its answer was written in: snapshots from later frames are about it.</summary>
        private sealed class Taken
        {
            public uint Sequence;
            public long Frame;
        }

        /// <summary>A request the game turned away, and why.</summary>
        private sealed class Refused
        {
            public uint Sequence;
            public string Message;
        }

        /// <summary>Whether the game is to drive an animation (from Begin until Release). Any thread.</summary>
        public static bool Active => Volatile.Read(ref _wanted).Target != null;

        /// <summary>Whether it is to play the animation, rather than hold it. Any thread.</summary>
        public static bool Playing
        {
            get
            {
                Wanted wanted = Volatile.Read(ref _wanted);
                return wanted.Target != null && wanted.Mode == LiveLink.AnimationRequest.Play;
            }
        }

        /// <summary>The animation the game is to drive, or null. Any thread.</summary>
        public static Target Current => Volatile.Read(ref _wanted).Target;

        /// <summary>The time the game was last asked to hold at, or play from. Any thread.</summary>
        public static float WantedTime => Volatile.Read(ref _wanted).Time;

        /// <summary>The sequence of the latest change asked for: GameAnimation.Shows says when the game has applied it. Any thread.</summary>
        public static uint Sequence => Volatile.Read(ref _wanted).Sequence;

        /// <summary>What the game last said it does with the animation (the answer to a request, or a GET); null before the first, and once given back.</summary>
        public static LiveLink.GameAnimation LastGame => Volatile.Read(ref _game);

        /// <summary>A short line saying what the game is doing with the animation, e.g. "Game: held at 1.25 s"; empty while the drive is off.</summary>
        public static string Status => Volatile.Read(ref _status) ?? "";

        /// <summary>The drive is off, but the game may still have the animation: its give-back is still to be answered. Any thread.</summary>
        public static bool Releasing => Volatile.Read(ref _wanted).Target == null && Volatile.Read(ref _holding) != 0;

        /// <summary>The game's runtime utils are from before ANIMATION: nothing is driven until it is launched from OpenCAGE again.</summary>
        public static bool Unsupported => _unsupported;

        /// <summary>Why the game turned the last request it was sent away, while no later one has been taken; null otherwise. Any thread.</summary>
        public static string Refusal => Volatile.Read(ref _refused)?.Message;

        /// <summary>Why the game turned this change away, if it did (and nothing since was taken); null otherwise. Any thread.</summary>
        public static string RefusalOf(uint sequence)
        {
            Refused refused = Volatile.Read(ref _refused);
            return refused != null && refused.Sequence == sequence ? refused.Message : null;
        }

        /// <summary>Status (or what the game does) changed. Raised on the pump's thread, or the caller's.</summary>
        public static event Action StateChanged;

        private static readonly object _changeLock = new object();
        private static uint _lastSequence;
        private static Wanted _wanted = new Wanted();
        private static LiveLink.GameAnimation _game;
        private static Taken _taken;
        private static string _status;
        //Bumped by every change and connection change: a pump standing down looks again if it moved meanwhile
        private static int _changes;
        //Bumped whenever the connection opens or closes: the pump then starts over with the game
        private static int _connection;
        private static int _pumping;    //1 while a pump runs: there is only ever one
        //Released by every change, so a pump waiting for its next look at the game goes at once
        private static readonly SemaphoreSlim _wake = new SemaphoreSlim(0, 1);
        //1 while the game may have an animation taken for OpenCAGE (sent and taken, or sent and not answered): a release is
        //then owed. A disconnect clears it, as the game gives the animation back by itself then.
        private static int _holding;

        //The pump's own: only the one pump running writes these
        private static int _pumpChanges = -1;
        private static int _pumpConnection = -1;
        private static uint _sentSequence;      //the change the game last took on this connection (0: none)
        private static uint _refusedSequence;   //one it turned away, or did not answer: sent again only with the next Reassert
        private static Refused _refused;        //the last one it turned away, until one is taken (read anywhere)
        private static bool _noAnswer;          //the last request went unanswered
        private static DateTime _refusedAt = DateTime.MinValue;
        private static DateTime _askedAt = DateTime.MinValue;  //when the game was last sent the wanted state
        private static DateTime _polledAt = DateTime.MinValue; //when it was last asked what it shows
        private static uint _releaseRoot;       //the level root of what the game last took, for the release
        private static string _releaseLabel = "";
        private static volatile bool _unsupported;
        private static string _reported;        //what the status bar was last told, so each change is said once
        private static DateTime _reportedAt = DateTime.MinValue;
        private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(1);

        //After the game turns a request away (or does not answer), the next waits this long: the reason (another level
        //running) does not change as the playhead moves, and a request a mouse move is not worth making to hear it again
        private static readonly TimeSpan RefusalBackoff = TimeSpan.FromSeconds(1);

        //The game can change while the playhead stays still - it finishes loading the level, loads another and comes back,
        //restarts - so what it should be doing is sent again this often while the drive is on (a play keeps its sequence,
        //so the game carries on rather than starting it over). One small request, answered at once.
        private static readonly TimeSpan Reassert = TimeSpan.FromSeconds(2);

        //How often the game is asked what it shows: about a frame while it plays (the editor's playhead follows it), and a
        //few times a second otherwise (a script that disables the animation, a zone that unloads)
        private static readonly TimeSpan FollowInterval = TimeSpan.FromMilliseconds(30);
        private static readonly TimeSpan WatchInterval = TimeSpan.FromMilliseconds(250);

        static LiveLinkAnimationDrive()
        {
            //A game that connects is sent what it should be doing; one that disconnects has given the animation back
            LiveLink.ConnectionChanged += () =>
            {
                Interlocked.Increment(ref _connection);
                Kick();
            };
            //What is driven belongs to the level that was open (Animation Mode's window closes with it too)
            Singleton.OnLevelLoaded += content => Release();
        }

        #region DRIVING
        /// <summary>Have the game drive this animation, held at a time. Replaces any it drives already (the game gives that back first). Returns the change's sequence.</summary>
        public static uint Begin(Target target, float time)
        {
            if (target == null)
            {
                Release();
                return 0;
            }
            return Change(current => new Wanted() { Target = target, Mode = LiveLink.AnimationRequest.Hold, Time = time });
        }

        /// <summary>Hold the animation at a time (stopping it, if it plays). Returns the change's sequence, or 0 while the drive is off.</summary>
        public static uint Hold(float time)
        {
            return Change(current => current.Target == null ? null : new Wanted() { Target = current.Target, Mode = LiveLink.AnimationRequest.Hold, Time = time });
        }

        /// <summary>
        /// Play the animation from a time on the game's own clock, at rate times real time; looping wraps to 0 rather than
        /// ending, and events runs the event tracks the play passes over (which reach the level's scripts). Returns the
        /// change's sequence, or 0 while the drive is off.
        /// </summary>
        public static uint Play(float from, float rate, bool loop, bool events)
        {
            LiveLink.AnimationFlags flags = (loop ? LiveLink.AnimationFlags.Loop : LiveLink.AnimationFlags.None) | (events ? LiveLink.AnimationFlags.Events : LiveLink.AnimationFlags.None);
            return Change(current => current.Target == null ? null : new Wanted() { Target = current.Target, Mode = LiveLink.AnimationRequest.Play, Time = from, Rate = rate, Flags = flags });
        }

        /// <summary>Give the game its animation back, as it was before it was taken. Sent until the game answers.</summary>
        public static void Release()
        {
            Change(current => current.Target == null ? null : new Wanted() { Mode = LiveLink.AnimationRequest.Release });
        }

        /// <summary>
        /// Give the game this animation back - only while the drive is still this one's, as something else may have taken it
        /// over since (checked and released as one step). Returns whether it was given back. Any thread.
        /// </summary>
        public static bool Release(Target target)
        {
            if (target == null)
                return false;
            return Change(current => ReferenceEquals(current.Target, target) ? new Wanted() { Mode = LiveLink.AnimationRequest.Release } : null) != 0;
        }

        /// <summary>
        /// The game's answer about this change: a snapshot from a frame after the one the game took it in (so it is about this
        /// request, even when it says it could not apply it), or null while there is none yet. Any thread.
        /// </summary>
        public static LiveLink.GameAnimation AnswerTo(uint sequence)
        {
            Taken taken = Volatile.Read(ref _taken);
            LiveLink.GameAnimation game = Volatile.Read(ref _game);
            if (sequence == 0 || game == null || !game.Valid)
                return null;
            if (game.Shows(sequence))
                return game;
            //A frame may have been under way when the request arrived: the one after it has certainly seen it
            return taken != null && taken.Sequence == sequence && game.Frame > taken.Frame + 1 ? game : null;
        }

        private static uint Change(Func<Wanted, Wanted> make)
        {
            Wanted next;
            lock (_changeLock)
            {
                next = make(Volatile.Read(ref _wanted));
                if (next == null)
                    return 0;
                if (++_lastSequence == 0)
                    _lastSequence = 1; //0 is the game's "nothing taken"
                next.Sequence = _lastSequence;
                Interlocked.Exchange(ref _wanted, next);
            }
            Kick();
            return next.Sequence;
        }
        #endregion

        #region PUMP
        private static void Kick()
        {
            Interlocked.Increment(ref _changes);
            if (_wake.CurrentCount == 0)
            {
                try
                {
                    _wake.Release();
                }
                catch (SemaphoreFullException)
                {
                    //Another change woke it already
                }
            }
            if (Interlocked.CompareExchange(ref _pumping, 1, 0) != 0)
                return;
            Task.Run(() => Pump());
        }

        private static async Task Pump()
        {
            try
            {
                while (true)
                {
                    if (await Step())
                        continue;

                    //Nothing left to do: stand down, then look once more - a change that came in just before found the pump
                    //still running, so did not start another
                    Volatile.Write(ref _pumping, 0);
                    if (Volatile.Read(ref _changes) == _pumpChanges || Interlocked.CompareExchange(ref _pumping, 1, 0) != 0)
                        return;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Live Link", "Driving the game's animation failed: " + ex.Message);
                Volatile.Write(ref _pumping, 0);
            }
        }

        /// <summary>Send the game the one thing it should be sent next, or ask it what it shows. True while there is more to do.</summary>
        private static async Task<bool> Step()
        {
            _pumpChanges = Volatile.Read(ref _changes);
            Wanted wanted = Volatile.Read(ref _wanted);

            int connection = Volatile.Read(ref _connection);
            if (connection != _pumpConnection)
            {
                //Another connection, or none: what was taken and turned away was the last game's, and this one is asked afresh
                //(a game whose runtime utils were too old may have been relaunched with newer ones)
                _pumpConnection = connection;
                _sentSequence = 0;
                _refusedSequence = 0;
                Volatile.Write(ref _refused, null);
                _noAnswer = false;
                _refusedAt = DateTime.MinValue;
                _askedAt = DateTime.MinValue;
                _unsupported = false;
                Volatile.Write(ref _game, null);
            }

            if (!LiveLink.Connected)
            {
                //A game that disconnects gives the animation back by itself, and there is nothing to send until one connects
                //(that kicks the pump again)
                Volatile.Write(ref _holding, 0);
                _sentSequence = 0;
                Update(wanted);
                return false;
            }

            if (wanted.Target == null)
            {
                if (Volatile.Read(ref _holding) == 0)
                {
                    Update(wanted);
                    return false;
                }
                if (await WaitOutRefusal())
                    return true;
                //Given back until the game answers: a release is always taken, whatever level it runs
                LiveLink.GameAnimation released = await LiveLink.ReleaseAnimation(_releaseRoot);
                if (released.Reply.Answered)
                {
                    Volatile.Write(ref _holding, 0);
                    _sentSequence = 0;
                    Volatile.Write(ref _refused, null);
                    _noAnswer = false;
                    _refusedAt = DateTime.MinValue;
                    Volatile.Write(ref _game, null);
                    Report("Live Link: gave " + _releaseLabel + " back to the game", now: true);
                }
                else
                {
                    _noAnswer = true;
                    _refusedAt = DateTime.UtcNow;
                }
                Update(wanted);
                return true;
            }

            if (_unsupported)
            {
                Update(wanted);
                return false;
            }

            DateTime now = DateTime.UtcNow;
            //A change the game has not taken yet - one it turned away is asked about again only once Reassert is up
            bool fresh = wanted.Sequence != _sentSequence && wanted.Sequence != _refusedSequence;
            if (fresh || now - _askedAt >= Reassert)
            {
                if (await WaitOutRefusal())
                    return true;
                await Send(wanted);
                return true;
            }

            //Nothing to send: see what the game shows, about every frame while it plays
            TimeSpan interval = wanted.Mode == LiveLink.AnimationRequest.Play ? FollowInterval : WatchInterval;
            TimeSpan wait = _polledAt + interval - now;
            TimeSpan reassertDue = _askedAt + Reassert - now;
            if (reassertDue < wait)
                wait = reassertDue;
            if (wait > TimeSpan.Zero)
            {
                await _wake.WaitAsync(wait);
                return true;
            }
            _polledAt = now;
            LiveLink.GameAnimation game = await LiveLink.GetAnimation(wanted.Target.Root);
            if (LiveLink.IsUnknownRequest(game.Reply))
                _unsupported = true;
            else if (game.Valid)
                Volatile.Write(ref _game, game);
            _noAnswer = !game.Reply.Answered;
            Update(wanted);
            return true;
        }

        /// <summary>Turned away (or not answered) a moment ago: wait out the rest of that. True when it waited (look again after). Pump thread.</summary>
        private static async Task<bool> WaitOutRefusal()
        {
            TimeSpan wait = _refusedAt + RefusalBackoff - DateTime.UtcNow;
            if (wait <= TimeSpan.Zero)
                return false;
            await Task.Delay(wait);
            return true;
        }

        private static async Task Send(Wanted wanted)
        {
            _askedAt = DateTime.UtcNow;
            Target target = wanted.Target;
            LiveLink.GameAnimation answer = await LiveLink.SetAnimation(target.Root, target.Composite, target.Entity, target.Path, wanted.Mode, wanted.Time, wanted.Rate, wanted.Flags, wanted.Sequence);
            if (LiveLink.IsUnknownRequest(answer.Reply))
            {
                _unsupported = true;
                Update(wanted);
                return;
            }

            //Not answered, it may have been taken all the same (a game that is slow for a moment): given back either way
            if (answer.Reply.Ok || !answer.Reply.Answered)
            {
                Volatile.Write(ref _holding, 1);
                _releaseRoot = target.Root;
                _releaseLabel = string.IsNullOrEmpty(target.Label) ? "the animation" : target.Label;
            }
            _noAnswer = !answer.Reply.Answered;
            if (answer.Reply.Ok)
            {
                _sentSequence = wanted.Sequence;
                _refusedSequence = 0;
                Volatile.Write(ref _refused, null);
                _refusedAt = DateTime.MinValue;
                //The answer is as of the frame before the request applies
                Volatile.Write(ref _taken, new Taken() { Sequence = wanted.Sequence, Frame = answer.Valid ? answer.Frame : -1 });
                if (answer.Valid)
                    Volatile.Write(ref _game, answer);
            }
            else
            {
                _refusedSequence = wanted.Sequence;
                if (answer.Reply.Answered)
                    Volatile.Write(ref _refused, new Refused() { Sequence = wanted.Sequence, Message = answer.Reply.Message ?? "" });
                _refusedAt = DateTime.UtcNow;
            }
            Update(wanted);
        }
        #endregion

        #region STATUS
        /// <summary>Work the status line out again, and say so when it changed. Pump thread.</summary>
        private static void Update(Wanted wanted)
        {
            LiveLink.GameAnimation game = Volatile.Read(ref _game);
            string status = Describe(wanted, LiveLink.Connected, game);
            if (status != Volatile.Read(ref _status))
            {
                Volatile.Write(ref _status, status);
                StateChanged?.Invoke();
            }

            //The status bar hears of each change of state once, without the times (which change every frame while it plays);
            //a disconnect it hears of from the live link itself
            if (wanted.Target != null && status.Length != 0 && LiveLink.Connected)
                Report("Live Link: " + (string.IsNullOrEmpty(wanted.Target.Label) ? "the animation" : wanted.Target.Label) + " - " + Words(status));
        }

        private static string Describe(Wanted wanted, bool connected, LiveLink.GameAnimation game)
        {
            if (wanted.Target == null)
                return connected && Volatile.Read(ref _holding) != 0 ? "Game: giving the animation back..." : "";
            if (!connected)
                return "Game: waiting for the game to connect";
            if (_unsupported)
                return "Game: runtime utils too old - relaunch the game from OpenCAGE";
            Refused refused = Volatile.Read(ref _refused);
            if (refused != null)
                return "Game: " + Lower(refused.Message);
            if (_noAnswer)
                return "Game: not answering";
            if (game == null || !game.Valid)
                return "Game: sending...";

            string text;
            switch (game.State)
            {
                case "held":
                    text = "Game: held at " + Seconds(game.Time);
                    break;
                case "playing":
                    text = "Game: playing " + Seconds(game.Time) + ((wanted.Flags & LiveLink.AnimationFlags.Loop) != 0 ? " (looping)" : "");
                    break;
                case "ended":
                    text = "Game: played to the end (" + Seconds(game.Time) + ")";
                    break;
                case "waiting":
                    text = "Game: waiting - " + Lower(game.Reason);
                    break;
                case "not_found":
                    //Its reason also covers an exception while it was looked for, which saving and reloading would not help
                    text = game.Reason.Contains("exception")
                        ? "Game: failed - " + Lower(game.Reason)
                        : "Game: this animation is not in the running level - save and reload";
                    break;
                case "not_animation":
                    text = "Game: this is not a CAGEAnimation in the running level - save and reload";
                    break;
                case "disabled":
                    text = "Game: disabled in the game - its level enables it";
                    break;
                case "no_data":
                    text = "Game: it has no keyframes in the running level - save and reload";
                    break;
                case "cinematic":
                    text = "Game: cinematic animations can't be driven yet";
                    break;
                case "released":
                    text = "Game: taking the animation...";
                    break;
                default:
                    text = "Game: " + game.State + (game.Reason.Length != 0 ? " - " + Lower(game.Reason) : "");
                    break;
            }
            //A composite placed several times: only some placements could be driven
            bool shown = game.State == "held" || game.State == "playing" || game.State == "ended";
            if (shown && game.Found > 1 && game.Applied >= 0 && game.Applied < game.Found)
                text += " (" + game.Applied + " of " + game.Found + " placements)";
            return text;
        }

        /// <summary>The status line as the status bar has it: what the game does, without the times.</summary>
        private static string Words(string status)
        {
            if (status.StartsWith("Game: held at"))
                return "held in the game";
            if (status.StartsWith("Game: playing"))
                return "playing in the game";
            if (status.StartsWith("Game: played to the end"))
                return "played to its end in the game";
            return status.StartsWith("Game: ") ? status.Substring(6) : status;
        }

        private static string Seconds(double time)
        {
            return double.IsNaN(time) ? "?" : time.ToString("0.00") + " s";
        }

        //The game's reasons are sentences: one that follows a dash starts lower case (an acronym or a name keeps its case)
        private static string Lower(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2 || !char.IsUpper(text[0]) || !char.IsLower(text[1]))
                return text ?? "";
            return char.ToLowerInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>
        /// Tell the status bar, when it is something other than what it was last told - at most once a second, as a state
        /// can flap (a zone the game finds loaded one frame and not the next); the latest is said once that is up, as the
        /// pump looks again a few times a second while the drive is on. Pump thread.
        /// </summary>
        private static void Report(string text, bool now = false)
        {
            if (text == _reported || (!now && DateTime.UtcNow - _reportedAt < ReportInterval))
                return;
            _reported = text;
            _reportedAt = DateTime.UtcNow;
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
        #endregion
    }
}
