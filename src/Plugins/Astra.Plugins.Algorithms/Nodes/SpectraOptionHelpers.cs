using Astra.Plugins.Algorithms.APIs;
using Astra.Workflow.AlgorithmChannel.APIs;

namespace Astra.Plugins.Algorithms.Nodes
{
    internal static class SpectraOptionHelpers
    {
        public static void ResolveSpectrumLinesAndIncrement(
            Signal signal,
            SpectraCalcOptions calcOpt,
            SpectraStepOptions stepOpt,
            out double spectrumLines,
            out double increment)
        {
            switch (calcOpt.CalcType)
            {
                case SpectraCalcType.SpectrumLines:
                    spectrumLines = calcOpt.CalcValue;
                    break;
                case SpectraCalcType.Resolution:
                    spectrumLines = 1 / (signal.DeltaTime * calcOpt.CalcValue * 2);
                    break;
                case SpectraCalcType.FrameLength:
                    spectrumLines = calcOpt.CalcValue / 2;
                    break;
                default:
                    throw new InvalidOperationException("Invalid SpectraCalcType");
            }

            switch (stepOpt.StepType)
            {
                case SpectraStepType.Increment:
                    increment = stepOpt.StepValue;
                    break;
                case SpectraStepType.Overlap:
                    increment = (1 - stepOpt.StepValue) * (spectrumLines * signal.DeltaTime * 2);
                    break;
                default:
                    throw new InvalidOperationException("Invalid SpectraStepType");
            }
        }

        public static void ResolveHilbertAvgSegment(
            Signal signal,
            SpectraCalcOptions calcOpt,
            SpectraStepOptions stepOpt,
            out double segmentLength,
            out double overlap)
        {
            switch (calcOpt.CalcType)
            {
                case SpectraCalcType.SpectrumLines:
                    segmentLength = calcOpt.CalcValue * 2;
                    break;
                case SpectraCalcType.Resolution:
                    segmentLength = 1 / (signal.DeltaTime * calcOpt.CalcValue);
                    break;
                case SpectraCalcType.FrameLength:
                    segmentLength = calcOpt.CalcValue;
                    break;
                default:
                    throw new InvalidOperationException("Invalid SpectraCalcType");
            }

            switch (stepOpt.StepType)
            {
                case SpectraStepType.Increment:
                    overlap = 1 - stepOpt.StepValue / segmentLength / signal.DeltaTime;
                    break;
                case SpectraStepType.Overlap:
                    overlap = stepOpt.StepValue;
                    break;
                default:
                    throw new InvalidOperationException("Invalid SpectraStepType");
            }
        }
    }
}
