using Newtonsoft.Json.Linq;

namespace LinearGraphQL
{
    /// <summary>
    /// Minimal Linear gateway used by Preesta.
    /// Future-proof but intentionally small for the MVP.
    /// </summary>
    public interface ILinearGateway
    {
        /// <summary>
        /// POSTs the given GraphQL query (and optional variables object) to Linear and
        /// returns the raw JSON response envelope (<c>data</c> + optional <c>errors</c>).
        /// </summary>
        JObject Query(string graphqlQuery, object? variables = null);
    }
}
