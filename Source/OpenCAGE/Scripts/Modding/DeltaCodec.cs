using System;
using System.Collections.Generic;
using System.IO;

namespace OpenCAGE.Modding
{
    /* Binary patches against known source bytes, for the files a level save rewrites without really
     * changing: measured on a no-edit save, the 72MB texture pak is 99.98% recoverable from vanilla
     * and the 27MB models pak 96.7%, so a patch carries kilobytes where a copy carries the file.
     *
     * The format (ACDELTA1) is a plain op list - COPY runs from the source, LITERAL runs of new
     * bytes - found rsync-style: a rolling hash slides the target over a table of aligned source
     * blocks, and each confirmed match is extended byte-by-byte in both directions. A patch only
     * applies to bytes with the recorded source hash and only counts if the result matches the
     * recorded target hash, so applying either reproduces the exact bytes or fails cleanly. */
    public static class DeltaCodec
    {
        private const int BLOCK = 4096;
        private static readonly byte[] MAGIC = { (byte)'A', (byte)'C', (byte)'D', (byte)'E', (byte)'L', (byte)'T', (byte)'A', (byte)'1' };

        /// <summary>
        /// Encode target as a patch against source. Returns null when the patch wouldn't be worth
        /// shipping (larger than maxSize, if given) - callers then ship the whole file instead.
        /// </summary>
        public static byte[] Encode(byte[] source, byte[] target, long maxSize = long.MaxValue)
        {
            List<long[]> ops = FindOps(source, target); //{0, srcOffset, length} copy, {1, tgtOffset, length} literal

            long size = 8 + 4 + (32 + 8) * 2 + 4;
            foreach (long[] op in ops)
                size += op[0] == 0 ? 1 + 8 + 4 : 1 + 4 + op[2];
            if (size > maxSize)
                return null;

            using (MemoryStream stream = new MemoryStream((int)size))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(MAGIC);
                writer.Write((Int32)1);
                writer.Write(ModToolkit.Sha256(source));
                writer.Write((Int64)source.Length);
                writer.Write(ModToolkit.Sha256(target));
                writer.Write((Int64)target.Length);
                writer.Write(ops.Count);
                foreach (long[] op in ops)
                {
                    writer.Write((byte)op[0]);
                    if (op[0] == 0)
                    {
                        writer.Write((Int64)op[1]);
                        writer.Write((Int32)op[2]);
                    }
                    else
                    {
                        writer.Write((Int32)op[2]);
                        writer.Write(target, (int)op[1], (int)op[2]);
                    }
                }
                return stream.ToArray();
            }
        }

        /// <summary>
        /// The source hash a patch expects, so callers can check applicability without applying.
        /// </summary>
        public static byte[] ReadSourceSha(byte[] delta)
        {
            byte[] sha = new byte[32];
            Array.Copy(delta, 12, sha, 0, 32);
            return sha;
        }

