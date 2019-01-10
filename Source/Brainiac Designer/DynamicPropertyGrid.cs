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
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using Brainiac.Design.Attributes;

namespace CustomPropertyGridTest
{
	public partial class DynamicPropertyGrid : UserControl
	{
		public DynamicPropertyGrid()
		{
			InitializeComponent();
		}

		private const int _padding= 1;

		public void PropertiesVisible(bool visible)
		{
			propertiesSplitContainer.Visible= visible;
			propertyNameLabel.Visible= visible;
			propertyDescriptionLabel.Visible= visible;
		}

		private int _location= 0;

		public void ClearProperties()
		{
			_location= 0;

			propertiesSplitContainer.Panel1.Controls.Clear();
			propertiesSplitContainer.Panel2.Controls.Clear();
		}

		public void AddCategory(string name, bool expanded)
		{
			Label label= new Label();
			label.AutoSize= false;
			label.Text= name;
			label.Location= new Point(0, _location);
			label.Height= 15;
			label.BackColor= Color.DarkGray;
			label.Font= new Font(label.Font.FontFamily, label.Font.Size, FontStyle.Bold, GraphicsUnit.Point);
			propertiesSplitContainer.Panel1.Controls.Add(label);

			Panel panel= new Panel();
			panel.Location= new Point(0, _location);
			panel.Height= 15;
			panel.BackColor= Color.DarkGray;
			propertiesSplitContainer.Panel2.Controls.Add(panel);

			_location+= 15 + _padding;
			propertiesSplitContainer.Height= _location;
		}

		public Label AddProperty(string name, Type editorType, bool isReadOnly)
		{
			Label label= new Label();
			label.AutoSize= false;
			label.Text= name;
			label.TextAlign= ContentAlignment.MiddleLeft;
			label.Location= new Point(0, _location);
			propertiesSplitContainer.Panel1.Controls.Add(label);

			Control ctrl= null;
			if(editorType ==null)
			{
				ctrl= new Panel();
				ctrl.Height= 20;
				ctrl.BackColor= Color.Red;
			}
			else
			{
				DesignerPropertyEditor editor= (DesignerPropertyEditor)editorType.InvokeMember(string.Empty, BindingFlags.CreateInstance, null, null, new object[0]);

				if(isReadOnly)
					editor.ReadOnly();

				ctrl= editor;
			}

			label.Height= ctrl.Height;
			label.Tag= ctrl;
			ctrl.Location= new Point(0, _location);

			propertiesSplitContainer.Panel2.Controls.Add(ctrl);

			_location+= ctrl.Height + _padding;
			propertiesSplitContainer.Height= _location - _padding;

			return label;
		}

		public void UpdateSizes()
		{
			foreach(Control ctrl in propertiesSplitContainer.Panel1.Controls)
				ctrl.Width= propertiesSplitContainer.Panel1.Width;

			foreach(Control ctrl in propertiesSplitContainer.Panel2.Controls)
				ctrl.Width= propertiesSplitContainer.Panel2.Width;
		}

		private void propertiesSplitContainer_SplitterMoved(object sender, SplitterEventArgs e)
		{
			UpdateSizes();
		}

		private void propertiesSplitContainer_Resize(object sender, EventArgs e)
		{
			UpdateSizes();
		}

		public void ShowDescription(string name, string description)
		{
			propertyNameLabel.Text= name;
			propertyDescriptionLabel.Text= description;
		}
	}
}
