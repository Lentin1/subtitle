# 实时日语字幕翻译工具

Windows 桌面实时字幕工具，用于播放日语视频时显示日文识别和中文翻译。当前版本目标是 `v2`：支持绿色发行包、模型下载/选择、按进程采集、字幕样式配置和调试日志。

## 功能

- 托盘菜单：开始、暂停、显示、隐藏、设置、退出。
- 透明置顶字幕窗，支持拖动、双击隐藏和自动隐藏。
- 系统声音采集，以及基于 Windows Process Loopback 的按进程监听。
- F8 全局热键开始/暂停识别。
- `faster-whisper` 日文流式预览识别和最终句确认。
- DeepSeek 中文翻译，支持上下文和 partial 预览翻译。
- 设置窗口可调整模型、API Key、字幕样式、热键和调试日志。

## 使用方式

推荐普通用户下载 GitHub Releases 里的绿色版 zip。开发者或需要修改代码时，再从源码构建。

## 方式一：下载发行版

1. 打开本仓库的 GitHub Releases 页面。
2. 下载类似下面名称的压缩包：

```text
RealtimeSubtitle-v2-green.zip
```

3. 解压到任意目录。
4. 运行：

```text
RealtimeSubtitle.App.exe
```

5. 打开托盘菜单里的“设置”，填写 DeepSeek API Key。
6. 选择 Whisper 模型，点击“下载/验证模型”。
7. 保存设置后，托盘菜单点击“开始识别”。

发行版内置 .NET self-contained 主程序、PyInstaller worker 和 CUDA Python DLL 依赖。新机器仍需要安装 NVIDIA 驱动。

## 发行版目录

解压后的关键目录：

```text
RealtimeSubtitle.App.exe
config\config.json
worker\RealtimeSubtitle.Worker.exe
worker\nvidia\
models\
logs\
```

`config/config.json` 保存本机设置和 API Key。`models/` 保存下载后的 Whisper 模型。`logs/` 保存调试日志。

## 方式二：从源码运行

开发环境要求：

- Windows 11
- .NET 8 SDK
- Conda / Python 3.11
- NVIDIA 驱动
- DeepSeek API Key

安装 Python 依赖：

```powershell
conda create -n realtime-subtitle python=3.11
conda activate realtime-subtitle
pip install -r src\RealtimeSubtitle.Worker\requirements.txt
pip install nvidia-cublas-cu12 nvidia-cudnn-cu12
```

准备配置：

```powershell
Copy-Item config\default_config.json config\config.json
notepad config\config.json
```

源码运行：

```powershell
dotnet run --project src\RealtimeSubtitle.App\RealtimeSubtitle.App.csproj
```

源码模式下，`worker.pythonPath` 应指向 conda 环境里的 `python.exe`，`worker.scriptPath` 使用 `src/RealtimeSubtitle.Worker/main.py`。

## 模型管理

设置窗口支持选择或输入 Whisper 模型：

```text
small
medium
large-v3-turbo
large-v3
```

模型缓存目录默认为：

```text
models
```

首次点击“下载/验证模型”需要联网。下载完成后模型会留在 `models/`，后续启动会复用缓存。

## 字幕样式

设置窗口支持持久保存：

- 日文/中文字幕字号和颜色。
- 字体、背景颜色、背景透明度。
- 圆角、内边距、行距。
- 阴影模糊、阴影透明度。
- 左/中/右对齐。
- 自动隐藏秒数。

## 调试日志

设置中启用“调试日志”后会写入：

```text
logs\app.log
logs\worker.log
```

## 常见问题

- `Worker IPC 已断开`：发行版检查 `worker\RealtimeSubtitle.Worker.exe`，源码模式检查 `worker.pythonPath`。
- `cublas64_12.dll is not found`：确认发行版里有 `worker\nvidia`，并且机器已安装 NVIDIA 驱动。
- 按进程监听无声：确认设置里选择的是正在播放声音的进程 ID。
- 翻译慢：通常是 DeepSeek 网络响应慢；日文识别不会等待翻译完成。
