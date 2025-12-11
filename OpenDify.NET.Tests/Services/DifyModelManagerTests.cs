using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using OpenDify.NET.Configuration;
using OpenDify.NET.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OpenDify.NET.Tests.Services
{
    public class DifyModelManagerTests
    {
        private readonly Mock<ILogger<DifyModelManager>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly DifySettings _settings;
        private readonly DifyModelManager _modelManager;

        public DifyModelManagerTests()
        {
            _mockLogger = new Mock<ILogger<DifyModelManager>>();
            
            // Create HttpMessageHandler mock for HttpClient
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            
            _settings = new DifySettings
            {
                ApiBase = "https://api.dify.ai/v1",
                ApiKeys = new List<string> { "test-api-key-1", "test-api-key-2" },
                ConversationMemoryMode = 1
            };

            _modelManager = new DifyModelManager(_mockLogger.Object, _httpClient, _settings);
        }

        [Fact]
        public void GetApiKey_WithValidModel_ReturnsApiKey()
        {
            // Arrange
            var modelName = "测试functioncall"; // This is set up in InitializeTestData

            // Act
            var result = _modelManager.GetApiKey(modelName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test-api-key-1", result);
        }

        [Fact]
        public void GetApiKey_WithInvalidModel_ReturnsNull()
        {
            // Arrange
            var modelName = "invalid-model";

            // Act
            var result = _modelManager.GetApiKey(modelName);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetModelName_WithValidApiKey_ReturnsModelName()
        {
            // Arrange
            var apiKey = "test-api-key-1";

            // Act
            var result = _modelManager.GetModelName(apiKey);

            // Assert
            Assert.Equal("测试functioncall", result);
        }

        [Fact]
        public void GetModelName_WithInvalidApiKey_ReturnsNull()
        {
            // Arrange
            var apiKey = "invalid-api-key";

            // Act
            var result = _modelManager.GetModelName(apiKey);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetAvailableModels_ReturnsModelList()
        {
            // Act
            var result = _modelManager.GetAvailableModels();

            // Assert
            Assert.NotEmpty(result);
            Assert.Contains(result, m => m.Id == "测试functioncall");
        }

        [Fact]
        public void GetApiKeys_ReturnsApiKeyList()
        {
            // Act
            var result = _modelManager.GetApiKeys();

            // Assert
            Assert.NotEmpty(result);
            Assert.Equal(2, result.Count);
            Assert.Contains("test-api-key-1", result);
            Assert.Contains("test-api-key-2", result);
        }

        [Fact]
        public void IsModelSupported_WithValidModel_ReturnsTrue()
        {
            // Arrange
            var modelName = "测试functioncall";

            // Act
            var result = _modelManager.IsModelSupported(modelName);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsModelSupported_WithInvalidModel_ReturnsFalse()
        {
            // Arrange
            var modelName = "invalid-model";

            // Act
            var result = _modelManager.IsModelSupported(modelName);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetSupportedModelNames_ReturnsModelNameList()
        {
            // Act
            var result = _modelManager.GetSupportedModelNames();

            // Assert
            Assert.NotEmpty(result);
            Assert.Contains("测试functioncall", result);
        }

        [Fact]
        public async Task RefreshModelInfoAsync_UpdatesModelMappings()
        {
            // Arrange - Mock HTTP responses for API calls
            var mockResponse1 = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"name\":\"test-app-1\"}", System.Text.Encoding.UTF8, "application/json")
            };
            
            var mockResponse2 = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"name\":\"test-app-2\"}", System.Text.Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(mockResponse1);

            // Act
            await _modelManager.RefreshModelInfoAsync();

            // Assert
            var models = _modelManager.GetSupportedModelNames();
            Assert.NotEmpty(models);
            Assert.Contains("test-app-1", models);
        }
    }
}