using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace OpenCAGE.Rigging
{
    /// <summary>
    /// Builds a display rig: a resized copy of a gameplay rig (MALE, ALIEN...) that a character's mesh is skinned to while its
    /// animation, AI, capsule, IK and weapons still run on the gameplay rig.
    /// SKELE\DEFS\&lt;name&gt;.BIN names the gameplay rig as lo-res and this one as hi-res; the SKELE\MAPS mapping between them
    /// maps the pose each frame (translation out = scale x translation in + offset), so rest offsets times s with a mapping of
    /// scale s draw every pose s times the size. Writes SK, MAPS, DEFS and DB.BIN rows into ANIMATION.PAK (global), plus copies of
    /// the gameplay rig's bone groups and mirror table. The gameplay rig's ragdoll is kept, so corpses pose on gameplay-size joints.
    /// </summary>
    public static class DisplayRigBuilder
    {
        public const string RigDir = @"DATA\ANIM_SYS\SKELE\";

        /// <summary>The markers off the root that carry targets out into the world (where to aim, where to put a hand) and feed the character's culling box: kept at the gameplay size.</summary>
        public static readonly string[] KeptMarkers = { "L_FOOT", "L_HAND", "R_FOOT", "R_HAND", "REFERENCE_ROOT" };

        /// <summary>The source path older display rigs were filed under; still recognised so they can be replaced.</summary>
        public const string SourcePrefix = @"OPENCAGE\DISPLAYRIG\";

        /* The source path a rig is filed under in SKELE\DB.BIN; must be P:\\CONTENT\BUILD\REFERENCESKELETONS\<name>, as shipped.
         * The game finds DATA\REFERENCESKELETONS\<name>_BONE_GROUPS.TXT from it; any other path leaves bone_groups null and the game
         * crashes in Actor::AnimationSystem::evaluate_layer_masks on spawn (GOG AI+0x291c31, Steam 0x291751). */
        public const string ShippedSourcePrefix = @"P:\\CONTENT\BUILD\REFERENCESKELETONS\";

        public class Options
        {
            /// <summary>The rig whose animation drives it.</summary>
            public string Gameplay = "MALE";

            /// <summary>The new rig's name (upper case letters, digits and underscores).</summary>
            public string Name = "";

            /// <summary>Its size against the gameplay rig.</summary>
            public float Scale = 1;

            /// <summary>Bone name (without the rig's prefix) -> a factor on that bone's distance from its parent, on top of Scale.</summary>
            public Dictionary<string, float> BoneScale = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Bone name -> metres added to that bone's offset from its parent (in the parent's frame), at rest and in every pose.</summary>
            public Dictionary<string, Vector3> BoneOffset = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);

        }

        public class Result
        {
            public string Problem;
            public bool Replaced;
            public Skeleton Skeleton;
            public string MappingFile;
            public List<string> Written = new List<string>();
            public List<string> Report = new List<string>();
            public bool Ok => Problem == null;
        }

        /// <summary>What is wrong with a name for a new rig, or null.</summary>
        public static string NameProblem(Animation anim, string name, out bool replaces)
        {
            replaces = false;
            if (string.IsNullOrWhiteSpace(name)) return "Give the rig a name.";
            if (!Regex.IsMatch(name, "^[A-Z][A-Z0-9_]{1,31}$")) return "A rig's name is 2 to 32 upper case letters, digits and underscores, starting with a letter.";
            SkeletonDB.SkeletonEntry existing = anim?.SkeletonIndex?.Skeletons.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing == null) return null;
            if ((existing.SourcePath ?? "").StartsWith(SourcePrefix, StringComparison.OrdinalIgnoreCase)) { replaces = true; return null; }
            //Filed like a shipped rig, a display rig is told apart by its skeleton, which keeps the gameplay rig's name inside
            string inner = anim.GetSkeleton(existing)?.Skeleton?.Name;
            if (string.Equals(existing.SourcePath, ShippedSourcePrefix + name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(inner) && !string.Equals(inner, name, StringComparison.OrdinalIgnoreCase)
                && anim.SkeletonIndex.Mappings.Any(m => string.Equals(m.SkeletonA, name, StringComparison.OrdinalIgnoreCase) && string.Equals(m.Filename, name + "_MAP_" + inner, StringComparison.OrdinalIgnoreCase)))
            { replaces = true; return null; }
            return "The game already has a rig called " + name + " (one it ships, or one made outside OpenCAGE's display rig tool): pick another name.";
        }

        /// <summary>Each bone's scale on the new rig: 1 for the root and the kept markers, Scale x its own factor otherwise.</summary>
        public static float[] BoneScales(Skeleton gameplay, Options o)
        {
            float[] s = new float[gameplay.Bones.Count];
            for (int i = 0; i < s.Length; i++)
            {
                string bone = RigRoles.Short(gameplay.Bones[i].Name);
                bool kept = gameplay.Bones[i].ParentIndex < 0 || KeptMarkers.Contains(bone, StringComparer.OrdinalIgnoreCase);
                s[i] = kept ? 1f : o.Scale * (o.BoneScale.TryGetValue(bone, out float f) ? f : 1f);
            }
            return s;
        }

        public static Result Create(Animation anim, Options o)
        {
            Result r = new Result();
            try { Build(anim, o, r); }
            catch (Exception e) { r.Problem = "The display rig could not be built: " + e.Message; }
            return r;
        }

        private static PAK2.File Entry(Animation anim, string name) =>
            anim.PAK.Entries.FirstOrDefault(f => string.Equals(f.Filename, name, StringComparison.OrdinalIgnoreCase));

        private static void Put(Animation anim, Result r, string name, byte[] content)
        {
            PAK2.File e = Entry(anim, name);
            if (e == null) anim.PAK.Entries.Add(new PAK2.File { Filename = name, Content = content });
            else e.Content = content;
            r.Written.Add(name + " (" + content.Length + " bytes" + (e == null ? ")" : ", replaced)"));
        }

        private static void Build(Animation anim, Options o, Result r)
        {
            if (anim == null || !anim.Loaded) { r.Problem = "ANIMATION.PAK is not loaded."; return; }
            string name = (o.Name ?? "").Trim().ToUpperInvariant();
            string problem = NameProblem(anim, name, out bool replaces);
            if (problem != null) { r.Problem = problem; return; }
            r.Replaced = replaces;
            if (!(o.Scale > 0.05f && o.Scale < 20f)) { r.Problem = "The scale has to be between 0.05 and 20."; return; }
            foreach (KeyValuePair<string, float> b in o.BoneScale) if (!(b.Value > 0.05f && b.Value < 20f)) { r.Problem = "Bone scale " + b.Key + " has to be between 0.05 and 20."; return; }

            Animation.SkeletonAsset gameplayAsset = anim.GetSkeleton(o.Gameplay);
            Skeleton gameplay = gameplayAsset?.Skeleton;
            if (gameplay == null) { r.Problem = "There is no rig called " + o.Gameplay + "."; return; }
            string g = anim.SkeletonIndex.Skeletons.First(x => string.Equals(x.Name, o.Gameplay, StringComparison.OrdinalIgnoreCase)).Name;
            if (string.Equals(g, name, StringComparison.OrdinalIgnoreCase)) { r.Problem = "A display rig needs a name of its own."; return; }
            foreach (string bone in o.BoneScale.Keys.Concat(o.BoneOffset.Keys))
                if (RigRoles.Bone(gameplay, bone) < 0) { r.Problem = g + " has no bone " + bone + "."; return; }
            if (gameplay.Bones.Count > short.MaxValue) { r.Problem = "Too many bones."; return; }

            uint nameId = Utilities.AnimationHashedString(name), gId = Utilities.AnimationHashedString(g);
            string mapName = name + "_MAP_" + g;
            uint mapId = Utilities.AnimationHashedString(mapName);
            string source = ShippedSourcePrefix + name;
            string hkt = @"N:\CONTENT\BUILD\REFERENCESKELETONS\" + name + ".HKT";
            foreach (string s in new[] { name, mapName, source, hkt })
            {
                anim.AddName(s, false);
                anim.AddName(s, true);
            }

            float[] scale = BoneScales(gameplay, o);
            Vector3[] offset = new Vector3[gameplay.Bones.Count];
            for (int i = 0; i < offset.Length; i++)
                if (o.BoneOffset.TryGetValue(RigRoles.Short(gameplay.Bones[i].Name), out Vector3 v)) offset[i] = v;

            //---- SK: the gameplay rig's, resized bone by bone
            PAK2.File gameplaySk = Entry(anim, gameplay.Filepath) ?? Entry(anim, RigDir + @"SK\" + gId);
            if (gameplaySk == null) { r.Problem = "The 32 bit skeleton file of " + g + " is not in ANIMATION.PAK."; return; }
            string skPath = RigDir + @"SK\" + nameId;
            Skeleton sk = new Skeleton(gameplaySk.Content, anim.Strings, skPath);
            if (!sk.Loaded) { r.Problem = g + "'s skeleton could not be read."; return; }
            for (int i = 0; i < sk.Bones.Count; i++)
            {
                Skeleton.Bone b = sk.Bones[i];
                b.Translation = new Vector4(b.Translation.X * scale[i] + offset[i].X, b.Translation.Y * scale[i] + offset[i].Y, b.Translation.Z * scale[i] + offset[i].Z, b.Translation.W);
            }
            sk.IK.DistanceToEye *= o.Scale;
            sk.Retargets.Clear();
            sk.Retargets.Add(new Skeleton.Retarget { Skeleton = g, MappingFile = mapName });
            byte[] skBytes = sk.ToBytes();
            Skeleton check = new Skeleton(skBytes, anim.Strings, skPath);
            if (!check.Loaded || check.Bones.Count != gameplay.Bones.Count) { r.Problem = "The resized skeleton did not read back."; return; }

            //---- MAPS: every bone onto itself, at its scale, both ways (A = the new rig, B = the gameplay rig)
            Animation.MappingAsset template = anim.Mappings.FirstOrDefault(m => (m.Mapping?.Havok?.Header.PointerSize ?? 0) == 4 && (string.Equals(m.SkeletonA, g, StringComparison.OrdinalIgnoreCase) || string.Equals(m.SkeletonB, g, StringComparison.OrdinalIgnoreCase)))
                ?? anim.Mappings.FirstOrDefault(m => (m.Mapping?.Havok?.Header.PointerSize ?? 0) == 4 && m.Mapping.GetMappers().Count == 2);
            if (template == null) { r.Problem = "No skeleton mapping in ANIMATION.PAK to lay the new one out from."; return; }
            byte[] havok = BuildMapperHavok(template.Mapping, gameplay.Bones.Count, scale, offset);
            byte[] maps;
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(nameId); w.Write(gId); w.Write(havok.Length); w.Write(havok);
                int floats = gameplay.FloatMaps.Count;
                for (int t = 0; t < 2; t++) { w.Write(floats); for (int i = 0; i < floats; i++) w.Write(i); }
                maps = ms.ToArray();
            }
            string mapsPath = RigDir + @"MAPS\" + mapId;
            SkeletonMapping mapping = new SkeletonMapping(maps, anim.Strings, mapsPath);
            List<HavokPackfile.SkeletonMapper> mappers = mapping.Loaded ? mapping.GetMappers() : null;
            if (mappers == null || mappers.Count != 2 || mappers.Any(m => m.Mappings.Count != gameplay.Bones.Count || m.UnmappedBones.Count != 0))
            { r.Problem = "The new skeleton mapping did not read back (" + (mappers == null ? "unreadable" : string.Join(", ", mappers.Select(m => m.ToString()))) + ")."; return; }

            //---- DEFS: the gameplay rig's definition, with this as the hi-res (display) rig
            PAK2.File gameplayDef = Entry(anim, RigDir + @"DEFS\" + gId + ".BIN");
            if (gameplayDef == null || gameplayDef.Content.Length < 176) { r.Problem = g + " has no skeleton definition (SKELE\\DEFS) to copy."; return; }
            byte[] def = (byte[])gameplayDef.Content.Clone();
            void W(int at, uint v) => Buffer.BlockCopy(BitConverter.GetBytes(v), 0, def, at, 4);
            W(0, nameId);
            W(56, nameId); W(60, Utilities.AnimationHashedString(hkt));
            W(64, 1); W(68, gId); for (int at = 72; at <= 104; at += 4) W(at, 0);

            //---- DB.BIN: the rig, and the mapping both ways
            SkeletonDB db = anim.SkeletonIndex;
            db.Skeletons.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            db.Mappings.RemoveAll(x => string.Equals(x.Filename, mapName, StringComparison.OrdinalIgnoreCase));
            db.Skeletons.Add(new SkeletonDB.SkeletonEntry { Name = name, SourcePath = source });
            db.Mappings.Add(new SkeletonDB.MappingEntry { SkeletonA = name, SkeletonB = g, Filename = mapName });
            db.Mappings.Add(new SkeletonDB.MappingEntry { SkeletonA = g, SkeletonB = name, Filename = mapName });

            //---- The files. Save writes parsed assets back over their entries, so swap out any parsed copy of a rig being replaced
            anim.Skeletons.RemoveAll(a => string.Equals(a.Name, nameId.ToString(), StringComparison.OrdinalIgnoreCase));
            anim.Mappings.RemoveAll(a => (string.Equals(a.SkeletonA, name, StringComparison.OrdinalIgnoreCase) && string.Equals(a.SkeletonB, g, StringComparison.OrdinalIgnoreCase))
                                      || (string.Equals(a.SkeletonB, name, StringComparison.OrdinalIgnoreCase) && string.Equals(a.SkeletonA, g, StringComparison.OrdinalIgnoreCase)));
            Put(anim, r, skPath, skBytes);
            Put(anim, r, mapsPath, maps);
            Put(anim, r, RigDir + @"DEFS\" + nameId + ".BIN", def);
            foreach (string side in new[] { "_BONE_GROUPS.TXT", "_MIRROR.TXT" })
            {
                PAK2.File from = Entry(anim, @"DATA\REFERENCESKELETONS\" + g + side);
                if (from != null) Put(anim, r, @"DATA\REFERENCESKELETONS\" + name + side, (byte[])from.Content.Clone());
            }
            anim.Skeletons.Add(new Animation.SkeletonAsset { Name = nameId.ToString(), Skeleton = check });
            anim.Mappings.Add(new Animation.MappingAsset { SkeletonA = name, SkeletonB = g, Mapping = mapping });

            r.Skeleton = check;
            r.MappingFile = mapName;
            int head = RigRoles.Bone(check, "HEAD");
            List<System.Numerics.Matrix4x4> pose = check.GetBindPose(), gameplayPose = gameplay.GetBindPose();
            r.Report.Add(name + ": " + g + " x" + o.Scale.ToString("0.###") + (o.BoneScale.Count + o.BoneOffset.Count != 0 ? " with " + (o.BoneScale.Count + o.BoneOffset.Count) + " bone change(s)" : "") + ", " + check.Bones.Count + " bones"
                + (head >= 0 ? "; head joint " + pose[head].Translation.Y.ToString("0.000") + " m up (" + g + " " + gameplayPose[head].Translation.Y.ToString("0.000") + " m)" : "")
                + "; the root and " + string.Join(", ", KeptMarkers.Where(k => RigRoles.Bone(gameplay, k) >= 0)) + " kept at " + g + "'s size.");
            r.Report.Add("Keeps " + g + "'s ragdoll: corpses are posed on " + g + "-size joints, so a scaled character dies with long, thin limbs.");
        }

        /* Rewrites a template mapping packfile's two hkaSkeletonMappers (32 bit) to map every bone onto itself: new arrays go on the
         * end of the data and their local fixups move to them; nothing is unmapped and extracted motion is the identity. Mapper 0 maps
         * A (display rig) onto B (gameplay rig), mapper 1 B onto A. hkaSkeletonMapper: +0x3C simple mappings {ptr, size, capacity},
         * +0x54 unmapped bones, +0x60 extracted motion hkQsTransform. SimpleMapping (64 bytes): int16 bone A, int16 bone B, pad to 16,
         * then translation, rotation, scale as 16 byte vectors. */
        private static byte[] BuildMapperHavok(SkeletonMapping template, int bones, float[] scale, Vector3[] offset)
        {
            HavokPackfile hk = new HavokPackfile(template.Havok.ToBytes());
            List<HavokPackfile.PackfileObject> mappers = hk.Objects.Where(x => x.ClassName == "hkaSkeletonMapper").ToList();
            if (hk.Header.PointerSize != 4 || mappers.Count != 2) throw new Exception("the template mapping is not a 32 bit pair of mappers");
            List<byte> payload = new List<byte>(hk.DataPayload);
            List<HavokPackfile.LocalFixup> local = hk.LocalFixups.ToList();
            for (int m = 0; m < 2; m++)
            {
                uint obj = mappers[m].DataOffset;
                while (payload.Count % 16 != 0) payload.Add(0);
                int at = payload.Count;
                for (int i = 0; i < bones; i++)
                {
                    float s = m == 1 ? scale[i] : 1f / scale[i];
                    Vector3 t = m == 1 ? offset[i] : -offset[i] / scale[i];
                    byte[] row = new byte[64];
                    Buffer.BlockCopy(BitConverter.GetBytes((short)i), 0, row, 0, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes((short)i), 0, row, 2, 2);
                    float[] values = { t.X, t.Y, t.Z, 0, 0, 0, 0, 1, s, s, s, s };
                    for (int k = 0; k < values.Length; k++) Buffer.BlockCopy(BitConverter.GetBytes(values[k]), 0, row, 16 + k * 4, 4);
                    payload.AddRange(row);
                }
                local.RemoveAll(f => f.Src == obj + 0x3C || f.Src == obj + 0x54);
                local.Add(new HavokPackfile.LocalFixup { Src = obj + 0x3C, Dst = (uint)at });
                byte[] data = payload.ToArray();
                Buffer.BlockCopy(BitConverter.GetBytes(bones), 0, data, (int)obj + 0x40, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(unchecked((int)0x80000000) | bones), 0, data, (int)obj + 0x44, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(0), 0, data, (int)obj + 0x54, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(0), 0, data, (int)obj + 0x58, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(unchecked((int)0x80000000)), 0, data, (int)obj + 0x5C, 4);
                float[] identity = { 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 };
                for (int k = 0; k < identity.Length; k++) Buffer.BlockCopy(BitConverter.GetBytes(identity[k]), 0, data, (int)obj + 0x60 + k * 4, 4);
                payload = new List<byte>(data);
            }
            while (payload.Count % 16 != 0) payload.Add(0);
            hk.DataPayload = payload.ToArray();
            hk.LocalFixups = local.OrderBy(f => f.Src).ToList();
            return hk.ToBytes();
        }
    }
}
