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
            //Shown without the file's root element, which every location in the file shares
            where = where.IndexOf(" › ", StringComparison.Ordinal) >= 0 ? where.Substring(where.IndexOf(" › ", StringComparison.Ordinal) + 3) : where;
            //The graphics and sound options by their settings' and quality levels' names, a level's value as the level
            if (string.Equals(_target, "DATA/ENGINE_SETTINGS.XML", StringComparison.OrdinalIgnoreCase))
                where = System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(where, @"\b(?:Setting|Quality) '([^']*)'", "$1"), @" › @(?:int|float|bool)$", "");
            Conflicts.Add(new MergeConflict()
            {
                Target = _target,
                Where = where,
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

    /// <summary>
    /// An XML file written back in its own layout. An XmlDocument written out whole spaces every element's attributes its own
    /// way (ENGINE_SETTINGS.XML lines its values up with tabs), so two changed values come back as hundreds of changed lines.
    /// Here the changes are written into the original text instead - a value where the old one was, an added entry on a line of
    /// its own after its neighbour, a removed one with its line - and everything else stays exactly as it was: the file differs
    /// from the original by what changed, and nothing more.
    /// </summary>
    public static class XmlLayout
    {
        /* Where an element is in the text: its start tag, and its end tag (none for an empty one, <x/>) */
        private class Span
        {
            public int Start;           //The start tag's '<'
            public int StartEnd;        //Just after the start tag's '>'
            public int CloseStart = -1; //The end tag's '<'
            public int End;             //Just after the element
            public bool Empty => CloseStart < 0;
        }

        /// <summary>
        /// The original file's text changed to say what <paramref name="result"/> says - its elements, attributes and values;
        /// comments and the spacing between elements are the original's. Null when it can't be (the original doesn't read back
        /// as itself, it has a document type, its root is another, or what's written doesn't say what the document does): the
        /// document is written out whole then.
        /// </summary>
        public static byte[] Rewrite(byte[] original, XmlDocument result)
        {
            try
            {
                string text = TextDbMerge.Decode(original, out Encoding encoding);
                if (!TextDbMerge.Encode(text, encoding).AsSpan().SequenceEqual(original))
                    return null;
                XmlDocument before = new XmlDocument() { PreserveWhitespace = true };
                before.LoadXml(text);
                List<Span> spans = Scan(text);
                XmlNodeList elements = before.GetElementsByTagName("*");
                if (spans == null || spans.Count != elements.Count || before.DocumentElement == null || result?.DocumentElement == null || before.DocumentElement.Name != result.DocumentElement.Name)
                    return null;
                //The elements in the order their start tags are in the text
                Dictionary<XmlElement, Span> where = new Dictionary<XmlElement, Span>();
                for (int i = 0; i < spans.Count; i++)
                {
                    XmlElement element = (XmlElement)elements[i];
                    if (string.CompareOrdinal(text, spans[i].Start + 1, element.Name, 0, element.Name.Length) != 0)
                        return null;
                    where[element] = spans[i];
                }

                Writer writer = new Writer(text, where);
                writer.Element(before.DocumentElement, result.DocumentElement);
                string written = writer.Apply();
                if (written == null)
                    return null;
                XmlDocument check = new XmlDocument() { PreserveWhitespace = true };
                check.LoadXml(written);
                if (Canonical(check.DocumentElement) != Canonical(result.DocumentElement))
                    return null;
                return TextDbMerge.Encode(written, encoding);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /* Each element's start and end tags, in the order the start tags come; null for anything this doesn't read (a document type) */
        private static List<Span> Scan(string text)
        {
            List<Span> spans = new List<Span>();
            Stack<Span> open = new Stack<Span>();
            int at = 0;
            while (true)
            {
                int lt = text.IndexOf('<', at);
                if (lt < 0)
                    break;
                if (string.CompareOrdinal(text, lt, "<!--", 0, 4) == 0)
                    at = Past(text, "-->", lt + 4);
                else if (string.CompareOrdinal(text, lt, "<![CDATA[", 0, 9) == 0)
                    at = Past(text, "]]>", lt + 9);
                else if (string.CompareOrdinal(text, lt, "<?", 0, 2) == 0)
                    at = Past(text, "?>", lt + 2);
                else if (string.CompareOrdinal(text, lt, "<!", 0, 2) == 0)
                    return null;
                else if (string.CompareOrdinal(text, lt, "</", 0, 2) == 0)
                {
                    int gt = text.IndexOf('>', lt);
                    if (gt < 0 || open.Count == 0)
                        return null;
                    Span closed = open.Pop();
                    closed.CloseStart = lt;
                    closed.End = at = gt + 1;
                }
                else
                {
                    //A start tag: to its '>', past any in quoted values
                    int i = lt + 1;
                    char quote = '\0';
                    for (; i < text.Length; i++)
                    {
                        char c = text[i];
                        if (quote != '\0') { if (c == quote) quote = '\0'; }
                        else if (c == '"' || c == '\'') quote = c;
                        else if (c == '>') break;
                    }
                    if (i >= text.Length)
                        return null;
                    Span span = new Span() { Start = lt, StartEnd = i + 1 };
                    spans.Add(span);
                    if (text[i - 1] == '/')
                        span.End = i + 1;
                    else
                        open.Push(span);
                    at = i + 1;
                }
                if (at < 0)
                    return null;
            }
            return open.Count == 0 ? spans : null;
        }

        private static int Past(string text, string end, int from)
        {
            int found = text.IndexOf(end, from, StringComparison.Ordinal);
            return found < 0 ? -1 : found + end.Length;
        }

        /* The changes, as edits of the original text */
        private class Writer
        {
            private readonly string _text;
            private readonly Dictionary<XmlElement, Span> _where;
            private readonly string _newline;
            private readonly List<(int Start, int End, string Text)> _edits = new List<(int, int, string)>();

            public Writer(string text, Dictionary<XmlElement, Span> where)
            {
                _text = text;
                _where = where;
                _newline = text.Contains("\r\n") ? "\r\n" : "\n";
            }

            /// <summary>Write the result's version of an element of the original over it.</summary>
            public void Element(XmlElement was, XmlElement now)
            {
                if (was.OuterXml == now.OuterXml)
                    return;
                Span span = _where[was];
                string tag = _text.Substring(span.Start, span.StartEnd - span.Start);
                string newTag = was.Name == now.Name ? StartTag(tag, was, now) : null;
                bool hadChildren = HasElementChildren(was), hasChildren = HasElementChildren(now);
                Dictionary<XmlElement, XmlElement> pairs = hadChildren && hasChildren ? Pairs(was, now) : null;
                //Nothing to line up (another element, a value become a group or the other way round, no child still there): it's replaced
                if (newTag == null || hadChildren != hasChildren || (hadChildren && pairs == null))
                {
                    Replace(span.Start, span.End, Fragment(now));
                    return;
                }
                //An empty element given a value: written out open, with it
                if (!hadChildren && span.Empty && was.InnerXml != now.InnerXml)
                {
                    Replace(span.Start, span.End, Opened(newTag) + now.InnerXml + "</" + now.Name + ">");
                    return;
                }
                if (newTag != tag)
                    Replace(span.Start, span.StartEnd, newTag);
                if (!hadChildren)
                {
                    //A value: what's in it
                    if (was.InnerXml != now.InnerXml)
                        Replace(span.StartEnd, span.CloseStart, now.InnerXml);
                    return;
                }

                List<XmlElement> wasKids = Kids(was), nowKids = Kids(now);
                HashSet<XmlElement> kept = new HashSet<XmlElement>(pairs.Values);
                foreach (XmlElement kid in wasKids)
                    if (!kept.Contains(kid))
                        RemoveWithLine(_where[kid]);
                foreach (KeyValuePair<XmlElement, XmlElement> pair in pairs)
                    Element(pair.Value, pair.Key);
                //Added ones go in after the kept one before them (or before the first kept one), one to a line as their neighbours are
                XmlElement after = null;
                List<XmlElement> added = new List<XmlElement>();
                foreach (XmlElement kid in nowKids)
                {
                    if (pairs.TryGetValue(kid, out XmlElement match))
                    {
                        if (added.Count != 0)
                            Insert(after, match, added);
                        added.Clear();
                        after = match;
                    }
                    else
                        added.Add(kid);
                }
                if (added.Count != 0)
                    Insert(after, null, added);
            }

            /* Each of the result's children with the original child it is - or null when none is. By key (tag and name, else tag
               and place among those of its tag), keeping the most of those that are in the same order in both (one moved is taken
               out and put in again); then one whose key changed (its name did) is still the one in its place: between the same
               two kept ones, the next of its tag along - so it's written over where it is, not taken out and put in again */
            private static Dictionary<XmlElement, XmlElement> Pairs(XmlElement was, XmlElement now)
            {
                List<XmlElement> wasKids = Kids(was), nowKids = Kids(now);
                Dictionary<XmlElement, int> wasIndex = new Dictionary<XmlElement, int>();
                for (int i = 0; i < wasKids.Count; i++)
                    wasIndex[wasKids[i]] = i;
                Dictionary<string, XmlElement> wasByKey = Keys(wasKids).ToDictionary(o => o.Value, o => o.Key);
                Dictionary<XmlElement, string> nowKeys = Keys(nowKids);

                List<(XmlElement Now, XmlElement Was)> keyed = new List<(XmlElement, XmlElement)>();
                foreach (XmlElement kid in nowKids)
                    if (wasByKey.TryGetValue(nowKeys[kid], out XmlElement match))
                        keyed.Add((kid, match));
                //The longest run of them in the same order in both
                int[] tails = new int[keyed.Count], before = new int[keyed.Count];
                int length = 0;
                for (int i = 0; i < keyed.Count; i++)
                {
                    int low = 0, high = length;
                    while (low < high)
                    {
                        int mid = (low + high) / 2;
                        if (wasIndex[keyed[tails[mid]].Was] < wasIndex[keyed[i].Was]) low = mid + 1;
                        else high = mid;
                    }
                    before[i] = low > 0 ? tails[low - 1] : -1;
                    tails[low] = i;
                    if (low == length)
                        length++;
                }
                Dictionary<XmlElement, XmlElement> pairs = new Dictionary<XmlElement, XmlElement>();
                for (int i = length == 0 ? -1 : tails[length - 1]; i >= 0; i = before[i])
                    pairs[keyed[i].Now] = keyed[i].Was;

                HashSet<XmlElement> anchors = new HashSet<XmlElement>(pairs.Values);
                int floor = -1;
                foreach (XmlElement kid in nowKids)
                {
                    if (pairs.TryGetValue(kid, out XmlElement kept))
                    {
                        floor = wasIndex[kept];
                        continue;
                    }
                    for (int i = floor + 1; i < wasKids.Count && !anchors.Contains(wasKids[i]); i++)
                    {
                        if (wasKids[i].Name != kid.Name)
                            continue;
                        pairs[kid] = wasKids[i];
                        floor = i;
                        break;
                    }
                }
                return pairs.Count == 0 ? null : pairs;
            }

            /* Each child's key: its tag and name when no other of its tag has that name, else its tag and place among those of its tag */
            private static Dictionary<XmlElement, string> Keys(List<XmlElement> kids)
            {
                Dictionary<string, int> named = new Dictionary<string, int>();
                foreach (XmlElement kid in kids)
                {
                    string key = kid.Name + "\u0001" + kid.GetAttribute("name");
                    named.TryGetValue(key, out int count);
                    named[key] = count + 1;
                }
                Dictionary<XmlElement, string> keys = new Dictionary<XmlElement, string>();
                Dictionary<string, int> places = new Dictionary<string, int>();
                foreach (XmlElement kid in kids)
                {
                    places.TryGetValue(kid.Name, out int place);
                    places[kid.Name] = place + 1;
                    string name = kid.GetAttribute("name");
                    keys[kid] = name.Length != 0 && named[kid.Name + "\u0001" + name] == 1 ? kid.Name + "\u0001" + name : kid.Name + "#" + place;
                }
                return keys;
            }

            /* The original start tag with the result's attributes: a changed value where the old one was, a removed attribute
               with the space before it, a new one after the last - null when its attributes can't all be found in it */
            private static readonly System.Text.RegularExpressions.Regex _attribute = new System.Text.RegularExpressions.Regex(@"(\s+)([^\s=/>]+)(\s*=\s*)(?:""([^""]*)""|'([^']*)')");
            private static string StartTag(string tag, XmlElement was, XmlElement now)
            {
                System.Text.RegularExpressions.MatchCollection found = _attribute.Matches(tag);
                if (found.Count != was.Attributes.Count)
                    return null;
                StringBuilder written = new StringBuilder();
                HashSet<string> there = new HashSet<string>();
                //"<name", then each attribute as it was, changed or taken out, then any new ones
                int at = 1 + was.Name.Length;
                written.Append(tag, 0, at);
                foreach (System.Text.RegularExpressions.Match match in found)
                {
                    string name = match.Groups[2].Value;
                    if (!was.HasAttribute(name) || !there.Add(name))
                        return null;
                    written.Append(tag, at, match.Index - at);
                    at = match.Index + match.Length;
                    if (!now.HasAttribute(name))
                        continue;
                    string value = now.GetAttribute(name);
                    if (value == was.GetAttribute(name))
                    {
                        written.Append(match.Value);
                        continue;
                    }
                    char quote = match.Groups[4].Success ? '"' : '\'';
                    written.Append(match.Groups[1].Value).Append(name).Append(match.Groups[3].Value).Append(quote).Append(Escape(value, quote)).Append(quote);
                }
                foreach (XmlAttribute attribute in now.Attributes)
                    if (!there.Contains(attribute.Name))
                        written.Append(' ').Append(attribute.Name).Append("=\"").Append(Escape(attribute.Value, '"')).Append('"');
                written.Append(tag, at, tag.Length - at);
                return written.ToString();
            }

            /* <x .../> as <x ...> */
            private static string Opened(string tag)
            {
                return tag.Substring(0, tag.LastIndexOf('/')).TrimEnd() + ">";
            }

            /* An element as text to put in the file: without the namespace declarations it's written with only for being written
               on its own (where it goes in the file, they're declared already) */
            private static string Fragment(XmlElement element)
            {
                string xml = element.OuterXml;
                int end = 0;
                char quote = '\0';
                for (; end < xml.Length; end++)
                {
                    char c = xml[end];
                    if (quote != '\0') { if (c == quote) quote = '\0'; }
                    else if (c == '"' || c == '\'') quote = c;
                    else if (c == '>') break;
                }
                string tag = xml.Substring(0, end);
                string kept = _attribute.Replace(tag, match =>
                {
                    string name = match.Groups[2].Value;
                    return (name == "xmlns" || name.StartsWith("xmlns:")) && !element.HasAttribute(name) ? "" : match.Value;
                });
                return kept + xml.Substring(end);
            }

            private static string Escape(string value, char quote)
            {
                StringBuilder escaped = new StringBuilder(value.Length);
                foreach (char c in value)
                {
                    switch (c)
                    {
                        case '&': escaped.Append("&amp;"); break;
                        case '<': escaped.Append("&lt;"); break;
                        case '"': escaped.Append(quote == '"' ? "&quot;" : "\""); break;
                        case '\'': escaped.Append(quote == '\'' ? "&apos;" : "'"); break;
                        case '\t': escaped.Append("&#x9;"); break;
                        case '\n': escaped.Append("&#xA;"); break;
                        case '\r': escaped.Append("&#xD;"); break;
                        default: escaped.Append(c); break;
                    }
                }
                return escaped.ToString();
            }

            /* The whitespace an element's line starts with before it - or null when something else comes first on its line */
            private string IndentOf(int position)
            {
                int lineStart = position == 0 ? 0 : _text.LastIndexOf('\n', position - 1) + 1;
                string indent = _text.Substring(lineStart, position - lineStart);
                return indent.Trim().Length == 0 ? indent : null;
            }

            /* Where the line an element ends on ends (just after its newline) when nothing but whitespace follows it, or -1 */
            private int LineAfter(int position)
            {
                int newline = _text.IndexOf('\n', position);
                return newline >= 0 && _text.Substring(position, newline - position).Trim().Length == 0 ? newline + 1 : -1;
            }

            private void RemoveWithLine(Span span)
            {
                string indent = IndentOf(span.Start);
                int next = LineAfter(span.End);
                if (indent != null && next >= 0)
                    Replace(span.Start - indent.Length, next, "");
                else
                    Replace(span.Start, span.End, "");
            }

            private void Insert(XmlElement after, XmlElement before, List<XmlElement> added)
            {
                if (after != null)
                {
                    Span anchor = _where[after];
                    string indent = IndentOf(anchor.Start);
                    int next = LineAfter(anchor.End);
                    if (indent != null && next >= 0)
                        Replace(next, next, string.Concat(added.Select(o => indent + Fragment(o) + _newline)));
                    else
                        Replace(anchor.End, anchor.End, string.Concat(added.Select(o => _newline + (indent ?? "") + Fragment(o))));
                }
                else
                {
                    Span anchor = _where[before];
                    string indent = IndentOf(anchor.Start);
                    int at = indent == null ? anchor.Start : anchor.Start - indent.Length;
                    Replace(at, at, string.Concat(added.Select(o => (indent ?? "") + Fragment(o) + _newline)));
                }
            }

            private void Replace(int start, int end, string text)
            {
                _edits.Add((start, end, text));
            }

            /// <summary>The original text with every change made, or null when two of them overlap.</summary>
            public string Apply()
            {
                StringBuilder text = new StringBuilder(_text);
                int limit = _text.Length;
                //From the end, so the places of those still to make hold; a removal before an insertion where it ends
                foreach ((int start, int end, string replacement) in _edits.OrderByDescending(o => o.Start).ThenByDescending(o => o.End))
                {
                    if (end > limit)
                        return null;
                    text.Remove(start, end - start).Insert(start, replacement);
                    limit = start;
                }
                return text.ToString();
            }
        }

        /* What a document says, its layout aside: each element, its attributes in any order, and a value's text */
        private static string Canonical(XmlElement element)
        {
            StringBuilder text = new StringBuilder();
            void Write(XmlElement e)
            {
                text.Append('<').Append(e.Name);
                foreach (XmlAttribute attribute in e.Attributes.Cast<XmlAttribute>().OrderBy(o => o.Name, StringComparer.Ordinal))
                    text.Append(' ').Append(attribute.Name).Append('=').Append(attribute.Value.Length).Append(':').Append(attribute.Value);
                text.Append('>');
                if (HasElementChildren(e))
                    foreach (XmlElement kid in Kids(e))
                        Write(kid);
                else
                    text.Append(e.InnerText);
                text.Append("</>");
            }
            Write(element);
            return text.ToString();
        }

        private static bool HasElementChildren(XmlElement element)
        {
            foreach (XmlNode node in element.ChildNodes)
                if (node.NodeType == XmlNodeType.Element)
                    return true;
            return false;
        }

        private static List<XmlElement> Kids(XmlElement element)
        {
            return element.ChildNodes.OfType<XmlElement>().ToList();
        }
    }
}
#endif
