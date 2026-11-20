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

        //Divider rows carry this as their tag: drawn as a line, nothing to tick
        private static readonly object DividerTag = new object();
        private static bool IsDivider(ListViewItem item) => item != null && item.Tag == DividerTag;
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

            bool touchesFilters = SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, Settings.BoxRenderFilters)
                || SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, Settings.RenderFilterOrder);
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
                _lastSelected = -1;

                int imageIndex = 0;

                //In the user's order (the cog button), scene geometry and a divider before the entity previews by default
                foreach (string entry in RenderFilters.LoadOrder())
                {
                    if (entry == RenderFilters.DividerEntry)
                    {
                        filterList.Items.Add(new ListViewItem(string.Empty) { Tag = DividerTag });
                        continue;
                    }

                    if (RenderFilters.TryParseSceneEntry(entry, out SceneFilterKind kind) && RenderFilterDefinitions.TryGetSceneFilter(kind, out SceneFilterDefinition scene))
                    {
                        filterIcons.Images.Add(RenderFilters.CreateColorSwatch(RenderFilters.ToMenuColor(scene)));
                        filterList.Items.Add(new ListViewItem(scene.Label, imageIndex)
                        {
                            Tag = scene.Kind,
                            Checked = RenderFilters.IsSceneFilterEnabled(scene.Kind),
                        });
                        imageIndex++;
                        continue;
                    }

                    if (RenderFilters.TryParseTypeEntry(entry, out uint functionType) && RenderFilterDefinitions.TryGetDefinition((FunctionType)functionType, out RenderFilterDefinitions.Definition definition))
                    {
                        filterIcons.Images.Add(RenderFilters.CreateFilterListIcon(definition));
                        filterList.Items.Add(new ListViewItem(definition.FunctionType.ToString(), imageIndex)
                        {
                            Tag = definition.FunctionTypeUInt,
                            Checked = RenderFilters.IsEnabled(definition.FunctionTypeUInt),
                        });
                        imageIndex++;
                    }
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
            if (!IsDivider(e.Item))
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
            //Only raised for rows DrawItem did not hand back to commctrl, i.e. dividers
            e.DrawDefault = !IsDivider(e.Item);
        }

        /* A divider has a checkbox slot like any row: a click there, a double-click or Space would tick it */
        private void FilterList_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.Index >= 0 && e.Index < filterList.Items.Count && IsDivider(filterList.Items[e.Index]))
                e.NewValue = e.CurrentValue;
        }

        /* Arrow keys (or a click) can land on a divider: step over it (and any next to it) the way the selection was going */
        private void FilterList_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected)
                return;
            if (!IsDivider(e.Item))
            {
                _lastSelected = e.ItemIndex;
                return;
            }

            int step = _lastSelected > e.ItemIndex ? -1 : 1;
            int index = NextFilterRow(e.ItemIndex, step);
            if (index < 0)
                index = NextFilterRow(e.ItemIndex, -step);
            if (index < 0)
                return;

            //After the notification, not inside it
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || index >= filterList.Items.Count || IsDivider(filterList.Items[index]))
                    return;
                filterList.Items[index].Selected = true;
                filterList.Items[index].Focused = true;
            }));
        }

        private int NextFilterRow(int from, int step)
        {
            for (int index = from + step; index >= 0 && index < filterList.Items.Count; index += step)
            {
                if (!IsDivider(filterList.Items[index]))
                    return index;
            }
            return -1;
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
            if (_updating || e.Item?.Tag == null || IsDivider(e.Item))
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

            if (!(e.Item.Tag is uint functionType) || RenderFilters.IsEnabled(functionType) == e.Item.Checked)
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

        private void orderButton_Click(object sender, EventArgs e)
        {
            using (Popups.RenderFilterOrderWindow window = new Popups.RenderFilterOrderWindow(RenderFilters.LoadOrder()))
            {
                if (window.ShowDialog((IWin32Window)Singleton.Editor ?? this) != DialogResult.OK)
                    return;
                RenderFilters.SaveOrder(window.Order);
            }
            RefreshFilters();
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
