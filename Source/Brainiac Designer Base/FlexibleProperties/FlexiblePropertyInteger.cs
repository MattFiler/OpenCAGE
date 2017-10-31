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
	public class FlexiblePropertyInteger : FlexibleProperty, CanUseDefinition
	{
		protected int _min;
		public int Min
		{
			get { return _min; }
			set { _min= value; }
		}

		protected int _max;
		public int Max
		{
			get { return _max; }
			set { _max= value; }
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
			if(_selectedEditor ==null || _selectedEditor ==typeof(DesignerNumberEditor))
				return string.Format(CultureInfo.InvariantCulture, "{0}", _min);

			if(_selectedEditor ==typeof(DesignerRandomNumberEditor))
			{
				if(_min ==_max)
					return string.Format(CultureInfo.InvariantCulture, "{0}", _min);

				return string.Format(CultureInfo.InvariantCulture, "{0} to {1}", _min, _max);
			}

			if(_selectedEditor ==typeof(DesignerAIDefinitionNumberEditor))
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
			return string.Format(CultureInfo.InvariantCulture, "{0}f", _min);
		}

		protected override void FromStringValue(Type editor, string[] str)
		{
			if(editor ==typeof(DesignerNumberEditor))
			{
				if(str.Length !=1)
					throw new Exception( string.Format(Resources.ExceptionFlexiblePropertyInvalidValue, str) );

				int result;
				if(int.TryParse(str[0], NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out result))
				{
					_min= result;
					_max= result;
				}

				return;
			}

			if(editor ==typeof(DesignerRandomNumberEditor))
			{
				if(str.Length !=2)
					throw new Exception( string.Format(Resources.ExceptionFlexiblePropertyInvalidValue, str) );

				int result;
				if(int.TryParse(str[0], NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out result))
					_min= result;

				if(int.TryParse(str[1], NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out result))
					_max= result;

				return;
			}

			if(editor ==typeof(DesignerAIDefinitionNumberEditor))
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
			if(_selectedEditor ==typeof(DesignerNumberEditor))
			{
				values.Add( string.Format(CultureInfo.InvariantCulture, "{0}", _min) );
				return;
			}

			if(_selectedEditor ==typeof(DesignerRandomNumberEditor))
			{
				values.Add( string.Format(CultureInfo.InvariantCulture, "{0}", _min) );
				values.Add( string.Format(CultureInfo.InvariantCulture, "{0}", _max) );
				return;
			}

			if(_selectedEditor ==typeof(DesignerAIDefinitionNumberEditor))
			{
				values.Add(_definitionName);
				values.Add(_definitionMember);
				return;
			}

			throw new Exception( string.Format(Resources.ExceptionFlexiblePropertyInvalidEditor, _selectedEditor.FullName) );
		}

		public override DesignerProperty.EditorType[] GetEditorTypes()
		{
			return new DesignerProperty.EditorType[] { new DesignerProperty.EditorType("EditorValue", typeof(DesignerNumberEditor)), new DesignerProperty.EditorType("EditorRandom", typeof(DesignerRandomNumberEditor)), new DesignerProperty.EditorType("EditorAIDefinition", typeof(DesignerAIDefinitionNumberEditor)) };
		}

		public override void ChangeEditor(Type newEditor)
		{
			if(newEditor ==typeof(DesignerNumberEditor))
				_max= _min;

			base.ChangeEditor(newEditor);
		}

		protected override void CloneProperties(FlexibleProperty flexProp)
		{
			FlexiblePropertyInteger prop= (FlexiblePropertyInteger)flexProp;
			prop._min= _min;
			prop._max= _max;
			prop._definitionName= _definitionName;
			prop._definitionMember= _definitionMember;

			base.CloneProperties(flexProp);
		}

		public Type PropertyType
		{
			get { return typeof(int); }
		}
	}
}
