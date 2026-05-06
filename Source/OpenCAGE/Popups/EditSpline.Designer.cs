namespace CommandsEditor
{
    partial class EditSpline
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditSpline));
            this.modelRendererHost = new System.Windows.Forms.Integration.ElementHost();
            this.pointTransform = new UserControls.GUI_TransformDataType();
            this.splinePoints = new System.Windows.Forms.ComboBox();
            this.addPoint = new System.Windows.Forms.Button();
            this.removePoint = new System.Windows.Forms.Button();
            this.saveSpline = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // modelRendererHost
            // 
            this.modelRendererHost.Location = new System.Drawing.Point(12, 12);
            this.modelRendererHost.Name = "modelRendererHost";
            this.modelRendererHost.Size = new System.Drawing.Size(708, 597);
            this.modelRendererHost.TabIndex = 1;
            this.modelRendererHost.Text = "elementHost1";
            this.modelRendererHost.Child = null;
            // 
            // pointTransform
            // 
            this.pointTransform.Location = new System.Drawing.Point(726, 253);
            this.pointTransform.Name = "pointTransform";
            this.pointTransform.Size = new System.Drawing.Size(340, 113);
            this.pointTransform.TabIndex = 2;
            // 
            // splinePoints
            // 
            this.splinePoints.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.splinePoints.FormattingEnabled = true;
            this.splinePoints.Location = new System.Drawing.Point(727, 226);
            this.splinePoints.Name = "splinePoints";
            this.splinePoints.Size = new System.Drawing.Size(207, 21);
            this.splinePoints.TabIndex = 3;
            this.splinePoints.SelectedIndexChanged += new System.EventHandler(this.splinePoints_SelectedIndexChanged);
            // 
            // addPoint
            // 
            this.addPoint.Location = new System.Drawing.Point(943, 225);
            this.addPoint.Name = "addPoint";
            this.addPoint.Size = new System.Drawing.Size(57, 23);
            this.addPoint.TabIndex = 4;
            this.addPoint.Text = "Add";
            this.addPoint.UseVisualStyleBackColor = true;
            this.addPoint.Click += new System.EventHandler(this.addPoint_Click);
            // 
            // removePoint
            // 
            this.removePoint.Location = new System.Drawing.Point(1006, 225);
            this.removePoint.Name = "removePoint";
            this.removePoint.Size = new System.Drawing.Size(57, 23);
            this.removePoint.TabIndex = 5;
            this.removePoint.Text = "Delete";
            this.removePoint.UseVisualStyleBackColor = true;
            this.removePoint.Click += new System.EventHandler(this.removePoint_Click);
            // 
            // saveSpline
            // 
            this.saveSpline.Location = new System.Drawing.Point(935, 567);
            this.saveSpline.Name = "saveSpline";
            this.saveSpline.Size = new System.Drawing.Size(133, 42);
            this.saveSpline.TabIndex = 6;
            this.saveSpline.Text = "Save";
            this.saveSpline.UseVisualStyleBackColor = true;
            this.saveSpline.Click += new System.EventHandler(this.saveSpline_Click);
            // 
            // EditSpline
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1078, 619);
            this.Controls.Add(this.saveSpline);
            this.Controls.Add(this.removePoint);
            this.Controls.Add(this.addPoint);
            this.Controls.Add(this.splinePoints);
            this.Controls.Add(this.pointTransform);
            this.Controls.Add(this.modelRendererHost);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "EditSpline";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Edit Spline";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Integration.ElementHost modelRendererHost;
        private UserControls.GUI_TransformDataType pointTransform;
        private System.Windows.Forms.ComboBox splinePoints;
        private System.Windows.Forms.Button addPoint;
        private System.Windows.Forms.Button removePoint;
        private System.Windows.Forms.Button saveSpline;
    }
}
