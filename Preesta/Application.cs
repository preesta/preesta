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

            // GitLab pipeline runs only when registered (i.e. Gitlab:token is set).
            var gitlabPipe = container.TryResolveNotificationPipe<Issue>("Gitlab");
            if (gitlabPipe != null)
                tasks.Add(gitlabPipe.RunAsync());

            Task.WaitAll(tasks.ToArray());
        }
    }
}