using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups.Base
{
    public partial class BaseWindow : Form
    {
        protected LevelContent Content => Singleton.Editor?.CompositeBrowser?.Content;

        private WindowClosesOn _closesOn;

        /// <summary>
        /// Set this in a derived window's constructor to tie the window to the main editor window, so it can't
        /// end up behind it. Intended for pickers/dialogs launched from a field or button - larger standalone
        /// editors should stay independent so they remain separately switchable.
        /// </summary>
        protected bool StayAboveEditor { get; set; } = false;

        public BaseWindow()
        {
            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);
        }

        public BaseWindow(WindowClosesOn config)
        {
            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);

            _closesOn = config;
            Subscribe();
        }

        /// <summary>
        /// What closes a window that is both an editor in its own right, opened from the toolbar, and a picker.
        /// As an editor only a level load closes it. As a picker it chooses for the selected entity, so a new
        /// entity or composite selection closes it too: what it would write to has moved on. A picker that
        /// chooses for another window instead is handed to that window with <see cref="CloseWith"/>.
        /// </summary>
        protected static WindowClosesOn EditorOrPicker(bool picker)
        {
            if (!picker)
                return WindowClosesOn.COMMANDS_RELOAD;
            return WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION;
        }

        private Form _opener;

        /// <summary>
        /// Tie this window to the window that opened it, for a picker that chooses for that window rather than
        /// for the selected entity - a texture for the Character Asset Sets editor, a model for the animation
        /// preview. It closes when that window closes, and leaves selection changes to it: a toolbar editor
        /// stays open as the selection changes, so what it opened should too. A level load still closes it
        /// if it did before.
        /// </summary>
        public void CloseWith(Form opener)
        {
            if (opener == null || opener == _opener || IsDisposed)
                return;

            Unsubscribe();
            _closesOn &= WindowClosesOn.COMMANDS_RELOAD;
            Subscribe();

            ReleaseOpener();
            _opener = opener;
            _opener.FormClosed += OnOpenerClosed;
            _opener.Disposed += OnOpenerClosed;
        }

        private void OnOpenerClosed(object sender, EventArgs e)
        {
            ReleaseOpener();

            //An application exit closes every window itself, and its walk of them is left alone (see CloseReasons)
            if (e is FormClosedEventArgs closed && CloseReasons.IsApplicationShutdown(closed.CloseReason))
                return;
            if (!IsDisposed)
                this.Close();
        }

        private void ReleaseOpener()
        {
            if (_opener == null)
                return;
            _opener.FormClosed -= OnOpenerClosed;
            _opener.Disposed -= OnOpenerClosed;
            _opener = null;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            //NOTE: this has to happen once the window is actually shown - in the constructor there's no handle
            //yet, so BringToFront/Focus silently do nothing.
            if (StayAboveEditor)
                TieToEditorWindow();

            if (!ActivateOnShown)
                return;
            this.BringToFront();
            this.Activate();
        }

        /// <summary>
        /// Whether showing the window makes it the active one. A window that only reports (a progress
        /// window) says no: it stays on top by being TopMost, and never takes focus - so closing it never
        /// hands focus back to whatever had it, which matters when that is the embedded viewer.
        /// </summary>
        protected virtual bool ActivateOnShown => true;

        /* Own this window from the main editor window, so Windows keeps it above it in the z-order */
        private void TieToEditorWindow()
        {
            try
            {
                if (this.Owner != null)
                    return;

                Form editor = Singleton.Editor;
                if (editor == null || editor.IsDisposed || editor == this || !editor.TopLevel)
                    return;

                this.Owner = editor;
            }
            catch
            {
                //Ownership is a nicety - if Windows rejects it, the window still works
            }
        }

        private void OnFormClosed(Object sender, FormClosedEventArgs e)
        {
            Unsubscribe();
            ReleaseOpener();
        }

        private void Subscribe()
        {
            if (_closesOn.HasFlag(WindowClosesOn.COMMANDS_RELOAD))
                Singleton.OnLevelLoaded += OnCommandsSelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_ENTITY_SELECTION))
                Singleton.OnEntitySelected += OnEntitySelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_COMPOSITE_SELECTION))
                Singleton.OnCompositeSelected += OnCompositeSelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED))
                Singleton.OnCAGEAnimationEditorOpened += OnCAGEAnimationEditorOpened;
        }

        //Also from Dispose: Close() on a form that never had a handle disposes it without raising FormClosed
        private void Unsubscribe()
        {
            if (_closesOn.HasFlag(WindowClosesOn.COMMANDS_RELOAD))
                Singleton.OnLevelLoaded -= OnCommandsSelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_ENTITY_SELECTION))
                Singleton.OnEntitySelected -= OnEntitySelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_COMPOSITE_SELECTION))
                Singleton.OnCompositeSelected -= OnCompositeSelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED))
                Singleton.OnCAGEAnimationEditorOpened -= OnCAGEAnimationEditorOpened;
        }

        private void OnCommandsSelected(LevelContent content)
        {
            //Raised on the level loader's thread; closing is a window operation, so it goes to the UI thread
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(Close)); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }
            this.Close();
        }

        private void OnEntitySelected(Entity entity)
        {
            this.Close();
        }

        private void OnCompositeSelected(Composite composite)
        {
            this.Close();
        }

        private void OnCAGEAnimationEditorOpened()
        {
            this.Close();
        }
    }

    [Flags]
    public enum WindowClosesOn
    {
        COMMANDS_RELOAD = 1,
        NEW_ENTITY_SELECTION = 2,
        NEW_COMPOSITE_SELECTION = 4,

        NEW_CAGEANIM_EDITOR_OPENED = 8,

        NONE = 16,
    }
}
