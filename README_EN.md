# OpenDify.NET

An OpenAI compatible Dify proxy server based on .NET 10, providing complete OpenAI API compatibility with support for conversations, streaming responses, image uploads, function calls, and tool execution.

## Documentation Language

🇺🇸 [English](README_EN.md) | 🇨🇳 [中文](README.md)

## Features

- ✅ **Full OpenAI API Compatibility** - Supports standard OpenAI API format
- ✅ **Streaming Response Support** - Real-time streaming output of response content
- ✅ **Image Upload Processing** - Supports image uploads in multimodal conversations
- ✅ **Function Call Support** - Complete Function Calling functionality
- ✅ **Conversation Memory Management** - Supports both history message and zero-width character memory modes
- ✅ **Multi-Application Management** - Supports managing multiple Dify applications
- ✅ **JWT Authentication** - Secure API authentication mechanism
- ✅ **Error Handling** - Comprehensive error handling and logging

## Quick Start

### 1. Configure Environment

Copy the `.env` file and modify it according to your environment:

```bash
cp .env.example .env
```

Edit the `.env` file:

```env
# Dify API Keys Configuration
# Format: Comma-separated list of API keys
DIFY_API_KEYS=app-OtfA94FWDwPw5YAmo8lj0kJ9

# Dify API Base URL
DIFY_API_BASE=http://192.168.0.117:8186/v1

# Conversation memory mode
# 1: history_message mode (default)
# 2: zero-width character mode
CONVERSATION_MEMORY_MODE=1

# Server Configuration
SERVER_HOST=127.0.0.1
SERVER_PORT=5003

# OpenAI compatible API Keys
VALID_API_KEYS=sk-abc123,sk-def456

# Logging Configuration
LOG_LEVEL=Information
```

### 2. Run the Application

```bash
# Using .NET CLI
dotnet run

# Or using Visual Studio
# Open OpenDify.NET.csproj and run
```

The application will start at `http://127.0.0.1:5003`.

### 3. Test the API

#### Get Model List

```bash
curl -X GET "http://127.0.0.1:5003/v1/models" \
  -H "Authorization: Bearer sk-abc123"
```

#### Send Chat Request

```bash
curl -X POST "http://127.0.0.1:5003/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-abc123" \
  -d '{
    "model": "testfunctioncall",
    "messages": [
      {
        "role": "user",
        "content": "Hello, please introduce yourself."
      }
    ]
  }'
```

#### Streaming Response

```bash
curl -X POST "http://127.0.0.1:5003/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-abc123" \
  -d '{
    "model": "testfunctioncall",
    "messages": [
      {
        "role": "user",
        "content": "Please write a poem about spring"
      }
    ],
    "stream": true
  }'
```

#### Image Upload

```bash
curl -X POST "http://127.0.0.1:5003/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-abc123" \
  -d '{
    "model": "testfunctioncall",
    "messages": [
      {
        "role": "user",
        "content": [
          {
            "type": "text",
            "text": "Please describe the content of this image"
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

#### Function Call

```bash
curl -X POST "http://127.0.0.1:5003/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-abc123" \
  -d '{
    "model": "testfunctioncall",
    "messages": [
      {
        "role": "user",
        "content": "What time is it now in Beijing?"
      }
    ],
    "functions": [
      {
        "name": "get_current_time",
        "description": "Get current time",
        "parameters": {
          "type": "object",
          "properties": {
            "timezone": {
              "type": "string",
              "description": "Timezone, e.g., Asia/Shanghai"
            }
          },
          "required": ["timezone"]
        }
      }
    ]
  }'
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/v1/models` | GET | Get available model list |
| `/v1/chat/completions` | POST | Send chat request |
| `/health` | GET | Health check |
| `/` | GET | API information |

## Configuration

### Application Configuration (appsettings.json)

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
        "Url": "http://127.0.0.1:5003"
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
    "Host": "127.0.0.1",
    "Port": 5003
  }
}
```

### Conversation Memory Modes

1. **1 (history_message)** - Use standard conversation history mode (default)
2. **2 (zero_width_character)** - Use zero-width character to hide memory information

### Environment Variables

