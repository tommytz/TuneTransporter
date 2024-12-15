namespace TuneTransporter;

public class DirectoryMoveService(string directoryPath, string musicPath, IList<TagLib.File> taggedFiles)
{
    public void MoveDirectory()
    {
        // Create artist directory if it doesn't exist
        string artist = taggedFiles.First().Tag.FirstAlbumArtist;
        string album = taggedFiles.First().Tag.Album;

        var sourceDirectory = new DirectoryInfo(directoryPath);
        if (!sourceDirectory.Exists)
        {
            Console.WriteLine("Directory does not exist.");
        }

        var artistDirectory = new DirectoryInfo($"{musicPath}/{artist}");
        if (!artistDirectory.Exists)
        {
            Console.WriteLine("Artist directory does not exist. Creating it...");
            artistDirectory.Create();
        }
        
        // If directory already exists Directory.Move will throw an error since
        // by default it tries to create the destination directory. We can get around
        // this by instead iterating through the directory contents and moving individual
        // files. Another alternative is to change the target directory name, e.g. "Album (soulseek download)"
        var albumDirectory = Path.Combine(artistDirectory.FullName, album);
        sourceDirectory.MoveTo(albumDirectory);
        Console.WriteLine("Directory moved to {0}", albumDirectory);
    }
}