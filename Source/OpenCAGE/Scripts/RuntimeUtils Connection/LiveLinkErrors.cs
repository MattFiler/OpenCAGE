using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.RuntimeUtilsConnection
{
    public static partial class LiveLink
    {
        /// <summary>The script errors the game has noted since the one numbered after (0: all it still keeps), as ERRORS_GET gives them.</summary>
        public static Task<Reply> GetErrors(uint after, double timeoutSeconds)
        {
            return Request(Command.ERRORS_GET, w => w.Write(after), timeoutSeconds);
        }
    }

    /// <summary>One of the game's script errors, or a check of a script entity it made that failed, as ERRORS_GET gives it.</summary>
    public sealed class ScriptError
    {
        public uint Sequence;
        /// <summary>A check the game made of the entity failed (else it reported an error).</summary>
        public bool FailedCheck;
        /// <summary>The entity could be read: its ids are there.</summary>
        public bool EntityRead;
        public float ScriptTime;
        /// <summary>The level it happened in: the id of its root composite.</summary>
        public uint Root;
        public uint Entity;
        /// <summary>The composite instance the entity is in (0: the level's root instance).</summary>
        public uint Instance;
        /// <summary>The entity's type, or the composite it is an instance of.</summary>
        public uint Type;
        /// <summary>The composite the entity is in, as the game gives it (0 for the level's root instance: see <see cref="CompositeId"/>).</summary>
        public uint Composite;
        public string Message = "";
        public string CompositeName = "";
        public string InstanceOf = "";

        /// <summary>The composite the entity is in: the level's root composite for the root instance.</summary>
        public uint CompositeId => Instance == 0 && Composite == 0 ? Root : Composite;
    }

    /// <summary>
    /// The Error Log (Launch Game's Error log): the script errors and failed checks the running game reports, taken from it
    /// over Live Link (ERRORS_GET) twice a second while the Error Log window is open, with how many there are on each entity
    /// for the flowgraph's warning signs. Clearing it, or closing the window, takes them all away. The game numbers its
    /// errors from 1 each time it starts and keeps its latest 2048, across levels: what was taken is not asked for again, and
    /// a launch from OpenCAGE starts the numbering afresh (NewSession).
    /// </summary>
    public static class ScriptErrorLog
    {
        private const int PollMs = 500;
        private const double PollTimeoutSeconds = 3.0;
        //The most kept here: the oldest go first
        public const int MaxKept = 5000;

        private static readonly object _lock = new object();
        private static readonly List<ScriptError> _errors = new List<ScriptError>();
        private static readonly Dictionary<(uint composite, uint entity), int> _counts = new Dictionary<(uint, uint), int>();
        private static uint _after;
        private static int _lost;
        //Bumped by Clear (a poll under way when the log was cleared adds nothing, but what it took is not asked for again) and by
        //NewSession (a poll under way then took from the game before it: dropped, and asked again from the start)
        private static int _clears;
        private static int _session;
        private static volatile bool _active;
        private static volatile int _countsSize;
        private static int _pumping;
        private static readonly SemaphoreSlim _wake = new SemaphoreSlim(0, 1);
        private static string _status = "";

        /// <summary>Errors arrived, oldest first. Any thread.</summary>
        public static event Action<List<ScriptError>> Added;
        /// <summary>The log was emptied. Any thread.</summary>
        public static event Action Cleared;
        /// <summary>What the log says of the game changed (connected, refused...). Any thread.</summary>
        public static event Action StatusChanged;
        /// <summary>The warning signs on entities changed. Any thread.</summary>
        public static event Action MarksChanged;

        /// <summary>The Error Log is open: the game is polled and its errors' entities are marked.</summary>
        public static bool Active => _active;
        public static string Status => _status;
        /// <summary>The <see cref="Status"/> while the game answers: nothing to say.</summary>
        public const string FollowingStatus = "Following the game's script errors";
        /// <summary>Errors the game noted but no longer had by the time they were asked for (more than it keeps came at once).</summary>
        public static int Lost { get { lock (_lock) return _lost; } }
        public static bool AnyMarks => _active && _countsSize != 0;

        public static List<ScriptError> Snapshot()
        {
            lock (_lock)
                return new List<ScriptError>(_errors);
        }

        /// <summary>How many errors in the log are about this entity of this composite (none while the log is closed).</summary>
        public static int CountOn(uint composite, uint entity)
        {
            if (!_active || _countsSize == 0)
                return 0;
            lock (_lock)
                return _counts.TryGetValue((composite, entity), out int count) ? count : 0;
        }

        /// <summary>The window opened: start taking the game's errors.</summary>
        public static void Start()
        {
            _active = true;
            Kick();
        }

        /// <summary>The window closed: stop, and forget what was taken (the warning signs go with it).</summary>
        public static void Stop()
        {
            _active = false;
            Clear();
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _errors.Clear();
                _counts.Clear();
                _countsSize = 0;
                _lost = 0;
                _clears++;
            }
            Cleared?.Invoke();
            MarksChanged?.Invoke();
        }

        /// <summary>OpenCAGE is launching the game, whose errors are numbered from 1 again: asked for from the start, and the log emptied first when wanted.</summary>
        public static void NewSession(bool clear)
        {
            lock (_lock)
            {
                _after = 0;
                _session++;
            }
            if (clear)
                Clear();
            Kick();
        }

        /// <summary>Look again now. Any thread.</summary>
        public static void Kick()
        {
            if (_wake.CurrentCount == 0)
            {
                try
                {
                    _wake.Release();
                }
                catch (SemaphoreFullException)
                {
                    //Woken already
                }
            }
            if (!_active || Interlocked.CompareExchange(ref _pumping, 1, 0) != 0)
                return;
            Task.Run(() => Pump());
        }

        private static async Task Pump()
        {
            try
            {
                while (_active)
                {
                    bool more = false;
                    if (LiveLink.Connected)
                        more = await Poll();
                    else
                        SetStatus("Waiting for the game to connect (Live Link)");
                    if (!more)
                        await _wake.WaitAsync(PollMs);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Live Link", "The Error Log's poll failed: " + ex.Message);
            }
            finally
            {
                Volatile.Write(ref _pumping, 0);
            }
            if (_active)
                Kick();
        }

        /// <summary>One look at the game: true when it has more to give at once. Pump thread.</summary>
        private static async Task<bool> Poll()
        {
            uint after;
            int clears, session;
            lock (_lock)
            {
                after = _after;
                clears = _clears;
                session = _session;
            }
            LiveLink.Reply reply = await LiveLink.GetErrors(after, PollTimeoutSeconds);
            if (!reply.Answered)
            {
                SetStatus(LiveLink.Connected ? "The game is not answering (loading?)" : "Waiting for the game to connect (Live Link)");
                return false;
            }
            if (!reply.Ok)
            {
                SetStatus(string.IsNullOrEmpty(reply.Message) ? "The game refused to give its script errors" : reply.Message);
                return false;
            }
            if (!Read(reply.Payload, out uint oldest, out uint newest, out List<ScriptError> taken))
            {
                SetStatus("The game's answer could not be read (is it running an older OpenCAGE's runtime utils?)");
                return false;
            }

            List<ScriptError> added = new List<ScriptError>();
            bool trimmed = false;
            lock (_lock)
            {
                if (session != _session)
                    return true;
                //The game started again (numbered afresh) without a launch from here: everything it has is new
                if (newest < _after)
                {
                    _after = 0;
                    return true;
                }
                if (clears != _clears)
                {
                    if (taken.Count != 0)
                        _after = Math.Max(_after, taken[taken.Count - 1].Sequence);
                    return true;
                }
                if (oldest != 0 && oldest > _after + 1)
                    _lost += (int)Math.Min(int.MaxValue, oldest - _after - 1);
                foreach (ScriptError error in taken)
                {
                    if (error.Sequence <= _after)
                        continue;
                    _after = error.Sequence;
                    _errors.Add(error);
                    added.Add(error);
                    Count(error, 1);
                }
                while (_errors.Count > MaxKept)
                {
                    Count(_errors[0], -1);
                    _errors.RemoveAt(0);
                    trimmed = true;
                }
                _countsSize = _counts.Count;
            }
            SetStatus(FollowingStatus);
            if (trimmed)
                Cleared?.Invoke();
            if (added.Count != 0)
            {
                Added?.Invoke(added);
                MarksChanged?.Invoke();
            }
            return reply.Message != null && reply.Message.Contains("more=1");
        }

        private static void Count(ScriptError error, int by)
        {
            if (!error.EntityRead)
                return;
            (uint, uint) key = (error.CompositeId, error.Entity);
            _counts.TryGetValue(key, out int count);
            count += by;
            if (count <= 0)
                _counts.Remove(key);
            else
                _counts[key] = count;
        }

        private static void SetStatus(string status)
        {
            if (status == _status)
                return;
            _status = status;
            StatusChanged?.Invoke();
        }

        /// <summary>ERRORS_GET's payload (format 1, see the runtime utils' LIVE_LINK_SERVER.h).</summary>
        public static bool Read(byte[] payload, out uint oldest, out uint newest, out List<ScriptError> errors)
        {
            oldest = newest = 0;
            errors = new List<ScriptError>();
            if (payload == null)
                return false;
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(payload)))
                {
                    if (reader.ReadUInt32() != 1)
                        return false;
                    oldest = reader.ReadUInt32();
                    newest = reader.ReadUInt32();
                    reader.ReadUInt32(); //the running level's root
                    uint count = reader.ReadUInt32();
                    for (uint i = 0; i < count; i++)
                    {
                        ScriptError error = new ScriptError();
                        error.Sequence = reader.ReadUInt32();
                        error.FailedCheck = reader.ReadByte() == 1;
                        error.EntityRead = (reader.ReadByte() & 1) != 0;
                        reader.ReadUInt16();
                        error.ScriptTime = reader.ReadSingle();
                        error.Root = reader.ReadUInt32();
                        error.Entity = reader.ReadUInt32();
                        error.Instance = reader.ReadUInt32();
                        error.Type = reader.ReadUInt32();
                        error.Composite = reader.ReadUInt32();
                        error.Message = Text(reader);
                        error.CompositeName = Text(reader);
                        error.InstanceOf = Text(reader);
                        errors.Add(error);
                    }
                }
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        private static string Text(BinaryReader reader)
        {
            int length = (int)reader.ReadUInt32();
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        #region IN THE LEVEL
        /// <summary>
        /// Where an error's entity is in the level open here: the composite, the entity, and the instance entity ids leading
        /// to it from the level's root. Null when found, else why not. UI thread.
        /// </summary>
        public static string Locate(ScriptError error, out Commands commands, out Composite composite, out Entity entity, out List<ShortGuid> path)
        {
            composite = null;
            entity = null;
            path = null;
            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            commands = content?.Level?.Commands;
            if (error == null || !error.EntityRead)
                return "The game could not read which entity this is about";
            if (commands?.EntryPoints == null || commands.EntryPoints.Length == 0 || commands.EntryPoints[0] == null)
                return "No level is open here";
            if (error.Root != 0 && LiveLink.RootOf(commands) != error.Root)
                return "This happened in another level than the one open here";
            if (error.Instance == 0)
            {
                composite = commands.EntryPoints[0];
                path = new List<ShortGuid>();
            }
            else
            {
                content.EnsureEditorUtils();
                if (content.EditorUtils == null)
                    return "The level's instances are still being worked out - try again in a moment";
                (Composite found, EntityPath foundPath) = content.EditorUtils.GetCompositeFromInstanceID(commands, new ShortGuid(error.Instance));
                if (found == null)
                    return "The entity is in a composite instance this level does not have - is the game running the level as saved?";
                composite = found;
                path = foundPath?.path.Where(o => o != ShortGuid.Invalid).ToList() ?? new List<ShortGuid>();
            }
            entity = composite.GetEntityByID(new ShortGuid(error.Entity));
            if (entity == null)
                return "The entity is not in " + composite.name + " here - is the game running the level as saved?";
            return null;
        }

        /// <summary>Show an error's entity: in the instance it happened in, else its composite. Null when shown, else why not. UI thread.</summary>
        public static string Show(ScriptError error)
        {
            string problem = Locate(error, out Commands commands, out Composite composite, out Entity entity, out List<ShortGuid> path);
            if (problem != null)
                return problem;
            if (LiveLinkDebugger.ShowEntity(commands, path, entity.shortGUID))
                return null;
            Singleton.Editor?.CompositeBrowser?.LoadCompositeAndEntity(composite, entity);
            return null;
        }

        /// <summary>The entity an error is about, as the log lists it: its name where the level open here has it.</summary>
        public static string EntityText(ScriptError error)
        {
            if (error == null || !error.EntityRead)
                return "(unknown entity)";
            if (Locate(error, out Commands commands, out Composite composite, out Entity entity, out _) == null)
                return LiveLinkDebugger.EntityText(commands, composite, entity);
            string type = error.Type == 0 ? "" : ShortGuidUtils.FindString(new ShortGuid(error.Type));
            if (!string.IsNullOrEmpty(error.InstanceOf))
                type = error.InstanceOf;
            return (string.IsNullOrEmpty(type) ? "" : type + " ") + "[" + new ShortGuid(error.Entity).ToByteString() + "]";
        }

        /// <summary>The composite an error's entity is in, by name.</summary>
        public static string CompositeText(ScriptError error)
        {
            if (error == null || !error.EntityRead)
                return "";
            if (!string.IsNullOrEmpty(error.CompositeName))
                return error.CompositeName;
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            Composite composite = commands?.GetComposite(new ShortGuid(error.CompositeId));
            return composite?.name ?? (error.Instance == 0 ? "(the level's root)" : new ShortGuid(error.CompositeId).ToByteString());
        }
        #endregion

        #region SIGNS
        /// <summary>A warning sign (a failed check, and the flowgraph's mark on an entity with errors): an amber triangle with a "!".</summary>
        public static void DrawWarning(Graphics g, RectangleF area)
        {
            SmoothingMode smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.HighQuality;
            PointF[] triangle =
            {
                new PointF(area.Left + area.Width * 0.5f, area.Top),
                new PointF(area.Right, area.Bottom),
                new PointF(area.Left, area.Bottom),
            };
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(245, 185, 40)))
                g.FillPolygon(fill, triangle);
            using (Pen edge = new Pen(Color.FromArgb(230, 20, 20, 20), Math.Max(1f, area.Width / 14f)) { LineJoin = LineJoin.Round })
                g.DrawPolygon(edge, triangle);
            Mark(g, area, area.Top + area.Height * 0.32f, area.Top + area.Height * 0.66f, area.Top + area.Height * 0.8f, Color.FromArgb(30, 30, 30));
            g.SmoothingMode = smoothing;
        }

        /// <summary>An error sign: a red circle with a "!".</summary>
        public static void DrawError(Graphics g, RectangleF area)
        {
            SmoothingMode smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.HighQuality;
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(215, 50, 45)))
                g.FillEllipse(fill, area);
            using (Pen edge = new Pen(Color.FromArgb(230, 20, 20, 20), Math.Max(1f, area.Width / 14f)))
                g.DrawEllipse(edge, area);
            Mark(g, area, area.Top + area.Height * 0.2f, area.Top + area.Height * 0.6f, area.Top + area.Height * 0.76f, Color.White);
            g.SmoothingMode = smoothing;
        }

        //The "!" of a sign: a bar from top to bottom, and a dot at dot
        private static void Mark(Graphics g, RectangleF area, float top, float bottom, float dot, Color colour)
        {
            float x = area.Left + area.Width * 0.5f;
            float width = Math.Max(1.2f, area.Width * 0.13f);
            using (Pen bar = new Pen(colour, width) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(bar, x, top, x, bottom);
            using (SolidBrush point = new SolidBrush(colour))
                g.FillEllipse(point, x - width * 0.6f, dot - width * 0.1f, width * 1.2f, width * 1.2f);
        }

        /// <summary>A sign as a picture (for lists and menus).</summary>
        public static Bitmap SignImage(bool error, int size)
        {
            Bitmap image = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(image))
            {
                RectangleF area = new RectangleF(1f, 1f, size - 2.5f, size - 2.5f);
                if (error)
                    DrawError(g, area);
                else
                    DrawWarning(g, area);
            }
            return image;
        }
        #endregion
    }
}
