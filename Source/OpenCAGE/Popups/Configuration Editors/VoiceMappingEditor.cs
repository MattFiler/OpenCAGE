using OpenCAGE.ConfigEditors;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace OpenCAGE.Popups.Configuration_Editors
{
    /// <summary>
    /// CHR_INFO/CUSTOMCHARACTERVOICETYPEMAPPINGS.BIN - which voice actor a character is spoken by. Named
    /// .BIN but it is plain XML, and it documents its own format in a readme comment at the top of the file.
    ///
    /// The mappings are a tree of character attributes (CharacterClass, Gender, Ethnicity, Build) with the
    /// permitted voice types at each level, nestable in any order. The engine picks a voice from the
    /// deepest set of attributes matching the character being spawned, so a level only needs voices at the
    /// depth it cares about - but a nested set with no voices at its own level cannot answer for a
    /// character whose deeper attribute is unknown, which is what the "no voices of its own" warning here
    /// is about.
    /// </summary>
    public partial class VoiceMappingEditor : BaseWindow
    {
        private const string ConfigFile = "/DATA/CHR_INFO/CUSTOMCHARACTERVOICETYPEMAPPINGS.BIN";

        //The attribute kinds and values the readme in the file lists
        private static readonly Dictionary<string, string[]> AttributeKinds = new Dictionary<string, string[]>()
        {
            { "CharacterClass", new[] { "PLAYER", "ALIEN", "ANDROID", "ANDROID_HEAVY", "CIVILIAN", "SECURITY", "FACEHUGGER", "INNOCENT", "MOTION_TRACKER", "MELEE_HUMAN" } },
            { "Gender", new[] { "MALE", "FEMALE" } },
            { "Ethnicity", new[] { "AFRICAN", "CAUCASIAN", "ASIAN" } },
            { "Build", new[] { "STANDARD", "HEAVY" } },
        };

        //Voice actor types the readme lists. Anything already in the file is added to these.
        private static readonly string[] KnownVoiceTypes = { "CV1", "CV2", "CV3", "CV4", "CV5", "CV6", "RT1", "RT2", "RT3", "AN1", "AN2", "AN3", "ANH" };

        private const string VoiceElement = "VoiceType";

        private XmlDocument _config;
        private XmlElement _root;
        private bool _loadFailed;

        public VoiceMappingEditor() : base()
        {
            InitializeComponent();

            string path = Singleton.PathToAI + ConfigFile;
            try
            {
                _config = new XmlDocument();
                //Keep the readme comment and the file's indentation
                _config.PreserveWhitespace = true;
                _config.Load(path);
                _root = _config["VoiceTypeMappings"];
            }
            catch (Exception e)
            {
                Debug.Log("Voice Mappings", "Failed to read " + path + ": " + e.Message);
                _root = null;
            }

            if (_root == null)
            {
                MessageBox.Show("Could not read CHR_INFO/CUSTOMCHARACTERVOICETYPEMAPPINGS.BIN.\n\nUse Options -> Manage Game Directories to point OpenCAGE at a valid install.",
                    "Voice mappings missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _loadFailed = true;
                return;
            }

            ConfigEditorUtils.ShowAutoSaveTipOnce();

            attributeKind.Items.AddRange(AttributeKinds.Keys.ToArray());
            attributeKind.SelectedIndex = 0;

            List<string> voiceTypes = new List<string>(KnownVoiceTypes);
            foreach (XmlElement voice in _root.SelectNodes(".//" + VoiceElement).OfType<XmlElement>())
            {
                string value = voice.GetAttribute("value");
                if (value.Length != 0 && !voiceTypes.Contains(value))
                    voiceTypes.Add(value);
            }
            voiceType.Items.AddRange(voiceTypes.ToArray());
            voiceType.SelectedIndex = 0;

            PopulateTree();

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

        private void PopulateTree(XmlElement toSelect = null)
        {
            mappingTree.BeginUpdate();
            mappingTree.Nodes.Clear();
            TreeNode selected = null;
            AddChildren(mappingTree.Nodes, _root, toSelect, ref selected);
            mappingTree.ExpandAll();
            mappingTree.EndUpdate();

            if (selected != null)
                mappingTree.SelectedNode = selected;
            UpdateButtons();
        }

        private static void AddChildren(TreeNodeCollection nodes, XmlElement parent, XmlElement toSelect, ref TreeNode selected)
        {
            foreach (XmlElement child in parent.ChildNodes.OfType<XmlElement>())
            {
                TreeNode node;
                if (child.Name == VoiceElement)
                {
                    node = new TreeNode("Voice: " + child.GetAttribute("value")) { Tag = child };
                }
                else
                {
                    node = new TreeNode(child.Name + ": " + child.GetAttribute("type")) { Tag = child };
                    AddChildren(node.Nodes, child, toSelect, ref selected);

                    //An attribute set that only nests deeper sets cannot answer for a character whose deeper
                    //attribute is unknown - the readme calls this out, so say it where it can be seen
                    if (node.Nodes.Count != 0 && !child.ChildNodes.OfType<XmlElement>().Any(o => o.Name == VoiceElement))
                        node.Text += "   (no voices of its own)";
                }

                nodes.Add(node);
                if (toSelect != null && ReferenceEquals(child, toSelect))
                    selected = node;
            }
        }

        private void mappingTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            XmlElement selected = mappingTree.SelectedNode?.Tag as XmlElement;
            removeSelected.Enabled = selected != null;

            //Adds always land in an attribute set: the selected one, the set a selected voice belongs to,
            //or the top level with nothing selected. The label says which, so neither button needs disabling.
            addingToLabel.Text = "Adding to: " + Describe(AddTarget());
        }

        private string Describe(XmlElement element)
        {
            if (element == null || ReferenceEquals(element, _root))
                return "all characters (top level)";

            List<string> parts = new List<string>();
            XmlElement current = element;
            while (current != null && !ReferenceEquals(current, _root))
            {
                parts.Insert(0, current.Name + " " + current.GetAttribute("type"));
                current = current.ParentNode as XmlElement;
            }
            return string.Join(" / ", parts);
        }

        /* The set the next Add lands in: the selected attribute element, or the root */
        private XmlElement AddTarget()
        {
            XmlElement selected = mappingTree.SelectedNode?.Tag as XmlElement;
            if (selected == null)
                return _root;
            return selected.Name == VoiceElement ? (selected.ParentNode as XmlElement ?? _root) : selected;
        }

        private void attributeKind_SelectedIndexChanged(object sender, EventArgs e)
        {
            attributeType.Items.Clear();
            if (attributeKind.SelectedItem != null && AttributeKinds.TryGetValue(attributeKind.SelectedItem.ToString(), out string[] values))
                attributeType.Items.AddRange(values);
            if (attributeType.Items.Count > 0)
                attributeType.SelectedIndex = 0;
        }

        private void addAttribute_Click(object sender, EventArgs e)
        {
            string kind = attributeKind.SelectedItem?.ToString();
            string type = attributeType.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(kind) || type.Length == 0)
                return;

            XmlElement parent = AddTarget();
            if (parent.ChildNodes.OfType<XmlElement>().Any(o => o.Name == kind && o.GetAttribute("type").ToUpper() == type))
            {
                MessageBox.Show("This set already has a " + kind + " of " + type + ".", "Already here", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            XmlElement element = _config.CreateElement(kind);
            element.SetAttribute("type", type);
            ConfigEditorUtils.AppendIndented(parent, element, "\r\n\t\t");

            PopulateTree(element);
            Save();
        }

        private void addVoice_Click(object sender, EventArgs e)
        {
            string value = voiceType.Text.Trim().ToUpper();
            if (value.Length == 0)
                return;

            XmlElement parent = AddTarget();
            if (parent.ChildNodes.OfType<XmlElement>().Any(o => o.Name == VoiceElement && o.GetAttribute("value").ToUpper() == value))
            {
                MessageBox.Show("This set already uses " + value + ".", "Already here", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            XmlElement element = _config.CreateElement(VoiceElement);
            element.SetAttribute("value", value);
            ConfigEditorUtils.AppendIndented(parent, element, "\r\n\t\t");

            PopulateTree(element);
            Save();
        }

        private void removeSelected_Click(object sender, EventArgs e)
        {
            XmlElement selected = mappingTree.SelectedNode?.Tag as XmlElement;
            if (selected?.ParentNode == null)
                return;

            if (selected.Name != VoiceElement && selected.ChildNodes.OfType<XmlElement>().Any())
            {
                if (MessageBox.Show("Remove " + Describe(selected) + " and everything inside it?", "Remove attribute set",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            //Take the indentation in front of it too, or the line it was on is left blank
            if (selected.PreviousSibling != null && selected.PreviousSibling.NodeType == XmlNodeType.Whitespace)
                selected.ParentNode.RemoveChild(selected.PreviousSibling);
            selected.ParentNode.RemoveChild(selected);
            PopulateTree();
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
