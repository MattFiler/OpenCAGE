using CATHODE.Scripting;
using OpenCAGE.UnityConnection;
using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
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
               per-item colours, selection and hover are untouched; only the divider row is ours.
               OwnerDraw does cost the hovered and selected rows their text colour: see HoverTextColour. */
            filterList.OwnerDraw = true;
            filterList.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            filterList.DrawItem += FilterList_DrawItem;
            filterList.DrawSubItem += FilterList_DrawSubItem;
            filterList.ItemCheck += FilterList_ItemCheck;
            filterList.ItemSelectionChanged += FilterList_ItemSelectionChanged;
            HoverTextColour.Attach(filterList);

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

        /* WinForms colours a hovered or selected row from a cache it only fills while OwnerDraw is off, so with it
           on those rows kept the cache's starting colours, the system's black text on white: unreadable on the dark
           theme. Once WinForms has set a row's colours, a row given that pair gets the list's own colours, as it
           would in any other list. The light theme's list colours are that pair, so nothing changes there. */
        private sealed class HoverTextColour : NativeWindow
        {
            private const int WM_REFLECT_NOTIFY = 0x2000 + 0x004E;
            private const int NM_CUSTOMDRAW = -12;
            private const int CDDS_ITEMPREPAINT = 0x00010001;
            private const int CDDS_SUBITEM = 0x00020000;

            [StructLayout(LayoutKind.Sequential)]
            private struct NMHDR
            {
                public IntPtr hwndFrom;
                public IntPtr idFrom;
                public int code;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct NMLVCUSTOMDRAW
            {
                public NMHDR hdr;
                public int dwDrawStage;
                public IntPtr hdc;
                public int left, top, right, bottom;
                public IntPtr dwItemSpec;
                public int uItemState;
                public IntPtr lItemlParam;
                public int clrText;
                public int clrTextBk;
            }

            private static readonly int CodeOffset = Marshal.OffsetOf(typeof(NMHDR), "code").ToInt32();
            private static readonly int TextOffset = Marshal.OffsetOf(typeof(NMLVCUSTOMDRAW), "clrText").ToInt32();
            private static readonly int TextBackOffset = Marshal.OffsetOf(typeof(NMLVCUSTOMDRAW), "clrTextBk").ToInt32();

            private readonly ListView _listView;

            private HoverTextColour(ListView listView)
            {
                _listView = listView;
            }

            public static void Attach(ListView listView)
            {
                HoverTextColour fix = new HoverTextColour(listView);
                if (listView.IsHandleCreated)
                    fix.AssignHandle(listView.Handle);
                //A new handle drops the old subclass with it
                listView.HandleCreated += (s, e) => fix.AssignHandle(listView.Handle);
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg != WM_REFLECT_NOTIFY || Marshal.ReadInt32(m.LParam, CodeOffset) != NM_CUSTOMDRAW)
                    return;
                NMLVCUSTOMDRAW draw = Marshal.PtrToStructure<NMLVCUSTOMDRAW>(m.LParam);
                if (draw.hdr.hwndFrom != Handle || (draw.dwDrawStage & ~CDDS_SUBITEM) != CDDS_ITEMPREPAINT)
                    return;
                if (draw.clrText != ColorTranslator.ToWin32(SystemColors.WindowText) || draw.clrTextBk != ColorTranslator.ToWin32(SystemColors.Window))
                    return;

                Marshal.WriteInt32(m.LParam, TextOffset, ColorTranslator.ToWin32(_listView.ForeColor));
                Marshal.WriteInt32(m.LParam, TextBackOffset, ColorTranslator.ToWin32(_listView.BackColor));
            }
        }
        #endregion

        private void FilterList_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_updating || e.Item?.Tag == null)
                return;

            /* The list reports every checked row again when its handle is created (or recreated) - after RefreshFilters
               has finished - and each one rewrote the setting and sent the viewer the whole filter table: some ninety
               packets on every level load. Only a row whose state actually differs from the setting is a change. */
            if (e.Item.Tag is SceneFilterKind sceneFilter)
            {
                if (RenderFilters.IsSceneFilterEnabled(sceneFilter) == e.Item.Checked)
                    return;
                RenderFilters.SetSceneFilterEnabled(sceneFilter, e.Item.Checked);
                QueueFilterPacket();
                return;
            }

            uint functionType = (uint)e.Item.Tag;
            if (RenderFilters.IsEnabled(functionType) == e.Item.Checked)
                return;
            RenderFilters.SetEnabled(functionType, e.Item.Checked);
            QueueFilterPacket();
        }

        /* Several rows toggled at once (Space on a multi-selection) are one table for the viewer, sent once they are all in */
        private bool _filterPacketQueued;
        private void QueueFilterPacket()
        {
            if (_filterPacketQueued)
                return;
            //The main window's queue, not this panel's: a panel whose handle is recreated drops what was posted to it
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed || !editor.IsHandleCreated)
            {
                UnityConnection.Send.SendRenderFilterPacket();
                return;
            }
            _filterPacketQueued = true;
            editor.BeginInvoke(new Action(() =>
            {
                _filterPacketQueued = false;
                UnityConnection.Send.SendRenderFilterPacket();
            }));
        }

        private void RenderFiltersPanel_FormClosing(object sender, FormClosingEventArgs e)
        {
            //An application exit goes through as it is (see CloseReasons); a user close hides the panel instead
            if (CloseReasons.IsApplicationShutdown(e))
                return;
            e.Cancel = true;
            Hide();
        }
    }
}
