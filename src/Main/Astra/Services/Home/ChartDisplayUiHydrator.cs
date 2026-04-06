using Astra.Core.Constants;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Ui;
using Astra.UI.Abstractions.Nodes;
using NVHDataBridge.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Services.Home
{
    /// <summary>
    /// 节点完成时：将内联 <see cref="NodeUiOutputKeys.ChartPayloadSnapshot"/>、Raw 中的 <see cref="ChartDisplayPayload"/>，
    /// 或 NVH 波形，写入图表缓存。
    /// </summary>
    public sealed class ChartDisplayUiHydrator : INodeExecutionUiHydrator
    {
        private readonly IChartDisplayDataCache _cache;

        public ChartDisplayUiHydrator(IChartDisplayDataCache cache)
        {
            _cache = cache;
        }

        public void OnNodeExecutionCompleted(NodeContext? context, string nodeId, ExecutionResult? result)
        {
            if (context == null || string.IsNullOrWhiteSpace(nodeId) || result?.OutputData == null)
            {
                return;
            }

            if (result.OutputData.TryGetValue(NodeUiOutputKeys.ChartPayloadSnapshot, out var snap) &&
                snap is ChartDisplayPayload inlineSnapshot)
            {
                _cache.SetPayload(nodeId, ChartDisplayPayload.MergeAxisMetadata(inlineSnapshot, result.OutputData));
                return;
            }

            if (!result.OutputData.TryGetValue(NodeUiOutputKeys.HasChartData, out var hasObj) ||
                hasObj is not bool hasChart ||
                !hasChart)
            {
                return;
            }

            if (!result.OutputData.TryGetValue(NodeUiOutputKeys.ChartArtifactKey, out var keyObj) ||
                keyObj is not string artifactKey ||
                string.IsNullOrWhiteSpace(artifactKey))
            {
                return;
            }

            if (TryBuildMultiSeriesPayload(context, result.OutputData, out var multiPayload))
            {
                _cache.SetPayload(nodeId, ChartDisplayPayload.MergeAxisMetadata(multiPayload!, result.OutputData));
                return;
            }

            if (!context.TryGetArtifact(artifactKey.Trim(), out var raw) || raw == null)
            {
                return;
            }

            if (raw is ChartDisplayPayload payloadFromStore)
            {
                _cache.SetPayload(nodeId, ChartDisplayPayload.MergeAxisMetadata(payloadFromStore, result.OutputData));
                return;
            }

            if (raw is not NvhMemoryFile file)
            {
                return;
            }

            if (TryBuildFilteredNvhPayloadFromLimitsOutput(file, result.OutputData, out var filteredPayload))
            {
                _cache.SetPayload(nodeId, ChartDisplayPayload.MergeAxisMetadata(filteredPayload, result.OutputData));
                return;
            }

            var allChannels = NvhMemoryFileSampleExtractor.ExtractAllChannels(file);
            if (allChannels.Count == 0)
            {
                return;
            }

            if (allChannels.Count == 1)
            {
                var ch = allChannels[0];
                var nvhPayload = new ChartDisplayPayload
                {
                    Kind = ChartPayloadKind.Signal1D,
                    SignalY = ch.Samples,
                    SamplePeriod = ch.WfIncrement > 0 ? ch.WfIncrement : 1.0,
                    BottomAxisLabel = "样本",
                    LeftAxisLabel = "数值"
                };
                _cache.SetPayload(nodeId, ChartDisplayPayload.MergeAxisMetadata(nvhPayload, result.OutputData));
                return;
            }

            var singleFileSeries = new List<ChartSeriesEntry>();
            foreach (var ch in allChannels)
            {
                singleFileSeries.Add(new ChartSeriesEntry
                {
                    Name = $"{ch.GroupName}/{ch.ChannelName}",
                    IsVisibleByDefault = true,
                    Data = new ChartDisplayPayload
                    {
                        Kind = ChartPayloadKind.Signal1D,
                        SignalY = ch.Samples,
                        SamplePeriod = ch.WfIncrement > 0 ? ch.WfIncrement : 1.0
                    }
                });
            }

            var singleFilePayload = new ChartDisplayPayload
            {
                Kind = ChartPayloadKind.Signal1D,
                Series = singleFileSeries,
                LayoutMode = ChartDisplayPayload.InferDefaultLayout(singleFileSeries),
                BottomAxisLabel = "样本",
                LeftAxisLabel = "数值"
            };
            _cache.SetPayload(nodeId, ChartDisplayPayload.MergeAxisMetadata(singleFilePayload, result.OutputData));
        }

        /// <summary>
        /// Limits 卡控节点输出 <see cref="NodeUiOutputKeys.ChartNvhChannelFilter"/> 时，仅构建所选 Signal 通道的单曲线（与报告一致）。
        /// </summary>
        private static bool TryBuildFilteredNvhPayloadFromLimitsOutput(
            NvhMemoryFile file,
            IReadOnlyDictionary<string, object> outputData,
            out ChartDisplayPayload payload)
        {
            payload = null!;
            if (!outputData.TryGetValue(NodeUiOutputKeys.ChartNvhChannelFilter, out var fo) || fo is not string filterStr)
            {
                return false;
            }

            var ch = string.IsNullOrWhiteSpace(filterStr) ? null : filterStr.Trim();
            if (!NvhMemoryFileSampleExtractor.TryExtractAsDoubleArray(
                    file,
                    AstraSharedConstants.DataGroups.Signal,
                    ch,
                    out var samples,
                    out var wfInc) ||
                samples.Length == 0)
            {
                return false;
            }

            payload = new ChartDisplayPayload
            {
                Kind = ChartPayloadKind.Signal1D,
                SignalY = samples,
                SamplePeriod = wfInc > 0 ? wfInc : 1.0,
                BottomAxisLabel = "样本",
                LeftAxisLabel = "数值"
            };
            return true;
        }

        private static bool TryBuildMultiSeriesPayload(
            NodeContext context,
            IReadOnlyDictionary<string, object> outputData,
            out ChartDisplayPayload? payload)
        {
            payload = null;
            if (!TryGetArtifactKeys(outputData, out var artifactKeys) || artifactKeys.Count <= 0)
            {
                return false;
            }

            var series = new List<ChartSeriesEntry>();
            for (var i = 0; i < artifactKeys.Count; i++)
            {
                var key = artifactKeys[i];
                if (!context.TryGetArtifact(key, out var artifactObj) || artifactObj == null)
                {
                    continue;
                }

                if (artifactObj is ChartDisplayPayload embeddedPayload)
                {
                    series.Add(new ChartSeriesEntry
                    {
                        Name = $"系列 {i + 1}",
                        IsVisibleByDefault = true,
                        Data = embeddedPayload
                    });
                    continue;
                }

                if (artifactObj is NvhMemoryFile nvh)
                {
                    if (outputData.TryGetValue(NodeUiOutputKeys.ChartNvhChannelFilter, out var cf) && cf is string fstr)
                    {
                        var chKey = string.IsNullOrWhiteSpace(fstr) ? null : fstr.Trim();
                        if (NvhMemoryFileSampleExtractor.TryExtractAsDoubleArray(
                                nvh,
                                AstraSharedConstants.DataGroups.Signal,
                                chKey,
                                out var samples,
                                out var wfInc) &&
                            samples.Length > 0)
                        {
                            series.Add(new ChartSeriesEntry
                            {
                                Name = string.IsNullOrWhiteSpace(chKey) ? "曲线" : chKey.Trim(),
                                IsVisibleByDefault = true,
                                Data = new ChartDisplayPayload
                                {
                                    Kind = ChartPayloadKind.Signal1D,
                                    SignalY = samples,
                                    SamplePeriod = wfInc > 0 ? wfInc : 1.0
                                }
                            });
                        }

                        continue;
                    }

                    var channels = NvhMemoryFileSampleExtractor.ExtractAllChannels(nvh);
                    foreach (var ch in channels)
                    {
                        // 与上方单文件多通道分支一致：图例使用 NVH 通道键（及组/设备名），
                        // 避免「每卡仅一路」时被误标为「曲线 1、曲线 2」。
                        var seriesName = string.IsNullOrWhiteSpace(ch.ChannelName)
                            ? $"系列 {series.Count + 1}"
                            : string.IsNullOrWhiteSpace(ch.GroupName)
                                ? ch.ChannelName.Trim()
                                : $"{ch.GroupName}/{ch.ChannelName}";

                        series.Add(new ChartSeriesEntry
                        {
                            Name = seriesName,
                            IsVisibleByDefault = true,
                            Data = new ChartDisplayPayload
                            {
                                Kind = ChartPayloadKind.Signal1D,
                                SignalY = ch.Samples,
                                SamplePeriod = ch.WfIncrement > 0 ? ch.WfIncrement : 1.0
                            }
                        });
                    }
                }
            }

            if (series.Count == 0)
            {
                return false;
            }

            var userSubPlots = outputData.TryGetValue(NodeUiOutputKeys.ChartUseSubPlots, out var subPlotObj) &&
                               subPlotObj is bool b && b;
            var layoutMode = userSubPlots
                ? ChartLayoutMode.SubPlots
                : ChartDisplayPayload.InferDefaultLayout(series);

            payload = new ChartDisplayPayload
            {
                Kind = series[0].Data.Kind,
                Series = series,
                LayoutMode = layoutMode,
                BottomAxisLabel = "样本",
                LeftAxisLabel = "数值"
            };
            return true;
        }

        private static bool TryGetArtifactKeys(IReadOnlyDictionary<string, object> outputData, out List<string> keys)
        {
            keys = new List<string>();
            if (!outputData.TryGetValue(NodeUiOutputKeys.ChartArtifactKeys, out var raw) || raw == null)
            {
                return false;
            }

            if (raw is IEnumerable<string> seq)
            {
                foreach (var s in seq)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        keys.Add(s.Trim());
                    }
                }
                return keys.Count > 0;
            }

            if (raw is string one && !string.IsNullOrWhiteSpace(one))
            {
                keys.Add(one.Trim());
                return true;
            }

            return false;
        }
    }
}
