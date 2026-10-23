<p align="center">
  <img src="src/SoundConsole/Assets/logo.png" width="96" height="96" alt="Sound Console 图标" />
</p>

<h1 align="center">声卡控制台 <sub>Sound Console · Signal Desk</sub></h1>

<p align="center">
  把吉他效果器 / USB 音频接口的输入，路由到电脑上的耳机、音箱、DAC 或虚拟声卡。
</p>

<p align="center">
  <a href="README.md">English</a> ·
  <a href="https://github.com/MungRed/sound-console/releases/latest">下载最新版本</a> ·
  <a href="docs/ARCHITECTURE.md">架构说明</a> ·
  <a href="docs/BUILDING.md">构建说明</a> ·
  <a href="docs/TROUBLESHOOTING.md">故障排查</a>
</p>

<p align="center">
  <a href="https://github.com/MungRed/sound-console/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/MungRed/sound-console/actions/workflows/ci.yml/badge.svg" /></a>
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-5DE2AE.svg" /></a>
  <a href="https://dotnet.microsoft.com/"><img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4.svg" /></a>
</p>

---

Sound Console 是一个轻量的 Windows 桌面音频路由台。它通过 WASAPI 采集 **Ampero II Stomp** 或其他 USB 音频接口的输入，再低延迟地送到一个或多个播放设备；共享模式下，电脑里的其他声音也可以同时从同一副耳机/音箱播放。

程序界面目前为中文，英文界面本地化欢迎贡献。

![声卡控制台界面](docs/preview.png)

## 适合谁

- 用 Ampero II Stomp、综合效果器或 USB 声卡连接电脑，希望从电脑耳机/音箱/DAC 监听的人。
- 需要把效果器声音同时送往多个输出，或送往 OBS / DAW / 会议软件的人。
- 希望用图形界面管理输入输出、电平、增益和缓冲，而不想打开一整套 DAW 的人。

典型链路：

```text
效果器 → USB 音频 → 电脑（Sound Console） → 耳机 / 音箱 / USB DAC / 虚拟声卡
```

## 功能

- 自动发现当前可用的 WASAPI 输入/输出设备，自动识别 Ampero 并优先作为输入。
- 多声道输入选择：8 声道设备可选 `1+2`、`3+4`、`5+6`、`7+8`。
- 同时路由到最多 8 个输出，每路独立音量、静音和实时电平表。
- 输入增益 −24 ~ +12 dB（0.5 dB 步进），双声道峰值表，CLIP 指示。
- 30 / 40 / 60 / 100 / 160 ms 安全缓冲，默认 40 ms。
- 共享输出 / 独占输出；独占模式失败时自动回退共享，并在界面提示。
- 独立时钟漂移校正和三次插值重采样，长时间运行不会越积越延迟。
- 设备热插拔检测、`F5` 刷新、`Ctrl+Enter` 开始/停止、设置自动保存。
- 单实例运行、错误提示、日志、关闭时自动淡出并释放设备。
- 无遥测、无网络请求、不录制音频。

## 下载与运行

