using CATHODE;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using System.Xml;

namespace OpenCAGE.ConfigEditors
{
    static class ConfigEditorUtils
    {
        public static XmlElement EnsureChildElements(XmlNode parent, params string[] localNames)
        {
            XmlNode current = parent;
            XmlDocument document = parent as XmlDocument ?? parent.OwnerDocument;
            foreach (string name in localNames)
            {
                XmlElement match = null;
                foreach (XmlNode child in current.ChildNodes)
                {
                    if (child is XmlElement el && el.LocalName == name)
                    {
                        match = el;
                        break;
                    }
                }
                if (match == null)
                {
                    match = document.CreateElement(name);
                    current.AppendChild(match);
                }
                current = match;
            }
            return (XmlElement)current;
        }

        /* Widen NumericUpDown range to the full decimal extremes so game/mod values can load/edit without ArgumentOutOfRangeException */
        public static void ExpandNumericRange(NumericUpDown updown)
        {
            if (updown == null)
                return;

            try
            {
                // Expand Maximum first so current Value can't sit above the new Maximum mid-update
                if (updown.Maximum < decimal.MaxValue)
                    updown.Maximum = decimal.MaxValue;
                if (updown.Minimum > decimal.MinValue)
                    updown.Minimum = decimal.MinValue;
            }
            catch
            {
                // Never let range expansion crash the editor
            }
        }

        public static void ExpandNumericRanges(Control.ControlCollection controls)
        {
            if (controls == null)
                return;

            foreach (Control c in controls)
            {
                if (c is NumericUpDown nud)
                    ExpandNumericRange(nud);

                if (c.HasChildren)
                    ExpandNumericRanges(c.Controls);
            }
        }

        /* Safely assign a numeric value, clamping into the control's current Min/Max */
        public static void SetNumericValue(NumericUpDown updown, decimal value)
        {
            if (updown == null)
                return;

            ExpandNumericRange(updown);

            try
            {
                if (value < updown.Minimum)
                    value = updown.Minimum;
                if (value > updown.Maximum)
                    value = updown.Maximum;

                if (updown.Value != value)
                    updown.Value = value;
            }
            catch
            {
                try
                {
                    decimal fallback = updown.Value;
                    if (fallback < updown.Minimum)
                        fallback = updown.Minimum;
                    if (fallback > updown.Maximum)
                        fallback = updown.Maximum;
                    updown.Value = fallback;
                }
                catch
                {
                    // Swallow — keep the previous Value if even clamping fails
                }
            }
        }

