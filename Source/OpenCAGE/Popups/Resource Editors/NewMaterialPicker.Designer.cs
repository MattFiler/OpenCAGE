namespace OpenCAGE
{
    partial class NewMaterialPicker
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
            this._familyLabel = new System.Windows.Forms.Label();
            this._family = new System.Windows.Forms.ComboBox();
            this._detail = new System.Windows.Forms.Label();
            this._nameLabel = new System.Windows.Forms.Label();
            this._name = new System.Windows.Forms.TextBox();
            this._note = new System.Windows.Forms.Label();
            this._ok = new System.Windows.Forms.Button();
            this._cancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // _familyLabel
            //
            this._familyLabel.Location = new System.Drawing.Point(12, 12);
            this._familyLabel.Name = "_familyLabel";
            this._familyLabel.Size = new System.Drawing.Size(436, 18);
            this._familyLabel.TabIndex = 0;
            this._familyLabel.Text = "Shader type";
            //
            // _family
            //
            this._family.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._family.Location = new System.Drawing.Point(12, 32);
            this._family.Name = "_family";
            this._family.Size = new System.Drawing.Size(436, 21);
            this._family.TabIndex = 1;
            this._family.SelectedIndexChanged += new System.EventHandler(this._family_SelectedIndexChanged);
            //
            // _detail
            //
            this._detail.ForeColor = System.Drawing.SystemColors.GrayText;
            this._detail.Location = new System.Drawing.Point(12, 58);
            this._detail.Name = "_detail";
            this._detail.Size = new System.Drawing.Size(436, 48);
            this._detail.TabIndex = 2;
            //
            // _nameLabel
            //
            this._nameLabel.Location = new System.Drawing.Point(12, 112);
            this._nameLabel.Name = "_nameLabel";
            this._nameLabel.Size = new System.Drawing.Size(436, 18);
            this._nameLabel.TabIndex = 3;
            this._nameLabel.Text = "Material name";
            //
            // _name
            //
            this._name.Location = new System.Drawing.Point(12, 132);
            this._name.Name = "_name";
            this._name.Size = new System.Drawing.Size(436, 23);
            this._name.TabIndex = 4;
            this._name.Text = "new material";
            this._name.TextChanged += new System.EventHandler(this._name_TextChanged);
            //
            // _note
            //
            this._note.ForeColor = System.Drawing.SystemColors.GrayText;
            this._note.Location = new System.Drawing.Point(12, 158);
            this._note.Name = "_note";
            this._note.Size = new System.Drawing.Size(436, 18);
            this._note.TabIndex = 5;
            this._note.Text = "The material starts with no textures. Pick its features and samplers once it opens.";
            //
            // _ok
            //
            this._ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._ok.Location = new System.Drawing.Point(292, 184);
            this._ok.Name = "_ok";
            this._ok.Size = new System.Drawing.Size(75, 26);
            this._ok.TabIndex = 6;
            this._ok.Text = "Create";
            //
            // _cancel
            //
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(373, 184);
            this._cancel.Name = "_cancel";
            this._cancel.Size = new System.Drawing.Size(75, 26);
            this._cancel.TabIndex = 7;
            this._cancel.Text = "Cancel";
            //
            // NewMaterialPicker
            //
            this.AcceptButton = this._ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(460, 222);
            this.Controls.Add(this._familyLabel);
            this.Controls.Add(this._family);
            this.Controls.Add(this._detail);
            this.Controls.Add(this._nameLabel);
            this.Controls.Add(this._name);
            this.Controls.Add(this._note);
            this.Controls.Add(this._ok);
            this.Controls.Add(this._cancel);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NewMaterialPicker";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "New material";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label _familyLabel;
        private System.Windows.Forms.ComboBox _family;
        private System.Windows.Forms.Label _detail;
        private System.Windows.Forms.Label _nameLabel;
        private System.Windows.Forms.TextBox _name;
        private System.Windows.Forms.Label _note;
        private System.Windows.Forms.Button _ok;
        private System.Windows.Forms.Button _cancel;
    }
}
