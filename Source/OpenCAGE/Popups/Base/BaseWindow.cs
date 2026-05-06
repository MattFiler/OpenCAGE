using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using DarkModeForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor.Popups.Base
{
    public partial class BaseWindow : Form
    {
        protected LevelContent Content => Singleton.Editor?.CommandsDisplay?.Content;

        private WindowClosesOn _closesOn;
        private DarkModeCS _dm;

        public BaseWindow()
        {
            InitializeComponent();
#if USE_DARK_MODE
            _dm = new DarkModeCS(this);
#endif

            this.BringToFront();
            this.Focus();
        }

        public BaseWindow(WindowClosesOn config)
        {
            InitializeComponent();
#if USE_DARK_MODE
            _dm = new DarkModeCS(this);
#endif

            _closesOn = config;

            if (_closesOn.HasFlag(WindowClosesOn.COMMANDS_RELOAD))
                Singleton.OnLevelLoaded += OnCommandsSelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_ENTITY_SELECTION))
                Singleton.OnEntitySelected += OnEntitySelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_COMPOSITE_SELECTION))
                Singleton.OnCompositeSelected += OnCompositeSelected;
            if (_closesOn.HasFlag(WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED))
                Singleton.OnCAGEAnimationEditorOpened += OnCAGEAnimationEditorOpened;

            this.BringToFront();
            this.Focus();
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
