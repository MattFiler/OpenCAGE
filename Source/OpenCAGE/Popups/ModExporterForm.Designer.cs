#if ENABLE_MOD_PACKAGES
namespace OpenCAGE.Popups
{
    partial class ModExporterForm
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
            this._split = new System.Windows.Forms.SplitContainer();
            this._treePanel = new System.Windows.Forms.Panel();
            this._tree = new System.Windows.Forms.TreeView();
            this._treeTip = new System.Windows.Forms.Label();
            this._meta = new System.Windows.Forms.TableLayoutPanel();
            this._saveAsLabel = new System.Windows.Forms.Label();
            this._updates = new System.Windows.Forms.ComboBox();
            this._installsLabel = new System.Windows.Forms.Label();
            this._installs = new System.Windows.Forms.ComboBox();
            this._installsInfo = new System.Windows.Forms.Label();
            this._launchLabel = new System.Windows.Forms.Label();
            this.launchLevel = new System.Windows.Forms.ComboBox();
            this._launchOptionsRow = new System.Windows.Forms.FlowLayoutPanel();
            this._nameLabel = new System.Windows.Forms.Label();
            this._name = new System.Windows.Forms.TextBox();
            this._authorLabel = new System.Windows.Forms.Label();
            this._author = new System.Windows.Forms.TextBox();
            this._versionLabel = new System.Windows.Forms.Label();
            this._version = new System.Windows.Forms.TextBox();
            this._descriptionLabel = new System.Windows.Forms.Label();
            this._description = new System.Windows.Forms.TextBox();
            this._changesLabel = new System.Windows.Forms.Label();
            this._changes = new System.Windows.Forms.TextBox();
            this._pictureRow = new System.Windows.Forms.FlowLayoutPanel();
            this._choosePicture = new System.Windows.Forms.Button();
            this._clearPicture = new System.Windows.Forms.Button();
            this._picture = new System.Windows.Forms.PictureBox();
            this._summary = new System.Windows.Forms.Label();
            this._buttons = new System.Windows.Forms.FlowLayoutPanel();
            this._closeButton = new System.Windows.Forms.Button();
            this._exportButton = new System.Windows.Forms.Button();
            this._tips = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
            this._split.Panel1.SuspendLayout();
            this._split.Panel2.SuspendLayout();
            this._split.SuspendLayout();
            this._treePanel.SuspendLayout();
            this._meta.SuspendLayout();
            this._pictureRow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._picture)).BeginInit();
            this._buttons.SuspendLayout();
            this.SuspendLayout();
            // 
            // _split
            // 
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this._split.Location = new System.Drawing.Point(0, 0);
            this._split.Name = "_split";
            // 
            // _split.Panel1
            // 
            this._split.Panel1.Controls.Add(this._treePanel);
            // 
            // _split.Panel2
            // 
            this._split.Panel2.Controls.Add(this._meta);
            this._split.Size = new System.Drawing.Size(964, 577);
            this._split.SplitterDistance = 564;
            this._split.TabIndex = 0;
            // 
            // _treePanel
            // 
            this._treePanel.Controls.Add(this._tree);
            this._treePanel.Controls.Add(this._treeTip);
            this._treePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._treePanel.Location = new System.Drawing.Point(0, 0);
            this._treePanel.Name = "_treePanel";
            this._treePanel.Size = new System.Drawing.Size(564, 577);
            this._treePanel.TabIndex = 0;
            // 
            // _tree
            // 
            this._tree.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tree.Location = new System.Drawing.Point(0, 36);
            this._tree.Name = "_tree";
            this._tree.ShowNodeToolTips = true;
            this._tree.Size = new System.Drawing.Size(564, 541);
            this._tree.TabIndex = 0;
            this._tree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.OnAfterCheck);
            this._tree.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this._tree_NodeMouseClick);
            this._tree.KeyDown += new System.Windows.Forms.KeyEventHandler(this._tree_KeyDown);
            // 
            // _treeTip
            // 
            this._treeTip.Dock = System.Windows.Forms.DockStyle.Top;
            this._treeTip.Location = new System.Drawing.Point(0, 0);
            this._treeTip.Name = "_treeTip";
            this._treeTip.Padding = new System.Windows.Forms.Padding(2, 4, 2, 0);
            this._treeTip.Size = new System.Drawing.Size(564, 36);
            this._treeTip.TabIndex = 1;
            this._treeTip.Text = "Everything you've changed in the game is listed here. Untick anything that shouldn't go in the mod.";
            // 
            // _meta
            // 
            this._meta.AutoScroll = true;
            this._meta.ColumnCount = 1;
            this._meta.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._meta.Controls.Add(this._saveAsLabel, 0, 0);
            this._meta.Controls.Add(this._updates, 0, 1);
            this._meta.Controls.Add(this._installsLabel, 0, 2);
            this._meta.Controls.Add(this._installs, 0, 3);
            this._meta.Controls.Add(this._installsInfo, 0, 4);
            this._meta.Controls.Add(this._launchLabel, 0, 5);
            this._meta.Controls.Add(this.launchLevel, 0, 6);
            this._meta.Controls.Add(this._launchOptionsRow, 0, 7);
            this._meta.Controls.Add(this._nameLabel, 0, 8);
            this._meta.Controls.Add(this._name, 0, 9);
            this._meta.Controls.Add(this._authorLabel, 0, 10);
            this._meta.Controls.Add(this._author, 0, 11);
            this._meta.Controls.Add(this._versionLabel, 0, 12);
            this._meta.Controls.Add(this._version, 0, 13);
            this._meta.Controls.Add(this._descriptionLabel, 0, 14);
            this._meta.Controls.Add(this._description, 0, 15);
            this._meta.Controls.Add(this._changesLabel, 0, 16);
            this._meta.Controls.Add(this._changes, 0, 17);
            this._meta.Controls.Add(this._pictureRow, 0, 18);
            this._meta.Controls.Add(this._picture, 0, 19);
            this._meta.Dock = System.Windows.Forms.DockStyle.Fill;
            this._meta.Location = new System.Drawing.Point(0, 0);
            this._meta.Name = "_meta";
            this._meta.Padding = new System.Windows.Forms.Padding(8);
            this._meta.RowCount = 20;
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._meta.Size = new System.Drawing.Size(396, 577);
            this._meta.TabIndex = 0;
            // 
            // _saveAsLabel
            // 
            this._saveAsLabel.AutoSize = true;
            this._saveAsLabel.Location = new System.Drawing.Point(11, 8);
            this._saveAsLabel.Name = "_saveAsLabel";
            this._saveAsLabel.Size = new System.Drawing.Size(66, 17);
            this._saveAsLabel.TabIndex = 0;
            this._saveAsLabel.Text = "Save this as";
            this._tips.SetToolTip(this._saveAsLabel, "A new version of a mod you've installed takes its place: anyone who installs this gets it instead of the old one, not as well as it.\nIts name, author, description and picture are filled in from the old one, and its version goes up by one.");
            // 
            // _updates
            // 
            this._updates.Dock = System.Windows.Forms.DockStyle.Top;
            this._updates.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._updates.Items.AddRange(new object[] {
            "A new mod"});
            this._updates.Location = new System.Drawing.Point(11, 28);
            this._updates.Name = "_updates";
            this._updates.Size = new System.Drawing.Size(374, 21);
            this._updates.TabIndex = 1;
            this._tips.SetToolTip(this._updates, "A new version of a mod you've installed takes its place: anyone who installs this gets it instead of the old one, not as well as it.\nIts name, author, description and picture are filled in from the old one, and its version goes up by one.");
            // 
            // _installsLabel
            // 
            this._installsLabel.AutoSize = true;
            this._installsLabel.Location = new System.Drawing.Point(8, 60);
            this._installsLabel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._installsLabel.Name = "_installsLabel";
            this._installsLabel.Size = new System.Drawing.Size(73, 17);
            this._installsLabel.TabIndex = 2;
            this._installsLabel.Text = "How it installs";
            // 
            // _installs
            // 
            this._installs.Dock = System.Windows.Forms.DockStyle.Top;
            this._installs.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._installs.Items.AddRange(new object[] {
            "Combines with other mods",
            "Replaces what it changes"});
            this._installs.Location = new System.Drawing.Point(11, 80);
            this._installs.Name = "_installs";
            this._installs.Size = new System.Drawing.Size(374, 21);
            this._installs.TabIndex = 3;
            // 
            // _installsInfo
            // 
            this._installsInfo.AutoSize = true;
            this._installsInfo.Dock = System.Windows.Forms.DockStyle.Top;
            this._installsInfo.Location = new System.Drawing.Point(8, 106);
            this._installsInfo.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this._installsInfo.MaximumSize = new System.Drawing.Size(360, 0);
            this._installsInfo.Name = "_installsInfo";
            this._installsInfo.Size = new System.Drawing.Size(360, 55);
            this._installsInfo.TabIndex = 4;
            // 
            // _launchLabel
            // 
            this._launchLabel.AutoSize = true;
            this._launchLabel.Location = new System.Drawing.Point(8, 169);
            this._launchLabel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._launchLabel.Name = "_launchLabel";
            this._launchLabel.Size = new System.Drawing.Size(118, 17);
            this._launchLabel.TabIndex = 5;
            this._launchLabel.Text = "Launch Mod starts it in";
            this._tips.SetToolTip(this._launchLabel, "The level the game starts in when a player uses Launch Mod in the Mod Manager - with that level's own UI movies (Level UI Overrides), which are used only while it's played. Not set: the mod's only level, or the player picks one of its levels.");
            // 
            // launchLevel
            // 
            this.launchLevel.Dock = System.Windows.Forms.DockStyle.Top;
            this.launchLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.launchLevel.Items.AddRange(new object[] {
            "Not set (its only level, or the player picks)",
            "The main menu"});
            this.launchLevel.Location = new System.Drawing.Point(11, 189);
            this.launchLevel.Name = "launchLevel";
            this.launchLevel.Size = new System.Drawing.Size(374, 21);
            this.launchLevel.TabIndex = 6;
            this._tips.SetToolTip(this.launchLevel, "The level the game starts in when a player uses Launch Mod in the Mod Manager - with that level's own UI movies (Level UI Overrides), which are used only while it's played. Not set: the mod's only level, or the player picks one of its levels.");
            // 
            // _launchOptionsRow
            // 
            this._launchOptionsRow.AutoSize = true;
            this._launchOptionsRow.Dock = System.Windows.Forms.DockStyle.Top;
            this._launchOptionsRow.Location = new System.Drawing.Point(8, 215);
            this._launchOptionsRow.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this._launchOptionsRow.Name = "_launchOptionsRow";
            this._launchOptionsRow.Size = new System.Drawing.Size(380, 48);
            this._launchOptionsRow.TabIndex = 7;
            // 
            // _nameLabel
            // 
            this._nameLabel.AutoSize = true;
            this._nameLabel.Location = new System.Drawing.Point(8, 271);
            this._nameLabel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._nameLabel.Name = "_nameLabel";
            this._nameLabel.Size = new System.Drawing.Size(58, 17);
            this._nameLabel.TabIndex = 8;
            this._nameLabel.Text = "Mod name";
            // 
            // _name
            // 
            this._name.Dock = System.Windows.Forms.DockStyle.Top;
            this._name.Location = new System.Drawing.Point(11, 291);
            this._name.Name = "_name";
            this._name.Size = new System.Drawing.Size(374, 20);
            this._name.TabIndex = 9;
            // 
            // _authorLabel
            // 
            this._authorLabel.AutoSize = true;
            this._authorLabel.Location = new System.Drawing.Point(8, 322);
            this._authorLabel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._authorLabel.Name = "_authorLabel";
            this._authorLabel.Size = new System.Drawing.Size(38, 17);
            this._authorLabel.TabIndex = 10;
            this._authorLabel.Text = "Author";
            // 
            // _author
            // 
            this._author.Dock = System.Windows.Forms.DockStyle.Top;
            this._author.Location = new System.Drawing.Point(11, 342);
            this._author.Name = "_author";
            this._author.Size = new System.Drawing.Size(374, 20);
            this._author.TabIndex = 11;
            // 
            // _versionLabel
            // 
            this._versionLabel.AutoSize = true;
            this._versionLabel.Location = new System.Drawing.Point(8, 373);
            this._versionLabel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._versionLabel.Name = "_versionLabel";
            this._versionLabel.Size = new System.Drawing.Size(43, 17);
            this._versionLabel.TabIndex = 12;
            this._versionLabel.Text = "Version";
            // 
            // _version
            // 
            this._version.Dock = System.Windows.Forms.DockStyle.Top;
            this._version.Location = new System.Drawing.Point(11, 393);
            this._version.Name = "_version";
            this._version.Size = new System.Drawing.Size(374, 20);
            this._version.TabIndex = 13;
            this._version.Text = "1.0";
            // 
            // _descriptionLabel
            // 
            this._descriptionLabel.AutoSize = true;
            this._descriptionLabel.Location = new System.Drawing.Point(8, 424);
            this._descriptionLabel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._descriptionLabel.Name = "_descriptionLabel";
            this._descriptionLabel.Size = new System.Drawing.Size(201, 17);
            this._descriptionLabel.TabIndex = 14;
            this._descriptionLabel.Text = "Description (what players should know)";
            // 
            // _description
            // 
            this._description.Dock = System.Windows.Forms.DockStyle.Fill;
            this._description.Location = new System.Drawing.Point(11, 444);
            this._description.Multiline = true;
            this._description.Name = "_description";
            this._description.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._description.Size = new System.Drawing.Size(374, 110);
            this._description.TabIndex = 15;
            // 
            // _changesLabel
            // 
            this._changesLabel.AutoSize = true;
            this._changesLabel.Location = new System.Drawing.Point(8, 565);
            this._changesLabel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._changesLabel.Name = "_changesLabel";
            this._changesLabel.Size = new System.Drawing.Size(256, 17);
            this._changesLabel.TabIndex = 16;
            this._changesLabel.Text = "What it changes (filled in for you - edit it if you like)";
            // 
            // _changes
            // 
            this._changes.Dock = System.Windows.Forms.DockStyle.Top;
            this._changes.Location = new System.Drawing.Point(11, 585);
            this._changes.Multiline = true;
            this._changes.Name = "_changes";
            this._changes.Size = new System.Drawing.Size(374, 46);
            this._changes.TabIndex = 17;
            this._changes.TextChanged += new System.EventHandler(this._changes_TextChanged);
            // 
            // _pictureRow
            // 
            this._pictureRow.AutoSize = true;
            this._pictureRow.Controls.Add(this._choosePicture);
            this._pictureRow.Controls.Add(this._clearPicture);
            this._pictureRow.Dock = System.Windows.Forms.DockStyle.Top;
            this._pictureRow.Location = new System.Drawing.Point(8, 642);
            this._pictureRow.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._pictureRow.Name = "_pictureRow";
            this._pictureRow.Size = new System.Drawing.Size(380, 30);
            this._pictureRow.TabIndex = 18;
            // 
            // _choosePicture
            // 
            this._choosePicture.AutoSize = true;
            this._choosePicture.Location = new System.Drawing.Point(3, 3);
            this._choosePicture.Name = "_choosePicture";
            this._choosePicture.Size = new System.Drawing.Size(75, 24);
            this._choosePicture.TabIndex = 0;
            this._choosePicture.Text = "Picture...";
            this._choosePicture.Click += new System.EventHandler(this._choosePicture_Click);
            // 
            // _clearPicture
            // 
            this._clearPicture.AutoSize = true;
            this._clearPicture.Location = new System.Drawing.Point(84, 3);
            this._clearPicture.Name = "_clearPicture";
            this._clearPicture.Size = new System.Drawing.Size(75, 24);
            this._clearPicture.TabIndex = 1;
            this._clearPicture.Text = "No picture";
            this._clearPicture.Click += new System.EventHandler(this._clearPicture_Click);
            // 
            // _picture
            // 
            this._picture.Dock = System.Windows.Forms.DockStyle.Top;
            this._picture.Location = new System.Drawing.Point(11, 675);
            this._picture.Name = "_picture";
            this._picture.Size = new System.Drawing.Size(374, 120);
            this._picture.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._picture.TabIndex = 19;
            this._picture.TabStop = false;
            // 
            // _summary
            // 
            this._summary.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._summary.Location = new System.Drawing.Point(0, 577);
            this._summary.Name = "_summary";
            this._summary.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this._summary.Size = new System.Drawing.Size(964, 22);
            this._summary.TabIndex = 1;
            this._summary.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _buttons
            // 
            this._buttons.Controls.Add(this._closeButton);
            this._buttons.Controls.Add(this._exportButton);
            this._buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._buttons.Location = new System.Drawing.Point(0, 599);
            this._buttons.Name = "_buttons";
            this._buttons.Padding = new System.Windows.Forms.Padding(6);
            this._buttons.Size = new System.Drawing.Size(964, 42);
            this._buttons.TabIndex = 2;
            // 
            // _closeButton
            // 
            this._closeButton.AutoSize = true;
            this._closeButton.Location = new System.Drawing.Point(874, 9);
            this._closeButton.Name = "_closeButton";
            this._closeButton.Size = new System.Drawing.Size(75, 28);
            this._closeButton.TabIndex = 0;
            this._closeButton.Text = "Close";
            this._closeButton.Click += new System.EventHandler(this._closeButton_Click);
            // 
            // _exportButton
            // 
            this._exportButton.AutoSize = true;
            this._exportButton.Location = new System.Drawing.Point(793, 9);
            this._exportButton.Name = "_exportButton";
            this._exportButton.Size = new System.Drawing.Size(75, 28);
            this._exportButton.TabIndex = 1;
            this._exportButton.Text = "Save Mod...";
            this._exportButton.Click += new System.EventHandler(this._exportButton_Click);
            // 
            // _tips
            // 
            this._tips.AutoPopDelay = 15000;
            // 
            // ModExporterForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(964, 641);
            this.Controls.Add(this._split);
            this.Controls.Add(this._summary);
            this.Controls.Add(this._buttons);
            this.MinimumSize = new System.Drawing.Size(760, 520);
            this.Name = "ModExporterForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Create a Mod";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.ModExporterForm_FormClosed);
            this.Shown += new System.EventHandler(this.ModExporterForm_Shown);
            this._split.Panel1.ResumeLayout(false);
            this._split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
            this._split.ResumeLayout(false);
            this._treePanel.ResumeLayout(false);
            this._meta.ResumeLayout(false);
            this._meta.PerformLayout();
            this._pictureRow.ResumeLayout(false);
            this._pictureRow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._picture)).EndInit();
            this._buttons.ResumeLayout(false);
            this._buttons.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.Panel _treePanel;
        private System.Windows.Forms.TreeView _tree;
        private System.Windows.Forms.Label _treeTip;
        private System.Windows.Forms.TableLayoutPanel _meta;
        private System.Windows.Forms.Label _saveAsLabel;
        private System.Windows.Forms.ComboBox _updates;
        private System.Windows.Forms.Label _installsLabel;
        private System.Windows.Forms.ComboBox _installs;
        private System.Windows.Forms.Label _installsInfo;
        private System.Windows.Forms.Label _launchLabel;
        private System.Windows.Forms.ComboBox launchLevel;
        private System.Windows.Forms.FlowLayoutPanel _launchOptionsRow;
        private System.Windows.Forms.Label _nameLabel;
        private System.Windows.Forms.TextBox _name;
        private System.Windows.Forms.Label _authorLabel;
        private System.Windows.Forms.TextBox _author;
        private System.Windows.Forms.Label _versionLabel;
        private System.Windows.Forms.TextBox _version;
        private System.Windows.Forms.Label _descriptionLabel;
        private System.Windows.Forms.TextBox _description;
        private System.Windows.Forms.Label _changesLabel;
        private System.Windows.Forms.TextBox _changes;
        private System.Windows.Forms.FlowLayoutPanel _pictureRow;
        private System.Windows.Forms.Button _choosePicture;
        private System.Windows.Forms.Button _clearPicture;
        private System.Windows.Forms.PictureBox _picture;
        private System.Windows.Forms.Label _summary;
        private System.Windows.Forms.FlowLayoutPanel _buttons;
        private System.Windows.Forms.Button _closeButton;
        private System.Windows.Forms.Button _exportButton;
        private System.Windows.Forms.ToolTip _tips;
    }
}
#endif
