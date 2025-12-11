using Microsoft.Extensions.Logging;
using Moq;
using OpenDify.NET.Configuration;
using OpenDify.NET.Models;
using OpenDify.NET.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDify.NET.Tests.Services
{
    public class ConversationMemoryManagerTests
    {
        private readonly Mock<ILogger<ConversationMemoryManager>> _mockLogger;
        private readonly DifySettings _settings;
        private readonly ConversationMemoryManager _memoryManager;

        public ConversationMemoryManagerTests()
        {
            _mockLogger = new Mock<ILogger<ConversationMemoryManager>>();
            _settings = new DifySettings
            {
                ApiBase = "http://localhost:8186/v1",
                ApiKeys = new List<string> { "test-api-key" },
                ConversationMemoryMode = 1
            };
            _memoryManager = new ConversationMemoryManager(_mockLogger.Object, _settings);
        }

        [Fact]
        public void ProcessMessageHistory_WithHistoryMessageMode_ReturnsQueryAndNullConversationId()
        {
            // Arrange
            _settings.ConversationMemoryMode = 1; // history_message mode
            var messages = new List<OpenAIMessage>
            {
                new() { Role = "system", Content = "You are a helpful assistant" },
                new() { Role = "user", Content = "Hello" },
                new() { Role = "assistant", Content = "Hi there!" },
                new() { Role = "user", Content = "How are you?" }
            };

            // Act
            var result = _memoryManager.ProcessMessageHistory(messages);

            // Assert
            Assert.NotNull(result.Query);
            Assert.Contains("How are you?", result.Query);
            Assert.Null(result.ConversationId);
        }

        [Fact]
        public void ProcessMessageHistory_WithZeroWidthMode_ReturnsQueryAndConversationId()
        {
            // Arrange
            _settings.ConversationMemoryMode = 2; // zero_width mode
            var messages = new List<OpenAIMessage>
            {
                new() { Role = "system", Content = "You are a helpful assistant" },
                new() { Role = "user", Content = "Hello" }
            };

            // Act
            var result = _memoryManager.ProcessMessageHistory(messages);

            // Assert
            Assert.NotNull(result.Query);
            Assert.Contains("Hello", result.Query);
            Assert.Null(result.ConversationId); // First conversation, no ID yet
        }

        [Fact]
        public void EncodeConversationId_WithValidId_ReturnsEncodedString()
        {
            // Arrange
            var conversationId = "test-conversation-123";

            // Act
            var result = _memoryManager.EncodeConversationId(conversationId);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            // The result should contain only zero-width characters
            Assert.True(result.All(c => c == '\u200b' || c == '\u200c' || c == '\u200d' || c == '\ufeff' || c == '\u2060' || c == '\u180e' || c == '\u2061' || c == '\u2062'));
        }

        [Fact]
        public void DecodeConversationId_WithValidContent_ReturnsConversationId()
        {
            // Arrange
            var conversationId = "test-conversation-123";
            var encoded = _memoryManager.EncodeConversationId(conversationId);
            var content = $"This is a response{encoded}"; // Put encoded ID at the end

            // Act
            var result = _memoryManager.DecodeConversationId(content);

            // Assert
            Assert.Equal(conversationId, result);
        }

        [Fact]
        public void DecodeConversationId_WithInvalidContent_ReturnsNull()
        {
            // Arrange
            var content = "This is a response without encoded ID";

            // Act
            var result = _memoryManager.DecodeConversationId(content);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ProcessResponseContent_WithZeroWidthModeAndConversationId_ReturnsContentWithEncodedId()
        {
            // Arrange
            _settings.ConversationMemoryMode = 2; // zero_width mode
            var content = "This is a response";
            var conversationId = "test-conversation-123";
            var conversationHistory = new List<DifyMessage>();

            // Act
            var result = _memoryManager.ProcessResponseContent(content, conversationId, conversationHistory);

            // Assert
            Assert.NotNull(result);
            Assert.StartsWith(content, result);
            Assert.NotEqual(content, result); // Should have encoded ID appended
        }

        [Fact]
        public void ProcessResponseContent_WithHistoryMessageMode_ReturnsOriginalContent()
        {
            // Arrange
            _settings.ConversationMemoryMode = 1; // history_message mode
            var content = "This is a response";
            var conversationId = "test-conversation-123";
            var conversationHistory = new List<DifyMessage>();

            // Act
            var result = _memoryManager.ProcessResponseContent(content, conversationId, conversationHistory);

            // Assert
            Assert.Equal(content, result);
        }
    }
}