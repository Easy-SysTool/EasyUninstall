<h1 align="center">EasyUninstall</h1>

<p align="center">
  <img src="src/EasyUninstall/Images/Square150x150Logo.png" width="96" alt="EasyUninstall 图标"/>
</p>

**EasyUninstall** 是一款轻量级的 Windows 软件卸载工具，基于 WinUI 3 / Windows App SDK 构建，遵循 Fluent Design 设计语言，原生支持 Mica 材质与明暗主题。

> [!IMPORTANT]
> 本项目仍在开发中。欢迎提交 Issue 与 Pull Request。

## 功能特性

- **软件发现**：自动扫描桌面软件（注册表 Uninstall 项）与当前用户的 Microsoft Store 应用包
- **分类浏览**：全部软件 / 桌面应用 / 商店应用
- **搜索与排序**：按名称实时搜索；按名称、安装日期、发布者排序（支持升降序）
- **应用图标**：列表中显示提取自系统缓存的应用真实图标（本地缓存，秒开）
- **软件详情**：展示名称、版本、发布者、安装日期、安装路径
- **安全卸载**：调用软件自带的官方卸载程序，卸载后自动重新扫描确认结果
- **强制卸载**：对没有可验证卸载器的顽固软件，可提权强制结束进程、删除安装目录并清理卸载项（带安全护栏与副作用警告）
- **多语言**：简体中文 / English / 日本語，可在设置中切换
- **主题适配**：跟随系统 / 浅色 / 深色，Mica 窗口背景
- **状态反馈**：加载中、空列表、错误提示、卸载结果（成功 / 失败 / 取消 / 结果未知）

## 卸载安全

EasyUninstall 在设计上严格约束卸载行为，不做任何"粗暴删除"

**绝不**
> 直接删除软件安装目录、用户文件或注册表
> 将 `UninstallString` 交给 `cmd.exe` / PowerShell 执行
> 使用字符串拆分猜测可执行文件与参数

**安全措施**
> 独立的命令解析器验证每条卸载命令：拒绝 cmd / powershell / mshta / rundll32 / regsvr32 等危险宿主，可执行文件必须真实存在
> MSI 仅接受 `MsiExec.exe /X{GUID}` 模式并校验 GUID 格式
> 卸载命令有歧义或无法验证时，禁用卸载按钮并向用户说明原因
> 不默认静默卸载；启动卸载程序 ≠ 卸载成功，卸载后重新扫描清单确认
> 明确区分成功 / 失败 / 用户取消 / 结果未知四种状态
> 默认无需管理员权限；仅在卸载程序自身需要时按需提权（UAC）

### 强制卸载

任何桌面软件都可以选择**强制卸载**（即使它有可验证的官方卸载器）——这是显式的、需二次确认的高影响操作：

- 请求管理员权限（UAC）后执行：强行结束该软件的进程树 → 删除其安装目录 → 仅删除该软件**自己的**卸载注册表项
- 安装目录有严格的安全护栏（UI 与提权助手双重校验）：拒绝盘根、`C:\Windows` / `C:\Users` / `C:\ProgramData` 及其子目录、`WindowsApps`、自身目录与浅路径
- 确认对话框明确警告副作用：共享组件、文件关联、开机启动项与服务不会被清理；共用目录的其他软件可能受影响
- **系统组件保护**：识别为操作系统组件或平台运行库的软件（VC++ 运行库、.NET、Edge WebView、驱动包、更新补丁等）禁止强制卸载，按钮禁用并说明原因
- Store 应用不提供强制卸载（使用专用的包管理机制）

## 安装

### 系统要求

- Windows 10 17763+ / Windows 11
- 应用本身运行无需安装任何依赖（自包含部署）

### 从源码构建

```powershell
git clone https://github.com/yoyo-coding/EasyUninstall.git
cd EasyUninstall

# 构建（自动生成 MSIX 安装包）
dotnet build src/EasyUninstall/EasyUninstall.csproj -c Release -p:Platform=x64
```

生成的安装包位于：
`src/EasyUninstall/AppPackages/EasyUninstall_<版本>_x64_Test/EasyUninstall_<版本>_x64.msix`

### 安装 MSIX

生成的包未签名，首次安装需要创建并信任一个测试证书：

```powershell
# 1. 创建自签名证书（主题须为 CN=EasyUninstall）
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=EasyUninstall" `
  -KeyUsage DigitalSignature -FriendlyName "EasyUninstall" `
  -CertStoreLocation "Cert:\CurrentUser\My"

# 2. 导出证书并信任（.cer 只含公钥，可分发）
$pwd = ConvertTo-SecureString -String "<你的密码>" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath .\EasyUninstall.pfx -Password $pwd
Export-Certificate -Cert $cert -FilePath .\EasyUninstall.cer
Import-Certificate -FilePath .\EasyUninstall.cer -CertStoreLocation Cert:\LocalMachine\Root

# 3. 签名安装包
signtool sign /fd SHA256 /a /f .\EasyUninstall.pfx /p <你的密码> <包路径>.msix

# 4. 安装
Add-AppxPackage -Path <包路径>.msix
```

安装后从开始菜单启动 **EasyUninstall**。

## 运行测试

```powershell
dotnet test tests/EasyUninstall.Tests/EasyUninstall.Tests.csproj -p:Platform=x64
```

测试覆盖：卸载命令解析（MSI / 引号路径 / 危险宿主拒绝）、安装日期解析、图标路径解析、搜索 / 分类 / 排序逻辑、强制卸载安全护栏（危险路径拒绝 / 正常目录放行 / 指令序列化）。

## 项目结构

```
src/EasyUninstall/
├── Models/          数据模型（InstalledApp、AppCategory、UninstallOutcome）
├── Contracts/       接口定义（IAppSource、IAppCatalogService、IUninstallService…）
├── Services/        核心实现
│   ├── RegistryAppSource      注册表软件发现
│   ├── PackageAppSource       Store 应用发现
│   ├── AppCatalogService      合并、去重、搜索、排序、刷新
│   ├── UninstallCommandParser 卸载命令安全解析
│   ├── UninstallService       卸载编排与结果确认
│   ├── ForceUninstallService  强制卸载（提权助手、安全护栏）
│   ├── IconExtractService     应用图标提取与本地缓存
│   ├── LocalizationService    多语言（中 / 英 / 日）
│   ├── Win32Uninstaller       桌面软件卸载执行
│   ├── PackageUninstaller     Store 应用卸载执行
│   └── SettingsService        主题与语言设置持久化
├── ViewModels/      MVVM 视图模型（CommunityToolkit.Mvvm）
├── Views/           主窗口
├── Converters/      XAML 值转换器
└── Infrastructure/  依赖注入与基础设施
```

## 路线图

- [ ] 残留清理（快捷方式 / 文件关联 / 开机项）
- [ ] 批量卸载
- [ ] 软件更新检查
- [x] 多语言支持（中 / 英 / 日）
- [x] 强制卸载（带安全护栏）

## 参与贡献

欢迎提交 Issue 反馈问题或建议，Pull Request 请确保：

1. 代码可编译，现有测试通过
2. 新功能附带必要的单元测试
3. 遵循现有的 MVVM 架构与代码风格

## 许可证

[MIT](LICENSE)
