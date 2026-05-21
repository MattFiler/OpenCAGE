using CATHODE.Scripting;
using OpenCAGE;
using OpenCAGE.Properties;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace OpenCAGE.UnityConnection
{
    public static class RenderFilters
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
            return false;
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
            foreach (RenderFilterDefinitions.Definition definition in RenderFilterDefinitions.All)
            {
                if (!filters.ContainsKey(definition.FunctionTypeUInt))
                    filters[definition.FunctionTypeUInt] = false;
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

        public static Color ToMenuColor(RenderFilterDefinitions.Definition definition)
        {
            return Color.FromArgb(255, ToByte(definition.R), ToByte(definition.G), ToByte(definition.B));
        }

        public static Image CreateMenuImage(RenderFilterDefinitions.Definition definition, int size = 16)
        {
            Bitmap icon = CreateMenuIcon(definition, size);
            if (icon != null)
                return icon;

            return CreateColorSwatch(ToMenuColor(definition), size);
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

        private static Bitmap CreateMenuIcon(RenderFilterDefinitions.Definition definition, int size)
        {
            Bitmap source = GetMenuIconSource(definition.PreviewKind);
            if (source == null)
                return null;

            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.FromArgb(40, 40, 40));
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, new Rectangle(0, 0, size, size));
            }
            return bitmap;
        }

        private static Bitmap GetMenuIconSource(RenderPreviewKind previewKind)
        {
            switch (previewKind)
            {
                case RenderPreviewKind.Sound:
                    return Resources.RenderFilter_AudioSource;
                case RenderPreviewKind.LightReference:
                    return Resources.RenderFilter_Light;
                case RenderPreviewKind.ParticleEmitter:
                    return Resources.RenderFilter_Particle;
                default:
                    return null;
            }
        }

        private static int ToByte(float value)
        {
            return (int)(value * 255f);
        }
    }
}
