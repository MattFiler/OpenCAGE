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
    public class ActionRangedAim : Action
	{
        //All parameters added

        private string _cond1 = "";
        protected RequestShutDownSpeed _cond2;
        protected ShouldRaiseGun _cond3;
        private string _cond4 = "";

        [DesignerString("NoiseTime", "NoiseTime", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags)]
        public string NoiseTime
        {
            get { return _cond1; }
            set { _cond1 = value; }
        }

        [DesignerEnum("RequestShutDownSpeed", "RequestShutDownSpeed", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags, null)]
        public RequestShutDownSpeed RequestShutDownSpeed
        {
            get { return _cond2; }
            set { _cond2 = value; }
        }

        [DesignerEnum("ShouldRaiseGun", "ShouldRaiseGun", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags, null)]
        public ShouldRaiseGun ShouldRaiseGun
        {
            get { return _cond3; }
            set { _cond3 = value; }
        }

        [DesignerString("Time", "Time", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags)]
        public string Time
        {
            get { return _cond4; }
            set { _cond4 = value; }
        }

        public ActionRangedAim() : base(Resources.ActionRangedAim, Resources.ActionRangedAim)
        {
        }

        protected override void CloneProperties(Node newnode)
        {
            base.CloneProperties(newnode);

            ActionRangedAim cond = (ActionRangedAim)newnode;
            cond._cond1 = _cond1;
            cond._cond2 = _cond2;
            cond._cond3 = _cond3;
            cond._cond4 = _cond4;
        }
    }
}
