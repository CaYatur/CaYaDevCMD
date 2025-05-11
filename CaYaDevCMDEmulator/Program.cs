using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CaYaDevCMDEmulator
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Komut argümaný var mý kontrol et
            if (args.Length > 0)
            {
                // Konsol modu
                string command = string.Join(" ", args);

                CmdEmulator cmdEmulator = new CmdEmulator(true);
                string output = cmdEmulator.ExecuteCommandLine(command);

                // Çýktýyý konsola yaz
                Console.WriteLine(output);
            }
            else
            {
                // GUI modu
                Application.Run(new CmdEmulator());
            }
        }
    }
}