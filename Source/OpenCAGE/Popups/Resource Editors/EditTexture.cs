using AlienPAK;
using CATHODE;
using CathodeLib.ObjectExtensions;
using OpenCAGE.Popups.Base;
using OpenCAGE.TextureTools;
using OpenCAGE.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static CATHODE.Textures;

namespace OpenCAGE
{
    public partial class EditTexture : BaseWindow
    {
        public Action<Textures.TEX4> OnTextureSelected;

        TreeUtility _treeHelper;
        Textures _activeTextures;
        Textures.TEX4 _selectedTexture;
        bool _suppressSearchChanged;
        bool _suppressFlagChange;
        bool _environmentMapsOnly;
        bool _suppressCubemapModeChange;
        GUI_CubemapViewer _cubemapViewer;
        readonly List<(Textures.TextureStateFlag flag, CheckBox cb)> _stateFlagChecks = new List<(Textures.TextureStateFlag, CheckBox)>();
        readonly List<(Textures.TextureUsageFlag flag, CheckBox cb)> _usageFlagChecks = new List<(Textures.TextureUsageFlag, CheckBox)>();

        public EditTexture(Textures.TEX4 currentMapping = null, bool showSelectBtn = true, int initialTextureSourceIndex = 0, bool environmentMapsOnly = false) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            PopulateTextureFlagCheckboxes();
            BuildMipBars();

            _environmentMapsOnly = environmentMapsOnly;
            _cubemapViewer = new GUI_CubemapViewer();
            cubemapViewerHost.Child = _cubemapViewer;
            if (cubemapSourceCombo.Items.Count > 0)
                cubemapSourceCombo.SelectedIndex = 0;
            Disposed += (s, e) => DetachCubemapViewer();
            FormClosing += (s, e) => DetachCubemapViewer();

            _treeHelper = new TreeUtility(FileTree, TreeType.GENERIC_FOLDER_AND_FILE);

            _activeTextures = Content.Level.Textures;
            RebuildTextureTree();

            if (currentMapping != null && !string.IsNullOrEmpty(currentMapping.Name))
                _treeHelper.SelectNode(currentMapping.Name);

            selectTextureBtn.Visible = showSelectBtn;
            FileTree.ImageList = imageList1;
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
            if (_activeTextures == null)
                return textureNames;

            if (_environmentMapsOnly)
            {
                List<Textures.TEX4> envMaps = _activeTextures.GetEnvironmentMaps();
                for (int i = 0; i < envMaps.Count; i++)
                    textureNames.Add(envMaps[i].Name);
                return textureNames;
            }

            if (_activeTextures.Entries != null)
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
            _cubemapViewer?.Clear();
            texturePreviewArea.Text = "";
            streamedMetaText.Text = "";
            persistentMetaText.Text = "";
            tabStreamed.Enabled = true;
            tabPersistent.Enabled = true;
            selectTextureBtn.Enabled = false;
            _selectedTexture = null;
            ResetFlagCheckboxes();
            UpdateMipBars(null);
            UpdateCubemapPreviewModeUi(false);
        }

        private static string GetTex4Desc(Textures.TEX4.Texture part)
        {
            if (part == null || part.Content == null || part.Content.Length == 0)
                return "(none)";
            return "Width: " + part.Width + "\r\n" +
                "Height: " + part.Height + "\r\n" +
                "Depth: " + part.Depth + "\r\n" +
                "Mips: " + part.MipLevels + "\r\n" +
                "Size: " + FormatByteSize(part.Content.Length);
        }

        private static string FormatByteSize(long byteCount)
        {
            string[] units = { "bytes", "KB", "MB", "GB", "TB" };
            if (byteCount < 1024)
                return byteCount + " bytes";

            double size = byteCount;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return size.ToString("0.##") + " " + units[unit] + " (" + byteCount.ToString("N0") + " bytes)";
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
            if (_suppressFlagChange || _selectedTexture == null)
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
            if (_suppressFlagChange || _selectedTexture == null)
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
            bool isCubemap = texture.StateFlags.HasFlag(TextureStateFlag.CUBE);
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

            UpdateMipBars(texture);
            UpdateCubemapPreviewModeUi(isCubemap);
            if (isCubemap && cubemapMode3D.Checked)
                RefreshCubemapViewer();
        }

