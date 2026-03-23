using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class BorderlessResizableForm : Form
{
    private const int WM_NCHITTEST = 0x84;
    private const int HTCLIENT = 1;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private const int resizeArea = 8; // толщина зоны ресайза

    public BorderlessResizableForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(200, 100);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);

            if ((int)m.Result == HTCLIENT)
            {
                var cursor = PointToClient(Cursor.Position);

                bool left = cursor.X <= resizeArea;
                bool right = cursor.X >= Width - resizeArea;
                bool top = cursor.Y <= resizeArea;
                bool bottom = cursor.Y >= Height - resizeArea;

                if (left && top) m.Result = (IntPtr)HTTOPLEFT;
                else if (right && top) m.Result = (IntPtr)HTTOPRIGHT;
                else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (left) m.Result = (IntPtr)HTLEFT;
                else if (right) m.Result = (IntPtr)HTRIGHT;
                else if (top) m.Result = (IntPtr)HTTOP;
                else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            }

            return;
        }

        base.WndProc(ref m);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    protected void DragForm(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(this.Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN + HTCAPTION
        }
    }
}