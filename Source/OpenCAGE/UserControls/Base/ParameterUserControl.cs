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

namespace CommandsEditor.UserControls
{
    public partial class ParameterUserControl : UserControl
    {
        public Action<Parameter> OnDeleted;
        public Parameter Parameter = null;

        protected LevelContent Content => Singleton.Editor?.CommandsDisplay?.Content;

        protected bool _isModified = false;
        protected bool _hasDoneSetup = false;

        public ParameterUserControl()
        {
            InitializeComponent();
        }

        protected void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnDeleted?.Invoke(Parameter);
        }

        //This is kinda hacked in and should be handled a little nicer
        private ShortGuid _compositeGUID;
        private ShortGuid _entityGUID;
        private ShortGuid _parameterGUID;
        public void TrackInstanceInfo(ShortGuid composite, ShortGuid entity, ShortGuid parameter)
        {
            _compositeGUID = composite;
            _entityGUID = entity;
            _parameterGUID = parameter;
        }

        //override this and set the UI as bold. make sure to call the base so we can track.
        public virtual void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            if (_entityGUID != ShortGuid.Invalid)
                Singleton.OnParameterModified?.Invoke();

#if AUTO_POPULATE_PARAMS
            if (!_hasDoneSetup || _isModified || _compositeGUID.IsInvalid)
                return;

            if (!_isModified && fontToUpdate != null)
                fontToUpdate.Font = new Font(fontToUpdate.Font, FontStyle.Bold);

            _isModified = true;

            if (updateDatabase)
                ParameterModificationTracker.SetParameterModified(_compositeGUID, _entityGUID, _parameterGUID);
#endif
        }
    }
}
