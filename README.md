# ZipCraft（小压匠）· 交付说明

面向普通用户的 Windows x64 简体中文压缩工具。参考 WinRAR 使用习惯，首页突出“压缩文件 / 解压文件”，并在文件右键菜单提供直接操作入口；高级功能按需展开。技术栈：.NET 9 WPF + 命令行调用内置 7-Zip 后端。

交接日期：2026-11-04。默认界面语言：简体中文。

---

## 1. 目录结构

```
ZipCraft/                    源码工程
├─ ZipCraft.csproj           项目文件（net9.0-windows, WPF）
├─ app.manifest                asInvoker（免管理员）+ PerMonitorV2 DPI
├─ App.xaml / App.xaml.cs      入口、命令行分发（右键菜单由此启动真实任务）
├─ Models/                     AppSettings / ArchiveTask（枚举与请求/结果模型）
├─ Services/
│  ├─ EngineLocator.cs         发现 7z.exe（配置→随包 tools→已知 PeaZip→系统 7-Zip→注册表）
│  ├─ ArchiveEngine.cs         封装 7z：压缩/解压/测试/列表、进度、取消、错误分类、密码脱敏
│  ├─ PathGuard.cs             解压越界/绝对路径判定与规范化、目录可写检查
│  ├─ SettingsStore.cs         JSON 持久化（优先程序目录 settings.json）
│  └─ ShellIntegration.cs      HKCU 右键菜单注册/移除
├─ Views/                      MainWindow / Compress / Extract / Task / Browse / More / Settings / PasswordPrompt
└─ tools/7z/                   随包引擎（7z.exe + 7z.dll + Codecs）

dist/ZipCraft/                已发布绿色包（可直接运行）
```

## 2. 构建方法

前置：已安装 .NET 9 SDK（本机为 9.0.316）与 Windows Desktop 运行时。

```powershell
cd d:\AIWorks\压缩软件\ZipCraft
dotnet build   -c Release                                    # 仅编译验证
dotnet publish -c Release --self-contained false `
    -o "d:\AIWorks\压缩软件\dist\ZipCraft"                   # 生成绿色包
