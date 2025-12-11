using Microsoft.AspNetCore.Mvc;
using OpenDify.NET.Configuration;
using OpenDify.NET.Models;
using OpenDify.NET.Services;
using System.Text.Json;

namespace OpenDify.NET.Controllers
{
    /// <summary>
    /// OpenAI 兼容 API 控制器
    /// </summary>
    [ApiController]
    [Route("v1")]
    public class OpenAIController : ControllerBase
    {
        private readonly ILogger<OpenAIController> _logger;
        private readonly DifyModelManager _modelManager;
        private readonly RequestTransformationService _requestTransformer;
        private readonly ResponseTransformationService _responseTransformer;
        private readonly FileUploadService _fileUploadService;
        private readonly StreamingService _streamingService;
        private readonly HttpClient _httpClient;
        private readonly AppSettings _settings;

        public OpenAIController(
            ILogger<OpenAIController> logger,
            DifyModelManager modelManager,
            RequestTransformationService requestTransformer,
            ResponseTransformationService responseTransformer,
            FileUploadService fileUploadService,
            StreamingService streamingService,
            HttpClient httpClient,
            AppSettings settings)
        {
            _logger = logger;
            _modelManager = modelManager;
            _requestTransformer = requestTransformer;
            _responseTransformer = responseTransformer;
            _fileUploadService = fileUploadService;
            _streamingService = streamingService;
            _httpClient = httpClient;
            _settings = settings;
        }

        /// <summary>
        /// 获取可用模型列表
        /// </summary>
        [HttpGet("models")]
        public async Task<ActionResult<OpenAIModelsResponse>> ListModels()
        {
            try
            {
                _logger.LogInformation("获取可用模型列表");
                
                // 刷新模型信息
                await _modelManager.RefreshModelInfoAsync();
                
                // 获取可用模型列表
                var availableModels = _modelManager.GetAvailableModels();
                
                var response = _responseTransformer.CreateModelsResponse(availableModels);
                _logger.LogInformation("返回 {count} 个可用模型", availableModels.Count);
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取模型列表失败");
                var errorResponse = _responseTransformer.CreateErrorResponse("获取模型列表失败", "api_error");
                return StatusCode(500, errorResponse);
            }
        }

        /// <summary>
        /// 聊天完成接口
        /// </summary>
        [HttpPost("chat/completions")]
        public async Task<ActionResult> ChatCompletions([FromBody] OpenAIChatCompletionRequest request)
        {
            try
            {
                // 验证 API 密钥
                if (!ValidateApiKey(out var apiKeyError))
                {
                    return Unauthorized(apiKeyError);
                }

                _logger.LogInformation("收到聊天完成请求，模型: {model}，流式: {stream}", request.Model, request.Stream);

                // 验证模型是否支持
                var difyApiKey = _modelManager.GetApiKey(request.Model);
                if (string.IsNullOrEmpty(difyApiKey))
                {
                    var availableModels = _modelManager.GetSupportedModelNames();
                    var errorResponse = _responseTransformer.CreateErrorResponse(
                        $"模型 {request.Model} 不受支持。可用模型: {string.Join(", ", availableModels)}", 
                        "model_not_found");
                    return NotFound(errorResponse);
                }

                // 转换请求
                var (difyRequest, uploadedImages) = await _requestTransformer.TransformChatCompletionRequestAsync(request);

                // 处理图片上传
                if (uploadedImages.Any())
                {
                    _logger.LogInformation("已上传 {count} 张图片", uploadedImages.Count);
                }

                // 发送请求到 Dify
                if (request.Stream)
                {
                    return await HandleStreamingRequestAsync(difyRequest, difyApiKey, request.Model);
                }
                else
                {
                    return await HandleBlockingRequestAsync(difyRequest, difyApiKey, request.Model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理聊天完成请求失败");
                var errorResponse = _responseTransformer.CreateErrorResponse("处理请求失败", "internal_error");
                return StatusCode(500, errorResponse);
            }
        }

        /// <summary>
        /// 处理流式请求
        /// </summary>
        private async Task<ActionResult> HandleStreamingRequestAsync(
            DifyChatMessageRequest difyRequest, 
            string difyApiKey, 
            string model)
        {
            try
            {
                var difyEndpoint = $"{_settings.Dify.ApiBase}/chat-messages";
                
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, difyEndpoint);
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", difyApiKey);
                httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
                
                var json = JsonSerializer.Serialize(difyRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                httpRequest.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Dify API 错误: {error}", errorContent);
                    var errorResponse = _responseTransformer.CreateErrorResponse(
                        $"Dify API 错误: {errorContent}", "api_error");
                    return StatusCode((int)response.StatusCode, errorResponse);
                }

                // 返回流式响应
                return new StreamingActionResult(async (stream, cancellationToken) =>
                {
                    using var difyStream = await response.Content.ReadAsStreamAsync();
                    await foreach (var chunk in _streamingService.CreateStreamingResult(difyStream, model, cancellationToken))
                    {
                        await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(chunk), cancellationToken);
                        await stream.FlushAsync(cancellationToken);
                    }
                }, "text/event-stream");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理流式请求失败");
                var errorResponse = _responseTransformer.CreateErrorResponse("流式请求失败", "stream_error");
                return StatusCode(500, errorResponse);
            }
        }

