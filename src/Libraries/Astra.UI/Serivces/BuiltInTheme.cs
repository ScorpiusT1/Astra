using Astra.UI.Abstractions.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Serivces
{
    /// <summary>
    /// 内置主题实现
    /// </summary>
    internal class BuiltInTheme : ITheme
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Uri ResourceUri { get; }
        public string Icon { get; }
        public bool IsBuiltIn => true;

        public BuiltInTheme(string id, string displayName, string description, string resourcePath, string icon = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Description = description ?? string.Empty;
            ResourceUri = new Uri(resourcePath, UriKind.Relative);
            Icon = icon ?? string.Empty;
        }

        public override string ToString() => DisplayName;
    }
}
