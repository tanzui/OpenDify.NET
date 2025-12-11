using OpenDify.NET.Configuration;
using OpenDify.NET.Models;
using System.Text.Json;

namespace OpenDify.NET.Services
{
    /// <summary>
    /// 文件上传服务，负责处理图片上传到 Dify
    /// </summary>
    public class FileUploadService
    {
        private readonly ILogger<FileUploadService> _logger;
        private readonly HttpClient _httpClient;
        private readonly DifySettings _settings;

        public FileUploadService(
            ILogger<FileUploadService> logger,
            HttpClient httpClient,
            DifySettings settings)
        {
            _logger = logger;
            _httpClient = httpClient;
            _settings = settings;
        }

        /// <summary>
        /// 上传图片到 Dify
        /// </summary>
        /// <param name="imageUrl">图片URL或base64数据</param>
        /// <param name="userId">用户ID</param>
        /// <returns>上传的文件信息，如果失败返回null</returns>
        public async Task<UploadedImage?> UploadImageAsync(string imageUrl, string userId = "default_user")
        {
            try
            {
                byte[] imageBytes;
                string fileName;
                string mimeType;

                if (imageUrl.StartsWith("data:image"))
                {
                    // 处理 base64 图片数据
                    var result = ProcessBase64Image(imageUrl);
                    if (result == null)
                    {
                        return null;
                    }

                    imageBytes = result.ImageBytes;
                    fileName = result.FileName;
                    mimeType = result.MimeType;
                }
                else if (imageUrl.StartsWith("http"))
                {
                    // 处理网络图片URL
                    var result = await DownloadImageFromUrlAsync(imageUrl);
                    if (result == null)
                    {
                        return null;
                    }

                    imageBytes = result.ImageBytes;
                    fileName = result.FileName;
                    mimeType = result.MimeType;
                }
                else
                {
                    _logger.LogError("不支持的图片格式: {imageUrl}", imageUrl);
                    return null;
                }

                // 上传到 Dify
                return await UploadToDifyAsync(imageBytes, fileName, mimeType, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传图片失败: {imageUrl}", imageUrl);
                return null;
            }
        }

        /// <summary>
        /// 处理 base64 图片数据
        /// </summary>
        private ImageData? ProcessBase64Image(string base64Data)
        {
            try
            {
                // 提取实际的base64数据 (去除data:image/*;base64,前缀)
                var base64Content = base64Data.Split(',')[1];
                var imageBytes = Convert.FromBase64String(base64Content);

                // 检测图片类型
                var mimeType = DetectImageMimeType(base64Data);
                var extension = GetImageExtension(mimeType);
                var fileName = $"image_{Guid.NewGuid()}{extension}";

                _logger.LogInformation("处理 base64 图片，大小: {size} bytes，类型: {mimeType}", 
                    imageBytes.Length, mimeType);

                return new ImageData
                {
                    ImageBytes = imageBytes,
                    FileName = fileName,
                    MimeType = mimeType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理 base64 图片数据失败");
                return null;
            }
        }

        /// <summary>
        /// 从URL下载图片
        /// </summary>
        private async Task<ImageData?> DownloadImageFromUrlAsync(string imageUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(imageUrl);
                response.EnsureSuccessStatusCode();

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                var mimeType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
                var extension = GetImageExtension(mimeType);
                var fileName = $"image_{Guid.NewGuid()}{extension}";

                _logger.LogInformation("下载图片完成，URL: {imageUrl}，大小: {size} bytes，类型: {mimeType}", 
                    imageUrl, imageBytes.Length, mimeType);

                return new ImageData
                {
                    ImageBytes = imageBytes,
                    FileName = fileName,
                    MimeType = mimeType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下载图片失败: {imageUrl}", imageUrl);
                return null;
            }
        }

        /// <summary>
        /// 上传图片到 Dify
        /// </summary>
        private async Task<UploadedImage?> UploadToDifyAsync(
            byte[] imageBytes, 
            string fileName, 
            string mimeType, 
            string userId)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var imageContent = new ByteArrayContent(imageBytes);
                
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
                content.Add(imageContent, "file", fileName);
                content.Add(new StringContent(userId), "user");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.ApiBase}/files/upload");
                request.Content = content;
                
                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var fileResponse = JsonSerializer.Deserialize<DifyFileUploadResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (fileResponse != null)
                    {
                        _logger.LogInformation("图片上传成功，文件ID: {fileId}，大小: {size} bytes", 
                            fileResponse.Id, fileResponse.Size);

                        return new UploadedImage
                        {
                            FileId = fileResponse.Id,
                            OriginalUrl = "", // 上传后不再需要原始URL
                            FileName = fileResponse.Name,
                            Size = fileResponse.Size,
                            MimeType = fileResponse.MimeType
                        };
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("图片上传失败，状态码: {statusCode}，响应: {response}", 
                        response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传图片到 Dify 失败");
            }

            return null;
        }

        /// <summary>
        /// 检测图片 MIME 类型
        /// </summary>
        private string DetectImageMimeType(string base64Data)
        {
            // 从 data URL 中提取 MIME 类型
            if (base64Data.StartsWith("data:image/"))
            {
                var mimeTypeEnd = base64Data.IndexOf(';');
                if (mimeTypeEnd > 0)
                {
                    return base64Data.Substring(5, mimeTypeEnd - 5); // 去掉 "data:" 前缀
                }
            }

            // 如果无法从 data URL 中提取，通过文件头检测
            return "image/png"; // 默认值
        }

        /// <summary>
        /// 根据 MIME 类型获取文件扩展名
        /// </summary>
        private string GetImageExtension(string mimeType)
        {
            return mimeType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".png"
            };
        }

