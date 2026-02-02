using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WinPanel.Services
{
    public class DragAdorner : Adorner
    {
        private readonly Brush _visualBrush;
        private readonly Size _size;
        private readonly Point _centerOffset;

        public DragAdorner(UIElement adornedElement, UIElement dragVisual, double opacity, Point centerOffset, Size size)
            : base(adornedElement)
        {
            _visualBrush = new VisualBrush(dragVisual) { Opacity = opacity, Stretch = Stretch.None };
            _centerOffset = centerOffset;
            _size = size;
            IsHitTestVisible = false;
        }

        public double LeftOffset { get; set; }
        public double TopOffset { get; set; }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var location = new Point(LeftOffset - _centerOffset.X, TopOffset - _centerOffset.Y);
            drawingContext.DrawRectangle(_visualBrush, null, new Rect(location, _size));
        }

        public void UpdatePosition(Point point)
        {
            LeftOffset = point.X;
            TopOffset = point.Y;
            InvalidateVisual();
        }
    }
}
