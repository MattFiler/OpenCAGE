using CATHODE.Scripting;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OpenCAGE.Undo
{
    /// <summary>A comment box as it was at one moment: what an edit puts back, and how it finds the box again after a page rebuild.</summary>
    public sealed class CommentState
    {
        public Rectangle Bounds;
        public string Title;
        public Color Colour;

        public static CommentState Of(STNodeComment comment)
        {
            return comment == null ? null : new CommentState() { Bounds = comment.Bounds, Title = comment.Title ?? "", Colour = comment.Colour };
        }

        public void ApplyTo(STNodeComment comment)
        {
            comment.Bounds = Bounds;
            comment.Title = Title ?? "";
            comment.Colour = Colour;
        }

        public bool Matches(STNodeComment comment)
        {
            return comment != null && comment.Bounds == Bounds && (comment.Title ?? "") == (Title ?? "") && comment.Colour.ToArgb() == Colour.ToArgb() && comment.Colour.IsEmpty == Colour.IsEmpty;
        }
    }

    /// <summary>
    /// A comment box added to a page, removed from it, or changed (moved, resized, renamed, recoloured). Only how the page is
    /// presented: the scripts are the same before and after.
    /// </summary>
    public sealed class CommentEdit : IEdit, IChangesComposites
    {
        private readonly ShortGuid _composite;
        private readonly string _page;
        private readonly CommentState _before;
        private readonly CommentState _after;
        private readonly int _index;
        //The box itself while the page has not been rebuilt since
        private STNodeComment _box;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public IEnumerable<ShortGuid> ChangedComposites => new ShortGuid[0];
        public ShortGuid EntityId => ShortGuid.Invalid;

        /// <param name="before">The box before, or null when it was added.</param>
        /// <param name="after">The box after, or null when it was removed.</param>
        /// <param name="index">Where the box sits in the page's list (a removed box goes back there).</param>
        public CommentEdit(Composite composite, string page, STNodeComment box, CommentState before, CommentState after, int index, string label)
        {
            _composite = composite.shortGUID;
            _page = page;
            _box = box;
            _before = before;
            _after = after;
            _index = index;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _before, _after);
        public void Revert(UndoContext context) => Set(context, _after, _before);

        private void Set(UndoContext context, CommentState from, CommentState to)
        {
            Flowgraph page = FlowgraphEdits.RequirePage(context, _composite, _page);
            _box = page.ApplyCommentState(_box, from, to, _index);
            DirtyTracker.MarkLevelDataModified();
        }

        public bool TryMerge(IEdit next) => false;
    }
}
