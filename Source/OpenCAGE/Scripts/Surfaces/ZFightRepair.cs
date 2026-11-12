using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using AssetKind = CathodeLib.AssetModificationInfoTable.AssetKind;
using Submesh = CATHODE.Models.CS2.Component.LOD.Submesh;

namespace OpenCAGE.Surfaces
{
    /// <summary>Which of the two moves: push the one lying on top out, or sink the one underneath back.</summary>
    public enum ZFixAction { Auto, Push, Sink }

    public sealed class ZFixOptions
    {
        public ZFixAction Action = ZFixAction.Auto;
        /// <summary>How far in front (m) the top surface ends up everywhere over the overlap: 8 mm by default.</summary>
        public float Clearance = 0.008f;
        /// <summary>A mesh mostly in the overlap (a decal, a panel) moves whole rather than just its overlapping triangles.</summary>
        public bool WholeMesh = true;
        /// <summary>Vertex moves only: search again with the moves made and move what still overlaps (a mesh crossing another at a shallow
        /// angle only hands its overlap on to the next triangles out), up to this many times. Placement moves are rigid: no rounds.</summary>
        public int Rounds = 3;
        /// <summary>A placement is never moved further than this (m) to clear a crossing: deeper than that, a person decides.</summary>
        public float MaxPlacementMove = 0.25f;
        /// <summary>Of a placement inside a composite: write the move on the entity in its composite when the other surface is in that
        /// composite too (every placement has the same fight); otherwise an alias moves just this placement.</summary>
        public bool InComposite = true;
    }

    /// <summary>One thing a fix moves: a placement (its position), or vertices of a model's submesh.</summary>
    public sealed class ZMove
    {
        public bool IsPlacement => Placement != null;
        public ZPlacement Placement;
        /// <summary>Where the placement's move is written: "position", "override", "composite" (its own composite: every placement) or "alias" (a new or reused alias in the root, this placement only).</summary>
        public string WrittenTo;
        /// <summary>World metres (placement), as planned.</summary>
        public Vector3 Delta;
        /// <summary>Each fight's ask, before they were put together, and that fight's overlap (m2).</summary>
        public List<Vector3> Asked = new List<Vector3>();
        public List<double> AskedArea = new List<double>();

        public Models.CS2 Model;
        public Submesh Submesh;
        public string SubmeshLabel;
        /// <summary>Per vertex, model space metres as planned.</summary>
        public Dictionary<int, Vector3> Vertices = new Dictionary<int, Vector3>();
        /// <summary>Per vertex, as the model's position grid stores it.</summary>
        public Dictionary<int, Vector3> Realised = new Dictionary<int, Vector3>();
        public bool WholeMesh;
        /// <summary>How many placements draw the model (a vertex move shows in each).</summary>
        public int ModelPlacements;

        public List<int> Fights = new List<int>();
        public string Label;
        /// <summary>Why it cannot be made as planned (null when it can).</summary>
        public string Problem;

        public float Distance => IsPlacement ? Delta.Length() : (Realised.Count != 0 ? Realised.Values.Max(o => o.Length()) : Vertices.Values.DefaultIfEmpty().Max(o => o.Length()));
    }

    /// <summary>What a fix would do, and what the search finds once it is done (the preview).</summary>
    public sealed class ZFixPlan
    {
        public ZScan Scan;
        public ZFixOptions Options;
        public List<ZFight> Fights = new List<ZFight>();
        public List<ZMove> Moves = new List<ZMove>();
        public List<string> Notes = new List<string>();
        /// <summary>Per fight: what is left of its pair of surfaces' overlap (every way round) once the moves are made (0 = gone).</summary>
        public Dictionary<int, double> AreaAfter = new Dictionary<int, double>();
        /// <summary>Per pair of surfaces (scan indices): what is left of their overlap.</summary>
        public Dictionary<(int, int), double> PairLeft = new Dictionary<(int, int), double>();
        /// <summary>Fights the moves would make (a pushed panel now on something else).</summary>
        public List<ZFight> Made = new List<ZFight>();
        /// <summary>Every fight the check found with the moves made (its surfaces the scan's own).</summary>
        public List<ZFight> After = new List<ZFight>();
        /// <summary>How many extra rounds the vertex moves took.</summary>
        public int RoundsTaken;
        public double AreaBefore => Fights.Sum(o => o.Area);
        public double AreaLeft => PairLeft.Values.Sum();
        public bool Verified;
        /// <summary>Per fight: which surface moves and which way (for pictures): +1 A pushed out, -1 B sunk back.</summary>
        public Dictionary<int, (bool sink, float distance)> How = new Dictionary<int, (bool, float)>();
    }

