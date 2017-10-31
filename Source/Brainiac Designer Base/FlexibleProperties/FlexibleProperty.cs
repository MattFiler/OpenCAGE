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
using Brainiac.Design.Attributes;
using Brainiac.Design.Properties;

namespace Brainiac.Design.FlexibleProperties
{
	public abstract class FlexibleProperty
	{
		public abstract string GetDisplayValue();
		public abstract string GetExportValue();

		public void FromStringValue(string str)
		{
			string[] splitted= str.Split(';');
			if(splitted.Length <1)
				throw new Exception( string.Format(Resources.ExceptionFlexiblePropertyInvalidEditor, "NONE") );

			_selectedEditor= null;
			DesignerProperty.EditorType[] editors= GetEditorTypes();
			for(int i= 0; i <editors.Length; ++i)
			{
				if(editors[i].Editor.Name ==splitted[0])
				{
					_selectedEditor= editors[i].Editor;
					break;
				}
			}

			//if(_selectedEditor ==null)
			//	throw new Exception( string.Format(Resources.ExceptionFlexiblePropertyInvalidEditor, splitted[0]) );

			string[] param= null;

			// keep compatibility with non-flexible properties
			if(_selectedEditor ==null)
			{
				_selectedEditor= editors[0].Editor;
				param= splitted;
			}
			else
			{
				if(splitted.Length >1)
				{
					param= new string[splitted.Length -1];
					for(int i= 1; i <splitted.Length; ++i)
						param[i -1]= splitted[i];
				}
			}

			FromStringValue(_selectedEditor, param);
		}

		protected abstract void FromStringValue(Type editor, string[] str);
		public abstract DesignerProperty.EditorType[] GetEditorTypes();

		protected Type _selectedEditor= null;
		public Type GetSelectedEditorType()
		{
			if(_selectedEditor ==null)
			{
				DesignerProperty.EditorType[] editorTypes= GetEditorTypes();
				_selectedEditor= editorTypes[0].Editor;
			}

			return _selectedEditor;
		}

		public virtual void ChangeEditor(Type newEditor)
		{
			_selectedEditor= newEditor;
		}

		public string GetStringValue()
		{
			List<string> values= new List<string>();
			GetStringValues(ref values);

			string val= _selectedEditor.Name;
			for(int i= 0; i <values.Count; ++i)
				val+= ';'+ values[i];

			return val;
		}

		protected abstract void GetStringValues(ref List<string> values);

		public object Clone()
		{
			FlexibleProperty flexProp= (FlexibleProperty)GetType().InvokeMember(string.Empty, System.Reflection.BindingFlags.CreateInstance, null, null, new object[0]);

			CloneProperties(flexProp);

			return flexProp;
		}

		protected virtual void CloneProperties(FlexibleProperty flexProp)
		{
			flexProp._selectedEditor= _selectedEditor;
		}
	}
}
