using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    public static partial class PaletteAnalysis
    {
        // Estimate perceptually distinct, populated color groups, not unique RGB values or
        // connected components. Small gradients/noise do not keep increasing the color count.
        public static List<PaletteColor> ExtractAutomatic(IList<ColorSample> samples, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (samples.Count == 0) return new List<PaletteColor>();
            var groups = new List<ColorGroup> { Summarize(samples, Enumerable.Range(0, samples.Count).ToArray()) };
            double minimumMass = groups[0].Mass * .002; // 0.2% of alpha-weighted visible area.
            while (groups.Count < RecolorSettings.MaxPaletteColors)
            {
                token.ThrowIfCancellationRequested();
                var split = groups.Where(g => !g.Finished && g.Error / g.Mass > .012 * .012)
                    .OrderByDescending(g => g.Error).FirstOrDefault();
                if (split == null) break;
                split.Finished = true;
                if (split.Indices.Length < 2) continue;
                int first = FarthestPopulated(samples, split.Indices, split.Center);
                int second = FarthestPopulated(samples, split.Indices, samples[first].Color);
                var a = samples[first].Color; var b = samples[second].Color;
                ColorGroup left = null, right = null;
                for (int iteration = 0; iteration < 30; iteration++)
                {
                    token.ThrowIfCancellationRequested();
                    var l = new List<int>(); var r = new List<int>();
                    foreach (int index in split.Indices)
                    {
                        if ((index & 1023) == 0) token.ThrowIfCancellationRequested();
                        var c = samples[index].Color;
                        (OklabColor.DistanceSquared(c, a) <= OklabColor.DistanceSquared(c, b) ? l : r).Add(index);
                    }
                    if (l.Count == 0 || r.Count == 0) { left = right = null; break; }
                    left = Summarize(samples, l.ToArray()); right = Summarize(samples, r.ToArray());
                    double movement = OklabColor.DistanceSquared(a, left.Center) + OklabColor.DistanceSquared(b, right.Center);
                    a = left.Center; b = right.Center;
                    if (movement < 1e-12) break;
                }
                if (left == null || left.Mass < minimumMass || right.Mass < minimumMass ||
                    OklabColor.DistanceSquared(a, b) < .03 * .03) continue;
                groups.Remove(split); groups.Add(left); groups.Add(right);
            }
            return groups.Select(g => new PaletteColor
            {
                Weight = g.Mass,
                Rgb = samples[g.Indices.OrderBy(i => OklabColor.DistanceSquared(samples[i].Color, g.Center))
                    .ThenBy(i => samples[i].Rgb).First()].Rgb
            }).OrderByDescending(c => c.Weight).ThenBy(c => c.Rgb).ToList();
        }
        private sealed class ColorGroup
        {
            public int[] Indices;
            public Oklab Center;
            public double Mass, Error;
            public bool Finished;
        }
        private static ColorGroup Summarize(IList<ColorSample> samples, int[] indices)
        {
            var group = new ColorGroup { Indices = indices };
            foreach (int i in indices)
            {
                var s = samples[i]; group.Mass += s.Weight;
                group.Center.L += s.Color.L * s.Weight; group.Center.A += s.Color.A * s.Weight; group.Center.B += s.Color.B * s.Weight;
            }
            group.Center.L /= group.Mass; group.Center.A /= group.Mass; group.Center.B /= group.Mass;
            foreach (int i in indices) group.Error += OklabColor.DistanceSquared(samples[i].Color, group.Center) * samples[i].Weight;
            return group;
        }
        private static int FarthestPopulated(IList<ColorSample> samples, int[] indices, Oklab center)
            => indices.OrderByDescending(i => OklabColor.DistanceSquared(samples[i].Color, center) * samples[i].Weight)
                .ThenBy(i => samples[i].Rgb).First();
    }
}
