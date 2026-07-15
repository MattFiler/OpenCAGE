using CathodeLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    public partial class GameDirectory : UserControl
    {
        public Action<string> OnSetDefault;

        public bool IsDefault { get { return _isDefault; } }
        private bool _isDefault = false;

        public string GameVersion => gameVersion.Text;

        private Process _subProcess = null;

        public GameDirectory()
        {
            InitializeComponent();

            this.Disposed += GameDirectory_Disposed;
        }

        private void GameDirectory_Disposed(object sender, EventArgs e)
        {
            if (_subProcess != null)
            {
                _subProcess.Exited -= P_Exited;
            }
        }

        public void Populate(string path)
        {
            gameInstallDir.Text = path;
            gameVersion.Text = PatchManager.GetPlatform(path).ToString();

            if (path == Singleton.PathToAI)
            {
                groupBox1.Text = "CURRENTLY LOADED";
                openInEditor.Enabled = false;
            }
            else
            {
                _subProcess = ChildInstanceManager.GetProcess(path);
                if (_subProcess != null)
                {
                    openInEditor.Enabled = false;
                    _subProcess.Exited += P_Exited;
                }
            }
        }

        private void P_Exited(object sender, EventArgs e)
        {
            _subProcess.Exited -= P_Exited;
            this.BeginInvoke(new Action(() =>
            {
                openInEditor.Enabled = true;
            }));
        }

        public void MarkAsDefault(bool isDefault = true)
        {
            setAsDefault.Enabled = !isDefault;
            _isDefault = isDefault;
        }

        private void setAsDefault_Click(object sender, EventArgs e)
        {
            OnSetDefault?.Invoke(gameInstallDir.Text);
        }

        private void openInEditor_Click(object sender, EventArgs e)
        {
            openInEditor.Enabled = false;
            _subProcess = ChildInstanceManager.Start(gameInstallDir.Text);
            _subProcess.Exited += P_Exited;
        }
    }
    
    public static class ChildInstanceManager
    {
        public static Dictionary<Process, string> _processes = new Dictionary<Process, string>();
        
        public static Process Start(string path)
        {
            string args = "-pathToAI=\"" + path + "\"";
            if (!Singleton.ViewportEnabled || SettingsManager.GetBool(Settings.LaunchChildrenWithoutViewport))
                args += " -disable_viewport";
            if (Singleton.DontRequireAIexe)
                args += " -dontRequireAIexe";

            Process p = Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location, args);
            p.EnableRaisingEvents = true;
            _processes.Add(p, path);

            p.Exited += P_Exited;
            return p;
        }

        private static void P_Exited(object sender, EventArgs e)
        {
            Process p = (Process)sender;
            p.Exited -= P_Exited;
            _processes.Remove(p);
        }

        public static Process GetProcess(string path)
        {
            foreach (KeyValuePair<Process, string> entry in _processes)
            {
                if (entry.Value == path)
                    return entry.Key;
            }
            return null;
        }
    }
}