        /// <summary>
        /// 处理阻塞请求
        /// </summary>
        private async Task<ActionResult> HandleBlockingRequestAsync(
            DifyChatMessageRequest difyRequest, 
            string difyApiKey, 
            string model)
        {
            try
            {
                var difyEndpoint = $"{_settings.Dify.ApiBase}/chat-messages";
                
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, difyEndpoint);
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", difyApiKey);
                
                var json = JsonSerializer.Serialize(difyRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                httpRequest.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Dify API 错误: {error}", errorContent);
                    var errorResponse = _responseTransformer.CreateErrorResponse(
                        $"Dify API 错误: {errorContent}", "api_error");
                    return StatusCode((int)response.StatusCode, errorResponse);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var difyResponse = JsonSerializer.Deserialize<DifyChatMessageResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (difyResponse == null)
                {
                    var errorResponse = _responseTransformer.CreateErrorResponse("解析 Dify 响应失败", "parse_error");
                    return StatusCode(500, errorResponse);
                }

                // 转换响应
                var openaiResponse = _responseTransformer.TransformChatCompletionResponse(difyResponse, model);

                // 如果有会话ID，在响应头中传递
                if (!string.IsNullOrEmpty(difyResponse.ConversationId))
                {
                    Response.Headers.Add("Conversation-Id", difyResponse.ConversationId);
                }

                return Ok(openaiResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理阻塞请求失败");
                var errorResponse = _responseTransformer.CreateErrorResponse("请求失败", "request_error");
                return StatusCode(500, errorResponse);
            }
        }

        /// <summary>
        /// 验证 API 密钥
        /// </summary>
        private bool ValidateApiKey(out OpenAIErrorResponse? errorResponse)
        {
            errorResponse = null;

            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                errorResponse = _responseTransformer.CreateErrorResponse(
                    "缺少 Authorization 头", "invalid_request_error", "invalid_api_key");
                return false;
            }

            var parts = authHeader.Split(' ');
            if (parts.Length != 2 || parts[0].ToLowerInvariant() != "bearer")
            {
                errorResponse = _responseTransformer.CreateErrorResponse(
                    "无效的 Authorization 头格式。期望: Bearer <API_KEY>", "invalid_request_error", "invalid_api_key");
                return false;
            }

            var providedApiKey = parts[1];
            if (!_settings.OpenAI.ValidApiKeys.Contains(providedApiKey))
            {
                errorResponse = _responseTransformer.CreateErrorResponse(
                    "无效的 API 密钥", "invalid_request_error", "invalid_api_key");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 健康检查端点
        /// </summary>
        [HttpGet("health")]
        public ActionResult HealthCheck()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTimeOffset.UtcNow,
                version = "1.0.0"
            });
        }
    }

    /// <summary>
    /// 流式动作结果
    /// </summary>
    public class StreamingActionResult : ActionResult
    {
        private readonly Func<Stream, CancellationToken, Task> _callback;
        private readonly string _contentType;

        public StreamingActionResult(Func<Stream, CancellationToken, Task> callback, string contentType)
        {
            _callback = callback;
            _contentType = contentType;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = _contentType;
            response.Headers.Add("Cache-Control", "no-cache, no-transform");
            response.Headers.Add("Connection", "keep-alive");
            response.Headers.Add("X-Accel-Buffering", "no");

            await _callback(response.Body, context.HttpContext.RequestAborted);
        }
    }
}