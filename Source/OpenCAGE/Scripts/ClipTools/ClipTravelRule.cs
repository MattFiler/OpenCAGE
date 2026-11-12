using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.ClipTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE
{
    /// <summary>
    /// Level Check: a CMD_PlayAnimation playing a clip whose body travels away from the character's capsule. The game puts the capsule at
    /// marker * inverse(REFERENCE_ROOT) and measures animation LOD, culling and AI from it, so a clip that moves the body while
    /// REFERENCE_ROOT holds still leaves the capsule behind and snaps back to it when it ends. Retail travelling gameplay clips keep the
    /// body within about half a metre (REFERENCE_ROOT carries the travel); some retail cutscene clips don't, so only user composites are checked.
    /// </summary>
    internal sealed class ClipParksCapsuleRule : LevelCheckRule
    {
        const float Far = 1.5f;
        public override string Id => "clip_parks_capsule";
        public override string Name => "Travelling clip leaves its capsule behind";
        public override string About => "A CMD_PlayAnimation plays a clip whose body travels more than " + Far + " m from the character's capsule (its REFERENCE_ROOT holds still while the body moves): the game updates and culls the character by where the capsule is, and it snaps back there when the clip ends.";

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            Dictionary<Animation.ClipReference, float> measured = new Dictionary<Animation.ClipReference, float>();
            foreach ((Composite composite, FunctionEntity entity) in context.InUsers(FunctionType.CMD_PlayAnimation))
            {
                Animation.ClipReference clip = ClipSync.ClipOf(entity);
                if (clip == null || !clip.Playable || clip.Additive) continue;
                if (!measured.TryGetValue(clip, out float far)) measured[clip] = far = Measure(clip);
                if (far <= Far) continue;
                found.Add(new LevelCheckFinding()
                {
                    Key = LevelCheckScript.Key(composite, entity),
                    Severity = LevelCheckSeverity.Warning,
                    Title = context.Where(composite, entity) + " leaves its capsule behind",
                    Text = "It plays '" + clip.Name + "', whose body travels up to " + far.ToString("0.0") + " m from the character's capsule: the clip moves the body but not REFERENCE_ROOT, "
                        + "so the capsule stays where the clip started. The game measures animation detail and culling from the capsule - far from it the character is posed less often (it judders) "
                        + "and vanishes when that spot is out of view - and the character snaps back to it when the clip ends. (Some of the game's own cutscene clips are like this; in a short "
                        + "shot close to the capsule, with the character despawned or held afterwards, it can be left.) Otherwise rebuild the clip the game's way, the capsule following the body: "
                        + "the Animation Editor's import with Root bone 'Travels at a marker', or the AI assistant's make_clip_travel (ANIMATION.PAK, every level).",
                    Sources = { context.Source(composite, entity, clip.Name + ": " + far.ToString("0.0") + " m") },
                });
            }
        }

        /// <summary>The furthest the body (HIPS) gets from the capsule across the floor, over a dozen frames of the clip, as the game draws it.</summary>
        public static float Measure(Animation.ClipReference clip)
        {
            try
            {
                Skeleton rig = Singleton.Animations?.GetSkeleton(clip.Skeleton)?.Skeleton;
                int reference = RigBones.ReferenceRoot(rig), body = RigBones.Body(rig);
                if (rig == null || reference < 0 || body < 0) return 0;
                int frames = clip.Animation.FrameCount;
                float far = 0;
                for (int i = 0; i <= 12; i++)
                {
                    int f = (int)Math.Round((frames - 1) * i / 12.0);
                    //drawn at model x inverse(model REFERENCE_ROOT) from the marker, with the capsule at inverse(model REFERENCE_ROOT): so the
                    //body sits at its plain model position from the capsule
                    Vector3 hips = ClipFrames.FromClip(clip, rig, f, f).Model(0)[body].Translation;
                    far = Math.Max(far, new Vector2(hips.X, hips.Z).Length());
                }
                return far;
            }
            catch { return 0; }
        }
    }
}
