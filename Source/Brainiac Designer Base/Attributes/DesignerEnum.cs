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
using System.Globalization;
using Brainiac.Design.Properties;

namespace Brainiac.Design.Attributes
{
	[AttributeUsage(AttributeTargets.Property)]
	public class DesignerEnum : DesignerProperty
	{
		public enum ExportMode { Namespace_Type_Value, Type_Value, Value }
		protected static ExportMode __exportMode= ExportMode.Namespace_Type_Value;

		/// <summary>
		/// Defines is enumerations are exported as Namespace.Enum.Value or simply as Value.
		/// </summary>
		public static ExportMode ExportTextMode
		{
			get { return __exportMode; }
			set { __exportMode= value; }
		}

		protected static string __exportPrefix= string.Empty;

		/// <summary>
		/// This prefix is placed in front of the enum's value when exporting;
		/// </summary>
		public static string ExportPrefix
		{
			get { return __exportPrefix; }
			set { __exportPrefix= value; }
		}

		protected object[] _excludedElements;

		/// <summary>
		/// The values of the enum which will not be shown in the designer.
		/// </summary>
		public object[] ExcludedElements
		{
			get { return _excludedElements; }
		}

		/// <summary>
		/// Creates a new designer attribute for handling an enumeration value.
		/// </summary>
		/// <param name="displayName">The name shown on the node and in the property editor for the property.</param>
		/// <param name="description">The description shown in the property editor for the property.</param>
		/// <param name="category">The category shown in the property editor for the property.</param>
		/// <param name="displayMode">Defines how the property is visualised in the editor.</param>
		/// <param name="displayOrder">Defines the order the properties will be sorted in when shown in the property grid. Lower come first.</param>
		/// <param name="flags">Defines the designer flags stored for the property.</param>
		/// <param name="exclude">The enum values which will be excluded from the values shown in the designer.</param>
		public DesignerEnum(string displayName, string description, string category, DisplayMode displayMode, int displayOrder, DesignerFlags flags, object[] exclude) : base(displayName, description, category, displayMode, displayOrder, flags, new EditorType[] { new EditorType("EditorValue", typeof(DesignerEnumEditor)) })
		{
			_excludedElements= exclude;
		}

		public override string GetDisplayValue(object obj)
		{
			Type type= obj.GetType();

			if(!type.IsEnum)
				throw new Exception( string.Format(Resources.ExceptionDesignerAttributeExpectedEnum, obj) );

			int enumval= (int)obj;

			string enumName= Enum.GetName(type, enumval);
			if(enumName ==string.Empty)
				throw new Exception( string.Format(Resources.ExceptionDesignerAttributeEnumValueIllegal, enumval) );

			return enumName;
		}

		public override string GetExportValue(object obj)
		{
			Type type= obj.GetType();

			switch(__exportMode)
			{
				case(ExportMode.Namespace_Type_Value): return string.Format("{0}{1}.{2}", __exportPrefix, type.FullName.Replace('+', '.'), Enum.GetName(type, (int)obj));
				case(ExportMode.Type_Value): return string.Format("{0}{1}.{2}", __exportPrefix, type.Name, Enum.GetName(type, (int)obj));
				case(ExportMode.Value): return string.Format("{0}{1}", __exportPrefix, Enum.GetName(type, (int)obj));
			}

			Debug.Check(false);
			return null;
		}

		public override string GetStringValue(object obj)
		{
			string enumName= GetDisplayValue(obj);
			int enumval= (int)obj;

			return string.Format("{0}:{1}", enumName, enumval);
		}

		public override object FromStringValue(Type type, string str)
		{
			if(!type.IsEnum)
				throw new Exception( string.Format(Resources.ExceptionDesignerAttributeExpectedEnum, type) );

			string[] parts= str.Split(':');

			if(parts.Length !=2)
			{
				// keep compatibility with version 1
				//throw new Exception( string.Format(Resources.ExceptionDesignerAttributeEnumCouldNotReadValue, str) );
				parts= new string[] { str, "-1" };
			}

			string enumname= parts[0];

			int enumval;
			try
			{
				// try to get the enum value by name
				enumval= (int)Enum.Parse(type, enumname, true);
			}
			catch
			{
				// try to read the stored enum value index
				if(!int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out enumval))
					throw new Exception( string.Format(Resources.ExceptionDesignerAttributeEnumCouldNotParseValue, str) );

				// try to get the enum value by index
				if(Enum.ToObject(type, enumval) ==null)
					throw new Exception( string.Format(Resources.ExceptionDesignerAttributeEnumIllegalEnumIndex, enumval) );
			}

			return enumval;
		}
	}
}
