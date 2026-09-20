using System.Collections.Generic;

namespace TextureGrade.Localization
{
    internal static class RecolorTexts
    {
        internal static void Add(Dictionary<string, string[]> d)
        {
            void A(string key, string en, string cn, string tw, string ja) => d["Rc." + key] = new[] { en, cn, tw, ja };
            A("Title", "Palette", "调色板", "調色盤", "パレット");
            A("EnablePalette", "Enable palette", "启用调色板", "啟用調色盤", "パレットを有効化");
            A("Off", "Off", "关闭", "關閉", "オフ");
            A("Palette", "Palette", "调色板", "調色盤", "パレット");
            A("ModeNote", "Edit source → target colors, or generate a target-style palette from a reference image below. The palette runs before the other adjustments.", "编辑原色→目标色，也可在下方「颜色迁移」中从参考图自动生成目标风格调色板。调色板先于其他调色执行。", "編輯原色→目標色，亦可在下方「色彩遷移」中從參考圖自動產生目標風格調色盤。調色盤先於其他調色執行。", "元の色→目標色を編集するか、下の色転送で参照画像のスタイルを持つパレットを生成できます。他の調整より先に適用されます。");
            A("Count", "Source colors (1–128)", "识别原色数量（1–128）", "辨識原色數量（1–128）", "元の色数（1–128）");
            A("ReferenceCount", "Reference colors (1–128; 0 = follow source)", "参考色数量（1–128；0 = 跟随原色）", "參考色數量（1–128；0 = 跟隨原色）", "参照色数（1–128、0 = 元の色数）");
            A("Detect", "Detect colors", "自动识别", "自動辨識", "色を自動抽出");
            A("AutoCount", "Auto count · distinct color blocks", "自动数量 · 按主色块估计", "自動數量 · 依主色塊估計", "色数を自動推定 · 主な色のまとまり");
            A("CountAuto", "Source count (updated after detection)", "原色数量（识别后更新）", "原色數量（辨識後更新）", "元の色数（抽出後に更新）");
            A("ReferenceCountAuto", "Reference count (updated after generation)", "参考色数量（生成后更新）", "參考色數量（產生後更新）", "参照色数（生成後に更新）");
            A("AutoCountNote", "Estimate 1–128 colors from perceptual separation and visible area. Ignore tiny noise and subtle gradients. Detect or generate to update the displayed count; turn this off to enter a count manually.", "根据色差和有效面积估计 1–128 色，忽略零星噪点与细微渐变。识别或生成后显示实际数量；关闭可手动指定。", "依色差和有效面積估計 1–128 色，忽略零星雜訊與細微漸層。辨識或產生後顯示實際數量；關閉可手動指定。", "知覚的な色差と面積から 1–128 色を推定し、小さなノイズや微細な階調差を無視します。抽出・生成後に実際の色数を表示します。オフにすると手動指定できます。");
            A("DetectedCount", "Detected {0} source colors. You can undo this operation.", "已识别 {0} 个原色，可撤销本次操作。", "已辨識 {0} 個原色，可復原本次操作。", "元の色を {0} 色抽出しました。元に戻す操作が可能です。");
            A("MatchedCount", "Generated palette: {0} source colors / {1} reference colors, matched by lightness and area. You can undo this operation.", "已生成调色板：原图 {0} 色 / 参考图 {1} 色，按明度与面积占比配对，可撤销。", "已產生調色盤：原圖 {0} 色 / 參考圖 {1} 色，依明度與面積占比配對，可復原。", "元画像 {0} 色 / 参照画像 {1} 色を明度と面積比で対応付けました。元に戻す操作が可能です。");
            A("Add", "Add mapping", "添加映射", "新增對應", "対応を追加");
            A("Reset", "Clear palette", "清空调色板", "清空調色盤", "パレットをクリア");
            A("DetectNote", "Detection rebuilds automatic rows and keeps manual rows. Use Undo to restore the previous palette.", "重新识别会重建自动项并保留手动项，可撤销恢复。", "重新辨識會重建自動項並保留手動項，可復原。", "再抽出は自動項目を作り直し、手動項目は保持します。元に戻す操作が可能です。");
            A("AutomaticWeights", "Palette colors jointly transform all colors within the grading scope. Their influence is computed automatically, with no manual strength or range limit.", "调色板共同作用于当前调色范围内的所有颜色，自动计算各控制色的影响，不设手动强度或范围上限。", "調色盤共同作用於目前調色範圍內的所有色彩，自動計算各控制色的影響，不設手動強度或範圍上限。", "調色範囲内のすべての色をパレット全体で変換します。各色の影響は自動計算され、手動の強度や範囲の上限はありません。");
            A("Auto", "Auto", "自动", "自動", "自動");
            A("Manual", "Manual", "手动", "手動", "手動");
            A("Source", "Edit original color", "编辑原色", "編輯原色", "元の色を編集");
            A("Target", "Edit target color", "编辑目标色", "編輯目標色", "変換先の色を編集");
            A("SourceColumn", "source", "原色", "原色", "元の色");
            A("TargetColumn", "target", "目标色", "目標色", "目標色");
            A("SwapHint", "Drag colors within the same column to exchange them. Click to edit. Scroll at the top/bottom edge; Esc cancels.", "同列色块可拖动交换，单击仍可编辑。拖至上下边缘自动滚动，Esc 取消。", "同欄色塊可拖曳交換，點選仍可編輯。拖至上下邊緣自動捲動，Esc 取消。", "同じ列の色をドラッグして交換、クリックで編集。上下端で自動スクロール、Esc でキャンセル。");
            A("SwapClickMode", "Click to exchange · long distances", "点击交换 · 适合远距离", "點選交換 · 適合遠距離", "クリックで交換 · 離れた色向け");
            A("SwapClickHint", "Select one color, scroll freely, then click another color in the same column. Row lightness locks still apply. Turn this off to click and edit colors.", "先点一个色块，自由滚动后再点同列另一色块。各行明度锁定继续生效；关闭此选项恢复单击编辑。", "先點一個色塊，自由捲動後再點同欄另一色塊。各列明度鎖定繼續生效；關閉此選項恢復點選編輯。", "色を選び、自由にスクロールして同じ列の別の色をクリック。各行の明度保持は有効です。オフにするとクリックで編集できます。");
            A("SwapCellHint", "Row {0} · {1}. Click to edit; drag within this column to exchange. Right-click to start a long-distance exchange.", "第 {0} 行 · {1}。单击编辑，同列拖动交换；右键可开始远距离交换。", "第 {0} 列 · {1}。點選編輯，同欄拖曳交換；右鍵可開始遠距離交換。", "{0} 行目 · {1}。クリックで編集、同じ列でドラッグ交換。右クリックで離れた色との交換を開始。");
            A("SwapStart", "Exchange with another color…", "与另一色块交换…", "與另一色塊交換…", "別の色と交換…");
            A("SwapChooseFirst", "Click exchange: select the first color. Esc exits.", "点击交换：请选择第一个色块；Esc 退出。", "點選交換：請選第一個色塊；Esc 退出。", "クリック交換：最初の色を選択。Esc で終了。");
            A("SwapPending", "Row {0} · {1} selected. Choose another in this column; Esc cancels.", "已选第 {0} 行{1}，请选同列另一色块；Esc 取消。", "已選第 {0} 列{1}，請選同欄另一色塊；Esc 取消。", "{0} 行目の{1}を選択中。同じ列の別の色を選択。Esc でキャンセル。");
            A("SwapSameColumn", "Exchange within the same column: source ↔ source, target ↔ target.", "请在同一列交换：原色↔原色，目标色↔目标色。", "請在同一欄交換：原色↔原色，目標色↔目標色。", "同じ列で交換してください：元の色同士、目標色同士。");
            A("SwapDragging", "Row {0} · {1} · drop in the same column", "第 {0} 行{1} · 拖至同列色块", "第 {0} 列{1} · 拖至同欄色塊", "{0} 行目 · {1} · 同じ列にドロップ");
            A("SwapDrop", "Release to exchange rows {0} ↔ {1}", "松开交换第 {0} ↔ {1} 行", "放開交換第 {0} ↔ {1} 列", "離すと {0} ↔ {1} 行を交換");
            A("SwapDone", "Exchanged rows {0} ↔ {1}. Lightness locks still apply; Undo restores the exchange.", "已交换第 {0} ↔ {1} 行；各行明度锁定继续生效，可撤销。", "已交換第 {0} ↔ {1} 列；各列明度鎖定繼續生效，可復原。", "{0} ↔ {1} 行を交換しました。明度保持は有効です。元に戻せます。");
            A("Pick", "Pick", "吸管", "滴管", "スポイト");
            A("Delete", "Delete", "删除", "刪除", "削除");
            A("Lock", "Preserve OKLCH lightness", "保持 OKLCH 明度", "保持 OKLCH 明度", "OKLCH の明度を保持");
            A("Picker", "Color picker", "颜色选择", "色彩選擇", "カラーピッカー");
            A("PickerMode", "Mode", "选色方式", "選色方式", "色の指定");
            A("OK", "OK", "确定", "確定", "確定");
            A("Cancel", "Cancel", "取消", "取消", "キャンセル");
            A("InvalidColor", "Enter valid channel values, #RRGGBB or RGB values (0–255).", "请输入有效的通道数值、#RRGGBB 或 RGB 数值（0–255）。", "請輸入有效的通道數值、#RRGGBB 或 RGB 數值（0–255）。", "有効なチャンネル値、#RRGGBB または RGB 値（0–255）を入力してください。");
            A("GamutNote", "Out-of-gamut colors lose chroma while keeping L and hue. OKLCH lightness differs from the existing grayscale filter; later exposure and effects can still change lightness.", "超出色域时降低彩度并保留 L 与色相。OKLCH 明度与现有黑白滤镜不同；后续曝光和效果仍可改变明暗。", "超出色域時降低彩度並保留 L 與色相。OKLCH 明度與現有黑白濾鏡不同；後續曝光及效果仍可改變明暗。", "色域外の色は L と色相を保ち彩度を下げます。既存の白黒フィルターとは明度の定義が異なり、後段の露出や効果は明暗を変えます。");
            A("Reference", "Color transfer · from reference", "颜色迁移 · 从参考图生成", "色彩遷移 · 從參考圖產生", "色転送 · 参照画像から生成");
            A("ReferenceNote", "Extract the reference image's colors to create an editable target palette. With lightness locked, keep source lightness and transfer hue/chroma. Generated mappings work without the reference image.", "提取参考图的配色，自动创建可编辑的目标风格调色板。勾选保持明度时只迁移色相和彩度；生成后无需保留参考图片。", "提取參考圖的配色，自動建立可編輯的目標風格調色盤。勾選保持明度時只遷移色相和彩度；產生後無需保留參考圖片。", "参照画像の配色から編集可能な目標パレットを生成します。明度保持時は色相と彩度のみ転送し、生成後は参照画像がなくても使用できます。");
            A("Import", "Import reference…", "导入参考图…", "匯入參考圖…", "参照画像を読み込む…");
            A("FullImage", "Use whole image", "恢复全图", "恢復全圖", "画像全体を使用");
            A("CropNote", "Drag a rectangle to sample a subject. Importing or cropping does not apply changes until you choose Generate target palette.", "拖动框选取样区域；导入或框选后，点击「生成目标风格调色板」才会改变贴图。", "拖曳框選取樣區域；匯入或框選後，點選「產生目標風格調色盤」才會改變貼圖。", "ドラッグして抽出範囲を選択します。読み込みや範囲変更だけではテクスチャは変更されません。");
            A("NoReference", "Import a reference image first.", "请先导入参考图片。", "請先匯入參考圖片。", "先に参照画像を読み込んでください。");
            A("Generate", "Generate target palette", "生成目标风格调色板", "產生目標風格調色盤", "目標パレットを生成");
            A("NumberRange", "Enter an integer from {0} to {1}.", "请输入 {0}–{1} 的整数。", "請輸入 {0}–{1} 的整數。", "{0} から {1} の整数を入力してください。");
            A("PickHint", "Click the original texture to pick a color (UV lines are ignored). Esc cancels.", "点击贴图取原色（忽略 UV 线框），Esc 取消。", "點選貼圖取原色（忽略 UV 線框），Esc 取消。", "テクスチャをクリックして元の色を取得します（UV 線は無視）。Esc でキャンセル。");
            A("NoPixels", "No visible pixels in this sample region. Existing settings were kept.", "取样区域没有有效像素，已保留原设置。", "取樣區域沒有有效像素，已保留原設定。", "抽出範囲に有効な画素がありません。既存の設定は保持されます。");
            A("Candidate", "Reference preview updated. Choose Generate target palette to apply this style.", "参考范围已更新，点击「生成目标风格调色板」应用配色。", "參考範圍已更新，點選「產生目標風格調色盤」套用配色。", "参照範囲を更新しました。「目標パレットを生成」で配色を適用できます。");
            A("Working", "Analyzing colors…", "正在分析颜色…", "正在分析色彩…", "色を解析中…");
            A("Done", "Recoloring settings updated. You can undo this operation.", "重着色设置已更新，可撤销本次操作。", "重新著色設定已更新，可復原本次操作。", "色変換の設定を更新しました。元に戻す操作が可能です。");
            A("Failed", "Color processing failed: {0}", "颜色处理失败：{0}", "色彩處理失敗：{0}", "色処理に失敗しました：{0}");
            A("ImageFilter", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.tga;*.dds", "图像|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.tga;*.dds", "影像|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.tga;*.dds", "画像|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.tga;*.dds");
        }
        internal static string Manual(Lang language)
        {
            var text = new[]
            {
                "\n[Palette and color transfer]\nEnable or disable the palette without losing its settings. Detect 1–128 source colors (default 8) from the original texture using the grading scope selected on the left. Add manual source → target mappings with the eyedropper or color picker. Choose HSV (default), RGB or OKLCH; HEX/RGB entry is always available. Lightness lock is independent of the picker mode. Detection replaces automatic entries and keeps manual entries. Each row has a lightness lock. All colors within the grading scope participate in global RBF interpolation. The Gaussian width follows the mean distance between source colors; weights are calculated automatically, clamped only below zero and normalized. There are no manual strength or range controls, and neither width nor individual weight is capped at 0.5.\nColor transfer is a child section of Palette. Import a reference image, optionally drag a sample rectangle, then choose Generate target palette. Source and reference colors are paired by lightness rank to create editable mappings. Enable Preserve OKLCH lightness to keep source lightness while transferring hue/chroma; disable it to use reference lightness too. A reference count of 0 follows the source count. Generation keeps manual mappings and replaces automatic mappings.\nLightness is locked by default. Out-of-gamut colors lose chroma while retaining OKLCH L and hue. Subsequent exposure/effects may change lightness. A color dialog previews live; Cancel restores it and OK records one undo step. Presets store generated color mappings and need no reference file. Import again to choose a new reference region.\n",
                "\n【调色板与颜色迁移】\n调色板可启用或关闭，切换保留设置。按左侧调色范围，从原始贴图整图或 UV 选区自动识别 1–128 色（默认 8）；可用吸管或选色器添加手动原色→目标色映射。选色方式可切换 HSV（默认）、RGB、OKLCH，均支持 HEX/RGB 输入；明度锁定独立于选色方式。重新识别替换自动项并保留手动项；每行可设置明度锁定。当前调色范围内的所有颜色共同参与全局 RBF 插值；高斯核宽度由原色间的平均距离自动计算，权重截负后归一化。不设手动强度或影响范围，核宽度与单项权重均不限制在 0.5。\n「颜色迁移」位于调色板内：导入参考图后可拖动框选取样区域，点击「生成目标风格调色板」，按明度排序配对，自动创建可继续编辑的原色→目标色映射。勾选「保持 OKLCH 明度」时保留源色明度，仅迁移参考色的色相和彩度；取消勾选则也采用参考色明度。参考色数量 0 表示跟随原色数量。生成时替换自动项，保留手动项。\n默认保持 OKLCH 明度，超出色域时降低彩度并保留 L 与色相；后续曝光与效果仍可改变明暗。选色对话框实时预览，取消恢复，确定仅记录一步撤销。预设保存生成的颜色映射，不依赖参考文件；重新框选需再次导入参考图。\n",
                "\n【調色盤與色彩遷移】\n調色盤可啟用或關閉，切換保留設定。依左側調色範圍，從原始貼圖整圖或 UV 選區自動辨識 1–128 色（預設 8）；可用滴管或選色器新增手動原色→目標色對應。選色方式可切換 HSV（預設）、RGB、OKLCH，皆支援 HEX/RGB 輸入；明度鎖定獨立於選色方式。重新辨識替換自動項並保留手動項；每行可設定明度鎖定。目前調色範圍內的所有色彩共同參與全域 RBF 插值；高斯核寬度依原色間的平均距離自動計算，權重截去負值後正規化。不設手動強度或影響範圍，核寬度與單項權重皆不限制在 0.5。\n「色彩遷移」位於調色盤內：匯入參考圖後可拖曳框選取樣區域，點選「產生目標風格調色盤」，按明度排序配對，自動建立可繼續編輯的原色→目標色對應。勾選「保持 OKLCH 明度」時保留來源明度，僅遷移參考色的色相及彩度；取消勾選則亦採用參考色明度。參考色數量 0 表示跟隨原色數量。產生時替換自動項，保留手動項。\n預設保持 OKLCH 明度，超出色域時降低彩度並保留 L 與色相；後續曝光及效果仍可改變明暗。選色對話框即時預覽，取消恢復，確定僅記錄一步復原。預設集儲存產生的色彩對應，不依賴參考檔案；重新框選需再次匯入參考圖。\n",
                "\n【パレットと色転送】\nパレットの有効・無効を切り替えても設定は保持されます。左の調色範囲に従って元画像全体または UV 選択範囲から 1–128 色（既定 8）を抽出します。スポイトやピッカーで手動対応を追加できます。HSV（既定）、RGB、OKLCH を選択でき、HEX/RGB 入力は常に使用可能です。明度保持は選色方式とは独立した設定です。再抽出は自動項目だけを置き換えます。各項目で明度保持を設定できます。調色範囲内のすべての色を全域 RBF 補間で変換します。ガウス核の幅は元の色の平均距離から自動計算し、負の重みをゼロにして正規化します。手動の強度や影響範囲はなく、核幅や個別の重みを 0.5 に制限しません。\n色転送はパレットの子項目です。参照画像を読み込み、必要なら範囲をドラッグし、「目標パレットを生成」で明度順に対応付けた編集可能なパレットを作成します。明度保持を有効にすると元の明度を保って色相と彩度を転送し、無効にすると参照色の明度も使用します。参照色数 0 は元の色数に従います。生成時は自動項目を置き換え、手動項目を保持します。\n既定では OKLCH 明度を保持します。色域外では L と色相を保って彩度を下げます。後段の露出や効果は明暗を変えます。ピッカーは即時プレビューし、キャンセルで復元、確定で一回の履歴を記録します。生成した色の対応をプリセットに保存するため参照ファイルは不要です。範囲を選び直す場合は再読込してください。\n"
            };
            var automatic = new[]
            {
                "\nAutomatic count can be enabled independently for the source and reference. It estimates 1–128 major color groups using perceptual separation and alpha-weighted area, ignoring subtle gradients and isolated noise; this is not object segmentation. Detect/generate updates the displayed counts. Disable the option to enter counts manually. Matching considers relative lightness and color-block area while preserving dark-to-light order. With enough source colors, every reference color is represented; with fewer colors, intermediate reference colors compete by lightness and area.\nReference-generated mappings also rotate/scale chromatic differences between control colors to reduce residual source hues. Lightness lock still preserves each pixel's floating-point OKLCH L; 8-bit output introduces quantization error, particularly near black. Previously saved mappings retain their old interpolation; regenerate from the reference to use the corrected transfer. Auto options, results and interpolation are included in presets, material memory and undo/redo.\n",
                "\n原图和参考图可分别勾选「自动数量 · 按主色块估计」。按感知色差与 Alpha 加权面积估计 1–128 个主色，忽略细微渐变和零星噪点；这不是物体语义分割。点击识别/生成后更新数量，取消勾选可手动指定。配对兼顾相对明度和色块面积占比，并保持从暗到亮的顺序。原图控制色足够时覆盖全部参考色；数量不足时按明度与占比选择中间色。\n参考图生成的映射会同时旋转、缩放控制色之间的色彩差异，减少原图底色残留。明度锁定仍保留每个像素的浮点 OKLCH L；8 位输出存在量化误差，尤其是接近黑色时。旧预设维持原有插值方式，需重新从参考图生成以使用修正后的迁移。自动开关、数量与迁移方式均支持预设、分材质记忆及撤销/重做。\n",
                "\n原圖與參考圖可分別勾選「自動數量 · 依主色塊估計」。依感知色差與 Alpha 加權面積估計 1–128 個主色，忽略細微漸層和零星雜訊；這不是物體語意分割。點選辨識/產生後更新數量，取消勾選可手動指定。對應兼顧相對明度和色塊面積占比，並保持由暗到亮的順序。原圖控制色足夠時涵蓋全部參考色；數量不足時依明度與占比選擇中間色。\n參考圖產生的對應也會旋轉、縮放控制色之間的色彩差異，減少原圖底色殘留。明度鎖定仍保留每個像素的浮點 OKLCH L；8 位元輸出存在量化誤差，尤其是接近黑色時。舊預設集維持原有插值方式，需重新從參考圖產生以使用修正後的遷移。自動開關、數量與遷移方式皆支援預設集、分材質記憶及復原/重做。\n",
                "\n元画像と参照画像の色数を個別に自動推定できます。知覚的な色差と Alpha 加重面積から 1–128 の主要色群を推定し、微細な階調差や孤立したノイズを無視します。物体の意味的な領域分割ではありません。抽出・生成後に色数を更新し、オフにすると手動で指定できます。対応付けは相対明度と面積比を考慮し、暗部から明部の順序を保ちます。元の色数が十分なら全参照色を使用し、少なければ明度と面積から中間色を選びます。\n参照画像から生成する対応では制御色間の色差も回転・拡縮し、元の色相の残留を減らします。明度保持時は各画素の浮動小数点 OKLCH L を保持します。8 ビット出力には量子化誤差があり、特に黒付近で大きくなります。既存プリセットの補間方法は変更しません。修正版を使うには参照画像から再生成してください。自動設定、色数、補間方法はプリセット、材質別記憶、履歴に保存されます。\n"
            };
            var exchange = new[]
            {
                "\n[Exchange palette colors]\nDrag source colors onto source colors, or target colors onto target colors. Row numbers, origin/drop highlights and edge scrolling help locate distant rows. Alternatively enable Click to exchange or right-click a swatch to start: select a color, scroll freely, then click another in the same column. The pinned status and Cancel button remain visible. Esc cancels; ordinary clicks still edit when click exchange is off. Row enable/manual flags, lightness locks and interpolation modes stay in place. Targets obey destination lightness/gamut constraints; the stored color is not repeatedly quantized. Dragging only updates feedback; dropping updates two rows and queues one asynchronous preview. Each exchange is one undo step and persists in material settings and presets.\n",
                "\n【交换调色板颜色】\n原色与原色、目标色与目标色可拖动交换，另一列保持原位。行号、起点/落点高亮与边缘自动滚动辅助定位。远距离可勾选「点击交换」，或右键色块发起交换：选中后自由滚动，再点同列另一色块。顶部始终显示待交换信息和取消按钮，Esc 可取消；关闭点击交换后单击仍打开选色器。各行启用、手动标记、明度锁定及插值方式保持原位，目标色按所在行的明度与色域约束显示，反复交换不重复量化保存值。拖动只更新提示，放下后只更新两行并异步预览；一次交换可一步撤销，支持分材质记忆和预设。\n",
                "\n【交換調色盤色彩】\n原色與原色、目標色與目標色可拖曳交換，另一欄保持原位。列號、起點/落點醒目提示與邊緣自動捲動輔助定位。遠距離可勾選「點選交換」或右鍵發起交換，自由捲動後再點同欄另一色塊。頂部顯示待交換資訊及取消按鈕，Esc 可取消；關閉點選交換後仍可點選開啟選色器。各列啟用、手動標記、明度鎖定與插值方式保持原位，目標色依所在列的明度及色域約束顯示，重複交換不累積量化誤差。拖曳只更新提示，放開後更新兩列並非同步預覽；可一步復原，支援分材質記憶及預設集。\n",
                "\n[パレット色の交換]\n同じ列の色同士をドラッグで交換し、もう一方の列は保持します。行番号、開始色・交換先の強調表示、端での自動スクロールで位置を確認できます。離れた色は「クリックで交換」または右クリックから開始し、スクロール後に同じ列の相手を選択します。上部の状態表示とキャンセル、Esc を利用できます。クリック交換をオフにすると通常のピッカー編集に戻ります。各行の有効・手動フラグ、明度保持、補間方式は移動せず、目標色に交換先の明度・色域制約を適用します。反復交換で保存色を再量子化しません。ドラッグ中は表示だけを更新し、離すと二行を更新して非同期プレビューします。一回の交換は一回で元に戻せ、材質別設定とプリセットにも保存されます。\n"
            };
            return text[(int)language] + automatic[(int)language] + exchange[(int)language];
        }
    }
}
