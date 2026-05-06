namespace OpenCAGE
{
    partial class AddEntity_Function
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddEntity_Function));
            this.addDefaultParams = new System.Windows.Forms.CheckBox();
            this.createEntity = new System.Windows.Forms.Button();
            this.entityName = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.helpBtn = new System.Windows.Forms.Button();
            this.functionTypeList1 = new Popups.UserControls.FunctionTypeList();
            this.SuspendLayout();
            // 
            // addDefaultParams
            // 
            this.addDefaultParams.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.addDefaultParams.AutoSize = true;
            this.addDefaultParams.Location = new System.Drawing.Point(15, 364);
            this.addDefaultParams.Name = "addDefaultParams";
            this.addDefaultParams.Size = new System.Drawing.Size(138, 17);
            this.addDefaultParams.TabIndex = 15;
            this.addDefaultParams.Text = "Add Default Parameters";
            this.addDefaultParams.UseVisualStyleBackColor = true;
            // 
            // createEntity
            // 
            this.createEntity.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.createEntity.Location = new System.Drawing.Point(540, 360);
            this.createEntity.Name = "createEntity";
            this.createEntity.Size = new System.Drawing.Size(101, 23);
            this.createEntity.TabIndex = 6;
            this.createEntity.Text = "Create";
            this.createEntity.UseVisualStyleBackColor = true;
            this.createEntity.Click += new System.EventHandler(this.createEntity_Click);
            // 
            // entityName
            // 
            this.entityName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.entityName.Location = new System.Drawing.Point(15, 34);
            this.entityName.Name = "entityName";
            this.entityName.Size = new System.Drawing.Size(626, 20);
            this.entityName.TabIndex = 1;
            this.entityName.KeyDown += new System.Windows.Forms.KeyEventHandler(this.CreateEntityOnEnterKey);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(99, 18);
            this.label1.TabIndex = 147;
            this.label1.Text = "Entity Name";
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
            // functionTypeList1
            // 
            this.functionTypeList1.Location = new System.Drawing.Point(13, 60);
            this.functionTypeList1.Name = "functionTypeList1";
            this.functionTypeList1.Size = new System.Drawing.Size(630, 280);
            this.functionTypeList1.TabIndex = 0;
            // 
            // AddEntity_Function
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(653, 400);
            this.Controls.Add(this.functionTypeList1);
            this.Controls.Add(this.helpBtn);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.addDefaultParams);
            this.Controls.Add(this.createEntity);
            this.Controls.Add(this.entityName);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "AddEntity_Function";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Create Function Entity";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox addDefaultParams;
        private System.Windows.Forms.Button createEntity;
        private System.Windows.Forms.TextBox entityName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button helpBtn;
        private Popups.UserControls.FunctionTypeList functionTypeList1;
    }
}
