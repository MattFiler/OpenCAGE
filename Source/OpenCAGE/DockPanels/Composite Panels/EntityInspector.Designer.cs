namespace OpenCAGE.DockPanels
{
    partial class EntityInspector
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EntityInspector));
            this.goToZone = new System.Windows.Forms.Button();
            this.showOverridesAndProxies = new System.Windows.Forms.Button();
            this.editEntityMovers = new System.Windows.Forms.Button();
            this.editEntityResources = new System.Windows.Forms.Button();
            this.entityInfoGroup = new System.Windows.Forms.GroupBox();
            this.hierarchyDisplay = new System.Windows.Forms.TextBox();
            this.jumpToComposite = new System.Windows.Forms.Button();
            this.selected_entity_name = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.selected_entity_type_description = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.editFunction = new System.Windows.Forms.Button();
            this.entityParamGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.addLinkOut = new System.Windows.Forms.Button();
            this.ModifyParameters_Link = new System.Windows.Forms.Button();
            this.entity_params = new System.Windows.Forms.Panel();
            this.contextMenuStrip2 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.createLinkToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.renameEntity = new System.Windows.Forms.ToolStripButton();
            this.duplicateEntity = new System.Windows.Forms.ToolStripButton();
            this.deleteEntity = new System.Windows.Forms.ToolStripButton();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.ModifyParameters = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.applyDefaultsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addUnsetParametersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.applyAllDefaultsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.entityInfoGroup.SuspendLayout();
            this.entityParamGroup.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.contextMenuStrip2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // goToZone
            // 
            this.goToZone.Dock = System.Windows.Forms.DockStyle.Fill;
            this.goToZone.Location = new System.Drawing.Point(231, 3);
            this.goToZone.Name = "goToZone";
            this.goToZone.Size = new System.Drawing.Size(70, 23);
            this.goToZone.TabIndex = 188;
            this.goToZone.Text = "Zone";
            this.toolTip1.SetToolTip(this.goToZone, "Zone");
            this.goToZone.UseVisualStyleBackColor = true;
            this.goToZone.Click += new System.EventHandler(this.goToZone_Click);
            // 
            // showOverridesAndProxies
            // 
            this.showOverridesAndProxies.Dock = System.Windows.Forms.DockStyle.Fill;
            this.showOverridesAndProxies.Location = new System.Drawing.Point(79, 3);
            this.showOverridesAndProxies.Name = "showOverridesAndProxies";
            this.showOverridesAndProxies.Size = new System.Drawing.Size(70, 23);
            this.showOverridesAndProxies.TabIndex = 187;
            this.showOverridesAndProxies.Text = "References";
            this.toolTip1.SetToolTip(this.showOverridesAndProxies, "References");
            this.showOverridesAndProxies.UseVisualStyleBackColor = true;
            this.showOverridesAndProxies.Click += new System.EventHandler(this.showOverridesAndProxies_Click);
            // 
            // editEntityMovers
            // 
            this.editEntityMovers.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editEntityMovers.Location = new System.Drawing.Point(3, 3);
            this.editEntityMovers.Name = "editEntityMovers";
            this.editEntityMovers.Size = new System.Drawing.Size(70, 23);
            this.editEntityMovers.TabIndex = 186;
            this.editEntityMovers.Text = "Movers";
            this.toolTip1.SetToolTip(this.editEntityMovers, "Movers");
            this.editEntityMovers.UseVisualStyleBackColor = true;
            this.editEntityMovers.Click += new System.EventHandler(this.editEntityMovers_Click);
            // 
            // editEntityResources
            // 
            this.editEntityResources.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editEntityResources.Location = new System.Drawing.Point(155, 3);
            this.editEntityResources.Name = "editEntityResources";
            this.editEntityResources.Size = new System.Drawing.Size(70, 23);
            this.editEntityResources.TabIndex = 184;
            this.editEntityResources.Text = "Resources";
            this.toolTip1.SetToolTip(this.editEntityResources, "Resources");
            this.editEntityResources.UseVisualStyleBackColor = true;
            this.editEntityResources.Click += new System.EventHandler(this.editEntityResources_Click);
            // 
            // entityInfoGroup
            // 
            this.entityInfoGroup.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.entityInfoGroup.Controls.Add(this.hierarchyDisplay);
            this.entityInfoGroup.Controls.Add(this.jumpToComposite);
            this.entityInfoGroup.Controls.Add(this.selected_entity_name);
            this.entityInfoGroup.Controls.Add(this.label9);
            this.entityInfoGroup.Controls.Add(this.selected_entity_type_description);
            this.entityInfoGroup.Controls.Add(this.label6);
            this.entityInfoGroup.Location = new System.Drawing.Point(12, 25);
            this.entityInfoGroup.Name = "entityInfoGroup";
            this.entityInfoGroup.Size = new System.Drawing.Size(382, 69);
            this.entityInfoGroup.TabIndex = 183;
            this.entityInfoGroup.TabStop = false;
            this.entityInfoGroup.Text = "Selected Entity Info";
            // 
            // hierarchyDisplay
            // 
            this.hierarchyDisplay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.hierarchyDisplay.Location = new System.Drawing.Point(7, 39);
            this.hierarchyDisplay.Name = "hierarchyDisplay";
            this.hierarchyDisplay.ReadOnly = true;
            this.hierarchyDisplay.Size = new System.Drawing.Size(321, 20);
            this.hierarchyDisplay.TabIndex = 9;
            // 
            // jumpToComposite
            // 
            this.jumpToComposite.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.jumpToComposite.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.jumpToComposite.Location = new System.Drawing.Point(334, 6);
            this.jumpToComposite.Name = "jumpToComposite";
            this.jumpToComposite.Size = new System.Drawing.Size(48, 62);
            this.jumpToComposite.TabIndex = 8;
            this.jumpToComposite.Text = "Go";
            this.jumpToComposite.UseVisualStyleBackColor = true;
            this.jumpToComposite.Visible = false;
            this.jumpToComposite.Click += new System.EventHandler(this.jumpToComposite_Click);
            // 
            // selected_entity_name
            // 
            this.selected_entity_name.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.selected_entity_name.AutoSize = true;
            this.selected_entity_name.Location = new System.Drawing.Point(54, 22);
            this.selected_entity_name.Name = "selected_entity_name";
            this.selected_entity_name.Size = new System.Drawing.Size(0, 13);
            this.selected_entity_name.TabIndex = 7;
            // 
            // label9
            // 
            this.label9.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label9.AutoSize = true;
            this.label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label9.Location = new System.Drawing.Point(9, 22);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(47, 13);
            this.label9.TabIndex = 6;
            this.label9.Text = "Name: ";
            // 
            // selected_entity_type_description
            // 
            this.selected_entity_type_description.AutoSize = true;
            this.selected_entity_type_description.Location = new System.Drawing.Point(54, 41);
            this.selected_entity_type_description.Name = "selected_entity_type_description";
            this.selected_entity_type_description.Size = new System.Drawing.Size(0, 13);
            this.selected_entity_type_description.TabIndex = 5;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(13, 40);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(39, 13);
            this.label6.TabIndex = 4;
            this.label6.Text = "Type:";
            // 
            // editFunction
            // 
            this.editFunction.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editFunction.Location = new System.Drawing.Point(307, 3);
            this.editFunction.Name = "editFunction";
            this.editFunction.Size = new System.Drawing.Size(74, 23);
            this.editFunction.TabIndex = 185;
            this.editFunction.Text = "Function";
            this.toolTip1.SetToolTip(this.editFunction, "Function");
            this.editFunction.UseVisualStyleBackColor = true;
            this.editFunction.Click += new System.EventHandler(this.editFunction_Click);
            // 
            // entityParamGroup
            // 
            this.entityParamGroup.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.entityParamGroup.Controls.Add(this.tableLayoutPanel2);
            this.entityParamGroup.Controls.Add(this.entity_params);
            this.entityParamGroup.Location = new System.Drawing.Point(12, 132);
            this.entityParamGroup.Name = "entityParamGroup";
            this.entityParamGroup.Size = new System.Drawing.Size(382, 638);
            this.entityParamGroup.TabIndex = 182;
            this.entityParamGroup.TabStop = false;
            this.entityParamGroup.Text = "Selected Entity Parameters";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel2.Controls.Add(this.addLinkOut, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.ModifyParameters_Link, 0, 0);
            this.tableLayoutPanel2.Location = new System.Drawing.Point(6, 603);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(375, 28);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // addLinkOut
            // 
            this.addLinkOut.Dock = System.Windows.Forms.DockStyle.Fill;
            this.addLinkOut.Location = new System.Drawing.Point(190, 3);
            this.addLinkOut.Name = "addLinkOut";
            this.addLinkOut.Size = new System.Drawing.Size(182, 22);
            this.addLinkOut.TabIndex = 151;
            this.addLinkOut.Text = "Create Link";
            this.addLinkOut.UseVisualStyleBackColor = true;
            this.addLinkOut.Click += new System.EventHandler(this.addLinkOut_Click);
            // 
            // ModifyParameters_Link
            // 
            this.ModifyParameters_Link.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ModifyParameters_Link.Location = new System.Drawing.Point(3, 3);
            this.ModifyParameters_Link.Name = "ModifyParameters_Link";
            this.ModifyParameters_Link.Size = new System.Drawing.Size(181, 22);
            this.ModifyParameters_Link.TabIndex = 149;
            this.ModifyParameters_Link.Text = "Modify Parameters";
            this.ModifyParameters_Link.UseVisualStyleBackColor = true;
            this.ModifyParameters_Link.Click += new System.EventHandler(this.ModifyParameters_Click);
            // 
            // entity_params
            // 
            this.entity_params.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.entity_params.AutoScroll = true;
            this.entity_params.ContextMenuStrip = this.contextMenuStrip2;
            this.entity_params.Location = new System.Drawing.Point(6, 20);
            this.entity_params.Name = "entity_params";
            this.entity_params.Size = new System.Drawing.Size(375, 581);
            this.entity_params.TabIndex = 0;
            // 
            // contextMenuStrip2
            // 
            this.contextMenuStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem1,
            this.createLinkToolStripMenuItem,
            this.applyDefaultsToolStripMenuItem});
            this.contextMenuStrip2.Name = "contextMenuStrip1";
            this.contextMenuStrip2.Size = new System.Drawing.Size(181, 92);
            this.contextMenuStrip2.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStrip2_Opening);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripMenuItem1.Image")));
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(180, 22);
            this.toolStripMenuItem1.Text = "Modify Parameters";
            this.toolStripMenuItem1.Click += new System.EventHandler(this.toolStripMenuItem1_Click);
            // 
            // createLinkToolStripMenuItem
            // 
            this.createLinkToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("createLinkToolStripMenuItem.Image")));
            this.createLinkToolStripMenuItem.Name = "createLinkToolStripMenuItem";
            this.createLinkToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.createLinkToolStripMenuItem.Text = "Create Link";
            this.createLinkToolStripMenuItem.Click += new System.EventHandler(this.createLinkToolStripMenuItem_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.Controls.Add(this.editEntityMovers, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.goToZone, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.editFunction, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.showOverridesAndProxies, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.editEntityResources, 2, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(12, 97);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(384, 29);
            this.tableLayoutPanel1.TabIndex = 189;
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.renameEntity,
            this.duplicateEntity,
            this.deleteEntity});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(406, 25);
            this.toolStrip1.TabIndex = 190;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // renameEntity
            // 
            this.renameEntity.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.renameEntity.Image = ((System.Drawing.Image)(resources.GetObject("renameEntity.Image")));
            this.renameEntity.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.renameEntity.Name = "renameEntity";
            this.renameEntity.Size = new System.Drawing.Size(103, 22);
            this.renameEntity.Text = "Rename Entity";
            this.renameEntity.Click += new System.EventHandler(this.renameEntity_Click);
            // 
            // duplicateEntity
            // 
            this.duplicateEntity.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.duplicateEntity.Image = ((System.Drawing.Image)(resources.GetObject("duplicateEntity.Image")));
            this.duplicateEntity.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.duplicateEntity.Name = "duplicateEntity";
            this.duplicateEntity.Size = new System.Drawing.Size(110, 22);
            this.duplicateEntity.Text = "Duplicate Entity";
            this.duplicateEntity.Click += new System.EventHandler(this.duplicateEntity_Click);
            // 
            // deleteEntity
            // 
            this.deleteEntity.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.deleteEntity.Image = ((System.Drawing.Image)(resources.GetObject("deleteEntity.Image")));
            this.deleteEntity.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.deleteEntity.Name = "deleteEntity";
            this.deleteEntity.Size = new System.Drawing.Size(93, 22);
            this.deleteEntity.Text = "Delete Entity";
            this.deleteEntity.Click += new System.EventHandler(this.deleteEntity_Click);
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "GenericEditor.ico");
            // 
            // ModifyParameters
            // 
            this.ModifyParameters.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ModifyParameters.Location = new System.Drawing.Point(22, 738);
            this.ModifyParameters.Name = "ModifyParameters";
            this.ModifyParameters.Size = new System.Drawing.Size(368, 22);
            this.ModifyParameters.TabIndex = 1;
            this.ModifyParameters.Text = "Modify Parameters";
            this.ModifyParameters.UseVisualStyleBackColor = true;
            this.ModifyParameters.Click += new System.EventHandler(this.ModifyParameters_Click);
            // 
            // applyDefaultsToolStripMenuItem
            // 
            this.applyDefaultsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addUnsetParametersToolStripMenuItem,
            this.applyAllDefaultsToolStripMenuItem});
            this.applyDefaultsToolStripMenuItem.Name = "applyDefaultsToolStripMenuItem";
            this.applyDefaultsToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.applyDefaultsToolStripMenuItem.Text = "Apply Defaults";
            // 
            // addUnsetParametersToolStripMenuItem
            // 
            this.addUnsetParametersToolStripMenuItem.Name = "addUnsetParametersToolStripMenuItem";
            this.addUnsetParametersToolStripMenuItem.Size = new System.Drawing.Size(278, 22);
            this.addUnsetParametersToolStripMenuItem.Text = "Add Missing Parameter Defaults";
            this.addUnsetParametersToolStripMenuItem.ToolTipText = "Selecting this will add the default values for all parameters currently not appli" +
    "ed to the entity.";
            this.addUnsetParametersToolStripMenuItem.Click += new System.EventHandler(this.addUnsetParametersToolStripMenuItem_Click);
            // 
            // applyAllDefaultsToolStripMenuItem
            // 
            this.applyAllDefaultsToolStripMenuItem.Name = "applyAllDefaultsToolStripMenuItem";
            this.applyAllDefaultsToolStripMenuItem.Size = new System.Drawing.Size(278, 22);
            this.applyAllDefaultsToolStripMenuItem.Text = "Apply All Defaults (Overwrites Existing)";
            this.applyAllDefaultsToolStripMenuItem.ToolTipText = "Selecting this will apply all default parameters to this entity, overwriting the " +
    "ones already set.";
            this.applyAllDefaultsToolStripMenuItem.Click += new System.EventHandler(this.applyAllDefaultsToolStripMenuItem_Click);
            // 
            // EntityInspector
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(406, 780);
            this.Controls.Add(this.ModifyParameters);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.entityInfoGroup);
            this.Controls.Add(this.entityParamGroup);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "EntityInspector";
            this.DockAreas = ((WeifenLuo.WinFormsUI.Docking.DockAreas)((WeifenLuo.WinFormsUI.Docking.DockAreas.Float | WeifenLuo.WinFormsUI.Docking.DockAreas.DockRight)));
            this.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockRight;
            this.Text = "Entity Inspector";
            this.entityInfoGroup.ResumeLayout(false);
            this.entityInfoGroup.PerformLayout();
            this.entityParamGroup.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.contextMenuStrip2.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button goToZone;
        private System.Windows.Forms.Button showOverridesAndProxies;
        private System.Windows.Forms.Button editEntityMovers;
        private System.Windows.Forms.Button editEntityResources;
        private System.Windows.Forms.GroupBox entityInfoGroup;
        private System.Windows.Forms.TextBox hierarchyDisplay;
        private System.Windows.Forms.Button jumpToComposite;
        private System.Windows.Forms.Label selected_entity_name;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label selected_entity_type_description;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button editFunction;
        private System.Windows.Forms.GroupBox entityParamGroup;
        private System.Windows.Forms.Button addLinkOut;
        private System.Windows.Forms.Button ModifyParameters_Link;
        private System.Windows.Forms.Panel entity_params;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton deleteEntity;
        private System.Windows.Forms.ToolStripButton duplicateEntity;
        private System.Windows.Forms.ToolStripButton renameEntity;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.Button ModifyParameters;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem createLinkToolStripMenuItem;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ToolStripMenuItem applyDefaultsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addUnsetParametersToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem applyAllDefaultsToolStripMenuItem;
    }
}
