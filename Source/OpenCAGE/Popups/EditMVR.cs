using CATHODE.Scripting;
using CommandsEditor.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Numerics;
using CATHODE;
using CommandsEditor.Popups.Base;
using CommandsEditor.DockPanels;
using CathodeLib;
using OpenCAGE;
using System.Windows;
using static CATHODE.Movers;

namespace CommandsEditor
{
    public partial class EditMVR : BaseWindow
    {
        private int loadedMvrIndex = -1;
        private ShortGuid filteredNodeID;

        List<int> _mvrListIndexes = new List<int>();

        private EntityInspector _entityDisplay;

        public EditMVR(EntityInspector editor) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            _entityDisplay = editor;
            
            PopulateUI(editor.Entity.shortGUID);

            POS_X.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            POS_Y.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            POS_Z.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            ROT_X.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStepRot);
            ROT_Y.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStepRot);
            ROT_Z.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStepRot);
            SCALE_X.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            SCALE_Y.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            SCALE_Z.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
        }

        private void PopulateUI(ShortGuid nodeID)
        {
            filteredNodeID = nodeID;

            //Get all MVR entries that match this entity
            _mvrListIndexes.Clear();
            for (int i = 0; i < Content.Level.Movers.Entries.Count; i++)
            {
                if (Content.Level.Movers.Entries[i].Entity.entity_id != nodeID) continue;
                _mvrListIndexes.Add(i);
            }

            //Fetch the hierarchies for the MVR entries that point to this entity
            EntityPath[] hierarchies = new EntityPath[_mvrListIndexes.Count];
            Parallel.For(0, _mvrListIndexes.Count, i =>
            {
                hierarchies[i] = _entityDisplay.Content.EditorUtils.GetHierarchyFromHandle(Content.Level.Movers.Entries[_mvrListIndexes[i]].Entity);
            });

            //Write the hierarchies to the list
            listBox1.BeginUpdate();
            listBox1.Items.Clear();
            for (int i = 0; i < hierarchies.Length; i++)
            {
                listBox1.Items.Add(hierarchies[i] == null ? _mvrListIndexes[i].ToString() + " [unresolvable]" : Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveHierarchy(hierarchies[i]), SettingsManager.GetBool(Singleton.Settings.ShowShortGuids)));
            }
            listBox1.EndUpdate();
            if (listBox1.Items.Count != 0) listBox1.SelectedIndex = 0;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            loadedMvrIndex = -1;
            if (listBox1.SelectedIndex == -1) return;
            LoadMVR(_mvrListIndexes[listBox1.SelectedIndex]);
        }

        private bool hasLoaded = false;
        private void LoadMVR(int mvrIndex)
        {
            hasLoaded = false;
            loadedMvrIndex = mvrIndex;
            Movers.MOVER_DESCRIPTOR mvr = Content.Level.Movers.Entries[loadedMvrIndex];
            renderable.PopulateUI(mvr.RenderableElements);

            Matrix4x4.Decompose(mvr.Transform, out Vector3 scale, out Quaternion rotation, out Vector3 position);

            POS_X.Value = (decimal)position.X; POS_Y.Value = (decimal)position.Y; POS_Z.Value = (decimal)position.Z;
            SCALE_X.Value = (decimal)scale.X; SCALE_Y.Value = (decimal)scale.Y; SCALE_Z.Value = (decimal)scale.Z;

            (decimal yaw, decimal pitch, decimal roll) = rotation.ToYawPitchRoll();
            ROT_X.Value = pitch; ROT_Y.Value = yaw; ROT_Z.Value = roll;

            //NOTE: Obviously, all these values actually come from the scripting (e.g. disable_size_culling is the cull flag).
            //      Really we should write all this data automatically, and update the entities appropriately.
            //      I need to write a sanity checker that re-populates the Commands data with the things that got stripped, but are in MVR.

            Debug.Log("MVR", "Emissive Flags: " + mvr.EmissiveFlags.ToString());

            emReplaceTint.Checked = mvr.EmissiveFlags.HasFlag(EmissiveFlag.ReplaceTint);
            emReplaceIntensity.Checked = mvr.EmissiveFlags.HasFlag(EmissiveFlag.ReplaceIntensity);
            emMasterOff.Checked = mvr.EmissiveFlags.HasFlag(EmissiveFlag.MasterOff);

            Debug.Log("MVR", "Cull Flags: " + mvr.CullFlags.ToString());

            cfDontShadows.Checked = mvr.CullFlags.HasFlag(CullFlag.NO_CAST_SHADOWS);
            cfDontRender.Checked = mvr.CullFlags.HasFlag(CullFlag.NO_RENDER);
            cfReflections.Checked = mvr.CullFlags.HasFlag(CullFlag.INCLUDE_IN_REFLECTIVE);
            cfNoSize.Checked = mvr.CullFlags.HasFlag(CullFlag.NO_SIZE_CULLING); 
            cfNoTorch.Checked = mvr.CullFlags.HasFlag(CullFlag.NO_CAST_TORCH_SHADOW);
            cfAlwaysPass.Checked = mvr.CullFlags.HasFlag(CullFlag.ALWAYS_PASS);

            requiresScript.Checked = mvr.Flags.RequiresScript;
            isVisible.Checked = mvr.Flags.Visible;
            isStationary.Checked = mvr.Flags.Stationary;

            emTint.BackColor = Color.FromArgb((int)mvr.EmissiveTint.X, (int)mvr.EmissiveTint.Y, (int)mvr.EmissiveTint.Z);
            emIntensityMultiplier.Value = (decimal)mvr.EmissiveIntensityMultiplier;
            emRadiosityMultiplier.Value = (decimal)mvr.EmissiveRadiosityMultiplier;

            hasLoaded = true;
        }

        private void doSave_Click(object sender, EventArgs e)
        {
            if (!hasLoaded || loadedMvrIndex == -1) return;
            Movers.MOVER_DESCRIPTOR mvr = Content.Level.Movers.Entries[loadedMvrIndex];
            mvr.RenderableElements = renderable.GetAsRenderableElements();

            Vector3 scale = new Vector3((float)SCALE_X.Value, (float)SCALE_Y.Value, (float)SCALE_Z.Value);
            Vector3 position = new Vector3((float)POS_X.Value, (float)POS_Y.Value, (float)POS_Z.Value);

            Quaternion rotation = Quaternion.CreateFromYawPitchRoll(
                (float)Convert.ToDouble(ROT_Y.Value) * (float)Math.PI / 180.0f, 
                (float)Convert.ToDouble(ROT_X.Value) * (float)Math.PI / 180.0f, 
                (float)Convert.ToDouble(ROT_Z.Value) * (float)Math.PI / 180.0f);

            mvr.Transform = Matrix4x4.CreateScale(scale) *
                            Matrix4x4.CreateFromQuaternion(rotation) *
                            Matrix4x4.CreateTranslation(position);

            mvr.EmissiveFlags = EmissiveFlag.None;
            if (emReplaceTint.Checked) mvr.EmissiveFlags |= EmissiveFlag.ReplaceTint;
            if (emReplaceIntensity.Checked) mvr.EmissiveFlags |= EmissiveFlag.ReplaceIntensity;
            if (emMasterOff.Checked) mvr.EmissiveFlags |= EmissiveFlag.MasterOff;

            mvr.CullFlags = CullFlag.DEFAULT;
            if (cfDontShadows.Checked) mvr.CullFlags |= CullFlag.NO_CAST_SHADOWS;
            if (cfDontRender.Checked) mvr.CullFlags |= CullFlag.NO_RENDER;
            if (cfReflections.Checked) mvr.CullFlags |= CullFlag.INCLUDE_IN_REFLECTIVE;
            if (cfNoSize.Checked) mvr.CullFlags |= CullFlag.NO_SIZE_CULLING;
            if (cfNoTorch.Checked) mvr.CullFlags |= CullFlag.NO_CAST_TORCH_SHADOW;
            if (cfAlwaysPass.Checked) mvr.CullFlags |= CullFlag.ALWAYS_PASS;

            mvr.Flags.RequiresScript = requiresScript.Checked;
            mvr.Flags.Visible = isVisible.Checked;
            mvr.Flags.Stationary = isStationary.Checked;

            mvr.EmissiveTint = new Vector3(emTint.BackColor.R, emTint.BackColor.G, emTint.BackColor.B);
            mvr.EmissiveIntensityMultiplier = (float)emIntensityMultiplier.Value;
            mvr.EmissiveRadiosityMultiplier = (float)emRadiosityMultiplier.Value;

            Debug.Log("MVR", "SAVED");
            //MessageBox.Show("Saved changes for mover " + loadedMvrIndex + "!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void deleteMover_Click(object sender, EventArgs e)
        {
            if (loadedMvrIndex == -1) return;
            //CurrentInstance.moverDB.Movers.RemoveAt(loadedMvrIndex);

            Content.Level.Movers.Entries[loadedMvrIndex] = Content.Level.Movers.Entries[0];

            PopulateUI(filteredNodeID);
        }

        private void openColourPicker_Click(object sender, EventArgs e)
        {
            ColorDialog colourPicker = new ColorDialog();
            colourPicker.Color = emTint.BackColor;
            if (colourPicker.ShowDialog() == DialogResult.OK)
                emTint.BackColor = colourPicker.Color;
        }
    }
}
