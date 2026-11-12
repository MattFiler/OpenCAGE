using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace OpenCAGE.Brushwork
{
    /// <summary>
    /// Create Level's "Make playable test room": the player's body (DisplayModel:RIPLEY_FP, from FRONTEND) and spawn logic
    /// (SpawnPositionSelect, from SCI_HUB) come in through the composite import; once the level is open, a room of brushes, ceiling
    /// lights, a player start and a navmesh seed are added, then a Save &amp; Build. Everything it makes stays ordinary and editable.
    /// </summary>
    public static class TestRoom
    {
        public sealed class Options
        {
            /// <summary>Inside the walls, metres: X, Z.</summary>
            public float Width = 16, Length = 16;
            /// <summary>Floor to ceiling, metres.</summary>
            public float Height = 5;
            /// <summary>Light it (the ceiling lights); off, the room is dark.</summary>
            public bool Lights = true;

            public string Problem()
            {
                if (!(Width >= MinimumSide && Width <= MaximumSide) || !(Length >= MinimumSide && Length <= MaximumSide))
                    return "A test room is " + MinimumSide + " to " + MaximumSide + " m across.";
                if (!(Height >= MinimumHeight && Height <= MaximumHeight))
                    return "A test room is " + MinimumHeight + " to " + MaximumHeight + " m high (the player stands about 1.8 m).";
                return null;
            }

            public string Describe() => Num(Width) + " x " + Num(Length) + " x " + Num(Height) + " m" + (Lights ? ", lit" : ", unlit");
        }

        public const float MinimumSide = 2, MaximumSide = 128, MinimumHeight = 2.5f, MaximumHeight = 32;

        /// <summary>Where the player's body and the spawn logic come from.</summary>
        public static readonly (string Level, string Composite)[] Imports =
        {
            (EditorUtils.FrontendLevel, "DisplayModel:RIPLEY_FP"),
            ("PRODUCTION/SCI_HUB", "Archetypes\\Script\\Mission\\SpawnPositionSelect"),
        };

        //The walls', floor's and ceiling's thickness
        private const float Thickness = 0.5f;
        //Lights sit about this far apart, in a grid over the ceiling (fewer, wider, past MaximumLights)
        private const float LightSpacing = 8;
        private const int MaximumLights = 36;
        //Each light's reach against R, the distance to the far floor corner of its cell, and its strength: a broad, even
        //spread (CATHODE: full strength out to start_attenuation, then a steep fall)
        private const float StartOfR = 0.3f, EndOfR = 2.2f, Intensity = 0.9f;
        private static readonly ShortGuid SpawnOnReset = ShortGuidUtils.Generate("spawn_on_reset");
        private static readonly Vector3 Warm = new Vector3(255, 244, 229), Cool = new Vector3(229, 239, 255), White = new Vector3(255, 255, 255);

        #region The imports
        /// <summary>A test room import in a selection: its level and id.</summary>
        public struct Entry
        {
            public string Level;
            public ShortGuid Id;
        }

        /// <summary>
        /// Put the test room's composites in <paramref name="selection"/> (with what they place, as the picker lists it), and
        /// return the ones that were not in it already - what unticking the option takes out again. Throws when a source level
        /// cannot be read or lacks one.
        /// </summary>
        public static List<Entry> AddImports(CompositeSelection selection)
        {
            List<Entry> added = new List<Entry>();
            foreach ((string level, string composite) in Imports)
            {
                List<CompositeIndexEntry> index = CompositeIndexCache.Get(level);
                CompositeIndexEntry entry = index.FirstOrDefault(o => string.Equals(McpScript.NormalisePath(o.Name), composite, StringComparison.OrdinalIgnoreCase));
                if (entry.Name == null)
                    throw new BrushException(level + " has no " + composite + ", which a test room needs.");
                CompositeSelection.LevelPick pick = selection.GetOrAdd(level);
                if (!pick.Composites.ContainsKey(entry.ID))
                {
                    pick.Composites[entry.ID] = entry.Name;
                    added.Add(new Entry() { Level = level, Id = entry.ID });
                }
                RefreshImplied(pick, index, selection.IncludeChildren);
            }
            return added;
        }

        /// <summary>Take out of <paramref name="selection"/> the entries <see cref="AddImports"/> added (those still there).</summary>
        public static void RemoveImports(CompositeSelection selection, IEnumerable<Entry> added)
        {
            foreach (Entry entry in added ?? Enumerable.Empty<Entry>())
            {
                CompositeSelection.LevelPick pick = selection.Levels.FirstOrDefault(o => string.Equals(o.Level, entry.Level, StringComparison.OrdinalIgnoreCase));
                if (pick == null || !pick.Composites.Remove(entry.Id)) continue;
                try { RefreshImplied(pick, CompositeIndexCache.Get(pick.Level), selection.IncludeChildren); }
                catch (Exception) { pick.Implied.Clear(); }
            }
            selection.Prune();
        }

        /// <summary>Whether this ticked composite is one a test room brings.</summary>
        public static bool IsImport(string level, string compositeName)
        {
            return Imports.Any(o => string.Equals(o.Level, level, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(o.Composite, McpScript.NormalisePath(compositeName), StringComparison.OrdinalIgnoreCase));
        }

        //What the ticked composites place, all the way down, as the picker fills it
        private static void RefreshImplied(CompositeSelection.LevelPick pick, List<CompositeIndexEntry> index, bool children)
        {
            pick.Implied.Clear();
            foreach (CompositeIndexEntry entry in McpPortingTools.IndexClosure(index, pick.Composites.Keys, children))
                if (!pick.Composites.ContainsKey(entry.ID))
                    pick.Implied[entry.ID] = entry.Name;
        }
        #endregion

        #region Building it
        /// <summary>What <see cref="Build"/> made.</summary>
        public sealed class Report
        {
            public List<string> Brushes = new List<string>();
            public List<string> Lights = new List<string>();
            public string Spawn;
            public Vector3 SpawnPosition;
            public string NavMeshSeed;
            public List<string> Notes = new List<string>();

            public JObject ToJson() => new JObject()
            {
                ["brushes"] = new JArray(Brushes),
                ["lights"] = new JArray(Lights),
                ["player_start"] = new JObject() { ["name"] = Spawn, ["position"] = McpValues.Vector(SpawnPosition), ["faces"] = "+Z (the north wall)" },
                ["navmesh_seed"] = NavMeshSeed,
            };
        }

        /// <summary>
        /// Build the room in the open level's root, as one undo step ("Build test room"). UI thread. The level needs
        /// SpawnPositionSelect (the imports bring it). Throws <see cref="BrushException"/> with the reason when it cannot.
        /// </summary>
        public static Report Build(LevelContent content, Options options)
        {
            options = options ?? new Options();
            string problem = options.Problem();
            if (problem != null) throw new BrushException(problem);
            Level level = content?.Level ?? throw new BrushException("No level is open.");
            Commands commands = level.Commands;
            Composite root = commands.EntryPoints[0];
            Composite spawn = commands.Entries.FirstOrDefault(o => o != null && McpScript.NormalisePath(o.name).EndsWith("SpawnPositionSelect", StringComparison.OrdinalIgnoreCase))
                ?? throw new BrushException(level.Name + " has no SpawnPositionSelect composite for the player start: the test room imports it from " + Imports[1].Level + ".");

            Report report = new Report();
            object mark = UndoStack.Current.Mark();
            try
            {
                BuildInto(level, commands, root, spawn, options, report);
            }
            finally
            {
                //Every step it made - all of them, or as far as it got before refusing - is one step to undo
                int steps = UndoStack.Current.StepsSince(mark);
                if (steps > 1) UndoStack.Current.Collapse(steps, "Build test room");
            }
            return report;
        }

        private static void BuildInto(Level level, Commands commands, Composite root, Composite spawn, Options options, Report report)
        {
            float w = options.Width / 2, l = options.Length / 2, h = options.Height, t = Thickness;

            //The shell: the floor and ceiling run under and over the walls; the north and south walls the full width, east and
            //west between them. Each box's inside face is the room's: grid lines meet at the corners (a line every metre)
            void Box(string name, float x0, float y0, float z0, float x1, float y1, float z1, string collision = "solid")
            {
                Vector3 size = new Vector3(x1 - x0, y1 - y0, z1 - z0);
                cTransform placement = new cTransform(new Vector3((x0 + x1) / 2, y0, (z0 + z1) / 2), Vector3.Zero);
                BrushDefinition brush = BrushDefinition.NewBox(new Vector3(-size.X / 2, 0, -size.Z / 2), new Vector3(size.X / 2, size.Y, size.Z / 2), null);
                brush.UvOrigin = BrushService.WorldAlignedOrigin(commands, root, placement);
                brush.Collision = collision;
                FunctionEntity made = BrushService.Create(level, root, brush, placement, FreeName(commands, root, name), "Create " + name, report.Notes);
                report.Brushes.Add(commands.Utils.GetEntityName(root, made));
            }
            Box("Room_Floor", -w - t, -t, -l - t, w + t, 0, l + t);
            //The ceiling is ballistic only: as solid its top is walkable, and the navmesh bake (whose seed filter ignores seeds
            //reaching fewer than 50 polygons) keeps that island instead of the floor, putting the player on the roof
            Box("Room_Ceiling", -w - t, h, -l - t, w + t, h + t, l + t, "ballistic");
            Box("Room_Wall_North", -w - t, 0, l, w + t, h, l + t);
            Box("Room_Wall_South", -w - t, 0, -l - t, w + t, h, -l);
            Box("Room_Wall_East", w, 0, -l, w + t, h, l);
            Box("Room_Wall_West", -w - t, 0, -l, -w, h, l);

            //The lights and the player start, as add_light and set_player_start make them
            report.SpawnPosition = new Vector3(0, 0.1f, 0);
            List<(string Name, Vector3 Position, Vector3 Colour, float Start, float End)> lights = options.Lights ? LightPlan(options) : new List<(string, Vector3, Vector3, float, float)>();
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Test room lights, player start and navmesh seed", root, edit =>
            {
                foreach ((string name, Vector3 position, Vector3 colour, float start, float end) in lights)
                {
                    FunctionEntity light = edit.AddFunction(root, FunctionType.LightReference, FreeName(commands, root, name));
                    edit.SetParameter(root, light, "position", new JObject() { ["position"] = McpValues.Vector(position), ["rotation"] = McpValues.Vector(Vector3.Zero) }, allowCustom: true);
                    edit.SetParameter(root, light, "type", "OMNI", allowCustom: true);
                    edit.SetParameter(root, light, "colour", McpValues.Vector(colour), allowCustom: true);
                    edit.SetParameter(root, light, "intensity_multiplier", Intensity, allowCustom: true);
                    edit.SetParameter(root, light, "end_attenuation", Math.Round(end, 2), allowCustom: true);
                    edit.SetParameter(root, light, "start_attenuation", Math.Round(start, 2), allowCustom: true);
                    edit.SetParameter(root, light, "light_on_reset", true, allowCustom: true);
                    edit.SetParameter(root, light, "is_specular", true, allowCustom: true);
                    edit.SetParameter(root, light, "radiosity_multiplier", 1.0, allowCustom: true);
                    report.Lights.Add(commands.Utils.GetEntityName(root, light));
                }

                //In the middle of the floor, facing +Z (the north wall); the only spawn a fresh start uses
                FunctionEntity playerStart = edit.AddInstance(root, spawn, null);
                edit.SetParameter(root, playerStart, "position", new JObject() { ["position"] = McpValues.Vector(report.SpawnPosition), ["rotation"] = McpValues.Vector(Vector3.Zero) });
                edit.SetParameter(root, playerStart, "spawn_on_reset", true, allowCustom: true);
                foreach (Composite composite in commands.Entries.Where(o => o != null))
                    foreach (FunctionEntity other in composite.functions.Where(o => o != playerStart && o.function == spawn.shortGUID).ToList())
                        if ((other.GetParameter(SpawnOnReset)?.content as cBool)?.value != false)
                            edit.SetParameter(composite, other, "spawn_on_reset", false, allowCustom: true);
                report.Spawn = commands.Utils.GetEntityName(root, playerStart);

                //A navmesh seed on the floor: the walkable area the room is for (a level without one keeps only its largest island;
                //the room's own walls and floors added later stay with it while they connect)
                FunctionEntity seed = edit.AddFunction(root, FunctionType.NavMeshReachabilitySeedPoint, FreeName(commands, root, "Room_NavMeshSeed"));
                edit.SetParameter(root, seed, "position", new JObject() { ["position"] = McpValues.Vector(new Vector3(0, 0.05f, 0)), ["rotation"] = McpValues.Vector(Vector3.Zero) }, allowCustom: true);
                report.NavMeshSeed = commands.Utils.GetEntityName(root, seed);
            }, show: false);
            report.Notes.AddRange(outcome.Notes);
        }

        /// <summary>The ceiling lights: a grid about <see cref="LightSpacing"/> apart, just under the ceiling, warm and cool in turn.</summary>
        public static List<(string Name, Vector3 Position, Vector3 Colour, float Start, float End)> LightPlan(Options options)
        {
            float spacing = Math.Max(LightSpacing, (float)Math.Sqrt(options.Width * options.Length / MaximumLights));
            int nx = Math.Max(1, (int)Math.Round(options.Width / spacing)), nz = Math.Max(1, (int)Math.Round(options.Length / spacing));
            while (nx * nz > MaximumLights) { if (nx >= nz) nx--; else nz--; }
            float cellX = options.Width / nx, cellZ = options.Length / nz;
            float y = Math.Max(options.Height * 0.6f, options.Height - 0.35f);
            float reach = (float)Math.Sqrt(cellX * cellX / 4 + cellZ * cellZ / 4 + y * y);
            List<(string, Vector3, Vector3, float, float)> plan = new List<(string, Vector3, Vector3, float, float)>();
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    Vector3 position = new Vector3(-options.Width / 2 + cellX * (i + 0.5f), y, -options.Length / 2 + cellZ * (j + 0.5f));
                    Vector3 colour = nx * nz == 1 ? White : ((i + j) % 2 == 0 ? Warm : Cool);
                    plan.Add(("Room_Light_" + (plan.Count + 1), position, colour, StartOfR * reach, EndOfR * reach));
                }
            }
            return plan;
        }

        //The name, or the name numbered (_2, _3...) when the composite already has an entity called it
        private static string FreeName(Commands commands, Composite composite, string name)
        {
            HashSet<string> taken = new HashSet<string>(composite.GetEntities().Select(o => commands.Utils.GetEntityName(composite, o)), StringComparer.OrdinalIgnoreCase);
            if (!taken.Contains(name)) return name;
            for (int i = 2; ; i++)
                if (!taken.Contains(name + "_" + i))
                    return name + "_" + i;
        }
        #endregion

        #region After Create Level
        /// <summary>
        /// Create Level's half: once <paramref name="levelId"/> has opened in the editor (it was just asked to), build the room
        /// in it and Save &amp; Build. Gives up quietly if another level is opened instead, and says so if the build fails.
        /// </summary>
        public static void BuildWhenOpen(string levelId, Options options)
        {
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer() { Interval = 500 };
            Stopwatch waited = Stopwatch.StartNew();
            Stopwatch elsewhere = null;
            void Stop()
            {
                timer.Stop();
                timer.Dispose();
            }
            timer.Tick += (sender, e) =>
            {
                CommandsEditor editor = Singleton.Editor;
                if (editor == null || editor.IsDisposed)
                {
                    Stop();
                    return;
                }
                if (editor.IsLevelLoadInProgress || editor.IsLevelLoaderRunning)
                    return;
                if (!McpEditor.IsReady(levelId))
                {
                    //Another level open, and still so a moment later (the viewer can clear the load flag early); or very slow
                    string open = editor.CompositeBrowser?.Content?.Level?.Name;
                    bool other = open != null && !string.Equals(open, levelId, StringComparison.OrdinalIgnoreCase);
                    if (other && elsewhere == null) elsewhere = Stopwatch.StartNew();
                    if (!other) elsewhere = null;
                    if ((elsewhere != null && elsewhere.Elapsed > TimeSpan.FromSeconds(5)) || waited.Elapsed > TimeSpan.FromMinutes(15))
                        Stop();
                    return;
                }
                //Never under a box the user is answering (the load's own questions, say)
                try { McpDialogs.ThrowIfUserModal(); }
                catch (McpError) { return; }
                Stop();

                Report report;
                try
                {
                    report = Build(editor.CompositeBrowser.Content, options);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The level was created, but its test room could not be built:\n\n" + ex.Message, "Test room", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                foreach (string note in report.Notes.Distinct())
                    Debug.Log("TestRoom", note);
                editor.SaveLevel(true);
            };
            timer.Start();
        }
        #endregion

        private static string Num(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
