# Exceptions 异常基类

## 📋 目录说明

本目录用于存放异常基类和自定义异常。

## ✅ 已创建的异常类

### 1. AstraCoreException（核心异常基类）
- **命名空间：** `Astra.Core.Exceptions`
- **说明：** 所有模块异常的基础类，提供统一的异常结构
- **特性：**
  - 错误码支持
  - 时间戳记录
  - 上下文信息
  - 模块和操作标识

### 2. BusinessException（业务异常类）
- **命名空间：** `Astra.Core.Exceptions`
- **说明：** 用于业务逻辑相关的异常
- **继承：** `AstraCoreException`

### 3. ValidationException（验证异常类）
- **命名空间：** `Astra.Core.Exceptions`
- **说明：** 用于数据验证失败的情况
- **继承：** `BusinessException`
- **特性：** 支持多个验证错误

## 📝 使用示例

### 使用 AstraCoreException

```csharp
using Astra.Core.Exceptions;

// 基本用法
throw new AstraCoreException("操作失败");

// 带错误码
throw new AstraCoreException("操作失败", 1001);

// 带模块和操作信息
throw new AstraCoreException("操作失败", "Access", "Login")
    .WithContext("UserId", userId)
    .WithErrorCode(1001);
```

### 使用 BusinessException

```csharp
throw new BusinessException("业务规则违反", "RuleViolation");

throw new BusinessException("用户已存在", "Access", "Register", "DuplicateUser");
```

### 使用 ValidationException

```csharp
// 单个验证错误
throw new ValidationException("验证失败", "Email", "邮箱格式不正确");

// 多个验证错误
var errors = new List<ValidationError>
{
    new ValidationError("Email", "邮箱格式不正确"),
    new ValidationError("Password", "密码长度至少8位")
};
throw new ValidationException("验证失败", errors);
```

## 🎯 异常类层次结构

```
Exception (System)
└── AstraCoreException (Foundation)
    ├── BusinessException (Foundation)
    │   └── ValidationException (Foundation)
    └── [模块特定异常]
        ├── AccessGuardException (Access)
        └── PluginSystemException (Addins)
```

## 📝 迁移原则

- **通用异常** - 放在 Foundation/Exceptions/
- **模块特定异常** - 保留在各自模块的 Exceptions/ 目录
- **命名空间** - 通用异常使用 `Astra.Core.Exceptions`，模块特定异常保持原有命名空间

---

**创建时间：** 2024年  
**状态：** 已创建通用异常基类

