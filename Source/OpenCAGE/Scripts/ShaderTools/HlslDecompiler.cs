using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using static OpenCAGE.ShaderTools.DxbcAssembly;
using static OpenCAGE.ShaderTools.DxbcReflection;
using Type = OpenCAGE.ShaderTools.DxbcReflection.Type;

namespace OpenCAGE.ShaderTools
{
    /// <summary>
    /// Turns compiled shader bytecode (DXBC, shader model 4/5) back into HLSL that compiles to a shader doing the same
    /// thing, with the same inputs, outputs and bindings. Registers become float4 temporaries (r0, r1...) and integer work
    /// is done through asint/asuint/asfloat, so every value keeps its exact bits; constant buffers, textures, samplers and
    /// buffers keep the names the shader was written with, so what it reads shows by name (Time.x, ViewProj...).
    /// </summary>
    public class HlslDecompiler
    {
        /// <summary>The shader as HLSL with a main() entry point. Throws <see cref="NotSupportedException"/> naming what it can't decompile.</summary>
        public static string Decompile(byte[] bytecode, string title = null)
        {
            string text = D3DCompiler.Disassemble(bytecode, D3DCompiler.DISASM_PRINT_HEX_LITERALS);
            if (text == null)
                throw new NotSupportedException("The shader couldn't be disassembled: the bytecode isn't valid, or the Windows HLSL compiler (d3dcompiler_47.dll) is missing.");
            DxbcAssembly assembly = DxbcAssembly.Parse(text);
            DxbcReflection reflection = DxbcReflection.Read(bytecode);

            //A constant buffer read in a way its variables can't name (an index into something other than an array of
            //single-register elements) is declared as a plain array of float4s instead: found by trying, and retried
            HashSet<string> raw = new HashSet<string>();
            for (int attempt = 0; attempt < 64; attempt++)
            {
                try
                {
                    return new HlslDecompiler(assembly, reflection, raw, title).Write();
                }
                catch (RawFallback fallback)
                {
                    if (!raw.Add(fallback.Key))
                        throw new NotSupportedException("Couldn't decompile reads of " + fallback.Key + ".");
                }
            }
            throw new NotSupportedException("Couldn't decompile the shader's constant buffers.");
        }

        /// <summary>The compile target the bytecode was built for (ps_5_0...), or null.</summary>
        public static string TargetOf(byte[] bytecode)
        {
            string text = D3DCompiler.Disassemble(bytecode, D3DCompiler.DISASM_PRINT_HEX_LITERALS);
            if (text == null) return null;
            DxbcAssembly assembly = DxbcAssembly.Parse(text);
            return assembly.Stage.Length == 0 ? null : assembly.Target;
        }

        #region STATE
        private enum V { Float, Int, UInt }

        private class Leaf
        {
            public string Base;
            /// <summary>x, y... for a vector, _m01 for a matrix element, empty for a scalar.</summary>
            public string Select = "";
            public V Type;
            public bool Bool;
        }

        private class RawFallback : Exception
        {
            public string Key;
            public RawFallback(string key) : base(key) { Key = key; }
        }

        private class ConstantView
        {
            public string Name;
            public int Slot;
            public ConstantBuffer Buffer;
            public bool Raw;
            public Dictionary<(int, int), Leaf> Leaves = new Dictionary<(int, int), Leaf>();
            /// <summary>Arrays whose elements are one register each, for indexed reads: variable, first register, element count.</summary>
            public List<(string name, int first, int count, Type element)> Arrays = new List<(string, int, int, Type)>();
        }

        private class BufferView
        {
            public string Name;
            public Binding Binding;
            public int Stride;
            public Type Element;
            public bool Generic;
            public string TypeName;
            public Dictionary<int, Leaf> Dwords = new Dictionary<int, Leaf>();
        }

        private readonly DxbcAssembly _asm;
        private readonly DxbcReflection _refl;
        private readonly HashSet<string> _raw;
        private readonly string _title;
        private readonly StringBuilder _code = new StringBuilder();
        private int _indent = 1;
        private int _blockTemps = 0;

        private readonly Dictionary<int, ConstantView> _constants = new Dictionary<int, ConstantView>();
        private readonly Dictionary<(int, int, int), Leaf> _inputs = new Dictionary<(int, int, int), Leaf>();
        private readonly Dictionary<string, Leaf[]> _specials = new Dictionary<string, Leaf[]>();
        private readonly Dictionary<string, BufferView> _buffers = new Dictionary<string, BufferView>();
        private readonly Dictionary<int, (int dwords, int stride)> _groupShared = new Dictionary<int, (int, int)>();
        private readonly List<string> _outputCopies = new List<string>();
        private readonly HashSet<string> _usedNames = new HashSet<string>();
        private readonly List<string> _structs = new List<string>();
        private readonly Dictionary<Type, string> _structNames = new Dictionary<Type, string>();
        //Append buffers: the register an imm_atomic_alloc put the new element's index in, and the last store into it
        private readonly Dictionary<string, (string index, int lastStore)> _appends = new Dictionary<string, (string, int)>();
        private int _instructionIndex;

        //Each register's storage: float4, or uint4 when it ever holds integer bits (a float4 would let the compiler flush
        //small integers such as 1 and 2, which are denormal floats, to zero). Found by a dry run of the translation.
        private readonly Dictionary<string, V> _storage = new Dictionary<string, V>();
        private bool _dryRun;
        private readonly HashSet<string> _bitsWrites = new HashSet<string>();
        private readonly List<(string from, string to)> _links = new List<(string, string)>();

        private HlslDecompiler(DxbcAssembly assembly, DxbcReflection reflection, HashSet<string> raw, string title)
        {
            _asm = assembly;
            _refl = reflection;
            _raw = raw;
            _title = title;
        }
        #endregion

        #region WRITING
        private string Write()
        {
            foreach (string reserved in new[] { "main", "icb", "stream", "gsin", "GSIn", "GSOut" })
                _usedNames.Add(reserved);

            string resources = Resources();
            string signature = Signature(out string attributes, out string preamble);
            //A dry run finds which registers hold integer bits; the real run then writes them as uint4
            _dryRun = true;
            TranslateAll();
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var link in _links)
                    if (_bitsWrites.Contains(link.from) && _bitsWrites.Add(link.to))
                        changed = true;
            }
            foreach (string key in _bitsWrites)
                if (!key.StartsWith("o"))
                    _storage[key] = V.UInt;
            _dryRun = false;
            string temps = Temps();
            TranslateAll();

