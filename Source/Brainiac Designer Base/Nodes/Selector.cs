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
using System.Drawing;
using Brainiac.Design.Properties;

namespace Brainiac.Design.Nodes
{
	/// <summary>
	/// This node represents a selector which can be attached to the behaviour tree.
	/// </summary>
	public class Selector : StyledNode
	{
		private static Brush _theBackgroundBrush= new SolidBrush( Color.FromArgb(79,129,189) );
		private static Brush _theDraggedBackgroundBrush= new SolidBrush( Color.FromArgb(68,111,163) );

		protected ConnectorMultiple _genericChildren;

		public Selector(string label, string description) : base(null, _theBackgroundBrush, _theDraggedBackgroundBrush, label, true, description)
		{
			_genericChildren= new ConnectorMultiple(_children, "Try {0}", "GenericChildren", 2, int.MaxValue);
		}

		public override void CheckForErrors(BehaviorNode rootBehavior, List<ErrorCheck> result)
		{
			if(_genericChildren.ChildCount <2)
				result.Add( new Node.ErrorCheck(this, ErrorCheckLevel.Warning, Resources.SelectorOnlyOneChildError) );
			else if(_genericChildren.ChildCount <1)
				result.Add( new Node.ErrorCheck(this, ErrorCheckLevel.Error, Resources.SelectorNoChildrenError) );

			base.CheckForErrors(rootBehavior, result);
		}
	}
}
