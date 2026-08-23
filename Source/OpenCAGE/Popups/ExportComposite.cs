using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class ExportComposite : BaseWindow
    {
        private Composite _composite;
        private CompositeFlowgraphTable _fgLayouts;

#if DEBUG
        //Source Havok data offset to dest object, so shared proxies/systems aren't imported twice
        private readonly Dictionary<uint, uint> _collisionRemap32 = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, uint> _collisionRemap64 = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, uint> _physicsRemap32 = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, uint> _physicsRemap64 = new Dictionary<uint, uint>();
#endif

        public ExportComposite(Composite composite, bool canExportChildren) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _composite = composite;

            InitializeComponent();

            levelList.BeginUpdate();
            levelList.Items.AddRange(Level.GetLevels(Singleton.PathToAI).ToArray());
            levelList.Items.Remove(Content.Level.Name);
            levelList.EndUpdate();

            if (levelList.Items.Count > 0)
                levelList.SelectedIndex = 0;

            this.Text = "Port '" + _composite.name + "'";
            
            if (!canExportChildren)
            {
                recurse.Checked = false;
                recurse.Enabled = false;
            }

            MessageBox.Show("Warning! This is a highly experimental feature which is not yet complete. Please use with caution! Take backups of any levels you plan to copy content to.", "Warning!", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void portToAllLevels_CheckedChanged(object sender, EventArgs e)
        {
            levelList.Enabled = !portToAllLevels.Checked;
            label1.Enabled = !portToAllLevels.Checked;
        }

        private void export_Click(object sender, System.EventArgs e)
        {
            List<string> targetLevels;
            if (portToAllLevels.Checked)
            {
                string currentLevel = Content.Level.Name;
                targetLevels = Level.GetLevels(Singleton.PathToAI)
                    .Where(levelName => !string.Equals(levelName, currentLevel, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                if (levelList.SelectedItem == null)
                {
                    MessageBox.Show("Please select a destination level.", "No level selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                targetLevels = new List<string> { levelList.SelectedItem.ToString() };
            }

            if (targetLevels.Count == 0)
            {
                MessageBox.Show("There are no destination levels to port to.", "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (string levelName in targetLevels)
                PortCompositeToLevel(levelName);

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            string destinationLabel = portToAllLevels.Checked
                ? (targetLevels.Count + " levels")
                : ("'" + targetLevels[0] + "'");
            MessageBox.Show("Finished porting '" + _composite.name + "' to " + destinationLabel + "!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

            this.Close();
        }

        private void PortCompositeToLevel(string levelName)
        {
            Level lvl = new Level(Singleton.PathToAI + "/DATA/ENV/" + levelName, Singleton.Global, false);
            {
                ProgressUI loadProgress = new ProgressUI();
                loadProgress.ShowLevelLoading(lvl);
                loadProgress.BringToFront();
                lvl.Load();
                loadProgress.Close();
                loadProgress.Dispose();
            }

            _fgLayouts = (CompositeFlowgraphTable)CustomTable.ReadTable(lvl.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS);
            if (_fgLayouts == null) _fgLayouts = new CompositeFlowgraphTable();

#if DEBUG
            _collisionRemap32.Clear();
            _collisionRemap64.Clear();
            _physicsRemap32.Clear();
            _physicsRemap64.Clear();
#endif

            {
                ProgressUI exportProgress = new ProgressUI();
                exportProgress.ShowTransferring("Porting to " + levelName + "...");
                exportProgress.BringToFront();
                AddCompositesRecursively(_composite, lvl, exportProgress);
                exportProgress.Close();
                exportProgress.Dispose();
            }

            //Close alien down if it's open, it conflicts with our write locks!
            EditorUtils.CloseAI();

            {
                ProgressUI saveProgress = new ProgressUI();
#if DEBUG
                // Full re-instance required for Havok collision/physics remaps (WIP)
                saveProgress.ShowLevelSaving(lvl, true);
                saveProgress.BringToFront();
                lvl.Save(true);
#else
                saveProgress.ShowLevelSaving(lvl, false);
                saveProgress.BringToFront();
                lvl.Save(false);
#endif
                saveProgress.Close();
                saveProgress.Dispose();
            }
            CustomTable.WriteTable(lvl.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS, _fgLayouts);
        }

        private void AddCompositesRecursively(Composite composite, Level lvl, ProgressUI ui)
        {
            //Check to see if the composite already exists at our destination
            Composite dest = lvl.Commands.Entries.FirstOrDefault(o => o.shortGUID == composite.shortGUID);

            //If the user opted to overwrite & we found an existing matching comp in the destination, delete it
            if (overwrite.Checked)
            {
                if (dest != null)
                    lvl.Commands.Entries.Remove(dest);
                dest = null;
            }

            //Copy composite and bring over the resources referenced by it
            if (dest == null)
            {
                //We need to add the composite to the new location
                Composite copiedComp = composite.Copy();
                lvl.Commands.Entries.Add(copiedComp);
                ui.DoRefresh();

                foreach (FunctionEntity ent in copiedComp.functions)
                {
                    if (ent.resources != null)
                        CopyResourcesToLevel(ent, ent.resources, lvl, ui);

                    Parameter resources = ent.GetParameter("resource");
                    if (resources != null)
                        CopyResourcesToLevel(ent, ((cResource)resources.content).value, lvl, ui);
                }

                //Bring over generic metadata
                //NOTE: entity names travel with the entities themselves now (as a 'name' parameter)
                lvl.Commands.Utils.AddCustomPinInfos(copiedComp, Content.Level.Commands.Utils.GetAllCustomPinInfo(composite));
                lvl.Commands.Utils.SetModificationInfo(Content.Level.Commands.Utils.GetModificationInfo(composite));
                lvl.Commands.Utils.PurgedComposites.purged.Remove(copiedComp.shortGUID); //mark for re-purge

                //Bring over flowgraph layouts (deep-copied; includes predefined fallback)
                List<CompositeFlowgraphTable.FlowgraphMeta> layouts = FlowgraphLayoutManager.GetLayoutsForPort(composite);
                _fgLayouts.flowgraphs.RemoveAll(o => o.CompositeGUID == composite.shortGUID);
                _fgLayouts.flowgraphs.AddRange(layouts);
            }

            //If the user opted to recurse, follow any composite instances through, and copy those too
            if (!recurse.Checked) return;
            foreach (FunctionEntity ent in composite.functions)
            {
                if (ent.function.IsFunctionType) continue;

                Composite nestedComp = Content.Level.Commands.GetComposite(ent.function);
                if (nestedComp != null)
                    AddCompositesRecursively(nestedComp, lvl, ui);
            }
        }

        private void CopyResourcesToLevel(FunctionEntity hostEntity, List<ResourceReference> resourceRefs, Level lvl, ProgressUI ui)
        {
            bool overwriteDestinationAssets = overwriteAssets.Checked;

            for (int i = 0; i < resourceRefs.Count; i++)
            {
                switch (resourceRefs[i].resource_type)
                {
                    case ResourceType.ANIMATED_MODEL:
                        resourceRefs[i].AnimatedModel = lvl.EnvironmentAnimations.ImportEntry(resourceRefs[i].AnimatedModel);
                        break;
                    case ResourceType.RENDERABLE_INSTANCE:
                        resourceRefs[i].RenderableInstance = lvl.RenderableElements.ImportEntry(resourceRefs[i].RenderableInstance, Content.Level.Models, overwriteDestinationAssets);
                        break;
#if DEBUG
                    case ResourceType.COLLISION_MAPPING:
                        PortCollisionMapping(resourceRefs[i], lvl, overwriteDestinationAssets);
                        break;
                    case ResourceType.DYNAMIC_PHYSICS_SYSTEM:
                        PortDynamicPhysicsSystem(resourceRefs[i], lvl);
                        break;
#endif
                    case ResourceType.TRAVERSAL_SEGMENT:
                    case ResourceType.NAV_MESH_BARRIER_RESOURCE:
                    case ResourceType.EXCLUSIVE_MASTER_STATE_RESOURCE:
                        break;
                    default:
                        Debug.Log("Porting", "Skipping resource type: " + resourceRefs[i].resource_type.ToString());
                        break;
                }
                ui.DoRefresh();
            }
        }

#if DEBUG
        private void PortCollisionMapping(ResourceReference resource, Level destLevel, bool overwriteDestinationAssets)
        {
            CollisionMaps.COLLISION_MAPPING srcMap = resource.CollisionMapping;
            HavokPackfile.StaticCompoundShape remappedProxy = null;
            if (srcMap?.CollisionProxy != null)
                remappedProxy = ImportCollisionProxyPair(srcMap.CollisionProxy, destLevel);
            resource.CollisionMapping = destLevel.CollisionMaps.ImportEntry(srcMap, remappedProxy, overwriteDestinationAssets);
        }

        private HavokPackfile.StaticCompoundShape ImportCollisionProxyPair(HavokPackfile.StaticCompoundShape sourceProxy, Level destLevel)
        {
            if (sourceProxy == null)
                return null;

            HavokPackfile src32 = Content.Level.CollisionHKX;
            HavokPackfile src64 = Content.Level.CollisionHKX64;
            HavokPackfile dst32 = destLevel.CollisionHKX;
            HavokPackfile dst64 = destLevel.CollisionHKX64;

            HavokPackfile.StaticCompoundShape imported32 = null;
            if (src32 != null && dst32 != null)
                imported32 = dst32.ImportStaticCompoundShape(src32, sourceProxy, _collisionRemap32);
            else if (src32 != null && dst32 == null)
                Debug.Log("Porting", "Destination level has no COLLISION.HKX — cannot import collision proxy.");

            if (src64 != null && dst64 != null)
            {
                HavokPackfile.StaticCompoundShape source64 = src64.GetCompound(sourceProxy.ProxyIndex);
                if (source64 != null)
                {
                    try
                    {
                        dst64.ImportStaticCompoundShape(src64, source64, _collisionRemap64);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("Porting", "COLLISION.HKX64 import failed: " + ex.Message);
                    }
                }
                else
                {
                    Debug.Log("Porting", "No matching COLLISION.HKX64 compound for proxy " + sourceProxy.ProxyIndex);
                }
            }

            return imported32;
        }

        private void PortDynamicPhysicsSystem(ResourceReference resource, Level destLevel)
        {
            HavokPackfile.PhysicsSystem srcSystem = resource.PhysicsSystem;
            if (srcSystem == null && resource.PhysicsSystemIndex >= 0)
                srcSystem = Content.Level.Physics?.GetPhysicsSystem(resource.PhysicsSystemIndex);

            if (srcSystem == null)
            {
                Debug.Log("Porting", "DYNAMIC_PHYSICS_SYSTEM has no bound PhysicsSystem — leaving as-is.");
                return;
            }

            HavokPackfile.PhysicsSystem imported = ImportPhysicsSystemPair(srcSystem, destLevel);
            resource.PhysicsSystem = imported;
            resource.PhysicsSystemIndex = imported?.SystemIndex ?? -1;
        }

        private HavokPackfile.PhysicsSystem ImportPhysicsSystemPair(HavokPackfile.PhysicsSystem sourceSystem, Level destLevel)
        {
            if (sourceSystem == null)
                return null;

            HavokPackfile src32 = Content.Level.PhysicsHKX;
            HavokPackfile src64 = Content.Level.PhysicsHKX64;
            HavokPackfile dst32 = destLevel.PhysicsHKX;
            HavokPackfile dst64 = destLevel.PhysicsHKX64;

            HavokPackfile.PhysicsSystem imported32 = null;
            if (src32 != null && dst32 != null)
                imported32 = dst32.ImportPhysicsSystem(src32, sourceSystem, _physicsRemap32);
            else if (src32 != null && dst32 == null)
                Debug.Log("Porting", "Destination level has no PHYSICS.HKX — cannot import physics system.");

            if (src64 != null && dst64 != null)
            {
                HavokPackfile.PhysicsSystem source64 = src64.GetPhysicsSystem(sourceSystem.SystemIndex);
                if (source64 != null)
                {
                    try
                    {
                        dst64.ImportPhysicsSystem(src64, source64, _physicsRemap64);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("Porting", "PHYSICS.HKX64 import failed: " + ex.Message);
                    }
                }
                else
                {
                    Debug.Log("Porting", "No matching PHYSICS.HKX64 system for index " + sourceSystem.SystemIndex);
                }
            }

            return imported32;
        }
#endif
    }
}
