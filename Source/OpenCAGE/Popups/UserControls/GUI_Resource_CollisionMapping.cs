using CATHODE;
using CATHODE.Scripting;
using OpenCAGE;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static CATHODE.CollisionMaps;

namespace OpenCAGE.Popups.UserControls
{
    public partial class GUI_Resource_CollisionMapping : ResourceUserControl
    {
        public Vector3 Position { get { return new Vector3((float)POS_X.Value, (float)POS_Y.Value, (float)POS_Z.Value); } }
        public Vector3 Rotation { get { return new Vector3((float)ROT_X.Value, (float)ROT_Y.Value, (float)ROT_Z.Value); } }

        private ResourceReference _resourceRef;
        private COLLISION_MAPPING _currentCollisionMapping;

        private Dictionary<CheckBox, CollisionFlags> _flagCheckboxes = new Dictionary<CheckBox, CollisionFlags>();
        private Dictionary<CheckBox, CollisionFlags> _flagMasks = new Dictionary<CheckBox, CollisionFlags>();

        public GUI_Resource_CollisionMapping() : base()
        {
            InitializeComponent();

            POS_X.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            POS_Y.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            POS_Z.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            ROT_X.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStepRot);
            ROT_Y.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStepRot);
            ROT_Z.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStepRot);

            InitializeFlagCheckboxes();

            numericIndex.ValueChanged += NumericIndex_ValueChanged;
            numericCollisionProxyIndex.ValueChanged += NumericCollisionProxyIndex_ValueChanged;
        }

        private void InitializeFlagCheckboxes()
        {
            AddFlagCheckbox(groupBoxType, CollisionFlags.STANDARD, "Standard", CollisionFlags.COLLISION_TYPE_MASK);
            AddFlagCheckbox(groupBoxType, CollisionFlags.PHANTOM, "Phantom", CollisionFlags.COLLISION_TYPE_MASK);
            AddFlagCheckbox(groupBoxType, CollisionFlags.DYNAMIC, "Dynamic", CollisionFlags.COLLISION_TYPE_MASK);
            AddFlagCheckbox(groupBoxType, CollisionFlags.PATHFINDING, "Pathfinding", CollisionFlags.COLLISION_TYPE_MASK);
            AddFlagCheckbox(groupBoxType, CollisionFlags.CAMERA, "Camera", CollisionFlags.COLLISION_TYPE_MASK);
            AddFlagCheckbox(groupBoxType, CollisionFlags.SOUND, "Sound", CollisionFlags.COLLISION_TYPE_MASK);
            AddFlagCheckbox(groupBoxType, CollisionFlags.USER_INTERFACE, "User Interface", CollisionFlags.COLLISION_TYPE_MASK);
            AddFlagCheckbox(groupBoxType, CollisionFlags.PLAYER, "Player", CollisionFlags.COLLISION_TYPE_MASK);

            AddFlagCheckbox(groupBoxStorage, CollisionFlags.LANDSCAPE, "Landscape", CollisionFlags.STORAGE_TYPE_MASK);
            AddFlagCheckbox(groupBoxStorage, CollisionFlags.WORLD, "World", CollisionFlags.STORAGE_TYPE_MASK);
            AddFlagCheckbox(groupBoxStorage, CollisionFlags.BALLISTIC, "Ballistic", CollisionFlags.STORAGE_TYPE_MASK);

            AddFlagCheckbox(groupBoxMotion, CollisionFlags.FIXED, "Fixed", CollisionFlags.MOTION_TYPE_MASK);
            AddFlagCheckbox(groupBoxMotion, CollisionFlags.KEYFRAMED, "Keyframed", CollisionFlags.MOTION_TYPE_MASK);
            AddFlagCheckbox(groupBoxMotion, CollisionFlags.SIMULATING, "Simulating", CollisionFlags.MOTION_TYPE_MASK);

            AddFlagCheckbox(groupBoxSource, CollisionFlags.PREBUILT, "Prebuilt", CollisionFlags.SOURCE_TYPE_MASK);
            AddFlagCheckbox(groupBoxSource, CollisionFlags.RESOURCE, "Resource", CollisionFlags.SOURCE_TYPE_MASK);
            AddFlagCheckbox(groupBoxSource, CollisionFlags.SYSTEM, "System", CollisionFlags.SOURCE_TYPE_MASK);
            AddFlagCheckbox(groupBoxSource, CollisionFlags.SCRIPT, "Script", CollisionFlags.SOURCE_TYPE_MASK);

            AddFlagCheckbox(groupBoxState, CollisionFlags.GHOSTED, "Ghosted", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.PRE_GHOSTED, "Pre-Ghosted", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.FROZEN, "Frozen", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.PRE_FROZEN, "Pre-Frozen", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.REMOVED, "Removed", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.FORCE_KEYFRAMED, "Force Keyframed", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.BALLISTIC_ONLY, "Ballistic Only", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.STANDARD_ONLY, "Standard Only", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.PRE_ZERO_GRAVITY, "Pre-Zero Gravity", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.SOFT_COLLISION, "Soft Collision", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.REPORT_SLIDING, "Report Sliding", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.IS_SUBMERGED, "Is Submerged", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.ZERO_GRAVITY, "Zero Gravity", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.REPORTING, "Reporting", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.FORCE_TRANSPARENT, "Force Transparent", CollisionFlags.STATE_MASK);
            AddFlagCheckbox(groupBoxState, CollisionFlags.HIGH_PRIORITY, "High Priority", CollisionFlags.STATE_MASK);
        }

        private void AddFlagCheckbox(GroupBox groupBox, CollisionFlags flag, string text, CollisionFlags mask)
        {
            CheckBox checkbox = new CheckBox
            {
                Text = text,
                AutoSize = true,
                Location = new Point(6, 19 + (groupBox.Controls.Count * 20)),
                Enabled = false
            };
            groupBox.Controls.Add(checkbox);
            _flagCheckboxes[checkbox] = flag;
            _flagMasks[checkbox] = mask;
        }

        private void NumericIndex_ValueChanged(object sender, EventArgs e)
        {
            if (_currentCollisionMapping == null)
                return;

            _currentCollisionMapping.Index = (int)numericIndex.Value;
        }

        private void NumericCollisionProxyIndex_ValueChanged(object sender, EventArgs e)
        {
            if (_currentCollisionMapping == null)
                return;

            _currentCollisionMapping.CollisionProxyIndex = (int)numericCollisionProxyIndex.Value;
        }

        public override void PopulateUI(ResourceReference resource)
        {
            _currentCollisionMapping = resource.CollisionMapping;
            _resourceRef = resource;

            //TODO: can we deprecate these?
            POS_X.Value = (decimal)resource.position.X;
            POS_Y.Value = (decimal)resource.position.Y;
            POS_Z.Value = (decimal)resource.position.Z;
            ROT_X.Value = (decimal)resource.rotation.X;
            ROT_Y.Value = (decimal)resource.rotation.Y;
            ROT_Z.Value = (decimal)resource.rotation.Z;

            CollisionFlags flags = _currentCollisionMapping.Flags;
            foreach (var kvp in _flagCheckboxes)
            {
                CheckBox checkbox = kvp.Key;
                CollisionFlags flag = kvp.Value;
                CollisionFlags mask = _flagMasks[checkbox];

                uint maskedFlags = (uint)(flags & mask);
                uint maskedFlag = (uint)(flag & mask);

                if (mask == CollisionFlags.COLLISION_TYPE_MASK)
                {
                    checkbox.Checked = (maskedFlags == maskedFlag) && maskedFlag != 0;
                }
                else
                {
                    checkbox.Checked = (maskedFlags & maskedFlag) != 0;
                }
            }

            numericIndex.Value = _currentCollisionMapping.Index;
            numericCollisionProxyIndex.Value = _currentCollisionMapping.CollisionProxyIndex;

            materialName.Text = _currentCollisionMapping?.Material?.Name;
            materialMappingName.Text = _currentCollisionMapping?.MaterialMapping?.Name;
        }

        private void btnSetMaterial_Click(object sender, EventArgs e)
        {
            EditMaterial editMaterial = new EditMaterial(_currentCollisionMapping.Material);
            editMaterial.OnMaterialSelected += SetMaterial;
            editMaterial.Show();
        }

        private void btnSetMaterialMapping_Click(object sender, EventArgs e)
        {
            EditMaterialMapping editMaterialMapping = new EditMaterialMapping(_currentCollisionMapping.MaterialMapping);
            editMaterialMapping.OnMaterialMappingSelected += SetMaterialMapping;
            editMaterialMapping.Show();
        }

        private void btnClearMaterialMapping_Click(object sender, EventArgs e)
        {
            SetMaterialMapping(null);
        }

        private void SetMaterial(Materials.Material material)
        {
            _currentCollisionMapping.Material = material;
            materialName.Text = _currentCollisionMapping?.Material?.Name;
        }
        private void SetMaterialMapping(MaterialMappings.MaterialMapping mapping)
        {
            _currentCollisionMapping.MaterialMapping = null;
            materialMappingName.Text = _currentCollisionMapping?.MaterialMapping?.Name;
        }

        private void POS_X_ValueChanged(object sender, EventArgs e)
        {
            _resourceRef.position.X = (float)POS_X.Value;
            Singleton.OnResourceModified?.Invoke();
        }
        private void POS_Y_ValueChanged(object sender, EventArgs e)
        {
            _resourceRef.position.Y = (float)POS_Y.Value;
            Singleton.OnResourceModified?.Invoke();
        }
        private void POS_Z_ValueChanged(object sender, EventArgs e)
        {
            _resourceRef.position.Z = (float)POS_Z.Value;
            Singleton.OnResourceModified?.Invoke();
        }
        private void ROT_X_ValueChanged(object sender, EventArgs e)
        {
            _resourceRef.rotation.X = (float)ROT_X.Value;
            Singleton.OnResourceModified?.Invoke();
        }
        private void ROT_Y_ValueChanged(object sender, EventArgs e)
        {
            _resourceRef.rotation.Y = (float)ROT_Y.Value;
            Singleton.OnResourceModified?.Invoke();
        }
        private void ROT_Z_ValueChanged(object sender, EventArgs e)
        {
            _resourceRef.rotation.Z = (float)ROT_Z.Value;
            Singleton.OnResourceModified?.Invoke();
        }
    }
}
