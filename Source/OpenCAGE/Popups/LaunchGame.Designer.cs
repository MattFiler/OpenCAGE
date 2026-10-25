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
            this.loadToLevel = new System.Windows.Forms.CheckBox();
            this.levelList = new System.Windows.Forms.ComboBox();
            this.enableDebugMenu = new System.Windows.Forms.CheckBox();
            this.debugMenuKey = new System.Windows.Forms.ComboBox();
            this.dontWriteSaves = new System.Windows.Forms.CheckBox();
            this.enableDebugEntities = new System.Windows.Forms.CheckBox();
            this.enableHotReload = new System.Windows.Forms.CheckBox();
            this.hotReloadKey = new System.Windows.Forms.ComboBox();
            this.enableLiveLink = new System.Windows.Forms.CheckBox();
            this.enableErrorLog = new System.Windows.Forms.CheckBox();
            this.renderConstantAmbient = new System.Windows.Forms.CheckBox();
            this.disableUI = new System.Windows.Forms.CheckBox();
            this.patchCurrentGen = new System.Windows.Forms.CheckBox();
            this.enableCapFps = new System.Windows.Forms.CheckBox();
            this.capFpsRate = new System.Windows.Forms.ComboBox();
            this.enableUnlockMouse = new System.Windows.Forms.CheckBox();
            this.unlockMouseKey = new System.Windows.Forms.ComboBox();
            this.enablePossess = new System.Windows.Forms.CheckBox();
            this.possessKey = new System.Windows.Forms.ComboBox();
            this.debugCheckpoints = new System.Windows.Forms.CheckBox();
            this.levelSelect = new System.Windows.Forms.CheckBox();
            this.quitToMenuOnDeath = new System.Windows.Forms.CheckBox();
            this.debugLoadingScreen = new System.Windows.Forms.CheckBox();
            this.debugToolsGroup = new System.Windows.Forms.GroupBox();
            this.gameOptionsGroup = new System.Windows.Forms.GroupBox();
            this.uiTweaksGroup = new System.Windows.Forms.GroupBox();
            this.debugToolsGroup.SuspendLayout();
            this.gameOptionsGroup.SuspendLayout();
            this.uiTweaksGroup.SuspendLayout();
            this.SuspendLayout();
            //
            // OpenGame
            //
            this.OpenGame.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OpenGame.Location = new System.Drawing.Point(12, 314);
            this.OpenGame.Name = "OpenGame";
            this.OpenGame.Size = new System.Drawing.Size(436, 40);
            this.OpenGame.TabIndex = 5;
            this.OpenGame.Text = "Start Alien: Isolation";
            this.toolTip1.SetToolTip(this.OpenGame, "Start the game with the options above. It loads levels as they are saved on disk: unsaved changes are offere" +
        "d a save first. The Debug Menu, Possess characters, Debug entities, Hot reload, Live Link, Cap FPS and Unlock mo" +
        "use need an ASI loader in the game folder as d3d11.dll: another mod\'s d3d11.dll there (ReShade, Special K...) i" +
        "s moved to d3d11.dll.opencage_backup while any of them is on, and put back when the game is next started from he" +
        "re with them all off.");
            this.OpenGame.UseVisualStyleBackColor = true;
            this.OpenGame.Click += new System.EventHandler(this.LaunchGame_Click);
            //
            // loadToLevel
            //
            this.loadToLevel.AutoSize = true;
            this.loadToLevel.Location = new System.Drawing.Point(12, 14);
            this.loadToLevel.Name = "loadToLevel";
            this.loadToLevel.Size = new System.Drawing.Size(110, 17);
            this.loadToLevel.TabIndex = 0;
            this.loadToLevel.Text = "Load straight into";
            this.toolTip1.SetToolTip(this.loadToLevel, "Ticked: the game boots straight into the chosen level and skips the title screen. Unticked: the game starts a" +
        "t its main menu.");
            this.loadToLevel.UseVisualStyleBackColor = true;
            this.loadToLevel.CheckedChanged += new System.EventHandler(this.loadToLevel_CheckedChanged);
            //
            // levelList
            //
            this.levelList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.levelList.Enabled = false;
            this.levelList.FormattingEnabled = true;
            this.levelList.Location = new System.Drawing.Point(126, 12);
            this.levelList.Name = "levelList";
            this.levelList.Size = new System.Drawing.Size(322, 21);
            this.levelList.TabIndex = 1;
            this.toolTip1.SetToolTip(this.levelList, "The level the game boots straight into.");
            this.levelList.SelectedIndexChanged += new System.EventHandler(this.levelList_SelectedIndexChanged);
            //
            // enableDebugMenu
            //
            this.enableDebugMenu.AutoSize = true;
            this.enableDebugMenu.Enabled = false;
            this.enableDebugMenu.Location = new System.Drawing.Point(10, 19);
            this.enableDebugMenu.Name = "enableDebugMenu";
            this.enableDebugMenu.Size = new System.Drawing.Size(88, 17);
            this.enableDebugMenu.TabIndex = 0;
            this.enableDebugMenu.Text = "Debug Menu";
            this.toolTip1.SetToolTip(this.enableDebugMenu, "An in-game debug menu: press the chosen key to open it. See what each character senses, thinks and is doing (i" +
        "ts behaviour tree, view cones, path, suspicion), flip the behaviour trees\' debug switches, rendering switches and" +
        " more. Cinematic Tools are part of it now, under Cinematic (their free camera, camera tracks and visuals), and s" +
        "o is the UI memory overlay (Performance > Memory). It has debug checkpoints too (Game > Tools), and loads any le" +
        "vel (Game > Game flow).");
            this.enableDebugMenu.UseVisualStyleBackColor = true;
            this.enableDebugMenu.CheckedChanged += new System.EventHandler(this.enableDebugMenu_CheckedChanged);
            //
            // debugMenuKey
            //
            this.debugMenuKey.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.debugMenuKey.Enabled = false;
            this.debugMenuKey.FormattingEnabled = true;
            this.debugMenuKey.Location = new System.Drawing.Point(102, 17);
            this.debugMenuKey.Name = "debugMenuKey";
            this.debugMenuKey.Size = new System.Drawing.Size(86, 21);
            this.debugMenuKey.TabIndex = 1;
            this.toolTip1.SetToolTip(this.debugMenuKey, "The key that opens and closes the in-game debug menu.");
            this.debugMenuKey.SelectedIndexChanged += new System.EventHandler(this.debugMenuKey_SelectedIndexChanged);
            //
            // dontWriteSaves
            //
            this.dontWriteSaves.AutoSize = true;
            this.dontWriteSaves.Enabled = false;
            this.dontWriteSaves.Location = new System.Drawing.Point(26, 42);
            this.dontWriteSaves.Name = "dontWriteSaves";
            this.dontWriteSaves.Size = new System.Drawing.Size(109, 17);
            this.dontWriteSaves.TabIndex = 2;
            this.dontWriteSaves.Text = "Don\'t write saves";
            this.toolTip1.SetToolTip(this.dontWriteSaves, "The game writes nothing to your saved games while it runs (its saves, progress and settings), so testing a lev" +
        "el never changes them. Part of the Debug Menu: tick Debug Menu to use it. The Debug Menu can switch it in-game to" +
        "o.");
            this.dontWriteSaves.UseVisualStyleBackColor = true;
            this.dontWriteSaves.CheckedChanged += new System.EventHandler(this.dontWriteSaves_CheckedChanged);
            //
            // enableDebugEntities
            //
            this.enableDebugEntities.AutoSize = true;
            this.enableDebugEntities.Enabled = false;
            this.enableDebugEntities.Location = new System.Drawing.Point(236, 19);
            this.enableDebugEntities.Name = "enableDebugEntities";
            this.enableDebugEntities.Size = new System.Drawing.Size(96, 17);
            this.enableDebugEntities.TabIndex = 5;
            this.enableDebugEntities.Text = "Debug entities";
            this.toolTip1.SetToolTip(this.enableDebugEntities, "Debug script entities work in game, with or without the Debug Menu: DebugText and DebugTextStacking draw thei" +
        "r text on screen, DebugEnvironmentMarker draws its text at a place in the world and DebugPositionMarker draws axes" +
        " there, DebugGraph plots its inputs, DebugObjectMarker names its object, and DebugCaptureScreenShot saves a scree" +
        "nshot. DebugMenuToggle sets Debug Menu switches (with the Debug Menu on).");
            this.enableDebugEntities.UseVisualStyleBackColor = true;
            this.enableDebugEntities.CheckedChanged += new System.EventHandler(this.enableDebugEntities_CheckedChanged);
            //
            // enableHotReload
            //
            this.enableHotReload.AutoSize = true;
            this.enableHotReload.Enabled = false;
            this.enableHotReload.Location = new System.Drawing.Point(10, 65);
            this.enableHotReload.Name = "enableHotReload";
            this.enableHotReload.Size = new System.Drawing.Size(75, 17);
            this.enableHotReload.TabIndex = 3;
            this.enableHotReload.Text = "Hot reload";
            this.toolTip1.SetToolTip(this.enableHotReload, "Press the chosen key in-game to reload the current level.");
            this.enableHotReload.UseVisualStyleBackColor = true;
            this.enableHotReload.CheckedChanged += new System.EventHandler(this.enableHotReload_CheckedChanged);
            //
            // hotReloadKey
            //
            this.hotReloadKey.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.hotReloadKey.Enabled = false;
            this.hotReloadKey.FormattingEnabled = true;
            this.hotReloadKey.Location = new System.Drawing.Point(102, 63);
            this.hotReloadKey.Name = "hotReloadKey";
            this.hotReloadKey.Size = new System.Drawing.Size(86, 21);
            this.hotReloadKey.TabIndex = 4;
            this.toolTip1.SetToolTip(this.hotReloadKey, "The key that reloads the current level.");
            this.hotReloadKey.SelectedIndexChanged += new System.EventHandler(this.hotReloadKey_SelectedIndexChanged);
            //
            // enableLiveLink
            //
            this.enableLiveLink.AutoSize = true;
            this.enableLiveLink.Enabled = false;
            this.enableLiveLink.Location = new System.Drawing.Point(236, 42);
            this.enableLiveLink.Name = "enableLiveLink";
            this.enableLiveLink.Size = new System.Drawing.Size(68, 17);
            this.enableLiveLink.TabIndex = 6;
            this.enableLiveLink.Text = "Live Link";
            this.toolTip1.SetToolTip(this.enableLiveLink, "The game serves OpenCAGE\'s Live Link (on this PC only), and OpenCAGE connects to it by itself once the game i" +
        "s up (the Live Link button, beside Launch Game): script edits reach the running level without a reload, entity " +
        "methods can be triggered from the Entity Inspector or a method pin\'s right-click menu, and the viewport\'s camer" +
        "a and the game\'s can follow each other.");
            this.enableLiveLink.UseVisualStyleBackColor = true;
            this.enableLiveLink.CheckedChanged += new System.EventHandler(this.enableLiveLink_CheckedChanged);
            //
            // enableErrorLog
            //
            this.enableErrorLog.AutoSize = true;
            this.enableErrorLog.Enabled = false;
            this.enableErrorLog.Location = new System.Drawing.Point(252, 65);
            this.enableErrorLog.Name = "enableErrorLog";
            this.enableErrorLog.Size = new System.Drawing.Size(68, 17);
            this.enableErrorLog.TabIndex = 7;
            this.enableErrorLog.Text = "Error log";
            this.toolTip1.SetToolTip(this.enableErrorLog, "The Error Log opens as the game starts and lists the level\'s script errors as the game finds them (Live Link): " +
        "double-click one to go to its entity, and flowgraph nodes with errors show a warning sign. Needs Live Link.");
            this.enableErrorLog.UseVisualStyleBackColor = true;
            this.enableErrorLog.CheckedChanged += new System.EventHandler(this.enableErrorLog_CheckedChanged);
            //
            // renderConstantAmbient
            //
            this.renderConstantAmbient.AutoSize = true;
            this.renderConstantAmbient.Location = new System.Drawing.Point(236, 19);
            this.renderConstantAmbient.Name = "renderConstantAmbient";
            this.renderConstantAmbient.Size = new System.Drawing.Size(151, 17);
            this.renderConstantAmbient.TabIndex = 6;
            this.renderConstantAmbient.Text = "Constant ambient lighting";
            this.toolTip1.SetToolTip(this.renderConstantAmbient, "The game renders in \'constant ambient\' mode: every surface evenly lit (essentially full-bright).");
            this.renderConstantAmbient.UseVisualStyleBackColor = true;
            this.renderConstantAmbient.CheckedChanged += new System.EventHandler(this.renderConstantAmbient_CheckedChanged);
            //
            // disableUI
            //
            this.disableUI.AutoSize = true;
            this.disableUI.Location = new System.Drawing.Point(236, 42);
            this.disableUI.Name = "disableUI";
            this.disableUI.Size = new System.Drawing.Size(65, 17);
            this.disableUI.TabIndex = 7;
            this.disableUI.Text = "No HUD";
            this.toolTip1.SetToolTip(this.disableUI, "The game draws no HUD. This also hides UI popups, so some actions may become impossible.");
            this.disableUI.UseVisualStyleBackColor = true;
            this.disableUI.CheckedChanged += new System.EventHandler(this.disableUI_CheckedChanged);
            //
            // patchCurrentGen
            //
            this.patchCurrentGen.AutoSize = true;
            this.patchCurrentGen.Location = new System.Drawing.Point(236, 65);
            this.patchCurrentGen.Name = "patchCurrentGen";
            this.patchCurrentGen.Size = new System.Drawing.Size(140, 17);
            this.patchCurrentGen.TabIndex = 8;
            this.patchCurrentGen.Text = "Current-gen script fixes";
            this.toolTip1.SetToolTip(this.patchCurrentGen, "Patches out the current-gen script optimisations, so functionality they leave disabled in the PC build (such a" +
        "s additional dynamic lighting) runs. On by default: edited levels need it.");
            this.patchCurrentGen.UseVisualStyleBackColor = true;
            this.patchCurrentGen.CheckedChanged += new System.EventHandler(this.patchCurrentGen_CheckedChanged);
            //
            // enableCapFps
            //
            this.enableCapFps.AutoSize = true;
            this.enableCapFps.Enabled = false;
            this.enableCapFps.Location = new System.Drawing.Point(10, 19);
            this.enableCapFps.Name = "enableCapFps";
            this.enableCapFps.Size = new System.Drawing.Size(69, 17);
            this.enableCapFps.TabIndex = 0;
            this.enableCapFps.Text = "Cap FPS";
            this.toolTip1.SetToolTip(this.enableCapFps, "The game runs at most the chosen frames a second. Above 360 it runs too fast (a near-empty level with V-Sync" +
        " off, say), so this keeps it at its proper speed.");
            this.enableCapFps.UseVisualStyleBackColor = true;
            this.enableCapFps.CheckedChanged += new System.EventHandler(this.enableCapFps_CheckedChanged);
            //
            // capFpsRate
            //
            this.capFpsRate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.capFpsRate.Enabled = false;
            this.capFpsRate.FormattingEnabled = true;
            this.capFpsRate.Location = new System.Drawing.Point(138, 17);
            this.capFpsRate.Name = "capFpsRate";
            this.capFpsRate.Size = new System.Drawing.Size(86, 21);
            this.capFpsRate.TabIndex = 1;
            this.toolTip1.SetToolTip(this.capFpsRate, "The most frames a second the game runs at.");
            this.capFpsRate.SelectedIndexChanged += new System.EventHandler(this.capFpsRate_SelectedIndexChanged);
            //
            // enableUnlockMouse
            //
            this.enableUnlockMouse.AutoSize = true;
            this.enableUnlockMouse.Enabled = false;
            this.enableUnlockMouse.Location = new System.Drawing.Point(10, 42);
            this.enableUnlockMouse.Name = "enableUnlockMouse";
            this.enableUnlockMouse.Size = new System.Drawing.Size(95, 17);
            this.enableUnlockMouse.TabIndex = 2;
            this.enableUnlockMouse.Text = "Unlock mouse";
            this.toolTip1.SetToolTip(this.enableUnlockMouse, "The game leaves your mouse cursor free, so you can work in OpenCAGE beside it (handy with Live Link\'s came" +
        "ra sync). To look around in-game, click the game window and press the chosen key; press it again to free the" +
        " cursor.");
            this.enableUnlockMouse.UseVisualStyleBackColor = true;
            this.enableUnlockMouse.CheckedChanged += new System.EventHandler(this.enableUnlockMouse_CheckedChanged);
            //
            // unlockMouseKey
            //
            this.unlockMouseKey.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.unlockMouseKey.Enabled = false;
            this.unlockMouseKey.FormattingEnabled = true;
            this.unlockMouseKey.Location = new System.Drawing.Point(138, 40);
            this.unlockMouseKey.Name = "unlockMouseKey";
            this.unlockMouseKey.Size = new System.Drawing.Size(86, 21);
            this.unlockMouseKey.TabIndex = 3;
            this.toolTip1.SetToolTip(this.unlockMouseKey, "The key that gives the game the mouse to look around with, and frees the cursor again.");
            this.unlockMouseKey.SelectedIndexChanged += new System.EventHandler(this.unlockMouseKey_SelectedIndexChanged);
            //
            // enablePossess
            //
            this.enablePossess.AutoSize = true;
            this.enablePossess.Enabled = false;
            this.enablePossess.Location = new System.Drawing.Point(10, 65);
            this.enablePossess.Name = "enablePossess";
            this.enablePossess.Size = new System.Drawing.Size(122, 17);
            this.enablePossess.TabIndex = 4;
            this.enablePossess.Text = "Possess characters";
            this.toolTip1.SetToolTip(this.enablePossess, "Take control of a character (the alien, an android, a human) in-game: press the chosen key to open a menu of " +
        "the characters around you and pick one. W/A/S/D move it, Shift faster, Ctrl slower, C crouches, the mouse turns the camera behind it and the wheel zooms it.");
            this.enablePossess.UseVisualStyleBackColor = true;
            this.enablePossess.CheckedChanged += new System.EventHandler(this.enablePossess_CheckedChanged);
            //
            // possessKey
            //
            this.possessKey.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.possessKey.Enabled = false;
            this.possessKey.FormattingEnabled = true;
            this.possessKey.Location = new System.Drawing.Point(138, 63);
            this.possessKey.Name = "possessKey";
            this.possessKey.Size = new System.Drawing.Size(86, 21);
            this.possessKey.TabIndex = 5;
            this.toolTip1.SetToolTip(this.possessKey, "The key that opens and closes the in-game menu of characters to possess.");
            this.possessKey.SelectedIndexChanged += new System.EventHandler(this.possessKey_SelectedIndexChanged);
            //
            // debugCheckpoints
            //
            this.debugCheckpoints.AutoSize = true;
            this.debugCheckpoints.Location = new System.Drawing.Point(10, 19);
            this.debugCheckpoints.Name = "debugCheckpoints";
            this.debugCheckpoints.Size = new System.Drawing.Size(196, 17);
            this.debugCheckpoints.TabIndex = 0;
            this.debugCheckpoints.Text = "Debug Checkpoints (in Pause Menu)";
            this.toolTip1.SetToolTip(this.debugCheckpoints, "Adds the level\'s debug checkpoints to the pause menu: pick one to jump to it. The Debug Menu\'s Game > Tools ta" +
        "b has them too.");
            this.debugCheckpoints.UseVisualStyleBackColor = true;
            this.debugCheckpoints.CheckedChanged += new System.EventHandler(this.debugCheckpoints_CheckedChanged);
            //
            // levelSelect
            //
            this.levelSelect.AutoSize = true;
            this.levelSelect.Location = new System.Drawing.Point(236, 19);
            this.levelSelect.Name = "levelSelect";
            this.levelSelect.Size = new System.Drawing.Size(158, 17);
            this.levelSelect.TabIndex = 1;
            this.levelSelect.Text = "Level Select (in Main Menu)";
            this.toolTip1.SetToolTip(this.levelSelect, "Adds a level selection to the main menu: pick any level, your own included, to load it. The Debug Menu\'s Game" +
        " > Game flow tab can load levels too.");
            this.levelSelect.UseVisualStyleBackColor = true;
            this.levelSelect.CheckedChanged += new System.EventHandler(this.levelSelect_CheckedChanged);
            //
            // debugLoadingScreen
            //
            this.debugLoadingScreen.AutoSize = true;
            this.debugLoadingScreen.Location = new System.Drawing.Point(10, 42);
            this.debugLoadingScreen.Name = "debugLoadingScreen";
            this.debugLoadingScreen.Size = new System.Drawing.Size(119, 17);
            this.debugLoadingScreen.TabIndex = 2;
            this.debugLoadingScreen.Text = "Quicker Loadscreen";
            this.toolTip1.SetToolTip(this.debugLoadingScreen, "OpenCAGE\'s quicker loading screen, which shows the name of the level being loaded.");
            this.debugLoadingScreen.UseVisualStyleBackColor = true;
            this.debugLoadingScreen.CheckedChanged += new System.EventHandler(this.debugLoadingScreen_CheckedChanged);
            //
            // quitToMenuOnDeath
            //
            this.quitToMenuOnDeath.AutoSize = true;
            this.quitToMenuOnDeath.Location = new System.Drawing.Point(236, 42);
            this.quitToMenuOnDeath.Name = "quitToMenuOnDeath";
            this.quitToMenuOnDeath.Size = new System.Drawing.Size(160, 17);
            this.quitToMenuOnDeath.TabIndex = 3;
            this.quitToMenuOnDeath.Text = "Quit Option On Deathscreen";
            this.toolTip1.SetToolTip(this.quitToMenuOnDeath, "Adds a Quit To Menu option to the game-over screen, so a death can go back to the main menu.");
            this.quitToMenuOnDeath.UseVisualStyleBackColor = true;
            this.quitToMenuOnDeath.CheckedChanged += new System.EventHandler(this.quitToMenuOnDeath_CheckedChanged);
            //
            // debugToolsGroup
            //
            this.debugToolsGroup.Controls.Add(this.enableDebugMenu);
            this.debugToolsGroup.Controls.Add(this.debugMenuKey);
            this.debugToolsGroup.Controls.Add(this.dontWriteSaves);
            this.debugToolsGroup.Controls.Add(this.enableDebugEntities);
            this.debugToolsGroup.Controls.Add(this.enableHotReload);
            this.debugToolsGroup.Controls.Add(this.hotReloadKey);
            this.debugToolsGroup.Controls.Add(this.enableLiveLink);
            this.debugToolsGroup.Controls.Add(this.enableErrorLog);
            this.debugToolsGroup.Location = new System.Drawing.Point(12, 41);
            this.debugToolsGroup.Name = "debugToolsGroup";
            this.debugToolsGroup.Size = new System.Drawing.Size(436, 92);
            this.debugToolsGroup.TabIndex = 2;
            this.debugToolsGroup.TabStop = false;
            this.debugToolsGroup.Text = "Debug tools";
            //
            // gameOptionsGroup
            //
            this.gameOptionsGroup.Controls.Add(this.renderConstantAmbient);
            this.gameOptionsGroup.Controls.Add(this.disableUI);
            this.gameOptionsGroup.Controls.Add(this.patchCurrentGen);
            this.gameOptionsGroup.Controls.Add(this.enableCapFps);
            this.gameOptionsGroup.Controls.Add(this.capFpsRate);
            this.gameOptionsGroup.Controls.Add(this.enableUnlockMouse);
            this.gameOptionsGroup.Controls.Add(this.unlockMouseKey);
            this.gameOptionsGroup.Controls.Add(this.enablePossess);
            this.gameOptionsGroup.Controls.Add(this.possessKey);
            this.gameOptionsGroup.Location = new System.Drawing.Point(12, 139);
            this.gameOptionsGroup.Name = "gameOptionsGroup";
            this.gameOptionsGroup.Size = new System.Drawing.Size(436, 92);
            this.gameOptionsGroup.TabIndex = 3;
            this.gameOptionsGroup.TabStop = false;
            this.gameOptionsGroup.Text = "Game options";
            //
            // uiTweaksGroup
            //
            this.uiTweaksGroup.Controls.Add(this.debugCheckpoints);
            this.uiTweaksGroup.Controls.Add(this.levelSelect);
            this.uiTweaksGroup.Controls.Add(this.debugLoadingScreen);
            this.uiTweaksGroup.Controls.Add(this.quitToMenuOnDeath);
            this.uiTweaksGroup.Location = new System.Drawing.Point(12, 237);
            this.uiTweaksGroup.Name = "uiTweaksGroup";
            this.uiTweaksGroup.Size = new System.Drawing.Size(436, 69);
            this.uiTweaksGroup.TabIndex = 4;
            this.uiTweaksGroup.TabStop = false;
            this.uiTweaksGroup.Text = "UI tweaks";
            //
            // LaunchGame
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(460, 366);
            this.Controls.Add(this.loadToLevel);
            this.Controls.Add(this.levelList);
            this.Controls.Add(this.debugToolsGroup);
            this.Controls.Add(this.gameOptionsGroup);
            this.Controls.Add(this.uiTweaksGroup);
            this.Controls.Add(this.OpenGame);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.HelpButton = true;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LaunchGame";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Launch Game";
            this.debugToolsGroup.ResumeLayout(false);
            this.debugToolsGroup.PerformLayout();
            this.gameOptionsGroup.ResumeLayout(false);
            this.gameOptionsGroup.PerformLayout();
            this.uiTweaksGroup.ResumeLayout(false);
            this.uiTweaksGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button OpenGame;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.CheckBox loadToLevel;
        private System.Windows.Forms.ComboBox levelList;
        private System.Windows.Forms.GroupBox debugToolsGroup;
        private System.Windows.Forms.CheckBox enableDebugMenu;
        private System.Windows.Forms.ComboBox debugMenuKey;
        private System.Windows.Forms.CheckBox dontWriteSaves;
        private System.Windows.Forms.CheckBox enableDebugEntities;
        private System.Windows.Forms.CheckBox enableHotReload;
        private System.Windows.Forms.ComboBox hotReloadKey;
        private System.Windows.Forms.CheckBox enableLiveLink;
        private System.Windows.Forms.CheckBox enableErrorLog;
        private System.Windows.Forms.GroupBox gameOptionsGroup;
        private System.Windows.Forms.CheckBox renderConstantAmbient;
        private System.Windows.Forms.CheckBox disableUI;
        private System.Windows.Forms.CheckBox patchCurrentGen;
        private System.Windows.Forms.CheckBox enableCapFps;
        private System.Windows.Forms.ComboBox capFpsRate;
        private System.Windows.Forms.CheckBox enableUnlockMouse;
        private System.Windows.Forms.ComboBox unlockMouseKey;
        private System.Windows.Forms.CheckBox enablePossess;
        private System.Windows.Forms.ComboBox possessKey;
        private System.Windows.Forms.GroupBox uiTweaksGroup;
        private System.Windows.Forms.CheckBox debugCheckpoints;
        private System.Windows.Forms.CheckBox levelSelect;
        private System.Windows.Forms.CheckBox quitToMenuOnDeath;
        private System.Windows.Forms.CheckBox debugLoadingScreen;
    }
}
