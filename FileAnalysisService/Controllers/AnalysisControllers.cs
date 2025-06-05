using FileAnalysisService.Data;
using FileAnalysisService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FileAnalysisService.Controllers;

[ApiController]
[Route("api/analysis")]
public class AnalysisController : ControllerBase
{
    private readonly AnalysisDbContext _context;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        AnalysisDbContext context,
        IConfiguration config,
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory,
        ILogger<AnalysisController> logger)
    {
        _context = context;
        _config = config;
        _env = env;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("/word-cloud/{fileId}")]
    public async Task<IActionResult> ReturnCloud(int fileId)
    {
        try
        {
            _logger.LogInformation($"Starting finding cloud for file ID: {fileId}");

            var fileMetadata = await GetFileMetadata(fileId);
            if (fileMetadata == null)
            {
                _logger.LogWarning($"File metadata not found for ID: {fileId}");
                return NotFound("File not found");
            }

            _logger.LogInformation($"File metadata received: {fileMetadata.Name}, Hash: {fileMetadata.HashCode}");
            _logger.LogInformation($"Checking for existing clouds of hash: {fileMetadata.HashCode}");

            var existingCloud = await _context.AnalysisResults
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.OriginalFileHash == fileMetadata.HashCode);
            if (existingCloud != null)
            {
                _logger.LogInformation(
                    $"Found existing analysis ID: {existingCloud.Id} for file hash: {fileMetadata.HashCode}");
                return await GetCloudFile(existingCloud.CloudFileId);
            }

            _logger.LogInformation("File does not found.");
            return NotFound("File with this if does not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error returning of cloud file ID: {fileId}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    private string GetUploadPath()
    {
        var uploadPath = _config["FileStorage:UploadPath"]!;
        return Path.IsPathRooted(uploadPath) 
            ? uploadPath 
            : Path.Combine(_env.ContentRootPath, uploadPath);
    }
    
    [HttpGet("{fileId}")]
    public async Task<IActionResult> AnalyzeFile(int fileId)
    {
        try
        {
            _logger.LogInformation($"Starting analysis for file ID: {fileId}");
            
            var fileMetadata = await GetFileMetadata(fileId);
            if (fileMetadata == null)
            {
                _logger.LogWarning($"File metadata not found for ID: {fileId}");
                return NotFound("File not found");
            }

            _logger.LogInformation($"File metadata received: {fileMetadata.Name}, Hash: {fileMetadata.HashCode}");

            _logger.LogInformation($"Checking for existing analysis of hash: {fileMetadata.HashCode}");
            
            var existingAnalysis = await _context.AnalysisResults
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.OriginalFileHash == fileMetadata.HashCode);

            if (existingAnalysis != null)
            {
                _logger.LogInformation(
                    $"Found existing analysis ID: {existingAnalysis.Id} for file hash: {fileMetadata.HashCode}");
                return await GetAnalysisResultFile(existingAnalysis.ResultFileId);
            }

            _logger.LogInformation($"No existing analysis found, proceeding with new analysis");

            _logger.LogInformation($"Downloading file content for ID: {fileId}");
            var fileContent = await DownloadFile(fileId);
            if (fileContent == null)
            {
                _logger.LogError($"File content not found for ID: {fileId}");
                return NotFound("File content not available");
            }

            _logger.LogInformation($"Analyzing content for file ID: {fileId}");
            var analysisResult = AnalyzeContent(fileContent);
            var cloudResult = await GenerateWordCloudAsync(Encoding.UTF8.GetString(fileContent));
            
            if (cloudResult == null || cloudResult.Length == 0)
            {
                _logger.LogError("Word cloud generation failed");
                return StatusCode(500, "Word cloud generation failed");
            }
            
            _logger.LogInformation($"Saving analysis results for file ID: {fileId}");
            
            var (fileRecord, analysisRecord) = await SaveAnalysisResults(
                fileMetadata.Name, 
                fileMetadata.HashCode, 
                cloudResult,
                analysisResult);

            return await GetAnalysisResultFile(fileRecord.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error analyzing file ID: {fileId}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    private async Task<FileMetadataDto?> GetFileMetadata(int fileId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("FileStorage");
            var response = await client.GetAsync($"/api/files/metadata/{fileId}");
        
            _logger.LogInformation($"Metadata request status: {response.StatusCode}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"File metadata not found for ID: {fileId}");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Error getting metadata: {response.StatusCode}, Content: {errorContent}");
                throw new HttpRequestException($"Error getting metadata: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"Received metadata: {content}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<FileMetadataDto>(content, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetFileMetadata");
            throw;
        }
    }
    
    private async Task<byte[]?> DownloadFile(int fileId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("FileStorage");
            var response = await client.GetAsync($"/api/files/{fileId}");

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file");
            return null;
        }
    }
    private async Task<byte[]?> GenerateWordCloudAsync(string? text)
    {
        var requestData = new WordCloudRequest
        {
            Text = text ?? "",
            MaxWords = 1000,
            Width = 1200,
            Height = 800
        };
        var client = _httpClientFactory.CreateClient("FileStorage");
        var url = "https://quickchart.io/wordcloud";
        
        var response = await client.PostAsJsonAsync(url, requestData);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"WordCloud API error: {response.StatusCode}, {errorContent}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }
    private async Task SaveWordCloudImage(byte[] imageBytes, string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory) )
            {
                Directory.CreateDirectory(directory);
            }
            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
            _logger.LogInformation($"Word cloud saved to: {filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error saving word cloud to {filePath}");
            throw;
        }
    }

    private static string AnalyzeContent(byte[] fileContent)
    {
        var content = Encoding.UTF8.GetString(fileContent);
        var wordCount = content.Split(new[] { ' ', '\t', '\n', '\r' }, 
                                StringSplitOptions.RemoveEmptyEntries).Length;

        return $"Word count: {wordCount}\n" +
               $"Character count: {content.Length}\n" +
               $"Lines: {content.Count(c => c == '\n') + 1}\n" +
               "You can find Word Cloud of this file in /analysis/word-cloud/{id}";
    }

    private async Task<(FileRecord, AnalysisRecord)> SaveAnalysisResults(
        string originalFileName,
        string originalFileHash,
        byte[] cloudResult,
        string analysisResult)
    {
        var fileName = $"analysis_{originalFileHash}_{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
        var uploadPath = GetUploadPath();
        Directory.CreateDirectory(uploadPath);
        var filePath = Path.Combine(uploadPath, fileName);

        await System.IO.File.WriteAllTextAsync(filePath, analysisResult);
        
        var hash = ComputeSha256(filePath);
        var cloudFileName = $"word-cloud_{originalFileHash}_{DateTime.UtcNow:yyyyMMddHHmmss}.png";
        var cloudPath = Path.Combine(uploadPath, cloudFileName);
        await SaveWordCloudImage(cloudResult, cloudPath);
        
        var analysisFileRecord = new FileRecord
        {
            Name = fileName,
            Location = filePath,
            HashCode = hash
        };
        var cloudFileRecord = new FileRecord
        {
            Name = cloudFileName,
            Location = cloudPath,
            HashCode = hash
        };

        _context.FileRecords.Add(analysisFileRecord);
        _context.FileRecords.Add(cloudFileRecord);
        await _context.SaveChangesAsync();

        var analysisRecord = new AnalysisRecord
        {
            OriginalFileHash = originalFileHash,
            AnalysisResult = analysisResult,
            ResultFileId = analysisFileRecord.Id,
            CloudFileId = cloudFileRecord.Id
        };

        _context.AnalysisResults.Add(analysisRecord);
        await _context.SaveChangesAsync();

        return (analysisFileRecord, analysisRecord);
    }

    private async Task<IActionResult> GetAnalysisResultFile(int fileId)
    {
        var fileRecord = await _context.FileRecords.FindAsync(fileId);
        if (fileRecord == null) 
            return NotFound("Analysis result file not found");

        if (!System.IO.File.Exists(fileRecord.Location))
        {
            _logger.LogError($"Analysis file missing: {fileRecord.Location}");
            return NotFound("Analysis file not found on disk");
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(fileRecord.Location);
        return File(bytes, "text/plain", fileRecord.Name);
    }
    
    private async Task<IActionResult> GetCloudFile(int fileId)
    {
        var fileRecord = await _context.FileRecords.FindAsync(fileId);
        if (fileRecord == null) 
            return NotFound("Analysis result file not found");

        if (!System.IO.File.Exists(fileRecord.Location))
        {
            _logger.LogError($"Cloud file missing: {fileRecord.Location}");
            return NotFound("Cloud file not found on disk");
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(fileRecord.Location);
        return File(bytes, "image/png", fileRecord.Name);
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = System.IO.File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}

public record FileMetadataDto(int Id, string Name, string HashCode);