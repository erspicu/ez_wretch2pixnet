using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ez_wretch2pixnet
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new wretch2pixnet());
        }
    }
}
