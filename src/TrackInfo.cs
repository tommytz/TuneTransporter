namespace TuneTransporter;

public class TrackInfo
{
    private readonly string _directory;
    
    public TrackInfo(string file)
    {
        var metadata = TagLib.File.Create(file);
        
        _directory = Path.GetDirectoryName(metadata.Name)
                     ?? throw new InvalidOperationException($"Could not determine directory for file: {metadata.Name}");

        Name = Path.GetFileName(metadata.Name);
        Title = metadata.Tag.Title;
        Artist = metadata.Tag.FirstAlbumArtist;
        Album = metadata.Tag.Album;
        Track = Convert.ToInt32(metadata.Tag.Track);
        Disc = Convert.ToInt32(metadata.Tag.Disc);
        DiscCount = Convert.ToInt32(metadata.Tag.DiscCount);
    }
    
    public string Name { get; set; }
    public string FullName => Path.Combine(_directory, Name);
    public string Title { get; }
    public string Artist { get; }
    public string Album { get; }
    public int Track { get; }
    public int Disc { get; }
    public int DiscCount { get; }
}