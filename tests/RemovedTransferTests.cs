using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using TextureGrade.ColorGrade;
using TextureGrade.Localization;
using TextureGrade.Models;
using TextureGrade.WpfUI;

internal static partial class Program
{
    private static readonly string[] RetiredTransferFields =
        { "ReferenceAmount", "HasReference", "MeanL", "MeanA", "MeanB", "StdL", "StdA", "StdB" };

    private static Dictionary<string, double> LegacyTransferPreset()
    {
        var values = Settings(Palette()).Capture();
        values["Recolor.Mode"] = 2;
        foreach (string key in RetiredTransferFields) values["Recolor." + key] = 1;
        values["Recolor.ReferenceAmount"] = 100;
        return values;
    }

    private static bool HasRetiredTransferFields(Dictionary<string, double> values)
        => RetiredTransferFields.Any(key => values.ContainsKey("Recolor." + key));

    private static void RemovedTransferCompatibility()
    {
        var stored = new GradeSettings(); stored.Replace(LegacyTransferPreset());
        var state = RecolorSettings.Read(stored);
        Check(state.Mode == RecolorMode.Off && !state.HasEffect, "Removed transfer mode loads disabled");
        Check(state.Entries.Count == 2 && Near(state.Entries[0].Target, Palette().Entries[0].Target), "Legacy preset keeps inactive palette mappings");
        var pixels = Gradient(32, 32);
        Check(GradeRenderer.Render(pixels, 32, 32, stored).Rgba.SequenceEqual(pixels), "Legacy statistics cannot affect full render");
        Check(GradeRenderer.Render(pixels, 32, 32, stored, null, 16).Rgba.SequenceEqual(
            GradeRenderer.Render(pixels, 32, 32, new GradeSettings(), null, 16).Rgba), "Legacy statistics cannot affect temporary preview");
        var direct = (byte[])pixels.Clone(); new GradePipeline().Run(direct, 32, 32, stored);
        Check(direct.SequenceEqual(pixels), "Removed transfer is absent from direct pipeline");
        stored["Exposure"] = 1; var basic = new GradeSettings(); basic["Exposure"] = 1;
        Check(GradeRenderer.Render(pixels, 32, 32, stored).Rgba.SequenceEqual(GradeRenderer.Render(pixels, 32, 32, basic).Rgba),
            "Unrelated adjustments in a legacy transfer preset still work");
        state.Write(stored);
        Check(!HasRetiredTransferFields(stored.Capture()) && stored["Recolor.Mode"] == 0, "New preset omits retired transfer settings");
        var restored = new GradeSettings(); restored.Replace(MiniJson.ReadObject(MiniJson.WriteObject(stored.Capture())));
        Check(RecolorSettings.Read(restored).Mode == RecolorMode.Off && RecolorSettings.Read(restored).Entries.Count == 2, "Migrated preset round trip keeps palette disabled and editable");
        stored["Exposure"] = 0; state.Mode = RecolorMode.Palette; state.Write(stored);
        Check(GradeRenderer.Render(pixels, 32, 32, stored).Rgba.SequenceEqual(GradeRenderer.Render(pixels, 32, 32, Settings(Palette())).Rgba),
            "User can enable the retained palette after migration");
    }

    private static void RemovedTransferUi(MainPanel panel)
    {
        var settings = (GradeSettings)Get(panel, "Settings");
        var before = settings.Capture();
        Call(panel, "ApplyParams", LegacyTransferPreset());
        Check(settings["Recolor.Mode"] == 0 && !HasRetiredTransferFields(settings.Capture()), "Preset import removes legacy transfer data from UI snapshots");
        var root = (StackPanel)Get(panel, "_recolorRoot");
        Check(root.Children.OfType<WrapPanel>().Single().Children.OfType<RadioButton>().First().IsChecked == true, "Removed mode displays Off");
        var reference = root.Children.OfType<Expander>().Single(); reference.IsExpanded = true;
        var content = (StackPanel)reference.Content;
        Check(!content.Children.OfType<Expander>().Any(), "Reference palette section has no advanced transfer submenu");
        Check(content.Children.OfType<Button>().Single().Content.Equals(L.T("Rc.Generate")), "Reference section retains palette generation only");
        Check(root.Children.OfType<StackPanel>().All(p => p.IsEnabled), "Imported legacy mode never disables palette editing");
        panel.Undo(); Check(MiniJson.WriteObject(settings.Capture()) == MiniJson.WriteObject(before), "Undo legacy preset import restores previous palette");
        panel.Redo(); Check(settings["Recolor.Mode"] == 0 && !HasRetiredTransferFields(settings.Capture()), "Redo retains normalized preset");
        Call(panel, "ApplyParams", before);
        foreach (Lang lang in Enum.GetValues(typeof(Lang)))
        {
            var manual = DefaultManual.Get(lang);
            Check(!new[] { "statistics", "统计迁移", "統計遷移", "統計の直接転送" }.Any(term => manual.Contains(term)), "Removed feature absent from manual " + lang);
        }
    }
}
