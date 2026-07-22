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
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.EntityListControls = new System.Windows.Forms.ListView();
            this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader6 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.BehaviourTreeControls = new System.Windows.Forms.ListView();
            this.columnHeader7 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader8 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tabPage6 = new System.Windows.Forms.TabPage();
            this.CAGEAnimationControls = new System.Windows.Forms.ListView();
            this.columnHeader9 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader10 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.remainOnTop = new System.Windows.Forms.CheckBox();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.tabPage5.SuspendLayout();
            this.tabPage6.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Controls.Add(this.tabPage6);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(497, 300);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.FlowgraphControls);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(489, 274);
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
            this.FlowgraphControls.Size = new System.Drawing.Size(483, 268);
            this.FlowgraphControls.TabIndex = 178;
            this.FlowgraphControls.UseCompatibleStateImageBehavior = false;
            this.FlowgraphControls.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Action";
            this.columnHeader1.Width = 220;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Binding";
            this.columnHeader2.Width = 240;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.ModelViewerControls);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(489, 274);
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
            this.ModelViewerControls.Size = new System.Drawing.Size(483, 268);
            this.ModelViewerControls.TabIndex = 177;
            this.ModelViewerControls.UseCompatibleStateImageBehavior = false;
            this.ModelViewerControls.View = System.Windows.Forms.View.Details;
            // 
            // Action
            // 
            this.Action.Text = "Action";
            this.Action.Width = 220;
            // 
            // Binding
            // 
            this.Binding.Text = "Binding";
            this.Binding.Width = 240;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.LevelViewerControls);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(489, 274);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Viewport Controls";
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
            this.LevelViewerControls.Size = new System.Drawing.Size(489, 274);
            this.LevelViewerControls.TabIndex = 178;
            this.LevelViewerControls.UseCompatibleStateImageBehavior = false;
            this.LevelViewerControls.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Action";
            this.columnHeader3.Width = 220;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Binding";
            this.columnHeader4.Width = 240;
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.EntityListControls);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Size = new System.Drawing.Size(489, 274);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Entity List Controls";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // EntityListControls
            // 
            this.EntityListControls.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader5,
            this.columnHeader6});
            this.EntityListControls.Dock = System.Windows.Forms.DockStyle.Fill;
            this.EntityListControls.FullRowSelect = true;
            this.EntityListControls.HideSelection = false;
            this.EntityListControls.LabelWrap = false;
            this.EntityListControls.Location = new System.Drawing.Point(0, 0);
            this.EntityListControls.MultiSelect = false;
            this.EntityListControls.Name = "EntityListControls";
            this.EntityListControls.Size = new System.Drawing.Size(489, 274);
            this.EntityListControls.TabIndex = 179;
            this.EntityListControls.UseCompatibleStateImageBehavior = false;
            this.EntityListControls.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader5
            // 
            this.columnHeader5.Text = "Action";
            this.columnHeader5.Width = 220;
            // 
            // columnHeader6
            // 
            this.columnHeader6.Text = "Binding";
            this.columnHeader6.Width = 240;
            // 
            // tabPage5
            // 
            this.tabPage5.Controls.Add(this.BehaviourTreeControls);
            this.tabPage5.Location = new System.Drawing.Point(4, 22);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Size = new System.Drawing.Size(489, 274);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "Behaviour Tree Controls";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // BehaviourTreeControls
            // 
            this.BehaviourTreeControls.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader7,
            this.columnHeader8});
            this.BehaviourTreeControls.Dock = System.Windows.Forms.DockStyle.Fill;
            this.BehaviourTreeControls.FullRowSelect = true;
            this.BehaviourTreeControls.HideSelection = false;
            this.BehaviourTreeControls.LabelWrap = false;
            this.BehaviourTreeControls.Location = new System.Drawing.Point(0, 0);
            this.BehaviourTreeControls.MultiSelect = false;
            this.BehaviourTreeControls.Name = "BehaviourTreeControls";
            this.BehaviourTreeControls.Size = new System.Drawing.Size(489, 274);
            this.BehaviourTreeControls.TabIndex = 180;
            this.BehaviourTreeControls.UseCompatibleStateImageBehavior = false;
            this.BehaviourTreeControls.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader7
            // 
            this.columnHeader7.Text = "Action";
            this.columnHeader7.Width = 280;
            // 
            // columnHeader8
            // 
            this.columnHeader8.Text = "Binding";
            this.columnHeader8.Width = 200;
            // 
            // tabPage6
            // 
            this.tabPage6.Controls.Add(this.CAGEAnimationControls);
            this.tabPage6.Location = new System.Drawing.Point(4, 22);
            this.tabPage6.Name = "tabPage6";
            this.tabPage6.Size = new System.Drawing.Size(489, 274);
            this.tabPage6.TabIndex = 5;
            this.tabPage6.Text = "CAGEAnimation Controls";
            this.tabPage6.UseVisualStyleBackColor = true;
            // 
            // CAGEAnimationControls
            // 
            this.CAGEAnimationControls.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader9,
            this.columnHeader10});
            this.CAGEAnimationControls.Dock = System.Windows.Forms.DockStyle.Fill;
            this.CAGEAnimationControls.FullRowSelect = true;
            this.CAGEAnimationControls.HideSelection = false;
            this.CAGEAnimationControls.LabelWrap = false;
            this.CAGEAnimationControls.Location = new System.Drawing.Point(0, 0);
            this.CAGEAnimationControls.MultiSelect = false;
            this.CAGEAnimationControls.Name = "CAGEAnimationControls";
            this.CAGEAnimationControls.Size = new System.Drawing.Size(489, 274);
            this.CAGEAnimationControls.TabIndex = 181;
            this.CAGEAnimationControls.UseCompatibleStateImageBehavior = false;
            this.CAGEAnimationControls.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader9
            // 
            this.columnHeader9.Text = "Action";
            this.columnHeader9.Width = 220;
            // 
            // columnHeader10
            // 
            this.columnHeader10.Text = "Binding";
            this.columnHeader10.Width = 240;
            // 
            // remainOnTop
            // 
            this.remainOnTop.AutoSize = true;
            this.remainOnTop.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.remainOnTop.Location = new System.Drawing.Point(0, 300);
            this.remainOnTop.Name = "remainOnTop";
            this.remainOnTop.Padding = new System.Windows.Forms.Padding(6, 6, 0, 6);
            this.remainOnTop.Size = new System.Drawing.Size(497, 29);
            this.remainOnTop.TabIndex = 1;
            this.remainOnTop.Text = "Remain on top";
            this.remainOnTop.UseVisualStyleBackColor = true;
            // 
            // ControlsWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(497, 329);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.remainOnTop);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "ControlsWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OpenCAGE Commands Editor Controls";
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.tabPage4.ResumeLayout(false);
            this.tabPage5.ResumeLayout(false);
            this.tabPage6.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

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
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.ListView EntityListControls;
        private System.Windows.Forms.ColumnHeader columnHeader5;
        private System.Windows.Forms.ColumnHeader columnHeader6;
        private System.Windows.Forms.TabPage tabPage5;
        private System.Windows.Forms.ListView BehaviourTreeControls;
        private System.Windows.Forms.ColumnHeader columnHeader7;
        private System.Windows.Forms.ColumnHeader columnHeader8;
        private System.Windows.Forms.TabPage tabPage6;
        private System.Windows.Forms.ListView CAGEAnimationControls;
        private System.Windows.Forms.ColumnHeader columnHeader9;
        private System.Windows.Forms.ColumnHeader columnHeader10;
        private System.Windows.Forms.CheckBox remainOnTop;
    }
}
