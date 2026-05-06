namespace CommandsEditor
{
    partial class ModifyParameters
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ModifyParameters));
            System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Reference Pin", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("Target Pin", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup3 = new System.Windows.Forms.ListViewGroup("State Parameter", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup4 = new System.Windows.Forms.ListViewGroup("Input Pin", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup5 = new System.Windows.Forms.ListViewGroup("Output Pin", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup6 = new System.Windows.Forms.ListViewGroup("Parameter", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup7 = new System.Windows.Forms.ListViewGroup("Internal", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup8 = new System.Windows.Forms.ListViewGroup("Method Function", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup9 = new System.Windows.Forms.ListViewGroup("Method Pin", System.Windows.Forms.HorizontalAlignment.Left);
            this.createParams = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.searchText = new System.Windows.Forms.TextBox();
            this.clearSearchBtn = new System.Windows.Forms.Button();
            this.typesCount = new System.Windows.Forms.Label();
            this.param_name = new System.Windows.Forms.ListView();
            this.funcHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.inheritHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listIcons = new System.Windows.Forms.ImageList(this.components);
            this.helpBtn = new System.Windows.Forms.Button();
            this.selectAll = new System.Windows.Forms.Button();
            this.deSelectAll = new System.Windows.Forms.Button();
            this.AddCustom = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // createParams
            // 
            this.createParams.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.createParams.Location = new System.Drawing.Point(472, 377);
            this.createParams.Name = "createParams";
            this.createParams.Size = new System.Drawing.Size(169, 23);
            this.createParams.TabIndex = 6;
            this.createParams.Text = "Set Checked As Pins In";
            this.createParams.UseVisualStyleBackColor = true;
            this.createParams.Click += new System.EventHandler(this.createParams_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(12, 10);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(59, 18);
            this.label2.TabIndex = 12;
            this.label2.Text = "Pins In";
            // 
            // searchText
            // 
            this.searchText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.searchText.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.searchText.Location = new System.Drawing.Point(15, 35);
            this.searchText.Name = "searchText";
            this.searchText.Size = new System.Drawing.Size(607, 20);
            this.searchText.TabIndex = 2;
            this.searchText.TextChanged += new System.EventHandler(this.searchText_TextChanged);
            // 
            // clearSearchBtn
            // 
            this.clearSearchBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.clearSearchBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.clearSearchBtn.Image = ((System.Drawing.Image)(resources.GetObject("clearSearchBtn.Image")));
            this.clearSearchBtn.Location = new System.Drawing.Point(621, 35);
            this.clearSearchBtn.Name = "clearSearchBtn";
            this.clearSearchBtn.Size = new System.Drawing.Size(20, 20);
            this.clearSearchBtn.TabIndex = 3;
            this.clearSearchBtn.UseVisualStyleBackColor = true;
            this.clearSearchBtn.Click += new System.EventHandler(this.clearSearchBtn_Click);
            // 
            // typesCount
            // 
            this.typesCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.typesCount.AutoSize = true;
            this.typesCount.Location = new System.Drawing.Point(537, 15);
            this.typesCount.Name = "typesCount";
            this.typesCount.Size = new System.Drawing.Size(89, 13);
            this.typesCount.TabIndex = 179;
            this.typesCount.Text = "Showing 0 Types";
            this.typesCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // param_name
            // 
            this.param_name.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.param_name.CheckBoxes = true;
            this.param_name.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.funcHeader,
            this.inheritHeader});
            this.param_name.FullRowSelect = true;
            listViewGroup1.Header = "Reference Pin";
            listViewGroup1.Name = "REFERENCE_PIN";
            listViewGroup2.Header = "Target Pin";
            listViewGroup2.Name = "TARGET_PIN";
            listViewGroup3.Header = "State Parameter";
            listViewGroup3.Name = "STATE_PARAMETER";
            listViewGroup4.Header = "Input Pin";
            listViewGroup4.Name = "INPUT_PIN";
            listViewGroup5.Header = "Output Pin";
            listViewGroup5.Name = "OUTPUT_PIN";
            listViewGroup6.Header = "Parameter";
            listViewGroup6.Name = "PARAMETER";
            listViewGroup7.Header = "Internal";
            listViewGroup7.Name = "INTERNAL";
            listViewGroup8.Header = "Method Function";
            listViewGroup8.Name = "METHOD_FUNCTION";
            listViewGroup9.Header = "Method Pin";
            listViewGroup9.Name = "METHOD_PIN";
            this.param_name.Groups.AddRange(new System.Windows.Forms.ListViewGroup[] {
            listViewGroup1,
            listViewGroup2,
            listViewGroup3,
            listViewGroup4,
            listViewGroup5,
            listViewGroup6,
            listViewGroup7,
            listViewGroup8,
            listViewGroup9});
            this.param_name.HideSelection = false;
            this.param_name.LargeImageList = this.listIcons;
            this.param_name.Location = new System.Drawing.Point(15, 54);
            this.param_name.MultiSelect = false;
            this.param_name.Name = "param_name";
            this.param_name.Size = new System.Drawing.Size(626, 317);
            this.param_name.SmallImageList = this.listIcons;
            this.param_name.TabIndex = 180;
            this.param_name.UseCompatibleStateImageBehavior = false;
            this.param_name.View = System.Windows.Forms.View.Details;
            this.param_name.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.ColumnClick);
            // 
            // funcHeader
            // 
            this.funcHeader.Text = "Parameter";
            this.funcHeader.Width = 364;
            // 
            // inheritHeader
            // 
            this.inheritHeader.Text = "Datatype";
            this.inheritHeader.Width = 232;
            // 
            // listIcons
            // 
            this.listIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("listIcons.ImageStream")));
            this.listIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.listIcons.Images.SetKeyName(0, "Database.ico");
            // 
            // helpBtn
            // 
            this.helpBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.helpBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.helpBtn.Image = ((System.Drawing.Image)(resources.GetObject("helpBtn.Image")));
            this.helpBtn.Location = new System.Drawing.Point(633, 0);
            this.helpBtn.Name = "helpBtn";
            this.helpBtn.Size = new System.Drawing.Size(20, 20);
            this.helpBtn.TabIndex = 181;
            this.helpBtn.UseVisualStyleBackColor = true;
            this.helpBtn.Click += new System.EventHandler(this.helpBtn_Click);
            // 
            // selectAll
            // 
            this.selectAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.selectAll.Location = new System.Drawing.Point(15, 377);
            this.selectAll.Name = "selectAll";
            this.selectAll.Size = new System.Drawing.Size(94, 23);
            this.selectAll.TabIndex = 183;
            this.selectAll.Text = "Select All";
            this.selectAll.UseVisualStyleBackColor = true;
            this.selectAll.Click += new System.EventHandler(this.selectAll_Click);
            // 
            // deSelectAll
            // 
            this.deSelectAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.deSelectAll.Location = new System.Drawing.Point(115, 377);
            this.deSelectAll.Name = "deSelectAll";
            this.deSelectAll.Size = new System.Drawing.Size(94, 23);
            this.deSelectAll.TabIndex = 184;
            this.deSelectAll.Text = "De-select All";
            this.deSelectAll.UseVisualStyleBackColor = true;
            this.deSelectAll.Click += new System.EventHandler(this.deSelectAll_Click);
            // 
            // AddCustom
            // 
            this.AddCustom.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.AddCustom.Location = new System.Drawing.Point(215, 377);
            this.AddCustom.Name = "AddCustom";
            this.AddCustom.Size = new System.Drawing.Size(94, 23);
            this.AddCustom.TabIndex = 185;
            this.AddCustom.Text = "Add Custom";
            this.AddCustom.UseVisualStyleBackColor = true;
            this.AddCustom.Click += new System.EventHandler(this.AddCustom_Click);
            // 
            // ModifyPinsOrParameters
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(653, 412);
            this.Controls.Add(this.AddCustom);
            this.Controls.Add(this.deSelectAll);
            this.Controls.Add(this.selectAll);
            this.Controls.Add(this.helpBtn);
            this.Controls.Add(this.param_name);
            this.Controls.Add(this.typesCount);
            this.Controls.Add(this.clearSearchBtn);
            this.Controls.Add(this.searchText);
            this.Controls.Add(this.createParams);
            this.Controls.Add(this.label2);
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MinimumSize = new System.Drawing.Size(514, 192);
            this.Name = "ModifyPinsOrParameters";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Add Parameter";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button createParams;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox searchText;
        private System.Windows.Forms.Button clearSearchBtn;
        private System.Windows.Forms.Label typesCount;
        private System.Windows.Forms.ListView param_name;
        private System.Windows.Forms.ColumnHeader funcHeader;
        private System.Windows.Forms.ColumnHeader inheritHeader;
        private System.Windows.Forms.ImageList listIcons;
        private System.Windows.Forms.Button helpBtn;
        private System.Windows.Forms.Button selectAll;
        private System.Windows.Forms.Button deSelectAll;
        private System.Windows.Forms.Button AddCustom;
    }
}
