using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace NVHDataBridge.IO.WAV
{
    /// <summary>
    /// 音频归一化方法枚举
    /// </summary>
    public enum NormalizationMethod
    {
        /// <summary>
        /// 线性归一化（默认）- 直接映射，保真度最高
        /// </summary>
        Linear,

        /// <summary>
        /// 峰值归一化 - 基于最大绝对值归一化，充分利用动态范围
        /// </summary>
        Peak,

        /// <summary>
        /// RMS归一化 - 基于均方根值归一化，保持感知音量一致
        /// </summary>
        RMS,

        /// <summary>
        /// 对数归一化 - 使用对数压缩动态范围，适合大动态范围信号
        /// </summary>
        Logarithmic,

        /// <summary>
        /// 自动增益控制 - 基于时间窗口动态调整，保持音量稳定
        /// </summary>
        AGC
    }

    /// <summary>
    /// 音频归一化工具类 - 提供多种归一化方法
    /// </summary>
    public static class AudioNormalization
    {
        #region 峰值归一化（Peak Normalization）

        /// <summary>
        /// 峰值归一化 - 基于最大绝对值进行归一化
        /// </summary>
        /// <param name="samples">输入样本数组</param>
        /// <param name="targetPeak">目标峰值（默认1.0）</param>
        /// <returns>归一化后的样本数组</returns>
        public static float[] NormalizePeak(float[] samples, float targetPeak = 1.0f)
        {
            if (samples == null || samples.Length == 0)
                return samples ?? Array.Empty<float>();

            float maxAbs = FindMaxAbsolute(samples);

            if (maxAbs < 1e-10f)
                return samples; // 信号太小，无需归一化

            float scale = targetPeak / maxAbs;
            float[] normalized = new float[samples.Length];

            NormalizeByScaleSIMD(samples, normalized, scale);

            return normalized;
        }

        /// <summary>
        /// 峰值归一化 - 双精度版本
        /// </summary>
        public static double[] NormalizePeak(double[] samples, out double normalizationFactor, double targetPeak = 1.0)
        {
            normalizationFactor = 1;

            if (samples == null || samples.Length == 0)
            {
                return samples ?? Array.Empty<double>();
            }


            double maxAbs = FindMaxAbsolute(samples);
            normalizationFactor = maxAbs < 1e-10 ? 1.0 : maxAbs;

            if (maxAbs < 1e-10)
                return samples;

            double scale = targetPeak / maxAbs;
            double[] normalized = new double[samples.Length];

            NormalizeByScaleSIMD(samples, normalized, scale);

            return normalized;
        }

        /// <summary>
        /// 峰值归一化 - 返回归一化因子
        /// </summary>
        public static float[] NormalizePeak(float[] samples, out float normalizationFactor, float targetPeak = 1.0f)
        {
            if (samples == null || samples.Length == 0)
            {
                normalizationFactor = 1.0f;
                return samples ?? Array.Empty<float>();
            }

            float maxAbs = FindMaxAbsolute(samples);
            normalizationFactor = maxAbs < 1e-10f ? 1.0f : maxAbs;

            if (maxAbs < 1e-10f)
                return samples;

            float scale = targetPeak / maxAbs;
            float[] normalized = new float[samples.Length];
            NormalizeByScaleSIMD(samples, normalized, scale);

            return normalized;
        }

        /// <summary>
        /// 反归一化 - 从归一化数据还原原始数据（峰值归一化的逆操作）
        /// </summary>
        /// <param name="normalizedSamples">归一化后的样本数组（通常在-1.0到1.0范围）</param>
        /// <param name="normalizationFactor">归一化因子（最大绝对值，即原始数据的峰值）</param>
        /// <param name="targetPeak">目标峰值（默认1.0，必须与归一化时使用的值一致）</param>
        /// <returns>还原后的原始数据</returns>
        public static float[] DenormalizePeak(float[] normalizedSamples, float normalizationFactor, float targetPeak = 1.0f)
        {
            if (normalizedSamples == null || normalizedSamples.Length == 0)
                return normalizedSamples ?? Array.Empty<float>();

            if (normalizationFactor < 1e-10f)
                return normalizedSamples; // 归一化因子无效，无法还原

            // 还原公式：original = normalized * (normalizationFactor / targetPeak)
            float scale = normalizationFactor / targetPeak;
            float[] original = new float[normalizedSamples.Length];
            
            NormalizeByScaleSIMD(normalizedSamples, original, scale);
            
            return original;
        }

        /// <summary>
        /// 反归一化 - 双精度版本
        /// </summary>
        /// <param name="normalizedSamples">归一化后的样本数组（通常在-1.0到1.0范围）</param>
        /// <param name="normalizationFactor">归一化因子（最大绝对值，即原始数据的峰值）</param>
        /// <param name="targetPeak">目标峰值（默认1.0，必须与归一化时使用的值一致）</param>
        /// <returns>还原后的原始数据</returns>
        public static double[] DenormalizePeak(double[] normalizedSamples, double normalizationFactor, double targetPeak = 1.0)
        {
            if (normalizedSamples == null || normalizedSamples.Length == 0)
                return normalizedSamples ?? Array.Empty<double>();

            if (normalizationFactor < 1e-10)
                return normalizedSamples; // 归一化因子无效，无法还原

            // 还原公式：original = normalized * (normalizationFactor / targetPeak)
            double scale = normalizationFactor / targetPeak;
            double[] original = new double[normalizedSamples.Length];
            
            NormalizeByScaleSIMD(normalizedSamples, original, scale);
            
            return original;
        }

        #endregion

        #region RMS归一化（RMS Normalization）

        /// <summary>
        /// RMS归一化 - 基于均方根值进行归一化
        /// </summary>
        /// <param name="samples">输入样本数组</param>
        /// <param name="targetRMS">目标RMS值（默认0.1，约为-20dB）</param>
        /// <returns>归一化后的样本数组</returns>
        public static float[] NormalizeRMS(float[] samples, float targetRMS = 0.1f)
        {
            if (samples == null || samples.Length == 0)
                return samples ?? Array.Empty<float>();

            float rms = CalculateRMS(samples);

            if (rms < 1e-10f)
                return samples; // 信号太小，无需归一化

            float scale = targetRMS / rms;
            float[] normalized = new float[samples.Length];
            NormalizeByScaleSIMD(samples, normalized, scale);

            return normalized;
        }

        /// <summary>
        /// RMS归一化 - 双精度版本
        /// </summary>
        public static double[] NormalizeRMS(double[] samples, double targetRMS = 0.1)
        {
            if (samples == null || samples.Length == 0)
                return samples ?? Array.Empty<double>();

            double rms = CalculateRMS(samples);

            if (rms < 1e-10)
                return samples;

            double scale = targetRMS / rms;
            double[] normalized = new double[samples.Length];
            NormalizeByScaleSIMD(samples, normalized, scale);

            return normalized;
        }

        /// <summary>
        /// RMS归一化 - 双精度版本，返回归一化因子
        /// </summary>
        public static double[] NormalizeRMS(double[] samples, out double normalizationFactor, double targetRMS = 0.1)
        {
            if (samples == null || samples.Length == 0)
            {
                normalizationFactor = 1.0;
                return samples ?? Array.Empty<double>();
            }

            double rms = CalculateRMS(samples);
            normalizationFactor = rms < 1e-10 ? 1.0 : rms;

            if (rms < 1e-10)
                return samples;

            double scale = targetRMS / rms;
            double[] normalized = new double[samples.Length];
            NormalizeByScaleSIMD(samples, normalized, scale);

            return normalized;
        }

        /// <summary>
        /// RMS归一化 - 返回归一化因子
        /// </summary>
        public static float[] NormalizeRMS(float[] samples, out float normalizationFactor, float targetRMS = 0.1f)
        {
            if (samples == null || samples.Length == 0)
            {
                normalizationFactor = 1.0f;
                return samples ?? Array.Empty<float>();
            }

            float rms = CalculateRMS(samples);
            normalizationFactor = rms < 1e-10f ? 1.0f : rms;

            if (rms < 1e-10f)
                return samples;

            float scale = targetRMS / rms;
            float[] normalized = new float[samples.Length];
            NormalizeByScaleSIMD(samples, normalized, scale);

            return normalized;
        }

        #endregion

        #region 对数归一化（Logarithmic Normalization）

        /// <summary>
        /// 对数归一化 - 使用对数压缩动态范围
        /// </summary>
        /// <param name="samples">输入样本数组（应在-1.0到1.0范围内）</param>
        /// <param name="compressionFactor">压缩因子（默认9.0，值越大压缩越强）</param>
        /// <returns>归一化后的样本数组</returns>
        public static float[] NormalizeLogarithmic(float[] samples, float compressionFactor = 9.0f)
        {
            if (samples == null || samples.Length == 0)
                return samples ?? Array.Empty<float>();

            float[] normalized = new float[samples.Length];
            float logScale = 1.0f / MathF.Log10(1.0f + compressionFactor);

            for (int i = 0; i < samples.Length; i++)
            {
                float sample = samples[i];
                float absSample = Math.Abs(sample);

                if (absSample < 1e-10f)
                {
                    normalized[i] = 0.0f;
                }
                else
                {
                    float compressed = MathF.Log10(1.0f + absSample * compressionFactor) * logScale;
                    normalized[i] = Math.Sign(sample) * compressed;
                }
            }

            return normalized;
        }

        /// <summary>
        /// 对数归一化 - 双精度版本
        /// </summary>
        public static double[] NormalizeLogarithmic(double[] samples, double compressionFactor = 9.0)
        {
            if (samples == null || samples.Length == 0)
                return samples ?? Array.Empty<double>();

            double[] normalized = new double[samples.Length];
            double logScale = 1.0 / Math.Log10(1.0 + compressionFactor);

            for (int i = 0; i < samples.Length; i++)
            {
                double sample = samples[i];
                double absSample = Math.Abs(sample);

                if (absSample < 1e-10)
                {
                    normalized[i] = 0.0;
                }
                else
                {
                    double compressed = Math.Log10(1.0 + absSample * compressionFactor) * logScale;
                    normalized[i] = Math.Sign(sample) * compressed;
                }
            }

            return normalized;
        }

        #endregion

        #region 自动增益控制（AGC - Automatic Gain Control）

        /// <summary>
        /// 自动增益控制 - 基于时间窗口动态调整增益
        /// </summary>
        /// <param name="samples">输入样本数组</param>
        /// <param name="windowSize">时间窗口大小（样本数）</param>
        /// <param name="targetRMS">目标RMS值（默认0.1）</param>
        /// <param name="attackTime">启动时间常数（0-1，默认0.9，值越大响应越快）</param>
        /// <param name="releaseTime">释放时间常数（0-1，默认0.99，值越大响应越慢）</param>
        /// <returns>归一化后的样本数组</returns>
        public static float[] NormalizeAGC(float[] samples, int windowSize = 4096, float targetRMS = 0.1f,
            float attackTime = 0.9f, float releaseTime = 0.99f)
        {
            if (samples == null || samples.Length == 0)
                return samples ?? Array.Empty<float>();

            float[] normalized = new float[samples.Length];
            float currentGain = 1.0f;

            // 确保窗口大小合理
            windowSize = Math.Max(64, Math.Min(windowSize, samples.Length));

            for (int i = 0; i < samples.Length; i++)
            {
                // 计算当前窗口的RMS
                int windowStart = Math.Max(0, i - windowSize / 2);
                int windowEnd = Math.Min(samples.Length, i + windowSize / 2);
                int actualWindowSize = windowEnd - windowStart;

                float windowRMS = CalculateRMSWindow(samples, windowStart, actualWindowSize);

                // 计算目标增益
                float targetGain = windowRMS > 1e-10f ? targetRMS / windowRMS : 1.0f;

                // 应用攻击和释放时间常数
                if (targetGain > currentGain)
                {
                    // 攻击：快速增加增益
                    currentGain = currentGain * attackTime + targetGain * (1.0f - attackTime);
                }
                else
                {
                    // 释放：缓慢降低增益
                    currentGain = currentGain * releaseTime + targetGain * (1.0f - releaseTime);
                }

                // 限制增益范围，防止过大
                currentGain = Math.Clamp(currentGain, 0.01f, 10.0f);

                normalized[i] = samples[i] * currentGain;
            }

            return normalized;
        }

        /// <summary>
        /// 自动增益控制 - 双精度版本
        /// </summary>
        public static double[] NormalizeAGC(double[] samples, int windowSize = 4096, double targetRMS = 0.1,
            double attackTime = 0.9, double releaseTime = 0.99)
        {
            if (samples == null || samples.Length == 0)
                return samples ?? Array.Empty<double>();

            double[] normalized = new double[samples.Length];
            double currentGain = 1.0;

            windowSize = Math.Max(64, Math.Min(windowSize, samples.Length));

            for (int i = 0; i < samples.Length; i++)
            {
                int windowStart = Math.Max(0, i - windowSize / 2);
                int windowEnd = Math.Min(samples.Length, i + windowSize / 2);
                int actualWindowSize = windowEnd - windowStart;

                double windowRMS = CalculateRMSWindow(samples, windowStart, actualWindowSize);
                double targetGain = windowRMS > 1e-10 ? targetRMS / windowRMS : 1.0;

                if (targetGain > currentGain)
                {
                    currentGain = currentGain * attackTime + targetGain * (1.0 - attackTime);
                }
                else
                {
                    currentGain = currentGain * releaseTime + targetGain * (1.0 - releaseTime);
                }

                currentGain = Math.Clamp(currentGain, 0.01, 10.0);
                normalized[i] = samples[i] * currentGain;
            }

            return normalized;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 查找数组中的最大绝对值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FindMaxAbsolute(float[] samples)
        {
            float maxAbs = 0.0f;

            if (Vector.IsHardwareAccelerated && samples.Length >= Vector<float>.Count)
            {
                int vectorSize = Vector<float>.Count;
                var maxVec = Vector<float>.Zero;
                int i = 0;
                int vectorEnd = samples.Length - vectorSize + 1;

                for (; i < vectorEnd; i += vectorSize)
                {
                    var vec = new Vector<float>(samples, i);
                    var absVec = Vector.Abs(vec);
                    maxVec = Vector.Max(maxVec, absVec);
                }

                // 从向量中提取最大值
                for (int j = 0; j < vectorSize; j++)
                {
                    maxAbs = Math.Max(maxAbs, maxVec[j]);
                }

                // 处理剩余元素
                for (; i < samples.Length; i++)
                {
                    maxAbs = Math.Max(maxAbs, Math.Abs(samples[i]));
                }
            }
            else
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    maxAbs = Math.Max(maxAbs, Math.Abs(samples[i]));
                }
            }

            return maxAbs;
        }

        /// <summary>
        /// 查找数组中的最大绝对值（双精度）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double FindMaxAbsolute(double[] samples)
        {
            double maxAbs = 0.0;

            if (Vector.IsHardwareAccelerated && samples.Length >= Vector<double>.Count)
            {
                int vectorSize = Vector<double>.Count;
                var maxVec = Vector<double>.Zero;
                int i = 0;
                int vectorEnd = samples.Length - vectorSize + 1;

                for (; i < vectorEnd; i += vectorSize)
                {
                    var vec = new Vector<double>(samples, i);
                    var absVec = Vector.Abs(vec);
                    maxVec = Vector.Max(maxVec, absVec);
                }

                for (int j = 0; j < vectorSize; j++)
                {
                    maxAbs = Math.Max(maxAbs, maxVec[j]);
                }

                for (; i < samples.Length; i++)
                {
                    maxAbs = Math.Max(maxAbs, Math.Abs(samples[i]));
                }
            }
            else
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    maxAbs = Math.Max(maxAbs, Math.Abs(samples[i]));
                }
            }

            return maxAbs;
        }

        /// <summary>
        /// 计算RMS值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateRMS(float[] samples)
        {
            if (samples.Length == 0)
                return 0.0f;

            double sumSquares = 0.0;

            if (Vector.IsHardwareAccelerated && samples.Length >= Vector<float>.Count)
            {
                int vectorSize = Vector<float>.Count;
                var sumVec = Vector<double>.Zero;
                int i = 0;
                int vectorEnd = samples.Length - vectorSize + 1;

                for (; i < vectorEnd; i += vectorSize)
                {
                    var vec = new Vector<float>(samples, i);
                    var squared = vec * vec;

                    for (int j = 0; j < vectorSize; j++)
                    {
                        sumSquares += squared[j];
                    }
                }

                for (; i < samples.Length; i++)
                {
                    double sample = samples[i];
                    sumSquares += sample * sample;
                }
            }
            else
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    double sample = samples[i];
                    sumSquares += sample * sample;
                }
            }

            return (float)Math.Sqrt(sumSquares / samples.Length);
        }

        /// <summary>
        /// 计算RMS值（双精度）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double CalculateRMS(double[] samples)
        {
            if (samples.Length == 0)
                return 0.0;

            double sumSquares = 0.0;

            if (Vector.IsHardwareAccelerated && samples.Length >= Vector<double>.Count)
            {
                int vectorSize = Vector<double>.Count;
                var sumVec = Vector<double>.Zero;
                int i = 0;
                int vectorEnd = samples.Length - vectorSize + 1;

                for (; i < vectorEnd; i += vectorSize)
                {
                    var vec = new Vector<double>(samples, i);
                    var squared = vec * vec;

                    for (int j = 0; j < vectorSize; j++)
                    {
                        sumSquares += squared[j];
                    }
                }

                for (; i < samples.Length; i++)
                {
                    double sample = samples[i];
                    sumSquares += sample * sample;
                }
            }
            else
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    double sample = samples[i];
                    sumSquares += sample * sample;
                }
            }

            return Math.Sqrt(sumSquares / samples.Length);
        }

        /// <summary>
        /// 计算窗口的RMS值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateRMSWindow(float[] samples, int start, int length)
        {
            if (length == 0)
                return 0.0f;

            double sumSquares = 0.0;
            int end = start + length;

            for (int i = start; i < end && i < samples.Length; i++)
            {
                double sample = samples[i];
                sumSquares += sample * sample;
            }

            return (float)Math.Sqrt(sumSquares / length);
        }

        /// <summary>
        /// 计算窗口的RMS值（双精度）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double CalculateRMSWindow(double[] samples, int start, int length)
        {
            if (length == 0)
                return 0.0;

            double sumSquares = 0.0;
            int end = start + length;

            for (int i = start; i < end && i < samples.Length; i++)
            {
                double sample = samples[i];
                sumSquares += sample * sample;
            }

            return Math.Sqrt(sumSquares / length);
        }

        /// <summary>
        /// 使用SIMD优化的数组缩放（通用方法：output[i] = input[i] * scale）
        /// 用于归一化和反归一化操作中的向量乘法运算
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void NormalizeByScaleSIMD(float[] input, float[] output, float scale)
        {
            if (Vector.IsHardwareAccelerated && input.Length >= Vector<float>.Count)
            {
                int vectorSize = Vector<float>.Count;
                var scaleVec = new Vector<float>(scale);
                int i = 0;
                int vectorEnd = input.Length - vectorSize + 1;

                for (; i < vectorEnd; i += vectorSize)
                {
                    var vec = new Vector<float>(input, i);
                    var scaled = vec * scaleVec;
                    scaled.CopyTo(output, i);
                }

                for (; i < input.Length; i++)
                {
                    output[i] = input[i] * scale;
                }
            }
            else
            {
                for (int i = 0; i < input.Length; i++)
                {
                    output[i] = input[i] * scale;
                }
            }
        }

        /// <summary>
        /// 使用SIMD优化的数组缩放（通用方法：output[i] = input[i] * scale）
        /// 用于归一化和反归一化操作中的向量乘法运算
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void NormalizeByScaleSIMD(double[] input, double[] output, double scale)
        {
            if (Vector.IsHardwareAccelerated && input.Length >= Vector<double>.Count)
            {
                int vectorSize = Vector<double>.Count;
                var scaleVec = new Vector<double>(scale);
                int i = 0;
                int vectorEnd = input.Length - vectorSize + 1;

                for (; i < vectorEnd; i += vectorSize)
                {
                    var vec = new Vector<double>(input, i);
                    var scaled = vec * scaleVec;
                    scaled.CopyTo(output, i);
                }

                for (; i < input.Length; i++)
                {
                    output[i] = input[i] * scale;
                }
            }
            else
            {
                for (int i = 0; i < input.Length; i++)
                {
                    output[i] = input[i] * scale;
                }
            }
        }

        #endregion

        #region 通用归一化与反归一化方法

        /// <summary>
        /// 通用归一化方法 - 根据指定的归一化方法进行归一化
        /// </summary>
        /// <param name="samples">输入样本数组</param>
        /// <param name="method">归一化方法</param>
        /// <param name="normalizationFactor">输出参数：归一化因子（用于还原数据）</param>
        /// <param name="targetPeak">目标峰值（仅用于Peak方法，默认1.0）</param>
        /// <param name="targetRMS">目标RMS值（仅用于RMS方法，默认0.1）</param>
        /// <returns>归一化后的样本数组</returns>
        public static float[] Normalize(float[] samples, NormalizationMethod method, out float normalizationFactor, 
            float targetPeak = 1.0f, float targetRMS = 0.1f)
        {
            normalizationFactor = 1.0f;

            switch (method)
            {
                case NormalizationMethod.Peak:
                    return NormalizePeak(samples, out normalizationFactor, targetPeak);

                case NormalizationMethod.RMS:
                    return NormalizeRMS(samples, out normalizationFactor, targetRMS);

                case NormalizationMethod.Logarithmic:
                    // 对数归一化需要先进行峰值归一化
                    var peakNormalized = NormalizePeak(samples, out normalizationFactor, targetPeak);
                    return NormalizeLogarithmic(peakNormalized);

                case NormalizationMethod.AGC:
                    // AGC归一化不返回归一化因子（动态调整）
                    normalizationFactor = 1.0f;
                    return NormalizeAGC(samples, targetRMS: targetRMS);

                case NormalizationMethod.Linear:
                default:
                    // 线性归一化（简单的线性映射）
                    return NormalizeLinear(samples, out normalizationFactor, targetPeak);
            }
        }

        /// <summary>
        /// 通用归一化方法 - 双精度版本
        /// </summary>
        public static double[] Normalize(double[] samples, NormalizationMethod method, out double normalizationFactor,
            double targetPeak = 1.0, double targetRMS = 0.1)
        {
            normalizationFactor = 1.0;

            switch (method)
            {
                case NormalizationMethod.Peak:
                    return NormalizePeak(samples, out normalizationFactor, targetPeak);

                case NormalizationMethod.RMS:
                    return NormalizeRMS(samples, out normalizationFactor, targetRMS);

                case NormalizationMethod.Logarithmic:
                    var peakNormalized = NormalizePeak(samples, out normalizationFactor, targetPeak);
                    return NormalizeLogarithmic(peakNormalized);

                case NormalizationMethod.AGC:
                    normalizationFactor = 1.0;
                    return NormalizeAGC(samples, targetRMS: targetRMS);

                case NormalizationMethod.Linear:
                default:
                    return NormalizeLinear(samples, out normalizationFactor, targetPeak);
            }
        }

        /// <summary>
        /// 通用反归一化方法 - 根据归一化方法还原数据
        /// </summary>
        /// <param name="normalizedSamples">归一化后的样本数组</param>
        /// <param name="method">归一化方法（必须与归一化时使用的方法一致）</param>
        /// <param name="normalizationFactor">归一化因子（归一化时保存的值）</param>
        /// <param name="targetPeak">目标峰值（仅用于Peak方法，默认1.0）</param>
        /// <param name="targetRMS">目标RMS值（仅用于RMS方法，默认0.1）</param>
        /// <returns>还原后的原始数据</returns>
        public static float[] Denormalize(float[] normalizedSamples, NormalizationMethod method, float normalizationFactor,
            float targetPeak = 1.0f, float targetRMS = 0.1f)
        {
            switch (method)
            {
                case NormalizationMethod.Peak:
                    return DenormalizePeak(normalizedSamples, normalizationFactor, targetPeak);

                case NormalizationMethod.RMS:
                    return DenormalizeRMS(normalizedSamples, normalizationFactor, targetRMS);

                case NormalizationMethod.Logarithmic:
                    // 对数归一化的反归一化：先反对数，再反峰值归一化
                    var denormLog = DenormalizeLogarithmic(normalizedSamples);
                    return DenormalizePeak(denormLog, normalizationFactor, targetPeak);

                case NormalizationMethod.AGC:
                    // AGC无法完全还原（动态调整，信息已丢失）
                    throw new NotSupportedException("AGC归一化无法完全还原原始数据，因为它是动态调整的");

                case NormalizationMethod.Linear:
                default:
                    return DenormalizeLinear(normalizedSamples, normalizationFactor, targetPeak);
            }
        }

        /// <summary>
        /// 通用反归一化方法 - 双精度版本
        /// </summary>
        public static double[] Denormalize(double[] normalizedSamples, NormalizationMethod method, double normalizationFactor,
            double targetPeak = 1.0, double targetRMS = 0.1)
        {
            switch (method)
            {
                case NormalizationMethod.Peak:
                    return DenormalizePeak(normalizedSamples, normalizationFactor, targetPeak);

                case NormalizationMethod.RMS:
                    return DenormalizeRMS(normalizedSamples, normalizationFactor, targetRMS);

                case NormalizationMethod.Logarithmic:
                    var denormLog = DenormalizeLogarithmic(normalizedSamples);
                    return DenormalizePeak(denormLog, normalizationFactor, targetPeak);

                case NormalizationMethod.AGC:
                    throw new NotSupportedException("AGC归一化无法完全还原原始数据，因为它是动态调整的");

                case NormalizationMethod.Linear:
                default:
                    return DenormalizeLinear(normalizedSamples, normalizationFactor, targetPeak);
            }
        }

        #endregion

        #region 线性归一化（Linear Normalization）

        /// <summary>
        /// 线性归一化 - 简单的线性映射到目标范围
        /// </summary>
        /// <param name="samples">输入样本数组</param>
        /// <param name="normalizationFactor">输出参数：归一化因子（用于还原）</param>
        /// <param name="targetPeak">目标峰值（默认1.0）</param>
        /// <returns>归一化后的样本数组</returns>
        public static float[] NormalizeLinear(float[] samples, out float normalizationFactor, float targetPeak = 1.0f)
        {
            if (samples == null || samples.Length == 0)
            {
                normalizationFactor = 1.0f;
                return samples ?? Array.Empty<float>();
            }

            float maxAbs = FindMaxAbsolute(samples);
            normalizationFactor = maxAbs < 1e-10f ? 1.0f : maxAbs;

            if (maxAbs < 1e-10f)
                return samples;

            float scale = targetPeak / maxAbs;
            float[] normalized = new float[samples.Length];
            NormalizeByScaleSIMD(samples, normalized, scale);

            return normalized;
        }

        /// <summary>
        /// 线性归一化 - 双精度版本
        /// </summary>
        public static double[] NormalizeLinear(double[] samples, out double normalizationFactor, double targetPeak = 1.0)
        {
            if (samples == null || samples.Length == 0)
            {
                normalizationFactor = 1.0;
                return samples ?? Array.Empty<double>();
            }

            double maxAbs = FindMaxAbsolute(samples);
            normalizationFactor = maxAbs < 1e-10 ? 1.0 : maxAbs;

            if (maxAbs < 1e-10)
                return samples;

            double scale = targetPeak / maxAbs;
            double[] normalized = new double[samples.Length];
            NormalizeByScaleSIMD(samples, normalized, scale);

            return normalized;
        }

        /// <summary>
        /// 反线性归一化
        /// </summary>
        public static float[] DenormalizeLinear(float[] normalizedSamples, float normalizationFactor, float targetPeak = 1.0f)
        {
            return DenormalizePeak(normalizedSamples, normalizationFactor, targetPeak);
        }

        /// <summary>
        /// 反线性归一化 - 双精度版本
        /// </summary>
        public static double[] DenormalizeLinear(double[] normalizedSamples, double normalizationFactor, double targetPeak = 1.0)
        {
            return DenormalizePeak(normalizedSamples, normalizationFactor, targetPeak);
        }

        #endregion

        #region RMS反归一化（RMS Denormalization）

        /// <summary>
        /// 反RMS归一化 - 从RMS归一化数据还原原始数据
        /// </summary>
        /// <param name="normalizedSamples">归一化后的样本数组</param>
        /// <param name="normalizationFactor">归一化因子（原始RMS值）</param>
        /// <param name="targetRMS">目标RMS值（默认0.1，必须与归一化时使用的值一致）</param>
        /// <returns>还原后的原始数据</returns>
        public static float[] DenormalizeRMS(float[] normalizedSamples, float normalizationFactor, float targetRMS = 0.1f)
        {
            if (normalizedSamples == null || normalizedSamples.Length == 0)
                return normalizedSamples ?? Array.Empty<float>();

            if (normalizationFactor < 1e-10f)
                return normalizedSamples;

            // 还原公式：original = normalized * (normalizationFactor / targetRMS)
            float scale = normalizationFactor / targetRMS;
            float[] original = new float[normalizedSamples.Length];
            
            NormalizeByScaleSIMD(normalizedSamples, original, scale);
            
            return original;
        }

        /// <summary>
        /// 反RMS归一化 - 双精度版本
        /// </summary>
        public static double[] DenormalizeRMS(double[] normalizedSamples, double normalizationFactor, double targetRMS = 0.1)
        {
            if (normalizedSamples == null || normalizedSamples.Length == 0)
                return normalizedSamples ?? Array.Empty<double>();

            if (normalizationFactor < 1e-10)
                return normalizedSamples;

            double scale = normalizationFactor / targetRMS;
            double[] original = new double[normalizedSamples.Length];
            
            NormalizeByScaleSIMD(normalizedSamples, original, scale);
            
            return original;
        }

        #endregion

        #region 对数反归一化（Logarithmic Denormalization）

        /// <summary>
        /// 反对数归一化 - 从对数归一化数据还原
        /// </summary>
        /// <param name="normalizedSamples">对数归一化后的样本数组</param>
        /// <param name="compressionFactor">压缩因子（默认9.0，必须与归一化时使用的值一致）</param>
        /// <returns>还原后的样本数组（已进行峰值归一化，但未还原到原始范围）</returns>
        public static float[] DenormalizeLogarithmic(float[] normalizedSamples, float compressionFactor = 9.0f)
        {
            if (normalizedSamples == null || normalizedSamples.Length == 0)
                return normalizedSamples ?? Array.Empty<float>();

            float[] denormalized = new float[normalizedSamples.Length];
            float invLogScale = MathF.Log10(1.0f + compressionFactor);

            for (int i = 0; i < normalizedSamples.Length; i++)
            {
                float sample = normalizedSamples[i];
                float absSample = Math.Abs(sample);

                if (absSample < 1e-10f)
                {
                    denormalized[i] = 0.0f;
                }
                else
                {
                    // 反对数压缩：expanded = (10^(compressed * invLogScale) - 1) / compressionFactor
                    float expanded = (MathF.Pow(10.0f, absSample * invLogScale) - 1.0f) / compressionFactor;
                    denormalized[i] = Math.Sign(sample) * expanded;
                }
            }

            return denormalized;
        }

        /// <summary>
        /// 反对数归一化 - 双精度版本
        /// </summary>
        public static double[] DenormalizeLogarithmic(double[] normalizedSamples, double compressionFactor = 9.0)
        {
            if (normalizedSamples == null || normalizedSamples.Length == 0)
                return normalizedSamples ?? Array.Empty<double>();

            double[] denormalized = new double[normalizedSamples.Length];
            double invLogScale = Math.Log10(1.0 + compressionFactor);

            for (int i = 0; i < normalizedSamples.Length; i++)
            {
                double sample = normalizedSamples[i];
                double absSample = Math.Abs(sample);

                if (absSample < 1e-10)
                {
                    denormalized[i] = 0.0;
                }
                else
                {
                    double expanded = (Math.Pow(10.0, absSample * invLogScale) - 1.0) / compressionFactor;
                    denormalized[i] = Math.Sign(sample) * expanded;
                }
            }

            return denormalized;
        }

        #endregion
    }
}

