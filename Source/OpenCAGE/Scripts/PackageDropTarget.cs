using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Lets a package file (.ocp / .omp) be dropped anywhere on the editor window to open it.
    /// </summary>
    public static class PackageDropTarget
    {
        private static readonly HashSet<Control> _attached = new HashSet<Control>();
        private static readonly HashSet<Control> _watched = new HashSet<Control>();

        public static void Attach(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            if (ShouldAttach(root) && _attached.Add(root))
            {
                try
                {
                    root.AllowDrop = true;
                    root.DragEnter += OnDragEnter;
                    root.DragOver += OnDragEnter;
                    root.DragDrop += OnDragDrop;
                    root.Disposed += OnDisposed;
                }
                catch
                {
                    //A control that refuses (a tool strip with item reordering on): drops rise past it
                    _attached.Remove(root);
                }
            }

            if (IsContainer(root) && _watched.Add(root))
            {
                root.ControlAdded += OnControlAdded;
                root.Disposed += OnDisposed;
            }

            foreach (Control child in root.Controls)
                Attach(child);
        }

        private static bool IsContainer(Control control)
        {
            return control is ContainerControl || control is Panel || control is GroupBox || control is TabControl || control is TabPage || control is ToolStrip;
        }

        private static bool ShouldAttach(Control control)
        {
            //The flowgraph canvas keeps AllowDrop off on purpose so its dock content takes the drop
            if (control.GetType().FullName == "ST.Library.UI.NodeEditor.STNodeEditor")
                return false;
            //Hosted WPF, browser and rich text controls have drag handling of their own
            if (control is WebBrowser || control is RichTextBox || control.GetType().Name == "ElementHost")
                return false;
            if (control is ToolStrip strip && strip.AllowItemReorder)
                return false;
            if (control.AllowDrop)
                return true;
            return IsContainer(control);
        }

        private static void OnControlAdded(object sender, ControlEventArgs e)
        {
            Attach(e.Control);
        }

        private static void OnDisposed(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (control == null)
                return;
            if (_attached.Remove(control))
            {
                control.DragEnter -= OnDragEnter;
                control.DragOver -= OnDragEnter;
                control.DragDrop -= OnDragDrop;
            }
            if (_watched.Remove(control))
                control.ControlAdded -= OnControlAdded;
            control.Disposed -= OnDisposed;
        }

        /// <summary>The package files in a drag, or an empty list when the drag holds none.</summary>
        public static List<string> PackagesIn(IDataObject data)
        {
            List<string> packages = new List<string>();
            try
            {
                if (data == null || !data.GetDataPresent(DataFormats.FileDrop))
                    return packages;
                string[] files = data.GetData(DataFormats.FileDrop) as string[];
                if (files == null)
                    return packages;
                packages.AddRange(files.Where(PackageFiles.IsPackage));
            }
            catch { }
            return packages;
        }

        /// <summary>True when the drag is a package drop, which the handlers here look after.</summary>
        public static bool IsPackageDrag(DragEventArgs e)
        {
            return e != null && PackagesIn(e.Data).Count != 0;
        }

        private static void OnDragEnter(object sender, DragEventArgs e)
        {
            if (PackagesIn(e.Data).Count != 0)
                e.Effect = DragDropEffects.Copy;
        }

        private static void OnDragDrop(object sender, DragEventArgs e)
        {
            List<string> packages = PackagesIn(e.Data);
            if (packages.Count == 0)
                return;
            //Out of the OLE callback before anything is shown
            CommandsEditor editor = Singleton.Editor;
            if (editor != null && !editor.IsDisposed && editor.IsHandleCreated)
                editor.BeginInvoke(new Action(() => PackageFiles.OpenMany(packages)));
        }
    }
}
