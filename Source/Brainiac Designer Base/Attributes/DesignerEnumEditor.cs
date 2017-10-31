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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Brainiac.Design.FlexibleProperties;
using Brainiac.Design.Properties;

namespace Brainiac.Design.Attributes
{
	public partial class DesignerEnumEditor : Brainiac.Design.Attributes.DesignerPropertyEditor
	{
		public DesignerEnumEditor()
		{
			InitializeComponent();
		}

		private List<object> _values= new List<object>();

		public override void SetProperty(DesignerPropertyInfo property, object obj)
		{
			base.SetProperty(property, obj);

			object[] excludedElements= null;
			string enumName= string.Empty;
			Type enumtype= null;

			DesignerEnum enumAtt= property.Attribute as DesignerEnum;
			if(enumAtt !=null)
			{
				excludedElements= enumAtt.ExcludedElements;
				enumName= Enum.GetName(property.Property.PropertyType, property.Property.GetValue(obj, null));
				enumtype= property.Property.PropertyType;
			}

			DesignerFlexibleEnum flexEnumAtt= property.Attribute as DesignerFlexibleEnum;
			if(flexEnumAtt !=null)
			{
				excludedElements= flexEnumAtt.ExcludedElements;

				FlexiblePropertyEnum flexProp= (FlexiblePropertyEnum)property.Property.GetValue(obj, null);
				enumName= Enum.GetName(flexProp.EnumType, flexProp.Value);
				enumtype= flexProp.EnumType;
			}

			if(enumtype ==null)
				throw new Exception( string.Format(Resources.ExceptionDesignerAttributeExpectedEnum, property.Property.Name) );

			Array list= Enum.GetValues(enumtype);
			foreach(object enumVal in list)
			{
				bool excluded= false;

				if(excludedElements !=null)
				{
					for(int i= 0; i <excludedElements.Length; ++i)
					{
						if(excludedElements[i].Equals(enumVal))
						{
							excluded= true;
							break;
						}
					}
				}

				if(!excluded)
				{
					comboBox.Items.Add( Enum.GetName(enumtype, enumVal) );
					_values.Add(enumVal);
				}
			}

			comboBox.Text= enumName;
		}

		public override void ReadOnly()
		{
			base.ReadOnly();

			comboBox.Enabled= false;
		}

		private void comboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			if(!_valueWasAssigned)
				return;

			if(_property.Attribute is DesignerEnum)
				_property.Property.SetValue(_object, _values[comboBox.SelectedIndex], null);

			if(_property.Attribute is DesignerFlexibleEnum)
			{
				FlexiblePropertyEnum flexProp= (FlexiblePropertyEnum)_property.Property.GetValue(_object, null);
				flexProp.Value= (int)_values[comboBox.SelectedIndex];
			}

			OnValueChanged();
		}
	}
}
