using OpenDify.NET.Configuration;
using OpenDify.NET.Models;
using System.Text.Json;

namespace OpenDify.NET.Services
{
    /// <summary>
    /// 请求转换服务，负责将 OpenAI 格式的请求转换为 Dify 格式
    /// </summary>
    public class RequestTransformationService
    {
        private readonly ILogger<RequestTransformationService> _logger;
        private readonly ConversationMemoryManager _memoryManager;

        public RequestTransformationService(
            ILogger<RequestTransformationService> logger,
            ConversationMemoryManager memoryManager)
        {
            _logger = logger;
            _memoryManager = memoryManager;
        }

        /// <summary>
        /// 将 OpenAI 聊天完成请求转换为 Dify 聊天消息请求
        /// </summary>
        /// <param name="openaiRequest">OpenAI 请求</param>
        /// <param name="conversationId">会话ID（可选）</param>
        /// <returns>转换后的 Dify 请求和上传的文件信息</returns>
        public virtual async Task<(DifyChatMessageRequest DifyRequest, List<UploadedImage> UploadedImages)> TransformChatCompletionRequestAsync(
            OpenAIChatCompletionRequest openaiRequest,
            string? conversationId = null)
        {
            var userId = openaiRequest.User ?? "default_user";
            var inputs = openaiRequest.Inputs ?? new Dictionary<string, object>();
            
            // 处理消息历史和内容
            var (query, processedConversationId) = _memoryManager.ProcessMessageHistory(
                openaiRequest.Messages, conversationId);
            
            // 处理图片上传
            var (processedQuery, uploadedImages) = await ProcessImagesAsync(openaiRequest.Messages, userId);
            if (!string.IsNullOrEmpty(processedQuery))
            {
                query = processedQuery;
            }

            // 处理系统消息和 Function Calling
            query = ProcessSystemMessageWithFunctions(openaiRequest, query);

            var difyRequest = new DifyChatMessageRequest
            {
                Inputs = inputs,
                Query = query,
                ResponseMode = openaiRequest.Stream ? "streaming" : "blocking",
                User = userId,
                ConversationId = processedConversationId,
                AutoGenerateName = true
            };

            // 如果有上传的文件，添加到请求中
            if (uploadedImages.Any())
            {
                difyRequest.Files = uploadedImages.Select(img => new DifyFile
                {
                    Type = "image",
                    TransferMethod = "local_file",
                    UploadFileId = img.FileId
                }).ToList();
                
                _logger.LogInformation("已添加 {count} 个上传文件到请求中", uploadedImages.Count);
            }

            _logger.LogInformation("请求转换完成，模式: {responseMode}，包含文件: {fileCount}", 
                difyRequest.ResponseMode, uploadedImages.Count);

            return (difyRequest, uploadedImages);
        }

