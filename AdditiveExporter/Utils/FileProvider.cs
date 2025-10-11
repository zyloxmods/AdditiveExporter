﻿using System.Collections.Generic;
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
            try
            {
                const string uedbEndpoint = "https://uedb.dev/svc/api/v1/fortnite/mappings";
                Logger.Log($"Attempting to fetch mappings from: {uedbEndpoint}", LogLevel.Cfg);
                var response = await HttpClient.GetStringAsync(uedbEndpoint);
                var mappingsData = JsonConvert.DeserializeObject<UEDBResponse>(response);
                if (mappingsData?.Mappings?.ZStandard != null)
                {
                    var url = mappingsData.Mappings.ZStandard;
                    var fileName = Path.GetFileName(url);
                    return await DownloadAndLoadMappings(fileName, url, uedbEndpoint);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to fetch from primary endpoint (uedb.dev): {ex.Message}", LogLevel.Info);
            }
            
            try
            {
                const string dillyEndpoint = "https://export-service.dillyapis.com/v1/mappings";
                Logger.Log($"Attempting to fetch mappings from fallback: {dillyEndpoint}", LogLevel.Cfg);
                var response = await HttpClient.GetStringAsync(dillyEndpoint);
                var mappingsData = JsonConvert.DeserializeObject<List<MappingsResponse>>(response)?.FirstOrDefault();
                if (mappingsData != null)
                {
                    return await DownloadAndLoadMappings(mappingsData.FileName, mappingsData.Url, dillyEndpoint);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to fetch from fallback endpoint (dillyapis): {ex.Message}", LogLevel.Error);
            }
            
            var finalErrorMessage = "Failed to fetch mappings from all available endpoints.";
            Logger.Log(finalErrorMessage, LogLevel.Error);
            throw new Exception(finalErrorMessage);
        }

        private static async Task<FileUsmapTypeMappingsProvider> DownloadAndLoadMappings(string fileName, string url, string sourceEndpoint)
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(url))
            {
                var error = $"Received invalid mappings data from {sourceEndpoint}.";
                Logger.Log(error, LogLevel.Error);
                throw new Exception(error);
            }

            var path = Path.Combine(Constants.DataPath, fileName);
            if (!File.Exists(path))
            {
                Logger.Log($"Cannot find latest mappings file '{fileName}', downloading from {url}", LogLevel.Cfg);
                byte[] mappingBytes = await HttpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(path, mappingBytes);
            }

            Logger.Log($"Mappings pulled from file: {fileName}", LogLevel.Cue4);
            return new FileUsmapTypeMappingsProvider(path);
        }
    }
}