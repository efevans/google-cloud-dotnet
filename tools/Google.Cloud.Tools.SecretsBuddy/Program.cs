// Copyright 2025 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License"):
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Cloud.SecretManager.V1;
using System.Reflection;

namespace Google.Cloud.Tools.SecretsBuddy;

public class Program
{
    private const string SecretsDirectoryName = "secret_manager";
    private const string CloudProjectName = "cloud-sharp-jenkins";
    private const string MDSUrl = "http://169.254.169.254/computeMetadata/v1";

    // Mapping of the profile, which is passed in as the program's singular argument,
    // to the secret keys it retrieves
    private static readonly Dictionary<string, List<string>> s_profileSecrets = new()
    {
        ["GOOGLE_APIS_NUGET"] = ["google-apis-nuget-api-key"],
        ["GOOGLE_CLOUD_NUGET"] = ["google-cloud-nuget-api-key"],
        ["GOOGLE_API_DOTNET_CLIENT_GITHUB"] = [
            "google-api-dotnet-client-github-user-name",
            "google-api-dotnet-client-github-user-email",
            "google-api-dotnet-client-github-token"
        ]
    };

#if DEBUG
    private static string KokoroGFileDirectory => "";
#else
    private const string KokoroGFileDirectoryEnvVar = "KOKORO_GFILE_DIR";
    private static string KokoroGFileDirectory
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable(KokoroGFileDirectoryEnvVar);
            if (string.IsNullOrEmpty(dir))
            {
                Console.WriteLine($"Error: {KokoroGFileDirectoryEnvVar} environment variable is not set.");
                Environment.Exit(1);
            }
            return dir;
        }
    }
#endif

    private static string SecretsDirectoryPath => Path.Combine(KokoroGFileDirectory, SecretsDirectoryName);

    // Lazy initializer to defer client creation (and credential detection) until after
    // PingMDSAsync has run. Eager static initialization can crash the application if MDS
    // is not yet ready to respond to credential requests when the class is loaded.
    private static readonly Lazy<SecretManagerServiceClient> s_lazyClient = new(() => SecretManagerServiceClient.Create());
    private static SecretManagerServiceClient SecretManagerServiceClient => s_lazyClient.Value;

    public static async Task Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine($"{Assembly.GetExecutingAssembly().GetName().Name} must be called with exactly 1 argument");
            Environment.Exit(1);
        }
        Options options = ParseArguments(args);

        if (!s_profileSecrets.TryGetValue(options.Profile, out var secretKeys))
        {
            Console.WriteLine($"Profile '{options.Profile}' is not recognized.");
            Environment.Exit(1);
            return;
        }
        await PopulateSecrets(secretKeys, CloudProjectName);
    }

    private static Options ParseArguments(string[] args) => new(args[0]);

    private static async Task PopulateSecrets(List<string> secretKeys, string project)
    {
        // We need to make sure the metadata server is up, so ping it
        await PingMDSAsync();

        foreach (var key in secretKeys)
        {
            var payload = await GetSecretPayload(project, key);
            WriteSecretToFile(key, payload);
        }
    }

    // Get the secret payload for this key
    private static async Task<string> GetSecretPayload(string project, string key)
    {
        Console.WriteLine($"Retrieving secret payload: {key}");

        string fullSecretPath = $"projects/{project}/secrets/{key}/versions/latest";
        AccessSecretVersionRequest request = new AccessSecretVersionRequest()
        {
            Name = fullSecretPath,
        };

        try
        {
            var response = await SecretManagerServiceClient.AccessSecretVersionAsync(request);
            return response.Payload.Data.ToStringUtf8();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting secret payload: {key}: {ex.Message}");
            Environment.Exit(1);
            // Need this return here to avoid compilation errors
            return null;
        }
    }

    // Write this secret to disk
    private static void WriteSecretToFile(string key, string payload)
    {
        // Create directory if needed
        if (!Directory.Exists(SecretsDirectoryPath))
        {
            var directory = Directory.CreateDirectory(SecretsDirectoryPath);
            Console.WriteLine($"Created directory on disk at: {directory.FullName}");
        }

        string path = Path.Combine(SecretsDirectoryPath, key);
        File.WriteAllText(path, payload);
    }

    private static async Task PingMDSAsync()
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Metadata-Flavor", "Google");

        for (int i = 0; i < 10; i++)
        {
            try
            {
                var response = await client.GetAsync(MDSUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Silently catch this exception, we just want to stupidly
                // try 10 times to wake the server up
            }
            await Task.Delay(1000);
        }
    }

    public class Options
    {
        public Options(string profile) => Profile = profile;

        public string Profile { get; }
    }
}
