using CATHODE.Scripting;
using System.Collections.Generic;

namespace OpenCAGE
{
    /// <summary>
    /// Where the user has been, so the composite display can offer a back button.
    ///
    /// An entry records the whole drill path, not just the composite: the same composite opened from
    /// two different hierarchies is two different places to be, and going back to one of them should
    /// put the breadcrumb back the way it was.
    ///
    /// Only ids are held, never Composite/Entity objects - a level reload replaces every instance, and
    /// the history outliving that would hand back stale references. Ids are name hashes and so are
    /// shared between levels, which is exactly why this is cleared on load too.
    /// </summary>
    public static class CompositeNavigationHistory
    {
        /// <summary>How far back the dropdown offers to go.</summary>
        public const int MaxEntries = 5;

        public class Entry
        {
            /// <summary>Composite the drill path starts from (the composite itself when not drilled in).</summary>
            public ShortGuid EntryComposite;

            /// <summary>Entities followed from the entry composite, in order.</summary>
            public List<ShortGuid> PathEntities = new List<ShortGuid>();

            /// <summary>Where that path ends up - what the user was actually looking at.</summary>
            public ShortGuid Composite;

            public bool SamePlaceAs(Entry other)
            {
                if (other == null || Composite != other.Composite || EntryComposite != other.EntryComposite)
                    return false;
                if (PathEntities.Count != other.PathEntities.Count)
                    return false;

                for (int i = 0; i < PathEntities.Count; i++)
                {
                    if (PathEntities[i] != other.PathEntities[i])
                        return false;
                }

                return true;
            }
        }

        //Most recent first
        private static readonly List<Entry> _history = new List<Entry>();

        public static bool CanGoBack => _history.Count != 0;

        static CompositeNavigationHistory()
        {
            Singleton.OnLevelLoaded += OnLevelLoaded;
        }

        private static void OnLevelLoaded(LevelContent content)
        {
            Clear();
        }

        public static void Clear()
        {
            _history.Clear();
        }

        /// <summary>
        /// Note the place being navigated away from. Re-visiting somewhere already in the list moves it
        /// to the front rather than stacking duplicates, so the history stays useful when the user
        /// bounces between the same two composites.
        /// </summary>
        public static void Record(Entry entry)
        {
            if (entry == null)
                return;

            _history.RemoveAll(o => o.SamePlaceAs(entry));
            _history.Insert(0, entry);

            while (_history.Count > MaxEntries)
                _history.RemoveAt(_history.Count - 1);
        }

        /// <summary>Snapshot a place. The ids are copied out, so later edits to the path don't alter it.</summary>
        public static Entry CreateEntry(Composite composite, CompositePath path)
        {
            if (composite == null)
                return null;

            Entry entry = new Entry
            {
                Composite = composite.shortGUID,
                EntryComposite = composite.shortGUID,
            };

            //A drill path starts at its first composite and follows one entity per hop
            if (path != null && path.AllComposites.Count != 0 && path.AllComposites[0] != null)
            {
                entry.EntryComposite = path.AllComposites[0].shortGUID;
                for (int i = 0; i < path.AllEntities.Count; i++)
                {
                    if (path.AllEntities[i] == null)
                    {
                        //Broken hop - the path can't be replayed, so fall back to the composite alone
                        entry.EntryComposite = composite.shortGUID;
                        entry.PathEntities.Clear();
                        break;
                    }

                    entry.PathEntities.Add(path.AllEntities[i].shortGUID);
                }
            }

            return entry;
        }

        /// <summary>
        /// The places we can go back to, nearest first. Entries whose composite has been deleted are
        /// dropped outright; ones that have merely lost their entry composite are kept, because the
        /// composite itself is still somewhere valid to land - only the hierarchy replay is lost.
        /// </summary>
        public static List<Entry> GetHistory()
        {
            List<Entry> valid = new List<Entry>();
            CATHODE.Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            if (commands == null)
                return valid;

            for (int i = 0; i < _history.Count; i++)
            {
                if (commands.GetComposite(_history[i].Composite) != null)
                    valid.Add(_history[i]);
            }

            return valid;
        }

        /// <summary>Resolve the composite an entry lands on, or null if it has since been deleted.</summary>
        public static Composite ResolveComposite(Entry entry)
        {
            if (entry == null)
                return null;

            return Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands?.GetComposite(entry.Composite);
        }

        /// <summary>Resolve the composite an entry's drill path starts from.</summary>
        public static Composite ResolveEntryComposite(Entry entry)
        {
            if (entry == null)
                return null;

            return Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands?.GetComposite(entry.EntryComposite);
        }

        /// <summary>
        /// Take the most recent entry off the history and hand it back, or the entry at
        /// <paramref name="index"/> when stepping further back from the dropdown - everything more
        /// recent than the chosen one goes with it, the way a browser's back list behaves.
        /// </summary>
        public static Entry StepBack(int index = 0)
        {
            CATHODE.Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            if (commands == null)
                return null;

            //Drop everything deleted first. The index comes from the dropdown, which is built off the
            //filtered list, so pruning only from `index` onwards would misalign the two whenever a
            //deleted entry sat in front of the one that was picked.
            _history.RemoveAll(o => commands.GetComposite(o.Composite) == null);

            if (index < 0 || index >= _history.Count)
                return null;

            Entry target = _history[index];
            _history.RemoveRange(0, index + 1);
            return target;
        }
    }
}



