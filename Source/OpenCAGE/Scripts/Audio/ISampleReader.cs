using System;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// A decoded audio source: interleaved float frames pulled until the stream ends.
    ///
    /// This is the seam between the codecs and the preview pipeline. The PC builds only ever need
    /// Vorbis, but the Switch build stores some of its audio as Wwise ADPCM and a little as plain
    /// PCM, and everything downstream of decoding - the downmix, the streaming buffer, the player -
    /// is identical for all three.
    /// </summary>
    internal interface ISampleReader : IDisposable
    {
        int Channels { get; }
        int SampleRate { get; }

        /// <summary>Frames in the stream, or 0 if it doesn't say.</summary>
        long TotalSamples { get; }

        /// <summary>
        /// Fill <paramref name="buffer"/> with interleaved samples. Returns the number of floats
        /// written, always a whole number of frames, and 0 at the end of the stream.
        /// </summary>
        int ReadSamples(float[] buffer, int offset, int count);
    }
}
