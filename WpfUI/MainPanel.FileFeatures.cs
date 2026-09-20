using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TextureGrade.Models;
using TextureGrade.Localization;
using TextureGrade.TextureIO;

namespace TextureGrade.WpfUI
{
    public partial class MainPanel
    {
        private TextureFormat _lastSaveFormat = TextureFormat.Png;
        private int _lastJpegQuality = 92;
        private System.Windows.Forms.IWin32Window DlgOwner() => Win32Owner.From(this);

        public void SaveNewAs()
        {
            if (_originalBytes == null || _currentMatIndex < 0 || _currentAbsOriginal == null)
            { Status(L.T("St.NeedTexturedMaterial")); return; }

            string def = TextureNaming.NewSavePath(_currentAbsOriginal,
                TextureIO.TextureWriter.Extension(_lastSaveFormat));

            string path;
            TextureIO.TextureFormat fmt;
            int tw, th, quality;
            using (var dlg = new SaveTextureDialog(def, _w, _h, _lastSaveFormat, _lastJpegQuality))
            {
                if (dlg.ShowDialog(DlgOwner()) != System.Windows.Forms.DialogResult.OK) return;
                path = dlg.TargetPath;
                fmt = dlg.Format;
                tw = dlg.TargetWidth;
                th = dlg.TargetHeight;
                quality = dlg.JpegQuality;
            }

            // 记住这次的选择，下次打开对话框时沿用
            _lastSaveFormat = fmt;
            _lastJpegQuality = quality;

            try
            {
                SaveGradedTexture(path, fmt, tw, th, quality);
            }
            catch (Exception ex)
            {
                Status(L.F("St.SaveAsFail", ex.Message));
                System.Windows.Forms.MessageBox.Show(DlgOwner(), L.F("St.SaveAsFail", ex.Message), L.T("SaveDlg.Title"),
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            Status(L.F("St.SavedAsFmt", System.IO.Path.GetFileName(path), tw, th));
        }

        // Shared by the format dialog, quick PNG save and the integration tests.
        private void SaveGradedTexture(string path, TextureFormat format, int width, int height, int quality = 92)
        {
            path = System.IO.Path.GetFullPath(path);
            if (string.Equals(path, System.IO.Path.GetFullPath(_currentAbsOriginal), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(L.T("SaveDlg.ProtectSource"));
            var pixels = TextureWriter.Resample(ComputeGraded(), _w, _h, width, height);
            TextureWriter.Save(path, pixels, width, height, format, quality);
            _bridge.ApplySaved(_currentMatIndex, path);
            _pushedMats.Add(_currentMatIndex);
            MarkCurrentModified();
            RefreshCurrentThumbnail(path);
        }

        public void ExportSelectionAlpha()
        {
            if (_currentMatIndex < 0) { Status(L.T("St.NeedMaterial")); return; }
            if (_selectedTris.Count == 0)
            {
                Status(L.T("St.NoSelection"));
                System.Windows.Forms.MessageBox.Show(
                    L.T("Dlg.NoSelMaskBody"), L.T("Dlg.MaskTitle"),
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                return;
            }

            var info = FindMaterialInfo(_currentMatIndex);
            int w, h;
            if (!ResolveMaskSize(info, out w, out h)) return;

            var options = _originalBytes == null ? new[] { L.T("AlphaDlg.White") } :
                new[] { L.T("AlphaDlg.White"), L.T("AlphaDlg.Original"), L.T("AlphaDlg.Graded") };
            int pick = ChoiceDialog.Choose(DlgOwner(), L.T("AlphaDlg.Title"),
                L.F("AlphaDlg.Desc", _selectedTris.Count), options, 0);
            if (pick < 0) return;

            string fileName = Sanitize(info != null ? info.Name : "material") + "_alpha.png";
            string path;
            using (var dlg = new System.Windows.Forms.SaveFileDialog())
            {
                dlg.Title = L.T("AlphaDlg.Title");
                dlg.Filter = L.T("Dlg.FilterPng");
                dlg.FileName = fileName;
                dlg.InitialDirectory = _bridge.PmxDirectory ?? "";
                if (dlg.ShowDialog(DlgOwner()) != System.Windows.Forms.DialogResult.OK) return;
                path = dlg.FileName;
            }

            try
            {
                SaveSelectionAlpha(path, w, h, pick);

                Status(L.F("St.AlphaExportedFmt", System.IO.Path.GetFileName(path), w, h, _selectedTris.Count));
                System.Windows.Forms.MessageBox.Show(
                    L.F("Dlg.AlphaBodyFmt", path, w, h, _selectedTris.Count, options[pick]),
                    L.T("Dlg.MaskTitle"), System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Status(L.F("St.MaskFail", ex.Message));
                System.Windows.Forms.MessageBox.Show(L.F("St.MaskFail", ex.Message), L.T("Dlg.MaskTitle"),
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private void SaveSelectionAlpha(string path, int width, int height, int rgbMode)
        {
            if (!string.IsNullOrEmpty(_currentAbsOriginal) && string.Equals(System.IO.Path.GetFullPath(path),
                System.IO.Path.GetFullPath(_currentAbsOriginal), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(L.T("SaveDlg.ProtectSource"));
            // Always rasterize the explicit UV selection, even while Whole Texture grading is on.
            var mask = new bool[checked(width * height)];
            foreach (int i in _selectedTris) if (i >= 0 && i < _tris.Count) RasterizeTri(mask, width, height, _tris[i]);
            byte[] rgb = rgbMode == 1 ? _originalBytes : rgbMode == 2 ? ComputeGraded() : null;
            MaskWriter.SaveAlphaPng(path, mask, width, height, SameSize(rgb, _w, _h, width, height));
        }

        private static byte[] SameSize(byte[] rgba, int w, int h, int targetW, int targetH)
        {
            if (rgba == null) return null;
            if (w == targetW && h == targetH) return rgba;
            return TextureIO.TextureWriter.Resample(rgba, w, h, targetW, targetH);
        }
    }
}
