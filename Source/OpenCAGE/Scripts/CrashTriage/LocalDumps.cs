using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OpenCAGE.CrashTriage
{
    /// <summary>
    /// Whether Windows writes a crash dump when AI.exe crashes (Windows Error Reporting's "LocalDumps"), and turning that on or
    /// off. Crash triage needs a dump. Turning it on is the user's choice: one registry key,
    /// HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\AI.exe, written by reg.exe as administrator.
    /// </summary>
    internal static class LocalDumps
    {
        public const string KeyPath = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps";
        public const string AppKeyPath = KeyPath + @"\AI.exe";
        private const string WerKeyPath = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting";
        //Windows' defaults when a LocalDumps key has no values
        public const int DefaultCount = 10;
        public static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");

        public sealed class State
        {
            /// <summary>Windows writes a dump when AI.exe crashes.</summary>
            public bool On;
            /// <summary>Through AI.exe's own key (which <see cref="TurnOff"/> removes), not only the key for every program.</summary>
            public bool AppKey;
            public string Folder;
            /// <summary>0 custom, 1 small (mini), 2 full.</summary>
            public int DumpType = 1;
            public int DumpCount = DefaultCount;
            /// <summary>Why they are off when it is not just the missing key, else null.</summary>
            public string Note;

            public string Describe()
            {
                if (!On)
                    return "off" + (Note != null ? " (" + Note + ")" : "");
                return "on, " + (DumpType == 2 ? "full" : DumpType == 0 ? "custom" : "small") + " dumps in " + Folder + " (the newest " + DumpCount + ")" +
                    (AppKey ? "" : ", for every program on this PC");
            }
        }

        /// <summary>What Windows does now when AI.exe crashes (read from the registry; nothing is written).</summary>
        public static State Read()
        {
            State state = new State() { Folder = DefaultFolder };
            try
            {
                using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default))
                {
                    using (RegistryKey wer = machine.OpenSubKey(WerKeyPath))
                    {
                        if (wer?.GetValue("Disabled") is int disabled && disabled != 0)
                        {
                            state.Note = "Windows Error Reporting is switched off on this PC";
                            return state;
                        }
                    }
                    using (RegistryKey all = machine.OpenSubKey(KeyPath))
                    using (RegistryKey app = machine.OpenSubKey(AppKeyPath))
                    {
                        if (all == null && app == null)
                            return state;
                        state.AppKey = app != null;
                        //AI.exe's own key decides; values it lacks are Windows' defaults
                        RegistryKey key = app ?? all;
                        if (key.GetValue("DumpFolder") is string folder && folder.Trim() != "")
                            state.Folder = Environment.ExpandEnvironmentVariables(folder.Trim());
                        if (key.GetValue("DumpType") is int type) state.DumpType = type;
                        if (key.GetValue("DumpCount") is int count) state.DumpCount = count;
                        state.On = state.DumpCount != 0;
                        if (!state.On)
                            state.Note = "the registry keeps 0 dumps";
                    }
                }
            }
            catch (Exception e)
            {
                state.Note = "the registry could not be read: " + e.Message;
            }
            return state;
        }

        /// <summary>Where Windows puts AI.exe's dumps: AI.exe's DumpFolder, the one for every program, and the default folder.</summary>
        public static List<string> Folders()
        {
            List<string> folders = new List<string>();
            try
            {
                using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default))
                {
                    foreach (string path in new[] { AppKeyPath, KeyPath })
                    {
                        using (RegistryKey key = machine.OpenSubKey(path))
                        {
                            if (key?.GetValue("DumpFolder") is string folder && folder.Trim() != "")
                                folders.Add(Environment.ExpandEnvironmentVariables(folder.Trim()));
                        }
                    }
                }
            }
            catch (Exception) { }
            folders.Add(DefaultFolder);
            List<string> full = new List<string>();
            foreach (string folder in folders)
            {
                try { full.Add(Path.GetFullPath(folder)); }
                catch (Exception) { }
            }
            return full.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>What turning dumps on changes, for the user to agree to first.</summary>
        public static string WhatTurningOnDoes()
        {
            return "When Alien: Isolation crashes, Windows will save a crash dump of it: a .dmp file of about 30 MB holding the game's threads, " +
                "the modules it had loaded and parts of its memory (which can include file paths on this PC). It goes in " + DefaultFolder +
                ", and Windows keeps the newest " + DefaultCount + ". Nothing is sent anywhere. OpenCAGE reads the dump to tell you why the game crashed.\n\n" +
                "This adds one registry key for every copy of the game on this PC:\n" +
                "HKEY_LOCAL_MACHINE\\" + AppKeyPath + "\n(DumpType 1 = small dumps, DumpCount " + DefaultCount + "). " +
                "Windows will ask for administrator permission to write it. Turning crash dumps off again removes the key.";
        }

        /// <summary>
        /// Turn dumps on for AI.exe: reg.exe, run as administrator (Windows asks the user), writes AI.exe's LocalDumps key.
        /// Returns null when done, else what went wrong ("cancelled" when the user said no).
        /// </summary>
        public static string TurnOn()
        {
            string key = "\"HKLM\\" + AppKeyPath + "\"";
            return RunElevated("/c reg add " + key + " /v DumpType /t REG_DWORD /d 1 /f /reg:64 && reg add " + key + " /v DumpCount /t REG_DWORD /d " + DefaultCount + " /f /reg:64", () => Read().AppKey);
        }

        /// <summary>Turn dumps off for AI.exe again: remove AI.exe's own LocalDumps key. Null when done, else what went wrong.</summary>
        public static string TurnOff()
        {
            return RunElevated("/c reg delete \"HKLM\\" + AppKeyPath + "\" /f /reg:64", () => !Read().AppKey);
        }

        private static string RunElevated(string arguments, Func<bool> done)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo()
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                    Arguments = arguments,
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                using (Process process = Process.Start(info))
                {
                    if (process == null)
                        return "Windows did not start reg.exe";
                    if (!process.WaitForExit(60000))
                        return "reg.exe did not finish within a minute";
                    if (process.ExitCode != 0)
                        return "reg.exe failed (exit code " + process.ExitCode + ")";
                }
                return done() ? null : "the registry still reads as before";
            }
            catch (Win32Exception e) when (e.NativeErrorCode == 1223)
            {
                return "cancelled";
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
