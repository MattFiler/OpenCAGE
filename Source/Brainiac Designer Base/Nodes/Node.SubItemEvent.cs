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
		/// A subitem used to visualise events on a node.
		/// </summary>
		public class SubItemEvent : SubItemText
		{
			protected Events.Event _event;

			/// <summary>
			/// The event stored in this subitem.
			/// </summary>
			public Events.Event Event
			{
				get { return _event; }
			}

			public override object SelectableObject
			{
				// when the subitem gets selected, show the stored event in the properties.
				get { return _event; }
			}

			public override bool CanBeDeleted
			{
				get { return true; }
			}

			protected override string Label
			{
				get { return _event.Label; }
			}

			private static Brush _theDefaultEventBrush= new SolidBrush(Color.FromArgb(50, Color.Black));
			private static Brush _theSelectedEventBrush= new SolidBrush(Color.FromArgb(100, Color.Black));
			private static Font _theEventLabelFont= new Font("Calibri,Arial", 6.0f, FontStyle.Regular);

			/// <summary>
			/// Creates a new subitm which can render an event on the node.
			/// </summary>
			/// <param name="evnt">The event we want to draw.</param>
			public SubItemEvent(Events.Event evnt) : base(_theDefaultEventBrush, _theSelectedEventBrush, _theEventLabelFont, null, Alignment.Center, false)
			{
				_event= evnt;
			}

			public override void Draw(Graphics graphics, NodeViewData nvd, RectangleF boundingBox)
			{
				// use a different brush depending on if the event is reacted to or blocked.
				_labelBrush= _event.BlockEvent ? Brushes.Orange : Brushes.White;

				base.Draw(graphics, nvd, boundingBox);
			}

			public override SubItem Clone(Node newnode)
			{
				return new SubItemEvent( _event.Clone(newnode) );
			}
		}
	}
}