```

> 说明：本开发环境 NuGet 源为离线源，缺少 `Microsoft.*.Runtime.win-x64` 运行时包，
> 因此**无法离线 `--self-contained true`**。当前交付为“框架依赖”绿色包，需目标机装有
> .NET 9 Desktop 运行时（本机已具备）。若要真正免运行时的自包含包，在可联网机器上执行：
> `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o dist\ZipCraft`

`tools\7z` 会通过 `<None>` + `CopyToOutputDirectory` 自动随包输出，无需手工拷贝。

## 3. 运行说明

双击 `dist\ZipCraft\ZipCraft.exe`。

- 首页两大按钮：**压缩文件**、**解压文件**；中部为拖放区，底部为“设置 / 打开压缩包 / 更多功能”。
- 拖入：仅压缩包 → 进入解压；含普通文件/文件夹或混合 → 进入压缩（混合时先确认）。
- 压缩：名称（随格式自动同步扩展名、避免重复后缀）、格式 ZIP/7Z、保存位置（系统目录选择器，默认首个项目父目录）、密码（可显示、7Z 可加密文件头）、高级（标准/快速/最大等级、分卷 100MB/700MB/1GB/4.7GB）。
- 解压：默认解压到压缩包旁同名文件夹、“完成后打开文件夹”默认勾选；高级含同名文件处理（每次询问/保留两份/跳过/替换，询问支持“应用到全部”）。多压缩包分别解压到各自同名文件夹。
- 密码解压：需要密码时自动请求；错误密码在结果窗口提供“重新输入密码”重试。
- 结果窗口显示真实进度、当前文件、可折叠日志（密码脱敏）、取消按钮；成功显示结果路径并可打开文件夹。

## 4. 右键菜单：注册与移除

- **注册/移除入口**：应用内 **设置 → “在文件右键菜单显示压缩和解压”** 勾选=注册，取消=移除。写入 `HKCU\Software\Classes`，**免管理员**。
- 注册的菜单项：
  - 普通文件 / 文件夹：`压缩为 ZIP`、`压缩到……`
  - 压缩包（.zip/.7z/.rar/.tar/.gz/.bz2/.xz/.iso/.zst/.cab）：`解压到同名文件夹`、`解压到当前文件夹`、`解压到……`、`打开压缩包`
- 菜单命令形如：`"…\ZipCraft.exe" --cmd <动词> "%1" %U`，由 `App.OnStartup` 解析后直接执行真实任务。
- **手动移除**（若需彻底清理）：删除上述 `HKCU\Software\Classes` 下以 `SimpleCompress.` 开头的 `shell` 子项。
- **移动程序后**：注册项记录了注册时的 exe 绝对路径，移动后请在设置里关闭再重新启用以刷新路径。

### Windows 11 呈现位置（实测说明）
本工程采用传统注册表 `shell` 命令（非 COM 稀疏图标/Ext shellex），这些命令出现在 Windows 11 的 **“显示更多选项”**（经典右键菜单）中，而非折叠前的一级 Pinned 菜单。一级菜单常驻需实现 `IExplorerCommand` 打包扩展（更高集成成本，当前未做）。目标用户机器建议通过“设置”这一明确入口开启，而非静默注入。

## 5. 引擎发现与依赖许可

- **引擎来源**：随包 `tools\7z\7z.exe`（7-Zip **26.04 x64**，Igor Pavlov， LGPL/bundled 由 PeaZip 11.4.0 便携版 `res\bin\7z` 提供）。
- **发现优先级**：用户配置路径 → 随包 `tools\7z` → 已知 PeaZip 目录 `D:\Tools\PeaZipPortable\...` → 系统 7-Zip（Program Files / 注册表）。**不写死单一路径**，可在设置覆盖。
- “更多功能 → PeaZip 完整界面”通过 `EngineLocator.LocatePeaZip` 定位 `peazip.exe`（配置或已知路径），缺失时给中文提示。
- **许可**：
  - 7-Zip：author Igor Pavlov， LICENSE 见 7-Zip 官方（ LGPL 兼容、含 unRAR 内容遵循 unRAR license —— 不得用于再实现 RAR 压缩算法，仅解压）。
  - PeaZip / 便携分发：官方允许个人、商业使用；再分发需遵循其许可。参考 https://peazip.github.io/peazip-misc.html
  - 本项目随包复制的是 PeaZip 便携版内的 7-Zip 二进制，用途为“调用成熟引擎”，未修改引擎本体。
  - 交付前应核对实际所用版本许可文件并随包附 `LICENSE`。

## 6. 实际测试结果（本机 Windows x64）

构建：`dotnet build -c Release` → **0 错误 / 1 无害警告**（未使用字段）。
发布：`dotnet publish --self-contained false` → **成功**，`dist\ZipCraft\ZipCraft.exe` 双击启动后进程稳定存活，未崩溃。

引擎端到端（使用与 App 相同的 7z 参数形态，含中文+空格路径）：

| 用例 | 结果 |
| --- | --- |
| ZIP 创建（标准 -mx5，输入含“源 目 录/中文文件 名.txt、子 目 录/内部.txt”） | 成功，包内路径与中文/空格完整保留 |
| 7Z 创建（最大 -mx9 + 密码 + `-mhe=on` 加密头） | 成功，`Method = LZMA2:12 7zAES`，`Everything is Ok` |
| ZIP 解压到目录 | 成功，2 个文件、嵌套结构保留 |
| 7Z 正确密码解压 | 成功，2 个文件还原 |
| 7Z 错误密码解压 | 正确失败（0 文件产出），触发密码错误分类 |
| 7z 读取 RAR 能力 | 支持（`7z i` 列出 Rar/Rar5 + Rar1–5 编解码） |

> 说明：GUI 的逐项点击验收（拖放、冲突弹窗、设置持久化后重开生效等）需在可视化环境人工完成；引擎与发布链路已用脚本验证。

## 7. 已实现 / 尚未支持

已实现（对齐验收清单核心项）：主界面两大入口、文件与多选/文件夹选择、真实 ZIP/7Z 创建、ZIP/RAR/7Z 解压、名称/格式/目标可改且与输出一致、密码压缩与解密+错误重试、进度/取消/中文错误分类（密码/空间/占用/损坏/不支持）、完成后打开目录、同名文件四策略+应用到全部、右键注册/移除、配置持久化、拖放自动分类、浏览内容/测试完整性/打开 PeaZip。

已知限制 / 待完善：
- Windows 11 一级菜单常驻需 COM 扩展（当前为“显示更多选项”内命令）。
- 交付为框架依赖包（离线无法自包含），目标机需 .NET 9 Desktop 运行时。
- “保留两份”冲突处理针对文件；跨卷 `.z01/.r00` 解压为“首卷传参”由 7z 自动识别同源分卷。
- 单实例：多次右键可能启动多进程各自执行（互不影响，符合“多任务独立”，但未做窗口级合并）。
- 图标/品牌占位（“▤”）；项目名 ZipCraft，中文名“小压匠”。
- GUI 点击级回归与高 DPI 实测需在交互环境补做。
