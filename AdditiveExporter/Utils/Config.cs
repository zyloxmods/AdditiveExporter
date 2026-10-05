using CUE4Parse_Conversion.Options;   // replaces: using CUE4Parse_Conversion.Animations;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AdditiveExporter.Utils
{
    
    public class Config
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public EMeshFormat AnimFormat { get; set; } = EMeshFormat.UEFormat;

        [JsonConverter(typeof(StringEnumConverter))]
        public EGame UEVersion { get; set; } = EGame.GAME_UE6_0;
        
        public string GamePathOverride { get; set; } = "";
        
        public string AesKeyOverride { get; set; } = "";
        
        public string MappingsOverride { get; set; } = "";
    }
}
