#if ENABLE_MOD_PACKAGES
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;

namespace OpenCAGE.Modding
{
    /* Config changes as changes, not files. A config BML is XML underneath, and OpenCAGE ships
     * vanilla copies of every config it edits - so a mod can carry "alien max speed = 2" rather
     * than a whole DIFFICULTYSETTINGS.BML, two mods editing different values can coexist, and a
     * conflict warning can say which value two mods disagree on.
     *
     * Elements are addressed by steps like difficulty[@name='HARD'] - the name attribute when it's
     * unique among same-tag siblings, an ordinal otherwise. Ops are diffed against vanilla and
     * applied to whatever the file currently holds, which is what makes them mergeable. */
    public class BmlPatchOp
    {
        public string Kind;   //"set" (attribute), "removeattr", "settext", "add" (element), "remove", "replace"
        public string Path;   //Element address; for "add", the parent's address
        public string Attr;   //For set/removeattr
        public string Value;  //For set/settext
        public string Xml;    //For add/replace: the element's outer XML
        public int Index;     //For add: position among the parent's children

        /// <summary>
        /// What this op touches, for conflict detection between mods.
        /// </summary>
        public string Claim
        {
            get { return Attr == null ? Path : Path + "/@" + Attr; }
        }
    }

    public static class BmlPatch
    {
        #region DIFF
        /// <summary>
        /// The ops that turn the vanilla document into the current one.
        /// </summary>
        public static List<BmlPatchOp> Diff(XmlDocument vanilla, XmlDocument current)
        {
            return Diff(vanilla, current, null);
        }

        /// <summary>
        /// The ops that turn the vanilla document into the current one. <paramref name="inherited"/>, when given, says whether
        /// a value element only the current document has holds just what the file would get anyway from a template it
        /// inherits from: such an element is no change, and nor is such a value inside an element the current document adds.
        /// </summary>
        public static List<BmlPatchOp> Diff(XmlDocument vanilla, XmlDocument current, Func<XmlElement, bool> inherited)
        {
            List<BmlPatchOp> ops = new List<BmlPatchOp>();
            XmlElement vanillaRoot = vanilla.DocumentElement;
            XmlElement currentRoot = current.DocumentElement;
            if (vanillaRoot == null || currentRoot == null || vanillaRoot.Name != currentRoot.Name)
            {
                //Roots don't line up: the whole document is the change
                ops.Add(new BmlPatchOp() { Kind = "replace", Path = currentRoot == null ? "" : Step(currentRoot), Xml = current.OuterXml });
                return ops;
            }
            DiffElement(vanillaRoot, currentRoot, Step(vanillaRoot), ops, inherited);
            return ops;
        }

        /// <summary>
        /// Whether two spellings of a value mean the same to the game. The config editors save a number as their number box
        /// holds it, so a 3 can come back as "3.000" and 0.45 as "0.450" (and, from an older OpenCAGE on a German or French
        /// Windows, "0,450"); a tick box saves "True" whatever case the file had. Whole numbers are only the same when spelt
        /// the same, so a "007" stays apart from a "7".
        /// </summary>
        public static bool SameValue(string a, string b)
        {
            if (a == b)
                return true;
            if (a == null || b == null)
                return false;
            string x = a.Trim(), y = b.Trim();
            if (x == y)
                return true;
            if (IsFraction(x) || IsFraction(y))
            {
                decimal dx, dy;
                if (TryNumber(x, out dx) && TryNumber(y, out dy))
                    return dx == dy;
                double fx, fy;
                if (double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out fx) && double.TryParse(y, NumberStyles.Float, CultureInfo.InvariantCulture, out fy))
                    return fx == fy;
            }
            if (IsBoolean(x) && IsBoolean(y))
                return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        /* A number with a fractional part or an exponent ("3.000", "0,450", "1e-3"), as against a whole number ("3", "007") */
        private static bool IsFraction(string text)
        {
            decimal ignored;
            return (text.IndexOf('.') >= 0 || text.IndexOf(',') >= 0 || text.IndexOf('e') >= 0 || text.IndexOf('E') >= 0) && TryNumber(text, out ignored);
        }

        private static bool TryNumber(string text, out decimal value)
        {
            if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
            //"0,450": a decimal comma, as an older OpenCAGE wrote numbers on a PC set to a language that uses one
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"^[-+]?\d+,\d+$")
                && decimal.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsBoolean(string text)
        {
            return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase);
        }

