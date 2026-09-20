# TextureGrade — PMXEditor 貼圖調色外掛

[English](ReadMe.md) | [简体中文](ReadMe_sc.md) | [繁體中文](ReadMe_tc.md) | [日本語](ReadMe_ja.md)

直接預覽及調整模型貼圖，從原始像素開始處理。「刷新模型」提供暫存 3D 預覽；另存只更新目前材質的貼圖參照。

## 調色與調色盤

- 曝光、對比、高光、陰影、白黑場、白平衡、飽和度、HSL、曲線、色階、RGB、清晰度與銳化。
- 黑白化為連續灰階；閾值強度與黑白分界值（0–255）獨立設定。舊漸變效果和 Lab 色環已移除。
- 可編輯原色→目標色、吸取原圖顏色、新增手動項目，以及啟用/刪除/鎖定明度。重新辨識保留手動項目。
- 同欄色塊可拖曳交換：原色↔原色、目標色↔目標色，另一欄保持原位。拖曳顯示列號及落點提示，上下邊緣自動捲動。遠距離可啟用「點選交換」或右鍵「與另一色塊交換」，自由捲動後再點另一色塊；Esc 或頂部取消按鈕退出。一般點選仍開啟選色器。各列啟用、手動標記及明度鎖定保持原位，目標色依所在列重新約束明度與色域。交換可一步復原；拖曳不重算貼圖，放開後只更新相關色塊並非同步預覽。
- 原圖及參考圖可分別自動估計主色數量，或手動指定 1–128 色。自動方式依感知色差與有效面積忽略細微漸層及孤立雜訊，並非物件語意分割。
- 在調色盤內匯入參考圖並框選取樣，產生依相對明度、面積占比和明暗順序對應的可編輯調色盤。控制色足夠時涵蓋全部參考主色。
- 過渡色也會轉換色相及彩度，減少原圖底色殘留；舊預設集維持舊插值，重新產生可使用修正版。
- 選色支援 HSV、RGB、OKLCH，明度鎖定獨立開關。預設保留每像素 OKLCH L，色域外降低彩度；8 位元量化及後續效果仍可能改變最終明度。
- 全域調色盤插值先於其他效果執行，不設手動強度或半徑。統計遷移已移除。
- 支援復原/重做、分材質記憶與預設集；產生後不需要原參考檔案即可重現效果。

## UV、材質與輸出

- 滾輪縮放、右鍵平移、點選面、雙擊 UV 島；Shift 加選、Ctrl 減選，並可與 PMXEditor 收發頂點選取。
- 左側選取工具或編輯選單可反選（`Ctrl+I`），`Ctrl+A` 全選；不攔截文字框的編輯快捷鍵。
- 切換材質或重新讀取會恢復各自選區及接收頂點；幾何或 UV 改變時丟棄舊選區。
- 預設有選區只調整局部，無選區則調整整張貼圖；反選全部後也遵循此規則。「整張貼圖」忽略但保留選區，並提示共用區域。隱藏 UV 不改變選區或調色。
- 大量頂點使用合併繪圖層；拖曳預覽最長邊 1024 像素，結束後恢復全解析度。
- 另存可選格式、原尺寸/半尺寸/四分之一/自訂尺寸及 JPEG 品質，另保留快速 PNG 儲存。優先使用相對 PMX 路徑，跨磁碟或模型未儲存時使用絕對路徑。
- 可匯出黑白遮罩、UV 線框及選區 Alpha PNG。選取 = 255，其他 = 0，取代原 Alpha；RGB 可選白色、原圖或調色結果。整圖調色時仍使用明確的 UV 選區，原貼圖及模型參照不變。
- 預設集支援改名、名稱/修改時間/手動排序及上移/下移。`presets/order.txt` 與 `sort.txt` 保留順序和排序方式。

| 格式 | Alpha 與壓縮 |
| --- | --- |
| PNG / TGA / DDS Raw | 無損、完整 Alpha |
| DDS DXT1 | 有損、二值 Alpha |
| DDS DXT3 / DXT5 | 有損、4 位元/插值 Alpha |
| JPEG / BMP / GIF / TIFF | 與白底合成；JPEG/GIF 有損，GIF 為索引色 |

介面及內建說明支援英、簡中、繁中、日文。既有自訂 `data` 說明不會自動覆寫。

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
