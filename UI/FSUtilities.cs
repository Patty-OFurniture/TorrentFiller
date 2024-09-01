using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HashTester.UI
{
    internal class FSUtilities
    {
#if WINDOWS
        private static string[] reserved = {"CON", "PRN", "AUX", "NUL" };

        // superscripts are ISO/IEC 8859-1, and interpreted as 1,2,3
        private static string pattern = @"(COM|LPT)[0-9¹²³]";
        private static Regex reservedCounters = new Regex(pattern, RegexOptions.Compiled);

        public static bool IsReserved(string path)
        {
            if (string.IsNullOrEmpty(path)) 
                return false;

            path = Path.GetFileNameWithoutExtension(path); // NUL is invalid, as is NUL.txt etc.
            path = path.ToUpper();

            if (reserved.Contains(path))
                return true;

            if (reserved.Length == 4 && reservedCounters.IsMatch(path)) 
                return true;

            return false;
        }

        public static string? IsValidPath(object value)
        {
            // http://techfilth.blogspot.com/2011/07/taking-data-binding-validation-and-mvvm.html
            if (value != null && value.GetType() != typeof(string))
                return "Input value was of the wrong type, expected a string";

            var filePath = value as string;

            //check for empty/null file path:
            if (string.IsNullOrEmpty(filePath))
            {
                //if (!AllowEmptyPath)
                    return "The file path may not be empty.";
                //else
                //    return null;
            }

            //null & empty has been handled above, now check for pure whitespace:
            if (string.IsNullOrWhiteSpace(filePath))
                return "The file path cannot consist only of whitespace.";

            //check the path:
            if (Path.GetInvalidPathChars().Any(x => filePath.Contains(x)))
                return "Invalid characters found in file path.";

            //check the filename (if one can be isolated out):
            try
            {
                string fileName = Path.GetFileName(filePath);
                if (Path.GetInvalidFileNameChars().Any(x => fileName.Contains(x)))
                    throw new ArgumentException("Invalid characters found in file name.");
            }
            catch (ArgumentException e) { return e.ToString(); }

            return null;
        }

        public static bool IsAbsolutePath(string path)
        {
            return Path.IsPathFullyQualified(path);
        }

        // https://stackoverflow.com/questions/1266674/how-can-one-get-an-absolute-or-normalized-file-path-in-net
        public static string NormalizePath(string path)
        {
            return path; // TODO: these validations assume fully qualified
            return new Uri(path).LocalPath;
            return Path.GetFullPath(new Uri(path).LocalPath)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       //.ToUpperInvariant()
                       ;
        }
#else
#endif
    }
}
