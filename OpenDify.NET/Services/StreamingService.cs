using OpenDify.NET.Configuration;
using OpenDify.NET.Models;
using System.Text.Json;

namespace OpenDify.NET.Services
{
    /// <summary>
    /// 流式响应处理服务，负责处理 Dify 的流式响应并转换为 OpenAI 格式
    /// </summary>
    public class StreamingService
    {
        private readonly ILogger<StreamingService> _logger;
        private readonly ResponseTransformationService _responseTransformer;
        private readonly ConversationMemoryManager _memoryManager;

        public StreamingService(
            ILogger<StreamingService> logger,
            ResponseTransformationService responseTransformer,
            ConversationMemoryManager memoryManager)
        {
            _logger = logger;
            _responseTransformer = responseTransformer;
            _memoryManager = memoryManager;
        }

        /// <summary>
        /// 处理流式响应
        /// </summary>
        /// <param name="stream">Dify 流式响应流</param>
        /// <param name="model">模型名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>OpenAI 格式的流式响应</returns>
        public async IAsyncEnumerable<OpenAIChatCompletionResponse> ProcessStreamAsync(
            Stream stream,
            string model,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(stream);
            var messageId = string.Empty;
            var outputBuffer = new List<char>();
            var conversationId = string.Empty;
            var conversationHistory = new List<DifyMessage>();
            var isProcessingBuffer = false;
            var streamEnded = false;

            while (!streamEnded && !cancellationToken.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken);
                    if (line == null)
                    {
                        streamEnded = true;
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "读取流式响应行时发生错误");
                    yield break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                DifyStreamChunk? difyChunk = null;
                try
                {
                    difyChunk = ParseDifyStreamChunk(line);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析流式响应块时发生错误: {line}", line);
                    continue;
                }

                if (difyChunk == null)
                {
                    continue;
                }

                // 处理不同类型的事件
                try
                {
                    await ProcessStreamChunkAsync(
                        difyChunk, model, messageId, outputBuffer, conversationId, conversationHistory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理流式响应块时发生错误: {line}", line);
                    continue;
                }

                // 如果有新的内容添加到缓冲区，开始处理缓冲区
                if (outputBuffer.Any() && !isProcessingBuffer)
                {
                    isProcessingBuffer = true;
                    while (outputBuffer.Any() && !cancellationToken.IsCancellationRequested)
                    {
                        var ch = outputBuffer[0];
                        outputBuffer.RemoveAt(0);
                        
                        var delay = CalculateDelay(outputBuffer.Count);
                        if (delay.TotalMilliseconds > 0)
                        {
                            await Task.Delay(delay, cancellationToken);
                        }

                        var response = CreateCharacterChunk(ch, messageId, model);
                        if (response != null)
                        {
                            messageId = response.Id;
                            yield return response;
                        }
                    }
                    isProcessingBuffer = false;
                }

                // 处理会话结束事件
                if (difyChunk.Event == "message_end")
                {
                    conversationId = difyChunk.ConversationId ?? conversationId;
                    conversationHistory = difyChunk.ConversationHistory ?? conversationHistory;

                    // 确保所有缓冲区内容都已输出
                    while (outputBuffer.Any() && !cancellationToken.IsCancellationRequested)
                    {
                        var ch = outputBuffer[0];
                        outputBuffer.RemoveAt(0);
                        
                        var response = CreateCharacterChunk(ch, messageId, model);
                        if (response != null)
                        {
                            yield return response;
                        }
                    }

                    // 处理零宽字符模式的会话ID
                    if (!string.IsNullOrEmpty(conversationId))
                    {
                        var encodedConversationId = _memoryManager.EncodeConversationId(conversationId);
                        if (!string.IsNullOrEmpty(encodedConversationId))
                        {
                            foreach (var ch in encodedConversationId)
                            {
                                yield return CreateCharacterChunk(ch, messageId, model);
                            }
                        }
                    }

                    // 发送结束标记
                    yield return CreateEndChunk(messageId, model);
                    break;
                }
            }
        }

