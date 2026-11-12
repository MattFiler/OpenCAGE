using CATHODE;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Cinematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Anim = CathodeLib.Animation;

namespace OpenCAGE.MCP
{
    /// <summary>bake_pose_model: a character frozen at one frame of a clip, as a static model in the level.</summary>
    internal static partial class McpCinematicTools
    {
        private static IEnumerable<McpTool> PoseTools()
        {
            yield return new McpTool()
            {
                Name = "bake_pose_model",
                Title = "Bake pose into a model",
                Description = "Freeze a skinned character model at one frame of a clip and bring it in as a STATIC model (CA_ENVIRONMENT materials made from the character's own textures), optionally placed: a body that rides a vehicle with no lag (an animated character trails a keyed vehicle by ~2 frames; key the posed model with the vehicle's keys - ride_along), a statue, a corpse in a pose, a set-dressing extra. " +
                    "space 'marker' (default) is where the game draws a character playing that clip at a marker, so the model placed at the same marker matches the character's frame exactly; 'preview' is what the Animation Preview shows. Writes the posed mesh as FBX + DDS textures (kept with keep_file) and runs import_model on it. preview: report only. " +
                    "Hit-collision parts (COL_) and lower LODs are left out; unskinned parts cannot be posed and are listed.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("model", "The skinned model to pose (its name in the level, as list_models gives it).", required: true),
                    McpSchema.String("set", "The animation set the clip is in (e.g. MALE, ALIEN).", required: true),
                    McpSchema.String("animation", "The clip.", required: true),
                    McpSchema.String("context", "The clip's context in the set, when its name is in more than one."),
                    McpSchema.String("rig", "The rig to pose on (default: the rig the model is skinned to)."),
                    McpSchema.Integer("frame", "The frame (0-based; default 0)."),
                    McpSchema.Number("time", "Or a time in seconds into the clip (the nearest frame)."),
                    McpSchema.String("space", "marker (default) or preview.", options: new[] { "marker", "preview" }),
                    McpSchema.String("root_motion", "space preview: hold (default: on the spot) or follow (carried where the clip takes it).", options: new[] { "hold", "follow" }),
                    McpSchema.Integer("lod", "The level of detail to bake (default 0, the finest)."),
                    McpSchema.Strings("exclude", "Parts to leave out (Cc_Ss tags or material names; preview lists them)."),
                    McpSchema.String("name", "The new model's name in the level (default POSED\\<model>_<clip>_F<frame>)."),
                    McpSchema.String("composite_name", "The composite that places it (default beside the model's name)."),
                    McpSchema.String("place_in", "Also place an instance of it in this composite ('root' for the level)."),
                    McpSchema.Position("position", "With place_in: where (default the origin)."),
                    McpSchema.Rotation("rotation", "With place_in: its rotation."),
                    McpSchema.String("place_space", "With place_in: composite (default) or world.", options: new[] { "composite", "world" }),
                    McpSchema.String("instance_name", "With place_in: the instance's name."),
                    McpSchema.String("keep_file", "Also keep the posed FBX (and its textures folder) at this full path."),
                    McpSchema.Boolean("preview", "Bake and report (parts, size, fit) without importing anything.")),
                Destructive = false,
                Toolset = "animation",
                Run = BakePoseModel,
            };
        }

        private static object BakePoseModel(McpCall call)
        {
            string space = (call.Str("space") ?? "marker").Trim().ToLowerInvariant();
            if (space != "marker" && space != "preview") throw McpError.Invalid("'space' is marker or preview.");
            string rootMotion = (call.Str("root_motion") ?? "hold").Trim().ToLowerInvariant();
            if (rootMotion != "hold" && rootMotion != "follow") throw McpError.Invalid("'root_motion' is hold or follow.");
            bool preview = call.Bool("preview");
            string keep = call.Str("keep_file");
            if (keep != null && !Path.IsPathRooted(keep)) throw McpError.Invalid("'keep_file' is a full path to a .fbx file.");
            if (keep != null && !keep.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) throw McpError.Invalid("'keep_file' is a .fbx file.");

