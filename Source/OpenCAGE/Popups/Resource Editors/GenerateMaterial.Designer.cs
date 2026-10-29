namespace OpenCAGE
{
    partial class GenerateMaterial
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.familyLabel = new System.Windows.Forms.Label();
            this.familyList = new System.Windows.Forms.ComboBox();
            this.nameLabel = new System.Windows.Forms.Label();
            this.nameBox = new OpenCAGE.Popups.UserControls.AssetNameBox();
            this.textureGroup = new System.Windows.Forms.GroupBox();
            this.textureList = new System.Windows.Forms.ListView();
            this.colRole = new System.Windows.Forms.ColumnHeader();
            this.colSampler = new System.Windows.Forms.ColumnHeader();
            this.colFormat = new System.Windows.Forms.ColumnHeader();
            this.colSource = new System.Windows.Forms.ColumnHeader();
            this.featureGroup = new System.Windows.Forms.GroupBox();
            this.featureList = new System.Windows.Forms.ListBox();
            this.summaryLabel = new System.Windows.Forms.Label();
            this.alwaysNewBox = new System.Windows.Forms.CheckBox();
            this.createBtn = new System.Windows.Forms.Button();
            this.cancelBtn = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.textureGroup.SuspendLayout();
            this.featureGroup.SuspendLayout();
            this.SuspendLayout();
            //
            // familyLabel
            //
            this.familyLabel.AutoSize = true;
            this.familyLabel.Location = new System.Drawing.Point(12, 15);
            this.familyLabel.Name = "familyLabel";
            this.familyLabel.Size = new System.Drawing.Size(66, 13);
            this.familyLabel.TabIndex = 0;
            this.familyLabel.Text = "Shader type:";
            //
            // familyList
            //
            this.familyList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.familyList.FormattingEnabled = true;
            this.familyList.Location = new System.Drawing.Point(90, 12);
            this.familyList.Name = "familyList";
            this.familyList.Size = new System.Drawing.Size(280, 21);
            this.familyList.TabIndex = 1;
            this.toolTip1.SetToolTip(this.familyList, "Skinned meshes are suggested CA_CHARACTER, everything else CA_ENVIRONMENT.");
            this.familyList.SelectedIndexChanged += new System.EventHandler(this.familyList_SelectedIndexChanged);
            //
            // nameLabel
            //
            this.nameLabel.AutoSize = true;
            this.nameLabel.Location = new System.Drawing.Point(390, 15);
            this.nameLabel.Name = "nameLabel";
            this.nameLabel.Size = new System.Drawing.Size(38, 13);
            this.nameLabel.TabIndex = 2;
            this.nameLabel.Text = "Name:";
            //
            // nameBox
            //
            this.nameBox.Location = new System.Drawing.Point(434, 8);
            this.nameBox.Name = "nameBox";
            this.nameBox.Size = new System.Drawing.Size(354, 44);
            this.nameBox.TabIndex = 3;
            //
            // textureGroup
            //
            this.textureGroup.Controls.Add(this.textureList);
            this.textureGroup.Location = new System.Drawing.Point(12, 62);
            this.textureGroup.Name = "textureGroup";
            this.textureGroup.Size = new System.Drawing.Size(552, 250);
            this.textureGroup.TabIndex = 4;
            this.textureGroup.TabStop = false;
            this.textureGroup.Text = "Textures taken from the model";
            //
            // textureList
            //
            this.textureList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colRole,
            this.colSampler,
            this.colFormat,
            this.colSource});
            this.textureList.FullRowSelect = true;
            this.textureList.HideSelection = false;
            this.textureList.Location = new System.Drawing.Point(12, 22);
            this.textureList.MultiSelect = false;
            this.textureList.Name = "textureList";
            this.textureList.Size = new System.Drawing.Size(528, 216);
            this.textureList.TabIndex = 0;
            this.textureList.UseCompatibleStateImageBehavior = false;
            this.textureList.View = System.Windows.Forms.View.Details;
            //
            // colRole
            //
            this.colRole.Text = "Map";
            this.colRole.Width = 120;
            //
            // colSampler
            //
            this.colSampler.Text = "Sampler";
            this.colSampler.Width = 150;
            //
            // colFormat
            //
            this.colFormat.Text = "Imported as";
            this.colFormat.Width = 110;
            //
            // colSource
            //
            this.colSource.Text = "From";
            this.colSource.Width = 400;
            //
            // featureGroup
            //
            this.featureGroup.Controls.Add(this.featureList);
            this.featureGroup.Location = new System.Drawing.Point(576, 62);
            this.featureGroup.Name = "featureGroup";
            this.featureGroup.Size = new System.Drawing.Size(212, 250);
            this.featureGroup.TabIndex = 5;
            this.featureGroup.TabStop = false;
            this.featureGroup.Text = "Shader features";
            //
            // featureList
            //
            this.featureList.FormattingEnabled = true;
            this.featureList.IntegralHeight = false;
            this.featureList.Location = new System.Drawing.Point(12, 22);
            this.featureList.Name = "featureList";
            this.featureList.Size = new System.Drawing.Size(188, 216);
            this.featureList.TabIndex = 0;
            this.toolTip1.SetToolTip(this.featureList, "A feature is turned on exactly when its texture is present - that is the rule ever" +
        "y shipped material follows.");
            //
            // summaryLabel
            //
            this.summaryLabel.Location = new System.Drawing.Point(12, 318);
            this.summaryLabel.Name = "summaryLabel";
            this.summaryLabel.Size = new System.Drawing.Size(600, 56);
            this.summaryLabel.TabIndex = 6;
            //
            // alwaysNewBox
            //
            this.alwaysNewBox.AutoSize = true;
            this.alwaysNewBox.Location = new System.Drawing.Point(14, 378);
            this.alwaysNewBox.Name = "alwaysNewBox";
            this.alwaysNewBox.Size = new System.Drawing.Size(200, 17);
            this.alwaysNewBox.TabIndex = 9;
            this.alwaysNewBox.Text = "Always create a new material";
            this.alwaysNewBox.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.alwaysNewBox, "Build a material of this model\'s own even when the level already holds an identica" +
        "l one, so the two can be tuned apart later.");
            this.alwaysNewBox.CheckedChanged += new System.EventHandler(this.alwaysNewBox_CheckedChanged);
            //
            // createBtn
            //
            this.createBtn.Location = new System.Drawing.Point(628, 368);
            this.createBtn.Name = "createBtn";
            this.createBtn.Size = new System.Drawing.Size(160, 30);
            this.createBtn.TabIndex = 7;
            this.createBtn.Text = "Create Material";
            this.createBtn.UseVisualStyleBackColor = true;
            this.createBtn.Click += new System.EventHandler(this.createBtn_Click);
            //
            // cancelBtn
            //
            this.cancelBtn.Location = new System.Drawing.Point(628, 336);
            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Size = new System.Drawing.Size(160, 26);
            this.cancelBtn.TabIndex = 8;
            this.cancelBtn.Text = "Cancel";
            this.cancelBtn.UseVisualStyleBackColor = true;
            this.cancelBtn.Click += new System.EventHandler(this.cancelBtn_Click);
            //
            // GenerateMaterial
            //
            this.AcceptButton = this.createBtn;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelBtn;
            this.ClientSize = new System.Drawing.Size(800, 410);
            this.Controls.Add(this.cancelBtn);
            this.Controls.Add(this.createBtn);
            this.Controls.Add(this.alwaysNewBox);
            this.Controls.Add(this.summaryLabel);
            this.Controls.Add(this.featureGroup);
            this.Controls.Add(this.textureGroup);
            this.Controls.Add(this.nameBox);
            this.Controls.Add(this.nameLabel);
            this.Controls.Add(this.familyList);
            this.Controls.Add(this.familyLabel);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GenerateMaterial";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Generate Material";
            this.textureGroup.ResumeLayout(false);
            this.featureGroup.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label familyLabel;
        private System.Windows.Forms.ComboBox familyList;
        private System.Windows.Forms.Label nameLabel;
        private OpenCAGE.Popups.UserControls.AssetNameBox nameBox;
        private System.Windows.Forms.GroupBox textureGroup;
        private System.Windows.Forms.ListView textureList;
        private System.Windows.Forms.ColumnHeader colRole;
        private System.Windows.Forms.ColumnHeader colSampler;
        private System.Windows.Forms.ColumnHeader colFormat;
        private System.Windows.Forms.ColumnHeader colSource;
        private System.Windows.Forms.GroupBox featureGroup;
        private System.Windows.Forms.ListBox featureList;
        private System.Windows.Forms.Label summaryLabel;
        private System.Windows.Forms.CheckBox alwaysNewBox;
        private System.Windows.Forms.Button createBtn;
        private System.Windows.Forms.Button cancelBtn;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
