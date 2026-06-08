using CATHODE;
using CATHODE.EXPERIMENTAL;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static CATHODE.Movers.MOVER_DESCRIPTOR;

namespace OpenCAGE.Scripts
{
    //New local debugging helpers for figuring out instanced resource related stuff

    public static class LocalDebug_NEW
    {
        public static void TestConstantData(string aiPath, string level)
        {
            Level lvl = Utilities.LoadLevel(aiPath, level);

            foreach (var mvr in lvl.Movers.Entries)
            {
                if (mvr.RenderableElements.Count > 0)
                {
                     Console.WriteLine("Model: " + lvl.Models.FindModel(mvr.RenderableElements[0].Model)?.Name);
                     Console.WriteLine("Material: " + mvr.RenderableElements[0].Material?.Name);
                }

                switch (mvr.GetRenderableType())
                {
                    case RenderableInstanceType.CHARACTER:
                        {
                            //gpu stuff is set at runtime based on character info
                            var cpu_constants = mvr.RenderConstants.GetAs<Movers.MOVER_DESCRIPTOR.RENDER_CONSTANTS.MODEL_PARAMS>(); // i think only first two arrays are used here. verify.

                            string gsdsdf = "";
                        }
                        break;
                    case RenderableInstanceType.DYNAMICFX:
                    case RenderableInstanceType.DYNAMICFX_UNIQUE_MAT:
                        {
                            {
                                //For CPU particles
                                var gpu_constants = mvr.GPUConstants.GetAs<Movers.MOVER_DESCRIPTOR.GPU_CONSTANTS.DYNAMIC_FX_GPU_CONSTANTS>();
                                var cpu_constants = mvr.RenderConstants.GetAs<Movers.MOVER_DESCRIPTOR.RENDER_CONSTANTS.DYNAMIC_PFX_PARAMS>();
                            }
                            {
                                //For GPU particles
                                var gpu_constants = mvr.GPUConstants.GetAs<Movers.MOVER_DESCRIPTOR.GPU_CONSTANTS.PARTICLE_GPU_CONSTANTS>();
                                var cpu_constants = mvr.RenderConstants.GetAs<Movers.MOVER_DESCRIPTOR.RENDER_CONSTANTS.PARTICLE_PARAMS>();
                            }

                            string gsdsdf = "";
                        }
                        break;
                    case RenderableInstanceType.ENVIRONMENT_EXTRA:
                    case RenderableInstanceType.ENVIRONMENT:
                        {
                            var gpu_constants = mvr.GPUConstants.GetAs<Movers.MOVER_DESCRIPTOR.GPU_CONSTANTS.ENVIRONMENT_GPU_CONSTANTS>();
                            var cpu_constants = mvr.RenderConstants.GetAs<Movers.MOVER_DESCRIPTOR.RENDER_CONSTANTS.MODEL_PARAMS>(); // i think only first two arrays are used here. verify.
                        }
                        break;
                    case RenderableInstanceType.FOGSPHERE:
                        {
                            var gpu_constants = mvr.GPUConstants.GetAs<Movers.MOVER_DESCRIPTOR.GPU_CONSTANTS.FOGSPHERE_GPU_CONSTANTS>();
                            var cpu_constants = mvr.RenderConstants.GetAs<Movers.MOVER_DESCRIPTOR.RENDER_CONSTANTS.MODEL_PARAMS>(); // i think only first two arrays are used here. verify.

                            string fgsdfd = "";
                        }
                        break;
                    case RenderableInstanceType.LIGHT:
                        {
                            var gpu_constants = mvr.GPUConstants.GetAs<Movers.MOVER_DESCRIPTOR.GPU_CONSTANTS.DEFERRED_GPU_CONSTANTS>();
                            var cpu_constants = mvr.RenderConstants.GetAs<Movers.MOVER_DESCRIPTOR.RENDER_CONSTANTS.DEFERRED_PARAMS>();

                            string gsdsdf = "";
                        }
                        break;
                    case RenderableInstanceType.MISC:
                        {
                            // gpu_constants can be any!!
                            var cpu_constants = mvr.RenderConstants.GetAs<Movers.MOVER_DESCRIPTOR.RENDER_CONSTANTS.MODEL_PARAMS>(); // i think only first two arrays are used here. verify.

                            string fgsdfd = "";
                        }
                        break;
                    case RenderableInstanceType.PLANET:
                        {
                            //these are set at runtime
                        }
                        break;
                }
            }

            foreach (var mat in lvl.Materials.Entries)
            {
                if (mat.Name.StartsWith("FOGSPHERE_"))
                {
                    Directory.CreateDirectory(mat.Name);
                    File.WriteAllBytes(mat.Name + "\\vertex_shader.bin", mat.Shader.VertexShader);
                    File.WriteAllBytes(mat.Name + "\\pixel_shader.bin", mat.Shader.PixelShader);
                }
            }
        }

