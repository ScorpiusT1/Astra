using System.ComponentModel;
using System.Threading;

namespace Astra.Core.Services.Ui;

/// <summary>
/// 主窗口级忙碌遮罩与可取消操作协作（单例，宿主与各模块/插件共用）。
/// </summary>
public interface IBusyService : INotifyPropertyChanged
{
    bool IsBusy { get; }

    string BusyMessage { get; }

    /// <summary>是否显示取消按钮（由当前最内层 <see cref="BeginBusy"/> 的配置决定）。</summary>
    bool ShowCancelButton { get; }

    string CancelButtonText { get; }

    /// <summary>当前最内层忙碌域的取消令牌；无忙碌或不允许取消时为 <see cref="CancellationToken.None"/>。</summary>
    CancellationToken CurrentCancellationToken { get; }

    /// <summary>
    /// 进入忙碌状态（可嵌套）。必须通过返回的 <see cref="IDisposable"/> 在 <c>finally</c> 或 <c>using</c> 中结束。
    /// </summary>
    IDisposable BeginBusy(string message, BusyRequestOptions? options = null);

    /// <summary>请求取消当前最内层允许取消的忙碌域。</summary>
    void RequestCancel();
}
