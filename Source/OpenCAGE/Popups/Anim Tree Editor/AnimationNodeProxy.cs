using CATHODE;
using CATHODE.Animations;
using CathodeLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Windows.Forms;

namespace OpenCAGE.AnimTrees
{
    /// <summary>
    /// Presents an animation node to a PropertyGrid, the way EntityParameterProxy presents an entity's
    /// parameters. Rows come from reflection over the node's own members, so a field added to a node in
    /// CathodeLib turns up here without this file changing.
    ///
    /// Graph wiring - children, callbacks, parameter bindings, state targets - is deliberately absent.
    /// Those are edges, and they are drawn and edited on the canvas.
    ///
    /// Every row is built against the matching member of a freshly constructed node of the same class,
    /// which is what tells the grid whether the value is still the default one.
    /// </summary>
    public class AnimationNodeProxy : ICustomTypeDescriptor
    {
        private readonly AnimationNodeEditor _editor;
        private readonly AnimationNode _node;
        private readonly AnimationTree _tree;
        private PropertyDescriptorCollection _properties;

        public AnimationNodeEditor Editor => _editor;
        public AnimationNode Node => _node;
        public AnimationTree Tree => _tree;

        public AnimationNodeProxy(AnimationNodeEditor editor, AnimationNode node, AnimationTree tree)
        {
            _editor = editor;
            _node = node;
            _tree = tree;
        }

        /* A row has written its value into the node - the editor's chance to catch up */
        public void NotifyEdited(AnimationNodeDescriptor descriptor)
        {
            _editor?.OnNodeEdited(descriptor);
        }

        #region Rows

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            if (_properties != null)
                return _properties;

            List<PropertyDescriptor> rows = new List<PropertyDescriptor>();
            if (_node != null)
            {
                /* The node this one would be if nobody had touched it. Walked alongside the real one so
                 * every row, however deeply nested, knows the value its own class starts out with. */
                object untouched = Prototype(_node.GetType());

                foreach (MemberInfo member in MembersOf(_node.GetType()))
                    AddMember(rows, _node, untouched, member, "");
            }

            _properties = new PropertyDescriptorCollection(rows.ToArray(), true);
            return _properties;
        }

        public PropertyDescriptorCollection GetProperties()
        {
            return GetProperties(null);
        }

