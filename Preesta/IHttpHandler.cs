using System.Collections.Generic;
using JiraRest;

namespace Preesta
{
    public interface IHttpHandler
    {
        void HandleAll(IEnumerable<HttpRequest> requests);
    }
}