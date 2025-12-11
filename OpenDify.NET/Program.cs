using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDify.NET.Configuration;
using OpenDify.NET.Services;
using System.Text.Json.Serialization;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// 配置 Kestrel 服务器
builder.WebHost.ConfigureKestrel((context, options) =>
{
    var isDocker = context.HostingEnvironment.IsProduction();
    var serverPort = context.Configuration.GetValue<int>("Server:Port", 5003);
    
    if (isDocker)
    {
        // 在生产环境（Docker）中监听所有接口
        options.ListenAnyIP(serverPort);
    }
    else
    {
        // 在开发环境中只监听 localhost
        options.ListenLocalhost(serverPort);
    }
});

// 配置服务
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

// 配置中间件管道
ConfigureMiddleware(app);

// 运行应用
app.Run();

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // 添加控制器
    services.AddControllers()
        .AddJsonOptions(options =>
        {
            // 配置 JSON 序列化选项
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    // 添加 API 文档
    services.AddEndpointsApiExplorer();
    // 暂时注释掉 Swagger，直到添加 Swashbuckle 包
    // services.AddSwaggerGen(c =>
    // {
    //     c.SwaggerDoc("v1", new() {
    //         Title = "OpenDify.NET API",
    //         Version = "v1",
    //         Description = "OpenAI 兼容的 Dify 代理服务器 API"
    //     });
    //
    //     // 包含 XML 注释
    //     var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    //     var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    //     if (File.Exists(xmlPath))
    //     {
    //         c.IncludeXmlComments(xmlPath);
    //     }
    // });

    // 添加 CORS
    services.AddCors(options =>
    {
        options.AddDefaultPolicy(builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
    });

    // 配置并注册 AppSettings
    var appSettings = new AppSettings();
    configuration.Bind(appSettings);
    services.AddSingleton(appSettings);
    
    // 直接从配置中获取 DifySettings
    var difySettings = new DifySettings();
    configuration.GetSection("Dify").Bind(difySettings);
    services.AddSingleton(difySettings);

    // 注册 HttpClient
    services.AddHttpClient();

    // 注册自定义服务
    services.AddSingleton<DifyModelManager>();
    services.AddSingleton<ConversationMemoryManager>();
    services.AddSingleton<RequestTransformationService>();
    services.AddSingleton<ResponseTransformationService>();
    services.AddSingleton<FileUploadService>();
    services.AddSingleton<StreamingService>();

    // 添加日志
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.AddDebug();
    });

    // 添加内存缓存（用于会话管理）
    services.AddMemoryCache();
}

static void ConfigureMiddleware(WebApplication app)
{
    // 启用 CORS
    app.UseCors();

    // 启用路由
    app.UseRouting();

    // 添加全局异常处理中间件
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // 映射控制器
    app.MapControllers();

    // 健康检查端点
    app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });

    // 根路径信息
    app.MapGet("/", () => new {
        message = "OpenDify.NET API Server",
        version = "1.0.0",
        endpoints = new {
            health = "/health",
            chat = "/v1/chat/completions",
            models = "/v1/models"
        }
    });
}

/// <summary>
/// 全局异常处理中间件
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            error = new
            {
                message = exception.Message,
                type = exception.GetType().Name,
                code = "internal_error"
            }
        };

        switch (exception)
        {
            case ArgumentException:
                context.Response.StatusCode = 400;
                break;
            case UnauthorizedAccessException:
                context.Response.StatusCode = 401;
                break;
            case KeyNotFoundException:
                context.Response.StatusCode = 404;
                break;
            case InvalidOperationException:
                context.Response.StatusCode = 400;
                break;
            default:
                context.Response.StatusCode = 500;
                break;
        }

        var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(jsonResponse);
    }
}