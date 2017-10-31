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
using System.Reflection;
using Brainiac.Design.Attributes;

namespace Brainiac.Design.FlexibleProperties
{
	public interface CanUseDefinition
	{
		Type PropertyType { get; }
		string DefinitionName { get; set; }
		string DefinitionMember { get; set; }
	}

	public partial class DesignerAIDefinitionEditor : Brainiac.Design.Attributes.DesignerPropertyEditor
	{
		public DesignerAIDefinitionEditor()
		{
			InitializeComponent();
		}

		public override void SetProperty(DesignerPropertyInfo property, object obj)
		{
			base.SetProperty(property, obj);

			CanUseDefinition canUseDefinition= (CanUseDefinition)property.Property.GetValue(obj, null);

			if(AIType.Current !=null)
			{
				Type type= canUseDefinition.PropertyType;

				foreach(Definition definition in AIType.Current.Definitions)
				{
					PropertyInfo[] properties= definition.Object.GetType().GetProperties(BindingFlags.Instance|BindingFlags.Public);

					foreach(PropertyInfo prop in properties)
					{
						if(!prop.CanRead || prop.PropertyType !=type)
							continue;

						comboBox.Items.Add(definition.Name +'.'+ prop.Name);
					}
				}
			}

			if(canUseDefinition.DefinitionName !=string.Empty && canUseDefinition.DefinitionMember !=string.Empty)
			{
				string str= canUseDefinition.DefinitionName +'.'+ canUseDefinition.DefinitionMember;
				if(!comboBox.Items.Contains(str))
					comboBox.Items.Insert(0, str);

				comboBox.Text= str;
			}
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

			CanUseDefinition canUseDefinition= (CanUseDefinition)_property.Property.GetValue(_object, null);

			string[] str= comboBox.Text.Split('.');
			Debug.Check(str.Length ==2);

			canUseDefinition.DefinitionName= str[0];
			canUseDefinition.DefinitionMember= str[1];

			OnValueChanged();
		}
	}
}
