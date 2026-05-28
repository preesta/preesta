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
            // CLI args are the lefthook-style tag filter. Zero args = run every
            // rule (no filter); one or more = OR-match against each rule's
            // `tags:`, with untagged rules dropping out per lefthook semantics.
            var container = new DependencyContainer((IReadOnlyList<string>)args.ToList());
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