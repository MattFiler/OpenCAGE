namespace CommandsEditor
{
    partial class SelectComposite
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SelectComposite));
            this.selectComp = new System.Windows.Forms.Button();
            this.FileTree = new System.Windows.Forms.TreeView();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.SuspendLayout();
            // 
            // selectComp
            // 
            this.selectComp.Location = new System.Drawing.Point(285, 633);
            this.selectComp.Name = "selectComp";
            this.selectComp.Size = new System.Drawing.Size(171, 23);
            this.selectComp.TabIndex = 147;
            this.selectComp.Text = "Select Composite";
            this.selectComp.UseVisualStyleBackColor = true;
            this.selectComp.Click += new System.EventHandler(this.SelectEntity_Click);
            // 
            // FileTree
            // 
            this.FileTree.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.FileTree.FullRowSelect = true;
            this.FileTree.HideSelection = false;
            this.FileTree.ImageIndex = 0;
            this.FileTree.ImageList = this.imageList1;
            this.FileTree.Location = new System.Drawing.Point(12, 12);
            this.FileTree.Name = "FileTree";
            this.FileTree.SelectedImageIndex = 0;
            this.FileTree.Size = new System.Drawing.Size(444, 615);
            this.FileTree.TabIndex = 148;
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "Folder Icon.png");
            this.imageList1.Images.SetKeyName(1, "d_Prefab Icon.png");
            this.imageList1.Images.SetKeyName(2, "FolderOpened Icon.png");
            this.imageList1.Images.SetKeyName(3, "globe.png");
            this.imageList1.Images.SetKeyName(4, "cog.png");
            this.imageList1.Images.SetKeyName(5, "Avatar Icon.png");
            // 
            // SelectComposite
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(468, 664);
            this.Controls.Add(this.FileTree);
            this.Controls.Add(this.selectComp);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "SelectComposite";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Select Composite";
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button selectComp;
        private System.Windows.Forms.TreeView FileTree;
        private System.Windows.Forms.ImageList imageList1;
    }
}
