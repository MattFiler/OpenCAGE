using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenCAGE.Rigging
{
    /// <summary>
    /// Adds named contexts to an animation set: clip lines the game swaps in while a character is in that state (as MALE's
    /// WEAPON_HANDGUN; an item's character_animation_context names one). A context lives in the set's x_ANIM_CLIP_DB.BIN and its
    /// name only in the debug string table. A context is a table of names onto the set's shared clips.
    /// </summary>
    public static class AnimationContexts
    {
        public class Result
        {
            public string Problem;
            public CathodeLib.Animation.AnimationContext Context;
            public List<string> Report = new List<string>();
            public bool Ok => Problem == null;
        }

        public static string NameProblem(CathodeLib.Animation.AnimationSet set, string name)
        {
            if (set?.Database == null) return "That animation set has no clip database to add a context to.";
            if (string.IsNullOrWhiteSpace(name)) return "Give the context a name.";
            if (!Regex.IsMatch(name, "^[A-Z][A-Z0-9_]{1,47}$")) return "A context's name is 2 to 48 upper case letters, digits and underscores, starting with a letter (as WEAPON_HANDGUN).";
            if (set.Database.Contexts.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                return set.Name + " already has a context called " + name + ".";
            return null;
        }

        /// <summary>The set's contexts: the unnamed one first (the clips that always apply), then each named one.</summary>
        public static List<CathodeLib.Animation.AnimationContext> Of(CathodeLib.Animation.AnimationSet set)
        {
            return set?.Contexts.ToList() ?? new List<CathodeLib.Animation.AnimationContext>();
        }

        /// <summary>Adds a context to a set, empty or copying <paramref name="template"/>'s lines ("" = the set's own lines).</summary>
        public static Result Create(CathodeLib.Animation anim, CathodeLib.Animation.AnimationSet set, string name, string template = null, bool copyBlendSets = true)
        {
            Result r = new Result();
            name = (name ?? "").Trim().ToUpperInvariant();
            r.Problem = NameProblem(set, name);
            if (r.Problem != null) return r;

            List<AnimClipDB.AnimClip> lines = new List<AnimClipDB.AnimClip>();
            List<AnimClipDB.BlendSet> blends = new List<AnimClipDB.BlendSet>();
            CathodeLib.Animation.AnimationContext from = null;
            if (template != null)
            {
                if (template.Length == 0)
                {
                    lines = set.Database.Animations; blends = set.Database.BlendSets;
                    from = set.Contexts.FirstOrDefault(c => c.Name.Length == 0);
                }
                else
                {
                    AnimClipDB.Context source = set.Database.Contexts.FirstOrDefault(c => string.Equals(c.Name, template, StringComparison.OrdinalIgnoreCase));
                    if (source == null) { r.Problem = set.Name + " has no context " + template + " to copy."; return r; }
                    lines = source.Animations; blends = source.BlendSets;
                    from = set.Contexts.FirstOrDefault(c => string.Equals(c.Name, template, StringComparison.OrdinalIgnoreCase));
                }
            }

            //Context names live in the debug string table; the file writes them through the plain table's fallback
            anim.AddName(name, true);
            AnimClipDB.Context context = new AnimClipDB.Context
            {
                Name = name,
                Animations = lines.Select(o => new AnimClipDB.AnimClip { Name = o.Name, Path = o.Path, MetadataInstance = o.MetadataInstance }).ToList(),
                BlendSets = copyBlendSets ? blends.Select(o => new AnimClipDB.BlendSet { Name = o.Name, Filename = o.Filename }).ToList() : new List<AnimClipDB.BlendSet>(),
            };
            set.Database.Contexts.Add(context);

            //Keep the resolved view the editor browses in step rather than rebuilding it
            CathodeLib.Animation.AnimationContext resolved = new CathodeLib.Animation.AnimationContext { Name = name, Set = set };
            if (from != null)
                foreach (CathodeLib.Animation.ClipReference clip in from.Clips)
                    resolved.Clips.Add(new CathodeLib.Animation.ClipReference { Name = clip.Name, Path = clip.Path, Context = resolved, Section = clip.Section, Index = clip.Index });
            set.Contexts.Add(resolved);
            set.ClipCount += resolved.Clips.Count;
            r.Context = resolved;
            r.Report.Add(set.Name + "\\" + name + ": " + context.Animations.Count + " clip line(s) and " + context.BlendSets.Count + " blend set line(s)"
                + (template == null ? " (empty)" : " copied from " + (template.Length == 0 ? "the set's own lines" : template)) + ".");
            return r;
        }

        /// <summary>Points a context's line <paramref name="slot"/> at a clip the set holds, adding the line if missing.</summary>
        public static string SetLine(CathodeLib.Animation.AnimationSet set, string contextName, string slot, string clipName)
        {
            AnimClipDB.Context context = set?.Database?.Contexts.FirstOrDefault(c => string.Equals(c.Name, contextName, StringComparison.OrdinalIgnoreCase));
            CathodeLib.Animation.AnimationContext resolved = set?.Contexts.FirstOrDefault(c => c.Name.Length != 0 && string.Equals(c.Name, contextName, StringComparison.OrdinalIgnoreCase));
            if (context == null || resolved == null) return (set?.Name ?? "The set") + " has no context " + contextName + ".";
            CathodeLib.Animation.ClipReference clip = set.Contexts.SelectMany(c => c.Clips).FirstOrDefault(c => string.Equals(c.Name, clipName, StringComparison.OrdinalIgnoreCase));
            if (clip == null) return set.Name + " has no clip " + clipName + " (import it into the set first).";
            AnimClipDB.AnimClip source = set.Database.Animations.Concat(set.Database.Contexts.SelectMany(c => c.Animations)).FirstOrDefault(o => string.Equals(o.Path, clip.Path, StringComparison.OrdinalIgnoreCase));
            AnimClipDB.AnimClip line = context.Animations.FirstOrDefault(o => string.Equals(o.Name, slot, StringComparison.OrdinalIgnoreCase));
            if (line == null) { line = new AnimClipDB.AnimClip { Name = slot }; context.Animations.Add(line); }
            line.Path = clip.Path;
            line.MetadataInstance = source?.MetadataInstance ?? 0;
            CathodeLib.Animation.ClipReference view = resolved.Clips.FirstOrDefault(o => string.Equals(o.Name, slot, StringComparison.OrdinalIgnoreCase));
            if (view == null) { view = new CathodeLib.Animation.ClipReference { Name = slot, Context = resolved }; resolved.Clips.Add(view); set.ClipCount++; }
            view.Path = clip.Path; view.Section = clip.Section; view.Index = clip.Index;
            return null;
        }
    }
}
