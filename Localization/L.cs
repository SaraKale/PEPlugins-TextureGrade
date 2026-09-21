using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TextureGrade.Localization
{
    /// <summary>界面语言。数组下标即词条表的列序，不要随意改数字。</summary>
    public enum Lang
    {
        En = 0,     // English
        ZhCn = 1,   // 简体中文
        ZhTw = 2,   // 繁體中文
        Ja = 3      // 日本語
    }

    /// <summary>
    /// 极简多语言：一张「键 -> 四种语言」的表 + 当前语言。
    /// 词条缺失时按「当前语言 -> 英语 -> 键名本身」回退，永远不会显示空白。
    /// 语言选择存在插件同级目录的 lang.txt（不可写时回退 %APPDATA%\TextureGrade）。
    /// </summary>
    public static class L
    {
        private const string LangFileName = "lang.txt";

        private static readonly Dictionary<string, string[]> Tbl = BuildTable();
        private static Lang _current = LoadSaved();

        public static Lang Current => _current;

        /// <summary>取词条；{0}{1} 等占位符不在这里处理（请用 F）。</summary>
        public static string T(string key)
        {
            if (key != null && Tbl.TryGetValue(key, out var row))
            {
                var s = row[(int)_current];
                if (!string.IsNullOrEmpty(s)) return s;
                if (!string.IsNullOrEmpty(row[(int)Lang.En])) return row[(int)Lang.En];
            }
            return key ?? "";
        }

        /// <summary>取词条并按当前语言格式化占位符。</summary>
        public static string F(string key, params object[] args)
        {
            var s = T(key);
            try { return string.Format(CultureInfo.CurrentCulture, s, args); }
            catch { return s; }
        }

        public static string Code(Lang l)
        {
            switch (l)
            {
                case Lang.ZhCn: return "zh-CN";
                case Lang.ZhTw: return "zh-TW";
                case Lang.Ja: return "ja";
                default: return "en";
            }
        }

        public static Lang ParseCode(string code)
        {
            if (!string.IsNullOrEmpty(code))
            {
                string c = code.Trim().ToLowerInvariant();
                if (c.StartsWith("zh")) return c.Contains("hant") || c.Contains("tw") || c.Contains("hk") || c.Contains("mo")
                    ? Lang.ZhTw : Lang.ZhCn;
                if (c.StartsWith("ja") || c.StartsWith("jp")) return Lang.Ja;
                if (c.StartsWith("en")) return Lang.En;
            }
            return Lang.En;
        }

        public static void Set(Lang l)
        {
            _current = l;
            Save();
        }

        public static void Save()
        {
            try { File.WriteAllText(System.IO.Path.Combine(Models.AppPaths.Dir, LangFileName), Code(_current)); }
            catch
            {
                try { File.WriteAllText(System.IO.Path.Combine(Models.AppPaths.FallbackDir, LangFileName), Code(_current)); }
                catch { /* 存不下就算了，下次启动用默认语言 */ }
            }
        }

        /// <summary>启动时：先读 lang.txt，没有就按系统 UI 语言猜一个。</summary>
        private static Lang LoadSaved()
        {
            try
            {
                foreach (var dir in new[] { Models.AppPaths.Dir, Models.AppPaths.FallbackDir })
                {
                    var p = System.IO.Path.Combine(dir, LangFileName);
                    if (File.Exists(p)) return ParseCode(File.ReadAllText(p));
                }
            }
            catch { /* 忽略，走系统语言推断 */ }

            return GuessFromSystem();
        }

        private static Lang GuessFromSystem()
        {
            try
            {
                string n = CultureInfo.CurrentUICulture.Name.ToLowerInvariant();
                if (n.StartsWith("ja")) return Lang.Ja;
                if (n.StartsWith("zh"))
                    return (n.Contains("hant") || n.Contains("tw") || n.Contains("hk") || n.Contains("mo"))
                        ? Lang.ZhTw : Lang.ZhCn;
            }
            catch { /* 忽略 */ }
            return Lang.En;
        }

        // =====================================================================
        // 词条表：Add(键, English, 简体中文, 繁體中文, 日本語)
        // =====================================================================
        private static Dictionary<string, string[]> BuildTable()
        {
            var d = new Dictionary<string, string[]>();
            Action<string, string, string, string, string> add =
                (k, en, zh, tw, ja) => d[k] = new[] { en, zh, tw, ja };

            // ---------------- 窗口 / 菜单 ----------------
            add("App.Title", "Texture Grade — Texture Grading", "Texture Grade — 贴图调色", "Texture Grade — 貼圖調色", "Texture Grade — テクスチャ調整");
            add("Menu.File", "File(&F)", "文件(&F)", "檔案(&F)", "ファイル(&F)");
            add("Menu.Edit", "Edit(&E)", "编辑(&E)", "編輯(&E)", "編集(&E)");
            add("Menu.View", "View(&V)", "视图(&V)", "檢視(&V)", "表示(&V)");
            add("Menu.Language", "Language(&L)", "语言(&L)", "語言(&L)", "言語(&L)");
            add("Menu.Help", "Help(&H)", "帮助(&H)", "說明(&H)", "ヘルプ(&H)");

            add("Lang.En", "English", "English", "English", "English");
            add("Lang.ZhCn", "Simplified Chinese", "简体中文", "简体中文", "簡体中国語");
            add("Lang.ZhTw", "Traditional Chinese", "繁體中文", "繁體中文", "繁体中国語");
            add("Lang.Ja", "Japanese", "日本語", "日本語", "日本語");

            add("File.ReRead", "Re-read", "重新读取", "重新讀取", "再読み込み");
            add("File.RefreshModel", "Refresh Model", "刷新模型", "刷新模型", "モデルを更新");
            add("File.SaveNew", "Save as New Texture", "另存为新贴图", "另存為新貼圖", "新規テクスチャに保存");
            add("File.ExportUvLayout", "Export UV Layout…", "导出 UV 布局图…", "匯出 UV 佈局圖…", "UV レイアウトを書き出し…");
            add("File.ExportSelectionMask", "Export Selection Mask… (white = selected)", "导出当前选区蒙版…（白=选中）", "匯出目前選取遮罩…（白=選取）", "選択範囲マスクを書き出し…（白=選択）");
            add("File.ExportMaterialMask", "Export Current Material Mask…", "导出当前材质蒙版…", "匯出目前材質遮罩…", "現在の材質マスクを書き出し…");
            add("File.ExportAllMasks", "Export All Material Masks… (one per material)", "导出全部材质蒙版…（每材质一张）", "匯出全部材質遮罩…（每材質一張）", "全材質マスクを書き出し…（材質ごとに1枚）");
            add("File.OpenPresetFolder", "Open Preset Folder", "打开预设文件夹", "開啟預設資料夾", "プリセットフォルダを開く");
            add("File.Revert", "Revert This Material's Texture", "还原该材质贴图", "還原該材質貼圖", "この材質のテクスチャを元に戻す");
            add("File.ResetParams", "Reset Parameters", "重置参数", "重設參數", "パラメータをリセット");
            add("File.Undo", "Undo", "撤销", "復原", "元に戻す");
            add("File.Redo", "Redo", "重做", "重做", "やり直し");
            add("File.Exit", "Exit", "退出", "結束", "終了");

            add("Edit.ShowUV", "Show UV Layout", "显示 UV 布局", "顯示 UV 佈局", "UV レイアウトを表示");
            add("Edit.ShowVertices", "Show UV Vertex Marks", "显示 UV 顶点记号", "顯示 UV 頂點記號", "UV 頂点マークを表示");
            add("Edit.UvColor", "Change UV Layout Color", "修改 UV 布局颜色", "修改 UV 佈局顏色", "UV レイアウトの色を変更");
            add("Edit.UvCustom", "Custom Color…", "自定义颜色…", "自訂顏色…", "カスタムカラー…");
            add("Edit.UvBlue", "Blue (default)", "蓝色（默认）", "藍色（預設）", "青（既定）");
            add("Edit.UvRed", "Red", "红色", "紅色", "赤");
            add("Edit.UvGreen", "Green", "绿色", "綠色", "緑");
            add("Edit.UvPurple", "Purple", "紫色", "紫色", "紫");
            add("Edit.UvWhite", "White", "白色", "白色", "白");
            add("Edit.UvBlack", "Black", "黑色", "黑色", "黒");
            add("Edit.ModeSelect", "Select Mode (drag = box select)", "选面模式（拖动框选）", "選面模式（拖曳框選）", "面選択モード（ドラッグで矩形選択）");
            add("Edit.ModePan", "Pan Mode (drag = pan)", "平移模式（拖动平移）", "平移模式（拖曳平移）", "平移モード（ドラッグで移動）");
            add("Edit.SelectIsland", "Select Connected UV Island (same as double-click)", "选连通 UV 块（等同双击）", "選取連通 UV 塊（等同雙擊）", "連結 UV アイランドを選択（ダブルクリックと同じ）");
            add("Edit.SelectAll", "Select All UV Triangles", "全选 UV 三角面", "全選 UV 三角面", "全ての UV 三角面を選択");
            add("Edit.ClearSel", "Clear UV Selection", "清除 UV 选区", "清除 UV 選取", "UV 選択を解除");

            add("View.Fit", "Fit to Window", "适应窗口", "適應視窗", "ウィンドウに合わせる");
            add("View.Actual", "Actual Size (100%)", "实际大小（100%）", "實際大小（100%）", "等倍（100%）");
            add("View.ZoomIn", "Zoom In", "放大", "放大", "拡大");
            add("View.ZoomOut", "Zoom Out", "缩小", "縮小", "縮小");
            add("View.Grid", "Show Grid", "显示网格", "顯示格線", "グリッドを表示");
            add("View.Histogram", "Show Histogram", "显示直方图", "顯示直方圖", "ヒストグラムを表示");
            add("View.Compare", "Compare with Original", "对比原图", "對比原圖", "元画像と比較");

            add("Help.Operation", "Operation Manual", "操作说明", "操作說明", "操作説明");
            add("Help.About", "About", "关于", "關於", "バージョン情報");
            add("Help.Reload", "Reload", "重新载入", "重新載入", "再読み込み");
            add("Help.OpenFolder", "Open Folder", "打开文件夹", "開啟資料夾", "フォルダを開く");
            add("Help.Close", "Close", "关闭", "關閉", "閉じる");
            add("Help.Title", "Operation Manual", "操作说明", "操作說明", "操作説明");
            add("Help.MissingFmt",
                "Operation manual not found:\n{0}\n\na default one has been created there — edit it freely.",
                "未找到操作说明文件：\n{0}\n\n已在该位置生成一份默认说明，可直接编辑。",
                "未找到操作說明檔案：\n{0}\n\n已在該位置產生一份預設說明，可直接編輯。",
                "操作説明ファイルが見つかりません：\n{0}\n\n既定の説明をそこに作成しました。自由に編集できます。");
            add("Help.LoadFailFmt", "Failed to read the manual file:\n{0}\n\n{1}", "读取操作说明失败：\n{0}\n\n{1}", "讀取操作說明失敗：\n{0}\n\n{1}", "操作説明の読み込みに失敗：\n{0}\n\n{1}");
            add("Help.SourceFmt", "Source: {0}", "文件：{0}", "檔案：{0}", "ファイル：{0}");

            add("About.Title", "About Texture Grade", "关于 Texture Grade", "關於 Texture Grade", "Texture Grade について");
            add("About.Body",
                "Texture Grade — PMX texture grading plug-in\nNon-destructive grading like Lightroom / Camera Raw.\n\nSupported formats: PNG / JPG / BMP / GIF / TIFF / TGA / DDS (DXT1,3,5)\n\nversion: 1.0.3\nby: SaraKale",
                "Texture Grade — PMX 贴图调色插件\n仿 Lightroom / Camera Raw 的非破坏式贴图调色。\n\n贴图格式支持：PNG / JPG / BMP / GIF / TIFF / TGA / DDS(DXT1,3,5)\n\nversion:1.0.3\nby:SaraKale",
                "Texture Grade — PMX 貼圖調色外掛\n仿 Lightroom / Camera Raw 的非破壞式貼圖調色。\n\n貼圖格式支援：PNG / JPG / BMP / GIF / TIFF / TGA / DDS(DXT1,3,5)\n\nversion:1.0.3\nby:SaraKale",
                "Texture Grade — PMX テクスチャ調整プラグイン\nLightroom / Camera Raw 風の非破壊調色。\n\n対応形式：PNG / JPG / BMP / GIF / TIFF / TGA / DDS(DXT1,3,5)\n\nversion:1.0.3\nby:SaraKale");

            // ---------------- 主界面固定文案 ----------------
            add("Title.Preview", "Texture + UV Preview", "贴图 + UV 预览", "貼圖 + UV 預覽", "テクスチャ + UV プレビュー");
            add("Title.Materials", "Materials", "材质列表", "材質清單", "材質一覧");
            add("Title.Adjust", "Adjust", "调整", "調整", "調整");
            add("Hist.Label", "Histogram (RGB overlaid)", "直方图（RGB 叠加）", "直方圖（RGB 疊加）", "ヒストグラム（RGB 重ね合わせ）");
            add("Badge.Modified", "Modified", "已修改", "已修改", "変更済み");

            add("Btn.ReRead", "Re-read", "重新读取", "重新讀取", "再読み込み");
            add("Btn.RefreshModel", "Refresh Model", "刷新模型", "刷新模型", "モデルを更新");
            add("Btn.SaveNew", "Save as New Texture", "另存为新贴图", "另存為新貼圖", "新規テクスチャに保存");
            add("Btn.Revert", "Revert", "还原", "還原", "元に戻す");
            add("Btn.ResetParams", "Reset Parameters", "重置参数", "重設參數", "パラメータリセット");
            add("Btn.Undo", "Undo", "撤销", "復原", "元に戻す");
            add("Btn.Redo", "Redo", "重做", "重做", "やり直し");
            add("Btn.Fit", "Fit", "适配", "適配", "フィット");
            add("Btn.Actual", "100%", "100%", "100%", "100%");
            add("Tools.View", "View", "视图", "檢視", "表示");
            add("Tools.Selection", "Selection", "选择", "選取", "選択");
            add("Tools.Vertices", "Vertices", "顶点同步", "頂點同步", "頂点同期");
            add("Tools.Source", "Model textures", "模型贴图", "模型貼圖", "モデルのテクスチャ");
            add("Tools.Output", "Apply / Save", "应用与保存", "套用與儲存", "反映・保存");
            add("Tools.History", "Grading history", "调色历史", "調色歷程", "調整履歴");
            add("Scope.Title", "Grading scope", "调色范围", "調色範圍", "調整範囲");
            add("Scope.Selection", "UV selection first", "UV 选区优先", "UV 選取優先", "UV 選択を優先");
            add("Scope.Whole", "Whole texture", "整张贴图", "整張貼圖", "テクスチャ全体");
            add("Scope.SelectionNote", "Grading the {0} selected UV faces. Switching to Whole texture includes pixels outside this selection.", "当前仅调整所选 {0} 个 UV 面；切换整张贴图会包含选区外像素。", "目前僅調整所選 {0} 個 UV 面；切換整張貼圖會包含選取外像素。", "選択した {0} UV 面のみ調整します。全体に切り替えると選択外の画素も含みます。");
            add("Scope.WholeNote", "Whole texture: includes pixels outside this material's selection. {0} materials use this texture; other materials' UVs are gray. Refresh/Save only updates the current material's texture reference.", "整图调色也会改变本材质选区外像素。此贴图共供 {0} 个材质使用，其他材质 UV 显示为灰线；刷新／另存仅更新当前材质的贴图引用。", "整圖調色也會改變本材質選取外像素。此貼圖共供 {0} 個材質使用，其他材質 UV 顯示為灰線；更新／另存僅更新目前材質的貼圖參照。", "この材質の選択外画素も調整します。{0} 材質が使用し、他材質の UV は灰色で表示します。更新・保存で参照先が変わるのは現在の材質のみです。");
            add("Scope.NoSelectionNote", "No UV selection: grading the whole image, including other material regions. {0} materials use this texture. Refresh/Save only updates the current material's texture reference.", "尚未选择 UV：当前会调整整图，包含其他材质可能使用的区域（共 {0} 个材质使用此贴图）。刷新／另存仅更新当前材质的贴图引用。", "尚未選取 UV：目前會調整整圖，包含其他材質可能使用的區域（共 {0} 個材質使用此貼圖）。更新／另存僅更新目前材質的貼圖參照。", "UV 未選択のため他材質の領域を含む画像全体を調整します（{0} 材質が使用）。更新・保存で参照先が変わるのは現在の材質のみです。");
            add("Scope.ContextFailed", "Other material UVs could not be displayed: {0}", "其他材质 UV 显示失败：{0}", "其他材質 UV 顯示失敗：{0}", "他材質の UV を表示できません：{0}");
            add("Btn.Grid", "Grid", "网格", "格線", "グリッド");
            add("Btn.HideUV", "Hide UV", "隐藏 UV", "隱藏 UV", "UV を隠す");
            add("Btn.ShowUV", "Show UV", "显示 UV", "顯示 UV", "UV を表示");
            add("Tip.ToggleUV", "Show or hide UV lines and vertex markers. Selection and grading stay unchanged.", "显示或隐藏 UV 线框及顶点标记，不改变选区和调色结果。", "顯示或隱藏 UV 線框及頂點標記，不改變選取範圍和調色結果。", "UV 線と頂点マーカーの表示を切り替えます。選択範囲と調色結果は変わりません。");
            add("Btn.ModeToPan", "Pan", "平移", "平移", "平移");
            add("Btn.ModeToSelect", "Select", "选面", "選面", "選択");
            add("Btn.Island", "Select Island", "选连通块", "選連通塊", "アイランド選択");
            add("Btn.ClearSel", "Clear Selection", "清空选区", "清除選取", "選択解除");
            add("Btn.Compare", "Compare Original", "对比原图", "對比原圖", "元画像比較");
            add("Btn.RecvVerts", "From Model", "从模型接收", "從模型接收", "モデルから受信");
            add("Btn.SendVerts", "To Model", "发送到模型", "傳送到模型", "モデルへ送信");
            add("Tip.RecvVerts", "Read the vertices currently selected in the PMXEditor 3D view and mark them red here (only those used by this material)",
                                "读取 PMXEditor 3D 视图当前选中的顶点，在本面板标红（只保留本材质用到的）",
                                "讀取 PMXEditor 3D 視圖目前選取的頂點，在本面板標紅（只保留本材質用到的）",
                                "PMXエディタの3Dビューで選択中の頂点を読み込み、ここで赤く表示（この材質で使われる分のみ）");
            add("Tip.SendVerts", "Send the red-marked vertices (face corners + received ones) back to the PMXEditor 3D view as its selection",
                                 "把本面板标红的顶点（选中面的顶点 + 接收来的）发送给 PMXEditor 3D 视图作为选中顶点",
                                 "把本面板標紅的頂點（選取面的頂點 + 接收來的）傳送給 PMXEditor 3D 視圖作為選取頂點",
                                 "赤表示中の頂点（選択面の頂点＋受信分）を3Dビューの選択頂点として送信");

            add("Hint.PickMaterial", "Please select a material with a texture.", "请选择一个带贴图的材质。", "請選擇一個帶貼圖的材質。", "テクスチャ付きの材質を選択してください。");
            add("Hint.Zoom", "Zoom {0:F0}%", "缩放 {0:F0}%", "縮放 {0:F0}%", "ズーム {0:F0}%");
            add("Hint.SelCount", "{0} faces selected", "已选 {0} 个面", "已選 {0} 個面", "{0} 面を選択中");
            add("Hint.SelNone", "no selection", "未选区域", "未選區域", "未選択");
            add("Hint.Wheel", "wheel = zoom", "滚轮缩放", "滾輪縮放", "ホイールでズーム");
            add("Hint.ModeSelect", "Select mode (click / double-click = island, repeatable / drag = box)", "选面模式（单击点选 / 双击=加选整块，可连续双击选多块 / 拖动框选）", "選面模式（單擊點選 / 雙擊=加選整塊，可連續雙擊選多塊 / 拖曳框選）", "選択モード（クリック / ダブルクリック=アイランド追加、連続で複数可 / ドラッグ=矩形）");
            add("Hint.ModePan", "Pan mode (drag to pan)", "平移模式（左键拖动平移）", "平移模式（左鍵拖曳平移）", "平移モード（ドラッグで移動）");
            add("Hint.RightPan", "right-drag = pan anytime", "右键随时平移", "右鍵隨時平移", "右ドラッグでいつでも平移");
            add("Hint.Comparing", "   ·   [Comparing original]", "   ·   【对比原图】", "   ·   【對比原圖】", "   ·   【元画像と比較】");

            add("Readout.Empty", "(U: —, V: —)", "(U: —, V: —)", "(U: —, V: —)", "(U: —, V: —)");
            add("Readout.Fmt", "(U: {0:F3}, V: {1:F3})   ·   Pixel ({2}, {3})",
                               "(U: {0:F3}, V: {1:F3})   ·   像素 ({2}, {3})",
                               "(U: {0:F3}, V: {1:F3})   ·   像素 ({2}, {3})",
                               "(U: {0:F3}, V: {1:F3})   ·   ピクセル ({2}, {3})");

            // ---------------- 折叠组标题 ----------------
            add("Grp.Preset", "Presets / Favorites", "预设 / 收藏", "預設 / 收藏", "プリセット / お気に入り");
            add("Grp.Basic", "Basic", "基本", "基本", "基本");
            add("Grp.Color", "Color", "色彩", "色彩", "カラー");
            add("Grp.Hsl", "HSL per Channel (8 colors)", "HSL 分通道（8 色）", "HSL 分通道（8 色）", "HSL チャンネル別（8 色）");
            add("Grp.Curve", "Curves / Levels / RGB / HSV", "曲线 / 色阶 / RGB / HSV", "曲線 / 色階 / RGB / HSV", "カーブ / レベル / RGB / HSV");
            add("Grp.Detail", "Detail", "细节", "細節", "ディテール");
            add("Grp.Fx", "Effects", "效果", "效果", "エフェクト");

            // ---------------- 滑块标签（键与 GradeSettings 的键同名） ----------------
            add("Exposure", "Exposure", "曝光", "曝光", "露出");
            add("Contrast", "Contrast", "对比度", "對比度", "コントラスト");
            add("Highlights", "Highlights", "高光", "高光", "ハイライト");
            add("Shadows", "Shadows", "阴影", "陰影", "シャドウ");
            add("Whites", "Whites", "白色", "白色", "白レベル");
            add("Blacks", "Blacks", "黑色", "黑色", "黒レベル");
            add("Temperature", "Temperature", "色温", "色溫", "色温度");
            add("Tint", "Tint", "色调", "色調", "色かぶり");
            add("Vibrance", "Vibrance", "自然饱和度", "自然飽和度", "自然な彩度");
            add("Saturation", "Saturation", "饱和度", "飽和度", "彩度");
            add("Hue", "Hue", "色相", "色相", "色相");
            add("ColorBalanceR", "Color Balance R", "色彩平衡 R", "色彩平衡 R", "カラーバランス R");
            add("ColorBalanceG", "Color Balance G", "色彩平衡 G", "色彩平衡 G", "カラーバランス G");
            add("ColorBalanceB", "Color Balance B", "色彩平衡 B", "色彩平衡 B", "カラーバランス B");
            add("Hsl.Hue", "Hue", "色相", "色相", "色相");
            add("Hsl.Sat", "Saturation", "饱和度", "飽和度", "彩度");
            add("Hsl.Lum", "Luminance", "明度", "明度", "明度");
            add("Curve", "Curve", "曲线", "曲線", "カーブ");
            add("LevelsBlack", "Levels · Black", "色阶·黑点", "色階·黑點", "レベル·黒点");
            add("LevelsWhite", "Levels · White", "色阶·白点", "色階·白點", "レベル·白点");
            add("LevelsGamma", "Levels · Gamma", "色阶·灰阶", "色階·灰階", "レベル·ガンマ");
            add("RgbR", "RGB · R", "RGB·R", "RGB·R", "RGB·R");
            add("RgbG", "RGB · G", "RGB·G", "RGB·G", "RGB·G");
            add("RgbB", "RGB · B", "RGB·B", "RGB·B", "RGB·B");
            add("HsvValue", "HSV · Value", "HSV·明度", "HSV·明度", "HSV·明度");
            add("Clarity", "Clarity", "清晰度", "清晰度", "クラリティ");
            add("Sharpen", "Sharpen", "锐化", "銳化", "シャープ");
            add("Grayscale", "Grayscale", "黑白化", "黑白化", "白黒化");
            add("Invert", "Invert", "反相", "反相", "反転");
            add("Threshold", "Threshold mix", "阈值强度", "閾值強度", "2 値化の強度");
            add("ThresholdLevel", "Threshold level", "黑白分界值", "黑白分界值", "2 値化の境界値");
            add("Fx.ThresholdTip", "0% disables thresholding; 100% produces solid black/white regions. Intermediate values blend with the original colors.",
                "0% 关闭阈值效果；100% 得到纯黑白块，中间值与原颜色混合。", "0% 關閉閾值效果；100% 得到純黑白塊，中間值與原色混合。", "0% で無効、100% で白黒の領域に変換します。中間値では元の色と混合します。");
            add("Fx.ThresholdLevelTip", "Brightness below this value becomes black; at or above it becomes white. Range 0–255, default 127.5. Higher values expand black regions. Type a value and press Enter, or drag the slider.",
                "亮度低于此值变黑，达到此值变白；范围 0–255，默认 127.5。值越大，黑色区域越多。可输入数值后按 Enter，或拖动滑块。", "亮度低於此值變黑，達到此值變白；範圍 0–255，預設 127.5。值越大，黑色區域越多。可輸入數值後按 Enter，或拖曳滑桿。", "この値未満は黒、以上は白になります。範囲は 0–255、既定は 127.5。大きくすると黒い領域が増えます。数値入力後 Enter、またはスライダーで調整できます。");
            add("Fx.GrayscaleTip", "Blends colors toward their weighted brightness while preserving continuous gray tones. 0 keeps the original colors; 100 produces a grayscale image.",
                "按加权亮度将颜色转为连续灰阶，滑块控制黑白化强度。0 保留原色，100 完全黑白化。", "依加權亮度將色彩轉為連續灰階，滑桿控制黑白化強度。0 保留原色，100 完全黑白化。", "色を加重した明るさに変換し、連続した階調を保ちます。0 は元の色を保持、100 は完全な白黒化です。");
            add("Fx.InvalidThreshold", "Enter a threshold level from 0 to 255.", "请输入 0–255 的黑白分界值。", "請輸入 0–255 的黑白分界值。", "0–255 の境界値を入力してください。");

            // HSL 分通道的小节标题
            add("Band.Red", "Red", "红 Red", "紅 Red", "赤 Red");
            add("Band.Orange", "Orange", "橙 Orange", "橙 Orange", "橙 Orange");
            add("Band.Yellow", "Yellow", "黄 Yellow", "黃 Yellow", "黄 Yellow");
            add("Band.Green", "Green", "绿 Green", "綠 Green", "緑 Green");
            add("Band.Aqua", "Aqua", "青 Aqua", "青 Aqua", "水 Aqua");
            add("Band.Blue", "Blue", "蓝 Blue", "藍 Blue", "青 Blue");
            add("Band.Purple", "Purple", "紫 Purple", "紫 Purple", "紫 Purple");
            add("Band.Magenta", "Magenta", "品红 Magenta", "品紅 Magenta", "マゼンタ Magenta");

            // ---------------- 预设组 ----------------
            add("Preset.NameTip", "Type a name and click Save; leave empty to use \"material recipe\" automatically",
                                 "输入预设名称后点「保存」；留空则自动用「材质名 配方」",
                                 "輸入預設名稱後點「保存」；留空則自動用「材質名 配方」",
                                 "プリセット名を入力して「保存」をクリック。空欄なら「材質名 配方」を自動使用");
            add("Preset.Save", "Save", "保存", "保存", "保存");
            add("Preset.Apply", "Apply", "应用", "應用", "適用");
            add("Preset.Delete", "Delete", "删除", "刪除", "削除");
            add("Preset.Refresh", "Refresh", "刷新", "刷新", "更新");
            add("Preset.OpenFolder", "Open Folder", "打开文件夹", "開啟資料夾", "フォルダを開く");
            add("Preset.Hint", "Presets are stored as JSON in the presets folder next to the plug-in:\n{0}\nDouble-click a preset, or click Apply, to use it on the current material.",
                               "预设存放在插件同级目录的 presets 文件夹（JSON）：\n{0}\n双击预设或点「应用」即套用到当前材质。",
                               "預設存放在外掛同級目錄的 presets 資料夾（JSON）：\n{0}\n雙擊預設或點「應用」即套用到目前材質。",
                               "プリセットはプラグインと同じ階層の presets フォルダに JSON で保存されます：\n{0}\nダブルクリック、または「適用」で現在の材質に反映されます。");
            add("Preset.OverwriteTitle", "Save Preset", "保存预设", "保存預設", "プリセットを保存");
            add("Preset.OverwriteBody", "Preset \"{0}\" already exists. Overwrite it?", "预设「{0}」已存在，要覆盖吗？", "預設「{0}」已存在，要覆蓋嗎？", "プリセット「{0}」は既に存在します。上書きしますか？");
            add("Preset.DeleteTitle", "Delete Preset", "删除预设", "刪除預設", "プリセットを削除");
            add("Preset.DeleteBody", "Delete preset \"{0}\"?", "确定删除预设「{0}」？", "確定刪除預設「{0}」？", "プリセット「{0}」を削除しますか？");
            add("Preset.NeedMaterial", "Please select a material first", "请先选择一个材质", "請先選擇一個材質", "先に材質を選択してください");
            add("Preset.AllDefault", "All parameters are default — adjust something before saving a preset", "当前参数全是默认值，先调一下再保存预设", "目前參數全是預設值，先調一下再保存預設", "全て既定値です。調整してから保存してください");
            add("Preset.SavedAuto", "Preset saved with auto name: \"{0}\"", "已用自动命名保存预设「{0}」", "已用自動命名保存預設「{0}」", "自動命名でプリセット「{0}」を保存しました");
            add("Preset.Saved", "Preset saved: \"{0}\"", "已保存预设「{0}」", "已保存預設「{0}」", "プリセット「{0}」を保存しました");
            add("Preset.SelectFirst", "Select a preset in the list first", "请先在列表里选中一个预设", "請先在清單裡選中一個預設", "リストからプリセットを選択してください");
            add("Preset.LoadFail", "Preset \"{0}\" is empty or failed to load", "预设「{0}」为空或读取失败", "預設「{0}」為空或讀取失敗", "プリセット「{0}」は空、または読み込みに失敗");
            add("Preset.Applied", "Applied preset \"{0}\" ({1} parameters, undoable)", "已套用预设「{0}」共 {1} 个参数（可撤销）", "已套用預設「{0}」共 {1} 個參數（可復原）", "プリセット「{0}」を適用（{1} パラメータ、元に戻せます）");
            add("Preset.Deleted", "Preset deleted: \"{0}\"", "已删除预设「{0}」", "已刪除預設「{0}」", "プリセット「{0}」を削除しました");
            add("Preset.OpenFail", "Failed to open preset folder: {0}", "打开预设文件夹失败：{0}", "開啟預設資料夾失敗：{0}", "プリセットフォルダを開けません：{0}");
            add("Preset.DefaultName", "My Preset", "我的预设", "我的預設", "マイプリセット");
            add("Preset.Suffix", " recipe", " 配方", " 配方", " 配方");

            // ---------------- 状态栏 / 消息 ----------------
            add("St.LangChanged", "Language switched", "已切换语言", "已切換語言", "言語を切り替えました");
            add("St.NeedMaterial", "Please select a material first", "请先选择一个材质", "請先選擇一個材質", "先に材質を選択してください");
            add("St.NeedTexturedMaterial", "Please select a material with a texture on the right.", "请先在右侧选择一个有贴图的材质。", "請先在右側選擇一個有貼圖的材質。", "右側で テクスチャ付きの材質を選択してください。");
            add("St.NoMaterialInModel", "The model has no material.", "模型无材质。", "模型無材質。", "モデルに材質がありません。");
            add("St.TextureLoadFail", "Failed to load texture: {0}", "贴图读取失败：{0}", "貼圖讀取失敗：{0}", "テクスチャの読み込みに失敗：{0}");
            add("St.TextureMissing", "Texture file not found: {0}", "贴图文件不存在：{0}", "貼圖檔案不存在：{0}", "テクスチャファイルが見つかりません：{0}");
            add("St.NoTexture", "This material has no texture (Tex is empty)", "该材质没有贴图 (Tex 为空)", "該材質沒有貼圖 (Tex 為空)", "この材質にテクスチャはありません (Tex が空)");
            add("St.NoSelection", "There is no UV selection", "当前没有 UV 选区", "目前沒有 UV 選取", "UV 選択がありません");
            add("St.Cleared", "UV selection cleared (grading now applies to the whole texture)", "已清除 UV 选区（调色作用于整张贴图）", "已清除 UV 選取（調色作用於整張貼圖）", "UV 選択を解除（調色はテクスチャ全体に適用）");
            add("St.RecvVertsFmt", "Received {0} selected vertices from the 3D view (out of {1}, filtered to this material)",
                                    "已从 3D 视图接收 {0} 个选中顶点（视图共选 {1} 个，已过滤为本材质用到的）",
                                    "已從 3D 視圖接收 {0} 個選取頂點（視圖共選 {1} 個，已過濾為本材質用到的）",
                                    "3Dビューから選択頂点を {0} 個受信（ビュー全体 {1} 個のうち、この材質分のみ）");
            add("St.RecvVertsNone", "No selected vertices in the 3D view (or none belong to this material)",
                                    "3D 视图当前没有选中顶点（或选中的顶点都不属于本材质）",
                                    "3D 視圖目前沒有選取頂點（或選取的頂點都不屬於本材質）",
                                    "3Dビューに選択頂点がない（またはこの材質の頂点がない）");
            add("St.RecvVertsFail", "Receiving vertices failed: {0}", "接收顶点失败：{0}", "接收頂點失敗：{0}", "頂点の受信に失敗：{0}");
            add("St.SendVertsNone", "No marked vertices to send — select UV faces or click \"Recv Vertices\" first",
                                    "没有可发送的顶点 —— 先选 UV 面，或点「接收选择顶点」",
                                    "沒有可傳送的頂點 —— 先選 UV 面，或點「接收選取頂點」",
                                    "送信できる頂点がない —— 先に UV 面を選択するか「選択頂点を受信」を押してください");
            add("St.SendVertsFmt", "Sent {0} vertex indices to the 3D view", "已把 {0} 个顶点索引发送到 3D 视图", "已把 {0} 個頂點索引傳送到 3D 視圖", "{0} 個の頂点インデックスを3Dビューへ送信しました");
            add("St.SendVertsFail", "Sending vertices failed: {0}", "发送顶点失败：{0}", "傳送頂點失敗：{0}", "頂点の送信に失敗：{0}");
            add("St.NoTris", "This material has no UV triangles", "当前材质没有 UV 三角面", "目前材質沒有 UV 三角面", "この材質に UV 三角面はありません");
            add("St.SelectedAll", "Selected all {0} UV triangles", "已选中全部 {0} 个 UV 三角面", "已選取全部 {0} 個 UV 三角面", "全ての UV 三角面 {0} 枚を選択しました");
            add("St.CompareOn", "Showing the original (compare mode) — your parameters are kept", "正在显示原图（对比模式）—— 调色参数仍然保留", "正在顯示原圖（對比模式）—— 調色參數仍然保留", "元画像を表示中（比較モード）— パラメータは保持されます");
            add("St.CompareOff", "Back to the graded result", "已回到调色结果", "已回到調色結果", "調整結果に戻りました");
            add("St.UvColorFmt", "UV layout color changed to #{0:X2}{1:X2}{2:X2}", "UV 布局颜色已改为 #{0:X2}{1:X2}{2:X2}", "UV 佈局顏色已改為 #{0:X2}{1:X2}{2:X2}", "UV レイアウトの色を #{0:X2}{1:X2}{2:X2} に変更");
            add("St.NothingUndo", "Nothing to undo", "没有可撤销的操作", "沒有可復原的操作", "元に戻す操作はありません");
            add("St.NothingRedo", "Nothing to redo", "没有可重做的操作", "沒有可重做的操作", "やり直す操作はありません");
            add("St.UndoFmt", "Undone (undo {0} / redo {1})", "已撤销（撤销 {0} / 重做 {1}）", "已復原（復原 {0} / 重做 {1}）", "元に戻しました（元に戻す {0} / やり直し {1}）");
            add("St.RedoFmt", "Redone (undo {0} / redo {1})", "已重做（撤销 {0} / 重做 {1}）", "已重做（復原 {0} / 重做 {1}）", "やり直しました（元に戻す {0} / やり直し {1}）");
            add("St.AlreadyDefault", "All parameters are already default", "参数已经全部为默认值", "參數已經全部為預設值", "全て既定値です");
            add("St.ResetDone", "All grading parameters of this material reset", "已重置该材质的全部调色参数", "已重設該材質的全部調色參數", "この材質の調色パラメータを全てリセットしました");
            add("St.RefreshedFmt", "Pushed to the 3D model (temp file {0})", "已刷新到 3D 模型（临时文件 {0}）", "已刷新到 3D 模型（暫存檔 {0}）", "3D モデルに反映しました（一時ファイル {0}）");
            add("St.SavedNewFmt", "Saved as new texture and pointed to: {0}", "已另存为新贴图并指向：{0}", "已另存為新貼圖並指向：{0}", "新規テクスチャとして保存し参照先を変更：{0}");
            add("St.NeedUvMaterial", "Please select a material that has UVs", "请先选择一个有 UV 的材质", "請先選擇一個有 UV 的材質", "UV を持つ材質を選択してください");
            add("St.UvExportedFmt", "UV layout exported: {0} ({1}×{2})", "UV 布局图已导出：{0}（{1}×{2}）", "UV 佈局圖已匯出：{0}（{1}×{2}）", "UV レイアウトを書き出し：{0}（{1}×{2}）");
            add("St.ExportFail", "Export failed: {0}", "导出失败：{0}", "匯出失敗：{0}", "書き出しに失敗：{0}");
            add("St.SelMaskExportedFmt", "Selection mask exported: {0} ({1}×{2}, {3} faces)", "选区蒙版已导出：{0}（{1}×{2}，{3} 个面）", "選取遮罩已匯出：{0}（{1}×{2}，{3} 個面）", "選択範囲マスクを書き出し：{0}（{1}×{2}，{3} 枚）");
            add("St.MatMaskExportedFmt", "Material mask exported: {0} ({1}×{2}, {3} faces)", "材质蒙版已导出：{0}（{1}×{2}，{3} 个面）", "材質遮罩已匯出：{0}（{1}×{2}，{3} 個面）", "材質マスクを書き出し：{0}（{1}×{2}，{3} 枚）");
            add("St.MaskFail", "Mask export failed: {0}", "导出蒙版失败：{0}", "匯出遮罩失敗：{0}", "マスク書き出しに失敗：{0}");
            add("St.TooLargeFmt", "Texture too large ({0}×{1}) — mask export skipped", "贴图尺寸过大（{0}×{1}），已跳过蒙版导出", "貼圖尺寸過大（{0}×{1}），已跳過遮罩匯出", "テクスチャが大きすぎます（{0}×{1}）— マスク書き出しをスキップ");
            add("St.Reverted", "Texture reverted (Material.Tex restored, temp files cleaned)", "已还原该材质贴图（Material.Tex 恢复原始值，临时文件已清理）", "已還原該材質貼圖（Material.Tex 恢復原始值，暫存檔已清理）", "テクスチャを元に戻しました（Material.Tex を復元、一時ファイルを削除）");
            add("St.NoTriHere", "No UV triangle here (drag to box-select, or click near a UV edge)", "此处没有 UV 三角面（可拖动框选，或直接点 UV 线附近）", "此處沒有 UV 三角面（可拖曳框選，或直接點 UV 線附近）", "ここに UV 三角面はありません（ドラッグで矩形選択、または UV 線の近くをクリック）");
            add("St.DoubleClickMiss", "No UV triangle here — double-click must land on a UV face", "此处没有 UV 三角面，双击请落在 UV 面上", "此處沒有 UV 三角面，雙擊請落在 UV 面上", "ここに UV 三角面はありません—ダブルクリックは UV 面上で");
            add("St.SelectedFmt", "Selected {0} UV triangles — grading applies to the selection only (double-click adds a whole connected island; keep going to select several)",
                                  "已选 {0} 个 UV 三角面 —— 调色只作用于选区（双击加选整块连通 UV，可连续双击选多块）",
                                  "已選 {0} 個 UV 三角面 —— 調色只作用於選取（雙擊加選整塊連通 UV，可連續雙擊選多塊）",
                                  "{0} 枚の UV 三角面を選択 — 調色は選択範囲のみ（ダブルクリックで連結アイランドを追加、続けて複数選択可）");
            add("St.NoneSelected", "No UV area selected — grading applies to the whole texture", "未选择 UV 区域 —— 调色作用于整张贴图", "未選擇 UV 區域 —— 調色作用於整張貼圖", "UV 未選択 — 調色はテクスチャ全体に適用");
            add("St.IslandHint", "Click the island you want in the preview first, or just double-click it (double-click = whole island, keep double-clicking to add more)",
                                 "请先在预览里点一下想选的那块 UV，或直接双击它（双击 = 选中整块，可继续双击其它块加选多块）",
                                 "請先在預覽裡點一下想選的那塊 UV，或直接雙擊它（雙擊 = 選取整塊，可繼續雙擊其它塊加選多塊）",
                                 "先にプレビューで選択したいアイランドをクリック、またはダブルクリック（ダブルクリック = アイランド全体、続けてダブルクリックで複数選択可）");
            add("St.IslandSelectedFmt", "Selected a whole connected UV island: {0} triangles (island {1} / {2})",
                                        "已选中整块连通 UV：{0} 个三角面（第 {1} / {2} 块）",
                                        "已選取整塊連通 UV：{0} 個三角面（第 {1} / {2} 塊）",
                                        "連結 UV アイランドを選択：{0} 枚（アイランド {1} / {2}）");
            add("St.IslandAddedFmt", "Island {0}/{1} added ({2} triangles) → {3} selected in total. Double-click another island to add it; double-click it again (or Ctrl+double-click) to remove",
                                     "已加选第 {0}/{1} 块连通 UV（{2} 个三角面）→ 当前共选 {3} 个；继续双击其它块可继续加选，再双击一次（或 Ctrl+双击）取消",
                                     "已加選第 {0}/{1} 塊連通 UV（{2} 個三角面）→ 目前共選 {3} 個；繼續雙擊其它塊可繼續加選，再雙擊一次（或 Ctrl+雙擊）取消",
                                     "アイランド {0}/{1} を追加（{2} 枚）→ 合計 {3} 枚。他のアイランドをダブルクリックで追加、もう一度（または Ctrl+ダブルクリック）で解除");
            add("St.IslandRemovedFmt", "Island {0}/{1} removed → {2} selected in total",
                                       "已取消第 {0}/{1} 块连通 UV → 当前共选 {2} 个",
                                       "已取消第 {0}/{1} 塊連通 UV → 目前共選 {2} 個",
                                       "アイランド {0}/{1} を解除 → 合計 {2} 枚");
            add("St.IslandGrownFmt", "Grew {0} island(s) into complete islands (+{1} triangles) → {2} selected in total",
                                     "已把 {0} 块连通 UV 补全（新增 {1} 个三角面）→ 当前共选 {2} 个",
                                     "已把 {0} 塊連通 UV 補全（新增 {1} 個三角面）→ 目前共選 {2} 個",
                                     "{0} 個のアイランドを完全な形に補完（+{1} 枚）→ 合計 {2} 枚");
            add("St.BandSelectedFmt", "Box-selected {0} triangles → {1} selected in total (grading applies to the selection only)",
                                      "框选 {0} 个三角面 → 共选 {1} 个（调色只作用于选区）",
                                      "框選 {0} 個三角面 → 共選 {1} 個（調色只作用於選取）",
                                      "矩形選択 {0} 枚 → 合計 {1} 枚（調色は選択範囲のみ）");
            add("St.BandEmpty", "No UV triangle inside the box", "框选范围内没有 UV 三角面", "框選範圍內沒有 UV 三角面", "矩形内に UV 三角面はありません");
            add("St.MatInfoFmt", "{0}   ·   {1}×{2}   ·   {3} triangles   ·   {4} connected UV islands",
                                 "{0}   ·   {1}×{2}   ·   {3} 个三角面   ·   {4} 块连通 UV",
                                 "{0}   ·   {1}×{2}   ·   {3} 個三角面   ·   {4} 塊連通 UV",
                                 "{0}   ·   {1}×{2}   ·   {3} 三角面   ·   {4} 連結 UV アイランド");
            add("St.MatNoUv", "This material has no UV faces — nothing to export", "该材质没有 UV 面，无法导出蒙版", "該材質沒有 UV 面，無法匯出遮罩", "この材質に UV 面がないため書き出しできません");
            add("St.NoMaterialToExport", "No material to export", "没有可导出的材质", "沒有可匯出的材質", "書き出し可能な材質がありません");
            add("St.AllMaskDoneFmt", "Exported {0} material masks to {1}{2}{3}",
                                     "已导出 {0} 张材质蒙版到 {1}{2}{3}",
                                     "已匯出 {0} 張材質遮罩到 {1}{2}{3}",
                                     "{0} 枚の材質マスクを {1} に書き出し{2}{3}");
            add("St.AllMaskSkipFmt", "，{0} 个材质没有 UV 面已跳过", "，{0} 个材质没有 UV 面已跳过", "，{0} 個材質沒有 UV 面已跳過", "、UV 面のない材質 {0} 件をスキップ");
            add("St.AllMaskFailFmt", "，{0} 张失败", "，{0} 张失败", "，{0} 張失敗", "、{0} 枚失敗");
            add("St.NeedTexture", "Please select a material with a texture first", "请先选择一个有贴图的材质", "請先選擇一個有貼圖的材質", "先にテクスチャ付きの材質を選択してください");

            // ---------------- 对话框 ----------------
            add("Dlg.FilterPng", "PNG Image|*.png", "PNG 图像|*.png", "PNG 圖片|*.png", "PNG 画像|*.png");
            add("Dlg.ExportUvTitle", "Export UV Layout (transparent + wireframe)", "导出 UV 布局图（透明背景 + 线框）", "匯出 UV 佈局圖（透明背景 + 線框）", "UV レイアウトを書き出し（透明 + ワイヤーフレーム）");
            add("Dlg.ExportUvBody", "UV layout exported to:\n{0}\n\nSize {1}×{2}, transparent background + wireframe — it aligns directly over the texture.",
                                    "UV 布局图已导出到：\n{0}\n\n尺寸 {1}×{2}，透明背景 + 线框，可直接叠在贴图上对齐。",
                                    "UV 佈局圖已匯出到：\n{0}\n\n尺寸 {1}×{2}，透明背景 + 線框，可直接疊在貼圖上對齊。",
                                    "UV レイアウトを書き出し：\n{0}\n\nサイズ {1}×{2}、透明背景 + ワイヤーフレーム。テクスチャに重ねて使えます。");
            add("Dlg.MaskSelTitle", "Export selection mask (white = selected, black = rest)", "导出当前选区蒙版（白 = 选中，黑 = 其余）", "匯出目前選取遮罩（白 = 選取，黑 = 其餘）", "選択範囲マスクを書き出し（白 = 選択、黒 = その他）");
            add("Dlg.MaskMatTitle", "Export current material mask (white = this material's UVs)", "导出当前材质蒙版（白 = 该材质的 UV）", "匯出目前材質遮罩（白 = 該材質的 UV）", "現在の材質マスクを書き出し（白 = この材質の UV）");
            add("Dlg.MaskTitle", "Export Mask", "导出蒙版", "匯出遮罩", "マスクを書き出し");
            add("Dlg.NoSelMaskBody", "There is no UV selection.\n\nClick / box-select / double-click UV faces in the left panel first;\n to export one mask per material, use \"Export All Material Masks…\".",
                                     "当前没有 UV 选区。\n\n在左栏单击 / 框选 / 双击选中 UV 面后再导出；\n若想按材质整体导出（每个材质一张），请用「导出全部材质蒙版…」。",
                                     "目前沒有 UV 選取。\n\n在左欄單擊 / 框選 / 雙擊選取 UV 面後再匯出；\n若想按材質整體匯出（每個材質一張），請用「匯出全部材質遮罩…」。",
                                     "UV 選択がありません。\n\n左パネルでクリック / 矩形選択 / ダブルクリックして UV 面を選んでから書き出してください。\n材質ごとに書き出す場合は「全材質マスクを書き出し…」を使います。");
            add("Dlg.SelMaskBodyFmt", "Selection mask exported to:\n{0}\n\nSize {1}×{2}; white = the {3} selected UV faces, black = the rest.\nUsable directly as a layer mask in Photoshop, or Ctrl+click to load a selection.",
                                      "选区蒙版已导出到：\n{0}\n\n尺寸 {1}×{2}，白色 = 选中的 {3} 个 UV 面，黑色 = 其余。\n在 PS 里可直接当图层蒙版，或 Ctrl+点击载入选区。",
                                      "選取遮罩已匯出到：\n{0}\n\n尺寸 {1}×{2}，白色 = 選取的 {3} 個 UV 面，黑色 = 其餘。\n在 PS 裡可直接當圖層遮罩，或 Ctrl+點擊載入選取。",
                                      "選択範囲マスクを書き出し：\n{0}\n\nサイズ {1}×{2}、白 = 選択した {3} 枚の UV 面、黒 = その他。\nPhotoshop のレイヤーマスクとして、または Ctrl+クリックで選択範囲として使えます。");
            add("Dlg.MatMaskBodyFmt", "Material mask exported to:\n{0}\n\nSize {1}×{2}; white = all {3} UV faces of this material, black = the rest.\nUsable directly as a layer mask in Photoshop, or Ctrl+click to load a selection.",
                                      "材质蒙版已导出到：\n{0}\n\n尺寸 {1}×{2}，白色 = 该材质的全部 {3} 个 UV 面，黑色 = 其余。\n在 PS 里可直接当图层蒙版，或 Ctrl+点击载入选区。",
                                      "材質遮罩已匯出到：\n{0}\n\n尺寸 {1}×{2}，白色 = 該材質的全部 {3} 個 UV 面，黑色 = 其餘。\n在 PS 裡可直接當圖層遮罩，或 Ctrl+點擊載入選取。",
                                      "材質マスクを書き出し：\n{0}\n\nサイズ {1}×{2}、白 = この材質の全 {3} UV 面、黒 = その他。\nPhotoshop のレイヤーマスクとして、または Ctrl+クリックで選択範囲として使えます。");
            add("Dlg.MaskAllDesc", "Choose the output folder (one PNG per material)", "选择蒙版输出文件夹（每个材质一张 PNG）", "選擇遮罩輸出資料夾（每個材質一張 PNG）", "書き出し先フォルダを選択（材質ごとに 1 枚の PNG）");
            add("Dlg.AllMaskTitle", "Export All Material Masks", "导出全部材质蒙版", "匯出全部材質遮罩", "全材質マスクを書き出し");
            add("Dlg.AllMaskBodyFmt", "Exported {0} masks to:\n{1}\n\n{2}{3}{4}",
                                      "已导出 {0} 张蒙版到：\n{1}\n\n{2}{3}{4}",
                                      "已匯出 {0} 張遮罩到：\n{1}\n\n{2}{3}{4}",
                                      "{0} 枚のマスクを書き出し：\n{1}\n\n{2}{3}{4}");
            add("Dlg.AllMaskNamedFmt", "Named like {0} (white = that material's UV area).\n", "命名形如 {0}（白 = 该材质的 UV 覆盖区域）。\n", "命名形如 {0}（白 = 該材質的 UV 覆蓋區域）。\n", "{0} のような名前（白 = その材質の UV 領域）。\n");
            add("Dlg.AllMaskSkipFmt", "{0} materials have no UV faces and were skipped.\n", "{0} 个材质没有 UV 面，已跳过。\n", "{0} 個材質沒有 UV 面，已跳過。\n", "UV 面のない材質 {0} 件をスキップしました。\n");
            add("Dlg.AllMaskFailFmt", "{0} failed (too large or file in use).\n", "{0} 张导出失败（尺寸过大或文件被占用）。\n", "{0} 張匯出失敗（尺寸過大或檔案被佔用）。\n", "{0} 枚が失敗（サイズが大きすぎる、またはファイルが使用中）。\n");
            add("Err.Title", "TextureGrade Error", "TextureGrade 错误", "TextureGrade 錯誤", "TextureGrade エラー");
            add("Err.NoModel", "No PMX model is open.\n\nPlease open (or load) a model in PMXEditor first, then run Texture Grade again.",
                                "还没有打开 PMX 模型。\n\n请先在 PMXEditor 里打开（或载入）一个模型，再运行本插件。",
                                "尚未開啟 PMX 模型。\n\n請先在 PMXEditor 裡開啟（或載入）一個模型，再執行本外掛。",
                                "PMX モデルが開かれていません。\n\nPMXエディタでモデルを開いてから、本プラグインを再度実行してください。");

            add("File.SaveNewAs", "Save as New Texture… (pick format & size)", "另存为新贴图…（选格式与尺寸）", "另存為新貼圖…（選格式與尺寸）", "新規テクスチャに保存…（形式とサイズを選択）");
            add("File.ExportSelectionAlpha", "Export Selection Mask… (as Alpha channel PNG)", "导出选区蒙版…（带 Alpha 通道 PNG）", "匯出選取遮罩…（含 Alpha 通道 PNG）", "選択範囲マスクを書き出し…（アルファ通道付き PNG）");
            add("Edit.InvertSel", "Invert UV Selection", "反选 UV 选区", "反選 UV 選取", "UV 選択を反転");
            add("Grp.Lab", "Lab Color Wheel (brightness locked)", "Lab 取色环（亮度锁定）", "Lab 取色環（亮度鎖定）", "Lab カラーホイール（明度ロック）");
            add("Gradient", "Gradient", "渐变", "漸變", "グラデーション");
            add("LabTargetL", "Target Lightness L*", "目标亮度 L*", "目標亮度 L*", "目標明度 L*");
            add("LabAmount", "Amount", "强度", "強度", "強度");
            add("Preset.Rename", "Rename", "改名", "改名", "名前を変更");
            add("Preset.RenameTitle", "Rename Preset", "重命名预设", "重新命名預設", "プリセット名を変更");
            add("Preset.RenamePrompt", "New name for \"{0}\":", "给「{0}」起个新名字：", "為「{0}」取個新名字：", "「{0}」の新しい名前：");
            add("Preset.RenameDup", "A preset named \"{0}\" already exists.", "已经有叫「{0}」的预设了。", "已經有叫「{0}」的預設了。", "「{0}」というプリセットは既に存在します。");
            add("Preset.Renamed", "Renamed \"{0}\" to \"{1}\"", "已把「{0}」改名为「{1}」", "已把「{0}」改名為「{1}」", "「{0}」を「{1}」に変更しました");
            add("Preset.RenameFail", "Rename failed: \"{0}\" (invalid name, or the file is in use)", "改名失败：「{0}」（名字不合法或文件被占用）", "改名失敗：「{0}」（名字不合法或檔案被佔用）", "名前の変更に失敗：「{0}」（無効な名前、またはファイルが使用中）");
            add("Preset.SortFmt", "Sort: {0}", "排序：{0}", "排序：{0}", "並び替え：{0}");
            add("Preset.SortName", "by name", "按名称", "按名稱", "名前順");
            add("Preset.SortTime", "by time", "按时间", "按時間", "更新順");
            add("Preset.SortCustom", "manual", "手动顺序", "手動順序", "手動");
            add("Preset.Sorted", "Preset list sorted ({0})", "预设列表已按 {0} 排序", "預設清單已按 {0} 排序", "プリセットを {0} で並び替えました");
            add("Preset.Up", "Move Up", "上移", "上移", "上へ");
            add("Preset.Down", "Move Down", "下移", "下移", "下へ");
            add("Preset.Moved", "Moved \"{0}\" to position {1} of {2}", "已把「{0}」移到第 {1} 位（共 {2} 个）", "已把「{0}」移到第 {1} 位（共 {2} 個）", "「{0}」を {2} 件中 {1} 番目に移動しました");
            add("Preset.AtTop", "Already at the top", "已经在最前面了", "已經在最前面了", "すでに先頭です");
            add("Preset.AtBottom", "Already at the bottom", "已经在最后面了", "已經在最後面了", "すでに末尾です");
            add("Lab.WheelTip", "Outer ring: hue (constant lightness)\nInner disc: chroma (a single line at fixed lightness)",
                                "外环：色相（等亮度）\n内圈：彩度（固定亮度下的一条线）",
                                "外環：色相（等亮度）\n內圈：彩度（固定亮度下的一條線）",
                                "外環：色相（明度一定）\n内円：彩度（明度固定の1本の線）");
            add("Lab.Lock", "Lock lightness L* (change color only, brightness untouched)", "锁定亮度 L*（只换颜色，明暗完全不变）", "鎖定亮度 L*（只換顏色，明暗完全不變）", "明度 L* をロック（色だけ変更、明暗は不変）");
            add("Lab.LockTip", "Checked: any color you pick only changes a*/b*; every pixel keeps its own lightness L*.\nUnchecked: lightness also moves toward the target.",
                               "勾选：色环上取任何颜色都只改 a*/b*，每个像素自身的亮度 L* 原样保留。\n取消：连亮度也一起朝目标色靠拢。",
                               "勾選：色環上取任何顏色都只改 a*/b*，每個像素自身的亮度 L* 原樣保留。\n取消：連亮度也一起朝目標色靠攏。",
                               "オン：選んだ色は a*/b* のみ変更し、各ピクセルの明度 L* は保持されます。\nオフ：明度も目標色に近づきます。");
            add("Lab.PickL", "Pick Image Lightness", "取画面亮度", "取畫面亮度", "画像の明度を取得");
            add("Lab.PickHue", "Pick Image Hue", "取画面色相", "取畫面色相", "画像の色相を取得");
            add("Lab.Reset", "Reset", "重置", "重設", "リセット");
            add("Lab.Note", "The wheel is an iso-lightness hue ring (every angle is at max chroma for that lightness); the inner disc is the chroma line of the current hue.\nWith lightness locked, brightness never changes no matter where you turn — only hue and chroma do.",
                            "色环 = 等亮度色相环（环上每个角度都取该亮度下的最高彩度）；内圈 = 当前色相的彩度线。\n锁定亮度时不管在环上转到哪，画面明暗都不变，只是色相/彩度在动。",
                            "色環 = 等亮度色相環（環上每個角度都取該亮度下的最高彩度）；內圈 = 目前色相的彩度線。\n鎖定亮度時不管在環上轉到哪，畫面明暗都不變，只是色相/彩度在動。",
                            "ホイールは等明度色相環（どの角度もその明度での最高彩度）、内円は現在の色相の彩度線です。\n明度をロックすると、どこに回しても明るさは変わらず色相と彩度だけが動きます。");
            add("Lab.InfoFmt", "#{0:X2}{1:X2}{2:X2}   Chroma {3:F0}   Hue {4:F0}°\n{5}   Amount {6:F0}%",
                               "#{0:X2}{1:X2}{2:X2}   彩度 {3:F0}   色相 {4:F0}°\n{5}   强度 {6:F0}%",
                               "#{0:X2}{1:X2}{2:X2}   彩度 {3:F0}   色相 {4:F0}°\n{5}   強度 {6:F0}%",
                               "#{0:X2}{1:X2}{2:X2}   彩度 {3:F0}   色相 {4:F0}°\n{5}   強度 {6:F0}%");
            add("Lab.Locked", "● Lightness locked (brightness unchanged)", "● 亮度锁定（明暗不变）", "● 亮度鎖定（明暗不變）", "● 明度ロック（明暗不変）");
            add("Lab.Unlocked", "○ Lightness adjustable", "○ 亮度可调", "○ 亮度可調", "○ 明度調整可");
            add("Lab.LockOn", "Lab wheel: lightness locked (only hue/chroma change, brightness stays)", "Lab 取色环：已锁定亮度（只改色相/彩度，画面明暗不变）", "Lab 取色環：已鎖定亮度（只改色相/彩度，畫面明暗不變）", "Lab ホイール：明度をロック（色相/彩度のみ変化、明暗は不変）");
            add("Lab.LockOff", "Lab wheel: lightness unlocked (brightness also moves toward the target)", "Lab 取色环：已解除亮度锁定（亮度也会朝目标色靠拢）", "Lab 取色環：已解除亮度鎖定（亮度也會朝目標色靠攏）", "Lab ホイール：明度ロック解除（明るさも目標色に近づきます）");
            add("Lab.ResetDone", "Lab wheel reset (amount 0 = not applied)", "Lab 取色环已重置（强度 0 = 不参与调色）", "Lab 取色環已重設（強度 0 = 不參與調色）", "Lab ホイールをリセット（強度 0 = 適用なし）");
            add("Lab.PickedLFmt", "{0} average lightness L* = {1:F1} (wheel drawn at this lightness)", "{0}平均亮度 L* = {1:F1}（色环已按这个亮度绘制）", "{0}平均亮度 L* = {1:F1}（色環已按這個亮度繪製）", "{0}の平均明度 L* = {1:F1}（この明度でホイールを描画）");
            add("Lab.PickedHueFmt", "Picked {0} average color (a*={1:F1}, b*={2:F1}) into the wheel", "已取{0}平均色（a*={1:F1}, b*={2:F1}）到取色环", "已取{0}平均色（a*={1:F1}, b*={2:F1}）到取色環", "{0}の平均色（a*={1:F1}, b*={2:F1}）をホイールに取得しました");
            add("Lab.ScopeSelection", "Selection", "选区", "選取", "選択範囲");
            add("Lab.ScopeWhole", "Whole image", "整张图", "整張圖", "画像全体");
            add("St.InvertedFmt", "Inverted selection — {0} of {1} UV triangles selected", "已反选 —— {0}/{1} 个 UV 三角面处于选中状态", "已反選 —— {0}/{1} 個 UV 三角面處於選取狀態", "選択を反転 — {1} 枚中 {0} 枚を選択");
            add("St.InvertedEmpty", "Inverted selection — nothing selected now (the previous selection was everything)", "已反选 —— 当前没有选中面（原来的选区是全部）", "已反選 —— 目前沒有選取面（原本的選取是全部）", "選択を反転 — 現在の選択はなし（前回は全選択でした）");
            add("St.RefreshFailFmt", "Pushing to the 3D model failed: {0}", "刷新到 3D 模型失败：{0}", "刷新到 3D 模型失敗：{0}", "3D モデルへの反映に失敗：{0}");
            add("St.PreviewFallbackFmt", "Texture folder is not writable — preview written to {0} instead",
                                         "贴图所在目录不可写，临时预览已改写到 {0}",
                                         "貼圖所在目錄不可寫，暫存預覽已改寫到 {0}",
                                         "テクスチャのフォルダに書き込めないため、一時プレビューを {0} に書き出しました");
            add("St.SelRestoredFmt", "Restored this material's previous UV selection ({0} triangles) — grading still applies to the selection only",
                                     "已恢复该材质上一次的 UV 选区（{0} 个三角面）—— 调色仍只作用于选区",
                                     "已恢復該材質上一次的 UV 選取（{0} 個三角面）—— 調色仍只作用於選取",
                                     "この材質の前回の UV 選択を復元しました（{0} 枚）— 調色は選択範囲のみに適用されます");
            add("St.SavedAsFmt", "Saved as {0} ({1}×{2}) and pointed the material at it", "已另存为 {0}（{1}×{2}）并让材质指向它", "已另存為 {0}（{1}×{2}）並讓材質指向它", "{0}（{1}×{2}）として保存し、材質の参照先を変更しました");
            add("St.NeedTextureForLab", "Please select a material with a texture first", "请先选择一个有贴图的材质", "請先選擇一個有貼圖的材質", "先にテクスチャ付きの材質を選択してください");
            add("Dlg.OK", "OK", "确定", "確定", "OK");
            add("Dlg.Cancel", "Cancel", "取消", "取消", "キャンセル");
            add("SaveDlg.Title", "Save Texture As", "另存贴图为", "另存貼圖為", "テクスチャを別名保存");
            add("SaveDlg.Path", "File", "文件", "檔案", "ファイル");
            add("SaveDlg.Browse", "Browse…", "浏览…", "瀏覽…", "参照…");
            add("SaveDlg.Format", "Format", "格式", "格式", "形式");
            add("SaveDlg.Size", "Size", "尺寸", "尺寸", "サイズ");
            add("SaveDlg.SizeOriginal", "Original (100%)", "原尺寸（100%）", "原尺寸（100%）", "元サイズ（100%）");
            add("SaveDlg.SizeHalf", "Half (50%)", "二分之一（50%）", "二分之一（50%）", "1/2（50%）");
            add("SaveDlg.SizeQuarter", "Quarter (25%)", "四分之一（25%）", "四分之一（25%）", "1/4（25%）");
            add("SaveDlg.SizeCustom", "Custom…", "自定义…", "自訂…", "カスタム…");
            add("SaveDlg.Width", "W", "宽", "寬", "幅");
            add("SaveDlg.Height", "H", "高", "高", "高");
            add("SaveDlg.Quality", "JPEG quality", "JPEG 质量", "JPEG 品質", "JPEG 品質");
            add("SaveDlg.NoAlpha", "No alpha channel", "不含 Alpha 通道", "不含 Alpha 通道", "アルファなし");
            add("SaveDlg.Lossy", "Lossy compression", "有损压缩", "有損壓縮", "非可逆圧縮");
            add("SaveDlg.DdsNote", "DDS sizes are safest as powers of two", "DDS 建议宽高保持 2 的幂", "DDS 建議寬高保持 2 的冪", "DDS は幅・高さを 2 のべき乗にするのが無難");
            add("SaveDlg.InfoFmt", "Source {0}×{1} → output {2}×{3}", "原图 {0}×{1} → 输出 {2}×{3}", "原圖 {0}×{1} → 輸出 {2}×{3}", "元 {0}×{1} → 出力 {2}×{3}");
            add("SaveDlg.Save", "Save", "保存", "保存", "保存");
            add("SaveDlg.Cancel", "Cancel", "取消", "取消", "キャンセル");
            add("SaveDlg.NeedPath", "Please choose where to save the file.", "请选择保存位置。", "請選擇儲存位置。", "保存先を選んでください。");
            add("SaveDlg.FilterAll", "All supported formats|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.tga;*.dds|All files|*.*",
                                     "所有支持的格式|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.tga;*.dds|所有文件|*.*",
                                     "所有支援的格式|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.tga;*.dds|所有檔案|*.*",
                                     "対応形式|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.tga;*.dds|すべてのファイル|*.*");
            add("AlphaDlg.Title", "Export Selection Mask (Alpha Channel)", "导出选区蒙版（Alpha 通道）", "匯出選取遮罩（Alpha 通道）", "選択範囲マスクを書き出し（アルファチャンネル）");
            add("AlphaDlg.Desc", "The alpha channel holds the selection (selected = opaque).\nWhat should the RGB channels contain? Currently {0} UV faces are selected.",
                                 "Alpha 通道保存选区（选中 = 不透明）。RGB 通道要放什么？\n当前选中 {0} 个 UV 面。",
                                 "Alpha 通道保存選取（選取 = 不透明）。RGB 通道要放什麼？\n目前選取 {0} 個 UV 面。",
                                 "アルファチャンネルに選択範囲（選択 = 不透明）を入れます。\nRGB には何を入れますか？現在 {0} 枚の UV 面が選択中。");
            add("AlphaDlg.White", "Plain white (best as a layer mask)", "纯白（最适合当图层蒙版）", "純白（最適合當圖層遮罩）", "白一色（レイヤーマスク向け）");
            add("AlphaDlg.Original", "Original texture (see what you selected)", "原贴图（方便看选的是哪块）", "原貼圖（方便看選的是哪塊）", "元テクスチャ（選択箇所の確認向け）");
            add("AlphaDlg.Graded", "Graded result (pull out the graded area)", "调色结果（方便把调好的部分抠出来）", "調色結果（方便把調好的部分摳出來）", "調整結果（調整部分の抜き出し向け）");
            add("Dlg.AlphaBodyFmt", "Alpha mask exported to:\n{0}\n\nSize {1}×{2}; alpha = the {3} selected UV faces (255), the rest transparent (0).\nRGB = {4}.\nIn Photoshop: Ctrl+click the layer thumbnail to load the selection, or paste it straight into a layer mask.",
                                    "带 Alpha 的蒙版已导出到：\n{0}\n\n尺寸 {1}×{2}；alpha：选中的 {3} 个 UV 面 = 255，其余 = 0（透明）。\nRGB = {4}。\n在 PS 里：Ctrl+点击图层缩略图即可载入选区，也可以直接贴进图层蒙版。",
                                    "帶 Alpha 的遮罩已匯出到：\n{0}\n\n尺寸 {1}×{2}；alpha：選取的 {3} 個 UV 面 = 255，其餘 = 0（透明）。\nRGB = {4}。\n在 PS 裡：Ctrl+點擊圖層縮圖即可載入選取，也可以直接貼進圖層遮罩。",
                                    "アルファ付きマスクを書き出し：\n{0}\n\nサイズ {1}×{2}、アルファ：選択した {3} 枚 = 255、その他 = 0（透明）。\nRGB = {4}。\nPhotoshop では Ctrl+クリックで選択範囲を読み込み、そのままレイヤーマスクにも貼り付けられます。");
            add("St.SaveAsFail", "Saving failed: {0}", "保存失败：{0}", "儲存失敗：{0}", "保存に失敗：{0}");
            add("St.AlphaExportedFmt", "Alpha mask exported: {0} ({1}×{2}, {3} faces)", "带 Alpha 的蒙版已导出：{0}（{1}×{2}，{3} 个面）", "帶 Alpha 的遮罩已匯出：{0}（{1}×{2}，{3} 個面）", "アルファ付きマスクを書き出し：{0}（{1}×{2}、{3} 枚）");
            RecolorTexts.Add(d);
            add("St.SelectionRestored", "Restored UV selection ({0} triangles); the grading scope is unchanged.", "已恢复 UV 选区（{0} 个面），调色范围保持原设置。", "已恢復 UV 選取（{0} 個面），調色範圍保持原設定。", "UV 選択（{0} 面）を復元しました。調色範囲は元の設定を保持します。");
            add("File.SavePngQuick", "Quick save as new PNG", "快速另存为 PNG", "快速另存為 PNG", "新規 PNG にクイック保存");
            add("SaveDlg.BinaryAlpha", "Alpha becomes binary (transparent/opaque).", "Alpha 仅保留全透明/不透明两级。", "Alpha 僅保留全透明/不透明兩級。", "アルファは透明・不透明の二値になります。");
            add("SaveDlg.TooLarge", "Output exceeds the 64-megapixel limit.", "输出不能超过 64 × 1024 × 1024 像素。", "輸出不能超過 64 × 1024 × 1024 像素。", "出力は 64 × 1024 × 1024 ピクセル以下にしてください。");
            add("SaveDlg.Overwrite", "Replace the existing file?\n{0}", "替换已有文件？\n{0}", "取代現有檔案？\n{0}", "既存のファイルを置き換えますか？\n{0}");
            add("SaveDlg.ProtectSource", "Choose a new filename to preserve the original texture.", "请选择新文件名，以保留原始贴图。", "請選擇新檔名，以保留原始貼圖。", "元のテクスチャを保護するため、新しいファイル名を指定してください。");
            add("Preset.StorageFail", "Cannot save preset order: {0}", "无法保存预设排序：{0}", "無法儲存預設排序：{0}", "プリセットの順序を保存できません：{0}");
            return d;
        }
    }
}
