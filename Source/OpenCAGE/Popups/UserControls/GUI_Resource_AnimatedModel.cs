using CATHODE;
using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor.Popups.UserControls
{
    public partial class GUI_Resource_AnimatedModel : ResourceUserControl
    {
        //this is done, for now, but the EnvironmentAnimations parser needs completing 

        private EnvironmentAnimations.EnvironmentAnimation _envAnim = null;

        public GUI_Resource_AnimatedModel() : base()
        {
            InitializeComponent();

            skeletonList.BeginUpdate();
            skeletonList.Items.Clear();
            foreach (var skeleton in Singleton.AllSkeletons)
            {
                skeletonList.Items.Add(skeleton);
            }
            skeletonList.EndUpdate();
        }

        public override void PopulateUI(ResourceReference resource)
        {
            _envAnim = resource.AnimatedModel;

            if (_envAnim.SkeletonName == "")
                skeletonList.SelectedIndex = 0;
            else
                skeletonList.SelectedItem = _envAnim.SkeletonName;
        }

        private void animatedModelIndex_SelectedIndexChanged(object sender, EventArgs e)
        {
            _envAnim.SkeletonName = skeletonList.SelectedItem.ToString();
            Singleton.OnResourceModified?.Invoke();
        }
    }
}
