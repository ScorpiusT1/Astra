# Simcenter Testlab 对标基线（模板）

- **锁定版本**：记录 Testlab 主版本 + 补丁（例如 2306）。
- **每算法**：官方 Help / Theory 章节链接或截图索引；`NvhAlgorithmDescriptor.TestlabFeatureHint` 与界面路径对应表。
- **金数据**：同一段波形在 Testlab 中的窗长、重叠、FFT 线数、计权、参考声压、校准；导出 CSV/UFF 路径；本库容差（相对/绝对）。
- **标准条文**：IEC 61260、ISO 532-1 等与具体指标对应关系。

在 CI 无 Testlab 时，以数学单测与检入的**小 fixture** 为主；完整金数据可在夜间或手动跑。
