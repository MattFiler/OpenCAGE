using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CommandsEditor.DockPanels;
using CommandsEditor.Popups.Base;
using OpenCAGE;
using ST.Library.UI.NodeEditor;
using WebSocketSharp;
using static System.Net.Mime.MediaTypeNames;
using static CATHODE.SkeleDB;
using static CommandsEditor.EditorUtils;

namespace CommandsEditor
{
    public partial class ModifyParameters : BaseWindow
    {
        public Action OnSaved;

        private List<ListViewItem> _items = new List<ListViewItem>();
        private ListViewColumnSorter _sorter = new ListViewColumnSorter();

        private EntityInspector _inspector = null;

        public ModifyParameters() : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
        }

        public ModifyParameters(EntityInspector inspector) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _inspector = inspector;
            SetupUI(inspector.Entity, inspector.Composite);
        }

        private void SetupUI(Entity ent, Composite comp)
        { 
            InitializeComponent();
            param_name.ListViewItemSorter = _sorter;

            this.Text = "Modify Parameters";
            label2.Text = "Parameters";
            createParams.Text = "Set Parameters";

            List<ListViewItem> options = Singleton.Editor.CommandsDisplay.Content.EditorUtils.GenerateParameterListAsListViewItem(ent, comp);
            //Add all base-game ones
            for (int i = 0; i < options.Count; i++)
            {
                var metadata = Content.Level.Commands.Utils.GetParameterMetadata(ent, options[i].Text, comp);

                if (metadata.Item1 == null)
                    continue;

                //If the composite supports flowgraphs, we should only show a filtered list of parameters (not the event pins)
                if (Singleton.Editor.CommandsDisplay.CompositeDisplay.SupportsFlowgraphs)
                {
                    switch (metadata.Item1)
                    {
                        case ParameterVariant.REFERENCE_PIN:
                        case ParameterVariant.TARGET_PIN:
                        //case ParameterVariant.INTERNAL:  //NOTE: shouldn't really be showing internal, but until I handle some of the values better, I need to still (e.g. resources, spline points, etc)
                        case ParameterVariant.METHOD_FUNCTION:
                        case ParameterVariant.METHOD_PIN:
                        case ParameterVariant.OUTPUT_PIN:
                            continue;
                    }
                    //NOTE: This same switch case is found in EntityInspector popup window, keep in sync!
                }

                options[i].Checked = ent.GetParameter(options[i].Text) != null;
                options[i].SubItems[1].Text = metadata.Item2.Value.ToUIString();
                options[i].Group = GetGroupFromVariant(metadata.Item1.Value);
                options[i].ImageIndex = 0;
                _items.Add(options[i]);
            }
            //Add any additional custom ones
            for (int i = 0; i < ent.parameters.Count; i++)
            {
                if (options.FirstOrDefault(o => ((ParameterListViewItemTag)o.Tag).ShortGUID == ent.parameters[i].name && o.SubItems[1].Text == ent.parameters[i].content.dataType.ToUIString()) != null)
                    continue;
                AddCustomEntry(ent.parameters[i].name, ent.parameters[i].content.dataType);
            }

            AddCustom.Visible = ent.variant != EntityVariant.VARIABLE;

            Search();
        }

        private ListViewGroup GetGroupFromVariant(ParameterVariant variant)
        {
            foreach (ListViewGroup group in param_name.Groups)
                if (group.Name == variant.ToString())
                    return group;

            return null;
        }

        private void clearSearchBtn_Click(object sender, EventArgs e)
        {
            searchText.Text = "";
            Search();
        }

        private void searchText_TextChanged(object sender, EventArgs e)
        {
            Search();
        }

        private void Search()
        {
            param_name.BeginUpdate();
            param_name.Items.Clear();
            ListViewItem[] items = _items.Where(o => o.Text.ToUpper().Replace(" ", "").Contains(searchText.Text.ToUpper().Replace(" ", ""))).ToList().ToArray();
            foreach (ListViewItem item in items)
            {
                ParameterListViewItemTag tag = (ParameterListViewItemTag)item.Tag;
                item.Group = GetGroupFromVariant(tag.Usage);
                param_name.Items.Add(item);
            }
            param_name.EndUpdate();

            typesCount.Text = "Showing " + param_name.Items.Count;
        }

        private void createParams_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in _items)
            {
                ParameterListViewItemTag tag = (ParameterListViewItemTag)item.Tag;
                if (item.Checked)
                {
                    Parameter existing = _inspector.Entity.GetParameter(tag.ShortGUID);
                    DataType type = item.SubItems[1].Text.ToDataType();
                    if (existing == null || existing.content.dataType != type)
                    {
                        ParameterData data = Content.Level.Commands.Utils.CreateDefaultParameterData(_inspector.Entity, _inspector.Composite, item.Text);
                        if (data != null)
                        {
                            _inspector.Entity.AddParameter(ShortGuidUtils.Generate(item.Text), data);
                        }
                        else
                        {
                            //Data can be null if this is a custom parameter (e.g. CAGEAnimation) - use the type info from the list here instead.
                            _inspector.Entity.AddParameter(ShortGuidUtils.Generate(item.Text), item.SubItems[1].Text.ToDataType());
                        }
                        Singleton.OnParameterModified?.Invoke();
                        switch (type)
                        {
                            case DataType.RESOURCE:
                                Singleton.OnResourceModified?.Invoke();
                                break;
                            case DataType.TRANSFORM:
                                if (data != null && item.Text == "position")
                                {
                                    cTransform transformVal = (cTransform)data;
                                    Singleton.OnEntityMoved?.Invoke(transformVal, _inspector.Entity);
                                }
                                break;
                        }
                    }
                }
                else
                {
                    if (_inspector.Entity.RemoveParameter(tag.ShortGUID))
                    {
                        DataType type = item.SubItems[1].Text.ToDataType();
                        Singleton.OnParameterModified?.Invoke();
                        switch (type)
                        {
                            case DataType.RESOURCE:
                                Singleton.OnResourceModified?.Invoke();
                                break;
                            case DataType.TRANSFORM:
                                if (item.Text == "position")
                                    Singleton.OnEntityMoved?.Invoke(null, _inspector.Entity);
                                break;
                        }
                    }
                }
            }

            OnSaved?.Invoke();
            this.Close();
        }

        private void selectAll_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in param_name.Items)
            {
                item.Selected = true;
                item.Checked = true;
            }
        }

        private void deSelectAll_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in param_name.Items)
            {
                item.Selected = false;
                item.Checked = false;
            }
        }

        private void ColumnClick(object sender, System.Windows.Forms.ColumnClickEventArgs e)
        {
            // Determine if the clicked column is already the column that is being sorted.
            if (e.Column == _sorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (_sorter.Order == SortOrder.Ascending)
                {
                    _sorter.Order = SortOrder.Descending;
                }
                else
                {
                    _sorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                _sorter.SortColumn = e.Column;
                _sorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            this.param_name.Sort();
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            if (_inspector.Entity.variant == EntityVariant.FUNCTION)
            {
                FunctionEntity funcEnt = (FunctionEntity)_inspector.Entity;
                if (funcEnt.function.IsFunctionType)
                    Process.Start("https://opencage.co.uk/docs/cathode-entities/#" + ((FunctionEntity)_inspector.Entity).function.AsFunctionType.ToString());
                else
                    Process.Start("https://opencage.co.uk/docs/cathode-entities/#" + FunctionType.CompositeInterface.ToString());
                return;
            }
            else if (_inspector.Entity.variant == EntityVariant.PROXY)
                Process.Start("https://opencage.co.uk/docs/cathode-entities/#" + FunctionType.ProxyInterface.ToString());
            else
                Process.Start("https://opencage.co.uk/docs/cathode-entities/#entities");

            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
        }

        AddCustomParameter _customParam = null;
        private void AddCustom_Click(object sender, EventArgs e)
        {
            if (_customParam != null)
            {
                _customParam.OnSelected -= OnAddedCustomParam;
                _customParam.Close();
            }

            _customParam = new AddCustomParameter(_inspector);
            _customParam.Show();
            _customParam.OnSelected += OnAddedCustomParam;
        }

        private bool IsNameValid(string name)
        {
            foreach (ListViewItem existingItem in _items)
            {
                if (existingItem.Text == name)
                {
                    MessageBox.Show("The parameter '" + name + "' is already available on this Entity!\nPlease pick a new name.", "Parameter exists", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return false;
                }
            }
            return true;
        }

        private void OnAddedCustomParam(string name, DataType datatype)
        {
            if (IsNameValid(name))
                AddCustomEntry(ShortGuidUtils.Generate(name), datatype);
        }

        private void AddCustomEntry(ShortGuid guid, DataType datatype, ParameterVariant variant = ParameterVariant.PARAMETER)
        {
            string paramName = guid.ToString();

            ListViewItem item = new ListViewItem(paramName);
            item.SubItems.Add(datatype.ToUIString());
            item.Tag = new ParameterListViewItemTag() { ShortGUID = guid, Usage = variant };
            item.Checked = true;
            item.SubItems[1].Text = datatype.ToUIString();
            item.Group = GetGroupFromVariant(variant);
            item.ImageIndex = 0;
            item.Selected = true;
            _items.Add(item);

            Search();
        }
    }
}
