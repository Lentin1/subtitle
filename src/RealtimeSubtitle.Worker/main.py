from __future__ import annotations

import base64
import json
import sys
import traceback

from pipeline import SubtitlePipeline


def emit(event_type: str, **payload: object) -> None:
    payload["type"] = event_type
    print(json.dumps(payload, ensure_ascii=False), flush=True)


def main() -> int:
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
                pipeline = SubtitlePipeline(message.get("config") or {}, emit)
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
            traceback.print_exc(file=sys.stderr)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
