using System.Drawing;

namespace AsterMultiseat;

public sealed class SeatWindow : Form
{
    private readonly Label _status;

    public SeatWindow(string seatName, Color color)
    {
        SeatName = seatName;
        Text = $"Seat: {seatName}";
        BackColor = color;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;

        _status = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 28, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = $"{seatName}\nWaiting for input..."
        };

        Controls.Add(_status);
    }

    public string SeatName { get; }

    public void UpdateStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateStatus(message));
            return;
        }

        _status.Text = message;
    }
}
