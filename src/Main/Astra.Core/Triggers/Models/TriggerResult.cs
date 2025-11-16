using Astra.Core.Triggers.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Triggers.Models
{
    #region ========== 触发结果封装 ==========  

    /// <summary>  
    /// 触发结果（子类返回给父类）  
    /// </summary>  
    public class TriggerResult
    {
        /// <summary>  
        /// 是否触发  
        /// </summary>  
        public bool IsTriggered { get; set; }

        /// <summary>  
        /// 触发源类型  
        /// </summary>  
        public TriggerSource Source { get; set; }

        /// <summary>  
        /// 触发数据（包含SN等）  
        /// </summary>  
        public Dictionary<string, object> Data { get; set; }

        /// <summary>  
        /// 创建未触发结果  
        /// </summary>  
        public static TriggerResult NotTriggered()
        {
            return new TriggerResult
            {
                IsTriggered = false
            };
        }

        /// <summary>  
        /// 创建触发结果  
        /// </summary>  
        public static TriggerResult Triggered(TriggerSource source, Dictionary<string, object> data)
        {
            return new TriggerResult
            {
                IsTriggered = true,
                Source = source,
                Data = data ?? new Dictionary<string, object>()
            };
        }

        /// <summary>  
        /// 便捷方法：创建触发结果（带SN）  
        /// </summary>  
        public static TriggerResult TriggeredWithSN(TriggerSource source, string sn, Dictionary<string, object> additionalData = null)
        {
            var data = additionalData ?? new Dictionary<string, object>();
            data["SN"] = sn;

            return new TriggerResult
            {
                IsTriggered = true,
                Source = source,
                Data = data
            };
        }
    }

    #endregion
}
