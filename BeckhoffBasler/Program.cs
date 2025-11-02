using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using RuntimeMultiGPU2;

namespace Inspection
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
           

            SplashScreen.ShowSplashScreen(60); //sec Vision loading...
            SplashScreen.SetStatus("Loading Vision...");

            
            Application.Run(new frmBeckhoff());
        }
    }
}
