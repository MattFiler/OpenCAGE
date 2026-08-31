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
    /// <summary>Contextual state of a parameter, shown as a coloured indicator on its grid row.</summary>
    public enum ParameterStatus
    {
        None,
        LinkedInput,   //Fed by flowgraph logic - the inspector value is ignored (blue)
        AliasOverride, //Overridden by (or set on) an alias (orange)
    }

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
            //In multi-edit the framework merges rows and only bolds when EVERY descriptor agrees, so each
            //descriptor answers for the whole selection: bold unless the value is the default everywhere.
            if (_proxy.Host != null && _proxy.Host.IsMultiEditing)
                return _proxy.Host.IsParameterModifiedAcrossGroup(_proxy, _parameter.name);

            return IsModified();
        }

        /* Is this parameter modified from its default on this entity? */
        public bool IsModified()
        {
            return _proxy.Entity.variant == EntityVariant.VARIABLE
                || ParameterModificationTracker.IsParameterModified(_proxy.Composite.shortGUID, _proxy.Entity.shortGUID, _parameter.name);
        }

        /* Mark this parameter as modified and raise the editor-wide events */
        public void NotifyEdited()
        {
            _proxy.Host?.NotifyParameterEdited(_proxy, _parameter);
        }

        /* Contextual status (linked pin / alias override) for the row's coloured indicator */
        public ParameterStatus Status => _proxy.GetParameterStatus(_parameter.name);

        /* All editors are wrapped so the status highlight can be painted on any row type */
        public sealed override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
                return new ParameterStatusEditor(this, CreateValueEditor());
            return base.GetEditor(editorBaseType);
        }

        /* Override to supply the row's actual value editor (popup/checkbox/etc) */
        protected virtual UITypeEditor CreateValueEditor() => null;

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
        protected override UITypeEditor CreateValueEditor() => new BoolCheckboxEditor();
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

            //ControlPaint.DrawCheckBox is hardcoded to a white face and a system 3D border, so in dark
            //mode it has to be drawn by hand or every bool row carries a bright white square
            if (Theming.ThemeManager.IsDark)
            {
                PaintDarkCheckBox(e.Graphics, e.Bounds, isChecked);
                return;
            }

            //Clear the colour-swatch style border area then draw a flat checkbox
            e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds);
            ButtonState state = ButtonState.Flat | (isChecked ? ButtonState.Checked : ButtonState.Normal);
            ControlPaint.DrawCheckBox(e.Graphics, e.Bounds, state);
        }

        private static void PaintDarkCheckBox(Graphics graphics, Rectangle bounds, bool isChecked)
        {
            if (bounds.Width < 3 || bounds.Height < 3)
                return;

            using (SolidBrush face = new SolidBrush(Theming.ThemeColours.Input))
                graphics.FillRectangle(face, bounds);
            using (Pen border = new Pen(Theming.ThemeColours.BorderStrong))
                graphics.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

            if (!isChecked)
                return;

            //A tick rather than a filled block, so it reads at the small size the grid gives it
            Rectangle inner = Rectangle.Inflate(bounds, -3, -3);
            if (inner.Width < 2 || inner.Height < 2)
                return;

            System.Drawing.Drawing2D.SmoothingMode previous = graphics.SmoothingMode;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen tick = new Pen(Theming.ThemeColours.Text, 1.8f))
            {
                graphics.DrawLines(tick, new[]
                {
                    new PointF(inner.Left, inner.Top + inner.Height * 0.55f),
                    new PointF(inner.Left + inner.Width * 0.38f, inner.Bottom - 1),
                    new PointF(inner.Right, inner.Top),
                });
            }
            graphics.SmoothingMode = previous;
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
            //A row can outlive its value for a moment - removing an alias override drops the content
            //before the grid rebuilds - and the grid repaints in between
            cTransform data = Parameter.content as cTransform;
            if (data == null)
                return new GridTransform(new GridVector3(0, 0, 0), new GridVector3(0, 0, 0));
            return new GridTransform(
                new GridVector3(data.position.X, data.position.Y, data.position.Z),
                new GridVector3(data.rotation.X, data.rotation.Y, data.rotation.Z));
        }
        public override void SetValue(object component, object value)
        {
            if (!(value is GridTransform transform)) return;
            cTransform data = Parameter.content as cTransform;
            if (data == null) return;
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
            cVector3 data = Parameter.content as cVector3;
            if (data == null)
                return new GridVector3(0, 0, 0);
            return new GridVector3(data.value.X, data.value.Y, data.value.Z);
        }
        public override void SetValue(object component, object value)
        {
            if (!(value is GridVector3 vec)) return;
            cVector3 data = Parameter.content as cVector3;
            if (data == null) return;
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
        protected override UITypeEditor CreateValueEditor() => new ColourPickerEditor();
        public override object GetValue(object component)
        {
            cVector3 data = Parameter.content as cVector3;
            if (data == null)
                return Color.Black;
            return Color.FromArgb(ClampChannel(data.value.X), ClampChannel(data.value.Y), ClampChannel(data.value.Z));
        }
        public override void SetValue(object component, object value)
        {
            if (!(value is Color colour)) return;
            cVector3 data = Parameter.content as cVector3;
            if (data == null) return;
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
        protected override UITypeEditor CreateValueEditor() => _enumDescriptor == null ? new EnumTypePickerEditor() : null;

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
        protected override UITypeEditor CreateValueEditor() => new EnumStringPopupEditor();
    }

    /// <summary>
    /// The entity's 'name' parameter. Aliases/proxies without a name of their own inherit the name of the
    /// entity they point at, so show that inherited name here rather than an empty box - it stays a display
    /// value only, and nothing is written to the alias unless the user actually types a name.
    /// </summary>
    public class NameParameterDescriptor : StringParameterDescriptor
    {
        public NameParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }

        public override object GetValue(object component)
        {
            string own = ((cString)Parameter.content).value;
            if (!string.IsNullOrEmpty(own))
                return own;
            return _proxy.GetInheritedName() ?? "";
        }
    }

    /// <summary>EnvironmentMap "Texture" parameter - a string path picked via the texture browser.</summary>
    public class TexturePathParameterDescriptor : StringParameterDescriptor
    {
        public TexturePathParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        protected override UITypeEditor CreateValueEditor() => new TexturePopupEditor();
    }
    #endregion

    #region Popup-edited data types
    /// <summary>Composite material "mapping" parameter (a cResource whose shortGUID references a MaterialMapping).</summary>
    public class MappingParameterDescriptor : ParameterGridDescriptor
    {
        public MappingParameterDescriptor(EntityParameterProxy proxy, Parameter parameter, string name, Attribute[] attributes) : base(proxy, parameter, name, attributes) { }
        public override Type PropertyType => typeof(string);
        protected override UITypeEditor CreateValueEditor() => new MappingPopupEditor();
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
        protected override UITypeEditor CreateValueEditor() => new ResourcePopupEditor();
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
        protected override UITypeEditor CreateValueEditor() => new SplinePopupEditor();
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
    /// Wraps a row's value editor to paint the contextual status highlight as the background of the
    /// whole value cell: blue when the parameter is fed by flowgraph logic (inspector value ignored),
    /// orange when overridden by/set on an alias. The wrapper is bound to its descriptor directly, and
    /// the descriptor resolves no status in multi-edit mode, so multi-selections stay unhighlighted.
    /// </summary>
    public class ParameterStatusEditor : UITypeEditor
    {
        //Pale enough that the value text stays readable on top
        public static readonly Color LinkedInputColour = Color.FromArgb(173, 205, 245);
        public static readonly Color AliasOverrideColour = Color.FromArgb(250, 211, 160);

        private readonly ParameterGridDescriptor _descriptor;
        private readonly UITypeEditor _inner;
        public ParameterStatusEditor(ParameterGridDescriptor descriptor, UITypeEditor inner)
        {
            _descriptor = descriptor;
            _inner = inner;
        }

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return _inner?.GetEditStyle(context) ?? UITypeEditorEditStyle.None;
        }
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            return _inner != null ? _inner.EditValue(context, provider, value) : value;
        }
        public override bool GetPaintValueSupported(ITypeDescriptorContext context)
        {
            if (GetStatus(context) != ParameterStatus.None)
                return true;
            return _inner != null && _inner.GetPaintValueSupported(context);
        }
        public override void PaintValue(PaintValueEventArgs e)
        {
            ParameterStatus status = GetStatus(e.Context);

            //Fill the entire value cell (the grid draws the value text after this, so it stays on top).
            //The graphics clip keeps the fill within the row even though the width overshoots.
            if (status != ParameterStatus.None)
            {
                Color colour = status == ParameterStatus.LinkedInput ? LinkedInputColour : AliasOverrideColour;
                using (SolidBrush brush = new SolidBrush(colour))
                    e.Graphics.FillRectangle(brush, e.Bounds.X - 2, e.Bounds.Y - 2, 4000, e.Bounds.Height + 4);
            }

            //Draw the row's own visual (checkbox/colour swatch) on top of the highlight
            if (_inner != null && _inner.GetPaintValueSupported(e.Context))
                _inner.PaintValue(e);
        }

        private ParameterStatus GetStatus(ITypeDescriptorContext context)
        {
            if (_descriptor != null)
                return _descriptor.Status;
            return (context?.PropertyDescriptor as ParameterGridDescriptor)?.Status ?? ParameterStatus.None;
        }
    }

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
        private static EditAnimations _animationPopup;

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
            else if (first.enumID == EnumStringType.ANIMATION || first.enumID == EnumStringType.ANIMATION_SET)
            {
                /* The animation browser knows far more about these than a flat list of names does -
                 * how long a clip runs, what it's tagged with, and what it looks like. */
                if (_animationPopup != null)
                    _animationPopup.Close();

                bool wholeSet = first.enumID == EnumStringType.ANIMATION_SET;
                _animationPopup = new EditAnimations(
                    wholeSet ? EditAnimations.PickMode.AnimationSet : EditAnimations.PickMode.Animation,
                    wholeSet ? (first.value ?? "") : AnimationSetOf(targets[0]),
                    wholeSet ? null : first.value);
                _animationPopup.OnPicked += (str) => ApplyToTargets(targets, str);
                _animationPopup.Show();
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

        /* An animation only means anything inside a set, and the entity being edited normally says
         * which one on a sibling parameter - so open the browser on it. */
        private static string AnimationSetOf(EnumStringParameterDescriptor target)
        {
            Parameter parameter = target?.Proxy?.Entity?.GetParameter("AnimationSet");
            if (parameter?.content == null) return null;

            switch (parameter.content.dataType)
            {
                case DataType.STRING:
                case DataType.ENUM_STRING:
                    return ((cString)parameter.content).value;
                default:
                    return null;
            }
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
            if (!string.IsNullOrEmpty(path) && content?.Level?.Textures != null)
            {
                current = content.Level.Textures.GetEnvironmentMapByPath(path);
            }

            _popup = new EditTexture(current, showSelectBtn: true, environmentMapsOnly: true);
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

            //The instancing pass rebuilds these renderables from the entity's own parameters, so a
            //hand edit here is overwritten the next time the level is saved. Say so rather than
            //opening an editor whose changes will not survive.
            if (inspector.Entity is FunctionEntity generatedEntity
                && EntityInspector.FunctionResourcesAreGenerated(generatedEntity.function.AsFunctionType))
            {
                MessageBox.Show("The renderable for this entity is generated from its parameters when the level is saved, "
                    + "so edits made here would be overwritten. Change the entity's parameters instead.",
                    "Generated resource", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return value;
            }

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
