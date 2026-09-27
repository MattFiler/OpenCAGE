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
    /// program) when no OpenCAGE is running, and relays the editor's answers back.
    /// </para>
    /// <para>
    /// It answers the protocol's handshake itself, and the tool list from a copy of the editor's last
    /// answer, so a client is not kept waiting while the editor starts. Messages for the editor queue on a
    /// sender thread; pings are answered at once whatever it is doing.
    /// </para>
    /// <para>
    /// Arguments: <c>--editor &lt;path&gt;</c> to use an OpenCAGE.exe somewhere else (or set OPENCAGE_EXE);
    /// <c>--no-launch</c> to only ever attach to an OpenCAGE that is already open; anything after <c>--</c>
    /// is passed to OpenCAGE when this starts it (e.g. <c>-- -disable_viewport</c>).
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

        private const string LatestProtocol = "2025-06-18";
        private static readonly string[] SupportedProtocols = { "2025-06-18", "2025-03-26", "2024-11-05" };

        private static readonly object _stdoutLock = new object();
        private static StreamWriter _stdout;

        //Connecting can take minutes (the editor starting): it has its own lock, so nothing else waits on it
        private static readonly object _connectLock = new object();
        private static readonly object _pipeLock = new object();
        private static volatile NamedPipeClientStream _pipe;
        private static StreamWriter _pipeWriter;
        private static readonly ConcurrentDictionary<string, JToken> _pending = new ConcurrentDictionary<string, JToken>();
        private static readonly BlockingCollection<JObject> _outbox = new BlockingCollection<JObject>();

        private static string _editorPath;
        private static string _editorArguments = "";
        private static bool _mayLaunch = true;
        private static Process _launched;

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

            Log("OpenCAGE MCP bridge " + Assembly.GetExecutingAssembly().GetName().Version + " (pipe " + PipeName + ", editor " + _editorPath + ")");
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
                    if (message["id"] != null)
                        WriteOut(Error(message["id"], -32603, e.Message));
                }
            }

            Log("The client closed stdin; exiting");
            _outbox.CompleteAdding();
            lock (_pipeLock) { try { _pipe?.Dispose(); } catch { } }
            return 0;
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
                        WriteOut(Result(id, new JObject()
                        {
                            ["protocolVersion"] = Array.IndexOf(SupportedProtocols, requested) >= 0 ? requested : LatestProtocol,
                            ["capabilities"] = new JObject() { ["tools"] = new JObject() { ["listChanged"] = false } },
                            ["serverInfo"] = new JObject() { ["name"] = "opencage", ["title"] = "OpenCAGE", ["version"] = Assembly.GetExecutingAssembly().GetName().Version.ToString() },
                            ["instructions"] = Instructions,
                        }));
                        //Get the editor going while the client finishes its handshake
                        new Thread(() => { try { EnsureEditor(); } catch (Exception e) { Log(e.Message); } }) { IsBackground = true }.Start();
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
                        //While the editor starts, the list it gave last time stands in for it
                        NamedPipeClientStream pipe = _pipe;
                        if ((pipe == null || !pipe.IsConnected) && TryCachedTools(out JArray tools))
                        {
                            WriteOut(Result(id, new JObject() { ["tools"] = tools }));
                            new Thread(() => { try { EnsureEditor(); } catch (Exception e) { Log(e.Message); } }) { IsBackground = true }.Start();
                            return;
                        }
                        break;
                    }
            }

            //Everything else is the editor's to answer
            if (id != null)
                _pending[id.ToString(Formatting.None)] = id;
            _outbox.Add(message);
        }

        private static void SendLoop()
        {
            foreach (JObject message in _outbox.GetConsumingEnumerable())
            {
                JToken id = message["id"];
                //A notification is not worth starting (or waiting minutes for) OpenCAGE: with no editor there is nothing to tell
                NamedPipeClientStream connected = _pipe;
                if (id == null && (connected == null || !connected.IsConnected))
                    continue;
                try
                {
                    EnsureEditor();
                    lock (_pipeLock)
                        _pipeWriter.WriteLine(message.ToString(Formatting.None));
                }
                catch (Exception e)
                {
                    Log("Could not reach OpenCAGE: " + e.Message);
                    if (id != null && _pending.TryRemove(id.ToString(Formatting.None), out _))
                        WriteOut(Error(id, -32603, "OpenCAGE is not available: " + e.Message));
                }
            }
        }

        /// <summary>Connect to OpenCAGE's pipe, starting OpenCAGE first if none is running.</summary>
        private static void EnsureEditor()
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
                    //Two bridges starting together (a client that starts its servers side by side) must start one OpenCAGE
                    using (Mutex launch = new Mutex(false, LaunchMutex))
                    {
                        bool held;
                        try { held = launch.WaitOne(TimeSpan.FromMinutes(5)); }
                        catch (AbandonedMutexException) { held = true; }
                        try
                        {
                            //Whoever held it may have started one while this waited
                            pipe = TryConnect(300) ?? LaunchOrWait();
                        }
                        finally
                        {
                            if (held)
                                try { launch.ReleaseMutex(); } catch { }
                        }
                    }
                }

                UTF8Encoding utf8 = new UTF8Encoding(false);
                lock (_pipeLock)
                {
                    _pipeWriter = new StreamWriter(pipe, utf8) { AutoFlush = true, NewLine = "\n" };
                    _pipe = pipe;
                }
                StreamReader reader = new StreamReader(pipe, utf8);
                new Thread(() => PumpFromEditor(reader, pipe)) { IsBackground = true, Name = "From OpenCAGE" }.Start();
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

        /// <summary>The editor's answers (and notifications) go straight out to the client.</summary>
        private static void PumpFromEditor(StreamReader reader, NamedPipeClientStream pipe)
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
                            _pending.TryRemove(id.ToString(Formatting.None), out _);
                    }
                    catch { }
                    WriteOutRaw(line);
                }
            }
            catch (Exception e)
            {
                Log("Lost OpenCAGE: " + e.Message);
            }
            Log("OpenCAGE closed the connection");
            lock (_pipeLock)
            {
                if (_pipe == pipe)
                    _pipe = null;
            }
            //Whatever was still waiting on it will not be answered now
            foreach (KeyValuePair<string, JToken> waiting in _pending.ToList())
                if (_pending.TryRemove(waiting.Key, out JToken id))
                    WriteOut(Error(id, -32603, "OpenCAGE closed before answering. It is started again on the next request."));
        }

        private static bool TryCachedTools(out JArray tools)
        {
            tools = null;
            try
            {
                if (!File.Exists(ToolCachePath)) return false;
                JObject cache = JObject.Parse(File.ReadAllText(ToolCachePath));
                //Only a list from this very OpenCAGE.exe: another build may offer other tools
                if ((string)cache["editor"] != EditorStamp(_editorPath)) return false;
                tools = cache["tools"] as JArray;
                return tools != null;
            }
            catch { return false; }
        }

        /// <summary>Which executable, which build of it. Must match OpenCAGE's McpServer.EditorStamp.</summary>
        private static string EditorStamp(string path)
        {
            try
            {
                FileInfo exe = new FileInfo(path);
                return exe.FullName.ToUpperInvariant() + "|" + exe.Length + "|" + exe.LastWriteTimeUtc.Ticks;
            }
            catch { return ""; }
        }

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

        //Mirrors OpenCAGE's McpTools.Instructions: the bridge answers the handshake before the editor is there
        private const string Instructions =
            "OpenCAGE is the level editor for Alien: Isolation. These tools drive the OpenCAGE window the user has open (it is started if it is not), " +
            "so every change appears there as you make it. Changes to the open level's script are steps on its undo history (the undo tool, or Ctrl+Z) and reach the game only when you save; " +
            "each tool's description says when a change is instead written straight away (assets, shared game files).\n\n" +
            "How a level is built: a level's script is a tree of composites. The level's root composite places instances of other composites; composites hold " +
            "entities: functions (built-in types such as ModelReference, Character, TriggerBox, LogicGate - see list_function_types and describe_function_type), " +
            "instances of other composites, variables (a composite's own pins, which appear as parameters and link points on its instances), aliases (overrides " +
            "that reach into a nested instance) and proxies. Entities are wired with links (source entity.parameter -> target entity.parameter: events from a relay " +
            "or target pin to a method pin, data from a parameter to a reference or variable pin) and configured with parameters. Positions are relative to the composite.\n\n" +
            "Typical work: get_editor_state first. Use load_level or create_level to get a level open. Find content with find_composites / find_entities, or " +
            "search_level for another level; port_composites brings composites (with their models, materials, textures and collision) in from another level. " +
            "create_entities places instances and functions and can wire them in the same call; set_parameters and add_links configure and wire existing ones " +
            "(list_enum_string_values gives valid values for sound, animation and other named parameters). Scripts stay drawn in the flowgraph editor automatically; " +
            "get_flowgraph, edit_flowgraph_pages and edit_flowgraph_nodes arrange pages. save_level writes the level; save_level with build=true (Save & Build) also " +
            "rebuilds lighting, navigation and the rest of the runtime data the game needs to run the level - do that before playing it (launch_game).\n\n" +
            "Beyond the script: get_entity_resources and set_renderable / set_collision / set_physics_system change an entity's model, materials, collision and physics; " +
            "the model, material, texture and collision/physics import tools add assets; get_cage_animation, animate_parameters and set_animation_events edit CAGEAnimations; " +
            "capture_viewport and set_viewport_view let you look at the result. Tools that change files every level shares (configuration records, text strings, " +
            "ANIMATION.PAK, sound banks, UI.PAK) take effect at once and cannot be undone: run them with dry_run first. " +
            "Refer to composites by their path (e.g. 'Archetypes\\Script\\Mission\\SpawnPositionSelect'; 'root' is the level's root composite) and to entities by the id the tools return, or by name.";
    }
}
