using System;
using System.Diagnostics;
using System.Linq;
using TextureGrade.ColorGrade;
using TextureGrade.Models;

internal static partial class Program
{
    private static void LargeReferencePalette()
    {
        var samples = PaletteAnalysis.Sample(Gradient(96, 96));
        var watch = Stopwatch.StartNew();
        var source = PaletteAnalysis.Extract(samples, 128);
        var reference = PaletteAnalysis.Extract(PaletteAnalysis.Sample(Gradient(72, 72)), 96);
        Console.WriteLine("128 source / 96 reference colors: " + watch.ElapsedMilliseconds + " ms");
        Check(source.Count == 128 && reference.Count == 96, "Both extracted palettes exceed the former 32-color limit");
        Check(source.Select(c => c.Rgb).SequenceEqual(PaletteAnalysis.Extract(samples, 128).Select(c => c.Rgb)), "Large palette extraction remains deterministic");
        var state = new RecolorSettings { Mode = RecolorMode.Palette, DetectCount = 128, ReferenceCount = 96,
            Entries = PaletteAnalysis.Match(source, reference) };
        Check(state.Entries.Count == 128, "Reference transfer generates all 128 mappings");
        var restored = RecolorSettings.Read(Settings(state));
        Check(restored.DetectCount == 128 && restored.ReferenceCount == 96 && restored.Entries.Count == 128, "Large palette counts and rows survive snapshots");
        var transform = new PaletteRecolor(restored);
        foreach (var entry in restored.Entries)
            Check(Near(transform.Transform(entry.Source), entry.EffectiveTarget), "Large palette retains exact source anchors");
        var random = new Random(128);
        for (int i = 0; i < 64; i++)
        {
            var input = OklabColor.FromRgb(random.Next(0x1000000)); var result = transform.Transform(input);
            Check(OklabColor.InGamut(result) && Math.Abs(input.L - result.L) < 1e-6, "Large palette stays finite and preserves locked lightness");
        }
        Check(PaletteAnalysis.Extract(PaletteAnalysis.Sample(Pixels(0xff0000)), 128).Count == 1, "Large requested count still honors actual color availability");
        var limits = Settings(state); limits["Recolor.Count"] = 999; limits["Recolor.ReferenceCount"] = 999;
        Check(RecolorSettings.Read(limits).DetectCount == 128 && RecolorSettings.Read(limits).ReferenceCount == 128, "Preset input enforces the new maximum consistently");
    }
}
