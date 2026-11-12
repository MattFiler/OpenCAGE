using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.ClipTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Anim = CathodeLib.Animation;
using Transform = CATHODE.HavokPackfile.SampledTransform;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Clips that travel at a marker and the clip splicer: sync_clip_to_animation, make_clip_travel, splice_clips.
    /// The work is in Scripts\ClipTools (ClipFrames, TravelAtMarker, ClipSplice, ClipSync), which the Animation Editor's Clip Splicer and
    /// the flowgraph's "Sync to a CAGEAnimation..." share.
    /// </summary>
    internal static class McpClipTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "sync_clip_to_animation",
                Title = "Sync a clip to a CAGEAnimation",
                Toolset = "animation",
                Description = "Lock a CMD_PlayAnimation's clip to a CAGEAnimation's clock, so a cutscene character never drifts from its camera and props: links the command's ExternalTime to the CAGEAnimation " +
                    "(clip time = the CAGEAnimation's time every frame; without it the clip runs its own clock, which the game advances at most 1/24 s a frame, so on slow or hitching frames the character falls behind). " +
                    "offset starts the clip that many seconds into the CAGEAnimation (ExternalStartTime <- a VariableFloat <command>_SyncStart). unsync takes the links off. One undo step. " +
                    "The command must still be started (its TriggerBindCharacter) when the CAGEAnimation starts, and only fires 'finished' once the CAGEAnimation's time passes the clip's end. " +
                    "Additive clips (has_motion false) are layers: they belong on CHR_PlaySecondaryAnimation, which takes no clock.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding both (path or id; default the root).", required: false),
                    McpSchema.String("command", "The CMD_PlayAnimation (id or name).", required: true),
                    McpSchema.String("animation", "The CAGEAnimation whose time it follows (id or name; same composite). Not needed with unsync."),
                    McpSchema.Number("offset", "Seconds into the CAGEAnimation at which the clip's frame 0 plays (default 0)."),
                    McpSchema.Boolean("unsync", "Take the command's ExternalTime / ExternalStartTime links off."),
                    McpSchema.Boolean("dry_run", "Check and say what would change.")),
                Run = SyncClip,
            };

            yield return new McpTool()
            {
                Name = "make_clip_travel",
                Title = "Make a clip travel at a marker",
                Toolset = "animation",
                Description = "Rebuild a character clip so it TRAVELS the way the game's own travelling clips do (a vault, a cover entry, a cutscene walk): the root (EXTRACT) left to the engine, a capsule frame following the body " +
                    "(over the hips, at the feet's height, turned the way the body faces) carried inverted on REFERENCE_ROOT, the body stored relative to it. Where the body is drawn doesn't change, but the character's capsule - " +
                    "what animation LOD, culling and the AI measure from - rides along under it. Without this a clip whose body travels while REFERENCE_ROOT stays put leaves the capsule at the marker (LOD drops to 10 updates a second " +
                    "as the body walks away: 'jitter'; the character vanishes when the marker is out of shot), and travel on the root or on a REFERENCE_ROOT that moves with the body cancels out, leaving the body at the marker. " +
                    "import_animation root 'marker' does the same on the way in. Writes a new clip (name) or rebuilds this one (replace). ANIMATION.PAK is shared by every level and written at once (not undoable); dry_run reports first.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The animation set (list_animation_sets).", required: true),
                    McpSchema.String("clip", "The clip to rebuild (list_animations).", required: true),
                    McpSchema.String("name", "The new clip's name (default <clip>_travel). Ignored with replace."),
                    McpSchema.String("stored_path", "Where the new clip is stored (default ANIMATION\\OPENCAGE\\<SET>\\<NAME>)."),
                    McpSchema.Boolean("replace", "Rebuild the clip itself in place (its name, path and every user stay)."),
                    McpSchema.Boolean("from_first_frame", "Start on the marker facing along it (default true); false keeps the clip's own origin as the marker (clips made together in one scene)."),
                    McpSchema.Boolean("follow_turns", "The capsule turns with the body (default true)."),
                    McpSchema.Boolean("follow_height", "The capsule rises and falls with the feet (default true: jumps, falls, stairs)."),
                    McpSchema.Boolean("root_is_travel", "Count motion on the root (EXTRACT) as the body's travel (default true: what an imported file means; the game itself cancels it)."),
                    McpSchema.Boolean("dry_run", "Work it out and report, writing nothing."),
                    McpSchema.Boolean("close_game", "Close a running game first (it holds ANIMATION.PAK open).")),
                Destructive = true,
                Run = MakeClipTravel,
            };

            yield return new McpTool()
            {
                Name = "splice_clips",
                Title = "Splice clips into a new clip",
                Toolset = "animation",
                Description = "Assemble a new character clip from pieces of existing ones (sit, stand, step, grab, leap, land...) without a 3D package: each segment is a frame range of a clip, played at a speed (retime), " +
                    "optionally held on its last frame; each one carries on from where the last got to (root-motion continuity, on the clips' own capsules - shipped clips that follow one another, like leap_in_2_5m -> leap_2_5m -> leap_2_5m_out, meet exactly) " +
                    "and is crossfaded over 'blend' seconds (the END of the previous overlapping the START of this one; 0 = a hard cut, right for chains made to follow one another). " +
                    "turn / offset nudge a join; carry adds travel over a segment (shipped leaps jump on the spot - the traversal code moves them - so a leap over a gap says how far). " +
                    "The result is in the game's travelling shape (make_clip_travel), so it carries the character on whether or not it plays at a marker. All segments must be authored on one rig (list_animations shows each clip's). " +
                    "The result reports each join's pose pop and the worst frame-to-frame step after splicing. ANIMATION.PAK is shared by every level and written at once (not undoable); dry_run reports first. " +
                    "lock_feet (default true): a foot planted on both sides of a join is put down on the same spot and held through the crossfade by two-bone IK until it lifts, so a blend between two stances does not slide it (the result reports each join's foot drift). Hand locks are not done.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The set the new clip goes into, and the default set of the segments' clips.", required: true),
                    McpSchema.String("name", "The new clip's name.", required: true),
                    McpSchema.Array("segments", "In order: [{clip, set?, from? (frame, 0), to? (frame, inclusive; default the last), speed? (1), blend? (s, crossfade from the previous; default 0.2), hold? (s on its last frame), " +
                        "continue? (true: carry on from the previous; false: keep its own placement), turn? (degrees at the join), offset? [x,y,z] (m at the join, in the previous capsule's frame), carry? [x,y,z] (m of extra travel over the segment, +Z forward)}].", new JObject() { ["type"] = "object" }, required: true),
                    McpSchema.String("stored_path", "Where it is stored (default ANIMATION\\OPENCAGE\\<SET>\\<NAME>)."),
                    McpSchema.String("context", "File it as a line of this context of the set instead of its own lines (a command plays only own lines)."),
                    McpSchema.Boolean("travel", "Keep the travel (default true); false plays it all on the spot."),
                    McpSchema.Boolean("from_first_frame", "Start on the marker / where the character stands (default true); false keeps the first segment's own placement."),
                    McpSchema.Number("frame_rate", "Frames per second (default the first segment's)."),
                    McpSchema.Boolean("lock_feet", "Hold planted feet through the crossfades (default true)."),
                    McpSchema.Boolean("dry_run", "Build and report, writing nothing."),
                    McpSchema.Boolean("close_game", "Close a running game first (it holds ANIMATION.PAK open).")),
                Destructive = true,
                Run = SpliceClips,
            };
        }

        #region sync_clip_to_animation
        private static object SyncClip(McpCall call)
        {
            bool unsync = call.Bool("unsync"), dryRun = call.Bool("dry_run");
            float offset = (float)call.Num("offset", 0);
            if (offset < 0 || offset > 36000) throw McpError.Invalid("'offset' is seconds into the CAGEAnimation (0 or more).");
            if (!unsync && !call.Has("animation")) throw McpError.Invalid("'animation' names the CAGEAnimation to sync to (or give unsync).");
            Composite composite = null;
            Entity command = null, cage = null;
            JObject result = new JObject();
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: !dryRun);
                composite = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : commands.EntryPoints[0];
                command = McpScript.FindEntity(commands, composite, call.Str("command", required: true));
                if (!unsync) cage = McpScript.FindEntity(commands, composite, call.Str("animation"));
                string problem = unsync ? (ClipSync.IsPlayAnimation(command) ? null : McpScript.EntityName(commands, composite, command) + " is not a CMD_PlayAnimation.") : ClipSync.Problem(commands, composite, command, cage);
                if (problem != null) throw McpError.Invalid(problem);
                Entity was = ClipSync.LinkedTo(composite, command, ClipSync.ExternalTime);
                result["command"] = McpScript.EntityName(commands, composite, command);
                result["composite"] = composite.name;
                result["was_synced_to"] = was == null ? null : McpScript.EntityName(commands, composite, was);
                if (!unsync)
                {
                    result["animation"] = McpScript.EntityName(commands, composite, cage);
                    result["offset"] = offset;
                    Anim.ClipReference clip = ClipSync.ClipOf(command);
                    if (clip != null) result["clip"] = new JObject() { ["name"] = clip.Name, ["duration"] = Math.Round(clip.Duration, 3), ["additive"] = clip.Additive };
                    result["notes"] = new JArray(ClipSync.Notes(commands, composite, command, cage, offset));
                }
            });
            McpScriptEdit.Outcome outcome = ClipSync.Apply(composite, command, cage, offset, unsync, false, dryRun);
            result["changed"] = outcome.Changed || outcome.DryRun;
            if (dryRun) { result["dry_run"] = true; result["next"] = "Call again without dry_run to make it, as one undo step."; }
            else result["next"] = unsync ? "The clip runs its own clock again." : "In game, Debug Menu scripts.list_animations (the CAGEAnimation's time) and anim.report <character> (the clip's) read the same.";
            foreach (string note in outcome.Notes) call.Note(note);
            return result;
        }
        #endregion

        #region make_clip_travel
        private static object MakeClipTravel(McpCall call)
        {
            bool dryRun = call.Bool("dry_run"), replace = call.Bool("replace");
            Anim animations = null;
            Anim.AnimationSet set = null;
            Anim.ClipReference clip = null;
            Skeleton rig = null;
            string rigName = null, name = null, path = null;
            McpEditor.UI(() =>
            {
                animations = RequireAnimations();
                set = FindSet(call.Str("set", required: true));
                clip = FindClip(set, call.Str("clip", required: true));
                if (clip.Additive) throw McpError.Invalid("'" + clip.Name + "' is additive: it lays changes over whatever the character is doing and has no travel of its own (additive clips play on CHR_PlaySecondaryAnimation).");
                rigName = clip.Skeleton;
                rig = animations.GetSkeleton(rigName)?.Skeleton;
                if (rig == null) throw new McpError("The clip's rig '" + rigName + "' is not in ANIMATION.PAK.");
                name = replace ? clip.Name : (call.Str("name") ?? clip.Name + "_travel").Trim();
                path = replace ? clip.Path : (call.Str("stored_path") ?? AnimationImport.PathFor(set, name)).Trim().Replace('/', '\\');
                if (!replace) CheckFree(animations, set, name, path);
            });

            ClipFrames frames = ClipFrames.FromClip(clip, rig);
            TravelAtMarker.Result travel = TravelAtMarker.Convert(frames.ToPoses(), rig, frames.FrameDuration, new TravelAtMarker.Options()
            {
                FromFirstFrame = call.Bool("from_first_frame", true),
                FollowTurns = call.Bool("follow_turns", true),
                FollowHeight = call.Bool("follow_height", true),
                RootIsTravel = call.Bool("root_is_travel", true),
            });
            if (!travel.Ok) throw new McpError(travel.Problem);

            JObject result = new JObject()
            {
                ["set"] = set.Name,
                ["clip"] = clip.Name,
                ["rig"] = rigName,
                ["frames"] = frames.Count,
                ["duration"] = Math.Round(frames.Duration, 3),
                ["travel"] = TravelJson(travel),
            };
            if (replace) result["replaces"] = clip.Name; else { result["name"] = name; result["stored_path"] = path; }
            if (travel.Notes.Count != 0) result["notes"] = new JArray(travel.Notes);
            if (dryRun) { result["dry_run"] = true; result["next"] = "Call again without dry_run to write it."; return result; }

            Write(call, animations, set, rigName, travel.Poses, frames.FrameDuration, name, path, null, replace ? clip : null);
            result["plays"] = "CMD_PlayAnimation AnimationSet " + set.Name + " Animation " + name + " - at a marker (play_character_animation 'at') it starts on the marker; the capsule follows the body.";
            return result;
        }
        #endregion

        #region splice_clips
        private static object SpliceClips(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            JArray given = call.Array("segments", required: true);
            if (given.Count == 0) throw McpError.Invalid("'segments' is empty.");
            double rate = call.Num("frame_rate", 0);
            if (rate < 0 || rate > 120) throw McpError.Invalid("'frame_rate' is 1 - 120 (or 0 for the first segment's).");
            Anim animations = null;
            Anim.AnimationSet set = null;
            string name = null, path = null, context = call.Str("context");
            if (context != null && context.Trim().Length == 0) context = null;
            List<ClipSplice.Segment> segments = new List<ClipSplice.Segment>();
            McpEditor.UI(() =>
            {
                animations = RequireAnimations();
                set = FindSet(call.Str("set", required: true));
                name = call.Str("name", required: true).Trim();
                path = (call.Str("stored_path") ?? AnimationImport.PathFor(set, name)).Trim().Replace('/', '\\');
                if (context != null && !set.Contexts.Any(o => string.Equals(o.Name, context.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw McpError.Invalid(set.Name + " has no context '" + context + "' (list_animation_contexts).");
                CheckFree(animations, set, name, path);
                for (int i = 0; i < given.Count; i++)
                {
                    if (!(given[i] is JObject s)) throw McpError.Invalid("'segments[" + i + "]' is an object {clip, ...}.");
                    string what = "segments[" + i + "]";
                    foreach (JProperty p in s.Properties())
                        if (!new[] { "clip", "set", "from", "to", "speed", "blend", "hold", "continue", "turn", "offset", "carry" }.Contains(p.Name))
                            throw McpError.Invalid(what + " has an unknown key '" + p.Name + "'.");
                    Anim.AnimationSet from = s["set"] != null ? FindSet((string)s["set"]) : set;
                    segments.Add(new ClipSplice.Segment()
                    {
                        Clip = FindClip(from, (string)s["clip"] ?? throw McpError.Invalid(what + " needs 'clip'.")),
                        From = s["from"] != null ? (int)s["from"] : 0,
                        To = s["to"] != null ? (int)s["to"] : -1,
                        Speed = s["speed"] != null ? (float)s["speed"] : 1f,
                        Blend = s["blend"] != null ? (float)s["blend"] : 0.2f,
                        Hold = s["hold"] != null ? (float)s["hold"] : 0f,
                        Continue = s["continue"] == null || (bool)s["continue"],
                        Turn = s["turn"] != null ? (float)s["turn"] : 0f,
                        Offset = s["offset"] != null ? McpValues.ReadVector(s["offset"], what + ".offset", null) : Vector3.Zero,
                        Carry = s["carry"] != null ? McpValues.ReadVector(s["carry"], what + ".carry", null) : Vector3.Zero,
                    });
                    ClipSplice.Segment made = segments[segments.Count - 1];
                    int frames = made.Clip.Animation?.FrameCount ?? 0;
                    if (made.From < 0 || made.From >= frames || (made.To >= 0 && (made.To < made.From || made.To >= frames)))
                        throw McpError.Invalid(what + ": '" + made.Clip.Name + "' has frames 0 - " + (frames - 1) + ", so it can't play " + made.From + " - " + (made.To < 0 ? "the end" : made.To.ToString()) + ".");
                }
            });

            ClipSplice.Result spliced;
            using (McpEditorTools.Heartbeat(call, "Splicing " + segments.Count + " segments"))
                spliced = ClipSplice.Build(animations, segments, new ClipSplice.Options()
                {
                    Travel = call.Bool("travel", true),
                    FromFirstFrame = call.Bool("from_first_frame", true),
                    FrameRate = (float)rate,
                    LockFeet = call.Bool("lock_feet", true),
                });
            if (!spliced.Ok) throw McpError.Invalid(spliced.Problem);

            Matrix4x4 end = spliced.Capsule[spliced.Capsule.Count - 1];
            JObject result = new JObject()
            {
                ["set"] = set.Name,
                ["name"] = name,
                ["stored_path"] = path,
                ["rig"] = spliced.RigName,
                ["frames"] = spliced.Frames,
                ["duration"] = Math.Round(spliced.Duration, 3),
                ["fps"] = Math.Round(1.0 / spliced.FrameDuration, 2),
                ["segments"] = new JArray(spliced.Pieces.Select(p => new JObject()
                {
                    ["clip"] = p.Segment.Clip.Name,
                    ["frames"] = new JArray(p.FirstFrame, p.LastFrame),
                    ["seconds"] = new JArray(Math.Round(p.Start, 3), Math.Round(p.End, 3)),
                    ["blend_frames"] = p.BlendFrames,
                    ["capsule_travel_m"] = Math.Round(p.Travel, 3),
                    ["join_pop_cm"] = Math.Round(p.Pop * 100, 1),
                    ["join_worst_step_cm"] = Math.Round(p.JoinStep * 100, 1),
                    ["join_foot_drift_cm"] = Math.Round(p.FootDrift * 100, 1),
                })),
                ["ends"] = new JObject() { ["position"] = McpValues.Vector(end.Translation), ["yaw"] = Math.Round(ClipFrames.YawOf(end), 1), ["note"] = "relative to where it starts (the marker), +Z forward" },
                ["smoothness"] = new JObject() { ["worst_join_step_cm"] = Math.Round(spliced.WorstJoinStep * 100, 1), ["worst_step_elsewhere_cm"] = Math.Round(spliced.WorstOwnStep * 100, 1), ["worst_pop_without_blending_cm"] = Math.Round(spliced.WorstNaiveStep * 100, 1), ["worst_planted_foot_drift_cm"] = Math.Round(spliced.WorstFootDrift * 100, 1), ["foot_locks"] = spliced.LockedFeet },
            };
            if (spliced.Notes.Count != 0) result["notes"] = new JArray(spliced.Notes);
            if (dryRun) { result["dry_run"] = true; result["next"] = "Call again without dry_run to write it."; return result; }

            Write(call, animations, set, spliced.RigName, spliced.Poses, spliced.FrameDuration, name, path, context, null);
            result["plays"] = context != null
                ? "A line of " + set.Name + "'s " + context + " context: the animation trees play it in that context's slot of the same name (a command can't)."
                : "CMD_PlayAnimation AnimationSet " + set.Name + " Animation " + name + " (play_character_animation).";
            return result;
        }
        #endregion

        #region helpers
        private static JObject TravelJson(TravelAtMarker.Result travel)
        {
            return new JObject()
            {
                ["capsule_path_m"] = Math.Round(travel.PathLength, 3),
                ["ends"] = McpValues.Vector(travel.End),
                ["turn_deg"] = Math.Round(travel.Turn, 1),
                ["height_range_m"] = new JArray(Math.Round(travel.Lowest, 3), Math.Round(travel.Highest, 3)),
                ["body_from_capsule_m"] = Math.Round(travel.BodyFromCapsuleAfter, 3),
                ["body_from_marker_if_parked_m"] = Math.Round(travel.BodyFromCapsuleBefore, 3),
                ["summary"] = travel.Describe(),
            };
        }

        private static Anim RequireAnimations()
        {
            Anim animations = Singleton.Animations;
            if (animations == null || !animations.Loaded)
                throw new McpError("The game's ANIMATION.PAK is not loaded (it is missing or unreadable in this install).");
            return animations;
        }

        private static Anim.AnimationSet FindSet(string name)
        {
            Anim.AnimationSet set = McpGlobalAssetChecks.FindSet(name);
            if (set == null) throw McpError.NotFound("animation set", name ?? "", Singleton.Animations.Sets.Select(o => o.Name), "list_animation_sets lists them.");
            return set;
        }

        private static Anim.ClipReference FindClip(Anim.AnimationSet set, string name)
        {
            Anim.ClipReference clip = McpGlobalAssetChecks.FindClip(set, name);
            if (clip == null || !clip.Playable)
                throw McpError.NotFound("clip in " + set.Name, name ?? "", set.Contexts.SelectMany(o => o.Clips).Select(o => o.Name), "list_animations set=" + set.Name + " lists its clips.");
            return clip;
        }

        private static void CheckFree(Anim animations, Anim.AnimationSet set, string name, string path)
        {
            if (name.Length == 0) throw McpError.Invalid("'name' can't be empty.");
            if (set.Contexts.SelectMany(o => o.Clips).Any(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw McpError.Invalid("'" + name + "' is already the name of an animation in " + set.Name + ": pick another name.");
            if (animations.GetSection(path, out int _) != null)
                throw McpError.Invalid("Something is already stored at '" + path + "': pass another stored_path.");
        }

        /// <summary>Add (or rebuild) the clip and write ANIMATION.PAK, as import_animation does.</summary>
        private static void Write(McpCall call, Anim animations, Anim.AnimationSet set, string rigName, List<List<Transform>> poses, float frameDuration,
                                  string name, string path, string context, Anim.ClipReference replacing)
        {
            McpAnimationAssetTools.CloseOrRefuse(call, "ANIMATION.PAK can't be written while it holds it open");
            McpAnimationAssetTools.RefuseIfAnimationPakChanged();
            using (McpEditorTools.Heartbeat(call, "Writing ANIMATION.PAK"))
                McpEditor.UI(() =>
                {
                    string stale = AnimationPakWrite.Stale(animations);
                    if (stale != null) throw new McpError(McpErrorCodes.Refused, stale + " Nothing was written.");
                    rigName = animations.SkeletonIndex?.GetSkeleton(rigName)?.Name ?? rigName;
                    List<short> tracks = Enumerable.Range(0, poses[0].Count).Select(i => (short)i).ToList();
                    if (replacing != null)
                    {
                        if (!animations.ReplaceClip(replacing, rigName, tracks, poses, frameDuration, false, out string problem))
                            throw new McpError("The clip could not be rebuilt: " + problem);
                    }
                    else
                    {
                        bool added;
                        try { added = animations.AddClip(set, name, path, rigName, tracks, poses, frameDuration, false, context); }
                        catch (Exception e) { throw new McpError("The clip could not be added: " + e.Message); }
                        if (!added) throw new McpError("The clip could not be added to the animation database.");
                        if (context == null) Singleton.RegisterAnimation(set.Name, name);
                    }
                    Singleton.OnAnimationsModified?.Invoke();
                    McpAnimationAssetTools.WriteAnimationPak(call, animations, "'" + name + "'" + (replacing != null ? " rebuilt in " : " added to ") + set.Name);
                });
            call.Note("ANIMATION.PAK has been written.");
        }
        #endregion
    }
}
