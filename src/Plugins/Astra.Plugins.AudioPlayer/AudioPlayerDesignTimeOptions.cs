using Astra.Plugins.DataAcquisition.Providers;
using Astra.Core.Constants;
using NVHDataBridge.IO.WAV;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.AudioPlayer
{
    /// <summary>
    /// 属性编辑器下拉：与 Limits 插件中卡控节点相同规则，采集卡首项为未选择，选定后通道列表刷新。
    /// </summary>
    public static class AudioPlayerDesignTimeOptions
    {
        public const string UnselectedLabel = AstraSharedConstants.DesignTimeLabels.Unselected;

        public const string UseFirstChannelInGroupLabel = AstraSharedConstants.DesignTimeLabels.UseFirstChannelInGroup;

        /// <summary>播放设备首项，表示使用系统默认 WASAPI 输出。</summary>
        public const string DefaultPlaybackDeviceLabel = AstraSharedConstants.DesignTimeLabels.DefaultPlaybackDevice;

        /// <summary>
        /// 播放设备下拉（静态方法，供属性编辑器 ItemsSource 绑定）。
        /// 仅 <see cref="PlaybackDeviceListItem.DisplayName"/> 用于展示；ID 存在 <see cref="PlaybackDeviceListItem.MmDeviceId"/>。
        /// 底层枚举由 <see cref="RealtimeAudioPlayer.EnumerateWasapiRenderDevices"/> 提供缓存；插件启动时会预加载。
        /// </summary>
        public static IEnumerable<PlaybackDeviceListItem> GetPlaybackDeviceOptions()
        {
            var list = new List<PlaybackDeviceListItem>
            {
                new PlaybackDeviceListItem { DisplayName = DefaultPlaybackDeviceLabel, MmDeviceId = null }
            };

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                DefaultPlaybackDeviceLabel
            };

            foreach (var d in RealtimeAudioPlayer.EnumerateWasapiRenderDevices())
            {
                if (string.IsNullOrEmpty(d.MmDeviceId))
                {
                    continue;
                }

                var baseName = string.IsNullOrWhiteSpace(d.ProductName) ? "输出设备" : d.ProductName.Trim();
                var displayName = baseName;
                if (!usedNames.Add(displayName))
                {
                    var tail = d.MmDeviceId.Length > 10 ? d.MmDeviceId.Substring(d.MmDeviceId.Length - 8) : d.MmDeviceId;
                    displayName = $"{baseName} ({tail})";
                }

                list.Add(new PlaybackDeviceListItem { DisplayName = displayName, MmDeviceId = d.MmDeviceId });
            }

            return list;
        }

        public static IEnumerable<string> GetAcquisitionDeviceNames()
        {
            var list = DataAcquisitionCardProvider.GetDataAcquisitionNames().ToList();
            list.Insert(0, UnselectedLabel);
            return list;
        }

        public static IEnumerable<string> GetChannelNamesForDevice(string? deviceDisplayName)
        {
            var d = deviceDisplayName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(d) || string.Equals(d, UnselectedLabel, StringComparison.Ordinal))
            {
                return new[] { UnselectedLabel };
            }

            var list = DataAcquisitionCardProvider.GetConfiguredChannelNamesForDeviceDisplayName(d).ToList();
            if (list.Count > 0 && list[0] == string.Empty)
            {
                list[0] = UseFirstChannelInGroupLabel;
            }

            return list;
        }
    }
}
