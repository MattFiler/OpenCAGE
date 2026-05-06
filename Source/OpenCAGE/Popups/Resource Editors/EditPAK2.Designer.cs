namespace CommandsEditor
{
    partial class EditPAK2
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditPAK2));
            this.listEntries = new System.Windows.Forms.ListView();
            this.colPath = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panelActions = new System.Windows.Forms.Panel();
            this.btnReplace = new System.Windows.Forms.Button();
            this.btnExportAll = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnImport = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.searchBtn = new System.Windows.Forms.Button();
            this.searchTxt = new System.Windows.Forms.TextBox();
            this.panelActions.SuspendLayout();
            this.SuspendLayout();
            // 
            // listEntries
            // 
            this.listEntries.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listEntries.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colPath,
            this.colSize});
            this.listEntries.FullRowSelect = true;
            this.listEntries.GridLines = true;
            this.listEntries.HideSelection = false;
            this.listEntries.Location = new System.Drawing.Point(0, 37);
            this.listEntries.MultiSelect = false;
            this.listEntries.Name = "listEntries";
            this.listEntries.Size = new System.Drawing.Size(884, 454);
            this.listEntries.TabIndex = 1;
            this.listEntries.UseCompatibleStateImageBehavior = false;
            this.listEntries.View = System.Windows.Forms.View.Details;
            this.listEntries.SelectedIndexChanged += new System.EventHandler(this.listEntries_SelectedIndexChanged);
            // 
            // colPath
            // 
            this.colPath.Text = "Path in archive";
            this.colPath.Width = 620;
            // 
            // colSize
            // 
            this.colSize.Text = "Size (bytes)";
            this.colSize.Width = 120;
            // 
            // panelActions
            // 
            this.panelActions.Controls.Add(this.btnReplace);
            this.panelActions.Controls.Add(this.btnExportAll);
            this.panelActions.Controls.Add(this.btnDelete);
            this.panelActions.Controls.Add(this.btnImport);
            this.panelActions.Controls.Add(this.btnExport);
            this.panelActions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelActions.Location = new System.Drawing.Point(0, 491);
            this.panelActions.Name = "panelActions";
            this.panelActions.Padding = new System.Windows.Forms.Padding(8, 6, 8, 8);
            this.panelActions.Size = new System.Drawing.Size(884, 44);
            this.panelActions.TabIndex = 2;
            // 
            // btnReplace
            // 
            this.btnReplace.Location = new System.Drawing.Point(394, 8);
            this.btnReplace.Name = "btnReplace";
            this.btnReplace.Size = new System.Drawing.Size(123, 28);
            this.btnReplace.TabIndex = 5;
            this.btnReplace.Text = "Replace Selected";
            this.btnReplace.UseVisualStyleBackColor = true;
            this.btnReplace.Click += new System.EventHandler(this.btnReplace_Click);
            // 
            // btnExportAll
            // 
            this.btnExportAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExportAll.Location = new System.Drawing.Point(753, 8);
            this.btnExportAll.Name = "btnExportAll";
            this.btnExportAll.Size = new System.Drawing.Size(120, 28);
            this.btnExportAll.TabIndex = 4;
            this.btnExportAll.Text = "Export All";
            this.btnExportAll.UseVisualStyleBackColor = true;
            this.btnExportAll.Click += new System.EventHandler(this.btnExportAll_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.Location = new System.Drawing.Point(265, 8);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(123, 28);
            this.btnDelete.TabIndex = 3;
            this.btnDelete.Text = "Delete Selected";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnImport
            // 
            this.btnImport.Location = new System.Drawing.Point(11, 8);
            this.btnImport.Name = "btnImport";
            this.btnImport.Size = new System.Drawing.Size(123, 28);
            this.btnImport.TabIndex = 2;
            this.btnImport.Text = "Import New File";
            this.btnImport.UseVisualStyleBackColor = true;
            this.btnImport.Click += new System.EventHandler(this.btnImport_Click);
            // 
            // btnExport
            // 
            this.btnExport.Location = new System.Drawing.Point(137, 8);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(123, 28);
            this.btnExport.TabIndex = 0;
            this.btnExport.Text = "Export Selected";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // searchBtn
            // 
            this.searchBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.searchBtn.Location = new System.Drawing.Point(753, 8);
            this.searchBtn.Name = "searchBtn";
            this.searchBtn.Size = new System.Drawing.Size(120, 22);
            this.searchBtn.TabIndex = 5;
            this.searchBtn.Text = "Search";
            this.searchBtn.UseVisualStyleBackColor = true;
            this.searchBtn.Click += new System.EventHandler(this.searchBtn_Click);
            // 
            // searchTxt
            // 
            this.searchTxt.Location = new System.Drawing.Point(12, 9);
            this.searchTxt.Name = "searchTxt";
            this.searchTxt.Size = new System.Drawing.Size(734, 20);
            this.searchTxt.TabIndex = 6;
            this.searchTxt.KeyDown += new System.Windows.Forms.KeyEventHandler(this.SearchOnEnter);
            // 
            // EditPAK2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 535);
            this.Controls.Add(this.searchTxt);
            this.Controls.Add(this.searchBtn);
            this.Controls.Add(this.listEntries);
            this.Controls.Add(this.panelActions);
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(670, 417);
            this.Name = "EditPAK2";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Edit UI";
            this.panelActions.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ListView listEntries;
        private System.Windows.Forms.ColumnHeader colPath;
        private System.Windows.Forms.ColumnHeader colSize;
        private System.Windows.Forms.Panel panelActions;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnImport;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnExportAll;
        private System.Windows.Forms.Button btnReplace;
        private System.Windows.Forms.Button searchBtn;
        private System.Windows.Forms.TextBox searchTxt;
    }
}
