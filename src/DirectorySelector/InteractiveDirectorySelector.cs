namespace TuneTransporter.DirectorySelector;

public class InteractiveDirectorySelector : IDirectorySelector
{
    private readonly string _downloadsPath;

    public InteractiveDirectorySelector(string downloadsPath)
    {
        _downloadsPath = downloadsPath;
    }

    public string GetTargetDirectory()
    {
        var downloads = Directory.EnumerateDirectories(_downloadsPath).ToList();
        var indexedDownloads = downloads.Select((v, i) => new { Value = v, Index = i });
        
        foreach (var item in indexedDownloads)
        {
            Console.WriteLine("({0}) {1}", item.Index, Path.GetFileName(item.Value));
        }
        
        Console.WriteLine("\nChoose a directory or ENTER to exit:");
        var input = Console.ReadKey();
        
        while (!char.IsDigit(input.KeyChar) && input.Key != ConsoleKey.Enter)
        {
            Console.WriteLine("Invalid input. Please enter a number or ENTER to exit.");
            input = Console.ReadKey();
        }

        if (input.Key == ConsoleKey.Enter)
        {
            Console.WriteLine("Exiting...");
            Environment.Exit(0);
        }
        
        var index = int.Parse(input.KeyChar.ToString());
        var directoryPath = downloads.ElementAt(index);
        return directoryPath;
    }
}