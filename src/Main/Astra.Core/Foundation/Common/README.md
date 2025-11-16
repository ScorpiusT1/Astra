# Common 通用工具类

## 📋 目录说明

本目录用于存放跨模块使用的通用工具类和辅助方法。

## ✅ 已迁移的类

### 1. OperationResult（操作结果类）
- **原位置：** `Devices/Common/OperationResult.cs`
- **命名空间：** `Astra.Core.Foundation.Common`（已更新为与文件夹匹配）
- **说明：** 通用的操作结果类，用于统一返回操作结果

### 2. PathHelper（路径辅助类）
- **原位置：** `Access/Utilities/PathHelper.cs`
- **命名空间：** `Astra.Core.Foundation.Common`（已更新为与文件夹匹配）
- **说明：** 通用的路径处理工具类

### 3. PasswordHelper（密码加密辅助类）
- **原位置：** `Access/Security/PasswordHelper.cs`
- **命名空间：** `Astra.Core.Foundation.Common`（已更新为与文件夹匹配）
- **说明：** 通用的密码加密工具类，使用 SHA256 加密

## 📝 迁移原则

- **命名空间与文件夹匹配** - 迁移后命名空间更新为与文件夹结构匹配
- **逐步迁移** - 每次迁移后验证编译和功能
- **文档记录** - 在文件中添加迁移说明

---

**创建时间：** 2024年  
**状态：** 已迁移部分通用工具类