        /// <summary>
        /// The public fields and settable properties of a type, base class first, so rows read in the
        /// order the type declares them - Name, then whatever this node type adds on top.
        /// </summary>
        private static IEnumerable<MemberInfo> MembersOf(Type type)
        {
            List<Type> chain = new List<Type>();
            for (Type step = type; step != null && step != typeof(object); step = step.BaseType)
                chain.Insert(0, step);

            foreach (Type step in chain)
            {
                foreach (FieldInfo field in step.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (!field.IsInitOnly)
                        yield return field;

                foreach (PropertyInfo property in step.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (property.GetIndexParameters().Length == 0
                        && property.GetGetMethod(false) != null && property.GetSetMethod(false) != null)
                        yield return property;
            }
        }

        /* Turn one member into the row (or group of rows) that edits it */
        private void AddMember(List<PropertyDescriptor> rows, object owner, object untouched, MemberInfo member, string path)
        {
            Type type = TypeOf(member);
            string name = member.Name;
            string childPath = string.IsNullOrEmpty(path) ? name : path + "." + name;

            //The node's type is in the window title, and its wiring belongs to the canvas
            if (owner is AnimationNode && name == "Type")
                return;
            if (typeof(AnimationNode).IsAssignableFrom(type) || IsNodeCollection(type))
                return;

            //Renaming has to go through the tree, which keeps the name lookups and rejects duplicates
            if (ReferenceEquals(owner, _node) && name == "Name")
            {
                rows.Add(new NodeNameDescriptor(this, untouched as AnimationNode));
                return;
            }

            //An enumerated selector picks its state by hashed string rather than by number
            if (name == "Value" && !(owner is AnimationNode) && (type == typeof(uint) || type == typeof(int))
                && _node != null && _node.Type == NodeType.ANIM_Enumerated_Selector)
            {
                rows.Add(new HashedStringDescriptor(this, owner, untouched, member, childPath, name));
                return;
            }

            if (type.IsEnum)
            {
                if (type.GetCustomAttributes(typeof(FlagsAttribute), false).Length != 0)
                    rows.Add(FlagsRow(owner, untouched, member, childPath, name));
                else
                    rows.Add(new NodeMemberDescriptor(this, owner, untouched, member, childPath, name));
                return;
            }

            if (IsScalar(type))
            {
                rows.Add(new NodeMemberDescriptor(this, owner, untouched, member, childPath, name));
                return;
            }

            if (type == typeof(Vector3))
            {
                rows.Add(VectorRow(owner, untouched, member, childPath, name));
                return;
            }

            if (typeof(AnimationMetadataValue).IsAssignableFrom(type))
            {
                rows.Add(MetadataRow(owner, untouched, member, childPath, name));
                return;
            }

            if (typeof(IList).IsAssignableFrom(type))
            {
                rows.Add(CollectionRow(owner, untouched, member, childPath, name));
                return;
            }

            //Anything else is a shape this editor has no way to show
        }

        /* A fixed-length state or animation pool: one expandable row per slot */
        private PropertyDescriptor CollectionRow(object owner, object untouched, MemberInfo member, string path, string label)
        {
            IList list = Read(owner, member) as IList;
            if (list == null)
                return new NodeGroupDescriptor(this, path, label, () => "(none)", new PropertyDescriptor[0]);

            FillEmptySlots(list);

            /* Slot by slot, because a class can set its slots up differently - a selector's states start
             * out numbered 0..15, so state 3 defaulting to 3 is not the same as defaulting to nothing. */
            IList standard = untouched == null ? null : Read(untouched, member) as IList;

            List<PropertyDescriptor> items = new List<PropertyDescriptor>();
            for (int i = 0; i < list.Count; i++)
            {
                object item = list[i];
                if (item == null)
                    continue;

                object standardItem = Matching(item, standard != null && i < standard.Count ? standard[i] : null);

                string itemPath = path + "[" + i + "]";
                List<PropertyDescriptor> fields = new List<PropertyDescriptor>();
                foreach (MemberInfo field in MembersOf(item.GetType()))
                    AddMember(fields, item, standardItem, field, itemPath);

                object captured = item;
                items.Add(new NodeGroupDescriptor(this, itemPath, "[" + i + "]", () => Summarise(captured), fields));
            }

            IList counted = list;
            return new NodeGroupDescriptor(this, path, label, () => "Count: " + counted.Count, items);
        }

        /* A [Flags] enum: a checkbox per bit, under a row summarising what is set */
        private PropertyDescriptor FlagsRow(object owner, object untouched, MemberInfo member, string path, string label)
        {
            Type type = TypeOf(member);

            List<PropertyDescriptor> bits = new List<PropertyDescriptor>();
            foreach (object flag in Enum.GetValues(type))
            {
                long mask = Convert.ToInt64(flag);
                if (mask == 0)
                    continue;

                string name = flag.ToString();
                bits.Add(new FlagBitDescriptor(this, owner, untouched, member, path + "." + name, name, mask));
            }

            return new NodeGroupDescriptor(this, path, label, () => DescribeFlags(Read(owner, member)), bits);
        }

        private PropertyDescriptor VectorRow(object owner, object untouched, MemberInfo member, string path, string label)
        {
            PropertyDescriptor[] parts = new PropertyDescriptor[]
            {
                new VectorPartDescriptor(this, owner, untouched, member, path + ".X", "X", 0),
                new VectorPartDescriptor(this, owner, untouched, member, path + ".Y", "Y", 1),
                new VectorPartDescriptor(this, owner, untouched, member, path + ".Z", "Z", 2),
            };

            return new NodeGroupDescriptor(this, path, label, () => DescribeVector(Read(owner, member)), parts);
        }

        /// <summary>
        /// A property node's payload. Its concrete class is chosen by ValueType, so changing that swaps
        /// the whole object - which is why ValueType gets a row of its own rather than being reflected.
        /// </summary>
        private PropertyDescriptor MetadataRow(object owner, object untouched, MemberInfo member, string path, string label)
        {
            AnimationMetadataValue meta = Read(owner, member) as AnimationMetadataValue;
            if (meta == null)
            {
                meta = new FloatMetadataValue();
                Write(owner, member, meta);
            }

            /* A property node ships with no payload at all, so there is no class default to read here -
             * the defaults that mean anything are the ones belonging to whichever payload it now holds. */
            object standard = Matching(meta, untouched == null ? null : Read(untouched, member));

            List<PropertyDescriptor> fields = new List<PropertyDescriptor>();
            fields.Add(new MetadataTypeDescriptor(this, owner, member, standard as AnimationMetadataValue, path + ".ValueType", "ValueType"));
            foreach (MemberInfo field in MembersOf(meta.GetType()))
                AddMember(fields, meta, standard, field, path);

            AnimationMetadataValue captured = meta;
            return new NodeGroupDescriptor(this, path, label, () => captured.GetType().Name, fields);
        }

        #endregion

        #region Defaults

        private static readonly Dictionary<Type, object> _prototypes = new Dictionary<Type, object>();

        /// <summary>
        /// One freshly constructed instance of a type, kept and reused. Nothing ever writes to these -
        /// they exist only to be read for the value a member starts out holding.
        /// </summary>
        internal static object Prototype(Type type)
        {
            object prototype;
            if (_prototypes.TryGetValue(type, out prototype))
                return prototype;

            try { prototype = Activator.CreateInstance(type); }
            catch { prototype = null; }

            _prototypes[type] = prototype;
            return prototype;
        }

        /// <summary>
        /// The untouched counterpart of a live object: the one walked alongside it if it is of the same
        /// class, and otherwise a fresh one of the class the live object actually turned out to be.
        /// </summary>
        private static object Matching(object live, object untouched)
        {
            if (live == null)
                return null;
            if (untouched != null && untouched.GetType() == live.GetType())
                return untouched;

            return Prototype(live.GetType());
        }

        #endregion

        #region Values

        /// <summary>The one-line label for a collection entry, preferring something intrinsic to it.</summary>
        private string Summarise(object item)
        {
            if (item == null)
                return "(empty)";

            string animation = Read(item, "AnimationName") as string;
            if (!string.IsNullOrEmpty(animation))
                return animation;

            string name = Read(item, "Name") as string;
            if (!string.IsNullOrEmpty(name))
                return name;

            object value = Read(item, "Value");
            if (value != null)
            {
                if (_node != null && _node.Type == NodeType.ANIM_Enumerated_Selector && value is uint hashed)
                    return AnimationHashedString.Format(hashed);
                return value.ToString();
            }

            object min = Read(item, "Min");
            object max = Read(item, "Max");
            if (min != null && max != null)
                return min + " .. " + max;

            return item.GetType().Name;
        }

        internal static string DescribeFlags(object value)
        {
            if (value == null)
                return "None";

            long bits = Convert.ToInt64(value);
            if (bits == 0)
                return "None";

            List<string> set = new List<string>();
            foreach (object flag in Enum.GetValues(value.GetType()))
            {
                long mask = Convert.ToInt64(flag);
                if (mask != 0 && (bits & mask) == mask)
                    set.Add(flag.ToString());
            }

            if (set.Count == 0)
                return "None";
            return set.Count <= 3 ? string.Join(", ", set) : set.Count + " flags set";
        }

        internal static string DescribeVector(object value)
        {
            if (!(value is Vector3 vector))
                return "";
            return vector.X.ToString("0.######") + ", " + vector.Y.ToString("0.######") + ", " + vector.Z.ToString("0.######");
        }

        /* The state arrays ship at a fixed length with unused slots left null - fill them so every slot
         * has rows to edit, which is what the old grid did when it listed them. */
        private static void FillEmptySlots(IList list)
        {
            Type element = ElementType(list);
            if (element == null || element.IsValueType || element == typeof(string)
                || typeof(AnimationNode).IsAssignableFrom(element))
                return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                    continue;

                try { list[i] = Activator.CreateInstance(element); }
                catch { /* leave the slot empty if it can't be built */ }
            }
        }

        private static Type ElementType(IList list)
        {
            Type type = list.GetType();
            if (type.IsArray)
                return type.GetElementType();

            foreach (Type contract in type.GetInterfaces())
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IList<>))
                    return contract.GetGenericArguments()[0];

