# TextureGrade — PMXEditor texture grading

[English](ReadMe.md) | [简体中文](ReadMe_sc.md) | [繁體中文](ReadMe_tc.md) | [日本語](ReadMe_ja.md)

Preview and grade model textures inside PMXEditor. Grading starts from the original image; Refresh Model provides a temporary 3D preview, while saving updates only the current material's texture reference.

## Grading and palettes

- Exposure, contrast, highlights, shadows, whites/blacks, white balance, saturation, HSL, curves, levels, RGB, clarity and sharpening.
- Continuous grayscale plus separate threshold amount and cutoff (0–255). The old gradient effect and Lab wheel are removed.
- Editable source→target palettes, original-image eyedropper, per-row enable/delete/lightness lock, and manual entries preserved on regeneration.
- Drag to exchange colors within a column: source↔source or target↔target; the other column stays in place. Row numbers, drop feedback and top/bottom edge scrolling assist dragging. For distant colors, enable **Click to exchange** or right-click **Exchange with another color**, scroll freely, then select another swatch. Esc or the pinned Cancel button exits; ordinary clicks still open the picker. Row enable/manual flags and lightness locks stay in place; targets are gamut-fitted at their destination lightness. Each exchange is one undo step. Dragging does not render the texture; dropping updates only the affected swatches and schedules an asynchronous preview.
- Independently estimate source/reference color counts automatically or enter 1–128 colors. Estimation uses perceptual separation and visible area, suppressing subtle gradients and isolated noise; it is not object segmentation.
- Import and optionally crop a reference under Palette. Generate editable mappings matched by relative lightness, occupied area and dark-to-light order. All reference controls are used when the source count permits.
- Reference mappings transform chromatic variation between anchors as well, reducing residual source hues. Existing presets retain their old interpolation; regenerate to use the corrected transfer.
- Choose HSV, RGB or OKLCH in the picker. Independent lightness lock defaults to preserving per-pixel OKLCH L; gamut fitting reduces chroma at fixed L/hue. Eight-bit quantization and later grading effects may change final measured lightness.
- Global palette interpolation runs before other effects, without manual radius/strength controls. Statistical transfer is removed.
- Undo/redo, per-material memory and presets include palette data, scope and automatic count choices. Generated palettes need no reference image to replay.

## UV workflow

- Wheel zoom, right-drag pan, click faces, double-click connected islands; Shift adds and Ctrl subtracts. Receive/send vertex selections from/to PMXEditor.
- Invert from the left Selection toolbar or Edit menu (`Ctrl+I`); select all with `Ctrl+A`. Text boxes retain their text-editing shortcuts.
- Switching materials or re-reading preserves each material's UV selection and received vertex markers. Changed geometry or UVs invalidate old selections.
- Selection-first scope grades selected faces, or the whole texture when selection is empty. Inverting a full selection produces that same empty-selection behavior.
- Whole Texture scope retains but ignores the selection and warns about shared texture regions. Hiding UV overlays does not change grading or selection.
- Dense vertex displays use retained drawing layers. Drag previews are limited to a 1024-pixel longest side; final rendering uses full resolution.

## Output and presets

- Refresh Model writes a temporary preview, falling back to the system temp folder if the texture folder is not writable.
- Save as New Texture opens format, size and JPEG-quality options. File also offers quick PNG saving. Original textures are protected; saved references are relative to the current PMX folder when possible.
- Export black/white masks, UV wireframes or selection-alpha PNGs. Selection alpha replaces the original alpha: selected = 255, other = 0. RGB can contain white, original pixels or graded pixels. This uses the explicit UV selection even in Whole Texture scope and changes no model reference.
- Presets support rename, name/time/custom sorting and Move Up/Down. Custom order and sort mode persist in `presets/order.txt` and `sort.txt`; numeric JSON contents are retained on rename.

| Format | Alpha and compression |
| --- | --- |
| PNG / TGA / DDS Raw | Lossless, full alpha |
| DDS DXT1 | Lossy, binary alpha |
| DDS DXT3 / DXT5 | Lossy, 4-bit/interpolated alpha |
| JPEG / BMP / GIF / TIFF | Flattened on white; JPEG/GIF are lossy; GIF is indexed |

The UI and built-in manual support English, Simplified/Traditional Chinese and Japanese. Existing customized manuals in `data` are not overwritten automatically.

## Build / 构建

Windows, .NET Framework 4.8, PMXEditor 0.2.7.5 SDK. No new NuGet dependencies.

```powershell
dotnet build PEPlugins-TextureGrade.csproj -c Release -p:PmxEditorDir="D:\Tools\PmxEditor_0275"
dotnet build tests/TextureGrade.Tests.csproj -c Release -p:PmxEditorDir="D:\Tools\PmxEditor_0275"
.\tests\bin\Release\net48\TextureGrade.Tests.exe .\artifacts\verification
```

`PmxEditorDir` must contain `Lib/PEPlugin/PEPlugin.dll` and the PMXEditor runtime libraries. If a sibling `PmxEditor_0275` directory exists, the default path is sufficient.

Output: `bin/Release/net48/PEPlugins-TextureGrade.dll`. Copy the plugin DLL into PMXEditor's plugin folder and restart PMXEditor. Do not replace the host's runtime libraries with build output. `bin/`, `obj/`, and `artifacts/` are ignored by Git; user-provided originals in `ref/` are retained.

## Source / 来源

Fork: [Loki-0228/PEPlugins-TextureGrade](https://github.com/Loki-0228/PEPlugins-TextureGrade). Original project: [SaraKale/PEPlugins-TextureGrade](https://github.com/SaraKale/PEPlugins-TextureGrade). Selected v1.0.2 changes were ported from [f1a51a4](https://github.com/SaraKale/PEPlugins-TextureGrade/commit/f1a51a4c865602231d35784940e342bce5671c19), preserving the fork's palette and performance work. See [CHANGELOG.md](CHANGELOG.md) and [GPL-3.0 license](LICENSE).
