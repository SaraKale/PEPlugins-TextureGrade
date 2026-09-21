using System;
using System.Collections.Generic;
using System.Linq;
using TextureGrade.ColorGrade;

namespace TextureGrade.Models
{
    public enum RecolorMode { Off = 0, Palette = 1 }

    public sealed class PaletteEntry
    {
        public int SourceRgb;
        public Oklab Target;
        public bool Enabled = true, LockLightness = true, Manual;
        // Reference-generated mappings also transfer the hue/chroma of colors between controls.
        // Persist per entry so later manual edits, snapshots and image-free presets keep the same interpolation.
        public bool TransferChroma;
        public Oklab Source => OklabColor.FromRgb(SourceRgb);
        public Oklab EffectiveTarget => OklabColor.FitGamut(new Oklab(LockLightness ? Source.L : Target.L, Target.A, Target.B));
        public PaletteEntry Copy() => (PaletteEntry)MemberwiseClone();
        public static PaletteEntry Identity(int rgb, bool manual = false)
            => new PaletteEntry { SourceRgb = rgb, Target = OklabColor.FromRgb(rgb), Manual = manual };
    }

    /// <summary>Typed view over the existing numeric preset format; no external reference file is required.</summary>
    public sealed class RecolorSettings
    {
        public const string Prefix = "Recolor.";
        public const int MaxPaletteColors = 128;
        public RecolorMode Mode;
        public int DetectCount = 8, ReferenceCount; // 0 = follow source count
        public bool ReferenceLock = true;
        public bool AutoDetectCount, AutoReferenceCount;
        public List<PaletteEntry> Entries = new List<PaletteEntry>();
        public bool HasEffect => Mode == RecolorMode.Palette && Entries.Any(e => e.Enabled &&
            OklabColor.DistanceSquared(e.Source, e.EffectiveTarget) > 1e-15);

        public static RecolorSettings Read(GradeSettings s)
        {
            double Get(string k, double fallback = 0) => s.Get(Prefix + k, fallback);
            var r = new RecolorSettings
            {
                // Former mode 2 must turn off, not silently activate the saved palette.
                Mode = Get("Mode") == (int)RecolorMode.Palette ? RecolorMode.Palette : RecolorMode.Off,
                DetectCount = (int)OklabColor.Clamp(Get("Count", 8), 1, MaxPaletteColors),
                ReferenceCount = (int)OklabColor.Clamp(Get("ReferenceCount"), 0, MaxPaletteColors),
                ReferenceLock = Get("ReferenceUnlock") < .5,
                AutoDetectCount = Get("AutoCount") > .5, AutoReferenceCount = Get("AutoReferenceCount") > .5
            };
            int n = (int)OklabColor.Clamp(Get("Rows"), 0, 1024);
            for (int i = 0; i < n; i++)
            {
                string p = "Row" + i + ".";
                int rgb = (int)OklabColor.Clamp(Get(p + "Source"), 0, 0xffffff);
                var source = OklabColor.FromRgb(rgb);
                r.Entries.Add(new PaletteEntry
                {
                    SourceRgb = rgb,
                    Target = new Oklab(OklabColor.Clamp(Get(p + "L", source.L), 0, 1),
                        OklabColor.Clamp(Get(p + "A", source.A), -.5, .5), OklabColor.Clamp(Get(p + "B", source.B), -.5, .5)),
                    Enabled = Get(p + "Disabled") < .5, LockLightness = Get(p + "Unlock") < .5,
                    Manual = Get(p + "Manual") > .5, TransferChroma = Get(p + "TransferChroma") > .5
                });
            }
            r.Entries = Unique(r.Entries);
            return r;
        }
        public static List<PaletteEntry> Unique(IEnumerable<PaletteEntry> entries)
            => entries.GroupBy(e => e.SourceRgb).Select(g => g.OrderByDescending(e => e.Manual).First().Copy()).ToList();

        public void ReplaceAutomatic(IEnumerable<PaletteEntry> automatic)
            => Entries = Unique(Entries.Where(e => e.Manual).Concat(automatic));

        // Swap one column, not entire mappings. Keep row flags and the unquantized target
        // values: each destination row applies its own lightness lock and gamut constraint.
        public bool SwapColors(int first, int second, bool source)
        {
            if (first < 0 || second < 0 || first >= Entries.Count || second >= Entries.Count || first == second) return false;
            var a = Entries[first]; var b = Entries[second];
            if (source)
            {
                if (a.SourceRgb == b.SourceRgb) return false;
                int rgb = a.SourceRgb; a.SourceRgb = b.SourceRgb; b.SourceRgb = rgb;
            }
            else
            {
                if (a.Target.L == b.Target.L && a.Target.A == b.Target.A && a.Target.B == b.Target.B) return false;
                var color = a.Target; a.Target = b.Target; b.Target = color;
            }
            return true;
        }

        public void Write(GradeSettings s)
        {
            var values = s.Capture();
            foreach (string key in values.Keys.Where(k => k.StartsWith(Prefix, StringComparison.Ordinal)).ToArray()) values.Remove(key);
            void Set(string key, double v) => values[Prefix + key] = v;
            Set("Mode", Mode == RecolorMode.Palette ? 1 : 0); Set("Count", DetectCount); Set("ReferenceCount", ReferenceCount);
            Set("AutoCount", AutoDetectCount ? 1 : 0); Set("AutoReferenceCount", AutoReferenceCount ? 1 : 0);
            // Old Amount / Row*.Radius keys are intentionally retired: palette interpolation is global.
            Set("ReferenceUnlock", ReferenceLock ? 0 : 1);
            Set("Rows", Entries.Count);
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i]; string p = "Row" + i + ".";
                Set(p + "Source", e.SourceRgb); Set(p + "L", e.Target.L); Set(p + "A", e.Target.A); Set(p + "B", e.Target.B);
                Set(p + "Disabled", e.Enabled ? 0 : 1);
                Set(p + "Unlock", e.LockLightness ? 0 : 1); Set(p + "Manual", e.Manual ? 1 : 0);
                Set(p + "TransferChroma", e.TransferChroma ? 1 : 0);
            }
            s.Replace(values);
        }
    }
}
