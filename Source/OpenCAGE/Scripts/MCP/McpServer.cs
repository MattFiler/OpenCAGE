using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Lets AI assistants drive this editor: the Model Context Protocol, served over a local named pipe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An MCP client (Claude Desktop, Claude Code, ...) starts OpenCAGE.MCP.exe, which speaks the protocol
    /// over its stdin/stdout and relays every message here, starting OpenCAGE first if it is not running.
    /// The tools (<see cref="McpTools"/>) run against the level open in this window, through the same
    /// code paths the UI uses, so everything they do shows up in the editor, goes onto its undo history,
    /// and reaches the viewport.
    /// </para>
    /// <para>
    /// The pipe is per logon session and only the user running OpenCAGE may open it (never from another
    /// machine). One tool runs at a time; pings, cancellations and the few tools marked Concurrent (status
    /// reads such as get_activity) are answered while one runs, and a call waiting its turn reports so as
    /// progress. Only the first OpenCAGE to start serves: a second one keeps trying until the first goes
    /// away, so a client never talks to two editors at once.
    /// </para>
    /// <para>
    /// Every tools/call result carries _meta "opencage/session" (this editor's process id and start, the open
    /// level, unsaved changes): the bridge compares it across reconnects to tell the assistant when OpenCAGE was
    /// restarted and what was lost.
    /// </para>
    /// </remarks>
    public static class McpServer
    {
        /// <summary>The pipe the bridge connects to. Must match OpenCAGE.MCP's Program.PipeName.</summary>
        public static readonly string PipeName = "OpenCAGE.MCP." + Process.GetCurrentProcess().SessionId;

        public const string ProtocolVersion = "2025-06-18";
        private static readonly string[] _supportedProtocols = { "2025-06-18", "2025-03-26", "2024-11-05" };

        private static Thread _acceptThread;
        private static volatile bool _stop;
        private static NamedPipeServerStream _listening;
        private static readonly object _lock = new object();
        private static readonly List<Connection> _connections = new List<Connection>();

        //One tool at a time, whichever client asks: they all drive the one editor
        private static readonly SemaphoreSlim _toolGate = new SemaphoreSlim(1, 1);

        /// <summary>Whether this OpenCAGE is the one clients reach (false while another holds the pipe).</summary>
        public static bool Serving { get; private set; }

        /// <summary>How many clients are connected now.</summary>
        public static int ClientCount { get { lock (_lock) return _connections.Count; } }

        /// <summary>The tool running now, if any (for the status bar).</summary>
        public static string RunningTool { get; private set; }

        /// <summary>Raised (on a background thread) when a tool starts or finishes, or a client comes or goes.</summary>
        public static event Action StatusChanged;

        public static bool Running => _acceptThread != null;

        public static void Start()
        {
            lock (_lock)
            {
                if (_acceptThread != null)
                    return;
                //Called on the UI thread: which thread's message boxes are the editor's own (see McpDialogs)
                McpDialogs.UiThreadId = McpDialogs.CurrentThreadId();
                McpSession.Hook();
                //Steps recorded while a tool's own work runs on the UI thread are the assistant's (get_undo_history, and undo's
                //guard) - not those the user makes while a step pumps their input (a Save & Build's message loop)
                Undo.UndoStack.Current.OriginProvider = () => McpDialogs.OwnWork ? McpSession.AssistantOrigin : null;
                _stop = false;
                _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "OpenCAGE MCP server" };
                _acceptThread.Start();
            }
        }

        public static void Stop()
        {
            _stop = true;
            List<Connection> open;
            lock (_lock)
            {
                try { _listening?.Dispose(); } catch { }
                _listening = null;
                _acceptThread = null;
                open = _connections.ToList();
            }
            foreach (Connection connection in open)
                connection.Close();
            Serving = false;
            RaiseStatus();
        }

        private static void AcceptLoop()
        {
            //Only one OpenCAGE serves: whoever holds this. Another waits here until the holder goes away.
            using (Mutex owner = new Mutex(false, @"Local\OpenCAGE_MCP_Server"))
            {
                bool held = false;
                try
                {
                    while (!_stop && !held)
                    {
                        try { held = owner.WaitOne(3000); }
                        catch (AbandonedMutexException) { held = true; }
                    }
                    if (held)
                    {
                        WriteToolCache();
                        Serve();
                    }
                }
                finally
                {
                    if (held)
                        try { owner.ReleaseMutex(); } catch { }
                    Serving = false;
                    RaiseStatus();
                }
            }
        }

        private static void Serve()
        {
            while (!_stop)
            {
                NamedPipeServerStream server;
                try
                {
                    server = CreateServer();
                }
                catch (Exception)
                {
                    //The name is still held by an instance on its way out: try again shortly
                    Thread.Sleep(1000);
                    continue;
                }
                //Another instance of a pipe shares the first one's security: if something else made the first, it is not ours
                if (!OwnedByThisUser(server))
                {
                    try { server.Dispose(); } catch { }
                    Console.WriteLine("OpenCAGE MCP: the pipe " + PipeName + " was created by another user's program; not serving until it goes away.");
                    Thread.Sleep(5000);
                    continue;
                }
                if (!Serving) { Serving = true; RaiseStatus(); }

                lock (_lock) { _listening = server; }
                try
                {
                    server.WaitForConnection();
                }
                catch (Exception)
                {
                    try { server.Dispose(); } catch { }
                    if (_stop) return;
                    Thread.Sleep(250);
                    continue;
                }
                finally
                {
                    lock (_lock) { if (ReferenceEquals(_listening, server)) _listening = null; }
                }
                if (_stop)
                {
                    try { server.Dispose(); } catch { }
                    return;
                }

                Connection connection = new Connection(server);
                lock (_lock) _connections.Add(connection);
                RaiseStatus();
                new Thread(connection.Run) { IsBackground = true, Name = "OpenCAGE MCP client" }.Start();
            }
        }

        /// <summary>A pipe instance only this user can open, and never over the network.</summary>
        private static NamedPipeServerStream CreateServer()
        {
            PipeSecurity security = new PipeSecurity();
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
            return new NamedPipeServerStream(PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536, security);
        }

        /// <summary>Whether a pipe was created by this user: its owner is this user, or Administrators when created elevated.</summary>
        private static bool OwnedByThisUser(PipeStream pipe)
        {
            try
            {
                SecurityIdentifier owner = (SecurityIdentifier)pipe.GetAccessControl().GetOwner(typeof(SecurityIdentifier));
                return owner == WindowsIdentity.GetCurrent().User || owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
            }
            catch { return false; }
        }

        /// <summary>
        /// The tool list, left where OpenCAGE.MCP answers a client's tools/list from while this editor is
        /// still starting. Stamped with this executable: a bridge beside another build ignores it.
        /// </summary>
        private static void WriteToolCache()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenCAGE", "mcp_tools.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + "." + Process.GetCurrentProcess().Id + ".tmp";
                //Everything the bridge answers itself before this editor is reached: the handshake's instructions and version (one
                //copy of them, McpTools.Instructions), the tools, the prompts and the toolsets
                JObject cache = new JObject()
                {
                    ["editor"] = EditorStamp(System.Windows.Forms.Application.ExecutablePath),
                    ["version"] = Singleton.Version ?? "",
                    ["instructions"] = McpTools.Instructions,
                    ["tools"] = McpTools.Describe(),
                    ["prompts"] = McpPrompts.Definitions(),
                    ["toolsets"] = McpToolsets.Describe(),
                };
                File.WriteAllText(temp, cache.ToString(Formatting.None));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Console.WriteLine("OpenCAGE MCP: could not write the tool list cache: " + e.Message);
            }
        }

        /// <summary>Which executable, which build of it. Must match OpenCAGE.MCP's Program.EditorStamp.</summary>
        private static string EditorStamp(string path)
        {
            try
            {
                FileInfo exe = new FileInfo(path);
                return exe.FullName.ToUpperInvariant() + "|" + exe.Length + "|" + exe.LastWriteTimeUtc.Ticks;
            }
            catch { return ""; }
        }

        /// <summary>A message as sent: strings that look like dates stay strings (a date-like name or value reaches a tool unchanged).</summary>
        internal static JObject ParseObject(string line)
        {
            using (JsonTextReader reader = new JsonTextReader(new StringReader(line)) { DateParseHandling = DateParseHandling.None })
                return JObject.Load(reader);
        }

        private static void RaiseStatus()
        {
            try { StatusChanged?.Invoke(); } catch { }
        }

        /// <summary>One connected client: reads its messages, answers each on its own task.</summary>
        private sealed class Connection
        {
            private readonly NamedPipeServerStream _pipe;
            private readonly StreamWriter _writer;
            private readonly object _writeLock = new object();
            private readonly ConcurrentDictionary<string, CancellationTokenSource> _inFlight = new ConcurrentDictionary<string, CancellationTokenSource>();
            private volatile bool _closed;

            //The level as this client last saw it (McpSession.Generation after its last call): -1 before its first call
            private int _knownGeneration = -1;
            private string _knownLevel;
            private readonly object _levelLock = new object();

            public Connection(NamedPipeServerStream pipe)
            {
                _pipe = pipe;
                _writer = new StreamWriter(pipe, new UTF8Encoding(false), 65536, true) { AutoFlush = true, NewLine = "\n" };
            }

            public void Run()
            {
                try
                {
                    using (StreamReader reader = new StreamReader(_pipe, new UTF8Encoding(false), false, 65536, true))
                    {
                        string line;
                        while (!_closed && (line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            JObject message;
                            try
                            {
                                message = ParseObject(line);
                            }
                            catch (Exception e)
                            {
                                Send(ErrorMessage(null, -32700, "Parse error: " + e.Message));
                                continue;
                            }
                            Receive(message);
                        }
                    }
                }
                catch (Exception) { }
                Close();
            }

            public void Close()
            {
                if (_closed) return;
                _closed = true;
                foreach (CancellationTokenSource cancel in _inFlight.Values)
                    try { cancel.Cancel(); } catch { }
                try { _pipe.Dispose(); } catch { }
                lock (_lock) _connections.Remove(this);
                RaiseStatus();
            }

            private void Receive(JObject message)
            {
                string method = (string)message["method"];
                JToken id = message["id"];
                if (method == null)
                    return; //a response to something we never ask

                if (id == null)
                {
                    //Notifications: the only one that asks anything of us is a cancellation
                    if (method == "notifications/cancelled")
                    {
                        string requestId = message["params"]?["requestId"]?.ToString(Formatting.None);
                        if (requestId != null && _inFlight.TryGetValue(requestId, out CancellationTokenSource cancel))
                            try { cancel.Cancel(); } catch { }
                    }
                    return;
                }

                string key = id.ToString(Formatting.None);
                CancellationTokenSource source = new CancellationTokenSource();
                _inFlight[key] = source;
                Task.Run(() =>
                {
                    JObject response;
                    try
                    {
                        response = Answer(method, id, message["params"] as JObject ?? new JObject(), source.Token);
                    }
                    catch (McpProtocolException e)
                    {
                        response = ErrorMessage(id, e.Code, e.Message);
                    }
                    catch (Exception e)
                    {
                        response = ErrorMessage(id, -32603, e.Message);
                    }
                    finally
                    {
                        _inFlight.TryRemove(key, out _);
                    }
                    //A cancelled request is not answered (the client has stopped waiting for it)
                    if (!source.IsCancellationRequested && response != null)
                        Send(response);
                    source.Dispose();
                });
            }

            private JObject Answer(string method, JToken id, JObject parameters, CancellationToken cancel)
            {
                switch (method)
                {
                    case "initialize":
                        {
                            //The bridge answers this itself; a client connected straight to the pipe gets the same answer
                            string requested = (string)parameters["protocolVersion"];
                            return ResultMessage(id, new JObject()
                            {
                                ["protocolVersion"] = Array.IndexOf(_supportedProtocols, requested) >= 0 ? requested : ProtocolVersion,
                                ["capabilities"] = new JObject() { ["tools"] = new JObject() { ["listChanged"] = false }, ["prompts"] = new JObject() { ["listChanged"] = false } },
                                ["serverInfo"] = new JObject() { ["name"] = "opencage", ["title"] = "OpenCAGE", ["version"] = Singleton.Version ?? "" },
                                ["instructions"] = McpTools.Instructions,
                            });
                        }
                    case "ping":
                        return ResultMessage(id, new JObject());
                    case "tools/list":
                        return ResultMessage(id, new JObject() { ["tools"] = McpTools.Describe() });
                    case "tools/call":
                        {
                            JObject result = CallTool(parameters, cancel);
                            //Which editor answered, and what it had open: the bridge tells the assistant when that changes under it
                            result["_meta"] = new JObject() { [McpSession.MetaKey] = McpSession.Describe() };
                            return ResultMessage(id, result);
                        }
                    case "resources/list":
                        return ResultMessage(id, new JObject() { ["resources"] = new JArray() });
                    case "resources/templates/list":
                        return ResultMessage(id, new JObject() { ["resourceTemplates"] = new JArray() });
                    case "prompts/list":
                        return ResultMessage(id, new JObject() { ["prompts"] = McpPrompts.List(McpPrompts.Definitions()) });
                    case "prompts/get":
                        {
                            JObject prompt = McpPrompts.Get(McpPrompts.Definitions(), (string)parameters["name"], parameters["arguments"] as JObject, out string problem);
                            if (prompt == null)
                                throw new McpProtocolException(-32602, problem);
                            return ResultMessage(id, prompt);
                        }
                    case "logging/setLevel":
                        return ResultMessage(id, new JObject());
                    default:
                        throw new McpProtocolException(-32601, "Method not found: " + method);
                }
            }

            //Tools that open or close levels themselves, or work on files and the game rather than the open level's script: a level
            //switch since the last call is no reason to refuse them. Only a call that opened a level itself (McpCall.OpenedLevel)
            //takes the level it leaves open as known; for the rest a switch still refuses the next change.
            private static readonly HashSet<string> LevelOpeners = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "load_level", "create_level", "launch_game", "close_game", "restore_backup", "create_backup", "delete_backups", "undo_group",
            };

            private JObject CallTool(JObject parameters, CancellationToken cancel)
            {
                string name = (string)parameters["name"];
                McpTool tool = McpTools.Find(name);
                if (tool == null)
                    throw new McpProtocolException(-32602, "Unknown tool: " + name + "." + McpNames.DidYouMean(McpTools.All.Select(o => o.Name), name ?? ""));

                //A model that encodes the arguments twice sends a string: read the object in it, rather than run with none
                JToken given = parameters["arguments"];
                JObject arguments;
                string argumentsNote = null;
                if (given == null || given.Type == JTokenType.Null)
                    arguments = new JObject();
                else if (given is JObject obj)
                    arguments = obj;
                else if (given.Type == JTokenType.String && TryParseObject((string)given, out JObject parsed))
                {
                    arguments = parsed;
                    argumentsNote = "'arguments' came as a string of JSON; the object in it was used. Send the arguments as an object.";
                }
                else
                    return McpTool.ErrorResult("'arguments' must be a JSON object of argument names to values (it was " + given.Type.ToString().ToLowerInvariant() + "). Nothing was done.", null, McpErrorCodes.InvalidArgument);
                JToken progressToken = parameters["_meta"]?["progressToken"];

                //The protocol wants each progress value above the last, whatever a tool reports
                //...and none after the answer (a heartbeat timer can still be ticking as the tool returns)
                double reported = 0;
                bool finished = false;
                object progressLock = new object();
                McpCall call = new McpCall(tool, arguments, cancel, progressToken == null ? null : (Action<string, double?, double?>)((text, done, total) =>
                {
                    lock (progressLock)
                    {
                        if (finished) return;
                        double value = done ?? reported + 1;
                        if (value <= reported) value = reported + 0.001;
                        reported = value;
                        JObject progress = new JObject() { ["progressToken"] = progressToken, ["progress"] = value, ["message"] = text };
                        if (total != null && total >= value) progress["total"] = total;
                        Send(new JObject() { ["jsonrpc"] = "2.0", ["method"] = "notifications/progress", ["params"] = progress });
                    }
                }));

                if (argumentsNote != null)
                    call.Note(argumentsNote);
                McpActivity.Entry entry = McpActivity.Begin(tool, arguments, call);
                JObject result = null;

                //Status reads answer straight away, whatever is running
                if (tool.Concurrent)
                {
                    try
                    {
                        McpActivity.Running(entry);
                        result = tool.Invoke(call);
                        return result;
                    }
                    finally
                    {
                        lock (progressLock) finished = true;
                        McpActivity.End(entry, result, cancel.IsCancellationRequested);
                    }
                }

                //Queue behind any tool already running (saying so), but let a cancellation take this one out of the queue
                Interlocked.Increment(ref McpActivity.Queued);
                try
                {
                    Stopwatch waited = Stopwatch.StartNew();
                    while (!_toolGate.Wait(TimeSpan.FromSeconds(3), cancel))
                        call.Progress("Waiting for " + (RunningTool ?? "the call before it") + " to finish (" + (int)waited.Elapsed.TotalSeconds + " s): one tool runs at a time; get_activity shows what it is doing.");
                }
                catch (OperationCanceledException)
                {
                    result = McpTool.ErrorResult("Cancelled before it started.", null, McpErrorCodes.Cancelled);
                    McpActivity.End(entry, result, true);
                    return result;
                }
                finally
                {
                    Interlocked.Decrement(ref McpActivity.Queued);
                }
                try
                {
                    RunningTool = tool.Name;
                    McpActivity.Running(entry);
                    RaiseStatus();

                    //The user opened another level since this client's last call: what it knows (ids, names) belongs to the old one
                    string refusal = CheckLevel(tool, call);
                    if (refusal != null)
                    {
                        result = McpTool.ErrorResult(refusal, null, McpErrorCodes.LevelChanged);
                        return result;
                    }
                    //The client knows the level as this call found it, even if the user opens another while it runs (the next
                    //change is then refused, as for a switch between calls) - unless the call opens one itself (see the finally)
                    lock (_levelLock)
                    {
                        if (_knownGeneration < 0)
                        {
                            _knownGeneration = McpSession.Generation;
                            _knownLevel = McpSession.LevelName;
                        }
                    }

                    //The very same change asked for again after the client gave up waiting on it (a timeout and a retry): it
                    //has already been made, so its result is given rather than making it twice - while nothing since has made
                    //that result untrue (the level re-opened, its step undone, a save's level changed again)
                    string key = tool.ReadOnly ? null : tool.Name + " " + McpJson.Canonical(arguments);
                    if (key != null && McpActivity.TakeUnanswered(key, out McpActivity.Kept earlier))
                    {
                        string undone = earlier.Undone();
                        if (undone != null)
                        {
                            result = McpTool.ErrorResult("This same " + tool.Name + " call already ran (it finished at " + earlier.At.ToString("HH:mm:ss") + ", after the client stopped waiting for it), " +
                                "and " + undone + " since. Nothing was done: call it again to run it anew.", null, McpErrorCodes.Conflict);
                            return result;
                        }
                        if (earlier.StillTrue(tool))
                        {
                            result = (JObject)earlier.Result.DeepClone();
                            JArray content = result["content"] as JArray ?? new JArray();
                            content.Insert(0, new JObject()
                            {
                                ["type"] = "text",
                                ["text"] = "Note: this same " + tool.Name + " call already ran (it finished at " + earlier.At.ToString("HH:mm:ss") + ", after the client stopped waiting for it), " +
                                    "so it was not run again: this is its result. Call it again to run it anew.",
                            });
                            result["content"] = content;
                            return result;
                        }
                    }

                    result = tool.Invoke(call);
                    //Nobody is waiting for this answer: keep it for a retry of the same call
                    if (key != null && cancel.IsCancellationRequested && result["isError"]?.Type == JTokenType.Boolean && !(bool)result["isError"])
                        McpActivity.KeepUnanswered(key, result, entry, call);
                    return result;
                }
                finally
                {
                    lock (progressLock) finished = true;
                    RunningTool = null;
                    //A call that opened a level leaves the client knowing that one; any other leaves what it knew alone
                    if (call.OpenedLevel)
                    {
                        lock (_levelLock)
                        {
                            _knownGeneration = McpSession.Generation;
                            _knownLevel = McpSession.LevelName;
                        }
                    }
                    McpActivity.End(entry, result, cancel.IsCancellationRequested);
                    _toolGate.Release();
                    RaiseStatus();
                }
            }

            /// <summary>
            /// Null, or why this call is refused: the user opened another level (or re-opened this one, discarding changes)
            /// since this client's last call, and the call would change it. The next call goes ahead. A read-only call
            /// gets a note instead.
            /// </summary>
            private string CheckLevel(McpTool tool, McpCall call)
            {
                lock (_levelLock)
                {
                    int generation = McpSession.Generation;
                    string before = _knownLevel, now = McpSession.LevelName;
                    if (_knownGeneration < 0 || generation == _knownGeneration || LevelOpeners.Contains(tool.Name))
                        return null;
                    _knownGeneration = generation;
                    _knownLevel = now;
                    if (now == null || before == null)
                        return null;
                    string what = string.Equals(now, before, StringComparison.OrdinalIgnoreCase)
                        ? "The user re-opened " + now + " since your last call: unsaved changes made before that are gone, and ids you had may no longer exist."
                        : "The open level is now " + now + " (the user opened it); your earlier calls worked in " + before + ". Ids and names you have from before belong to " + before + ".";
                    if (tool.ReadOnly)
                    {
                        call.Note(what);
                        return null;
                    }
                    return what + " Nothing was done: call again to go ahead in " + now + (string.Equals(now, before, StringComparison.OrdinalIgnoreCase) ? "" : ", or load_level " + before + " to go back to it") + ".";
                }
            }

            private static bool TryParseObject(string text, out JObject parsed)
            {
                parsed = null;
                if (string.IsNullOrWhiteSpace(text) || text.TrimStart()[0] != '{')
                    return false;
                try
                {
                    parsed = ParseObject(text);
                    return true;
                }
                catch { return false; }
            }

            private void Send(JObject message)
            {
                if (_closed) return;
                string text = message.ToString(Formatting.None);
                lock (_writeLock)
                {
                    try { _writer.WriteLine(text); }
                    catch (Exception) { Close(); }
                }
            }
        }

        private static JObject ResultMessage(JToken id, JObject result) => new JObject() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
        private static JObject ErrorMessage(JToken id, int code, string message) => new JObject() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JObject() { ["code"] = code, ["message"] = message } };
    }

    /// <summary>A JSON-RPC level failure: an unknown method or tool, rather than a tool that failed.</summary>
    internal sealed class McpProtocolException : Exception
    {
        public int Code { get; }
        public McpProtocolException(int code, string message) : base(message) { Code = code; }
    }

    /// <summary>
    /// This editor as the tools' clients see it: which process it is, and which level it has open (counting every
    /// open and close, so a client can tell the user switched level between its calls).
    /// </summary>
    internal static class McpSession
    {
        /// <summary>The key of the session record in each tools/call result's _meta. Must match OpenCAGE.MCP's Program.SessionMetaKey.</summary>
        public const string MetaKey = "opencage/session";

        /// <summary>How the undo history marks the steps tools made (UndoStack.OriginProvider).</summary>
        public const string AssistantOrigin = "assistant";

        public static readonly int ProcessId;
        public static readonly DateTime Started;

        private static int _generation;
        private static volatile string _levelName;
        private static bool _hooked;

        static McpSession()
        {
            using (Process self = Process.GetCurrentProcess())
            {
                ProcessId = self.Id;
                try { Started = self.StartTime; }
                catch { Started = DateTime.Now; }
            }
        }

        /// <summary>Goes up each time a level is opened or closed.</summary>
        public static int Generation => Volatile.Read(ref _generation);

        /// <summary>The open level's name, or null.</summary>
        public static string LevelName => _levelName;

        /// <summary>When the open level was opened (or the last one closed).</summary>
        public static DateTime LevelOpened { get; private set; } = DateTime.Now;

        /// <summary>Follow the editor's levels (UI thread, once).</summary>
        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            _levelName = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name;
            Singleton.OnLevelLoaded += content =>
            {
                _levelName = content?.Level?.Name;
                LevelOpened = DateTime.Now;
                Interlocked.Increment(ref _generation);
            };
            Singleton.OnLevelClosing += content =>
            {
                _levelName = null;
                LevelOpened = DateTime.Now;
                Interlocked.Increment(ref _generation);
                //What changes kept for a retry did was done to this level
                McpActivity.DropUnanswered();
            };
        }

        public static JObject Describe()
        {
            return new JObject()
            {
                ["pid"] = ProcessId,
                ["started"] = Started.ToString("yyyy-MM-dd HH:mm:ss"),
                ["level"] = _levelName,
                ["unsaved_changes"] = _levelName != null && DirtyTracker.IsDirty,
            };
        }
    }

    /// <summary>
    /// What the tools have been doing: the call running, how many wait, and the last 50 calls with their outcome and
    /// undo step (get_activity reads it). Also keeps the results of changes nobody was waiting for any more, so a
    /// client that timed out and retries the same call gets the first result instead of a second change.
    /// </summary>
    internal static class McpActivity
    {
        public sealed class Entry
        {
            public long Id;
            public string Tool;
            public string Arguments;
            public bool ReadOnly;
            public DateTime Queued;
            public DateTime? Started;
            public DateTime? Finished;
            /// <summary>queued, running, ok, error or cancelled.</summary>
            public string Outcome = "queued";
            public string Error;
            /// <summary>The undo step it made, if any.</summary>
            public string UndoStep;
            public bool ClientGaveUp;
            public string Level;
            public McpCall Call;
            public int RecordCountBefore;
            /// <summary>McpSession.Generation as it started: the opening of the level it worked in.</summary>
            public int GenerationBefore;
            /// <summary>It said it changed nothing: a dry run, or a result marked unchanged (get_undo_history leaves these out).</summary>
            public bool NoChange;
        }

        private const int Keep = 50;
        private static readonly object _lock = new object();
        private static readonly LinkedList<Entry> _recent = new LinkedList<Entry>();
        private static long _nextId;

        /// <summary>Calls waiting their turn now.</summary>
        public static int Queued;

        public static Entry Begin(McpTool tool, JObject arguments, McpCall call)
        {
            string text = arguments == null || arguments.Count == 0 ? "" : arguments.ToString(Formatting.None);
            if (text.Length > 300) text = text.Substring(0, 300) + "... (" + text.Length + " characters)";
            Entry entry = new Entry() { Id = Interlocked.Increment(ref _nextId), Tool = tool.Name, Arguments = text, ReadOnly = tool.ReadOnly, Queued = DateTime.Now, Call = call };
            lock (_lock)
            {
                _recent.AddFirst(entry);
                while (_recent.Count > Keep)
                    _recent.RemoveLast();
            }
            return entry;
        }

        public static void Running(Entry entry)
        {
            entry.RecordCountBefore = Undo.UndoStack.Current.RecordCount;
            entry.GenerationBefore = McpSession.Generation;
            entry.Started = DateTime.Now;
            entry.Outcome = "running";
        }

        public static void End(Entry entry, JObject result, bool clientGaveUp)
        {
            entry.Finished = DateTime.Now;
            entry.ClientGaveUp = clientGaveUp;
            entry.Level = entry.Call?.Level;
            bool failed = result == null || (result["isError"]?.Type == JTokenType.Boolean && (bool)result["isError"]);
            string text = (string)result?["content"]?.LastOrDefault(o => (string)o["type"] == "text")?["text"];
            if (failed)
            {
                entry.Outcome = text != null && text.StartsWith("[" + McpErrorCodes.Cancelled + "]") ? "cancelled" : "error";
                entry.Error = text == null ? "no result" : text.Length > 300 ? text.Substring(0, 300) + "..." : text;
            }
            else
            {
                entry.Outcome = "ok";
                entry.NoChange = SaysNoChange(entry.Call?.Args, text);
            }
            if (entry.Started != null && Undo.UndoStack.Current.RecordCount != entry.RecordCountBefore)
            {
                try { entry.UndoStep = Undo.UndoStack.Current.UndoLabel; }
                catch { }
            }
            entry.Call = null;
        }

        /// <summary>
        /// Whether a call's arguments or result say it changed nothing: dry_run asked for or reported, or a top-level
        /// "unchanged" (text or true) or "changed": false / 0, as the tools mark a call that found nothing to do.
        /// </summary>
        private static bool SaysNoChange(JObject arguments, string text)
        {
            if (arguments?["dry_run"] is JValue asked && asked.Type == JTokenType.Boolean && (bool)asked)
                return true;
            if (string.IsNullOrEmpty(text) || text[0] != '{')
                return false;
            JObject result;
            try { result = JObject.Parse(text); }
            catch { return false; }
            JToken dry = result["dry_run"];
            if (dry != null && !(dry.Type == JTokenType.Boolean && !(bool)dry) && dry.Type != JTokenType.Null)
                return true;
            JToken unchanged = result["unchanged"];
            if (unchanged != null && (unchanged.Type == JTokenType.String || (unchanged.Type == JTokenType.Boolean && (bool)unchanged)))
                return true;
            JToken changed = result["changed"];
            if (changed != null && ((changed.Type == JTokenType.Boolean && !(bool)changed) || (changed.Type == JTokenType.Integer && (long)changed == 0) || (changed is JArray list && list.Count == 0)))
                return true;
            return false;
        }

        /// <summary>The calls, newest first.</summary>
        public static List<Entry> Recent()
        {
            lock (_lock)
                return _recent.ToList();
        }

        /// <summary>A change kept for a retry: its result, and what that result is true of.</summary>
        public sealed class Kept
        {
            public string Key;
            public JObject Result;
            public DateTime At;
            //The opening of the level it worked in (McpSession.Generation): a result from another is never given
            public int Generation;
            //A mark just above the undo step it made, or null if it made none
            public object Step;
            //The history and the unsaved state as it finished: what a save's result is true of
            public int Records;
            public bool Dirty;

            /// <summary>Null, or what became of the change since it was made (its step undone): its result is then untrue.</summary>
            public string Undone()
            {
                if (Step == null || Generation != McpSession.Generation)
                    return null;
                bool there = OnUi(() => Undo.UndoStack.Current.StillHas(Step), true);
                return there ? null : "its undo step has been undone (or has dropped off the history)";
            }

            /// <summary>Whether the result still describes the level: the same opening of it, and for a save, no change since.</summary>
            public bool StillTrue(McpTool tool)
            {
                if (Generation != McpSession.Generation)
                    return false;
                if (tool.Name == "save_level" && (DirtyTracker.IsDirty != Dirty || Undo.UndoStack.Current.RecordCount != Records))
                    return false;
                return true;
            }
        }
        private static readonly List<Kept> _unanswered = new List<Kept>();
        private static readonly TimeSpan UnansweredFor = TimeSpan.FromMinutes(10);

        /// <summary>A change finished after its client stopped waiting: kept for ten minutes for a retry of the same call.</summary>
        public static void KeepUnanswered(string key, JObject result, Entry entry, McpCall call)
        {
            Undo.UndoStack stack = Undo.UndoStack.Current;
            int records = stack.RecordCount;
            object step = null;
            if (records != entry.RecordCountBefore)
            {
                step = OnUi(() => stack.Mark(), null);
                //Without it, whether the step was undone by the time of a retry cannot be told: the retry runs anew
                if (step == null)
                    return;
            }
            Kept kept = new Kept()
            {
                Key = key,
                Result = result,
                At = DateTime.Now,
                //What it did belongs to the level it started in - unless it opened a level itself
                Generation = call.OpenedLevel ? McpSession.Generation : entry.GenerationBefore,
                Step = step,
                Records = records,
                Dirty = DirtyTracker.IsDirty,
            };
            lock (_lock)
            {
                _unanswered.RemoveAll(o => o.Key == key || DateTime.Now - o.At > UnansweredFor);
                _unanswered.Add(kept);
                while (_unanswered.Count > 10)
                    _unanswered.RemoveAt(0);
            }
        }

        /// <summary>The kept result of the same call, taken (a second retry runs it anew).</summary>
        public static bool TakeUnanswered(string key, out Kept kept)
        {
            lock (_lock)
            {
                _unanswered.RemoveAll(o => DateTime.Now - o.At > UnansweredFor);
                kept = _unanswered.FirstOrDefault(o => o.Key == key);
                if (kept == null)
                    return false;
                _unanswered.Remove(kept);
                return true;
            }
        }

        /// <summary>The level is closing: no kept result is true of the next one (any thread).</summary>
        public static void DropUnanswered()
        {
            lock (_lock)
                _unanswered.Clear();
        }

        /// <summary>A read of the undo history, made on the UI thread (it is not safe to read from any other); <paramref name="fallback"/> if it cannot be.</summary>
        private static T OnUi<T>(Func<T> read, T fallback)
        {
            try
            {
                CommandsEditor editor = Singleton.Editor;
                if (editor == null || editor.IsDisposed)
                    return fallback;
                if (!editor.InvokeRequired)
                    return read();
                T value = fallback;
                editor.Invoke(new Action(() => value = read()));
                return value;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>Changes kept for a retry (tool names and when they finished), for get_activity.</summary>
        public static List<(string tool, DateTime at)> UnansweredChanges()
        {
            lock (_lock)
                return _unanswered.Where(o => DateTime.Now - o.At <= UnansweredFor).Select(o => (o.Key.Split(' ')[0], o.At)).ToList();
        }
    }
}
