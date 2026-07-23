using CATHODE.Animations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.AnimTrees
{
    public partial class AnimationNodeEditor : DockContent
    {
        private AnimationNode _currentNode;
        private bool _isUpdating = false;
        private Dictionary<int, (string collectionName, int index, string elementType)> _arrayItemLookup = new Dictionary<int, (string, int, string)>();
        private Dictionary<int, (string propertyName, BoneMaskGroups flag)> _boneMaskFlagLookup = new Dictionary<int, (string, BoneMaskGroups)>();

        public AnimationNodeEditor()
        {
            InitializeComponent();
        }

        public bool PopulateData(AnimationNode node)
        {
            _isUpdating = true;
            _currentNode = node;

            if (node == null)
            {
                this.Text = "";
                dataGridView1.Rows.Clear();
                _isUpdating = false;
                return false;
            }

            this.Text = node.Name + " [" + node.Type.ToString() + "]";
            dataGridView1.Rows.Clear();
            _arrayItemLookup.Clear();
            _boneMaskFlagLookup.Clear();

            switch (node.Type)
            {
                case NodeType.ANIM_Tree_Top_Level:
                    PopulateAnimationTreeProperties((AnimationTree)node);
                    break;
                case NodeType.ANIM_Animation:
                    PopulateLeafNodeProperties((LeafNode)node);
                    break;
                case NodeType.ANIM_Randomised_Animation:
                    PopulateRandomisedLeafNodeProperties((RandomisedLeafNode)node);
                    break;
                case NodeType.ANIM_Metadata_Event_Listener:
                    PopulateMetadataListenerNodeProperties((MetadataListenerNode)node);
                    break;
                case NodeType.ANIM_Parameter:
                    PopulateParameterNodeProperties((ParameterNode)node);
                    break;
                case NodeType.ANIM_FloatInterpolator:
                    PopulateFloatInterpolatorNodeProperties((FloatInterpolatorNode)node);
                    break;
                case NodeType.ANIM_Property:
                    PopulatePropertyNodeProperties((PropertyNode)node);
                    break;
                case NodeType.ANIM_Property_Listener:
                    PopulatePropertyListenerNodeProperties((PropertyListenerNode)node);
                    break;
                case NodeType.ANIM_Enumerated_Selector:
                case NodeType.ANIM_Selector:
                    PopulateSelectorNodeProperties((SelectorNode)node);
                    break;
                case NodeType.ANIM_Parametric:
                    PopulateParametricNodeProperties((ParametricNode)node);
                    break;
                case NodeType.ANIM_2DParametric:
                    PopulateBlendSetNodeProperties((Parametric2DNode)node);
                    break;
                case NodeType.ANIM_3DParametric:
                    PopulateBlendSetNodeProperties((Parametric3DNode)node);
                    break;
                case NodeType.ANIM_4DParametric:
                    PopulateBlendSet4DNodeProperties((Parametric4DNode)node);
                    break;
                case NodeType.ANIM_Additive_Blend:
                    PopulateAdditiveBlendNodeProperties((AdditiveBlendNode)node);
                    break;
                case NodeType.ANIM_Parametric_Additive_Blend:
                    PopulateParametricAdditiveBlendNodeProperties((ParametricAdditiveBlendNode)node);
                    break;
                case NodeType.ANIM_Ranged_Selector:
                    PopulateRangedSelectorNodeProperties((RangedSelectorNode)node);
                    break;
                case NodeType.ANIM_Foot_Sync_Selector:
                    PopulateFootSyncSelectorNodeProperties((FootSyncSelectorNode)node);
                    break;
                case NodeType.ANIM_Bone_Mask:
                    PopulateBoneMaskNodeProperties((BoneMaskNode)node);
                    break;
                case NodeType.ANIM_IK:
                    PopulateIkNodeProperties((IkNode)node);
                    break;
                case NodeType.ANIM_Weighted:
                    PopulateWeightedNodeProperties((WeightedNode)node);
                    break;
            }
            
            _isUpdating = false;
            return true;
        }

        public void RefreshCurrentNode()
        {
            if (_currentNode != null)
            {
                PopulateData(_currentNode);
            }
        }

        public void CommitPendingEdits()
        {
            if (dataGridView1.IsCurrentCellInEditMode)
                dataGridView1.EndEdit();
            dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdating || _currentNode == null || e.RowIndex < 0 || e.ColumnIndex != 1) 
                return;

            try
            {
                string propertyName = dataGridView1.Rows[e.RowIndex].Cells[0].Value?.ToString();
                object cellValue = dataGridView1.Rows[e.RowIndex].Cells[1].Value;

                if (string.IsNullOrEmpty(propertyName) || propertyName.StartsWith("  ")) 
                    return;

                // Skip header rows that have "Flags:" in their value (these are BoneMaskGroups headers)
                string cellValueStr = cellValue?.ToString() ?? "";
                if (cellValueStr.StartsWith("Flags:"))
                    return;

                string newValue = GetStringValueFromCell(cellValue, "string");
                SetPropertyValue(_currentNode, propertyName, newValue, "string");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating property: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dataGridView1_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex != 1)
            {
                e.Cancel = true;
                return;
            }

            string propertyName = dataGridView1.Rows[e.RowIndex].Cells[0].Value?.ToString();
            if (propertyName == "NodeType" || propertyName.Contains("Count:") || propertyName.Contains("... (showing first"))
            {
                e.Cancel = true;
            }
        }

        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 1 || e.RowIndex < 0)
                return;

            string propertyName = dataGridView1.Rows[e.RowIndex].Cells[0].Value?.ToString();
            if (propertyName == "NodeType" || propertyName.Contains("Count:") || propertyName.Contains("... (showing first"))
                return;

            // BoneMaskGroups are now handled as individual checkboxes, no special handling needed

            dataGridView1.BeginEdit(true);
        }

        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdating || _currentNode == null || e.RowIndex < 0 || e.ColumnIndex != 1)
                return;

            try
            {
                string propertyName = dataGridView1.Rows[e.RowIndex].Cells[0].Value?.ToString();
                object cellValue = dataGridView1.Rows[e.RowIndex].Cells[1].Value;

                if (string.IsNullOrEmpty(propertyName))
                    return;

                // Skip header rows that have "Flags:" in their value (these are BoneMaskGroups headers)
                string cellValueStr = cellValue?.ToString() ?? "";
                if (cellValueStr.StartsWith("Flags:"))
                    return;

                if (_arrayItemLookup.ContainsKey(e.RowIndex))
                {
                    var (collectionName, index, elementType) = _arrayItemLookup[e.RowIndex];
                    string newValue = GetStringValueFromCell(cellValue, elementType);
                    SetArrayItemValue(_currentNode, collectionName, index, newValue, elementType);
                }
                else if (_boneMaskFlagLookup.ContainsKey(e.RowIndex))
                {
                    var (propName, flag) = _boneMaskFlagLookup[e.RowIndex];
                    bool isChecked = (bool)cellValue;
                    UpdateBoneMaskGroupsFlag(_currentNode, propName, flag, isChecked);
                }
                else if (!propertyName.StartsWith("  ")) // Skip collection headers
                {
                    string newValue = GetStringValueFromCell(cellValue, "string");
                    SetPropertyValue(_currentNode, propertyName, newValue, "string");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating property: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_isUpdating || _currentNode == null)
                return;

            DataGridView dgv = sender as DataGridView;
            if (dgv.CurrentCell == null || dgv.CurrentCell.ColumnIndex != 1)
                return;

            try
            {
                dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);

                string propertyName = dgv.Rows[dgv.CurrentCell.RowIndex].Cells[0].Value?.ToString();
                object cellValue = dgv.Rows[dgv.CurrentCell.RowIndex].Cells[1].Value;

                if (string.IsNullOrEmpty(propertyName))
                    return;

                string cellValueStr = cellValue?.ToString() ?? "";
                if (cellValueStr.StartsWith("Flags:"))
                    return;

                if (_arrayItemLookup.ContainsKey(dgv.CurrentCell.RowIndex))
                {
                    var (collectionName, index, elementType) = _arrayItemLookup[dgv.CurrentCell.RowIndex];
                    string newValue = GetStringValueFromCell(cellValue, elementType);
                    SetArrayItemValue(_currentNode, collectionName, index, newValue, elementType);
                }
                else if (_boneMaskFlagLookup.ContainsKey(dgv.CurrentCell.RowIndex))
                {
                    var (propName, flag) = _boneMaskFlagLookup[dgv.CurrentCell.RowIndex];
                    bool isChecked = (bool)cellValue;
                    UpdateBoneMaskGroupsFlag(_currentNode, propName, flag, isChecked);
                }
                else if (!propertyName.StartsWith("  ")) 
                {
                    string newValue = GetStringValueFromCell(cellValue, "string");
                    SetPropertyValue(_currentNode, propertyName, newValue, "string");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating property: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetArrayItemValue(AnimationNode node, string collectionName, int index, string value, string elementType)
        {
            if (string.IsNullOrEmpty(value) || value == "null")
                return;

            try
            {
                Type nodeType = node.GetType();
                PropertyInfo property = nodeType.GetProperty(collectionName, BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                {
                    var allProperties = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    property = allProperties.FirstOrDefault(p => string.Equals(p.Name, collectionName, StringComparison.OrdinalIgnoreCase));
                }
                
                if (property != null)
                {
                    var collection = property.GetValue(node) as System.Collections.IList;
                    if (collection != null && index < collection.Count)
                    {
                        object convertedValue = ConvertValue(value, elementType, GetElementType(property.PropertyType), collectionName);
                        collection[index] = convertedValue;
                    }
                }
                else
                {
                    FieldInfo field = nodeType.GetField(collectionName, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null)
                    {
                        var allFields = nodeType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                        field = allFields.FirstOrDefault(f => string.Equals(f.Name, collectionName, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    if (field != null)
                    {
                        var collection = field.GetValue(node) as System.Collections.IList;
                        if (collection != null && index < collection.Count)
                        {
                            object convertedValue = ConvertValue(value, elementType, GetElementType(field.FieldType), collectionName);
                            collection[index] = convertedValue;
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Collection '{collectionName}' not found on type {nodeType.Name}.", "Collection Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set array item '{collectionName}[{index}]': {ex.Message}", ex);
            }
        }

        private Type GetElementType(Type collectionType)
        {
            if (collectionType.IsGenericType)
                return collectionType.GetGenericArguments()[0];
            if (collectionType.IsArray)
                return collectionType.GetElementType();

            return typeof(object);
        }

        private string FormatFloatValue(float value)
        {
            return value.ToString("G9");
        }

        private string GetStringValueFromCell(object cellValue, string propertyType)
        {
            if (cellValue == null)
                return "null";

            switch (propertyType.ToLower())
            {
                case "bool":
                    if (cellValue is bool boolValue)
                        return boolValue.ToString();
                    return cellValue.ToString();
                case "animationblendnodetype":
                case "bonemaskgroups":
                case "animtreeparametertype":
                case "parameterblendusage":
                case "poselayer":
                case "iksolvertype":
                case "ikcontroltarget":
                case "footstrikeselectionmethod":
                    return cellValue.ToString();
                default:
                    return cellValue.ToString();
            }
        }

        private void AddPropertyRow(string propertyName, string value, string type)
        {
            int rowIndex = dataGridView1.Rows.Add(propertyName, value);
            
            if (propertyName == "NodeType" || propertyName.Contains("Count:") || propertyName.Contains("... (showing first"))
            {
                dataGridView1.Rows[rowIndex].Cells[1].ReadOnly = true;
                dataGridView1.Rows[rowIndex].Cells[1].Style.BackColor = Color.LightGray;
            }
            else
            {
                SetupCellControl(rowIndex, type, value, propertyName);
            }
        }

        private void SetupCellControl(int rowIndex, string type, string value, string propertyName = null)
        {
            DataGridViewCell cell = dataGridView1.Rows[rowIndex].Cells[1];
            if (propertyName == null)
                propertyName = dataGridView1.Rows[rowIndex].Cells[0].Value?.ToString();
            
            switch (type.ToLower())
            {
                case "bool":
                    cell = new DataGridViewCheckBoxCell();
                    cell.Value = bool.Parse(value);
                    cell.ReadOnly = false;
                    dataGridView1.Rows[rowIndex].Cells[1] = cell;
                    break;
                    
                case "animationblendnodetype":
                    cell = new DataGridViewComboBoxCell();
                    var nodeTypeCombo = (DataGridViewComboBoxCell)cell;
                    nodeTypeCombo.DataSource = Enum.GetNames(typeof(NodeType));
                    nodeTypeCombo.Value = value;
                    cell.ReadOnly = false;
                    dataGridView1.Rows[rowIndex].Cells[1] = cell;
                    break;
                    
                case "bonemaskgroups":
                    cell.ReadOnly = false;
                    break;
                    
                case "animtreeparametertype":
                    cell = new DataGridViewComboBoxCell();
                    var paramTypeCombo = (DataGridViewComboBoxCell)cell;
                    paramTypeCombo.DataSource = Enum.GetNames(typeof(AnimTreeParameterType));
                    paramTypeCombo.Value = value;
                    cell.ReadOnly = false;
                    dataGridView1.Rows[rowIndex].Cells[1] = cell;
                    break;
                    
                case "parameterblendusage":
                    cell = new DataGridViewComboBoxCell();
                    var blendUsageCombo = (DataGridViewComboBoxCell)cell;
                    blendUsageCombo.DataSource = Enum.GetNames(typeof(ParameterBlendUsage));
                    blendUsageCombo.Value = value;
                    cell.ReadOnly = false;
                    dataGridView1.Rows[rowIndex].Cells[1] = cell;
                    break;
                    
                case "poselayer":
                    cell = new DataGridViewComboBoxCell();
                    var poseLayerCombo = (DataGridViewComboBoxCell)cell;
                    poseLayerCombo.DataSource = Enum.GetNames(typeof(PoseLayer));
                    poseLayerCombo.Value = value;
                    cell.ReadOnly = false;
                    dataGridView1.Rows[rowIndex].Cells[1] = cell;
                    break;
                    
                case "iksolvertype":
                    cell = new DataGridViewComboBoxCell();
                    var ikSolverCombo = (DataGridViewComboBoxCell)cell;
                    ikSolverCombo.DataSource = Enum.GetNames(typeof(IkSolverType));
                    ikSolverCombo.Value = value;
                    cell.ReadOnly = false;
                    dataGridView1.Rows[rowIndex].Cells[1] = cell;
                    break;
                    
                case "ikcontroltarget":
                    cell = new DataGridViewComboBoxCell();
                    var ikTargetCombo = (DataGridViewComboBoxCell)cell;
                    ikTargetCombo.DataSource = Enum.GetNames(typeof(IkControlTarget));
                    ikTargetCombo.Value = value;
                    cell.ReadOnly = false;
                    dataGridView1.Rows[rowIndex].Cells[1] = cell;
                    break;
                    
                case "footstrikeselectionmethod":
                    cell = new DataGridViewComboBoxCell();
                    var footStrikeCombo = (DataGridViewComboBoxCell)cell;
                    footStrikeCombo.DataSource = Enum.GetNames(typeof(FootStrikeSelectionMethod));
                    footStrikeCombo.Value = value;
                    cell.ReadOnly = false;
                    dataGridView1.Rows[rowIndex].Cells[1] = cell;
                    break;
                    
                case "uint":
                    cell.Style.BackColor = Color.LightYellow;
                    cell.ReadOnly = false;
                    break;
                    
                case "int":
                case "float":
                case "byte":
                    cell.Style.BackColor = Color.LightYellow;
                    cell.ReadOnly = false;
                    break;
                    
                case "string":
                    cell.ReadOnly = false;
                    break;
            }
        }

        private void AddCollectionProperty(string propertyName, object collection, string elementType)
        {
            if (collection == null)
            {
                AddPropertyRow(propertyName, "null", elementType + "[]");
                return;
            }

            if (collection is System.Collections.ICollection col)
            {
                int headerRowIndex = dataGridView1.Rows.Add(propertyName, $"Count: {col.Count}");
                dataGridView1.Rows[headerRowIndex].Cells[1].ReadOnly = true;
                dataGridView1.Rows[headerRowIndex].Cells[1].Style.BackColor = Color.LightBlue;
                dataGridView1.Rows[headerRowIndex].DefaultCellStyle.Font = new Font(dataGridView1.DefaultCellStyle.Font, FontStyle.Bold);
                
                if (col.Count <= 10)
                {
                    int index = 0;
                    foreach (var item in col)
                    {
                        int itemRowIndex = dataGridView1.Rows.Add($"  └─ [{index}]", item?.ToString() ?? "null");
                        _arrayItemLookup[itemRowIndex] = (propertyName, index, elementType);
                        SetupCellControl(itemRowIndex, elementType, item?.ToString() ?? "null", $"  └─ [{index}]");
                        index++;
                    }
                }
                else
                {
                    int infoRowIndex = dataGridView1.Rows.Add("  └─ [0..9]", "... (showing first 10 items)");
                    dataGridView1.Rows[infoRowIndex].Cells[1].ReadOnly = true;
                    dataGridView1.Rows[infoRowIndex].Cells[1].Style.BackColor = Color.LightGray;
                    
                    int index = 0;
                    foreach (var item in col)
                    {
                        if (index >= 10) break;
                        int itemRowIndex = dataGridView1.Rows.Add($"  └─ [{index}]", item?.ToString() ?? "null");
                        _arrayItemLookup[itemRowIndex] = (propertyName, index, elementType);
                        SetupCellControl(itemRowIndex, elementType, item?.ToString() ?? "null", $"  └─ [{index}]");
                        index++;
                    }
                }
            }
        }

        private void SetPropertyValue(AnimationNode node, string propertyName, string value, string propertyType)
        {
            if (string.IsNullOrEmpty(value) || value == "null")
                return;

            try
            {
                if (propertyName == "NodeName")
                {
                    node.Name = value;
                    return;
                }

                if (propertyName == "NodeType" || propertyName.StartsWith("  ") || propertyName.Contains("Count:"))
                    return;

                Type nodeType = node.GetType();
                PropertyInfo property = nodeType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    var allProperties = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    property = allProperties.FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
                }
                
                if (property != null)
                {
                    if (!property.CanWrite)
                    {
                        MessageBox.Show($"Property '{propertyName}' exists but is read-only on type {nodeType.Name}.", "Read-only Property", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    
                    object convertedValue = ConvertValue(value, propertyType, property.PropertyType, propertyName);
                    property.SetValue(node, convertedValue);
                }
                else
                {
                    FieldInfo field = nodeType.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null)
                    {
                        var allFields = nodeType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                        field = allFields.FirstOrDefault(f => string.Equals(f.Name, propertyName, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    if (field != null)
                    {
                        if (field.IsInitOnly)
                        {
                            MessageBox.Show($"Field '{propertyName}' exists but is read-only on type {nodeType.Name}.", "Read-only Field", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        
                        object convertedValue = ConvertValue(value, propertyType, field.FieldType, propertyName);
                        field.SetValue(node, convertedValue);
                    }
                    else
                    {
                        var allProperties = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        var allFields = nodeType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                        string available = string.Join(", ", allProperties.Select(p => p.Name).Concat(allFields.Select(f => f.Name)));
                        
                        MessageBox.Show($"Property/Field '{propertyName}' does not exist on type {nodeType.Name}.\nAvailable: {available}", "Property/Field Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set property '{propertyName}': {ex.Message}", ex);
            }
        }

        private object ConvertValue(string value, string displayType, Type targetType, string propertyName = null)
        {
            try
            {
                if (targetType == typeof(string))
                    return value;

                if (targetType == typeof(bool))
                {
                    if (bool.TryParse(value, out bool boolResult))
                        return boolResult;
                    throw new FormatException($"Invalid boolean value: '{value}'. Use 'True' or 'False'.");
                }

                if (targetType == typeof(int))
                {
                    if (int.TryParse(value, out int intResult))
                        return intResult;
                    throw new FormatException($"Invalid integer value: '{value}'.");
                }

                if (targetType == typeof(uint))
                {
                    if (uint.TryParse(value, out uint uintResult))
                        return uintResult;
                    throw new FormatException($"Invalid unsigned integer value: '{value}'. Must be a positive number.");
                }

                if (targetType == typeof(float))
                {
                    if (float.TryParse(value, out float floatResult))
                        return floatResult;
                    throw new FormatException($"Invalid float value: '{value}'. Use decimal format (e.g., '1.5').");
                }

                if (targetType == typeof(byte))
                {
                    if (byte.TryParse(value, out byte byteResult))
                        return byteResult;
                    throw new FormatException($"Invalid byte value: '{value}'. Must be between 0 and 255.");
                }

                if (targetType == typeof(NodeType))
                {
                    if (Enum.TryParse<NodeType>(value, out NodeType nodeTypeResult))
                        return nodeTypeResult;
                    throw new FormatException($"Invalid AnimationBlendNodeType: '{value}'. Valid values: {string.Join(", ", Enum.GetNames(typeof(NodeType)))}");
                }

                if (targetType == typeof(BoneMaskGroups))
                {
                    if (string.IsNullOrEmpty(value) || value == "None")
                        return BoneMaskGroups.NONE;
                        
                    if (Enum.TryParse<BoneMaskGroups>(value, out BoneMaskGroups boneMaskResult))
                        return boneMaskResult;
                       
                    if (value.Contains(","))
                    {
                        BoneMaskGroups combinedMask = BoneMaskGroups.NONE;
                        string[] flagNames = value.Split(',').Select(s => s.Trim()).ToArray();
                        
                        foreach (string flagName in flagNames)
                        {
                            if (Enum.TryParse<BoneMaskGroups>(flagName, out BoneMaskGroups flag))
                            {
                                combinedMask |= flag;
                            }
                            else
                            {
                                throw new FormatException($"Invalid BoneMaskGroups flag: '{flagName}'. Valid values: {string.Join(", ", Enum.GetNames(typeof(BoneMaskGroups)))}");
                            }
                        }
                        
                        return combinedMask;
                    }
                    
                    throw new FormatException($"Invalid BoneMaskGroups: '{value}'. Valid values: {string.Join(", ", Enum.GetNames(typeof(BoneMaskGroups)))}");
                }

                if (targetType == typeof(AnimTreeParameterType))
                {
                    if (Enum.TryParse<AnimTreeParameterType>(value, out AnimTreeParameterType paramTypeResult))
                        return paramTypeResult;
                    throw new FormatException($"Invalid AnimTreeParameterType: '{value}'. Valid values: {string.Join(", ", Enum.GetNames(typeof(AnimTreeParameterType)))}");
                }

                if (targetType == typeof(ParameterBlendUsage))
                {
                    if (Enum.TryParse<ParameterBlendUsage>(value, out ParameterBlendUsage blendUsageResult))
                        return blendUsageResult;
                    throw new FormatException($"Invalid ParameterBlendUsage: '{value}'. Valid values: {string.Join(", ", Enum.GetNames(typeof(ParameterBlendUsage)))}");
                }

                if (targetType == typeof(PoseLayer))
                {
                    if (Enum.TryParse<PoseLayer>(value, out PoseLayer poseLayerResult))
                        return poseLayerResult;
                    throw new FormatException($"Invalid PoseLayer: '{value}'. Valid values: {string.Join(", ", Enum.GetNames(typeof(PoseLayer)))}");
                }

                if (targetType == typeof(IkSolverType))
                {
                    if (Enum.TryParse<IkSolverType>(value, out IkSolverType ikSolverResult))
                        return ikSolverResult;
                    throw new FormatException($"Invalid IkSolverType: '{value}'. Valid values: {string.Join(", ", Enum.GetNames(typeof(IkSolverType)))}");
                }

                if (targetType == typeof(IkControlTarget))
                {
                    if (Enum.TryParse<IkControlTarget>(value, out IkControlTarget ikTargetResult))
                        return ikTargetResult;
                    throw new FormatException($"Invalid IkControlTarget: '{value}'. Valid values: {string.Join(", ", Enum.GetNames(typeof(IkControlTarget)))}");
                }

                if (targetType == typeof(FootStrikeSelectionMethod))
                {
                    if (Enum.TryParse<FootStrikeSelectionMethod>(value, out FootStrikeSelectionMethod footStrikeResult))
                        return footStrikeResult;
                    throw new FormatException($"Invalid FootStrikeSelectionMethod: '{value}'. Valid values: {string.Join(", ", Enum.GetNames(typeof(FootStrikeSelectionMethod)))}");
                }

                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    Type underlyingType = Nullable.GetUnderlyingType(targetType);
                    if (value == "null")
                        return null;
                    return ConvertValue(value, displayType, underlyingType);
                }

                if (targetType.IsEnum)
                {
                    try
                    {
                        return Enum.Parse(targetType, value);
                    }
                    catch (ArgumentException)
                    {
                        throw new FormatException($"Invalid enum value: '{value}'. Valid values: {string.Join(", ", Enum.GetNames(targetType))}");
                    }
                }

                throw new NotSupportedException($"Cannot convert value '{value}' to type {targetType.Name}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Conversion error: {ex.Message}", ex);
            }
        }

        #region Node Type Specific Property Methods

        private void PopulateLeafNodeProperties(LeafNode node)
        {
            AddPropertyRow("AnimationName", node.AnimationName, "string");
            AddPropertyRow("Looping", node.Looping.ToString(), "bool");
            AddPropertyRow("Mirrored", node.Mirrored.ToString(), "bool");
            AddBoneMaskGroupsProperties(node, "Mask", node.Mask);
            AddPropertyRow("ConvergeOrientation", node.ConvergeOrientation.ToString(), "bool");
            AddPropertyRow("ConvergeTranslation", node.ConvergeTranslation.ToString(), "bool");
            AddPropertyRow("NotifyTimeOffset", FormatFloatValue(node.NotifyTimeOffset), "float");
            AddPropertyRow("StartTimeOffset", FormatFloatValue(node.StartTimeOffset), "float");
            AddPropertyRow("EndTimeOffset", FormatFloatValue(node.EndTimeOffset), "float");
        }

        private void PopulateRandomisedLeafNodeProperties(RandomisedLeafNode node)
        {
            AddPropertyRow("Looping", node.Looping.ToString(), "bool");
            AddPropertyRow("NewSelectionOnLoop", node.NewSelectionOnLoop.ToString(), "bool");
            AddPropertyRow("BlendTime", FormatFloatValue(node.BlendTime), "float");
            AddPropertyRow("ConvergeOrientation", node.ConvergeOrientation.ToString(), "bool");
            AddPropertyRow("ConvergeTranslation", node.ConvergeTranslation.ToString(), "bool");
            AddCollectionProperty("AnimationPool", node.AnimationPool, "RandomisedLeafNode.Animation"); //todo ; this doesnt display properly
        }

        private void PopulateSelectorNodeProperties(SelectorNode node)
        {
            AddPropertyRow("EaseSelectionTime", FormatFloatValue(node.EaseSelectionTime), "float");
            AddPropertyRow("ResetPlaybackOnChangeSelection", node.ResetPlaybackOnChangeSelection.ToString(), "bool");
            AddCollectionProperty("States", node.States, "SelectorNode.State");
        }

        private void PopulateParametricNodeProperties(ParametricNode node)
        {
            AddCollectionProperty("States", node.States, "ParametricNode.State");
            AddPropertyRow("ParameterMin", FormatFloatValue(node.ParameterMin), "float");
            AddPropertyRow("ParameterMax", FormatFloatValue(node.ParameterMax), "float");
            AddPropertyRow("ParameterUsage", node.ParameterUsage.ToString(), "ParameterBlendUsage");
            AddPropertyRow("BlendProperty", node.BlendProperty, "string");
            AddPropertyRow("SyncDurations", node.SyncDurations.ToString(), "bool");
            AddPropertyRow("ExtractBlendPropertiesAutomatically", node.ExtractBlendPropertiesAutomatically.ToString(), "bool");
        }

        private void PopulateBlendSetNodeProperties(Parametric2DNode node)
        {
            AddPropertyRow("SyncBlendSet", node.SyncBlendSet.ToString(), "bool");
            AddPropertyRow("LoopBlendSet", node.LoopBlendSet.ToString(), "bool");
        }

        private void PopulateBlendSet4DNodeProperties(Parametric4DNode node)
        {
            AddPropertyRow("SyncBlendSet", node.SyncBlendSet.ToString(), "bool");
            AddPropertyRow("LoopBlendSet", node.LoopBlendSet.ToString(), "bool");
        }

        private void PopulateAdditiveBlendNodeProperties(AdditiveBlendNode node)
        {
            AddPropertyRow("AdditiveNodeWeight", FormatFloatValue(node.AdditiveNodeWeight), "float");
            AddPropertyRow("SyncAdditiveDurationToBase", node.SyncAdditiveDurationToBase.ToString(), "bool");
        }

        private void PopulateParametricAdditiveBlendNodeProperties(ParametricAdditiveBlendNode node)
        {
            AddPropertyRow("AdditiveNodeWeight", FormatFloatValue(node.AdditiveNodeWeight), "float");
            AddPropertyRow("ParameterMin", FormatFloatValue(node.ParameterMin), "float");
            AddPropertyRow("ParameterMax", FormatFloatValue(node.ParameterMax), "float");
            AddPropertyRow("SyncAdditiveDurationToBase", node.SyncAdditiveDurationToBase.ToString(), "bool");
        }

        private void PopulateRangedSelectorNodeProperties(RangedSelectorNode node)
        {
            AddPropertyRow("EaseSelectionTime", FormatFloatValue(node.EaseSelectionTime), "float");
            AddPropertyRow("ResetPlaybackOnChange", node.ResetPlaybackOnChange.ToString(), "bool");
            AddCollectionProperty("States", node.States, "RangedSelectorNode.State");
        }

        private void PopulateFootSyncSelectorNodeProperties(FootSyncSelectorNode node)
        {
            AddPropertyRow("StrikeSelectionMethod", node.StrikeSelectionMethod.ToString(), "FootStrikeSelectionMethod");
            AddPropertyRow("GaitSyncTargetOnSelect", node.GaitSyncTargetOnSelect.ToString(), "bool");
        }

        private void PopulateBoneMaskNodeProperties(BoneMaskNode node)
        {
            AddBoneMaskGroupsProperties(node, "Mask", node.Mask);
            AddPropertyRow("MaskPrecedingLayers", node.MaskPrecedingLayers.ToString(), "bool");
            AddPropertyRow("MaskFollowingLayers", node.MaskFollowingLayers.ToString(), "bool");
            AddPropertyRow("MaskSelf", node.MaskSelf.ToString(), "bool");
        }

        private void PopulateIkNodeProperties(IkNode node)
        {
            AddPropertyRow("PoseLayer", node.PoseLayer.ToString(), "PoseLayer");
            AddPropertyRow("IkType", node.IkType.ToString(), "IkSolverType");
            AddPropertyRow("Target", node.Target.ToString(), "IkControlTarget");
            AddPropertyRow("EffectorFullyEffectiveRadius", FormatFloatValue(node.EffectorFullyEffectiveRadius), "float");
            AddPropertyRow("EffectorLeastEffectiveRadius", FormatFloatValue(node.EffectorLeastEffectiveRadius), "float");
            AddPropertyRow("FalloffRate", FormatFloatValue(node.FalloffRate), "float");
            AddPropertyRow("EnforceTranslation", node.EnforceTranslation.ToString(), "bool");
            AddPropertyRow("EnforceEndBoneRotation", node.EnforceEndBoneRotation.ToString(), "bool");
        }

        private void PopulateWeightedNodeProperties(WeightedNode node)
        {
            AddPropertyRow("ParameterMin", FormatFloatValue(node.ParameterMin), "float");
            AddPropertyRow("ParameterMax", FormatFloatValue(node.ParameterMax), "float");
        }

        private void PopulateAnimationTreeProperties(AnimationTree node)
        {
            AddPropertyRow("Set", node.Set, "string");
            AddPropertyRow("TreeEaseInTime", FormatFloatValue(node.TreeEaseInTime), "float");
            AddPropertyRow("RemoveMotionExtractionOnEaseOut", node.RemoveMotionExtractionOnEaseOut.ToString(), "bool");
            AddPropertyRow("RemoveMotionExtractionOnPreceding", node.RemoveMotionExtractionOnPreceding.ToString(), "bool");
            AddPropertyRow("NeverUseMotionExtraction", node.NeverUseMotionExtraction.ToString(), "bool");
            AddPropertyRow("AllowFootIkIfPrimary", node.AllowFootIkIfPrimary.ToString(), "bool");
            AddPropertyRow("AllowHipLeanIkIfPrimary", node.AllowHipLeanIkIfPrimary.ToString(), "bool");
            AddPropertyRow("GaitSyncOnStart", node.GaitSyncOnStart.ToString(), "bool");
            AddPropertyRow("UseLinearBlend", node.UseLinearBlend.ToString(), "bool");
            AddPropertyRow("MinInitialPlayspeed", FormatFloatValue(node.MinInitialPlayspeed), "float");
            AddPropertyRow("MaxInitialPlayspeed", FormatFloatValue(node.MaxInitialPlayspeed), "float");
            AddCollectionProperty("Nodes", node.Nodes, "AnimationNode");
            AddCollectionProperty("Children", node.Children, "AnimationNode");
        }

        private void PopulateMetadataListenerNodeProperties(MetadataListenerNode node)
        {
            AddPropertyRow("EventName", node.EventName, "string");
            AddPropertyRow("WeightThreshold", FormatFloatValue(node.WeightThreshold), "float");
            AddPropertyRow("FilterTime", FormatFloatValue(node.FilterTime), "float");
        }

        private void PopulateParameterNodeProperties(ParameterNode node)
        {
            AddPropertyRow("ParameterType", node.ParameterType.ToString(), "AnimTreeParameterType");
        }

        private void PopulateFloatInterpolatorNodeProperties(FloatInterpolatorNode node)
        {
            AddPropertyRow("InitialValue", FormatFloatValue(node.InitialValue), "float");
            AddPropertyRow("UnitsPerSecond", FormatFloatValue(node.UnitsPerSecond), "float");
        }

        private void PopulatePropertyNodeProperties(PropertyNode node)
        {
            AddPropertyRow("Value", node.Value?.ToString() ?? "null", "AnimationMetadataValue");
        }

        private void PopulatePropertyListenerNodeProperties(PropertyListenerNode node)
        {
            AddPropertyRow("AnimProperty", node.AnimProperty, "string");
        }

        private void AddBoneMaskGroupsProperties(AnimationNode node, string propertyName, BoneMaskGroups currentMask)
        {
            if (currentMask == null)
            {
                currentMask = BoneMaskGroups.NONE;
            }
            
            int headerRowIndex = dataGridView1.Rows.Add(propertyName, $"Flags: {GetBoneMaskGroupsDisplayText(currentMask)}");
            dataGridView1.Rows[headerRowIndex].Cells[1].ReadOnly = true;
            dataGridView1.Rows[headerRowIndex].Cells[1].Style.BackColor = Color.LightBlue;
            dataGridView1.Rows[headerRowIndex].DefaultCellStyle.Font = new Font(dataGridView1.DefaultCellStyle.Font, FontStyle.Bold);
            
            foreach (BoneMaskGroups flag in Enum.GetValues(typeof(BoneMaskGroups)))
            {
                if (flag != BoneMaskGroups.NONE)
                {
                    try
                    {
                        string flagName = flag.ToString();
                        bool isChecked = (currentMask & flag) == flag;
                        int rowIndex = dataGridView1.Rows.Add($"  └─ {flagName}", isChecked.ToString());
                        
                        _boneMaskFlagLookup[rowIndex] = (propertyName, flag);
                        
                        var cell = new DataGridViewCheckBoxCell();
                        cell.Value = isChecked;
                        cell.ReadOnly = false;
                        dataGridView1.Rows[rowIndex].Cells[1] = cell;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error creating checkbox for flag {flag}: {ex.Message}");
                    }
                }
            }
        }
        
        private string GetBoneMaskGroupsDisplayText(BoneMaskGroups mask)
        {
            if (mask == BoneMaskGroups.NONE)
                return "None";

            var selectedFlags = new List<string>();
            foreach (BoneMaskGroups flag in Enum.GetValues(typeof(BoneMaskGroups)))
            {
                if (flag != BoneMaskGroups.NONE && (mask & flag) == flag)
                {
                    selectedFlags.Add(flag.ToString());
                }
            }

            if (selectedFlags.Count == 0)
                return "None";
            if (selectedFlags.Count <= 3)
                return string.Join(", ", selectedFlags);
            else
                return $"{selectedFlags.Count} flags selected";
        }
        
        private void UpdateBoneMaskGroupsFlag(AnimationNode node, string propertyName, BoneMaskGroups flag, bool isChecked)
        {
            try
            {
                Type nodeType = node.GetType();
                
                PropertyInfo property = nodeType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    var allProperties = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    property = allProperties.FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
                }
                
                if (property != null && property.CanWrite)
                {
                    object currentValue = property.GetValue(node);
                    BoneMaskGroups currentMask;
                    
                    if (currentValue == null)
                        currentMask = BoneMaskGroups.NONE;
                    else if (currentValue is BoneMaskGroups)
                        currentMask = (BoneMaskGroups)currentValue;
                    else
                    {
                        if (Enum.TryParse<BoneMaskGroups>(currentValue.ToString(), out BoneMaskGroups parsedMask))
                            currentMask = parsedMask;
                        else
                            currentMask = BoneMaskGroups.NONE;
                    }
                    
                    if (isChecked)
                        currentMask |= flag;
                    else
                        currentMask &= ~flag;
                    
                    property.SetValue(node, currentMask);
                    UpdateBoneMaskGroupsHeader(propertyName, currentMask);
                }
                else
                {
                    FieldInfo field = nodeType.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null)
                    {
                        var allFields = nodeType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                        field = allFields.FirstOrDefault(f => string.Equals(f.Name, propertyName, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    if (field != null && !field.IsInitOnly)
                    {
                        object currentValue = field.GetValue(node);
                        BoneMaskGroups currentMask;
                        
                        if (currentValue == null)
                            currentMask = BoneMaskGroups.NONE;
                        else if (currentValue is BoneMaskGroups)
                            currentMask = (BoneMaskGroups)currentValue;
                        else
                        {
                            if (Enum.TryParse<BoneMaskGroups>(currentValue.ToString(), out BoneMaskGroups parsedMask))
                                currentMask = parsedMask;
                            else
                                currentMask = BoneMaskGroups.NONE;
                        }
                        
                        if (isChecked)
                            currentMask |= flag; 
                        else
                            currentMask &= ~flag; 
                        
                        field.SetValue(node, currentMask);
                        UpdateBoneMaskGroupsHeader(propertyName, currentMask);
                    }
                    else
                    {
                        MessageBox.Show($"Property/Field '{propertyName}' not found or not writable on type {nodeType.Name}.", "Property/Field Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating bone mask flag: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void UpdateBoneMaskGroupsHeader(string propertyName, BoneMaskGroups newMask)
        {
            bool wasUpdating = _isUpdating;
            _isUpdating = true;
            
            try
            {
                for (int i = 0; i < dataGridView1.Rows.Count; i++)
                {
                    string rowPropertyName = dataGridView1.Rows[i].Cells[0].Value?.ToString();
                    if (rowPropertyName == propertyName)
                    {
                        dataGridView1.Rows[i].Cells[1].Value = $"Flags: {GetBoneMaskGroupsDisplayText(newMask)}";
                        break;
                    }
                }
            }
            finally
            {
                _isUpdating = wasUpdating;
            }
        }

        #endregion
    }
}
