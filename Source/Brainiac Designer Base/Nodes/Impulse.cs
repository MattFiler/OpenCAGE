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
using Brainiac.Design.Attributes;
using Brainiac.Design.FlexibleProperties;

namespace Brainiac.Design.Nodes
{
	/// <summary>
	/// This node represents an impulse which can be attached to the behaviour tree.
	/// </summary>
	public class Impulse : StyledNode
	{
		private static Brush _theBackgroundBrush= new SolidBrush( Color.FromArgb(149,55,53) );
		private static Brush _theDraggedBackgroundBrush= new SolidBrush( Color.FromArgb(123,45,44) );

		protected Connector _genericChildren;
		public Connector GenericChildren
		{
			get { return _genericChildren; }
		}

		protected FlexiblePropertyFloat _duration= new FlexiblePropertyFloat();

		[DesignerFlexibleFloat("ImpulseDuration", "ImpulseDurationDesc", "CategoryBasic", DesignerProperty.DisplayMode.List, 0, DesignerProperty.DesignerFlags.NoFlags, -1.0f, 60.0f, 1.0f, 1, "UnitsSeconds")]
		public FlexiblePropertyFloat Duration
		{
			get { return _duration; }
			set { _duration= value; }
		}

		protected FlexiblePropertyFloat _delay= new FlexiblePropertyFloat();

		[DesignerFlexibleFloat("ImpulseDelay", "ImpulseDelayDesc", "CategoryBasic", DesignerProperty.DisplayMode.List, 0, DesignerProperty.DesignerFlags.NoFlags, -1.0f, 60.0f, 1.0f, 1, "UnitsSeconds")]
		public FlexiblePropertyFloat Delay
		{
			get { return _delay; }
			set { _delay= value; }
		}

		public Impulse(string label, string description) : base(null, _theBackgroundBrush, _theDraggedBackgroundBrush, label, true, description)
		{
			_genericChildren= new ConnectorSingle(_children, string.Empty, "GenericChildren");
		}

		/// <summary>
		/// Creates a new instance of an impulse for a sub-reference graph.
		/// </summary>
		/// <param name="impulse">The original non-sub-reference graph impulse node.</param>
		public Impulse(Impulse impulse) : base(null, _theBackgroundBrush, _theDraggedBackgroundBrush, impulse.BaseLabel, false, impulse.Description)
		{
			_genericChildren= new ConnectorMultiple(_children, string.Empty, "GenericChildren", 1, int.MaxValue);

			impulse.CloneProperties(this);

			OnPropertyValueChanged(false);

			CopyEventHandlers(impulse);

#if DEBUG
			_debugIsSubreferencedGraphNode= true;
#endif
		}

		protected static Brush _defaultBrushSub= new SolidBrush( Color.FromArgb(200, 104, 102) );

		public override void Draw(Graphics graphics, NodeViewData nvd, bool isCurrent, bool isSelected, bool isDragged, PointF graphMousePos)
		{
			Brush defBrush = _defaultStyle.Background;
			if(_genericChildren.IsReadOnly)
				_defaultStyle.Background= _defaultBrushSub;

			base.Draw(graphics, nvd, isCurrent, isSelected, isDragged, graphMousePos);

			_defaultStyle.Background = defBrush;
		}

		protected override void CloneProperties(Node newnode)
		{
			base.CloneProperties(newnode);

			Impulse imp= (Impulse)newnode;
			imp._duration= (FlexiblePropertyFloat)_duration.Clone();
			imp._delay= (FlexiblePropertyFloat)_delay.Clone();
		}
	}
}
