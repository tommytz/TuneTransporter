namespace TuneTransporter;

using System.Text;
using System.Text.Json;
using Serilog;
using Slskd;

// TODO: Make the stdinput stream reading more robust
//  - Add a timeout so that it doesn't hang forever and can exit if waiting for too long
//  - Maybe put the stream reading and the json deserialization together in some way so they're out of Main
//  - That way the Main program only cares about the output (the slskd event object)

// TODO: Read environment variables into configuration instead of from a config file
//  - This allows the slskd container to use env vars in the docker compose for the downloads and music path
//  - Also allows it to be different outside of the container

public static class Program
{
    private const string DownloadsPath = "/home/thomas/.local/share/slskd/downloads";
    private const string MusicPath = "/home/thomas/.local/share/slskd/music";
    private static readonly string[] FileExtensions = [".flac", ".wav", ".mp3", ".m4a"];

    public static void Main()
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("/home/thomas/slskd_integrations/logs/tune_transporter.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        logger.Information("Starting Tune Transporter...");
        
        Event? eventData = null;
        
        var stdin = ReadFromStdin(logger, 10000);

        if (string.IsNullOrEmpty(stdin))
        {
            logger.Warning("No arguments provided. Exiting.");
            Environment.Exit(1);
        }
        
        try
        {
            var json = stdin;
            logger.Information("Read event from stdin: {Json}", json);
            
            eventData = JsonSerializer.Deserialize<Event>(json);
            logger.Information("Parsed event from JSON: {Event}", eventData);
        }
        catch (Exception e)
        {
            logger.Error(e, "Error parsing event from stdin");
        }
            
        if (eventData == null)
        {
            logger.Warning("Event data is null. It may have been empty or not in the expected format.");
            Environment.Exit(1);
        }
            
        var directoryName = eventData.Name;

        var directoryPath = Path.Combine(DownloadsPath, directoryName);
        
        if (!Directory.Exists(directoryPath))
        {
            logger.Warning("Directory does not exist: {DirectoryPath}", directoryPath);
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
            logger.Error(e, "Error parsing metadata from audio files");
            Environment.Exit(1);
        }

        if (!trackList.Any())
        {
            logger.Warning("No audio files found in directory: {DirectoryPath}", directoryPath);
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
        var directoryService = new DirectoryService(logger, pathHelper, FileExtensions);
        
        var transferResult = directoryService.MoveFiles(fileTransfers);
        
        // Cleanup the directory left behind if the files have moved
        if (transferResult)
        {
            logger.Information("File transfer successful. Cleaning up directory...");
            directoryService.CleanUp(directoryPath);
        }
        else
        {
            logger.Warning("File transfer failed.");
            Environment.Exit(1);
        }
    }
    
    private static string ReadFromStdin(ILogger logger, int timeoutMilliseconds = 5000)
    {
        var input = new StringBuilder();
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Task to handle the timeout
        var timeoutTask = Task.Delay(timeoutMilliseconds, token);

        // Task to read from stdin
        var readTask = Task.Run(() =>
        {
            try
            {
                using (var sr = new StreamReader(Console.OpenStandardInput()))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        // If "exit" is encountered, stop reading
                        if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
                        {
                            logger.Information("Received 'exit' signal, terminating stdin reading");
                            break;
                        }

                        line = line.Trim();
                        if (string.IsNullOrEmpty(line))
                        {
                            logger.Debug("Skipped empty line from stdin");
                            continue;
                        }

                        input.AppendLine(line);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error while reading from stdin");
                throw; // Handle any read error
            }
        });

        // Wait for either the timeout or stdin input to complete
        var completedTask = Task.WhenAny(readTask, timeoutTask).Result;

        if (completedTask == timeoutTask)
        {
            // Timeout occurred before any input was received
            logger.Warning("Timeout reached while waiting for input.");
            return string.Empty; // Or handle timeout as necessary (e.g., throw exception or return a default value)
        }

        // Wait for the read task to complete if the timeout was not triggered
        readTask.Wait();

        // Return the collected input
        logger.Information("Successfully read input from stdin, length: {Length} characters", input.Length);
        return input.ToString();
    }
}
