namespace OpenCAGE
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
            this.modelRendererHost = new System.Windows.Forms.Integration.ElementHost();
            this.pointTransform = new UserControls.GUI_TransformDataType();
            this.splinePoints = new System.Windows.Forms.ListBox();
            this.addPoint = new System.Windows.Forms.Button();
            this.insertPoint = new System.Windows.Forms.Button();
            this.removePoint = new System.Windows.Forms.Button();
            this.saveSpline = new System.Windows.Forms.Button();
            this.editInViewport = new System.Windows.Forms.CheckBox();
            this.viewportHint = new System.Windows.Forms.Label();
            this.pointsLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // modelRendererHost
            //
            this.modelRendererHost.Location = new System.Drawing.Point(12, 12);
            this.modelRendererHost.Name = "modelRendererHost";
            this.modelRendererHost.Size = new System.Drawing.Size(708, 616);
            this.modelRendererHost.TabIndex = 0;
            this.modelRendererHost.Text = "elementHost1";
            this.modelRendererHost.Child = null;
            //
            // editInViewport
            //
            this.editInViewport.Appearance = System.Windows.Forms.Appearance.Button;
            this.editInViewport.Location = new System.Drawing.Point(726, 12);
            this.editInViewport.Name = "editInViewport";
            this.editInViewport.Size = new System.Drawing.Size(340, 30);
            this.editInViewport.TabIndex = 1;
            this.editInViewport.Text = "Edit in Viewport";
            this.editInViewport.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.editInViewport.UseVisualStyleBackColor = true;
            this.editInViewport.CheckedChanged += new System.EventHandler(this.editInViewport_CheckedChanged);
            //
            // viewportHint
            //
            this.viewportHint.Location = new System.Drawing.Point(726, 46);
            this.viewportHint.Name = "viewportHint";
            this.viewportHint.Size = new System.Drawing.Size(340, 44);
            this.viewportHint.TabIndex = 2;
            this.viewportHint.Text = "Click a point in the viewport to select it, and move it with the gizmo. Delete removes the selected point, and Escape leaves. Nothing is saved until you press Save.";
            //
            // pointsLabel
            //
            this.pointsLabel.AutoSize = true;
            this.pointsLabel.Location = new System.Drawing.Point(723, 97);
            this.pointsLabel.Name = "pointsLabel";
            this.pointsLabel.Size = new System.Drawing.Size(36, 13);
            this.pointsLabel.TabIndex = 3;
            this.pointsLabel.Text = "Points";
            //
            // splinePoints
            //
            this.splinePoints.FormattingEnabled = true;
            this.splinePoints.IntegralHeight = false;
            this.splinePoints.Location = new System.Drawing.Point(726, 114);
            this.splinePoints.Name = "splinePoints";
            this.splinePoints.Size = new System.Drawing.Size(340, 186);
            this.splinePoints.TabIndex = 4;
            this.splinePoints.SelectedIndexChanged += new System.EventHandler(this.splinePoints_SelectedIndexChanged);
            //
            // addPoint
            //
            this.addPoint.Location = new System.Drawing.Point(726, 306);
            this.addPoint.Name = "addPoint";
            this.addPoint.Size = new System.Drawing.Size(110, 23);
            this.addPoint.TabIndex = 5;
            this.addPoint.Text = "Add";
            this.addPoint.UseVisualStyleBackColor = true;
            this.addPoint.Click += new System.EventHandler(this.addPoint_Click);
            //
            // insertPoint
            //
            this.insertPoint.Location = new System.Drawing.Point(841, 306);
            this.insertPoint.Name = "insertPoint";
            this.insertPoint.Size = new System.Drawing.Size(110, 23);
            this.insertPoint.TabIndex = 6;
            this.insertPoint.Text = "Insert After";
            this.insertPoint.UseVisualStyleBackColor = true;
            this.insertPoint.Click += new System.EventHandler(this.insertPoint_Click);
            //
            // removePoint
            //
            this.removePoint.Location = new System.Drawing.Point(956, 306);
            this.removePoint.Name = "removePoint";
            this.removePoint.Size = new System.Drawing.Size(110, 23);
            this.removePoint.TabIndex = 7;
            this.removePoint.Text = "Delete";
            this.removePoint.UseVisualStyleBackColor = true;
            this.removePoint.Click += new System.EventHandler(this.removePoint_Click);
            //
            // pointTransform
            //
            this.pointTransform.Location = new System.Drawing.Point(726, 340);
            this.pointTransform.Name = "pointTransform";
            this.pointTransform.Size = new System.Drawing.Size(340, 113);
            this.pointTransform.TabIndex = 8;
            //
            // saveSpline
            //
            this.saveSpline.Location = new System.Drawing.Point(933, 586);
            this.saveSpline.Name = "saveSpline";
            this.saveSpline.Size = new System.Drawing.Size(133, 42);
            this.saveSpline.TabIndex = 9;
            this.saveSpline.Text = "Save";
            this.saveSpline.UseVisualStyleBackColor = true;
            this.saveSpline.Click += new System.EventHandler(this.saveSpline_Click);
            //
            // EditSpline
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1078, 640);
            this.Controls.Add(this.saveSpline);
            this.Controls.Add(this.pointTransform);
            this.Controls.Add(this.removePoint);
            this.Controls.Add(this.insertPoint);
            this.Controls.Add(this.addPoint);
            this.Controls.Add(this.splinePoints);
            this.Controls.Add(this.pointsLabel);
            this.Controls.Add(this.viewportHint);
            this.Controls.Add(this.editInViewport);
            this.Controls.Add(this.modelRendererHost);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "EditSpline";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Edit Spline";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Integration.ElementHost modelRendererHost;
        private UserControls.GUI_TransformDataType pointTransform;
        private System.Windows.Forms.ListBox splinePoints;
        private System.Windows.Forms.Button addPoint;
        private System.Windows.Forms.Button insertPoint;
        private System.Windows.Forms.Button removePoint;
        private System.Windows.Forms.Button saveSpline;
        private System.Windows.Forms.CheckBox editInViewport;
        private System.Windows.Forms.Label viewportHint;
        private System.Windows.Forms.Label pointsLabel;
    }
}
