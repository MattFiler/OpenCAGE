using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The bits of the animation export flow that both the editor and the preview window need.
    /// </summary>
    public static class AnimationExport
    {
        /* COLLADA leads here rather than FBX, which is the other way round to the model exporter.
         * The reason is the assimp build we ship, measured against a 158 bone rig:
         *
         *   COLLADA  every animation, every channel, every key. Verbose, but complete.
         *   FBX      every channel, but only a handful of keys - a 227 frame clip came back as 8.
         *   GLTF     keeps the keys, but splits each channel into an animation of its own and drops
         *            the names; worse, exporting a skinned mesh and an animation together kills the
         *            process outright inside the native exporter. It isn't offered because of that.
         */
        public const string FileFilter = "COLLADA Model|*.dae|FBX Model|*.fbx";

        /// <summary>Whether a format keeps every keyframe, or flattens the animation on the way out.</summary>
        public static bool KeepsEveryKeyframe(string filename)
        {
            return string.Equals(Path.GetExtension(filename), ".dae", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ask where to write an export, warning if the chosen format won't carry the animation.
        /// Returns null if the user backed out.
        /// </summary>
        public static string AskWhereToSave(IWin32Window owner, string suggestedName)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = FileFilter;
                dialog.FilterIndex = 1;
                dialog.DefaultExt = "dae";
                dialog.AddExtension = true;
                dialog.FileName = CleanFileName(suggestedName) + ".dae";
                if (dialog.ShowDialog(owner) != DialogResult.OK) return null;

                if (!KeepsEveryKeyframe(dialog.FileName))
                {
                    DialogResult answer = MessageBox.Show(
                        "The FBX exporter this tool uses keeps only a handful of an animation's keyframes - a three second "
                        + "clip comes out as about eight poses with everything in between thrown away. The rig and the mesh "
                        + "survive intact; the movement does not.\n\nCOLLADA (.dae) writes every frame.\n\nExport as FBX anyway?",
                        "FBX loses most of the animation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (answer != DialogResult.Yes) return null;
                }
                return dialog.FileName;
            }
        }

        /// <summary>
        /// COLLADA is XML, so a rig of any size runs to a few megabytes per clip. Check before writing
        /// a whole context out. Returns false if the user would rather not.
        /// </summary>
        public static bool ConfirmLargeExport(IWin32Window owner, int clips, int bones, int frames)
        {
            //measured at roughly 340 bytes per bone per frame once COLLADA's float formatting is counted
            long estimate = (long)clips * bones * frames * 340;
            if (estimate < 250L * 1024 * 1024) return true;

            return MessageBox.Show(
                "Writing " + clips + " animations for a " + bones + " bone rig will produce somewhere around "
                + (estimate / (1024 * 1024)) + " MB, and will take a while.\n\nCarry on?",
                "Large export", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        /// <summary>Turn a clip or set name into something a file system will accept.</summary>
        public static string CleanFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "animation";

            char[] invalid = Path.GetInvalidFileNameChars();
            string result = new string(name.Select(x => invalid.Contains(x) ? '_' : x).ToArray()).Trim();
            return result.Length == 0 ? "animation" : result;
        }
    }
}