        //Proof of concept of removing all instanced data from a level, populating the level with only Commands (excluding collisions)
        public static void StripInstancedData(string pathToAI, string level)
        {
            Level lvl = Utilities.LoadLevel(pathToAI, level);

            //Clear out the movers - these are the instanced objects populated from offline data
            lvl.Movers.Entries.Clear();
            lvl.Movers.Save();

            //Strip out radiosity data: this references instanced movers, so we need to get rid of it
            File.WriteAllBytes(pathToAI + "/DATA/ENV/" + level + "/WORLD/RADIOSITY_COLLISION_MAPPING.BIN", new byte[4]);
            File.WriteAllBytes(pathToAI + "/DATA/ENV/" + level + "/RENDERABLE/RADIOSITY_RUNTIME.BIN", new byte[0]);
            File.Delete(pathToAI + "/DATA/ENV/" + level + "/RENDERABLE/RADIOSITY_INSTANCE_MAP.TXT");

            //Strip out light info, again, these point to movers, so get rid
            lvl.Lights.Indexes.Clear();
            lvl.Lights.Values.Clear();

            lvl.Lights.Save();
        }

        /*
        [Obsolete("This function is safe to use but not performant. It's intended for test code only.")]
        public static void DEBUG_DumpAllInstancedStuff(string level, string outputdir)
        {
            Console.WriteLine("Starting " + level);
            Directory.CreateDirectory(outputdir);

            LevelContent content = LevelContent.DEBUG_LoadUnthreadedAndPopulateShortGuids(level);

            /*
            for (int i = 0; i < 12; i++)
            {
                for (int x = 0; x < content.mvr.Entries[i].renderable_element_count; x++)
                {
                    var reds = content.resource.reds.Entries[(int)content.mvr.Entries[i].renderable_element_index + x];

                    var submesh = _CompositeBrowser.Content.Level.Models.GetAtWriteIndex(reds.ModelIndex);
                    var model = _CompositeBrowser.Content.Level.Models.FindModelForSubmesh(submesh);
                    var component = _CompositeBrowser.Content.Level.Models.FindModelComponentForSubmesh(submesh);
                    var lod = _CompositeBrowser.Content.Level.Models.FindModelLODForSubmesh(submesh);
                    var material = _CompositeBrowser.Content.Level.Materials.GetAtWriteIndex(reds.MaterialIndex);

                    Console.WriteLine(level + " [" + i + "] -> " + model.Name + " (" + lod.Name + ") -> " + material.Name);
                }
            }

            return;
            */

