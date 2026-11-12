using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// What a CAGEAnimation keeps when an entity it keys is deleted. The delete takes the animation's bindings that reach the entity
    /// (<see cref="CAGEAnimation.connections"/>) but would leave the float tracks they played, growing the animation every time a recipe
    /// deletes and re-creates what it animates; here those tracks go too. A track another binding still plays stays; event tracks stay
    /// (their string events are the animation's own output pins, linked from elsewhere).
    /// </summary>
    public static class CageAnimationTrackCascade
    {
        /// <summary>A float track taken off an animation, where it was in the list at the time.</summary>
        public sealed class Removed
        {
            public int Index;
            public CAGEAnimation.FloatTrack Track;
        }

        /// <summary>
        /// The float tracks of <paramref name="animation"/> that <paramref name="removedBindings"/> played and no binding it still has
        /// plays: taken off (into a new list - an undo record may hold the old one), and returned in ascending index order.
        /// Call once the bindings are gone. Nothing when no removed binding played a float track.
        /// </summary>
        public static List<Removed> RemoveUnplayed(CAGEAnimation animation, IEnumerable<CAGEAnimation.Connection> removedBindings)
        {
            List<Removed> removed = new List<Removed>();
            if (animation?.floatTracks == null || removedBindings == null)
                return removed;
            HashSet<ShortGuid> played = new HashSet<ShortGuid>(removedBindings.Where(o => o != null).Select(o => o.target_track));
            if (played.Count == 0)
                return removed;
            HashSet<ShortGuid> stillPlayed = new HashSet<ShortGuid>((animation.connections ?? new List<CAGEAnimation.Connection>()).Where(o => o != null).Select(o => o.target_track));

            List<CAGEAnimation.FloatTrack> kept = new List<CAGEAnimation.FloatTrack>(animation.floatTracks.Count);
            for (int i = 0; i < animation.floatTracks.Count; i++)
            {
                CAGEAnimation.FloatTrack track = animation.floatTracks[i];
                if (track != null && played.Contains(track.shortGUID) && !stillPlayed.Contains(track.shortGUID))
                    removed.Add(new Removed() { Index = i, Track = track });
                else
                    kept.Add(track);
            }
            if (removed.Count != 0)
                animation.floatTracks = kept;
            return removed;
        }

        /// <summary>Put tracks <see cref="RemoveUnplayed"/> took back where they were (into a new list). The last removal goes back first.</summary>
        public static void Restore(CAGEAnimation animation, List<Removed> removed)
        {
            if (animation == null || removed == null || removed.Count == 0)
                return;
            List<CAGEAnimation.FloatTrack> tracks = new List<CAGEAnimation.FloatTrack>(animation.floatTracks ?? new List<CAGEAnimation.FloatTrack>());
            //Taken in ascending order from one list: inserting in that order lands each at its old index
            foreach (Removed record in removed.OrderBy(o => o.Index))
            {
                if (tracks.Contains(record.Track)) continue;
                tracks.Insert(Math.Min(record.Index, tracks.Count), record.Track);
            }
            animation.floatTracks = tracks;
        }

        /// <summary>
        /// As <see cref="RemoveUnplayed"/>, inside an MCP script edit: the transaction does not snapshot float tracks, so the
        /// edit sets the lists itself on every redo and undo. Returns how many tracks went.
        /// </summary>
        internal static int RemoveUnplayed(MCP.McpScriptEdit edit, CAGEAnimation animation, IEnumerable<CAGEAnimation.Connection> removedBindings)
        {
            if (edit == null || animation == null)
                return 0;
            List<CAGEAnimation.FloatTrack> before = animation.floatTracks;
            List<Removed> removed = RemoveUnplayed(animation, removedBindings);
            if (removed.Count == 0)
                return 0;
            List<CAGEAnimation.FloatTrack> after = animation.floatTracks;
            edit.OnEachApply(reverted => animation.floatTracks = new List<CAGEAnimation.FloatTrack>(reverted ? before : after));
            return removed.Count;
        }

        /// <summary>"N tracks" / "1 track", for what a delete reports.</summary>
        public static string Describe(int count) => count + " animation track" + (count == 1 ? "" : "s");
    }
}
