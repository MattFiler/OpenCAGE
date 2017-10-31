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
using Brainiac.Design.Properties;

namespace Brainiac.Design.FlexibleProperties
{
	[AttributeUsage(AttributeTargets.Property)]
	public class DesignerFlexibleEnum : DesignerFlexible
	{
		protected object[] _excludedElements;

		/// <summary>
		/// The values of the enum which will not be shown in the designer.
		/// </summary>
		public object[] ExcludedElements
		{
			get { return _excludedElements; }
		}

		protected Type _enumType;

		/// <summary>
		/// The type of the enum we want to use here.
		/// </summary>
		public Type EnumType
		{
			get { return _enumType; }
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
		/// <param name="exclude">The enum values which will be excluded from the values shown in the designer.</param>
		/// <param name="enumType">The type of the enum we want to use here.</param>
		public DesignerFlexibleEnum(string displayName, string description, string category, DisplayMode displayMode, int displayOrder, DesignerFlags flags, object[] exclude, Type enumType) : base(displayName, description, category, displayMode, displayOrder, flags)
		{
			_excludedElements= exclude;
			_enumType= enumType;
		}

		public override object FromStringValue(Type type, string str)
		{
			if(type !=typeof(FlexiblePropertyEnum))
				throw new Exception(Resources.ExceptionDesignerAttributeInvalidType);

			FlexibleProperty flexible= new FlexiblePropertyEnum(_enumType);
			flexible.FromStringValue(str);

			return flexible;
		}
	}
}
