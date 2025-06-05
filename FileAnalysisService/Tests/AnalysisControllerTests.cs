using FileAnalysisService.Controllers;
using FileAnalysisService.Data;
using FileAnalysisService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace FileAnalysisService.Tests
{
    public class AnalysisControllerTests : IDisposable
    {
        private readonly AnalysisController _controller;
        private readonly AnalysisDbContext _context;
        private readonly MockHttpMessageHandler _mockHttpHandler;
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly string _testAnalysisFolder = "analysis-results-test";
        private readonly string _testAnalysisPath;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;

        public AnalysisControllerTests()
        {
            _testAnalysisPath = Path.Combine(Path.GetTempPath(), _testAnalysisFolder);

            var options = new DbContextOptionsBuilder<AnalysisDbContext>()
                .UseInMemoryDatabase(databaseName: "AnalysisTestDb")
                .Options;

            _context = new AnalysisDbContext(options);
            _context.Database.EnsureCreated();

            _mockHttpHandler = new MockHttpMessageHandler();

            _httpClient = new HttpClient(_mockHttpHandler)
            {
                BaseAddress = new Uri("http://test-service")
            };

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            _httpClientFactory = mockHttpClientFactory.Object;

            _mockEnv = new Mock<IWebHostEnvironment>();
            _mockEnv.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

            _mockConfig = new Mock<IConfiguration>();
            _mockConfig.Setup(c => c["FileStorage:UploadPath"]).Returns(_testAnalysisPath);
            _mockConfig.Setup(c => c["Services:FileStoringService"]).Returns("http://test-service");

            var mockLogger = new Mock<ILogger<AnalysisController>>();

            _controller = new AnalysisController(
                _context,
                _mockConfig.Object,
                _mockEnv.Object,
                _httpClientFactory,
                mockLogger.Object);

            if (Directory.Exists(_testAnalysisPath))
                Directory.Delete(_testAnalysisPath, true);

            Directory.CreateDirectory(_testAnalysisPath);
        }

        [Fact]
        public async Task AnalyzeFile_NewFile_ReturnsTextFile()
        {
            // Arrange
            const int fileId = 1;
            const string fileContent = "This is a test content for analysis";
            SetupFileStoringMocks(fileId, "test.txt", fileContent);

            // Act
            var result = await _controller.AnalyzeFile(fileId);

            // Assert
            result.Should().BeOfType<FileContentResult>();
            var fileResult = result as FileContentResult;
            fileResult.ContentType.Should().Be("text/plain");
            fileResult.FileContents.Should().NotBeEmpty();
            
            var analysisRecord = await _context.AnalysisResults.FirstOrDefaultAsync();
            analysisRecord.Should().NotBeNull();
            analysisRecord.OriginalFileHash.Should().Be("testhash");
        }

        [Fact]
        public async Task AnalyzeFile_CachedAnalysis_ReturnsSameResult()
        {
            // Arrange
            const int fileId = 2;
            const string fileContent = "Cached analysis test";
            SetupFileStoringMocks(fileId, "cached.txt", fileContent);
            var firstResult = await _controller.AnalyzeFile(fileId) as FileContentResult;
            firstResult.Should().NotBeNull();
            var firstBytes = firstResult.FileContents;

            // Act 
            var secondResult = await _controller.AnalyzeFile(fileId) as FileContentResult;
            secondResult.Should().NotBeNull();
            var secondBytes = secondResult.FileContents;

            // Assert
            secondBytes.Should().Equal(firstBytes);
            (await _context.AnalysisResults.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task AnalyzeFile_FileNotFound_ReturnsNotFound()
        {
            // Arrange
            _mockHttpHandler
                .When(HttpMethod.Get, "http://test-service/api/files/metadata/999")
                .Respond(HttpStatusCode.NotFound);

            // Act
            var result = await _controller.AnalyzeFile(999);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        private void SetupFileStoringMocks(int fileId, string fileName, string fileContent)
        {
            var metadataResponse = $"{{\"id\":{fileId},\"name\":\"{fileName}\",\"hashCode\":\"testhash\"}}";
            _mockHttpHandler
                .When(HttpMethod.Get, $"http://test-service/api/files/metadata/{fileId}")
                .Respond("application/json", metadataResponse);

            _mockHttpHandler
                .When(HttpMethod.Get, $"http://test-service/api/files/{fileId}")
                .Respond("text/plain", fileContent);

            _mockHttpHandler
                .When(HttpMethod.Post, "https://quickchart.io/wordcloud")
                .Respond(req =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(Encoding.UTF8.GetBytes("fakeimage"))
                    };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    return response;
                });
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();

            if (Directory.Exists(_testAnalysisPath))
                Directory.Delete(_testAnalysisPath, true);
        }
    }
}