        /// <summary>
        /// 解析 Dify 流式响应块
        /// </summary>
        private DifyStreamChunk? ParseDifyStreamChunk(string line)
        {
            try
            {
                if (!line.StartsWith("data: "))
                {
                    return null;
                }

                var jsonStr = line.Substring(6); // 去掉 "data: " 前缀
                if (jsonStr == "[DONE]")
                {
                    return null;
                }

                return JsonSerializer.Deserialize<DifyStreamChunk>(jsonStr, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析 Dify 流式块失败: {line}", line);
                return null;
            }
        }

        /// <summary>
        /// 处理单个流式块
        /// </summary>
        private async Task ProcessStreamChunkAsync(
            DifyStreamChunk difyChunk,
            string model,
            string messageId,
            List<char> outputBuffer,
            string conversationId,
            List<DifyMessage> conversationHistory)
        {
            switch (difyChunk.Event)
            {
                case "message":
                case "agent_message":
                    if (!string.IsNullOrEmpty(difyChunk.Answer))
                    {
                        await ProcessMessageChunkAsync(difyChunk, outputBuffer);
                    }
                    break;

                case "agent_thought":
                    ProcessAgentThoughtChunk(difyChunk, model, messageId);
                    break;

                case "message_file":
                    ProcessMessageFileChunk(difyChunk, model, messageId);
                    break;
            }
        }

        /// <summary>
        /// 处理消息块
        /// </summary>
        private async Task ProcessMessageChunkAsync(
            DifyStreamChunk difyChunk,
            List<char> outputBuffer)
        {
            var answer = difyChunk.Answer ?? string.Empty;
            
            // 将字符添加到输出缓冲区
            foreach (var ch in answer)
            {
                outputBuffer.Add(ch);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 处理 Agent 思考块
        /// </summary>
        private void ProcessAgentThoughtChunk(
            DifyStreamChunk difyChunk,
            string model,
            string messageId)
        {
            _logger.LogInformation("[Agent Thought] ID: {thoughtId}, Tool: {tool}",
                difyChunk.Id, difyChunk.Tool);

            if (!string.IsNullOrEmpty(difyChunk.Thought))
            {
                _logger.LogInformation("[Agent Thought] Thought: {thought}", difyChunk.Thought);
            }

            if (!string.IsNullOrEmpty(difyChunk.ToolInput))
            {
                _logger.LogInformation("[Agent Thought] Tool Input: {toolInput}", difyChunk.ToolInput);
            }

            if (!string.IsNullOrEmpty(difyChunk.Observation))
            {
                _logger.LogInformation("[Agent Thought] Observation: {observation}", difyChunk.Observation);
            }

            // Agent 思考事件不直接返回给客户端
        }

        /// <summary>
        /// 处理消息文件块
        /// </summary>
        private void ProcessMessageFileChunk(
            DifyStreamChunk difyChunk,
            string model,
            string messageId)
        {
            _logger.LogInformation("[Message File] ID: {fileId}, Type: {fileType}",
                difyChunk.Id, difyChunk.Files?.FirstOrDefault()?.Type);

            // 消息文件事件不直接返回给客户端
        }

        /// <summary>
        /// 创建单个字符的响应块
        /// </summary>
        private OpenAIChatCompletionResponse CreateCharacterChunk(char character, string messageId, string model)
        {
            return new OpenAIChatCompletionResponse
            {
                Id = messageId,
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = model,
                Choices = new List<OpenAIChoice>
                {
                    new()
                    {
                        Index = 0,
                        Delta = new OpenAIMessage
                        {
                            Role = "assistant",
                            Content = character.ToString()
                        },
                        FinishReason = null
                    }
                }
            };
        }

        /// <summary>
        /// 创建结束响应块
        /// </summary>
        private OpenAIChatCompletionResponse CreateEndChunk(string messageId, string model)
        {
            return new OpenAIChatCompletionResponse
            {
                Id = messageId,
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = model,
                Choices = new List<OpenAIChoice>
                {
                    new()
                    {
                        Index = 0,
                        Delta = null,
                        FinishReason = "stop"
                    }
                }
            };
        }

        /// <summary>
        /// 根据缓冲区大小动态计算延迟
        /// </summary>
        private TimeSpan CalculateDelay(int bufferSize)
        {
            return bufferSize switch
            {
                > 30 => TimeSpan.FromMilliseconds(1),   // 缓冲区内容较多，快速输出
                > 20 => TimeSpan.FromMilliseconds(2),   // 中等数量，适中速度
                > 10 => TimeSpan.FromMilliseconds(10),  // 较少内容，稍慢速度
                _ => TimeSpan.FromMilliseconds(20)      // 内容很少，使用较慢的速度
            };
        }

        /// <summary>
        /// 将流式响应格式化为 Server-Sent Events 格式
        /// </summary>
        public string FormatAsSSE(OpenAIChatCompletionResponse response)
        {
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            return $"data: {json}\n\n";
        }

        /// <summary>
        /// 创建流式结束标记
        /// </summary>
        public string CreateSSEEndMarker()
        {
            return "data: [DONE]\n\n";
        }

        /// <summary>
        /// 处理流式响应并返回 SSE 格式的字符串
        /// </summary>
        public async Task<string> ProcessStreamToSSEAsync(
            Stream stream,
            string model,
            CancellationToken cancellationToken = default)
        {
            var result = new System.Text.StringBuilder();

            await foreach (var response in ProcessStreamAsync(stream, model, cancellationToken))
            {
                result.Append(FormatAsSSE(response));
            }

            result.Append(CreateSSEEndMarker());
            return result.ToString();
        }

        /// <summary>
        /// 创建流式响应的 IResult
        /// </summary>
        public IAsyncEnumerable<string> CreateStreamingResult(
            Stream stream,
            string model,
            CancellationToken cancellationToken = default)
        {
            async IAsyncEnumerable<string> GenerateStream()
            {
                await foreach (var response in ProcessStreamAsync(stream, model, cancellationToken))
                {
                    yield return FormatAsSSE(response);
                }
                yield return CreateSSEEndMarker();
            }

            return GenerateStream();
        }
    }
}