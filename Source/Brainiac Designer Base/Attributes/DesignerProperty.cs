////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2009, Daniel Kollmann
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted
// provided that the following conditions are met:
//
// - Redistributions of source code must retain the above copyright notice, this list of conditions
//   and the following disclaimer.
//
// - Redistributions in binary form must reproduce the above copyright notice, this list of
//   conditions and the following disclaimer in the documentation and/or other materials provided
//   with the distribution.
//
// - Neither the name of Daniel Kollmann nor the names of its contributors may be used to endorse
//   or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR
// IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY
// WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Globalization;
using System.Reflection;
using Brainiac.Design.Properties;

namespace Brainiac.Design.Attributes
{
	public struct DesignerPropertyInfo
	{
		private PropertyInfo _property;

		public PropertyInfo Property
		{
			get { return _property; }
		}

		private DesignerProperty _attribute;

		public DesignerProperty Attribute
		{
			get { return _attribute; }
		}

		public DesignerPropertyInfo(PropertyInfo property)
		{
			_property= property;

			DesignerProperty[] attributes= (DesignerProperty[]) _property.GetCustomAttributes(typeof(DesignerProperty), true);

			if(attributes.Length !=1)
				throw new Exception(Resources.ExceptionMultipleDesignerAttributes);

			_attribute= attributes[0];
		}

		public DesignerPropertyInfo(PropertyInfo property, DesignerProperty attribute)
		{
			_property= property;
			_attribute= attribute;
		}

		/// <summary>
		/// Returns the property's value.
		/// </summary>
		/// <param name="node">The node we want to get the value from.</param>
		/// <returns>The value as an object.</returns>
		public object GetValue(Nodes.Node node)
		{
			return _property.GetValue(node, null);
		}

		/// <summary>
		/// Returns the property's value.
		/// </summary>
		/// <param name="node">The event we want to get the value from.</param>
		/// <returns>The value as an object.</returns>
		public object GetValue(Events.Event evnt)
		{
			return _property.GetValue(evnt, null);
		}

		/// <summary>
		/// Returns the property's value as a string.
		/// </summary>
		/// <param name="node">The node we want to get the value from.</param>
		/// <returns>The value as a string.</returns>
		public string GetStringValue(Nodes.Node node)
		{
			return _attribute.GetStringValue( _property.GetValue(node, null) );
		}

		/// <summary>
		/// Returns the property's value as a string.
		/// </summary>
		/// <param name="node">The event we want to get the value from.</param>
		/// <returns>The value as a string.</returns>
		public string GetStringValue(Events.Event evnt)
		{
			return _attribute.GetStringValue( _property.GetValue(evnt, null) );
		}

		/// <summary>
		/// Returns the property's value as a string.
		/// </summary>
		/// <param name="node">The comment we want to get the value from.</param>
		/// <returns>The value as a string.</returns>
		public string GetStringValue(Nodes.Node.Comment comment)
		{
			return _attribute.GetStringValue( _property.GetValue(comment, null) );
		}

		/// <summary>
		/// Returns the property's value as a string for displaying, skipping any encoding.
		/// </summary>
		/// <param name="node">The node whose value we want to get.</param>
		/// <returns>The value as a string.</returns>
		public string GetDisplayValue(Nodes.Node node)
		{
			return _attribute.GetDisplayValue( _property.GetValue(node, null) );
		}

		/// <summary>
		/// Returns the property's value as a string for displaying, skipping any encoding.
		/// </summary>
		/// <param name="evnt">The event whose value we want to get.</param>
		/// <returns>The value as a string.</returns>
		public string GetDisplayValue(Events.Event evnt)
		{
			return _attribute.GetDisplayValue( _property.GetValue(evnt, null) );
		}

		/// <summary>
		/// Returns the property's value as a string for exporting, skipping any encoding.
		/// </summary>
		/// <param name="obj">The value stored in the property unencoded.</param>
		/// <returns>The value as a string.</returns>
		public string GetExportValue(Events.Event evnt)
		{
			return _attribute.GetExportValue( _property.GetValue(evnt, null) );
		}

		/// <summary>
		/// Returns the property's value as a string for exporting, skipping any encoding.
		/// </summary>
		/// <param name="obj">The value stored in the property unencoded.</param>
		/// <returns>The value as a string.</returns>
		public string GetExportValue(Nodes.Node node)
		{
			return _attribute.GetExportValue( _property.GetValue(node, null) );
		}

