using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LF2MultiServerConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "LF2 Multi Server v2.0";

            MyConsole.Write("Welcome to ");
            MyConsole.WriteLine(ConsoleColor.Yellow, "LF2 Multi Server v2.0!");
            MyConsole.NewLine();

            LF2MultiServer.Start();
        }
    }
}
