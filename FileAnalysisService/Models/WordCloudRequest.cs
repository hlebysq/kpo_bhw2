namespace FileAnalysisService.Models;

public class WordCloudRequest
{
    public string Format { get; set; } = "png";
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
    public string Text { get; set; }
    public string[] RemoveStopwords { get; set; } = { "english" };
    public int MaxWords { get; set; } = 100;
    
    public string Case { get; set; } = "lower";
    public string[] Colors { get; set; } = { "#1f77b4", "#ff7f0e", "#2ca02c" };
    public string BackgroundColor { get; set; } = "#ffffff";
    public int Padding { get; set; } = 10;
}