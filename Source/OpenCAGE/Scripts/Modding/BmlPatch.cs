#if ENABLE_MOD_PACKAGES
using System;
using System.Collections.Generic;
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
            List<BmlPatchOp> ops = new List<BmlPatchOp>();
            XmlElement vanillaRoot = vanilla.DocumentElement;
            XmlElement currentRoot = current.DocumentElement;
            if (vanillaRoot == null || currentRoot == null || vanillaRoot.Name != currentRoot.Name)
            {
                //Roots don't line up: the whole document is the change
                ops.Add(new BmlPatchOp() { Kind = "replace", Path = currentRoot == null ? "" : Step(currentRoot), Xml = current.OuterXml });
                return ops;
            }
            DiffElement(vanillaRoot, currentRoot, Step(vanillaRoot), ops);
            return ops;
        }

        private static void DiffElement(XmlElement vanilla, XmlElement current, string path, List<BmlPatchOp> ops)
        {
            //Attributes
            foreach (XmlAttribute attr in current.Attributes)
            {
                string vanillaValue = vanilla.HasAttribute(attr.Name) ? vanilla.GetAttribute(attr.Name) : null;
                if (vanillaValue != attr.Value)
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
                if (vanillaText != currentText)
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
                    DiffElement(vanillaChild, child.Value, path + "/" + child.Key, ops);
                else
                    ops.Add(new BmlPatchOp()
                    {
                        Kind = "add",
                        Path = path,
                        Index = currentChildren.IndexOf(child.Value),
                        Xml = child.Value.OuterXml,
                    });
            }
            foreach (KeyValuePair<string, XmlElement> child in vanillaByStep)
                if (!currentByStep.ContainsKey(child.Key))
                    ops.Add(new BmlPatchOp() { Kind = "remove", Path = path + "/" + child.Key });
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
