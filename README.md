# OpenDify.NET

一个基于 .NET 10 的 OpenAI 兼容 Dify 代理服务器，提供完整的 OpenAI API 兼容性，支持对话、流式响应、图片上传、函数调用和工具执行。

## 文档语言

🇨🇳 [中文](README.md) | 🇺🇸 [English](README_EN.md)

## 功能特性

- ✅ **OpenAI API 完全兼容** - 支持标准的 OpenAI API 格式
- ✅ **流式响应支持** - 实时流式输出响应内容
- ✅ **图片上传处理** - 支持多模态对话中的图片上传
- ✅ **函数调用支持** - 完整的 Function Calling 功能
- ✅ **会话记忆管理** - 支持历史消息和零宽字符两种记忆模式
- ✅ **多应用管理** - 支持管理多个 Dify 应用
- ✅ **JWT 认证** - 安全的 API 认证机制
- ✅ **错误处理** - 完善的错误处理和日志记录

## 快速开始

### 1. 配置环境

编辑 `OpenDify.NET/appsettings.json` 文件，根据你的环境修改配置：

```json
{
  "Dify": {
    "ApiBase": "http://192.168.0.117:8186/v1",
    "ApiKeys": [
      "app-OtfA94FWDwPw5YAmo8lj0kJ9"
    ],
    "ConversationMemoryMode": 1
  },
  "OpenAI": {
    "ValidApiKeys": [
      "sk-abc123",
      "sk-def456"
    ]
  },
  "Server": {
    "Port": 5003
  }
}
```

**配置说明：**
- `Dify.ApiBase`: Dify API 基础URL
- `Dify.ApiKeys`: Dify API 密钥列表
- `Dify.ConversationMemoryMode`: 会话记忆模式（1=history_message，2=zero_width_character）
- `OpenAI.ValidApiKeys`: OpenAI 兼容 API 密钥列表
- `Server.Port`: 服务器监听端口

### 2. 运行应用

```bash
# 使用 .NET CLI
dotnet run

# 或使用 Visual Studio
# 打开 OpenDify.NET.csproj 并运行
```

应用将在 `http://localhost:5003` 启动。

### 3. 测试 API

#### 获取模型列表

```bash
curl -X GET "http://localhost:5003/v1/models" \
  -H "Authorization: Bearer sk-abc123"
```

#### 发送聊天请求

```bash
curl -X POST "http://localhost:5003/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-abc123" \
  -d '{
    "model": "测试functioncall",
    "messages": [
      {
        "role": "user",
        "content": "你好，请介绍一下自己。"
      }
    ]
  }'
```

#### 流式响应

```bash
curl -X POST "http://localhost:5003/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-abc123" \
  -d '{
    "model": "测试functioncall",
    "messages": [
      {
        "role": "user",
        "content": "请写一首关于春天的诗"
      }
    ],
    "stream": true
  }'
```

#### 图片上传

```bash
curl -X POST "http://localhost:5003/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-abc123" \
  -d '{
    "model": "测试functioncall",
    "messages": [
      {
        "role": "user",
        "content": [
          {
            "type": "text",
            "text": "请描述这张图片的内容"
          },
          {
            "type": "image_url",
            "image_url": {
              "url": "https://example.com/image.jpg"
            }
          }
        ]
      }
    ]
  }'
```

#### 函数调用

```bash
curl -X POST "http://localhost:5003/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-abc123" \
  -d '{
    "model": "测试functioncall",
    "messages": [
      {
        "role": "user",
        "content": "现在北京几点了？"
      }
    ],
    "functions": [
      {
        "name": "get_current_time",
        "description": "获取当前时间",
        "parameters": {
          "type": "object",
          "properties": {
            "timezone": {
              "type": "string",
              "description": "时区，例如：Asia/Shanghai"
            }
          },
          "required": ["timezone"]
        }
      }
    ]
  }'
```

## API 端点

| 端点 | 方法 | 描述 |
|------|------|------|
| `/v1/models` | GET | 获取可用模型列表 |
| `/v1/chat/completions` | POST | 发送聊天请求 |
| `/health` | GET | 健康检查 |
| `/` | GET | API 信息 |

## 配置说明

