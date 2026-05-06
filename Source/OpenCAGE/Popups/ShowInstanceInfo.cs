using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Numerics;
using CathodeLib;

namespace OpenCAGE
{
    public partial class ShowInstanceInfo : BaseWindow
    {
        private CompositeDisplay _display;

        public ShowInstanceInfo(CompositeDisplay display) : base(WindowClosesOn.NEW_COMPOSITE_SELECTION | WindowClosesOn.COMMANDS_RELOAD)
        {
            _display = display;
            InitializeComponent();

            // Use proper matrix multiplication instead of naive addition
            Matrix4x4 globalMatrix = Matrix4x4.Identity;
            foreach (Entity entity in display.Path.AllEntities)
            {
                Parameter position = entity.GetParameter("position");
                if (position == null) continue;
                if (position.content == null || position.content.dataType != DataType.TRANSFORM) continue;
                cTransform localTransform = (cTransform)position.content;

                // Convert cTransform to Matrix4x4
                Quaternion rotation = Quaternion.CreateFromYawPitchRoll(
                    localTransform.rotation.Y * (float)Math.PI / 180.0f,  // Yaw
                    localTransform.rotation.X * (float)Math.PI / 180.0f,  // Pitch
                    localTransform.rotation.Z * (float)Math.PI / 180.0f   // Roll
                );
                
                Matrix4x4 localMatrix = Matrix4x4.CreateFromQuaternion(rotation) * 
                                       Matrix4x4.CreateTranslation(localTransform.position);
                
                // Combine transforms using proper matrix multiplication
                globalMatrix = localMatrix * globalMatrix;
            }

            // Decompose the final matrix back to position and rotation
            Matrix4x4.Decompose(globalMatrix, out Vector3 scale, out Quaternion finalRotation, out Vector3 finalPosition);
            
            // Convert quaternion back to Euler angles
            (decimal yaw, decimal pitch, decimal roll) = finalRotation.ToYawPitchRoll();
            Vector3 finalEulerRotation = new Vector3((float)pitch, (float)yaw, (float)roll);
            
            cTransform globalTransform = new cTransform(finalPosition, finalEulerRotation);

            bool isFromRoot = 
                (display.Path.PreviousComposite == null && display.Composite == Content.Level.Commands.EntryPoints[0]) ||         //Current composite is root
                (display.Path.AllComposites.Count > 0 && display.Path.AllComposites[0] == Content.Level.Commands.EntryPoints[0]); //First composite in path is root

            guI_TransformDataType1.PopulateUI(null, globalTransform, isFromRoot ? "Global Position" : "Relative Position", true);
        }
    }
}
