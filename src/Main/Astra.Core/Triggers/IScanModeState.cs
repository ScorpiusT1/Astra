using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Astra.Core.Triggers
{
    /// <summary>
    /// Home 运行模式：自动（触发器轮询）与手动（扫码）共用状态，供宿主视图与触发器生命周期同步。
    /// </summary>
    public interface IScanModeState
    {
        /// <summary>
        /// 为 true 时表示当前为自动模式（对应 <c>!IsManualScanMode</c>）。
        /// </summary>
        bool IsAutoScanMode { get; set; }
    }

    /// <summary>
    /// 默认实现：进程内单例，由 Home 在切换自动/手动时更新。
    /// </summary>
    public sealed class HomeScanModeState : IScanModeState, INotifyPropertyChanged
    {
        private bool _isAutoScanMode = true;

        public bool IsAutoScanMode
        {
            get => _isAutoScanMode;
            set
            {
                if (_isAutoScanMode == value)
                {
                    return;
                }

                _isAutoScanMode = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
