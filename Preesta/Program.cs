using System;
using System.Linq;
using System.Reflection;

namespace Preesta
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
            {
                Console.WriteLine(GetVersionString());
                return 0;
            }

            if (args.Length == 0 || args.Any(a => a == "--help" || a == "-h"))
            {
                Console.WriteLine(GetHelpText());
                return args.Length == 0 ? 1 : 0;
            }

            if (args.Length != 1)
            {
                Console.Error.WriteLine("Preesta takes exactly one argument: the schedule group name.");
                Console.Error.WriteLine("Run `preesta --help` for usage.");
                return 1;
            }

            Application.Run(args);
            return 0;
        }

        private static string GetVersionString()
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return $"preesta {info ?? asm.GetName().Version?.ToString() ?? "unknown"}";
        }

        private static string GetHelpText() =>
@"preesta — rule-based digests for your issue trackers.

Usage:
  preesta <schedule-group>    Run all rules whose `group:` matches.
  preesta --version | -v      Print version and exit.
  preesta --help    | -h      Show this help.

Configuration lives in:
  appsettings.yaml          non-secret defaults
  secrets/appsettings.secrets.yaml   tokens and passwords (gitignored)
  rules.yaml                rule definitions

Docs: https://preesta.github.io/preesta/";
    }
}
