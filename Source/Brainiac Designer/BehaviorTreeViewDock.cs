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

namespace Brainiac.Design
{
	internal partial class BehaviorTreeViewDock : WeifenLuo.WinFormsUI.Docking.DockContent
	{
		private static List<BehaviorTreeViewDock> __instances= new List<BehaviorTreeViewDock>();
		internal static IList<BehaviorTreeViewDock> Instances
		{
			get { return __instances.AsReadOnly(); }
		}

		private static BehaviorTreeViewDock __lastFocusedInstance= null;
		internal static BehaviorTreeViewDock LastFocused
		{
			get { return __lastFocusedInstance; }
		}

		internal static BehaviorTreeViewDock GetBehaviorTreeViewDock(Nodes.BehaviorNode node)
		{
			foreach(BehaviorTreeViewDock dock in __instances)
			{
				if(dock.BehaviorTreeView.RootNode ==node)
					return dock;
			}

			return null;
		}

		internal static BehaviorTreeView GetBehaviorTreeView(Nodes.BehaviorNode node)
		{
			foreach(BehaviorTreeViewDock dock in __instances)
			{
				if(dock.BehaviorTreeView.RootNode ==node)
					return dock.BehaviorTreeView;
			}

			return null;
		}

		private BehaviorTreeView _behaviorTreeView;
		internal BehaviorTreeView BehaviorTreeView
		{
			get { return _behaviorTreeView; }

			set
			{
				if(_behaviorTreeView !=null)
					throw new Exception("BehaviorTreeView already assigned");

				_behaviorTreeView= value;
				Controls.Add(_behaviorTreeView);

				_behaviorTreeView.MouseDown += new MouseEventHandler(BehaviorTreeView_MouseDown);

				_behaviorTreeView.RootNode.WasSaved += new Brainiac.Design.Nodes.Behavior.WasSavedEventDelegate(RootNode_WasSaved);
			}
		}

		void BehaviorTreeView_MouseDown(object sender, MouseEventArgs e)
		{
			__lastFocusedInstance= this;
		}

		void RootNode_WasSaved(Brainiac.Design.Nodes.BehaviorNode node)
		{
			Text= ((Nodes.Node)node).Label;
			TabText= ((Nodes.Node)node).Label;
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);

			__instances.Add(this);
		}

		protected override void OnClosed(EventArgs e)
		{
			if(__lastFocusedInstance ==this)
				__lastFocusedInstance= null;

			__instances.Remove(this);

			_behaviorTreeView.RootNode.WasSaved-= RootNode_WasSaved;

			base.OnClosed(e);
		}

		public BehaviorTreeViewDock()
		{
			InitializeComponent();
		}
	}
}
