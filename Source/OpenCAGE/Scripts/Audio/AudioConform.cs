using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Lays audio read from a .wav out the way a replacement needs it: the channel count and sample rate the sound it
    /// replaces was shipped with (or that the user asked for), and never one the game's Vorbis decoder isn't set up for.
    ///
    /// The game's own audio is mono or stereo (and 5.1 beds, which the encoder can't write: their coupled codebooks aren't
    /// in the table the game ships) at 24, 32, 36, 44.1 or 48 kHz. The encoder lands on a setup the game knows for mono
    /// from 16 kHz up and for stereo from 32 kHz up - measured over every common rate at every quality - so anything
    /// slower is resampled up to the nearest rate that works, and anything above 48 kHz down to 48 kHz, which is the
    /// highest the game ships.
    /// </summary>
    internal static class AudioConform
    {
        /// <summary>The highest sample rate the game ships audio at.</summary>
        public const int HighestRate = 48000;

        //Tried from the lowest up when a rate has to change: the first one at or above the wanted rate that the
        //encoder can do for the channel count is used
        private static readonly int[] StandardRates = { 16000, 22050, 24000, 32000, 44100, 48000 };

        private static readonly Dictionary<long, bool> _encodable = new Dictionary<long, bool>();

        /// <summary>Whether the encoder can write audio of this layout in a form the game decodes. Asked once per layout.</summary>
        public static bool CanEncode(int channels, int sampleRate)
        {
            long key = ((long)channels << 32) | (uint)sampleRate;
            lock (_encodable)
            {
                bool known;
                if (_encodable.TryGetValue(key, out known))
                    return known;
            }

            bool can = VorbisEncoder.CanEncode(channels, sampleRate);
            lock (_encodable)
                _encodable[key] = can;
            return can;
        }

        /// <summary>
        /// The sample rate to encode at, given the one wanted: that rate when the encoder can do it, else the nearest
        /// standard rate above it that it can - capped at <see cref="HighestRate"/>.
        /// </summary>
        public static int EncodableRate(int channels, int wanted)
        {
            if (wanted > HighestRate)
                wanted = HighestRate;
            if (wanted >= 8000 && CanEncode(channels, wanted))
                return wanted;

            foreach (int rate in StandardRates)
                if (rate >= wanted && CanEncode(channels, rate))
                    return rate;

            return HighestRate;
        }

        #region CHANNELS

        /// <summary>
        /// The audio mixed to <paramref name="channels"/> (1 or 2). Mono goes to stereo by copying it to both sides; stereo
        /// to mono by averaging; anything wider is folded down to stereo first in the usual .wav speaker order (front left,
        /// front right, centre, LFE, then the surrounds), each speaker weighted as a standard downmix would and the whole
        /// scaled by the weights' sum, so the fold can never clip.
        /// </summary>
        public static float[][] Mix(float[][] samples, int channels)
        {
            if (channels < 1 || channels > 2)
                throw new ArgumentOutOfRangeException("channels", "Only mono or stereo can be mixed to.");
            if (samples.Length == channels)
                return samples;

            int frames = samples[0].Length;
            float[][] stereo = samples.Length == 1 ? new[] { samples[0], samples[0] } : samples.Length == 2 ? samples : FoldToStereo(samples);
            if (channels == 2)
                return stereo.Length == 2 && ReferenceEquals(stereo[0], stereo[1]) ? new[] { stereo[0], (float[])stereo[1].Clone() } : stereo;

            float[] mono = new float[frames];
            for (int i = 0; i < frames; i++)
                mono[i] = (stereo[0][i] + stereo[1][i]) * 0.5f;
            return new[] { mono };
        }

        /* Where each channel of a wider file goes, by channel count, in the default .wav speaker order: L and R are the
         * fronts, C the centre (both sides, -3 dB), F the LFE (left out), l and r a surround or back speaker on that side
         * (-3 dB), B a back centre (both sides, -6 dB) */
        private static readonly Dictionary<int, string> Layouts = new Dictionary<int, string>
        {
            { 3, "LRC" },      //3.0
            { 4, "LRlr" },     //quad
            { 5, "LRClr" },    //5.0
            { 6, "LRCFlr" },   //5.1
            { 7, "LRCFBlr" },  //6.1
            { 8, "LRCFlrlr" }, //7.1
        };

        private static float[][] FoldToStereo(float[][] samples)
        {
            const float Side = 0.7071f;
            int count = samples.Length;
            string layout;
            if (!Layouts.TryGetValue(count, out layout))
                layout = "LR" + new string('C', count - 2); //nothing standard: the rest spread evenly

            float[] left = new float[count], right = new float[count];
            for (int c = 0; c < count; c++)
            {
                switch (layout[c])
                {
                    case 'L': left[c] = 1; break;
                    case 'R': right[c] = 1; break;
                    case 'C': left[c] = right[c] = Side; break;
                    case 'l': left[c] = Side; break;
                    case 'r': right[c] = Side; break;
                    case 'B': left[c] = right[c] = 0.5f; break;
                }
            }

            float scale = 1f / Math.Max(left.Sum(), right.Sum());
            int frames = samples[0].Length;
            float[] l = new float[frames], r = new float[frames];
            for (int c = 0; c < count; c++)
            {
                if (left[c] == 0 && right[c] == 0)
                    continue;

                float[] channel = samples[c];
                float wl = left[c] * scale, wr = right[c] * scale;
                for (int i = 0; i < frames; i++)
                {
                    l[i] += channel[i] * wl;
                    r[i] += channel[i] * wr;
                }
            }
            return new[] { l, r };
        }

        #endregion

        #region SAMPLE RATE

        //A windowed sinc: this many zero crossings either side of the centre at the lower of the two rates, and the kernel
        //sampled this finely between taps, interpolated linearly in between
        private const int ZeroCrossings = 24;
        private const int Phases = 512;

        /// <summary>
        /// The audio at another sample rate. A band-limited (windowed sinc) resample, so going down keeps nothing above the
        /// new rate's Nyquist to alias back in, and going up adds nothing that wasn't there; the cutoff sits a few percent
        /// under Nyquist, where the window rolls off.
        /// </summary>
        public static float[][] Resample(float[][] samples, int from, int to)
        {
            if (from == to)
                return samples;
            if (from <= 0 || to <= 0)
                throw new ArgumentOutOfRangeException("to", "A sample rate has to be above zero.");

            double ratio = (double)to / from;
            double cutoff = Math.Min(1.0, ratio) * 0.96;
            int halfTaps = (int)Math.Ceiling(ZeroCrossings / cutoff);
            float[] kernel = Kernel(cutoff, halfTaps);

            float[][] output = new float[samples.Length][];
            for (int c = 0; c < samples.Length; c++)
                output[c] = Resample(samples[c], ratio, halfTaps, kernel);
            return output;
        }

        /// <summary>The kernel for one side, from the centre out: cutoff * sinc(cutoff * x) under a Blackman window.</summary>
        private static float[] Kernel(double cutoff, int halfTaps)
        {
            float[] kernel = new float[halfTaps * Phases + 2];
            for (int i = 0; i < kernel.Length; i++)
            {
                double x = i / (double)Phases;
                if (x > halfTaps)
                    break;

                double sinc = x == 0 ? 1 : Math.Sin(Math.PI * cutoff * x) / (Math.PI * cutoff * x);
                double w = (x / halfTaps + 1) * 0.5; //0.5 at the centre to 1 at the edge, the window's second half
                double window = 0.42 - 0.5 * Math.Cos(2 * Math.PI * w) + 0.08 * Math.Cos(4 * Math.PI * w);
                kernel[i] = (float)(cutoff * sinc * window);
            }
            return kernel;
        }

        private static float[] Resample(float[] input, double ratio, int halfTaps, float[] kernel)
        {
            long length = (long)Math.Ceiling(input.Length * ratio);
            if (length > int.MaxValue / 2)
                throw new NotSupportedException("That sound is too long to resample.");

            float[] output = new float[length];
            double step = 1.0 / ratio;
            for (long n = 0; n < length; n++)
            {
                double t = n * step;
                int centre = (int)Math.Floor(t);
                int first = Math.Max(0, centre - halfTaps + 1), last = Math.Min(input.Length - 1, centre + halfTaps);

                double sum = 0;
                for (int k = first; k <= last; k++)
                {
                    double at = Math.Abs(t - k) * Phases;
                    int index = (int)at;
                    if (index + 1 >= kernel.Length)
                        continue;

                    double frac = at - index;
                    sum += input[k] * (kernel[index] + (kernel[index + 1] - kernel[index]) * frac);
                }
                output[n] = (float)sum;
            }
            return output;
        }

        #endregion
    }
}
