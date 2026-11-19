namespace OpenCAGE
{
    partial class BlendSetUserPicker
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
            this._body = new System.Windows.Forms.Panel();
            this._tree = new System.Windows.Forms.TreeView();
            this._top = new System.Windows.Forms.Panel();
            this._search = new System.Windows.Forms.TextBox();
            this._searchLabel = new System.Windows.Forms.Label();
            this._bottom = new System.Windows.Forms.Panel();
            this._ok = new System.Windows.Forms.Button();
            this._cancel = new System.Windows.Forms.Button();
            this._body.SuspendLayout();
            this._top.SuspendLayout();
            this._bottom.SuspendLayout();
            this.SuspendLayout();
            //
            // _body
            //
            this._body.Controls.Add(this._tree);
            this._body.Dock = System.Windows.Forms.DockStyle.Fill;
            this._body.Location = new System.Drawing.Point(0, 30);
            this._body.Name = "_body";
            this._body.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this._body.Size = new System.Drawing.Size(460, 408);
            this._body.TabIndex = 0;
            //
            // _tree
            //
            this._tree.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tree.HideSelection = false;
            this._tree.Location = new System.Drawing.Point(8, 4);
            this._tree.Name = "_tree";
            this._tree.Size = new System.Drawing.Size(444, 400);
            this._tree.TabIndex = 0;
            this._tree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this._tree_AfterSelect);
            this._tree.DoubleClick += new System.EventHandler(this._tree_DoubleClick);
            //
            // _top
            //
            this._top.Controls.Add(this._search);
            this._top.Controls.Add(this._searchLabel);
            this._top.Dock = System.Windows.Forms.DockStyle.Top;
            this._top.Location = new System.Drawing.Point(0, 0);
            this._top.Name = "_top";
            this._top.Padding = new System.Windows.Forms.Padding(8, 5, 8, 0);
            this._top.Size = new System.Drawing.Size(460, 30);
            this._top.TabIndex = 1;
            //
            // _search
            //
            this._search.Dock = System.Windows.Forms.DockStyle.Fill;
            this._search.Location = new System.Drawing.Point(54, 5);
            this._search.Name = "_search";
            this._search.Size = new System.Drawing.Size(398, 20);
            this._search.TabIndex = 0;
            this._search.TextChanged += new System.EventHandler(this._search_TextChanged);
            //
            // _searchLabel
            //
            this._searchLabel.Dock = System.Windows.Forms.DockStyle.Left;
            this._searchLabel.Location = new System.Drawing.Point(8, 5);
            this._searchLabel.Name = "_searchLabel";
            this._searchLabel.Size = new System.Drawing.Size(46, 25);
            this._searchLabel.TabIndex = 1;
            this._searchLabel.Text = "Search";
            this._searchLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _bottom
            //
            this._bottom.Controls.Add(this._ok);
            this._bottom.Controls.Add(this._cancel);
            this._bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._bottom.Location = new System.Drawing.Point(0, 438);
            this._bottom.Name = "_bottom";
            this._bottom.Size = new System.Drawing.Size(460, 42);
            this._bottom.TabIndex = 2;
            //
            // _ok
            //
            this._ok.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._ok.Enabled = false;
            this._ok.Location = new System.Drawing.Point(266, 9);
            this._ok.Name = "_ok";
            this._ok.Size = new System.Drawing.Size(90, 23);
            this._ok.TabIndex = 0;
            this._ok.Text = "Add";
            this._ok.Click += new System.EventHandler(this._ok_Click);
            //
            // _cancel
            //
            this._cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(362, 9);
            this._cancel.Name = "_cancel";
            this._cancel.Size = new System.Drawing.Size(90, 23);
            this._cancel.TabIndex = 1;
            this._cancel.Text = "Cancel";
            //
            // BlendSetUserPicker
            //
            this.AcceptButton = this._ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(460, 480);
            this.Controls.Add(this._body);
            this.Controls.Add(this._top);
            this.Controls.Add(this._bottom);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BlendSetUserPicker";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this._body.ResumeLayout(false);
            this._top.ResumeLayout(false);
            this._top.PerformLayout();
            this._bottom.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel _body;
        private System.Windows.Forms.TreeView _tree;
        private System.Windows.Forms.Panel _top;
        private System.Windows.Forms.TextBox _search;
        private System.Windows.Forms.Label _searchLabel;
        private System.Windows.Forms.Panel _bottom;
        private System.Windows.Forms.Button _ok;
        private System.Windows.Forms.Button _cancel;
    }
}
