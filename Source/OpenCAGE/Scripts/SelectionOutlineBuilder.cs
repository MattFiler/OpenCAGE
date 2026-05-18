using HelixToolkit.Wpf;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using static OpenCAGE.CompositeSceneBuilder;

namespace OpenCAGE
{
    internal static class SelectionOutlineBuilder
    {
        private static readonly Color OutlineColor = Color.FromRgb(255, 140, 0);

        public static Visual3D Create(SceneNode node)
        {
            if (node?.Content == null)
                return null;

            Point3DCollection points = new Point3DCollection();
            foreach (GeometryModel3D geometry in EnumerateGeometries(node.Content))
            {
                if (geometry?.Geometry is MeshGeometry3D mesh)
                    AddBoundaryEdges(mesh, points);
            }

            if (points.Count == 0)
                return null;

            LinesVisual3D lines = new LinesVisual3D
            {
                Color = OutlineColor,
                Thickness = 2.5,
                Points = points,
            };

            lines.Transform = node.Transform;
            return lines;
        }

        private static IEnumerable<GeometryModel3D> EnumerateGeometries(Model3DGroup group)
        {
            if (group == null)
                yield break;

            foreach (Model3D child in group.Children)
            {
                if (child is GeometryModel3D geometry)
                    yield return geometry;
                else if (child is Model3DGroup childGroup)
                {
                    foreach (GeometryModel3D nested in EnumerateGeometries(childGroup))
                        yield return nested;
                }
            }
        }

        private static void AddBoundaryEdges(MeshGeometry3D mesh, Point3DCollection points)
        {
            Point3DCollection positions = mesh.Positions;
            Int32Collection indices = mesh.TriangleIndices;
            if (positions == null || positions.Count == 0 || indices == null || indices.Count < 3)
                return;

            Dictionary<ulong, int> edgeCounts = new Dictionary<ulong, int>();
            for (int i = 0; i + 2 < indices.Count; i += 3)
            {
                CountEdge(edgeCounts, indices[i], indices[i + 1]);
                CountEdge(edgeCounts, indices[i + 1], indices[i + 2]);
                CountEdge(edgeCounts, indices[i + 2], indices[i]);
            }

            foreach (KeyValuePair<ulong, int> edge in edgeCounts)
            {
                if (edge.Value != 1)
                    continue;

                int a = (int)(edge.Key >> 32);
                int b = (int)(edge.Key & 0xFFFFFFFF);
                points.Add(positions[a]);
                points.Add(positions[b]);
            }
        }

        private static void CountEdge(Dictionary<ulong, int> edgeCounts, int indexA, int indexB)
        {
            if (indexA == indexB)
                return;

            int min = indexA < indexB ? indexA : indexB;
            int max = indexA < indexB ? indexB : indexA;
            ulong key = ((ulong)(uint)min << 32) | (uint)max;

            if (edgeCounts.TryGetValue(key, out int count))
                edgeCounts[key] = count + 1;
            else
                edgeCounts[key] = 1;
        }
    }
}