        private static void DiffElement(XmlElement vanilla, XmlElement current, string path, List<BmlPatchOp> ops, Func<XmlElement, bool> inherited)
        {
            //Attributes
            foreach (XmlAttribute attr in current.Attributes)
            {
                string vanillaValue = vanilla.HasAttribute(attr.Name) ? vanilla.GetAttribute(attr.Name) : null;
                if (!SameValue(vanillaValue, attr.Value))
                    ops.Add(new BmlPatchOp() { Kind = "set", Path = path, Attr = attr.Name, Value = attr.Value });
            }
            foreach (XmlAttribute attr in vanilla.Attributes)
                if (!current.HasAttribute(attr.Name))
                    ops.Add(new BmlPatchOp() { Kind = "removeattr", Path = path, Attr = attr.Name });

            //Direct text (only when neither side has element children - mixed content gets replaced wholesale below)
            if (!HasElementChildren(vanilla) && !HasElementChildren(current))
            {
                string vanillaText = DirectText(vanilla);
                string currentText = DirectText(current);
                if (!SameValue(vanillaText, currentText))
                    ops.Add(new BmlPatchOp() { Kind = "settext", Path = path, Value = currentText });
                return;
            }

            //Children: pair up by address, then diff pairs and emit add/remove for the rest
            List<XmlElement> vanillaChildren = Children(vanilla);
            List<XmlElement> currentChildren = Children(current);
            Dictionary<string, XmlElement> vanillaByStep = StepMap(vanillaChildren);
            Dictionary<string, XmlElement> currentByStep = StepMap(currentChildren);

            if (vanillaByStep == null || currentByStep == null)
            {
                //Ambiguous children (duplicate addresses): replace this element wholesale
                if (vanilla.OuterXml != current.OuterXml)
                    ops.Add(new BmlPatchOp() { Kind = "replace", Path = path, Xml = current.OuterXml });
                return;
            }

            foreach (KeyValuePair<string, XmlElement> child in currentByStep)
            {
                XmlElement vanillaChild;
                if (vanillaByStep.TryGetValue(child.Key, out vanillaChild))
                    DiffElement(vanillaChild, child.Value, path + "/" + child.Key, ops, inherited);
                else
                {
                    XmlElement added = inherited == null ? child.Value : WithoutInherited(child.Value, inherited);
                    if (added == null)
                        continue;
                    ops.Add(new BmlPatchOp()
                    {
                        Kind = "add",
                        Path = path,
                        Index = currentChildren.IndexOf(child.Value),
                        Xml = added.OuterXml,
                    });
                }
            }
            foreach (KeyValuePair<string, XmlElement> child in vanillaByStep)
                if (!currentByStep.ContainsKey(child.Key))
                    ops.Add(new BmlPatchOp() { Kind = "remove", Path = path + "/" + child.Key });
        }

        /* An added element without the values in it that it only inherits (see Diff): the element itself when nothing goes,
           a copy without them when some do, and null when it is nothing but such values. A value with attributes, and any
           group's own attributes, always stay. */
        private static XmlElement WithoutInherited(XmlElement element, Func<XmlElement, bool> inherited)
        {
            if (!HasElementChildren(element))
                return element.Attributes.Count == 0 && inherited(element) ? null : element;

            XmlElement copy = (XmlElement)element.CloneNode(false); //its attributes, none of its children
            bool changed = false, keptAny = false;
            foreach (XmlNode node in element.ChildNodes)
            {
                XmlElement child = node as XmlElement;
                if (child == null)
                {
                    copy.AppendChild(node.CloneNode(true));
                    continue;
                }
                XmlElement kept = WithoutInherited(child, inherited);
                if (kept == null)
                {
                    changed = true;
                    continue;
                }
                if (kept != child)
                    changed = true;
                copy.AppendChild(kept == child ? child.CloneNode(true) : kept);
                keptAny = true;
            }
            if (!changed)
                return element;
            return keptAny || element.Attributes.Count != 0 ? copy : null;
        }
        #endregion

