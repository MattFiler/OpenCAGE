using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenCAGE.ShaderTools
{
    /// <summary>
    /// The Windows HLSL compiler, for reading and building shader bytecode. Disassembly uses d3dcompiler_47, which every
    /// Windows 10 and 11 install has. Compiling prefers d3dcompiler_43 (the June 2010 compiler every one of the game's own
    /// shaders was built with, installed with the DirectX runtime), so a shader rebuilt from its source comes out as close
    /// to the original as it can; without it, d3dcompiler_47 is used, which the game also runs happily.
    /// </summary>
    public static class D3DCompiler
    {
        [ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ID3DBlob
        {
            [PreserveSig] IntPtr GetBufferPointer();
            [PreserveSig] IntPtr GetBufferSize();
        }

        [DllImport("d3dcompiler_47.dll", EntryPoint = "D3DDisassemble")]
        private static extern int Disassemble47(byte[] src, IntPtr size, uint flags, string comments, out ID3DBlob text);

        [DllImport("d3dcompiler_47.dll", EntryPoint = "D3DCompile", CharSet = CharSet.Ansi)]
        private static extern int Compile47(byte[] src, IntPtr size, string name, IntPtr defines, IntPtr include, string entry, string target, uint flags1, uint flags2, out ID3DBlob code, out ID3DBlob errors);

        [DllImport("d3dcompiler_43.dll", EntryPoint = "D3DCompile", CharSet = CharSet.Ansi)]
        private static extern int Compile43(byte[] src, IntPtr size, string name, IntPtr defines, IntPtr include, string entry, string target, uint flags1, uint flags2, out ID3DBlob code, out ID3DBlob errors);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string name);

        /// <summary>Literals in the disassembly as their exact bits (0x3f800000), not rounded decimals.</summary>
        public const uint DISASM_PRINT_HEX_LITERALS = 0x80;

        private static bool? _has43, _has47;

        /// <summary>The game's own compiler (d3dcompiler_43) is installed.</summary>
        public static bool HasOriginalCompiler { get { if (_has43 == null) _has43 = LoadLibrary("d3dcompiler_43.dll") != IntPtr.Zero; return _has43.Value; } }

        /// <summary>A compiler of any version is installed (d3dcompiler_47 ships with Windows 10 and 11).</summary>
        public static bool Available { get { if (_has47 == null) _has47 = LoadLibrary("d3dcompiler_47.dll") != IntPtr.Zero; return _has47.Value || HasOriginalCompiler; } }

        /// <summary>The bytecode as assembly text, or null if it isn't valid shader bytecode (or no compiler is installed).</summary>
        public static string Disassemble(byte[] bytecode, uint flags = 0)
        {
            if (bytecode == null || bytecode.Length == 0)
                return null;
            try
            {
                if (Disassemble47(bytecode, (IntPtr)bytecode.Length, flags, null, out ID3DBlob text) != 0 || text == null)
                    return null;
                return Encoding.ASCII.GetString(Read(text)).TrimEnd('\0');
            }
            catch (DllNotFoundException) { return null; }
            catch (EntryPointNotFoundException) { return null; }
        }

        /// <summary>
        /// Compile HLSL for a target such as "ps_5_0". Returns the bytecode, or null with the compiler's errors. Warnings
        /// from a successful compile come back in <paramref name="messages"/> too.
        /// </summary>
        public static byte[] Compile(string hlsl, string entry, string target, out string messages, bool preferOriginalCompiler = true)
        {
            messages = null;
            byte[] source = Encoding.ASCII.GetBytes(hlsl ?? "");
            bool use43 = preferOriginalCompiler && HasOriginalCompiler;
            int hr;
            ID3DBlob code, errors;
            try
            {
                hr = use43
                    ? Compile43(source, (IntPtr)source.Length, "shader.hlsl", IntPtr.Zero, IntPtr.Zero, entry, target, 0, 0, out code, out errors)
                    : Compile47(source, (IntPtr)source.Length, "shader.hlsl", IntPtr.Zero, IntPtr.Zero, entry, target, 0, 0, out code, out errors);
            }
            catch (DllNotFoundException)
            {
                messages = "No HLSL compiler (d3dcompiler_43.dll or d3dcompiler_47.dll) was found on this machine.";
                return null;
            }
            if (errors != null)
                messages = Encoding.ASCII.GetString(Read(errors)).TrimEnd('\0', '\r', '\n');
            if (hr != 0 || code == null)
            {
                if (string.IsNullOrEmpty(messages))
                    messages = "The compiler failed (0x" + hr.ToString("X8") + ").";
                return null;
            }
            return Read(code);
        }

        /// <summary>Which compiler <see cref="Compile"/> uses.</summary>
        public static string CompilerName(bool preferOriginalCompiler = true)
        {
            return preferOriginalCompiler && HasOriginalCompiler ? "d3dcompiler_43 (the game's own)" : "d3dcompiler_47";
        }

        private static byte[] Read(ID3DBlob blob)
        {
            try
            {
                int length = (int)blob.GetBufferSize();
                byte[] bytes = new byte[length];
                Marshal.Copy(blob.GetBufferPointer(), bytes, 0, length);
                return bytes;
            }
            finally
            {
                Marshal.ReleaseComObject(blob);
            }
        }
    }
}
