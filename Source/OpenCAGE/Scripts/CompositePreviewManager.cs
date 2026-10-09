using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// The rendered preview of a composite: what the level viewer draws of it, framed in a square. Two
    /// tables answer for one, the way flowgraph layouts do - the level's own (user) table in its
    /// COMMANDS.PAK first, then the shipped (baked) one in flowgraphs.dat, which holds a preview of every
    /// vanilla composite. Composites edited since their preview was taken are captured again by the
    /// viewer when the level is saved, and the results go into the user table.
    /// </summary>
    /// <remarks>
    /// A user entry with no PNG in it means "this composite draws nothing any more" (the viewer reported
    /// it empty) and hides the baked preview rather than falling through to it. The baked table is read
    /// lazily and may not exist at all: everything here works without it.
    /// </remarks>
    public static class CompositePreviewManager
    {
        private const string LogSystem = "Composite Previews";

        private static CompositePreviewTable _baked;
        private static bool _bakedLoaded;
        private static CompositePreviewTable _user = new CompositePreviewTable();

        public static Commands LinkedCommands => _commands;
        private static Commands _commands;

        //Composites whose preview is out of date: cleared per composite when the viewer's preview lands
        private static readonly HashSet<ShortGuid> _dirty = new HashSet<ShortGuid>();

        //Composites deleted this session, by ID: one that comes back as a different object (a port overwriting it) has new
        //content, one that comes back as itself (an undo) has the content it went with - and gets back the level's own
        //preview of it and any recapture it was waiting for. Weak: nothing is kept alive here.
        private sealed class Removal
        {
            public WeakReference<Composite> Composite;
            public bool WasDirty;
            public CompositePreviewTable.Preview UserEntry;
        }
        private static readonly Dictionary<ShortGuid, Removal> _removed = new Dictionary<ShortGuid, Removal>();

        //Ported composites about to arrive whose source level had its own preview of them: it shows what is arriving
        private static readonly Dictionary<ShortGuid, CompositePreviewTable.Preview> _carried = new Dictionary<ShortGuid, CompositePreviewTable.Preview>();

        //Arrivals that only the shipped table can vouch for, while it is still unread: settled when someone needs the
        //answer (a save, or a question about one), so adding a composite never reads the whole table on its own
        private static readonly HashSet<ShortGuid> _unsettled = new HashSet<ShortGuid>();

        //Decoded once per (composite, size) - UI thread only
        private static readonly Dictionary<ShortGuid, Dictionary<int, Image>> _images = new Dictionary<ShortGuid, Dictionary<int, Image>>();

        //The capture asked for at the last save: results are only taken from the request we made
        private static uint _pendingRequestId;
        private static string _pendingDir;
        private static uint _requestCounter = (uint)new Random().Next(1, int.MaxValue);

        /* An instanced save writes COMMANDS.PAK on a worker while the UI thread pumps, so a capture reply
           can land mid-save. Results that arrive then are held and applied once the save has finished,
           which keeps the table out of reach of the save's own OnSaveSuccess write. */
        private static bool _saveInProgress;
        private static readonly List<Packet> _deferred = new List<Packet>();

        /// <summary>
        /// Probe seam: when set, nothing here ever writes COMMANDS.PAK - not the save-time table write, not a
        /// capture result. GUI probes set it because they must never write a level file.
        /// </summary>
        internal static bool SuppressDiskWrites = false;

        /// <summary>The previews of these composites changed (taken again, or gone). Raised on the UI thread.</summary>
        public static event Action<IReadOnlyCollection<ShortGuid>> PreviewsChanged;

        /// <summary>The loaded level's own table. Probes fill it directly; the editor only ever fills it from capture results.</summary>
        internal static CompositePreviewTable User => _user;

        static CompositePreviewManager()
        {
            //Anything that changes what a composite looks like marks it for a new preview at the next save - and only
            //that: script logic, variables and proxies draw nothing (see CanChangeLook). A deletion is caught while it is
            //pending: by the time OnEntityDeleted fires the entity is out of its composite, and nothing here could say
            //which one that was.
            Singleton.OnEntityAdded += entity => { if (CanChangeLook(entity)) MarkDirty(entity); };
            Singleton.OnEntityDeletePending += (entity, composite) => { if (CanChangeLook(entity)) MarkDirty(composite); };
            Singleton.OnEntityMoved += (transform, entity) => MarkDirty(entity);
            Singleton.OnEntityParameterModified += (entity, parameter, removed) => { if (CanChangeLook(entity)) MarkDirty(entity); };
            Singleton.OnCompositeAdded += composite => MarkAdded(composite);
            Singleton.OnCompositeDeleted += composite => Forget(composite);
        }

        #region LEVEL
        /// <summary>Follow a loaded level: its table is read now and again on every load, and written on every save.</summary>
        public static void LinkCommands(LevelContent content)
        {
            if (_commands != null)
            {
                _commands.OnLoadSuccess -= LoadUserPreviews;
                _commands.OnSaveSuccess -= SaveUserPreviews;
            }

            _commands = content?.Level?.Commands;
            Reset();
            if (_commands == null)
                return;

            _commands.OnLoadSuccess += LoadUserPreviews;
            _commands.OnSaveSuccess += SaveUserPreviews;

            LoadUserPreviews(_commands.Filepath);
        }

        /* Everything that belonged to the previous level */
        private static void Reset()
        {
            _user = new CompositePreviewTable();
            _dirty.Clear();
            _removed.Clear();
            _carried.Clear();
            _unsettled.Clear();
            _deferred.Clear();
            DropCapture();
            DropImagesOnUiThread();
        }

        private static void LoadUserPreviews(string filepath)
        {
            CompositePreviewTable table = null;
            try
            {
                table = CustomTable.ReadTable(filepath, CustomTableType.COMPOSITE_PREVIEWS) as CompositePreviewTable;
            }
            catch (Exception e)
            {
                Debug.Log(LogSystem, "Could not read the level's preview table: " + e.Message);
            }
            _user = table ?? new CompositePreviewTable();
            _dirty.Clear();
            _removed.Clear();
            _carried.Clear();
            _unsettled.Clear();

            //A composite deleted and saved away by an earlier session left its preview behind: one ported in later under
            //that ID would be trusted to it, whatever it now holds
            int pruned = 0;
            if (_commands != null)
            {
                foreach (ShortGuid id in _user.previews.Keys.ToList())
                {
                    if (_commands.GetComposite(id) != null)
                        continue;
                    _user.previews.Remove(id);
                    pruned++;
                }
            }
            DropImagesOnUiThread();
            Debug.Log(LogSystem, "Loaded " + _user.Count + " composite previews from the level" + (pruned == 0 ? "" : " (dropped " + pruned + " of composites no longer in it)"));
        }

        /* A level loads on its own thread, and that is where its table is read. The cached previews are the
           UI thread's - made, handed out and copied there - so their disposal is handed to it as well; it is
           queued ahead of the panels the load goes on to build, which is what fills the cache again. */
        private static void DropImagesOnUiThread()
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor != null && !editor.IsDisposed && editor.IsHandleCreated && editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new Action(DropImages));
                    return;
                }
                catch (Exception e)
                {
                    Debug.Log(LogSystem, "Could not hand the image cache to the UI thread: " + e.Message);
                }
            }
            DropImages();
        }

        private static void SaveUserPreviews(string filepath)
        {
            if (SuppressDiskWrites)
            {
                Debug.Log(LogSystem, "Disk writes suppressed: not writing " + _user.Count + " composite previews on save");
                return;
            }
            //This runs inside the save's own success handlers: a table that cannot be written must not stop
            //the tables written after it, nor turn a save whose script is already on disk into a failed one
            try
            {
                CustomTable.WriteTable(filepath, CustomTableType.COMPOSITE_PREVIEWS, _user);
                Debug.Log(LogSystem, "Saved " + _user.Count + " composite previews with the level");
            }
            catch (Exception e)
            {
                Debug.Log(LogSystem, "Could not write the preview table with the level: " + e.Message);
            }
        }

        /* Written outside a save: after a capture reply has been folded into the table */
        private static void WriteUserTable()
        {
            if (SuppressDiskWrites)
            {
                Debug.Log(LogSystem, "Disk writes suppressed: not writing " + _user.Count + " composite previews");
                return;
            }
            if (_commands == null)
                return;
            try
            {
                CustomTable.WriteTable(_commands.Filepath, CustomTableType.COMPOSITE_PREVIEWS, _user);
                Debug.Log(LogSystem, "Wrote " + _user.Count + " composite previews to " + Path.GetFileName(_commands.Filepath));
            }
            catch (Exception e)
            {
                Debug.Log(LogSystem, "Could not write the preview table: " + e.Message);
            }
        }
        #endregion

        #region BAKED
        /* The shipped table, read from the same flowgraphs.dat beside the exe that the layout manager reads (and the
           same local override), the first time anyone asks. Null when there is no such file or table. */
        private static CompositePreviewTable Baked
        {
            get
            {
                if (_bakedLoaded)
                    return _baked;
                _bakedLoaded = true;
                try
                {
                    byte[] contentCompressed = FlowgraphLayoutManager.ReadShippedTables();
                    if (contentCompressed == null)
                    {
                        Debug.Log(LogSystem, "No baked composite previews are shipped (flowgraphs.dat is not beside OpenCAGE.exe)");
                        _baked = null;
                        return _baked;
                    }

                    byte[] content;
                    using (MemoryStream stream = new MemoryStream())
                    using (GZipStream compressedStream = new GZipStream(new MemoryStream(contentCompressed), CompressionMode.Decompress))
                    {
                        compressedStream.CopyTo(stream);
                        content = stream.ToArray();
                    }
                    _baked = CustomTable.ReadTable(content, CustomTableType.COMPOSITE_PREVIEWS) as CompositePreviewTable;
                    Debug.Log(LogSystem, _baked == null ? "No baked composite previews are shipped" : "Loaded " + _baked.Count + " baked composite previews");
                }
                catch (Exception e)
                {
                    Debug.Log(LogSystem, "Could not read the baked composite previews: " + e.Message);
                    _baked = null;
                }
                return _baked;
            }
        }

        /// <summary>How many previews the shipped table holds (0 when there is none).</summary>
        public static int BakedCount => Baked?.Count ?? 0;
        #endregion

        #region LOOKUP
        /// <summary>
        /// The PNG of a composite's preview: the level's own first, else the shipped one. False when there is
        /// none, including when the level's table says the composite draws nothing now.
        /// </summary>
        public static bool TryGetPreviewPng(ShortGuid id, out byte[] png)
        {
            png = null;
            if (_user.previews.TryGetValue(id, out CompositePreviewTable.Preview user))
            {
                if (user.png_gzip == null || user.png_gzip.Length == 0)
                    return false;
                png = _user.GetPreviewPng(id);
                return png != null && png.Length != 0;
            }

            CompositePreviewTable baked = Baked;
            if (baked == null || !baked.previews.TryGetValue(id, out CompositePreviewTable.Preview shipped))
                return false;
            if (shipped.png_gzip == null || shipped.png_gzip.Length == 0)
                return false;
            png = baked.GetPreviewPng(id);
            return png != null && png.Length != 0;
        }

        public static bool HasPreview(ShortGuid id)
        {
            if (_user.previews.TryGetValue(id, out CompositePreviewTable.Preview user))
                return user.png_gzip != null && user.png_gzip.Length != 0;
            CompositePreviewTable baked = Baked;
            return baked != null && baked.previews.TryGetValue(id, out CompositePreviewTable.Preview shipped)
                && shipped.png_gzip != null && shipped.png_gzip.Length != 0;
        }

        /// <summary>
        /// A composite's preview as a square image of this size, decoded once and kept. Null when it has none.
        /// UI thread only. The image is the cache's: copy it rather than dispose it.
        /// </summary>
        public static Image GetPreviewImage(ShortGuid id, int size)
        {
            if (size <= 0)
                return null;
            if (_images.TryGetValue(id, out Dictionary<int, Image> sizes) && sizes.TryGetValue(size, out Image cached))
                return cached;

            if (!TryGetPreviewPng(id, out byte[] png))
                return null;

            Image image = Decode(png, size);
            if (image == null)
                return null;

            if (sizes == null)
            {
                sizes = new Dictionary<int, Image>();
                _images[id] = sizes;
            }
            sizes[size] = image;
            return image;
        }

        /// <summary>
        /// A composite's preview at the size it was captured - 128 px for the shipped previews, 256 px for those
        /// retaken when the level is saved - pixel for pixel, decoded once and kept. Null when it has none. UI
        /// thread only. The image is the cache's: copy it rather than dispose it.
        /// </summary>
        public static Image GetPreviewImageAsCaptured(ShortGuid id)
        {
            if (_images.TryGetValue(id, out Dictionary<int, Image> sizes) && sizes.TryGetValue(AsCaptured, out Image cached))
                return cached;

            if (!TryGetPreviewPng(id, out byte[] png))
                return null;

            Image image = DecodeAsCaptured(png);
            if (image == null)
                return null;

            if (sizes == null)
            {
                sizes = new Dictionary<int, Image>();
                _images[id] = sizes;
            }
            sizes[AsCaptured] = image;
            return image;
        }

        //Where the cache keeps a preview at the size it was captured: never a size anyone asks GetPreviewImage for
        private const int AsCaptured = 0;

        /// <summary>
        /// A composite's preview decoded afresh for the caller, who owns it. Nothing is kept here: a list
        /// that previews a whole level as it scrolls would otherwise leave a copy of every capture it ever
        /// showed in the cache above, on top of the ones it holds itself. Null when there is none.
        /// </summary>
        public static Image DecodePreviewImage(ShortGuid id, int size)
        {
            if (size <= 0 || !TryGetPreviewPng(id, out byte[] png))
                return null;
            return Decode(png, size);
        }

        /* A Bitmap made straight from a stream keeps that stream for as long as it lives, so the decoded
           preview is drawn into a fresh square and the decode dropped */
        private static Image Decode(byte[] png, int size)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(png))
                using (Image decoded = Image.FromStream(stream))
                {
                    Bitmap square = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(square))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.DrawImage(decoded, Fit(decoded.Size, size));
                    }
                    return square;
                }
            }
            catch (Exception e)
            {
                Debug.Log(LogSystem, "Could not decode a preview: " + e.Message);
                return null;
            }
        }

        /* The decoded PNG copied out unscaled (see Decode for why it is copied) */
        private static Image DecodeAsCaptured(byte[] png)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(png))
                using (Image decoded = Image.FromStream(stream))
                {
                    Bitmap copy = new Bitmap(decoded.Width, decoded.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(copy))
                    {
                        //A straight copy: same size, source pixels replace the empty ones rather than blend over them
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(decoded, new Rectangle(0, 0, decoded.Width, decoded.Height), 0, 0, decoded.Width, decoded.Height, GraphicsUnit.Pixel);
                    }
                    return copy;
                }
            }
            catch (Exception e)
            {
                Debug.Log(LogSystem, "Could not decode a preview: " + e.Message);
                return null;
            }
        }

        /* Scaled to fit the square, centred, keeping its shape - the viewer's previews are square already */
        private static Rectangle Fit(Size source, int size)
        {
            if (source.Width <= 0 || source.Height <= 0)
                return new Rectangle(0, 0, size, size);
            float scale = Math.Min((float)size / source.Width, (float)size / source.Height);
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            return new Rectangle((size - width) / 2, (size - height) / 2, width, height);
        }

        private static void DropImages()
        {
            foreach (Dictionary<int, Image> sizes in _images.Values)
                foreach (Image image in sizes.Values)
                    image.Dispose();
            _images.Clear();
        }

        private static void DropImages(IEnumerable<ShortGuid> ids)
        {
            foreach (ShortGuid id in ids)
            {
                if (!_images.TryGetValue(id, out Dictionary<int, Image> sizes))
                    continue;
                foreach (Image image in sizes.Values)
                    image.Dispose();
                _images.Remove(id);
            }
        }

        /// <summary>The level's own entry for a composite (a copy), for carrying across to another level. Null when it has none.</summary>
        public static CompositePreviewTable.Preview GetUserPreview(ShortGuid id)
        {
            if (!_user.previews.TryGetValue(id, out CompositePreviewTable.Preview preview) || preview.png_gzip == null)
                return null;
            return new CompositePreviewTable.Preview() { captured_at = preview.captured_at, png_gzip = preview.png_gzip };
        }
        #endregion

        #region CHANGE TRACKING
        public static bool IsDirty(ShortGuid id)
        {
            if (_unsettled.Contains(id))
                Settle();
            return _dirty.Contains(id);
        }
        public static int DirtyCount
        {
            get
            {
                Settle();
                return _dirty.Count;
            }
        }

        private static void MarkDirty(Composite composite)
        {
            if (composite == null)
                return;
            _dirty.Add(composite.shortGUID);
        }

        /// <summary>A composite changed by something that raises no entity events (an AI assistant's edit): new preview at the next save.</summary>
        internal static void MarkEdited(Composite composite) => MarkDirty(composite);

        /// <summary>
        /// Whether a change to this entity can change what its composite's preview shows: a composite instance, a function
        /// entity placed somewhere (models, lights, triggers - anything with a position), or an alias (it can override
        /// either). Script logic, variables and proxies draw nothing, so editing them never retakes a preview.
        /// </summary>
        internal static bool CanChangeLook(Entity entity)
        {
            switch (entity?.variant)
            {
                case EntityVariant.FUNCTION:
                    FunctionEntity function = (FunctionEntity)entity;
                    return !function.function.IsFunctionType || ViewerFunctionDrop.HasPosition(function.function.AsFunctionType);
                case EntityVariant.ALIAS:
                    return true;
            }
            return false;
        }

        /// <summary>
        /// A port is about to bring this composite in from a level whose own table (<paramref name="source"/>) may hold a
        /// preview of it. That preview shows what is arriving, so it comes too rather than the composite being taken again.
        /// Call before raising OnCompositeAdded for it; a source with no preview of it changes nothing.
        /// </summary>
        internal static void CarryPreview(ShortGuid id, CompositePreviewTable source)
        {
            if (source == null || !source.previews.TryGetValue(id, out CompositePreviewTable.Preview preview) || preview == null)
                return;
            _carried[id] = new CompositePreviewTable.Preview() { captured_at = preview.captured_at, png_gzip = preview.png_gzip };
        }

        /// <summary>
        /// For a level built on disk (Create Level): a composite ported into it takes its source level's own preview, or
        /// none (the shipped one stands) when the source had none - a later pick porting the same ID over it replaces it.
        /// </summary>
        internal static void CarryInto(CompositePreviewTable into, ShortGuid id, CompositePreviewTable source)
        {
            if (into == null)
                return;
            if (source != null && source.previews.TryGetValue(id, out CompositePreviewTable.Preview preview) && preview != null)
                into.previews[id] = new CompositePreviewTable.Preview() { captured_at = preview.captured_at, png_gzip = preview.png_gzip };
            else
                into.previews.Remove(id);
        }

        /* A composite has arrived in the level. A new one (made here) has no preview and needs one. One brought in by a
           port keeps its ID: when its source level had its own preview of it, that comes across with it; otherwise the
           level or the shipped table already holds one for that ID - the same composite, so there is nothing to retake -
           unless it replaced a different composite of that ID this session (a port overwriting it), whose preview showed
           the old content. An undo bringing back a deleted composite gets the same object back, with its preview and
           any recapture it was waiting for. */
        private static void MarkAdded(Composite composite)
        {
            if (composite == null)
                return;
            ShortGuid id = composite.shortGUID;
            _removed.TryGetValue(id, out Removal gone);
            _removed.Remove(id);

            if (_carried.TryGetValue(id, out CompositePreviewTable.Preview carried))
            {
                _carried.Remove(id);
                _user.previews[id] = carried;
                _dirty.Remove(id);
                _unsettled.Remove(id);
                ChangedDuringPort(id);
                return;
            }
            if (gone != null)
            {
                if (!gone.Composite.TryGetTarget(out Composite old) || !ReferenceEquals(old, composite))
                {
                    MarkDirty(composite);
                    return;
                }
                if (gone.UserEntry != null)
                {
                    _user.previews[id] = gone.UserEntry;
                    ChangedDuringPort(id);
                }
                if (gone.WasDirty)
                {
                    MarkDirty(composite);
                    return;
                }
            }
            if (_user.previews.ContainsKey(id))
                return;
            if (!_bakedLoaded)
            {
                _unsettled.Add(id);
                return;
            }
            if (!KnowsPreview(id))
                MarkDirty(composite);
        }

        /* The level's table or the shipped one has an entry for this ID - a preview, or the verdict that it draws nothing */
        private static bool KnowsPreview(ShortGuid id)
        {
            if (_user.previews.ContainsKey(id))
                return true;
            CompositePreviewTable baked = Baked;
            return baked != null && baked.previews.ContainsKey(id);
        }

        /* A composite's preview changed while a port or an undo is bringing composites in: the cached pictures and the tree
           lists are put right at once (cheap), and the listeners - which rebuild whole lists - hear once, after it */
        private static readonly HashSet<ShortGuid> _changedLater = new HashSet<ShortGuid>();
        private static void ChangedDuringPort(ShortGuid id)
        {
            DropImages(new[] { id });
            CompositePreviewImages.Refresh(new[] { id });
            if (!_changedLater.Add(id) || _changedLater.Count != 1)
                return;
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed || !editor.IsHandleCreated)
            {
                FlushChangedLater();
                return;
            }
            editor.BeginInvoke(new Action(FlushChangedLater));
        }

        private static void FlushChangedLater()
        {
            if (_changedLater.Count == 0)
                return;
            List<ShortGuid> ids = _changedLater.ToList();
            _changedLater.Clear();
            PreviewsChanged?.Invoke(ids);
        }

        /* Arrivals left for the shipped table to vouch for are judged now, reading it if it has not been read */
        private static void Settle()
        {
            if (_unsettled.Count == 0)
                return;
            foreach (ShortGuid id in _unsettled)
                if (!KnowsPreview(id))
                    _dirty.Add(id);
            _unsettled.Clear();
        }

        private static void MarkDirty(Entity entity)
        {
            Composite composite = CompositeOf(entity);
            if (composite != null)
                MarkDirty(composite);
        }

        /* Events that carry only an entity almost always mean the open composite; anything else (an undo
           of an edit made elsewhere) is found by looking, which is rare enough to afford */
        private static Composite CompositeOf(Entity entity)
        {
            if (entity == null)
                return null;
            Composite open = Singleton.Editor?.CompositeDisplay?.Composite;
            if (open != null && open.GetEntityByID(entity.shortGUID) == entity)
                return open;
            if (_commands == null)
                return null;
            return _commands.Entries.FirstOrDefault(o => o != null && o.GetEntityByID(entity.shortGUID) == entity);
        }

        /* Its level preview goes with it: a save while it is gone must not keep a picture for an ID nothing in the level
           has, which a later port under that ID would be trusted to */
        private static void Forget(Composite composite)
        {
            if (composite == null)
                return;
            ShortGuid id = composite.shortGUID;
            _user.previews.TryGetValue(id, out CompositePreviewTable.Preview userEntry);
            _user.previews.Remove(id);
            _unsettled.Remove(id);
            _removed[id] = new Removal()
            {
                Composite = new WeakReference<Composite>(composite),
                WasDirty = _dirty.Remove(id),
                UserEntry = userEntry,
            };
            DropImages(new[] { id });
            //The lists the trees draw from would go on showing the level's picture of it
            if (userEntry != null)
                CompositePreviewImages.Refresh(new[] { id });
        }
        #endregion

        #region SAVE
        /// <summary>
        /// Called just before the level is written: asks the viewer for new previews of every composite edited
        /// since its last one. Nothing is asked for without a viewer; those composites simply stay marked for
        /// the next save.
        /// </summary>
        public static void BeginSave()
        {
            _saveInProgress = true;
            //Without a viewer nothing is taken, so arrivals waiting on the shipped table wait on: no reason to read it now
            if (!Send.Connected)
            {
                Debug.Log(LogSystem, "No viewer connected: " + _dirty.Count + " composite(s) keep their old previews until the next save");
                return;
            }
            Settle();
            if (_dirty.Count == 0)
            {
                Debug.Log(LogSystem, "No composite has changed since its preview was taken");
                return;
            }

            //A request still open from the last save never answered: its folder goes, and so does it
            if (_pendingRequestId != 0)
            {
                Debug.Log(LogSystem, "The previous capture request (" + _pendingRequestId + ") was never answered; dropping it");
                DropCapture();
            }

            string dir;
            try
            {
                dir = Path.Combine(Path.GetTempPath(), "OpenCAGE", "previews", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
            }
            catch (Exception e)
            {
                Debug.Log(LogSystem, "Could not make a folder for the viewer's previews: " + e.Message);
                return;
            }

            uint requestId = ++_requestCounter;
            if (requestId == 0) requestId = ++_requestCounter;
            ExpectCapture(requestId, dir);
            List<uint> ids = _dirty.Select(o => o.AsUInt32).ToList();
            Send.SendPreviewCaptureRequest(ids, dir, requestId);
            Debug.Log(LogSystem, "Asked the viewer for " + ids.Count + " composite preview(s) (request " + requestId + ") into " + dir);
        }

        /// <summary>The save is over (however it ended): results held back during it are folded in now.</summary>
        public static void EndSave()
        {
            _saveInProgress = false;
            if (_deferred.Count == 0)
                return;
            List<Packet> deferred = new List<Packet>(_deferred);
            _deferred.Clear();
            foreach (Packet packet in deferred)
                ApplyCaptureResults(packet);
        }

        /// <summary>The request whose reply the next COMPOSITE_PREVIEW_CAPTURED is taken from, and where its files land.</summary>
        internal static void ExpectCapture(uint requestId, string dir)
        {
            _pendingRequestId = requestId;
            _pendingDir = dir;
        }

        private static void DropCapture()
        {
            _pendingRequestId = 0;
            if (_pendingDir != null)
            {
                try { if (Directory.Exists(_pendingDir)) Directory.Delete(_pendingDir, true); }
                catch (Exception e) { Debug.Log(LogSystem, "Could not remove " + _pendingDir + ": " + e.Message); }
            }
            _pendingDir = null;
        }

        /// <summary>Every COMPOSITE_PREVIEW_CAPTURED packet, whoever asked (on the UI thread): captures requested outside the
        /// save-time batch (the MCP fresh preview) learn from it that the viewer refused or failed.</summary>
        public static event Action<Packet> CaptureAnswered;

        /// <summary>A COMPOSITE_PREVIEW_CAPTURED packet from the viewer (on the UI thread, from the dispatcher).</summary>
        public static void OnCaptured(Packet packet)
        {
            if (packet == null)
                return;
            CaptureAnswered?.Invoke(packet);
            if (_saveInProgress)
            {
                Debug.Log(LogSystem, "Capture results arrived mid-save; holding them until it finishes");
                _deferred.Add(packet);
                return;
            }
            ApplyCaptureResults(packet);
        }

        private static void ApplyCaptureResults(Packet packet)
        {
            if (_pendingRequestId == 0 || packet.preview_request_id != _pendingRequestId)
            {
                Debug.Log(LogSystem, "Ignoring capture results for request " + packet.preview_request_id + " (expecting " + _pendingRequestId + ")");
                return;
            }

            int now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            List<ShortGuid> changed = new List<ShortGuid>();
            int captured = 0, empty = 0, failed = 0;
            foreach (CompositePreviewResult result in packet.preview_results ?? new List<CompositePreviewResult>())
            {
                if (result == null)
                    continue;
                ShortGuid id = new ShortGuid(result.composite);
                switch ((CompositePreviewStatus)result.status)
                {
                    case CompositePreviewStatus.Captured:
                    case CompositePreviewStatus.SkippedExisting:
                        {
                            byte[] png = null;
                            try
                            {
                                if (!string.IsNullOrEmpty(result.file) && File.Exists(result.file))
                                    png = File.ReadAllBytes(result.file);
                            }
                            catch (Exception e)
                            {
                                Debug.Log(LogSystem, "Could not read " + result.file + ": " + e.Message);
                            }
                            if (png == null || png.Length == 0)
                            {
                                Debug.Log(LogSystem, "The viewer reported a preview of " + id.ToByteString() + " but " + (result.file ?? "") + " is not there; it stays marked");
                                failed++;
                                break;
                            }
                            if (KeepForDeleted(id, png, now))
                                captured++;
                            else
                            {
                                _user.SetPreview(id, png, now);
                                _dirty.Remove(id);
                                changed.Add(id);
                                captured++;
                            }
                            try { File.Delete(result.file); } catch { }
                        }
                        break;
                    case CompositePreviewStatus.Empty:
                        if (KeepForDeleted(id, new byte[0], now))
                        {
                            empty++;
                            break;
                        }
                        //Draws nothing: remembered as such, so the shipped preview (of what it used to be) is not shown for it
                        _user.previews[id] = new CompositePreviewTable.Preview() { captured_at = now, png_gzip = new byte[0] };
                        _dirty.Remove(id);
                        changed.Add(id);
                        empty++;
                        break;
                    case CompositePreviewStatus.Failed:
                        Debug.Log(LogSystem, "The viewer could not capture " + id.ToByteString() + " (see its log); it stays marked for the next save");
                        failed++;
                        break;
                    case CompositePreviewStatus.Unknown:
                        Debug.Log(LogSystem, "The viewer has no composite " + id.ToByteString() + "; it stays marked for the next save");
                        failed++;
                        break;
                    default:
                        Debug.Log(LogSystem, "Unrecognised capture status " + result.status + " for " + id.ToByteString());
                        failed++;
                        break;
                }
            }
            Debug.Log(LogSystem, "Capture request " + packet.preview_request_id + " answered: " + captured + " captured, " + empty + " empty, " + failed + " not taken");

            DropCapture();
            if (changed.Count != 0)
                NotifyPreviewsChanged(changed);
            if (captured != 0 || empty != 0)
                WriteUserTable();

            //The viewer captured each composite by opening it on its own, rebuilding its scene each time, and has
            //put the composite that was on screen back - but not what was selected in it. Sent again, as a
            //settings change would be, so the selection and the stepped-into instance come back too.
            Send.SendReSyncPacket();
        }

        /* A capture of a composite deleted while the viewer worked: it goes with the deletion (an undo brings it back,
           taken), not into the table a save writes. A composite that is gone with no undo to bring it back is dropped. */
        private static bool KeepForDeleted(ShortGuid id, byte[] png, int now)
        {
            if (_removed.TryGetValue(id, out Removal removal))
            {
                CompositePreviewTable taken = new CompositePreviewTable();
                if (png.Length == 0)
                    taken.previews[id] = new CompositePreviewTable.Preview() { captured_at = now, png_gzip = new byte[0] };
                else
                    taken.SetPreview(id, png, now);
                removal.UserEntry = taken.previews[id];
                removal.WasDirty = false;
                return true;
            }
            if (_commands != null && _commands.GetComposite(id) == null)
            {
                Debug.Log(LogSystem, "The viewer captured " + id.ToByteString() + ", which is no longer in the level; dropped");
                return true;
            }
            return false;
        }

        /// <summary>These composites' previews are different now: cached images go, and listeners redraw.</summary>
        internal static void NotifyPreviewsChanged(IReadOnlyCollection<ShortGuid> ids)
        {
            if (ids == null || ids.Count == 0)
                return;
            DropImages(ids);
            //The lists the trees draw from are put right first: a listener that rebuilds its tree on this
            //event reads them, and must not find the previews from before
            CompositePreviewImages.Refresh(ids);
            PreviewsChanged?.Invoke(ids);
        }
        #endregion
    }
}
