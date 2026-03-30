using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.Algorithms.APIs
{
    
    public static partial class NvhInterop
    {
        private const string DLLPATH = "Lib/Algorithm/BrcSignalKit.dll";

        [DllImport(DLLPATH, EntryPoint = "LoadLicense", CharSet = CharSet.Ansi)]
        public static extern int LoadLicense(string licensePath);

        [DllImport(DLLPATH, EntryPoint = "GetLastErrorMessage", CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public static extern string GetLastErrorMessage(int errorCode);

        [DllImport(DLLPATH, EntryPoint = "OverallLevelSpectral")]
        public static extern int OverallLevelSpectral(
            Signal signal,
            int spectrumLines,
            double increment,
            double referenceValue,
            int windowType,
            int weightType,
            int scaleType,
            ref IntPtr data,
            ref int bins);

        [DllImport(DLLPATH, EntryPoint = "Octave")]
        public static extern int Octave(
            IntPtr amplitudeSpectraPtr, 
            int SpectraLength, 
            double frequencyStep, 
            int windowType, 
            int octaveType, 
            int scaleType, 
            double referenceValue, 
            ref IntPtr bandLevels, 
            ref IntPtr bandCenters, 
            ref IntPtr bandLowers, 
            ref IntPtr bandUppers, 
            ref int bandCount);


        [DllImport(DLLPATH, EntryPoint = "OrderSection")]
        public static extern int OrderSection(
            Signal signal,
            Rpm rpm,
            int spectrumLines,
            double targetOrder,
            double orderBandwidth,
            double minRpm,
            double maxRpm,
            double rpmStep,
            double referenceValue,
            int formatType,
            int windowType,
            int weightType,
            int scaleType,
            int rpmTriggerType,
            ref IntPtr data,
            ref IntPtr rpmAxis,
            ref int bins);

        [DllImport(DLLPATH, EntryPoint = "AveragedSpectrumByIncrement")]
        public static extern int AveragedSpectrum(
            Signal signal, 
            int spectrumLines,
            double increment, 
            int formatType, 
            int averageType, 
            int windowType, 
            int weightType, 
            ref IntPtr data, 
            ref int bins);

        [DllImport(DLLPATH, EntryPoint = "GenerateTimeFrequencyColormapByIncrement")]
        public static extern int TimeFrequencyMap(
            Signal signal,
            int spectrumLines,
            double increment,
            double startTime,
            double endTime,
            double referenceValue,
            int formatType,
            int windowType,
            int weightType,
            int scaleType,
            ref IntPtr data,
            ref int timeBins,
            ref int frequencyBins);

        [DllImport(DLLPATH, EntryPoint = "GenerateRpmFrequencyColormap")]
        public static extern int RpmFrequencyMap(
            Signal signal,
            Rpm rpm,
            int spectrumLines,
            double minRpm,
            double maxRpm,
            double rpmStep,
            double referenceValue,
            int formatType,
            int windowType,
            int weightType,
            int scaleType,
            int rpmTriggerType,
            ref IntPtr data,
            ref IntPtr rpmAxis,
            ref IntPtr frequencyAxis,
            ref int rpmBins,
            ref int frequencyBins);

        [DllImport(DLLPATH, EntryPoint = "GenerateRpmOrderColormap")]
        public static extern int RpmOrderMap(
            Signal signal,
            Rpm rpm,
            double maxOrder,
            double orderResolution,
            double oversamplingFactor,
            double minRpm,
            double maxRpm,
            double rpmStep,
            double referenceValue,
            int formatType,
            int windowType,
            int weightType,
            int scaleType,
            ref IntPtr data,
            ref IntPtr rpmAxis,
            ref IntPtr orderAxis,
            ref int rpmBins,
            ref int orderBins);

        [DllImport(DLLPATH, EntryPoint = "GetEnvelope")]
        public static extern int HilbertEnvelope(
            Signal signal,
            ref IntPtr data,
            ref int bins);

        [DllImport(DLLPATH, EntryPoint = "GetEnvelopeExFixed")]
        public static extern int HilbertEnvelopeExFixed(
            Signal signal, 
            double centerFrequency, 
            double bandwidth, 
            ref IntPtr data, 
            ref int bins);

        [DllImport(DLLPATH, EntryPoint = "GetEnvelopeExTracked")]
        public static extern int HilbertEnvelopeExTracked(
            Signal signal, 
            IntPtr rpm, int 
            rpmBins, 
            double centerOrder, 
            double bandwidth, 
            int windowLength, 
            double minFreq, 
            double maxFreq, 
            ref IntPtr data, 
            ref int bins);


        [DllImport(DLLPATH, EntryPoint = "GetEnvelopeSpectra")]
        public static extern int HilbertEnvelopeSpectra(
            Signal signal, 
            int windowType, 
            int formatType, 
            ref IntPtr data, 
            ref int outLength, 
            ref IntPtr freqAxis, 
            ref int freqBins);

        [DllImport(DLLPATH, EntryPoint = "GetAvgEnvelopeSpectra")]
        public static extern int HilbertEnvelopeAvgSpectra(
            Signal signal, 
            int segmentLength, 
            double overlap, 
            int formatType, 
            int averageType, 
            int weightType, 
            int windowType, 
            ref IntPtr data, 
            ref int outLength, 
            ref IntPtr freqAxis, 
            ref int freqBins);

        [DllImport(DLLPATH, EntryPoint = "MorletWaveletTransform")]
        public static extern int MorletWaveletTransform(
            Signal signal, 
            IntPtr frequencyAxis, 
            int frequencyBins, 
            double nCycles, 
            int scaleType, 
            double referenceValue, 
            ref IntPtr data, 
            ref int timeBins, 
            ref int freqBins);

        [DllImport(DLLPATH, EntryPoint = "LmsMorletWaveletTransform")]
        public static extern int LmsMorletWaveletTransform(
            Signal signal,
            double minFreq, 
            double maxFreq, 
            int octave, 
            int scaleType, 
            double referenceValue,
            ref IntPtr data, 
            ref int timeBins,
            ref IntPtr freqAxis, 
            ref int freqBins);

        [DllImport(DLLPATH, EntryPoint = "ModulationSpectrumAnalyze")]
        public static extern int ModulationSpectrumAnalyze(
            Signal signal, 
            double frequencyResolution, 
            double cutoffFreq, 
            int scaleType, 
            double referenceValue, 
            ref IntPtr spectrogram, 
            ref IntPtr freqAxis, 
            ref IntPtr timeAxis, 
            ref IntPtr modulationDepth, 
            ref IntPtr modulationFreq, 
            ref int freqBins, 
            ref int timeBins);


        [DllImport(DLLPATH, EntryPoint = "ModulationSpectrumAnalyzeStft")]
        public static extern int ModulationSpectrumAnalyzeStft(
            Signal signal, 
            int windowSize, 
            int hopSize, 
            double cutoffFreq, 
            int scaleType, 
            double referenceValue, 
            ref IntPtr spectrogram, 
            ref IntPtr freqAxis, 
            ref IntPtr timeAxis, 
            ref IntPtr modulationDepth, 
            ref IntPtr modulationFreq, 
            ref int freqBins, 
            ref int timeBins);



        [DllImport(DLLPATH, EntryPoint = "StationaryLoudnessAnalyze")]
        public static extern int StationaryLoudnessAnalyze(
            Signal signal, 
            int soundField, 
            double skipInSec, 
            ref double outLoudness, 
            ref IntPtr outSpecLoudness, 
            ref IntPtr outBarkAxis, 
            ref IntPtr outFreqAxis, 
            ref int barkBins);


        [DllImport(DLLPATH, EntryPoint = "TimeVaryingLoudnessAnalyze")]
        public static extern int TimeVaryingLoudnessAnalyze(
            Signal signal, 
            int soundField, 
            double skipInSec, 
            ref IntPtr outLoudness, 
            ref IntPtr outSpecLoudness, 
            ref IntPtr outBarkAxis, 
            ref IntPtr outFreqAxis, 
            ref IntPtr outTimeAxis, 
            ref int barkBins, 
            ref int timeBins);


        [DllImport(DLLPATH, EntryPoint = "StationarySharpnessAnalyze")]
        public static extern int StationarySharpnessAnalyze(
            Signal signal, 
            int sharpnessWeighting, 
            int soundField, 
            double skipInSec, 
            ref double outSharpness, 
            ref IntPtr outSpecSharpness, 
            ref IntPtr outBarkAxis, 
            ref IntPtr outFreqAxis, 
            ref int outBarkBins);



        [DllImport(DLLPATH, EntryPoint = "TimeVaryingSharpnessAnalyze")]
        public static extern int TimeVaryingSharpnessAnalyze(
            Signal signal, 
            int sharpnessWeighting, 
            int soundField, 
            double skipInSec, 
            ref IntPtr outSharpness, 
            ref IntPtr outSpecSharpness, 
            ref IntPtr outBarkAxis, 
            ref IntPtr outFreqAxis, 
            ref IntPtr outTimeAxis, 
            ref int barkBins, 
            ref int timeBins);




        [DllImport(DLLPATH, EntryPoint = "RoughnessAnalyze")]
        public static extern int RoughnessAnalyze(
            Signal signal, 
            int soundField, 
            double skipInSec, 
            ref double outRoughness, 
            ref IntPtr outRoughnessTimeDep, 
            ref IntPtr outRoughnessSpec, 
            ref IntPtr outRoughnessSpecAvg, 
            ref IntPtr bandAxis, 
            ref IntPtr barkAxis, 
            ref IntPtr freqAxis,
            ref int bandBins, ref 
            IntPtr timeAxis, 
            ref int timeBins);


        [DllImport(DLLPATH, EntryPoint = "FluctuationStrengthAnalyze")]
        public static extern int FluctuationStrengthAnalyze(
            Signal signal, 
            int fluctuationMethod, 
            ref double outTotalFluctuation, 
            ref IntPtr outFluctuationTimeDep, 
            ref IntPtr outFluctuationSpec, 
            ref IntPtr outFluctuationSpecAvg, 
            ref IntPtr barkAxis, 
            ref IntPtr freqAxis, 
            ref int barkBins, 
            ref IntPtr timeAxis, 
            ref int timeBins);
    }
}
