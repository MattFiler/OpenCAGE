namespace Alien_Isolation_Mod_Tools
{
    partial class BlueprintEditorPopup
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BlueprintEditorPopup));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.RESOURCES_NEW = new System.Windows.Forms.ComboBox();
            this.labelToChange = new System.Windows.Forms.Label();
            this.QUANTITY_NEW = new System.Windows.Forms.TextBox();
            this.label40 = new System.Windows.Forms.Label();
            this.label78 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.openItemDocs = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.RESOURCES_NEW);
            this.groupBox1.Controls.Add(this.labelToChange);
            this.groupBox1.Controls.Add(this.QUANTITY_NEW);
            this.groupBox1.Controls.Add(this.label40);
            this.groupBox1.Location = new System.Drawing.Point(5, 41);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(439, 84);
            this.groupBox1.TabIndex = 328;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "New Item Info";
            // 
            // RESOURCES_NEW
            // 
            this.RESOURCES_NEW.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.RESOURCES_NEW.FormattingEnabled = true;
            this.RESOURCES_NEW.Items.AddRange(new object[] {
            "PLACEHOLDER"});
            this.RESOURCES_NEW.Location = new System.Drawing.Point(20, 41);
            this.RESOURCES_NEW.Name = "RESOURCES_NEW";
            this.RESOURCES_NEW.Size = new System.Drawing.Size(187, 21);
            this.RESOURCES_NEW.TabIndex = 355;
            // 
            // labelToChange
            // 
            this.labelToChange.AutoSize = true;
            this.labelToChange.Location = new System.Drawing.Point(17, 26);
            this.labelToChange.Name = "labelToChange";
            this.labelToChange.Size = new System.Drawing.Size(86, 13);
            this.labelToChange.TabIndex = 237;
            this.labelToChange.Text = "PLACEHOLDER";
            // 
            // QUANTITY_NEW
            // 
            this.QUANTITY_NEW.Location = new System.Drawing.Point(230, 42);
            this.QUANTITY_NEW.Name = "QUANTITY_NEW";
            this.QUANTITY_NEW.Size = new System.Drawing.Size(187, 20);
            this.QUANTITY_NEW.TabIndex = 244;
            // 
            // label40
            // 
            this.label40.AutoSize = true;
            this.label40.Location = new System.Drawing.Point(227, 26);
            this.label40.Name = "label40";
            this.label40.Size = new System.Drawing.Size(46, 13);
            this.label40.TabIndex = 243;
            this.label40.Text = "Quantity";
            // 
            // label78
            // 
            this.label78.AutoSize = true;
            this.label78.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label78.Location = new System.Drawing.Point(14, 9);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(424, 29);
            this.label78.TabIndex = 327;
            this.label78.Text = "Alien: Isolation Blueprint Recipe Editor";
            this.label78.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(263, 131);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(181, 30);
            this.btnSave.TabIndex = 326;
            this.btnSave.Text = "Add Item";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // openItemDocs
            // 
            this.openItemDocs.Location = new System.Drawing.Point(5, 140);
            this.openItemDocs.Name = "openItemDocs";
            this.openItemDocs.Size = new System.Drawing.Size(129, 21);
            this.openItemDocs.TabIndex = 329;
            this.openItemDocs.Text = "Item Documentation";
            this.openItemDocs.UseVisualStyleBackColor = true;
            this.openItemDocs.Click += new System.EventHandler(this.openItemDocs_Click);
            // 
            // BlueprintEditorPopup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(450, 168);
            this.Controls.Add(this.openItemDocs);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label78);
            this.Controls.Add(this.btnSave);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "BlueprintEditorPopup";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Blueprint Recipe Editor";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label labelToChange;
        private System.Windows.Forms.TextBox QUANTITY_NEW;
        private System.Windows.Forms.Label label40;
        private System.Windows.Forms.Label label78;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.ComboBox RESOURCES_NEW;
        private System.Windows.Forms.Button openItemDocs;
    }
}