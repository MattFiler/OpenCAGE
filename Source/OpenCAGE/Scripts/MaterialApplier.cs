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
            return GetSamplerTexture(material, GetNormalMapSamplerIndex);
        }

        public static Textures.TEX4 GetSpecularMapTexture(Materials.Material material)
        {
            return GetSamplerTexture(material, GetSpecularMapSamplerIndex);
        }

        public static Textures.TEX4 GetDirtMapTexture(Materials.Material material)
        {
            return GetSamplerTexture(material, GetDirtMapSamplerIndex);
        }

        public static Textures.TEX4 GetSecondarySpecularMapTexture(Materials.Material material)
        {
            return GetSamplerTexture(material, GetSecondarySpecularMapSamplerIndex);
        }

        private static Textures.TEX4 GetSamplerTexture(Materials.Material material, Func<Shaders.Shader, int> getSamplerIndex)
        {
            if (material == null || material.Shader == null) return null;
            int sampler = getSamplerIndex(material.Shader);
            if (sampler == -1 || sampler >= material.Shader.SamplerRemaps.Count) return null;
            int textureIndex = material.Shader.SamplerRemaps[sampler];
            if (textureIndex == 255 || textureIndex >= material.TextureReferences.Count) return null;
            return material.TextureReferences[textureIndex]?.Texture;
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

        private static int GetSpecularMapSamplerIndex(Shaders.Shader shader)
        {
            switch (shader.Ubershader)
            {
                case SHADER_LIST.CA_ENVIRONMENT: return (int)CA_ENVIRONMENT.SAMPLERS.SPECULAR_MAP;
                case SHADER_LIST.CA_DECAL_ENVIRONMENT: return (int)CA_DECAL_ENVIRONMENT.SAMPLERS.SPECULAR_MAP;
                case SHADER_LIST.CA_CHARACTER: return (int)CA_CHARACTER.SAMPLERS.SPECULAR_MAP;
                case SHADER_LIST.CA_SKIN: return (int)CA_SKIN.SAMPLERS.SPECULAR_MAP;
                case SHADER_LIST.CA_HAIR: return (int)CA_HAIR.SAMPLERS.SPECULAR_MAP;
                case SHADER_LIST.CA_DECAL: return (int)CA_DECAL.SAMPLERS.SPECULAR_MAP;
                case SHADER_LIST.CA_SURFACE_EFFECTS: return (int)CA_SURFACE_EFFECTS.SAMPLERS.SPECULAR_MAP;
                case SHADER_LIST.CA_TERRAIN: return (int)CA_TERRAIN.SAMPLERS.SPECULAR_MAP;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT: return (int)CA_LIGHTMAP_ENVIRONMENT.SAMPLERS.SPECULAR_MAP;
                case SHADER_LIST.CA_STREAMER: return (int)CA_STREAMER.SAMPLERS.SPECULAR_MAP;
                case SHADER_LIST.CA_LOW_LOD_CHARACTER: return (int)CA_LOW_LOD_CHARACTER.SAMPLERS.SPECULAR_MAP;
                default: return -1;
            }
        }

        private static int GetDirtMapSamplerIndex(Shaders.Shader shader)
        {
            switch (shader.Ubershader)
            {
                case SHADER_LIST.CA_ENVIRONMENT: return (int)CA_ENVIRONMENT.SAMPLERS.DIRT_MAP;
                case SHADER_LIST.CA_DECAL_ENVIRONMENT: return (int)CA_DECAL_ENVIRONMENT.SAMPLERS.DIRT_MAP;
                case SHADER_LIST.CA_CHARACTER: return (int)CA_CHARACTER.SAMPLERS.DIRT_MAP;
                case SHADER_LIST.CA_SKIN: return (int)CA_SKIN.SAMPLERS.DIRT_MAP;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT: return (int)CA_LIGHTMAP_ENVIRONMENT.SAMPLERS.DIRT_MAP;
                case SHADER_LIST.CA_STREAMER: return (int)CA_STREAMER.SAMPLERS.DIRT_MAP;
                case SHADER_LIST.CA_SPACESUIT_VISOR: return (int)CA_SPACESUIT_VISOR.SAMPLERS.DIRT_MAP;
                default: return -1;
            }
        }

        private static int GetSecondarySpecularMapSamplerIndex(Shaders.Shader shader)
        {
            switch (shader.Ubershader)
            {
                case SHADER_LIST.CA_ENVIRONMENT: return (int)CA_ENVIRONMENT.SAMPLERS.SECONDARY_SPECULAR_MAP;
                case SHADER_LIST.CA_DECAL_ENVIRONMENT: return (int)CA_DECAL_ENVIRONMENT.SAMPLERS.SECONDARY_SPECULAR_MAP;
                case SHADER_LIST.CA_CHARACTER: return (int)CA_CHARACTER.SAMPLERS.SECONDARY_SPECULAR_MAP;
                case SHADER_LIST.CA_SKIN: return (int)CA_SKIN.SAMPLERS.SECONDARY_SPECULAR_MAP;
                case SHADER_LIST.CA_TERRAIN: return (int)CA_TERRAIN.SAMPLERS.SECONDARY_SPECULAR_MAP;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT: return (int)CA_LIGHTMAP_ENVIRONMENT.SAMPLERS.SECONDARY_SPECULAR_MAP;
                case SHADER_LIST.CA_STREAMER: return (int)CA_STREAMER.SAMPLERS.SECONDARY_SPECULAR_MAP;
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

                ImageBrush rawDirtMapBrush = GetDirtMapTextureBrush(material);
                if (rawDirtMapBrush != null && HasDirtMappingEnabled(material.Shader))
                {
                    ImageSource dirtComposited = CompositeDirtOntoDiffuse(brush.ImageSource, rawDirtMapBrush.ImageSource, material);
                    if (dirtComposited != null)
                        brush = new ImageBrush(dirtComposited);
                }

                float uvScale = GetDiffuseUvScale(material);
                if (geometryModel.Geometry is MeshGeometry3D meshGeometry && meshGeometry.TextureCoordinates != null)
                {
                    if (uvScale != 1.0f)
                    {
                        PointCollection scaledUVs = new PointCollection();
                        foreach (System.Windows.Point uv in meshGeometry.TextureCoordinates)
                        {
                            scaledUVs.Add(new System.Windows.Point(uv.X * uvScale, uv.Y * uvScale));
                        }
                        meshGeometry.TextureCoordinates = scaledUVs;
                    }

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

                ImageBrush normalMapBrush = GetNormalMapTextureBrush(material);
                ImageBrush rawSpecularMapBrush = GetSpecularMapTextureBrush(material);

                if (rawSpecularMapBrush != null && HasSpecularMappingEnabled(material.Shader))
                {
                    ImageSource secondarySpecularSource = null;
                    if (HasSecondarySpecularMappingEnabled(material.Shader))
                    {
                        Textures.TEX4 secondarySpecularTex = GetSecondarySpecularMapTexture(material);
                        secondarySpecularSource = GetCachedTextureImage(secondarySpecularTex);
                    }

                    ImageSource specularAdjusted = ApplySpecularChannelsToDiffuse(
                        brush.ImageSource,
                        rawSpecularMapBrush.ImageSource,
                        material,
                        secondarySpecularSource);
                    if (specularAdjusted != null)
                    {
                        brush = new ImageBrush(specularAdjusted);
                        if (geometryModel.Geometry is MeshGeometry3D meshWithUvs && meshWithUvs.TextureCoordinates != null)
                            ConfigureTiledBrush(brush);
                    }
                }

                Material mat = CreateMaterialWithEffects(brush, material, tintColor);
                SetMaterialsWithBackface(geometryModel, mat, hasAlphaBlending);

                if (normalMapBrush != null || rawSpecularMapBrush != null || rawDirtMapBrush != null)
                {
                    ConfigureTiledBrush(normalMapBrush);
                    ConfigureTiledBrush(rawSpecularMapBrush);
                    ConfigureTiledBrush(rawDirtMapBrush);
                    SetMaterialTextureBrushes(geometryModel, new MaterialTextureBrushes
                    {
                        DiffuseBrush = brush,
                        NormalMapBrush = normalMapBrush,
                        SpecularMapBrush = rawSpecularMapBrush,
                        DirtMapBrush = rawDirtMapBrush,
                    });
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
            public ImageBrush SpecularMapBrush { get; set; }
            public ImageBrush DirtMapBrush { get; set; }
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
            return GetTextureBrush(GetNormalMapTexture(material));
        }

        public static ImageBrush GetSpecularMapTextureBrush(Materials.Material material)
        {
            return GetTextureBrush(GetSpecularMapTexture(material));
        }

        public static ImageBrush GetDirtMapTextureBrush(Materials.Material material)
        {
            return GetTextureBrush(GetDirtMapTexture(material));
        }

        private static ImageBrush GetTextureBrush(Textures.TEX4 tex)
        {
            ImageSource imageSource = GetCachedTextureImage(tex);
            return imageSource != null ? new ImageBrush(imageSource) : null;
        }

        private static void ConfigureTiledBrush(ImageBrush brush)
        {
            if (brush == null)
                return;

            brush.TileMode = TileMode.Tile;
            brush.Viewport = new Rect(0, 0, 1, 1);
            brush.ViewportUnits = BrushMappingMode.Absolute;
        }

        private static ImageBrush GetDiffuseTextureBrush(Materials.Material material)
        {
            return GetTextureBrush(GetDiffuseTexture(material));
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

        private static ImageSource CompositeDirtOntoDiffuse(ImageSource diffuseSource, ImageSource dirtSource, Materials.Material material)
        {
            BitmapSource diffuseBitmap = ToBgra32Bitmap(diffuseSource);
            BitmapSource dirtBitmap = ToBgra32Bitmap(dirtSource);
            if (diffuseBitmap == null || dirtBitmap == null)
                return null;

            int width = diffuseBitmap.PixelWidth;
            int height = diffuseBitmap.PixelHeight;
            if (width <= 0 || height <= 0)
                return null;

            int diffuseStride = (width * 4 + 3) & ~3;
            int dirtWidth = dirtBitmap.PixelWidth;
            int dirtHeight = dirtBitmap.PixelHeight;
            int dirtStride = (dirtWidth * 4 + 3) & ~3;

            byte[] diffusePixels = new byte[height * diffuseStride];
            byte[] dirtPixels = new byte[dirtHeight * dirtStride];
            byte[] output = new byte[height * diffuseStride];
            diffuseBitmap.CopyPixels(diffusePixels, diffuseStride, 0);
            dirtBitmap.CopyPixels(dirtPixels, dirtStride, 0);

            DirtPreviewSettings dirtSettings = GetDirtPreviewSettings(material);

            for (int y = 0; y < height; y++)
            {
                float v = height == 1 ? 0f : y / (float)(height - 1);
                float scaledV = v * dirtSettings.UvMult;
                scaledV -= (float)Math.Floor(scaledV);
                int dirtY = dirtHeight == height ? y : (int)(scaledV * (dirtHeight - 1));
                if (dirtY >= dirtHeight)
                    dirtY = dirtHeight - 1;

                for (int x = 0; x < width; x++)
                {
                    float u = width == 1 ? 0f : x / (float)(width - 1);
                    float scaledU = u * dirtSettings.UvMult;
                    scaledU -= (float)Math.Floor(scaledU);
                    int dirtX = dirtWidth == width ? x : (int)(scaledU * (dirtWidth - 1));
                    if (dirtX >= dirtWidth)
                        dirtX = dirtWidth - 1;

                    int diffuseIndex = y * diffuseStride + x * 4;
                    int dirtIndex = dirtY * dirtStride + dirtX * 4;

                    float dr = diffusePixels[diffuseIndex + 2] / 255f;
                    float dg = diffusePixels[diffuseIndex + 1] / 255f;
                    float db = diffusePixels[diffuseIndex] / 255f;
                    float da = diffusePixels[diffuseIndex + 3] / 255f;

                    float dirtR = dirtPixels[dirtIndex + 2] / 255f;
                    float dirtG = dirtPixels[dirtIndex + 1] / 255f;
                    float dirtB = dirtPixels[dirtIndex] / 255f;
                    float dirtAlpha = dirtPixels[dirtIndex + 3] / 255f;

                    // Red channel is typically AO/mask; alpha is coverage. DIRT_AO_AMOUNT weights the map AO.
                    float dirtAo = dirtR;
                    float blend = dirtAlpha * (1f - dirtSettings.AoAmount + dirtSettings.AoAmount * dirtAo);
                    blend = Math.Max(0f, Math.Min(1f, blend));

                    float outR, outG, outB;
                    if (dirtSettings.BlendMultiply)
                    {
                        if (Math.Abs(dirtSettings.BlendMultSpecPower - 1f) > 0.01f)
                            blend = (float)Math.Pow(blend, 1f / dirtSettings.BlendMultSpecPower);

                        outR = dr * (1f - blend + blend * dirtR);
                        outG = dg * (1f - blend + blend * dirtG);
                        outB = db * (1f - blend + blend * dirtB);
                    }
                    else
                    {
                        blend = Math.Max(0f, Math.Min(1f, blend * dirtSettings.BlendMultSpecPower));
                        // blend 0 = diffuse, blend 1 = dirt (was inverted, which washed out materials with empty dirt maps)
                        outR = dr + (dirtR - dr) * blend;
                        outG = dg + (dirtG - dg) * blend;
                        outB = db + (dirtB - db) * blend;
                    }

                    output[diffuseIndex] = (byte)Math.Max(0, Math.Min(255, outB * 255f));
                    output[diffuseIndex + 1] = (byte)Math.Max(0, Math.Min(255, outG * 255f));
                    output[diffuseIndex + 2] = (byte)Math.Max(0, Math.Min(255, outR * 255f));
                    output[diffuseIndex + 3] = (byte)Math.Max(0, Math.Min(255, da * 255f));
                }
            }

            var result = new WriteableBitmap(width, height, diffuseBitmap.DpiX, diffuseBitmap.DpiY, PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, width, height), output, diffuseStride, 0);
            result.Freeze();
            return result;
        }

        private struct DirtPreviewSettings
        {
            public float UvMult;
            public float AoAmount;
            public float BlendMultSpecPower;
            public bool BlendMultiply;
        }

        private static DirtPreviewSettings GetDirtPreviewSettings(Materials.Material material)
        {
            DirtPreviewSettings settings = new DirtPreviewSettings
            {
                UvMult = 1f,
                AoAmount = 1f,
                BlendMultSpecPower = 1f,
                BlendMultiply = HasDirtBlendMultiplyEnabled(material.Shader),
            };

            switch (material.Shader.Ubershader)
            {
                case SHADER_LIST.CA_ENVIRONMENT:
                    settings.UvMult = GetRemappedShaderFloat(material, (int)CA_ENVIRONMENT.PARAMETERS.DIRT_UV_MULT, 1f);
                    settings.AoAmount = GetRemappedShaderFloat(material, (int)CA_ENVIRONMENT.PARAMETERS.DIRT_AO_AMOUNT, 1f);
                    settings.BlendMultSpecPower = GetRemappedShaderFloat(material, (int)CA_ENVIRONMENT.PARAMETERS.DIRT_BLEND_MULT_SPEC_POWER, 1f);
                    break;
                case SHADER_LIST.CA_DECAL_ENVIRONMENT:
                    settings.UvMult = GetRemappedShaderFloat(material, (int)CA_DECAL_ENVIRONMENT.PARAMETERS.DIRT_UV_MULT, 1f);
                    settings.AoAmount = GetRemappedShaderFloat(material, (int)CA_DECAL_ENVIRONMENT.PARAMETERS.DIRT_AO_AMOUNT, 1f);
                    settings.BlendMultSpecPower = GetRemappedShaderFloat(material, (int)CA_DECAL_ENVIRONMENT.PARAMETERS.DIRT_BLEND_MULT_SPEC_POWER, 1f);
                    break;
                case SHADER_LIST.CA_CHARACTER:
                    settings.UvMult = GetRemappedShaderFloat(material, (int)CA_CHARACTER.PARAMETERS.DIRT_UV_MULT, 1f);
                    settings.AoAmount = GetRemappedShaderFloat(material, (int)CA_CHARACTER.PARAMETERS.DIRT_AO_AMOUNT, 1f);
                    settings.BlendMultSpecPower = GetRemappedShaderFloat(material, (int)CA_CHARACTER.PARAMETERS.DIRT_BLEND_MULT_SPEC_POWER, 1f);
                    break;
                case SHADER_LIST.CA_SKIN:
                    settings.UvMult = GetRemappedShaderFloat(material, (int)CA_SKIN.PARAMETERS.DIRT_UV_MULT, 1f);
                    settings.AoAmount = GetRemappedShaderFloat(material, (int)CA_SKIN.PARAMETERS.DIRT_AO_AMOUNT, 1f);
                    settings.BlendMultSpecPower = GetRemappedShaderFloat(material, (int)CA_SKIN.PARAMETERS.DIRT_BLEND_MULT_SPEC_POWER, 1f);
                    break;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT:
                    settings.UvMult = GetRemappedShaderFloat(material, (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.DIRT_UV_MULT, 1f);
                    settings.AoAmount = GetRemappedShaderFloat(material, (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.DIRT_AO_AMOUNT, 1f);
                    settings.BlendMultSpecPower = GetRemappedShaderFloat(material, (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.DIRT_BLEND_MULT_SPEC_POWER, 1f);
                    break;
                case SHADER_LIST.CA_STREAMER:
                    settings.UvMult = GetRemappedShaderFloat(material, (int)CA_STREAMER.PARAMETERS.DIRT_UV_MULT, 1f);
                    settings.AoAmount = GetRemappedShaderFloat(material, (int)CA_STREAMER.PARAMETERS.DIRT_AO_AMOUNT, 1f);
                    settings.BlendMultSpecPower = GetRemappedShaderFloat(material, (int)CA_STREAMER.PARAMETERS.DIRT_BLEND_MULT_SPEC_POWER, 1f);
                    break;
                case SHADER_LIST.CA_SPACESUIT_VISOR:
                    settings.UvMult = GetRemappedShaderFloat(material, (int)CA_SPACESUIT_VISOR.PARAMETERS.DIRT_UV_MULT, 1f);
                    settings.BlendMultSpecPower = GetRemappedShaderFloat(material, (int)CA_SPACESUIT_VISOR.PARAMETERS.DIRT_BLEND_MULT_SPEC_POWER, 1f);
                    break;
            }

            settings.UvMult = Math.Max(0.01f, settings.UvMult);
            settings.AoAmount = Math.Max(0f, Math.Min(1f, settings.AoAmount));
            settings.BlendMultSpecPower = Math.Max(0.01f, Math.Min(4f, settings.BlendMultSpecPower));

            return settings;
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

        /// <summary>Scales baked specular in WPF; shader constants drive the rest.</summary>
        private const float WpfSpecularPreviewIntensity = 0.35f;

        private struct SpecularPreviewSettings
        {
            public float SpecularPower;
            public float SpecularLevel;
            public float SecondarySpecularPower;
            public float SpecularUvMult;
            public float SecondarySpecularUvMult;
            public float SpecularGloss;
            public float SecondarySpecularGloss;
            public float DiffuseRoughness;
            public System.Windows.Media.Color SpecularTint;
            public System.Windows.Media.Color SecondarySpecularTint;
            public bool SecondarySpecularMapping;
            public bool SecondarySpecularBlendMultiply;
            public bool DiffuseRoughnessEnabled;
            public bool FrontRoughnessEnabled;
            public bool AdditiveRoughnessEnabled;
            public bool UseSpecularGlossParameter;

            public float RoughnessAttenuation
            {
                get
                {
                    float attenuation = 1f;
                    if (DiffuseRoughnessEnabled)
                        attenuation *= Math.Max(0f, 1f - Math.Min(1f, DiffuseRoughness) * 0.75f);
                    if (FrontRoughnessEnabled)
                        attenuation *= 0.7f;
                    if (AdditiveRoughnessEnabled)
                        attenuation *= 0.7f;
                    return attenuation;
                }
            }

            public float GetGlossExponent(bool secondary)
            {
                if (UseSpecularGlossParameter)
                    return 1f;

                return 32f / Math.Max(1f, secondary ? SecondarySpecularPower : SpecularPower);
            }

            public float GetHighlightScale(bool secondary) =>
                SpecularLevel * (secondary ? SecondarySpecularGloss : SpecularGloss) * RoughnessAttenuation;

            public float GetPreviewOpacity() =>
                Math.Max(0.05f, Math.Min(1f, WpfSpecularPreviewIntensity * SpecularLevel * Math.Max(0.1f, SpecularGloss) * RoughnessAttenuation));
        }

        private static float GetRemappedShaderFloat(Materials.Material material, int parameterIndex, float defaultValue)
        {
            if (material?.Shader == null || parameterIndex == -1 || parameterIndex >= material.Shader.PixelShaderParameterRemaps.Count)
                return defaultValue;

            int remappedIndex = material.Shader.PixelShaderParameterRemaps[parameterIndex];
            if (remappedIndex == 255 || remappedIndex >= material.PixelShaderConstants.Count)
                return defaultValue;

            return material.PixelShaderConstants[remappedIndex];
        }

        private static System.Windows.Media.Color GetShaderColorParameter(Materials.Material material, int parameterIndex, string parameterName, System.Windows.Media.Color defaultColor)
        {
            if (material?.Shader == null || parameterIndex == -1 || parameterIndex >= material.Shader.PixelShaderParameterRemaps.Count)
                return defaultColor;

            int remappedIndex = material.Shader.PixelShaderParameterRemaps[parameterIndex];
            if (remappedIndex == 255 || remappedIndex >= material.PixelShaderConstants.Count)
                return defaultColor;

            UberShaderParameterType? parameterType = ShaderUtility.GetParameterType(material.Shader.Ubershader, parameterName);
            if (!parameterType.HasValue)
                return defaultColor;

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
                (byte)(b * 255));
        }

        private static SpecularPreviewSettings GetSpecularPreviewSettings(Materials.Material material)
        {
            Shaders.Shader shader = material.Shader;
            SpecularPreviewSettings settings = new SpecularPreviewSettings
            {
                SpecularPower = 32f,
                SpecularLevel = 1f,
                SecondarySpecularPower = 32f,
                SpecularUvMult = 1f,
                SecondarySpecularUvMult = 1f,
                SpecularGloss = 1f,
                SecondarySpecularGloss = 1f,
                SpecularTint = System.Windows.Media.Colors.White,
                SecondarySpecularTint = System.Windows.Media.Colors.White,
                SecondarySpecularMapping = HasSecondarySpecularMappingEnabled(shader),
                SecondarySpecularBlendMultiply = HasSecondarySpecularBlendMultiplyEnabled(shader),
                DiffuseRoughnessEnabled = HasShaderFeature(shader, "DIFFUSE_ROUGHNESS"),
                FrontRoughnessEnabled = HasShaderFeature(shader, "FRONT_ROUGHNESS"),
                AdditiveRoughnessEnabled = HasShaderFeature(shader, "ADDITIVE_ROUGHNESS"),
                UseSpecularGlossParameter = shader.Ubershader == SHADER_LIST.CA_TERRAIN || HasShaderFeature(shader, "SPECULAR_GLOSS"),
            };

            switch (shader.Ubershader)
            {
                case SHADER_LIST.CA_ENVIRONMENT:
                    settings.SpecularPower = GetRemappedShaderFloat(material, (int)CA_ENVIRONMENT.PARAMETERS.SPECULAR_POWER, 32f);
                    settings.SecondarySpecularPower = GetRemappedShaderFloat(material, (int)CA_ENVIRONMENT.PARAMETERS.SECONDARY_SPECULAR_POWER, 32f);
                    settings.SpecularUvMult = GetRemappedShaderFloat(material, (int)CA_ENVIRONMENT.PARAMETERS.SPECULAR_UV_MULT, 1f);
                    settings.SecondarySpecularUvMult = GetRemappedShaderFloat(material, (int)CA_ENVIRONMENT.PARAMETERS.SECONDARY_SPECULAR_UV_MULT, 1f);
                    settings.DiffuseRoughness = GetRemappedShaderFloat(material, (int)CA_ENVIRONMENT.PARAMETERS.DIFFUSE_ROUGHNESS_FACTOR, 0f);
                    settings.SpecularTint = GetShaderColorParameter(material, (int)CA_ENVIRONMENT.PARAMETERS.SPECULAR_TINT, "SPECULAR_TINT", settings.SpecularTint);
                    settings.SecondarySpecularTint = GetShaderColorParameter(material, (int)CA_ENVIRONMENT.PARAMETERS.SECONDARY_SPECULAR_TINT, "SECONDARY_SPECULAR_TINT", settings.SecondarySpecularTint);
                    break;
                case SHADER_LIST.CA_DECAL_ENVIRONMENT:
                    settings.SpecularPower = GetRemappedShaderFloat(material, (int)CA_DECAL_ENVIRONMENT.PARAMETERS.SPECULAR_POWER, 32f);
                    settings.SecondarySpecularPower = GetRemappedShaderFloat(material, (int)CA_DECAL_ENVIRONMENT.PARAMETERS.SECONDARY_SPECULAR_POWER, 32f);
                    settings.SpecularUvMult = GetRemappedShaderFloat(material, (int)CA_DECAL_ENVIRONMENT.PARAMETERS.SPECULAR_UV_MULT, 1f);
                    settings.SecondarySpecularUvMult = GetRemappedShaderFloat(material, (int)CA_DECAL_ENVIRONMENT.PARAMETERS.SECONDARY_SPECULAR_UV_MULT, 1f);
                    settings.DiffuseRoughness = GetRemappedShaderFloat(material, (int)CA_DECAL_ENVIRONMENT.PARAMETERS.DIFFUSE_ROUGHNESS_FACTOR, 0f);
                    settings.SpecularTint = GetShaderColorParameter(material, (int)CA_DECAL_ENVIRONMENT.PARAMETERS.SPECULAR_TINT, "SPECULAR_TINT", settings.SpecularTint);
                    settings.SecondarySpecularTint = GetShaderColorParameter(material, (int)CA_DECAL_ENVIRONMENT.PARAMETERS.SECONDARY_SPECULAR_TINT, "SECONDARY_SPECULAR_TINT", settings.SecondarySpecularTint);
                    break;
                case SHADER_LIST.CA_CHARACTER:
                    settings.SpecularPower = GetRemappedShaderFloat(material, (int)CA_CHARACTER.PARAMETERS.SPECULAR_POWER, 32f);
                    settings.SecondarySpecularPower = GetRemappedShaderFloat(material, (int)CA_CHARACTER.PARAMETERS.SECONDARY_SPECULAR_POWER, 32f);
                    settings.SpecularUvMult = GetRemappedShaderFloat(material, (int)CA_CHARACTER.PARAMETERS.SPECULAR_UV_MULT, 1f);
                    settings.SecondarySpecularUvMult = GetRemappedShaderFloat(material, (int)CA_CHARACTER.PARAMETERS.SECONDARY_SPECULAR_UV_MULT, 1f);
                    settings.DiffuseRoughness = GetRemappedShaderFloat(material, (int)CA_CHARACTER.PARAMETERS.DIFFUSE_ROUGHNESS_FACTOR, 0f);
                    settings.SpecularTint = GetShaderColorParameter(material, (int)CA_CHARACTER.PARAMETERS.SPECULAR_TINT, "SPECULAR_TINT", settings.SpecularTint);
                    settings.SecondarySpecularTint = GetShaderColorParameter(material, (int)CA_CHARACTER.PARAMETERS.SECONDARY_SPECULAR_TINT, "SECONDARY_SPECULAR_TINT", settings.SecondarySpecularTint);
                    break;
                case SHADER_LIST.CA_SKIN:
                    settings.SpecularPower = GetRemappedShaderFloat(material, (int)CA_SKIN.PARAMETERS.SPECULAR_POWER, 32f);
                    settings.SpecularUvMult = GetRemappedShaderFloat(material, (int)CA_SKIN.PARAMETERS.SPECULAR_UV_MULT, 1f);
                    settings.SecondarySpecularUvMult = GetRemappedShaderFloat(material, (int)CA_SKIN.PARAMETERS.SECONDARY_SPECULAR_UV_MULT, 1f);
                    settings.DiffuseRoughness = GetRemappedShaderFloat(material, (int)CA_SKIN.PARAMETERS.DIFFUSE_ROUGHNESS_FACTOR, 0f);
                    settings.SpecularTint = GetShaderColorParameter(material, (int)CA_SKIN.PARAMETERS.SPECULAR_TINT, "SPECULAR_TINT", settings.SpecularTint);
                    break;
                case SHADER_LIST.CA_HAIR:
                    settings.SpecularPower = GetRemappedShaderFloat(material, (int)CA_HAIR.PARAMETERS.SPECULAR_POWER, 32f);
                    settings.SpecularUvMult = GetRemappedShaderFloat(material, (int)CA_HAIR.PARAMETERS.SPECULAR_UV_MULT, 1f);
                    settings.SpecularTint = GetShaderColorParameter(material, (int)CA_HAIR.PARAMETERS.SPECULAR_TINT, "SPECULAR_TINT", settings.SpecularTint);
                    break;
                case SHADER_LIST.CA_DECAL:
                    settings.SpecularPower = GetRemappedShaderFloat(material, (int)CA_DECAL.PARAMETERS.SPECULAR_POWER, 32f);
                    settings.SpecularLevel = GetRemappedShaderFloat(material, (int)CA_DECAL.PARAMETERS.SPECULAR_LEVEL, 1f);
                    break;
                case SHADER_LIST.CA_SURFACE_EFFECTS:
                    settings.SpecularPower = GetRemappedShaderFloat(material, (int)CA_SURFACE_EFFECTS.PARAMETERS.SPECULAR_POWER, 32f);
                    settings.SpecularUvMult = GetRemappedShaderFloat(material, (int)CA_SURFACE_EFFECTS.PARAMETERS.SPECULAR_UV_MULT, 1f);
                    settings.DiffuseRoughness = GetRemappedShaderFloat(material, (int)CA_SURFACE_EFFECTS.PARAMETERS.DIFFUSE_ROUGHNESS_FACTOR, 0f);
                    settings.SpecularTint = GetShaderColorParameter(material, (int)CA_SURFACE_EFFECTS.PARAMETERS.SPECULAR_TINT, "SPECULAR_TINT", settings.SpecularTint);
                    break;
                case SHADER_LIST.CA_PLANET:
                    settings.SpecularLevel = GetRemappedShaderFloat(material, (int)CA_PLANET.PARAMETERS.TERRAIN_MAP_SPECULAR_LEVEL, 1f);
                    settings.SpecularPower = GetRemappedShaderFloat(material, (int)CA_PLANET.PARAMETERS.TERRAIN_MAP_SPECULAR_POWER, 32f);
                    break;
                case SHADER_LIST.CA_TERRAIN:
                    settings.SpecularGloss = GetRemappedShaderFloat(material, (int)CA_TERRAIN.PARAMETERS.SPECULAR_GLOSS, 1f);
                    settings.SecondarySpecularGloss = GetRemappedShaderFloat(material, (int)CA_TERRAIN.PARAMETERS.SECONDARY_SPECULAR_GLOSS, 1f);
                    settings.SpecularUvMult = GetRemappedShaderFloat(material, (int)CA_TERRAIN.PARAMETERS.SPECULAR_UV_MULT, 1f);
                    settings.SecondarySpecularUvMult = GetRemappedShaderFloat(material, (int)CA_TERRAIN.PARAMETERS.SECONDARY_SPECULAR_UV_MULT, 1f);
                    settings.SpecularTint = GetShaderColorParameter(material, (int)CA_TERRAIN.PARAMETERS.SPECULAR_TINT, "SPECULAR_TINT", settings.SpecularTint);
                    settings.SecondarySpecularTint = GetShaderColorParameter(material, (int)CA_TERRAIN.PARAMETERS.SECONDARY_SPECULAR_TINT, "SECONDARY_SPECULAR_TINT", settings.SecondarySpecularTint);
                    break;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT:
                    settings.SpecularPower = GetRemappedShaderFloat(material, (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.SPECULAR_POWER, 32f);
                    settings.SecondarySpecularPower = GetRemappedShaderFloat(material, (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.SECONDARY_SPECULAR_POWER, 32f);
                    settings.SpecularUvMult = GetRemappedShaderFloat(material, (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.SPECULAR_UV_MULT, 1f);
                    settings.SecondarySpecularUvMult = GetRemappedShaderFloat(material, (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.SECONDARY_SPECULAR_UV_MULT, 1f);
                    settings.DiffuseRoughness = GetRemappedShaderFloat(material, (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.DIFFUSE_ROUGHNESS_FACTOR, 0f);
                    settings.SpecularTint = GetShaderColorParameter(material, (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.SPECULAR_TINT, "SPECULAR_TINT", settings.SpecularTint);
                    settings.SecondarySpecularTint = GetShaderColorParameter(material, (int)CA_LIGHTMAP_ENVIRONMENT.PARAMETERS.SECONDARY_SPECULAR_TINT, "SECONDARY_SPECULAR_TINT", settings.SecondarySpecularTint);
                    break;
                case SHADER_LIST.CA_STREAMER:
                    settings.SpecularPower = GetRemappedShaderFloat(material, (int)CA_STREAMER.PARAMETERS.SPECULAR_POWER, 32f);
                    settings.SecondarySpecularPower = GetRemappedShaderFloat(material, (int)CA_STREAMER.PARAMETERS.SECONDARY_SPECULAR_POWER, 32f);
                    settings.SpecularUvMult = GetRemappedShaderFloat(material, (int)CA_STREAMER.PARAMETERS.SPECULAR_UV_MULT, 1f);
                    settings.SecondarySpecularUvMult = GetRemappedShaderFloat(material, (int)CA_STREAMER.PARAMETERS.SECONDARY_SPECULAR_UV_MULT, 1f);
                    settings.DiffuseRoughness = GetRemappedShaderFloat(material, (int)CA_STREAMER.PARAMETERS.DIFFUSE_ROUGHNESS_FACTOR, 0f);
                    settings.SpecularTint = GetShaderColorParameter(material, (int)CA_STREAMER.PARAMETERS.SPECULAR_TINT, "SPECULAR_TINT", settings.SpecularTint);
                    settings.SecondarySpecularTint = GetShaderColorParameter(material, (int)CA_STREAMER.PARAMETERS.SECONDARY_SPECULAR_TINT, "SECONDARY_SPECULAR_TINT", settings.SecondarySpecularTint);
                    break;
                case SHADER_LIST.CA_LOW_LOD_CHARACTER:
                    settings.SpecularPower = GetRemappedShaderFloat(material, (int)CA_LOW_LOD_CHARACTER.PARAMETERS.SPECULAR_POWER, 32f);
                    settings.SpecularUvMult = GetRemappedShaderFloat(material, (int)CA_LOW_LOD_CHARACTER.PARAMETERS.SPECULAR_UV_MULT, 1f);
                    settings.SpecularTint = GetShaderColorParameter(material, (int)CA_LOW_LOD_CHARACTER.PARAMETERS.SPECULAR_TINT, "SPECULAR_TINT", settings.SpecularTint);
                    break;
            }

            if (settings.SpecularTint.R + settings.SpecularTint.G + settings.SpecularTint.B < 16)
                settings.SpecularTint = System.Windows.Media.Colors.White;
            if (settings.SecondarySpecularTint.R + settings.SecondarySpecularTint.G + settings.SecondarySpecularTint.B < 16)
                settings.SecondarySpecularTint = System.Windows.Media.Colors.White;

            settings.SpecularPower = Math.Max(1f, Math.Min(256f, settings.SpecularPower));
            settings.SecondarySpecularPower = Math.Max(1f, Math.Min(256f, settings.SecondarySpecularPower));
            settings.SpecularLevel = Math.Max(0f, Math.Min(2f, settings.SpecularLevel));
            settings.SpecularGloss = Math.Max(0f, Math.Min(2f, settings.SpecularGloss));
            settings.SecondarySpecularGloss = Math.Max(0f, Math.Min(2f, settings.SecondarySpecularGloss));

            return settings;
        }

        /// <summary>
        /// Specular maps pack glossiness in green and metalness in blue (BGRA byte order).
        /// Baked into diffuse because WPF SpecularMaterial tends to render surfaces black.
        /// </summary>
        private static ImageSource ApplySpecularChannelsToDiffuse(ImageSource diffuseSource, ImageSource specularSource, Materials.Material material, ImageSource secondarySpecularSource = null)
        {
            BitmapSource diffuseBitmap = ToBgra32Bitmap(diffuseSource);
            BitmapSource specularBitmap = ToBgra32Bitmap(specularSource);
            if (diffuseBitmap == null || specularBitmap == null)
                return null;

            int width = diffuseBitmap.PixelWidth;
            int height = diffuseBitmap.PixelHeight;
            if (width <= 0 || height <= 0)
                return null;

            int diffuseStride = (width * 4 + 3) & ~3;
            int specWidth = specularBitmap.PixelWidth;
            int specHeight = specularBitmap.PixelHeight;
            int specStride = (specWidth * 4 + 3) & ~3;

            byte[] diffusePixels = new byte[height * diffuseStride];
            byte[] specPixels = new byte[specHeight * specStride];
            byte[] output = new byte[height * diffuseStride];
            diffuseBitmap.CopyPixels(diffusePixels, diffuseStride, 0);
            specularBitmap.CopyPixels(specPixels, specStride, 0);

            SpecularPreviewSettings settings = GetSpecularPreviewSettings(material);
            BitmapSource secondarySpecularBitmap = ToBgra32Bitmap(secondarySpecularSource);
            byte[] secondarySpecPixels = null;
            int secondarySpecWidth = 0;
            int secondarySpecHeight = 0;
            int secondarySpecStride = 0;
            if (settings.SecondarySpecularMapping && secondarySpecularBitmap != null)
            {
                secondarySpecWidth = secondarySpecularBitmap.PixelWidth;
                secondarySpecHeight = secondarySpecularBitmap.PixelHeight;
                secondarySpecStride = (secondarySpecWidth * 4 + 3) & ~3;
                secondarySpecPixels = new byte[secondarySpecHeight * secondarySpecStride];
                secondarySpecularBitmap.CopyPixels(secondarySpecPixels, secondarySpecStride, 0);
            }

            for (int y = 0; y < height; y++)
            {
                float v = height == 1 ? 0f : y / (float)(height - 1);

                for (int x = 0; x < width; x++)
                {
                    float u = width == 1 ? 0f : x / (float)(width - 1);
                    int diffuseIndex = y * diffuseStride + x * 4;

                    float dr = diffusePixels[diffuseIndex + 2] / 255f;
                    float dg = diffusePixels[diffuseIndex + 1] / 255f;
                    float db = diffusePixels[diffuseIndex] / 255f;
                    float da = diffusePixels[diffuseIndex + 3] / 255f;

                    ApplySpecularMapSample(
                        specPixels, specWidth, specHeight, specStride,
                        x, y, width, height, settings, secondary: false,
                        out float metalness, out float shapedGloss);

                    float dielectricScale = 1f - metalness * 0.35f;
                    float highlight = shapedGloss * settings.GetHighlightScale(secondary: false);
                    float metallic = metalness * shapedGloss;

                    float tintR = settings.SpecularTint.R / 255f;
                    float tintG = settings.SpecularTint.G / 255f;
                    float tintB = settings.SpecularTint.B / 255f;

                    // Soft additive: brighten toward tint without clipping everything to white
                    float outR = dr * dielectricScale + (1f - dr) * highlight * tintR + (1f - dr) * metallic * tintR * 0.35f;
                    float outG = dg * dielectricScale + (1f - dg) * highlight * tintG + (1f - dg) * metallic * tintG * 0.35f;
                    float outB = db * dielectricScale + (1f - db) * highlight * tintB + (1f - db) * metallic * tintB * 0.35f;

                    if (secondarySpecPixels != null)
                    {
                        ApplySpecularMapSample(
                            secondarySpecPixels, secondarySpecWidth, secondarySpecHeight, secondarySpecStride,
                            x, y, width, height, settings, secondary: true,
                            out float secondaryMetalness, out float secondaryShapedGloss);

                        float secTintR = settings.SecondarySpecularTint.R / 255f;
                        float secTintG = settings.SecondarySpecularTint.G / 255f;
                        float secTintB = settings.SecondarySpecularTint.B / 255f;
                        float secondaryHighlight = secondaryShapedGloss * settings.GetHighlightScale(secondary: true);
                        float secondaryStrength = Math.Max(secondaryShapedGloss, secondaryMetalness * secondaryShapedGloss);

                        if (settings.SecondarySpecularBlendMultiply)
                        {
                            outR *= 1f - secondaryStrength + secondaryStrength * secTintR;
                            outG *= 1f - secondaryStrength + secondaryStrength * secTintG;
                            outB *= 1f - secondaryStrength + secondaryStrength * secTintB;
                        }
                        else
                        {
                            outR = outR + (secondaryStrength * secTintR - outR) * secondaryHighlight;
                            outG = outG + (secondaryStrength * secTintG - outG) * secondaryHighlight;
                            outB = outB + (secondaryStrength * secTintB - outB) * secondaryHighlight;
                        }
                    }

                    float previewOpacity = settings.GetPreviewOpacity();
                    outR = dr + (outR - dr) * previewOpacity;
                    outG = dg + (outG - dg) * previewOpacity;
                    outB = db + (outB - db) * previewOpacity;

                    output[diffuseIndex] = (byte)Math.Max(0, Math.Min(255, outB * 255f));
                    output[diffuseIndex + 1] = (byte)Math.Max(0, Math.Min(255, outG * 255f));
                    output[diffuseIndex + 2] = (byte)Math.Max(0, Math.Min(255, outR * 255f));
                    output[diffuseIndex + 3] = (byte)Math.Max(0, Math.Min(255, da * 255f));
                }
            }

            var result = new WriteableBitmap(width, height, diffuseBitmap.DpiX, diffuseBitmap.DpiY, PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, width, height), output, diffuseStride, 0);
            result.Freeze();
            return result;
        }

        private static void ApplySpecularMapSample(
            byte[] specPixels,
            int specWidth,
            int specHeight,
            int specStride,
            int x,
            int y,
            int targetWidth,
            int targetHeight,
            SpecularPreviewSettings settings,
            bool secondary,
            out float metalness,
            out float shapedGloss)
        {
            float u = targetWidth == 1 ? 0f : x / (float)(targetWidth - 1);
            float v = targetHeight == 1 ? 0f : y / (float)(targetHeight - 1);
            float uvMult = secondary ? settings.SecondarySpecularUvMult : settings.SpecularUvMult;
            float scaledU = u * uvMult;
            scaledU -= (float)Math.Floor(scaledU);
            float scaledV = v * uvMult;
            scaledV -= (float)Math.Floor(scaledV);

            int specY = specHeight == targetHeight ? y : (int)(scaledV * (specHeight - 1));
            if (specY >= specHeight)
                specY = specHeight - 1;

            int specX = specWidth == targetWidth ? x : (int)(scaledU * (specWidth - 1));
            if (specX >= specWidth)
                specX = specWidth - 1;

            int specIndex = specY * specStride + specX * 4;
            metalness = specPixels[specIndex] / 255f;
            float glossiness = specPixels[specIndex + 1] / 255f;
            float glossExponent = settings.GetGlossExponent(secondary);
            shapedGloss = (float)Math.Pow(Math.Max(0f, glossiness), glossExponent);
        }

        private static BitmapSource ToBgra32Bitmap(ImageSource source)
        {
            if (source == null)
                return null;

            BitmapSource bitmap = source as BitmapSource;
            if (bitmap == null)
                return null;

            if (bitmap.Format == PixelFormats.Bgra32)
                return bitmap;

            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            converted.Freeze();
            return converted;
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

        private static bool HasShaderFeature(Shaders.Shader shader, string featureName)
        {
            if (shader == null)
                return false;

            int? featureIndex = ShaderUtility.GetShaderFunctionalityIndex(shader.Ubershader, ShaderIndexType.FEATURES, featureName);
            return featureIndex.HasValue && (shader.UbershaderFeatureFlags & (1L << featureIndex.Value)) != 0;
        }

        private static bool HasDirtMappingEnabled(Shaders.Shader shader) => HasShaderFeature(shader, "DIRT_MAPPING");

        private static bool HasDirtBlendMultiplyEnabled(Shaders.Shader shader) => HasShaderFeature(shader, "DIRT_BLEND_MULTIPLY");

        private static bool HasSpecularMappingEnabled(Shaders.Shader shader) =>
            HasShaderFeature(shader, "SPECULAR_MAPPING") || HasShaderFeature(shader, "SPECULAR_GLOSS");

        private static bool HasSecondarySpecularMappingEnabled(Shaders.Shader shader) =>
            HasShaderFeature(shader, "SECONDARY_SPECULAR_MAPPING");

        private static bool HasSecondarySpecularBlendMultiplyEnabled(Shaders.Shader shader) =>
            HasShaderFeature(shader, "SECONDARY_SPECULAR_BLEND_MULTIPLY");

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
