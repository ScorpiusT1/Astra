using System.IO;
using System.Text.Json;

namespace Astra.Services.Home
{
    public sealed class YieldDailyStatsService : IYieldDailyStatsService
    {
        private readonly object _sync = new();
        private readonly string _filePath;
        private Dictionary<string, YieldDailyStats> _daily = new();

        public YieldDailyStatsService()
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Astra");
            Directory.CreateDirectory(baseDir);
            _filePath = Path.Combine(baseDir, "yield-daily-stats.json");
            Load();
        }

        public YieldDailyStats GetToday()
        {
            lock (_sync)
            {
                var key = GetTodayKey();
                if (!_daily.TryGetValue(key, out var stats))
                {
                    stats = new YieldDailyStats();
                    _daily[key] = stats;
                }

                return new YieldDailyStats
                {
                    PassCount = stats.PassCount,
                    FailCount = stats.FailCount
                };
            }
        }

        public void AddToday(int passDelta, int failDelta)
        {
            if (passDelta <= 0 && failDelta <= 0)
                return;

            lock (_sync)
            {
                var key = GetTodayKey();
                if (!_daily.TryGetValue(key, out var stats))
                {
                    stats = new YieldDailyStats();
                    _daily[key] = stats;
                }

                if (passDelta > 0)
                    stats.PassCount += passDelta;
                if (failDelta > 0)
                    stats.FailCount += failDelta;

                Save();
            }
        }

        public void ClearToday()
        {
            lock (_sync)
            {
                _daily[GetTodayKey()] = new YieldDailyStats();
                Save();
            }
        }

        private void Load()
        {
            if (!File.Exists(_filePath))
                return;

            try
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, YieldDailyStats>>(json);
                if (data != null)
                    _daily = data;
            }
            catch
            {
                _daily = new Dictionary<string, YieldDailyStats>();
            }
        }

        private void Save()
        {
            var json = JsonSerializer.Serialize(_daily, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_filePath, json);
        }

        private static string GetTodayKey() => DateTime.Now.ToString("yyyy-MM-dd");
    }
}
