namespace Astra.Core.Devices.Buffers
{
    /// <summary>
    /// 缓冲区状态
    /// </summary>
    public struct BufferStatus
    {
        public int Capacity { get; set; }
        public int Count { get; set; }
        public int AvailableSpace { get; set; }
        public long TotalReceived { get; set; }
        public long TotalRead { get; set; }
        public long DroppedCount { get; set; }
        public double UsagePercentage => Capacity > 0 ? Count * 100.0 / Capacity : 0;
    }
}