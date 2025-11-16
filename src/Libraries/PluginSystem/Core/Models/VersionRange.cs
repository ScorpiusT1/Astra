namespace Addins.Core.Models
{
    /// <summary>
    /// 版本范围
    /// </summary>
    public class VersionRange
    {
        public Version MinVersion { get; set; }
        public Version MaxVersion { get; set; }
        public bool IncludeMin { get; set; } = true;
        public bool IncludeMax { get; set; } = true;

        public bool IsInRange(Version version)
        {
            var minCheck = MinVersion == null ||
                          (IncludeMin ? version >= MinVersion : version > MinVersion);
            var maxCheck = MaxVersion == null ||
                          (IncludeMax ? version <= MaxVersion : version < MaxVersion);
            return minCheck && maxCheck;
        }
    }
}
