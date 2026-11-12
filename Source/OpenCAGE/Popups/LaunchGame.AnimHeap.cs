using CathodeLib;
using System;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Launch Game's "Allow larger animation memory" (AnimationHeapBudget): an AI.exe patch, written when ticked and again at every
    /// launch (McpLevelTools.ApplyPatchSettings), like the other game options.
    /// </summary>
    public partial class LaunchGame
    {
        private bool _settingAnimHeap;

        private void LaunchGame_LoadAnimHeap(object sender, EventArgs e)
        {
            ShowAnimHeapSetting();
            allowLargerAnimMemory.Enabled = AnimationHeapBudget.Supported(Singleton.Platform);
            SettingsManager.SettingsChanged += OnAnimHeapSettingChanged;
            FormClosed += (s, a) => SettingsManager.SettingsChanged -= OnAnimHeapSettingChanged;
        }

        private void ShowAnimHeapSetting()
        {
            _settingAnimHeap = true;
            allowLargerAnimMemory.Checked = AnimationHeapBudget.Enabled;
            _settingAnimHeap = false;
        }

        /* Changed elsewhere (MCP launch_options, a Level Check fix, another OpenCAGE): the box follows */
        private void OnAnimHeapSettingChanged(object sender, SettingsChangedEventArgs e)
        {
            if (IsDisposed || !SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, AnimationHeapBudget.SettingKey))
                return;
            if (InvokeRequired)
                BeginInvoke(new Action(() => { if (!IsDisposed) ShowAnimHeapSetting(); }));
            else
                ShowAnimHeapSetting();
        }

        private void allowLargerAnimMemory_CheckedChanged(object sender, EventArgs e)
        {
            if (_settingAnimHeap) return;
            if (!AnimationHeapBudget.Set(allowLargerAnimMemory.Checked))
                MessageBox.Show("Failed to set the larger animation memory option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
