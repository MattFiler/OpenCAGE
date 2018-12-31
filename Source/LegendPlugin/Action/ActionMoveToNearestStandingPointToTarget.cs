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
    public class ActionMoveToNearestStandingPointToTarget : Action
	{
        //All parameters added

        protected RequestShutDownSpeed _type;
        protected MovementSpeedType _MovementSpeedType;
        private string _Distance = "";
        private bool _PlayStoppingAnim = false;

        [DesignerEnum("Movement speed type", "MovementSpeedType", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags, null)]
        public MovementSpeedType MovementSpeedType
        {
            get { return _MovementSpeedType; }
            set { _MovementSpeedType = value; }
        }

        [DesignerBoolean("Play stopping anim", "PlayStoppingAnim", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags)]
        public bool PlayStoppingAnim
        {
            get { return _PlayStoppingAnim; }
            set { _PlayStoppingAnim = value; }
        }

        [DesignerEnum("Request shutdown speed", "RequestShutDownSpeed", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags, null)]
        public RequestShutDownSpeed RequestShutDownSpeed
        {
            get { return _type; }
            set { _type = value; }
        }

        [DesignerString("Stopping distance", "StoppingDistance", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags)]
        public string StoppingDistance
        {
            get { return _Distance; }
            set { _Distance = value; }
        }

        public ActionMoveToNearestStandingPointToTarget() : base("Move to nearest standing point to target ", "Perform a movement of specified speed to the nearest standing point to target.")
        {
        }

        protected override void CloneProperties(Node newnode)
        {
            base.CloneProperties(newnode);

            ActionMoveToNearestStandingPointToTarget cond = (ActionMoveToNearestStandingPointToTarget)newnode;
            cond._PlayStoppingAnim = _PlayStoppingAnim;
            cond._Distance = _Distance;
            cond._MovementSpeedType = _MovementSpeedType;
            cond._type = _type;
        }
    }
}
