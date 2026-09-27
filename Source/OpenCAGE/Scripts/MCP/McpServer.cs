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
    /// machine). One tool runs at a time; pings and cancellations are answered while one runs. Only the
    /// first OpenCAGE to start serves: a second one keeps trying until the first goes away, so a client
    /// never talks to two editors at once.
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
                File.WriteAllText(temp, new JObject() { ["editor"] = EditorStamp(System.Windows.Forms.Application.ExecutablePath), ["tools"] = McpTools.Describe() }.ToString(Formatting.None));
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
                                ["capabilities"] = new JObject() { ["tools"] = new JObject() { ["listChanged"] = false } },
                                ["serverInfo"] = new JObject() { ["name"] = "opencage", ["title"] = "OpenCAGE", ["version"] = Singleton.Version ?? "" },
                                ["instructions"] = McpTools.Instructions,
                            });
                        }
                    case "ping":
                        return ResultMessage(id, new JObject());
                    case "tools/list":
                        return ResultMessage(id, new JObject() { ["tools"] = McpTools.Describe() });
                    case "tools/call":
                        return ResultMessage(id, CallTool(parameters, cancel));
                    case "resources/list":
                        return ResultMessage(id, new JObject() { ["resources"] = new JArray() });
                    case "resources/templates/list":
                        return ResultMessage(id, new JObject() { ["resourceTemplates"] = new JArray() });
                    case "prompts/list":
                        return ResultMessage(id, new JObject() { ["prompts"] = new JArray() });
                    case "logging/setLevel":
                        return ResultMessage(id, new JObject());
                    default:
                        throw new McpProtocolException(-32601, "Method not found: " + method);
                }
            }

            private JObject CallTool(JObject parameters, CancellationToken cancel)
            {
                string name = (string)parameters["name"];
                McpTool tool = McpTools.Find(name);
                if (tool == null)
                    throw new McpProtocolException(-32602, "Unknown tool: " + name);
                JObject arguments = parameters["arguments"] as JObject ?? new JObject();
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

                //Queue behind any tool already running, but let a cancellation take this one out of the queue
                try
                {
                    _toolGate.Wait(cancel);
                }
                catch (OperationCanceledException)
                {
                    return McpTool.ErrorResult("Cancelled before it started.");
                }
                try
                {
                    RunningTool = tool.Name;
                    RaiseStatus();
                    return tool.Invoke(call);
                }
                finally
                {
                    lock (progressLock) finished = true;
                    RunningTool = null;
                    _toolGate.Release();
                    RaiseStatus();
                }
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
}
