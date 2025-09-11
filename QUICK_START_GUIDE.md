# 用户认证模块快速启动指南

## 🚀 快速开始

### 1. 环境准备

#### 必需软件
- ✅ .NET 9.0 SDK
- ✅ Redis Server 6.0+
- ✅ Oracle Database（已有）
- ✅ Visual Studio Code 或 Visual Studio 2022

#### 验证环境
```bash
# 检查.NET版本
dotnet --version

# 检查Redis是否运行
redis-cli ping
# 应该返回: PONG
```

### 2. 配置Redis

#### Windows (推荐使用Docker)
```bash
# 使用Docker运行Redis
docker run -d -p 6379:6379 --name redis redis:latest

# 或者下载Windows版Redis
# https://github.com/microsoftarchive/redis/releases
```

#### Linux/macOS
```bash
# Ubuntu/Debian
sudo apt-get install redis-server
sudo systemctl start redis-server

# macOS
brew install redis
brew services start redis
```

### 3. 配置应用程序

#### 更新配置文件
编辑 `src/Presentation/appsettings.json`：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "你的Oracle数据库连接字符串",
    "RedisConnection": "localhost:6379,defaultDatabase=0"
  },
  "Jwt": {
    "SecretKey": "your-super-secret-key-that-is-at-least-32-characters-long-for-security",
    "Issuer": "DbApp",
    "Audience": "DbApp",
    "AccessTokenExpirationMinutes": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### 4. 构建和运行

#### 构建项目
```bash
# 在项目根目录执行
dotnet build

# 应该看到: 构建成功
```

#### 运行应用程序
```bash
# 启动应用程序
dotnet run --project src/Presentation

# 或者使用开发模式
dotnet watch run --project src/Presentation
```

#### 验证启动
应用程序启动后，你应该看到类似输出：
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### 5. 测试API

#### 方法1：使用REST Client扩展（推荐）
1. 在VS Code中安装 "REST Client" 扩展
2. 打开 `test-authentication.http` 文件
3. 点击请求上方的 "Send Request" 按钮

#### 方法2：使用curl命令
```bash
# 测试API连通性
curl -X GET "http://localhost:5000/api/testauth/ping"

# 测试Redis连接
curl -X GET "http://localhost:5000/api/testauth/test-cache"

# 测试用户登录
curl -X POST "http://localhost:5000/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "alice_member",
    "password": "hashedpassword123"
  }'
```

#### 方法3：使用Swagger UI
访问：`http://localhost:5000/scalar/v1`

### 6. 常见问题排查

#### 问题1：Redis连接失败
```
错误: Unable to connect to Redis
解决: 
1. 确保Redis服务正在运行
2. 检查连接字符串配置
3. 检查防火墙设置
```

#### 问题2：数据库连接失败
```
错误: Oracle connection failed
解决:
1. 检查Oracle数据库是否运行
2. 验证连接字符串正确性
3. 确保数据库用户权限
```

#### 问题3：JWT Token无效
```
错误: 401 Unauthorized
解决:
1. 检查JWT SecretKey配置
2. 确保Token没有过期
3. 验证Token格式正确
```

#### 问题4：构建失败
```
错误: Build failed
解决:
1. 确保.NET 9.0 SDK已安装
2. 运行 dotnet restore
3. 检查NuGet包引用
```

### 7. 测试用户数据

#### 默认测试用户
如果数据库中没有测试用户，可以通过以下方式创建：

```sql
-- 插入测试用户（密码: hashedpassword123）
INSERT INTO Users (UserId, Username, PasswordHash, Email, DisplayName, PhoneNumber, 
                   BirthDate, Gender, RegisterTime, PermissionLevel, RoleId, 
                   CreatedAt, UpdatedAt)
VALUES (
    'test-user-id-001',
    'alice_member',
    '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj/VjPoyNdO2', -- hashedpassword123
    'alice@example.com',
    'Alice Johnson',
    '1234567890',
    TO_DATE('1990-01-01', 'YYYY-MM-DD'),
    'Female',
    CURRENT_TIMESTAMP,
    0,
    'role-id-001',
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
);
```

### 8. 开发工具推荐

#### VS Code扩展
- REST Client - API测试
- C# - 代码支持
- Redis - Redis管理
- Oracle Developer Tools - 数据库管理

#### 其他工具
- Postman - API测试
- Redis Desktop Manager - Redis可视化管理
- Oracle SQL Developer - 数据库管理

### 9. 监控和日志

#### 查看应用日志
```bash
# 实时查看日志
dotnet run --project src/Presentation --verbosity normal
```

#### Redis监控
```bash
# 连接Redis CLI
redis-cli

# 查看所有键
KEYS *

# 查看特定用户会话
GET user_session:your-user-id

# 监控Redis命令
MONITOR
```

### 10. 生产部署建议

#### 安全配置
1. 更改JWT SecretKey为强密钥
2. 配置HTTPS证书
3. 设置Redis密码认证
4. 配置防火墙规则

#### 性能优化
1. 配置Redis持久化
2. 设置连接池大小
3. 启用响应压缩
4. 配置缓存策略

#### 监控配置
1. 配置应用程序日志
2. 设置健康检查端点
3. 配置性能计数器
4. 设置告警规则

## 🎉 完成！

如果所有步骤都成功完成，你现在应该有一个完全功能的用户认证系统：

- ✅ JWT Token认证
- ✅ Redis会话管理
- ✅ 密码安全处理
- ✅ 登录失败保护
- ✅ 用户资料管理
- ✅ 完整的API接口

## 📞 获取帮助

如果遇到问题：
1. 检查应用程序日志
2. 验证配置文件
3. 确认服务依赖（Redis、Oracle）
4. 查看错误消息详情

祝你使用愉快！🚀
