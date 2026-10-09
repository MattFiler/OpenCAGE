using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// The OpenCAGE MCP server, as an AI client (Claude Desktop, Claude Code, ...) starts it: a console
    /// program speaking the Model Context Protocol over stdin/stdout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tools themselves run inside the OpenCAGE editor, against the level it has open, so every change
    /// shows up in the editor as it is made, goes through its undo history, and reaches its viewport. This
    /// program is the bridge between the two: it relays each message to the editor over a local named pipe
    /// (<see cref="PipeName"/>, served by OpenCAGE's McpServer), starting OpenCAGE.exe (from beside this
    /// program) the first time a tool is called when no OpenCAGE is running, and relays the editor's answers back.
    /// </para>
    /// <para>
    /// It answers the protocol's handshake itself, and the tool list and prompts from the copy the editor last
    /// left (McpServer.WriteToolCache, which also holds the editor's instructions), so a client starting up
    /// neither waits for nor starts the editor. The instructions have one source, OpenCAGE's McpTools.Instructions:
    /// from that copy when this very OpenCAGE.exe wrote it, else read out of the exe itself; the bridge keeps no
    /// text of its own beyond a two-line fallback for when OpenCAGE.exe cannot be read at all. Messages for the editor queue on a sender
    /// thread; pings are answered at once whatever it is doing. When the editor answering is not the one that
    /// answered before (it was restarted, or crashed), the next tool result says so and what was lost.
    /// </para>
    /// <para>
    /// Arguments: <c>--editor &lt;path&gt;</c> to use an OpenCAGE.exe somewhere else (or set OPENCAGE_EXE);
    /// <c>--no-launch</c> to only ever attach to an OpenCAGE that is already open; <c>--launch-at-start</c> to
    /// start OpenCAGE at the handshake rather than on the first tool call; <c>--toolsets core,script,...</c> to
    /// offer only those toolsets at first (a load_toolsets tool offers the rest; every tool stays callable);
    /// anything after <c>--</c> is passed to OpenCAGE when this starts it (e.g. <c>-- -disable_viewport</c>).
    /// </para>
    /// </remarks>
    internal static class Program
    {
        /// <summary>Per logon session, like OpenCAGE's other pipes. Must match McpServer.PipeName.</summary>
        public static readonly string PipeName = "OpenCAGE.MCP." + Process.GetCurrentProcess().SessionId;
        private const string ServerMutex = @"Local\OpenCAGE_MCP_Server";
        private const string EditorMutex = @"Local\OpenCAGE_PrimaryInstance";
        //Held while one bridge checks for, starts and waits on an OpenCAGE, so two starting at once start one
        private const string LaunchMutex = @"Local\OpenCAGE_MCP_Launch";
        //Exists while an OpenCAGE is open with Allow AI Assistants turned off. Must match OpenCAGE's CommandsEditor.
        private const string OffEvent = @"Local\OpenCAGE_MCP_Off";
        //Where each tools/call result says which editor answered. Must match OpenCAGE's McpSession.MetaKey.
        private const string SessionMetaKey = "opencage/session";
        //Where each tool says which toolset it is in. Must match OpenCAGE's McpToolsets.MetaKey.
        private const string ToolsetMetaKey = "opencage/toolset";
        //The bridge's own tool, offered while only some toolsets are
        private const string LoadToolsetsTool = "load_toolsets";

        private const string LatestProtocol = "2025-06-18";
        private static readonly string[] SupportedProtocols = { "2025-06-18", "2025-03-26", "2024-11-05" };

        private static readonly object _stdoutLock = new object();
        private static StreamWriter _stdout;

        //Connecting can take minutes (the editor starting): it has its own lock, so nothing else waits on it
        private static readonly object _connectLock = new object();
        private static readonly object _pipeLock = new object();
        private static volatile NamedPipeClientStream _pipe;
        private static StreamWriter _pipeWriter;
        //Each connection to an editor is a generation: a request is answered with an error only if the pipe it went down closes
        private static int _generation;
        private static readonly HashSet<int> _closedGenerations = new HashSet<int>();

        /// <summary>A request of the client's that the editor has yet to answer.</summary>
        private sealed class Pending
        {
            public JToken Id;
            public string Method;
            /// <summary>The connection it was written to; 0 while it waits to be sent.</summary>
            public volatile int Generation;
        }
        private static readonly ConcurrentDictionary<string, Pending> _pending = new ConcurrentDictionary<string, Pending>();

        private sealed class Outgoing
        {
            public JObject Message;
            /// <summary>Worth starting OpenCAGE for (a tool call, or a tool list with no copy to answer from).</summary>
            public bool Launch;
        }
        private static readonly BlockingCollection<Outgoing> _outbox = new BlockingCollection<Outgoing>();

        private static string _editorPath;
        private static string _editorArguments = "";
        private static bool _mayLaunch = true;
        private static bool _launchAtStart = false;
        private static Process _launched;

        //--toolsets: the ones offered (null: all), and those load_toolsets added since
        private static HashSet<string> _toolsets;
        private static readonly object _toolsetLock = new object();

        //The editor that answered the last tool call (its _meta session record), to notice a restart
        private static readonly object _sessionLock = new object();
        private static JObject _lastSession;

        //Written by the editor when it starts serving (McpServer.WriteToolCache), stamped with its executable
        private static string ToolCachePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenCAGE", "mcp_tools.json");

        private static int Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--")
                {
                    _editorArguments = string.Join(" ", args.Skip(i + 1).Select(Quote));
                    break;
                }
                if (args[i] == "--editor" && i + 1 < args.Length) _editorPath = args[++i];
                else if (args[i] == "--no-launch") _mayLaunch = false;
                else if (args[i] == "--launch-at-start") _launchAtStart = true;
                else if (args[i] == "--toolsets" && i + 1 < args.Length)
                {
                    HashSet<string> sets = new HashSet<string>(args[++i].Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim().ToLowerInvariant()));
                    _toolsets = sets.Contains("all") ? null : sets;
                    if (_toolsets != null) _toolsets.Add("core");
                }
            }
            if (string.IsNullOrEmpty(_editorPath))
                _editorPath = Environment.GetEnvironmentVariable("OPENCAGE_EXE");
            if (string.IsNullOrEmpty(_editorPath))
                _editorPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "OpenCAGE.exe");

            //Protocol messages only on stdout; anything else goes to stderr, which clients keep as the server's log
            UTF8Encoding utf8 = new UTF8Encoding(false);
            _stdout = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
            Console.SetOut(Console.Error);
            StreamReader stdin = new StreamReader(Console.OpenStandardInput(), utf8);

            Log("OpenCAGE MCP bridge " + Assembly.GetExecutingAssembly().GetName().Version + " (pipe " + PipeName + ", editor " + _editorPath +
                (_toolsets == null ? "" : ", toolsets " + string.Join(",", _toolsets)) + ")");
            new Thread(SendLoop) { IsBackground = true, Name = "To OpenCAGE" }.Start();

            string line;
            while ((line = stdin.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                JObject message;
                try
                {
                    message = ParseObject(line);
                }
                catch (Exception e)
                {
                    WriteOut(Error(null, -32700, "Parse error: " + e.Message));
                    continue;
                }
                try
                {
                    Handle(message);
                }
                catch (Exception e)
                {
                    Log("Failed to handle a message: " + e);
                    if (message["id"] != null && message["method"] != null)
                        WriteOut(Error(message["id"], -32603, e.Message));
                }
            }

            Log("The client closed stdin; exiting");
            _outbox.CompleteAdding();
            lock (_pipeLock) { try { _pipe?.Dispose(); } catch { } }
            return 0;
        }

        private static bool Connected
        {
            get
            {
                NamedPipeClientStream pipe = _pipe;
                return pipe != null && pipe.IsConnected;
            }
        }

        private static void Handle(JObject message)
        {
            string method = (string)message["method"];
            JToken id = message["id"];
            switch (method)
            {
                case "initialize":
                    {
                        string requested = (string)message["params"]?["protocolVersion"];
                        JObject cache = ReadCache(out bool current);
                        JObject capabilities = new JObject()
                        {
                            ["tools"] = new JObject() { ["listChanged"] = _toolsets != null },
                            ["prompts"] = new JObject() { ["listChanged"] = false },
                        };
                        WriteOut(Result(id, new JObject()
                        {
                            ["protocolVersion"] = Array.IndexOf(SupportedProtocols, requested) >= 0 ? requested : LatestProtocol,
                            ["capabilities"] = capabilities,
                            ["serverInfo"] = new JObject() { ["name"] = "opencage", ["title"] = "OpenCAGE", ["version"] = EditorVersion(cache, current) },
                            ["instructions"] = EditorInstructions(cache, current),
                        }));
                        //Only attach to an OpenCAGE already serving: one is started on the first tool call, not because a client started
                        //(clients start every configured server with each session, whatever it is about)
                        StartInBackground(_launchAtStart);
                        return;
                    }
                case "ping":
                    WriteOut(Result(id, new JObject()));
                    return;
                case "notifications/initialized":
                    return;
                case "notifications/cancelled":
                    {
                        //The client has stopped waiting: the editor never answers a cancelled request, and nor do we
                        string cancelled = message["params"]?["requestId"]?.ToString(Formatting.None);
                        if (cancelled != null) _pending.TryRemove(cancelled, out _);
                        break;
                    }
                case "tools/list":
                    {
                        //While the editor is not connected, the list it gave last time stands in for it
                        if (!Connected)
                        {
                            JObject cache = ReadCache(out bool current);
                            if (current && cache["tools"] is JArray tools)
                            {
                                WriteOut(Result(id, new JObject() { ["tools"] = OfferedTools(tools, cache) }));
                                StartInBackground(false);
                                return;
                            }
                        }
                        Forward(message, launch: true);
                        return;
                    }
                case "tools/call":
                    {
                        if ((string)message["params"]?["name"] == LoadToolsetsTool && _toolsets != null)
                        {
                            WriteOut(Result(id, LoadToolsets(message["params"]?["arguments"] as JObject)));
                            return;
                        }
                        Forward(message, launch: true);
                        return;
                    }
                case "prompts/list":
                case "prompts/get":
                    {
                        JObject cache = ReadCache(out bool current);
                        JArray prompts = cache?["prompts"] as JArray;
                        //The copy is this very OpenCAGE.exe's, or a connected editor answers for itself (another build's prompts may differ)
                        if ((prompts == null || !current) && Connected)
                            break;
                        prompts = prompts ?? new JArray();
                        if (method == "prompts/list")
                        {
                            WriteOut(Result(id, new JObject() { ["prompts"] = ListPrompts(prompts) }));
                            return;
                        }
                        JObject prompt = GetPrompt(prompts, (string)message["params"]?["name"], message["params"]?["arguments"] as JObject, out string problem);
                        WriteOut(prompt != null ? Result(id, prompt) : Error(id, -32602, problem));
                        return;
                    }
                case "resources/list":
                    WriteOut(Result(id, new JObject() { ["resources"] = new JArray() }));
                    return;
                case "resources/templates/list":
                    WriteOut(Result(id, new JObject() { ["resourceTemplates"] = new JArray() }));
                    return;
                case "logging/setLevel":
                    WriteOut(Result(id, new JObject()));
                    return;
                case "completion/complete":
                    WriteOut(Result(id, new JObject() { ["completion"] = new JObject() { ["values"] = new JArray(), ["hasMore"] = false } }));
                    return;
            }

            //A request nothing here answers goes to an editor that is there; none is started for it
            if (method != null && id != null && method != "notifications/cancelled" && !Connected)
            {
                WriteOut(Error(id, -32601, "Method not found: " + method));
                return;
            }
            Forward(message, launch: false);
        }

        /// <summary>Hand a message to the sender thread; a request (a method and an id) waits for the editor's answer.</summary>
        private static void Forward(JObject message, bool launch)
        {
            JToken id = message["id"];
            string method = (string)message["method"];
            if (id != null && method != null)
                _pending[id.ToString(Formatting.None)] = new Pending() { Id = id, Method = method };
            _outbox.Add(new Outgoing() { Message = message, Launch = launch });
        }

        private static void StartInBackground(bool launch)
        {
            new Thread(() =>
            {
                try { EnsureEditor(launch); }
                catch (Exception e) { if (launch) Log(e.Message); }
            }) { IsBackground = true }.Start();
        }

        private static void SendLoop()
        {
            foreach (Outgoing outgoing in _outbox.GetConsumingEnumerable())
            {
                JObject message = outgoing.Message;
                JToken id = message["id"];
                string key = id?.ToString(Formatting.None);
                bool request = id != null && message["method"] != null;
                //Not worth starting (or waiting minutes for) OpenCAGE: with no editor there is nothing to tell
                if (!outgoing.Launch && !Connected)
                {
                    if (request && _pending.TryRemove(key, out _))
                        WriteOut(Error(id, -32603, "OpenCAGE is not open."));
                    continue;
                }
                //A request cancelled while it waited here is not sent at all
                if (request && !_pending.ContainsKey(key))
                    continue;
                try
                {
                    EnsureEditor(outgoing.Launch);
                    int generation;
                    lock (_pipeLock)
                    {
                        _pipeWriter.WriteLine(message.ToString(Formatting.None));
                        generation = _generation;
                    }
                    if (request && _pending.TryGetValue(key, out Pending pending))
                    {
                        pending.Generation = generation;
                        //It went down a pipe that closed as it was written: nothing will answer it there
                        bool closed;
                        lock (_closedGenerations) closed = _closedGenerations.Contains(generation);
                        if (closed && _pending.TryRemove(key, out _))
                            WriteOut(Error(id, -32603, ClosedMessage));
                    }
                }
                catch (Exception e)
                {
                    Log("Could not reach OpenCAGE: " + e.Message);
                    if (request && _pending.TryRemove(key, out _))
                        WriteOut(Error(id, -32603, "OpenCAGE is not available: " + e.Message));
                }
            }
        }

        private const string ClosedMessage = "OpenCAGE closed before answering (it was closed, or crashed): whether the call did anything is unknown, and unsaved changes are lost. " +
            "It is started again on the next tool call; check get_editor_state then.";

        /// <summary>Connect to OpenCAGE's pipe; when none is serving, start OpenCAGE first if <paramref name="launch"/>, else fail.</summary>
        private static void EnsureEditor(bool launch)
        {
            lock (_connectLock)
            {
                if (_pipe != null && _pipe.IsConnected)
                    return;
                lock (_pipeLock)
                {
                    _pipe?.Dispose();
                    _pipe = null;
                }

                NamedPipeClientStream pipe = TryConnect(300);
                if (pipe == null)
                {
                    if (!launch)
                        throw new InvalidOperationException("OpenCAGE is not open.");
                    //Two bridges starting together (a client that starts its servers side by side) must start one OpenCAGE
                    using (Mutex mutex = new Mutex(false, LaunchMutex))
                    {
                        bool held;
                        try { held = mutex.WaitOne(TimeSpan.FromMinutes(5)); }
                        catch (AbandonedMutexException) { held = true; }
                        try
                        {
                            //Whoever held it may have started one while this waited
                            pipe = TryConnect(300) ?? LaunchOrWait();
                        }
                        finally
                        {
                            if (held)
                                try { mutex.ReleaseMutex(); } catch { }
                        }
                    }
                }

                UTF8Encoding utf8 = new UTF8Encoding(false);
                int generation;
                lock (_pipeLock)
                {
                    _pipeWriter = new StreamWriter(pipe, utf8) { AutoFlush = true, NewLine = "\n" };
                    _pipe = pipe;
                    generation = ++_generation;
                }
                StreamReader reader = new StreamReader(pipe, utf8);
                new Thread(() => PumpFromEditor(reader, pipe, generation)) { IsBackground = true, Name = "From OpenCAGE" }.Start();
                Log("Connected to OpenCAGE");
            }
        }

        /// <summary>Wait for the OpenCAGE that is running to serve, or start one and wait for that.</summary>
        private static NamedPipeClientStream LaunchOrWait()
        {
            const string turnedOff = "OpenCAGE is open but is not accepting AI assistants. Turn on Options > Misc > Allow AI Assistants (MCP) in OpenCAGE.";
            TimeSpan wait;
            if (IsHeld(ServerMutex))
                wait = TimeSpan.FromSeconds(30); //it serves, but every instance of the pipe is taken for now
            else if (IsHeld(EditorMutex))
            {
                if (Exists(OffEvent))
                    throw new InvalidOperationException(turnedOff);
                //OpenCAGE is running without serving yet: still starting up
                wait = TimeSpan.FromSeconds(60);
                Log("OpenCAGE is running but not accepting AI assistants yet; waiting");
            }
            else
            {
                if (!_mayLaunch)
                    throw new InvalidOperationException("OpenCAGE is not open (and --no-launch was given). Open OpenCAGE and try again.");
                if (!File.Exists(_editorPath))
                    throw new FileNotFoundException("OpenCAGE.exe was not found at " + _editorPath + ". Pass --editor <path to OpenCAGE.exe>, or set OPENCAGE_EXE.");
                if (_launched == null || _launched.HasExited)
                {
                    Log("Starting " + _editorPath + " " + _editorArguments);
                    //Through the shell, so the editor inherits none of our handles: our stdout is the protocol, and
                    //OpenCAGE writes its log to whatever console it is handed
                    _launched = Process.Start(new ProcessStartInfo(_editorPath, _editorArguments)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(_editorPath)),
                    });
                }
                //A first start loads the game's global data: give it time. Steam may restart it through itself, so the process exiting is not failure.
                wait = TimeSpan.FromMinutes(4);
            }

            NamedPipeClientStream pipe = null;
            Stopwatch waited = Stopwatch.StartNew();
            while (pipe == null && waited.Elapsed < wait)
            {
                //An OpenCAGE that has come up with the option off will not serve until someone turns it on
                if (!IsHeld(ServerMutex) && Exists(OffEvent))
                    throw new InvalidOperationException(turnedOff);
                pipe = TryConnect(1000);
            }
            if (pipe == null)
            {
                if (IsHeld(EditorMutex) && !IsHeld(ServerMutex))
                    throw new InvalidOperationException(turnedOff);
                throw new TimeoutException("OpenCAGE did not start accepting AI assistants in time" + (_launched != null && _launched.HasExited ? " (it exited with code " + _launched.ExitCode + ")" : "") + ".");
            }
            return pipe;
        }

        /// <summary>Whether a named event exists (some process holds it open).</summary>
        private static bool Exists(string eventName)
        {
            try
            {
                if (!EventWaitHandle.TryOpenExisting(eventName, out EventWaitHandle handle))
                    return false;
                handle.Dispose();
                return true;
            }
            catch (UnauthorizedAccessException) { return true; }
            catch { return false; }
        }

        private static NamedPipeClientStream TryConnect(int timeout)
        {
            //Identification only: whatever serves the pipe learns who connected, but cannot act as us
            NamedPipeClientStream pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
            try
            {
                pipe.Connect(timeout);
                //Anyone can create a pipe of this name first: only talk to one this user (or this user elevated) created
                if (!OwnedByThisUser(pipe))
                {
                    pipe.Dispose();
                    throw new InvalidOperationException("The OpenCAGE connection (" + PipeName + ") is held by a program running as another user. Close it, or run OpenCAGE and your AI client as the same user.");
                }
                return pipe;
            }
            catch (TimeoutException)
            {
                pipe.Dispose();
                return null;
            }
            catch (IOException)
            {
                pipe.Dispose();
                Thread.Sleep(timeout);
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                pipe.Dispose();
                throw new InvalidOperationException("An OpenCAGE run by another user (or as administrator) holds the connection. Run OpenCAGE and your AI client as the same user.");
            }
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

        /// <summary>Whether another process holds a named mutex.</summary>
        private static bool IsHeld(string name)
        {
            try
            {
                using (Mutex existing = Mutex.OpenExisting(name))
                {
                    if (existing.WaitOne(0))
                    {
                        existing.ReleaseMutex();
                        return false;
                    }
                    return true;
                }
            }
            catch (AbandonedMutexException) { return false; }
            catch (WaitHandleCannotBeOpenedException) { return false; }
            catch (UnauthorizedAccessException) { return true; }
            catch (Exception) { return false; }
        }

        /// <summary>The editor's answers (and notifications) go out to the client: each answer once, to a request still waiting.</summary>
        private static void PumpFromEditor(StreamReader reader, NamedPipeClientStream pipe, int generation)
        {
            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        JObject message = ParseObject(line);
                        JToken id = message["id"];
                        if (id != null && message["method"] == null)
                        {
                            //An answer to a request nobody waits for (cancelled, or already answered with an error) is dropped
                            if (!_pending.TryRemove(id.ToString(Formatting.None), out Pending pending))
                                continue;
                            string rewritten = Rewrite(message, pending);
                            if (rewritten != null) line = rewritten;
                        }
                    }
                    catch (Exception e)
                    {
                        Log("Could not read OpenCAGE's message: " + e.Message);
                    }
                    WriteOutRaw(line);
                }
            }
            catch (Exception e)
            {
                Log("Lost OpenCAGE: " + e.Message);
            }
            Log("OpenCAGE closed the connection");
            lock (_closedGenerations) _closedGenerations.Add(generation);
            lock (_pipeLock)
            {
                if (_pipe == pipe)
                    _pipe = null;
            }
            //What went down this pipe will not be answered now (what waits to be sent goes to the next editor)
            foreach (KeyValuePair<string, Pending> waiting in _pending.ToList())
                if (waiting.Value.Generation == generation && _pending.TryRemove(waiting.Key, out Pending pending))
                    WriteOut(Error(pending.Id, -32603, ClosedMessage));
        }

        /// <summary>An answer as the client should see it (null: unchanged): the tool list filtered, a restart reported.</summary>
        private static string Rewrite(JObject message, Pending pending)
        {
            JObject result = message["result"] as JObject;
            if (result == null)
                return null;
            if (pending.Method == "tools/list" && result["tools"] is JArray tools)
            {
                result["tools"] = OfferedTools(tools, ReadCache(out _));
                return message.ToString(Formatting.None);
            }
            if (pending.Method == "tools/call" && result["_meta"]?[SessionMetaKey] is JObject session)
            {
                string note = null;
                lock (_sessionLock)
                {
                    if (_lastSession != null && (int?)_lastSession["pid"] != (int?)session["pid"])
                        note = RestartNote(_lastSession, session);
                    _lastSession = session;
                }
                if (note == null)
                    return null;
                JArray content = result["content"] as JArray ?? new JArray();
                content.Insert(0, new JObject() { ["type"] = "text", ["text"] = note });
                result["content"] = content;
                return message.ToString(Formatting.None);
            }
            return null;
        }

        /// <summary>What the assistant should know when a different OpenCAGE answers than last time.</summary>
        private static string RestartNote(JObject before, JObject now)
        {
            string level = (string)before["level"];
            bool unsaved = (bool?)before["unsaved_changes"] == true;
            string nowLevel = (string)now["level"];
            return "Note: OpenCAGE was restarted since your last call (it was process " + (int?)before["pid"] + (level == null ? "" : ", with " + level + " open" + (unsaved ? " and unsaved changes" : "")) + "). " +
                (unsaved ? "Those unsaved changes are lost. " : "") + "Its undo history is gone, and ids from before refer to nothing until the level is open again. " +
                (nowLevel == null ? "No level is open now: load_level opens one." : nowLevel + " is open now.");
        }

        #region Cache
        private static JObject _cache;
        private static string _cacheKey;

        /// <summary>The editor's last tool cache, or null; <paramref name="current"/>: it was written by this very OpenCAGE.exe.</summary>
        private static JObject ReadCache(out bool current)
        {
            current = false;
            try
            {
                FileInfo file = new FileInfo(ToolCachePath);
                if (!file.Exists) return null;
                string key = file.Length + "|" + file.LastWriteTimeUtc.Ticks;
                if (key != _cacheKey)
                {
                    _cache = JObject.Parse(File.ReadAllText(file.FullName));
                    _cacheKey = key;
                }
                //Only a list from this very OpenCAGE.exe: another build may offer other tools
                current = (string)_cache["editor"] == EditorStamp(_editorPath);
                return _cache;
            }
            catch { return null; }
        }

        private static string _instructionsFromExe;
        private static bool _instructionsRead;

        /// <summary>
        /// The editor's instructions (McpTools.Instructions): from its cache, else read from OpenCAGE.exe itself, so the
        /// bridge never carries a copy of its own that could drift.
        /// </summary>
        private static string EditorInstructions(JObject cache, bool current)
        {
            if (current && cache?["instructions"] is JValue fresh)
                return (string)fresh;
            if (!_instructionsRead)
            {
                _instructionsRead = true;
                try
                {
                    //From the bytes, so the file is not held open (OpenCAGE can be updated while a client runs this)
                    Assembly editor = Assembly.ReflectionOnlyLoad(File.ReadAllBytes(_editorPath));
                    Type tools = editor.GetType("OpenCAGE.MCP.McpTools", false);
                    _instructionsFromExe = tools?.GetField("Instructions", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetRawConstantValue() as string;
                }
                catch (Exception e)
                {
                    Log("Could not read the instructions from " + _editorPath + ": " + e.Message);
                }
            }
            if (_instructionsFromExe != null)
                return _instructionsFromExe;
            if (cache?["instructions"] is JValue stale)
                return (string)stale;
            return FallbackInstructions;
        }

        private static string EditorVersion(JObject cache, bool current)
        {
            if (current && !string.IsNullOrEmpty((string)cache?["version"]))
                return (string)cache["version"];
            try
            {
                if (File.Exists(_editorPath))
                    return FileVersionInfo.GetVersionInfo(_editorPath).ProductVersion ?? "";
            }
            catch { }
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        //Only when OpenCAGE.exe can be neither found nor read: the real text is the editor's
        private const string FallbackInstructions =
            "OpenCAGE is the level editor for Alien: Isolation. These tools drive the OpenCAGE window the user has open (it is started on first use). " +
            "Call get_editor_state first; each tool's description says what it does, whether it can be undone, and its units (positions in metres, Y up; rotations in degrees).";

        private static string EditorStamp(string path)
        {
            try
            {
                FileInfo exe = new FileInfo(path);
                return exe.FullName.ToUpperInvariant() + "|" + exe.Length + "|" + exe.LastWriteTimeUtc.Ticks;
            }
            catch { return ""; }
        }
        #endregion

        #region Toolsets
        /// <summary>
        /// The tools the client is offered: those of the toolsets asked for (all by default), without the toolset tags,
        /// and load_toolsets while some are held back.
        /// </summary>
        private static JArray OfferedTools(JArray tools, JObject cache)
        {
            HashSet<string> wanted;
            lock (_toolsetLock) wanted = _toolsets == null ? null : new HashSet<string>(_toolsets);
            JArray offered = new JArray();
            Dictionary<string, List<string>> heldBack = new Dictionary<string, List<string>>();
            foreach (JObject tool in tools.OfType<JObject>())
            {
                string toolset = (string)tool["_meta"]?[ToolsetMetaKey];
                JObject copy = (JObject)tool.DeepClone();
                if (copy["_meta"] is JObject meta)
                {
                    meta.Remove(ToolsetMetaKey);
                    if (meta.Count == 0) copy.Remove("_meta");
                }
                if (wanted != null && toolset != null && !wanted.Contains(toolset))
                {
                    if (!heldBack.TryGetValue(toolset, out List<string> names)) heldBack[toolset] = names = new List<string>();
                    names.Add((string)tool["name"]);
                    continue;
                }
                offered.Add(copy);
            }
            if (wanted != null && heldBack.Count != 0)
            {
                JObject described = cache?["toolsets"] as JObject;
                string list = string.Join("; ", heldBack.OrderBy(o => o.Key).Select(o =>
                    o.Key + " (" + ((string)described?[o.Key]?["description"] ?? "") + ": " + string.Join(", ", o.Value) + ")"));
                offered.Add(new JObject()
                {
                    ["name"] = LoadToolsetsTool,
                    ["title"] = "Load toolsets",
                    ["description"] = "Offer more of OpenCAGE's tools: only some toolsets were loaded, to keep the tool list small. Not loaded yet: " + list + ". " +
                        "Any tool here can also be called by name straight away.",
                    ["inputSchema"] = new JObject()
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                        {
                            ["toolsets"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" }, ["description"] = "Toolset names, or ['all']." },
                        },
                        ["required"] = new JArray("toolsets"),
                    },
                    ["annotations"] = new JObject() { ["readOnlyHint"] = true, ["idempotentHint"] = true, ["openWorldHint"] = false },
                });
            }
            return offered;
        }

        private static JObject LoadToolsets(JObject arguments)
        {
            JToken given = arguments?["toolsets"];
            List<string> asked = given is JArray array ? array.Select(o => ((string)o ?? "").Trim().ToLowerInvariant()).ToList()
                : given != null && given.Type == JTokenType.String ? ((string)given).Split(',').Select(o => o.Trim().ToLowerInvariant()).ToList() : new List<string>();
            asked.RemoveAll(string.IsNullOrEmpty);
            JObject known = ReadCache(out _)?["toolsets"] as JObject;
            if (asked.Count == 0)
                return ToolResult("[invalid_argument] 'toolsets' names the toolsets to load" + (known == null ? "" : ": " + string.Join(", ", known.Properties().Select(o => o.Name))) + ", or ['all'].", true);
            List<string> unknown = known == null ? new List<string>() : asked.Where(o => o != "all" && known[o] == null).ToList();
            if (unknown.Count != 0)
                return ToolResult("[not_found] No toolset " + string.Join(", ", unknown.Select(o => "'" + o + "'")) + ". There are: " + string.Join(", ", known.Properties().Select(o => o.Name)) + ".", true);
            lock (_toolsetLock)
            {
                if (asked.Contains("all")) _toolsets = null;
                else if (_toolsets != null) _toolsets.UnionWith(asked);
            }
            //The client asks for the list again, and gets the new tools
            WriteOut(new JObject() { ["jsonrpc"] = "2.0", ["method"] = "notifications/tools/list_changed" });
            List<string> added = known == null ? new List<string>() : (asked.Contains("all") ? known.Properties().Select(o => o.Name) : asked)
                .SelectMany(o => (known[o]?["tools"] as JArray)?.Select(t => (string)t) ?? Enumerable.Empty<string>()).ToList();
            return ToolResult("Loaded " + string.Join(", ", asked) + (added.Count == 0 ? "" : ": " + string.Join(", ", added)) +
                ". They are offered from now on; if your tool list does not show them, call them by name anyway.", false);
        }

        private static JObject ToolResult(string text, bool error) => new JObject()
        {
            ["content"] = new JArray(new JObject() { ["type"] = "text", ["text"] = text }),
            ["isError"] = error,
        };
        #endregion

        #region Prompts
        //The editor's prompts (McpPrompts) are data in its cache: '{argument}' in the template is the argument's value
        private static JArray ListPrompts(JArray prompts)
        {
            return new JArray(prompts.OfType<JObject>().Select(o => new JObject()
            {
                ["name"] = o["name"],
                ["title"] = o["title"],
                ["description"] = o["description"],
                ["arguments"] = new JArray(((o["arguments"] as JArray) ?? new JArray()).OfType<JObject>().Select(a => new JObject() { ["name"] = a["name"], ["description"] = a["description"], ["required"] = a["required"] })),
            }));
        }

        private static JObject GetPrompt(JArray prompts, string name, JObject arguments, out string problem)
        {
            problem = null;
            JObject prompt = prompts.OfType<JObject>().FirstOrDefault(o => string.Equals((string)o["name"], name, StringComparison.OrdinalIgnoreCase));
            if (prompt == null)
            {
                problem = "Unknown prompt: " + name + ". There are: " + string.Join(", ", prompts.Select(o => (string)o["name"])) + ".";
                return null;
            }
            string text = (string)prompt["template"] ?? "";
            foreach (JObject argument in ((prompt["arguments"] as JArray) ?? new JArray()).OfType<JObject>())
            {
                string key = (string)argument["name"];
                JToken given = arguments?[key];
                string value = given == null || given.Type == JTokenType.Null ? null : given.Type == JTokenType.String ? (string)given : given.ToString(Formatting.None);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if ((bool?)argument["required"] == true)
                    {
                        problem = "The " + name + " prompt needs '" + key + "': " + (string)argument["description"];
                        return null;
                    }
                    value = (string)argument["default"] ?? "";
                }
                text = text.Replace("{" + key + "}", value.Trim());
            }
            return new JObject()
            {
                ["description"] = prompt["description"],
                ["messages"] = new JArray(new JObject() { ["role"] = "user", ["content"] = new JObject() { ["type"] = "text", ["text"] = text } }),
            };
        }
        #endregion

        /// <summary>A message as sent: strings that look like dates stay strings (ids and arguments pass through untouched).</summary>
        private static JObject ParseObject(string line)
        {
            using (JsonTextReader reader = new JsonTextReader(new StringReader(line)) { DateParseHandling = DateParseHandling.None })
                return JObject.Load(reader);
        }

        private static string Quote(string argument) => argument.Contains(" ") && !argument.StartsWith("\"") ? "\"" + argument + "\"" : argument;

        private static JObject Result(JToken id, JObject result) => new JObject() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
        private static JObject Error(JToken id, int code, string message) => new JObject() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JObject() { ["code"] = code, ["message"] = message } };

        private static void WriteOut(JObject message) => WriteOutRaw(message.ToString(Formatting.None));
        private static void WriteOutRaw(string line)
        {
            lock (_stdoutLock)
                _stdout.WriteLine(line);
        }

        private static void Log(string text)
        {
            try { Console.Error.WriteLine("[OpenCAGE MCP] " + text); } catch { }
        }
    }
}
