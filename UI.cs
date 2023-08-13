using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace HashTester
{
    internal class UI
    {
#if UI_WINDOWS
        public static string? RequestFolder()
        {
            string? result = null;

            OpenFileDialog folderBrowser = new OpenFileDialog();
            // Set validate names and check file exists to false otherwise windows will
            // not let you select "Folder Selection."
            folderBrowser.ValidateNames = false;
            folderBrowser.CheckFileExists = false;
            folderBrowser.CheckPathExists = true;
            // Always default to Folder Selection.
            folderBrowser.FileName = "Select Folder";
            if (folderBrowser.ShowDialog() == DialogResult.OK)
            {
                result = Path.GetDirectoryName(folderBrowser.FileName);
            }
            return result;
        }
#else
        public static string? RequestFolder()
        {
            string? result = null;

            do
            {
                // write to stderror in case stdout is piped
                Console.Error.WriteLine("Folder to examine:");
                result = Console.ReadLine();
            }
            while (!Directory.Exists(result));

            return result;
        }
#endif
    }
}
