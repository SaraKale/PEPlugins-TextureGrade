using System;
using System.Linq;
using TextureGrade.ColorGrade;
using TextureGrade.Models;

internal static partial class Program
{
    private static void GlobalPaletteCoverage()
    {
        // Partition of unity: equal control displacements must affect every input color equally,
        // including colors much farther from the controls than the former .15 radius.
        var state = new RecolorSettings { Mode = RecolorMode.Palette };
        const double da = .008, db = -.004;
        foreach (int rgb in new[] { 0x445566, 0x887766, 0x557755 })
        {
            var entry = PaletteEntry.Identity(rgb);
            entry.Target = new Oklab(entry.Source.L, entry.Source.A + da, entry.Source.B + db);
            Check(OklabColor.InGamut(entry.Target), "Global coverage fixture target is in gamut");
            state.Entries.Add(entry);
        }
        var transform = new PaletteRecolor(state);
        var random = new Random(9301);
        for (int i = 0; i < 400; i++)
        {
            var c = OklabColor.FromRgb(random.Next(0x1000000));
            var expected = OklabColor.FitGamut(new Oklab(c.L, c.A + da, c.B + db));
            var result = transform.Transform(c);
            Check(Near(result, expected, 1e-9), "All colors share a uniform palette edit without local falloff");
            Check(Math.Abs(result.L - c.L) < 1e-6 && OklabColor.InGamut(result), "Global recolor preserves locked L and gamut");
        }

        // The analytical two-control solution from section 3.5 permits a dominant weight > .5.
        var a = PaletteEntry.Identity(0x303030); a.Target.A += .006;
        var b = PaletteEntry.Identity(0xcccccc);
        state.Entries.Clear(); state.Entries.Add(a); state.Entries.Add(b);
        transform = new PaletteRecolor(state);
        const double t = .2;
        var point = new Oklab(a.Source.L * (1 - t) + b.Source.L * t,
            a.Source.A * (1 - t) + b.Source.A * t, a.Source.B * (1 - t) + b.Source.B * t);
        double k = Math.Exp(-.5), g0 = Math.Exp(-t * t / 2), g1 = Math.Exp(-(1 - t) * (1 - t) / 2);
        double w0 = Math.Max(0, g0 - k * g1), w1 = Math.Max(0, g1 - k * g0), weight = w0 / (w0 + w1);
        var mapped = transform.Transform(point);
        Check(weight > .5 && Math.Abs((mapped.A - point.A) / .006 - weight) < 1e-9, "Dominant RBF weight exceeds .5 and matches the paper equations");
        Check(Near(transform.Transform(a.Source), a.EffectiveTarget, 1e-9), "Edited control maps at weight one");
        Check(Near(transform.Transform(b.Source), b.Source, 1e-9), "Unchanged control remains an exact anchor");

        state.Entries.Clear(); state.Entries.Add(PaletteEntry.Identity(0x00ff00)); state.Entries.Add(PaletteEntry.Identity(0x0000ff));
        transform = new PaletteRecolor(state);
        double width = Math.Sqrt((double)Get(transform, "sigmaSquared"));
        Check(width > .5 && Math.Abs(width - Math.Sqrt(OklabColor.DistanceSquared(state.Entries[0].Source, state.Entries[1].Source))) < 1e-12,
            "Automatically calculated kernel width is not capped at .5");

        // A near-monochrome palette previously underflowed to zero at distant colors.
        state.Entries.Clear();
        foreach (int rgb in new[] { 0x303030, 0x303031 })
        {
            var entry = PaletteEntry.Identity(rgb); entry.Target.A += .006; state.Entries.Add(entry);
        }
        transform = new PaletteRecolor(state);
        var far = OklabColor.FromRgb(0xcccccc);
        Check(!transform.UsesFallback && Near(transform.Transform(far), new Oklab(far.L, far.A + .006, far.B), 1e-9), "Distant Gaussian weights stay normalized without underflow");
        state.Entries.RemoveAt(1);
        Check(Near(new PaletteRecolor(state).Transform(far), new Oklab(far.L, far.A + .006, far.B), 1e-9), "One control has full influence everywhere");

        var near = new RecolorSettings { Mode = RecolorMode.Palette };
        for (int i = 0; i < 30; i++)
        {
            var entry = PaletteEntry.Identity(0x808080 + i); entry.Target.A += .006; near.Entries.Add(entry);
        }
        transform = new PaletteRecolor(near);
        Check(transform.UsesFallback && Near(transform.Transform(far), new Oklab(far.L, far.A + .006, far.B), 1e-9), "Degenerate matrix fallback also covers distant colors");

        // Presets from the former UI must not impose invisible intensity or radius restrictions.
        var preset = Settings(state); preset["Recolor.Amount"] = 0; preset["Recolor.Row0.Radius"] = .01;
        var migrated = RecolorSettings.Read(preset);
        Check(migrated.HasEffect && Near(new PaletteRecolor(migrated).Transform(far), new Oklab(far.L, far.A + .006, far.B), 1e-9), "Legacy strength and radius no longer attenuate recoloring");
        migrated.Write(preset);
        Check(!preset.Capture().Keys.Any(key => key == "Recolor.Amount" || key.EndsWith(".Radius", StringComparison.Ordinal)), "New palette snapshots omit retired controls");

        var pixels = Pixels(0x303030, 0xcccccc, 0xcccccc, 0xcccccc); pixels[15] = 0; pixels[11] = 127;
        var output = GradeRenderer.Render(pixels, 4, 1, preset, new[] { true, false, true, true }).Rgba;
        Check(Rgb(output, 0) != Rgb(pixels, 0) && Rgb(output, 2) != Rgb(pixels, 2), "Both near and distant selected pixels are recolored");
        Check(Rgb(output, 1) == Rgb(pixels, 1) && Rgb(output, 3) == Rgb(pixels, 3) && output[11] == 127 && output[15] == 0,
            "Global color coverage respects spatial selection and alpha");
    }
}