| Variable Name | Description | Default Value |
|---------------|-------------|---------------|
| `DIFY_API_KEYS` | Dify API key list (comma-separated) | - |
| `DIFY_API_BASE` | Dify API base URL | `http://192.168.0.117:8186/v1` |
| `CONVERSATION_MEMORY_MODE` | Conversation memory mode (1=history_message, 2=zero_width_character) | `1` |
| `SERVER_HOST` | Server host address | `127.0.0.1` |
| `SERVER_PORT` | Server port | `5003` |
| `VALID_API_KEYS` | OpenAI compatible API key list (comma-separated) | - |
| `LOG_LEVEL` | Log level | `Information` |

## Project Structure

```
OpenDify.NET/
├── Configuration/          # Configuration classes
│   └── AppSettings.cs
├── Controllers/            # API controllers
│   └── OpenAIController.cs
├── Models/                 # Data models
│   ├── OpenAIModels.cs
│   └── DifyModels.cs
├── Services/               # Business services
│   ├── DifyModelManager.cs
│   ├── ConversationMemoryManager.cs
│   ├── RequestTransformationService.cs
│   ├── ResponseTransformationService.cs
│   ├── FileUploadService.cs
│   └── StreamingService.cs
├── Properties/             # Property configuration
│   └── launchSettings.json
├── Program.cs              # Application entry point
├── appsettings.json        # Application configuration
├── .env                    # Environment variables
├── OpenDify.NET.csproj     # Project file
├── MIGRATION_AND_CONTEXT.md # Migration and context documentation
└── README.md               # Project documentation

OpenDify.NET.Tests/         # Test project
├── Controllers/            # Controller tests
├── Services/               # Service tests
├── IntegrationTests/       # Integration tests
└── OpenDify.NET.Tests.csproj
```

## Development Guide

### Adding New API Endpoints

1. Add new methods in `Controllers/OpenAIController.cs`
2. Define corresponding request/response models in `Models/`
3. Implement business logic in `Services/`

### Custom Authentication

You can modify the authentication middleware in `Program.cs` to support other authentication methods.

### Extending Functionality

The project adopts a modular design, making it easy to extend new features:

- Add new Dify API integrations
- Implement proxies for other AI models
- Add monitoring and analytics features

## Deployment

### Docker Deployment

#### Build Image

```bash
# Execute in project root directory
docker build -t opendify-net:latest .
```

#### Run Container

```bash
# Basic run
docker run -d \
  --name opendify-net \
  -p 5003:5003 \
  opendify-net:latest

# Run with environment variables
docker run -d \
  --name opendify-net \
  -p 5003:5003 \
  -e DIFY_API_KEYS=your-dify-api-key \
  -e DIFY_API_BASE=https://api.dify.ai/v1 \
  -e VALID_API_KEYS=your-openai-api-key \
  opendify-net:latest

# Use Docker Compose (recommended)
docker-compose up -d
```

#### Docker Compose Configuration

Create `docker-compose.yml` file:

```yaml
version: '3.8'

services:
  opendify-net:
    build: .
    container_name: opendify-net
    ports:
      - "5003:5003"
    environment:
      - DIFY_API_KEYS=your-dify-api-key
      - DIFY_API_BASE=https://api.dify.ai/v1
      - CONVERSATION_MEMORY_MODE=1
      - SERVER_HOST=0.0.0.0
      - SERVER_PORT=5003
      - VALID_API_KEYS=sk-abc123,sk-def456
      - LOG_LEVEL=Information
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5003/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

### Environment Variable Configuration

In production environments, it is recommended to use environment variables or configuration services to manage sensitive information:

1. **Docker Environment Variables**: Set via `-e` parameters or docker-compose.yml
2. **Kubernetes ConfigMap/Secret**: For container orchestration environments
3. **Azure App Service Configuration**: For cloud deployment
4. **Environment-specific Configuration Files**: appsettings.Production.json

## License

This project is open source under the MIT License.

## Contributing

Welcome to submit Issues and Pull Requests to improve this project.

Project URL: https://github.com/tanzui/OpenDify.NET

## Support

If you encounter problems during use, please:

1. Check this documentation and configuration instructions
2. Check log output
3. Submit an Issue to describe the problem

---

**Note**: This is a .NET reimplementation of the Python version of OpenDify proxy server, aiming to provide better performance and integration with the .NET ecosystem.