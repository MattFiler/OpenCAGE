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
            this.enableHotReload = new System.Windows.Forms.CheckBox();
            this.enableCinematicTools = new System.Windows.Forms.CheckBox();
            this.enableUIPerf = new System.Windows.Forms.CheckBox();
            this.enableMemReplayLogs = new System.Windows.Forms.CheckBox();
            this.disableUI = new System.Windows.Forms.CheckBox();
            this.skipFrontend = new System.Windows.Forms.CheckBox();
            this.patchCurrentGen = new System.Windows.Forms.CheckBox();
            this.UIMOD_DebugCheckpoints = new System.Windows.Forms.CheckBox();
            this.UIMOD_MapSelection = new System.Windows.Forms.CheckBox();
            this.UIMOD_MapName = new System.Windows.Forms.CheckBox();
            this.UIMOD_ReturnFrontend = new System.Windows.Forms.CheckBox();
            this.levelList = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.hotReloadKey = new System.Windows.Forms.ComboBox();
            this.enableDebugText = new System.Windows.Forms.CheckBox();
            this.loadToLevel = new System.Windows.Forms.CheckBox();
            this.renderConstantAmbient = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // OpenGame
            // 
            this.OpenGame.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OpenGame.Location = new System.Drawing.Point(15, 283);
            this.OpenGame.Name = "OpenGame";
            this.OpenGame.Size = new System.Drawing.Size(350, 44);
            this.OpenGame.TabIndex = 22;
            this.OpenGame.Text = "Start Alien: Isolation";
            this.OpenGame.UseVisualStyleBackColor = true;
            this.OpenGame.Click += new System.EventHandler(this.LaunchGame_Click);
            // 
            // enableHotReload
            // 
            this.enableHotReload.AutoSize = true;
            this.enableHotReload.Enabled = false;
            this.enableHotReload.Location = new System.Drawing.Point(11, 20);
            this.enableHotReload.Name = "enableHotReload";
            this.enableHotReload.Size = new System.Drawing.Size(130, 17);
            this.enableHotReload.TabIndex = 30;
            this.enableHotReload.Text = "Enable Hot Reload";
            this.toolTip1.SetToolTip(this.enableHotReload, "Press the chosen key in-game to reload the current level.");
            this.enableHotReload.UseVisualStyleBackColor = true;
            this.enableHotReload.CheckedChanged += new System.EventHandler(this.enableHotReload_CheckedChanged);
            // 
            // enableCinematicTools
            // 
            this.enableCinematicTools.AutoSize = true;
            this.enableCinematicTools.Location = new System.Drawing.Point(191, 68);
            this.enableCinematicTools.Name = "enableCinematicTools";
            this.enableCinematicTools.Size = new System.Drawing.Size(137, 17);
            this.enableCinematicTools.TabIndex = 27;
            this.enableCinematicTools.Text = "Enable Cinematic Tools";
            this.toolTip1.SetToolTip(this.enableCinematicTools, "Enables HattiWatti\'s Cinematic Tools.");
            this.enableCinematicTools.UseVisualStyleBackColor = true;
            this.enableCinematicTools.CheckedChanged += new System.EventHandler(this.enableCinematicTools_CheckedChanged);
            // 
            // enableUIPerf
            // 
            this.enableUIPerf.AutoSize = true;
            this.enableUIPerf.Location = new System.Drawing.Point(191, 22);
            this.enableUIPerf.Name = "enableUIPerf";
            this.enableUIPerf.Size = new System.Drawing.Size(152, 17);
            this.enableUIPerf.TabIndex = 28;
            this.enableUIPerf.Text = "Enable UI Memory Overlay";
            this.toolTip1.SetToolTip(this.enableUIPerf, "Displays current UI memory in an in-game overlay.");
            this.enableUIPerf.UseVisualStyleBackColor = true;
            this.enableUIPerf.CheckedChanged += new System.EventHandler(this.enableUIPerf_CheckedChanged);
            // 
            // enableMemReplayLogs
            // 
            this.enableMemReplayLogs.AutoSize = true;
            this.enableMemReplayLogs.Location = new System.Drawing.Point(191, 45);
            this.enableMemReplayLogs.Name = "enableMemReplayLogs";
            this.enableMemReplayLogs.Size = new System.Drawing.Size(140, 17);
            this.enableMemReplayLogs.TabIndex = 29;
            this.enableMemReplayLogs.Text = "Enable Memory Logging";
            this.toolTip1.SetToolTip(this.enableMemReplayLogs, "Enables memory replay logs, saved to the Mem_Replay_Logs folder.");
            this.enableMemReplayLogs.UseVisualStyleBackColor = true;
            this.enableMemReplayLogs.CheckedChanged += new System.EventHandler(this.enableMemReplayLogs_CheckedChanged);
            // 
            // disableUI
            // 
            this.disableUI.AutoSize = true;
            this.disableUI.Location = new System.Drawing.Point(191, 91);
            this.disableUI.Name = "disableUI";
            this.disableUI.Size = new System.Drawing.Size(54, 17);
            this.disableUI.TabIndex = 31;
            this.disableUI.Text = "No UI";
            this.toolTip1.SetToolTip(this.disableUI, "Enabling this will disable the in-game HUD. Be aware that this also disables UI p" +
        "opups, so may make some actions impossible.");
            this.disableUI.UseVisualStyleBackColor = true;
            this.disableUI.CheckedChanged += new System.EventHandler(this.disableUI_CheckedChanged);
            // 
            // skipFrontend
            // 
            this.skipFrontend.AutoSize = true;
            this.skipFrontend.Location = new System.Drawing.Point(11, 114);
            this.skipFrontend.Name = "skipFrontend";
            this.skipFrontend.Size = new System.Drawing.Size(92, 17);
            this.skipFrontend.TabIndex = 32;
            this.skipFrontend.Text = "Skip Frontend";
            this.toolTip1.SetToolTip(this.skipFrontend, "Enabling this will skip the title screen when loading directly to a level.");
            this.skipFrontend.UseVisualStyleBackColor = true;
            this.skipFrontend.CheckedChanged += new System.EventHandler(this.skipFrontend_CheckedChanged);
            // 
            // patchCurrentGen
            // 
            this.patchCurrentGen.AutoSize = true;
            this.patchCurrentGen.Location = new System.Drawing.Point(11, 137);
            this.patchCurrentGen.Name = "patchCurrentGen";
            this.patchCurrentGen.Size = new System.Drawing.Size(179, 17);
            this.patchCurrentGen.TabIndex = 33;
            this.patchCurrentGen.Text = "Patch Current-Gen Optimisations";
            this.toolTip1.SetToolTip(this.patchCurrentGen, "Enabling this will patch current-gen script optimisations to enable functionality" +
        " previously disabled in the PC build, such as additional dynamic lighting.");
            this.patchCurrentGen.UseVisualStyleBackColor = true;
            this.patchCurrentGen.CheckedChanged += new System.EventHandler(this.patchCurrentGen_CheckedChanged);
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
            // levelList
            // 
            this.levelList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.levelList.Enabled = false;
            this.levelList.FormattingEnabled = true;
            this.levelList.Location = new System.Drawing.Point(12, 30);
            this.levelList.Name = "levelList";
            this.levelList.Size = new System.Drawing.Size(350, 21);
            this.levelList.TabIndex = 30;
            this.levelList.SelectedIndexChanged += new System.EventHandler(this.levelList_SelectedIndexChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.renderConstantAmbient);
            this.groupBox1.Controls.Add(this.patchCurrentGen);
            this.groupBox1.Controls.Add(this.skipFrontend);
            this.groupBox1.Controls.Add(this.disableUI);
            this.groupBox1.Controls.Add(this.UIMOD_DebugCheckpoints);
            this.groupBox1.Controls.Add(this.UIMOD_MapSelection);
            this.groupBox1.Controls.Add(this.UIMOD_MapName);
            this.groupBox1.Controls.Add(this.enableMemReplayLogs);
            this.groupBox1.Controls.Add(this.UIMOD_ReturnFrontend);
            this.groupBox1.Controls.Add(this.enableUIPerf);
            this.groupBox1.Controls.Add(this.enableCinematicTools);
            this.groupBox1.Location = new System.Drawing.Point(12, 61);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(350, 163);
            this.groupBox1.TabIndex = 31;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Launch Options";
            //
            // loadToLevel
            //
            this.loadToLevel.AutoSize = true;
            this.loadToLevel.Location = new System.Drawing.Point(12, 9);
            this.loadToLevel.Name = "loadToLevel";
            this.loadToLevel.Size = new System.Drawing.Size(140, 17);
            this.loadToLevel.TabIndex = 32;
            this.loadToLevel.Text = "Load straight into a level";
            this.toolTip1.SetToolTip(this.loadToLevel, "Unchecked: the game starts at its main menu (FRONTEND). Checked: the game boots dir" +
        "ectly into the selected level.");
            this.loadToLevel.UseVisualStyleBackColor = true;
            this.loadToLevel.CheckedChanged += new System.EventHandler(this.loadToLevel_CheckedChanged);
            // 
            // renderConstantAmbient
            // 
            this.renderConstantAmbient.AutoSize = true;
            this.renderConstantAmbient.Location = new System.Drawing.Point(191, 114);
            this.renderConstantAmbient.Name = "renderConstantAmbient";
            this.renderConstantAmbient.Size = new System.Drawing.Size(147, 17);
            this.renderConstantAmbient.TabIndex = 34;
            this.renderConstantAmbient.Text = "Render Constant Ambient";
            this.toolTip1.SetToolTip(this.renderConstantAmbient, "Enabling this will render in \'constant ambient\' mode (essentially full-bright).");
            this.renderConstantAmbient.UseVisualStyleBackColor = true;
            this.renderConstantAmbient.CheckedChanged += new System.EventHandler(this.renderConstantAmbient_CheckedChanged);
            //
            // hotReloadKey
            //
            this.hotReloadKey.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.hotReloadKey.Enabled = false;
            this.hotReloadKey.FormattingEnabled = true;
            this.hotReloadKey.Location = new System.Drawing.Point(130, 17);
            this.hotReloadKey.Name = "hotReloadKey";
            this.hotReloadKey.Size = new System.Drawing.Size(85, 21);
            this.hotReloadKey.TabIndex = 36;
            this.toolTip1.SetToolTip(this.hotReloadKey, "The key that reloads the current level.");
            this.hotReloadKey.SelectedIndexChanged += new System.EventHandler(this.hotReloadKey_SelectedIndexChanged);
            //
            // enableDebugText
            //
            this.enableDebugText.AutoSize = true;
            this.enableDebugText.Enabled = false;
            this.enableDebugText.Location = new System.Drawing.Point(228, 20);
            this.enableDebugText.Name = "enableDebugText";
            this.enableDebugText.Size = new System.Drawing.Size(114, 17);
            this.enableDebugText.TabIndex = 37;
            this.enableDebugText.Text = "Enable Debug Text";
            this.toolTip1.SetToolTip(this.enableDebugText, "Draws DebugText and DebugTextStacking script entities on screen in-game.");
            this.enableDebugText.UseVisualStyleBackColor = true;
            this.enableDebugText.CheckedChanged += new System.EventHandler(this.enableDebugText_CheckedChanged);
            //
            // groupBox2
            //
            this.groupBox2.Controls.Add(this.enableHotReload);
            this.groupBox2.Controls.Add(this.hotReloadKey);
            this.groupBox2.Controls.Add(this.enableDebugText);
            this.groupBox2.Location = new System.Drawing.Point(12, 230);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(350, 47);
            this.groupBox2.TabIndex = 35;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Scripting Helpers";
            //
            // LaunchGame
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(374, 341);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.loadToLevel);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.levelList);
            this.Controls.Add(this.OpenGame);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "LaunchGame";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Launch Game";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
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
        private System.Windows.Forms.CheckBox loadToLevel;
        private System.Windows.Forms.CheckBox enableHotReload;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ComboBox hotReloadKey;
        private System.Windows.Forms.CheckBox enableDebugText;
        private System.Windows.Forms.CheckBox disableUI;
        private System.Windows.Forms.CheckBox skipFrontend;
        private System.Windows.Forms.CheckBox patchCurrentGen;
        private System.Windows.Forms.CheckBox renderConstantAmbient;
    }
}
