namespace CommandsEditor.ConfigEditors
{
    partial class GraphicsEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GraphicsEditor));
            this.btnSave = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.fovPresets = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.add_fov = new System.Windows.Forms.Button();
            this.remove_fov = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.windowedResolutionPresets = new System.Windows.Forms.ListView();
            this.Name = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Width = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Height = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.remove_res = new System.Windows.Forms.Button();
            this.add_res = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.shadowMapResolutionPresets = new System.Windows.Forms.ListView();
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.remove_shadowmap = new System.Windows.Forms.Button();
            this.add_shadowmap = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.shadowMapFilterQualityPresets = new System.Windows.Forms.ListView();
            this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader6 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.remove_filter = new System.Windows.Forms.Button();
            this.add_filter = new System.Windows.Forms.Button();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.lodPresets = new System.Windows.Forms.ListView();
            this.columnHeader7 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader8 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.remove_lod = new System.Windows.Forms.Button();
            this.add_lod = new System.Windows.Forms.Button();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.checkHighGloss = new System.Windows.Forms.CheckBox();
            this.groupBox8 = new System.Windows.Forms.GroupBox();
            this.checkAnaglph = new System.Windows.Forms.CheckBox();
            this.check3D = new System.Windows.Forms.CheckBox();
            this.checkRift = new System.Windows.Forms.CheckBox();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.groupBox8.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(846, 637);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(136, 33);
            this.btnSave.TabIndex = 354;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click_1);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.fovPresets);
            this.groupBox2.Controls.Add(this.add_fov);
            this.groupBox2.Controls.Add(this.remove_fov);
            this.groupBox2.Location = new System.Drawing.Point(450, 17);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(339, 411);
            this.groupBox2.TabIndex = 359;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Field of View Presets";
            // 
            // fovPresets
            // 
            this.fovPresets.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
            this.fovPresets.FullRowSelect = true;
            this.fovPresets.HideSelection = false;
            this.fovPresets.Location = new System.Drawing.Point(6, 19);
            this.fovPresets.MultiSelect = false;
            this.fovPresets.Name = "fovPresets";
            this.fovPresets.Size = new System.Drawing.Size(327, 355);
            this.fovPresets.TabIndex = 365;
            this.fovPresets.UseCompatibleStateImageBehavior = false;
            this.fovPresets.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Name";
            this.columnHeader1.Width = 150;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "FOV";
            this.columnHeader2.Width = 69;
            // 
            // add_fov
            // 
            this.add_fov.Location = new System.Drawing.Point(224, 379);
            this.add_fov.Name = "add_fov";
            this.add_fov.Size = new System.Drawing.Size(109, 25);
            this.add_fov.TabIndex = 336;
            this.add_fov.Text = "Add New";
            this.add_fov.UseVisualStyleBackColor = true;
            this.add_fov.Click += new System.EventHandler(this.add_fov_Click);
            // 
            // remove_fov
            // 
            this.remove_fov.Location = new System.Drawing.Point(6, 379);
            this.remove_fov.Name = "remove_fov";
            this.remove_fov.Size = new System.Drawing.Size(110, 25);
            this.remove_fov.TabIndex = 337;
            this.remove_fov.Text = "Remove Selected";
            this.remove_fov.UseVisualStyleBackColor = true;
            this.remove_fov.Click += new System.EventHandler(this.remove_fov_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.windowedResolutionPresets);
            this.groupBox1.Controls.Add(this.remove_res);
            this.groupBox1.Controls.Add(this.add_res);
            this.groupBox1.Location = new System.Drawing.Point(12, 17);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(432, 411);
            this.groupBox1.TabIndex = 358;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Windowed Resolution Presets";
            // 
            // windowedResolutionPresets
            // 
            this.windowedResolutionPresets.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.Name,
            this.Width,
            this.Height});
            this.windowedResolutionPresets.FullRowSelect = true;
            this.windowedResolutionPresets.HideSelection = false;
            this.windowedResolutionPresets.Location = new System.Drawing.Point(6, 19);
            this.windowedResolutionPresets.MultiSelect = false;
            this.windowedResolutionPresets.Name = "windowedResolutionPresets";
            this.windowedResolutionPresets.Size = new System.Drawing.Size(420, 355);
            this.windowedResolutionPresets.TabIndex = 364;
            this.windowedResolutionPresets.UseCompatibleStateImageBehavior = false;
            this.windowedResolutionPresets.View = System.Windows.Forms.View.Details;
            // 
            // Name
            // 
            this.Name.Text = "Name";
            this.Name.Width = 163;
            // 
            // Width
            // 
            this.Width.Text = "Width";
            this.Width.Width = 69;
            // 
            // Height
            // 
            this.Height.Text = "Height";
            this.Height.Width = 69;
            // 
            // remove_res
            // 
            this.remove_res.Location = new System.Drawing.Point(6, 379);
            this.remove_res.Name = "remove_res";
            this.remove_res.Size = new System.Drawing.Size(110, 25);
            this.remove_res.TabIndex = 337;
            this.remove_res.Text = "Remove Selected";
            this.remove_res.UseVisualStyleBackColor = true;
            this.remove_res.Click += new System.EventHandler(this.remove_res_Click);
            // 
            // add_res
            // 
            this.add_res.Location = new System.Drawing.Point(317, 379);
            this.add_res.Name = "add_res";
            this.add_res.Size = new System.Drawing.Size(109, 25);
            this.add_res.TabIndex = 336;
            this.add_res.Text = "Add New";
            this.add_res.UseVisualStyleBackColor = true;
            this.add_res.Click += new System.EventHandler(this.add_res_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.shadowMapResolutionPresets);
            this.groupBox3.Controls.Add(this.remove_shadowmap);
            this.groupBox3.Controls.Add(this.add_shadowmap);
            this.groupBox3.Location = new System.Drawing.Point(12, 434);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(257, 236);
            this.groupBox3.TabIndex = 361;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Shadow Map Resolution Presets";
            // 
            // shadowMapResolutionPresets
            // 
            this.shadowMapResolutionPresets.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4});
            this.shadowMapResolutionPresets.FullRowSelect = true;
            this.shadowMapResolutionPresets.HideSelection = false;
            this.shadowMapResolutionPresets.Location = new System.Drawing.Point(6, 19);
            this.shadowMapResolutionPresets.MultiSelect = false;
            this.shadowMapResolutionPresets.Name = "shadowMapResolutionPresets";
            this.shadowMapResolutionPresets.Size = new System.Drawing.Size(245, 179);
            this.shadowMapResolutionPresets.TabIndex = 366;
            this.shadowMapResolutionPresets.UseCompatibleStateImageBehavior = false;
            this.shadowMapResolutionPresets.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Name";
            this.columnHeader3.Width = 128;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Resolution (px)";
            this.columnHeader4.Width = 87;
            // 
            // remove_shadowmap
            // 
            this.remove_shadowmap.Location = new System.Drawing.Point(6, 203);
            this.remove_shadowmap.Name = "remove_shadowmap";
            this.remove_shadowmap.Size = new System.Drawing.Size(110, 25);
            this.remove_shadowmap.TabIndex = 337;
            this.remove_shadowmap.Text = "Remove Selected";
            this.remove_shadowmap.UseVisualStyleBackColor = true;
            this.remove_shadowmap.Click += new System.EventHandler(this.remove_shadowmap_Click);
            // 
            // add_shadowmap
            // 
            this.add_shadowmap.Location = new System.Drawing.Point(142, 203);
            this.add_shadowmap.Name = "add_shadowmap";
            this.add_shadowmap.Size = new System.Drawing.Size(109, 25);
            this.add_shadowmap.TabIndex = 336;
            this.add_shadowmap.Text = "Add New";
            this.add_shadowmap.UseVisualStyleBackColor = true;
            this.add_shadowmap.Click += new System.EventHandler(this.add_shadowmap_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.shadowMapFilterQualityPresets);
            this.groupBox4.Controls.Add(this.remove_filter);
            this.groupBox4.Controls.Add(this.add_filter);
            this.groupBox4.Location = new System.Drawing.Point(275, 434);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(257, 236);
            this.groupBox4.TabIndex = 362;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Shadow Map Filter Quality Presets";
            // 
            // shadowMapFilterQualityPresets
            // 
            this.shadowMapFilterQualityPresets.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader5,
            this.columnHeader6});
            this.shadowMapFilterQualityPresets.FullRowSelect = true;
            this.shadowMapFilterQualityPresets.HideSelection = false;
            this.shadowMapFilterQualityPresets.Location = new System.Drawing.Point(6, 19);
            this.shadowMapFilterQualityPresets.MultiSelect = false;
            this.shadowMapFilterQualityPresets.Name = "shadowMapFilterQualityPresets";
            this.shadowMapFilterQualityPresets.Size = new System.Drawing.Size(245, 179);
            this.shadowMapFilterQualityPresets.TabIndex = 367;
            this.shadowMapFilterQualityPresets.UseCompatibleStateImageBehavior = false;
            this.shadowMapFilterQualityPresets.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader5
            // 
            this.columnHeader5.Text = "Name";
            this.columnHeader5.Width = 128;
            // 
            // columnHeader6
            // 
            this.columnHeader6.Text = "PCF Kernel";
            this.columnHeader6.Width = 87;
            // 
            // remove_filter
            // 
            this.remove_filter.Location = new System.Drawing.Point(6, 203);
            this.remove_filter.Name = "remove_filter";
            this.remove_filter.Size = new System.Drawing.Size(110, 25);
            this.remove_filter.TabIndex = 337;
            this.remove_filter.Text = "Remove Selected";
            this.remove_filter.UseVisualStyleBackColor = true;
            this.remove_filter.Click += new System.EventHandler(this.remove_filter_Click);
            // 
            // add_filter
            // 
            this.add_filter.Location = new System.Drawing.Point(142, 204);
            this.add_filter.Name = "add_filter";
            this.add_filter.Size = new System.Drawing.Size(109, 25);
            this.add_filter.TabIndex = 336;
            this.add_filter.Text = "Add New";
            this.add_filter.UseVisualStyleBackColor = true;
            this.add_filter.Click += new System.EventHandler(this.add_filter_Click);
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.lodPresets);
            this.groupBox5.Controls.Add(this.remove_lod);
            this.groupBox5.Controls.Add(this.add_lod);
            this.groupBox5.Location = new System.Drawing.Point(538, 434);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(257, 236);
            this.groupBox5.TabIndex = 363;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Level of Detail Presets";
            // 
            // lodPresets
            // 
            this.lodPresets.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader7,
            this.columnHeader8});
            this.lodPresets.FullRowSelect = true;
            this.lodPresets.HideSelection = false;
            this.lodPresets.Location = new System.Drawing.Point(6, 19);
            this.lodPresets.MultiSelect = false;
            this.lodPresets.Name = "lodPresets";
            this.lodPresets.Size = new System.Drawing.Size(245, 179);
            this.lodPresets.TabIndex = 368;
            this.lodPresets.UseCompatibleStateImageBehavior = false;
            this.lodPresets.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader7
            // 
            this.columnHeader7.Text = "Name";
            this.columnHeader7.Width = 128;
            // 
            // columnHeader8
            // 
            this.columnHeader8.Text = "LOD Value";
            this.columnHeader8.Width = 87;
            // 
            // remove_lod
            // 
            this.remove_lod.Location = new System.Drawing.Point(6, 203);
            this.remove_lod.Name = "remove_lod";
            this.remove_lod.Size = new System.Drawing.Size(110, 25);
            this.remove_lod.TabIndex = 337;
            this.remove_lod.Text = "Remove Selected";
            this.remove_lod.UseVisualStyleBackColor = true;
            this.remove_lod.Click += new System.EventHandler(this.remove_lod_Click);
            // 
            // add_lod
            // 
            this.add_lod.Location = new System.Drawing.Point(142, 203);
            this.add_lod.Name = "add_lod";
            this.add_lod.Size = new System.Drawing.Size(109, 25);
            this.add_lod.TabIndex = 336;
            this.add_lod.Text = "Add New";
            this.add_lod.UseVisualStyleBackColor = true;
            this.add_lod.Click += new System.EventHandler(this.add_lod_Click);
            // 
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.checkHighGloss);
            this.groupBox6.Location = new System.Drawing.Point(800, 17);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Size = new System.Drawing.Size(182, 53);
            this.groupBox6.TabIndex = 360;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "Extra Planar Reflection Options";
            // 
            // checkHighGloss
            // 
            this.checkHighGloss.AutoSize = true;
            this.checkHighGloss.Location = new System.Drawing.Point(10, 24);
            this.checkHighGloss.Name = "checkHighGloss";
            this.checkHighGloss.Size = new System.Drawing.Size(166, 17);
            this.checkHighGloss.TabIndex = 0;
            this.checkHighGloss.Text = "High Gloss Planar Reflections";
            this.checkHighGloss.UseVisualStyleBackColor = true;
            // 
            // groupBox8
            // 
            this.groupBox8.Controls.Add(this.checkAnaglph);
            this.groupBox8.Controls.Add(this.check3D);
            this.groupBox8.Controls.Add(this.checkRift);
            this.groupBox8.Location = new System.Drawing.Point(800, 86);
            this.groupBox8.Name = "groupBox8";
            this.groupBox8.Size = new System.Drawing.Size(182, 116);
            this.groupBox8.TabIndex = 361;
            this.groupBox8.TabStop = false;
            this.groupBox8.Text = "Extra Stereo Mode Options";
            // 
            // checkAnaglph
            // 
            this.checkAnaglph.AutoSize = true;
            this.checkAnaglph.Location = new System.Drawing.Point(10, 77);
            this.checkAnaglph.Name = "checkAnaglph";
            this.checkAnaglph.Size = new System.Drawing.Size(137, 17);
            this.checkAnaglph.TabIndex = 7;
            this.checkAnaglph.Text = "Stereo Mode: Anaglyph";
            this.checkAnaglph.UseVisualStyleBackColor = true;
            // 
            // check3D
            // 
            this.check3D.AutoSize = true;
            this.check3D.Location = new System.Drawing.Point(10, 54);
            this.check3D.Name = "check3D";
            this.check3D.Size = new System.Drawing.Size(124, 17);
            this.check3D.TabIndex = 6;
            this.check3D.Text = "Stereo Mode: 3D TV";
            this.check3D.UseVisualStyleBackColor = true;
            // 
            // checkRift
            // 
            this.checkRift.AutoSize = true;
            this.checkRift.Location = new System.Drawing.Point(10, 31);
            this.checkRift.Name = "checkRift";
            this.checkRift.Size = new System.Drawing.Size(145, 17);
            this.checkRift.TabIndex = 5;
            this.checkRift.Text = "Stereo Mode: Oculus Rift";
            this.checkRift.UseVisualStyleBackColor = true;
            // 
            // GraphicsEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(994, 682);
            this.Controls.Add(this.groupBox8);
            this.Controls.Add(this.groupBox6);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnSave);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Graphics Settings Editor";
            this.groupBox2.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.groupBox6.ResumeLayout(false);
            this.groupBox6.PerformLayout();
            this.groupBox8.ResumeLayout(false);
            this.groupBox8.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button remove_fov;
        private System.Windows.Forms.Button add_fov;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button remove_res;
        private System.Windows.Forms.Button add_res;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button remove_shadowmap;
        private System.Windows.Forms.Button add_shadowmap;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Button remove_filter;
        private System.Windows.Forms.Button add_filter;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Button remove_lod;
        private System.Windows.Forms.Button add_lod;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.CheckBox checkHighGloss;
        private System.Windows.Forms.GroupBox groupBox8;
        private System.Windows.Forms.CheckBox checkAnaglph;
        private System.Windows.Forms.CheckBox check3D;
        private System.Windows.Forms.CheckBox checkRift;
        private System.Windows.Forms.ListView windowedResolutionPresets;
        private System.Windows.Forms.ColumnHeader Name;
        private System.Windows.Forms.ColumnHeader Width;
        private System.Windows.Forms.ColumnHeader Height;
        private System.Windows.Forms.ListView fovPresets;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ListView shadowMapResolutionPresets;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.ListView shadowMapFilterQualityPresets;
        private System.Windows.Forms.ColumnHeader columnHeader5;
        private System.Windows.Forms.ColumnHeader columnHeader6;
        private System.Windows.Forms.ListView lodPresets;
        private System.Windows.Forms.ColumnHeader columnHeader7;
        private System.Windows.Forms.ColumnHeader columnHeader8;
    }
}
