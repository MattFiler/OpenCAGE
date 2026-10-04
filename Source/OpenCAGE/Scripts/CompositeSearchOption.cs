using System;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Search Only Composite Names (Options > Composite Display). Off by default, so the composite searches - the
    /// Composite Browser, Select Composite and Create Composite Instance Entity - match the whole path, folders and all.
    /// On, they match only each composite's own name, so a search for a folder finds nothing: their Search buttons
    /// say so on hover while it is on.
    /// </summary>
    public static class CompositeSearchOption
    {
        public const string NamesOnlyHint = "Search Only Composite Names is on: only composite names are searched, not the folders they're in.\n"
            + "Turn it off under Options > Composite Display to search folder names too.";

        /// <summary>The option was switched, from the menu, an assistant or another OpenCAGE: a search on show matches again.</summary>
        public static event Action Changed;

        public static bool NamesOnly => SettingsManager.GetBool(Settings.CompNameOnlyOpt);

        /// <summary>The search button's hover text: the hint while the option is on, none while it is off.</summary>
        public static void ApplyHint(ToolTip tip, Control searchButton)
        {
            tip.SetToolTip(searchButton, NamesOnly ? NamesOnlyHint : null);
        }

        public static void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }
}
