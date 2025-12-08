using Astra.Core.Nodes.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Astra.UI.Services
{
    public interface IMinimapRenderService
    {
        void RenderElements(Canvas targetCanvas, IEnumerable<Node> elements, Rect minimapBounds, Brush selectionStroke);
    }

    public class DefaultMinimapRenderService : IMinimapRenderService
    {
     
        public void RenderElements(Canvas targetCanvas, IEnumerable<Node> elements, Rect minimapBounds, Brush selectionStroke)
        {
            if (targetCanvas == null) return;
            if (minimapBounds.Width <= 0 || minimapBounds.Height <= 0) return;

            double scaleX = targetCanvas.ActualWidth / minimapBounds.Width;
            double scaleY = targetCanvas.ActualHeight / minimapBounds.Height;

            foreach (var element in elements)
            {
                double x = (element.Position.X - minimapBounds.X) * scaleX;
                double y = (element.Position.Y - minimapBounds.Y) * scaleY;
                double w = Math.Max(1, element.Size.Width * scaleX);
                double h = Math.Max(1, element.Size.Height * scaleY);

                
            }
        }
    }
}
