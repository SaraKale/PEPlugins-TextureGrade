using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    public sealed class PaletteRecolor
    {
        private readonly PaletteEntry[] entries;
        private readonly Oklab[] sources, shifts;
        private readonly double[] chromaReal, chromaImaginary;
        private readonly double[,] inverse;
        private readonly double sigmaSquared;
        public bool UsesFallback => inverse == null && entries.Length > 1;

        public PaletteRecolor(RecolorSettings settings)
        {
            entries = RecolorSettings.Unique(settings.Entries.Where(e => e.Enabled)).OrderBy(e => e.SourceRgb).ToArray();
            sources = entries.Select(e => e.Source).ToArray();
            shifts = entries.Select(e => { var t = e.EffectiveTarget; var s = e.Source;
                return new Oklab(e.LockLightness ? 0 : t.L - s.L, t.A - s.A, t.B - s.B); }).ToArray();
            chromaReal = new double[entries.Length]; chromaImaginary = new double[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                chromaReal[i] = 1;
                if (!entries[i].TransferChroma) continue;
                var s = sources[i]; var t = entries[i].EffectiveTarget;
                if (Math.Abs(shifts[i].A) + Math.Abs(shifts[i].B) < 1e-12) continue;
                if (t.Chroma < 1e-6) { chromaReal[i] = 0; continue; }
                // A complex multiplier rotates/scales chromatic residuals around each source anchor:
                // f_i(c).ab = target_i.ab + M_i * (c.ab - source_i.ab).
                // Plain translation left saturated purple residuals in cyan reference transfers.
                // Regularize toward identity near neutral source colors, where hue is undefined.
                // Identity edits stay identity, and every edited anchor still maps exactly to its target.
                double denominator = s.A * s.A + s.B * s.B + 1e-6;
                double real = 1 + (shifts[i].A * s.A + shifts[i].B * s.B) / denominator;
                double imaginary = (shifts[i].B * s.A - shifts[i].A * s.B) / denominator;
                // Bound noise amplification for nearly gray controls, not palette influence/strength.
                double scale = Math.Max(1, Math.Sqrt(real * real + imaginary * imaginary) / 4);
                chromaReal[i] = real / scale; chromaImaginary[i] = imaginary / scale;
            }
            double sum = 0; int pairs = 0;
            for (int i = 0; i < sources.Length; i++)
                for (int j = i + 1; j < sources.Length; j++) { sum += Math.Sqrt(OklabColor.DistanceSquared(sources[i], sources[j])); pairs++; }
            // Chang et al., section 3.5: Gaussian width is the mean pairwise source distance.
            // No user radius, upper bound or local cutoff. A single control has weight one everywhere.
            sigmaSquared = pairs > 0 ? Math.Pow(sum / pairs, 2) : 1;
            if (sources.Length > 1)
            {
                var matrix = new double[sources.Length, sources.Length];
                for (int i = 0; i < sources.Length; i++)
                    for (int j = 0; j < sources.Length; j++) matrix[i, j] = Kernel(OklabColor.DistanceSquared(sources[i], sources[j]));
                inverse = Invert(matrix);
            }
        }
        private double Kernel(double d) => Math.Exp(-d / (2 * sigmaSquared));
        public Oklab Transform(Oklab color) => Transform(color, new double[entries.Length], new double[entries.Length], new double[entries.Length]);
        private Oklab Transform(Oklab c, double[] distances, double[] weights, double[] kernels)
        {
            if (entries.Length == 0) return c;
            double nearest = double.MaxValue;
            for (int i = 0; i < entries.Length; i++)
            {
                distances[i] = OklabColor.DistanceSquared(c, sources[i]);
                if (distances[i] < 1e-20) return Shift(c, shifts[i]);
                nearest = Math.Min(nearest, distances[i]);
            }
            // Scaling every Gaussian by the same positive factor cancels during normalization.
            // Subtract the nearest distance to avoid underflow for a tight palette and a distant pixel.
            if (inverse != null) for (int i = 0; i < entries.Length; i++) kernels[i] = Kernel(distances[i] - nearest);
            double total = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                double w = 0;
                if (inverse == null) w = nearest / distances[i];
                else for (int j = 0; j < entries.Length; j++) w += inverse[i, j] * kernels[j];
                weights[i] = Math.Max(0, w); total += weights[i];
            }
            if (total <= 1e-20 || double.IsNaN(total) || double.IsInfinity(total))
            {
                // A degenerate interpolant must still cover the full color space.
                total = 0;
                for (int i = 0; i < entries.Length; i++) { weights[i] = nearest / distances[i]; total += weights[i]; }
            }
            var delta = new Oklab();
            for (int i = 0; i < entries.Length; i++)
            {
                double w = weights[i] / total;
                double da = c.A - sources[i].A, db = c.B - sources[i].B;
                delta.L += shifts[i].L * w;
                delta.A += (shifts[i].A + (chromaReal[i] - 1) * da - chromaImaginary[i] * db) * w;
                delta.B += (shifts[i].B + chromaImaginary[i] * da + (chromaReal[i] - 1) * db) * w;
            }
            return Shift(c, delta);
        }
        private static Oklab Shift(Oklab c, Oklab shift)
            => OklabColor.FitGamut(new Oklab(c.L + shift.L, c.A + shift.A, c.B + shift.B));

        public void Apply(byte[] rgba, bool[] mask, CancellationToken token)
        {
            if (entries.Length == 0 || shifts.All(s => Math.Abs(s.L) + Math.Abs(s.A) + Math.Abs(s.B) < 1e-12)) return;
            var cache = new Dictionary<int, int>();
            var distances = new double[entries.Length]; var weights = new double[entries.Length]; var kernels = new double[entries.Length];
            for (int p = 0; p < rgba.Length / 4; p++)
            {
                if ((p & 4095) == 0) token.ThrowIfCancellationRequested();
                int o = p * 4;
                if (rgba[o + 3] == 0 || (mask != null && !mask[p])) continue;
                int rgb = (rgba[o] << 16) | (rgba[o + 1] << 8) | rgba[o + 2];
                if (!cache.TryGetValue(rgb, out int output))
                {
                    var c = OklabColor.FromRgb(rgb); var t = Transform(c, distances, weights, kernels);
                    output = Math.Abs(c.L - t.L) + Math.Abs(c.A - t.A) + Math.Abs(c.B - t.B) < 1e-14 ? rgb : OklabColor.ToRgb(t);
                    if (cache.Count < 65536) cache[rgb] = output;
                }
                rgba[o] = (byte)(output >> 16); rgba[o + 1] = (byte)(output >> 8); rgba[o + 2] = (byte)output;
            }
        }

        private static double[,] Invert(double[,] source)
        {
            int n = source.GetLength(0); var m = new double[n, n * 2];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) m[i, j] = source[i, j]; m[i, i + n] = 1; }
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int row = col + 1; row < n; row++) if (Math.Abs(m[row, col]) > Math.Abs(m[pivot, col])) pivot = row;
                if (Math.Abs(m[pivot, col]) < 1e-9) return null;
                for (int j = 0; j < 2 * n; j++) { double t = m[col, j]; m[col, j] = m[pivot, j]; m[pivot, j] = t; }
                double d = m[col, col];
                for (int j = 0; j < 2 * n; j++) m[col, j] /= d;
                for (int row = 0; row < n; row++)
                {
                    if (row == col) continue;
                    double f = m[row, col];
                    for (int j = 0; j < 2 * n; j++) m[row, j] -= f * m[col, j];
                }
            }
            var result = new double[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            {
                result[i, j] = m[i, j + n];
                if (Math.Abs(result[i, j]) > 1e9 || double.IsNaN(result[i, j])) return null;
            }
            return result;
        }
    }

}
