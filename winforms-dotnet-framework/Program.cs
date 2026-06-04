using System;
using System.Windows.Forms;

namespace SimpleNotepad
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application. Accepts an optional file
        /// path argument so the app can be associated with .txt files, just
        /// like the original Notepad.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string startupFile = (args != null && args.Length > 0) ? args[0] : null;
            Application.Run(new MainForm(startupFile));
        }
    }
}
