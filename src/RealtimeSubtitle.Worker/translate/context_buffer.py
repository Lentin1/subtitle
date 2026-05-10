from __future__ import annotations

from collections import deque


class ContextBuffer:
    def __init__(self, limit: int) -> None:
        self._items: deque[str] = deque(maxlen=max(0, limit))

    def add(self, text: str) -> None:
        if text:
            self._items.append(text)

    def items(self) -> list[str]:
        return list(self._items)
