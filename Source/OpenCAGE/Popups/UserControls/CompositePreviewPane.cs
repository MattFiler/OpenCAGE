using CATHODE.Scripting;
using OpenCAGE.Theming;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    /// <summary>
    /// A composite's stored preview (see <see cref="CompositePreviewManager"/>) shown larger than the trees and the
    /// Composite Browser show it, for choosing a composite: its name and folder at the top, and the preview under them
    /// at the size it was captured - 128 px for the shipped previews, 256 px for those retaken when a level is saved.
    /// It is never stretched: in a pane smaller than that it is shrunk to fit, keeping its shape. A composite without
    /// a preview, a folder, or nothing selected gets a line saying so.
    /// </summary>
    public class CompositePreviewPane : UserControl
    {
        //Around the edge, and between the name, the preview and the note
        private const int Gap = 8;

        //The square a missing preview is drawn as: the size the shipped ones were captured at
        private const int PlaceholderSize = 128;

        private Composite _composite;
        private string _title;
        private string _subtitle;
        private string _message;
        private Font _titleFont;

        //A composite of a level that is not the loaded one (see ShowOtherLevelComposite): its preview is the caller's
        private bool _otherLevel;
        private Image _otherPreview;
        private bool _otherDrawsNothing;

        public CompositePreviewPane()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            //Only shows things: tabbing passes it by
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            AccessibleRole = AccessibleRole.Graphic;
            AccessibleName = "Composite preview";

            CompositePreviewManager.PreviewsChanged += OnPreviewsChanged;
            ThemeManager.ThemeChanged += OnThemeChanged;
            ShowComposite(null);
        }

        /// <summary>Show this composite's preview. Null (nothing selected) clears it.</summary>
        public void ShowComposite(Composite composite)
        {
            ForgetOtherLevel();
            _composite = composite;
            _message = null;
            if (composite == null)
            {
                _title = null;
                _subtitle = null;
                _message = "Select a composite to see its preview.";
            }
            else
            {
                string path = string.IsNullOrWhiteSpace(composite.name) ? "(Root)" : composite.name.Replace('\\', '/');
                SplitPath(path, out _title, out _subtitle);
                //Decoded here rather than at the next paint, so it is part of the selection change; the manager keeps it
                CompositePreviewManager.GetPreviewImageAsCaptured(composite.shortGUID);
            }
            AccessibleDescription = _title;
            Invalidate();
        }

        /// <summary>A folder is selected: its name, and a line saying to pick a composite in it.</summary>
        public void ShowFolder(string path)
        {
            ForgetOtherLevel();
            _composite = null;
            SplitPath((path ?? "").Replace('\\', '/').TrimEnd('/'), out _title, out _subtitle);
            _message = "A folder. Select a composite in it to see its preview.";
            AccessibleDescription = _title;
            Invalidate();
        }

        /// <summary>
        /// A composite of a level other than the loaded one, which a window lists without loading it (Import Composites From
        /// Level): its name, and the preview that level holds for it, which the caller found and keeps (null: it has none;
        /// <paramref name="drawsNothing"/>: that level's own table says it draws nothing). The loaded level's previews are
        /// not asked - the same ID there can be a different composite.
        /// </summary>
        public void ShowOtherLevelComposite(string name, Image preview, bool drawsNothing)
        {
            _composite = null;
            _message = null;
            _otherLevel = true;
            _otherPreview = preview;
            _otherDrawsNothing = drawsNothing;
            SplitPath(string.IsNullOrWhiteSpace(name) ? "(Root)" : name.Replace('\\', '/'), out _title, out _subtitle);
            AccessibleDescription = _title;
            Invalidate();
        }

        private void ForgetOtherLevel()
        {
            _otherLevel = false;
            _otherPreview = null;
            _otherDrawsNothing = false;
        }

        /// <summary>
        /// Whatever a composite tree (see <see cref="TreeUtility"/>) has selected: a composite's preview, a folder, or
        /// nothing. The pickers that show this pane beside their tree call it as the selection moves.
        /// </summary>
        public void ShowTreeNode(TreeNode node, CATHODE.Commands commands)
        {
            if (!(node?.Tag is TreeItem item))
                ShowComposite(null);
            else if (item.Item_Type == TreeItemType.DIRECTORY)
                ShowFolder(item.String_Value);
            else
                ShowComposite(commands?.GetComposite(item.String_Value));
        }

        /* "A/B/C" -> "C" over "A/B" */
        private static void SplitPath(string path, out string leaf, out string folder)
        {
            int slash = path.LastIndexOf('/');
            leaf = slash < 0 ? path : path.Substring(slash + 1);
            folder = slash < 0 ? "" : path.Substring(0, slash);
        }

        #region PAINT
        /* The tree beside it: the pane reads as the list's companion, and the previews sit on the same ground they do
           in the Composite Browser's list */
        private Color Ground => ThemeManager.IsDark ? ThemeColours.Input : SystemColors.Window;
        private Color Ink => ThemeManager.IsDark ? ThemeColours.Text : SystemColors.WindowText;
        private Color DimInk => ThemeManager.IsDark ? ThemeColours.TextDim : SystemColors.GrayText;
        private Color Edge => ThemeManager.IsDark ? ThemeColours.Border : SystemColors.ControlDark;
        private Color DashEdge => ThemeManager.IsDark ? ThemeColours.BorderStrong : SystemColors.ControlDark;

        private Font TitleFont => _titleFont ?? (_titleFont = new Font(Font, FontStyle.Bold));

        protected override void OnFontChanged(EventArgs e)
        {
            _titleFont?.Dispose();
            _titleFont = null;
            base.OnFontChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ground);
            using (Pen edge = new Pen(Edge))
                g.DrawRectangle(edge, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

            Rectangle area = Rectangle.Inflate(ClientRectangle, -Gap, -Gap);
            if (area.Width <= 0 || area.Height <= 0)
                return;

            //The name, and the folder it is in, one line each - the field under the list spells the whole path
            const TextFormatFlags line = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            int top = area.Top;
            if (_title != null)
            {
                int height = TextRenderer.MeasureText(g, "Ag", TitleFont, area.Size, line).Height;
                TextRenderer.DrawText(g, _title, TitleFont, new Rectangle(area.Left, top, area.Width, height), Ink, line);
                top += height;
                if (!string.IsNullOrEmpty(_subtitle))
                {
                    height = TextRenderer.MeasureText(g, "Ag", Font, area.Size, line).Height;
                    TextRenderer.DrawText(g, _subtitle, Font, new Rectangle(area.Left, top + 2, area.Width, height), DimInk, line);
                    top += height + 2;
                }
                top += Gap;
            }
            Rectangle body = new Rectangle(area.Left, top, area.Width, area.Bottom - top);
            if (body.Height <= 0)
                return;

            if (_otherLevel)
            {
                //Nothing here knows whether it was edited since: its own level's preview is all there is
                if (_otherPreview != null)
                    DrawPreview(g, _otherPreview, body);
                else
                    DrawMissing(g, body, "No preview", _otherDrawsNothing
                        ? "Composites with nothing to draw, such as scripts and lights, have none."
                        : "Composites with nothing to draw, such as scripts and lights, have none. Others get one when their level is saved with the Level Viewer open.");
                return;
            }

            if (_composite == null)
            {
                DrawMessage(g, _message, body);
                return;
            }

            bool changed = CompositePreviewManager.IsDirty(_composite.shortGUID);
            Image image = CompositePreviewManager.GetPreviewImageAsCaptured(_composite.shortGUID);
            if (image == null)
            {
                //Edited since its preview was taken, or new: the viewer takes one when the level is saved
                DrawMissing(g, body, changed ? "No preview yet" : "No preview",
                    changed ? "One is taken when the level is saved with the Level Viewer open."
                            : "Composites with nothing to draw, such as scripts and lights, have none.");
                return;
            }

            if (changed)
            {
                Rectangle note = DrawNoteAtBottom(g, body, "Changed since this preview was taken. Saving the level with the Level Viewer open takes a new one.");
                body.Height = Math.Max(0, note.Top - Gap - body.Top);
            }
            DrawPreview(g, image, body);
        }

        /* At the size it was captured, centred; only ever shrunk, keeping its shape, when the room is smaller */
        private static void DrawPreview(Graphics g, Image image, Rectangle room)
        {
            if (room.Width <= 0 || room.Height <= 0 || image.Width <= 0 || image.Height <= 0)
                return;

            float scale = Math.Min(1f, Math.Min((float)room.Width / image.Width, (float)room.Height / image.Height));
            int width = Math.Max(1, (int)Math.Floor(image.Width * scale));
            int height = Math.Max(1, (int)Math.Floor(image.Height * scale));
            Rectangle target = new Rectangle(room.Left + (room.Width - width) / 2, room.Top + (room.Height - height) / 2, width, height);

            InterpolationMode interpolation = g.InterpolationMode;
            PixelOffsetMode offset = g.PixelOffsetMode;
            if (scale >= 1f)
            {
                //Pixel for pixel
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
            }
            else
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            }
            g.DrawImage(image, target, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
            g.InterpolationMode = interpolation;
            g.PixelOffsetMode = offset;
        }

        /* An outlined square where the preview would be, saying there is none, and why underneath */
        private void DrawMissing(Graphics g, Rectangle body, string heading, string reason)
        {
            const TextFormatFlags wrap = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;
            int reasonHeight = TextRenderer.MeasureText(g, reason, Font, new Size(body.Width, int.MaxValue), wrap).Height;

            int size = Math.Min(PlaceholderSize, Math.Min(body.Width, body.Height - reasonHeight - Gap));
            int headingHeight = TextRenderer.MeasureText(g, heading, Font, new Size(body.Width, int.MaxValue), wrap).Height;
            if (size < headingHeight + 2 * Gap)
            {
                //No room for the square: the words alone
                DrawMessage(g, heading + "\n" + reason, body);
                return;
            }

            int top = body.Top + (body.Height - (size + Gap + reasonHeight)) / 2;
            Rectangle square = new Rectangle(body.Left + (body.Width - size) / 2, top, size, size);
            using (Pen dash = new Pen(DashEdge) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(dash, square.Left, square.Top, square.Width - 1, square.Height - 1);
            TextRenderer.DrawText(g, heading, Font, square, DimInk, wrap | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, reason, Font, new Rectangle(body.Left, square.Bottom + Gap, body.Width, reasonHeight), DimInk, wrap);
        }

        private void DrawMessage(Graphics g, string text, Rectangle body)
        {
            if (string.IsNullOrEmpty(text))
                return;
            TextRenderer.DrawText(g, text, Font, body, DimInk,
                TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /* Wrapped to the pane's width at the bottom of the body; returns where it went */
        private Rectangle DrawNoteAtBottom(Graphics g, Rectangle body, string text)
        {
            const TextFormatFlags wrap = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;
            int height = Math.Min(body.Height, TextRenderer.MeasureText(g, text, Font, new Size(body.Width, int.MaxValue), wrap).Height);
            Rectangle note = new Rectangle(body.Left, body.Bottom - height, body.Width, height);
            TextRenderer.DrawText(g, text, Font, note, DimInk, wrap);
            return note;
        }
        #endregion

        /* A save retakes previews while the window is open: the one on show is drawn again (the manager has dropped
           its old copy, so the paint decodes the new one) */
        private void OnPreviewsChanged(IReadOnlyCollection<ShortGuid> ids)
        {
            if (IsDisposed || _composite == null)
                return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => OnPreviewsChanged(ids))); }
                catch (InvalidOperationException) { }
                return;
            }

            foreach (ShortGuid id in ids)
            {
                if (id != _composite.shortGUID)
                    continue;
                Invalidate();
                return;
            }
        }

        private void OnThemeChanged()
        {
            if (!IsDisposed)
                Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CompositePreviewManager.PreviewsChanged -= OnPreviewsChanged;
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _titleFont?.Dispose();
                _titleFont = null;
            }
            base.Dispose(disposing);
        }
    }
}
