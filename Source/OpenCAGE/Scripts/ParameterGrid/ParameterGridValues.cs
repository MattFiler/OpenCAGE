using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;

namespace OpenCAGE
{
    /// <summary>
    /// Snapshot of a Vector3 parameter value shown in the parameter grid.
    /// Immutable in spirit: edits flow through the grid descriptor's SetValue via the converter's CreateInstance,
    /// which is what makes nested edits fan out correctly across a multi-selection.
    /// </summary>
    [TypeConverter(typeof(GridVector3Converter))]
    public class GridVector3
    {
        public GridVector3() { }
        public GridVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        [NotifyParentProperty(true)]
        public float X { get; set; }
        [NotifyParentProperty(true)]
        public float Y { get; set; }
        [NotifyParentProperty(true)]
        public float Z { get; set; }

        public override string ToString()
        {
            return X.ToString(CultureInfo.InvariantCulture) + ", "
                + Y.ToString(CultureInfo.InvariantCulture) + ", "
                + Z.ToString(CultureInfo.InvariantCulture);
        }

        public override bool Equals(object obj)
        {
            return obj is GridVector3 other && other.X == X && other.Y == Y && other.Z == Z;
        }
        public override int GetHashCode()
        {
            return X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2);
        }
    }

    public class GridVector3Converter : ExpandableObjectConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string str)
            {
                string[] parts = str.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3)
                    throw new FormatException("Expected three comma-separated values (e.g. \"1, 2, 3\")");
                return new GridVector3(
                    float.Parse(parts[0], CultureInfo.InvariantCulture),
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture));
            }
            return base.ConvertFrom(context, culture, value);
        }
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is GridVector3 vec)
                return vec.ToString();
            return base.ConvertTo(context, culture, value, destinationType);
        }
        public override bool GetCreateInstanceSupported(ITypeDescriptorContext context)
        {
            return true;
        }
        public override object CreateInstance(ITypeDescriptorContext context, IDictionary propertyValues)
        {
            return new GridVector3(
                Convert.ToSingle(propertyValues["X"]),
                Convert.ToSingle(propertyValues["Y"]),
                Convert.ToSingle(propertyValues["Z"]));
        }
    }

    /// <summary>
    /// Snapshot of a cTransform parameter value shown in the parameter grid (position + euler rotation).
    /// </summary>
    [TypeConverter(typeof(GridTransformConverter))]
    public class GridTransform
    {
        public GridTransform()
        {
            Position = new GridVector3();
            Rotation = new GridVector3();
        }
        public GridTransform(GridVector3 position, GridVector3 rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        [NotifyParentProperty(true)]
        public GridVector3 Position { get; set; }
        [NotifyParentProperty(true)]
        public GridVector3 Rotation { get; set; }

        public override string ToString()
        {
            return "Pos (" + Position + ")  Rot (" + Rotation + ")";
        }

        public override bool Equals(object obj)
        {
            return obj is GridTransform other && Equals(other.Position, Position) && Equals(other.Rotation, Rotation);
        }
        public override int GetHashCode()
        {
            return (Position?.GetHashCode() ?? 0) ^ ((Rotation?.GetHashCode() ?? 0) << 1);
        }
    }

    public class GridTransformConverter : ExpandableObjectConverter
    {
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is GridTransform transform)
                return transform.ToString();
            return base.ConvertTo(context, culture, value, destinationType);
        }
        public override bool GetCreateInstanceSupported(ITypeDescriptorContext context)
        {
            return true;
        }
        public override object CreateInstance(ITypeDescriptorContext context, IDictionary propertyValues)
        {
            return new GridTransform(
                propertyValues["Position"] as GridVector3 ?? new GridVector3(),
                propertyValues["Rotation"] as GridVector3 ?? new GridVector3());
        }
    }
}
