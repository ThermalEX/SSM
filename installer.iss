#define AppName    "SSM"
#define AppFullName "SSM — Secondary Screen Monitor"
#define AppVersion  "1.0.0"
#define AppPublisher "ThermalEX and slothtata-2004"
#define AppExeName  "SSM.exe"
#define PublishDir  "build\publish"

[Setup]
AppId={{A3F2C8D1-4B7E-4F9A-8C3D-2E6B1A5F0D9C}
AppName={#AppFullName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=build\installer
OutputBaseFilename=SSM-Setup-{#AppVersion}
SetupIconFile=src\SSM\Assets\Icons\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
; 要求管理员安装（和 app.manifest 保持一致）
PrivilegesRequired=admin
; 仅 64 位 Windows 10+
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
; 主程序及所有依赖（发布目录）
Source: "{#PublishDir}\*"; DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs; \
    Excludes: "Themes\Monitor\*"

; 只打包内置默认主题
Source: "{#PublishDir}\Themes\Monitor\template\*"; \
    DestDir: "{app}\Themes\Monitor\template"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; \
    Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; \
    Description: "立即启动 {#AppName}"; \
    Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallDelete]
; 卸载时删除运行时生成的配置文件（可选，注释掉则保留）
; Type: files; Name: "{app}\Assets\settings.json"
; Type: filesandordirs; Name: "{app}\Themes\Monitor"
