namespace TestGenerator.Core.FileUtils;

internal readonly struct FileWriteItem
{
    public string FilePath { get; }
    public string Content { get; }

    public FileWriteItem(string filePath, string content)
    {
        FilePath = filePath;
        Content = content;
    }
}