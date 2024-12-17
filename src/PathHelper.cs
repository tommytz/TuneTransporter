namespace TuneTransporter;

public interface IPathHelper
{
    string FormatName(TrackInfo trackFile);
    string BuildDestinationPath(TrackInfo trackFile);
    string ArtistDirectory(string artist);
    string AlbumDirectory(string artist, string album);
}

public class PathHelper : IPathHelper
{
    private readonly string _baseDirectoryPath;
    private readonly char _directorySeparatorReplacement = '+';
    
    public PathHelper(string baseDirectoryPath)
    {
        _baseDirectoryPath = baseDirectoryPath;
    }

    public string FormatName(TrackInfo trackFile)
    {
        string formattedName;
        
        // Multi-Disc
        if (trackFile.DiscCount > 1)
        {
            // {Album Title}{ (Album Disambiguation)}/{medium:0}{track:00} - {Track Title}
            formattedName = $"{trackFile.Disc}{trackFile.Track:D2} - {trackFile.Title}{Path.GetExtension(trackFile.Name)}";
        }
        else
        {
            // {Album Title}{ (Album Disambiguation)}/{track:00} - {Track Title}
            formattedName = $"{trackFile.Track:D2} - {trackFile.Title}{Path.GetExtension(trackFile.Name)}";
        }
        
        return formattedName.Replace(Path.DirectorySeparatorChar, '+');
    }

    public string BuildDestinationPath(TrackInfo trackFile)
    {
        return Path.Combine(AlbumDirectory(trackFile.Artist, trackFile.Album), trackFile.Name);
    }

    public string ArtistDirectory(string artist)
    {
        return Path.Combine(_baseDirectoryPath, ReplaceSeparator(artist));
    }

    public string AlbumDirectory(string artist, string album)
    {
        return Path.Combine(ArtistDirectory(artist), ReplaceSeparator(album));
    }

    private string ReplaceSeparator(string str)
    {
        return str.Replace(Path.DirectorySeparatorChar, _directorySeparatorReplacement);
    }
}