		/// <summary>
		/// Sets the property's value based on a string.
		/// </summary>
		/// <param name="type">The type of the property.</param>
		/// <param name="str">The string representing the value to be set.</param>
		/// <returns>Returns the value encoded in the string in the correct type given.</returns>
		public object FromStringValue(Type type, string str)
		{
			return _attribute.FromStringValue(type, str);
		}

		/// <summary>
		/// Sets the value of the property for the given node from a string.
		/// </summary>
		/// <param name="node">The node we want to set the value on.</param>
		/// <param name="valueString">The string holding the value.</param>
		public void SetValueFromString(Nodes.Node node, string valueString)
		{
			_property.SetValue(node, FromStringValue(_property.PropertyType, valueString), null);
		}

		/// <summary>
		/// Sets the value of the property for the given event from a string.
		/// </summary>
		/// <param name="evnt">The event we want to set the value on.</param>
		/// <param name="valueString">The string holding the value.</param>
		public void SetValueFromString(Events.Event evnt, string valueString)
		{
			_property.SetValue(evnt, FromStringValue(_property.PropertyType, valueString), null);
		}

		/// <summary>
		/// Sets the value of the property for the given comment from a string.
		/// </summary>
		/// <param name="evnt">The comment we want to set the value on.</param>
		/// <param name="valueString">The string holding the value.</param>
		public void SetValueFromString(Nodes.Node.Comment comment, string valueString)
		{
			_property.SetValue(comment, FromStringValue(_property.PropertyType, valueString), null);
		}
	}

	/// <summary>
	/// The base class for all designer attributes. Any designer attribute must be able to be saved and loaded.
	/// </summary>
	public abstract class DesignerProperty : Attribute
	{
		public struct EditorType
		{
			private string _name;
			public string Name
			{
				get { return Plugin.GetResourceString(_name); }
			}

			private Type _editor;
			public Type Editor
			{
				get { return _editor; }
			}

			public EditorType(string name, Type editor)
			{
				_name= name;
				_editor= editor;

				Debug.Check(editor.IsSubclassOf(typeof(DesignerPropertyEditor)));
			}
		}

		[Flags]
		public enum DesignerFlags
		{
			NoFlags = 0,
			ReadOnly = 1,
			NoSave = 2,
			NoExport = 4
		}

		/// <summary>
		/// The enumeration defines how this attribute will be visualised in the editor.
		/// </summary>
		public enum DisplayMode { NoDisplay, Parameter, List }

		/// <summary>
		/// This method is used to sort properties by their name.
		/// </summary>
		public static int SortByName(DesignerPropertyInfo a, DesignerPropertyInfo b)
		{
			return a.Property.Name.CompareTo( b.Property.Name );
		}

		/// <summary>
		/// This method is used to sort properties by their display order.
		/// </summary>
		public static int SortByDisplayOrder(DesignerPropertyInfo a, DesignerPropertyInfo b)
		{
			if(a.Attribute.DisplayOrder ==b.Attribute.DisplayOrder)
				return 0;

			return a.Attribute.DisplayOrder <b.Attribute.DisplayOrder ? -1 : 1;
		}

				/// <summary>
		/// Returns a list of all properties which have a designer attribute attached.
		/// </summary>
		/// <param name="type">The type we want to get the properties from.</param>
		/// <returns>A list of all properties relevant to the designer.</returns>
		public static IList<DesignerPropertyInfo> GetDesignerProperties(Type type)
		{
			return GetDesignerProperties(type, SortByName);
		}

		/// <summary>
		/// Returns a list of all properties which have a designer attribute attached.
		/// </summary>
		/// <param name="type">The type we want to get the properties from.</param>
		/// <param name="comparison">The comparison used to sort the design properties.</param>
		/// <returns>A list of all properties relevant to the designer.</returns>
		public static IList<DesignerPropertyInfo> GetDesignerProperties(Type type, Comparison<DesignerPropertyInfo> comparison)
		{
			List<DesignerPropertyInfo> list= new List<DesignerPropertyInfo>();

			PropertyInfo[] properties= type.GetProperties(BindingFlags.Instance|BindingFlags.Public);

			foreach(PropertyInfo property in properties)
			{
				DesignerProperty[] attributes= (DesignerProperty[]) property.GetCustomAttributes(typeof(DesignerProperty), true);

				if(attributes.Length >1)
					throw new Exception(Resources.ExceptionMultipleDesignerAttributes);

				if(attributes.Length ==1)
				{
					// all properties with a designer attribute must be able to be read and written
					if(!property.CanRead)
						throw new Exception(Resources.ExceptionPropertyCannotBeRead);

					// all properties with a designer attribute must be able to be written or marked as read-only and no-save
					if(!property.CanWrite && !attributes[0].HasFlags(DesignerFlags.ReadOnly|DesignerFlags.NoSave))
						throw new Exception(Resources.ExceptionPropertyCannotBeWritten);

					list.Add( new DesignerPropertyInfo(property, attributes[0]) );
				}
			}

			list.Sort(comparison);

			return list.AsReadOnly();
		}

