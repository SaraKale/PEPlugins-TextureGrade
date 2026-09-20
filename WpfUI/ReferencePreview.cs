using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TextureGrade.WpfUI
{
    public sealed class ReferencePreview : FrameworkElement
    {
        public ImageSource Image;
        public int PixelWidth, PixelHeight;
        public Int32Rect? Crop;
        public event Action<Int32Rect?> CropChanged;
        private Point start;
        private bool dragging;
        private Rect ImageRect
        {
            get
            {
                double scale = Math.Min(ActualWidth / Math.Max(1, PixelWidth), ActualHeight / Math.Max(1, PixelHeight));
                double w = PixelWidth * scale, h = PixelHeight * scale;
                return new Rect((ActualWidth - w) / 2, (ActualHeight - h) / 2, w, h);
            }
        }
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.LightGray, null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (Image == null) return;
            var rect = ImageRect; dc.DrawImage(Image, rect);
            if (Crop.HasValue)
            {
                var c = Crop.Value;
                var selection = new Rect(rect.X + rect.Width * c.X / PixelWidth, rect.Y + rect.Height * c.Y / PixelHeight,
                    rect.Width * c.Width / PixelWidth, rect.Height * c.Height / PixelHeight);
                dc.DrawRectangle(null, new Pen(Brushes.Black, 3), selection);
                dc.DrawRectangle(null, new Pen(Brushes.White, 1), selection);
            }
        }
        private Point Pixel(Point p)
        {
            var rect = ImageRect;
            return new Point(Math.Max(0, Math.Min(PixelWidth - 1, (p.X - rect.X) / rect.Width * PixelWidth)),
                Math.Max(0, Math.Min(PixelHeight - 1, (p.Y - rect.Y) / rect.Height * PixelHeight)));
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (Image == null || !ImageRect.Contains(e.GetPosition(this))) return;
            start = Pixel(e.GetPosition(this)); dragging = true; CaptureMouse(); e.Handled = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!dragging) return;
            var end = Pixel(e.GetPosition(this));
            int x = (int)Math.Min(start.X, end.X), y = (int)Math.Min(start.Y, end.Y);
            Crop = new Int32Rect(x, y, (int)Math.Max(start.X, end.X) - x + 1, (int)Math.Max(start.Y, end.Y) - y + 1);
            InvalidateVisual();
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!dragging) return;
            dragging = false; ReleaseMouseCapture(); CropChanged?.Invoke(Crop); e.Handled = true;
        }
    }
}
