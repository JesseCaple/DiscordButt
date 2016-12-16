namespace DiscordButt
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class Extensions
    {
        public static async Task<JToken> GetJsonAsync(this HttpClient client, string uri, string jsonPath = null)
        {
            var response = await client.GetStringAsync(uri);
            var json = JObject.Parse(response);
            if (jsonPath != null)
            {
                return json.SelectToken(jsonPath);
            }
            else
            {
                return json;
            }
        }

        public static async Task<T> GetJsonAsync<T>(this HttpClient client, string uri, string jsonPath = null)
        {
            var response = await client.GetStringAsync(uri);
            if (jsonPath != null)
            {
                var json = JObject.Parse(response);
                var token = json.SelectToken(jsonPath);
                return token.ToObject<T>();
            }
            else
            {
                return JsonConvert.DeserializeObject<T>(response);
            }
        }
    }
}
