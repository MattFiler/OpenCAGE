using System.Windows.Controls;

namespace CommandsEditor
{
    public partial class TimeMarker : UserControl
    {
        public TimeMarker(float seconds, double lineHeight)
        {
            InitializeComponent();
            text.Text = seconds.ToString("0.00") + "s";
            verticalLine.Height = lineHeight;
        }
    }
}
