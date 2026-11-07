namespace OpenCAGE
{
    partial class BespokeShaderStageView
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.buttonBar = new System.Windows.Forms.FlowLayoutPanel();
            this.decompileBtn = new System.Windows.Forms.Button();
            this.compileBtn = new System.Windows.Forms.Button();
            this.restoreBtn = new System.Windows.Forms.Button();
            this.exportBtn = new System.Windows.Forms.Button();
            this.importBtn = new System.Windows.Forms.Button();
            this.stateLabel = new System.Windows.Forms.Label();
            this.views = new System.Windows.Forms.TabControl();
            this.hlslTab = new System.Windows.Forms.TabPage();
            this.hlslText = new System.Windows.Forms.TextBox();
            this.assemblyTab = new System.Windows.Forms.TabPage();
            this.assemblyText = new System.Windows.Forms.TextBox();
            this.messagesSplitter = new System.Windows.Forms.Splitter();
            this.messagesText = new System.Windows.Forms.TextBox();
            this.buttonBar.SuspendLayout();
            this.views.SuspendLayout();
            this.hlslTab.SuspendLayout();
            this.assemblyTab.SuspendLayout();
            this.SuspendLayout();
            //
            // buttonBar
            //
            this.buttonBar.Controls.Add(this.decompileBtn);
            this.buttonBar.Controls.Add(this.compileBtn);
            this.buttonBar.Controls.Add(this.restoreBtn);
            this.buttonBar.Controls.Add(this.exportBtn);
            this.buttonBar.Controls.Add(this.importBtn);
            this.buttonBar.Controls.Add(this.stateLabel);
            this.buttonBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.buttonBar.Location = new System.Drawing.Point(0, 0);
            this.buttonBar.Name = "buttonBar";
            this.buttonBar.Padding = new System.Windows.Forms.Padding(2);
            this.buttonBar.Size = new System.Drawing.Size(840, 34);
            this.buttonBar.TabIndex = 0;
            this.buttonBar.WrapContents = false;
            //
            // decompileBtn
            //
            this.decompileBtn.Location = new System.Drawing.Point(5, 5);
            this.decompileBtn.Name = "decompileBtn";
            this.decompileBtn.Size = new System.Drawing.Size(100, 26);
            this.decompileBtn.TabIndex = 0;
            this.decompileBtn.Text = "Decompile";
            this.decompileBtn.UseVisualStyleBackColor = true;
            this.decompileBtn.Click += new System.EventHandler(this.decompileBtn_Click);
            //
            // compileBtn
            //
            this.compileBtn.Location = new System.Drawing.Point(111, 5);
            this.compileBtn.Name = "compileBtn";
            this.compileBtn.Size = new System.Drawing.Size(130, 26);
            this.compileBtn.TabIndex = 1;
            this.compileBtn.Text = "Compile and Save";
            this.compileBtn.UseVisualStyleBackColor = true;
            this.compileBtn.Click += new System.EventHandler(this.compileBtn_Click);
            //
            // restoreBtn
            //
            this.restoreBtn.Location = new System.Drawing.Point(247, 5);
            this.restoreBtn.Name = "restoreBtn";
            this.restoreBtn.Size = new System.Drawing.Size(125, 26);
            this.restoreBtn.TabIndex = 2;
            this.restoreBtn.Text = "Restore Original";
            this.restoreBtn.UseVisualStyleBackColor = true;
            this.restoreBtn.Click += new System.EventHandler(this.restoreBtn_Click);
            //
            // exportBtn
            //
            this.exportBtn.Location = new System.Drawing.Point(378, 5);
            this.exportBtn.Name = "exportBtn";
            this.exportBtn.Size = new System.Drawing.Size(110, 26);
            this.exportBtn.TabIndex = 3;
            this.exportBtn.Text = "Export HLSL...";
            this.exportBtn.UseVisualStyleBackColor = true;
            this.exportBtn.Click += new System.EventHandler(this.exportBtn_Click);
            //
            // importBtn
            //
            this.importBtn.Location = new System.Drawing.Point(494, 5);
            this.importBtn.Name = "importBtn";
            this.importBtn.Size = new System.Drawing.Size(110, 26);
            this.importBtn.TabIndex = 4;
            this.importBtn.Text = "Import HLSL...";
            this.importBtn.UseVisualStyleBackColor = true;
            this.importBtn.Click += new System.EventHandler(this.importBtn_Click);
            //
            // stateLabel
            //
            this.stateLabel.AutoSize = true;
            this.stateLabel.Location = new System.Drawing.Point(615, 10);
            this.stateLabel.Margin = new System.Windows.Forms.Padding(8, 8, 3, 3);
            this.stateLabel.Name = "stateLabel";
            this.stateLabel.Size = new System.Drawing.Size(0, 13);
            this.stateLabel.TabIndex = 5;
            //
            // views
            //
            this.views.Controls.Add(this.hlslTab);
            this.views.Controls.Add(this.assemblyTab);
            this.views.Dock = System.Windows.Forms.DockStyle.Fill;
            this.views.Location = new System.Drawing.Point(0, 34);
            this.views.Name = "views";
            this.views.SelectedIndex = 0;
            this.views.Size = new System.Drawing.Size(840, 451);
            this.views.TabIndex = 1;
            //
            // hlslTab
            //
            this.hlslTab.Controls.Add(this.hlslText);
            this.hlslTab.Location = new System.Drawing.Point(4, 22);
            this.hlslTab.Name = "hlslTab";
            this.hlslTab.Size = new System.Drawing.Size(832, 425);
            this.hlslTab.TabIndex = 0;
            this.hlslTab.Text = "HLSL";
            this.hlslTab.UseVisualStyleBackColor = true;
            //
            // hlslText
            //
            this.hlslText.AcceptsReturn = true;
            this.hlslText.AcceptsTab = true;
            this.hlslText.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hlslText.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.hlslText.HideSelection = false;
            this.hlslText.Location = new System.Drawing.Point(0, 0);
            this.hlslText.MaxLength = 0;
            this.hlslText.Multiline = true;
            this.hlslText.Name = "hlslText";
            this.hlslText.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.hlslText.Size = new System.Drawing.Size(832, 425);
            this.hlslText.TabIndex = 0;
            this.hlslText.WordWrap = false;
            this.hlslText.TextChanged += new System.EventHandler(this.hlslText_TextChanged);
            this.hlslText.VisibleChanged += new System.EventHandler(this.hlslText_VisibleChanged);
            this.hlslText.KeyDown += new System.Windows.Forms.KeyEventHandler(this.hlslText_KeyDown);
            //
            // assemblyTab
            //
            this.assemblyTab.Controls.Add(this.assemblyText);
            this.assemblyTab.Location = new System.Drawing.Point(4, 22);
            this.assemblyTab.Name = "assemblyTab";
            this.assemblyTab.Size = new System.Drawing.Size(832, 425);
            this.assemblyTab.TabIndex = 1;
            this.assemblyTab.Text = "Assembly (read only)";
            this.assemblyTab.UseVisualStyleBackColor = true;
            //
            // assemblyText
            //
            this.assemblyText.Dock = System.Windows.Forms.DockStyle.Fill;
            this.assemblyText.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.assemblyText.HideSelection = false;
            this.assemblyText.Location = new System.Drawing.Point(0, 0);
            this.assemblyText.MaxLength = 0;
            this.assemblyText.Multiline = true;
            this.assemblyText.Name = "assemblyText";
            this.assemblyText.ReadOnly = true;
            this.assemblyText.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.assemblyText.Size = new System.Drawing.Size(832, 425);
            this.assemblyText.TabIndex = 0;
            this.assemblyText.WordWrap = false;
            this.assemblyText.KeyDown += new System.Windows.Forms.KeyEventHandler(this.assemblyText_KeyDown);
            //
            // messagesSplitter
            //
            this.messagesSplitter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.messagesSplitter.Location = new System.Drawing.Point(0, 485);
            this.messagesSplitter.Name = "messagesSplitter";
            this.messagesSplitter.Size = new System.Drawing.Size(840, 5);
            this.messagesSplitter.TabIndex = 2;
            this.messagesSplitter.TabStop = false;
            //
            // messagesText
            //
            this.messagesText.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.messagesText.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.messagesText.Location = new System.Drawing.Point(0, 490);
            this.messagesText.MaxLength = 0;
            this.messagesText.Multiline = true;
            this.messagesText.Name = "messagesText";
            this.messagesText.ReadOnly = true;
            this.messagesText.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.messagesText.Size = new System.Drawing.Size(840, 110);
            this.messagesText.TabIndex = 3;
            //
            // BespokeShaderStageView
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.views);
            this.Controls.Add(this.messagesSplitter);
            this.Controls.Add(this.messagesText);
            this.Controls.Add(this.buttonBar);
            this.Name = "BespokeShaderStageView";
            this.Size = new System.Drawing.Size(840, 600);
            this.buttonBar.ResumeLayout(false);
            this.buttonBar.PerformLayout();
            this.views.ResumeLayout(false);
            this.hlslTab.ResumeLayout(false);
            this.hlslTab.PerformLayout();
            this.assemblyTab.ResumeLayout(false);
            this.assemblyTab.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel buttonBar;
        private System.Windows.Forms.Button decompileBtn;
        private System.Windows.Forms.Button compileBtn;
        private System.Windows.Forms.Button restoreBtn;
        private System.Windows.Forms.Button exportBtn;
        private System.Windows.Forms.Button importBtn;
        private System.Windows.Forms.Label stateLabel;
        private System.Windows.Forms.TabControl views;
        private System.Windows.Forms.TabPage hlslTab;
        private System.Windows.Forms.TextBox hlslText;
        private System.Windows.Forms.TabPage assemblyTab;
        private System.Windows.Forms.TextBox assemblyText;
        private System.Windows.Forms.Splitter messagesSplitter;
        private System.Windows.Forms.TextBox messagesText;
    }
}
