namespace CommandsEditor.UserControls
{
    partial class GUI_SplineDataType
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GUI_SplineDataType));
            this.SPLINE_CONTAINER = new System.Windows.Forms.GroupBox();
            this.openSplineEditor = new System.Windows.Forms.Button();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SPLINE_CONTAINER.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // SPLINE_CONTAINER
            // 
            this.SPLINE_CONTAINER.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.SPLINE_CONTAINER.Controls.Add(this.openSplineEditor);
            this.SPLINE_CONTAINER.Location = new System.Drawing.Point(3, 3);
            this.SPLINE_CONTAINER.Name = "SPLINE_CONTAINER";
            this.SPLINE_CONTAINER.Size = new System.Drawing.Size(334, 56);
            this.SPLINE_CONTAINER.TabIndex = 18;
            this.SPLINE_CONTAINER.TabStop = false;
            this.SPLINE_CONTAINER.Text = "Parameter Name (00-00-00-00)";
            // 
            // openSplineEditor
            // 
            this.openSplineEditor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.openSplineEditor.Location = new System.Drawing.Point(17, 21);
            this.openSplineEditor.Name = "openSplineEditor";
            this.openSplineEditor.Size = new System.Drawing.Size(304, 23);
            this.openSplineEditor.TabIndex = 1;
            this.openSplineEditor.Text = "Edit Spline";
            this.openSplineEditor.UseVisualStyleBackColor = true;
            this.openSplineEditor.Click += new System.EventHandler(this.openSplineEditor_Click);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deleteToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(108, 26);
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteToolStripMenuItem.Image")));
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.deleteToolStripMenuItem.Text = "Delete";
            // 
            // GUI_SplineDataType
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.SPLINE_CONTAINER);
            this.Name = "GUI_SplineDataType";
            this.Size = new System.Drawing.Size(340, 61);
            this.SPLINE_CONTAINER.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox SPLINE_CONTAINER;
        private System.Windows.Forms.Button openSplineEditor;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
    }
}
