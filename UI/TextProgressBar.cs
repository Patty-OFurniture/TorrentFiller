using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HashTester.UI
{
    // modified from https://www.codeproject.com/Tips/5255878/A-Console-Progress-Bar-in-Csharp
    internal class TextProgressBar 
    {
        const char _block = '■';
        const string _twirl = "-\\|/";
        private string _back;
        private decimal _widthChars;

        public TextProgressBar(decimal widthChars=20)
        {
            _widthChars = widthChars;
            _back = new string('\b', (int) Math.Round(_widthChars));
        }

        public void WriteProgressBar(decimal percent, bool update = false)
        {
            if (update)
                Console.Write(_back);
            Console.Write("[");
            var p = (int)((percent * _widthChars / 100m) + .5m);
            for (var i = 0; i < _widthChars; ++i)
            {
                if (i >= p)
                    Console.Write(' ');
                else
                    Console.Write(_block);
            }
            Console.Write("] {0,3:##0}%", percent);
        }
        public void WriteProgress(int progress, bool update = false)
        {
            int x,y;
            (x, y) = Console.GetCursorPosition();

            if (update)
                Console.Write("\b");
            Console.Write(_twirl[progress % _twirl.Length]);

            Console.SetCursorPosition(x, y);
        }
    }
}
