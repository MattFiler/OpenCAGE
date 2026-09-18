using CATHODE.Scripting;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Write any number of the loaded level's composites, with everything they use, to an .ocp
    /// package on disk - for bringing them into a level on another install or another machine.
    /// The level itself is untouched.
    /// </summary>
    public partial class ExportCompositeArchive : BaseWindow
    {
        private readonly CompositeTree _tree;

        /// <param name="composite">A composite to start with ticked, or null for none.</param>
        public ExportCompositeArchive(Composite composite) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            _tree = new CompositeTree(compositeTree);
            _tree.SelectionChanged += UpdateSummary;
            _tree.Load(
                Content.Level.Commands.Entries.Where(o => o != null).Select(o => new CompositeTree.Item() { Id = o.shortGUID, Name = o.name, Kind = CompositeTree.KindOf(Content.EditorUtils.GetCompositeType(o)) }),
                CompositeNesting.InstancesOf(Content.Level.Commands),
                composite == null ? null : new[] { composite.shortGUID });

            if (composite != null)
                nameBox.Text = LeafName(composite.name);
        }

        private void filterBox_TextChanged(object sender, EventArgs e)
        {
            _tree.Filter = filterBox.Text;
        }

        private void checkShown_Click(object sender, EventArgs e)
        {
            _tree.SetShown(true);
        }

        private void uncheckShown_Click(object sender, EventArgs e)
        {
            _tree.UntickAll();
        }

        private void UpdateSummary()
        {
            summaryLabel.Text = _tree.Summary();
        }

        private void export_Click(object sender, EventArgs e)
        {
            List<Composite> composites = _tree.Ticked.Select(id => Content.Level.Commands.GetComposite(id)).Where(o => o != null).ToList();
            if (composites.Count == 0)
            {
                MessageBox.Show("Tick the composites to export first.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string archivePath;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Export composites to disk";
                dialog.Filter = CompositeArchive.FileFilter;
                dialog.DefaultExt = CompositeArchive.Extension.TrimStart('.');
                dialog.AddExtension = true;
                dialog.OverwritePrompt = true;
                dialog.FileName = SuggestedFileName(composites);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                archivePath = dialog.FileName;
            }

            //What the author typed, blank included: the import window shows a name only when there is one
            CompositeArchive.PackageInfo info = new CompositeArchive.PackageInfo()
            {
                Name = nameBox.Text.Trim(),
                Description = descriptionBox.Text.Trim(),
            };

            Enabled = false;
            Cursor.Current = Cursors.WaitCursor;
            CompositeArchive.Manifest manifest;
            try
            {
                manifest = CompositeArchive.Export(Content.Level, composites, archivePath, info);
            }
            catch (Exception ex)
            {
                Cursor.Current = Cursors.Default;
                Enabled = true;
                MessageBox.Show("The export did not complete:\n\n" + ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Cursor.Current = Cursors.Default;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            string size = File.Exists(archivePath) ? " (" + FormatSize(new FileInfo(archivePath).Length) + ")" : "";
            MessageBox.Show("Exported " + composites.Count + " composite" + (composites.Count == 1 ? "" : "s") + " (" + manifest.Composites.Count + " including the composites they instance) to\n\n" + archivePath + size, "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Close();
        }

        /* The package name if there is one, else the first ticked composite's leaf name, else the
           level's - with anything a filename can't hold dropped */
        private string SuggestedFileName(List<Composite> composites)
        {
            string name = nameBox.Text.Trim();
            if (name.Length == 0)
            {
                name = composites.Count == 1
                    ? LeafName(composites[0].name)
                    : LeafName(Content.Level.Name) + "_composites";
            }
            return SafeFileName(name) + CompositeArchive.Extension;
        }

        private static string LeafName(string path)
        {
            return Path.GetFileName((path ?? "").Replace('\\', '/').TrimEnd('/'));
        }

        private static string SafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "composites";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name.Trim();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("0.0") + " GB";
            if (bytes >= 1L << 20) return (bytes / (double)(1L << 20)).ToString("0.0") + " MB";
            if (bytes >= 1L << 10) return (bytes / (double)(1L << 10)).ToString("0.0") + " KB";
            return bytes + " B";
        }
    }
}
