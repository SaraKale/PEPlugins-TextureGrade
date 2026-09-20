# TextureGrade — PMXEditor 贴图调色插件

[English](ReadMe.md) | [简体中文](ReadMe_sc.md) | [繁體中文](ReadMe_tc.md) | [日本語](ReadMe_ja.md)

在 PMXEditor 内预览、调整模型贴图。使用原始贴图进行非破坏式调色；刷新模型用于 3D 预览，另存后仅更新当前材质的贴图引用。

## 调色与调色板

- 曝光、对比度、高光、阴影、白黑场、白平衡、饱和度、HSL、曲线、色阶、RGB、清晰度与锐化。
- 黑白化为连续灰阶；阈值强度与黑白分界值独立设置，分界值范围 0–255。旧渐变功能和 Lab 取色环已移除。
- 调色板支持原色→目标色映射、原图吸管、启用/删除、手动添加以及重新识别时保留手动项。
- 同列色块可以拖动交换：原色↔原色、目标色↔目标色，另一列保持原位。拖动时显示行号和落点提示，上下边缘自动滚动。远距离可启用「点击交换」或右键「与另一色块交换」，选中后自由滚动再点另一色块；Esc 或上方取消按钮退出。普通单击仍打开选色器。各行的启用、手动标记与明度锁定保持原位，目标色按所在行重新约束明度和色域；不交换整行。交换可一步撤销，拖动过程不重算贴图，放下后只更新相关色块并异步预览。
- 原图与参考图可分别选择自动估计主色数量，或手动指定 1–128 色。自动数量按感知色差和有效面积估计，忽略细微渐变与孤立噪点，并非物体语义分割。
- 在调色板内导入参考图，可框选取样。点击生成后，按相对明度、色块占比与明暗顺序配对；控制色足够时覆盖全部参考主色。
- 参考映射同时转换过渡色的色相和彩度，减少原图底色残留。旧预设保留原有插值；重新生成参考调色板可使用修正后的迁移。
- HSV、RGB、OKLCH 三种选色方式均可使用；明度锁定独立开关，默认保留每像素 OKLCH 感知明度。色域约束固定 L 和色相、降低彩度。8 位量化和后续曝光等效果仍可能改变最终明度数值。
- 调色板在其它调色之前执行，不设手动影响半径或强度限制；颜色由全局插值共同作用。统计迁移已移除。
- 调色参数、调色板、范围和自动数量支持撤销/重做、分材质记忆、预设。生成后的参考映射不依赖原参考文件。

## UV 与材质

- 滚轮缩放，右键平移；单击选面，双击/选连通块选择 UV 岛；Shift 加选，Ctrl 减选。可接收和发送 PMXEditor 顶点选区。
- 左侧「选择」栏或编辑菜单可以反选，快捷键 `Ctrl+I`；`Ctrl+A` 全选 UV。文本框内保留原有文字编辑快捷键。
- 材质切换和重新读取会恢复各自的 UV 选区与接收顶点标记；几何索引或 UV 已改变时丢弃旧选区，避免套错位置。
- 默认「UV 选区优先」：有选区则局部调色，没有选区则整张贴图。反选全部得到空选区后，同样遵循此规则。
- 「整张贴图」暂时忽略选区，提示共用贴图及材质选中顶点之外的影响范围。隐藏 UV 只隐藏线框/标记，不改变选区或调色。
- 大量顶点以合并绘图层显示；拖动使用最长边 1024 像素的临时预览，结束后恢复全分辨率。

## 保存、蒙版和预设

1. 「刷新模型」生成临时预览；原目录不可写时回退到系统临时目录。
2. 「另存为新贴图」选择格式、原尺寸/半尺寸/四分之一/自定义尺寸和 JPEG 质量。文件菜单保留「快速另存为 PNG」。
3. 保存优先写入相对当前 PMX 目录的路径，跨盘或模型未保存时使用绝对路径。保存结果与最终调色使用同一管线；格式压缩及缩放可能改变像素。
4. 文件菜单可导出黑白蒙版、UV 线框以及「选区 Alpha 通道 PNG」。Alpha 由显式 UV 选区生成：选中 255，其余 0；替换原 Alpha，RGB 可选纯白/原图/调色结果。即使处于整图调色范围，也只导出实际选区，不改变原贴图或模型引用。
5. 预设可保存、应用、删除、改名，并按名称、修改时间或手动顺序排序；上移/下移保存到 `presets/order.txt`，排序方式记在 `sort.txt`。改名保留全部参数和调色板。

| 导出格式 | Alpha 与压缩 |
| --- | --- |
| PNG / TGA / DDS Raw | 无损，保留完整 Alpha |
| DDS DXT1 | 有损，Alpha 仅透明/不透明两级 |
| DDS DXT3 / DXT5 | 有损，Alpha 分别为 4 位/插值编码 |
| JPEG / BMP / GIF / TIFF | 与白底合成；JPEG/GIF 有损，GIF 使用索引色 |

界面及内置说明支持英、简中、繁中、日文。旧的自定义 `data` 说明文件不会自动覆盖，可参照本 README 更新。

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
