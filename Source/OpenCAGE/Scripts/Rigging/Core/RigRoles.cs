using CATHODE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenCAGE.Rigging
{
    /// <summary>
    /// Maps a foreign skeleton's joints to body parts named after CATHODE's bones (HIPS, LEFTFOREARM, LEFTHANDINDEX2...), which
    /// every shipped character rig uses; the spine, neck and tail are the chains between named parts. Names from any tool (Biped,
    /// Unreal, Mixamo, Blender, Valve, OpenCAGE's CS2_BONE_ exports) are split into words and read for side, part and number.
    /// </summary>
    public static class RigRoles
    {
        public static readonly string[] Fingers = { "THUMB", "INDEX", "MIDDLE", "RING", "PINKY" };

        /// <summary>Every part, in the order the mapping is shown and worked out (each limb from its root out).</summary>
        public static List<string> AllRoles()
        {
            List<string> roles = new List<string>() { "HIPS", "NECK", "HEAD" };
            foreach (string side in new[] { "LEFT", "RIGHT" })
            {
                roles.AddRange(new[] { "SHOULDER", "ARM", "ARMROLL", "FOREARM", "FOREARMROLL", "HAND" }.Select(o => side + o));
                foreach (string f in Fingers) for (int j = 1; j <= 3; j++) roles.Add(side + "HAND" + f + j);
                roles.AddRange(new[] { "UPLEG", "LEG", "LEG2", "FOOT", "TOEBASE" }.Select(o => side + o));
            }
            roles.Add("TAIL");
            return roles;
        }

        /// <summary>What a role means, for the mapping editor.</summary>
        public static string Describe(string role)
        {
            if (role == "HIPS") return "hips / pelvis (the root of the body)";
            if (role == "NECK") return "neck (the spine is everything between the hips and this)";
            if (role == "HEAD") return "head";
            if (role == "TAIL") return "the first tail joint (the rest of the tail follows from it)";
            string side = role.StartsWith("LEFT") ? "left " : role.StartsWith("RIGHT") ? "right " : "";
            string part = role.Substring(side.Length == 0 ? 0 : side.Length - 1).ToUpperInvariant();
            switch (part)
            {
                case "SHOULDER": return side + "collar bone (clavicle)";
                case "ARM": return side + "upper arm";
                case "ARMROLL": return side + "upper arm twist";
                case "FOREARM": return side + "forearm";
                case "FOREARMROLL": return side + "forearm twist";
                case "HAND": return side + "hand (wrist)";
                case "UPLEG": return side + "thigh";
                case "LEG": return side + "calf (knee)";
                case "LEG2": return side + "extra leg joint of a digitigrade (reverse-jointed) leg";
                case "FOOT": return side + "foot (ankle)";
                case "TOEBASE": return side + "toes / ball of the foot";
            }
            foreach (string f in Fingers)
                if (part.StartsWith("HAND" + f)) return side + f.ToLowerInvariant() + " " + part.Substring(4 + f.Length);
            return role;
        }

        #region READING A NAME
        /// <summary>A joint's name read for what it says: its side, its body part, and the numbers in it.</summary>
        public class Reading
        {
            public string Side = "";             //LEFT, RIGHT or empty
            public string Core = "";             //the words that are left, run together: "forearm", "upleg", "finger"
            public List<int> Numbers = new List<int>();
            public List<string> NumberText = new List<string>();
            public override string ToString() => Side + "|" + Core + "|" + string.Join(",", NumberText);
        }

        private static readonly HashSet<string> Noise = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bip", "biped", "xeno", "mixamorig", "valvebiped", "def", "org", "mch", "cc", "base", "b", "bn", "bone", "bones", "jnt", "joint",
            "j", "armature", "cs", "skeleton", "rig", "sk", "c", "jt", "ik", "fk", "bind", "deform",
        };

        public static Reading Read(string name)
        {
            Reading reading = new Reading();
            if (string.IsNullOrEmpty(name)) return reading;
            string s = name;
            int colon = Math.Max(s.LastIndexOf(':'), s.LastIndexOf('|'));
            if (colon >= 0) s = s.Substring(colon + 1);

            //OpenCAGE's exports: CS2_BONE_020_LEFTFOREARM - the part is the CATHODE name after the number
            Match own = Regex.Match(s, @"CS2_BONE_\d+_(.+)$");
            if (own.Success) s = own.Groups[1].Value;

            //words: split on anything not a letter or digit, on lower-to-upper case changes, and between letters and digits
            List<string> words = new List<string>();
            StringBuilder word = new StringBuilder();
            void Flush() { if (word.Length != 0) { words.Add(word.ToString()); word.Clear(); } }
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!char.IsLetterOrDigit(c)) { Flush(); continue; }
                if (word.Length != 0)
                {
                    char p = word[word.Length - 1];
                    bool caseBreak = char.IsLower(p) && char.IsUpper(c);
                    bool kindBreak = char.IsDigit(p) != char.IsDigit(c);
                    //"LUpArmTwist": an upper case letter followed by upper-then-lower starts a new word ("L" | "Up")
                    bool acronymBreak = char.IsUpper(p) && char.IsUpper(c) && i + 1 < s.Length && char.IsLower(s[i + 1]);
                    if (caseBreak || kindBreak || acronymBreak) Flush();
                }
                word.Append(c);
            }
            Flush();

            //CATHODE's own names are one word: LEFTFOREARM, RIGHTHANDINDEX2
            if (words.Count == 1 || (words.Count == 2 && words[1].All(char.IsDigit)))
            {
                string w = words[0].ToUpperInvariant();
                if (w.StartsWith("LEFT") && w.Length > 4) { reading.Side = "LEFT"; words[0] = w.Substring(4); }
                else if (w.StartsWith("RIGHT") && w.Length > 5) { reading.Side = "RIGHT"; words[0] = w.Substring(5); }
            }

            List<string> rest = new List<string>();
            bool afterNoise = false;
            foreach (string w in words)
            {
                string lower = w.ToLowerInvariant();
                if (lower.All(char.IsDigit))
                {
                    //the number of a prefix ("Bip01", "Xeno01") says nothing about the joint
                    if (!afterNoise) { reading.Numbers.Add(int.Parse(lower.Length > 6 ? lower.Substring(0, 6) : lower)); reading.NumberText.Add(lower); }
                    afterNoise = false;
                    continue;
                }
                afterNoise = false;
                if (reading.Side.Length == 0)
                {
                    if (lower == "l" || lower == "left" || lower == "lt" || lower == "lf") { reading.Side = "LEFT"; continue; }
                    if (lower == "r" || lower == "right" || lower == "rt" || lower == "rg") { reading.Side = "RIGHT"; continue; }
                }
                else if (lower == "l" || lower == "r" || lower == "left" || lower == "right") continue;
                if (Noise.Contains(lower)) { afterNoise = true; continue; }
                rest.Add(lower);
            }
            reading.Core = string.Concat(rest);
            return reading;
        }

        /// <summary>
        /// The part a joint's name says it is, or null. Fingers carry their joint number (LEFTHANDINDEX2); spine and tail joints
        /// come back as "SPINE" and "TAIL", placed in the chain by the hierarchy.
        /// </summary>
        public static string RoleFromName(string name)
        {
            Reading r = Read(name);
            string c = r.Core;
            if (c.Length == 0) return null;
            string side = r.Side;
            bool twist = c.Contains("twist") || c.Contains("roll");
            //ends and tips are markers, not joints anything bends at: "head_end", "Bip01 L Finger0Nub", "index_tip"
            if (c.EndsWith("end") || c.Contains("nub") || (c.Contains("tip") && !c.Contains("hip"))) return null;

            //fingers first: "LeftHandIndex1" holds "hand" too
            string finger = null;
            if (c.Contains("thumb")) finger = "THUMB";
            else if (c.Contains("index") || c.Contains("pointer")) finger = "INDEX";
            else if (c.Contains("middle")) finger = "MIDDLE";
            else if (c.Contains("ring")) finger = "RING";
            else if (c.Contains("pinky") || c.Contains("pinkie") || c.Contains("little")) finger = "PINKY";
            if (finger != null)
            {
                if (c.Contains("metacarpal") || c.Contains("meta")) return null;
                int joint = r.Numbers.Count != 0 ? r.Numbers[0] : 1;
                if (joint == 0) joint = 1;                                  //"thumb_0" style
                if (joint > 3 && joint < 10) joint = 3;
                if (joint > 10) joint = joint % 10 == 0 ? 1 : Math.Min(3, joint % 10);   //"index_01" -> 1
                return side.Length == 0 ? null : side + "HAND" + finger + joint;
            }
            if (c == "finger" || c.StartsWith("finger"))
            {
                //3ds Max Biped: Finger<N>[<M>] - N 0..4 is the finger (thumb first), M the joint after the first
                if (side.Length == 0 || r.NumberText.Count == 0) return null;
                string digits = r.NumberText[0];
                int f = digits[0] - '0';
                if (f < 0 || f > 4) return null;
                int joint = digits.Length > 1 ? (digits[1] - '0') + 1 : 1;
                if (joint < 1 || joint > 3) return null;
                return side + "HAND" + Fingers[f] + joint;
            }

            if (c.Contains("horselink")) return side.Length == 0 ? null : side + "LEG2";
            if (twist)
            {
                if (side.Length == 0) return null;
                if (c.Contains("fore") || c.Contains("lower")) return side + "FOREARMROLL";
                if (c.Contains("arm") || c.Contains("upper")) return side + "ARMROLL";
                return null;
            }

            if (c.Contains("clavicle") || c.Contains("collar") || c == "clav" || c == "shoulder") return side.Length == 0 ? null : side + "SHOULDER";
            if (c == "upperarm" || c == "uparm" || c == "arm" || c == "humerus" || c == "upperarm" || c == "armupper") return side.Length == 0 ? null : side + "ARM";
            if (c == "forearm" || c == "lowerarm" || c == "elbow" || c == "armlower" || c == "foreearm") return side.Length == 0 ? null : side + "FOREARM";
            if (c == "hand" || c == "wrist") return side.Length == 0 ? null : side + "HAND";
            if (c == "thigh" || c == "upleg" || c == "upperleg" || c == "legupper" || c == "femur" || (c == "hip" && side.Length != 0)) return side.Length == 0 ? null : side + "UPLEG";
            if (c == "calf" || c == "leg" || c == "shin" || c == "lowerleg" || c == "leglower" || c == "knee" || c == "tibia") return side.Length == 0 ? null : side + "LEG";
            if (c == "foot" || c == "ankle") return side.Length == 0 ? null : side + "FOOT";
            if (c == "toe" || c == "toes" || c == "toebase" || c == "ball" || c == "toe0") return side.Length == 0 ? null : side + "TOEBASE";

            if (side.Length != 0) return null;
            if (c == "hips" || c == "hip" || c == "pelvis") return "HIPS";
            if (c.StartsWith("spine") || c == "chest" || c == "upperchest" || c == "torso" || c == "abdomen" || c == "back" || c == "waist") return "SPINE";
            if (c.StartsWith("neck")) return "NECK";
            if (c == "head") return "HEAD";
            if (c.StartsWith("tail")) return "TAIL";
            return null;
        }
        #endregion

        #region THE SOURCE'S ROLES
        public class Assignment
        {
            public Dictionary<string, int> Joint = new Dictionary<string, int>();
            public Dictionary<string, string> How = new Dictionary<string, string>();
            public List<string> Notes = new List<string>();

            public int this[string role] => Joint.TryGetValue(role, out int j) ? j : -1;
            public bool Has(string role) => Joint.ContainsKey(role);
        }

        /// <summary>
        /// Works out which joint of <paramref name="rig"/> plays each part, from names then the hierarchy.
        /// <paramref name="overrides"/> (role -> joint name, empty to leave the role out) wins over both.
        /// </summary>
        public static Assignment Assign(SourceRig rig, IDictionary<string, string> overrides = null)
        {
            Assignment a = new Assignment();
            Dictionary<string, List<int>> claims = new Dictionary<string, List<int>>();
            for (int j = 0; j < rig.Joints.Count; j++)
            {
                string role = RoleFromName(rig.Joints[j].Name);
                if (role == null) continue;
                if (!claims.TryGetValue(role, out List<int> list)) claims[role] = list = new List<int>();
                list.Add(j);
            }

            HashSet<string> forced = new HashSet<string>();
            if (overrides != null)
                foreach (KeyValuePair<string, string> o in overrides)
                {
                    string role = o.Key.ToUpperInvariant();
                    forced.Add(role);
                    if (string.IsNullOrEmpty(o.Value) || o.Value == "-") { a.How[role] = "left out"; continue; }
                    int j = rig.Find(o.Value);
                    if (j < 0) { a.Notes.Add("The mapping names '" + o.Value + "' for " + role + ", but the file has no such joint."); continue; }
                    a.Joint[role] = j;
                    a.How[role] = "set";
                }

            void Take(string role, int joint, string how)
            {
                if (forced.Contains(role) || joint < 0 || a.Joint.ContainsKey(role)) return;
                a.Joint[role] = joint;
                a.How[role] = how;
            }

            //the shallowest claimant, preferring one under 'under' when given
            int Claim(string role, int under = -1)
            {
                if (!claims.TryGetValue(role, out List<int> list)) return -1;
                IEnumerable<int> pool = list;
                if (under >= 0 && list.Any(j => rig.IsAncestor(under, j))) pool = list.Where(j => rig.IsAncestor(under, j));
                return pool.OrderBy(rig.Depth).First();
            }

            //HIPS: a joint called exactly "hips" beats "pelvis" (a CATHODE-named rig has both, and its HIPS is the root)
            int hips = Enumerable.Range(0, rig.Joints.Count).Where(j => string.Equals(Read(rig.Joints[j].Name).Core, "hips", StringComparison.Ordinal)).OrderBy(rig.Depth).DefaultIfEmpty(-1).First();
            Take("HIPS", hips >= 0 ? hips : Claim("HIPS"), "name");
            hips = a["HIPS"];

            Take("HEAD", Claim("HEAD", hips), "name");
            Take("NECK", Claim("NECK", hips), "name");
            if (!a.Has("NECK") && a.Has("HEAD"))
            {
                int parent = rig.Joints[a["HEAD"]].Parent;
                if (parent >= 0 && parent != hips && RoleFromName(rig.Joints[parent].Name) != "SPINE") Take("NECK", parent, "hierarchy (the head's parent)");
            }
            Take("TAIL", Claim("TAIL", hips), "name");

            foreach (string side in new[] { "LEFT", "RIGHT" })
            {
                int last = hips;
                foreach (string part in new[] { "SHOULDER", "ARM", "FOREARM", "HAND" })
                {
                    Take(side + part, Claim(side + part, last), "name");
                    if (a.Has(side + part)) last = a[side + part];
                }
                //a forearm the names miss, between the upper arm and the hand
                if (!a.Has(side + "FOREARM") && a.Has(side + "ARM") && a.Has(side + "HAND"))
                {
                    List<int> path = rig.Path(a[side + "ARM"], a[side + "HAND"]);
                    if (path != null && path.Count >= 3) Take(side + "FOREARM", path[path.Count - 2], "hierarchy (between the upper arm and the hand)");
                }
                Take(side + "ARMROLL", Claim(side + "ARMROLL", a[side + "ARM"]), "name");
                Take(side + "FOREARMROLL", Claim(side + "FOREARMROLL", a[side + "FOREARM"]), "name");
                int hand = a[side + "HAND"];
                foreach (string f in Fingers)
                {
                    int prev = hand;
                    for (int j = 1; j <= 3; j++)
                    {
                        string role = side + "HAND" + f + j;
                        Take(role, Claim(role, prev), "name");
                        if (!a.Has(role) && j > 1 && a.Has(side + "HAND" + f + (j - 1)))
                        {
                            //the next joint down the finger, when the names stop numbering them
                            int up = a[side + "HAND" + f + (j - 1)];
                            int child = rig.Joints[up].Children.Where(c => rig.Joints[c].Weighted).DefaultIfEmpty(-1).First();
                            if (child >= 0 && RoleFromName(rig.Joints[child].Name) == null) Take(role, child, "hierarchy (next joint down the finger)");
                        }
                        if (a.Has(role)) prev = a[role];
                    }
                }

                last = hips;
                foreach (string part in new[] { "UPLEG", "LEG", "LEG2", "FOOT", "TOEBASE" })
                {
                    Take(side + part, Claim(side + part, last), "name");
                    if (a.Has(side + part)) last = a[side + part];
                }
                if (!a.Has(side + "LEG") && a.Has(side + "UPLEG") && a.Has(side + "FOOT"))
                {
                    List<int> path = rig.Path(a[side + "UPLEG"], a[side + "FOOT"]);
                    if (path != null && path.Count == 3) Take(side + "LEG", path[1], "hierarchy (between the thigh and the foot)");
                    if (path != null && path.Count == 4) { Take(side + "LEG", path[1], "hierarchy"); Take(side + "LEG2", path[2], "hierarchy (a second knee)"); }
                }
                if (!a.Has(side + "TOEBASE") && a.Has(side + "FOOT"))
                {
                    int child = rig.Joints[a[side + "FOOT"]].Children.Where(c => rig.Joints[c].Weighted).DefaultIfEmpty(-1).First();
                    if (child >= 0) Take(side + "TOEBASE", child, "hierarchy (the foot's child)");
                }
            }

            foreach (string role in AllRoles())
                if (claims.TryGetValue(role, out List<int> list) && list.Count > 1 && a.Has(role))
                    a.Notes.Add(role + ": " + list.Count + " joints answer to it (" + string.Join(", ", list.Take(4).Select(j => rig.Joints[j].Name)) + "); took " + rig.Joints[a[role]].Name + ".");
            return a;
        }
        #endregion

        #region THE GAME RIG'S BONES
        /// <summary>The bone of a game rig a role lands on, or -1 when the rig has no such bone.</summary>
        public static int TargetBone(Skeleton rig, string role)
        {
            if (rig == null || string.IsNullOrEmpty(role)) return -1;
            if (role == "TAIL") role = "TAIL1";
            return Bone(rig, role);
        }

        /// <summary>A game rig's bone by its CATHODE name, with or without the rig's prefix.</summary>
        public static int Bone(Skeleton rig, string name)
        {
            for (int i = 0; i < rig.Bones.Count; i++)
            {
                string n = rig.Bones[i].Name ?? "";
                if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase) || n.EndsWith(":" + name, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>A bone's name without the rig's prefix: MALE:LEFTHAND -> LEFTHAND.</summary>
        public static string Short(string name)
        {
            if (name == null) return "";
            int colon = name.IndexOf(':');
            return colon >= 0 ? name.Substring(colon + 1) : name;
        }

        /// <summary>Whether a game rig has the bones a rebind fit is measured from (hips, neck, both upper arms, both feet).</summary>
        public static bool CanTarget(Skeleton rig)
        {
            if (rig == null || rig.Bones.Count == 0) return false;
            foreach (string b in new[] { "HIPS", "NECK", "LEFTARM", "RIGHTARM", "LEFTFOOT", "RIGHTFOOT" })
                if (Bone(rig, b) < 0) return false;
            return true;
        }
        #endregion
    }
}
