using Newtonsoft.Json;

namespace JiraRest
{
    internal class NewtonsoftSerializer : ISerializer
    {
        public T Deserialize<T>(string input)
        {
            return JsonConvert.DeserializeObject<T>(input);
        }
    }
}