        #region APPLY
        /// <summary>
        /// Apply ops to a document in place. Returns the ops that could not be applied (their
        /// targets no longer resolve) - an empty list is a fully clean apply.
        /// </summary>
        public static List<BmlPatchOp> Apply(XmlDocument document, List<BmlPatchOp> ops)
        {
            List<BmlPatchOp> failed = new List<BmlPatchOp>();
            foreach (BmlPatchOp op in ops)
            {
                XmlElement target = Resolve(document, op.Path);
                try
                {
                    switch (op.Kind)
                    {
                        case "set":
                            if (target == null) { failed.Add(op); continue; }
                            target.SetAttribute(op.Attr, op.Value);
                            break;
                        case "removeattr":
                            if (target == null) { failed.Add(op); continue; }
                            target.RemoveAttribute(op.Attr);
                            break;
                        case "settext":
                            if (target == null) { failed.Add(op); continue; }
                            target.InnerText = op.Value ?? "";
                            break;
                        case "add":
                            {
                                if (target == null) { failed.Add(op); continue; }
                                XmlDocumentFragment fragment = document.CreateDocumentFragment();
                                fragment.InnerXml = op.Xml;
                                List<XmlElement> children = Children(target);
                                if (op.Index >= 0 && op.Index < children.Count)
                                    target.InsertBefore(fragment, children[op.Index]);
                                else
                                    target.AppendChild(fragment);
                                break;
                            }
                        case "remove":
                            if (target == null) { failed.Add(op); continue; }
                            if (target.ParentNode == null) { failed.Add(op); continue; }
                            target.ParentNode.RemoveChild(target);
                            break;
                        case "replace":
                            {
                                XmlDocumentFragment fragment = document.CreateDocumentFragment();
                                fragment.InnerXml = op.Xml;
                                if (target == null || target.ParentNode == null)
                                {
                                    //Root replacement, or a target that never resolved
                                    if (target == null && document.DocumentElement != null && op.Path.IndexOf('/') < 0)
                                        target = document.DocumentElement;
                                    if (target == null || target.ParentNode == null) { failed.Add(op); continue; }
                                }
                                target.ParentNode.ReplaceChild(fragment, target);
                                break;
                            }
                        default:
                            failed.Add(op);
                            break;
                    }
                }
                catch
                {
                    failed.Add(op);
                }
            }
            return failed;
        }
        #endregion

