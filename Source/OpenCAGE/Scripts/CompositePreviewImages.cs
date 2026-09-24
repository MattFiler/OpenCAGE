using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Puts composite previews into the ImageLists the editor's lists and trees draw from. A list or tree
    /// keeps its stock icons at the indices it always used - a derived list starts with those, scaled to
    /// the preview size - and previews are appended by key, so an item with a preview uses the key and one
    /// without keeps its index.
    /// </summary>
    public static class CompositePreviewImages
    {
        /// <summary>The square a tree row grows to when it shows previews.</summary>
        public const int TreeSize = 32;

        //The keys each derived list holds, so a lookup is not a scan of the list's own key strings
        private static readonly Dictionary<ImageList, HashSet<string>> _keys = new Dictionary<ImageList, HashSet<string>>();

        //Per tree: the list it was made with, and the preview list derived from it
        private static readonly Dictionary<TreeView, ImageList> _stockLists = new Dictionary<TreeView, ImageList>();
        private static readonly Dictionary<TreeView, ImageList> _previewLists = new Dictionary<TreeView, ImageList>();

        public static bool TreesEnabled => SettingsManager.GetBool(Settings.CompositePreviewsInTrees, false);

        /// <summary>
        /// The derived list for a stock one, made once: its first images are the stock list's, scaled and
        /// centred in the preview square, so ImageIndex values keep their meaning.
        /// </summary>
        public static ImageList EnsurePreviewList(ImageList stock, int size, ref ImageList cache)
        {
            if (cache != null)
                return cache;

            //An ImageList makes light grey transparent unless told otherwise, which would punch holes in a preview
            ImageList list = new ImageList() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(size, size), TransparentColor = Color.Transparent };
            if (stock != null)
            {
                for (int i = 0; i < stock.Images.Count; i++)
                {
                    string key = i < stock.Images.Keys.Count ? stock.Images.Keys[i] : "";
                    using (Image image = stock.Images[i])
                    {
                        Bitmap scaled = Centred(image, size);
                        if (string.IsNullOrEmpty(key)) list.Images.Add(scaled);
                        else list.Images.Add(key, scaled);
                    }
                }
            }
            _keys[list] = new HashSet<string>();
            cache = list;
            return list;
        }

        /// <summary>
        /// The key of a composite's preview in this list, adding it on first use. Null when the composite
        /// has no preview, in which case the caller keeps the stock icon.
        /// </summary>
        public static string EnsurePreview(ImageList list, ShortGuid id, int size)
        {
            if (list == null)
                return null;

            string key = KeyFor(id);
            HashSet<string> keys = KeysOf(list);
            if (keys.Contains(key))
                return key;

            Image image = CompositePreviewManager.GetPreviewImage(id, size);
            if (image == null)
                return null;

            //The list keeps whatever it is handed until its handle exists, and the manager may drop its
            //cached copy in the meantime - so the list gets one of its own. A preview the list still holds
            //from before (the composite went without one for a while) is replaced where it is: added again
            //under the same key, the old one would be the one that key found.
            Bitmap copy = new Bitmap(image);
            int index = list.Images.IndexOfKey(key);
            if (index >= 0) list.Images[index] = copy;
            else list.Images.Add(key, copy);
            keys.Add(key);
            return key;
        }

        /// <summary>
        /// The previews a tree is about to ask for, put into its list in one go. A tree bound to the list is
        /// told about every preview added, and answers by rebinding the list to all of its nodes: added one
        /// at a time as the nodes were made, the 900 previews of a level cost 20 seconds. Added here first,
        /// as one batch, EnsurePreview then finds each key already there.
        /// </summary>
        public static void EnsurePreviews(ImageList list, IEnumerable<ShortGuid> ids, int size)
        {
            if (list == null || ids == null)
                return;

            HashSet<string> keys = KeysOf(list);
            List<Bitmap> bitmaps = new List<Bitmap>();
            List<string> newKeys = new List<string>();
            foreach (ShortGuid id in ids)
            {
                string key = KeyFor(id);
                if (keys.Contains(key) || newKeys.Contains(key))
                    continue;

                Image image = CompositePreviewManager.GetPreviewImage(id, size);
                if (image == null)
                    continue;

                //A preview the list still holds from before goes back in where it was (see EnsurePreview)
                int index = list.Images.IndexOfKey(key);
                if (index >= 0)
                {
                    list.Images[index] = new Bitmap(image);
                    keys.Add(key);
                    continue;
                }

                bitmaps.Add(new Bitmap(image));
                newKeys.Add(key);
            }
            if (bitmaps.Count == 0)
                return;

            int start = list.Images.Count;
            list.Images.AddRange(bitmaps.ToArray());
            for (int j = 0; j < bitmaps.Count; j++)
            {
                list.Images.SetKeyName(start + j, newKeys[j]);
                keys.Add(newKeys[j]);
                //A list with a handle copied the preview into its own strip and keeps no reference to ours
                if (list.HandleCreated)
                    bitmaps[j].Dispose();
            }
        }

        /// <summary>
        /// Previews for several composites at once, giving each one's index in the list, or -1 where there is
        /// no preview. For the list that previews a whole level as it scrolls, which differs from a tree in
        /// two ways. Its previews are decoded for it alone rather than through the manager's cache: they come
        /// and go, and a cached copy of each one it passed would stay behind for the rest of the session. And
        /// they go in as one batch rather than one at a time: every preview added tells the views drawing
        /// from the list, and a ListView answers by sending itself every item's image again, which with every
        /// composite listed made each single add cost as much as previewing a whole screen. Items are best
        /// pointed at these by index - a key is looked up by a scan of the list's keys each time that sweep runs.
        /// </summary>
        public static int[] AddTransientPreviews(ImageList list, IList<ShortGuid> ids, int size)
        {
            int[] indices = new int[ids?.Count ?? 0];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = -1;
            if (list == null || indices.Length == 0)
                return indices;

            HashSet<string> keys = KeysOf(list);
            List<Bitmap> bitmaps = new List<Bitmap>();
            List<int> slots = new List<int>();
            for (int i = 0; i < ids.Count; i++)
            {
                string key = KeyFor(ids[i]);
                if (keys.Contains(key))
                {
                    indices[i] = list.Images.IndexOfKey(key);
                    continue;
                }

                Image image = CompositePreviewManager.DecodePreviewImage(ids[i], size);
                if (image == null)
                    continue;

                //A preview the list still holds from before goes back in where it was (see Ensure)
                int existing = list.Images.IndexOfKey(key);
                if (existing >= 0)
                {
                    list.Images[existing] = (Bitmap)image;
                    if (list.HandleCreated)
                        image.Dispose();
                    keys.Add(key);
                    indices[i] = existing;
                    continue;
                }

                bitmaps.Add((Bitmap)image);
                slots.Add(i);
            }
            if (bitmaps.Count == 0)
                return indices;

            int start = list.Images.Count;
            list.Images.AddRange(bitmaps.ToArray());
            for (int j = 0; j < bitmaps.Count; j++)
            {
                string key = KeyFor(ids[slots[j]]);
                list.Images.SetKeyName(start + j, key);
                keys.Add(key);
                indices[slots[j]] = start + j;
                if (list.HandleCreated)
                    bitmaps[j].Dispose();
            }
            return indices;
        }

        private static HashSet<string> KeysOf(ImageList list)
        {
            if (!_keys.TryGetValue(list, out HashSet<string> keys))
            {
                keys = new HashSet<string>();
                _keys[list] = keys;
            }
            return keys;
        }

        public static string KeyFor(ShortGuid id) => "preview:" + id.ToByteString();

        /* The manager has new previews (or none) for these composites, and calls this before it tells anyone,
           so a panel that rebuilds on the news finds the lists already right. A derived list that holds one
           gets the new preview put in where the old one was - nothing is ever taken out of the middle of a
           list, since the nodes built on it were given positions, not keys - and the trees on that list
           repaint, so an open tree shows the change at once. A composite with no preview any more keeps its
           old one until its tree is rebuilt: its key is dropped here, so the rebuild gives that node its icon
           back. */
        internal static void Refresh(IReadOnlyCollection<ShortGuid> ids)
        {
            if (ids == null || ids.Count == 0)
                return;
            foreach (KeyValuePair<ImageList, HashSet<string>> entry in _keys)
            {
                ImageList list = entry.Key;
                bool replaced = false;
                foreach (ShortGuid id in ids)
                {
                    string key = KeyFor(id);
                    if (!entry.Value.Contains(key))
                        continue;
                    int index = list.Images.IndexOfKey(key);
                    Image image = index >= 0 ? CompositePreviewManager.GetPreviewImage(id, list.ImageSize.Width) : null;
                    if (image != null)
                    {
                        //A list with a handle copies the preview into its own strip and keeps no reference to
                        //ours, so the copy made for it can go straight away (see AddTransientPreviews)
                        Bitmap copy = new Bitmap(image);
                        list.Images[index] = copy;
                        if (list.HandleCreated)
                            copy.Dispose();
                        replaced = true;
                    }
                    else
                    {
                        entry.Value.Remove(key);
                    }
                }
                if (!replaced)
                    continue;
                foreach (KeyValuePair<TreeView, ImageList> owner in _previewLists)
                    if (owner.Value == list && !owner.Key.IsDisposed)
                        owner.Key.Invalidate();
            }
        }

        /// <summary>
        /// Drop every preview from a derived list, keeping the stock icons at the front. A list that only
        /// ever shows one folder's worth of composites is emptied and refilled per folder.
        /// </summary>
        public static void ClearPreviews(ImageList list, int stockCount)
        {
            if (list == null)
                return;
            while (list.Images.Count > stockCount)
                list.Images.RemoveAt(list.Images.Count - 1);
            if (_keys.TryGetValue(list, out HashSet<string> keys))
                keys.Clear();
        }

        /// <summary>
        /// Swap a tree between its own 16px list and a 32px preview list derived from it. The first call sees
        /// the tree's current list as its own; disabling puts that back.
        /// </summary>
        public static void ApplyToTree(TreeView tree, bool enabled)
        {
            if (tree == null || tree.IsDisposed)
                return;

            if (!_stockLists.TryGetValue(tree, out ImageList stock))
            {
                stock = tree.ImageList;
                _stockLists[tree] = stock;
                tree.Disposed += TreeDisposed;
            }

            if (enabled)
            {
                if (!_previewLists.TryGetValue(tree, out ImageList previews) || previews == null)
                {
                    previews = null;
                    EnsurePreviewList(stock, TreeSize, ref previews);
                    _previewLists[tree] = previews;
                }
                if (tree.ImageList != previews)
                    tree.ImageList = previews;
            }
            else if (tree.ImageList != stock)
            {
                tree.ImageList = stock;
            }
        }

        private static void TreeDisposed(object sender, EventArgs e)
        {
            TreeView tree = sender as TreeView;
            if (tree == null)
                return;
            tree.Disposed -= TreeDisposed;
            _stockLists.Remove(tree);
            if (_previewLists.TryGetValue(tree, out ImageList previews))
            {
                _previewLists.Remove(tree);
                if (previews != null)
                {
                    _keys.Remove(previews);
                    previews.Dispose();
                }
            }
        }

        /// <summary>Forget a derived list that is being disposed by its owner.</summary>
        public static void Release(ImageList list)
        {
            if (list != null)
                _keys.Remove(list);
        }

        /* The stock icon in the middle of the square at its own size, or shrunk to fit when it is bigger */
        private static Bitmap Centred(Image image, int size)
        {
            Bitmap square = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(square))
            {
                g.Clear(Color.Transparent);
                if (image == null)
                    return square;
                float scale = Math.Min(1f, Math.Min((float)size / image.Width, (float)size / image.Height));
                int width = Math.Max(1, (int)Math.Round(image.Width * scale));
                int height = Math.Max(1, (int)Math.Round(image.Height * scale));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(image, new Rectangle((size - width) / 2, (size - height) / 2, width, height));
            }
            return square;
        }
    }
}
