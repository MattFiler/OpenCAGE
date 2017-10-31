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
using Brainiac.Design.Attributes;

namespace Brainiac.Design.FlexibleProperties
{
	public partial class DesignerRandomNumberEditor : Brainiac.Design.Attributes.DesignerPropertyEditor
	{
		public DesignerRandomNumberEditor()
		{
			InitializeComponent();
		}

		public override void SetProperty(DesignerPropertyInfo property, object obj)
		{
			base.SetProperty(property, obj);

			DesignerFlexibleFloat flexFloatAtt= property.Attribute as DesignerFlexibleFloat;
			if(flexFloatAtt !=null)
			{
				minNumericUpDown.DecimalPlaces= flexFloatAtt.Precision;
				minNumericUpDown.Minimum= (decimal)flexFloatAtt.Min;
				minNumericUpDown.Maximum= (decimal)flexFloatAtt.Max;
				minNumericUpDown.Increment= (decimal)flexFloatAtt.Steps;

				maxNumericUpDown.DecimalPlaces= flexFloatAtt.Precision;
				maxNumericUpDown.Minimum= (decimal)flexFloatAtt.Min;
				maxNumericUpDown.Maximum= (decimal)flexFloatAtt.Max;
				maxNumericUpDown.Increment= (decimal)flexFloatAtt.Steps;

				FlexiblePropertyFloat flexProp= (FlexiblePropertyFloat)property.Property.GetValue(obj, null);
				minNumericUpDown.Value= (decimal)flexProp.Min;
				maxNumericUpDown.Value= (decimal)flexProp.Max;

				unitLabel.Text= flexFloatAtt.Units;
			}

			DesignerFlexibleInteger flexIntegerAtt= property.Attribute as DesignerFlexibleInteger;
			if(flexIntegerAtt !=null)
			{
				minNumericUpDown.DecimalPlaces= 0;
				minNumericUpDown.Minimum= (decimal)flexIntegerAtt.Min;
				minNumericUpDown.Maximum= (decimal)flexIntegerAtt.Max;
				minNumericUpDown.Increment= (decimal)flexIntegerAtt.Steps;

				maxNumericUpDown.DecimalPlaces= 0;
				maxNumericUpDown.Minimum= (decimal)flexIntegerAtt.Min;
				maxNumericUpDown.Maximum= (decimal)flexIntegerAtt.Max;
				maxNumericUpDown.Increment= (decimal)flexIntegerAtt.Steps;

				FlexiblePropertyInteger flexProp= (FlexiblePropertyInteger)property.Property.GetValue(obj, null);
				minNumericUpDown.Value= (decimal)flexProp.Min;
				maxNumericUpDown.Value= (decimal)flexProp.Max;

				unitLabel.Text= flexIntegerAtt.Units;
			}
		}

		public override void ReadOnly()
		{
			base.ReadOnly();

			minNumericUpDown.Enabled= false;
			maxNumericUpDown.Enabled= false;
		}

		private void ValueChanged()
		{
			if(_property.Attribute is DesignerFlexibleFloat)
			{
				FlexiblePropertyFloat flexProp= (FlexiblePropertyFloat)_property.Property.GetValue(_object, null);
				flexProp.Min= (float)minNumericUpDown.Value;
				flexProp.Max= (float)maxNumericUpDown.Value;
			}

			if(_property.Attribute is DesignerFlexibleInteger)
			{
				FlexiblePropertyInteger flexProp= (FlexiblePropertyInteger)_property.Property.GetValue(_object, null);
				flexProp.Min= (int)minNumericUpDown.Value;
				flexProp.Max= (int)maxNumericUpDown.Value;
			}

			OnValueChanged();
		}

		private void minNumericUpDown_ValueChanged(object sender, EventArgs e)
		{
			if(!_valueWasAssigned)
				return;

			if(minNumericUpDown.Value >maxNumericUpDown.Value)
			{
				maxNumericUpDown.Value= minNumericUpDown.Value;
				return;
			}

			ValueChanged();
		}

		private void maxNumericUpDown_ValueChanged(object sender, EventArgs e)
		{
			if(!_valueWasAssigned)
				return;

			if(maxNumericUpDown.Value <minNumericUpDown.Value)
			{
				minNumericUpDown.Value= maxNumericUpDown.Value;
				return;
			}

			ValueChanged();
		}

		protected override void OnResize(EventArgs e)
		{
			if(minNumericUpDown !=null && maxNumericUpDown !=null)
			{
				int extent= Width - 50;
				minNumericUpDown.Width= extent /2;
				maxNumericUpDown.Location= new Point(minNumericUpDown.Location.X + minNumericUpDown.Width +3, maxNumericUpDown.Location.Y);
				maxNumericUpDown.Width= extent /2;
			}

			base.OnResize(e);
		}
	}
}
