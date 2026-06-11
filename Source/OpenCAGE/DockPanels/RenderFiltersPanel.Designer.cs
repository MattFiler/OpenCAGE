namespace OpenCAGE.DockPanels
{
    partial class RenderFiltersPanel
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
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
            // RenderFiltersPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(281, 292);
            this.CloseButtonVisible = false;
            this.Controls.Add(this.filterList);
            this.DockAreas = ((WeifenLuo.WinFormsUI.Docking.DockAreas)((WeifenLuo.WinFormsUI.Docking.DockAreas.Float | WeifenLuo.WinFormsUI.Docking.DockAreas.DockRight)));
            this.Icon = global::OpenCAGE.SharedFormIcon.Icon;
            this.Name = "RenderFiltersPanel";
            this.Padding = new System.Windows.Forms.Padding(8);
            this.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockRight;
            this.Text = "Render Filters";
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.ListView filterList;
        private System.Windows.Forms.ColumnHeader filterTypeColumn;
        private System.Windows.Forms.ImageList filterIcons;
    }
}
