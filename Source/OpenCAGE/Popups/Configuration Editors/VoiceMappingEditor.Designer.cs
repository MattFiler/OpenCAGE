namespace OpenCAGE.Popups.Configuration_Editors
{
    partial class VoiceMappingEditor
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
            this.mappingsLabel = new System.Windows.Forms.Label();
            this.mappingTree = new System.Windows.Forms.TreeView();
            this.addingToLabel = new System.Windows.Forms.Label();
            this.attributeGroup = new System.Windows.Forms.GroupBox();
            this.attributeKind = new System.Windows.Forms.ComboBox();
            this.attributeType = new System.Windows.Forms.ComboBox();
            this.addAttribute = new System.Windows.Forms.Button();
            this.voiceGroup = new System.Windows.Forms.GroupBox();
            this.voiceType = new System.Windows.Forms.ComboBox();
            this.addVoice = new System.Windows.Forms.Button();
            this.removeSelected = new System.Windows.Forms.Button();
            this.helpLabel = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.attributeGroup.SuspendLayout();
            this.voiceGroup.SuspendLayout();
            this.SuspendLayout();
            //
            // mappingsLabel
            //
            this.mappingsLabel.AutoSize = true;
            this.mappingsLabel.Location = new System.Drawing.Point(12, 9);
            this.mappingsLabel.Name = "mappingsLabel";
            this.mappingsLabel.Size = new System.Drawing.Size(147, 13);
            this.mappingsLabel.TabIndex = 0;
            this.mappingsLabel.Text = "Character attributes and voices:";
            //
            // mappingTree
            //
            this.mappingTree.HideSelection = false;
            this.mappingTree.Location = new System.Drawing.Point(15, 28);
            this.mappingTree.Name = "mappingTree";
            this.mappingTree.Size = new System.Drawing.Size(440, 380);
            this.mappingTree.TabIndex = 1;
            this.mappingTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.mappingTree_AfterSelect);
            //
            // addingToLabel
            //
            this.addingToLabel.AutoEllipsis = true;
            this.addingToLabel.Location = new System.Drawing.Point(470, 9);
            this.addingToLabel.Name = "addingToLabel";
            this.addingToLabel.Size = new System.Drawing.Size(313, 26);
            this.addingToLabel.TabIndex = 2;
            this.addingToLabel.Text = "Adding to:";
            //
            // attributeGroup
            //
            this.attributeGroup.Controls.Add(this.attributeKind);
            this.attributeGroup.Controls.Add(this.attributeType);
            this.attributeGroup.Controls.Add(this.addAttribute);
            this.attributeGroup.Location = new System.Drawing.Point(473, 40);
            this.attributeGroup.Name = "attributeGroup";
            this.attributeGroup.Size = new System.Drawing.Size(310, 100);
            this.attributeGroup.TabIndex = 3;
            this.attributeGroup.TabStop = false;
            this.attributeGroup.Text = "Narrow by another attribute";
            //
            // attributeKind
            //
            this.attributeKind.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.attributeKind.FormattingEnabled = true;
            this.attributeKind.Location = new System.Drawing.Point(12, 26);
            this.attributeKind.Name = "attributeKind";
            this.attributeKind.Size = new System.Drawing.Size(140, 21);
            this.attributeKind.TabIndex = 0;
            this.attributeKind.SelectedIndexChanged += new System.EventHandler(this.attributeKind_SelectedIndexChanged);
            //
            // attributeType
            //
            this.attributeType.FormattingEnabled = true;
            this.attributeType.Location = new System.Drawing.Point(158, 26);
            this.attributeType.Name = "attributeType";
            this.attributeType.Size = new System.Drawing.Size(140, 21);
            this.attributeType.TabIndex = 1;
            //
            // addAttribute
            //
            this.addAttribute.Location = new System.Drawing.Point(158, 57);
            this.addAttribute.Name = "addAttribute";
            this.addAttribute.Size = new System.Drawing.Size(140, 23);
            this.addAttribute.TabIndex = 2;
            this.addAttribute.Text = "Add Attribute";
            this.toolTip1.SetToolTip(this.addAttribute, "Adds a nested attribute set inside the selected one, for voices that only apply t" +
        "o a narrower kind of character.");
            this.addAttribute.UseVisualStyleBackColor = true;
            this.addAttribute.Click += new System.EventHandler(this.addAttribute_Click);
            //
            // voiceGroup
            //
            this.voiceGroup.Controls.Add(this.voiceType);
            this.voiceGroup.Controls.Add(this.addVoice);
            this.voiceGroup.Location = new System.Drawing.Point(473, 152);
            this.voiceGroup.Name = "voiceGroup";
            this.voiceGroup.Size = new System.Drawing.Size(310, 100);
            this.voiceGroup.TabIndex = 4;
            this.voiceGroup.TabStop = false;
            this.voiceGroup.Text = "Voice actor type";
            //
            // voiceType
            //
            this.voiceType.FormattingEnabled = true;
            this.voiceType.Location = new System.Drawing.Point(12, 26);
            this.voiceType.Name = "voiceType";
            this.voiceType.Size = new System.Drawing.Size(140, 21);
            this.voiceType.TabIndex = 0;
            this.toolTip1.SetToolTip(this.voiceType, "A voice actor type, e.g. CV1. The game picks at random between every type listed " +
        "in a set, and won\'t repeat one until it has used the others.");
            //
            // addVoice
            //
            this.addVoice.Location = new System.Drawing.Point(158, 25);
            this.addVoice.Name = "addVoice";
            this.addVoice.Size = new System.Drawing.Size(140, 23);
            this.addVoice.TabIndex = 1;
            this.addVoice.Text = "Add Voice";
            this.addVoice.UseVisualStyleBackColor = true;
            this.addVoice.Click += new System.EventHandler(this.addVoice_Click);
            //
            // removeSelected
            //
            this.removeSelected.Location = new System.Drawing.Point(631, 264);
            this.removeSelected.Name = "removeSelected";
            this.removeSelected.Size = new System.Drawing.Size(152, 23);
            this.removeSelected.TabIndex = 5;
            this.removeSelected.Text = "Remove Selected";
            this.removeSelected.UseVisualStyleBackColor = true;
            this.removeSelected.Click += new System.EventHandler(this.removeSelected_Click);
            //
            // helpLabel
            //
            this.helpLabel.Location = new System.Drawing.Point(473, 300);
            this.helpLabel.Name = "helpLabel";
            this.helpLabel.Size = new System.Drawing.Size(310, 108);
            this.helpLabel.TabIndex = 6;
            this.helpLabel.Text = "The game picks a voice from the deepest set of attributes that matches the charact" +
        "er being spawned. A set that only nests deeper sets can\'t answer for a character " +
        "whose deeper attribute is unknown, so give it voices of its own as well.";
            //
            // VoiceMappingEditor
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(798, 420);
            this.Controls.Add(this.helpLabel);
            this.Controls.Add(this.removeSelected);
            this.Controls.Add(this.voiceGroup);
            this.Controls.Add(this.attributeGroup);
            this.Controls.Add(this.addingToLabel);
            this.Controls.Add(this.mappingTree);
            this.Controls.Add(this.mappingsLabel);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "VoiceMappingEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Voice Mapping Editor";
            this.attributeGroup.ResumeLayout(false);
            this.voiceGroup.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label mappingsLabel;
        private System.Windows.Forms.TreeView mappingTree;
        private System.Windows.Forms.Label addingToLabel;
        private System.Windows.Forms.GroupBox attributeGroup;
        private System.Windows.Forms.ComboBox attributeKind;
        private System.Windows.Forms.ComboBox attributeType;
        private System.Windows.Forms.Button addAttribute;
        private System.Windows.Forms.GroupBox voiceGroup;
        private System.Windows.Forms.ComboBox voiceType;
        private System.Windows.Forms.Button addVoice;
        private System.Windows.Forms.Button removeSelected;
        private System.Windows.Forms.Label helpLabel;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
