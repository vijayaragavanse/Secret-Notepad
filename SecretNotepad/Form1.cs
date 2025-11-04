using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SecretNotepad
{
    public partial class Form1 : Form
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        public Form1()
        {
            InitializeComponent();

            // Hide from taskbar
            this.ShowInTaskbar = false;

            // Always on top
            this.TopMost = true;

            // Make window a tool window to hide from Alt+Tab
            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);

            this.Text = "Secret Notepad";
            this.Width = 600;
            this.Height = 400;

            var textBox = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 14),
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true
            };
            Controls.Add(textBox);

            this.Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE);
        }
    }
}
