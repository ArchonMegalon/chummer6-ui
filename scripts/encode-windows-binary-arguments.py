#!/usr/bin/env python3
from __future__ import annotations

import json
import sys


def encode(raw: bytes) -> str:
    if not raw:
        values: list[str] = []
    else:
        if not raw.endswith(b"\0"):
            raise ValueError("Windows argument stream is missing its final NUL delimiter")
        values = [chunk.decode("utf-8") for chunk in raw[:-1].split(b"\0")]
    return json.dumps(values, ensure_ascii=True, separators=(",", ":"))


def main() -> int:
    try:
        encoded = encode(sys.stdin.buffer.read())
    except (UnicodeDecodeError, ValueError) as exc:
        print(f"Windows argument encoding failed: {exc}", file=sys.stderr)
        return 1
    print(encoded)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
