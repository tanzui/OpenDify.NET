using System.Text.Json.Serialization;

namespace OpenDify.NET.Models
{
    /// <summary>
    /// Dify 聊天消息请求
    /// </summary>
    public class DifyChatMessageRequest
    {
        [JsonPropertyName("inputs")]
        public Dictionary<string, object> Inputs { get; set; } = new();

        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("response_mode")]
        public string ResponseMode { get; set; } = "blocking";

        [JsonPropertyName("user")]
        public string User { get; set; } = "default_user";

        [JsonPropertyName("conversation_id")]
        public string? ConversationId { get; set; }

        [JsonPropertyName("files")]
        public List<DifyFile>? Files { get; set; }

        [JsonPropertyName("auto_generate_name")]
        public bool AutoGenerateName { get; set; } = true;

        [JsonPropertyName("workflow_id")]
        public string? WorkflowId { get; set; }

        [JsonPropertyName("trace_id")]
        public string? TraceId { get; set; }
    }

    /// <summary>
    /// Dify 文件
    /// </summary>
    public class DifyFile
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "image";

        [JsonPropertyName("transfer_method")]
        public string TransferMethod { get; set; } = "local_file";

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("upload_file_id")]
        public string? UploadFileId { get; set; }
    }

    /// <summary>
    /// Dify 聊天消息响应
    /// </summary>
    public class DifyChatMessageResponse
    {
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; } = string.Empty;

        [JsonPropertyName("conversation_id")]
        public string? ConversationId { get; set; }

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = string.Empty;

        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonPropertyName("metadata")]
        public DifyMetadata? Metadata { get; set; }

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("conversation_history")]
        public List<DifyMessage>? ConversationHistory { get; set; }

        [JsonPropertyName("agent_thoughts")]
        public List<DifyAgentThought>? AgentThoughts { get; set; }
    }

    /// <summary>
    /// Dify 元数据
    /// </summary>
    public class DifyMetadata
    {
        [JsonPropertyName("usage")]
        public DifyUsage? Usage { get; set; }

        [JsonPropertyName("retriever_resources")]
        public List<object>? RetrieverResources { get; set; }
    }

    /// <summary>
    /// Dify 使用情况
    /// </summary>
    public class DifyUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("prompt_unit_price")]
        public string PromptUnitPrice { get; set; } = string.Empty;

        [JsonPropertyName("prompt_price_unit")]
        public string PromptPriceUnit { get; set; } = string.Empty;

        [JsonPropertyName("prompt_price_amount")]
        public string PromptPriceAmount { get; set; } = string.Empty;

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("completion_unit_price")]
        public string CompletionUnitPrice { get; set; } = string.Empty;

        [JsonPropertyName("completion_price_unit")]
        public string CompletionPriceUnit { get; set; } = string.Empty;

        [JsonPropertyName("completion_price_amount")]
        public string CompletionPriceAmount { get; set; } = string.Empty;

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }

        [JsonPropertyName("total_price")]
        public string TotalPrice { get; set; } = string.Empty;

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("latency")]
        public double Latency { get; set; }
    }

    /// <summary>
    /// Dify 消息
    /// </summary>
    public class DifyMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Dify Agent 思考
    /// </summary>
    public class DifyAgentThought
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("thought")]
        public string Thought { get; set; } = string.Empty;

        [JsonPropertyName("tool")]
        public string? Tool { get; set; }

        [JsonPropertyName("tool_input")]
        public string? ToolInput { get; set; }

        [JsonPropertyName("observation")]
        public string? Observation { get; set; }

        [JsonPropertyName("tool_process")]
        public string? ToolProcess { get; set; }

        [JsonPropertyName("files")]
        public List<DifyFile>? Files { get; set; }

        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("openai_model")]
        public string? OpenaiModel { get; set; }

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dify 流式响应块
    /// </summary>
    public class DifyStreamChunk
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("message_id")]
        public string? MessageId { get; set; }

        [JsonPropertyName("conversation_id")]
        public string? ConversationId { get; set; }

        [JsonPropertyName("answer")]
        public string? Answer { get; set; }

        [JsonPropertyName("metadata")]
        public DifyMetadata? Metadata { get; set; }

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("task_id")]
        public string? TaskId { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("thought")]
        public string? Thought { get; set; }

        [JsonPropertyName("tool")]
        public string? Tool { get; set; }

        [JsonPropertyName("tool_input")]
        public string? ToolInput { get; set; }

        [JsonPropertyName("observation")]
        public string? Observation { get; set; }

        [JsonPropertyName("files")]
        public List<DifyFile>? Files { get; set; }

        [JsonPropertyName("conversation_history")]
        public List<DifyMessage>? ConversationHistory { get; set; }
    }

    /// <summary>
    /// Dify 应用信息响应
    /// </summary>
    public class DifyAppInfoResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        [JsonPropertyName("icon_background")]
        public string? IconBackground { get; set; }

        [JsonPropertyName("icon_url")]
        public string? IconUrl { get; set; }

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = string.Empty;

        [JsonPropertyName("enable_site")]
        public bool EnableSite { get; set; }

        [JsonPropertyName("enable_api")]
        public bool EnableApi { get; set; }

        [JsonPropertyName("api_token_count")]
        public int ApiTokenCount { get; set; }

        [JsonPropertyName("channel")]
        public string Channel { get; set; } = string.Empty;

        [JsonPropertyName("model_config")]
        public DifyModelConfig? ModelConfig { get; set; }
    }

    /// <summary>
    /// Dify 模型配置
    /// </summary>
    public class DifyModelConfig
    {
        [JsonPropertyName("opening_statement")]
        public string? OpeningStatement { get; set; }

        [JsonPropertyName("suggested_questions")]
        public List<string>? SuggestedQuestions { get; set; }

        [JsonPropertyName("speech_to_text")]
        public Dictionary<string, object>? SpeechToText { get; set; }

        [JsonPropertyName("text_to_speech")]
        public Dictionary<string, object>? TextToSpeech { get; set; }

        [JsonPropertyName("retriever_resource")]
        public Dictionary<string, object>? RetrieverResource { get; set; }

        [JsonPropertyName("annotation_reply")]
        public Dictionary<string, object>? AnnotationReply { get; set; }

        [JsonPropertyName("more_like_this")]
        public Dictionary<string, object>? MoreLikeThis { get; set; }

        [JsonPropertyName("user_input_form")]
        public List<object>? UserInputForm { get; set; }

        [JsonPropertyName("sensitive_word_avoidance")]
        public Dictionary<string, object>? SensitiveWordAvoidance { get; set; }

        [JsonPropertyName("pre_prompt")]
        public string? PrePrompt { get; set; }
    }

    /// <summary>
    /// Dify 文件上传响应
    /// </summary>
    public class DifyFileUploadResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("extension")]
        public string Extension { get; set; } = string.Empty;

        [JsonPropertyName("mime_type")]
        public string MimeType { get; set; } = string.Empty;

        [JsonPropertyName("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }
    }

    /// <summary>
    /// Dify 错误响应
    /// </summary>
    public class DifyErrorResponse
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }
    }
}