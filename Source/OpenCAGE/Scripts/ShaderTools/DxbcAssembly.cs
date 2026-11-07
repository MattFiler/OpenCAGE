using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OpenCAGE.ShaderTools
{
    /// <summary>
    /// A shader's assembly (as the Windows disassembler writes it, with hex literals) read into instructions and operands.
    /// </summary>
    public class DxbcAssembly
    {
        /// <summary>vs, ps, gs, hs, ds or cs.</summary>
        public string Stage = "";
        public int Major, Minor;
        public List<Declaration> Declarations = new List<Declaration>();
        public List<Instruction> Instructions = new List<Instruction>();
        /// <summary>The immediate constant buffer (icb), four values a row.</summary>
        public List<uint[]> ImmediateConstants = new List<uint[]>();

        /// <summary>The target to compile for, such as ps_5_0.</summary>
        public string Target => Stage + "_" + Major + "_" + Minor;

        public class Declaration
        {
            public string Op;
            /// <summary>Everything after the opcode.</summary>
            public string Arguments = "";
            public string Text;
            /// <summary>The parenthesised groups straight after the opcode, such as (float,float,float,float).</summary>
            public List<string> Groups = new List<string>();
            /// <summary>The comma-separated arguments, parsed as operands where they are one.</summary>
            public List<string> Parts = new List<string>();
            public override string ToString() => Text;
        }

        public class Operand
        {
            /// <summary>r, v, o, cb, icb, x, l, t, s, u, g, m, null, or a named register (vThreadID, oDepth, vPrim...).</summary>
            public string Type = "";
            /// <summary>The register number: r3 is 3, cb2[28] is 2, x0[1] is 0, v[0][1] is 1.</summary>
            public int Register = -1;
            /// <summary>The fixed part of an index: cb2[28] is 28, x0[r0.x + 1] is 1.</summary>
            public int Offset = -1;
            /// <summary>The register part of an index: x0[r0.x + 1] is r0.x.</summary>
            public Operand Relative;
            /// <summary>The vertex of a geometry shader input: v[2][1] is 2.</summary>
            public int Vertex = -1;
            public Operand VertexRelative;
            /// <summary>The swizzle or mask as written (xyzx, x), or empty.</summary>
            public string Swizzle = "";
            public bool Negate, Absolute;
            /// <summary>The values of a literal, l(...).</summary>
            public uint[] Values;
            public string Text;

            public bool IsNull => Type == "null";
            public bool IsLiteral => Type == "l";

            /// <summary>Components 0-3 of the mask (for a destination): r0.xz is 0, 2. No mask is all four (or one for a scalar register).</summary>
            public int[] MaskComponents()
            {
                if (Swizzle.Length == 0)
                    return new[] { 0 };
                return Swizzle.Select(Component).ToArray();
            }

            /// <summary>The component this operand gives for each destination component (a swizzle lines up with the destination's mask).</summary>
            public int[] Pick(int[] destination)
            {
                if (IsLiteral)
                    return destination.Select(c => Values.Length == 1 ? 0 : c).ToArray();
                if (Swizzle.Length == 0)
                    return destination.Select(c => 0).ToArray();
                if (Swizzle.Length == 1)
                    return destination.Select(c => Component(Swizzle[0])).ToArray();
                return destination.Select(c => Component(Swizzle[Math.Min(c, Swizzle.Length - 1)])).ToArray();
            }

            /// <summary>The first <paramref name="count"/> components, in order (for coordinates and addresses, which don't follow the destination).</summary>
            public int[] First(int count)
            {
                int[] order = new int[count];
                for (int i = 0; i < count; i++)
                {
                    if (IsLiteral) order[i] = Values.Length == 1 ? 0 : i;
                    else if (Swizzle.Length == 0) order[i] = 0;
                    else if (Swizzle.Length == 1) order[i] = Component(Swizzle[0]);
                    else order[i] = Component(Swizzle[Math.Min(i, Swizzle.Length - 1)]);
                }
                return order;
            }

            public static int Component(char c)
            {
                switch (c)
                {
                    case 'x': case 'r': return 0;
                    case 'y': case 'g': return 1;
                    case 'z': case 'b': return 2;
                    case 'w': case 'a': return 3;
                }
                throw new FormatException("'" + c + "' is not a component");
            }

            public override string ToString() => Text;
        }

        public class Instruction
        {
            /// <summary>The operation without its modifiers: sample_l, mad, ld_structured, if_nz...</summary>
            public string Op;
            public bool Saturate;
            /// <summary>Immediate texel offsets (aoffimmi), or null.</summary>
            public int[] Offsets;
            /// <summary>texture2d, structured_buffer, raw_buffer, buffer... where the opcode names one.</summary>
            public string Dimension;
            public int Stride;
            /// <summary>The return types where the opcode names them: float,float,float,float.</summary>
            public string Returns;
            /// <summary>What follows the opcode's brackets, such as _uint on resinfo.</summary>
            public string Suffix = "";
            public List<Operand> Operands = new List<Operand>();
            public string Text;
            public override string ToString() => Text;
        }

        public static DxbcAssembly Parse(string text)
        {
            DxbcAssembly assembly = new DxbcAssembly();
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            bool headerRead = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("//"))
                    continue;
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0)
                    line = line.Substring(0, comment).Trim();
                if (!headerRead)
                {
                    //vs_5_0
                    string[] model = line.Split('_');
                    if (model.Length == 3 && model[0].Length == 2)
                    {
                        assembly.Stage = model[0];
                        assembly.Major = int.Parse(model[1]);
                        assembly.Minor = int.Parse(model[2]);
                        headerRead = true;
                        continue;
                    }
                }
                if (line.StartsWith("dcl_immediateConstantBuffer"))
                {
                    //Runs over several lines until its braces close
                    StringBuilder block = new StringBuilder(line);
                    while (Depth(block.ToString(), '{', '}') > 0 && i + 1 < lines.Length)
                        block.Append(' ').Append(lines[++i].Trim());
                    string body = block.ToString();
                    int open = body.IndexOf('{');
                    foreach (string row in body.Substring(open + 1, body.LastIndexOf('}') - open - 1).Split('}'))
                    {
                        int start = row.IndexOf('{');
                        if (start < 0) continue;
                        uint[] values = row.Substring(start + 1).Split(',').Select(o => o.Trim()).Where(o => o.Length != 0).Select(ParseValue).ToArray();
                        uint[] padded = new uint[4];
                        Array.Copy(values, padded, Math.Min(4, values.Length));
                        assembly.ImmediateConstants.Add(padded);
                    }
                    assembly.Declarations.Add(new Declaration() { Op = "dcl_immediateConstantBuffer", Text = body });
                    continue;
                }

                SplitOpcode(line, out string opcode, out string rest);
                if (opcode.StartsWith("dcl_"))
                {
                    Declaration declaration = new Declaration() { Text = line, Arguments = rest };
                    string head = Groups(opcode, declaration.Groups, out string tail);
                    declaration.Op = head + tail;
                    //The type group can also follow the opcode after a space: dcl_resource_texture2d (float,float,float,float) t0
                    string arguments = rest;
                    while (arguments.StartsWith("("))
                    {
                        int close = Close(arguments, 0);
                        declaration.Groups.Add(arguments.Substring(1, close - 1));
                        arguments = arguments.Substring(close + 1).Trim();
                    }
                    declaration.Parts = SplitTopLevel(arguments).Select(o => o.Trim()).Where(o => o.Length != 0).ToList();
                    assembly.Declarations.Add(declaration);
                    continue;
                }

                Instruction instruction = new Instruction() { Text = line };
                string name = Groups(opcode, null, out string suffix, out List<string> groups);
                if (name.EndsWith("_sat")) { instruction.Saturate = true; name = name.Substring(0, name.Length - 4); }
                if (suffix.Contains("_sat")) { instruction.Saturate = true; suffix = suffix.Replace("_sat", ""); }
                instruction.Suffix = suffix;
                bool offsets = name.Contains("_aoffimmi");
                name = name.Replace("_indexable", "").Replace("_aoffimmi", "");
                foreach (string group in groups)
                {
                    string g = group.Trim();
                    if (offsets && instruction.Offsets == null && g.Length != 0 && (char.IsDigit(g[0]) || g[0] == '-'))
                    {
                        instruction.Offsets = g.Split(',').Select(o => int.Parse(o.Trim(), CultureInfo.InvariantCulture)).ToArray();
                        continue;
                    }
                    if (g.StartsWith("texture") || g.StartsWith("buffer") || g.StartsWith("structured_buffer") || g.StartsWith("raw_buffer"))
                    {
                        string[] bits = g.Split(',');
                        instruction.Dimension = bits[0].Trim();
                        foreach (string bit in bits.Skip(1))
                            if (bit.Trim().StartsWith("stride="))
                                instruction.Stride = int.Parse(bit.Trim().Substring(7), CultureInfo.InvariantCulture);
                        continue;
                    }
                    instruction.Returns = g;
                }
                instruction.Op = name;
                foreach (string part in SplitTopLevel(rest))
                    if (part.Trim().Length != 0)
                        instruction.Operands.Add(ParseOperand(part.Trim()));
                assembly.Instructions.Add(instruction);
            }
            return assembly;
        }

        /// <summary>The declarations with this opcode.</summary>
        public IEnumerable<Declaration> Declared(string op) => Declarations.Where(o => o.Op == op);

        #region PARSING
        private static int Depth(string text, char open, char close)
        {
            int depth = 0;
            foreach (char c in text) { if (c == open) depth++; else if (c == close) depth--; }
            return depth;
        }

        private static int Close(string text, int open)
        {
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')' && --depth == 0) return i;
            }
            return text.Length - 1;
        }

        /* The opcode runs to the first space outside brackets (its groups can hold spaces: (structured_buffer, stride=48)) */
        private static void SplitOpcode(string line, out string opcode, out string rest)
        {
            int depth = 0;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ' ' && depth == 0)
                {
                    opcode = line.Substring(0, i);
                    rest = line.Substring(i + 1).Trim();
                    return;
                }
            }
            opcode = line;
            rest = "";
        }

        private static string Groups(string opcode, List<string> into, out string tail)
        {
            string head = Groups(opcode, into, out tail, out List<string> groups);
            into?.AddRange(groups.Where(g => !into.Contains(g)));
            return head;
        }

        private static string Groups(string opcode, List<string> unused, out string tail, out List<string> groups)
        {
            groups = new List<string>();
            int open = opcode.IndexOf('(');
            if (open < 0) { tail = ""; return opcode; }
            string head = opcode.Substring(0, open);
            int i = open;
            while (i < opcode.Length && opcode[i] == '(')
            {
                int close = Close(opcode, i);
                groups.Add(opcode.Substring(i + 1, close - i - 1));
                i = close + 1;
            }
            tail = i < opcode.Length ? opcode.Substring(i) : "";
            return head;
        }

        /* Commas outside () and [] */
        private static List<string> SplitTopLevel(string text)
        {
            List<string> parts = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    parts.Add(text.Substring(start, i - start));
                    start = i + 1;
                }
            }
            parts.Add(text.Substring(start));
            return parts;
        }

        public static uint ParseValue(string text)
        {
            text = text.Trim();
            if (text.StartsWith("0x") || text.StartsWith("0X"))
                return uint.Parse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (text.StartsWith("-0x"))
                return unchecked((uint)-(int)uint.Parse(text.Substring(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            if (text.Contains(".") || text.Contains("e") || text.Contains("E") || text.Contains("INF") || text.Contains("NaN"))
            {
                float f = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                return BitConverter.ToUInt32(BitConverter.GetBytes(f), 0);
            }
            return unchecked((uint)long.Parse(text, CultureInfo.InvariantCulture));
        }

        public static Operand ParseOperand(string text)
        {
            Operand operand = new Operand() { Text = text };
            string t = text.Trim();
            if (t.StartsWith("-")) { operand.Negate = true; t = t.Substring(1).Trim(); }
            if (t.StartsWith("|"))
            {
                operand.Absolute = true;
                int end = t.LastIndexOf('|');
                t = t.Substring(1, end - 1).Trim();
            }
            if (t.StartsWith("l("))
            {
                operand.Type = "l";
                operand.Values = t.Substring(2, t.LastIndexOf(')') - 2).Split(',').Select(ParseValue).ToArray();
                return operand;
            }
            if (t == "null") { operand.Type = "null"; return operand; }

            int i = 0;
            while (i < t.Length && (char.IsLetter(t[i]) || t[i] == '_')) i++;
            string letters = t.Substring(0, i);
            int digitsStart = i;
            while (i < t.Length && char.IsDigit(t[i])) i++;
            if (i > digitsStart)
                operand.Register = int.Parse(t.Substring(digitsStart, i - digitsStart), CultureInfo.InvariantCulture);
            operand.Type = letters;

            List<string> indexes = new List<string>();
            while (i < t.Length && t[i] == '[')
            {
                int depth = 0, start = i;
                for (; i < t.Length; i++)
                {
                    if (t[i] == '[') depth++;
                    else if (t[i] == ']' && --depth == 0) break;
                }
                indexes.Add(t.Substring(start + 1, i - start - 1));
                i++;
            }
            if (i < t.Length && t[i] == '.')
                operand.Swizzle = t.Substring(i + 1).Trim();

            if (letters == "v" && indexes.Count == 2)
            {
                ParseIndex(indexes[0], out operand.Vertex, out operand.VertexRelative);
                ParseIndex(indexes[1], out int register, out Operand relative);
                operand.Register = register;
            }
            else if (indexes.Count == 1)
            {
                if (operand.Register < 0 && letters != "icb")
                {
                    //v[3] style: the index is the register
                    ParseIndex(indexes[0], out int register, out Operand relative);
                    operand.Register = register;
                    operand.Relative = relative;
                }
                else
                {
                    ParseIndex(indexes[0], out operand.Offset, out operand.Relative);
                }
            }
            return operand;
        }

        private static void ParseIndex(string text, out int offset, out Operand relative)
        {
            text = text.Trim();
            relative = null;
            int plus = text.LastIndexOf('+');
            if (plus > 0)
            {
                relative = ParseOperand(text.Substring(0, plus).Trim());
                offset = int.Parse(text.Substring(plus + 1).Trim(), CultureInfo.InvariantCulture);
                return;
            }
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out offset))
                return;
            relative = ParseOperand(text);
            offset = 0;
        }
        #endregion
    }
}