### 应用配置 (appsettings.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "System.Net.Http.HttpClient": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5003"
      }
    }
  },
  "Dify": {
    "ApiBase": "http://192.168.0.117:8186/v1",
    "ApiKeys": [
      "app-OtfA94FWDwPw5YAmo8lj0kJ9"
    ],
    "ConversationMemoryMode": 1
  },
  "OpenAI": {
    "ValidApiKeys": [
      "sk-abc123",
      "sk-def456"
    ]
  },
  "Server": {
    "Port": 5003
  }
}
```

### 配置参数说明

| 参数 | 描述 | 默认值 |
|------|------|--------|
| `Dify.ApiBase` | Dify API 基础URL | `http://192.168.0.117:8186/v1` |
| `Dify.ApiKeys` | Dify API 密钥列表 | - |
| `Dify.ConversationMemoryMode` | 会话记忆模式 | `1` |
| `OpenAI.ValidApiKeys` | OpenAI 兼容 API 密钥列表 | - |
| `Server.Port` | 服务器端口 | `5003` |

### 会话记忆模式

1. **1 (history_message)** - 使用标准的对话历史记录模式（默认）
2. **2 (zero_width_character)** - 使用零宽字符隐藏记忆信息

### 环境特定配置

项目支持不同环境的配置文件：
- `appsettings.json` - 基础配置
- `appsettings.Development.json` - 开发环境配置
- `appsettings.Production.json` - 生产环境配置

## 项目结构

```
OpenDify.NET/
├── Configuration/          # 配置类
│   └── AppSettings.cs
├── Controllers/            # API 控制器
│   └── OpenAIController.cs
├── Models/                 # 数据模型
│   ├── OpenAIModels.cs
│   └── DifyModels.cs
├── Services/               # 业务服务
│   ├── DifyModelManager.cs
│   ├── ConversationMemoryManager.cs
│   ├── RequestTransformationService.cs
│   ├── ResponseTransformationService.cs
│   ├── FileUploadService.cs
│   └── StreamingService.cs
├── Properties/             # 属性配置
│   └── launchSettings.json
├── Program.cs              # 应用程序入口
├── appsettings.json        # 应用配置
├── appsettings.Development.json  # 开发环境配置
├── appsettings.Production.json   # 生产环境配置
├── OpenDify.NET.csproj     # 项目文件
├── MIGRATION_AND_CONTEXT.md # 迁移和上下文文档
└── README.md               # 项目文档

OpenDify.NET.Tests/         # 测试项目
├── Controllers/            # 控制器测试
├── Services/               # 服务测试
├── IntegrationTests/       # 集成测试
└── OpenDify.NET.Tests.csproj
```

## 开发指南

### 添加新的 API 端点

1. 在 `Controllers/OpenAIController.cs` 中添加新的方法
2. 在 `Models/` 中定义相应的请求/响应模型
3. 在 `Services/` 中实现业务逻辑

### 自定义认证

可以在 `Program.cs` 中修改认证中间件来支持其他认证方式。

### 扩展功能

项目采用模块化设计，可以轻松扩展新功能：

- 添加新的 Dify API 集成
- 实现其他 AI 模型的代理
- 添加监控和分析功能

## 部署

### Docker 部署

#### 构建镜像

```bash
# 在项目根目录执行
docker build -t opendify-net:latest .
```

#### 运行容器

```bash
# 基本运行
docker run -d \
  --name opendify-net \
  -p 5003:5003 \
  opendify-net:latest

# 使用 Docker Compose（推荐）
docker-compose up -d
```

#### Docker Compose 配置

项目已包含 `docker-compose.yml` 文件，配置如下：

```yaml
version: '3.8'

services:
  opendify-net:
    build: .
    container_name: opendify-net
    ports:
      - "5003:5003"
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5003/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

### 生产环境配置

在生产环境中，建议：

1. **使用配置文件**：修改 `appsettings.Production.json` 文件
2. **容器编排环境**：使用 Kubernetes ConfigMap/Secret
3. **云服务配置**：Azure App Service Configuration 等
4. **环境变量**：可通过环境变量覆盖配置文件设置

## 许可证

本项目基于 MIT 许可证开源。

## 贡献

欢迎提交 Issue 和 Pull Request 来改进这个项目。

项目地址：https://github.com/tanzui/OpenDify.NET

## 支持

如果你在使用过程中遇到问题，请：

1. 查看本文档和配置说明
2. 检查日志输出
3. 提交 Issue 描述问题

---