    /// <summary>
    /// Plans and makes z-fighting repairs: the surface on top pushed out along the shared normal (or the one underneath sunk back) until
    /// it is <see cref="ZFixOptions.Clearance"/> in front everywhere over the overlap. Between two placements the placement moves
    /// (its position, the alias already moving it, or a new alias for that placement alone); inside one model its vertices move on the
    /// model's own position grid (all copies of a point together, so nothing tears). The plan is checked by searching again with the
    /// moves made (nothing is changed); the fix itself is one undo step.
    /// </summary>
    public static class ZFightRepair
    {
        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public static readonly RefEq<T> Instance = new RefEq<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        #region Planning
        public static ZFixPlan Plan(ZScan scan, IEnumerable<ZFight> fights, ZFixOptions options)
        {
            options = options ?? new ZFixOptions();
            ZFixPlan plan = new ZFixPlan() { Scan = scan, Options = options };
            plan.Fights = fights.Where(o => o != null).Distinct().ToList();
            if (options.Clearance <= scan.Options.Gap)
                plan.Notes.Add("The clearance (" + Mm(options.Clearance) + ") is not more than the search's gap (" + Mm(scan.Options.Gap) + "): a search afterwards still counts them as fighting.");

            Dictionary<int, ZMove> placementMoves = new Dictionary<int, ZMove>();
            Dictionary<Submesh, ZMove> vertexMoves = new Dictionary<Submesh, ZMove>(RefEq<Submesh>.Instance);
            Dictionary<Models.CS2, int> modelUse = scan.LevelMode ? ModelPlacements(scan) : null;

            //What each vertex target is asked for, fight by fight: decided once every fight is in
            Dictionary<Submesh, List<(ZFight fight, ZSurface moving, Vector3 local, bool sink)>> vertexAsks = new Dictionary<Submesh, List<(ZFight, ZSurface, Vector3, bool)>>(RefEq<Submesh>.Instance);
            HashSet<string> copiesNoted = new HashSet<string>();
            foreach (ZFight fight in plan.Fights)
            {
                bool sink = options.Action == ZFixAction.Sink;
                ZSurface moving = sink ? fight.B : fight.A;
                ZSurface staying = sink ? fight.A : fight.B;
                ZSurfaceSource source = ZFightScene.SourceOf(moving), other = ZFightScene.SourceOf(staying);
                bool placements = scan.LevelMode && !fight.SameGroup && source?.Placement != null;
                //Two copies of one thing in one place: no move separates them (they lie on each other every way round)
                if (fight.Copies && placements)
                {
                    if (copiesNoted.Add(fight.A.Index + "/" + fight.B.Index))
                        plan.Notes.Add("#" + fight.Id + ": " + fight.A.Label + " and " + fight.B.Label + " are the same shape twice in one place - delete one, unless a script shows one at a time (then they never fight).");
                    continue;
                }
                //Out of the way of the other: A forwards along B's facing (or its own, back to back), B the other way
                Vector3 dir = fight.BackToBack ? (sink ? fight.Normal : -fight.Normal) : (sink ? -fight.Normal : fight.Normal);
                //A placement moves rigidly, so it has to clear the whole of a crossing (its deepest point), not just the band within the gap;
                //vertices move where they overlap, in rounds
                float deepest = placements && !fight.BackToBack ? Math.Min(fight.OffsetMin, fight.DeepestMin) : fight.OffsetMin;
                float distance = options.Clearance - deepest;
                if (distance <= 0)
                {
                    plan.Notes.Add("#" + fight.Id + " is already " + Mm(fight.OffsetMin) + " apart: nothing to move.");
                    continue;
                }
                plan.How[fight.Id] = (sink, distance);
                Vector3 delta = dir * distance;

                if (placements)
                {
                    if (distance > options.MaxPlacementMove)
                    {
                        plan.Notes.Add("#" + fight.Id + ": " + moving.Label + " crosses " + staying.Label + " " + Mm(-deepest) + " deep - more than a move should take (" + Mm(options.MaxPlacementMove) + "): move or rebuild one of them by hand.");
                        plan.How.Remove(fight.Id);
                        continue;
                    }
                    ZPlacement placement = source.Placement;
                    if (!placementMoves.TryGetValue(placement.Index, out ZMove move))
                    {
                        move = new ZMove() { Placement = placement };
                        placementMoves[placement.Index] = move;
                        move.WrittenTo = WhereWritten(placement, other?.Placement, options);
                    }
                    else if (move.WrittenTo == "composite" && WhereWritten(placement, other?.Placement, options) != "composite")
                        move.WrittenTo = WhereWritten(placement, null, options);
                    move.Asked.Add(delta);
                    move.AskedArea.Add(fight.Area);
                    move.Fights.Add(fight.Id);
                    continue;
                }

                //Inside one model: its vertices
                if (source?.Submesh == null || source.Model == null)
                {
                    plan.Notes.Add("#" + fight.Id + ": " + moving.Label + " belongs to no model the level holds, so it cannot be moved.");
                    continue;
                }
                if (fight.Copies && copiesNoted.Add(fight.A.Index + "/" + fight.B.Index))
                    plan.Notes.Add("#" + fight.Id + ": " + fight.A.Label + " and " + fight.B.Label + " are the same shape twice: the fix swells one out of the other; deleting one mesh in the Model Editor is cleaner.");
                Vector3 local = scan.LevelMode && source.Placement != null ? InstanceTransform.DirectionToLocal(source.Placement.World, delta) : delta;
                if (!vertexAsks.TryGetValue(source.Submesh, out List<(ZFight, ZSurface, Vector3, bool)> asks)) vertexAsks[source.Submesh] = asks = new List<(ZFight, ZSurface, Vector3, bool)>();
                asks.Add((fight, moving, local, sink));
                if (!vertexMoves.ContainsKey(source.Submesh))
                    vertexMoves[source.Submesh] = new ZMove()
                    {
                        Model = source.Model,
                        Submesh = source.Submesh,
                        SubmeshLabel = Path.GetFileName(source.Model.Name) + (source.Model.Components.Count > 1 ? " component " + source.Component : "") + " mesh " + source.SubmeshIndex,
                        ModelPlacements = modelUse != null && modelUse.TryGetValue(source.Model, out int uses) ? uses : 0,
                    };
            }

            //Vertex moves: a mesh lying (mostly) on others one way only moves whole; otherwise each overlapping triangle's corners (and
            //every vertex at the same spots) move, a corner asked to go several ways goes all of them - the mesh swells out of the other
            foreach (KeyValuePair<Submesh, List<(ZFight fight, ZSurface moving, Vector3 local, bool sink)>> kv in vertexAsks)
            {
                ZMove vmove = vertexMoves[kv.Key];
                List<(ZFight fight, ZSurface moving, Vector3 local, bool sink)> asks = kv.Value;
                ZSurface moving = asks[0].moving;
                Vector3 first = asks[0].local;
                bool oneWay = asks.All(o => Vector3.Dot(Vector3.Normalize(o.local), Vector3.Normalize(first)) > 0.95f);
                double cover = asks.GroupBy(o => o.fight.A.Index + "/" + o.fight.B.Index).Sum(g => g.First().fight.PairArea) / Math.Max(1e-9, moving.Area);
                if (options.WholeMesh && oneWay && cover >= 0.9)
                {
                    vmove.WholeMesh = true;
                    Vector3 longest = asks.Select(o => o.local).OrderByDescending(o => o.Length()).First();
                    for (int v = 0; v < moving.Positions.Length; v++) vmove.Vertices[v] = longest;
                }
                else
                {
                    foreach ((ZFight fight, ZSurface m, Vector3 local, bool sink) in asks)
                    {
                        Dictionary<int, Vector3> mine = new Dictionary<int, Vector3>();
                        foreach (int v in SameSpots(m, (sink ? fight.TrianglesB : fight.TrianglesA).SelectMany(t => new[] { m.Triangles[t * 3], m.Triangles[t * 3 + 1], m.Triangles[t * 3 + 2] })))
                            vmove.Vertices[v] = vmove.Vertices.TryGetValue(v, out Vector3 had) ? Combine(had, local) : local;
                    }
                }
                vmove.Fights.AddRange(asks.Select(o => o.fight.Id));
            }
            foreach (ZMove move in placementMoves.Values)
            {
                //The biggest overlap leads. Another the same way or across is added in; one asking the OPPOSITE way (a tile lying on another
                //top and bottom: its top wants up, its underside down) is met by going far enough the lead way - it then ends behind
                //instead of in front, which separates it just as well
                int lead = 0;
                for (int i = 1; i < move.Asked.Count; i++) if (move.AskedArea[i] > move.AskedArea[lead]) lead = i;
                Vector3 delta = move.Asked[lead], leadDir = Vector3.Normalize(move.Asked[lead]);
                bool opposite = false;
                for (int i = 0; i < move.Asked.Count; i++)
                {
                    if (i == lead) continue;
                    Vector3 ask = move.Asked[i];
                    if (Vector3.Dot(Vector3.Normalize(ask), leadDir) < -0.3f)
                    {
                        opposite = true;
                        float along = Vector3.Dot(delta, leadDir);
                        if (along < ask.Length()) delta += leadDir * (ask.Length() - along);
                    }
                    else delta = Combine(delta, ask);
                }
                move.Delta = delta;
                if (opposite)
                    plan.Notes.Add(move.Placement.Name + " lies on another surface from opposite sides (e.g. a tile's top and underside): it moves " + Way(delta) + " far enough to clear both, so one pair ends with it behind instead of in front.");
                move.Label = MoveLabel(move);
                plan.Moves.Add(move);
            }
            foreach (ZMove move in vertexMoves.Values)
            {
                Quantise(move);
                move.Label = MoveLabel(move);
                plan.Moves.Add(move);
                if (move.ModelPlacements > 1)
                    plan.Notes.Add(Path.GetFileName(move.Model.Name) + " is drawn by " + move.ModelPlacements + " placements: its vertices move in every one of them.");
            }
            if (plan.Moves.Any(o => o.WrittenTo == "composite"))
                plan.Notes.Add("Moves written inside a composite move that entity in every placement of the composite (the fight is inside the composite, so every placement has it).");
            Verify(plan);
            ExtraRounds(plan, vertexMoves);
            return plan;
        }

