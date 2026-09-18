using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE
{
    /// <summary>
    /// Hands a package file (a double-clicked .ocp or .omp) to the OpenCAGE that is already running,
    /// so a second copy never starts just to open it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The primary instance (the one holding <see cref="PrimaryInstanceLock"/>) serves a named pipe for
    /// the life of the process; a process launched with -openfile= writes the path down it and exits.
    /// The pipe answers with the server's process id first, so the sender - which was launched by the
    /// user's click and so holds the right to set the foreground window - can pass that right on
    /// (AllowSetForegroundWindow) before the primary brings itself forward. Without it the primary's
    /// Activate would only flash the taskbar and the import window would open behind Explorer.
    /// </para>
    /// <para>
    /// A primary that holds the mutex but is not serving yet (still starting up, or between a level
    /// load and its window) must never be worked around by starting a second instance: on Steam that
    /// ends in "app already running". So when the pipe cannot be reached the file is written to a
    /// stash file beside the executable and the sender exits regardless; the primary watches that
    /// file and opens whatever lands in it. The stash is also how a file survives Steam relaunching
    /// the process (see Program.Main), which drops the arguments. A primary that goes away while the
    /// sender is waiting is a different matter: then nobody is left to read a stash, so the sender
    /// starts up and opens the file itself.
    /// </para>
    /// </remarks>
    public static class PackageHandover
    {
        //Per logon session, to match the primary mutex's scope
        private static readonly string PipeName = "OpenCAGE.OpenFile." + Process.GetCurrentProcess().SessionId;
        private const string Acknowledgement = "OK";
        private const string StashMutexName = @"Local\OpenCAGE_PackageStash";
        private static readonly TimeSpan StashLifetime = TimeSpan.FromHours(1);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);

        private static Thread _server;
        private static NamedPipeServerStream _listening;
        private static readonly object _serverLock = new object();
        private static volatile bool _stop;

        /// <summary>Where files wait for the primary: one absolute path per line, beside the executable.</summary>
        public static string StashPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenCAGE Pending Open.txt");

        private enum SendOutcome { Sent, NotAnswering, NoPrimary }

        #region SENDER
        /// <summary>
        /// A launch with a file, from a process that must not start up if OpenCAGE is already running.
        /// True when the file has been handed over or stashed for a running primary - the caller exits.
        /// False when no primary is running (or it went away while we waited), so this process should
        /// start up and open the file itself.
        /// </summary>
        public static bool TryHandOver(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (!PrimaryInstanceLock.IsHeldByAnotherProcess())
                return false;

            switch (TrySend(path, 10000))
            {
                case SendOutcome.Sent:
                    return true;
                case SendOutcome.NoPrimary:
                    return false;
                default:
                    //Held but not answering: it is starting, or busy. Leave the file where it will look.
                    Stash(path);
                    return true;
            }
        }

        private static SendOutcome TrySend(string path, int totalTimeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(totalTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (!PrimaryInstanceLock.IsHeldByAnotherProcess())
                    return SendOutcome.NoPrimary;

                try
                {
                    using (NamedPipeClientStream client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                    {
                        //A short connect: Connect spins while the pipe does not exist, so the waiting is done here
                        client.Connect(50);
                        using (StreamReader reader = new StreamReader(client, new UTF8Encoding(false), false, 4096, true))
                        using (StreamWriter writer = new StreamWriter(client, new UTF8Encoding(false), 4096, true))
                        {
                            string serverPid = reader.ReadLine();
                            if (int.TryParse(serverPid, out int pid))
                            {
                                try { AllowSetForegroundWindow(pid); } catch { }
                            }
                            writer.WriteLine(path);
                            writer.Flush();
                            if (reader.ReadLine() == Acknowledgement)
                                return SendOutcome.Sent;
                        }
                    }
                }
                catch (TimeoutException) { }
                catch (IOException) { }
                catch (UnauthorizedAccessException)
                {
                    //Another user's (or an elevated) OpenCAGE owns the name: the stash is the way in
                    return SendOutcome.NotAnswering;
                }
                Thread.Sleep(250);
            }
            return SendOutcome.NotAnswering;
        }
        #endregion

        #region STASH
        /// <summary>Leave a file for the primary (or for the process Steam relaunches) to open.</summary>
        public static void Stash(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            WithStashLock(() =>
            {
                List<string> lines = ReadStashLines();
                if (!lines.Contains(path, StringComparer.OrdinalIgnoreCase))
                    lines.Add(path);
                File.WriteAllLines(StashPath, lines, new UTF8Encoding(false));
            });
        }

        /// <summary>Every stashed file, in the order they were left; the stash is emptied.</summary>
        public static List<string> TakeStashed()
        {
            List<string> result = new List<string>();
            WithStashLock(() =>
            {
                if (!File.Exists(StashPath))
                    return;
                //A stash nobody came for in an hour is from a launch that never happened
                bool fresh = DateTime.UtcNow - File.GetLastWriteTimeUtc(StashPath) < StashLifetime;
                if (fresh)
                    result.AddRange(ReadStashLines());
                try { File.Delete(StashPath); } catch { }
            });
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static bool HasStash()
        {
            try { return File.Exists(StashPath) && new FileInfo(StashPath).Length > 0; }
            catch { return false; }
        }

        private static List<string> ReadStashLines()
        {
            try
            {
                if (!File.Exists(StashPath))
                    return new List<string>();
                return File.ReadAllLines(StashPath).Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /* Several senders can land at once (Explorer opens each selected file separately) */
        private static void WithStashLock(Action action)
        {
            try
            {
                using (Mutex mutex = new Mutex(false, StashMutexName))
                {
                    bool held = false;
                    try
                    {
                        try { held = mutex.WaitOne(3000); }
                        catch (AbandonedMutexException) { held = true; }
                        action();
                    }
                    finally
                    {
                        if (held)
                        {
                            try { mutex.ReleaseMutex(); } catch { }
                        }
                    }
                }
            }
            catch { }
        }
        #endregion

        #region SERVER
        /// <summary>
        /// Serve the pipe on a background thread. <paramref name="onFile"/> is called on that thread
        /// with each path received; the caller marshals to the UI. Only the process holding the
        /// primary mutex should serve - a second server cannot bind the name, and keeps trying until
        /// it can (the previous holder may take a while to go away).
        /// </summary>
        public static void StartServer(Action<string> onFile)
        {
            lock (_serverLock)
            {
                if (_server != null || onFile == null)
                    return;

                _stop = false;
                _server = new Thread(() => ServeLoop(onFile))
                {
                    IsBackground = true,
                    Name = "OpenCAGE package handover",
                };
                _server.Start();
            }
        }

        /// <summary>Stop taking files: a closing primary must not accept what it can no longer open.</summary>
        public static void StopServer()
        {
            _stop = true;
            lock (_serverLock)
            {
                //Drops WaitForConnection out with an exception the loop reads as "stop"
                try { _listening?.Dispose(); } catch { }
                _listening = null;
            }
        }

        private static void ServeLoop(Action<string> onFile)
        {
            while (!_stop)
            {
                NamedPipeServerStream server;
                try
                {
                    server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                }
                catch (Exception)
                {
                    //The name is still held (a previous primary on its way out): wait and try again
                    Thread.Sleep(2000);
                    continue;
                }

                try
                {
                    lock (_serverLock) { _listening = server; }
                    using (server)
                    {
                        server.WaitForConnection();
                        if (_stop)
                            return;

                        using (StreamWriter writer = new StreamWriter(server, new UTF8Encoding(false), 4096, true))
                        using (StreamReader reader = new StreamReader(server, new UTF8Encoding(false), false, 4096, true))
                        {
                            writer.WriteLine(Process.GetCurrentProcess().Id);
                            writer.Flush();

                            //A client that connects and says nothing must not hold the only instance forever
                            Task<string> read = Task.Run(() => reader.ReadLine());
                            if (!read.Wait(5000))
                                continue; //disposing the stream ends the read
                            string path = read.Result;

                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                try { onFile(path.Trim()); }
                                catch { }
                            }
                            writer.WriteLine(Acknowledgement);
                            writer.Flush();
                        }
                        try { server.WaitForPipeDrain(); } catch { }
                    }
                }
                catch (Exception)
                {
                    if (_stop)
                        return;
                    //A broken client or a pipe hiccup: back off briefly and serve again
                    Thread.Sleep(250);
                }
                finally
                {
                    lock (_serverLock) { if (ReferenceEquals(_listening, server)) _listening = null; }
                }
            }
        }
        #endregion
    }
}
