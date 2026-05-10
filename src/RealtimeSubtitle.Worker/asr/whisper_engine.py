from __future__ import annotations

import os
import site
import sys
from typing import Any

import numpy as np


class WhisperEngine:
    def __init__(self, config: dict[str, Any]) -> None:
        add_nvidia_dll_directories()
        try:
            from faster_whisper import WhisperModel
        except ImportError as exc:
            raise RuntimeError("缺少 faster-whisper，请先安装 worker 依赖") from exc

        self.language = str(config.get("language", "ja"))
        model = str(config.get("model", "large-v3-turbo"))
        device = str(config.get("device", "cuda"))
        compute_type = str(config.get("computeType", "float16"))
        model_cache_dir = str(config.get("modelCacheDir", "models")).strip()
        if model_cache_dir and not os.path.isabs(model_cache_dir):
            model_cache_dir = os.path.abspath(model_cache_dir)

        try:
            kwargs: dict[str, Any] = {"device": device, "compute_type": compute_type}
            if model_cache_dir:
                os.makedirs(model_cache_dir, exist_ok=True)
                kwargs["download_root"] = model_cache_dir
            self.model = WhisperModel(model, **kwargs)
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

    def transcribe_partial(self, audio: np.ndarray) -> str:
        segments, _ = self.model.transcribe(
            audio,
            language=self.language,
            beam_size=1,
            vad_filter=False,
            condition_on_previous_text=False,
            without_timestamps=True,
            temperature=0.0,
            compression_ratio_threshold=2.4,
            log_prob_threshold=-1.0,
            no_speech_threshold=0.6,
        )
        return "".join(segment.text for segment in segments).strip()


def add_nvidia_dll_directories() -> None:
    roots = list(site.getsitepackages())
    user_site = site.getusersitepackages()
    if user_site:
        roots.append(user_site)
    executable_dir = os.path.dirname(sys.executable)
    roots.extend(
        root
        for root in (
            getattr(sys, "_MEIPASS", ""),
            executable_dir,
            os.path.join(executable_dir, "worker"),
        )
        if root
    )

    dll_dirs: list[str] = []
    for root in roots:
        nvidia_root = os.path.join(root, "nvidia")
        if not os.path.isdir(nvidia_root):
            continue

        for package_name in ("cublas", "cuda_nvrtc", "cudnn"):
            dll_dir = os.path.join(nvidia_root, package_name, "bin")
            if os.path.isdir(dll_dir):
                dll_dirs.append(dll_dir)

    for dll_dir in dll_dirs:
        try:
            os.add_dll_directory(dll_dir)
        except (FileNotFoundError, OSError):
            pass

    if dll_dirs:
        os.environ["PATH"] = os.pathsep.join(dll_dirs + [os.environ.get("PATH", "")])