        /// <summary>
        /// Vertex moves that left overlap behind (a shallow crossing hands it on to the next triangles out), moved again from where the
        /// check left them: each leftover's moving side goes on by what it still lacks, added to what it already moves.
        /// </summary>
        private static void ExtraRounds(ZFixPlan plan, Dictionary<Submesh, ZMove> vertexMoves)
        {
            ZScan scan = plan.Scan;
            bool sink = plan.Options.Action == ZFixAction.Sink;
            for (int round = 0; round < plan.Options.Rounds && plan.Verified && vertexMoves.Count != 0; round++)
            {
                HashSet<(int, int)> planned = new HashSet<(int, int)>(plan.Fights.Select(o => (Math.Min(o.A.Index, o.B.Index), Math.Max(o.A.Index, o.B.Index))));
                List<ZFight> leftovers = plan.After.Where(f =>
                {
                    ZSurface moving = sink ? f.B : f.A;
                    ZSurfaceSource source = ZFightScene.SourceOf(moving);
                    if (source?.Submesh == null || !vertexMoves.ContainsKey(source.Submesh)) return false;
                    if (scan.LevelMode && !f.SameGroup) return false;
                    return planned.Contains((Math.Min(f.A.Index, f.B.Index), Math.Max(f.A.Index, f.B.Index))) || plan.Made.Contains(f);
                }).ToList();
                if (leftovers.Count == 0) break;
                foreach (ZFight f in leftovers)
                {
                    ZSurface moving = sink ? f.B : f.A;
                    ZSurfaceSource source = ZFightScene.SourceOf(moving);
                    ZMove vmove = vertexMoves[source.Submesh];
                    Vector3 dir = f.BackToBack ? (sink ? f.Normal : -f.Normal) : (sink ? -f.Normal : f.Normal);
                    float distance = plan.Options.Clearance - f.OffsetMin;
                    if (distance <= 0) continue;
                    Vector3 delta = dir * distance;
                    Vector3 local = scan.LevelMode && source.Placement != null ? InstanceTransform.DirectionToLocal(source.Placement.World, delta) : delta;
                    HashSet<int> triangles = sink ? f.TrianglesB : f.TrianglesA;
                    foreach (int v in SameSpots(moving, triangles.SelectMany(t => new[] { moving.Triangles[t * 3], moving.Triangles[t * 3 + 1], moving.Triangles[t * 3 + 2] })))
                        vmove.Vertices[v] = (vmove.Vertices.TryGetValue(v, out Vector3 had) ? had : Vector3.Zero) + local;
                    vmove.WholeMesh = false;
                }
                foreach (ZMove m in vertexMoves.Values)
                {
                    Quantise(m);
                    m.Label = MoveLabel(m);
                }
                plan.RoundsTaken = round + 1;
                Verify(plan);
            }
        }