        /* Parse text into a NumericUpDown, expanding range and clamping as needed */
        public static void SetNumericFromText(NumericUpDown updown, string text)
        {
            if (updown == null)
                return;

            ExpandNumericRange(updown);

            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value)
                    || decimal.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                {
                    SetNumericValue(updown, value);
                }
            }
            catch
            {
                // Leave existing value if parse/assign fails
            }
        }

        public static void SetCheckbox(List<BML> configs, CheckBox checkbox, params string[] elementPath)
        {
            if (elementPath == null || elementPath.Length == 0)
                return;
            string pathLabel = string.Join("/", elementPath);
            bool foundValue = false;
            for (int i = 0; i < configs.Count; i++)
            {
                XmlElement leaf = TryGetDescendant(configs[i].Content, elementPath);
                if (leaf?.InnerText == null)
                    continue;
                checkbox.Checked = leaf.InnerText.ToUpper() == "TRUE";
                checkbox.Enabled = true;
                foundValue = true;

#if DEBUG
                if (i != 0)
                    Console.WriteLine("Inherited " + pathLabel + " value of " + checkbox.Checked + " from " + configs[i].Filepath);
#endif
                break;
            }

            if (!foundValue)
                checkbox.Enabled = false;
        }

        public static void SetNumber(List<BML> configs, NumericUpDown updown, params string[] elementPath)
        {
            if (elementPath == null || elementPath.Length == 0)
                return;
            string pathLabel = string.Join("/", elementPath);
            bool foundValue = false;
            for (int i = 0; i < configs.Count; i++)
            {
                XmlElement leaf = TryGetDescendant(configs[i].Content, elementPath);
                if (leaf?.InnerText == null)
                    continue;

                try
                {
                    SetNumericFromText(updown, leaf.InnerText);
                    updown.Enabled = true;
                    foundValue = true;
                }
                catch
                {
                    updown.Enabled = false;
                    return;
                }

#if DEBUG
                if (i != 0)
                    Console.WriteLine("Inherited " + pathLabel + " value of " + updown.Value + " from " + configs[i].Filepath);
#endif
                break;
            }

            if (!foundValue)
                updown.Enabled = false;
        }

        public static void SetCombo(List<BML> configs, ComboBox combo, params string[] elementPath)
        {
            if (elementPath == null || elementPath.Length == 0)
                return;
            string pathLabel = string.Join("/", elementPath);
            bool foundValue = false;
            for (int i = 0; i < configs.Count; i++)
            {
                XmlElement leaf = TryGetDescendant(configs[i].Content, elementPath);
                if (leaf?.InnerText == null)
                    continue;
                combo.Text = leaf.InnerText;
                combo.Enabled = true;
                foundValue = true;

#if DEBUG
                if (i != 0)
                    Console.WriteLine("Inherited " + pathLabel + " value of " + combo.Text + " from " + configs[i].Filepath);
#endif
                break;
            }

            if (!foundValue)
                combo.Enabled = false;
        }

        public static void SetText(List<BML> configs, TextBox textbox, params string[] elementPath)
        {
            if (elementPath == null || elementPath.Length == 0)
                return;
            string pathLabel = string.Join("/", elementPath);
            bool foundValue = false;
            for (int i = 0; i < configs.Count; i++)
            {
                XmlElement leaf = TryGetDescendant(configs[i].Content, elementPath);
                if (leaf?.InnerText == null)
                    continue;
                textbox.Text = leaf.InnerText;
                textbox.Enabled = true;
                foundValue = true;

#if DEBUG
                if (i != 0)
                    Console.WriteLine("Inherited " + pathLabel + " value of " + textbox.Text + " from " + configs[i].Filepath);
#endif
                break;
            }

            if (!foundValue)
                textbox.Enabled = false;
        }

        private static XmlElement TryGetDescendant(XmlNode root, params string[] localNames)
        {
            if (localNames == null || localNames.Length == 0)
                return null;
            XmlNode current = root;
            foreach (string name in localNames)
            {
                if (current == null)
                    return null;
                current = current[name];
            }
            return current as XmlElement;
        }

        public static void Subscribe(Control.ControlCollection controls, EventHandler handler)
        {
            foreach (Control c in controls)
            {
                if (c is TextBox tb)
                    tb.TextChanged += handler;
                else if (c is ComboBox cb)
                    cb.SelectedIndexChanged += handler;
                else if (c is CheckBox chk)
                    chk.CheckedChanged += handler;
                else if (c is NumericUpDown nud)
                {
                    ExpandNumericRange(nud);
                    nud.ValueChanged += handler;
                }
                else if (c is TrackBar tbr)
                    tbr.ValueChanged += handler;

                if (c.HasChildren)
                    Subscribe(c.Controls, handler);
            }
        }

        public static void Unsubscribe(Control.ControlCollection controls, EventHandler handler)
        {
            foreach (Control c in controls)
            {
                if (c is TextBox tb)
                    tb.TextChanged -= handler;
                else if (c is ComboBox cb)
                    cb.SelectedIndexChanged -= handler;
                else if (c is CheckBox chk)
                    chk.CheckedChanged -= handler;
                else if (c is NumericUpDown nud)
                    nud.ValueChanged -= handler;
                else if (c is TrackBar tbr)
                    tbr.ValueChanged -= handler;

                if (c.HasChildren)
                    Unsubscribe(c.Controls, handler);
            }
        }
    }
}
