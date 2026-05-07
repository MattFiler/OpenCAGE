using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

// Extension class to disable the auto-select behavior when a combobox is in DropDown mode.
// Thank you so much: https://stackoverflow.com/a/25696213/3798962
public static class ComboBoxAutoSelectEx
{

    public static void AutoSelectOff(this ComboBox combo)
    {
        Data.Register(combo);
    }

    public static void AutoSelectOn(this ComboBox combo)
    {
        Data data = null;
        if (Data.dict.TryGetValue(combo, out data))
        {
            data.Dispose();
            Data.dict.Remove(combo);
        }
    }

    private class Data
    {
        // keep a reference to the native windows so they don't get disposed
        internal static Dictionary<ComboBox, Data> dict = new Dictionary<ComboBox, Data>();

        // a ComboBox consists of 3 windows (combobox handle, text edit handle and dropdown list handle)
        ComboBox combo;
        NW nwList = null; // handle to the combobox's dropdown list
        NW2 nwEdit = null; // handle to the edit window

        internal void Dispose()
        {
            dict.Remove(this.combo);
            this.nwList.ReleaseHandle();
            this.nwEdit.ReleaseHandle();
        }

        public static void Register(ComboBox combo)
        {
            if (dict.ContainsKey(combo))
                return; // already registered

            Data data = new Data() { combo = combo };
            Action assign = () => {
                if (dict.ContainsKey(combo))
                    return; // already assigned

                COMBOBOXINFO info = COMBOBOXINFO.GetInfo(combo); // new COMBOBOXINFO();
                                                                 //info.cbSize = Marshal.SizeOf(info);
                                                                 //COMBOBOXINFO2.SendMessageCb(combo.Handle, 0x164, IntPtr.Zero, out info);

                dict[combo] = data;
                data.nwList = new NW(combo, info.hwndList);
                data.nwEdit = new NW2(info.hwndEdit);
            };

            if (!combo.IsHandleCreated)
                combo.HandleCreated += delegate { assign(); };
            else
                assign();

            combo.HandleDestroyed += delegate {
                data.Dispose();
            };
        }
    }

    private class NW : NativeWindow
    {
        ComboBox combo;
        public NW(ComboBox combo, IntPtr handle)
        {
            this.combo = combo;
            AssignHandle(handle);
        }

        private const int LB_FINDSTRING = 0x018F;
        private const int LB_FINDSTRINGEXACT = 0x01A2;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == LB_FINDSTRING)
            {
                m.Msg = LB_FINDSTRINGEXACT;
            }

            base.WndProc(ref m);

            if (m.Msg == LB_FINDSTRINGEXACT)
            {
                String find = Marshal.PtrToStringAuto(m.LParam);
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    Object item = combo.Items[i];
                    if (item.Equals(find))
                    {
                        m.Result = new IntPtr(i);
                        break;
                    }
                }
            }
        }
    }

    private class NW2 : NativeWindow
    {
        public NW2(IntPtr handle)
        {
            AssignHandle(handle);
        }

        private const int EM_SETSEL = 0x00B1;
        private const int EM_GETSEL = 0x00B0;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == EM_SETSEL)
            {
                // if this code is not here, then the entire combobox text is selected
                // which looks ugly, especially when there are multiple combo boxes.
                //
                // if this method returns immediately, then the caret position is set
                // to (0, 0). However, it seems that calling EM_GETSEL has a side effect
                // that the caret position is mostly maintained. Sometimes it slips back
                // to (0, 0).
                SendMessage(Handle, EM_GETSEL, IntPtr.Zero, IntPtr.Zero);
                //int selStart = (sel & 0x00ff);
                //int selEnd = (sel >> 16) & 0x00ff;
                //Debug.WriteLine("EM_GETSEL: " + selStart + " nEnd: " + selEnd);
                return;
            }
            base.WndProc(ref m);
        }

        [DllImportAttribute("user32.dll", SetLastError = true)]
        private static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }

}

[StructLayout(LayoutKind.Sequential)]
public struct COMBOBOXINFO
{
    public Int32 cbSize;
    public RECT rcItem;
    public RECT rcButton;
    public int buttonState;
    public IntPtr hwndCombo;
    public IntPtr hwndEdit;
    public IntPtr hwndList;

    public static COMBOBOXINFO GetInfo(ComboBox combo)
    {
        COMBOBOXINFO info = new COMBOBOXINFO();
        info.cbSize = Marshal.SizeOf(info);
        SendMessageCb(combo.Handle, 0x164, IntPtr.Zero, out info);
        return info;
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageCb(IntPtr hWnd, int msg, IntPtr wp, out COMBOBOXINFO lp);
}

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}