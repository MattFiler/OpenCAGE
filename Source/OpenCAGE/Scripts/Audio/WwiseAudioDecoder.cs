
using System;
using System.IO;
using System.Threading;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Decoded audio, filled in as it decodes.
    ///
    /// Most of the game's sounds are a couple of seconds long and decode faster than they can be
    /// started, but the ambient beds run to several minutes, and waiting for one of those to decode in
    /// full before the first sample plays is a noticeable stall. The buffer is therefore sized up front
    /// from the sample count in the file's header - which is known before decoding starts, so it never
    /// has to grow - and filled by a background thread while the player reads along behind it.
    ///
    /// <see cref="Length"/> is the publication point: the decoder writes samples and only then advances
    /// it, and readers never look past it, so no lock is needed on the audio itself.
    /// </summary>
    public sealed class DecodedAudio : IDisposable
    {
        /// <summary>
        /// How much of a sound to decode. Six of the game's sounds are longer than this; they are
        /// ambient loops where the opening minutes are more than enough to tell what you are listening
        /// to, and decoding one in full would cost well over a hundred megabytes.
        /// </summary>
        public const int MaxPreviewSeconds = 300;

        private readonly byte[] _buffer;
        private int _length;
        private volatile bool _complete;
        private volatile bool _disposed;
        private volatile string _error;

        public int Channels { get; private set; }
        public int SampleRate { get; private set; }

        /// <summary>How many channels the sound has before any downmix.</summary>
        public int SourceChannels { get; private set; }

        /// <summary>True when the sound is longer than the preview limit and has been cut short.</summary>
        public bool Truncated { get; private set; }

        internal DecodedAudio(VorbisReader reader, int expectedFrames, bool truncated)
        {
            SourceChannels = reader.Channels;
            Channels = reader.Channels > 2 ? 2 : reader.Channels;
            SampleRate = reader.SampleRate;
            Truncated = truncated;

            _buffer = new byte[(long)expectedFrames * Channels * 2 > int.MaxValue
                ? int.MaxValue
                : expectedFrames * Channels * 2];

            Thread thread = new Thread(() => Fill(reader)) { IsBackground = true, Name = "Sound decode" };
            thread.Start();
        }

        /// <summary>Bytes decoded so far.</summary>
        public int Length
        {
            get { return Volatile.Read(ref _length); }
        }

        /// <summary>The whole sound, or as much of it as the preview limit allows.</summary>
        public int Capacity
        {
            get { return _buffer.Length; }
        }

        public bool Complete
        {
            get { return _complete; }
        }

        /// <summary>Set if decoding stopped early because the stream was damaged.</summary>
        public string Error
        {
            get { return _error; }
        }

        public int BytesPerSecond
        {
            get { return SampleRate * Channels * 2; }
        }

        /// <summary>How long the sound will be once it has finished decoding.</summary>
        public TimeSpan Duration
        {
            get
            {
                if (BytesPerSecond == 0)
                    return TimeSpan.Zero;

                //While decoding, the length from the header is the honest answer; once finished, what
                //was actually produced is, in case the stream ran short
                return TimeSpan.FromSeconds((double)(Complete ? Length : Capacity) / BytesPerSecond);
            }
        }

        /// <summary>How much of it can be played right now.</summary>
        public TimeSpan Decoded
        {
            get { return BytesPerSecond == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)Length / BytesPerSecond); }
        }

        /// <summary>Copy out of the decoded region. Never reads past <see cref="Length"/>.</summary>
        public int Read(int offset, byte[] destination, int destinationOffset, int count)
        {
            int available = Length - offset;
            if (offset < 0 || available <= 0)
                return 0;

            int taken = Math.Min(count, available);
            Buffer.BlockCopy(_buffer, offset, destination, destinationOffset, taken);
            return taken;
        }

        /// <summary>Block until the whole sound is decoded, for saving it out.</summary>
        public void WaitForCompletion()
        {
            while (!_complete && !_disposed)
                Thread.Sleep(10);
        }

        private void Fill(VorbisReader reader)
        {
            try
            {
                float[] samples = new float[SourceChannels * 4096];
                byte[] converted = new byte[Channels * 4096 * 2];

                while (!_disposed)
                {
                    int read = reader.ReadSamples(samples, 0, samples.Length);
                    if (read <= 0)
                        break;

                    int frames = read / SourceChannels;
                    int produced = Mix(samples, frames, SourceChannels, Channels, converted);

                    int position = _length;
                    int room = _buffer.Length - position;
                    if (room <= 0)
                        break;

                    if (produced > room)
                        produced = room;

                    Buffer.BlockCopy(converted, 0, _buffer, position, produced);

                    //Only now is the audio safe for the player to read
                    Volatile.Write(ref _length, position + produced);
                }
            }
            catch (Exception e)
            {
                //Whatever decoded before the fault is still worth playing
                _error = e.Message;
            }
            finally
            {
                _complete = true;

                try
                {
                    reader.Dispose();
                }
                catch
                {
                }
            }
        }

        /// <summary>Wrap the decoded samples in a RIFF header, for saving out.</summary>
        public byte[] ToWave()
        {
            WaitForCompletion();

            int length = Length;
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + length);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1); //PCM
                writer.Write((short)Channels);
                writer.Write(SampleRate);
                writer.Write(BytesPerSecond);
                writer.Write((short)(Channels * 2));
                writer.Write((short)16);
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(length);
                writer.Write(_buffer, 0, length);

                writer.Flush();
                return stream.ToArray();
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }

        //Downmix coefficients for 5.1. The centre and the surrounds each fold in at the conventional
        //-3dB; the low frequency channel goes in quieter still, since it is mostly redundant with the
        //bass already present in the full range channels.
        private const float CentreGain = 0.707f;
        private const float SurroundGain = 0.707f;
        private const float LowFrequencyGain = 0.5f;

        /// <summary>
        /// What a fully correlated input would sum to. Dividing by it makes clipping arithmetically
        /// impossible, and it costs nothing in practice: the game's surround content is close to
        /// correlated across channels, so it lands just under full scale rather than quiet. Scaling by
        /// anything larger is what produced the harsh distortion on loud 5.1 sounds - measured across
        /// the game, 56 of 60 surround sounds clipped.
        /// </summary>
        private static readonly float SurroundScale = 1f / (1f + CentreGain + SurroundGain + LowFrequencyGain);

        private static int Mix(float[] samples, int frames, int sourceChannels, int outputChannels, byte[] output)
        {
            int position = 0;

            for (int frame = 0; frame < frames; frame++)
            {
                int offset = frame * sourceChannels;

                if (sourceChannels <= 2)
                {
                    //Mono and stereo already peak at full scale and need no attenuation
                    for (int channel = 0; channel < outputChannels; channel++)
                        position = WriteSample(output, position, samples[offset + channel]);

                    continue;
                }

                float left, right;
                if (sourceChannels == 6)
                {
                    //Vorbis orders six channels as left, centre, right, surround left, surround right,
                    //low frequency
                    left = samples[offset]
                        + CentreGain * samples[offset + 1]
                        + SurroundGain * samples[offset + 3]
                        + LowFrequencyGain * samples[offset + 5];

                    right = samples[offset + 2]
                        + CentreGain * samples[offset + 1]
                        + SurroundGain * samples[offset + 4]
                        + LowFrequencyGain * samples[offset + 5];

                    left *= SurroundScale;
                    right *= SurroundScale;
                }
                else
                {
                    //An unusual channel count - keep everything audible rather than guess at a layout.
                    //Dividing by the channel count is the same guarantee as above.
                    left = 0;
                    right = 0;
                    for (int channel = 0; channel < sourceChannels; channel++)
                    {
                        float value = samples[offset + channel] / sourceChannels;
                        if ((channel & 1) == 0)
                            left += value;
                        else
                            right += value;
                    }

                    //Each side only received half the channels, so it can only reach half of full scale
                    left *= 2f;
                    right *= 2f;
                }

                position = WriteSample(output, position, left);
                position = WriteSample(output, position, right);
            }

            return position;
        }

        private static int WriteSample(byte[] output, int position, float value)
        {
            if (value > 1f) value = 1f;
            else if (value < -1f) value = -1f;

            short sample = (short)(value * short.MaxValue);
            output[position] = (byte)sample;
            output[position + 1] = (byte)(sample >> 8);
            return position + 2;
        }
    }

    /// <summary>
    /// Turns the bytes of a .wem into samples.
    ///
    /// Anything with more than two channels is folded down to stereo. The game's surround sounds are
    /// mixed for a 5.1 room, and playing one back through a stereo device without a downmix drops the
    /// centre channel - which for dialogue and most one-shots is where nearly all of the sound is.
    /// </summary>
    public static class WwiseAudioDecoder
    {
        /// <summary>
        /// Rebuild a sound as Ogg and start decoding it. Returns as soon as the stream is open, with
        /// the samples arriving behind it.
        /// </summary>
        public static DecodedAudio Decode(byte[] wem)
        {
            byte[] ogg = WwiseVorbisConverter.ToOgg(wem);
            VorbisReader reader = new VorbisReader(ogg);

            long frames = reader.TotalSamples;
            if (frames <= 0)
                frames = reader.SampleRate; //A stream with no length in it - decode a second and see

            long limit = (long)DecodedAudio.MaxPreviewSeconds * reader.SampleRate;
            bool truncated = frames > limit;
            if (truncated)
                frames = limit;

            return new DecodedAudio(reader, (int)frames, truncated);
        }
    }
}
