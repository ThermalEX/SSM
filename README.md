# SSM — Secondary Screen Monitor

副屏硬件监控软件，将 CPU、GPU、内存、网络等实时传感器数据渲染到副屏，并完整兼容 AIDA64 SensorPanel（`.sensorpanel` / `.sp2`）模板格式。

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![Framework](https://img.shields.io/badge/.NET-10-purple)
![UI](https://img.shields.io/badge/UI-WPF-blueviolet)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 下载

| 版本 | 说明 |
| --- | --- |
| `SSM-Setup-x.x.x.exe` | 安装版，含安装向导与卸载程序 |
| `SSM-Portable-x.x.x.zip` | 便携版，解压即用，无需安装 |

> 需要 Windows 10 / 11 x64，首次运行请**以管理员身份**启动（LibreHardwareMonitor 读取硬件传感器需要）。

---

## 功能特性

- **实时硬件监控仪表盘** — CPU / GPU 温度、使用率、时钟频率、风扇转速；内存用量；硬盘温度；网络上下行速率；主板温度
- **副屏 Overlay 投放** — 自动枚举多显示器，将渲染画面全屏覆盖到指定副屏；支持 0° / 90° / 180° / 270° 旋转及三种适配模式（默认 / 居中 / 拉伸）
- **兼容 AIDA64 模板格式** — 解析 `.sensorpanel`（UTF-8 / UTF-16 LE 自动检测）及 `.sp2` / `.spzip` 格式，渲染 IMG / LBL / SIMPLE / GAUGE / GRAPH / BAR 六种元素类型
- **主题库浏览** — 在内置实时预览控件中翻阅已安装主题，切换即应用到 Overlay
- **sp2 编辑器** — 三栏式编辑器（元素列表 / 可缩放滚动的渲染预览 / 属性面板），支持拖拽定位、上移 / 下移 / 删除，底部 Mock 数值实时驱动预览
- **主题导入** — 点击按钮或直接将文件拖入窗口（`.sensorpanel` / `.spzip` / `.sp2`），支持以管理员权限运行时的跨进程拖放（Win32 WM_DROPFILES）
- **可定制仪表盘布局** — 拖拽式卡片换位，布局持久化保存
- **深色 / 浅色主题** — 运行时切换，所有界面元素跟随变化
- **设置持久化** — 刷新频率、目标显示器、适配方式、开机自启等写入 JSON
- **开机自启** — 通过注册表 `HKCU\Run` 管理自启项

---

## 技术栈

| 模块 | 技术 |
| --- | --- |
| 桌面框架 | C# / .NET 10 / WPF |
| 硬件数据 | LibreHardwareMonitor |
| MVVM | CommunityToolkit.Mvvm |
| 多屏枚举 | Windows Forms `Screen.AllScreens` |
| 托盘图标 | Hardcodet.NotifyIcon.Wpf（已引入，待实现） |

---

## 项目结构

```text
SSM/
├── installer.iss                   # Inno Setup 安装包脚本
├── build.ps1                       # 一键构建脚本（发布 + 便携包 + 安装包）
├── make-icon.ps1                   # 程序图标生成脚本
├── src/SSM/
│   ├── App.xaml(.cs)               # 启动、主题切换
│   ├── Assets/
│   │   ├── Icons/app.ico           # 应用图标
│   │   └── settings.json           # 用户配置（自动生成）
│   ├── Core/
│   │   ├── Helpers/
│   │   │   ├── OverlayCanvasRenderer.cs   # sp2 渲染引擎（Overlay 与预览控件共用）
│   │   │   ├── Sp2Parser.cs               # 解析 UTF-16 LE 编码的 .sp2 文件
│   │   │   ├── Sp2Writer.cs               # 写回 .sp2 文件
│   │   │   ├── Sp2ThumbnailRenderer.cs    # 离线生成主题缩略图 BitmapSource
│   │   │   ├── SensorpanelImporter.cs     # 将 .sensorpanel 转换为 sp2 格式
│   │   │   ├── ThemeImporter.cs           # 统一导入入口（.sensorpanel / .spzip / .sp2）
│   │   │   ├── Win32FileDrop.cs           # Win32 WM_DROPFILES 跨权限拖放支持
│   │   │   └── UnitConverter.cs           # 温度/内存/网速单位换算
│   │   ├── Interfaces/
│   │   │   ├── IHardwareMonitorService.cs
│   │   │   └── ISettingsService.cs
│   │   ├── Models/
│   │   │   ├── HardwareData.cs     # CpuData / GpuData / MemoryData / NetworkData / StorageData / MotherboardData
│   │   │   ├── AppSettings.cs      # 含 OverlayFitMode 枚举
│   │   │   └── Sp2Models.cs        # Sp2Panel / Sp2Element
│   │   └── Services/
│   │       ├── HardwareMonitorService.cs  # LibreHardwareMonitor 封装，含 Storage / Motherboard SubHardware
│   │       ├── SettingsService.cs
│   │       └── StartupHelper.cs
│   ├── Themes/
│   │   ├── DarkTheme.xaml          # 深色资源字典
│   │   └── LightTheme.xaml         # 浅色资源字典
│   ├── Themes/Monitor/             # 已安装的 sp2 主题目录（每个子文件夹一套主题）
│   │   └── template/               # 内置默认模板
│   └── Views/
│       ├── MainWindow.xaml(.cs)    # 主窗口：仪表盘、侧边栏导航、拖拽布局
│       ├── OverlayWindow.xaml(.cs) # 副屏全屏 Overlay
│       ├── ThemeEditorWindow.xaml(.cs)  # sp2 编辑器（实时渲染 + Mock 数值 + 缩放滚动）
│       ├── Controls/
│       │   └── OverlayPreviewControl    # 主题库实时预览控件
│       └── Pages/
│           ├── ThemePage.xaml(.cs)      # 主题库页
│           ├── EditorPage.xaml(.cs)     # 编辑页（主题卡片列表）
│           └── SettingsPage.xaml(.cs)   # 设置页
```

---

## 快速开始

### 环境要求

- Windows 10 / 11（x64）
- .NET 10 SDK
- 管理员权限（LibreHardwareMonitor 读取硬件传感器需要）

### 构建运行

```bash
git clone <repo-url>
cd SSM
dotnet restore src/SSM/SSM.csproj

# 以管理员身份运行（否则传感器数据为空）
dotnet run --project src/SSM/SSM.csproj
```

> **调试 UI 时**可将 `app.manifest` 中的 `requireAdministrator` 改为 `asInvoker`，无需管理员即可启动。

### 打包发布

需要提前安装 [Inno Setup 6](https://jrsoftware.org/isdl.php)，然后在项目根目录运行：

```powershell
.\build.ps1
```

在 `build\installer\` 下生成安装版（`.exe`）与便携版（`.zip`）。

---

## sp2 主题格式

兼容 AIDA64 SensorPanel 的模板文件。支持：

- `.sensorpanel` — AIDA64 原生导出格式，UTF-8 或 UTF-16 LE 编码自动检测
- `.sp2` — SSM 内部格式（UTF-16 LE）
- `.spzip` — 含完整资源的压缩包

### 支持的元素类型

| 类型 | 说明 |
| --- | --- |
| `IMG` | 背景图片或装饰图片，`BGIMG=1` 时全画布拉伸 |
| `LBL` | 静态文字标签 |
| `SIMPLE` | 单传感器数值（标签 + 数值 + 单位） |
| `GAUGE` | 帧动画仪表（`STAFLS` 帧图片列表，按数值取帧） |
| `GRAPH` | 历史滚动条形图（60 帧环形队列） |
| `BAR` | 传感器数值文本（含标签前缀） |

### 传感器 ID 映射（部分）

| ID | 含义 |
| --- | --- |
| `TCPU` / `TCPUDIO` | CPU 温度 °C |
| `SCPUUTI` | CPU 使用率 % |
| `SCPUCLK` | CPU 频率 MHz |
| `TGPU1` | GPU 温度 °C |
| `SGPU1UTI` | GPU 使用率 % |
| `SGPU1CLK` | GPU 频率 MHz |
| `SRAMUTI` | 内存使用率 % |
| `SNIC1DLRATE` | 网络下行 B/s |
| `SNIC1ULRATE` | 网络上行 B/s |
| `THDD1` / `TDTS` | 硬盘 / M.2 温度 °C |
| `TMOBO` | 主板温度 °C |
| `FCPU` / `FCHA1` | CPU / 机箱风扇转速 RPM |
| `SMASTVOL` | 系统主音量 % |

---

## 主题编辑器

通过「编辑器」页 → 点击主题卡片上的「编辑」按钮打开 sp2 编辑器窗口。

- **左侧**：元素列表，支持上移 / 下移 / 删除
- **中间**：渲染预览画布，按 Ctrl + 滚轮缩放，超大画布可滚动；自动 ZoomFit 适配面板尺寸
- **右侧**：属性面板（位置、大小、SensorId、字体、颜色、数值范围等）
- **底部**：Mock 传感器数值输入栏，实时驱动预览中的显示内容

---

## 路线图

### 已完成

- [x] 实时硬件数据采集（CPU / GPU / 内存 / 网络 / 硬盘 / 主板 / 风扇 / 音量）
- [x] AIDA64 `.sensorpanel` / `.sp2` / `.spzip` 解析与渲染
- [x] 副屏 Overlay 投放（旋转 + 适配方式）
- [x] 主题库浏览与一键应用
- [x] sp2 编辑器（元素列表、属性编辑、实时预览、Mock 数值）
- [x] 主题导入（文件选择器 + Win32 跨权限拖放）
- [x] 仪表盘卡片拖拽排序
- [x] 深色 / 浅色主题切换
- [x] 设置持久化（JSON）+ 开机自启

### 计划中

- [ ] 系统托盘图标（最小化到托盘、右键菜单快速控制）
- [ ] 编辑器：新增元素（文字、传感器值、仪表、图片）
- [ ] 编辑器：导出为 `.sp2` / 打包为 `.spzip`
- [ ] 编辑器：多选 + 对齐辅助线
- [ ] 更多 AIDA64 传感器 ID 支持（电压、功耗、PCIe 带宽）
- [ ] 全局热键：一键开关 Overlay

---

## 提交规范

格式：`<type>: <中文描述>`

| type | 用途 |
| --- | --- |
| `feat` | 新功能 |
| `fix` | 缺陷修复 |
| `refactor` | 重构（不改变行为） |
| `chore` | 构建 / 依赖 / 配置 |
| `docs` | 仅文档变更 |

---

## Collaborators

- ThermalEX
- slothtata-2004
