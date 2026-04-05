using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Astra.Workflow.AlgorithmChannel.APIs
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rpm
    {
        public Rpm(double[] rpmValues, double increment, double startOffset = 0)
        {
            RpmValues = rpmValues.ToIntPtr(out _);
            TimeValues = Enumerable.Range(0, rpmValues.Length).Select(i => startOffset + i * increment).ToArray().ToIntPtr(out _);
            Length = rpmValues.Length;
        }
        public IntPtr RpmValues { get; set; }

        public IntPtr TimeValues { get; set; }

        public int Length { get; set; }
    }
}
