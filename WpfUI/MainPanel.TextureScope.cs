using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TextureGrade.Localization;

namespace TextureGrade.WpfUI
{
    public partial class MainPanel
    {
        private const string TextureScopeKey = "Scope.WholeTexture";
        private bool _syncTextureScope, _lastWholeTextureScope;
        private CancellationTokenSource _sharedUvCancel;
        private int _sharedUvVersion;
        private StreamGeometry _sharedUvGeometry;
        private bool WholeTextureScope => Settings[TextureScopeKey] > .5;

        private void SetWholeTextureScope(bool whole)
        {
            if (_syncTextureScope || _currentMatIndex < 0 || _originalBytes == null || WholeTextureScope == whole) return;
            var before = Snapshot(); Settings[TextureScopeKey] = whole ? 1 : 0;
            CommitRecolor(before);
            if (whole) FitView();
            RunGradeAsync();
        }

        private void SyncTextureScopeUi()
        {
            if (ScopeWhole == null) return;
            bool whole = WholeTextureScope;
            if (_lastWholeTextureScope != whole)
            {
                _lastWholeTextureScope = whole;
                InvalidateSelectionGeometry(); CancelAnalysis();
                DrawUV(_currentMatIndex);
            }
            _syncTextureScope = true;
            try
            {
                TxtScopeTitle.Text = L.T("Scope.Title");
                ScopeSelection.Content = L.T("Scope.Selection"); ScopeWhole.Content = L.T("Scope.Whole");
                ScopeSelection.IsChecked = !whole; ScopeWhole.IsChecked = whole;
                ScopeSelection.IsEnabled = ScopeWhole.IsEnabled = _originalBytes != null;
            }
            finally { _syncTextureScope = false; }
            UpdateTextureScopeNotice();
            DrawSharedTextureContext();
        }

        private bool SharesCurrentTexture(string path)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(_currentAbsOriginal)) return false;
            return string.Equals(System.IO.Path.GetFullPath(path), System.IO.Path.GetFullPath(_currentAbsOriginal), StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateTextureScopeNotice()
        {
            if (ScopeNotice == null) return;
            ScopeSelection.IsEnabled = ScopeWhole.IsEnabled = _originalBytes != null;
            ScopeNotice.Visibility = _originalBytes == null ? Visibility.Collapsed : Visibility.Visible;
            bool full = WholeTextureScope || _selectedTris.Count == 0;
            ScopeNotice.Background = full ? new SolidColorBrush(Color.FromRgb(255, 248, 225)) : new SolidColorBrush(Color.FromRgb(244, 246, 248));
            int shared = _rows.Count(r => SharesCurrentTexture(r.Info.TexAbsPath));
            TxtScopeNotice.Text = full ? L.F(WholeTextureScope ? "Scope.WholeNote" : "Scope.NoSelectionNote", shared) : L.F("Scope.SelectionNote", _selectedTris.Count);
        }

        private void CancelSharedTextureContext()
        {
            _sharedUvVersion++; _sharedUvCancel?.Cancel(); _sharedUvCancel = null; _sharedUvGeometry = null;
            if (SharedTexturePath != null) SharedTexturePath.Data = null;
        }

        private void DrawSharedTextureContext()
        {
            if (TextureContextCanvas == null) return;
            bool visible = WholeTextureScope && _showUV && _originalBytes != null && _w > 0 && _h > 0;
            TextureContextCanvas.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible) return;
            SharedTexturePath.StrokeThickness = Math.Max(.4, 1 / Math.Max(_zoom, 1e-6));
            if (_sharedUvGeometry != null) SharedTexturePath.Data = _sharedUvGeometry;
            else if (_sharedUvCancel == null) BuildSharedTextureContext();
        }

        private async void BuildSharedTextureContext()
        {
            var cancel = new CancellationTokenSource(); _sharedUvCancel = cancel;
            int version = _sharedUvVersion, material = _currentMatIndex, w = _w, h = _h;
            try
            {
                // Bridge access stays on the UI thread; only the immutable geometry snapshot goes to the worker.
                var triangles = _rows.Where(r => r.Info.Index != material && SharesCurrentTexture(r.Info.TexAbsPath))
                    .SelectMany(r => _bridge.GetUVTriangles(r.Info.Index)).Select(t => new UvTri(t.u1, t.v1, t.u2, t.v2, t.u3, t.v3)).ToArray();
                var geometry = await Task.Run(() =>
                {
                    var result = new StreamGeometry();
                    using (var context = result.Open())
                        foreach (var t in triangles)
                        {
                            cancel.Token.ThrowIfCancellationRequested();
                            context.BeginFigure(new Point(t.u1 * w, t.v1 * h), false, true);
                            context.LineTo(new Point(t.u2 * w, t.v2 * h), true, false);
                            context.LineTo(new Point(t.u3 * w, t.v3 * h), true, false);
                        }
                    result.Freeze(); return result;
                }, cancel.Token);
                if (version != _sharedUvVersion || material != _currentMatIndex) return;
                _sharedUvGeometry = geometry; SharedTexturePath.Data = geometry;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (version == _sharedUvVersion) Status(L.F("Scope.ContextFailed", ex.Message)); }
            finally { if (ReferenceEquals(_sharedUvCancel, cancel)) _sharedUvCancel = null; cancel.Dispose(); }
        }
    }
}
