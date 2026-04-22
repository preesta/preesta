using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    internal class StubDelegatingHandler : DelegatingHandler
    {
        public Queue<HttpResponseMessage> Responses { get; } = new();
        public List<HttpRequestMessage> Requests { get; } = new();

        public StubDelegatingHandler(params HttpResponseMessage[] responses)
        {
            foreach (var r in responses) Responses.Enqueue(r);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Responses.Count > 0 ? Responses.Dequeue() : new HttpResponseMessage());
        }
    }
}
