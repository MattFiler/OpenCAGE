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
	public class DesignerFloat : DesignerProperty
	{
		protected float _min, _max, _steps;
		protected string _units;
		protected int _precision;

		/// <summary>
		/// The units the value is represented in.
		/// </summary>
		public string Units
		{
			get { return Plugin.GetResourceString(_units); }
		}

		/// <summary>
		/// The minimum value of the property.
		/// </summary>
		public float Min
		{
			get { return _min; }
		}

		/// <summary>
		/// The maximum value of the property.
		/// </summary>
		public float Max
		{
			get { return _max; }
		}

		/// <summary>
		/// The minimum value added or substracted when changing the property's value.
		/// </summary>
		public float Steps
		{
			get { return _steps; }
		}

		/// <summary>
		/// The number of decimal places shown.
		/// </summary>
		public int Precision
		{
			get { return _precision; }
		}

		/// <summary>
		/// Creates a new designer attribute for handling a float value.
		/// </summary>
		/// <param name="displayName">The name shown on the node and in the property editor for the property.</param>
		/// <param name="description">The description shown in the property editor for the property.</param>
		/// <param name="category">The category shown in the property editor for the property.</param>
		/// <param name="displayMode">Defines how the property is visualised in the editor.</param>
		/// <param name="displayOrder">Defines the order the properties will be sorted in when shown in the property grid. Lower come first.</param>
		/// <param name="flags">Defines the designer flags stored for the property.</param>
		/// <param name="min">The minimum value of the property.</param>
		/// <param name="max">The maximum value of the property.</param>
		/// <param name="steps">The minimum value added or substracted when changing the property's value.</param>
		/// <param name="precision">The number of decimal places shown.</param>
		/// <param name="units">The units the value is represented in.</param>
		public DesignerFloat(string displayName, string description, string category, DisplayMode displayMode, int displayOrder, DesignerFlags flags, float min, float max, float steps, int precision, string units) : base(displayName, description, category, displayMode, displayOrder, flags, new EditorType[] { new EditorType("EditorValue", typeof(DesignerNumberEditor)) })
		{
			_min= min;
			_max= max;
			_steps= steps;
			_precision= precision;
			_units= units;
		}

		public override string GetDisplayValue(object obj)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}", (float)obj);
		}

		public override string GetExportValue(object obj)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}f", (float)obj);
		}

		public override object FromStringValue(Type type, string str)
		{
			if(type !=typeof(float))
				throw new Exception(Resources.ExceptionDesignerAttributeInvalidType);

			float result;
			if(float.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out result))
				return result;

			throw new Exception( string.Format(Resources.ExceptionDesignerAttributeIllegalFloatValue, str) );
		}
	}
}