1. 打开 [Releases](https://github.com/MungRed/sound-console/releases/latest)，下载最新的 `SoundConsole-*-win-x64.zip` 并解压。
2. 双击 **`声卡控制台.exe`**。发布包是自包含程序，Windows 10/11 x64 无需另装 .NET，也不需要管理员权限。
3. 用 USB 连接 Ampero，确认效果器的 USB Audio 输出已开启。
4. 在“输入源”选择 **Ampero II Stomp**，先尝试 **声道 1 + 2**。
5. 在“输出设备”勾选耳机、音箱或 DAC；可以多选。
6. 点击 **开始路由**，弹奏并观察输入电平。建议峰值保持在 **−12 dBFS 左右**，出现 `CLIP` 时降低输入增益。

> 路由运行中仍可调整输入增益、各输出音量和静音。切换设备、声道、缓冲或独占模式前，请先停止路由。

## 使用说明

### Ampero 的声道怎么选

Ampero II Stomp 是 8 进 8 出的 USB 音频接口。不同固件和效果器内部路由下，通道含义可能不同：

- 通常 **1 + 2** 是处理后的主输出。
- 如果 1+2 没有声音，可依次尝试 **3+4**、**5+6**、**7+8**。
- 请同时在 Ampero 的 USB Audio 设置里确认输出路由和音量。

### 共享模式与独占模式

| 模式 | 行为 | 适用场景 |
| --- | --- | --- |
| 共享（默认） | 不占用设备，电脑和其他程序仍可播放；系统声音与吉他声混合。 | 日常监听、看视频、直播、虚拟声卡 |
| 独占 | 绕过 Windows 混音器，降低一层延迟；其他程序可能暂时无法使用该设备。 | 专用输出上的低延迟监听 |

设备不支持独占或正被其他程序占用时，程序会自动改用共享模式并给出提示。

### 延迟与缓冲

实际延迟 = 应用缓冲 + WASAPI 设备缓冲 + USB/驱动延迟。建议先使用 **40 ms + 共享模式**；如果需要更低延迟，可尝试 **30 ms + 独占模式**。蓝牙耳机和虚拟声卡通常需要 60 ms 或更高。

这是电脑端路由工具，不是零延迟硬件监听。需要最低延迟时，优先使用效果器自带直接监听或专业 ASIO 声卡。

### 送给 OBS / DAW / 会议软件

Windows 不能直接把一个播放设备变成另一个程序的麦克风。请安装虚拟音频线（例如 [VB-CABLE](https://vb-audio.com/Cable/) 或 VoiceMeeter）：

1. 在本程序中把虚拟声卡的 **播放端** 选为输出（如 `CABLE Input`）。
2. 在 OBS / DAW / 会议软件里选择虚拟声卡的 **录音端**（如 `CABLE Output`）作为输入。
3. 可同时勾选真实耳机输出，实现本地监听 + 软件采集。

## 数据与日志

- 设置：`%LOCALAPPDATA%\SoundConsole\settings.json`
- 日志：`%LOCALAPPDATA%\SoundConsole\logs\`
- 界面底部的“日志 / 数据目录”按钮可以直接打开对应文件夹。
- 设置文件损坏时会自动备份并恢复默认值。
- 日志只包含设备名称和错误信息，不包含音频内容。

## 常见问题

**开始路由后没有声音**

1. 看左侧输入电平：显示 `−∞` 说明电脑没有收到 Ampero 的 USB 音频，检查 USB 线、效果器 USB Audio 设置和输出路由。
2. 输入有电平但没声音：检查输出设备是否选对、输出是否静音、Windows 中该设备的音量是否被调低。
3. 换一个输入声道组合（`1+2` → `3+4`）再试。
4. 关闭可能独占 Ampero 的 ASIO 软件，然后按 `F5` 刷新。

**独占模式无法启动**

设备可能被其他程序占用或不支持独占。关闭占用程序，或改用共享模式。

**独占模式下电脑没有提示音**

这是正常行为：独占模式占用了输出设备，系统提示音和其他应用可能听不到。

**出现 CLIP**

输入信号已达到 0 dBFS。降低输入增益或效果器输出音量。CLIP 指示灯会保持一小段时间以便观察。

更多内容见 [故障排查文档](docs/TROUBLESHOOTING.md)。

## 从源码构建

需要 Windows 10/11 和 **.NET 10 SDK**：

```powershell
dotnet restore
dotnet build SoundConsole.sln -c Release
dotnet test SoundConsole.sln -c Release

# 生成自包含单文件到 dist\声卡控制台-win-x64
./build.ps1
```

项目结构：

```text
src/
  SoundConsole/             WPF 界面、视图模型、电平表控件和主题
  SoundConsole.Core/        WASAPI 设备、采集/输出引擎、DSP、设置
tests/
  SoundConsole.Core.Tests/  不依赖声卡硬件的单元测试
docs/                       架构、构建、故障排查、截图
.github/                    CI、发布工作流、Issue/PR 模板
```

构建和发布细节见 [构建说明](docs/BUILDING.md)。

## 设计概要

```text
WASAPI 采集（如 Ampero 44.1 kHz / 8 ch）
        │  选定声道对 + 输入增益
        ▼
共享立体声环形缓冲（2 秒容量，线程安全）
        │  每个输出独立三次插值 + 时钟漂移校正
        ├──────────────► WASAPI 共享输出（48/96 kHz 流）
        ├──────────────► WASAPI 独占输出（44.1/48/96 kHz）
        └──────────────► 虚拟声卡播放端
```

每个输出维护自己的读取位置，并用极小比例调整重采样比，让共享缓冲保持目标水位，避免延迟累积或频繁硬重置。更多细节见 [架构说明](docs/ARCHITECTURE.md)。

## 验证情况

在本机 Windows 11 x64 上实测：

- Ampero II Stomp（44.1 kHz / 8 声道）→ TOPPING USB DAC：连续运行 60 秒，0 次缓冲丢帧。
- Ampero → Redmi 电脑音箱独占模式：启动、停止、关闭程序均正常。
- INZONE H3 8 声道端点：共享输出正常；独占失败时自动回退共享。
- 同时输出到两个设备：两路电平表独立工作。
- 关闭窗口时正在路由：自动淡出、停止并释放设备。

不同电脑的 USB 控制器、驱动和 Windows 音频设置可能产生不同结果。遇到问题时请附上日志提交 Issue。

## 限制

- 仅支持 Windows；基于 WPF、WASAPI 和 [NAudio](https://github.com/naudio/NAudio)。
- 不提供 ASIO 驱动、内置虚拟声卡、录音或效果处理。
- 多个输出由各自时钟驱动，不是采样级对齐的监听总线。
- 界面目前为中文；英文界面本地化欢迎 PR。
- 蓝牙/虚拟设备可能带来明显延迟。

本项目与 Hotone、Ampero、Sony、TOPPING 等硬件厂商没有关联；产品名称仅用于说明兼容性。

## 参与贡献

欢迎提交 Bug、硬件兼容性测试结果、文档改进和翻译：

- 提交 PR 前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。
- 请使用 Issue 模板提供可复现信息。
- 提交前运行 `dotnet test SoundConsole.sln -c Release`。
- 安全问题请按 [SECURITY.md](SECURITY.md) 私下报告。

## 开源协议

本项目使用 [MIT License](LICENSE)。第三方组件声明见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

如果这个项目对你有帮助，欢迎 Star 或提交你的设备兼容性测试结果。
