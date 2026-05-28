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

            if (args.Any(a => a == "--help" || a == "-h"))
            {
                Console.WriteLine(GetHelpText());
                return 0;
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
  preesta                    Run every rule (no tag filter).
  preesta <tag> [<tag>…]     Run rules whose `tags:` include any of these.
                             Untagged rules are skipped when a tag is given.
  preesta --version | -v     Print version and exit.
  preesta --help    | -h     Show this help.

Configuration lives in:
  appsettings.yaml          non-secret defaults
  secrets/appsettings.secrets.yaml   tokens and passwords (gitignored)
  rules.yaml                rule definitions

Docs: https://preesta.github.io/preesta/";
    }
}
