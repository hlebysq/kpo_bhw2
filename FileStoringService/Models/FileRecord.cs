namespace FileStoringService.Models;

public class FileRecord
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Location { get; set; }
    public required string HashCode { get; set; }
}