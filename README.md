# SSM — Secondary Screen Monitor

副屏性能监控软件，将 CPU、GPU、内存、网络等实时数据投放到副屏上显示。

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![Framework](https://img.shields.io/badge/.NET-8.0-purple)
![UI](https://img.shields.io/badge/UI-WPF-blueviolet)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 功能特性

- **实时硬件监控** — CPU/GPU 温度、使用率、内存、风扇转速、硬盘、网络
- **副屏全屏投放** — 自动识别多显示器，一键投放到指定屏幕
- **主题系统** — 支持 JSON 配置主题，内置多套预设风格
- **可视化编辑器** — 拖拽式主题编辑器，自定义组件布局与样式
- **系统集成** — 开机自启、托盘图标、窗口置顶防误触
- **MVVM 架构** — 界面与数据分离，易于维护和扩展

---

## 技术栈

| 模块 | 技术 |
|------|------|
| 桌面框架 | C# / .NET 8 / WPF |
| 硬件数据 | LibreHardwareMonitor |
| MVVM 框架 | CommunityToolkit.Mvvm |
| 图表组件 | LiveCharts2 |
| 托盘图标 | Hardcodet.NotifyIcon.Wpf |
| 日志 | Serilog |
| 打包发布 | dotnet publish + Inno Setup |

---

## 项目结构

```
SSM/
├── src/
│   └── SSM/
│       ├── Assets/
│       │   ├── Icons/          # 应用图标资源
│       │   └── Themes/         # 内置主题 JSON 文件
│       ├── Core/
│       │   ├── Interfaces/     # 服务接口定义
│       │   ├── Models/         # 数据模型
│       │   └── Services/       # 业务逻辑服务
│       ├── Controls/           # 自定义 WPF 控件（仪表盘、图表等）
│       ├── Helpers/            # 工具类（多屏检测、开机启动等）
│       ├── Themes/             # WPF 资源字典（配色、样式）
│       ├── ViewModels/         # MVVM ViewModel 层
│       └── Views/
│           └── Pages/          # 设置页、主题编辑器页
├── docs/
│   └── screenshots/            # 截图文档
└── build/                      # 构建脚本与输出
```

---

## 快速开始

### 环境要求

- Windows 10 / 11（x64）
- .NET 8 SDK
- Visual Studio 2022 或 VS Code + C# Dev Kit

### 构建运行

```bash
git clone https://github.com/your-username/SSM.git
cd SSM
dotnet restore src/SSM/SSM.csproj
dotnet run --project src/SSM/SSM.csproj
```

> **注意**：读取 CPU/GPU 温度等传感器数据需要**管理员权限**，请以管理员身份运行。

---

## 数据说明

| 数据项 | 来源 | 刷新频率 |
|--------|------|----------|
| CPU 温度 / 使用率 | LibreHardwareMonitor | 可配置（默认 1s）|
| GPU 温度 / 显存 | LibreHardwareMonitor | 可配置（默认 1s）|
| 内存使用 | LibreHardwareMonitor | 可配置（默认 1s）|
| 风扇转速 | LibreHardwareMonitor | 可配置（默认 1s）|
| 网络流量 | LibreHardwareMonitor | 可配置（默认 1s）|

---

## 主题配置

主题以 JSON 文件存储，路径：`Assets/Themes/`

```json
{
  "name": "Dark Blue",
  "background": "#0D1117",
  "accentColor": "#58A6FF",
  "fontFamily": "Segoe UI",
  "fontSize": 14,
  "components": []
}
```

---

## 路线图

- [x] 项目结构搭建
- [ ] 硬件数据采集模块
- [ ] 副屏投放模块
- [ ] 主题系统与预设主题
- [ ] 自定义显示组件（仪表盘、折线图）
- [ ] 主题可视化编辑器
- [ ] 开机自启 & 托盘图标
- [ ] 安装包发布

---

## License

MIT © 2026
