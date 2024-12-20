namespace TuneTransporter;

using System.Text;
using System.Text.Json;
using Serilog;
using Slskd;

public static class Program
{
    private const string DownloadsPath = "/home/thomas/.local/share/slskd/downloads";
    private const string MusicPath = "/home/thomas/.local/share/slskd/music";
    private static readonly string[] FileExtensions = [".flac", ".wav", ".mp3", ".m4a"];

    public static void Main()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("/home/thomas/slskd_integrations/logs/tune_transporter.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        Log.Information("Starting Tune Transporter...");
        
        Event? eventData = null;
        
        var stdin = ReadFromStdin();

        if (string.IsNullOrEmpty(stdin))
        {
            Log.Warning("No arguments provided. Exiting.");
            Environment.Exit(1);
        }
        
        try
        {
            var json = stdin;
            Log.Information("Read event from stdin: {Json}", json);
            
            eventData = JsonSerializer.Deserialize<Event>(json);
            Log.Information("Parsed event from JSON: {Event}", eventData);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error parsing event from stdin");
        }
            
        if (eventData == null)
        {
            Log.Warning("Event data is null. It may have been empty or not in the expected format.");
            Environment.Exit(1);
        }
            
        var directoryName = eventData.Name;

        var directoryPath = Path.Combine(DownloadsPath, directoryName);
        
        if (!Directory.Exists(directoryPath))
        {
            Log.Warning("Directory does not exist: {DirectoryPath}", directoryPath);
            Environment.Exit(1);
        }
        
        List<string> filePaths = Directory.EnumerateFiles(directoryPath).ToList();
        IList<TrackInfo> trackList = new List<TrackInfo>();
        
        try
        {
            trackList = filePaths
                .Where(f => FileExtensions.Contains(Path.GetExtension(f)))
                .Select(f => new TrackInfo(f))
                .OrderBy(t => t.Track)
                .ToList();
        }
        catch (Exception e)
        {
            Log.Error(e, "Error parsing metadata from audio files");
            Environment.Exit(1);
        }

        if (!trackList.Any())
        {
            Log.Warning("No audio files found in directory: {DirectoryPath}", directoryPath);
            Environment.Exit(1);
        }
        
        var pathHelper = new PathHelper(MusicPath);
        
        var fileTransfers = new List<FileTransfer>();

        foreach (var trackFile in trackList)
        {
            var previousPath = trackFile.FullName;
            trackFile.Name = pathHelper.FormatName(trackFile);
            
            var transfer = new FileTransfer(trackFile, previousPath);
            fileTransfers.Add(transfer);
        }
        
        // Attempt to transfer files, and bail out if anything isn't right
        var directoryService = new DirectoryService(pathHelper, FileExtensions);
        
        var transferResult = directoryService.MoveFiles(fileTransfers);
        
        // Cleanup the directory left behind if the files have moved
        if (transferResult)
        {
            Log.Information("File transfer successful. Cleaning up directory...");
            directoryService.CleanUp(directoryPath);
        }
        else
        {
            Log.Warning("File transfer failed.");
            Environment.Exit(1);
        }
    }

    private static string ReadFromStdin()
    {
        var input = new StringBuilder();

        try
        {
            using (var sr = new StreamReader(Console.OpenStandardInput()))
            {
                string line;
                while ((line = sr.ReadLine()) != null)  // Read input line by line until EOF
                {
                    // Trim any leading/trailing whitespace to avoid accidental empty lines
                    line = line.Trim();

                    // Skip empty lines or lines that only contain whitespace
                    if (string.IsNullOrEmpty(line)) { continue; }

                    // If the line is "exit", stop reading but don't append it to input
                    if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Information("Received 'exit' signal, terminating stdin reading");
                        break;  // Stop reading, but don't append "exit" to the result
                    }

                    // Append valid input to our StringBuilder
                    input.AppendLine(line);
                }
            }

            // Return the complete input as a single string
            Log.Information("Successfully read input from stdin, length: {Length} characters", input.Length);

            return input.ToString();
        }
        catch (IOException ioEx)
        {
            Log.Error(ioEx, "IOException while reading from stdin");
            throw;  // Re-throw or handle as necessary
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error while reading from stdin");
            throw;  // Re-throw or handle as necessary
        }
    }
}
