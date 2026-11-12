using Assimp;
using CATHODE;
using System;
using System.Collections.Generic;
using System.Linq;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.Rigging
{
    /// <summary>
    /// Moves a model skinned to a foreign skeleton onto one of the game's rigs (vertices into its bind pose, weights onto its
    /// bones), since a new character skeleton would also need bone groups, a mirror table, a ragdoll and HUMANOID's contexts.
    /// Each source bone becomes a segment laid onto its rig joints (placed, turned, stretched), so meeting bones agree on their
    /// joint; chains of different lengths are laid by arc length. Joints that play no part ride the nearest one above that does.
    /// </summary>
    public class SkinRebind
    {
        public class Options
        {
            /// <summary>Scale the model so its hips-to-neck matches the rig's (otherwise the file's own units are kept).</summary>
            public bool FitSize = true;

            /// <summary>An extra size factor on top.</summary>
            public float Scale = 1;

            /// <summary>Move the head without turning or stretching it, so it keeps its shape.</summary>
            public bool KeepHeadShape = true;

            /// <summary>Keep the head's own height above the shoulders rather than pinning it to the rig's HEAD joint (a long or short neck otherwise).</summary>
            public bool HeadAtOwnHeight = true;

            /// <summary>Stretch a tail to the rig's whole tail; otherwise it keeps its own length, laid along the rig's.</summary>
            public bool StretchTail = false;

            /// <summary>Role (HIPS, LEFTFOREARM, ...) to source joint name, over the automatic mapping; empty leaves a role out.</summary>
            public Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public class MeshOut
        {
            public int MeshIndex;
            public string Name;
            public Vector3[] Positions, Normals, Tangents, Bitangents;
            public int[][] Bones;
            public float[][] Weights;
            public bool FlipWinding;
        }

        public class Segment
        {
            public string Name;
            public Matrix4x4 Transform = Matrix4x4.Identity;     //fitted source space -> the rig's bind space (row vectors)
            public Matrix4x4 Normals = Matrix4x4.Identity;
            public Func<Vector3, List<(int bone, float weight)>> Weights;
            public string Rides;                                  //for a joint that plays no part: what it rides
        }

        public class Result
        {
            public SourceRig Source;
            public Skeleton Target;
            public RigRoles.Assignment Roles;
            public Matrix4x4 Frame = Matrix4x4.Identity;
            public bool Mirrored;
            public float Scale = 1;
            public Segment[] Segments;
            public Vector3[] Fitted;      //each source joint once turned and sized onto the rig, before the segments
            public Vector3[] Moved;       //each source joint where its own segment puts it
            public List<Matrix4x4> TargetBind;
            public List<MeshOut> Meshes = new List<MeshOut>();
            public List<string> Report = new List<string>();
            public List<string> Warnings = new List<string>();
            public float Fit = -1;
            public string Problem;
            public bool Ok => Problem == null;
        }

        private Result _r;
        private Options _o;
        private SourceRig _src;
        private Skeleton _rig;
        private Vector3[] _fj;
        private List<Matrix4x4> _tb;

        public static Result Build(SourceRig source, Skeleton target, Options options = null)
        {
            SkinRebind job = new SkinRebind() { _o = options ?? new Options(), _src = source, _rig = target };
            job._r = new Result() { Source = source, Target = target };
            try { job.Run(); }
            catch (Exception e) { job._r.Problem = "The rebind failed: " + e.Message; }
            return job._r;
        }

        int T(string role) => RigRoles.TargetBone(_rig, role);
        Vector3 TP(int bone) => _tb[bone].Translation;
        Vector3 TP(string role) => TP(T(role));
        int S(string role) => _r.Roles[role];
        bool Both(string role) => _r.Roles.Has(role) && T(role) >= 0;
        static string V(Vector3 v) => $"({v.X:0.000}, {v.Y:0.000}, {v.Z:0.000})";
        static List<(int, float)> One(int bone) => new List<(int, float)>() { (bone, 1f) };

        void Run()
        {
            if (_src == null || _src.Joints.Count == 0) { _r.Problem = "The file has no skeleton to rebind from."; return; }
            if (!RigRoles.CanTarget(_rig)) { _r.Problem = (_rig?.Name ?? "That rig") + " is not a character rig a model can be rebound onto (it needs HIPS, NECK, both arms and both feet)."; return; }
            _tb = _rig.GetBindPose();
            _r.TargetBind = _tb;
            _r.Roles = RigRoles.Assign(_src, _o.Map);
            _r.Warnings.AddRange(_src.Notes);
            _r.Warnings.AddRange(_r.Roles.Notes);
            if (!_r.Roles.Has("HIPS")) { _r.Problem = "No joint of the file could be taken for the hips. Map HIPS by hand."; return; }
            if (!_r.Roles.Has("NECK") && !_r.Roles.Has("HEAD")) { _r.Problem = "No joint of the file could be taken for the neck or the head. Map NECK (and HEAD) by hand."; return; }
            string lArm = _r.Roles.Has("LEFTARM") ? "LEFTARM" : "LEFTSHOULDER", rArm = _r.Roles.Has("RIGHTARM") ? "RIGHTARM" : "RIGHTSHOULDER";
            if (!_r.Roles.Has(lArm) || !_r.Roles.Has(rArm)) { _r.Problem = "The file's left and right arms could not both be found, so there is no telling which side is which. Map LEFTARM and RIGHTARM by hand."; return; }

            FitFrame(lArm, rArm);
            BuildSegments();
            ApplyToMeshes();
            Measure();
        }

        #region THE FRAME
        /* Turns and sizes the file onto the rig, scoring up (hips to neck), left (arm to arm) and forward (ankle to toe) over all
         * 48 signed axis permutations, reflections included. Never derive the third axis with a cross product: that assumes the
         * handedness the fit exists to find. */
        void FitFrame(string lArm, string rArm)
        {
            Vector3 J(string role) => _src.Joints[S(role)].Rest;
            string top = _r.Roles.Has("NECK") ? "NECK" : "HEAD";
            Vector3 srcUp = Vector3.Normalize(J(top) - J("HIPS"));
            Vector3 srcLeft = Vector3.Normalize(J(lArm) - J(rArm));
            Vector3 tgtUp = Vector3.Normalize(TP(top) - TP("HIPS"));
            Vector3 tgtLeft = Vector3.Normalize(TP(T(lArm) >= 0 ? lArm : "LEFTARM") - TP(T(rArm) >= 0 ? rArm : "RIGHTARM"));

            Vector3 Across(Vector3 v, Vector3 up) { v -= Vector3.Dot(v, up) * up; return v.LengthSquared() < 1e-10f ? Vector3.Zero : Vector3.Normalize(v); }
            Vector3 srcFwd = Vector3.Zero, tgtFwd = Vector3.Zero;
            foreach (string side in new[] { "LEFT", "RIGHT" })
            {
                string toe = _r.Roles.Has(side + "TOEBASE") ? side + "TOEBASE" : null, ankle = _r.Roles.Has(side + "FOOT") ? side + "FOOT" : _r.Roles.Has(side + "LEG2") ? side + "LEG2" : null;
                if (toe != null && ankle != null) srcFwd += Across(J(toe) - J(ankle), srcUp);
                tgtFwd += Across(TP(side + "TOEBASE") - TP(side + "FOOT"), tgtUp);
            }
            bool haveForward = srcFwd.LengthSquared() > 1e-6f;
            if (haveForward) srcFwd = Vector3.Normalize(srcFwd);
            tgtFwd = Vector3.Normalize(tgtFwd);
            if (!haveForward) _r.Warnings.Add("The file's feet and toes weren't found, so which way it faces was judged from up and left alone; check the picture faces the rig's way.");

            Matrix4x4 best = Matrix4x4.Identity;
            double bestError = double.MaxValue;
            foreach (Matrix4x4 candidate in RigMath.SignedPermutations())
            {
                double e = (Vector3.TransformNormal(srcUp, candidate) - tgtUp).LengthSquared() + (Vector3.TransformNormal(srcLeft, candidate) - tgtLeft).LengthSquared();
                if (haveForward) e += (Vector3.TransformNormal(srcFwd, candidate) - tgtFwd).LengthSquared();
                else if (RigMath.Determinant3(candidate) < 0) e += 0.5;     //with only two directions, never pick a mirror over a rotation
                if (e < bestError) { bestError = e; best = candidate; }
            }
            _r.Frame = best;
            _r.Mirrored = RigMath.Determinant3(best) < 0;

            float srcSpan = (J(top) - J("HIPS")).Length(), tgtSpan = (TP(top) - TP("HIPS")).Length();
            _r.Scale = (_o.FitSize && srcSpan > 1e-5f ? tgtSpan / srcSpan : 1f) * (_o.Scale > 0 ? _o.Scale : 1f);

            Vector3 hips = J("HIPS"), tgtHips = TP("HIPS");
            _fj = _src.Joints.Select(j => Fit(j.Rest)).ToArray();
            _r.Fitted = _fj;

            string turn = best.IsIdentity ? "already facing the rig's way" : "turned " + Describe(best) + (_r.Mirrored ? " (a MIRROR: the file's left and right are the other way round to its geometry, so its triangles are rewound)" : "");
            _r.Report.Add("Fit: " + turn + "; sized x" + _r.Scale.ToString("0.000") + (_o.FitSize ? " (hips to " + top.ToLowerInvariant() + " " + srcSpan.ToString("0.000") + " -> " + tgtSpan.ToString("0.000") + ")" : " (the file's own units)") + "; the hips put on " + _rig.Name + "'s.");
            if (bestError > 0.6) _r.Warnings.Add("The file's up / left / forward don't line up well with any turn of the rig's (error " + bestError.ToString("0.00") + "): check the mapping of HIPS, NECK, the arms and the feet.");
        }

        Vector3 Fit(Vector3 p) => Vector3.Transform((p - _src.Joints[S("HIPS")].Rest) * _r.Scale, _r.Frame) + TP("HIPS");

        static string Describe(Matrix4x4 m)
        {
            string Axis(float x, float y, float z) => (x + y + z < 0 ? "-" : "+") + (Math.Abs(x) > 0.5f ? "X" : Math.Abs(y) > 0.5f ? "Y" : "Z");
            return "X->" + Axis(m.M11, m.M12, m.M13) + " Y->" + Axis(m.M21, m.M22, m.M23) + " Z->" + Axis(m.M31, m.M32, m.M33);
        }
        #endregion

        #region SEGMENTS
        Segment[] _seg;
        Dictionary<int, int> _weightTo = new Dictionary<int, int>();   //source joint -> rig bone, for joints that ride but weight to a bone of their own
        HashSet<int> _usedBones = new HashSet<int>();

        Segment Seg(string name, Vector3 srcA, Vector3 srcB, Vector3 tgtA, Vector3 tgtB, Vector3? srcAcross = null, Vector3? tgtAcross = null)
        {
            Segment s = new Segment() { Name = name };
            Vector3 ds = srcB - srcA, dt = tgtB - tgtA;
            float ls = ds.Length(), lt = dt.Length();
            if (ls < 1e-5f || lt < 1e-5f)
            {
                s.Transform = Matrix4x4.CreateTranslation(tgtA - srcA);
                return s;
            }
            ds /= ls; dt /= lt;
            Matrix4x4 rotation = srcAcross.HasValue && tgtAcross.HasValue
                ? RigMath.Frame(ds, srcAcross.Value, dt, tgtAcross.Value)
                : Matrix4x4.CreateFromQuaternion(RigMath.Arc(ds, dt));
            Matrix4x4 stretch = Matrix4x4.Identity + RigMath.Outer(ds) * (lt / ls - 1);
            stretch.M44 = 1;
            s.Transform = Matrix4x4.CreateTranslation(-srcA) * stretch * rotation * Matrix4x4.CreateTranslation(tgtA);
            s.Normals = RigMath.NormalMatrix(stretch * rotation);
            return s;
        }

        //carries on from another segment (a last finger joint, the toes): its turn and stretch, about this joint
        static Segment Continue(string name, Segment parent, Vector3 srcA, Vector3 tgtA)
        {
            Matrix4x4 linear = parent.Transform;
            linear.M41 = linear.M42 = linear.M43 = 0;
            return new Segment() { Name = name, Transform = Matrix4x4.CreateTranslation(-srcA) * linear * Matrix4x4.CreateTranslation(tgtA), Normals = parent.Normals };
        }

        void Put(int joint, Segment s, int bone)
        {
            if (joint < 0 || s == null) return;
            if (s.Weights == null) s.Weights = p => One(bone);
            _seg[joint] = s;
            if (bone >= 0) _usedBones.Add(bone);
        }

        //weights along a polyline of the rig's bones: the two bones of the nearest segment, by how far along it the vertex is
        Func<Vector3, List<(int, float)>> Along(List<Vector3> points, List<int> bones)
        {
            return p =>
            {
                if (points.Count < 2) return One(bones[0]);
                RigMath.Nearest(points, p, out int i, out float t);
                return bones[i] == bones[i + 1] ? One(bones[i]) : new List<(int, float)>() { (bones[i], 1 - t), (bones[i + 1], t) };
            };
        }

        //the rig's bones from one down to another, both included
        List<int> TargetPath(int from, int to)
        {
            List<int> path = new List<int>();
            for (int b = to; b >= 0; b = _rig.Bones[b].ParentIndex)
            {
                path.Add(b);
                if (b == from) { path.Reverse(); return path; }
            }
            return null;
        }

        /* A source chain laid along a rig chain by arc length: each source joint lands the same fraction of the way
         * along the rig's (stretch) or the same distance (keep length, clamped at its end), and each bone between two
         * of them becomes a segment onto the matching stretch of the rig's polyline. */
        void Chain(string name, List<int> srcJoints, List<int> tgtBones, List<int> weightBones, bool stretch, bool lastGetsSegment)
        {
            List<Vector3> sp = srcJoints.Select(j => _fj[j]).ToList(), tp = tgtBones.Select(TP).ToList();
            float[] sAt = RigMath.ArcLengths(sp), tAt = RigMath.ArcLengths(tp);
            float sLen = sAt[sAt.Length - 1], tLen = tAt[tAt.Length - 1];
            Vector3 OnTarget(float s) => RigMath.PointAt(tp, tAt, stretch ? (sLen > 0 ? s / sLen * tLen : 0) : s);
            Func<Vector3, List<(int, float)>> weights = Along(tp, weightBones);
            Segment last = null;
            for (int k = 0; k + 1 < sp.Count; k++)
            {
                last = Seg(_src.Joints[srcJoints[k]].Name, sp[k], sp[k + 1], OnTarget(sAt[k]), OnTarget(sAt[k + 1]));
                last.Weights = weights;
                Put(srcJoints[k], last, -1);
            }
            if (lastGetsSegment && last != null)
            {
                Segment end = Continue(_src.Joints[srcJoints[sp.Count - 1]].Name, last, sp[sp.Count - 1], OnTarget(sAt[sp.Count - 1]));
                end.Weights = weights;
                Put(srcJoints[sp.Count - 1], end, -1);
            }
            foreach (int b in weightBones) _usedBones.Add(b);
            _r.Report.Add(name + ": " + sp.Count + " joint(s), " + sLen.ToString("0.000") + " m, onto " + string.Join(" ", tgtBones.Select(b => RigRoles.Short(_rig.Bones[b].Name))) + " (" + tLen.ToString("0.000") + " m)" + (stretch ? ", stretched to fit" : ", keeping its length") + "; weighted along it per vertex.");
        }

        void Report(string what, int srcA, int srcB, int tgtA, int tgtB)
        {
            float ls = (_fj[srcB] - _fj[srcA]).Length(), lt = (TP(tgtB) - TP(tgtA)).Length();
            double turn = ls > 1e-5f && lt > 1e-5f ? Math.Acos(Math.Max(-1, Math.Min(1, Vector3.Dot(Vector3.Normalize(_fj[srcB] - _fj[srcA]), Vector3.Normalize(TP(tgtB) - TP(tgtA)))))) * 180 / Math.PI : 0;
            _r.Report.Add(what + ": " + _src.Joints[srcA].Name + " -> " + RigRoles.Short(_rig.Bones[tgtA].Name) + ", " + ls.ToString("0.000") + " -> " + lt.ToString("0.000") + " m (x" + (ls > 1e-5f ? lt / ls : 1).ToString("0.00") + "), turned " + turn.ToString("0") + " deg");
        }

        void BuildSegments()
        {
            _seg = new Segment[_src.Joints.Count];

            //---- torso: the hips to the neck, by arc length onto the rig's spine, weighted per vertex along it
            int hips = S("HIPS");
            int neckJoint = _r.Roles.Has("NECK") ? S("NECK") : _src.Joints[S("HEAD")].Parent;
            List<int> spine = _src.Path(hips, neckJoint);
            int tgtHips = T("HIPS"), tgtNeck = T("NECK");
            List<int> tgtSpine = TargetPath(tgtHips, tgtNeck);
            if (spine == null || spine.Count < 2) { spine = new List<int>() { hips, neckJoint }; _r.Warnings.Add("The neck is not under the hips in the file, so the torso is one straight piece."); }
            if (tgtSpine == null) tgtSpine = new List<int>() { tgtHips, tgtNeck };
            List<int> torsoBones = new List<int>(tgtSpine);
            int pelvis = T("PELVIS");
            if (pelvis >= 0 && _rig.Bones[pelvis].ParentIndex == tgtHips) torsoBones[0] = pelvis;   //the lower body hangs off PELVIS, as retail skins it
            torsoBones[torsoBones.Count - 1] = torsoBones[torsoBones.Count - 2];                   //the top of the chest stays on the spine, not the neck
            Chain("Torso", spine, tgtSpine, torsoBones, true, false);

            //---- the neck, onto NECK up to where the head goes; the head moved, not turned
            Vector3 headAt = TP("HEAD");
            int head = _r.Roles.Has("HEAD") ? S("HEAD") : -1;
            if (head >= 0 && _o.HeadAtOwnHeight)
            {
                string l = Both("LEFTARM") ? "LEFTARM" : "LEFTSHOULDER", r = Both("RIGHTARM") ? "RIGHTARM" : "RIGHTSHOULDER";
                if (Both(l) && Both(r))
                {
                    Vector3 srcShoulders = (_fj[S(l)] + _fj[S(r)]) * 0.5f, tgtShoulders = (TP(l) + TP(r)) * 0.5f;
                    headAt = tgtShoulders + (_fj[head] - srcShoulders);
                    _r.Report.Add("Head: kept at its own height over the shoulders, " + ((TP("HEAD") - headAt).Length() * 100).ToString("0.0") + " cm from " + _rig.Name + "'s HEAD joint.");
                }
            }
            Segment neckSeg = null;
            if (head >= 0)
            {
                List<int> neck = _src.Path(neckJoint, head);
                if (neck != null && neck.Count >= 2)
                {
                    List<Vector3> sp = neck.Select(j => _fj[j]).ToList();
                    float[] at = RigMath.ArcLengths(sp);
                    float len = at[at.Length - 1];
                    for (int k = 0; k + 1 < neck.Count; k++)
                    {
                        Vector3 a = Vector3.Lerp(TP(tgtNeck), headAt, len > 0 ? at[k] / len : 0), b = Vector3.Lerp(TP(tgtNeck), headAt, len > 0 ? at[k + 1] / len : 1);
                        neckSeg = Seg(_src.Joints[neck[k]].Name, sp[k], sp[k + 1], a, b);
                        Put(neck[k], neckSeg, tgtNeck);
                    }
                    Report("Neck", neckJoint, head, tgtNeck, T("HEAD"));
                }
                Segment headSeg = _o.KeepHeadShape || neckSeg == null
                    ? new Segment() { Name = "head", Transform = Matrix4x4.CreateTranslation(headAt - _fj[head]) }
                    : Continue("head", neckSeg, _fj[head], headAt);
                Put(head, headSeg, T("HEAD"));
            }
            else
                _r.Warnings.Add("No head joint was found: whatever sits above the neck rides the neck.");

            //---- arms and hands
            foreach (string side in new[] { "LEFT", "RIGHT" })
            {
                string[] parts = { "SHOULDER", "ARM", "FOREARM", "HAND" };
                List<string> present = parts.Where(p => Both(side + p)).ToList();
                Segment forearm = null;
                for (int i = 0; i + 1 < present.Count; i++)
                {
                    string a = side + present[i], b = side + present[i + 1];
                    Segment s = Seg(_src.Joints[S(a)].Name, _fj[S(a)], _fj[S(b)], TP(a), TP(b));
                    Put(S(a), s, T(a));
                    Report(a.Substring(side.Length).ToLowerInvariant() == "shoulder" ? side.ToLowerInvariant() + " collar" : side.ToLowerInvariant() + " " + present[i].ToLowerInvariant(), S(a), S(b), T(a), T(b));
                    if (present[i] == "FOREARM") forearm = s;
                }
                foreach (string twist in new[] { "ARMROLL", "FOREARMROLL" })
                    if (_r.Roles.Has(side + twist))
                    {
                        int bone = twist == "FOREARMROLL" && T(side + "FOREARMROLL1") >= 0 ? T(side + "FOREARMROLL1") : T(side + twist);
                        if (bone >= 0) { _weightTo[S(side + twist)] = bone; _usedBones.Add(bone); }
                    }

                if (!Both(side + "HAND")) continue;
                int hand = S(side + "HAND");

                //the palm: wrist to the middle of the finger bases both have, turned with the palm's across axis
                List<string> bases = Fingers(side).Where(f => Both(side + "HAND" + f + "1")).ToList();
                List<string> across = bases.Where(f => f != "THUMB").ToList();
                Vector3? srcAcross = null, tgtAcross = null;
                if (across.Count >= 2)
                {
                    srcAcross = _fj[S(side + "HAND" + across.Last() + "1")] - _fj[S(side + "HAND" + across.First() + "1")];
                    tgtAcross = TP(side + "HAND" + across.Last() + "1") - TP(side + "HAND" + across.First() + "1");
                }
                else if (bases.Contains("THUMB") && across.Count == 1)
                {
                    srcAcross = _fj[S(side + "HAND" + across[0] + "1")] - _fj[S(side + "HANDTHUMB1")];
                    tgtAcross = TP(side + "HAND" + across[0] + "1") - TP(side + "HANDTHUMB1");
                }
                Segment palm;
                List<string> knuckles = across.Count != 0 ? across : bases;
                if (knuckles.Count != 0)
                {
                    Vector3 sk = knuckles.Select(f => _fj[S(side + "HAND" + f + "1")]).Aggregate(Vector3.Add) / knuckles.Count;
                    Vector3 tk = knuckles.Select(f => TP(side + "HAND" + f + "1")).Aggregate(Vector3.Add) / knuckles.Count;
                    palm = Seg(_src.Joints[hand].Name, _fj[hand], sk, TP(side + "HAND"), tk, srcAcross, tgtAcross);
                    _r.Report.Add(side.ToLowerInvariant() + " hand: wrist to " + knuckles.Count + " knuckle(s), " + (sk - _fj[hand]).Length().ToString("0.000") + " -> " + (tk - TP(side + "HAND")).Length().ToString("0.000") + " m" + (srcAcross.HasValue ? ", palm turned by its across axis" : ""));
                }
                else if (forearm != null)
                    palm = Continue(_src.Joints[hand].Name, forearm, _fj[hand], TP(side + "HAND"));
                else
                    palm = new Segment() { Name = _src.Joints[hand].Name, Transform = Matrix4x4.CreateTranslation(TP(side + "HAND") - _fj[hand]) };
                Put(hand, palm, T(side + "HAND"));

                //fingers, joint by joint; the last one carries on from the one before
                foreach (string f in Fingers(side))
                {
                    Segment previous = palm;
                    for (int j = 1; j <= 3; j++)
                    {
                        string role = side + "HAND" + f + j;
                        if (!Both(role)) break;
                        string next = side + "HAND" + f + (j + 1);
                        Segment s = j < 3 && Both(next)
                            ? Seg(_src.Joints[S(role)].Name, _fj[S(role)], _fj[S(next)], TP(role), TP(next), srcAcross, tgtAcross)
                            : Continue(_src.Joints[S(role)].Name, previous, _fj[S(role)], TP(role));
                        Put(S(role), s, T(role));
                        previous = s;
                    }
                }
            }

            //---- legs: one for one when both legs have as many joints, otherwise along the rig's leg by arc length
            foreach (string side in new[] { "LEFT", "RIGHT" })
            {
                List<int> src = new[] { "UPLEG", "LEG", "LEG2", "FOOT", "TOEBASE" }.Where(p => _r.Roles.Has(side + p)).Select(p => S(side + p)).ToList();
                List<int> tgt = new List<int>();
                int up = T(side + "UPLEG");
                if (up >= 0)
                {
                    tgt.Add(up);
                    for (int b = up; ;)
                    {
                        int child = Enumerable.Range(0, _rig.Bones.Count).FirstOrDefault(c => _rig.Bones[c].ParentIndex == b && new[] { "LEG", "FOOT", "TOEBASE", "PASTERN" }.Any(p => RigRoles.Short(_rig.Bones[c].Name).Equals(side + p, StringComparison.OrdinalIgnoreCase)));
                        if (child <= 0) break;
                        tgt.Add(child);
                        b = child;
                    }
                }
                if (src.Count < 2 || tgt.Count < 2) { if (src.Count != 0) _r.Warnings.Add("The " + side.ToLowerInvariant() + " leg could not be laid onto " + _rig.Name + "'s: it rides the hips."); continue; }
                if (src.Count == tgt.Count)
                {
                    Segment last = null;
                    for (int k = 0; k + 1 < src.Count; k++)
                    {
                        last = Seg(_src.Joints[src[k]].Name, _fj[src[k]], _fj[src[k + 1]], TP(tgt[k]), TP(tgt[k + 1]));
                        Put(src[k], last, tgt[k]);
                        Report(side.ToLowerInvariant() + " leg " + (k + 1), src[k], src[k + 1], tgt[k], tgt[k + 1]);
                    }
                    Put(src[src.Count - 1], Continue(_src.Joints[src[src.Count - 1]].Name, last, _fj[src[src.Count - 1]], TP(tgt[tgt.Count - 1])), tgt[tgt.Count - 1]);
                }
                else
                    Chain(side.ToLowerInvariant() + " leg (" + src.Count + " joints onto " + tgt.Count + ")", src, tgt, tgt, true, true);
            }

            //---- a tail, along the rig's tail
            if (_r.Roles.Has("TAIL") && T("TAIL1") >= 0)
            {
                List<int> src = new List<int>() { S("TAIL") };
                for (int j = S("TAIL"); ;)
                {
                    List<int> kids = _src.Joints[j].Children;
                    if (kids.Count == 0) break;
                    j = kids.OrderByDescending(c => _src.Descendants(c).Count()).First();
                    src.Add(j);
                }
                List<int> tgt = new List<int>() { T("TAIL1") };
                for (int b = tgt[0]; ;)
                {
                    int child = Enumerable.Range(0, _rig.Bones.Count).FirstOrDefault(c => _rig.Bones[c].ParentIndex == b && RigRoles.Short(_rig.Bones[c].Name).StartsWith("TAIL", StringComparison.OrdinalIgnoreCase));
                    if (child <= 0) break;
                    tgt.Add(child);
                    b = child;
                }
                if (src.Count >= 2 && tgt.Count >= 2) Chain("Tail", src, tgt, tgt, _o.StretchTail, true);
            }

            //---- joints named after a bone the rig has, that nothing above claimed (a jaw, say): their weight goes there
            for (int j = 0; j < _src.Joints.Count; j++)
            {
                if (_seg[j] != null || _weightTo.ContainsKey(j)) continue;
                RigRoles.Reading reading = RigRoles.Read(_src.Joints[j].Name);
                if (reading.Core.Length == 0) continue;
                string name = reading.Side + reading.Core.ToUpperInvariant();
                int bone = RigRoles.Bone(_rig, name);
                if (bone >= 0 && !_usedBones.Contains(bone)) { _weightTo[j] = bone; _usedBones.Add(bone); _r.Report.Add(_src.Joints[j].Name + " weights to " + _rig.Name + "'s " + name + " (same name)."); }
            }

            //---- everything else rides the nearest joint above it that has a segment
            Segment torso = _seg[hips];
            int riders = 0;
            for (int j = 0; j < _src.Joints.Count; j++)
            {
                if (_seg[j] != null) continue;
                int a = _src.Joints[j].Parent;
                while (a >= 0 && _seg[a] == null) a = _src.Joints[a].Parent;
                Segment carrier = a >= 0 ? _seg[a] : torso;
                Segment rider = new Segment() { Name = _src.Joints[j].Name, Transform = carrier.Transform, Normals = carrier.Normals, Weights = carrier.Weights, Rides = a >= 0 ? _src.Joints[a].Name : "the hips" };
                if (_weightTo.TryGetValue(j, out int own)) rider.Weights = p => One(own);
                _seg[j] = rider;
                if (_src.Joints[j].Weighted) riders++;
            }
            if (riders != 0)
                _r.Report.Add(riders + " weighted joint(s) play no part of their own and ride the joint above them: " + string.Join(", ", Enumerable.Range(0, _src.Joints.Count).Where(j => _seg[j].Rides != null && _src.Joints[j].Weighted).Take(12).Select(j => _src.Joints[j].Name + " (" + _seg[j].Rides + ")")) + (riders > 12 ? ", ..." : "") + ".");
            _r.Segments = _seg;
            _r.Moved = Enumerable.Range(0, _src.Joints.Count).Select(j => Vector3.Transform(_fj[j], _seg[j].Transform)).ToArray();
        }

        IEnumerable<string> Fingers(string side) => RigRoles.Fingers;
        #endregion

        #region THE MESHES
        void ApplyToMeshes()
        {
            Matrix4x4 frame = _r.Frame;
            int hips = S("HIPS");
            foreach (SourceRig.MeshSkin mesh in _src.Meshes)
            {
                int n = mesh.Positions.Length;
                MeshOut o = new MeshOut()
                {
                    MeshIndex = mesh.MeshIndex,
                    Name = mesh.Name,
                    Positions = new Vector3[n],
                    Normals = mesh.Normals == null ? null : new Vector3[n],
                    Tangents = mesh.Tangents == null ? null : new Vector3[n],
                    Bitangents = mesh.Bitangents == null ? null : new Vector3[n],
                    Bones = new int[n][],
                    Weights = new float[n][],
                    FlipWinding = _r.Mirrored,
                };
                for (int i = 0; i < n; i++)
                {
                    List<(int joint, float weight)> w = mesh.Skinned ? mesh.Weights[i] : new List<(int, float)>() { (mesh.RigidJoint >= 0 ? mesh.RigidJoint : hips, 1f) };
                    if (w.Count == 0) w = new List<(int, float)>() { (hips, 1f) };
                    Vector3 p = Fit(mesh.Positions[i]);
                    Vector3 pos = Vector3.Zero, nrm = Vector3.Zero, tan = Vector3.Zero, bit = Vector3.Zero;
                    float total = 0;
                    foreach ((int joint, float weight) in w)
                    {
                        Segment s = _seg[joint];
                        pos += Vector3.Transform(p, s.Transform) * weight;
                        if (o.Normals != null) nrm += Vector3.TransformNormal(Vector3.TransformNormal(mesh.Normals[i], frame), s.Normals) * weight;
                        if (o.Tangents != null) tan += Vector3.TransformNormal(Vector3.TransformNormal(mesh.Tangents[i], frame), s.Transform) * weight;
                        if (o.Bitangents != null) bit += Vector3.TransformNormal(Vector3.TransformNormal(mesh.Bitangents[i], frame), s.Transform) * weight;
                        total += weight;
                    }
                    pos /= total;
                    o.Positions[i] = pos;
                    if (o.Normals != null) o.Normals[i] = SourceRig.SafeNormalize(nrm);
                    if (o.Tangents != null) o.Tangents[i] = SourceRig.SafeNormalize(tan);
                    if (o.Bitangents != null) o.Bitangents[i] = SourceRig.SafeNormalize(bit);

                    Dictionary<int, float> weights = new Dictionary<int, float>();
                    foreach ((int joint, float weight) in w)
                        foreach ((int bone, float bw) in _seg[joint].Weights(pos))
                            weights[bone] = (weights.TryGetValue(bone, out float had) ? had : 0) + weight * bw;
                    KeyValuePair<int, float>[] top = weights.Where(x => x.Value > 1e-4f).OrderByDescending(x => x.Value).Take(4).ToArray();
                    if (top.Length == 0) top = new[] { new KeyValuePair<int, float>(T("HIPS"), 1f) };
                    float sum = top.Sum(x => x.Value);
                    o.Bones[i] = top.Select(x => x.Key).ToArray();
                    o.Weights[i] = top.Select(x => x.Value / sum).ToArray();
                }
                _r.Meshes.Add(o);
            }
        }

        /* How well the result sits on the rig, as ModelImportRig.ScoreFit measures it: each bone against the centre of the
         * vertices weighted to it. Retail models sit 2.5-7.8 cm; stretched limbs carry their flesh further off-centre. */
        void Measure()
        {
            Dictionary<int, Vector3> sum = new Dictionary<int, Vector3>();
            Dictionary<int, float> total = new Dictionary<int, float>();
            foreach (MeshOut m in _r.Meshes)
                for (int i = 0; i < m.Positions.Length; i++)
                    for (int k = 0; k < m.Bones[i].Length; k++)
                    {
                        int b = m.Bones[i][k];
                        sum[b] = (sum.TryGetValue(b, out Vector3 s) ? s : Vector3.Zero) + m.Positions[i] * m.Weights[i][k];
                        total[b] = (total.TryGetValue(b, out float t) ? t : 0) + m.Weights[i][k];
                    }
            if (sum.Count == 0) return;
            _r.Fit = (float)sum.Keys.Average(b => (sum[b] / total[b] - TP(b)).Length());
            int bones = sum.Count;
            _r.Report.Add("Result: " + _r.Meshes.Sum(m => m.Positions.Length) + " vertices on " + bones + " of " + _rig.Name + "'s " + _rig.Bones.Count + " bones; each bone sits " + (_r.Fit * 100).ToString("0.0") + " cm on average from the centre of what is weighted to it (the importer treats up to 15 cm as built on the rig).");
            List<string> missed = new List<string>();
            foreach (string role in new[] { "HEAD", "LEFTHAND", "RIGHTHAND", "LEFTFOOT", "RIGHTFOOT" })
                if (T(role) >= 0 && !sum.ContainsKey(T(role))) missed.Add(role);
            if (missed.Count != 0) _r.Warnings.Add("Nothing is weighted to " + string.Join(", ", missed) + ": those parts of the body won't move with the rig.");
        }
        #endregion

        #region WRITING IT BACK
        /// <summary>
        /// Rewrites the source scene as if OpenCAGE had exported the model on the rig (bind-space vertices in the file's units),
        /// so the model importer takes it as a mesh on that rig.
        /// </summary>
        /// <param name="boneName">The rig's bone index -> a name the importer reads (ModelIO.BoneName).</param>
        public static void WriteBack(Result result, Scene scene, Func<int, string> boneName)
        {
            float unit = result.Source.UnitScale;
            foreach (MeshOut o in result.Meshes)
            {
                Mesh mesh = scene.Meshes[o.MeshIndex];
                for (int i = 0; i < o.Positions.Length; i++)
                {
                    mesh.Vertices[i] = RigMath.A(o.Positions[i] * unit);
                    if (o.Normals != null && mesh.Normals.Count == o.Positions.Length) mesh.Normals[i] = RigMath.A(o.Normals[i]);
                    if (o.Tangents != null && mesh.Tangents.Count == o.Positions.Length) mesh.Tangents[i] = RigMath.A(o.Tangents[i]);
                    if (o.Bitangents != null && mesh.BiTangents.Count == o.Positions.Length) mesh.BiTangents[i] = RigMath.A(o.Bitangents[i]);
                }
                mesh.Bones.Clear();
                Dictionary<int, Bone> bones = new Dictionary<int, Bone>();
                for (int i = 0; i < o.Positions.Length; i++)
                    for (int k = 0; k < o.Bones[i].Length; k++)
                    {
                        int b = o.Bones[i][k];
                        if (!bones.TryGetValue(b, out Bone bone))
                        {
                            Matrix4x4 bind = result.TargetBind[b];
                            bind.Translation *= unit;
                            Matrix4x4.Invert(bind, out Matrix4x4 offset);
                            bones[b] = bone = new Bone() { Name = boneName(b), OffsetMatrix = RigMath.ToAssimp(offset) };
                            mesh.Bones.Add(bone);
                        }
                        bone.VertexWeights.Add(new VertexWeight(i, o.Weights[i][k]));
                    }
                if (o.FlipWinding)
                    foreach (Face face in mesh.Faces)
                        if (face.IndexCount == 3) { int t = face.Indices[1]; face.Indices[1] = face.Indices[2]; face.Indices[2] = t; }
            }
        }
        #endregion
    }
}
