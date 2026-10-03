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

            /// <summary>The composite the path starts from, or null when it is empty.</summary>
            public Composite EntryComposite => Depth == 0 ? null : Composites[0];
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

        /// <summary>
        /// A place in the hierarchy by id rather than by object: the composite the user was in, the composites and
        /// instances they stepped down through to reach it, and what they had selected there.
        /// </summary>
        /// <remarks>
        /// For a caller that closes the display while it swaps composites out (an import or port that overwrites).
        /// By the time the user is put back, a composite it replaced is the level's new copy, and only the id still
        /// finds it - a <see cref="Snapshot"/> would hand back the old objects.
        /// </remarks>
        public class Place
        {
            internal List<ShortGuid> Composites = new List<ShortGuid>();
            internal List<ShortGuid> Entities = new List<ShortGuid>();
            internal ShortGuid Composite;
            internal List<ShortGuid> Selected = new List<ShortGuid>();

            /// <summary>How many instances the user had stepped down through.</summary>
            public int Depth => Entities.Count;

            /// <summary>What was selected that <paramref name="composite"/> still holds, when it is the composite the user was in.</summary>
            public List<Entity> SelectionIn(Composite composite)
            {
                if (composite == null || composite.shortGUID != Composite)
                    return new List<Entity>();
                return Selected.Select(o => composite.GetEntityByID(o)).Where(o => o != null).ToList();
            }
        }

        /// <summary>Take the path down to <paramref name="current"/>, and the selection there, as ids.</summary>
        public Place CapturePlace(Composite current, IEnumerable<Entity> selected)
        {
            if (current == null)
                return null;

            Place place = new Place() { Composite = current.shortGUID };
            //A hop with nothing recorded can't be walked again: the place is then the composite alone
            if (_composites.Count == _entities.Count && !_composites.Contains(null) && !_entities.Contains(null))
            {
                place.Composites.AddRange(_composites.Select(o => o.shortGUID));
                place.Entities.AddRange(_entities.Select(o => o.shortGUID));
            }
            if (selected != null)
                place.Selected.AddRange(selected.Where(o => o != null).Select(o => o.shortGUID));
            return place;
        }

        /// <summary>
        /// Walk a place back down through the composites the level holds now, as far as it still leads.
        /// </summary>
        /// <remarks>
        /// Every step has to find the instance it followed, still placing the composite it did then: one that has
        /// gone, or now places something else, ends the walk in the composite it was in, rather than going on
        /// somewhere the user never was. If the composite the walk starts from has gone, the one the user was in is
        /// all that can be put back, on its own.
        /// </remarks>
        /// <param name="landed">The composite the walk ended in; null when nothing of the place is left.</param>
        /// <returns>The path down to <paramref name="landed"/>, empty when it stands on its own.</returns>
        public static Snapshot Resolve(CATHODE.Commands commands, Place place, out Composite landed)
        {
            landed = null;
            Snapshot path = new Snapshot() { Composites = new List<Composite>(), Entities = new List<Entity>() };
            if (commands == null || place == null)
                return path;

            Composite current = place.Composites.Count == 0 ? null : commands.GetComposite(place.Composites[0]);
            if (current == null)
            {
                landed = commands.GetComposite(place.Composite);
                return path;
            }

            for (int i = 0; i < place.Entities.Count; i++)
            {
                ShortGuid expected = i + 1 < place.Composites.Count ? place.Composites[i + 1] : place.Composite;
                Entity entity = current.GetEntityByID(place.Entities[i]);
                Composite child = entity is FunctionEntity function && !function.function.IsFunctionType ? commands.GetComposite(function.function) : null;
                if (child == null || child.shortGUID != expected)
                    break;

                path.Composites.Add(current);
                path.Entities.Add(entity);
                current = child;
            }
            landed = current;
            return path;
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