        /// <summary>Two moves of one thing: the longer of two the same way, else both.</summary>
        private static Vector3 Combine(Vector3 had, Vector3 add)
        {
            float lh = had.Length(), la = add.Length();
            if (lh < 1e-9f) return add;
            if (la < 1e-9f) return had;
            if (Vector3.Dot(had, add) / (lh * la) > 0.95f) return lh >= la ? had : add;
            return had + add;
        }

        /// <summary>The vertices given and every other vertex of the mesh at the same spot (uv seams, hard edges), so nothing tears.</summary>
        private static IEnumerable<int> SameSpots(ZSurface surface, IEnumerable<int> vertices)
        {
            HashSet<int> wanted = new HashSet<int>(vertices);
            Dictionary<(long, long, long), List<int>> spots = new Dictionary<(long, long, long), List<int>>();
            for (int v = 0; v < surface.Positions.Length; v++)
            {
                (long, long, long) k = Spot(surface.Positions[v]);
                if (!spots.TryGetValue(k, out List<int> l)) spots[k] = l = new List<int>(2);
                l.Add(v);
            }
            HashSet<int> all = new HashSet<int>();
            foreach (int v in wanted)
            {
                if (v < 0 || v >= surface.Positions.Length) continue;
                foreach (int w in spots[Spot(surface.Positions[v])]) all.Add(w);
            }
            return all;
        }

        private static (long, long, long) Spot(Vector3 p) => ((long)Math.Round(p.X * 10000), (long)Math.Round(p.Y * 10000), (long)Math.Round(p.Z * 10000));

        /// <summary>Where a placement's move goes: the alias already moving it, its own position (a root entity), its position in its
        /// composite (when the other surface is in the same composite placement), or an alias for this placement alone.</summary>
        private static string WhereWritten(ZPlacement placement, ZPlacement other, ZFixOptions options)
        {
            if (placement.Override != null) return "override";
            if (placement.Chain.Count == 1) return "position";
            if (options.InComposite && other != null && other.Chain.Count == placement.Chain.Count)
            {
                bool same = true;
                for (int i = 0; i < placement.Chain.Count - 1 && same; i++) same = placement.Chain[i] == other.Chain[i];
                if (same) return "composite";
            }
            return "alias";
        }

        private static Dictionary<Models.CS2, int> ModelPlacements(ZScan scan)
        {
            Dictionary<Models.CS2, HashSet<int>> uses = new Dictionary<Models.CS2, HashSet<int>>(RefEq<Models.CS2>.Instance);
            foreach (ZSurface surface in scan.Surfaces)
            {
                ZSurfaceSource source = ZFightScene.SourceOf(surface);
                if (source?.Model == null || source.Placement == null) continue;
                if (!uses.TryGetValue(source.Model, out HashSet<int> set)) uses[source.Model] = set = new HashSet<int>();
                set.Add(source.Placement.Index);
            }
            Dictionary<Models.CS2, int> counts = new Dictionary<Models.CS2, int>(RefEq<Models.CS2>.Instance);
            foreach (KeyValuePair<Models.CS2, HashSet<int>> kv in uses) counts[kv.Key] = kv.Value.Count;
            //A search of part of the level sees only part of the placements: count the level's own when it can
            return counts;
        }

