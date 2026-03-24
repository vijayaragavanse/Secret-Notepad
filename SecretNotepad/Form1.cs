using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SecretNotepad
{
    public partial class Form1 : Form
    {
        private MeetingAssistant assistant; // Move this inside the Form1 class

        // Import User32 to hide the window from screen capture
        [DllImport("user32.dll")]
        public static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

        const uint WDA_NONE = 0x00000000;
        const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011; // Hides from screenshare

        private RichTextBox notepadBox;

        public Form1()
        {
            InitializeComponent();
            SetupModernUI();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Apply the stealth mode as soon as the form loads
            SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE);

            // Initialize and start the meeting assistant
            assistant = new MeetingAssistant(this);
            assistant.StartListening();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F12)
            {
                assistant.ClearSession();
                AppendAIResponse("[SYSTEM: Meeting memory cleared. Starting fresh.]");
            }
        }

        private void SetupModernUI()
        {
            // Modernize the Form itself
            this.Text = "Secret Notepad";
            this.Size = new Size(450, 600);
            this.BackColor = Color.FromArgb(30, 30, 30); // Dark mode background
            this.ForeColor = Color.White;
            this.TopMost = true; // Keeps it above other windows
            this.ShowInTaskbar = false; // Optional: hides it from the bottom taskbar

            // Initialize and style the RichTextBox
            notepadBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                Margin = new Padding(10),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                // ADD THIS LINE: Force the standard arrow pointer
                Cursor = Cursors.Arrow
            };

            this.Controls.Add(notepadBox);
            this.KeyPreview = true; // Allows the form to catch keystrokes
            this.KeyDown += Form1_KeyDown; // Attach the event
        }

        // Helper method to append AI responses with formatting
        public void AppendAIResponse(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(AppendAIResponse), text);
                return;
            }

            notepadBox.SelectionStart = notepadBox.TextLength;
            notepadBox.SelectionLength = 0;
            notepadBox.SelectionColor = Color.Cyan; // AI text color
            notepadBox.SelectionFont = new Font("Segoe UI", 11F, FontStyle.Bold);
            notepadBox.AppendText("\nAI: " + text + "\n");
            notepadBox.SelectionColor = notepadBox.ForeColor;
            notepadBox.ScrollToCaret();
        }
    }
}