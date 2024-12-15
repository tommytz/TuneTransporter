namespace TuneTransporter;

public class FileRenameService(IList<TagLib.File> taggedFiles)
{
    private IList<TagLib.File> _taggedFiles = taggedFiles;

    public IList<string> RenameFiles()
    {
        IList<string> renamedFiles = new List<string>();
        
        foreach (var file in _taggedFiles)
        {
            var source = new FileInfo(file.Name);
    
            if (!source.Exists)
            {
                Console.WriteLine("File {0} does not exist.", source.Name);
                continue;
            }
    
            var destination = Path.Combine(source.DirectoryName, FormatTitle(file));
    
            if (source.FullName == destination)
            {
                continue;
            }
            Console.WriteLine("Renaming '{0}' to '{1}'\n", source.FullName, destination);
            source.MoveTo(destination);
            
            renamedFiles.Add(destination);
        }
        
        return renamedFiles;
    }
    
    private string FormatTitle(TagLib.File file)
    {
        string formattedName;
        
        // Multi-Disc
        if (file.Tag.DiscCount > 1)
        {
            // {Album Title}{ (Album Disambiguation)}/{medium:0}{track:00} - {Track Title}
            formattedName = $"{file.Tag.Disc}{file.Tag.Track:D2} - {file.Tag.Title}{Path.GetExtension(file.Name)}";
        }
        else
        {
            // {Album Title}{ (Album Disambiguation)}/{track:00} - {Track Title}
            formattedName = $"{file.Tag.Track:D2} - {file.Tag.Title}{Path.GetExtension(file.Name)}";
        }
        
        if (formattedName.Contains(Path.DirectorySeparatorChar))
        {
            return formattedName.Replace(Path.DirectorySeparatorChar, '+');
        }
        
        return formattedName;
    }
}