        private static string MoveLabel(ZMove move)
        {
            if (move.IsPlacement)
            {
                string where = move.WrittenTo == "override" ? " (on the alias already moving it)" : move.WrittenTo == "composite" ? " (in its composite: every placement)" : move.WrittenTo == "alias" ? " (an alias: this placement only)" : "";
                return "Move " + move.Placement.Name + " " + Mm(move.Delta.Length()) + " " + Way(move.Delta) + where;
            }
            return "Move " + move.Vertices.Count + " vertices of " + move.SubmeshLabel + (move.WholeMesh ? " (the whole mesh)" : "") + " " + Mm(move.Distance) + " " + Way(move.Vertices.Values.FirstOrDefault());
        }

        /// <summary>"up", "down", "along +X", "along (0.71, 0, 0.71)".</summary>
        public static string Way(Vector3 d)
        {
            if (d.LengthSquared() < 1e-14f) return "";
            Vector3 n = Vector3.Normalize(d);
            if (n.Y > 0.95f) return "up";
            if (n.Y < -0.95f) return "down";
            if (n.X > 0.95f) return "along +X";
            if (n.X < -0.95f) return "along -X";
            if (n.Z > 0.95f) return "along +Z";
            if (n.Z < -0.95f) return "along -Z";
            return "along (" + n.X.ToString("0.##") + ", " + n.Y.ToString("0.##") + ", " + n.Z.ToString("0.##") + ")";
        }

        public static string Mm(double metres) => (metres * 1000).ToString(Math.Abs(metres) < 0.0995 ? "0.0" : "0") + " mm";
        #endregion

        #region The model's grid
        /// <summary>Each planned vertex move as the position grid can hold it: whole steps of the submesh's grid, at least as far along the move as planned.</summary>
        private static void Quantise(ZMove move)
        {
            move.Realised.Clear();
            float step = Step(move.Submesh);
            Dictionary<Vector3, Vector3> done = new Dictionary<Vector3, Vector3>();
            foreach (KeyValuePair<int, Vector3> kv in move.Vertices)
            {
                if (!done.TryGetValue(kv.Value, out Vector3 r)) done[kv.Value] = r = OnGrid(kv.Value, step);
                move.Realised[kv.Key] = r;
            }
        }

        /// <summary>The position grid's step of a submesh, metres (positions are 16-bit, scaled by its VertexScale).</summary>
        public static float Step(Submesh submesh) => Math.Max(1, submesh.VertexScale) / (float)short.MaxValue;

        private static Vector3 OnGrid(Vector3 delta, float step)
        {
            float length = delta.Length();
            if (length < 1e-9f) return Vector3.Zero;
            Vector3 dir = delta / length;
            int kx = (int)Math.Round(delta.X / step), ky = (int)Math.Round(delta.Y / step), kz = (int)Math.Round(delta.Z / step);
            for (int i = 0; i < 64; i++)
            {
                Vector3 r = new Vector3(kx, ky, kz) * step;
                if (Vector3.Dot(r, dir) >= length * 0.999f) return r;
                //One more step along the move's largest axis
                if (Math.Abs(dir.X) >= Math.Abs(dir.Y) && Math.Abs(dir.X) >= Math.Abs(dir.Z)) kx += Math.Sign(dir.X);
                else if (Math.Abs(dir.Y) >= Math.Abs(dir.Z)) ky += Math.Sign(dir.Y);
                else kz += Math.Sign(dir.Z);
            }
            return new Vector3(kx, ky, kz) * step;
        }

        /// <summary>Where each vertex's position sits in the submesh's data, and its type.</summary>
        private static List<(long offset, Models.VertexFormat.Type type)> PositionSlots(Submesh submesh)
        {
            List<(long, Models.VertexFormat.Type)> slots = new List<(long, Models.VertexFormat.Type)>();
            using (BinaryReader reader = new BinaryReader(new MemoryStream(submesh.Data)))
            {
                for (int i = 0; i < submesh.VertexFormatFull.Attributes.Count; ++i)
                {
                    if (i == submesh.VertexFormatFull.Attributes.Count - 1) break;
                    for (int x = 0; x < submesh.VertexCount; ++x)
                    {
                        for (int y = 0; y < submesh.VertexFormatFull.Attributes[i].Count; ++y)
                        {
                            Models.VertexFormat.Attribute attribute = submesh.VertexFormatFull.Attributes[i][y];
                            long at = reader.BaseStream.Position;
                            ModelUtility.ReadVertexData(reader, attribute.Type);
                            if (attribute.Usage == Models.VertexFormat.Usage.Position) slots.Add((at, attribute.Type));
                        }
                    }
                    Utilities.Align(reader, 16);
                }
            }
            return slots;
        }

