namespace OpenCAGE.ConfigEditors
{
    partial class PhysicalMaterialEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PhysicalMaterialEditor));
            this.materialList = new System.Windows.Forms.ListBox();
            this.addNew = new System.Windows.Forms.Button();
            this.removeSelected = new System.Windows.Forms.Button();
            this.helpBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // materialList
            // 
            this.materialList.FormattingEnabled = true;
            this.materialList.Location = new System.Drawing.Point(12, 12);
            this.materialList.Name = "materialList";
            this.materialList.Size = new System.Drawing.Size(698, 498);
            this.materialList.TabIndex = 1;
            // 
            // addNew
            // 
            this.addNew.Location = new System.Drawing.Point(12, 516);
            this.addNew.Name = "addNew";
            this.addNew.Size = new System.Drawing.Size(128, 32);
            this.addNew.TabIndex = 2;
            this.addNew.Text = "Add New";
            this.addNew.UseVisualStyleBackColor = true;
            this.addNew.Click += new System.EventHandler(this.addNew_Click);
            // 
            // removeSelected
            // 
            this.removeSelected.Location = new System.Drawing.Point(146, 516);
            this.removeSelected.Name = "removeSelected";
            this.removeSelected.Size = new System.Drawing.Size(128, 32);
            this.removeSelected.TabIndex = 3;
            this.removeSelected.Text = "Remove Selected";
            this.removeSelected.UseVisualStyleBackColor = true;
            this.removeSelected.Click += new System.EventHandler(this.removeSelected_Click);
            // 
            // helpBtn
            // 
            this.helpBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.helpBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.helpBtn.Image = ((System.Drawing.Image)(resources.GetObject("helpBtn.Image")));
            this.helpBtn.Location = new System.Drawing.Point(704, 0);
            this.helpBtn.Name = "helpBtn";
            this.helpBtn.Size = new System.Drawing.Size(20, 20);
            this.helpBtn.TabIndex = 477;
            this.helpBtn.UseVisualStyleBackColor = true;
            this.helpBtn.Click += new System.EventHandler(this.helpBtn_Click);
            // 
            // PhysicalMaterialEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(724, 558);
            this.Controls.Add(this.helpBtn);
            this.Controls.Add(this.removeSelected);
            this.Controls.Add(this.addNew);
            this.Controls.Add(this.materialList);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "PhysicalMaterialEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Physical Materials Editor";
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ListBox materialList;
        private System.Windows.Forms.Button addNew;
        private System.Windows.Forms.Button removeSelected;
        private System.Windows.Forms.Button helpBtn;
    }
}
