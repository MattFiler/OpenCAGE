using CATHODE.Scripting;
using OpenCAGE;
using OpenCAGE.Properties;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

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

        private const int MenuCheckColumnWidth = 14;
        private const int MenuPreviewSize = 16;

        public static Image CreateFilterListIcon(RenderFilterDefinitions.Definition definition)
        {
            Bitmap preview = CreateMenuIcon(definition, 16);
            if (preview == null)
                preview = CreateColorSwatch(ToMenuColor(definition), 16);
            return preview;
        }

        public static Image CreateMenuImage(RenderFilterDefinitions.Definition definition, bool isChecked)
        {
            int width = MenuCheckColumnWidth + MenuPreviewSize + 2;
            Bitmap bitmap = new Bitmap(width, MenuPreviewSize);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                if (isChecked)
                    DrawMenuCheckMark(graphics, new Rectangle(0, 0, MenuCheckColumnWidth, MenuPreviewSize));

                Bitmap preview = CreateMenuIcon(definition, MenuPreviewSize);
                if (preview == null)
                    preview = CreateColorSwatch(ToMenuColor(definition), MenuPreviewSize);

                graphics.DrawImage(preview, MenuCheckColumnWidth + 2, 0, MenuPreviewSize, MenuPreviewSize);
                preview.Dispose();
            }

            return bitmap;
        }

        public static void UpdateMenuImage(ToolStripMenuItem item, uint functionType, bool isChecked)
        {
            if (item == null)
                return;

            if (!RenderFilterDefinitions.TryGetDefinition((FunctionType)functionType, out RenderFilterDefinitions.Definition definition))
                return;

            Image previous = item.Image;
            item.Image = CreateMenuImage(definition, isChecked);
            previous?.Dispose();
        }

        private static void DrawMenuCheckMark(Graphics graphics, Rectangle bounds)
        {
            TextRenderer.DrawText(
                graphics,
                "\u2713",
                SystemFonts.MenuFont,
                bounds,
                SystemColors.MenuText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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
                graphics.Clear(Color.FromArgb(255, 255, 255));
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
                case RenderPreviewKind.SoundObject:
                    return Resources.RenderFilter_SoundObject;
                case RenderPreviewKind.LightReference:
                    return Resources.RenderFilter_Light;
                case RenderPreviewKind.ParticleEmitter:
                    return Resources.RenderFilter_Particle;
                case RenderPreviewKind.CameraResource:
                    return Resources.RenderFilter_Camera;
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
