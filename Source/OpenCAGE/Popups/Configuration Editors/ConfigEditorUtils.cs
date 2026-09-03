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
        // Original Save handler → wrapped handler (so Unsubscribe still works with the same method group)
        static readonly Dictionary<EventHandler, EventHandler> _wrappedAutoSaveHandlers = new Dictionary<EventHandler, EventHandler>();

        /* Fail MessageBox for auto-saving config & PAK editors (#599) */
        public static void NotifyAutoSave(bool success, string errorDetail = null)
        {
            if (success)
                return;

            string detail = string.IsNullOrWhiteSpace(errorDetail) ? "" : "\n\n" + errorDetail;
            MessageBox.Show(
                "Failed to save changes." + detail,
                "Save failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        public static void ShowAutoSaveTipOnce()
        {
            if (SettingsManager.GetBool(Settings.DidConfigAutoSaveTip))
                return;

            SettingsManager.SetBool(Settings.DidConfigAutoSaveTip, true);
            MessageBox.Show(
                "Changes in Configuration and UI/Animation PAK editors are saved automatically as you edit — you don't need to click Save.",
                "Automatic saving",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        static EventHandler WrapAutoSaveHandler(EventHandler handler)
        {
            if (handler == null)
                return null;

            if (_wrappedAutoSaveHandlers.TryGetValue(handler, out EventHandler existing))
                return existing;

            EventHandler wrapped = (sender, e) =>
            {
                try
                {
                    handler(sender, e);
                    NotifyAutoSave(true);
                }
                catch (Exception ex)
                {
                    NotifyAutoSave(false, ex.Message);
                }
            };
            _wrappedAutoSaveHandlers[handler] = wrapped;
            return wrapped;
        }

        static EventHandler ResolveAutoSaveHandler(EventHandler handler)
        {
            if (handler == null)
                return null;
            if (_wrappedAutoSaveHandlers.TryGetValue(handler, out EventHandler wrapped))
                return wrapped;
            return handler;
        }

        /* The XML configs are loaded with PreserveWhitespace so editing one value doesn't reflow the whole
         * file - which means indentation is data, and a plain AppendChild lands a new element on the end of
         * the previous line. These two copy the indentation the parent's existing children use. */
        private static bool IsWhitespace(XmlNode node)
        {
            return node != null && (node.NodeType == XmlNodeType.Whitespace || node.NodeType == XmlNodeType.SignificantWhitespace);
        }

        /// <summary>The whitespace in front of the parent's first element child, i.e. one entry's indentation.</summary>
        public static string ChildIndent(XmlNode parent, string fallback = null)
        {
            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node.NodeType != XmlNodeType.Element)
                    continue;
                return IsWhitespace(node.PreviousSibling) ? node.PreviousSibling.Value : fallback;
            }
            return fallback;
        }

        /// <summary>Append a child, indented like the children already there, and before the closing tag's own indentation.</summary>
        public static XmlNode AppendIndented(XmlNode parent, XmlNode child, string indentFallback = null)
        {
            XmlDocument document = parent.OwnerDocument;
            string indent = ChildIndent(parent, indentFallback);
            XmlNode trailing = IsWhitespace(parent.LastChild) ? parent.LastChild : null;

            if (trailing != null)
            {
                if (indent != null)
                    parent.InsertBefore(document.CreateWhitespace(indent), trailing);
                parent.InsertBefore(child, trailing);
            }
            else
            {
                if (indent != null)
                    parent.AppendChild(document.CreateWhitespace(indent));
                parent.AppendChild(child);
            }
            return child;
        }

        /// <summary>
        /// Replace every child of <paramref name="parent"/> with <paramref name="children"/>, keeping the
        /// indentation the parent used. Removing elements one by one would leave their whitespace behind.
        /// </summary>
        public static void ReplaceChildrenIndented(XmlNode parent, IEnumerable<XmlNode> children, string indentFallback = null, string closeIndentFallback = null)
        {
            XmlDocument document = parent.OwnerDocument;
            string indent = ChildIndent(parent, indentFallback);
            string closeIndent = IsWhitespace(parent.LastChild) ? parent.LastChild.Value : closeIndentFallback;

            parent.RemoveAll();
            //RemoveAll drops attributes too, so anything the caller needs on the parent is set by the caller afterwards
            foreach (XmlNode child in children)
            {
                if (indent != null)
                    parent.AppendChild(document.CreateWhitespace(indent));
                parent.AppendChild(child);
            }
            if (closeIndent != null)
                parent.AppendChild(document.CreateWhitespace(closeIndent));
        }

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

        /* Safely assign a TrackBar value, clamping into the control's current Min/Max */
        public static void SetTrackBarValue(TrackBar trackBar, int value)
        {
            if (trackBar == null)
                return;

            try
            {
                if (value < trackBar.Minimum)
                    value = trackBar.Minimum;
                if (value > trackBar.Maximum)
                    value = trackBar.Maximum;

                if (trackBar.Value != value)
                    trackBar.Value = value;
            }
            catch
            {
                try
                {
                    int fallback = trackBar.Value;
                    if (fallback < trackBar.Minimum)
                        fallback = trackBar.Minimum;
                    if (fallback > trackBar.Maximum)
                        fallback = trackBar.Maximum;
                    trackBar.Value = fallback;
                }
                catch
                {
                    // Swallow — keep the previous Value if even clamping fails
                }
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
            ShowAutoSaveTipOnce();
            EventHandler wrapped = WrapAutoSaveHandler(handler);

            foreach (Control c in controls)
            {
                if (c is TextBox tb)
                    tb.TextChanged += wrapped;
                else if (c is ComboBox cb)
                    cb.SelectedIndexChanged += wrapped;
                else if (c is CheckBox chk)
                    chk.CheckedChanged += wrapped;
                else if (c is NumericUpDown nud)
                {
                    ExpandNumericRange(nud);
                    nud.ValueChanged += wrapped;
                }
                else if (c is TrackBar tbr)
                    tbr.ValueChanged += wrapped;

                if (c.HasChildren)
                    SubscribeChildren(c.Controls, wrapped);
            }
        }

        // Recurse with already-wrapped handler so we don't re-tip / re-wrap per child collection
        static void SubscribeChildren(Control.ControlCollection controls, EventHandler wrapped)
        {
            foreach (Control c in controls)
            {
                if (c is TextBox tb)
                    tb.TextChanged += wrapped;
                else if (c is ComboBox cb)
                    cb.SelectedIndexChanged += wrapped;
                else if (c is CheckBox chk)
                    chk.CheckedChanged += wrapped;
                else if (c is NumericUpDown nud)
                {
                    ExpandNumericRange(nud);
                    nud.ValueChanged += wrapped;
                }
                else if (c is TrackBar tbr)
                    tbr.ValueChanged += wrapped;

                if (c.HasChildren)
                    SubscribeChildren(c.Controls, wrapped);
            }
        }

        public static void Unsubscribe(Control.ControlCollection controls, EventHandler handler)
        {
            EventHandler wrapped = ResolveAutoSaveHandler(handler);

            foreach (Control c in controls)
            {
                if (c is TextBox tb)
                    tb.TextChanged -= wrapped;
                else if (c is ComboBox cb)
                    cb.SelectedIndexChanged -= wrapped;
                else if (c is CheckBox chk)
                    chk.CheckedChanged -= wrapped;
                else if (c is NumericUpDown nud)
                    nud.ValueChanged -= wrapped;
                else if (c is TrackBar tbr)
                    tbr.ValueChanged -= wrapped;

                if (c.HasChildren)
                    Unsubscribe(c.Controls, handler);
            }
        }
    }
}
