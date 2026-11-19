namespace OpenCAGE
{
    partial class TextureImportOptions
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
            this._source = new System.Windows.Forms.Label();
            this._nameLabel = new System.Windows.Forms.Label();
            this._name = new OpenCAGE.Popups.UserControls.AssetNameBox();
            this._storeAsLabel = new System.Windows.Forms.Label();
            this._format = new System.Windows.Forms.ComboBox();
            this._mipmapsLabel = new System.Windows.Forms.Label();
            this._mips = new System.Windows.Forms.TrackBar();
            this._mipLabel = new System.Windows.Forms.Label();
            this._persistent = new System.Windows.Forms.CheckBox();
            this._drop = new System.Windows.Forms.TrackBar();
            this._dropLabel = new System.Windows.Forms.Label();
            this._keepLoaded = new System.Windows.Forms.CheckBox();
            this._ok = new System.Windows.Forms.Button();
            this._cancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this._mips)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._drop)).BeginInit();
            this.SuspendLayout();
            //
            // _source
            //
            this._source.AutoEllipsis = true;
            this._source.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this._source.Location = new System.Drawing.Point(12, 12);
            this._source.Name = "_source";
            this._source.Size = new System.Drawing.Size(516, 20);
            this._source.TabIndex = 0;
            //
            // _nameLabel
            //
            this._nameLabel.AutoSize = true;
            this._nameLabel.Location = new System.Drawing.Point(12, 42);
            this._nameLabel.Name = "_nameLabel";
            this._nameLabel.Size = new System.Drawing.Size(139, 15);
            this._nameLabel.TabIndex = 1;
            this._nameLabel.Text = "Name (use \\ for folders):";
            //
            // _name
            //
            this._name.Location = new System.Drawing.Point(12, 62);
            this._name.Name = "_name";
            this._name.Size = new System.Drawing.Size(516, 44);
            this._name.TabIndex = 2;
            //
            // _storeAsLabel
            //
            this._storeAsLabel.AutoSize = true;
            this._storeAsLabel.Location = new System.Drawing.Point(12, 104);
            this._storeAsLabel.Name = "_storeAsLabel";
            this._storeAsLabel.Size = new System.Drawing.Size(53, 15);
            this._storeAsLabel.TabIndex = 3;
            this._storeAsLabel.Text = "Store as:";
            //
            // _format
            //
            this._format.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._format.Location = new System.Drawing.Point(12, 124);
            this._format.Name = "_format";
            this._format.Size = new System.Drawing.Size(516, 21);
            this._format.TabIndex = 4;
            //
            // _mipmapsLabel
            //
            this._mipmapsLabel.AutoSize = true;
            this._mipmapsLabel.Location = new System.Drawing.Point(12, 158);
            this._mipmapsLabel.Name = "_mipmapsLabel";
            this._mipmapsLabel.Size = new System.Drawing.Size(57, 15);
            this._mipmapsLabel.TabIndex = 5;
            this._mipmapsLabel.Text = "Mipmaps:";
            //
            // _mips
            //
            this._mips.Location = new System.Drawing.Point(70, 152);
            this._mips.Minimum = 1;
            this._mips.Name = "_mips";
            this._mips.Size = new System.Drawing.Size(300, 45);
            this._mips.TabIndex = 6;
            this._mips.TickStyle = System.Windows.Forms.TickStyle.BottomRight;
            this._mips.Value = 1;
            //
            // _mipLabel
            //
            this._mipLabel.Location = new System.Drawing.Point(376, 158);
            this._mipLabel.Name = "_mipLabel";
            this._mipLabel.Size = new System.Drawing.Size(152, 32);
            this._mipLabel.TabIndex = 7;
            //
            // _persistent
            //
            this._persistent.AutoSize = true;
            this._persistent.Location = new System.Drawing.Point(14, 212);
            this._persistent.Name = "_persistent";
            this._persistent.Size = new System.Drawing.Size(245, 19);
            this._persistent.TabIndex = 8;
            this._persistent.Text = "Keep a smaller copy in the persistent slot";
            //
            // _drop
            //
            this._drop.Location = new System.Drawing.Point(70, 236);
            this._drop.Minimum = 1;
            this._drop.Name = "_drop";
            this._drop.Size = new System.Drawing.Size(300, 45);
            this._drop.TabIndex = 9;
            this._drop.TickStyle = System.Windows.Forms.TickStyle.BottomRight;
            this._drop.Value = 1;
            //
            // _dropLabel
            //
            this._dropLabel.Location = new System.Drawing.Point(376, 242);
            this._dropLabel.Name = "_dropLabel";
            this._dropLabel.Size = new System.Drawing.Size(152, 32);
            this._dropLabel.TabIndex = 10;
            //
            // _keepLoaded
            //
            this._keepLoaded.AutoSize = true;
            this._keepLoaded.Location = new System.Drawing.Point(14, 288);
            this._keepLoaded.Name = "_keepLoaded";
            this._keepLoaded.Size = new System.Drawing.Size(466, 19);
            this._keepLoaded.TabIndex = 11;
            this._keepLoaded.Text = "Keep loaded at full size (never streamed: always sharp, memory kept for the level)";
            //
            // _ok
            //
            this._ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._ok.Location = new System.Drawing.Point(372, 317);
            this._ok.Name = "_ok";
            this._ok.Size = new System.Drawing.Size(75, 23);
            this._ok.TabIndex = 12;
            this._ok.Text = "Import";
            //
            // _cancel
            //
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(453, 317);
            this._cancel.Name = "_cancel";
            this._cancel.Size = new System.Drawing.Size(75, 23);
            this._cancel.TabIndex = 13;
            this._cancel.Text = "Cancel";
            //
            // TextureImportOptions
            //
            this.AcceptButton = this._ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(540, 352);
            this.Controls.Add(this._source);
            this.Controls.Add(this._nameLabel);
            this.Controls.Add(this._name);
            this.Controls.Add(this._storeAsLabel);
            this.Controls.Add(this._format);
            this.Controls.Add(this._mipmapsLabel);
            this.Controls.Add(this._mips);
            this.Controls.Add(this._mipLabel);
            this.Controls.Add(this._persistent);
            this.Controls.Add(this._drop);
            this.Controls.Add(this._dropLabel);
            this.Controls.Add(this._keepLoaded);
            this.Controls.Add(this._ok);
            this.Controls.Add(this._cancel);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TextureImportOptions";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import texture";
            ((System.ComponentModel.ISupportInitialize)(this._mips)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._drop)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label _source;
        private System.Windows.Forms.Label _nameLabel;
        private OpenCAGE.Popups.UserControls.AssetNameBox _name;
        private System.Windows.Forms.Label _storeAsLabel;
        private System.Windows.Forms.ComboBox _format;
        private System.Windows.Forms.Label _mipmapsLabel;
        private System.Windows.Forms.TrackBar _mips;
        private System.Windows.Forms.Label _mipLabel;
        private System.Windows.Forms.CheckBox _persistent;
        private System.Windows.Forms.TrackBar _drop;
        private System.Windows.Forms.Label _dropLabel;
        private System.Windows.Forms.CheckBox _keepLoaded;
        private System.Windows.Forms.Button _ok;
        private System.Windows.Forms.Button _cancel;
    }
}
