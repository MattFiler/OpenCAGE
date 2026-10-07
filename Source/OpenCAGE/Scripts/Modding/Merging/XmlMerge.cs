#if ENABLE_MOD_PACKAGES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace OpenCAGE.Modding.Merging
{
    /// <summary>
    /// Three-way merge of XML documents - the configs, the item database, the compiled behaviour trees - against
    /// the vanilla document they were all edited from. Each mod's version is merged into the result in list
    /// order, so changes to different values, different list entries or different behaviour trees all survive,
    /// and where two mods change the same thing the later one wins and the clash is reported.
    /// </summary>
    /// <remarks>
    /// Elements are paired with their vanilla originals by a key attribute (a <c>name</c> unique among their
    /// siblings, and the like), or - where a list has no keys, as behaviour tree nodes don't - by lining the
    /// children up: identical subtrees first, then what's left in order. A mod's additions go in after the
    /// sibling they followed in that mod's own version, which keeps behaviour tree priority order intact.
    /// </remarks>
    public class XmlMerge
    {
        private readonly string _target;
        private readonly ClaimLedger _ledger = new ClaimLedger();
        public List<MergeConflict> Conflicts { get; } = new List<MergeConflict>();

        private string _modName;
        private string _previousMods;

        public XmlMerge(string target)
        {
            _target = target;
        }

        /// <summary>
        /// Merge every mod's version of the document into a copy of the vanilla one, in list order.
        /// </summary>
        public XmlDocument Merge(XmlDocument vanilla, IList<(string ModName, XmlDocument Document)> versions)
        {
            XmlDocument merged = (XmlDocument)vanilla.CloneNode(true);
            List<string> applied = new List<string>();
            foreach ((string modName, XmlDocument version) in versions)
            {
                _modName = modName;
                _previousMods = applied.Count == 0 ? null : string.Join(", ", applied);
                MergeInto(vanilla, merged, version);
                applied.Add(modName);
            }
            return merged;
        }

        /// <summary>Merge one mod's version into the result so far.</summary>
        private void MergeInto(XmlDocument vanilla, XmlDocument merged, XmlDocument version)
        {
            XmlElement b = vanilla.DocumentElement, m = merged.DocumentElement, t = version.DocumentElement;
            if (b == null || m == null || t == null || b.Name != t.Name || b.Name != m.Name)
            {
                //Nothing to line up: the mod replaced the whole document
                if (t != null && !SameXml(b, t))
                {
                    string earlier = _ledger.ClaimedAtOrUnder(Step(b ?? t));
                    if (earlier != null)
                        AddConflict(Step(t), ConflictKind.Overridden, earlier, "the whole document is replaced");
                    XmlNode imported = merged.ImportNode(t, true);
                    if (m != null) merged.ReplaceChild(imported, m); else merged.AppendChild(imported);
                    _ledger.Claim(Step(t), _modName);
                }
                return;
            }
            MergeElement(b, m, t, Step(b));
        }

        #region ELEMENTS
        private void MergeElement(XmlElement b, XmlElement m, XmlElement t, string where)
        {
            MergeAttributes(b, m, t, where);

            bool anyChildren = HasElementChildren(b) || HasElementChildren(m) || HasElementChildren(t);
            if (!anyChildren)
            {
                string bt = DirectText(b), mt = DirectText(m), tt = DirectText(t);
                if (tt != bt && tt != mt)
                {
                    if (mt != bt)
                        AddConflict(where, ConflictKind.Overridden, _ledger.ClaimedBy(where), "set to '" + Clip(tt) + "' instead of '" + Clip(mt) + "'");
                    SetText(m, tt);
                    _ledger.Claim(where, _modName);
                }
                return;
            }

            MergeChildren(b, m, t, where);
        }

        private void MergeAttributes(XmlElement b, XmlElement m, XmlElement t, string where)
        {
            HashSet<string> names = new HashSet<string>();
            foreach (XmlAttribute a in b.Attributes) names.Add(a.Name);
            foreach (XmlAttribute a in t.Attributes) names.Add(a.Name);
            foreach (string name in names)
            {
                string bv = b.HasAttribute(name) ? b.GetAttribute(name) : null;
                string mv = m.HasAttribute(name) ? m.GetAttribute(name) : null;
                string tv = t.HasAttribute(name) ? t.GetAttribute(name) : null;
                if (tv == bv || tv == mv)
                    continue;
                string at = where + " › @" + name;
                if (mv != bv)
                    AddConflict(at, ConflictKind.Overridden, _ledger.ClaimedBy(at), tv == null ? "removed" : "set to '" + Clip(tv) + "' instead of '" + Clip(mv ?? "(nothing)") + "'");
                if (tv == null) m.RemoveAttribute(name); else m.SetAttribute(name, tv);
                _ledger.Claim(at, _modName);
            }
        }

        private void MergeChildren(XmlElement b, XmlElement m, XmlElement t, string where)
        {
            List<XmlElement> bKids = Children(b), mKids = Children(m), tKids = Children(t);
            Dictionary<XmlElement, XmlElement> mToB = Match(bKids, mKids);
            Dictionary<XmlElement, XmlElement> tToB = Match(bKids, tKids);
            Dictionary<XmlElement, XmlElement> bToM = mToB.ToDictionary(o => o.Value, o => o.Key);
            Dictionary<XmlElement, XmlElement> bToT = tToB.ToDictionary(o => o.Value, o => o.Key);
            Dictionary<string, string> keyAttr = KeyAttributes(bKids, mKids, tKids);

            //What became of each vanilla child: removed, changed, or merged further down
            foreach (XmlElement bChild in bKids)
            {
                bToM.TryGetValue(bChild, out XmlElement mChild);
                bToT.TryGetValue(bChild, out XmlElement tChild);
                string childWhere = where + " › " + Describe(bChild, keyAttr);

                if (tChild == null)
                {
                    if (mChild == null)
                        continue;
                    //Removed by this mod: changes an earlier mod made inside it go with it
                    string earlier = SameXml(mChild, bChild) ? null : _ledger.ClaimedAtOrUnder(childWhere);
                    if (earlier != null)
                        AddConflict(childWhere, ConflictKind.RemovedVsChanged, earlier, "'" + _modName + "' removes it, '" + earlier + "' had changed it");
                    m.RemoveChild(mChild);
                    _ledger.Claim(childWhere, _modName);
                }
                else if (mChild == null)
                {
                    //An earlier mod removed it; this one keeps it, and changed it - the later mod wins and it comes back
                    if (SameXml(tChild, bChild))
                        continue;
                    string earlier = _ledger.ClaimedAtOrUnder(childWhere);
                    AddConflict(childWhere, ConflictKind.RemovedVsChanged, earlier, "'" + (earlier ?? "an earlier mod") + "' removes it, '" + _modName + "' changes it");
                    InsertAfterAnchor(m, (XmlElement)m.OwnerDocument.ImportNode(tChild, true), tChild, tKids, tToB, bToM, null);
                    _ledger.Claim(childWhere, _modName);
                }
                else if (!SameXml(tChild, bChild))
                {
                    MergeElement(bChild, mChild, tChild, childWhere);
                }
            }

            //What this mod added: merged with the same addition from an earlier mod, or put in after its neighbour
            Dictionary<string, XmlElement> mAdded = new Dictionary<string, XmlElement>();
            foreach (XmlElement mChild in Children(m))
            {
                if (mToB.ContainsKey(mChild))
                    continue;
                string key = KeyOf(mChild, keyAttr);
                if (key != null && !mAdded.ContainsKey(key))
                    mAdded[key] = mChild;
            }
            Dictionary<XmlElement, XmlElement> placed = new Dictionary<XmlElement, XmlElement>();
            foreach (XmlElement tChild in tKids)
            {
                if (tToB.ContainsKey(tChild))
                    continue;
                string key = KeyOf(tChild, keyAttr);
                string childWhere = where + " › " + Describe(tChild, keyAttr);
                if (key != null && mAdded.TryGetValue(key, out XmlElement twin))
                {
                    //Both mods add the same entry: merge the two as if the earlier one were vanilla
                    if (!SameXml(twin, tChild))
                    {
                        AddConflict(childWhere, ConflictKind.Overridden, _ledger.ClaimedAtOrUnder(childWhere), "both add it, differently");
                        XmlElement replacement = (XmlElement)m.OwnerDocument.ImportNode(tChild, true);
                        m.ReplaceChild(replacement, twin);
                        placed[tChild] = replacement;
                    }
                    else
                        placed[tChild] = twin;
                    _ledger.Claim(childWhere, _modName);
                    continue;
                }
                XmlElement added = (XmlElement)m.OwnerDocument.ImportNode(tChild, true);
                InsertAfterAnchor(m, added, tChild, tKids, tToB, bToM, placed);
                placed[tChild] = added;
                _ledger.Claim(childWhere, _modName);
            }
        }

        /* Put a mod's addition in after the nearest sibling before it (in that mod's version) that the result
           also has - so an entry added at the top of a list, or a behaviour node added between two others,
           lands in the same place it did for the mod */
        private static void InsertAfterAnchor(XmlElement parent, XmlElement node, XmlElement source, List<XmlElement> sourceKids,
            Dictionary<XmlElement, XmlElement> sourceToBase, Dictionary<XmlElement, XmlElement> baseToResult, Dictionary<XmlElement, XmlElement> placed)
        {
            int index = sourceKids.IndexOf(source);
            for (int i = index - 1; i >= 0; i--)
            {
                XmlElement anchor = null;
                if (placed != null && placed.TryGetValue(sourceKids[i], out XmlElement p))
                    anchor = p;
                else if (sourceToBase.TryGetValue(sourceKids[i], out XmlElement b) && baseToResult.TryGetValue(b, out XmlElement r) && r.ParentNode == parent)
                    anchor = r;
                if (anchor != null)
                {
                    parent.InsertAfter(node, anchor);
                    return;
                }
            }
            XmlElement first = Children(parent).FirstOrDefault();
            if (first != null && index == 0)
                parent.InsertBefore(node, first);
            else if (first != null && sourceKids.Count > 0 && index < sourceKids.Count / 2)
                parent.InsertBefore(node, first);
            else
                parent.AppendChild(node);
        }
        #endregion

        #region MATCHING
        private static readonly string[] _keyCandidates = { "name", "Name", "NAME", "id", "ID", "Id", "guid", "GUID", "key", "Key", "Identifier", "filename", "Filename", "file", "File", "type_name" };

        /// <summary>
        /// For each tag among these siblings, the attribute that names each one uniquely in every version - or no
        /// entry when nothing does.
        /// </summary>
        private static Dictionary<string, string> KeyAttributes(params List<XmlElement>[] versions)
        {
            Dictionary<string, string> keys = new Dictionary<string, string>();
            foreach (string tag in versions.SelectMany(o => o).Select(o => o.Name).Distinct())
            {
                foreach (string candidate in _keyCandidates)
                {
                    bool ok = true;
                    foreach (List<XmlElement> kids in versions)
                    {
                        List<XmlElement> same = kids.Where(o => o.Name == tag).ToList();
                        if (same.Any(o => !o.HasAttribute(candidate)) || same.Select(o => o.GetAttribute(candidate)).Distinct().Count() != same.Count)
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok)
                    {
                        keys[tag] = candidate;
                        break;
                    }
                }
            }
            return keys;
        }

        private static string KeyOf(XmlElement element, Dictionary<string, string> keyAttr)
        {
            return keyAttr.TryGetValue(element.Name, out string attr) ? element.Name + "\u0001" + element.GetAttribute(attr) : null;
        }

        /// <summary>
        /// Pair each child of a version with the vanilla child it came from (or not at all, when it's new).
        /// </summary>
        private static Dictionary<XmlElement, XmlElement> Match(List<XmlElement> baseKids, List<XmlElement> otherKids)
        {
            Dictionary<XmlElement, XmlElement> result = new Dictionary<XmlElement, XmlElement>();
            Dictionary<string, string> keyAttr = KeyAttributes(baseKids, otherKids);

            //Keyed tags: by key
            Dictionary<string, XmlElement> baseByKey = new Dictionary<string, XmlElement>();
            foreach (XmlElement b in baseKids)
            {
                string key = KeyOf(b, keyAttr);
                if (key != null) baseByKey[key] = b;
            }
            foreach (XmlElement o in otherKids)
            {
                string key = KeyOf(o, keyAttr);
                if (key != null && baseByKey.TryGetValue(key, out XmlElement b))
                    result[o] = b;
            }

            //Unkeyed: identical subtrees by longest common subsequence, then the rest in order within each gap
            List<XmlElement> bRest = baseKids.Where(o => KeyOf(o, keyAttr) == null).ToList();
            List<XmlElement> oRest = otherKids.Where(o => KeyOf(o, keyAttr) == null).ToList();
            if (bRest.Count == 0 || oRest.Count == 0)
                return result;

            string[] bSig = bRest.Select(o => o.OuterXml).ToArray();
            string[] oSig = oRest.Select(o => o.OuterXml).ToArray();
            List<(int, int)> pairs = Lcs(bSig, oSig);
            foreach ((int bi, int oi) in pairs)
                result[oRest[oi]] = bRest[bi];

            //Gaps between the identical pairs: same-tag elements in order (an edited node, an edited list entry)
            int lastB = -1, lastO = -1;
            foreach ((int bi, int oi) in pairs.Concat(new[] { (bRest.Count, oRest.Count) }))
            {
                List<XmlElement> bGap = bRest.Skip(lastB + 1).Take(bi - lastB - 1).ToList();
                List<XmlElement> oGap = oRest.Skip(lastO + 1).Take(oi - lastO - 1).ToList();
                foreach (string tag in oGap.Select(o => o.Name).Distinct())
                {
                    List<XmlElement> bs = bGap.Where(o => o.Name == tag).ToList();
                    List<XmlElement> os = oGap.Where(o => o.Name == tag).ToList();
                    //Equal numbers: pair them up. Otherwise pair by best resemblance so an insertion or a removal
                    //doesn't shift every pairing after it
                    if (bs.Count == os.Count)
                    {
                        for (int i = 0; i < bs.Count; i++) result[os[i]] = bs[i];
                    }
                    else
                    {
                        HashSet<XmlElement> used = new HashSet<XmlElement>();
                        int floor = 0;
                        foreach (XmlElement o in os)
                        {
                            int best = -1; int bestScore = int.MinValue;
                            for (int i = floor; i < bs.Count; i++)
                            {
                                if (used.Contains(bs[i])) continue;
                                int score = Resemblance(bs[i], o);
                                if (score > bestScore) { bestScore = score; best = i; }
                            }
                            if (best >= 0 && bestScore > 0)
                            {
                                used.Add(bs[best]);
                                result[o] = bs[best];
                                floor = best + 1;
                            }
                        }
                    }
                }
                lastB = bi; lastO = oi;
            }
            return result;
        }

        /* How alike two elements are: shared attributes, then shared direct children */
        private static int Resemblance(XmlElement a, XmlElement b)
        {
            int score = 0;
            foreach (XmlAttribute attr in a.Attributes)
                if (b.HasAttribute(attr.Name) && b.GetAttribute(attr.Name) == attr.Value) score += 2;
            HashSet<string> aKids = new HashSet<string>(Children(a).Select(o => o.OuterXml));
            foreach (XmlElement kid in Children(b))
                if (aKids.Contains(kid.OuterXml)) score += 3;
            if (DirectText(a) == DirectText(b) && DirectText(a).Length != 0) score += 1;
            return score;
        }

        private static List<(int, int)> Lcs(string[] a, string[] b)
        {
            //Quadratic, but these are sibling lists - long ones are keyed and never get here
            if ((long)a.Length * b.Length > 25_000_000)
                return new List<(int, int)>();
            int[,] table = new int[a.Length + 1, b.Length + 1];
            for (int i = a.Length - 1; i >= 0; i--)
                for (int j = b.Length - 1; j >= 0; j--)
                    table[i, j] = a[i] == b[j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
            List<(int, int)> pairs = new List<(int, int)>();
            int x = 0, y = 0;
            while (x < a.Length && y < b.Length)
            {
                if (a[x] == b[y]) { pairs.Add((x, y)); x++; y++; }
                else if (table[x + 1, y] >= table[x, y + 1]) x++;
                else y++;
            }
            return pairs;
        }
        #endregion

        #region HELPERS
        private void AddConflict(string where, ConflictKind kind, string earlier, string detail)
        {
            if (earlier == _modName)
                return;
            Conflicts.Add(new MergeConflict()
            {
                Target = _target,
                //Shown without the file's root element, which every location in the file shares
                Where = where.IndexOf(" › ", StringComparison.Ordinal) >= 0 ? where.Substring(where.IndexOf(" › ", StringComparison.Ordinal) + 3) : where,
                Kind = kind,
                Kept = _modName,
                Lost = earlier ?? _previousMods ?? "an earlier mod",
                Detail = detail,
            });
        }

        private static bool SameXml(XmlElement a, XmlElement b)
        {
            if (a == null || b == null) return a == b;
            return a.OuterXml == b.OuterXml;
        }

        private static bool HasElementChildren(XmlElement element)
        {
            foreach (XmlNode node in element.ChildNodes)
                if (node.NodeType == XmlNodeType.Element)
                    return true;
            return false;
        }

        private static string DirectText(XmlElement element)
        {
            StringBuilder text = new StringBuilder();
            foreach (XmlNode node in element.ChildNodes)
                if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.CDATA || node.NodeType == XmlNodeType.SignificantWhitespace)
                    text.Append(node.Value);
            return text.ToString();
        }

        private static void SetText(XmlElement element, string text)
        {
            foreach (XmlNode node in element.ChildNodes.Cast<XmlNode>().ToList())
                if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.CDATA || node.NodeType == XmlNodeType.SignificantWhitespace || node.NodeType == XmlNodeType.Whitespace)
                    element.RemoveChild(node);
            if (text.Length != 0)
                element.AppendChild(element.OwnerDocument.CreateTextNode(text));
        }

        private static List<XmlElement> Children(XmlElement element)
        {
            List<XmlElement> children = new List<XmlElement>();
            foreach (XmlNode node in element.ChildNodes)
                if (node.NodeType == XmlNodeType.Element)
                    children.Add((XmlElement)node);
            return children;
        }

        private static string Step(XmlElement element)
        {
            return element == null ? "(document)" : element.Name;
        }

        /* A readable step for messages: the tag, with whichever attribute names it */
        private static string Describe(XmlElement element, Dictionary<string, string> keyAttr)
        {
            if (keyAttr.TryGetValue(element.Name, out string attr))
                return element.Name + " '" + element.GetAttribute(attr) + "'";
            foreach (string candidate in _keyCandidates)
                if (element.HasAttribute(candidate))
                    return element.Name + " '" + element.GetAttribute(candidate) + "'";
            if (element.HasAttribute("Class"))
                return element.GetAttribute("Class").Split('.').Last();
            return element.Name;
        }

        private static string Clip(string value)
        {
            if (value == null) return "";
            value = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length > 40 ? value.Substring(0, 37) + "..." : value;
        }
        #endregion
    }
}
#endif
