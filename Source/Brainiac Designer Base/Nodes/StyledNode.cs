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

namespace Brainiac.Design.Nodes
{
	/// <summary>
	/// This node determines the overall look of the nodes in the graph. It is for convenience only.
	/// </summary>
	public abstract class StyledNode : Node
	{
		private static Pen _theCurrentBorderPen= new Pen(Brushes.Black, 2.0f);
		private static Pen _theSelectedBorderPen= new Pen(Brushes.Black, 2.0f);
		private static Font _theLabelFont= new Font("Calibri,Arial", 8.0f, FontStyle.Regular);

		public StyledNode(Pen borderPen, Brush backgroundBrush, Brush draggedBackgroundBrush, string label, bool acceptsEvents, string description) :
			base(NodeShape.RoundedRectangle,
				new Style(backgroundBrush, null, Brushes.White),
				new Style(null, _theCurrentBorderPen, null),
				new Style(null, _theSelectedBorderPen, null),
				new Style(draggedBackgroundBrush, null, null),
				label, _theLabelFont, 120, 35,
				acceptsEvents, description)
		{
		}
	}
}
