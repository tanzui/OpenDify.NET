using System.Text.Json.Serialization;

namespace OpenDify.NET.Models
{
    /// <summary>
    /// OpenAI 聊天完成请求
    /// </summary>
    public class OpenAIChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenAIMessage> Messages { get; set; } = new();

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;

        [JsonPropertyName("user")]
        public string? User { get; set; }

        [JsonPropertyName("inputs")]
        public Dictionary<string, object> Inputs { get; set; } = new();

        [JsonPropertyName("functions")]
        public List<OpenAIFunction>? Functions { get; set; }

        [JsonPropertyName("function_call")]
        public object? FunctionCall { get; set; }

        [JsonPropertyName("tools")]
        public List<OpenAITool>? Tools { get; set; }

        [JsonPropertyName("tool_choice")]
        public object? ToolChoice { get; set; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }

        [JsonPropertyName("top_p")]
        public double? TopP { get; set; }

        [JsonPropertyName("frequency_penalty")]
        public double? FrequencyPenalty { get; set; }

        [JsonPropertyName("presence_penalty")]
        public double? PresencePenalty { get; set; }

        [JsonPropertyName("stop")]
        public List<string>? Stop { get; set; }
    }

    /// <summary>
    /// OpenAI 消息
    /// </summary>
    public class OpenAIMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public object Content { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("function_call")]
        public OpenAIFunctionCall? FunctionCall { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<OpenAIToolCall>? ToolCalls { get; set; }

        [JsonPropertyName("tool_call_id")]
        public string? ToolCallId { get; set; }
    }

    /// <summary>
    /// OpenAI 函数定义
    /// </summary>
    public class OpenAIFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("parameters")]
        public object? Parameters { get; set; }
    }

    /// <summary>
    /// OpenAI 函数调用
    /// </summary>
    public class OpenAIFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;
    }

    /// <summary>
    /// OpenAI 工具定义
    /// </summary>
    public class OpenAITool
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public OpenAIFunction Function { get; set; } = new();
    }

    /// <summary>
    /// OpenAI 工具调用
    /// </summary>
    public class OpenAIToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public OpenAIFunctionCall Function { get; set; } = new();
    }

    /// <summary>
    /// OpenAI 聊天完成响应
    /// </summary>
    public class OpenAIChatCompletionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("object")]
        public string Object { get; set; } = "chat.completion";

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("choices")]
        public List<OpenAIChoice> Choices { get; set; } = new();

        [JsonPropertyName("usage")]
        public OpenAIUsage? Usage { get; set; }
    }

    /// <summary>
    /// OpenAI 选择项
    /// </summary>
    public class OpenAIChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; set; }

        [JsonPropertyName("delta")]
        public OpenAIMessage? Delta { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    /// <summary>
    /// OpenAI 使用情况
    /// </summary>
    public class OpenAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    /// <summary>
    /// OpenAI 模型列表响应
    /// </summary>
    public class OpenAIModelsResponse
    {
        [JsonPropertyName("object")]
        public string Object { get; set; } = "list";

        [JsonPropertyName("data")]
        public List<OpenAIModel> Data { get; set; } = new();
    }

    /// <summary>
    /// OpenAI 模型
    /// </summary>
    public class OpenAIModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("object")]
        public string Object { get; set; } = "model";

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("owned_by")]
        public string OwnedBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// OpenAI 错误响应
    /// </summary>
    public class OpenAIErrorResponse
    {
        [JsonPropertyName("error")]
        public OpenAIError Error { get; set; } = new();
    }

    /// <summary>
    /// OpenAI 错误
    /// </summary>
    public class OpenAIError
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("param")]
        public string? Param { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}