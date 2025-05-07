using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using AdditiveExporter.Models;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Animations;
using CUE4Parse.Compression;

namespace AdditiveExporter.Utils
{
    public class FileProvider
    {
        public static DefaultFileProvider? Provider { get; set; }
        private static Config? _config;
        private static readonly HttpClient HttpClient = new HttpClient();

        public static async Task Init()
        {
            try
            {
                LoadConfig();
                string aesResponse = await HttpClient.GetStringAsync("https://fortnitecentral.genxgames.gg/api/v1/aes");
                var aesData = JsonConvert.DeserializeObject<AESResponse>(aesResponse);

                if (aesData == null)
                {
                    Logger.Log("Failed to parse AES response from API", LogLevel.Cue4);
                    return;
                }
                var version = new VersionContainer(_config!.UEVersion);
                Provider = new DefaultFileProvider(FortniteUtils.PaksPath, SearchOption.TopDirectoryOnly, version);
                Provider.Initialize();
                Logger.Log($"File provider initialized with at {FortniteUtils.PaksPath} with {_config.UEVersion}", LogLevel.Cfg);
                
                var keys = new List<KeyValuePair<FGuid, FAesKey>>
                {
                    new KeyValuePair<FGuid, FAesKey>(new FGuid(), new FAesKey(aesData.MainKey))
                };
                
                foreach (var dynamicKey in aesData.DynamicKeys)
                {
                    keys.Add(new KeyValuePair<FGuid, FAesKey>(new FGuid(dynamicKey.Guid), new FAesKey(dynamicKey.Key)));
                }
                
                await Provider.SubmitKeysAsync(keys);
                Logger.Log($"Loaded {Provider.Keys.Count} keys", LogLevel.Cue4);
                Logger.Log($"AnimFormat set to {_config.AnimFormat}", LogLevel.Cfg);

                var oodlePath = Path.Combine(Constants.DataPath, OodleHelper.OODLE_DLL_NAME);
                if (File.Exists(OodleHelper.OODLE_DLL_NAME))
                {
                    File.Move(OodleHelper.OODLE_DLL_NAME, oodlePath, true);
                }
                else if (!File.Exists(oodlePath))
                {
                    await OodleHelper.DownloadOodleDllAsync(oodlePath);
                }

                OodleHelper.Initialize(oodlePath);
                var mappings = await Mappings();

                Provider.MappingsContainer = mappings;
            }
            catch (Exception ex)
            {
                Logger.Log(ex.ToString(), LogLevel.Cue4);
            }
        }

        private static void LoadConfig()
        {
            var configPath = Path.Combine(Constants.DataPath, "config.json");

            if (File.Exists(configPath))
            {
                var configContent = File.ReadAllText(configPath);
                _config = JsonConvert.DeserializeObject<Config>(configContent) ?? new Config();
            }
            else
            {
                Logger.Log("Config file not found. Creating a default config.json file.", LogLevel.Cfg);
                
                _config = new Config();
                
                var defaultConfigContent = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(configPath, defaultConfigContent);

                Logger.Log("Default config.json file created.", LogLevel.Cfg);
            }
        }

        public static void ExportAdditiveAnimation()
        {
            Logger.Log("Enter the path to the Additive Pose:", LogLevel.Cue4);
            string additivePose = Console.ReadLine() ?? string.Empty;
            additivePose = EnsureCorrectPath(additivePose);

            Logger.Log("Enter the path to the Ref Pose:", LogLevel.Cue4);
            string refPose = Console.ReadLine() ?? string.Empty;
            refPose = EnsureCorrectPath(refPose);

            var addUAnimSequence = Provider!.LoadPackageObject<UAnimSequence>(additivePose);
            var refUAnimSequence = Provider.LoadPackageObject<UAnimSequence>(refPose);

            addUAnimSequence.RefPoseSeq = new ResolvedLoadedObject(refUAnimSequence);
            var exporterOptions = new ExporterOptions()
            {
                AnimFormat = _config!.AnimFormat
            };
            var exporter = new AnimExporter(addUAnimSequence, exporterOptions);
            exporter.TryWriteToDir(new DirectoryInfo(Constants.ExportPath), out _, out var fileName);
            Logger.Log($"Exported to: {fileName}", LogLevel.Cue4);
            Logger.Log("Ready for next export...");
            Logger.Log("Press Ctrl+C to exit");
        }

        private static string EnsureCorrectPath(string inputPath)
        {
            var fileName = Path.GetFileName(inputPath);
            if (!fileName.Contains("."))
            {
                inputPath = $"{inputPath}.{fileName}";
            }

            return inputPath;
        }
        
        private static async Task<FileUsmapTypeMappingsProvider> Mappings()
        {
            var mappingsResponse = await HttpClient.GetStringAsync("https://fortnitecentral.genxgames.gg/api/v1/mappings");
            var mappingsData = JsonConvert.DeserializeObject<List<MappingsResponse>>(mappingsResponse)?.FirstOrDefault();

            if (mappingsData == null)
            {
                String noMappings = "Mappings data is null. Please check the API or try again later.";
                Logger.Log(noMappings, LogLevel.Cue4);
                throw new Exception(noMappings);
            }

            var path = Path.Combine(Constants.DataPath, mappingsData.FileName);
            if (!File.Exists(path))
            {
                Logger.Log($"Cannot find latest mappings, Downloading {mappingsData.Url}", LogLevel.Cfg);
                
                byte[] mappingBytes = await HttpClient.GetByteArrayAsync(mappingsData.Url);
                await File.WriteAllBytesAsync(path, mappingBytes);
            }

            var latestUsmapInfo = new DirectoryInfo(Constants.DataPath).GetFiles("*.usmap")
                .FirstOrDefault(x => x.Name == mappingsData.FileName);

            if (latestUsmapInfo == null)
            {
                String noMappings = "Mappings file not found after download.";
                Logger.Log(noMappings, LogLevel.Cue4);
                throw new FileNotFoundException(noMappings);
            }

            Logger.Log($"Mappings pulled from file: {latestUsmapInfo.Name}", LogLevel.Cue4);
            return new FileUsmapTypeMappingsProvider(latestUsmapInfo.FullName);
        }
    }
}