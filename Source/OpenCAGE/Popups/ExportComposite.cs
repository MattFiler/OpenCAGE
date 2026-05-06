using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using CommandsEditor.DockPanels;
using CommandsEditor.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace CommandsEditor
{
    public partial class ExportComposite : BaseWindow
    {
        private Composite _composite;
        private CompositeFlowgraphTable _fgLayouts;

        public ExportComposite(Composite composite, bool canExportChildren) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _composite = composite;

            InitializeComponent();

            levelList.BeginUpdate();
            levelList.Items.AddRange(Level.GetLevels(Singleton.PathToAI).ToArray());
            levelList.Items.Remove(Content.Level.Name);
            levelList.EndUpdate();

            levelList.SelectedIndex = 0;

            this.Text = "Port '" + _composite.name + "'";
            
            if (!canExportChildren)
            {
                recurse.Checked = false;
                recurse.Enabled = false;
            }

            MessageBox.Show("Warning! This is a highly experimental feature which is not yet complete. Please use with caution! Take backups of any levels you plan to copy content to.", "Warning!", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void export_Click(object sender, System.EventArgs e)
        {
            {
                Level lvl = new Level(Singleton.PathToAI + "/DATA/ENV/" + levelList.SelectedItem.ToString(), Singleton.Global, false);
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

                {
                    ProgressUI exportProgress = new ProgressUI();
                    exportProgress.ShowTransferring("Porting content...");
                    exportProgress.BringToFront();
                    AddCompositesRecursively(_composite, lvl, exportProgress);
                    exportProgress.Close();
                    exportProgress.Dispose();
                }

                //Close alien down if it's open, it conflicts with our write locks!
                EditorUtils.CloseAI();

                {
                    ProgressUI saveProgress = new ProgressUI();
                    saveProgress.ShowLevelSaving(lvl);
                    saveProgress.BringToFront();
                    lvl.Save();
                    saveProgress.Close();
                    saveProgress.Dispose();
                }
                CustomTable.WriteTable(lvl.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS, _fgLayouts);
            }
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            MessageBox.Show("Finished porting '" + _composite.name + "' to '" + levelList.SelectedItem.ToString() + "'!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

            this.Close();
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
                        CopyResourcesToLevel(ent.resources, lvl, ui);

                    Parameter resources = ent.GetParameter("resource");
                    if (resources != null)
                        CopyResourcesToLevel(((cResource)resources.content).value, lvl, ui);
                }

                //Bring over generic metadata
                lvl.Commands.Utils.AddCustomEntityNames(copiedComp, Content.Level.Commands.Utils.GetAllCustomEntityNames(composite));
                lvl.Commands.Utils.AddCustomPinInfos(copiedComp, Content.Level.Commands.Utils.GetAllCustomPinInfo(composite));
                lvl.Commands.Utils.SetModificationInfo(Content.Level.Commands.Utils.GetModificationInfo(composite));
                lvl.Commands.Utils.PurgedComposites.purged.Remove(copiedComp.shortGUID); //mark for re-purge

                //Bring over flowgraph layouts
                List<CompositeFlowgraphTable.FlowgraphMeta> layouts = FlowgraphLayoutManager.GetLayouts(composite);
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

        private void CopyResourcesToLevel(List<ResourceReference> resourceRefs, Level lvl, ProgressUI ui)
        {
            for (int i = 0; i < resourceRefs.Count; i++)
            {
                switch (resourceRefs[i].resource_type)
                {
                    case ResourceType.ANIMATED_MODEL:
                        int resourceIndex = lvl.EnvironmentAnimations.Entries.Count;
                        resourceRefs[i].AnimatedModel = lvl.EnvironmentAnimations.ImportEntry(resourceRefs[i].AnimatedModel);
                        resourceRefs[i].AnimatedModel.ResourceIndex = resourceIndex; //TODO: would be good to just handle this at build time
                        break;
                    case ResourceType.RENDERABLE_INSTANCE:
                        resourceRefs[i].RenderableInstance = lvl.RenderableElements.ImportEntry(resourceRefs[i].RenderableInstance, Content.Level.Models);
                        break;
                    case ResourceType.COLLISION_MAPPING:
                        resourceRefs[i].CollisionMapping = lvl.CollisionMaps.ImportEntry(resourceRefs[i].CollisionMapping);
                        break;
                    default:
                        Debug.Log("Porting", "Skipping resource type: " + resourceRefs[i].resource_type.ToString());
                        break;
                }
                ui.DoRefresh();
            }
        }
    }
}
