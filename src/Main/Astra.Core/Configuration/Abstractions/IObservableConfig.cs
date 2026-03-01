namespace Astra.Core.Configuration.Abstractions
{
    /// <summary>
    /// 支持属性变更通知的配置接口。
    /// 实现此接口后，属性变更时会同时触发 <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// 和 <see cref="ConfigChanged"/>（后者携带旧值/新值信息）。
    /// </summary>
    public interface IObservableConfig : IConfig
    {
        event EventHandler<ConfigChangedEventArgs> ConfigChanged;
    }

    /// <summary>
    /// 配置属性变更事件参数。
    /// </summary>
    public class ConfigChangedEventArgs : EventArgs
    {
        public string PropertyName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.Now;
    }
}
