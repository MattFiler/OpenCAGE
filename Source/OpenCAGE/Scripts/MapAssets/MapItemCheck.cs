using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.MapAssets
{
    /// <summary>A level's map floors as its script sets them, and its map items' floors checked against them.</summary>
    public static class MapItemCheck
    {
        private static readonly string[] _slots = { "keyframe", "keyframe1", "keyframe2", "keyframe3", "keyframe4", "keyframe5" };

        /// <summary>
        /// The floors of the root composite's MapAnchor, each with the heights of the PlayerTriggerBoxes whose on_entered sets it
        /// (Map_Floor_Change). Empty when there is no anchor, or a floor has no such box (the floors' heights aren't known then).
        /// </summary>
        public static List<MapFloor> FloorsOf(Commands commands)
        {
            List<MapFloor> floors = new List<MapFloor>();
            Composite root = commands?.EntryPoints?[0];
            if (root == null) return floors;
            FunctionEntity anchor = root.functions.FirstOrDefault(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.MapAnchor);
            if (anchor == null) return floors;
            List<string> keyframes = _slots.Select(s => (anchor.GetParameter(ShortGuidUtils.Generate(s))?.content as cString)?.value).Where(o => !string.IsNullOrEmpty(o)).ToList();
            ShortGuid floorName = ShortGuidUtils.Generate("floor_name"), entered = ShortGuidUtils.Generate("on_entered"), setTrue = ShortGuidUtils.Generate("set_true");
            ShortGuid half = ShortGuidUtils.Generate("half_dimensions");
            foreach (string key in keyframes)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (FunctionEntity change in root.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.Map_Floor_Change
                    && string.Equals((o.GetParameter(floorName)?.content as cString)?.value, key, StringComparison.Ordinal)))
                    foreach (FunctionEntity box in root.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.PlayerTriggerBox
                        && o.childLinks.Any(l => l.thisParamID == entered && l.linkedEntityID == change.shortGUID && l.linkedParamID == setTrue)))
                    {
                        float y = (box.GetParameter(ShortGuids.position)?.content as cTransform)?.position.Y ?? 0;
                        float h = (box.GetParameter(half)?.content as cVector3)?.value.Y ?? 0;
                        lo = Math.Min(lo, y - h); hi = Math.Max(hi, y + h);
                    }
                if (lo > hi) return new List<MapFloor>();
                floors.Add(new MapFloor() { Keyframe = key, Name = key, From = lo, To = hi, Level = (lo + hi) / 2 });
            }
            return floors;
        }
    }
}

namespace OpenCAGE
{
    using OpenCAGE.MapAssets;

    /// <summary>Map items whose map_keyframe isn't the floor they stand on: their icon shows on the wrong floor of the Tab map, or none.</summary>
    internal sealed class MapItemFloorRule : LevelCheckRule
    {
        public override string Id => "map_item_floor";
        public override string Name => "Map item on the wrong floor";
        public override string About => "A map item (door, terminal, pickup... icon on the Tab map) whose map_keyframe is not the map floor it stands on, by the level's MapAnchor floors and floor boxes.";

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            List<MapFloor> floors = MapItemCheck.FloorsOf(context.Commands);
            if (floors.Count == 0) return;
            Composite root = context.Commands.EntryPoints[0];
            List<MapItemFloor> wrong = MapAssetGenerator.MapItems(context.Commands, floors).Where(o => o.Wrong).ToList();
            foreach (MapItemFloor item in wrong)
            {
                MapItemFloor fixing = item;
                List<MapItemPlacement> off = item.Placements.Where(o => o.Keyframe != null && o.Keyframe != o.Was).ToList();
                bool simple = !item.Mixed && off.All(o => o.Alias == null);
                MapItemPlacement first = off[0];
                found.Add(new LevelCheckFinding()
                {
                    Key = LevelCheckScript.Key(item.Composite, item.Entity),
                    Severity = LevelCheckSeverity.Warning,
                    Title = context.Where(item.Composite, item.Entity) + " shows on the wrong map floor",
                    Text = simple
                        ? "Its map_keyframe is '" + first.Was + "' but it stands on the floor '" + first.Keyframe + "' (" + Math.Round(first.World.Y, 1) + " m up), so its icon shows on another floor of the Tab map, or none. " +
                          "Set map_keyframe to " + first.Keyframe + " (File > Generate Map Assets sets every map item's floor)."
                        : off.Count + " of its " + item.Placements.Count + " placements show on another floor of the Tab map than the one they stand on (" +
                          string.Join(", ", off.Take(4).Select(o => "'" + o.Was + "' at " + Math.Round(o.World.Y, 1) + " m up, on '" + o.Keyframe + "'")) + (off.Count > 4 ? "..." : "") +
                          "). Its composite is placed on more than one floor (or overridden per placement), so each needs its own map_keyframe: an alias in the root composite.",
                    Sources = { context.Source(item.Composite, item.Entity) },
                    Fix = new LevelCheckFix()
                    {
                        Label = simple ? "Set map_keyframe to " + first.Keyframe : "Set map_keyframe per placement",
                        Detail = simple ? "Sets " + fixing.Name + "'s map_keyframe to " + first.Keyframe + ", the floor it stands on."
                                        : "Sets the map_keyframe of each of " + fixing.Name + "'s placements to the floor it stands on, by an alias in the root composite.",
                        Apply = fromTool => LevelCheckFix.Script("Map item floor", simple ? fixing.Composite : root, fromTool, edit =>
                        {
                            int n = MapAssetGenerator.SetFloors(edit, root, fixing);
                            return "Set " + fixing.Name + "'s map_keyframe (" + n + " setting" + (n == 1 ? "" : "s") + ") to the floor it stands on.";
                        }),
                    },
                });
            }
        }
    }
}
