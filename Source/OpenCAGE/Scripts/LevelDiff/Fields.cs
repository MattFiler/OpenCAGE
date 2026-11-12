using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace OpenCAGE.LevelDiff
{
    /// <summary>
    /// A record from one of a level's files (a mover, a collision row, a sound bank ...) as ordered "field = value" text, by reflection,
    /// so two of them can be compared field by field without a hand-written comparer per type. What points into the level's tables (a
    /// material, a submesh, a texture) is named, not followed, so a renumbered table doesn't show as every record changing.
    /// </summary>
    public static class Fields
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly Dictionary<Type, FieldInfo[]> _fields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Field names left out: per-session runtime numbers that say nothing about the content.</summary>
        private static readonly HashSet<string> Skip = new HashSet<string>(StringComparer.Ordinal) { "RuntimeIndex", "RuntimeRefs", "_cachedHash", "Object", "DataOffset" };

        public static List<KeyValuePair<string, string>> Of(object record, ResourceNames names, int depth = 4, int maxList = 64)
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            Walk(record, "", names, depth, maxList, list);
            return list;
        }

        /// <summary>The record as one line of text (for whole-record compares and hashes).</summary>
        public static string Line(object record, ResourceNames names, int depth = 4, int maxList = 64)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string> f in Of(record, names, depth, maxList))
                sb.Append(f.Key).Append('=').Append(f.Value).Append("; ");
            return sb.ToString();
        }

        /// <summary>What changed between two records: "field: before -> now", at most <paramref name="max"/>.</summary>
        public static List<string> Changes(List<KeyValuePair<string, string>> before, List<KeyValuePair<string, string>> after, int max = 40)
        {
            List<string> changes = new List<string>();
            Dictionary<string, string> a = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> f in before) a[f.Key] = f.Value;
            HashSet<string> seen = new HashSet<string>();
            int more = 0;
            foreach (KeyValuePair<string, string> f in after)
            {
                seen.Add(f.Key);
                a.TryGetValue(f.Key, out string old);
                if (old == f.Value) continue;
                if (changes.Count >= max) { more++; continue; }
                changes.Add(f.Key + ": " + (old ?? "(none)") + " -> " + f.Value);
            }
            foreach (KeyValuePair<string, string> f in before)
            {
                if (seen.Contains(f.Key)) continue;
                if (changes.Count >= max) { more++; continue; }
                changes.Add(f.Key + ": " + f.Value + " -> (none)");
            }
            if (more != 0) changes.Add("... and " + more + " more fields");
            return changes;
        }

        public static string Num(float f) => float.IsNaN(f) || float.IsInfinity(f) ? f.ToString(Invariant) : ((double)f).ToString("0.######", Invariant);
        public static string Vec(Vector3 v) => "(" + Num(v.X) + ", " + Num(v.Y) + ", " + Num(v.Z) + ")";

        public static string Matrix(Matrix4x4 m)
        {
            if (Matrix4x4.Decompose(m, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
            {
                //A quaternion, not angles: near straight up or down two Euler readings of one rotation differ wildly
                if (rotation.W < 0) rotation = new Quaternion(-rotation.X, -rotation.Y, -rotation.Z, -rotation.W);
                string s = "at " + Vec(translation) + " rot q(" + Num(rotation.X) + ", " + Num(rotation.Y) + ", " + Num(rotation.Z) + ", " + Num(rotation.W) + ")";
                if (Math.Abs(scale.X - 1) > 1e-5 || Math.Abs(scale.Y - 1) > 1e-5 || Math.Abs(scale.Z - 1) > 1e-5) s += " scale " + Vec(scale);
                return s;
            }
            return string.Join(",", new[] { m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44 }.Select(Num));
        }

        /// <summary>A 64-bit FNV-1a hash of some bytes, as hex: enough to tell content apart in a report.</summary>
        public static string Hash(byte[] data)
        {
            if (data == null) return "none";
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < data.Length; i++) { h ^= data[i]; h *= 1099511628211UL; }
            return data.Length + " bytes #" + h.ToString("x16");
        }

        private static FieldInfo[] FieldsOf(Type type)
        {
            lock (_fields)
            {
                if (!_fields.TryGetValue(type, out FieldInfo[] fields))
                {
                    fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(o => !Skip.Contains(o.Name)).ToArray();
                    _fields[type] = fields;
                }
                return fields;
            }
        }

        private static void Walk(object value, string path, ResourceNames names, int depth, int maxList, List<KeyValuePair<string, string>> into)
        {
            string leaf = Leaf(value, names);
            if (leaf != null) { into.Add(new KeyValuePair<string, string>(path == "" ? "value" : path, leaf)); return; }
            if (depth <= 0) { into.Add(new KeyValuePair<string, string>(path, value.GetType().Name)); return; }
            if (value is IDictionary dict)
            {
                int n = 0;
                foreach (DictionaryEntry kv in dict)
                {
                    if (n++ >= maxList) { into.Add(new KeyValuePair<string, string>(path + ".more", (dict.Count - maxList).ToString())); break; }
                    Walk(kv.Value, path + "[" + (Leaf(kv.Key, names) ?? kv.Key.ToString()) + "]", names, depth - 1, maxList, into);
                }
                if (dict.Count == 0) into.Add(new KeyValuePair<string, string>(path, "{}"));
                return;
            }
            if (value is IEnumerable list && !(value is string))
            {
                List<object> items = list.Cast<object>().ToList();
                into.Add(new KeyValuePair<string, string>(path + ".count", items.Count.ToString(Invariant)));
                //A list of plain values reads best as one line
                if (items.Count != 0 && items.All(o => o == null || IsPlain(o)))
                {
                    string joined = string.Join(",", items.Take(maxList * 4).Select(o => Leaf(o, names) ?? "null"));
                    if (items.Count > maxList * 4) joined += ",... #" + Hash(Encoding.UTF8.GetBytes(string.Join(",", items.Select(o => Leaf(o, names) ?? "null"))));
                    into.Add(new KeyValuePair<string, string>(path, joined));
                    return;
                }
                for (int i = 0; i < items.Count && i < maxList; i++)
                    Walk(items[i], path + "[" + i + "]", names, depth - 1, maxList, into);
                return;
            }
            foreach (FieldInfo f in FieldsOf(value.GetType()))
            {
                object v;
                try { v = f.GetValue(value); } catch { continue; }
                Walk(v, path == "" ? f.Name : path + "." + f.Name, names, depth - 1, maxList, into);
            }
        }

        private static bool IsPlain(object o) => o.GetType().IsPrimitive || o is string || o is Enum || o is ShortGuid || o is decimal;

        /// <summary>The value as text when it is a leaf (a number, text, a vector, something in a level table); null to walk into it.</summary>
        private static string Leaf(object value, ResourceNames names)
        {
            switch (value)
            {
                case null: return "null";
                case string s: return "\"" + s + "\"";
                case float f: return Num(f);
                case double d: return d.ToString("0.######", Invariant);
                case bool b: return b ? "true" : "false";
                case Enum e: return e.ToString();
                case ShortGuid g: return g.ToByteString();
                case Vector2 v2: return "(" + Num(v2.X) + ", " + Num(v2.Y) + ")";
                case Vector3 v3: return Vec(v3);
                case Vector4 v4: return "(" + Num(v4.X) + ", " + Num(v4.Y) + ", " + Num(v4.Z) + ", " + Num(v4.W) + ")";
                case Quaternion q: return "(" + Num(q.X) + ", " + Num(q.Y) + ", " + Num(q.Z) + ", " + Num(q.W) + ")";
                case Matrix4x4 m: return Matrix(m);
                case byte[] bytes: return Hash(bytes);
                case Materials.Material mat: return "material " + (names?.MaterialName(mat) ?? mat.Name);
                case Models.CS2.Component.LOD.Submesh sub:
                    if (names != null) { Newtonsoft.Json.Linq.JObject o = names.Element(new RenderableElements.Element() { Model = sub }); return "submesh " + (string)o["model"] + "#" + CathodeJson.Text(o["sub"]); }
                    return "submesh";
                case Models.CS2 model: return "model " + (names?.ModelName(model) ?? model.Name);
                case RenderableElements.Element el: return "element " + CathodeJson.Text(names != null ? names.Element(el) : new Newtonsoft.Json.Linq.JObject() { ["material"] = el.Material?.Name });
                case Textures.TEX4 tex: return "texture " + tex.Name;
                case Shaders.Shader sh: return "shader " + sh.Ubershader + "/" + sh.Technique + " " + sh.PermutationHash.ToString("x8");
                case EntityHandle h: return h.entity_id.ToByteString() + "@" + h.composite_instance_id.ToByteString();
                case Resources.Resource r: return "resource " + r.resource_id.ToByteString() + "@" + r.composite_instance_id.ToByteString();
                case HavokPackfile.PhysicsSystem ps: return "physics system " + ps.SystemIndex + " " + ps.Name;
                case HavokPackfile.StaticCompoundShape sc: return names != null ? names.ProxyName(sc) : "collision proxy " + sc.ProxyIndex;
                //An instance by what it is, not its slot in the proxy (a build or compaction reorders them)
                case HavokPackfile.CompoundInstance ci: return "collision instance " + ci.ShapeClassName + " at " + Vec(new Vector3(ci.Translation.X, ci.Translation.Y, ci.Translation.Z)) + " q(" + Num(ci.Rotation.X) + ", " + Num(ci.Rotation.Y) + ", " + Num(ci.Rotation.Z) + ", " + Num(ci.Rotation.W) + ") filter " + ci.FilterInfo + " " + (names?.MaterialName(ci.Material) ?? ci.Material?.Name);
                case MaterialMappings.MaterialMapping mm: return "mapping " + mm.Name;
                case EnvironmentAnimations.EnvironmentAnimation ea: return "environment animation " + ea.ID + " " + ea.SkeletonName;
                case Collisions.WeightedCollision _: return "weighted collision";
                case MorphTargets.MorphTarget _: return "morph target";
            }
            if (IsPlain(value)) return Convert.ToString(value, Invariant);
            return null;
        }
    }
}
