﻿using DistributionTools;
using Newtonsoft.Json;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace FetchPrebuiltUE4Lib
{
    public static class FetchPrebuiltUE4Lib
    {
        private static readonly ApplicationDefaultCredentialsFile ApplicationDefaultCredentialsFile = new ApplicationDefaultCredentialsFile("application-default-credentials.json");

        private struct Config
        {
            public OAuth.ClientID ClientID;
            public OAuth.ClientSecret ClientSecret;
            public string EndpointOverride;
            public string RegionOverride;
            public Longtail.BlockStorageURI BlockStorageURI;
            public Longtail.VersionIndexStorageURI VersionIndexStorageURI;
            public string UE4Folder;
        }

        private static Longtail.VersionIndexURI PackageNameToURI(Longtail.VersionIndexStorageURI versionIndexStorageURI, string packageName)
        {
            return new Longtail.VersionIndexURI($"{versionIndexStorageURI}/versions/{packageName}.lvi");
        }

        private static Config ReadConfig()
        {
            string configFile = "FetchPrebuiltUE4.config.json";

            JsonSerializer jsonSerializer = new JsonSerializer();

            using (StreamReader streamReader = new StreamReader(configFile))
            using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
            {
                return jsonSerializer.Deserialize<Config>(jsonTextReader);
            }
        }

        private static void Initialize(out Config config, out ApplicationConfiguration applicationConfiguration)
        {
            config = ReadConfig();
            applicationConfiguration = new ApplicationConfiguration
            {
                ClientID = config.ClientID,
                ClientSecret = config.ClientSecret,
                ApplicationDefaultCredentialsFile = ApplicationDefaultCredentialsFile,
                EndpointOverride = config.EndpointOverride,
                RegionOverride = config.RegionOverride
            };
        }

        private static RootCommand CreateCommand()
        {
            RootCommand rootCommand = new RootCommand();

            Command uploadPackage = new Command("upload-package");
            uploadPackage.Add(new Option<string>("--folder") { Required = true });
            uploadPackage.Add(new Option<string>("--package") { Required = true });
            uploadPackage.Handler = CommandHandler.Create<string, string>(UploadPackage);
            rootCommand.Add(uploadPackage);

            Command downloadPackage = new Command("download-package");
            downloadPackage.Add(new Option<string>("--folder") { Required = true });
            downloadPackage.Add(new Option<string>("--package") { Required = true });
            downloadPackage.Handler = CommandHandler.Create<string, string>(DownloadPackage);
            rootCommand.Add(downloadPackage);

            Command updateLocalUE4Version = new Command("update-local-ue4-version");
            updateLocalUE4Version.Handler = CommandHandler.Create(UpdateLocalUE4Version);
            rootCommand.Add(updateLocalUE4Version);

            Command runPrerequisitesInstaller = new Command("run-prerequisites-installer");
            runPrerequisitesInstaller.Handler = CommandHandler.Create(RunPrerequisitesInstaller);
            rootCommand.Add(runPrerequisitesInstaller);

            Command clearAuth = new Command("clear-auth");
            clearAuth.Handler = CommandHandler.Create(ClearGoogleUserAuth);
            rootCommand.Add(clearAuth);

            Command createUserAuth = new Command("create-user-auth");
            createUserAuth.Handler = CommandHandler.Create(CreateGoogleUserAuth);
            rootCommand.Add(createUserAuth);

            return rootCommand;
        }

        public static async Task<int> Run(string[] args)
        {
            RootCommand rootCommand = CreateCommand();

            Task<int> result = rootCommand.InvokeAsync(args);
            return await result;
        }

        private static async Task<int> UpsyncWithAuthentication(Config config, ApplicationConfiguration applicationConfiguration, string folder, string package)
        {
            if (((string)config.BlockStorageURI).StartsWith("gs://"))
            {
                await GoogleOAuthFlow.RefreshUserApplicationDefaultCredentials(applicationConfiguration);

                if (!await Longtail.UpsyncToGSBucket(applicationConfiguration, config.BlockStorageURI, folder, PackageNameToURI(config.VersionIndexStorageURI, package)))
                {
                    Console.WriteLine("Tar operation failed.");
                    return 1;
                }
            }
            else if (((string)config.BlockStorageURI).StartsWith("s3://"))
            {
                if (!await Longtail.UpsyncToS3Bucket(applicationConfiguration, config.BlockStorageURI, folder, PackageNameToURI(config.VersionIndexStorageURI, package)))
                {
                    Console.WriteLine("Tar operation failed.");
                    return 1;
                }
            }
            else
            {
                if (!await Longtail.UpsyncToLocalStore(config.BlockStorageURI, folder, PackageNameToURI(config.VersionIndexStorageURI, package)))
                {
                    Console.WriteLine("Tar operation failed.");
                    return 1;
                }
            }

            return 0;
        }

        private static async Task<int> UploadPackage(string folder, string package)
        {
            Initialize(out Config config, out ApplicationConfiguration applicationConfiguration);

            return await UpsyncWithAuthentication(config, applicationConfiguration, folder, package);
        }

        private static async Task<int> DownsyncWithAuthentication(Config config, ApplicationConfiguration applicationConfiguration, string folder, string package)
        {
            if (((string)config.BlockStorageURI).StartsWith("gs://"))
            {
                await GoogleOAuthFlow.RefreshUserApplicationDefaultCredentials(applicationConfiguration);

                if (!await Longtail.DownsyncFromGSBucket(applicationConfiguration, config.BlockStorageURI, folder, PackageNameToURI(config.VersionIndexStorageURI, package)))
                {
                    Console.WriteLine("Untar operation failed.");
                    return 1;
                }
            }
            if (((string)config.BlockStorageURI).StartsWith("s3://"))
            {
                if (!await Longtail.DownsyncFromS3Bucket(applicationConfiguration, config.BlockStorageURI, folder, PackageNameToURI(config.VersionIndexStorageURI, package)))
                {
                    Console.WriteLine("Untar operation failed.");
                    return 1;
                }
            }
            else
            {
                if (!await Longtail.DownsyncFromLocalStore(config.BlockStorageURI, folder, PackageNameToURI(config.VersionIndexStorageURI, package)))
                {
                    Console.WriteLine("Untar operation failed.");
                    return 1;
                }
            }

            return 0;
        }

        private static async Task<int> DownloadPackage(string folder, string package)
        {
            Initialize(out Config config, out ApplicationConfiguration applicationConfiguration);

            return await DownsyncWithAuthentication(config, applicationConfiguration, folder, package);
        }

        public struct UE4Version
        {
            public string BuildId;
        }

        public static UE4Version ReadUE4Version(string versionFile)
        {
            JsonSerializer jsonSerializer = new JsonSerializer();

            try
            {
                using (StreamReader streamReader = new StreamReader(versionFile))
                using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
                {
                    return jsonSerializer.Deserialize<UE4Version>(jsonTextReader);
                }
            }
            catch (System.IO.FileNotFoundException)
            {
                // credentials file does not exist
                return default(UE4Version);
            }
        }

        public static void WriteUE4Version(UE4Version version, string versionFile)
        {
            JsonSerializer jsonSerializer = new JsonSerializer();

            using (StreamWriter streamWriter = new StreamWriter(versionFile))
            using (JsonTextWriter jsonTextWriter = new JsonTextWriter(streamWriter))
            {
                jsonSerializer.Serialize(jsonTextWriter, version);
            }
        }

        private static async Task<int> UpdateLocalUE4Version()
        {
            Initialize(out Config config, out ApplicationConfiguration applicationConfiguration);

            const string installedUE4VersionFile = "InstalledUE4Version.json";
            const string desiredUE4VersionFile = "DesiredUE4Version.json";

            UE4Version installedUE4Version = ReadUE4Version(installedUE4VersionFile);
            UE4Version desiredUE4Version = ReadUE4Version(desiredUE4VersionFile);

            if (installedUE4Version.BuildId != desiredUE4Version.BuildId)
            {
                Console.WriteLine($"Installing UE4 version {desiredUE4Version.BuildId}...");

                int result = await DownsyncWithAuthentication(config, applicationConfiguration, Path.GetFullPath(config.UE4Folder), desiredUE4Version.BuildId);

                if (result != 0)
                {
                    Console.WriteLine("Download failed.");
                    return result;
                }
                else
                {
                    WriteUE4Version(desiredUE4Version, installedUE4VersionFile);
                    Console.WriteLine($"UE4 version {desiredUE4Version.BuildId} has been downloaded");

                    result = await Prerequisites.RunPrerequisitesInstaller(config.UE4Folder);
                    return result;
                }
            }
            else
            {
                Console.WriteLine($"UE4 version {desiredUE4Version.BuildId} is already installed");
                return 0;
            }
        }

        private static async Task<int> RunPrerequisitesInstaller()
        {
            Initialize(out Config config, out ApplicationConfiguration applicationConfiguration);
            int result = await Prerequisites.RunPrerequisitesInstaller(config.UE4Folder);
            return result;
        }

        private static void ClearGoogleUserAuth()
        {
            GoogleOAuthFlow.RemoveApplicationDefaultCredentials(ApplicationDefaultCredentialsFile);
        }

        private static async Task CreateGoogleUserAuth()
        {
            Initialize(out _, out ApplicationConfiguration applicationConfiguration);

            await GoogleOAuthFlow.CreateUserApplicationDefaultCredentials(applicationConfiguration);
        }
    }
}
