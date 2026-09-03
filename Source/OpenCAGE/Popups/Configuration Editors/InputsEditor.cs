using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace OpenCAGE.ConfigEditors
{
    /// <summary>
    /// DATA/INPUT.XML - the default bindings the game ships with, per input section and per controller
    /// preset, plus the per-device settings that sit above them.
    ///
    /// The file is three sections the engine looks up by name (player_input, menu_input, debug_input),
    /// each holding one or more &lt;device&gt; blocks. A gamepad device can be one of several presets
    /// (preset 2 is the left-handed layout, and so on), which is what the game's controller-layout option
    /// picks between. Inside a device, each binding is a button, axis or slider naming an action and the
    /// id it is bound to; the same action may appear more than once to bind it to several inputs.
    ///
    /// The action names are fixed - the engine asks for them by name - so this edits bindings and adds or
    /// removes bindings for existing actions rather than inventing new ones.
    /// </summary>
    public partial class InputsEditor : BaseWindow
    {
        private const string ConfigFile = "/DATA/INPUT.XML";
        private static readonly string[] BindingKinds = { "button", "axis", "slider" };

        private class DeviceEntry
        {
            public string Display;
            public XmlElement Element;
            public string Type;
        }

        private readonly List<DeviceEntry> _devices = new List<DeviceEntry>();
        private XmlDocument _config;
        private DeviceEntry _selected;
        private bool _populating;
        private bool _loadFailed;

        public InputsEditor() : base()
        {
            InitializeComponent();

            string path = Singleton.PathToAI + ConfigFile;
            try
            {
                _config = new XmlDocument();
                //Keep the file's own indentation and its comments, which document what several actions are for
                _config.PreserveWhitespace = true;
                _config.Load(path);
            }
            catch (Exception e)
            {
                Debug.Log("Inputs", "Failed to read " + path + ": " + e.Message);
                _config = null;
            }

            if (_config?["input"] == null)
            {
                MessageBox.Show("Could not read DATA/INPUT.XML.\n\nUse Options -> Manage Game Directories to point OpenCAGE at a valid install.",
                    "Input config missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _loadFailed = true;
                return;
            }

            ConfigEditorUtils.ShowAutoSaveTipOnce();

            foreach (XmlElement section in _config["input"].ChildNodes.OfType<XmlElement>())
            {
                if (!section.Name.EndsWith("_input"))
                    continue;

                foreach (XmlElement device in section.ChildNodes.OfType<XmlElement>().Where(o => o.Name == "device"))
                {
                    string type = device.GetAttribute("type");
                    string label = SectionLabel(section.Name) + " - " + type;

                    string preset = device.GetAttribute("preset");
                    if (preset.Length != 0)
                        label += ", preset " + preset;
                    string remote = device.GetAttribute("remote_scheme");
                    if (remote.Length != 0)
                        label += ", remote scheme " + remote;

                    _devices.Add(new DeviceEntry() { Display = label, Element = device, Type = type });
                }
            }

            deviceList.BeginUpdate();
            foreach (DeviceEntry device in _devices)
                deviceList.Items.Add(device.Display);
            deviceList.EndUpdate();

            if (deviceList.Items.Count > 0)
                deviceList.SelectedIndex = 0;

            Singleton.OnResetConfigs += OnResetConfigs;
            FormClosed += (s, e) => { Singleton.OnResetConfigs -= OnResetConfigs; };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_loadFailed)
                Close();
        }

        private void OnResetConfigs()
        {
            Close();
        }

        private static string SectionLabel(string name)
        {
            switch (name)
            {
                case "player_input": return "Gameplay";
                case "menu_input": return "Menus";
                case "debug_input": return "Debug";
                default: return name;
            }
        }

        private void deviceList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (deviceList.SelectedIndex < 0 || deviceList.SelectedIndex >= _devices.Count)
                return;

            _selected = _devices[deviceList.SelectedIndex];
            _populating = true;

            bindingGrid.Rows.Clear();
            foreach (XmlElement binding in _selected.Element.ChildNodes.OfType<XmlElement>())
            {
                if (!BindingKinds.Contains(binding.Name))
                    continue;
                int row = bindingGrid.Rows.Add(binding.Name, binding.GetAttribute("action"),
                    binding.GetAttribute("id"), binding.GetAttribute("combo"), binding.GetAttribute("toggle"));
                bindingGrid.Rows[row].Tag = binding;
            }

            //The thresholds for this kind of device, which sit outside the sections and apply to all of them
            settingsGrid.Rows.Clear();
            XmlElement settings = DeviceSettings(_selected.Type);
            settingsGrid.Enabled = settings != null;
            if (settings != null)
            {
                foreach (XmlAttribute attribute in settings.Attributes)
                {
                    if (attribute.Name == "type")
                        continue;
                    int row = settingsGrid.Rows.Add(attribute.Name, attribute.Value);
                    settingsGrid.Rows[row].Tag = settings;
                }
            }
            settingsGroup.Text = settings == null
                ? "No device settings for this device type"
                : "Device settings - every " + _selected.Type + " device";

            _populating = false;
            UpdateButtons();
        }

        private XmlElement DeviceSettings(string type)
        {
            return _config["input"].ChildNodes.OfType<XmlElement>()
                .FirstOrDefault(o => o.Name == "device_settings" && string.Equals(o.GetAttribute("type"), type, StringComparison.OrdinalIgnoreCase));
        }

        private void UpdateButtons()
        {
            bool hasRow = bindingGrid.CurrentRow != null && bindingGrid.CurrentRow.Tag is XmlElement;
            duplicateBinding.Enabled = hasRow;
            removeBinding.Enabled = hasRow;
        }

        private void bindingGrid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        /* Commit a cell as soon as it's typed into, so CellValueChanged runs without waiting for focus to move */
        private void bindingGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (bindingGrid.IsCurrentCellDirty)
                bindingGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void bindingGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_populating || e.RowIndex < 0 || e.RowIndex >= bindingGrid.Rows.Count)
                return;

            DataGridViewRow row = bindingGrid.Rows[e.RowIndex];
            XmlElement binding = row.Tag as XmlElement;
            if (binding == null)
                return;

            //An empty cell means the attribute isn't there at all, which is not the same as id=""
            //(a binding the game ships deliberately unbound), so only id keeps an empty value.
            binding.SetAttribute("id", Cell(row, 2));
            SetOrRemove(binding, "combo", Cell(row, 3));
            SetOrRemove(binding, "toggle", Cell(row, 4));
            Save();
        }

        private static void SetOrRemove(XmlElement element, string attribute, string value)
        {
            if (value.Length == 0)
                element.RemoveAttribute(attribute);
            else
                element.SetAttribute(attribute, value);
        }

        private static string Cell(DataGridViewRow row, int index)
        {
            return row.Cells[index].Value?.ToString().Trim() ?? "";
        }

        private void duplicateBinding_Click(object sender, EventArgs e)
        {
            XmlElement binding = bindingGrid.CurrentRow?.Tag as XmlElement;
            if (binding == null)
                return;

            //A second binding for the same action: the engine accepts several, which is how the menus
            //take both Return and Space for "ok".
            XmlElement copy = _config.CreateElement(binding.Name);
            copy.SetAttribute("action", binding.GetAttribute("action"));
            copy.SetAttribute("id", "");

            //Whitespace is preserved as text nodes, so a bare insert would land the new binding on the end
            //of the previous line. Repeat the indentation in front of the binding being copied.
            XmlNode indent = binding.PreviousSibling;
            binding.ParentNode.InsertAfter(copy, binding);
            if (indent != null && indent.NodeType == XmlNodeType.Whitespace)
                binding.ParentNode.InsertBefore(indent.CloneNode(true), copy);

            _populating = true;
            int row = bindingGrid.Rows.Add(copy.Name, copy.GetAttribute("action"), "", "", "");
            bindingGrid.Rows[row].Tag = copy;
            _populating = false;

            bindingGrid.CurrentCell = bindingGrid.Rows[row].Cells[2];
            UpdateButtons();
            Save();
        }

        private void removeBinding_Click(object sender, EventArgs e)
        {
            DataGridViewRow row = bindingGrid.CurrentRow;
            XmlElement binding = row?.Tag as XmlElement;
            if (binding?.ParentNode == null)
                return;

            //The engine asks for its actions by name: dropping the only binding for one leaves it
            //permanently unbound, where an empty id at least keeps the action in the file.
            bool onlyOne = binding.ParentNode.ChildNodes.OfType<XmlElement>()
                .Count(o => o.Name == binding.Name && o.GetAttribute("action") == binding.GetAttribute("action")) == 1;
            if (onlyOne)
            {
                string message = "'" + binding.GetAttribute("action") + "' has no other binding on this device.\n\n" +
                    "Removing it drops the action from the file entirely. Clearing the Bound to column instead leaves it in place but unbound, which is how the game ships its own unused actions.\n\nRemove it anyway?";
                if (MessageBox.Show(message, "Last binding for this action", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            //Take the indentation in front of it too, or the line it was on is left blank
            if (binding.PreviousSibling != null && binding.PreviousSibling.NodeType == XmlNodeType.Whitespace)
                binding.ParentNode.RemoveChild(binding.PreviousSibling);
            binding.ParentNode.RemoveChild(binding);

            _populating = true;
            bindingGrid.Rows.Remove(row);
            _populating = false;

            UpdateButtons();
            Save();
        }

        private void settingsGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (settingsGrid.IsCurrentCellDirty)
                settingsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void settingsGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_populating || e.RowIndex < 0 || e.RowIndex >= settingsGrid.Rows.Count)
                return;

            DataGridViewRow row = settingsGrid.Rows[e.RowIndex];
            XmlElement settings = row.Tag as XmlElement;
            string name = Cell(row, 0);
            if (settings == null || name.Length == 0)
                return;

            settings.SetAttribute(name, Cell(row, 1));
            Save();
        }

        private void Save()
        {
            string path = Singleton.PathToAI + ConfigFile;
            try
            {
                Modding.ModServices.CaptureBeforeWrite(path);
                _config.Save(path);
                ConfigEditorUtils.NotifyAutoSave(true);
            }
            catch (Exception ex)
            {
                ConfigEditorUtils.NotifyAutoSave(false, ex.Message);
                return;
            }

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }
    }
}
