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
    /// Writes a <see cref="PropRig"/> into ANIMATION.PAK as an environment rig plus an animation set of its own (named after the rig)
    /// holding its clips. The SK is a shipped environment rig with at least as many bones, re-posed and renamed (a skeleton's bone
    /// count is fixed in its file; spare bones hang off the root); the DEFS names it as both lo- and hi-res rig with no ragdoll.
    /// Prop rigs only carry parts: a character skeleton also needs bone groups, a mirror table, a ragdoll and contexts.
    /// </summary>
    public static class PropRigBuilder
    {
        /// <summary>The source path older prop rigs were filed under; still recognised so they can be replaced.</summary>
        public const string SourcePrefix = @"OPENCAGE\PROPRIG\";

        /* Filed under the shipped prefix (the game finds bone groups from this path) in a folder of their own, as environment rigs
         * are; the OPENCAGE folder tells them from shipped rigs. */
        public const string ShippedSourcePrefix = @"P:\\CONTENT\BUILD\REFERENCESKELETONS\OPENCAGE\PROPS\";

        private static bool IsOwn(string sourcePath) => (sourcePath ?? "").StartsWith(SourcePrefix, StringComparison.OrdinalIgnoreCase) || (sourcePath ?? "").StartsWith(ShippedSourcePrefix, StringComparison.OrdinalIgnoreCase);

        public class Result
        {
            public string Problem;
            public Skeleton Skeleton;
            public CathodeLib.Animation.AnimationSet Set;
            public string Template;
            public bool Replaced;
            public List<string> Clips = new List<string>();
            public List<string> Written = new List<string>();
            public List<string> Report = new List<string>();
            public bool Ok => Problem == null;
        }

        public static string NameProblem(CathodeLib.Animation anim, string name, out bool replaces)
        {
            replaces = false;
            if (string.IsNullOrWhiteSpace(name)) return "Give the prop's rig a name.";
            if (!Regex.IsMatch(name, "^[A-Z][A-Z0-9_]{1,40}$")) return "A rig's name is 2 to 41 upper case letters, digits and underscores, starting with a letter.";
            SkeletonDB.SkeletonEntry existing = anim?.SkeletonIndex?.Skeletons.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            bool setExists = anim?.Sets?.Any(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)) ?? false;
            if (existing == null && !setExists) return null;
            if (existing != null && IsOwn(existing.SourcePath)) { replaces = true; return null; }
            return "The game already has a rig or animation set called " + name + ": pick another name.";
        }

        /// <summary>The shipped environment rig with the fewest bones that still has room for <paramref name="bones"/>.</summary>
        public static Skeleton Template(CathodeLib.Animation anim, int bones, int nameLength = 0)
        {
            HashSet<string> environment = new HashSet<string>(anim.SkeletonDefs.Values.Where(d => d.IsEnvironment).Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
            //a name at least as long as the new one: the copy is renamed inside its Havok data, in the room the old name takes up
            return anim.Skeletons.Select(a => a.Skeleton).Where(s => s != null && environment.Contains(s.Name) && s.Bones.Count >= bones && s.Name.Length >= nameLength && !string.IsNullOrEmpty(s.Filepath))
                .Where(s => anim.SkeletonIndex.Skeletons.Any(e => string.Equals(e.Name, s.Name, StringComparison.OrdinalIgnoreCase) && !IsOwn(e.SourcePath)))
                .OrderBy(s => s.Bones.Count).ThenBy(s => s.Name).FirstOrDefault();
        }

        private static PAK2.File Entry(CathodeLib.Animation anim, string name) => anim.PAK.Entries.FirstOrDefault(f => string.Equals(f.Filename, name, StringComparison.OrdinalIgnoreCase));

        private static void Put(CathodeLib.Animation anim, Result r, string name, byte[] content)
        {
            PAK2.File e = Entry(anim, name);
            if (e == null) anim.PAK.Entries.Add(new PAK2.File { Filename = name, Content = content });
            else e.Content = content;
            r.Written.Add(name + " (" + content.Length + " bytes" + (e == null ? ")" : ", replaced)"));
        }

        /// <param name="maxWorldspace">End the rig with a MAX_WORLDSPACE bone on the root, as shipped environment rigs do; the template
        /// then needs one bone more than the prop.</param>
        public static Result Create(CathodeLib.Animation anim, string name, PropRig rig, IList<string> clipNames = null, bool maxWorldspace = true)
        {
            Result r = new Result();
            try { Build(anim, (name ?? "").Trim().ToUpperInvariant(), rig, clipNames, maxWorldspace, r); }
            catch (Exception e) { r.Problem = "The prop's rig could not be built: " + e.Message; }
            return r;
        }

        private static void Build(CathodeLib.Animation anim, string name, PropRig rig, IList<string> clipNames, bool maxWorldspace, Result r)
        {
            if (anim == null || !anim.Loaded) { r.Problem = "ANIMATION.PAK is not loaded."; return; }
            string problem = NameProblem(anim, name, out bool replaces);
            if (problem != null) { r.Problem = problem; return; }
            r.Replaced = replaces;
            if (rig == null || rig.Joints.Count < 1) { r.Problem = "There is no rig to write."; return; }
            Skeleton template = Template(anim, rig.Joints.Count + (maxWorldspace ? 1 : 0), name.Length);
            if (template == null) { r.Problem = "No shipped prop rig has room for " + rig.Joints.Count + " bones."; return; }
            r.Template = template.Name;
            uint nameId = Utilities.AnimationHashedString(name), templateId = Utilities.AnimationHashedString(template.Name);
            string rigDir = DisplayRigBuilder.RigDir;
            string source = ShippedSourcePrefix + name;
            string hkt = @"N:\CONTENT\BUILD\REFERENCESKELETONS\OPENCAGE\PROPS\" + name + ".HKT";

            //---- Replacing a rig: its old set, clips and files go first
            if (replaces) RemoveOwn(anim, name, nameId, r);

            //---- names: the rig's, its bones' (plain and debug, as shipped skeleton names are)
            List<string> boneNames = new List<string>();
            for (int i = 0; i < template.Bones.Count; i++) boneNames.Add(i < rig.Joints.Count ? rig.Joints[i].Name : maxWorldspace && i == template.Bones.Count - 1 ? "MAX_WORLDSPACE" : name + "_SPARE_" + i);
            foreach (string s in new[] { name, source, hkt }.Concat(boneNames)) { anim.AddName(s, false); anim.AddName(s, true); }

            //---- SK: the template's, re-posed and renamed
            PAK2.File templateSk = Entry(anim, template.Filepath);
            if (templateSk == null) { r.Problem = "The skeleton file of " + template.Name + " is not in ANIMATION.PAK."; return; }
            string skPath = rigDir + @"SK\" + nameId;
            Skeleton sk = new Skeleton(templateSk.Content, anim.Strings, skPath);
            if (!sk.Loaded) { r.Problem = template.Name + "'s skeleton could not be read."; return; }
            for (int i = 0; i < sk.Bones.Count; i++)
            {
                Skeleton.Bone b = sk.Bones[i];
                b.Name = boneNames[i];
                if (i < rig.Joints.Count)
                {
                    HavokPackfile.SampledTransform local = rig.RestLocal(i);
                    b.ParentIndex = rig.Joints[i].Parent;
                    b.Translation = new Vector4(local.Translation, b.Translation.W);
                    b.Rotation = local.Rotation;
                    b.Scale = new Vector4(local.Scale, b.Scale.W);
                }
                else
                {
                    b.ParentIndex = i == 0 ? -1 : 0;
                    b.Translation = new Vector4(0, 0, 0, b.Translation.W);
                    b.Rotation = Quaternion.Identity;
                    b.Scale = new Vector4(1, 1, 1, b.Scale.W);
                }
            }
            sk.PartialNameBoneMaps.Clear();
            sk.FloatMaps.Clear();
            sk.Retargets.Clear();
            //Shipped rigs carry their own name inside the Havok data, not the template's
            if (!sk.RenameInPlace(name)) { r.Problem = "The prop's skeleton could not be renamed inside its Havok data (" + template.Name + "'s name has no room for " + name + "): pick a shorter name."; return; }
            byte[] skBytes = sk.ToBytes();
            Skeleton check = new Skeleton(skBytes, anim.Strings, skPath);
            if (!check.Loaded || check.Bones.Count != template.Bones.Count) { r.Problem = "The prop's skeleton did not read back."; return; }
            /* The bone-by-name lookup, one entry per bone, as shipped environment rigs have (props animate without it). Filled on the
             * copy read back with the lookup empty, so it is written in the order the game searches it. */
            for (int i = 0; i < check.Bones.Count; i++) check.PartialNameBoneMaps.Add(new Skeleton.Mapping { Name = check.Bones[i].Name, Value = i });
            skBytes = check.ToBytes();
            check = new Skeleton(skBytes, anim.Strings, skPath);
            if (!check.Loaded || check.Bones.Count != template.Bones.Count || check.PartialNameBoneMaps.Count != check.Bones.Count ||
                check.PartialNameBoneMaps.Any(m => m.Value < 0 || m.Value >= check.Bones.Count || check.Bones[m.Value].Name != m.Name))
            { r.Problem = "The prop's skeleton did not read back with its bones' names."; return; }
            List<Matrix4x4> model = check.GetModelSpacePose();
            float worst = 0;
            for (int i = 0; i < rig.Joints.Count; i++) worst = Math.Max(worst, (model[i].Translation - rig.Joints[i].Rest.Translation).Length());
            if (worst > 0.001f) { r.Problem = "The prop's skeleton reads back up to " + (worst * 100).ToString("0.0") + " cm from where its parts are."; return; }

            //---- DEFS: the template's, naming this rig for both, no ragdoll
            PAK2.File templateDef = Entry(anim, rigDir + @"DEFS\" + templateId + ".BIN");
            if (templateDef == null || templateDef.Content.Length < 176) { r.Problem = template.Name + " has no skeleton definition to copy."; return; }
            byte[] def = (byte[])templateDef.Content.Clone();
            void W(int at, uint v) => Buffer.BlockCopy(BitConverter.GetBytes(v), 0, def, at, 4);
            uint hktId = Utilities.AnimationHashedString(hkt);
            W(0, nameId); W(4, nameId); W(8, hktId); W(12, 0); for (int at = 16; at <= 52; at += 4) W(at, 0);
            W(56, nameId); W(60, hktId); W(64, 0); for (int at = 68; at <= 104; at += 4) W(at, 0);
            W(108, 0);

            //---- DB.BIN
            anim.SkeletonIndex.Skeletons.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            anim.SkeletonIndex.Skeletons.Add(new SkeletonDB.SkeletonEntry { Name = name, SourcePath = source });

            anim.Skeletons.RemoveAll(a => string.Equals(a.Name, nameId.ToString(), StringComparison.OrdinalIgnoreCase));
            Put(anim, r, skPath, skBytes);
            Put(anim, r, rigDir + @"DEFS\" + nameId + ".BIN", def);
            anim.Skeletons.Add(new CathodeLib.Animation.SkeletonAsset { Name = nameId.ToString(), Skeleton = check });
            r.Skeleton = check;

            //---- the set: a clip DB named after the rig, empty, laid out from a shipped prop's
            AnimClipDB templateDb = anim.ClipDatabases.FirstOrDefault(d => string.Equals(d.Character, template.Name, StringComparison.OrdinalIgnoreCase))
                ?? anim.ClipDatabases.FirstOrDefault(d => anim.GetSet(d.Character)?.Kind == CathodeLib.Animation.AnimationKind.Environment);
            if (templateDb == null) { r.Problem = "No prop animation set to lay a new one out from."; return; }
            string folder = Path.GetDirectoryName(templateDb.Filepath) ?? @"DATA\ANIM_SYS";
            string dbPath = Path.Combine(folder, nameId + "_ANIM_CLIP_DB.BIN");
            AnimClipDB db = new AnimClipDB(templateDb.ToBytes(), anim.Strings, dbPath, anim.StringsDebug);
            db.Character = name;
            db.Animations.Clear(); db.BlendSets.Clear(); db.Contexts.Clear();
            byte[] dbBytes = db.ToBytes();
            db = new AnimClipDB(dbBytes, anim.Strings, dbPath, anim.StringsDebug);
            if (!db.Loaded) { r.Problem = "The prop's animation set did not read back."; return; }
            Put(anim, r, dbPath, dbBytes);
            anim.ClipDatabases.Add(db);

            //Its rows in the global index, as shipped props have: no section dependencies, no parent
            GlobalAnimClipDB index = anim.ClipIndex;
            GlobalAnimClipDB.ParentDependency templateParent = index.ParentDependencies.FirstOrDefault(p => string.Equals(p.Name, templateDb.Character, StringComparison.OrdinalIgnoreCase));
            index.DependencyMap.RemoveAll(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
            index.ParentDependencies.RemoveAll(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
            index.DependencyMap.Add(new GlobalAnimClipDB.DependencyRange { Name = name, FirstEntryIndex = (uint)index.SectionDependencyList.Count, EntryCount = 0 });
            index.ParentDependencies.Add(new GlobalAnimClipDB.ParentDependency { Name = name, Parent = templateParent?.Parent ?? "" });

            CathodeLib.Animation.AnimationSet set = new CathodeLib.Animation.AnimationSet { Name = name, Database = db, Skeleton = name, Kind = CathodeLib.Animation.AnimationKind.Environment };
            set.Contexts.Add(new CathodeLib.Animation.AnimationContext { Name = "", Set = set });
            anim.Sets.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            anim.Sets.Add(set);
            r.Set = set;

            //---- the clips, each a streamed section of its own, on the rig bone for bone (spare bones held at rest)
            List<short> trackToBone = Enumerable.Range(0, check.Bones.Count).Select(i => (short)i).ToList();
            HashSet<string> taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < rig.Clips.Count; c++)
            {
                PropRig.Clip clip = rig.Clips[c];
                string clipName = clipNames != null && c < clipNames.Count && !string.IsNullOrWhiteSpace(clipNames[c]) ? clipNames[c] : clip.Name;
                clipName = AnimationName(clipName);
                for (int k = 2; taken.Contains(clipName); k++) clipName = AnimationName(clip.Name) + "_" + k;
                taken.Add(clipName);
                string clipPath = @"ANIMATION\OPENCAGE\PROPS\" + name + @"\" + clipName.ToUpperInvariant();
                List<List<HavokPackfile.SampledTransform>> frames = clip.Frames.Select(pose =>
                {
                    List<HavokPackfile.SampledTransform> full = new List<HavokPackfile.SampledTransform>(pose);
                    for (int i = pose.Count; i < check.Bones.Count; i++)
                        full.Add(new HavokPackfile.SampledTransform { Translation = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One, HasTranslation = true, HasRotation = true, HasScale = true });
                    return full;
                }).ToList();
                if (!anim.AddClip(set, clipName, clipPath, name, trackToBone, frames, clip.FrameDuration))
                { r.Problem = "The clip '" + clipName + "' could not be added (" + frames.Count + " frames)."; return; }
                r.Clips.Add(clipName);
                r.Report.Add("Clip " + clipName + ": " + frames.Count + " frames at " + (1f / clip.FrameDuration).ToString("0.#") + " fps (" + ((frames.Count - 1) * clip.FrameDuration).ToString("0.##") + " s), stored as " + clipPath + ".");
            }
            r.Report.Insert(0, "Rig " + name + ": " + rig.Joints.Count + " bone(s) (the root and " + (rig.Joints.Count - 1) + " part(s)/joint(s)) in " + template.Name + "'s layout of " + template.Bones.Count + "; animation set " + name + " with " + r.Clips.Count + " clip(s).");
        }

        /* A name the set plays a clip by: lower case letters, digits and underscores */
        public static string AnimationName(string name)
        {
            string clean = new string((name ?? "").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) && c < 128 ? c : '_').ToArray()).Trim('_');
            if (clean.Length == 0) clean = "clip";
            if (char.IsDigit(clean[0])) clean = "clip_" + clean;
            return clean.Length > 60 ? clean.Substring(0, 60) : clean;
        }

        /* Removes the set, clips and files of a prop rig being replaced */
        private static void RemoveOwn(CathodeLib.Animation anim, string name, uint nameId, Result r)
        {
            CathodeLib.Animation.AnimationSet old = anim.GetSet(name);
            if (old != null)
            {
                foreach (CathodeLib.Animation.ClipReference clip in old.Contexts.SelectMany(c => c.Clips).ToList())
                    if (!anim.RemoveClip(clip, out string why)) r.Report.Add("An old clip " + clip.Name + " could not be taken out: " + why);
                if (old.Database != null)
                {
                    anim.ClipDatabases.Remove(old.Database);
                    anim.PAK.Entries.RemoveAll(e => string.Equals(e.Filename, old.Database.Filepath, StringComparison.OrdinalIgnoreCase));
                }
                anim.Sets.Remove(old);
            }
            anim.Skeletons.RemoveAll(a => string.Equals(a.Name, nameId.ToString(), StringComparison.OrdinalIgnoreCase));
            r.Report.Add("Replaced the rig, set and clips made before under " + name + ".");
        }
    }
}
