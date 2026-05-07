using System;
using System.Windows.Forms;

public class SmoothNumericUpDown : NumericUpDown
{
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // Base delta is 120 per "standard" notch
        int numberOfNotches = e.Delta / SystemInformation.MouseWheelScrollDelta;

        if (numberOfNotches > 0)
        {
            this.Value = Math.Min(this.Maximum, this.Value + this.Increment);
        }
        else if (numberOfNotches < 0)
        {
            this.Value = Math.Max(this.Minimum, this.Value - this.Increment);
        }

        // Prevent the base control from processing multiple events
        ((HandledMouseEventArgs)e).Handled = true;
    }
}