        /// <summary>A submesh's data with the vertex moves made (a copy; the submesh is not changed). Null and a reason when it can't be.</summary>
        public static byte[] MovedData(Submesh submesh, Dictionary<int, Vector3> moves, out Vector3 min, out Vector3 max, out string problem)
        {
            problem = null;
            min = submesh.MinBounds; max = submesh.MaxBounds;
            if (submesh.Data == null || ReferenceEquals(submesh.VertexFormatFull, null) || submesh.VertexFormatFull.Attributes == null) { problem = "it has no vertex data"; return null; }
            List<(long offset, Models.VertexFormat.Type type)> slots = PositionSlots(submesh);
            if (slots.Count != submesh.VertexCount) { problem = "its positions could not be read (" + slots.Count + " of " + submesh.VertexCount + ")"; return null; }
            byte[] data = (byte[])submesh.Data.Clone();
            float scale = Math.Max(1, submesh.VertexScale);
            int overflow = 0;
            using (MemoryStream stream = new MemoryStream(data, true))
            using (BinaryReader reader = new BinaryReader(stream))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                foreach (KeyValuePair<int, Vector3> kv in moves)
                {
                    if (kv.Key < 0 || kv.Key >= slots.Count) continue;
                    (long offset, Models.VertexFormat.Type type) = slots[kv.Key];
                    stream.Position = offset;
                    if (type == Models.VertexFormat.Type.S16_4N)
                    {
                        short x = reader.ReadInt16(), y = reader.ReadInt16(), z = reader.ReadInt16();
                        long nx = x + (long)Math.Round(kv.Value.X / scale * short.MaxValue), ny = y + (long)Math.Round(kv.Value.Y / scale * short.MaxValue), nz = z + (long)Math.Round(kv.Value.Z / scale * short.MaxValue);
                        if (Math.Abs(nx) > short.MaxValue || Math.Abs(ny) > short.MaxValue || Math.Abs(nz) > short.MaxValue) { overflow++; continue; }
                        stream.Position = offset;
                        writer.Write((short)nx); writer.Write((short)ny); writer.Write((short)nz);
                    }
                    else if (type == Models.VertexFormat.Type.FP32_3 || type == Models.VertexFormat.Type.FP32_4)
                    {
                        float x = reader.ReadSingle(), y = reader.ReadSingle(), z = reader.ReadSingle();
                        stream.Position = offset;
                        writer.Write(x + kv.Value.X / scale); writer.Write(y + kv.Value.Y / scale); writer.Write(z + kv.Value.Z / scale);
                    }
                    else { problem = "its positions are stored as " + type; return null; }
                }
            }
            if (overflow != 0) { problem = overflow + " vertices would leave the mesh's position range (resize it in the Model Editor first)"; return null; }
            //Bounds to cover the moved mesh
            Submesh probe = new Submesh() { Data = data, VertexFormatFull = submesh.VertexFormatFull, VertexCount = submesh.VertexCount, IndexCount = submesh.IndexCount, VertexScale = submesh.VertexScale };
            List<Vector3> positions = probe.ToMesh().Vertices;
            if (positions.Count != 0)
            {
                min = new Vector3(float.MaxValue); max = new Vector3(float.MinValue);
                foreach (Vector3 p in positions) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            }
            return data;
        }
        #endregion

