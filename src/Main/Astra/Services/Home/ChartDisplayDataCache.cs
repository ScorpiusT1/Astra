using System.Collections.Concurrent;
using System.Collections.Generic;
using Astra.Core.Nodes.Ui;

namespace Astra.Services.Home
{
    public sealed class ChartDisplayDataCache : IChartDisplayDataCache, IChartCurveDataCache
    {
        private readonly ConcurrentDictionary<string, ChartDisplayPayload> _byNodeId = new(StringComparer.Ordinal);

        public void SetPayload(string nodeId, ChartDisplayPayload payload)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || payload == null)
            {
                return;
            }

            _byNodeId[nodeId] = payload.Clone();
        }

        public bool TryGetPayload(string nodeId, out ChartDisplayPayload? payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            if (!_byNodeId.TryGetValue(nodeId, out var p))
            {
                return false;
            }

            payload = p.Clone();
            return true;
        }

        public void SetSamplesForNode(string nodeId, IReadOnlyList<double> samples)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || samples == null || samples.Count == 0)
            {
                return;
            }

            var y = new double[samples.Count];
            for (var i = 0; i < samples.Count; i++)
            {
                y[i] = samples[i];
            }

            SetPayload(nodeId, new ChartDisplayPayload
            {
                Kind = ChartPayloadKind.Signal1D,
                SignalY = y,
                SamplePeriod = 1.0,
                BottomAxisLabel = "样本",
                LeftAxisLabel = "数值"
            });
        }

        public bool TryGetSamplesForNode(string nodeId, out IReadOnlyList<double>? samples)
        {
            samples = null;
            if (!TryGetPayload(nodeId, out var p) || p == null || p.Kind != ChartPayloadKind.Signal1D || p.SignalY == null)
            {
                return false;
            }

            samples = p.SignalY;
            return true;
        }
    }
}
