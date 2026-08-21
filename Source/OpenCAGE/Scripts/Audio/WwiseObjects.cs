using System.Collections.Generic;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// The object kinds that appear in a soundbank's HIRC chunk.
    ///
    /// Only the ones we actually walk are modelled below; the rest are still indexed by id and type so
    /// that parent lookups resolve, they just carry no extra data.
    /// </summary>
    public enum WwiseObjectType : byte
    {
        Settings = 1,
        Sound = 2,
        Action = 3,
        Event = 4,
        RandomSequenceContainer = 5,
        SwitchContainer = 6,
        ActorMixer = 7,
        AudioBus = 8,
        BlendContainer = 9,
        MusicSegment = 10,
        MusicTrack = 11,
        MusicSwitchContainer = 12,
        MusicRandomSequenceContainer = 13,
        Attenuation = 14,
        DialogueEvent = 15,
        MotionBus = 16,
        MotionFx = 17,
        Effect = 18,
        AuxBus = 19,
        Modulator = 20,
    }

    public enum WwiseStreamType : uint
    {
        /// <summary>Media lives in the bank's own DATA chunk.</summary>
        Embedded = 0,

        /// <summary>Media is a loose .wem, or an entry in a file package.</summary>
        Streamed = 1,

        /// <summary>Streamed, but with the opening of the file kept in the bank.</summary>
        PrefetchStreamed = 2,
    }

    /// <summary>Where the bytes for one piece of audio actually live.</summary>
    public sealed class WwiseMediaLocation
    {
        /// <summary>The file to read from - a .wem, a .bnk, or a .pck.</summary>
        public string File;

        /// <summary>Absolute offset into <see cref="File"/>.</summary>
        public long Offset;

        public int Length;

        /// <summary>What produced this - shown in the UI so it is obvious where a sound came from.</summary>
        public string Origin;
    }

    public class WwiseObject
    {
        public WwiseObjectType Type;
        public uint Id;

        /// <summary>
        /// The object one level up. Containers are walked downwards by inverting this across the whole
        /// index, rather than by reading each container's own child list - see WwiseSoundBank for why.
        /// </summary>
        public uint ParentId;

        /// <summary>The bank this object was read out of.</summary>
        public WwiseSoundBank Bank;
    }

    public sealed class WwiseEvent : WwiseObject
    {
        public uint[] ActionIds = new uint[0];
    }

    public sealed class WwiseAction : WwiseObject
    {
        public ushort ActionType;
        public uint TargetId;

        /// <summary>
        /// The high byte is the action, the low byte its scope. 0x04 is Play - the only one that can
        /// lead to audible audio, so the only one preview follows.
        /// </summary>
        public bool IsPlay
        {
            get { return (ActionType >> 8) == 0x04; }
        }
    }

    public sealed class WwiseSound : WwiseObject
    {
        public uint PluginId;
        public WwiseStreamType StreamType;
        public uint SourceId;
        public uint FileId;

        /// <summary>
        /// False for the source plugins - tone generators and silence - which have no media to play.
        /// The low nibble of the plugin id is the plugin's kind, and 1 means "codec".
        /// </summary>
        public bool HasMedia
        {
            get { return (PluginId & 0x0F) == 1; }
        }
    }

    /// <summary>A music track, which carries its sources inline rather than through child sounds.</summary>
    public sealed class WwiseMusicTrack : WwiseObject
    {
        public List<uint> SourceIds = new List<uint>();
    }
}
