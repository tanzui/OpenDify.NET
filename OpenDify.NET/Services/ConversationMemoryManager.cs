using OpenDify.NET.Configuration;
using OpenDify.NET.Models;

namespace OpenDify.NET.Services
{
    /// <summary>
    /// 会话记忆管理器，处理不同模式的会话记忆功能
    /// </summary>
    public class ConversationMemoryManager
    {
        private readonly ILogger<ConversationMemoryManager> _logger;
        private readonly DifySettings _settings;

        public ConversationMemoryManager(
            ILogger<ConversationMemoryManager> logger,
            DifySettings settings)
        {
            _logger = logger;
            _settings = settings;
        }

        /// <summary>
        /// 零宽字符映射表
        /// </summary>
        private static readonly Dictionary<char, string> CharToZeroWidth = new()
        {
            { '0', "\u200b" },  // 零宽空格
            { '1', "\u200c" },  // 零宽非连接符
            { '2', "\u200d" },  // 零宽连接符
            { '3', "\ufeff" },  // 零宽非断空格
            { '4', "\u2060" },  // 词组连接符
            { '5', "\u180e" },  // 蒙古语元音分隔符
            { '6', "\u2061" },  // 函数应用
            { '7', "\u2062" }   // 不可见乘号
        };

        /// <summary>
        /// 零宽字符到数字的映射表
        /// </summary>
        private static readonly Dictionary<string, char> ZeroWidthToChar = new()
        {
            { "\u200b", '0' },  // 零宽空格
            { "\u200c", '1' },  // 零宽非连接符
            { "\u200d", '2' },  // 零宽连接符
            { "\ufeff", '3' },  // 零宽非断空格
            { "\u2060", '4' },  // 词组连接符
            { "\u180e", '5' },  // 蒙古语元音分隔符
            { "\u2061", '6' },  // 函数应用
            { "\u2062", '7' }   // 不可见乘号
        };

        /// <summary>
        /// 处理消息历史，根据配置的模式转换消息
        /// </summary>
        /// <param name="messages">OpenAI 消息列表</param>
        /// <param name="conversationId">会话ID（零宽字符模式时使用）</param>
        /// <returns>处理后的查询内容和会话ID</returns>
        public virtual (string Query, string? ConversationId) ProcessMessageHistory(
            List<OpenAIMessage> messages,
            string? conversationId = null)
        {
            if (_settings.ConversationMemoryMode == 2)
            {
                return ProcessZeroWidthMode(messages, conversationId);
            }
            else
            {
                return ProcessHistoryMessageMode(messages);
            }
        }

        /// <summary>
        /// 处理零宽字符模式
        /// </summary>
        private (string Query, string? ConversationId) ProcessZeroWidthMode(
            List<OpenAIMessage> messages, 
            string? conversationId)
        {
            // 提取系统消息内容
            var systemContent = ExtractSystemContent(messages);
            
            // 获取用户消息
            var userMessage = messages.LastOrDefault(m => m.Role != "system") ?? new OpenAIMessage();
            var userQuery = GetMessageContent(userMessage.Content);

            // 如果没有会话ID，尝试从历史消息中解码
            if (string.IsNullOrEmpty(conversationId))
            {
                conversationId = DecodeConversationIdFromHistory(messages);
            }

            // 如果有系统消息且是首次对话，将系统内容添加到用户查询前
            if (!string.IsNullOrEmpty(systemContent) && string.IsNullOrEmpty(conversationId))
            {
                userQuery = $"系统指令: {systemContent}\n\n用户问题: {userQuery}";
                _logger.LogInformation("[零宽字符模式] 首次对话，添加system内容到查询前");
            }

            _logger.LogInformation("[零宽字符模式] 处理完成，会话ID: {conversationId}", conversationId);
            return (userQuery, conversationId);
        }

