namespace CommandsEditor
{
    partial class SelectHierarchy
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SelectHierarchy));
            this.FollowEntityThrough = new System.Windows.Forms.Button();
            this.SelectEntity = new System.Windows.Forms.Button();
            this.compositeEntityList1 = new Popups.UserControls.CompositeEntityList();
            this.pathDisplay = new System.Windows.Forms.TextBox();
            this.goBackOnPath = new System.Windows.Forms.Button();
            this.applyDefaultParams = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // FollowEntityThrough
            // 
            this.FollowEntityThrough.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.FollowEntityThrough.Location = new System.Drawing.Point(12, 755);
            this.FollowEntityThrough.Name = "FollowEntityThrough";
            this.FollowEntityThrough.Size = new System.Drawing.Size(171, 23);
            this.FollowEntityThrough.TabIndex = 148;
            this.FollowEntityThrough.Text = "Follow Entity Through";
            this.FollowEntityThrough.UseVisualStyleBackColor = true;
            this.FollowEntityThrough.Click += new System.EventHandler(this.FollowEntityThrough_Click);
            // 
            // SelectEntity
            // 
            this.SelectEntity.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.SelectEntity.Location = new System.Drawing.Point(580, 755);
            this.SelectEntity.Name = "SelectEntity";
            this.SelectEntity.Size = new System.Drawing.Size(171, 23);
            this.SelectEntity.TabIndex = 147;
            this.SelectEntity.Text = "Select Entity";
            this.SelectEntity.UseVisualStyleBackColor = true;
            this.SelectEntity.Click += new System.EventHandler(this.SelectEntity_Click);
            // 
            // compositeEntityList1
            // 
            this.compositeEntityList1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.compositeEntityList1.Location = new System.Drawing.Point(9, 10);
            this.compositeEntityList1.Name = "compositeEntityList1";
            this.compositeEntityList1.Size = new System.Drawing.Size(741, 715);
            this.compositeEntityList1.TabIndex = 149;
            // 
            // pathDisplay
            // 
            this.pathDisplay.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pathDisplay.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pathDisplay.Enabled = false;
            this.pathDisplay.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.pathDisplay.Location = new System.Drawing.Point(72, 724);
            this.pathDisplay.Name = "pathDisplay";
            this.pathDisplay.ReadOnly = true;
            this.pathDisplay.Size = new System.Drawing.Size(678, 20);
            this.pathDisplay.TabIndex = 178;
            // 
            // goBackOnPath
            // 
            this.goBackOnPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.goBackOnPath.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.goBackOnPath.Location = new System.Drawing.Point(12, 724);
            this.goBackOnPath.Name = "goBackOnPath";
            this.goBackOnPath.Size = new System.Drawing.Size(62, 20);
            this.goBackOnPath.TabIndex = 179;
            this.goBackOnPath.Text = "< Back";
            this.goBackOnPath.UseVisualStyleBackColor = true;
            this.goBackOnPath.Click += new System.EventHandler(this.goBackOnPath_Click);
            // 
            // applyDefaultParams
            // 
            this.applyDefaultParams.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.applyDefaultParams.AutoSize = true;
            this.applyDefaultParams.Location = new System.Drawing.Point(429, 759);
            this.applyDefaultParams.Name = "applyDefaultParams";
            this.applyDefaultParams.Size = new System.Drawing.Size(145, 17);
            this.applyDefaultParams.TabIndex = 185;
            this.applyDefaultParams.Text = "Apply Default Parameters";
            this.applyDefaultParams.UseVisualStyleBackColor = true;
            // 
            // SelectHierarchy
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(763, 792);
            this.Controls.Add(this.applyDefaultParams);
            this.Controls.Add(this.pathDisplay);
            this.Controls.Add(this.goBackOnPath);
            this.Controls.Add(this.compositeEntityList1);
            this.Controls.Add(this.FollowEntityThrough);
            this.Controls.Add(this.SelectEntity);
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.Name = "SelectHierarchy";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Select Entity";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button SelectEntity;
        private System.Windows.Forms.Button FollowEntityThrough;
        private Popups.UserControls.CompositeEntityList compositeEntityList1;
        private System.Windows.Forms.TextBox pathDisplay;
        private System.Windows.Forms.Button goBackOnPath;
        private System.Windows.Forms.CheckBox applyDefaultParams;
    }
}
