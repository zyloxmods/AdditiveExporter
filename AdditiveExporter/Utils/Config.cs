using CUE4Parse_Conversion.Animations;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AdditiveExporter.Utils
{
    
    public class Config
    {
        [JsonConverter(typeof(StringEnumConverter))] 
        public EAnimFormat AnimFormat { get; set; } = EAnimFormat.UEFormat;
        
        [JsonConverter(typeof(StringEnumConverter))]
        public EGame UEVersion { get; set; } = EGame.GAME_UE5_6;
        
    }
}
