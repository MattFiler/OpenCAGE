namespace OpenCAGE.ConfigEditors
{
    partial class BlueprintEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BlueprintEditor));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.blueprintInput = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.removeInputItem = new System.Windows.Forms.Button();
            this.addNewItemRequired = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.blueprints = new System.Windows.Forms.ComboBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.blueprintOutput = new System.Windows.Forms.ListView();
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.removeOutputItem = new System.Windows.Forms.Button();
            this.addNewOutputItem = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.helpBtn = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.blueprintInput);
            this.groupBox1.Controls.Add(this.removeInputItem);
            this.groupBox1.Controls.Add(this.addNewItemRequired);
            this.groupBox1.Location = new System.Drawing.Point(12, 39);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(241, 234);
            this.groupBox1.TabIndex = 328;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Blueprint Required Items To Craft";
            // 
            // blueprintInput
            // 
            this.blueprintInput.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
            this.blueprintInput.FullRowSelect = true;
            this.blueprintInput.HideSelection = false;
            this.blueprintInput.Location = new System.Drawing.Point(8, 21);
            this.blueprintInput.MultiSelect = false;
            this.blueprintInput.Name = "blueprintInput";
            this.blueprintInput.Size = new System.Drawing.Size(224, 175);
            this.blueprintInput.TabIndex = 338;
            this.blueprintInput.UseCompatibleStateImageBehavior = false;
            this.blueprintInput.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Item Name";
            this.columnHeader1.Width = 145;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Quantity";
            // 
            // removeInputItem
            // 
            this.removeInputItem.Location = new System.Drawing.Point(8, 203);
            this.removeInputItem.Name = "removeInputItem";
            this.removeInputItem.Size = new System.Drawing.Size(110, 25);
            this.removeInputItem.TabIndex = 337;
            this.removeInputItem.Text = "Remove Selected";
            this.removeInputItem.UseVisualStyleBackColor = true;
            this.removeInputItem.Click += new System.EventHandler(this.removeInputItem_Click);
            // 
            // addNewItemRequired
            // 
            this.addNewItemRequired.Location = new System.Drawing.Point(124, 203);
            this.addNewItemRequired.Name = "addNewItemRequired";
            this.addNewItemRequired.Size = new System.Drawing.Size(109, 25);
            this.addNewItemRequired.TabIndex = 336;
            this.addNewItemRequired.Text = "Add New";
            this.addNewItemRequired.UseVisualStyleBackColor = true;
            this.addNewItemRequired.Click += new System.EventHandler(this.addNewItemRequired_Click);
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(364, 279);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(136, 33);
            this.btnSave.TabIndex = 326;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // blueprints
            // 
            this.blueprints.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.blueprints.FormattingEnabled = true;
            this.blueprints.Location = new System.Drawing.Point(12, 12);
            this.blueprints.Name = "blueprints";
            this.blueprints.Size = new System.Drawing.Size(488, 21);
            this.blueprints.TabIndex = 325;
            this.blueprints.SelectedIndexChanged += new System.EventHandler(this.blueprints_SelectedIndexChanged);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.blueprintOutput);
            this.groupBox3.Controls.Add(this.removeOutputItem);
            this.groupBox3.Controls.Add(this.addNewOutputItem);
            this.groupBox3.Location = new System.Drawing.Point(259, 39);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(241, 234);
            this.groupBox3.TabIndex = 335;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Blueprint Output When Crafted";
            // 
            // blueprintOutput
            // 
            this.blueprintOutput.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4});
            this.blueprintOutput.FullRowSelect = true;
            this.blueprintOutput.HideSelection = false;
            this.blueprintOutput.Location = new System.Drawing.Point(8, 22);
            this.blueprintOutput.MultiSelect = false;
            this.blueprintOutput.Name = "blueprintOutput";
            this.blueprintOutput.Size = new System.Drawing.Size(225, 175);
            this.blueprintOutput.TabIndex = 339;
            this.blueprintOutput.UseCompatibleStateImageBehavior = false;
            this.blueprintOutput.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Item Name";
            this.columnHeader3.Width = 145;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Quantity";
            // 
            // removeOutputItem
            // 
            this.removeOutputItem.Location = new System.Drawing.Point(8, 203);
            this.removeOutputItem.Name = "removeOutputItem";
            this.removeOutputItem.Size = new System.Drawing.Size(110, 25);
            this.removeOutputItem.TabIndex = 335;
            this.removeOutputItem.Text = "Remove Selected";
            this.removeOutputItem.UseVisualStyleBackColor = true;
            this.removeOutputItem.Click += new System.EventHandler(this.removeOutputItem_Click);
            // 
            // addNewOutputItem
            // 
            this.addNewOutputItem.Location = new System.Drawing.Point(124, 203);
            this.addNewOutputItem.Name = "addNewOutputItem";
            this.addNewOutputItem.Size = new System.Drawing.Size(109, 25);
            this.addNewOutputItem.TabIndex = 330;
            this.addNewOutputItem.Text = "Add New";
            this.addNewOutputItem.UseVisualStyleBackColor = true;
            this.addNewOutputItem.Click += new System.EventHandler(this.button3_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(9, 283);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(299, 26);
            this.label5.TabIndex = 336;
            this.label5.Text = "It\'s recommended to stick to existing item input requirements\r\nas the UI will nee" +
    "d to be modded to support new components.\r\n";
            // 
            // helpBtn
            // 
            this.helpBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.helpBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.helpBtn.Image = ((System.Drawing.Image)(resources.GetObject("helpBtn.Image")));
            this.helpBtn.Location = new System.Drawing.Point(490, 1);
            this.helpBtn.Name = "helpBtn";
            this.helpBtn.Size = new System.Drawing.Size(20, 20);
            this.helpBtn.TabIndex = 373;
            this.helpBtn.UseVisualStyleBackColor = true;
            this.helpBtn.Click += new System.EventHandler(this.helpBtn_Click);
            // 
            // BlueprintEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(510, 322);
            this.Controls.Add(this.helpBtn);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.blueprints);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "BlueprintEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Blueprint Recipe Editor";
            this.groupBox1.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.ComboBox blueprints;
        private System.Windows.Forms.Button removeInputItem;
        private System.Windows.Forms.Button addNewItemRequired;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button removeOutputItem;
        private System.Windows.Forms.Button addNewOutputItem;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button helpBtn;
        private System.Windows.Forms.ListView blueprintInput;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ListView blueprintOutput;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
    }
}