            string folder = Path.Combine(Path.GetTempPath(), "OpenCAGE", "PoseBake", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            JObject report = null;
            string file = null, modelName = null, clipName = null;
            List<string> materials = null;
            int frame = 0;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(!preview);
                Anim animations = Singleton.Animations;
                if (animations == null || !animations.Loaded) throw new McpError("The game's ANIMATION.PAK is not loaded.");
                Models.CS2 model = McpAssets.FindModel(content.Level, call.Str("model", required: true));
                if (Skeleton.RequiredBoneCount(model) == 0) throw McpError.Invalid(model.Name + " is not skinned to a rig, so a clip cannot pose it.");
                Anim.AnimationSet set = animations.GetSet(call.Str("set", required: true).Trim());
                if (set == null) throw McpError.Invalid("There is no animation set '" + call.Str("set") + "' (list_animation_sets lists them).");
                string wanted = call.Str("animation", required: true).Trim();
                string context = call.Str("context");
                List<Anim.ClipReference> clips = set.Contexts.Where(o => context == null || string.Equals(o.Name ?? "", context, StringComparison.OrdinalIgnoreCase)).SelectMany(o => o.Clips).ToList();
                Anim.ClipReference clip = clips.FirstOrDefault(o => string.Equals(o.Name, wanted, StringComparison.OrdinalIgnoreCase))
                    ?? clips.FirstOrDefault(o => string.Equals(o.Path, wanted, StringComparison.OrdinalIgnoreCase));
                if (clip?.Animation == null) throw McpError.Invalid(set.Name + " has no animation '" + wanted + "'" + (context != null ? " in context " + context : "") + " (list_animations lists them).");
                Skeleton rig;
                if (call.Has("rig"))
                {
                    rig = animations.GetSkeleton(call.Str("rig").Trim())?.Skeleton;
                    if (rig == null || !rig.Loaded) throw McpError.Invalid("There is no rig '" + call.Str("rig") + "' (list_skeletons lists them).");
                }
                else
                {
                    rig = animations.RigFor(model, clip.Animation.SkeletonName, set.Skeleton);
                    if (rig == null) throw McpError.Invalid("No rig in ANIMATION.PAK fits " + model.Name + ": give 'rig'.");
                }
                Retargeter retarget = string.IsNullOrEmpty(clip.Animation.SkeletonName) || string.Equals(clip.Animation.SkeletonName, rig.Name, StringComparison.OrdinalIgnoreCase)
                    ? null : Retargeter.Between(animations, clip.Animation.SkeletonName, rig.Name);
                int frames = clip.Animation.FrameCount;
                float frameDuration = clip.Animation.FrameDuration > 0 ? clip.Animation.FrameDuration : 1f / 30f;
                frame = call.Has("time") ? (int)Math.Round(call.Num("time") / frameDuration) : call.Int("frame", 0);
                if (frame < 0 || frame >= Math.Max(1, frames)) throw McpError.Invalid("The clip has frames 0-" + (frames - 1) + " (" + Math.Round(frames * frameDuration, 2) + " s).");
                modelName = model.Name;
                clipName = clip.Name;
                string leaf = Path.GetFileNameWithoutExtension((model.Name ?? "MODEL").Replace('/', '\\').Split('\\').Last());
                string outName = (call.Str("name") ?? ("POSED\\" + leaf + "_" + clip.Name + "_F" + frame)).Trim();
                string stem = new string(outName.Split('\\').Last().Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
                file = keep ?? Path.Combine(folder, stem + ".fbx");
                PoseBake.Options options = new PoseBake.Options()
                {
                    Model = model,
                    Skeleton = rig,
                    Clip = clip,
                    Frame = frame,
                    Space = space,
                    PreviewRoot = rootMotion == "follow" ? Anim.RootMotion.Follow : Anim.RootMotion.Ignore,
                    Retarget = retarget,
                    Lod = Math.Max(0, call.Int("lod", 0)),
                    Exclude = call.Has("exclude") ? new HashSet<string>(call.StrList("exclude"), StringComparer.OrdinalIgnoreCase) : null,
                    MaterialPrefix = stem.ToUpperInvariant(),
                };
                PoseBake.Result baked;
                try { baked = PoseBake.Write(options, file); }
                catch (ArgumentException e) { throw McpError.Invalid(e.Message); }
                materials = baked.Materials;
                report = new JObject()
                {
                    ["model"] = model.Name,
                    ["rig"] = rig.Name + (retarget != null ? " (the clip retargeted from " + clip.Animation.SkeletonName + ")" : ""),
                    ["clip"] = set.Name + "/" + clip.Name,
                    ["frame"] = frame,
                    ["time"] = Math.Round(frame * frameDuration, 3),
                    ["space"] = space,
                    ["parts"] = baked.Meshes,
                    ["vertices"] = baked.Vertices,
                    ["triangles"] = baked.Triangles,
                    ["bounds"] = new JObject() { ["min"] = V(baked.Min), ["max"] = V(baked.Max), ["height_m"] = Math.Round(baked.Max.Y - baked.Min.Y, 3) },
                    ["fit_cm"] = new JObject() { ["rest"] = Math.Round(baked.RestFit * 100, 2), ["posed"] = Math.Round(baked.PosedFit * 100, 2), ["bones"] = baked.FitBones },
                    ["materials"] = new JArray(baked.Materials),
                    ["file"] = file,
                    ["new_name"] = outName,
                };
                if (baked.Skipped.Count != 0) report["left_out"] = new JArray(baked.Skipped);
                if (baked.Notes.Count != 0) report["notes"] = new JArray(baked.Notes);
            });
            if (preview)
            {
                report["preview"] = true;
                return report;
            }

