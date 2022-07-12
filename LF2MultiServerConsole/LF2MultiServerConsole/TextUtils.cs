using System;
using System.Collections.Generic;
using System.Text;

namespace LF2MultiServerConsole
{
    class TextUtils
    {
        private static ASCIIEncoding encoding = new ASCIIEncoding();

        public static String SetChar(String text, int index, char newChar)
        {
            byte[] data = encoding.GetBytes(text);
            data[index] = Convert.ToByte(newChar);
            return encoding.GetString(data);
        }

        #if _DEBUG
        public static void PrintBinary(byte[] data)
        {
            for (int i = 0; i < data.Length; ++i)
            {
                Console.Write(data[i]);
                Console.Write(" ");
            }
            Console.WriteLine("");
        }
        #endif
    }
}
