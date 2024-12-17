namespace TuneTransporter;

public class DirectoryService(IPathHelper pathHelper, string[] extensions)
{
    public bool MoveFiles(IEnumerable<FileTransfer> fileTransfers)
    {
        foreach (var transfer in fileTransfers)
        {
            var targetPath = pathHelper.BuildDestinationPath(transfer.TrackFile);
            var artistDirectory = pathHelper.ArtistDirectory(transfer.TrackFile.Artist);
            var albumDirectory = pathHelper.AlbumDirectory(transfer.TrackFile.Artist, transfer.TrackFile.Album);
            
            // Check file to transfer does exist
            if (!File.Exists(transfer.PreviousPath))
            {
                Console.WriteLine("File does not exist.");
                break;
            }

            // Check current path and target path are not the same
            if (transfer.PreviousPath == targetPath)
            {
                Console.WriteLine("Source and destination are the same.");
                break;
            }

            // Check there isn't already a file at that location
            if (File.Exists(targetPath))
            {
                Console.WriteLine("File already exists at destination.");
                break;
            }
            
            // Cancel if any files fail either of these conditions
          
            // Check artist directory exists
            if (!Directory.Exists(artistDirectory))
            {
                // Create it if not
                Directory.CreateDirectory(artistDirectory);
            }
            
            // Check album directory exists
            if (!Directory.Exists(albumDirectory))
            {
                // Create it if not
                Directory.CreateDirectory(albumDirectory);
            }
            
            // Move file to new location
            File.Move(transfer.PreviousPath, targetPath);
            Console.WriteLine("\"{0}\" moved to \"{1}\"", transfer.PreviousPath, targetPath);
        }

        return true;
    }

    public void CleanUp(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }
        
        var files = Directory.GetFiles(directoryPath);

        if (files.Any(f => extensions.Contains(Path.GetExtension(f))))
        {
            Console.WriteLine("Cleanup aborted! There are still audio files in the directory.");
        }
        
        Directory.Delete(directoryPath, true);
    }
}