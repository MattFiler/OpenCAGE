using Microsoft.Win32.SafeHandles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace OpenCAGE
{
    /// <summary>
    /// Starts a process whose window can never reach the desktop, however long it takes us to take
    /// ownership of it.
    ///
    /// Windows lets whoever starts a process dictate that process's <em>first</em> ShowWindow call:
    /// when STARTUPINFO carries STARTF_USESHOWWINDOW, the value the child passes the first time round
    /// is ignored and the launcher's is used instead. Starting it with SW_HIDE means the child's own
    /// "show my window now" does nothing, and the window sits there invisible until somebody else
    /// shows it - which is exactly what <see cref="EmbeddedWindowHost"/> does once it has reparented
    /// it. No race, no flash.
    ///
    /// This has to be CreateProcess directly, because .NET's Process.Start only honours WindowStyle
    /// when it goes through ShellExecute, and that can't redirect the child's output.
    /// </summary>
    internal static class HiddenProcessLauncher
    {
        /// <summary>
        /// Start a process with its window suppressed, relaying each line of its output.
        /// </summary>
        /// <param name="onLine">Called per line from the child, with true for standard error.</param>
        /// <returns>The started process, or null if it could not be started.</returns>
        public static Process Start(string executable, string arguments, string workingDirectory,
                                    IDictionary<string, string> extraEnvironment, Action<string, bool> onLine)
        {
            SafeFileHandle outRead = null, outWrite = null, errorRead = null, errorWrite = null;

            try
            {
                CreatePipePair(out outRead, out outWrite);
                CreatePipePair(out errorRead, out errorWrite);

                NativeMethods.STARTUPINFO startup = new NativeMethods.STARTUPINFO
                {
                    cb = Marshal.SizeOf(typeof(NativeMethods.STARTUPINFO)),
                    dwFlags = NativeMethods.STARTF_USESHOWWINDOW | NativeMethods.STARTF_USESTDHANDLES,
                    wShowWindow = NativeMethods.SW_HIDE,
                    hStdInput = IntPtr.Zero,
                    hStdOutput = outWrite.DangerousGetHandle(),
                    hStdError = errorWrite.DangerousGetHandle(),
                };

                //CreateProcess is allowed to write into the command line it is given, so hand it a buffer
                StringBuilder commandLine = new StringBuilder("\"" + executable + "\" " + arguments);

                bool started;
                IntPtr environment = BuildEnvironment(extraEnvironment, out GCHandle pinned);
                try
                {
                    started = NativeMethods.CreateProcess(
                        null,
                        commandLine,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        true,
                        NativeMethods.CREATE_NO_WINDOW | NativeMethods.CREATE_UNICODE_ENVIRONMENT,
                        environment,
                        workingDirectory,
                        ref startup,
                        out NativeMethods.PROCESS_INFORMATION information);

                    if (started)
                    {
                        NativeMethods.CloseHandle(information.hThread);
                        NativeMethods.CloseHandle(information.hProcess);

                        /* The parent has to let go of the writing ends, or the readers below never
                         * see the end of the stream when the child exits. */
                        outWrite.Dispose(); outWrite = null;
                        errorWrite.Dispose(); errorWrite = null;

                        Relay(outRead, onLine, false); outRead = null;
                        Relay(errorRead, onLine, true); errorRead = null;

                        try { return Process.GetProcessById((int)information.dwProcessId); }
                        catch { return null; }   //exited before we could open it
                    }
                }
                finally
                {
                    if (pinned.IsAllocated) pinned.Free();
                }

                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                outRead?.Dispose();
                outWrite?.Dispose();
                errorRead?.Dispose();
                errorWrite?.Dispose();
            }
        }

        /* A pipe the child can write to and we can read from. Only the writing end is inheritable -
         * if the child inherited the reading end too it would hold the pipe open after exiting. */
        private static void CreatePipePair(out SafeFileHandle read, out SafeFileHandle write)
        {
            NativeMethods.SECURITY_ATTRIBUTES security = new NativeMethods.SECURITY_ATTRIBUTES
            {
                nLength = Marshal.SizeOf(typeof(NativeMethods.SECURITY_ATTRIBUTES)),
                lpSecurityDescriptor = IntPtr.Zero,
                bInheritHandle = true,
            };

            if (!NativeMethods.CreatePipe(out read, out write, ref security, 0))
                throw new IOException("Could not create a pipe for the process output.");

            if (!NativeMethods.SetHandleInformation(read, NativeMethods.HANDLE_FLAG_INHERIT, 0))
                throw new IOException("Could not detach the pipe from the child process.");
        }

        /* Read the pipe on a background thread and hand each line over. */
        private static void Relay(SafeFileHandle handle, Action<string, bool> onLine, bool isError)
        {
            Thread thread = new Thread(() =>
            {
                try
                {
                    using (StreamReader reader = new StreamReader(new FileStream(handle, FileAccess.Read, 1024), Encoding.UTF8))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                            onLine?.Invoke(line, isError);
                    }
                }
                catch
                {
                    //the pipe closing as the process exits is the ordinary way for this to end
                }
            });

            thread.IsBackground = true;
            thread.Name = isError ? "viewer stderr" : "viewer stdout";
            thread.Start();
        }

        /* Our environment plus the extras, as the double-null-terminated block CreateProcess wants. */
        private static IntPtr BuildEnvironment(IDictionary<string, string> extra, out GCHandle pinned)
        {
            pinned = default(GCHandle);
            if (extra == null || extra.Count == 0)
                return IntPtr.Zero;   //null means "inherit ours unchanged"

            Dictionary<string, string> variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                variables[(string)entry.Key] = entry.Value as string ?? "";
            foreach (KeyValuePair<string, string> entry in extra)
                variables[entry.Key] = entry.Value ?? "";

            StringBuilder block = new StringBuilder();
            foreach (KeyValuePair<string, string> entry in variables)
            {
                //a name starting with '=' is a drive's current directory, which must stay first
                block.Append(entry.Key).Append('=').Append(entry.Value).Append('\0');
            }
            block.Append('\0');

            byte[] bytes = Encoding.Unicode.GetBytes(block.ToString());
            pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            return pinned.AddrOfPinnedObject();
        }

        private static class NativeMethods
        {
            public const uint STARTF_USESHOWWINDOW = 0x00000001;
            public const uint STARTF_USESTDHANDLES = 0x00000100;
            public const short SW_HIDE = 0;
            public const uint CREATE_NO_WINDOW = 0x08000000;
            public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
            public const int HANDLE_FLAG_INHERIT = 1;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct STARTUPINFO
            {
                public int cb;
                public string lpReserved;
                public string lpDesktop;
                public string lpTitle;
                public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute;
                public uint dwFlags;
                public short wShowWindow;
                public short cbReserved2;
                public IntPtr lpReserved2;
                public IntPtr hStdInput, hStdOutput, hStdError;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct PROCESS_INFORMATION
            {
                public IntPtr hProcess, hThread;
                public uint dwProcessId, dwThreadId;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SECURITY_ATTRIBUTES
            {
                public int nLength;
                public IntPtr lpSecurityDescriptor;
                [MarshalAs(UnmanagedType.Bool)] public bool bInheritHandle;
            }

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CreateProcess(string applicationName, StringBuilder commandLine,
                IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint creationFlags,
                IntPtr environment, string currentDirectory, ref STARTUPINFO startupInfo,
                out PROCESS_INFORMATION information);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CreatePipe(out SafeFileHandle readPipe, out SafeFileHandle writePipe,
                ref SECURITY_ATTRIBUTES attributes, int size);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetHandleInformation(SafeFileHandle handle, int mask, int flags);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr handle);
        }
    }
}
