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
	public partial class DesignerNumberEditor : Brainiac.Design.Attributes.DesignerPropertyEditor
	{
		public DesignerNumberEditor()
		{
			InitializeComponent();
		}

		public override void SetProperty(DesignerPropertyInfo property, object obj)
		{
			base.SetProperty(property, obj);

			DesignerFloat floatAtt= property.Attribute as DesignerFloat;
			if(floatAtt !=null)
			{
				numericUpDown.DecimalPlaces= floatAtt.Precision;
				numericUpDown.Minimum= (decimal)floatAtt.Min;
				numericUpDown.Maximum= (decimal)floatAtt.Max;
				numericUpDown.Increment= (decimal)floatAtt.Steps;

				float val= (float)property.Property.GetValue(obj, null);
				numericUpDown.Value= (decimal)val;

				unitLabel.Text= floatAtt.Units;
			}

			DesignerInteger intAtt= property.Attribute as DesignerInteger;
			if(intAtt !=null)
			{
				numericUpDown.DecimalPlaces= 0;
				numericUpDown.Minimum= (decimal)intAtt.Min;
				numericUpDown.Maximum= (decimal)intAtt.Max;
				numericUpDown.Increment= (decimal)intAtt.Steps;

				int val= (int)property.Property.GetValue(obj, null);
				numericUpDown.Value= (decimal)val;

				unitLabel.Text= intAtt.Units;
			}

			DesignerFlexibleFloat flexFloatAtt= property.Attribute as DesignerFlexibleFloat;
			if(flexFloatAtt !=null)
			{
				numericUpDown.DecimalPlaces= flexFloatAtt.Precision;
				numericUpDown.Minimum= (decimal)flexFloatAtt.Min;
				numericUpDown.Maximum= (decimal)flexFloatAtt.Max;
				numericUpDown.Increment= (decimal)flexFloatAtt.Steps;

				FlexiblePropertyFloat flexProp= (FlexiblePropertyFloat)property.Property.GetValue(obj, null);
				numericUpDown.Value= (decimal)flexProp.Min;

				unitLabel.Text= flexFloatAtt.Units;
			}

			DesignerFlexibleInteger flexIntegerAtt= property.Attribute as DesignerFlexibleInteger;
			if(flexIntegerAtt !=null)
			{
				numericUpDown.DecimalPlaces= 0;
				numericUpDown.Minimum= (decimal)flexIntegerAtt.Min;
				numericUpDown.Maximum= (decimal)flexIntegerAtt.Max;
				numericUpDown.Increment= (decimal)flexIntegerAtt.Steps;

				FlexiblePropertyInteger flexProp= (FlexiblePropertyInteger)property.Property.GetValue(obj, null);
				numericUpDown.Value= (decimal)flexProp.Min;

				unitLabel.Text= flexIntegerAtt.Units;
			}
		}

		public override void ReadOnly()
		{
			base.ReadOnly();

			numericUpDown.Enabled= false;
		}

		private void numericUpDown_ValueChanged(object sender, EventArgs e)
		{
			if(!_valueWasAssigned)
				return;

			if(_property.Attribute is DesignerFloat)
				_property.Property.SetValue(_object, (float)numericUpDown.Value, null);

			if(_property.Attribute is DesignerInteger)
				_property.Property.SetValue(_object, (int)numericUpDown.Value, null);

			if(_property.Attribute is DesignerFlexibleFloat)
			{
				FlexiblePropertyFloat flexProp= (FlexiblePropertyFloat)_property.Property.GetValue(_object, null);
				flexProp.Min= (float)numericUpDown.Value;
				flexProp.Max= (float)numericUpDown.Value;
			}

			if(_property.Attribute is DesignerFlexibleInteger)
			{
				FlexiblePropertyInteger flexProp= (FlexiblePropertyInteger)_property.Property.GetValue(_object, null);
				flexProp.Min= (int)numericUpDown.Value;
				flexProp.Max= (int)numericUpDown.Value;
			}

			OnValueChanged();
		}
	}
}
