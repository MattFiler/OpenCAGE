using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.ShaderTools
{
    /// <summary>
    /// What a compiled shader (DXBC) says about itself: its constant buffers and their variables, the textures, samplers
    /// and buffers it binds, and its input and output signatures. The game ships its shaders unstripped, so all of this
    /// carries the names the shaders were written with.
    /// </summary>
    public class DxbcReflection
    {
        public string Creator = "";
        public List<ConstantBuffer> ConstantBuffers = new List<ConstantBuffer>();
        public List<Binding> Bindings = new List<Binding>();
        public List<SignatureElement> Inputs = new List<SignatureElement>();
        public List<SignatureElement> Outputs = new List<SignatureElement>();
        public List<SignatureElement> PatchConstants = new List<SignatureElement>();
        /// <summary>The four-character name of every chunk, in order.</summary>
        public List<string> Chunks = new List<string>();

        #region TYPES
        public enum VariableClass { Scalar = 0, Vector = 1, MatrixRows = 2, MatrixColumns = 3, Object = 4, Struct = 5, InterfaceClass = 6, InterfacePointer = 7 }
        public enum VariableType { Void = 0, Bool = 1, Int = 2, Float = 3, String = 4, UInt = 19, UInt8 = 20, Double = 39 }
        public enum InputType { CBuffer = 0, TBuffer = 1, Texture = 2, Sampler = 3, UavRWTyped = 4, Structured = 5, UavRWStructured = 6, ByteAddress = 7, UavRWByteAddress = 8, UavAppendStructured = 9, UavConsumeStructured = 10, UavRWStructuredWithCounter = 11 }
        public enum Dimension { Unknown = 0, Buffer = 1, Texture1D = 2, Texture1DArray = 3, Texture2D = 4, Texture2DArray = 5, Texture2DMS = 6, Texture2DMSArray = 7, Texture3D = 8, TextureCube = 9, TextureCubeArray = 10, BufferEx = 11 }
        public enum ReturnType { None = 0, UNorm = 1, SNorm = 2, SInt = 3, UInt = 4, Float = 5, Mixed = 6, Double = 7, Continued = 8 }

        public class Type
        {
            public VariableClass Class;
            public VariableType Base;
            public int Rows, Columns, Elements;
            public string Name = "";
            public List<Member> Members = new List<Member>();

            public bool IsMatrix => Class == VariableClass.MatrixRows || Class == VariableClass.MatrixColumns;

            /// <summary>The type as HLSL writes it, without the array part.</summary>
            public string HlslName
            {
                get
                {
                    if (Class == VariableClass.Struct)
                        return Name;
                    string scalar;
                    switch (Base)
                    {
                        case VariableType.Bool: scalar = "bool"; break;
                        case VariableType.Int: scalar = "int"; break;
                        case VariableType.UInt: case VariableType.UInt8: scalar = "uint"; break;
                        case VariableType.Double: scalar = "double"; break;
                        default: scalar = "float"; break;
                    }
                    switch (Class)
                    {
                        case VariableClass.Scalar: return scalar;
                        case VariableClass.Vector: return scalar + Columns;
                        case VariableClass.MatrixRows: return "row_major " + scalar + Rows + "x" + Columns;
                        case VariableClass.MatrixColumns: return scalar + Rows + "x" + Columns;
                        default: return scalar;
                    }
                }
            }

            /// <summary>The bytes one element takes in a structured buffer, where nothing is padded to 16 bytes.</summary>
            public int PackedSize
            {
                get
                {
                    int one;
                    if (Class == VariableClass.Struct)
                        one = Members.Count == 0 ? 0 : Members.Max(o => o.Offset + o.Type.PackedSize * Math.Max(1, o.Type.Elements));
                    else
                        one = 4 * Math.Max(1, Rows) * Math.Max(1, Columns) * (Base == VariableType.Double ? 2 : 1);
                    return one;
                }
            }
        }

        public class Member
        {
            public string Name;
            public int Offset;
            public Type Type;
        }

        public class Variable
        {
            public string Name;
            public int Offset, Size;
            public bool Used;
            public Type Type;
        }

        public class ConstantBuffer
        {
            public string Name;
            public int Size;
            /// <summary>0 cbuffer, 1 tbuffer, 3 the element type of a structured buffer (named after it).</summary>
            public int Kind;
            public List<Variable> Variables = new List<Variable>();
        }

        public class Binding
        {
            public string Name;
            public InputType Type;
            public ReturnType Return;
            public Dimension Dimension;
            /// <summary>Samples of a multisampled texture, or the stride of a structured buffer.</summary>
            public int NumSamples;
            public int BindPoint, BindCount;
            public uint Flags;
            public int Components => (int)((Flags >> 2) & 3) + 1;
            public bool ComparisonSampler => (Flags & 2) != 0;
        }

        public class SignatureElement
        {
            public string Name;
            public int SemanticIndex;
            public int SystemValue;
            /// <summary>1 uint, 2 int, 3 float.</summary>
            public int ComponentType;
            public int Register;
            public int Mask, ReadWriteMask;
            public int Stream;

            public int FirstComponent { get { for (int i = 0; i < 4; i++) if ((Mask & (1 << i)) != 0) return i; return 0; } }
            public int ComponentCount { get { int n = 0; for (int i = 0; i < 4; i++) if ((Mask & (1 << i)) != 0) n++; return n; } }
            public override string ToString() => Name + SemanticIndex + " r" + Register + " mask " + Mask + " sv " + SystemValue + " type " + ComponentType + " stream " + Stream;
        }
        #endregion

        /// <summary>The chunks of a DXBC container, by four-character name (the first of each).</summary>
        public static Dictionary<string, byte[]> ReadChunks(byte[] dxbc, List<string> order = null)
        {
            Dictionary<string, byte[]> chunks = new Dictionary<string, byte[]>();
            if (dxbc == null || dxbc.Length < 32 || Encoding.ASCII.GetString(dxbc, 0, 4) != "DXBC")
                return chunks;
            int count = BitConverter.ToInt32(dxbc, 28);
            for (int i = 0; i < count; i++)
            {
                int offset = BitConverter.ToInt32(dxbc, 32 + i * 4);
                string fourcc = Encoding.ASCII.GetString(dxbc, offset, 4);
                int size = BitConverter.ToInt32(dxbc, offset + 4);
                byte[] data = new byte[size];
                Buffer.BlockCopy(dxbc, offset + 8, data, 0, size);
                order?.Add(fourcc);
                if (!chunks.ContainsKey(fourcc))
                    chunks.Add(fourcc, data);
            }
            return chunks;
        }

        public static DxbcReflection Read(byte[] dxbc)
        {
            DxbcReflection reflection = new DxbcReflection();
            Dictionary<string, byte[]> chunks = ReadChunks(dxbc, reflection.Chunks);
            if (chunks.TryGetValue("RDEF", out byte[] rdef))
                reflection.ReadRdef(rdef);
            foreach (KeyValuePair<string, byte[]> chunk in chunks)
            {
                switch (chunk.Key)
                {
                    case "ISGN": reflection.Inputs = ReadSignature(chunk.Value, 24); break;
                    case "ISG1": reflection.Inputs = ReadSignature(chunk.Value, 32); break;
                    case "OSGN": reflection.Outputs = ReadSignature(chunk.Value, 24); break;
                    case "OSG5": reflection.Outputs = ReadSignature(chunk.Value, 28); break;
                    case "OSG1": reflection.Outputs = ReadSignature(chunk.Value, 32); break;
                    case "PCSG": reflection.PatchConstants = ReadSignature(chunk.Value, 24); break;
                    case "PSG1": reflection.PatchConstants = ReadSignature(chunk.Value, 32); break;
                }
            }
            return reflection;
        }

        private static string Text(byte[] data, int offset)
        {
            if (offset <= 0 || offset >= data.Length) return "";
            int end = offset;
            while (end < data.Length && data[end] != 0) end++;
            return Encoding.ASCII.GetString(data, offset, end - offset);
        }

        private static List<SignatureElement> ReadSignature(byte[] data, int elementSize)
        {
            List<SignatureElement> elements = new List<SignatureElement>();
            int count = BitConverter.ToInt32(data, 0);
            int start = BitConverter.ToInt32(data, 4);
            for (int i = 0; i < count; i++)
            {
                int at = start + i * elementSize;
                SignatureElement element = new SignatureElement();
                if (elementSize >= 28)
                {
                    element.Stream = BitConverter.ToInt32(data, at);
                    at += 4;
                }
                element.Name = Text(data, BitConverter.ToInt32(data, at));
                element.SemanticIndex = BitConverter.ToInt32(data, at + 4);
                element.SystemValue = BitConverter.ToInt32(data, at + 8);
                element.ComponentType = BitConverter.ToInt32(data, at + 12);
                element.Register = BitConverter.ToInt32(data, at + 16);
                element.Mask = data[at + 20];
                element.ReadWriteMask = data[at + 21];
                elements.Add(element);
            }
            return elements;
        }

        private void ReadRdef(byte[] data)
        {
            int cbCount = BitConverter.ToInt32(data, 0);
            int cbOffset = BitConverter.ToInt32(data, 4);
            int bindCount = BitConverter.ToInt32(data, 8);
            int bindOffset = BitConverter.ToInt32(data, 12);
            int major = data[17];
            Creator = Text(data, BitConverter.ToInt32(data, 24));
            int variableSize = major >= 5 ? 40 : 24;
            int typeSize = major >= 5 ? 36 : 16;
            Dictionary<int, Type> types = new Dictionary<int, Type>();

            for (int i = 0; i < bindCount; i++)
            {
                int at = bindOffset + i * 32;
                Bindings.Add(new Binding()
                {
                    Name = Text(data, BitConverter.ToInt32(data, at)),
                    Type = (InputType)BitConverter.ToInt32(data, at + 4),
                    Return = (ReturnType)BitConverter.ToInt32(data, at + 8),
                    Dimension = (Dimension)BitConverter.ToInt32(data, at + 12),
                    NumSamples = BitConverter.ToInt32(data, at + 16),
                    BindPoint = BitConverter.ToInt32(data, at + 20),
                    BindCount = BitConverter.ToInt32(data, at + 24),
                    Flags = BitConverter.ToUInt32(data, at + 28),
                });
            }

            for (int i = 0; i < cbCount; i++)
            {
                int at = cbOffset + i * 24;
                ConstantBuffer buffer = new ConstantBuffer()
                {
                    Name = Text(data, BitConverter.ToInt32(data, at)),
                    Size = BitConverter.ToInt32(data, at + 12),
                    Kind = BitConverter.ToInt32(data, at + 20),
                };
                int varCount = BitConverter.ToInt32(data, at + 4);
                int varOffset = BitConverter.ToInt32(data, at + 8);
                for (int v = 0; v < varCount; v++)
                {
                    int va = varOffset + v * variableSize;
                    buffer.Variables.Add(new Variable()
                    {
                        Name = Text(data, BitConverter.ToInt32(data, va)),
                        Offset = BitConverter.ToInt32(data, va + 4),
                        Size = BitConverter.ToInt32(data, va + 8),
                        Used = (BitConverter.ToInt32(data, va + 12) & 2) != 0,
                        Type = ReadType(data, BitConverter.ToInt32(data, va + 16), typeSize, types),
                    });
                }
                ConstantBuffers.Add(buffer);
            }
        }

        private static Type ReadType(byte[] data, int offset, int typeSize, Dictionary<int, Type> types)
        {
            if (types.TryGetValue(offset, out Type known))
                return known;
            Type type = new Type()
            {
                Class = (VariableClass)BitConverter.ToUInt16(data, offset),
                Base = (VariableType)BitConverter.ToUInt16(data, offset + 2),
                Rows = BitConverter.ToUInt16(data, offset + 4),
                Columns = BitConverter.ToUInt16(data, offset + 6),
                Elements = BitConverter.ToUInt16(data, offset + 8),
            };
            types[offset] = type;
            int memberCount = BitConverter.ToUInt16(data, offset + 10);
            int memberOffset = BitConverter.ToInt32(data, offset + 12);
            if (typeSize >= 36)
                type.Name = Text(data, BitConverter.ToInt32(data, offset + 32));
            for (int m = 0; m < memberCount; m++)
            {
                int ma = memberOffset + m * 12;
                type.Members.Add(new Member()
                {
                    Name = Text(data, BitConverter.ToInt32(data, ma)),
                    Type = ReadType(data, BitConverter.ToInt32(data, ma + 4), typeSize, types),
                    Offset = BitConverter.ToInt32(data, ma + 8),
                });
            }
            return type;
        }

        /// <summary>Where two shaders' interfaces differ (signatures, constant buffers and bindings), one line each; empty when they match.</summary>
        public static List<string> Compare(DxbcReflection a, DxbcReflection b)
        {
            List<string> differences = new List<string>();
            void Signature(string what, List<SignatureElement> x, List<SignatureElement> y)
            {
                if (x.Count != y.Count) { differences.Add(what + ": " + x.Count + " elements, now " + y.Count); return; }
                for (int i = 0; i < x.Count; i++)
                {
                    SignatureElement p = x[i], q = y[i];
                    if (!string.Equals(p.Name, q.Name, StringComparison.OrdinalIgnoreCase) || p.SemanticIndex != q.SemanticIndex || p.SystemValue != q.SystemValue || p.ComponentType != q.ComponentType || p.Register != q.Register || p.Mask != q.Mask || p.Stream != q.Stream)
                        differences.Add(what + " element " + i + ": " + p + ", now " + q);
                }
            }
            Signature("input signature", a.Inputs, b.Inputs);
            Signature("output signature", a.Outputs, b.Outputs);
            Signature("patch constant signature", a.PatchConstants, b.PatchConstants);
            foreach (Binding binding in a.Bindings)
            {
                Binding other = b.Bindings.FirstOrDefault(o => o.BindPoint == binding.BindPoint && Space(o.Type) == Space(binding.Type));
                if (other == null) { differences.Add(binding.Name + " (" + Space(binding.Type) + binding.BindPoint + ") is no longer bound"); continue; }
                if (other.Type != binding.Type || other.Dimension != binding.Dimension || other.Return != binding.Return || other.NumSamples != binding.NumSamples || other.BindCount != binding.BindCount || other.Components != binding.Components || other.ComparisonSampler != binding.ComparisonSampler)
                    differences.Add(binding.Name + " (" + Space(binding.Type) + binding.BindPoint + ") is now " + other.Type + " " + other.Dimension + " " + other.Return + "x" + other.Components + (other.NumSamples != 0 ? " " + other.NumSamples : "") + ", was " + binding.Type + " " + binding.Dimension + " " + binding.Return + "x" + binding.Components + (binding.NumSamples != 0 ? " " + binding.NumSamples : ""));
                if (binding.Type == InputType.CBuffer)
                {
                    ConstantBuffer x = a.ConstantBuffers.FirstOrDefault(o => o.Name == binding.Name && o.Kind == 0);
                    ConstantBuffer y = b.ConstantBuffers.FirstOrDefault(o => o.Name == other.Name && o.Kind == 0);
                    if (x != null && y != null && x.Size != y.Size)
                        differences.Add("constant buffer " + binding.Name + " is " + y.Size + " bytes, was " + x.Size);
                }
            }
            foreach (Binding binding in b.Bindings)
                if (!a.Bindings.Any(o => o.BindPoint == binding.BindPoint && Space(o.Type) == Space(binding.Type)))
                    differences.Add(binding.Name + " (" + Space(binding.Type) + binding.BindPoint + ") is newly bound");
            return differences;
        }

        /// <summary>The register letter a binding of this type takes: b, t, s or u.</summary>
        public static string Space(InputType type)
        {
            switch (type)
            {
                case InputType.CBuffer: return "b";
                case InputType.Sampler: return "s";
                case InputType.UavRWTyped:
                case InputType.UavRWStructured:
                case InputType.UavRWByteAddress:
                case InputType.UavAppendStructured:
                case InputType.UavConsumeStructured:
                case InputType.UavRWStructuredWithCounter: return "u";
                default: return "t";
            }
        }
    }
}
