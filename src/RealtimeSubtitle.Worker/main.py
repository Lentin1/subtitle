from __future__ import annotations

import base64
import json
import os
import sys
import traceback

from asr.whisper_engine import WhisperEngine
from pipeline import SubtitlePipeline


DEBUG_LOG = False
LOG_PATH = os.path.abspath(os.path.join("logs", "worker.log"))


def emit(event_type: str, **payload: object) -> None:
    payload["type"] = event_type
    print(json.dumps(payload, ensure_ascii=False), flush=True)
    log(f"{event_type}: {payload}")


def log(message: str) -> None:
    if not DEBUG_LOG:
        return

    os.makedirs(os.path.dirname(LOG_PATH), exist_ok=True)
    with open(LOG_PATH, "a", encoding="utf-8") as file:
        file.write(message + "\n")


def configure_logging(config: dict[str, object]) -> None:
    global DEBUG_LOG
    DEBUG_LOG = bool((config.get("app") or {}).get("debugLog", False)) if isinstance(config.get("app"), dict) else False


def download_model(config_path: str) -> int:
    with open(config_path, "r", encoding="utf-8") as file:
        config = json.load(file)

    configure_logging(config)
    asr_config = config.get("asr") or {}
    model = asr_config.get("model", "large-v3-turbo")
    device = asr_config.get("device", "cuda")
    compute_type = asr_config.get("computeType", "float16")
    print(f"正在下载/验证模型: {model} ({device}, {compute_type})", flush=True)
    WhisperEngine(asr_config)
    print(f"模型已可用: {model}", flush=True)
    return 0


def main() -> int:
    if len(sys.argv) >= 3 and sys.argv[1] == "--download-model":
        return download_model(sys.argv[2])

    pipeline: SubtitlePipeline | None = None
    running = False

    emit("status", message="Worker 已启动")
    for raw_line in sys.stdin:
        line = raw_line.strip()
        if not line:
            continue

        try:
            message = json.loads(line)
            msg_type = message.get("type")

            if msg_type == "configure":
                config = message.get("config") or {}
                configure_logging(config)
                pipeline = SubtitlePipeline(config, emit)
                emit("status", message="配置已加载")

            elif msg_type == "control":
                command = message.get("command")
                if command == "start":
                    if pipeline is None:
                        pipeline = SubtitlePipeline({}, emit)
                    pipeline.start()
                    running = True
                    emit("status", message="识别已开始")
                elif command == "stop":
                    running = False
                    if pipeline is not None:
                        pipeline.reset()
                    emit("status", message="识别已暂停")

            elif msg_type == "audio" and running and pipeline is not None:
                audio_bytes = base64.b64decode(message["data"])
                pipeline.add_audio(
                    audio_bytes=audio_bytes,
                    sample_rate=int(message["sampleRate"]),
                    channels=int(message["channels"]),
                    bits_per_sample=int(message["bitsPerSample"]),
                )

        except Exception as exc:  # Keep the host alive and report worker faults.
            emit("error", message=f"{type(exc).__name__}: {exc}")
            log(traceback.format_exc())
            traceback.print_exc(file=sys.stderr)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