        /*
            //correct for the 18 entries we remove
            for (int i = 0; i < 18; i++)
                content.Level.CollisionMaps.Entries.Insert(0, new CollisionMaps.COLLISION_MAPPING());

            List<string> resources_dump = new List<string>();
            int resource_index = -1;

            bool DO_MVR = false;
            bool DO_RESOURCES = true;
            bool DO_COLLISION = true;
            bool DO_PHYSICS = true;

            bool ORDER = false;

            if (DO_MVR)
            {
                if (ORDER)
                    content.Level.Movers.Entries = content.Level.Movers.Entries.OrderBy(o => o.entity.composite_instance_id).ThenBy(o => o.entity.entity_id).ThenBy(o => o.primary_zone_id).ToList();

                foreach (var entry in content.Level.Movers.Entries)
                {
                    resource_index++;

                    (Composite entComp, EntityPath entPath) = content.EditorUtils.GetCompositeFromInstanceID(content.Level.Commands, entry.entity.composite_instance_id);
                    Entity entEnt = entComp?.GetEntityByID(entry.entity.entity_id);
                    (Composite zoneComp1, EntityPath zonePath1, Entity zoneEnt1) = content.EditorUtils.GetZoneFromInstanceID(content.Level.Commands, entry.primary_zone_id);
                    (Composite zoneComp2, EntityPath zonePath2, Entity zoneEnt2) = content.EditorUtils.GetZoneFromInstanceID(content.Level.Commands, entry.secondary_zone_id);

                    string convertedResoureName = "[" + resource_index + "] ";

                    string renderableInfo = content.EditorUtils.PrettyPrintMoverRenderable(entry);
                    if (renderableInfo != "")
                    {
                        convertedResoureName += "\n\t REDS INFO: " + renderableInfo;
                    }

                    bool wroteSomething = false;
                    if (entComp != null)
                    {
                        convertedResoureName += "\n\t Entity Composite: " + entComp.name;
                        wroteSomething = true;
                    }
                    if (entPath != null)
                    {
                        convertedResoureName += "\n\t Entity Instance: " + entPath.ToString(content.Level.Commands, content.Level.Commands.EntryPoints[0], true);
                        wroteSomething = true;
                    }
                    if (entEnt != null && entComp == null)
                    {
                        convertedResoureName += "\n\t Entity Entity: " + entEnt.shortGUID + " -> can't resolve name";
                        wroteSomething = true;
                    }
                    if (entEnt != null && entComp != null)
                    {
                        convertedResoureName += "\n\t Entity Entity: " + entEnt.shortGUID + " -> " + content.Level.Commands.Utils.GetEntityName(entComp, entEnt);
                        wroteSomething = true;
                    }

                    if (!wroteSomething)
                    {
                        convertedResoureName += "\n\t COULDNTRESOLVE - Entity EntityPath stuff: " + entry.entity.entity_id.ToByteString() + " -> " + entry.entity.composite_instance_id.ToByteString();

                        foreach (Composite comp2 in content.Level.Commands.Entries)
                        {
                            Entity ent2 = comp2.GetEntityByID(entry.entity.entity_id);
                            if (ent2 == null) continue;
                            convertedResoureName += "\n\t\t[ENTITY ID FOUND IN " + comp2.name + ": " + ShortGuidUtils.Generate(content.Level.Commands.Utils.GetEntityName(comp2, ent2)) + "]";

                            convertedResoureName += content.EditorUtils.GetAllZonesForEntity(ent2);

                            //?//continue;
                        }
                    }

                    if (zonePath1 != null && zonePath1.path.Length == 2 && zonePath1.path[0] == new ShortGuid("01-00-00-00"))
                    {
                        convertedResoureName += "\n\t Primary Zone: GLOBAL ZONE";
                    }
                    else if (zonePath1 != null && zonePath1.path.Length == 1 && zonePath1.path[0] == new ShortGuid("00-00-00-00"))
                    {
                        convertedResoureName += "\n\t Primary Zone: ZERO ZONE";
                    }
                    else
                    {
                        convertedResoureName += "\n\t Primary Zone: " + entry.primary_zone_id.ToByteString();
                        if (zoneComp1 != null)
                            convertedResoureName += "\n\t Primary Zone Composite: " + zoneComp1.name;
                        if (zonePath1 != null)
                            convertedResoureName += "\n\t Primary Zone Instance: " + zonePath1.ToString(content.Level.Commands, content.Level.Commands.EntryPoints[0], true);
                        if (zoneEnt1 != null && zoneComp1 == null)
                            convertedResoureName += "\n\t Primary Zone Entity: " + zoneEnt1.shortGUID + " -> can't resolve name";
                        if (zoneEnt1 != null && zoneComp1 != null)
                            convertedResoureName += "\n\t Primary Zone Entity: " + zoneEnt1.shortGUID + " -> " + content.Level.Commands.Utils.GetEntityName(zoneComp1, zoneEnt1);
                    }

                    if (zonePath2 != null && zonePath2.path.Length == 2 && zonePath2.path[0] == new ShortGuid("01-00-00-00"))
                    {
                        convertedResoureName += "\n\t Secondary Zone: GLOBAL ZONE";
                    }
                    else if (zonePath2 != null && zonePath2.path.Length == 1 && zonePath2.path[0] == new ShortGuid("00-00-00-00"))
                    {
                        convertedResoureName += "\n\t Secondary Zone: ZERO ZONE";
                    }
                    else
                    {
                        convertedResoureName += "\n\t Secondary Zone: " + entry.secondary_zone_id.ToByteString();
                        if (zoneComp2 != null)
                            convertedResoureName += "\n\t Secondary Zone Composite: " + zoneComp2.name;
                        if (zonePath2 != null)
                            convertedResoureName += "\n\t Secondary Zone Instance: " + zonePath2.ToString(content.Level.Commands, content.Level.Commands.EntryPoints[0], true);
                        if (zoneEnt2 != null && zoneComp2 == null)
                            convertedResoureName += "\n\t Secondary Zone Entity: " + zoneEnt2.shortGUID + " -> can't resolve name";
                        if (zoneEnt2 != null && zoneComp2 != null)
                            convertedResoureName += "\n\t Secondary Zone Entity: " + zoneEnt2.shortGUID + " -> " + content.Level.Commands.Utils.GetEntityName(zoneComp2, zoneEnt2);
                    }

                    resources_dump.Add(convertedResoureName);
                }
                File.WriteAllLines(outputdir + "/mover_dump_" + level.Replace("\\", "_").Replace("/", "_") + ".txt", resources_dump);
            }

            resources_dump.Clear();
            resource_index = -1;

            if (DO_RESOURCES)
            {
                foreach (var entry in content.Level.Resources.Entries)
                {
                    resource_index++;

                    (Composite comp, EntityPath path) = content.EditorUtils.GetCompositeFromInstanceID(content.Level.Commands, entry.composite_instance_id);

                    string convertedResoureName = "[" + resource_index + "] " + entry.resource_id.ToString() + " (" + entry.resource_id.ToByteString() + ") ";
                    if (convertedResoureName != entry.resource_id.ToByteString())
                    {
                        convertedResoureName += " [CONVERTED FROM SHORTGUID]";
                    }
                    else if (comp != null)
                    {
                        Entity ent = comp.GetEntityByID(entry.resource_id);
                        if (ent != null)
                        {
                            convertedResoureName = content.Level.Commands.Utils.GetEntityName(comp, ent);
                            if (convertedResoureName != entry.resource_id.ToByteString())
                            {
                                convertedResoureName += " [CONVERTED FROM ENTITY - " + ent.variant + "]";
                            }
                            else
                            {
                                convertedResoureName += " [COULDN'T RESOLVE, BUT HAS COMP & ENT]";
                            }
                        }
                        else
                        {
                            convertedResoureName += " [COULDN'T RESOLVE, BUT HAS COMP, AND NO ENTITY]";
                        }
                    }
                    else
                    {
                        convertedResoureName += " [COULDN'T RESOLVE]";
                    }

                    convertedResoureName += " -> " + entry.composite_instance_id.ToByteString();

                    // Look at AYZ\CONTROLS\LOCKERDRESSING_A - some very odd stuff there. all the Postit composites are listed with NULL COMPOSITE NAMES, plus a load of resources that aren't there (maybe it's the sub-composite entities? -> i think it is)

                    if (comp == null && path != null)
                    {
                        resources_dump.Add(convertedResoureName + "\n\tINSTANCE: NULL COMPOSITE NAME!! (" + path.ToString() + ")");
                    }
                    else if (comp != null && path == null)
                    {
                        resources_dump.Add(convertedResoureName + "\n\tINSTANCE: " + comp.name + " (NULL PATH !!!!)");
                    }
                    else if (comp == null && path == null)
                    {
                        if (entry.resource_id.ToString() != "Bolt_Gun_Structural_Metal_Decal" && entry.resource_id.ToString() != "AnimatedModel")
                        {
                            foreach (Composite comp2 in content.Level.Commands.Entries)
                            {
                                Entity ent2 = comp2.GetEntityByID(entry.resource_id);
                                if (ent2 == null) continue;
                                convertedResoureName += "\n\t[ENTITY ID FOUND IN " + comp2.name + ": " + ShortGuidUtils.Generate(content.Level.Commands.Utils.GetEntityName(comp2, ent2)) + "]";
                                break;
                            }

                            int ll = 0;
                            foreach (Composite comp2 in content.Level.Commands.Entries)
                            {
                                foreach (FunctionEntity funcEnt in comp2.functions)
                                {
                                    List<ResourceReference> resRefs = funcEnt.resources;
                                    Parameter resParam = funcEnt.GetParameter("resource");
                                    if (resParam != null && resParam.content != null && resParam.content.dataType == DataType.RESOURCE)
                                    {
                                        resRefs.AddRange(((cResource)resParam.content).value);
                                    }

                                    foreach (ResourceReference resRef in resRefs)
                                    {
                                        if (resRef.resource_id == entry.resource_id)
                                        {
                                            convertedResoureName += "\n\t[RESOURCE ID FOUND IN " + comp2.name + ": " + ShortGuidUtils.Generate(content.Level.Commands.Utils.GetEntityName(comp2, funcEnt)) + "] (" + resRef.resource_type + ")";
                                            ll++;
                                            if (ll > 3)
                                            {
                                                convertedResoureName += "\n\t... omitting more results";
                                                break;
                                            }
                                        }
                                    }
                                    if (ll > 3)
                                        break;
                                }
                                if (ll > 3)
                                    break;
                            }
                        }
                        else
                        {
                            convertedResoureName += "\n\tNOTE: skipping " + entry.resource_id.ToString();
                        }


                        resources_dump.Add(convertedResoureName + "\n\tINSTANCE: NULL COMPOSITE NAME!! (NULL PATH !!!!)");
                        //Console.WriteLine("INSTANCE: ??");
                    }
                    else
                    {
                        resources_dump.Add(convertedResoureName + "\n\tINSTANCE: " + comp.name + " (" + path.ToString(content.Level.Commands, content.Level.Commands.EntryPoints[0], true) + ")");

                        //Console.WriteLine("INSTANCE: " + comp.name + " (" + path.GetAsString(content.Level.Commands, content.Level.Commands.EntryPoints[0], true) + ")");
                    }

                }
                File.WriteAllLines(outputdir + "/resources_dump_" + level.Replace("\\", "_").Replace("/", "_") + ".txt", resources_dump);
            }

            resources_dump.Clear();
            resource_index = -1;

            if (DO_COLLISION)
            {
                if (ORDER)
                    content.Level.CollisionMaps.Entries = content.Level.CollisionMaps.Entries.OrderBy(o => o.Entity.composite_instance_id).ThenBy(o => o.Entity.entity_id).ThenBy(o => o.ZoneID).ToList();

                foreach (var entry in content.Level.CollisionMaps.Entries)
                {
                    resource_index++;

                    (Composite entComp, EntityPath entPath) = content.EditorUtils.GetCompositeFromInstanceID(content.Level.Commands, entry.Entity.composite_instance_id);
                    Entity entEnt = entComp?.GetEntityByID(entry.Entity.entity_id);
                    (Composite zoneComp1, EntityPath zonePath1, Entity zoneEnt1) = content.EditorUtils.GetZoneFromInstanceID(content.Level.Commands, entry.ZoneID);

                    string convertedResoureName = "[" + resource_index + "] " + entry.ResourceGUID;

                    if (entComp != null)
                        convertedResoureName += "\n\t Entity Composite: " + entComp.name;
                    if (entPath != null)
                        convertedResoureName += "\n\t Entity Instance: " + entPath.ToString(content.Level.Commands, content.Level.Commands.EntryPoints[0], true);
                    if (entEnt != null && entComp == null)
                        convertedResoureName += "\n\t Entity Entity: " + entEnt.shortGUID + " -> can't resolve name";
                    if (entEnt != null && entComp != null)
                        convertedResoureName += "\n\t Entity Entity: " + entEnt.shortGUID + " -> " + content.Level.Commands.Utils.GetEntityName(entComp, entEnt);

                    if (zonePath1 != null && zonePath1.path.Length == 2 && zonePath1.path[0] == new ShortGuid("01-00-00-00"))
                    {
                        convertedResoureName += "\n\t Primary Zone: GLOBAL ZONE";
                    }
                    else if (zonePath1 != null && zonePath1.path.Length == 1 && zonePath1.path[0] == new ShortGuid("00-00-00-00"))
                    {
                        convertedResoureName += "\n\t Primary Zone: ZERO ZONE";
                    }
                    else
                    {
                        convertedResoureName += "\n\t Primary Zone: " + entry.ZoneID.ToByteString();
                        if (zoneComp1 != null)
                            convertedResoureName += "\n\t Primary Zone Composite: " + zoneComp1.name;
                        if (zonePath1 != null)
                            convertedResoureName += "\n\t Primary Zone Instance: " + zonePath1.ToString(content.Level.Commands, content.Level.Commands.EntryPoints[0], true);
                        if (zoneEnt1 != null && zoneComp1 == null)
                            convertedResoureName += "\n\t Primary Zone Entity: " + zoneEnt1.shortGUID + " -> can't resolve name";
                        if (zoneEnt1 != null && zoneComp1 != null)
                            convertedResoureName += "\n\t Primary Zone Entity: " + zoneEnt1.shortGUID + " -> " + content.Level.Commands.Utils.GetEntityName(zoneComp1, zoneEnt1);
                    }

                    resources_dump.Add(convertedResoureName);
                }
                File.WriteAllLines(outputdir + "/collision_dump_" + level.Replace("\\", "_").Replace("/", "_") + ".txt", resources_dump);
            }

            resources_dump.Clear();
            resource_index = -1;

            if (DO_PHYSICS)
            {
                if (ORDER)
                    content.Level.PhysicsMaps.Entries = content.Level.PhysicsMaps.Entries.OrderBy(o => o.entity.composite_instance_id).ThenBy(o => o.entity.entity_id).ThenBy(o => o.composite_instance_id).ToList();

                foreach (var entry in content.Level.PhysicsMaps.Entries)
                {
                    resource_index++;

                    (Composite entComp, EntityPath entPath) = content.EditorUtils.GetCompositeFromInstanceID(content.Level.Commands, entry.entity.composite_instance_id);
                    Entity entEnt = entComp?.GetEntityByID(entry.entity.entity_id);

                    (Composite entCompParent, EntityPath entPathParent) = content.EditorUtils.GetCompositeFromInstanceID(content.Level.Commands, entry.composite_instance_id);

                    string convertedResoureName = "[" + resource_index + "] " + entry.physics_system_index;

                    if (entComp != null)
                        convertedResoureName += "\n\t Parent Entity Composite: " + entComp.name;
                    if (entPath != null)
                        convertedResoureName += "\n\t Parent Entity Instance: " + entPath.ToString(content.Level.Commands, content.Level.Commands.EntryPoints[0], true);
                    if (entEnt != null && entComp == null)
                        convertedResoureName += "\n\t Parent Entity Entity: " + entEnt.shortGUID + " -> can't resolve name";
                    if (entEnt != null && entComp != null)
                        convertedResoureName += "\n\t Parent Entity Entity: " + entEnt.shortGUID + " -> " + content.Level.Commands.Utils.GetEntityName(entComp, entEnt);

                    if (entCompParent != null)
                        convertedResoureName += "\n\t Composite Composite: " + entCompParent.name;
                    if (entPathParent != null)
                        convertedResoureName += "\n\t Composite Instance: " + entPathParent.ToString(content.Level.Commands, content.Level.Commands.EntryPoints[0], true);

                    resources_dump.Add(convertedResoureName);
                }
                File.WriteAllLines(outputdir + "/physics_dump_" + level.Replace("\\", "_").Replace("/", "_") + ".txt", resources_dump);
            }
        }
        */
    }
}
