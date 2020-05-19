namespace Alien_Isolation_Mod_Tools
{
    partial class SoundTool
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SoundTool));
            this.FileTree = new System.Windows.Forms.TreeView();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.fileContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.importFileContext = new System.Windows.Forms.ToolStripMenuItem();
            this.exportFileContext = new System.Windows.Forms.ToolStripMenuItem();
            this.soundPreview = new NAudio.Gui.WaveViewer();
            this.fileContextMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // FileTree
            // 
            this.FileTree.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.FileTree.ImageIndex = 0;
            this.FileTree.ImageList = this.imageList1;
            this.FileTree.Location = new System.Drawing.Point(-1, 1);
            this.FileTree.Name = "FileTree";
            this.FileTree.SelectedImageIndex = 0;
            this.FileTree.Size = new System.Drawing.Size(500, 679);
            this.FileTree.TabIndex = 6;
            this.FileTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.FileTree_AfterSelect);
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "directory_icon.png");
            this.imageList1.Images.SetKeyName(1, "file_icon.png");
            this.imageList1.Images.SetKeyName(2, "text_icon.png");
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
            // 
            // exportFileContext
            // 
            this.exportFileContext.Name = "exportFileContext";
            this.exportFileContext.Size = new System.Drawing.Size(131, 22);
            this.exportFileContext.Text = "Export File";
            // 
            // soundPreview
            // 
            this.soundPreview.Location = new System.Drawing.Point(505, 12);
            this.soundPreview.Name = "soundPreview";
            this.soundPreview.SamplesPerPixel = 128;
            this.soundPreview.Size = new System.Drawing.Size(272, 133);
            this.soundPreview.StartPosition = ((long)(0));
            this.soundPreview.TabIndex = 17;
            this.soundPreview.Visible = false;
            this.soundPreview.WaveStream = null;
            // 
            // SoundTool
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(789, 681);
            this.Controls.Add(this.soundPreview);
            this.Controls.Add(this.FileTree);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "SoundTool";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OpenCAGE - Sound Tool";
            this.fileContextMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TreeView FileTree;
        private System.Windows.Forms.ContextMenuStrip fileContextMenu;
        private System.Windows.Forms.ToolStripMenuItem importFileContext;
        private System.Windows.Forms.ToolStripMenuItem exportFileContext;
        private System.Windows.Forms.ImageList imageList1;
        private NAudio.Gui.WaveViewer soundPreview;
    }
}