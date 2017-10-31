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
	public partial class Node
	{
		/// <summary>
		/// A subitem used to draw text on the node.
		/// </summary>
		public abstract class SubItemText : SubItem
		{
			public enum Alignment { Left, Center, Right };

			/// <summary>
			/// The background color when the subitem is not selected.
			/// </summary>
			protected Brush _backgroundNormal;

			/// <summary>
			/// The background color when the subitem is selected.
			/// </summary>
			protected Brush _backgroundSelected;

			/// <summary>
			/// The font used to draw the text.
			/// </summary>
			protected Font _labelFont;

			/// <summary>
			/// The brush used to draw the text.
			/// </summary>
			protected Brush _labelBrush;

			/// <summary>
			/// The width of the font being drawn.
			/// </summary>
			protected float _width;

			/// <summary>
			/// The alignment of the text.
			/// </summary>
			protected Alignment _alignment;

			protected abstract string Label { get; }

			public override float Width
			{
				get { return _width; }
			}

			public override void Update(Node node, Graphics graphics)
			{
				// calculate the extent used by the label
				_width= MeasureDisplayStringWidth(graphics, Label, _labelFont).Width;
			}

			public override void Draw(Graphics graphics, NodeViewData nvd, RectangleF boundingBox)
			{
				// render background
				DrawBackground(graphics, nvd, _selected ? _backgroundSelected : _backgroundNormal);

				// render the label
				PointF center= new PointF(boundingBox.Left + boundingBox.Width *0.5f, boundingBox.Top + boundingBox.Height *0.5f);

				SizeF labelSize= MeasureDisplayStringWidth(graphics, Label, _labelFont);

				// draw text
				switch(_alignment)
				{
					case(Alignment.Left):
						graphics.DrawString(Label, _labelFont, _labelBrush, boundingBox.Left + 6.0f, center.Y - labelSize.Height *0.5f);
					break;

					case(Alignment.Center):
						graphics.DrawString(Label, _labelFont, _labelBrush, center.X - labelSize.Width *0.5f, center.Y - labelSize.Height *0.5f);
					break;

					case(Alignment.Right):
						graphics.DrawString(Label, _labelFont, _labelBrush, boundingBox.Right - labelSize.Width -6.0f, center.Y - labelSize.Height *0.5f);
					break;
				}

				//graphics.DrawRectangle(Pens.Red, boundingBox.X, boundingBox.Y, boundingBox.Width, boundingBox.Height);
			}

			/// <summary>
			/// Creates a new subitem which can draw text on a node.
			/// </summary>
			/// <param name="backgroundNormal">The background brush used when the subitem is not selected.</param>
			/// <param name="backgroundSelected">The background brush used when the subitem is selected. If it cannot be selected, use null.</param>
			/// <param name="labelFont">The font used to draw the label.</param>
			/// <param name="labelBrush">The brush used to draw the label.</param>
			/// <param name="alignment">The alignment of the label.</param>
			/// <param name="showParallelToLabel">Holds if the subitem will be drawn next to the node's label or below it.</param>
			protected SubItemText(Brush backgroundNormal, Brush backgroundSelected, Font labelFont, Brush labelBrush, Alignment alignment, bool showParallelToLabel) : base(showParallelToLabel)
			{
				_backgroundNormal= backgroundNormal;
				_backgroundSelected= backgroundSelected;
				_labelFont= labelFont;
				_labelBrush= labelBrush;
				_alignment= alignment;
			}

			public override string ToString()
			{
				return Label;
			}
		}
	}
}
