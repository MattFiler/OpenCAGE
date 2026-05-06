namespace CommandsEditor.UserControls
{
    partial class GUI_VectorVariant_Colour
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GUI_VectorVariant_Colour));
            this.openColourPicker = new System.Windows.Forms.Button();
            this.GUID_VARIABLE_DUMMY = new System.Windows.Forms.GroupBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyTransformToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteTransformToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.GUID_VARIABLE_DUMMY.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // openColourPicker
            // 
            this.openColourPicker.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.openColourPicker.Location = new System.Drawing.Point(17, 21);
            this.openColourPicker.Name = "openColourPicker";
            this.openColourPicker.Size = new System.Drawing.Size(259, 23);
            this.openColourPicker.TabIndex = 0;
            this.openColourPicker.Text = "Edit Colour";
            this.openColourPicker.UseVisualStyleBackColor = true;
            this.openColourPicker.Click += new System.EventHandler(this.openColourPicker_Click);
            // 
            // GUID_VARIABLE_DUMMY
            // 
            this.GUID_VARIABLE_DUMMY.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.GUID_VARIABLE_DUMMY.ContextMenuStrip = this.contextMenuStrip1;
            this.GUID_VARIABLE_DUMMY.Controls.Add(this.pictureBox1);
            this.GUID_VARIABLE_DUMMY.Controls.Add(this.openColourPicker);
            this.GUID_VARIABLE_DUMMY.Location = new System.Drawing.Point(3, 3);
            this.GUID_VARIABLE_DUMMY.Name = "GUID_VARIABLE_DUMMY";
            this.GUID_VARIABLE_DUMMY.Size = new System.Drawing.Size(334, 56);
            this.GUID_VARIABLE_DUMMY.TabIndex = 20;
            this.GUID_VARIABLE_DUMMY.TabStop = false;
            this.GUID_VARIABLE_DUMMY.Text = "Parameter Name (00-00-00-00)";
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyTransformToolStripMenuItem,
            this.pasteTransformToolStripMenuItem,
            this.toolStripSeparator1,
            this.deleteToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(181, 98);
            // 
            // copyTransformToolStripMenuItem
            // 
            this.copyTransformToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("copyTransformToolStripMenuItem.Image")));
            this.copyTransformToolStripMenuItem.Name = "copyTransformToolStripMenuItem";
            this.copyTransformToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.copyTransformToolStripMenuItem.Text = "Copy Colour";
            this.copyTransformToolStripMenuItem.Click += new System.EventHandler(this.copyTransformToolStripMenuItem_Click);
            // 
            // pasteTransformToolStripMenuItem
            // 
            this.pasteTransformToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("pasteTransformToolStripMenuItem.Image")));
            this.pasteTransformToolStripMenuItem.Name = "pasteTransformToolStripMenuItem";
            this.pasteTransformToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.pasteTransformToolStripMenuItem.Text = "Paste Colour";
            this.pasteTransformToolStripMenuItem.Click += new System.EventHandler(this.pasteTransformToolStripMenuItem_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.pictureBox1.Location = new System.Drawing.Point(293, 13);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(35, 35);
            this.pictureBox1.TabIndex = 1;
            this.pictureBox1.TabStop = false;
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteToolStripMenuItem.Image")));
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.deleteToolStripMenuItem.Text = "Delete";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(177, 6);
            // 
            // GUI_VectorVariant_Colour
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.GUID_VARIABLE_DUMMY);
            this.Name = "GUI_VectorVariant_Colour";
            this.Size = new System.Drawing.Size(340, 61);
            this.GUID_VARIABLE_DUMMY.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button openColourPicker;
        private System.Windows.Forms.GroupBox GUID_VARIABLE_DUMMY;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem copyTransformToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteTransformToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
    }
}
