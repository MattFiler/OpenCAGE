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
using System.Windows.Forms.Design;
using System.Reflection;
using Brainiac.Design.Properties;
using Brainiac.Design.Attributes;
using Brainiac.Design.FlexibleProperties;

namespace Brainiac.Design
{
	internal partial class PropertiesDock : WeifenLuo.WinFormsUI.Docking.DockContent
	{
		private static List<PropertiesDock> __propertyGrids= new List<PropertiesDock>();

		/// <summary>
		/// The number of property grids available.
		/// </summary>
		public static int Count
		{
			get { return __propertyGrids.Count; }
		}

		/// <summary>
		/// Forces all property grids to reinspect their objects.
		/// </summary>
		public static void UpdatePropertyGrids()
		{
			foreach(PropertiesDock dock in __propertyGrids)
				dock.SelectedObject= dock.SelectedObject;
		}

		private object _selectedObject= null;

		internal object SelectedObject
		{
			set
			{
				propertyGrid.ClearProperties();

				_selectedObject= value;

				string text= _selectedObject ==null ? "Properties" : "Properties of "+ _selectedObject.ToString();
				Text= text;
				TabText= text;

				// this is a hack to work around a bug in the docking suite
				text+= ' ';
				Text= text;
				TabText= text;

				if(_selectedObject ==null)
				{
					propertyGrid.PropertiesVisible(false);
				}
				else
				{
					propertyGrid.PropertiesVisible(false);

					IList<DesignerPropertyInfo> properties= DesignerProperty.GetDesignerProperties(_selectedObject.GetType(), DesignerProperty.SortByDisplayOrder);

					List<string> categories= new List<string>();
					foreach(DesignerPropertyInfo property in properties)
					{
						if(!categories.Contains(property.Attribute.CategoryResourceString))
							categories.Add(property.Attribute.CategoryResourceString);
					}

					categories.Sort();

					foreach(string category in categories)
					{
						propertyGrid.AddCategory(Plugin.GetResourceString(category), true);

						foreach(DesignerPropertyInfo property in properties)
						{
							if(property.Attribute.CategoryResourceString ==category)
							{
								DesignerProperty.EditorType[] editorTypes= property.Attribute.GetEditorTypes(property.Property.GetValue(_selectedObject, null));
								Type type= property.Attribute.GetSelectedEditorType(property.Property.GetValue(_selectedObject, null));
								Label label= propertyGrid.AddProperty(property.Attribute.DisplayName, type, property.Attribute.HasFlags(DesignerProperty.DesignerFlags.ReadOnly));

								// register description showing
								label.MouseEnter+= new EventHandler(label_MouseEnter);

								// mark flexible properties
								if(property.Attribute is DesignerFlexible)
									label.BackColor= Color.FromArgb(209, 218, 228);

								// register context menu
								if(editorTypes.Length >1)
								{
									System.Windows.Forms.ContextMenu contextMenu= new ContextMenu();
									contextMenu.Tag= label;

									// add items to context menu
									for(int i= 0; i <editorTypes.Length; ++i)
									{
										MenuItem item= contextMenu.MenuItems.Add(editorTypes[i].Name);
										item.Tag= editorTypes[i].Editor;
										item.Checked= editorTypes[i].Editor ==type;
										item.Click+= new EventHandler(item_Click);
									}

									// assign context menu
									label.ContextMenu= contextMenu;
								}

								// when we found an editor we connect it to the object
								if(type !=null)
								{
									DesignerPropertyEditor editor= (DesignerPropertyEditor)label.Tag;
									editor.SetProperty(property, _selectedObject);
									editor.ValueWasAssigned();
									editor.MouseEnter += new EventHandler(editor_MouseEnter);
									editor.ValueWasChanged += new DesignerPropertyEditor.ValueChanged(editor_ValueWasChanged);
								}
							}
						}
					}

					propertyGrid.UpdateSizes();
					propertyGrid.PropertiesVisible(true);
				}
			}

			get { return _selectedObject; }
		}

		void editor_ValueWasChanged()
		{
				string text= _selectedObject ==null ? "Properties" : "Properties of "+ _selectedObject.ToString();
				Text= text;
				TabText= text;
		}

		void label_MouseEnter(object sender, EventArgs e)
		{
			Label label= (Label)sender;
			DesignerPropertyEditor editor= (DesignerPropertyEditor)label.Tag;

			propertyGrid.ShowDescription(editor.Property.Attribute.DisplayName, editor.Property.Attribute.Description);
		}

		void editor_MouseEnter(object sender, EventArgs e)
		{
			DesignerPropertyEditor editor= (DesignerPropertyEditor)sender;

			propertyGrid.ShowDescription(editor.Property.Attribute.DisplayName, editor.Property.Attribute.Description);
		}

		void item_Click(object sender, EventArgs e)
		{
			MenuItem item= (MenuItem)sender;

			Type editorType= (Type)item.Tag;
			Label label= (Label)item.Parent.Tag;
			DesignerPropertyEditor editor= (DesignerPropertyEditor)label.Tag;

			Debug.Check(_selectedObject ==editor.SelectedObject);

			FlexibleProperty flexProp= (FlexibleProperty)editor.Property.Property.GetValue(_selectedObject, null);
			flexProp.ChangeEditor(editorType);

			Nodes.Node node= _selectedObject as Nodes.Node;
			if(node !=null)
				node.OnPropertyValueChanged(true);

			Events.Event evnt= _selectedObject as Events.Event;
			if(evnt !=null)
				evnt.OnPropertyValueChanged(true);

			SelectedObject= _selectedObject;
		}

		internal static bool InspectObject(object obj)
		{
			if(__propertyGrids.Count <1)
				return false;

			__propertyGrids[0].SelectedObject= obj;
			return true;
		}

		public PropertiesDock()
		{
			InitializeComponent();

			__propertyGrids.Add(this);

			propertyGrid.PropertiesVisible(false);
		}

		protected override void OnClosed(EventArgs e)
		{
			SelectedObject= null;
			__propertyGrids.Remove(this);

			base.OnClosed(e);
		}
	}
}
