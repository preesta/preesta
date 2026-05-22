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

            // Run every configured tracker's pipeline plus the release pipeline.
            // No tracker is named here — the container hands back whatever the
            // module loop registered, so adding a tracker never touches this loop.
            var tasks = container.IssuePipelines()
                .Select(pipe => pipe.RunAsync())
                .ToList();

            var releasePipe = container.ReleasePipeline();
            if (releasePipe != null)
                tasks.Add(releasePipe.RunAsync());

            Task.WaitAll(tasks.ToArray());
        }
    }
}