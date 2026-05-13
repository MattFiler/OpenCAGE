namespace OpenCAGE
{
    partial class SubmeshInfoForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.groupBoxSubmesh = new System.Windows.Forms.GroupBox();
            this.lblDecodeError = new System.Windows.Forms.Label();
            this.lblTriangleCount = new System.Windows.Forms.Label();
            this.lblTrianglesCaption = new System.Windows.Forms.Label();
            this.lblIndexCount = new System.Windows.Forms.Label();
            this.lblIndicesCaption = new System.Windows.Forms.Label();
            this.lblVertexCount = new System.Windows.Forms.Label();
            this.lblVerticesCaption = new System.Windows.Forms.Label();
            this.groupBoxVertexFormat = new System.Windows.Forms.GroupBox();
            this.lblSupportedValue = new System.Windows.Forms.Label();
            this.groupBoxSubmesh.SuspendLayout();
            this.groupBoxVertexFormat.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxSubmesh
            // 
            this.groupBoxSubmesh.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxSubmesh.Controls.Add(this.lblDecodeError);
            this.groupBoxSubmesh.Controls.Add(this.lblTriangleCount);
            this.groupBoxSubmesh.Controls.Add(this.lblTrianglesCaption);
            this.groupBoxSubmesh.Controls.Add(this.lblIndexCount);
            this.groupBoxSubmesh.Controls.Add(this.lblIndicesCaption);
            this.groupBoxSubmesh.Controls.Add(this.lblVertexCount);
            this.groupBoxSubmesh.Controls.Add(this.lblVerticesCaption);
            this.groupBoxSubmesh.Location = new System.Drawing.Point(12, 12);
            this.groupBoxSubmesh.Name = "groupBoxSubmesh";
            this.groupBoxSubmesh.Size = new System.Drawing.Size(356, 99);
            this.groupBoxSubmesh.TabIndex = 0;
            this.groupBoxSubmesh.TabStop = false;
            this.groupBoxSubmesh.Text = "Submesh Content";
            // 
            // lblDecodeError
            // 
            this.lblDecodeError.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDecodeError.AutoSize = true;
            this.lblDecodeError.ForeColor = System.Drawing.Color.Firebrick;
            this.lblDecodeError.Location = new System.Drawing.Point(16, 88);
            this.lblDecodeError.MaximumSize = new System.Drawing.Size(320, 0);
            this.lblDecodeError.Name = "lblDecodeError";
            this.lblDecodeError.Size = new System.Drawing.Size(0, 13);
            this.lblDecodeError.TabIndex = 6;
            this.lblDecodeError.Visible = false;
            // 
            // lblTriangleCount
            // 
            this.lblTriangleCount.AutoSize = true;
            this.lblTriangleCount.Location = new System.Drawing.Point(83, 64);
            this.lblTriangleCount.Name = "lblTriangleCount";
            this.lblTriangleCount.Size = new System.Drawing.Size(13, 13);
            this.lblTriangleCount.TabIndex = 5;
            this.lblTriangleCount.Text = "0";
            // 
            // lblTrianglesCaption
            // 
            this.lblTrianglesCaption.AutoSize = true;
            this.lblTrianglesCaption.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTrianglesCaption.Location = new System.Drawing.Point(16, 64);
            this.lblTrianglesCaption.Name = "lblTrianglesCaption";
            this.lblTrianglesCaption.Size = new System.Drawing.Size(63, 13);
            this.lblTrianglesCaption.TabIndex = 4;
            this.lblTrianglesCaption.Text = "Triangles:";
            // 
            // lblIndexCount
            // 
            this.lblIndexCount.AutoSize = true;
            this.lblIndexCount.Location = new System.Drawing.Point(83, 44);
            this.lblIndexCount.Name = "lblIndexCount";
            this.lblIndexCount.Size = new System.Drawing.Size(13, 13);
            this.lblIndexCount.TabIndex = 3;
            this.lblIndexCount.Text = "0";
            // 
            // lblIndicesCaption
            // 
            this.lblIndicesCaption.AutoSize = true;
            this.lblIndicesCaption.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblIndicesCaption.Location = new System.Drawing.Point(26, 44);
            this.lblIndicesCaption.Name = "lblIndicesCaption";
            this.lblIndicesCaption.Size = new System.Drawing.Size(52, 13);
            this.lblIndicesCaption.TabIndex = 2;
            this.lblIndicesCaption.Text = "Indices:";
            // 
            // lblVertexCount
            // 
            this.lblVertexCount.AutoSize = true;
            this.lblVertexCount.Location = new System.Drawing.Point(83, 24);
            this.lblVertexCount.Name = "lblVertexCount";
            this.lblVertexCount.Size = new System.Drawing.Size(13, 13);
            this.lblVertexCount.TabIndex = 1;
            this.lblVertexCount.Text = "0";
            // 
            // lblVerticesCaption
            // 
            this.lblVerticesCaption.AutoSize = true;
            this.lblVerticesCaption.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblVerticesCaption.Location = new System.Drawing.Point(22, 24);
            this.lblVerticesCaption.Name = "lblVerticesCaption";
            this.lblVerticesCaption.Size = new System.Drawing.Size(57, 13);
            this.lblVerticesCaption.TabIndex = 0;
            this.lblVerticesCaption.Text = "Vertices:";
            // 
            // groupBoxVertexFormat
            // 
            this.groupBoxVertexFormat.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxVertexFormat.Controls.Add(this.lblSupportedValue);
            this.groupBoxVertexFormat.Location = new System.Drawing.Point(12, 117);
            this.groupBoxVertexFormat.Name = "groupBoxVertexFormat";
            this.groupBoxVertexFormat.Size = new System.Drawing.Size(356, 58);
            this.groupBoxVertexFormat.TabIndex = 1;
            this.groupBoxVertexFormat.TabStop = false;
            this.groupBoxVertexFormat.Text = "Vertex Format Supports";
            // 
            // lblSupportedValue
            // 
            this.lblSupportedValue.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSupportedValue.AutoSize = true;
            this.lblSupportedValue.Location = new System.Drawing.Point(16, 25);
            this.lblSupportedValue.MaximumSize = new System.Drawing.Size(320, 0);
            this.lblSupportedValue.Name = "lblSupportedValue";
            this.lblSupportedValue.Size = new System.Drawing.Size(37, 13);
            this.lblSupportedValue.TabIndex = 1;
            this.lblSupportedValue.Text = "(none)";
            // 
            // SubmeshInfoForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(380, 186);
            this.Controls.Add(this.groupBoxVertexFormat);
            this.Controls.Add(this.groupBoxSubmesh);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SubmeshInfoForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Submesh info";
            this.groupBoxSubmesh.ResumeLayout(false);
            this.groupBoxSubmesh.PerformLayout();
            this.groupBoxVertexFormat.ResumeLayout(false);
            this.groupBoxVertexFormat.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxSubmesh;
        private System.Windows.Forms.Label lblVerticesCaption;
        private System.Windows.Forms.Label lblVertexCount;
        private System.Windows.Forms.Label lblIndicesCaption;
        private System.Windows.Forms.Label lblIndexCount;
        private System.Windows.Forms.Label lblTrianglesCaption;
        private System.Windows.Forms.Label lblTriangleCount;
        private System.Windows.Forms.Label lblDecodeError;
        private System.Windows.Forms.GroupBox groupBoxVertexFormat;
        private System.Windows.Forms.Label lblSupportedValue;
    }
}
