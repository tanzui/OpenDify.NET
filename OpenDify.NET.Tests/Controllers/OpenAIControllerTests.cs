using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using OpenDify.NET.Configuration;
using OpenDify.NET.Controllers;
using OpenDify.NET.Models;
using OpenDify.NET.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OpenDify.NET.Tests.Controllers
{
    public class OpenAIControllerTests
    {
        private readonly Mock<ILogger<OpenAIController>> _mockLogger;
        private readonly Mock<DifyModelManager> _mockModelManager;
        private readonly Mock<RequestTransformationService> _mockRequestTransformation;
        private readonly Mock<ResponseTransformationService> _mockResponseTransformation;
        private readonly Mock<FileUploadService> _mockFileUploadService;
        private readonly Mock<StreamingService> _mockStreamingService;
        private readonly HttpClient _httpClient;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly AppSettings _settings;
        private readonly OpenAIController _controller;

        public OpenAIControllerTests()
        {
            _mockLogger = new Mock<ILogger<OpenAIController>>();
            
            // Create HttpMessageHandler mock for HttpClient
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            
            _settings = new AppSettings
            {
                Dify = new DifySettings
                {
                    ApiBase = "https://api.dify.ai/v1",
                    ApiKeys = new List<string> { "test-api-key" }
                },
                OpenAI = new OpenAISettings
                {
                    ValidApiKeys = new List<string> { "valid-api-key" }
                }
            };

            // Create mocks with proper constructor parameters
            var mockModelLogger = new Mock<ILogger<DifyModelManager>>();
            _mockModelManager = new Mock<DifyModelManager>(
                mockModelLogger.Object,
                _httpClient,
                _settings.Dify);

            var mockMemoryLogger = new Mock<ILogger<ConversationMemoryManager>>();
            var mockMemoryManager = new Mock<ConversationMemoryManager>(
                mockMemoryLogger.Object,
                _settings.Dify);

            var mockRequestLogger = new Mock<ILogger<RequestTransformationService>>();
            _mockRequestTransformation = new Mock<RequestTransformationService>(
                mockRequestLogger.Object,
                mockMemoryManager.Object);

            var mockResponseLogger = new Mock<ILogger<ResponseTransformationService>>();
            _mockResponseTransformation = new Mock<ResponseTransformationService>(
                mockResponseLogger.Object,
                mockMemoryManager.Object);

            var mockFileUploadLogger = new Mock<ILogger<FileUploadService>>();
            _mockFileUploadService = new Mock<FileUploadService>(
                mockFileUploadLogger.Object,
                _httpClient,
                _settings.Dify);

            var mockStreamingLogger = new Mock<ILogger<StreamingService>>();
            _mockStreamingService = new Mock<StreamingService>(
                mockStreamingLogger.Object,
                _mockResponseTransformation.Object,
                mockMemoryManager.Object);

            _controller = new OpenAIController(
                _mockLogger.Object,
                _mockModelManager.Object,
                _mockRequestTransformation.Object,
                _mockResponseTransformation.Object,
                _mockFileUploadService.Object,
                _mockStreamingService.Object,
                _httpClient,
                _settings);
        }

        [Fact]
        public async Task ListModels_ReturnsOkResult()
        {
            // Arrange
            var expectedModels = new List<OpenAIModel>
            {
                new() { Id = "测试functioncall", Object = "model", Created = 1234567890, OwnedBy = "dify" }
            };

            _mockModelManager.Setup(x => x.RefreshModelInfoAsync()).Returns(Task.CompletedTask);
            _mockModelManager.Setup(x => x.GetAvailableModels()).Returns(expectedModels);
            _mockResponseTransformation.Setup(x => x.CreateModelsResponse(expectedModels))
                .Returns(new OpenAIModelsResponse
                {
                    Object = "list",
                    Data = expectedModels
                });

            // Act
            var result = await _controller.ListModels();

            // Assert
            var okResult = Assert.IsType<ActionResult<OpenAIModelsResponse>>(result);
            var models = Assert.IsType<OpenAIModelsResponse>(okResult.Value);
            Assert.Equal("list", models.Object);
            Assert.Single(models.Data);
        }

        [Fact]
        public async Task ChatCompletions_WithValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new OpenAIChatCompletionRequest
            {
                Model = "测试functioncall",
                Messages = new List<OpenAIMessage>
                {
                    new() { Role = "user", Content = "Hello" }
                },
                Stream = false
            };

            var difyRequest = new DifyChatMessageRequest
            {
                Query = "Hello",
                User = "default_user"
            };

            var difyResponse = new DifyChatMessageResponse
            {
                Answer = "Hi there!",
                ConversationId = "test-conversation-id"
            };

            var openaiResponse = new OpenAIChatCompletionResponse
            {
                Id = "test-id",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<OpenAIChoice>
                {
                    new()
                    {
                        Index = 0,
                        Message = new OpenAIMessage { Role = "assistant", Content = "Hi there!" },
                        FinishReason = "stop"
                    }
                }
            };

            _mockModelManager.Setup(x => x.GetApiKey(request.Model)).Returns("test-api-key");
            _mockRequestTransformation.Setup(x => x.TransformChatCompletionRequestAsync(request))
                .ReturnsAsync((difyRequest, new List<UploadedImage>()));
            _mockResponseTransformation.Setup(x => x.TransformChatCompletionResponse(difyResponse, request.Model))
                .Returns(openaiResponse);

            // Setup HTTP client mock
            var mockHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"answer\":\"Hi there!\",\"conversation_id\":\"test-conversation-id\"}")
            };
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(mockHttpResponse);

            // Act
            var result = await _controller.ChatCompletions(request);

            // Assert
            var okResult = Assert.IsType<ActionResult<OpenAIChatCompletionResponse>>(result);
            var response = Assert.IsType<OpenAIChatCompletionResponse>(okResult.Value);
            Assert.Equal("Hi there!", response.Choices[0].Message.Content);
        }

        [Fact]
        public async Task ChatCompletions_WithStreamingRequest_ReturnsStreamingResult()
        {
            // Arrange
            var request = new OpenAIChatCompletionRequest
            {
                Model = "测试functioncall",
                Messages = new List<OpenAIMessage>
                {
                    new() { Role = "user", Content = "Hello" }
                },
                Stream = true
            };

            var difyRequest = new DifyChatMessageRequest
            {
                Query = "Hello",
                User = "default_user"
            };

            _mockModelManager.Setup(x => x.GetApiKey(request.Model)).Returns("test-api-key");
            _mockRequestTransformation.Setup(x => x.TransformChatCompletionRequestAsync(request))
                .ReturnsAsync((difyRequest, new List<UploadedImage>()));

            // Setup HTTP client mock for streaming
            var mockHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("data: {\"answer\":\"Hi\"}\n\ndata: {\"answer\":\" there!\"}\n\ndata: [DONE]\n")
            };
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(mockHttpResponse);

            // Act
            var result = await _controller.ChatCompletions(request);

            // Assert
            Assert.IsType<StreamingActionResult>(result);
        }

        [Fact]
        public async Task ChatCompletions_WithInvalidModel_ReturnsNotFound()
        {
            // Arrange
            var request = new OpenAIChatCompletionRequest
            {
                Model = "invalid-model",
                Messages = new List<OpenAIMessage>
                {
                    new() { Role = "user", Content = "Hello" }
                }
            };

            _mockModelManager.Setup(x => x.GetApiKey(request.Model)).Returns((string?)null);
            _mockModelManager.Setup(x => x.GetSupportedModelNames()).Returns(new List<string> { "测试functioncall" });
            _mockResponseTransformation.Setup(x => x.CreateErrorResponse(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Model not found",
                        Type = "model_not_found"
                    }
                });

            // Act
            var result = await _controller.ChatCompletions(request);

            // Assert
            var notFoundResult = Assert.IsType<ActionResult<OpenAIErrorResponse>>(result);
            var error = Assert.IsType<OpenAIErrorResponse>(notFoundResult.Value);
            Assert.Equal("model_not_found", error.Error.Type);
        }

        [Fact]
        public void HealthCheck_ReturnsOkResult()
        {
            // Act
            var result = _controller.HealthCheck();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var health = okResult.Value;
            var statusProp = health.GetType().GetProperty("status");
            Assert.Equal("healthy", statusProp?.GetValue(health));
        }
    }
}