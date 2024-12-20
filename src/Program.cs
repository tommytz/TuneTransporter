using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

namespace TuneTransporter;

using Slskd;

// TODO: Make the stdinput stream reading more robust
//  - Add a timeout so that it doesn't hang forever and can exit if waiting for too long
//  - Maybe put the stream reading and the json deserialization together in some way so they're out of Main
//  - That way the Main program only cares about the output (the slskd event object)

// TODO: Read environment variables into configuration instead of from a config file
//  - This allows the slskd container to use env vars in the docker compose for the downloads and music path
//  - Also allows it to be different outside of the container

// TODO: Accept cli args as well as stdin stream so it can be run outside of slskd events
//  - This would allow it to be run outside of the container as well
//  - It would check if there are any cli args, and if not read from stdin

public static class Program
{
    private static readonly string[] FileExtensions = [".flac", ".wav", ".mp3", ".m4a"];

    public static void Main()
    {
        IConfiguration config = new ConfigurationBuilder().AddEnvironmentVariables(prefix: "TUNE_TRANSPORTER_").Build();
        
        var downloadsPath = config["DOWNLOADS_PATH"];
        var musicPath = config["MUSIC_PATH"];
        var logPath = config["LOG_PATH"];

        if (string.IsNullOrEmpty(logPath))
        {
            logPath = Path.Combine(Directory.GetCurrentDirectory(), "tune-transporter.log");
        }
        
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(NewLogger(logPath)));
        Microsoft.Extensions.Logging.ILogger logger = loggerFactory.CreateLogger<Serilog.ILogger>();
        
        logger.LogInformation("Starting Tune Transporter...");

        if (string.IsNullOrEmpty(downloadsPath) || string.IsNullOrEmpty(musicPath))
        {
            logger.LogWarning("DOWNLOADS_PATH or MUSIC_PATH environment variables are not set. Exiting.");
            Environment.Exit(1);
        }
        
        logger.LogInformation("LOG_PATH: {LogPath}", logPath);
        logger.LogInformation("DOWNLOADS_PATH: {DownloadsPath}", downloadsPath);
        logger.LogInformation("MUSIC_PATH: {MusicPath}", musicPath);
        
        Event? eventData = null;
        
        var stdin = ReadFromStdin(logger);

        if (string.IsNullOrEmpty(stdin))
        {
            logger.LogWarning("No arguments provided. Exiting.");
            Environment.Exit(1);
        }
        
        try
        {
            var json = stdin;
            logger.LogInformation("Read event from stdin: {Json}", json);
            
            eventData = JsonSerializer.Deserialize<Event>(json);
            logger.LogInformation("Parsed event from JSON: {Event}", eventData);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error parsing event from stdin");
        }
            
        if (eventData == null)
        {
            logger.LogWarning("Event data is null. It may have been empty or not in the expected format.");
            Environment.Exit(1);
        }
            
        var directoryName = eventData.Name;

        var directoryPath = Path.Combine(downloadsPath, directoryName);
        
        if (!Directory.Exists(directoryPath))
        {
            logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
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
            logger.LogError(e, "Error parsing metadata from audio files");
            Environment.Exit(1);
        }

        if (!trackList.Any())
        {
            logger.LogWarning("No audio files found in directory: {DirectoryPath}", directoryPath);
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
        var directoryService = new DirectoryService(logger, pathHelper, FileExtensions);
        
        var transferResult = directoryService.MoveFiles(fileTransfers);
        
        // Cleanup the directory left behind if the files have moved
        if (transferResult)
        {
            logger.LogInformation("File transfer successful. Cleaning up directory...");
            directoryService.CleanUp(directoryPath);
        }
        else
        {
            logger.LogWarning("File transfer failed.");
            Environment.Exit(1);
        }
    }
    
    private static string ReadFromStdin(Microsoft.Extensions.Logging.ILogger logger, int timeoutMilliseconds = 5000)
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
                            logger.LogInformation("Received 'exit' signal, terminating stdin reading");
                            break;
                        }

                        line = line.Trim();
                        if (string.IsNullOrEmpty(line))
                        {
                            logger.LogDebug("Skipped empty line from stdin");
                            continue;
                        }

                        input.AppendLine(line);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while reading from stdin");
                throw; // Handle any read error
            }
        });

        // Wait for either the timeout or stdin input to complete
        var completedTask = Task.WhenAny(readTask, timeoutTask).Result;

        if (completedTask == timeoutTask)
        {
            // Timeout occurred before any input was received
            logger.LogWarning("Timeout reached while waiting for input.");
            return string.Empty; // Or handle timeout as necessary (e.g., throw exception or return a default value)
        }

        // Wait for the read task to complete if the timeout was not triggered
        readTask.Wait();

        // Return the collected input
        logger.LogInformation("Successfully read input from stdin, length: {Length} characters", input.Length);
        return input.ToString();
    }

    private static Serilog.ILogger NewLogger(string path)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(path, rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }
}
