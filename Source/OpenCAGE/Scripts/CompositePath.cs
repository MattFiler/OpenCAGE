using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAGE
{
    public class CompositePath
    {
        private List<Composite> _composites = new List<Composite>();
        private List<Entity> _entities = new List<Entity>();

        public void StepForwards(Composite prevComp, Entity entityFollowed)
        {
            _composites.Add(prevComp);
            _entities.Add(entityFollowed);
        }

        public bool StepBackwards() => StepBackwards(out Composite c, out Entity e);
        public bool StepBackwards(out Composite prevComp, out Entity entityFollowed)
        {
            if (_composites.Count == 0 || _entities.Count == 0)
            {
                prevComp = null;
                entityFollowed = null;
                return false;
            }

            prevComp = _composites[_composites.Count - 1];
            entityFollowed = _entities[_entities.Count - 1];

            _composites.RemoveAt(_composites.Count - 1);
            _entities.RemoveAt(_entities.Count - 1);

            return true;
        }

        /// <summary>
        /// Jump to a composite at the given breadcrumb segment index (0 = first ancestor on the path).
        /// Truncates the stored drill path and returns the entity to re-select in that composite.
        /// </summary>
        public bool TryNavigateToCompositeIndex(
            Composite currentComposite,
            int segmentIndex,
            out Composite targetComposite,
            out Entity entityToSelect)
        {
            targetComposite = null;
            entityToSelect = null;

            if (currentComposite == null || segmentIndex < 0)
                return false;

            List<CompAndEnt> segments = GetPathRich(currentComposite);
            if (segmentIndex >= segments.Count - 1)
                return false;

            targetComposite = segments[segmentIndex].Composite;
            entityToSelect = segments[segmentIndex].Entity;

            if (targetComposite == null)
                return false;

            if (entityToSelect != null)
            {
                Entity resolved = targetComposite.GetEntityByID(entityToSelect.shortGUID);
                if (resolved == null)
                    entityToSelect = null;
                else
                    entityToSelect = resolved;
            }

            if (segmentIndex < _composites.Count)
                _composites.RemoveRange(segmentIndex, _composites.Count - segmentIndex);
            if (segmentIndex < _entities.Count)
                _entities.RemoveRange(segmentIndex, _entities.Count - segmentIndex);

            return true;
        }

        public void Reset()
        {
            _composites.Clear();
            _entities.Clear();
        }

        /// <summary>A drill path lifted out of the display, to put back after its panel is rebuilt.</summary>
        public class Snapshot
        {
            internal List<Composite> Composites;
            internal List<Entity> Entities;

            public int Depth => Composites == null ? 0 : Composites.Count;
        }

        /// <summary>
        /// Take a copy of the path as it stands. A copy, because the display clears its own path on the
        /// way down and a snapshot sharing the list would be emptied with it.
        /// </summary>
        public Snapshot Capture()
        {
            return new Snapshot
            {
                Composites = new List<Composite>(_composites),
                Entities = new List<Entity>(_entities),
            };
        }

        /// <summary>
        /// Put a captured path back.
        /// </summary>
        /// <remarks>
        /// A rebuild hands the level across rather than reloading it, so these are still the same
        /// composites and entities the user walked through and can go back verbatim. If any of it does
        /// not line up the whole thing is refused rather than half restored - a breadcrumb missing a
        /// step in the middle would take you somewhere you never were.
        /// </remarks>
        public bool Restore(Snapshot snapshot)
        {
            if (snapshot?.Composites == null || snapshot.Entities == null)
                return false;
            if (snapshot.Composites.Count != snapshot.Entities.Count)
                return false;
            for (int i = 0; i < snapshot.Composites.Count; i++)
                if (snapshot.Composites[i] == null || snapshot.Entities[i] == null)
                    return false;

            _composites.Clear();
            _composites.AddRange(snapshot.Composites);
            _entities.Clear();
            _entities.AddRange(snapshot.Entities);
            return true;
        }

        public Composite PreviousComposite
        {
            get
            {
                if (_composites.Count == 0) return null;
                return _composites[_composites.Count - 1];
            }
        }

        public Entity PreviousEntity
        {
            get
            {
                if (_entities.Count == 0) return null;
                return _entities[_entities.Count - 1];
            }
        }

        public List<Composite> AllComposites
        {
            get
            {
                return _composites;
            }
        }

        public List<Entity> AllEntities
        {
            get
            {
                return _entities;
            }
        }

        // returns the path as a pretty string for UI
        public string GetPath(Composite currentComp)
        {
            string path = "";
            for (int i = 0; i < _composites.Count; i++)
            {
                path += (SettingsManager.GetBool(Settings.ShowShortGuids) ? "[" + _composites[i].shortGUID.ToByteString() + "] " : "") + EditorUtils.GetCompositeName(_composites[i]) + " > ";
            }
            path += (SettingsManager.GetBool(Settings.ShowShortGuids) ? "[" + currentComp.shortGUID.ToByteString() + "] " : "") + EditorUtils.GetCompositeName(currentComp);
            return path;
        }

        // returns the path as the entity IDs for use in scripting
        public List<ShortGuid> GetPath()
        {
            List<ShortGuid> path = new List<ShortGuid>();
            for (int i = 0; i < _entities.Count; i++)
            {
                path.Add(_entities[i].shortGUID);
            }
            return path;
        }

        // returns the path with Composite and Entity objects
        public List<CompAndEnt> GetPathRich(Composite currentComp)
        {
            List<CompAndEnt> rich = new List<CompAndEnt>();
            for (int i = 0; i < _composites.Count; i++)
            {
                rich.Add(new CompAndEnt() { Composite = _composites[i], Entity = _entities[i] });
            }
            rich.Add(new CompAndEnt() { Composite = currentComp, Entity = null });
            return rich;
        }

        public struct CompAndEnt
        {
            public Composite Composite;
            public Entity Entity;
        }
    }
}
