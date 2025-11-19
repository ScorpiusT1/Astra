using System.Runtime.CompilerServices;

namespace Astra.Plugins.DataAcquisition.Commons
{
    /// <summary>
    /// 数据块，固定大小以提高性能和内存管理
    /// </summary>
    public class DataChunk
    {
        public double[] Data { get; }
        public int Count { get; set; }
        public int Capacity => Data.Length;
        public bool IsFull => Count >= Capacity;

        public DataChunk(int capacity)
        {
            Data = new double[capacity];
            Count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(double value)
        {
            if (IsFull) return false;
            Data[Count++] = value;
            return true;
        }

        public int AddRange(ReadOnlySpan<double> values)
        {
            int available = Capacity - Count;
            int toWrite = Math.Min(available, values.Length);
            values.Slice(0, toWrite).CopyTo(Data.AsSpan(Count));
            Count += toWrite;
            return toWrite;
        }
    }
}
