namespace OpenCAGE.ConfigEditors
{
    partial class LocalisationEditor
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
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
                if (this.searchDebounceTimer != null)
                {
                    this.searchDebounceTimer.Stop();
                    this.searchDebounceTimer.Dispose();
                }
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LocalisationEditor));
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.listViewStrings = new System.Windows.Forms.ListView();
            this.columnHeaderId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderEnglish = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panelListSearch = new System.Windows.Forms.Panel();
            this.buttonAddString = new System.Windows.Forms.Button();
            this.textSearchFilter = new System.Windows.Forms.TextBox();
            this.labelSearch = new System.Windows.Forms.Label();
            this.panelLanguagesScroll = new System.Windows.Forms.Panel();
            this.tableLayoutLanguages = new System.Windows.Forms.TableLayoutPanel();
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
            this.updateString = new System.Windows.Forms.Button();
            this.searchDebounceTimer = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.panelListSearch.SuspendLayout();
            this.panelLanguagesScroll.SuspendLayout();
            this.tableLayoutLanguages.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitMain
            // 
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 0);
            this.splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.listViewStrings);
            this.splitMain.Panel1.Controls.Add(this.panelListSearch);
            this.splitMain.Panel1MinSize = 200;
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.panelLanguagesScroll);
            this.splitMain.Panel2.Controls.Add(this.updateString);
            this.splitMain.Panel2MinSize = 280;
            this.splitMain.Size = new System.Drawing.Size(926, 667);
            this.splitMain.SplitterDistance = 391;
            this.splitMain.SplitterWidth = 6;
            this.splitMain.TabIndex = 0;
            // 
            // listViewStrings
            // 
            this.listViewStrings.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderId,
            this.columnHeaderEnglish});
            this.listViewStrings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewStrings.FullRowSelect = true;
            this.listViewStrings.HideSelection = false;
            this.listViewStrings.Location = new System.Drawing.Point(0, 72);
            this.listViewStrings.Name = "listViewStrings";
            this.listViewStrings.Size = new System.Drawing.Size(391, 595);
            this.listViewStrings.TabIndex = 2;
            this.listViewStrings.UseCompatibleStateImageBehavior = false;
            this.listViewStrings.View = System.Windows.Forms.View.Details;
            this.listViewStrings.SelectedIndexChanged += new System.EventHandler(this.listViewStrings_SelectedIndexChanged);
            // 
            // columnHeaderId
            // 
            this.columnHeaderId.Text = "ID";
            this.columnHeaderId.Width = 200;
            // 
            // columnHeaderEnglish
            // 
            this.columnHeaderEnglish.Text = "English";
            this.columnHeaderEnglish.Width = 400;
            // 
            // panelListSearch
            // 
            this.panelListSearch.Controls.Add(this.buttonAddString);
            this.panelListSearch.Controls.Add(this.textSearchFilter);
            this.panelListSearch.Controls.Add(this.labelSearch);
            this.panelListSearch.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelListSearch.Location = new System.Drawing.Point(0, 0);
            this.panelListSearch.Name = "panelListSearch";
            this.panelListSearch.Padding = new System.Windows.Forms.Padding(4, 8, 4, 8);
            this.panelListSearch.Size = new System.Drawing.Size(391, 72);
            this.panelListSearch.TabIndex = 0;
            this.panelListSearch.Resize += new System.EventHandler(this.panelListSearch_Resize);
            // 
            // buttonAddString
            // 
            this.buttonAddString.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonAddString.Location = new System.Drawing.Point(4, 40);
            this.buttonAddString.Name = "buttonAddString";
            this.buttonAddString.Size = new System.Drawing.Size(128, 24);
            this.buttonAddString.TabIndex = 2;
            this.buttonAddString.Text = "Add new string…";
            this.buttonAddString.UseVisualStyleBackColor = true;
            this.buttonAddString.Click += new System.EventHandler(this.buttonAddString_Click);
            // 
            // textSearchFilter
            // 
            this.textSearchFilter.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textSearchFilter.Location = new System.Drawing.Point(140, 9);
            this.textSearchFilter.Name = "textSearchFilter";
            this.textSearchFilter.Size = new System.Drawing.Size(247, 20);
            this.textSearchFilter.TabIndex = 1;
            this.textSearchFilter.TextChanged += new System.EventHandler(this.textSearchFilter_TextChanged);
            // 
            // labelSearch
            // 
            this.labelSearch.AutoSize = true;
            this.labelSearch.Location = new System.Drawing.Point(4, 12);
            this.labelSearch.Name = "labelSearch";
            this.labelSearch.Size = new System.Drawing.Size(113, 13);
            this.labelSearch.TabIndex = 0;
            this.labelSearch.Text = "Search (ID or English):";
            // 
            // panelLanguagesScroll
            // 
            this.panelLanguagesScroll.AutoScroll = true;
            this.panelLanguagesScroll.Controls.Add(this.tableLayoutLanguages);
            this.panelLanguagesScroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLanguagesScroll.Location = new System.Drawing.Point(0, 0);
            this.panelLanguagesScroll.Name = "panelLanguagesScroll";
            this.panelLanguagesScroll.Padding = new System.Windows.Forms.Padding(4);
            this.panelLanguagesScroll.Size = new System.Drawing.Size(529, 625);
            this.panelLanguagesScroll.TabIndex = 0;
            this.panelLanguagesScroll.Resize += new System.EventHandler(this.panelLanguagesScroll_Resize);
            // 
            // tableLayoutLanguages
            // 
            this.tableLayoutLanguages.AutoSize = true;
            this.tableLayoutLanguages.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutLanguages.ColumnCount = 1;
            this.tableLayoutLanguages.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutLanguages.Controls.Add(this.labelLangCzech, 0, 0);
            this.tableLayoutLanguages.Controls.Add(this.textLangCzech, 0, 1);
            this.tableLayoutLanguages.Controls.Add(this.labelLangEnglish, 0, 2);
            this.tableLayoutLanguages.Controls.Add(this.textLangEnglish, 0, 3);
            this.tableLayoutLanguages.Controls.Add(this.labelLangFrench, 0, 4);
            this.tableLayoutLanguages.Controls.Add(this.textLangFrench, 0, 5);
            this.tableLayoutLanguages.Controls.Add(this.labelLangGerman, 0, 6);
            this.tableLayoutLanguages.Controls.Add(this.textLangGerman, 0, 7);
            this.tableLayoutLanguages.Controls.Add(this.labelLangItalian, 0, 8);
            this.tableLayoutLanguages.Controls.Add(this.textLangItalian, 0, 9);
            this.tableLayoutLanguages.Controls.Add(this.labelLangPolish, 0, 10);
            this.tableLayoutLanguages.Controls.Add(this.textLangPolish, 0, 11);
            this.tableLayoutLanguages.Controls.Add(this.labelLangPortuguese, 0, 12);
            this.tableLayoutLanguages.Controls.Add(this.textLangPortuguese, 0, 13);
            this.tableLayoutLanguages.Controls.Add(this.labelLangRussian, 0, 14);
            this.tableLayoutLanguages.Controls.Add(this.textLangRussian, 0, 15);
            this.tableLayoutLanguages.Controls.Add(this.labelLangSpanish, 0, 16);
            this.tableLayoutLanguages.Controls.Add(this.textLangSpanish, 0, 17);
            this.tableLayoutLanguages.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutLanguages.Location = new System.Drawing.Point(4, 4);
            this.tableLayoutLanguages.Name = "tableLayoutLanguages";
            this.tableLayoutLanguages.RowCount = 18;
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutLanguages.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutLanguages.Size = new System.Drawing.Size(504, 1017);
            this.tableLayoutLanguages.TabIndex = 0;
            // 
            // labelLangCzech
            // 
            this.labelLangCzech.AutoSize = true;
            this.labelLangCzech.Location = new System.Drawing.Point(8, 10);
            this.labelLangCzech.Margin = new System.Windows.Forms.Padding(8, 10, 8, 4);
            this.labelLangCzech.Name = "labelLangCzech";
            this.labelLangCzech.Size = new System.Drawing.Size(43, 13);
            this.labelLangCzech.TabIndex = 0;
            this.labelLangCzech.Text = "CZECH";
            // 
            // textLangCzech
            // 
            this.textLangCzech.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangCzech.Location = new System.Drawing.Point(8, 27);
            this.textLangCzech.Margin = new System.Windows.Forms.Padding(8, 0, 8, 8);
            this.textLangCzech.Multiline = true;
            this.textLangCzech.Name = "textLangCzech";
            this.textLangCzech.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangCzech.Size = new System.Drawing.Size(488, 78);
            this.textLangCzech.TabIndex = 1;
            // 
            // labelLangEnglish
            // 
            this.labelLangEnglish.AutoSize = true;
            this.labelLangEnglish.Location = new System.Drawing.Point(8, 123);
            this.labelLangEnglish.Margin = new System.Windows.Forms.Padding(8, 10, 8, 4);
            this.labelLangEnglish.Name = "labelLangEnglish";
            this.labelLangEnglish.Size = new System.Drawing.Size(54, 13);
            this.labelLangEnglish.TabIndex = 2;
            this.labelLangEnglish.Text = "ENGLISH";
            // 
            // textLangEnglish
            // 
            this.textLangEnglish.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangEnglish.Location = new System.Drawing.Point(8, 140);
            this.textLangEnglish.Margin = new System.Windows.Forms.Padding(8, 0, 8, 8);
            this.textLangEnglish.Multiline = true;
            this.textLangEnglish.Name = "textLangEnglish";
            this.textLangEnglish.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangEnglish.Size = new System.Drawing.Size(488, 78);
            this.textLangEnglish.TabIndex = 3;
            // 
            // labelLangFrench
            // 
            this.labelLangFrench.AutoSize = true;
            this.labelLangFrench.Location = new System.Drawing.Point(8, 236);
            this.labelLangFrench.Margin = new System.Windows.Forms.Padding(8, 10, 8, 4);
            this.labelLangFrench.Name = "labelLangFrench";
            this.labelLangFrench.Size = new System.Drawing.Size(51, 13);
            this.labelLangFrench.TabIndex = 4;
            this.labelLangFrench.Text = "FRENCH";
            // 
            // textLangFrench
            // 
            this.textLangFrench.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangFrench.Location = new System.Drawing.Point(8, 253);
            this.textLangFrench.Margin = new System.Windows.Forms.Padding(8, 0, 8, 8);
            this.textLangFrench.Multiline = true;
            this.textLangFrench.Name = "textLangFrench";
            this.textLangFrench.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangFrench.Size = new System.Drawing.Size(488, 78);
            this.textLangFrench.TabIndex = 5;
            // 
            // labelLangGerman
            // 
            this.labelLangGerman.AutoSize = true;
            this.labelLangGerman.Location = new System.Drawing.Point(8, 349);
            this.labelLangGerman.Margin = new System.Windows.Forms.Padding(8, 10, 8, 4);
            this.labelLangGerman.Name = "labelLangGerman";
            this.labelLangGerman.Size = new System.Drawing.Size(54, 13);
            this.labelLangGerman.TabIndex = 6;
            this.labelLangGerman.Text = "GERMAN";
            // 
            // textLangGerman
            // 
            this.textLangGerman.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangGerman.Location = new System.Drawing.Point(8, 366);
            this.textLangGerman.Margin = new System.Windows.Forms.Padding(8, 0, 8, 8);
            this.textLangGerman.Multiline = true;
            this.textLangGerman.Name = "textLangGerman";
            this.textLangGerman.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangGerman.Size = new System.Drawing.Size(488, 78);
            this.textLangGerman.TabIndex = 7;
            // 
            // labelLangItalian
            // 
            this.labelLangItalian.AutoSize = true;
            this.labelLangItalian.Location = new System.Drawing.Point(8, 462);
            this.labelLangItalian.Margin = new System.Windows.Forms.Padding(8, 10, 8, 4);
            this.labelLangItalian.Name = "labelLangItalian";
            this.labelLangItalian.Size = new System.Drawing.Size(48, 13);
            this.labelLangItalian.TabIndex = 8;
            this.labelLangItalian.Text = "ITALIAN";
            // 
            // textLangItalian
            // 
            this.textLangItalian.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangItalian.Location = new System.Drawing.Point(8, 479);
            this.textLangItalian.Margin = new System.Windows.Forms.Padding(8, 0, 8, 8);
            this.textLangItalian.Multiline = true;
            this.textLangItalian.Name = "textLangItalian";
            this.textLangItalian.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangItalian.Size = new System.Drawing.Size(488, 78);
            this.textLangItalian.TabIndex = 9;
            // 
            // labelLangPolish
            // 
            this.labelLangPolish.AutoSize = true;
            this.labelLangPolish.Location = new System.Drawing.Point(8, 575);
            this.labelLangPolish.Margin = new System.Windows.Forms.Padding(8, 10, 8, 4);
            this.labelLangPolish.Name = "labelLangPolish";
            this.labelLangPolish.Size = new System.Drawing.Size(46, 13);
            this.labelLangPolish.TabIndex = 10;
            this.labelLangPolish.Text = "POLISH";
            // 
            // textLangPolish
            // 
            this.textLangPolish.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangPolish.Location = new System.Drawing.Point(8, 592);
            this.textLangPolish.Margin = new System.Windows.Forms.Padding(8, 0, 8, 8);
            this.textLangPolish.Multiline = true;
            this.textLangPolish.Name = "textLangPolish";
            this.textLangPolish.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangPolish.Size = new System.Drawing.Size(488, 78);
            this.textLangPolish.TabIndex = 11;
            // 
            // labelLangPortuguese
            // 
            this.labelLangPortuguese.AutoSize = true;
            this.labelLangPortuguese.Location = new System.Drawing.Point(8, 688);
            this.labelLangPortuguese.Margin = new System.Windows.Forms.Padding(8, 10, 8, 4);
            this.labelLangPortuguese.Name = "labelLangPortuguese";
            this.labelLangPortuguese.Size = new System.Drawing.Size(82, 13);
            this.labelLangPortuguese.TabIndex = 12;
            this.labelLangPortuguese.Text = "PORTUGUESE";
            // 
            // textLangPortuguese
            // 
            this.textLangPortuguese.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangPortuguese.Location = new System.Drawing.Point(8, 705);
            this.textLangPortuguese.Margin = new System.Windows.Forms.Padding(8, 0, 8, 8);
            this.textLangPortuguese.Multiline = true;
            this.textLangPortuguese.Name = "textLangPortuguese";
            this.textLangPortuguese.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangPortuguese.Size = new System.Drawing.Size(488, 78);
            this.textLangPortuguese.TabIndex = 13;
            // 
            // labelLangRussian
            // 
            this.labelLangRussian.AutoSize = true;
            this.labelLangRussian.Location = new System.Drawing.Point(8, 801);
            this.labelLangRussian.Margin = new System.Windows.Forms.Padding(8, 10, 8, 4);
            this.labelLangRussian.Name = "labelLangRussian";
            this.labelLangRussian.Size = new System.Drawing.Size(55, 13);
            this.labelLangRussian.TabIndex = 14;
            this.labelLangRussian.Text = "RUSSIAN";
            // 
            // textLangRussian
            // 
            this.textLangRussian.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangRussian.Location = new System.Drawing.Point(8, 818);
            this.textLangRussian.Margin = new System.Windows.Forms.Padding(8, 0, 8, 8);
            this.textLangRussian.Multiline = true;
            this.textLangRussian.Name = "textLangRussian";
            this.textLangRussian.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangRussian.Size = new System.Drawing.Size(488, 78);
            this.textLangRussian.TabIndex = 15;
            // 
            // labelLangSpanish
            // 
            this.labelLangSpanish.AutoSize = true;
            this.labelLangSpanish.Location = new System.Drawing.Point(8, 914);
            this.labelLangSpanish.Margin = new System.Windows.Forms.Padding(8, 10, 8, 4);
            this.labelLangSpanish.Name = "labelLangSpanish";
            this.labelLangSpanish.Size = new System.Drawing.Size(54, 13);
            this.labelLangSpanish.TabIndex = 16;
            this.labelLangSpanish.Text = "SPANISH";
            // 
            // textLangSpanish
            // 
            this.textLangSpanish.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textLangSpanish.Location = new System.Drawing.Point(8, 931);
            this.textLangSpanish.Margin = new System.Windows.Forms.Padding(8, 0, 8, 8);
            this.textLangSpanish.Multiline = true;
            this.textLangSpanish.Name = "textLangSpanish";
            this.textLangSpanish.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLangSpanish.Size = new System.Drawing.Size(488, 78);
            this.textLangSpanish.TabIndex = 17;
            // 
            // updateString
            // 
            this.updateString.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.updateString.Location = new System.Drawing.Point(0, 625);
            this.updateString.Name = "updateString";
            this.updateString.Size = new System.Drawing.Size(529, 42);
            this.updateString.TabIndex = 1;
            this.updateString.Text = "Save all languages";
            this.updateString.UseVisualStyleBackColor = true;
            this.updateString.Click += new System.EventHandler(this.updateString_Click);
            // 
            // searchDebounceTimer
            // 
            this.searchDebounceTimer.Interval = 300;
            this.searchDebounceTimer.Tick += new System.EventHandler(this.searchDebounceTimer_Tick);
            // 
            // LocalisationEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(926, 667);
            this.Controls.Add(this.splitMain);
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "LocalisationEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Localisation Editor";
            this.Load += new System.EventHandler(this.LocalisationEditor_Load);
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.panelListSearch.ResumeLayout(false);
            this.panelListSearch.PerformLayout();
            this.panelLanguagesScroll.ResumeLayout(false);
            this.panelLanguagesScroll.PerformLayout();
            this.tableLayoutLanguages.ResumeLayout(false);
            this.tableLayoutLanguages.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.ListView listViewStrings;
        private System.Windows.Forms.ColumnHeader columnHeaderId;
        private System.Windows.Forms.ColumnHeader columnHeaderEnglish;
        private System.Windows.Forms.Panel panelListSearch;
        private System.Windows.Forms.Label labelSearch;
        private System.Windows.Forms.TextBox textSearchFilter;
        private System.Windows.Forms.Button buttonAddString;
        private System.Windows.Forms.Timer searchDebounceTimer;
        private System.Windows.Forms.Panel panelLanguagesScroll;
        private System.Windows.Forms.TableLayoutPanel tableLayoutLanguages;
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
        private System.Windows.Forms.Button updateString;
    }
}

