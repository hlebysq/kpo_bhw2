using FileStoringService.Controllers;
using FileStoringService.Data;
using FileStoringService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using FluentAssertions;

namespace FileStoringService.Tests
{
    public class FilesControllerTests : IDisposable
    {
        private readonly FilesController _controller;
        private readonly AppDbContext _context;
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly string _testUploadFolder = "file-uploads-test";
        private readonly string _testUploadPath;
        public FilesControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "FileStoringTestDb")
                .Options;
            
            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();

            _mockEnv = new Mock<IWebHostEnvironment>();
            _mockEnv.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
            
            _mockConfig = new Mock<IConfiguration>();
            _mockConfig.Setup(c => c["FileStorage:UploadPath"]).Returns(_testUploadPath);
            _mockConfig.Setup(c => c["FileStorage:MaxFileSizeMB"]).Returns("100");
            _testUploadPath = Path.Combine(Path.GetTempPath(), _testUploadFolder);
            _mockConfig.Setup(c => c["FileStorage:UploadPath"]).Returns(_testUploadFolder);
            var mockLogger = new Mock<ILogger<FilesController>>();

            _controller = new FilesController(
                _context, 
                _mockConfig.Object, 
                _mockEnv.Object, 
                mockLogger.Object);
            
            if (Directory.Exists(_testUploadPath))
                Directory.Delete(_testUploadPath, true);
            Directory.CreateDirectory(_testUploadPath);
        }

        [Fact]
        public async Task Upload_ValidFile_ReturnsOkWithId()
        {
            // Arrange
            var content = "Test file content";
            var fileName = "test.txt";
            var file = CreateTestFile(content, fileName);

            // Act
            var result = await _controller.Upload(file);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Value.Should().NotBeNull();
            dynamic response = okResult.Value;
            int id = response.Id;
            id.Should().BeGreaterThan(0);
            
            var record = await _context.Files.FindAsync(id);
            record.Should().NotBeNull();
            record.Name.Should().Be(fileName);
            
            var files = Directory.GetFiles(_testUploadPath, $"*_{fileName}");
            files.Length.Should().Be(1);
            (await File.ReadAllTextAsync(files[0])).Should().Be(content);
        }

        [Fact]
        public async Task Upload_DuplicateFile_ReturnsSameId()
        {
            // Arrange
            var content = "Duplicate content";
            var file1 = CreateTestFile(content, "file1.txt");
            var file2 = CreateTestFile(content, "file2.txt");

            var result1 = await _controller.Upload(file1) as OkObjectResult;
            dynamic response1 = result1.Value;
            int originalId = response1.Id;

            // Act
            var result2 = await _controller.Upload(file2) as OkObjectResult;
            dynamic response2 = result2.Value;
            int newId = response2.Id;

            // Assert
            newId.Should().Be(originalId);
            (await _context.Files.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task Download_ExistingFile_ReturnsFileContent()
        {
            // Arrange
            var content = "Download test content";
            var fileName = "download.txt";
            var file = CreateTestFile(content, fileName);
            var uploadResult = await _controller.Upload(file) as OkObjectResult;
            dynamic uploadResponse = uploadResult.Value;
            int fileId = uploadResponse.Id;
            
            // Act
            var result = await _controller.Download(fileId);

            // Assert
            result.Should().BeOfType<FileContentResult>();
            var fileResult = result as FileContentResult;
            fileResult.ContentType.Should().Be("text/plain");
            fileResult.FileDownloadName.Should().Be(fileName);
            Encoding.UTF8.GetString(fileResult.FileContents).Should().Be(content);
        }

        [Fact]
        public async Task Download_NonExistentFile_ReturnsNotFound()
        {
            // Act
            var result = await _controller.Download(999);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        private IFormFile CreateTestFile(string content, string fileName)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            return new FormFile(stream, 0, stream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };
        }
        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
    
            if (Directory.Exists(_testUploadPath)) 
                Directory.Delete(_testUploadPath, true);
        }
    }
}