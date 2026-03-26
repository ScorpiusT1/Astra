namespace Astra.Services.Home
{
    public sealed class YieldDailyStats
    {
        public int PassCount { get; set; }

        public int FailCount { get; set; }
    }

    public interface IYieldDailyStatsService
    {
        YieldDailyStats GetToday();

        void AddToday(int passDelta, int failDelta);

        void ClearToday();
    }
}
