using CATHODE.Scripting;
using ST.Library.UI.NodeEditor;
using System.Collections.Generic;
using System.Drawing;

namespace OpenCAGE.Undo
{
    /// <summary>A wire tie as an undo step keeps it: its links named so they can be found again after the page is rebuilt.</summary>
    public sealed class WireTieRef
    {
        public int Id;
        public PointF Position;
        public byte Axis;
        public List<Member> Members = new List<Member>();

        public sealed class Member
        {
            public LineRef Line;
            public int Slot;
            public int Order;
        }

        public static List<WireTieRef> From(IEnumerable<WireTieState> ties)
        {
            List<WireTieRef> refs = new List<WireTieRef>();
            foreach (WireTieState tie in ties ?? new WireTieState[0])
            {
                WireTieRef copy = new WireTieRef() { Id = tie.Id, Position = tie.Position, Axis = tie.Axis };
                foreach (WireTieMemberState member in tie.Members)
                {
                    if (member.From?.Owner == null || member.To?.Owner == null)
                        continue;
                    copy.Members.Add(new Member() { Line = new LineRef(member.From, member.To), Slot = member.Slot, Order = member.Order });
                }
                refs.Add(copy);
            }
            return refs;
        }
    }

    /// <summary>
    /// The page's wire ties changed - links tied or untied, a tie moved or straightened - kept as all the page's ties before
    /// and after. Only how lines are drawn: the links are the same. A null side is left as it is: a step that brackets other
    /// edits (Arrange) is two of these, the first putting the ties back on undo and the last setting them on redo, since the
    /// edits between them change links the ties name.
    /// </summary>
    public sealed class WireTieEdit : IEdit, IChangesComposites
    {
        private readonly ShortGuid _composite;
        private readonly string _page;
        private readonly List<WireTieRef> _before;
        private readonly List<WireTieRef> _after;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public IEnumerable<ShortGuid> ChangedComposites => new ShortGuid[0];
        public ShortGuid EntityId => ShortGuid.Invalid;

        public WireTieEdit(Composite composite, string page, List<WireTieRef> before, List<WireTieRef> after, string label)
        {
            _composite = composite.shortGUID;
            _page = page;
            _before = before;
            _after = after;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _after);
        public void Revert(UndoContext context) => Set(context, _before);

        private void Set(UndoContext context, List<WireTieRef> ties)
        {
            if (ties == null)
                return;
            Flowgraph page = FlowgraphEdits.RequirePage(context, _composite, _page);
            page.SetWireTieRefs(ties);
        }

        public bool TryMerge(IEdit next) => false;
    }
}
