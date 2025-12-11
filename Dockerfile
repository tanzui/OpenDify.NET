# 使用 .NET 10 运行时作为基础镜像
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 5003

# 设置环境变量
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5003

# 创建非 root 用户
RUN groupadd -r appuser && useradd -r -g appuser appuser && chown -R appuser /app
USER appuser

# 复制本地编译好的应用程序文件
COPY OpenDify.NET/bin/Release/net10.0/ .

# 启动应用程序
ENTRYPOINT ["dotnet", "OpenDify.NET.dll"]