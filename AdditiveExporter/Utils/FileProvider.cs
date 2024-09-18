using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using System.Net;
using Newtonsoft.Json;
using AdditiveExporter.Models;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Animations;
using CUE4Parse.Compression;

namespace AdditiveExporter.Utils
{
    public class FileProvider
    {
        public static DefaultFileProvider Provider { get; set; }
        private static Config _config;

        public static async Task Init()
        {
            try
            {
                // Load configuration
                LoadConfig();

                var aes = JsonConvert
                    .DeserializeObject<FortniteAPIResponse<AES>>(
                        await new HttpClient().GetStringAsync("https://fortnite-api.com/v2/aes")).Data;

                Provider = new DefaultFileProvider(FortniteUtils.PaksPath, SearchOption.TopDirectoryOnly, false,
                    new VersionContainer(EGame.GAME_UE5_5));
                Provider.Initialize();

                var keys = new List<KeyValuePair<FGuid, FAesKey>>
                {
                    new KeyValuePair<FGuid, FAesKey>(new FGuid(), new FAesKey(aes.MainKey))
                };

                keys.AddRange(aes.DynamicKeys.Select(x =>
                    new KeyValuePair<FGuid, FAesKey>(new Guid(x.PakGuid), new FAesKey(x.Key))));
                await Provider.SubmitKeysAsync(keys);
                Logger.Log($"File provider initialized with {Provider.Keys.Count} keys", LogLevel.Cue4);

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
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

            if (File.Exists(configPath))
            {
                var configContent = File.ReadAllText(configPath);
                _config = JsonConvert.DeserializeObject<Config>(configContent) ?? new Config();
            }
            else
            {
                Logger.Log("Config file not found. Creating a default config.json file.", LogLevel.Cue4);
                
                _config = new Config();
                
                var defaultConfigContent = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(configPath, defaultConfigContent);

                Logger.Log("Default config.json file created.", LogLevel.Cue4);
            }
        }

        public static void ExportAdditiveAnimation()
        {
            Console.WriteLine("Enter the path to the Additive Pose:");
            string additivePose = Console.ReadLine() ?? string.Empty;
            additivePose = EnsureCorrectPath(additivePose);

            Console.WriteLine("Enter the path to the Ref Pose:");
            string refPose = Console.ReadLine() ?? string.Empty;
            refPose = EnsureCorrectPath(refPose);

            var addUAnimSequence = Provider.LoadObject<UAnimSequence>(additivePose);
            var refUAnimSequence = Provider.LoadObject<UAnimSequence>(refPose);

            addUAnimSequence.RefPoseSeq = new ResolvedLoadedObject(refUAnimSequence);
            var exporterOptions = new ExporterOptions()
            {
                AnimFormat = _config.AnimFormat
            };
            var exporter = new AnimExporter(addUAnimSequence, exporterOptions);
            exporter.TryWriteToDir(new DirectoryInfo(Constants.ExportPath), out var label, out var fileName);
            Logger.Log($"Exported to: {fileName}", LogLevel.Cue4);
            Logger.Log("Press any key to exit...");
            Console.ReadKey();
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
            var mappingsResponse = await new HttpClient().GetStringAsync("https://fortnitecentral.genxgames.gg/api/v1/mappings");
            var mappingsData = JsonConvert.DeserializeObject<List<MappingsResponse>>(mappingsResponse)?.FirstOrDefault();

            if (mappingsData == null)
            {
                Logger.Log("Failed to fetch mappings. No data received from the API.", LogLevel.Cue4);
                throw new Exception("Mappings data is null. Please check the API or try again later.");
            }

            var path = Path.Combine(Constants.DataPath, mappingsData.FileName);
            if (!File.Exists(path))
            {
                Logger.Log($"Cannot find latest mappings, Downloading {mappingsData.Url}", LogLevel.Cue4);
                var wc = new WebClient();
                wc.DownloadFile(new Uri(mappingsData.Url), path);
            }

            var latestUsmapInfo = new DirectoryInfo(Constants.DataPath).GetFiles("*.usmap")
                .FirstOrDefault(x => x.Name == mappingsData.FileName);

            if (latestUsmapInfo == null)
            {
                Logger.Log("Could not find the downloaded mappings file.", LogLevel.Cue4);
                throw new FileNotFoundException("Mappings file not found after download.");
            }

            Logger.Log($"Mappings pulled from file: {latestUsmapInfo.Name}", LogLevel.Cue4);
            return new FileUsmapTypeMappingsProvider(latestUsmapInfo.FullName);
        }


    }
}
