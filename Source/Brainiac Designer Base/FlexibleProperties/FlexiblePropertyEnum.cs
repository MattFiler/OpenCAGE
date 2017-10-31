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
using Brainiac.Design.Attributes;
using Brainiac.Design.Properties;
using System.Globalization;
using System.Reflection;

namespace Brainiac.Design.FlexibleProperties
{
	public class FlexiblePropertyEnum : FlexibleProperty, CanUseDefinition
	{
		protected int _value;
		public int Value
		{
			get { return _value; }
			set { _value= value; }
		}

		protected Type _enumType;
		public Type EnumType
		{
			get { return _enumType; }
		}

		protected string _definitionName= string.Empty;
		public string DefinitionName
		{
			get { return _definitionName; }
			set { _definitionName= value; }
		}

		protected string _definitionMember= string.Empty;
		public string DefinitionMember
		{
			get { return _definitionMember; }
			set { _definitionMember= value; }
		}

		public override string GetDisplayValue()
		{
			if(_selectedEditor ==null || _selectedEditor ==typeof(DesignerEnumEditor))
			{
				if(!_enumType.IsEnum)
					throw new Exception( string.Format(Resources.ExceptionDesignerAttributeExpectedEnum, _value) );

				int enumval= (int)_value;

				string enumName= Enum.GetName(_enumType, enumval);
				if(enumName ==string.Empty)
					throw new Exception( string.Format(Resources.ExceptionDesignerAttributeEnumValueIllegal, enumval) );

				return enumName;
			}

			if(_selectedEditor ==typeof(DesignerAIDefinitionEditor))
			{
				string val= "???";
				if(AIType.Current !=null)
				{
					Definition definition= AIType.Current.FindDefinition(_definitionName);
					if(definition !=null)
					{
						PropertyInfo property= definition.Object.GetType().GetProperty(_definitionMember);
						val= property.GetValue(definition.Object, null).ToString();
					}
				}

				if(_definitionName ==string.Empty || _definitionMember ==string.Empty)
					return "???";

				return string.Format(CultureInfo.InvariantCulture, "{0}.{1} ({2})", _definitionName, _definitionMember, val);
			}

			throw new Exception( string.Format(Resources.ExceptionFlexiblePropertyInvalidEditor, _selectedEditor.FullName) );
		}

		public override string GetExportValue()
		{
			Type type= _value.GetType();

			switch(DesignerEnum.ExportTextMode)
			{
				case(DesignerEnum.ExportMode.Namespace_Type_Value): return string.Format("{0}{1}.{2}", DesignerEnum.ExportPrefix, type.FullName.Replace('+', '.'), Enum.GetName(type, (int)_value));
				case(DesignerEnum.ExportMode.Type_Value): return string.Format("{0}{1}.{2}", DesignerEnum.ExportPrefix, type.Name, Enum.GetName(type, (int)_value));
				case(DesignerEnum.ExportMode.Value): return string.Format("{0}{1}", DesignerEnum.ExportPrefix, Enum.GetName(type, (int)_value));
			}

			Debug.Check(false);
			return null;
		}

		protected override void FromStringValue(Type editor, string[] str)
		{
			if(editor ==typeof(DesignerEnumEditor))
			{
				if(str.Length !=2)
					throw new Exception( string.Format(Resources.ExceptionFlexiblePropertyInvalidValue, str) );

				string enumname= str[0];

				int enumval;
				try
				{
					// try to get the enum value by name
					enumval= (int)Enum.Parse(_enumType, enumname, true);
				}
				catch
				{
					// try to read the stored enum value index
					if(!int.TryParse(str[1], NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out enumval))
						throw new Exception( string.Format(Resources.ExceptionDesignerAttributeEnumCouldNotParseValue, str) );

					// try to get the enum value by index
					if(Enum.ToObject(_enumType, enumval) ==null)
						throw new Exception( string.Format(Resources.ExceptionDesignerAttributeEnumIllegalEnumIndex, enumval) );
				}

				_value= enumval;

				return;
			}

			if(editor ==typeof(DesignerAIDefinitionEditor))
			{
				if(str.Length !=2)
					throw new Exception( string.Format(Resources.ExceptionFlexiblePropertyInvalidValue, str) );

				_definitionName= str[0];
				_definitionMember= str[1];

				return;
			}

			throw new Exception( string.Format(Resources.ExceptionFlexiblePropertyInvalidEditor, editor.FullName) );
		}

		protected override void GetStringValues(ref List<string> values)
		{
			if(_selectedEditor ==typeof(DesignerEnumEditor))
			{
				string enumName= Enum.GetName(_enumType, _value);
				int enumval= (int)_value;

				values.Add(enumName);
				values.Add(enumval.ToString());
				return;
			}

			if(_selectedEditor ==typeof(DesignerAIDefinitionEditor))
			{
				values.Add(_definitionName);
				values.Add(_definitionMember);
				return;
			}

			throw new Exception( string.Format(Resources.ExceptionFlexiblePropertyInvalidEditor, _selectedEditor.FullName) );
		}

		public override DesignerProperty.EditorType[] GetEditorTypes()
		{
			return new DesignerProperty.EditorType[] { new DesignerProperty.EditorType("EditorValue", typeof(DesignerEnumEditor)), new DesignerProperty.EditorType("EditorAIDefinition", typeof(DesignerAIDefinitionEditor)) };
		}

		protected override void CloneProperties(FlexibleProperty flexProp)
		{
			FlexiblePropertyEnum prop= (FlexiblePropertyEnum)flexProp;
			prop._enumType= _enumType;
			prop._value= _value;
			prop._definitionName= _definitionName;
			prop._definitionMember= _definitionMember;

			base.CloneProperties(flexProp);
		}

		public FlexiblePropertyEnum(Type enumType)
		{
			if(!enumType.IsEnum)
				throw new Exception( string.Format(Resources.ExceptionDesignerAttributeExpectedEnum, enumType) );

			_enumType= enumType;
		}

		public Type PropertyType
		{
			get { return _enumType; }
		}
	}
}
