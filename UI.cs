// use code from Simon Mourier
// https://stackoverflow.com/questions/11624298/how-do-i-use-openfiledialog-to-select-a-folder/66187224#66187224
#define UI_CUSTOM

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
#if UI_WINDOWS && UI_CUSTOM
        public static string? RequestFolder(string? InputPath = null)
        {
            string? result = null;
            var dlg = new FolderPicker();
            dlg.InputPath = @"c:\windows\system32";
            if (dlg.ShowDialog(IntPtr.Zero) == true)
            {
                result = dlg.ResultPath;
            }
            return result;
        }

#elif UI_WINDOWS
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
#elif UI_CONSOLE
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
