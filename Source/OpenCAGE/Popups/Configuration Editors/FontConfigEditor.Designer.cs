namespace OpenCAGE.ConfigEditors
{
    partial class FontConfigEditor
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
            this.components = new System.ComponentModel.Container();
            this.languageLabel = new System.Windows.Forms.Label();
            this.languageList = new System.Windows.Forms.ListBox();
            this.fontLibLabel = new System.Windows.Forms.Label();
            this.fontLib = new System.Windows.Forms.TextBox();
            this.fontsLabel = new System.Windows.Forms.Label();
            this.fontGrid = new System.Windows.Forms.DataGridView();
            this.columnId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnStyle = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.addFont = new System.Windows.Forms.Button();
            this.removeFont = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.fontGrid)).BeginInit();
            this.SuspendLayout();
            //
            // languageLabel
            //
            this.languageLabel.AutoSize = true;
            this.languageLabel.Location = new System.Drawing.Point(12, 9);
            this.languageLabel.Name = "languageLabel";
            this.languageLabel.Size = new System.Drawing.Size(58, 13);
            this.languageLabel.TabIndex = 0;
            this.languageLabel.Text = "Language:";
            //
            // languageList
            //
            this.languageList.FormattingEnabled = true;
            this.languageList.IntegralHeight = false;
            this.languageList.Location = new System.Drawing.Point(15, 28);
            this.languageList.Name = "languageList";
            this.languageList.Size = new System.Drawing.Size(200, 380);
            this.languageList.TabIndex = 1;
            this.languageList.SelectedIndexChanged += new System.EventHandler(this.languageList_SelectedIndexChanged);
            //
            // fontLibLabel
            //
            this.fontLibLabel.AutoSize = true;
            this.fontLibLabel.Location = new System.Drawing.Point(230, 9);
            this.fontLibLabel.Name = "fontLibLabel";
            this.fontLibLabel.Size = new System.Drawing.Size(95, 13);
            this.fontLibLabel.TabIndex = 2;
            this.fontLibLabel.Text = "Font library (.gfx):";
            //
            // fontLib
            //
            this.fontLib.Location = new System.Drawing.Point(233, 28);
            this.fontLib.Name = "fontLib";
            this.fontLib.Size = new System.Drawing.Size(550, 20);
            this.fontLib.TabIndex = 3;
            this.toolTip1.SetToolTip(this.fontLib, "The Scaleform font library this language\'s faces are loaded from, e.g. fonts_en.g" +
        "fx. It has to exist in DATA/UI.PAK.");
            this.fontLib.TextChanged += new System.EventHandler(this.fontLib_TextChanged);
            //
            // fontsLabel
            //
            this.fontsLabel.AutoSize = true;
            this.fontsLabel.Location = new System.Drawing.Point(230, 58);
            this.fontsLabel.Name = "fontsLabel";
            this.fontsLabel.Size = new System.Drawing.Size(37, 13);
            this.fontsLabel.TabIndex = 4;
            this.fontsLabel.Text = "Fonts:";
            //
            // fontGrid
            //
            this.fontGrid.AllowUserToAddRows = false;
            this.fontGrid.AllowUserToDeleteRows = false;
            this.fontGrid.AllowUserToResizeRows = false;
            this.fontGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.fontGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnId,
            this.columnName,
            this.columnStyle});
            this.fontGrid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnKeystrokeOrF2;
            this.fontGrid.Location = new System.Drawing.Point(233, 74);
            this.fontGrid.MultiSelect = false;
            this.fontGrid.Name = "fontGrid";
            this.fontGrid.RowHeadersVisible = false;
            this.fontGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.fontGrid.Size = new System.Drawing.Size(550, 305);
            this.fontGrid.TabIndex = 5;
            this.fontGrid.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.fontGrid_CellValueChanged);
            this.fontGrid.CurrentCellDirtyStateChanged += new System.EventHandler(this.fontGrid_CurrentCellDirtyStateChanged);
            //
            // columnId
            //
            this.columnId.HeaderText = "Slot";
            this.columnId.Name = "columnId";
            this.columnId.ToolTipText = "The name the game\'s UI asks for, e.g. $Isolation. Renaming one the UI still asks " +
        "for leaves that text unstyled.";
            this.columnId.Width = 180;
            //
            // columnName
            //
            this.columnName.HeaderText = "Typeface";
            this.columnName.Name = "columnName";
            this.columnName.ToolTipText = "The face inside the font library, e.g. Nostromo.";
            this.columnName.Width = 220;
            //
            // columnStyle
            //
            this.columnStyle.HeaderText = "Style";
            this.columnStyle.Name = "columnStyle";
            this.columnStyle.ToolTipText = "normal, bold, italic or bold_italic.";
            this.columnStyle.Width = 120;
            //
            // addFont
            //
            this.addFont.Location = new System.Drawing.Point(233, 385);
            this.addFont.Name = "addFont";
            this.addFont.Size = new System.Drawing.Size(110, 23);
            this.addFont.TabIndex = 6;
            this.addFont.Text = "Add Font";
            this.addFont.UseVisualStyleBackColor = true;
            this.addFont.Click += new System.EventHandler(this.addFont_Click);
            //
            // removeFont
            //
            this.removeFont.Location = new System.Drawing.Point(349, 385);
            this.removeFont.Name = "removeFont";
            this.removeFont.Size = new System.Drawing.Size(110, 23);
            this.removeFont.TabIndex = 7;
            this.removeFont.Text = "Remove Font";
            this.removeFont.UseVisualStyleBackColor = true;
            this.removeFont.Click += new System.EventHandler(this.removeFont_Click);
            //
            // FontConfigEditor
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(798, 420);
            this.Controls.Add(this.removeFont);
            this.Controls.Add(this.addFont);
            this.Controls.Add(this.fontGrid);
            this.Controls.Add(this.fontsLabel);
            this.Controls.Add(this.fontLib);
            this.Controls.Add(this.fontLibLabel);
            this.Controls.Add(this.languageList);
            this.Controls.Add(this.languageLabel);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "FontConfigEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Font Config Editor";
            ((System.ComponentModel.ISupportInitialize)(this.fontGrid)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label languageLabel;
        private System.Windows.Forms.ListBox languageList;
        private System.Windows.Forms.Label fontLibLabel;
        private System.Windows.Forms.TextBox fontLib;
        private System.Windows.Forms.Label fontsLabel;
        private System.Windows.Forms.DataGridView fontGrid;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnId;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnStyle;
        private System.Windows.Forms.Button addFont;
        private System.Windows.Forms.Button removeFont;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
