namespace AlienPAK
{
    partial class AlienPAK_Imported
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AlienPAK_Imported));
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.FileTree = new System.Windows.Forms.TreeView();
            this.fileContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.importFileContext = new System.Windows.Forms.ToolStripMenuItem();
            this.exportFileContext = new System.Windows.Forms.ToolStripMenuItem();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.filePreviewImage = new System.Windows.Forms.PictureBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.fileTypeInfo = new System.Windows.Forms.Label();
            this.fileSizeInfo = new System.Windows.Forms.Label();
            this.fileNameInfo = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.importFile = new System.Windows.Forms.Button();
            this.exportFile = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.mapToLoadContentFrom = new System.Windows.Forms.ComboBox();
            this.fileContextMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.filePreviewImage)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.SuspendLayout();
            // 
            // FileTree
            // 
            this.FileTree.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.FileTree.Location = new System.Drawing.Point(-1, 1);
            this.FileTree.Name = "FileTree";
            this.FileTree.Size = new System.Drawing.Size(500, 679);
            this.FileTree.TabIndex = 5;
            this.FileTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.FileTree_AfterSelect);
            // 
            // fileContextMenu
            // 
            this.fileContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.importFileContext,
            this.exportFileContext});
            this.fileContextMenu.Name = "fileContextMenu";
            this.fileContextMenu.Size = new System.Drawing.Size(132, 48);
            // 
            // importFileContext
            // 
            this.importFileContext.Name = "importFileContext";
            this.importFileContext.Size = new System.Drawing.Size(131, 22);
            this.importFileContext.Text = "Import File";
            this.importFileContext.Click += new System.EventHandler(this.importFileContext_Click);
            // 
            // exportFileContext
            // 
            this.exportFileContext.Name = "exportFileContext";
            this.exportFileContext.Size = new System.Drawing.Size(131, 22);
            this.exportFileContext.Text = "Export File";
            this.exportFileContext.Click += new System.EventHandler(this.exportFileContext_Click);
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "directory_icon.png");
            this.imageList1.Images.SetKeyName(1, "file_icon.png");
            this.imageList1.Images.SetKeyName(2, "text_icon.png");
            // 
            // filePreviewImage
            // 
            this.filePreviewImage.Location = new System.Drawing.Point(6, 19);
            this.filePreviewImage.Name = "filePreviewImage";
            this.filePreviewImage.Size = new System.Drawing.Size(265, 242);
            this.filePreviewImage.TabIndex = 7;
            this.filePreviewImage.TabStop = false;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.filePreviewImage);
            this.groupBox1.Location = new System.Drawing.Point(505, 4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(277, 267);
            this.groupBox1.TabIndex = 8;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "File Preview";
            this.groupBox1.Visible = false;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.fileTypeInfo);
            this.groupBox2.Controls.Add(this.fileSizeInfo);
            this.groupBox2.Controls.Add(this.fileNameInfo);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Location = new System.Drawing.Point(505, 277);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(277, 96);
            this.groupBox2.TabIndex = 9;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "File Info";
            // 
            // fileTypeInfo
            // 
            this.fileTypeInfo.AutoSize = true;
            this.fileTypeInfo.Location = new System.Drawing.Point(55, 61);
            this.fileTypeInfo.Name = "fileTypeInfo";
            this.fileTypeInfo.Size = new System.Drawing.Size(0, 13);
            this.fileTypeInfo.TabIndex = 15;
            // 
            // fileSizeInfo
            // 
            this.fileSizeInfo.AutoSize = true;
            this.fileSizeInfo.Location = new System.Drawing.Point(55, 43);
            this.fileSizeInfo.Name = "fileSizeInfo";
            this.fileSizeInfo.Size = new System.Drawing.Size(0, 13);
            this.fileSizeInfo.TabIndex = 14;
            // 
            // fileNameInfo
            // 
            this.fileNameInfo.AutoSize = true;
            this.fileNameInfo.Location = new System.Drawing.Point(55, 26);
            this.fileNameInfo.Name = "fileNameInfo";
            this.fileNameInfo.Size = new System.Drawing.Size(0, 13);
            this.fileNameInfo.TabIndex = 13;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(15, 60);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(39, 13);
            this.label3.TabIndex = 12;
            this.label3.Text = "Type:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(19, 43);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(35, 13);
            this.label2.TabIndex = 11;
            this.label2.Text = "Size:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(11, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(43, 13);
            this.label1.TabIndex = 10;
            this.label1.Text = "Name:";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.importFile);
            this.groupBox3.Controls.Add(this.exportFile);
            this.groupBox3.Location = new System.Drawing.Point(505, 379);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(277, 57);
            this.groupBox3.TabIndex = 10;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "File Utilities";
            // 
            // importFile
            // 
            this.importFile.Enabled = false;
            this.importFile.Location = new System.Drawing.Point(139, 21);
            this.importFile.Name = "importFile";
            this.importFile.Size = new System.Drawing.Size(132, 24);
            this.importFile.TabIndex = 1;
            this.importFile.Text = "Import";
            this.importFile.UseVisualStyleBackColor = true;
            this.importFile.Click += new System.EventHandler(this.importFile_Click);
            // 
            // exportFile
            // 
            this.exportFile.Enabled = false;
            this.exportFile.Location = new System.Drawing.Point(6, 21);
            this.exportFile.Name = "exportFile";
            this.exportFile.Size = new System.Drawing.Size(132, 24);
            this.exportFile.TabIndex = 0;
            this.exportFile.Text = "Export";
            this.exportFile.UseVisualStyleBackColor = true;
            this.exportFile.Click += new System.EventHandler(this.exportFile_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.mapToLoadContentFrom);
            this.groupBox4.Location = new System.Drawing.Point(505, 621);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(277, 48);
            this.groupBox4.TabIndex = 12;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Select Map to Load Content From";
            // 
            // mapToLoadContentFrom
            // 
            this.mapToLoadContentFrom.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.mapToLoadContentFrom.FormattingEnabled = true;
            this.mapToLoadContentFrom.Location = new System.Drawing.Point(6, 19);
            this.mapToLoadContentFrom.Name = "mapToLoadContentFrom";
            this.mapToLoadContentFrom.Size = new System.Drawing.Size(265, 21);
            this.mapToLoadContentFrom.TabIndex = 0;
            this.mapToLoadContentFrom.SelectedIndexChanged += new System.EventHandler(this.mapToLoadContentFrom_SelectedIndexChanged);
            // 
            // AlienPAK_Imported
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(789, 681);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.FileTree);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "AlienPAK_Imported";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation PAK Tool";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.fileContextMenu.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.filePreviewImage)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.TreeView FileTree;
        private System.Windows.Forms.ContextMenuStrip fileContextMenu;
        private System.Windows.Forms.ToolStripMenuItem exportFileContext;
        private System.Windows.Forms.ToolStripMenuItem importFileContext;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.PictureBox filePreviewImage;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label fileTypeInfo;
        private System.Windows.Forms.Label fileSizeInfo;
        private System.Windows.Forms.Label fileNameInfo;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button importFile;
        private System.Windows.Forms.Button exportFile;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.ComboBox mapToLoadContentFrom;
    }
}