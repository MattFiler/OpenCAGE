using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.ShaderTypes;
using CathodeLib;
using CathodeLib.Ubershaders;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// A SimpleWater draws the game's water plane with a CA_SIMPLEWATER material given to it as its renderable; without one the game
    /// crashes as the level loads (SIMPLE_WATER::update_collision_hulls on a null renderable: GOG AI+0xdc906). <see cref="Ensure"/> gives a
    /// new one the renderable of a SimpleWater the level already has, else the level's required water plane drawn with a CA_SIMPLEWATER
    /// material it has, else one made from the shader database. The build re-points the mesh at the water plane and the entity's own
    /// parameters override the material's constants at runtime, so the material only has to be a water one.
    /// </summary>
    public static class SimpleWaterModel
    {
        public const string MaterialName = "OPENCAGE\\SIMPLEWATER";

        public static bool IsWater(Entity entity) => entity is FunctionEntity f && f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.SimpleWater;

        public static bool Draws(FunctionEntity water) => (water?.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance?.Count ?? 0) != 0;

        /// <summary>
        /// Why a new SimpleWater could not be given a renderable in this level (then it must not be made: it would crash the game), or
        /// null when it can. Changes nothing.
        /// </summary>
        public static string WhyCannot(Level level, string gameRoot)
        {
            if (level?.Commands == null) return "No level is open.";
            if (level.Commands.Entries.Where(o => o != null).SelectMany(o => o.functions).Any(o => IsWater(o) && Draws(o))) return null;
            if (level.Models == null || RequiredModels.Resolve(level.Models, RequiredModels.Model.REQUIRED_MODEL_WATER) == null)
                return "This level has no water plane (its REQUIRED_MODEL_WATER model), so a new SimpleWater would have nothing to draw and would crash the game as the level loads. Port a retail water instead (from PRODUCTION/SCI_HOSPITALLOWER: AYZ\\FX_Library\\Location_Comp\\SCI_HospitalLower\\SCI_HospitalLower_St_3) and copy its SimpleWater.";
            if (level.Materials?.Entries.Any(o => o?.Shader != null && o.Shader.Ubershader == SHADER_LIST.CA_SIMPLEWATER) == true) return null;
            if (level.Materials?.Entries.Any(o => o != null && string.Equals(o.Name, MaterialName, StringComparison.OrdinalIgnoreCase)) == true) return null;
            if (level.Shaders != null && ShaderPermutationService.CreatableFamilies(level.Shaders, gameRoot).Any(o => o.Family == SHADER_LIST.CA_SIMPLEWATER)) return null;
            return "Neither this level nor the game's shader database has a water (CA_SIMPLEWATER) shader, so a new SimpleWater would have no material and would crash the game as the level loads. Port a retail water instead (from PRODUCTION/SCI_HOSPITALLOWER: AYZ\\FX_Library\\Location_Comp\\SCI_HospitalLower\\SCI_HospitalLower_St_3) and copy its SimpleWater.";
        }

        /// <summary>
        /// Give a SimpleWater with no renderable one (see the class). Returns what it was given, in a sentence, or null when it is not a
        /// SimpleWater or already draws. Throws <see cref="InvalidOperationException"/> when the level can give it nothing: then it would
        /// crash the game, and the caller refuses.
        /// </summary>
        public static string Ensure(Level level, FunctionEntity water, string gameRoot)
        {
            if (!IsWater(water) || Draws(water) || level?.Commands == null)
                return null;
            List<RenderableElements.Element> elements;
            string how;
            FunctionEntity donor = level.Commands.Entries.Where(o => o != null).SelectMany(o => o.functions).FirstOrDefault(o => o != water && IsWater(o) && Draws(o));
            if (donor != null)
            {
                elements = new List<RenderableElements.Element>(donor.GetResource(ResourceType.RENDERABLE_INSTANCE, true).RenderableInstance);
                how = "the water material of " + (CommandsUtils.GetEntityNameParameter(donor) ?? "another SimpleWater") + " (" + elements.Count + " element" + (elements.Count == 1 ? "" : "s") + ")";
            }
            else
            {
                Models.CS2.Component.LOD.Submesh plane = level.Models == null ? null : RequiredModels.Resolve(level.Models, RequiredModels.Model.REQUIRED_MODEL_WATER);
                if (plane == null)
                    throw new InvalidOperationException("This level has no water plane (its REQUIRED_MODEL_WATER model), so a SimpleWater would have nothing to draw and crash the game: port a retail water instead (port_composites PRODUCTION/SCI_HOSPITALLOWER AYZ\\FX_Library\\Location_Comp\\SCI_HospitalLower\\SCI_HospitalLower_St_3).");
                Materials.Material material = level.Materials?.Entries.FirstOrDefault(o => o?.Shader != null && o.Shader.Ubershader == SHADER_LIST.CA_SIMPLEWATER);
                if (material != null)
                    how = "the level's water plane with its water material " + level.Materials.GetMaterialName(material);
                else
                {
                    material = level.Materials?.Entries.FirstOrDefault(o => o != null && string.Equals(o.Name, MaterialName, StringComparison.OrdinalIgnoreCase));
                    if (material == null)
                    {
                        if (level.Materials == null || level.Shaders == null)
                            throw new InvalidOperationException("This level's materials or shaders are not loaded, so no water material can be made for a SimpleWater: it would crash the game.");
                        material = ShaderPermutationService.CreateMaterial(level.Materials, level.Shaders, SHADER_LIST.CA_SIMPLEWATER, MaterialName, gameRoot, out string error);
                        if (material == null)
                            throw new InvalidOperationException("No water material can be made here (" + (error ?? "no CA_SIMPLEWATER shader in this level or the shader database") + "), so a SimpleWater would crash the game: port a retail water instead.");
                        if (!level.Materials.Entries.Contains(material))
                            level.Materials.Entries.Add(material);
                        MaterialGenerator.SeedConstantsFromDonor(material, level.Materials, SHADER_LIST.CA_SIMPLEWATER);
                        Singleton.OnResourceModified?.Invoke();
                    }
                    how = "the level's water plane with a water material made for it (" + MaterialName + ")";
                }
                elements = new List<RenderableElements.Element>() { new RenderableElements.Element() { Model = plane, Material = material } };
            }

            cResource resource = water.GetParameter(ShortGuids.resource)?.content as cResource;
            cResource next = resource == null ? new cResource(water.shortGUID) : PhysicsBodyKeys.Rekeyed(resource, resource.shortGUID);
            next.value = next.value.Where(o => o != null && o.resource_type != ResourceType.RENDERABLE_INSTANCE).ToList();
            next.value.Add(new ResourceReference(ResourceType.RENDERABLE_INSTANCE) { resource_id = next.shortGUID, RenderableInstance = elements });
            water.AddParameter(ShortGuids.resource, next, ParameterVariant.INTERNAL, true, bindResourceToEntity: false);
            return "Given " + how + ": without a renderable a SimpleWater crashes the game.";
        }
    }
}
