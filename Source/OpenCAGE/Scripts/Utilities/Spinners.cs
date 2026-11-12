using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE
{
    /// <summary>
    /// "Rotate continuously" for a prop, a light or a composite instance: fans, rotors, beacons and turning gobo lights without keyframes.
    /// Retail's wiring (Industria_Extractor's fan and its gobo spot): a looping RotateAtSpeed whose start_pos, origin and Result are linked
    /// to the entity's 'reference', turning it about ITS OWN axes (speed_X / speed_Y / speed_Z, degrees per second), started by a
    /// ThinkOnce; a LightReference also gets on_think -> refresh. A spot shines along its own +Z, so a gobo turns about Z.
    /// </summary>
    public static class Spinners
    {
        public enum Axis { X, Y, Z }

        public const string StarterName = "Spin_Start";
        private static readonly ShortGuid ReferencePin = ShortGuidUtils.Generate("reference");
        private static readonly ShortGuid ResultPin = ShortGuidUtils.Generate("Result");

        public sealed class Spinner
        {
            public Composite Composite;
            public FunctionEntity RotateAtSpeed;
            public Entity Target;
            public Axis Axis;
            public float DegreesPerSecond;
        }

        /// <summary>What can be turned: a ModelReference, a LightReference, an EnvironmentModelReference or a composite instance.</summary>
        public static bool CanSpin(Entity entity, out string why)
        {
            why = null;
            if (!(entity is FunctionEntity function))
            {
                why = "Only a prop (ModelReference), a light (LightReference) or a composite instance can be turned; " + (entity == null ? "nothing is selected." : "this is a " + entity.variant.ToString().ToLowerInvariant() + ".");
                return false;
            }
            if (!function.function.IsFunctionType)
                return true;
            switch (function.function.AsFunctionType)
            {
                case FunctionType.ModelReference:
                case FunctionType.LightReference:
                case FunctionType.EnvironmentModelReference:
                    return true;
            }
            why = "Only a prop (ModelReference), a light (LightReference) or a composite instance can be turned; this is a " + function.function.AsFunctionType + ".";
            return false;
        }

        public static bool IsLight(Entity entity) => entity is FunctionEntity f && f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.LightReference;

        /// <summary>The RotateAtSpeed already turning <paramref name="target"/> in its composite (its Result linked to the target's reference), or null.</summary>
        public static Spinner Find(Composite composite, Entity target)
        {
            if (composite == null || target == null) return null;
            foreach (FunctionEntity rotate in composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.RotateAtSpeed))
            {
                if (!rotate.childLinks.Any(o => o.thisParamID == ResultPin && o.linkedEntityID == target.shortGUID && o.linkedParamID == ReferencePin)) continue;
                Spinner spinner = new Spinner() { Composite = composite, RotateAtSpeed = rotate, Target = target, Axis = Axis.Y };
                foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
                {
                    float speed = (rotate.GetParameter("speed_" + axis)?.content as cFloat)?.value ?? 0f;
                    if (speed != 0f) { spinner.Axis = axis; spinner.DegreesPerSecond = speed; break; }
                }
                return spinner;
            }
            return null;
        }

        /// <summary>Every spinner in the composite.</summary>
        public static List<Spinner> All(Composite composite)
        {
            List<Spinner> found = new List<Spinner>();
            if (composite == null) return found;
            foreach (Entity entity in composite.GetEntities())
            {
                Spinner spinner = Find(composite, entity);
                if (spinner != null) found.Add(spinner);
            }
            return found;
        }

        /// <summary>The box the entity's model fills in its own space (the union of what its renderable draws), or null.</summary>
        public static (Vector3 Min, Vector3 Max)? ModelBounds(Entity entity)
        {
            if (!(entity is FunctionEntity function)) return null;
            ResourceReference renderable = function.GetResource(ResourceType.RENDERABLE_INSTANCE, true);
            List<RenderableElements.Element> elements = renderable?.RenderableInstance?.Where(o => o?.Model != null).ToList();
            if (elements == null || elements.Count == 0) return null;
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (RenderableElements.Element element in elements)
            {
                min = Vector3.Min(min, element.Model.MinBounds);
                max = Vector3.Max(max, element.Model.MaxBounds);
            }
            return (min, max);
        }

        /// <summary>
        /// The axis it most likely turns about: a light about Z (the way a spot shines), a model about its thinnest side (a fan disc,
        /// a rotor), anything else about Y. <paramref name="why"/> says which rule.
        /// </summary>
        public static Axis SuggestAxis(Entity entity, out string why)
        {
            if (IsLight(entity))
            {
                why = "a light turns about Z, the way a spot shines (a gobo pattern turns in place)";
                return Axis.Z;
            }
            (Vector3 Min, Vector3 Max)? bounds = ModelBounds(entity);
            if (bounds != null)
            {
                Vector3 size = bounds.Value.Max - bounds.Value.Min;
                Axis thin = size.X <= size.Y && size.X <= size.Z ? Axis.X : size.Y <= size.Z ? Axis.Y : Axis.Z;
                float smallest = Math.Min(size.X, Math.Min(size.Y, size.Z)), next = new[] { size.X, size.Y, size.Z }.OrderBy(o => o).ElementAt(1);
                if (next > 0 && smallest < next * 0.6f)
                {
                    why = "its model is thinnest along " + thin + " (" + size.X.ToString("0.##") + " x " + size.Y.ToString("0.##") + " x " + size.Z.ToString("0.##") + " m): a disc turns about its thin side";
                    return thin;
                }
                why = "its model has no thin side (" + size.X.ToString("0.##") + " x " + size.Y.ToString("0.##") + " x " + size.Z.ToString("0.##") + " m), so its own up axis";
                return Axis.Y;
            }
            why = "its own up axis";
            return Axis.Y;
        }

        /// <summary>
        /// Why a spinner in <paramref name="composite"/> would not move in game, or null: a CAGEAnimation keys an instance of it (or of
        /// a composite holding it), and an animation inside an instance another CAGEAnimation keys stays at time 0 while that one plays.
        /// </summary>
        public static string WhyFrozen(Commands commands, Composite composite)
        {
            if (commands == null || composite == null) return null;
            //The composite and every composite that holds an instance of it, however deep
            HashSet<ShortGuid> holders = new HashSet<ShortGuid>() { composite.shortGUID };
            bool grew = true;
            for (int pass = 0; grew && pass < 16; pass++)
            {
                grew = false;
                foreach (Composite other in commands.Entries)
                {
                    if (other == null || holders.Contains(other.shortGUID)) continue;
                    if (other.functions.Any(o => !o.function.IsFunctionType && holders.Contains(o.function)))
                    {
                        holders.Add(other.shortGUID);
                        grew = true;
                    }
                }
            }
            foreach (Composite holder in commands.Entries)
            {
                if (holder == null) continue;
                foreach (CAGEAnimation animation in holder.functions.OfType<CAGEAnimation>())
                {
                    foreach (CAGEAnimation.Connection connection in animation.connections)
                    {
                        if (connection?.connectedEntity?.path == null) continue;
                        List<Tuple<Composite, Entity>> resolved;
                        try { resolved = commands.Utils.ResolveEntityPath(connection.connectedEntity, holder); }
                        catch { continue; }
                        if (resolved == null) continue;
                        foreach (Tuple<Composite, Entity> step in resolved)
                        {
                            if (step.Item2 is FunctionEntity instance && !instance.function.IsFunctionType && holders.Contains(instance.function))
                                return "CAGEAnimation '" + commands.Utils.GetEntityName(holder, animation) + "' in " + EditorUtils.GetCompositeName(holder) + " keys " + commands.Utils.GetEntityName(step.Item1, instance) +
                                    ", an instance holding " + EditorUtils.GetCompositeName(composite) + ": an animation inside an instance another CAGEAnimation keys stays at time 0 while that one plays, so this would not turn. Key the turn in that CAGEAnimation instead, or move the entity out of the keyed instance.";
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Make the target turn (or change how it turns), inside an MCP script edit: one RotateAtSpeed per target ("Spin_" + its name),
        /// looping, the speed on the chosen own axis, start_pos / origin / Result to its reference, on_think -> refresh for a light; the
        /// composite's ThinkOnce "Spin_Start" (made once, start_on_reset) starts it. Returns the spinner.
        /// </summary>
        internal static Spinner Apply(McpScriptEdit edit, Composite composite, Entity target, Axis axis, float degreesPerSecond)
        {
            if (!CanSpin(target, out string why)) throw new McpError(why);
            if (degreesPerSecond == 0f) throw new McpError("A turn of 0 degrees per second doesn't move it: give a speed (negative turns the other way), or remove the rotation.");
            Commands commands = edit.Commands;
            Spinner existing = Find(composite, target);
            FunctionEntity rotate = existing?.RotateAtSpeed;
            string targetName = commands.Utils.GetEntityName(composite, target) ?? target.shortGUID.ToByteString();
            if (rotate == null)
            {
                rotate = edit.AddFunction(composite, FunctionType.RotateAtSpeed, UniqueName(commands, composite, "Spin_" + targetName));
                edit.AddLink(composite, rotate, "start_pos", target, "reference");
                edit.AddLink(composite, rotate, "origin", target, "reference");
                edit.AddLink(composite, rotate, "Result", target, "reference");
                if (IsLight(target))
                    edit.AddLink(composite, rotate, "on_think", target, "refresh");
                FunctionEntity starter = composite.functions.FirstOrDefault(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.ThinkOnce && commands.Utils.GetEntityName(composite, o) == StarterName);
                if (starter == null)
                {
                    starter = edit.AddFunction(composite, FunctionType.ThinkOnce, StarterName);
                    edit.SetParameter(composite, starter, "start_on_reset", new JValue(true));
                }
                edit.AddLink(composite, starter, "on_think", rotate, "start");
            }
            else
                edit.Touch(composite, rotate);
            edit.SetParameter(composite, rotate, "loop", new JValue(true));
            foreach (Axis each in new[] { Axis.X, Axis.Y, Axis.Z })
                edit.SetParameter(composite, rotate, "speed_" + each, new JValue(each == axis ? (double)degreesPerSecond : 0.0));
            return new Spinner() { Composite = composite, RotateAtSpeed = rotate, Target = target, Axis = axis, DegreesPerSecond = degreesPerSecond };
        }

        /// <summary>Stop the target turning: its RotateAtSpeed goes (and the composite's Spin_Start when nothing else uses it). False when it wasn't turning.</summary>
        internal static bool Remove(McpScriptEdit edit, Composite composite, Entity target)
        {
            Spinner existing = Find(composite, target);
            if (existing == null) return false;
            Commands commands = edit.Commands;
            FunctionEntity starter = composite.functions.FirstOrDefault(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.ThinkOnce && commands.Utils.GetEntityName(composite, o) == StarterName);
            edit.Delete(composite, existing.RotateAtSpeed);
            if (starter != null && starter.childLinks.Count == 0 && !composite.GetEntities().Any(o => o.childLinks.Any(l => l.linkedEntityID == starter.shortGUID)))
                edit.Delete(composite, starter);
            return true;
        }

        private static string UniqueName(Commands commands, Composite composite, string wanted)
        {
            HashSet<string> taken = new HashSet<string>(composite.GetEntities().Select(o => commands.Utils.GetEntityName(composite, o) ?? ""), StringComparer.OrdinalIgnoreCase);
            if (!taken.Contains(wanted)) return wanted;
            for (int i = 2; ; i++)
                if (!taken.Contains(wanted + "_" + i)) return wanted + "_" + i;
        }

        /// <summary>"one turn every 3.0 s" for a speed.</summary>
        public static string Period(float degreesPerSecond) =>
            degreesPerSecond == 0f ? "still" : "one turn every " + (360.0 / Math.Abs(degreesPerSecond)).ToString(Math.Abs(degreesPerSecond) >= 36 ? "0.00" : "0.0") + " s";
    }
}