        #region Checking the plan
        /// <summary>Search again with the moves made, on copies: what is left of each fight, and any new one the moves make.</summary>
        public static void Verify(ZFixPlan plan)
        {
            ZScan scan = plan.Scan;
            Dictionary<int, ZSurface> moved = new Dictionary<int, ZSurface>();
            Dictionary<Submesh, ZMove> byMesh = plan.Moves.Where(o => !o.IsPlacement).ToDictionary(o => o.Submesh, o => o, RefEq<Submesh>.Instance);
            Dictionary<int, ZMove> byPlacement = plan.Moves.Where(o => o.IsPlacement).ToDictionary(o => o.Placement.Index, o => o);
            foreach (ZSurface surface in scan.Surfaces)
            {
                ZSurfaceSource source = ZFightScene.SourceOf(surface);
                if (source == null) continue;
                Vector3[] positions = null;
                if (source.Submesh != null && byMesh.TryGetValue(source.Submesh, out ZMove vmove))
                {
                    positions = (Vector3[])surface.Positions.Clone();
                    foreach (KeyValuePair<int, Vector3> kv in vmove.Realised.Count != 0 ? vmove.Realised : vmove.Vertices)
                    {
                        if (kv.Key < 0 || kv.Key >= positions.Length) continue;
                        Vector3 d = scan.LevelMode && source.Placement != null ? InstanceTransform.DirectionToWorld(source.Placement.World, kv.Value) : kv.Value;
                        positions[kv.Key] += d;
                    }
                }
                if (source.Placement != null && byPlacement.TryGetValue(source.Placement.Index, out ZMove pmove))
                {
                    positions = positions ?? (Vector3[])surface.Positions.Clone();
                    for (int v = 0; v < positions.Length; v++) positions[v] += pmove.Delta;
                }
                if (positions != null)
                    moved[surface.Index] = Copy(surface, positions);
            }
            if (moved.Count == 0) { plan.Verified = false; return; }

            //The moved surfaces and everything near them
            float reach = scan.Options.Gap + plan.Options.Clearance + 0.05f;
            List<(Vector3 min, Vector3 max)> boxes = moved.Values.Select(o => { o.Prepare(); return (o.Min - new Vector3(reach), o.Max + new Vector3(reach)); }).ToList();
            List<ZSurface> set = new List<ZSurface>();
            Dictionary<ZSurface, ZSurface> original = new Dictionary<ZSurface, ZSurface>(RefEq<ZSurface>.Instance);
            foreach (ZSurface surface in scan.Surfaces)
            {
                ZSurface use = moved.TryGetValue(surface.Index, out ZSurface m) ? m : surface;
                if (use == surface)
                {
                    bool near = false;
                    foreach ((Vector3 min, Vector3 max) in boxes)
                        if (surface.Max.X >= min.X && surface.Min.X <= max.X && surface.Max.Y >= min.Y && surface.Min.Y <= max.Y && surface.Max.Z >= min.Z && surface.Min.Z <= max.Z) { near = true; break; }
                    if (!near) continue;
                    use = Copy(surface, surface.Positions);
                }
                original[use] = surface;
                set.Add(use);
            }
            List<ZFight> after = ZFightFinder.Find(set, scan.Options, out ZFightFinder.Stats _);
            //Index the scan's surfaces back (Find renumbered the copies)
            Dictionary<(int, int), double> left = new Dictionary<(int, int), double>();
            foreach (ZFight f in after)
            {
                ZSurface a = original[f.A], b = original[f.B];
                (int, int) key = (Math.Min(a.Index, b.Index), Math.Max(a.Index, b.Index));
                left[key] = (left.TryGetValue(key, out double had) ? had : 0) + f.Area;
            }
            HashSet<(int, int)> before = new HashSet<(int, int)>(scan.Fights.Select(o => (Math.Min(o.A.Index, o.B.Index), Math.Max(o.A.Index, o.B.Index))));
            foreach (ZFight f in plan.Fights)
            {
                (int, int) key = (Math.Min(f.A.Index, f.B.Index), Math.Max(f.A.Index, f.B.Index));
                plan.AreaAfter[f.Id] = plan.PairLeft[key] = left.TryGetValue(key, out double a) ? a : 0;
            }
            plan.Made.Clear();
            plan.After.Clear();
            foreach (ZFight f in after)
            {
                ZSurface a = original[f.A], b = original[f.B];
                f.A = a; f.B = b;
                plan.After.Add(f);
                (int, int) key = (Math.Min(a.Index, b.Index), Math.Max(a.Index, b.Index));
                if (before.Contains(key)) continue;
                if (!moved.ContainsKey(a.Index) && !moved.ContainsKey(b.Index)) continue;
                plan.Made.Add(f);
            }
            plan.Verified = true;
        }

        private static ZSurface Copy(ZSurface surface, Vector3[] positions) => new ZSurface()
        {
            Group = surface.Group,
            Label = surface.Label,
            GroupLabel = surface.GroupLabel,
            Material = surface.Material,
            Pass = surface.Pass,
            DoubleSided = surface.DoubleSided,
            Positions = positions,
            Triangles = surface.Triangles,
            Tag = surface.Tag,
        };
        #endregion

        #region Making it
        /// <summary>A submesh's vertex data and bounds, before or after a fix.</summary>
        public sealed class SubmeshState
        {
            public Submesh Submesh;
            public Models.CS2 Model;
            public byte[] Data;
            public Vector3 Min, Max;
            public void ApplyTo()
            {
                Submesh.Data = (byte[])Data.Clone();
                Submesh.MinBounds = Min;
                Submesh.MaxBounds = Max;
            }
        }

        /// <summary>Vertex moves as one undo step (when a fix moves no placement).</summary>
        public sealed class ModelEdit : IEdit, IChangesComposites
        {
            private readonly List<SubmeshState> _before, _after;
            private readonly Level _level;
            private readonly Action _changed;
            public string Label { get; }
            public ShortGuid CompositeId { get; }
            public ShortGuid EntityId => ShortGuid.Invalid;
            public IEnumerable<ShortGuid> ChangedComposites => Enumerable.Empty<ShortGuid>();

            public ModelEdit(Level level, Composite composite, List<SubmeshState> before, List<SubmeshState> after, string label, Action changed)
            {
                _level = level;
                _before = before;
                _after = after;
                _changed = changed;
                CompositeId = composite?.shortGUID ?? ShortGuid.Invalid;
                Label = label;
            }

            public void Apply(UndoContext context) => Set(_after);
            public void Revert(UndoContext context) => Set(_before);
            private void Set(List<SubmeshState> states) => SetStates(_level, states, _changed);
            public bool TryMerge(IEdit next) => false;
        }

        private static void SetStates(Level level, List<SubmeshState> states, Action changed)
        {
            foreach (SubmeshState state in states) state.ApplyTo();
            foreach (Models.CS2 model in states.Select(o => o.Model).Where(o => o != null).Distinct())
                EditHistory.AssetChanged(level, AssetKind.MODEL, model.Name);
            DirtyTracker.MarkLevelDataModified();
            Singleton.OnResourceModified?.Invoke();
            changed?.Invoke();
        }

