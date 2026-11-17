
# Poke_token 1.1.7
Windows 上的 ChatGPT / Codex 桌宠。轻点精灵可以摸头，并打开漫画气泡查看当天 Token、美元和 credits 估算，以及每周剩余额度。

## 安装和使用

- 在 64 位 Windows 上解压到固定位置后，双击 `Install.cmd`。Windows 登录时会启动浮窗，ChatGPT 在前台时显示。
- 按住精灵身体拖动，在 ChatGPT 窗口范围内移动。右键点精灵打开菜单。
- 轻点精灵会播放短暂的摸头效果并在原位打开用量气泡，再点可收起。右键「用量详情」也可开关气泡。
- 右键选「调整大小」设置 65% 至 160% 的缩放。小窗口会限制实际显示尺寸。
- 右键选「切换宝可梦」，可以按世代筛选第一至第七世代共 809 只宝可梦，或按中文名、英文名和图鉴编号搜索。选中后可先看立绘，再点击「选择这只」。
- 点亮立绘下方的星标即可收藏，再点一次可取消。勾选「仅看收藏」可快速切换。关闭图鉴时会保存收藏。
- 立绘采用透明背景，并根据进化阶段和图鉴身高调整大小。
- 右键菜单也可以使用自选 PNG / JPEG、恢复默认位置和大小、临时隐藏或退出。
- 位置、缩放、所选宝可梦、收藏和自定义头像路径保存在 `%LOCALAPPDATA%\JieniToken\settings.json`。首次加载某只宝可梦的立绘需要联网，加载后可离线使用。图片缓存位于同目录下的 `kanto-artwork`。双击 `Uninstall.cmd` 可以停止程序并移除自启动。

## 数据来源

- **Today** 汇总本机日志里的输入和输出，按 Windows 本地日期过午夜重新累计。缓存是输入的一部分，命中率以缓存输入除以总输入。
- **美元与 credits** 是公开费率下的估算。模型费率在 `config/rates.json`。更新费率后重启生效。实际账单以账户为准。[API 费率](https://developers.openai.com/api/docs/pricing) · [ChatGPT credits](https://learn.chatgpt.com/docs/pricing)
- **周剩余** 读取最近一条七天 Codex 通用额度记录，用 100% 减去已用比例。悬停可以查看记录时间和重置时间。日志缺少七天窗口或记录已经到期时显示「—」。切换账户或工作区后，以新写入的记录为准。[Codex 额度字段](https://learn.chatgpt.com/docs/app-server#6-rate-limits-chatgpt)
- 日志目录优先使用 `CODEX_HOME`，默认使用 `%USERPROFILE%\.codex`。程序只读本机会话日志。
- 图鉴收录全国图鉴 #001–#809 的默认形态。中文名、英文名和属性取自 PokéAPI 数据，立绘来自 PokéAPI 的 official-artwork 与宝可梦官网图鉴。仅在遇到缺少缓存的立绘时请求图片，用量数据留在本机。[PokéAPI 数据](https://github.com/PokeAPI/pokeapi/tree/8a92bfa7dab2f12b84b5d151562f9857c133ff30/data/v2/csv) · [PokéAPI 图像来源](https://github.com/PokeAPI/sprites) · [宝可梦官网图鉴](https://www.pokemon.com/pokedex/)

## 源码结构

| 目录 | 职责 |
| --- | --- |
| `src/Desktop/` | WPF 入口、桌宠布局与交互。用量气泡在 `Views/UsageDetails.xaml`，菜单与设置窗口共享 `Views/ComicTheme.xaml`，拖动计算在 `Views/WindowGesture.cs` |
| `src/Core/` | Token、额度和汇总模型、增量汇总、金额计算 |
| `src/Infrastructure/Codex/` | 发现会话文件、增量读取 JSONL、解析周额度 |
| `src/Desktop/Pokemon/` | 图鉴选择器、立绘和摸头效果 |
| `src/Infrastructure/Pokemon/` | 加载图鉴数据，从固定来源下载并缓存立绘 |
| `src/Infrastructure/Configuration/` | 用户设置和费率配置的读写 |
| `src/Infrastructure/Windows/` | Win32 前台窗口识别和 DPI 感知定位 |
| `assets/` 与 `config/` | 第一至第七世代图鉴与独立费率表 |

依赖由桌面层指向数据读取层和 Core。立绘的尺寸和位置由 `MascotControl.cs` 控制。

## 开发和发布

先退出正在运行的精灵，在项目根目录打开 PowerShell：

```powershell
.\Build.ps1
Start-Process -FilePath .\CodexTokenPet.exe -ArgumentList '--preview' -Wait
.\Build.ps1 -Test
```

`Build.ps1` 使用 Windows 自带的 64 位 .NET Framework 编译器，将界面和图鉴数据嵌入 `CodexTokenPet.exe`。布局预览使用示例用量。`-Test` 检查跨日、周额度解析、窗口配置、图鉴数据及选择器界面的加载。用 `--report` 可以把本机用量和窗口诊断写入临时目录，便于本地排查。

可以用 `--preview 4 details` 预览小火龙和展开的用量气泡。

向 GitHub 提交修改时，将源码、资源、配置和脚本放入仓库。发布附件可包含编译好的 EXE、配置目录和安装脚本。提交交互改动时可以附一张预览，并说明测试过的拖动、缩放和重启场景。
