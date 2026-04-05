namespace Astra.Core.Reporting
{
    /// <summary>
    /// 提供软件配置中的工站名与线体名，供归档文件名与报告元数据使用（由宿主注册实现）。
    /// </summary>
    public interface IReportStationLineSource
    {
        /// <summary>返回 (工站名, 线体名)；未配置时可为空字符串。</summary>
        (string StationName, string LineName) GetStationAndLine();
    }
}
