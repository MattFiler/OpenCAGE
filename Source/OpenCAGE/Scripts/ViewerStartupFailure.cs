using System;
using System.IO;

namespace OpenCAGE
{
    /// <summary>
    /// The level viewer is a self-contained Godot .NET export: the data_* folder beside
    /// CathodeEditorGodot.exe carries hostfxr, coreclr and a runtimeconfig that names the bundled
    /// runtime. A copy assembled from a 'dotnet build' output has a framework-dependent runtimeconfig
    /// and no runtime in that folder, so the viewer dies before its first frame: hostfxr looks for an
    /// installed .NET 8, finds none, the C# glue never initialises and the engine faults on the null it
    /// left behind. Two dashboard reports from one self-built install arrived as plain access
    /// violations, when the viewer's own output says exactly what is wrong and the user can fix it.
    /// </summary>
    public static class ViewerStartupFailure
    {
        /* What hostfxr and Godot's mono module print, in order, on the way down. Any one of them is enough. */
        private static readonly string[] MissingDotNetRuntimeMarkers =
        {
            "You must install or update .NET to run this application",
            "hostfxr_initialize_for_dotnet_command_line failed",
            "\"load_assembly_and_get_function_pointer\" is null",
            "\"godot_plugins_initialize\" is null",
        };

        /// <summary>True if this viewer output (one line, or a run of them) says the .NET runtime could not be brought up.</summary>
        public static bool IsMissingDotNetRuntime(string viewerOutput)
        {
            if (string.IsNullOrEmpty(viewerOutput))
                return false;

            foreach (string marker in MissingDotNetRuntimeMarkers)
            {
                if (viewerOutput.IndexOf(marker, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>The message shown for it, naming the folder the runtime should be in.</summary>
        public static string DescribeMissingDotNetRuntime(string viewerExecutablePath)
        {
            string folder;
            try
            {
                folder = Path.GetDirectoryName(viewerExecutablePath);
            }
            catch
            {
                folder = null;
            }
            if (string.IsNullOrEmpty(folder))
                folder = viewerExecutablePath ?? "(unknown)";

            return "The level viewer could not start: the .NET runtime it needs is missing from its folder.\n\n"
                + folder + "\n\n"
                + "The viewer is a self-contained Godot export that carries its own copy of .NET. "
                + "If this copy was built from source, export it from the Godot editor (the export bundles the runtime) "
                + "rather than copying a 'dotnet build' output into the folder. "
                + "If OpenCAGE was installed through Steam, verify the integrity of its files.\n\n"
                + "Until then the viewport stays hidden.";
        }
    }
}
