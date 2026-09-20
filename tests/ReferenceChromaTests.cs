using System;
using System.Linq;
using TextureGrade.ColorGrade;
using TextureGrade.Models;

internal static partial class Program
{
    private static void ReferenceChromaTransfer()
    {
        var source = new[] { 0x4c4067, 0xa87cbb, 0xc7b6d1, 0xe1dee3 }
            .Select(rgb => new PaletteColor { Rgb = rgb, Weight = 1 }).ToArray();
        var reference = new[] { 0x274e5d, 0x7895a1, 0xb0c1c3, 0xdae2dc }
            .Select(rgb => new PaletteColor { Rgb = rgb, Weight = 1 }).ToArray();
        var state = new RecolorSettings { Mode = RecolorMode.Palette, Entries = PaletteAnalysis.Match(source, reference) };
        Check(state.Entries.All(e => e.TransferChroma), "Generated reference entries transfer chromatic residuals");
        var transform = new PaletteRecolor(state);
        foreach (var entry in state.Entries)
        {
            Check(Near(transform.Transform(entry.Source), entry.EffectiveTarget, 1e-9), "Reference chroma exact anchor");
            var neighbor = entry.Source; neighbor.A += 1e-8;
            Check(Near(transform.Transform(neighbor), entry.EffectiveTarget, 1e-6), "Reference chroma continuous at anchor");
        }
        // More saturated purples lie outside the extracted controls. A constant color offset
        // leaves magenta here even though all reference targets are cyan/green.
        foreach (int rgb in new[] { 0xe481f0, 0xcd88e0, 0xbd77ce, 0xa854c1, 0xdf80ec })
        {
            var input = OklabColor.FromRgb(rgb); var result = transform.Transform(input);
            Check(result.A < 0, "Saturated purple residual follows the cyan reference style");
            Check(Math.Abs(input.L - result.L) < 1e-6, "Reference residual transfer preserves pixel L");
        }
        var saved = Settings(state);
        var json = MiniJson.WriteObject(saved.Capture()); var restoredSettings = new GradeSettings();
        restoredSettings.Replace(MiniJson.ReadObject(json));
        var restored = RecolorSettings.Read(restoredSettings);
        Check(restored.Entries.All(e => e.TransferChroma), "Reference interpolation survives flat JSON without an image");
        var replay = new PaletteRecolor(restored);
        restored.Entries.Reverse(); var reversed = new PaletteRecolor(restored);
        var random = new Random(278);
        for (int i = 0; i < 150; i++)
        {
            var c = OklabColor.FromRgb(random.Next(0x1000000)); var result = transform.Transform(c);
            Check(Near(result, replay.Transform(c), 1e-12) && Near(result, reversed.Transform(c), 1e-12), "Reference preset/order reproducibility");
            Check(OklabColor.InGamut(result) && Math.Abs(c.L - result.L) < 1e-6, "Reference transfer finite, in gamut and lightness locked");
        }
        var pixels = Pixels(0xe481f0, 0xcd88e0, 0xbd77ce, 0xa854c1); pixels[11] = 128; pixels[15] = 0;
        var rendered = GradeRenderer.Render(pixels, 4, 1, saved, new[] { true, false, true, true }).Rgba;
        Check(Rgb(rendered, 0) != Rgb(pixels, 0) && Rgb(rendered, 2) != Rgb(pixels, 2), "Reference renderer transforms selected pixels");
        Check(Rgb(rendered, 1) == Rgb(pixels, 1) && Rgb(rendered, 3) == Rgb(pixels, 3) && rendered[11] == 128 && rendered[15] == 0,
            "Reference residual transfer respects mask and alpha");

        state.Entries = PaletteAnalysis.Match(source, source);
        transform = new PaletteRecolor(state);
        Check(GradeRenderer.Render(pixels, 4, 1, Settings(state)).Rgba.SequenceEqual(pixels), "Self reference remains bit-exact");
        Check(Near(transform.Transform(OklabColor.FromRgb(0xe481f0)), OklabColor.FromRgb(0xe481f0), 1e-12), "Self reference also preserves colors between anchors");

        state.Entries = PaletteAnalysis.Match(source, new[] { new PaletteColor { Rgb = 0x808080, Weight = 1 } });
        transform = new PaletteRecolor(state);
        foreach (int rgb in new[] { 0xe481f0, 0x000000, 0xffffff, 0x00ff00, 0x0000ff })
        {
            var c = OklabColor.FromRgb(rgb); var result = transform.Transform(c);
            Check(result.Chroma < 1e-6 && Math.Abs(c.L - result.L) < 1e-6, "Gray reference removes residual hue without changing L");
        }
        state.Entries = PaletteAnalysis.Match(new[] { new PaletteColor { Rgb = 0x808080 } }, new[] { new PaletteColor { Rgb = 0xff0080 } });
        transform = new PaletteRecolor(state);
        var gray = OklabColor.FromRgb(0x818181);
        Check(OklabColor.InGamut(transform.Transform(gray)), "Neutral source to saturated reference is stable");

        state.Entries = PaletteAnalysis.Match(source, reference, false);
        transform = new PaletteRecolor(state);
        foreach (var entry in state.Entries)
            Check(Near(transform.Transform(entry.Source), entry.EffectiveTarget, 1e-9), "Unlocked reference anchor uses target lightness");
        var manual = PaletteEntry.Identity(0xe481f0, true); state.Entries.Add(manual);
        Check(Near(new PaletteRecolor(state).Transform(manual.Source), manual.Source, 1e-9), "Manual unchanged anchor protects its exact color in a reference palette");
        state.ReplaceAutomatic(source.Select(e => PaletteEntry.Identity(e.Rgb)));
        Check(state.Entries.All(e => !e.TransferChroma), "Fresh detection retires reference behavior and keeps manual entry");
        var legacy = Settings(new RecolorSettings { Entries = PaletteAnalysis.Match(source, reference) });
        var values = legacy.Capture(); foreach (string key in values.Keys.Where(k => k.EndsWith(".TransferChroma")).ToArray()) values.Remove(key);
        legacy.Replace(values);
        Check(RecolorSettings.Read(legacy).Entries.All(e => !e.TransferChroma), "Existing presets retain their original interpolation until regenerated");
    }
}
