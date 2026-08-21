using CATHODE.Scripting;
using OpenCAGE.UnityConnection;
using System;
using System.Linq;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.DockPanels
{
    public partial class RenderFiltersPanel : DockContent
    {
        private bool _updating;

        public RenderFiltersPanel()
        {
            InitializeComponent();

            CloseButton = false;
            CloseButtonVisible = false;
            AllowEndUserDocking = false;
            FormClosing += RenderFiltersPanel_FormClosing;
            filterList.ItemChecked += FilterList_ItemChecked;

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
            e.Cancel = true;
            Hide();
        }
    }
}
