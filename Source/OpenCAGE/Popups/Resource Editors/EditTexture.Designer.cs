namespace OpenCAGE
{
    partial class EditTexture
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditTexture));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.leftMainLayout = new System.Windows.Forms.TableLayoutPanel();
            this.exportAllTexturesBtn = new System.Windows.Forms.Button();
            this.tableLayoutLeftHeader = new System.Windows.Forms.TableLayoutPanel();
            this.labelSearch = new System.Windows.Forms.Label();
            this.textureSearchBox = new System.Windows.Forms.TextBox();
            this.textureSourceLabel = new System.Windows.Forms.Label();
            this.textureSourceCombo = new System.Windows.Forms.ComboBox();
            this.FileTree = new System.Windows.Forms.TreeView();
            this.importTextureBtn = new System.Windows.Forms.Button();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.texturePreviewArea = new System.Windows.Forms.GroupBox();
            this.previewOuterTable = new System.Windows.Forms.TableLayoutPanel();
            this.previewTabControl = new System.Windows.Forms.TabControl();
            this.tabStreamed = new System.Windows.Forms.TabPage();
            this.streamedTabLayout = new System.Windows.Forms.TableLayoutPanel();
            this.pictureStreamed = new System.Windows.Forms.PictureBox();
            this.groupStreamedMeta = new System.Windows.Forms.GroupBox();
            this.streamedMetaText = new System.Windows.Forms.TextBox();
            this.tabPersistent = new System.Windows.Forms.TabPage();
            this.persistentTabLayout = new System.Windows.Forms.TableLayoutPanel();
            this.picturePersistent = new System.Windows.Forms.PictureBox();
            this.groupPersistentMeta = new System.Windows.Forms.GroupBox();
            this.persistentMetaText = new System.Windows.Forms.TextBox();
            this.textureActionsTable = new System.Windows.Forms.TableLayoutPanel();
            this.replaceTextureBtn = new System.Windows.Forms.Button();
            this.metadataTable = new System.Windows.Forms.TableLayoutPanel();
            this.metaStateCaption = new System.Windows.Forms.Label();
            this.stateFlagsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.metaUsageCaption = new System.Windows.Forms.Label();
            this.usageFlagsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.deleteTextureBtn = new System.Windows.Forms.Button();
            this.exportTextureBtn = new System.Windows.Forms.Button();
            this.selectTextureBtn = new System.Windows.Forms.Button();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.leftMainLayout.SuspendLayout();
            this.tableLayoutLeftHeader.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.texturePreviewArea.SuspendLayout();
            this.previewOuterTable.SuspendLayout();
            this.previewTabControl.SuspendLayout();
            this.tabStreamed.SuspendLayout();
            this.streamedTabLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureStreamed)).BeginInit();
            this.groupStreamedMeta.SuspendLayout();
            this.tabPersistent.SuspendLayout();
            this.persistentTabLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picturePersistent)).BeginInit();
            this.groupPersistentMeta.SuspendLayout();
            this.textureActionsTable.SuspendLayout();
            this.metadataTable.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(7, 8);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.leftMainLayout);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(1057, 632);
            this.splitContainer1.SplitterDistance = 352;
            this.splitContainer1.TabIndex = 105;
            // 
            // leftMainLayout
            // 
            this.leftMainLayout.ColumnCount = 1;
            this.leftMainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.leftMainLayout.Controls.Add(this.exportAllTexturesBtn, 0, 3);
            this.leftMainLayout.Controls.Add(this.tableLayoutLeftHeader, 0, 0);
            this.leftMainLayout.Controls.Add(this.FileTree, 0, 1);
            this.leftMainLayout.Controls.Add(this.importTextureBtn, 0, 2);
            this.leftMainLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftMainLayout.Location = new System.Drawing.Point(0, 0);
            this.leftMainLayout.Name = "leftMainLayout";
            this.leftMainLayout.RowCount = 4;
            this.leftMainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.leftMainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.leftMainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.leftMainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.leftMainLayout.Size = new System.Drawing.Size(352, 632);
            this.leftMainLayout.TabIndex = 0;
            // 
            // exportAllTexturesBtn
            // 
            this.exportAllTexturesBtn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.exportAllTexturesBtn.Location = new System.Drawing.Point(3, 603);
            this.exportAllTexturesBtn.Name = "exportAllTexturesBtn";
            this.exportAllTexturesBtn.Size = new System.Drawing.Size(346, 26);
            this.exportAllTexturesBtn.TabIndex = 4;
            this.exportAllTexturesBtn.Text = "Export All";
            this.exportAllTexturesBtn.UseVisualStyleBackColor = true;
            this.exportAllTexturesBtn.Click += new System.EventHandler(this.exportAllTexturesBtn_Click);
            // 
            // tableLayoutLeftHeader
            // 
            this.tableLayoutLeftHeader.AutoSize = true;
            this.tableLayoutLeftHeader.ColumnCount = 2;
            this.tableLayoutLeftHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 52F));
            this.tableLayoutLeftHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutLeftHeader.Controls.Add(this.labelSearch, 0, 0);
            this.tableLayoutLeftHeader.Controls.Add(this.textureSearchBox, 1, 0);
            this.tableLayoutLeftHeader.Controls.Add(this.textureSourceLabel, 0, 1);
            this.tableLayoutLeftHeader.Controls.Add(this.textureSourceCombo, 1, 1);
            this.tableLayoutLeftHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutLeftHeader.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutLeftHeader.Name = "tableLayoutLeftHeader";
            this.tableLayoutLeftHeader.RowCount = 2;
            this.tableLayoutLeftHeader.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutLeftHeader.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutLeftHeader.Size = new System.Drawing.Size(346, 56);
            this.tableLayoutLeftHeader.TabIndex = 0;
            // 
            // labelSearch
            // 
            this.labelSearch.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelSearch.AutoSize = true;
            this.labelSearch.Location = new System.Drawing.Point(3, 7);
            this.labelSearch.Name = "labelSearch";
            this.labelSearch.Size = new System.Drawing.Size(44, 13);
            this.labelSearch.TabIndex = 0;
            this.labelSearch.Text = "Search:";
            // 
            // textureSearchBox
            // 
            this.textureSearchBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.textureSearchBox.Location = new System.Drawing.Point(55, 4);
            this.textureSearchBox.Name = "textureSearchBox";
            this.textureSearchBox.Size = new System.Drawing.Size(288, 20);
            this.textureSearchBox.TabIndex = 1;
            this.textureSearchBox.TextChanged += new System.EventHandler(this.textureSearchBox_TextChanged);
            // 
            // textureSourceLabel
            // 
            this.textureSourceLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.textureSourceLabel.AutoSize = true;
            this.textureSourceLabel.Location = new System.Drawing.Point(3, 35);
            this.textureSourceLabel.Name = "textureSourceLabel";
            this.textureSourceLabel.Size = new System.Drawing.Size(44, 13);
            this.textureSourceLabel.TabIndex = 2;
            this.textureSourceLabel.Text = "Source:";
            // 
            // textureSourceCombo
            // 
            this.textureSourceCombo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.textureSourceCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.textureSourceCombo.FormattingEnabled = true;
            this.textureSourceCombo.Location = new System.Drawing.Point(55, 31);
            this.textureSourceCombo.Name = "textureSourceCombo";
            this.textureSourceCombo.Size = new System.Drawing.Size(288, 21);
            this.textureSourceCombo.TabIndex = 3;
            this.textureSourceCombo.SelectedIndexChanged += new System.EventHandler(this.textureSourceCombo_SelectedIndexChanged);
            // 
            // FileTree
            // 
            this.FileTree.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.FileTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FileTree.FullRowSelect = true;
            this.FileTree.HideSelection = false;
            this.FileTree.Location = new System.Drawing.Point(3, 65);
            this.FileTree.Name = "FileTree";
            this.FileTree.Size = new System.Drawing.Size(346, 500);
            this.FileTree.TabIndex = 1;
            this.FileTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.FileTree_AfterSelect);
            // 
            // importTextureBtn
            // 
            this.importTextureBtn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.importTextureBtn.Location = new System.Drawing.Point(3, 571);
            this.importTextureBtn.Name = "importTextureBtn";
            this.importTextureBtn.Size = new System.Drawing.Size(346, 26);
            this.importTextureBtn.TabIndex = 2;
            this.importTextureBtn.Text = "Import New";
            this.importTextureBtn.UseVisualStyleBackColor = true;
            this.importTextureBtn.Click += new System.EventHandler(this.importTextureBtn_Click);
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer2.IsSplitterFixed = true;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.texturePreviewArea);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.textureActionsTable);
            this.splitContainer2.Size = new System.Drawing.Size(701, 632);
            this.splitContainer2.SplitterDistance = 484;
            this.splitContainer2.SplitterWidth = 5;
            this.splitContainer2.TabIndex = 0;
            // 
            // texturePreviewArea
            // 
            this.texturePreviewArea.Controls.Add(this.previewOuterTable);
            this.texturePreviewArea.Dock = System.Windows.Forms.DockStyle.Fill;
            this.texturePreviewArea.Location = new System.Drawing.Point(0, 0);
            this.texturePreviewArea.Name = "texturePreviewArea";
            this.texturePreviewArea.Size = new System.Drawing.Size(484, 632);
            this.texturePreviewArea.TabIndex = 0;
            this.texturePreviewArea.TabStop = false;
            // 
            // previewOuterTable
            // 
            this.previewOuterTable.ColumnCount = 1;
            this.previewOuterTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.previewOuterTable.Controls.Add(this.previewTabControl, 0, 1);
            this.previewOuterTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.previewOuterTable.Location = new System.Drawing.Point(3, 16);
            this.previewOuterTable.Name = "previewOuterTable";
            this.previewOuterTable.RowCount = 2;
            this.previewOuterTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.previewOuterTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.previewOuterTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.previewOuterTable.Size = new System.Drawing.Size(478, 613);
            this.previewOuterTable.TabIndex = 0;
            // 
            // previewTabControl
            // 
            this.previewTabControl.Controls.Add(this.tabStreamed);
            this.previewTabControl.Controls.Add(this.tabPersistent);
            this.previewTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.previewTabControl.Location = new System.Drawing.Point(3, 3);
            this.previewTabControl.Name = "previewTabControl";
            this.previewTabControl.SelectedIndex = 0;
            this.previewTabControl.Size = new System.Drawing.Size(472, 607);
            this.previewTabControl.TabIndex = 1;
            // 
            // tabStreamed
            // 
            this.tabStreamed.Controls.Add(this.streamedTabLayout);
            this.tabStreamed.Location = new System.Drawing.Point(4, 22);
            this.tabStreamed.Name = "tabStreamed";
            this.tabStreamed.Padding = new System.Windows.Forms.Padding(3);
            this.tabStreamed.Size = new System.Drawing.Size(464, 581);
            this.tabStreamed.TabIndex = 0;
            this.tabStreamed.Text = "Streamed";
            this.tabStreamed.UseVisualStyleBackColor = true;
            // 
            // streamedTabLayout
            // 
            this.streamedTabLayout.ColumnCount = 1;
            this.streamedTabLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.streamedTabLayout.Controls.Add(this.pictureStreamed, 0, 0);
            this.streamedTabLayout.Controls.Add(this.groupStreamedMeta, 0, 1);
            this.streamedTabLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.streamedTabLayout.Location = new System.Drawing.Point(3, 3);
            this.streamedTabLayout.Name = "streamedTabLayout";
            this.streamedTabLayout.RowCount = 2;
            this.streamedTabLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.streamedTabLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 108F));
            this.streamedTabLayout.Size = new System.Drawing.Size(458, 575);
            this.streamedTabLayout.TabIndex = 0;
            // 
            // pictureStreamed
            // 
            this.pictureStreamed.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.pictureStreamed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureStreamed.Location = new System.Drawing.Point(3, 3);
            this.pictureStreamed.Name = "pictureStreamed";
            this.pictureStreamed.Size = new System.Drawing.Size(452, 461);
            this.pictureStreamed.TabIndex = 0;
            this.pictureStreamed.TabStop = false;
            // 
            // groupStreamedMeta
            // 
            this.groupStreamedMeta.Controls.Add(this.streamedMetaText);
            this.groupStreamedMeta.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupStreamedMeta.Location = new System.Drawing.Point(3, 470);
            this.groupStreamedMeta.Name = "groupStreamedMeta";
            this.groupStreamedMeta.Size = new System.Drawing.Size(452, 102);
            this.groupStreamedMeta.TabIndex = 1;
            this.groupStreamedMeta.TabStop = false;
            this.groupStreamedMeta.Text = "Metadata";
            // 
            // streamedMetaText
            // 
            this.streamedMetaText.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.streamedMetaText.Dock = System.Windows.Forms.DockStyle.Fill;
            this.streamedMetaText.Location = new System.Drawing.Point(3, 16);
            this.streamedMetaText.Multiline = true;
            this.streamedMetaText.Name = "streamedMetaText";
            this.streamedMetaText.ReadOnly = true;
            this.streamedMetaText.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.streamedMetaText.Size = new System.Drawing.Size(446, 83);
            this.streamedMetaText.TabIndex = 0;
            this.streamedMetaText.TabStop = false;
            // 
            // tabPersistent
            // 
            this.tabPersistent.Controls.Add(this.persistentTabLayout);
            this.tabPersistent.Location = new System.Drawing.Point(4, 22);
            this.tabPersistent.Name = "tabPersistent";
            this.tabPersistent.Padding = new System.Windows.Forms.Padding(3);
            this.tabPersistent.Size = new System.Drawing.Size(464, 581);
            this.tabPersistent.TabIndex = 1;
            this.tabPersistent.Text = "Persistent";
            this.tabPersistent.UseVisualStyleBackColor = true;
            // 
            // persistentTabLayout
            // 
            this.persistentTabLayout.ColumnCount = 1;
            this.persistentTabLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.persistentTabLayout.Controls.Add(this.picturePersistent, 0, 0);
            this.persistentTabLayout.Controls.Add(this.groupPersistentMeta, 0, 1);
            this.persistentTabLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.persistentTabLayout.Location = new System.Drawing.Point(3, 3);
            this.persistentTabLayout.Name = "persistentTabLayout";
            this.persistentTabLayout.RowCount = 2;
            this.persistentTabLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.persistentTabLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 108F));
            this.persistentTabLayout.Size = new System.Drawing.Size(458, 575);
            this.persistentTabLayout.TabIndex = 0;
            // 
            // picturePersistent
            // 
            this.picturePersistent.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.picturePersistent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picturePersistent.Location = new System.Drawing.Point(3, 3);
            this.picturePersistent.Name = "picturePersistent";
            this.picturePersistent.Size = new System.Drawing.Size(452, 461);
            this.picturePersistent.TabIndex = 0;
            this.picturePersistent.TabStop = false;
            // 
            // groupPersistentMeta
            // 
            this.groupPersistentMeta.Controls.Add(this.persistentMetaText);
            this.groupPersistentMeta.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupPersistentMeta.Location = new System.Drawing.Point(3, 470);
            this.groupPersistentMeta.Name = "groupPersistentMeta";
            this.groupPersistentMeta.Size = new System.Drawing.Size(452, 102);
            this.groupPersistentMeta.TabIndex = 1;
            this.groupPersistentMeta.TabStop = false;
            this.groupPersistentMeta.Text = "Metadata";
            // 
            // persistentMetaText
            // 
            this.persistentMetaText.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.persistentMetaText.Dock = System.Windows.Forms.DockStyle.Fill;
            this.persistentMetaText.Location = new System.Drawing.Point(3, 16);
            this.persistentMetaText.Multiline = true;
            this.persistentMetaText.Name = "persistentMetaText";
            this.persistentMetaText.ReadOnly = true;
            this.persistentMetaText.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.persistentMetaText.Size = new System.Drawing.Size(446, 83);
            this.persistentMetaText.TabIndex = 0;
            this.persistentMetaText.TabStop = false;
            // 
            // textureActionsTable
            // 
            this.textureActionsTable.ColumnCount = 1;
            this.textureActionsTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.textureActionsTable.Controls.Add(this.replaceTextureBtn, 0, 0);
            this.textureActionsTable.Controls.Add(this.metadataTable, 0, 4);
            this.textureActionsTable.Controls.Add(this.deleteTextureBtn, 0, 1);
            this.textureActionsTable.Controls.Add(this.exportTextureBtn, 0, 2);
            this.textureActionsTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textureActionsTable.Location = new System.Drawing.Point(0, 0);
            this.textureActionsTable.Name = "textureActionsTable";
            this.textureActionsTable.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this.textureActionsTable.RowCount = 5;
            this.textureActionsTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.textureActionsTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.textureActionsTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.textureActionsTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.textureActionsTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.textureActionsTable.Size = new System.Drawing.Size(212, 632);
            this.textureActionsTable.TabIndex = 0;
            // 
            // replaceTextureBtn
            // 
            this.replaceTextureBtn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.replaceTextureBtn.Enabled = false;
            this.replaceTextureBtn.Location = new System.Drawing.Point(6, 9);
            this.replaceTextureBtn.Name = "replaceTextureBtn";
            this.replaceTextureBtn.Size = new System.Drawing.Size(200, 24);
            this.replaceTextureBtn.TabIndex = 0;
            this.replaceTextureBtn.Text = "Replace Selected";
            this.replaceTextureBtn.UseVisualStyleBackColor = true;
            this.replaceTextureBtn.Click += new System.EventHandler(this.replaceTextureBtn_Click);
            // 
            // metadataTable
            // 
            this.metadataTable.AutoSize = true;
            this.metadataTable.ColumnCount = 1;
            this.metadataTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.metadataTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.metadataTable.Controls.Add(this.metaStateCaption, 0, 0);
            this.metadataTable.Controls.Add(this.stateFlagsPanel, 0, 1);
            this.metadataTable.Controls.Add(this.metaUsageCaption, 0, 2);
            this.metadataTable.Controls.Add(this.usageFlagsPanel, 0, 3);
            this.metadataTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.metadataTable.Location = new System.Drawing.Point(6, 129);
            this.metadataTable.Name = "metadataTable";
            this.metadataTable.RowCount = 4;
            this.metadataTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 18F));
            this.metadataTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 168F));
            this.metadataTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.metadataTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 58F));
            this.metadataTable.Size = new System.Drawing.Size(200, 494);
            this.metadataTable.TabIndex = 0;
            // 
            // metaStateCaption
            // 
            this.metaStateCaption.AutoSize = true;
            this.metaStateCaption.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.metaStateCaption.Location = new System.Drawing.Point(3, 0);
            this.metaStateCaption.Name = "metaStateCaption";
            this.metaStateCaption.Size = new System.Drawing.Size(72, 13);
            this.metaStateCaption.TabIndex = 4;
            this.metaStateCaption.Text = "State flags:";
            // 
            // stateFlagsPanel
            // 
            this.stateFlagsPanel.AutoScroll = true;
            this.stateFlagsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.stateFlagsPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.stateFlagsPanel.Location = new System.Drawing.Point(0, 18);
            this.stateFlagsPanel.Margin = new System.Windows.Forms.Padding(0);
            this.stateFlagsPanel.Name = "stateFlagsPanel";
            this.stateFlagsPanel.Padding = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.stateFlagsPanel.Size = new System.Drawing.Size(200, 168);
            this.stateFlagsPanel.TabIndex = 9;
            this.stateFlagsPanel.WrapContents = false;
            // 
            // metaUsageCaption
            // 
            this.metaUsageCaption.AutoSize = true;
            this.metaUsageCaption.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.metaUsageCaption.Location = new System.Drawing.Point(3, 186);
            this.metaUsageCaption.Name = "metaUsageCaption";
            this.metaUsageCaption.Size = new System.Drawing.Size(78, 13);
            this.metaUsageCaption.TabIndex = 6;
            this.metaUsageCaption.Text = "Usage flags:";
            // 
            // usageFlagsPanel
            // 
            this.usageFlagsPanel.AutoScroll = true;
            this.usageFlagsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.usageFlagsPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.usageFlagsPanel.Location = new System.Drawing.Point(0, 206);
            this.usageFlagsPanel.Margin = new System.Windows.Forms.Padding(0);
            this.usageFlagsPanel.Name = "usageFlagsPanel";
            this.usageFlagsPanel.Padding = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.usageFlagsPanel.Size = new System.Drawing.Size(200, 288);
            this.usageFlagsPanel.TabIndex = 10;
            this.usageFlagsPanel.WrapContents = false;
            // 
            // deleteTextureBtn
            // 
            this.deleteTextureBtn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.deleteTextureBtn.Enabled = false;
            this.deleteTextureBtn.Location = new System.Drawing.Point(6, 39);
            this.deleteTextureBtn.Name = "deleteTextureBtn";
            this.deleteTextureBtn.Size = new System.Drawing.Size(200, 24);
            this.deleteTextureBtn.TabIndex = 1;
            this.deleteTextureBtn.Text = "Delete Selected";
            this.deleteTextureBtn.UseVisualStyleBackColor = true;
            this.deleteTextureBtn.Click += new System.EventHandler(this.deleteTextureBtn_Click);
            // 
            // exportTextureBtn
            // 
            this.exportTextureBtn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.exportTextureBtn.Enabled = false;
            this.exportTextureBtn.Location = new System.Drawing.Point(6, 69);
            this.exportTextureBtn.Name = "exportTextureBtn";
            this.exportTextureBtn.Size = new System.Drawing.Size(200, 24);
            this.exportTextureBtn.TabIndex = 2;
            this.exportTextureBtn.Text = "Export Selected";
            this.exportTextureBtn.UseVisualStyleBackColor = true;
            this.exportTextureBtn.Click += new System.EventHandler(this.exportTextureBtn_Click);
            // 
            // selectTextureBtn
            // 
            this.selectTextureBtn.Enabled = false;
            this.selectTextureBtn.Location = new System.Drawing.Point(854, 604);
            this.selectTextureBtn.Name = "selectTextureBtn";
            this.selectTextureBtn.Size = new System.Drawing.Size(211, 38);
            this.selectTextureBtn.TabIndex = 0;
            this.selectTextureBtn.Text = "Select This Texture";
            this.selectTextureBtn.UseVisualStyleBackColor = true;
            this.selectTextureBtn.Click += new System.EventHandler(this.selectTextureBtn_Click);
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "Folder Icon.png");
            this.imageList1.Images.SetKeyName(1, "file_icon.png");
            this.imageList1.Images.SetKeyName(2, "FolderOpened Icon.png");
            // 
            // EditTexture
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1069, 647);
            this.Controls.Add(this.selectTextureBtn);
            this.Controls.Add(this.splitContainer1);
            this.DoubleBuffered = true;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "EditTexture";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Texture Editor";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.leftMainLayout.ResumeLayout(false);
            this.leftMainLayout.PerformLayout();
            this.tableLayoutLeftHeader.ResumeLayout(false);
            this.tableLayoutLeftHeader.PerformLayout();
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.texturePreviewArea.ResumeLayout(false);
            this.previewOuterTable.ResumeLayout(false);
            this.previewTabControl.ResumeLayout(false);
            this.tabStreamed.ResumeLayout(false);
            this.streamedTabLayout.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureStreamed)).EndInit();
            this.groupStreamedMeta.ResumeLayout(false);
            this.groupStreamedMeta.PerformLayout();
            this.tabPersistent.ResumeLayout(false);
            this.persistentTabLayout.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picturePersistent)).EndInit();
            this.groupPersistentMeta.ResumeLayout(false);
            this.groupPersistentMeta.PerformLayout();
            this.textureActionsTable.ResumeLayout(false);
            this.textureActionsTable.PerformLayout();
            this.metadataTable.ResumeLayout(false);
            this.metadataTable.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel leftMainLayout;
        private System.Windows.Forms.TableLayoutPanel tableLayoutLeftHeader;
        private System.Windows.Forms.Label labelSearch;
        private System.Windows.Forms.TextBox textureSearchBox;
        private System.Windows.Forms.Label textureSourceLabel;
        private System.Windows.Forms.ComboBox textureSourceCombo;
        private System.Windows.Forms.TreeView FileTree;
        private System.Windows.Forms.Button importTextureBtn;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.GroupBox texturePreviewArea;
        private System.Windows.Forms.TableLayoutPanel previewOuterTable;
        private System.Windows.Forms.TableLayoutPanel metadataTable;
        private System.Windows.Forms.Label metaStateCaption;
        private System.Windows.Forms.TabControl previewTabControl;
        private System.Windows.Forms.TabPage tabStreamed;
        private System.Windows.Forms.TabPage tabPersistent;
        private System.Windows.Forms.TableLayoutPanel streamedTabLayout;
        private System.Windows.Forms.PictureBox pictureStreamed;
        private System.Windows.Forms.GroupBox groupStreamedMeta;
        private System.Windows.Forms.TextBox streamedMetaText;
        private System.Windows.Forms.TableLayoutPanel persistentTabLayout;
        private System.Windows.Forms.PictureBox picturePersistent;
        private System.Windows.Forms.GroupBox groupPersistentMeta;
        private System.Windows.Forms.TextBox persistentMetaText;
        private System.Windows.Forms.Button selectTextureBtn;
        private System.Windows.Forms.TableLayoutPanel textureActionsTable;
        private System.Windows.Forms.Button replaceTextureBtn;
        private System.Windows.Forms.Button deleteTextureBtn;
        private System.Windows.Forms.Button exportTextureBtn;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.FlowLayoutPanel stateFlagsPanel;
        private System.Windows.Forms.Label metaUsageCaption;
        private System.Windows.Forms.FlowLayoutPanel usageFlagsPanel;
        private System.Windows.Forms.Button exportAllTexturesBtn;
    }
}
