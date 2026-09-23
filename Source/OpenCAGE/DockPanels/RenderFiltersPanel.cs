using CATHODE.Scripting;
using OpenCAGE.UnityConnection;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.DockPanels
{
    public partial class RenderFiltersPanel : DockContent
    {
        private bool _updating;

        //The row between the scene geometry and the entity previews: drawn as a line, nothing to tick
        private ListViewItem _divider;
        private int _lastSelected = -1;

        public RenderFiltersPanel()
        {
            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);

            CloseButton = false;
            CloseButtonVisible = false;
            AllowEndUserDocking = false;
            FormClosing += RenderFiltersPanel_FormClosing;
            filterList.ItemChecked += FilterList_ItemChecked;

            /* Every row but the divider is still commctrl's to draw (DrawDefault), so the theme's
               per-item colours, selection and hover are untouched; only the divider row is ours. */
            filterList.OwnerDraw = true;
            filterList.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            filterList.DrawItem += FilterList_DrawItem;
            filterList.DrawSubItem += FilterList_DrawSubItem;
            filterList.ItemCheck += FilterList_ItemCheck;
            filterList.ItemSelectionChanged += FilterList_ItemSelectionChanged;

            SettingsManager.SettingsChanged += OnSettingsChanged;
            RefreshFilters();
        }

        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (!e.ExternalChange || IsDisposed)
                return;

            bool touchesFilters = SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, Settings.BoxRenderFilters);
            foreach (SceneFilterDefinition definition in RenderFilterDefinitions.SceneFilters)
                touchesFilters |= SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, RenderFilters.SceneFilterSettingKey(definition.Kind));
            if (!touchesFilters)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshFilters));
                return;
            }

            RefreshFilters();
        }

        public void RefreshFilters()
        {
            _updating = true;
            try
            {
                filterList.BeginUpdate();
                filterList.Items.Clear();
                filterIcons.Images.Clear();
                _divider = null;
                _lastSelected = -1;

                int imageIndex = 0;

                //Scene geometry categories first: they aren't entity previews, so they sit above the list
                foreach (SceneFilterDefinition definition in RenderFilterDefinitions.SceneFilters)
                {
                    filterIcons.Images.Add(RenderFilters.CreateColorSwatch(RenderFilters.ToMenuColor(definition)));

                    ListViewItem sceneItem = new ListViewItem(definition.Label, imageIndex)
                    {
                        Tag = definition.Kind,
                        Checked = RenderFilters.IsSceneFilterEnabled(definition.Kind),
                    };
                    filterList.Items.Add(sceneItem);
                    imageIndex++;
                }

                //A line under the scene geometry, before the entity previews start
                if (filterList.Items.Count > 0 && RenderFilterDefinitions.All.Length > 0)
                {
                    _divider = new ListViewItem(string.Empty);
                    filterList.Items.Add(_divider);
                }

                foreach (RenderFilterDefinitions.Definition definition in RenderFilterDefinitions.All
                    .OrderBy(definition => definition.FunctionType.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    filterIcons.Images.Add(RenderFilters.CreateFilterListIcon(definition));

                    ListViewItem item = new ListViewItem(definition.FunctionType.ToString(), imageIndex)
                    {
                        Tag = definition.FunctionTypeUInt,
                        Checked = RenderFilters.IsEnabled(definition.FunctionTypeUInt),
                    };
                    filterList.Items.Add(item);
                    imageIndex++;
                }
            }
            finally
            {
                filterList.EndUpdate();
                _updating = false;
            }
        }

        #region DIVIDER
        private void FilterList_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            if (e.Item != _divider)
            {
                e.DrawDefault = true;
                return;
            }

            //None of the row is commctrl's: no checkbox, no text, no selection or hover highlight
            e.DrawDefault = false;
            using (SolidBrush back = new SolidBrush(e.Item.BackColor))
                e.Graphics.FillRectangle(back, e.Bounds);
            Color colour = Theming.ThemeManager.IsDark ? Theming.ThemeColours.BorderStrong : SystemColors.ControlDark;
            int y = e.Bounds.Top + e.Bounds.Height / 2;
            using (Pen pen = new Pen(colour))
                e.Graphics.DrawLine(pen, e.Bounds.Left + 4, y, e.Bounds.Right - 4, y);
        }

        private void FilterList_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            //Only raised for rows DrawItem did not hand back to commctrl, i.e. the divider
            e.DrawDefault = e.Item != _divider;
        }

        /* The divider has a checkbox slot like any row: a click there, a double-click or Space would tick it */
        private void FilterList_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_divider != null && e.Index == _divider.Index)
                e.NewValue = e.CurrentValue;
        }

        /* Arrow keys (or a click) can land on the divider: step over it the way the selection was going */
        private void FilterList_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected)
                return;
            if (e.Item != _divider)
            {
                _lastSelected = e.ItemIndex;
                return;
            }

            int step = _lastSelected > e.ItemIndex ? -1 : 1;
            int index = e.ItemIndex + step;
            if (index < 0 || index >= filterList.Items.Count)
                index = e.ItemIndex - step;
            if (index < 0 || index >= filterList.Items.Count)
                return;

            //After the notification, not inside it
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || index >= filterList.Items.Count || filterList.Items[index] == _divider)
                    return;
                filterList.Items[index].Selected = true;
                filterList.Items[index].Focused = true;
            }));
        }
        #endregion

        private void FilterList_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_updating || e.Item?.Tag == null)
                return;

            if (e.Item.Tag is SceneFilterKind sceneFilter)
            {
                RenderFilters.SetSceneFilterEnabled(sceneFilter, e.Item.Checked);
                UnityConnection.Send.SendRenderFilterPacket();
                return;
            }

            uint functionType = (uint)e.Item.Tag;
            RenderFilters.SetEnabled(functionType, e.Item.Checked);
            UnityConnection.Send.SendRenderFilterPacket();
        }

        private void RenderFiltersPanel_FormClosing(object sender, FormClosingEventArgs e)
        {
            SettingsManager.SettingsChanged -= OnSettingsChanged;
            //An application exit goes through as it is (see CloseReasons); a user close hides the panel instead
            if (CloseReasons.IsApplicationShutdown(e))
                return;
            e.Cancel = true;
            Hide();
        }
    }
}
