using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Astra.Plugins.Limits.Enums;

namespace Astra.Plugins.Limits.Helpers
{
    internal static class CurveScalarStatistics
    {
        public static string GetMetricDisplayName(CurveScalarMetricKind kind)
        {
            var mem = typeof(CurveScalarMetricKind).GetMember(kind.ToString()).FirstOrDefault();
            if (mem != null)
            {
                var da = mem.GetCustomAttribute<DisplayAttribute>();
                if (!string.IsNullOrEmpty(da?.Name))
                {
                    return da.Name;
                }
            }

            return kind.ToString();
        }

        /// <summary>按 X 闭区间 [min,max] 筛选点对；未启用筛选时返回完整序列副本。</summary>
        public static bool TryFilterByXInclusive(
            ReadOnlySpan<double> xFull,
            ReadOnlySpan<double> yFull,
            bool enableFilter,
            double xMin,
            double xMax,
            out double[] yFiltered,
            out string? error)
        {
            yFiltered = Array.Empty<double>();
            error = null;

            if (xFull.Length != yFull.Length)
            {
                error = "内部错误：X 与 Y 长度不一致";
                return false;
            }

            if (!enableFilter)
            {
                yFiltered = new double[yFull.Length];
                yFull.CopyTo(yFiltered);
                return true;
            }

            var lo = xMin;
            var hi = xMax;
            if (lo > hi)
            {
                (lo, hi) = (hi, lo);
            }

            var n = xFull.Length;
            var count = 0;
            for (var i = 0; i < n; i++)
            {
                var xv = xFull[i];
                var yv = yFull[i];
                if (xv >= lo && xv <= hi &&
                    !double.IsNaN(xv) && !double.IsInfinity(xv) &&
                    !double.IsNaN(yv) && !double.IsInfinity(yv))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                error = $"X 轴筛选 [{lo:G},{hi:G}] 后无有效样本点";
                return false;
            }

            yFiltered = new double[count];
            var w = 0;
            for (var i = 0; i < n; i++)
            {
                var xv = xFull[i];
                var yv = yFull[i];
                if (xv >= lo && xv <= hi &&
                    !double.IsNaN(xv) && !double.IsInfinity(xv) &&
                    !double.IsNaN(yv) && !double.IsInfinity(yv))
                {
                    yFiltered[w++] = yv;
                }
            }

            return true;
        }

        public static bool TryCompute(
            CurveScalarMetricKind kind,
            ReadOnlySpan<double> y,
            out double value,
            out string? error)
        {
            value = default;
            error = null;

            if (y.Length == 0)
            {
                error = "样本数为 0，无法计算统计量";
                return false;
            }

            switch (kind)
            {
                case CurveScalarMetricKind.Mean:
                    value = Mean(y);
                    return true;

                case CurveScalarMetricKind.Max:
                    value = Max(y);
                    return true;

                case CurveScalarMetricKind.Min:
                    value = Min(y);
                    return true;

                case CurveScalarMetricKind.PeakAbsolute:
                    value = PeakAbs(y);
                    return true;

                case CurveScalarMetricKind.PeakToPeak:
                    value = Max(y) - Min(y);
                    return true;

                case CurveScalarMetricKind.Rms:
                    value = Rms(y);
                    return true;

                case CurveScalarMetricKind.KurtosisExcess:
                    if (y.Length < 4)
                    {
                        error = "峭度（超额）需要至少 4 个样本点";
                        return false;
                    }

                    if (!TrySampleExcessKurtosis(y, out value, out var kErr))
                    {
                        error = kErr;
                        return false;
                    }

                    return true;

                default:
                    error = "未知的统计指标";
                    return false;
            }
        }

        private static double Mean(ReadOnlySpan<double> y)
        {
            double s = 0;
            for (var i = 0; i < y.Length; i++)
            {
                s += y[i];
            }

            return s / y.Length;
        }

        private static double Max(ReadOnlySpan<double> y)
        {
            var m = y[0];
            for (var i = 1; i < y.Length; i++)
            {
                if (y[i] > m)
                {
                    m = y[i];
                }
            }

            return m;
        }

        private static double Min(ReadOnlySpan<double> y)
        {
            var m = y[0];
            for (var i = 1; i < y.Length; i++)
            {
                if (y[i] < m)
                {
                    m = y[i];
                }
            }

            return m;
        }

        private static double PeakAbs(ReadOnlySpan<double> y)
        {
            var m = Math.Abs(y[0]);
            for (var i = 1; i < y.Length; i++)
            {
                var a = Math.Abs(y[i]);
                if (a > m)
                {
                    m = a;
                }
            }

            return m;
        }

        private static double Rms(ReadOnlySpan<double> y)
        {
            double s = 0;
            for (var i = 0; i < y.Length; i++)
            {
                var v = y[i];
                s += v * v;
            }

            return Math.Sqrt(s / y.Length);
        }

        /// <summary>无偏样本超额峭度（Fisher），正态总体期望为 0。</summary>
        private static bool TrySampleExcessKurtosis(ReadOnlySpan<double> y, out double g2, out string? error)
        {
            g2 = default;
            error = null;
            var n = y.Length;
            if (n < 4)
            {
                error = "样本数不足";
                return false;
            }

            var mean = Mean(y);
            double m2 = 0;
            for (var i = 0; i < n; i++)
            {
                var d = y[i] - mean;
                m2 += d * d;
            }

            var denomVar = n - 1;
            if (denomVar <= 0)
            {
                error = "方差分母无效";
                return false;
            }

            var varSample = m2 / denomVar;
            if (varSample <= 0 || double.IsNaN(varSample) || double.IsInfinity(varSample))
            {
                error = "样本方差为 0 或无效，无法计算峭度";
                return false;
            }

            var s = Math.Sqrt(varSample);
            double m4 = 0;
            for (var i = 0; i < n; i++)
            {
                var z = (y[i] - mean) / s;
                m4 += z * z * z * z;
            }

            var nMinus2 = n - 2;
            var nMinus3 = n - 3;
            if (nMinus2 <= 0 || nMinus3 <= 0)
            {
                error = "样本数不足以计算无偏峭度";
                return false;
            }

            var term1 = n * (n + 1.0) / ((n - 1.0) * nMinus2 * nMinus3) * m4;
            var term2 = 3.0 * (n - 1.0) * (n - 1.0) / (nMinus2 * nMinus3);
            g2 = term1 - term2;
            return true;
        }
    }
}
