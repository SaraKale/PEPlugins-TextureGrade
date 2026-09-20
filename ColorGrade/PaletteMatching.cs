using System;
using System.Linq;

namespace TextureGrade.ColorGrade
{
    public static partial class PaletteAnalysis
    {
        // Monotone alignment: prioritize relative lightness, then area quantiles. When enough
        // source controls exist, every reference block is represented. Never reverse shadows/highlights.
        private static int[] PairByLightnessAndMass(PaletteColor[] source, PaletteColor[] target)
        {
            int n = source.Length, m = target.Length;
            if (n == 1) return new[] { Enumerable.Range(0, m).OrderByDescending(i => target[i].Weight).ThenBy(i => target[i].Rgb).First() };
            if (m == 1) return new int[n];
            if (n == m) return Enumerable.Range(0, n).ToArray();
            var sl = RelativeLightness(source); var tl = RelativeLightness(target);
            var sw = RelativeMass(source); var tw = RelativeMass(target);
            var sq = MidQuantiles(sw); var tq = MidQuantiles(tw);
            var prefix = new double[m + 1]; for (int j = 0; j < m; j++) prefix[j + 1] = prefix[j] + tw[j];
            var cost = new double[n, m]; var parent = new int[n, m];
            for (int i = 0; i < n; i++) for (int j = 0; j < m; j++) { cost[i, j] = double.PositiveInfinity; parent[i, j] = -1; }
            double MatchCost(int i, int j) => (.25 / n + .75 * sw[i]) *
                (.65 * Math.Pow(sl[i] - tl[j], 2) + .35 * Math.Pow(sq[i] - tq[j], 2));
            cost[0, 0] = MatchCost(0, 0);
            for (int i = 1; i < n; i++)
                for (int j = 0; j < m; j++)
                {
                    // More sources: stay or advance one target, covering all target blocks.
                    // Fewer sources: advance; penalize skipping a large reference block.
                    int start = n >= m ? Math.Max(0, j - 1) : 0;
                    int end = n >= m ? j : j - 1;
                    for (int k = start; k <= end; k++)
                    {
                        double value = cost[i - 1, k] + MatchCost(i, j);
                        if (n < m) value += .1 * (prefix[j] - prefix[k + 1]);
                        if (value < cost[i, j]) { cost[i, j] = value; parent[i, j] = k; }
                    }
                }
            var result = new int[n]; result[n - 1] = m - 1;
            for (int i = n - 1; i > 0; i--) result[i - 1] = parent[i, result[i]];
            return result;
        }
        private static double[] RelativeLightness(PaletteColor[] palette)
        {
            var l = palette.Select(c => OklabColor.FromRgb(c.Rgb).L).ToArray();
            double span = l[l.Length - 1] - l[0], minimum = l[0];
            return l.Select(v => span > 1e-6 ? (v - minimum) / span : .5).ToArray();
        }
        private static double[] RelativeMass(PaletteColor[] palette)
        {
            double total = palette.Sum(c => Math.Max(0, c.Weight));
            return palette.Select(c => total > 0 ? Math.Max(0, c.Weight) / total : 1.0 / palette.Length).ToArray();
        }
        private static double[] MidQuantiles(double[] mass)
        {
            double cumulative = 0; var result = new double[mass.Length];
            for (int i = 0; i < mass.Length; i++) { result[i] = cumulative + mass[i] / 2; cumulative += mass[i]; }
            return result;
        }
    }
}
