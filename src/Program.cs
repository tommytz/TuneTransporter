namespace TuneTransporter;

using Microsoft.Extensions.Configuration;
using DirectorySelector;

public static class Program
{
    public static void Main(string[] args)
    {
        // Read config values into memory. Consider using IOptions if it makes sense?
        // Read environment variables if they are provided?
        // env vars > config
        IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        
        // e.g. [".flac", ".wav"]
        var audioFileTypes = config.GetSection("audioFileTypes").Get<string[]>();
        if (audioFileTypes == null || audioFileTypes.Length == 0)
        {
            throw new Exception("Audio file types not set");
        }

        var downloadsPath = config.GetValue<string>("downloadsPath");
        var musicPath = config.GetValue<string>("musicPath");

        if (string.IsNullOrEmpty(downloadsPath) || string.IsNullOrEmpty(musicPath))
        {
            throw new Exception("Downloads path or music path not set");
        }
        
        // Read event JSON or start interactive CLI session
        var directorySelector = new DirectorySelectorFactory(args, downloadsPath).Create();
        var directoryPath = directorySelector.GetTargetDirectory();
        
        // Scan album directory files
        // Add a check here that directory path does actually exist since the files could have been moved before the event was processed
        // Add a check here that there are any audio files/any files at all before moving to renaming, etc
        // Get metadata of all the audio files and prepare them for file transfer
        List<string> filePaths = Directory.EnumerateFiles(directoryPath).ToList();
        IList<TrackInfo> trackList = new List<TrackInfo>();
        
        try
        {
            trackList = filePaths
                .Where(f => audioFileTypes.Contains(Path.GetExtension(f)))
                .Select(f => new TrackInfo(f))
                .OrderBy(t => t.Track)
                .ToList();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Unable to parse metadata from audio files: {0}", e.Message);
            Environment.Exit(1);
        }

        if (!trackList.Any())
        {
            Console.Error.WriteLine("No audio files found in directory");
            Environment.Exit(1);
        }
        
        var pathHelper = new PathHelper(musicPath);
        
        var fileTransfers = new List<FileTransfer>();

        foreach (var trackFile in trackList)
        {
            var previousPath = trackFile.FullName;
            trackFile.Name = pathHelper.FormatName(trackFile);
            
            var transfer = new FileTransfer(trackFile, previousPath);
            fileTransfers.Add(transfer);
        }
        
        // Attempt to transfer files, and bail out if anything isn't right
        var directoryService = new DirectoryService(pathHelper, audioFileTypes);
        
        var transferResult = directoryService.MoveFiles(fileTransfers);
        
        // Cleanup the directory left behind if the files have moved
        if (transferResult)
        {
            Console.WriteLine("Files transferred successfully. Cleaning up directory...");
            directoryService.CleanUp(directoryPath);
        }
        else
        {
            Console.WriteLine("File transfer failed.");
            Environment.Exit(1);
        }
    }
}
