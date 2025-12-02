using Astra.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Astra.UI.Serivces
{
    /// <summary>
    /// 默认拖拽数据构建器 - 构建向后兼容的拖拽数据格式
    /// </summary>
    public class DefaultDragDropPayloadBuilder : IDragDropPayloadBuilder
    {
        /// <summary>
        /// 构建拖拽数据对象
        /// </summary>
        public DataObject BuildPayload(IToolItem toolItem, IToolCategory<IToolItem> category)
        {
            if (toolItem == null)
            {
                return null;
            }

            try
            {
                var data = new DataObject();

                // 设置多种数据格式以支持不同的拖放目标
                data.SetData(DragDropDataFormats.ToolItem, toolItem);
                data.SetData(DataFormats.StringFormat, toolItem.Name ?? string.Empty);

                if (category != null)
                {
                    data.SetData(DragDropDataFormats.ToolCategoryName, category.Name ?? string.Empty);
                }

                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建拖拽数据时发生错误: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 拖拽数据格式常量 - 遵循单一职责原则，集中管理所有拖拽数据格式字符串
    /// </summary>
    public static class DragDropDataFormats
    {
        /// <summary>
        /// 工具项数据格式
        /// </summary>
        public const string ToolItem = "FlowEditor/ToolItem";

        /// <summary>
        /// 工具类别名称数据格式
        /// </summary>
        public const string ToolCategoryName = "FlowEditor/ToolCategoryName";

        /// <summary>
        /// 标准字符串格式（用于向后兼容）
        /// </summary>
        public const string StringFormat = "System.String";
    }
}
