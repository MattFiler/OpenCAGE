using Assimp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.ModelExport
{
    /// <summary>
    /// The formats a model or animation can be written to, what each one can carry, and the one
    /// place that decides which writer handles it.
    ///
    /// FBX and glTF are written here rather than by assimp. assimp's FBX exporter resamples
    /// animation down to a handful of keys, and its glTF exporter mangles anything animated badly
    /// enough that it wasn't worth offering. The two formats everyone actually asks for are the two
    /// assimp is worst at, so they are ours; the rest still go through it.
    /// </summary>
    public static class ModelExporter
    {
        public class Format
        {
            /// <summary>Lower case, with the dot - ".fbx".</summary>
            public string Extension;
            public string Description;

            /// <summary>Whether an animation written to this format survives it.</summary>
            public bool Animation;

            /// <summary>Whether skin weights survive.</summary>
            public bool Skinning;

            /// <summary>How many UV sets it carries, for the formats that only take one.</summary>
            public int UVSets;

            /// <summary>What one unit means, in CATHODE metres.</summary>
            public float UnitScale;

            /// <summary>Whether the format's UV origin is the opposite corner to CATHODE's.</summary>
            public bool FlipUVs;

            /// <summary>Written here rather than handed to assimp.</summary>
            public bool Native;

            /// <summary>
            /// Whether textures have to go out as PNG. glTF allows PNG and JPEG only, so a .dds
            /// beside one is simply not loaded; every other format here reads DDS and keeps the
            /// compression, which is worth having on a character's worth of 2K maps.
            /// </summary>
            public bool PrefersPng;

            public override string ToString() { return Description; }
        }

        /* Centimetres for the DCC formats, which is what a unit has meant in FBX since the start and
         * what OBJ and COLLADA get exported as in practice. glTF is defined in metres. */
        private const float Centimetres = 100.0f;
        private const float Metres = 1.0f;

        public static readonly IReadOnlyList<Format> Formats = new List<Format>
        {
            new Format { Extension = ".fbx",  Description = "FBX",                Animation = true,  Skinning = true,  UVSets = 8, UnitScale = Centimetres, FlipUVs = true,  Native = true },
            new Format { Extension = ".glb",  Description = "glTF Binary",        Animation = true,  Skinning = true,  UVSets = 8, UnitScale = Metres,      FlipUVs = false, Native = true, PrefersPng = true },
            new Format { Extension = ".gltf", Description = "glTF",               Animation = true,  Skinning = true,  UVSets = 8, UnitScale = Metres,      FlipUVs = false, Native = true, PrefersPng = true },
            new Format { Extension = ".dae",  Description = "COLLADA",            Animation = true,  Skinning = true,  UVSets = 8, UnitScale = Centimetres, FlipUVs = true,  Native = false },
            new Format { Extension = ".obj",  Description = "Wavefront OBJ",      Animation = false, Skinning = false, UVSets = 1, UnitScale = Centimetres, FlipUVs = true,  Native = false },
        };

        /// <summary>The format a filename names, or FBX if it names nothing we know.</summary>
        public static Format For(string filename)
        {
            string extension = (Path.GetExtension(filename) ?? "").ToLowerInvariant();
            return Formats.FirstOrDefault(x => x.Extension == extension) ?? Formats[0];
        }

        /// <summary>
        /// A filter for a save dialog. Pass true to leave out the formats that can't carry an
        /// animation, so an animation export never offers one that would drop it.
        /// </summary>
        public static string Filter(bool animated)
        {
            IEnumerable<Format> offered = animated ? Formats.Where(x => x.Animation) : Formats;
            return string.Join("|", offered.Select(x => x.Description + " (*" + x.Extension + ")|*" + x.Extension));
        }

        /// <summary>
        /// A filter for an OPEN dialog, which is a different job to <see cref="Filter"/>: exporting
        /// has to settle on one format, but importing does not care which of them a file is, so the
        /// first entry takes them all and nobody has to know that their model is COLLADA.
        /// </summary>
        public static string ImportFilter(bool animated)
        {
            IEnumerable<Format> offered = animated ? Formats.Where(x => x.Animation) : Formats;
            List<Format> list = offered.ToList();

            string all = string.Join(";", list.Select(x => "*" + x.Extension));
            string entries = string.Join("|", list.Select(x => x.Description + " (*" + x.Extension + ")|*" + x.Extension));
            return "All supported models|" + all + "|" + entries + "|All files (*.*)|*.*";
        }

        /// <summary>Where a format sits in <see cref="Filter"/>, which dialogs count from one.</summary>
        public static int FilterIndex(string extension, bool animated)
        {
            List<Format> offered = (animated ? Formats.Where(x => x.Animation) : Formats).ToList();
            int index = offered.FindIndex(x => x.Extension == (extension ?? "").ToLowerInvariant());
            return index < 0 ? 1 : index + 1;
        }

        /// <summary>Write a scene, picking the writer from the file name.</summary>
        public static void Write(Scene scene, string filename)
        {
            Format format = For(filename);
            switch (format.Extension)
            {
                case ".fbx": FbxExporter.Export(scene, filename); return;
                case ".glb": GltfExporter.Export(scene, filename, true); return;
                case ".gltf": GltfExporter.Export(scene, filename, false); return;
                default:
                    using (AssimpContext exporter = new AssimpContext())
                        exporter.ExportFile(scene, filename, AssimpFormatId(format.Extension));
                    return;
            }
        }

        /* A couple of assimp's exporters are registered under an id that isn't the extension */
        private static string AssimpFormatId(string extension)
        {
            switch (extension)
            {
                case ".dae": return "collada";
                case ".gltf": return "gltf2";
                case ".glb": return "glb2";
                default: return extension.TrimStart('.');
            }
        }
    }
}
