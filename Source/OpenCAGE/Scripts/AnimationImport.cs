using AlienPAK;
using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenCAGE
{
    /// <summary>
    /// Bring an animation in from a model file and add it to ANIMATION.PAK as a clip the game can
    /// play. The counterpart to the export in <see cref="CathodeLibExtensions.ExportAnimations"/>.
    /// </summary>
    public static class AnimationImport
    {
        /* Derived from the format table rather than written out here, so adding a format to it offers
         * that format for animation import too. */
        public static string FileFilter { get { return ModelExport.ModelExporter.ImportFilter(true); } }

        /// <summary>What to do with the rig's root bone, which is the engine's rather than the rig's.</summary>
        public enum RootHandling
        {
            /// <summary>Leave it to the engine unless the file actually animates it.</summary>
            Auto,

            /// <summary>Always leave it to the engine, whatever the file says.</summary>
            LeaveToEngine,

            /// <summary>Write out whatever the file holds, root motion and all.</summary>
            KeepAsAuthored,
        }

        public class Options
        {
            /// <summary>The rig the animation is built against.</summary>
            public string Rig = "";

            public RootHandling Root = RootHandling.Auto;

            /// <summary>Frames per second, or zero to work it out from the file.</summary>
            public float FrameRate = 0;

            /// <summary>Layer this clip's movement over whatever else is playing rather than replacing it.</summary>
            public bool Additive = false;

            /// <summary>
            /// Bring the animation across from a rig that isn't this one - see
            /// <see cref="AnimationRetarget"/>. Without it the file's nodes have to be named after
            /// the rig's own bones, which only an export from here will be.
            /// </summary>
            public bool Retarget = false;

            /// <summary>
            /// Which animation in the file to read. Most carry one, but a file that brings a rig in
            /// with it often brings a whole set of clips, and each is imported in its own right.
            /// </summary>
            public int Index = 0;

            /// <summary>
            /// Node name to bone index, for a rig built from this very file. Matching by name cannot
            /// work there: the rig is renamed into CATHODE's convention as it is created, while the
            /// clip still names the nodes the file called them. The importer knows both, so it says
            /// so rather than leaving the two to fail to meet.
            /// </summary>
            public Dictionary<string, int> NodeToBone;

            /// <summary>Keep only frames <see cref="StartFrame"/> to <see cref="EndFrame"/> of the file's clip (inclusive, from 0; -1 for its last).</summary>
            public int StartFrame = 0;
            public int EndFrame = -1;

            /// <summary>The set's context to file the clip in (WEAPON_HANDGUN, CROUCHED...), or null for its own unnamed one.</summary>
            public string Context = null;
        }

        /// <summary>What came out of a file, and everything worth telling the user before they commit.</summary>
        public class Reading
        {
            public List<List<HavokPackfile.SampledTransform>> Poses;
            public int Frames;
            public float FrameDuration;

            /// <summary>The rate the file itself claims, which is not always the one it was written at.</summary>
            public float FileFrameRate;

            public int Channels, Matched;

            /// <summary>
            /// How many of the matched nodes matched outright - an OpenCAGE export's bone index, the rig's own
            /// full bone name, or its bare joint name in the rig's own case (LEFTFOREARM) - rather than by a bare
            /// joint name written another way (LeftForeArm), as another rig sharing the names writes it.
            /// </summary>
            public int MatchedExactly;

            /// <summary>
            /// Whether the names prove the file is on this very rig. A file that only shares bare joint names
            /// (a Mixamo export with its namespace stripped) is on a skeleton that rests differently, and its
            /// angles copied bone for bone come out mangled - it wants retargeting.
            /// </summary>
            public bool NamesAuthoritative { get { return Matched > 0 && MatchedExactly * 2 >= Matched; } }

            /// <summary>Whether this reading should be redone retargeted: the file can be, and its names don't prove otherwise.</summary>
            public bool ShouldRetarget { get { return CanRetarget && !Retargeted && !NamesAuthoritative; } }

            /// <summary>How many frames the file's clip has, before any trimming.</summary>
            public int FileFrames;

            /// <summary>How big the file's bones are against the rig's - one means the units agree.</summary>
            public double Scale = 1;

            /// <summary>Whether the file moves the root bone at all.</summary>
            public bool RootAnimated;

            /// <summary>Whether this came across from another rig, and whether that was a mirror.</summary>
            public bool Retargeted, Mirrored;

            /// <summary>Whether the file could be brought across from its own rig onto this one.</summary>
            public bool CanRetarget;
            public string RetargetHint = "";

            public string Problem;
            public List<string> Warnings = new List<string>();

            public bool Ok { get { return Poses != null && Problem == null; } }
            public float Duration { get { return Frames > 1 ? (Frames - 1) * FrameDuration : FrameDuration; } }
        }

        /// <summary>
        /// The rig a set's clips are normally authored on, which is what a new one should use too.
        /// For most characters that is a shared reference rig such as MALE rather than the character's
        /// own - the engine retargets it at runtime.
        /// </summary>
        public static string DefaultRigFor(CathodeLib.Animation.AnimationSet set)
        {
            List<string> rigs = RigsFor(set);
            return rigs.Count == 0 ? (set?.Skeleton ?? "") : rigs[0];
        }

        /// <summary>
        /// The rigs worth offering for a set, the ones its own clips use first and then everything
        /// else the game has, so an import is not limited to what the set happens to do today.
        /// </summary>
        public static List<string> RigsFor(CathodeLib.Animation.AnimationSet set)
        {
            List<string> rigs = new List<string>();
            if (set == null) return rigs;

            Dictionary<string, int> used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (CathodeLib.Animation.ClipReference clip in set.Contexts.SelectMany(x => x.Clips))
            {
                string rig = clip.Animation?.SkeletonName;
                if (string.IsNullOrEmpty(rig)) continue;
                used.TryGetValue(rig, out int seen);
                used[rig] = seen + 1;
            }

            foreach (KeyValuePair<string, int> entry in used.OrderByDescending(x => x.Value)) rigs.Add(entry.Key);
            if (!string.IsNullOrEmpty(set.Skeleton) && !rigs.Contains(set.Skeleton, StringComparer.OrdinalIgnoreCase))
                rigs.Add(set.Skeleton);
            return rigs;
        }

        /// <summary>The path a clip gets stored under, keeping the file's own name.</summary>
        public static string PathFor(CathodeLib.Animation.AnimationSet set, string file)
        {
            string name = Sanitise(Path.GetFileNameWithoutExtension(file ?? "")).ToUpperInvariant();
            if (name.Length == 0) name = "IMPORTED";
            return @"ANIMATION\OPENCAGE\" + (set?.Name ?? "CLIPS").ToUpperInvariant() + "\\" + name;
        }

        /// <summary>A name that survives being hashed into the game's string table.</summary>
        public static string Sanitise(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            char[] content = name.ToCharArray();
            for (int i = 0; i < content.Length; i++)
                if (!char.IsLetterOrDigit(content[i]) && content[i] != '_') content[i] = '_';
            return new string(content);
        }

        #region READING
        /// <summary>The animations a file holds, in order: each one's name and frame count. Throws if the file can't be read.</summary>
        public static List<Tuple<string, int>> ClipsIn(string file)
        {
            Assimp.Scene scene;
            using (Assimp.AssimpContext context = new Assimp.AssimpContext())
                scene = context.ImportFile(file, Assimp.PostProcessSteps.None);
            List<Tuple<string, int>> clips = new List<Tuple<string, int>>();
            if (scene == null) return clips;
            foreach (Assimp.Animation animation in scene.Animations)
                clips.Add(Tuple.Create(animation.Name ?? "", Frames(animation, out double _, out double _)));
            return clips;
        }

        /// <summary>
        /// The name a clip of a file should be imported under, from its own name in the file - or null when that
        /// name says nothing (Mixamo calls every take 'mixamo.com', Blender 'Armature|Action', others 'Take 001').
        /// </summary>
        public static string ClipNameFrom(string name)
        {
            string stem = (name ?? "").Trim();
            int bar = stem.LastIndexOf('|');
            if (bar >= 0) stem = stem.Substring(bar + 1).Trim();
            string bare = stem.ToLowerInvariant();
            if (bare.Length == 0 || bare == "mixamo.com" || bare == "scene" || bare == "action" || bare == "animation" || bare == "default take"
                || bare.StartsWith("take") || bare.StartsWith("armatureaction") || bare == "armature")
                return null;
            string clean = Sanitise(stem).Trim('_').ToLowerInvariant();
            return clean.Length == 0 ? null : clean;
        }

        /// <summary>
        /// Read an animation out of a model file onto a rig. Nothing is changed by this - it is what
        /// the import dialog shows before anything is committed.
        /// </summary>
        public static Reading Read(string file, Skeleton rig, Options options)
        {
            Reading reading = new Reading();
            options = options ?? new Options();

            if (rig == null || rig.Bones.Count == 0) { reading.Problem = "There's no rig to import against."; return reading; }
            if (!File.Exists(file)) { reading.Problem = "That file doesn't exist."; return reading; }

            Assimp.Scene scene;
            try
            {
                using (Assimp.AssimpContext context = new Assimp.AssimpContext())
                    scene = context.ImportFile(file, Assimp.PostProcessSteps.None);
            }
            catch (Exception ex) { reading.Problem = "That file couldn't be read: " + ex.Message; return reading; }

            if (scene == null || scene.AnimationCount == 0) { reading.Problem = "There's no animation in that file."; return reading; }
            if (options.Index < 0 || options.Index >= scene.AnimationCount)
            {
                reading.Problem = "That file has " + scene.AnimationCount + " animation(s), so there is no number " + (options.Index + 1) + ".";
                return reading;
            }
            Assimp.Animation animation = scene.Animations[options.Index];
            if (scene.AnimationCount > 1 && options.Index == 0)
                reading.Warnings.Add("The file holds " + scene.AnimationCount + " animations; the first one, '"
                    + animation.Name + "', is the one being imported.");

            reading.Channels = animation.NodeAnimationChannelCount;
            reading.Frames = Frames(animation, out double firstKey, out double keyStep);
            reading.FileFrames = reading.Frames;
            if (reading.Frames < 1) { reading.Problem = "That animation has no keyframes."; return reading; }
            int lastFrame = options.EndFrame < 0 ? reading.Frames - 1 : options.EndFrame;
            if (options.StartFrame < 0 || options.StartFrame > lastFrame || lastFrame >= reading.Frames)
            {
                reading.Problem = "The clip has frames 0 to " + (reading.Frames - 1) + ", so it can't be cut to " + options.StartFrame + " - " + lastFrame + ".";
                return reading;
            }

            /* The rate the file declares is not always the one it was written at - FBX carries a
             * document-wide frame rate and a clip written at 30 can come back saying 24 - so work it
             * out from the clip's length, and let the caller override it. */
            reading.FileFrameRate = RateOf(animation, reading.Frames, keyStep * (reading.Frames - 1));

            reading.FrameDuration = options.FrameRate > 0 ? 1f / options.FrameRate
                : reading.FileFrameRate > 0 ? 1f / reading.FileFrameRate
                : 1f / 30f;
            if (reading.FrameDuration <= 0 || reading.FrameDuration > 1) reading.FrameDuration = 1f / 30f;

            /* Worked out whether or not it is asked for, so the dialog can offer it without reading
             * the file a second time. */
            string what;
            reading.CanRetarget = AnimationRetarget.Supports(rig) && AnimationRetarget.Looks(scene, out what);
            reading.RetargetHint = reading.CanRetarget ? Describe(scene, rig) : "";

            /* Coming off another rig entirely is a different job: nothing matches by name, so the
             * clip has to be carried across from one skeleton's proportions to the other's. */
            if (options.Retarget)
            {
                AnimationRetarget.Reading across = AnimationRetarget.Build(scene, animation, rig, reading.Frames);
                if (!across.Ok) { reading.Problem = across.Problem; return reading; }

                reading.Poses = Trim(across.Poses, options.StartFrame, lastFrame, reading);
                reading.Matched = across.Driven;
                reading.Retargeted = true;
                reading.Mirrored = across.Mirrored;
                reading.Scale = across.Scale;
                reading.Warnings.AddRange(across.Notes);
                reading.RootAnimated = RootMoves(reading.Poses);
                ApplyRoot(reading, options.Root);
                return reading;
            }

            List<List<HavokPackfile.SampledTransform>> poses = new List<List<HavokPackfile.SampledTransform>>(reading.Frames);
            for (int frame = 0; frame < reading.Frames; frame++) poses.Add(RestPose(rig));

            /* Our own exports cross each bone into the file's handedness (ModelIO.ToFileSpace), which
             * leaves their skeleton node an ordinary transform. Older ones mirrored that node instead
             * and kept the keys as the game stores them, so only cross back when the node says so. */
            Assimp.Node skeletonNode = scene.RootNode?.FindNode(AlienPAK.ModelIO.SkeletonNodeName);
            bool fileSpace = skeletonNode != null && skeletonNode.Transform.Determinant() > 0;

            List<string> unmatched = new List<string>();
            foreach (Assimp.NodeAnimationChannel channel in animation.NodeAnimationChannels)
            {
                int bone = BoneFor(channel.NodeName, rig, options.NodeToBone, out bool exact);
                if (bone < 0)
                {
                    if (unmatched.Count < 6) unmatched.Add(channel.NodeName);
                    continue;
                }
                reading.Matched++;
                if (exact) reading.MatchedExactly++;

                for (int frame = 0; frame < reading.Frames; frame++)
                {
                    HavokPackfile.SampledTransform pose = poses[frame][bone];
                    Vector3 translation = pose.Translation;
                    Quaternion turned = pose.Rotation;
                    if (fileSpace) AlienPAK.ModelIO.ToFileSpace(ref translation, ref turned);

                    //whatever the file keys replaces the rest value, still in the file's handedness until crossed back
                    double when = firstKey + (frame * keyStep);
                    if (Key(channel.PositionKeys, when, keyStep, out Vector3 position)) translation = position;
                    if (Key(channel.ScalingKeys, when, keyStep, out Vector3 scaling)) pose.Scale = scaling;
                    if (Key(channel.RotationKeys, when, keyStep, out Quaternion rotation)) turned = rotation;

                    if (fileSpace) AlienPAK.ModelIO.ToFileSpace(ref translation, ref turned);
                    pose.Translation = translation;
                    pose.Rotation = turned;
                    poses[frame][bone] = pose;
                }
            }

            if (reading.Matched == 0)
            {
                reading.Problem = "None of the " + reading.Channels + " animated nodes in that file match a bone on "
                    + rig.Name + ".\r\n\r\n" + (reading.CanRetarget
                        ? reading.RetargetHint
                        : "The rig has to be the one the animation was built on."
                          + (unmatched.Count == 0 ? "" : "\r\n\r\nThe file animates: " + string.Join(", ", unmatched) + "..."));
                return reading;
            }
            if (unmatched.Count != 0)
                reading.Warnings.Add((reading.Channels - reading.Matched) + " of " + reading.Channels
                    + " animated nodes don't match a bone and are ignored (" + string.Join(", ", unmatched) + ").");

            reading.Scale = MeasureScale(poses[0], rig);
            if (reading.Scale > 1.02 || reading.Scale < 0.98)
                reading.Warnings.Add("The file's bones are about " + reading.Scale.ToString("0.###")
                    + " times the size of the rig's, so it is probably in the wrong units. The animation will be the wrong shape.");
            if (!reading.NamesAuthoritative && reading.CanRetarget)
                reading.Warnings.Add("Only " + reading.MatchedExactly + " of the " + reading.Matched + " matched nodes carry this rig's own names; the rest share bare joint names written another way, "
                    + "as other rigs (Mixamo's) write them. If it comes out mangled, it is on another skeleton: convert it (retarget) instead.");

            poses = Trim(poses, options.StartFrame, lastFrame, reading);
            reading.RootAnimated = RootMoves(poses);
            reading.Poses = poses;
            ApplyRoot(reading, options.Root);
            return reading;
        }

        /* Keep only the frames asked for, and say so */
        private static List<List<HavokPackfile.SampledTransform>> Trim(List<List<HavokPackfile.SampledTransform>> poses, int first, int last, Reading reading)
        {
            if (poses == null || (first == 0 && last >= poses.Count - 1)) return poses;
            last = Math.Min(last, poses.Count - 1);
            List<List<HavokPackfile.SampledTransform>> kept = poses.GetRange(first, last - first + 1);
            reading.Frames = kept.Count;
            reading.Warnings.Add("Cut to frames " + first + " - " + last + " of the file's 0 - " + (poses.Count - 1) + ".");
            return kept;
        }

        /* How fast the clip really runs.
         *
         * Duration over ticks-per-second is the obvious reading and it is wrong for glTF: assimp
         * hands those key times over in MILLISECONDS while declaring a ticks-per-second that is
         * really keys-per-second, so the two contradict each other and dividing one by the other
         * turns a six second dance into three and a half minutes. Neither number can be trusted
         * alone, so work from the key times and take whichever unit gives a believable rate. */
        private static float RateOf(Assimp.Animation animation, int frames, double span)
        {
            if (frames < 2) return 0;

            foreach (double perSecond in new double[] { 1, 1000 })
            {
                if (span <= 0) break;
                double rate = (frames - 1) / (span / perSecond);
                if (rate >= 5 && rate <= 240) return (float)rate;
            }

            //nothing believable in the keys, so fall back to what the file says about itself
            double seconds = animation.TicksPerSecond > 0 ? animation.DurationInTicks / animation.TicksPerSecond : 0;
            return seconds > 0 ? (float)((frames - 1) / seconds) : 0;
        }

        /* What to tell someone about a file that is on a rig of its own. */
        private static string Describe(Assimp.Scene scene, Skeleton rig)
        {
            string what;
            AnimationRetarget.Looks(scene, out what);
            return what + "\r\n\r\nIt can be converted onto " + rig.Name
                + ": the angles come across and " + rig.Name + " keeps its own proportions, so nothing stretches.";
        }

        /* Does the root actually move over the clip? Comparing it against the rig.s rest instead
         * would call a file animated just for carrying a root the engine would have defaulted - a
         * root that holds still is placement, and placement is the engine.s. */
        private static bool RootMoves(List<List<HavokPackfile.SampledTransform>> poses)
        {
            HavokPackfile.SampledTransform first = poses[0][0];
            for (int frame = 1; frame < poses.Count; frame++)
            {
                HavokPackfile.SampledTransform pose = poses[frame][0];
                if ((pose.Translation - first.Translation).Length() > 0.0001f) return true;
                if ((pose.Scale - first.Scale).Length() > 0.0001f) return true;
                if (Degrees(pose.Rotation, first.Rotation) > 0.05) return true;
            }
            return false;
        }

        /// <summary>
        /// The root bone belongs to the engine, not to the rig: a clip that doesn't animate it leaves
        /// it out and the engine places the character. A character rig rests its root a long way from
        /// identity - ALIEN:EXTRACT and MALE:EXTRACT are both 180 degrees off - so writing that rest
        /// value out as if it were animation turns the character round.
        ///
        /// Decided per channel, because they can disagree: a clip whose root drifts a fraction of a
        /// millimetre still wants its rest rotation dropped, and judging the root as a whole keeps
        /// that rotation and plays the character backwards.
        /// </summary>
        public static void ApplyRoot(Reading reading, RootHandling handling)
        {
            if (reading?.Poses == null || reading.Poses.Count == 0) return;
            if (handling == RootHandling.KeepAsAuthored) return;

            bool all = handling == RootHandling.LeaveToEngine;
            HavokPackfile.SampledTransform first = reading.Poses[0][0];

            bool translation = all, rotation = all, scale = all;
            if (!all)
            {
                //a channel that holds still over the whole clip was never animated
                translation = rotation = scale = true;
                for (int frame = 1; frame < reading.Poses.Count; frame++)
                {
                    HavokPackfile.SampledTransform pose = reading.Poses[frame][0];
                    if ((pose.Translation - first.Translation).Length() > 0.0001f) translation = false;
                    if (Degrees(pose.Rotation, first.Rotation) > 0.05) rotation = false;
                    if ((pose.Scale - first.Scale).Length() > 0.0001f) scale = false;
                }
            }

            for (int frame = 0; frame < reading.Poses.Count; frame++)
            {
                HavokPackfile.SampledTransform pose = reading.Poses[frame][0];
                if (translation) pose.Translation = Vector3.Zero;
                if (rotation) pose.Rotation = Quaternion.Identity;
                if (scale) pose.Scale = Vector3.One;
                reading.Poses[frame][0] = pose;
            }
        }

        /* How the file's bone offsets compare with the rig's, which catches a file in the wrong units */
        private static double MeasureScale(List<HavokPackfile.SampledTransform> pose, Skeleton rig)
        {
            List<double> ratios = new List<double>();
            for (int i = 1; i < rig.Bones.Count && i < pose.Count; i++)
            {
                float rest = rig.Bones[i].Position.Length();
                if (rest < 0.02f) continue;                    //too short to say anything
                ratios.Add(pose[i].Translation.Length() / rest);
            }
            if (ratios.Count == 0) return 1;

            ratios.Sort();
            return ratios[ratios.Count / 2];
        }
        #endregion

        #region IMPORTING
        /// <summary>
        /// Turn a reading into a clip that can be played but is in nobody's database - what the
        /// preview shows before anything is committed.
        /// </summary>
        public static CathodeLib.Animation.ClipReference BuildPreview(CathodeLib.Animation animations,
            CathodeLib.Animation.AnimationSet set, Reading reading, string clipName, string clipPath, Options options)
        {
            if (animations == null || set == null || reading == null || !reading.Ok) return null;

            AnimClipDBSec section = animations.BuildSection(reading.Poses, TrackToBone(reading), options.Rig,
                clipPath, set.Name + "\\" + (clipName ?? "").ToUpperInvariant(), reading.FrameDuration, options.Additive);
            if (section == null) return null;

            CathodeLib.Animation.AnimationContext context = (string.IsNullOrEmpty(options.Context) ? null
                : set.Contexts.FirstOrDefault(x => x.Name.Length != 0 && string.Equals(x.Name, options.Context, StringComparison.OrdinalIgnoreCase)))
                ?? set.Contexts.FirstOrDefault(x => x.Name.Length == 0) ?? set.Contexts.FirstOrDefault();
            return new CathodeLib.Animation.ClipReference
            {
                Name = clipName,
                Path = clipPath,
                Context = context,
                Section = section,
                Index = 0,
            };
        }

        /// <summary>
        /// Add the clip to the set. Nothing is written to disk - call
        /// <see cref="CathodeLib.Animation.Save"/> when the caller is ready.
        /// </summary>
        public static bool Add(CathodeLib.Animation animations, CathodeLib.Animation.AnimationSet set,
                               Reading reading, string clipName, string clipPath, Options options, out string problem)
        {
            problem = null;
            if (animations == null || set == null) { problem = "No animation set to import into."; return false; }
            if (reading == null || !reading.Ok) { problem = reading?.Problem ?? "Nothing was read from the file."; return false; }

            clipName = (clipName ?? "").Trim();
            if (clipName.Length == 0) { problem = "The clip needs a name."; return false; }
            if (set.Contexts.SelectMany(x => x.Clips).Any(x => string.Equals(x.Name, clipName, StringComparison.OrdinalIgnoreCase)))
            {
                problem = "'" + clipName + "' is already the name of an animation in " + set.Name + ".";
                return false;
            }
            if (animations.GetSection(clipPath, out int _) != null)
            {
                problem = "Something is already stored at '" + clipPath + "'.";
                return false;
            }

            /* AddClip decides carriage itself - it is not a choice, see Animation.SetCarriage. Whatever the encoder
             * throws (it refuses a section template it can't write for) comes back as the reason rather than as an
             * exception, so the import window and the MCP tool both say why instead of falling over. */
            if (!string.IsNullOrEmpty(options.Context) && !set.Contexts.Any(x => x.Name.Length != 0 && string.Equals(x.Name, options.Context, StringComparison.OrdinalIgnoreCase)))
            {
                problem = set.Name + " has no context '" + options.Context + "'.";
                return false;
            }

            bool added;
            try
            {
                added = animations.AddClip(set, clipName, clipPath, options.Rig, TrackToBone(reading), reading.Poses,
                                           reading.FrameDuration, options.Additive, options.Context);
            }
            catch (Exception ex)
            {
                problem = "The clip could not be added to the animation database: " + ex.Message;
                return false;
            }
            if (!added)
            {
                problem = "The clip could not be added to the animation database.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Rebuild an existing clip from a reading, in place: its name, path and every reference to it stay
        /// (see <see cref="CathodeLib.Animation.ReplaceClip"/>). Nothing is written to disk.
        /// </summary>
        public static bool Replace(CathodeLib.Animation animations, CathodeLib.Animation.ClipReference clip,
                                   Reading reading, Options options, out string problem)
        {
            problem = null;
            if (animations == null || clip == null) { problem = "There is no clip to replace."; return false; }
            if (reading == null || !reading.Ok) { problem = reading?.Problem ?? "Nothing was read from the file."; return false; }
            try
            {
                return animations.ReplaceClip(clip, options.Rig, TrackToBone(reading), reading.Poses, reading.FrameDuration, options.Additive, out problem);
            }
            catch (Exception ex)
            {
                problem = "The clip could not be rebuilt: " + ex.Message;
                return false;
            }
        }

        /* Track i drives bone i. A third of a rig's clips ship with some other permutation and the
         * mapping is written into the binding either way, but 697 of 4,000 shipped clips use this
         * one, so it is not a shape the runtime has to be talked into. */
        private static List<short> TrackToBone(Reading reading)
        {
            List<short> map = new List<short>(reading.Poses[0].Count);
            for (int i = 0; i < reading.Poses[0].Count; i++) map.Add((short)i);
            return map;
        }
        #endregion

        #region HELPERS
        private static List<HavokPackfile.SampledTransform> RestPose(Skeleton rig)
        {
            List<HavokPackfile.SampledTransform> pose = new List<HavokPackfile.SampledTransform>(rig.Bones.Count);
            for (int i = 0; i < rig.Bones.Count; i++)
                pose.Add(new HavokPackfile.SampledTransform
                {
                    Translation = rig.Bones[i].Position,
                    Rotation = rig.Bones[i].Rotation,
                    Scale = rig.Bones[i].ScaleXYZ,
                });
            return pose;
        }

        /* Our own exports lead with the bone index, which is exact. Anything else is matched on the
         * bone's name, with or without the rig prefix the game puts in front of it. */
        private static int BoneFor(string node, Skeleton rig, Dictionary<string, int> map, out bool exact)
        {
            //Exact: the index our export leads with, the caller's own map, or the rig's full bone name - not a bare joint name in another case
            exact = true;
            if (string.IsNullOrEmpty(node)) return -1;

            //the caller knows the answer where a rig was renamed on its way in
            if (map != null && map.TryGetValue(node, out int known))
                return known >= 0 && known < rig.Bones.Count ? known : -1;

            const string Prefix = "CS2_BONE_";
            int at = node.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
            {
                int digits = at + Prefix.Length, end = digits;
                while (end < node.Length && char.IsDigit(node[end])) end++;
                if (end > digits && int.TryParse(node.Substring(digits, end - digits), out int index)
                    && index >= 0 && index < rig.Bones.Count)
                    return index;
            }

            for (int i = 0; i < rig.Bones.Count; i++)
            {
                string name = rig.Bones[i].Name ?? "";
                if (string.Equals(name, node, StringComparison.OrdinalIgnoreCase)) return i;
                if (string.Equals(ModelIO.Sanitise(name), node, StringComparison.OrdinalIgnoreCase)) return i;
            }
            exact = false;
            for (int i = 0; i < rig.Bones.Count; i++)
            {
                string name = rig.Bones[i].Name ?? "";
                int colon = name.IndexOf(':');
                if (colon >= 0 && string.Equals(name.Substring(colon + 1), node, StringComparison.OrdinalIgnoreCase))
                {
                    /* The rig's own joint name in its own case (LEFTUPLEG: the game's are upper case) is this rig with its
                     * namespace stripped by whatever exported it. Another rig sharing the joint names writes them its own
                     * way (Mixamo's LeftUpLeg), and only that is left to decide whether to convert it. */
                    exact = string.Equals(name.Substring(colon + 1), node, StringComparison.Ordinal);
                    return i;
                }
            }
            return -1;
        }

        /* How many frames a clip runs to, when the first one is and how far apart they are, in the
         * file's own time units.
         *
         * Keys are found by time, not by counting them. Our own exports key every bone on every
         * frame, but a DCC tool writing a clip back out keeps a key only where a curve needs one -
         * Blender's FBX exporter thins each curve on its own by default - and counting keys then
         * reads each bone at the wrong moment: a walk brought back through Blender came in 1.26 m
         * and 103 degrees out. The frames are as far apart as the closest two keys anywhere in the
         * clip, which on a file of ours is every key. */
        private static int Frames(Assimp.Animation animation, out double first, out double step)
        {
            first = 0;
            step = 1;
            List<double> times = new List<double>();
            foreach (Assimp.NodeAnimationChannel channel in animation.NodeAnimationChannels)
            {
                foreach (Assimp.VectorKey key in channel.PositionKeys) times.Add(key.Time);
                foreach (Assimp.QuaternionKey key in channel.RotationKeys) times.Add(key.Time);
                foreach (Assimp.VectorKey key in channel.ScalingKeys) times.Add(key.Time);
            }
            if (times.Count == 0) return 0;

            times.Sort();
            first = times[0];
            double span = times[times.Count - 1] - first;
            if (span <= 0) return 1;

            //two keys closer than this are the same moment, written twice
            double same = span * 1e-6;
            double closest = double.MaxValue;
            for (int i = 1; i < times.Count; i++)
            {
                double gap = times[i] - times[i - 1];
                if (gap > same && gap < closest) closest = gap;
            }

            /* Keys that don't sit on a grid at all would ask for an absurd number of frames, so
             * those fall back to the most keys any one curve has, spread evenly over the clip */
            int frames = (int)Math.Round(span / closest) + 1;
            if (frames > 100000 || Math.Abs((span / closest) - Math.Round(span / closest)) > 0.01)
                frames = Math.Max(2, animation.NodeAnimationChannels.Max(x => Math.Max(x.PositionKeyCount, Math.Max(x.RotationKeyCount, x.ScalingKeyCount))));
            step = span / (frames - 1);
            return frames;
        }

        /* A curve's value at a moment: the key there, or between the two either side of it */
        private static bool Key(List<Assimp.VectorKey> keys, double when, double step, out Vector3 value)
        {
            value = Vector3.Zero;
            if (keys == null || keys.Count == 0) return false;

            int after = KeyAfter(keys.Count, x => keys[x].Time, when, step, out bool exact);
            Assimp.Vector3D b = keys[after].Value;
            value = new Vector3(b.X, b.Y, b.Z);
            if (exact || after == 0) return true;

            Assimp.Vector3D a = keys[after - 1].Value;
            double span = keys[after].Time - keys[after - 1].Time;
            value = Vector3.Lerp(new Vector3(a.X, a.Y, a.Z), value, span <= 0 ? 1 : (float)((when - keys[after - 1].Time) / span));
            return true;
        }

        private static bool Key(List<Assimp.QuaternionKey> keys, double when, double step, out Quaternion value)
        {
            value = Quaternion.Identity;
            if (keys == null || keys.Count == 0) return false;

            int after = KeyAfter(keys.Count, x => keys[x].Time, when, step, out bool exact);
            Assimp.Quaternion b = keys[after].Value;
            value = new Quaternion(b.X, b.Y, b.Z, b.W);
            if (exact || after == 0) return true;

            Assimp.Quaternion a = keys[after - 1].Value;
            double span = keys[after].Time - keys[after - 1].Time;
            value = Quaternion.Slerp(new Quaternion(a.X, a.Y, a.Z, a.W), value, span <= 0 ? 1 : (float)((when - keys[after - 1].Time) / span));
            return true;
        }

        /* The first key at or after a moment, and whether it is on that moment - within a hundredth
         * of a frame, so a key written at a rounded time still counts. Before the first key or past
         * the last, a curve holds its end value. */
        private static int KeyAfter(int count, Func<int, double> time, double when, double step, out bool exact)
        {
            double tolerance = Math.Abs(step) * 0.01;
            exact = true;
            if (time(count - 1) <= when + tolerance) return count - 1;
            if (time(0) >= when - tolerance) return 0;

            int low = 0, high = count - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (time(middle) < when - tolerance) low = middle; else high = middle;
            }
            exact = time(high) <= when + tolerance;
            return high;
        }

        /* Normalise first - the rig's quaternions are float32 and not quite unit, so comparing one
         * against itself raw comes out at 0.056 degrees rather than zero. */
        private static double Degrees(Quaternion a, Quaternion b)
        {
            if (a.LengthSquared() > 1e-12f) a = Quaternion.Normalize(a);
            if (b.LengthSquared() > 1e-12f) b = Quaternion.Normalize(b);
            return Math.Acos(Math.Min(1.0, Math.Abs(Quaternion.Dot(a, b)))) * 2.0 * 180.0 / Math.PI;
        }
        #endregion
    }
}
