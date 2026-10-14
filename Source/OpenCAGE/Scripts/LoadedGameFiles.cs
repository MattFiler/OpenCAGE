#if ENABLE_MOD_PACKAGES
using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.ConfigEditors;
using OpenCAGE.DockPanels;
using OpenCAGE.Modding;
using OpenCAGE.Popups;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// What OpenCAGE has loaded from the game's files, kept in step with them while the Mod Manager changes them. Before: work
    /// not saved yet in what may change is saved (or let go, as the user says), and windows keeping their own copy of those
    /// files are closed. After: whatever changed is loaded again - the open level, the animations, the global textures and
    /// text - so nothing shows what was there before, or saves it back over what the mods put there.
    /// </summary>
    /// <remarks>UI thread.</remarks>
    public static class LoadedGameFiles
    {
        /// <summary>A change to the game's files under way (<see cref="BeforeChange"/> to <see cref="AfterChange"/>).</summary>
        public class Change
        {
            internal Dictionary<string, (long Size, long Time)> Before;
            internal ICollection<string> MayChange;
            internal string Level;
            internal CompositePath.Place Place;
            internal bool UndoWasBlocked;
            internal List<Form> Disabled = new List<Form>();
            internal bool Ended;

            /// <summary>What was loaded again afterwards, in words ("the level BSP_TORRENS", "the animations").</summary>
            public List<string> Reloaded = new List<string>();
            /// <summary>What couldn't be loaded again, and what to do about it.</summary>
            public List<string> Problems = new List<string>();
        }

        private const string GlobalTextures = "DATA/ENV/GLOBAL/WORLD/GLOBAL_TEXTURES";

        /// <summary>
        /// Get ready for the game's files to change. <paramref name="mayChange"/>: the files that may (normalised; null for any).
        /// With an <paramref name="owner"/> the user is asked about work not saved yet (save it, let it go, or cancel), and
        /// shown whatever stops it; without one (the AI assistant tools), it's refused unless <paramref name="discardUnsaved"/>,
        /// and every window is disabled until it's done (nothing modal does that then). <paramref name="canSave"/> false when
        /// saving first would be undone by the change itself (putting files back as they were). Null when it mustn't go ahead,
        /// with why in <paramref name="refused"/> (empty when the user cancelled). Otherwise nothing may save over the game's
        /// files until <see cref="AfterChange"/>.
        /// </summary>
        public static Change BeforeChange(IWin32Window owner, ICollection<string> mayChange, out string refused, bool discardUnsaved = false, bool canSave = true)
        {
            refused = null;
            CommandsEditor editor = Singleton.Editor;
            bool May(Func<string, bool> which) => mayChange == null || mayChange.Any(which);
            Change Refuse(string why)
            {
                if (owner != null && !string.IsNullOrEmpty(why))
                    MessageBox.Show(owner, why, "Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            //Busy writing the game's files already
            if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                refused = Singleton.BusyText + ". Wait for it to finish, then try again.";
            else if (editor != null && (editor.IsLevelLoadInProgress || editor.IsLevelLoaderRunning))
                refused = "A level is loading. Wait for it to finish, then try again.";
            else if (UndoStack.Current.Blocked)
                refused = "OpenCAGE is saving. Wait for it to finish, then try again.";
            //Programs and windows keeping their own copy of files that may change, which only save it when told to: theirs to close
            else if (May(o => o.StartsWith("DATA/BINARY_BEHAVIOR/") || o.StartsWith("DATA/BEHAVIOR/")) && ConfigReset.BehaviourTreeProcesses.Any(o => Process.GetProcessesByName(o).Length != 0))
                refused = "The Behaviour Tree Editor is open. Close it first (saving anything you changed in it): it keeps its own copy of the behaviour trees, and would save it over the mods' changes.";
            else
            {
                List<Form> saveButtonWindows = WindowsHolding(mayChange, true);
                if (saveButtonWindows.Count != 0)
                    refused = "Close " + string.Join(", ", saveButtonWindows.Select(o => "'" + o.Text + "'")) + " first (saving anything you changed there): "
                        + (saveButtonWindows.Count == 1 ? "it keeps its own copy" : "they keep their own copies") + " of files this changes, and would save " + (saveButtonWindows.Count == 1 ? "it" : "them") + " over the mods' changes.";
            }
            if (refused != null)
                return Refuse(refused);

            //Work not saved yet in what may change. The open level goes with the global textures too: it holds its own copies of
            //those it uses, and its next save would write them back. Windows that put their edits into the level as they close
            //may hold some: they count as changes, and close only once this goes ahead.
            Level level = editor?.CompositeBrowser?.Content?.Level;
            bool levelMayChange = level != null && May(o => InLevel(o, level.Name) || o.StartsWith(GlobalTextures));
            List<Form> levelEditors = levelMayChange ? Application.OpenForms.Cast<Form>().Where(o => !o.IsDisposed && (o is TriggerSequenceEditor || o is CharacterEditor)).ToList() : new List<Form>();
            bool levelUnsaved = levelMayChange && (DirtyTracker.IsDirty || levelEditors.Count != 0);
            CathodeLib.Animation animations = Singleton.Animations;
            string animationPak = animations?.PAK?.Filepath == null ? null : ModToolkit.NormaliseFull(Singleton.PathToAI, animations.PAK.Filepath);
            List<string> animationsUnsaved = animationPak != null && May(o => o == animationPak) ? AnimationPakWrite.Unsaved(animations) : new List<string>();
            List<string> unsaved = new List<string>();
            if (levelUnsaved) unsaved.Add("the level " + LevelTitle(level.Name) + (levelEditors.Count != 0 ? " (with " + string.Join(" and ", levelEditors.Select(o => "'" + o.Text + "'").Distinct()) + " open on it)" : ""));
            if (animationsUnsaved.Count != 0) unsaved.Add("the animations: " + string.Join(", ", animationsUnsaved));
            bool save = false;
            if (unsaved.Count != 0)
            {
                if (owner == null)
                {
                    if (!discardUnsaved)
                    {
                        refused = "These have changes not saved yet, which this changes the files of: " + string.Join("; ", unsaved) + ". "
                            + (canSave ? "Save them first, or pass discard_unsaved:true to let them go (they're loaded again from the files afterwards)." : "Pass discard_unsaved:true to let them go (they're loaded again from the files afterwards).");
                        return null;
                    }
                }
                else
                {
                    string text = "These may have changes you haven't saved, and the files they're in are about to change:\r\n\r\n"
                        + string.Join("\r\n", unsaved.Select(o => "  • " + o)) + "\r\n\r\n"
                        + (canSave
                            ? "Save them first? OpenCAGE loads them again from the files once they've changed, so whatever isn't saved now is lost."
                            : "Their files are being put back as they were, and OpenCAGE loads them again from the files afterwards, so these changes will be lost.");
                    DialogResult answer = canSave
                        ? ModReportDialog.Show(owner, "Save your changes?", text, "Save them", "Cancel", "Don't save")
                        : ModReportDialog.Show(owner, "Changes not saved", text, "Go ahead", "Cancel");
                    if (answer != DialogResult.OK && answer != DialogResult.No)
                    {
                        refused = "";
                        return null;
                    }
                    save = canSave && answer == DialogResult.OK;
                }
            }

            //Going ahead: the windows putting their edits into the level as they close go first, so a save takes those edits too
            foreach (Form window in levelEditors)
                if (!window.IsDisposed)
                    window.Close();
            if (save && levelMayChange && DirtyTracker.IsDirty)
            {
                try { editor.SaveLevelQuietly(); }
                catch (Exception e)
                {
                    refused = "The level couldn't be saved (" + e.Message + "), so nothing was changed.";
                    return Refuse(refused);
                }
                if (DirtyTracker.IsDirty)
                {
                    refused = "The level wasn't saved, so nothing was changed.";
                    return Refuse(refused);
                }
            }
            if (save && animationsUnsaved.Count != 0)
            {
                string error = AnimationPakWrite.SaveWhole(animations);
                if (error != null)
                {
                    refused = "The animations weren't saved (" + error + "), so nothing was changed.";
                    return Refuse(refused);
                }
            }

            //Windows that keep their own copy of files that may change and save it back by themselves (they've nothing unsaved):
            //closed, or they'd put the old copy back
            if (mayChange == null)
                Singleton.OnResetConfigs?.Invoke();
            foreach (Form window in WindowsHolding(mayChange, false))
                if (!window.IsDisposed)
                    window.Close();

            Change change = new Change()
            {
                Before = Stamps(),
                MayChange = mayChange,
                Level = level?.Name,
                Place = level == null ? null : editor.CompositeDisplay?.CapturePlace(),
                UndoWasBlocked = UndoStack.Current.Blocked,
            };
            //Nothing saves over the files meanwhile: not the level (Save, the viewport asking, the AI assistant tools), not the
            //animations, not undo - and without a window of its own up to keep the user out, no window can be used either
            Singleton.CurrentBackupState = Singleton.BackupState.APPLYING_MODS;
            UndoStack.Current.Blocked = true;
            if (owner == null)
            {
                foreach (Form window in Application.OpenForms.Cast<Form>().Where(o => !o.IsDisposed && o.Enabled).ToList())
                {
                    window.Enabled = false;
                    change.Disabled.Add(window);
                }
            }
            return change;
        }

        /// <summary>
        /// The game's files have changed (<paramref name="filesChanged"/>: or may have - false when nothing was written, or what
        /// was got put back as it was): what's loaded of them that changed is loaded again. Says what in the change's
        /// <see cref="Change.Reloaded"/> and <see cref="Change.Problems"/>. Saving is allowed again either way.
        /// </summary>
        public static void AfterChange(Change change, bool filesChanged)
        {
            if (change == null || change.Ended)
                return;
            change.Ended = true;
            CommandsEditor editor = Singleton.Editor;
            if (Singleton.CurrentBackupState == Singleton.BackupState.APPLYING_MODS)
                Singleton.CurrentBackupState = Singleton.BackupState.NONE;
            UndoStack.Current.Blocked = change.UndoWasBlocked;
            foreach (Form window in change.Disabled)
                if (!window.IsDisposed)
                    window.Enabled = true;
            if (!filesChanged)
                return;

            //What changed, by each file's size and time. When that can't be told (a folder couldn't be read), whatever may have
            //changed is taken as changed - what was asked about at the start, so nothing with unsaved work is loaded again unasked
            Dictionary<string, (long Size, long Time)> after = Stamps();
            HashSet<string> changed = change.MayChange == null ? null : new HashSet<string>(change.MayChange);
            if (change.Before != null && after != null)
            {
                changed = new HashSet<string>(after.Where(o => !change.Before.TryGetValue(o.Key, out (long, long) was) || was != o.Value).Select(o => o.Key));
                changed.UnionWith(change.Before.Keys.Where(o => !after.ContainsKey(o)));
            }
            bool Changed(Func<string, bool> which) => changed == null || changed.Any(which);

            //The animations: by what the file holds (a mod put back as it was isn't a change)
            CathodeLib.Animation animations = Singleton.Animations;
            if (animations != null && AnimationPakWrite.ChangedOnDisk(animations.PAK?.Filepath) != null)
            {
                if (Singleton.ReloadAnimations(out string error))
                    change.Reloaded.Add("the animations");
                else
                    change.Problems.Add("The animations couldn't be loaded again (" + error + "): restart OpenCAGE before working on animations, animation trees or blend sets.");
            }

            //What every level shares: the global textures (which the viewport and the open level keep copies of too) and the game's text
            bool globalTextures = Changed(o => o.StartsWith(GlobalTextures));
            if (globalTextures && Singleton.Global != null)
            {
                try
                {
                    Singleton.Global.Textures = new Textures(Path.Combine(Singleton.PathToAI, "DATA", "ENV", "GLOBAL", "WORLD", "GLOBAL_TEXTURES.ALL.PAK"));
                    change.Reloaded.Add("the global textures");
                }
                catch (Exception e)
                {
                    change.Problems.Add("The global textures couldn't be loaded again (" + e.Message + "): restart OpenCAGE before working with textures.");
                }
            }
            List<string> textDatabases = changed == null
                ? Singleton.GlobalTextDBs.Keys.ToList()
                : changed.Where(o => o.StartsWith("DATA/TEXT/") && o.EndsWith(".TXT")).Select(Path.GetFileNameWithoutExtension).Distinct().ToList();
            if (textDatabases.Count != 0)
            {
                try
                {
                    LocalisationHandler text = new LocalisationHandler();
                    foreach (string database in textDatabases)
                        text.RefreshLoadedStrings(database);
                    change.Reloaded.Add("the game's text");
                }
                catch (Exception e)
                {
                    change.Problems.Add("The game's text couldn't be loaded again (" + e.Message + ").");
                }
            }
            if (Changed(o => o.StartsWith("DATA/SOUND") || o.EndsWith("/LEVEL_SOUND_OVERRIDE.PCK")))
                Audio.SoundPreviewLibrary.Invalidate();
            //Everything picked from lists of the game's own things (items, characters, animations...): read from the files again
            if (Changed(o => ModToolkit.LevelOf(o) == null))
                Task.Run(() => { try { EnumStringListViewItems.RefreshGlobalEntries(); } catch { } });

            //The open level, where it's shown from again once it's loaded - its own files changed, or the global textures it keeps copies of
            if (change.Level != null && editor != null && !editor.IsDisposed && (globalTextures || Changed(o => InLevel(o, change.Level) && !o.EndsWith("/WORLD/BEHAVIOR_TREE.DB"))))
            {
                if (Level.GetLevels(Singleton.PathToAI).Any(o => string.Equals(o.Replace('\\', '/'), change.Level.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)))
                {
                    editor.ReloadLevel(globalTextures);
                    change.Reloaded.Add("the level " + LevelTitle(change.Level));
                    WhenLoaded(change.Level, () => ReturnTo(change.Place));
                }
                else
                {
                    change.Problems.Add("The level " + LevelTitle(change.Level) + " isn't there any more (a mod added it), so it can't be shown: pick another level to work on, and don't save this one.");
                    editor.ShowLevelPicker();
                }
            }
            else if (globalTextures && editor != null && !editor.IsDisposed)
                editor.RestartLevelViewerIfRunning();
        }

        /* Each game file under DATA as it is now: its size and time (what anything writing it changes). The folders the game's
           files aren't in (OpenCAGE's own under DATA/MODTOOLS, caches, each level's DEV logs) aren't walked. Null when a folder
           couldn't be read: what's here then can't be told from what's gone. */
        private static Dictionary<string, (long Size, long Time)> Stamps()
        {
            Dictionary<string, (long, long)> stamps = new Dictionary<string, (long, long)>();
            DirectoryInfo data = new DirectoryInfo(Path.Combine(Singleton.PathToAI, "DATA"));
            if (!data.Exists)
                return stamps;
            Stack<DirectoryInfo> folders = new Stack<DirectoryInfo>();
            folders.Push(data);
            try
            {
                while (folders.Count != 0)
                {
                    DirectoryInfo folder = folders.Pop();
                    foreach (FileSystemInfo entry in folder.EnumerateFileSystemInfos())
                    {
                        string normalised = ModToolkit.NormaliseFull(Singleton.PathToAI, entry.FullName);
                        if (normalised == null)
                            continue;
                        if (entry is DirectoryInfo inside)
                        {
                            if (!ModToolkit.IsExcluded(normalised + "/"))
                                folders.Push(inside);
                        }
                        else if (entry is FileInfo file && !ModToolkit.IsExcluded(normalised))
                            stamps[normalised] = (file.Length, file.LastWriteTimeUtc.Ticks);
                    }
                }
            }
            catch
            {
                return null;
            }
            return stamps;
        }

        /* Whether a game file is one of a level's: its folder, or (the two Nostromo levels) the _PATCH folder beside it */
        private static bool InLevel(string normalisedPath, string levelName)
        {
            string folder = "DATA/ENV/" + levelName.Replace('\\', '/').ToUpperInvariant();
            return normalisedPath.StartsWith(folder + "/") || normalisedPath.StartsWith(folder + "_PATCH/");
        }

        private static string LevelTitle(string levelName)
        {
            string name = levelName.Replace('\\', '/');
            return name.Substring(name.LastIndexOf('/') + 1);
        }

        /* Windows open now that keep their own copy of a file that may change (any, when null): the configuration editors, the
           UI editor and Launch Game's mod ticks (UI.PAK), and the localisation editor (the game's and levels' text). Those that
           only write it on their Save button may hold edits not saved (saveButton); the rest save it back by themselves. */
        private static List<Form> WindowsHolding(ICollection<string> mayChange, bool saveButton)
        {
            HashSet<Type> configEditors = mayChange == null ? null
                : new HashSet<Type>(mayChange.Where(o => o.StartsWith("DATA/")).SelectMany(o => MCP.McpGameDataTools.EditorsFor(o.Substring("DATA/".Length))));
            bool interfacePak = mayChange == null || mayChange.Contains("DATA/UI.PAK") || mayChange.Contains("DATA/GLOBAL/UI.PAK");
            bool text = mayChange == null || mayChange.Any(o => o.StartsWith("DATA/TEXT/") || (o.StartsWith("DATA/ENV/") && o.Contains("/TEXT/")));

            List<Form> windows = new List<Form>();
            foreach (Form window in Application.OpenForms.Cast<Form>())
            {
                if (window.IsDisposed)
                    continue;
                bool holds, onSave;
                if (window is LocalisationEditor) { holds = text; onSave = true; }
                else if (window is EditPAK2 || window is LaunchGame) { holds = interfacePak; onSave = false; }
                else
                {
                    onSave = MCP.McpGameDataTools.SaveButtonEditors.Contains(window.GetType());
                    //Any configuration editor at all when anything may change; the autosaving ones then close on Revert Configs' event
                    holds = configEditors == null ? onSave : configEditors.Contains(window.GetType());
                }
                if (holds && onSave == saveButton)
                    windows.Add(window);
            }
            return windows;
        }

        /* Once the level has loaded again (or failed to), do this - on the UI thread */
        private static void WhenLoaded(string levelName, Action then)
        {
            Timer timer = new Timer() { Interval = 250 };
            Stopwatch idle = new Stopwatch(), waited = Stopwatch.StartNew();
            timer.Tick += (s, e) =>
            {
                CommandsEditor editor = Singleton.Editor;
                bool loading = editor != null && !editor.IsDisposed && (editor.IsLevelLoadInProgress || editor.IsLevelLoaderRunning);
                bool ready = MCP.McpEditor.IsReady(levelName);
                if (loading || ready) idle.Reset();
                else if (!idle.IsRunning) idle.Start();
                //Ready; or nothing loading for a while without being ready (the load failed, or another level was opened); or ages
                if (ready || idle.ElapsedMilliseconds > 3000 || waited.Elapsed.TotalMinutes > 15)
                {
                    timer.Stop();
                    timer.Dispose();
                    if (ready)
                    {
                        try { then(); }
                        catch { }
                    }
                }
            };
            timer.Start();
        }

        /* Back to where the user was in the level: the composite (down the same path, as far as it still leads) and what was selected */
        private static void ReturnTo(CompositePath.Place place)
        {
            CompositeBrowser browser = Singleton.Editor?.CompositeBrowser;
            if (place == null || browser?.Content?.Level?.Commands == null)
                return;
            CompositePath.Snapshot path = CompositePath.Resolve(browser.Content.Level.Commands, place, out Composite landed);
            if (landed == null)
                return;
            browser.SelectCompositeAndReloadList(landed, path);
            Singleton.Editor.CompositeDisplay?.RestoreSelection(place.SelectionIn(landed));
        }
    }
}
#endif
