#!/usr/bin/env python3
"""Expand scripts/eas-brand-bundle/*.b64 into mobile/assets PNG files for EAS."""
from __future__ import annotations
import base64, io, tarfile, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BUNDLE = ROOT / "scripts" / "eas-brand-bundle"
DEST = ROOT / "mobile" / "assets"

def main() -> int:
    parts = sorted(BUNDLE.glob("eas-brand.tar.gz.part*.b64"),
                   key=lambda p: int(p.name.rsplit("part", 1)[-1].split(".")[0]))
    if not parts:
        print("No eas-brand-bundle parts — skip")
        return 0
    b64 = "".join(p.read_text().strip() for p in parts)
    raw = base64.b64decode(b64)
    DEST.mkdir(parents=True, exist_ok=True)
    with tarfile.open(fileobj=io.BytesIO(raw), mode="r:gz") as tar:
        tar.extractall(DEST)
        for m in tar.getmembers():
            if m.isfile():
                print(f"extracted {m.name} ({m.size} bytes)")
    for name in ("icon.png", "adaptive-icon.png", "splash-icon.png", "favicon.png"):
        p = DEST / name
        if not p.exists() or p.stat().st_size < 50:
            print(f"ERROR missing {p}", file=sys.stderr)
            return 1
        if p.read_bytes()[:8] != b"\x89PNG\r\n\x1a\n":
            print(f"ERROR not PNG {p}", file=sys.stderr)
            return 1
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
