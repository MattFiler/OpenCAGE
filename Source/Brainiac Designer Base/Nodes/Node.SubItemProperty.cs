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
using System.Reflection;
using System.Drawing;
using Brainiac.Design.Attributes;

namespace Brainiac.Design.Nodes
{
	public partial class Node
	{
		/// <summary>
		/// A subitem used to draw a property of the node on it.
		/// </summary>
		public class SubItemProperty : SubItemText
		{
			protected Node _owner;
			protected PropertyInfo _property;
			protected DesignerProperty _attribute;

			private static Font __font= new Font("Calibri,Arial", 6.0f, FontStyle.Regular);

			/// <summary>
			/// Creates a new subitem which can show a property on the node.
			/// </summary>
			/// <param name="owner">The node whose property we want to show. MUST be the same as the one the subitem belongs to.</param>
			/// <param name="property">The property we want to show.</param>
			/// <param name="att">The attribute associated with the property.</param>
			public SubItemProperty(Node owner, PropertyInfo property, DesignerProperty att) : base(null, null, __font, Brushes.White, Alignment.Center, false)
			{
				_owner= owner;
				_property= property;
				_attribute= att;
			}

			protected override string Label
			{
				get
				{
					// get the value from the property
					object obj= _property.GetValue(_owner, null);
					return _attribute.DisplayName +" = "+ _attribute.GetDisplayValue(obj);
				}
			}

			public override SubItem Clone(Node newnode)
			{
				// this subitem is automatically added it does not need to be duplicated
				return null;
			}
		}
	}
}
