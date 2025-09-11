# 用户认证模块实现文档

## 概述

本文档描述了基于纯缓存实现方案的用户认证模块，该模块严格遵守不修改数据库结构的约束条件，所有认证相关的临时数据都存储在Redis缓存中。

## 技术架构

### 分层架构
- **Domain层**: 定义认证相关的领域模型和服务接口
- **Application层**: 实现CQRS命令和查询处理器
- **Infrastructure层**: 提供具体的服务实现（JWT、缓存、认证）
- **Presentation层**: 提供REST API控制器

### 核心组件

#### 1. 领域模型 (Domain/Models/Authentication/)
- `UserSession`: 用户会话信息
- `LoginFailureInfo`: 登录失败记录
- `PasswordResetToken`: 密码重置令牌
- `LoginHistoryEntry`: 登录历史记录

#### 2. 服务接口 (Domain/Services/)
- `IAuthenticationService`: 认证服务接口
- `ICacheService`: 缓存服务接口
- `IJwtTokenService`: JWT令牌服务接口

#### 3. 应用层 (Application/Authentication/)
- Commands: 登录、登出、密码重置等命令
- Queries: 用户信息查询、登录历史查询等
- DTOs: 数据传输对象
- Handlers: 命令和查询处理器

#### 4. 基础设施层 (Infrastructure/Services/)
- `AuthenticationService`: 认证服务实现
- `CacheService`: Redis缓存服务实现
- `JwtTokenService`: JWT令牌服务实现

## 功能特性

### 1. 用户认证
- ✅ 用户名密码登录
- ✅ JWT访问令牌生成
- ✅ 刷新令牌机制
- ✅ 安全登出（令牌黑名单）

### 2. 安全功能
- ✅ 登录失败次数限制
- ✅ 账户临时锁定
- ✅ 密码哈希（BCrypt）
- ✅ JWT令牌验证
- ✅ 令牌黑名单机制

### 3. 密码管理
- ✅ 密码重置（基于令牌）
- ✅ 密码修改
- ✅ 密码强度验证

### 4. 用户资料管理
- ✅ 个人信息查看
- ✅ 个人信息修改
- ✅ 登录历史查询
- ✅ 活跃会话查询

### 5. 会话管理
- ✅ 基于Redis的会话存储
- ✅ 会话过期管理
- ✅ 多设备登录支持
- ✅ 会话活跃时间更新

## API接口

### 认证接口 (/api/auth)

#### POST /api/auth/login
用户登录
```json
{
  "username": "string",
  "password": "string",
  "rememberMe": false
}
```

#### POST /api/auth/refresh
刷新令牌
```json
{
  "refreshToken": "string"
}
```

#### POST /api/auth/logout
用户登出（需要认证）

#### POST /api/auth/forgot-password
忘记密码
```json
{
  "usernameOrEmail": "string"
}
```

#### POST /api/auth/reset-password
重置密码
```json
{
  "token": "string",
  "newPassword": "string",
  "confirmPassword": "string"
}
```

#### POST /api/auth/change-password
修改密码（需要认证）
```json
{
  "currentPassword": "string",
  "newPassword": "string",
  "confirmPassword": "string"
}
```

### 用户资料接口 (/api/userprofile)

#### GET /api/userprofile/profile
获取用户资料（需要认证）

#### PUT /api/userprofile/profile
更新用户资料（需要认证）
```json
{
  "displayName": "string",
  "email": "string",
  "phoneNumber": "string",
  "birthDate": "2024-01-01T00:00:00Z",
  "gender": 0
}
```

#### GET /api/userprofile/login-history
获取登录历史（需要认证）

#### GET /api/userprofile/sessions
获取活跃会话（需要认证）

## 配置说明

### appsettings.json
```json
{
  "ConnectionStrings": {
    "RedisConnection": "localhost:6379,defaultDatabase=0"
  },
  "Jwt": {
    "SecretKey": "your-super-secret-key-that-is-at-least-32-characters-long",
    "Issuer": "DbApp",
    "Audience": "DbApp",
    "AccessTokenExpirationMinutes": 60
  }
}
```

### 环境变量
- `REDIS_CONNECTION_STRING`: Redis连接字符串
- `JWT_SECRET_KEY`: JWT密钥
- `JWT_ISSUER`: JWT发行者
- `JWT_AUDIENCE`: JWT受众

## 缓存键设计

### 会话相关
- `user_session:{userId}`: 用户会话信息
- `refresh_token:{refreshToken}`: 刷新令牌映射
- `blacklisted_token:{accessToken}`: 黑名单令牌

### 安全相关
- `login_failures:{username}`: 登录失败记录
- `password_reset_token:{token}`: 密码重置令牌
- `user_password_reset:{userId}`: 用户密码重置跟踪

### 历史记录
- `login_history:{userId}`: 用户登录历史

## 安全考虑

### 1. 密码安全
- 使用BCrypt进行密码哈希
- 密码强度验证（最少6位）
- 密码重置后强制重新登录

### 2. 令牌安全
- JWT令牌包含用户信息和权限
- 访问令牌短期有效（1小时）
- 刷新令牌长期有效（30天）
- 登出时令牌加入黑名单

### 3. 登录安全
- 登录失败次数限制（5次）
- 账户临时锁定（30分钟）
- IP地址记录和跟踪

### 4. 会话安全
- 会话过期自动清理
- 活跃时间更新
- 设备信息记录

## 部署要求

### 1. Redis服务器
- Redis 6.0+
- 建议配置持久化
- 生产环境建议使用集群

### 2. .NET运行时
- .NET 9.0+
- ASP.NET Core 9.0+

### 3. 依赖包
- Microsoft.AspNetCore.Authentication.JwtBearer
- System.IdentityModel.Tokens.Jwt
- BCrypt.Net-Next
- Microsoft.Extensions.Caching.StackExchangeRedis

## 测试

### 1. 单元测试
```bash
dotnet test
```

### 2. API测试
访问 `/scalar/v1` 查看API文档并进行测试

### 3. 缓存测试
```bash
GET /api/testauth/test-cache
```

## 监控和日志

### 1. 日志记录
- 登录成功/失败
- 密码重置请求
- 令牌刷新
- 会话创建/销毁

### 2. 性能监控
- Redis连接状态
- 令牌验证性能
- 缓存命中率

## 故障排除

### 1. 常见问题
- Redis连接失败：检查连接字符串和Redis服务状态
- JWT验证失败：检查密钥配置和令牌格式
- 登录失败：检查用户名密码和账户锁定状态

### 2. 调试技巧
- 启用详细日志记录
- 使用Redis CLI检查缓存数据
- 检查JWT令牌内容

## 扩展建议

### 1. 功能扩展
- 双因素认证（2FA）
- 社交登录集成
- 单点登录（SSO）
- 设备管理

### 2. 性能优化
- Redis集群部署
- 令牌缓存优化
- 会话数据压缩

### 3. 安全增强
- 密码复杂度策略
- 登录地理位置检测
- 异常登录告警
- 审计日志记录
