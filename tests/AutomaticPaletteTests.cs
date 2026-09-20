using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using TextureGrade.ColorGrade;
using TextureGrade.Models;

internal static partial class Program
{
    private static void AutomaticPalette()
    {
        var blocks = Pixels(Enumerable.Range(0, 1200).Select(i => new[] { 0xee3030, 0x30dd50, 0x3040ee }[i / 400]).ToArray());
        var samples = PaletteAnalysis.Sample(blocks);
        var automatic = PaletteAnalysis.ExtractAutomatic(samples);
        Check(automatic.Count == 3 && automatic.Select(c => c.Rgb).OrderBy(c => c).SequenceEqual(samples.Select(c => c.Rgb).OrderBy(c => c)), "Automatic count finds three distinct populated blocks");
        Check(automatic.Select(c => c.Rgb).SequenceEqual(PaletteAnalysis.ExtractAutomatic(samples).Select(c => c.Rgb)), "Automatic count deterministic");
        Check(PaletteAnalysis.ExtractAutomatic(PaletteAnalysis.Sample(blocks, Enumerable.Range(0, 1200).Select(i => i == 30).ToArray())).Count == 1, "Auto count honors a single selected pixel");
        Check(PaletteAnalysis.ExtractAutomatic(PaletteAnalysis.Sample(new byte[32])).Count == 0, "Auto count ignores all-transparent pixels");
        var noisy = Pixels(Enumerable.Range(0, 1000).Select(i => 0x808080 + i % 3).ToArray());
        Check(PaletteAnalysis.ExtractAutomatic(PaletteAnalysis.Sample(noisy)).Count == 1, "Subtle gradients and noise do not inflate auto count");
        var speckled = Pixels(Enumerable.Repeat(0x808080, 30000).Concat(new[] { 0xff0000 }).ToArray());
        Check(PaletteAnalysis.ExtractAutomatic(PaletteAnalysis.Sample(speckled)).Count == 1, "Isolated speck does not create a major block");
        var weighted = Pixels(0xff0000, 0x00ff00); weighted[7] = 0;
        Check(PaletteAnalysis.ExtractAutomatic(PaletteAnalysis.Sample(weighted)).Single().Rgb == 0xff0000, "Automatic count uses visible alpha");
        var cancel = new CancellationTokenSource(); cancel.Cancel(); bool canceled = false;
        try { PaletteAnalysis.ExtractAutomatic(samples, cancel.Token); } catch (OperationCanceledException) { canceled = true; }
        Check(canceled, "Automatic extraction can be canceled");
        var watch = Stopwatch.StartNew(); var richSamples = PaletteAnalysis.Sample(Gradient(192, 192));
        var rich = PaletteAnalysis.ExtractAutomatic(richSamples);
        Console.WriteLine($"Automatic palette: {rich.Count} colors / {richSamples.Count} samples in {watch.ElapsedMilliseconds} ms");
        Check(rich.Count > 1 && rich.Count <= RecolorSettings.MaxPaletteColors && rich.All(c => richSamples.Any(s => s.Rgb == c.Rgb)), "Automatic count is bounded and uses actual colors");

        PaletteColor Gray(double l, double weight = 1) => new PaletteColor { Rgb = OklabColor.ToRgb(new Oklab(l, 0, 0)), Weight = weight };
        var src = new[] { Gray(.1), Gray(.8), Gray(.82), Gray(.9) };
        var dst = new[] { Gray(.1), Gray(.5), Gray(.9) };
        var matched = PaletteAnalysis.Match(src, dst, false);
        var targets = matched.Select(e => OklabColor.ToRgb(e.Target)).ToArray();
        Check(targets[2] == dst[2].Rgb && targets.Distinct().Count() == 3, "Unequal palette alignment respects actual lightness and covers every reference block");
        var many = Enumerable.Range(0, 11).Select(i => Gray(.1 + i * .08, i == 5 ? 10 : 1)).ToArray();
        matched = PaletteAnalysis.Match(new[] { Gray(.1), Gray(.5), Gray(.9) }, many, false);
        Check(OklabColor.ToRgb(matched[1].Target) == many[5].Rgb, "Fewer source controls retain dominant middle reference block");
        foreach (int n in new[] { 1, 2, 4, 9, 32, 128 }) foreach (int m in new[] { 1, 3, 8, 64, 128 })
        {
            src = Enumerable.Range(0, n).Select(i => Gray(.1 + .8 * i / Math.Max(1, n - 1), i + 1)).ToArray();
            dst = Enumerable.Range(0, m).Select(i => Gray(.1 + .8 * i / Math.Max(1, m - 1), m - i)).ToArray();
            matched = PaletteAnalysis.Match(src, dst, false);
            Check(matched.Count == n && matched.All(e => OklabColor.InGamut(e.Target)), "Alignment covers all source rows for every count ratio");
            Check(matched.Zip(matched.Skip(1), (a, b) => a.Target.L <= b.Target.L + 1e-9).All(v => v), "Target lightness order never reverses");
            if (n >= m) Check(matched.Select(e => OklabColor.ToRgb(e.Target)).Distinct().Count() == dst.Select(c => c.Rgb).Distinct().Count(), "All reference controls represented when source count permits");
        }
        var settings = Settings(new RecolorSettings { AutoDetectCount = true, AutoReferenceCount = true, DetectCount = 10, ReferenceCount = 5 });
        var restored = RecolorSettings.Read(settings.Copy());
        Check(restored.AutoDetectCount && restored.AutoReferenceCount && restored.DetectCount == 10 && restored.ReferenceCount == 5, "Auto flags and computed counts survive snapshots");
        Check(!RecolorSettings.Read(new GradeSettings()).AutoDetectCount && !RecolorSettings.Read(new GradeSettings()).AutoReferenceCount, "Legacy presets retain manual count behavior");
    }
}
