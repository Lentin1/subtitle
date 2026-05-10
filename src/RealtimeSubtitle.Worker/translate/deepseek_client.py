from __future__ import annotations

import json
import urllib.error
import urllib.request
from typing import Any


class DeepSeekClient:
    endpoint = "https://api.deepseek.com/chat/completions"

    def __init__(self, config: dict[str, Any]) -> None:
        self.api_key = str(config.get("apiKey", "")).strip()
        self.model = str(config.get("model", "deepseek-chat"))
        self.timeout = int(config.get("timeoutSeconds", 5))

    def translate(self, current: str, context: list[str]) -> str:
        if not self.api_key:
            return "未配置 DeepSeek API Key"

        system_prompt = (
            "你是日语到中文的字幕翻译器。根据上下文翻译当前句，"
            "只输出自然中文译文，不要解释，不要添加引号。"
        )
        user_prompt = "上下文：\n"
        if context:
            user_prompt += "\n".join(context)
        else:
            user_prompt += "无"
        user_prompt += f"\n\n当前句：\n{current}"

        payload = {
            "model": self.model,
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt},
            ],
            "temperature": 0.2,
        }

        request = urllib.request.Request(
            self.endpoint,
            data=json.dumps(payload).encode("utf-8"),
            headers={
                "Authorization": f"Bearer {self.api_key}",
                "Content-Type": "application/json",
            },
            method="POST",
        )

        try:
            with urllib.request.urlopen(request, timeout=self.timeout) as response:
                body = json.loads(response.read().decode("utf-8"))
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"DeepSeek HTTP {exc.code}: {detail}") from exc

        return str(body["choices"][0]["message"]["content"]).strip()
