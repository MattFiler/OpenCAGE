using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The dock panels hide when closed rather than closing - the tab's X and a layout reset must leave
    /// them alive to show again - and do so by cancelling their FormClosing. An application exit is not
    /// a close to cancel: Application.Exit asks every open form in turn and gives up at the first refusal,
    /// so a panel that always refused kept a crashed Ship build running half torn down after the crash
    /// handler's dialogs (Program.HandleError), and swallowed the dark-mode restart whenever a level was
    /// loaded. The same goes for Windows shutting down and Task Manager ending the process.
    /// </summary>
    internal static class CloseReasons
    {
        public static bool IsApplicationShutdown(FormClosingEventArgs e)
        {
            return e.CloseReason == CloseReason.ApplicationExitCall
                || e.CloseReason == CloseReason.WindowsShutDown
                || e.CloseReason == CloseReason.TaskManagerClosing;
        }
    }
}
