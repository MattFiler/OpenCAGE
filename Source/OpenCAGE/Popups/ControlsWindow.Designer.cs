namespace OpenCAGE.Popups
{
    partial class ControlsWindow
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ControlsWindow));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.FlowgraphControls = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.ModelViewerControls = new System.Windows.Forms.ListView();
            this.Action = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Binding = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.LevelViewerControls = new System.Windows.Forms.ListView();
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(497, 298);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.FlowgraphControls);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(489, 272);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Flowgraph Controls";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // FlowgraphControls
            // 
            this.FlowgraphControls.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
            this.FlowgraphControls.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FlowgraphControls.FullRowSelect = true;
            this.FlowgraphControls.HideSelection = false;
            this.FlowgraphControls.LabelWrap = false;
            this.FlowgraphControls.Location = new System.Drawing.Point(3, 3);
            this.FlowgraphControls.MultiSelect = false;
            this.FlowgraphControls.Name = "FlowgraphControls";
            this.FlowgraphControls.Size = new System.Drawing.Size(483, 266);
            this.FlowgraphControls.TabIndex = 178;
            this.FlowgraphControls.UseCompatibleStateImageBehavior = false;
            this.FlowgraphControls.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Action";
            this.columnHeader1.Width = 161;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Binding";
            this.columnHeader2.Width = 286;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.ModelViewerControls);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(489, 272);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Model Viewer Controls";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // ModelViewerControls
            // 
            this.ModelViewerControls.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.Action,
            this.Binding});
            this.ModelViewerControls.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ModelViewerControls.FullRowSelect = true;
            this.ModelViewerControls.HideSelection = false;
            this.ModelViewerControls.LabelWrap = false;
            this.ModelViewerControls.Location = new System.Drawing.Point(3, 3);
            this.ModelViewerControls.MultiSelect = false;
            this.ModelViewerControls.Name = "ModelViewerControls";
            this.ModelViewerControls.Size = new System.Drawing.Size(483, 266);
            this.ModelViewerControls.TabIndex = 177;
            this.ModelViewerControls.UseCompatibleStateImageBehavior = false;
            this.ModelViewerControls.View = System.Windows.Forms.View.Details;
            // 
            // Action
            // 
            this.Action.Text = "Action";
            this.Action.Width = 161;
            // 
            // Binding
            // 
            this.Binding.Text = "Binding";
            this.Binding.Width = 286;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.LevelViewerControls);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(489, 272);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Level Viewer Controls";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // LevelViewerControls
            // 
            this.LevelViewerControls.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4});
            this.LevelViewerControls.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LevelViewerControls.FullRowSelect = true;
            this.LevelViewerControls.HideSelection = false;
            this.LevelViewerControls.LabelWrap = false;
            this.LevelViewerControls.Location = new System.Drawing.Point(0, 0);
            this.LevelViewerControls.MultiSelect = false;
            this.LevelViewerControls.Name = "LevelViewerControls";
            this.LevelViewerControls.Size = new System.Drawing.Size(489, 272);
            this.LevelViewerControls.TabIndex = 178;
            this.LevelViewerControls.UseCompatibleStateImageBehavior = false;
            this.LevelViewerControls.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Action";
            this.columnHeader3.Width = 161;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Binding";
            this.columnHeader4.Width = 286;
            // 
            // ControlsWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(497, 298);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "ControlsWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OpenCAGE Commands Editor Controls";
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.ListView FlowgraphControls;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ListView ModelViewerControls;
        private System.Windows.Forms.ColumnHeader Action;
        private System.Windows.Forms.ColumnHeader Binding;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.ListView LevelViewerControls;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
    }
}
