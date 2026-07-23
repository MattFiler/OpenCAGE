namespace OpenCAGE.AnimTrees
{
    partial class AnimationSets
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
            this.animSets = new System.Windows.Forms.ListView();
            this.animTrees = new System.Windows.Forms.ListView();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // animSets
            // 
            this.animSets.Dock = System.Windows.Forms.DockStyle.Fill;
            this.animSets.FullRowSelect = true;
            this.animSets.HideSelection = false;
            this.animSets.Location = new System.Drawing.Point(0, 0);
            this.animSets.MultiSelect = false;
            this.animSets.Name = "animSets";
            this.animSets.Size = new System.Drawing.Size(408, 147);
            this.animSets.TabIndex = 0;
            this.animSets.UseCompatibleStateImageBehavior = false;
            this.animSets.View = System.Windows.Forms.View.List;
            this.animSets.SelectedIndexChanged += new System.EventHandler(this.animSets_SelectedIndexChanged);
            // 
            // animTrees
            // 
            this.animTrees.Dock = System.Windows.Forms.DockStyle.Fill;
            this.animTrees.FullRowSelect = true;
            this.animTrees.HideSelection = false;
            this.animTrees.Location = new System.Drawing.Point(0, 0);
            this.animTrees.MultiSelect = false;
            this.animTrees.Name = "animTrees";
            this.animTrees.Size = new System.Drawing.Size(408, 306);
            this.animTrees.TabIndex = 1;
            this.animTrees.UseCompatibleStateImageBehavior = false;
            this.animTrees.View = System.Windows.Forms.View.List;
            this.animTrees.SelectedIndexChanged += new System.EventHandler(this.animTrees_SelectedIndexChanged);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(12, 12);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.animSets);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.animTrees);
            this.splitContainer1.Size = new System.Drawing.Size(408, 457);
            this.splitContainer1.SplitterDistance = 147;
            this.splitContainer1.TabIndex = 3;
            // 
            // AnimationSets
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(432, 481);
            this.Controls.Add(this.splitContainer1);
            this.Name = "AnimationSets";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Animation Sets";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView animSets;
        private System.Windows.Forms.ListView animTrees;
        private System.Windows.Forms.SplitContainer splitContainer1;
    }
}