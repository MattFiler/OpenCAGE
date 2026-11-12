using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.TextureTools;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using AssetKind = CathodeLib.AssetModificationInfoTable.AssetKind;

namespace OpenCAGE
{
    /// <summary>The editor side of <see cref="TextureStreaming"/>: streaming a texture again, and changes made as one undo step.</summary>
    public static partial class TextureStreaming
    {
        /// <summary>
        /// Streams a kept-loaded texture again: the image goes back to the streamed slot, and the persistent slot gets a resident copy of
        /// <see cref="ResidentEdge"/> px or under cut from its chain, as the importer makes one (a single-mip image keeps a full copy).
        /// Volume textures are refused: the game keeps every one persistent only.
        /// </summary>
        public static bool StreamAgain(Textures.TEX4 texture, out string problem)
        {
            problem = null;
            if (!IsKeptLoaded(texture)) { problem = texture == null ? "no texture" : "it isn't kept loaded"; return false; }
            Textures.TEX4.Texture full = texture.TexturePersistent;
            if (texture.StateFlags.HasFlag(Textures.TextureStateFlag.VOLUME) || full.Depth > 1) { problem = "it is a volume texture (the game keeps every one loaded)"; return false; }
            int drop = TextureConverter.DropForEdge(full.Width, full.Height, ResidentEdge, full.MipLevels);
            Textures.TEX4.Texture resident = drop > 0 ? TextureConverter.Slice(full, texture.Format, drop) : null;
            texture.TextureStreamed = new Textures.TEX4.Texture() { Width = full.Width, Height = full.Height, Depth = full.Depth, MipLevels = full.MipLevels, Content = (byte[])full.Content.Clone() };
            texture.TexturePersistent = resident ?? new Textures.TEX4.Texture() { Width = full.Width, Height = full.Height, Depth = full.Depth, MipLevels = full.MipLevels, Content = (byte[])full.Content.Clone() };
            return true;
        }

        /// <summary>
        /// Keeps textures loaded (or streams them again) as one undo step labelled <paramref name="label"/>. UI thread. Returns how many
        /// changed; <paramref name="problems"/> says why others didn't.
        /// </summary>
        public static int SetKeptLoaded(Level level, IEnumerable<Textures.TEX4> textures, bool keep, string label, out List<string> problems)
        {
            problems = new List<string>();
            List<Textures.TEX4> changed = new List<Textures.TEX4>();
            List<(Textures.TEX4 texture, Textures.TEX4.Texture streamed, Textures.TEX4.Texture persistent)> before = new List<(Textures.TEX4, Textures.TEX4.Texture, Textures.TEX4.Texture)>();
            foreach (Textures.TEX4 texture in textures.Distinct())
            {
                var was = (texture, texture.TextureStreamed, texture.TexturePersistent);
                string problem = null;
                bool done = keep ? KeepLoaded(texture) : StreamAgain(texture, out problem);
                if (!done) { problems.Add(texture.Name + ": " + (keep ? CannotKeepLoaded(texture) : problem)); continue; }
                before.Add(was);
                changed.Add(texture);
            }
            if (changed.Count == 0) return 0;
            var after = changed.Select(o => (texture: o, streamed: o.TextureStreamed, persistent: o.TexturePersistent)).ToList();
            Record(label, level, changed,
                () => { foreach (var o in after) { o.texture.TextureStreamed = o.streamed; o.texture.TexturePersistent = o.persistent; } },
                () => { foreach (var o in before) { o.texture.TextureStreamed = o.Item2; o.texture.TexturePersistent = o.Item3; } });
            return changed.Count;
        }

        /// <summary>Merge identical textures (<see cref="MergeDuplicates"/>) as one undo step. UI thread. Returns the material slots moved.</summary>
        public static int MergeDuplicates(Level level, List<DuplicateGroup> groups, string label)
        {
            List<(TexturePtr slot, Textures.TEX4 was)> moved = MergeDuplicates(level, groups);
            if (moved.Count == 0) return 0;
            List<(TexturePtr slot, Textures.TEX4 now)> to = moved.Select(o => (o.slot, o.slot.Texture)).ToList();
            Record(label, level, groups.Select(o => o.Keep).ToList(),
                () => { foreach (var o in to) o.slot.Texture = o.now; },
                () => { foreach (var o in moved) o.slot.Texture = o.was; });
            return moved.Count;
        }

        private static void Record(string label, Level level, List<Textures.TEX4> textures, Action apply, Action revert)
        {
            TextureStreamingEdit edit = new TextureStreamingEdit(label, level, textures.Select(o => o.Name).ToArray(), apply, revert);
            edit.Changed();
            UndoStack.Current?.Record(edit);
        }

        /// <summary>One undo step for the texture changes above.</summary>
        private sealed class TextureStreamingEdit : IEdit
        {
            private readonly Action _apply, _revert;
            private readonly Level _level;
            private readonly string[] _textures;

            public string Label { get; }
            public ShortGuid CompositeId => ShortGuid.Invalid;
            public ShortGuid EntityId => ShortGuid.Invalid;

            public TextureStreamingEdit(string label, Level level, string[] textures, Action apply, Action revert)
            {
                Label = label;
                _level = level;
                _textures = textures;
                _apply = apply;
                _revert = revert;
            }

            public void Apply(UndoContext context) { _apply(); Changed(); }
            public void Revert(UndoContext context) { _revert(); Changed(); }
            public bool TryMerge(IEdit next) => false;

            public void Changed()
            {
                foreach (string name in _textures)
                    EditHistory.AssetChanged(_level, AssetKind.TEXTURE, name);
                DirtyTracker.MarkLevelDataModified();
                Singleton.OnResourceModified?.Invoke();
            }
        }
    }
}
