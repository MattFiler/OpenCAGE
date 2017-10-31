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

namespace Brainiac.Design.Attributes
{
	public partial class DesignerBooleanEditor : Brainiac.Design.Attributes.DesignerPropertyEditor
	{
		public DesignerBooleanEditor()
		{
			InitializeComponent();
		}

		public override void SetProperty(DesignerPropertyInfo property, object obj)
		{
			base.SetProperty(property, obj);

			DesignerBoolean boolAtt= property.Attribute as DesignerBoolean;
			if(boolAtt !=null)
			{
				checkBox.Checked= (bool)property.Property.GetValue(obj, null);
			}

			DesignerFlexibleBoolean flexBooleanAtt= property.Attribute as DesignerFlexibleBoolean;
			if(flexBooleanAtt !=null)
			{
				FlexiblePropertyBoolean flexProp= (FlexiblePropertyBoolean)property.Property.GetValue(obj, null);
				checkBox.Checked= flexProp.Value;
			}
		}

		private void checkBox_CheckedChanged(object sender, EventArgs e)
		{
			if(!_valueWasAssigned)
				return;

			if(_property.Attribute is DesignerBoolean)
				_property.Property.SetValue(_object, checkBox.Checked, null);

			if(_property.Attribute is DesignerFlexibleBoolean)
			{
				FlexiblePropertyBoolean flexProp= (FlexiblePropertyBoolean)_property.Property.GetValue(_object, null);
				flexProp.Value= checkBox.Checked;
			}

			OnValueChanged();
		}

		public override void ReadOnly()
		{
			base.ReadOnly();

			checkBox.Enabled= false;
		}
	}
}
