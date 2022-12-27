namespace GoogleDriveClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            // Create a new GoogleDriveClient instance will need user verification if first time
            var client = new GoogleDriveClient();

            // List files in root folder
            var request = client._service.Files.List();
            request.Q = "mimeType='application/vnd.google-apps.folder' and trashed = false";
            request.Fields = "nextPageToken, files(id, name, mimeType)";
            request.Spaces = "drive";
            var result = request.Execute();
            foreach (var file in result.Files)
            {
                Console.WriteLine($"{file.Name} ({file.MimeType})");
            }

        }
    }
}