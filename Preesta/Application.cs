using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Preesta.Data;
using Preesta.DI;

namespace Preesta
{
    internal static class Application
    {
        public static void Run(IList<string> args)
        {
            var container = new DependencyContainer(args[0]);
            container.ValidateRules();

            var tasks = new List<Task>
            {
                container.ResolveNotificationPipe<Issue>("Jql").RunAsync(),
                container.ResolveNotificationPipe<Release>().RunAsync()
            };

            // Linear pipeline runs only when registered (i.e. Linear:apiKey is set).
            var linearPipe = container.TryResolveNotificationPipe<Issue>("Linear");
            if (linearPipe != null)
                tasks.Add(linearPipe.RunAsync());

            // GitHub pipeline runs only when registered (i.e. Github:token is set).
            var githubPipe = container.TryResolveNotificationPipe<Issue>("Github");
            if (githubPipe != null)
                tasks.Add(githubPipe.RunAsync());

            // Shortcut pipeline runs only when registered (i.e. Shortcut:apiToken is set).
            var shortcutPipe = container.TryResolveNotificationPipe<Issue>("Shortcut");
            if (shortcutPipe != null)
                tasks.Add(shortcutPipe.RunAsync());

            Task.WaitAll(tasks.ToArray());
        }
    }
}