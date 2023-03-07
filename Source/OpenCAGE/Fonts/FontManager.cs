using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAGE
{
    static class FontManager
    {
        static PrivateFontCollection fonts;
        static FontManager()
        {
            fonts = new PrivateFontCollection();
            AddFont(fonts, Properties.Resources.Isolation_Isolation);
            AddFont(fonts, Properties.Resources.JixellationBold_Jixellation);
        }
        static private void AddFont(PrivateFontCollection fontCol, byte[] fontData)
        {
            IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
            Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
            fontCol.AddMemoryFont(fontPtr, fontData.Length);
            Marshal.FreeCoTaskMem(fontPtr);
        }

        static public Font GetFont(int index, int size)
        {
            return new Font(fonts.Families[index], size);
        }
    }
}
