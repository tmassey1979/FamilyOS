#!/usr/bin/env python3
"""Expand mobile/assets/brand-source/*.b64 into mobile/assets PNG files."""
from __future__ import annotations
import base64, json, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MOBILE = ROOT / "mobile"
SRC = MOBILE / "assets" / "brand-source"
DEST = MOBILE / "assets"


def main() -> int:
    manifest = SRC / "manifest.json"
    if not manifest.exists():
        print("No brand-source/manifest.json — nothing to expand")
        return 0
    items = json.loads(manifest.read_text())
    for item in items:
        rel = item["path"]
        sources = item.get("sources")
        if not sources:
            single = item.get("source")
            sources = [single] if single else []
        chunks: list[str] = []
        for s in sources:
            if not s:
                continue
            src = SRC / Path(s).name
            if not src.exists():
                print(f"missing {src}", file=sys.stderr)
                return 1
            chunks.append(src.read_text().strip())
        data = base64.b64decode("".join(chunks))
        out = DEST / rel
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_bytes(data)
        print(f"expanded {rel} ({len(data)} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
