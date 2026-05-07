using AlienPAK;
using CATHODE;
using CathodeLib.ObjectExtensions;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class EditTexture : BaseWindow
    {
        public Action<Textures.TEX4> OnTextureSelected;

        TreeUtility _treeHelper;
        Textures _activeTextures;
        Textures.TEX4 _selectedTexture;
        bool _suppressSourceChange = true;
        bool _suppressSearchChanged;
        bool _suppressFlagChange;
        readonly List<(Textures.TextureStateFlag flag, CheckBox cb)> _stateFlagChecks = new List<(Textures.TextureStateFlag, CheckBox)>();
        readonly List<(Textures.TextureUsageFlag flag, CheckBox cb)> _usageFlagChecks = new List<(Textures.TextureUsageFlag, CheckBox)>();

        public EditTexture(Textures.TEX4 currentMapping = null, bool showSelectBtn = true, int initialTextureSourceIndex = 0) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            PopulateTextureFlagCheckboxes();

            _treeHelper = new TreeUtility(FileTree, TreeType.GENERIC_FOLDER_AND_FILE);

            textureSourceCombo.Items.Clear();
            textureSourceCombo.Items.Add(Content.Level.Name);
            textureSourceCombo.Items.Add("GLOBAL");

            _suppressSourceChange = true;
            int sourceIdx = initialTextureSourceIndex <= 0 ? 0 : 1;
            if (Singleton.Global?.Textures == null)
                sourceIdx = 0;
            textureSourceCombo.SelectedIndex = sourceIdx;

            _activeTextures = textureSourceCombo.SelectedIndex == 0 ? Content.Level.Textures : Singleton.Global.Textures;
            RebuildTextureTree();
            _suppressSourceChange = false;

            if (currentMapping != null && !string.IsNullOrEmpty(currentMapping.Name))
                _treeHelper.SelectNode(currentMapping.Name);

            selectTextureBtn.Visible = showSelectBtn;
            FileTree.ImageList = imageList1;
            UpdateTextureToolsState();
        }

        private void textureSourceCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressSourceChange)
                return;

            _activeTextures = textureSourceCombo.SelectedIndex == 0
                ? Content.Level.Textures
                : Singleton.Global.Textures;

            RebuildTextureTree();
            FileTree.SelectedNode = null;
            ClearPreview();
            UpdateTextureToolsState();
        }

        private void textureSearchBox_TextChanged(object sender, EventArgs e)
        {
            if (_suppressSearchChanged)
                return;
            RebuildTextureTree();
        }

        private List<string> GetAllTextureNames()
        {
            List<string> textureNames = new List<string>();
            if (_activeTextures?.Entries != null)
            {
                for (int i = 0; i < _activeTextures.Entries.Count; i++)
                    textureNames.Add(_activeTextures.Entries[i].Name);
            }
            return textureNames;
        }

        private void RebuildTextureTree()
        {
            List<string> names = GetAllTextureNames();
            string q = textureSearchBox?.Text?.Trim() ?? "";
            if (q.Length > 0)
            {
                names = names
                    .Where(n => n.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }
            _treeHelper.UpdateFileTree(names, null);
        }

        private void RefreshTexturePreviewFromSelection()
        {
            if (FileTree.SelectedNode == null)
            {
                ClearPreview();
                UpdateTextureToolsState();
                return;
            }
            FileTree_AfterSelect(FileTree, new TreeViewEventArgs(FileTree.SelectedNode));
        }

        private static void AssignPreviewImage(PictureBox box, Bitmap bmp)
        {
            Image old = box.BackgroundImage;
            box.BackgroundImage = bmp;
            old?.Dispose();
        }

        private void ClearPreview()
        {
            AssignPreviewImage(pictureStreamed, null);
            AssignPreviewImage(picturePersistent, null);
            texturePreviewArea.Text = "";
            streamedMetaText.Text = "";
            persistentMetaText.Text = "";
            tabStreamed.Enabled = true;
            tabPersistent.Enabled = true;
            selectTextureBtn.Enabled = false;
            _selectedTexture = null;
            ResetFlagCheckboxes();
        }

        private static string GetTex4Desc(Textures.TEX4.Texture part)
        {
            if (part == null || part.Content == null || part.Content.Length == 0)
                return "(none)";
            return "Width: " + part.Width + "\r\n" +
                "Height: " + part.Height + "\r\n" +
                "Depth: " + part.Depth + "\r\n" +
                "Mips: " + part.MipLevels + "\r\n" +
                "Size: " + part.Content.Length + " bytes";
        }

        private static bool HasContent(Textures.TEX4.Texture part)
        {
            return part?.Content != null && part.Content.Length > 0;
        }

        private Textures.TEX4 FindTexture(string nodeVal)
        {
            if (_activeTextures?.Entries == null || string.IsNullOrEmpty(nodeVal))
                return null;
            string norm = nodeVal.Replace('\\', '/');
            return _activeTextures.Entries.FirstOrDefault(o => o.Name.Replace('\\', '/') == norm);
        }

        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            ClearPreview();

            if (FileTree.SelectedNode == null)
            {
                UpdateTextureToolsState();
                return;
            }

            TreeItemType nodeType = ((TreeItem)FileTree.SelectedNode.Tag).Item_Type;
            string nodeVal = ((TreeItem)FileTree.SelectedNode.Tag).String_Value;
            switch (nodeType)
            {
                case TreeItemType.EXPORTABLE_FILE:
                    Textures.TEX4 texture = FindTexture(nodeVal);
                    if (texture == null)
                        break;

                    texturePreviewArea.Text = texture.Name + " [" + texture.Format.ToString() + "]";

                    streamedMetaText.Text = GetTex4Desc(texture.TextureStreamed);
                    persistentMetaText.Text = GetTex4Desc(texture.TexturePersistent);

                    bool hasStreamed = HasContent(texture.TextureStreamed);
                    bool hasPersistent = HasContent(texture.TexturePersistent);
                    tabStreamed.Enabled = hasStreamed;
                    tabPersistent.Enabled = hasPersistent;

                    if (hasStreamed)
                        previewTabControl.SelectedTab = tabStreamed;
                    else if (hasPersistent)
                        previewTabControl.SelectedTab = tabPersistent;

                    selectTextureBtn.Enabled = true;
                    _selectedTexture = texture;
                    ApplyFlagsUiFromTexture(texture);
                    RefreshTexturePreviewsForSelected();
                    break;
            }

            UpdateTextureToolsState();
        }

        private void PopulateTextureFlagCheckboxes()
        {
            if (_stateFlagChecks.Count > 0)
                return;

            foreach (string name in Enum.GetNames(typeof(Textures.TextureStateFlag)))
            {
                var flag = (Textures.TextureStateFlag)Enum.Parse(typeof(Textures.TextureStateFlag), name);
                if (!IsPowerOfTwoEnumMember(flag))
                    continue;
                CheckBox cb = CreateFlagCheckBox(name);
                cb.CheckedChanged += TextureStateFlag_CheckedChanged;
                stateFlagsPanel.Controls.Add(cb);
                _stateFlagChecks.Add((flag, cb));
            }

            foreach (string name in Enum.GetNames(typeof(Textures.TextureUsageFlag)))
            {
                var flag = (Textures.TextureUsageFlag)Enum.Parse(typeof(Textures.TextureUsageFlag), name);
                if (!IsPowerOfTwoEnumMember(flag))
                    continue;
                CheckBox cb = CreateFlagCheckBox(name);
                cb.CheckedChanged += TextureUsageFlag_CheckedChanged;
                usageFlagsPanel.Controls.Add(cb);
                _usageFlagChecks.Add((flag, cb));
            }

            SetFlagCheckboxesEnabled(false);
        }

        private static CheckBox CreateFlagCheckBox(string enumMemberName)
        {
            return new CheckBox
            {
                Text = enumMemberName.Replace("_", " "),
                AutoSize = true,
                Margin = new Padding(0, 0, 8, 2),
                Enabled = false
            };
        }

        private static bool IsPowerOfTwoEnumMember(Enum value)
        {
            ulong u = Convert.ToUInt64(value);
            if (u == 0)
                return false;
            return (u & (u - 1)) == 0;
        }

        private void ApplyFlagsUiFromTexture(Textures.TEX4 texture)
        {
            _suppressFlagChange = true;
            foreach (var pair in _stateFlagChecks)
                pair.cb.Checked = texture.StateFlags.HasFlag(pair.flag);
            foreach (var pair in _usageFlagChecks)
                pair.cb.Checked = texture.UsageFlags.HasFlag(pair.flag);
            SetFlagCheckboxesEnabled(true);
            _suppressFlagChange = false;
        }

        private void ResetFlagCheckboxes()
        {
            _suppressFlagChange = true;
            foreach (var pair in _stateFlagChecks)
            {
                pair.cb.Checked = false;
                pair.cb.Enabled = false;
            }
            foreach (var pair in _usageFlagChecks)
            {
                pair.cb.Checked = false;
                pair.cb.Enabled = false;
            }
            _suppressFlagChange = false;
        }

        private void SetFlagCheckboxesEnabled(bool enabled)
        {
            foreach (var pair in _stateFlagChecks)
                pair.cb.Enabled = enabled;
            foreach (var pair in _usageFlagChecks)
                pair.cb.Enabled = enabled;
        }

        private void TextureStateFlag_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressFlagChange || _selectedTexture == null || IsGlobalSourceSelected())
                return;
            Textures.TextureStateFlag combined = 0;
            foreach (var pair in _stateFlagChecks)
            {
                if (pair.cb.Checked)
                    combined |= pair.flag;
            }
            _selectedTexture.StateFlags = combined;
            Singleton.OnResourceModified?.Invoke();
            RefreshTexturePreviewsForSelected();
        }

        private void TextureUsageFlag_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressFlagChange || _selectedTexture == null || IsGlobalSourceSelected())
                return;
            Textures.TextureUsageFlag combined = 0;
            foreach (var pair in _usageFlagChecks)
            {
                if (pair.cb.Checked)
                    combined |= pair.flag;
            }
            _selectedTexture.UsageFlags = combined;
            Singleton.OnResourceModified?.Invoke();
            RefreshTexturePreviewsForSelected();
        }

        private void RefreshTexturePreviewsForSelected()
        {
            if (_selectedTexture == null)
                return;
            Textures.TEX4 texture = _selectedTexture;
            bool hasStreamed = HasContent(texture.TextureStreamed);
            bool hasPersistent = HasContent(texture.TexturePersistent);
            try
            {
                if (hasStreamed)
                    AssignPreviewImage(pictureStreamed, texture.ToBitmap(texture.TextureStreamed));
                else
                    AssignPreviewImage(pictureStreamed, null);
                if (hasPersistent)
                    AssignPreviewImage(picturePersistent, texture.ToBitmap(texture.TexturePersistent));
                else
                    AssignPreviewImage(picturePersistent, null);
            }
            catch
            {
                AssignPreviewImage(pictureStreamed, null);
                AssignPreviewImage(picturePersistent, null);
            }
        }

        private bool IsGlobalSourceSelected()
        {
            return textureSourceCombo.SelectedIndex == 1;
        }

        private void UpdateTextureToolsState()
        {
            bool file = FileTree.SelectedNode != null && ((TreeItem)FileTree.SelectedNode.Tag).Item_Type == TreeItemType.EXPORTABLE_FILE;
            bool canEditTextures = _activeTextures != null && !IsGlobalSourceSelected();
            replaceTextureBtn.Enabled = file && canEditTextures;
            deleteTextureBtn.Enabled = file && canEditTextures;
            exportTextureBtn.Enabled = file;
            importTextureBtn.Enabled = canEditTextures;
            exportAllTexturesBtn.Enabled = _activeTextures.Entries != null && _activeTextures.Entries.Count > 0;
            SetFlagCheckboxesEnabled(file && canEditTextures);
        }

        private void importTextureBtn_Click(object sender, EventArgs e)
        {
            if (_activeTextures == null || IsGlobalSourceSelected())
                return;

            using (OpenFileDialog picker = new OpenFileDialog())
            {
                picker.Filter = "DDS Files|*.dds";
                if (picker.ShowDialog() != DialogResult.OK)
                    return;

                string texName = Path.GetFileName(picker.FileName);
                string norm = texName.Replace('\\', '/');
                if (_activeTextures.Entries.Any(o => o.Name.Replace('\\', '/') == norm))
                {
                    MessageBox.Show("A texture with this name already exists!", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Cursor = Cursors.WaitCursor;
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(picker.FileName);
                    Textures.TEX4 texture = new Textures.TEX4 { Name = texName };
                    Textures.TEX4.Texture part = fileBytes.ToTEX4Part(out texture.Format, out texture.StateFlags, out texture.UsageFlags);
                    if (part == null)
                    {
                        MessageBox.Show("Please select a DX10 DDS image.\nIf you converted this DDS yourself, try the Nvidia Texture Tools Exporter.", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    texture.TextureStreamed = part.Copy();
                    texture.TexturePersistent = part.Copy();
                    _activeTextures.Entries.Add(texture);
                    Singleton.OnResourceModified?.Invoke();
                    _suppressSearchChanged = true;
                    textureSearchBox.Text = "";
                    _suppressSearchChanged = false;
                    RebuildTextureTree();
                    _treeHelper.SelectNode(texture.Name.Replace('/', '\\'));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            }
        }

        private void replaceTextureBtn_Click(object sender, EventArgs e)
        {
            if (_activeTextures == null || IsGlobalSourceSelected() || FileTree.SelectedNode == null)
                return;
            if (((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE)
                return;

            string nodeVal = ((TreeItem)FileTree.SelectedNode.Tag).String_Value;
            Textures.TEX4 texture = FindTexture(nodeVal);
            if (texture == null)
                return;

            using (OpenFileDialog picker = new OpenFileDialog())
            {
                picker.Filter = "DDS Files|*.dds";
                if (picker.ShowDialog() != DialogResult.OK)
                    return;

                Cursor = Cursors.WaitCursor;
                try
                {
                    byte[] content = File.ReadAllBytes(picker.FileName);
                    Textures.TEX4.Texture part = content.ToTEX4Part(out texture.Format, out texture.StateFlags, out texture.UsageFlags);
                    if (part == null)
                    {
                        MessageBox.Show("Please select a DX10 DDS image.\nIf you converted this DDS yourself, try the Nvidia Texture Tools Exporter.", "Replace failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    texture.TextureStreamed = part.Copy();
                    texture.TexturePersistent = part.Copy();
                    Singleton.OnResourceModified?.Invoke();
                    RefreshTexturePreviewFromSelection();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Replace failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            }
        }

        private void deleteTextureBtn_Click(object sender, EventArgs e)
        {
            if (_activeTextures == null || IsGlobalSourceSelected() || FileTree.SelectedNode == null)
                return;
            if (((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE)
                return;

            string nodeVal = ((TreeItem)FileTree.SelectedNode.Tag).String_Value;
            if (MessageBox.Show("Remove '" + nodeVal + "'?", "Delete texture", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            Cursor = Cursors.WaitCursor;
            try
            {
                string norm = nodeVal.Replace('\\', '/');
                _activeTextures.Entries.RemoveAll(o => o.Name.Replace('\\', '/') == norm);
                Singleton.OnResourceModified?.Invoke();
                FileTree.SelectedNode = null;
                ClearPreview();
                RebuildTextureTree();
                UpdateTextureToolsState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Delete failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void exportTextureBtn_Click(object sender, EventArgs e)
        {
            if (FileTree.SelectedNode == null)
                return;
            try
            {
                ExportTextureNode(FileTree.SelectedNode, "");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to export!\n" + ex.Message, "Failed export!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void exportAllTexturesBtn_Click(object sender, EventArgs e)
        {
            if (_activeTextures?.Entries == null || _activeTextures.Entries.Count == 0)
                return;

            using (FolderBrowserDialog folder = new FolderBrowserDialog())
            {
                folder.Description = "Select folder for exported textures";
                folder.ShowNewFolderButton = true;
                if (folder.ShowDialog() != DialogResult.OK)
                    return;

                string ext = PromptBulkExportExtension();
                if (string.IsNullOrEmpty(ext))
                    return;

                Cursor = Cursors.WaitCursor;
                int errors = 0;
                foreach (TreeNode node in FileTree.Nodes)
                {
                    try
                    {
                        ExportTextureNodeRecursive(node, folder.SelectedPath, ext);
                    }
                    catch (Exception ex)
                    {
                        errors++;
                    }
                }
#if DEBUG
                if (errors > 0)
                    MessageBox.Show("Encountered " + errors + " errors!");
#endif
                Process.Start(folder.SelectedPath);
                MessageBox.Show("Export complete.", "Textures", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Cursor = Cursors.Default;
            }
        }

        private static string PromptBulkExportExtension()
        {
            using (Form f = new Form
            {
                Text = "Export all as",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(280, 88)
            })
            {
                ComboBox cb = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(12, 12),
                    Width = 256
                };
                cb.Items.Add("DDS (*.dds)");
                cb.Items.Add("PNG (*.png)");
                cb.Items.Add("JPG (*.jpg)");
                cb.SelectedIndex = 0;
                Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(100, 48), Width = 80 };
                Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(188, 48), Width = 80 };
                f.Controls.Add(cb);
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                if (f.ShowDialog() != DialogResult.OK)
                    return null;
                switch (cb.SelectedIndex)
                {
                    case 1: return ".png";
                    case 2: return ".jpg";
                    default: return ".dds";
                }
            }
        }

        private void ExportTextureNodeRecursive(TreeNode node, string outputFolder, string ext)
        {
            ExportTextureNode(node, outputFolder, ext);
            foreach (TreeNode child in node.Nodes)
                ExportTextureNodeRecursive(child, outputFolder, ext);
        }

        private void ExportTextureNode(TreeNode node, string outputFolder)
        {
            ExportTextureNode(node, outputFolder, null);
        }

        private void ExportTextureNode(TreeNode node, string outputFolder, string bulkExtension)
        {
            if (node == null)
                return;
            TreeItemType nodeType = ((TreeItem)node.Tag).Item_Type;
            string nodeVal = ((TreeItem)node.Tag).String_Value;
            if (nodeType != TreeItemType.EXPORTABLE_FILE)
                return;

            Textures.TEX4 texture = FindTexture(nodeVal);
            if (texture == null)
                return;

            string pickedFileName;
            if (string.IsNullOrEmpty(outputFolder))
            {
                string fileStem = Path.GetFileName(nodeVal);
                while (!string.IsNullOrEmpty(Path.GetExtension(fileStem)))
                    fileStem = Path.GetFileNameWithoutExtension(fileStem);

                SaveFileDialog picker = new SaveFileDialog();
                picker.Filter = "DDS|*.dds|PNG|*.png|JPG|*.jpg";
                picker.FileName = fileStem;
                if (picker.ShowDialog() != DialogResult.OK)
                    return;
                pickedFileName = picker.FileName;
            }
            else
            {
                string rel = nodeVal.Replace('\\', '/');
                string subDir = Path.GetDirectoryName(rel);
                string baseName = Path.GetFileNameWithoutExtension(Path.GetFileName(rel));
                if (string.IsNullOrEmpty(baseName))
                    baseName = Path.GetFileName(rel);
                string folder = string.IsNullOrEmpty(subDir)
                    ? outputFolder
                    : Path.Combine(outputFolder, subDir.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(folder);
                pickedFileName = Path.Combine(folder, baseName + bulkExtension);
            }

            byte[] dds = texture.ToDDS();
            string ext = Path.GetExtension(pickedFileName);
            if (string.Equals(ext, ".dds", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllBytes(pickedFileName, dds);
            }
            else
            {
                using (Bitmap bmp = dds.ToBitmap())
                {
                    if (bmp == null)
                        throw new InvalidOperationException("Could not decode texture for export.");
                    bmp.Save(pickedFileName);
                }
            }

            if (string.IsNullOrEmpty(outputFolder))
                MessageBox.Show("Texture exported successfully.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void selectTextureBtn_Click(object sender, EventArgs e)
        {
            OnTextureSelected?.Invoke(_selectedTexture);
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AssignPreviewImage(pictureStreamed, null);
            AssignPreviewImage(picturePersistent, null);
            base.OnFormClosed(e);
        }
    }
}
