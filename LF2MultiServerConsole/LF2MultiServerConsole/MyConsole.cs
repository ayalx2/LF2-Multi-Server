using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LF2MultiServerConsole
{
    class MyConsole
    {
        private static ConsoleColor INPUT_COLOR = ConsoleColor.Red;
        private static ConsoleColor DEFAULT_OUTPUT_COLOR = ConsoleColor.White;

        private static String _consoleLock = "Lockable object";

        public static void Write(ConsoleColor color, String format, params Object[] arg)
        {
            format = format.Replace("\n", Environment.NewLine);
            lock (_consoleLock)
            {
                Console.ForegroundColor = color;
                Console.Write(format, arg);
                Console.ForegroundColor = INPUT_COLOR;
            }
        }

        public static void Write(ConsoleColor color, String text)
        {
            text = text.Replace("\n", Environment.NewLine);
            lock (_consoleLock)
            {
                Console.ForegroundColor = color;
                Console.Write(text);
                Console.ForegroundColor = INPUT_COLOR;
            }
        }

        public static void WriteLine(ConsoleColor color, String format, params Object[] arg)
        {
            Write(color, format + "\n", arg);
        }

        public static void WriteLine(ConsoleColor color, String text)
        {
            Write(color, text + "\n");
        }

        public static void WriteLine(String format, params Object[] arg)
        {
            Write(DEFAULT_OUTPUT_COLOR, format + "\n", arg);
        }

        public static void WriteLine(String text)
        {
            Write(DEFAULT_OUTPUT_COLOR, text + "\n");
        }

        public static void Write(String format, params Object[] arg)
        {
            Write(DEFAULT_OUTPUT_COLOR, format, arg);
        }

        public static void Write(String text)
        {
            Write(DEFAULT_OUTPUT_COLOR, text);
        }

        public static void NewLine()
        {
            Console.Write(Environment.NewLine);
        }
    }
}
