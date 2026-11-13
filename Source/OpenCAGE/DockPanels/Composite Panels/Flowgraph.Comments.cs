using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Comment boxes on a page: Add Comment on the canvas menu draws one from where the menu was opened to a click, then asks
    /// for its title. The box's own menu renames, recolours or deletes it. Every change is one undo step; the boxes are saved
    /// with the page's layout (FlowgraphComments).
    /// </summary>
    public partial class Flowgraph
    {
        private ToolStripSeparator _commentSeparator;
        private ToolStripMenuItem _addCommentItem;
        private ToolStripMenuItem _renameCommentItem;
        private ToolStripMenuItem _commentColourItem;
        private ToolStripMenuItem _deleteCommentItem;
        private ToolStripMenuItem _deleteCommentAndContentsItem;
        //What the menu was opened over: the box (or null), and where on the canvas
        private STNodeComment _menuComment;
        private PointF _commentMenuAt;
        private RenameGeneric _commentPrompt;

        private void SetUpComments()
        {
            _commentSeparator = new ToolStripSeparator();
            _addCommentItem = new ToolStripMenuItem("Add Comment", AddCommentIcon(), (s, e) => stNodeEditor1.BeginCommentPlacement(_commentMenuAt))
            {
                ToolTipText = "Draw a comment box from here: click to place it, Escape to cancel",
            };
            _renameCommentItem = new ToolStripMenuItem("Rename Comment", CopyImage(renameFGToolStripMenuItem.Image), (s, e) => PromptCommentTitle(_menuComment, false));
            _commentColourItem = new ToolStripMenuItem("Comment Colour");
            foreach ((string name, Color colour) in FlowgraphComments.Presets)
            {
                Color chosen = colour;
                _commentColourItem.DropDownItems.Add(new ToolStripMenuItem(name, Swatch(colour.IsEmpty ? stNodeEditor1.CommentColor : colour), (s, e) => SetCommentColour(_menuComment, chosen)) { Tag = colour });
            }
            _deleteCommentItem = new ToolStripMenuItem("Delete Comment", CopyImage(deleteToolStripMenuItem.Image), (s, e) => DeleteComment(_menuComment, false))
            {
                ToolTipText = "Remove the box; what is inside it stays",
            };
            _deleteCommentAndContentsItem = new ToolStripMenuItem("Delete Comment And Contents", CopyImage(deleteToolStripMenuItem.Image), (s, e) => DeleteComment(_menuComment, true))
            {
                ToolTipText = "Remove the box and every node and box inside it",
            };

            int at = nodeContextMenu.Items.IndexOf(arrangeSelectedToolStripMenuItem) + 1;
            ToolStripItem[] items = { _commentSeparator, _addCommentItem, _renameCommentItem, _commentColourItem, _deleteCommentItem, _deleteCommentAndContentsItem };
            for (int i = 0; i < items.Length; i++)
                nodeContextMenu.Items.Insert(at + i, items[i]);
            nodeContextMenu.Opening += CommentMenu_Opening;

            stNodeEditor1.CommentPlaced += StNodeEditor1_CommentPlaced;
            stNodeEditor1.CommentChanged += StNodeEditor1_CommentChanged;
            stNodeEditor1.CommentTitleDoubleClicked += StNodeEditor1_CommentTitleDoubleClicked;
            this.FormClosed += (s, e) =>
            {
                stNodeEditor1.CommentPlaced -= StNodeEditor1_CommentPlaced;
                stNodeEditor1.CommentChanged -= StNodeEditor1_CommentChanged;
                stNodeEditor1.CommentTitleDoubleClicked -= StNodeEditor1_CommentTitleDoubleClicked;
                nodeContextMenu.Opening -= CommentMenu_Opening;
                _commentPrompt?.Close();
            };
        }

        private static Image CopyImage(Image image) => image == null ? null : new Bitmap(image);

        //A box with a title bar, for Add Comment
        private static Image AddCommentIcon()
        {
            Bitmap icon = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(icon))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(90, 104, 118, 138)))
            using (SolidBrush bar = new SolidBrush(Color.FromArgb(104, 118, 138)))
            using (Pen edge = new Pen(Color.FromArgb(70, 84, 104)))
            {
                g.FillRectangle(fill, 1, 2, 14, 12);
                g.FillRectangle(bar, 1, 2, 14, 4);
                g.DrawRectangle(edge, 1, 2, 13, 11);
            }
            return icon;
        }

        private static Image Swatch(Color colour)
        {
            Bitmap swatch = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(swatch))
            using (SolidBrush fill = new SolidBrush(colour))
            using (Pen edge = new Pen(Color.FromArgb(60, 60, 60)))
            {
                g.FillRectangle(fill, 2, 2, 12, 12);
                g.DrawRectangle(edge, 2, 2, 11, 11);
            }
            return swatch;
        }

        private void CommentMenu_Opening(object sender, CancelEventArgs e)
        {
            //The right click that stopped a box being placed opens no menu
            if (stNodeEditor1.TakeCommentPlacementRightCancel())
            {
                e.Cancel = true;
                return;
            }
            bool onCanvas = stNodeEditor1.GetHoveredNode() == null && stNodeEditor1.GetHoveredPin() == null
                && stNodeEditor1.GetHoveredLink().Item1 == null && stNodeEditor1.GetReroutePointUnderMouse() == null;
            _commentMenuAt = stNodeEditor1.MousePositionInCanvas;
            _menuComment = onCanvas ? stNodeEditor1.GetCommentUnderMouse() : null;
            bool onComment = _menuComment != null;

            _commentSeparator.Visible = onCanvas;
            _addCommentItem.Visible = onCanvas;
            _renameCommentItem.Visible = onComment;
            _commentColourItem.Visible = onComment;
            _deleteCommentItem.Visible = onComment;
            _deleteCommentAndContentsItem.Visible = onComment;
            if (!onComment)
                return;
            _deleteCommentAndContentsItem.Enabled = stNodeEditor1.GetNodesInComment(_menuComment).Length != 0 || stNodeEditor1.GetCommentsInComment(_menuComment).Length != 0;
            foreach (ToolStripMenuItem item in _commentColourItem.DropDownItems.OfType<ToolStripMenuItem>())
            {
                Color colour = (Color)item.Tag;
                item.Checked = colour.IsEmpty ? _menuComment.Colour.IsEmpty : !_menuComment.Colour.IsEmpty && colour.ToArgb() == _menuComment.Colour.ToArgb();
            }
        }

        private static string Quoted(STNodeComment box) => "'" + (string.IsNullOrEmpty(box?.Title) ? "Comment" : box.Title) + "'";

        /* A box placed: it is on the page at once (with a stand-in title), and its title is asked for. It is recorded as added
           when the prompt closes, with whatever title it has then - one undo step. */
        private void StNodeEditor1_CommentPlaced(object sender, STNodeCommentPlacedEventArgs e)
        {
            STNodeComment box = new STNodeComment() { Bounds = e.Bounds, Title = "Comment" };
            stNodeEditor1.AddComment(box);
            stNodeEditor1.SelectedComment = box;
            MarkFlowgraphEdit();
            BeginInvoke(new Action(() => PromptCommentTitle(box, true)));
        }

        private void StNodeEditor1_CommentTitleDoubleClicked(object sender, STNodeCommentEventArgs e)
        {
            STNodeComment box = e.Comment;
            BeginInvoke(new Action(() => PromptCommentTitle(box, false)));
        }

        /// <summary>Ask for a box's title: for a box just placed (recorded as added when this closes), or to rename one.</summary>
        internal void PromptCommentTitle(STNodeComment box, bool adding)
        {
            if (box == null || IsDisposed)
                return;
            _commentPrompt?.Close();
            RenameGeneric prompt = new RenameGeneric(adding ? "" : box.Title, new RenameGeneric.RenameGenericContent()
            {
                Title = adding ? "New comment on " + _flowgraphName : "Rename comment " + Quoted(box),
                Description = "Comment Title",
                ButtonText = adding ? "Add Comment" : "Rename Comment",
            });
            prompt.OnRenamed += name =>
            {
                if (adding)
                {
                    box.Title = name;
                    stNodeEditor1.Invalidate();
                }
                else
                {
                    RenameComment(box, name);
                }
            };
            prompt.FormClosed += (s, e) =>
            {
                if (_commentPrompt == prompt)
                    _commentPrompt = null;
                if (adding)
                    RecordCommentAdded(box);
            };
            _commentPrompt = prompt;
            prompt.Show();
        }

        private void RecordCommentAdded(STNodeComment box)
        {
            int index = IsDisposed ? -1 : stNodeEditor1.Comments.ToList().IndexOf(box);
            if (index < 0 || !CanRecord)
                return;
            MarkFlowgraphEdit();
            UndoStack.Current.Record(new CommentEdit(_composite, _flowgraphName, box, null, CommentState.Of(box), index, "Add comment " + Quoted(box)));
        }

        /// <summary>Give a box another title, as one undo step.</summary>
        internal void RenameComment(STNodeComment box, string title)
        {
            if (box == null || !stNodeEditor1.Comments.Contains(box) || (box.Title ?? "") == (title ?? ""))
                return;
            string label = "Rename comment " + Quoted(box) + " to '" + title + "'";
            ChangeComment(box, o => o.Title = title ?? "", label);
        }

        /// <summary>Give a box another colour (Color.Empty: the editor's own), as one undo step.</summary>
        internal void SetCommentColour(STNodeComment box, Color colour)
        {
            if (box == null || !stNodeEditor1.Comments.Contains(box))
                return;
            if (box.Colour.IsEmpty == colour.IsEmpty && box.Colour.ToArgb() == colour.ToArgb())
                return;
            ChangeComment(box, o => o.Colour = colour, "Colour comment " + Quoted(box) + " " + (FlowgraphComments.PresetName(colour) ?? "custom").ToLowerInvariant());
        }

        /// <summary>Move or resize a box, as one undo step (only the box: what is inside it stays where it is).</summary>
        internal void SetCommentBounds(STNodeComment box, Rectangle bounds)
        {
            if (box == null || !stNodeEditor1.Comments.Contains(box) || box.Bounds == bounds)
                return;
            ChangeComment(box, o => o.Bounds = bounds, "Move comment " + Quoted(box));
        }

        private void ChangeComment(STNodeComment box, Action<STNodeComment> change, string label)
        {
            CommentState before = CommentState.Of(box);
            change(box);
            stNodeEditor1.Invalidate();
            MarkFlowgraphEdit();
            if (CanRecord)
                UndoStack.Current.Record(new CommentEdit(_composite, _flowgraphName, box, before, CommentState.Of(box), stNodeEditor1.Comments.ToList().IndexOf(box), label));
        }

        /// <summary>Add a box to the page, as one undo step. Returns it.</summary>
        internal STNodeComment AddCommentRecorded(Rectangle bounds, string title, Color colour)
        {
            STNodeComment box = new STNodeComment() { Bounds = bounds, Title = title ?? "", Colour = colour };
            stNodeEditor1.AddComment(box);
            MarkFlowgraphEdit();
            if (CanRecord)
                UndoStack.Current.Record(new CommentEdit(_composite, _flowgraphName, box, null, CommentState.Of(box), stNodeEditor1.Comments.Count - 1, "Add comment " + Quoted(box)));
            return box;
        }

        /// <summary>
        /// Take a box off the page, as one undo step - with everything inside it when asked: its nodes go as Delete Node takes
        /// them (an entity left with no node and nothing else wanting it goes too), and the boxes inside it.
        /// </summary>
        internal void DeleteComment(STNodeComment box, bool withContents, bool ask = true)
        {
            if (box == null || !stNodeEditor1.Comments.Contains(box))
                return;
            List<STNode> nodes = withContents ? stNodeEditor1.GetNodesInComment(box).ToList() : new List<STNode>();
            List<STNodeComment> inner = withContents ? stNodeEditor1.GetCommentsInComment(box).ToList() : new List<STNodeComment>();
            if (ask && nodes.Count != 0 && SettingsManager.GetBool(Settings.AskBeforeDeletingNode))
            {
                string what = nodes.Count == 1 ? "the node" : "the " + nodes.Count + " nodes";
                if (MessageBox.Show("Are you sure you want to remove this comment and " + what + " inside it?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            string label = (withContents ? "Delete comment and contents " : "Delete comment ") + Quoted(box);
            using (UndoStack.Current.BeginGroup(label))
            {
                foreach (STNodeComment comment in inner.Append(box))
                {
                    int index = stNodeEditor1.Comments.ToList().IndexOf(comment);
                    CommentState before = CommentState.Of(comment);
                    stNodeEditor1.RemoveComment(comment);
                    if (CanRecord)
                        UndoStack.Current.Record(new CommentEdit(_composite, _flowgraphName, comment, before, null, index, "Delete comment " + Quoted(comment)));
                }
                if (nodes.Count != 0)
                {
                    List<Entity> entities = NodeEntities(nodes);
                    RemoveNodesRecorded(nodes);
                    CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
                    if (display != null)
                    {
                        HashSet<ShortGuid> pointerTargets = display.CollectPointerTargets();
                        foreach (Entity entity in entities.Where(o => !CompositeDisplay.IsInSceneEntity(o) && !display.IsEntityStillReferenced(o, pointerTargets)).ToList())
                            display.DeleteEntity(entity, false);
                    }
                }
            }
            MarkFlowgraphEdit();
            RefreshNodeMarkers();
        }

        /// <summary>The selected box goes (Delete), or Escape stops one being placed. True when the key was used.</summary>
        private bool TryHandleCommentKey(Keys keyData)
        {
            Keys keyCode = keyData & Keys.KeyCode;
            if (keyCode == Keys.Escape && (keyData & Keys.Modifiers) == Keys.None)
                return stNodeEditor1.CancelCommentPlacement();
            if (keyCode == Keys.Delete && (keyData & Keys.Modifiers) == Keys.None && stNodeEditor1.SelectedComment != null && stNodeEditor1.GetSelectedNode().Length == 0)
            {
                DeleteComment(stNodeEditor1.SelectedComment, false);
                return true;
            }
            return false;
        }

        /* A box dragged by its title (with the nodes, boxes and reroute points inside it) or by an edge: one undo step */
        private void StNodeEditor1_CommentChanged(object sender, STNodeCommentChangedEventArgs e)
        {
            MarkFlowgraphEdit();
            if (!CanRecord)
                return;
            string label = (e.Change == CommentChange.Moved ? "Move comment " : "Resize comment ") + Quoted(e.Comment);
            using (UndoStack.Current.BeginGroup(label))
            {
                RecordCommentBounds(e.Comment, e.Before, e.After, label);
                foreach ((STNodeComment comment, Rectangle before, Rectangle after) in e.Inner)
                    RecordCommentBounds(comment, before, after, "Move comment " + Quoted(comment));
                if (e.NodesMoved != null)
                    RecordNodesMoved(e.NodesMoved);
            }
        }

        private void RecordCommentBounds(STNodeComment box, Rectangle before, Rectangle after, string label)
        {
            CommentState from = CommentState.Of(box);
            from.Bounds = before;
            CommentState to = CommentState.Of(box);
            to.Bounds = after;
            UndoStack.Current.Record(new CommentEdit(_composite, _flowgraphName, box, from, to, stNodeEditor1.Comments.ToList().IndexOf(box), label));
        }

        /// <summary>
        /// For undo: take the page's box from one state to another (null: not on the page) and hand back the box it is now.
        /// The box is the one given while it is on the page, else the one that matches <paramref name="from"/>.
        /// </summary>
        internal STNodeComment ApplyCommentState(STNodeComment box, CommentState from, CommentState to, int index)
        {
            STNodeComment live = box != null && stNodeEditor1.Comments.Contains(box) ? box : null;
            if (live == null && from != null)
                live = stNodeEditor1.Comments.FirstOrDefault(from.Matches);
            if (live == null && from != null)
                throw new InvalidOperationException("The comment box is no longer on the page");

            if (to == null)
            {
                stNodeEditor1.RemoveComment(live);
                return live;
            }
            if (live == null)
            {
                live = box ?? new STNodeComment();
                to.ApplyTo(live);
                stNodeEditor1.AddComment(live, index);
                return live;
            }
            to.ApplyTo(live);
            stNodeEditor1.Invalidate();
            return live;
        }
    }
}
