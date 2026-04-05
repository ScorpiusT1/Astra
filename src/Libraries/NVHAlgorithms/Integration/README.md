# 宿主 / 插件桥接说明

1. 引用 `NVHAlgorithms` 项目。
2. 在**新节点**执行路径中：
   - 单任务：可调用 `NvhAlgorithmExecutor.ExecuteAsync(algorithm, input, runOptions, userCt)` 以应用 `NvhRunOptions` 中的默认/覆盖超时；
   - 从工作流取 `CancellationToken`；
   - 若引擎支持暂停，将暂停与取消合成为 `CancellationToken` 或注入 `WaitIfPausedAsync` 包装；
   - 使用 `NvhRunOptions { Timeout = …, Progress = … }` 映射 UI 进度；
   - 填充 `NvhSignalDescriptor`（`Fs`、样本数、校准、`ReferencePressurePa` 等）。
3. 批处理：构造 `NvhAlgorithmWorkItem` 列表，调用 `NvhJobOrchestrator.RunBatchAsync`；默认 `ContinueOnTaskFailure=true`，按返回的 `NvhSingleTaskResult` 逐条更新 UI。
4. **不要**修改旧算法节点；新旧能力边界见 `docs/PLUGIN_COEXISTENCE.md`。
