using OpenCAGE.Theming;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The editor's icons for entities, composites, folders and the composite commands - in the order their art is
    /// stored, so never reorder them, only add to the end.
    /// </summary>
    public enum EditorIcon
    {
        Parameter,
        Function,
        CompositeInstance,
        Proxy,
        Alias,
        PinReference,
        PinMethod,
        PinTarget,
        PinInput,
        PinOutput,
        Folder,
        FolderOpen,
        File,
        Composite,
        RootComposite,
        SystemComposite,
        DisplayModel,
        CreateCompositeFromSelected,
        DeinstanceComposite,
        CompositeVariant,
        DuplicateComposite,
        ArrangePage,
        ArrangeSelected,
    }

    /// <summary>
    /// Where every list, tree and menu gets its entity and composite icons. The art is drawn for each theme at a few sizes
    /// (Resources\Icons: one strip of every icon per size, a light and a dark one), and a size in between is resampled
    /// down from the next size up, so an icon is never stretched. The image lists are shared, each in the order its kind
    /// of list has always indexed, and are redrawn in place when the theme changes: nothing that shows them needs to know
    /// which theme is on.
    /// </summary>
    public static class EditorIcons
    {
        private static readonly int[] _artSizes = { 16, 32, 48, 64, 128, 256 };
        private static readonly Dictionary<string, Bitmap> _strips = new Dictionary<string, Bitmap>();

        /// <summary>The entity lists' order: what <see cref="EditorUtils.GetIndexesForListViewItem"/> and the pin type icons index, with the palette's category folders after.</summary>
        public static readonly EditorIcon[] EntityOrder =
        {
            EditorIcon.Parameter, EditorIcon.Function, EditorIcon.CompositeInstance, EditorIcon.Proxy, EditorIcon.Alias,
            EditorIcon.PinReference, EditorIcon.PinMethod, EditorIcon.PinTarget, EditorIcon.PinInput, EditorIcon.PinOutput,
            EditorIcon.Folder, EditorIcon.FolderOpen,
        };

        /// <summary>The composite trees' order (<see cref="TreeUtility"/> and the pickers): folder, composite, open folder, root, GLOBAL/PAUSEMENU, DisplayModel.</summary>
        public static readonly EditorIcon[] CompositeTreeOrder =
        {
            EditorIcon.Folder, EditorIcon.Composite, EditorIcon.FolderOpen, EditorIcon.RootComposite, EditorIcon.SystemComposite, EditorIcon.DisplayModel,
        };

        /// <summary>The composite browser's flat list: composite, folder, root, GLOBAL/PAUSEMENU, DisplayModel.</summary>
        public static readonly EditorIcon[] CompositeTileOrder =
        {
            EditorIcon.Composite, EditorIcon.Folder, EditorIcon.RootComposite, EditorIcon.SystemComposite, EditorIcon.DisplayModel,
        };

        /// <summary>The asset trees' order (<see cref="TreeItemIcon"/>): folder, file, open folder.</summary>
        public static readonly EditorIcon[] FileTreeOrder = { EditorIcon.Folder, EditorIcon.File, EditorIcon.FolderOpen };

        /// <summary>The pin pickers' order: reference, method, target, input, output.</summary>
        public static readonly EditorIcon[] PinOrder = { EditorIcon.PinReference, EditorIcon.PinMethod, EditorIcon.PinTarget, EditorIcon.PinInput, EditorIcon.PinOutput };

        private sealed class SharedList
        {
            public ImageList List;
            public EditorIcon[] Order;
            public int Size;
        }
        private static readonly List<SharedList> _lists = new List<SharedList>();
        private static readonly Dictionary<Component, Action> _bound = new Dictionary<Component, Action>();

        /// <summary>The theme changed and every shared list and bound menu item has its new icons. For anything that copied icons out.</summary>
        public static event Action Changed;

        static EditorIcons()
        {
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public static ImageList EntityList => Shared(EntityOrder, 16);
        public static ImageList CompositeTree => Shared(CompositeTreeOrder, 16);
        public static ImageList FileTree => Shared(FileTreeOrder, 16);
        public static ImageList Pins => Shared(PinOrder, 16);

        /// <summary>The composite browser's flat list icons at a size of its own (its large and small views).</summary>
        public static ImageList CompositeTiles(int size) => Shared(CompositeTileOrder, size);

        /// <summary>The icons a shared list holds, in order; null for any other list.</summary>
        public static EditorIcon[] OrderOf(ImageList list)
        {
            foreach (SharedList shared in _lists)
                if (shared.List == list)
                    return shared.Order;
            return null;
        }

        /// <summary>An icon at a size, for the current theme. The bitmap is the caller's.</summary>
        public static Bitmap Get(EditorIcon icon, int size)
        {
            size = Math.Max(1, size);
            int art = _artSizes[_artSizes.Length - 1];
            foreach (int candidate in _artSizes)
            {
                if (candidate >= size)
                {
                    art = candidate;
                    break;
                }
            }

            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            Bitmap strip = Strip(art, ThemeManager.IsDark);
            if (strip == null || ((int)icon + 1) * art > strip.Width)
                return result;

            Rectangle cell = new Rectangle((int)icon * art, 0, art, art);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent);
                if (art == size)
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(strip, new Rectangle(0, 0, size, size), cell, GraphicsUnit.Pixel);
                }
                else
                {
                    //Out of the strip first, so the filter can't reach into the icons either side
                    using (Bitmap source = strip.Clone(cell, PixelFormat.Format32bppArgb))
                    using (ImageAttributes edges = new ImageAttributes())
                    {
                        edges.SetWrapMode(WrapMode.TileFlipXY);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.DrawImage(source, new Rectangle(0, 0, size, size), 0, 0, art, art, GraphicsUnit.Pixel, edges);
                    }
                }
            }
            return result;
        }

        /// <summary>An icon in the middle of a larger square (a preview's), at the size that sits well among previews.</summary>
        public static Bitmap GetInSquare(EditorIcon icon, int square)
        {
            int size = square <= 32 ? square : (int)Math.Round(square * 0.75);
            Bitmap result = new Bitmap(square, square, PixelFormat.Format32bppArgb);
            using (Bitmap art = Get(icon, size))
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent);
                g.DrawImageUnscaled(art, (square - size) / 2, (square - size) / 2);
            }
            return result;
        }

        /// <summary>Give a menu item an icon that follows the theme.</summary>
        public static void Bind(ToolStripItem item, EditorIcon icon)
        {
            Bind(item, () =>
            {
                Image old = item.Image;
                item.Image = Get(icon, 16);
                old?.Dispose();
            });
        }

        /// <summary>Give a button an icon that follows the theme, sized to sit inside it.</summary>
        public static void Bind(ButtonBase button, EditorIcon icon)
        {
            Bind(button, () =>
            {
                int width = button.ClientSize.Width > 0 ? button.ClientSize.Width : button.Width;
                int height = button.ClientSize.Height > 0 ? button.ClientSize.Height : button.Height;
                Image old = button.Image;
                button.Image = Get(icon, Math.Max(12, Math.Min(width, height) - 4));
                old?.Dispose();
            });
        }

        /// <summary>Give a window (a dock panel's tab) an icon that follows the theme, in place of the one it had.</summary>
        public static void BindIcon(Form form, EditorIcon icon)
        {
            Bind(form, () => form.Icon = GetIcon(icon));
        }

        /// <summary>An icon as a window icon, for the current theme. Made once per theme and kept: never dispose it.</summary>
        public static Icon GetIcon(EditorIcon icon)
        {
            string key = icon + (ThemeManager.IsDark ? ":dark" : "");
            if (_windowIcons.TryGetValue(key, out Icon made))
                return made;

            using (Bitmap art = Get(icon, 16))
            {
                IntPtr handle = art.GetHicon();
                try
                {
                    //FromHandle doesn't own the handle; its clone owns a copy, so the original can go
                    using (Icon borrowed = Icon.FromHandle(handle))
                        made = (Icon)borrowed.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
            _windowIcons[key] = made;
            return made;
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        private static readonly Dictionary<string, Icon> _windowIcons = new Dictionary<string, Icon>();

        //Everything given an icon that follows the theme, and how to give it the current theme's
        private static void Bind(Component owner, Action apply)
        {
            if (owner == null)
                return;
            if (!_bound.ContainsKey(owner))
                owner.Disposed += BoundOwnerDisposed;
            _bound[owner] = apply;
            apply();
        }

        private static void BoundOwnerDisposed(object sender, EventArgs e)
        {
            if (sender is Component owner)
                _bound.Remove(owner);
        }

        private static ImageList Shared(EditorIcon[] order, int size)
        {
            foreach (SharedList shared in _lists)
                if (shared.Order == order && shared.Size == size)
                    return shared.List;

            //An ImageList makes light grey see-through unless told otherwise
            ImageList list = new ImageList() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(size, size), TransparentColor = Color.Transparent };
            foreach (EditorIcon icon in order)
                list.Images.Add(Get(icon, size));
            _lists.Add(new SharedList() { List = list, Order = order, Size = size });
            return list;
        }

        private static Bitmap Strip(int size, bool dark)
        {
            string name = "icons_" + size + (dark ? "_dark" : "");
            if (_strips.TryGetValue(name, out Bitmap strip))
                return strip;

            strip = null;
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenCAGE.Resources.Icons." + name + ".png"))
                {
                    if (stream != null)
                    {
                        //A Bitmap made from a stream needs the stream for its whole life: this copy doesn't
                        using (Bitmap loaded = new Bitmap(stream))
                            strip = new Bitmap(loaded);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Editor Icons", "Couldn't load " + name + ": " + ex.Message);
            }
            _strips[name] = strip;
            return strip;
        }

        /* Every shared list gets the other theme's art where its old art was - the controls drawing from them keep their
           lists and indices - then the menus, then anything that copied icons out, and the windows repaint. */
        private static void OnThemeChanged()
        {
            foreach (SharedList shared in _lists)
                for (int i = 0; i < shared.Order.Length && i < shared.List.Images.Count; i++)
                    shared.List.Images[i] = Get(shared.Order[i], shared.Size);

            foreach (Action apply in new List<Action>(_bound.Values))
                apply();

            Changed?.Invoke();

            for (int i = 0; i < Application.OpenForms.Count; i++)
            {
                Form form = Application.OpenForms[i];
                if (form != null && !form.IsDisposed)
                    form.Invalidate(true);
            }
        }
    }
}