            //The ordinary import: static, CA_ENVIRONMENT materials made from the character's textures
            string newName = (string)report["new_name"];
            JObject generate = new JObject();
            string prefix = new string(newName.Split('\\').Last().Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray()).ToUpperInvariant() + "_";
            foreach (string material in materials)
                generate[material] = new JObject() { ["family"] = "CA_ENVIRONMENT", ["name"] = newName + "_" + (material.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? material.Substring(prefix.Length) : material), ["always_new"] = true };
            JObject args = new JObject()
            {
                ["path"] = file,
                ["name"] = newName,
                ["generate"] = generate,
                ["skeleton"] = "none",
                ["create_composite"] = true,
                ["collision"] = "none",
            };
            if (call.Has("composite_name")) args["composite_name"] = call.Str("composite_name");
            if (call.Has("place_in"))
            {
                args["place_in"] = call.Str("place_in");
                if (call.Has("position")) args["position"] = call.Token("position").DeepClone();
                if (call.Has("rotation")) args["rotation"] = call.Token("rotation").DeepClone();
                if (call.Has("place_space")) args["space"] = call.Str("place_space");
                if (call.Has("instance_name")) args["instance_name"] = call.Str("instance_name");
            }
            McpTool import = McpTools.Find("import_model") ?? throw new McpError("The import_model tool is missing from this build.");
            McpCall nested = new McpCall(import, args, call.Cancel, (text, d, total) => call.Progress(text, d, total));
            object imported = import.Run(nested);
            foreach (string note in nested.Notes) call.Note(note);
            JObject importedJson = imported as JObject ?? (imported == null ? new JObject() : JObject.FromObject(imported));
            report["imported"] = importedJson;
            if (keep == null)
                try { Directory.Delete(folder, true); } catch { }
            report["next"] = "save_level to have it in the game. To ride a vehicle frozen in this pose: place it beside the vehicle and ride_along it with the vehicle's keys.";
            return report;
        }
    }
}
