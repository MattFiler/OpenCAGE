using CATHODE;
using CATHODE.EXPERIMENTAL;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using OpenCAGE.DockPanels;
using OpenCAGE.Scripts;
using OpenCAGE.UserControls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.Design;
using System.Windows.Media.Animation;
using System.Xml.Linq;
using WebSocketSharp;
using static CATHODE.CollisionMaps;
using static CATHODE.Models;
using static CATHODE.Movers;
using static CathodeLib.CathodeEnumTable;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE
{
    public static class LocalDebug
    {
        public static void CheckMVRZones(string lvl)
        {
            Singleton.Global = new Global(Singleton.PathToAI + "\\DATA\\ENV\\GLOBAL\\", new PAK2(Singleton.PathToAI + "/DATA/GLOBAL/ANIMATION.PAK"));
            LevelContent content = new LevelContent(lvl);
            content.Load();
            content.EditorUtils = new EditorUtils(content);
            content.EditorUtils.GenerateEntityNameCache(Singleton.Editor);
            content.EditorUtils.GenerateCompositeInstances(content.Level.Commands, false);
            List<string> dump = new List<string>();
            foreach (Movers.MOVER_DESCRIPTOR mvr in content.Level.Movers.Entries)
            {
                if (mvr.SecondaryZoneID == ShortGuid.Invalid)
                    continue;

                dump.Add(mvr.RenderableElements.Count > 0 && mvr.RenderableElements[0].Model != null ? content.Level.Models.FindModel(mvr.RenderableElements[0].Model)?.Name : "UNKNOWN MODEL");
                dump[dump.Count - 1] += " (" + (mvr.RenderableElements.Count > 0 && mvr.RenderableElements[0].Material != null ? mvr.RenderableElements[0].Material?.Name : "UNKNOWN MATERIAL") + ")";

                var ZONE1 = content.EditorUtils.GetZoneFromInstanceID(content.Level.Commands, mvr.PrimaryZoneID).Item3;
                var ZONE2 = content.EditorUtils.GetZoneFromInstanceID(content.Level.Commands, mvr.SecondaryZoneID).Item3;

                dump.Add("\t Primary Zone ID: " + mvr.PrimaryZoneID + " (" + (ZONE1 == null ? "UNKNOWN NAME" : ((cString)ZONE1.GetParameter("name").content).value) + ")");
                dump.Add("\t Secondary Zone ID: " + mvr.SecondaryZoneID + " (" + (ZONE1 == null ? "UNKNOWN NAME" : ((cString)ZONE2.GetParameter("name").content).value) + ")");
            }
            File.WriteAllLines("MoversDumpZones.txt", dump);
        }

        public static void CheckWriteInstanced()
        {
#if DEBUG
            string levelToTest = "production/tech_rnd_hzdlab";

            Level lvll = Utilities.LoadLevel(Singleton.PathToAI, levelToTest);

            uint toFind = new ShortGuid("D9-FA-49-01").AsUInt32;
            foreach (Composite composite in lvll.Commands.Entries)
            {
                if (composite.functions.FirstOrDefault(o => o.shortGUID.AsUInt32 == toFind) != null)
                {
                    var sdfsdf = composite.functions.FirstOrDefault(o => o.shortGUID.AsUInt32 == toFind);

                    string fgsgdf = lvll.Commands.Utils.GetEntityName(composite, sdfsdf);

                    string gsdfsd = "";
                }
            }

            try { File.Delete("log.txt"); } catch { }
            try { File.Delete("log2.txt"); } catch { }

            for (int i = 0; i < lvll.Commands.Entries.Count; i++)
                lvll.Commands.Utils.PurgeDeadLinks(lvll.Commands.Entries[i]);
            for (int i = 0; i < lvll.Commands.Entries.Count; i++)
                lvll.Commands.Utils.PurgeDeadLinks(lvll.Commands.Entries[i]);

            lvll.PhysicsMaps.Save("PHYSICS.MAP_orig");
            lvll.PhysicsMaps.Entries.Sort();
            var physEntries = lvll.PhysicsMaps.Entries.Select(e => new PhysMapEntry(e)).ToList();
            File.WriteAllText("physics.map_orig.json", JsonConvert.SerializeObject(physEntries, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));

            lvll.CollisionMaps.Save("COLLISION.MAP_orig");
            var colEntries = lvll.CollisionMaps.Entries.Select(e => new ColMapEntry(e)).ToList();
            colEntries.Sort();
            File.WriteAllText("COLLISION.map_orig.json", JsonConvert.SerializeObject(colEntries, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));

            List<string> colmaps = new List<string>();
            foreach (var entry in colEntries)
            {
                bool found = false;
                foreach (var comp in lvll.Commands.Entries)
                {
                    Entity ent = comp.GetEntityByID(entry.Entity.entity_id);
                    if (ent != null)
                    {
                        colmaps.Add(lvll.Commands.Utils.GetEntityName(comp, ent) + " in " + comp.name);
                        found = true;
                        break;
                    }
                }
                if (!found)
                    colmaps.Add("Could not find entity: " + entry.Entity.entity_id.ToByteString());
            }
            File.WriteAllLines("colmaps_orig.txt", colmaps);

            lvll.Resources.Entries.Clear();
            lvll.PhysicsMaps.Entries.Clear();
            lvll.CollisionMaps.Entries.Clear();
            lvll.Movers.Entries.Clear();

            Instancing inst = new Instancing(lvll);
            inst.GenerateInstances();
            inst.ProcessInstances();

            lvll.PhysicsMaps.Save("PHYSICS.MAP");
            lvll.PhysicsMaps.Entries.Sort();
            physEntries = lvll.PhysicsMaps.Entries.Select(e => new PhysMapEntry(e)).ToList();
            File.WriteAllText("physics.map.json", JsonConvert.SerializeObject(physEntries, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));

            lvll.CollisionMaps.Save("COLLISION.MAP");
            colEntries = lvll.CollisionMaps.Entries.Select(e => new ColMapEntry(e)).ToList();
            colEntries.Sort();
            File.WriteAllText("COLLISION.map.json", JsonConvert.SerializeObject(colEntries, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));

            colmaps = new List<string>();
            foreach (var entry in colEntries)
            {
                bool found = false;
                foreach (var comp in lvll.Commands.Entries)
                {
                    Entity ent = comp.GetEntityByID(entry.Entity.entity_id);
                    if (ent != null)
                    {
                        colmaps.Add(lvll.Commands.Utils.GetEntityName(comp, ent) + " in " + comp.name);
                        found = true;
                        break;
                    }
                }
                if (!found)
                    colmaps.Add("Could not find entity: " + entry.Entity.entity_id.ToByteString());
            }
            File.WriteAllLines("colmaps.txt", colmaps);

            string[] ourLog = !File.Exists("log.txt") ? new string[0] : File.ReadAllLines("log.txt");

            //try { File.Delete(repoDir + "\\Content\\Build\\Levels\\" + levelToTest + "\\debug_log.txt"); } catch { }

            var ourLogSorted = ourLog.ToList();
            ourLogSorted.Sort();
            ourLogSorted.RemoveAll(o => o == "");
            for (int i = 0; i < ourLogSorted.Count; i++)
            {
                //ourLogSorted[i] = ourLogSorted[i].Split('-')[2];
            }

            File.WriteAllLines("our_log.txt", ourLogSorted);

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
#endif
        }

        public static void GetExclusiveMasters(string level)
        {
            LevelContent content = LevelContent.DEBUG_LoadUnthreadedAndPopulateShortGuids(level);
            for (int i = 1; i < content.Level.StateResources.Count; i++)
            {
                (Composite comp, EntityPath path) = content.EditorUtils.GetCompositeFromInstanceID(content.Level.Commands, content.Level.StateResources[i].Resource.composite_instance_id);
                Entity ent = comp.GetEntityByID(content.Level.StateResources[i].Resource.resource_id);
                Console.WriteLine(comp.name + " -> " + content.Level.Commands.Utils.GetEntityName(comp, ent));
            }
        }

        public static void SanityCheckResources()
        {
            Directory.Delete("RESOURCES", true);

            Directory.CreateDirectory("RESOURCES/ORIG");
            Directory.CreateDirectory("RESOURCES/NEW");

            //SanityCheckResourcesInternal("bsp_lv426_pt01");
            //SanityCheckResourcesInternal("BSP_LV426_PT02");
            //SanityCheckResourcesInternal("BSP_Torrens");
            //SanityCheckResourcesInternal("eng_alien_nest");
            //SanityCheckResourcesInternal("eng_reactorcore");
            //SanityCheckResourcesInternal("ENG_TOWPLATFORM");
            //SanityCheckResourcesInternal("FRONTEND");
            //SanityCheckResourcesInternal("HAB_AIRPORT");
            //SanityCheckResourcesInternal("HAB_CORPORATEPENT");
            //SanityCheckResourcesInternal("HAB_SHOPPINGCENTRE");
            //SanityCheckResourcesInternal("sci_androidlab");
            //SanityCheckResourcesInternal("SCI_HOSPITALLOWER");
            //SanityCheckResourcesInternal("sci_hospitalupper");
            //SanityCheckResourcesInternal("sci_hub");
            //SanityCheckResourcesInternal("solace");
            //SanityCheckResourcesInternal("TECH_COMMS");
            //SanityCheckResourcesInternal("TECH_HUB");
            //SanityCheckResourcesInternal("TECH_MUTHRCORE");
            //SanityCheckResourcesInternal("TECH_RND");
            SanityCheckResourcesInternal("tech_rnd_hzdlab");
            SanityCheckResourcesInternal("dlc/CHALLENGEMAP1");
            SanityCheckResourcesInternal("dlc/CHALLENGEMAP3");
            SanityCheckResourcesInternal("dlc/CHALLENGEMAP4");
            SanityCheckResourcesInternal("dlc/CHALLENGEMAP5");
            SanityCheckResourcesInternal("dlc/CHALLENGEMAP7");
            SanityCheckResourcesInternal("dlc/CHALLENGEMAP9");
            SanityCheckResourcesInternal("dlc/CHALLENGEMAP11");
            SanityCheckResourcesInternal("dlc/CHALLENGEMAP12");
            SanityCheckResourcesInternal("dlc/CHALLENGEMAP14");
            SanityCheckResourcesInternal("dlc/CHALLENGEMAP16");
            SanityCheckResourcesInternal("dlc/SALVAGEMODE1");
            SanityCheckResourcesInternal("dlc/SALVAGEMODE2");
        }
        public static void SanityCheckResourcesInternal(string level)
        {
            Singleton.PathToAI = "C:\\AlienData\\game\\pc\\";

            Level lvll = Utilities.LoadLevel(Singleton.PathToAI, "Production/" + level);

            lvll.Resources.Entries.Sort();
            File.WriteAllText("RESOURCES/ORIG/" + level.Replace("/", "_") + ".json", JsonConvert.SerializeObject(lvll.Resources.Entries, Formatting.Indented, new ShortGuidConverter()));

            for (int i = 0; i < lvll.Commands.Entries.Count; i++)
                lvll.Commands.Utils.PurgeDeadLinks(lvll.Commands.Entries[i]);
            for (int i = 0; i < lvll.Commands.Entries.Count; i++)
                lvll.Commands.Utils.PurgeDeadLinks(lvll.Commands.Entries[i]);

            Instancing inst = new Instancing(lvll);
            inst.GenerateInstances();

           // var entries = lvll.Resources.Entries.Copy();
           lvll.Resources.Entries.Clear();
            inst.ProcessInstances();

          //  if (entries.Count != lvll.Resources.Entries.Count)
          //  {
          //      string gsdfds = "";
          //  }

            lvll.Resources.Entries.Sort();
            File.WriteAllText("RESOURCES/NEW/" + level.Replace("/", "_") + ".json", JsonConvert.SerializeObject(lvll.Resources.Entries, Formatting.Indented, new ShortGuidConverter()));

            if (!File.Exists(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/" + level + "/WORLD/RESOURCES_old.BIN"))
                File.Copy(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/" + level + "/WORLD/RESOURCES.BIN", Singleton.PathToAI + "/DATA/ENV/PRODUCTION/" + level + "/WORLD/RESOURCES_old.BIN");

            PAK2 animPAK = new PAK2(Singleton.PathToAI + "\\DATA\\GLOBAL\\ANIMATION.PAK");
            LocalDebug_NEW.StripInstancedData(Singleton.PathToAI, "Production/" + level);
            lvll.Resources.Save();

            //List<string> bruh = new List<string>();
            //foreach (FunctionType func in Instancing.funcz)
            //{
            //    bruh.Add(func.ToString());
            //}
            //bruh.Sort();
            //File.WriteAllLines(level + ".txt", bruh);

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
        }

        public static void SanityCheckColMaps()
        {
            Directory.Delete("COLMAPS", true);

            Directory.CreateDirectory("COLMAPS/ORIG");
            Directory.CreateDirectory("COLMAPS/NEW");

            SanityCheckColMapsInternal("bsp_lv426_pt01");
            SanityCheckColMapsInternal("BSP_LV426_PT02");
            SanityCheckColMapsInternal("BSP_Torrens");
            SanityCheckColMapsInternal("eng_alien_nest");
            SanityCheckColMapsInternal("eng_reactorcore");
            SanityCheckColMapsInternal("ENG_TOWPLATFORM");
            SanityCheckColMapsInternal("FRONTEND");
            SanityCheckColMapsInternal("HAB_AIRPORT");
            SanityCheckColMapsInternal("HAB_CORPORATEPENT");
            SanityCheckColMapsInternal("HAB_SHOPPINGCENTRE");
            SanityCheckColMapsInternal("sci_androidlab");
            SanityCheckColMapsInternal("SCI_HOSPITALLOWER");
            SanityCheckColMapsInternal("sci_hospitalupper");
            SanityCheckColMapsInternal("sci_hub");
            SanityCheckColMapsInternal("solace");
            SanityCheckColMapsInternal("TECH_COMMS");
            SanityCheckColMapsInternal("TECH_HUB");
            SanityCheckColMapsInternal("TECH_MUTHRCORE");
            SanityCheckColMapsInternal("TECH_RND");
            SanityCheckColMapsInternal("tech_rnd_hzdlab");
            SanityCheckColMapsInternal("dlc/CHALLENGEMAP1");
            SanityCheckColMapsInternal("dlc/CHALLENGEMAP3");
            SanityCheckColMapsInternal("dlc/CHALLENGEMAP4");
            SanityCheckColMapsInternal("dlc/CHALLENGEMAP5");
            SanityCheckColMapsInternal("dlc/CHALLENGEMAP7");
            SanityCheckColMapsInternal("dlc/CHALLENGEMAP9");
            SanityCheckColMapsInternal("dlc/CHALLENGEMAP11");
            SanityCheckColMapsInternal("dlc/CHALLENGEMAP12");
            SanityCheckColMapsInternal("dlc/CHALLENGEMAP14");
            SanityCheckColMapsInternal("dlc/CHALLENGEMAP16");
            SanityCheckColMapsInternal("dlc/SALVAGEMODE1");
            SanityCheckColMapsInternal("dlc/SALVAGEMODE2");
        }
        public static void SanityCheckColMapsInternal(string level)
        {
            Level lvll = Utilities.LoadLevel(Singleton.PathToAI, "Production/" + level);

            lvll.CollisionMaps.Entries.Sort();
            var newEntries = lvll.CollisionMaps.Entries.OrderBy(o => o.ResourceGUID).ThenBy(o => o.Entity.entity_id).ThenBy(o => o.Entity.composite_instance_id).ThenBy(o => o.Index).ThenBy(o => o.CollisionProxyIndex).ToList().Select(e => new ColMapEntry(e)).ToList();
            File.WriteAllText("COLMAPS/ORIG/" + level.Replace("/", "_") + ".json", JsonConvert.SerializeObject(newEntries, Formatting.Indented, new ShortGuidConverter()));

            for (int i = 0; i < lvll.Commands.Entries.Count; i++)
                lvll.Commands.Utils.PurgeDeadLinks(lvll.Commands.Entries[i]);
            for (int i = 0; i < lvll.Commands.Entries.Count; i++)
                lvll.Commands.Utils.PurgeDeadLinks(lvll.Commands.Entries[i]);

            Instancing inst = new Instancing(lvll);
            inst.GenerateInstances();
            lvll.CollisionMaps.Entries.Clear();
            inst.ProcessInstances();

            newEntries = lvll.CollisionMaps.Entries.OrderBy(o => o.ResourceGUID).ThenBy(o => o.Entity.entity_id).ThenBy(o => o.Entity.composite_instance_id).ThenBy(o => o.Index).ThenBy(o => o.CollisionProxyIndex).ToList().Select(e => new ColMapEntry(e)).ToList();
            File.WriteAllText("COLMAPS/NEW/" + level.Replace("/", "_") + ".json", JsonConvert.SerializeObject(newEntries, Formatting.Indented, new ShortGuidConverter()));

            //lvll.CollisionMaps.Save();

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
        }

        //for writing out 
        public class ColMapEntry : IComparable<ColMapEntry>
        {
            //public CollisionFlags Flags = 0;
            //public int Index = -1; //Compound shape index for static and ballistic collision 

            //public ShortGuid ResourceGUID = ShortGuid.Invalid; //This is the name of the entity hashed via ShortGuid
            public EntityHandle Entity = new EntityHandle();

            //public string MaterialName = null;

            //public int CollisionProxyIndex = -1; // Index in COLLISION.HKX (hkpStaticCompoundShape)
            //public string MaterialMappingName = null; //This remaps the material to the physics material for Havok

            //public ShortGuid ZoneID = ShortGuid.Invalid; //this maps the entity to a zone ID. interestingly, this seems to be the point of truth for the zone rendering

            public ColMapEntry(CollisionMaps.COLLISION_MAPPING entry)
            {
                //Flags = entry.Flags;
                //Index = entry.Index;
                //ResourceGUID = entry.ResourceGUID;
                Entity = entry.Entity;
                //MaterialName = entry?.Material?.Name;
                //CollisionProxyIndex = entry.CollisionProxyIndex;
                //MaterialMappingName = entry.MaterialMapping?.Name;
                //ZoneID = entry.ZoneID;
            }

            public int CompareTo(ColMapEntry other)
            {
                if (other == null) return 1;

                //int resourceGuidComparison = ResourceGUID.CompareTo(other.ResourceGUID);
                //if (resourceGuidComparison != 0) return resourceGuidComparison;

                int entityIdComparison = Entity.entity_id.CompareTo(other.Entity.entity_id);
                if (entityIdComparison != 0) return entityIdComparison;

                int compositeInstanceIdComparison = Entity.composite_instance_id.CompareTo(other.Entity.composite_instance_id);
                /*if (compositeInstanceIdComparison != 0)*/ return compositeInstanceIdComparison;

                //int indexComparison = Index.CompareTo(other.Index);
                //if (indexComparison != 0) return indexComparison;

                //compare zone
                //compare flags
                //compare mat mapping
                //compare mat name

                //return CollisionProxyIndex.CompareTo(other.CollisionProxyIndex);
            }
        }

        public static void SanityCheckPhysMaps()
        {
            Directory.Delete("PHYSMAPS", true);

            Directory.CreateDirectory("PHYSMAPS/ORIG");
            Directory.CreateDirectory("PHYSMAPS/NEW");

            //SanityCheckPhysMapsInternal("bsp_lv426_pt01");
            //SanityCheckPhysMapsInternal("BSP_LV426_PT02");
            SanityCheckPhysMapsInternal("BSP_Torrens");
            SanityCheckPhysMapsInternal("eng_alien_nest");
            SanityCheckPhysMapsInternal("eng_reactorcore");
            SanityCheckPhysMapsInternal("ENG_TOWPLATFORM");
            SanityCheckPhysMapsInternal("FRONTEND");
            SanityCheckPhysMapsInternal("HAB_AIRPORT");
            SanityCheckPhysMapsInternal("HAB_CORPORATEPENT");
            SanityCheckPhysMapsInternal("HAB_SHOPPINGCENTRE");
            SanityCheckPhysMapsInternal("sci_androidlab");
            SanityCheckPhysMapsInternal("SCI_HOSPITALLOWER");
            SanityCheckPhysMapsInternal("sci_hospitalupper");
            SanityCheckPhysMapsInternal("sci_hub");
            SanityCheckPhysMapsInternal("solace");
            SanityCheckPhysMapsInternal("TECH_COMMS");
            SanityCheckPhysMapsInternal("TECH_HUB");
            SanityCheckPhysMapsInternal("TECH_MUTHRCORE");
            SanityCheckPhysMapsInternal("TECH_RND");
            SanityCheckPhysMapsInternal("tech_rnd_hzdlab");
            SanityCheckPhysMapsInternal("dlc/CHALLENGEMAP1");
            SanityCheckPhysMapsInternal("dlc/CHALLENGEMAP3");
            SanityCheckPhysMapsInternal("dlc/CHALLENGEMAP4");
            SanityCheckPhysMapsInternal("dlc/CHALLENGEMAP5");
            SanityCheckPhysMapsInternal("dlc/CHALLENGEMAP7");
            SanityCheckPhysMapsInternal("dlc/CHALLENGEMAP9");
            SanityCheckPhysMapsInternal("dlc/CHALLENGEMAP11");
            SanityCheckPhysMapsInternal("dlc/CHALLENGEMAP12");
            SanityCheckPhysMapsInternal("dlc/CHALLENGEMAP14");
            SanityCheckPhysMapsInternal("dlc/CHALLENGEMAP16");
            SanityCheckPhysMapsInternal("dlc/SALVAGEMODE1");
            SanityCheckPhysMapsInternal("dlc/SALVAGEMODE2");
        }
        public static void SanityCheckPhysMapsInternal(string level)
        {
            Level lvll = Utilities.LoadLevel(Singleton.PathToAI, "Production/" + level);

            lvll.PhysicsMaps.Entries.Sort();
            var origEntries = lvll.PhysicsMaps.Entries.Select(e => new PhysMapEntry(e)).ToList();
            File.WriteAllText("PHYSMAPS/ORIG/" + level.Replace("/", "_") + ".json", JsonConvert.SerializeObject(origEntries, Formatting.Indented, new ShortGuidConverter()));

            for (int i = 0; i < lvll.Commands.Entries.Count; i++)
                lvll.Commands.Utils.PurgeDeadLinks(lvll.Commands.Entries[i]);
            for (int i = 0; i < lvll.Commands.Entries.Count; i++)
                lvll.Commands.Utils.PurgeDeadLinks(lvll.Commands.Entries[i]);

            Instancing inst = new Instancing(lvll);
            inst.GenerateInstances();
            lvll.PhysicsMaps.Entries.Clear();
            inst.ProcessInstances();

            lvll.PhysicsMaps.Entries.Sort();
            var newEntries = lvll.PhysicsMaps.Entries.Select(e => new PhysMapEntry(e)).ToList();
            File.WriteAllText("PHYSMAPS/NEW/" + level.Replace("/", "_") + ".json", JsonConvert.SerializeObject(newEntries, Formatting.Indented, new ShortGuidConverter()));

            //lvll.PhysicsMaps.Save();

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
        }

        //for writing out 
        public class PhysMapEntry
        {
            public int physics_system_index;
            //public string resource_type;
            public string composite_instance_id;
            public EntityHandle entity;
            public RoundedVec Position;
            //todo - i think the rotation calculation may be incorrect

            public PhysMapEntry(PhysicsMaps.DYNAMIC_PHYSICS_SYSTEM entry)
            {
                physics_system_index = entry.physics_system_index;
                composite_instance_id = entry.composite_instance_id.ToByteString();
                entity = entry.entity;
                Position = new RoundedVec(entry.Position);
            }

            public class RoundedVec
            {
                public float X;
                public float Y;
                public float Z;

                public RoundedVec(Vector3 v)
                {
                    X = (float)Math.Round(v.X, 2);
                    Y = (float)Math.Round(v.Y, 2);
                    Z = (float)Math.Round(v.Z, 2);
                }
            }
        }

        public static void SanityCheckResourceTransforms()
        {
            SanityCheckResourceTransformsInternal("Production/bsp_lv426_pt01");
            SanityCheckResourceTransformsInternal("Production/BSP_LV426_PT02");
            SanityCheckResourceTransformsInternal("Production/BSP_Torrens");
            SanityCheckResourceTransformsInternal("Production/eng_alien_nest");
            SanityCheckResourceTransformsInternal("Production/eng_reactorcore");
            SanityCheckResourceTransformsInternal("Production/ENG_TOWPLATFORM");
            SanityCheckResourceTransformsInternal("Production/FRONTEND");
            SanityCheckResourceTransformsInternal("Production/HAB_AIRPORT");
            SanityCheckResourceTransformsInternal("Production/HAB_CORPORATEPENT");
            SanityCheckResourceTransformsInternal("Production/HAB_SHOPPINGCENTRE");
            SanityCheckResourceTransformsInternal("Production/sci_androidlab");
            SanityCheckResourceTransformsInternal("Production/SCI_HOSPITALLOWER");
            SanityCheckResourceTransformsInternal("Production/sci_hospitalupper");
            SanityCheckResourceTransformsInternal("Production/sci_hub");
            SanityCheckResourceTransformsInternal("Production/solace");
            SanityCheckResourceTransformsInternal("Production/TECH_COMMS");
            SanityCheckResourceTransformsInternal("Production/TECH_HUB");
            SanityCheckResourceTransformsInternal("Production/TECH_MUTHRCORE");
            SanityCheckResourceTransformsInternal("Production/TECH_RND");
            SanityCheckResourceTransformsInternal("Production/tech_rnd_hzdlab");
            SanityCheckResourceTransformsInternal("Production/dlc/CHALLENGEMAP1");
            SanityCheckResourceTransformsInternal("Production/dlc/CHALLENGEMAP3");
            SanityCheckResourceTransformsInternal("Production/dlc/CHALLENGEMAP4");
            SanityCheckResourceTransformsInternal("Production/dlc/CHALLENGEMAP5");
            SanityCheckResourceTransformsInternal("Production/dlc/CHALLENGEMAP7");
            SanityCheckResourceTransformsInternal("Production/dlc/CHALLENGEMAP9");
            SanityCheckResourceTransformsInternal("Production/dlc/CHALLENGEMAP11");
            SanityCheckResourceTransformsInternal("Production/dlc/CHALLENGEMAP12");
            SanityCheckResourceTransformsInternal("Production/dlc/CHALLENGEMAP14");
            SanityCheckResourceTransformsInternal("Production/dlc/CHALLENGEMAP16");
            SanityCheckResourceTransformsInternal("Production/dlc/SALVAGEMODE1");
            SanityCheckResourceTransformsInternal("Production/dlc/SALVAGEMODE2");
        }
        private static void SanityCheckResourceTransformsInternal(string level)
        {
            Level lvl = Utilities.LoadLevel(Singleton.PathToAI, level);
            foreach (Composite composite in lvl.Commands.Entries)
            {
                foreach (FunctionEntity function in composite.functions)
                {
                    Parameter p = function.GetParameter("position");
                    cTransform t;
                    if (p != null)
                        t = (cTransform)p.content;
                    else
                        t = new cTransform();

                    foreach (ResourceReference resource in function.resources)
                    {
                        if (resource.resource_type != ResourceType.DYNAMIC_PHYSICS_SYSTEM)
                            continue;

                        if (t.position != resource.position)
                        {
                            string sdfsdfffffd = "";
                        }
                        if (t.rotation != resource.rotation)
                        {
                            string sdfsdffddfffd = "";
                        }
                    }

                    Parameter pR = function.GetParameter("resource");
                    if (pR?.content?.dataType == DataType.RESOURCE)
                    {
                        foreach (ResourceReference resource in ((cResource)pR.content).value)
                        {
                            if (resource.resource_type != ResourceType.DYNAMIC_PHYSICS_SYSTEM)
                                continue;

                            if (t.position != resource.position)
                            {
                                string sdfsdfffffd = "";
                            }
                            if (t.rotation != resource.rotation)
                            {
                                string sdfsdffddfffd = "";
                            }
                        }
                    }
                }
            }
        }

#if NO
        public static void CheckFlowgraphsNew()
        {
            string env = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Alien Isolation\\DATA\\ENV";
            if (Directory.Exists(env + "/PRODUCTION/DLC/BSPNOSTROMO_RIPLEY/WORLD"))
                Directory.Delete(env + "/PRODUCTION/DLC/BSPNOSTROMO_RIPLEY/WORLD", true);
            if (Directory.Exists(env + "/PRODUCTION/DLC/BSPNOSTROMO_TWOTWEAMS/WORLD"))
                Directory.Delete(env + "/PRODUCTION/DLC/BSPNOSTROMO_TWOTWEAMS/WORLD", true);
            List<string> files = Directory.GetFiles(env, "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            List<string> output = new List<string>();
            foreach (string file in files)
            {
                output.Add("Unsupported in " + file);
                Commands cmd = new Commands(file);
                FlowgraphLayoutManager.LinkCommands(cmd);
                foreach (Composite comp in cmd.Entries)
                {
                    cmd.Utils.PurgeDeadLinks(comp);
                    cmd.Utils.PurgeDeadLinks(comp);
                }
                foreach (Composite comp in cmd.Entries)
                {
                    cmd.Utils.PurgeDeadLinks(comp);
                    cmd.Utils.PurgeDeadLinks(comp);
                }
                foreach (Composite comp in cmd.Entries)
                {
                    cmd.Utils.PurgeDeadLinks(comp);
                    FlowgraphLayoutManager.EvaluateCompatibility(comp);
                    if (!FlowgraphLayoutManager.IsCompatible(comp))
                    {
                        output.Add("\t - " + comp.name);
                    }
                }
                output.Add("");
            }
            File.WriteAllLines("unsupported.log", output);
        }

        public static void ProxyTester()
        {
            Directory.Delete("ProxyTest", true);
            List<string> files = Directory.GetFiles("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Alien Isolation\\DATA\\ENV", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            foreach (string file in files)
            {
                bool warn = false;
                Commands commandsRetail = new Commands(file);
                List<string> output = new List<string>();
                foreach (Composite comp in commandsRetail.Entries)
                {
                    foreach (ProxyEntity p in comp.proxies)
                    {
                        output.Add("***************************");
                        output.Add("New Proxy Found:");
                        for (int i = 0; i < p.proxy.path.Length; i++)
                        {
                            output.Add("[" + i + "] " + p.proxy.path[i].ToByteString());
                        }

                        output.Add("");

                        //Entity pointedE = commandsRetail.Utils.ResolveHierarchy(comp, p.proxy.path, out Composite compContanied, out string Str);
                        //output.Add("Using My Resolver:");
                        //output.Add(Str);

                        output.Add("");

                        int resolved = 0;
                        if (p.proxy.path.Length >= 1)
                        {
                            Composite test = commandsRetail.GetComposite(p.proxy.path[0]);
                            if (test != null)
                            {
                                output.Add("0 -> COMPOSITE: " + test.name);
                                resolved++;
                            }
                            else
                            {
                                output.Add("Could not resolve proxy start");
                            }

                            for (int i = 0; i < p.proxy.path.Length; i++)
                            {
                                bool found = false;
                                foreach (Composite c in commandsRetail.Entries)
                                {
                                    string name = commandsRetail.Utils.GetEntityName(c.shortGUID, p.proxy.path[i]);
                                    if (name != p.proxy.path[i].ToByteString())
                                    {
                                        output.Add(i + " -> [" + c.shortGUID.ToByteString() + "] " + c.name + " -> [" + p.proxy.path[i] + "] " + name);
                                        resolved++;
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found && i != 0)
                                {
                                    foreach (Composite c in commandsRetail.Entries)
                                    {
                                        foreach (Entity e in c.GetEntities())
                                        {
                                            if (e.shortGUID == p.proxy.path[i])
                                            {
                                                output.Add(i + " -> [" + c.shortGUID.ToByteString() + "] " + c.name + " -> [" + p.proxy.path[i] + "] " + e.shortGUID.ToByteString() + " (NAME NOT RESOLVABLE - TYPE = " + e.variant + ")");
                                                resolved++;
                                            }
                                        }
                                    }
                                }
                            }

                            if (resolved != p.proxy.path.Length - 1)
                            {
                                output.Add("\n\nWARNING - COULD NOT RESOLVE ALL AS EXPECTED! ");
                                warn = true;
                            }
                        }
                        else
                        {
                            output.Add("EMPTY PROXY!!!?!?");
                        }

                        output.Add("***************************");
                        output.Add("");

                        if (p.shortGUID == new ShortGuid("C3-0D-91-BC"))
                        {
                            continue;


                            //string str1 = commandsRetail.Utils.GetEntityName(comp, p);
                            //
                            //CATHODE.Scripting.Composite path_entry_one = commandsRetail.GetComposite(p.proxy.path[0]); //ok, so first resolves to composite MISSIONS_Torrens
                            //(Composite test55, EntityPath path22) = content.editor_utils.GetCompositeFromInstanceID(commandsRetail, p.proxy.path[1]);
                            ////second doesn't resolve to anything, but is used in THREE torrens proxies (and nowehre else)
                            //Entity path_entry_three = path_entry_one.GetEntityByID(p.proxy.path[2]); // third resolves to an entity within the composite pointed to by entry one
                            //
                            //
                            //
                            //bool valid = p.proxy.IsPathValid(commandsRetail, comp);
                            //Entity entP = p.proxy.GetPointedEntity(commandsRetail, comp);
                            //
                            //CATHODE.Scripting.Composite com = commandsRetail.GetComposite(new ShortGuid("56-38-F8-99"));
                            //
                            //Entity pointedEnt = commandsRetail.Utils.ResolveHierarchy(comp, p.proxy.path, out CATHODE.Scripting.Composite containedComp, out string str);
                            //
                            //CATHODE.Scripting.Composite comp1 = commandsRetail.GetComposite(p.proxy.path[0]);
                            //CATHODE.Scripting.Composite comp2 = commandsRetail.GetComposite(p.proxy.path[1]);
                            //CATHODE.Scripting.Composite comp3 = commandsRetail.GetComposite(p.proxy.path[2]);
                            //CATHODE.Scripting.Composite comp4 = commandsRetail.GetComposite(p.proxy.path[3]);
                            //
                            //Entity ent1 = comp1.GetEntityByID(p.proxy.path[1]);
                            //
                            //
                            ////FunctionEntity ent2 = (FunctionEntity)ent1.GetEntityByID(p.proxy.path[1]);
                            ////FunctionEntity ent3 = (FunctionEntity)commandsRetail.GetComposite(ent2.function).GetEntityByID(p.proxy.path[2]);
                            ////FunctionEntity ent4 = (FunctionEntity)commandsRetail.GetComposite(ent3.function).GetEntityByID(p.proxy.path[3]);
                            //
                            //string ffsd = "";
                        }
                    }
                }
                Directory.CreateDirectory("ProxyTest");
                File.WriteAllLines("ProxyTest/" + Path.GetFileName(commandsRetail.EntryPoints[0].name) + (warn ? " [!]" : "") + ".txt", output);
            }
        }

        public static void CheckAllFlowgraphLayouts()
        {
#if FG_TEST
            List<string> files = Directory.GetFiles("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Alien Isolation\\DATA\\ENV", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            foreach (string file in files)
            {
                List<string> output = new List<string>();
                Commands commands = new Commands(file);
                CompositeFlowgraphTable.FlowgraphMeta.SupportedLevel level = (CompositeFlowgraphTable.FlowgraphMeta.SupportedLevel)Enum.Parse(typeof(CompositeFlowgraphTable.FlowgraphMeta.SupportedLevel), Path.GetFileName(commands.EntryPoints[0].name).ToUpper());           
                foreach (Composite composite in commands.Entries)
                {

                    var metas = FlowgraphLayoutManager.PreDefinedLayouts.flowgraphs.FindAll(o => o.CompositeGUID == composite.shortGUID && o.SupportedLevels.HasFlag(level));

                    List<LinkData> flowgraphLinks = new List<LinkData>();
                    for (int i = 0; i < metas.Count; i++)
                    {
                        for (int x = 0; x < metas[i].Nodes.Count; x++)
                        {
                            for (int y = 0; y < metas[i].Nodes[x].ConnectionsOut.Count; y++)
                            {
                                flowgraphLinks.Add(new LinkData(
                                    metas[i].Nodes[x].EntityGUID,
                                    metas[i].Nodes[x].ConnectionsOut[y].ParameterGUID,
                                    metas[i].Nodes[x].ConnectionsOut[y].ConnectedEntityGUID,
                                    metas[i].Nodes[x].ConnectionsOut[y].ConnectedParameterGUID)
                                );
                            }
                        }
                    }

                    List<Entity> entities = composite.GetEntities();
                    List<LinkData> compositeLinks = new List<LinkData>();
                    for (int i = 0; i < entities.Count; i++)
                    {
                        //clear out any dead links first
                        List<EntityConnector> trimmedChildren = new List<EntityConnector>();
                        foreach (EntityConnector connector in entities[i].childLinks)
                        {
                            if (composite.GetEntityByID(connector.linkedEntityID) == null)
                                continue;
                            trimmedChildren.Add(connector);
                        }

                        for (int x = 0; x < trimmedChildren.Count; x++)
                        {
                            compositeLinks.Add(new LinkData(
                                entities[i].shortGUID,
                                trimmedChildren[x].thisParamID,
                                trimmedChildren[x].linkedEntityID,
                                trimmedChildren[x].linkedParamID)
                            );
                        }
                    }

                    flowgraphLinks = flowgraphLinks.OrderBy(o => o.In.ParameterID.ToString()).ThenBy(o => o.Out.ParameterID.ToString()).ThenBy(o => o.In.EntityID.ToByteString()).ThenBy(o => o.Out.EntityID.ToByteString()).ToList();
                    compositeLinks = compositeLinks.OrderBy(o => o.In.ParameterID.ToString()).ThenBy(o => o.Out.ParameterID.ToString()).ThenBy(o => o.In.EntityID.ToByteString()).ThenBy(o => o.Out.EntityID.ToByteString()).ToList();

                    Directory.CreateDirectory(level + "/source/" + composite.name.Replace(':', '_'));
                    Directory.CreateDirectory(level + "/retail/" + composite.name.Replace(':', '_'));
                    File.WriteAllText(level + "/source/" + composite.name.Replace(':', '_') + "/links.json", JsonConvert.SerializeObject(flowgraphLinks, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));
                    File.WriteAllText(level + "/retail/" + composite.name.Replace(':', '_') + "/links.json", JsonConvert.SerializeObject(compositeLinks, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));

                    if (flowgraphLinks.Count != compositeLinks.Count)
                    {
                        //Entity fgIn = composite.GetEntityByID(flowgraphLinks[i].In.EntityID);
                        //Entity fgOut = composite.GetEntityByID(flowgraphLinks[i].Out.EntityID);
                        //string fgInP = flowgraphLinks[i].In.ParameterID.ToString();
                        //string fgOutP = flowgraphLinks[i].Out.ParameterID.ToString();
                        //string fgInN = Singleton.Editor.CommandsDisplay.Content.Level.Commands.Utils.GetEntityName(composite, fgIn);
                        //string fgOutN = Singleton.Editor.CommandsDisplay.Content.Level.Commands.Utils.GetEntityName(composite, fgOut);

                        output.Add(composite.name + "\n\t" + flowgraphLinks.Count + " vs retail " + compositeLinks.Count + " [" + ((flowgraphLinks.Count > compositeLinks.Count) ? "SMALLER" : "LARGER") + "]");
                    }

                    //for (int i = 0; i < flowgraphLinks.Count; i++)
                    //{
                    //    if (flowgraphLinks[i] != compositeLinks[i])
                    //    {
                    //    }
                    //}
                }
                Directory.CreateDirectory("summary");
                File.WriteAllLines("summary/" + level + ".txt", output);
            }
        }

        //quick copied from flowgraphlayoutmanager
        struct LinkData
        {
            public LinkData(ShortGuid EntityID, ShortGuid ParameterID, ShortGuid LinkedEntityID, ShortGuid LinkedParameterID)
            {
                Out = new Parameter() { EntityID = EntityID, ParameterID = ParameterID };
                In = new Parameter() { EntityID = LinkedEntityID, ParameterID = LinkedParameterID };
            }

            public Parameter Out;
            public Parameter In;

            public struct Parameter
            {
                public ShortGuid EntityID;
                public ShortGuid ParameterID;

                public static bool operator ==(Parameter left, Parameter right)
                {
                    return left.Equals(right);
                }

                public static bool operator !=(Parameter left, Parameter right)
                {
                    return !(left == right);
                }

                public override bool Equals(object obj)
                {
                    if (obj is Parameter other)
                    {
                        return EntityID.Equals(other.EntityID) && ParameterID.Equals(other.ParameterID);
                    }
                    return false;
                }

                public override int GetHashCode()
                {
                    int hashCode = -1506387652;
                    hashCode = hashCode * -1521134295 + EntityID.GetHashCode();
                    hashCode = hashCode * -1521134295 + ParameterID.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(LinkData left, LinkData right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(LinkData left, LinkData right)
            {
                return !(left == right);
            }

            public override bool Equals(object obj)
            {
                if (obj is LinkData other)
                {
                    return Out == other.Out && In == other.In;
                }
                return false;
            }

            public override int GetHashCode()
            {
                int hashCode = 1047395477;
                hashCode = hashCode * -1521134295 + Out.GetHashCode();
                hashCode = hashCode * -1521134295 + In.GetHashCode();
                return hashCode;
            }
#endif
        }

        public static void DumpCommandsToJson(string path, string dir)
        {
#if DEBUG
            List<string> files = Directory.GetFiles(path, "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            Directory.CreateDirectory(dir);
            foreach (string file in files)
            {
                Commands cmd = new Commands(file);
                string lvl = Path.GetFileName(cmd.EntryPoints[0].name);
                cmd.EntryPoints[0].name = lvl;
                foreach (Composite comp in cmd.Entries)
                {
                    string outPath = dir + "/" + lvl + "/" + comp.name.Replace(':', '_') + ".json";
                    Directory.CreateDirectory(outPath.Substring(0, outPath.Length - Path.GetFileName(outPath).Length));
                    File.WriteAllText(outPath, JsonConvert.SerializeObject(comp, Newtonsoft.Json.Formatting.Indented, new ShortGuidConverter()));
                }
            }
#endif
        }

        public static void TestEntityNames()
        {
#if DEBUG
            List<string> files = Directory.GetFiles("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Alien Isolation\\data\\ENV", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            List<string> unnamed = new List<string>();
            foreach (string file in files)
            {
                unnamed.Add("***\n"+ file + "\n***");

                Commands commands = new Commands(file);
                foreach (Composite comp in commands.Entries)
                {
                    commands.Utils.PurgeDeadLinks(comp);

                    foreach (Entity ent in comp.GetEntities())
                    {
                        if (ent.variant == EntityVariant.ALIAS)
                            continue;
                        if (ent.variant == EntityVariant.FUNCTION)
                        {
                            FunctionType func = ((FunctionEntity)ent).function.AsFunctionType;
                            if (func == FunctionType.ModelReference || 
                                func == FunctionType.RadiosityProxy || 
                                func == FunctionType.EnvironmentModelReference || 
                                func == FunctionType.PhysicsSystem)
                                continue;
                        }

                        string name = commands.Utils.GetEntityName(comp, ent);
                        if (name == ent.shortGUID.ToByteString())
                        {
                            string type = ent.variant.ToString();
                            if (ent.variant == EntityVariant.FUNCTION)
                            {
                                Composite comp2 = commands.GetComposite(((FunctionEntity)ent).function);
                                if (comp2 != null)
                                    type += " " + comp2.name;
                                else
                                    type += " " + ((FunctionEntity)ent).function.AsFunctionType.ToString();
                            }
                            unnamed.Add(comp.name + " -> [" + type + "] " + ent.shortGUID.ToByteString());
                        }
                    }
                }

                unnamed.Add("\n\n");
            }
            File.WriteAllLines("unnamed.txt", unnamed);
#endif
        }

        public static void DefaultsUnitTest(Commands cmd)
        {
#if DEBUG
            var values = Enum.GetValues(typeof(FunctionType));
            foreach (var value in values)
            {
                var paramz = cmd.Utils.GetAllParameters((FunctionType)value);
                foreach (var param in paramz)
                {
                    if (param.Item1.ToByteString() == param.Item1.ToString())
                    {
                        throw new Exception("string missing");
                    }
                }
            }
#endif
        }

        public static void ParameterCloneUnitTest()
        {
#if DEBUG
            Commands test = new Commands("M:\\Modding\\Steam Projects\\steamapps\\common\\Alien Isolation\\data\\ENV\\PRODUCTION\\BSP_LV426_PT02\\WORLD\\COMMANDS.PAK");
            {
                Parameter p = null;
                for (int i = 0; i < test.Entries.Count; i++)
                {
                    p = test.Entries[i].GetEntities().FirstOrDefault(o => o.parameters.FirstOrDefault(x => x.content.dataType == DataType.SPLINE && ((cSpline)x.content).splinePoints.Count != 0) != null)?.parameters.FirstOrDefault(x => x.content.dataType == DataType.SPLINE && ((cSpline)x.content).splinePoints.Count != 0);
                    if (p != null) break;
                }
                cSpline p0 = (cSpline)p.content;
                cSpline p2 = (cSpline)p.content.Clone();
                p2.splinePoints.Add(new cTransform());
                if (p0.splinePoints.Count == p2.splinePoints.Count)
                    throw new Exception("");
                p2.splinePoints[0].position.X = 9999;
                if (p0.splinePoints[0].position.X == p2.splinePoints[0].position.X)
                    throw new Exception("");
                p2.splinePoints[0].rotation.X = 9999;
                if (p0.splinePoints[0].rotation.X == p2.splinePoints[0].rotation.X)
                    throw new Exception("");
            }
            {
                Parameter p = null;
                for (int i = 0; i < test.Entries.Count; i++)
                {
                    p = test.Entries[i].GetEntities().FirstOrDefault(o => o.parameters.FirstOrDefault(x => x.content.dataType == DataType.BOOL) != null)?.parameters.FirstOrDefault(x => x.content.dataType == DataType.BOOL);
                    if (p != null) break;
                }
                cBool p0 = (cBool)p.content;
                cBool p2 = (cBool)p.content.Clone();
                p2.value = !p2.value;
                if (p0.value == p2.value)
                    throw new Exception("");
            }
            {
                Parameter p = null;
                for (int i = 0; i < test.Entries.Count; i++)
                {
                    p = test.Entries[i].GetEntities().FirstOrDefault(o => o.parameters.FirstOrDefault(x => x.content.dataType == DataType.ENUM) != null)?.parameters.FirstOrDefault(x => x.content.dataType == DataType.ENUM);
                    if (p != null) break;
                }
                cEnum p0 = (cEnum)p.content;
                cEnum p2 = (cEnum)p.content.Clone();
                p0.enumIndex = 999;
                if (p0.enumIndex == p2.enumIndex)
                    throw new Exception("");
                p0.enumID = new ShortGuid(99999);
                if (p0.enumID.AsUInt32 == p2.enumID.AsUInt32)
                    throw new Exception("");
            }
            {
                Parameter p = null;
                for (int i = 0; i < test.Entries.Count; i++)
                {
                    p = test.Entries[i].GetEntities().FirstOrDefault(o => o.parameters.FirstOrDefault(x => x.content.dataType == DataType.TRANSFORM) != null)?.parameters.FirstOrDefault(x => x.content.dataType == DataType.TRANSFORM);
                    if (p != null) break;
                }
                cTransform p0 = (cTransform)p.content;
                cTransform p2 = (cTransform)p.content.Clone();
                p2.position.X = 99999;
                if (p0.position.X == p2.position.X)
                    throw new Exception("");
                p2.rotation.X = 99999;
                if (p0.rotation.X == p2.rotation.X)
                    throw new Exception("");
            }
            {
                Parameter p = null;
                for (int i = 0; i < test.Entries.Count; i++)
                {
                    p = test.Entries[i].GetEntities().FirstOrDefault(o => o.parameters.FirstOrDefault(x => x.content.dataType == DataType.INTEGER) != null)?.parameters.FirstOrDefault(x => x.content.dataType == DataType.INTEGER);
                    if (p != null) break;
                }
                cInteger p0 = (cInteger)p.content;
                cInteger p2 = (cInteger)p.content.Clone();
                p2.value = 9999;
                if (p0.value == p2.value)
                    throw new Exception("");
            }
            {
                Parameter p = null;
                for (int i = 0; i < test.Entries.Count; i++)
                {
                    p = test.Entries[i].GetEntities().FirstOrDefault(o => o.parameters.FirstOrDefault(x => x.content.dataType == DataType.VECTOR) != null)?.parameters.FirstOrDefault(x => x.content.dataType == DataType.VECTOR);
                    if (p != null) break;
                }
                cVector3 p0 = (cVector3)p.content;
                cVector3 p2 = (cVector3)p.content.Clone();
                p2.value.X = 9999;
                if (p0.value.X == p2.value.X)
                    throw new Exception("");
            }
            {
                Parameter p = null;
                for (int i = 0; i < test.Entries.Count; i++)
                {
                    p = test.Entries[i].GetEntities().FirstOrDefault(o => o.parameters.FirstOrDefault(x => x.content.dataType == DataType.RESOURCE && ((cResource)x.content).value.Count != 0) != null)?.parameters.FirstOrDefault(x => x.content.dataType == DataType.RESOURCE && ((cResource)x.content).value.Count != 0);
                    if (p != null) break;
                }
                cResource p0 = (cResource)p.content;
                cResource p2 = (cResource)p.content.Clone();
                p2.value.Add(new ResourceReference());
                if (p0.value.Count == p2.value.Count)
                    throw new Exception("");
                p2.value[0].entityID = new ShortGuid(99);
                if (p0.value[0].entityID.AsUInt32 == p2.value[0].entityID.AsUInt32)
                    throw new Exception("");
                p2.value[0].AnimatedModel = null;
                if (p0.value[0].AnimatedModel == p2.value[0].AnimatedModel)
                    throw new Exception("");
                p2.value[0].CollisionMapping = null;
                if (p0.value[0].CollisionMapping == p2.value[0].CollisionMapping)
                    throw new Exception("");
                p2.value[0].DynamicPhysicsSystem = null;
                if (p0.value[0].DynamicPhysicsSystem == p2.value[0].DynamicPhysicsSystem)
                    throw new Exception("");
                p2.value[0].RenderableInstance = null;
                if (p0.value[0].RenderableInstance == p2.value[0].AnimatedModel)
                    throw new Exception("");
                p2.value[0].position.X = 9999;
                if (p0.value[0].position.X == p2.value[0].position.X)
                    throw new Exception("");
                p2.value[0].rotation.X = 9999;
                if (p0.value[0].rotation.X == p2.value[0].rotation.X)
                    throw new Exception("");
            }
            {
                Parameter p = null;
                for (int i = 0; i < test.Entries.Count; i++)
                {
                    p = test.Entries[i].GetEntities().FirstOrDefault(o => o.parameters.FirstOrDefault(x => x.content.dataType == DataType.FLOAT) != null)?.parameters.FirstOrDefault(x => x.content.dataType == DataType.FLOAT);
                    if (p != null) break;
                }
                cFloat p0 = (cFloat)p.content;
                cFloat p2 = (cFloat)p.content.Clone();
                p0.value = 1;
                p2.value = 2;
                if (p0.value == p2.value)
                    throw new Exception("");
            }
#endif
        }

        public static void MAPTEST(string path)
        {
            /*
            Console.WriteLine("Loading Commands");
            Commands commands = new Commands(path + "/WORLD/COMMANDS.PAK");
            Console.WriteLine("Generating Instances");
            EditorUtils.GenerateCompositeInstances(commands);

            int resolved = 0;
            Console.WriteLine("Loading Collision Maps");
            CollisionMaps collisionMaps = new CollisionMaps(path + "/WORLD/COLLISION.MAP");
            string json = JsonConvert.SerializeObject(collisionMaps.Entries, Formatting.Indented);
            File.WriteAllText("CollisionMaps.json", json);
            foreach (CollisionMaps.Entry entry in collisionMaps.Entries)
            {
                if (entry.entity.composite_instance_id == ShortGuid.Invalid || entry.entity.entity_id == ShortGuid.Invalid)
                {
                    Console.WriteLine("Skipping invalid");
                    continue;
                }

                EntityHierarchy hierarchy = EditorUtils.GetHierarchyFromReference(entry.entity);
                if (hierarchy == null)
                {
                    Console.WriteLine("FAILED TO RESOLVE");
                    continue;
                }
                Entity ent = hierarchy.GetPointedEntity(commands, out Composite comp);
                Console.WriteLine(hierarchy.GetHierarchyAsString(commands, comp, true));
                resolved++;
            }
            Console.WriteLine("Resolved: " + resolved);
            Console.WriteLine("Not Resolved: " + (collisionMaps.Entries.Count - resolved));

            /*
            resolved = 0;
            Console.WriteLine("Loading Physics Maps");
            PhysicsMaps physicsMaps = new PhysicsMaps(path + "/WORLD/PHYSICS.MAP");
            string json2 = JsonConvert.SerializeObject(physicsMaps.Entries, Formatting.Indented);
            File.WriteAllText("PhysicsMaps.json", json2);
            foreach (PhysicsMaps.Entry entry in physicsMaps.Entries)
            {
                if (entry.entity.composite_instance_id == ShortGuid.Invalid || entry.entity.entity_id == ShortGuid.Invalid)
                {
                    Console.WriteLine("Skipping invalid");
                    continue;
                }

                EntityHierarchy hierarchy = EditorUtils.GetHierarchyFromReference(entry.entity);
                if (hierarchy == null)
                {
                    Console.WriteLine("FAILED TO RESOLVE");
                    continue;
                }
                Entity ent = hierarchy.GetPointedEntity(commands, out Composite comp);
                Console.WriteLine(hierarchy.GetHierarchyAsString(commands, comp, true));
                resolved++;
            }
            Console.WriteLine("Resolved: " + resolved);
            Console.WriteLine("Not Resolved: " + (physicsMaps.Entries.Count - resolved));
            */

            string breakhere = "";
        }

        /*
        public static void mvr_test()
        {
#if DEBUG
            string level = "tech_rnd";

            Movers mvr = new Movers(Singleton.PathToAI + "\\data\\ENV\\PRODUCTION\\" + level + "\\WORLD\\MODELS.MVR");
            Lights lights = new Lights(Singleton.PathToAI + "\\data\\ENV\\PRODUCTION\\" + level + "\\WORLD\\LIGHTS.BIN");
            AlphaLightLevel lightsAlpha = new AlphaLightLevel(Singleton.PathToAI + "\\data\\ENV\\PRODUCTION\\" + level + "\\WORLD\\ALPHALIGHT_LEVEL.BIN");
            EnvironmentMaps env = new EnvironmentMaps(Singleton.PathToAI + "\\data\\ENV\\PRODUCTION\\" + level + "\\WORLD\\ENVIRONMENTMAP.BIN");
            //Resources res = new Resources(Singleton.PathToAI + "\\data\\ENV\\PRODUCTION\\" + level + "\\WORLD\\RESOURCES.BIN");

            mvr.Entries.Clear();
            env.Entries.Clear();
            //res.Entries.Clear();

            mvr.Save();
            env.Save();
            //res.Save();

            File.Delete(lights.Filepath);
            File.Delete(lightsAlpha.Filepath); //deleting this one causes significant visual changes (anything with alpha doesn't render, or render properly)
#endif
        }
        */

        public static void CheckAllParamInfo()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Alien Isolation\\data\\ENV", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            List<string> dump = new List<string>();
            dump.Add("PAK File,Composite Name,VariableEntity ShortGuid,VariableEntity Param Name");
            foreach (string file in files)
            {
                Commands commands = new Commands(file);

                for (int x = 0; x < commands.Entries.Count; x++)
                {
                    foreach (var variable in commands.Entries[x].variables_dictionary.Values)
                    {
                        if (commands.Utils.GetPinInfo(commands.Entries[x], variable) == null)
                        {
                            dump.Add(file + "," + commands.Entries[x].name + "," + variable.shortGUID + "," + variable.name);
                        }
                    }
                }
            }
            File.WriteAllLines("missing_param_info.csv", dump);
#endif
        }

        public static void ApplyAllDefaults()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("M:\\Modding\\Steam Projects\\steamapps\\common\\Alien Isolation\\data\\ENV\\PRODUCTION\\hab_airport", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            foreach (string file in files)
            {
                Commands commands = new Commands(file);

                for (int x = 0; x < commands.Entries.Count; x++)
                {
                    commands.Utils.PurgeDeadLinks(commands.Entries[x]);
                }
                for (int x = 0; x < commands.Entries.Count; x++)
                {
                    commands.Utils.PurgeDeadLinks(commands.Entries[x]);
                }

                for (int x = 0; x < commands.Entries.Count; x++)
                {
                    List<Entity> entities = commands.Entries[x].GetEntities();
                    for (int i = 0; i < entities.Count; i++)
                        commands.Utils.AddAllDefaultParameters(entities[i], commands.Entries[x], false); //note: applying just PARAMETER seems fine. including the others results in minor issues.
                }

                commands.Save();
            }
#endif
        }

        public static void TestPurge()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("F:\\SteamLibrary\\steamapps\\common\\Alien Isolation\\data_orig\\ENV\\Production", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            foreach (string file in files)
            {
                Commands commands = new Commands(file);
                for (int x = 0; x < commands.Entries.Count; x++)
                    commands.Utils.PurgeDeadLinks(commands.Entries[x]);

                for (int x = 0; x < commands.Entries.Count; x++)
                {
                    if (commands.Utils.PurgeDeadLinks(commands.Entries[x])) {
                        string sdfsddf = "";
                    }
                }
            }
#endif
        }

        /*
        public static void TestMVR()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("M:\\Modding\\Steam Projects\\steamapps\\common\\Alien Isolation\\data\\ENV\\PRODUCTION", "MODELS.MVR", SearchOption.AllDirectories).ToList<string>();
            foreach (string file in files)
            {
                Movers movers = new Movers(file);
                for (int i = 0; i < movers.Entries.Count; i++)
                {
                    Matrix4x4.Decompose(movers.Entries[i].transform, out Vector3 scale, out System.Numerics.Quaternion rotation, out Vector3 position);
                    if (scale.X < 0.99 || scale.X > 1.01 ||
                        scale.Y < 0.99 || scale.Y > 1.01 || 
                        scale.Z < 0.99 || scale.Z > 1.01)
                    {
                        Console.WriteLine(scale);
                    }
                }
                //movers.Save();
            }
#endif
        }
        */

        public static void TestLights()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("M:\\Modding\\Steam Projects\\steamapps\\common\\Alien Isolation\\data\\ENV\\PRODUCTION", "lights.bin", SearchOption.AllDirectories).ToList<string>();
            foreach (string file in files)
            {
                Lights lights = new Lights(file);
                lights.Sun.enabled = true;
                lights.Sun.colour = new Vector3(1, 1, 1);
                lights.Sun.direction = new Vector3(0, 90, 0);
                lights.Sun.feature_flags |= Lights.LightFeature.LensFlare;
                lights.Sun.feature_flags |= Lights.LightFeature.NoClip;
                lights.Sun.feature_flags |= Lights.LightFeature.Specular;
                lights.Sun.feature_flags = 0;
                lights.Save();
            }

            string sdffsd = "";
#endif
        }

        public static void TestAllFlowgraphs()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Alien Isolation\\DATA\\ENV", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            foreach (string file in files)
            {
                Commands commands = new Commands(file);
                FlowgraphLayoutManager.LinkCommands(commands);
                foreach (Composite comp in commands.Entries)
                {
                    List<FlowgraphMeta> layouts = FlowgraphLayoutManager.GetLayouts(comp);
                    foreach (FlowgraphMeta layout in layouts)
                    {
                        Flowgraph flowgraph = new Flowgraph(commands);
                        flowgraph.Show();
                        flowgraph.ShowFlowgraph(comp, layout);
                        flowgraph.Hide();
                        flowgraph.Dispose();
                    }
                }
            }

            string sdfsdf = "";
#endif
        }

        public static void checkphysicssystempositions()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("F:\\Alien Isolation Versions\\Alien Isolation PC Final\\DATA\\ENV", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            Parallel.ForEach(files, file =>
            {
                Commands commands = new Commands(file);
                Parallel.ForEach(commands.Entries, comp =>
                {
                    Parallel.ForEach(comp.functions, func =>
                    {
                        if (func.function == ShortGuidUtils.Generate("PhysicsSystem"))
                        {
                            Parameter pos = func.GetParameter("position");
                            if (pos != null)
                            {
                                string sdfsdf = "";
                            }
                        }
                    });
                });
            });
#endif
        }

        public static void checkprefabinstances()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("F:\\Alien Isolation Versions\\Alien Isolation PC Final\\DATA\\ENV\\PRODUCTION", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            Parallel.ForEach(files, file =>
            {
                Commands commands = new Commands(file);
                Parallel.ForEach(commands.Entries, comp =>
                {
                    Parallel.ForEach(comp.functions, func =>
                    {
                        if (func.function == FunctionType.Zone)
                        {
                            List<EntityConnector> parentLinks = func.GetParentLinks(comp);
                            if (parentLinks.FindAll(o => o.thisParamID == ShortGuidUtils.Generate("composites")).Count != 0)
                            {
                                string sdfsdf = "";
                            }
                            if (func.childLinks.FindAll(o => o.thisParamID == ShortGuidUtils.Generate("composites")).Count != 0)
                            {
                                string sdfsdf = "";
                            }
                        }
                    });
                });
            });
#endif
        }

        public static void checkresources()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("F:\\Alien Isolation Versions\\Alien Isolation PC Final\\DATA\\ENV\\PRODUCTION", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            Parallel.ForEach(files, file =>
            {
                Commands commands = new Commands(file);
                Parallel.ForEach(commands.Entries, comp =>
                {
                    Parallel.ForEach(comp.functions, func =>
                    {
                        ResourceReference resource = func.GetResource(ResourceType.COLLISION_MAPPING);
                        if (resource != null)
                        {
                            Console.WriteLine("COLLISION_MAPPING: " + func.function.ToString());
                        }
                        ResourceReference resource2 = func.GetResource(ResourceType.RENDERABLE_INSTANCE);
                        if (resource2 != null)
                        {
                            Console.WriteLine("RENDERABLE_INSTANCE: " + func.function.ToString());
                        }
                    });
                });
            });
#endif
        }

        public static void checkaliases()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("D:\\Alien Isolation Modding\\Isolation Mod Tools\\Alien Isolation PC Final/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            Parallel.ForEach(files, file =>
            {
                Commands commands = new Commands(file);
                Parallel.ForEach(commands.Entries, comp =>
                {
                    Parallel.ForEach(comp.aliases, func =>
                    {
                        for (int i = 0; i < func.childLinks.Count; i++)
                        {
                            Entity childEnt = comp.GetEntityByID(func.childLinks[i].linkedEntityID);
                            switch (childEnt.variant)
                            {
                                case EntityVariant.FUNCTION:
                                    Console.WriteLine("FUNCTION: " + ((FunctionEntity)childEnt).function.ToString());
                                    break;
                                case EntityVariant.VARIABLE:
                                    Console.WriteLine("VARIABLE: " + ((VariableEntity)childEnt).name.ToString());
                                    break;
                                case EntityVariant.PROXY:
                                    string sadfsfd = "";
                                    break;
                                case EntityVariant.ALIAS:
                                    Console.WriteLine("ALIAS! " );
                                    break;
                            }
                        }
                        //if (func.childLinks.Count != 0)
                        //{
                        //    Console.WriteLine(file + "\n\t" + comp.name + "\n\t" + func.shortGUID.ToByteString());
                        //}
                    });;
                });
            });
#endif
        }

        public static void checkvariables()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("D:\\Alien Isolation Modding\\Isolation Mod Tools\\Alien Isolation PC Final/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            Parallel.ForEach(files, file =>
            {
                Commands commands = new Commands(file);
                Parallel.ForEach(commands.Entries, comp =>
                {
                    Parallel.ForEach(comp.variables, func =>
                    {
                        if (func.childLinks.Count == 0 && func.parameters.Count == 0)
                        {
                            //Console.WriteLine("NO PARAMS OR LINKS");
                        }
                        if (func.parameters.Count > 1) 
                        {
                            Console.WriteLine("PARAMS: " + func.parameters.Count);
                        }
                        List<string> paramStrs = new List<string>();
                        foreach (EntityConnector connector in func.childLinks)
                        {
                            if (!paramStrs.Contains(connector.thisParamID.ToString()))
                                paramStrs.Add(connector.thisParamID.ToString());
                        }
                        if (paramStrs.Count > 1)
                        {
                            Console.WriteLine("UNIQUE LINKS: " + paramStrs.Count);
                        }
                    });
                });
            });
#endif
        }

        //Go to the top of the connection chain, and follow it down to get the number of nodes in the sequence - keep a track of the longest sequences and where they are
        public static void CountEntitySequences()
        {
            string OnlyDoOneComp = "";//"SCRIPT_STORYMISSION\\M04_WELCOME_TO_THE_SEVASTOPOL\\M04_PART_01\\M04_PT01"; //keep this blank to dump all

            List<string> files = Directory.GetFiles("F:\\SteamLibrary\\steamapps\\common\\Alien Isolation\\data_orig\\ENV\\Production", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            List<string> output = new List<string>();
            List<int> counts = new List<int>();
            int max = 0;
            foreach (string file in files)
            {
                output.Add("====== Loading =====");
                output.Add(file);
                output.Add("====================");

                Commands commands = new Commands(file);

                foreach (Composite composite in commands.Entries)
                {
                    if (OnlyDoOneComp != "" && composite.name != OnlyDoOneComp)
                        continue;

                    commands.Utils.PurgeDeadLinks(composite);

                    List<Entity> entities = composite.GetEntities();
                    List<Entity> checkedEnts = new List<Entity>();
                    foreach (Entity entity in entities)
                    {
                        if (checkedEnts.Contains(entity))
                            continue;

                        List<Entity> connectedEntities = new List<Entity>();
                        RecurseEntityConnections(entity, composite, connectedEntities);

                        if (connectedEntities.Count != 1)
                        {
                            if (OnlyDoOneComp != "")
                            {
                                Console.WriteLine(composite.name + " -> " + connectedEntities.Count);
                            }
                            else
                            {
                                //foreach (Entity ent in connectedEntities)
                                //{
                                //    Console.WriteLine(EntityUtils.GetName(composite, ent));
                                //}

                                output.Add(composite.name + " -> " + connectedEntities.Count);
                                counts.Add(connectedEntities.Count);
                            }
                        }

                        if (max < connectedEntities.Count)
                            max = connectedEntities.Count;

                        checkedEnts.AddRange(connectedEntities);
                    }

                    if (OnlyDoOneComp != "")
                        return;
                }
            }
            output.Add("-----------");
            output.Add("Max: " + max);
            File.WriteAllLines("output.txt", output);

            counts.Sort();
            output.Clear();
            foreach (int count in counts)
            {
                output.Add(count.ToString());
            }
            File.WriteAllLines("counts.txt", output);
        }
        private static void RecurseEntityConnections(Entity entity, Composite comp, List<Entity> checkedEnts)
        {
            if (checkedEnts.Contains(entity))
                return;
            checkedEnts.Add(entity);

            List<EntityConnector> connectionsIn = entity.GetParentLinks(comp);
            foreach (EntityConnector connection in connectionsIn)
            {
                Entity ent = comp.GetEntityByID(connection.linkedEntityID);
                if (ent == null) continue;
                RecurseEntityConnections(ent, comp, checkedEnts);
            }

            List<EntityConnector> connectionsOut = entity.childLinks;
            foreach (EntityConnector connection in connectionsOut)
            {
                Entity ent = comp.GetEntityByID(connection.linkedEntityID);
                if (ent == null) continue;
                RecurseEntityConnections(ent, comp, checkedEnts);
            }
        }

        //This checks to make sure that variable entities don't have any links that aren't the variable name (spoiler: they don't)
        public static void CheckVariablesHaveNoRogueParams()
        {
            List<string> files = Directory.GetFiles("F:\\SteamLibrary\\steamapps\\common\\Alien Isolation\\data_orig\\ENV\\Production", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            foreach (string file in files)
            {
                Commands commands = new Commands(file);
                foreach (Composite comp in commands.Entries)
                {
                    foreach (VariableEntity var in comp.variables)
                    {
                        List<EntityConnector> connectionsIn = var.GetParentLinks(comp);
                        List<EntityConnector> connectionsOut = var.childLinks;

                        foreach (EntityConnector connection in connectionsIn)
                        {
                            if (connection.thisParamID != var.name)
                            {
                                throw new Exception("Rogue");
                            }
                        }
                        foreach (EntityConnector connection in connectionsOut)
                        {
                            if (connection.thisParamID != var.name)
                            {
                                throw new Exception("Rogue");
                            }
                        }

                        foreach (Parameter param in var.parameters)
                        {
                            if (param.name != var.name)
                            {
                                throw new Exception("Rogue");
                            }
                        }
                    }
                }
            }
        }

        public static void checkanims()
        {
#if DEBUG

            List<string> files = Directory.GetFiles("D:\\Alien Isolation Modding\\Isolation Mod Tools\\Alien Isolation PC Final/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            Parallel.ForEach(files, file =>
            {
                Commands commands = new Commands(file);
                Parallel.ForEach(commands.Entries, comp =>
                {
                    Parallel.ForEach(comp.functions, func =>
                    {
                        Parameter Animation = func.GetParameter("Animation");
                        Parameter AnimationSet = func.GetParameter("AnimationSet");
                        if (AnimationSet == null && Animation != null)
                        {
                            Console.WriteLine(comp.name + " -> " + commands.Utils.GetEntityName(comp, func) + " (" + func.function.ToString() + ")" + "\n\tWARNING: Found Animation without AnimationSet\n\n");
                        }
                        if (AnimationSet != null && Animation == null)
                        {
                            Console.WriteLine(comp.name + " -> " + commands.Utils.GetEntityName(comp, func) + " (" + func.function.ToString() + ")" + "\n\tWARNING: Found AnimationSet without Animation\n\n");
                        }
                        if (AnimationSet != null && Animation != null)
                        {
                            //Console.WriteLine("Animation matched with AnimationSet");
                        }
                    });
                });
            });
#endif
        }

        public static void DumpAllEnts(string alien_path, string output_append)
        {
#if DEBUG

            List<string> files = Directory.GetFiles(alien_path + "/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            Dictionary<string, List<string>> usesOfFunction = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> usesOfComposite = new Dictionary<string, List<string>>();

            foreach (FunctionType func in (FunctionType[])Enum.GetValues(typeof(FunctionType)))
            {
                usesOfFunction.Add(func.ToString(), new List<string>());
            }

            foreach (string file in files)
            {
                string[] split = file.Replace("\\", "/").Split(new[] { "/DATA/ENV/PRODUCTION/" }, StringSplitOptions.None);
                string mapName = split[split.Length - 1].Substring(0, split[split.Length - 1].Length - ("/WORLD/COMMANDS.PAK").Length);

                Commands commands = new Commands(file);
                foreach (Composite comp in commands.Entries)
                {
                    if (!usesOfComposite.ContainsKey(comp.name))
                    {
                        usesOfComposite.Add(comp.name, new List<string>());
                    }
                    usesOfComposite[comp.name].Add(mapName);

                    foreach (FunctionEntity ent in comp.functions)
                    {
                        if (commands.GetComposite(ent.function) != null) continue;
                        //if (!CommandsUtils.FunctionTypeExists(ent.function)) continue; //NOT USING THIS ANYMORE AS DELETED FUNCTIONS COULD WILL NOT BE COUNTED FOR...

                        string type = !ent.function.IsFunctionType ? "DELETED FUNCTION OR COMPOSITE: " + ent.function.ToByteString() : ent.function.AsFunctionType.ToString();
                        if (!usesOfFunction.ContainsKey(type))
                        {
                            usesOfFunction.Add(type, new List<string>());
                        }
                        if (!usesOfFunction[type].Contains(comp.name))
                        {
                            usesOfFunction[type].Add(comp.name);
                        }
                    }
                }
            }

            List<string> functionFile = new List<string>();
            foreach (KeyValuePair<string, List<string>> val in usesOfFunction)
            {
                val.Value.Sort();

                functionFile.Add("<h3>" + val.Key.ToString() + "</h3>");
                functionFile.Add("<h6>Seen in " + val.Value.Count + " composites:<h6><ul>");
                foreach (string val2 in val.Value)
                {
                    functionFile.Add("<li>" + val2 + "</li>");
                }
                functionFile.Add("</ul><hr>");
            }
            File.WriteAllLines("AlienFunctionUses" + output_append + ".html", functionFile);

            List<string> compFile = new List<string>();
            List<string> allComps = new List<string>();
            foreach (KeyValuePair<string, List<string>> val in usesOfComposite)
            {
                val.Value.Sort();
                allComps.Add(val.Key);

                compFile.Add("<h3>" + val.Key + "</h3>");
                compFile.Add("<h6>Seen in " + val.Value.Count + " levels:<h6><ul>");
                foreach (string val2 in val.Value)
                {
                    compFile.Add("<li>" + val2 + "</li>");
                }
                compFile.Add("</ul><hr>");
            }
            File.WriteAllLines("AlienCompositeUses" + output_append + ".html", compFile);
            allComps.Sort();
            File.WriteAllLines("AllComposites" + output_append + ".txt", allComps);

#endif
            }

        public static void TestOrders()
        {
#if DEBUG
            List<string> files = Directory.GetFiles(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            GUI_EnumDataType enumUI = new GUI_EnumDataType();
            Parallel.ForEach(files, file =>
            {
                Commands phys = new Commands(file);
                Parallel.ForEach(phys.Entries, comp =>
                {
                    UInt32 prevVal = 0;
                    /*
                    Console.WriteLine("\t FUNCTIONS");
                    foreach (FunctionEntity ent in comp.functions)
                    {
                        UInt32 currVal = ent.shortGUID.ToUInt32();
                        if (prevVal > currVal)
                            Console.WriteLine("NOPE");
                        else
                            Console.WriteLine("YES");
                        prevVal = currVal;

                        //Console.WriteLine(ent.shortGUID.ToByteString() + " = " + ent.shortGUID.ToUInt32());
                    }*/
                    //Console.WriteLine("----");
                    //Console.WriteLine("\t ALIASES");
                    foreach (AliasEntity ent in comp.aliases)
                    {
                        UInt32 currVal = ent.shortGUID.ToUInt32();
                        if (prevVal > currVal)
                            Console.WriteLine("NOPE");
                        else
                            Console.WriteLine("YES");
                        prevVal = currVal;

                        //Console.WriteLine(ent.shortGUID.ToByteString() + " = " + ent.shortGUID.ToUInt32());
                    }
                    //Console.WriteLine("----");
                    //Console.WriteLine("\t PROXIES");
                    foreach (ProxyEntity ent in comp.proxies)
                    {
                        UInt32 currVal = ent.shortGUID.ToUInt32();
                        if (prevVal > currVal)
                            Console.WriteLine("NOPE");
                        else
                            Console.WriteLine("YES");
                        prevVal = currVal;

                        //Console.WriteLine(ent.shortGUID.ToByteString() + " = " + ent.shortGUID.ToUInt32());
                    }
                    /*
                    Console.WriteLine("----");
                    Console.WriteLine("\t VARIABLES");
                    foreach (VariableEntity ent in comp.variables)
                    {
                        UInt32 currVal = ent.shortGUID.ToUInt32();
                        if (prevVal > currVal)
                            Console.WriteLine("NOPE");
                        else
                            Console.WriteLine("YES");
                        prevVal = currVal;

                        //Console.WriteLine(ent.shortGUID.ToByteString() + " = " + ent.shortGUID.ToUInt32());
                    }
                    */
                    //Console.WriteLine("----");
                });
            });
#endif
        }

        public static void TestNewEnumDropdowns()
        {
#if DEBUG
            List<string> files = Directory.GetFiles(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            GUI_EnumDataType enumUI = new GUI_EnumDataType();
            Parallel.ForEach(files, file =>
            {
                Commands phys = new Commands(file);
                Parallel.ForEach(phys.Entries, comp =>
                {
                    Parallel.ForEach(comp.GetEntities(), ent =>
                    {
                        Parallel.ForEach(ent.parameters, param =>
                        {
                            if (param.content.dataType == DataType.ENUM)
                                Console.WriteLine(file + "," + comp.name + "," + phys.Utils.GetEntityName(comp, ent) + "," + param.name + "," + ((cEnum)param.content).enumID.ToString() + "," + ((cEnum)param.content).enumIndex);
                            //if (param.content.dataType == DataType.ENUM)
                            //    enumUI.PopulateUI((cEnum)param.content, file + "," + comp.name + "," + EntityUtils.GetName(comp, ent) + "," + param.name.ToString() + ",");
                        });
                    });
                });
            });
#endif
        }



        public static void CommandsTest()
        {
#if DEBUG
            List<string> files = Directory.GetFiles(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            Parallel.ForEach(files, file =>
            {
                Commands phys = new Commands(file);
                Parallel.ForEach(phys.Entries, comp =>
                {
                    Parallel.ForEach(comp.functions_dictionary.Values, ent =>
                    {
                        if (comp.functions_dictionary.Values.Count(o => o.function == FunctionType.PhysicsModifyGravity) != 0)
                        {
                            Console.WriteLine(file + "\n\t" + comp.name);
                        }
                    });
                });
            });
#endif
        }

        public static void CheckMissingCageAnimParams()
        {
#if DEBUG
            List<string> files = Directory.GetFiles(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            Parallel.ForEach(files, file =>
            {
                Commands phys = new Commands(file);
                Composite gui = phys.GetComposite("ARCHETYPES\\GAMEPLAY\\UI_OBJECTIVE");
                if (gui != null)
                {
                    ShortGuid id = gui.shortGUID;
                    Parallel.ForEach(phys.Entries, comp =>
                    {
                        Parallel.ForEach(comp.functions_dictionary.Values.Where(o => o.function == id), ent =>
                        {
                            Console.WriteLine(file + "\n\t" + comp.name + "\n\t\t" + phys.Utils.GetEntityName(comp, ent));

                            //Parallel.ForEach(ent.parameters, param =>
                            //{
                            //    if (list.Contains(param.name))
                            //    {
                            //        Console.WriteLine(file + "\n\t" + comp.name + "\n\t\t" + EntityUtils.GetName(comp, ent) + " -> " + param.name.ToString());
                            //    }
                            //});
                        });
                    });
                }
            });
#endif
        }

        public static void SanityCheckCAGEAnimation()
        {
#if DEBUG
            List<string> files = Directory.GetFiles(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            Parallel.ForEach(files, file =>
            {
                Commands phys = new Commands(file);
                Parallel.ForEach(phys.Entries, comp =>
                {
                    List<FunctionEntity> ents = comp.functions_dictionary.Values.Where(o => o.function == FunctionType.CAGEAnimation).ToList();
                    Parallel.ForEach(ents, ent =>
                    {
                        CAGEAnimation anim = (CAGEAnimation)ent;
                        //File.WriteAllText("out.json", JsonConvert.SerializeObject(anim, Formatting.Indented));

                        List<CAGEAnimation.Connection> prunedConnections = new List<CAGEAnimation.Connection>();
                        foreach (CAGEAnimation.Connection connection in anim.connections)
                        {
                            List<CAGEAnimation.FloatTrack> anim_target = anim.animations.FindAll(o => o.shortGUID == connection.target_track);
                            List<CAGEAnimation.EventTrack> event_target = anim.events.FindAll(o => o.shortGUID == connection.target_track);
                            if (anim_target.Count == 0 && event_target.Count == 0) continue;
                            prunedConnections.Add(connection);
                        }
                        anim.connections = prunedConnections;

                        foreach (CAGEAnimation.Connection connection in anim.connections)
                        {
                            List<CAGEAnimation.FloatTrack> anim_target = anim.animations.FindAll(o => o.shortGUID == connection.target_track);
                            List<CAGEAnimation.EventTrack> event_target = anim.events.FindAll(o => o.shortGUID == connection.target_track);

                            //We expect to never point to both
                            if (anim_target.Count != 0 && event_target.Count != 0)
                            {
                                throw new Exception();
                            }
                            if (anim_target.Count > 1 || event_target.Count > 1)
                            {
                                throw new Exception();
                            }

                            if (connection.binding_type == ObjectType.ENTITY)
                            {
                                //ENTITY links always point to Animation keyframes
                                if (anim_target.Count == 0 || event_target.Count != 0)
                                {
                                    throw new Exception();
                                }

                                //ENTITY links must always point to params, these appear to only be TRANSFORM or FLOAT in vanilla PAKs
                                if (connection.target_param_type != DataType.TRANSFORM &&
                                    connection.target_param_type != DataType.FLOAT)
                                {
                                    throw new Exception();
                                }

                                //Check to make sure all TRANSFORM keys happen on the same intervals & are complete
                                if (connection.target_param_type == DataType.TRANSFORM)
                                {
                                    List<CAGEAnimation.Connection> transform = anim.connections.FindAll(o => o.connectedEntity == connection.connectedEntity && o.target_param.ToString() == "position");
                                    if (transform.Count != 6 && transform.Count != 3 && transform.Count != 5) //x,y,z,Yaw,Pitch,Roll
                                    {
                                        throw new Exception();
                                    }
                                    List<float> keyframeIntervals = null;
                                    foreach (CAGEAnimation.Connection transformPart in transform)
                                    {
                                        CAGEAnimation.FloatTrack keyframes = anim.animations.FirstOrDefault(o => o.shortGUID == connection.target_track);
                                        if (keyframeIntervals == null)
                                        {
                                            keyframeIntervals = new List<float>();
                                            foreach (CAGEAnimation.FloatTrack.Keyframe keyframe in keyframes.keyframes)
                                            {
                                                keyframeIntervals.Add(keyframe.time);
                                            }
                                        }
                                        else
                                        {
                                            if (keyframeIntervals.Count != keyframes.keyframes.Count)
                                            {
                                                throw new Exception();
                                            }
                                            for (int i = 0; i < keyframes.keyframes.Count; i++)
                                            {
                                                if (keyframeIntervals[i] != keyframes.keyframes[i].time)
                                                {
                                                    throw new Exception();
                                                }
                                            }
                                        }
                                    }
                                }

                                //Check sub IDs for pointed datatypes
                                if (connection.target_param_type == DataType.TRANSFORM)
                                {
                                    if (connection.target_sub_param.ToString() != "Yaw" &&
                                        connection.target_sub_param.ToString() != "Pitch" &&
                                        connection.target_sub_param.ToString() != "Roll" &&
                                        connection.target_sub_param.ToString() != "x" &&
                                        connection.target_sub_param.ToString() != "y" &&
                                        connection.target_sub_param.ToString() != "z")
                                    {
                                        throw new Exception();
                                    }
                                    //TODO: validate that all these vals are modified at the same keyframe times (can simplify UI!)
                                }
                                if (connection.target_param_type == DataType.FLOAT)
                                {
                                    if (connection.target_sub_param.ToString() != "")
                                    {
                                        throw new Exception();
                                    }
                                }
                            }
                            else
                            {
                                //CHARACTER and MARKER links always point to Event keyframes
                                if (anim_target.Count != 0 || event_target.Count == 0)
                                {
                                    throw new Exception();
                                }

                                //CHARACTER links usually pair with MARKER links - check that
                                if (connection.binding_type == ObjectType.CHARACTER)
                                {
                                    List<CAGEAnimation.Connection> pairedMarker = anim.connections.FindAll(o => o.binding_type == ObjectType.MARKER && o.target_track == connection.target_track);
                                    if (pairedMarker.Count != 1)
                                    {
                                        //throw new Exception();
                                    }
                                    List<CAGEAnimation.Connection> duplicateCharRef = anim.connections.FindAll(o => o.binding_type == ObjectType.CHARACTER && o.target_track == connection.target_track && o.binding_guid != connection.binding_guid);
                                    if (duplicateCharRef.Count != 0)
                                    {
                                        throw new Exception();
                                    }
                                }

                                //As we point to events and not parameters, this info should always be empty
                                if (connection.target_param.ToString() != "" ||
                                    connection.target_param_type != DataType.NONE ||
                                    connection.target_sub_param.ToString() != "")
                                {
                                    throw new Exception();
                                }
                            }
                        }
                    });
                });
            });
#endif
        }

        private static List<string> unnamed_params = new List<string>();
        public static void DumpAllUnnamedParams()
        {
#if DEBUG
            List<string> files = Directory.GetFiles(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            foreach (string file in files)
            {
                Commands phys = new Commands(file);
                foreach (Composite comp in phys.Entries)
                {
                    List<FunctionEntity> anims = comp.functions_dictionary.Values.Where(o => o.function == FunctionType.CAGEAnimation).ToList();
                    foreach (FunctionEntity ent in anims)
                    {
                        CAGEAnimation anim = (CAGEAnimation)ent;
                        foreach (CAGEAnimation.EventTrack key in anim.events)
                        {
                            foreach (CAGEAnimation.EventTrack.Keyframe keyData in key.keyframes)
                            {
                                AddToListIfUnnamed(keyData.forward);
                                AddToListIfUnnamed(keyData.reverse);
                            }
                        }
                    }

                    List<FunctionEntity> trigs = comp.functions_dictionary.Values.Where(o => o.function == FunctionType.TriggerSequence).ToList();
                    foreach (FunctionEntity ent in trigs)
                    {
                        TriggerSequence trig = (TriggerSequence)ent;
                        foreach (TriggerSequence.MethodEntry e in trig.methods)
                        {
                            AddToListIfUnnamed(e.method);
                            AddToListIfUnnamed(e.finished);
                        }
                    }

                    foreach (Entity ent in comp.GetEntities())
                    {
                        foreach (Parameter p in ent.parameters)
                        {
                            AddToListIfUnnamed(p.name);
                        }
                    }
                }
            }
            File.WriteAllLines("unnamed.txt", unnamed_params);
#endif
        }
        private static void AddToListIfUnnamed(ShortGuid id)
        {
            if (id.ToString() != id.ToByteString()) return;
            if (unnamed_params.Contains(id.ToByteString())) return;
            unnamed_params.Add(id.ToByteString());
        }


        public static void ModelTestStuff()
        {
#if DEBUG
            /*
            AlienVBF vertexFormat = new AlienVBF();
            vertexFormat.Elements.Add(new AlienVBF.Element()
            {
                ArrayIndex = 0,
                Offset = 0,
                ShaderSlot = VBFE_InputSlot.VERTEX,
                Unknown_ = 2,
                VariableType = VBFE_InputType.VECTOR3,
                VariantIndex = 0,
            });
            vertexFormat.Elements.Add(new AlienVBF.Element()
            {
                ArrayIndex = 255,
                Offset = 0,
                ShaderSlot = VBFE_InputSlot.VERTEX,
                Unknown_ = 2,
                VariableType = VBFE_InputType.AlienVertexInputType_u16, // TODO!!!!! IS THIS ALWAYS THE LAST?
                VariantIndex = 0,
            });

            byte[] content = new byte[240];
            using (BinaryWriter writer = new BinaryWriter(new MemoryStream(content)))
            {
                writer.Write((float)(1)); writer.Write((float)(1)); writer.Write((float)(-1));
                writer.Write((float)(1)); writer.Write((float)(-1)); writer.Write((float)(-1));
                writer.Write((float)(1)); writer.Write((float)(1)); writer.Write((float)(1));
                writer.Write((float)(1)); writer.Write((float)(-1)); writer.Write((float)(1));
                writer.Write((float)(-1)); writer.Write((float)(1)); writer.Write((float)(-1));
                writer.Write((float)(-1)); writer.Write((float)(-1)); writer.Write((float)(-1));
                writer.Write((float)(-1)); writer.Write((float)(1)); writer.Write((float)(1));
                writer.Write((float)(-1)); writer.Write((float)(-1)); writer.Write((float)(1));
                //96
            
                writer.Write((Int16)0); writer.Write((Int16)0); writer.Write((Int16)0);
                writer.Write((Int16)4); writer.Write((Int16)4); writer.Write((Int16)0);
                writer.Write((Int16)6); writer.Write((Int16)8); writer.Write((Int16)0);
                writer.Write((Int16)2); writer.Write((Int16)2); writer.Write((Int16)0);

                writer.Write((Int16)3); writer.Write((Int16)3); writer.Write((Int16)1);
                writer.Write((Int16)2); writer.Write((Int16)2); writer.Write((Int16)1);
                writer.Write((Int16)6); writer.Write((Int16)9); writer.Write((Int16)1);
                writer.Write((Int16)7); writer.Write((Int16)11); writer.Write((Int16)1);

                writer.Write((Int16)7); writer.Write((Int16)12); writer.Write((Int16)2);
                writer.Write((Int16)6); writer.Write((Int16)10); writer.Write((Int16)2);
                writer.Write((Int16)4); writer.Write((Int16)5); writer.Write((Int16)2);
                writer.Write((Int16)5); writer.Write((Int16)7); writer.Write((Int16)2);

                writer.Write((Int16)5); writer.Write((Int16)6); writer.Write((Int16)3);
                writer.Write((Int16)1); writer.Write((Int16)1); writer.Write((Int16)3);
                writer.Write((Int16)3); writer.Write((Int16)3); writer.Write((Int16)3);
                writer.Write((Int16)7); writer.Write((Int16)13); writer.Write((Int16)3);

                writer.Write((Int16)1); writer.Write((Int16)1); writer.Write((Int16)4);
                writer.Write((Int16)0); writer.Write((Int16)0); writer.Write((Int16)4);
                writer.Write((Int16)2); writer.Write((Int16)2); writer.Write((Int16)4);
                writer.Write((Int16)3); writer.Write((Int16)3); writer.Write((Int16)4);

                writer.Write((Int16)5); writer.Write((Int16)7); writer.Write((Int16)5);
                writer.Write((Int16)4); writer.Write((Int16)5); writer.Write((Int16)5);
                writer.Write((Int16)0); writer.Write((Int16)0); writer.Write((Int16)5);
                writer.Write((Int16)1); writer.Write((Int16)1); writer.Write((Int16)5);
                //144
            }

            */
            /*
            CS2 model = new CS2();
            model.Name = "SomeTestShit";
            model.Submeshes.Add(new CS2.Submesh()
            {
                Name = "Test",
                LODMinDistance_ = 0,
                LODMaxDistance_ = 10000,
                AABBMin = new Vector3(),
                AABBMax = new Vector3(),
                MaterialLibraryIndex = 1,
                Unknown2_ = 134239232,
                UnknownIndex = -1,
                CollisionIndex_ = -1,
                // VertexFormat = vertexFormat,
                // VertexFormatLowDetail = vertexFormat,
                ScaleFactor = 4,
                HeadRelated_ = -1,
            });
            //model.Submeshes[0].content = content;
            model.Submeshes[0].IndexCount = 48;
            model.Submeshes[0].VertexCount = 8;

            CathodeLib.Level lvl = new Level(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/opencage/");
            /*
            CS2 model = lvl.AllModels.Entries.FirstOrDefault(o => o.Name == "SomeTestShit");
            if (model == null)
            {
                throw new Exception("bruh");
            }
            */
            /*
            CS2 modelCopy = lvl.Models.Entries.FirstOrDefault(o => o.Submeshes.FirstOrDefault(x => x.Name.Contains("Sphere")) != null);
            if (modelCopy == null)
            {
                throw new Exception("bruh");
            }

            model.Submeshes[0].VertexFormat = modelCopy.Submeshes[0].VertexFormat;
            model.Submeshes[0].VertexFormatLowDetail = modelCopy.Submeshes[0].VertexFormatLowDetail;
            model.Submeshes[0].content = modelCopy.Submeshes[0].content;
            model.Submeshes[0].IndexCount = modelCopy.Submeshes[0].IndexCount;
            model.Submeshes[0].VertexCount = modelCopy.Submeshes[0].VertexCount;
            model.Submeshes[0].Unknown2_ = modelCopy.Submeshes[0].Unknown2_;
            model.Submeshes[0].AABBMax = modelCopy.Submeshes[0].AABBMax;
            model.Submeshes[0].AABBMin = modelCopy.Submeshes[0].AABBMin;

            File.WriteAllBytes("out.bin", model.Submeshes[0].content);

            AlienVBF vertexFormat = new AlienVBF();
            /*
            vertexFormat.Elements.Add(new AlienVBF.Element()
            {
                //ArrayIndex = 0,
                Offset = 0,
                ShaderSlot = VBFE_InputSlot.VERTEX,
                VariableType = VBFE_InputType.VECTOR4_INT16_DIVMAX,
                VariantIndex = 0,
            });
            vertexFormat.Elements.Add(new AlienVBF.Element()
            {
                //ArrayIndex = 255,
                Offset = 0,
                ShaderSlot = VBFE_InputSlot.VERTEX,
                VariableType = VBFE_InputType.AlienVertexInputType_u16, // TODO!!!!! IS THIS ALWAYS THE LAST?
                VariantIndex = 0,
            });*/

            //TODO: go through using vertex format and pull out verts and indices, and then only write them
            //using (BinaryWriter writer = new BinaryWriter(new MemoryStream(model.Submeshes[0].content)))
            //{
            //
            //}
            //model.Submeshes[0].content = content;
            //model.Submeshes[0].VertexFormat = vertexFormat;
            //model.Submeshes[0].VertexFormatLowDetail = vertexFormat;

            //lvl.Save();
#endif
        }

        public static void LoadAllFileTests()
        {
#if DEBUG
            //Models mdls = new Models(Singleton.PathToAI + "\\DATA\\ENV\\PRODUCTION\\BSP_TORRENS\\RENDERABLE\\LEVEL_MODELS.PAK");
            //mdls.Save();
            /*
            CathodeModels mdls_old = new CathodeModels(
                Singleton.PathToAI + "\\DATA\\ENV\\PRODUCTION\\BSP_TORRENS\\RENDERABLE\\MODELS_LEVEL.BIN",
                Singleton.PathToAI + "\\DATA\\ENV\\PRODUCTION\\BSP_TORRENS\\RENDERABLE\\LEVEL_MODELS.PAK");

            int binIndex = 0;
            for (int i = 0; i < mdls.Entries.Count; i++)
            {
                for (int y = 0; y < mdls.Entries[i].Submeshes.Count; y++)
                {
                    File.WriteAllBytes(binIndex + "_new.bin", mdls.Entries[i].Submeshes[y].content);
                    binIndex++;
                }
            }

            for (int i = 0; i < mdls_old.Models.Count; i++)
            {
                for (int y = 0; y < mdls_old.Models[i].Submeshes.Count; y++)
                {
                    File.WriteAllBytes(mdls_old.Models[i].Submeshes[y].binIndex + "_old.bin", mdls_old.Models[i].Submeshes[y].content);
                }
            }

           // mdls.Save();

            for (int i = 0; i < mdls_old.Models.Count; i++)
            {
                for (int x = 0;x < mdls_old.Models[i].Submeshes.Count; x++)
                {
                    int correct = mdls_old.Models[i].Submeshes[x].content.Length;
                    int newmethod = mdls.Entries[i].Submeshes[x].content.Length;
                    Console.WriteLine("Correct = " + correct + ", new = " + newmethod);
                }
            }
            */
            //return;

            //string sdffds = "";

            //Models mdls = new Models(Singleton.PathToAI + "\\DATA\\ENV\\PRODUCTION\\ENG_ALIEN_NEST\\RENDERABLE\\LEVEL_MODELS.PAK");
            //mdls.Save();
            ////Textures tex34 = new Textures(Singleton.PathToAI + "\\DATA\\ENV\\PRODUCTION\\BSP_LV426_PT01\\RENDERABLE\\LEVEL_TEXTURES.ALL.PAK");
            ////tex34.Save();
            //return;

            /*
            List<string> files = Directory.GetFiles(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/", "LEVEL_TEXTURES.ALL.PAK", SearchOption.AllDirectories).ToList<string>();
            Textures texBase = new Textures("LEVEL_TEXTURES.ALL.PAK");
            foreach (string file in files)
            {
                Textures tex = new Textures(file);
                for (int i = 0; i < tex.Entries.Count; i++)
                {
                    Textures.TEX4 texture = texBase.Entries.FirstOrDefault(o => o.FileName == tex.Entries[i].FileName);
                    if (texture != null) continue;
                    texBase.Entries.Add(tex.Entries[i]);
                }


                //Models mdl = new Models(file);
                //Console.WriteLine(mdl.Loaded + " -> " + file);
                //Console.WriteLine("Saved: " + mdl.Save());
                //return;
            }
            texBase.Save();

            return;
            */

            /*
            List<string> files = Directory.GetFiles(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/", "LEVEL_MODELS.PAK", SearchOption.AllDirectories).ToList<string>();
            //Models texBase = new Models("LEVEL_MODELS.PAK");
            List<AlienVBF> formats = new List<AlienVBF>();
            foreach (string file in files)
            {
                Console.WriteLine(file);
                Models tex = new Models(file);
                for (int i = 0; i < tex.Entries.Count; i++)
                {
                    for (int y = 0; y < tex.Entries[i].Submeshes.Count; y++)
                    {
                        /*
                        if (tex.Entries[i].Submeshes[y].VertexFormat.Elements.Count == 2)
                        {
                            if (!formats.Contains(tex.Entries[i].Submeshes[y].VertexFormat))
                                formats.Add(tex.Entries[i].Submeshes[y].VertexFormat);
                        }
                        if (tex.Entries[i].Submeshes[y].VertexFormatLowDetail.Elements.Count == 2)
                        {
                            if (!formats.Contains(tex.Entries[i].Submeshes[y].VertexFormatLowDetail))
                                formats.Add(tex.Entries[i].Submeshes[y].VertexFormatLowDetail);
                        }
                        */
            /*
                        for (int x= 0; x < tex.Entries[i].Submeshes[y].VertexFormat.Elements.Count; x++)
                        {
                            if (tex.Entries[i].Submeshes[y].VertexFormat.Elements[x].ArrayIndex != 0 &&
                                tex.Entries[i].Submeshes[y].VertexFormat.Elements[x].ArrayIndex != 255)
                            {
                                string sdfsdf = "";
                            }
                        }
                    }
                }
                 
                //for (int i = 0; i < tex.Entries.Count; i++)
                //{
                //     Models.CS2 texture = texBase.Entries.FirstOrDefault(o => o.name == tex.Entries[i].name);
                //    if (texture != null) continue;
                //     texBase.Entries.Add(tex.Entries[i]);
                // }


                //Models mdl = new Models(file);
                //Console.WriteLine(mdl.Loaded + " -> " + file);
                //Console.WriteLine("Saved: " + mdl.Save());
                //return;
            }
            //texBase.Save();

            return;*/
#endif
        }

        /*
        public static void TestAllPhysMap()
        {
#if DEBUG
            List<string> files = Directory.GetFiles(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/", "LEVEL_MODELS.PAK", SearchOption.AllDirectories).ToList<string>();
            foreach (string file in files)
            {
                Models phys = new Models(file);
                Console.WriteLine("[" + phys.Loaded + "] " + file);
                for (int i = 0; i < phys.Entries.Count; i++)
                {

                }
                //phys.Save();
            }
#endif
        }
        */

        private static void WriteVert(float x, float y, float z, BinaryWriter writer)
        {
            Int16 x_16 = ((Int16)(x * Int16.MaxValue));
            Int16 y_16 = ((Int16)(y * Int16.MaxValue));
            Int16 z_16 = ((Int16)(z * Int16.MaxValue));
            writer.Write(x_16);
            writer.Write(y_16);
            writer.Write(z_16);
            writer.Write((Int16)(-Int16.MaxValue));
        }


        public static void DumpEnumList()
        {
#if DEBUG
            List<string> enumfile = File.ReadAllLines(@"C:\Users\mattf\Downloads\enums.txt").ToList<string>(); //https://myfiles.mattfiler.co.uk/enums.txt
            Dictionary<string, List<string>> outputtt = new Dictionary<string, List<string>>();
            string currenteunm = "";
            foreach (string str in enumfile)
            {
                if (str.Length > 2 && str.Substring(0, 2) == "##")
                {
                    currenteunm = str.Substring(3).Trim();
                    outputtt.Add(currenteunm, new List<string>());
                }
                if (str.Length > 2 && str.Substring(0, 2) == " *")
                {
                    outputtt[currenteunm].Add(str.Substring(3).Trim());
                }
            }
            File.WriteAllText("enums.json", JsonConvert.SerializeObject(outputtt, Formatting.Indented));

            List<string> enums = new List<string>();
            foreach (var val in outputtt)
            {
                enums.Add(val.Key);
            }
            File.WriteAllLines("enums.txt", enums);

            BinaryWriter write = new BinaryWriter(File.OpenWrite("enums.bin"));
            write.BaseStream.SetLength(0);
            foreach (var str in outputtt)
            {
                write.Write(ShortGuidUtils.Generate(str.Key).ToUInt32());
                write.Write(str.Key);
                write.Write(str.Value.Count);
                foreach (string str_ in str.Value)
                {
                    write.Write(str_);
                }
            }
            write.Close();
            return;
#endif
        }

#if DEBUG
        private class EntityDef
        {
            public string title = "";
            public Dictionary<ParameterVariant, List<ParameterDef>> stuff = new Dictionary<ParameterVariant, List<ParameterDef>>();
        }
        private class ParameterDef
        {
            public string name;
            public string datatype;
            public string defaultval;
        }
#endif


        public static void FindAllNodesInCommands(CommandsEditor editor)
        {
#if DEBUG
            /*
            List<string> mapList = Directory.GetFiles(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            for (int i = 0; i < mapList.Count; i++)
            {
                string[] fileSplit = mapList[i].Split(new[] { "PRODUCTION" }, StringSplitOptions.None);
                string mapName = fileSplit[fileSplit.Length - 1].Substring(1, fileSplit[fileSplit.Length - 1].Length - 20);
                mapList[i] = (mapName);
            }
            mapList.Remove("DLC\\BSPNOSTROMO_RIPLEY"); mapList.Remove("DLC\\BSPNOSTROMO_TWOTEAMS");

            for (int mm = 0; mm < mapList.Count; mm++)
            {
                //if (env_list.Items[mm].ToString() != "BSP_TORRENS") continue;

                editor.Editor.commands = new Commands(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/" + mapList[mm].ToString() + "/WORLD/COMMANDS.PAK");
                Console.WriteLine("Loading: " + editor.Editor.commands.Filepath + "...");
                //CurrentInstance.compositeLookup = new EntityNameLookup(CurrentInstance.commandsPAK);

                //EnvironmentAnimationDatabase db = new EnvironmentAnimationDatabase(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/" + env_list.Items[mm].ToString() + "/WORLD/ENVIRONMENT_ANIMATION.DAT");
                //Console.WriteLine(db.Header.MatrixCount0);
                //Console.WriteLine(db.Header.MatrixCount1);
                //Console.WriteLine(db.Header.EntryCount1);
                //Console.WriteLine(db.Header.EntryCount0);
                //Console.WriteLine(db.Header.IDCount0);
                //Console.WriteLine(db.Header.IDCount1);
                //Console.WriteLine(db.Header.Unknown1_);

                string[] towrite = new string[200];
                for (int i = 0; i < editor.Editor.commands.Entries.Count; i++)
                {
                    foreach (var function in editor.Editor.commands.Entries[i].functions_dictionary.Values)
                    {
                        if (!CommandsUtils.FunctionTypeExists(function.function)) continue;
                        FunctionType type = CommandsUtils.GetFunctionType(function.function);
                        switch (type)
                        {
                            case FunctionType.CameraShake:
                            case FunctionType.BoneAttachedCamera:
                            case FunctionType.CamPeek:
                            case FunctionType.ClipPlanesController:
                            case FunctionType.ControllableRange:
                            case FunctionType.FixedCamera:
                                //ResourceReference rr = CurrentInstance.commandsPAK.Composites[i].functions[x].resources.FirstOrDefault(o => o.entryType == ResourceType.COLLISION_MAPPING);
                                //if (rr == null)
                                //{
                                //    Console.WriteLine("NULL");
                                //}
                                //else
                                //{
                                //    Console.WriteLine(rr.startIndex + " / " + rr.count + " / " + rr.entityID);
                                //    //Console.WriteLine(JsonConvert.SerializeObject(rr));
                                //}

                                //if (CurrentInstance.commandsPAK.Composites[i].functions[x].resources.Count != 0)
                                //{
                                //    string breasdfdf = "";
                                //    if (CurrentInstance.commandsPAK.Composites[i].functions[x].GetResource(ResourceType.COLLISION_MAPPING) == null)
                                //    {
                                //        string sdfsd = "";
                                //    }
                                //}


                                //Console.WriteLine(CurrentInstance.commandsPAK.Composites[i].name + " -> " + CurrentInstance.commandsPAK.Composites[i].functions[x].shortGUID + " -> " +  type);
                                //for (int y = 0; y < CurrentInstance.commandsPAK.Composites[i].functions[x].resources.Count; y++)
                                //{
                                //    Console.WriteLine("\t" + CurrentInstance.commandsPAK.Composites[i].functions[x].resources[y].entryType);
                                //}


                                //Console.WriteLine(type);

                                //Console.WriteLine("");

                                //Composite comp = CurrentInstance.commandsPAK.Composites[i];
                                //List<ResourceReference> rr = ((cResource)comp.functions[x].GetParameter("resource").content).value;
                                //towrite[rr[0].startIndex] = rr[0].startIndex  + "\n\t" + comp.name + "\n\t" + CurrentInstance.compositeLookup.GetEntityName(comp.shortGUID, comp.functions[x].shortGUID);

                                //Console.WriteLine(rr.Count);


                                Console.WriteLine(editor.Editor.commands.Entries[i].name + " -> " + function.shortGUID + " -> " + type);

                                //for (int y = 0; y < CurrentInstance.commandsPAK.Composites[i].functions[x].resources.Count; y++)
                                //{
                                //    ResourceReference rr = CurrentInstance.commandsPAK.Composites[i].functions[x].resources[y];
                                //    if (rr.entryType != ResourceType.RENDERABLE_INSTANCE)
                                //    {
                                //        Console.WriteLine(" !!!!!!!  FOUND " + rr.entryType);
                                //    }
                                //    Console.WriteLine(JsonConvert.SerializeObject(rr));
                                //}
                                break;
                        }
                    }
                }
                //foreach (string line in towrite)
                //{
                //    if (line == null)
                //    {
                //        Console.WriteLine("--");
                //    }
                //    else
                //    {
                //        Console.WriteLine(line);
                //    }
                //}

                //CurrentInstance.commandsPAK.Save();
            }
            */
#endif
        }

        public static void LoadAndSaveAllCommands()
        {
#if DEBUG
            List<string> pairs = new List<string>();
            List<string> commandsFiles = Directory.GetFiles(Singleton.PathToAI + "/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories).ToList<string>();
            for (int i = 0; i < commandsFiles.Count; i++)
            {
                Console.WriteLine(commandsFiles[i]);
                Commands cmd = new Commands(commandsFiles[i]);

                /*
                foreach (Composite comp in cmd.Composites)
                {
                    int numberOfFunctionNodes = comp.functions_dictionary.Values.Count(o => CommandsUtils.FunctionTypeExists(o.function));
                    int numberOfFunctionNodesIncludingCompositeRefs = comp.functions_dictionary.Count;

                    int numberOfExcludedNodes = 0;
                    numberOfExcludedNodes += comp.functions_dictionary.Values.Count(o => o.resources.Count != 0);

                    //numberOfExcludedNodes += comp.functions.FindAll(o => o.function == CommandsUtils.GetFunctionTypeGUID(FunctionType.Zone)).Count;
                    //numberOfExcludedNodes += comp.functions.FindAll(o => o.function == CommandsUtils.GetFunctionTypeGUID(FunctionType.TriggerSequence)).Count;
                    //numberOfExcludedNodes += comp.functions.FindAll(o => o.function == CommandsUtils.GetFunctionTypeGUID(FunctionType.ParticleEmitterReference)).Count;
                    //numberOfExcludedNodes += comp.functions.FindAll(o => o.function == CommandsUtils.GetFunctionTypeGUID(FunctionType.Master)).Count; //OR LogicGate

                    //numberOfExcludedNodes += comp.functions.FindAll(o => o.function == CommandsUtils.GetFunctionTypeGUID(FunctionType.ModelReference)).Count;
                    //numberOfExcludedNodes += comp.functions.FindAll(o => o.function == CommandsUtils.GetFunctionTypeGUID(FunctionType.SoundEnvironmentMarker)).Count;
                    //numberOfExcludedNodes += comp.functions.FindAll(o => o.function == CommandsUtils.GetFunctionTypeGUID(FunctionType.LightReference)).Count;
                    //numberOfExcludedNodes += comp.functions.FindAll(o => o.function == CommandsUtils.GetFunctionTypeGUID(FunctionType.ModelReference)).Count;
                    //numberOfExcludedNodes += comp.functions.FindAll(o => o.function == CommandsUtils.GetFunctionTypeGUID(FunctionType.EnvironmentModelReference)).Count;
                    //numberOfExcludedNodes += comp.functions.FindAll(o => o.function == CommandsUtils.GetFunctionTypeGUID(FunctionType.GPU_PFXEmitterReference)).Count;
                    //numberOfExcludedNodes += comp.functions.FindAll(o => o.function == CommandsUtils.GetFunctionTypeGUID(FunctionType.RibbonEmitterReference)).Count;

                    numberOfFunctionNodes -= numberOfExcludedNodes;
                    numberOfFunctionNodesIncludingCompositeRefs -= numberOfExcludedNodes;

                    pairs.Add(comp.name + " => " + comp.unk1 + ", " + comp.unk2 + " => " + numberOfFunctionNodes + ", " + numberOfFunctionNodesIncludingCompositeRefs);

                    if (comp.unk1 != numberOfFunctionNodes || comp.unk2 != numberOfFunctionNodesIncludingCompositeRefs)
                    {
                        Dictionary<string, int> counts = new Dictionary<string, int>();
                        foreach (FunctionEntity ent in comp.functions_dictionary.Values.Where(o => CommandsUtils.FunctionTypeExists(o.function)))
                        {
                            if (!counts.ContainsKey(ent.function.ToString()))
                                counts.Add(ent.function.ToString(), 0);
                            counts[ent.function.ToString()]++;
                        }

                        string breakhere = "";
                    }
                }
                */
                cmd.Save();
            }
            string fdfsdf = "";
#endif
        }

        public static List<string> CommandsToScript(Commands cmd)
        {
#if DEBUG
            List<string> script = new List<string>();
            script.Add("Commands cmd = new Commands(\"COMMANDS.PAK\");");
            script.Add("cmd.Composites.Clear();");
            for (int i = 0; i < cmd.Entries.Count; i++)
            {
                string compositeName = "COMP_" + cmd.Entries[i].shortGUID.ToByteString().Replace('-', '_');
                script.Add("Composite " + compositeName + " = cmd.AddComposite(@\"" + cmd.Entries[i].name + "\");");

                foreach (var function in cmd.Entries[i].functions_dictionary.Values)
                {
                    string entityName = "ENT_" + function.shortGUID.ToByteString().Replace('-', '_');
                    script.Add("FunctionEntity " + entityName + " = " + compositeName + ".AddFunction(");
                    if (function.function.IsFunctionType) script[script.Count - 1] += "FunctionType." + function.function.AsFunctionType + ");";
                    else script[script.Count - 1] += "@\"" + cmd.GetComposite(function.function).name + "\");";

                    for (int y = 0; y < function.resources.Count; y++)
                    {
                        string resourceName = "RES_" + function.resources[y].GetHashCode().ToString().Replace('-', '_');
                        switch (function.resources[y].resource_type)
                        {
                            case ResourceType.RENDERABLE_INSTANCE:
                                script.Add("ResourceReference " + resourceName + " = " + entityName + ".AddResource(ResourceType." + function.resources[y].resource_type + ");");
                                Vector3 pos = function.resources[y].position;
                                script.Add(resourceName + ".position = new Vector3(" + pos.X + "f, " + pos.Y + "f, " + pos.Z + "f);");
                                Vector3 rot = function.resources[y].rotation;
                                script.Add(resourceName + ".rotation = new Vector3(" + rot.X + "f, " + rot.Y + "f, " + rot.Z + "f);");
                                script.Add(resourceName + ".index = " + function.resources[y].index + ";");
                                script.Add(resourceName + ".count = " + function.resources[y].count + ";");
                                break;
                            default:
                                throw new Exception("Unhandled resource");
                        }
                    }
                }
                foreach (var variable in cmd.Entries[i].variables_dictionary.Values)
                {
                    string entityName = "ENT_" + variable.shortGUID.ToByteString().Replace('-', '_');
                    script.Add("VariableEntity " + entityName + " = " + compositeName + ".AddVariable(\"" + ShortGuidUtils.FindString(variable.name) + "\", DataType." + variable.type.ToString() + ");");
                }
                foreach (var proxy in cmd.Entries[i].proxies_dictionary.Values)
                {
                    throw new Exception("Unhandled proxy");
                }
                foreach (var alias in cmd.Entries[i].aliases_dictionary.Values)
                {
                    throw new Exception("Unhandled alias");
                }

                List<Entity> entities = cmd.Entries[i].GetEntities();
                for (int x = 0; x < entities.Count; x++)
                {
                    string entityName = "ENT_" + entities[x].shortGUID.ToByteString().Replace('-', '_');
                    for (int y = 0; y < entities[x].parameters.Count; y++)
                    {
                        string paramName = ShortGuidUtils.FindString(entities[x].parameters[y].name);
                        script.Add(entityName + ".AddParameter(" + ((paramName == entities[x].parameters[y].name.ToByteString()) ? "new ShortGuid(\"" : "\"") + paramName + "\"" + ((paramName == entities[x].parameters[y].name.ToByteString()) ? ")" : "") + ", new ");
                        switch (entities[x].parameters[y].content.dataType)
                        {
                            case DataType.FLOAT:
                                script[script.Count - 1] += "cFloat(" + ((cFloat)entities[x].parameters[y].content).value + "f)";
                                break;
                            case DataType.BOOL:
                                script[script.Count - 1] += "cBool(" + ((cBool)entities[x].parameters[y].content).value.ToString().ToLower() + ")";
                                break;
                            case DataType.ENUM:
                                cEnum en = ((cEnum)entities[x].parameters[y].content);
                                script[script.Count - 1] += "cEnum(\"" + ShortGuidUtils.FindString(en.enumID) + "\", " + en.enumIndex + ")";
                                break;
                            case DataType.FILEPATH:
                            case DataType.STRING:
                                script[script.Count - 1] += "cString(@\"" + ((cString)entities[x].parameters[y].content).value + "\")";
                                break;
                            case DataType.INTEGER:
                                script[script.Count - 1] += "cInteger(" + ((cInteger)entities[x].parameters[y].content).value + ")";
                                break;
                            case DataType.VECTOR:
                                Vector3 vc = ((cVector3)entities[x].parameters[y].content).value;
                                script[script.Count - 1] += "cVector3(new Vector3(" + vc.X + "f, " + vc.Y + "f, " + vc.Z + "f))";
                                break;
                            case DataType.TRANSFORM:
                                Vector3 rot = ((cTransform)entities[x].parameters[y].content).rotation;
                                Vector3 pos = ((cTransform)entities[x].parameters[y].content).position;
                                script[script.Count - 1] += "cTransform(new Vector3(" + pos.X + "f, " + pos.Y + "f, " + pos.Z + "f), new Vector3(" + rot.X + "f, " + rot.Y + "f, " + rot.Z + "f))";
                                break;
                            default:
                                throw new Exception("Unhandled parameter datatype");
                        }
                        script[script.Count - 1] += ");";
                    }
                    for (int y = 0; y < entities[x].childLinks.Count; y++)
                    {
                        string connectedEntityName = "ENT_" + entities[x].childLinks[y].linkedEntityID.ToByteString().Replace('-', '_');
                        script.Add(entityName + ".AddParameterLink(\"" + ShortGuidUtils.FindString(entities[x].childLinks[y].thisParamID) + "\", " + connectedEntityName + ", \"" + ShortGuidUtils.FindString(entities[x].childLinks[y].linkedParamID) + "\");");
                    }
                }
            }
            script.Add("cmd.Save();");
            return script;
#else
            return null;
#endif
        }
#endif
    }

    public class ShortGuidConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            // This converter can only handle the ShortGuid type.
            return objectType == typeof(ShortGuid);
        }

        /// <summary>
        /// Writes the ShortGuid to JSON as its integer value.
        /// </summary>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((ShortGuid)value).ToByteString() + ": " + ((ShortGuid)value).ToString());
        }

        /// <summary>
        /// Reads an integer from JSON to create a ShortGuid.
        /// </summary>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return new ShortGuid(reader.Value.ToString().Split(':')[0]);
        }
    }
}
