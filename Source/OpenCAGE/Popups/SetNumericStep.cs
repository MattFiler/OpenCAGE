using OpenCAGE;
using System;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    public partial class SetNumericStep : Form
    {
        private bool _applyingExternalSettings;

        public SetNumericStep()
        {
            InitializeComponent();

            ApplyValuesFromSettings();

            SettingsManager.SettingsChanged += OnSettingsChanged;
            FormClosed += (s, e) => SettingsManager.SettingsChanged -= OnSettingsChanged;
        }

        private void ApplyValuesFromSettings()
        {
            posStep.Value = (decimal)SettingsManager.GetFloat(Settings.NumericStep);
            rotStep.Value = (decimal)SettingsManager.GetFloat(Settings.NumericStepRot);
        }

        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (!e.ExternalChange || IsDisposed)
                return;

            bool positionChanged = SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, Settings.NumericStep);
            bool rotationChanged = SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, Settings.NumericStepRot);
            if (!positionChanged && !rotationChanged)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnSettingsChanged(sender, e)));
                return;
            }

            _applyingExternalSettings = true;
            try
            {
                if (positionChanged)
                    posStep.Value = (decimal)SettingsManager.GetFloat(Settings.NumericStep);
                if (rotationChanged)
                    rotStep.Value = (decimal)SettingsManager.GetFloat(Settings.NumericStepRot);
            }
            finally
            {
                _applyingExternalSettings = false;
            }
        }

        private void posStep_ValueChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings)
                return;

            SettingsManager.SetFloat(Settings.NumericStep, (float)posStep.Value);
            NumericStepSettings.NotifyChanged();
        }

        private void rotStep_ValueChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings)
                return;

            SettingsManager.SetFloat(Settings.NumericStepRot, (float)rotStep.Value);
            NumericStepSettings.NotifyChanged();
        }
    }
}
