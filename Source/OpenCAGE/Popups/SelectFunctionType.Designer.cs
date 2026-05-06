namespace CommandsEditor.Popups
{
    partial class SelectFunctionType
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SelectFunctionType));
            this.functionTypeList1 = new Popups.UserControls.FunctionTypeList();
            this.button1 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // functionTypeList1
            // 
            this.functionTypeList1.Location = new System.Drawing.Point(10, 8);
            this.functionTypeList1.Name = "functionTypeList1";
            this.functionTypeList1.Size = new System.Drawing.Size(630, 280);
            this.functionTypeList1.TabIndex = 0;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(492, 294);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(148, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "Select";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // SelectFunctionType
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(647, 326);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.functionTypeList1);
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.Name = "SelectFunctionType";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Select Function Type";
            this.ResumeLayout(false);

        }

        #endregion

        private Popups.UserControls.FunctionTypeList functionTypeList1;
        private System.Windows.Forms.Button button1;
    }
}
