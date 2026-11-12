using AlienPAK;
using Assimp;
using CATHODE;
using CATHODE.ShaderTypes;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AssetKind = CathodeLib.AssetModificationInfoTable.AssetKind;

namespace OpenCAGE.Brushwork
{
    /// <summary>
    /// The materials brushes draw with: any of the level's own, and by default the grey grid (OPENCAGE\BRUSH_GRID) - a
    /// generated CA_ENVIRONMENT material on a 512 px picture made here: a little noise, with a darker line every 256 px, so at
    /// a brush's one repeat per 2 m a line runs every metre. Made the first time a level needs it, through the same generator
    /// a model import uses, and found again by name after that.
    /// </summary>
    public static class BrushMaterials
    {
        public const string GridName = "OPENCAGE\\BRUSH_GRID";
        private const string GridTextureFile = "OPENCAGE_BRUSH_GRID.png";

        /// <summary>The physical material the grid's collision reads (footsteps, impacts): a solid metal floor, as most of the station is.</summary>
        private const string GridPhysicalMaterial = "METAL_SOLID";

        /// <summary>A level material by name (exact, then ignoring case), or null.</summary>
        public static Materials.Material Find(Level level, string name)
        {
            if (level?.Materials == null || string.IsNullOrEmpty(name)) return null;
            return level.Materials.Entries.FirstOrDefault(o => o != null && o.Name == name)
                ?? level.Materials.Entries.FirstOrDefault(o => o != null && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The grid material, made now if the level has none. Not undoable (like an import's generated materials); null with a reason when it could not be made.</summary>
        public static Materials.Material EnsureGrid(Level level, out string error)
        {
            error = null;
            Materials.Material existing = Find(level, GridName);
            if (existing != null) return existing;
            if (level?.Materials == null || level.Textures == null || level.Shaders == null)
            {
                error = "the level's materials are not loaded";
                return null;
            }

            string folder = Path.Combine(Path.GetTempPath(), "OpenCAGE", "Brushes");
            Directory.CreateDirectory(folder);
            string picture = Path.Combine(folder, GridTextureFile);
            using (Bitmap grid = DrawGrid())
                grid.Save(picture, ImageFormat.Png);

            //A one-quad scene carrying the picture as its diffuse map: what the generator reads a model's material from
            Scene scene = new Scene() { RootNode = new Node("BRUSH_GRID") };
            Assimp.Material source = new Assimp.Material() { Name = "BRUSH_GRID" };
            source.ColorDiffuse = new Color4D(1, 1, 1, 1);
            source.TextureDiffuse = new TextureSlot(GridTextureFile, TextureType.Diffuse, 0, TextureMapping.FromUV, 0, 1, TextureOperation.Add, TextureWrapMode.Wrap, TextureWrapMode.Wrap, 0);
            scene.Materials.Add(source);
            Mesh quad = new Mesh("BRUSH_GRID", PrimitiveType.Triangle) { MaterialIndex = 0 };
            quad.Vertices.AddRange(new[] { new Vector3D(0, 0, 0), new Vector3D(1, 0, 0), new Vector3D(1, 0, 1), new Vector3D(0, 0, 1) });
            quad.Normals.AddRange(Enumerable.Repeat(new Vector3D(0, 1, 0), 4));
            quad.TextureCoordinateChannels[0].AddRange(new[] { new Vector3D(0, 0, 0), new Vector3D(1, 0, 0), new Vector3D(1, 1, 0), new Vector3D(0, 1, 0) });
            quad.UVComponentCount[0] = 2;
            quad.Faces.Add(new Face(new[] { 0, 2, 1 }));
            quad.Faces.Add(new Face(new[] { 0, 3, 2 }));
            scene.Meshes.Add(quad);
            scene.RootNode.MeshIndices.Add(0);

            int materialsBefore = level.Materials.Entries.Count, texturesBefore = level.Textures.Entries.Count;
            MaterialGenerator.Plan plan = MaterialGenerator.Describe(scene, source, quad, picture, false, level, SHADER_LIST.CA_ENVIRONMENT, GridName, Singleton.PathToAI);
            if (!plan.CanGenerate)
            {
                error = plan.Error;
                return null;
            }
            Materials.Material made;
            try { made = MaterialGenerator.Generate(plan, source, level, Singleton.PathToAI, out error); }
            catch (Exception e) { made = null; error = e.Message; }
            if (made == null)
                return null;

            //Footsteps and impacts on it sound like a solid metal floor, not the engine's fallback or whatever donor material the
            //generator copied from
            int physical = PhysicalMaterialIndex(GridPhysicalMaterial);
            if (physical >= 0) made.PhysicalMaterialIndex = physical;

            for (int i = materialsBefore; i < level.Materials.Entries.Count; i++)
                EditHistory.AssetChanged(level, AssetKind.MATERIAL, level.Materials.Entries[i].Name);
            for (int i = texturesBefore; i < level.Textures.Entries.Count; i++)
                EditHistory.AssetChanged(level, AssetKind.TEXTURE, level.Textures.Entries[i].Name);
            Singleton.OnResourceModified?.Invoke();
            return made;
        }

        /// <summary>An index into the game's MATERIALS.BML by name, or -1.</summary>
        public static int PhysicalMaterialIndex(string name)
        {
            try
            {
                string path = Path.Combine(Singleton.PathToAI, "DATA", "MATERIAL_DATA", "MATERIALS.BML");
                if (!File.Exists(path)) return -1;
                System.Xml.XmlNode list = new BML(path).Content?["Materials"];
                if (list == null) return -1;
                List<string> names = list.ChildNodes.OfType<System.Xml.XmlElement>().Select(o => o.GetAttribute("name")).ToList();
                int index = names.FindIndex(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase));
                return index >= 0 && index < 255 ? index : -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>
        /// The grid picture: 512 px, grey noise around 118 (a touch bluer in G and B), and a line 38 darker and 3 px wide along
        /// every 256th row and column. Seeded, so it comes out the same every time.
        /// </summary>
        public static Bitmap DrawGrid()
        {
            const int size = 512;
            Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            Random random = new Random(7);
            byte[] pixels = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int n = 118 + random.Next(-10, 11);
                    if ((x % 256) < 3 || (y % 256) < 3) n -= 38;
                    int at = (y * size + x) * 4;
                    pixels[at + 0] = (byte)Math.Min(255, n + 6); //B
                    pixels[at + 1] = (byte)Math.Min(255, n + 2); //G
                    pixels[at + 2] = (byte)n;                    //R
                    pixels[at + 3] = 255;
                }
            }
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < size; y++)
                    Marshal.Copy(pixels, y * size * 4, data.Scan0 + y * data.Stride, size * 4);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }
    }
}
