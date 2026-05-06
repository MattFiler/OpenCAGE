namespace CommandsEditor.ConfigEditors
{
    partial class AddLocalisedStringDialog
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddLocalisedStringDialog));
            this.tableLayoutRoot = new System.Windows.Forms.TableLayoutPanel();
            this.labelMission = new System.Windows.Forms.Label();
            this.comboMission = new System.Windows.Forms.ComboBox();
            this.labelStringId = new System.Windows.Forms.Label();
            this.textStringId = new System.Windows.Forms.TextBox();
            this.panelLangScroll = new System.Windows.Forms.Panel();
            this.tableLayoutLang = new System.Windows.Forms.TableLayoutPanel();
            this.labelLangCzech = new System.Windows.Forms.Label();
            this.textLangCzech = new System.Windows.Forms.TextBox();
            this.labelLangEnglish = new System.Windows.Forms.Label();
            this.textLangEnglish = new System.Windows.Forms.TextBox();
            this.labelLangFrench = new System.Windows.Forms.Label();
            this.textLangFrench = new System.Windows.Forms.TextBox();
            this.labelLangGerman = new System.Windows.Forms.Label();
            this.textLangGerman = new System.Windows.Forms.TextBox();
            this.labelLangItalian = new System.Windows.Forms.Label();
            this.textLangItalian = new System.Windows.Forms.TextBox();
            this.labelLangPolish = new System.Windows.Forms.Label();
            this.textLangPolish = new System.Windows.Forms.TextBox();
            this.labelLangPortuguese = new System.Windows.Forms.Label();
            this.textLangPortuguese = new System.Windows.Forms.TextBox();
            this.labelLangRussian = new System.Windows.Forms.Label();
            this.textLangRussian = new System.Windows.Forms.TextBox();
            this.labelLangSpanish = new System.Windows.Forms.Label();
            this.textLangSpanish = new System.Windows.Forms.TextBox();
            this.flowLayoutButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonOK = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.tableLayoutRoot.SuspendLayout();
            this.panelLangScroll.SuspendLayout();
            this.tableLayoutLang.SuspendLayout();
            this.flowLayoutButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutRoot
            // 
            this.tableLayoutRoot.ColumnCount = 2;
            this.tableLayoutRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 118F));
            this.tableLayoutRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutRoot.Controls.Add(this.labelMission, 0, 0);
            this.tableLayoutRoot.Controls.Add(this.comboMission, 1, 0);
            this.tableLayoutRoot.Controls.Add(this.labelStringId, 0, 1);
            this.tableLayoutRoot.Controls.Add(this.textStringId, 1, 1);
            this.tableLayoutRoot.Controls.Add(this.panelLangScroll, 0, 2);
            this.tableLayoutRoot.Controls.Add(this.flowLayoutButtons, 0, 3);
            this.tableLayoutRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutRoot.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutRoot.Name = "tableLayoutRoot";
            this.tableLayoutRoot.Padding = new System.Windows.Forms.Padding(10);
            this.tableLayoutRoot.RowCount = 4;
            this.tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutRoot.Size = new System.Drawing.Size(540, 520);
            this.tableLayoutRoot.TabIndex = 0;
            // 
            // labelMission
            // 
            this.labelMission.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelMission.AutoSize = true;
            this.labelMission.Location = new System.Drawing.Point(13, 19);
            this.labelMission.Margin = new System.Windows.Forms.Padding(3, 6, 8, 6);
            this.labelMission.Name = "labelMission";
            this.labelMission.Size = new System.Drawing.Size(83, 13);
            this.labelMission.TabIndex = 0;
            this.labelMission.Text = "Text file (group):";
            // 
            // comboMission
            // 
            this.comboMission.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboMission.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMission.FormattingEnabled = true;
            this.comboMission.Location = new System.Drawing.Point(128, 14);
            this.comboMission.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            this.comboMission.Name = "comboMission";
            this.comboMission.Size = new System.Drawing.Size(402, 21);
            this.comboMission.TabIndex = 0;
            // 
            // labelStringId
            // 
            this.labelStringId.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelStringId.AutoSize = true;
            this.labelStringId.Location = new System.Drawing.Point(13, 49);
            this.labelStringId.Margin = new System.Windows.Forms.Padding(3, 6, 8, 6);
            this.labelStringId.Name = "labelStringId";
            this.labelStringId.Size = new System.Drawing.Size(51, 13);
            this.labelStringId.TabIndex = 0;
            this.labelStringId.Text = "String ID:";
            // 
            // textStringId
            // 
            this.textStringId.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textStringId.Location = new System.Drawing.Point(128, 45);
            this.textStringId.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            this.textStringId.Name = "textStringId";
            this.textStringId.Size = new System.Drawing.Size(402, 20);
            this.textStringId.TabIndex = 1;
            // 
            // panelLangScroll
            // 
            this.panelLangScroll.AutoScroll = true;
            this.tableLayoutRoot.SetColumnSpan(this.panelLangScroll, 2);
            this.panelLangScroll.Controls.Add(this.tableLayoutLang);
            this.panelLangScroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLangScroll.Location = new System.Drawing.Point(10, 75);
            this.panelLangScroll.Margin = new System.Windows.Forms.Padding(0, 4, 0, 8);
            this.panelLangScroll.Name = "panelLangScroll";
            this.panelLangScroll.Size = new System.Drawing.Size(520, 390);
            this.panelLangScroll.TabIndex = 0;
            this.panelLangScroll.Resize += new System.EventHandler(this.panelLangScroll_Resize);
            // 
            // tableLayoutLang
            // 
            this.tableLayoutLang.AutoSize = true;
            this.tableLayoutLang.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutLang.ColumnCount = 1;
            this.tableLayoutLang.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutLang.Controls.Add(this.labelLangCzech, 0, 0);
            this.tableLayoutLang.Controls.Add(this.textLangCzech, 0, 1);
            this.tableLayoutLang.Controls.Add(this.labelLangEnglish, 0, 2);
            this.tableLayoutLang.Controls.Add(this.textLangEnglish, 0, 3);
            this.tableLayoutLang.Controls.Add(this.labelLangFrench, 0, 4);
            this.tableLayoutLang.Controls.Add(this.textLangFrench, 0, 5);
            this.tableLayoutLang.Controls.Add(this.labelLangGerman, 0, 6);
            this.tableLayoutLang.Controls.Add(this.textLangGerman, 0, 7);
            this.tableLayoutLang.Controls.Add(this.labelLangItalian, 0, 8);
            this.tableLayoutLang.Controls.Add(this.textLangItalian, 0, 9);
            this.tableLayoutLang.Controls.Add(this.labelLangPolish, 0, 10);
            this.tableLayoutLang.Controls.Add(this.textLangPolish, 0, 11);
            this.tableLayoutLang.Controls.Add(this.labelLangPortuguese, 0, 12);
            this.tableLayoutLang.Controls.Add(this.textLangPortuguese, 0, 13);
            this.tableLayoutLang.Controls.Add(this.labelLangRussian, 0, 14);
            this.tableLayoutLang.Controls.Add(this.textLangRussian, 0, 15);
            this.tableLayoutLang.Controls.Add(this.labelLangSpanish, 0, 16);
            this.tableLayoutLang.Controls.Add(this.textLangSpanish, 0, 17);
            this.tableLayoutLang.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutLang.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutLang.Name = "tableLayoutLang";
            this.tableLayoutLang.RowCount = 18;
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLang.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutLang.Size = new System.Drawing.Size(503, 855);
            this.tableLayoutLang.TabIndex = 0;
            // 
            // labelLangCzech
            // 
            this.labelLangCzech.AutoSize = true;
            this.labelLangCzech.Location = new System.Drawing.Point(3, 8);
            this.labelLangCzech.Margin = new System.Windows.Forms.Padding(3, 8, 3, 2);
            this.labelLangCzech.Name = "labelLangCzech";
            this.labelLangCzech.Size = new System.Drawing.Size(43, 13);
            this.labelLangCzech.TabIndex = 0;
            this.labelLangCzech.Text = "CZECH";
            // 
            // textLangCzech
            // 
            this.textLangCzech.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangCzech.Location = new System.Drawing.Point(3, 23);
            this.textLangCzech.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
            this.textLangCzech.Multiline = true;
            this.textLangCzech.Name = "textLangCzech";
            this.textLangCzech.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangCzech.Size = new System.Drawing.Size(497, 66);
            this.textLangCzech.TabIndex = 2;
            // 
            // labelLangEnglish
            // 
            this.labelLangEnglish.AutoSize = true;
            this.labelLangEnglish.Location = new System.Drawing.Point(3, 103);
            this.labelLangEnglish.Margin = new System.Windows.Forms.Padding(3, 8, 3, 2);
            this.labelLangEnglish.Name = "labelLangEnglish";
            this.labelLangEnglish.Size = new System.Drawing.Size(54, 13);
            this.labelLangEnglish.TabIndex = 0;
            this.labelLangEnglish.Text = "ENGLISH";
            // 
            // textLangEnglish
            // 
            this.textLangEnglish.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangEnglish.Location = new System.Drawing.Point(3, 118);
            this.textLangEnglish.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
            this.textLangEnglish.Multiline = true;
            this.textLangEnglish.Name = "textLangEnglish";
            this.textLangEnglish.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangEnglish.Size = new System.Drawing.Size(497, 66);
            this.textLangEnglish.TabIndex = 3;
            // 
            // labelLangFrench
            // 
            this.labelLangFrench.AutoSize = true;
            this.labelLangFrench.Location = new System.Drawing.Point(3, 198);
            this.labelLangFrench.Margin = new System.Windows.Forms.Padding(3, 8, 3, 2);
            this.labelLangFrench.Name = "labelLangFrench";
            this.labelLangFrench.Size = new System.Drawing.Size(51, 13);
            this.labelLangFrench.TabIndex = 0;
            this.labelLangFrench.Text = "FRENCH";
            // 
            // textLangFrench
            // 
            this.textLangFrench.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangFrench.Location = new System.Drawing.Point(3, 213);
            this.textLangFrench.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
            this.textLangFrench.Multiline = true;
            this.textLangFrench.Name = "textLangFrench";
            this.textLangFrench.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangFrench.Size = new System.Drawing.Size(497, 66);
            this.textLangFrench.TabIndex = 4;
            // 
            // labelLangGerman
            // 
            this.labelLangGerman.AutoSize = true;
            this.labelLangGerman.Location = new System.Drawing.Point(3, 293);
            this.labelLangGerman.Margin = new System.Windows.Forms.Padding(3, 8, 3, 2);
            this.labelLangGerman.Name = "labelLangGerman";
            this.labelLangGerman.Size = new System.Drawing.Size(54, 13);
            this.labelLangGerman.TabIndex = 0;
            this.labelLangGerman.Text = "GERMAN";
            // 
            // textLangGerman
            // 
            this.textLangGerman.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangGerman.Location = new System.Drawing.Point(3, 308);
            this.textLangGerman.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
            this.textLangGerman.Multiline = true;
            this.textLangGerman.Name = "textLangGerman";
            this.textLangGerman.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangGerman.Size = new System.Drawing.Size(497, 66);
            this.textLangGerman.TabIndex = 5;
            // 
            // labelLangItalian
            // 
            this.labelLangItalian.AutoSize = true;
            this.labelLangItalian.Location = new System.Drawing.Point(3, 388);
            this.labelLangItalian.Margin = new System.Windows.Forms.Padding(3, 8, 3, 2);
            this.labelLangItalian.Name = "labelLangItalian";
            this.labelLangItalian.Size = new System.Drawing.Size(48, 13);
            this.labelLangItalian.TabIndex = 0;
            this.labelLangItalian.Text = "ITALIAN";
            // 
            // textLangItalian
            // 
            this.textLangItalian.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangItalian.Location = new System.Drawing.Point(3, 403);
            this.textLangItalian.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
            this.textLangItalian.Multiline = true;
            this.textLangItalian.Name = "textLangItalian";
            this.textLangItalian.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangItalian.Size = new System.Drawing.Size(497, 66);
            this.textLangItalian.TabIndex = 6;
            // 
            // labelLangPolish
            // 
            this.labelLangPolish.AutoSize = true;
            this.labelLangPolish.Location = new System.Drawing.Point(3, 483);
            this.labelLangPolish.Margin = new System.Windows.Forms.Padding(3, 8, 3, 2);
            this.labelLangPolish.Name = "labelLangPolish";
            this.labelLangPolish.Size = new System.Drawing.Size(46, 13);
            this.labelLangPolish.TabIndex = 0;
            this.labelLangPolish.Text = "POLISH";
            // 
            // textLangPolish
            // 
            this.textLangPolish.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangPolish.Location = new System.Drawing.Point(3, 498);
            this.textLangPolish.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
            this.textLangPolish.Multiline = true;
            this.textLangPolish.Name = "textLangPolish";
            this.textLangPolish.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangPolish.Size = new System.Drawing.Size(497, 66);
            this.textLangPolish.TabIndex = 7;
            // 
            // labelLangPortuguese
            // 
            this.labelLangPortuguese.AutoSize = true;
            this.labelLangPortuguese.Location = new System.Drawing.Point(3, 578);
            this.labelLangPortuguese.Margin = new System.Windows.Forms.Padding(3, 8, 3, 2);
            this.labelLangPortuguese.Name = "labelLangPortuguese";
            this.labelLangPortuguese.Size = new System.Drawing.Size(82, 13);
            this.labelLangPortuguese.TabIndex = 0;
            this.labelLangPortuguese.Text = "PORTUGUESE";
            // 
            // textLangPortuguese
            // 
            this.textLangPortuguese.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangPortuguese.Location = new System.Drawing.Point(3, 593);
            this.textLangPortuguese.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
            this.textLangPortuguese.Multiline = true;
            this.textLangPortuguese.Name = "textLangPortuguese";
            this.textLangPortuguese.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangPortuguese.Size = new System.Drawing.Size(497, 66);
            this.textLangPortuguese.TabIndex = 8;
            // 
            // labelLangRussian
            // 
            this.labelLangRussian.AutoSize = true;
            this.labelLangRussian.Location = new System.Drawing.Point(3, 673);
            this.labelLangRussian.Margin = new System.Windows.Forms.Padding(3, 8, 3, 2);
            this.labelLangRussian.Name = "labelLangRussian";
            this.labelLangRussian.Size = new System.Drawing.Size(55, 13);
            this.labelLangRussian.TabIndex = 0;
            this.labelLangRussian.Text = "RUSSIAN";
            // 
            // textLangRussian
            // 
            this.textLangRussian.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangRussian.Location = new System.Drawing.Point(3, 688);
            this.textLangRussian.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
            this.textLangRussian.Multiline = true;
            this.textLangRussian.Name = "textLangRussian";
            this.textLangRussian.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangRussian.Size = new System.Drawing.Size(497, 66);
            this.textLangRussian.TabIndex = 9;
            // 
            // labelLangSpanish
            // 
            this.labelLangSpanish.AutoSize = true;
            this.labelLangSpanish.Location = new System.Drawing.Point(3, 768);
            this.labelLangSpanish.Margin = new System.Windows.Forms.Padding(3, 8, 3, 2);
            this.labelLangSpanish.Name = "labelLangSpanish";
            this.labelLangSpanish.Size = new System.Drawing.Size(54, 13);
            this.labelLangSpanish.TabIndex = 0;
            this.labelLangSpanish.Text = "SPANISH";
            // 
            // textLangSpanish
            // 
            this.textLangSpanish.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangSpanish.Location = new System.Drawing.Point(3, 783);
            this.textLangSpanish.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
            this.textLangSpanish.Multiline = true;
            this.textLangSpanish.Name = "textLangSpanish";
            this.textLangSpanish.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangSpanish.Size = new System.Drawing.Size(497, 66);
            this.textLangSpanish.TabIndex = 10;
            // 
            // flowLayoutButtons
            // 
            this.flowLayoutButtons.AutoSize = true;
            this.tableLayoutRoot.SetColumnSpan(this.flowLayoutButtons, 2);
            this.flowLayoutButtons.Controls.Add(this.buttonOK);
            this.flowLayoutButtons.Controls.Add(this.buttonCancel);
            this.flowLayoutButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutButtons.Location = new System.Drawing.Point(10, 477);
            this.flowLayoutButtons.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.flowLayoutButtons.Name = "flowLayoutButtons";
            this.flowLayoutButtons.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.flowLayoutButtons.Size = new System.Drawing.Size(520, 33);
            this.flowLayoutButtons.TabIndex = 0;
            this.flowLayoutButtons.WrapContents = false;
            // 
            // buttonOK
            // 
            this.buttonOK.AutoSize = true;
            this.buttonOK.Location = new System.Drawing.Point(442, 7);
            this.buttonOK.Margin = new System.Windows.Forms.Padding(8, 3, 3, 3);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 11;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.AutoSize = true;
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(356, 7);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 12;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // AddLocalisedStringDialog
            // 
            this.AcceptButton = this.buttonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(540, 520);
            this.Controls.Add(this.tableLayoutRoot);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AddLocalisedStringDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Add new localised string";
            this.Shown += new System.EventHandler(this.AddLocalisedStringDialog_Shown);
            this.tableLayoutRoot.ResumeLayout(false);
            this.tableLayoutRoot.PerformLayout();
            this.panelLangScroll.ResumeLayout(false);
            this.panelLangScroll.PerformLayout();
            this.tableLayoutLang.ResumeLayout(false);
            this.tableLayoutLang.PerformLayout();
            this.flowLayoutButtons.ResumeLayout(false);
            this.flowLayoutButtons.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutRoot;
        private System.Windows.Forms.Label labelMission;
        private System.Windows.Forms.ComboBox comboMission;
        private System.Windows.Forms.Label labelStringId;
        private System.Windows.Forms.TextBox textStringId;
        private System.Windows.Forms.Panel panelLangScroll;
        private System.Windows.Forms.TableLayoutPanel tableLayoutLang;
        private System.Windows.Forms.Label labelLangCzech;
        private System.Windows.Forms.TextBox textLangCzech;
        private System.Windows.Forms.Label labelLangEnglish;
        private System.Windows.Forms.TextBox textLangEnglish;
        private System.Windows.Forms.Label labelLangFrench;
        private System.Windows.Forms.TextBox textLangFrench;
        private System.Windows.Forms.Label labelLangGerman;
        private System.Windows.Forms.TextBox textLangGerman;
        private System.Windows.Forms.Label labelLangItalian;
        private System.Windows.Forms.TextBox textLangItalian;
        private System.Windows.Forms.Label labelLangPolish;
        private System.Windows.Forms.TextBox textLangPolish;
        private System.Windows.Forms.Label labelLangPortuguese;
        private System.Windows.Forms.TextBox textLangPortuguese;
        private System.Windows.Forms.Label labelLangRussian;
        private System.Windows.Forms.TextBox textLangRussian;
        private System.Windows.Forms.Label labelLangSpanish;
        private System.Windows.Forms.TextBox textLangSpanish;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutButtons;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonCancel;
    }
}

