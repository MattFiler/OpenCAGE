using CathodeLib.NavMesh;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The navmesh bake's settings from Options > Save/Build, for every save that bakes the navmesh.
    /// </summary>
    static class NavMeshBakeOptions
    {
        //Options > Save/Build > Walk On Downward-Facing Floors: the navmesh covers floors whose collision faces down (off by default)
        public const string DownFacingFloorsKey = "NavMeshDownFacingFloors";

        public static NavMeshBakeSettings FromOptions()
        {
            return new NavMeshBakeSettings()
            {
                WalkDownFacingFloors = SettingsManager.GetBool(DownFacingFloorsKey),
            };
        }

        /// <summary>Adds the navmesh options to the end of the Save/Build menu.</summary>
        public static void AttachMenu(ToolStripMenuItem saveBuild)
        {
            if (saveBuild == null) return;
            ToolStripMenuItem item = new ToolStripMenuItem("Walk On Downward-Facing Floors")
            {
                Name = "navMeshDownFacingFloorsToolStripMenuItem",
                ToolTipText = "If enabled, the navmesh bake also covers floors whose collision faces down (imported models wound the wrong way), " +
                              "where a character can walk onto them from the floor around. The game's collision holds the player up on them either way; " +
                              "without this the navmesh leaves them out, and the AI and the motion tracker's objective route stop at their edge. " +
                              "The levels the game ships with are baked with it off.",
            };
            item.Click += (s, e) => SettingsManager.SetBool(DownFacingFloorsKey, !SettingsManager.GetBool(DownFacingFloorsKey));
            saveBuild.DropDownItems.Add(new ToolStripSeparator() { Name = "saveBuildToolStripSeparatorNavMesh" });
            saveBuild.DropDownItems.Add(item);
            saveBuild.DropDownOpening += (s, e) => item.Checked = SettingsManager.GetBool(DownFacingFloorsKey);
        }
    }
}
