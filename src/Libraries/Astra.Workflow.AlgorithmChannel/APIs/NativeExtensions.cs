using System;
using System.Runtime.InteropServices;

namespace Astra.Workflow.AlgorithmChannel.APIs
{
    public static class NativeExtensions
    {
        public static IntPtr ToIntPtr(this double[] array, out int length)
        {
            int size = array.Length * sizeof(double);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(array, 0, ptr, array.Length);

            length = array.Length;
            return ptr;
        }
    }
}
