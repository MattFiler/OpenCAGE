using CATHODE.Scripting;
using System;
using System.Collections.Generic;

namespace OpenCAGE
{
    /// <summary>
    /// Reads and writes the float tracks of a CAGEAnimation: what a track holds at a given time, and
    /// how a keyframe is put there.
    /// </summary>
    /// <remarks>
    /// The evaluation here is the same one the curve editor draws with (<see cref="CurveEditor"/>
    /// delegates to it), so what Animation Mode shows in the viewport is what the graph shows on
    /// screen. Interpolation is a property of the whole animation rather than of a keyframe: the
    /// keyframes carry a mode each, but the editor sets them all together and draws with one, so this
    /// takes it as an argument rather than reading it off a key.
    ///
    /// <para><b>What a keyframe's fields mean</b>, read off the retail data (8 Sep 2026):</para>
    /// <list type="bullet">
    /// <item><c>value</c> is the point (time, value) - its X is the key's time on every retail key, so
    /// it is written alongside <c>time</c> here rather than left at the struct's default of 1.</item>
    /// <item><c>tan_in</c>/<c>tan_out</c> are weighted tangents in (seconds, value) units, and the cubic
    /// Bezier control points sit at <c>key + tan_out / 3</c> and <c>key - tan_in / 3</c> - the same
    /// Hermite-to-Bezier identity Maya uses. Exported keys carry <c>tan.X</c> equal to the segment
    /// length exactly (a tangent weight of one), and the tiny <c>(0.01, 0.01)</c> pairs the level
    /// animators use are how "linear" is spelt in this representation. Reading them as whole
    /// control-point offsets, as the editor did before, put control points past the neighbouring key
    /// on a fair share of retail segments - the curve's time ran backwards, which is not something
    /// the game can be playing.</item>
    /// </list>
    /// </remarks>
    public static class CageAnimationCurves
    {
        /// <summary>Two keyframe times closer than this are the same keyframe.</summary>
        public const float TimeEpsilon = 1e-4f;

        /// <summary>A tangent is three times the control point's offset from its key.</summary>
        private const float TangentScale = 3f;

        /// <summary>Where the outgoing Bezier control point of a key sits.</summary>
        public static void OutControl(CAGEAnimation.FloatTrack.Keyframe key, out float time, out float value)
        {
            time = key.time + key.tan_out.X / TangentScale;
            value = key.value.Y + key.tan_out.Y / TangentScale;
        }

        /// <summary>Where the incoming Bezier control point of a key sits.</summary>
        public static void InControl(CAGEAnimation.FloatTrack.Keyframe key, out float time, out float value)
        {
            time = key.time - key.tan_in.X / TangentScale;
            value = key.value.Y - key.tan_in.Y / TangentScale;
        }

        /// <summary>Set the outgoing tangent so its control point lands at (time, value).</summary>
        public static void SetOutControl(CAGEAnimation.FloatTrack.Keyframe key, float time, float value)
        {
            key.tan_out = new System.Numerics.Vector2((time - key.time) * TangentScale, (value - key.value.Y) * TangentScale);
        }

        /// <summary>Set the incoming tangent so its control point lands at (time, value).</summary>
        public static void SetInControl(CAGEAnimation.FloatTrack.Keyframe key, float time, float value)
        {
            key.tan_in = new System.Numerics.Vector2((key.time - time) * TangentScale, (key.value.Y - value) * TangentScale);
        }

        /// <summary>Move a keyframe in time, keeping the (time, value) point it carries in step.</summary>
        public static void SetTime(CAGEAnimation.FloatTrack.Keyframe key, float time)
        {
            key.time = time;
            key.value.X = time;
        }

        /// <summary>A keyframe with flat tangents, as the editor makes them.</summary>
        public static CAGEAnimation.FloatTrack.Keyframe NewKeyframe(float time, float value, CAGEAnimation.InterpolationMode mode)
        {
            return new CAGEAnimation.FloatTrack.Keyframe()
            {
                time = time,
                value = new System.Numerics.Vector2(time, value),
                mode = mode,
                tan_in = new System.Numerics.Vector2(1f, 0f),
                tan_out = new System.Numerics.Vector2(1f, 0f),
            };
        }

