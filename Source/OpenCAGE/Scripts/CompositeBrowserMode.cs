using System;

namespace OpenCAGE
{
    /// <summary>
    /// What the Composite Browser shows under its tree: nothing, the folder browser, or one flat list of
    /// every composite in the level with its preview. One setting rather than the pair it replaced (the
    /// folder browser on or off, and previews in it or not), since neither could say "every composite,
    /// captured, searchable".
    /// </summary>
    public enum CompositeBrowserMode
    {
        TreeOnly,
        TreeAndBrowser,
        TreeAndPreview,
    }

    public static class CompositeBrowserModes
    {
        public const CompositeBrowserMode Default = CompositeBrowserMode.TreeAndPreview;

        /// <summary>The mode a settings value names, or the default for anything it does not.</summary>
        public static CompositeBrowserMode Parse(string value)
        {
            //TryParse takes a number as well as a name, so a stray "7" in the file is checked against the enum too
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out CompositeBrowserMode mode) && Enum.IsDefined(typeof(CompositeBrowserMode), mode))
                return mode;
            return Default;
        }

        public static CompositeBrowserMode Current => Parse(SettingsManager.GetString(Settings.CompositeBrowserMode));

        public static void Set(CompositeBrowserMode mode)
        {
            SettingsManager.SetString(Settings.CompositeBrowserMode, mode.ToString());
        }
    }
}