        /// <summary>
        /// 处理历史消息模式
        /// </summary>
        private (string Query, string? ConversationId) ProcessHistoryMessageMode(List<OpenAIMessage> messages)
        {
            var systemContent = ExtractSystemContent(messages);
            var userMessage = messages.LastOrDefault(m => m.Role != "system") ?? new OpenAIMessage();
            var userQuery = GetMessageContent(userMessage.Content);

            if (messages.Count > 1)
            {
                var historyMessages = new List<string>();
                var hasSystemInHistory = false;

                // 检查历史消息中是否已经包含system消息
                foreach (var msg in messages.SkipLast(1)) // 除了最后一条消息
                {
                    var role = msg.Role;
                    var content = GetMessageContent(msg.Content);
                    if (!string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(content))
                    {
                        if (role == "system")
                        {
                            hasSystemInHistory = true;
                        }
                        historyMessages.Add($"{role}: {content}");
                    }
                }

                // 如果历史中没有system消息但现在有system消息，则添加到历史的最前面
                if (!string.IsNullOrEmpty(systemContent) && !hasSystemInHistory)
                {
                    historyMessages.Insert(0, $"system: {systemContent}");
                    _logger.LogInformation("[history_message模式] 添加system内容到历史消息前");
                }

                // 将历史消息添加到查询中
                if (historyMessages.Any())
                {
                    var historyContext = string.Join("\n\n", historyMessages);
                    userQuery = $"<history>\n{historyContext}\n</history>\n\n用户当前问题: {userQuery}";
                }
            }
            else if (!string.IsNullOrEmpty(systemContent)) // 没有历史消息但有system消息
            {
                userQuery = $"系统指令: {systemContent}\n\n用户问题: {userQuery}";
                _logger.LogInformation("[history_message模式] 首次对话，添加system内容到查询前");
            }

            _logger.LogInformation("[history_message模式] 处理完成");
            return (userQuery, null);
        }

        /// <summary>
        /// 从消息列表中提取系统消息内容
        /// </summary>
        private string ExtractSystemContent(List<OpenAIMessage> messages)
        {
            var systemMessage = messages.FirstOrDefault(m => m.Role == "system");
            var content = GetMessageContent(systemMessage?.Content);
            
            if (!string.IsNullOrEmpty(content))
            {
                _logger.LogInformation("找到系统消息: {content}...", content.Substring(0, Math.Min(100, content.Length)));
            }
            
            return content;
        }

        /// <summary>
        /// 获取消息内容，支持字符串和列表格式
        /// </summary>
        private string GetMessageContent(object content)
        {
            return content switch
            {
                string str => str,
                List<object> list when list.Any() => string.Join("\n", 
                    list.OfType<Dictionary<string, object>>()
                        .Where(d => d.ContainsKey("text"))
                        .Select(d => d["text"]?.ToString() ?? "")),
                _ => ""
            };
        }

