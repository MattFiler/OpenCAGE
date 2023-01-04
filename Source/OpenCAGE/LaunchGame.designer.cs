namespace OpenCAGE
{
    partial class LaunchGame
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LaunchGame));
            this.OpenGame = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.UIMOD_DebugCheckpoints = new System.Windows.Forms.CheckBox();
            this.UIMOD_MapSelection = new System.Windows.Forms.CheckBox();
            this.UIMOD_MapName = new System.Windows.Forms.CheckBox();
            this.UIMOD_ReturnFrontend = new System.Windows.Forms.CheckBox();
            this.enableCinematicTools = new System.Windows.Forms.CheckBox();
            this.enableUIPerf = new System.Windows.Forms.CheckBox();
            this.enableMemReplayLogs = new System.Windows.Forms.CheckBox();
            this.levelList = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.enableRuntimeUtils = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // OpenGame
            // 
            this.OpenGame.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OpenGame.Location = new System.Drawing.Point(12, 186);
            this.OpenGame.Name = "OpenGame";
            this.OpenGame.Size = new System.Drawing.Size(350, 44);
            this.OpenGame.TabIndex = 22;
            this.OpenGame.Text = "Start Alien: Isolation";
            this.OpenGame.UseVisualStyleBackColor = true;
            this.OpenGame.Click += new System.EventHandler(this.LaunchGame_Click);
            // 
            // UIMOD_DebugCheckpoints
            // 
            this.UIMOD_DebugCheckpoints.AutoSize = true;
            this.UIMOD_DebugCheckpoints.Location = new System.Drawing.Point(11, 22);
            this.UIMOD_DebugCheckpoints.Name = "UIMOD_DebugCheckpoints";
            this.UIMOD_DebugCheckpoints.Size = new System.Drawing.Size(156, 17);
            this.UIMOD_DebugCheckpoints.TabIndex = 23;
            this.UIMOD_DebugCheckpoints.Text = "Enable Debug Checkpoints";
            this.UIMOD_DebugCheckpoints.UseVisualStyleBackColor = true;
            this.UIMOD_DebugCheckpoints.CheckedChanged += new System.EventHandler(this.UIMOD_DebugCheckpoints_CheckedChanged);
            // 
            // UIMOD_MapSelection
            // 
            this.UIMOD_MapSelection.AutoSize = true;
            this.UIMOD_MapSelection.Location = new System.Drawing.Point(11, 45);
            this.UIMOD_MapSelection.Name = "UIMOD_MapSelection";
            this.UIMOD_MapSelection.Size = new System.Drawing.Size(135, 17);
            this.UIMOD_MapSelection.TabIndex = 24;
            this.UIMOD_MapSelection.Text = "Enable Level Selection";
            this.UIMOD_MapSelection.UseVisualStyleBackColor = true;
            this.UIMOD_MapSelection.CheckedChanged += new System.EventHandler(this.UIMOD_MapSelection_CheckedChanged);
            // 
            // UIMOD_MapName
            // 
            this.UIMOD_MapName.AutoSize = true;
            this.UIMOD_MapName.Location = new System.Drawing.Point(11, 68);
            this.UIMOD_MapName.Name = "UIMOD_MapName";
            this.UIMOD_MapName.Size = new System.Drawing.Size(153, 17);
            this.UIMOD_MapName.TabIndex = 25;
            this.UIMOD_MapName.Text = "Enable Debug Loadscreen";
            this.UIMOD_MapName.UseVisualStyleBackColor = true;
            this.UIMOD_MapName.CheckedChanged += new System.EventHandler(this.UIMOD_MapName_CheckedChanged);
            // 
            // UIMOD_ReturnFrontend
            // 
            this.UIMOD_ReturnFrontend.AutoSize = true;
            this.UIMOD_ReturnFrontend.Location = new System.Drawing.Point(11, 91);
            this.UIMOD_ReturnFrontend.Name = "UIMOD_ReturnFrontend";
            this.UIMOD_ReturnFrontend.Size = new System.Drawing.Size(162, 17);
            this.UIMOD_ReturnFrontend.TabIndex = 26;
            this.UIMOD_ReturnFrontend.Text = "Add Quit To Menu On Death";
            this.UIMOD_ReturnFrontend.UseVisualStyleBackColor = true;
            this.UIMOD_ReturnFrontend.CheckedChanged += new System.EventHandler(this.UIMOD_ReturnFrontend_CheckedChanged);
            // 
            // enableCinematicTools
            // 
            this.enableCinematicTools.AutoSize = true;
            this.enableCinematicTools.Location = new System.Drawing.Point(191, 22);
            this.enableCinematicTools.Name = "enableCinematicTools";
            this.enableCinematicTools.Size = new System.Drawing.Size(137, 17);
            this.enableCinematicTools.TabIndex = 27;
            this.enableCinematicTools.Text = "Enable Cinematic Tools";
            this.enableCinematicTools.UseVisualStyleBackColor = true;
            this.enableCinematicTools.CheckedChanged += new System.EventHandler(this.enableCinematicTools_CheckedChanged);
            // 
            // enableUIPerf
            // 
            this.enableUIPerf.AutoSize = true;
            this.enableUIPerf.Location = new System.Drawing.Point(191, 45);
            this.enableUIPerf.Name = "enableUIPerf";
            this.enableUIPerf.Size = new System.Drawing.Size(152, 17);
            this.enableUIPerf.TabIndex = 28;
            this.enableUIPerf.Text = "Enable UI Memory Overlay";
            this.enableUIPerf.UseVisualStyleBackColor = true;
            this.enableUIPerf.CheckedChanged += new System.EventHandler(this.enableUIPerf_CheckedChanged);
            // 
            // enableMemReplayLogs
            // 
            this.enableMemReplayLogs.AutoSize = true;
            this.enableMemReplayLogs.Enabled = false;
            this.enableMemReplayLogs.Location = new System.Drawing.Point(191, 68);
            this.enableMemReplayLogs.Name = "enableMemReplayLogs";
            this.enableMemReplayLogs.Size = new System.Drawing.Size(140, 17);
            this.enableMemReplayLogs.TabIndex = 29;
            this.enableMemReplayLogs.Text = "Enable Memory Logging";
            this.enableMemReplayLogs.UseVisualStyleBackColor = true;
            this.enableMemReplayLogs.CheckedChanged += new System.EventHandler(this.enableMemReplayLogs_CheckedChanged);
            // 
            // levelList
            // 
            this.levelList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.levelList.FormattingEnabled = true;
            this.levelList.Location = new System.Drawing.Point(12, 30);
            this.levelList.Name = "levelList";
            this.levelList.Size = new System.Drawing.Size(350, 21);
            this.levelList.TabIndex = 30;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.enableRuntimeUtils);
            this.groupBox1.Controls.Add(this.UIMOD_DebugCheckpoints);
            this.groupBox1.Controls.Add(this.UIMOD_MapSelection);
            this.groupBox1.Controls.Add(this.UIMOD_MapName);
            this.groupBox1.Controls.Add(this.enableMemReplayLogs);
            this.groupBox1.Controls.Add(this.UIMOD_ReturnFrontend);
            this.groupBox1.Controls.Add(this.enableUIPerf);
            this.groupBox1.Controls.Add(this.enableCinematicTools);
            this.groupBox1.Location = new System.Drawing.Point(12, 61);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(350, 118);
            this.groupBox1.TabIndex = 31;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Launch Options";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(72, 13);
            this.label1.TabIndex = 32;
            this.label1.Text = "Starting Level";
            // 
            // enableRuntimeUtils
            // 
            this.enableRuntimeUtils.AutoSize = true;
            this.enableRuntimeUtils.Enabled = false;
            this.enableRuntimeUtils.Location = new System.Drawing.Point(191, 91);
            this.enableRuntimeUtils.Name = "enableRuntimeUtils";
            this.enableRuntimeUtils.Size = new System.Drawing.Size(130, 17);
            this.enableRuntimeUtils.TabIndex = 30;
            this.enableRuntimeUtils.Text = "Enable Hot Reloading";
            this.toolTip1.SetToolTip(this.enableRuntimeUtils, "Press INSERT in-game to hot reload the current level.");
            this.enableRuntimeUtils.UseVisualStyleBackColor = true;
            this.enableRuntimeUtils.CheckedChanged += new System.EventHandler(this.enableRuntimeUtils_CheckedChanged);
            // 
            // LaunchGame
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(374, 241);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.levelList);
            this.Controls.Add(this.OpenGame);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "LaunchGame";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Launch Game";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button OpenGame;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.CheckBox UIMOD_DebugCheckpoints;
        private System.Windows.Forms.CheckBox UIMOD_MapSelection;
        private System.Windows.Forms.CheckBox UIMOD_MapName;
        private System.Windows.Forms.CheckBox UIMOD_ReturnFrontend;
        private System.Windows.Forms.CheckBox enableCinematicTools;
        private System.Windows.Forms.CheckBox enableUIPerf;
        private System.Windows.Forms.CheckBox enableMemReplayLogs;
        private System.Windows.Forms.ComboBox levelList;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox enableRuntimeUtils;
    }
}