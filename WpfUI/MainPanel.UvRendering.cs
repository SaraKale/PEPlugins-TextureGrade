using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TextureGrade.WpfUI
{
    public partial class MainPanel
    {
        // Four retained drawing layers, independent of the number of selected faces.
        private Path _uvLines, _uvSelected, _uvDots, _uvMarked;
        private StreamGeometry _geoAllCache, _geoSelectedCache, _geoDotsCache, _geoMarkedCache;
        private Point[] _allDotPositions, _markedDotPositions;
        private double _geoDotsZoom, _geoMarkedZoom;
        private bool _geoDirty = true;
        private int _selectionVersion;

        private void InvalidateGeo()
        {
            _allDotPositions = null; _geoDotsCache = null; _geoDotsZoom = 0;
            InvalidateSelectionGeometry();
        }

        private void InvalidateSelectionGeometry()
        {
            _selectionVersion++; _maskCache = null; _geoDirty = true;
            InvalidateMarkedGeometry();
        }

        private void InvalidateMarkedGeometry()
        {
            _markedDotPositions = null; _geoMarkedCache = null; _geoMarkedZoom = 0;
        }

        private void DrawUV(int matIndex)
        {
            if (UvCanvas == null) return;
            DrawGrid();
            DrawSharedTextureContext();
            UvCanvas.Visibility = _showUV && matIndex >= 0 && _w > 0 && _h > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (UvCanvas.Visibility != Visibility.Visible) return;
            if (_uvLines == null)
            {
                _uvLines = new Path();
                _uvSelected = new Path { Stroke = FrozenBrush(Color.FromArgb(240, 0, 168, 60)) };
                _uvDots = new Path();
                _uvMarked = new Path { Fill = FrozenBrush(Color.FromArgb(235, 255, 42, 42)), Stroke = FrozenBrush(Color.FromArgb(255, 110, 0, 0)) };
            }
            if (UvCanvas.Children.Count == 0)
            {
                UvCanvas.Children.Add(_uvLines); UvCanvas.Children.Add(_uvSelected);
                UvCanvas.Children.Add(_uvDots); UvCanvas.Children.Add(_uvMarked);
            }
            if (_geoDirty)
            {
                var normal = new StreamGeometry(); var selected = new StreamGeometry();
                using (var normalContext = normal.Open())
                using (var selectedContext = selected.Open())
                    for (int i = 0; i < _tris.Count; i++) AppendTriangle(_selectedTris.Contains(i) ? selectedContext : normalContext, _tris[i]);
                normal.Freeze(); selected.Freeze();
                _geoAllCache = normal; _geoSelectedCache = selected; _geoDirty = false;
            }

            double zoom = Math.Max(_zoom, 1e-6), thickness = Math.Max(.4, 1.15 / zoom);
            _uvLines.Data = _geoAllCache; _uvLines.Stroke = FrozenBrush(_uvLine); _uvLines.StrokeThickness = thickness;
            _uvSelected.Data = _geoSelectedCache; _uvSelected.StrokeThickness = thickness;
            _uvDots.Visibility = _showVertices && _tris.Count <= DotTriangleLimit ? Visibility.Visible : Visibility.Collapsed;
            if (_uvDots.Visibility == Visibility.Visible)
            {
                if (_allDotPositions == null)
                {
                    var positions = new HashSet<Point>();
                    foreach (var tri in _tris) AddPositions(positions, tri);
                    _allDotPositions = new Point[positions.Count]; positions.CopyTo(_allDotPositions);
                }
                if (NeedMarkerGeometry(_geoDotsCache, _geoDotsZoom))
                {
                    _geoDotsCache = BuildDotGeometry(_allDotPositions, 2.4 / zoom);
                    _geoDotsZoom = zoom;
                }
                _uvDots.Data = _geoDotsCache; _uvDots.Fill = FrozenBrush(_uvVertex);
            }
            // Selection markers stay visible when ordinary vertex dots are hidden, as before.
            if (_markedDotPositions == null)
            {
                var positions = new HashSet<Point>();
                foreach (int i in _selectedTris) if (i >= 0 && i < _tris.Count) AddPositions(positions, _tris[i]);
                foreach (int i in _recvVerts) if (_vertUv.TryGetValue(i, out var uv)) positions.Add(new Point(uv.u, uv.v));
                _markedDotPositions = new Point[positions.Count]; positions.CopyTo(_markedDotPositions);
            }
            bool denseMarkers = _markedDotPositions.Length > 4000;
            if (NeedMarkerGeometry(_geoMarkedCache, _geoMarkedZoom))
            {
                _geoMarkedCache = BuildDotGeometry(_markedDotPositions, (denseMarkers ? 2.5 : 6) / zoom);
                _geoMarkedZoom = zoom;
            }
            // Dense outlines cover the texture and make WPF tessellate thousands of overlapping strokes.
            _uvMarked.Stroke = denseMarkers ? null : FrozenBrush(Color.FromArgb(255, 110, 0, 0));
            _uvMarked.Data = _geoMarkedCache; _uvMarked.StrokeThickness = .8 / zoom;
        }

        private bool NeedMarkerGeometry(StreamGeometry geometry, double zoom)
            => geometry == null || zoom <= 0 || Math.Abs(Math.Log(_zoom / zoom)) > .22;

        private static SolidColorBrush FrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color); brush.Freeze(); return brush;
        }

        private static void AddPositions(HashSet<Point> positions, UvTri tri)
        {
            positions.Add(new Point(tri.u1, tri.v1)); positions.Add(new Point(tri.u2, tri.v2)); positions.Add(new Point(tri.u3, tri.v3));
        }

        private StreamGeometry BuildDotGeometry(Point[] positions, double radius)
        {
            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
            // Coalesce marks in the same two-pixel display cell. Selection and sent vertex IDs remain complete.
            var cells = new HashSet<(long x, long y)>();
            using (var context = geometry.Open())
                foreach (var point in positions)
                {
                    var cell = ((long)Math.Floor(point.X * _w * _zoom / 2), (long)Math.Floor(point.Y * _h * _zoom / 2));
                    if (cells.Add(cell)) Dot(context, point.X, point.Y, radius);
                }
            geometry.Freeze(); return geometry;
        }
    }
}
