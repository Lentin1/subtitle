from __future__ import annotations

from typing import Any

import numpy as np


class WhisperEngine:
    def __init__(self, config: dict[str, Any]) -> None:
        try:
            from faster_whisper import WhisperModel
        except ImportError as exc:
            raise RuntimeError("缺少 faster-whisper，请先安装 worker 依赖") from exc

        self.language = str(config.get("language", "ja"))
        model = str(config.get("model", "large-v3-turbo"))
        device = str(config.get("device", "cuda"))
        compute_type = str(config.get("computeType", "float16"))

        try:
            self.model = WhisperModel(model, device=device, compute_type=compute_type)
        except Exception as exc:
            if device == "cuda":
                raise RuntimeError("CUDA 模型加载失败，可在配置中切换为 CPU 或检查 NVIDIA 驱动/CUDA 依赖") from exc
            raise

    def transcribe(self, audio: np.ndarray) -> str:
        segments, _ = self.model.transcribe(
            audio,
            language=self.language,
            beam_size=5,
            vad_filter=False,
            condition_on_previous_text=False,
        )
        return "".join(segment.text for segment in segments).strip()
