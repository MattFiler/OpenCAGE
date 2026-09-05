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

            if (_closesOn.HasFlag(WindowClosesOn.COMMANDS_RELOAD))
                Singleton.OnLevelLoaded += OnCommandsSelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_ENTITY_SELECTION))
                Singleton.OnEntitySelected += OnEntitySelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_COMPOSITE_SELECTION))
                Singleton.OnCompositeSelected += OnCompositeSelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED))
                Singleton.OnCAGEAnimationEditorOpened += OnCAGEAnimationEditorOpened;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            //NOTE: this has to happen once the window is actually shown - in the constructor there's no handle
            //yet, so BringToFront/Focus silently do nothing.
            if (StayAboveEditor)
                TieToEditorWindow();

            this.BringToFront();
            this.Activate();
        }

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
