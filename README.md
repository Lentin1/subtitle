# Realtime Subtitle MVP

Windows 11 desktop MVP for real-time Japanese subtitles and Chinese translation. The app follows `realtime-subtitle-technical-design.md`: WPF host, WASAPI device loopback capture, Python worker, `faster-whisper`, and DeepSeek translation.

## Current Scope

- Manual start/pause from tray menu.
- Transparent topmost subtitle window with draggable position.
- Default output device loopback capture through NAudio.
- JSON Lines IPC between WPF and Python.
- Worker-side VAD, segment finalization, Japanese ASR, DeepSeek translation, and context cache.
- Basic settings window and local JSON config.

## Prerequisites

- .NET 8 SDK with Windows Desktop workload.
- Python 3.10+.
- NVIDIA driver and CUDA-compatible environment for the default `cuda` setting, or change `asr.device` to `cpu`.
- DeepSeek API key.

## Setup

```powershell
python -m venv .venv
.\.venv\Scripts\pip install -r src\RealtimeSubtitle.Worker\requirements.txt
dotnet restore src\RealtimeSubtitle.App\RealtimeSubtitle.App.csproj
```

Edit `config/config.json` after the first run, or copy `config/default_config.json` to `config/config.json` and set:

- `worker.pythonPath`: for example `.venv\\Scripts\\python.exe`
- `translate.apiKey`: your DeepSeek API key
- `asr.model`: `large-v3-turbo`, `large-v3`, `medium`, or another faster-whisper model

## Run

```powershell
dotnet run --project src\RealtimeSubtitle.App\RealtimeSubtitle.App.csproj
```

Use the tray icon to start or pause recognition. Play Japanese audio through the default output device; the subtitle window shows detected Japanese first and Chinese after the segment is finalized.
