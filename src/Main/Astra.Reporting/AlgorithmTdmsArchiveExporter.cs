using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Nodes;
using Microsoft.Extensions.Logging;
using NVHDataBridge.Converters;
using NVHDataBridge.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Astra.Reporting;

/// <summary>
/// 将测试总线中的算法图表产物（<see cref="ChartDisplayPayload"/>）导出为 TDMS：
/// 每个算法产物一个 TDMS 文件，支持一维与二维（Heatmap）数据。
/// </summary>
public static class AlgorithmTdmsArchiveExporter
{
    private const string GroupSignal = "Signal";
    private const string GroupHeatmap = "Heatmap";

    /// <summary>
    /// 导出算法图表产物到指定目录，单个产物对应一个 TDMS 文件。
    /// </summary>
    /// <returns>成功写入的 TDMS 文件数量。</returns>
    public static int ExportAlgorithmChartsToTdms(
        ITestDataBus? dataBus,
        string outputDirectory,
        string filePrefix,
        WorkFlowRunRecord? runRecord,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        if (dataBus == null || string.IsNullOrWhiteSpace(outputDirectory))
            return 0;

        Directory.CreateDirectory(outputDirectory);

        var refs = dataBus.Query(DataArtifactCategory.Algorithm);
        if (refs.Count == 0)
            return 0;

        var writtenCount = 0;
        var nameUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seq = 1;

        foreach (var artifact in refs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsAlgorithmChannelArtifact(artifact))
                continue;

            if (!dataBus.TryGet<ChartDisplayPayload>(artifact.Key, out var payload) || payload == null)
                continue;

            var nvh = BuildNvhForArtifact(payload, artifact);
            if (nvh == null)
                continue;

            var baseName = BuildFileStem(filePrefix, seq, artifact, runRecord, nameUsed);
            var tdmsPath = Path.Combine(outputDirectory, baseName + ".tdms");
            try
            {
                NvhTdmsConverter.SaveToTdms(nvh, tdmsPath);
                writtenCount++;
                seq++;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "算法 TDMS 导出失败: {ArtifactKey}", artifact.Key);
            }
        }

        return writtenCount;
    }

    /// <summary>
    /// 仅导出「算法节点对应通道」产物：
    /// 1) 必须纳入报告；
    /// 2) 算法分层必须为 Algorithm（排除导入预览 Raw 与 Limits 曲线重发布 CurveResult）；
    /// 3) Preview 必须包含设备/通道段（__ReportDeviceChannel）。
    /// </summary>
    private static bool IsAlgorithmChannelArtifact(DataArtifactReference artifact)
    {
        if (!ReportIncludeKeys.PreviewIncludesInReport(artifact.Preview))
            return false;

        if (!PreviewBool(artifact.Preview, ReportArtifactPreviewKeys.ExportAlgorithmData, defaultValue: true))
            return false;

        var sourceLayer = ResolveSourceLayer(artifact.Preview);
        if (sourceLayer != ReportChartSourceKind.Algorithm)
            return false;

        if (artifact.Preview == null ||
            !artifact.Preview.TryGetValue(ReportArtifactPreviewKeys.DeviceChannel, out var deviceChannelObj))
            return false;

        var deviceChannel = Convert.ToString(deviceChannelObj, CultureInfo.InvariantCulture);
        return !string.IsNullOrWhiteSpace(deviceChannel);
    }

    private static bool PreviewBool(IReadOnlyDictionary<string, object>? preview, string key, bool defaultValue)
    {
        if (preview == null || !preview.TryGetValue(key, out var raw) || raw == null)
            return defaultValue;

        return raw switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var bs) => bs,
            int i => i != 0,
            long l => l != 0,
            _ => defaultValue
        };
    }

    private static ReportChartSourceKind ResolveSourceLayer(IReadOnlyDictionary<string, object>? preview)
    {
        if (preview == null ||
            !preview.TryGetValue(ReportArtifactPreviewKeys.ChartReportSourceKind, out var layerObj) ||
            layerObj == null)
            return ReportChartSourceKind.Algorithm;

        var layerText = Convert.ToString(layerObj, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        if (string.Equals(layerText, nameof(ReportChartSourceKind.Raw), StringComparison.OrdinalIgnoreCase))
            return ReportChartSourceKind.Raw;
        if (string.Equals(layerText, nameof(ReportChartSourceKind.CurveResult), StringComparison.OrdinalIgnoreCase))
            return ReportChartSourceKind.CurveResult;
        return ReportChartSourceKind.Algorithm;
    }

    private static NvhMemoryFile? BuildNvhForArtifact(ChartDisplayPayload payload, DataArtifactReference artifact)
    {
        if (payload.Series is { Count: > 0 } series)
            return BuildForSeriesPayload(series, artifact);

        return BuildForSinglePayload(payload, artifact, null);
    }

    private static NvhMemoryFile? BuildForSeriesPayload(IReadOnlyList<ChartSeriesEntry> series, DataArtifactReference artifact)
    {
        var file = CreateFileShell(artifact);
        var wrote = false;
        for (var i = 0; i < series.Count; i++)
        {
            var name = string.IsNullOrWhiteSpace(series[i].Name) ? $"Series_{i + 1}" : series[i].Name.Trim();
            var safeSeries = SanitizeName(name);
            var ok = AppendPayloadToFile(file, series[i].Data, safeSeries);
            wrote = wrote || ok;
        }

        return wrote ? file : null;
    }

    private static NvhMemoryFile? BuildForSinglePayload(ChartDisplayPayload payload, DataArtifactReference artifact, string? channelPrefix)
    {
        var file = CreateFileShell(artifact);
        var ok = AppendPayloadToFile(file, payload, channelPrefix);
        return ok ? file : null;
    }

    private static NvhMemoryFile CreateFileShell(DataArtifactReference artifact)
    {
        var file = new NvhMemoryFile();
        file.Properties.Set("name", string.IsNullOrWhiteSpace(artifact.DisplayName) ? "AlgorithmChart" : artifact.DisplayName.Trim());
        file.Properties.Set("description", artifact.Description ?? string.Empty);
        file.Properties.Set("created_at_utc", artifact.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        file.Properties.Set("artifact_key", artifact.Key ?? string.Empty);
        if (artifact.Preview != null)
        {
            if (artifact.Preview.TryGetValue("__ProducerNodeId", out var producer))
                file.Properties.Set("producer_node", Convert.ToString(producer, CultureInfo.InvariantCulture) ?? string.Empty);
            if (artifact.Preview.TryGetValue("tag", out var tag))
                file.Properties.Set("artifact_tag", Convert.ToString(tag, CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return file;
    }

    private static bool AppendPayloadToFile(NvhMemoryFile file, ChartDisplayPayload payload, string? channelPrefix)
    {
        return payload.Kind switch
        {
            ChartPayloadKind.Signal1D => AppendSignal1D(file, payload, channelPrefix),
            ChartPayloadKind.XYLine => AppendXY(file, payload, channelPrefix),
            ChartPayloadKind.XYScatter => AppendXY(file, payload, channelPrefix),
            ChartPayloadKind.Heatmap => AppendHeatmap(file, payload, channelPrefix),
            _ => false
        };
    }

    private static bool AppendSignal1D(NvhMemoryFile file, ChartDisplayPayload payload, string? channelPrefix)
    {
        var y = payload.SignalY;
        if (y == null || y.Length == 0)
            return false;

        var x = payload.X;
        if (x == null || x.Length != y.Length)
            x = BuildLinearX(y.Length, payload.SamplePeriod);

        var group = file.GetOrCreateGroup(GroupSignal);
        var xName = Prefix(channelPrefix, "X");
        var yName = Prefix(channelPrefix, "Y");
        WriteDoubleChannel(group, xName, x);
        WriteDoubleChannel(group, yName, y);

        ApplyAxisProperties(group, xName, yName, payload);
        return true;
    }

    private static bool AppendXY(NvhMemoryFile file, ChartDisplayPayload payload, string? channelPrefix)
    {
        var x = payload.X;
        var y = payload.Y;
        if (x == null || y == null || x.Length == 0 || y.Length == 0)
            return false;

        var count = Math.Min(x.Length, y.Length);
        if (count <= 0)
            return false;

        var xData = count == x.Length ? x : x.Take(count).ToArray();
        var yData = count == y.Length ? y : y.Take(count).ToArray();

        var group = file.GetOrCreateGroup(GroupSignal);
        var xName = Prefix(channelPrefix, "X");
        var yName = Prefix(channelPrefix, "Y");
        WriteDoubleChannel(group, xName, xData);
        WriteDoubleChannel(group, yName, yData);

        ApplyAxisProperties(group, xName, yName, payload);
        return true;
    }

    private static bool AppendHeatmap(NvhMemoryFile file, ChartDisplayPayload payload, string? channelPrefix)
    {
        var z = payload.HeatmapZ;
        var x = payload.HeatmapXCoordinates;
        var y = payload.HeatmapYCoordinates;
        if (z == null || x == null || y == null)
            return false;

        var rows = z.GetLength(0);
        var cols = z.GetLength(1);
        if (rows <= 0 || cols <= 0 || x.Length != cols || y.Length != rows)
            return false;

        var zFlat = new double[rows * cols];
        var k = 0;
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
                zFlat[k++] = z[r, c];
        }

        var group = file.GetOrCreateGroup(GroupHeatmap);
        var xName = Prefix(channelPrefix, "X");
        var yName = Prefix(channelPrefix, "Y");
        var zName = Prefix(channelPrefix, "Z");
        WriteDoubleChannel(group, xName, x);
        WriteDoubleChannel(group, yName, y);
        WriteDoubleChannel(group, zName, zFlat);

        group.Properties.Set("z_rows", rows);
        group.Properties.Set("z_cols", cols);
        group.Properties.Set("z_order", "row-major");
        group.Properties.Set("y_is_log10", payload.HeatmapYAxisIsLog10OfQuantity);
        group.Properties.Set("x_axis_label", payload.BottomAxisLabel ?? string.Empty);
        group.Properties.Set("x_axis_unit", payload.BottomAxisUnit ?? string.Empty);
        group.Properties.Set("y_axis_label", payload.LeftAxisLabel ?? string.Empty);
        group.Properties.Set("y_axis_unit", payload.LeftAxisUnit ?? string.Empty);
        return true;
    }

    private static void ApplyAxisProperties(
        NvhMemoryGroup group,
        string xName,
        string yName,
        ChartDisplayPayload payload)
    {
        if (group.TryGetChannel(xName, out var xChannel) && xChannel != null)
        {
            xChannel.Properties.Set("axis_role", "X");
            xChannel.Properties.Set("axis_label", payload.BottomAxisLabel ?? string.Empty);
            xChannel.Properties.Set("axis_unit", payload.BottomAxisUnit ?? string.Empty);
        }

        if (group.TryGetChannel(yName, out var yChannel) && yChannel != null)
        {
            yChannel.Properties.Set("axis_role", "Y");
            yChannel.Properties.Set("axis_label", payload.LeftAxisLabel ?? string.Empty);
            yChannel.Properties.Set("axis_unit", payload.LeftAxisUnit ?? string.Empty);
            if (payload.SamplePeriod > 0)
                yChannel.WfIncrement = payload.SamplePeriod;
        }
    }

    private static void WriteDoubleChannel(NvhMemoryGroup group, string channelName, double[] data)
    {
        var channel = group.CreateChannel<double>(channelName);
        channel.WriteSamples(data);
    }

    private static double[] BuildLinearX(int count, double samplePeriod)
    {
        var dt = samplePeriod > 0 ? samplePeriod : 1.0;
        var x = new double[count];
        for (var i = 0; i < count; i++)
            x[i] = i * dt;
        return x;
    }

    private static string BuildFileStem(
        string filePrefix,
        int seq,
        DataArtifactReference artifact,
        WorkFlowRunRecord? runRecord,
        HashSet<string> used)
    {
        var namePart = string.IsNullOrWhiteSpace(artifact.DisplayName) ? "算法图表" : artifact.DisplayName.Trim();
        namePart = SanitizeName(namePart);
        if (namePart.Length == 0)
            namePart = "算法图表";

        var nodeName = ResolveProducerNodeName(artifact, runRecord);
        var nodePart = SanitizeName(nodeName);
        if (string.IsNullOrWhiteSpace(nodePart))
            nodePart = "算法节点";

        var stem = $"{filePrefix}_algorithm_{seq:D3}_{nodePart}_{namePart}";
        if (stem.Length > 180)
            stem = stem[..180];

        var unique = stem;
        var suffix = 1;
        while (!used.Add(unique))
        {
            unique = $"{stem}_{suffix}";
            suffix++;
        }

        return unique;
    }

    private static string ResolveProducerNodeName(DataArtifactReference artifact, WorkFlowRunRecord? runRecord)
    {
        if (runRecord?.NodeRuns == null || artifact.Preview == null)
            return string.Empty;

        if (!artifact.Preview.TryGetValue("__ProducerNodeId", out var producerObj) || producerObj == null)
            return string.Empty;

        var producerNode = Convert.ToString(producerObj, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrWhiteSpace(producerNode))
            return string.Empty;

        foreach (var nodeRun in runRecord.NodeRuns)
        {
            if (!string.Equals(nodeRun.NodeId, producerNode, StringComparison.Ordinal))
                continue;

            if (!string.IsNullOrWhiteSpace(nodeRun.NodeName))
                return nodeRun.NodeName.Trim();
        }

        return string.Empty;
    }

    private static string Prefix(string? prefix, string name) =>
        string.IsNullOrWhiteSpace(prefix) ? name : $"{SanitizeName(prefix)}_{name}";

    private static string SanitizeName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var s = raw.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (invalid.Contains(chars[i]) || chars[i] == '\\')
                chars[i] = '_';
        }

        return new string(chars).Trim();
    }
}
