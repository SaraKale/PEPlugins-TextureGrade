using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using TextureGrade.Localization;

namespace TextureGrade.WpfUI
{
    public partial class MainPanel
    {
        private sealed class MaterialSelection
        {
            public UvTri[] Geometry;
            public int[] Triangles, Vertices;
            public int LastPicked;
        }
        private readonly Dictionary<int, MaterialSelection> _materialSelections = new Dictionary<int, MaterialSelection>();

        private void SaveCurrentSelection()
        {
            if (_currentMatIndex < 0) return;
            if (_selectedTris.Count == 0 && _recvVerts.Count == 0) { _materialSelections.Remove(_currentMatIndex); return; }
            _materialSelections[_currentMatIndex] = new MaterialSelection
            {
                Geometry = _tris.ToArray(), Triangles = _selectedTris.ToArray(), Vertices = _recvVerts.ToArray(), LastPicked = _lastPickedTri
            };
        }
        private bool RestoreSelection(int material)
        {
            if (!_materialSelections.TryGetValue(material, out var state)) return false;
            // Equal counts alone do not establish that face indices still mean the same thing.
            if (state.Geometry.Length != _tris.Count || !state.Geometry.SequenceEqual(_tris))
            { _materialSelections.Remove(material); return false; }
            _selectedTris.UnionWith(state.Triangles.Where(i => i >= 0 && i < _tris.Count));
            _recvVerts.UnionWith(state.Vertices.Where(i => _vertUv.ContainsKey(i)));
            _lastPickedTri = state.LastPicked >= 0 && state.LastPicked < _tris.Count ? state.LastPicked : -1;
            return true;
        }
        public void InvertUVSelection()
        {
            if (_tris.Count == 0) { Status(L.T("St.NoTris")); return; }
            for (int i = 0; i < _tris.Count; i++) if (!_selectedTris.Remove(i)) _selectedTris.Add(i);
            _lastPickedTri = _selectedTris.Count > 0 ? _selectedTris.Min() : -1;
            AfterSelectionChanged(_selectedTris.Count > 0 ? L.F("St.InvertedFmt", _selectedTris.Count, _tris.Count) : L.T("St.InvertedEmpty"));
        }
        private void SelectionShortcut(object sender, KeyEventArgs e)
        {
            if (_colorDialogOpen || _pickPaletteIndex >= 0 || Keyboard.FocusedElement is TextBoxBase ||
                e.OriginalSource is TextBoxBase || Keyboard.Modifiers != ModifierKeys.Control) return;
            if (e.Key == Key.I) { InvertUVSelection(); e.Handled = true; }
            else if (e.Key == Key.A) { SelectAllUv(); e.Handled = true; }
        }
    }
}
