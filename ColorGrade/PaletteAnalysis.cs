using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    public sealed class ColorSample
    {
        public readonly int Rgb;
        public readonly Oklab Color;
        public double Weight;
        public ColorSample(int rgb, double weight) { Rgb = rgb; Color = OklabColor.FromRgb(rgb); Weight = weight; }
    }
    public sealed class PaletteColor
    {
        public int Rgb;
        public double Weight;
    }
    public static partial class PaletteAnalysis
    {
        // Sample the sequence of eligible pixels, not the image grid: even a one-pixel UV selection is sampled.
        public static List<ColorSample> Sample(byte[] rgba, bool[] mask = null, CancellationToken token = default)
        {
            int count = rgba.Length / 4, eligible = 0;
            for (int p = 0; p < count; p++)
            {
                if ((p & 16383) == 0) token.ThrowIfCancellationRequested();
                if (rgba[4 * p + 3] != 0 && (mask == null || mask[p])) eligible++;
            }
            int take = Math.Min(32768, eligible), seen = 0, taken = 0;
            var colors = new Dictionary<int, ColorSample>();
            if (take == 0) return new List<ColorSample>();
            for (int p = 0; p < count && taken < take; p++)
            {
                if ((p & 16383) == 0) token.ThrowIfCancellationRequested();
                int o = 4 * p;
                if (rgba[o + 3] == 0 || (mask != null && !mask[p])) continue;
                if (seen++ != (long)taken * eligible / take) continue;
                taken++;
                int rgb = (rgba[o] << 16) | (rgba[o + 1] << 8) | rgba[o + 2];
                double weight = rgba[o + 3] / 255.0;
                if (colors.TryGetValue(rgb, out var s)) s.Weight += weight;
                else colors.Add(rgb, new ColorSample(rgb, weight));
            }
            return colors.Values.OrderBy(s => s.Rgb).ToList();
        }

        public static List<PaletteColor> Extract(IList<ColorSample> samples, int count, CancellationToken token = default)
        {
            int k = Math.Min(Math.Max(1, Math.Min(count, RecolorSettings.MaxPaletteColors)), samples.Count);
            if (k == 0) return new List<PaletteColor>();
            if (k == samples.Count) return samples.Select(s => new PaletteColor { Rgb = s.Rgb, Weight = s.Weight })
                .OrderByDescending(s => s.Weight).ThenBy(s => s.Rgb).ToList();
            var centers = new List<Oklab>();
            var random = new Random(23817);
            var distances = Enumerable.Repeat(double.MaxValue, samples.Count).ToArray();
            int initial = WeightedChoice(samples.Select(s => s.Weight).ToArray(), random);
            centers.Add(samples[initial].Color);
            while (centers.Count < k)
            {
                token.ThrowIfCancellationRequested();
                var weights = new double[samples.Count];
                for (int i = 0; i < samples.Count; i++)
                {
                    distances[i] = Math.Min(distances[i], OklabColor.DistanceSquared(samples[i].Color, centers[centers.Count - 1]));
                    weights[i] = distances[i] * samples[i].Weight;
                }
                centers.Add(samples[WeightedChoice(weights, random)].Color);
            }
            var labels = new int[samples.Count];
            for (int iteration = 0; iteration < 30; iteration++)
            {
                token.ThrowIfCancellationRequested();
                var sum = new Oklab[k]; var mass = new double[k];
                for (int i = 0; i < samples.Count; i++)
                {
                    if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                    int j = Nearest(samples[i].Color, centers); labels[i] = j;
                    var s = samples[i]; mass[j] += s.Weight;
                    sum[j].L += s.Color.L * s.Weight; sum[j].A += s.Color.A * s.Weight; sum[j].B += s.Color.B * s.Weight;
                }
                double moved = 0;
                for (int j = 0; j < k; j++)
                {
                    if (mass[j] == 0) continue;
                    var c = new Oklab(sum[j].L / mass[j], sum[j].A / mass[j], sum[j].B / mass[j]);
                    moved += OklabColor.DistanceSquared(c, centers[j]); centers[j] = c;
                }
                if (moved < 1e-12) break;
            }
            var result = new List<PaletteColor>();
            for (int i = 0; i < samples.Count; i++) labels[i] = Nearest(samples[i].Color, centers);
            for (int j = 0; j < k; j++)
            {
                double nearest = double.MaxValue, weight = 0; int rgb = -1;
                for (int i = 0; i < samples.Count; i++)
                {
                    if (labels[i] != j) continue;
                    weight += samples[i].Weight;
                    double d = OklabColor.DistanceSquared(samples[i].Color, centers[j]);
                    if (d < nearest) { nearest = d; rgb = samples[i].Rgb; }
                }
                if (rgb >= 0) result.Add(new PaletteColor { Rgb = rgb, Weight = weight });
            }
            return result.OrderByDescending(c => c.Weight).ThenBy(c => c.Rgb).ToList();
        }
        private static int WeightedChoice(double[] w, Random random)
        {
            double target = random.NextDouble() * w.Sum();
            for (int i = 0; i < w.Length; i++) { target -= w[i]; if (target <= 0 && w[i] > 0) return i; }
            return w.Length - 1;
        }
        private static int Nearest(Oklab color, IList<Oklab> centers)
        {
            int best = 0; double distance = double.MaxValue;
            for (int j = 0; j < centers.Count; j++)
            {
                double d = OklabColor.DistanceSquared(color, centers[j]);
                if (d < distance) { distance = d; best = j; }
            }
            return best;
        }
        public static List<PaletteEntry> Match(IList<PaletteColor> source, IList<PaletteColor> reference, bool lockLightness = true)
        {
            var result = new List<PaletteEntry>();
            if (source.Count == 0 || reference.Count == 0) return result;
            var src = source.OrderBy(c => OklabColor.FromRgb(c.Rgb).L).ThenBy(c => c.Rgb).ToArray();
            var dst = reference.OrderBy(c => OklabColor.FromRgb(c.Rgb).L).ThenBy(c => c.Rgb).ToArray();
            var pairing = PairByLightnessAndMass(src, dst);
            for (int i = 0; i < src.Length; i++)
            {
                int rgb = dst[pairing[i]].Rgb;
                var entry = PaletteEntry.Identity(src[i].Rgb);
                entry.TransferChroma = true;
                entry.LockLightness = lockLightness;
                entry.Target = OklabColor.FromRgb(rgb);
                entry.Target = entry.EffectiveTarget;
                result.Add(entry);
            }
            return result;
        }
    }
}
