using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Universal_Pumpkin
{
    public static class MojangApiHelper
    {
        private static readonly HttpClient _client = new HttpClient();

        public static async Task<string> GetUuidFromUsernameAsync(string username)
        {
            try
            {
                string url = $"https://api.mojang.com/users/profiles/minecraft/{username}";

                string json = await _client.GetStringAsync(url);
                var data = JObject.Parse(json);

                string uuid = data["id"]?.ToString();

                if (!string.IsNullOrEmpty(uuid) && uuid.Length == 32)
                {
                    return $"{uuid.Substring(0, 8)}-{uuid.Substring(8, 4)}-{uuid.Substring(12, 4)}-{uuid.Substring(16, 4)}-{uuid.Substring(20, 12)}";
                }

                return uuid;
            }
            catch
            {
                return null;
            }
        }
    }
}