using CATHODE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using static System.Windows.Forms.Form;

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
                updown.Value = Convert.ToDecimal(leaf.InnerText);
                updown.Enabled = true;
                foundValue = true;

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
                    nud.ValueChanged += handler;
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
