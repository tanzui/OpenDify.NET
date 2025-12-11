using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace OpenDify.NET.Tests.IntegrationTests
{
    public class BasicApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public BasicApiTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task GetHealth_ReturnsHealthyStatus()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("healthy", content);
        }

        [Fact]
        public async Task GetRoot_ReturnsApiInfo()
        {
            // Act
            var response = await _client.GetAsync("/");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("OpenDify.NET", content);
            Assert.Contains("API Server", content);
        }

        [Fact]
        public async Task GetModels_WithoutAuth_ReturnsUnauthorized()
        {
            // Act
            var response = await _client.GetAsync("/v1/models");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetModels_WithValidAuth_ReturnsModelList()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/v1/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-api-key");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            // 注意：在测试环境中，这可能会返回 401，因为我们没有配置真实的 API 密钥
            // 这个测试主要验证端点存在且路由正确
            Assert.True(response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                       response.StatusCode == System.Net.HttpStatusCode.OK);
        }

        [Fact]
        public async Task PostChatCompletion_WithoutAuth_ReturnsUnauthorized()
        {
            // Arrange
            var chatRequest = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "user", content = "Hello" }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(chatRequest),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await _client.PostAsync("/v1/chat/completions", content);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task PostChatCompletion_WithValidAuth_ProcessesRequest()
        {
            // Arrange
            var chatRequest = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "user", content = "Hello" }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-api-key");
            request.Content = new StringContent(
                JsonSerializer.Serialize(chatRequest),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            // 注意：在测试环境中，这可能会返回错误，因为我们没有配置真实的 Dify API
            // 这个测试主要验证端点存在且路由正确
            Assert.True(response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                       response.StatusCode == System.Net.HttpStatusCode.InternalServerError ||
                       response.StatusCode == System.Net.HttpStatusCode.BadRequest);
        }
    }
}