using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAGE
{
    public static class DirtyTracker
    {
        private static bool _isDirty = false;
        public static bool IsDirty => _isDirty;

        public static Action OnDirty;
        public static Action OnClean;
        public static Action<bool> OnChanged;

        static DirtyTracker()
        {
            Singleton.OnLevelLoaded += MarkClean;
            Singleton.OnSaved += MarkClean;

            Singleton.OnCompositeAdded += MarkDirty;
            Singleton.OnCompositeDeleted += MarkDirty;
            Singleton.OnCompositeRenamed += MarkDirty;

            Singleton.OnEntityAdded += MarkDirty;
            Singleton.OnEntityDeleted += MarkDirty;
            Singleton.OnEntityRenamed += MarkDirty;
            Singleton.OnEntityMoved += MarkDirty;

            Singleton.OnResourceModified += MarkDirty;
            Singleton.OnParameterModified += MarkDirty;
            Singleton.OnEntityParameterModified += (entity, parameter, removed) => MarkDirty();
        }

        /// <summary>
        /// Flag that level data has been changed. Most changes come through the Singleton events above, but
        /// call this directly from anywhere that edits level data without raising one of them (flowgraph
        /// links/layouts, the function editors, etc).
        /// </summary>
        public static void MarkLevelDataModified() => MarkDirty();

        /// <summary>
        /// Capture the state of some level data, for editors that mutate it in many places. Pair with
        /// MarkIfChanged when the editor closes: that way a new edit path added to the editor later is
        /// covered automatically, rather than needing its own MarkLevelDataModified call.
        /// </summary>
        public static string Snapshot(object data)
        {
            try
            {
                return JsonConvert.SerializeObject(data);
            }
            catch
            {
                return null; //couldn't snapshot - MarkIfChanged will assume the worst
            }
        }

        /// <summary>
        /// Mark the level as modified if the data differs from the snapshot (or if it couldn't be captured -
        /// a spurious "unsaved changes" prompt is far better than silently losing an edit).
        /// </summary>
        public static void MarkIfChanged(string snapshot, object data)
        {
            if (snapshot == null)
            {
                MarkDirty();
                return;
            }

            string current = Snapshot(data);
            if (current == null || current != snapshot)
                MarkDirty();
        }

        private static void MarkClean(object a) => MarkClean();
        private static void MarkClean()
        {
            bool changed = _isDirty;
            _isDirty = false;
            OnClean?.Invoke();
            if (changed) OnChanged?.Invoke(false);
        }

        private static void MarkDirty(object a, object b) => MarkDirty();
        private static void MarkDirty(object a) => MarkDirty();
        private static void MarkDirty()
        {
            bool changed = !_isDirty;
            _isDirty = true;
            OnDirty?.Invoke();
            if (changed) OnChanged?.Invoke(true);
        }
    }
}
