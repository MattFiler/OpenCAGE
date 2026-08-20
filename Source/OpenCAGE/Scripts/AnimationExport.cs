using OpenCAGE.ModelExport;
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
        /// <summary>Only the formats that can carry an animation, so none of them loses one.</summary>
        public static string FileFilter { get { return ModelExporter.Filter(true); } }

        /// <summary>
        /// Ask where to write an export. Returns null if the user backed out.
        /// </summary>
        public static string AskWhereToSave(IWin32Window owner, string suggestedName)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = FileFilter;
                dialog.FilterIndex = ModelExporter.FilterIndex(SettingsManager.GetString(Settings.AnimationExportFormat, ".fbx"), true);
                dialog.DefaultExt = "fbx";
                dialog.AddExtension = true;
                dialog.FileName = CleanFileName(suggestedName) + ".fbx";
                if (dialog.ShowDialog(owner) != DialogResult.OK) return null;

                //open on whatever they chose last time rather than making them find it again
                SettingsManager.SetString(Settings.AnimationExportFormat, ModelExporter.For(dialog.FileName).Extension);
                return dialog.FileName;
            }
        }

        /// <summary>
        /// A whole context can be a lot of clips against a lot of bones. Check before writing one
        /// that will take a while. Returns false if the user would rather not.
        /// </summary>
        public static bool ConfirmLargeExport(IWin32Window owner, int clips, int bones, int frames, string filename = null)
        {
            /* Measured per bone per frame across the shipped rigs: COLLADA spends about 340 bytes on
             * formatting each one as text, FBX and glTF about 40 on packed floats. */
            long perKey = ModelExporter.For(filename ?? ".fbx").Extension == ".dae" ? 340 : 40;
            long estimate = (long)clips * bones * frames * perKey;
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
