using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tests")]

// This instruction is for NSubstitute<Preesta.ILogger> in the Tests library since ILogger is internal
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
