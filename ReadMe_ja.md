# TextureGrade — PMXEditor テクスチャ調整プラグイン

[English](ReadMe.md) | [简体中文](ReadMe_sc.md) | [繁體中文](ReadMe_tc.md) | [日本語](ReadMe_ja.md)

元画像から非破壊でテクスチャを調整します。「モデルを更新」は一時プレビューを作成し、新規保存は現在の材質の参照先だけを変更します。

## 調色とパレット

- 露出、コントラスト、ハイライト、シャドウ、白黒点、ホワイトバランス、彩度、HSL、カーブ、レベル、RGB、明瞭度、シャープ。
- 連続階調の白黒化と、独立したしきい値強度・境界値（0–255）。旧グラデーション効果と Lab 色環は削除済みです。
- 元色→目標色の編集、元画像スポイト、手動追加、有効/削除/明度保持。再抽出は手動項目を保持します。
- 同じ列の色をドラッグして交換できます（元色同士・目標色同士）。もう一方の列はそのままです。行番号とドロップ先を表示し、上下端で自動スクロールします。離れた色には「クリックで交換」または右クリックの「別の色と交換」を使い、自由にスクロールして相手を選べます。Esc または上部のキャンセルで終了。通常のクリックはピッカーを開きます。各行の有効・手動フラグと明度保持は移動せず、交換先で明度・色域制約を適用します。交換は一回で元に戻せます。ドラッグ中は再計算せず、離した時だけ色見本を更新し非同期プレビューします。
- 元画像と参照画像の色数を個別に自動推定、または 1–128 色で手動指定できます。色差と面積から主要色群を推定し、微細な階調差や孤立ノイズを無視します。物体の意味的な領域分割ではありません。
- パレット内で参照画像を読み込み、必要なら矩形範囲を選択して生成します。相対明度、面積比、暗部から明部への順序を考慮し、元色数が十分なら全参照色を使用します。
- 制御色間の色相・彩度も変換し、元の色相の残留を減らします。旧プリセットの補間は維持され、再生成で修正版を適用します。
- ピッカーは HSV、RGB、OKLCH。明度保持は独立して切り替えられ、既定では画素ごとの OKLCH L を保ちます。色域外では彩度を下げます。8 ビット量子化や後段の調整による明度差は生じ得ます。
- パレット全体の補間を他の調整より先に適用します。手動の強度・半径はなく、統計転送機能は削除済みです。
- 履歴、材質別記憶、プリセットに対応。生成済みのパレットは参照画像なしで再現できます。

## UV、保存、プリセット

- ホイール拡縮、右ドラッグ移動、面クリック、UV 島ダブルクリック。Shift で追加、Ctrl で解除。PMXEditor と頂点選択を送受信できます。
- 左の選択ツールまたは編集メニューから反転（`Ctrl+I`）、`Ctrl+A` で全選択。文字入力中の編集ショートカットは保持します。
- 材質切替・再読込で UV 選択と受信頂点を復元します。形状や UV が変わった場合は古い選択を破棄します。
- 既定は選択部分のみ、選択が空なら画像全体を調整します。全選択の反転後も同じ規則です。「画像全体」は選択を保持して無視し、共有領域への影響を表示します。UV 非表示は選択や調色を変更しません。
- 大量の頂点はまとめた描画層を使用。ドラッグ中は長辺 1024 ピクセル、終了後は元の解像度で描画します。
- 新規保存で形式、元/半分/四分の一/任意サイズ、JPEG 品質を選択できます。クイック PNG 保存もあります。可能なら PMX からの相対パスを使用します。
- 白黒マスク、UV 線、選択 Alpha PNG を出力できます。選択部分の Alpha は 255、それ以外は 0 で元の Alpha を置換します。RGB は白/元画像/調整結果を選択。画像全体モードでも明示的な UV 選択を使用し、元画像やモデル参照は変更しません。
- プリセットの名前変更、名前/更新日時/手動順、上下移動に対応。順序と並び替え方法は `presets/order.txt` と `sort.txt` に保存します。

| 形式 | Alpha と圧縮 |
| --- | --- |
| PNG / TGA / DDS Raw | 可逆、完全な Alpha |
| DDS DXT1 | 非可逆、二値 Alpha |
| DDS DXT3 / DXT5 | 非可逆、4 ビット/補間 Alpha |
| JPEG / BMP / GIF / TIFF | 白背景へ合成。JPEG/GIF は非可逆、GIF はインデックス色 |

UI と内蔵説明は英語・簡体字・繁体字・日本語に対応。既存のカスタム `data` 説明ファイルは自動上書きしません。

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
