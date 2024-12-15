namespace TuneTransporter;

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SlskdEvent;

public static class Program
{
    static string[] audioFileTypes = [".flac", ".wav", ".mp3"];
    
    public static void Main(string[] args)
    {
        // Read env vars to build file paths for IO operations
        IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        string downloadsPath = Path.Combine(config["basePath"], config["downloads"]);
        string musicPath = Path.Combine(config["basePath"], config["music"]);
        
        // Read event JSON into memory
        string jsonString = File.ReadAllText("event_test.json");
        
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var eventJson = new SlskdEventParser(options).ParseEvent(jsonString);
        
        // Scan album directory files
        var directoryPath = Path.Combine(downloadsPath, eventJson.Name);
        // Add a check here that directory path does actually exist since the files could have been moved before the event was processed
        List<string> files = Directory.EnumerateFiles(directoryPath).ToList();
        // Add a check here that there are any audio files/any files at all before moving to renaming, etc
        
        // Get metadata of all the audio files and rename them using it
        var audioFiles = files.Where(f => audioFileTypes.Contains(Path.GetExtension(f))).ToList();
        var taggedFiles = audioFiles.Select((file) => TagLib.File.Create(file)).ToList().OrderBy(f => f.Tag.Track).ToList();
        var renamedFiles = new FileRenameService(taggedFiles).RenameFiles();
        
        // Move the album directory to the music directory
        new DirectoryMoveService(directoryPath, musicPath, taggedFiles).MoveDirectory();
    }
}

// Check if all the songs have the same artist and album
// if (!AudioFile.AllSameArtist(audioFiles))
// {
//     Console.WriteLine("The audio files in this directory do not all have the same artist.");
//     return;
// }
//
// if (!AudioFile.AllSameAlbum(audioFiles))
// {
//     Console.WriteLine("The audio files in this directory do not all have the same album.");
//     return;
// }

// public static bool AllSameArtist(IEnumerable<AudioFile> audioFiles)
// {
//     return audioFiles.Select(af => af.Artist).Distinct().Count() == 1;
// }
//
// public static bool AllSameAlbum(IEnumerable<AudioFile> audioFiles)
// {
//     return audioFiles.Select(af => af.Album).Distinct().Count() == 1;
// }

// var soulseekDirs = Directory.EnumerateFileSystemEntries(downloadsPath).ToList();
// Console.WriteLine("Soulseek directory contents:\n");
// foreach (var item in soulseekDirs.Select((v, i) => new { Value = v, Index = i }))
// {
//     Console.WriteLine("({0}) {1}", item.Index, Path.GetFileName(item.Value));
// }
//
// Console.WriteLine("\nChoose a directory or ENTER to exit:");
// var input = Console.ReadKey();
//
// while (!char.IsDigit(input.KeyChar) && input.Key != ConsoleKey.Enter)
// {
//     Console.WriteLine("Invalid input. Please enter a number or ENTER to exit.");
//     input = Console.ReadKey();
// }
//
// if (input.Key == ConsoleKey.Enter)
// {
//     Console.WriteLine("Exiting...");
//     return;
// }
//
// var index = int.Parse(input.KeyChar.ToString());
// var directoryPath = soulseekDirs[index];