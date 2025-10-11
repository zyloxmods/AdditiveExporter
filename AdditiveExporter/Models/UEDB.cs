using Newtonsoft.Json;

namespace AdditiveExporter.Models;

public class UEDBResponse
{
    [JsonProperty("mappings")]
    public UEDBMappings Mappings { get; set; }
}

public class UEDBMappings
{
    [JsonProperty("ZStandard")]
    public string ZStandard { get; set; }
}