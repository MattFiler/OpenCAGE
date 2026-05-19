using CATHODE;
using CATHODE.ShaderTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace AlienPAK
{

    public static class MaterialApplier
    {
        private static readonly object _textureCacheLock = new object();
        private static readonly Dictionary<object, ImageSource> _textureImageCache = new Dictionary<object, ImageSource>();

        public static readonly DependencyProperty IsTransparentProperty = DependencyProperty.RegisterAttached("IsTransparent", typeof(bool), typeof(MaterialApplier), new PropertyMetadata(false));
        public static void SetIsTransparent(Model3D element, bool value) { element?.SetValue(IsTransparentProperty, value); }
        public static bool GetIsTransparent(Model3D element) { return element != null && (bool)element.GetValue(IsTransparentProperty); }

        public static Textures.TEX4 GetDiffuseTexture(Materials.Material material)
        {
            if (material == null || material.Shader == null) return null;
            int diffuseMap = GetDiffuseMapSamplerIndex(material.Shader);
            if (diffuseMap == -1 || diffuseMap >= material.Shader.SamplerRemaps.Count) return null;
            int diffuseMapIndex = material.Shader.SamplerRemaps[diffuseMap];
            if (diffuseMapIndex == 255 || diffuseMapIndex >= material.TextureReferences.Count) return null;
            return material.TextureReferences[diffuseMapIndex]?.Texture;
        }

        public static Textures.TEX4 GetNormalMapTexture(Materials.Material material)
        {
            if (material == null || material.Shader == null) return null;
            int normalMap = GetNormalMapSamplerIndex(material.Shader);
            if (normalMap == -1 || normalMap >= material.Shader.SamplerRemaps.Count) return null;
            int normalMapIndex = material.Shader.SamplerRemaps[normalMap];
            if (normalMapIndex == 255 || normalMapIndex >= material.TextureReferences.Count) return null;
            return material.TextureReferences[normalMapIndex]?.Texture;
        }

        public static void GetDiffuseTintForExport(Materials.Material material, out float r, out float g, out float b)
        {
            r = g = b = 1.0f;
            if (material == null || material.Shader == null) return;
            System.Windows.Media.Color c = GetDiffuseTint(material);
            r = c.R / 255f;
            g = c.G / 255f;
            b = c.B / 255f;
        }

        private static int GetDiffuseMapSamplerIndex(Shaders.Shader shader)
        {
            switch (shader.Ubershader)
            {
                case SHADER_LIST.CA_ENVIRONMENT: return (int)CA_ENVIRONMENT.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_DECAL_ENVIRONMENT: return (int)CA_DECAL_ENVIRONMENT.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_CHARACTER: return (int)CA_CHARACTER.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_SKIN: return (int)CA_SKIN.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_HAIR: return (int)CA_HAIR.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_EYE: return (int)CA_EYE.SAMPLERS.IRIS_MAP;
                case SHADER_LIST.CA_SKIN_OCCLUSION: return (int)CA_SKIN_OCCLUSION.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_DECAL: return (int)CA_DECAL.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_FOGPLANE: return (int)CA_FOGPLANE.SAMPLERS.DIFFUSE_MAP_0;
                case SHADER_LIST.CA_EFFECT: return (int)CA_EFFECT.SAMPLERS.DIFFUSE_MAP_0;
                case SHADER_LIST.CA_LIQUID_ENVIRONMENT: return (int)CA_LIQUID_ENVIRONMENT.SAMPLERS.LIQUIFLOW_DISTORTION_MAP;
                case SHADER_LIST.CA_LIQUID_CHARACTER: return (int)CA_LIQUID_CHARACTER.SAMPLERS.LIQUIFLOW_DISTORTION_MAP;
                case SHADER_LIST.CA_SKYDOME: return (int)CA_SKYDOME.SAMPLERS.SKYDOME_MAP;
                case SHADER_LIST.CA_SURFACE_EFFECTS: return (int)CA_SURFACE_EFFECTS.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_EFFECT_OVERLAY: return (int)CA_EFFECT_OVERLAY.SAMPLERS.TEXTURE_MAP;
                case SHADER_LIST.CA_TERRAIN: return (int)CA_TERRAIN.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_PLANET: return (int)CA_PLANET.SAMPLERS.DETAIL_MAP;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT: return (int)CA_LIGHTMAP_ENVIRONMENT.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_STREAMER: return (int)CA_STREAMER.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_LOW_LOD_CHARACTER: return (int)CA_LOW_LOD_CHARACTER.SAMPLERS.DIFFUSE_MAP;
                case SHADER_LIST.CA_CAMERA_MAP: return (int)CA_CAMERA_MAP.SAMPLERS.DIFFUSE_MAP;
                default: return -1;
            }
        }

        private static int GetNormalMapSamplerIndex(Shaders.Shader shader)
        {
            switch (shader.Ubershader)
            {
                case SHADER_LIST.CA_ENVIRONMENT: return (int)CA_ENVIRONMENT.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_DECAL_ENVIRONMENT: return (int)CA_DECAL_ENVIRONMENT.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_CHARACTER: return (int)CA_CHARACTER.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_SKIN: return (int)CA_SKIN.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_HAIR: return (int)CA_HAIR.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_DECAL: return (int)CA_DECAL.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_SURFACE_EFFECTS: return (int)CA_SURFACE_EFFECTS.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_TERRAIN: return (int)CA_TERRAIN.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT: return (int)CA_LIGHTMAP_ENVIRONMENT.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_STREAMER: return (int)CA_STREAMER.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_LOW_LOD_CHARACTER: return (int)CA_LOW_LOD_CHARACTER.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_LIQUID_ENVIRONMENT: return (int)CA_LIQUID_ENVIRONMENT.SAMPLERS.NORMAL_MAP;
                case SHADER_LIST.CA_LIQUID_CHARACTER: return (int)CA_LIQUID_CHARACTER.SAMPLERS.NORMAL_MAP;
                default: return -1;
            }
        }

        public static void ApplyMaterial(GeometryModel3D geometryModel, Materials.Material material)
        {
            if (geometryModel == null || material == null || material.Shader == null)
                return;

            ImageBrush brush = GetDiffuseTextureBrush(material);

            if (brush != null)
            {
                System.Windows.Media.Color tintColor = GetDiffuseTint(material);
                if (!IsColorTransparentOrWhite(tintColor))
                {
                    ImageSource tintedSource = ApplyTintToImageSource(brush.ImageSource, tintColor);
                    if (tintedSource != null)
                    {
                        brush = new ImageBrush(tintedSource);
                        tintColor = System.Windows.Media.Colors.White;
                    }
                }

                float uvScale = GetDiffuseUvScale(material);
                if (geometryModel.Geometry is MeshGeometry3D meshGeometry && meshGeometry.TextureCoordinates != null)
                {
                    PointCollection scaledUVs = new PointCollection();
                    foreach (System.Windows.Point uv in meshGeometry.TextureCoordinates)
                    {
                        scaledUVs.Add(new System.Windows.Point(uv.X * uvScale, uv.Y * uvScale));
                    }
                    meshGeometry.TextureCoordinates = scaledUVs;

                    brush.TileMode = TileMode.Tile;
                    brush.Viewport = new Rect(0, 0, 1, 1);
                    brush.ViewportUnits = BrushMappingMode.Absolute;
                }

                bool hasAlphaBlending = HasAlphaBlendingEnabled(material.Shader) || ImageSourceHasTransparency(brush.ImageSource);
                if (!hasAlphaBlending)
                {
                    brush.Opacity = 1.0;
                }
                SetIsTransparent(geometryModel, hasAlphaBlending);

                Material mat = CreateMaterialWithEffects(brush, material, tintColor);
                SetMaterialsWithBackface(geometryModel, mat, hasAlphaBlending);

                ImageBrush normalMapBrush = GetNormalMapTextureBrush(material);
                if (normalMapBrush != null)
                {
                    normalMapBrush.TileMode = TileMode.Tile;
                    normalMapBrush.Viewport = new Rect(0, 0, 1, 1);
                    normalMapBrush.ViewportUnits = BrushMappingMode.Absolute;
                    SetMaterialTextureBrushes(geometryModel, new MaterialTextureBrushes { DiffuseBrush = brush, NormalMapBrush = normalMapBrush });
                }
            }
            else
            {
                SetIsTransparent(geometryModel, false);
            }
        }

        public class MaterialTextureBrushes
        {
            public ImageBrush DiffuseBrush { get; set; }
            public ImageBrush NormalMapBrush { get; set; }
        }

        public static readonly DependencyProperty MaterialTextureBrushesProperty = DependencyProperty.RegisterAttached("MaterialTextureBrushes", typeof(MaterialTextureBrushes), typeof(MaterialApplier), new PropertyMetadata(null));
        public static void SetMaterialTextureBrushes(Model3D element, MaterialTextureBrushes value) { element?.SetValue(MaterialTextureBrushesProperty, value); }
        public static MaterialTextureBrushes GetMaterialTextureBrushes(Model3D element) { return (MaterialTextureBrushes)(element?.GetValue(MaterialTextureBrushesProperty)); }

        public static void ClearTextureCache()
        {
            lock (_textureCacheLock)
                _textureImageCache.Clear();
        }

        public static ImageBrush GetNormalMapTextureBrush(Materials.Material material)
        {
            Textures.TEX4 tex = GetNormalMapTexture(material);
            ImageSource imageSource = GetCachedTextureImage(tex);
            return imageSource != null ? new ImageBrush(imageSource) : null;
        }

        private static ImageBrush GetDiffuseTextureBrush(Materials.Material material)
        {
            Textures.TEX4 tex = GetDiffuseTexture(material);
            ImageSource imageSource = GetCachedTextureImage(tex);
            return imageSource != null ? new ImageBrush(imageSource) : null;
        }

        private static ImageSource GetCachedTextureImage(Textures.TEX4 tex)
        {
            if (tex == null)
                return null;

            lock (_textureCacheLock)
            {
                if (_textureImageCache.TryGetValue(tex, out ImageSource cached))
                    return cached;

                ImageSource imageSource = tex.ToDDS()?.ToBitmap()?.ToImageSource();
                if (imageSource != null && imageSource.CanFreeze)
                    imageSource.Freeze();

                _textureImageCache[tex] = imageSource;
                return imageSource;
            }
        }

        private static ImageSource ApplyTintToImageSource(ImageSource source, System.Windows.Media.Color tint)
        {
            if (source == null) return null;
            BitmapSource bitmap = source as BitmapSource;
            if (bitmap == null) return source;

            if (bitmap.Format != System.Windows.Media.PixelFormats.Bgra32)
            {
                var converted = new FormatConvertedBitmap(bitmap, System.Windows.Media.PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                bitmap = converted;
            }

            int w = bitmap.PixelWidth;
            int h = bitmap.PixelHeight;
            int stride = (w * 4 + 3) & ~3;
            byte[] pixels = new byte[h * stride];
            bitmap.CopyPixels(pixels, stride, 0);

            float tr = tint.R / 255f, tg = tint.G / 255f, tb = tint.B / 255f, ta = tint.A / 255f;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = (byte)Math.Max(0, Math.Min(255, (int)(pixels[i] * tb)));
                pixels[i + 1] = (byte)Math.Max(0, Math.Min(255, (int)(pixels[i + 1] * tg)));
                pixels[i + 2] = (byte)Math.Max(0, Math.Min(255, (int)(pixels[i + 2] * tr)));
                pixels[i + 3] = (byte)Math.Max(0, Math.Min(255, (int)(pixels[i + 3] * ta)));
            }

            var result = new WriteableBitmap(w, h, bitmap.DpiX, bitmap.DpiY, System.Windows.Media.PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
            result.Freeze();
            return result;
        }

        private static float GetDiffuseUvScale(Materials.Material material)
        {
            int diffuseUvMult = -1;
            switch (material.Shader.Ubershader)
            {
                case SHADER_LIST.CA_ENVIRONMENT:
                    diffuseUvMult = (int)CA_ENVIRONMENT.PARAMETERS.DIFFUSE_UV_MULT;
                    break;
                case SHADER_LIST.CA_DECAL_ENVIRONMENT:
                    diffuseUvMult = (int)CA_DECAL_ENVIRONMENT.PARAMETERS.DIFFUSE_UV_MULT;
                    break;
                case SHADER_LIST.CA_CHARACTER:
                    diffuseUvMult = (int)CA_CHARACTER.PARAMETERS.DIFFUSE_UV_MULT;
                    break;
                case SHADER_LIST.CA_SKIN:
                    diffuseUvMult = (int)CA_SKIN.PARAMETERS.DIFFUSE_UV_MULT;
                    break;
                case SHADER_LIST.CA_HAIR:
                    diffuseUvMult = (int)CA_HAIR.PARAMETERS.DIFFUSE_UV_MULT;
                    break;
                case SHADER_LIST.CA_SKIN_OCCLUSION:
                    diffuseUvMult = (int)CA_SKIN_OCCLUSION.PARAMETERS.DIFFUSE_UV_MULT;
                    break;
                case SHADER_LIST.CA_SURFACE_EFFECTS:
                    diffuseUvMult = (int)CA_SURFACE_EFFECTS.PARAMETERS.DIFFUSE_UV_MULT;
                    break;
                case SHADER_LIST.CA_TERRAIN:
                    diffuseUvMult = (int)CA_TERRAIN.PARAMETERS.DIFFUSE_UV_MULT;
                    break;
                case SHADER_LIST.CA_PLANET:
                    diffuseUvMult = (int)CA_PLANET.PARAMETERS.DETAIL_TEX_SCALAR;
                    break;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT:
                    diffuseUvMult = (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.DIFFUSE_UV_MULT;
                    break;
                case SHADER_LIST.CA_STREAMER:
                    diffuseUvMult = (int)CA_STREAMER.PARAMETERS.DIFFUSE_UV_MULT;
                    break;
                case SHADER_LIST.CA_LOW_LOD_CHARACTER:
                    diffuseUvMult = (int)CA_LOW_LOD_CHARACTER.PARAMETERS.DIFFUSE_UV_MULT;
                    break;
            }

            if (diffuseUvMult != -1 && diffuseUvMult < material.Shader.PixelShaderParameterRemaps.Count)
            {
                int remappedIndex = material.Shader.PixelShaderParameterRemaps[diffuseUvMult];
                if (remappedIndex != 255 && remappedIndex < material.PixelShaderConstants.Count)
                {
                    return material.PixelShaderConstants[remappedIndex];
                }
            }

            return 1.0f;
        }

        private static System.Windows.Media.Color GetDiffuseTint(Materials.Material material)
        {
            int diffuseTint = -1;
            switch (material.Shader.Ubershader)
            {
                case SHADER_LIST.CA_ENVIRONMENT:
                    diffuseTint = (int)CA_ENVIRONMENT.PARAMETERS.DIFFUSE_TINT;
                    break;
                case SHADER_LIST.CA_DECAL_ENVIRONMENT:
                    diffuseTint = (int)CA_DECAL_ENVIRONMENT.PARAMETERS.DIFFUSE_TINT;
                    break;
                case SHADER_LIST.CA_CHARACTER:
                    diffuseTint = (int)CA_CHARACTER.PARAMETERS.DIFFUSE_TINT;
                    break;
                case SHADER_LIST.CA_SKIN:
                    diffuseTint = (int)CA_SKIN.PARAMETERS.DIFFUSE_TINT;
                    break;
                case SHADER_LIST.CA_HAIR:
                    diffuseTint = (int)CA_HAIR.PARAMETERS.DIFFUSE_TINT;
                    break;
                case SHADER_LIST.CA_SKIN_OCCLUSION:
                    diffuseTint = (int)CA_SKIN_OCCLUSION.PARAMETERS.DIFFUSE_TINT;
                    break;
                case SHADER_LIST.CA_SURFACE_EFFECTS:
                    diffuseTint = (int)CA_SURFACE_EFFECTS.PARAMETERS.DIFFUSE_TINT;
                    break;
                case SHADER_LIST.CA_TERRAIN:
                    diffuseTint = (int)CA_TERRAIN.PARAMETERS.DIFFUSE_TINT;
                    break;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT:
                    diffuseTint = (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.DIFFUSE_TINT;
                    break;
                case SHADER_LIST.CA_STREAMER:
                    diffuseTint = (int)CA_STREAMER.PARAMETERS.DIFFUSE_TINT;
                    break;
                case SHADER_LIST.CA_LOW_LOD_CHARACTER:
                    diffuseTint = (int)CA_LOW_LOD_CHARACTER.PARAMETERS.DIFFUSE_TINT;
                    break;
                case SHADER_LIST.CA_EFFECT:
                    diffuseTint = (int)CA_EFFECT.PARAMETERS.COLOUR_TINT;
                    break;
            }

            if (diffuseTint != -1 && diffuseTint < material.Shader.PixelShaderParameterRemaps.Count)
            {
                int remappedIndex = material.Shader.PixelShaderParameterRemaps[diffuseTint];
                if (remappedIndex != 255 && remappedIndex < material.PixelShaderConstants.Count)
                {
                    UberShaderParameterType? parameterType = ShaderUtility.GetParameterType(material.Shader.Ubershader, "DIFFUSE_TINT");
                    if (parameterType == null && material.Shader.Ubershader == SHADER_LIST.CA_EFFECT)
                        parameterType = ShaderUtility.GetParameterType(material.Shader.Ubershader, "COLOUR_TINT");

                    if (parameterType.HasValue)
                    {
                        float r = 0, g = 0, b = 0, a = 1.0f;

                        switch (parameterType.Value)
                        {
                            case UberShaderParameterType.Float3:
                            case UberShaderParameterType.Half3:
                                if (remappedIndex < material.PixelShaderConstants.Count)
                                    r = material.PixelShaderConstants[remappedIndex];
                                if (remappedIndex + 1 < material.PixelShaderConstants.Count)
                                    g = material.PixelShaderConstants[remappedIndex + 1];
                                if (remappedIndex + 2 < material.PixelShaderConstants.Count)
                                    b = material.PixelShaderConstants[remappedIndex + 2];
                                break;
                            case UberShaderParameterType.Float4:
                            case UberShaderParameterType.Half4:
                                if (remappedIndex < material.PixelShaderConstants.Count)
                                    r = material.PixelShaderConstants[remappedIndex];
                                if (remappedIndex + 1 < material.PixelShaderConstants.Count)
                                    g = material.PixelShaderConstants[remappedIndex + 1];
                                if (remappedIndex + 2 < material.PixelShaderConstants.Count)
                                    b = material.PixelShaderConstants[remappedIndex + 2];
                                if (remappedIndex + 3 < material.PixelShaderConstants.Count)
                                    a = material.PixelShaderConstants[remappedIndex + 3];
                                break;
                        }

                        r = Math.Max(0, Math.Min(1, r));
                        g = Math.Max(0, Math.Min(1, g));
                        b = Math.Max(0, Math.Min(1, b));
                        a = Math.Max(0, Math.Min(1, a));

                        return System.Windows.Media.Color.FromArgb(
                            (byte)(a * 255),
                            (byte)(r * 255),
                            (byte)(g * 255),
                            (byte)(b * 255)
                        );
                    }
                }
            }

            return System.Windows.Media.Colors.White;
        }

        private static Material CreateMaterialWithEffects(ImageBrush brush, Materials.Material material, System.Windows.Media.Color diffuseTintColor)
        {
            DiffuseMaterial diffuseMat = new DiffuseMaterial(brush);
            diffuseMat.AmbientColor = System.Windows.Media.Colors.White;

            bool needsTinting = !IsColorTransparentOrWhite(diffuseTintColor);
            System.Windows.Media.Color emissiveTintColor = GetEmissiveTint(material);
            float emissiveMult = GetEmissiveMult(material);
            bool needsEmissive = !IsColorTransparentOrWhite(emissiveTintColor) || emissiveMult > 0.1f;

            if (!needsTinting && !needsEmissive)
                return diffuseMat;

            MaterialGroup materialGroup = new MaterialGroup();
            materialGroup.Children.Add(diffuseMat);

            if (needsTinting)
            {
                float tintIntensity = 0.15f;
                System.Windows.Media.Color emissiveTint = System.Windows.Media.Color.FromArgb(
                    (byte)(diffuseTintColor.A * 0.5f),
                    (byte)Math.Min(255, (int)(diffuseTintColor.R * tintIntensity)),
                    (byte)Math.Min(255, (int)(diffuseTintColor.G * tintIntensity)),
                    (byte)Math.Min(255, (int)(diffuseTintColor.B * tintIntensity))
                );

                EmissiveMaterial tintMaterial = new EmissiveMaterial(new SolidColorBrush(emissiveTint));
                materialGroup.Children.Add(tintMaterial);
            }

            if (needsEmissive)
            {
                if (IsColorTransparentOrWhite(emissiveTintColor))
                    emissiveTintColor = System.Windows.Media.Colors.White;

                if (emissiveMult <= 0)
                    emissiveMult = 1.0f;

                emissiveMult = Math.Min(emissiveMult, 3.0f);

                System.Windows.Media.Color finalEmissive = System.Windows.Media.Color.FromArgb(
                    emissiveTintColor.A,
                    (byte)Math.Min(255, (int)(emissiveTintColor.R * emissiveMult * 0.5f)),
                    (byte)Math.Min(255, (int)(emissiveTintColor.G * emissiveMult * 0.5f)),
                    (byte)Math.Min(255, (int)(emissiveTintColor.B * emissiveMult * 0.5f))
                );

                EmissiveMaterial emissiveMaterial = new EmissiveMaterial(new SolidColorBrush(finalEmissive));
                materialGroup.Children.Add(emissiveMaterial);
            }

            return materialGroup;
        }

        private static void SetMaterialsWithBackface(GeometryModel3D geometryModel, Material frontMaterial, bool isTransparent)
        {
            geometryModel.Material = frontMaterial;

            if (isTransparent)
            {
                geometryModel.BackMaterial = null;
                return;
            }

            if (frontMaterial is DiffuseMaterial frontDiffuse)
            {
                DiffuseMaterial backMaterial = new DiffuseMaterial(frontDiffuse.Brush);
                backMaterial.AmbientColor = frontDiffuse.AmbientColor;
                geometryModel.BackMaterial = backMaterial;
            }
            else if (frontMaterial is MaterialGroup frontGroup)
            {
                foreach (Material mat in frontGroup.Children)
                {
                    if (mat is DiffuseMaterial dm)
                    {
                        DiffuseMaterial backMaterial = new DiffuseMaterial(dm.Brush);
                        backMaterial.AmbientColor = dm.AmbientColor;
                        geometryModel.BackMaterial = backMaterial;
                        break;
                    }
                }
            }
        }

        private static bool IsColorTransparentOrWhite(System.Windows.Media.Color color)
        {
            return (color.R == 255 && color.G == 255 && color.B == 255 && color.A == 255) ||
                   (color.A == 0 && color.R == 0 && color.G == 0 && color.B == 0);
        }

        private static float GetEmissiveMult(Materials.Material material)
        {
            int emissiveMult = -1;
            switch (material.Shader.Ubershader)
            {
                case SHADER_LIST.CA_ENVIRONMENT:
                    emissiveMult = (int)CA_ENVIRONMENT.PARAMETERS.EMISSIVE_MULT;
                    break;
                case SHADER_LIST.CA_CHARACTER:
                    emissiveMult = (int)CA_CHARACTER.PARAMETERS.EMISSIVE_MULT;
                    break;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT:
                    emissiveMult = (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.EMISSIVE_MULT;
                    break;
            }

            if (emissiveMult != -1 && emissiveMult < material.Shader.PixelShaderParameterRemaps.Count)
            {
                int remappedIndex = material.Shader.PixelShaderParameterRemaps[emissiveMult];
                if (remappedIndex != 255 && remappedIndex < material.PixelShaderConstants.Count)
                {
                    return material.PixelShaderConstants[remappedIndex];
                }
            }

            return 0;
        }

        private static bool HasAlphaBlendingEnabled(Shaders.Shader shader)
        {
            if (shader == null)
                return false;

            if ((shader.UbershaderRequirementFlags & (1L << (int)SHADER_REQUIREMENTS.FORCE_TO_ALPHA)) != 0 ||
                (shader.UbershaderRequirementFlags & (1L << (int)SHADER_REQUIREMENTS.EARLY_ALPHA)) != 0 ||
                (shader.UbershaderRequirementFlags & (1L << (int)SHADER_REQUIREMENTS.POST_ALPHA)) != 0 ||
                (shader.UbershaderRequirementFlags & (1L << (int)SHADER_REQUIREMENTS.LOWRES_ALPHA)) != 0 ||
                (shader.UbershaderRequirementFlags & (1L << (int)SHADER_REQUIREMENTS.FORCE_TO_HI_ALPHA)) != 0)
            {
                return true;
            }

            int? useAlphaFeatureIndex = ShaderUtility.GetShaderFunctionalityIndex(shader.Ubershader, ShaderIndexType.FEATURES, "USE_ALPHA_AS_BLENDFACTOR");
            if (useAlphaFeatureIndex.HasValue)
            {
                if ((shader.UbershaderFeatureFlags & (1L << useAlphaFeatureIndex.Value)) != 0)
                    return true;
            }

            int? forceToAlphaFeatureIndex = ShaderUtility.GetShaderFunctionalityIndex(shader.Ubershader, ShaderIndexType.FEATURES, "FORCE_TO_ALPHA");
            if (forceToAlphaFeatureIndex.HasValue)
            {
                if ((shader.UbershaderFeatureFlags & (1L << forceToAlphaFeatureIndex.Value)) != 0)
                    return true;
            }

            return false;
        }

        //bit of a bodge - figure out if the image has any transparency to render it nicely
        private static bool ImageSourceHasTransparency(ImageSource source)
        {
            BitmapSource bitmap = source as BitmapSource;
            if (bitmap == null)
                return false;

            if (bitmap.Format != PixelFormats.Bgra32 && bitmap.Format != PixelFormats.Pbgra32)
            {
                bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                bitmap.Freeze();
            }

            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            if (width <= 0 || height <= 0)
                return false;

            int stride = ((width * 32 + 31) / 32) * 4;
            byte[] pixels = new byte[stride * height];
            bitmap.CopyPixels(pixels, stride, 0);

            int tested = 0;
            int alphaPixels = 0;
            byte minAlpha = byte.MaxValue;

            for (int i = 3; i < pixels.Length; i += 16)
            {
                tested++;
                byte a = pixels[i];
                if (a < minAlpha)
                    minAlpha = a;
                if (a < 250)
                    alphaPixels++;
            }

            if (tested == 0)
                return false;

            if (minAlpha <= 16)
                return true;

            return alphaPixels * 200 >= tested;
        }

        private static System.Windows.Media.Color GetEmissiveTint(Materials.Material material)
        {
            int emissiveTint = -1;
            switch (material.Shader.Ubershader)
            {
                case SHADER_LIST.CA_ENVIRONMENT:
                    emissiveTint = (int)CA_ENVIRONMENT.PARAMETERS.EMISSIVE_TINT;
                    break;
                case SHADER_LIST.CA_CHARACTER:
                    emissiveTint = (int)CA_CHARACTER.PARAMETERS.EMISSIVE_TINT;
                    break;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT:
                    emissiveTint = (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.EMISSIVE_TINT;
                    break;
            }

            if (emissiveTint != -1 && emissiveTint < material.Shader.PixelShaderParameterRemaps.Count)
            {
                int remappedIndex = material.Shader.PixelShaderParameterRemaps[emissiveTint];
                if (remappedIndex != 255 && remappedIndex < material.PixelShaderConstants.Count)
                {
                    UberShaderParameterType? parameterType = ShaderUtility.GetParameterType(material.Shader.Ubershader, "EMISSIVE_TINT");
                    if (parameterType.HasValue)
                    {
                        float r = 0, g = 0, b = 0, a = 1.0f;

                        switch (parameterType.Value)
                        {
                            case UberShaderParameterType.Float3:
                            case UberShaderParameterType.Half3:
                                if (remappedIndex < material.PixelShaderConstants.Count)
                                    r = material.PixelShaderConstants[remappedIndex];
                                if (remappedIndex + 1 < material.PixelShaderConstants.Count)
                                    g = material.PixelShaderConstants[remappedIndex + 1];
                                if (remappedIndex + 2 < material.PixelShaderConstants.Count)
                                    b = material.PixelShaderConstants[remappedIndex + 2];
                                break;
                            case UberShaderParameterType.Float4:
                            case UberShaderParameterType.Half4:
                                if (remappedIndex < material.PixelShaderConstants.Count)
                                    r = material.PixelShaderConstants[remappedIndex];
                                if (remappedIndex + 1 < material.PixelShaderConstants.Count)
                                    g = material.PixelShaderConstants[remappedIndex + 1];
                                if (remappedIndex + 2 < material.PixelShaderConstants.Count)
                                    b = material.PixelShaderConstants[remappedIndex + 2];
                                if (remappedIndex + 3 < material.PixelShaderConstants.Count)
                                    a = material.PixelShaderConstants[remappedIndex + 3];
                                break;
                        }

                        r = Math.Max(0, Math.Min(1, r));
                        g = Math.Max(0, Math.Min(1, g));
                        b = Math.Max(0, Math.Min(1, b));
                        a = Math.Max(0, Math.Min(1, a));

                        return System.Windows.Media.Color.FromArgb(
                            (byte)(a * 255),
                            (byte)(r * 255),
                            (byte)(g * 255),
                            (byte)(b * 255)
                        );
                    }
                }
            }

            return System.Windows.Media.Colors.Transparent;
        }
    }
}
