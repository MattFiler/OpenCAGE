using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using OpenCAGE.DockPanels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms;
using static CathodeLib.CathodeEnumTable;

namespace OpenCAGE
{
    /// <summary>
    /// Base descriptor for a single entity parameter row in the inspector's parameter grid.
    /// Equality is by name + descriptor type so the PropertyGrid can merge rows across a multi-selection.
    /// </summary>
    public abstract class ParameterGridDescriptor : PropertyDescriptor
    {
        protected readonly EntityParameterProxy _proxy;
        private readonly Parameter _parameter;

        public Parameter Parameter => _parameter;
        public EntityParameterProxy Proxy => _proxy;

        protected ParameterGridDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes)
            : base(name, attributes)
        {
            _proxy = proxy;
            _parameter = parameter;
        }

        public override Type ComponentType => typeof(EntityParameterProxy);
        public override bool IsReadOnly => false;
        public override bool CanResetValue(object component) => false;
        public override void ResetValue(object component) { }

        //ShouldSerializeValue == true renders the row in bold - our "modified from default" highlight.
        //Variable entity parameters have no defaults, so they're always considered modified.
        public override bool ShouldSerializeValue(object component)
        {
            return _proxy.Entity.variant == EntityVariant.VARIABLE
                || ParameterModificationTracker.IsParameterModified(_proxy.Composite.shortGUID, _proxy.Entity.shortGUID, _parameter.name);
        }

        /* Mark this parameter as modified and raise the editor-wide events */
        public void NotifyEdited()
        {
            _proxy.Host?.NotifyParameterEdited(_proxy, _parameter);
        }

