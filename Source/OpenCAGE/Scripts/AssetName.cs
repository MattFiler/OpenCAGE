using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// Naming an asset inside a level's texture or model list.
    ///
    /// These lists are flat - a name simply carries its folders in it, and the browsers split it
    /// back apart to build their trees. So "make a folder" is nothing more than typing one into the
    /// name, and there is no separate folder to create or tidy up afterwards.
    ///
    /// The convention comes from the shipped data rather than from taste: names use backslashes,
    /// never forward slashes (1,139 of 1,142 level textures, 1,059 of 1,060 models, and not one
    /// forward slash between them), they carry the source art's extension - .tga and .dds for
    /// textures, .cs2 for models - and they nest up to eight deep with a median of three or four.
    /// Nothing in them strays outside letters, digits and "_-.[]() ", though a fair few models do
    /// start with "..\", so a leading parent segment has to stay legal.
    /// </summary>
    public static class AssetName
    {
        /// <summary>The separator the game's own names use.</summary>
        public const char Separator = '\\';

        /// <summary>The longest name worth allowing, comfortably past the deepest the game ships.</summary>
        public const int MaximumLength = 220;

        /// <summary>
        /// Tidy a name the way it will be stored: forward slashes become backslashes, runs of
        /// separators collapse, and stray whitespace around each part goes.
        /// </summary>
        public static string Normalise(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            string[] parts = name.Replace('/', Separator).Split(Separator);
            List<string> kept = new List<string>(parts.Length);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length != 0) kept.Add(trimmed);
            }
            return string.Join(Separator.ToString(), kept);
        }

        /// <summary>
        /// What's wrong with a name, or null if nothing is. Checked against the normalised form, so
        /// a caller should show <see cref="Normalise"/>'s result alongside it.
        /// </summary>
        public static string Problem(string name)
        {
            string tidy = Normalise(name);

            if (tidy.Length == 0)
                return "Give it a name.";

            if (tidy.Length > MaximumLength)
                return "That name is too long (" + tidy.Length + " characters, the limit is " + MaximumLength + ").";

            /* The separator is the one thing allowed through that a file name can't hold, since it
             * is what divides the folders rather than part of any one of them. */
            char[] illegal = Path.GetInvalidFileNameChars().Where(x => x != Separator && x != '/').ToArray();
            foreach (string part in tidy.Split(Separator))
            {
                if (part == "." || part == "..") continue;   //the game's own model names start with "..\"

                int at = part.IndexOfAny(illegal);
                if (at >= 0)
                    return "'" + part[at] + "' can't be used in a name.";

                if (part.EndsWith("."))
                    return "A folder or file name can't end with a full stop.";
            }

            return null;
        }

        /// <summary>Whether a name would land on top of something already there.</summary>
        public static bool Exists(string name, IEnumerable<string> taken)
        {
            if (taken == null) return false;

            string tidy = Normalise(name);
            foreach (string other in taken)
                if (string.Equals(Normalise(other), tidy, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>
        /// The same name with a number on the end, far enough along to be free. Only for callers
        /// that have to resolve a clash themselves rather than ask.
        /// </summary>
        public static string MakeUnique(string name, IEnumerable<string> taken)
        {
            string tidy = Normalise(name);
            if (!Exists(tidy, taken)) return tidy;

            /* Not Path.GetExtension: these names are not paths, and it throws on characters a path
             * cannot hold - which an asset name imported from a model file certainly can. */
            string extension = "";
            int dot = tidy.LastIndexOf('.');
            if (dot > tidy.LastIndexOf(Separator)) extension = tidy.Substring(dot);
            string stem = tidy.Substring(0, tidy.Length - extension.Length);

            for (int i = 1; i < 10000; i++)
            {
                string candidate = stem + "_" + i + extension;
                if (!Exists(candidate, taken)) return candidate;
            }
            return tidy;
        }

        /// <summary>
        /// A starting name for something being imported from <paramref name="sourcePath"/>, keeping
        /// the folders of whatever it is replacing or sitting alongside.
        /// </summary>
        /// <param name="extension">Forced extension, e.g. ".cs2", or null to keep the file's own.</param>
        public static string FromFile(string sourcePath, string extension = null, string folderOf = null)
        {
            string file = Path.GetFileName(sourcePath ?? "");
            if (extension != null)
                file = Path.GetFileNameWithoutExtension(sourcePath ?? "") + extension;

            string folder = FolderOf(folderOf);
            return Normalise(folder.Length == 0 ? file : folder + Separator + file);
        }

        /// <summary>Everything up to the last separator, or an empty string for a name with no folders.</summary>
        public static string FolderOf(string name)
        {
            string tidy = Normalise(name);
            int at = tidy.LastIndexOf(Separator);
            return at < 0 ? "" : tidy.Substring(0, at);
        }
    }
}
