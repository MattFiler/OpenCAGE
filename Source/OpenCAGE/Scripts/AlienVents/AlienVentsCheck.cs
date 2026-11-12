using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE
{
    /// <summary>Something about a level's vents that stops the alien using them in game.</summary>
    internal sealed class VentIssue
    {
        /// <summary>no_sheet, colinear, close_together, not_baked, off_navmesh, no_navmesh.</summary>
        public string Kind;
        /// <summary>Stable from run to run (a Level Check dismissal is kept against it).</summary>
        public string Key;
        public LevelCheckSeverity Severity;
        public string Title;
        public string Text;
        /// <summary>The network it is about (-1: none in particular).</summary>
        public int Network = -1;
        public List<PlacedVent> Vents = new List<PlacedVent>();
    }

    /// <summary>What the check found: the vents, each network's sheet, the saved navmesh's backstage, the issues.</summary>
    internal sealed class VentReport
    {
        public List<PlacedVent> Vents = new List<PlacedVent>();
        public List<AlienVents.Sheet> Sheets = new List<AlienVents.Sheet>();
        public AlienVents.Baked Baked;
        public List<VentIssue> Issues = new List<VentIssue>();
    }

    internal static partial class AlienVents
    {
        /// <summary>
        /// Check a level's vents the way the navmesh baker and the game use them: per network, 3 or more vents not in a line (or no backstage
        /// sheet is baked) spread over more than 10 m (the breakout goes to a vent that far away); per vent, a Backstage link in the saved
        /// navmesh that starts on the alien's ground navmesh. Issues cover only vents the user placed or changed. Any thread; only reads.
        /// </summary>
        public static VentReport Check(Level level, Func<Composite, bool> users = null)
        {
            VentReport report = new VentReport();
            if (level?.Commands == null) return report;
            report.Vents = Survey(level.Commands, users);
            if (report.Vents.Count == 0) return report;
            report.Baked = MarkBaked(level, report.Vents);

            foreach (IGrouping<int, PlacedVent> network in report.Vents.GroupBy(o => o.Network).OrderBy(o => o.Key))
            {
                List<PlacedVent> vents = network.ToList();
                Sheet sheet = SheetFor(network.Key, vents.Select(o => o.Bottom).ToList());
                report.Sheets.Add(sheet);
                if (!vents.Any(o => o.User)) continue;
                string which = report.Vents.Select(o => o.Network).Distinct().Count() > 1 || network.Key != 0 ? "Network " + network.Key + "'s" : "The level's";
                if (!sheet.Builds)
                {
                    report.Issues.Add(new VentIssue()
                    {
                        Kind = "no_sheet", Key = "network:" + network.Key, Network = network.Key, Severity = LevelCheckSeverity.Broken, Vents = vents,
                        Title = which + " alien vents make no backstage (" + vents.Count + " vent" + (vents.Count == 1 ? "" : "s") + ")",
                        Text = which + " " + vents.Count + " vent" + (vents.Count == 1 ? "" : "s") + " cannot carry the alien anywhere: the navmesh baker triangulates the alien's backstage 6 m over 3 or more vents of one network (Network_ID), not in a line (" + sheet.Warning + "). " +
                            "The alien climbs into a vent with no backstage to move along. Add vents (File > Alien Vents..., or MCP add_alien_vents) until the network has 3 or more spread over the area, then Save & Build with the navmesh baker.",
                    });
                    continue;
                }
                if (sheet.Colinear)
                    report.Issues.Add(new VentIssue()
                    {
                        Kind = "colinear", Key = "colinear:" + network.Key, Network = network.Key, Severity = LevelCheckSeverity.Warning, Vents = vents,
                        Title = which + " alien vents stand in a line",
                        Text = which + " vents all stand on one line, so the baker makes the alien's backstage a 1 m strip along it rather than a sheet over the area: the alien can only travel along that line. Add a vent off the line.",
                    });
                if (sheet.Spread < BreakoutDistance)
                    report.Issues.Add(new VentIssue()
                    {
                        Kind = "close_together", Key = "close:" + network.Key, Network = network.Key, Severity = LevelCheckSeverity.Warning, Vents = vents,
                        Title = which + " alien vents are all within " + BreakoutDistance + " m of each other",
                        Text = which + " vents are at most " + Math.Round(sheet.Spread, 1) + " m apart. When the alien breaks out (flamed, sent away) it goes backstage to a vent more than " + BreakoutDistance + " m from where it went in, so it has nowhere to come out. Spread them over the area the alien hunts.",
                    });
            }

            List<PlacedVent> mine = report.Vents.Where(o => o.User).ToList();
            if (mine.Count == 0) return report;
            if (!report.Baked.HasNavmesh)
            {
                report.Issues.Add(new VentIssue()
                {
                    Kind = "no_navmesh", Key = "no_navmesh", Severity = LevelCheckSeverity.Broken, Vents = mine,
                    Title = "The level has alien vents but no navmesh",
                    Text = "The vents only work through the navmesh (the backstage sheet and a link up into each vent are baked into it): Save & Build with the navmesh baker on.",
                });
                return report;
            }
            List<PlacedVent> unbaked = mine.Where(o => o.Baked == false).ToList();
            if (unbaked.Count != 0)
                report.Issues.Add(new VentIssue()
                {
                    Kind = "not_baked", Key = "not_baked", Severity = LevelCheckSeverity.Broken, Vents = unbaked,
                    Title = unbaked.Count + " alien vent" + (unbaked.Count == 1 ? " is" : "s are") + " not in the navmesh yet",
                    Text = "The saved navmesh has no backstage link at " + (unbaked.Count == 1 ? "this vent" : "these vents") + " (" + report.Baked.LinkBottoms.Count + " Backstage link(s), " + report.Baked.SheetPolygons + " backstage polygon(s) in it), so the alien cannot use " + (unbaked.Count == 1 ? "it" : "them") + ": " +
                        "Save & Build with the navmesh baker on (a build that skips the navmesh keeps the old one).",
                });
            List<PlacedVent> off = mine.Where(o => o.OnNavmesh == false).ToList();
            if (off.Count != 0)
                report.Issues.Add(new VentIssue()
                {
                    Kind = "off_navmesh", Key = "off_navmesh", Severity = LevelCheckSeverity.Broken, Vents = off,
                    Title = off.Count + " alien vent" + (off.Count == 1 ? " stands" : "s stand") + " off the navmesh",
                    Text = "The alien walks to a vent's node (its floor point, the vent's height under the vent) to climb in, and drops out onto it: " + (off.Count == 1 ? "this one's is" : "these ones' are") + " more than 0.5 m from any floor the alien's navmesh covers, so its link up joins nothing. " +
                        "Move " + (off.Count == 1 ? "it" : "them") + " so the node stands on walkable floor (the vent's origin is the vent itself: its kind's height over that floor), then Save & Build.",
                });
            return report;
        }

        /// <summary>The vents as a short list for a message: "where (x, y, z)".</summary>
        public static string List(IEnumerable<PlacedVent> vents, int max = 4)
        {
            List<PlacedVent> all = vents.ToList();
            return string.Join("; ", all.Take(max).Select(o => o.Where + " " + MCP.McpRegion.Format(o.Bottom))) + (all.Count > max ? "; and " + (all.Count - max) + " more" : "");
        }
    }
}
