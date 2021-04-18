
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using static DistributionTools.JsonHelpers;

namespace DistributionTools
{
    public static class Longtail
    {
        [JsonConverter(typeof(ObjectToStringConverter<BlockStorageURI>))]
        public struct BlockStorageURI
        {
            private string URI;

            public BlockStorageURI(string uri)
            {
                URI = uri;
            }

            public static explicit operator string(BlockStorageURI blockStorageURI)
            {
                return blockStorageURI.URI;
            }

            public override string ToString()
            {
                return URI;
            }
        }

        [JsonConverter(typeof(ObjectToStringConverter<VersionIndexStorageURI>))]
        public struct VersionIndexStorageURI
        {
            private string URI;

            public VersionIndexStorageURI(string uri)
            {
                URI = uri;
            }

            public static explicit operator string(VersionIndexStorageURI versionIndexStorageURI)
            {
                return versionIndexStorageURI.URI;
            }

            public override string ToString()
            {
                return URI;
            }
        }

        public struct VersionIndexURI
        {
            private string URI;

            public VersionIndexURI(string uri)
            {
                URI = uri;
            }

            public static explicit operator string(VersionIndexURI versionIndexURI)
            {
                return versionIndexURI.URI;
            }

            public override string ToString()
            {
                return URI;
            }
        }

        public enum StorageProtocol
        { 
            None,
            Local,
            Google,
            S3
        }

        private static async Task<bool> RunLongtailCommand(ApplicationConfiguration applicationConfiguration, StorageProtocol protocol, string[] args)
        {
            string longtailAppName = "longtail.exe";

            string arguments = string.Join(" ", args);

            ProcessStartInfo startInfo = new ProcessStartInfo { FileName = longtailAppName, Arguments = arguments, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };


            switch (protocol)
            {
                case StorageProtocol.Google:
                    if (!string.IsNullOrEmpty((string)applicationConfiguration.ApplicationDefaultCredentialsFile))
                        startInfo.EnvironmentVariables["GOOGLE_APPLICATION_CREDENTIALS"] = (string)applicationConfiguration.ApplicationDefaultCredentialsFile;
                    break;
                case StorageProtocol.S3:
                    if (!string.IsNullOrEmpty(applicationConfiguration.EndpointOverride))
                    {
                        startInfo.EnvironmentVariables["AWS_ENDPOINT_OVERRIDE"] = applicationConfiguration.EndpointOverride;
                        Console.WriteLine("AWS_ENDPOINT_OVERRIDE: {0}", applicationConfiguration.EndpointOverride);
                    }
                    if (!string.IsNullOrEmpty(applicationConfiguration.RegionOverride))
                    {
                        startInfo.EnvironmentVariables["AWS_REGION"] = applicationConfiguration.RegionOverride;
                        Console.WriteLine("AWS_REGION: {0}", applicationConfiguration.RegionOverride);
                    }
                    startInfo.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = (string)applicationConfiguration.ClientID;
                    startInfo.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = (string)applicationConfiguration.ClientSecret;
                    //Console.WriteLine("Key: {0}\nSecret: {1}", startInfo.EnvironmentVariables["AWS_ACCESS_KEY_ID"], startInfo.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"]);
                    break;
                case StorageProtocol.Local:
                    break;
                case StorageProtocol.None:
                default:
                    throw new NotSupportedException("Unsupported protocol");
            }

            Console.WriteLine($"Running command: {startInfo.FileName} {startInfo.Arguments}...");

            System.Diagnostics.Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();
            ProcessAsyncHelper.Result result = await ProcessAsyncHelper.RunAsync(startInfo, (s) => Console.WriteLine(s), (s) => Console.WriteLine(s));
            stopwatch.Stop();

            Console.WriteLine("Elapsed time: {0}s", (float)stopwatch.ElapsedMilliseconds / 1000.0f);

            return result.ExitCode == 0;
        }

        public static async Task<bool> UpsyncToGSBucket(ApplicationConfiguration applicationOAuthConfiguration, BlockStorageURI blockStorageURI, string localPath, VersionIndexURI versionIndexURI)
        {
            return await RunLongtailCommand(applicationOAuthConfiguration, StorageProtocol.Google, new string[] { "upsync", "--source-path", $"\"{localPath}\"", "--target-path", $"\"{(string)versionIndexURI}\"", "--storage-uri", $"\"{(string)blockStorageURI}\"" });
        }

        public static async Task<bool> DownsyncFromGSBucket(ApplicationConfiguration applicationOAuthConfiguration, BlockStorageURI blockStorageURI, string localPath, VersionIndexURI versionIndexURI)
        {
            return await RunLongtailCommand(applicationOAuthConfiguration, StorageProtocol.Google, new string[] { "downsync", "--source-path", $"\"{(string)versionIndexURI}\"", "--target-path", $"\"{localPath}\"", "--storage-uri", $"\"{(string)blockStorageURI}\"" });
        }

        public static async Task<bool> DownsyncFromS3Bucket(ApplicationConfiguration applicationConfiguration, BlockStorageURI blockStorageURI, string localPath, VersionIndexURI versionIndexURI)
        {
            return await RunLongtailCommand(applicationConfiguration, StorageProtocol.S3, new string[] { "downsync", "--source-path", $"\"{(string)versionIndexURI}\"", "--target-path", $"\"{localPath}\"", "--storage-uri", $"\"{(string)blockStorageURI}\"" });
        }

        public static async Task<bool> UpsyncToS3Bucket(ApplicationConfiguration applicationConfiguration, BlockStorageURI blockStorageURI, string localPath, VersionIndexURI versionIndexURI)
        {
            return await RunLongtailCommand(applicationConfiguration, StorageProtocol.S3, new string[] { "upsync", "--source-path", $"\"{localPath}\"", "--target-path", $"\"{(string)versionIndexURI}\"", "--storage-uri", $"\"{(string)blockStorageURI}\"" });
        }

        public static async Task<bool> UpsyncToLocalStore(BlockStorageURI blockStorageURI, string localPath, VersionIndexURI versionIndexURI)
        {
            return await RunLongtailCommand(null, StorageProtocol.Local, new string[] { "upsync", "--source-path", $"\"{localPath}\"", "--target-path", $"\"{(string)versionIndexURI}\"", "--storage-uri", $"\"{(string)blockStorageURI}\"" });
        }

        public static async Task<bool> DownsyncFromLocalStore(BlockStorageURI blockStorageURI, string localPath, VersionIndexURI versionIndexURI)
        {
            return await RunLongtailCommand(null, StorageProtocol.Local, new string[] { "downsync", "--source-path", $"\"{(string)versionIndexURI}\"", "--target-path", $"\"{localPath}\"", "--storage-uri", $"\"{(string)blockStorageURI}\"" });
        }
    }
}
