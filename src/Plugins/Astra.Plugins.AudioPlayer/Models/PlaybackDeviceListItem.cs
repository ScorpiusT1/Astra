using System;

namespace Astra.Plugins.AudioPlayer.Models
{
    /// <summary>
    /// 播放设备下拉项：界面只绑定 <see cref="DisplayName"/>，选中后保存 <see cref="MmDeviceId"/>。
    /// </summary>
    public sealed class PlaybackDeviceListItem : IEquatable<PlaybackDeviceListItem>
    {
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>为 null 表示使用系统默认输出。</summary>
        public string? MmDeviceId { get; init; }

        public bool Equals(PlaybackDeviceListItem? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(MmDeviceId, other.MmDeviceId, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is PlaybackDeviceListItem o && Equals(o);

        public override int GetHashCode() => MmDeviceId?.GetHashCode(StringComparison.Ordinal) ?? 0;

        public override string ToString() => DisplayName;
    }
}