            StringBuilder hlsl = new StringBuilder();
            hlsl.AppendLine("// " + (_title ?? "Shader") + " (" + _asm.Target + "), decompiled by OpenCAGE from its bytecode.");
            hlsl.AppendLine("// r0, r1... are the shader's registers; integer work goes through asint/asuint/asfloat so values keep their exact bits.");
            if (_refl.Creator.Length != 0)
                hlsl.AppendLine("// Originally built with: " + _refl.Creator);
            hlsl.AppendLine();
            foreach (string s in _structs)
                hlsl.AppendLine(s);
            hlsl.Append(resources);
            hlsl.Append(preamble);
            if (attributes.Length != 0)
                hlsl.AppendLine(attributes);
            hlsl.AppendLine("void main(" + signature + ")");
            hlsl.AppendLine("{");
            hlsl.Append(temps);
            hlsl.Append(_code);
            hlsl.AppendLine("}");
            return hlsl.ToString();
        }

        private void TranslateAll()
        {
            _code.Clear();
            _indent = 1;
            _blockTemps = 0;
            _appends.Clear();
            for (_instructionIndex = 0; _instructionIndex < _asm.Instructions.Count; _instructionIndex++)
                Translate(_asm.Instructions[_instructionIndex]);
        }

        /// <summary>How a register (r0, x1, o2) is stored.</summary>
        private V Storage(string key) => _storage.TryGetValue(key, out V v) ? v : V.Float;

        private static string Key(Operand op) => op.Type + op.Register;

        private static bool IsRegister(Operand op) => op.Type == "r" || op.Type == "x" || op.Type == "o";

        /* The immediate constant buffer holds only ordinary floats (so it can be declared as float4 without changing a bit) */
        private bool IcbIsFloat => !_asm.ImmediateConstants.Any(row => row.Any(OddBits));

        /* A literal whose bits aren't an ordinary float (a small integer is a denormal): kept exact only in uint storage */
        private static bool OddBits(uint bits)
        {
            uint exponent = (bits >> 23) & 0xFF;
            return bits != 0 && (exponent == 0 || exponent == 0xFF || bits == 0x80000000);
        }

        private void Line(string text)
        {
            _code.Append(' ', _indent * 4).AppendLine(text);
        }

        private string Unique(string name)
        {
            string clean = new string((name ?? "").Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
            if (clean.Length == 0 || char.IsDigit(clean[0])) clean = "_" + clean;
            if (Keywords.Contains(clean)) clean += "_";
            string unique = clean;
            for (int i = 2; _usedNames.Contains(unique); i++)
                unique = clean + "_" + i;
            _usedNames.Add(unique);
            return unique;
        }

        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "float", "int", "uint", "bool", "half", "double", "true", "false", "in", "out", "inout", "return", "if", "else", "for", "while", "do",
            "switch", "case", "default", "break", "continue", "discard", "struct", "cbuffer", "tbuffer", "register", "packoffset", "static", "const",
            "sampler", "texture", "point", "line", "triangle", "lineadj", "triangleadj", "linear", "centroid", "nointerpolation", "noperspective",
            "sample", "groupshared", "uniform", "vector", "matrix", "string", "pass", "technique", "compile", "shared", "volatile", "precise",
        };
        #endregion

        #region DECLARATIONS
        private string Resources()
        {
            StringBuilder text = new StringBuilder();

            //Constant buffers
            foreach (Binding binding in _refl.Bindings.Where(o => o.Type == InputType.CBuffer).OrderBy(o => o.BindPoint))
            {
                ConstantBuffer buffer = _refl.ConstantBuffers.FirstOrDefault(o => o.Name == binding.Name && o.Kind == 0);
                int registers = (buffer?.Size ?? 16) / 16;
                foreach (Declaration d in _asm.Declared("dcl_constantbuffer"))
                {
                    Operand cb = ParseOperand(d.Parts[0]);
                    if (cb.Register == binding.BindPoint) registers = Math.Max(registers, cb.Offset);
                }
                ConstantView view = new ConstantView() { Slot = binding.BindPoint, Buffer = buffer };
                string name = Unique(binding.Name);
                bool raw = buffer == null || _raw.Contains("cb" + binding.BindPoint) || buffer.Variables.Any(v => HasBool(v.Type));
                if (!raw)
                {
                    try
                    {
                        StringBuilder members = new StringBuilder();
                        foreach (Variable variable in buffer.Variables)
                        {
                            string varName = Unique(variable.Name);
                            string type = TypeDeclaration(variable.Type);
                            int reg = variable.Offset / 16, comp = (variable.Offset % 16) / 4;
                            members.AppendLine("    " + type + " " + varName + (variable.Type.Elements > 0 ? "[" + variable.Type.Elements + "]" : "") + " : packoffset(c" + reg + (comp != 0 || IsSmall(variable.Type) ? "." + "xyzw"[comp] : "") + ");" + (variable.Used ? "" : " // unused"));
                            Constants(view, variable.Type, varName, variable.Offset, true);
                        }
                        text.AppendLine("cbuffer " + name + " : register(b" + binding.BindPoint + ")");
                        text.AppendLine("{");
                        text.Append(members);
                        text.AppendLine("}");
                        text.AppendLine();
                        view.Name = name;
                        _constants[binding.BindPoint] = view;
                        continue;
                    }
                    catch (NotSupportedException) { }
                }
                //As a plain array of registers
                view.Raw = true;
                view.Name = name;
                view.Leaves.Clear();
                text.AppendLine("cbuffer " + name + " : register(b" + binding.BindPoint + ")");
                text.AppendLine("{");
                text.AppendLine("    float4 " + name + "_c[" + Math.Max(1, registers) + "];");
                if (buffer != null)
                    foreach (Variable variable in buffer.Variables)
                        text.AppendLine("    // c" + (variable.Offset / 16) + "." + "xyzw"[(variable.Offset % 16) / 4] + ": " + TypeDeclaration(variable.Type) + " " + variable.Name + (variable.Type.Elements > 0 ? "[" + variable.Type.Elements + "]" : ""));
                text.AppendLine("}");
                text.AppendLine();
                view.Name = name + "_c";
                _constants[binding.BindPoint] = view;
            }

            //Textures, buffers, samplers and UAVs
            foreach (Binding binding in _refl.Bindings.Where(o => o.Type != InputType.CBuffer && o.Type != InputType.TBuffer).OrderBy(o => Space(o.Type)).ThenBy(o => o.BindPoint))
            {
                string name = Unique(binding.Name);
                _bindingNames[binding] = name;
                string array = binding.BindCount > 1 ? "[" + binding.BindCount + "]" : "";
                string reg = " : register(" + Space(binding.Type) + binding.BindPoint + ")";
                switch (binding.Type)
                {
                    case InputType.Sampler:
                        text.AppendLine((binding.ComparisonSampler || SamplerIsComparison(binding.BindPoint) ? "SamplerComparisonState " : "SamplerState ") + name + array + reg + ";");
                        break;
                    case InputType.Texture:
                        text.AppendLine(TextureType(binding, false) + " " + name + array + reg + ";");
                        break;
                    case InputType.UavRWTyped:
                        text.AppendLine(TextureType(binding, true) + " " + name + array + reg + ";");
                        break;
                    case InputType.ByteAddress:
                        text.AppendLine("ByteAddressBuffer " + name + array + reg + ";");
                        break;
                    case InputType.UavRWByteAddress:
                        text.AppendLine("RWByteAddressBuffer " + name + array + reg + ";");
                        break;
                    case InputType.Structured:
                    case InputType.UavRWStructured:
                    case InputType.UavRWStructuredWithCounter:
                    case InputType.UavAppendStructured:
                    case InputType.UavConsumeStructured:
                        {
                            BufferView view = StructuredView(binding, name);
                            string kind = binding.Type == InputType.Structured ? "StructuredBuffer" : binding.Type == InputType.UavAppendStructured ? "AppendStructuredBuffer" : binding.Type == InputType.UavConsumeStructured ? "ConsumeStructuredBuffer" : "RWStructuredBuffer";
                            text.AppendLine(kind + "<" + view.TypeName + "> " + name + array + reg + ";");
                            break;
                        }
                    default:
                        throw new NotSupportedException("Bindings of type " + binding.Type + " (" + binding.Name + ") aren't supported.");
                }
            }

            //Groupshared memory, as plain arrays of 32-bit values
            foreach (Declaration d in _asm.Declarations.Where(o => o.Op == "dcl_tgsm_raw" || o.Op == "dcl_tgsm_structured"))
            {
                Operand g = ParseOperand(d.Parts[0]);
                int dwords, stride;
                if (d.Op == "dcl_tgsm_raw") { dwords = int.Parse(d.Parts[1], CultureInfo.InvariantCulture) / 4; stride = 0; }
                else { stride = int.Parse(d.Parts[1], CultureInfo.InvariantCulture); dwords = stride * int.Parse(d.Parts[2], CultureInfo.InvariantCulture) / 4; }
                _groupShared[g.Register] = (dwords, stride);
                text.AppendLine("groupshared uint g" + g.Register + "[" + Math.Max(1, dwords) + "];" + (stride != 0 ? " // " + (dwords * 4 / stride) + " elements of " + stride + " bytes" : ""));
            }

            //Immediate constants: floats when every value is an ordinary float (so the compiler keeps it a table, as the
            //original had it, rather than working the values out), else their exact bits
            if (_asm.ImmediateConstants.Count != 0)
            {
                bool floats = IcbIsFloat;
                text.AppendLine("static const " + (floats ? "float4" : "uint4") + " icb[" + _asm.ImmediateConstants.Count + "] =");
                text.AppendLine("{");
                for (int i = 0; i < _asm.ImmediateConstants.Count; i++)
                {
                    string comma = i < _asm.ImmediateConstants.Count - 1 ? "," : "";
                    if (floats)
                        text.AppendLine("    float4(" + string.Join(", ", _asm.ImmediateConstants[i].Select(FloatText)) + ")" + comma);
                    else
                        text.AppendLine("    uint4(" + string.Join(", ", _asm.ImmediateConstants[i].Select(o => "0x" + o.ToString("x8"))) + ")" + comma + " // " + string.Join(", ", _asm.ImmediateConstants[i].Select(o => FloatText(o))));
                }
                text.AppendLine("};");
            }
            if (text.Length != 0)
                text.AppendLine();
            return text.ToString();
        }

        private bool SamplerIsComparison(int slot)
        {
            foreach (Declaration d in _asm.Declared("dcl_sampler"))
                if (ParseOperand(d.Parts[0]).Register == slot && d.Parts.Count > 1 && d.Parts[1].Contains("comparison"))
                    return true;
            return false;
        }

        private static bool HasBool(Type type)
        {
            if (type.Class == VariableClass.Struct) return type.Members.Any(m => HasBool(m.Type));
            return type.Base == VariableType.Bool || type.Base == VariableType.Double || type.Class == VariableClass.Object;
        }

        private static bool IsSmall(Type type)
        {
            return type.Elements == 0 && (type.Class == VariableClass.Scalar || type.Class == VariableClass.Vector);
        }

        private string TypeDeclaration(Type type)
        {
            if (type.Class == VariableClass.Struct)
                return StructName(type, false);
            return type.HlslName;
        }

        /* A struct declared for a constant buffer variable (padded cbuffer packing, which the members' own offsets already
           follow) or a structured buffer element (tightly packed) */
        private string StructName(Type type, bool packed)
        {
            if (_structNames.TryGetValue(type, out string known))
                return known;
            string name = Unique(type.Name.Length != 0 && !type.Name.Contains(" ") ? type.Name : "Struct");
            _structNames[type] = name;
            StringBuilder s = new StringBuilder();
            s.AppendLine("struct " + name);
            s.AppendLine("{");
            foreach (Member member in type.Members)
                s.AppendLine("    " + TypeDeclaration(member.Type) + " " + member.Name + (member.Type.Elements > 0 ? "[" + member.Type.Elements + "]" : "") + ";");
            s.AppendLine("};");
            _structs.Add(s.ToString());
            return name;
        }

        private static string ScalarName(VariableType type)
        {
            switch (type)
            {
                case VariableType.Int: return "int";
                case VariableType.UInt: case VariableType.UInt8: return "uint";
                case VariableType.Bool: return "bool";
                default: return "float";
            }
        }

        private static V ViewOf(VariableType type)
        {
            switch (type)
            {
                case VariableType.Int: return V.Int;
                case VariableType.UInt: case VariableType.UInt8: case VariableType.Bool: return V.UInt;
                default: return V.Float;
            }
        }

        /* Every component of a constant buffer variable, by register and component (cbuffer packing: arrays and
           structs start each element on a new register, matrices take a register a column, or a row if row_major) */
        private void Constants(ConstantView view, Type type, string path, int offset, bool top)
        {
            if (type.Elements > 0)
            {
                Type element = new Type() { Class = type.Class, Base = type.Base, Rows = type.Rows, Columns = type.Columns, Elements = 0, Name = type.Name, Members = type.Members };
                int stride = (CbSize(element) + 15) / 16 * 16;
                if (stride == 16 && element.Class != VariableClass.Struct)
                    view.Arrays.Add((path, offset / 16, type.Elements, element));
                for (int i = 0; i < type.Elements; i++)
                    Constants(view, element, path + "[" + i + "]", offset + i * stride, false);
                return;
            }
            V v = ViewOf(type.Base);
            int reg = offset / 16, comp = (offset % 16) / 4;
            switch (type.Class)
            {
                case VariableClass.Scalar:
                    view.Leaves[(reg, comp)] = new Leaf() { Base = path, Type = v };
                    break;
                case VariableClass.Vector:
                    for (int i = 0; i < type.Columns; i++)
                        view.Leaves[(reg, comp + i)] = new Leaf() { Base = path, Select = "xyzw"[i].ToString(), Type = v };
                    break;
                case VariableClass.MatrixColumns:
                    for (int c = 0; c < type.Columns; c++)
                        for (int r = 0; r < type.Rows; r++)
                            view.Leaves[(reg + c, r)] = new Leaf() { Base = path, Select = "_m" + r + c, Type = v };
                    break;
                case VariableClass.MatrixRows:
                    for (int r = 0; r < type.Rows; r++)
                        for (int c = 0; c < type.Columns; c++)
                            view.Leaves[(reg + r, c)] = new Leaf() { Base = path, Select = "_m" + r + c, Type = v };
                    break;
                case VariableClass.Struct:
                    foreach (Member member in type.Members)
                        Constants(view, member.Type, path + "." + member.Name, offset + member.Offset, false);
                    break;
                default:
                    throw new NotSupportedException("constant of class " + type.Class);
            }
        }

        /* Bytes a type takes in a constant buffer, not counting padding after it */
        private static int CbSize(Type type)
        {
            int one;
            switch (type.Class)
            {
                case VariableClass.Scalar: one = 4; break;
                case VariableClass.Vector: one = 4 * type.Columns; break;
                case VariableClass.MatrixColumns: one = 16 * (type.Columns - 1) + 4 * type.Rows; break;
                case VariableClass.MatrixRows: one = 16 * (type.Rows - 1) + 4 * type.Columns; break;
                case VariableClass.Struct: one = type.Members.Count == 0 ? 0 : type.Members.Max(m => m.Offset + CbSize(m.Type)); break;
                default: one = 16; break;
            }
            if (type.Elements > 1)
                return (one + 15) / 16 * 16 * (type.Elements - 1) + one;
            return one;
        }

        private string TextureType(Binding binding, bool uav)
        {
            string component;
            switch (binding.Return)
            {
                case ReturnType.UInt: component = "uint"; break;
                case ReturnType.SInt: component = "int"; break;
                case ReturnType.UNorm: component = "unorm float"; break;
                case ReturnType.SNorm: component = "snorm float"; break;
                default: component = "float"; break;
            }
            if (binding.Components > 1) component += binding.Components;
            string kind;
            switch (binding.Dimension)
            {
                case Dimension.Buffer: kind = "Buffer"; break;
                case Dimension.Texture1D: kind = "Texture1D"; break;
                case Dimension.Texture1DArray: kind = "Texture1DArray"; break;
                case Dimension.Texture2D: kind = "Texture2D"; break;
                case Dimension.Texture2DArray: kind = "Texture2DArray"; break;
                case Dimension.Texture2DMS: kind = "Texture2DMS"; break;
                case Dimension.Texture2DMSArray: kind = "Texture2DMSArray"; break;
                case Dimension.Texture3D: kind = "Texture3D"; break;
                case Dimension.TextureCube: kind = "TextureCube"; break;
                case Dimension.TextureCubeArray: kind = "TextureCubeArray"; break;
                default: throw new NotSupportedException("Textures of dimension " + binding.Dimension + " (" + binding.Name + ") aren't supported.");
            }
            if (uav) kind = "RW" + kind;
            if (binding.Dimension == Dimension.Texture2DMS || binding.Dimension == Dimension.Texture2DMSArray)
                return kind + "<" + component + (binding.NumSamples > 0 ? ", " + binding.NumSamples : "") + ">";
            return kind + "<" + component + ">";
        }

        private BufferView StructuredView(Binding binding, string name)
        {
            BufferView view = new BufferView() { Name = name, Binding = binding, Stride = binding.NumSamples };
            ConstantBuffer layout = _refl.ConstantBuffers.FirstOrDefault(o => o.Kind == 3 && o.Name == binding.Name);
            Type element = layout?.Variables.FirstOrDefault()?.Type;
            string key = Space(binding.Type) + binding.BindPoint;
            if (element != null && !_raw.Contains(key) && !HasBool(element) && element.PackedSize == view.Stride)
            {
                try
                {
                    Dwords(view, element, "", 0);
                    view.Element = element;
                    view.TypeName = element.Class == VariableClass.Struct ? StructName(element, true) : element.HlslName;
                }
                catch (NotSupportedException) { view.Dwords.Clear(); }
            }
            if (view.Element == null)
            {
                view.Generic = true;
                view.TypeName = Unique(name + "_element");
                _structs.Add("struct " + view.TypeName + "\n{\n    uint d[" + Math.Max(1, view.Stride / 4) + "]; // " + view.Stride + " bytes\n};\n");
                for (int i = 0; i < view.Stride / 4; i++)
                    view.Dwords[i] = new Leaf() { Base = ".d[" + i + "]", Type = V.UInt };
            }
            _buffers[key] = view;
            return view;
        }

        /* Every 32-bit value of a structured buffer element, by its index in the element (tightly packed) */
        private void Dwords(BufferView view, Type type, string path, int offset)
        {
            if (type.Elements > 0)
            {
                Type element = new Type() { Class = type.Class, Base = type.Base, Rows = type.Rows, Columns = type.Columns, Elements = 0, Name = type.Name, Members = type.Members };
                int size = element.PackedSize;
                for (int i = 0; i < type.Elements; i++)
                    Dwords(view, element, path + "[" + i + "]", offset + i * size);
                return;
            }
            V v = ViewOf(type.Base);
            int at = offset / 4;
            switch (type.Class)
            {
                case VariableClass.Scalar:
                    view.Dwords[at] = new Leaf() { Base = path, Type = v };
                    break;
                case VariableClass.Vector:
                    for (int i = 0; i < type.Columns; i++)
                        view.Dwords[at + i] = new Leaf() { Base = path, Select = "xyzw"[i].ToString(), Type = v };
                    break;
                case VariableClass.MatrixColumns:
                    for (int c = 0; c < type.Columns; c++)
                        for (int r = 0; r < type.Rows; r++)
                            view.Dwords[at + c * type.Rows + r] = new Leaf() { Base = path, Select = "_m" + r + c, Type = v };
                    break;
                case VariableClass.MatrixRows:
                    for (int r = 0; r < type.Rows; r++)
                        for (int c = 0; c < type.Columns; c++)
                            view.Dwords[at + r * type.Columns + c] = new Leaf() { Base = path, Select = "_m" + r + c, Type = v };
                    break;
                case VariableClass.Struct:
                    foreach (Member member in type.Members)
                        Dwords(view, member.Type, path + "." + member.Name, offset + member.Offset);
                    break;
                default:
                    throw new NotSupportedException("buffer member of class " + type.Class);
            }
        }
        #endregion

        #region SIGNATURE
        private static readonly Dictionary<string, (string semantic, string type)> SpecialInputs = new Dictionary<string, (string, string)>()
        {
            { "vThreadID", ("SV_DispatchThreadID", "uint3") },
            { "vThreadGroupID", ("SV_GroupID", "uint3") },
            { "vThreadIDInGroup", ("SV_GroupThreadID", "uint3") },
            { "vThreadIDInGroupFlattened", ("SV_GroupIndex", "uint") },
            { "vPrim", ("SV_PrimitiveID", "uint") },
            { "vCoverage", ("SV_Coverage", "uint") },
            { "vGSInstanceID", ("SV_GSInstanceID", "uint") },
        };

        private static string Semantic(SignatureElement e)
        {
            if (e.Name.StartsWith("SV_", StringComparison.OrdinalIgnoreCase))
                return e.SemanticIndex == 0 ? e.Name : e.Name + e.SemanticIndex;
            return e.Name + e.SemanticIndex;
        }

        private static int Width(SignatureElement e)
        {
            int first = -1, last = -1;
            for (int i = 0; i < 4; i++)
                if ((e.Mask & (1 << i)) != 0) { if (first < 0) first = i; last = i; }
            return first < 0 ? 1 : last - first + 1;
        }

        private static V ViewOf(SignatureElement e) => e.ComponentType == 1 ? V.UInt : e.ComponentType == 2 ? V.Int : V.Float;

        private static string ElementType(SignatureElement e)
        {
            if (e.SystemValue == 9) return "bool"; //SV_IsFrontFace
            string scalar = e.ComponentType == 1 ? "uint" : e.ComponentType == 2 ? "int" : "float";
            int width = Width(e);
            return width == 1 ? scalar : scalar + width;
        }

        private static string Letters(int mask)
        {
            string s = "";
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) s += "xyzw"[i];
            return s;
        }

        private string ElementName(string prefix, SignatureElement e, List<SignatureElement> all)
        {
            bool shared = all.Count(o => o.Register == e.Register && o.Register >= 0) > 1;
            return Unique(prefix + e.Register + (shared || e.FirstComponent != 0 ? "_" + Letters(e.Mask) : ""));
        }

        private void AddLeaves(int vertex, SignatureElement e, string name)
        {
            int width = Width(e);
            for (int c = 0; c < 4; c++)
            {
                if ((e.Mask & (1 << c)) == 0) continue;
                _inputs[(vertex, e.Register, c)] = new Leaf()
                {
                    Base = name,
                    Select = width == 1 ? "" : "xyzw"[c - e.FirstComponent].ToString(),
                    Type = ViewOf(e),
                    Bool = e.SystemValue == 9,
                };
            }
        }

        private string Interpolation(SignatureElement e)
        {
            if (_asm.Stage != "ps" || e.SystemValue == 1)
                return "";
            foreach (Declaration d in _asm.Declarations.Where(o => o.Op.StartsWith("dcl_input_ps")))
            {
                string[] words = d.Parts[0].Split(' ');
                Operand reg = ParseOperand(words[words.Length - 1]);
                if (reg.Type != "v" || reg.Register != e.Register) continue;
                int mask = reg.Swizzle.Length == 0 ? 0xF : reg.Swizzle.Aggregate(0, (m, c) => m | (1 << Operand.Component(c)));
                if ((mask & e.Mask) == 0) continue;
                List<string> modes = new List<string>();
                for (int w = 0; w < words.Length - 1; w++)
                {
                    switch (words[w])
                    {
                        case "constant": modes.Add("nointerpolation"); break;
                        case "noperspective": modes.Add("noperspective"); break;
                        case "centroid": modes.Add("centroid"); break;
                        case "sample": modes.Add("sample"); break;
                    }
                }
                return modes.Count == 0 ? "" : string.Join(" ", modes) + " ";
            }
            return "";
        }

        private string Signature(out string attributes, out string preamble)
        {
            foreach (SignatureElement e in _refl.Outputs.Where(o => o.Register >= 0 && o.SystemValue < 65 && o.ComponentType != 3))
                _storage["o" + e.Register] = V.UInt;
            List<string> parameters = new List<string>();
            List<string> attrs = new List<string>();
            StringBuilder pre = new StringBuilder();

            if (_asm.Stage == "gs")
            {
                //Inputs come as an array of vertices
                List<SignatureElement> vertexInputs = _refl.Inputs.Where(o => o.Register >= 0 && o.SystemValue != 7).ToList();
                Declaration primitive = _asm.Declared("dcl_inputprimitive").FirstOrDefault();
                string kind = primitive?.Parts.FirstOrDefault()?.Trim() ?? "triangle";
                int vertices;
                switch (kind)
                {
                    case "point": vertices = 1; break;
                    case "line": vertices = 2; break;
                    case "lineadj": vertices = 4; break;
                    case "triangleadj": kind = "triangleadj"; vertices = 6; break;
                    default: kind = "triangle"; vertices = 3; break;
                }
                pre.AppendLine("struct GSIn");
                pre.AppendLine("{");
                foreach (SignatureElement e in vertexInputs)
                {
                    string member = ElementName("v", e, vertexInputs);
                    pre.AppendLine("    " + ElementType(e) + " " + member + " : " + Semantic(e) + ";");
                    for (int v = 0; v < vertices; v++)
                        AddLeaves(v, e, "gsin[" + v + "]." + member);
                }
                pre.AppendLine("};");
                pre.AppendLine();
                parameters.Add(kind + " GSIn gsin[" + vertices + "]");

                //Outputs are written to a stream at each emit
                List<SignatureElement> outputs = _refl.Outputs.Where(o => o.Stream == 0).ToList();
                pre.AppendLine("struct GSOut");
                pre.AppendLine("{");
                foreach (SignatureElement e in outputs)
                {
                    string member = ElementName("o", e, outputs);
                    pre.AppendLine("    " + ElementType(e) + " " + member + " : " + Semantic(e) + ";");
                    _outputCopies.Add("gsout." + member + " = " + Convert("o" + e.Register + "." + Letters(e.Mask), Storage("o" + e.Register), ViewOf(e)) + ";");
                }
                pre.AppendLine("};");
                pre.AppendLine();
                string topology = _asm.Declared("dcl_outputtopology").FirstOrDefault()?.Parts.FirstOrDefault()?.Trim() ?? "trianglestrip";
                string stream = topology == "pointlist" ? "PointStream" : topology == "linestrip" ? "LineStream" : "TriangleStream";
                parameters.Add("inout " + stream + "<GSOut> stream");
                string maxout = _asm.Declared("dcl_maxout").FirstOrDefault()?.Parts.FirstOrDefault() ?? "1";
                attrs.Add("[maxvertexcount(" + maxout.Trim() + ")]");
                Declaration instances = _asm.Declared("dcl_gsinstances").FirstOrDefault();
                if (instances != null) attrs.Add("[instance(" + instances.Parts[0].Trim() + ")]");
            }
            else
            {
                foreach (SignatureElement e in _refl.Inputs.Where(o => o.Register >= 0))
                {
                    string name = ElementName("v", e, _refl.Inputs);
                    parameters.Add(Interpolation(e) + ElementType(e) + " " + name + " : " + Semantic(e));
                    AddLeaves(-1, e, name);
                }
            }

            //Inputs the signature doesn't hold (compute thread IDs, the primitive ID...)
            foreach (Declaration d in _asm.Declared("dcl_input").Concat(_asm.Declared("dcl_input_sgv")).Concat(_asm.Declared("dcl_input_siv")))
            {
                Operand input = ParseOperand(d.Parts[0]);
                if (!SpecialInputs.TryGetValue(input.Type, out var special) || _specials.ContainsKey(input.Type)) continue;
                _usedNames.Add(input.Type);
                parameters.Add(special.type + " " + input.Type + " : " + special.semantic);
                int width = special.type.EndsWith("3") ? 3 : 1;
                _specials[input.Type] = Enumerable.Range(0, width).Select(c => new Leaf() { Base = input.Type, Select = width == 1 ? "" : "xyzw"[c].ToString(), Type = V.UInt }).ToArray();
            }

            if (_asm.Stage != "gs")
            {
                foreach (SignatureElement e in _refl.Outputs)
                {
                    string name;
                    string source;
                    if (e.Register < 0 || e.SystemValue == 65 || e.SystemValue == 66 || e.SystemValue == 67 || e.SystemValue == 68)
                    {
                        //SV_Depth, SV_Coverage...: their own registers
                        string reg = e.SystemValue == 66 ? "oMask" : e.SystemValue == 67 ? "oDepthGE" : e.SystemValue == 68 ? "oDepthLE" : "oDepth";
                        name = Unique("out_" + reg);
                        source = Convert(reg, reg == "oMask" ? V.UInt : V.Float, ViewOf(e));
                    }
                    else
                    {
                        name = ElementName("out", e, _refl.Outputs);
                        source = Convert("o" + e.Register + "." + Letters(e.Mask), Storage("o" + e.Register), ViewOf(e));
                    }
                    parameters.Add("out " + ElementType(e) + " " + name + " : " + Semantic(e));
                    _outputCopies.Add(name + " = " + source + ";");
                }
            }

            if (_asm.Stage == "cs")
            {
                Declaration group = _asm.Declared("dcl_thread_group").FirstOrDefault();
                if (group != null) attrs.Add("[numthreads(" + string.Join(", ", group.Parts.Select(o => o.Trim())) + ")]");
            }
            foreach (Declaration flags in _asm.Declared("dcl_globalFlags"))
                if (flags.Parts.Any(o => o.Trim() == "forceEarlyDepthStencil"))
                    attrs.Add("[earlydepthstencil]");

            attributes = string.Join("\n", attrs);
            preamble = pre.ToString();
            return parameters.Count == 0 ? "" : "\n    " + string.Join(",\n    ", parameters);
        }

        private string Temps()
        {
            StringBuilder text = new StringBuilder();
            Declaration temps = _asm.Declared("dcl_temps").FirstOrDefault();
            int count = temps == null ? 0 : int.Parse(temps.Parts[0], CultureInfo.InvariantCulture);
            void Declare(string type, IEnumerable<string> names)
            {
                List<string> list = names.ToList();
                if (list.Count != 0) text.AppendLine("    " + type + " " + string.Join(", ", list.Select(o => o + " = 0")) + ";");
            }
            Declare("float4", Enumerable.Range(0, count).Select(i => "r" + i).Where(k => Storage(k) == V.Float));
            Declare("uint4", Enumerable.Range(0, count).Select(i => "r" + i).Where(k => Storage(k) == V.UInt));
            foreach (Declaration d in _asm.Declared("dcl_indexableTemp"))
            {
                Operand x = ParseOperand(d.Parts[0]);
                text.AppendLine("    " + (Storage("x" + x.Register) == V.UInt ? "uint4" : "float4") + " x" + x.Register + "[" + x.Offset + "] = { " + string.Join(", ", Enumerable.Repeat("0, 0, 0, 0", x.Offset)) + " };");
            }
            List<string> outputs = _refl.Outputs.Where(o => o.Register >= 0 && o.SystemValue < 65).Select(o => o.Register).Distinct().OrderBy(o => o).Select(o => "o" + o).ToList();
            Declare("float4", outputs.Where(k => Storage(k) == V.Float));
            Declare("uint4", outputs.Where(k => Storage(k) == V.UInt));
            foreach (SignatureElement e in _refl.Outputs.Where(o => o.Register < 0 || o.SystemValue >= 65))
                text.AppendLine("    " + (e.SystemValue == 66 ? "uint oMask" : e.SystemValue == 67 ? "float oDepthGE" : e.SystemValue == 68 ? "float oDepthLE" : "float oDepth") + " = 0;");
            if (_asm.Stage == "gs")
                text.AppendLine("    GSOut gsout = (GSOut)0;");
            if (text.Length != 0)
                text.AppendLine();
            return text.ToString();
        }
        #endregion

        #region VALUES
        private static string TypeName(V v, int width)
        {
            string s = v == V.Float ? "float" : v == V.Int ? "int" : "uint";
            return width == 1 ? s : s + width;
        }

        private static string Convert(string expression, V from, V to)
        {
            if (from == to) return expression;
            switch (to)
            {
                case V.Float: return "asfloat(" + expression + ")";
                case V.Int: return "asint(" + expression + ")";
                default: return "asuint(" + expression + ")";
            }
        }

        private static float Float(uint bits) => BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);

        /// <summary>A float literal that compiles to exactly these bits.</summary>
        private static string FloatText(uint bits)
        {
            if (bits == 0) return "0.0";
            uint exponent = (bits >> 23) & 0xFF;
            if (exponent == 0 || exponent == 0xFF || bits == 0x80000000)
                return "asfloat(0x" + bits.ToString("x8") + ")";
            float value = Float(bits);
            string text = null;
            for (int digits = 1; digits <= 9; digits++)
            {
                text = value.ToString("G" + digits, CultureInfo.InvariantCulture);
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float back) && BitConverter.ToUInt32(BitConverter.GetBytes(back), 0) == bits)
                    break;
            }
            if (text.Contains("E"))
            {
                int e = text.IndexOf('E');
                if (!text.Substring(0, e).Contains(".")) text = text.Substring(0, e) + ".0" + text.Substring(e);
                text = text.Replace("E+", "e").Replace("E", "e");
            }
            else if (!text.Contains("."))
                text += ".0";
            return text;
        }

        private static string Literal(uint bits, V v)
        {
            switch (v)
            {
                case V.Float: return FloatText(bits);
                case V.Int:
                    {
                        int i = unchecked((int)bits);
                        return i >= -65536 && i <= 65536 ? i.ToString(CultureInfo.InvariantCulture) : "asint(0x" + bits.ToString("x8") + ")";
                    }
                default:
                    return bits <= 65536 ? bits.ToString(CultureInfo.InvariantCulture) + "u" : "0x" + bits.ToString("x8");
            }
        }

        /* Leaves (each one component) as one expression of the wanted type */
        private static string Combine(List<Leaf> leaves, V want)
        {
            int width = leaves.Count;
            Leaf first = leaves[0];
            bool oneBase = leaves.All(o => o.Base == first.Base && o.Type == first.Type && o.Bool == first.Bool);
            if (oneBase)
            {
                string expression;
                if (first.Select.Length == 0)
                    expression = width == 1 ? first.Base : first.Base + "." + new string('x', width);
                else if (first.Select.StartsWith("_m"))
                    expression = first.Base + "." + string.Concat(leaves.Select(o => o.Select));
                else
                    expression = first.Base + "." + string.Concat(leaves.Select(o => o.Select));
                return LeafValue(expression, first, want);
            }
            return TypeName(want, width) + "(" + string.Join(", ", leaves.Select(o => Combine(new List<Leaf>() { o }, want))) + ")";
        }

        private static string LeafValue(string expression, Leaf leaf, V want)
        {
            if (leaf.Bool)
            {
                string mask = "(" + expression + " ? 0xffffffff : 0u)";
                return want == V.UInt ? mask : Convert(mask, V.UInt, want);
            }
            return Convert(expression, leaf.Type, want);
        }

        private string Index(Operand operand)
        {
            if (operand.Relative == null)
                return operand.Offset.ToString(CultureInfo.InvariantCulture);
            string relative = Read(operand.Relative, operand.Relative.First(1), V.Int);
            return operand.Offset == 0 ? relative : relative + " + " + operand.Offset;
        }

        private string ResourceName(string space, int register)
        {
            foreach (Binding binding in _refl.Bindings)
            {
                if (Space(binding.Type) != space || register < binding.BindPoint || register >= binding.BindPoint + Math.Max(1, binding.BindCount)) continue;
                string name = binding.Type == InputType.Structured || binding.Type == InputType.UavRWStructured || binding.Type == InputType.UavRWStructuredWithCounter || binding.Type == InputType.UavAppendStructured || binding.Type == InputType.UavConsumeStructured
                    ? _buffers[space + binding.BindPoint].Name
                    : null;
                if (name == null)
                    name = DeclaredName(binding);
                return binding.BindCount > 1 ? name + "[" + (register - binding.BindPoint) + "]" : name;
            }
            throw new NotSupportedException(space + register + " isn't bound in the shader's reflection data.");
        }

        private readonly Dictionary<Binding, string> _bindingNames = new Dictionary<Binding, string>();
        private string DeclaredName(Binding binding)
        {
            if (_bindingNames.TryGetValue(binding, out string name)) return name;
            throw new NotSupportedException(binding.Name + " wasn't declared.");
        }

        /// <summary>An operand's components (0-3, one for each wanted) as an expression of type <paramref name="want"/>, its modifiers applied.</summary>
        private string Read(Operand op, int[] components, V want)
        {
            string value = ReadPlain(op, components, want);
            if (op.Absolute)
                value = "abs(" + value + ")";
            if (op.Negate)
                value = value.StartsWith("-") || value.Contains(" ") ? "-(" + value + ")" : "-" + value;
            return value;
        }

        private string ReadPlain(Operand op, int[] c, V want)
        {
            int width = c.Length;
            string letters = string.Concat(c.Select(o => "xyzw"[o]));
            switch (op.Type)
            {
                case "l":
                    {
                        if (width == 1) return Literal(op.Values[c[0]], want);
                        return TypeName(want, width) + "(" + string.Join(", ", c.Select(o => Literal(op.Values[o], want))) + ")";
                    }
                case "r":
                    return Convert("r" + op.Register + "." + letters, Storage(Key(op)), want);
                case "o":
                    return Convert("o" + op.Register + "." + letters, Storage(Key(op)), want);
                case "x":
                    return Convert("x" + op.Register + "[" + Index(op) + "]." + letters, Storage(Key(op)), want);
                case "icb":
                    return Convert("icb[" + Index(op) + "]." + letters, IcbIsFloat ? V.Float : V.UInt, want);
                case "v":
                    {
                        if (op.Relative != null || op.VertexRelative != null)
                            throw new NotSupportedException("Indexing into the shader's inputs by a register (" + op.Text + ") isn't supported.");
                        List<Leaf> leaves = new List<Leaf>();
                        foreach (int component in c)
                        {
                            if (!_inputs.TryGetValue((op.Vertex, op.Register, component), out Leaf leaf))
                                throw new NotSupportedException("Input " + op.Text + " isn't in the shader's input signature.");
                            leaves.Add(leaf);
                        }
                        return Combine(leaves, want);
                    }
                case "cb":
                    return ReadConstant(op, c, want);
                default:
                    {
                        if (_specials.TryGetValue(op.Type, out Leaf[] special))
                            return Combine(c.Select(o => special[Math.Min(o, special.Length - 1)]).ToList(), want);
                        if (op.Type == "oDepth" || op.Type == "oMask" || op.Type == "oDepthGE" || op.Type == "oDepthLE")
                            return Convert(width == 1 ? op.Type : op.Type + "." + new string('x', width), V.Float, want);
                        throw new NotSupportedException("Operands like " + op.Text + " aren't supported.");
                    }
            }
        }

        private string ReadConstant(Operand op, int[] c, V want)
        {
            if (!_constants.TryGetValue(op.Register, out ConstantView view))
                throw new NotSupportedException("cb" + op.Register + " isn't in the shader's reflection data.");
            string letters = string.Concat(c.Select(o => "xyzw"[o]));
            if (view.Raw)
                return Convert(view.Name + "[" + Index(op) + "]." + letters, V.Float, want);
            if (op.Relative != null)
            {
                //An array of single-register elements, indexed
                foreach (var array in view.Arrays)
                {
                    if (op.Offset < array.first || op.Offset >= array.first + array.count) continue;
                    int width = array.element.Class == VariableClass.Vector ? array.element.Columns : 1;
                    if (c.Any(o => o >= width)) break;
                    string relative = Read(op.Relative, op.Relative.First(1), V.Int);
                    int shift = op.Offset - array.first;
                    string element = array.name + "[" + relative + (shift != 0 ? " + " + shift : "") + "]";
                    Leaf leaf = new Leaf() { Base = element, Type = ViewOf(array.element.Base) };
                    string expression = array.element.Class == VariableClass.Scalar
                        ? (c.Length == 1 ? element : element + "." + new string('x', c.Length))
                        : element + "." + letters;
                    return LeafValue(expression, leaf, want);
                }
                throw new RawFallback("cb" + op.Register);
            }
            List<Leaf> leaves = new List<Leaf>();
            foreach (int component in c)
            {
                if (!view.Leaves.TryGetValue((op.Offset, component), out Leaf leaf))
                    throw new RawFallback("cb" + op.Register);
                leaves.Add(leaf);
            }
            return Combine(leaves, want);
        }
        #endregion

        #region INSTRUCTIONS
        private string Destination(Operand d, out int[] components)
        {
            components = d.MaskComponents();
            switch (d.Type)
            {
                case "r": return "r" + d.Register + "." + d.Swizzle;
                case "o": return "o" + d.Register + "." + d.Swizzle;
                case "x": return "x" + d.Register + "[" + Index(d) + "]." + d.Swizzle;
                case "oDepth": case "oDepthGE": case "oDepthLE": case "oMask":
                    components = new[] { 0 };
                    return d.Type;
            }
            throw new NotSupportedException("Writing to " + d.Text + " isn't supported.");
        }

        private void Assign(Instruction ins, Operand d, string expression, V type)
        {
            if (d.IsNull) return;
            string target = Destination(d, out _);
            if (_dryRun && IsRegister(d) && type != V.Float)
                _bitsWrites.Add(Key(d));
            if (ins.Saturate)
            {
                expression = "saturate(" + Convert(expression, type, V.Float) + ")";
                type = V.Float;
            }
            V storage = IsRegister(d) ? Storage(Key(d)) : d.Type == "oMask" ? V.UInt : V.Float;
            Line(target + " = " + Convert(expression, type, storage) + ";");
        }

        /* mov, movc and swapc move bits without a type: read in the destination's own storage type */
        private V Untyped(Operand d, params Operand[] sources)
        {
            if (_dryRun && IsRegister(d))
            {
                foreach (Operand s in sources)
                {
                    if (IsRegister(s)) _links.Add((Key(s), Key(d)));
                    else if (s.IsLiteral && s.Values.Any(OddBits)) _bitsWrites.Add(Key(d));
                    else if (s.Type == "icb" && _asm.ImmediateConstants.Any(row => row.Any(OddBits))) _bitsWrites.Add(Key(d));
                }
            }
            //abs() and negation are float operations: a source with them is read as a float
            if (sources.Any(s => s.Absolute || s.Negate)) return V.Float;
            if (IsRegister(d)) return Storage(Key(d));
            return d.Type == "oMask" ? V.UInt : V.Float;
        }

        /* An instruction that works component by component: each source read for the destination's components */
        private void Componentwise(Instruction ins, V source, V result, Func<string[], string> make)
        {
            Operand d = ins.Operands[0];
            if (d.IsNull) return;
            Destination(d, out int[] components);
            string[] sources = ins.Operands.Skip(1).Select(o => Read(o, o.Pick(components), source)).ToArray();
            _width = components.Length;
            Assign(ins, d, make(sources), result);
        }

        private void Compare(Instruction ins, V source, string op)
        {
            Componentwise(ins, source, V.UInt, s => "(" + s[0] + " " + op + " " + s[1] + ") ? 0xffffffff : 0u");
        }

        private string Condition(Operand op, bool nonZero)
        {
            return Read(op, op.First(1), V.UInt) + (nonZero ? " != 0" : " == 0");
        }

        private void Return()
        {
            if (_asm.Stage != "gs")
                foreach (string copy in _outputCopies)
                    Line(copy);
            Line("return;");
        }

        /* Several destinations that read the same registers they write: the sources go to temporaries first */
        private bool Overlaps(Instruction ins, int destinations)
        {
            HashSet<string> written = new HashSet<string>(ins.Operands.Take(destinations).Where(o => !o.IsNull).Select(o => o.Type + o.Register));
            return ins.Operands.Skip(destinations).Any(o => written.Contains(o.Type + o.Register));
        }

        private string Temp(string type, string value)
        {
            string name = "t" + (_blockTemps++);
            Line(type + " " + name + " = " + value + ";");
            return name;
        }

        private void Translate(Instruction ins)
        {
            List<Operand> o = ins.Operands;
            switch (ins.Op)
            {
                //Float maths
                case "add": Componentwise(ins, V.Float, V.Float, s => s[0] + " + " + s[1]); return;
                case "mul": Componentwise(ins, V.Float, V.Float, s => s[0] + " * " + s[1]); return;
                case "div": Componentwise(ins, V.Float, V.Float, s => s[0] + " / " + s[1]); return;
                case "mad": Componentwise(ins, V.Float, V.Float, s => s[0] + " * " + s[1] + " + " + s[2]); return;
                case "min": Componentwise(ins, V.Float, V.Float, s => "min(" + s[0] + ", " + s[1] + ")"); return;
                case "max": Componentwise(ins, V.Float, V.Float, s => "max(" + s[0] + ", " + s[1] + ")"); return;
                case "mov": { V view = Untyped(o[0], o[1]); Componentwise(ins, view, view, s => s[0]); return; }
                case "frc": Componentwise(ins, V.Float, V.Float, s => "frac(" + s[0] + ")"); return;
                case "exp": Componentwise(ins, V.Float, V.Float, s => "exp2(" + s[0] + ")"); return;
                case "log": Componentwise(ins, V.Float, V.Float, s => "log2(" + s[0] + ")"); return;
                case "rsq": Componentwise(ins, V.Float, V.Float, s => "rsqrt(" + s[0] + ")"); return;
                case "sqrt": Componentwise(ins, V.Float, V.Float, s => "sqrt(" + s[0] + ")"); return;
                case "rcp": Componentwise(ins, V.Float, V.Float, s => "rcp(" + s[0] + ")"); return;
                case "round_ni": Componentwise(ins, V.Float, V.Float, s => "floor(" + s[0] + ")"); return;
                case "round_pi": Componentwise(ins, V.Float, V.Float, s => "ceil(" + s[0] + ")"); return;
                case "round_ne": Componentwise(ins, V.Float, V.Float, s => "round(" + s[0] + ")"); return;
                case "round_z": Componentwise(ins, V.Float, V.Float, s => "trunc(" + s[0] + ")"); return;
                case "deriv_rtx": Componentwise(ins, V.Float, V.Float, s => "ddx(" + s[0] + ")"); return;
                case "deriv_rty": Componentwise(ins, V.Float, V.Float, s => "ddy(" + s[0] + ")"); return;
                case "deriv_rtx_coarse": Componentwise(ins, V.Float, V.Float, s => "ddx_coarse(" + s[0] + ")"); return;
                case "deriv_rty_coarse": Componentwise(ins, V.Float, V.Float, s => "ddy_coarse(" + s[0] + ")"); return;
                case "deriv_rtx_fine": Componentwise(ins, V.Float, V.Float, s => "ddx_fine(" + s[0] + ")"); return;
                case "deriv_rty_fine": Componentwise(ins, V.Float, V.Float, s => "ddy_fine(" + s[0] + ")"); return;
                case "dp2": Dot(ins, 2); return;
                case "dp3": Dot(ins, 3); return;
                case "dp4": Dot(ins, 4); return;
                case "lt": Compare(ins, V.Float, "<"); return;
                case "ge": Compare(ins, V.Float, ">="); return;
                case "eq": Compare(ins, V.Float, "=="); return;
                case "ne": Compare(ins, V.Float, "!="); return;
                case "movc":
                    {
                        Operand d = o[0];
                        Destination(d, out int[] components);
                        V view = Untyped(d, o[2], o[3]);
                        string condition = Read(o[1], o[1].Pick(components), V.UInt);
                        string a = Read(o[2], o[2].Pick(components), view), b = Read(o[3], o[3].Pick(components), view);
                        Assign(ins, d, "(" + condition + " != 0) ? " + a + " : " + b, view);
                        return;
                    }
                case "swapc":
                    {
                        //swapc d0, d1, cond, a, b: d0 = cond ? b : a, d1 = cond ? a : b
                        Destination(o[0], out int[] c0);
                        Destination(o[1], out int[] c1);
                        string cond0 = Temp(TypeName(V.UInt, c0.Length), Read(o[2], o[2].Pick(c0), V.UInt));
                        string a0 = Temp(TypeName(V.Float, c0.Length), Read(o[3], o[3].Pick(c0), V.Float));
                        string b0 = Temp(TypeName(V.Float, c0.Length), Read(o[4], o[4].Pick(c0), V.Float));
                        string cond1 = Temp(TypeName(V.UInt, c1.Length), Read(o[2], o[2].Pick(c1), V.UInt));
                        string a1 = Temp(TypeName(V.Float, c1.Length), Read(o[3], o[3].Pick(c1), V.Float));
                        string b1 = Temp(TypeName(V.Float, c1.Length), Read(o[4], o[4].Pick(c1), V.Float));
                        Assign(ins, o[0], "(" + cond0 + " != 0) ? " + b0 + " : " + a0, V.Float);
                        Assign(ins, o[1], "(" + cond1 + " != 0) ? " + a1 + " : " + b1, V.Float);
                        return;
                    }
                case "sincos":
                    {
                        Operand sinD = o[0], cosD = o[1];
                        int[] sc = sinD.IsNull ? null : sinD.MaskComponents(), cc = cosD.IsNull ? null : cosD.MaskComponents();
                        bool overlap = !sinD.IsNull && !cosD.IsNull && Overlaps(ins, 2);
                        if (overlap) Line("{");
                        if (overlap) _indent++;
                        string sinSource = sc == null ? null : Read(o[2], o[2].Pick(sc), V.Float);
                        string cosSource = cc == null ? null : Read(o[2], o[2].Pick(cc), V.Float);
                        if (overlap)
                        {
                            if (sinSource != null) sinSource = Temp(TypeName(V.Float, sc.Length), sinSource);
                            if (cosSource != null) cosSource = Temp(TypeName(V.Float, cc.Length), cosSource);
                        }
                        if (sc != null) Assign(ins, sinD, "sin(" + sinSource + ")", V.Float);
                        if (cc != null) Assign(ins, cosD, "cos(" + cosSource + ")", V.Float);
                        if (overlap) { _indent--; Line("}"); }
                        return;
                    }

                //Integer maths
                case "iadd": Componentwise(ins, V.Int, V.Int, s => s[0] + " + " + s[1]); return;
                case "imad": Componentwise(ins, V.Int, V.Int, s => s[0] + " * " + s[1] + " + " + s[2]); return;
                case "umad": Componentwise(ins, V.UInt, V.UInt, s => s[0] + " * " + s[1] + " + " + s[2]); return;
                case "ineg": Componentwise(ins, V.Int, V.Int, s => "-" + s[0]); return;
                case "imin": Componentwise(ins, V.Int, V.Int, s => "min(" + s[0] + ", " + s[1] + ")"); return;
                case "imax": Componentwise(ins, V.Int, V.Int, s => "max(" + s[0] + ", " + s[1] + ")"); return;
                case "umin": Componentwise(ins, V.UInt, V.UInt, s => "min(" + s[0] + ", " + s[1] + ")"); return;
                case "umax": Componentwise(ins, V.UInt, V.UInt, s => "max(" + s[0] + ", " + s[1] + ")"); return;
                case "ishl": Componentwise(ins, V.Int, V.Int, s => s[0] + " << " + s[1]); return;
                case "ishr": Componentwise(ins, V.Int, V.Int, s => s[0] + " >> " + s[1]); return;
                case "ushr": Componentwise(ins, V.UInt, V.UInt, s => s[0] + " >> " + s[1]); return;
                case "and": Componentwise(ins, V.UInt, V.UInt, s => s[0] + " & " + s[1]); return;
                case "or": Componentwise(ins, V.UInt, V.UInt, s => s[0] + " | " + s[1]); return;
                case "xor": Componentwise(ins, V.UInt, V.UInt, s => s[0] + " ^ " + s[1]); return;
                case "not": Componentwise(ins, V.UInt, V.UInt, s => "~" + s[0]); return;
                case "ige": Compare(ins, V.Int, ">="); return;
                case "ilt": Compare(ins, V.Int, "<"); return;
                case "ieq": Compare(ins, V.Int, "=="); return;
                case "ine": Compare(ins, V.Int, "!="); return;
                case "uge": Compare(ins, V.UInt, ">="); return;
                case "ult": Compare(ins, V.UInt, "<"); return;
                case "ftoi": Componentwise(ins, V.Float, V.Int, s => "(int" + Suffix(s) + ")" + Paren(s[0])); return;
                case "ftou": Componentwise(ins, V.Float, V.UInt, s => "(uint" + Suffix(s) + ")" + Paren(s[0])); return;
                case "itof": Componentwise(ins, V.Int, V.Float, s => "(float" + Suffix(s) + ")" + Paren(s[0])); return;
                case "utof": Componentwise(ins, V.UInt, V.Float, s => "(float" + Suffix(s) + ")" + Paren(s[0])); return;
                case "f16tof32": Componentwise(ins, V.UInt, V.Float, s => "f16tof32(" + s[0] + ")"); return;
                case "f32tof16": Componentwise(ins, V.Float, V.UInt, s => "f32tof16(" + s[0] + ")"); return;
                case "countbits": Componentwise(ins, V.UInt, V.UInt, s => "countbits(" + s[0] + ")"); return;
                case "firstbit_hi": Componentwise(ins, V.UInt, V.UInt, s => "firstbithigh(" + s[0] + ")"); return;
                case "firstbit_shi": Componentwise(ins, V.Int, V.UInt, s => "firstbithigh(" + s[0] + ")"); return;
                case "firstbit_lo": Componentwise(ins, V.UInt, V.UInt, s => "firstbitlow(" + s[0] + ")"); return;
                case "bfrev": Componentwise(ins, V.UInt, V.UInt, s => "reversebits(" + s[0] + ")"); return;
                case "ubfe":
                    //ubfe d, width, offset, value: width and offset use their low five bits
                    Componentwise(ins, V.UInt, V.UInt, s => BitfieldExtract(s[0], s[1], s[2], false));
                    return;
                case "ibfe":
                    {
                        Operand d = o[0];
                        Destination(d, out int[] components);
                        string w = Read(o[1], o[1].Pick(components), V.UInt), off = Read(o[2], o[2].Pick(components), V.UInt), value = Read(o[3], o[3].Pick(components), V.Int);
                        Assign(ins, d, BitfieldExtract(w, off, value, true), V.Int);
                        return;
                    }
                case "bfi":
                    {
                        Operand d = o[0];
                        Destination(d, out int[] components);
                        string w = Read(o[1], o[1].Pick(components), V.UInt), off = Read(o[2], o[2].Pick(components), V.UInt);
                        string insert = Read(o[3], o[3].Pick(components), V.UInt), into = Read(o[4], o[4].Pick(components), V.UInt);
                        string width = Paren(w) + " & 31u", offset = Paren(off) + " & 31u";
                        string mask = "((((1u << (" + width + ")) - 1u) << (" + offset + ")))";
                        Assign(ins, d, "((" + insert + " << (" + offset + ")) & " + mask + ") | (" + into + " & ~" + mask + ")", V.UInt);
                        return;
                    }
                case "imul":
                case "umul":
                    {
                        //imul hi, lo, a, b: only the low 32 bits are written in practice
                        V v = ins.Op == "imul" ? V.Int : V.UInt;
                        if (!o[0].IsNull)
                            throw new NotSupportedException("The high half of a 32x32 multiply (" + ins.Text + ") isn't supported.");
                        Destination(o[1], out int[] components);
                        Assign(ins, o[1], Read(o[2], o[2].Pick(components), v) + " * " + Read(o[3], o[3].Pick(components), v), v);
                        return;
                    }
                case "udiv":
                    {
                        Operand q = o[0], r = o[1];
                        int[] qc = q.IsNull ? null : q.MaskComponents(), rc = r.IsNull ? null : r.MaskComponents();
                        bool overlap = !q.IsNull && !r.IsNull && Overlaps(ins, 2);
                        if (overlap) { Line("{"); _indent++; }
                        string qa = qc == null ? null : Read(o[2], o[2].Pick(qc), V.UInt), qb = qc == null ? null : Read(o[3], o[3].Pick(qc), V.UInt);
                        string ra = rc == null ? null : Read(o[2], o[2].Pick(rc), V.UInt), rb = rc == null ? null : Read(o[3], o[3].Pick(rc), V.UInt);
                        if (overlap)
                        {
                            if (qa != null) { qa = Temp(TypeName(V.UInt, qc.Length), qa); qb = Temp(TypeName(V.UInt, qc.Length), qb); }
                            if (ra != null) { ra = Temp(TypeName(V.UInt, rc.Length), ra); rb = Temp(TypeName(V.UInt, rc.Length), rb); }
                        }
                        if (qc != null) Assign(ins, q, qa + " / " + qb, V.UInt);
                        if (rc != null) Assign(ins, r, ra + " % " + rb, V.UInt);
                        if (overlap) { _indent--; Line("}"); }
                        return;
                    }

                //Flow control
                case "if_nz": Line("[branch]"); Line("if (" + Condition(o[0], true) + ")"); Line("{"); _indent++; return;
                case "if_z": Line("[branch]"); Line("if (" + Condition(o[0], false) + ")"); Line("{"); _indent++; return;
                case "else": _indent--; Line("}"); Line("else"); Line("{"); _indent++; return;
                case "endif": _indent--; Line("}"); return;
                case "loop": Line("[loop]"); Line("while (true)"); Line("{"); _indent++; return;
                case "endloop": _indent--; Line("}"); return;
                case "break": Line("break;"); return;
                case "breakc_nz": Line("if (" + Condition(o[0], true) + ") break;"); return;
                case "breakc_z": Line("if (" + Condition(o[0], false) + ") break;"); return;
                case "continue": Line("continue;"); return;
                case "continuec_nz": Line("if (" + Condition(o[0], true) + ") continue;"); return;
                case "continuec_z": Line("if (" + Condition(o[0], false) + ") continue;"); return;
                case "switch": Line("switch (" + Read(o[0], o[0].First(1), V.Int) + ")"); Line("{"); _indent++; return;
                case "case": _indent--; Line("case " + Literal(o[0].Values[0], V.Int) + ":"); _indent++; return;
                case "default": _indent--; Line("default:"); _indent++; return;
                case "endswitch": _indent--; Line("}"); return;
                case "ret": Return(); return;
                case "retc_nz": Line("if (" + Condition(o[0], true) + ")"); Line("{"); _indent++; Return(); _indent--; Line("}"); return;
                case "retc_z": Line("if (" + Condition(o[0], false) + ")"); Line("{"); _indent++; Return(); _indent--; Line("}"); return;
                case "discard_nz": Line("if (" + Condition(o[0], true) + ") discard;"); return;
                case "discard_z": Line("if (" + Condition(o[0], false) + ") discard;"); return;
                case "nop": return;

                //Geometry shader output
                case "emit_stream":
                case "emit":
                    foreach (string copy in _outputCopies) Line(copy);
                    Line("stream.Append(gsout);");
                    return;
                case "cut_stream":
                case "cut":
                    Line("stream.RestartStrip();");
                    return;
                case "emitThenCut_stream":
                case "emitThenCut":
                    foreach (string copy in _outputCopies) Line(copy);
                    Line("stream.Append(gsout);");
                    Line("stream.RestartStrip();");
                    return;

                //Compute
                case "sync_g_t": Line("GroupMemoryBarrierWithGroupSync();"); return;
                case "sync_g": Line("GroupMemoryBarrier();"); return;
                case "sync_uglobal_g_t": Line("AllMemoryBarrierWithGroupSync();"); return;
                case "sync_uglobal_g": Line("AllMemoryBarrier();"); return;
                case "sync_uglobal_t": Line("DeviceMemoryBarrierWithGroupSync();"); return;
                case "sync_uglobal": Line("DeviceMemoryBarrier();"); return;

                //Resources
                case "sample": Sample(ins, "Sample", 0); return;
                case "sample_l": Sample(ins, "SampleLevel", 1); return;
                case "sample_b": Sample(ins, "SampleBias", 1); return;
                case "sample_d": Sample(ins, "SampleGrad", 2); return;
                case "sample_c": Sample(ins, "SampleCmp", 1); return;
                case "sample_c_lz": Sample(ins, "SampleCmpLevelZero", 1); return;
                case "gather4": Gather(ins, false, false); return;
                case "gather4_c": Gather(ins, true, false); return;
                case "gather4_po": Gather(ins, false, true); return;
                case "gather4_po_c": Gather(ins, true, true); return;
                case "ld": Load(ins, false); return;
                case "ld_ms": Load(ins, true); return;
                case "resinfo": ResInfo(ins); return;
                case "ld_raw": LoadRaw(ins); return;
                case "store_raw": StoreRaw(ins); return;
                case "ld_structured": LoadStructured(ins); return;
                case "store_structured": StoreStructured(ins); return;
                case "ld_uav_typed": LoadTyped(ins); return;
                case "store_uav_typed": StoreTyped(ins); return;
                case "imm_atomic_alloc":
                case "imm_atomic_consume":
                    Allocate(ins);
                    return;
            }
            if (ins.Op.StartsWith("atomic_") || ins.Op.StartsWith("imm_atomic_"))
            {
                Atomic(ins);
                return;
            }
            throw new NotSupportedException("The instruction '" + ins.Text + "' isn't supported.");
        }

        private int _width = 1;
        /* The width of the destination being written, for a conversion: (float2)v0.xy, not (float)v0.xy */
        private string Suffix(string[] sources) => _width > 1 ? _width.ToString() : "";

        private static string Paren(string expression)
        {
            return expression.Contains(" ") ? "(" + expression + ")" : expression;
        }

        private static string BitfieldExtract(string width, string offset, string value, bool signed)
        {
            string w = "(" + Paren(width) + " & 31u)", o = "(" + Paren(offset) + " & 31u)";
            if (signed)
                return "(" + w + " == 0) ? 0 : ((" + w + " + " + o + " < 32u) ? ((" + Paren(value) + " << (32u - " + w + " - " + o + ")) >> (32u - " + w + ")) : (" + Paren(value) + " >> " + o + "))";
            return "(" + w + " == 0) ? 0u : ((" + w + " + " + o + " < 32u) ? ((" + Paren(value) + " << (32u - " + w + " - " + o + ")) >> (32u - " + w + ")) : (" + Paren(value) + " >> " + o + "))";
        }

        private void Dot(Instruction ins, int n)
        {
            Operand d = ins.Operands[0];
            string a = Read(ins.Operands[1], ins.Operands[1].First(n), V.Float);
            string b = Read(ins.Operands[2], ins.Operands[2].First(n), V.Float);
            Assign(ins, d, "dot(" + a + ", " + b + ")", V.Float);
        }

        /* Coordinates a texture of this dimension takes */
        private static int Coordinates(string dimension)
        {
            switch (dimension)
            {
                case "texture1d": case "buffer": return 1;
                case "texture1darray": case "texture2d": case "texture2dms": return 2;
                case "texture2darray": case "texture2dmsarray": case "texture3d": case "texturecube": return 3;
                case "texturecubearray": return 4;
            }
            throw new NotSupportedException("Textures of dimension " + dimension + " aren't supported.");
        }

        private static int OffsetCount(string dimension)
        {
            switch (dimension)
            {
                case "texture1d": case "texture1darray": return 1;
                case "texture3d": return 3;
                case "texturecube": case "texturecubearray": return 0;
                default: return 2;
            }
        }

        private string OffsetArgument(Instruction ins)
        {
            if (ins.Offsets == null) return "";
            int count = OffsetCount(ins.Dimension);
            if (count == 0) return "";
            return ", " + TypeName(V.Int, count) + "(" + string.Join(", ", ins.Offsets.Take(count)) + ")";
        }

        private Binding TextureBinding(Operand resource)
        {
            return _refl.Bindings.FirstOrDefault(b => Space(b.Type) == resource.Type.Substring(0, 1) && resource.Register >= b.BindPoint && resource.Register < b.BindPoint + Math.Max(1, b.BindCount));
        }

        private static V ReturnView(Binding binding)
        {
            if (binding == null) return V.Float;
            return binding.Return == ReturnType.UInt ? V.UInt : binding.Return == ReturnType.SInt ? V.Int : V.Float;
        }

        /* The fetched value's components as the destination wants them, through the resource's swizzle */
        private string Fetched(string call, Operand resource, int[] components, int available)
        {
            int[] picked = resource.Pick(components);
            if (available == 1 && picked.All(c => c == 0))
                return components.Length == 1 ? call : call + "." + new string('x', components.Length);
            return call + "." + string.Concat(picked.Select(c => "xyzw"[c]));
        }

        private void Sample(Instruction ins, string method, int extras)
        {
            List<Operand> o = ins.Operands;
            Operand d = o[0], coordinates = o[1], texture = o[2], sampler = o[3];
            Destination(d, out int[] components);
            int count = Coordinates(ins.Dimension);
            string uv = Read(coordinates, coordinates.First(count), V.Float);
            List<string> arguments = new List<string>() { ResourceName("s", sampler.Register), uv };
            if (method == "SampleGrad")
            {
                int gradient = ins.Dimension == "texture3d" || ins.Dimension.StartsWith("texturecube") ? 3 : ins.Dimension.StartsWith("texture1d") ? 1 : 2;
                arguments.Add(Read(o[4], o[4].First(gradient), V.Float));
                arguments.Add(Read(o[5], o[5].First(gradient), V.Float));
            }
            else if (extras == 1)
                arguments.Add(Read(o[4], o[4].First(1), V.Float));
            Binding binding = TextureBinding(texture);
            bool compare = method.StartsWith("SampleCmp");
            string call = ResourceName("t", texture.Register) + "." + method + "(" + string.Join(", ", arguments) + OffsetArgument(ins) + ")";
            Assign(ins, d, Fetched(call, texture, components, compare ? 1 : binding?.Components ?? 4), compare ? V.Float : ReturnView(binding));
        }

        private void Gather(Instruction ins, bool compare, bool programmableOffset)
        {
            List<Operand> o = ins.Operands;
            int at = 0;
            Operand d = o[at++], coordinates = o[at++];
            Operand offset = programmableOffset ? o[at++] : null;
            Operand texture = o[at++], sampler = o[at++];
            Operand reference = compare ? o[at++] : null;
            Destination(d, out int[] components);
            int count = Coordinates(ins.Dimension);
            string channel = sampler.Swizzle.Length == 0 ? "Red" : new[] { "Red", "Green", "Blue", "Alpha" }[Operand.Component(sampler.Swizzle[0])];
            List<string> arguments = new List<string>() { ResourceName("s", sampler.Register), Read(coordinates, coordinates.First(count), V.Float) };
            if (reference != null) arguments.Add(Read(reference, reference.First(1), V.Float));
            string offsets = programmableOffset ? ", " + Read(offset, offset.First(OffsetCount(ins.Dimension)), V.Int) : OffsetArgument(ins);
            string call = ResourceName("t", texture.Register) + "." + (compare ? "GatherCmp" : "Gather") + channel + "(" + string.Join(", ", arguments) + offsets + ")";
            Binding binding = TextureBinding(texture);
            Assign(ins, d, Fetched(call, texture, components, 4), compare ? V.Float : ReturnView(binding));
        }

        private void Load(Instruction ins, bool multisampled)
        {
            List<Operand> o = ins.Operands;
            Operand d = o[0], address = o[1], texture = o[2];
            Destination(d, out int[] components);
            string dimension = ins.Dimension ?? "texture2d";
            int count = Coordinates(dimension);
            int[] order = address.First(4);
            string location;
            if (dimension == "buffer")
                location = Read(address, new[] { order[0] }, V.Int);
            else if (multisampled)
                location = Read(address, order.Take(count).ToArray(), V.Int);
            else
            {
                //The mip level is the w component
                int[] picked = order.Take(count).Concat(new[] { order[3] }).ToArray();
                location = Read(address, picked, V.Int);
            }
            string arguments = location + (multisampled ? ", " + Read(o[3], o[3].First(1), V.Int) : "") + OffsetArgument(ins);
            Binding binding = TextureBinding(texture);
            string call = ResourceName(texture.Type.Substring(0, 1), texture.Register) + ".Load(" + arguments + ")";
            Assign(ins, d, Fetched(call, texture, components, binding?.Components ?? 4), ReturnView(binding));
        }

        private void ResInfo(Instruction ins)
        {
            List<Operand> o = ins.Operands;
            Operand d = o[0], mip = o[1], texture = o[2];
            Destination(d, out int[] components);
            string dimension = ins.Dimension ?? "texture2d";
            string name = ResourceName("t", texture.Register);
            Line("{");
            _indent++;
            Line("uint w, h, e, levels;");
            string mipLevel = Read(mip, mip.First(1), V.UInt);
            switch (dimension)
            {
                case "texture1d": Line(name + ".GetDimensions(" + mipLevel + ", w, levels); h = 0; e = 0;"); break;
                case "texture1darray": Line(name + ".GetDimensions(" + mipLevel + ", w, h, levels); e = 0;"); break;
                case "texture2d": case "texturecube": Line(name + ".GetDimensions(" + mipLevel + ", w, h, levels); e = 0;"); break;
                case "texture2darray": case "texture3d": case "texturecubearray": Line(name + ".GetDimensions(" + mipLevel + ", w, h, e, levels);"); break;
                default: throw new NotSupportedException("resinfo on " + dimension + " isn't supported.");
            }
            string value;
            if (ins.Suffix.Contains("_uint"))
                value = "uint4(w, h, e, levels)";
            else if (ins.Suffix.Contains("_rcpFloat"))
                value = "float4(rcp((float)w), rcp((float)h), rcp((float)e), (float)levels)";
            else
                value = "float4((float)w, (float)h, (float)e, (float)levels)";
            Assign(ins, d, value + "." + string.Concat(texture.Pick(components).Select(c => "xyzw"[c])), ins.Suffix.Contains("_uint") ? V.UInt : V.Float);
            _indent--;
            Line("}");
        }

        private string GroupSharedIndex(Operand g, string byteAddress, int dword)
        {
            return "g" + g.Register + "[(" + byteAddress + " >> 2)" + (dword != 0 ? " + " + dword : "") + "]";
        }

        private void LoadRaw(Instruction ins)
        {
            List<Operand> o = ins.Operands;
            Operand d = o[0], address = o[1], resource = o[2];
            Destination(d, out int[] components);
            string at = Read(address, address.First(1), V.UInt);
            int[] dwords = resource.Pick(components);
            if (resource.Type == "g")
            {
                string value = dwords.Length == 1 ? GroupSharedIndex(resource, at, dwords[0]) : TypeName(V.UInt, dwords.Length) + "(" + string.Join(", ", dwords.Select(k => GroupSharedIndex(resource, at, k))) + ")";
                Assign(ins, d, value, V.UInt);
                return;
            }
            int count = dwords.Max() + 1;
            string call = ResourceName(resource.Type.Substring(0, 1), resource.Register) + ".Load" + (count == 1 ? "" : count.ToString()) + "(" + at + ")";
            Assign(ins, d, count == 1 ? (dwords.Length == 1 ? call : call + "." + new string('x', dwords.Length)) : call + "." + string.Concat(dwords.Select(c => "xyzw"[c])), V.UInt);
        }

        private void StoreRaw(Instruction ins)
        {
            List<Operand> o = ins.Operands;
            Operand target = o[0], address = o[1], value = o[2];
            int[] components = target.MaskComponents();
            string at = Read(address, address.First(1), V.UInt);
            if (target.Type == "g")
            {
                foreach (int c in components)
                    Line(GroupSharedIndex(target, at, c) + " = " + Read(value, value.Pick(new[] { c }), V.UInt) + ";");
                return;
            }
            Line(ResourceName("u", target.Register) + ".Store" + (components.Length == 1 ? "" : components.Length.ToString()) + "(" + at + ", " + Read(value, value.Pick(components), V.UInt) + ");");
        }

        private string StructuredElement(Operand resource, string index, out BufferView view)
        {
            string key = resource.Type.Substring(0, 1) + resource.Register;
            Binding binding = TextureBinding(resource);
            if (binding == null || !_buffers.TryGetValue(Space(binding.Type) + binding.BindPoint, out view))
                throw new NotSupportedException(key + " isn't a structured buffer in the shader's reflection data.");
            return ResourceName(resource.Type.Substring(0, 1), resource.Register) + "[" + index + "]";
        }

        private List<Leaf> StructuredLeaves(BufferView view, string element, Operand offset, int[] dwords)
        {
            if (!offset.IsLiteral)
            {
                if (view.Generic)
                {
                    string at = Read(offset, offset.First(1), V.UInt);
                    return dwords.Select(k => new Leaf() { Base = element + ".d[(" + at + " >> 2)" + (k != 0 ? " + " + k : "") + "]", Type = V.UInt }).ToList();
                }
                throw new RawFallback(Space(view.Binding.Type) + view.Binding.BindPoint);
            }
            int start = (int)offset.Values[0] / 4;
            List<Leaf> leaves = new List<Leaf>();
            foreach (int k in dwords)
            {
                if (!view.Dwords.TryGetValue(start + k, out Leaf leaf))
                    throw new RawFallback(Space(view.Binding.Type) + view.Binding.BindPoint);
                leaves.Add(new Leaf() { Base = element + leaf.Base, Select = leaf.Select, Type = leaf.Type });
            }
            return leaves;
        }

        private void LoadStructured(Instruction ins)
        {
            List<Operand> o = ins.Operands;
            Operand d = o[0], index = o[1], offset = o[2], resource = o[3];
            Destination(d, out int[] components);
            int[] dwords = resource.Pick(components);
            if (resource.Type == "g")
            {
                int stride = _groupShared[resource.Register].stride / 4;
                string at = Read(index, index.First(1), V.UInt) + (stride != 1 ? " * " + stride : "");
                string byteOffset = Read(offset, offset.First(1), V.UInt);
                string Dword(int k) => "g" + resource.Register + "[" + at + " + (" + byteOffset + " >> 2)" + (k != 0 ? " + " + k : "") + "]";
                Assign(ins, d, dwords.Length == 1 ? Dword(dwords[0]) : TypeName(V.UInt, dwords.Length) + "(" + string.Join(", ", dwords.Select(Dword)) + ")", V.UInt);
                return;
            }
            string element = StructuredElement(resource, Read(index, index.First(1), V.UInt), out BufferView view);
            Assign(ins, d, Combine(StructuredLeaves(view, element, offset, dwords), V.Float), V.Float);
        }

        private void StoreStructured(Instruction ins)
        {
            List<Operand> o = ins.Operands;
            Operand target = o[0], index = o[1], offset = o[2], value = o[3];
            int[] components = target.MaskComponents();
            if (target.Type == "g")
            {
                int stride = _groupShared[target.Register].stride / 4;
                string at = Read(index, index.First(1), V.UInt) + (stride != 1 ? " * " + stride : "");
                string byteOffset = Read(offset, offset.First(1), V.UInt);
                foreach (int c in components)
                    Line("g" + target.Register + "[" + at + " + (" + byteOffset + " >> 2)" + (c != 0 ? " + " + c : "") + "] = " + Read(value, value.Pick(new[] { c }), V.UInt) + ";");
                return;
            }
            string element;
            BufferView view;
            string appendKey = "u" + target.Register;
            if (_appends.TryGetValue(appendKey, out var append) && index.Text == append.index)
            {
                StructuredElement(target, "0", out view);
                element = appendKey + "_new";
            }
            else
                element = StructuredElement(target, Read(index, index.First(1), V.UInt), out view);
            List<Leaf> leaves = StructuredLeaves(view, element, offset, components.Select((c, i) => i).ToArray());
            //Each written dword into its member, in that member's type
            for (int i = 0; i < components.Length; i++)
            {
                Leaf leaf = leaves[i];
                string lhs = leaf.Base + (leaf.Select.Length != 0 ? "." + leaf.Select : "");
                Line(lhs + " = " + Read(value, value.Pick(new[] { components[i] }), leaf.Type) + ";");
            }
            if (_appends.TryGetValue(appendKey, out append) && append.lastStore == _instructionIndex)
            {
                Line(ResourceName("u", target.Register) + ".Append(" + appendKey + "_new);");
                _appends.Remove(appendKey);
            }
        }

        private void Allocate(Instruction ins)
        {
            Operand d = ins.Operands[0], uav = ins.Operands[1];
            Binding binding = TextureBinding(uav);
            if (binding != null && binding.Type == InputType.UavAppendStructured && ins.Op == "imm_atomic_alloc")
            {
                //An Append: the stores that follow into the new element become one Append() after the last of them
                string key = "u" + uav.Register;
                int last = -1;
                for (int i = _instructionIndex + 1; i < _asm.Instructions.Count; i++)
                {
                    Instruction next = _asm.Instructions[i];
                    if (next.Op == "store_structured" && next.Operands[0].Type == "u" && next.Operands[0].Register == uav.Register && next.Operands[1].Text == d.Text)
                        last = i;
                    else if (next.Operands.Count != 0 && next.Operands[0].Type == d.Type && next.Operands[0].Register == d.Register && !next.Op.StartsWith("store") && next.Op != "if_nz" && next.Op != "if_z" && next.Op != "breakc_nz" && next.Op != "breakc_z")
                        break;
                }
                if (last < 0)
                    throw new NotSupportedException("An append to " + key + " with no stores after it isn't supported.");
                BufferView view = _buffers[Space(binding.Type) + binding.BindPoint];
                Line(view.TypeName + " " + key + "_new = (" + view.TypeName + ")0;");
                _appends[key] = (d.Text, last);
                return;
            }
            string call = ResourceName("u", uav.Register) + (ins.Op == "imm_atomic_alloc" ? ".IncrementCounter()" : ".DecrementCounter()");
            Assign(ins, d, call, V.UInt);
        }

        private void LoadTyped(Instruction ins)
        {
            List<Operand> o = ins.Operands;
            Operand d = o[0], address = o[1], uav = o[2];
            Destination(d, out int[] components);
            Binding binding = TextureBinding(uav);
            int count = Coordinates(ins.Dimension ?? "texture2d");
            string call = ResourceName("u", uav.Register) + "[" + Read(address, address.First(count), V.UInt) + "]";
            Assign(ins, d, Fetched(call, uav, components, binding?.Components ?? 1), ReturnView(binding));
        }

        private void StoreTyped(Instruction ins)
        {
            List<Operand> o = ins.Operands;
            Operand uav = o[0], address = o[1], value = o[2];
            Binding binding = TextureBinding(uav);
            if (binding == null) throw new NotSupportedException("u" + uav.Register + " isn't bound in the shader's reflection data.");
            int count;
            switch (binding.Dimension)
            {
                case Dimension.Buffer: case Dimension.Texture1D: count = 1; break;
                case Dimension.Texture1DArray: case Dimension.Texture2D: count = 2; break;
                default: count = 3; break;
            }
            int width = binding.Components;
            int[] written = Enumerable.Range(0, width).ToArray();
            Line(ResourceName("u", uav.Register) + "[" + Read(address, address.First(count), V.UInt) + "] = " + Read(value, value.Pick(written), ReturnView(binding)) + ";");
        }

        private void Atomic(Instruction ins)
        {
            List<Operand> o = ins.Operands;
            bool immediate = ins.Op.StartsWith("imm_");
            string op = immediate ? ins.Op.Substring(11) : ins.Op.Substring(7);
            int at = 0;
            Operand result = immediate ? o[at++] : null;
            Operand target = o[at++], address = o[at++];
            Operand compare = op == "cmp_exch" || op == "cmp_store" ? o[at++] : null;
            Operand value = o[at++];

            string method;
            V v = V.UInt;
            switch (op)
            {
                case "and": method = "InterlockedAnd"; break;
                case "or": method = "InterlockedOr"; break;
                case "xor": method = "InterlockedXor"; break;
                case "iadd": method = "InterlockedAdd"; break;
                case "imax": method = "InterlockedMax"; v = V.Int; break;
                case "imin": method = "InterlockedMin"; v = V.Int; break;
                case "umax": method = "InterlockedMax"; break;
                case "umin": method = "InterlockedMin"; break;
                case "exch": method = "InterlockedExchange"; break;
                case "cmp_exch": case "cmp_store": method = "InterlockedCompareExchange"; break;
                default: throw new NotSupportedException("The atomic '" + ins.Text + "' isn't supported.");
            }
            if (op == "cmp_store") method = "InterlockedCompareStore";

            string location;
            Binding binding = target.Type == "g" ? null : TextureBinding(target);
            if (target.Type == "g")
            {
                int stride = _groupShared[target.Register].stride;
                if (stride == 0)
                    location = GroupSharedIndex(target, Read(address, address.First(1), V.UInt), 0);
                else
                {
                    int[] a = address.First(2);
                    location = "g" + target.Register + "[" + Read(address, new[] { a[0] }, V.UInt) + (stride / 4 != 1 ? " * " + stride / 4 : "") + " + (" + Read(address, new[] { a[1] }, V.UInt) + " >> 2)]";
                }
            }
            else if (binding != null && (binding.Type == InputType.UavRWByteAddress))
                location = null;
            else if (binding != null && (binding.Type == InputType.UavRWStructured || binding.Type == InputType.UavRWStructuredWithCounter))
            {
                int[] a = address.First(2);
                string element = StructuredElement(target, Read(address, new[] { a[0] }, V.UInt), out BufferView view);
                Operand offsetOperand = new Operand() { Type = address.Type, Register = address.Register, Swizzle = "xyzw"[a[1]].ToString(), Values = address.Values, Text = address.Text };
                if (address.IsLiteral) offsetOperand = new Operand() { Type = "l", Values = new[] { address.Values[address.Values.Length == 1 ? 0 : a[1]] }, Text = address.Text };
                Leaf leaf = StructuredLeaves(view, element, offsetOperand, new[] { 0 })[0];
                location = leaf.Base + (leaf.Select.Length != 0 ? "." + leaf.Select : "");
                v = leaf.Type == V.Int ? V.Int : v;
            }
            else
            {
                int count = binding == null ? 1 : binding.Dimension == Dimension.Texture2D || binding.Dimension == Dimension.Texture1DArray ? 2 : binding.Dimension == Dimension.Texture3D || binding.Dimension == Dimension.Texture2DArray ? 3 : 1;
                location = ResourceName("u", target.Register) + "[" + Read(address, address.First(count), V.UInt) + "]";
            }

            List<string> arguments = new List<string>();
            if (compare != null) arguments.Add(Read(compare, compare.First(1), v));
            arguments.Add(Read(value, value.First(1), v));
            if (result != null && !result.IsNull)
            {
                Line("{");
                _indent++;
                Line(TypeName(v, 1) + " original;");
                arguments.Add("original");
                Line(location == null
                    ? ResourceName("u", target.Register) + "." + method + "(" + Read(address, address.First(1), V.UInt) + ", " + string.Join(", ", arguments) + ");"
                    : method + "(" + location + ", " + string.Join(", ", arguments) + ");");
                Assign(ins, result, "original", v);
                _indent--;
                Line("}");
            }
            else
            {
                Line(location == null
                    ? ResourceName("u", target.Register) + "." + method + "(" + Read(address, address.First(1), V.UInt) + ", " + string.Join(", ", arguments) + ");"
                    : method + "(" + location + ", " + string.Join(", ", arguments) + ");");
            }
        }
        #endregion
    }
}
