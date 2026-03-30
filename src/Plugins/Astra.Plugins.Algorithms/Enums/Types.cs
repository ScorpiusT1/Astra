using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.Algorithms.APIs
{
    public enum Format
    {
        Rms = 0,
        Peak = 1,
        Peak2Peak = 2,
    }

    public enum Window
    {
        Uniform = 0,
        Hanning = 1,
    }

    public enum Weight
    {
        Linear = 0,
        A = 1,
    }

    public enum Scale
    {
        Linear = 0,
        dB = 1
    }

    public enum RpmTrigger
    {
        Up = 0,
        ImmUp = 1,
    }

    public enum Average
    {
        Energy,
        Mean,
        Max,
    }
}
