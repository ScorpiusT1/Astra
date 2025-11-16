using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Triggers.Enums
{
    #region ========== 触发器类型 ==========

    /// <summary>
    /// 触发器工作类型
    /// </summary>
    public enum TriggerWorkType
    {
        /// <summary>轮询型（PLC、定时器等）- 父类循环调用 CheckTriggerAsync</summary>
        Polling,

        /// <summary>事件型（扫码枪、API等）- 子类通过事件主动调用 RaiseTrigger</summary>
        EventDriven
    }

    #endregion
}