        #region ADDRESSING
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
                if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.CDATA)
                    text.Append(node.Value);
            return text.ToString();
        }

        private static List<XmlElement> Children(XmlElement element)
        {
            List<XmlElement> children = new List<XmlElement>();
            foreach (XmlNode node in element.ChildNodes)
                if (node.NodeType == XmlNodeType.Element)
                    children.Add((XmlElement)node);
            return children;
        }

        /// <summary>
        /// The address step for an element among its siblings: name[@name='X'] when that's unique,
        /// name#ordinal otherwise. Returns null when even that is ambiguous (duplicate name attrs).
        /// </summary>
        private static string Step(XmlElement element)
        {
            XmlElement parent = element.ParentNode as XmlElement;
            List<XmlElement> siblings = parent == null
                ? new List<XmlElement>() { element }
                : Children(parent).Where(o => o.Name == element.Name).ToList();

            string nameAttr = element.GetAttribute("name");
            if (nameAttr.Length != 0 && siblings.Count(o => o.GetAttribute("name") == nameAttr) == 1)
                return element.Name + "[@name='" + nameAttr.Replace("'", "&apos;") + "']";
            if (siblings.Count == 1)
                return element.Name;
            return element.Name + "#" + siblings.IndexOf(element);
        }

        /* Steps for every child at once; null when two children share an address (unpairable) */
        private static Dictionary<string, XmlElement> StepMap(List<XmlElement> children)
        {
            Dictionary<string, XmlElement> map = new Dictionary<string, XmlElement>();
            foreach (XmlElement child in children)
            {
                string step = Step(child);
                if (map.ContainsKey(step))
                    return null;
                map[step] = child;
            }
            return map;
        }

        #region DESCRIBING
        /// <summary>
        /// A change in words a modder recognises, with the value it replaces: "AreaSweep › menace cool down time: 30 → 77",
        /// "adds keycard 'MODLAB_A' to keycards", "removes difficulty 'HARD'".
        /// </summary>
        public static string Describe(BmlPatchOp op, XmlDocument vanilla)
        {
            XmlElement original = vanilla == null ? null : Resolve(vanilla, op.Path);
            string where = FriendlyPath(op.Path);
            switch (op.Kind)
            {
                case "set":
                    {
                        string was = original != null && original.HasAttribute(op.Attr) ? original.GetAttribute(op.Attr) : null;
                        return (where.Length == 0 ? "" : where + " › ") + Friendly(op.Attr) + ": " + (was == null ? "" : Short(was) + " → ") + Short(op.Value);
                    }
                case "settext":
                    return where + ": " + (original == null ? "" : Short(original.InnerText) + " → ") + Short(op.Value);
                case "removeattr":
                    return "removes " + Friendly(op.Attr) + (where.Length == 0 ? "" : " from " + where);
                case "add":
                    return DescribeValues(op.Xml, where) ?? "adds " + DescribeElement(op.Xml) + (where.Length == 0 ? "" : " to " + where);
                case "remove":
                    return "removes " + FriendlyStep(op.Path.Substring(op.Path.LastIndexOf('/') + 1));
                case "replace":
                    return "replaces " + (where.Length == 0 ? "the whole file" : where);
                default:
                    return op.Kind + " " + where;
            }
        }

        /// <summary>The behaviour tree a change in DATA/BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML belongs to (null: the directory itself).</summary>
        public static string TreeOf(BmlPatchOp op)
        {
            string name = null;
            if (op.Kind == "add" && op.Path.IndexOf('/') < 0)
                name = AttributeOf(op.Xml, "name");
            else
            {
                string[] steps = op.Path.Split('/');
                if (steps.Length > 1)
                    name = NameInStep(steps[1]);
            }
            if (name == null) return null;
            return name.EndsWith(".bml", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;
        }

        /// <summary>An address without its root, each step as a person would say it: AreaSweep › keycard 'Erik Mosser'</summary>
        public static string FriendlyPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string[] steps = path.Split('/');
            return string.Join(" › ", steps.Skip(1).Select(FriendlyStep));
        }

        private static string FriendlyStep(string step)
        {
            int bracket = step.IndexOf('[');
            if (bracket < 0) return Friendly(step);
            string tag = Friendly(step.Substring(0, bracket));
            string name = NameInStep(step);
            if (name != null) return tag + " '" + name + "'";
            string ordinal = step.Substring(bracket + 1).TrimEnd(']');
            return tag + " " + ordinal;
        }

        private static string NameInStep(string step)
        {
            int at = step.IndexOf("[@name='", StringComparison.Ordinal);
            if (at < 0) return null;
            int start = at + "[@name='".Length;
            int end = step.IndexOf("']", start, StringComparison.Ordinal);
            return end < 0 ? null : step.Substring(start, end - start);
        }

        /* An added element that is only values (no attributes anywhere): the values, as a set would say them - "Senses ›
           Sensing Set 2 › max hearing distance set 2: 12.345" - rather than "adds Sensing Set 2 to Senses". Null for anything
           else, or more than a few values. */
        private static string DescribeValues(string xml, string where)
        {
            XmlElement added;
            try
            {
                XmlDocument fragment = new XmlDocument();
                fragment.LoadXml(xml);
                added = fragment.DocumentElement;
            }
            catch (XmlException)
            {
                return null;
            }
            List<string> values = new List<string>();
            if (added == null || !CollectValues(added, where.Length == 0 ? "" : where + " › ", values) || values.Count == 0 || values.Count > 3)
                return null;
            return string.Join("; ", values);
        }

        private static bool CollectValues(XmlElement element, string prefix, List<string> values)
        {
            if (element.Attributes.Count != 0)
                return false;
            string here = prefix + Friendly(element.Name);
            if (!HasElementChildren(element))
            {
                values.Add(here + ": " + Short(element.InnerText));
                return true;
            }
            foreach (XmlElement child in Children(element))
                if (!CollectValues(child, here + " › ", values))
                    return false;
            return true;
        }

        private static string DescribeElement(string xml)
        {
            string tag = xml == null ? "an entry" : System.Text.RegularExpressions.Regex.Match(xml, @"^\s*<([\w:.-]+)").Groups[1].Value;
            string name = AttributeOf(xml, "name");
            return Friendly(tag.Length == 0 ? "entry" : tag) + (name == null ? "" : " '" + name + "'");
        }

        private static string AttributeOf(string xml, string attribute)
        {
            if (xml == null) return null;
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(xml, @"^\s*<[^>]*\s" + attribute + "=\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string Friendly(string name)
        {
            return (name ?? "").Replace('_', ' ');
        }

        private static string Short(string value)
        {
            if (value == null) return "(nothing)";
            value = value.Trim();
            return value.Length <= 40 ? value : value.Substring(0, 40) + "...";
        }
        #endregion
        private static XmlElement Resolve(XmlDocument document, string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            string[] steps = SplitSteps(path);
            XmlElement at = document.DocumentElement;
            if (at == null || !StepMatches(at, steps[0], 0, new List<XmlElement>() { at }))
                return null;
            for (int i = 1; i < steps.Length; i++)
            {
                List<XmlElement> children = Children(at);
                XmlElement next = null;
                for (int x = 0; x < children.Count; x++)
                {
                    if (StepMatches(children[x], steps[i], IndexAmongSameName(children, x), children))
                    {
                        next = children[x];
                        break;
                    }
                }
                if (next == null)
                    return null;
                at = next;
            }
            return at;
        }

        private static int IndexAmongSameName(List<XmlElement> children, int index)
        {
            int ordinal = 0;
            for (int i = 0; i < index; i++)
                if (children[i].Name == children[index].Name)
                    ordinal++;
            return ordinal;
        }

        private static bool StepMatches(XmlElement element, string step, int ordinalAmongSameName, List<XmlElement> siblings)
        {
            int attrAt = step.IndexOf("[@name='");
            int hashAt = step.LastIndexOf('#');
            if (attrAt >= 0)
            {
                string name = step.Substring(0, attrAt);
                string value = step.Substring(attrAt + 8, step.Length - attrAt - 8 - 2).Replace("&apos;", "'");
                return element.Name == name && element.GetAttribute("name") == value;
            }
            if (hashAt >= 0 && IsNumber(step, hashAt + 1))
            {
                string name = step.Substring(0, hashAt);
                int ordinal = int.Parse(step.Substring(hashAt + 1));
                return element.Name == name && ordinalAmongSameName == ordinal;
            }
            return element.Name == step;
        }

        private static bool IsNumber(string text, int from)
        {
            if (from >= text.Length)
                return false;
            for (int i = from; i < text.Length; i++)
                if (text[i] < '0' || text[i] > '9')
                    return false;
            return true;
        }

        /* Split on '/' outside of [@name='...'] quoting */
        private static string[] SplitSteps(string path)
        {
            List<string> steps = new List<string>();
            int start = 0;
            bool quoted = false;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '\'')
                    quoted = !quoted;
                else if (path[i] == '/' && !quoted)
                {
                    steps.Add(path.Substring(start, i - start));
                    start = i + 1;
                }
            }
            steps.Add(path.Substring(start));
            return steps.ToArray();
        }
        #endregion

        #region SERIALISATION
        public static string Serialise(List<BmlPatchOp> ops, string targetFile)
        {
            XmlDocument document = new XmlDocument();
            XmlElement root = document.CreateElement("bmlpatch");
            root.SetAttribute("version", "1");
            root.SetAttribute("file", targetFile ?? "");
            document.AppendChild(root);
            foreach (BmlPatchOp op in ops)
            {
                XmlElement element = document.CreateElement(op.Kind);
                element.SetAttribute("path", op.Path ?? "");
                if (op.Attr != null) element.SetAttribute("attr", op.Attr);
                if (op.Value != null) element.SetAttribute("value", op.Value);
                if (op.Kind == "add") element.SetAttribute("index", op.Index.ToString());
                if (op.Xml != null) element.InnerXml = op.Xml;
                root.AppendChild(element);
            }
            return document.OuterXml;
        }

        public static List<BmlPatchOp> Deserialise(string xml)
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(xml);
            List<BmlPatchOp> ops = new List<BmlPatchOp>();
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null)
                    continue;
                BmlPatchOp op = new BmlPatchOp()
                {
                    Kind = element.Name,
                    Path = element.GetAttribute("path"),
                    Attr = element.HasAttribute("attr") ? element.GetAttribute("attr") : null,
                    Value = element.HasAttribute("value") ? element.GetAttribute("value") : null,
                    Xml = element.InnerXml.Length == 0 ? null : element.InnerXml,
                };
                int index;
                if (int.TryParse(element.GetAttribute("index"), out index))
                    op.Index = index;
                ops.Add(op);
            }
            return ops;
        }
        #endregion
    }
}
#endif