        /// <summary>
        /// Apply a patch. Throws when the source bytes aren't the ones the patch was made against,
        /// or the result doesn't hash to what was recorded - never returns wrong bytes.
        /// </summary>
        public static byte[] Apply(byte[] delta, byte[] source)
        {
            using (MemoryStream stream = new MemoryStream(delta))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                byte[] magic = reader.ReadBytes(8);
                for (int i = 0; i < 8; i++)
                    if (magic[i] != MAGIC[i])
                        throw new Exception("Not an ACDELTA1 patch.");
                int version = reader.ReadInt32();
                if (version != 1)
                    throw new Exception("Unsupported patch version " + version + ".");

                byte[] sourceSha = reader.ReadBytes(32);
                long sourceLength = reader.ReadInt64();
                if (source.Length != sourceLength || !HashEquals(ModToolkit.Sha256(source), sourceSha))
                    throw new Exception("The patch was made against different source bytes.");

                byte[] targetSha = reader.ReadBytes(32);
                long targetLength = reader.ReadInt64();
                byte[] target = new byte[targetLength];
                long at = 0;

                int opCount = reader.ReadInt32();
                for (int i = 0; i < opCount; i++)
                {
                    byte kind = reader.ReadByte();
                    if (kind == 0)
                    {
                        long srcOffset = reader.ReadInt64();
                        int length = reader.ReadInt32();
                        Array.Copy(source, srcOffset, target, at, length);
                        at += length;
                    }
                    else
                    {
                        int length = reader.ReadInt32();
                        reader.Read(target, (int)at, length);
                        at += length;
                    }
                }

                if (at != targetLength || !HashEquals(ModToolkit.Sha256(target), targetSha))
                    throw new Exception("The patch did not reproduce the recorded result.");
                return target;
            }
        }

        private static bool HashEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /* The rsync trick: weak-hash every aligned source block once, then slide a rolling hash
         * over the target. Weak hits are confirmed by direct comparison and grown outward - block
         * alignment finds the match, the extension recovers everything around it. */
        private static List<long[]> FindOps(byte[] source, byte[] target)
        {
            List<long[]> ops = new List<long[]>();
            long literalStart = 0;

            if (source.Length >= BLOCK && target.Length >= BLOCK)
            {
                Dictionary<uint, List<int>> table = new Dictionary<uint, List<int>>();
                for (int at = 0; at + BLOCK <= source.Length; at += BLOCK)
                {
                    uint weak = Adler(source, at);
                    List<int> list;
                    if (!table.TryGetValue(weak, out list))
                        table[weak] = list = new List<int>(2);
                    if (list.Count < 8)
                        list.Add(at);
                }

                long pos = 0;
                uint rolling = Adler(target, 0);
                ushort s1 = (ushort)(rolling & 0xffff), s2 = (ushort)(rolling >> 16);
                while (pos + BLOCK <= target.Length)
                {
                    List<int> candidates;
                    int match = -1;
                    if (table.TryGetValue((uint)(s1 | (s2 << 16)), out candidates))
                    {
                        foreach (int candidate in candidates)
                        {
                            if (BlockEquals(source, candidate, target, (int)pos))
                            {
                                match = candidate;
                                break;
                            }
                        }
                    }

                    if (match >= 0)
                    {
                        long srcStart = match, tgtStart = pos;
                        long srcEnd = match + BLOCK, tgtEnd = pos + BLOCK;
                        //Grow backward into the pending literal, and forward into unexamined bytes
                        while (srcStart > 0 && tgtStart > literalStart && source[srcStart - 1] == target[tgtStart - 1])
                        {
                            srcStart--;
                            tgtStart--;
                        }
                        while (srcEnd < source.Length && tgtEnd < target.Length && source[srcEnd] == target[tgtEnd])
                        {
                            srcEnd++;
                            tgtEnd++;
                        }

                        if (tgtStart > literalStart)
                            ops.Add(new long[] { 1, literalStart, tgtStart - literalStart });

                        //Merge with a previous copy this one continues
                        long[] previous = ops.Count == 0 ? null : ops[ops.Count - 1];
                        if (previous != null && previous[0] == 0 && tgtStart == literalStart
                            && previous[1] + previous[2] == srcStart)
                            previous[2] += tgtEnd - tgtStart;
                        else
                            ops.Add(new long[] { 0, srcStart, tgtEnd - tgtStart });

                        literalStart = tgtEnd;
                        pos = tgtEnd;
                        if (pos + BLOCK > target.Length)
                            break;
                        rolling = Adler(target, (int)pos);
                        s1 = (ushort)(rolling & 0xffff);
                        s2 = (ushort)(rolling >> 16);
                    }
                    else
                    {
                        if (pos + BLOCK >= target.Length)
                            break;
                        s1 = (ushort)(s1 - target[pos] + target[pos + BLOCK]);
                        s2 = (ushort)(s2 - BLOCK * target[pos] + s1);
                        pos++;
                    }
                }
            }

            if (literalStart < target.Length)
                ops.Add(new long[] { 1, literalStart, target.Length - literalStart });
            return ops;
        }

        private static bool BlockEquals(byte[] source, int sourceAt, byte[] target, int targetAt)
        {
            for (int i = 0; i < BLOCK; i++)
                if (source[sourceAt + i] != target[targetAt + i])
                    return false;
            return true;
        }

        private static uint Adler(byte[] data, int at)
        {
            ushort s1 = 0, s2 = 0;
            for (int i = 0; i < BLOCK; i++)
            {
                s1 = (ushort)(s1 + data[at + i]);
                s2 = (ushort)(s2 + s1);
            }
            return (uint)(s1 | (s2 << 16));
        }
    }
}
