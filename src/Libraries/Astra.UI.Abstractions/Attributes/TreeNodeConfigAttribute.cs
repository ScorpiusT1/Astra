using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TreeNodeConfigAttribute : Attribute
    {
        public TreeNodeConfigAttribute(string category, string? icon, Type view, Type ViewModel, int order = -1, string? header = null)
        {
            Category = category;
            Header = header;
            ViewType = view;
            ViewModelType = ViewModel;
            Icon = icon;
            Order = order;
        }

        public string? Header { get; set; }

        public string? Icon { get; set; }

        public Type ViewModelType { get; set; }

        public Type ViewType { get; set; }

        public string Category { get; set; }

        public int Order { get; set; }
    }
}
