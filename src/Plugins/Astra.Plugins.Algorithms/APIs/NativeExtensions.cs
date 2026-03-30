using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.Algorithms.APIs
{
    public static class NativeExtensions
    {
        public static IntPtr ToIntPtr(this double[] array, out int length)
        {
            int size = array.Length * sizeof(double);
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            System.Runtime.InteropServices.Marshal.Copy(array, 0, ptr, array.Length);

            length = array.Length;
            return ptr;
        }
    }
}
