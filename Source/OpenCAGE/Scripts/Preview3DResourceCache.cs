using AlienPAK;
using CATHODE;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using static CATHODE.Models.CS2.Component.LOD;

namespace OpenCAGE
{
    /// <summary>
    /// Caches converted preview meshes and materials for the composite 3D viewer.
    /// </summary>
    public static class Preview3DResourceCache
    {
        private static readonly object _sync = new object();
        private static readonly Dictionary<Submesh, MeshGeometry3D> _baseGeometry = new Dictionary<Submesh, MeshGeometry3D>();
        private static readonly Dictionary<PreviewMeshKey, CachedPreviewMesh> _previewMeshes = new Dictionary<PreviewMeshKey, CachedPreviewMesh>();

        static Preview3DResourceCache()
        {
            Singleton.OnLevelLoaded += _ => Clear();
        }

        public static GeometryModel3D GetGeometry(Submesh submesh, Materials.Material material, bool applyMaterials)
        {
            if (submesh == null)
                return new GeometryModel3D();

            if (!applyMaterials)
                return CreateGeometryOnlyInstance(submesh);

            PreviewMeshKey key = new PreviewMeshKey(submesh, material);
            lock (_sync)
            {
                if (!_previewMeshes.TryGetValue(key, out CachedPreviewMesh cached))
                {
                    MeshGeometry3D geometry = CloneMeshGeometry(GetBaseMeshGeometry(submesh));
                    GeometryModel3D template = new GeometryModel3D { Geometry = geometry };

                    if (material != null)
                        MaterialApplier.ApplyMaterial(template, material);
                    else
                    {
                        DiffuseMaterial fallback = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 255, 0)));
                        template.Material = fallback;
                        template.BackMaterial = fallback;
                    }

                    cached = new CachedPreviewMesh(template);
                    _previewMeshes[key] = cached;
                }

                return cached.CreateInstance();
            }
        }

        public static void Clear()
        {
            lock (_sync)
            {
                _baseGeometry.Clear();
                _previewMeshes.Clear();
            }

            MaterialApplier.ClearTextureCache();
        }

        private static GeometryModel3D CreateGeometryOnlyInstance(Submesh submesh)
        {
            return new GeometryModel3D
            {
                Geometry = CloneMeshGeometry(GetBaseMeshGeometry(submesh)),
                Material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 255, 0))),
                BackMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 255, 0))),
            };
        }

        private static MeshGeometry3D GetBaseMeshGeometry(Submesh submesh)
        {
            lock (_sync)
            {
                if (_baseGeometry.TryGetValue(submesh, out MeshGeometry3D cached))
                    return cached;

                GeometryModel3D built = submesh.ToGeometryModel3D(applyMaterials: false);
                MeshGeometry3D geometry = built.Geometry as MeshGeometry3D;
                if (geometry == null)
                {
                    geometry = new MeshGeometry3D();
                }
                else if (geometry.CanFreeze)
                {
                    geometry.Freeze();
                }

                _baseGeometry[submesh] = geometry;
                return geometry;
            }
        }

        private static MeshGeometry3D CloneMeshGeometry(MeshGeometry3D source)
        {
            if (source == null)
                return new MeshGeometry3D();

            PointCollection uvs = null;
            if (source.TextureCoordinates != null && source.TextureCoordinates.Count > 0)
                uvs = new PointCollection(source.TextureCoordinates);

            MeshGeometry3D clone = new MeshGeometry3D
            {
                Positions = source.Positions,
                TriangleIndices = source.TriangleIndices,
                TextureCoordinates = uvs,
            };

            return clone;
        }

        private sealed class PreviewMeshKey
        {
            public readonly Submesh Submesh;
            public readonly Materials.Material Material;

            public PreviewMeshKey(Submesh submesh, Materials.Material material)
            {
                Submesh = submesh;
                Material = material;
            }

            public override bool Equals(object obj)
            {
                PreviewMeshKey other = obj as PreviewMeshKey;
                if (other == null)
                    return false;

                return ReferenceEquals(Submesh, other.Submesh)
                    && ReferenceEquals(Material, other.Material);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + RuntimeHelpers.GetHashCode(Submesh);
                    hash = hash * 31 + RuntimeHelpers.GetHashCode(Material);
                    return hash;
                }
            }
        }

        private sealed class CachedPreviewMesh
        {
            private readonly MeshGeometry3D _geometry;
            private readonly Material _material;
            private readonly Material _backMaterial;
            private readonly bool _isTransparent;
            private readonly MaterialApplier.MaterialTextureBrushes _textureBrushes;

            public CachedPreviewMesh(GeometryModel3D template)
            {
                _geometry = template.Geometry as MeshGeometry3D;
                _material = template.Material;
                _backMaterial = template.BackMaterial;
                _isTransparent = MaterialApplier.GetIsTransparent(template);
                _textureBrushes = MaterialApplier.GetMaterialTextureBrushes(template);

                if (_geometry != null && _geometry.CanFreeze)
                    _geometry.Freeze();
            }

            public GeometryModel3D CreateInstance()
            {
                GeometryModel3D instance = new GeometryModel3D
                {
                    Geometry = _geometry,
                    Material = _material,
                    BackMaterial = _backMaterial,
                };

                MaterialApplier.SetIsTransparent(instance, _isTransparent);
                if (_textureBrushes != null)
                    MaterialApplier.SetMaterialTextureBrushes(instance, _textureBrushes);

                return instance;
            }
        }
    }
}
