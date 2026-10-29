using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.AnimTrees
{
    public partial class AnimTreeEditor : Form
    {
        public static DockPanel DockPanel;

        private AnimationSets _animationSets;

        public AnimTreeEditor()
        {
            InitializeComponent();
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/animation-tree-editor/");

            Theming.ThemeManager.ApplyToForm(this);
            Theming.ThemeManager.ApplyToDockPanel(dockPanel);

            DockPanel = this.dockPanel;
            //A closed editor must not stay reachable through the static, with every tree it had open
            this.Disposed += (s, e) => { if (DockPanel == dockPanel) DockPanel = null; };

            _animationSets = new AnimationSets();
            _animationSets.Show(dockPanel, DockState.DockLeft);

            FormClosing += AnimTreeEditor_FormClosing;
        }

        //The tree sets whose unsaved changes are let go of as the editor closes (null: nothing to let go of)
        private List<CATHODE.AnimTreeDB> _lettingGo;

        /* Tree edits go straight into the animations everything shares, where the next write of ANIMATION.PAK from anywhere
           would take them out: closing asks to save them, and puts the trees back as ANIMATION.PAK holds them if not */
        private void AnimTreeEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            _lettingGo = null;
            //The application is going: nothing in memory outlives it
            CathodeLib.Animation animations = Singleton.Global?.Animations;
            if (_animationSets == null || animations == null || CloseReasons.IsApplicationShutdown(e))
                return;

            _animationSets.CommitPendingEdits();
            List<CATHODE.AnimTreeDB> changed = AnimationSets.ChangedSets(animations);
            if (changed.Count == 0 && !AnimTreeLayoutManager.HasPending)
                return;

            //Not asked when they can't be written - ANIMATION.PAK changed on disk, or is being loaded again (the Mod Manager
            //asked about them already): they're let go of
            if (e.CloseReason == CloseReason.UserClosing && AnimationPakWrite.Stale(animations) == null)
            {
                DialogResult answer = MessageBox.Show(
                    "There are animation tree changes that haven't been written to ANIMATION.PAK.\n\nSave them before closing?",
                    "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

                if (answer == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (answer == DialogResult.Yes)
                {
                    EditorUtils.CloseAI(null, thisInstallOnly: true);
                    //Not saved after all (it said why): stay open, rather than letting the changes go
                    if (!_animationSets.SaveAll())
                        e.Cancel = true;
                    return;
                }
            }
            _lettingGo = changed;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_lettingGo != null)
                AnimationSets.LetGo(Singleton.Global?.Animations, _lettingGo);
            _lettingGo = null;
            base.OnFormClosed(e);
        }

        /// <summary>Show a tree as if it were picked from the lists. Returns its graph (null when the lists do not hold it).</summary>
        internal AnimationTreeGraph OpenTree(CATHODE.AnimTreeDB database, CATHODE.Animations.AnimationTree tree)
        {
            return _animationSets?.OpenTree(database, tree);
        }

        private void saveBtn_Click(object sender, EventArgs e)
        {
            if (_animationSets == null)
                return;

            saveBtn.Enabled = false;
            try
            {
                EditorUtils.CloseAI(null, thisInstallOnly: true);
                if (_animationSets.SaveAll())
                    MessageBox.Show("Animation trees saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                saveBtn.Enabled = true;
            }
        }
    }
}
