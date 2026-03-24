using System;
using System.Threading;
using System.Windows.Forms;

namespace SecretNotepad
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Create a system-wide Mutex lock
            using (Mutex mutex = new Mutex(false, "Global\\WinAudioHostMutex"))
            {
                // Check if another instance already has the lock
                if (!mutex.WaitOne(0, false))
                {
                    // The app is already running! Exit silently.
                    return;
                }

                // If we got the lock, start the application normally
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
        }
    }
}