# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Commands

```bash
# 构建
dotnet build src/SSM/SSM.csproj

# 运行（需要管理员权限读取传感器）
# 在管理员 PowerShell 中：
cd src/SSM && dotnet run

# 发布
dotnet publish src/SSM/SSM.csproj -c Release -o build/publish
```

> **注意**：`app.manifest` 要求 `requireAdministrator`，必须以管理员身份运行，否则硬件传感器数据为空。调试 UI 时可临时将其改为 `asInvoker`。

## 架构概览

### 技术栈
- .NET 10 / WPF + Windows Forms（仅用于 `Screen.AllScreens` 多屏枚举）
- MVVM：`CommunityToolkit.Mvvm`（`ObservableObject`、`[ObservableProperty]`、`[RelayCommand]`）
- 托盘图标：`Hardcodet.NotifyIcon.Wpf`（已引入，未实现）

### 主题系统
主题通过 `App.SwitchTheme(string name)` 在运行时切换，原理是清空并重新加载 `Application.Resources.MergedDictionaries`：

```csharp
App.SwitchTheme("Dark");   // 加载 Themes/DarkTheme.xaml
App.SwitchTheme("Light");  // 加载 Themes/LightTheme.xaml
```

两个主题文件使用**完全相同的资源 Key**（`BgDeep`、`BgCard`、`Accent`、`TextPrimary` 等），所有 XAML 控件通过 `{StaticResource ...}` 引用 Key，切换时自动适配颜色。新增样式必须同时在 `DarkTheme.xaml` 和 `LightTheme.xaml` 中定义。`AppTheme.xaml` 是遗留文件，已不使用。

### 页面导航
`MainWindow` 没有用 `Frame`，而是在同一个 Grid 单元格内叠放四个视图，通过 `Visibility` 互斥切换：

| 视图名 | 类型 | 导航 Tag | 状态 |
|--------|------|----------|------|
| `DashboardView` | `ScrollViewer` | `"dashboard"` | 默认可见 |
| `ThemeView` | `ContentControl` → `ThemePage` | `"theme"` | 占位页 |
| `EditorView` | `ContentControl` → `EditorPage` | `"editor"` | 占位页 |
| `SettingsView` | `ContentControl` → `SettingsPage` | `"settings"` | 已实现 |

侧边栏每个导航按钮有对应的 `NavXxxIndicator`（3px 蓝色竖条）。`NavButton_Click` 统一用 `Vis(bool)` 辅助方法批量切换所有视图和指示条的 Visibility。

### 主题库页（ThemePage）— 待实现

> 文件：`Views/Pages/ThemePage.xaml`

计划功能：

- 横向可滚动的主题卡片列表，每张卡片展示主题缩略图预览、名称、分辨率标签
- 当前选中主题高亮（Accent 描边）
- 支持内置主题（随 exe 打包）与外部 `.ssm-theme` 文件导入
- 选中即应用到 `OverlayWindow`（无需重启投放）
- 数据模型建议：`OverlayTheme { string Name; string PreviewImagePath; string LayoutXamlPath; }`

### 主题编辑器页（EditorPage）— 待实现

> 文件：`Views/Pages/EditorPage.xaml`

计划功能：

- 左侧：组件面板（CPU 仪表、GPU 仪表、RAM 进度条、网络速率、时钟、自定义文本、图像）
- 中间：1:1 比例的 Overlay 画布预览，支持拖拽组件、调整大小
- 右侧：属性面板（选中组件的字体、颜色、透明度、数据源绑定）
- 底部工具栏：背景设置（纯色/渐变/图片）、导出为 `.ssm-theme`、保存草稿
- 实时预览：编辑时同步推送到正在运行的 `OverlayWindow`（若已开启投放）

### 数据流（待完成）
```
LibreHardwareMonitor → HardwareMonitorService → DataUpdated 事件
  → MainViewModel.HardwareData
  → MainWindow / OverlayWindow 更新 UI 控件
```
`HardwareMonitorService` 目前是空桩，使用 `System.Timers.Timer` 定时触发但不读取真实数据。接入 LibreHardwareMonitor 是下一个主要任务。

### 设置持久化
`SettingsService` 将 `AppSettings` 序列化为 `Assets/settings.json`（相对于 exe 目录）。`AppSettings` 包含：
- `ThemeName`（`"Dark"` / `"Light"`）
- `OverlayLayout`（`"Vertical"` / `"Horizontal"`）
- `TargetScreenIndex`、`RefreshIntervalMs`、`StartWithWindows` 等

`App.OnStartup` 中先 `Load()` 设置，再调用 `SwitchTheme` 应用已保存的主题，最后实例化 `MainWindow`。

### 开机自启
`StartupHelper` 通过注册表 `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` 管理自启项，`SettingsViewModel.Save()` 在保存时自动调用。

### 副屏投放
`OverlayWindow` 通过 `System.Windows.Forms.Screen.AllScreens[index]` 定位到目标显示器，设置 `Left/Top/Width/Height` 实现全屏覆盖。`AllowsTransparency="True"` + 半透明背景，`Topmost="True"` 防止被遮挡。
