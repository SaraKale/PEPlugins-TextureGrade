# 更新记录 / Changelog

## 2026-09-21 — fork 工作版本

### 调色板颜色交换

- 原色列、目标色列分别支持拖动交换；普通单击保留选色器。固定左右列位置并显示行号、起点高亮、落点高亮及跟随提示。
- 拖至上下边缘渐进加速自动滚动；提供「点击交换」和右键发起交换，可松开鼠标后滚动到远处再选择目标。顶部显示待交换信息与取消按钮，Esc、丢失捕获或无效落点可取消拖动。
- 只交换所选列的颜色，另一列与各行启用、手动、明度锁定及插值方式保持原位。目标保存未量化颜色，在目标行按明度锁定及色域重新计算显示；反复交换不累积量化误差。
- 拖动仅命中检测、高亮与滚动，不写设置、不计算调色；放下后只更新两行色块，保持控件、焦点及滚动位置，一次通知触发异步预览并记为一步撤销。材质/参数变化或列表重建取消过期交换，成功交换取消未完成的自动识别。
- 补齐四语言提示、README 和内置说明；增加长调色板交互、滚动、撤销/重做、锁定明度及预设往返回归测试。宿主内鼠标捕获和拖动仍需 PMXEditor 实机确认。

### 选择性移植上游 v1.0.2

上游：[SaraKale/PEPlugins-TextureGrade](https://github.com/SaraKale/PEPlugins-TextureGrade)，提交 [f1a51a4c865602231d35784940e342bce5671c19](https://github.com/SaraKale/PEPlugins-TextureGrade/commit/f1a51a4c865602231d35784940e342bce5671c19)。对照本地基线 `314c198` 审阅差异后按功能移植，没有用整文件覆盖本地调色板主界面。

- 修复「已修改」徽章使用上次缓存参数导致误判的问题。
- 材质切换、重新读取保留 UV 选区和接收顶点。比上游仅比较面数更严格：比较顶点索引及 UV；也保留只有顶点标记、没有选面的状态。
- 左侧与编辑菜单增加反选；Ctrl+I 反选、Ctrl+A 全选，文字编辑时不接管。
- 另存按钮直接打开格式/尺寸/JPEG 品质选项，文件菜单保留快速 PNG 保存。支持 PNG、JPEG、BMP、GIF、TIFF、TGA、DDS Raw/DXT1/DXT3/DXT5。
- 采用上游格式编码器并适配：最终扩展名校验、覆盖确认、原图保护、原子写入、尺寸校验、Alpha 预乘缩放及奇数尺寸边缘处理。JPEG/BMP/GIF/TIFF 与白底合成；DXT1 的 Alpha 是二值。
- 预设增加改名、名称/修改时间/手动排序、上移/下移；自定义顺序去重，改名保留位置和参数，排序方式可重开恢复。
- 文件菜单增加选区 Alpha PNG；RGB 可选白色、原始贴图、调色结果，Alpha 显式替换为 UV 选区。整图调色状态不会把导出选区错误扩大。
- 增加 PMXEditor 单材质刷新兼容调用、保存错误提示和不可写目录的临时预览回退。对话框 owner 兼容 WinForms ElementHost。
- 保留本地相对贴图路径、预览关闭前恢复正式引用的实现；不引入上游按前缀批量删除预览文件的策略。

### 本分支已有功能

- 全局调色板重着色、1–128 色与自动主色数量，参考图框选与目标风格调色板。
- 按明度与占比配对，过渡色色相/彩度同步迁移，修复紫色残留。
- HSV/RGB/OKLCH 可选取色、独立 OKLCH 明度锁定；移除旧 Lab 环、渐变效果和统计迁移。
- 黑白化和可调阈值；密集 UV 绘制优化、整图调色范围、隐藏 UV 与清晰的工具分组。
- 完整调色快照、撤销/重做、分材质记忆与数值 JSON 预设；参考图移除后可重现已生成映射。
- .NET Framework 4.8，零新增 NuGet 包，可配置 PMXEditor 依赖路径，保留 ref 原始参考资料。

### 验证范围

独立算法与 WPF/WinForms 控件测试、真实 PEPlugin 接口的宿主替身测试、格式往返读取、选区及保存像素比较、预设排序/改名持久化及 Release 编译。自动化测试不等同于 PMXEditor 内的交互实测。

## Upstream integration notes

The fork selectively ports v1.0.2 features while retaining its palette/OKLCH pipeline, whole-texture scope, dense-UV rendering, relative texture paths and safe preview-reference cleanup. Selection restoration validates geometry, the format dialog is wired to the existing save button, and export/preset data are covered by regression tests. No release tag or upstream merge commit is created by this working-tree integration.
