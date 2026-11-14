using OpenCAGE.Undo;
using System;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>Undo and redo as the main window offers them: the Edit menu, the key chords, the status line.</summary>
    public partial class CommandsEditor
    {
        private ToolStripMenuItem _undoMenuItem;
        private ToolStripMenuItem _redoMenuItem;
        private ToolStripMenuItem _undoSelectionMenuItem;
        private System.Windows.Forms.Timer _undoStatusTimer;
        private string _lastUndoStatus;

        private void SetupUndo()
        {
            UndoStack.Current.ContextFactory = () => new UndoContext(_compositeBrowser?.Content, new WinFormsUndoUi(this));

            _undoMenuItem = new ToolStripMenuItem("Undo") { ShortcutKeyDisplayString = "Ctrl+Z" };
            _redoMenuItem = new ToolStripMenuItem("Redo") { ShortcutKeyDisplayString = "Ctrl+Y" };
            _undoMenuItem.Click += (sender, e) => UndoStack.Current.Undo();
            _redoMenuItem.Click += (sender, e) => UndoStack.Current.Redo();
            toolStripButton5.DropDownItems.Insert(0, _undoMenuItem);
            toolStripButton5.DropDownItems.Insert(1, _redoMenuItem);
            toolStripButton5.DropDownOpening += (sender, e) => RefreshUndoMenu();

            UndoStack.Current.Changed += RefreshUndoMenu;
            UndoStack.Current.SelectionStepsChanged += RefreshUndoMenu;
            UndoStack.Current.Status += ShowUndoStatus;

            //Options > Entity Display: whether selecting is a step of its own
            _undoSelectionMenuItem = new ToolStripMenuItem("Undo Includes Selection Changes")
            {
                ToolTipText = "If enabled, selecting and deselecting entities (in the entity list, on a flowgraph or in the viewport) is a step for Undo and Redo of its own, so a selection lost to a stray click comes back with Ctrl+Z.",
                Checked = SelectionUndo.Enabled,
            };
            _undoSelectionMenuItem.Click += (sender, e) => ToggleUndoIncludesSelection();
            entityDisplayToolStripMenuItem.DropDownItems.Add(_undoSelectionMenuItem);
            entityDisplayToolStripMenuItem.DropDownOpening += (sender, e) => _undoSelectionMenuItem.Checked = SelectionUndo.Enabled;

            //Edits name entities of the level they were made in; a different level makes them meaningless. Cleared as the
            //level closes too, as the edits hold on to it
            Singleton.OnLevelLoaded += content => UndoStack.Current.Clear();
            Singleton.OnLevelClosing += content => UndoStack.Current.Clear();

            RefreshUndoMenu();
        }

        /* Off, the selection steps already taken go too: the history is as if they were never recorded */
        private void ToggleUndoIncludesSelection()
        {
            SettingsManager.SetBool(Settings.UndoIncludesSelection, !SelectionUndo.Enabled);
            _undoSelectionMenuItem.Checked = SelectionUndo.Enabled;
            if (!SelectionUndo.Enabled)
                UndoStack.Current.RemoveSelectionSteps();
        }

        private void RefreshUndoMenu()
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                //Level load clears the stack from the loader's thread
                try { BeginInvoke(new Action(RefreshUndoMenu)); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }
            if (_undoMenuItem == null)
                return;

            bool loaded = _compositeBrowser?.Content?.Level?.Commands != null;
            bool canUndo = loaded && UndoStack.Current.CanUndo;
            bool canRedo = loaded && UndoStack.Current.CanRedo;
            _undoMenuItem.Enabled = canUndo;
            _redoMenuItem.Enabled = canRedo;
            _undoMenuItem.Text = canUndo ? "Undo " + UndoStack.Current.UndoLabel : "Undo";
            _redoMenuItem.Text = canRedo ? "Redo " + UndoStack.Current.RedoLabel : "Redo";
        }

        /* "Undid Move Door_1" for a few seconds, then gone - unless something else has taken the bar since */
        private void ShowUndoStatus(string text)
        {
            if (IsDisposed || statusStrip == null || statusStrip.IsDisposed || statusText == null)
                return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(ShowUndoStatus), text); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            if (_undoStatusTimer == null)
            {
                _undoStatusTimer = new System.Windows.Forms.Timer() { Interval = 4000 };
                _undoStatusTimer.Tick += (sender, e) =>
                {
                    _undoStatusTimer.Stop();
                    if (statusText != null && statusText.Text == _lastUndoStatus)
                        statusText.Text = "";
                };
            }

            _lastUndoStatus = text;
            statusText.Text = text;
            statusStrip.Update();
            _undoStatusTimer.Stop();
            _undoStatusTimer.Start();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (UndoKeys.TryHandle(keyData))
                return true;
            if (UnityConnection.ViewerCreateModeKeys.TryHandle(keyData))
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
