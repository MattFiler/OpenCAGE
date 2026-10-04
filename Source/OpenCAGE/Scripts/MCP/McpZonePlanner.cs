using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// The geometry behind zoning a level: splitting a composite's placed content into zones, finding which zones
    /// neighbour each other, and which two zones each door joins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It works on plain data - entity paths and world positions - and nothing of the editor, so the same code is run
    /// over real levels outside OpenCAGE to measure it against hand-made zones (ChallengeMap4's 26). Positions and the kit
    /// names of what is placed (walls, windows, vent ducts) are all it uses: Y is up, X and Z the floor plane, metres. The
    /// work is done by <see cref="Geometry"/>.
    /// </para>
    /// <para>
    /// A path is the ids of the entities stepped through from the composite being zoned, joined by '/'; a leaf (a model,
    /// light, piece of collision...) belongs to a zone when one of the zone's entries is a prefix of its path.
    /// </para>
    /// </remarks>
    internal static partial class McpZonePlanner
    {
        #region Input
        /// <summary>A placed entity at the top of the composite being zoned (an instance or a function).</summary>
        public sealed class Item
        {
            public string Path;
            public string Name;
            /// <summary>Its function type, or the path of the composite it places.</summary>
            public string Type;
            public Vector3 Position;
            public Vector3 Rotation;
        }

        /// <summary>Something the player sees or stands on, anywhere under the composite.</summary>
        public sealed class Leaf
        {
            public string Path;
            public string Type;
            public Vector3 Position;
        }

        /// <summary>A door variant instance: what bridges two zones (it goes in both, and drives their ZoneLink).</summary>
        public sealed class Door
        {
            public string Path;
            public string Name;
            public string Type;
            public Vector3 Position;
            public Vector3 Rotation;
        }

        /// <summary>A pair of neighbouring zones: 'door' (a door between them drives a ZoneLink) or 'open' (they see each other with no door shut between, so an always-open link joins them). A pair can be both.</summary>
        public sealed class Pair
        {
            public string A;
            public string B;
            public string Kind;
            public Door Door;
            /// <summary>How many of the two zones' content points meet across the boundary with no wall or doorway between (0 if none): a rough measure of how open it is.</summary>
            public int Contacts;
            /// <summary>Why an 'open' pair needs an always-open link: "contact" (their content meets with nothing between), "window_door"
            /// (a door between them is see-through), "window_wall" (a see-through wall), or "beside_door" (they also meet away from the door).</summary>
            public string Reason;
        }

        /// <summary>Whether <paramref name="path"/> is <paramref name="prefix"/> or lies under it.</summary>
        public static bool Under(string path, string prefix)
        {
            return path.Length >= prefix.Length && string.CompareOrdinal(path, 0, prefix, 0, prefix.Length) == 0 && (path.Length == prefix.Length || path[prefix.Length] == '/');
        }
        #endregion

        /// <summary>Leaves bucketed in coarse cells, for finding those near a point without looking at every one.</summary>
        public sealed class LeafIndex
        {
            private const float Cell = 8f;
            private readonly Dictionary<(int, int), List<Leaf>> _cells = new Dictionary<(int, int), List<Leaf>>();

            public LeafIndex(IEnumerable<Leaf> leaves)
            {
                foreach (Leaf leaf in leaves)
                {
                    (int, int) key = ((int)Math.Floor(leaf.Position.X / Cell), (int)Math.Floor(leaf.Position.Z / Cell));
                    if (!_cells.TryGetValue(key, out List<Leaf> list)) _cells[key] = list = new List<Leaf>();
                    list.Add(leaf);
                }
            }

            /// <summary>The leaves within <paramref name="reach"/> metres of a point across the floor plane (any height).</summary>
            public List<Leaf> Near(Vector3 at, float reach)
            {
                List<Leaf> found = new List<Leaf>();
                int x0 = (int)Math.Floor((at.X - reach) / Cell), x1 = (int)Math.Floor((at.X + reach) / Cell);
                int z0 = (int)Math.Floor((at.Z - reach) / Cell), z1 = (int)Math.Floor((at.Z + reach) / Cell);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                        if (_cells.TryGetValue((x, z), out List<Leaf> list))
                            foreach (Leaf leaf in list)
                                if (Math.Abs(leaf.Position.X - at.X) <= reach && Math.Abs(leaf.Position.Z - at.Z) <= reach) found.Add(leaf);
                return found;
            }
        }
    }
}
