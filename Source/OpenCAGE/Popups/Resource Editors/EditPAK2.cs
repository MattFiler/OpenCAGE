using CATHODE;
using CathodeLib;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class EditPAK2 : BaseWindow
    {
        PAK2 _archive;

        public EditPAK2() : base()
        {
            InitializeComponent();
        }

        public void LoadPAK2(string pak, string title)
        {
            this.Text = "Edit " + title;
            _archive = new PAK2(Singleton.PathToAI + "/DATA/" + pak);
            FillListFromArchive();
        }

        private void FillListFromArchive()
        {
            listEntries.BeginUpdate();
            listEntries.Items.Clear();
            foreach (PAK2.File entry in _archive.Entries.OrderBy(e => e.Filename, StringComparer.OrdinalIgnoreCase))
            {
                if (searchTxt.Text != "" && NormalisePath(entry.Filename.ToUpper().Replace(" ", "")).Contains(NormalisePath(searchTxt.Text.Replace(" ", "").ToUpper())) == false)
                    continue;

                int len = entry.Content?.Length ?? 0;
                var item = new ListViewItem(entry.Filename.Replace('\\', '/'));
                item.SubItems.Add(len.ToString("N0"));
                item.Tag = entry.Filename;
                listEntries.Items.Add(item);
            }
            listEntries.EndUpdate();

            UpdateButtonState();
        }

        private void searchBtn_Click(object sender, EventArgs e)
        {
            FillListFromArchive();
        }

        private void SearchOnEnter(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                searchBtn.PerformClick();
        }

        private void UpdateButtonState()
        {
            bool loaded = _archive != null && _archive.Loaded;
            btnExport.Enabled = loaded && listEntries.SelectedItems.Count > 0;
            btnDelete.Enabled = loaded && listEntries.SelectedItems.Count > 0;
            btnReplace.Enabled = loaded && listEntries.SelectedItems.Count > 0;
            btnImport.Enabled = loaded;
            btnExportAll.Enabled = loaded && _archive.Entries.Count > 0;
        }

        private string GetSelectedFilename()
        {
            if (listEntries.SelectedItems.Count == 0) return null;
            return listEntries.SelectedItems[0].Tag as string;
        }

        private static string NormalisePath(string p) => (p ?? "").Replace('\\', '/');

        private PAK2.File FindEntry(string filename)
        {
            if (_archive == null || filename == null) return null;
            string n = NormalisePath(filename);
            return _archive.Entries.FirstOrDefault(o => NormalisePath(o.Filename) == n);
        }

        private void listEntries_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtonState();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            string fn = GetSelectedFilename();
            if (fn == null) return;
            var entry = FindEntry(fn);
            using (var dlg = new SaveFileDialog())
            {
                dlg.FileName = Path.GetFileName(fn);
                dlg.Filter = "All files|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    File.WriteAllBytes(dlg.FileName, entry.Content);
                    MessageBox.Show("File exported.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnReplace_Click(object sender, EventArgs e)
        {
            string fn = GetSelectedFilename();
            if (fn == null) return;
            var entry = FindEntry(fn);
            if (entry == null) return;

            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "All files|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                entry.Content = File.ReadAllBytes(dlg.FileName);

                RefreshListItemFor(fn);
                UpdateButtonState();
                Save();
            }
        }

        private void RefreshListItemFor(string filename)
        {
            string n = NormalisePath(filename);
            foreach (ListViewItem item in listEntries.Items)
            {
                if (NormalisePath(item.Tag as string) != n) continue;
                var entry = FindEntry(filename);
                if (entry != null && item.SubItems.Count > 1)
                    item.SubItems[1].Text = (entry.Content?.Length ?? 0).ToString("N0");
                break;
            }
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            string diskPath;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "All files|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                diskPath = dlg.FileName;
            }

            byte[] fileBytes;
            try { fileBytes = File.ReadAllBytes(diskPath); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string defaultRel = Path.GetFileName(diskPath).Replace('\\', '/');
            string pakPath;
            using (var pathDlg = new Form())
            {
                pathDlg.Text = "Set Path";
                pathDlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                pathDlg.StartPosition = FormStartPosition.CenterParent;
                pathDlg.MinimizeBox = false;
                pathDlg.MaximizeBox = false;
                pathDlg.ClientSize = new System.Drawing.Size(520, 110);

                var lbl = new Label { Left = 12, Top = 12, Width = 496, Text = "Entry path:" };
                var tb = new TextBox { Left = 12, Top = 36, Width = 496, Text = defaultRel };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 332, Top = 72, Width = 80 };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 420, Top = 72, Width = 80 };
                pathDlg.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
                pathDlg.AcceptButton = ok;
                pathDlg.CancelButton = cancel;

                if (pathDlg.ShowDialog(this) != DialogResult.OK) return;
                pakPath = tb.Text.Trim().Replace('\\', '/');
            }

            if (string.IsNullOrEmpty(pakPath)) return;

            var existing = FindEntry(pakPath);
            if (existing != null)
            {
                if (MessageBox.Show("A file with this path already exists. Replace its contents?", "Import", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                existing.Content = fileBytes;
            }
            else
            {
                _archive.Entries.Add(new PAK2.File { Filename = NormalisePath(pakPath), Content = fileBytes });
            }

            FillListFromArchive();
            Save();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            string fn = GetSelectedFilename();
            if (fn == null) return;
            if (MessageBox.Show("Remove this file?\n" + fn, "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            _archive.Entries.RemoveAll(o => NormalisePath(o.Filename) == NormalisePath(fn));
            FillListFromArchive();
            Save();
        }

        private void btnExportAll_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Choose folder to export all files.";
                if (fbd.ShowDialog() != DialogResult.OK) return;

                string root = fbd.SelectedPath;
                foreach (var entry in _archive.Entries)
                {
                    if (entry.Content == null) continue;
                    string rel = NormalisePath(entry.Filename);
                    string outPath = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                    string dir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllBytes(outPath, entry.Content);
                }
                MessageBox.Show("Exported " + _archive.Entries.Count + " entries.", "Export all", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void Save()
        {
            EditorUtils.CloseAI();
            _archive.Save();
        }
    }
}