        public override bool Equals(object obj)
        {
            return obj is ParameterGridDescriptor other && other.GetType() == GetType() && other.Name == Name;
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ GetType().GetHashCode();
        }
    }

    #region Simple data types
    public class BoolParameterDescriptor : ParameterGridDescriptor
    {
        public BoolParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(bool);
        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
                return new BoolCheckboxEditor();
            return base.GetEditor(editorBaseType);
        }
        public override object GetValue(object component) => ((cBool)Parameter.content).value;
        public override void SetValue(object component, object value)
        {
            cBool data = (cBool)Parameter.content;
            if (data.value == (bool)value) return;
            data.value = (bool)value;
            NotifyEdited();
        }
    }

    /// <summary>
    /// Draws a checkbox glyph in the value cell for BOOL parameters.
    /// Clicking the glyph (handled by ParameterGridPanel), double-clicking the value, or the edit button all toggle.
    /// </summary>
    public class BoolCheckboxEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            //Instant toggle - no dialog
            return !(value is bool current && current);
        }
        public override bool GetPaintValueSupported(ITypeDescriptorContext context) => true;
        public override void PaintValue(PaintValueEventArgs e)
        {
            bool isChecked = e.Value is bool value && value;
            //Clear the colour-swatch style border area then draw a flat checkbox
            e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds);
            ButtonState state = ButtonState.Flat | (isChecked ? ButtonState.Checked : ButtonState.Normal);
            ControlPaint.DrawCheckBox(e.Graphics, e.Bounds, state);
        }
    }

    /// <summary>Full colour picker (sliders + stored custom colour presets) for colour VECTOR parameters.</summary>
    public class ColourPickerEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;
        public override bool GetPaintValueSupported(ITypeDescriptorContext context) => true;
        public override void PaintValue(PaintValueEventArgs e)
        {
            if (!(e.Value is Color colour))
                return;
            using (SolidBrush brush = new SolidBrush(colour))
                e.Graphics.FillRectangle(brush, e.Bounds);
        }
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                dialog.Color = value is Color current ? current : Color.Black;
                dialog.CustomColors = SettingsManager.GetIntegerArray(Settings.CustomColours);
                if (dialog.ShowDialog() != DialogResult.OK)
                    return value;

                SettingsManager.SetIntegerArray(Settings.CustomColours, dialog.CustomColors);
                //Returning the new colour lets the grid apply it (including across a multi-selection)
                return dialog.Color;
            }
        }
    }

    public class IntParameterDescriptor : ParameterGridDescriptor
    {
        public IntParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(int);
        public override object GetValue(object component) => ((cInteger)Parameter.content).value;
        public override void SetValue(object component, object value)
        {
            cInteger data = (cInteger)Parameter.content;
            if (data.value == (int)value) return;
            data.value = (int)value;
            NotifyEdited();
        }
    }

    public class FloatParameterDescriptor : ParameterGridDescriptor
    {
        public FloatParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(float);
        public override object GetValue(object component) => ((cFloat)Parameter.content).value;
        public override void SetValue(object component, object value)
        {
            cFloat data = (cFloat)Parameter.content;
            if (data.value == (float)value) return;
            data.value = (float)value;
            NotifyEdited();
        }
    }

    public class StringParameterDescriptor : ParameterGridDescriptor
    {
        public StringParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(string);
        public override object GetValue(object component) => ((cString)Parameter.content).value;
        public override void SetValue(object component, object value)
        {
            cString data = (cString)Parameter.content;
            string newValue = (string)value ?? "";
            if (data.value == newValue) return;
            data.value = newValue;
            NotifyEdited();
        }
    }
    #endregion

    #region Transform / vector / colour
    public class TransformParameterDescriptor : ParameterGridDescriptor
    {
        public TransformParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(GridTransform);
        public override object GetValue(object component)
        {
            cTransform data = (cTransform)Parameter.content;
            return new GridTransform(
                new GridVector3(data.position.X, data.position.Y, data.position.Z),
                new GridVector3(data.rotation.X, data.rotation.Y, data.rotation.Z));
        }
        public override void SetValue(object component, object value)
        {
            if (!(value is GridTransform transform)) return;
            cTransform data = (cTransform)Parameter.content;
            if (Equals(GetValue(component), transform)) return;
            data.position.X = transform.Position.X;
            data.position.Y = transform.Position.Y;
            data.position.Z = transform.Position.Z;
            data.rotation.X = transform.Rotation.X;
            data.rotation.Y = transform.Rotation.Y;
            data.rotation.Z = transform.Rotation.Z;
            NotifyEdited();
            Singleton.OnEntityMoved?.Invoke(data, _proxy.Entity);
        }
    }

    public class VectorParameterDescriptor : ParameterGridDescriptor
    {
        public VectorParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(GridVector3);
        public override object GetValue(object component)
        {
            cVector3 data = (cVector3)Parameter.content;
            return new GridVector3(data.value.X, data.value.Y, data.value.Z);
        }
        public override void SetValue(object component, object value)
        {
            if (!(value is GridVector3 vec)) return;
            cVector3 data = (cVector3)Parameter.content;
            if (Equals(GetValue(component), vec)) return;
            data.value.X = vec.X;
            data.value.Y = vec.Y;
            data.value.Z = vec.Z;
            NotifyEdited();
        }
    }

    /// <summary>VECTOR parameters that represent 0-255 RGB colours - edited with the standard colour picker.</summary>
    public class ColourParameterDescriptor : ParameterGridDescriptor
    {
        public ColourParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(Color);
        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
                return new ColourPickerEditor();
            return base.GetEditor(editorBaseType);
        }
        public override object GetValue(object component)
        {
            cVector3 data = (cVector3)Parameter.content;
            return Color.FromArgb(ClampChannel(data.value.X), ClampChannel(data.value.Y), ClampChannel(data.value.Z));
        }
        public override void SetValue(object component, object value)
        {
            if (!(value is Color colour)) return;
            cVector3 data = (cVector3)Parameter.content;
            if (Equals(GetValue(component), colour)) return;
            data.value.X = colour.R;
            data.value.Y = colour.G;
            data.value.Z = colour.B;
            NotifyEdited();
        }
        private static int ClampChannel(float value)
        {
            return Math.Max(0, Math.Min(255, (int)value));
        }
    }
    #endregion

    #region Enums
    public class EnumParameterDescriptor : ParameterGridDescriptor
    {
        private readonly EnumDescriptor _enumDescriptor; //null when the value has no valid enum type assigned

        public EnumParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes, EnumDescriptor enumDescriptor)
            : base(proxy, parameter, name, attributes)
        {
            _enumDescriptor = enumDescriptor;
        }

        public override Type PropertyType => typeof(string);
        public override TypeConverter Converter => _enumDescriptor == null ? base.Converter : new EnumEntriesConverter(_enumDescriptor);
        public override object GetEditor(Type editorBaseType)
        {
            if (_enumDescriptor == null && editorBaseType == typeof(UITypeEditor))
                return new EnumTypePickerEditor();
            return base.GetEditor(editorBaseType);
        }

        public override object GetValue(object component)
        {
            cEnum data = (cEnum)Parameter.content;
            if (_enumDescriptor == null)
                return "(no enum type)";
            EnumDescriptor.Entry entry = _enumDescriptor.Entries.FirstOrDefault(o => o.Index == data.enumIndex);
            return entry != null ? entry.Name : "(index " + data.enumIndex + ")";
        }
        public override void SetValue(object component, object value)
        {
            if (_enumDescriptor == null) return;
            EnumDescriptor.Entry entry = _enumDescriptor.Entries.FirstOrDefault(o => o.Name == (string)value);
            if (entry == null) return;
            cEnum data = (cEnum)Parameter.content;
            if (data.enumID == _enumDescriptor.ID && data.enumIndex == entry.Index) return;
            data.enumID = _enumDescriptor.ID;
            data.enumIndex = entry.Index;
            NotifyEdited();
        }

        private class EnumEntriesConverter : StringConverter
        {
            private readonly EnumDescriptor _descriptor;
            public EnumEntriesConverter(EnumDescriptor descriptor) { _descriptor = descriptor; }
            public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;
            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                return new StandardValuesCollection(_descriptor.Entries.Select(o => o.Name).ToList());
            }
        }
    }

    public class EnumStringParameterDescriptor : StringParameterDescriptor
    {
        public EnumStringParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
                return new EnumStringPopupEditor();
            return base.GetEditor(editorBaseType);
        }
    }

    /// <summary>EnvironmentMap "Texture" parameter - a string path picked via the texture browser.</summary>
    public class TexturePathParameterDescriptor : StringParameterDescriptor
    {
        public TexturePathParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
                return new TexturePopupEditor();
            return base.GetEditor(editorBaseType);
        }
    }
    #endregion

    #region Popup-edited data types
    /// <summary>Composite material "mapping" parameter (a cResource whose shortGUID references a MaterialMapping).</summary>
    public class MappingParameterDescriptor : ParameterGridDescriptor
    {
        public MappingParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(string);
        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
                return new MappingPopupEditor();
            return base.GetEditor(editorBaseType);
        }
        public override object GetValue(object component)
        {
            cResource data = (cResource)Parameter.content;
            MaterialMappings.MaterialMapping map = _proxy.Content?.Level?.MaterialMappings?.Entries?.FirstOrDefault(o => o.ID == data.shortGUID);
            return map != null ? map.Name : "";
        }
        public override void SetValue(object component, object value)
        {
            //Typed values only apply if they exactly match an existing mapping name - otherwise use the picker
            MaterialMappings.MaterialMapping map = _proxy.Content?.Level?.MaterialMappings?.Entries?.FirstOrDefault(o => o.Name == (string)value);
            if (map == null) return;
            cResource data = (cResource)Parameter.content;
            if (data.shortGUID == map.ID) return;
            data.shortGUID = map.ID;
            NotifyEdited();
        }
    }

    public class ResourceParameterDescriptor : ParameterGridDescriptor
    {
        public ResourceParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(string);
        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
                return new ResourcePopupEditor();
            return base.GetEditor(editorBaseType);
        }
        public override object GetValue(object component)
        {
            cResource data = (cResource)Parameter.content;
            int count = data.value?.Count ?? 0;
            return count == 1 ? "1 resource" : count + " resources";
        }
        public override void SetValue(object component, object value) { }
    }

    public class SplineParameterDescriptor : ParameterGridDescriptor
    {
        public SplineParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(string);
        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
                return new SplinePopupEditor();
            return base.GetEditor(editorBaseType);
        }
        public override object GetValue(object component)
        {
            cSpline data = (cSpline)Parameter.content;
            int count = data.splinePoints?.Count ?? 0;
            return "Spline (" + count + (count == 1 ? " point)" : " points)");
        }
        public override void SetValue(object component, object value) { }
    }

    /// <summary>Fallback row for data types the grid can't edit yet.</summary>
    public class ReadOnlyParameterDescriptor : ParameterGridDescriptor
    {
        public ReadOnlyParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(string);
        public override bool IsReadOnly => true;
        public override object GetValue(object component) => "(" + Parameter.content.dataType + ")";
        public override void SetValue(object component, object value) { }
    }
    #endregion

    #region UITypeEditors
    /// <summary>
    /// Resolves the grid descriptors targeted by a UITypeEditor edit - handles both single selection
    /// and a merged multi-selection (where context.Instance is the array of selected proxies).
    /// </summary>
    internal static class ParameterGridTargets
    {
        public static List<T> Get<T>(ITypeDescriptorContext context) where T : ParameterGridDescriptor
        {
            List<T> result = new List<T>();
            string name = context?.PropertyDescriptor?.Name;
            if (name == null)
                return result;

            IEnumerable instances = context.Instance is Array array ? (IEnumerable)array : new object[] { context.Instance };
            foreach (object obj in instances)
            {
                if (obj is EntityParameterProxy proxy && proxy.GetParameterDescriptor(name) is T descriptor)
                    result.Add(descriptor);
            }
            return result;
        }
    }

    /// <summary>Opens the enum-string picker (or material picker for MATERIAL enum-strings) and applies the choice to every selected entity.</summary>
    public class EnumStringPopupEditor : UITypeEditor
    {
        private static SelectEnumString _popup;
        private static EditMaterial _materialPopup;

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            List<EnumStringParameterDescriptor> targets = ParameterGridTargets.Get<EnumStringParameterDescriptor>(context);
            if (targets.Count == 0)
                return value;

            cEnumString first = (cEnumString)targets[0].Parameter.content;
            if (first.enumID == EnumStringType.MATERIAL)
            {
                if (_materialPopup != null)
                    _materialPopup.Close();

                LevelContent content = targets[0].Proxy.Content;
                string current = (first.value ?? "").Trim();
                Materials.Material initial = null;
                if (content?.Level?.Materials?.Entries != null && current.Length != 0)
                {
                    initial = content.Level.Materials.Entries.FirstOrDefault(m =>
                        m?.Name != null &&
                        (m.Name.Equals(current, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(content.Level.Materials.GetMaterialName(m), current, StringComparison.OrdinalIgnoreCase)));
                }

                _materialPopup = new EditMaterial(initial, showSelectBtn: true);
                _materialPopup.OnMaterialSelected += (material) =>
                {
                    if (material == null) return;
                    ApplyToTargets(targets, material.Name);
                };
                _materialPopup.Show();
            }
            else
            {
                if (_popup != null)
                    _popup.Close();

                _popup = new SelectEnumString(targets[0].Name, first, false);
                _popup.OnSelected += (str) => ApplyToTargets(targets, str);
                _popup.Show();
            }
            return value;
        }

        private static void ApplyToTargets(List<EnumStringParameterDescriptor> targets, string value)
        {
            foreach (EnumStringParameterDescriptor target in targets)
                target.SetValue(target.Proxy, value);
            ParameterGridPanel.Current?.RefreshValues();
        }
    }

    /// <summary>Opens the environment map texture picker and applies the choice to every selected entity.</summary>
    public class TexturePopupEditor : UITypeEditor
    {
        private static EditTexture _popup;

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            List<TexturePathParameterDescriptor> targets = ParameterGridTargets.Get<TexturePathParameterDescriptor>(context);
            if (targets.Count == 0)
                return value;

            if (_popup != null)
                _popup.Close();

            LevelContent content = targets[0].Proxy.Content;
            string path = ((cString)targets[0].Parameter.content).value;
            Textures.TEX4 current = null;
            int sourceIndex = 0;
            if (!string.IsNullOrEmpty(path) && content?.Level?.Textures != null)
            {
                current = content.Level.Textures.GetEnvironmentMapByPath(path);
                if (current == null && Singleton.Global?.Textures != null)
                {
                    current = Singleton.Global.Textures.GetEnvironmentMapByPath(path);
                    if (current != null)
                        sourceIndex = 1;
                }
            }

            _popup = new EditTexture(current, showSelectBtn: true, initialTextureSourceIndex: sourceIndex, environmentMapsOnly: true);
            _popup.OnTextureSelected += (texture) =>
            {
                if (texture == null) return;
                string newPath = "n:\\content\\build\\textures\\" + texture.Name;
                foreach (TexturePathParameterDescriptor target in targets)
                    target.SetValue(target.Proxy, newPath);
                ParameterGridPanel.Current?.RefreshValues();
            };
            _popup.Show();
            return value;
        }
    }

    /// <summary>Opens the material mapping picker and applies the choice to every selected entity.</summary>
    public class MappingPopupEditor : UITypeEditor
    {
        private static EditMaterialMapping _popup;

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            List<MappingParameterDescriptor> targets = ParameterGridTargets.Get<MappingParameterDescriptor>(context);
            if (targets.Count == 0)
                return value;

            if (_popup != null)
                _popup.Close();

            cResource first = (cResource)targets[0].Parameter.content;
            MaterialMappings.MaterialMapping currentMap = targets[0].Proxy.Content?.Level?.MaterialMappings?.Entries?.FirstOrDefault(o => o.ID == first.shortGUID);

            _popup = new EditMaterialMapping(currentMap, true);
            _popup.OnMaterialMappingSelected += (map) =>
            {
                if (map == null) return;
                foreach (MappingParameterDescriptor target in targets)
                {
                    cResource data = (cResource)target.Parameter.content;
                    if (data.shortGUID == map.ID) continue;
                    data.shortGUID = map.ID;
                    target.NotifyEdited();
                }
                ParameterGridPanel.Current?.RefreshValues();
            };
            _popup.Show();
            return value;
        }
    }

    /// <summary>Opens the resource editor. Resources are per-entity data, so this only supports a single selection.</summary>
    public class ResourcePopupEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            List<ResourceParameterDescriptor> targets = ParameterGridTargets.Get<ResourceParameterDescriptor>(context);
            if (targets.Count == 0)
                return value;
            if (targets.Count > 1)
            {
                MessageBox.Show("Resources are entity-specific and can't be edited across a multi-selection.\nSelect a single entity to edit its resources.", "Multi-edit unsupported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return value;
            }

            ResourceParameterDescriptor target = targets[0];
            EntityInspector inspector = ParameterGridPanel.Current?.Inspector;
            if (inspector == null)
                return value;

            cResource resource = (cResource)target.Parameter.content;
            List<ResourceReference> original = resource.value == null ? new List<ResourceReference>() : resource.value.Select(o => o.Copy()).ToList();

            AddOrEditResource popup = new AddOrEditResource(inspector, resource, target.Name);
            popup.FormClosed += (s, e) =>
            {
                List<ResourceReference> current = resource.value ?? new List<ResourceReference>();
                if (original.Count != current.Count || !original.SequenceEqual(current))
                {
                    target.NotifyEdited();
                    Singleton.OnResourceModified?.Invoke();
                }
                ParameterGridPanel.Current?.RefreshValues();
            };
            popup.Show();
            return value;
        }
    }

    /// <summary>Opens the spline editor. Splines are per-entity data, so this only supports a single selection.</summary>
    public class SplinePopupEditor : UITypeEditor
    {
        private static EditSpline _popup;

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            List<SplineParameterDescriptor> targets = ParameterGridTargets.Get<SplineParameterDescriptor>(context);
            if (targets.Count == 0)
                return value;
            if (targets.Count > 1)
            {
                MessageBox.Show("Splines are entity-specific and can't be edited across a multi-selection.\nSelect a single entity to edit its spline.", "Multi-edit unsupported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return value;
            }

            SplineParameterDescriptor target = targets[0];
            cSpline spline = (cSpline)target.Parameter.content;

            if (_popup != null)
                _popup.Close();

            _popup = new EditSpline(spline, target.Proxy.Entity.GetParameter("loop"));
            _popup.OnSaved += (newSpline) =>
            {
                spline.splinePoints = newSpline.splinePoints;
                target.NotifyEdited();
                ParameterGridPanel.Current?.RefreshValues();
            };
            _popup.Show();
            return value;
        }
    }

    /// <summary>Small modal picker for ENUM parameters that have no enum type assigned yet.</summary>
    public class EnumTypePickerEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            List<EnumParameterDescriptor> targets = ParameterGridTargets.Get<EnumParameterDescriptor>(context);
            if (targets.Count == 0)
                return value;

            CommandsUtils utils = targets[0].Proxy.Content?.Level?.Commands?.Utils;
            if (utils == null)
                return value;

            using (Form dialog = new Form())
            {
                dialog.Text = "Select Enum - " + targets[0].Name;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ClientSize = new Size(320, 110);

                ComboBox typeCombo = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(12, 12), Width = 296 };
                ComboBox valueCombo = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(12, 42), Width = 296 };
                Button ok = new Button() { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(152, 76), Width = 75 };
                Button cancel = new Button() { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(233, 76), Width = 75 };
                dialog.Controls.AddRange(new Control[] { typeCombo, valueCombo, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                List<string> enumTypes = Enum.GetValues(typeof(EnumType)).Cast<EnumType>().Select(o => o.ToString()).OrderBy(o => o).ToList();
                typeCombo.Items.AddRange(enumTypes.ToArray());
                typeCombo.SelectedIndexChanged += (s, e) =>
                {
                    valueCombo.Items.Clear();
                    EnumDescriptor descriptor = utils.GetEnum(typeCombo.Text);
                    if (descriptor == null) return;
                    valueCombo.Items.AddRange(descriptor.Entries.Select(o => o.Name).ToArray());
                    if (valueCombo.Items.Count > 0)
                        valueCombo.SelectedIndex = 0;
                };
                typeCombo.SelectedIndex = 0;

                if (dialog.ShowDialog() != DialogResult.OK || valueCombo.SelectedItem == null)
                    return value;

                EnumDescriptor chosen = utils.GetEnum(typeCombo.Text);
                EnumDescriptor.Entry entry = chosen?.Entries.FirstOrDefault(o => o.Name == valueCombo.SelectedItem.ToString());
                if (entry == null)
                    return value;

                foreach (EnumParameterDescriptor target in targets)
                {
                    cEnum data = (cEnum)target.Parameter.content;
                    data.enumID = chosen.ID;
                    data.enumIndex = entry.Index;
                    target.NotifyEdited();
                }

                //The descriptors cache the enum type, so rebuild rows to pick up the new dropdown
                ParameterGridPanel.Current?.RebuildProperties();
            }
            return value;
        }
    }
    #endregion
}
