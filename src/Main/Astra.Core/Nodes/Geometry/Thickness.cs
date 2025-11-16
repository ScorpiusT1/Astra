using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Nodes.Geometry
{
    // 端口边距
    public readonly struct Thickness
    {
        public double Left { get; }
        public double Top { get; }
        public double Right { get; }
        public double Bottom { get; }

        public Thickness(double uniformLength)
        {
            Left = Top = Right = Bottom = uniformLength;
        }

        public Thickness(double left, double top, double right, double bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public override string ToString()
        {
            return $"({Left}, {Top}, {Right}, {Bottom})";
        }
    }
}
