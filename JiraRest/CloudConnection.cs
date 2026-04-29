using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx.Synchronous;

namespace JiraRest
{
    public class CloudConnection : IJiraGateway, IDisposable
    {
        private readonly Connection _inner;
        private readonly Lazy<Uri> _apiRoot;

        public CloudConnection(string tenantUri, string user, string apiToken)
        {
            _inner = new Connection(tenantUri, user, apiToken);
            _apiRoot = new Lazy<Uri>(() => ResolveApiRoot(_inner));
        }

        internal HttpClient Client
        {
            get => _inner.Client;
            set => _inner.Client = value;
        }

        internal Uri ApiRoot => _apiRoot.Value;

        public dynamic GetIssuesFromJql(string query, int? maxResults, bool includeHistory = false)
        {
            var uri = new Uri(ApiRoot, "rest/api/3/search/jql");
            var requestBody = new
            {
                jql = query,
                maxResults = maxResults ?? 50,
                fields = new[] { "*all" }
            };
            return PostJson(uri, requestBody);
        }

        public dynamic GetIssue(string issueKey)
        {
            var uri = new Uri(ApiRoot, $"rest/api/3/issue/{issueKey}");
            return GetJson(uri);
        }

        public dynamic GetIssueWorklogs(string issueKey)
        {
            var uri = new Uri(ApiRoot, $"rest/api/3/issue/{issueKey}/worklog");
            return GetJson(uri);
        }

        public dynamic GetIssueComments(string issueKey)
        {
            var uri = new Uri(ApiRoot, $"rest/api/3/issue/{issueKey}/comment");
            return GetJson(uri);
        }

        public dynamic GetBuilds(string projectCode)
        {
            var uri = new Uri(ApiRoot, $"rest/api/3/project/{projectCode}/versions");
            return GetJson(uri);
        }

        public string[] GetIssuesInStructure(string structId) =>
            _inner.GetIssuesInStructure(structId);

        public void HandleRequest(HttpRequest request) =>
            _inner.HandleRequest(request);

        public void Dispose() => _inner.Dispose();

        private dynamic GetJson(Uri uri)
        {
            var response = _inner.Client.GetAsync(uri).WaitAndUnwrapException();
            return DecodeResponse(response, uri);
        }

        private dynamic PostJson(Uri uri, object body)
        {
            var json = JsonConvert.SerializeObject(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = _inner.Client.PostAsync(uri, content).WaitAndUnwrapException();
            return DecodeResponse(response, uri);
        }

        private dynamic DecodeResponse(HttpResponseMessage response, Uri uri)
        {
            var text = response.Content.ReadAsStringAsync().WaitAndUnwrapException();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Server returns error '{text}' from url '{uri}'");
            return _inner.Serializer.Deserialize<dynamic>(text);
        }

        private static Uri ResolveApiRoot(Connection inner)
        {
            var tenantInfoUri = new Uri(inner.RootUri, "_edge/tenant_info");
            var response = inner.Client.GetAsync(tenantInfoUri).WaitAndUnwrapException();
            var text = response.Content.ReadAsStringAsync().WaitAndUnwrapException();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Cannot resolve cloudId from '{tenantInfoUri}': {text}");

            var cloudId = (string?)JObject.Parse(text)["cloudId"]
                ?? throw new InvalidOperationException($"cloudId field is missing in tenant_info response: {text}");
            return new Uri($"https://api.atlassian.com/ex/jira/{cloudId}/");
        }
    }
}
