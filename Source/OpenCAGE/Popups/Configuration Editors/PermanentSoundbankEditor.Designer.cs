namespace CommandsEditor.ConfigEditors
{
    partial class PermanentSoundbankEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PermanentSoundbankEditor));
            this.removeSelected = new System.Windows.Forms.Button();
            this.addNew = new System.Windows.Forms.Button();
            this.permaSoundbanks = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.helpBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // removeSelected
            // 
            this.removeSelected.Location = new System.Drawing.Point(450, 375);
            this.removeSelected.Name = "removeSelected";
            this.removeSelected.Size = new System.Drawing.Size(136, 33);
            this.removeSelected.TabIndex = 329;
            this.removeSelected.Text = "Remove Selected";
            this.removeSelected.UseVisualStyleBackColor = true;
            this.removeSelected.Click += new System.EventHandler(this.removeSelected_Click);
            // 
            // addNew
            // 
            this.addNew.Location = new System.Drawing.Point(308, 375);
            this.addNew.Name = "addNew";
            this.addNew.Size = new System.Drawing.Size(136, 33);
            this.addNew.TabIndex = 328;
            this.addNew.Text = "Add New";
            this.addNew.UseVisualStyleBackColor = true;
            this.addNew.Click += new System.EventHandler(this.addNew_Click);
            // 
            // permaSoundbanks
            // 
            this.permaSoundbanks.FormattingEnabled = true;
            this.permaSoundbanks.Location = new System.Drawing.Point(15, 27);
            this.permaSoundbanks.Name = "permaSoundbanks";
            this.permaSoundbanks.Size = new System.Drawing.Size(571, 342);
            this.permaSoundbanks.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(170, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Permanently Loaded Soundbanks:";
            // 
            // helpBtn
            // 
            this.helpBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.helpBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.helpBtn.Image = ((System.Drawing.Image)(resources.GetObject("helpBtn.Image")));
            this.helpBtn.Location = new System.Drawing.Point(578, 0);
            this.helpBtn.Name = "helpBtn";
            this.helpBtn.Size = new System.Drawing.Size(20, 20);
            this.helpBtn.TabIndex = 477;
            this.helpBtn.UseVisualStyleBackColor = true;
            this.helpBtn.Click += new System.EventHandler(this.helpBtn_Click);
            // 
            // PermanentSoundbankEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(598, 420);
            this.Controls.Add(this.helpBtn);
            this.Controls.Add(this.removeSelected);
            this.Controls.Add(this.addNew);
            this.Controls.Add(this.permaSoundbanks);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "PermanentSoundbankEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Permanent Soundbank Editor";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListBox permaSoundbanks;
        private System.Windows.Forms.Button addNew;
        private System.Windows.Forms.Button removeSelected;
        private System.Windows.Forms.Button helpBtn;
    }
}