        private void cubemapMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressCubemapModeChange || !(sender is RadioButton rb) || !rb.Checked)
                return;
            UpdateCubemapPreviewModeUi(_selectedTexture != null && _selectedTexture.StateFlags.HasFlag(TextureStateFlag.CUBE));
            if (cubemapMode3D.Checked)
                RefreshCubemapViewer();
        }

        private void cubemapSourceCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressCubemapModeChange)
                return;
            if (cubemapMode3D.Checked)
                RefreshCubemapViewer();
        }

        private void UpdateCubemapPreviewModeUi(bool isCubemap)
        {
            cubemapModePanel.Visible = isCubemap;
            bool show3d = isCubemap && cubemapMode3D.Checked;
            cubemapViewerHost.Visible = show3d;
            previewTabControl.Visible = !show3d;

            bool hasStreamed = _selectedTexture != null && HasContent(_selectedTexture.TextureStreamed);
            bool hasPersistent = _selectedTexture != null && HasContent(_selectedTexture.TexturePersistent);
            cubemapSourceCombo.Enabled = show3d && hasStreamed && hasPersistent;
            cubemapSourceLabel.Enabled = cubemapSourceCombo.Enabled;

            if (show3d && cubemapSourceCombo.SelectedIndex < 0 && cubemapSourceCombo.Items.Count > 0)
            {
                _suppressCubemapModeChange = true;
                cubemapSourceCombo.SelectedIndex = hasStreamed ? 0 : 1;
                _suppressCubemapModeChange = false;
            }
        }

        private void RefreshCubemapViewer()
        {
            if (_cubemapViewer == null || _selectedTexture == null)
                return;

            Textures.TEX4.Texture part = ResolveCubemapPreviewPart();
            if (part == null || !HasContent(part))
            {
                _cubemapViewer.Clear();
                return;
            }

            if (!_selectedTexture.TryDecodeCubemapFaces(part, out Bitmap[] faces) || faces == null)
            {
                _cubemapViewer.Clear();
                return;
            }

            try
            {
                _cubemapViewer.ShowCubemap(faces);
            }
            finally
            {
                for (int i = 0; i < faces.Length; i++)
                    faces[i]?.Dispose();
            }
        }

        private Textures.TEX4.Texture ResolveCubemapPreviewPart()
        {
            if (_selectedTexture == null)
                return null;

            bool preferStreamed = cubemapSourceCombo.SelectedIndex != 1;
            if (preferStreamed && HasContent(_selectedTexture.TextureStreamed))
                return _selectedTexture.TextureStreamed;
            if (HasContent(_selectedTexture.TexturePersistent))
                return _selectedTexture.TexturePersistent;
            if (HasContent(_selectedTexture.TextureStreamed))
                return _selectedTexture.TextureStreamed;
            return null;
        }

        private void UpdateTextureToolsState()
        {
            bool file = FileTree.SelectedNode != null && ((TreeItem)FileTree.SelectedNode.Tag).Item_Type == TreeItemType.EXPORTABLE_FILE;
            bool canEditTextures = _activeTextures != null;
            replaceTextureBtn.Enabled = file && canEditTextures;
            deleteTextureBtn.Enabled = file && canEditTextures;
            exportTextureBtn.Enabled = file;
            importTextureBtn.Enabled = canEditTextures;
            exportAllTexturesBtn.Enabled = _activeTextures.Entries != null && _activeTextures.Entries.Count > 0;
            SetFlagCheckboxesEnabled(file && canEditTextures);
        }

        private void importTextureBtn_Click(object sender, EventArgs e)
        {
            if (_activeTextures == null)
                return;

            using (OpenFileDialog picker = new OpenFileDialog())
            {
                picker.Filter = TextureConverter.ImportFilter;
                if (picker.ShowDialog() != DialogResult.OK)
                    return;

                /* The name is settled in the dialog, along with any folders it should sit in, and the
                 * clash check happens there as it's typed - so by the time this returns the name is
                 * known to be free. */
                Textures.TextureFormat chosen;
                int mipLevels, persistentDrop;
                bool persistentOnly;
                string texName;
                using (TextureImportOptions options = new TextureImportOptions(picker.FileName, null, null,
                    () => _activeTextures.Entries.Select(x => x.Name)))
                {
                    if (options.ShowDialog(this) != DialogResult.OK) return;
                    chosen = options.Format;
                    mipLevels = options.MipLevels;
                    persistentDrop = options.PersistentDrop;
                    persistentOnly = options.PersistentOnly;
                    texName = options.AssetName;
                }

                /* Belt and braces: the list could in principle have moved on since the dialog opened,
                 * and two textures sharing a name merge into one when the level is read back. */
                if (AssetName.Exists(texName, _activeTextures.Entries.Select(x => x.Name)))
                {
                    MessageBox.Show("A texture called '" + texName + "' already exists.", "Import",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Cursor = Cursors.WaitCursor;
                try
                {
                    byte[] fileBytes = TextureConverter.Convert(picker.FileName, chosen, mipLevels, out string problem);
                    if (fileBytes == null)
                    {
                        MessageBox.Show(problem ?? "The image could not be converted.", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    Textures.TEX4 texture = new Textures.TEX4 { Name = texName };
                    Textures.TEX4.Texture part = fileBytes.ToTEX4Part(out texture.Format, out texture.StateFlags, out texture.UsageFlags);
                    if (part == null)
                    {
                        MessageBox.Show("The converted image could not be read back as a texture.", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    //A8 and L8 share a DDS format, so the choice is the only thing that separates them
                    texture.Format = chosen;
                    ApplyParts(texture, part, persistentDrop, persistentOnly);
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
            if (_activeTextures == null || FileTree.SelectedNode == null)
                return;
            if (((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE)
                return;

            string nodeVal = ((TreeItem)FileTree.SelectedNode.Tag).String_Value;
            Textures.TEX4 texture = FindTexture(nodeVal);
            if (texture == null)
                return;

            using (OpenFileDialog picker = new OpenFileDialog())
            {
                picker.Filter = TextureConverter.ImportFilter;
                if (picker.ShowDialog() != DialogResult.OK)
                    return;

                /* Default to what's already in the slot - its format, and the way it splits between
                 * the streamed and persistent copies - so bringing a texture back in leaves it the
                 * shape the game shipped it in. */
                Textures.TextureFormat chosen;
                int mipLevels, persistentDrop;
                bool persistentOnly;
                using (TextureImportOptions options = new TextureImportOptions(picker.FileName, texture.Format, texture))
                {
                    if (options.ShowDialog(this) != DialogResult.OK) return;
                    chosen = options.Format;
                    mipLevels = options.MipLevels;
                    persistentDrop = options.PersistentDrop;
                    persistentOnly = options.PersistentOnly || !HasContent(texture.TextureStreamed);
                }

                Cursor = Cursors.WaitCursor;
                try
                {
                    byte[] content = TextureConverter.Convert(picker.FileName, chosen, mipLevels, out string problem);
                    if (content == null)
                    {
                        MessageBox.Show(problem ?? "The image could not be converted.", "Replace failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    /* Keep the slot's usage flags. Those say which pack the texture belongs to and
                     * what the engine does with it - facts about the slot, not about the file being
                     * dropped into it. */
                    Textures.TextureUsageFlag usage = texture.UsageFlags;
                    Textures.TEX4.Texture part = content.ToTEX4Part(out texture.Format, out texture.StateFlags, out Textures.TextureUsageFlag _);
                    if (part == null)
                    {
                        MessageBox.Show("The converted image could not be read back as a texture.", "Replace failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    texture.Format = chosen;
                    texture.UsageFlags = usage;
                    ApplyParts(texture, part, persistentDrop, persistentOnly);
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
            if (_activeTextures == null || FileTree.SelectedNode == null)
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

            string ext = Path.GetExtension(pickedFileName);
            if (string.Equals(ext, ".dds", StringComparison.OrdinalIgnoreCase))
            {
                byte[] dds = texture.ToDDS();
                if (dds == null)
                    throw new InvalidOperationException("'" + texture.Format + "' has no DDS equivalent, so this texture can't be written as one.");
                File.WriteAllBytes(pickedFileName, dds);
            }
            else
            {
                /* Through the texture rather than its DDS, so an ASTC one takes the route that can
                 * actually decode it. */
                using (Bitmap bmp = texture.ToBitmap())
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

        private void DetachCubemapViewer()
        {
            try
            {
                if (cubemapViewerHost != null && !cubemapViewerHost.IsDisposed)
                    cubemapViewerHost.Child = null;
            }
            catch
            {
                // ElementHost / WPF teardown can race during Close.
            }
            _cubemapViewer = null;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AssignPreviewImage(pictureStreamed, null);
            AssignPreviewImage(picturePersistent, null);
            base.OnFormClosed(e);
        }
    }
}
