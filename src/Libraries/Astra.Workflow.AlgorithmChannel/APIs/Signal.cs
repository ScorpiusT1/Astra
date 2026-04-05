using System;
using System.Runtime.InteropServices;

namespace Astra.Workflow.AlgorithmChannel.APIs
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Signal
    {
        public Signal(double[] samples, double deltaTime, long unixTime = 0)
        {
            Samples = samples.ToIntPtr(out _);
            Length = samples.Length;
            DeltaTime = deltaTime;
            UnixTime = unixTime;
        }

        public IntPtr Samples { get; set; }

        public int Length { get; set; }
        public double DeltaTime { get; set; }

        public long UnixTime { get; set; }
    }
}
