using FileStoringService.Data;
using FileStoringService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FileStoringService.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        AppDbContext context,
        IConfiguration config,
        IWebHostEnvironment env,
        ILogger<FilesController> logger)
    {
        _context = context;
        _config = config;
        _env = env;
        _logger = logger;
    }

    [HttpPost("upload")]
         public async Task<IActionResult> Upload(IFormFile file)
         {
             if (file == null || file.Length == 0)
                 return BadRequest("Invalid file");
     
             var tempFilePath = Path.GetTempFileName();
             await using (var tempStream = System.IO.File.Create(tempFilePath))
             {
                 await file.CopyToAsync(tempStream);
             }
     
             var hash = await ComputeSha256Async(tempFilePath);
             
             var existingRecord = await _context.Files
                 .FirstOrDefaultAsync(f => f.HashCode == hash);
             
             if (existingRecord != null)
             {
                 _logger.LogInformation($"File with hash {hash} already exists. ID: {existingRecord.Id}");
                 System.IO.File.Delete(tempFilePath); 
                 return Ok(new UploadResponse(existingRecord.Id, hash));
             }
     
             var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
             var uploadPath = Path.Combine(_env.ContentRootPath, _config["FileStorage:UploadPath"]!);
             Directory.CreateDirectory(uploadPath);
             var finalFilePath = Path.Combine(uploadPath, fileName);
     
             System.IO.File.Move(tempFilePath, finalFilePath);
     
             var newRecord = new FileRecord
             {
                 Name = file.FileName,
                 Location = finalFilePath,
                 HashCode = hash
             };
             
             _context.Files.Add(newRecord);
             await _context.SaveChangesAsync();
             
             return Ok(new UploadResponse(newRecord.Id, hash));
         }
     
         [HttpGet("{id}")]
         public async Task<IActionResult> Download(int id)
         {
             var record = await _context.Files.FindAsync(id);
             if (record == null) return NotFound();
             
             if (!System.IO.File.Exists(record.Location))
             {
                 _logger.LogError($"File not found at path: {record.Location}");
                 return NotFound("Physical file not found");
             }
             
             var bytes = await System.IO.File.ReadAllBytesAsync(record.Location);
             return File(bytes, "text/plain", record.Name);
         }
     
         private static async Task<string> ComputeSha256Async(string filePath)
         {
             await using var stream = System.IO.File.OpenRead(filePath);
             using var sha256 = SHA256.Create();
             var hashBytes = await sha256.ComputeHashAsync(stream);
             return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
         }
         
         [HttpGet("metadata/{id}")]
         public async Task<IActionResult> GetMetadata(int id)
         {
             var record = await _context.Files.FindAsync(id);
             if (record == null) return NotFound();
         
             return Ok(new FileMetadataDto(
                 record.Id,
                 record.Name,
                 record.HashCode
             ));
         }

    public record FileMetadataDto(int Id, string Name, string HashCode);
}