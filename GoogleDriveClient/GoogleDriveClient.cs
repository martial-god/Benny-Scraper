
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace GoogleDriveClient
{
    /// <summary>
    /// Setup for file can be found here https://developers.google.com/api-client-library/dotnet/guide/aaa_oauth
    /// Grab the client secret from https://console.cloud.google.com/apis/credentials?project=novel-scraper
    /// </summary>
    public class GoogleDriveClient
    {
        private static readonly string[] Scopes = { DriveService.Scope.Drive };
        private static readonly string ApplicationName = "My Project";
        // CurrentDirectory is the directory where the .exe is located, which is why we step back
        private static readonly string ClientSecretPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\client_access\client_secret.json");

        internal DriveService _service;

        public GoogleDriveClient()
        {
            // Load the client secrets from the JSON file. Will automatically dispose once done due to "using"
            using (var stream = new FileStream(ClientSecretPath, FileMode.Open, FileAccess.Read))
            {
                var credPath = "token.json";
                var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets, // get the client_secret from the JSON file
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result; // save the response in a file in the bin folder
                Console.WriteLine("Credential file saved to: " + credPath);

                // Create the Drive service.
                _service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });
            }
        }
    }
}