		protected readonly string _displayName;
		protected readonly string _description;
		protected readonly string _category;
		protected readonly DisplayMode _displayMode;
		protected readonly DesignerFlags _flags;
		protected readonly EditorType[] _editorTypes;
		protected readonly int _displayOrder;

		/// <summary>
		/// Gets the name shown on the node and in the property editor for the property.
		/// </summary>
		public string DisplayName
		{
			get { return Plugin.GetResourceString(_displayName); }
		}

		/// <summary>
		/// Gets the description shown in the property editor for the property.
		/// </summary>
		public string Description
		{
			get { return Plugin.GetResourceString(_description); }
		}

		/// <summary>
		/// Gets the category shown in the property editor for the property.
		/// </summary>
		public string Category
		{
			get { return Plugin.GetResourceString(_category); }
		}

		/// <summary>
		/// Returns the resource used for the category.
		/// </summary>
		public string CategoryResourceString
		{
			get { return _category; }
		}

		/// <summary>
		/// Gets how the property is visualised in the editor.
		/// </summary>
		public DisplayMode Display
		{
			get { return _displayMode; }
		}

		/// <summary>
		/// Gets the type of the editor used in the property grid.
		/// </summary>
		public virtual EditorType[] GetEditorTypes(object obj)
		{
			return _editorTypes;
		}

		/// <summary>
		/// The editor which is currently selected.
		/// </summary>
		public virtual Type GetSelectedEditorType(object obj)
		{
			return _editorTypes.Length >0 ? _editorTypes[0].Editor : null;
		}

		/// <summary>
		/// Defines the order the properties will be sorted in when shown in the property grid. Lower come first.
		/// </summary>
		public int DisplayOrder
		{
			get { return _displayOrder; }
		}

		/// <summary>
		/// Returns if the property has given flags.
		/// </summary>
		/// <param name="flags">The flags we want to check.</param>
		/// <returns>Returns true when all given flags were found.</returns>
		public bool HasFlags(DesignerFlags flags)
		{
			return (_flags & flags) ==flags;
		}

		/// <summary>
		/// Returns the property's value as a string for displaying, skipping any encoding.
		/// </summary>
		/// <param name="obj">The value stored in the property unencoded.</param>
		/// <returns>The value as a string.</returns>
		public abstract string GetDisplayValue(object obj);

		/// <summary>
		/// Returns the property's value as a string for exporting, skipping any encoding.
		/// </summary>
		/// <param name="obj">The value stored in the property unencoded.</param>
		/// <returns>The value as a string.</returns>
		public abstract string GetExportValue(object obj);

		/// <summary>
		/// Creates a new designer attribute.
		/// </summary>
		/// <param name="displayName">The name shown on the node and in the property editor for the property.</param>
		/// <param name="description">The description shown in the property editor for the property.</param>
		/// <param name="category">The category shown in the property editor for the property.</param>
		/// <param name="displayMode">Defines how the property is visualised in the editor.</param>
		/// <param name="displayOrder">Defines the order the properties will be sorted in when shown in the property grid. Lower come first.</param>
		/// <param name="flags">Defines the designer flags stored for the property.</param>
		/// <param name="editorTypes">The type of the editor used in the property grid.</param>
		protected DesignerProperty(string displayName, string description, string category, DisplayMode displayMode, int displayOrder, DesignerFlags flags, EditorType[] editorTypes)
		{
			_displayName= displayName;
			_description= description;
			_category= category;
			_displayMode= displayMode;
			_displayOrder= displayOrder;
			_flags= flags;
			_editorTypes= editorTypes;
		}

		/// <summary>
		/// Returns the property's value as a string.
		/// </summary>
		/// <param name="obj">The value stored in the property.</param>
		/// <returns>The value as a string.</returns>
		public virtual string GetStringValue(object obj)
		{
			return GetDisplayValue(obj);
		}

		/// <summary>
		/// Sets the property's value based on a string.
		/// </summary>
		/// <param name="type">The type of the property.</param>
		/// <param name="str">The string representing the value to be set.</param>
		/// <returns>Returns the value encoded in the string in the correct type given.</returns>
		public abstract object FromStringValue(Type type, string str);
	}
}
