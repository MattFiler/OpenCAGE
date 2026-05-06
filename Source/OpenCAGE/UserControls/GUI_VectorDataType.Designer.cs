namespace CommandsEditor.UserControls
{
    partial class GUI_VectorDataType
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GUI_VectorDataType));
            this.POS_Z_1 = new SmoothNumericUpDown();
            this.POS_Y_1 = new SmoothNumericUpDown();
            this.POS_X_1 = new SmoothNumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.tableLayoutPanel9 = new System.Windows.Forms.TableLayoutPanel();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyTransformToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteTransformToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.POS_Z_1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Y_1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_X_1)).BeginInit();
            this.tableLayoutPanel9.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // POS_Z_1
            // 
            this.POS_Z_1.DecimalPlaces = 7;
            this.POS_Z_1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.POS_Z_1.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.POS_Z_1.Location = new System.Drawing.Point(225, 3);
            this.POS_Z_1.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.POS_Z_1.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_Z_1.Name = "POS_Z_1";
            this.POS_Z_1.Size = new System.Drawing.Size(106, 20);
            this.POS_Z_1.TabIndex = 5;
            this.toolTip1.SetToolTip(this.POS_Z_1, "Z");
            this.POS_Z_1.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_Z_1.ValueChanged += new System.EventHandler(this.POS_Z_1_ValueChanged);
            // 
            // POS_Y_1
            // 
            this.POS_Y_1.DecimalPlaces = 7;
            this.POS_Y_1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.POS_Y_1.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.POS_Y_1.Location = new System.Drawing.Point(114, 3);
            this.POS_Y_1.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.POS_Y_1.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_Y_1.Name = "POS_Y_1";
            this.POS_Y_1.Size = new System.Drawing.Size(105, 20);
            this.POS_Y_1.TabIndex = 3;
            this.toolTip1.SetToolTip(this.POS_Y_1, "Y");
            this.POS_Y_1.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_Y_1.ValueChanged += new System.EventHandler(this.POS_Y_1_ValueChanged);
            // 
            // POS_X_1
            // 
            this.POS_X_1.DecimalPlaces = 7;
            this.POS_X_1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.POS_X_1.Increment = new decimal(new int[] {
            1,
            0,
            0,
            851968});
            this.POS_X_1.Location = new System.Drawing.Point(3, 3);
            this.POS_X_1.Maximum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            0});
            this.POS_X_1.Minimum = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_X_1.Name = "POS_X_1";
            this.POS_X_1.Size = new System.Drawing.Size(105, 20);
            this.POS_X_1.TabIndex = 1;
            this.toolTip1.SetToolTip(this.POS_X_1, "X");
            this.POS_X_1.Value = new decimal(new int[] {
            1874919423,
            2328306,
            0,
            -2147483648});
            this.POS_X_1.ValueChanged += new System.EventHandler(this.POS_X_1_ValueChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 4);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(152, 13);
            this.label1.TabIndex = 17;
            this.label1.Text = "Parameter Name (00-00-00-00)";
            // 
            // tableLayoutPanel9
            // 
            this.tableLayoutPanel9.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel9.ColumnCount = 3;
            this.tableLayoutPanel9.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel9.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel9.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel9.Controls.Add(this.POS_Z_1, 2, 0);
            this.tableLayoutPanel9.Controls.Add(this.POS_Y_1, 1, 0);
            this.tableLayoutPanel9.Controls.Add(this.POS_X_1, 0, 0);
            this.tableLayoutPanel9.Location = new System.Drawing.Point(3, 22);
            this.tableLayoutPanel9.Name = "tableLayoutPanel9";
            this.tableLayoutPanel9.RowCount = 1;
            this.tableLayoutPanel9.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel9.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 27F));
            this.tableLayoutPanel9.Size = new System.Drawing.Size(334, 27);
            this.tableLayoutPanel9.TabIndex = 22;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyTransformToolStripMenuItem,
            this.pasteTransformToolStripMenuItem,
            this.toolStripSeparator1,
            this.deleteToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(139, 76);
            // 
            // copyTransformToolStripMenuItem
            // 
            this.copyTransformToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("copyTransformToolStripMenuItem.Image")));
            this.copyTransformToolStripMenuItem.Name = "copyTransformToolStripMenuItem";
            this.copyTransformToolStripMenuItem.Size = new System.Drawing.Size(138, 22);
            this.copyTransformToolStripMenuItem.Text = "Copy Vector";
            this.copyTransformToolStripMenuItem.Click += new System.EventHandler(this.copyTransformToolStripMenuItem_Click);
            // 
            // pasteTransformToolStripMenuItem
            // 
            this.pasteTransformToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("pasteTransformToolStripMenuItem.Image")));
            this.pasteTransformToolStripMenuItem.Name = "pasteTransformToolStripMenuItem";
            this.pasteTransformToolStripMenuItem.Size = new System.Drawing.Size(138, 22);
            this.pasteTransformToolStripMenuItem.Text = "Paste Vector";
            this.pasteTransformToolStripMenuItem.Click += new System.EventHandler(this.pasteTransformToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(135, 6);
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteToolStripMenuItem.Image")));
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(138, 22);
            this.deleteToolStripMenuItem.Text = "Delete";
            // 
            // GUI_VectorDataType
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel9);
            this.Controls.Add(this.label1);
            this.Name = "GUI_VectorDataType";
            this.Size = new System.Drawing.Size(340, 51);
            ((System.ComponentModel.ISupportInitialize)(this.POS_Z_1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_Y_1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.POS_X_1)).EndInit();
            this.tableLayoutPanel9.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private SmoothNumericUpDown POS_Z_1;
        private SmoothNumericUpDown POS_Y_1;
        private SmoothNumericUpDown POS_X_1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel9;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem copyTransformToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteTransformToolStripMenuItem;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
    }
}
