namespace TuneTransporter.SlskdEvent;

public class SlskdEvent
{
    public required string Type { get; set; }
    public required int Version { get; set; }
    public required string LocalDirectoryName { get; set; }
    public required string RemoteDirectoryName { get; set; }
    public required string Username { get; set; }
    public required Guid Id { get; set; }
    public required DateTime Timestamp { get; set; }
    
    public string Name => Path.GetFileName(LocalDirectoryName);
}