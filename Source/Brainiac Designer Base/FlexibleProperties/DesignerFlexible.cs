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
using Brainiac.Design.Attributes;

namespace Brainiac.Design.FlexibleProperties
{
	[AttributeUsage(AttributeTargets.Property)]
	public abstract class DesignerFlexible : DesignerProperty
	{
		/// <summary>
		/// Creates a new designer attribute for handling a string value.
		/// </summary>
		/// <param name="displayName">The name shown on the node and in the property editor for the property.</param>
		/// <param name="description">The description shown in the property editor for the property.</param>
		/// <param name="category">The category shown in the property editor for the property.</param>
		/// <param name="displayMode">Defines how the property is visualised in the editor.</param>
		/// <param name="displayOrder">Defines the order the properties will be sorted in when shown in the property grid. Lower come first.</param>
		/// <param name="flags">Defines the designer flags stored for the property.</param>
		protected DesignerFlexible(string displayName, string description, string category, DisplayMode displayMode, int displayOrder, DesignerFlags flags) : base(displayName, description, category, displayMode, displayOrder, flags, null)
		{
		}

		public override string GetDisplayValue(object obj)
		{
			FlexibleProperty flexible= (FlexibleProperty)obj;
			return flexible.GetDisplayValue();
		}

		public override string GetExportValue(object obj)
		{
			FlexibleProperty flexible= (FlexibleProperty)obj;
			return flexible.GetExportValue();
		}

		public override object FromStringValue(Type type, string str)
		{
			if(!type.IsSubclassOf(typeof(FlexibleProperty)))
				throw new Exception(Resources.ExceptionDesignerAttributeInvalidType);

			FlexibleProperty flexible= (FlexibleProperty)type.InvokeMember(string.Empty, System.Reflection.BindingFlags.CreateInstance, null, null, new object[0]);
			flexible.FromStringValue(str);

			return flexible;
		}

		public override EditorType[] GetEditorTypes(object obj)
		{
			FlexibleProperty flexible= (FlexibleProperty)obj;
			return flexible.GetEditorTypes();
		}

		public override Type GetSelectedEditorType(object obj)
		{
			FlexibleProperty flexible= (FlexibleProperty)obj;
			return flexible.GetSelectedEditorType();
		}

		public override string GetStringValue(object obj)
		{
			FlexibleProperty flexible= (FlexibleProperty)obj;
			return flexible.GetStringValue();
		}
	}
}
