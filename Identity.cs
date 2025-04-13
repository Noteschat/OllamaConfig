using System.Text.Json.Serialization;

namespace OllamaConfig
{
    public struct User
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
