namespace OpenCAGE.DockPanels
{
    partial class RenderFiltersPanel
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            //Here rather than on closing: a user close only hides the panel, and a level change disposes it without one
            if (disposing)
                SettingsManager.SettingsChanged -= OnSettingsChanged;
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.filterList = new System.Windows.Forms.ListView();
            this.filterTypeColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.filterIcons = new System.Windows.Forms.ImageList(this.components);
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.orderButton = new System.Windows.Forms.Button();
            this.orderTip = new System.Windows.Forms.ToolTip(this.components);
            this.bottomPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // filterList
            // 
            this.filterList.CheckBoxes = true;
            this.filterList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.filterTypeColumn});
            this.filterList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.filterList.FullRowSelect = true;
            this.filterList.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.filterList.HideSelection = false;
            this.filterList.MultiSelect = false;
            this.filterList.Name = "filterList";
            this.filterList.SmallImageList = this.filterIcons;
            this.filterList.TabIndex = 0;
            this.filterList.UseCompatibleStateImageBehavior = false;
            this.filterList.View = System.Windows.Forms.View.Details;
            // 
            // filterTypeColumn
            // 
            this.filterTypeColumn.Text = "Type";
            this.filterTypeColumn.Width = 220;
            // 
            // filterIcons
            // 
            this.filterIcons.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
            this.filterIcons.ImageSize = new System.Drawing.Size(16, 16);
            this.filterIcons.TransparentColor = System.Drawing.Color.Transparent;
            //
            // bottomPanel
            //
            this.bottomPanel.Controls.Add(this.orderButton);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Location = new System.Drawing.Point(8, 254);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Size = new System.Drawing.Size(265, 30);
            this.bottomPanel.TabIndex = 1;
            //
            // orderButton
            //
            this.orderButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.orderButton.Font = new System.Drawing.Font("Segoe UI Symbol", 10F);
            this.orderButton.Location = new System.Drawing.Point(237, 4);
            this.orderButton.Name = "orderButton";
            this.orderButton.Size = new System.Drawing.Size(28, 26);
            this.orderButton.TabIndex = 0;
            this.orderButton.Text = "⚙";
            this.orderTip.SetToolTip(this.orderButton, "Arrange the filters: drag them into your own order and add dividers");
            this.orderButton.UseVisualStyleBackColor = true;
            this.orderButton.Click += new System.EventHandler(this.orderButton_Click);
            //
            // RenderFiltersPanel
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(281, 292);
            this.CloseButtonVisible = false;
            this.Controls.Add(this.filterList);
            this.Controls.Add(this.bottomPanel);
            this.DockAreas = ((WeifenLuo.WinFormsUI.Docking.DockAreas)((WeifenLuo.WinFormsUI.Docking.DockAreas.Float | WeifenLuo.WinFormsUI.Docking.DockAreas.DockLeft)));
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "RenderFiltersPanel";
            this.Padding = new System.Windows.Forms.Padding(8);
            this.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockLeft;
            this.Text = "Render Filters";
            this.bottomPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.ListView filterList;
        private System.Windows.Forms.ColumnHeader filterTypeColumn;
        private System.Windows.Forms.ImageList filterIcons;
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.Button orderButton;
        private System.Windows.Forms.ToolTip orderTip;
    }
}
