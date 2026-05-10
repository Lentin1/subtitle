from __future__ import annotations

from concurrent.futures import Future, ThreadPoolExecutor
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
        self.partial_seconds = 0.0
        self.last_partial_text = ""
        self.partial_generation = 0
        self.partial_future: Future[str] | None = None
        self.partial_executor = ThreadPoolExecutor(max_workers=1, thread_name_prefix="partial-asr")
        self.translation_executor = ThreadPoolExecutor(max_workers=2, thread_name_prefix="translate")
        self.translation_generation = 0
        self.translation_history: deque[tuple[str, str]] = deque(maxlen=3)
        self.partial_translation_generation = 0
        self.last_partial_translation_text = ""

        self.stable_silence_ms = int(self.asr_config.get("stableSilenceMs", 900))
        self.max_segment_seconds = int(self.asr_config.get("maxSegmentSeconds", 4))
        self.partial_interval_seconds = float(self.asr_config.get("partialIntervalSeconds", 0.8))
        self.partial_min_seconds = float(self.asr_config.get("partialMinSeconds", 1.2))
        self.partial_max_seconds = float(self.asr_config.get("partialMaxSeconds", 2.8))
        self.partial_translate_enabled = bool(self.translate_config.get("partialTranslateEnabled", True))
        self.partial_translate_min_chars = int(self.translate_config.get("partialTranslateMinChars", 8))
        self.energy_threshold = 0.006
        self.last_progress_seconds = 0

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
        self.partial_generation += 1
        self.translation_generation += 1
        self.partial_translation_generation += 1

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
            self.emit_progress_if_needed()
            self.emit_partial_if_needed()
            self.was_speaking = True
        elif self.segment_parts:
            self.segment_parts.append(audio)
            self.segment_seconds += audio.size / 16000
            self.silence_ms += chunk_ms
            self.emit_progress_if_needed()

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

        try:
            japanese = self.engine.transcribe(audio).strip()
        except Exception as exc:
            self.emit("error", message=f"识别失败: {exc}")
            return

        if not japanese:
            return

        self.partial_translation_generation += 1
        self.emit("subtitle", japanese=japanese, chinese="")
        self.schedule_translation(japanese)

    def translate(self, japanese: str, context: list[str]) -> str:
        if japanese in self.translation_cache:
            return self.translation_cache[japanese]

        try:
            chinese = self.translator.translate(japanese, context)
        except Exception as exc:
            self.emit("error", message=f"翻译失败: {exc}")
            chinese = "翻译失败"

        self.translation_cache[japanese] = chinese
        return chinese

    def schedule_translation(self, japanese: str) -> None:
        if japanese in self.translation_cache:
            self.emit_translation(japanese, self.translation_cache[japanese])
            return

        context = self.context.items()
        generation = self.translation_generation
        future = self.translation_executor.submit(self.translate, japanese, context)
        future.add_done_callback(lambda result: self.handle_translation_result(result, generation, japanese))

    def handle_translation_result(self, future: Future[str], generation: int, japanese: str) -> None:
        if generation != self.translation_generation:
            return

        try:
            chinese = future.result()
        except Exception as exc:
            self.emit("error", message=f"翻译失败: {exc}")
            chinese = "翻译失败"

        self.context.add(japanese)
        self.emit_translation(japanese, chinese)

    def emit_translation(self, japanese: str, chinese: str) -> None:
        if not chinese:
            return

        self.translation_history.append((japanese, chinese))
        self.emit("subtitle", japanese=japanese, chinese=chinese)

    def reset_segment(self) -> None:
        self.segment_parts.clear()
        self.segment_seconds = 0.0
        self.silence_ms = 0.0
        self.was_speaking = False
        self.last_progress_seconds = 0
        self.partial_seconds = 0.0
        self.last_partial_text = ""
        self.partial_generation += 1
        self.partial_translation_generation += 1
        self.last_partial_translation_text = ""

    def emit_progress_if_needed(self) -> None:
        current_seconds = int(self.segment_seconds)
        if current_seconds > 0 and current_seconds != self.last_progress_seconds:
            self.last_progress_seconds = current_seconds

    def emit_partial_if_needed(self) -> None:
        if self.engine is None or not self.segment_parts:
            return

        if self.partial_future is not None and not self.partial_future.done():
            return

        if self.segment_seconds - self.partial_seconds < self.partial_interval_seconds:
            return

        self.partial_seconds = self.segment_seconds
        audio = np.concatenate(self.segment_parts)
        if audio.size < int(self.partial_min_seconds * 16000):
            return

        max_samples = int(self.partial_max_seconds * 16000)
        if audio.size > max_samples:
            audio = audio[-max_samples:]

        generation = self.partial_generation
        self.partial_future = self.partial_executor.submit(self.engine.transcribe_partial, audio.copy())
        self.partial_future.add_done_callback(lambda future: self.handle_partial_result(future, generation))

    def handle_partial_result(self, future: Future[str], generation: int) -> None:
        if generation != self.partial_generation:
            return

        try:
            japanese = future.result().strip()
        except Exception as exc:
            self.emit("error", message=f"流式识别失败: {exc}")
            return

        if japanese and japanese != self.last_partial_text:
            self.last_partial_text = japanese
            self.emit("subtitle", japanese=japanese, chinese="")
            self.schedule_partial_translation(japanese)

    def schedule_partial_translation(self, japanese: str) -> None:
        if not self.partial_translate_enabled:
            return

        normalized = japanese.strip()
        if len(normalized) < self.partial_translate_min_chars:
            return

        if normalized == self.last_partial_translation_text:
            return

        self.last_partial_translation_text = normalized
        context = self.context.items()
        generation = self.partial_translation_generation
        future = self.translation_executor.submit(self.translate, normalized, context)
        future.add_done_callback(lambda result: self.handle_partial_translation_result(result, generation, normalized))

    def handle_partial_translation_result(self, future: Future[str], generation: int, japanese: str) -> None:
        if generation != self.partial_translation_generation:
            return

        try:
            chinese = future.result()
        except Exception as exc:
            self.emit("error", message=f"预览翻译失败: {exc}")
            return

        if chinese:
            self.emit("subtitle", japanese=japanese, chinese=chinese)


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
