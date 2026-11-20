using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE
{
    /// <summary>How the tool's spots for one kind compare with the markup a level already has.</summary>
    internal sealed class MarkupComparison
    {
        public MarkupKind Kind;
        public int Level, Ours;
        /// <summary>The navmesh round the level's own markup the comparison covers (m²).</summary>
        public double Area;
        /// <summary>A spot counts as matched within this distance (and, for high cover, facing within 30 degrees).</summary>
        public float Within;
        /// <summary>Share of the level's markup with one of ours that close, and of ours with one of the level's.</summary>
        public double Recall, Precision;
        /// <summary>The recall a set as dense as ours spread at random would get: what the match share means.</summary>
        public double Chance;
        public double F1 => Recall + Precision <= 0 ? 0 : 2 * Recall * Precision / (Recall + Precision);
        /// <summary>Median distance from each to the nearest of its own kind, and median clearance from the floor's edge (m).</summary>
        public double LevelSpacing, OurSpacing, LevelClearance, OurClearance;
        /// <summary>Corpse traps: the androids compared round, and the mean distance from each of the level's to the nearest of ours (m).</summary>
        public int Androids;
        public double MeanNearest = double.NaN;
        public bool Faces => Kind == AIMarkup.HighCover || Kind == AIMarkup.LowCover || Kind == AIMarkup.CorpseTrap;
        public string Summary()
        {
            string match = "Within " + Within + " m" + (Faces ? " facing the same way" : "") + ": " + Pct(Recall) + " of the level's have one of ours, " + Pct(Precision) + " of ours have one of the level's" +
                " (F1 " + Pct(F1) + "; " + (Kind == AIMarkup.CorpseTrap ? "placed at random round the android: " : "spread at random: ") + Pct(Chance) + ").";
            if (Kind == AIMarkup.CorpseTrap)
                return Kind.NamePrefix + ": the level has " + Level + " round " + Androids + " android(s) playing dead, the tool finds " + Ours + " (" + Ratio() + "). " + match +
                    (double.IsNaN(MeanNearest) ? "" : " The nearest of ours to each of the level's: " + Math.Round(MeanNearest, 2) + " m on average.");
            string s = Kind.NamePrefix + ": the level has " + Level + ", the tool finds " + Ours + " in the same " + Math.Round(Area) + " m² (" + Ratio() + "). " + match;
            s += " Spacing " + Math.Round(LevelSpacing, 1) + " m against " + Math.Round(OurSpacing, 1) + " m";
            if (!Faces) s += ", clearance " + Math.Round(LevelClearance, 2) + " m against " + Math.Round(OurClearance, 2) + " m";
            return s + ".";
        }
        private string Ratio() => Level == 0 ? "-" : "x" + Math.Round((double)Ours / Level, 2);
        private static string Pct(double v) => Math.Round(v * 100) + "%";
    }

    /// <summary>Whether one piece of markup stands on the navmesh its NPCs walk (null: no navmesh to look at).</summary>
    internal sealed class MarkupStanding
    {
        public PlacedMarkup Markup;
        public bool? OnNavmesh;
    }

    internal static partial class AIMarkup
    {
        /// <summary>Each piece of markup against the saved navmesh: within 0.6 m in plan and 0.75 m up or down of floor its NPCs walk. Any thread.</summary>
        public static List<MarkupStanding> OnNavmesh(Level level, List<PlacedMarkup> markup)
        {
            List<MarkupStanding> result = new List<MarkupStanding>();
            CathodeLib.NavMesh.NavigationMeshQuery query = AlienVents.QueryOf(level, out NavigationMesh mesh);
            if (query == null) return result;
            foreach (PlacedMarkup m in markup)
            {
                CathodeLib.NavMesh.NavigationMeshQuery.Nearest at = query.FindNearest(m.Position, new CathodeLib.NavMesh.NavigationMeshQuery.Options() { Classes = m.Kind.Classes, IncludeDisabled = true, Extents = new Vector3(1f, 1.5f, 1f) });
                bool on = at.Found && !AlienVents.IsSheet(mesh, at.Polygon) && Flat(at.Point - m.Position) <= 0.6f && Math.Abs(at.Point.Y - m.Position.Y) <= 0.75f;
                result.Add(new MarkupStanding() { Markup = m, OnNavmesh = on });
            }
            return result;
        }

        /// <summary>Median plan distance from each point to the nearest other on the same floor.</summary>
        public static double MedianSpacing(List<Vector3> points) =>
            Median(points.Select(o => (double)points.Where(q => q != o && Math.Abs(q.Y - o.Y) < 2f).Select(q => Flat(q - o)).DefaultIfEmpty(float.NaN).Min()));

        /// <summary>
        /// The retail check: for each kind the level already places, find spots again round that markup (within <see cref="ArenaRadius"/>
        /// of it, the level's own markup ignored) and score them against it. Changes nothing. UI thread or a tool's thread.
        /// </summary>
        public static List<MarkupComparison> Compare(McpCall call, LevelContent content, MarkupOptions options)
        {
            List<PlacedMarkup> existing = McpEditor.UI(() => Survey(content.Level.Commands, o => true));
            List<MarkupComparison> results = new List<MarkupComparison>();
            foreach (MarkupKind kind in Kinds.Where(options.Wants))
            {
                List<Vector3> theirs = existing.Where(o => o.Kind == kind).Select(o => o.Position).ToList();
                if (theirs.Count == 0) continue;
                List<float> theirYaw = existing.Where(o => o.Kind == kind).Select(o => o.Yaw).ToList();
                bool trap = kind == CorpseTrap;
                MarkupOptions only = new MarkupOptions()
                {
                    Search = kind == SearchJob, Sweep = kind == AreaSweepJob, HighCover = kind == HighCover, LowCover = kind == LowCover, CorpseTraps = trap, IgnoreExistingCorpseTraps = true,
                    Spacing = options.Spacing, Seed = options.Seed, KeepClearOfExisting = false, IndoorsOnly = options.IndoorsOnly,
                    RegionMin = options.RegionMin, RegionMax = options.RegionMax,
                    Within = (k, p) => theirs.Any(o => Near(o, p, trap ? CorpseTrapReach : ArenaRadius)),
                };
                MarkupProposal proposal = Propose(call, content, only);
                List<MarkupSpot> ours = proposal.Spots.Where(o => o.Kind == kind).ToList();
                MarkupComparison c = new MarkupComparison() { Kind = kind, Level = theirs.Count, Ours = ours.Count, Within = trap ? 0.5f : kind == HighCover || kind == LowCover ? 1.0f : 2.5f };
                bool Matches(Vector3 a, float ya, Vector3 b, float yb) => Near(a, b, c.Within) && (!c.Faces || AngleBetween(ya, yb) <= 30f);
                c.Recall = theirs.Count == 0 ? 0 : (double)Enumerable.Range(0, theirs.Count).Count(i => ours.Any(o => Matches(theirs[i], theirYaw[i], o.Position, o.Yaw))) / theirs.Count;
                c.Precision = ours.Count == 0 ? 0 : (double)ours.Count(o => Enumerable.Range(0, theirs.Count).Any(i => Matches(theirs[i], theirYaw[i], o.Position, o.Yaw))) / ours.Count;
                if (trap)
                {
                    //A spot on the ring round the android lands within reach of a given one this share of the time
                    c.Androids = ours.Select(o => o.Guards).Distinct().Count();
                    double each = 2 * Math.Asin(Math.Min(1, c.Within / (2 * CorpseTrapDistance))) / Math.PI;
                    c.Chance = 1 - Math.Pow(1 - each, ours.Count / (double)Math.Max(1, c.Androids));
                    if (ours.Count != 0) c.MeanNearest = theirs.Average(t => ours.Min(o => (double)Flat(o.Position - t)));
                    results.Add(c);
                    continue;
                }
                c.Area = Math.Max(1, AreaRound(content, theirs));
                double density = ours.Count / c.Area;
                c.Chance = 1 - Math.Exp(-density * Math.PI * c.Within * c.Within);
                //A random facing is within 30 degrees one time in six
                if (c.Faces) c.Chance *= 60.0 / 360.0;
                c.LevelSpacing = MedianSpacing(theirs);
                c.OurSpacing = MedianSpacing(ours.Select(o => o.Position).ToList());
                c.OurClearance = Median(ours.Select(o => (double)o.Clearance));
                c.LevelClearance = ClearanceMedian(content, theirs);
                results.Add(c);
            }
            return results;
        }

        private static bool Near(Vector3 a, Vector3 b, float within) => Math.Abs(a.Y - b.Y) < 2f && (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z) <= within * within;

        private static float AngleBetween(float a, float b)
        {
            float d = Math.Abs(a - b) % 360f;
            return d > 180f ? 360f - d : d;
        }

        private static double Median(IEnumerable<double> values)
        {
            List<double> v = values.Where(o => !double.IsNaN(o)).OrderBy(o => o).ToList();
            return v.Count == 0 ? double.NaN : v[v.Count / 2];
        }

        /* The NPCs' standing floor within ArenaRadius of the points (m²), from samples */
        private static double AreaRound(LevelContent content, List<Vector3> points)
        {
            CathodeLib.NavMesh.NavigationMeshQuery query = AlienVents.QueryOf(content.Level, out NavigationMesh mesh);
            if (query == null) return 0;
            Floor floor = FloorFor(mesh, query, NpcClasses);
            List<(Vector3 Point, int Polygon)> samples = Sample(floor, 1f, 400000, new Random(5), null, null);
            return samples.Count(s => points.Any(o => Near(o, s.Point, ArenaRadius))) * 1.0;
        }

        /* Median clearance of the level's own markup, measured as the tool measures its spots */
        private static double ClearanceMedian(LevelContent content, List<Vector3> points)
        {
            CathodeLib.NavMesh.NavigationMeshQuery query = AlienVents.QueryOf(content.Level, out NavigationMesh mesh);
            if (query == null) return double.NaN;
            Floor floor = FloorFor(mesh, query, NpcClasses);
            return Median(points.Select(o => (double)floor.Edges.Nearest(o + new Vector3(0, 0.05f, 0), 5f)));
        }
    }
}
