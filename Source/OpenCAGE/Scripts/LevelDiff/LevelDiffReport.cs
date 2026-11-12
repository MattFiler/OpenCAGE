using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenCAGE.LevelDiff
{
    public enum DiffKind { Added, Removed, Changed, Info }

    /// <summary>One line of a comparison: an item (a composite, a material, a file ...) or a detail of one, with what is under it.</summary>
    public sealed class DiffNode
    {
        public DiffKind Kind;
        public string Text;
        /// <summary>What it is, for drill-down and "go to": "composite:ID", "entity:COMPOSITE/ENTITY", "material:NAME", "file:PATH" ...</summary>
        public string Ref;
        public readonly List<DiffNode> Children = new List<DiffNode>();

        public DiffNode() { }
        public DiffNode(DiffKind kind, string text, string reference = null)
        {
            Kind = kind;
            Text = text;
            Ref = reference;
        }

        public DiffNode Add(DiffKind kind, string text, string reference = null)
        {
            DiffNode child = new DiffNode(kind, text, reference);
            Children.Add(child);
            return child;
        }

        public static string Mark(DiffKind kind)
        {
            switch (kind)
            {
                case DiffKind.Added: return "+";
                case DiffKind.Removed: return "-";
                case DiffKind.Changed: return "~";
                default: return " ";
            }
        }

        public JObject ToJson(int depth, int maxChildren)
        {
            JObject o = new JObject() { ["kind"] = Kind.ToString().ToLowerInvariant(), ["text"] = Text };
            if (Ref != null) o["ref"] = Ref;
            if (Children.Count != 0)
            {
                if (depth <= 0) o["details"] = Children.Count;
                else
                {
                    JArray list = new JArray(Children.Take(maxChildren).Select(c => c.ToJson(depth - 1, maxChildren)));
                    if (Children.Count > maxChildren) list.Add("... and " + (Children.Count - maxChildren) + " more (ask for this item to see them)");
                    o["details"] = list;
                }
            }
            return o;
        }

        public DiffNode FindRef(string reference)
        {
            if (Ref != null && string.Equals(Ref, reference, StringComparison.OrdinalIgnoreCase)) return this;
            foreach (DiffNode child in Children)
            {
                DiffNode found = child.FindRef(reference);
                if (found != null) return found;
            }
            return null;
        }
    }

    /// <summary>One kind of data compared (script, materials, collision ...): its items and a one-line summary.</summary>
    public sealed class DiffCategory
    {
        public string Key, Title;
        public string Summary;
        public readonly List<DiffNode> Items = new List<DiffNode>();
        public readonly List<string> Notes = new List<string>();
        /// <summary>Why it wasn't compared (not asked for, not on one side ...); null when it was.</summary>
        public string Skipped;
        public string Error;
        public int Added, Removed, Changed;
        /// <summary>Editor-only data: no difference in game.</summary>
        public bool EditorOnly;

        public DiffCategory(string key, string title)
        {
            Key = key;
            Title = title;
        }

        public bool HasDifferences => Items.Count != 0;

        public DiffNode Item(DiffKind kind, string text, string reference = null)
        {
            DiffNode node = new DiffNode(kind, text, reference);
            Items.Add(node);
            switch (kind)
            {
                case DiffKind.Added: Added++; break;
                case DiffKind.Removed: Removed++; break;
                case DiffKind.Changed: Changed++; break;
            }
            return node;
        }

        /// <summary>"3 added, 1 removed, 12 changed" (or the summary set, or "no differences").</summary>
        public string Line()
        {
            if (Skipped != null) return "not compared: " + Skipped;
            if (Error != null) return "could not be compared: " + Error;
            if (Summary != null) return Summary;
            if (!HasDifferences) return "no differences";
            List<string> parts = new List<string>();
            if (Added != 0) parts.Add(Added + " added");
            if (Removed != 0) parts.Add(Removed + " removed");
            if (Changed != 0) parts.Add(Changed + " changed");
            int info = Items.Count - Added - Removed - Changed;
            if (info != 0) parts.Add(info + " other");
            return string.Join(", ", parts);
        }
    }

    /// <summary>A whole comparison of two levels: <see cref="LevelComparer"/> makes it; the window and MCP show it.</summary>
    public sealed class LevelDiffReport
    {
        public string Before, After;
        public DateTime Made = DateTime.Now;
        public TimeSpan Took;
        public readonly List<DiffCategory> Categories = new List<DiffCategory>();
        public readonly List<string> Notes = new List<string>();

        public DiffCategory Category(string key) => Categories.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>Categories with differences in game (editor-only ones apart).</summary>
        public int CategoriesDiffering => Categories.Count(o => o.HasDifferences && !o.EditorOnly && o.Key != "files");

        /// <summary>Files that differ byte for byte (a save rewrites some with no change in what they hold).</summary>
        public int FilesDiffering => Category("files")?.Items.Count ?? 0;

        public string ToText(int depth = 3, int maxChildren = 200)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("COMPARE LEVELS");
            sb.AppendLine("before: " + Before);
            sb.AppendLine("after:  " + After);
            sb.AppendLine("made " + Made.ToString("yyyy-MM-dd HH:mm:ss") + " in " + Took.TotalSeconds.ToString("0.0") + " s");
            foreach (string note in Notes) sb.AppendLine("note: " + note);
            sb.AppendLine();
            sb.AppendLine("SUMMARY");
            foreach (DiffCategory c in Categories)
                sb.AppendLine("  " + (c.Title + (c.EditorOnly ? " (editor only)" : "")).PadRight(30) + c.Line());
            foreach (DiffCategory c in Categories.Where(o => o.HasDifferences || o.Notes.Count != 0))
            {
                sb.AppendLine();
                sb.AppendLine(c.Title.ToUpperInvariant() + " - " + c.Line());
                foreach (string note in c.Notes) sb.AppendLine("  note: " + note);
                foreach (DiffNode item in c.Items.Take(maxChildren)) Write(sb, item, 1, depth, maxChildren);
                if (c.Items.Count > maxChildren) sb.AppendLine("  ... and " + (c.Items.Count - maxChildren) + " more");
            }
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, DiffNode node, int indent, int depth, int maxChildren)
        {
            sb.Append(' ', indent * 2).Append(DiffNode.Mark(node.Kind)).Append(' ').AppendLine(node.Text);
            if (depth <= 1) { if (node.Children.Count != 0) sb.Append(' ', indent * 2 + 2).AppendLine("(" + node.Children.Count + " details)"); return; }
            foreach (DiffNode child in node.Children.Take(maxChildren)) Write(sb, child, indent + 1, depth - 1, maxChildren);
            if (node.Children.Count > maxChildren) sb.Append(' ', indent * 2 + 2).AppendLine("... and " + (node.Children.Count - maxChildren) + " more");
        }

        public JObject ToJson(IEnumerable<string> categories, int depth, int maxItems, int maxChildren, int offset = 0)
        {
            HashSet<string> only = categories == null ? null : new HashSet<string>(categories, StringComparer.OrdinalIgnoreCase);
            JObject summary = new JObject();
            foreach (DiffCategory c in Categories)
                summary[c.Key] = c.Line() + (c.EditorOnly ? " (editor only)" : "");
            JObject o = new JObject() { ["before"] = Before, ["after"] = After, ["summary"] = summary };
            if (Notes.Count != 0) o["notes"] = new JArray(Notes);
            JObject details = new JObject();
            foreach (DiffCategory c in Categories)
            {
                if (only != null && !only.Contains(c.Key)) continue;
                if (!c.HasDifferences && c.Notes.Count == 0) continue;
                JObject cat = new JObject() { ["summary"] = c.Line() };
                if (c.Notes.Count != 0) cat["notes"] = new JArray(c.Notes);
                List<DiffNode> page = c.Items.Skip(offset).Take(maxItems).ToList();
                cat["items"] = new JArray(page.Select(i => i.ToJson(depth - 1, maxChildren)));
                if (c.Items.Count > offset + maxItems) cat["more"] = "items " + (offset + 1) + "-" + (offset + page.Count) + " of " + c.Items.Count + ": pass offset " + (offset + maxItems) + " for the next";
                details[c.Key] = cat;
            }
            o["categories"] = details;
            return o;
        }

        /// <summary>The item (at any depth) a ref names, with its category's key.</summary>
        public DiffNode Find(string category, string reference, out DiffCategory found)
        {
            found = null;
            foreach (DiffCategory c in Categories)
            {
                if (category != null && !string.Equals(c.Key, category, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (DiffNode item in c.Items)
                {
                    DiffNode node = item.FindRef(reference);
                    if (node != null) { found = c; return node; }
                }
            }
            return null;
        }

        /// <summary>Items whose text contains <paramref name="text"/>, for drill-down by name.</summary>
        public List<(DiffCategory category, DiffNode node)> Search(string category, string text, int max)
        {
            List<(DiffCategory, DiffNode)> hits = new List<(DiffCategory, DiffNode)>();
            foreach (DiffCategory c in Categories)
            {
                if (category != null && !string.Equals(c.Key, category, StringComparison.OrdinalIgnoreCase)) continue;
                //At any depth (an entity under its composite, a field under its entity), the items first
                foreach (DiffNode item in c.Items)
                    if (Look(c, item, text, hits, max, 0)) return hits;
            }
            return hits;
        }

        private static bool Look(DiffCategory c, DiffNode node, string text, List<(DiffCategory, DiffNode)> hits, int max, int depth)
        {
            if (node.Text != null && node.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                hits.Add((c, node));
                return hits.Count >= max;
            }
            if (depth < 4)
                foreach (DiffNode child in node.Children)
                    if (Look(c, child, text, hits, max, depth + 1)) return true;
            return false;
        }
    }
}
