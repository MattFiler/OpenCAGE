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
    public class ActionIdleTimeFacingTargetOutsideCombatArea : Action
	{
        //All parameters added

        private string _FacingTolerance = "";
        private string _NoiseTime = "";
        protected RequestShutDownSpeed _type;
        protected ThresholdQualifier _ThresholdQualifier;
        private string _Time = "";

        [DesignerString("FacingTolerance", "FacingTolerance", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags)]
        public string FacingTolerance
        {
            get { return _FacingTolerance; }
            set { _FacingTolerance = value; }
        }

        [DesignerString("NoiseTime", "NoiseTime", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags)]
        public string NoiseTime
        {
            get { return _NoiseTime; }
            set { _NoiseTime = value; }
        }

        [DesignerEnum("RequestShutDownSpeed", "RequestShutDownSpeed", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags, null)]
        public RequestShutDownSpeed RequestShutDownSpeed
        {
            get { return _type; }
            set { _type = value; }
        }

        [DesignerEnum("ThresholdQualifier", "ThresholdQualifier", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags, null)]
        public ThresholdQualifier ThresholdQualifier
        {
            get { return _ThresholdQualifier; }
            set { _ThresholdQualifier = value; }
        }

        [DesignerString("Time", "Time", "CategoryBasic", DesignerProperty.DisplayMode.Parameter, 0, DesignerProperty.DesignerFlags.NoFlags)]
        public string Time
        {
            get { return _Time; }
            set { _Time = value; }
        }

        public ActionIdleTimeFacingTargetOutsideCombatArea() : base(Resources.ActionIdleTimeFacingTargetOutsideCombatArea, Resources.ActionIdleTimeFacingTargetOutsideCombatArea)
        {
        }

        protected override void CloneProperties(Node newnode)
        {
            base.CloneProperties(newnode);

            ActionIdleTimeFacingTargetOutsideCombatArea cond = (ActionIdleTimeFacingTargetOutsideCombatArea)newnode;
            cond._FacingTolerance = _FacingTolerance;
            cond._NoiseTime = _NoiseTime;
            cond._type = _type;
            cond._ThresholdQualifier = _ThresholdQualifier;
            cond._Time = _Time;
        }
    }
}
