using CATHODE.Scripting;
using ST.Library.UI.NodeEditor;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static CathodeLib.CompositeFlowgraphTable;
using static CathodeLib.CompositeFlowgraphTable.FlowgraphMeta;

namespace OpenCAGE
{
    /// <summary>
    /// A page's wire ties between the node editor and the saved layout: links are named there by node id (the node's place
    /// on the page as saved), entity and pin at each end.
    /// </summary>
    public static class WireTieLayouts
    {
        /// <summary>The editor's ties into the layout being saved from it (node ids as AsFlowgraphMeta numbers them).</summary>
        public static void Save(STNodeEditor editor, FlowgraphMeta meta)
        {
            meta.WireTies = new List<WireTieMeta>();
            if (editor == null)
                return;
            foreach (WireTieState tie in editor.GetWireTies())
            {
                WireTieMeta saved = new WireTieMeta() { ID = tie.Id, Position = Point.Round(tie.Position), Axis = tie.Axis };
                foreach (WireTieMemberState member in tie.Members)
                {
                    STNode from = member.From?.Owner, to = member.To?.Owner;
                    if (from == null || to == null)
                        continue;
                    saved.Members.Add(new WireTieMemberMeta()
                    {
                        FromNodeID = editor.Nodes.IndexOf(from),
                        FromEntityGUID = from.ShortGUID,
                        FromParameterGUID = member.From.ShortGUID,
                        ToNodeID = editor.Nodes.IndexOf(to),
                        ToEntityGUID = to.ShortGUID,
                        ToParameterGUID = member.To.ShortGUID,
                        Slot = member.Slot,
                        Order = member.Order,
                    });
                }
                if (saved.Members.Count != 0)
                    meta.WireTies.Add(saved);
            }
        }

        /// <summary>
        /// The layout's ties onto a page just built from it (after its connections and reroute points). A link no longer on
        /// the page is left out, and a tie left with none.
        /// </summary>
        public static void Apply(STNodeEditor editor, FlowgraphMeta meta)
        {
            if (editor == null || meta?.WireTies == null || meta.WireTies.Count == 0)
                return;

            List<STNode> nodes = editor.Nodes.ToArray().Where(o => o != null).ToList();
            List<WireTieState> ties = new List<WireTieState>();
            foreach (WireTieMeta saved in meta.WireTies)
            {
                if (saved == null)
                    continue;
                WireTieState tie = new WireTieState() { Id = saved.ID, Position = saved.Position, Axis = saved.Axis };
                foreach (WireTieMemberMeta member in saved.Members)
                {
                    (STNodeOption from, STNodeOption to) = FindLink(nodes, member);
                    if (from != null)
                        tie.Members.Add(new WireTieMemberState() { From = from, To = to, Slot = member.Slot, Order = member.Order });
                }
                if (tie.Members.Count != 0)
                    ties.Add(tie);
            }
            editor.SetWireTies(ties);
        }

        /// <summary>
        /// The link a saved member names: between the nodes with its ids and entities, else between any nodes of those entities
        /// that carry it. Nulls when it is not on the page.
        /// </summary>
        private static (STNodeOption, STNodeOption) FindLink(List<STNode> nodes, WireTieMemberMeta member)
        {
            IEnumerable<STNode> Candidates(int id, ShortGuid entity) =>
                nodes.Where(o => o.ShortGUID == entity).OrderBy(o => o.NodeID == id ? 0 : 1);

            foreach (STNode from in Candidates(member.FromNodeID, member.FromEntityGUID))
            {
                foreach (STNodeOption pin in from.GetOutputOptions().Concat(from.GetTopOptions()))
                {
                    if (pin == null || pin == STNodeOption.Empty || pin.ShortGUID != member.FromParameterGUID)
                        continue;
                    foreach (STNode to in Candidates(member.ToNodeID, member.ToEntityGUID))
                    {
                        STNodeOption other = pin.GetConnectedOption()?.FirstOrDefault(o => o?.Owner == to && o.ShortGUID == member.ToParameterGUID);
                        if (other != null)
                            return (pin, other);
                    }
                }
            }
            return (null, null);
        }

        /// <summary>
        /// A layout's ties copied onto another page of nodes renumbered by <paramref name="ids"/> ((old node id, entity) to new
        /// id) and shifted by (dx, dy) - as a page merged into another carries its links' reroute points. Ties naming a node
        /// not renumbered are left out; ids are made unique among <paramref name="into"/>'s ties.
        /// </summary>
        public static void CarryInto(FlowgraphMeta from, FlowgraphMeta into, Dictionary<(int, ShortGuid), int> ids, int dx, int dy)
        {
            if (from?.WireTies == null || from.WireTies.Count == 0 || into == null)
                return;
            if (into.WireTies == null)
                into.WireTies = new List<WireTieMeta>();
            int next = into.WireTies.Count == 0 ? 1 : into.WireTies.Max(o => o.ID) + 1;
            foreach (WireTieMeta tie in from.WireTies)
            {
                WireTieMeta copy = new WireTieMeta() { ID = next, Position = new Point(tie.Position.X + dx, tie.Position.Y + dy), Axis = tie.Axis };
                foreach (WireTieMemberMeta member in tie.Members)
                {
                    if (!ids.TryGetValue((member.FromNodeID, member.FromEntityGUID), out int fromId) || !ids.TryGetValue((member.ToNodeID, member.ToEntityGUID), out int toId))
                        continue;
                    WireTieMemberMeta moved = member.Clone();
                    moved.FromNodeID = fromId;
                    moved.ToNodeID = toId;
                    copy.Members.Add(moved);
                }
                if (copy.Members.Count == 0)
                    continue;
                into.WireTies.Add(copy);
                next++;
            }
        }
    }
}
