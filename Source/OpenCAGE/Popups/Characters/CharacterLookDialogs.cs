using CATHODE;
using CATHODE.Scripting;
using Newtonsoft.Json.Linq;
using OpenCAGE.Characters;
using OpenCAGE.MCP;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups.Characters
{
    /// <summary>
    /// Runs one of the character tools (McpDisplayModelTools) off the UI thread, as any tool call runs, and hands back its text and image.
    /// </summary>
    internal static class CharacterToolRunner
    {
        public static async Task<(bool error, string text, Image image)> Run(string toolName, JObject args)
        {
            McpTool tool = McpTools.Find(toolName);
            if (tool == null) return (true, "The " + toolName + " tool is missing from this build.", null);
            JObject result = await Task.Run(() =>
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes(20)))
                    return tool.Invoke(new McpCall(tool, args, cts.Token, null));
            });
            JArray content = (JArray)result["content"] ?? new JArray();
            string text = string.Concat(content.Where(c => (string)c["type"] == "text").Select(c => (string)c["text"]));
            try { text = JToken.Parse(text).ToString(Newtonsoft.Json.Formatting.Indented); } catch { }
            Image image = null;
            JToken png = content.FirstOrDefault(c => (string)c["type"] == "image");
            if (png != null)
            {
                try { image = Image.FromStream(new MemoryStream(Convert.FromBase64String((string)png["data"]))); } catch { image = null; }
            }
            return ((bool?)result["isError"] == true, text.Replace("\n", Environment.NewLine), image);
        }
    }

    /// <summary>
    /// The Composite Browser's right-click on a "DisplayModel:" composite: the character tools for it. Call from the browser's menu
    /// opening, as ReplaceWeaponModel.PrepareMenu is: the items show only for a display model.
    /// </summary>
    public static class DisplayModelMenu
    {
        public static void PrepareMenu(ContextMenuStrip menu, Composite composite)
        {
            if (menu == null) return;
            ToolStripMenuItem root = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(o => (o.Name ?? "") == "displayModelToolsMenuItem");
            if (root == null)
            {
                root = new ToolStripMenuItem("Character Look") { Name = "displayModelToolsMenuItem" };
                ToolStripMenuItem variant = new ToolStripMenuItem("Make Variant (tint, held part)...") { ToolTipText = "A copy of this look that works in game: tinted, and/or holding a part skinned to its rig" };
                ToolStripMenuItem held = new ToolStripMenuItem("Add Part Skinned To This Rig...") { ToolTipText = "A prop that follows one bone - a gun in the hand, a lamp on the head" };
                ToolStripMenuItem firstPerson = new ToolStripMenuItem("Make First-Person Body...") { ToolTipText = "Cut the head, neck and upper torso off for the player's view" };
                ToolStripMenuItem player = new ToolStripMenuItem("Use As Player Model") { ToolTipText = "Global's FlightSuit registers this display model (the player wears it in first person)" };
                ToolStripMenuItem check = new ToolStripMenuItem("Check / Fix Shape") { ToolTipText = "Ids, skeleton and animated-model entry as the game needs them" };
                variant.Click += (o, e) => new DisplayVariantDialog(root.Tag as string, false).Show();
                held.Click += (o, e) => new DisplayVariantDialog(root.Tag as string, true).Show();
                firstPerson.Click += (o, e) => new FirstPersonBodyDialog(root.Tag as string, null).Show();
                player.Click += async (o, e) =>
                {
                    var r = await CharacterToolRunner.Run("set_player_model", new JObject() { ["display_model"] = root.Tag as string });
                    MessageBox.Show(r.text, r.error ? "Not set" : "Player model set", MessageBoxButtons.OK, r.error ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                };
                check.Click += async (o, e) =>
                {
                    var r = await CharacterToolRunner.Run("check_display_models", new JObject() { ["composite"] = root.Tag as string });
                    if (!r.error && r.text.Contains("\"ok\": false") && MessageBox.Show(r.text + Environment.NewLine + Environment.NewLine + "Put it right? (one undo step)", "Display model shape", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        r = await CharacterToolRunner.Run("check_display_models", new JObject() { ["composite"] = root.Tag as string, ["fix"] = true });
                    MessageBox.Show(r.text, "Display model shape", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                root.DropDownItems.AddRange(new ToolStripItem[] { variant, held, firstPerson, new ToolStripSeparator(), player, check });
                menu.Items.Add(root);
            }
            bool show = DisplayModelShape.IsDisplayModel(composite);
            root.Visible = show;
            root.Tag = show ? composite.name : null;
        }
    }
}
