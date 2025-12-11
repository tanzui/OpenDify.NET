using OpenDify.NET.Configuration;
using OpenDify.NET.Models;
using System.Text.Json;

namespace OpenDify.NET.Services
{
    /// <summary>
    /// 响应转换服务，负责将 Dify 格式的响应转换为 OpenAI 格式
    /// </summary>
    public class ResponseTransformationService
    {
        private readonly ILogger<ResponseTransformationService> _logger;
        private readonly ConversationMemoryManager _memoryManager;

        public ResponseTransformationService(
            ILogger<ResponseTransformationService> logger,
            ConversationMemoryManager memoryManager)
        {
            _logger = logger;
            _memoryManager = memoryManager;
        }

        /// <summary>
        /// 将 Dify 聊天消息响应转换为 OpenAI 聊天完成响应
        /// </summary>
        /// <param name="difyResponse">Dify 响应</param>
        /// <param name="model">模型名称</param>
        /// <param name="stream">是否为流式响应</param>
        /// <returns>转换后的 OpenAI 响应</returns>
        public virtual OpenAIChatCompletionResponse TransformChatCompletionResponse(
            DifyChatMessageResponse difyResponse,
            string model,
            bool stream = false)
        {
            // 获取回答内容
            var answer = ExtractAnswer(difyResponse);
            
            // 处理会话记忆（零宽字符模式）
            answer = _memoryManager.ProcessResponseContent(
                answer, difyResponse.ConversationId, difyResponse.ConversationHistory);

            return new OpenAIChatCompletionResponse
            {
                Id = difyResponse.MessageId,
                Object = "chat.completion",
                Created = difyResponse.CreatedAt,
                Model = model,
                Choices = new List<OpenAIChoice>
                {
                    new()
                    {
                        Index = 0,
                        Message = new OpenAIMessage
                        {
                            Role = "assistant",
                            Content = answer
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = TransformUsage(difyResponse.Metadata?.Usage)
            };
        }

        /// <summary>
        /// 将 Dify 流式响应块转换为 OpenAI 流式响应块
        /// </summary>
        /// <param name="difyChunk">Dify 流式块</param>
        /// <param name="model">模型名称</param>
        /// <param name="messageId">消息ID</param>
        /// <returns>转换后的 OpenAI 流式块</returns>
        public virtual OpenAIChatCompletionResponse? TransformStreamChunk(
            DifyStreamChunk difyChunk,
            string model,
            string? messageId = null)
        {
            messageId ??= difyChunk.MessageId ?? Guid.NewGuid().ToString();

            return difyChunk.Event switch
            {
                "message" or "agent_message" when !string.IsNullOrEmpty(difyChunk.Answer) => new OpenAIChatCompletionResponse
                {
                    Id = messageId,
                    Object = "chat.completion.chunk",
                    Created = difyChunk.CreatedAt,
                    Model = model,
                    Choices = new List<OpenAIChoice>
                    {
                        new()
                        {
                            Index = 0,
                            Delta = new OpenAIMessage
                            {
                                Role = "assistant",
                                Content = difyChunk.Answer
                            },
                            FinishReason = null
                        }
                    }
                },
                
                "message_end" => new OpenAIChatCompletionResponse
                {
                    Id = messageId,
                    Object = "chat.completion.chunk",
                    Created = difyChunk.CreatedAt,
                    Model = model,
                    Choices = new List<OpenAIChoice>
                    {
                        new()
                        {
                            Index = 0,
                            Delta = new OpenAIMessage(),
                            FinishReason = "stop"
                        }
                    }
                },
                
                "agent_thought" => HandleAgentThought(difyChunk, model, messageId),
                
                "message_file" => HandleMessageFile(difyChunk, model, messageId),
                
                _ => null
            };
        }

        /// <summary>
        /// 处理 Agent 思考事件
        /// </summary>
        private OpenAIChatCompletionResponse? HandleAgentThought(
            DifyStreamChunk difyChunk, string model, string messageId)
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

            // Agent 思考事件不直接返回给客户端，只记录日志
            return null;
        }

        /// <summary>
        /// 处理消息文件事件
        /// </summary>
        private OpenAIChatCompletionResponse? HandleMessageFile(
            DifyStreamChunk difyChunk, string model, string messageId)
        {
            _logger.LogInformation("[Message File] ID: {fileId}, Type: {fileType}", 
                difyChunk.Id, difyChunk.Files?.FirstOrDefault()?.Type);
            
            // 消息文件事件不直接返回给客户端，只记录日志
            return null;
        }

        /// <summary>
        /// 从 Dify 响应中提取回答内容
        /// </summary>
        private string ExtractAnswer(DifyChatMessageResponse difyResponse)
        {
            // 普通聊天模式
            if (!string.IsNullOrEmpty(difyResponse.Answer))
            {
                return difyResponse.Answer;
            }

            // Agent 模式，从 agent_thoughts 中提取回答
            if (difyResponse.AgentThoughts?.Any() == true)
            {
                // 通常最后一个 thought 包含最终答案
                var lastThought = difyResponse.AgentThoughts.LastOrDefault();
                if (!string.IsNullOrEmpty(lastThought?.Thought))
                {
                    return lastThought.Thought;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 转换使用情况信息
        /// </summary>
        private OpenAIUsage? TransformUsage(DifyUsage? difyUsage)
        {
            if (difyUsage == null)
            {
                return null;
            }

            return new OpenAIUsage
            {
                PromptTokens = difyUsage.PromptTokens,
                CompletionTokens = difyUsage.CompletionTokens,
                TotalTokens = difyUsage.TotalTokens
            };
        }

        /// <summary>
        /// 处理 Function Calling 响应
        /// </summary>
        public OpenAIChatCompletionResponse? TransformFunctionCallResponse(
            DifyChatMessageResponse difyResponse,
            string model)
        {
            var answer = ExtractAnswer(difyResponse);
            
            // 尝试解析 Function Call 响应
            var functionCall = TryParseFunctionCall(answer);
            if (functionCall != null)
            {
                return new OpenAIChatCompletionResponse
                {
                    Id = difyResponse.MessageId,
                    Object = "chat.completion",
                    Created = difyResponse.CreatedAt,
                    Model = model,
                    Choices = new List<OpenAIChoice>
                    {
                        new()
                        {
                            Index = 0,
                            Message = new OpenAIMessage
                            {
                                Role = "assistant",
                                Content = null,
                                FunctionCall = functionCall
                            },
                            FinishReason = "function_call"
                        }
                    },
                    Usage = TransformUsage(difyResponse.Metadata?.Usage)
                };
            }

            // 尝试解析 Tool Calls 响应
            var toolCalls = TryParseToolCalls(answer);
            if (toolCalls?.Any() == true)
            {
                return new OpenAIChatCompletionResponse
                {
                    Id = difyResponse.MessageId,
                    Object = "chat.completion",
                    Created = difyResponse.CreatedAt,
                    Model = model,
                    Choices = new List<OpenAIChoice>
                    {
                        new()
                        {
                            Index = 0,
                            Message = new OpenAIMessage
                            {
                                Role = "assistant",
                                Content = null,
                                ToolCalls = toolCalls
                            },
                            FinishReason = "tool_calls"
                        }
                    },
                    Usage = TransformUsage(difyResponse.Metadata?.Usage)
                };
            }

            // 如果不是 Function Call 响应，返回普通响应
            return null;
        }

        /// <summary>
        /// 尝试解析 Function Call
        /// </summary>
        private OpenAIFunctionCall? TryParseFunctionCall(string answer)
        {
            try
            {
                // 这里应该实现实际的 Function Call 解析逻辑
                // 由于 Dify 的响应格式可能不同，需要根据实际情况调整
                if (answer.Contains("function_call") || answer.Contains("函数调用"))
                {
                    // 示例解析逻辑，需要根据实际 Dify 响应格式调整
                    // 这里只是示例，实际实现需要更复杂的解析逻辑
                    return new OpenAIFunctionCall
                    {
                        Name = "example_function",
                        Arguments = "{}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析 Function Call 失败");
            }

            return null;
        }

        /// <summary>
        /// 尝试解析 Tool Calls
        /// </summary>
        private List<OpenAIToolCall>? TryParseToolCalls(string answer)
        {
            try
            {
                // 这里应该实现实际的 Tool Calls 解析逻辑
                if (answer.Contains("tool_calls") || answer.Contains("工具调用"))
                {
                    // 示例解析逻辑，需要根据实际 Dify 响应格式调整
                    return new List<OpenAIToolCall>
                    {
                        new()
                        {
                            Id = Guid.NewGuid().ToString(),
                            Type = "function",
                            Function = new OpenAIFunctionCall
                            {
                                Name = "example_tool",
                                Arguments = "{}"
                            }
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析 Tool Calls 失败");
            }

            return null;
        }

        /// <summary>
        /// 创建错误响应
        /// </summary>
        public virtual OpenAIErrorResponse CreateErrorResponse(string message, string type = "api_error", string? code = null)
        {
            return new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = message,
                    Type = type,
                    Code = code
                }
            };
        }

        /// <summary>
        /// 创建模型列表响应
        /// </summary>
        public virtual OpenAIModelsResponse CreateModelsResponse(List<OpenAIModel> models)
        {
            return new OpenAIModelsResponse
            {
                Object = "list",
                Data = models
            };
        }
    }
}