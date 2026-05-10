# 实时日语字幕翻译工具

这是一个 Windows 11 桌面 MVP，用于播放日语视频时实时显示日文识别和中文翻译。当前实现基于 WPF 桌面端、WASAPI 系统声音采集、Python worker、`faster-whisper` 和 DeepSeek API。

## 当前功能

- 托盘菜单手动开始、暂停、显示、隐藏、设置、退出。
- 透明置顶字幕窗，支持拖动和双击隐藏。
- 采集默认输出设备的系统声音。
- 日文流式预览识别，最终句确认后稳定显示。
- DeepSeek 中文翻译，支持 partial 日文预览翻译。
- 本地 JSON 配置文件。

## 环境要求

- Windows 11。
- .NET 8 SDK。
- Conda 环境，建议 Python 3.11。
- NVIDIA 驱动。GPU 模式还需要 CUDA 相关 Python 包。
- DeepSeek API Key。

## Conda 环境

```powershell
conda create -n realtime-subtitle python=3.11
conda activate realtime-subtitle
pip install -r src\RealtimeSubtitle.Worker\requirements.txt
pip install nvidia-cublas-cu12 nvidia-cudnn-cu12
```

如果暂时不用 GPU，可以不装 CUDA 包，并把配置改成 CPU：

```json
"device": "cpu",
"computeType": "int8"
```

## 配置

首次运行前复制配置：

```powershell
Copy-Item config\default_config.json config\config.json
```

编辑 `config\config.json`：

```json
"worker": {
  "pythonPath": "D:\\Anaconda\\envs\\realtime-subtitle\\python.exe",
  "scriptPath": "src/RealtimeSubtitle.Worker/main.py"
}
```

填入 DeepSeek API Key：

```json
"translate": {
  "apiKey": "你的 DeepSeek API Key"
}
```

GPU 推荐配置：

```json
"asr": {
  "model": "large-v3-turbo",
  "device": "cuda",
  "computeType": "float16"
}
```

## 源码运行

```powershell
dotnet restore src\RealtimeSubtitle.App\RealtimeSubtitle.App.csproj
dotnet run --project src\RealtimeSubtitle.App\RealtimeSubtitle.App.csproj
```

启动后在右下角托盘图标中点击“开始识别”，然后播放日语视频或音频。

## exe 启动

已发布的本机 exe 位于：

```text
dist\RealtimeSubtitle\RealtimeSubtitle.App.exe
```

也可以运行：

```powershell
.\run-packaged.ps1
```

当前 exe 仍依赖本机 conda 环境中的 Python worker，不是完整绿色版。

## 常见问题

- `Worker IPC 已断开`：检查 `worker.pythonPath` 是否是 conda 环境里的 `python.exe` 绝对路径。
- `cublas64_12.dll is not found`：安装 `nvidia-cublas-cu12` 和 `nvidia-cudnn-cu12`，或切换 CPU 模式。
- 中文翻译慢：DeepSeek 网络响应较慢时会延迟；当前已使用后台翻译，不会阻塞日文识别。