        /// <summary>
        /// 从历史消息中解码会话ID
        /// </summary>
        private string? DecodeConversationIdFromHistory(List<OpenAIMessage> messages)
        {
            // 遍历历史消息，找到最近的assistant消息
            foreach (var msg in messages.AsEnumerable().Reverse().Skip(1)) // 除了最后一条消息
            {
                if (msg.Role == "assistant")
                {
                    var content = GetMessageContent(msg.Content);
                    var conversationId = DecodeConversationId(content);
                    if (!string.IsNullOrEmpty(conversationId))
                    {
                        return conversationId;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 将会话ID编码为不可见的字符序列
        /// </summary>
        public virtual string EncodeConversationId(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                return string.Empty;
            }

            try
            {
                // 使用Base64编码减少长度
                var base64Encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(conversationId));
                
                var result = new System.Text.StringBuilder();
                
                // 将Base64字符串转换为八进制数字
                foreach (var c in base64Encoded)
                {
                    var val = GetBase64CharValue(c);
                    
                    // 每个Base64字符可以产生2个3位数字
                    var first = (val >> 3) & 0x7;
                    var second = val & 0x7;
                    
                    result.Append(CharToZeroWidth[first.ToString()[0]]);
                    
                    if (c != '=') // 不编码填充字符的后半部分
                    {
                        result.Append(CharToZeroWidth[second.ToString()[0]]);
                    }
                }
                
                return result.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "编码会话ID时发生错误: {conversationId}", conversationId);
                return string.Empty;
            }
        }

        /// <summary>
        /// 从消息内容中解码会话ID
        /// </summary>
        public virtual string? DecodeConversationId(string content)
        {
            try
            {
                if (string.IsNullOrEmpty(content))
                {
                    return null;
                }

                // 提取最后一段零宽字符序列
                var spaceChars = new List<char>();
                for (int i = content.Length - 1; i >= 0; i--)
                {
                    if (ZeroWidthToChar.ContainsKey(content[i].ToString()))
                    {
                        spaceChars.Add(content[i]);
                    }
                    else
                    {
                        break;
                    }
                }

                if (!spaceChars.Any())
                {
                    return null;
                }

                // 将零宽字符转换回Base64字符串
                spaceChars.Reverse();
                var base64Chars = new List<char>();
                
                for (int i = 0; i < spaceChars.Count; i += 2)
                {
                    var first = ZeroWidthToChar[spaceChars[i].ToString()];
                    var firstVal = (byte)first;
                    
                    if (i + 1 < spaceChars.Count)
                    {
                        var second = ZeroWidthToChar[spaceChars[i + 1].ToString()];
                        var secondVal = (byte)second;
                        var val = (firstVal << 3) | secondVal;
                        base64Chars.Add(GetBase64CharFromValue(val));
                    }
                    else
                    {
                        var val = firstVal << 3;
                        base64Chars.Add(GetBase64CharFromValue(val));
                    }
                }

                // 添加Base64填充
                var padding = base64Chars.Count % 4;
                if (padding != 0)
                {
                    base64Chars.AddRange(Enumerable.Repeat('=', 4 - padding));
                }

                // 解码Base64字符串
                var base64Str = new string(base64Chars.ToArray());
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Str));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "解码会话ID失败");
                return null;
            }
        }

        /// <summary>
        /// 获取Base64字符的数值
        /// </summary>
        private int GetBase64CharValue(char c)
        {
            return c switch
            {
                >= 'A' and <= 'Z' => c - 'A',
                >= 'a' and <= 'z' => c - 'a' + 26,
                >= '0' and <= '9' => c - '0' + 52,
                '+' => 62,
                '/' => 63,
                '=' => 0,
                _ => 0
            };
        }

        /// <summary>
        /// 根据数值获取Base64字符
        /// </summary>
        private char GetBase64CharFromValue(int val)
        {
            return val switch
            {
                < 26 => (char)('A' + val),
                < 52 => (char)('a' + (val - 26)),
                < 62 => (char)('0' + (val - 52)),
                62 => '+',
                63 => '/',
                _ => '='
            };
        }

        /// <summary>
        /// 处理响应内容，根据模式添加会话ID
        /// </summary>
        public virtual string ProcessResponseContent(string content, string? conversationId, List<DifyMessage>? conversationHistory)
        {
            if (_settings.ConversationMemoryMode != 2 || string.IsNullOrEmpty(conversationId))
            {
                return content;
            }

            // 检查历史消息中是否已经有会话ID
            var hasConversationId = false;
            if (conversationHistory != null)
            {
                foreach (var msg in conversationHistory)
                {
                    if (msg.Role == "assistant")
                    {
                        if (DecodeConversationId(msg.Content) != null)
                        {
                            hasConversationId = true;
                            break;
                        }
                    }
                }
            }

            // 只在新会话且历史消息中没有会话ID时插入
            if (!hasConversationId)
            {
                _logger.LogInformation("在响应中插入会话ID: {conversationId}", conversationId);
                var encoded = EncodeConversationId(conversationId);
                return content + encoded;
            }

            return content;
        }
    }
}