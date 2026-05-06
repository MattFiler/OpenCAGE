namespace OpenCAGE
{
    partial class ShowInstanceInfo
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ShowInstanceInfo));
            this.guI_TransformDataType1 = new UserControls.GUI_TransformDataType();
            this.SuspendLayout();
            // 
            // guI_TransformDataType1
            // 
            this.guI_TransformDataType1.Location = new System.Drawing.Point(12, 12);
            this.guI_TransformDataType1.Name = "guI_TransformDataType1";
            this.guI_TransformDataType1.Size = new System.Drawing.Size(340, 114);
            this.guI_TransformDataType1.TabIndex = 2;
            // 
            // ShowInstanceInfo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(364, 142);
            this.Controls.Add(this.guI_TransformDataType1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "ShowInstanceInfo";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Instance Info";
            this.ResumeLayout(false);

        }

        #endregion
        private UserControls.GUI_TransformDataType guI_TransformDataType1;
    }
}
