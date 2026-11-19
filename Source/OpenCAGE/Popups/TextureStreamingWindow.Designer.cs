namespace OpenCAGE
{
    partial class TextureStreamingWindow
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
            this._summary = new System.Windows.Forms.Label();
            this.keepLabel = new System.Windows.Forms.Label();
            this._edge = new System.Windows.Forms.ComboBox();
            this.pxLabel = new System.Windows.Forms.Label();
            this._keepCost = new System.Windows.Forms.Label();
            this._keep = new System.Windows.Forms.Button();
            this._merge = new System.Windows.Forms.Button();
            this.closeBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // _summary
            //
            this._summary.Location = new System.Drawing.Point(12, 12);
            this._summary.Name = "_summary";
            this._summary.Size = new System.Drawing.Size(536, 170);
            this._summary.TabIndex = 0;
            //
            // keepLabel
            //
            this.keepLabel.AutoSize = true;
            this.keepLabel.Location = new System.Drawing.Point(12, 196);
            this.keepLabel.Name = "keepLabel";
            this.keepLabel.Size = new System.Drawing.Size(108, 15);
            this.keepLabel.TabIndex = 1;
            this.keepLabel.Text = "Keep textures up to";
            //
            // _edge
            //
            this._edge.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._edge.Location = new System.Drawing.Point(130, 192);
            this._edge.Name = "_edge";
            this._edge.Size = new System.Drawing.Size(70, 21);
            this._edge.TabIndex = 2;
            //
            // pxLabel
            //
            this.pxLabel.AutoSize = true;
            this.pxLabel.Location = new System.Drawing.Point(206, 196);
            this.pxLabel.Name = "pxLabel";
            this.pxLabel.Size = new System.Drawing.Size(58, 15);
            this.pxLabel.TabIndex = 3;
            this.pxLabel.Text = "px loaded";
            //
            // _keepCost
            //
            this._keepCost.Location = new System.Drawing.Point(12, 220);
            this._keepCost.Name = "_keepCost";
            this._keepCost.Size = new System.Drawing.Size(400, 34);
            this._keepCost.TabIndex = 4;
            //
            // _keep
            //
            this._keep.Location = new System.Drawing.Point(428, 191);
            this._keep.Name = "_keep";
            this._keep.Size = new System.Drawing.Size(120, 24);
            this._keep.TabIndex = 5;
            this._keep.Text = "Keep Loaded";
            this._keep.UseVisualStyleBackColor = true;
            this._keep.Click += new System.EventHandler(this.Keep_Click);
            //
            // _merge
            //
            this._merge.Location = new System.Drawing.Point(12, 262);
            this._merge.Name = "_merge";
            this._merge.Size = new System.Drawing.Size(536, 24);
            this._merge.TabIndex = 6;
            this._merge.UseVisualStyleBackColor = true;
            this._merge.Click += new System.EventHandler(this.Merge_Click);
            //
            // closeBtn
            //
            this.closeBtn.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.closeBtn.Location = new System.Drawing.Point(473, 296);
            this.closeBtn.Name = "closeBtn";
            this.closeBtn.Size = new System.Drawing.Size(75, 23);
            this.closeBtn.TabIndex = 7;
            this.closeBtn.Text = "Close";
            this.closeBtn.UseVisualStyleBackColor = true;
            //
            // TextureStreamingWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.closeBtn;
            this.ClientSize = new System.Drawing.Size(560, 330);
            this.Controls.Add(this._summary);
            this.Controls.Add(this.keepLabel);
            this.Controls.Add(this._edge);
            this.Controls.Add(this.pxLabel);
            this.Controls.Add(this._keepCost);
            this.Controls.Add(this._keep);
            this.Controls.Add(this._merge);
            this.Controls.Add(this.closeBtn);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TextureStreamingWindow";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Texture Streaming";
            this.Shown += new System.EventHandler(this.TextureStreamingWindow_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label _summary;
        private System.Windows.Forms.Label keepLabel;
        private System.Windows.Forms.ComboBox _edge;
        private System.Windows.Forms.Label pxLabel;
        private System.Windows.Forms.Label _keepCost;
        private System.Windows.Forms.Button _keep;
        private System.Windows.Forms.Button _merge;
        private System.Windows.Forms.Button closeBtn;
    }
}
