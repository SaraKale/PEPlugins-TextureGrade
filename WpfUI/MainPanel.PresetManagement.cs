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
        private PresetStore.PresetSort _presetSort = PresetStore.LoadSort();
        private Button _btnPresetSort;
        private void UpdatePresetSortButton()
        {
            if (_btnPresetSort == null) return;
            string mode;
            switch (_presetSort)
            {
                case PresetStore.PresetSort.Time: mode = L.T("Preset.SortTime"); break;
                case PresetStore.PresetSort.Custom: mode = L.T("Preset.SortCustom"); break;
                default: mode = L.T("Preset.SortName"); break;
            }
            _btnPresetSort.Content = L.F("Preset.SortFmt", mode);
        }

        private void CyclePresetSort()
        {
            var previous = _presetSort;
            _presetSort = _presetSort == PresetStore.PresetSort.Name
                ? PresetStore.PresetSort.Time
                : _presetSort == PresetStore.PresetSort.Time
                    ? PresetStore.PresetSort.Custom
                    : PresetStore.PresetSort.Name;
            try { PresetStore.SaveSort(_presetSort); }
            catch (Exception ex) { _presetSort = previous; Status(L.F("Preset.StorageFail", ex.Message)); return; }
            UpdatePresetSortButton();
            RefreshPresetList();
            Status(L.F("Preset.Sorted", _btnPresetSort == null ? "" : _btnPresetSort.Content));
        }

        private void RenameSelectedPreset()
        {
            var name = _presetList == null ? null : _presetList.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) { Status(L.T("Preset.SelectFirst")); return; }

            string input = InputDialog.Prompt(DlgOwner(), L.T("Preset.RenameTitle"), L.F("Preset.RenamePrompt", name), name);
            if (string.IsNullOrEmpty(input)) return;                 // 取消或空
            if (string.Equals(input, name, StringComparison.OrdinalIgnoreCase)) return;

            if (PresetStore.Exists(input))
            {
                System.Windows.Forms.MessageBox.Show(
                    L.F("Preset.RenameDup", input), L.T("Preset.RenameTitle"),
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            if (!PresetStore.Rename(name, input))
            {
                Status(L.F("Preset.RenameFail", name));
                return;
            }
            RefreshPresetList();
            if (_presetList != null) _presetList.SelectedItem = input;
            Status(L.F("Preset.Renamed", name, input));
        }

        private void MoveSelectedPreset(int delta)
        {
            var name = _presetList == null ? null : _presetList.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) { Status(L.T("Preset.SelectFirst")); return; }

            var names = PresetStore.List(_presetSort);
            int i = names.IndexOf(name);
            if (i < 0) return;
            int j = i + delta;
            if (j < 0 || j >= names.Count)
            {
                Status(delta < 0 ? L.T("Preset.AtTop") : L.T("Preset.AtBottom"));
                return;
            }

            names[i] = names[j];
            names[j] = name;
            try { PresetStore.SaveOrder(names); PresetStore.SaveSort(PresetStore.PresetSort.Custom); }
            catch (Exception ex) { Status(L.F("Preset.StorageFail", ex.Message)); return; }

            // 手动调过顺序后，排序方式必须切到「手动顺序」，不然看不出效果
            if (_presetSort != PresetStore.PresetSort.Custom)
            {
                _presetSort = PresetStore.PresetSort.Custom;
                UpdatePresetSortButton();
            }
            RefreshPresetList();
            if (_presetList != null) _presetList.SelectedItem = name;
            Status(L.F("Preset.Moved", name, j + 1, names.Count));
        }
    }
}
