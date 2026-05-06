namespace CommandsEditor.UserControls
{
    partial class GUI_ResourceDataType
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GUI_ResourceDataType));
            this.GUID_VARIABLE_DUMMY = new System.Windows.Forms.GroupBox();
            this.openResourceEditor = new System.Windows.Forms.Button();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.GUID_VARIABLE_DUMMY.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // GUID_VARIABLE_DUMMY
            // 
            this.GUID_VARIABLE_DUMMY.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.GUID_VARIABLE_DUMMY.Controls.Add(this.openResourceEditor);
            this.GUID_VARIABLE_DUMMY.Location = new System.Drawing.Point(3, 3);
            this.GUID_VARIABLE_DUMMY.Name = "GUID_VARIABLE_DUMMY";
            this.GUID_VARIABLE_DUMMY.Size = new System.Drawing.Size(334, 56);
            this.GUID_VARIABLE_DUMMY.TabIndex = 19;
            this.GUID_VARIABLE_DUMMY.TabStop = false;
            this.GUID_VARIABLE_DUMMY.Text = "Parameter Name (00-00-00-00)";
            // 
            // openResourceEditor
            // 
            this.openResourceEditor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.openResourceEditor.Location = new System.Drawing.Point(17, 21);
            this.openResourceEditor.Name = "openResourceEditor";
            this.openResourceEditor.Size = new System.Drawing.Size(304, 23);
            this.openResourceEditor.TabIndex = 0;
            this.openResourceEditor.Text = "Edit Resource References";
            this.openResourceEditor.UseVisualStyleBackColor = true;
            this.openResourceEditor.Click += new System.EventHandler(this.openResourceEditor_Click);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deleteToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(181, 48);
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteToolStripMenuItem.Image")));
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.deleteToolStripMenuItem.Text = "Delete";
            // 
            // GUI_ResourceDataType
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.GUID_VARIABLE_DUMMY);
            this.Name = "GUI_ResourceDataType";
            this.Size = new System.Drawing.Size(340, 61);
            this.GUID_VARIABLE_DUMMY.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox GUID_VARIABLE_DUMMY;
        private System.Windows.Forms.Button openResourceEditor;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
    }
}
