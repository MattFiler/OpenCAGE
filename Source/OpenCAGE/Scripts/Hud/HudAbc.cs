using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.Hud
{
    /// <summary>
    /// A movie's ActionScript 3 bytecode block (an .abc, the body of a DoABC tag), read and written losslessly (byte for byte on a
    /// round trip), with find-or-add helpers for its constant pools.
    /// </summary>
    public sealed class AbcFile
    {
        public ushort Minor = 16, Major = 46;
        public List<uint> Ints = new List<uint> { 0 }, UInts = new List<uint> { 0 };
        public List<byte[]> Doubles = new List<byte[]> { null };
        public List<byte[]> Strings = new List<byte[]> { null };
        public List<AbcNamespace> Namespaces = new List<AbcNamespace> { null };
        public List<List<int>> NsSets = new List<List<int>> { null };
        public List<AbcMultiname> Multinames = new List<AbcMultiname> { null };
        public List<AbcMethod> Methods = new List<AbcMethod>();
        public List<AbcMetadata> Metadata = new List<AbcMetadata>();
        public List<AbcInstance> Instances = new List<AbcInstance>();
        public List<AbcClass> Classes = new List<AbcClass>();
        public List<AbcScript> Scripts = new List<AbcScript>();
        public List<AbcBody> Bodies = new List<AbcBody>();

        #region Reading and writing
        public static AbcFile Read(byte[] data)
        {
            AbcReader r = new AbcReader(data);
            AbcFile a = new AbcFile();
            try
            {
                a.Minor = r.U16(); a.Major = r.U16();
                int n = r.Count(); for (int i = 1; i < n; i++) a.Ints.Add(r.U32());
                n = r.Count(); for (int i = 1; i < n; i++) a.UInts.Add(r.U32());
                n = r.Count(); for (int i = 1; i < n; i++) a.Doubles.Add(r.Bytes(8));
                n = r.Count(); for (int i = 1; i < n; i++) a.Strings.Add(r.Bytes(r.U30()));
                n = r.Count(); for (int i = 1; i < n; i++) a.Namespaces.Add(new AbcNamespace() { Kind = r.U8(), Name = r.U30() });
                n = r.Count();
                for (int i = 1; i < n; i++)
                {
                    int c = r.Count(); List<int> set = new List<int>();
                    for (int k = 0; k < c; k++) set.Add(r.U30());
                    a.NsSets.Add(set);
                }
                n = r.Count();
                for (int i = 1; i < n; i++)
                {
                    AbcMultiname m = new AbcMultiname() { Kind = r.U8() };
                    switch (m.Kind)
                    {
                        case AbcMultiname.QName: case AbcMultiname.QNameA: m.A = r.U30(); m.B = r.U30(); break;
                        case AbcMultiname.RTQName: case AbcMultiname.RTQNameA: m.A = r.U30(); break;
                        case AbcMultiname.RTQNameL: case AbcMultiname.RTQNameLA: break;
                        case AbcMultiname.Multiname: case AbcMultiname.MultinameA: m.A = r.U30(); m.B = r.U30(); break;
                        case AbcMultiname.MultinameL: case AbcMultiname.MultinameLA: m.B = r.U30(); break;
                        case AbcMultiname.TypeName:
                            m.A = r.U30();
                            int pc = r.Count();
                            for (int k = 0; k < pc; k++) m.Params.Add(r.U30());
                            break;
                        default: throw new InvalidDataException("multiname " + i + " has an unknown kind 0x" + m.Kind.ToString("X2"));
                    }
                    a.Multinames.Add(m);
                }
                n = r.Count();
                for (int i = 0; i < n; i++)
                {
                    AbcMethod m = new AbcMethod();
                    int pc = r.Count();
                    m.ReturnType = r.U30();
                    for (int k = 0; k < pc; k++) m.ParamTypes.Add(r.U30());
                    m.Name = r.U30();
                    m.Flags = r.U8();
                    if ((m.Flags & AbcMethod.HasOptional) != 0)
                    {
                        int oc = r.Count();
                        for (int k = 0; k < oc; k++) { int v = r.U30(); m.Options.Add(new AbcOption() { Value = v, Kind = r.U8() }); }
                    }
                    if ((m.Flags & AbcMethod.HasParamNames) != 0)
                        for (int k = 0; k < pc; k++) m.ParamNames.Add(r.U30());
                    a.Methods.Add(m);
                }
                n = r.Count();
                for (int i = 0; i < n; i++)
                {
                    AbcMetadata md = new AbcMetadata() { Name = r.U30() };
                    int ic = r.Count();
                    for (int k = 0; k < ic * 2; k++) md.Items.Add(r.U30());
                    a.Metadata.Add(md);
                }
                n = r.Count();
                for (int i = 0; i < n; i++)
                {
                    AbcInstance ii = new AbcInstance() { Name = r.U30(), Super = r.U30(), Flags = r.U8() };
                    if ((ii.Flags & AbcInstance.ProtectedNsFlag) != 0) ii.ProtectedNs = r.U30();
                    int ic = r.Count();
                    for (int k = 0; k < ic; k++) ii.Interfaces.Add(r.U30());
                    ii.IInit = r.U30();
                    ii.Traits = AbcTrait.ReadAll(r);
                    a.Instances.Add(ii);
                }
                for (int i = 0; i < n; i++) a.Classes.Add(new AbcClass() { CInit = r.U30(), Traits = AbcTrait.ReadAll(r) });
                n = r.Count();
                for (int i = 0; i < n; i++) a.Scripts.Add(new AbcScript() { Init = r.U30(), Traits = AbcTrait.ReadAll(r) });
                n = r.Count();
                for (int i = 0; i < n; i++)
                {
                    AbcBody mb = new AbcBody() { Method = r.U30(), MaxStack = r.U30(), LocalCount = r.U30(), InitScope = r.U30(), MaxScope = r.U30() };
                    mb.Code = r.Bytes(r.U30());
                    int ec = r.Count();
                    for (int k = 0; k < ec; k++)
                        mb.Exceptions.Add(new AbcException() { From = r.U30(), To = r.U30(), Target = r.U30(), ExcType = r.U30(), VarName = r.U30() });
                    mb.Traits = AbcTrait.ReadAll(r);
                    a.Bodies.Add(mb);
                }
            }
            catch (IndexOutOfRangeException)
            {
                throw new InvalidDataException("the ActionScript block is cut short");
            }
            catch (ArgumentException)
            {
                throw new InvalidDataException("the ActionScript block is cut short");
            }
            if (r.Position != data.Length)
                throw new InvalidDataException("the ActionScript block has " + (data.Length - r.Position) + " bytes after its end");
            return a;
        }

        public byte[] Write()
        {
            AbcWriter w = new AbcWriter();
            w.U16(Minor); w.U16(Major);
            w.U30(Ints.Count == 1 ? 0 : Ints.Count); foreach (uint v in Ints.Skip(1)) w.U32(v);
            w.U30(UInts.Count == 1 ? 0 : UInts.Count); foreach (uint v in UInts.Skip(1)) w.U32(v);
            w.U30(Doubles.Count == 1 ? 0 : Doubles.Count); foreach (byte[] v in Doubles.Skip(1)) w.Bytes(v);
            w.U30(Strings.Count == 1 ? 0 : Strings.Count); foreach (byte[] v in Strings.Skip(1)) { w.U30(v.Length); w.Bytes(v); }
            w.U30(Namespaces.Count == 1 ? 0 : Namespaces.Count); foreach (AbcNamespace v in Namespaces.Skip(1)) { w.U8(v.Kind); w.U30(v.Name); }
            w.U30(NsSets.Count == 1 ? 0 : NsSets.Count); foreach (List<int> v in NsSets.Skip(1)) { w.U30(v.Count); foreach (int x in v) w.U30(x); }
            w.U30(Multinames.Count == 1 ? 0 : Multinames.Count);
            foreach (AbcMultiname m in Multinames.Skip(1))
            {
                w.U8(m.Kind);
                switch (m.Kind)
                {
                    case AbcMultiname.QName: case AbcMultiname.QNameA: case AbcMultiname.Multiname: case AbcMultiname.MultinameA: w.U30(m.A); w.U30(m.B); break;
                    case AbcMultiname.RTQName: case AbcMultiname.RTQNameA: w.U30(m.A); break;
                    case AbcMultiname.MultinameL: case AbcMultiname.MultinameLA: w.U30(m.B); break;
                    case AbcMultiname.TypeName: w.U30(m.A); w.U30(m.Params.Count); foreach (int x in m.Params) w.U30(x); break;
                }
            }
            w.U30(Methods.Count);
            foreach (AbcMethod m in Methods)
            {
                w.U30(m.ParamTypes.Count); w.U30(m.ReturnType); foreach (int x in m.ParamTypes) w.U30(x); w.U30(m.Name); w.U8(m.Flags);
                if ((m.Flags & AbcMethod.HasOptional) != 0) { w.U30(m.Options.Count); foreach (AbcOption o in m.Options) { w.U30(o.Value); w.U8(o.Kind); } }
                if ((m.Flags & AbcMethod.HasParamNames) != 0) foreach (int x in m.ParamNames) w.U30(x);
            }
            w.U30(Metadata.Count); foreach (AbcMetadata md in Metadata) { w.U30(md.Name); w.U30(md.Items.Count / 2); foreach (int x in md.Items) w.U30(x); }
            w.U30(Instances.Count);
            foreach (AbcInstance ii in Instances)
            {
                w.U30(ii.Name); w.U30(ii.Super); w.U8(ii.Flags); if ((ii.Flags & AbcInstance.ProtectedNsFlag) != 0) w.U30(ii.ProtectedNs);
                w.U30(ii.Interfaces.Count); foreach (int x in ii.Interfaces) w.U30(x); w.U30(ii.IInit); AbcTrait.WriteAll(w, ii.Traits);
            }
            foreach (AbcClass c in Classes) { w.U30(c.CInit); AbcTrait.WriteAll(w, c.Traits); }
            w.U30(Scripts.Count); foreach (AbcScript sc in Scripts) { w.U30(sc.Init); AbcTrait.WriteAll(w, sc.Traits); }
            w.U30(Bodies.Count);
            foreach (AbcBody mb in Bodies)
            {
                w.U30(mb.Method); w.U30(mb.MaxStack); w.U30(mb.LocalCount); w.U30(mb.InitScope); w.U30(mb.MaxScope);
                w.U30(mb.Code.Length); w.Bytes(mb.Code);
                w.U30(mb.Exceptions.Count); foreach (AbcException e in mb.Exceptions) { w.U30(e.From); w.U30(e.To); w.U30(e.Target); w.U30(e.ExcType); w.U30(e.VarName); }
                AbcTrait.WriteAll(w, mb.Traits);
            }
            return w.ToArray();
        }
        #endregion

        #region Names
        public string Str(int i) => i <= 0 || i >= Strings.Count ? "*" : Encoding.UTF8.GetString(Strings[i]);

        /// <summary>A multiname's local name ("*" for none / runtime).</summary>
        public string MnName(int i)
        {
            if (i <= 0 || i >= Multinames.Count) return "*";
            AbcMultiname m = Multinames[i];
            switch (m.Kind)
            {
                case AbcMultiname.QName: case AbcMultiname.QNameA: return Str(m.B);
                case AbcMultiname.Multiname: case AbcMultiname.MultinameA: case AbcMultiname.RTQName: case AbcMultiname.RTQNameA: return Str(m.A);
                case AbcMultiname.TypeName: return MnName(m.A) + ".<" + string.Join(",", m.Params.Select(MnName)) + ">";
                default: return "*";
            }
        }

        /// <summary>A multiname's namespace (package) name for a QName, or null.</summary>
        public string MnNamespace(int i)
        {
            if (i <= 0 || i >= Multinames.Count) return null;
            AbcMultiname m = Multinames[i];
            if ((m.Kind != AbcMultiname.QName && m.Kind != AbcMultiname.QNameA) || m.A <= 0 || m.A >= Namespaces.Count) return null;
            return Str(Namespaces[m.A].Name);
        }

        /// <summary>"package.Name" for a QName ("Name" in the top-level package).</summary>
        public string MnFullName(int i)
        {
            string ns = MnNamespace(i);
            return string.IsNullOrEmpty(ns) ? MnName(i) : ns + "." + MnName(i);
        }

        public int String(string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s);
            for (int i = 1; i < Strings.Count; i++) if (Strings[i].SequenceEqual(b)) return i;
            Strings.Add(b);
            return Strings.Count - 1;
        }

        public int Namespace(byte kind, string name)
        {
            int n = String(name);
            for (int i = 1; i < Namespaces.Count; i++) if (Namespaces[i].Kind == kind && Namespaces[i].Name == n) return i;
            Namespaces.Add(new AbcNamespace() { Kind = kind, Name = n });
            return Namespaces.Count - 1;
        }

        public int QName(int ns, string name)
        {
            int n = String(name);
            for (int i = 1; i < Multinames.Count; i++)
            {
                AbcMultiname m = Multinames[i];
                if (m.Kind == AbcMultiname.QName && m.A == ns && m.B == n) return i;
            }
            Multinames.Add(new AbcMultiname() { Kind = AbcMultiname.QName, A = ns, B = n });
            return Multinames.Count - 1;
        }

        public int NsSet(params int[] namespaces)
        {
            for (int i = 1; i < NsSets.Count; i++) if (NsSets[i].SequenceEqual(namespaces)) return i;
            NsSets.Add(namespaces.ToList());
            return NsSets.Count - 1;
        }

        /// <summary>A name looked up in any of a set of namespaces (what compilers write for a member of an untyped value).</summary>
        public int MultinameIn(string name, int nsSet)
        {
            int n = String(name);
            for (int i = 1; i < Multinames.Count; i++)
            {
                AbcMultiname m = Multinames[i];
                if (m.Kind == AbcMultiname.Multiname && m.A == n && m.B == nsSet) return i;
            }
            Multinames.Add(new AbcMultiname() { Kind = AbcMultiname.Multiname, A = n, B = nsSet });
            return Multinames.Count - 1;
        }

        /// <summary>A name taken from the stack (array[index]), looked up in a set of namespaces.</summary>
        public int MultinameL(int nsSet)
        {
            for (int i = 1; i < Multinames.Count; i++)
            {
                AbcMultiname m = Multinames[i];
                if (m.Kind == AbcMultiname.MultinameL && m.B == nsSet) return i;
            }
            Multinames.Add(new AbcMultiname() { Kind = AbcMultiname.MultinameL, B = nsSet });
            return Multinames.Count - 1;
        }

        /// <summary>A public name (the package "" namespace): properties and methods of built-in and generated classes.</summary>
        public int Pub(string name) => QName(Namespace(AbcNamespace.Package, ""), name);
        /// <summary>A class in a package: Pkg("flash.text", "TextField").</summary>
        public int Pkg(string package, string name) => QName(Namespace(AbcNamespace.Package, package), name);

        public int Int(int v)
        {
            uint u = (uint)v;
            for (int i = 1; i < Ints.Count; i++) if (Ints[i] == u) return i;
            Ints.Add(u);
            return Ints.Count - 1;
        }

        public int Double(double v)
        {
            byte[] b = BitConverter.GetBytes(v);
            for (int i = 1; i < Doubles.Count; i++) if (Doubles[i].SequenceEqual(b)) return i;
            Doubles.Add(b);
            return Doubles.Count - 1;
        }

        public double DoubleAt(int i) => i <= 0 || i >= Doubles.Count ? double.NaN : BitConverter.ToDouble(Doubles[i], 0);

        /// <summary>The body of a method, or null.</summary>
        public AbcBody BodyOf(int method) => Bodies.FirstOrDefault(o => o.Method == method);
        #endregion
    }

    public sealed class AbcNamespace
    {
        public const byte Namespace = 0x08, Package = 0x16, PackageInternal = 0x17, Protected = 0x18, Explicit = 0x19, StaticProtected = 0x1A, Private = 0x05;
        public byte Kind;
        public int Name;
    }

    public sealed class AbcMultiname
    {
        public const byte QName = 0x07, QNameA = 0x0D, RTQName = 0x0F, RTQNameA = 0x10, RTQNameL = 0x11, RTQNameLA = 0x12,
            Multiname = 0x09, MultinameA = 0x0E, MultinameL = 0x1B, MultinameLA = 0x1C, TypeName = 0x1D;
        public byte Kind;
        /// <summary>QName: namespace, name. RTQName: name. Multiname: name, namespace set. MultinameL: (B) namespace set. TypeName: the generic.</summary>
        public int A, B;
        public List<int> Params = new List<int>();

        /// <summary>How many values the name itself takes off the stack when used by an instruction.</summary>
        public int RuntimeParts
        {
            get
            {
                switch (Kind)
                {
                    case RTQName: case RTQNameA: return 1;
                    case RTQNameL: case RTQNameLA: return 2;
                    case MultinameL: case MultinameLA: return 1;
                    default: return 0;
                }
            }
        }
    }

    public sealed class AbcOption { public int Value; public byte Kind; }

    public sealed class AbcMethod
    {
        public const byte NeedArguments = 0x01, NeedActivation = 0x02, NeedRest = 0x04, HasOptional = 0x08, SetDxns = 0x40, HasParamNames = 0x80;
        public int ReturnType, Name;
        public byte Flags;
        public List<int> ParamTypes = new List<int>();
        public List<AbcOption> Options = new List<AbcOption>();
        public List<int> ParamNames = new List<int>();
    }

    public sealed class AbcMetadata { public int Name; public List<int> Items = new List<int>(); }

    public sealed class AbcInstance
    {
        public const byte Sealed = 0x01, Final = 0x02, Interface = 0x04, ProtectedNsFlag = 0x08;
        public int Name, Super;
        public byte Flags;
        public int ProtectedNs;
        public List<int> Interfaces = new List<int>();
        public int IInit;
        public List<AbcTrait> Traits = new List<AbcTrait>();
    }

    public sealed class AbcClass { public int CInit; public List<AbcTrait> Traits = new List<AbcTrait>(); }
    public sealed class AbcScript { public int Init; public List<AbcTrait> Traits = new List<AbcTrait>(); }
    public sealed class AbcException { public int From, To, Target, ExcType, VarName; }

    public sealed class AbcBody
    {
        public int Method, MaxStack, LocalCount, InitScope, MaxScope;
        public byte[] Code;
        public List<AbcException> Exceptions = new List<AbcException>();
        public List<AbcTrait> Traits = new List<AbcTrait>();
    }

    public sealed class AbcTrait
    {
        public const byte Slot = 0, Method = 1, Getter = 2, Setter = 3, Class = 4, Function = 5, Const = 6;
        public const byte AttrFinal = 0x10, AttrOverride = 0x20, AttrMetadata = 0x40;

        public int Name;
        /// <summary>Low four bits the kind, high four the attributes.</summary>
        public byte Kind;
        /// <summary>Slot/const: A = slot id, B = type, C = value index (+ VKind). Method/getter/setter: A = disp id, B = method. Class: A = slot id, B = class. Function: A = slot id, B = function.</summary>
        public int A, B, C;
        public byte VKind;
        public List<int> Metadata = new List<int>();
        public int Type => Kind & 0x0F;

        public static List<AbcTrait> ReadAll(AbcReader r)
        {
            int n = r.Count();
            List<AbcTrait> list = new List<AbcTrait>();
            for (int i = 0; i < n; i++)
            {
                AbcTrait t = new AbcTrait() { Name = r.U30(), Kind = r.U8() };
                switch (t.Type)
                {
                    case Slot: case Const: t.A = r.U30(); t.B = r.U30(); t.C = r.U30(); if (t.C != 0) t.VKind = r.U8(); break;
                    case Method: case Getter: case Setter: case Class: case Function: t.A = r.U30(); t.B = r.U30(); break;
                    default: throw new InvalidDataException("a trait has an unknown kind " + t.Type);
                }
                if ((t.Kind & AttrMetadata) != 0) { int m = r.Count(); for (int k = 0; k < m; k++) t.Metadata.Add(r.U30()); }
                list.Add(t);
            }
            return list;
        }

        public static void WriteAll(AbcWriter w, List<AbcTrait> list)
        {
            w.U30(list.Count);
            foreach (AbcTrait t in list)
            {
                w.U30(t.Name); w.U8(t.Kind);
                switch (t.Type)
                {
                    case Slot: case Const: w.U30(t.A); w.U30(t.B); w.U30(t.C); if (t.C != 0) w.U8(t.VKind); break;
                    default: w.U30(t.A); w.U30(t.B); break;
                }
                if ((t.Kind & AttrMetadata) != 0) { w.U30(t.Metadata.Count); foreach (int m in t.Metadata) w.U30(m); }
            }
        }
    }

    public sealed class AbcReader
    {
        private readonly byte[] _b;
        public int Position;
        public AbcReader(byte[] b, int position = 0) { _b = b; Position = position; }
        public bool AtEnd => Position >= _b.Length;
        public byte U8() => _b[Position++];
        public ushort U16() { ushort v = (ushort)(_b[Position] | (_b[Position + 1] << 8)); Position += 2; return v; }
        public uint U32()
        {
            uint r = 0;
            for (int s = 0; s < 35; s += 7)
            {
                byte x = _b[Position++];
                r |= (uint)(x & 0x7F) << s;
                if ((x & 0x80) == 0) break;
            }
            return r;
        }
        public int U30() => (int)(U32() & 0x3FFFFFFF);
        public int S24() { int v = _b[Position] | (_b[Position + 1] << 8) | (_b[Position + 2] << 16); Position += 3; return (v << 8) >> 8; }
        /// <summary>A count that has to fit in what is left (a corrupt count is refused rather than allocated).</summary>
        public int Count()
        {
            int n = U30();
            if (n > _b.Length - Position + 1)
                throw new InvalidDataException("a count in the ActionScript block (" + n + ") is larger than the block");
            return n;
        }
        public byte[] Bytes(int n)
        {
            if (n < 0 || Position + n > _b.Length) throw new InvalidDataException("the ActionScript block is cut short");
            byte[] r = new byte[n];
            Array.Copy(_b, Position, r, 0, n);
            Position += n;
            return r;
        }
    }

    public sealed class AbcWriter
    {
        private readonly MemoryStream _s = new MemoryStream();
        public void U8(int v) => _s.WriteByte((byte)v);
        public void U16(int v) { _s.WriteByte((byte)v); _s.WriteByte((byte)(v >> 8)); }
        public void U32(uint v) { do { byte x = (byte)(v & 0x7F); v >>= 7; if (v != 0) x |= 0x80; _s.WriteByte(x); } while (v != 0); }
        public void U30(int v) => U32((uint)v);
        public void Bytes(byte[] d) => _s.Write(d, 0, d.Length);
        public byte[] ToArray() => _s.ToArray();
    }

    /// <summary>
    /// The AVM2 instruction set: each opcode's name and operands, for checking code (<see cref="HudMovieCheck"/>) and writing it (<see cref="AbcCode"/>).
    /// Operand letters: m multiname, s string, i int, I uint, d double, n namespace, M method, C class, x exception, L local,
    /// a argument count, u other u30, b byte, B signed byte, o branch offset (s24), W lookupswitch, D debug, S slot.
    /// </summary>
    public static class AbcOps
    {
        public sealed class Op
        {
            public byte Code;
            public string Name, Operands;
            /// <summary>The instruction never falls through to the next (return, throw, jump, lookupswitch).</summary>
            public bool Ends;
            public bool Branch;
        }

        private static readonly Op[] _ops = new Op[256];
        public static Op Get(byte code) => _ops[code];

        private static void D(int code, string name, string operands = "", bool ends = false, bool branch = false)
        {
            _ops[code] = new Op() { Code = (byte)code, Name = name, Operands = operands, Ends = ends, Branch = branch };
        }

        static AbcOps()
        {
            D(0x01, "bkpt"); D(0x02, "nop"); D(0x03, "throw", "", true); D(0x04, "getsuper", "m"); D(0x05, "setsuper", "m");
            D(0x06, "dxns", "s"); D(0x07, "dxnslate"); D(0x08, "kill", "L"); D(0x09, "label");
            D(0x0C, "ifnlt", "o", false, true); D(0x0D, "ifnle", "o", false, true); D(0x0E, "ifngt", "o", false, true); D(0x0F, "ifnge", "o", false, true);
            D(0x10, "jump", "o", true, true); D(0x11, "iftrue", "o", false, true); D(0x12, "iffalse", "o", false, true);
            D(0x13, "ifeq", "o", false, true); D(0x14, "ifne", "o", false, true); D(0x15, "iflt", "o", false, true); D(0x16, "ifle", "o", false, true);
            D(0x17, "ifgt", "o", false, true); D(0x18, "ifge", "o", false, true); D(0x19, "ifstricteq", "o", false, true); D(0x1A, "ifstrictne", "o", false, true);
            D(0x1B, "lookupswitch", "W", true, true); D(0x1C, "pushwith"); D(0x1D, "popscope"); D(0x1E, "nextname"); D(0x1F, "hasnext");
            D(0x20, "pushnull"); D(0x21, "pushundefined"); D(0x23, "nextvalue"); D(0x24, "pushbyte", "B"); D(0x25, "pushshort", "u");
            D(0x26, "pushtrue"); D(0x27, "pushfalse"); D(0x28, "pushnan"); D(0x29, "pop"); D(0x2A, "dup"); D(0x2B, "swap");
            D(0x2C, "pushstring", "s"); D(0x2D, "pushint", "i"); D(0x2E, "pushuint", "I"); D(0x2F, "pushdouble", "d"); D(0x30, "pushscope");
            D(0x31, "pushnamespace", "n"); D(0x32, "hasnext2", "LL");
            D(0x35, "li8"); D(0x36, "li16"); D(0x37, "li32"); D(0x38, "lf32"); D(0x39, "lf64"); D(0x3A, "si8"); D(0x3B, "si16"); D(0x3C, "si32"); D(0x3D, "sf32"); D(0x3E, "sf64");
            D(0x40, "newfunction", "M"); D(0x41, "call", "a"); D(0x42, "construct", "a"); D(0x43, "callmethod", "ua"); D(0x44, "callstatic", "Ma");
            D(0x45, "callsuper", "ma"); D(0x46, "callproperty", "ma"); D(0x47, "returnvoid", "", true); D(0x48, "returnvalue", "", true);
            D(0x49, "constructsuper", "a"); D(0x4A, "constructprop", "ma"); D(0x4C, "callproplex", "ma"); D(0x4E, "callsupervoid", "ma");
            D(0x4F, "callpropvoid", "ma"); D(0x50, "sxi1"); D(0x51, "sxi8"); D(0x52, "sxi16"); D(0x53, "applytype", "a");
            D(0x55, "newobject", "a"); D(0x56, "newarray", "a"); D(0x57, "newactivation"); D(0x58, "newclass", "C"); D(0x59, "getdescendants", "m");
            D(0x5A, "newcatch", "x"); D(0x5D, "findpropstrict", "m"); D(0x5E, "findproperty", "m"); D(0x5F, "finddef", "m"); D(0x60, "getlex", "m");
            D(0x61, "setproperty", "m"); D(0x62, "getlocal", "L"); D(0x63, "setlocal", "L"); D(0x64, "getglobalscope"); D(0x65, "getscopeobject", "b");
            D(0x66, "getproperty", "m"); D(0x67, "getouterscope", "u"); D(0x68, "initproperty", "m"); D(0x6A, "deleteproperty", "m");
            D(0x6C, "getslot", "S"); D(0x6D, "setslot", "S"); D(0x6E, "getglobalslot", "S"); D(0x6F, "setglobalslot", "S");
            D(0x70, "convert_s"); D(0x71, "esc_xelem"); D(0x72, "esc_xattr"); D(0x73, "convert_i"); D(0x74, "convert_u"); D(0x75, "convert_d");
            D(0x76, "convert_b"); D(0x77, "convert_o"); D(0x78, "checkfilter");
            D(0x80, "coerce", "m"); D(0x81, "coerce_b"); D(0x82, "coerce_a"); D(0x83, "coerce_i"); D(0x84, "coerce_d"); D(0x85, "coerce_s");
            D(0x86, "astype", "m"); D(0x87, "astypelate"); D(0x88, "coerce_u"); D(0x89, "coerce_o");
            D(0x90, "negate"); D(0x91, "increment"); D(0x92, "inclocal", "L"); D(0x93, "decrement"); D(0x94, "declocal", "L"); D(0x95, "typeof");
            D(0x96, "not"); D(0x97, "bitnot");
            D(0xA0, "add"); D(0xA1, "subtract"); D(0xA2, "multiply"); D(0xA3, "divide"); D(0xA4, "modulo"); D(0xA5, "lshift"); D(0xA6, "rshift");
            D(0xA7, "urshift"); D(0xA8, "bitand"); D(0xA9, "bitor"); D(0xAA, "bitxor"); D(0xAB, "equals"); D(0xAC, "strictequals"); D(0xAD, "lessthan");
            D(0xAE, "lessequals"); D(0xAF, "greaterthan"); D(0xB0, "greaterequals"); D(0xB1, "instanceof"); D(0xB2, "istype", "m"); D(0xB3, "istypelate");
            D(0xB4, "in");
            D(0xC0, "increment_i"); D(0xC1, "decrement_i"); D(0xC2, "inclocal_i", "L"); D(0xC3, "declocal_i", "L"); D(0xC4, "negate_i");
            D(0xC5, "add_i"); D(0xC6, "subtract_i"); D(0xC7, "multiply_i");
            D(0xD0, "getlocal0"); D(0xD1, "getlocal1"); D(0xD2, "getlocal2"); D(0xD3, "getlocal3");
            D(0xD4, "setlocal0"); D(0xD5, "setlocal1"); D(0xD6, "setlocal2"); D(0xD7, "setlocal3");
            D(0xEF, "debug", "D"); D(0xF0, "debugline", "u"); D(0xF1, "debugfile", "s"); D(0xF2, "bkptline", "u"); D(0xF3, "timestamp");
        }
    }

    /// <summary>
    /// Writes AVM2 code with named labels (branch offsets are worked out at the end, relative to the end of the branch).
    /// </summary>
    public sealed class AbcCode
    {
        private readonly AbcFile _a;
        private readonly List<byte> _c = new List<byte>();
        private readonly Dictionary<string, int> _labels = new Dictionary<string, int>();
        private readonly List<KeyValuePair<int, string>> _fixups = new List<KeyValuePair<int, string>>();
        private readonly List<KeyValuePair<int, string>> _switchFixups = new List<KeyValuePair<int, string>>();
        private readonly Dictionary<int, int> _switchBase = new Dictionary<int, int>();

        public AbcCode(AbcFile a) { _a = a; }

        public AbcCode Op(int code) { _c.Add((byte)code); return this; }
        private void U30(int v) { uint u = (uint)v; do { byte x = (byte)(u & 0x7F); u >>= 7; if (u != 0) x |= 0x80; _c.Add(x); } while (u != 0); }

        /// <summary>A backward branch's target (the 'label' instruction marks it, as compilers do).</summary>
        public AbcCode Label(string name) { _labels[name] = _c.Count; return Op(0x09); }
        /// <summary>A forward branch's target.</summary>
        public AbcCode Target(string name) { _labels[name] = _c.Count; return this; }
        private AbcCode Branch(int op, string label) { Op(op); _fixups.Add(new KeyValuePair<int, string>(_c.Count, label)); _c.Add(0); _c.Add(0); _c.Add(0); return this; }
        public AbcCode Jump(string l) => Branch(0x10, l);
        public AbcCode IfTrue(string l) => Branch(0x11, l);
        public AbcCode IfFalse(string l) => Branch(0x12, l);
        public AbcCode IfEq(string l) => Branch(0x13, l);
        public AbcCode IfNe(string l) => Branch(0x14, l);
        public AbcCode IfLt(string l) => Branch(0x15, l);
        public AbcCode IfLe(string l) => Branch(0x16, l);
        public AbcCode IfGt(string l) => Branch(0x17, l);
        public AbcCode IfGe(string l) => Branch(0x18, l);
        public AbcCode IfNLt(string l) => Branch(0x0C, l);
        public AbcCode IfNLe(string l) => Branch(0x0D, l);
        public AbcCode IfNGt(string l) => Branch(0x0E, l);
        public AbcCode IfNGe(string l) => Branch(0x0F, l);
        public AbcCode IfStrictEq(string l) => Branch(0x19, l);
        public AbcCode IfStrictNe(string l) => Branch(0x1A, l);

        /// <summary>lookupswitch on the int on the stack: case i goes to cases[i], anything else to the default.</summary>
        public AbcCode Switch(string defaultLabel, params string[] cases)
        {
            int at = _c.Count;
            Op(0x1B);
            _switchBase[_c.Count] = at;
            _switchFixups.Add(new KeyValuePair<int, string>(_c.Count, defaultLabel)); _c.Add(0); _c.Add(0); _c.Add(0);
            U30(cases.Length - 1);
            foreach (string c in cases)
            {
                _switchBase[_c.Count] = at;
                _switchFixups.Add(new KeyValuePair<int, string>(_c.Count, c)); _c.Add(0); _c.Add(0); _c.Add(0);
            }
            return this;
        }

        public AbcCode GetLocal(int i) { if (i <= 3) return Op(0xD0 + i); Op(0x62); U30(i); return this; }
        public AbcCode SetLocal(int i) { if (i <= 3) return Op(0xD4 + i); Op(0x63); U30(i); return this; }
        public AbcCode This() => Op(0xD0);
        public AbcCode PushScope() => Op(0x30);
        public AbcCode PopScope() => Op(0x1D);
        public AbcCode GetScopeObject(int i) { Op(0x65); _c.Add((byte)i); return this; }
        public AbcCode Get(string name) { Op(0x66); U30(_a.Pub(name)); return this; }
        public AbcCode GetMn(int mn) { Op(0x66); U30(mn); return this; }
        public AbcCode Set(string name) { Op(0x61); U30(_a.Pub(name)); return this; }
        public AbcCode SetMn(int mn) { Op(0x61); U30(mn); return this; }
        public AbcCode InitProperty(int mn) { Op(0x68); U30(mn); return this; }
        public AbcCode Call(string name, int args) { Op(0x46); U30(_a.Pub(name)); U30(args); return this; }
        public AbcCode CallVoid(string name, int args) { Op(0x4F); U30(_a.Pub(name)); U30(args); return this; }
        public AbcCode CallMn(int mn, int args) { Op(0x46); U30(mn); U30(args); return this; }
        public AbcCode FindPropStrict(int mn) { Op(0x5D); U30(mn); return this; }
        public AbcCode ConstructProp(int mn, int args) { Op(0x4A); U30(mn); U30(args); return this; }
        /// <summary>new Class(args...): <see cref="NewBegin"/>, push the arguments, then <see cref="NewEnd"/>.</summary>
        public AbcCode NewBegin(int classMn) => FindPropStrict(classMn);
        public AbcCode NewEnd(int classMn, int args) => ConstructProp(classMn, args);
        public AbcCode ConstructSuper(int args) { Op(0x49); U30(args); return this; }
        public AbcCode GetLex(int mn) { Op(0x60); U30(mn); return this; }
        public AbcCode NewClass(int classIndex) { Op(0x58); U30(classIndex); return this; }
        public AbcCode NewArray(int count) { Op(0x56); U30(count); return this; }
        public AbcCode Str(string s) { Op(0x2C); U30(_a.String(s ?? "")); return this; }
        public AbcCode Int(int v)
        {
            if (v >= -128 && v <= 127) { Op(0x24); _c.Add((byte)(sbyte)v); return this; }
            Op(0x2D); U30(_a.Int(v)); return this;
        }
        public AbcCode Num(double v)
        {
            if (v == Math.Floor(v) && v >= -128 && v <= 127 && !(v == 0 && 1 / v < 0)) return Int((int)v);
            Op(0x2F); U30(_a.Double(v)); return this;
        }
        public AbcCode True() => Op(0x26);
        public AbcCode False() => Op(0x27);
        public AbcCode Null() => Op(0x20);
        public AbcCode Undefined() => Op(0x21);
        public AbcCode Dup() => Op(0x2A);
        public AbcCode Pop() => Op(0x29);
        public AbcCode Swap() => Op(0x2B);
        public AbcCode Add() => Op(0xA0);
        public AbcCode Sub() => Op(0xA1);
        public AbcCode Mul() => Op(0xA2);
        public AbcCode Div() => Op(0xA3);
        public AbcCode Mod() => Op(0xA4);
        public AbcCode Not() => Op(0x96);
        public AbcCode ToNumber() => Op(0x75);
        public AbcCode ToInt() => Op(0x73);
        public AbcCode ToStringOp() => Op(0x70);
        public AbcCode ToBool() => Op(0x76);
        public AbcCode CoerceAny() => Op(0x82);
        public AbcCode Coerce(int mn) { Op(0x80); U30(mn); return this; }
        public AbcCode LessThan() => Op(0xAD);
        public AbcCode LessEquals() => Op(0xAE);
        public AbcCode EqualsOp() => Op(0xAB);
        public AbcCode Bool(bool value) => Op(value ? 0x26 : 0x27);
        public AbcCode Ret() => Op(0x47);
        public AbcCode RetValue() => Op(0x48);

        public int Length => _c.Count;

        /// <summary>Exception handlers by label: code from 'from' up to 'to' (not included) goes to 'target' on any exception.</summary>
        public readonly List<KeyValuePair<string, string>> HandlerRanges = new List<KeyValuePair<string, string>>();
        public readonly List<string> HandlerTargets = new List<string>();
        public AbcCode Try(string from, string to, string target)
        {
            HandlerRanges.Add(new KeyValuePair<string, string>(from, to));
            HandlerTargets.Add(target);
            return this;
        }

        /// <summary>A label's offset in the code.</summary>
        public int OffsetOf(string label)
        {
            if (!_labels.TryGetValue(label, out int at)) throw new InvalidOperationException("no label " + label);
            return at;
        }

        /// <summary>The handlers as the method body stores them (catch anything, no variable).</summary>
        public List<AbcException> Handlers()
        {
            List<AbcException> handlers = new List<AbcException>();
            for (int i = 0; i < HandlerTargets.Count; i++)
                handlers.Add(new AbcException() { From = OffsetOf(HandlerRanges[i].Key), To = OffsetOf(HandlerRanges[i].Value), Target = OffsetOf(HandlerTargets[i]) });
            return handlers;
        }

        public byte[] ToArray()
        {
            byte[] code = _c.ToArray();
            foreach (KeyValuePair<int, string> f in _fixups)
            {
                if (!_labels.TryGetValue(f.Value, out int target)) throw new InvalidOperationException("no label " + f.Value);
                Put(code, f.Key, target - (f.Key + 3));
            }
            foreach (KeyValuePair<int, string> f in _switchFixups)
            {
                if (!_labels.TryGetValue(f.Value, out int target)) throw new InvalidOperationException("no label " + f.Value);
                Put(code, f.Key, target - _switchBase[f.Key]);
            }
            return code;
        }

        private static void Put(byte[] code, int at, int offset)
        {
            code[at] = (byte)offset; code[at + 1] = (byte)(offset >> 8); code[at + 2] = (byte)(offset >> 16);
        }
    }
}
