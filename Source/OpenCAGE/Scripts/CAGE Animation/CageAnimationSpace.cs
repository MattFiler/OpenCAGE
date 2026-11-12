using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE
{
    /// <summary>
    /// The space a CAGEAnimation's transform keys apply in. A CAGEAnimation keying a target inside composite instances applies its keys
    /// in the animation's own composite's space - the instances in between do not move them - so a key and the target's resting
    /// <c>position</c> differ by the instances' transforms. A path from the level's root keeps keys in the holding composite's space.
    /// </summary>
    internal static class CageAnimationSpace
    {
        private static readonly ShortGuid PositionParam = ShortGuidUtils.Generate("position");

        /// <summary>
        /// The frame of the composite holding a nested target, in the animation's composite's space: its instances composed, each at
        /// <paramref name="animated"/>'s transform for it (an instance this animation keys: its key, which is already in the animation's
        /// space) or else at its resting placement. Null for a target directly in the animation's composite, a path from the level root,
        /// or a path that does not resolve - for those a key is the parameter itself.
        /// </summary>
        public static cTransform HolderFrame(Commands commands, Composite animationComposite, EntityPath stored, Func<ShortGuid[], cTransform> animated = null)
        {
            if (commands == null || animationComposite == null || stored?.path == null) return null;
            List<Tuple<Composite, Entity>> chain;
            try { chain = commands.Utils.ResolveEntityPath(stored, animationComposite); }
            catch { return null; }
            if (chain == null || chain.Count < 2 || chain[0].Item1 != animationComposite) return null;
            ShortGuid[] ids = stored.path.Where(o => !o.IsInvalid).ToArray();
            cTransform frame = null;
            for (int i = 0; i < chain.Count - 1; i++)
            {
                cTransform keyed = animated != null && i < ids.Length ? animated(ids.Take(i + 1).ToArray()) : null;
                if (keyed != null)
                {
                    frame = new cTransform(keyed.position, keyed.rotation);
                    continue;
                }
                cTransform rest = InstanceTransform.TransformOf(chain[i].Item2) ?? new cTransform(Vector3.Zero, Vector3.Zero);
                frame = frame == null ? new cTransform(rest.position, rest.rotation) : InstanceTransform.Compose(frame, rest);
            }
            return frame;
        }

        /// <summary>Whether a stored path reaches its target through instances from the animation's composite (keys and parameter differ).</summary>
        public static bool IsNested(Commands commands, Composite animationComposite, EntityPath stored) => HolderFrame(commands, animationComposite, stored) != null;

        /// <summary>A target's parameter value (its own composite's space) as a key would hold it; unchanged unless nested.</summary>
        public static cTransform ToKey(cTransform holder, cTransform parameterValue)
        {
            if (parameterValue == null) return null;
            if (holder == null) return new cTransform(parameterValue.position, parameterValue.rotation);
            return InstanceTransform.Compose(holder, parameterValue);
        }

        /// <summary>A key as the target's own parameter would hold it (its own composite's space); unchanged unless nested.</summary>
        public static cTransform FromKey(cTransform holder, cTransform key)
        {
            if (key == null) return null;
            if (holder == null) return new cTransform(key.position, key.rotation);
            return InstanceTransform.ToLocal(holder, key);
        }

        /// <summary>
        /// What a transform track of this target should rest at (a new track's seed): the resting <paramref name="parameter"/> value
        /// in the key's space. Only <c>position</c> is moved into the animation's space - other transform parameters are not placements.
        /// </summary>
        public static cTransform RestingKey(Commands commands, Composite animationComposite, EntityPath stored, ShortGuid parameter, cTransform resting)
        {
            if (resting == null || parameter != PositionParam) return resting;
            return ToKey(HolderFrame(commands, animationComposite, stored), resting);
        }

        /// <summary>One component (x, y, z, Yaw, Pitch, Roll) of a transform.</summary>
        public static float Component(cTransform t, string component)
        {
            switch (component)
            {
                case "x": return t.position.X;
                case "y": return t.position.Y;
                case "z": return t.position.Z;
                case "Yaw": return t.rotation.Y;
                case "Pitch": return t.rotation.X;
                case "Roll": return t.rotation.Z;
            }
            return 0f;
        }
    }
}
