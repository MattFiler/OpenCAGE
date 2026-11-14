using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenCAGE.CrashTriage
{
    /// <summary>
    /// The parts of a Windows minidump (the .dmp Windows Error Reporting writes to its LocalDumps folder when a program
    /// crashes) that crash triage reads: the modules that were loaded (file, base address, size, PE time stamp), the
    /// exception (code, address, read / write and the address it touched) and the crashing thread's registers and stack
    /// memory. Read straight from the file, nothing written.
    /// </summary>
    internal sealed class MiniDump : IDisposable
    {
        public sealed class Module
        {
            /// <summary>The module's file as the process loaded it (full path).</summary>
            public string Path;
            public ulong Base;
            public uint Size;
            /// <summary>The PE header's TimeDateStamp: with <see cref="Size"/>, which build of a program it is.</summary>
            public uint TimeDateStamp;
            public uint CheckSum;

            public string FileName => System.IO.Path.GetFileName(Path ?? "");
            public bool Contains(ulong address) => address >= Base && address < Base + Size;
        }

        private sealed class Range
        {
            public ulong Start;
            public ulong Size;
            public long FileOffset;
        }

        private readonly FileStream _file;
        private readonly BinaryReader _reader;
        private readonly List<Range> _memory = new List<Range>();

        public string FilePath { get; }

        /// <summary>When the dump was written (its header's time stamp), UTC.</summary>
        public DateTime WrittenUtc { get; private set; }

        public List<Module> Modules { get; } = new List<Module>();

        /// <summary>PROCESSOR_ARCHITECTURE of the dumped process: 0 x86, 9 x64, 0xFFFF unknown (no system info stream).</summary>
        public ushort ProcessorArchitecture { get; private set; } = 0xFFFF;

        public bool HasException { get; private set; }
        public uint ExceptionThreadId { get; private set; }
        public uint ExceptionCode { get; private set; }
        public ulong ExceptionAddress { get; private set; }
        /// <summary>The exception's parameters: for an access violation [0] = 0 read, 1 write, 8 execute (DEP) and [1] = the address.</summary>
        public ulong[] ExceptionParameters { get; private set; } = new ulong[0];

        /// <summary>Whether the crashing thread's registers were read (<see cref="StackPointer"/> and the others).</summary>
        public bool HasContext { get; private set; }
        public bool Context64 { get; private set; }
        public ulong InstructionPointer { get; private set; }
        public ulong StackPointer { get; private set; }
        public ulong FramePointer { get; private set; }

        /// <summary>The crashing thread's stack memory as the dump holds it (start address, bytes), or null.</summary>
        public ulong StackStart { get; private set; }
        public byte[] Stack { get; private set; }

        private const uint Signature = 0x504D444D; //"MDMP"
        private const uint ThreadListStream = 3, ModuleListStream = 4, MemoryListStream = 5, ExceptionStream = 6, SystemInfoStream = 7, Memory64ListStream = 9;
        //The most of the crashing thread's stack read: plenty for the frames triage looks at
        private const int MaxStackBytes = 512 * 1024;

        /// <summary>Read a minidump. Throws <see cref="InvalidDataException"/> when the file is not one.</summary>
        public MiniDump(string path)
        {
            FilePath = path;
            _file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            try
            {
                _reader = new BinaryReader(_file);
                Read();
            }
            catch
            {
                _file.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            _file.Dispose();
        }

        /// <summary>The module an address is in, or null.</summary>
        public Module ModuleAt(ulong address)
        {
            foreach (Module module in Modules)
                if (module.Contains(address))
                    return module;
            return null;
        }

        /// <summary>The first module with this file name (any case), or null.</summary>
        public Module FindModule(string fileName)
        {
            foreach (Module module in Modules)
                if (string.Equals(module.FileName, fileName, StringComparison.OrdinalIgnoreCase))
                    return module;
            return null;
        }

        /// <summary>Bytes of the dumped process's memory at an address, or null when the dump does not hold all of them.</summary>
        public byte[] ReadMemory(ulong address, int count)
        {
            foreach (Range range in _memory)
            {
                if (address < range.Start || address + (ulong)count > range.Start + range.Size)
                    continue;
                _file.Position = range.FileOffset + (long)(address - range.Start);
                byte[] bytes = _reader.ReadBytes(count);
                return bytes.Length == count ? bytes : null;
            }
            return null;
        }

        private void Read()
        {
            if (_file.Length < 32)
                throw new InvalidDataException("too short to be a crash dump");
            _file.Position = 0;
            if (_reader.ReadUInt32() != Signature)
                throw new InvalidDataException("not a Windows crash dump (no MDMP signature)");
            _reader.ReadUInt32(); //version
            uint streams = _reader.ReadUInt32();
            uint directory = _reader.ReadUInt32();
            _reader.ReadUInt32(); //checksum
            uint stamp = _reader.ReadUInt32();
            WrittenUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(stamp);
            if (streams > 4096 || directory + (long)streams * 12 > _file.Length)
                throw new InvalidDataException("its stream directory is damaged");

            List<(uint type, uint size, uint rva)> found = new List<(uint, uint, uint)>();
            for (uint i = 0; i < streams; i++)
            {
                _file.Position = directory + i * 12;
                found.Add((_reader.ReadUInt32(), _reader.ReadUInt32(), _reader.ReadUInt32()));
            }
            //Memory first: the thread list's stacks and the exception's context are read against it
            foreach (var stream in found)
            {
                if (stream.type == MemoryListStream) ReadMemoryList(stream.rva);
                else if (stream.type == Memory64ListStream) ReadMemory64List(stream.rva);
            }
            foreach (var stream in found)
            {
                if (stream.type == ModuleListStream) ReadModules(stream.rva);
                else if (stream.type == SystemInfoStream) ReadSystemInfo(stream.rva);
            }
            foreach (var stream in found)
                if (stream.type == ExceptionStream) ReadException(stream.rva);
            foreach (var stream in found)
                if (stream.type == ThreadListStream) ReadThreads(stream.rva);
        }

        private bool InFile(long offset, long size) => offset >= 0 && size >= 0 && offset + size <= _file.Length;

        private void ReadMemoryList(uint rva)
        {
            _file.Position = rva;
            uint count = _reader.ReadUInt32();
            for (uint i = 0; i < count && i < 1000000; i++)
            {
                _file.Position = rva + 4 + i * 16;
                ulong start = _reader.ReadUInt64();
                uint size = _reader.ReadUInt32();
                uint at = _reader.ReadUInt32();
                if (InFile(at, size))
                    _memory.Add(new Range() { Start = start, Size = size, FileOffset = at });
            }
        }

        private void ReadMemory64List(uint rva)
        {
            _file.Position = rva;
            ulong count = _reader.ReadUInt64();
            long at = (long)_reader.ReadUInt64();
            for (ulong i = 0; i < count && i < 10000000; i++)
            {
                _file.Position = rva + 16 + (long)i * 16;
                ulong start = _reader.ReadUInt64();
                ulong size = _reader.ReadUInt64();
                if (InFile(at, (long)size))
                    _memory.Add(new Range() { Start = start, Size = size, FileOffset = at });
                at += (long)size;
            }
        }

        private void ReadModules(uint rva)
        {
            _file.Position = rva;
            uint count = _reader.ReadUInt32();
            for (uint i = 0; i < count && i < 100000; i++)
            {
                //MINIDUMP_MODULE: 108 bytes
                _file.Position = rva + 4 + i * 108;
                Module module = new Module();
                module.Base = _reader.ReadUInt64();
                module.Size = _reader.ReadUInt32();
                module.CheckSum = _reader.ReadUInt32();
                module.TimeDateStamp = _reader.ReadUInt32();
                uint name = _reader.ReadUInt32();
                module.Path = ReadString(name);
                Modules.Add(module);
            }
        }

        private void ReadSystemInfo(uint rva)
        {
            _file.Position = rva;
            ProcessorArchitecture = _reader.ReadUInt16();
        }

        private void ReadException(uint rva)
        {
            //MINIDUMP_EXCEPTION_STREAM: thread id, alignment, MINIDUMP_EXCEPTION (152 bytes), the thread's context
            _file.Position = rva;
            ExceptionThreadId = _reader.ReadUInt32();
            _reader.ReadUInt32();
            ExceptionCode = _reader.ReadUInt32();
            _reader.ReadUInt32(); //flags
            _reader.ReadUInt64(); //nested record
            ExceptionAddress = _reader.ReadUInt64();
            uint parameters = Math.Min(_reader.ReadUInt32(), 15u);
            _reader.ReadUInt32();
            ExceptionParameters = new ulong[parameters];
            for (int i = 0; i < 15; i++)
            {
                ulong value = _reader.ReadUInt64();
                if (i < parameters) ExceptionParameters[i] = value;
            }
            uint contextSize = _reader.ReadUInt32();
            uint contextAt = _reader.ReadUInt32();
            HasException = true;
            ReadContext(contextAt, contextSize);
        }

        /* An x86 CONTEXT is 0x2CC bytes (Ebp 0xB4, Eip 0xB8, Esp 0xC4); an x64 one 0x4D0 (Rsp 0x98, Rbp 0xA0, Rip 0xF8) */
        private void ReadContext(uint at, uint size)
        {
            if (!InFile(at, size))
                return;
            if (size >= 0x2CC && size < 0x4D0)
            {
                _file.Position = at + 0xB4;
                FramePointer = _reader.ReadUInt32();
                InstructionPointer = _reader.ReadUInt32();
                _file.Position = at + 0xC4;
                StackPointer = _reader.ReadUInt32();
                HasContext = true;
            }
            else if (size >= 0x4D0)
            {
                _file.Position = at + 0x98;
                StackPointer = _reader.ReadUInt64();
                FramePointer = _reader.ReadUInt64();
                _file.Position = at + 0xF8;
                InstructionPointer = _reader.ReadUInt64();
                Context64 = true;
                HasContext = true;
            }
        }

        private void ReadThreads(uint rva)
        {
            if (!HasException)
                return;
            _file.Position = rva;
            uint count = _reader.ReadUInt32();
            for (uint i = 0; i < count && i < 100000; i++)
            {
                //MINIDUMP_THREAD: 48 bytes (id, suspend count, priority class, priority, TEB, stack, context)
                _file.Position = rva + 4 + i * 48;
                uint id = _reader.ReadUInt32();
                if (id != ExceptionThreadId)
                    continue;
                _file.Position = rva + 4 + i * 48 + 24;
                ulong start = _reader.ReadUInt64();
                uint size = _reader.ReadUInt32();
                uint at = _reader.ReadUInt32();
                if (!InFile(at, size))
                    return;
                //From the stack pointer up (the stack grows down): the crashing frame and its callers
                ulong from = start;
                if (HasContext && StackPointer >= start && StackPointer < start + size)
                    from = StackPointer;
                int length = (int)Math.Min((ulong)MaxStackBytes, start + size - from);
                _file.Position = at + (long)(from - start);
                StackStart = from;
                Stack = _reader.ReadBytes(length);
                return;
            }
        }

        private string ReadString(uint rva)
        {
            if (!InFile(rva, 4))
                return null;
            _file.Position = rva;
            uint bytes = _reader.ReadUInt32();
            if (bytes > 65536 || !InFile(rva + 4, bytes))
                return null;
            return Encoding.Unicode.GetString(_reader.ReadBytes((int)bytes));
        }
    }
}
