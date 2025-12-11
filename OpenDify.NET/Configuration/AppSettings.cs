namespace OpenDify.NET.Configuration
{
    /// <summary>
    /// 应用程序配置模型
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Dify 配置
        /// </summary>
        public DifySettings Dify { get; set; } = new();

        /// <summary>
        /// OpenAI 兼容配置
        /// </summary>
        public OpenAISettings OpenAI { get; set; } = new();

        /// <summary>
        /// 服务器配置
        /// </summary>
        public ServerSettings Server { get; set; } = new();

        /// <summary>
        /// 日志配置
        /// </summary>
        public LoggingSettings Logging { get; set; } = new();
    }

    /// <summary>
    /// Dify API 配置
    /// </summary>
    public class DifySettings
    {
        /// <summary>
        /// Dify API 基础URL
        /// </summary>
        public string ApiBase { get; set; } = "http://localhost:8186/v1";

        /// <summary>
        /// Dify API 密钥列表
        /// </summary>
        public List<string> ApiKeys { get; set; } = new();

        /// <summary>
        /// 会话记忆模式：1=history_message模式，2=零宽字符模式
        /// </summary>
        public int ConversationMemoryMode { get; set; } = 1;
    }

    /// <summary>
    /// OpenAI 兼容配置
    /// </summary>
    public class OpenAISettings
    {
        /// <summary>
        /// 有效的API密钥列表
        /// </summary>
        public List<string> ValidApiKeys { get; set; } = new();
    }

    /// <summary>
    /// 服务器配置
    /// </summary>
    public class ServerSettings
    {
        /// <summary>
        /// 服务器主机地址
        /// </summary>
        public string Host { get; set; } = "127.0.0.1";

        /// <summary>
        /// 服务器端口
        /// </summary>
        public int Port { get; set; } = 5003;
    }

    /// <summary>
    /// 日志配置
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// 日志级别
        /// </summary>
        public string LogLevel { get; set; } = "Information";
    }
}