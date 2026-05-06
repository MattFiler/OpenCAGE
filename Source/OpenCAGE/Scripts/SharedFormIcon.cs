using System.Drawing;
using System.Windows.Forms;

namespace CommandsEditor
{
    public static class SharedFormIcon
    {
        private static Icon _icon;

        public static Icon Icon
        {
            get
            {
                if (_icon == null)
                {
                    _icon = SystemIcons.Application;
                    try
                    {
                        var executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                        if (executableIcon != null)
                        {
                            _icon = executableIcon;
                        }
                    }
                    catch
                    {
                        // Fallback stays as SystemIcons.Application.
                    }
                }

                return _icon;
            }
        }
    }
}
