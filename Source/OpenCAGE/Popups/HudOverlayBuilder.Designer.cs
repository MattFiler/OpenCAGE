namespace OpenCAGE
{
    partial class HudOverlayBuilder
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
            this.previewHost = new System.Windows.Forms.Panel();
            this.rightPanel = new System.Windows.Forms.Panel();
            this.grid = new System.Windows.Forms.PropertyGrid();
            this.leftPanel = new System.Windows.Forms.Panel();
            this.slots = new System.Windows.Forms.ListBox();
            this.slotButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.addButton = new System.Windows.Forms.Button();
            this.copyButton = new System.Windows.Forms.Button();
            this.removeButton = new System.Windows.Forms.Button();
            this.upButton = new System.Windows.Forms.Button();
            this.downButton = new System.Windows.Forms.Button();
            this.status = new System.Windows.Forms.TextBox();
            this.buttonsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.closeButton = new System.Windows.Forms.Button();
            this.saveButton = new System.Windows.Forms.Button();
            this.checkButton = new System.Windows.Forms.Button();
            this.exportButton = new System.Windows.Forms.Button();
            this.removeFromLevelButton = new System.Windows.Forms.Button();
            this.topPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.overlayLabel = new System.Windows.Forms.Label();
            this.overlays = new System.Windows.Forms.ComboBox();
            this.newButton = new System.Windows.Forms.Button();
            this.importButton = new System.Windows.Forms.Button();
            this.folderButton = new System.Windows.Forms.Button();
            this.tips = new System.Windows.Forms.ToolTip(this.components);
            this.rightPanel.SuspendLayout();
            this.leftPanel.SuspendLayout();
            this.slotButtons.SuspendLayout();
            this.buttonsPanel.SuspendLayout();
            this.topPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // previewHost
            //
            this.previewHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.previewHost.Location = new System.Drawing.Point(230, 34);
            this.previewHost.Name = "previewHost";
            this.previewHost.Size = new System.Drawing.Size(750, 612);
            this.previewHost.TabIndex = 0;
            //
            // rightPanel
            //
            this.rightPanel.Controls.Add(this.grid);
            this.rightPanel.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightPanel.Location = new System.Drawing.Point(980, 34);
            this.rightPanel.Name = "rightPanel";
            this.rightPanel.Padding = new System.Windows.Forms.Padding(4);
            this.rightPanel.Size = new System.Drawing.Size(340, 612);
            this.rightPanel.TabIndex = 1;
            //
            // grid
            //
            this.grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grid.Location = new System.Drawing.Point(4, 4);
            this.grid.Name = "grid";
            this.grid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
            this.grid.Size = new System.Drawing.Size(332, 604);
            this.grid.TabIndex = 0;
            this.grid.ToolbarVisible = false;
            this.grid.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.grid_PropertyValueChanged);
            //
            // leftPanel
            //
            this.leftPanel.Controls.Add(this.slots);
            this.leftPanel.Controls.Add(this.slotButtons);
            this.leftPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftPanel.Location = new System.Drawing.Point(0, 34);
            this.leftPanel.Name = "leftPanel";
            this.leftPanel.Padding = new System.Windows.Forms.Padding(4);
            this.leftPanel.Size = new System.Drawing.Size(230, 612);
            this.leftPanel.TabIndex = 2;
            //
            // slots
            //
            this.slots.Dock = System.Windows.Forms.DockStyle.Fill;
            this.slots.IntegralHeight = false;
            this.slots.Location = new System.Drawing.Point(4, 4);
            this.slots.Name = "slots";
            this.slots.Size = new System.Drawing.Size(222, 540);
            this.slots.TabIndex = 0;
            this.slots.SelectedIndexChanged += new System.EventHandler(this.slots_SelectedIndexChanged);
            //
            // slotButtons
            //
            this.slotButtons.Controls.Add(this.addButton);
            this.slotButtons.Controls.Add(this.copyButton);
            this.slotButtons.Controls.Add(this.removeButton);
            this.slotButtons.Controls.Add(this.upButton);
            this.slotButtons.Controls.Add(this.downButton);
            this.slotButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.slotButtons.Location = new System.Drawing.Point(4, 544);
            this.slotButtons.Name = "slotButtons";
            this.slotButtons.Size = new System.Drawing.Size(222, 64);
            this.slotButtons.TabIndex = 1;
            //
            // addButton
            //
            this.addButton.Location = new System.Drawing.Point(3, 3);
            this.addButton.Name = "addButton";
            this.addButton.Size = new System.Drawing.Size(66, 26);
            this.addButton.TabIndex = 0;
            this.addButton.Text = "Add";
            this.tips.SetToolTip(this.addButton, "Add a slot: a text, number, timer, bar, reticle or panel.");
            this.addButton.Click += new System.EventHandler(this.addButton_Click);
            //
            // copyButton
            //
            this.copyButton.Location = new System.Drawing.Point(75, 3);
            this.copyButton.Name = "copyButton";
            this.copyButton.Size = new System.Drawing.Size(66, 26);
            this.copyButton.TabIndex = 1;
            this.copyButton.Text = "Copy";
            this.tips.SetToolTip(this.copyButton, "Duplicate the selected slot.");
            this.copyButton.Click += new System.EventHandler(this.copyButton_Click);
            //
            // removeButton
            //
            this.removeButton.Location = new System.Drawing.Point(147, 3);
            this.removeButton.Name = "removeButton";
            this.removeButton.Size = new System.Drawing.Size(66, 26);
            this.removeButton.TabIndex = 2;
            this.removeButton.Text = "Remove";
            this.tips.SetToolTip(this.removeButton, "Remove the selected slot.");
            this.removeButton.Click += new System.EventHandler(this.removeButton_Click);
            //
            // upButton
            //
            this.upButton.Location = new System.Drawing.Point(3, 35);
            this.upButton.Name = "upButton";
            this.upButton.Size = new System.Drawing.Size(66, 26);
            this.upButton.TabIndex = 3;
            this.upButton.Text = "Up";
            this.tips.SetToolTip(this.upButton, "Earlier in the list: drawn under the slots after it.");
            this.upButton.Click += new System.EventHandler(this.upButton_Click);
            //
            // downButton
            //
            this.downButton.Location = new System.Drawing.Point(75, 35);
            this.downButton.Name = "downButton";
            this.downButton.Size = new System.Drawing.Size(66, 26);
            this.downButton.TabIndex = 4;
            this.downButton.Text = "Down";
            this.tips.SetToolTip(this.downButton, "Later in the list: drawn over the slots before it.");
            this.downButton.Click += new System.EventHandler(this.downButton_Click);
            //
            // status
            //
            this.status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.status.Location = new System.Drawing.Point(0, 646);
            this.status.Multiline = true;
            this.status.Name = "status";
            this.status.ReadOnly = true;
            this.status.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.status.Size = new System.Drawing.Size(1320, 96);
            this.status.TabIndex = 3;
            //
            // buttonsPanel
            //
            this.buttonsPanel.Controls.Add(this.closeButton);
            this.buttonsPanel.Controls.Add(this.saveButton);
            this.buttonsPanel.Controls.Add(this.checkButton);
            this.buttonsPanel.Controls.Add(this.exportButton);
            this.buttonsPanel.Controls.Add(this.removeFromLevelButton);
            this.buttonsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonsPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttonsPanel.Location = new System.Drawing.Point(0, 742);
            this.buttonsPanel.Name = "buttonsPanel";
            this.buttonsPanel.Padding = new System.Windows.Forms.Padding(4);
            this.buttonsPanel.Size = new System.Drawing.Size(1320, 38);
            this.buttonsPanel.TabIndex = 4;
            //
            // closeButton
            //
            this.closeButton.Location = new System.Drawing.Point(1219, 7);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new System.Drawing.Size(90, 26);
            this.closeButton.TabIndex = 0;
            this.closeButton.Text = "Close";
            this.closeButton.Click += new System.EventHandler(this.closeButton_Click);
            //
            // saveButton
            //
            this.saveButton.Location = new System.Drawing.Point(1093, 7);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(120, 26);
            this.saveButton.TabIndex = 1;
            this.saveButton.Text = "Save to Level";
            this.tips.SetToolTip(this.saveButton, "Check the overlay\'s movie, write it into the level\'s folder and make (or remake) the composite HUD\\<Name> that loads and drives it, as one undo step. Save the level afterwards.");
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            //
            // checkButton
            //
            this.checkButton.Location = new System.Drawing.Point(997, 7);
            this.checkButton.Name = "checkButton";
            this.checkButton.Size = new System.Drawing.Size(90, 26);
            this.checkButton.TabIndex = 2;
            this.checkButton.Text = "Check";
            this.tips.SetToolTip(this.checkButton, "Build the movie and check it the way the game will use it (nothing is written).");
            this.checkButton.Click += new System.EventHandler(this.checkButton_Click);
            //
            // exportButton
            //
            this.exportButton.Location = new System.Drawing.Point(881, 7);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(110, 26);
            this.exportButton.TabIndex = 3;
            this.exportButton.Text = "Export .gfx...";
            this.tips.SetToolTip(this.exportButton, "Save the checked movie somewhere else.");
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            //
            // removeFromLevelButton
            //
            this.removeFromLevelButton.Location = new System.Drawing.Point(735, 7);
            this.removeFromLevelButton.Name = "removeFromLevelButton";
            this.removeFromLevelButton.Size = new System.Drawing.Size(140, 26);
            this.removeFromLevelButton.TabIndex = 4;
            this.removeFromLevelButton.Text = "Remove from Level";
            this.tips.SetToolTip(this.removeFromLevelButton, "Take this overlay\'s movie and its composite\'s contents out of the level.");
            this.removeFromLevelButton.Click += new System.EventHandler(this.removeFromLevelButton_Click);
            //
            // topPanel
            //
            this.topPanel.Controls.Add(this.overlayLabel);
            this.topPanel.Controls.Add(this.overlays);
            this.topPanel.Controls.Add(this.newButton);
            this.topPanel.Controls.Add(this.importButton);
            this.topPanel.Controls.Add(this.folderButton);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPanel.Location = new System.Drawing.Point(0, 0);
            this.topPanel.Name = "topPanel";
            this.topPanel.Padding = new System.Windows.Forms.Padding(4, 4, 4, 0);
            this.topPanel.Size = new System.Drawing.Size(1320, 34);
            this.topPanel.TabIndex = 5;
            this.topPanel.WrapContents = false;
            //
            // overlayLabel
            //
            this.overlayLabel.AutoSize = true;
            this.overlayLabel.Location = new System.Drawing.Point(7, 4);
            this.overlayLabel.Name = "overlayLabel";
            this.overlayLabel.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this.overlayLabel.Size = new System.Drawing.Size(46, 19);
            this.overlayLabel.TabIndex = 0;
            this.overlayLabel.Text = "Overlay:";
            //
            // overlays
            //
            this.overlays.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.overlays.Location = new System.Drawing.Point(59, 7);
            this.overlays.Name = "overlays";
            this.overlays.Size = new System.Drawing.Size(200, 21);
            this.overlays.TabIndex = 1;
            this.tips.SetToolTip(this.overlays, "The level\'s overlays (each a movie in its folder). Pick one to edit it.");
            this.overlays.SelectedIndexChanged += new System.EventHandler(this.overlays_SelectedIndexChanged);
            //
            // newButton
            //
            this.newButton.Location = new System.Drawing.Point(265, 7);
            this.newButton.Name = "newButton";
            this.newButton.Size = new System.Drawing.Size(70, 26);
            this.newButton.TabIndex = 2;
            this.newButton.Text = "New";
            this.tips.SetToolTip(this.newButton, "Start a new overlay from the example (objective, timer, health bar, counter).");
            this.newButton.Click += new System.EventHandler(this.newButton_Click);
            //
            // importButton
            //
            this.importButton.Location = new System.Drawing.Point(341, 7);
            this.importButton.Name = "importButton";
            this.importButton.Size = new System.Drawing.Size(110, 26);
            this.importButton.TabIndex = 3;
            this.importButton.Text = "Import Movie...";
            this.tips.SetToolTip(this.importButton, "Use a movie made elsewhere (.gfx / .swf) as one of the level\'s overlays: it is checked first (a movie the game can\'t make its root object from would crash it) and refused if it fails.");
            this.importButton.Click += new System.EventHandler(this.importButton_Click);
            //
            // folderButton
            //
            this.folderButton.Location = new System.Drawing.Point(457, 7);
            this.folderButton.Name = "folderButton";
            this.folderButton.Size = new System.Drawing.Size(95, 26);
            this.folderButton.TabIndex = 4;
            this.folderButton.Text = "Open Folder";
            this.tips.SetToolTip(this.folderButton, "Open the level\'s overlay folder (DATA\\ENV\\<level>\\UI\\OVERLAYS).");
            this.folderButton.Click += new System.EventHandler(this.folderButton_Click);
            //
            // tips
            //
            this.tips.AutoPopDelay = 20000;
            //
            // HudOverlayBuilder
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.closeButton;
            this.ClientSize = new System.Drawing.Size(1320, 780);
            this.Controls.Add(this.previewHost);
            this.Controls.Add(this.rightPanel);
            this.Controls.Add(this.leftPanel);
            this.Controls.Add(this.status);
            this.Controls.Add(this.buttonsPanel);
            this.Controls.Add(this.topPanel);
            this.KeyPreview = true;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(980, 560);
            this.Name = "HudOverlayBuilder";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.HudOverlayBuilder_FormClosing);
            this.Shown += new System.EventHandler(this.HudOverlayBuilder_Shown);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnKey);
            this.rightPanel.ResumeLayout(false);
            this.leftPanel.ResumeLayout(false);
            this.slotButtons.ResumeLayout(false);
            this.buttonsPanel.ResumeLayout(false);
            this.topPanel.ResumeLayout(false);
            this.topPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel previewHost;
        private System.Windows.Forms.Panel rightPanel;
        private System.Windows.Forms.PropertyGrid grid;
        private System.Windows.Forms.Panel leftPanel;
        private System.Windows.Forms.ListBox slots;
        private System.Windows.Forms.FlowLayoutPanel slotButtons;
        private System.Windows.Forms.Button addButton;
        private System.Windows.Forms.Button copyButton;
        private System.Windows.Forms.Button removeButton;
        private System.Windows.Forms.Button upButton;
        private System.Windows.Forms.Button downButton;
        private System.Windows.Forms.TextBox status;
        private System.Windows.Forms.FlowLayoutPanel buttonsPanel;
        private System.Windows.Forms.Button closeButton;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.Button checkButton;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.Button removeFromLevelButton;
        private System.Windows.Forms.FlowLayoutPanel topPanel;
        private System.Windows.Forms.Label overlayLabel;
        private System.Windows.Forms.ComboBox overlays;
        private System.Windows.Forms.Button newButton;
        private System.Windows.Forms.Button importButton;
        private System.Windows.Forms.Button folderButton;
        private System.Windows.Forms.ToolTip tips;
    }
}
