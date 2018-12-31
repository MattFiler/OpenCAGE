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

//using System;
using System.Collections.Generic;
using System.Text;
using Brainiac.Design.Nodes;
using Brainiac.Design.Attributes;
using LegendPlugin.Properties;

namespace LegendPlugin.Nodes
{
    public class ActionPlayTreeAndFaceTarget : Action
	{
        //All parameters added
        
        protected AnimCallbackEnum _cond1;
        protected AnimTreeEnum _cond2;
        protected RequestShutDownSpeed _cond3;

        [DesignerEnum("Anim callback enum", "AnimCallbackEnum", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags, null)]
        public AnimCallbackEnum AnimCallbackEnum
        {
            get { return _cond1; }
            set { _cond1 = value; }
        }

        [DesignerEnum("Anim tree enum", "AnimTreeEnum", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags, null)]
        public AnimTreeEnum AnimTreeEnum
        {
            get { return _cond2; }
            set { _cond2 = value; }
        }

        [DesignerEnum("Request shutdown speed", "RequestShutDownSpeed", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags, null)]
        public RequestShutDownSpeed RequestShutDownSpeed
        {
            get { return _cond3; }
            set { _cond3 = value; }
        }

        public ActionPlayTreeAndFaceTarget() : base("PLAY ANIMATION TREE AND FACE TARGET ", "PLAY A SPECIFIED ANIMATION TREE WHILE FACING OUR TARGET.")
 
        {
        }

        protected override void CloneProperties(Node newnode)
        {
            base.CloneProperties(newnode);

            ActionPlayTreeAndFaceTarget cond = (ActionPlayTreeAndFaceTarget)newnode;
            cond._cond1 = _cond1;
            cond._cond2 = _cond2;
            cond._cond3 = _cond3;
        }
    }
}
