namespace Astra.Plugins.DataAcquisition.Configs
{
    /// <summary>耦合方式</summary>
    public enum CouplingMode : int
    {
        DC,     // 直流耦合
        AC,     // 交流耦合
        ICP     // ICP/IEPE供电
    }
}