        /// <summary>The result of <see cref="Apply"/>.</summary>
        public sealed class Outcome
        {
            public bool Changed;
            public string Label;
            public List<string> Notes = new List<string>();
            public JArray Aliases = new JArray();
            public int Moved;
        }

        /// <summary>
        /// Make the plan's moves, as one undo step (UI thread). <paramref name="labelPrefix"/> starts the step's name ("AI: " for a tool).
        /// <paramref name="changed"/> runs after the change and after each undo / redo (a window redrawing).
        /// </summary>
        public static Outcome Apply(ZFixPlan plan, string labelPrefix, Action changed = null)
        {
            Outcome outcome = new Outcome();
            List<ZMove> moves = plan.Moves.Where(o => o.Problem == null).ToList();
            if (moves.Count == 0) throw new InvalidOperationException("Nothing to move" + (plan.Moves.Count != 0 ? ": " + string.Join("; ", plan.Moves.Select(o => o.Label + " - " + o.Problem)) : "."));
            string label = "Fix z-fighting (" + plan.Fights.Count + (plan.Fights.Count == 1 ? " overlap" : " overlaps") + ")";
            outcome.Label = (labelPrefix ?? "") + label;

            //The models' new data, made first: a mesh that can't be moved stops the fix before anything changes
            List<SubmeshState> before = new List<SubmeshState>(), after = new List<SubmeshState>();
            foreach (ZMove move in moves.Where(o => !o.IsPlacement))
            {
                byte[] data = MovedData(move.Submesh, move.Realised.Count != 0 ? move.Realised : move.Vertices, out Vector3 min, out Vector3 max, out string problem);
                if (data == null) throw new InvalidOperationException(move.SubmeshLabel + " can't be moved: " + problem + ".");
                before.Add(new SubmeshState() { Submesh = move.Submesh, Model = move.Model, Data = (byte[])move.Submesh.Data.Clone(), Min = move.Submesh.MinBounds, Max = move.Submesh.MaxBounds });
                after.Add(new SubmeshState() { Submesh = move.Submesh, Model = move.Model, Data = data, Min = Vector3.Min(min, move.Submesh.MinBounds), Max = Vector3.Max(max, move.Submesh.MaxBounds) });
            }

            Level level = plan.Scan.Level ?? Singleton.Editor?.CompositeBrowser?.Content?.Level;
            List<ZMove> placements = moves.Where(o => o.IsPlacement).ToList();
            if (placements.Count == 0)
            {
                Composite focus = Singleton.Editor?.CompositeDisplay?.Composite ?? level?.Commands?.EntryPoints?.FirstOrDefault();
                SetStates(level, after, changed);
                UndoStack.Current.Record(new ModelEdit(level, focus, before, after, outcome.Label, changed));
                outcome.Changed = true;
                outcome.Moved = moves.Count;
                return outcome;
            }

            Commands commands = level.Commands;
            Composite root = commands.EntryPoints[0];
            McpScriptEdit.Outcome done = McpScriptEdit.Run(label, root, edit =>
            {
                McpPlacements walker = McpRegion.Walker(edit.Commands);
                foreach (ZMove move in placements)
                {
                    ZPlacement p = move.Placement;
                    if (!edit.Commands.Entries.Contains(p.Composite) || p.Composite.GetEntityByID(p.Entity.shortGUID) != p.Entity)
                        throw new McpError(p.Path + " is no longer in the level: search again.");
                    cTransform parent = p.Chain.Count == 1 ? null : walker.Evaluate(root, p.Chain.Take(p.Chain.Count - 1).ToList()).World;
                    cTransform world = new cTransform(p.World.position + move.Delta, p.World.rotation);
                    cTransform local = InstanceTransform.ToLocal(parent, world);
                    JObject value = new JObject() { ["position"] = McpValues.Vector(local.position), ["rotation"] = McpValues.Vector(local.rotation) };
                    switch (move.WrittenTo)
                    {
                        case "override":
                            edit.SetParameter(p.OverrideOwner, p.Override, "position", value, allowCustom: true);
                            break;
                        case "position":
                        case "composite":
                            edit.SetParameter(p.Composite, p.Entity, "position", value, allowCustom: true);
                            break;
                        default:
                            AliasEntity alias = edit.FindOrAddAlias(root, p.Chain.Select(o => o.shortGUID).Concat(new[] { ShortGuid.Invalid }).ToArray(), (p.Name ?? "Model") + "_ZFix", out bool created);
                            edit.SetParameter(root, alias, "position", value, allowCustom: true);
                            outcome.Aliases.Add(new JObject() { ["for"] = p.Path, ["alias"] = McpScript.Id(alias.shortGUID), ["composite"] = root.name, ["reused"] = !created });
                            break;
                    }
                }
                if (after.Count != 0)
                {
                    SetStates(level, after, changed);
                    edit.OnEachApply(reverted => SetStates(level, reverted ? before : after, changed));
                }
            }, new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = labelPrefix ?? "" });
            outcome.Changed = done.Changed;
            outcome.Notes.AddRange(done.Notes);
            outcome.Moved = moves.Count;
            changed?.Invoke();
            return outcome;
        }
        #endregion
    }
}
