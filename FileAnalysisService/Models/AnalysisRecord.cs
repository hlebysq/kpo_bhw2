namespace FileAnalysisService.Models;

public class AnalysisRecord
{
    public int Id { get; set; }
    public required string OriginalFileHash { get; set; }
    public required string AnalysisResult { get; set; }
    public int ResultFileId { get; set; }
    public int CloudFileId { get; set; }
    public virtual FileRecord? ResultFile { get; set; }
}

public class FileRecord
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Location { get; set; }
    public required string HashCode { get; set; }
}