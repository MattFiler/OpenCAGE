using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// A composite that places an imported model, built the way the game's own are: one ModelReference
    /// per component drawing its LOD 0 submeshes with their materials and carrying the lower LODs, and,
    /// for a mesh skinned to one of the game's skeletons, the EnvironmentModelReference and
    /// ANIMATED_MODEL entry that make it a DisplayModel a character can wear.
    /// </summary>
    public static class ModelCompositeBuilder
    {
        public const string DisplayModelPrefix = "DisplayModel:";

        public class Result
        {
            public Composite Composite;
            public int ModelReferences;
            public List<string> Notes = new List<string>();
        }

        /// <summary>
        /// The name to offer: the model's own for a static mesh (folders and all), and
        /// "DisplayModel:" plus the model's leaf name for one on a skeleton, since display models are
        /// looked up by that prefix and cannot live in folders.
        /// </summary>
        public static string DefaultName(string modelName, bool displayModel)
        {
            string name = (modelName ?? "").Replace('/', '\\');
            if (name.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            if (!displayModel)
                return name;

            int at = name.LastIndexOf('\\');
            return DisplayModelPrefix + (at >= 0 ? name.Substring(at + 1) : name);
        }

        /// <summary>What is wrong with a composite name, or null when it can be used.</summary>
        public static string Problem(string name, bool displayModel, Commands commands)
        {
            string tidy = Normalise(name);
            if (displayModel)
            {
                if (!tidy.StartsWith(DisplayModelPrefix, StringComparison.Ordinal))
                    return "A display model's name has to start with " + DisplayModelPrefix;
                string rest = tidy.Substring(DisplayModelPrefix.Length);
                if (rest.Length == 0)
                    return "Give it a name after " + DisplayModelPrefix;
                if (rest.IndexOf('\\') >= 0)
                    return "A display model can't live in a folder - the game finds them by name.";
            }
            else
            {
                string problem = AssetName.Problem(tidy);
                if (problem != null)
                    return problem;
            }

            if (commands != null)
            {
                foreach (Composite existing in commands.Entries)
                    if (existing != null && string.Equals(Normalise(existing.name), tidy, StringComparison.OrdinalIgnoreCase))
                        return "A composite is already called that. Pick another name.";
            }
            return null;
        }

        /// <summary>The name as it will be stored: backslashes for folders, no stray whitespace.</summary>
        public static string Normalise(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            //Display model names keep their colon and everything after it verbatim; only the folder form is tidied
            if (name.TrimStart().StartsWith(DisplayModelPrefix, StringComparison.Ordinal))
                return name.Trim();
            return AssetName.Normalise(name);
        }

        /// <summary>
        /// Build the composite. The model must already be in the level's model table; the skeleton is
        /// the game rig the mesh is skinned to, or null for a static model.
        /// </summary>
        public static Result Create(Level level, Models.CS2 model, string compositeName, Skeleton skeleton, string skeletonName)
        {
            Commands commands = level.Commands;
            Composite composite = commands.AddComposite(Normalise(compositeName));
            Result result = new Result { Composite = composite };

            string leaf = LeafName(model.Name);
            List<ShortGuid> renderableIds = new List<ShortGuid>();
            for (int c = 0; c < model.Components.Count; c++)
            {
                Models.CS2.Component component = model.Components[c];
                if (component.LODs.Count == 0 || component.LODs[0].Submeshes.Count == 0)
                    continue;

                FunctionEntity entity = composite.AddFunction(FunctionType.ModelReference);
                commands.Utils.SetEntityName(composite, entity, model.Components.Count == 1 ? leaf : leaf + "_" + c);
                entity.AddParameter("position", new cTransform());

                //The resource is keyed by the entity: AddParameter binds the ids when it lands on "resource"
                cResource resource = new cResource(entity.shortGUID);
                ResourceReference renderable = resource.AddResource(ResourceType.RENDERABLE_INSTANCE);
                foreach (Models.CS2.Component.LOD.Submesh submesh in component.LODs[0].Submeshes)
                    renderable.RenderableInstance.Add(new RenderableElements.Element { Model = submesh, Material = submesh.Material });

                //Lower LODs hang off the run's first element, every submesh of every lower LOD in order,
                //which is how REDS carries them for the game's own characters
                for (int l = 1; l < component.LODs.Count; l++)
                    foreach (Models.CS2.Component.LOD.Submesh submesh in component.LODs[l].Submeshes)
                        renderable.RenderableInstance[0].LODs.Add(new RenderableElements.Element { Model = submesh, Material = submesh.Material });

                //Registered in REDS now rather than left to the save: a run REDS does not know is written
                //as an empty one, and the viewport looks entities up by their REDS index as well
                renderable.RenderableInstance = level.RenderableElements.EnsureRegistered(renderable.RenderableInstance);

                entity.AddParameter("resource", resource);
                renderableIds.Add(renderable.resource_id);
                result.ModelReferences++;
            }

            if (skeleton != null)
            {
                FunctionEntity reference = composite.AddFunction(FunctionType.EnvironmentModelReference);
                commands.Utils.SetEntityName(composite, reference, skeletonName);
                reference.AddParameter("name", new cString(skeletonName));

                string how;
                EnvironmentAnimations.EnvironmentAnimation entry = level.EnvironmentAnimations.AddForSkeleton(skeletonName, skeleton, Singleton.Animations, OtherLevels(level), renderableIds, out how);

                cResource resource = new cResource(reference.shortGUID);
                resource.AddResource(ResourceType.ANIMATED_MODEL).AnimatedModel = entry;
                reference.AddParameter("resource", resource);

                result.Notes.Add("Animated model " + entry.ID + " on skeleton " + skeletonName + ", " + how + ".");
            }

            return result;
        }

        /// <summary>
        /// Where an exact per-bone table might already exist beyond this level: every other shipped
        /// level's environment animations, each read only if the search gets that far.
        /// </summary>
        private static IEnumerable<EnvironmentAnimations> OtherLevels(Level level)
        {
            string root = Singleton.PathToAI + "/DATA/ENV/PRODUCTION";
            if (!Directory.Exists(root))
                yield break;

            string own = Path.GetFullPath(level.EnvironmentAnimations?.Filepath ?? "");
            foreach (string directory in Directory.GetDirectories(root))
            {
                string file = Path.Combine(directory, "WORLD", "ENVIRONMENT_ANIMATION.DAT");
                if (!File.Exists(file) || string.Equals(Path.GetFullPath(file), own, StringComparison.OrdinalIgnoreCase))
                    continue;

                EnvironmentAnimations loaded = null;
                try
                {
                    loaded = new EnvironmentAnimations(file, Singleton.Global?.AnimationStrings_Debug);
                }
                catch
                {
                    //An unreadable level is no reason to give up on the rest
                }
                if (loaded != null && loaded.Loaded)
                    yield return loaded;
            }
        }

        private static string LeafName(string modelName)
        {
            string name = (modelName ?? "").Replace('/', '\\');
            if (name.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            int at = name.LastIndexOf('\\');
            return at >= 0 ? name.Substring(at + 1) : name;
        }
    }
}
