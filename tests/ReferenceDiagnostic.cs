using System;
using System.IO;
using System.Linq;
using System.Threading;
using TextureGrade.ColorGrade;
using TextureGrade.Models;
using TextureGrade.TextureIO;

internal static partial class Program
{
    // Opt-in reproduction with external images; normal regression tests need no user assets.
    private static void ReferenceDiagnostic(string sourcePath, string observedPath, string referencePath)
    {
        var source = TextureLoader.Load(sourcePath); var observed = TextureLoader.Load(observedPath);
        var reference = TextureLoader.Load(referencePath);
        Check(source.width == observed.width && source.height == observed.height, "Diagnostic sizes match");
        var samples = PaletteAnalysis.Sample(source.rgba); var refs = PaletteAnalysis.Sample(reference.rgba);
        Console.WriteLine($"Source {source.width}x{source.height}, {samples.Count} sampled RGBs; reference {refs.Count}");
        Console.WriteLine($"Purple source={PurpleFraction(source.rgba):P3} observed={PurpleFraction(observed.rgba):P3} reference={PurpleFraction(reference.rgba):P3}");
        foreach (int count in new[] { 0, 4, 8, 16, 32, 64, 128 })
        {
            var src = count == 0 ? PaletteAnalysis.ExtractAutomatic(samples) : PaletteAnalysis.Extract(samples, count);
            var dst = count == 0 ? PaletteAnalysis.ExtractAutomatic(refs) : PaletteAnalysis.Extract(refs, count);
            foreach (bool locked in new[] { true, false })
            {
                var settings = new RecolorSettings { Mode = RecolorMode.Palette, Entries = PaletteAnalysis.Match(src, dst, locked) };
                var transform = new PaletteRecolor(settings); var bytes = (byte[])source.rgba.Clone();
                transform.Apply(bytes, null, CancellationToken.None);
                long error = 0, equal = 0;
                for (int p = 0; p < bytes.Length; p += 4)
                {
                    if (Rgb(bytes, p / 4) == Rgb(observed.rgba, p / 4)) equal++;
                    for (int c = 0; c < 3; c++) error += Math.Abs(bytes[p + c] - observed.rgba[p + c]);
                }
                string name = $"reference-{(count == 0 ? "auto" : count.ToString())}-{(locked ? "locked" : "unlocked")}";
                if (count == 0) Console.WriteLine($"Automatic count: {src.Count} source / {dst.Count} reference");
                Console.WriteLine($"{name}: fallback={transform.UsesFallback}, purple={PurpleFraction(bytes):P3}, MAE={error / (bytes.Length / 4.0 * 3):F3}, identical={equal / (bytes.Length / 4.0):P3}");
                TextureLoader.SavePng(Path.Combine(output, name + ".png"), bytes, source.width, source.height);
                File.WriteAllLines(Path.Combine(output, name + ".csv"), new[] { "source,target,L,a,b" }.Concat(settings.Entries.Select(e =>
                    $"{e.SourceRgb:X6},{OklabColor.ToRgb(e.EffectiveTarget):X6},{e.EffectiveTarget.L:R},{e.EffectiveTarget.A:R},{e.EffectiveTarget.B:R}")));
            }
        }
    }
    private static double PurpleFraction(byte[] pixels)
    {
        int purple = 0, visible = 0;
        for (int p = 0; p < pixels.Length / 4; p++)
        {
            if (pixels[p * 4 + 3] == 0) continue;
            visible++;
            var c = OklabColor.FromRgb(Rgb(pixels, p));
            if (c.A > .015 && c.B < -.015) purple++;
        }
        return purple / (double)Math.Max(1, visible);
    }
}
