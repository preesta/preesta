using System;

namespace Preesta
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine(
@"Preesta must always be run with a single parameter 'schedule name', for example:
----
# preesta daily
----");
                return;
            }
            Application.Run(args);
        }
    }
}
