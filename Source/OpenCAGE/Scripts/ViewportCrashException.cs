using System;

namespace OpenCAGE
{
    /// <summary>
    /// The embedded level viewer process died. Never thrown - it is constructed to describe the death in
    /// the crash log, because the crash dashboard classifies a report by the first
    /// "Type.SomethingException: message" line it finds. Without one, a viewer death lands as "Unknown"
    /// and every crash groups on its own last log line, so the same fault appears as several separate
    /// entries. Naming the exit code in the message keeps genuinely different failure modes apart (an
    /// access violation and a heap corruption are not the same bug) while grouping repeats of each.
    /// </summary>
    public class ViewportCrashException : Exception
    {
        public int ExitCode { get; }

        public ViewportCrashException(int exitCode)
            : base("The level viewer exited with " + Format(exitCode) + " (" + Describe(exitCode) + ")")
        {
            ExitCode = exitCode;
        }

        public static string Format(int exitCode)
        {
            return "0x" + exitCode.ToString("X8");
        }

        /// <summary>
        /// Plain-language name for the exit codes a Godot viewer actually dies with. Windows reports these
        /// as the process exit code when nothing catches the fault.
        /// </summary>
        public static string Describe(int exitCode)
        {
            switch (unchecked((uint)exitCode))
            {
                case 0xC0000005: return "access violation";
                case 0xC0000006: return "in-page error";
                case 0xC000001D: return "illegal instruction";
                case 0xC0000017: return "out of memory";
                case 0xC00000FD: return "stack overflow";
                case 0xC0000374: return "heap corruption";
                case 0xC0000409: return "stack buffer overrun";
                case 0xC000041D: return "unhandled exception in a callback";
                case 0xE0434352: return "unhandled .NET exception";
                case 0xE06D7363: return "unhandled C++ exception";
                case 0x40010004: return "killed by the debugger or a shutdown";
                case 1: return "generic failure";
                case 3: return "aborted";
                default: return "unrecognised exit code";
            }
        }
    }
}
