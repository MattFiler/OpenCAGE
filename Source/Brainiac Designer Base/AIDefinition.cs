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
using Brainiac.Design.Properties;

namespace Brainiac.Design
{
	public interface CurrentAITypeProvider
	{
		AIType CurrentAIType { get; }
	}

	/// <summary>
	/// This class is used to add AI types to the editor.
	/// </summary>
	public class AIType
	{
		private static CurrentAITypeProvider __currentAITypeProvider;

		/// <summary>
		/// Sets the current AI type provider.
		/// </summary>
		/// <param name="provider">The object which can provide the current AI type.</param>
		public static void SetProvider(CurrentAITypeProvider provider)
		{
			Debug.Check(__currentAITypeProvider ==null);

			__currentAITypeProvider= provider;
		}

		/// <summary>
		/// Returns the current AI type.
		/// </summary>
		public static AIType Current
		{
			get { return __currentAITypeProvider ==null ? null : __currentAITypeProvider.CurrentAIType; }
		}

		protected string _name;

		/// <summary>
		/// The name shown in the node explorer.
		/// </summary>
		public string Name
		{
			get { return Plugin.GetResourceString(_name); }
		}

		protected List<Definition> _definitions= new List<Definition>();

		/// <summary>
		/// The definitions associated.
		/// </summary>
		public IList<Definition> Definitions
		{
			get { return _definitions.AsReadOnly(); }
		}

		public AIType(string name)
		{
			_name= name;
		}

		public void AddDefinition(Definition definition)
		{
			_definitions.Add(definition);
		}

		public Definition FindDefinition(string name)
		{
			foreach(Definition definition in _definitions)
			{
				if(definition.Name ==name)
					return definition;
			}

			return null;
		}

		public override string ToString()
		{
			return Name;
		}
	}

	/// <summary>
	/// This class represents a single definition for example of an agent, a squad or a faction.
	/// </summary>
	public class Definition
	{
		protected string _name;

		/// <summary>
		/// The name shown when this definition is referenced.
		/// </summary>
		public string Name
		{
			get { return _name; }
		}

		protected object _object;

		/// <summary>
		/// The object which contains all members available in the definition.
		/// </summary>
		public object Object
		{
			get { return _object; }
		}

		public Definition(string name, object obj)
		{
			if(name.Contains(" "))
				throw new Exception(Resources.ExceptionInvalidDefinitionName);

			_name= name;
			_object= obj;
		}
	}
}
