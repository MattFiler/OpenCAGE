using System;
using System.Collections.Generic;
using System.IO;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Wraps packets in Ogg pages.
    ///
    /// One packet per page, which is a little wasteful of header bytes but makes the granule position
    /// on every page exact by construction - the position is defined as that of the last packet to
    /// finish on the page, and with one packet there is nothing to get wrong.
    /// </summary>
    internal sealed class OggWriter
    {
        private const int MaxSegments = 255;

        private readonly Stream _output;
        private readonly uint _serial;
        private uint _page;

        public OggWriter(Stream output, uint serial)
        {
            _output = output;
            _serial = serial;
        }

        public void WritePacket(byte[] packet, long granulePosition, bool beginStream, bool endStream)
        {
            //A packet longer than one page's worth of lacing has to be split, with the continuation
            //flagged so the decoder joins them back up
            int offset = 0;
            bool first = true;

            while (true)
            {
                int remaining = packet.Length - offset;
                int segments = remaining / 255 + 1;
                bool last = segments <= MaxSegments;
                if (!last)
                    segments = MaxSegments;

                int payload = last ? remaining : segments * 255;

                byte headerType = 0;
                if (!first) headerType |= 0x01;
                if (beginStream && first) headerType |= 0x02;
                if (endStream && last) headerType |= 0x04;

                WritePage(packet, offset, payload, segments, headerType, last ? granulePosition : -1);

                offset += payload;
                first = false;

                if (last)
                    break;
            }
        }

        private void WritePage(byte[] packet, int offset, int payload, int segments, byte headerType, long granulePosition)
        {
            byte[] page = new byte[27 + segments + payload];

            page[0] = (byte)'O';
            page[1] = (byte)'g';
            page[2] = (byte)'g';
            page[3] = (byte)'S';
            page[4] = 0; //stream structure version
            page[5] = headerType;

            WriteInt64(page, 6, granulePosition);
            WriteInt32(page, 14, _serial);
            WriteInt32(page, 18, _page++);
            //22..25 is the checksum, left zero while it is calculated
            page[26] = (byte)segments;

            int written = payload;
            for (int i = 0; i < segments; i++)
            {
                int lacing = written >= 255 ? 255 : written;
                page[27 + i] = (byte)lacing;
                written -= lacing;
            }

            Array.Copy(packet, offset, page, 27 + segments, payload);
            WriteInt32(page, 22, Checksum(page));

            _output.Write(page, 0, page.Length);
        }

        private static void WriteInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteInt64(byte[] buffer, int offset, long value)
        {
            for (int i = 0; i < 8; i++)
                buffer[offset + i] = (byte)(value >> (i * 8));
        }

        private static readonly uint[] _crcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            //Ogg uses a plain CRC-32 with polynomial 0x04C11DB7 - no reflection and no final inversion,
            //which is why the usual zlib table can't be borrowed
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint value = i << 24;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 0x80000000) != 0 ? (value << 1) ^ 0x04C11DB7 : value << 1;

                table[i] = value;
            }

            return table;
        }

        private static uint Checksum(byte[] page)
        {
            uint crc = 0;
            for (int i = 0; i < page.Length; i++)
                crc = (crc << 8) ^ _crcTable[((crc >> 24) & 0xFF) ^ page[i]];

            return crc;
        }
    }
}
