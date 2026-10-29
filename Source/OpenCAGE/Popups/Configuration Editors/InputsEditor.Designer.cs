namespace OpenCAGE.ConfigEditors
{
    partial class InputsEditor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.deviceLabel = new System.Windows.Forms.Label();
            this.deviceList = new System.Windows.Forms.ListBox();
            this.settingsGroup = new System.Windows.Forms.GroupBox();
            this.settingsGrid = new System.Windows.Forms.DataGridView();
            this.columnSetting = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnSettingValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bindingsLabel = new System.Windows.Forms.Label();
            this.bindingGrid = new System.Windows.Forms.DataGridView();
            this.columnKind = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnAction = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnBoundTo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnCombo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnToggle = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.duplicateBinding = new System.Windows.Forms.Button();
            this.removeBinding = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.settingsGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.settingsGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingGrid)).BeginInit();
            this.SuspendLayout();
            //
            // deviceLabel
            //
            this.deviceLabel.AutoSize = true;
            this.deviceLabel.Location = new System.Drawing.Point(12, 9);
            this.deviceLabel.Name = "deviceLabel";
            this.deviceLabel.Size = new System.Drawing.Size(96, 13);
            this.deviceLabel.TabIndex = 0;
            this.deviceLabel.Text = "Section and device:";
            //
            // deviceList
            //
            this.deviceList.FormattingEnabled = true;
            this.deviceList.IntegralHeight = false;
            this.deviceList.Location = new System.Drawing.Point(15, 28);
            this.deviceList.Name = "deviceList";
            this.deviceList.Size = new System.Drawing.Size(290, 280);
            this.deviceList.TabIndex = 1;
            this.toolTip1.SetToolTip(this.deviceList, "Each gamepad preset is a controller layout the game\'s options can switch to; pres" +
        "et 0 (no preset) is the default one.");
            this.deviceList.SelectedIndexChanged += new System.EventHandler(this.deviceList_SelectedIndexChanged);
            //
            // settingsGroup
            //
            this.settingsGroup.Controls.Add(this.settingsGrid);
            this.settingsGroup.Location = new System.Drawing.Point(15, 320);
            this.settingsGroup.Name = "settingsGroup";
            this.settingsGroup.Size = new System.Drawing.Size(290, 208);
            this.settingsGroup.TabIndex = 2;
            this.settingsGroup.TabStop = false;
            this.settingsGroup.Text = "Device settings";
            //
            // settingsGrid
            //
            this.settingsGrid.AllowUserToAddRows = false;
            this.settingsGrid.AllowUserToDeleteRows = false;
            this.settingsGrid.AllowUserToResizeRows = false;
            this.settingsGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.settingsGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnSetting,
            this.columnSettingValue});
            this.settingsGrid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnKeystrokeOrF2;
            this.settingsGrid.Location = new System.Drawing.Point(12, 22);
            this.settingsGrid.MultiSelect = false;
            this.settingsGrid.Name = "settingsGrid";
            this.settingsGrid.RowHeadersVisible = false;
            this.settingsGrid.Size = new System.Drawing.Size(266, 174);
            this.settingsGrid.TabIndex = 0;
            this.settingsGrid.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.settingsGrid_CellValueChanged);
            this.settingsGrid.CurrentCellDirtyStateChanged += new System.EventHandler(this.settingsGrid_CurrentCellDirtyStateChanged);
            //
            // columnSetting
            //
            this.columnSetting.HeaderText = "Setting";
            this.columnSetting.Name = "columnSetting";
            this.columnSetting.ReadOnly = true;
            this.columnSetting.Width = 160;
            //
            // columnSettingValue
            //
            this.columnSettingValue.HeaderText = "Value";
            this.columnSettingValue.Name = "columnSettingValue";
            this.columnSettingValue.Width = 80;
            //
            // bindingsLabel
            //
            this.bindingsLabel.AutoSize = true;
            this.bindingsLabel.Location = new System.Drawing.Point(320, 9);
            this.bindingsLabel.Name = "bindingsLabel";
            this.bindingsLabel.Size = new System.Drawing.Size(279, 13);
            this.bindingsLabel.TabIndex = 3;
            this.bindingsLabel.Text = "Bindings (an action can be bound more than once):";
            //
            // bindingGrid
            //
            this.bindingGrid.AllowUserToAddRows = false;
            this.bindingGrid.AllowUserToDeleteRows = false;
            this.bindingGrid.AllowUserToResizeRows = false;
            this.bindingGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.bindingGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnKind,
            this.columnAction,
            this.columnBoundTo,
            this.columnCombo,
            this.columnToggle});
            this.bindingGrid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnKeystrokeOrF2;
            this.bindingGrid.Location = new System.Drawing.Point(323, 28);
            this.bindingGrid.MultiSelect = false;
            this.bindingGrid.Name = "bindingGrid";
            this.bindingGrid.RowHeadersVisible = false;
            this.bindingGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.bindingGrid.Size = new System.Drawing.Size(717, 471);
            this.bindingGrid.TabIndex = 4;
            this.bindingGrid.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.bindingGrid_CellValueChanged);
            this.bindingGrid.CurrentCellDirtyStateChanged += new System.EventHandler(this.bindingGrid_CurrentCellDirtyStateChanged);
            this.bindingGrid.SelectionChanged += new System.EventHandler(this.bindingGrid_SelectionChanged);
            //
            // columnKind
            //
            this.columnKind.HeaderText = "Kind";
            this.columnKind.Name = "columnKind";
            this.columnKind.ReadOnly = true;
            this.columnKind.ToolTipText = "button, axis or slider - fixed by the engine for each action.";
            this.columnKind.Width = 70;
            //
            // columnAction
            //
            this.columnAction.HeaderText = "Action";
            this.columnAction.Name = "columnAction";
            this.columnAction.ReadOnly = true;
            this.columnAction.ToolTipText = "The action the engine asks for by name. Renaming one would make the engine stop f" +
        "inding it, so these are fixed.";
            this.columnAction.Width = 220;
            //
            // columnBoundTo
            //
            this.columnBoundTo.HeaderText = "Bound to";
            this.columnBoundTo.Name = "columnBoundTo";
            this.columnBoundTo.ToolTipText = "The input id, e.g. w, lshift, mbutton_left, lt_button, lstick_x. Leave it empty t" +
        "o ship the action unbound.";
            this.columnBoundTo.Width = 200;
            //
            // columnCombo
            //
            this.columnCombo.HeaderText = "Combo";
            this.columnCombo.Name = "columnCombo";
            this.columnCombo.ToolTipText = "Two ids pressed together, e.g. O+P. Used instead of Bound to.";
            this.columnCombo.Width = 110;
            //
            // columnToggle
            //
            this.columnToggle.HeaderText = "Toggle";
            this.columnToggle.Name = "columnToggle";
            this.columnToggle.ToolTipText = "true to make the action toggle on and off instead of being held. Leave empty for " +
        "hold.";
            this.columnToggle.Width = 80;
            //
            // duplicateBinding
            //
            this.duplicateBinding.Location = new System.Drawing.Point(323, 505);
            this.duplicateBinding.Name = "duplicateBinding";
            this.duplicateBinding.Size = new System.Drawing.Size(160, 23);
            this.duplicateBinding.TabIndex = 5;
            this.duplicateBinding.Text = "Bind Action Again";
            this.toolTip1.SetToolTip(this.duplicateBinding, "Adds a second binding for the selected action, so it answers to more than one inp" +
        "ut.");
            this.duplicateBinding.UseVisualStyleBackColor = true;
            this.duplicateBinding.Click += new System.EventHandler(this.duplicateBinding_Click);
            //
            // removeBinding
            //
            this.removeBinding.Location = new System.Drawing.Point(489, 505);
            this.removeBinding.Name = "removeBinding";
            this.removeBinding.Size = new System.Drawing.Size(160, 23);
            this.removeBinding.TabIndex = 6;
            this.removeBinding.Text = "Remove Binding";
            this.removeBinding.UseVisualStyleBackColor = true;
            this.removeBinding.Click += new System.EventHandler(this.removeBinding_Click);
            //
            // InputsEditor
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1054, 541);
            this.Controls.Add(this.removeBinding);
            this.Controls.Add(this.duplicateBinding);
            this.Controls.Add(this.bindingGrid);
            this.Controls.Add(this.bindingsLabel);
            this.Controls.Add(this.settingsGroup);
            this.Controls.Add(this.deviceList);
            this.Controls.Add(this.deviceLabel);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "InputsEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Inputs Editor";
            this.settingsGroup.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.settingsGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingGrid)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label deviceLabel;
        private System.Windows.Forms.ListBox deviceList;
        private System.Windows.Forms.GroupBox settingsGroup;
        private System.Windows.Forms.DataGridView settingsGrid;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnSetting;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnSettingValue;
        private System.Windows.Forms.Label bindingsLabel;
        private System.Windows.Forms.DataGridView bindingGrid;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnKind;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnAction;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnBoundTo;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnCombo;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnToggle;
        private System.Windows.Forms.Button duplicateBinding;
        private System.Windows.Forms.Button removeBinding;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
