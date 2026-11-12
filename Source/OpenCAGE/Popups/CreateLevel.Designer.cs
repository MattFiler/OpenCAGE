namespace OpenCAGE
{
    partial class CreateLevel
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
            this.createLevel = new System.Windows.Forms.Button();
            this.levelName = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.importSummary = new System.Windows.Forms.ListBox();
            this.chooseImports = new System.Windows.Forms.Button();
            this.clearImports = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.testRoom = new System.Windows.Forms.CheckBox();
            this.roomLights = new System.Windows.Forms.CheckBox();
            this.roomSizeLabel = new System.Windows.Forms.Label();
            this.roomWidth = new System.Windows.Forms.NumericUpDown();
            this.roomTimes1 = new System.Windows.Forms.Label();
            this.roomLength = new System.Windows.Forms.NumericUpDown();
            this.roomTimes2 = new System.Windows.Forms.Label();
            this.roomHeight = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.roomWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.roomLength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.roomHeight)).BeginInit();
            this.SuspendLayout();
            //
            // createLevel
            //
            this.createLevel.Location = new System.Drawing.Point(324, 232);
            this.createLevel.Name = "createLevel";
            this.createLevel.Size = new System.Drawing.Size(113, 42);
            this.createLevel.TabIndex = 5;
            this.createLevel.Text = "Create Level";
            this.toolTip1.SetToolTip(this.createLevel, "Create the level, import any chosen composites, and open it in the editor.");
            this.createLevel.UseVisualStyleBackColor = true;
            this.createLevel.Click += new System.EventHandler(this.createLevel_Click);
            //
            // levelName
            //
            this.levelName.Location = new System.Drawing.Point(15, 29);
            this.levelName.Name = "levelName";
            this.levelName.Size = new System.Drawing.Size(422, 20);
            this.levelName.TabIndex = 1;
            this.toolTip1.SetToolTip(this.levelName, "Letters, digits and underscores. The level is created at DATA\\ENV\\<name>, starting from the s" +
        "hared data every level carries.");
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(64, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Level name:";
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 58);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(180, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Composites to import (optional):";
            //
            // importSummary
            //
            this.importSummary.FormattingEnabled = true;
            this.importSummary.IntegralHeight = false;
            this.importSummary.Location = new System.Drawing.Point(15, 74);
            this.importSummary.Name = "importSummary";
            this.importSummary.SelectionMode = System.Windows.Forms.SelectionMode.None;
            this.importSummary.Size = new System.Drawing.Size(422, 120);
            this.importSummary.TabIndex = 3;
            this.toolTip1.SetToolTip(this.importSummary, "Composites (and the models, materials, textures, collision and physics they use) that will be por" +
        "ted into the new level. Nothing is placed in the world - the content is there to build with. GLOB" +
        "AL, PAUSEMENU and the required assets always come along.");
            //
            // chooseImports
            //
            this.chooseImports.Location = new System.Drawing.Point(15, 200);
            this.chooseImports.Name = "chooseImports";
            this.chooseImports.Size = new System.Drawing.Size(120, 23);
            this.chooseImports.TabIndex = 4;
            this.chooseImports.Text = "Choose...";
            this.toolTip1.SetToolTip(this.chooseImports, "Browse every level and tick the composites to bring into the new level.");
            this.chooseImports.UseVisualStyleBackColor = true;
            this.chooseImports.Click += new System.EventHandler(this.chooseImports_Click);
            //
            // clearImports
            //
            this.clearImports.Location = new System.Drawing.Point(141, 200);
            this.clearImports.Name = "clearImports";
            this.clearImports.Size = new System.Drawing.Size(80, 23);
            this.clearImports.TabIndex = 6;
            this.clearImports.Text = "Clear";
            this.clearImports.UseVisualStyleBackColor = true;
            this.clearImports.Click += new System.EventHandler(this.clearImports_Click);
            //
            // testRoom
            //
            this.testRoom.AutoSize = true;
            this.testRoom.Location = new System.Drawing.Point(15, 234);
            this.testRoom.Name = "testRoom";
            this.testRoom.Size = new System.Drawing.Size(148, 17);
            this.testRoom.TabIndex = 7;
            this.testRoom.Text = "Make playable test room";
            this.toolTip1.SetToolTip(this.testRoom, "Start the level as a room you can walk around in: the player (DisplayModel:RIPLEY_FP from FRONTEND) and the " +
        "spawn logic (SpawnPositionSelect from SCI_HUB) are added to the imports, then a room of editable brushes, " +
        "ceiling lights and a player start in its centre are built, and the level is saved and built ready to play.");
            this.testRoom.UseVisualStyleBackColor = true;
            this.testRoom.CheckedChanged += new System.EventHandler(this.testRoom_CheckedChanged);
            //
            // roomLights
            //
            this.roomLights.AutoSize = true;
            this.roomLights.Checked = true;
            this.roomLights.CheckState = System.Windows.Forms.CheckState.Checked;
            this.roomLights.Enabled = false;
            this.roomLights.Location = new System.Drawing.Point(180, 234);
            this.roomLights.Name = "roomLights";
            this.roomLights.Size = new System.Drawing.Size(88, 17);
            this.roomLights.TabIndex = 8;
            this.roomLights.Text = "Ceiling lights";
            this.toolTip1.SetToolTip(this.roomLights, "Light the room: a grid of lights just under the ceiling, about 8 m apart.");
            this.roomLights.UseVisualStyleBackColor = true;
            //
            // roomSizeLabel
            //
            this.roomSizeLabel.AutoSize = true;
            this.roomSizeLabel.Enabled = false;
            this.roomSizeLabel.Location = new System.Drawing.Point(30, 260);
            this.roomSizeLabel.Name = "roomSizeLabel";
            this.roomSizeLabel.Size = new System.Drawing.Size(77, 13);
            this.roomSizeLabel.TabIndex = 9;
            this.roomSizeLabel.Text = "Room size (m):";
            //
            // roomWidth
            //
            this.roomWidth.DecimalPlaces = 1;
            this.roomWidth.Enabled = false;
            this.roomWidth.Location = new System.Drawing.Point(110, 257);
            this.roomWidth.Maximum = new decimal(new int[] { 128, 0, 0, 0 });
            this.roomWidth.Minimum = new decimal(new int[] { 2, 0, 0, 0 });
            this.roomWidth.Name = "roomWidth";
            this.roomWidth.Size = new System.Drawing.Size(50, 20);
            this.roomWidth.TabIndex = 10;
            this.toolTip1.SetToolTip(this.roomWidth, "Width inside the walls (X), in metres.");
            this.roomWidth.Value = new decimal(new int[] { 16, 0, 0, 0 });
            //
            // roomTimes1
            //
            this.roomTimes1.AutoSize = true;
            this.roomTimes1.Enabled = false;
            this.roomTimes1.Location = new System.Drawing.Point(162, 260);
            this.roomTimes1.Name = "roomTimes1";
            this.roomTimes1.Size = new System.Drawing.Size(12, 13);
            this.roomTimes1.TabIndex = 11;
            this.roomTimes1.Text = "x";
            //
            // roomLength
            //
            this.roomLength.DecimalPlaces = 1;
            this.roomLength.Enabled = false;
            this.roomLength.Location = new System.Drawing.Point(176, 257);
            this.roomLength.Maximum = new decimal(new int[] { 128, 0, 0, 0 });
            this.roomLength.Minimum = new decimal(new int[] { 2, 0, 0, 0 });
            this.roomLength.Name = "roomLength";
            this.roomLength.Size = new System.Drawing.Size(50, 20);
            this.roomLength.TabIndex = 12;
            this.toolTip1.SetToolTip(this.roomLength, "Length inside the walls (Z), in metres. The player starts in the middle facing the +Z wall.");
            this.roomLength.Value = new decimal(new int[] { 16, 0, 0, 0 });
            //
            // roomTimes2
            //
            this.roomTimes2.AutoSize = true;
            this.roomTimes2.Enabled = false;
            this.roomTimes2.Location = new System.Drawing.Point(228, 260);
            this.roomTimes2.Name = "roomTimes2";
            this.roomTimes2.Size = new System.Drawing.Size(12, 13);
            this.roomTimes2.TabIndex = 13;
            this.roomTimes2.Text = "x";
            //
            // roomHeight
            //
            this.roomHeight.DecimalPlaces = 1;
            this.roomHeight.Enabled = false;
            this.roomHeight.Location = new System.Drawing.Point(242, 257);
            this.roomHeight.Maximum = new decimal(new int[] { 32, 0, 0, 0 });
            this.roomHeight.Minimum = new decimal(new int[] { 25, 0, 0, 65536 });
            this.roomHeight.Name = "roomHeight";
            this.roomHeight.Size = new System.Drawing.Size(50, 20);
            this.roomHeight.TabIndex = 14;
            this.toolTip1.SetToolTip(this.roomHeight, "Floor to ceiling, in metres.");
            this.roomHeight.Value = new decimal(new int[] { 5, 0, 0, 0 });
            //
            // CreateLevel
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(449, 286);
            this.Controls.Add(this.roomHeight);
            this.Controls.Add(this.roomTimes2);
            this.Controls.Add(this.roomLength);
            this.Controls.Add(this.roomTimes1);
            this.Controls.Add(this.roomWidth);
            this.Controls.Add(this.roomSizeLabel);
            this.Controls.Add(this.roomLights);
            this.Controls.Add(this.testRoom);
            this.Controls.Add(this.clearImports);
            this.Controls.Add(this.chooseImports);
            this.Controls.Add(this.importSummary);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.levelName);
            this.Controls.Add(this.createLevel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "CreateLevel";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Create Level";
            ((System.ComponentModel.ISupportInitialize)(this.roomWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.roomLength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.roomHeight)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button createLevel;
        private System.Windows.Forms.TextBox levelName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ListBox importSummary;
        private System.Windows.Forms.Button chooseImports;
        private System.Windows.Forms.Button clearImports;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.CheckBox testRoom;
        private System.Windows.Forms.CheckBox roomLights;
        private System.Windows.Forms.Label roomSizeLabel;
        private System.Windows.Forms.NumericUpDown roomWidth;
        private System.Windows.Forms.Label roomTimes1;
        private System.Windows.Forms.NumericUpDown roomLength;
        private System.Windows.Forms.Label roomTimes2;
        private System.Windows.Forms.NumericUpDown roomHeight;
    }
}