            return null;
        }

        private static bool IsNodeCollection(Type type)
        {
            if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
                return false;

            if (type.IsArray)
                return typeof(AnimationNode).IsAssignableFrom(type.GetElementType());

            foreach (Type contract in type.GetInterfaces())
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return typeof(AnimationNode).IsAssignableFrom(contract.GetGenericArguments()[0]);

            return false;
        }

        private static bool IsScalar(Type type)
        {
            return type == typeof(string) || type == typeof(bool)
                || type == typeof(float) || type == typeof(double)
                || type == typeof(sbyte) || type == typeof(byte)
                || type == typeof(short) || type == typeof(ushort)
                || type == typeof(int) || type == typeof(uint)
                || type == typeof(long) || type == typeof(ulong);
        }

        internal static Type TypeOf(MemberInfo member)
        {
            FieldInfo field = member as FieldInfo;
            return field != null ? field.FieldType : ((PropertyInfo)member).PropertyType;
        }

        internal static object Read(object owner, MemberInfo member)
        {
            if (owner == null)
                return null;

            FieldInfo field = member as FieldInfo;
            return field != null ? field.GetValue(owner) : ((PropertyInfo)member).GetValue(owner, null);
        }

        internal static void Write(object owner, MemberInfo member, object value)
        {
            FieldInfo field = member as FieldInfo;
            if (field != null)
                field.SetValue(owner, value);
            else
                ((PropertyInfo)member).SetValue(owner, value, null);
        }

        private static object Read(object owner, string memberName)
        {
            if (owner == null || string.IsNullOrEmpty(memberName))
                return null;

            Type type = owner.GetType();

            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(owner);

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                return property.GetValue(owner, null);

            return null;
        }

        #endregion

        #region ICustomTypeDescriptor

        public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(typeof(AnimationNodeProxy));
        public string GetClassName() => _node == null ? null : _node.Type.ToString();
        public string GetComponentName() => _node?.Name;
        public TypeConverter GetConverter() => TypeDescriptor.GetConverter(typeof(AnimationNodeProxy));
        public EventDescriptor GetDefaultEvent() => null;
        public PropertyDescriptor GetDefaultProperty() => null;
        public object GetEditor(Type editorBaseType) => null;
        public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;
        public EventDescriptorCollection GetEvents(Attribute[] attributes) => EventDescriptorCollection.Empty;
        public object GetPropertyOwner(PropertyDescriptor pd) => this;

        #endregion
    }

    #region Descriptors

    /// <summary>Base for every row in the animation node grid.</summary>
    public abstract class AnimationNodeDescriptor : PropertyDescriptor
    {
        private readonly AnimationNodeProxy _proxy;
        private readonly string _path;

        public AnimationNodeProxy Proxy => _proxy;

        /// <summary>Where the row sits in the node, e.g. "States[3].Value". Identifies it across a rebuild.</summary>
        public string Path => _path;

        protected AnimationNodeDescriptor(AnimationNodeProxy proxy, string path, string label)
            : base(label, null)
        {
            _proxy = proxy;
            _path = path;
        }

        public override Type ComponentType => typeof(AnimationNodeProxy);
        public override bool IsReadOnly => false;
        public override bool CanResetValue(object component) => false;
        public override void ResetValue(object component) { }

        //ShouldSerializeValue == true is what draws a row in bold - our "not the default" mark
        public override bool ShouldSerializeValue(object component) => IsModified();

        /// <summary>The value this row holds on a newly built node of the same class, if there is one.</summary>
        protected virtual bool TryGetDefault(out object value)
        {
            value = null;
            return false;
        }

        /// <summary>Does this row differ from what its class starts out with?</summary>
        public virtual bool IsModified()
        {
            object standard;
            if (!TryGetDefault(out standard))
                return false;

            return !Equals(GetValue(_proxy), standard);
        }

        protected void Edited()
        {
            _proxy.NotifyEdited(this);
        }

        public override bool Equals(object obj)
        {
            return obj is AnimationNodeDescriptor other && other.GetType() == GetType() && other.Path == Path;
        }

        public override int GetHashCode()
        {
            return (Path ?? "").GetHashCode() ^ GetType().GetHashCode();
        }
    }

    /// <summary>A row backed by one field or property of the node, read and written by reflection.</summary>
    public class NodeMemberDescriptor : AnimationNodeDescriptor
    {
        private readonly object _owner;
        private readonly object _untouched;
        private readonly MemberInfo _member;
        private readonly Type _type;

        public NodeMemberDescriptor(AnimationNodeProxy proxy, object owner, object untouched, MemberInfo member, string path, string label)
            : base(proxy, path, label)
        {
            _owner = owner;
            _untouched = untouched;
            _member = member;
            _type = AnimationNodeProxy.TypeOf(member);
        }

        public override Type PropertyType => _type;

        public override object GetValue(object component)
        {
            return Normalise(AnimationNodeProxy.Read(_owner, _member));
        }

        public override void SetValue(object component, object value)
        {
            object converted = Coerce(value);
            if (Equals(AnimationNodeProxy.Read(_owner, _member), converted))
                return;

            AnimationNodeProxy.Write(_owner, _member, converted);
            Edited();
        }

        protected override bool TryGetDefault(out object value)
        {
            value = null;
            if (_untouched == null)
                return false;

            value = Normalise(AnimationNodeProxy.Read(_untouched, _member));
            return true;
        }

        /* A name that refers to something the editor can open is chosen in the browser for it, reached
         * through the grid's own edit button - the same way a scripting parameter's resources are. */
        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
            {
                if (_type == typeof(bool))
                    return new BoolCheckboxEditor();
                if (Name == "AnimationName")
                    return new AnimationPickerEditor();
                if (Name == "BlendSet" || Name == "ExtraBlendSet")
                    return new BlendSetPickerEditor();
            }

            return base.GetEditor(editorBaseType);
        }

        //A missing string and an empty one are the same thing to look at, so they compare as one too
        private object Normalise(object value)
        {
            return value == null && _type == typeof(string) ? "" : value;
        }

        private object Coerce(object value)
        {
            if (value == null)
                return _type == typeof(string) ? "" : Activator.CreateInstance(_type);
            if (_type.IsInstanceOfType(value))
                return value;
            if (_type.IsEnum)
                return Enum.Parse(_type, value.ToString());

            return Convert.ChangeType(value, _type, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The node's name. Renames go through the tree, which keeps its lookups and rejects duplicates.</summary>
    public class NodeNameDescriptor : AnimationNodeDescriptor
    {
        private readonly AnimationNode _untouched;

        public NodeNameDescriptor(AnimationNodeProxy proxy, AnimationNode untouched) : base(proxy, "Name", "Name")
        {
            _untouched = untouched;
        }

        public override Type PropertyType => typeof(string);
        public override object GetValue(object component) => Proxy.Node?.Name ?? "";

        protected override bool TryGetDefault(out object value)
        {
            value = _untouched?.Name ?? "";
            return _untouched != null;
        }

        public override void SetValue(object component, object value)
        {
            AnimationNode node = Proxy.Node;
            if (node == null)
                return;

            string name = (value as string ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Name cannot be empty.", "Rename failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (name == node.Name)
                return;

            try
            {
                if (Proxy.Tree != null)
                    Proxy.Tree.RenameNode(node, name);
                else
                    node.Name = name;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Rename failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Proxy.Editor?.OnNodeRenamed(node);
            Edited();
        }
    }

    /// <summary>A state value that an enumerated selector matches by hashed string rather than by number.</summary>
    public class HashedStringDescriptor : AnimationNodeDescriptor
    {
        private readonly object _owner;
        private readonly object _untouched;
        private readonly MemberInfo _member;
        private readonly Type _type;

        public HashedStringDescriptor(AnimationNodeProxy proxy, object owner, object untouched, MemberInfo member, string path, string label)
            : base(proxy, path, label)
        {
            _owner = owner;
            _untouched = untouched;
            _member = member;
            _type = AnimationNodeProxy.TypeOf(member);
        }

        public override Type PropertyType => typeof(string);

        public override object GetValue(object component)
        {
            return Named(AnimationNodeProxy.Read(_owner, _member));
        }

        public override void SetValue(object component, object value)
        {
            uint id = AnimationHashedString.Parse(value as string);
            object converted = Convert.ChangeType(id, _type, CultureInfo.InvariantCulture);
            if (Equals(AnimationNodeProxy.Read(_owner, _member), converted))
                return;

            AnimationNodeProxy.Write(_owner, _member, converted);
            Edited();
        }

        protected override bool TryGetDefault(out object value)
        {
            value = null;
            if (_untouched == null)
                return false;

            value = Named(AnimationNodeProxy.Read(_untouched, _member));
            return true;
        }

        private static string Named(object raw)
        {
            return AnimationHashedString.Format(raw == null ? 0u : Convert.ToUInt32(raw));
        }
    }

    /// <summary>One bit of a [Flags] enum, shown as a checkbox under the flag set it belongs to.</summary>
    public class FlagBitDescriptor : AnimationNodeDescriptor
    {
        private readonly object _owner;
        private readonly object _untouched;
        private readonly MemberInfo _member;
        private readonly Type _type;
        private readonly long _mask;

        public FlagBitDescriptor(AnimationNodeProxy proxy, object owner, object untouched, MemberInfo member, string path, string label, long mask)
            : base(proxy, path, label)
        {
            _owner = owner;
            _untouched = untouched;
            _member = member;
            _type = AnimationNodeProxy.TypeOf(member);
            _mask = mask;
        }

        public override Type PropertyType => typeof(bool);

        public override object GetValue(object component)
        {
            return IsSet(_owner);
        }

        public override void SetValue(object component, object value)
        {
            bool set = value is bool flag && flag;
            long bits = Bits(_owner);
            long updated = set ? bits | _mask : bits & ~_mask;
            if (updated == bits)
                return;

            AnimationNodeProxy.Write(_owner, _member, Enum.ToObject(_type, updated));
            Edited();
        }

        protected override bool TryGetDefault(out object value)
        {
            value = null;
            if (_untouched == null)
                return false;

            value = IsSet(_untouched);
            return true;
        }

        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(UITypeEditor))
                return new BoolCheckboxEditor();
            return base.GetEditor(editorBaseType);
        }

        private bool IsSet(object owner)
        {
            return (Bits(owner) & _mask) == _mask;
        }

        private long Bits(object owner)
        {
            object value = AnimationNodeProxy.Read(owner, _member);
            return value == null ? 0 : Convert.ToInt64(value);
        }
    }

    /// <summary>One component of a Vector3. The vector is a value type, so each edit rewrites the whole thing.</summary>
    public class VectorPartDescriptor : AnimationNodeDescriptor
    {
        private readonly object _owner;
        private readonly object _untouched;
        private readonly MemberInfo _member;
        private readonly int _component;

        public VectorPartDescriptor(AnimationNodeProxy proxy, object owner, object untouched, MemberInfo member, string path, string label, int component)
            : base(proxy, path, label)
        {
            _owner = owner;
            _untouched = untouched;
            _member = member;
            _component = component;
        }

        public override Type PropertyType => typeof(float);

        public override object GetValue(object component)
        {
            return Part(_owner);
        }

        public override void SetValue(object component, object value)
        {
            float part = Convert.ToSingle(value, CultureInfo.InvariantCulture);
            Vector3 vector = Vector(_owner);

            if (_component == 0)
            {
                if (vector.X == part) return;
                vector.X = part;
            }
            else if (_component == 1)
            {
                if (vector.Y == part) return;
                vector.Y = part;
            }
            else
            {
                if (vector.Z == part) return;
                vector.Z = part;
            }

            AnimationNodeProxy.Write(_owner, _member, vector);
            Edited();
        }

        protected override bool TryGetDefault(out object value)
        {
            value = null;
            if (_untouched == null)
                return false;

            value = Part(_untouched);
            return true;
        }

        private float Part(object owner)
        {
            Vector3 vector = Vector(owner);
            return _component == 0 ? vector.X : _component == 1 ? vector.Y : vector.Z;
        }

        private Vector3 Vector(object owner)
        {
            return AnimationNodeProxy.Read(owner, _member) is Vector3 vector ? vector : Vector3.Zero;
        }
    }

    /// <summary>
    /// A property node's value type. Its concrete class carries the payload, so changing the type
    /// replaces the object - keeping the three flags that are common to all of them.
    /// </summary>
    public class MetadataTypeDescriptor : AnimationNodeDescriptor
    {
        private readonly object _owner;
        private readonly MemberInfo _member;
        private readonly AnimationMetadataValue _untouched;

        public MetadataTypeDescriptor(AnimationNodeProxy proxy, object owner, MemberInfo member, AnimationMetadataValue untouched, string path, string label)
            : base(proxy, path, label)
        {
            _owner = owner;
            _member = member;
            _untouched = untouched;
        }

        public override Type PropertyType => typeof(MetadataValueType);

        public override object GetValue(object component)
        {
            AnimationMetadataValue meta = AnimationNodeProxy.Read(_owner, _member) as AnimationMetadataValue;
            return meta == null ? MetadataValueType.FLOAT32 : meta.ValueType;
        }

        protected override bool TryGetDefault(out object value)
        {
            value = _untouched?.ValueType;
            return _untouched != null;
        }

        public override void SetValue(object component, object value)
        {
            AnimationMetadataValue meta = AnimationNodeProxy.Read(_owner, _member) as AnimationMetadataValue;
            if (meta == null || !(value is MetadataValueType type) || meta.ValueType == type)
                return;

            AnimationMetadataValue replacement = Create(type);
            replacement.RequiresConvert = meta.RequiresConvert;
            replacement.CanMirror = meta.CanMirror;
            replacement.CanModulateByPlayspeed = meta.CanModulateByPlayspeed;

            AnimationNodeProxy.Write(_owner, _member, replacement);
            Edited();

            //The payload row below this one belongs to the old class - the whole node has to be rebuilt
            Proxy.Editor?.RebuildAfterEdit();
        }

        private static AnimationMetadataValue Create(MetadataValueType type)
        {
            switch (type)
            {
                case MetadataValueType.UINT32: return new UIntMetadataValue();
                case MetadataValueType.INT32: return new IntMetadataValue();
                case MetadataValueType.FLOAT32: return new FloatMetadataValue();
                case MetadataValueType.STRING: return new StringMetadataValue();
                case MetadataValueType.BOOL: return new BoolMetadataValue();
                case MetadataValueType.VECTOR: return new VectorMetadataValue();
                case MetadataValueType.UINT64: return new ULongMetadataValue();
                case MetadataValueType.INT64: return new LongMetadataValue();
                case MetadataValueType.FLOAT64: return new Float64MetadataValue();
                case MetadataValueType.AUDIO: return new AudioMetadataValue();
                case MetadataValueType.PROPERTY_REFERENCE: return new PropertyReferenceMetadataValue();
                case MetadataValueType.SCRIPT_INTERFACE: return new ScriptInterfaceMetadataValue();
                default: return new FloatMetadataValue();
            }
        }
    }

    /// <summary>A row that owns child rows - a collection, a nested object, a flag set.</summary>
    public class NodeGroupDescriptor : AnimationNodeDescriptor
    {
        private readonly NodeGroup _group;

        public PropertyDescriptorCollection Children => _group.Children;

        public NodeGroupDescriptor(AnimationNodeProxy proxy, string path, string label, Func<string> summary, IEnumerable<PropertyDescriptor> children)
            : base(proxy, path, label)
        {
            _group = new NodeGroup(summary, children);
        }

        public override Type PropertyType => typeof(NodeGroup);
        public override bool IsReadOnly => true;

        public override object GetValue(object component) => _group;

        //The summary describes the rows below rather than being a value - there is nothing to write here
        public override void SetValue(object component, object value) { }

        /// <summary>Bold when anything under this row is off its default, so a collapsed group still shows it.</summary>
        public override bool IsModified()
        {
            foreach (PropertyDescriptor child in _group.Children)
                if (child is AnimationNodeDescriptor descriptor && descriptor.IsModified())
                    return true;

            return false;
        }
    }

    /// <summary>The value of a group row: a summary line, and the rows that sit under it.</summary>
    [TypeConverter(typeof(NodeGroupConverter))]
    public sealed class NodeGroup
    {
        private readonly Func<string> _summary;

        public PropertyDescriptorCollection Children { get; }

        public NodeGroup(Func<string> summary, IEnumerable<PropertyDescriptor> children)
        {
            _summary = summary;
            Children = new PropertyDescriptorCollection(children.ToArray(), true);
        }

        public override string ToString()
        {
            return _summary == null ? "" : _summary();
        }
    }

    /// <summary>Expands a group row into the rows it owns.</summary>
    public class NodeGroupConverter : ExpandableObjectConverter
    {
        public override bool GetPropertiesSupported(ITypeDescriptorContext context) => true;

        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
        {
            return (value as NodeGroup)?.Children ?? PropertyDescriptorCollection.Empty;
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
                return value == null ? "" : value.ToString();

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    #endregion

    #region Editors

    /// <summary>Opens the animation browser for a row that names a clip.</summary>
    public class AnimationPickerEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            AnimationNodeDescriptor descriptor = context?.PropertyDescriptor as AnimationNodeDescriptor;
            descriptor?.Proxy?.Editor?.PickAnimation(descriptor);
            return value;
        }
    }

    /// <summary>Opens the blend set editor for a row that names one.</summary>
    public class BlendSetPickerEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            AnimationNodeDescriptor descriptor = context?.PropertyDescriptor as AnimationNodeDescriptor;
            descriptor?.Proxy?.Editor?.PickBlendSet(descriptor);
            return value;
        }
    }

    #endregion

    /// <summary>Names behind the hashed string ids an enumerated selector matches on.</summary>
    public static class AnimationHashedString
    {
        public static string Format(uint id)
        {
            if (id == 0)
                return "";

            AnimationStrings strings = Singleton.AnimationStrings_Debug;
            if (strings != null && strings.Entries.TryGetValue(id, out string name))
                return name;

            return id.ToString();
        }

        public static uint Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            AnimationStrings strings = Singleton.AnimationStrings_Debug;
            if (strings == null)
                return uint.TryParse(value, out uint plain) ? plain : Utilities.AnimationHashedString(value);

            foreach (KeyValuePair<uint, string> entry in strings.Entries)
                if (entry.Value == value)
                    return entry.Key;

            //Unresolved hashes are shown as decimal; keep that round-trip intact
            if (uint.TryParse(value, out uint raw) && !strings.Entries.ContainsKey(raw))
                return raw;

            return strings.GetID(value);
        }
    }
}
