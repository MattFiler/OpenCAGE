using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.Popups.Base;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class EditMaterialMapping : BaseWindow
    {
        public Action<MaterialMappings.MaterialMapping> OnMaterialMappingSelected;

        private MaterialMappings.MaterialMapping _currentMapping;
        private TreeUtility treeHelper;
        private Dictionary<int, MaterialMappings.MaterialMapping> _indexToMapping = new Dictionary<int, MaterialMappings.MaterialMapping>();
        private Dictionary<ListViewItem, MaterialMappings.MaterialMapping.Mapping> _listItemToMapping = new Dictionary<ListViewItem, MaterialMappings.MaterialMapping.Mapping>();

        public EditMaterialMapping(MaterialMappings.MaterialMapping currentMapping = null, bool showSelectBtn = true) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            _currentMapping = currentMapping;

            selectButton.Visible = showSelectBtn;
            treeHelper = new TreeUtility(materialMappingTreeView, TreeType.GENERIC_FOLDER_AND_FILE);

            PopulateTreeView();

            this.Disposed += EditMaterialMapping_Disposed;
        }

        private void EditMaterialMapping_Disposed(object sender, EventArgs e)
        {
            treeHelper?.ForceClearTree();
            treeHelper = null;
            _indexToMapping.Clear();
            _listItemToMapping.Clear();
        }

        private void PopulateTreeView()
        {
            List<string> filePaths = new List<string>();
            List<string> tags = new List<string>();
            _indexToMapping.Clear();

            for (int i = 0; i < Content.Level.MaterialMappings.Entries.Count; i++)
            {
                string filePath = Content.Level.MaterialMappings.Entries[i].Name.Replace('\\', '/');
                if (filePath.Length > 0 && filePath[0] == '/')
                    filePath = filePath.Substring(1);

                filePaths.Add(filePath);
                tags.Add(i.ToString());
                _indexToMapping[i] = Content.Level.MaterialMappings.Entries[i];
            }

            treeHelper.UpdateFileTree(filePaths, null, tags);

            if (_currentMapping != null)
            {
                int index = Content.Level.MaterialMappings.Entries.IndexOf(_currentMapping);
                if (index >= 0 && index < filePaths.Count)
                {
                    treeHelper.SelectNode(filePaths[index]);
                }
            }

            UpdateMappingsPanel();
        }

        private void materialMappingTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdateMappingsPanel();
        }

        private void UpdateMappingsPanel()
        {
            mappingsListView.BeginUpdate();
            mappingsListView.Items.Clear();

            if (materialMappingTreeView.SelectedNode == null)
            {
                mappingsListView.EndUpdate();
                return;
            }

            TreeItem treeItem = (TreeItem)materialMappingTreeView.SelectedNode.Tag;
            if (treeItem.Item_Type != TreeItemType.EXPORTABLE_FILE)
            {
                mappingsListView.EndUpdate();
                return;
            }

            if (!int.TryParse(treeItem.String_Value, out int index) || !_indexToMapping.ContainsKey(index))
            {
                mappingsListView.EndUpdate();
                return;
            }

            MaterialMappings.MaterialMapping selectedMapping = _indexToMapping[index];
            if (selectedMapping == null)
            {
                mappingsListView.EndUpdate();
                return;
            }

            _listItemToMapping.Clear();
            foreach (var mapping in selectedMapping.Mappings)
            {
                ListViewItem item = new ListViewItem(mapping.from ?? "");
                item.SubItems.Add(mapping.to ?? "");
                mappingsListView.Items.Add(item);
                _listItemToMapping[item] = mapping;
            }

            mappingsListView.EndUpdate();
        }

        private void selectButton_Click(object sender, EventArgs e)
        {
            if (materialMappingTreeView.SelectedNode == null)
                return;

            TreeItem treeItem = (TreeItem)materialMappingTreeView.SelectedNode.Tag;
            if (treeItem.Item_Type != TreeItemType.EXPORTABLE_FILE)
                return;

            if (!int.TryParse(treeItem.String_Value, out int index) || !_indexToMapping.ContainsKey(index))
                return;

            MaterialMappings.MaterialMapping selectedMapping = _indexToMapping[index];
            if (selectedMapping == null)
                return;

            OnMaterialMappingSelected?.Invoke(selectedMapping);
            this.Close();
        }

        private void addNewSetButton_Click(object sender, EventArgs e)
        {
            string name = ShowInputDialog("Add New Material Mapping Set", "Name");
            if (string.IsNullOrEmpty(name))
                return;

            if (Content.Level.MaterialMappings.Entries.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("A material mapping set with this name already exists.", "Duplicate Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var newMapping = new MaterialMappings.MaterialMapping
            {
                Name = name,
                Mappings = new List<MaterialMappings.MaterialMapping.Mapping>(),
                ID = MaterialMappings.GenerateMappingID(name, true),
            };

            Content.Level.MaterialMappings.Entries.Add(newMapping);
            CommitMaterialMappingChange(newMapping);
            PopulateTreeView();
            
            int index = Content.Level.MaterialMappings.Entries.IndexOf(newMapping);
            if (index >= 0)
            {
                string filePath = newMapping.Name.Replace('\\', '/');
                if (filePath.Length > 0 && filePath[0] == '/')
                    filePath = filePath.Substring(1);
                treeHelper.SelectNode(filePath);
            }
        }

        private void addMappingButton_Click(object sender, EventArgs e)
        {
            if (materialMappingTreeView.SelectedNode == null)
            {
                MessageBox.Show("Please select a material mapping set first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TreeItem treeItem = (TreeItem)materialMappingTreeView.SelectedNode.Tag;
            if (treeItem.Item_Type != TreeItemType.EXPORTABLE_FILE)
            {
                MessageBox.Show("Please select a material mapping set first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!int.TryParse(treeItem.String_Value, out int index) || !_indexToMapping.ContainsKey(index))
                return;

            MaterialMappings.MaterialMapping selectedMapping = _indexToMapping[index];
            if (selectedMapping == null)
                return;

            var result = ShowMaterialPairDialog("Add Mapping");
            if (result == null)
                return;

            var newMapping = new MaterialMappings.MaterialMapping.Mapping
            {
                from = result.Item1,
                to = result.Item2
            };

            selectedMapping.Mappings.Add(newMapping);
            CommitMaterialMappingChange(selectedMapping);
            UpdateMappingsPanel();
        }

        private void removeMappingButton_Click(object sender, EventArgs e)
        {
            if (mappingsListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a mapping to remove.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (materialMappingTreeView.SelectedNode == null)
                return;

            TreeItem treeItem = (TreeItem)materialMappingTreeView.SelectedNode.Tag;
            if (treeItem.Item_Type != TreeItemType.EXPORTABLE_FILE)
                return;

            if (!int.TryParse(treeItem.String_Value, out int index) || !_indexToMapping.ContainsKey(index))
                return;

            MaterialMappings.MaterialMapping selectedMapping = _indexToMapping[index];
            if (selectedMapping == null)
                return;

            ListViewItem selectedItem = mappingsListView.SelectedItems[0];
            if (_listItemToMapping.ContainsKey(selectedItem))
            {
                var mapping = _listItemToMapping[selectedItem];
                selectedMapping.Mappings.Remove(mapping);
                CommitMaterialMappingChange(selectedMapping);
                UpdateMappingsPanel();
            }
        }

        private void mappingsListView_DoubleClick(object sender, EventArgs e)
        {
            if (mappingsListView.SelectedItems.Count == 0)
                return;

            if (materialMappingTreeView.SelectedNode == null)
                return;

            TreeItem treeItem = (TreeItem)materialMappingTreeView.SelectedNode.Tag;
            if (treeItem.Item_Type != TreeItemType.EXPORTABLE_FILE)
                return;

            if (!int.TryParse(treeItem.String_Value, out int index) || !_indexToMapping.ContainsKey(index))
                return;

            MaterialMappings.MaterialMapping selectedMapping = _indexToMapping[index];
            if (selectedMapping == null)
                return;

            ListViewItem selectedItem = mappingsListView.SelectedItems[0];
            if (!_listItemToMapping.ContainsKey(selectedItem))
                return;

            var mapping = _listItemToMapping[selectedItem];

            var result = ShowMaterialPairDialog("Edit Mapping", mapping.from ?? "", mapping.to ?? "");
            if (result == null)
                return;

            mapping.from = result.Item1;
            mapping.to = result.Item2;
            CommitMaterialMappingChange(selectedMapping);
            UpdateMappingsPanel();
        }

        private void mappingsListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && mappingsListView.SelectedItems.Count > 0)
            {
                removeMappingButton_Click(sender, e);
            }
        }

        private void CommitMaterialMappingChange(MaterialMappings.MaterialMapping mappingSet)
        {
            if (mappingSet == null)
                return;

            Singleton.OnResourceModified?.Invoke();
            Send.NotifyMaterialMappingModified(mappingSet);
        }

        private bool TryGetSelectedMappingSet(out MaterialMappings.MaterialMapping selectedMapping, bool notifyIfMissing = false)
        {
            selectedMapping = null;
            if (materialMappingTreeView.SelectedNode == null)
            {
                if (notifyIfMissing)
                    MessageBox.Show("Please select a material mapping set first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            TreeItem treeItem = (TreeItem)materialMappingTreeView.SelectedNode.Tag;
            if (treeItem.Item_Type != TreeItemType.EXPORTABLE_FILE)
            {
                if (notifyIfMissing)
                    MessageBox.Show("Please select a material mapping set first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!int.TryParse(treeItem.String_Value, out int index) || !_indexToMapping.TryGetValue(index, out selectedMapping) || selectedMapping == null)
            {
                if (notifyIfMissing)
                    MessageBox.Show("Please select a material mapping set first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        private void selectFromMaterialButton_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedMapping(out var mapping, notifyIfMissing: true))
                return;

            ApplyPickedMaterialToMappingSide(mapping, fromSide: true);
        }

        private void selectToMaterialButton_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedMapping(out var mapping, notifyIfMissing: true))
                return;

            ApplyPickedMaterialToMappingSide(mapping, fromSide: false);
        }

        /// <summary>Opens Material Editor focused on the current side; confirming applies the picked material.</summary>
        private void ApplyPickedMaterialToMappingSide(MaterialMappings.MaterialMapping.Mapping mapping, bool fromSide)
        {
            string currentName = fromSide ? mapping.from : mapping.to;
            Materials.Material initial = ResolveMaterialFromLevel(currentName);
            Materials.Material picked = ShowMaterialPickerDialog(this, initial);
            if (picked == null || string.IsNullOrEmpty(picked.Name))
                return;

            if (fromSide)
                mapping.from = picked.Name;
            else
                mapping.to = picked.Name;

            if (!TryGetSelectedMappingSet(out MaterialMappings.MaterialMapping selectedMapping))
                return;

            CommitMaterialMappingChange(selectedMapping);
            UpdateMappingsPanel();
            ReselectMappingRow(mapping);
        }

        private void ReselectMappingRow(MaterialMappings.MaterialMapping.Mapping mapping)
        {
            foreach (ListViewItem item in mappingsListView.Items)
            {
                if (_listItemToMapping.TryGetValue(item, out var m) && ReferenceEquals(m, mapping))
                {
                    item.Focused = true;
                    item.Selected = true;
                    item.EnsureVisible();
                    mappingsListView.Focus();
                    break;
                }
            }
        }

        private bool TryGetSelectedMapping(out MaterialMappings.MaterialMapping.Mapping mapping, bool notifyIfMissing = false)
        {
            mapping = null;
            if (mappingsListView.SelectedItems.Count == 0)
            {
                if (notifyIfMissing)
                    MessageBox.Show("Please select a mapping row first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            ListViewItem selectedItem = mappingsListView.SelectedItems[0];
            if (!_listItemToMapping.TryGetValue(selectedItem, out mapping) || mapping == null)
            {
                if (notifyIfMissing)
                    MessageBox.Show("Please select a mapping row first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        private Materials.Material ResolveMaterialFromLevel(string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName) || Content?.Level?.Materials?.Entries == null)
                return null;

            string trimmed = materialName.Trim();
            return Content.Level.Materials.Entries.FirstOrDefault(m =>
                m?.Name != null && m.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        private Materials.Material ShowMaterialPickerDialog(IWin32Window owner, Materials.Material initialMaterial = null)
        {
            Materials.Material chosen = null;
            using (var editor = new EditMaterial(initialMaterial, showSelectBtn: true))
            {
                void OnPick(Materials.Material m) { chosen = m; }
                editor.OnMaterialSelected += OnPick;
                editor.ShowDialog(owner);
                editor.OnMaterialSelected -= OnPick;
            }

            return chosen;
        }

        private string ShowInputDialog(string title, string labelText, string initialValue = "")
        {
            using (var dialog = new Form())
            {
                dialog.Text = title;
                dialog.Size = new System.Drawing.Size(400, 120);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                var label = new Label() { Text = labelText + ":", Left = 10, Top = 15, Width = 70 };
                var textBox = new TextBox() { Left = 90, Top = 12, Width = 280, Text = initialValue };
                var okButton = new Button() { Text = "OK", Left = 220, Top = 45, Width = 70, DialogResult = DialogResult.OK };
                var cancelButton = new Button() { Text = "Cancel", Left = 300, Top = 45, Width = 70, DialogResult = DialogResult.Cancel };

                dialog.Controls.Add(label);
                dialog.Controls.Add(textBox);
                dialog.Controls.Add(okButton);
                dialog.Controls.Add(cancelButton);
                dialog.AcceptButton = okButton;
                dialog.CancelButton = cancelButton;

                textBox.Select();
                textBox.SelectAll();

                if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(textBox.Text))
                {
                    return textBox.Text.Trim();
                }
            }
            return null;
        }

        /// <summary>Dialogs for entering From/To with optional Browse (Material Editor picker).</summary>
        private Tuple<string, string> ShowMaterialPairDialog(string title, string initialFrom = "", string initialTo = "")
        {
            using (var dialog = new Form())
            {
                dialog.Text = title;
                dialog.ClientSize = new Size(512, 120);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                int row1 = 12;
                int row2 = 44;
                int rowButtons = 80;

                var labelFrom = new Label { Text = "From:", Left = 12, Top = row1 + 3, Width = 44 };
                var textBoxFrom = new TextBox { Left = 62, Top = row1 - 1, Width = 318, Text = initialFrom ?? "" };
                var browseFrom = new Button { Text = "Browse...", Left = 386, Top = row1 - 2, Width = 94 };
                browseFrom.Click += (_, __) =>
                {
                    var picked = ShowMaterialPickerDialog(dialog, ResolveMaterialFromLevel(textBoxFrom.Text));
                    if (picked != null && !string.IsNullOrEmpty(picked.Name))
                        textBoxFrom.Text = picked.Name;
                };

                var labelTo = new Label { Text = "To:", Left = 12, Top = row2 + 3, Width = 44 };
                var textBoxTo = new TextBox { Left = 62, Top = row2 - 1, Width = 318, Text = initialTo ?? "" };
                var browseTo = new Button { Text = "Browse...", Left = 386, Top = row2 - 2, Width = 94 };
                browseTo.Click += (_, __) =>
                {
                    var picked = ShowMaterialPickerDialog(dialog, ResolveMaterialFromLevel(textBoxTo.Text));
                    if (picked != null && !string.IsNullOrEmpty(picked.Name))
                        textBoxTo.Text = picked.Name;
                };

                var okButton = new Button { Text = "OK", Left = 334, Top = rowButtons, Width = 74, DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = "Cancel", Left = 414, Top = rowButtons, Width = 74, DialogResult = DialogResult.Cancel };

                dialog.Controls.Add(labelFrom);
                dialog.Controls.Add(textBoxFrom);
                dialog.Controls.Add(browseFrom);
                dialog.Controls.Add(labelTo);
                dialog.Controls.Add(textBoxTo);
                dialog.Controls.Add(browseTo);
                dialog.Controls.Add(okButton);
                dialog.Controls.Add(cancelButton);

                dialog.AcceptButton = okButton;
                dialog.CancelButton = cancelButton;

                dialog.Shown += (_, __) => { textBoxFrom.Focus(); };

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    return new Tuple<string, string>(textBoxFrom.Text.Trim(), textBoxTo.Text.Trim());
                }
            }

            return null;
        }
    }
}

