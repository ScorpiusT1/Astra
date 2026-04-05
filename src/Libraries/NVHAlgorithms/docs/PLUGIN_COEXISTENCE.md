# NVHAlgorithms 与现有 `Astra.Plugins.Algorithms` 共存（已定稿）

- **新库仅用于新建节点**：现有 `SpectralAlgorithmNodes`、`WaveletSliceNodes`、`PsychoacousticNodes` 等**不修改**。
- **长期并存**：旧节点行为不因本库而改变；新节点通过工程引用 `NVHAlgorithms`，在节点内组装 `NvhSignalDescriptor`、调用 `INvhAlgorithm<,>` 或 `NvhJobOrchestrator`。
- **质量边界**：新算法以本库单测 + 金数据（见 `TestlabBaseline.md`）约束；不要求与旧节点实现逐比特一致。
- **暂停/取消/超时**：由 `plugin-bridge` 将引擎 `CancellationToken`、暂停与 `NvhRunOptions` 合成后传入库 API（实现细节见 `Integration/README.md`）。