        /// <summary>
        /// 验证图片格式是否受支持
        /// </summary>
        private bool IsSupportedImageFormat(string mimeType)
        {
            var supportedFormats = new[] { "image/png", "image/jpeg", "image/jpg", "image/webp", "image/gif" };
            return supportedFormats.Contains(mimeType.ToLowerInvariant());
        }

        /// <summary>
        /// 批量上传图片
        /// </summary>
        /// <param name="imageUrls">图片URL列表</param>
        /// <param name="userId">用户ID</param>
        /// <returns>上传结果列表</returns>
        public async Task<List<UploadedImage>> UploadImagesAsync(
            List<string> imageUrls, 
            string userId = "default_user")
        {
            var results = new List<UploadedImage>();
            var tasks = imageUrls.Select(async url =>
            {
                var result = await UploadImageAsync(url, userId);
                if (result != null)
                {
                    lock (results)
                    {
                        results.Add(result);
                    }
                }
                return result;
            });

            await Task.WhenAll(tasks);
            
            _logger.LogInformation("批量上传完成，成功: {successCount}/{totalCount}", 
                results.Count, imageUrls.Count);

            return results;
        }

        /// <summary>
        /// 获取文件预览URL
        /// </summary>
        /// <param name="fileId">文件ID</param>
        /// <returns>预览URL</returns>
        public string GetFilePreviewUrl(string fileId)
        {
            return $"{_settings.ApiBase}/files/{fileId}/preview";
        }

        /// <summary>
        /// 删除已上传的文件（如果Dify支持的话）
        /// </summary>
        /// <param name="fileId">文件ID</param>
        /// <param name="apiKey">API密钥</param>
        /// <returns>是否删除成功</returns>
        public async Task<bool> DeleteFileAsync(string fileId, string apiKey)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{_settings.ApiBase}/files/{fileId}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                
                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("文件删除成功: {fileId}", fileId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("文件删除失败: {fileId}，状态码: {statusCode}", fileId, response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除文件时发生错误: {fileId}", fileId);
                return false;
            }
        }
    }

    /// <summary>
    /// 图片数据
    /// </summary>
    internal class ImageData
    {
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
    }
}