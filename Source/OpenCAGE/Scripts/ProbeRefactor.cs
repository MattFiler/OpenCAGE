//TEMP PROBE: De-instance and Create Composite through the real menu items. Picks a composite with script pages and
//an instance that can be de-instanced, opens it, then: De-instance (answering the dialog if one comes up), undo, redo,
//undo; Create Composite on a linked selection (answering the name dialog), open the new composite, undo. After every
//step: the entity list, the page verdicts, and that compiling the rebuilt pages gives back exactly the links the
//refactor wrote. Also the viewport menu's items for the selection. Never saves a level.
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE
{
    internal static class ProbeRefactor
    {
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        private static string _log, _dir, _tag;
        private static CommandsEditor _editor;
        private static Timer _timer;
        private static DateTime _start, _stepStarted;
        private static bool _levelLoaded, _busy;
        private static int _step, _failures, _dialogs;
        private static readonly List<Func<bool>> _steps = new List<Func<bool>>();

        private static Composite _parent;
        private static FunctionEntity _instance;
        private static List<string> _linksBefore, _linksAfterDeinstance;
        private static List<Entity> _selection;
        private static Composite _created;
        private static string _dialogText;

        public static void Start(CommandsEditor editor)
        {
            if (Environment.GetEnvironmentVariable("OPENCAGE_PROBE") != "refactor") return;
            CompositePreviewManager.SuppressDiskWrites = true;
            _editor = editor;
            _dir = Environment.GetEnvironmentVariable("OPENCAGE_PROBE_DIR");
            _tag = Environment.GetEnvironmentVariable("OPENCAGE_PROBE_TAG") ?? "run";
            _log = Path.Combine(_dir, "probe_" + _tag + ".log");
            Directory.CreateDirectory(Path.Combine(_dir, "shots"));
            _start = DateTime.Now;
            Log("probe start tag=" + _tag);
            Singleton.OnLevelLoaded += c => { try { editor.BeginInvoke(new Action(() => { _levelLoaded = true; Log("level loaded: " + c.Level.Name); })); } catch { } };
            new System.Threading.Thread(() => { System.Threading.Thread.Sleep(200000); Log("WATCHDOG: exiting"); Environment.Exit(3); }) { IsBackground = true }.Start();

            //Pick: a composite whose pages carry its links, with an instance that can be de-instanced and is linked
            _steps.Add(() =>
            {
                if (!_levelLoaded) return false;
                Commands commands = Commands();
                foreach (Composite c in commands.Entries.OrderByDescending(o => o.GetEntities().Sum(e => e.childLinks.Count)))
                {
                    if (FlowgraphLayoutManager.GetLayouts(c).Count == 0) continue;
                    List<FlowgraphMeta> judged = FlowgraphLayoutManager.GetLayouts(c).Select(o => CathodeLib.ObjectExtensions.ObjectExtensions.Copy(o)).ToList();
                    FlowgraphLayoutManager.TrimToComposite(judged, c, out int _, out int _);
                    if (!RefactorPages.PagesMatchLinks(c, judged)) continue;
                    foreach (FunctionEntity f in c.functions_dictionary.Values)
                    {
                        if (!CompositeRefactoring.IsCompositeInstance(f, commands)) continue;
                        int linked = c.GetEntities().Sum(e => e.childLinks.Count(l => l.linkedEntityID == f.shortGUID)) + f.childLinks.Count;
                        if (linked < 2) continue;
                        DeinstancePlan plan = DeinstancePlan.Plan(commands, c, f);
                        if (!plan.CanApply) continue;
                        Composite content = commands.GetComposite(f.function);
                        if (content.GetEntities().Count > 60) continue;
                        _parent = c; _instance = f;
                        Log("picked " + c.name + " / " + commands.Utils.GetEntityName(c, f) + " (" + content.name + ", " + linked + " links, " + plan.Issues.Count + " notes)");
                        foreach (RefactorIssue issue in plan.Issues) Log("   " + issue);
                        return true;
                    }
                    if (DateTime.Now - _stepStarted > TimeSpan.FromSeconds(40)) break;
                }
                Check("found a composite and instance to test", false);
                Finish();
                return true;
            });

            //Open it
            _steps.Add(() =>
            {
                _editor.CompositeBrowser.LoadComposite(_parent);
                return true;
            });
            _steps.Add(() =>
            {
                CompositeDisplay d = Display();
                if (d?.Composite != _parent || !d.SupportsFlowgraphs) return Waited(8, "display on the parent with pages");
                d.SaveAllFlowgraphs();
                _linksBefore = Links(_parent);
                Log("opened: " + d.Flowgraphs.Count + " page window(s), " + _linksBefore.Count + " links");
                return true;
            });

            //Menu items for one composite instance
            _steps.Add(() =>
            {
                CompositeDisplay d = Display();
                d.LoadEntity(_instance, false);
                OpenListMenu();
                Check("list menu: De-instance shown for a composite instance", Item("deinstanceToolStripMenuItem").Available);
                Check("list menu: Create Composite shown and enabled", Item("createCompositeToolStripMenuItem").Available && Item("createCompositeToolStripMenuItem").Enabled);
                CloseListMenu();
                ViewportMenu(out bool deinst, out bool create);
                Check("viewport menu: De-instance shown for a composite instance", deinst);
                Check("viewport menu: Create Composite shown", create);
                return true;
            });

            //De-instance
            _steps.Add(() =>
            {
                Singleton.OnSelectionCleared += () => Log("   SELECTION CLEARED at " + string.Join(" <- ", new System.Diagnostics.StackTrace().GetFrames().Skip(1).Take(9).Select(f => f.GetMethod().DeclaringType?.Name + "." + f.GetMethod().Name)));
                Singleton.OnEntitySelected += e => Log("   ENTITY SELECTED " + (e == null ? "null" : e.shortGUID.ToByteString()) + " at " + string.Join(" <- ", new System.Diagnostics.StackTrace().GetFrames().Skip(1).Take(7).Select(f => f.GetMethod().DeclaringType?.Name + "." + f.GetMethod().Name)));
                _dialogText = null;
                AnswerDialogs(true);
                Item("deinstanceToolStripMenuItem").PerformClick();
                CompositeDisplay dd = Display();
                Log("   right after: list selected " + dd.EntityListPanel.List.SelectedEntities.Count + ", inspector entity " + (dd.EntityDisplay?.Entity != null) + ", multi " + (dd.EntityDisplay?.MultiSelectedEntities?.Count ?? -1));
                return true;
            });
            _steps.Add(() =>
            {
                CompositeDisplay d = Display();
                Commands commands = Commands();
                Check("de-instance: instance gone from the parent", _parent.GetEntityByID(_instance.shortGUID) == null);
                Check("de-instance: undo step named " + UndoStack.Current.UndoLabel, UndoStack.Current.UndoLabel?.StartsWith("De-instance") == true);
                Check("de-instance: still on the parent, pages supported", d.Composite == _parent && d.SupportsFlowgraphs);
                Check("de-instance: entity list has every entity of the parent (" + d.EntityListPanel.List.Composite?.name + ")", ListMatches(d));
                PagesMatch("de-instance: parent pages draw its links", _parent);
                int windows = d.Flowgraphs.Count;
                List<string> links = Links(_parent);
                d.SaveAllFlowgraphs();
                _linksAfterDeinstance = Links(_parent);
                Check("de-instance: compiling the pages gives back the same " + links.Count + " links (" + _linksAfterDeinstance.Count + " compiled, " + windows + " page windows)", links.SequenceEqual(_linksAfterDeinstance));
                return true;
            });
            _steps.Add(() =>
            {
                //The selection is posted, to land after the rebuilt pages settle
                CompositeDisplay d = Display();
                bool selected = d.EntityDisplay?.Entity != null || (d.EntityDisplay?.MultiSelectedEntities?.Count ?? 0) > 0;
                if (!selected && DateTime.Now - _stepStarted < TimeSpan.FromSeconds(3)) return false;
                Check("de-instance: the pulled entities are selected afterwards (" + (d.EntityDisplay?.MultiSelectedEntities?.Count ?? (d.EntityDisplay?.Entity != null ? 1 : 0)) + ")", selected);
                Shot("after_deinstance");
                return true;
            });
            _steps.Add(() =>
            {
                UndoStack.Current.Undo();
                return true;
            });
            _steps.Add(() =>
            {
                CompositeDisplay d = Display();
                Check("undo: instance back", _parent.GetEntityByID(_instance.shortGUID) == _instance);
                Check("undo: entity list matches", ListMatches(d));
                PagesMatch("undo: pages draw the links", _parent);
                d.SaveAllFlowgraphs();
                Check("undo: compiled links are the originals", Links(_parent).SequenceEqual(_linksBefore));
                return true;
            });
            _steps.Add(() => { UndoStack.Current.Redo(); return true; });
            _steps.Add(() =>
            {
                CompositeDisplay d = Display();
                Check("redo: instance gone again", _parent.GetEntityByID(_instance.shortGUID) == null);
                Check("redo: entity list matches", ListMatches(d));
                d.SaveAllFlowgraphs();
                Check("redo: compiled links are the de-instanced ones", Links(_parent).SequenceEqual(_linksAfterDeinstance));
                UndoStack.Current.Undo();
                return true;
            });
            _steps.Add(() =>
            {
                CompositeDisplay d = Display();
                d.SaveAllFlowgraphs();
                Check("undo again: back to the originals", _parent.GetEntityByID(_instance.shortGUID) == _instance && Links(_parent).SequenceEqual(_linksBefore));
                return true;
            });

            //Create Composite on a linked selection
            _steps.Add(() =>
            {
                CompositeDisplay d = Display();
                _selection = PickSelection(_parent);
                Log("selection: " + string.Join(", ", _selection.Select(o => Commands().Utils.GetEntityName(_parent, o))));
                d.ApplyMultiSelection(_selection);
                if (_selection.Count == 1) d.LoadEntity(_selection[0], false);
                OpenListMenu();
                Check("list menu: Create Composite shown for " + _selection.Count, Item("createCompositeToolStripMenuItem").Available && Item("createCompositeToolStripMenuItem").Enabled);
                Check("list menu: De-instance hidden for a multi-selection", !Item("deinstanceToolStripMenuItem").Available || _selection.Count == 1);
                CloseListMenu();
                _dialogText = null;
                AnswerDialogs(true);
                Item("createCompositeToolStripMenuItem").PerformClick();
                return true;
            });
            _steps.Add(() =>
            {
                CompositeDisplay d = Display();
                Commands commands = Commands();
                Check("create: the name dialog came up", _dialogText != null);
                FunctionEntity placed = _parent.functions_dictionary.Values.FirstOrDefault(f => !f.function.IsFunctionType && commands.GetComposite(f.function)?.name?.EndsWith("_Group") == true);
                _created = placed == null ? null : commands.GetComposite(placed.function);
                Check("create: new composite placed in the parent (" + _created?.name + ")", _created != null && commands.Entries.Contains(_created));
                Check("create: selection moved out", _selection.All(o => _parent.GetEntityByID(o.shortGUID) == null) && _created != null && _selection.All(o => _created.GetEntityByID(o.shortGUID) == o));
                Check("create: undo step named " + UndoStack.Current.UndoLabel, UndoStack.Current.UndoLabel?.StartsWith("Create composite") == true);
                Check("create: entity list matches", ListMatches(d));
                PagesMatch("create: parent pages draw its links", _parent);
                if (_created != null) PagesMatch("create: new composite's pages draw its links", _created);
                Check("create: new composite judged supported", _created != null && FlowgraphLayoutManager.IsCompatible(_created));
                List<string> links = Links(_parent);
                d.SaveAllFlowgraphs();
                Check("create: compiling the parent's pages gives back the same links", links.SequenceEqual(Links(_parent)));
                Shot("after_create");
                if (_created != null) _editor.CompositeBrowser.LoadComposite(_created);
                return true;
            });
            _steps.Add(() =>
            {
                CompositeDisplay d = Display();
                if (d?.Composite != _created) return Waited(8, "display on the new composite");
                Log("new composite open: " + d.Flowgraphs.Count + " page window(s), " + _created.variables_dictionary.Count + " pins, list " + d.EntityListPanel.List.Composite?.name);
                List<string> links = Links(_created);
                d.SaveAllFlowgraphs();
                Check("new composite: compiling its pages gives back its " + links.Count + " links", links.SequenceEqual(Links(_created)));
                Shot("new_composite");
                UndoStack.Current.Undo();
                return true;
            });
            _steps.Add(() =>
            {
                CompositeDisplay d = Display();
                Commands commands = Commands();
                Check("undo create: back on the parent", d.Composite == _parent);
                Check("undo create: new composite gone", _created == null || !commands.Entries.Contains(_created));
                Check("undo create: selection back in the parent", _selection.All(o => _parent.GetEntityByID(o.shortGUID) == o));
                d.SaveAllFlowgraphs();
                Check("undo create: compiled links are the originals", Links(_parent).SequenceEqual(_linksBefore));
                Check("undo create: entity list matches", ListMatches(d));
                Finish();
                return true;
            });

            _timer = new Timer() { Interval = 400 };
            _timer.Tick += (s, e) => Tick();
            _timer.Start();
        }

        private static void Tick()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                if (_step >= _steps.Count) return;
                if (_stepStarted == default(DateTime)) _stepStarted = DateTime.Now;
                if (_steps[_step]())
                {
                    _step++;
                    _stepStarted = DateTime.Now;
                }
            }
            catch (Exception e)
            {
                Log("STEP " + _step + " THREW: " + e);
                _failures++;
                Finish();
            }
            finally { _busy = false; }
        }

        private static bool Waited(int seconds, string what)
        {
            if (DateTime.Now - _stepStarted < TimeSpan.FromSeconds(seconds)) return false;
            Check("waited for " + what, false);
            return true;
        }

        /* Dialogs are modal: the timer still runs inside their loop, so a second timer answers them */
        private static Timer _answer;
        private static void AnswerDialogs(bool accept)
        {
            _answer?.Stop();
            _answer = new Timer() { Interval = 300 };
            _answer.Tick += (s, e) =>
            {
                RefactorDialog dialog = Application.OpenForms.OfType<RefactorDialog>().FirstOrDefault(f => f.Visible);
                if (dialog == null) return;
                _answer.Stop();
                _dialogs++;
                TextBox issues = Field<TextBox>(dialog, "issuesText");
                TextBox name = Field<TextBox>(dialog, "nameInput");
                Button ok = Field<Button>(dialog, "okButton");
                _dialogText = dialog.Text + " | name=" + (name.Visible ? name.Text : "-") + " | ok=" + ok.Enabled + " | " + issues.Text.Replace(Environment.NewLine, " / ");
                Log("dialog: " + _dialogText);
                Shot("dialog_" + _dialogs, dialog);
                if (accept && ok.Enabled) ok.PerformClick();
                else Field<Button>(dialog, "cancelButton").PerformClick();
            };
            _answer.Start();
        }

        #region Helpers
        private static Commands Commands() => _editor.CompositeBrowser.Content.Level.Commands;
        private static CompositeDisplay Display() => Singleton.Editor?.CompositeDisplay;

        private static List<string> Links(Composite composite)
        {
            List<string> links = new List<string>();
            foreach (Entity e in composite.GetEntities())
                foreach (EntityConnector l in e.childLinks)
                    links.Add(e.shortGUID.AsUInt32 + "." + l.thisParamID.AsUInt32 + ">" + l.linkedEntityID.AsUInt32 + "." + l.linkedParamID.AsUInt32);
            links.Sort(StringComparer.Ordinal);
            return links;
        }

        private static void PagesMatch(string what, Composite composite)
        {
            List<FlowgraphMeta> pages = FlowgraphLayoutManager.GetLayouts(composite);
            Check(what + " (" + pages.Count + " pages)", RefactorPages.PagesMatchLinks(composite, pages));
        }

        private static bool ListMatches(CompositeDisplay d)
        {
            HashSet<ShortGuid> listed = new HashSet<ShortGuid>(Field<Dictionary<ShortGuid, ListViewItem>>(d.EntityListPanel.List, "_itemsById")?.Keys ?? Enumerable.Empty<ShortGuid>());
            HashSet<ShortGuid> held = new HashSet<ShortGuid>(d.Composite.GetEntities().Select(o => o.shortGUID));
            bool ok = listed.SetEquals(held) || (listed.IsSubsetOf(held) && d.EntityListPanel.List.Composite == d.Composite);
            if (!ok) Log("   list has " + listed.Count + ", composite " + held.Count + "; missing " + held.Except(listed).Count() + ", extra " + listed.Except(held).Count());
            return ok;
        }

        private static List<Entity> PickSelection(Composite composite)
        {
            //An entity with links both ways, and what it links to that is not a pin
            Entity start = composite.functions_dictionary.Values.Where(o => o.function.IsFunctionType)
                .OrderByDescending(o => o.childLinks.Count + composite.GetEntities().Sum(x => x.childLinks.Count(l => l.linkedEntityID == o.shortGUID))).FirstOrDefault();
            List<Entity> chosen = new List<Entity>() { start };
            foreach (EntityConnector l in start.childLinks)
            {
                Entity t = composite.GetEntityByID(l.linkedEntityID);
                if (t != null && !(t is VariableEntity) && !chosen.Contains(t) && chosen.Count < 4) chosen.Add(t);
            }
            return chosen;
        }

        private static void OpenListMenu()
        {
            EntityList list = Display().EntityListPanel;
            MethodInfo opening = typeof(EntityList).GetMethod("EntityListContextMenu_Opening", BindingFlags.NonPublic | BindingFlags.Instance);
            opening.Invoke(list, new object[] { null, new CancelEventArgs() });
        }

        private static void CloseListMenu() { }

        private static ToolStripMenuItem Item(string name) => Field<ToolStripMenuItem>(Display().EntityListPanel, name);

        private static void ViewportMenu(out bool deinstance, out bool create)
        {
            Type menu = typeof(Send).Assembly.GetType("OpenCAGE.UnityConnection.ViewerContextMenu");
            MethodInfo show = menu.GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Static);
            Packet packet = new Packet() { context_menu_has_selection = true };
            show.Invoke(null, new object[] { packet });
            deinstance = ((ToolStripMenuItem)menu.GetField("_deinstance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Visible;
            create = ((ToolStripMenuItem)menu.GetField("_createComposite", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Visible;
            ContextMenuStrip strip = (ContextMenuStrip)menu.GetField("_menu", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Log("viewport menu: " + string.Join(" | ", strip.Items.Cast<ToolStripItem>().Where(i => i.Available).Select(i => i is ToolStripSeparator ? "---" : i.Text)));
            menu.GetMethod("Close", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }

        private static T Field<T>(object owner, string name) where T : class
        {
            for (Type t = owner.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null) return f.GetValue(owner) as T;
            }
            return null;
        }

        private static void Shot(string name, Form form = null)
        {
            try
            {
                Form target = form ?? _editor;
                using (Bitmap bmp = new Bitmap(target.Width, target.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        IntPtr hdc = g.GetHdc();
                        PrintWindow(target.Handle, hdc, 2);
                        g.ReleaseHdc(hdc);
                    }
                    bmp.Save(Path.Combine(_dir, "shots", _tag + "_" + name + ".png"), ImageFormat.Png);
                }
            }
            catch (Exception e) { Log("shot failed: " + e.Message); }
        }

        private static void Check(string what, bool ok)
        {
            if (!ok) _failures++;
            Log((ok ? "PASS " : "FAIL ") + what);
        }

        private static void Log(string text)
        {
            File.AppendAllText(_log, "[" + (DateTime.Now - _start).TotalSeconds.ToString("0.0") + "] " + text + Environment.NewLine);
        }

        private static void Finish()
        {
            Log("done: " + _failures + " failure(s), " + _dialogs + " dialog(s)");
            _timer?.Stop();
            _step = int.MaxValue;
            Environment.Exit(_failures == 0 ? 0 : 2);
        }
        #endregion
    }
}
