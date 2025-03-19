using Newtonsoft.Json;

namespace AdditiveExporter.Models
{
    public class AESResponse
    {
        [JsonProperty("version")]
        public string Version { get; set; }
        
        [JsonProperty("mainKey")]
        public string MainKey { get; set; }
        
        [JsonProperty("dynamicKeys")]
        public List<DynamicKeyInfo> DynamicKeys { get; set; } = new List<DynamicKeyInfo>();
    }
    
    public class DynamicKeyInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("guid")]
        public string Guid { get; set; }
        
        [JsonProperty("key")]
        public string Key { get; set; }
    }
    
    public class MappingsResponse
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("length")]
        public int Length { get; set; }

        [JsonProperty("uploaded")]
        public DateTime Uploaded { get; set; }

        [JsonProperty("meta")]
        public Meta Meta { get; set; }
    }

    public class Meta
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("compressionMethod")]
        public string CompressionMethod { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }
    }
}