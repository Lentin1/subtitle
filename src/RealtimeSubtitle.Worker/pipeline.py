from __future__ import annotations

from collections import deque
from typing import Any, Callable

import numpy as np

from asr.whisper_engine import WhisperEngine
from translate.context_buffer import ContextBuffer
from translate.deepseek_client import DeepSeekClient

Emitter = Callable[[str], None]


class SubtitlePipeline:
    def __init__(self, config: dict[str, Any], emit: Callable[..., None]) -> None:
        self.config = config
        self.emit = emit
        self.asr_config = config.get("asr", {})
        self.translate_config = config.get("translate", {})
        self.debug_log = bool(config.get("app", {}).get("debugLog", False))

        self.engine: WhisperEngine | None = None
        self.translator = DeepSeekClient(self.translate_config)
        self.context = ContextBuffer(int(self.translate_config.get("contextSentences", 5)))
        self.translation_cache: dict[str, str] = {}

        self.segment_parts: list[np.ndarray] = []
        self.segment_seconds = 0.0
        self.silence_ms = 0.0
        self.was_speaking = False
        self.started = False

        self.stable_silence_ms = int(self.asr_config.get("stableSilenceMs", 900))
        self.max_segment_seconds = int(self.asr_config.get("maxSegmentSeconds", 10))
        self.energy_threshold = 0.006

    def start(self) -> None:
        if self.engine is None:
            self.emit("status", message="正在加载 Whisper 模型...")
            self.engine = WhisperEngine(self.asr_config)
            self.emit("status", message="Whisper 模型已加载")
        self.started = True

    def reset(self) -> None:
        self.segment_parts.clear()
        self.segment_seconds = 0.0
        self.silence_ms = 0.0
        self.was_speaking = False
        self.started = False

    def add_audio(self, audio_bytes: bytes, sample_rate: int, channels: int, bits_per_sample: int) -> None:
        if not self.started:
            return

        audio = decode_pcm(audio_bytes, sample_rate, channels, bits_per_sample)
        if audio.size == 0:
            return

        chunk_ms = audio.size / 16000 * 1000
        energy = float(np.sqrt(np.mean(np.square(audio)))) if audio.size else 0.0
        is_voice = energy >= self.energy_threshold

        if is_voice:
            self.segment_parts.append(audio)
            self.segment_seconds += audio.size / 16000
            self.silence_ms = 0.0
            if not self.was_speaking:
                self.emit("status", message="检测到语音")
            self.was_speaking = True
        elif self.segment_parts:
            self.segment_parts.append(audio)
            self.segment_seconds += audio.size / 16000
            self.silence_ms += chunk_ms

        if self.segment_parts and (
            self.silence_ms >= self.stable_silence_ms
            or self.segment_seconds >= self.max_segment_seconds
        ):
            self.finalize_segment()

    def finalize_segment(self) -> None:
        if self.engine is None or not self.segment_parts:
            self.reset_segment()
            return

        audio = np.concatenate(self.segment_parts)
        self.reset_segment()

        japanese = self.engine.transcribe(audio).strip()
        if not japanese:
            return

        self.emit("subtitle", japanese=japanese, chinese="")
        chinese = self.translate(japanese)
        self.context.add(japanese)
        self.emit("subtitle", japanese=japanese, chinese=chinese)

    def translate(self, japanese: str) -> str:
        if japanese in self.translation_cache:
            return self.translation_cache[japanese]

        try:
            chinese = self.translator.translate(japanese, self.context.items())
        except Exception as exc:
            self.emit("error", message=f"翻译失败: {exc}")
            chinese = "翻译失败"

        self.translation_cache[japanese] = chinese
        return chinese

    def reset_segment(self) -> None:
        self.segment_parts.clear()
        self.segment_seconds = 0.0
        self.silence_ms = 0.0
        self.was_speaking = False


def decode_pcm(audio_bytes: bytes, sample_rate: int, channels: int, bits_per_sample: int) -> np.ndarray:
    if bits_per_sample == 32:
        data = np.frombuffer(audio_bytes, dtype=np.float32)
    elif bits_per_sample == 16:
        data = np.frombuffer(audio_bytes, dtype=np.int16).astype(np.float32) / 32768.0
    else:
        raise ValueError(f"Unsupported PCM depth: {bits_per_sample}")

    if channels > 1:
        frames = data.size // channels
        data = data[: frames * channels].reshape(frames, channels).mean(axis=1)

    if sample_rate != 16000:
        data = resample_linear(data.astype(np.float32), sample_rate, 16000)

    return np.clip(data.astype(np.float32), -1.0, 1.0)


def resample_linear(audio: np.ndarray, src_rate: int, dst_rate: int) -> np.ndarray:
    if audio.size == 0 or src_rate == dst_rate:
        return audio

    duration = audio.size / src_rate
    dst_size = max(1, int(duration * dst_rate))
    src_positions = np.linspace(0, audio.size - 1, num=dst_size)
    return np.interp(src_positions, np.arange(audio.size), audio).astype(np.float32)
