using OpenDify.NET.Configuration;
using OpenDify.NET.Models;
using System.Text.Json;

namespace OpenDify.NET.Services
{
    /// <summary>
    /// Dify 模型管理器，负责管理 Dify 应用和 API 密钥映射
    /// </summary>
    public class DifyModelManager
    {
        private readonly ILogger<DifyModelManager> _logger;
        private readonly HttpClient _httpClient;
        private readonly DifySettings _settings;
        
        private readonly List<string> _apiKeys = new();
        private readonly Dictionary<string, string> _nameToApiKey = new();
        private readonly Dictionary<string, string> _apiKeyToName = new();
        private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

        public DifyModelManager(
            ILogger<DifyModelManager> logger,
            HttpClient httpClient,
            DifySettings settings)
        {
            _logger = logger;
            _httpClient = httpClient;
            _settings = settings;
            LoadApiKeys();
            
            // 初始化测试数据
            InitializeTestData();
        }

        /// <summary>
        /// 从配置加载 API 密钥
        /// </summary>
        private void LoadApiKeys()
        {
            _apiKeys.Clear();
            _apiKeys.AddRange(_settings.ApiKeys.Where(key => !string.IsNullOrWhiteSpace(key)));
            _logger.LogInformation("已加载 {count} 个 API 密钥", _apiKeys.Count);
        }

        /// <summary>
        /// 初始化测试数据
        /// </summary>
        private void InitializeTestData()
        {
            // 为测试目的，手动添加一个模型映射
            var testApiKey = _apiKeys.FirstOrDefault();
            if (!string.IsNullOrEmpty(testApiKey))
            {
                var modelName = "测试functioncall";
                _nameToApiKey[modelName] = testApiKey;
                _apiKeyToName[testApiKey] = modelName;
                
                _logger.LogInformation("测试模式：已映射模型 '{modelName}' 到 API Key: {apiKeyPrefix}...",
                    modelName, testApiKey.Substring(0, Math.Min(8, testApiKey.Length)));
            }
        }

        /// <summary>
        /// 异步获取 Dify 应用信息
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <returns>应用名称，如果获取失败返回 null</returns>
        public async Task<string?> FetchAppInfoAsync(string apiKey)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.ApiBase}/info");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                
                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var appInfo = JsonSerializer.Deserialize<DifyAppInfoResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return appInfo?.Name;
                }
                else
                {
                    _logger.LogError("获取应用信息失败，API Key: {apiKeyPrefix}...，状态码: {statusCode}",
                        apiKey.Substring(0, Math.Min(8, apiKey.Length)), response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取应用信息时发生错误，API Key: {apiKeyPrefix}...",
                    apiKey.Substring(0, Math.Min(8, apiKey.Length)));
                return null;
            }
        }

        /// <summary>
        /// 异步刷新所有应用信息
        /// </summary>
        public virtual async Task RefreshModelInfoAsync()
        {
            await _refreshSemaphore.WaitAsync();
            try
            {
                _nameToApiKey.Clear();
                _apiKeyToName.Clear();

                var tasks = _apiKeys.Select(async apiKey =>
                {
                    var appName = await FetchAppInfoAsync(apiKey);
                    if (!string.IsNullOrWhiteSpace(appName))
                    {
                        _nameToApiKey[appName] = apiKey;
                        _apiKeyToName[apiKey] = appName;
                        _logger.LogInformation("已映射应用 '{appName}' 到 API Key: {apiKeyPrefix}...",
                            appName, apiKey.Substring(0, Math.Min(8, apiKey.Length)));
                    }
                });

                await Task.WhenAll(tasks);
                _logger.LogInformation("模型信息刷新完成，共映射 {count} 个应用", _nameToApiKey.Count);
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        /// <summary>
        /// 根据模型名称获取 API 密钥
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <returns>API 密钥，如果未找到返回 null</returns>
        public virtual string? GetApiKey(string modelName)
        {
            _nameToApiKey.TryGetValue(modelName, out var apiKey);
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("未找到模型 '{modelName}' 对应的 API 密钥", modelName);
            }
            return apiKey;
        }

        /// <summary>
        /// 根据 API 密钥获取模型名称
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <returns>模型名称，如果未找到返回 null</returns>
        public virtual string? GetModelName(string apiKey)
        {
            _apiKeyToName.TryGetValue(apiKey, out var modelName);
            return modelName;
        }

        /// <summary>
        /// 获取可用模型列表
        /// </summary>
        /// <returns>OpenAI 格式的模型列表</returns>
        public virtual List<OpenAIModel> GetAvailableModels()
        {
            return _nameToApiKey.Keys.Select(name => new OpenAIModel
            {
                Id = name,
                Object = "model",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                OwnedBy = "dify"
            }).ToList();
        }

        /// <summary>
        /// 获取所有 API 密钥
        /// </summary>
        /// <returns>API 密钥列表</returns>
        public IReadOnlyList<string> GetApiKeys()
        {
            return _apiKeys.AsReadOnly();
        }

        /// <summary>
        /// 检查模型是否受支持
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <returns>如果支持返回 true</returns>
        public bool IsModelSupported(string modelName)
        {
            return _nameToApiKey.ContainsKey(modelName);
        }

        /// <summary>
        /// 获取受支持的模型名称列表
        /// </summary>
        /// <returns>模型名称列表</returns>
        public virtual IReadOnlyList<string> GetSupportedModelNames()
        {
            return _nameToApiKey.Keys.ToList().AsReadOnly();
        }
    }
}