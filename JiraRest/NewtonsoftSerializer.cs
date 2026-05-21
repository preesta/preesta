using Newtonsoft.Json;

namespace JiraRest
{
    internal class NewtonsoftSerializer : ISerializer
    {
        public T Deserialize<T>(string input)
        {
            return JsonConvert.DeserializeObject<T>(input)
                ?? throw new System.InvalidOperationException(
                    $"Empty or null JSON body where {typeof(T).Name} was expected.");
        }
    }
}