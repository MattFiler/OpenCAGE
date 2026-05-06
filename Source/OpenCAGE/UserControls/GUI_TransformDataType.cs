using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CATHODE.Scripting;
using CATHODE;
using CathodeLib;
using Newtonsoft.Json;
using CATHODE.Scripting.Internal;
using OpenCAGE;

namespace CommandsEditor.UserControls
{
    public partial class GUI_TransformDataType : ParameterUserControl
    {
        public Action OnValueChanged;

        cTransform transformVal = null;
        Entity _entity = null;

        public GUI_TransformDataType()
        {
            InitializeComponent();
            this.ContextMenuStrip = contextMenuStrip1;
            this.deleteToolStripMenuItem.Click += new EventHandler(deleteToolStripMenuItem_Click);

            POS_X.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            POS_Y.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            POS_Z.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            ROT_X.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStepRot);
            ROT_Y.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStepRot);
            ROT_Z.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStepRot);
        }

        public void PopulateUI(Entity entity, cTransform cTrans, ShortGuid paramID)
        {
            PopulateUI(entity, cTrans, ShortGuidUtils.FindString(paramID));
        }
        public void PopulateUI(Entity entity, cTransform cTrans, string title, bool disableInput = false)
        {
            _entity = entity;

            POSITION_VARIABLE_DUMMY.Text = title;
            transformVal = cTrans;
            this.deleteToolStripMenuItem.Text = "Delete '" + title + "'";

            UpdateUI();

            if (disableInput)
            {
                POS_X.Enabled = false;
                POS_Y.Enabled = false;
                POS_Z.Enabled = false;
                ROT_X.Enabled = false;
                ROT_Y.Enabled = false;
                ROT_Z.Enabled = false;
            }

            _hasDoneSetup = true;
        }

        private void UpdateUI()
        {
            POS_X.Value = (decimal)transformVal.position.X;
            POS_Y.Value = (decimal)transformVal.position.Y;
            POS_Z.Value = (decimal)transformVal.position.Z;
            ROT_X.Value = (decimal)transformVal.rotation.X;
            ROT_Y.Value = (decimal)transformVal.rotation.Y;
            ROT_Z.Value = (decimal)transformVal.rotation.Z;
        }

        private void POS_X_ValueChanged(object sender, EventArgs e)
        {
            if (transformVal.position.X == (float)POS_X.Value)
                return;

            transformVal.position.X = (float)POS_X.Value;
            ValueChanged();
        }

        private void POS_Y_ValueChanged(object sender, EventArgs e)
        {
            if (transformVal.position.Y == (float)POS_Y.Value)
                return;

            transformVal.position.Y = (float)POS_Y.Value;
            ValueChanged();
        }

        private void POS_Z_ValueChanged(object sender, EventArgs e)
        {
            if (transformVal.position.Z == (float)POS_Z.Value)
                return;

            transformVal.position.Z = (float)POS_Z.Value;
            ValueChanged();
        }

        private void ROT_X_ValueChanged(object sender, EventArgs e)
        {
            if (transformVal.rotation.X == (float)ROT_X.Value)
                return;

            transformVal.rotation.X = (float)ROT_X.Value;
            ValueChanged();
        }

        private void ROT_Y_ValueChanged(object sender, EventArgs e)
        {
            if (transformVal.rotation.Y == (float)ROT_Y.Value)
                return;

            transformVal.rotation.Y = (float)ROT_Y.Value;
            ValueChanged();
        }

        private void ROT_Z_ValueChanged(object sender, EventArgs e)
        {
            if (transformVal.rotation.Z == (float)ROT_Z.Value)
                return;

            transformVal.rotation.Z = (float)ROT_Z.Value;
            ValueChanged();
        }

        private void ValueChanged()
        {
            OnValueChanged?.Invoke();
            HighlightAsModified();

            if (_entity != null)
                Singleton.OnEntityMoved?.Invoke(transformVal, _entity);
        }

        private void copyTransformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(JsonConvert.SerializeObject(transformVal));
        }

        private void pasteTransformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!POS_X.Enabled) return;

            string val = Clipboard.GetText()?.ToString();
            cTransform transform = null;
            try
            {
                transform = JsonConvert.DeserializeObject<cTransform>(val);
            }
            catch 
            {
                //This code SHOULD allow copying of Unity transforms, but the euler conversion isn't working right, so I've commented it out for now.
                /*
                try
                {
                    UnityTransform t = JsonConvert.DeserializeObject<UnityTransform>(val.Replace("UnityEditor.TransformWorldPlacementJSON:", ""));
                    (decimal yaw, decimal pitch, decimal roll) = new Quaternion(t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w).ToYawPitchRoll();
                    ROT_X.Value = pitch; ROT_Y.Value = yaw; ROT_Z.Value = roll;
                    transform = new cTransform(new Vector3(t.position.x, t.position.y, t.position.z), new Vector3((float)pitch, (float)yaw, (float)roll));
                }
                catch { }
                */
            }
            if (transform == null)
            {
                MessageBox.Show("Failed to paste transform.", "Invalid clipboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            transformVal.position.X = transform.position.X;
            transformVal.position.Y = transform.position.Y;
            transformVal.position.Z = transform.position.Z;
            transformVal.rotation.X = transform.rotation.X;
            transformVal.rotation.Y = transform.rotation.Y;
            transformVal.rotation.Z = transform.rotation.Z;

            UpdateUI();
            ValueChanged();
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, POSITION_VARIABLE_DUMMY);
        }

        [Serializable]
        private class UnityTransform
        {
            public Vector3Unity position = new Vector3Unity();
            public QuaternionUnity rotation = new QuaternionUnity();
            public Vector3Unity scale = new Vector3Unity();
            
            public class Vector3Unity
            {
                public float x = 0.0f;
                public float y = 0.0f;
                public float z = 0.0f;
            }
            public class QuaternionUnity
            {
                public float x = 0.0f;
                public float y = 0.0f;
                public float z = 0.0f;
                public float w = 0.0f;
            }
        }
    }
}