        /// <summary>
        /// 处理消息中的图片，提取文本并上传图片
        /// </summary>
        private async Task<(string Query, List<UploadedImage> UploadedImages)> ProcessImagesAsync(
            List<OpenAIMessage> messages, string userId)
        {
            var uploadedImages = new List<UploadedImage>();
            var textParts = new List<string>();

            // 获取最后一条非系统消息
            var lastMessage = messages.LastOrDefault(m => m.Role != "system");
            if (lastMessage == null)
            {
                return ("", uploadedImages);
            }

            var content = lastMessage.Content;
            
            if (content is string contentStr)
            {
                // 纯文本内容
                textParts.Add(contentStr);
            }
            else if (content is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    // 多模态内容（文本 + 图片）
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var typeProperty))
                        {
                            var type = typeProperty.GetString();
                            if (type == "text" && item.TryGetProperty("text", out var textProperty))
                            {
                                textParts.Add(textProperty.GetString() ?? "");
                            }
                            else if (type == "image_url" && item.TryGetProperty("image_url", out var imageUrlProperty))
                            {
                                if (imageUrlProperty.TryGetProperty("url", out var urlProperty))
                                {
                                    var imageUrl = urlProperty.GetString();
                                    if (!string.IsNullOrEmpty(imageUrl))
                                    {
                                        var uploadedImage = await UploadImageAsync(imageUrl, userId);
                                        if (uploadedImage != null)
                                        {
                                            uploadedImages.Add(uploadedImage);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    textParts.Add(jsonElement.GetString() ?? "");
                }
            }

            var query = string.Join("\n", textParts);
            _logger.LogInformation("处理消息内容完成，文本长度: {textLength}，图片数量: {imageCount}", 
                query.Length, uploadedImages.Count);

            return (query, uploadedImages);
        }

        /// <summary>
        /// 上传图片到 Dify
        /// </summary>
        private async Task<UploadedImage?> UploadImageAsync(string imageUrl, string userId)
        {
            try
            {
                // 这里应该实现实际的图片上传逻辑
                // 由于需要 HttpClient，我们在另一个服务中实现
                _logger.LogInformation("准备上传图片: {imageUrl}", imageUrl);
                
                // 暂时返回 null，实际实现需要在 FileUploadService 中完成
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传图片失败: {imageUrl}", imageUrl);
                return null;
            }
        }


        /// <summary>
        /// 处理系统消息和 Function Calling 的组合
        /// </summary>
        private string ProcessSystemMessageWithFunctions(OpenAIChatCompletionRequest openaiRequest, string query)
        {
            var systemMessage = openaiRequest.Messages.FirstOrDefault(m => m.Role == "system");
            var systemContent = systemMessage != null ? GetMessageContent(systemMessage.Content) : "";

            var additionalInfo = new List<string>();

            // 处理 Functions
            if (openaiRequest.Functions?.Any() == true)
            {
                var functionsJson = JsonSerializer.Serialize(openaiRequest.Functions, new JsonSerializerOptions
                {
                    WriteIndented = false
                });
                additionalInfo.Add($"可用函数:\n```json\n{functionsJson}\n```");
            }

            // 处理 Tools
            if (openaiRequest.Tools?.Any() == true)
            {
                var toolsJson = JsonSerializer.Serialize(openaiRequest.Tools, new JsonSerializerOptions
                {
                    WriteIndented = false
                });
                additionalInfo.Add($"可用工具:\n```json\n{toolsJson}\n```");
            }

            // 处理 Function Call 强制调用
            if (openaiRequest.FunctionCall != null)
            {
                var functionCallJson = JsonSerializer.Serialize(openaiRequest.FunctionCall, new JsonSerializerOptions
                {
                    WriteIndented = false
                });
                additionalInfo.Add($"强制调用函数: {functionCallJson}");
            }

            // 处理 Tool Choice
            if (openaiRequest.ToolChoice != null)
            {
                var toolChoiceJson = JsonSerializer.Serialize(openaiRequest.ToolChoice, new JsonSerializerOptions
                {
                    WriteIndented = false
                });
                additionalInfo.Add($"工具选择: {toolChoiceJson}");
            }

            // 组合所有信息
            if (!string.IsNullOrEmpty(systemContent) || additionalInfo.Any())
            {
                var combinedInfo = new List<string>();
                if (!string.IsNullOrEmpty(systemContent))
                {
                    combinedInfo.Add($"系统指令: {systemContent}");
                }
                combinedInfo.AddRange(additionalInfo);

                var systemInfo = string.Join("\n\n", combinedInfo);
                return $"{systemInfo}\n\n用户问题: {query}";
            }

            return query;
        }

        /// <summary>
        /// 获取消息内容
        /// </summary>
        private string GetMessageContent(object content)
        {
            return content switch
            {
                string str => str,
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? "",
                _ => content.ToString() ?? ""
            };
        }
    }

    /// <summary>
    /// 上传的图片信息
    /// </summary>
    public class UploadedImage
    {
        public string FileId { get; set; } = string.Empty;
        public string OriginalUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
        public string MimeType { get; set; } = string.Empty;
    }
}