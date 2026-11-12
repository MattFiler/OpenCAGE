using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Characters
{
    /// <summary>
    /// Which part of a body a first-person view would see from the inside. Modelled on the game's own first-person body, RIPLEY_FP
    /// (FEMALEFP bind space: Y up, facing -Z): no vertex led by HEAD, HEAD1, NECK or the clavicles (LEFT/RIGHTSHOULDER); the torso kept
    /// as a ring round the hips, a little higher at the front, and only a front panel up to the chest; arms from the upper arm down,
    /// legs and feet whole. Heights are kept as fractions of the rig's hips-to-neck height, so the same rule fits any character rig.
    /// </summary>
    public class FirstPersonRule
    {
        /// <summary>A vertex goes when its strongest bone is one of these.</summary>
        public List<string> DropDominant = new List<string>() { "HEAD", "HEAD1", "NECK", "NECK1", "LEFTSHOULDER", "RIGHTSHOULDER" };
        /// <summary>...or when it carries more than <see cref="AnyWeightMin"/> of one of these.</summary>
        public List<string> DropAnyWeight = new List<string>() { "HEAD", "HEAD1", "NECK", "NECK1", "LEFTSHOULDER", "RIGHTSHOULDER" };
        public float AnyWeightMin = 0.25f;
        /// <summary>A vertex led by one of these stays only below the torso profile.</summary>
        public List<string> Torso = new List<string>() { "HIPS", "PELVIS", "SPINE", "SPINE1", "SPINE2", "SPINE3" };

        /* The torso profile: kept where (y <= front and z <= FrontZ) or (y <= mid and z <= MidZ) or y <= back. Heights as fractions of
           hips (0) to neck (1); FEMALEFP's hips 0.934 m and neck 1.470 m give RIPLEY_FP's 1.25 / 1.05 / 0.90 m. z in metres, -z the front. */
        public float FrontHeight = 0.5896f, MidHeight = 0.2164f, BackHeight = -0.0634f;
        public float FrontZ = -0.025f, MidZ = 0.05f;

        /// <summary>Keep the torso whole (only the head, neck and clavicles go).</summary>
        public bool KeepTorso;

        /// <summary>
        /// A triangle goes only when two or more of its corners are in the cut (default: any one, as the game's own bodies are cut). Kinder
        /// to a low-polygon body whose big triangles reach from the chest to the collar; the edge of the cut can then show a sliver.
        /// </summary>
        public bool Lenient;

        public FirstPersonRule Copy()
        {
            FirstPersonRule copy = (FirstPersonRule)MemberwiseClone();
            copy.DropDominant = new List<string>(DropDominant);
            copy.DropAnyWeight = new List<string>(DropAnyWeight);
            copy.Torso = new List<string>(Torso);
            return copy;
        }

        public override string ToString()
        {
            return "drop led by [" + string.Join(", ", DropDominant) + "], drop weight > " + AnyWeightMin + " on [" + string.Join(", ", DropAnyWeight) + "], " +
                (KeepTorso ? "torso kept whole" : "torso [" + string.Join(", ", Torso) + "] kept to " + FrontHeight.ToString("0.###") + " (front of z " + FrontZ + ") / " + MidHeight.ToString("0.###") + " (z " + MidZ + ") / " + BackHeight.ToString("0.###") + " of hips-to-neck");
        }
    }

    /// <summary>The cut a rule makes on a model: per submesh what goes, and the heights the rule came to on the rig.</summary>
    public class FirstPersonCutResult
    {
        public class Piece
        {
            public int Component, LOD, Submesh;
            public int VerticesBefore, VerticesAfter, TrianglesBefore, TrianglesAfter;
            /// <summary>Every triangle of it goes: the submesh is left out of the body.</summary>
            public bool Dropped;
            /// <summary>Not skinned (or its data couldn't be read): left as it is.</summary>
            public string Skipped;
            public bool[] RemovedVertices;
            public Dictionary<string, int> Why = new Dictionary<string, int>();
            public Models.CS2.Component.LOD.Submesh Source;
            public override string ToString() => "C" + Component + "/L" + LOD + "/S" + Submesh + ": " + (Skipped != null ? "left as it is (" + Skipped + ")" :
                Dropped ? "left out (all " + TrianglesBefore + " triangles in the cut)" : TrianglesAfter == TrianglesBefore ? "untouched" :
                "triangles " + TrianglesBefore + " -> " + TrianglesAfter + ", vertices " + VerticesBefore + " -> " + VerticesAfter) +
                (Why.Count != 0 ? " [" + string.Join(", ", Why.OrderByDescending(o => o.Value).Select(o => o.Key + " " + o.Value)) + "]" : "");
        }

        public List<Piece> Pieces = new List<Piece>();
        public float HipsY, NeckY, FrontY, MidY, BackY;
        /// <summary>The rule's triangle test (see <see cref="FirstPersonRule.Lenient"/>).</summary>
        public bool Lenient;
        public List<string> Notes = new List<string>();
        public int TrianglesBefore => Pieces.Sum(o => o.TrianglesBefore);
        public int TrianglesAfter => Pieces.Sum(o => o.Dropped ? 0 : o.TrianglesAfter);
        public bool ChangesAnything => Pieces.Any(o => o.Dropped || (o.Skipped == null && o.TrianglesAfter != o.TrianglesBefore));
    }

    /// <summary>
    /// Take off a body skinned to a character rig what a first-person camera at its head would see from the inside (the head, neck,
    /// clavicles and the upper back), as the game's own first-person bodies are cut. A triangle goes when any of its corners goes; the
    /// kept vertices are copied byte for byte (nothing is requantised) and the indices renumbered. Running it again on its own result finds
    /// nothing more to take.
    /// </summary>
    public static class FirstPersonCut
    {
        public static int BoneIndex(Skeleton rig, string bone)
        {
            if (rig == null || string.IsNullOrEmpty(bone)) return -1;
            for (int i = 0; i < rig.Bones.Count; i++)
            {
                string name = rig.Bones[i].Name ?? "";
                int colon = name.LastIndexOf(':');
                if (string.Equals(colon >= 0 ? name.Substring(colon + 1) : name, bone, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        public static string ShortName(Skeleton rig, int bone)
        {
            if (rig == null || bone < 0 || bone >= rig.Bones.Count) return "?";
            string name = rig.Bones[bone].Name ?? "";
            int colon = name.LastIndexOf(':');
            return colon >= 0 ? name.Substring(colon + 1) : name;
        }

        private static HashSet<int> Set(Skeleton rig, IEnumerable<string> names)
        {
            HashSet<int> set = new HashSet<int>();
            foreach (string name in names ?? Enumerable.Empty<string>()) { int i = BoneIndex(rig, name); if (i >= 0) set.Add(i); }
            return set;
        }

        /// <summary>Where the rule's torso heights land on this rig (bind pose).</summary>
        public static bool Heights(Skeleton rig, FirstPersonRule rule, FirstPersonCutResult into, out string problem)
        {
            problem = null;
            int hips = BoneIndex(rig, "HIPS"), neck = BoneIndex(rig, "NECK");
            if (hips < 0) hips = BoneIndex(rig, "PELVIS");
            if (hips < 0 || neck < 0) { problem = "The rig " + rig?.Name + " has no HIPS and NECK to measure the torso by."; return false; }
            List<Matrix4x4> bind = rig.GetBindPose();
            into.HipsY = bind[hips].Translation.Y;
            into.NeckY = bind[neck].Translation.Y;
            float span = into.NeckY - into.HipsY;
            if (span <= 0.05f) { problem = "The rig's neck is not above its hips (" + into.HipsY.ToString("0.000") + " / " + into.NeckY.ToString("0.000") + " m)."; return false; }
            into.FrontY = into.HipsY + rule.FrontHeight * span;
            into.MidY = into.HipsY + rule.MidHeight * span;
            into.BackY = into.HipsY + rule.BackHeight * span;
            return true;
        }

        /// <summary>Which vertices of one submesh the rule takes (and why, per reason).</summary>
        public static bool[] RemovedVertices(cMesh mesh, List<int> bones, Skeleton rig, FirstPersonRule rule, FirstPersonCutResult heights, Dictionary<string, int> why)
        {
            HashSet<int> dropDominant = Set(rig, rule.DropDominant), dropAny = Set(rig, rule.DropAnyWeight), torso = Set(rig, rule.Torso);
            bool[] removed = new bool[mesh.Vertices.Count];
            if (bones == null || bones.Count == 0 || mesh.BoneIndexes.Count != mesh.Vertices.Count || mesh.BoneWeights.Count != mesh.Vertices.Count)
                return removed;
            for (int v = 0; v < mesh.Vertices.Count; v++)
            {
                float[] index = { mesh.BoneIndexes[v].X, mesh.BoneIndexes[v].Y, mesh.BoneIndexes[v].Z, mesh.BoneIndexes[v].W };
                float[] weight = { mesh.BoneWeights[v].X, mesh.BoneWeights[v].Y, mesh.BoneWeights[v].Z, mesh.BoneWeights[v].W };
                int strongest = 0;
                for (int k = 1; k < 4; k++) if (weight[k] > weight[strongest]) strongest = k;
                int Bone(int k) { int local = (int)Math.Round(index[k]); return local >= 0 && local < bones.Count ? bones[local] : -1; }
                int dominant = Bone(strongest);
                string reason = null;
                if (dropDominant.Contains(dominant)) reason = "led by " + ShortName(rig, dominant);
                if (reason == null)
                    for (int k = 0; k < 4; k++)
                        if (weight[k] > rule.AnyWeightMin && dropAny.Contains(Bone(k))) { reason = "weighted to " + ShortName(rig, Bone(k)); break; }
                if (reason == null && !rule.KeepTorso && torso.Contains(dominant))
                {
                    Vector3 p = mesh.Vertices[v];
                    bool kept = (p.Y <= heights.FrontY && p.Z <= rule.FrontZ) || (p.Y <= heights.MidY && p.Z <= rule.MidZ) || p.Y <= heights.BackY;
                    if (!kept) reason = "upper torso (" + ShortName(rig, dominant) + ")";
                }
                if (reason != null)
                {
                    removed[v] = true;
                    if (why != null) why[reason] = why.TryGetValue(reason, out int n) ? n + 1 : 1;
                }
            }
            return removed;
        }

        public static int SizeOf(Models.VertexFormat.Type type)
        {
            switch (type)
            {
                case Models.VertexFormat.Type.FP32_1:
                case Models.VertexFormat.Type.Color:
                case Models.VertexFormat.Type.U8_4:
                case Models.VertexFormat.Type.S16_2:
                case Models.VertexFormat.Type.U8_4N:
                case Models.VertexFormat.Type.S16_2N:
                case Models.VertexFormat.Type.U16_2N:
                case Models.VertexFormat.Type.UDec3:
                case Models.VertexFormat.Type.Dec3N:
                case Models.VertexFormat.Type.FP16_2:
                case Models.VertexFormat.Type.S8_4N:
                    return 4;
                case Models.VertexFormat.Type.FP32_2:
                case Models.VertexFormat.Type.S16_4:
                case Models.VertexFormat.Type.S16_4N:
                case Models.VertexFormat.Type.U16_4N:
                case Models.VertexFormat.Type.FP16_4:
                    return 8;
                case Models.VertexFormat.Type.FP32_3:
                    return 12;
                case Models.VertexFormat.Type.FP32_4:
                    return 16;
            }
            return -1;
        }

        private static int Align16(int p) => (p + 15) / 16 * 16;

        /// <summary>Whether a triangle goes: any corner in the cut, or (lenient) two or more.</summary>
        public static bool Goes(bool[] removed, int a, int b, int c, bool lenient)
        {
            if (removed == null) return false;
            int n = (removed[a] ? 1 : 0) + (removed[b] ? 1 : 0) + (removed[c] ? 1 : 0);
            return lenient ? n >= 2 : n >= 1;
        }

        /// <summary>
        /// Work out the cut on a whole model (every component, LOD and submesh skinned to <paramref name="rig"/>), changing nothing.
        /// </summary>
        public static FirstPersonCutResult Plan(Models.CS2 model, Skeleton rig, FirstPersonRule rule, out string problem)
        {
            FirstPersonCutResult result = new FirstPersonCutResult() { Lenient = rule.Lenient };
            if (!Heights(rig, rule, result, out problem)) return null;
            for (int c = 0; c < model.Components.Count; c++)
                for (int l = 0; l < model.Components[c].LODs.Count; l++)
                    for (int s = 0; s < model.Components[c].LODs[l].Submeshes.Count; s++)
                    {
                        Models.CS2.Component.LOD.Submesh submesh = model.Components[c].LODs[l].Submeshes[s];
                        FirstPersonCutResult.Piece piece = new FirstPersonCutResult.Piece() { Component = c, LOD = l, Submesh = s, Source = submesh, VerticesBefore = submesh.VertexCount, TrianglesBefore = submesh.IndexCount / 3 };
                        piece.VerticesAfter = piece.VerticesBefore; piece.TrianglesAfter = piece.TrianglesBefore;
                        result.Pieces.Add(piece);
                        if (submesh.Bones == null || submesh.Bones.Count == 0 || submesh.Data == null || submesh.Data.Length == 0) { piece.Skipped = "not skinned"; continue; }
                        if (submesh.WeightedCollision != null) { piece.Skipped = "hit collision: bullets need all of it"; continue; }
                        cMesh mesh;
                        try { mesh = submesh.ToMesh(); }
                        catch (Exception e) { piece.Skipped = "unreadable: " + e.Message; continue; }
                        if (submesh.Bones.Any(b => b >= rig.Bones.Count)) { piece.Skipped = "skinned to bones " + rig.Name + " doesn't have"; continue; }
                        piece.RemovedVertices = RemovedVertices(mesh, submesh.Bones, rig, rule, result, piece.Why);
                        int kept = 0;
                        bool[] used = new bool[mesh.Vertices.Count];
                        for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
                        {
                            ushort a = mesh.Indices[i], b = mesh.Indices[i + 1], cc = mesh.Indices[i + 2];
                            if (Goes(piece.RemovedVertices, a, b, cc, rule.Lenient)) continue;
                            kept++;
                            used[a] = used[b] = used[cc] = true;
                        }
                        piece.TrianglesAfter = kept;
                        piece.VerticesAfter = used.Count(o => o);
                        piece.Dropped = kept == 0;
                    }
            if (!result.ChangesAnything) result.Notes.Add("The rule takes nothing from this model (no head, neck, clavicle or upper-torso vertices on " + rig.Name + "'s bones, or it is cut already).");
            return result;
        }

        /// <summary>
        /// A copy of <paramref name="model"/> named <paramref name="name"/> with the cut made: submeshes the cut empties are left out
        /// (a component or LOD left with nothing goes too). The source model is not changed. Returns the cut submeshes of the copy in
        /// <paramref name="changed"/>, for their skinned bounds to be set again.
        /// </summary>
        public static Models.CS2 MakeCopy(Models.CS2 model, string name, FirstPersonCutResult plan, out List<Models.CS2.Component.LOD.Submesh> changed)
        {
            changed = new List<Models.CS2.Component.LOD.Submesh>();
            Models.CS2 copy = new Models.CS2() { Name = name };
            Dictionary<Models.CS2.Component.LOD.Submesh, FirstPersonCutResult.Piece> pieces = plan.Pieces.ToDictionary(o => o.Source, o => o);
            foreach (Models.CS2.Component component in model.Components)
            {
                Models.CS2.Component newComponent = new Models.CS2.Component();
                foreach (Models.CS2.Component.LOD lod in component.LODs)
                {
                    Models.CS2.Component.LOD newLod = new Models.CS2.Component.LOD(lod.Name);
                    foreach (Models.CS2.Component.LOD.Submesh submesh in lod.Submeshes)
                    {
                        pieces.TryGetValue(submesh, out FirstPersonCutResult.Piece piece);
                        if (piece != null && piece.Dropped) continue;
                        Models.CS2.Component.LOD.Submesh made = CopySubmesh(submesh);
                        if (piece != null && piece.Skipped == null && piece.TrianglesAfter != piece.TrianglesBefore)
                        {
                            Cut(made, piece.RemovedVertices, plan.Lenient);
                            changed.Add(made);
                        }
                        newLod.Submeshes.Add(made);
                    }
                    if (newLod.Submeshes.Count != 0) newComponent.LODs.Add(newLod);
                }
                if (newComponent.LODs.Count != 0) copy.Components.Add(newComponent);
            }
            return copy;
        }

        /// <summary>A submesh of its own with the same data (a new identity: the level tells submeshes apart by it).</summary>
        public static Models.CS2.Component.LOD.Submesh CopySubmesh(Models.CS2.Component.LOD.Submesh s)
        {
            return new Models.CS2.Component.LOD.Submesh()
            {
                MinBounds = s.MinBounds,
                MaxBounds = s.MaxBounds,
                MinLODRange = s.MinLODRange,
                MaxLODRange = s.MaxLODRange,
                RenderFlags = s.RenderFlags,
                Material = s.Material,
                CollisionProxyIndex = s.CollisionProxyIndex,
                WeightedCollision = s.WeightedCollision,
                MorphAnimSet = s.MorphAnimSet,
                VertexFormatFull = s.VertexFormatFull,
                VertexFormatPartial = s.VertexFormatPartial,
                VertexScale = s.VertexScale,
                VertexCount = s.VertexCount,
                IndexCount = s.IndexCount,
                Bones = new List<int>(s.Bones ?? new List<int>()),
                Data = (byte[])(s.Data ?? new byte[0]).Clone(),
            };
        }

        /// <summary>
        /// Take the triangles touching a removed vertex out of the submesh, and the vertices no kept triangle uses. The vertex streams are
        /// copied a vertex at a time as raw bytes; the result is decoded again and compared before it is kept. Throws, changing nothing,
        /// when the data is not laid out as its format says.
        /// </summary>
        public static void Cut(Models.CS2.Component.LOD.Submesh sm, bool[] removed, bool lenient = false)
        {
            cMesh m = sm.ToMesh();
            List<ushort> keptIdx = new List<ushort>(m.Indices.Count);
            for (int i = 0; i + 2 < m.Indices.Count; i += 3)
            {
                ushort a = m.Indices[i], b = m.Indices[i + 1], c = m.Indices[i + 2];
                if (Goes(removed, a, b, c, lenient)) continue;
                keptIdx.Add(a); keptIdx.Add(b); keptIdx.Add(c);
            }
            bool[] used = new bool[m.Vertices.Count];
            foreach (ushort i in keptIdx) used[i] = true;
            int[] remap = new int[m.Vertices.Count];
            int kept = 0;
            for (int v = 0; v < used.Length; v++) remap[v] = used[v] ? kept++ : -1;
            if (keptIdx.Count == 0) throw new InvalidOperationException("the cut would leave this submesh empty");

            Models.VertexFormat format = sm.VertexFormatFull;
            int streams = format.Attributes.Count - 1;
            int[] stride = new int[streams], start = new int[streams];
            int pos = 0;
            for (int s = 0; s < streams; s++)
            {
                foreach (Models.VertexFormat.Attribute at in format.Attributes[s])
                {
                    int size = SizeOf(at.Type);
                    if (size < 0) throw new InvalidOperationException("unknown vertex attribute type " + at.Type);
                    stride[s] += size;
                }
                start[s] = pos;
                pos = Align16(pos + sm.VertexCount * stride[s]);
            }
            int indexStart = pos;
            int expected = Align16(indexStart + sm.IndexCount * 2);
            if (expected != sm.Data.Length) throw new InvalidOperationException("the submesh's data is " + sm.Data.Length + " bytes where its format says " + expected);
            for (int i = 0; i < sm.IndexCount; i++)
                if (BitConverter.ToUInt16(sm.Data, indexStart + i * 2) != m.Indices[i]) throw new InvalidOperationException("index " + i + " doesn't read back");

            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                for (int s = 0; s < streams; s++)
                {
                    for (int v = 0; v < used.Length; v++)
                        if (used[v]) ms.Write(sm.Data, start[s] + v * stride[s], stride[s]);
                    while (ms.Position % 16 != 0) ms.WriteByte(0);
                }
                foreach (ushort i in keptIdx) { byte[] b = BitConverter.GetBytes((ushort)remap[i]); ms.Write(b, 0, 2); }
                while (ms.Position % 16 != 0) ms.WriteByte(0);
                data = ms.ToArray();
            }
            Models.CS2.Component.LOD.Submesh test = new Models.CS2.Component.LOD.Submesh()
            {
                VertexFormatFull = sm.VertexFormatFull, VertexFormatPartial = sm.VertexFormatPartial, VertexScale = sm.VertexScale,
                VertexCount = kept, IndexCount = keptIdx.Count, Data = data, Bones = new List<int>(sm.Bones)
            };
            cMesh back = test.ToMesh();
            for (int v = 0; v < used.Length; v++)
            {
                if (!used[v]) continue;
                int n = remap[v];
                if (back.Vertices[n] != m.Vertices[v] || back.BoneIndexes[n] != m.BoneIndexes[v] || back.BoneWeights[n] != m.BoneWeights[v] ||
                    (m.Normals.Count > 0 && back.Normals[n] != m.Normals[v]))
                    throw new InvalidOperationException("vertex " + v + " didn't survive the copy");
            }
            for (int i = 0; i < keptIdx.Count; i++)
                if (back.Indices[i] != remap[keptIdx[i]]) throw new InvalidOperationException("index " + i + " didn't survive the copy");

            sm.Data = data;
            sm.VertexCount = kept;
            sm.IndexCount = keptIdx.Count;
            //the plain box: the editor sets the skinned bounds (r, r, r) / max afterwards
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (Vector3 p in back.Vertices) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            sm.MaxBounds = max;
        }
    }
}
