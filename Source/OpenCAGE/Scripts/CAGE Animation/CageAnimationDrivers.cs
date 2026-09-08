using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenCAGE
{
    /// <summary>
    /// Which parameters, on which entity instances, a CAGEAnimation drives. This is what paints the
    /// purple highlight in the inspector.
    /// </summary>
    /// <remarks>
    /// Built the same way <see cref="ZoneMembership"/> is: a CAGEAnimation's connections name their
    /// targets by paths written relative to the composite it lives in, so a target is only pinned down
    /// once you know which PLACEMENT of that composite you are standing in. So the walk descends the
    /// instance tree from the composite the active hierarchy starts at, carrying the path that got
    /// there, and every answer is an instance path from that root - the same address the inspector
    /// builds for the entity it is showing, and the same one the Level Viewer addresses nodes by.
    ///
    /// A level has a few dozen CAGEAnimations and tens of thousands of composite instances, so the
    /// walk is first cut down to the composites that can reach one at all.
    /// </remarks>
    public sealed class CageAnimationDrivers
    {
        private readonly Dictionary<string, HashSet<uint>> _driven = new Dictionary<string, HashSet<uint>>();

        /// <summary>Every parameter driven on this entity instance, or null if it isn't animated.</summary>
        public HashSet<uint> DrivenParameters(IReadOnlyList<uint> instancePath)
        {
            if (instancePath == null || instancePath.Count == 0 || _driven.Count == 0)
                return null;
            return _driven.TryGetValue(Key(instancePath), out HashSet<uint> parameters) ? parameters : null;
        }

        #region CACHE

        private static readonly object _cacheLock = new object();
        private static Commands _cachedCommands;
        private static ShortGuid _cachedRoot;
        private static CageAnimationDrivers _cached;

        /// <summary>
        /// The index for a hierarchy starting at <paramref name="from"/>, built on first use and kept
        /// until something changes it.
        /// </summary>
        public static CageAnimationDrivers For(Commands commands, Composite from)
        {
            if (commands == null || from == null)
                return new CageAnimationDrivers();

            lock (_cacheLock)
            {
                if (_cached != null && ReferenceEquals(_cachedCommands, commands) && _cachedRoot == from.shortGUID)
                    return _cached;
            }

            CageAnimationDrivers built = Build(commands, from);

            lock (_cacheLock)
            {
                _cachedCommands = commands;
                _cachedRoot = from.shortGUID;
                _cached = built;
            }
            return built;
        }

        /// <summary>
        /// Something changed what a CAGEAnimation drives - a track added or removed, an undo, a level
        /// load. The next lookup rebuilds.
        /// </summary>
        public static void Invalidate()
        {
            lock (_cacheLock)
            {
                _cached = null;
                _cachedCommands = null;
                _cachedRoot = ShortGuid.Invalid;
            }
        }

        #endregion

        #region BUILD

        private static CageAnimationDrivers Build(Commands commands, Composite from)
        {
            CageAnimationDrivers drivers = new CageAnimationDrivers();

            HashSet<ShortGuid> worthDescending = FindCompositesReachingAnAnimation(commands);
            if (worthDescending.Count == 0)
                return drivers;

            Composite root = commands.EntryPoints != null && commands.EntryPoints.Length != 0
                ? commands.EntryPoints[0]
                : null;

            Walk(commands, from, new List<uint>(), new HashSet<ShortGuid>(), worthDescending, drivers,
                relativePathsOnly: from != root);
            return drivers;
        }

        /// <summary>
        /// Composites holding a CAGEAnimation, plus everything that can instance its way down to one.
        /// </summary>
        private static HashSet<ShortGuid> FindCompositesReachingAnAnimation(Commands commands)
        {
            Dictionary<ShortGuid, List<ShortGuid>> instancedBy = new Dictionary<ShortGuid, List<ShortGuid>>();
            Queue<ShortGuid> pending = new Queue<ShortGuid>();
            HashSet<ShortGuid> reaching = new HashSet<ShortGuid>();

            foreach (Composite composite in commands.Entries)
            {
                bool holdsAnimation = false;
                foreach (FunctionEntity function in composite.functions)
                {
                    if (function.function.IsFunctionType)
                    {
                        if (function is CAGEAnimation)
                            holdsAnimation = true;
                        continue;
                    }

                    if (!instancedBy.TryGetValue(function.function, out List<ShortGuid> parents))
                    {
                        parents = new List<ShortGuid>();
                        instancedBy[function.function] = parents;
                    }
                    if (!parents.Contains(composite.shortGUID))
                        parents.Add(composite.shortGUID);
                }

                if (holdsAnimation && reaching.Add(composite.shortGUID))
                    pending.Enqueue(composite.shortGUID);
            }

            while (pending.Count != 0)
            {
                ShortGuid current = pending.Dequeue();
                if (!instancedBy.TryGetValue(current, out List<ShortGuid> parents))
                    continue;

                foreach (ShortGuid parent in parents)
                {
                    if (reaching.Add(parent))
                        pending.Enqueue(parent);
                }
            }

            return reaching;
        }

        private static void Walk(
            Commands commands,
            Composite composite,
            List<uint> path,
            HashSet<ShortGuid> onStack,
            HashSet<ShortGuid> worthDescending,
            CageAnimationDrivers drivers,
            bool relativePathsOnly)
        {
            //A composite that (however indirectly) instances itself would otherwise walk forever
            if (!onStack.Add(composite.shortGUID))
                return;

            foreach (FunctionEntity function in composite.functions)
            {
                if (function.function.IsFunctionType)
                {
                    if (function is CAGEAnimation animation)
                        Emit(commands, composite, path, animation, drivers, relativePathsOnly);
                    continue;
                }

                if (!worthDescending.Contains(function.function))
                    continue;

                Composite nested = commands.GetComposite(function.function);
                if (nested == null)
                    continue;

                path.Add(function.shortGUID.AsUInt32);
                Walk(commands, nested, path, onStack, worthDescending, drivers, relativePathsOnly);
                path.RemoveAt(path.Count - 1);
            }

            onStack.Remove(composite.shortGUID);
        }

        private static void Emit(
            Commands commands,
            Composite composite,
            List<uint> path,
            CAGEAnimation animation,
            CageAnimationDrivers drivers,
            bool relativePathsOnly)
        {
            foreach (CAGEAnimation.Connection connection in animation.connections)
            {
                //Event-track bindings drive nothing on the entity they name, so they colour nothing
                if (connection == null || connection.target_param == ShortGuid.Invalid)
                    continue;
                if (!IsFloatTrackConnection(animation, connection))
                    continue;

                if (!EntityInstancePath.TryResolve(commands, composite, path, connection.connectedEntity,
                        out List<uint> full, out bool relativeToHere))
                    continue;

                //Walking from a composite that isn't the level root: a path written from the root names
                //somewhere this walk cannot place, so it is not ours to claim
                if (relativePathsOnly && !relativeToHere)
                    continue;

                drivers.Add(full, connection.target_param.AsUInt32);
            }
        }

        private static bool IsFloatTrackConnection(CAGEAnimation animation, CAGEAnimation.Connection connection)
        {
            for (int i = 0; i < animation.floatTracks.Count; i++)
            {
                if (animation.floatTracks[i].shortGUID == connection.target_track)
                    return true;
            }
            return false;
        }

        private void Add(List<uint> instancePath, uint parameter)
        {
            string key = Key(instancePath);
            if (!_driven.TryGetValue(key, out HashSet<uint> parameters))
            {
                parameters = new HashSet<uint>();
                _driven.Add(key, parameters);
            }
            parameters.Add(parameter);
        }

        private static string Key(IReadOnlyList<uint> instancePath)
        {
            StringBuilder builder = new StringBuilder(instancePath.Count * 9);
            for (int i = 0; i < instancePath.Count; i++)
            {
                if (i != 0) builder.Append('/');
                builder.Append(instancePath[i]);
            }
            return builder.ToString();
        }

        #endregion
    }
}
