using System;
using System.Windows.Forms;

namespace OpenCAGE
{
    public static class NumericStepSettings
    {
        public static event Action Changed;

        public static float PositionStep => SettingsManager.GetFloat(Settings.NumericStep, 0.1f);
        public static float RotationStep => SettingsManager.GetFloat(Settings.NumericStepRot, 1.0f);

        public static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        public static void ApplyPositionStep(NumericUpDown control)
        {
            if (control == null || control.IsDisposed)
                return;

            control.Increment = (decimal)PositionStep;
        }

        public static void ApplyRotationStep(NumericUpDown control)
        {
            if (control == null || control.IsDisposed)
                return;

            control.Increment = (decimal)RotationStep;
        }

        public static void ApplyTransformSteps(
            NumericUpDown posX,
            NumericUpDown posY,
            NumericUpDown posZ,
            NumericUpDown rotX,
            NumericUpDown rotY,
            NumericUpDown rotZ)
        {
            ApplyPositionStep(posX);
            ApplyPositionStep(posY);
            ApplyPositionStep(posZ);
            ApplyRotationStep(rotX);
            ApplyRotationStep(rotY);
            ApplyRotationStep(rotZ);
        }
    }
}
