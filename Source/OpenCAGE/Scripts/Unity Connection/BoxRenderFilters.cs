using CATHODE.Scripting;
using OpenCAGE;
using System.Collections.Generic;
using System.Drawing;

namespace OpenCAGE.UnityConnection
{
    public static class BoxRenderFilters
    {
        public static bool IsEnabled(FunctionType functionType)
        {
            return IsEnabled((uint)functionType);
        }

        public static bool IsEnabled(uint functionType)
        {
            Dictionary<uint, bool> filters = LoadAll();
            if (filters.TryGetValue(functionType, out bool enabled))
                return enabled;
            return true;
        }

        public static void SetEnabled(FunctionType functionType, bool enabled)
        {
            SetEnabled((uint)functionType, enabled);
        }

        public static void SetEnabled(uint functionType, bool enabled)
        {
            Dictionary<uint, bool> filters = LoadAll();
            filters[functionType] = enabled;
            SaveAll(filters);
        }

        public static Dictionary<uint, bool> LoadAll()
        {
            Dictionary<uint, bool> filters = SettingsManager.GetUIntBoolDictionary(Singleton.Settings.UNITY_BoxRenderFilters);
            foreach (BoxRenderFilterDefinitions.Definition definition in BoxRenderFilterDefinitions.All)
            {
                if (!filters.ContainsKey(definition.FunctionTypeUInt))
                    filters[definition.FunctionTypeUInt] = true;
            }
            return filters;
        }

        public static void SaveAll(Dictionary<uint, bool> filters)
        {
            SettingsManager.SetUIntBoolDictionary(Singleton.Settings.UNITY_BoxRenderFilters, filters);
        }

        public static Dictionary<uint, bool> GetPacketFilters()
        {
            return LoadAll();
        }

        public static Color ToMenuColor(BoxRenderFilterDefinitions.Definition definition)
        {
            // Full-strength RGB for the menu swatch; Unity still renders with PreviewAlpha.
            return Color.FromArgb(255, ToByte(definition.R), ToByte(definition.G), ToByte(definition.B));
        }

        public static Bitmap CreateColorSwatch(Color color, int size = 16)
        {
            const int background = 40;
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.FromArgb(background, background, background));
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, color.R, color.G, color.B)))
                    graphics.FillRectangle(brush, 2, 2, size - 4, size - 4);
            }
            return bitmap;
        }

        private static int ToByte(float value)
        {
            return (int)(value * 255f);
        }
    }
}