        /// <summary>
        /// Bring a track's keyframes in line with the retail invariant that <c>value.X</c> is the key's
        /// time. Older editor builds left it at the struct default of 1 on every key they made.
        /// </summary>
        public static void NormaliseKeyframes(CAGEAnimation.FloatTrack track)
        {
            if (track?.keyframes == null)
                return;
            foreach (CAGEAnimation.FloatTrack.Keyframe key in track.keyframes)
                key.value.X = key.time;
        }

        /// <summary>The value the track holds at <paramref name="time"/>.</summary>
        public static float ValueAt(CAGEAnimation.FloatTrack track, float time, bool bezier)
        {
            if (track == null || track.keyframes == null || track.keyframes.Count == 0)
                return 0f;

            List<CAGEAnimation.FloatTrack.Keyframe> keys = Sorted(track);
            if (time <= keys[0].time) return keys[0].value.Y;
            if (time >= keys[keys.Count - 1].time) return keys[keys.Count - 1].value.Y;

            for (int i = 0; i < keys.Count - 1; i++)
            {
                CAGEAnimation.FloatTrack.Keyframe a = keys[i];
                CAGEAnimation.FloatTrack.Keyframe b = keys[i + 1];
                if (time < a.time || time > b.time) continue;

                float span = b.time - a.time;
                if (span <= 1e-8f) return a.value.Y;

                if (!bezier)
                {
                    float uLin = (time - a.time) / span;
                    return a.value.Y + (b.value.Y - a.value.Y) * uLin;
                }

                OutControl(a, out float c1t, out float c1v);
                InControl(b, out float c2t, out float c2v);

                //Solve cubic X(u) ~= time (X is usually monotonic along the segment)
                float u = (time - a.time) / span;
                for (int iter = 0; iter < 8; iter++)
                {
                    float x = Cubic(a.time, c1t, c2t, b.time, u);
                    float dx = 3f * (
                        (c1t - a.time) * (1f - u) * (1f - u)
                        + 2f * (c2t - c1t) * (1f - u) * u
                        + (b.time - c2t) * u * u);
                    if (Math.Abs(dx) < 1e-8f) break;
                    u -= (x - time) / dx;
                    if (u < 0f) u = 0f;
                    else if (u > 1f) u = 1f;
                }
                return Cubic(a.value.Y, c1v, c2v, b.value.Y, u);
            }

            return keys[keys.Count - 1].value.Y;
        }

        /// <summary>
        /// Put <paramref name="value"/> on the track at <paramref name="time"/>: update the keyframe
        /// already there, or add one. Returns true if anything actually changed.
        /// </summary>
        public static bool SetKeyframe(CAGEAnimation.FloatTrack track, float time, float value, CAGEAnimation.InterpolationMode mode)
        {
            if (track == null)
                return false;
            if (track.keyframes == null)
                track.keyframes = new List<CAGEAnimation.FloatTrack.Keyframe>();

            CAGEAnimation.FloatTrack.Keyframe existing = FindKeyframe(track, time);
            if (existing != null)
            {
                if (Math.Abs(existing.value.Y - value) <= 1e-6f)
                    return false;
                existing.value.Y = value;
                return true;
            }

            track.keyframes.Add(NewKeyframe(time, value, mode));
            track.keyframes.Sort((a, b) => a.time.CompareTo(b.time));
            return true;
        }

        /// <summary>The keyframe at <paramref name="time"/>, or null if the track has none there.</summary>
        public static CAGEAnimation.FloatTrack.Keyframe FindKeyframe(CAGEAnimation.FloatTrack track, float time)
        {
            if (track?.keyframes == null)
                return null;
            for (int i = 0; i < track.keyframes.Count; i++)
            {
                if (Math.Abs(track.keyframes[i].time - time) <= TimeEpsilon)
                    return track.keyframes[i];
            }
            return null;
        }

        private static List<CAGEAnimation.FloatTrack.Keyframe> Sorted(CAGEAnimation.FloatTrack track)
        {
            List<CAGEAnimation.FloatTrack.Keyframe> keys = new List<CAGEAnimation.FloatTrack.Keyframe>(track.keyframes);
            keys.Sort((a, b) => a.time.CompareTo(b.time));
            return keys;
        }

        private static float Cubic(float p0, float p1, float p2, float p3, float u)
        {
            float omu = 1f - u;
            return omu * omu * omu * p0
                + 3f * omu * omu * u * p1
                + 3f * omu * u * u * p2
                + u * u * u * p3;
        }
    }
}
