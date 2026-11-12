using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Characters
{
    /// <summary>
    /// The shape every shipped "DisplayModel:" composite has, which the game relies on when a character wears it:
    ///  - the composite's id is ShortGuid(name after "DisplayModel:") - the game hashes the display_model string to find it;
    ///  - its EnvironmentModelReference (the skeleton) has the composite's own id, and an ANIMATED_MODEL resource with the id 00-E4-14-34;
    ///  - that resource's animated-model entry (ENVIRONMENT_ANIMATION.DAT) is its own - no two display models share one - and lists in
    ///    MeshMappings exactly the RENDERABLE_INSTANCE resource ids of its parts (the ModelReferences drawing the body, the hit collision...).
    /// A copy or a renamed composite keeps the old ids unless something fixes them: this does.
    /// </summary>
    public static class DisplayModelShape
    {
        public const string Prefix = "DisplayModel:";

        /// <summary>The id every shipped display model gives its ANIMATED_MODEL resource.</summary>
        public static readonly ShortGuid AnimatedModelResourceId = new ShortGuid("00-E4-14-34");

        public static bool IsDisplayModel(Composite composite) => IsDisplayModelName(composite?.name);
        public static bool IsDisplayModelName(string name) => name != null && name.Length > Prefix.Length && name.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>What a character's display_model says to wear this one: the name after the prefix.</summary>
        public static string CharacterName(Composite composite) => IsDisplayModel(composite) ? composite.name.Substring(Prefix.Length) : null;

        /// <summary>The id the game will look this composite up by.</summary>
        public static ShortGuid IdFor(string name) => Composite.DisplayModelId(name) ?? ShortGuid.Invalid;

        /// <summary>The skeleton entity: the EnvironmentModelReference carrying an ANIMATED_MODEL (the one with the composite's id first).</summary>
        public static FunctionEntity SkeletonEntity(Composite composite)
        {
            if (composite == null) return null;
            List<FunctionEntity> references = composite.functions.Where(f => f.function == FunctionType.EnvironmentModelReference).ToList();
            return references.FirstOrDefault(f => f.shortGUID == composite.shortGUID && AnimatedModelReference(f) != null)
                ?? references.FirstOrDefault(f => AnimatedModelReference(f) != null)
                ?? references.FirstOrDefault();
        }

        public static ResourceReference AnimatedModelReference(FunctionEntity skeleton)
        {
            cResource resource = skeleton?.GetParameter(ShortGuids.resource)?.content as cResource;
            return resource?.value?.FirstOrDefault(r => r.resource_type == ResourceType.ANIMATED_MODEL);
        }

        public static EnvironmentAnimations.EnvironmentAnimation Entry(Composite composite) => AnimatedModelReference(SkeletonEntity(composite))?.AnimatedModel;

        /// <summary>The rig the display model is on (its animated-model entry's skeleton, or the skeleton entity's name parameter).</summary>
        public static string RigName(Composite composite)
        {
            FunctionEntity skeleton = SkeletonEntity(composite);
            return AnimatedModelReference(skeleton)?.AnimatedModel?.SkeletonName ?? (skeleton?.GetParameter("name")?.content as cString)?.value;
        }

        /// <summary>Every part the display model draws: each ModelReference (or other entity) with a RENDERABLE_INSTANCE resource.</summary>
        public static List<(FunctionEntity Entity, ResourceReference Renderable)> Parts(Composite composite)
        {
            List<(FunctionEntity, ResourceReference)> parts = new List<(FunctionEntity, ResourceReference)>();
            if (composite == null) return parts;
            foreach (FunctionEntity function in composite.functions)
            {
                if (function.function == FunctionType.EnvironmentModelReference) continue;
                cResource resource = function.GetParameter(ShortGuids.resource)?.content as cResource;
                foreach (ResourceReference reference in resource?.value ?? new List<ResourceReference>())
                    if (reference.resource_type == ResourceType.RENDERABLE_INSTANCE)
                        parts.Add((function, reference));
            }
            return parts;
        }

        /// <summary>Every display model of the level using this animated-model entry.</summary>
        public static List<Composite> Users(Commands commands, EnvironmentAnimations.EnvironmentAnimation entry)
        {
            if (entry == null) return new List<Composite>();
            return commands.Entries.Where(o => IsDisplayModel(o) && ReferenceEquals(Entry(o), entry)).ToList();
        }

        public class Report
        {
            public List<string> Problems = new List<string>();
            public List<string> Facts = new List<string>();
            public bool IdWrong, SkeletonIdWrong, ResourceIdWrong, NoEntry, EntryShared, MappingsWrong, NoSkeleton;
            public bool Ok => Problems.Count == 0;
            /// <summary>What <see cref="DisplayModelShape"/> can put right without asking (everything but a missing skeleton or entry).</summary>
            public bool Fixable => !NoSkeleton && !NoEntry && (IdWrong || SkeletonIdWrong || ResourceIdWrong || EntryShared || MappingsWrong);
        }

        /// <summary>How this display model compares with the shipped shape.</summary>
        public static Report Inspect(Commands commands, Composite composite)
        {
            Report report = new Report();
            if (!IsDisplayModel(composite)) { report.Problems.Add(composite?.name + " is not a DisplayModel: composite."); return report; }
            ShortGuid want = IdFor(composite.name);
            if (composite.shortGUID != want)
            {
                report.IdWrong = true;
                report.Problems.Add("Its id is " + composite.shortGUID.ToByteString() + ", not " + want.ToByteString() + " (the hash of '" + CharacterName(composite) + "'): a character asking for " + CharacterName(composite) + " won't find it.");
            }
            FunctionEntity skeleton = SkeletonEntity(composite);
            ResourceReference animated = AnimatedModelReference(skeleton);
            if (skeleton == null || animated == null)
            {
                report.NoSkeleton = true;
                report.Problems.Add("It has no EnvironmentModelReference with an animated model (the skeleton a character's animation drives): the game crashes setting up a character wearing it.");
                return report;
            }
            report.Facts.Add("rig " + RigName(composite) + ", skeleton entity " + skeleton.shortGUID.ToByteString());
            if (skeleton.shortGUID != composite.shortGUID)
            {
                report.SkeletonIdWrong = true;
                report.Problems.Add("Its skeleton entity's id " + skeleton.shortGUID.ToByteString() + " is not the composite's own (every shipped display model's is).");
            }
            cResource resource = skeleton.GetParameter(ShortGuids.resource)?.content as cResource;
            if (resource != null && (resource.shortGUID != AnimatedModelResourceId || animated.resource_id != AnimatedModelResourceId))
            {
                report.ResourceIdWrong = true;
                report.Problems.Add("Its ANIMATED_MODEL resource id is " + resource.shortGUID.ToByteString() + ", not the shipped 00-E4-14-34.");
            }
            EnvironmentAnimations.EnvironmentAnimation entry = animated.AnimatedModel;
            if (entry == null)
            {
                report.NoEntry = true;
                report.Problems.Add("Its skeleton has no animated-model entry: re-import the model on its rig to make one.");
                return report;
            }
            report.Facts.Add("animated-model entry " + entry.ID + " on " + entry.SkeletonName + ", " + (entry.MeshMappings?.Count ?? 0) + " part(s) mapped");
            List<Composite> users = Users(commands, entry).Where(o => o != composite).ToList();
            if (users.Count != 0)
            {
                report.EntryShared = true;
                report.Problems.Add("It shares its animated-model entry with " + string.Join(", ", users.Select(o => o.name)) + " (no shipped display model shares one; a part added to either would bind wrong).");
            }
            HashSet<ShortGuid> parts = new HashSet<ShortGuid>(Parts(composite).Select(o => o.Renderable.resource_id));
            HashSet<ShortGuid> mapped = new HashSet<ShortGuid>(entry.MeshMappings ?? new List<ShortGuid>());
            List<ShortGuid> missing = parts.Where(o => !mapped.Contains(o)).ToList(), extra = mapped.Where(o => !parts.Contains(o)).ToList();
            if (missing.Count != 0 || extra.Count != 0)
            {
                report.MappingsWrong = true;
                report.Problems.Add("Its animated-model entry " + (missing.Count != 0 ? "doesn't list " + missing.Count + " of its parts (they won't follow the skeleton)" : "") +
                    (missing.Count != 0 && extra.Count != 0 ? " and " : "") + (extra.Count != 0 ? "lists " + extra.Count + " part(s) it no longer has" : "") + ".");
            }
            return report;
        }

        /// <summary>
        /// Give an entity of a composite a new id: its key, and every link and path inside the composite naming it. The caller makes sure
        /// nothing outside the composite points at it (a display model's skeleton is only ever looked up by the composite's id).
        /// </summary>
        public static void ChangeEntityId(Composite composite, Entity entity, ShortGuid id)
        {
            ShortGuid old = entity.shortGUID;
            if (old == id) return;
            if (composite.GetEntityByID(id) != null) throw new InvalidOperationException(composite.name + " already has an entity " + id.ToByteString() + ".");
            switch (entity)
            {
                case FunctionEntity f: composite.functions_dictionary.Remove(old); f.shortGUID = id; composite.functions_dictionary.Add(id, f); break;
                case VariableEntity v: composite.variables_dictionary.Remove(old); v.shortGUID = id; composite.variables_dictionary.Add(id, v); break;
                case AliasEntity a: composite.aliases_dictionary.Remove(old); a.shortGUID = id; composite.aliases_dictionary.Add(id, a); break;
                case ProxyEntity p: composite.proxies_dictionary.Remove(old); p.shortGUID = id; composite.proxies_dictionary.Add(id, p); break;
            }
            foreach (Entity other in composite.GetEntities())
                for (int i = 0; i < other.childLinks.Count; i++)
                {
                    if (other.childLinks[i].linkedEntityID != old) continue;
                    EntityConnector link = other.childLinks[i];
                    link.linkedEntityID = id;
                    other.childLinks[i] = link;
                }
            foreach (AliasEntity alias in composite.aliases)
                if (alias.alias?.path != null && alias.alias.path.Length > 0 && alias.alias.path[0] == old) alias.alias.path[0] = id;
            //resources keyed by the entity (an old import gave the skeleton's resource the entity's id) follow it
            if (entity is FunctionEntity function)
                foreach (ResourceReference reference in function.resources)
                    if (reference.resource_id == old) reference.resource_id = id;
        }

        /// <summary>
        /// Put the skeleton entity in shape: the composite's id, the shipped resource id. Returns what changed (empty if nothing).
        /// </summary>
        public static List<string> FixSkeletonEntity(Composite composite)
        {
            List<string> changed = new List<string>();
            FunctionEntity skeleton = SkeletonEntity(composite);
            if (skeleton == null) return changed;
            if (skeleton.shortGUID != composite.shortGUID && composite.GetEntityByID(composite.shortGUID) == null)
            {
                string was = skeleton.shortGUID.ToByteString();
                ChangeEntityId(composite, skeleton, composite.shortGUID);
                changed.Add("skeleton entity " + was + " -> " + composite.shortGUID.ToByteString());
            }
            cResource resource = skeleton.GetParameter(ShortGuids.resource)?.content as cResource;
            if (resource != null && resource.shortGUID != AnimatedModelResourceId)
            {
                string was = resource.shortGUID.ToByteString();
                resource.shortGUID = AnimatedModelResourceId;
                foreach (ResourceReference reference in resource.value.Where(r => r.resource_type == ResourceType.ANIMATED_MODEL))
                    reference.resource_id = AnimatedModelResourceId;
                changed.Add("animated-model resource " + was + " -> 00-E4-14-34");
            }
            return changed;
        }

        /// <summary>
        /// A new animated-model entry for <paramref name="composite"/>: a copy of <paramref name="source"/> (skeleton, inverse binds, helpers)
        /// listing exactly the composite's parts. Not yet in any file and without an id: <see cref="AddEntry"/> files it.
        /// </summary>
        public static EnvironmentAnimations.EnvironmentAnimation OwnEntry(EnvironmentAnimations.EnvironmentAnimation source, Composite composite)
        {
            EnvironmentAnimations.EnvironmentAnimation entry = source.Copy();
            entry.MeshMappings = Parts(composite).Select(o => o.Renderable.resource_id).Distinct().ToList();
            return entry;
        }

        /// <summary>File an entry in the level's ENVIRONMENT_ANIMATION table with a free id (unless it is filed already).</summary>
        public static void AddEntry(EnvironmentAnimations table, EnvironmentAnimations.EnvironmentAnimation entry)
        {
            if (table.Entries.Any(o => ReferenceEquals(o, entry))) return; //by reference: a fresh copy equals its source until it has its own id
            entry.ID = table.AllocateUniqueId();
            table.Entries.Add(entry);
        }

        /// <summary>
        /// Every place the level keys a composite by its id, moved to <paramref name="id"/>: the composite, the instances of it, its pin
        /// and colour rows, its clean-up and modification marks; and a display model's skeleton entity, which carries the composite's id.
        /// </summary>
        public static void ChangeCompositeId(Commands commands, Composite composite, ShortGuid id)
        {
            ShortGuid old = composite.shortGUID;
            if (old == id) return;
            if (commands.GetComposite(id) != null) throw new InvalidOperationException("Another composite already has the id " + id.ToByteString() + ".");
            FunctionEntity skeleton = IsDisplayModel(composite) || composite.functions.Any(f => f.shortGUID == old && f.function == FunctionType.EnvironmentModelReference)
                ? composite.functions.FirstOrDefault(f => f.shortGUID == old && f.function == FunctionType.EnvironmentModelReference) : null;

            List<CompositePinInfoTable.PinInfo> pins = commands.Utils.GetAllCustomPinInfo(old).Where(o => o != null).ToList();
            HashSet<ShortGuid> colours = new HashSet<ShortGuid>(commands.Utils.GetColourParameters(old));
            bool purged = commands.Utils.PurgedComposites.purged.Contains(old);
            CompositeModificationInfoTable.ModificationInfo modified = commands.Utils.GetModificationInfo(old);

            composite.shortGUID = id;
            foreach (Composite other in commands.Entries)
                if (other != null)
                    foreach (FunctionEntity function in other.functions)
                        if (function.function == old) function.function = id;
            if (skeleton != null && composite.GetEntityByID(id) == null) ChangeEntityId(composite, skeleton, id);

            if (pins.Count != 0) { commands.Utils.ReplaceCustomPinInfos(id, pins); commands.Utils.ReplaceCustomPinInfos(old, new List<CompositePinInfoTable.PinInfo>()); }
            if (colours.Count != 0) { commands.Utils.ReplaceColourParameters(id, colours); commands.Utils.ReplaceColourParameters(old, new List<ShortGuid>()); }
            if (purged) { commands.Utils.PurgedComposites.purged.Remove(old); commands.Utils.PurgedComposites.purged.Add(id); }
            if (modified != null)
                commands.Utils.SetModificationInfo(new CompositeModificationInfoTable.ModificationInfo() { composite_id = id, editor_version = modified.editor_version, modification_date = modified.modification_date });
        }
    }
}
